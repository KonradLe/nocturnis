using UnityEngine;
using System.Collections.Generic;
namespace Krypta
{
 public static class StageEventData
 {
  public static readonly int[,] Cells={{7,9},{17,23},{10,34}};
  public static readonly string[] Actions={"Zapal sygnał","Zerwij łańcuch","Zamknij kryptę","Wyłącz palenisko","Usuń pieczęć serca"};
  public static readonly string[] Summaries={"Przywróć trzy sygnały w wąwozie","Uwolnij trzy skrępowane cienie","Zamknij trzy śpiewające krypty","Wygasz trzy paleniska kuźni","Zerwij trzy przysięgi chroniące serce"};
  public static readonly string[] NPCs={"Leona • zwiadowczyni","Aster • ostatni nowicjusz","Tomasz • strażnik ciszy","Ragna • wygaszaczka","Eliasz • odstępca"};
  public static readonly string[] Introductions={
   "Na trzech rozwidleniach stoją kamienne sygnały. Przy każdym czatuje łowca, który strąca ludzi w mgłę. Pokonaj strażnika, zabezpiecz platformę i zapal ogień [E]. Dopiero trzy światła pokażą bezpieczne wyjście. Na końcu czeka Przewodnik Zgaszonych.",
   "Nie wszystkie cienie służą biskupowi. Trzy z nich przywiązano do ruin własnymi przysięgami. Pokonaj nadzorców i zerwij każdy łańcuch [E]. Gdy ostatni cień odejdzie, brama przestanie domagać się kolejnej ofiary. Jej Opiekun Łańcuchów nie odejdzie dobrowolnie.",
   "Słyszysz śpiew pod posadzką? To nie modlitwa. Trzy otwarte krypty wołają umarłych do galerii. Najpierw ucisz ich strażników, potem dociśnij każdą kamienną pieczęć [E]. Nie zostawiaj za sobą śpiewu. Dzwonnik Bez Twarzy pilnuje ostatniego przejścia.",
   "Kuźnia wyrabia kości do nowego ciała Varkosa. Trzy paleniska zasilają piec pod bramą. Zwykła woda tylko rozbudza ten żar. Zabij palaczy, oczyść komory i zamknij zawory [E]. Mistrz Czarnego Młota strzeże głównego kanału.",
   "Byłem przy składaniu tych przysiąg. Każda z trzech pieczęci chroni serce kosztem jednego imienia. Pokonaj nosicieli i zerwij pieczęcie [E]. Wtedy Malazar będzie mógł krwawić. Ten, który stoi przy ostatnich drzwiach, zna już twoje imię."};
  public static readonly string[] GuardNames={"Przewodnik Zgaszonych","Opiekun Łańcuchów","Dzwonnik Bez Twarzy","Mistrz Czarnego Młota","Nosiciel Ostatniej Przysięgi"};
  public static readonly string[] Completion={"Trzy ognie przecinają mgłę. Ocalała karawana zna już drogę.","Łańcuchy opadły. Krużganek po raz pierwszy nie powtarza rozkazów biskupa.","Ostatnia krypta zamilkła. Przejście pod galerią przestało oddychać.","Paleniska zgasły. Żebra nowego boga ostygły, zanim zdołały objąć serce.","Trzy przysięgi zerwane. Za drzwiami po raz pierwszy słychać nierówny oddech Malazara."};
  public static int Index(int zone){return zone-Campaign.Ravine;}
 }
 public sealed partial class KryptaGame
 {
  sealed class StageMarker {public int index;public Vector3 p;public GameObject root,glow;}
  readonly List<StageMarker> stageMarkers=new List<StageMarker>();
  int[] stageEvents=new int[Campaign.Count];int stageRewarded;
  public int StageObjectiveCount {get{if(!Campaign.EventStage(Zone))return 0;int n=0;for(int i=0;i<3;i++)if((stageEvents[Zone]&(1<<i))!=0)n++;return n;}}
  public bool StageObjectivesComplete {get{return !Campaign.EventStage(Zone) || (stageEvents[Zone]&7)==7;}}
  public string StageProgress {get{return !Campaign.EventStage(Zone) ? "" : StageEventData.Summaries[StageEventData.Index(Zone)]+" • "+StageObjectiveCount+" / 3";}}
  public string StageDirections {get{return !Campaign.EventStage(Zone) ? "" : "Zabezpiecz trzy oznaczone miejsca i wykonaj przy nich akcję [E]. Strażnik miejsca musi zostać pokonany. Następnie pokonaj strażnika bramy i przejdź dalej.";}}
  public Vector3 ActiveStageObjectivePosition
  {get{foreach(var m in stageMarkers)if((stageEvents[Zone]&(1<<m.index))==0)return m.p;return exit;}}
  void ResetStageEvents(){stageEvents=new int[Campaign.Count];stageRewarded=0;stageMarkers.Clear();}
  void RestoreStageEvents(int[] progress,int rewarded)
  {stageEvents=new int[Campaign.Count];for(int z=Campaign.Ravine;z<Campaign.Count;z++)if(progress!=null && z<progress.Length)stageEvents[z]=progress[z]&7;stageRewarded=rewarded&((1<<Campaign.Count)-1);}
  void BuildStageEventWorld()
  {
   stageMarkers.Clear();if(!Campaign.EventStage(Zone))return;int stage=StageEventData.Index(Zone);
   for(int i=0;i<3;i++)
   {
    var cell=grid.NearestWalkable(StageEventData.Cells[i,0],StageEventData.Cells[i,1]);Vector3 p=World(cell.X,cell.Z);
    var root=new GameObject("Cel etapu: "+StageEventData.Actions[stage]+" "+(i+1));root.transform.SetParent(world);root.transform.position=p;
    if(Zone==Campaign.Ravine)Place(art.torch,p,1.4f,false,root.transform);
    else if(Zone==Campaign.Cloister)
    {
     var pole=Cube("Słup przysięgi",p+Vector3.up*.9f,new Vector3(.28f,1.8f,.28f),art.wallMaterial);pole.transform.SetParent(root.transform);
     for(int link=0;link<5;link++){var chain=Ring(root.transform,.12f,.04f,art.wallMaterial);chain.transform.localPosition=new Vector3((link%2)*.08f,.3f+link*.25f,0);chain.transform.localRotation=Quaternion.Euler(90,link%2*90,0);}
    }
    else if(Zone==Campaign.Galleries){var lid=Cube("Kamienna pieczęć krypty",p+Vector3.up*.3f,new Vector3(1.2f,.6f,1.5f),art.wallMaterial);lid.transform.SetParent(root.transform);}
    else if(Zone==Campaign.Forge){var furnace=Cube("Palenisko serca",p+Vector3.up*.5f,new Vector3(1.25f,1,.8f),art.wallMaterial);furnace.transform.SetParent(root.transform);}
    else{var altar=Cube("Ołtarz przysięgi",p+Vector3.up*.55f,new Vector3(.8f,1.1f,.8f),art.wallMaterial);altar.transform.SetParent(root.transform);}
    var glow=GameObject.CreatePrimitive(PrimitiveType.Sphere);glow.name="Rdzeń celu etapu";glow.transform.SetParent(root.transform,false);glow.transform.localPosition=Vector3.up*1.35f;glow.transform.localScale=Vector3.one*.27f;Destroy(glow.GetComponent<Collider>());
    Ring(root.transform,.85f,.06f,art.goldMaterial);var marker=new StageMarker{index=i,p=p,root=root,glow=glow};stageMarkers.Add(marker);RefreshStageMarker(marker);
   }
   var npc=grid.NearestWalkable(6,5);AddCitizen(npc.X,npc.Z,stage%5,StageEventData.NPCs[stage],"stage",StageEventData.Introductions[stage]);
  }
  void RefreshStageMarker(StageMarker marker)
  {
   bool done=(stageEvents[Zone]&(1<<marker.index))!=0;marker.glow.GetComponent<Renderer>().sharedMaterial=done ? art.greenMaterial : Zone==Campaign.Forge ? art.redMaterial : art.violetMaterial;
   marker.glow.transform.localScale=Vector3.one*(done ? .16f : .27f);
  }
  bool StagePropNearby(Vector3 p,float radius)
  {if(!Campaign.EventStage(Zone))return false;for(int i=0;i<3;i++){var cell=grid.NearestWalkable(StageEventData.Cells[i,0],StageEventData.Cells[i,1]);if(Vector3.Distance(p,World(cell.X,cell.Z))<radius)return true;}return false;}
  StageMarker NearbyStageMarker()
  {StageMarker best=null;float distance=2.5f;foreach(var marker in stageMarkers){float d=Vector3.Distance(HeroPosition,marker.p);if(d<distance){distance=d;best=marker;}}return best;}
  string StageMarkerInteraction()
  {var marker=NearbyStageMarker();if(marker==null)return null;return (stageEvents[Zone]&(1<<marker.index))!=0 ? "Cel zabezpieczony • "+StageObjectiveCount+" / 3" : "E • "+StageEventData.Actions[StageEventData.Index(Zone)]+" "+(marker.index+1)+" / 3";}
  bool InteractStageMarker()
  {
   var marker=NearbyStageMarker();if(marker==null)return false;
   if((stageEvents[Zone]&(1<<marker.index))!=0){ShowNotice("Ten cel jest już zabezpieczony • "+StageProgress);return true;}
   if((slain[Zone]&(1<<(22+marker.index)))==0){ShowNotice("Najpierw pokonaj strażnika tego miejsca");return true;}
   if(enemies.Exists(e=>e.hp>0 && Vector3.Distance(e.Position,marker.p)<8 && Visible(e.Position,marker.p))){ShowNotice("Zabezpiecz miejsce • pozostali przeciwnicy są zbyt blisko");return true;}
   stageEvents[Zone]|=1<<marker.index;RefreshStageMarker(marker);Burst(marker.p,1.4f,art.goldMaterial);Sound("shrine");
   if(StageObjectivesComplete && (stageRewarded&(1<<Zone))==0)
   {
    stageRewarded|=1<<Zone;int gold=30+StageEventData.Index(Zone)*5;Coins+=gold;Materials+=6;
    ShowNotice(StageEventData.Completion[StageEventData.Index(Zone)]+" +"+gold+" zł, +6 odłamków.");
   }
   else ShowNotice(StageProgress);
   SaveCheckpoint();return true;
  }
 }
}
