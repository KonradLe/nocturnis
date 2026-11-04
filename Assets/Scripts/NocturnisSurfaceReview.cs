using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Krypta {
 public sealed partial class KryptaGame {
  [Serializable]sealed class SurfaceReviewResult {
   public bool passed;public string note="Actual Road scene captured through a separate review camera in isolated Testing mode. No UI, material replacement or postprocessing is applied to the screenshots.";
   public string[] screenshots;public string[] wagonMaterials;public int generatedTextures,generatedMeshes;public string zone;
  }
  public IEnumerator CaptureSurfaceReview(string directory) {
   if(!Testing)throw new InvalidOperationException("Surface capture requires isolated Testing mode.");
   Directory.CreateDirectory(directory);PrepareReviewScene(Campaign.Road,0,1);
   yield return null;yield return null;
   float previousScale=Time.timeScale;Time.timeScale=0;
   var cameraObject=new GameObject("Surface review camera");var camera=cameraObject.AddComponent<Camera>();camera.CopyFrom(View);camera.enabled=false;camera.cullingMask=~(1<<30);
   var targetTexture=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);targetTexture.name="Surface review buffer";camera.targetTexture=targetTexture;
   var image=new Texture2D(1280,720,TextureFormat.RGB24,false);var active=RenderTexture.active;
   try {
    var wagon=GameObject.Find("Rozbity opancerzony wóz");if(!wagon)throw new InvalidOperationException("Road wagon missing");
    var owner=world.GetComponent<NocturnisRoadSurfaceOwner>();if(!owner)throw new InvalidOperationException("Road material palette missing");
    var result=new SurfaceReviewResult{zone=Campaign.Names[Campaign.Road],screenshots=new[]{"road-scene.png","road-wagon-detail.png"},wagonMaterials=wagon.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Select(m=>m.name).Distinct().ToArray(),generatedTextures=owner.owned.OfType<Texture>().Count(),generatedMeshes=owner.owned.OfType<Mesh>().Count()};
    for(int shot=0;shot<2;shot++) {
     Vector3 aim=shot==0?Vector3.Lerp(HeroPosition,wagon.transform.position,.36f)+Vector3.up*.55f:wagon.transform.position+Vector3.up*.85f;
     camera.orthographic=true;camera.orthographicSize=shot==0?8.7f:2.65f;
     camera.transform.position=aim+(shot==0?new Vector3(9,15,-11):new Vector3(6,6,8));camera.transform.LookAt(aim);
     camera.Render();RenderTexture.active=targetTexture;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply(false);
     string file=Path.Combine(directory,result.screenshots[shot]);File.WriteAllBytes(file,image.EncodeToPNG());
     if(new FileInfo(file).Length<20000)throw new InvalidOperationException("Empty surface screenshot: "+file);
     Debug.Log("SURFACE_REVIEW_FRAME "+result.screenshots[shot]);yield return null;
    }
    result.passed=true;File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(result,true));
   } finally {
    RenderTexture.active=active;camera.targetTexture=null;targetTexture.Release();Destroy(targetTexture);Destroy(image);Destroy(cameraObject);Time.timeScale=previousScale;
   }
  }
 }
}
