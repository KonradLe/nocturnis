using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Krypta.Editor
{
 public static class NocturnisProfilesQA
 {
  const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  const string Session="Nocturnis.ProfilesQA";
  static double started;static int checks;static string errors="";
  static string Key(int p){return p==1 ? "Nocturnis.QA.Checkpoint" : "Nocturnis.QA.Profile"+p+".Checkpoint";}
  static object Call(KryptaGame game,string name,params object[] args){return typeof(KryptaGame).GetMethod(name,Private).Invoke(game,args);}
  static void Set(KryptaGame game,string name,object value){typeof(KryptaGame).GetProperty(name).SetValue(game,value);}
  static void Check(bool pass,string message){checks++;if(!pass)throw new Exception(message);}
  static bool Reload(KryptaGame game){bool loaded=game.ContinueCheckpoint();Time.timeScale=0;return loaded;}
  static bool Choose(KryptaGame game,int profile){Set(game,"State",KryptaGame.Phase.Title);return game.SelectProfile(profile);}
  [InitializeOnLoadMethod]static void Restore()
  {if(SessionState.GetBool(Session,false)){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}}
  public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool(Session,true);EditorApplication.isPlaying=true;}
  static void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors=message+"\n"+stack;}
  static void Tick()
  {
   if(EditorApplication.timeSinceStartup-started>180){Finish(false,"Timeout");return;}if(!EditorApplication.isPlaying)return;
   var game=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!game || !game.View)return;
   try{KryptaGame.Testing=true;game.enabled=false;var hud=game.GetComponent<KryptaHUD>();if(hud)hud.enabled=false;RunChecks(game,Check);Check(errors.Length==0,"No runtime errors");Finish(true,"");}
   catch(Exception ex){Finish(false,ex.ToString());}
  }
  public static void RunChecks(KryptaGame game,Action<bool,string> check)
  {
   KryptaGame.Testing=true;
   string[] qaKeys={Key(1),Key(2),Key(3),"Nocturnis.QA.LegacyCheckpoint","Nocturnis.QA.Stash"};
   var old=new Dictionary<string,string>();foreach(string key in qaKeys){old[key]=PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;PlayerPrefs.DeleteKey(key);}
   int oldActive=PlayerPrefs.GetInt("Nocturnis.QA.ActiveProfile",1),oldMarker=PlayerPrefs.GetInt("Nocturnis.QA.ProfileMigrationDone",0);
   PlayerPrefs.DeleteKey("Nocturnis.QA.ProfileMigrationDone");PlayerPrefs.SetInt("Nocturnis.QA.ActiveProfile",1);
   string[] playerKeys={"Nocturnis.Checkpoint.v2","Nocturnis.Checkpoint.v1","Nocturnis.Profile1.Checkpoint","Nocturnis.Profile2.Checkpoint","Nocturnis.Profile3.Checkpoint","Nocturnis.SharedStash.v1"};
   var player=new Dictionary<string,string>();foreach(string key in playerKeys)player[key]=PlayerPrefs.GetString(key,"");
   try
   {
    const string legacy="{\"version\":3,\"hero\":0,\"zone\":3,\"level\":2,\"coins\":71,\"xp\":0,\"potions\":2,\"weapon\":1,\"armor\":0,\"charm\":0}";
    PlayerPrefs.SetString("Nocturnis.QA.LegacyCheckpoint",legacy);
    check(Choose(game,1) && KryptaGame.HasCheckpoint,"Profile 1 discovers the former single-slot checkpoint");
    check(PlayerPrefs.GetString(Key(1))==legacy && PlayerPrefs.GetString("Nocturnis.QA.LegacyCheckpoint")==legacy,"Migration copies the exact record and leaves its legacy backup intact");
    check(Reload(game) && game.Coins==71 && game.SelectedHero==0,"Migrated first profile remains playable");Set(game,"Health",51.5f);Call(game,"SaveCheckpoint");
    for(int profile=2;profile<=3;profile++)
    {
     check(Choose(game,profile) && !KryptaGame.HasCheckpoint,"Unused profile has no fallback to another journey: "+profile);
     game.SelectHero(profile==2 ? 2 : 4);game.Begin();Time.timeScale=0;Set(game,"Coins",profile*111);Set(game,"Health",40f+profile);Call(game,"SaveCheckpoint");
    }
    check(!game.SelectProfile(1) && game.ActiveProfile==3,"Profile switching is rejected during gameplay");
    Set(game,"State",KryptaGame.Phase.Title);check(!game.SelectProfile(0) && !game.SelectProfile(4) && game.ActiveProfile==3,"Invalid profile IDs do not change the active slot");
    for(int profile=1;profile<=3;profile++)
    {
     check(Choose(game,profile) && KryptaGame.HasCheckpoint && Reload(game),"Independent profile resumes: "+profile);
     int hero=profile==1 ? 0 : profile==2 ? 2 : 4,coins=profile==1 ? 71 : profile*111;float health=profile==1 ? 51.5f : 40+profile;
     check(game.SelectedHero==hero && game.Coins==coins && Mathf.Abs(game.Health-health)<.001f,"Profile restores its own class, inventory economy and HP: "+profile);
    }
    Choose(game,1);Reload(game);game.Inventory.Add(new KryptaGame.Relic{slot=2,rank=4,enchant=3,affix=1,affixValue=6});Set(game,"Service","stash");
    check(game.StoreItem(0,false),"A profile can deposit a complete affixed relic into the shared stash");
    Choose(game,2);Reload(game);var stash=game.Stash;
    check(stash.Count==1 && stash[0].enchant==3 && stash[0].affixValue==6,"The other profile sees the shared stash with exact item properties");
    Set(game,"Service","stash");check(game.StoreItem(0,true) && game.Inventory.Exists(item=>item.affixValue==6 && item.enchant==3),"Withdrawing shared gear preserves all properties");
    string first=PlayerPrefs.GetString(Key(1)),third=PlayerPrefs.GetString(Key(3));
    Choose(game,2);game.SelectHero(1);game.Begin();Time.timeScale=0;
    check(PlayerPrefs.GetString(Key(1))==first && PlayerPrefs.GetString(Key(3))==third,"Starting a new journey only replaces the selected profile");
    Set(game,"State",KryptaGame.Phase.Title);string summary=game.SavedProfileSummary(3);
    check(game.ActiveProfile==2 && summary.Contains(NocturnisData.Heroes[4].person),"Reading another profile summary does not change the selection");
    Choose(game,1);Call(game,"ClearCheckpoint");Choose(game,3);Choose(game,1);
    check(!KryptaGame.HasCheckpoint && PlayerPrefs.GetString("Nocturnis.QA.LegacyCheckpoint")==legacy,"Cleared profile does not resurrect the legacy backup on a later selection");
    PlayerPrefs.SetString(Key(1),first);PlayerPrefs.SetString("Nocturnis.QA.LegacyCheckpoint",legacy.Replace("71","999"));PlayerPrefs.DeleteKey("Nocturnis.QA.ProfileMigrationDone");
    check(KryptaGame.HasCheckpoint && PlayerPrefs.GetString(Key(1))==first,"Migration never overwrites an existing profile with an older backup");
    foreach(string key in playerKeys)check(PlayerPrefs.GetString(key,"")==player[key],"Production key untouched: "+key);
   }
   finally
   {
    foreach(var pair in old){if(pair.Value==null)PlayerPrefs.DeleteKey(pair.Key);else PlayerPrefs.SetString(pair.Key,pair.Value);}
    PlayerPrefs.SetInt("Nocturnis.QA.ActiveProfile",oldActive);PlayerPrefs.SetInt("Nocturnis.QA.ProfileMigrationDone",oldMarker);PlayerPrefs.Save();Time.timeScale=0;
   }
  }
  static void Finish(bool pass,string message)
  {SessionState.SetBool(Session,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Directory.CreateDirectory("Logs");File.WriteAllText("Logs/profiles-qa-result.txt",(pass ? "PASS: " : "FAIL: ")+checks+" assertions. "+message);EditorApplication.Exit(pass ? 0 : 1);}
 }
}
