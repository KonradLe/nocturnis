using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
namespace Krypta.Editor {
 public static class NocturnisCinematicBuild {
  public static void Windows(){
   try{
    Directory.CreateDirectory("Builds/CinematicWindows");PlayerSettings.bundleVersion="0.7.1";
    var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(x=>x.enabled).Select(x=>x.path).ToArray(),locationPathName="Builds/CinematicWindows/Nocturnis.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.CleanBuildCache});
    if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Cinematic build failed");
    File.WriteAllText("Logs/cinematic-build-result.json","{\"passed\":true,\"bytes\":"+result.summary.totalSize+"}");
   }catch(Exception e){UnityEngine.Debug.LogException(e);EditorApplication.Exit(1);}
  }
 }
}
