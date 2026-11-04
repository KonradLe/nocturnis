using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Krypta.Editor {
 public static class NocturnisCampaignAuditQA {
  const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  const string Session="Nocturnis.CampaignAuditQA",Key="Nocturnis.QA.Checkpoint";
  static double started;static int checks;static string errors="";
  static readonly List<string> evidence=new List<string>();
  [Serializable]sealed class Report {public bool passed;public int assertions;public string error;public string[] evidence;}
  [Serializable]sealed class Saved {public string[] usedShrines;}
  static object Call(KryptaGame g,string n,params object[] a){return typeof(KryptaGame).GetMethod(n,Private).Invoke(g,a);}
  static object Field(object g,string n){return g.GetType().GetField(n,BindingFlags.Public|Private).GetValue(g);}
  static void Set(KryptaGame g,string n,object v){typeof(KryptaGame).GetProperty(n).SetValue(g,v);}
  static bool Near(float a,float b){return Mathf.Abs(a-b)<.001f;}
  static void Check(bool ok,string note){checks++;if(!ok)throw new Exception(note);}
  static void Load(KryptaGame g,int zone){g.Resume();Call(g,"LoadZone",zone);Time.timeScale=0;}
  static void Fresh(KryptaGame g){Set(g,"State",KryptaGame.Phase.Title);g.SelectHero(0);g.Begin();Time.timeScale=0;}
  static bool Reload(KryptaGame g){bool ok=g.ContinueCheckpoint();Time.timeScale=0;return ok;}
  static object Shrine(KryptaGame g,string id){
   foreach(object s in (IList)Field(g,"shrines"))if((string)Field(s,"id")==id)return s;
   throw new Exception("Missing shrine "+id);
  }
  static void Visit(KryptaGame g,object s){
   ((ActorVisual)Field(g,"player")).Root.transform.position=(Vector3)Field(s,"pos");g.Interact();
  }
  static string[] Used(KryptaGame g){return (string[])Call(g,"CaptureShrineUsage");}
  [InitializeOnLoadMethod]static void Restore(){
   if(!SessionState.GetBool(Session,false))return;started=EditorApplication.timeSinceStartup;
   EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
  }
  public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool(Session,true);EditorApplication.isPlaying=true;}
  static void Log(string m,string s,LogType t){if(t==LogType.Exception || t==LogType.Error || t==LogType.Assert)errors=m+"\n"+s;}
  static void Tick(){
   if(EditorApplication.timeSinceStartup-started>180){Finish(false,"Timeout");return;}
   if(!EditorApplication.isPlaying)return;var g=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!g || !g.View)return;
   EditorApplication.update-=Tick;
   try{
    KryptaGame.Testing=true;g.enabled=false;var hud=g.GetComponent<KryptaHUD>();if(hud)hud.enabled=false;
    RunChecks(g);Check(errors.Length==0,"No runtime errors");Finish(true,"");
   }catch(Exception e){Finish(false,e.ToString());}
  }
  static void RunChecks(KryptaGame g){
   string[] production={"Nocturnis.Checkpoint.v1","Nocturnis.Checkpoint.v2","Nocturnis.Profile1.Checkpoint","Nocturnis.Profile2.Checkpoint","Nocturnis.Profile3.Checkpoint","Nocturnis.SharedStash.v1"};
   var snapshots=new Dictionary<string,string>();foreach(var k in production)snapshots[k]=PlayerPrefs.GetString(k,"");
   string[] qa={Key,"Nocturnis.QA.Profile2.Checkpoint","Nocturnis.QA.Profile3.Checkpoint"};
   var oldQa=new Dictionary<string,string>();foreach(var k in qa)oldQa[k]=PlayerPrefs.HasKey(k)?PlayerPrefs.GetString(k):null;
   int profile=g.ActiveProfile;
   var bindings=new Dictionary<string,int>();foreach(var a in NocturnisInput.Actions)bindings[a]=PlayerPrefs.GetInt("Nocturnis.QA.Key."+a,-1);
   Set(g,"State",KryptaGame.Phase.Title);g.SelectProfile(1);
   try{
    Fresh(g);Set(g,"Health",27.5f);Load(g,Campaign.Fields);
    Check(Near(g.Health,57.5f),"First discovery grants exactly thirty HP");
    Set(g,"Health",24.25f);Load(g,Campaign.Fields);Check(Near(g.Health,24.25f),"Repeated same-map entry grants no HP");
    Load(g,Campaign.Forest);Check(Near(g.Health,54.25f),"Another first discovery grants its own bonus");
    Set(g,"Health",19.5f);Load(g,Campaign.Fields);Check(Near(g.Health,19.5f),"Returning from a branch grants no HP");
    Load(g,Campaign.Town);Check(Near(g.Health,g.MaxHealth),"Oakhaven retains intentional safe-town healing");
    Set(g,"Health",18.75f);Load(g,Campaign.Fields);Check(Near(g.Health,18.75f),"Known-zone waypoint entry grants no bonus");
    Set(g,"Resource",7.5f);Call(g,"SaveCheckpoint");Check(Reload(g)&&Near(g.Health,18.75f)&&Near(g.Resource,7.5f),"Checkpoint reload preserves exact vitals");
    evidence.Add("First discovery, repeated entries, branch return, safe-town healing and saved fractional HP/resource.");
    Set(g,"Health",12f);Set(g,"Potions",1);var shrine=Shrine(g,"renewal");string key=(string)Field(shrine,"key");Visit(g,shrine);
    Check((bool)Field(shrine,"used")&&Used(g).Length==1,"Actual E interaction records one stable shrine key");
    Check(Near(g.Health,g.MaxHealth)&&g.Potions==2,"Renewal gives its first heal and one potion");
    Check(Array.IndexOf(JsonUtility.FromJson<Saved>(PlayerPrefs.GetString(Key)).usedShrines,key)>=0,"Activation is saved immediately");
    Set(g,"Health",23.5f);Visit(g,shrine);Check(Near(g.Health,23.5f)&&g.Potions==2,"Repeated E gives no duplicate reward");
    Call(g,"SaveCheckpoint");Check(Reload(g)&&Near(g.Health,23.5f)&&g.Potions==2,"Consumed shrine and fractional HP survive reload");
    shrine=Shrine(g,"renewal");Check((bool)Field(shrine,"used")&&!((GameObject)Field(shrine,"ring")).activeSelf,"Reloaded shrine has no active glow");
    Visit(g,shrine);Check(Near(g.Health,23.5f)&&g.Potions==2,"Reload cannot refill via consumed shrine");
    Load(g,Campaign.Cellar);Set(g,"Health",31f);Load(g,Campaign.Fields);
    shrine=Shrine(g,"renewal");Check((string)Field(shrine,"key")==key&&(bool)Field(shrine,"used"),"Revisit uses the identical stable shrine key");
    Visit(g,shrine);Check(Near(g.Health,31f)&&g.Potions==2,"Leaving and returning cannot reset the shrine");
    foreach(var id in new[]{"slaughter","grace"}){shrine=Shrine(g,id);Visit(g,shrine);Check((bool)Field(shrine,"used"),"Buff shrine consumed "+id);}
    Check(Used(g).Length==3&&Reload(g)&&Used(g).Length==3,"All three shrine types persist together");
    foreach(object s in (IList)Field(g,"shrines"))Check((bool)Field(s,"used"),"All consumed props rebuild as used");
    Load(g,Campaign.Forest);Check(!(bool)Field(Shrine(g,"renewal"),"used"),"Same shrine type in another zone remains independent");
    shrine=Shrine(g,"renewal");Visit(g,shrine);Check(Used(g).Length==4,"Second zone contributes a distinct usage key");
    Call(g,"SaveCheckpoint");string profileOne=PlayerPrefs.GetString(Key);
    Set(g,"State",KryptaGame.Phase.Title);Check(g.SelectProfile(2),"Select isolated second profile");Fresh(g);Load(g,Campaign.Fields);
    Check(Used(g).Length==0&&!(bool)Field(Shrine(g,"renewal"),"used"),"New profile does not inherit another hero's shrines");
    Set(g,"State",KryptaGame.Phase.Title);g.SelectProfile(1);Check(Reload(g)&&Used(g).Length==4,"Original profile retains its four used shrines");
    Check(PlayerPrefs.GetString(Key)==profileOne,"Other profile cannot replace the first checkpoint");
    Fresh(g);Load(g,Campaign.Fields);Check(Used(g).Length==0&&!(bool)Field(Shrine(g,"renewal"),"used"),"New expedition clears shrine use in its selected profile");
    PlayerPrefs.SetString(Key,"{\"version\":4,\"hero\":0,\"zone\":4,\"level\":1,\"health\":22.25,\"resource\":17,\"shadow\":40,\"potions\":1}");
    Check(Reload(g)&&Near(g.Health,22.25f)&&Used(g).Length==0,"Pre-fix v4 save without shrine field remains valid");
    Visit(g,Shrine(g,"renewal"));Check(g.Potions==2&&Reload(g)&&g.Potions==2&&Used(g).Length==1,"Legacy journey adopts persistent activation after first use");
    var valid=Used(g)[0];Call(g,"RestoreShrineUsage",new object[]{new[]{valid,valid,null,"","-1:renewal:21:30","4:unknown:21:30","4:renewal:999:30","broken"}});
    Check(Used(g).Length==1&&Used(g)[0]==valid,"Malformed and duplicate persisted shrine keys are rejected");
    evidence.Add("E interactions for heal/buff shrines, immediate save, exact reload, revisit, independent zones/profiles, new-expedition reset and missing-field migration.");
    NocturnisInput.Reset();Check(!NocturnisInput.Bind("inventory",KeyCode.LeftShift),"Left Shift cannot override stationary attack");
    Check(NocturnisInput.Key("inventory")==KeyCode.I,"Rejected binding leaves the previous action intact");
    Check(NocturnisInput.Bind("inventory",KeyCode.E)&&NocturnisInput.Key("interact")==KeyCode.I,"Normal occupied-key swap still works");
    PlayerPrefs.SetInt("Nocturnis.QA.Key.inventory",(int)KeyCode.LeftShift);NocturnisInput.ReloadBindings();
    var keys=new HashSet<KeyCode>();foreach(string a in NocturnisInput.Actions){KeyCode k=NocturnisInput.Key(a);Check(k!=KeyCode.LeftShift&&keys.Add(k),"Legacy reserved-key repair remains distinct: "+a);}
    Check(PlayerPrefs.GetInt("Nocturnis.QA.Key.inventory")!=(int)KeyCode.LeftShift,"Repaired legacy assignment persists");
    evidence.Add("Reserved Shift rejection, existing swap behavior and collision-free migration of legacy saved Shift binding.");
    foreach(var k in production)Check(PlayerPrefs.GetString(k,"")==snapshots[k],"Production data unchanged: "+k);
   }finally{
    foreach(var k in qa)if(oldQa[k]==null)PlayerPrefs.DeleteKey(k);else PlayerPrefs.SetString(k,oldQa[k]);
    foreach(var kv in bindings)if(kv.Value<0)PlayerPrefs.DeleteKey("Nocturnis.QA.Key."+kv.Key);else PlayerPrefs.SetInt("Nocturnis.QA.Key."+kv.Key,kv.Value);
    PlayerPrefs.SetInt("Nocturnis.QA.ActiveProfile",profile);PlayerPrefs.Save();NocturnisInput.ReloadBindings();Time.timeScale=0;
   }
  }
  static void Finish(bool ok,string error){
   SessionState.SetBool(Session,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Directory.CreateDirectory("Logs");
   File.WriteAllText("Logs/campaign-audit-qa.json",JsonUtility.ToJson(new Report{passed=ok,assertions=checks,error=error,evidence=evidence.ToArray()},true));
   EditorApplication.Exit(ok?0:1);
  }
 }
}
