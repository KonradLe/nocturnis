using System;using System.IO;using System.Linq;using System.Text.RegularExpressions;using UnityEngine;using UnityEditor;using UnityEditor.Build.Reporting;
namespace Krypta.Editor {
 public static class NocturnisLanguageBuild {
  [Serializable]sealed class Report{public bool passed;public string version="0.8.4";public int translations,checks;public string error="";}
  static int checks;
  static void Check(bool condition,string why){checks++;if(!condition)throw new Exception(why);}
  public static void Prepare() {
   var report=new Report();bool testing=KryptaGame.Testing;KryptaGame.Testing=true;
   string key=NocturnisLanguage.PreferenceKey,old=PlayerPrefs.GetString(key,"");bool existed=PlayerPrefs.HasKey(key);
   string production=PlayerPrefs.GetString("Nocturnis.Language","<missing>");
   try {
    AssetDatabase.Refresh();NocturnisUalImport.AssertApplied();NocturnisLanguage.Reload();NocturnisLanguage.Select("en");
    var catalog=JsonUtility.FromJson<NocturnisLanguage.Catalog>(Resources.Load<TextAsset>("Localization/strings").text);
    report.translations=catalog.entries.Length;Check(report.translations>1000,"Incomplete translation catalog");
    foreach(var row in catalog.entries) {
     Check(NocturnisLanguage.Text(row.pl)==row.en,"Exact translation mismatch: "+row.pl);
     string Tokens(string value)=>string.Join("|",Regex.Matches(value,@"\{[^}]+\}").Cast<Match>().Select(m=>m.Value));
     Check(Tokens(row.pl)==Tokens(row.en),"Changed input/format tokens: "+row.pl);
    }
    Check(NocturnisLanguage.Text("ŻYCIE 100  ·  POZIOM 8")=="HEALTH 100  ·  LEVEL 8","Concatenated HUD stats");
    Check(NocturnisLanguage.Text("<b>ŻYCIE</b> 100")=="<b>HEALTH</b> 100","Rich-text tags preserved");
    Check(NocturnisLanguage.Text("Nocturnis.Profile1.Checkpoint")=="Nocturnis.Profile1.Checkpoint","Identifier protection");
    Check(NocturnisLanguage.Text("nieaktywny / pokonany")=="inactive / defeated","Lower-case dynamic status");
    Check(NocturnisLanguage.Text("+0,81 HP/s • 8,0 m")=="+0.81 HP/s • 8.0 m","English numeric presentation");
    Check(NocturnisLanguage.Text("Wspólna skrytka")=="Shared Stash","Localized shared stash");
    Check(NocturnisLanguage.Text("Relikty Oakhaven")=="Relics of Oakhaven","Localized forge dialogue");
    NocturnisLanguage.Reload();Check(NocturnisLanguage.English&&!NocturnisLanguage.NeedsChoice,"English preference persists");
    NocturnisLanguage.Select("pl");Check(NocturnisLanguage.Text("ŻYCIE 100")=="ŻYCIE 100","Switch back to Polish");
    NocturnisLanguage.Reload();Check(!NocturnisLanguage.English&&!NocturnisLanguage.NeedsChoice,"Polish preference persists");
    Check(PlayerPrefs.GetString("Nocturnis.Language","<missing>")==production,"Production language changed during QA");
    Check(Resources.Load<TextAsset>("Credits-en")!=null,"Missing English credits");
    foreach(string file in new[]{"Nocturnis-Intro.mp4","Nocturnis-Intro-en.mp4"})
     Check(new FileInfo(Path.Combine(Application.streamingAssetsPath,file)).Length>1000000,"Missing localized intro");
    report.passed=true;
   }catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
   finally{
    if(existed)PlayerPrefs.SetString(key,old);else PlayerPrefs.DeleteKey(key);
    PlayerPrefs.Save();KryptaGame.Testing=testing;NocturnisLanguage.Reload();
    report.checks=checks;Directory.CreateDirectory("Logs");File.WriteAllText("Logs/language-prepare.json",JsonUtility.ToJson(report,true));
   }
   if(!report.passed)EditorApplication.Exit(1);
  }
  public static void Windows() {
   try{
    NocturnisUalImport.AssertApplied();PlayerSettings.bundleVersion="0.8.4";
    Directory.CreateDirectory("Logs");Directory.CreateDirectory("Builds/Language084Windows");
    var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(x=>x.enabled).Select(x=>x.path).ToArray(),locationPathName="Builds/Language084Windows/Nocturnis.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
    if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed "+result.summary.result);
    File.WriteAllText("Logs/language-build.json","{\"passed\":true,\"version\":\"0.8.4\",\"bytes\":"+result.summary.totalSize+"}");
   }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
 }
}
