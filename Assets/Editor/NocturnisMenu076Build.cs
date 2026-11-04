using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace Krypta.Editor
{
 public static class NocturnisMenu076Build
 {
  public static void Windows()
  {
   try
   {
    NocturnisUalImport.AssertApplied();PlayerSettings.bundleVersion="0.7.6";
    Directory.CreateDirectory("Logs");Directory.CreateDirectory("Builds/Menu076Windows");
    var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
    if(scenes.Length==0)throw new Exception("No enabled scenes");
    var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName="Builds/Menu076Windows/Nocturnis.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
    if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed "+result.summary.result);
    File.WriteAllText("Logs/menu076-build-result.json","{\"passed\":true,\"version\":\"0.7.6\",\"bytes\":"+result.summary.totalSize+"}");
   }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
 }
}
