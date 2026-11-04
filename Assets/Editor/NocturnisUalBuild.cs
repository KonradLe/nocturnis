using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
namespace Krypta.Editor
{
 public static class NocturnisUalBuild
 {
  public static void Windows()
  {
   Directory.CreateDirectory("Logs");
   try
   {
    NocturnisUalImport.AssertApplied();
    PlayerSettings.bundleVersion="0.7.4";
    Directory.CreateDirectory("Builds/UALWindows");
    var scenes=EditorBuildSettings.scenes.Where(scene=>scene.enabled).Select(scene=>scene.path).ToArray();
    if(scenes.Length==0)throw new Exception("No enabled build scenes");
    var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName="Builds/UALWindows/Nocturnis.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
    if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Audit Windows build failed: "+result.summary.result);
    File.WriteAllText("Logs/ual-build-result.json","{\"passed\":true,\"version\":\"0.7.4\",\"bytes\":"+result.summary.totalSize+"}");
   }
   catch(Exception error)
   {
    File.WriteAllText("Logs/ual-build-result.json","{\"passed\":false,\"version\":\"0.7.4\"}");
    UnityEngine.Debug.LogException(error);EditorApplication.Exit(1);
   }
  }
 }
}
