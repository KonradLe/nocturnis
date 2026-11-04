using UnityEngine;

namespace Krypta
{
 public sealed partial class KryptaGame
 {
  public string CastNotice {get;private set;}="";
  public float CastNoticeUntil {get;private set;}
  public bool CastNoticeSucceeded {get;private set;}
  public int LastCastSlot {get;private set;}=-1;
  public int CastNoticeSequence {get;private set;}
  float rejectionAt=-10,healFeedbackAt=-10;
  void ResetCombatActions()
  {
   CancelCombatAction();ResetRelicCombat();ResetClassMechanics();
   strikeAt=pulseAt=defenseAt=dashAt=dashEnd=hurtUntil=graceAt=0;
   for(int i=0;i<extraAt.Length;i++)extraAt[i]=0;
   rejectionAt=healFeedbackAt=-10;
   lastMusicContact=-100;killStreak=0;CastNotice="";CastNoticeUntil=0;LastCastSlot=-1;
  }
  public bool CanUseSkill(int slot){return string.IsNullOrEmpty(SkillBlockReason(slot));}
  public string SkillBlockReason(int slot)
  {
   if(slot<0 || slot>5)return "Nieznana moc.";
   if(State!=Phase.Playing || player==null)return State==Phase.Title ? "Rozpocznij wyprawę." : "Wznów grę, aby użyć mocy.";
   if(IntroActive)return "Pomiń wprowadzenie [Spacja].";
   if(Zone==Campaign.Town && slot!=2)return "Moce bojowe są wyłączone w mieście.";
   if(slot==5 && Level<4)return "Wymagany poziom 4.";
   float cd=slot==0 ? Mathf.Max(0,strikeAt-Time.time) : slot==1 ? PulseReady : slot==2 ? DefenseReady : ExtraReady(slot-3);
   if(cd>0)return "Odnowienie: "+cd.ToString("0.0")+" s.";
   bool free=Status.Count("slaughter",Time.time)>0;
   if(slot==2 && SelectedHero==2 && !free && Shadow<20)return "Za mało: Mrok ("+Mathf.FloorToInt(Shadow)+" / 20).";
   int cost=slot==1 ? 25 : slot==5 ? 50 : slot>=3 ? 20 : 0;
   if(!free && SelectedHero!=3 && Resource<cost)return "Za mało: "+Hero.resource+" ("+Mathf.FloorToInt(Resource)+" / "+cost+").";
   return "";
  }
  bool BeginPower(int slot)
  {
   string reason=SkillBlockReason(slot);
   if(reason.Length==0){ReservePower(slot);return true;}
   PowerNotice(slot,false,reason);
   if(player!=null && Time.unscaledTime>=rejectionAt)
   {rejectionAt=Time.unscaledTime+.5f;Floating(HeroPosition,"NIEGOTOWE",new Color(1,.65f,.38f));}
   return false;
  }
  void PowerNotice(int slot,bool success,string message)
  {
   LastCastSlot=slot;CastNoticeSucceeded=success;CastNotice=message;CastNoticeUntil=Time.unscaledTime+(success ? 3.5f : 2.5f);CastNoticeSequence++;
   if(Testing || System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-server-preview")>=0)
    Debug.Log("COMBAT_FEEDBACK slot="+slot+" success="+success+" message="+message);
  }
  void PowerConfirmed(int slot,string effect,Vector3 center)
  {
   string name=slot==1 ? Hero.skill : slot==2 ? Hero.defense : ExtraName(slot-3);
   PowerNotice(slot,true,name+" • "+effect);
   Floating(HeroPosition,slot==2 ? "OBRONA" : slot==3 ? "WZMOCNIENIE" : "MOC!",Color.Lerp(Hero.color,Color.white,.45f));
   if(slot==2 || slot==3)
   {
    var glyph=new GameObject("Znak aktywnej mocy");glyph.transform.SetParent(player.Root.transform,false);glyph.transform.localPosition=Vector3.up*2.25f;
    var line=glyph.AddComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=art.effectMaterial;line.widthMultiplier=.08f;line.startColor=line.endColor=Color.Lerp(Hero.color,Color.white,.35f);
    Vector3[] points=slot==2 ? new[]{new Vector3(-.40f,.26f,0),new Vector3(.40f,.26f,0),new Vector3(.32f,-.18f,0),new Vector3(0,-.45f,0),new Vector3(-.32f,-.18f,0),new Vector3(-.40f,.26f,0)} : new[]{new Vector3(-.35f,0,0),new Vector3(0,.38f,0),new Vector3(.35f,0,0),new Vector3(0,-.30f,0),new Vector3(-.35f,0,0)};
    line.positionCount=points.Length;line.SetPositions(points);
    effects.Add(new Effect{obj=glyph,end=Time.time+1.2f,duration=1.2f});
   }
   else if(Vector3.Distance(center,HeroPosition)>2)Beam(HeroPosition+Vector3.up,center+Vector3.up*.5f,classMaterial);
  }
  public float StatusRemaining(string id)
  {foreach(var entry in Status.Entries)if(entry.id==id)return Mathf.Max(0,entry.until-Time.time);return 0;}
  public float SkillActiveRemaining(int slot)
  {
   // R and 2 can refresh the same drain status: both show its one effective timer.
   if(slot==2)return StatusRemaining(SelectedHero==1 ? "drain" : SelectedHero==2 ? "shadow" : "armor");
   if(slot==3)return StatusRemaining(SelectedHero==1 ? "drain" : SelectedHero==2 ? "fury" : SelectedHero==4 ? "regrowth" : "ward");
   if(slot==1 && SelectedHero==3){float remaining=0;foreach(var c in classCasts)if(c.slot==1)remaining=Mathf.Max(remaining,c.at-Time.time);return Mathf.Max(0,remaining);}
   return 0;
  }
  public string SkillActiveSummary(int slot)
  {
   if(slot==2)return BuffDescription(SelectedHero==1 ? "drain" : SelectedHero==2 ? "shadow" : "armor");
   if(slot==3)return BuffDescription(SelectedHero==1 ? "drain" : SelectedHero==2 ? "fury" : SelectedHero==4 ? "regrowth" : "ward");
   return slot==1 && SelectedHero==3 ? "Ładowanie osobliwości: eksplozja po opóźnieniu" : "";
  }
  public string BuffDescription(string id)
  {
   switch(id)
   {
    case "armor":return "Otrzymywane obrażenia −55%";
    case "ward":return "Otrzymywane obrażenia −30%";
    case "drain":return "Leczenie za 15% zadanych obrażeń";
    case "fury":return "Zadawane obrażenia +30%";
    case "regrowth":return "Regeneracja +6 HP/s; splot zatrzymuje ruch zwykłych wrogów";
    case "counter":return "Pierwsze trafienie daje kontrę; dodatkowa redukcja 85%";
    case "riposte":return "Następny cios lub moc główna: obrażenia +75%";
    case "precision":return "Następna strzała przebijająca: +25% obrażeń i 2 cele";
    case "voidshell":return "Magazynuje część obrażeń i oddaje je po 3 s";
    case "shadow":return "Niewrażliwość i szybkość +35%";
    case "entropy":return "Zadawane obrażenia +50%";
    case "slaughter":return "Darmowe moce, odnowienie −75%";
    case "grace":return "Szybkość i łańcuchy światła";
    case "ichor":return "Leczenie −10% za warstwę";
    case "slow":return "Ruch i ataki spowolnione";
    default:return "Aktywny efekt";
   }
  }
  float PowerDamageMultiplier {get{return (Status.Count("entropy",Time.time)>0 ? 1.5f : 1)*(Status.Count("fury",Time.time)>0 ? 1.3f : 1);}}
 }
}
