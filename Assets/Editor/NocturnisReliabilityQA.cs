using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Krypta.Editor
{
 public static class NocturnisReliabilityQA
 {
  const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  const string Session="Nocturnis.ReliabilityQA",Key="Nocturnis.QA.Checkpoint";
  static double started;static int checks;static string error="";
  static readonly List<string> evidence=new List<string>();
  [Serializable]sealed class Report {public bool passed;public int assertions;public string error;public string[] evidence;}
  [Serializable]sealed class Saved
  {
   public int version,hero,zone,level,coins,potions,defeated,weapon;
   public float health,resource,shadow;
   public int[] slain,gearEnchants;
   public List<KryptaGame.Relic> inventory;
  }
  static object Call(KryptaGame game,string name,params object[] args){return typeof(KryptaGame).GetMethod(name,Private).Invoke(game,args);}
  static object Field(KryptaGame game,string name){return typeof(KryptaGame).GetField(name,Private).GetValue(game);}
  static IList List(KryptaGame game,string name){return (IList)Field(game,name);}
  static void Set(KryptaGame game,string name,object value){typeof(KryptaGame).GetProperty(name).SetValue(game,value);}
  static Saved Snapshot(){return JsonUtility.FromJson<Saved>(PlayerPrefs.GetString(Key,""));}
  static bool Save(KryptaGame game){return (bool)Call(game,"SaveCheckpoint");}
  static void Check(bool pass,string message){checks++;if(!pass)throw new Exception(message);}
  static bool Near(float a,float b){return Mathf.Abs(a-b)<.001f;}
  static void Fresh(KryptaGame game)
  {Set(game,"State",KryptaGame.Phase.Title);game.SelectHero(0);game.Begin();Time.timeScale=0;}
  static bool Reload(KryptaGame game){bool loaded=game.ContinueCheckpoint();Time.timeScale=0;return loaded;}
  static string Inventory(KryptaGame game)
  {var items=new List<string>();foreach(var item in game.Inventory)items.Add(item.slot+":"+item.rank+":"+item.enchant+":"+item.identified);return string.Join("|",items);}
  [InitializeOnLoadMethod]static void Restore()
  {if(!SessionState.GetBool(Session,false))return;started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
  public static void Run()
  {EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool(Session,true);EditorApplication.isPlaying=true;}
  static void Log(string message,string stack,LogType type)
  {if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)error=message+"\n"+stack;}
  static void Tick()
  {
   if(EditorApplication.timeSinceStartup-started>180){Finish(false,"Timeout");return;}
   if(!EditorApplication.isPlaying)return;
   var game=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!game || !game.View)return;
   try
   {
    KryptaGame.Testing=true;game.enabled=false;var hud=game.GetComponent<KryptaHUD>();if(hud)hud.enabled=false;Time.timeScale=0;
    RunChecks(game,Check);Check(error.Length==0,"No Unity runtime errors");Finish(true,"");
   }
   catch(Exception ex){Finish(false,ex.ToString());}
  }
  public static void RunChecks(KryptaGame game,Action<bool,string> check)
  {
   KryptaGame.Testing=true;
   string production=PlayerPrefs.GetString("Nocturnis.Checkpoint.v2",""),legacy=PlayerPrefs.GetString("Nocturnis.Checkpoint.v1","");
   bool hadQa=PlayerPrefs.HasKey(Key);string oldQa=PlayerPrefs.GetString(Key,"");int oldProfile=game.ActiveProfile;
   Set(game,"State",KryptaGame.Phase.Title);game.SelectProfile(1);
   try
   {
    FullBackpack(game,check);Vitals(game,check);LegacyMigration(game,check);ExitState(game,check);
    check(PlayerPrefs.GetString("Nocturnis.Checkpoint.v2","")==production && PlayerPrefs.GetString("Nocturnis.Checkpoint.v1","")==legacy,"Every reliability test leaves production checkpoints unchanged");
   }
   finally
   {
    if(hadQa)PlayerPrefs.SetString(Key,oldQa);else PlayerPrefs.DeleteKey(Key);
    PlayerPrefs.SetInt("Nocturnis.QA.ActiveProfile",oldProfile);
    PlayerPrefs.Save();Time.timeScale=0;
   }
  }
  static void FullBackpack(KryptaGame game,Action<bool,string> check)
  {
   Fresh(game);Call(game,"SetGear",0,1);((int[])Field(game,"gearEnchants"))[0]=3;
   for(int i=0;i<KryptaGame.InventoryCapacity;i++)game.Inventory.Add(new KryptaGame.Relic{slot=i%3,rank=1+i%3,enchant=i%4,identified=true});
   string original=Inventory(game);int materials=game.Materials;
   Call(game,"CreateRelicDrop",game.HeroPosition,2,0);Call(game,"UpdateRelics");
   check(game.WeaponRank==1 && game.GearEnchant(0)==3,"Full backpack cannot replace or erase the equipped enchanted weapon");
   check(Inventory(game)==original && game.Inventory.Count==KryptaGame.InventoryCapacity,"Full backpack retains every original item and enchant");
   check(List(game,"relics").Count==1 && game.Materials==materials,"Unclaimed upgrade stays on ground without automatic salvage");
   game.ShowNotice("OTHER EVENT");Call(game,"UpdateRelics");
   check(game.Notice=="OTHER EVENT","A blocked pickup does not overwrite later notices every frame");
   check(Save(game) && Reload(game),"Blocked pickup checkpoint saves and loads");
   check(List(game,"relics").Count==1 && Inventory(game)==original && game.WeaponRank==1 && game.GearEnchant(0)==3,"Reload preserves ground upgrade, full inventory and old enchant together");
   Set(game,"Service","forge");check(game.Salvage(0),"Player intentionally frees one backpack slot at the forge");Set(game,"Service","");
   Call(game,"UpdateRelics");
   check(game.WeaponRank==2 && game.GearEnchant(0)==0 && List(game,"relics").Count==0,"Upgrade is collected exactly once after freeing space");
   check(game.Inventory.Count==KryptaGame.InventoryCapacity && game.Inventory.Exists(r=>r.slot==0 && r.rank==1 && r.enchant==3),"Old weapon and its enchant are returned to the newly freed slot");
   int oldIndex=game.Inventory.FindIndex(r=>r.slot==0 && r.rank==1 && r.enchant==3);game.OpenEquipment();
   check(game.EquipItem(oldIndex) && game.WeaponRank==1 && game.GearEnchant(0)==3,"Manual swapping at capacity restores the old enchanted weapon");
   check(game.Inventory.Count==KryptaGame.InventoryCapacity && game.Inventory.Exists(r=>r.slot==0 && r.rank==2 && r.enchant==0),"Manual full-bag swap also preserves the replaced upgrade");
   game.Resume();Time.timeScale=0;Call(game,"CreateRelicDrop",game.HeroPosition,5,2);Call(game,"UpdateRelics");
   check(List(game,"relics").Count==1 && game.Materials==materials+3,"Unidentified high-rank drop is not destroyed by a full backpack");
   check(Save(game) && Reload(game) && List(game,"relics").Count==1,"Unidentified blocked drop survives save/load without duplication");
   evidence.Add("Full bag: exact item/enchant preservation, upgrade remains in world, no repeated notice, save/reload, deliberate free slot, one pickup, manual full-bag swap and unidentified relic retention.");
  }
  static void Vitals(KryptaGame game,Action<bool,string> check)
  {
   Fresh(game);Call(game,"SetGear",0,2);((int[])Field(game,"gearEnchants"))[0]=2;
   Set(game,"Health",37.5f);Set(game,"Resource",0f);Set(game,"Shadow",9.25f);game.Pause();
   check(Save(game),"Paused living journey saves successfully");
   var saved=Snapshot();check(saved.version==4 && Near(saved.health,37.5f) && Near(saved.resource,0) && Near(saved.shadow,9.25f),"Checkpoint v4 serializes exact current health and both resources");
   for(int i=0;i<3;i++)
   {
    Set(game,"Health",1f);Set(game,"Resource",99f);Set(game,"Shadow",99f);
    check(Reload(game),"Vitals checkpoint reload "+i);
    check(Near(game.Health,37.5f) && Near(game.Resource,0) && Near(game.Shadow,9.25f),"Reload neither heals nor refills resources "+i);
    check(game.WeaponRank==2 && game.GearEnchant(0)==2 && game.Health<game.MaxHealth,"Gear and maximum-health enchant restore before current health "+i);
   }
   string snapshot=PlayerPrefs.GetString(Key,"");Set(game,"Health",float.NaN);
   check(!Save(game) && PlayerPrefs.GetString(Key,"")==snapshot,"Non-finite health cannot overwrite a valid checkpoint");
   Set(game,"Health",37.5f);Set(game,"Resource",float.PositiveInfinity);
   check(!Save(game) && PlayerPrefs.GetString(Key,"")==snapshot,"Non-finite resource cannot overwrite a valid checkpoint");
   Set(game,"Resource",0f);
   PlayerPrefs.SetString(Key,"{\"version\":4,\"hero\":0,\"zone\":3,\"level\":1,\"health\":0,\"resource\":10,\"shadow\":10}");
   check(!KryptaGame.HasCheckpoint && !game.ContinueCheckpoint(),"Malformed v4 zero-health save is rejected rather than silently resurrected");
   PlayerPrefs.SetString(Key,snapshot);
   evidence.Add("Checkpoint v4: fractional HP and depleted resource round-trip on three reloads; load order accounts for gear; invalid values and dead saves cannot replace a valid checkpoint.");
  }
  static void LegacyMigration(KryptaGame game,Action<bool,string> check)
  {
   // This genuine v3-shaped record deliberately has no health/resource/shadow.
   PlayerPrefs.SetString(Key,"{\"version\":3,\"hero\":2,\"zone\":3,\"level\":3,\"coins\":47,\"xp\":10,\"potions\":2,\"defeated\":1,\"weapon\":1,\"armor\":1,\"charm\":0,\"unlocked\":15,\"cleared\":0,\"slain\":[0,0,0,1],\"gearEnchants\":[3,0,0],\"inventory\":[{\"slot\":1,\"rank\":2,\"enchant\":2,\"identified\":true}]}");
   check(Reload(game),"Existing version 3 checkpoint loads without new vitals fields");
   check(game.SelectedHero==2 && game.Level==3 && game.Coins==47 && game.Potions==2 && game.Defeated==1,"Migration preserves campaign, class and consumables");
   check(Near(game.Health,game.MaxHealth) && Near(game.Resource,50) && Near(game.Shadow,60),"Legacy-only fallback uses the historical initial vitals once");
   check(game.GearEnchant(0)==3 && game.Inventory.Count==1 && game.Inventory[0].enchant==2 && game.enemies.Find(e=>e.spawnId==0).hp==0,"Migration preserves enchanted inventory and the exact recorded kill");
   check(Snapshot().version==4 && Near(Snapshot().health,game.Health),"Legacy checkpoint is immediately rewritten as v4");
   Set(game,"Health",23.75f);Set(game,"Resource",7f);Set(game,"Shadow",2f);check(Save(game) && Reload(game),"Migrated journey can be saved and reloaded normally");
   check(Near(game.Health,23.75f) && Near(game.Resource,7) && Near(game.Shadow,2),"Legacy migration does not grant another refill after the next save");
   evidence.Add("Real v3 JSON with absent vitals migrates once to v4 while retaining kill masks, class, potions, inventory and enchants; subsequent reload retains reduced vitals.");
  }
  static void ExitState(KryptaGame game,Action<bool,string> check)
  {
   Fresh(game);var enemy=game.enemies.Find(e=>!e.boss && !e.elite && e.hp>0);int spawn=enemy.spawnId;
   Call(game,"Damage",enemy,1000000f);Set(game,"Health",20f);
   check(game.UsePotion(),"Exit fixture consumes a real potion after a real enemy defeat");
   Set(game,"Resource",17f);Set(game,"Shadow",8f);float health=game.Health;int defeated=game.Defeated,potions=game.Potions;
   string old=PlayerPrefs.GetString(Key,"");
   check(!game.SaveBeforeQuit() && PlayerPrefs.GetString(Key,"")==old,"Public quit hook ignores automated QA fixtures");
   // Exercise the exact serializer/state guard called by SaveBeforeQuit, with
   // the isolated QA key. Never disable Testing just to exercise native quit.
   foreach(var phase in new[]{KryptaGame.Phase.Playing,KryptaGame.Phase.Paused,KryptaGame.Phase.Dialogue,KryptaGame.Phase.Won})
   {
    Set(game,"State",phase);check(Save(game),"Quit serializer accepts a living journey in "+phase);
    var snapshot=Snapshot();
    check(snapshot.defeated==defeated && snapshot.potions==potions && (snapshot.slain[game.Zone]&(1<<spawn))!=0,"Quit snapshot includes latest kill and consumed potion in "+phase);
    check(Near(snapshot.health,health) && Near(snapshot.resource,17) && Near(snapshot.shadow,8),"Quit snapshot includes current vitals in "+phase);
   }
   string living=PlayerPrefs.GetString(Key,"");Set(game,"State",KryptaGame.Phase.Title);game.SelectHero(4);
   check(!Save(game) && PlayerPrefs.GetString(Key,"")==living,"Title character preview cannot overwrite the living journey");
   Set(game,"State",KryptaGame.Phase.Dead);Set(game,"Health",0f);
   check(!Save(game) && PlayerPrefs.GetString(Key,"")==living,"Death/final-frame save attempt retains the previous living checkpoint");
   var audio=game.GetComponent<NocturnisAudio>();
   if(audio)typeof(NocturnisAudio).GetMethod("OnApplicationQuit",Private).Invoke(audio,null);
   else check(Application.isBatchMode,"Headless batch intentionally omits native audio; player-build quit smoke covers the native callback");
   check(!game.SaveBeforeQuit() && PlayerPrefs.GetString(Key,"")==living,"Quit guard does not persist a dead QA scene");
   check(Reload(game),"Checkpoint remains resumable after title/death exit attempts");
   check(game.SelectedHero==0 && game.Defeated==defeated && game.Potions==potions && Near(game.Health,health) && Near(game.Resource,17) && Near(game.Shadow,8),"Reload restores latest living kill/potion/vitals snapshot, not the preview or death scene");
   check(game.enemies.Find(e=>e.spawnId==spawn).hp==0,"Defeated enemy stays defeated after exit snapshot reload");
   evidence.Add("Exit serialization core in Playing/Paused/Dialogue/Won captures real kill and potion use; Title/Dead and public QA/native callback guards retain prior save. Actual native-window exit remains a separate player-build smoke check.");
  }
  static void Finish(bool passed,string message)
  {
   SessionState.SetBool(Session,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Directory.CreateDirectory("Logs");
   var report=new Report{passed=passed,assertions=checks,error=message,evidence=evidence.ToArray()};
   File.WriteAllText("Logs/reliability-qa.json",JsonUtility.ToJson(report,true));
   File.WriteAllText("Logs/reliability-qa-result.txt",(passed ? "PASS: " : "FAIL: ")+checks+" assertions. "+message+"\n"+string.Join("\n",evidence));
   EditorApplication.Exit(passed ? 0 : 1);
  }
 }
}
