using System;
using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaGame
 {
  [Serializable]public sealed class RecallSave {public int count=2,zone=-1;public float x,z;}
  int recallScrolls=2,recallTraceZone=-1;
  Vector3 recallTracePosition,recallOrigin;
  float recallStart,recallDuration;
  bool recallActive,recallUsesScroll;
  public int RecallScrolls {get{return recallScrolls;}}
  public bool RecallActive {get{return recallActive;}}
  public float RecallProgress {get{return recallActive ? Mathf.Clamp01((Time.time-recallStart)/recallDuration) : 0;}}
  public bool HasRecallTrace {get{return recallTraceZone>=0 && recallTraceZone<Campaign.Count && recallTraceZone!=Campaign.Town;}}
  public string RecallTraceName {get{return HasRecallTrace ? Campaign.Names[recallTraceZone] : "";}}
  public string RecallCaption {get{return recallUsesScroll ? "Zwój Powrotu • skupienie 1,5 s" : "Modlitwa powrotu • skupienie 8 s";}}
  public int RecallScrollPrice {get{return 15+Mathf.Min(15,Level*2);}}
  public RecallSave CaptureRecall(){return new RecallSave{count=recallScrolls,zone=recallTraceZone,x=recallTracePosition.x,z=recallTracePosition.z};}
  public void RestoreRecall(RecallSave s)
  {
   recallActive=false;recallScrolls=s==null ? 2 : Mathf.Clamp(s.count,0,99);recallTraceZone=-1;
   if(s!=null && s.zone>=0 && s.zone<Campaign.Count && s.zone!=Campaign.Town && ZoneUnlocked(s.zone) && Finite(s.x) && Finite(s.z))
   {recallTraceZone=s.zone;recallTracePosition=new Vector3(Mathf.Clamp(s.x,0,(DungeonGrid.Width-1)*Tile),0,Mathf.Clamp(s.z,0,(DungeonGrid.Height-1)*Tile));}
  }
  void ResetRecall(){recallActive=false;recallScrolls=2;recallTraceZone=-1;recallTracePosition=Vector3.zero;}
  public bool AddRecallScrolls(int count)
  {
   if(count<=0 || recallScrolls>=99)return false;int added=Mathf.Min(count,99-recallScrolls);recallScrolls+=added;
   ShowNotice("+"+added+" Zwój Powrotu • użyj [T]");Sound("coin");return true;
  }
  public bool BuyRecallScroll()
  {
   if(State!=Phase.Dialogue || Zone!=Campaign.Town || (Service!="waypoint" && Service!="market") || Coins<RecallScrollPrice || recallScrolls>=99)return false;
   Coins-=RecallScrollPrice;AddRecallScrolls(1);SaveCheckpoint();return true;
  }
  public bool StartRecall()
  {
   if(recallActive){CancelRecall(true);return false;}
   if(State!=Phase.Playing || Zone==Campaign.Town)return false;
   if(!ZoneUnlocked(Campaign.Town)){ShowNotice("Najpierw dotrzyj do Oakhaven za mostem.");return false;}
   if(Health<=0)return false;
   CancelCombatAction();target=null;hasDestination=false;recallOrigin=HeroPosition;
   recallUsesScroll=recallScrolls>0;recallDuration=recallUsesScroll ? 1.5f : 8;recallStart=Time.time;recallActive=true;
   Sound("shrine");ShowNotice(RecallCaption+" • ruch lub obrażenia przerywają");return true;
  }
  public void CancelRecall(bool notify=false)
  {if(!recallActive)return;recallActive=false;if(notify)ShowNotice("Powrót przerwany • zwój zachowany");}
  bool TickRecall()
  {
   if(!recallActive || State!=Phase.Playing)return false;
   bool moved=(HeroPosition-recallOrigin).sqrMagnitude>.04f;
   foreach(string key in new[]{"up","down","left","right","skill","defense","extra0","extra1","extra2","dodge","interact"})
    moved|=NocturnisInput.Held(key);
   moved|=Input.GetKey(KeyCode.UpArrow)||Input.GetKey(KeyCode.DownArrow)||Input.GetKey(KeyCode.LeftArrow)||Input.GetKey(KeyCode.RightArrow)||Input.GetMouseButton(0)||Input.GetMouseButton(1);
   if(moved){CancelRecall(true);return false;}
   if(Time.time-recallStart<recallDuration)return false;
   CompleteRecall();return true;
  }
  void CompleteRecall()
  {
   if(!recallActive || State!=Phase.Playing)return;
   bool paid=recallUsesScroll && recallScrolls>0;recallActive=false;
   if(paid){recallScrolls--;recallTraceZone=Zone;recallTracePosition=HeroPosition;}
   else recallTraceZone=-1;
   LoadZone(Campaign.Town);
   ShowNotice(paid ? "Oakhaven • Neme zachowała ślad Twojego powrotu" : "Oakhaven • zwoje kupisz u Neme lub kupca");SaveCheckpoint();
  }
  public bool ReturnAlongRecallTrace()
  {
   if(State!=Phase.Dialogue || Service!="waypoint" || Zone!=Campaign.Town || !HasRecallTrace)return false;
   int zone=recallTraceZone;Vector3 location=recallTracePosition;
   recallTraceZone=-1;Resume();LoadZone(zone);
   var cell=grid.NearestWalkable(Cell(location.x),Cell(location.z));Vector3 p=World(cell.X,cell.Z);
   if(grid.CanStand(location.x/Tile,location.z/Tile,.32f/Tile))p=location;
   player.Root.transform.position=p;hasDestination=false;target=null;
   View.transform.position=p+new Vector3(9,15,-11);View.transform.LookAt(p);
   SaveCheckpoint();ShowNotice("Powrót po śladzie • "+Campaign.Names[zone]);return true;
  }
  void OnRecallHeroDied()
  {
   CancelRecall();recallTraceZone=-1;
   var saved=ReadCheckpoint();if(saved==null || saved.recall==null)return;
   saved.recall.zone=-1;WriteCheckpoint(saved);
  }
  void RecallLootForEnemy(Enemy e)
  {if(e.boss || e.elite && (e.spawnId+Zone)%4==0)AddRecallScrolls(1);}
 }
 public sealed partial class KryptaHUD
 {
  void RecallHUD()
  {
   if(game.State!=KryptaGame.Phase.Playing || game.IntroActive)return;
   if(game.RecallActive)
   {
    StoryPanel(new Rect(437,370,406,76),0);Label(449,379,382,23,game.RecallCaption,center,gold);
    Bar(new Rect(455,410,370,7),game.RecallProgress,game.Hero.color);
    Label(449,424,382,18,"Ruch lub obrażenia przerywają. Zwój zużywa się po powrocie.",center,paper);
   }
   string count=game.RecallScrolls>0 ? game.RecallScrolls+" zwojów" : "modlitwa 8 s";
   if(Button(new Rect(945,535,310,28),"[T] POWRÓT • "+count))game.StartRecall();
  }
 }
}
