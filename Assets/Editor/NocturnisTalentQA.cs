using System;
using System.Reflection;
using UnityEngine;

namespace Krypta.Editor
{
 // ExecuteMethod entry point or callable from the wider development QA.
 // Uses a fresh, disabled actor with no world/player, so checkpoint writes
 // are rejected by SaveCheckpoint and the user's PlayerPrefs are untouched.
 public static class NocturnisTalentQA
 {
  static int checks;
  static void Check(bool condition,string message)
  {checks++;if(!condition)throw new Exception("TALENT_QA: "+message);}
  static void Near(float value,float expected,string message)
  {Check(Mathf.Abs(value-expected)<.001f,message+" actual="+value+" expected="+expected);}
  static void Set(KryptaGame game,string property,object value)
  {typeof(KryptaGame).GetProperty(property).SetValue(game,value,null);}
  public static void Run(){Debug.Log(RunChecks());}
  public static string RunChecks()
  {
   checks=0;
   var raw=new TalentSave{hero=0,respecs=2,ranks=new[]{3,2,1,3,2,1,3,2,1}};
   var low=NocturnisTalentRules.Normalize(raw,0,1);
   Check(NocturnisTalentRules.Spent(low)==0,"new character receives no spending");
   var old=NocturnisTalentRules.Normalize(null,3,12);
   Check(old.hero==3 && NocturnisTalentRules.Spent(old)==0 && NocturnisTalentRules.Earned(12)==11,"legacy checkpoint receives earned, unspent points");
   Check(NocturnisTalentRules.Spent(NocturnisTalentRules.Normalize(raw,0,5))==4,"over-budget save is clamped to earned points");
   var orphan=NocturnisTalentRules.Normalize(new TalentSave{hero=0,ranks=new[]{1,2,1,-1,100,1,0,2,1}},0,20);
   Check(orphan.ranks[1]==0 && orphan.ranks[2]==0 && orphan.ranks[4]==0 && orphan.ranks[7]==0,"orphan nodes and negative prerequisite rejected");
   Check(NocturnisTalentRules.Spent(NocturnisTalentRules.Normalize(raw,1,20))==0,"another class cannot inherit saved ranks");
   Check(NocturnisTalentRules.Spent(NocturnisTalentRules.Normalize(new TalentSave{hero=0,version=99},0,20))==0,"unknown schema safely discarded");
   Check(NocturnisTalentRules.Spent(NocturnisTalentRules.Normalize(new TalentSave{hero=0,ranks=new[]{999}},0,20))==3,"short oversized rank list safely normalized");
   var levelGate=NocturnisTalentRules.Normalize(raw,0,6);
   Check(levelGate.ranks[2]==0,"capstone locked before level seven");
   var round=JsonUtility.FromJson<TalentSave>(JsonUtility.ToJson(NocturnisTalentRules.Normalize(raw,0,20)));
   Check(NocturnisTalentRules.Spent(round)==18 && round.respecs==2,"serialized ranks and respec counter round-trip");
   round.ranks[0]=0;Check(raw.ranks[0]==3,"capture arrays are independent");

   float timeScale=Time.timeScale;bool testing=KryptaGame.Testing;GameObject host=null;
   try
   {
    KryptaGame.Testing=true;host=new GameObject("Isolated talent QA");
    var game=host.AddComponent<KryptaGame>();game.enabled=false;
    Set(game,"SelectedHero",0);Set(game,"Level",12);Set(game,"Zone",0);Set(game,"State",KryptaGame.Phase.Playing);Set(game,"Health",60f);Set(game,"Coins",250);
    game.ResetTalents();game.OpenTalents();
    Check(game.TalentPoints==11,"earned points exposed to gameplay");
    Check(!game.SpendTalent(2),"cannot buy capstone without prerequisites");
    Check(!game.SpendTalent(-1) && !game.SpendTalent(9),"invalid node indices rejected");
    Check(game.SpendTalent(3),"health talent can be learned");
    Near(game.Health,60,"learning health does not heal");
    Near(game.TalentHealthBonus,12,"health effect applied");
    Check(game.TalentPoints==10,"exactly one point spent");
    var snapshot=game.CaptureTalents();snapshot.ranks[3]=3;
    Check(game.TalentRank(3)==1,"export cannot mutate live character");
    Check(game.TalentRespecCost==0 && game.RespecTalents(),"first town respec is free");
    Check(game.Coins==250 && game.TalentPoints==11,"respec returns points with correct currency");
    Near(game.Health,60,"respec does not heal");
    Check(game.TalentRespecCost==80,"subsequent price depends on level");
    Check(game.SpendTalent(3),"spend after respec");
    Set(game,"Zone",1);Check(!game.RespecTalents(),"respec outside town rejected");Set(game,"Zone",0);
    Set(game,"Coins",79);Check(!game.RespecTalents(),"insufficient funds leave allocation intact");
    Check(game.TalentRank(3)==1 && game.Coins==79,"failed respec is atomic");
    Set(game,"Coins",100);Check(game.RespecTalents() && game.Coins==20,"paid respec deducts exact cost");
    Set(game,"Level",1);game.ResetTalents();Check(!game.SpendTalent(0),"level one cannot spend");
    Set(game,"Level",20);
    for(int hero=0;hero<5;hero++)
    {
     Set(game,"SelectedHero",hero);var max=new TalentSave{hero=hero,ranks=new[]{3,2,1,3,2,1,3,2,1}};
     game.RestoreTalents(max);
     Near(game.TalentHealthBonus,36,"max health bonus class "+hero);
     Near(game.TalentMoveMultiplier,1.09f,"movement bonus class "+hero);
     Near(game.TalentTakenMultiplier,.90f,"protection bonus class "+hero);
     Near(game.TalentResourceMultiplier,1.20f,"resource bonus class "+hero);
     Near(game.TalentCooldownMultiplier,.88f,"cooldown bonus class "+hero);
     Near(game.TalentPotionMultiplier,1.35f,"potion bonus class "+hero);
     Near(game.TalentDamageMultiplier(null),1.15f,"ordinary damage class "+hero);
     var elite=new KryptaGame.Enemy{hp=100,maxHp=100,elite=true,armorUntil=0,stunUntil=0};
     Set(game,"Resource",0f);
     Near(game.TalentDamageMultiplier(elite),1.31f,"elite damage without condition class "+hero);
     if(hero==0)elite.stunUntil=Time.time+10;
     if(hero==1)elite.hp=50;
     if(hero==3)Set(game,"Resource",70f);
     if(hero==4)elite.slowUntil=Time.time+10;
     if(hero!=2)Near(game.TalentDamageMultiplier(elite),1.49f,"class condition damage class "+hero);
     for(int n=0;n<9;n++)Check(!string.IsNullOrEmpty(game.TalentName(n)) && !string.IsNullOrEmpty(game.TalentEffect(n)),"named effect "+hero+"/"+n);
    }
    Set(game,"SelectedHero",0);game.RestoreTalents(new TalentSave{hero=0,ranks=new[]{0,0,0,3,0,0,0,0,0}});
    Set(game,"State",KryptaGame.Phase.Playing);game.OpenTalents();Set(game,"Health",999f);Set(game,"Coins",1000);
    Check(game.RespecTalents(),"health-clamp respec succeeds");Check(game.Health<=game.MaxHealth,"respec clamps down to new maximum");
   }
   finally
   {
    if(host)UnityEngine.Object.DestroyImmediate(host);
    KryptaGame.Testing=testing;Time.timeScale=timeScale;
   }
   return "TALENT_QA PASS "+checks+" checks: migration, prerequisites, earned budget, save isolation, respec, five-class effects.";
  }
 }
}
