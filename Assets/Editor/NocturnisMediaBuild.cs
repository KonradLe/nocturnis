using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
namespace Krypta.Editor {
 public static class NocturnisMediaBuild {
  public static void Windows(){
   try{
    Directory.CreateDirectory("Builds/MediaWindows");
    var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(x=>x.enabled).Select(x=>x.path).ToArray(),locationPathName="Builds/MediaWindows/Nocturnis.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
    if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Media build failed");
    File.WriteAllText("Logs/media-build-result.json","{\"passed\":true,\"bytes\":"+result.summary.totalSize+"}");
   }catch(Exception e){UnityEngine.Debug.LogException(e);EditorApplication.Exit(1);}
  }
 }
}
