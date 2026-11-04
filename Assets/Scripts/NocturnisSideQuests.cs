using System;
using System.Collections.Generic;
using UnityEngine;
namespace Krypta
{
 public static class SideQuestData
 {
  public const int Count=5;
  public static readonly string[] Titles={"Trzynasta lampa","Żar pod popiołem","Woda dla żywych","Imiona wymazanych","Ostatni pochówek"};
  public static readonly string[] NPCs={"Irma • latarniczka","Hargrym Bezręki","Siostra Vespera","Malakor • ślepy kronikarz","Oren • grabarz"};
  public static readonly string[] Services={"irma","forge","alchemy","lore","grave"};
  public static readonly int[] Zones={Campaign.Chapel,Campaign.Cellar,Campaign.Cistern,Campaign.Ossuary,Campaign.Forest};
  public static readonly int[] Required={3,3,3,4,3};
  public static readonly int[] Gold={45,55,65,85,70},Materials={5,9,6,8,7},XP={60,70,80,100,80};
  public static readonly string[] Objects={"Oczyść zimną lampę","Odzyskaj żar z paleniska","Zamknij skażony dopływ","Odczytaj tablicę z imieniem","Zabierz znacznik zaginionego"};
  public static readonly string[] Directions={
   "Cmentarzysko → boczna Kaplica Trzynastej Lampy. Rozpocznij oczyszczanie każdej lampy, broń jej przez 12 sekund i pokonaj oba przywołane cienie, aby zgasić zimny ogień. Pozostań w promieniu siedmiu kroków. Przerwaną zmianą mapy obronę zaczynasz ponownie.",
   "Martwe Błonia → piwnica spalonej farmy. Ocal trzy naczynia żaru i dostarcz je do misy przy wejściu. Ciężar żaru spowalnia ruch; kowal przywróci pracę kuźni.",
   "Katakumby II → boczna Cysterna Czarnej Wody. Odetnij dopływy w kolejności I → III → II. Najpierw źródło, potem odpływ, na końcu zawór środkowy.",
   "Krypta Królów-Biskupów → Ossuarium Bez Imion. Odczytaj cztery tablice i pokonaj Strażnika Wymazanych.",
   "Las Ostatnich Szeptów → trzy znaczniki zaginionych. Obroń też Orena przed trzema falami na cmentarzu; wróć do grabarza."};
  public static readonly string[] Offers={
   "Rozpoznaję każdą lampę, którą zapalam. Trzynasta nie jest moja. Jej siostry ukryto w kaplicy przy cmentarzu. Dopóki płoną, każda ścieżka prowadzi zmarłych do naszych drzwi. Oczyść je: broń każdej lampy i odeprzyj jej cienie, aż zgaśnie zimny ogień.",
   "Pod spaloną farmą stoi dawna kuźnia. W trzech paleniskach ocalał żar gwiezdnego żelaza. Ocal go, a znów będę mógł kuć dla żywych. Zajrzyj w boczne komory; rabusie zawsze pomijają to, czego nie potrafią unieść.",
   "Woda w studni przybrała kolor mojej maski. Trzy dopływy pod drugim poziomem katakumb karmią miejską cysternę. Zakręć zawory, ale najpierw odsuń od nich to, co żeruje na skażeniu. Mam dość odtrutki tylko dla dzieci.",
   "Nie potrzebuję klejnotów. Potrzebuję imion. W ossuarium za kryptą biskupów zostały cztery tablice. Ich strażnik woli zabić żywego, niż pozwolić wspomnieć zmarłego. Przeczytaj je i ucisz go. Wtedy zapiszę prawdziwą historię miasta.",
   "Mogę zamknąć groby, jeśli osłonisz mnie przed trzema falami. Ale trzy rodziny nadal czekają na zaginionych w lesie. Przynieś ich znaczniki z korzeni. Nie obiecuj, że wrócą. Obiecaj, że nie znikną bez imienia."};
  public static readonly string[] Endings={
   "Dziś zapaliłam dwanaście lamp. I naliczyłam dwanaście cieni. Nie zapomnę, kto zgasił trzynastą.",
   "Żar znów odpowiada na młot. Weź ten pancerz; będzie pamiętał ogień, który ocaliłeś.",
   "Pierwsza czysta kropla spadła o świcie. Ulepszyłam twoje mikstury i napełniłam pas. Pozwól mi teraz zająć się pozostałymi.",
   "Cztery imiona wróciły do księgi. Biskupi mogą odebrać oddech, ale nie ostatnie słowo. Ta pieczęć należała do człowieka, którego właśnie przywróciłeś pamięci.",
   "Wszystkie groby zamknięte. Wszystkie nazwane. Pierwszy raz od tygodnia odkładam łopatę. Zatrzymaj ich bursztyn — oni nie potrzebują już światła."};
 }
 public sealed partial class KryptaGame
 {
  sealed class QuestMarker {public int quest,index;public Vector3 p;public GameObject root;}
  readonly List<QuestMarker> questMarkers=new List<QuestMarker>();
  int sideAccepted,sideClaimed,clearRewarded;int[] sideObjects=new int[SideQuestData.Count];
  string sideReturnService="",sideReturnName="",sideReturnCopy="";
  static readonly int[,] QuestCells={{7,9},{17,23},{10,34},{19,32}};
  public int CompletedSideQuests {get{int n=0;for(int q=0;q<SideQuestData.Count;q++)if((sideClaimed&(1<<q))!=0)n++;return n;}}
  public int SideQuestForService(string service){return Array.IndexOf(SideQuestData.Services,service);}
  public int ActiveSideQuest {get{int q;return Service.StartsWith("sidequest:") && int.TryParse(Service.Substring(10),out q) && q>=0 && q<SideQuestData.Count ? q : -1;}}
  public int SideQuestObjectCount(int q){if(q<0 || q>=SideQuestData.Count)return 0;int n=0;for(int i=0;i<SideQuestData.Required[q];i++)if((sideObjects[q]&(1<<i))!=0)n++;return n;}
  bool GraveDefenseComplete {get{return (slain[Campaign.Cemetery]&0x7fff0)==0x7fff0 || (Cleared&(1<<Campaign.Cemetery))!=0;}}
  bool OssuaryGuardianDead {get{return (slain[Campaign.Ossuary]&(1<<19))!=0 || (Cleared&(1<<Campaign.Ossuary))!=0;}}
  public int SideQuestState(int q)
  {if(q<0 || q>=SideQuestData.Count)return -1;if((sideClaimed&(1<<q))!=0)return 3;if((sideAccepted&(1<<q))==0)return 0;return SideQuestObjectCount(q)>=SideQuestData.Required[q] && (q!=1 || (sideObjects[1]&256)!=0) && (q!=3 || OssuaryGuardianDead) && (q!=4 || GraveDefenseComplete) ? 2 : 1;}
  public string SideQuestProgress(int q)
  {
   if(SideQuestState(q)==3)return "Zakończone • nagroda odebrana";
   if(SideQuestState(q)==0)return "Porozmawiaj: "+SideQuestData.NPCs[q]+(q==4 ? " • Cmentarzysko" : " • Oakhaven");
   if(q==1 && SideQuestObjectCount(1)==3 && (sideObjects[1]&256)==0)return "Zanieś żar do misy przy wejściu do piwnicy [E]";
   string s=SideQuestData.Objects[q]+": "+SideQuestObjectCount(q)+" / "+SideQuestData.Required[q];
   if(q==3)s+=" • strażnik: "+(OssuaryGuardianDead ? "pokonany" : "żyje");
   if(q==4)s+=" • obrona grobów: "+(GraveDefenseComplete ? "ukończona" : "trzy fale");
   return SideQuestState(q)==2 ? "Wróć po nagrodę: "+SideQuestData.NPCs[q] : s;
  }
  public string SideQuestReward(int q)
  {return SideQuestData.Gold[q]+" zł • "+SideQuestData.Materials[q]+" odłamków • "+SideQuestData.XP[q]+" PD"+(q==1 ? " • pancerz +2" : q==2 ? " • ulepszone mikstury" : q==3 ? " • pieczęć +3" : q==4 ? " • bursztyn" : "");}
  public bool OpenSideQuest(int q)
  {
   if(q<0 || q>=SideQuestData.Count || Service!=SideQuestData.Services[q] || State!=Phase.Dialogue)return false;
   sideReturnService=Service;sideReturnName=DialogueName;sideReturnCopy=DialogueText;
   Service="sidequest:"+q;DialogueName=SideQuestData.NPCs[q];DialogueText=SideQuestState(q)==3 ? SideQuestData.Endings[q] : SideQuestData.Offers[q];return true;
  }
  public void ReturnFromSideQuest(){if(ActiveSideQuest<0)return;Service=sideReturnService;DialogueName=sideReturnName;DialogueText=sideReturnCopy;}
  public bool AcceptSideQuest(int q)
  {
   if(ActiveSideQuest!=q || State!=Phase.Dialogue || SideQuestState(q)!=0)return false;
   sideAccepted|=1<<q;OnJournalQuestAccepted(q);SaveCheckpoint();ShowNotice("Nowe zadanie: "+SideQuestData.Titles[q]+" • [J] dziennik");Sound("shrine");return true;
  }
  public bool ClaimSideQuest(int q)
  {
   if(ActiveSideQuest!=q || State!=Phase.Dialogue || SideQuestState(q)!=2)return false;
   sideClaimed|=1<<q;Coins+=SideQuestData.Gold[q];Materials+=SideQuestData.Materials[q];Experience+=SideQuestData.XP[q];
   if(q==1)SetGear(1,Mathf.Max(2,ArmorRank));if(q==3)SetGear(2,Mathf.Max(3,CharmRank));
   if(q==2){PotionRank=Mathf.Min(3,PotionRank+1);Potions=5;}if(q==4)GemBag[0]=Mathf.Min(99,GemBag[0]+1);
   while(Experience>=Level*60){Experience-=Level*60;Level++;Health=MaxHealth;}
   DialogueText=SideQuestData.Endings[q];OnJournalQuestClaimed(q);SaveCheckpoint();Sound("shrine");ShowNotice("Ukończono: "+SideQuestData.Titles[q]+" • "+SideQuestReward(q));return true;
  }
  void ResetSideQuests(){ResetQuestJournal();sideAccepted=sideClaimed=clearRewarded=0;sideObjects=new int[SideQuestData.Count];questMarkers.Clear();ResetStageEvents();}
  void RestoreSideQuests(int accepted,int claimed,int[] objects,int rewards)
  {
   sideAccepted=(accepted|claimed)&31;sideClaimed=claimed&31;clearRewarded=rewards&((1<<Campaign.Count)-1);sideObjects=new int[SideQuestData.Count];
   for(int q=0;q<SideQuestData.Count;q++)if(objects!=null && q<objects.Length)sideObjects[q]=objects[q]&(((1<<SideQuestData.Required[q])-1)|(q==1 ? 256 : 0));
  }
  void BuildSideQuestWorld()
  {
   questMarkers.Clear();
   for(int q=0;q<SideQuestData.Count;q++)if(SideQuestData.Zones[q]==Zone)
    for(int i=0;i<SideQuestData.Required[q];i++)
    {
     if((sideObjects[q]&(1<<i))!=0)continue;var cell=grid.NearestWalkable(QuestCells[i,0],QuestCells[i,1]);Vector3 p=World(cell.X,cell.Z);
     var root=new GameObject("Zadanie: "+SideQuestData.Titles[q]+" / "+(i+1));root.transform.SetParent(world);root.transform.position=p;
     if(q==0){Place(art.torch,p,.9f,false,root.transform);var glow=GameObject.CreatePrimitive(PrimitiveType.Sphere);glow.name="Zimny płomień lampy";glow.transform.SetParent(root.transform,false);glow.transform.localPosition=Vector3.up*1.15f;glow.transform.localScale=new Vector3(.19f,.36f,.19f);glow.GetComponent<Renderer>().sharedMaterial=art.tealMaterial;Destroy(glow.GetComponent<Collider>());}
     else if(q==1)Place(art.chest,p,1,false,root.transform);
     else if(q==2){var pipe=Cube("Zawór cysterny",p+Vector3.up*.4f,new Vector3(.5f,.8f,.5f),art.wallMaterial);pipe.transform.SetParent(root.transform);var valve=Ring(root.transform,.45f,.1f,art.tealMaterial);valve.transform.localPosition=Vector3.up*.9f;}
     else{var tablet=Cube(q==3 ? "Tablica imienna" : "Znacznik pielgrzyma",p+Vector3.up*.65f,new Vector3(.75f,1.3f,.18f),art.wallMaterial);tablet.transform.SetParent(root.transform);}
     var ring=Ring(root.transform,.7f,.055f,art.goldMaterial);ring.transform.localPosition=Vector3.up*.07f;
     questMarkers.Add(new QuestMarker{quest=q,index=i,p=p,root=root});
    }
   BuildStageEventWorld();BuildSideMechanicProps();
  }
  QuestMarker NearbyQuestMarker()
  {QuestMarker best=null;float distance=2.5f;foreach(var m in questMarkers){float d=Vector3.Distance(HeroPosition,m.p);if(d<distance && m.root && m.root.activeSelf){distance=d;best=m;}}return best;}
  bool QuestPropNearby(Vector3 p,float radius)
  {
   if(StagePropNearby(p,radius))return true;
   if(Zone==Campaign.Cellar && Vector3.Distance(p,EmberBasin)<radius+.8f)return true;
   // Reserve collected markers too, so seeded destructibles never move on a later visit.
   for(int q=0;q<SideQuestData.Count;q++)if(SideQuestData.Zones[q]==Zone)
    for(int i=0;i<SideQuestData.Required[q];i++){var cell=grid.NearestWalkable(QuestCells[i,0],QuestCells[i,1]);if(Vector3.Distance(World(cell.X,cell.Z),p)<radius)return true;}
   return Zone==Campaign.Fields && (Vector3.Distance(p,World(3,20))<radius || Vector3.Distance(p,World(20,15))<radius) || Zone==Campaign.Cemetery && Vector3.Distance(p,World(19,16))<radius || Zone==Campaign.Crypt2 && Vector3.Distance(p,World(3,22))<radius || Zone==Campaign.Kings && Vector3.Distance(p,World(20,16))<radius;
  }
  string QuestMarkerInteraction()
  {if(CarryingEmber && Vector3.Distance(HeroPosition,EmberBasin)<2.8f)return "E • Zabezpiecz żar w misie";string stage=StageMarkerInteraction();if(stage!=null)return stage;var m=NearbyQuestMarker();if(m==null)return null;return SideQuestState(m.quest)==0 ? "Zlecenie: "+SideQuestData.NPCs[m.quest]+" • [J]" : "E • "+SideQuestData.Objects[m.quest]+(m.quest==2 ? " ["+(m.index==0 ? "I" : m.index==1 ? "II" : "III")+"]" : "")+" ("+(SideQuestObjectCount(m.quest)+1)+"/"+SideQuestData.Required[m.quest]+")";}
  bool InteractQuestMarker()
  {
   if(DeliverEmbers() || InteractStageMarker())return true;
   var m=NearbyQuestMarker();if(m==null)return false;
   if(SideQuestState(m.quest)==0){ShowNotice("Najpierw przyjmij zadanie: "+SideQuestData.NPCs[m.quest]);return true;}
   if((sideObjects[m.quest]&(1<<m.index))!=0)return true;
   if(enemies.Exists(e=>e.hp>0 && Vector3.Distance(e.Position,m.p)<8 && Visible(e.Position,m.p))){ShowNotice("Najpierw zabezpiecz komnatę • wrogowie są zbyt blisko");return true;}
   if(HandleSideMechanic(m))return true;CompleteQuestMarker(m);return true;
  }
  void RewardFullClear()
  {
   if(Zone==Campaign.Town || Remaining!=0 || !CanExit || (clearRewarded&(1<<Zone))!=0)return;
   clearRewarded|=1<<Zone;Cleared|=1<<Zone;int reward=10+CampaignDepth*3;Coins+=reward;Materials+=3;SaveCheckpoint();
   ShowNotice("Lokacja oczyszczona • +"+reward+" zł, +3 odłamki • przejście otwarte");
  }
 }
}
