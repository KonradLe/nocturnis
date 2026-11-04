using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Krypta.Editor
{
    // Editor-only deterministic retarget. Runtime remains the existing Legacy
    // Animation stack; imported Animator/avatar state never controls movement.
    public static class NocturnisUalImport
    {
        public const string Source = "Assets/Art/UniversalAnimationLibrary/UAL1_Standard.fbx";
        public const string Folder = "Assets/NocturnisAnimations/UAL";
        public const string OriginalLibrary = Folder + "/OriginalActILibrary.asset";
        public const string OriginalConfig = Folder + "/OriginalKryptaAssets.asset";
        public static readonly string[] Heroes = {
            "hero_inquisitor", "hero_blood_weaver", "hero_dusk_hunter",
            "hero_void_herald", "hero_root_guardian"
        };
        const int Rate = 60;
        static readonly string[] TargetNames = {
            "hips", "chest", "head",
            "armR", "forearmR", "handR", "armL", "forearmL", "handL",
            "thighR", "shinR", "footR", "thighL", "shinL", "footL"
        };
        static readonly string[] SourceNames = {
            "pelvis", "spine_03", "Head",
            "upperarm_r", "lowerarm_r", "hand_r", "upperarm_l", "lowerarm_l", "hand_l",
            "thigh_r", "calf_r", "foot_r", "thigh_l", "calf_l", "foot_l"
        };
        static readonly int[] SegmentEnd = {-1,-1,-1,4,5,-1,7,8,-1,10,11,-1,13,14,-1};

        [Serializable] public sealed class Mapping
        {
            public string hero, target, source, asset;
            public float sourceLength, sourcePhase, sourceContact, targetContact;
            public float stridePerHeight, minFootHeight, maxHipOffset;
            public int frames;
        }
        [Serializable] public sealed class BakeReport
        {
            public string source = Source, method = "A_TPose world-basis retarget; quaternion bake; no root motion";
            public List<Mapping> mappings = new List<Mapping>();
            public List<string> retained = new List<string>();
            public int heroes;
        }
        public static BakeReport LastBake;

        internal static void Need(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        public static void Run()
        {
            Directory.CreateDirectory("Logs");
            try
            {
                Prepare();
                NocturnisUalRetargetQA.Validate(LastBake);
                Debug.Log("UAL_RETARGET_PASSED " + LastBake.mappings.Count);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                File.WriteAllText("Logs/ual-retarget-qa.json",
                    JsonUtility.ToJson(new Failure { error = e.ToString() }, true));
                EditorApplication.Exit(1);
            }
        }
        [Serializable] sealed class Failure { public bool passed = false; public string error; }

        sealed class SourceRig : IDisposable
        {
            public GameObject model;
            public Transform[] bones;
            public Dictionary<string, AnimationClip> clips;
            public Quaternion inverseFrame;
            public Matrix4x4 coordinateFrame;
            public Matrix4x4[] referenceMatrices;
            public Quaternion[] reference;
            public Vector3[] referencePositions;
            public Vector3 referenceHip;
            public float height;
            readonly Vector3[] positions, scales;
            readonly Quaternion[] rotations;
            readonly Transform[] all;
            public SourceRig()
            {
                model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
                model.name = "UAL_Source_For_Baking";
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animators = model.GetComponentsInChildren<Animator>(true);
                foreach (var a in animators) a.enabled = false;
                foreach (var a in model.GetComponentsInChildren<Animation>(true)) a.enabled = false;
                all = model.GetComponentsInChildren<Transform>(true);
                positions = all.Select(t => t.localPosition).ToArray();
                rotations = all.Select(t => t.localRotation).ToArray();
                scales = all.Select(t => t.localScale).ToArray();
                bones = SourceNames.Select(n => Find(all, n)).ToArray();
                clips = AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__preview__"))
                    .ToDictionary(c => ClipName(c.name), c => c);
                Need(clips.ContainsKey("A_TPose"), "UAL reference A_TPose is absent.");
                Sample(clips["A_TPose"], 0);
                var right = bones[12].position - bones[9].position;
                right.y = 0;
                // Anatomical left is positive X on Nocturnis. Derive the source
                // basis rather than relying on the FBX exporter coordinate axes.
                var up = bones[2].position - bones[0].position;
                up.Normalize(); right = Vector3.ProjectOnPlane(right, up).normalized;
                Need(right.sqrMagnitude > .9f, "Degenerate UAL left/right basis.");
                var forward = Vector3.Cross(right, up).normalized;
                var toeLeft=Find(all,"ball_l");var toeRight=Find(all,"ball_r");
                var toeDirection=(toeLeft.position-bones[14].position)+(toeRight.position-bones[11].position);
                toeDirection=Vector3.ProjectOnPlane(toeDirection,up).normalized;
                Need(Mathf.Abs(Vector3.Dot(forward,toeDirection))>.7f,"Unclear UAL forward direction from toe bones.");
                // FBX and glTF in this package differ in handedness. A left/up
                // cross product alone points backwards for the Unity FBX.
                // Preserve anatomical side, and orient +Z by the actual toes.
                if(Vector3.Dot(forward,toeDirection)<0)forward=-forward;
                coordinateFrame=Matrix4x4.identity;
                coordinateFrame.SetRow(0,new Vector4(right.x,right.y,right.z,0));
                coordinateFrame.SetRow(1,new Vector4(up.x,up.y,up.z,0));
                coordinateFrame.SetRow(2,new Vector4(forward.x,forward.y,forward.z,0));
                inverseFrame=Quaternion.Inverse(Quaternion.LookRotation(Vector3.Cross(right,up),up));
                referenceMatrices=bones.Select(t=>t.localToWorldMatrix).ToArray();
                reference=bones.Select(t=>inverseFrame*t.rotation).ToArray();
                referencePositions=bones.Select(t=>coordinateFrame.MultiplyPoint3x4(t.position)).ToArray();
                referenceHip = referencePositions[0];
                height = (referencePositions[2].y - Mathf.Min(referencePositions[11].y, referencePositions[14].y)) / .785f;
                Need(height > .01f && height < 1000, "Invalid UAL source height " + height);
            }
            public void Sample(AnimationClip clip, float time)
            {
                for (int i=0; i<all.Length; i++)
                {
                    all[i].localPosition = positions[i];
                    all[i].localRotation = rotations[i];
                    all[i].localScale = scales[i];
                }
                clip.SampleAnimation(model, Mathf.Clamp(time, 0, clip.length));
            }
            public Vector3 Position(int index) { return coordinateFrame.MultiplyPoint3x4(bones[index].position); }
            public Quaternion RotationDelta(int index)
            {
                // Both reference/current world matrices include ancestor scale.
                // Conjugating the relative delta also handles reflected bases:
                // det(C) may be -1, but det(C * delta * inverse(C)) remains +1.
                var delta=coordinateFrame*bones[index].localToWorldMatrix*referenceMatrices[index].inverse*coordinateFrame.inverse;
                return Quaternion.LookRotation(delta.GetColumn(2),delta.GetColumn(1));
            }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(model); }
        }
        static string ClipName(string name)
        {
            int p = name.LastIndexOf('|');
            return p < 0 ? name : name.Substring(p+1);
        }
        static Transform Find(Transform[] all, string name)
        {
            var matches = all.Where(t => t.name == name).ToArray();
            Need(matches.Length == 1, "Expected one bone named " + name + ", found " + matches.Length);
            return matches[0];
        }
        sealed class TargetRig : IDisposable
        {
            public GameObject model;
            public Transform[] bones;
            public Quaternion[] basis;
            public Vector3[] localPositions;
            public Vector3[] referencePositions;
            public Quaternion[] localRotations;
            public string[] paths;
            public float height, floorJoint;
            public Vector3 hip;
            public TargetRig(GameObject prefab, SourceRig source)
            {
                model = UnityEngine.Object.Instantiate(prefab);
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var all = model.GetComponentsInChildren<Transform>(true);
                bones = TargetNames.Select(n => Find(all,n)).ToArray();
                localPositions = bones.Select(t => t.localPosition).ToArray();
                referencePositions = bones.Select(t => t.position).ToArray();
                localRotations = bones.Select(t => t.localRotation).ToArray();
                paths = bones.Select(t => AnimationUtility.CalculateTransformPath(t, model.transform)).ToArray();
                var profile = model.GetComponent<ActorModelProfile>();
                Need(profile && profile.bodyHeight > 0, "Target body height missing: " + prefab.name);
                height = profile.bodyHeight;
                floorJoint = Mathf.Min(bones[11].position.y, bones[14].position.y);
                hip = bones[0].localPosition;
                basis = new Quaternion[bones.Length];
                for (int i=0; i<bones.Length; i++)
                {
                    var desiredReference = bones[i].rotation;
                    int child = SegmentEnd[i];
                    if (child >= 0)
                    {
                        var destinationVector = bones[child].position - bones[i].position;
                        var sourceVector = source.referencePositions[child] - source.referencePositions[i];
                        desiredReference = Quaternion.FromToRotation(destinationVector, sourceVector) * desiredReference;
                    }
                    // The target has no finger skeleton. Hands keep the same
                    // reference transport as the forearm, preserving rigid grip.
                    if (i == 5 || i == 8) desiredReference = basis[i-1];
                    basis[i] = desiredReference;
                }
            }
            public void Pose(SourceRig src, bool grounded, bool death, float sourceGround=float.NaN)
            {
                for (int i=0;i<bones.Length;i++)
                {
                    bones[i].localPosition=localPositions[i];
                    bones[i].localRotation=localRotations[i];
                }
                // World-to-local conversion collapses the source spine/clavicle
                // chain correctly into the shorter destination hierarchy.
                for (int i=0;i<bones.Length;i++)
                    bones[i].rotation=src.RotationDelta(i)*basis[i];
                for(int i=0;i<bones.Length;i++)if(SegmentEnd[i]>=0)
                {
                    int child=SegmentEnd[i];
                    float error=Vector3.Angle(bones[child].position-bones[i].position,src.Position(child)-src.Position(i));
                    Need(error<.5f,"Retarget segment direction diverged "+bones[i].name+": "+error);
                }
                var delta=(src.Position(0)-src.referenceHip)*(height/src.height);
                // CharacterController owns horizontal travel; only bounded
                // body sway is retained, never imported armature root motion.
                delta.x=Mathf.Clamp(delta.x,-height*.025f,height*.025f);
                delta.z=Mathf.Clamp(delta.z,-height*.035f,height*.035f);
                if (death)
                {
                    delta.x=0; delta.z=0;
                    delta.y=Mathf.Max(-height*.52f,delta.y);
                }
                bones[0].localPosition=hip+delta;
                if(grounded)
                {
                    float lowest=Mathf.Min(bones[11].position.y,bones[14].position.y);
                    float desired=floorJoint;
                    // A jog has a flight phase. Preserve source foot clearance;
                    // grounding every frame would turn flight into slow sliding
                    // support and corrupt both silhouette and stride estimation.
                    if(!float.IsNaN(sourceGround))desired+=Mathf.Max(0,Mathf.Min(src.Position(11).y,src.Position(14).y)-sourceGround)*(height/src.height);
                    bones[0].position+=Vector3.up*(desired-lowest);
                }
                else if(death)
                {
                    // Contact reference includes the head/hips as the body falls.
                    float low=Mathf.Min(bones[2].position.y-height*.065f,bones[0].position.y-height*.07f);
                    low=Mathf.Min(low,Mathf.Min(bones[11].position.y,bones[14].position.y)-height*.04f);
                    float bottom=hip.y-height*.53f;
                    if(low<bottom)bones[0].position+=Vector3.up*(bottom-low);
                }
            }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(model); }
        }
        static float GaitPhase(SourceRig source, AnimationClip clip, float supportGround)
        {
            // Left foot reaches its forward ground contact at phase zero.
            // Same convention for walk/jog keeps transitions and footfalls aligned.
            int best=-1, count=180; float score=float.NegativeInfinity;
            for(int i=0;i<count;i++)
            {
                source.Sample(clip,clip.length*i/count);
                Vector3 worldFoot=source.Position(14);
                // Footstep events occur at phase 0 and 0.5. Maximum reach may
                // still be in flight; choose the forwardmost supported sample.
                if(worldFoot.y>supportGround+source.height*.02f)continue;
                Vector3 foot=worldFoot-source.Position(0);
                float candidate=foot.z;
                if(candidate>score){score=candidate;best=i;}
            }
            Need(best>=0,"No supported left-foot phase in "+clip.name);
            return best/(float)count;
        }
        static float GaitGround(SourceRig source,AnimationClip clip)
        {
            float minimum=float.PositiveInfinity;
            for(int i=0;i<180;i++)
            {
                source.Sample(clip,clip.length*i/180f);
                minimum=Mathf.Min(minimum,Mathf.Min(source.Position(11).y,source.Position(14).y));
            }
            return minimum;
        }
        static float SwordContact(SourceRig source,TargetRig target,AnimationClip clip)
        {
            // Locate contact from the actual equipped sword, not a metadata
            // constant or a generic hand position. The baked clip is checked
            // independently by sampling the resulting weapon-tip trajectory.
            var original=AssetDatabase.LoadAssetAtPath<KryptaAssets>(OriginalConfig);
            var slot=target.model.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="handslot.r");
            var weapon=UnityEngine.Object.Instantiate(original.heroes[0].weapon,slot,false);
            weapon.transform.localPosition=Vector3.zero;weapon.transform.localRotation=Quaternion.identity;
            try
            {
                var tip=weapon.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="TipAnchor");
                float best=-1,phase=.42f;Vector3 previous=Vector3.zero;
                const int count=240;
                for(int i=0;i<=count;i++)
                {
                    float t=i/(float)count;
                    source.Sample(clip,t*clip.length);target.Pose(source,true,false);
                    Vector3 point=tip.position;
                    if(i>0&&t>.15f&&t<.72f)
                    {
                        var velocity=(point-previous)*count/Mathf.Max(.01f,clip.length);
                        float score=Mathf.Max(0,velocity.z)+Mathf.Max(0,-velocity.y)*.75f;
                        if(score>best){best=score;phase=t;}
                    }
                    previous=point;
                }
                Need(best>target.height*.1f,"Sword contact has no visible equipped tip motion.");
                return phase;
            }
            finally {UnityEngine.Object.DestroyImmediate(weapon);}
        }
        static float RemapContact(float t,float sourceContact)
        {
            return t<=.42f ? t/.42f*sourceContact
                : sourceContact+(t-.42f)/.58f*(1-sourceContact);
        }
        [Serializable] sealed class Pose
        {
            public Quaternion[] rotations;
            public Vector3 hip;
            public float minFoot;
            public Vector3 leftFoot,rightFoot,sourceLeftFoot,sourceRightFoot,sourceHip;
        }
        [Serializable] sealed class BoneDiagnostic
        {
            public string name;public Vector3 position,localScale,lossyScale;
            public Quaternion rotation;public float determinant;
        }
        [Serializable] sealed class GaitDiagnostic
        {
            public string hero,clip;public float targetHeight,sourceHeight,floorJoint,phase;
            public Quaternion inverseSourceFrame;
            public Matrix4x4 sourceCoordinateFrame;
            public Matrix4x4[] sourceReferenceMatrices;
            public Vector3[] sourceReference,targetReference;
            public BoneDiagnostic[] sourceTransforms;
            public Pose[] samples;
        }
        static void SaveGaitDiagnostic(SourceRig source,TargetRig target,string id,string clip,float phase,Pose[] poses)
        {
            Directory.CreateDirectory("Logs/ual-diagnostics");
            var report=new GaitDiagnostic{
                hero=id,clip=clip,phase=phase,targetHeight=target.height,sourceHeight=source.height,
                floorJoint=target.floorJoint,inverseSourceFrame=source.inverseFrame,
                sourceCoordinateFrame=source.coordinateFrame,sourceReferenceMatrices=source.referenceMatrices,
                sourceReference=source.referencePositions,targetReference=target.referencePositions,
                sourceTransforms=source.model.GetComponentsInChildren<Transform>(true).Select(t=>new BoneDiagnostic{
                    name=t.name,position=t.position,localScale=t.localScale,lossyScale=t.lossyScale,rotation=t.rotation,
                    determinant=t.localToWorldMatrix.determinant
                }).ToArray(),samples=poses
            };
            File.WriteAllText("Logs/ual-diagnostics/"+id+"-"+clip+".json",JsonUtility.ToJson(report,true));
        }
        static AnimationClip Bake(SourceRig source,TargetRig target,string id,string sourceName,string targetName,string asset)
        {
            Need(source.clips.ContainsKey(sourceName),"Missing UAL clip "+sourceName);
            var input=source.clips[sourceName];
            bool loop=targetName=="Idle"||targetName=="Walking_A"||targetName=="Running_A"||targetName=="Talk_A";
            bool gait=targetName=="Walking_A"||targetName=="Running_A";
            bool death=targetName=="Death_A";
            bool sword=targetName=="1H_Melee_Attack_Chop";
            float sourceGround=gait?GaitGround(source,input):float.NaN;
            float phase=gait?GaitPhase(source,input,sourceGround):0;
            float contact=sword?SwordContact(source,target,input):0;
            int intervals=Mathf.Max(2,Mathf.CeilToInt(input.length*Rate));
            var poses=new Pose[intervals+1];
            for(int i=0;i<=intervals;i++)
            {
                float t=i/(float)intervals;
                float sampled=sword?RemapContact(t,contact):t;
                if(gait)sampled=Mathf.Repeat(sampled+phase,1);
                source.Sample(input,sampled*input.length);
                target.Pose(source,!death,death,sourceGround);
                poses[i]=new Pose {
                    rotations=target.bones.Select(b=>b.localRotation).ToArray(),
                    hip=target.bones[0].localPosition,
                    minFoot=Mathf.Min(target.bones[11].position.y,target.bones[14].position.y),
                    leftFoot=target.bones[14].position,rightFoot=target.bones[11].position,
                    sourceLeftFoot=source.Position(14),sourceRightFoot=source.Position(11),sourceHip=source.Position(0)
                };
            }
            if(loop)
            {
                // Remove tiny nonperiodic FBX drift over the last tenth of a cycle.
                // Each seam is exact; blend avoids a one-frame wrap discontinuity.
                int blend=Mathf.Max(2,intervals/10);
                Pose end=poses[intervals],start=poses[0];
                for(int i=intervals-blend;i<=intervals;i++)
                {
                    float weight=Mathf.SmoothStep(0,1,(i-(intervals-blend))/(float)blend);
                    poses[i].hip+=weight*(start.hip-end.hip);
                    for(int b=0;b<TargetNames.Length;b++)
                    {
                        var correction=start.rotations[b]*Quaternion.Inverse(end.rotations[b]);
                        poses[i].rotations[b]=Quaternion.Slerp(Quaternion.identity,correction,weight)*poses[i].rotations[b];
                    }
                }
            }
            var output=new AnimationClip {name=targetName,legacy=true,frameRate=Rate,
                wrapMode=loop?WrapMode.Loop:WrapMode.ClampForever};
            for(int b=0;b<TargetNames.Length;b++)
            {
                var values=new Quaternion[poses.Length];
                for(int i=0;i<values.Length;i++)
                {
                    var q=poses[i].rotations[b];
                    if(i>0&&Quaternion.Dot(values[i-1],q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                    values[i]=q;
                }
                AddCurve(output,target.paths[b],"m_LocalRotation.x",input.length,values.Select(q=>q.x).ToArray());
                AddCurve(output,target.paths[b],"m_LocalRotation.y",input.length,values.Select(q=>q.y).ToArray());
                AddCurve(output,target.paths[b],"m_LocalRotation.z",input.length,values.Select(q=>q.z).ToArray());
                AddCurve(output,target.paths[b],"m_LocalRotation.w",input.length,values.Select(q=>q.w).ToArray());
            }
            AddCurve(output,target.paths[0],"m_LocalPosition.x",input.length,poses.Select(p=>p.hip.x).ToArray());
            AddCurve(output,target.paths[0],"m_LocalPosition.y",input.length,poses.Select(p=>p.hip.y).ToArray());
            AddCurve(output,target.paths[0],"m_LocalPosition.z",input.length,poses.Select(p=>p.hip.z).ToArray());
            output.EnsureQuaternionContinuity();
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(asset);
            if(existing){EditorUtility.CopySerialized(output,existing);UnityEngine.Object.DestroyImmediate(output);output=existing;}
            else AssetDatabase.CreateAsset(output,asset);
            float stride=0;
            if(gait)
            {
                // During stance an in-place foot travels backwards at character
                // speed. Twice its ground-contact excursion is a full gait stride.
                SaveGaitDiagnostic(source,target,id,targetName,phase,poses);
                var speeds=new List<float>();
                for(int side=0;side<2;side++)
                    for(int i=1;i<poses.Length-1;i++)
                    {
                        Vector3 a=side==0?poses[i-1].leftFoot:poses[i-1].rightFoot;
                        Vector3 b=side==0?poses[i].leftFoot:poses[i].rightFoot;
                        if(a.y<target.floorJoint+target.height*.02f&&b.y<target.floorJoint+target.height*.02f)
                        {
                            float speed=(a.z-b.z)*intervals/target.height;
                            if(speed>.05f)speeds.Add(speed);
                        }
                    }
                Need(speeds.Count>6,"No grounded backward stance for "+id+"/"+targetName);
                speeds.Sort();
                // Cadence derives from ground speed, independent of stance duty.
                stride=speeds[speeds.Count/2];
                Need(stride>.25f&&stride<4f,"Implausible measured gait stride "+id+"/"+targetName+": "+stride);
            }
            LastBake.mappings.Add(new Mapping {
                hero=id,target=targetName,source=sourceName,asset=asset,
                sourceLength=input.length,sourcePhase=phase,sourceContact=contact,
                targetContact=sword?.42f:0,stridePerHeight=stride,
                frames=poses.Length,minFootHeight=poses.Min(p=>p.minFoot),
                maxHipOffset=poses.Max(p=>(p.hip-target.hip).magnitude)
            });
            return output;
        }
        static void AddCurve(AnimationClip clip,string path,string property,float length,float[] values)
        {
            var keys=new Keyframe[values.Length];
            float step=length/(values.Length-1);
            for(int i=0;i<keys.Length;i++)
            {
                float slope=i==0?(values[1]-values[0])/step:i==keys.Length-1?(values[i]-values[i-1])/step:(values[i+1]-values[i-1])/(step*2);
                keys[i]=new Keyframe(i*step,values[i],slope,slope);
            }
            clip.SetCurve(path,typeof(Transform),property,new AnimationCurve(keys));
        }
        public static void AssertApplied()
        {
            var library=AssetDatabase.LoadAssetAtPath<NocturnisActILibrary>("Assets/Resources/ActILibrary.asset");
            var original=AssetDatabase.LoadAssetAtPath<NocturnisActILibrary>(OriginalLibrary);
            var config=AssetDatabase.LoadAssetAtPath<KryptaAssets>("Assets/Resources/KryptaAssets.asset");
            var originalConfig=AssetDatabase.LoadAssetAtPath<KryptaAssets>(OriginalConfig);
            Need(library&&original&&config&&originalConfig,"UAL backup or active library missing.");
            for(int index=0;index<Heroes.Length;index++)
            {
                string id=Heroes[index];
                var current=library.Find(id);var previous=original.Find(id);
                Need(current!=null&&previous!=null,"Missing UAL hero "+id);
                Need(AssetDatabase.GetAssetPath(current.prefab)==Folder+"/"+id+"/"+id+".prefab","Hero model is not retargeted: "+id);
                Need(config.heroes[index].model==current.prefab,"Hero config/library model mismatch: "+id);
                Need(config.heroes[index].animations.SequenceEqual(current.clips),"Hero config/library clip mismatch: "+id);
                Need(config.heroes[index].weapon==originalConfig.heroes[index].weapon,"Equipped weapon changed: "+id);
                var mapped=new HashSet<string>(new[]{"Idle","Walking_A","Running_A","Hit_A","Death_A","Interact_A","Talk_A"});
                if(index==0)mapped.Add("1H_Melee_Attack_Chop");
                if(index==1||index==3||index==4)mapped.Add("Cast_A");
                foreach(string name in mapped)
                {
                    var clip=current.clips.SingleOrDefault(c=>c.name==name);
                    Need(clip&&clip.legacy&&AssetDatabase.GetAssetPath(clip)==Folder+"/"+id+"/"+name+".anim","Missing baked UAL clip "+id+"/"+name);
                }
                foreach(var old in previous.clips)
                    if(!mapped.Contains(old.name))Need(current.clips.Single(c=>c.name==old.name)==old,"Original fallback changed: "+id+"/"+old.name);
                var profile=current.prefab.GetComponent<ActorModelProfile>();
                Need(profile&&profile.walkStridePerHeight>=.30f&&profile.runStridePerHeight>=.55f,"UAL stride metadata missing: "+id);
            }
            Need(config.knight==config.heroes[0].model&&config.knightAnimations.SequenceEqual(config.heroes[0].animations),"Knight alias points to old animations.");
        }

        public static void Prepare()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var importer=AssetImporter.GetAtPath(Source) as ModelImporter;
            Need(importer,"User-supplied UAL FBX was not imported.");
            importer.importAnimation=true;
            importer.animationType=ModelImporterAnimationType.Legacy;
            importer.animationCompression=ModelImporterAnimationCompression.Off;
            importer.importCameras=false;importer.importLights=false;
            importer.optimizeGameObjects=false;
            importer.SaveAndReimport();
            if(!AssetDatabase.LoadAssetAtPath<NocturnisActILibrary>(OriginalLibrary))
                Need(AssetDatabase.CopyAsset("Assets/Resources/ActILibrary.asset",OriginalLibrary),"Cannot preserve original animation library.");
            if(!AssetDatabase.LoadAssetAtPath<KryptaAssets>(OriginalConfig))
                Need(AssetDatabase.CopyAsset("Assets/Resources/KryptaAssets.asset",OriginalConfig),"Cannot preserve original hero config.");
            var original=AssetDatabase.LoadAssetAtPath<NocturnisActILibrary>(OriginalLibrary);
            var originalConfig=AssetDatabase.LoadAssetAtPath<KryptaAssets>(OriginalConfig);
            var library=AssetDatabase.LoadAssetAtPath<NocturnisActILibrary>("Assets/Resources/ActILibrary.asset");
            var config=AssetDatabase.LoadAssetAtPath<KryptaAssets>("Assets/Resources/KryptaAssets.asset");
            Need(original&&library&&config&&originalConfig,"Missing campaign asset configuration.");
            LastBake=new BakeReport();
            using(var source=new SourceRig())
            for(int hero=0;hero<Heroes.Length;hero++)
            {
                string id=Heroes[hero],folder=Folder+"/"+id;
                Directory.CreateDirectory(folder);AssetDatabase.Refresh();
                var baseline=original.Find(id);Need(baseline!=null&&baseline.prefab,"Missing original hero "+id);
                var clips=baseline.clips.ToDictionary(c=>c.name,c=>c);
                var replacements=new Dictionary<string,string>{
                    {"Idle",hero==0?"Sword_Idle":"Idle_Loop"},
                    {"Walking_A","Walk_Loop"},{"Running_A","Jog_Fwd_Loop"},
                    {"Hit_A","Hit_Chest"},{"Death_A","Death01"},
                    {"Interact_A","Interact"},{"Talk_A","Idle_Talking_Loop"}
                };
                if(hero==0)replacements["1H_Melee_Attack_Chop"]="Sword_Attack";
                if(hero==1||hero==3||hero==4)replacements["Cast_A"]="Spell_Simple_Shoot";
                using(var target=new TargetRig(baseline.prefab,source))
                {
                    foreach(var pair in replacements)
                        clips[pair.Key]=Bake(source,target,id,pair.Value,pair.Key,folder+"/"+pair.Key+".anim");
                    foreach(var clip in baseline.clips)
                        if(!replacements.ContainsKey(clip.name))LastBake.retained.Add(id+"/"+clip.name);
                    // The original prefab is retained untouched. Only this copied
                    // prefab gets cadence metadata for the imported gait.
                    foreach(var bone in target.bones)
                    {
                        int index=Array.IndexOf(target.bones,bone);
                        bone.localPosition=target.localPositions[index];
                        bone.localRotation=target.localRotations[index];
                    }
                    var profile=target.model.GetComponent<ActorModelProfile>();
                    profile.walkStridePerHeight=LastBake.mappings.Last(m=>m.hero==id&&m.target=="Walking_A").stridePerHeight;
                    profile.runStridePerHeight=LastBake.mappings.Last(m=>m.hero==id&&m.target=="Running_A").stridePerHeight;
                    target.model.name=id;
                    var prefab=PrefabUtility.SaveAsPrefabAsset(target.model,folder+"/"+id+".prefab");
                    var entry=library.Find(id);entry.prefab=prefab;entry.clips=clips.Values.ToArray();
                    config.heroes[hero]=new KryptaAssets.Character{
                        model=prefab,animations=entry.clips,weapon=originalConfig.heroes[hero].weapon
                    };
                }
                LastBake.heroes++;
            }
            config.knight=config.heroes[0].model;config.knightAnimations=config.heroes[0].animations;
            EditorUtility.SetDirty(library);EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
            File.WriteAllText("Logs/ual-bake-manifest.json",JsonUtility.ToJson(LastBake,true));
        }
    }
}
