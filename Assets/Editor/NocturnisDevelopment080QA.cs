using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Krypta.Editor
{
 public static class NocturnisDevelopment080QA
 {
  const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
  const string Session="Nocturnis.Development080";
  static int stage,checks;static double start;static string error="";
  static List<string> evidence=new List<string>();
  [Serializable]class Report{public bool passed;public int assertions;public string error;public string[] evidence;}
  [InitializeOnLoadMethod]static void Restore(){if(!SessionState.GetBool(Session,false))return;KryptaGame.Testing=true;start=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
  public static void Run(){KryptaGame.Testing=true;EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool(Session,true);EditorApplication.isPlaying=true;}
  static void Log(string m,string s,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)error=m+"\n"+s;}
  static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
  static object Call(KryptaGame g,string n,params object[] args){return typeof(KryptaGame).GetMethod(n,P).Invoke(g,args);}
  static void Field(KryptaGame g,string n,object v){typeof(KryptaGame).GetField(n,P).SetValue(g,v);}
  static void Set(KryptaGame g,string n,object v){typeof(KryptaGame).GetProperty(n).SetValue(g,v);}
  static void Talk(KryptaGame g,string service){Call(g,"Talk","QA","QA",service);}
  static void Fixture(KryptaGame g,int zone)
  {
   Set(g,"State",KryptaGame.Phase.Title);Call(g,"ResetCampaign");Set(g,"Level",8);Set(g,"Unlocked",(1<<Campaign.Town)|(1<<Campaign.Crypt)|(1<<Campaign.Road));Call(g,"LoadZone",zone);
   Set(g,"State",KryptaGame.Phase.Playing);Set(g,"Health",g.MaxHealth);Set(g,"Coins",1000);Field(g,"introUntil",0f);g.Status.Clear();Time.timeScale=0;
  }
  static void Tick()
  {
   if(EditorApplication.timeSinceStartup-start>300){Finish(false,"Timeout");return;}
   if(!EditorApplication.isPlaying)return;var g=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!g||!g.View)return;
   try
   {
    if(error.Length>0)throw new Exception(error);KryptaGame.Testing=true;g.enabled=false;g.GetComponent<KryptaHUD>().enabled=false;
    if(stage==0){evidence.Add(NocturnisTalentQA.RunChecks());Krypta.EditorTools.NocturnisSettingsQA.Run();evidence.Add("Real quality, frame pacing, preference persistence and display guards.");}
    else if(stage==1){NocturnisQuestJournalQA.RunChecks(g,Check);evidence.Add("Quest acceptance, three pins, saved journal, actual rewards once, main finale migration.");}
    else if(stage==2)Market(g);
    else if(stage==3)Recall(g);
    else if(stage==4)Migration(g);
    else {Finish(true,"");return;}
    stage++;
   }catch(Exception e){Finish(false,e.ToString());}
  }
  static void Market(KryptaGame g)
  {
   Fixture(g,Campaign.Town);Talk(g,"market");var first=g.MarketStock[0];int price=g.MarketPrice(first),gold=g.Coins;
   Check(g.BuyMarketItem(0) && g.Coins==gold-price && g.Inventory.Count==1,"Purchase is atomic");
   Check(!g.BuyMarketItem(0) && g.Inventory.Count==1,"Same stock cannot be purchased twice");
   Check(g.ToggleMarketLock(0) && !g.SellMarketItem(0),"Protected item cannot sell");
   Talk(g,"forge");Check(!g.Salvage(0),"Protected item cannot be dismantled");
   Talk(g,"market");g.ToggleMarketLock(0);int sell=g.MarketPrice(g.Inventory[0],true);gold=g.Coins;
   Check(g.SellMarketItem(0) && g.Coins==gold+sell && g.Inventory.Count==0,"Selling pays exact gold once");
   Check(!g.SellMarketItem(0),"Sold slot cannot pay twice");
   Check(g.BuyMarketItem(0,true) && g.Coins==gold && g.Inventory.Count==1,"Buyback returns same item at original sale price");
   Check(!g.BuyMarketItem(0,true),"Buyback cannot duplicate");
   g.Resume();Call(g,"SaveCheckpoint");Check(g.ContinueCheckpoint(),"Market checkpoint reloads");Talk(g,"market");
   Check(g.MarketStock[0]==null && g.Inventory.Count==1,"Reload cannot replenish purchased stock");
   int scrolls=g.RecallScrolls;gold=g.Coins;int scrollPrice=g.RecallScrollPrice;
   Check(g.BuyRecallScroll() && g.RecallScrolls==scrolls+1 && g.Coins==gold-scrollPrice,"Scroll purchase consumes exact currency");
   Set(g,"Coins",0);Check(!g.BuyMarketItem(1) && !g.BuyRecallScroll(),"Insufficient funds rejects transactions");
   Set(g,"Coins",10000);while(g.Inventory.Count<KryptaGame.InventoryCapacity)g.Inventory.Add(new KryptaGame.Relic{slot=0,rank=1});
   Check(!g.BuyMarketItem(1) && g.MarketStock[1]!=null,"Full inventory leaves stock and currency unchanged");
   evidence.Add("Shop buy/sell/buyback, item locks, full inventory, no reload reroll, scroll economy.");
  }
  static void Recall(KryptaGame g)
  {
   Fixture(g,Campaign.Crypt);int scrolls=g.RecallScrolls;Vector3 origin=g.HeroPosition;
   Check(g.StartRecall() && g.RecallActive,"Scroll starts channel");
   Field(g,"dashEnd",-10f);Field(g,"hurtUntil",-10f);Check((bool)Call(g,"Hurt",1f),"Fixture damage lands");
   Check(!g.RecallActive && g.RecallScrolls==scrolls,"Damage interrupts without consuming scroll");
   Check(g.StartRecall(),"Channel restarts");Field(g,"recallStart",Time.time-1.6f);Check((bool)Call(g,"TickRecall"),"Completed channel transitions");
   Check(g.Zone==Campaign.Town && g.RecallScrolls==scrolls-1 && g.HasRecallTrace,"One scroll consumed and return trace recorded");
   Call(g,"CompleteRecall");Check(g.RecallScrolls==scrolls-1,"Channel completion cannot consume twice");
   Call(g,"SaveCheckpoint");Check(g.ContinueCheckpoint() && g.HasRecallTrace,"Trace and scroll count persist");
   Talk(g,"waypoint");Check(g.ReturnAlongRecallTrace() && g.Zone==Campaign.Crypt && !g.HasRecallTrace,"Trace returns once");
   Check(Vector3.Distance(g.HeroPosition,origin)<.2f,"Trace restores original safe location");
   Talk(g,"waypoint");Check(!g.ReturnAlongRecallTrace(),"Consumed trace cannot replay");
   g.Resume();g.RestoreRecall(new KryptaGame.RecallSave{count=0});
   Check(g.StartRecall(),"Without scroll prayer starts");Field(g,"recallStart",Time.time-1.6f);Check(!(bool)Call(g,"TickRecall") && g.Zone==Campaign.Crypt,"Prayer cannot complete at scroll timing");
   Field(g,"recallStart",Time.time-8.1f);Check((bool)Call(g,"TickRecall") && g.Zone==Campaign.Town && !g.HasRecallTrace,"Prayer returns without a trace");
   evidence.Add("Recall damage cancellation, correct timing, one consumption, durable single-use trace and no-scroll fallback.");
  }
  sealed class StoredPreference
  {public bool exists;public string value;}
  static string RawLegacyCheckpoint(Type type,object saved,int version,string note)
  {
   type.GetField("version").SetValue(saved,version);
   string json=JsonUtility.ToJson(saved,true);
   // Unknown data, indentation and non-ASCII text must survive byte-for-byte.
   return json.Substring(0,json.LastIndexOf('}'))+",\n  \"legacyAuditSentinel\" : \"\u017b\u00f3\u0142\u0107 / "+note+"\"\n}\n";
  }
  static bool SameRawBytes(string expected,string actual)
  {
   return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(expected))==
    Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(actual));
  }
  static void Migration(KryptaGame g)
  {
   Check(KryptaGame.Testing,"Migration fixtures must use the QA namespace");
   string active="Nocturnis.QA.ActiveProfile";bool hadActive=PlayerPrefs.HasKey(active);int oldActive=PlayerPrefs.GetInt(active,1);
   var prior=new Dictionary<string,StoredPreference>();
   foreach(string key in new[]{"Nocturnis.QA.Checkpoint","Nocturnis.QA.Profile2.Checkpoint","Nocturnis.QA.Profile3.Checkpoint"})
   {
    foreach(string suffix in new[]{"",".Backup.v1",".Backup.v2",".Backup.v3",".Backup.v4"})
    {string k=key+suffix;prior[k]=new StoredPreference{exists=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetString(k,"")};}
   }
   try
   {
    PlayerPrefs.SetInt(active,1);
    Fixture(g,Campaign.Town);g.OpenTalents();Check(g.SpendTalent(0) && g.SpendTalent(0),"Talent fixture learns two ranks");
    float damage=g.TalentDamageMultiplier(null);Check(damage>1,"Learned damage bonus active");
    g.Resume();Call(g,"SaveCheckpoint");Check(g.ContinueCheckpoint() && g.TalentRank(0)==2 && g.TalentPoints==5,"Talent checkpoint roundtrip");
    string key="Nocturnis.QA.Checkpoint";
    var type=typeof(KryptaGame).GetNestedType("Checkpoint",BindingFlags.NonPublic);
    var saved=JsonUtility.FromJson(PlayerPrefs.GetString(key),type);
    foreach(string name in new[]{"talents","recall","market","trackedQuestIds"})type.GetField(name).SetValue(saved,null);
    string raw=RawLegacyCheckpoint(type,saved,4,"original profile one");
    PlayerPrefs.DeleteKey(key+".Backup.v4");PlayerPrefs.SetString(key,raw);PlayerPrefs.Save();
    Check(g.ContinueCheckpoint() && g.Level==8 && g.TalentPoints==7 && g.RecallScrolls==2,"Version4 gains unspent earned talents and two starter scrolls");
    Check(PlayerPrefs.GetString(key).Contains("\"version\":5"),"Legacy save upgrades after successful load");
    Check(SameRawBytes(raw,PlayerPrefs.GetString(key+".Backup.v4")),"v4 backup preserves original raw UTF8 bytes including whitespace and unknown fields");
    Check(g.ContinueCheckpoint() && g.RecallScrolls==2,"Repeated load cannot duplicate starting scrolls");
    Set(g,"Coins",g.Coins+17);Call(g,"SaveCheckpoint");
    Check(SameRawBytes(raw,PlayerPrefs.GetString(key+".Backup.v4")),"Repeated v5 save leaves original v4 backup unchanged");
    PlayerPrefs.SetString(key,RawLegacyCheckpoint(type,saved,4,"different later legacy save"));Call(g,"SaveCheckpoint");
    Check(SameRawBytes(raw,PlayerPrefs.GetString(key+".Backup.v4")),"Previously captured backup is never overwritten even by another v4 payload");

    // Exercise each supported older schema through the real save entry point.
    PlayerPrefs.SetInt(active,2);string second="Nocturnis.QA.Profile2.Checkpoint";
    for(int version=1;version<=4;version++)
    {
     string original=RawLegacyCheckpoint(type,saved,version,"profile two version "+version),backup=second+".Backup.v"+version;
     PlayerPrefs.DeleteKey(backup);PlayerPrefs.SetString(second,original);PlayerPrefs.Save();
     Check((bool)Call(g,"SaveCheckpoint"),"Legacy v"+version+" writes via guarded SaveCheckpoint");
     Check(SameRawBytes(original,PlayerPrefs.GetString(backup)),"Legacy v"+version+" has independent exact backup");
     Check(PlayerPrefs.GetString(second).Contains("\"version\":5"),"Legacy v"+version+" live key becomes v5");
    }
    Check(SameRawBytes(raw,PlayerPrefs.GetString(key+".Backup.v4")),"Writing profile two cannot alter profile one's backup");
    Check(PlayerPrefs.GetString(second+".Backup.v4")!=PlayerPrefs.GetString(key+".Backup.v4"),"Profiles retain different rollback payloads");

    // The death path formerly bypassed SaveCheckpoint. A forward-compatible
    // legacy payload containing recall exercises that bypass directly.
    PlayerPrefs.SetInt(active,3);string third="Nocturnis.QA.Profile3.Checkpoint";
    type.GetField("recall").SetValue(saved,new KryptaGame.RecallSave{count=3,zone=Campaign.Crypt,x=5,z=7});
    string deathRaw=RawLegacyCheckpoint(type,saved,4,"death path");
    PlayerPrefs.DeleteKey(third+".Backup.v4");PlayerPrefs.SetString(third,deathRaw);PlayerPrefs.Save();
    Set(g,"State",KryptaGame.Phase.Dead);Call(g,"OnRecallHeroDied");
    Check(SameRawBytes(deathRaw,PlayerPrefs.GetString(third+".Backup.v4")),"Death-path write also preserves its original legacy checkpoint");
    var afterDeath=JsonUtility.FromJson(PlayerPrefs.GetString(third),type);
    Check(((KryptaGame.RecallSave)type.GetField("recall").GetValue(afterDeath)).zone==-1,"Death-path mutation still clears the saved return trace");
    Check(SameRawBytes(raw,PlayerPrefs.GetString(key+".Backup.v4")),"Death-path write remains isolated from other save slots");
    evidence.Add("Version4 migration backfills earned talents once; exact raw v1-v4 backups survive repeated writes, are isolated per slot, and cover the recall death writer.");
   }
   finally
   {
    foreach(var pair in prior)
     if(pair.Value.exists)PlayerPrefs.SetString(pair.Key,pair.Value.value);else PlayerPrefs.DeleteKey(pair.Key);
    if(hadActive)PlayerPrefs.SetInt(active,oldActive);else PlayerPrefs.DeleteKey(active);
    PlayerPrefs.Save();Time.timeScale=0;
   }
  }
  static void Finish(bool passed,string message)
  {
   SessionState.SetBool(Session,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Directory.CreateDirectory("Logs");
   File.WriteAllText("Logs/development080-qa.json",JsonUtility.ToJson(new Report{passed=passed,assertions=checks,error=message,evidence=evidence.ToArray()},true));
   if(!passed)Debug.LogError(message);Time.timeScale=1;EditorApplication.Exit(passed?0:1);
  }
 }
}
