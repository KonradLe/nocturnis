using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Krypta.Editor
{
 public static class NocturnisStageGuardiansQA
 {
  const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
  static double started;static int checks;static bool errors;
  static object Call(KryptaGame g,string name,params object[] args){return typeof(KryptaGame).GetMethod(name,Private).Invoke(g,args);}
  static object Get(object o,string name){return o.GetType().GetField(name).GetValue(o);}
  static void Set(object o,string name,object value){o.GetType().GetField(name).SetValue(o,value);}
  static void ShiftSample(object state,float seconds)
  {
   // Advancing every stored timestamp backwards is equivalent to advancing Unity's
   // scaled clock, without sleeping or changing the global clock for other actors.
   foreach(string field in new[]{"started","activeAt","endAt","lastSample"})Set(state,field,(float)Get(state,field)-seconds);
  }
  static Vector3 OpenTestFloor(KryptaGame game)
  {
   for(int z=3;z<DungeonGrid.Height-3;z++)for(int x=3;x<DungeonGrid.Width-3;x++)
   {bool open=true;for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)open&=game.MapFloor(x+dx,z+dz);if(open)return new Vector3(x*1.8f,0,z*1.8f);}
   throw new Exception("Guardian timing QA requires an open 3x3 floor patch");
  }
  static void CheckHitch(KryptaGame game,KryptaGame.Enemy boss,int zone,Action<bool,string> check)
  {
   Call(game,"ResetStageGuardians");Call(game,"ResetCombatActions");game.Status.Clear();
   typeof(KryptaGame).GetProperty("Health").SetValue(game,game.MaxHealth);
   var player=(ActorVisual)typeof(KryptaGame).GetField("player",Private).GetValue(game);
   Vector3 origin=OpenTestFloor(game);boss.visual.Root.transform.position=origin;boss.hp=boss.maxHp;boss.stunUntil=0;
   player.Root.transform.position=origin+Vector3.forward*1.6f;
   check(game.ReviewStartGuardianAttack(boss,player.Root.transform.position),"Hitch test starts a real guardian in "+zone);
   var states=(IDictionary)typeof(KryptaGame).GetField("stageGuardians",Private).GetValue(game);object state=states[boss];
   float before=game.Health,windup=game.GuardianWindupRemaining(boss);
   ShiftSample(state,1.3f);Call(game,"UpdateSpecialEnemy",boss);
   check(game.GuardianPhase(boss)==1 && game.Health==before,"1.3-second hitch cannot skip an unseen warning or hurt the player in "+zone);
   check(Mathf.Abs(game.GuardianWindupRemaining(boss)-(windup-.1f))<.002f,"Hitch consumes only an observable 0.1-second warning step in "+zone);
   Vector3 dir=(Vector3)Get(state,"direction"),safe=origin+Vector3.Cross(dir,Vector3.up)*2;
   Vector3 unsafePoint=zone==14 ? origin+dir*.5f : zone==15 ? origin-dir*.8f : zone==18 ? origin+Quaternion.Euler(0,-32,0)*dir*2 : (Vector3)Get(state,"target");
   if(zone==15)safe=origin+dir*1.8f;else if(zone==16 || zone==17)safe=origin-dir*2;else if(zone==18)safe=origin;
   player.Root.transform.position=unsafePoint;
   for(int i=0;i<40 && game.GuardianPhase(boss)==1;i++){ShiftSample(state,.05f);Call(game,"UpdateSpecialEnemy",boss);}
   check(game.GuardianPhase(boss)==2 && game.Health<before && (bool)Get(state,"hit"),"Preserved warning leads to one actual hit in "+zone);
   float afterHit=game.Health;
   typeof(KryptaGame).GetField("hurtUntil",Private).SetValue(game,0f);
   ShiftSample(state,1.3f);Call(game,"UpdateSpecialEnemy",boss);
   check(game.GuardianPhase(boss)==2 && game.Health==afterHit,"A second hitch preserves active presentation without a duplicate hit in "+zone);
   for(int i=0;i<50 && game.GuardianPhase(boss)==2;i++)
   {typeof(KryptaGame).GetField("hurtUntil",Private).SetValue(game,0f);ShiftSample(state,.05f);Call(game,"UpdateSpecialEnemy",boss);}
   check(game.GuardianPhase(boss)==0 && game.ActiveStageGuardianCasts==0 && game.Health==afterHit,"Attack resolves once and releases its lock after observed active time in "+zone);

   // The player left the warned area during the hitch. Never infer damage from
   // the old position, and do not turn a missed one-shot into a lingering trap.
   Call(game,"ResetStageGuardians");Call(game,"ResetCombatActions");boss.visual.Root.transform.position=origin;
   player.Root.transform.position=origin+Vector3.forward*1.6f;
   check(game.ReviewStartGuardianAttack(boss,player.Root.transform.position),"Safe-exit hitch attack starts in "+zone);
   state=states[boss];before=game.Health;
   for(int i=0;i<40 && game.GuardianWindupRemaining(boss)>.075f;i++){ShiftSample(state,.05f);Call(game,"UpdateSpecialEnemy",boss);}
   player.Root.transform.position=safe;ShiftSample(state,1.3f);Call(game,"UpdateSpecialEnemy",boss);
   check(game.GuardianPhase(boss)==2 && game.Health==before,"Leaving the area before a hitch resolves causes no retrospective damage in "+zone);
   if(zone==16 || zone==17)
   {
    check((bool)Get(state,"resolved") && !(bool)Get(state,"hit"),"Missed instantaneous strike resolves explicitly in "+zone);
    player.Root.transform.position=(Vector3)Get(state,"target");ShiftSample(state,.05f);Call(game,"UpdateSpecialEnemy",boss);
    check(game.Health==before,"Entering a resolved ritual/hammer footprint is safe in "+zone);
   }
   Call(game,"ResetStageGuardians");
  }
  [InitializeOnLoadMethod]static void Restore()
  {if(SessionState.GetBool("Nocturnis.StageGuardiansQA",false)){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}}
  public static void Run()
  {EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool("Nocturnis.StageGuardiansQA",true);EditorApplication.isPlaying=true;}
  static void Log(string message,string stack,LogType type){if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert)errors=true;}
  static void Check(bool pass,string message){checks++;if(!pass)throw new Exception(message);}
  public static void RunChecks(KryptaGame game,Action<bool,string> check)
  {
   KryptaGame.Testing=true;if(game.State==KryptaGame.Phase.Title)game.Begin();else game.Resume();
   for(int zone=14;zone<=18;zone++)
   {
    Call(game,"LoadZone",zone);var boss=game.enemies.Find(e=>e.spawnId==25);var other=game.enemies.Find(e=>e.spawnId==22);
    check(boss!=null && other!=null && game.IsStageGuardian(boss) && game.IsStageGuardian(other),"Four site/stage guardians recognized in "+zone);
    check(!game.IsStageGuardian(game.enemies[0]),"Regular enemies retain original AI in "+zone);
    check(boss.maxHp>other.maxHp && boss.maxHp<other.maxHp*1.6f,"Final guardian HP is a modest increase in "+zone);
    check(game.ReviewStartGuardianAttack(boss,boss.Position+Vector3.forward*4),"Guardian starts a telegraphed attack in "+zone);
    check(game.GuardianPhase(boss)==1 && game.GuardianWindupRemaining(boss)>=1.15f,"Reaction window at least1.15seconds in "+zone);
    check(!game.ReviewStartGuardianAttack(other,game.HeroPosition) && game.ActiveStageGuardianCasts==1,"Only one guardian special can wind up at a time in "+zone);
    var states=(IDictionary)typeof(KryptaGame).GetField("stageGuardians",Private).GetValue(game);object state=states[boss];
    Vector3 origin=(Vector3)Get(state,"origin"),dir=(Vector3)Get(state,"direction");float radius=(float)Get(state,"radius");
    Vector3 unsafePoint,safePoint;
    switch(zone-14)
    {
     case 0:unsafePoint=origin+dir*2;safePoint=origin+Vector3.Cross(dir,Vector3.up)*2;break;
     case 1:unsafePoint=origin-dir*radius;safePoint=origin+dir*radius;break;
     case 2:unsafePoint=(Vector3)Get(state,"target");safePoint=origin-dir*5;break;
     case 3:unsafePoint=origin+dir*3;safePoint=origin-dir*2;break;
     default:unsafePoint=origin+Quaternion.Euler(0,-32,0)*dir*3;safePoint=origin;break;
    }
    check(game.GuardianDangerAt(boss,unsafePoint,true),"Telegraph unsafe region matches attack shape in "+zone);
    check(!game.GuardianDangerAt(boss,safePoint,true),"Attack retains an explicit safe region in "+zone);
    check(!game.GuardianDangerAt(boss,unsafePoint),"Windup cannot deal damage in "+zone);
    boss.stunUntil=Time.time+2;Call(game,"UpdateSpecialEnemy",boss);
    check(game.GuardianPhase(boss)==0 && game.ActiveStageGuardianCasts==0 && Get(state,"marker")==null,"Stun cancels telegraph and releases attack lock in "+zone);
    boss.stunUntil=0;check(game.ReviewStartGuardianAttack(boss,boss.Position+Vector3.forward*4),"Guardian can resume after interruption in "+zone);
    state=states[boss];Set(state,"activeAt",Time.time-.1f);Set(state,"endAt",Time.time+1f);Call(game,"UpdateSpecialEnemy",boss);
    check(game.GuardianPhase(boss)==2,"Windup transitions into active phase in "+zone);
    Set(state,"endAt",Time.time-.01f);Call(game,"UpdateSpecialEnemy",boss);
    check(game.GuardianPhase(boss)==0 && game.ActiveStageGuardianCasts==0,"Active attack ends in a damage opportunity in "+zone);
    check((float)Get(state,"recoverAt")>=Time.time+2.3f,"Recovery lasts at least2.3seconds in "+zone);
    game.ReviewStartGuardianAttack(boss,boss.Position+Vector3.forward*4);boss.hp=0;Call(game,"UpdateStageGuardianCleanup");
    check(game.ActiveStageGuardianCasts==0 && Get(state,"marker")==null,"Defeated guardian immediately loses marker and attack lock in "+zone);
    CheckHitch(game,boss,zone,check);
   }
   Call(game,"LoadZone",Campaign.Town);check(game.ActiveStageGuardianCasts==0,"Travel clears all guardian casts");
  }
  static void Tick()
  {
   if(!EditorApplication.isPlaying)return;if(EditorApplication.timeSinceStartup-started>180){Finish(false,"Timeout");return;}
   var game=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!game || !game.View)return;
   try{RunChecks(game,Check);Check(!errors,"No Unity runtime errors");Finish(true,checks+" assertions: five guardian patterns, real phases, 1.3-second hitches, single impacts, safe exits, attack lock, stun/recovery/death/travel cleanup.");}
   catch(Exception e){Finish(false,e.ToString());}
  }
  static void Finish(bool pass,string message)
  {SessionState.SetBool("Nocturnis.StageGuardiansQA",false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Directory.CreateDirectory("Logs");File.WriteAllText("Logs/stage-guardians-qa-result.txt",(pass ? "PASS: " : "FAIL: ")+message);Debug.Log(message);EditorApplication.Exit(pass ? 0 : 1);}
 }
}
