using System;using System.IO;using System.Collections;using System.Collections.Generic;using UnityEngine;
namespace Krypta {
 public sealed class NocturnisMasonryReview:MonoBehaviour {
  string output,error="";float started;bool finished;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Launch() {
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"-masonry-review")<0)return;
   KryptaGame.Testing=true;new GameObject("Opt-in masonry review").AddComponent<NocturnisMasonryReview>();
  }
  IEnumerator Start() {
   var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-masonry-output");
   if(i<0||i+1>=args.Length){Application.Quit(1);yield break;}
   output=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(output);started=Time.realtimeSinceStartup;
   Application.logMessageReceived+=Log;
   KryptaGame game;while(!(game=FindFirstObjectByType<KryptaGame>())||!game.View)yield return null;
   yield return game.MasonryCapture(output);
   finished=true;Application.logMessageReceived-=Log;Application.Quit(0);
  }
  void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)error=m+"\n"+s;}
  void Update() {
   if(finished||output==null)return;
   if(error.Length==0&&Time.realtimeSinceStartup-started<300)return;
   finished=true;StopAllCoroutines();File.WriteAllText(Path.Combine(output,"failure.txt"),error.Length>0?error:"Timeout");Application.Quit(1);
  }
 }
 public sealed partial class KryptaGame {
  [Serializable]public sealed class MasonryResult {
   public bool passed;public int zone,materials,meshes,vertices,triangles,normalMappedVertices,expectedTiles,exposedMetalBatches;public string profile,error;
  }
  public void MasonryFixture(int zone) {
   if(!Testing)throw new InvalidOperationException("Masonry fixture is QA-only");
   PrepareReviewScene(zone,0,8);ReviewPosition(World(12,18));cameraZoom=7.5f;
   GetComponent<NocturnisSettings>().SetPreset(NocturnisSettings.GraphicsPreset.High);
  }
  public MasonryResult MasonryInspect() {
   var result=new MasonryResult{zone=Zone,profile=NocturnisMasonry.Profile(Zone)};
   bool valid=true;var covered=new HashSet<int>();float floorY=float.NegativeInfinity;
   foreach(var renderer in world.GetComponentsInChildren<MeshRenderer>()) {
    var material=renderer.sharedMaterial;if(!NocturnisMasonry.IsMasonry(material))continue;
    result.materials++;floorY=Mathf.Max(floorY,renderer.bounds.max.y);
    var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh){valid=false;continue;}
    var mesh=filter.sharedMesh;result.meshes++;result.vertices+=mesh.vertexCount;result.triangles+=mesh.triangles.Length/3;
    foreach(string property in new[]{"_MainTex","_BumpMap","_RoughnessMap","_OcclusionMap"})if(!material.GetTexture(property))valid=false;
    var vertices=mesh.vertices;var uv=mesh.uv;var normals=mesh.normals;var tangents=mesh.tangents;float metres=material.GetFloat("_TileMeters");
    if(uv.Length!=vertices.Length||tangents.Length!=vertices.Length||normals.Length!=vertices.Length){valid=false;continue;}
    for(int i=0;i<vertices.Length;i++) {
     if(Vector2.Distance(uv[i],new Vector2(vertices[i].x,vertices[i].z)/metres)>.001f)valid=false;
     if(float.IsNaN(tangents[i].x)||float.IsNaN(tangents[i].y)||float.IsNaN(tangents[i].z))valid=false;
     if(normals[i].y>.8f){result.normalMappedVertices++;if(new Vector3(tangents[i].x,tangents[i].y,tangents[i].z).sqrMagnitude<.8f)valid=false;}
    }
    
    for(int i=0;i<vertices.Length;i+=4) {
     if(i+3>=vertices.Length){valid=false;break;}
     Vector3 centre=(vertices[i]+vertices[i+1]+vertices[i+2]+vertices[i+3])*.25f;
     int x=Cell(centre.x),z=Cell(centre.z);
     if(!covered.Add(x*DungeonGrid.Height+z))valid=false;
     if(Vector2.Distance(new Vector2(centre.x,centre.z),new Vector2(x*Tile,z*Tile))>.001f)valid=false;
    }
    if(!renderer.receiveShadows)valid=false;

   }
   
   
   
   foreach(var renderer in world.GetComponentsInChildren<MeshRenderer>()) {
    var filter=renderer.GetComponent<MeshFilter>();
    if(!filter||!filter.sharedMesh||filter.sharedMesh.name!="Metalowe detale posadzki")continue;
    if(renderer.bounds.max.y<=floorY+.001f)valid=false;
    else result.exposedMetalBatches++;
   }
   // Destructible crates close navigation cells AFTER the floor is built.

   // The authored grid is the reference: stone must also remain under those
   // crates when the player breaks them. Do not compare to temporary blockers.
   var authored=new DungeonGrid(Zone);
   for(int x=0;x<DungeonGrid.Width;x++)for(int z=0;z<DungeonGrid.Height;z++)
    if((authored.Walkable(x,z)||authored.Border(x,z))&&!TerrainWaterCell(x,z)) {
     result.expectedTiles++;if(!covered.Contains(x*DungeonGrid.Height+z))valid=false;
    }

   result.passed=valid&&result.meshes==1&&result.exposedMetalBatches>0&&result.normalMappedVertices==result.expectedTiles*4&&result.vertices==result.expectedTiles*4&&result.triangles==result.expectedTiles*2;

   if(!result.passed)result.error="Missing PBR maps, invalid world UVs/tangents or wrong authored floor coverage";
   return result;
  }
  [Serializable]sealed class MasonryCaptureReport {public bool passed;public string note="Actual game rendering at default gameplay zoom; prepared QA checkpoint.";public List<MasonryResult> zones=new List<MasonryResult>();public List<string> screenshots=new List<string>();}
  public IEnumerator MasonryCapture(string directory) {
   var report=new MasonryCaptureReport();State=Phase.Title;Begin();
   int[] zones={Campaign.Crypt,Campaign.Crypt2,Campaign.Kings,Campaign.Cistern,Campaign.Forge,Campaign.Heart,Campaign.Cellar};
   foreach(int zone in zones) {
    MasonryFixture(zone);yield return new WaitForSeconds(.3f);Time.timeScale=0;
    var check=MasonryInspect();report.zones.Add(check);if(!check.passed)throw new Exception(check.error);
    yield return new WaitForEndOfFrame();
    string name="floor-"+zone+".png";ScreenCapture.CaptureScreenshot(Path.Combine(directory,name));
    for(int frame=0;frame<4;frame++)yield return new WaitForEndOfFrame();
    report.screenshots.Add(name);Time.timeScale=1;
   }
   GetComponent<NocturnisSettings>().SetPreset(NocturnisSettings.GraphicsPreset.Low);
   yield return new WaitForSeconds(.2f);Time.timeScale=0;yield return new WaitForEndOfFrame();
   ScreenCapture.CaptureScreenshot(Path.Combine(directory,"floor-low.png"));
   for(int frame=0;frame<4;frame++)yield return new WaitForEndOfFrame();
   report.screenshots.Add("floor-low.png");
   report.passed=true;File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(report,true));Debug.Log("MASONRY_REVIEW_PASS");
  }
 }
}
