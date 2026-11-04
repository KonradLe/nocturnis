using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Krypta.Editor
{
 // A separate review build; the installed release and its save profile are untouched.
 public static class NocturnisMenuBuild
 {
  public static void Linux(){Build(BuildTarget.StandaloneLinux64,"Builds/ClassicMenu/Linux/Nocturnis.x86_64");}
  public static void Windows(){Build(BuildTarget.StandaloneWindows64,"Builds/ClassicMenu/Windows/Nocturnis.exe");}
  public static void KayKitLinux(){BuildKayKit(BuildTarget.StandaloneLinux64,"Builds/KayKitMenu/Linux/Nocturnis.x86_64");}
  public static void KayKitWindows(){BuildKayKit(BuildTarget.StandaloneWindows64,"Builds/KayKitMenu/Windows/Nocturnis.exe");}
  public static void StorybookLinux(){BuildStorybook(BuildTarget.StandaloneLinux64,"Builds/Storybook/Linux/Nocturnis.x86_64");}
  public static void StorybookWindows(){BuildStorybook(BuildTarget.StandaloneWindows64,"Builds/Storybook/Windows/Nocturnis.exe");}
  static void BuildKayKit(BuildTarget target,string path)
  {
   var config=Resources.Load<KryptaAssets>("KryptaAssets");
   if(!NocturnisKayKitImport.IsReady || !config || config.version<8 || config.heroes==null || config.heroes.Length!=5)
    throw new InvalidOperationException("The complete five-hero KayKit configuration is required for this preview.");
   Build(target,path,"0.4.4-preview.2");
  }
  static void BuildStorybook(BuildTarget target,string path)
  {
   var config=Resources.Load<KryptaAssets>("KryptaAssets");
   if(!NocturnisKayKitImport.IsReady || !config || config.version<8 || config.heroes==null || config.heroes.Length!=5)
    throw new InvalidOperationException("The complete five-hero KayKit configuration is required for the storybook preview.");
   foreach(var hero in config.heroes)
    if(hero==null || !hero.model || hero.animations==null || hero.animations.Length<8)
     throw new InvalidOperationException("Every KayKit hero must include a model and all eight animations.");
   ImportUITexture("Assets/Resources/UI/StorybookMenu.png");
   if(!Resources.Load<Texture2D>("UI/StorybookMenu"))
    throw new InvalidOperationException("The storybook menu illustration is missing from runtime resources.");
   Build(target,path,"0.4.4-storybook.1");
  }
  static void ImportUITexture(string path)
  {
   var texture=AssetImporter.GetAtPath(path) as TextureImporter;
   if(texture==null)throw new InvalidOperationException("Missing UI texture: "+path);
   texture.textureType=TextureImporterType.Default;texture.mipmapEnabled=false;texture.alphaIsTransparency=true;
   texture.wrapMode=TextureWrapMode.Clamp;texture.textureCompression=TextureImporterCompression.Uncompressed;
   texture.maxTextureSize=2048;texture.SaveAndReimport();
  }
  static void Build(BuildTarget target,string path,string buildVersion="0.4.4-menu.1")
  {
   ImportUITexture("Assets/Resources/UI/ClassicPanel.png");
   var scenes=EditorBuildSettings.scenes;
   if(scenes.Length==0)throw new InvalidOperationException("No configured game scene.");
   Directory.CreateDirectory(Path.GetDirectoryName(path));
   string version=PlayerSettings.bundleVersion;
   try
   {
    PlayerSettings.bundleVersion=buildVersion;
    var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=Array.ConvertAll(scenes,s=>s.path),locationPathName=path,target=target,options=BuildOptions.None});
    if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Menu build failed: "+report.summary.result);
    Debug.Log("Menu review build "+buildVersion+" succeeded: "+path);
   }
   finally{PlayerSettings.bundleVersion=version;}
  }
 }
}
