using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace Krypta.Editor
{
 // Editor-only visual QA. Never included in a player build. Commands are local files, not a network endpoint.
 [InitializeOnLoad]public static class NocturnisPreview
 {
  const string Command="Temp/nocturnis-preview-command.txt";
  static double at;
  static NocturnisPreview(){EditorApplication.update+=Tick;}
  static void Tick()
  {
   if(Application.isBatchMode || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup<at)return;
   at=EditorApplication.timeSinceStartup+.5;
   if(!File.Exists(Command))return;
   var g=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!g || !g.View)return;
   string command=File.ReadAllText(Command).Trim();File.Delete(Command);
   var type=typeof(KryptaGame);const BindingFlags p=BindingFlags.Instance|BindingFlags.NonPublic;
   KryptaGame.Testing=true;
   if(command=="knightmenu" || command=="knightforest" || command=="knightrun")
   {
    type.GetProperty("State").SetValue(g,KryptaGame.Phase.Title,null);g.SelectHero(0);
    if(command!="knightmenu")
    {
     type.GetMethod("LoadZone",p).Invoke(g,new object[]{Campaign.Forest});g.Resume();
     var actor=(ActorVisual)type.GetField("player",p).GetValue(g);
     actor.Root.transform.position=new Vector3(14.4f,0,36);
     actor.Root.transform.rotation=Quaternion.LookRotation(new Vector3(9,0,-11));
     g.View.transform.position=actor.Root.transform.position+new Vector3(9,15,-11);
     string clip=command=="knightrun" ? "Running_A" : "Idle";actor.Play(clip,true,30);
     var animation=actor.Root.GetComponentInChildren<Animation>();animation.Play(clip);animation[clip].time=.25f;animation.Sample();
    }
    Time.timeScale=0;
   }
   else if(command=="journey")NocturnisJourney.Start(g);
   else if(command=="hunter" || command=="alchemy")
   {
    type.GetProperty("State").SetValue(g,KryptaGame.Phase.Title,null);g.SelectHero(command=="hunter" ? 2 : 0);g.Resume();
    type.GetMethod("LoadZone",p).Invoke(g,new object[]{command=="hunter" ? Campaign.Crypt : 0});
    if(command=="hunter"){type.GetProperty("Health").SetValue(g,22f,null);type.GetProperty("Resource").SetValue(g,75f,null);type.GetProperty("Shadow").SetValue(g,40f,null);}
    else {var a=(ActorVisual)type.GetField("player",p).GetValue(g);a.Root.transform.position=new Vector3(14.4f,0,23.4f);g.Interact();type.GetProperty("Coins").SetValue(g,300,null);g.GemBag[0]=3;g.GemBag[1]=1;typeof(KryptaHUD).GetField("alchemyPage",p).SetValue(UnityEngine.Object.FindFirstObjectByType<KryptaHUD>(),1);}
    foreach(var e in g.enemies)e.stunUntil=Time.time+30;Time.timeScale=0;
   }
   else if(command=="cryptfx" || command=="cryptarena" || command=="forestview" || command=="forestdeep")
   {
    type.GetMethod("LoadZone",p).Invoke(g,new object[]{command=="forestview" || command=="forestdeep" ? Campaign.Forest : Campaign.Crypt});g.Resume();
    var actor=(ActorVisual)type.GetField("player",p).GetValue(g);
    actor.Root.transform.position=command=="forestdeep" ? new Vector3(14.4f,0,36) : command=="cryptarena" ? new Vector3(30.6f,0,59.4f) : new Vector3(9,0,7.2f);
    g.View.transform.position=actor.Root.transform.position+new Vector3(9,15,-11);
    if(command!="forestview" && command!="forestdeep")foreach(var e in g.enemies)e.stunUntil=Time.time+30;
    if(command=="cryptfx")
    {
     type.GetField("pulseAt",p).SetValue(g,0f);type.GetField("defenseAt",p).SetValue(g,0f);type.GetProperty("Resource").SetValue(g,100f,null);
     type.GetMethod("ActivateShrine",p).Invoke(g,new object[]{"slaughter"});g.CastDefense();g.CastSkill(g.HeroPosition+Vector3.forward*2);
     type.GetMethod("UpdateEffects",p).Invoke(g,null);
    }
    foreach(var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))ps.Simulate(.25f,true,false);
    Time.timeScale=0;
   }
   else if(command=="book" || command=="equipment")
   {g.Resume();if(command=="book")g.OpenBook();else {g.Inventory.Clear();for(int i=0;i<12;i++)g.Inventory.Add(new KryptaGame.Relic{slot=i%3,rank=1+i%5,enchant=i%4,identified=i%5<3});g.OpenEquipment();}}
   else if(command=="road" || command=="fields" || command=="cemetery" || command=="heart" || command=="kings")
   {
    int z=command=="road" ? Campaign.Road : command=="fields" ? Campaign.Fields : command=="cemetery" ? Campaign.Cemetery : command=="kings" ? Campaign.Kings : Campaign.Heart;
    type.GetMethod("LoadZone",p).Invoke(g,new object[]{z});g.Resume();
    var actor=(ActorVisual)type.GetField("player",p).GetValue(g);actor.Root.transform.position=z==Campaign.Heart ? new Vector3(21.6f,0,43.2f) : z==Campaign.Cemetery ? new Vector3(21.6f,0,45) : g.SpawnPosition;
    g.View.transform.position=actor.Root.transform.position+new Vector3(9,15,-11);foreach(var e in g.enemies)e.stunUntil=Time.time+30;
    foreach(var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))ps.Simulate(.3f,true,false);Time.timeScale=0;
   }
   else if(command=="forest" || command=="crypt" || command=="town")
   {
    int zone=command=="forest" ? 1 : command=="crypt" ? 2 : 0;
    type.GetMethod("LoadZone",p).Invoke(g,new object[]{zone});g.Resume();
   }
   else if(command=="buff")
   {type.GetMethod("ActivateShrine",p).Invoke(g,new object[]{"slaughter"});g.CastDefense();}
   else if(command=="pause")g.Pause();
   else if(command=="resume")g.Resume();
   else if(command=="title")
   {type.GetMethod("LoadZone",p).Invoke(g,new object[]{0});type.GetProperty("State").SetValue(g,KryptaGame.Phase.Title,null);Time.timeScale=0;}
   else if(command=="npc")
   {var a=(ActorVisual)type.GetField("player",p).GetValue(g);a.Root.transform.position=new Vector3(19.8f,0,12.6f);g.Interact();}
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/visual-state.txt",command+" / "+g.State+" / "+g.Zone+" / "+g.Hero.name+" / enemies "+g.Remaining);
   if(command!="journey")g.StartCoroutine(Capture(command));
  }
  static System.Collections.IEnumerator Capture(string command)
  {yield return new WaitForSecondsRealtime(1.5f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Logs/screen-"+command+".png");}
 }
}
