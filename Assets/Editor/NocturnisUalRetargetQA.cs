using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace Krypta.Editor
{
    public static class NocturnisUalRetargetQA
    {
        [Serializable] public sealed class ClipCheck
        {
            public string hero, clip;
            public int curves, samples;
            public float length, loopRotationError, loopHipError, minimumFootClearance;
            public float maximumHipSway, maximumRotationStep, motionDegrees;
        }
        [Serializable] public sealed class Report
        {
            public bool passed;
            public int heroes, replaced, preserved;
            public float swordPeakPhase,swordContactSpeedRatio,swordGripError;
            public bool rootMotionDisabled, originalAssetsRetained, nonHumanoidsUnchanged, gameplayContactUnchanged;
            public List<NocturnisUalImport.Mapping> mappings;
            public List<ClipCheck> clips=new List<ClipCheck>();
            public List<string> limitations=new List<string>{
                "Existing simplified skin weights remain; this is not a new anatomy/skin rig.",
                "Roll retained: source full roll does not fit the current 0.25 second dodge.",
                "Bow, axe, scythe, spin, block and creature attacks retain existing animations.",
                "Interact and talk use optional additional clips; only supported runtime hooks invoke them."
            };
        }
        static bool Finite(float v){return !float.IsNaN(v)&&!float.IsInfinity(v);}
        static bool Finite(Vector3 v){return Finite(v.x)&&Finite(v.y)&&Finite(v.z);}
        static bool Finite(Quaternion q){return Finite(q.x)&&Finite(q.y)&&Finite(q.z)&&Finite(q.w);}
        static void Need(bool value,string reason){NocturnisUalImport.Need(value,reason);}
        static Transform Bone(GameObject model,string name)
        {
            return model.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        }
        public static void Validate(NocturnisUalImport.BakeReport bake)
        {
            NocturnisUalImport.AssertApplied();
            var report=new Report{heroes=bake.heroes,replaced=bake.mappings.Count,
                preserved=bake.retained.Count,mappings=bake.mappings,
                rootMotionDisabled=true,originalAssetsRetained=true,nonHumanoidsUnchanged=true,gameplayContactUnchanged=true};
            var library=AssetDatabase.LoadAssetAtPath<NocturnisActILibrary>("Assets/Resources/ActILibrary.asset");
            var original=AssetDatabase.LoadAssetAtPath<NocturnisActILibrary>(NocturnisUalImport.OriginalLibrary);
            foreach(var mapping in bake.mappings)
            {
                var entry=library.Find(mapping.hero);
                var model=UnityEngine.Object.Instantiate(entry.prefab);
                try
                {
                    model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                    var clip=entry.clips.Single(c=>c.name==mapping.target);
                    var profile=model.GetComponent<ActorModelProfile>();
                    var hips=Bone(model,"hips");
                    var left=Bone(model,"footL");var right=Bone(model,"footR");
                    Vector3 restHip=hips.localPosition;
                    float floorJoint=Mathf.Min(left.position.y,right.position.y);
                    var bindings=AnimationUtility.GetCurveBindings(clip);
                    Need(clip.legacy,"Runtime clip must be Legacy: "+mapping.asset);
                    Need(bindings.Length==63,"Expected 15 quaternion rotations + hip translations: "+mapping.asset+"/"+bindings.Length);
                    foreach(var binding in bindings)
                    {
                        Need(binding.type==typeof(Transform),"Unexpected binding type: "+binding.propertyName);
                        Need(!string.IsNullOrEmpty(binding.path)&&model.transform.Find(binding.path),"Missing binding path "+binding.path);
                        Need(!binding.propertyName.Contains("Scale"),"Imported scale must never animate target skeleton.");
                        Need(!binding.propertyName.Contains("Position")||binding.path=="hips","Only hip pose may translate.");
                        foreach(var key in AnimationUtility.GetEditorCurve(clip,binding).keys)
                            Need(Finite(key.time)&&Finite(key.value)&&Finite(key.inTangent)&&Finite(key.outTangent),"Nonfinite key "+mapping.asset);
                    }
                    var bones=new[]{"hips","chest","head","armR","forearmR","handR","armL","forearmL","handL","thighR","shinR","footR","thighL","shinL","footL"}
                        .Select(n=>Bone(model,n)).ToArray();
                    Vector3[] positions=bones.Select(b=>b.localPosition).ToArray();
                    int samples=Mathf.Max(120,Mathf.CeilToInt(clip.length*120));
                    Quaternion[] first=null,last=null,previous=null;
                    Vector3 firstHip=Vector3.zero;
                    float minimum=float.PositiveInfinity,maxSway=0,maxStep=0,motion=0;
                    for(int i=0;i<=samples;i++)
                    {
                        clip.SampleAnimation(model,clip.length*i/samples);
                        Need(model.transform.position==Vector3.zero&&model.transform.rotation==Quaternion.identity,"Retarget moved actor root.");
                        var current=bones.Select(b=>b.localRotation).ToArray();
                        for(int j=0;j<bones.Length;j++)
                        {
                            Need(Finite(current[j])&&Finite(bones[j].position),"Nonfinite bone pose.");
                            if(j>0)Need(Vector3.Distance(positions[j],bones[j].localPosition)<.00001f,"Bone length changed: "+bones[j].name);
                            if(previous!=null)maxStep=Mathf.Max(maxStep,Quaternion.Angle(previous[j],current[j]));
                            if(first!=null)motion=Mathf.Max(motion,Quaternion.Angle(first[j],current[j]));
                        }
                        if(first==null){first=current;firstHip=hips.localPosition;}
                        previous=current;last=current;
                        float clearance=(Mathf.Min(left.position.y,right.position.y)-floorJoint)/profile.bodyHeight;
                        minimum=Mathf.Min(minimum,clearance);
                        var sway=hips.localPosition-restHip;sway.y=0;
                        maxSway=Mathf.Max(maxSway,sway.magnitude/profile.bodyHeight);
                        Need(maxSway<.045f,"Imported pelvis travel exceeds body sway allowance.");
                    }
                    float seam=first.Zip(last,(a,b)=>Quaternion.Angle(a,b)).Max();
                    float hipSeam=Vector3.Distance(firstHip,hips.localPosition)/profile.bodyHeight;
                    bool loop=clip.wrapMode==WrapMode.Loop;
                    if(loop){Need(seam<.15f,"Rotation loop seam "+mapping.asset+" "+seam);Need(hipSeam<.0001f,"Hip loop seam "+mapping.asset);}
                    if(mapping.target!="Death_A")Need(minimum>-.065f,"Foot sinks into floor "+mapping.asset+" "+minimum);
                    Need(motion>.1f,"Static replacement clip "+mapping.asset);
                    Need(maxStep<80,"Abrupt retarget rotation "+mapping.asset+" "+maxStep);
                    if(mapping.target=="1H_Melee_Attack_Chop"){Need(Mathf.Abs(mapping.targetContact-.42f)<.00001f,"Gameplay contact changed.");ValidateSwordContact(model,clip,report);}
                    report.clips.Add(new ClipCheck{
                        hero=mapping.hero,clip=mapping.target,curves=bindings.Length,samples=samples+1,length=clip.length,
                        loopRotationError=seam,loopHipError=hipSeam,minimumFootClearance=minimum,maximumHipSway=maxSway,
                        maximumRotationStep=maxStep,motionDegrees=motion
                    });
                }
                finally {UnityEngine.Object.DestroyImmediate(model);}
            }
            foreach(var previous in original.entries.Where(e=>!NocturnisUalImport.Heroes.Contains(e.id)))
            {
                var actual=library.Find(previous.id);
                Need(actual.prefab==previous.prefab,"Nonhero model changed: "+previous.id);
                Need((actual.clips??new AnimationClip[0]).SequenceEqual(previous.clips??new AnimationClip[0]),"Nonhero clips changed: "+previous.id);
            }
            report.passed=true;
            File.WriteAllText("Logs/ual-retarget-qa.json",JsonUtility.ToJson(report,true));
        }

        static void ValidateSwordContact(GameObject model,AnimationClip clip,Report report)
        {
            var config=AssetDatabase.LoadAssetAtPath<KryptaAssets>("Assets/Resources/KryptaAssets.asset");
            var slot=Bone(model,"handslot.r");
            var weapon=UnityEngine.Object.Instantiate(config.heroes[0].weapon,slot,false);
            weapon.transform.localPosition=Vector3.zero;weapon.transform.localRotation=Quaternion.identity;
            try
            {
                var tip=Bone(weapon,"TipAnchor");var grip=Bone(weapon,"GripAnchor");
                const int rate=300;
                Vector3 previous=Vector3.zero;
                float maximum=-1,contactScore=0,peak=0,maxGap=0;
                for(int i=0;i<=rate;i++)
                {
                    float phase=i/(float)rate;
                    clip.SampleAnimation(model,phase*clip.length);
                    maxGap=Mathf.Max(maxGap,Vector3.Distance(slot.position,grip.position));
                    Need(Finite(tip.position)&&Finite(tip.rotation),"Nonfinite equipped sword transform.");
                    if(i>0)
                    {
                        Vector3 velocity=(tip.position-previous)*rate/clip.length;
                        float score=Mathf.Max(0,velocity.z)+Mathf.Max(0,-velocity.y)*.75f;
                        if(phase>.1f&&phase<.85f&&score>maximum){maximum=score;peak=phase;}
                        if(Mathf.Abs(phase-.42f)<.025f)contactScore=Mathf.Max(contactScore,score);
                    }
                    previous=tip.position;
                }
                report.swordPeakPhase=peak;report.swordContactSpeedRatio=contactScore/Mathf.Max(.001f,maximum);report.swordGripError=maxGap;
                Need(maxGap<.001f,"Sword grip detaches while striking.");
                Need(Mathf.Abs(peak-.42f)<.16f,"Equipped sword strike peak is out of gameplay contact phase: "+peak);
                Need(report.swordContactSpeedRatio>.55f,"Weapon tip has already slowed before damage contact.");
            }
            finally {UnityEngine.Object.DestroyImmediate(weapon);}
        }
        public static void Capture()
        {
            Directory.CreateDirectory("Logs");
            try
            {
                NocturnisUalImport.AssertApplied();
                string folder=Environment.GetEnvironmentVariable("NOCTURNIS_UAL_CAPTURE_DIR");
                if(string.IsNullOrWhiteSpace(folder))folder="Logs/ual-preview";
                Directory.CreateDirectory(folder);
                CaptureModels(folder);
                File.WriteAllText(Path.Combine(folder,"capture.json"),"{\"passed\":true,\"method\":\"Unity Camera.Render only\",\"left\":\"0.7.3 original\",\"right\":\"UAL retarget\"}");
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText("Logs/ual-capture-error.txt",e.ToString());EditorApplication.Exit(1);}
        }
        static void CaptureModels(string folder)
        {
            // Isolated scene: no game saves, UI, desktop or other applications.
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.22f,.25f,.29f);
            RenderSettings.fog=false;
            var cameraObject=new GameObject("UAL_Review_Camera");
            var camera=cameraObject.AddComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.045f,.055f);
            camera.orthographic=true;camera.orthographicSize=1.55f;
            camera.transform.position=new Vector3(2.3f,1.65f,6.2f);
            camera.transform.LookAt(new Vector3(0,.95f,0));
            camera.nearClipPlane=.01f;camera.farClipPlane=40;
            var key=new GameObject("Key").AddComponent<Light>();
            key.type=LightType.Directional;key.intensity=1.35f;key.color=new Color(1,.86f,.70f);key.transform.rotation=Quaternion.Euler(40,-30,0);
            var fill=new GameObject("Fill").AddComponent<Light>();
            fill.type=LightType.Directional;fill.intensity=.55f;fill.color=new Color(.55f,.70f,1);fill.transform.rotation=Quaternion.Euler(25,145,0);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Preview floor";ground.transform.localScale=Vector3.one*2;
            var material=new Material(Shader.Find("Standard"));material.color=new Color(.14f,.16f,.18f);
            ground.GetComponent<Renderer>().sharedMaterial=material;
            var before=AssetDatabase.LoadAssetAtPath<KryptaAssets>(NocturnisUalImport.OriginalConfig);
            var after=AssetDatabase.LoadAssetAtPath<KryptaAssets>("Assets/Resources/KryptaAssets.asset");
            var clipNames=new[]{"Idle","Walking_A","Running_A","1H_Melee_Attack_Chop","Hit_A","Cast_A","Death_A"};
            for(int hero=0;hero<NocturnisUalImport.Heroes.Length;hero++)
            {
                var left=PreviewModel(before.heroes[hero],new Vector3(-1,0,0));
                var right=PreviewModel(after.heroes[hero],new Vector3(1,0,0));
                try
                {
                    string[] chosen=hero==0?clipNames:new[]{"Idle","Walking_A","Running_A","Cast_A"};
                    foreach(string name in chosen)
                    {
                        var oldClip=before.heroes[hero].animations.Single(c=>c.name==name);
                        var newClip=after.heroes[hero].animations.Single(c=>c.name==name);
                        for(int frame=0;frame<4;frame++)
                        {
                            float t=name=="Death_A"?(frame+1)/4f:.12f+frame*.22f;
                            oldClip.SampleAnimation(left,t*oldClip.length);
                            newClip.SampleAnimation(right,t*newClip.length);
                            SaveFrame(camera,Path.Combine(folder,"hero-"+hero+"-"+name+"-"+frame+".png"),1280,720);
                        }
                    }
                }
                finally {UnityEngine.Object.DestroyImmediate(left);UnityEngine.Object.DestroyImmediate(right);}
            }
            // A matched continuous cycle for the review reel, not fake gameplay.
            var oldModel=PreviewModel(before.heroes[0],new Vector3(-1,0,0));
            var newModel=PreviewModel(after.heroes[0],new Vector3(1,0,0));
            try
            {
                var reelFolder=Path.Combine(folder,"reel");Directory.CreateDirectory(reelFolder);
                var reelClips=new[]{"Idle","Walking_A","Running_A","1H_Melee_Attack_Chop","Cast_A"};
                int index=0;
                foreach(var name in reelClips)
                {
                    var oldClip=before.heroes[0].animations.Single(c=>c.name==name);
                    var newClip=after.heroes[0].animations.Single(c=>c.name==name);
                    for(int frame=0;frame<48;frame++)
                    {
                        float phase=frame/48f;
                        oldClip.SampleAnimation(oldModel,phase*oldClip.length);
                        newClip.SampleAnimation(newModel,phase*newClip.length);
                        SaveFrame(camera,Path.Combine(reelFolder,index.ToString("D4")+".png"),960,540);index++;
                    }
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(oldModel);UnityEngine.Object.DestroyImmediate(newModel);}
            UnityEngine.Object.DestroyImmediate(material);
        }
        static GameObject PreviewModel(KryptaAssets.Character character,Vector3 position)
        {
            var model=UnityEngine.Object.Instantiate(character.model);
            ModelSurface.Apply(model);
            var profile=model.GetComponent<ActorModelProfile>();
            float scale=1.9f/profile.bodyHeight;
            model.transform.localScale*=scale;
            model.transform.position=position+Vector3.up*(profile.groundOffset*scale);
            if(character.weapon)
            {
                var slot=Bone(model,"handslot.r");
                var weapon=UnityEngine.Object.Instantiate(character.weapon,slot,false);
                weapon.transform.localPosition=Vector3.zero;weapon.transform.localRotation=Quaternion.identity;
                ModelSurface.Apply(weapon);
            }
            foreach(var a in model.GetComponentsInChildren<Animator>(true))a.enabled=false;
            foreach(var a in model.GetComponentsInChildren<Animation>(true))a.enabled=false;
            return model;
        }
        sealed class RenderSkin
        {
            public SkinnedMeshRenderer source;public GameObject snapshot;public Mesh mesh;
        }
        static List<RenderSkin> FreezeSkinForFrame()
        {
            var result=new List<RenderSkin>();
            foreach(var skin in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
            {
                if(!skin.enabled||!skin.gameObject.activeInHierarchy||!skin.sharedMesh)continue;
                var mesh=new Mesh();skin.BakeMesh(mesh);
                var snapshot=new GameObject("SampledSkin_"+skin.name);
                snapshot.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);
                snapshot.transform.localScale=skin.transform.lossyScale;
                snapshot.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=snapshot.AddComponent<MeshRenderer>();
                renderer.sharedMaterials=skin.sharedMaterials;
                renderer.shadowCastingMode=skin.shadowCastingMode;renderer.receiveShadows=skin.receiveShadows;
                skin.enabled=false;
                result.Add(new RenderSkin{source=skin,snapshot=snapshot,mesh=mesh});
            }
            return result;
        }
        static void SaveFrame(Camera camera,string path,int width,int height)
        {
            var skins=FreezeSkinForFrame();
            var old=RenderTexture.active;
            var texture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
            var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
                pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=null;RenderTexture.active=old;texture.Release();
                foreach(var skin in skins)
                {
                    skin.source.enabled=true;
                    UnityEngine.Object.DestroyImmediate(skin.snapshot);
                    UnityEngine.Object.DestroyImmediate(skin.mesh);
                }
                UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
