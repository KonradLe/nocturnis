using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
namespace Krypta.Editor {
 public static class NocturnisPolish075Build {
  public static void MusicImport(){
   foreach(var cue in NocturnisMusic.Required){
    string path="Assets/Resources/NocturnisMusic/"+cue+".ogg";
    var importer=AssetImporter.GetAtPath(path) as AudioImporter;
    if(!importer)throw new Exception("Missing score "+cue);
    var settings=importer.defaultSampleSettings;
    settings.loadType=cue=="death"||cue=="victory"?AudioClipLoadType.CompressedInMemory:AudioClipLoadType.Streaming;
    settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.8f;
    settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
    importer.defaultSampleSettings=settings;importer.loadInBackground=true;
    importer.SaveAndReimport();
    var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    if(!clip || clip.length<(cue=="death"||cue=="victory"?9:50))throw new Exception("Invalid score length "+cue);
   }
  }
  public static void Windows(){
   Directory.CreateDirectory("Logs");
   try{
    NocturnisUalImport.AssertApplied();MusicImport();PlayerSettings.bundleVersion="0.7.5";
    Directory.CreateDirectory("Builds/Polish075Windows");
    var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
    if(scenes.Length==0)throw new Exception("No enabled scenes");
    var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName="Builds/Polish075Windows/Nocturnis.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
    if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed "+result.summary.result);
    File.WriteAllText("Logs/ual-build-result.json","{\"passed\":true,\"version\":\"0.7.5\",\"bytes\":"+result.summary.totalSize+"}");
   }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
 }
}
