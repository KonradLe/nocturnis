using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace Krypta
{
 public sealed class NocturnisMenu076RuntimeQA:MonoBehaviour
 {
  [Serializable]class Frame {public int hero,width,height;public string image;public float margin;public int visiblePixels;public bool transparentBorder;}
  [Serializable]class Report {public bool passed;public string error="";public int poses;public List<Frame> frames=new List<Frame>();}
  const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static bool Requested=>Array.IndexOf(Environment.GetCommandLineArgs(),"-menu076-review")>=0;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Isolate(){if(Requested)KryptaGame.Testing=true;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Launch(){if(Requested)new GameObject("Menu framing QA").AddComponent<NocturnisMenu076RuntimeQA>();}
  Report result=new Report();string folder;float started;bool done;
  void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)result.error+=text+"\n"+stack+"\n";}
  void Check(bool value,string why){if(!value)throw new Exception(why);}
  IEnumerator Start()
  {
   var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-menu076-output");
   folder=args[arg+1];Directory.CreateDirectory(folder);started=Time.realtimeSinceStartup;Application.logMessageReceived+=Log;
   KryptaGame game;while(!(game=FindFirstObjectByType<KryptaGame>())||!game.View)yield return null;
   game.enabled=false;Time.timeScale=0;
   var hud=game.GetComponent<KryptaHUD>();
   foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(960,540)})
   {
    Screen.SetResolution(size.x,size.y,false);
    for(int i=0;i<8;i++)yield return new WaitForEndOfFrame();
    Check(Screen.width==size.x&&Screen.height==size.y,"Requested window size was not applied");
    for(int hero=0;hero<5;hero++)
    {
     var actor=hud.ReviewMotionHero(hero);
     yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
     var camera=(Camera)typeof(KryptaHUD).GetField("portraitCamera",Private).GetValue(hud);
     var texture=(RenderTexture)typeof(KryptaHUD).GetField("portrait",Private).GetValue(hud);
     Check(Mathf.Abs(camera.aspect-502f/432)<.001f,"Portrait must use full center-panel aspect");
     Check(actor.Root.GetComponentInChildren<NocturnisWeaponGrip>()!=null,"Missing equipped weapon");
     float margin=1;Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
     if(size.x==1280)
     {
      var animation=actor.Root.GetComponentInChildren<Animation>();
      float duration=animation["Idle"].length;
      // Offset samples from the framing envelope's samples, so this checks
      // interpolated poses as well as the measured keyframes.
      for(int pose=0;pose<48;pose++)
      {
       actor.PreviewIdle(duration*(pose+.37f)/48f);
       margin=Mathf.Min(margin,GeometryMargin(actor,camera));result.poses++;
       Check(margin>.025f,"Clipped body or weapon: class "+hero+" margin "+margin);
      }
     }
     actor.PreviewIdle(Time.unscaledTime);
     yield return new WaitForSecondsRealtime(.25f);
     Check(Vector3.Distance(position,camera.transform.position)<.0001f&&Quaternion.Angle(rotation,camera.transform.rotation)<.001f,"Camera must not pump during idle");
     yield return new WaitForEndOfFrame();camera.Render();
     RenderTexture previous=RenderTexture.active;RenderTexture.active=texture;
     var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
     pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);pixels.Apply();RenderTexture.active=previous;
     var colors=pixels.GetPixels32();int visible=0;bool border=true;
     for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)
     {
      if(colors[y*texture.width+x].a<32)continue;visible++;
      if(x<8||y<8||x>=texture.width-8||y>=texture.height-8)border=false;
     }
     Destroy(pixels);
     Check(visible>10000&&border,"Empty or edge-clipped rendered portrait for class "+hero);
     string name="menu-"+size.x+"x"+size.y+"-class-"+hero+".png";
     var captured=ScreenCapture.CaptureScreenshotAsTexture();
     File.WriteAllBytes(Path.Combine(folder,name),captured.EncodeToPNG());Destroy(captured);
     result.frames.Add(new Frame{hero=hero,width=size.x,height=size.y,image=name,margin=margin,visiblePixels=visible,transparentBorder=border});
     yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
    }
   }
   foreach(var frame in result.frames)Check(File.Exists(Path.Combine(folder,frame.image)),"Missing screenshot "+frame.image);
   result.passed=result.error.Length==0&&result.frames.Count==15&&result.poses==240;Finish();
  }
  float GeometryMargin(ActorVisual actor,Camera camera)
  {
   var mesh=new Mesh();float margin=1;int count=0;
   foreach(var renderer in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
   {
    if(!renderer.enabled||!renderer.sharedMesh)continue;renderer.BakeMesh(mesh);
    foreach(var v in mesh.vertices){margin=Mathf.Min(margin,Point(camera,renderer.transform.TransformPoint(v)));count++;}
   }
   foreach(var filter in actor.Root.GetComponentsInChildren<MeshFilter>())
   {
    var renderer=filter.GetComponent<MeshRenderer>();if(!renderer||!renderer.enabled||!filter.sharedMesh)continue;
    foreach(var v in filter.sharedMesh.vertices){margin=Mathf.Min(margin,Point(camera,filter.transform.TransformPoint(v)));count++;}
   }
   Destroy(mesh);Check(count>1000,"No mesh geometry in portrait");return margin;
  }
  float Point(Camera camera,Vector3 point)
  {
   Vector3 v=camera.WorldToViewportPoint(point);
   Check(v.z>camera.nearClipPlane&&v.z<camera.farClipPlane,"Mesh outside depth clipping planes");
   return Mathf.Min(v.x,1-v.x,v.y,1-v.y);
  }
  void Update(){if(done||folder==null)return;if(result.error.Length>0||Time.realtimeSinceStartup-started>180){if(result.error.Length==0)result.error="QA timed out";result.passed=false;StopAllCoroutines();Finish();}}
  void Finish(){if(done)return;done=true;Application.logMessageReceived-=Log;File.WriteAllText(Path.Combine(folder,"results.json"),JsonUtility.ToJson(result,true));Debug.Log("MENU076_QA_COMPLETE "+result.passed);Application.Quit(result.passed?0:1);}
 }
}
