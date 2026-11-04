using System;
using UnityEngine;

namespace Krypta
{
 [Serializable]
 public sealed class TalentSave
 {
  public int version=1,hero,respecs;
  public int[] ranks=new int[9];
 }

 // Ranks are reconstructed in prerequisite order. Old checkpoints have no
 // talent payload and earn their existing level's points without altering XP.
 public static class NocturnisTalentRules
 {
  public const int Count=9;
  public static int Maximum(int node){return node<0 || node>=Count ? 0 : new[]{3,2,1}[node%3];}
  public static int LevelGate(int node){return node<0 || node>=Count ? 1000 : new[]{2,4,7}[node%3];}
  public static int Earned(int level){return Mathf.Clamp(level-1,0,99);}
  public static int Spent(TalentSave save)
  {
   int result=0;if(save==null || save.ranks==null)return 0;
   for(int i=0;i<Math.Min(Count,save.ranks.Length);i++)result+=Mathf.Clamp(save.ranks[i],0,Maximum(i));
   return result;
  }
  public static TalentSave Normalize(TalentSave raw,int hero,int level)
  {
   var clean=new TalentSave{hero=Mathf.Clamp(hero,0,4)};
   if(raw==null || raw.version!=1 || raw.hero!=hero)return clean;
   clean.respecs=Mathf.Clamp(raw.respecs,0,10000);
   if(raw.ranks==null)return clean;
   int budget=Earned(level);
   for(int i=0;i<Math.Min(Count,raw.ranks.Length);i++)
   {
    if(level<LevelGate(i) || (i%3>0 && clean.ranks[i-1]<2))continue;
    clean.ranks[i]=Math.Min(budget,Mathf.Clamp(raw.ranks[i],0,Maximum(i)));
    budget-=clean.ranks[i];
   }
   return clean;
  }
 }

