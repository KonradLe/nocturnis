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
 // Behavioral checks use the real game, interaction methods, combat deaths and checkpoint JSON.
 public static class NocturnisQuestQA
 {
  const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
  static int checks;static double started;static bool errors;static string playerSave;
  [InitializeOnLoadMethod]static void Restore()
  {if(!SessionState.GetBool("Nocturnis.QuestQA",false))return;started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
  public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool("Nocturnis.QuestQA",true);EditorApplication.isPlaying=true;}
  static void Log(string message,string trace,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors=true;}
  static void Check(bool pass,string message){checks++;if(!pass)throw new Exception(message);}
  static object Call(object o,string method,params object[] args){return o.GetType().GetMethod(method,Flags).Invoke(o,args);}
  static object Field(object o,string name){return o.GetType().GetField(name,Flags).GetValue(o);}
  static void SetField(object o,string name,object value){o.GetType().GetField(name,Flags).SetValue(o,value);}
  static Vector3 Cell(int x,int z){return new Vector3(x*1.8f,0,z*1.8f);}
  static void Move(KryptaGame game,Vector3 p){((ActorVisual)Field(game,"player")).Root.transform.position=p;}
  static void Load(KryptaGame game,int zone){game.Resume();Call(game,"LoadZone",zone);Time.timeScale=0;}
  static bool Reload(KryptaGame game){bool loaded=game.ContinueCheckpoint();Time.timeScale=0;return loaded;}
  static Vector3 Basin(KryptaGame game){return (Vector3)typeof(KryptaGame).GetProperty("EmberBasin",Flags).GetValue(game);}
  static void KillAll(KryptaGame game){foreach(var e in game.enemies.ToArray())if(e.hp>0)Call(game,"Damage",e,100000f);}
  static IList Markers(KryptaGame game){return (IList)Field(game,"questMarkers");}
  static object FirstActiveMarker(KryptaGame game)
  {foreach(var m in Markers(game)){var root=(GameObject)Field(m,"root");if(root && root.activeSelf)return m;}throw new Exception("No active quest marker");}
  static object Marker(KryptaGame game,int index)
  {foreach(var marker in Markers(game))if((int)Field(marker,"index")==index && ((GameObject)Field(marker,"root")).activeSelf)return marker;throw new Exception("Missing marker "+index);}
  static void TakeMarker(KryptaGame game)
  {
   var m=FirstActiveMarker(game);int q=(int)Field(m,"quest"),before=game.SideQuestObjectCount(q);
   if(q==2)m=Marker(game,(int)Call(game,"NextValveIndex"));Vector3 point=(Vector3)Field(m,"p");Move(game,point);int actorsBefore=game.enemies.Count;
   Check((bool)Call(game,"InteractQuestMarker"),"Quest object receives interaction");
   if(q==0)
   {
    Check(game.SideQuestObjectCount(q)==before && Field(game,"defendedLamp")!=null,"Lamp needs defense rather than immediate click credit");
    var guards=game.enemies.GetRange(actorsBefore,game.enemies.Count-actorsBefore);Check(guards.Count==2 && guards.TrueForAll(e=>e.hp>0),"Each lamp summons exactly two living attackers");
    float duration=(float)Field(game,"lampRemaining");Move(game,point+Vector3.right*8);Call(game,"UpdateSideMechanics",6f);
    Check(Mathf.Abs((float)Field(game,"lampRemaining")-duration)<.001f,"Leaving the seven-metre circle pauses the defense timer");
    Move(game,point);Call(game,"UpdateSideMechanics",11.9f);Check(game.SideQuestObjectCount(q)==before,"Lamp does not finish before twelve defended seconds");
    foreach(var guard in guards)guard.visual.Root.transform.position=point+Vector3.right*(point.x>22 ? -18 : 18);
    Call(game,"UpdateSideMechanics",.2f);Check(game.SideQuestObjectCount(q)==before,"Living summoned shadows cannot be bypassed by drawing them beyond the local enemy radius");
    Call(game,"Damage",guards[0],100000f);Call(game,"UpdateSideMechanics",0f);Check(game.SideQuestObjectCount(q)==before,"One surviving summoned shadow still blocks completion");
    Call(game,"Damage",guards[1],100000f);Call(game,"UpdateSideMechanics",0f);
   }
   Check(game.SideQuestObjectCount(q)==before+1,"Objective advances exactly once after its real conditions: "+q);
  }
  static void InterruptedLamp(KryptaGame game)
  {
   var marker=FirstActiveMarker(game);Move(game,(Vector3)Field(marker,"p"));Call(game,"InteractQuestMarker");Call(game,"UpdateSideMechanics",4f);Call(game,"SaveCheckpoint");
   Check(game.SideQuestObjectCount(0)==0 && Field(game,"defendedLamp")!=null,"Partially defended lamp has no premature permanent credit");
   Check(Reload(game) && game.SideQuestObjectCount(0)==0 && Field(game,"defendedLamp")==null,"Reload restarts an incomplete defense without granting progress");
   Check(game.enemies.Count==Campaign.EncounterBudget(Campaign.Chapel),"Incomplete defense reload does not duplicate transient shadows");
  }
  static void WrongValve(KryptaGame game)
  {
   int count=game.SideQuestObjectCount(2);int wrong=count==0 ? 1 : count==1 ? 1 : -1;if(wrong<0)return;
   Move(game,(Vector3)Field(Marker(game,wrong),"p"));Call(game,"InteractQuestMarker");Check(game.SideQuestObjectCount(2)==count,"Wrong valve order refuses credit at step "+count);
  }
  static void PartialEmbers(KryptaGame game)
  {
   Vector3 basin=Basin(game);Check(game.CarryingEmber && game.SideQuestState(1)==1,"One saved vessel is carried but does not complete delivery");
   Check(Mathf.Abs(game.PlayerMovementSpeed-game.Hero.speed*.8f)<.001f,"Carrying ember imposes the actual movement penalty");
   Move(game,basin);Check((bool)Call(game,"DeliverEmbers") && game.SideQuestState(1)==1 && game.CarryingEmber,"Basin refuses a partial vessel delivery");
   foreach(var container in (IList)Field(game,"containers"))Check(Vector3.Distance((Vector3)Field(container,"p"),basin)>=4,"Container placement reserves the fixed ember basin");
   game.ReviewBreakContainer(0,100000f);Check((Basin(game)-basin).sqrMagnitude<.00001f,"Breaking geometry cannot move the delivery interaction away from its bowl");
   Check(Reload(game) && (Basin(game)-basin).sqrMagnitude<.00001f && game.CarryingEmber,"Fixed basin and carried partial quest survive reload");
  }
  static void FinishEmbers(KryptaGame game)
  {
   Vector3 basin=Basin(game);Check(game.SideQuestObjectCount(1)==3 && game.SideQuestState(1)==1 && game.CarryingEmber,"Collecting all vessels still requires delivery");
   Move(game,basin+Vector3.right*5);Check(!(bool)Call(game,"DeliverEmbers") && game.SideQuestState(1)==1,"Delivery cannot occur away from the basin");
   Move(game,basin);var enemy=game.enemies[0];enemy.hp=1;enemy.visual.Root.SetActive(true);enemy.visual.Root.transform.position=basin+Vector3.right;
   Check((bool)Call(game,"DeliverEmbers") && game.SideQuestState(1)==1,"A nearby living enemy prevents delivery");Call(game,"Damage",enemy,100000f);
   Check((bool)Call(game,"DeliverEmbers") && game.SideQuestState(1)==2 && !game.CarryingEmber,"Three vessels delivered to a safe basin complete the commission");
   Check(Mathf.Abs(game.PlayerMovementSpeed-game.Hero.speed)<.001f,"Delivery removes the movement penalty");
   int gold=game.Coins;Check(!(bool)Call(game,"DeliverEmbers") && game.Coins==gold,"Repeated delivery cannot duplicate its outcome");
   Check(Reload(game) && game.SideQuestState(1)==2 && !game.CarryingEmber,"Delivery flag survives save/reload before the NPC reward");
  }
  static void LegacyValves(KryptaGame game)
  {
   PlayerPrefs.SetString("Nocturnis.QA.Checkpoint","{\"version\":3,\"hero\":0,\"zone\":"+Campaign.Cistern+",\"level\":3,\"coins\":47,\"xp\":10,\"potions\":2,\"sideAccepted\":4,\"sideObjects\":[0,0,3,0,0]}");
   Check(Reload(game) && game.SideQuestObjectCount(2)==2 && game.SideQuestState(2)==1,"Legacy out-of-order valves I+II retain their two completed objectives");
   Check((int)Call(game,"NextValveIndex")==2,"Legacy I+II correctly requests the still-missing valve III");
   var marker=Marker(game,2);Vector3 expected=(Vector3)Field(marker,"p"),tracked;
   if(game.TrackedQuest!=2)game.TrackQuest(2);
   Check(game.TrackedQuestPosition(out tracked) && (tracked-expected).sqrMagnitude<.00001f,"Map tracks the same missing legacy valve as the interaction rule");
   KillAll(game);Move(game,expected);Call(game,"InteractQuestMarker");
   Check(game.SideQuestObjectCount(2)==3 && game.SideQuestState(2)==2,"Legacy order cannot softlock the last remaining valve");
   Check(Reload(game) && game.SideQuestState(2)==2,"Repaired legacy completion persists");
   Meet(game,2);int coins=game.Coins;Check(game.ClaimSideQuest(2) && game.Coins==coins+SideQuestData.Gold[2],"Migrated valve quest grants its exact reward once");
   Check(!game.ClaimSideQuest(2),"Migrated valve reward also rejects a duplicate claim");
  }
  static void Meet(KryptaGame game,int q)
  {
   Load(game,q==4 ? Campaign.Cemetery : Campaign.Town);
   int[,] cells={{11,8},{8,6},{8,13},{15,8},{12,20}};Move(game,Cell(cells[q,0],cells[q,1]));Call(game,"InteractCampaign");
   Check(game.Service==SideQuestData.Services[q],"Correct NPC service "+q);Check(game.OpenSideQuest(q),"NPC opens commission "+q);
  }
  static void Tick()
  {
   if(!EditorApplication.isPlaying)return;if(EditorApplication.timeSinceStartup-started>240){Finish(false,"Timeout");return;}
   var game=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!game || !game.View)return;
   EditorApplication.update-=Tick;
   try
   {
    playerSave=PlayerPrefs.GetString("Nocturnis.Checkpoint.v2","");KryptaGame.Testing=true;game.enabled=false;var hud=game.GetComponent<KryptaHUD>();if(hud)hud.enabled=false;typeof(KryptaGame).GetProperty("State").SetValue(game,KryptaGame.Phase.Title);game.SelectProfile(1);game.Begin();Time.timeScale=0;
    Check(Campaign.Count==19 && Campaign.Route.Length==14,"Nineteen maps and fourteen-map main route");
    int total=0;for(int z=0;z<Campaign.Count;z++){total+=Campaign.EncounterBudget(z);Check(Campaign.EncounterBudget(z)<30,"Persistent enemy IDs remain below bit limit "+z);}
    Check(total==368,"Authored encounter budget covers nineteen maps and five extended stages");
    // Side exits open on the guardian, not a hidden straggler. Full clearing pays once.
    Load(game,Campaign.Cellar);var guardian=game.enemies[game.enemies.Count-1];Call(game,"Damage",guardian,100000f);
    Check(game.CanExit && game.Remaining>0,"Guardian opens exit before all incidental enemies die");
    int before=game.Coins;Call(game,"RewardFullClear");Check(game.Coins==before,"No full-clear reward while stragglers remain");
    KillAll(game);before=game.Coins;Call(game,"RewardFullClear");Check(game.Coins>before,"Full-clear reward granted");before=game.Coins;Call(game,"RewardFullClear");Check(game.Coins==before,"Full-clear reward cannot be farmed");
    // All three new branch portals enter a distinct dungeon and return to the parent map.
    int[,] branches={{Campaign.Cemetery,19,16,Campaign.Chapel},{Campaign.Crypt2,3,22,Campaign.Cistern},{Campaign.Kings,20,16,Campaign.Ossuary}};
    for(int b=0;b<3;b++)
    {Load(game,branches[b,0]);Move(game,Cell(branches[b,1],branches[b,2]));Call(game,"InteractCampaign");Check(game.Zone==branches[b,3],"Branch entrance "+b);KillAll(game);Move(game,game.ExitPosition);Call(game,"InteractCampaign");Check(game.Zone==branches[b,0],"Branch return "+b);}
    for(int q=0;q<SideQuestData.Count;q++)
    {
     Check(game.SideQuestState(q)==0,"Quest initially available "+q);
     Load(game,SideQuestData.Zones[q]);Move(game,(Vector3)Field(FirstActiveMarker(game),"p"));Call(game,"InteractQuestMarker");Check(game.SideQuestObjectCount(q)==0,"No objective credit before acceptance "+q);
     Meet(game,q);Check(!game.ClaimSideQuest(q),"Premature reward refused "+q);Check(game.AcceptSideQuest(q),"Accept commission "+q);Check(!game.AcceptSideQuest(q),"Duplicate acceptance refused "+q);
     Load(game,SideQuestData.Zones[q]);
     // Temporarily place a living enemy beside the objective to exercise combat gating.
     var marker=FirstActiveMarker(game);Vector3 point=(Vector3)Field(marker,"p");Move(game,point);var living=game.enemies.Find(e=>e.hp>0);
     if(living==null){living=game.enemies[0];living.hp=1;living.visual.Root.SetActive(true);}living.visual.Root.transform.position=point+Vector3.right;
     Call(game,"InteractQuestMarker");Check(game.SideQuestObjectCount(q)==0,"Collection waits until nearby combat ends "+q);KillAll(game);
     if(q==0)InterruptedLamp(game);if(q==2)WrongValve(game);
     TakeMarker(game);Check(game.SideQuestObjectCount(q)==1,"One objective collected "+q);
     Check(Reload(game),"Continue partial objective save "+q);Check(game.SideQuestObjectCount(q)==1,"Partial objective survives save "+q);
     Check(Markers(game).Count==SideQuestData.Required[q]-1,"Collected prop stays removed after load "+q);
     if(q==1)PartialEmbers(game);if(q==2)WrongValve(game);
     while(game.SideQuestObjectCount(q)<SideQuestData.Required[q])TakeMarker(game);
     if(q==1)FinishEmbers(game);
     if(q==4)
     {
      Check(game.SideQuestState(q)==1,"Oren also requires grave defense");Load(game,Campaign.Cemetery);Move(game,Cell(12,20));
      // This suite executes synchronously in the first editor frame, where Time.time
      // may still be zero. Set the deadline in the past, not exactly at zero.
      for(int wave=0;wave<3;wave++){KillAll(game);SetField(game,"waveAt",Time.time-1f);Call(game,"UpdateCampaign");Check(game.enemies.Count==4+(wave+1)*5,"Cemetery wave "+(wave+1)+" adds exactly five identities (count="+game.enemies.Count+", time="+Time.time+")");}
      KillAll(game);Check(game.enemies.Count==19,"Three cemetery waves preserve unique save IDs");
     }
     Check(game.SideQuestState(q)==2,"Quest ready only after all objectives "+q);Meet(game,q);
     before=game.Coins;int materials=game.Materials;Check(game.ClaimSideQuest(q),"NPC grants completed reward "+q);
     Check(game.Coins==before+SideQuestData.Gold[q] && game.Materials==materials+SideQuestData.Materials[q],"Exact currency reward "+q);
     before=game.Coins;Check(!game.ClaimSideQuest(q) && game.Coins==before,"No duplicate claim "+q);
     Check(Reload(game) && game.SideQuestState(q)==3,"Claim survives reload "+q);Meet(game,q);Check(!game.ClaimSideQuest(q),"Reload cannot duplicate reward "+q);
    }
    Check(game.CompletedSideQuests==5,"All five authored side quests can be completed");
    // Complete every authored main-route event using live guardians and actual E interaction.
    for(int zone=Campaign.Ravine;zone<=Campaign.Threshold;zone++)
    {
     Load(game,zone);Check(game.enemies.Count==26,"Stage has twenty-six persistent enemy identities "+zone);
     for(int guardianID=22;guardianID<=25;guardianID++)Check(game.enemies[guardianID].elite,"Authored guardian is elite "+zone+"/"+guardianID);
     var stageMarkers=(IList)Field(game,"stageMarkers");Check(stageMarkers.Count==3,"Stage has three interaction objectives "+zone);
     Move(game,(Vector3)Field(stageMarkers[0],"p"));Call(game,"InteractCampaign");Check(game.StageObjectiveCount==0,"Live site guardian prevents objective interaction "+zone);
     Call(game,"Damage",game.enemies[25],100000f);Check(!game.CanExit,"Exit guardian alone cannot skip stage objectives "+zone);
     Call(game,"Damage",game.enemies[22],100000f);var firstPoint=(Vector3)Field(stageMarkers[0],"p");
     foreach(var enemy in game.enemies.ToArray())if(enemy.hp>0 && Vector3.Distance(enemy.Position,firstPoint)<9)Call(game,"Damage",enemy,100000f);
     Move(game,firstPoint);Call(game,"InteractCampaign");Check(game.StageObjectiveCount==1,"First secured event completes through E "+zone);
     Check(Reload(game) && game.StageObjectiveCount==1,"Partial stage progress survives save/reload "+zone);Check(!game.CanExit,"Reload does not bypass remaining event objectives "+zone);
     KillAll(game);stageMarkers=(IList)Field(game,"stageMarkers");Move(game,(Vector3)Field(stageMarkers[1],"p"));Call(game,"InteractCampaign");Check(game.StageObjectiveCount==2,"Second stage objective completes "+zone);
     before=game.Coins;int materialsBefore=game.Materials;Move(game,(Vector3)Field(stageMarkers[2],"p"));Call(game,"InteractCampaign");
     Check(game.StageObjectivesComplete && game.StageObjectiveCount==3 && game.CanExit,"Three actions and guardian unlock stage exit "+zone);
     Check(game.Coins==before+30+(zone-Campaign.Ravine)*5 && game.Materials==materialsBefore+6,"Stage completion pays exact one-time reward "+zone);
     before=game.Coins;Call(game,"InteractCampaign");Check(game.Coins==before,"Completed event cannot repeat its reward "+zone);
     Check(Reload(game) && game.StageObjectivesComplete,"Completed stage state persists "+zone);stageMarkers=(IList)Field(game,"stageMarkers");Move(game,(Vector3)Field(stageMarkers[2],"p"));Call(game,"InteractCampaign");Check(game.Coins==before,"Reload cannot duplicate stage reward "+zone);
     Move(game,game.ExitPosition);Call(game,"InteractCampaign");Check(game.Zone==Campaign.Next(zone),"Completed stage proceeds along authored main route "+zone);
    }
    LegacyValves(game);
    // Version 0.5.1 had eleven slain masks. Expanding the array must not resurrect old kills.
    PlayerPrefs.SetString("Nocturnis.QA.Checkpoint","{\"version\":2,\"hero\":1,\"zone\":3,\"level\":3,\"coins\":47,\"xp\":10,\"potions\":2,\"weapon\":1,\"armor\":1,\"charm\":0,\"unlocked\":15,\"cleared\":0,\"slain\":[0,0,0,16,0,0,0,0,0,0,0]}");
    Check(Reload(game),"Partial legacy road checkpoint loads");Check(game.enemies[9].boss && game.enemies[9].hp<=0 && game.CanExit,"Legacy road boss defeat moves from ID4 to ID9");Check(game.enemies[4].hp>0,"Legacy boss bit does not suppress an unrelated new road actor");
    PlayerPrefs.SetString("Nocturnis.QA.Checkpoint","{\"version\":2,\"hero\":1,\"zone\":2,\"level\":3,\"coins\":47,\"xp\":10,\"potions\":2,\"weapon\":1,\"armor\":1,\"charm\":0,\"unlocked\":15,\"cleared\":8,\"slain\":[0,0,8,31,0,0,0,0,0,0,0]}");
    Check(Reload(game),"Version 0.5.1 checkpoint migrates");var kills=(int[])Field(game,"slain");
    Check(kills.Length==19 && kills[Campaign.Crypt]==32,"Legacy actor ID3 maps to the corresponding new ID5 in nineteen-map save");Check(game.enemies[5].hp<=0 && game.enemies[3].hp>0,"Legacy dead actor remains defeated while added actor stays alive");
    Check((kills[Campaign.Road]&1023)==1023,"Previously cleared road stays cleared despite added enemies");
    Check(game.SideQuestState(0)==0 && game.CompletedSideQuests==0,"Legacy save starts optional quests safely");
    Check(game.Coins==47 && game.SelectedHero==1 && game.Level==3,"Legacy progression retained");
    Call(game,"ResetCampaign");Check(game.CompletedSideQuests==0 && game.SideQuestState(0)==0,"New journey resets optional progress");
    Check(PlayerPrefs.GetString("Nocturnis.Checkpoint.v2","")==playerSave,"Player checkpoint untouched by QA");Check(!errors,"No runtime errors");
    Finish(true,checks+" assertions; 19 maps, 368 authored enemies plus bounded boss reinforcements, 5 complete quest loops including defended lamps, fixed-basin ember delivery and ordered valves, 15 secured main-stage objectives with guardians, portal return, non-repeatable rewards, partial save and legacy migration. One-hour duration remains a playtest target, not a measured human result.");
   }
   catch(Exception e){Finish(false,e.ToString());}
  }
  static void Finish(bool pass,string message)
  {SessionState.SetBool("Nocturnis.QuestQA",false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Directory.CreateDirectory("Logs");File.WriteAllText("Logs/act1-quest-qa.txt",(pass ? "PASS: " : "FAIL: ")+message);Debug.Log(message);EditorApplication.Exit(pass ? 0 : 1);}
 }
}
