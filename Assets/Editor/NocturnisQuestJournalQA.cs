using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace Krypta.Editor
{
 // Run from the integrated Play Mode QA with an isolated QA profile; never a real save.
 public static class NocturnisQuestJournalQA
 {
  const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
  static object Call(KryptaGame g,string method,params object[] args){return typeof(KryptaGame).GetMethod(method,Private).Invoke(g,args);}
  static void Set(KryptaGame g,string property,object value){typeof(KryptaGame).GetProperty(property).SetValue(g,value);}
  static void Field(KryptaGame g,string field,object value){typeof(KryptaGame).GetField(field,Private).SetValue(g,value);}
  static int TotalXp(KryptaGame g){return (g.Level-1)*g.Level*30+g.Experience;}
  static bool Open(KryptaGame g,int q)
  {Call(g,"Talk",SideQuestData.NPCs[q],SideQuestData.Offers[q],SideQuestData.Services[q]);return g.OpenSideQuest(q);}
  public static void RunChecks(KryptaGame g,Action<bool,string> check)
  {
   if(!KryptaGame.Testing)throw new InvalidOperationException("Quest QA requires an isolated QA profile.");
   Set(g,"State",KryptaGame.Phase.Title);Call(g,"ResetCampaign");Call(g,"LoadZone",Campaign.Town);Set(g,"State",KryptaGame.Phase.Playing);Time.timeScale=0;
   check(g.TrackedQuestCount==0,"New campaign has no stale quest pins");
   check(!g.ToggleQuestTracking(0) && !g.ToggleQuestTracking(-1) && !g.ToggleQuestTracking(99),"Available/invalid quests cannot be pinned");
   for(int q=0;q<4;q++)
   {
    check(Open(g,q),"Quest offer opens at correct NPC "+q);
    check(g.AcceptSideQuest(q),"Offer accepts exactly once "+q);
    check(!g.AcceptSideQuest(q),"Repeated accept rejected "+q);
   }
   check(g.TrackedQuestCount==3 && g.IsQuestTracked(0) && g.IsQuestTracked(1) && g.IsQuestTracked(2) && !g.IsQuestTracked(3),"First three accepted quests auto-pin; fourth cannot displace them");
   check(!g.ToggleQuestTracking(3) && g.TrackedQuestCount==3,"Fourth pin rejected at limit");
   check(g.ToggleQuestTracking(1) && g.ToggleQuestTracking(3),"Unpin frees a slot for another quest");
   string[] ids=g.CaptureQuestTracking();check(ids.SequenceEqual(new[]{QuestJournalData.IDs[0],QuestJournalData.IDs[2],QuestJournalData.IDs[3]}),"Pins use stable ordered IDs");
   int xp=TotalXp(g),coins=g.Coins;check(!g.ClaimSideQuest(3) && g.Coins==coins && TotalXp(g)==xp,"Incomplete quest grants no reward");
   g.Resume();check((bool)Call(g,"SaveCheckpoint"),"Quest checkpoint saved");
   g.ResetQuestJournal();check(g.TrackedQuestCount==0,"Tracking reset clears in-memory pins");
   check(g.ContinueCheckpoint() && g.CaptureQuestTracking().SequenceEqual(ids),"Checkpoint roundtrip preserves all three selected pins and order");Time.timeScale=0;
   var objects=(int[])typeof(KryptaGame).GetField("sideObjects",Private).GetValue(g);objects[0]=7;
   Field(g,"nextJournalObservation",0f);g.TickQuestJournal();check(g.SideQuestState(0)==2 && g.Notice.Contains("Cele ukończone"),"Completion announces return to NPC without auto-granting reward");
   coins=g.Coins;xp=TotalXp(g);check(!g.ClaimSideQuest(0) && g.Coins==coins && TotalXp(g)==xp,"Ready quest cannot be claimed remotely");
   check(Open(g,0) && g.ClaimSideQuest(0),"Ready quest grants reward at correct NPC");
   check(g.Coins==coins+SideQuestData.Gold[0] && TotalXp(g)==xp+SideQuestData.XP[0],"Quest reward amount is exact including level-up");
   check(!g.IsQuestTracked(0) && g.TrackedQuestCount==2,"Reward removes completed pin");
   coins=g.Coins;xp=TotalXp(g);check(!g.ClaimSideQuest(0) && g.Coins==coins && TotalXp(g)==xp,"Repeat claim cannot duplicate reward");
   g.Resume();Call(g,"SaveCheckpoint");check(g.ContinueCheckpoint(),"Completed quest loads");Time.timeScale=0;
   check(Open(g,0) && !g.ClaimSideQuest(0) && g.Coins==coins && TotalXp(g)==xp,"Reload and NPC revisit cannot duplicate reward");
   g.RestoreQuestTracking(new[]{"unknown.quest",QuestJournalData.IDs[0],QuestJournalData.IDs[1],QuestJournalData.IDs[1],QuestJournalData.IDs[2],QuestJournalData.IDs[3],QuestJournalData.IDs[4]});
   check(g.CaptureQuestTracking().SequenceEqual(new[]{QuestJournalData.IDs[1],QuestJournalData.IDs[2],QuestJournalData.IDs[3]}),"Restore drops unknown/completed/unaccepted/duplicate IDs and enforces limit");
   g.RestoreQuestTracking(null,2);check(g.IsQuestTracked(2) && g.TrackedQuestCount==1,"Legacy single active pin migrates");
   g.RestoreQuestTracking(new string[0],2);check(g.TrackedQuestCount==0,"Explicitly empty new pins do not resurrect legacy preference");
   Set(g,"State",KryptaGame.Phase.Playing);Set(g,"Unlocked",(1<<Campaign.Count)-1);Set(g,"Cleared",1<<Campaign.Heart);
   check(g.JournalChapterState(4)!=3,"Defeating all Heart monsters alone does not complete artifact recovery");
   g.RestoreJournalActFinished(true);check(g.JournalChapterState(4)==3,"Recorded artifact recovery completes final chapter");
   Call(g,"SaveCheckpoint");g.RestoreJournalActFinished(false);check(g.ContinueCheckpoint() && g.JournalActFinished,"Main finale completion survives checkpoint roundtrip");Time.timeScale=0;
   Call(g,"ResetCampaign");check(g.TrackedQuestCount==0 && !g.JournalActFinished,"New run clears quest pins and finale record");
   check(g.JournalChapterState(0)==1 && g.JournalChapterState(1)==0,"New run starts first chapter without unlocking later chapters");
  }
 }
}