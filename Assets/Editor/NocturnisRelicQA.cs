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
 public static class NocturnisRelicQA
 {
  const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  const string Session="Nocturnis.RelicQA",Key="Nocturnis.QA.Checkpoint",StashKey="Nocturnis.QA.Stash";
  static double started;static int checks;static string errors="";
  static object Call(KryptaGame g,string name,params object[] args){return typeof(KryptaGame).GetMethod(name,Private).Invoke(g,args);}
  static object Field(KryptaGame g,string name){return typeof(KryptaGame).GetField(name,Private).GetValue(g);}
  static IList List(KryptaGame g,string name){return (IList)Field(g,name);}
  static void Set(KryptaGame g,string name,object value){typeof(KryptaGame).GetProperty(name).SetValue(g,value);}
  static void Put(object obj,string name,object value){obj.GetType().GetField(name).SetValue(obj,value);}
  static object Get(object obj,string name){return obj.GetType().GetField(name).GetValue(obj);}
  static void Check(bool pass,string message){checks++;if(!pass)throw new Exception(message);}
  static bool Near(float a,float b){return Mathf.Abs(a-b)<.025f;}
  static string Item(KryptaGame.Relic item){return JsonUtility.ToJson(item);}
  static void Fresh(KryptaGame g,int hero)
  {Set(g,"State",KryptaGame.Phase.Title);g.SelectHero(hero);g.Begin();Time.timeScale=0;g.Status.Clear();}
  static bool Reload(KryptaGame g){bool loaded=g.ContinueCheckpoint();Time.timeScale=0;return loaded;}
  static KryptaGame.Enemy Target(KryptaGame g)
  {var enemy=g.enemies.Find(e=>e.hp>0 && !e.boss && !e.elite);enemy.archetype=0;enemy.armorUntil=0;enemy.hp=enemy.maxHp=1000;return enemy;}
  static void Equip(KryptaGame g,KryptaGame.Relic item){Call(g,"EquipRelicData",item);}
  static void Hurt(KryptaGame g,float amount)
  {typeof(KryptaGame).GetField("hurtUntil",Private).SetValue(g,0f);Call(g,"Hurt",amount);}
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
  public static void RunChecks(KryptaGame g,Action<bool,string> check)
  {
   KryptaGame.Testing=true;int profile=g.ActiveProfile;Set(g,"State",KryptaGame.Phase.Title);g.SelectProfile(1);
   bool had=PlayerPrefs.HasKey(Key),hadStash=PlayerPrefs.HasKey(StashKey);string save=PlayerPrefs.GetString(Key,""),stash=PlayerPrefs.GetString(StashKey,"");PlayerPrefs.DeleteKey(StashKey);
   try{PersistenceAndSwap(g,check);Affixes(g,check);Legends(g,check);Container(g,check);}
   finally
   {if(had)PlayerPrefs.SetString(Key,save);else PlayerPrefs.DeleteKey(Key);if(hadStash)PlayerPrefs.SetString(StashKey,stash);else PlayerPrefs.DeleteKey(StashKey);PlayerPrefs.SetInt("Nocturnis.QA.ActiveProfile",profile);PlayerPrefs.Save();Time.timeScale=0;}
  }
  static void PersistenceAndSwap(KryptaGame g,Action<bool,string> check)
  {
   Fresh(g,0);
   var worn=new KryptaGame.Relic{slot=0,rank=5,enchant=3,identified=true,affix=1,affixValue=7,legend=1,legendHero=0};
   g.Inventory.Add(KryptaGame.CopyRelic(worn));g.OpenEquipment();check(g.EquipItem(0),"Real equip action installs an affixed class relic");g.Resume();Time.timeScale=0;
   check(Item(g.EquippedRelic(0))==Item(worn),"Equipped slot retains exact enchant, affix and class effect");
   for(int i=0;i<KryptaGame.InventoryCapacity;i++)g.Inventory.Add(new KryptaGame.Relic{slot=i%3,rank=2,enchant=i%4,affix=2,affixValue=2});
   var incoming=new KryptaGame.Relic{slot=0,rank=4,enchant=1,identified=true,affix=3,affixValue=6,legend=2,legendHero=2};
   Call(g,"CreateRelicDropData",g.HeroPosition,incoming);Call(g,"UpdateRelics");Call(g,"SaveCheckpoint");
   check(Reload(g) && Item(g.EquippedRelic(0))==Item(worn) && Item(g.NearbyRelics()[0])==Item(incoming),"Checkpoint restores exact equipped and pending world relic metadata");
   var outgoing=KryptaGame.CopyRelic(g.Inventory[0]);g.OpenEquipment();
   check(g.SwapGroundRelic(0,0),"Explicit ground swap succeeds with a completely full backpack");
   check(g.Inventory.Count==KryptaGame.InventoryCapacity && Item(g.Inventory[0])==Item(incoming) && Item(g.NearbyRelics()[0])==Item(outgoing),"Ground swap exchanges intact items without deletion or capacity overflow");
   check(Reload(g) && Item(g.Inventory[0])==Item(incoming) && Item(g.NearbyRelics()[0])==Item(outgoing),"Ground swap survives a reload without duplication or reroll");
   check(g.RelicComparison(incoming).Contains("Wyrok") && g.RelicLegendLabel(incoming).Contains("Nieaktywne"),"Comparison exposes affix tradeoff and a class mismatch");
   Set(g,"Service","stash");check(g.StoreItem(0,false) && Item(g.Stash[0])==Item(incoming),"Shared stash preserves the full class relic record");
   check(g.StoreItem(0,true) && Item(g.Inventory[g.Inventory.Count-1])==Item(incoming),"Withdrawal restores the exact same class relic");
   Fresh(g,0);check(Near(g.RelicDamageMultiplier(Target(g)),1) && g.EquippedRelic(0).affix==0,"New journey clears all equipped affixes and effects");
   Set(g,"Materials",12);Set(g,"Level",6);Set(g,"Service","forge");check(g.Craft(),"Forge crafts a new affixed relic");
   check(g.Inventory.Count==1 && g.Inventory[0].rank==3 && g.Inventory[0].affix>=1 && g.Inventory[0].affix<=3 && g.Inventory[0].affixValue>0,"Crafted uncommon gear has an actual saved property, not only a rarity name");
   Call(g,"SaveCheckpoint");string crafted=Item(g.Inventory[0]);check(Reload(g) && Item(g.Inventory[0])==crafted,"Crafted affix does not reroll when loading");
  }
  static void Affixes(KryptaGame g,Action<bool,string> check)
  {
   Fresh(g,0);var enemy=Target(g);
   Equip(g,new KryptaGame.Relic{slot=0,rank=2,affix=1,affixValue=5});Call(g,"Damage",enemy,100f);
   check(Near(1000-enemy.hp,105),"Damage pipeline applies a +5% damage affix exactly once");
   Equip(g,new KryptaGame.Relic{slot=0,rank=2,affix=3,affixValue=7});enemy.hp=1000;Call(g,"Damage",enemy,100f);
   check(Near(1000-enemy.hp,100),"Execute affix does not affect healthy enemies");enemy.hp=300;Call(g,"Damage",enemy,100f);
   check(Near(300-enemy.hp,107),"Execute affix applies below 35% health");
   Equip(g,new KryptaGame.Relic{slot=0,rank=2,affix=2,affixValue=5});Set(g,"Health",100f);Hurt(g,20);
   check(Near(g.Health,81),"Incoming damage pipeline applies defensive affix once");
   Equip(g,new KryptaGame.Relic{slot=1,rank=2,affix=2,affixValue=10});Equip(g,new KryptaGame.Relic{slot=2,rank=2,affix=2,affixValue=10});
   check(Near(g.ApplyRelicProtection(100),88),"Defensive affixes have the explicit 12% aggregate cap");
  }
  static void Legends(KryptaGame g,Action<bool,string> check)
  {
   foreach(int hero in new[]{0,4})
   {
    Fresh(g,hero);Equip(g,new KryptaGame.Relic{slot=2,rank=4,legend=1,legendHero=hero});var enemy=Target(g);Call(g,"Damage",enemy,1000000f);
    check(g.Status.Count("relic_ward",Time.time)==1,"Class "+hero+" earns a shield from a real kill");Set(g,"Health",100f);Hurt(g,40);
    check(Near(g.Health,70) && g.Status.Count("relic_ward",Time.time)==0,"Shield reduces one hit by 25% and consumes itself for class "+hero);Hurt(g,40);
    check(Near(g.Health,30),"Shield does not reduce a second hit for class "+hero);Call(g,"Damage",enemy,1000000f);
    check(g.Status.Count("relic_ward",Time.time)==0,"Already defeated enemy cannot grant another shield");
   }
   Fresh(g,2);Equip(g,new KryptaGame.Relic{slot=0,rank=4,legend=2,legendHero=2});var archerTarget=Target(g);
   archerTarget.visual.Root.transform.position=g.HeroPosition+Vector3.right*6;Call(g,"Damage",archerTarget,100f);
   check(Near(1000-archerTarget.hp,115),"Hunter class relic rewards attacks beyond 5 metres");
   archerTarget.hp=1000;archerTarget.visual.Root.transform.position=g.HeroPosition+Vector3.right*3;Call(g,"Damage",archerTarget,100f);
   check(Near(1000-archerTarget.hp,100),"Hunter class relic loses its distance bonus at close range");
   foreach(int hero in new[]{1,3})
   {
    Fresh(g,hero);Equip(g,new KryptaGame.Relic{slot=2,rank=4,legend=3,legendHero=hero});Set(g,"Resource",0f);var enemy=Target(g);Call(g,"Damage",enemy,1000000f);
    float expected=hero==1 ? 35 : 18;check(Near(g.Resource,expected),"Class "+hero+" receives its baseline kill resource plus the relic's 10");
    Call(g,"Damage",enemy,1000000f);check(Near(g.Resource,expected),"Repeated damage to a corpse cannot trigger the resource relic");
    var next=Target(g);Call(g,"Damage",next,1000000f);check(Near(g.Resource,expected+(hero==1 ? 25 : 8)),"Resource relic respects its two-second cooldown across distinct kills");
   }
   Fresh(g,0);Equip(g,new KryptaGame.Relic{slot=2,rank=4,legend=1,legendHero=4});var mismatch=Target(g);Call(g,"Damage",mismatch,1000000f);
   check(g.Status.Count("relic_ward",Time.time)==0,"A class relic is inactive when equipped by a different class");
  }
  static void Container(KryptaGame g,Action<bool,string> check)
  {
   Fresh(g,0);for(int i=0;i<KryptaGame.InventoryCapacity;i++)g.Inventory.Add(new KryptaGame.Relic{slot=0,rank=1});
   var container=List(g,"containers")[0];Put(container,"roll",30);Vector3 position=(Vector3)Get(container,"p");
   ((ActorVisual)Field(g,"player")).Root.transform.position=position;Call(g,"DamageContainer",container,1000000f);
   check(g.ContainerLootOnGround==1,"A real broken container creates the controlled relic fixture");Call(g,"UpdateContainerLoot");
   check(g.ContainerLootOnGround==0 && List(g,"relics").Count==1 && g.Inventory.Count==KryptaGame.InventoryCapacity && g.Materials==0,"Full bag transfers the intact container relic to world loot without salvage");
   string dropped=Item((KryptaGame.Relic)Get(List(g,"relics")[0],"item"));
   check(Reload(g) && List(g,"relics").Count==1 && g.ContainerLootOnGround==0,"Container ID and transferred drop reload atomically without duplicate rewards");
   check(Item((KryptaGame.Relic)Get(List(g,"relics")[0],"item"))==dropped,"Transferred container relic retains exact properties after reload");
  }
  static void Finish(bool pass,string message)
  {SessionState.SetBool(Session,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Directory.CreateDirectory("Logs");File.WriteAllText("Logs/relic-qa-result.txt",(pass ? "PASS: " : "FAIL: ")+checks+" assertions. "+message);EditorApplication.Exit(pass ? 0 : 1);}
 }
}