 public sealed partial class KryptaGame
 {
  TalentSave talentState;
  public bool TalentsOpen {get;private set;}
  static readonly string[][] TalentBranches={
   new[]{"WYROK","PRZYSIĘGA","PIELGRZYM"},
   new[]{"HEMOMANCJA","ŻYWA KREW","TĘTNO"},
   new[]{"ŁOWY","CZUJNOŚĆ","CIEŃ"},
   new[]{"ENTROPIA","PRÓŻNIA","ORBITA"},
   new[]{"GNIEW","KORA","KORZENIE"}
  };
  static readonly string[][] TalentNames={
   new[]{"Rozżarzona stal","Sąd nad potężnymi","Pęknięta pieczęć","Niezłomna przysięga","Hart popiołów","Kielich pielgrzyma","Krok pokutnika","Podsycanie żaru","Wieczny wyrok"},
   new[]{"Ostra posoka","Krew władców","Ostatnie tętno","Żyła życia","Gęsta krew","Czerwona ambrozja","Taniec tętnic","Głód posoki","Szybki puls"},
   new[]{"Celne oko","Tropiciel bestii","Daleki strzał","Wytrwałość łowcy","Skóra zwiadowcy","Zapas na szlak","Lekki krok","Pełne skupienie","Rytm łowów"},
   new[]{"Szept osobliwości","Upadek olbrzymów","Krytyczna masa","Gwiezdne ciało","Gęstość próżni","Eliksir astralny","Orbitalny krok","Narastanie entropii","Zagięcie czasu"},
   new[]{"Cierniowe ostrze","Pogromca spaczenia","Splot cierni","Serce dębu","Stara kora","Sok odnowy","Leśna ścieżka","Żywa żywica","Rytm puszczy"}
  };
  void EnsureTalents()
  {if(talentState==null || talentState.hero!=SelectedHero)talentState=new TalentSave{hero=SelectedHero};}
  public void ResetTalents(){talentState=new TalentSave{hero=SelectedHero};TalentsOpen=false;}
  public TalentSave CaptureTalents(){EnsureTalents();return NocturnisTalentRules.Normalize(talentState,SelectedHero,Level);}
  public void RestoreTalents(TalentSave saved){talentState=NocturnisTalentRules.Normalize(saved,SelectedHero,Level);TalentsOpen=false;}
  public int TalentRank(int node)
  {EnsureTalents();return node>=0 && node<NocturnisTalentRules.Count ? talentState.ranks[node] : 0;}
  public int TalentSpent {get{EnsureTalents();return NocturnisTalentRules.Spent(talentState);}}
  public int TalentPoints {get{return Mathf.Max(0,NocturnisTalentRules.Earned(Level)-TalentSpent);}}
  public int TalentRespecCost {get{EnsureTalents();return talentState.respecs==0 ? 0 : 20+5*Mathf.Clamp(Level,1,100);}}
  public string TalentBranchName(int branch){return TalentBranches[Mathf.Clamp(SelectedHero,0,4)][Mathf.Clamp(branch,0,2)];}
  public string TalentName(int node){return node>=0 && node<9 ? TalentNames[Mathf.Clamp(SelectedHero,0,4)][node] : "";}
  public string TalentEffect(int node)
  {
   switch(node)
   {
    case 0:return "+5% obrażeń broni i mocy za rangę.";
    case 1:return "+8% obrażeń przeciw elitom i bossom za rangę.";
    case 2:return new[]{
     "+18% obrażeń przeciw wrogom ogłuszonym lub z osłabionym pancerzem.",
     "+18% obrażeń przeciw wrogom mającym najwyżej połowę zdrowia.",
     "+18% obrażeń przeciw wrogom oddalonym o co najmniej 4 metry.",
     "+18% obrażeń przy 60+ Entropii lub aktywnym Przepełnieniu.",
     "+18% obrażeń przeciw wrogom zakorzenionym lub spowolnionym."
    }[Mathf.Clamp(SelectedHero,0,4)];
    case 3:return "+12 maksymalnego zdrowia za rangę. Nie odnawia zdrowia.";
    case 4:return "-5% otrzymywanych obrażeń za rangę.";
    case 5:return "Mikstury zdrowia leczą o 35% więcej.";
    case 6:return "+3% szybkości poruszania się za rangę.";
    case 7:return "+10% zdobywanego zasobu za rangę.";
    case 8:return "Czas odnowienia mocy krótszy o 12%.";
    default:return "";
   }
  }
  public string TalentBlocked(int node)
  {
   if(node<0 || node>=9)return "Nieznany talent";
   if(TalentRank(node)>=NocturnisTalentRules.Maximum(node))return "Maksymalna ranga";
   if(Level<NocturnisTalentRules.LevelGate(node))return "Wymaga poziomu "+NocturnisTalentRules.LevelGate(node);
   if(node%3>0 && TalentRank(node-1)<2)return "Poprzedni talent: ranga 2";
   if(TalentPoints<=0)return "Brak wolnych punktów";
   return "";
  }
  public void OpenTalents()
  {
   if(State!=Phase.Playing && State!=Phase.Paused)return;
   Pause();EquipmentOpen=JournalOpen=BookOpen=false;Service="";TalentsOpen=true;Sound("triptych");
  }
  public bool SpendTalent(int node)
  {
   if(State!=Phase.Paused || !TalentsOpen || Health<=0 || TalentBlocked(node)!="")return false;
   talentState.ranks[node]++;
   // Increasing maximum health must never refill the current health bar.
   Health=Mathf.Min(Health,MaxHealth);SaveCheckpoint();Sound("shrine");
   ShowNotice("Rozwinięto: "+TalentName(node)+" • ranga "+TalentRank(node));return true;
  }
  public string TalentRespecBlocked()
  {
   if(Zone!=0)return "Zmiana talentów dostępna w Oakhaven";
   if(TalentSpent==0)return "Brak rozdanych punktów";
   if(Coins<TalentRespecCost)return "Brakuje złota: koszt "+TalentRespecCost;
   return "";
  }
  public bool RespecTalents()
  {
   if(State!=Phase.Paused || !TalentsOpen || Health<=0 || TalentRespecBlocked()!="")return false;
   int cost=TalentRespecCost;Coins-=cost;Array.Clear(talentState.ranks,0,9);talentState.respecs=Mathf.Min(10000,talentState.respecs+1);
   // Respeccing and buying health ranks again cannot become free healing.
   Health=Mathf.Min(Health,MaxHealth);SaveCheckpoint();Sound("shrine");
   ShowNotice("Talenty zwrócone • dostępne punkty: "+TalentPoints);return true;
  }
  public float TalentHealthBonus {get{return TalentRank(3)*12;}}
  public float TalentTakenMultiplier {get{return 1-TalentRank(4)*.05f;}}
  public float TalentPotionMultiplier {get{return 1+TalentRank(5)*.35f;}}
  public float TalentMoveMultiplier {get{return 1+TalentRank(6)*.03f;}}
  public float TalentResourceMultiplier {get{return 1+TalentRank(7)*.10f;}}
  public float TalentCooldownMultiplier {get{return 1-TalentRank(8)*.12f;}}
  public float TalentDamageMultiplier(Enemy enemy)
  {
   float bonus=TalentRank(0)*.05f;
   if(enemy==null)return 1+bonus;
   if(enemy.elite || enemy.boss || IsStageGuardian(enemy))bonus+=TalentRank(1)*.08f;
   if(TalentRank(2)>0)
   {
    bool active=false;
    switch(SelectedHero)
    {
     case 0:active=enemy.stunUntil>Time.time || enemy.armorUntil>Time.time;break;
     case 1:active=enemy.hp<=enemy.maxHp*.5f;break;
     case 2:active=enemy.visual!=null && enemy.visual.Root && Vector3.Distance(HeroPosition,enemy.Position)>=4;break;
     case 3:active=Resource>=60 || Status.Count("entropy",Time.time)>0;break;
     case 4:active=enemy.slowUntil>Time.time || (enemy.visual!=null && enemy.visual.Root && EnemyRootRemaining(enemy)>0);break;
    }
    if(active)bonus+=.18f;
   }
   return 1+Mathf.Min(.49f,bonus);
  }
 }
}
