using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Krypta.Editor
{
    public static class NocturnisOriginalModelQA
    {
        const string Output="Logs/OriginalModel20261029";
        [Serializable] sealed class ClipResult { public string name,render; public int bindings; public float duration,maximumDisplacement; }
        [Serializable] sealed class SourceGeometry { public int vertices,triangles; }
        [Serializable] sealed class Report {
            public bool passed;public int assertions,vertices,triangles,bones;public float bodyHeight,minimumY;
            public string error="";public List<ClipResult> clips=new List<ClipResult>();
        }
        static Report report;
        static void Check(bool pass,string message){report.assertions++;if(!pass)throw new InvalidOperationException(message);}
        static bool Finite(float value){return !float.IsNaN(value) && !float.IsInfinity(value);}

        public static Vector3[] SkinVertices(GameObject root)
        {
            var points=new List<Vector3>();
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh=skin.sharedMesh;
                if(!mesh || !mesh.isReadable)throw new InvalidOperationException("Readable skinned source mesh is required.");
                var weights=mesh.boneWeights;var vertices=mesh.vertices;var poses=mesh.bindposes;var bones=skin.bones;
                if(weights.Length!=vertices.Length || poses.Length!=bones.Length || bones.Any(bone=>!bone))
                    throw new InvalidOperationException("Incomplete bone weights, bind poses or bones.");
                var matrices=bones.Select((bone,index)=>bone.localToWorldMatrix*poses[index]).ToArray();
                for(int i=0;i<vertices.Length;i++)
                {
                    var w=weights[i];var v=vertices[i];var point=Vector3.zero;
                    if(w.weight0>0)point+=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0;
                    if(w.weight1>0)point+=matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1;
                    if(w.weight2>0)point+=matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2;
                    if(w.weight3>0)point+=matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
                    if(!Finite(point.x)||!Finite(point.y)||!Finite(point.z) || Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)>.001f)
                        throw new InvalidOperationException("Invalid world skinning geometry or non-normalized weights.");
                    points.Add(point);
                }
            }
            if(points.Count==0)throw new InvalidOperationException("No skinned vertices.");
            return points.ToArray();
        }
        public static Bounds SkinBounds(GameObject root){return BoundsOf(SkinVertices(root));}
        static Bounds BoundsOf(Vector3[] points){var bounds=new Bounds(points[0],Vector3.zero);foreach(var point in points)bounds.Encapsulate(point);return bounds;}

        public static void Run()
        {
            Directory.CreateDirectory(Output);report=new Report();
            float previousTimeScale=Time.timeScale;
            try
            {
                NocturnisOriginalRuntimeBuild.ValidateReady();
                Check(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null,"Model review needs a real graphics device.");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var config=Resources.Load<KryptaAssets>("KryptaAssets");var source=config.heroes[0];
                var actor=new ActorVisual(source.model,source.animations,Vector3.zero,1.8f,"Reviewed TRELLIS Inquisitor");
                actor.Equip(source.weapon);
                try
                {
                    var animation=actor.Root.GetComponentInChildren<Animation>(true);
                    Check(animation,"ActorVisual has an actual animation component.");
                    var skins=actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    report.vertices=skins.Sum(skin=>skin.sharedMesh.vertexCount);report.triangles=skins.Sum(skin=>skin.sharedMesh.triangles.Length/3);
                    report.bones=skins.SelectMany(skin=>skin.bones).Distinct().Count();
                    var exported=JsonUtility.FromJson<SourceGeometry>(File.ReadAllText(NocturnisOriginalRuntimeBuild.HeroFolder+"/manifest.json"));
                    Check(exported.vertices>0 && report.vertices>=exported.vertices && report.triangles==exported.triangles && report.bones>=20,"The prepared model preserves the exported geometry and has a usable rig.");
                    foreach(var skin in skins)
                    {
                        Check(skin.sharedMesh.uv.Length==skin.sharedMesh.vertexCount,"Every character vertex has texture coordinates.");
                        Check(skin.sharedMesh.normals.Length==skin.sharedMesh.vertexCount,"Every character vertex has a normal.");
                        foreach(var material in skin.sharedMaterials)
                            Check(material && material.shader && material.shader.isSupported && material.shader.name=="Standard" && material.mainTexture && material.mainTexture.width>=1024,"Explicit supported character atlas material.");
                    }
                    var slot=actor.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="handslot.r");
                    Check(slot && slot.GetComponentsInChildren<Renderer>().Length>0,"The equipped weapon is visible in the actual game hand slot.");
                    var left=actor.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="handslot.l");
                    Check(left && left.Find("Badge_Shield") && left.Find("Badge_Shield").GetComponentsInChildren<Renderer>().Length>0,"The equipped shield is visible in the actual game hand slot.");
                    animation.Stop();animation["Idle"].clip.SampleAnimation(animation.gameObject,.15f);
                    var bounds=SkinBounds(actor.Root);report.bodyHeight=bounds.size.y;report.minimumY=bounds.min.y;
                    Check(Mathf.Abs(bounds.size.y-1.8f)<.025f && Mathf.Abs(bounds.min.y)<.025f,"Normalized body height and floor contact are correct.");
                    var camera=CreateCamera();
                    foreach(var clip in source.animations)
                    {
                        var item=new ClipResult{name=clip.name,duration=clip.length};report.clips.Add(item);
                        var bindings=AnimationUtility.GetCurveBindings(clip);item.bindings=bindings.Length;
                        Check(clip.legacy && clip.length>.1f && bindings.Length>0,"Playable clip: "+clip.name);
                        foreach(var binding in bindings)
                            Check(string.IsNullOrEmpty(binding.path) || animation.transform.Find(binding.path),"Animation binding resolves on actual actor: "+clip.name+"/"+binding.path);
                        Vector3[] first=null;
                        foreach(float fraction in new[]{0f,.25f,.5f,.75f})
                        {
                            clip.SampleAnimation(animation.gameObject,clip.length*fraction);
                            var points=SkinVertices(actor.Root);var posed=BoundsOf(points);
                            Check(posed.size.magnitude<4.5f && posed.center.magnitude<3,"Animation has finite bounded deformation: "+clip.name+"/"+fraction);
                            if(first==null)first=points;
                            else for(int vertex=0;vertex<points.Length;vertex++)item.maximumDisplacement=Mathf.Max(item.maximumDisplacement,Vector3.Distance(first[vertex],points[vertex]));
                        }
                        Check(item.maximumDisplacement>.00003f,"Skin actually moves under animation: "+clip.name);
                        clip.SampleAnimation(animation.gameObject,clip.length*.35f);
                        item.render="pose-"+clip.name+".png";Capture(camera,item.render);
                    }
                    animation["Idle"].clip.SampleAnimation(animation.gameObject,.15f);
                    camera.transform.position=Vector3.up*.9f+new Vector3(9,15,-11).normalized*6;
                    camera.transform.LookAt(Vector3.up*.9f);camera.orthographicSize=1.4f;
                    Capture(camera,"view-back-game-direction.png");
                    var kit=Resources.Load<NocturnisOriginalFloorKit>("OriginalCryptFloor");
                    var patch=new GameObject("Original floor under actual game camera");
                    for(int x=-3;x<=3;x++)for(int z=-3;z<=3;z++)
                    {
                        var tile=UnityEngine.Object.Instantiate(kit.Module(((x+3)*7+z+3)%6),patch.transform);
                        tile.transform.localPosition=new Vector3(x*1.8f,0,z*1.8f);
                        foreach(var renderer in tile.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=kit.darkFantasy;
                    }
                    camera.transform.position=new Vector3(9,15,-11);camera.transform.LookAt(Vector3.zero);camera.orthographicSize=9;
                    Capture(camera,"view-back-floor-camera9.png",1280,720);
                    UnityEngine.Object.DestroyImmediate(patch);
                    camera.transform.position=new Vector3(3.4f,2.4f,4.5f);camera.transform.LookAt(Vector3.up*.9f);camera.orthographicSize=1.35f;
                    foreach(var pair in new[]{new KeyValuePair<string,float>("1H_Melee_Attack_Slice_Horizontal",.3f),new KeyValuePair<string,float>("2H_Melee_Attack_Spin",.5f)})
                    {
                        actor.Play(pair.Key,false,pair.Value);var state=animation[pair.Key];
                        Check(Mathf.Abs(state.speed-state.length/pair.Value)<.001f,"New hero fits full action to gameplay duration: "+pair.Key);
                        state.time=state.length*.6f;actor.Play(pair.Key,false,pair.Value);
                        Check(Mathf.Abs(actor.CurrentClipTime)<.0001f,"Repeated non-loop action restarts at frame zero.");
                    }
                    Time.timeScale=0;actor.Play("Death_A",false,9999);actor.AdvancePausedAnimation(1);
                    Check(actor.CurrentClip=="Death_A" && actor.CurrentClipTime>.1f,"Death progresses while the game clock is paused.");
                    animation.Sample();
                    Check(Mathf.Abs(SkinBounds(actor.Root).min.y)<.025f,"The final death pose rests on the floor rather than floating or sinking.");
                    Capture(camera,"pose-death-paused.png");
                    var other=config.heroes[1];var oldActor=new ActorVisual(other.model,other.animations,Vector3.zero,1.8f,"Existing KayKit timing regression");
                    try {
                        oldActor.Play("1H_Melee_Attack_Slice_Horizontal",false,.3f);
                        Check(Mathf.Abs(oldActor.Root.GetComponentInChildren<Animation>(true)["1H_Melee_Attack_Slice_Horizontal"].speed-1)<.001f,"Existing KayKit action speed is unchanged.");
                    }
                    finally{UnityEngine.Object.DestroyImmediate(oldActor.Root);}
                }
                finally{UnityEngine.Object.DestroyImmediate(actor.Root);}
                report.passed=true;
            }
            catch(Exception error){report.error=error.ToString();Debug.LogException(error);}
            finally{Time.timeScale=previousTimeScale;}
            File.WriteAllText(Output+"/result.json",JsonUtility.ToJson(report,true));
            Debug.Log("Original model QA "+(report.passed ? "PASS" : "FAIL")+": "+report.assertions+" assertions.");
            EditorApplication.Exit(report.passed ? 0 : 1);
        }

        static Camera CreateCamera()
        {
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.38f,.40f,.43f);RenderSettings.fog=false;
            var light=new GameObject("Current game sun").AddComponent<Light>();light.type=LightType.Directional;
            light.color=new Color(.67f,.74f,.91f);light.intensity=.85f;light.transform.rotation=Quaternion.Euler(48,-32,0);light.shadows=LightShadows.Soft;
            var camera=new GameObject("Actual imported character review").AddComponent<Camera>();
            camera.transform.position=new Vector3(3.4f,2.4f,4.5f);camera.transform.LookAt(Vector3.up*.9f);
            camera.orthographic=true;camera.orthographicSize=1.35f;camera.nearClipPlane=.05f;camera.farClipPlane=100;
            camera.backgroundColor=new Color(.10f,.11f,.13f);camera.clearFlags=CameraClearFlags.SolidColor;return camera;
        }
        static void Capture(Camera camera,string filename,int width=800,int height=1000)
        {
            var target=new RenderTexture(width,height,24);target.Create();var previous=RenderTexture.active;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            try {camera.aspect=(float)width/height;camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(Output+"/"+filename,image.EncodeToPNG());}
            finally{RenderTexture.active=previous;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
