using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Krypta.Editor
{
 public static class NocturnisMotionCombatQA
 {
  const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
  const string Session="Nocturnis.MotionCombatQA";
  static double started,next;static int stage,checks;static string error="";
  static readonly List<string> evidence=new List<string>();
  [Serializable]sealed class Report{public bool passed;public int assertions;public string error;public string[] evidence;}
  [InitializeOnLoadMethod]static void Restore()
  {if(!SessionState.GetBool(Session,false))return;started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
  public static void Run()
  {EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool(Session,true);EditorApplication.isPlaying=true;}
  static void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)error=message+"\n"+stack;}
  static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
  static void Near(float value,float expected,string name){Check(Mathf.Abs(value-expected)<.035f,name+" expected="+expected+" actual="+value);}
  static object Call(KryptaGame g,string name,params object[] args){return typeof(KryptaGame).GetMethod(name,P).Invoke(g,args);}
  static object Get(KryptaGame g,string name){return typeof(KryptaGame).GetField(name,P).GetValue(g);}
  static void Field(KryptaGame g,string name,object value){typeof(KryptaGame).GetField(name,P).SetValue(g,value);}
  static void Set(KryptaGame g,string name,object value){typeof(KryptaGame).GetProperty(name).SetValue(g,value);}
  static float Contact(KryptaGame g){object strike=Get(g,"pendingMeleeStrike");return (float)strike.GetType().GetField("contactAt").GetValue(strike);}
  static ActorVisual Player(KryptaGame g){return (ActorVisual)Get(g,"player");}
  static KryptaGame.Enemy Fixture(KryptaGame g,int hero)
  {
   Set(g,"State",KryptaGame.Phase.Title);g.SelectHero(hero);Call(g,"LoadZone",Campaign.Crypt);Set(g,"State",KryptaGame.Phase.Playing);
   var enemy=g.enemies[0];foreach(var other in g.enemies)if(other!=enemy){other.hp=0;other.visual.Root.SetActive(false);}
   Player(g).Root.transform.position=new Vector3(9,0,9);enemy.visual.Root.transform.position=new Vector3(9,0,10.8f);
   enemy.boss=enemy.elite=enemy.zombie=false;enemy.archetype=0;enemy.alerted=true;
   Check(g.MapFloor(5,5) && g.MapFloor(5,6) && (bool)Call(g,"Visible",g.HeroPosition,enemy.Position),"Valid fixture floor and sight");
   Reset(g,enemy);return enemy;
  }
  static void Reset(KryptaGame g,KryptaGame.Enemy enemy)
  {
   Call(g,"CancelMeleeActions");Call(g,"ResetCombatActions");Call(g,"ResetCraftedVfx");Call(g,"CancelEnemyMelee",enemy);g.Status.Clear();
   Set(g,"State",KryptaGame.Phase.Playing);Set(g,"Health",g.MaxHealth);Set(g,"Resource",50f);Set(g,"Shadow",60f);Field(g,"introUntil",0f);
   enemy.hp=enemy.maxHp=10000;enemy.impactAt=enemy.stunUntil=enemy.slowUntil=enemy.armorUntil=0;enemy.attackAt=Time.time+100;enemy.spellAt=Time.time+100;
   Player(g).Root.transform.position=new Vector3(9,0,9);enemy.visual.Root.transform.position=new Vector3(9,0,10.8f);
   Time.timeScale=0;
  }
  static void Tick()
  {
   if(EditorApplication.timeSinceStartup-started>180){Finish(false,"Timeout");return;}
   if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.2;
   var g=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!g || !g.View)return;
   try
   {
    if(error.Length>0)throw new Exception(error);
    KryptaGame.Testing=true;g.enabled=false;var hud=g.GetComponent<KryptaHUD>();if(hud)hud.enabled=false;Time.timeScale=0;
    if(stage<5){HeroChecks(g,stage);stage++;return;}
    // Title can freeze the very first frame at t=0. Enemy.impactAt=0 is the
    // runtime sentinel for no attack, so the actual UpdateEnemy fixture needs
    // a positive clock before it can set a due (non-sentinel) contact time.
    if(Time.time<=.0001f)
    {
     Debug.Log("MOTION_QA_CLOCK_BOOTSTRAP time="+Time.time+" (zero is the enemy no-contact sentinel)");
     evidence.Add("Enemy fixture advanced one disabled-game frame from t="+Time.time+" to avoid the impactAt=0 sentinel.");
     Time.timeScale=1;next=EditorApplication.timeSinceStartup+.1;return;
    }
    EnemyChecks(g);Finish(true,"");
   }
   catch(Exception ex){Finish(false,ex.ToString());}
  }
  static void HeroChecks(KryptaGame g,int hero)
  {
   var enemy=Fixture(g,hero);float damage=g.AttackPower;
   Call(g,"Strike",enemy.Position);float at=Contact(g);
   Check(g.MeleeWindupActive,"Class "+hero+" has pending windup");Check(at>Time.time+.1f && at<Time.time+.23f,"Contact lies in authored windup");
   Near(enemy.hp,10000,"No damage on button press");Near(g.Resource,50,"No resource before contact");Check(g.ActiveCraftedVfx==0,"No impact VFX before contact");
   Call(g,"TickMeleeActions",at-.001f);Near(enemy.hp,10000,"No damage before contact threshold");
   Call(g,"TickMeleeActions",at);Near(10000-enemy.hp,damage,"Damage occurs at contact");Check(!g.MeleeWindupActive,"Contact consumes queued strike");Check(g.ActiveCraftedVfx>0,"Contact produces real attack VFX");
   Near(g.Resource,hero==3 ? 70 : 60,"Contact grants resource once");Near((float)Get(g,"strikeAt")-Time.time,g.Hero.interval,"Original attack interval preserved");
   float hp=enemy.hp;Call(g,"TickMeleeActions",at+1);Call(g,"Strike",enemy.Position);Near(enemy.hp,hp,"No double hit and cooldown holds");Check(!g.MeleeWindupActive,"Cooldown rejects another windup");
   // A long frame may let ActorVisual.Tick return to locomotion before the
   // combat queue is serviced. The completed action must still hit once.
   foreach(string recovery in new[]{"Idle","Running_A"})
   {
    Reset(g,enemy);Call(g,"Strike",enemy.Position);at=Contact(g);int action=Player(g).ActionSequence;
    Player(g).Play(recovery,true,0);Check(Player(g).ActionSequence==action,"Loop recovery keeps action identity: "+recovery);
    Call(g,"TickMeleeActions",at+1);Near(10000-enemy.hp,damage,"Overdue contact survives locomotion recovery: "+recovery);
    Check(!g.MeleeWindupActive,"Overdue recovered action is consumed: "+recovery);
    Call(g,"TickMeleeActions",at+2);Near(10000-enemy.hp,damage,"Recovered action cannot hit twice: "+recovery);
   }
   foreach(string replacement in new[]{"Hit_A",g.Hero.range>3 ? "1H_Ranged_Shoot" : "1H_Melee_Attack_Slice_Horizontal"})
   {
    Reset(g,enemy);Call(g,"Strike",enemy.Position);at=Contact(g);int action=Player(g).ActionSequence;
    Player(g).Play(replacement,false,.3f);Check(Player(g).ActionSequence!=action,"New non-loop action changes identity: "+replacement);
    Call(g,"TickMeleeActions",at+1);Near(enemy.hp,10000,"New action cancels stale contact even with same clip name: "+replacement);
    Check(!g.MeleeWindupActive,"Replaced action clears pending contact");
   }
   Reset(g,enemy);Call(g,"Strike",enemy.Position);enemy.visual.Root.transform.position+=Vector3.forward*20;g.ReviewCompleteStrike();Near(enemy.hp,10000,"Target can leave reach before contact");Near(g.Resource,50,"Miss does not grant resource");
   Reset(g,enemy);Call(g,"Strike",enemy.Position);at=Contact(g);g.Pause();Call(g,"TickMeleeActions",at);Near(enemy.hp,10000,"Pause prevents a hit");Check(g.MeleeWindupActive,"Pause retains valid action");g.Resume();Time.timeScale=0;Call(g,"TickMeleeActions",at);Near(10000-enemy.hp,damage,"Resume resolves contact once");
   Reset(g,enemy);Call(g,"Strike",enemy.Position);Field(g,"dashEnd",Time.time+.2f);g.ReviewCompleteStrike();Near(enemy.hp,10000,"Dodge cancels pending attack");Check(!g.MeleeWindupActive,"Dodge clears queue");
   Reset(g,enemy);Call(g,"Strike",enemy.Position);g.CastDefense();hp=enemy.hp;g.ReviewCompleteStrike();Near(enemy.hp,hp,"Replacing strike with defense cancels contact");Check(!g.MeleeWindupActive,"Defense clears stale action");
   Reset(g,enemy);Call(g,"Strike",enemy.Position);Set(g,"State",KryptaGame.Phase.Title);g.ReviewCompleteStrike();Near(enemy.hp,10000,"Title cancels pending attack");Check(!g.MeleeWindupActive,"Title clears queue");
   Reset(g,enemy);Call(g,"Strike",enemy.Position);Set(g,"Health",1f);Call(g,"Hurt",100f);Check(g.State==KryptaGame.Phase.Dead && !g.MeleeWindupActive,"Death cancels pending contact");g.ReviewCompleteStrike();Near(enemy.hp,10000,"No hit after player death");
   Reset(g,enemy);Call(g,"Strike",enemy.Position);Call(g,"ResetCombatActions");Check(!g.MeleeWindupActive,"Restart reset clears pending contact");
   Reset(g,enemy);Call(g,"Strike",enemy.Position);Call(g,"CreateHero");g.ReviewCompleteStrike();Near(enemy.hp,10000,"Replacement actor cannot inherit attack");Check(!g.MeleeWindupActive,"Replacement clears old actor queue");
   Reset(g,enemy);Call(g,"Strike",enemy.Position);Call(g,"LoadZone",Campaign.Crypt);Check(!g.MeleeWindupActive,"Even reloading same map clears attack");
   evidence.Add("Class "+hero+": contact at 42%, no early damage/VFX/resource, one hit, original cooldown, hitch recovery to Idle/Running retains one contact, new action identity cancels, escape/pause/dodge/defense/title/death/reload cancellation.");
  }
  static void EnemyChecks(KryptaGame g)
  {
   Check(Time.time>.0001f,"Enemy fixture has a positive scaled clock (impactAt zero means no attack)");
   var enemy=Fixture(g,0);enemy.visual.Root.transform.position=g.HeroPosition+Vector3.forward;
   enemy.impactAt=Time.time+.65f;Call(g,"BeginEnemyMelee",enemy,g.HeroPosition);
   float hp=g.Health;bool ready=false;
   for(int i=1;i<=11;i++)ready=(bool)Call(g,"AdvanceEnemyMelee",enemy,Time.time+i*.05f);
   Check(!ready,"Enemy has no contact while starting visible windup");Check(enemy.visual.CurrentClip=="1H_Melee_Attack_Chop","Enemy starts chop before damage");Near(g.Health,hp,"No damage at enemy animation start");
   float at=enemy.impactAt;Check(!(bool)Call(g,"AdvanceEnemyMelee",enemy,at-.001f),"Enemy contact waits for threshold");Check((bool)Call(g,"AdvanceEnemyMelee",enemy,at),"Enemy contact ready at threshold");
   enemy.impactAt=Time.time;
   Check(enemy.impactAt>0 && !(bool)Call(g,"EnemyMeleeRecovering",enemy,Time.time),"Due attack enters contact branch rather than recovery");
   Check(g.State==KryptaGame.Phase.Playing && Vector3.Distance(enemy.Position,g.HeroPosition)<1.5f && (bool)Call(g,"Visible",enemy.Position,g.HeroPosition),"Contact fixture is playing, in reach and visible");
   Check(Time.time>=(float)Get(g,"dashEnd") && Time.time>=(float)Get(g,"hurtUntil") && g.Status.Count("shadow",Time.time)==0,"Contact fixture has no dodge or damage immunity");
   Call(g,"UpdateEnemy",enemy);Near(hp-g.Health,12,"Enemy damage occurs once at contact (clock="+Time.time+", pending="+enemy.impactAt+")");float after=g.Health;Call(g,"UpdateEnemy",enemy);Near(g.Health,after,"Enemy recovery cannot repeat damage");
   Reset(g,enemy);enemy.visual.Root.transform.position=g.HeroPosition+Vector3.forward;enemy.impactAt=Time.time+.65f;Call(g,"BeginEnemyMelee",enemy,g.HeroPosition);float original=enemy.impactAt;
   Check(!(bool)Call(g,"AdvanceEnemyMelee",enemy,Time.time+2),"A stalled frame cannot instantly complete warning");Check(enemy.impactAt>original+1.8f,"Unobserved enemy warning time is retained");
   enemy.stunUntil=Time.time+1;Call(g,"UpdateEnemy",enemy);Near(enemy.impactAt,0,"Stun cancels enemy attack");Near(g.Health,g.MaxHealth,"Stunned enemy causes no damage");
   Reset(g,enemy);enemy.visual.Root.transform.position=g.HeroPosition+Vector3.forward;enemy.impactAt=Time.time+.65f;Call(g,"BeginEnemyMelee",enemy,g.HeroPosition);
   for(int i=1;i<=11;i++)Call(g,"AdvanceEnemyMelee",enemy,Time.time+i*.05f);
   Check(enemy.visual.CurrentClip=="1H_Melee_Attack_Chop","Resurrection fixture is killed during its swing");
   Call(g,"Damage",enemy,999999f);Check(enemy.hp==0 && enemy.impactAt==0,"Death clears pending enemy contact");
   enemy.hp=enemy.maxHp*.5f;enemy.visual.Root.SetActive(true);enemy.visual.Play("Idle",true,0);enemy.attackAt=Time.time+1;
   hp=g.Health;Call(g,"UpdateEnemy",enemy);Near(g.Health,hp,"Revived enemy cannot replay a pre-death swing");
   evidence.Add("Enemy chop begins before impact, recovery has no duplicate damage, stun/death cancel, resurrection cannot replay contact, and a two-second hitch cannot consume unseen telegraph.");
  }
  static void Finish(bool passed,string message)
  {
   SessionState.SetBool(Session,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Directory.CreateDirectory("Logs");
   var report=new Report{passed=passed,assertions=checks,error=message,evidence=evidence.ToArray()};
   File.WriteAllText("Logs/motion-combat-qa.json",JsonUtility.ToJson(report,true));File.WriteAllText("Logs/motion-combat-qa-result.txt",(passed ? "PASS: " : "FAIL: ")+checks+" assertions. "+message+"\n"+string.Join("\n",evidence));
   EditorApplication.Exit(passed ? 0 : 1);
  }
 }
}
