using System.Collections.Generic;
using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaGame
 {
  sealed class Citizen { public string name,service,copy;public Vector3 p;public ActorVisual actor; }
  readonly List<Citizen> citizens=new List<Citizen>();
  readonly List<GameObject> arenaColumns=new List<GameObject>();
  public int Unlocked {get;private set;}=1<<Campaign.Road;
  public int Cleared {get;private set;}
  public int PotionRank {get;private set;}
  public int Materials {get;private set;}
  public int Gems {get;private set;}
  public int EdraChoice {get;private set;}
  public bool BookOpen {get;private set;}
  public string Service {get;private set;}="";
  public float Shadow {get;private set;}=60;
  public bool ReducedFlashes {get;set;}=true;
  public float AttackPower {get{return Hero.damage+(Level-1)*4+WeaponRank*5+Gems*2+EnchantBonus(1);}}
  public float Regeneration {get{return CharmRank*.12f+EnchantBonus(3);}}
  public int CampaignDepth {get{int z=Campaign.SideBranch(Zone) ? Campaign.Next(Zone) : Zone;int i=System.Array.IndexOf(Campaign.Route,z);return Mathf.Max(0,i-1);}}
  public Vector3 SpawnPosition {get{return Zone==0 ? World(11,5) : Zone==Campaign.Road || Zone==Campaign.Heart || Zone==Campaign.Cemetery ? World(12,3) : World(5,3);}}
  public Vector3 ExitPosition {get{return Zone==0 ? World(20,10) : Zone==Campaign.Road || Zone==Campaign.Heart ? World(12,37) : World(17,38);}}
  public string CampaignObjective {get{
   if(Zone==0)return QuestAccepted ? "Brama Błoni [E] • mieszkańcy oferują usługi" : "Porozmawiaj z Malakorem przy Namiocie Reliktów [E]";
   if(Zone==Campaign.Cemetery && graveWaves<3)return "Obroń grabarza • fala "+Mathf.Min(3,graveWaves+1)+" / 3";
   if(Campaign.EventStage(Zone) && !StageObjectivesComplete)return StageProgress;
   if(Remaining>0)return CanExit ? "Strażnik pokonany • przejście otwarte; pozostało "+Remaining : Zone==Campaign.Road ? "Przeżyj trakt • pokonaj Nadzorcę" : Zone==Campaign.Heart ? "Przerwij rytuał Malazara" : "Znajdź strażnika przejścia • pozostali: "+Remaining;
   return Zone==Campaign.Heart ? "Odzyskaj odłamek Varkosa [E]" : "Przejdź dalej przy złotej pieczęci [E]";
  }}
  int[] slain=new int[Campaign.Count];
  int graveWaves;float waveAt,trapAt,regenAt,bossRitualAt;
  GameObject drawbridge,heart;
  bool bridgeLowered;
  readonly float[] extraAt=new float[3];
  static readonly string[,] ExtraNames={
   {"Krąg pokuty","Łańcuch wyroku","Sąd popiołów"},
   {"Pieczęć transfuzji","Krwawy splot","Morze posoki"},
   {"Sokole oko","Strzała pęt","Zaćmienie"},
   {"Studnia entropii","Pętla czasu","Martwa gwiazda"},
   {"Totem odnowy","Ciernisty uścisk","Przebudzenie dębu"}
  };
  public string ExtraName(int i){return ExtraNames[SelectedHero,i];}
  public float ExtraReady(int i){return Mathf.Max(0,extraAt[i]-Time.time);}
  void ResetCampaign(){ResetTalents();ResetRecall();ResetMarket();ResetShrineUsage();ResetExploration();ResetWorldLoot();ResetCombatActions();ResetSideQuests();ResetBreakablesProgress();Unlocked=1<<Campaign.Road;Cleared=0;slain=new int[Campaign.Count];EdraChoice=PotionRank=Materials=Gems=0;QuestAccepted=false;Level=1;Experience=Coins=Defeated=0;Potions=3;WeaponRank=ArmorRank=CharmRank=0;Health=MaxHealth;Resource=50;Shadow=60;Elapsed=0;Inventory.Clear();ResetArtisans();ResetRelicGear();}
  void ResetZoneEvents(){citizens.Clear();arenaColumns.Clear();graveWaves=GraveDefenseComplete ? 3 : 0;waveAt=0;trapAt=Time.time+4;bossRitualAt=Time.time+8;drawbridge=null;heart=null;bridgeLowered=false;}
  void RecordKill(Enemy e){if(e.spawnId<30)slain[Zone]|=1<<e.spawnId;Materials++;if(Zone==Campaign.Road && e.boss){SetGear(0,Mathf.Max(2,WeaponRank));ShowNotice("Rzadka broń klasowa zdobyta • most do Oakhaven otwarty");}}
  void HideDefeated(){foreach(var e in enemies)if(e.spawnId<30 && (slain[Zone]&(1<<e.spawnId))!=0){e.hp=0;e.rewarded=true;e.visual.Root.SetActive(false);}}
  public bool ZoneUnlocked(int z){return z>=0 && z<Campaign.Count && (Unlocked&(1<<z))!=0;}
  public void Travel(int z){if(Service!="waypoint" || !ZoneUnlocked(z))return;Resume();LoadZone(z);}
  public void ReturnToTown(){StartRecall();}
  public void OpenBook(){Pause();TalentsOpen=EquipmentOpen=JournalOpen=false;Service="";BookOpen=true;Sound("triptych");}
  public void SetRune(int r){if(!BookOpen)return;Rune=Mathf.Clamp(r,0,1);SaveCheckpoint();Sound("shrine");}
  void Talk(string name,string copy,string service=""){DialogueName=name;DialogueText=copy;Service=service;State=Phase.Dialogue;Time.timeScale=0;Sound("shrine");}
  void AddCitizen(int x,int z,int hero,string name,string service,string copy)
  {
   var h=IntegratedCitizen(service,art.heroes[hero]);var a=new ActorVisual(h.model,h.animations,World(x,z),1.7f,name);a.Root.transform.SetParent(world);Ring(a.Root.transform,.6f,.04f,art.goldMaterial);citizens.Add(new Citizen{name=name,service=service,copy=copy,p=World(x,z),actor=a});
  }
  void BuildTownsfolk()
  {
   AddCitizen(11,8,3,"Irma • latarniczka","irma","Wczoraj zostawiłam dwanaście lamp. Rano wróciło trzynastu pielgrzymów. Żaden nie oddychał.\n\nMalazar otworzył groby pod katedrą Świętego Solariusa. Porozmawiaj z Malakorem przy Namiocie Reliktów. Potem idź przez Martwe Błonia. Serce pod katedrą nie może bić dla niego.");
   AddCitizen(8,6,0,"Hargrym Bezręki","forge","Moja ręka działa na żarze. Twoje ostrze też może. Rozbiorę stare relikty, a z ich odłamków wykuję silniejsze.\n\nKuźnia Upadłej Gwiazdy • wzmacnianie broni, pancerza i pieczęci.");
   Cube("Meteorytowe kowadło",World(7,7)+Vector3.up*.6f,new Vector3(1.6f,1.2f,.8f),art.wallMaterial);Flame(World(7,7)+Vector3.up*1.3f,.2f);
   AddCitizen(15,8,3,"Malakor • ślepy kronikarz","lore","Widzę lepiej odkąd zgasły moje oczy. Malazar chce nakarmić serce Varkosa krwią wiernych.\n\nPowierzam Ci wyprawę: odszukaj odłamek serca i powstrzymaj Malazara. Brama Martwych Błoni jest już dla Ciebie otwarta. Przejdź przez wąwóz i cmentarz, potem katakumby i krypty biskupów.\n\nW drodze wracaj z niezidentyfikowanymi reliktami; odczytam ich historię.");
   Place(art.tent,World(15,10),2.6f,false,world);
   AddCitizen(8,13,1,"Siostra Vespera","alchemy","Nie zdejmuję maski. Zapach Ichoritu zabija wcześniej niż jego dotyk.\n\nUzupełnię mikstury, połączę i wyjmę klejnoty. Zmieniam też zaklęcia oręża, pancerza i pieczęci.");
   AddCitizen(18,10,2,"Eryk • handlarz reliktów","market","Broń, pancerze i pieczęcie z wypraw. Kupisz tu wyposażenie i Zwoje Powrotu. Odkup zachowuję do opuszczenia miasta.");
   AddCitizen(15,13,2,"Neme • kartografka","waypoint","Pęknięte Astrolabium pamięta tylko miejsca, do których dotarłeś. Wybierz odkryty punkt szlaku.");
   var ring=Ring(world,1.3f,.12f,art.violetMaterial);ring.transform.position=World(12,13);Ring(ring.transform,.9f,.06f,art.goldMaterial);
   AddCitizen(7,13,4,"Ołtarz szat","wardrobe","Wybierz barwy pielgrzyma. Barwienie nie zmienia statystyk pancerza.");
   AddCitizen(7,10,4,"Żelazny kufer","stash","Wspólny kufer tego urządzenia. Pozostaw relikty następnemu pielgrzymowi.");Place(art.chest,World(6,10),1.2f,false,world);BuildQuestConsequences();
  }
  void BuildEncounter()
  {
   if(Zone==Campaign.Road)
   {
    int[,] road={{13,8},{11,12},{12,16},{12,18},{10,25},{15,27},{12,26},{10,28},{14,29}};
    for(int i=0;i<9;i++)SpawnEncounterAt(road[i,0],road[i,1],i<3 ? 0 : i%3==0 ? 1 : 3,false,"Zgubiony na trakcie");
    SpawnEncounterAt(12,30,8,false,"NADZORCA GNILCÓW",true);Torch(World(10,20));return;
   }
   if(Zone==Campaign.Heart)
   {
    SpawnEnemy(12,27,false,true);
    for(int i=0;i<8;i++)SpawnEncounterAt(i%2==0 ? 8 : 16,10+(i/2)*4,i%3==0 ? 2 : 9,i==7,i==7 ? "Herold ostatniego rytuału" : "Sługa Pękniętego Serca");return;
   }
   if(Zone==Campaign.Cemetery)
   {
    SpawnEnemy(4,5,false,false,true,4,"Widmo pierwszego grobowca");SpawnEnemy(20,5,false,false,true,4,"Widmo drugiego grobowca");SpawnEnemy(4,33,false,false,true,4,"Widmo trzeciego grobowca");SpawnEnemy(20,34,true,false,false,0,"Hiena cmentarna");
    AddCitizen(12,20,4,"Oren • grabarz","grave","Nie chcę złota. Daj mi tylko czas zamknąć te groby. Nadchodzą trzy fale. Nie opuszczę bramy.");AddShrine(11,15,"renewal","Źródło Życia",art.greenMaterial);return;
   }
   int[,] positions={{3,7},{7,8},{5,9},{4,13},{8,14},{3,16},{10,16},{6,21},{13,23},{10,24},{5,25},{16,26},{12,30},{18,31},{21,34},{15,35},{17,36},{9,33},{19,27},{5,30},{14,36},{18,37}};
   int count=Campaign.EncounterBudget(Zone);
   for(int i=0;i<count;i++)
   {
    bool guardian=i==count-1,siteGuardian=Campaign.EventStage(Zone) && i>=22 && i<=24;int type=Campaign.EventStage(Zone) && (guardian || siteGuardian) ? new[]{8,7,7,10,11}[StageEventData.Index(Zone)] : guardian ? (Zone==Campaign.Forest ? 11 : Zone==Campaign.Cellar ? 8 : Zone==Campaign.Cistern ? 5 : Zone==Campaign.Chapel ? 10 : 6) : EncounterArchetype(i);string name=EncounterName(type);
    if(guardian)name=Zone==Campaign.Ossuary ? "Strażnik Wymazanych" : Zone==Campaign.Forest ? "Dławiciel Korzeni" : Zone==Campaign.Cellar ? "Pożeracz Żaru" : Zone==Campaign.Chapel ? "Dzwonnik Trzynastej Lampy" : Zone==Campaign.Cistern ? "Nadzorca Czarnej Wody" : "Strażnik przejścia • "+name;
    if(Campaign.EventStage(Zone) && guardian)name=StageEventData.GuardNames[StageEventData.Index(Zone)];
    if(siteGuardian)name="Strażnik miejsca "+(i-21)+" • "+StageEventData.Actions[StageEventData.Index(Zone)];
    int x=guardian ? 17 : siteGuardian ? StageEventData.Cells[i-22,0]+1 : Zone==Campaign.Cellar && i==11 ? 15 : positions[i,0],z=guardian ? 35 : siteGuardian ? StageEventData.Cells[i-22,1]+1 : Zone==Campaign.Cellar && i==11 ? 35 : positions[i,1];
    SpawnEncounterAt(x,z,type,guardian || siteGuardian || i==6 || i==12,name);
   }
   foreach(var p in new[]{World(2,2),World(8,8),World(2,15),World(14,23),World(9,30),World(22,38)})Torch(p);
   var slaughterCell=grid.NearestWalkable(7,6);AddShrine(slaughterCell.X,slaughterCell.Z,"slaughter","Pylon Rzezi",art.redMaterial);var graceCell=grid.NearestWalkable(Zone==Campaign.Crypt2 ? 9 : 3,Zone==Campaign.Crypt2 ? 20 : 22);AddShrine(graceCell.X,graceCell.Z,"grace","Kapliczka Upadłych",art.goldMaterial);var renewalCell=grid.NearestWalkable(21,30);AddShrine(renewalCell.X,renewalCell.Z,"renewal","Źródło Życia",art.greenMaterial);
   if(Zone==Campaign.Forest)AddCitizen(20,35,4,"Edra • pod korzeniami","edra","Korzenie trzymają moje serce. Możesz przeciąć splot i mnie uwolnić albo oczyścić drzewo. Wybierz, czy ocalisz wędrowca, czy pamięć lasu.");
  }
  Enemy SpawnEncounterAt(int x,int z,int type,bool elite,string name,bool boss=false)
  {var p=grid.NearestWalkable(x,z);return SpawnEnemy(p.X,p.Z,type==0 || type==3 || type==5,boss,elite,type,name);}
  int EncounterArchetype(int i)
  {
   if(Zone==Campaign.Ravine)return new[]{3,1,0,9,3,4}[i%6];
   if(Zone==Campaign.Cloister)return new[]{6,2,0,4,7,1}[i%6];
   if(Zone==Campaign.Galleries)return new[]{0,1,4,10,6,7}[i%6];
   if(Zone==Campaign.Forge)return new[]{5,9,6,10,2,0}[i%6];
   if(Zone==Campaign.Threshold)return new[]{11,7,4,6,10,2}[i%6];
   if(Zone==Campaign.Fields)return new[]{0,1,3,2,0,3}[i%6];
   if(Zone==Campaign.Forest)return new[]{3,4,0,11,3,2}[i%6];
   if(Zone==Campaign.Kings || Zone==Campaign.Ossuary)return new[]{6,1,7,6,10,0}[i%6];
   if(Zone==Campaign.Chapel)return new[]{4,1,2,10,0,4}[i%6];
   if(Zone==Campaign.Cistern)return new[]{5,0,3,2,5,9}[i%6];
   if(Zone==Campaign.Crypt2 || Zone==Campaign.Crypt3)return new[]{5,2,10,9,0,6}[i%6];
   if(Zone==Campaign.Cellar)return new[]{0,3,5,0,2,9}[i%6];
   return new[]{0,1,4,6,0,2}[i%6];
  }
  string EncounterName(int type)
  {return new[]{"Wygłodniały pokutnik","Szkielet łucznik","Kultysta krwi","Zmutowany ogar","Widmo pielgrzyma","Plujnik Ichoritu","Tarczownik biskupa","Mroczny kapłan","Nadzorca gnilców","Fanatyk ofiarny","Kamienny gargulec","Demon korzeni"}[Mathf.Clamp(type,0,11)];}
  void UpdateCampaignInteraction()
  {
   Interaction=null;Vector3 p=HeroPosition;
   var nearby=NearbyCitizen(p);if(nearby!=null){Interaction="E • "+nearby.name;return;}
   foreach(var s in shrines)if(!s.used && Vector3.Distance(p,s.pos)<2.6f){Interaction="E • "+s.name;return;}
   string questHint=QuestMarkerInteraction();if(questHint!=null){Interaction=questHint;return;}
   if(Zone==Campaign.Fields && Vector3.Distance(p,World(3,20))<3)Interaction="E • Las Ostatnich Szeptów";
   if(Zone==Campaign.Fields && Vector3.Distance(p,World(20,15))<3)Interaction="E • Piwnica spalonej farmy";
   if(Zone==Campaign.Cemetery && Vector3.Distance(p,World(19,16))<3)Interaction="E • Kaplica Trzynastej Lampy";
   if(Zone==Campaign.Crypt2 && Vector3.Distance(p,World(3,22))<3)Interaction="E • Cysterna Czarnej Wody";
   if(Zone==Campaign.Kings && Vector3.Distance(p,World(20,16))<3)Interaction="E • Ossuarium Bez Imion";
   if(Vector3.Distance(p,exit)<2.8f)Interaction=Zone==0 ? (QuestAccepted ? "E • Martwe Błonia" : "Najpierw porozmawiaj z Malakorem") : CanExit ? (Zone==Campaign.Heart ? "E • Odzyskaj serce" : "E • "+Campaign.Names[Campaign.Next(Zone)]) : "Droga zamknięta • pokonaj wrogów";
  }
  public bool CanExit {get{
   if(Zone==Campaign.Town)return QuestAccepted;
   if(Zone==Campaign.Cemetery)return GraveDefenseComplete;
   int guardian=Zone==Campaign.Heart ? 0 : Campaign.EncounterBudget(Zone)-1;
   return guardian>=0 && guardian<30 && (slain[Zone]&(1<<guardian))!=0 && StageObjectivesComplete;
  }}
  Citizen NearbyCitizen(Vector3 p)
  {Citizen best=null;float nearest=2.7f;foreach(var citizen in citizens){float d=Vector3.Distance(p,citizen.p);if(d<nearest){best=citizen;nearest=d;}}return best;}
  void InteractCampaign()
  {
   if(State!=Phase.Playing)return;Vector3 p=HeroPosition;
   var c=NearbyCitizen(p);if(c!=null)
   {
    if(c.service=="lore" && !QuestAccepted){QuestAccepted=true;SaveCheckpoint();}
    Talk(c.name,c.copy,c.service);return;
   }
   foreach(var s in shrines)if(!s.used && Vector3.Distance(p,s.pos)<2.6f){ConsumeShrine(s);if(s.used)PlayInteractionAt(s.pos);return;}
   if(InteractQuestMarker())return;
   if(Zone==Campaign.Fields && Vector3.Distance(p,World(3,20))<3){LoadZone(Campaign.Forest);return;}
   if(Zone==Campaign.Fields && Vector3.Distance(p,World(20,15))<3){LoadZone(Campaign.Cellar);return;}
   if(Zone==Campaign.Cemetery && Vector3.Distance(p,World(19,16))<3){LoadZone(Campaign.Chapel);return;}
   if(Zone==Campaign.Crypt2 && Vector3.Distance(p,World(3,22))<3){LoadZone(Campaign.Cistern);return;}
   if(Zone==Campaign.Kings && Vector3.Distance(p,World(20,16))<3){LoadZone(Campaign.Ossuary);return;}
   if(Vector3.Distance(p,exit)>2.8f)return;
   if(Zone==0){if(QuestAccepted)LoadZone(Campaign.Fields);return;}
   if(!CanExit)return;if(Remaining==0)Cleared|=1<<Zone;
   if(Zone==Campaign.Heart){State=Phase.Won;Time.timeScale=0;SaveCheckpoint();Sound("victory");return;}
   int previous=Zone;LoadZone(Campaign.Next(Zone));
   if(Campaign.SideBranch(previous))
   {
    int x=previous==Campaign.Forest ? 3 : previous==Campaign.Cellar ? 20 : previous==Campaign.Chapel ? 19 : previous==Campaign.Cistern ? 3 : 20;
    int z=previous==Campaign.Forest ? 20 : previous==Campaign.Cellar ? 15 : previous==Campaign.Chapel ? 16 : previous==Campaign.Cistern ? 22 : 16;
    var cell=grid.NearestWalkable(x,z);player.Root.transform.position=World(cell.X,cell.Z);View.transform.position=HeroPosition+new Vector3(9,15,-11);View.transform.LookAt(HeroPosition);
   }
  }
  public void ChooseEdra(int choice){if(Service!="edra" || EdraChoice!=0 || Remaining>0)return;EdraChoice=choice;Materials+=choice==1 ? 12 : 4;GemBag[0]+=choice==2 ? 1 : 0;SaveCheckpoint();DialogueText=choice==1 ? "Przeciąłeś korzenie. Edra oddaje Ci pamiątki pielgrzymów i rusza do Oakhaven." : "Oczyściłeś drzewo. Edra znika w jego korze. W dłoni zostaje bursztyn pamięci.";}
  public bool Alchemy(int action)
  {
   if(Service!="alchemy" || action<0 || action>2)return false;
   if(action==2)return BuyGem();
   int cost=action==0 ? 8 : 40+PotionRank*30;
   if(Coins<cost || action==0 && Potions>=5 || action==1 && PotionRank>=3)return false;
   Coins-=cost;if(action==0)Potions++;else PotionRank++;SaveCheckpoint();Sound("shrine");return true;
  }
  void StartAdditional(int i,Vector3 aim)
  {
   if(i<0 || i>2){PowerNotice(-1,false,"Nieznana moc.");return;}
   if(!BeginPower(i+3))return;EnsureClassMechanics();
   int cost=i==2 ? 50 : 20;
   if(Status.Count("slaughter",Time.time)==0){if(SelectedHero==3)GainResource(cost);else Resource-=cost;}
   extraAt[i]=Time.time+Cooldown(i==2 ? 24 : i==1 ? 9 : 14);
   if(i>0)FaceActionAim(aim);
   player.Play(i==0 ? "Defense_A" : "Cast_A",false,i==2 ? .65f : .45f);
   Vector3 p=HeroPosition,center=p+Vector3.ClampMagnitude(aim-p,7);if(!Visible(p,center))center=p;
   if(i>0){float damage=(i==2 ? 90 : 35)+Level*5+CharmRank*7;SchedulePower((i==2?.65f:.45f)*MeleeContactFraction,()=>ResolveAdditional(i,aim,center,damage));return;}
   string result;
   if(i==0)
   {
    if(SelectedHero==0 || SelectedHero==3)Status.Apply("ward",ExtraName(i),Time.time,7);
    if(SelectedHero==1)Status.Apply("drain",ExtraName(i),Time.time,8);
    if(SelectedHero==2){Status.Apply("fury",ExtraName(i),Time.time,7);Shadow=Mathf.Min(100,Shadow+30);}
    if(SelectedHero==4){Status.Apply("regrowth",ExtraName(i),Time.time,7);CreateRootField(p,3.5f,4);}
    result=SkillActiveSummary(3)+" • "+SkillActiveRemaining(3).ToString("0")+" s";ClassSpellVfx(i+3,p,1.8f);
   }
   else result=CastClassAdditional(i,aim,center,(i==2 ? 90 : 35)+Level*5+CharmRank*7);
   Sound(ClassSpellSound);PowerConfirmed(i+3,result,i==0 ? p : center);
  }
  void ResolveAdditional(int i,Vector3 aim,Vector3 center,float damage){
   string result=CastClassAdditional(i,aim,center,damage);Sound(ClassSpellSound);PowerConfirmed(i+3,result,center);
  }
  void UpdateCampaign()
  {
   if(SelectedHero==2)Shadow=Mathf.Min(100,Shadow+Time.deltaTime*3);
   if(Time.time>regenAt){regenAt=Time.time+1;Heal(Regeneration+(Status.Count("regrowth",Time.time)>0 ? 6 : 0));}
   if(Zone==Campaign.Road && CanExit && !bridgeLowered){bridgeLowered=true;if(drawbridge)drawbridge.transform.rotation=Quaternion.identity;Burst(exit,2,art.goldMaterial);}
   if(Zone==Campaign.Cemetery && graveWaves<3 && Vector3.Distance(HeroPosition,World(12,20))<13)
   {
    bool waveAlive=enemies.Exists(e=>e.spawnId>=4 && e.hp>0);
    if(!waveAlive && Time.time>waveAt){graveWaves++;waveAt=Time.time+5;for(int i=0;i<5;i++)SpawnEncounterAt(10+i,25,graveWaves==2 && i%2==0 ? 1 : graveWaves==3 && i==2 ? 10 : 0,graveWaves==3 && i==2,"Zmarły z fali "+graveWaves);HideDefeated();ShowNotice("Obrona grabarza • fala "+graveWaves+" / 3");}
   }
   if((Zone==Campaign.Crypt2 || Zone==Campaign.Crypt3) && Time.time>trapAt){trapAt=Time.time+6;Vector3 p=World(5,12);if(Zone==Campaign.Crypt3)p=World(13,27);AddHazard(p,1.6f,3,true,Color.red,"trap");}
   RewardFullClear();
  }
 }
}
