using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using UnityEditor.Build.Reporting;
namespace Krypta.Editor {
 public static class NocturnisMasonryBuild {
  static readonly string[] Ids={"monastery_stone_floor","slate_floor_02","stone_floor"};
  static Texture2D Map(string id,string channel,bool normal,bool colour) {
   string dir="Assets/Resources/Masonry/"+id;
   string path=Directory.GetFiles(dir,id+"_"+channel+"_2k.*").First(p=>!p.EndsWith(".meta"));
   var importer=(TextureImporter)AssetImporter.GetAtPath(path);
   importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
   importer.sRGBTexture=colour;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;
   importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;importer.isReadable=false;
   importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;
   importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
  }
  public static void Prepare() {
   try {
    Directory.CreateDirectory("Logs");AssetDatabase.Refresh();
    var shader=Shader.Find(NocturnisMasonry.ShaderName);if(!shader)throw new Exception("Masonry shader missing");
    if(ShaderUtil.GetShaderMessages(shader).Any(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error))throw new Exception("Masonry shader compile error");
    foreach(string id in Ids) {
     string path="Assets/Resources/Masonry/"+id+"/Floor.mat";
     var material=AssetDatabase.LoadAssetAtPath<Material>(path);
     if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
     material.shader=shader;material.SetTexture("_MainTex",Map(id,"diff",false,true));
     material.SetTexture("_BumpMap",Map(id,"nor_gl",true,false));
     material.SetTexture("_RoughnessMap",Map(id,"rough",false,false));
     material.SetTexture("_OcclusionMap",Map(id,"ao",false,false));
     material.SetFloat("_TileMeters",id=="stone_floor"?2.4f:id=="slate_floor_02"?2.6f:2.7f);
     material.SetFloat("_BumpScale",id=="slate_floor_02"?.95f:1.12f);
     material.SetFloat("_Saturation",id=="slate_floor_02"?.22f:.38f);
     material.SetFloat("_DirtStrength",.32f);material.SetFloat("_Smoothness",.22f);
     material.enableInstancing=true;EditorUtility.SetDirty(material);
     foreach(string property in new[]{"_MainTex","_BumpMap","_RoughnessMap","_OcclusionMap"})
      if(!material.GetTexture(property))throw new Exception("Missing map "+id+"/"+property);
    }
    AssetDatabase.SaveAssets();NocturnisUalImport.AssertApplied();
    File.WriteAllText("Logs/masonry-prepare.json","{\"passed\":true,\"profiles\":3,\"maps\":12,\"resolution\":2048}");
   }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
  public static void Windows() {
   try {
    NocturnisUalImport.AssertApplied();PlayerSettings.bundleVersion="0.8.3";
    Directory.CreateDirectory("Logs");Directory.CreateDirectory("Builds/Floors083Windows");
    var r=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName="Builds/Floors083Windows/Nocturnis.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
    if(r.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed "+r.summary.result);
    File.WriteAllText("Logs/masonry-build.json","{\"passed\":true,\"version\":\"0.8.3\",\"bytes\":"+r.summary.totalSize+"}");
   }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
 }
}
