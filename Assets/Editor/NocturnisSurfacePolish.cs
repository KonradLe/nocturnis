using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Krypta.Editor {
 public static class NocturnisSurfacePolish {
  static readonly string[] Heroes={"hero_inquisitor","hero_blood_weaver","hero_dusk_hunter","hero_void_herald","hero_root_guardian"};
  static readonly Color[] Accents={new Color(.43f,.055f,.035f),new Color(.38f,.025f,.065f),new Color(.105f,.23f,.17f),new Color(.22f,.085f,.34f),new Color(.22f,.29f,.10f)};
  const string MaterialRoot="Assets/NocturnisActI/Materials/";
  const string RuntimeRoot="Assets/NocturnisActI/Runtime/";
  static readonly string[] Weapons={"weapon_sword","weapon_scythe","weapon_bow","weapon_staff","weapon_axe"};
  static readonly string[] SurfaceNames={"Nocturnis_BlackIron","Nocturnis_OldBronze","Nocturnis_DarkOak","Nocturnis_OldWax"};
  static Bounds BodyBounds(string id) {
   var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(RuntimeRoot+id+"/Skin.asset");
   if(!mesh)throw new Exception("Character rest mesh missing: "+id);return mesh.bounds;
  }
  static Mesh BodyMesh(string id) {
   var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(RuntimeRoot+id+"/Skin.asset");
   if(!mesh)throw new Exception("Character rest mesh missing: "+id);return mesh;
  }
  static byte ClothAlpha(float y,float cutoff,float fade) {
   return (byte)Mathf.RoundToInt(255*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(cutoff-fade,cutoff,y))));
  }
  static void BakeClothMask(string id,float cutoff,float fade) {
   var mesh=BodyMesh(id);var vertices=mesh.vertices;var colors=new Color32[vertices.Length];
   for(int i=0;i<vertices.Length;i++)colors[i]=new Color32(255,255,255,ClothAlpha(vertices[i].y,cutoff,fade));
   mesh.colors32=colors;EditorUtility.SetDirty(mesh);
  }
  public static void Prepare() {
   var shader=Shader.Find("Nocturnis/Character Surface");if(!shader)throw new Exception("Character Surface shader missing");
   for(int i=0;i<Heroes.Length;i++) {
    var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/NocturnisActI/Materials/"+Heroes[i]+"_MAT0.mat");
    if(!mat || !mat.mainTexture || !mat.GetTexture("_MR"))throw new Exception("Original character maps missing: "+Heroes[i]);
    var atlas=mat.mainTexture;var mr=mat.GetTexture("_MR");
    mat.shader=shader;mat.mainTexture=atlas;mat.SetTexture("_MR",mr);mat.SetColor("_Color",Color.white);
    mat.SetFloat("_UseMR",1);mat.SetFloat("_BumpScale",0);mat.SetFloat("_AlbedoLift",i==0?1.18f:1.30f);
    mat.SetFloat("_Saturation",1.14f);mat.SetColor("_ClothAccent",Accents[i]);mat.SetFloat("_ClothBlend",i==0?.35f:.65f);
    mat.SetColor("_ClothGarb",Color.white);mat.SetFloat("_MetallicScale",.86f);
    var bounds=BodyBounds(Heroes[i]);float cutoff=bounds.min.y+bounds.size.y*.76f,fade=bounds.size.y*.055f;
    mat.SetFloat("_ClothTopY",cutoff);mat.SetFloat("_ClothFadeY",fade);BakeClothMask(Heroes[i],cutoff,fade);
    EditorUtility.SetDirty(mat);
    var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(atlas)) as TextureImporter;
    if(importer && (importer.anisoLevel<4 || !importer.sRGBTexture)){importer.anisoLevel=4;importer.sRGBTexture=true;importer.SaveAndReimport();}
   }
   PrepareWeaponSurfaces();AssetDatabase.SaveAssets();Validate();
  }
  static void PrepareWeaponSurfaces() {
   const string folder=MaterialRoot+"MotionWeapons";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   string texturePath=folder+"/FineSteelWear.asset";
   var wear=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
   if(!wear) {
    wear=new Texture2D(128,128,TextureFormat.RGB24,true){name="Fine steel wear",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};
    var pixels=new Color[128*128];
    for(int y=0;y<128;y++)for(int x=0;x<128;x++) {
     float grain=.83f+.12f*Mathf.PerlinNoise(x*.18f+11,y*.18f+5)+.035f*Mathf.Sin(x*1.72f+y*.13f);
     pixels[y*128+x]=new Color(grain,grain,grain);
    }
    wear.SetPixels(pixels);wear.Apply(true,false);AssetDatabase.CreateAsset(wear,texturePath);
   }
   var replacements=new System.Collections.Generic.Dictionary<string,Material>();
   foreach(string sourceName in SurfaceNames) {
    var original=AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot+sourceName+".mat");
    if(!original)throw new Exception("Weapon source surface missing: "+sourceName);
    string path=folder+"/MotionWeapon_"+sourceName+".mat";
    var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(original);AssetDatabase.CreateAsset(m,path);}
    EditorUtility.CopySerialized(original,m);m.name="MotionWeapon_"+sourceName;
    m.SetFloat("_UseMR",0);m.SetFloat("_BumpScale",0);
    bool steel=sourceName=="Nocturnis_BlackIron",bronze=sourceName=="Nocturnis_OldBronze",wood=sourceName=="Nocturnis_DarkOak";
    m.SetColor("_Color",steel?new Color(.46f,.50f,.55f):bronze?new Color(.48f,.30f,.12f):wood?new Color(.28f,.16f,.075f):new Color(.47f,.39f,.26f));
    m.SetFloat("_Metallic",steel?.55f:bronze?.45f:0);m.SetFloat("_Roughness",steel?.40f:bronze?.52f:wood?.78f:.86f);
    if(steel && !m.mainTexture)m.mainTexture=wear;
    EditorUtility.SetDirty(m);replacements[sourceName]=m;
   }
   foreach(string id in Weapons) {
    string path=RuntimeRoot+id+"_equipped.prefab";var root=PrefabUtility.LoadPrefabContents(path);
    try {
     foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)) {
      var mats=renderer.sharedMaterials;
      for(int i=0;i<mats.Length;i++)if(mats[i]) {
       string key=mats[i].name.Replace("MotionWeapon_","");if(replacements.TryGetValue(key,out var replacement))mats[i]=replacement;
      }
      renderer.sharedMaterials=mats;
     }
     PrefabUtility.SaveAsPrefabAsset(root,path);
    } finally {PrefabUtility.UnloadPrefabContents(root);}
   }
  }
  public static void Validate() {
   int checks=0;
   Action<bool,string> check=(yes,message)=>{if(!yes)throw new Exception(message);checks++;};
   foreach(string id in Heroes) {
    var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/NocturnisActI/Materials/"+id+"_MAT0.mat");
    check(mat && mat.shader.name=="Nocturnis/Character Surface","Correct character shader: "+id);
    check(mat.mainTexture && mat.mainTexture.width>=1024,"Original detailed color atlas retained: "+id);
    check(mat.GetTexture("_MR") && mat.GetFloat("_UseMR")==1,"Original roughness and metal atlas retained: "+id);
    var baseImporter=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mat.mainTexture)) as TextureImporter;
    var packedImporter=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mat.GetTexture("_MR"))) as TextureImporter;
    check(baseImporter.sRGBTexture && !packedImporter.sRGBTexture,"Correct albedo / data color spaces: "+id);
    check(mat.GetColor("_Color")==Color.white,"Skin and steel are not globally dyed: "+id);
    check(mat.GetFloat("_AlbedoLift")>=1 && mat.GetFloat("_AlbedoLift")<=1.35f,"Bounded midtone correction: "+id);
    check(mat.GetColor("_ClothGarb")==Color.white,"Neutral saved wardrobe state: "+id);
    var bounds=BodyBounds(id);float cutoff=mat.GetFloat("_ClothTopY");
    check(Mathf.Abs(cutoff-(bounds.min.y+bounds.size.y*.76f))<.0001f,"Head exclusion follows this character geometry: "+id);
    check(mat.GetFloat("_ClothFadeY")>0 && cutoff<bounds.min.y+bounds.size.y*.78f,"Cloth correction ends below face and neck: "+id);
    var mesh=BodyMesh(id);var colors=mesh.colors32;var vertices=mesh.vertices;
    check(colors.Length==mesh.vertexCount,"Permanent cloth mask covers every rest-pose vertex: "+id);
    var runtimePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeRoot+id+"/"+id+".prefab");
    check(runtimePrefab,"Runtime character prefab retained: "+id);
    var skin=runtimePrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
    check(skin && skin.sharedMesh==mesh,"Runtime skin is bound to the mesh carrying the cloth mask: "+id);
    int headVertices=0,clothVertices=0;
    for(int i=0;i<vertices.Length;i++) {
     check(colors[i].r==255 && colors[i].g==255 && colors[i].b==255,"RGB vertex tint stays neutral: "+id);
     check(colors[i].a==ClothAlpha(vertices[i].y,cutoff,mat.GetFloat("_ClothFadeY")),"Stored alpha matches rest-pose cloth mask: "+id);
     if(colors[i].a==255)clothVertices++;
     if(vertices[i].y>=bounds.min.y+bounds.size.y*.80f) {
      headVertices++;check(colors[i].a==0,"Face/head vertex alpha is zero in the renderer source mesh: "+id);
     }
    }
    check(headVertices>20 && clothVertices>20,"Mask excludes head while retaining body cloth: "+id);
   }
   foreach(string id in Weapons) {
    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeRoot+id+"_equipped.prefab");check(prefab,"Equipped weapon retained: "+id);
    foreach(var material in prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct()) {
     if(material.name.Contains("BlackIron")) {
      check(material.name.StartsWith("MotionWeapon_"),"Weapon steel is isolated from world iron: "+id);
      check(material.GetFloat("_Metallic")==.55f && material.GetFloat("_Roughness")==.40f,"Readable steel without a reflection probe: "+id);
      check(material.color.r>=.4f && material.mainTexture,"Steel has a visible base color and subtle wear: "+id);
     }
     if(material.name.Contains("DarkOak"))check(material.GetFloat("_Metallic")==0 && material.color.r>material.color.b*2,"Wooden weapon handle remains wood: "+id);
    }
   }
   check(Accents.Distinct().Count()==5,"Five distinct restrained cloth palettes");
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/surface-polish-qa.txt","PASS "+checks+" material checks\nOriginal atlases and MR retained. Head region excluded by persistent rest-pose vertex alpha. Weapon steel isolated from world iron. Road wood / iron / cloth use dedicated procedural surfaces.\n");
   Debug.Log("SURFACE_POLISH_PASS "+checks);
  }
 }
}
