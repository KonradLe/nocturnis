using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Krypta.Editor {
 public static class NocturnisEnvironmentPolishQA {
  const string Session="Nocturnis.EnvironmentQA",Output="Logs/environment-polish-qa.json";
  const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
  [Serializable]public sealed class ZoneResult {public int zone,vertices,triangles,frustumSamples,backdropProps,releaseFrames;public float releaseWaitSeconds;public string name;}
  [Serializable]public sealed class Report {public bool passed;public int assertions,runtimeZones;public string failure;public List<ZoneResult> zones=new List<ZoneResult>();}
  static Report report;static int nextZone,stage,transitionFrame;static double started,nextTick,transitionAt;static bool errors;
  static Mesh[] previousMeshes;static Material[] previousMaterials;static NocturnisEnvironmentRoot previousOwner;
  static void Check(bool okay,string message){report.assertions++;if(!okay)throw new Exception(message);}
  static string GridSignature(DungeonGrid grid){var s=new System.Text.StringBuilder();for(int x=0;x<DungeonGrid.Width;x++)for(int z=0;z<DungeonGrid.Height;z++)s.Append(grid.Walkable(x,z)?'1':'0');return s.ToString();}
  public static Report ValidateGeometry() {
   report=new Report();var go=new GameObject("Environment audit camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=9;camera.aspect=16f/9;
   try {
    for(int zone=0;zone<Campaign.Count;zone++) {
     var grid=new DungeonGrid(zone);string before=GridSignature(grid);var layout=new NocturnisEnvironmentLayout(grid,zone);var mesh=layout.Surface();
     try {
      Check(before==GridSignature(grid),"Landscape changed navigation "+zone);
      var vertices=mesh.vertices;var triangles=mesh.triangles;var covered=new HashSet<Vector2Int>();
      Check(vertices.Length<8500&&triangles.Length/3<16500,"Landscape geometry budget "+zone);
      Check(mesh.normals.All(n=>n.sqrMagnitude>.95f&&n.y>.15f),"Landscape normals "+zone);
      foreach(var p in vertices)Check(!float.IsNaN(p.y)&&!float.IsInfinity(p.y),"Finite landscape height");
      for(int i=0;i<triangles.Length;i+=3) {
       Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]];
       Check(Vector3.Cross(b-a,c-a).y>0,"Ground faces upwards "+zone);Vector3 p=(a+b+c)/3;
       covered.Add(new Vector2Int(Mathf.FloorToInt(p.x/1.8f),Mathf.FloorToInt(p.z/1.8f)));
      }
      // Real triangle coverage at the camera corners, not the generator's distance formula.
      var cells=new List<Vector2Int>();for(int x=0;x<DungeonGrid.Width;x++)for(int z=0;z<DungeonGrid.Height;z++)if(grid.Walkable(x,z))cells.Add(new Vector2Int(x,z));
      var focus=new[]{cells.OrderBy(c=>c.x).First(),cells.OrderByDescending(c=>c.x).First(),cells.OrderBy(c=>c.y).First(),cells.OrderByDescending(c=>c.y).First()};
      int samples=0;var plane=new Plane(Vector3.up,Vector3.down*2.8f);
      foreach(var cell in focus) {
       Vector3 p=new Vector3(cell.x*1.8f,0,cell.y*1.8f);camera.transform.position=p+new Vector3(9,15,-11);camera.transform.LookAt(p);
       foreach(float u in new[]{0f,.5f,1f})foreach(float v in new[]{0f,.5f,1f}) {
        var ray=camera.ViewportPointToRay(new Vector3(u,v));float distance;Check(plane.Raycast(ray,out distance),"Camera intersects background");
        var hit=ray.GetPoint(distance);Check(covered.Contains(new Vector2Int(Mathf.FloorToInt(hit.x/1.8f),Mathf.FloorToInt(hit.z/1.8f))),"Black void in camera footprint "+zone+" "+u+","+v);samples++;
       }
      }
      if(Campaign.Outdoors(zone))foreach(var c in cells)Check(Mathf.Abs(layout.Height(c.x,c.y)+.025f)<.0001f,"Walking floor stays level");
      if(Campaign.Underground(zone))Check(vertices.All(p=>p.y<=-1.1f),"Crypt recess stays below masonry");
      if(zone==Campaign.Town)Check(vertices.All(p=>p.y<=-.29f),"Town backing stays below paving");
      report.zones.Add(new ZoneResult{zone=zone,name=Campaign.Names[zone],vertices=vertices.Length,triangles=triangles.Length/3,frustumSamples=samples});
     }finally{UnityEngine.Object.DestroyImmediate(mesh);}
    }return report;
   }finally{UnityEngine.Object.DestroyImmediate(go);}
  }
  public static void Run() {
   Directory.CreateDirectory("Logs");try{ValidateGeometry();File.WriteAllText(Output,JsonUtility.ToJson(report,true));}
   catch(Exception e){if(report==null)report=new Report();report.failure=e.ToString();File.WriteAllText(Output,JsonUtility.ToJson(report,true));Debug.LogException(e);EditorApplication.Exit(1);return;}
   KryptaGame.Testing=true;EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool(Session,true);EditorApplication.isPlaying=true;
  }
  [InitializeOnLoadMethod]static void Restore() {
   if(!SessionState.GetBool(Session,false))return;report=File.Exists(Output)?JsonUtility.FromJson<Report>(File.ReadAllText(Output)):new Report();
   KryptaGame.Testing=true;nextZone=stage=0;started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
  }
  static void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors=true;}
  static void Tick() {
   if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<nextTick)return;nextTick=EditorApplication.timeSinceStartup+.4;
   if(EditorApplication.timeSinceStartup-started>210){Finish(false,"Timeout");return;}var game=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!game||!game.View)return;
   try {
    if(stage==0){typeof(KryptaGame).GetMethod("LoadZone",Hidden).Invoke(game,new object[]{nextZone});transitionFrame=Time.frameCount;transitionAt=EditorApplication.timeSinceStartup;stage=1;return;}
    // EditorApplication.update is not a rendered/gameplay frame boundary.
    // The old root is deferred-destroyed at frame end; its OnDestroy then queues
    // its generated meshes/materials for destruction. Wait for both lifecycles.
    int frames=Time.frameCount-transitionFrame;double wait=EditorApplication.timeSinceStartup-transitionAt;
    if(frames<3){if(wait>8)throw new Exception("Game frames did not advance after zone "+nextZone+": "+frames);EditorApplication.QueuePlayerLoopUpdate();return;}
    if(previousMeshes!=null)
    {
     bool released=!previousOwner&&previousMeshes.All(v=>!v)&&previousMaterials.All(v=>!v);
     if(!released && wait<8){EditorApplication.QueuePlayerLoopUpdate();return;}
     Check(released,"Environment resource leak after "+frames+" game frames / "+wait.ToString("0.00")+" s into zone "+nextZone+": owner="+(previousOwner?previousOwner.name:"destroyed")+" meshes="+string.Join(",",previousMeshes.Where(v=>v).Select(v=>v.name))+" materials="+string.Join(",",previousMaterials.Where(v=>v).Select(v=>v.name)));
    }
    report.zones[nextZone].releaseFrames=frames;report.zones[nextZone].releaseWaitSeconds=(float)wait;
    var owner=UnityEngine.Object.FindFirstObjectByType<NocturnisEnvironmentRoot>();Check(owner&&owner.zone==nextZone,"Environment installed");
    var props=owner.GetComponentsInChildren<NocturnisBackdropProp>();Check(props.Length==owner.backdropProps&&props.Length>0&&props.Length<=NocturnisEnvironmentLayout.PropBudget,"Backdrop count budget");
    Check(!owner.GetComponentsInChildren<Collider>().Any(c=>c.enabled),"No backdrop colliders");var grid=new DungeonGrid(nextZone);
    foreach(var prop in props) {
     Bounds b;Check(ModelBounds.TryGet(prop.gameObject,out b),"Backdrop geometry exists");
     for(int x=0;x<DungeonGrid.Width;x++)for(int z=0;z<DungeonGrid.Height;z++)if(grid.Walkable(x,z))
      Check(b.max.x<x*1.8f-.91f||b.min.x>x*1.8f+.91f||b.max.z<z*1.8f-.91f||b.min.z>z*1.8f+.91f,"Backdrop blocks walking cell "+nextZone+" "+x+","+z+" "+prop.name);
    }
    var light=GameObject.Find("Soft moon fill on actors").GetComponent<Light>();Check(light.cullingMask==(1<<KryptaGame.ActorReadabilityLayer)&&light.shadows==LightShadows.None,"Actor fill excludes scenery and portraits");
    var world=(Transform)typeof(KryptaGame).GetField("world",Hidden).GetValue(game);Check(world.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.gameObject.layer==KryptaGame.ActorReadabilityLayer),"Actors receive fill");
    foreach(var mesh in owner.meshes)Check(mesh&&mesh.normals.All(n=>n.sqrMagnitude>.9f),"Valid environment normals");
    report.zones[nextZone].backdropProps=props.Length;report.runtimeZones++;previousOwner=owner;previousMeshes=owner.meshes.ToArray();previousMaterials=owner.materials.ToArray();
    if(++nextZone>=Campaign.Count){Check(!errors,"No Unity errors during map transitions");Finish(true,null);return;}stage=0;
   }catch(Exception e){Finish(false,e.ToString());}
  }
  static void Finish(bool passed,string failure) {
   SessionState.SetBool(Session,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;report.passed=passed;report.failure=failure;
   Directory.CreateDirectory("Logs");File.WriteAllText(Output,JsonUtility.ToJson(report,true));Debug.Log("ENVIRONMENT_POLISH_QA "+(passed?"PASS":"FAIL")+" "+report.assertions+" "+failure);EditorApplication.Exit(passed?0:1);
  }
 }
}
