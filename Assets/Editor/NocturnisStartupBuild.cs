using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
namespace Krypta.Editor {
 public static class NocturnisStartupBuild {
  public static void Windows(){
   try{
    Directory.CreateDirectory("Builds/StartupWindows");PlayerSettings.bundleVersion="0.7.2";
    var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(x=>x.enabled).Select(x=>x.path).ToArray(),locationPathName="Builds/StartupWindows/Nocturnis.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
    if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Startup build failed");
    File.WriteAllText("Logs/startup-build-result.json","{\"passed\":true,\"bytes\":"+result.summary.totalSize+"}");
   }catch(Exception e){UnityEngine.Debug.LogException(e);EditorApplication.Exit(1);}
  }
 }
}
