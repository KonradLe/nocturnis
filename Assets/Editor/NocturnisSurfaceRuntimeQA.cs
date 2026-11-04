using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Krypta.Editor {
 public static class NocturnisSurfaceRuntimeQA {
  static int step,checks;static double start,next;static Material[] oldMaterials;static Texture[] oldTextures;static Mesh[] oldMeshes;static bool errors;
  const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
  [InitializeOnLoadMethod]static void Restore(){if(!SessionState.GetBool("Nocturnis.SurfaceRuntimeQA",false))return;KryptaGame.Testing=true;start=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
  public static void Run(){NocturnisSurfacePolish.Validate();KryptaGame.Testing=true;EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool("Nocturnis.SurfaceRuntimeQA",true);EditorApplication.isPlaying=true;}
  static void Log(string m,string s,LogType t){if(t==LogType.Exception||t==LogType.Error||t==LogType.Assert)errors=true;}
  static void Check(bool yes,string message){checks++;if(!yes)throw new Exception(message);}
  static void Tick(){
   if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.25;
   if(EditorApplication.timeSinceStartup-start>120){Finish(false,"Timed out");return;}
   var g=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!g||!g.View)return;
   try {
    if(step==0) {
     var owner=UnityEngine.Object.FindFirstObjectByType<NocturnisRoadSurfaceOwner>();Check(owner,"Road palette exists at starting zone");
     var wagon=GameObject.Find("Rozbity opancerzony wóz");Check(wagon,"Road wagon exists");
     var mats=wagon.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct().ToArray();Check(mats.Length==5,"Wood / split wood / iron / brass / cloth differentiated");
     foreach(var m in mats)Check(m && m.mainTexture && m.mainTexture.width>=128,"Wagon material has detailed texture: "+m.name);
     Check(owner.wood.color.r>owner.wood.color.b*2,"Warm timber color");Check(owner.cloth.color.r>owner.cloth.color.g*3,"Oxblood canvas accent");
     Check(owner.iron.GetFloat("_Metallic")>.5f && owner.wood.GetFloat("_Metallic")==0,"Wood and iron reflect differently");
     var meshFilters=wagon.GetComponentsInChildren<MeshFilter>();foreach(var f in meshFilters.Where(f=>owner.owned.Contains(f.sharedMesh))) {
      Check(f.sharedMesh.normals.All(n=>n.sqrMagnitude>.9f),"Generated prop has valid nonzero surface normals: "+f.name);
     }
     Check(wagon.GetComponentsInChildren<Transform>().Count(t=>t.name=="Szprycha")==32,"Four recognizable spoked wheels");
     foreach(var hero in Resources.Load<KryptaAssets>("KryptaAssets").heroes) {
      var instance=UnityEngine.Object.Instantiate(hero.model);
      KryptaGame.ApplyGarb(instance,2);
      foreach(var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>())for(int i=0;i<renderer.sharedMaterials.Length;i++) {
       var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,i);
       Check(block.GetColor("_ClothGarb").b==1 && block.GetColor("_ClothGarb").g<.5f,"Wardrobe changes cloth channel only");
       Check(renderer.sharedMaterials[i].GetColor("_Color")==Color.white,"Wardrobe preserves original metal / skin atlas");
      }
      UnityEngine.Object.Destroy(instance);
     }
     var key=GameObject.Find("Światło martwego słońca").GetComponent<Light>();Check((key.cullingMask&(1<<30))==0,"World light excluded from portrait layer");
     oldMaterials=owner.owned.OfType<Material>().ToArray();oldTextures=owner.owned.OfType<Texture>().ToArray();oldMeshes=owner.owned.OfType<Mesh>().ToArray();
     typeof(KryptaGame).GetMethod("LoadZone",Hidden).Invoke(g,new object[]{Campaign.Town});
    } else if(step==1) {
     Check(oldMaterials.All(v=>!v) && oldTextures.All(v=>!v) && oldMeshes.All(v=>!v),"Zone transition releases generated surface resources");
     typeof(KryptaGame).GetMethod("LoadZone",Hidden).Invoke(g,new object[]{Campaign.Road});
    } else {
     var owner=UnityEngine.Object.FindFirstObjectByType<NocturnisRoadSurfaceOwner>();Check(owner && owner.wood && owner.cloth.mainTexture,"Road palette rebuilds after returning");Check(!errors,"No Unity errors");Finish(true,"PASS "+checks+" runtime surface checks");return;
    }
    step++;
   } catch(Exception e){Finish(false,e.ToString());}
  }
  static void Finish(bool ok,string message){SessionState.SetBool("Nocturnis.SurfaceRuntimeQA",false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Directory.CreateDirectory("Logs");File.WriteAllText("Logs/surface-runtime-qa.txt",message);Debug.Log(message);EditorApplication.Exit(ok?0:1);}
 }
}
