using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Krypta.Editor
{
 public static class NocturnisLootPersistenceQA
 {
  const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
  static double started;static int checks;static bool errors;
  static object Call(KryptaGame g,string name,params object[] args){return typeof(KryptaGame).GetMethod(name,Private).Invoke(g,args);}
  static IList List(KryptaGame g,string name){return (IList)typeof(KryptaGame).GetField(name,Private).GetValue(g);}
  static object Field(object obj,string name){return obj.GetType().GetField(name).GetValue(obj);}
  static void Put(object obj,string name,object value){obj.GetType().GetField(name).SetValue(obj,value);}
  static string Loot(KryptaGame g)
  {
   var parts=new System.Collections.Generic.List<string>();
   foreach(var p in List(g,"pickups"))parts.Add("P:"+Field(p,"value")+":"+Field(p,"grace")+":"+((Vector3)Field(p,"origin")).ToString("F3"));
   foreach(var r in List(g,"relics"))parts.Add("R:"+Field(r,"rank")+":"+Field(r,"slot")+":"+((Vector3)Field(r,"position")).ToString("F3"));
   parts.Sort(StringComparer.Ordinal);return string.Join("|",parts);
  }
  [InitializeOnLoadMethod]static void Restore()
  {if(SessionState.GetBool("Nocturnis.LootQA",false)){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}}
  public static void Run()
  {EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool("Nocturnis.LootQA",true);EditorApplication.isPlaying=true;}
  static void Log(string message,string stack,LogType type){if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert)errors=true;}
  static void Check(bool pass,string message){checks++;if(!pass)throw new Exception(message);}
  // May also be invoked from the parent integration QA in its isolated profile.
  public static void RunChecks(KryptaGame game,Action<bool,string> check)
  {
   KryptaGame.Testing=true;
   if(game.State!=KryptaGame.Phase.Title)typeof(KryptaGame).GetProperty("State").SetValue(game,KryptaGame.Phase.Title);
   game.Begin();
   var enemy=game.enemies.Find(e=>e.boss || e.elite) ?? game.enemies[0];int id=enemy.spawnId;
   Call(game,"Damage",enemy,100000f);
   check(enemy.hp==0 && enemy.rewarded,"Real enemy killed and reward locked");
   Call(game,"Drop",game.HeroPosition+Vector3.forward*3,13,false);
   Call(game,"Drop",game.HeroPosition+Vector3.forward*4,1,true);
   Call(game,"CreateRelicDrop",game.HeroPosition+Vector3.forward*5,4,2);
   Call(game,"SaveCheckpoint");string roadLoot=Loot(game);
   check(List(game,"pickups").Count>=3 && List(game,"relics").Count>=1,"Gold, grace and exact relic generated");
   check(game.ContinueCheckpoint() && Loot(game)==roadLoot,"Reload preserves pending gold, grace, relic rank/slot and positions");
   check(game.enemies.Find(e=>e.spawnId==id).hp==0,"Reward source remains defeated after reload");
   Call(game,"Drop",game.HeroPosition,999,false); // Deliberately unsaved old scene contents.
   check(game.ContinueCheckpoint() && Loot(game)==roadLoot,"Continue discards old scene loot without overwriting checkpoint");
   Call(game,"LoadZone",Campaign.Town);
   check(List(game,"pickups").Count==0 && List(game,"relics").Count==0,"Other zone does not instantiate Road loot");
   Call(game,"Drop",game.HeroPosition+Vector3.forward*5,55,false);Call(game,"SaveCheckpoint");string townLoot=Loot(game);
   Call(game,"LoadZone",Campaign.Road);
   check(Loot(game)==roadLoot,"Returning to Road restores uncollected items exactly once");
   check(game.ContinueCheckpoint() && Loot(game)==roadLoot,"Repeated load does not duplicate drops");
   int gold=0;foreach(var p in List(game,"pickups")){if(!(bool)Field(p,"grace"))gold+=(int)Field(p,"value");Put(p,"origin",game.HeroPosition);}
   foreach(var r in List(game,"relics"))Put(r,"position",game.HeroPosition);
   int before=game.Coins;Call(game,"UpdatePickups");Call(game,"UpdateRelics");
   check(game.Coins==before+gold && List(game,"pickups").Count==0 && List(game,"relics").Count==0,"Normal collection grants rewards and removes world objects");
   int inventory=game.Inventory.Count,weapon=game.WeaponRank,armor=game.ArmorRank,charm=game.CharmRank;
   check(game.ContinueCheckpoint() && game.Coins==before+gold && List(game,"pickups").Count==0 && List(game,"relics").Count==0,"Collection autosaves without respawning pickups");
   check(game.Inventory.Count==inventory && game.WeaponRank==weapon && game.ArmorRank==armor && game.CharmRank==charm,"Relic collection autosaves inventory and equipped rank");
   Call(game,"LoadZone",Campaign.Town);check(Loot(game)==townLoot,"Collecting Road loot preserves pending loot from another zone");
   typeof(KryptaGame).GetProperty("State").SetValue(game,KryptaGame.Phase.Title);game.Begin();Call(game,"LoadZone",Campaign.Town);
   check(List(game,"pickups").Count==0 && List(game,"relics").Count==0,"New journey clears prior pending loot and does not restash old scene");
   CheckRitualReload(game,check);
  }
  static void TriggerRitual(KryptaGame game)
  {
   var boss=game.enemies.Find(e=>e.boss);boss.alerted=true;
   typeof(KryptaGame).GetField("bossRitualAt",Private).SetValue(game,Time.time-1f);
   Call(game,"UpdateSpecialEnemy",boss);
  }
  static void CheckRitualReload(KryptaGame game,Action<bool,string> check)
  {
   Call(game,"LoadZone",Campaign.Heart);TriggerRitual(game);
   var first=game.enemies.Find(e=>e.spawnId==9);var second=game.enemies.Find(e=>e.spawnId==10);
   check(first!=null && second!=null && first.hp>0 && second.hp>0 && first.sacrificial && second.sacrificial,"Ritual creates two live sacrificial actors at stable IDs 9 and 10");
   Call(game,"Damage",first,100000f);Call(game,"Damage",second,100000f);Call(game,"SaveCheckpoint");
   int defeated=game.Defeated,xp=game.Experience,level=game.Level,coins=game.Coins,materials=game.Materials;
   string loot=Loot(game);int mask=((int[])typeof(KryptaGame).GetField("slain",Private).GetValue(game))[Campaign.Heart];
   check((mask&((1<<9)|(1<<10)))==((1<<9)|(1<<10)),"Actual ritual kills persist both stable spawn bits");
   check(game.ContinueCheckpoint(),"Ritual checkpoint reloads");TriggerRitual(game);
   first=game.enemies.Find(e=>e.spawnId==9);second=game.enemies.Find(e=>e.spawnId==10);
   check(first.hp==0 && second.hp==0 && first.rewarded && second.rewarded && !first.sacrificial && !second.sacrificial && !first.visual.Root.activeSelf && !second.visual.Root.activeSelf,"Saved ritual actors stay defeated, hidden and non-sacrificial after new ritual");
   Call(game,"Damage",first,100000f);Call(game,"Damage",second,100000f);
   check(game.Defeated==defeated && game.Experience==xp && game.Level==level && game.Coins==coins && game.Materials==materials && Loot(game)==loot,"Reloaded ritual actors grant no second XP, materials, coins or pending drops");
   check(((int[])typeof(KryptaGame).GetField("slain",Private).GetValue(game))[Campaign.Heart]==mask,"Ritual spawn respects existing slain mask without resetting it");
   TriggerRitual(game);var fresh=game.enemies.Find(e=>e.spawnId==11);var live=game.enemies.Find(e=>e.spawnId==12);
   check(fresh!=null && live!=null && fresh.hp>0 && live.hp>0 && !fresh.rewarded && !live.rewarded && fresh.sacrificial && live.sacrificial,"Later ritual proceeds to new live actors at IDs 11 and 12");
   Call(game,"Damage",fresh,100000f);Call(game,"SaveCheckpoint");
   check(game.Defeated==defeated+1 && game.Materials==materials+1,"A genuinely new ritual actor grants its reward once");
   check(game.ContinueCheckpoint(),"Partially defeated later ritual reloads");TriggerRitual(game);TriggerRitual(game);
   fresh=game.enemies.Find(e=>e.spawnId==11);live=game.enemies.Find(e=>e.spawnId==12);
   check(fresh.hp==0 && fresh.rewarded && !fresh.visual.Root.activeSelf && live.hp>0 && !live.rewarded && live.sacrificial,"Partial ritual reload distinguishes dead ID 11 from surviving ID 12");
   var boss=game.enemies.Find(e=>e.boss);Call(game,"Damage",boss,100f);float before=boss.hp;
   live.visual.Root.transform.position=new Vector3(12*1.8f,0,29*1.8f);Call(game,"UpdateSpecialEnemy",live);
   check(live.hp==0 && boss.hp>before,"A surviving ritual actor still sacrifices itself and heals the live boss");
  }
  static void Tick()
  {
   if(!EditorApplication.isPlaying)return;if(EditorApplication.timeSinceStartup-started>180){Finish(false,"Timeout");return;}
   var game=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!game || !game.View)return;
   try{RunChecks(game,Check);Check(!errors,"No Unity runtime errors");Finish(true,checks+" assertions: pending enemy loot, exact relics, travel, reload, no duplication, automatic collection save, new journey and ritual summon reload rewards.");}
   catch(Exception e){Finish(false,e.ToString());}
  }
  static void Finish(bool pass,string message)
  {SessionState.SetBool("Nocturnis.LootQA",false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Directory.CreateDirectory("Logs");File.WriteAllText("Logs/loot-persistence-qa-result.txt",(pass ? "PASS: " : "FAIL: ")+message);Debug.Log(message);EditorApplication.Exit(pass ? 0 : 1);}
 }
}
