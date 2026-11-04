using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Krypta.Editor {
 public static class NocturnisIntegratedVisualQA {
  [Serializable] sealed class Result { public string id;public int vertices,bones,clips;public float maximumMotion,maximumHeight; }
  [Serializable] sealed class Report { public bool passed;public string error;public List<Result> actors=new List<Result>(); }
  static readonly string Output="Logs/VisualReview";
  public static void PrepareAndRun(){NocturnisIntegratedBuild.Prepare();Run();}
  public static void Run(){
   Directory.CreateDirectory(Output);var report=new Report();
   try {
    NocturnisIntegratedBuild.Validate();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    var lib=Resources.Load<NocturnisActILibrary>("ActILibrary");
    foreach(var a in lib.entries.Where(e=>e.clips!=null&&e.clips.Length>0)){
     var actor=new ActorVisual(a.prefab,a.clips,Vector3.zero,a.prefab.GetComponent<ActorModelProfile>().bodyHeight,a.id);var skin=actor.Root.GetComponentInChildren<SkinnedMeshRenderer>();var anim=actor.Root.GetComponentInChildren<Animation>();
     var result=new Result{id=a.id,vertices=skin.sharedMesh.vertexCount,bones=skin.bones.Length,clips=a.clips.Length};report.actors.Add(result);
     var baked=new Mesh();Vector3[] rest=null;
     foreach(var clip in a.clips){
      Vector3[] first=null;float motion=0;
      foreach(float fraction in new[]{0f,.25f,.5f,.75f,1f}){
       clip.SampleAnimation(anim.gameObject,clip.length*fraction);skin.BakeMesh(baked);var points=baked.vertices;
       if(first==null)first=points;else for(int i=0;i<points.Length;i++)motion=Mathf.Max(motion,(points[i]-first[i]).magnitude);
       if(points.Any(v=>float.IsNaN(v.x)||float.IsInfinity(v.y))||baked.bounds.size.magnitude>9)throw new Exception("Invalid skin deformation "+a.id+"/"+clip.name);
       result.maximumHeight=Mathf.Max(result.maximumHeight,baked.bounds.size.y);
       if(clip.name=="Idle"&&fraction==0){rest=points;var source=skin.sharedMesh.vertices;float restError=0;for(int i=0;i<points.Length;i++)restError=Mathf.Max(restError,(points[i]-source[i]).magnitude);if(restError>.10f)throw new Exception("Rest silhouette changed "+a.id+": "+restError);}
      }
      if(motion<.0001f)throw new Exception("Static animation "+a.id+"/"+clip.name);result.maximumMotion=Mathf.Max(result.maximumMotion,motion);
     }
     if(skin.sharedMesh.boneWeights.Any(w=>Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)>.001f))throw new Exception("Invalid skin weights "+a.id);
     UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(actor.Root);
    }
    RenderHeroes(lib,"Idle",.35f,"heroes-idle");RenderHeroes(lib,"Running_A",.18f,"heroes-running");RenderHeroes(lib,"1H_Melee_Attack_Chop",.3f,"heroes-attack");
    report.passed=true;
   }catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
   File.WriteAllText(Output+"/rig-report.json",JsonUtility.ToJson(report,true));EditorApplication.Exit(report.passed?0:1);
  }
  static void RenderHeroes(NocturnisActILibrary lib,string clip,float time,string file){
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.ambientLight=new Color(.35f,.37f,.42f);RenderSettings.fog=false;
   var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.25f;light.transform.rotation=Quaternion.Euler(40,-35,0);
   var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.7f;fill.color=new Color(.7f,.8f,1);fill.transform.rotation=Quaternion.Euler(25,145,0);
   string[] ids={"hero_inquisitor","hero_blood_weaver","hero_dusk_hunter","hero_void_herald","hero_root_guardian"};
   var config=Resources.Load<KryptaAssets>("KryptaAssets");
   for(int i=0;i<5;i++){
    var a=lib.Character(ids[i]);var actor=new ActorVisual(a.model,a.animations,new Vector3((i-2)*1.6f,0,0),1.8f,ids[i]);actor.Equip(config.heroes[i].weapon);
    var animation=actor.Root.GetComponentInChildren<Animation>();animation[clip].clip.SampleAnimation(animation.gameObject,time);
   }
   var camera=new GameObject("Camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=2.1f;camera.backgroundColor=new Color(.025f,.03f,.04f);camera.clearFlags=CameraClearFlags.SolidColor;
   camera.transform.position=new Vector3(0,2.1f,10);camera.transform.LookAt(new Vector3(0,.9f,0));
   var rt=new RenderTexture(1920,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
   var image=new Texture2D(1920,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,900),0,0);image.Apply();File.WriteAllBytes(Output+"/"+file+".png",image.EncodeToPNG());
   camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
  }
 }
}
