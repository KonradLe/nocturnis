using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
namespace Krypta.Editor {
 public static class NocturnisGrip075 {
  const string Folder="Logs/Grip075";
  [Serializable] public class HandData { public int hero; public string side; public Vector3 slot,hand; public float height;public Vector3[] points; }
  [Serializable] public class Report { public bool passed;public string[] images;public HandData[] hands; }
  static Transform Bone(GameObject obj,string name){return obj.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);}
  public static void Capture(){
   try {
    Directory.CreateDirectory(Folder);
    UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
    RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.38f,.4f,.43f);RenderSettings.fog=false;
    var camera=new GameObject("Grip review camera").AddComponent<Camera>();camera.orthographic=true;camera.nearClipPlane=.01f;camera.farClipPlane=20;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.07f,.09f);
    var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.25f;key.transform.rotation=Quaternion.Euler(35,-40,0);
    var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.75f;fill.transform.rotation=Quaternion.Euler(20,140,0);
    var art=Resources.Load<KryptaAssets>("KryptaAssets");var images=new List<string>();var hands=new List<HandData>();
    for(int h=0;h<5;h++){
     var character=art.heroes[h];var actor=new ActorVisual(character.model,character.animations,Vector3.zero,1.8f,"Grip hero "+h);actor.Equip(character.weapon);actor.PreviewIdle(.15f);
     var skin=actor.Root.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();skin.BakeMesh(baked);
     foreach(string side in new[]{"r","l"}){
      var slot=Bone(actor.Root,"handslot."+side);var hand=Bone(actor.Root,side=="r"?"handR":"handL");
      var points=baked.vertices.Select(v=>skin.transform.TransformPoint(v)).Where(v=>Vector3.Distance(v,slot.position)<.15f).ToArray();
      hands.Add(new HandData{hero=h,side=side,slot=slot.position,hand=hand.position,height=1.8f,points=points});
      for(int view=0;view<3;view++){
       Vector3 target=slot.position;Vector3 direction=view==0?new Vector3(0,.1f,1):view==1?new Vector3(-1,.1f,.2f):new Vector3(.7f,.35f,.8f);
       camera.orthographicSize=.22f;camera.transform.position=target+direction.normalized*2;camera.transform.LookAt(target);
       string file="hero-"+h+"-"+side+"-"+view+".png";Save(camera,Folder+"/"+file);images.Add(file);
      }
     }
     camera.orthographicSize=1.15f;camera.transform.position=new Vector3(2,1.7f,4);camera.transform.LookAt(new Vector3(0,.95f,0));string full="hero-"+h+"-full.png";Save(camera,Folder+"/"+full);images.Add(full);
     UnityEngine.Object.DestroyImmediate(baked);UnityEngine.Object.DestroyImmediate(actor.Root);
    }
    File.WriteAllText(Folder+"/diagnostic.json",JsonUtility.ToJson(new Report{passed=true,images=images.ToArray(),hands=hands.ToArray()},true));EditorApplication.Exit(0);
   }catch(Exception e){Debug.LogException(e);File.WriteAllText(Folder+"/failure.txt",e.ToString());EditorApplication.Exit(1);}
  }
  static void Save(Camera camera,string path){
   var holders=new List<GameObject>();var skins=UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None).Where(s=>s.enabled).ToArray();var meshes=new List<Mesh>();
   foreach(var skin in skins){var m=new Mesh();skin.BakeMesh(m);var o=new GameObject("Baked pose");o.transform.SetParent(skin.transform,false);o.AddComponent<MeshFilter>().sharedMesh=m;o.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;holders.Add(o);meshes.Add(m);}
   var rt=new RenderTexture(960,960,24);camera.targetTexture=rt;var old=RenderTexture.active;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(960,960,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,960,960),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=old;rt.Release();
   UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
   foreach(var o in holders)UnityEngine.Object.DestroyImmediate(o);foreach(var m in meshes)UnityEngine.Object.DestroyImmediate(m);foreach(var s in skins)s.enabled=true;
  }
 }
}
