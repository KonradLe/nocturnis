using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Krypta
{
 // Explicit QA only. Tests actual ActorVisual clocks, collision, damage and revival.
 public sealed class NocturnisMovementQA : MonoBehaviour
 {
  string directory,error;float started;bool done;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  static void MarkTesting(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-movement-qa")>=0)KryptaGame.Testing=true;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Launch(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-movement-qa")>=0)new GameObject("Movement QA opt-in").AddComponent<NocturnisMovementQA>();}
  IEnumerator Start()
  {
   started=Time.realtimeSinceStartup;Application.logMessageReceived+=Log;
   var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-review-output");
   directory=Path.GetFullPath(i>=0 && i+1<args.Length ? args[i+1] : "Logs/MovementQA");Directory.CreateDirectory(directory);
   KryptaGame game;while(!(game=FindFirstObjectByType<KryptaGame>()) || !game.View)yield return null;
   game.enabled=false;var hud=game.GetComponent<KryptaHUD>();if(hud)hud.enabled=false;
   yield return game.CaptureMovementQA(directory);
   if(error!=null){Fail(error);yield break;}
   done=true;Application.Quit(0);
  }
  void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)error=message+"\n"+stack;}
  void Update(){if(done || directory==null)return;if(error!=null)Fail(error);else if(Time.realtimeSinceStartup-started>180)Fail("Movement QA timeout");}
  void Fail(string message){done=true;StopAllCoroutines();File.WriteAllText(Path.Combine(directory,"failure.txt"),message);Application.Quit(1);}
  void OnDestroy(){Application.logMessageReceived-=Log;}
 }

 public sealed partial class KryptaGame
 {
  [Serializable]sealed class MovementQAReport
  {
   public bool passed;public int assertions;public List<string> evidence=new List<string>();
   public List<MovementHeroEvidence> heroes=new List<MovementHeroEvidence>();
  }
  [Serializable]sealed class MovementHeroEvidence
  {public int hero;public float runDistance,runElapsed,runSpeed,maxFrameSeconds,contactSeconds,recoverySeconds,windupDrift;public int runFrames;public string runClip,recoveryClip;}
  MovementQAReport movementReport;string movementReportDirectory;
  void MotionCheck(bool condition,string message)
  {
   movementReport.assertions++;
   if(!condition)
   {
    File.WriteAllText(Path.Combine(movementReportDirectory,"movement-validation.json"),JsonUtility.ToJson(movementReport,true));
    throw new Exception("Movement QA: "+message);
   }
  }
  Vector3 MovementFixtureOrigin()
  {
   // Use the live collision grid, including containers. A valid starting cell
   // alone does not prove that a run, dodge or contact target has clear space.
   for(int z=2;z<DungeonGrid.Height-4;z++)for(int x=2;x<DungeonGrid.Width-5;x++)
   {
    Vector3 p=World(x,z);
    if(grid.CanTravel(p.x/Tile,p.z/Tile,(p.x+8)/Tile,p.z/Tile,.28f/Tile)
     && grid.CanTravel(p.x/Tile,p.z/Tile,p.x/Tile,(p.z+2)/Tile,.3f/Tile))return p;
   }
   throw new Exception("Movement QA: No clear 8 m run lane with contact clearance");
  }
  IEnumerator WarmMovementFixture()
  {
   // LoadZone and teleport are setup, not locomotion. Prime the distance clock
   // over two real frames so shader/upload stalls cannot consume the whole test.
   for(int frame=0;frame<2;frame++){yield return null;player.Tick(false);}
   yield return null;
  }
  Enemy MovementFixture(int hero)
  {
   State=Phase.Title;Time.timeScale=1;SelectHero(hero);LoadZone(Campaign.Crypt);State=Phase.Playing;
   ResetCombatActions();Status.Clear();introUntil=0;Health=MaxHealth;Resource=100;
   player.Root.transform.position=MovementFixtureOrigin();player.Root.transform.rotation=Quaternion.identity;facing=Vector3.forward;
   var enemy=enemies[0];foreach(var other in enemies){other.hp=0;other.visual.Root.SetActive(false);}
   enemy.hp=enemy.maxHp=10000;enemy.boss=enemy.elite=enemy.zombie=enemy.rewarded=false;enemy.archetype=0;
   enemy.visual.Root.SetActive(true);enemy.visual.Play("Idle",true,0);enemy.visual.Root.transform.position=HeroPosition+Vector3.forward*1.8f;
   enemy.corpseHideAt=enemy.impactAt=enemy.slowUntil=enemy.stunUntil=enemy.armorUntil=0;
   target=null;propTarget=null;hasDestination=false;
   MotionCheck(grid.CanStand(HeroPosition.x/Tile,HeroPosition.z/Tile,.28f/Tile),"Fixture on walkable floor");
   return enemy;
  }
  public IEnumerator CaptureMovementQA(string directory)
  {
   if(!Testing)yield break;movementReportDirectory=directory;movementReport=new MovementQAReport();
   for(int hero=0;hero<5;hero++)
   {
    var enemy=MovementFixture(hero);yield return WarmMovementFixture();
    Vector3 start=HeroPosition;float runStarted=Time.time,deadline=Time.time+1.5f;int runningFrames=0;
    var record=new MovementHeroEvidence{hero=hero};movementReport.heroes.Add(record);
    while(Time.time<deadline)
    {
     MovePlayer(Vector3.right,Time.deltaTime,float.PositiveInfinity);
     record.runFrames++;record.runDistance=Vector3.Distance(start,HeroPosition);record.runElapsed=Time.time-runStarted;
     record.runSpeed=player.LocomotionSpeed;record.runClip=player.CurrentClip;record.maxFrameSeconds=Mathf.Max(record.maxFrameSeconds,Time.deltaTime);
     runningFrames=record.runClip=="Running_A" && record.runSpeed>2.55f ? runningFrames+1 : 0;
     if(record.runDistance>.8f && runningFrames>=2)break;
     yield return null;
    }
    MotionCheck(record.runDistance>.8f && runningFrames>=2,
     "Class "+hero+" actual movement enters run: distance="+record.runDistance+" clip="+record.runClip+" speed="+record.runSpeed+" frames="+record.runFrames+" elapsed="+record.runElapsed+" maxFrame="+record.maxFrameSeconds);
    player.Root.transform.position=start;player.Play("Idle",true,0);yield return WarmMovementFixture();
    float hp=enemy.hp,started=Time.time;Strike(enemy.Position);
    MotionCheck(pendingMeleeStrike!=null,"Class "+hero+" accepts a ready attack");
    float due=pendingMeleeStrike.contactAt;var originalStrike=pendingMeleeStrike;int action=player.ActionSequence;
    MotionCheck(enemy.hp==hp,"No damage on input");
    Strike(enemy.Position);
    MotionCheck(pendingMeleeStrike==originalStrike && player.ActionSequence==action,"Holding attack cannot bypass cooldown or restart contact");
    while(MeleeWindupActive && Time.time-started<1)
    {
     MovePlayer(Vector3.right,Time.deltaTime,float.PositiveInfinity);
     record.windupDrift=Mathf.Max(record.windupDrift,Vector3.Distance(start,HeroPosition));
     record.maxFrameSeconds=Mathf.Max(record.maxFrameSeconds,Time.deltaTime);
     TickMeleeActions(Time.time);
     if(!MeleeWindupActive){record.contactSeconds=Time.time-started;break;}
     yield return null;
    }
    MotionCheck(record.windupDrift<.001f,"Class "+hero+" planted through actual contact: drift="+record.windupDrift);
    MotionCheck(!MeleeWindupActive && enemy.hp<hp && due>started+.1f,"Class "+hero+" normal delayed damage preserved");
    float afterHit=enemy.hp;deadline=Time.time+1;
    // The normal Update order moves, then resolves contact. Resume on its next
    // real frame, and measure before yielding rather than charging a render stall
    // to recovery. Also require the very first eligible frame to move.
    yield return null;
    while(Time.time<deadline)
    {
     MovePlayer(Vector3.right,Time.deltaTime,float.PositiveInfinity);TickMeleeActions(Time.time);
     record.maxFrameSeconds=Mathf.Max(record.maxFrameSeconds,Time.deltaTime);
     record.recoverySeconds=Time.time-started;record.recoveryClip=player.CurrentClip;
     bool moved=Vector3.Distance(start,HeroPosition)>.02f;
     MotionCheck(moved || Time.time<started+.28f || Time.deltaTime*Hero.speed<=.02f,
      "Class "+hero+" moves on the first eligible recovery frame: seconds="+record.recoverySeconds+" clip="+record.recoveryClip);
     if(moved)break;
     yield return null;
    }
    MotionCheck(Vector3.Distance(start,HeroPosition)>.02f && record.recoverySeconds<.40f+record.maxFrameSeconds,
     "Class "+hero+" responsive recovery: seconds="+record.recoverySeconds+" maxFrame="+record.maxFrameSeconds);
    MotionCheck(player.CurrentClip=="Running_A" || player.CurrentClip=="Walking_A","Movement has gait rather than frozen attack pose: clip="+player.CurrentClip+" speed="+player.LocomotionSpeed);
    MotionCheck(enemy.hp==afterHit,"Recovery cannot replay damage");
    ResetCombatActions();player.Play("Idle",true,0);Resource=100;Vector3 aim=HeroPosition+Vector3.forward*5;
    CastSkill(aim);Vector3 castStart=HeroPosition;
    MotionCheck(Vector3.Dot(player.Root.transform.forward,Vector3.forward)>.999f,"Class "+hero+" spell faces its target");
    MovePlayer(Vector3.right,.05f,float.PositiveInfinity);
    MotionCheck(Vector3.Distance(castStart,HeroPosition)<.001f,"Class "+hero+" cast begins with planted feet");
    BeginPlayerDodge(Vector3.right);
    MotionCheck(!MeleeWindupActive && player.CurrentClip=="Dodge_Forward" && PlayerMovementSpeed>0,"Dodge immediately cancels a committed cast");
    MovePlayer(Vector3.right*3.7f,.04f,float.PositiveInfinity);
    MotionCheck(Vector3.Distance(castStart,HeroPosition)>.45f,"Dodge retains its actual movement");
    yield return null;
   }
   yield return MovementWallAndDeathQA(directory);
   movementReport.passed=true;
   File.WriteAllText(Path.Combine(directory,"movement-validation.json"),JsonUtility.ToJson(movementReport,true));
   Debug.Log("MOVEMENT_QA_PASS assertions="+movementReport.assertions);
  }
  IEnumerator MovementWallAndDeathQA(string directory)
  {
   var enemy=MovementFixture(0);yield return WarmMovementFixture();bool found=false;Vector3 wallStart=Vector3.zero;
   for(int x=1;x<DungeonGrid.Width-1 && !found;x++)for(int z=1;z<DungeonGrid.Height-1 && !found;z++)
   {
    if(!grid.Walkable(x,z) || grid.Walkable(x+1,z))continue;
    Vector3 p=World(x,z)+Vector3.right*(Tile*.5f-.29f);
    if(!grid.CanStand(p.x/Tile,p.z/Tile,.28f/Tile) || !grid.CanTravel(p.x/Tile,p.z/Tile,p.x/Tile,(p.z+1.5f)/Tile,.28f/Tile))continue;
    wallStart=p;found=true;
   }
   MotionCheck(found,"Found a real wall with a clear parallel route");
   player.Root.transform.position=wallStart;player.Root.transform.rotation=Quaternion.LookRotation(Vector3.right);
   for(int frame=0;frame<4;frame++){MovePlayer(new Vector3(1,0,1).normalized,.08f,float.PositiveInfinity);yield return null;}
   Vector3 travelled=HeroPosition-wallStart;
   MotionCheck(Mathf.Abs(travelled.x)<.04f && travelled.z>.7f,"Collision slides along real wall without crossing it");
   MotionCheck(Vector3.Dot(player.Root.transform.forward,travelled.normalized)>.97f,"Wall travel turns toward actual movement");
   Quaternion before=player.Root.transform.rotation;MovePlayer(Vector3.right,.1f,float.PositiveInfinity);
   MotionCheck(Quaternion.Angle(before,player.Root.transform.rotation)<.01f,"Pushing a solid wall cannot rotate feet away from travel");
   MotionCheck(player.LocomotionSpeed<.05f,"Feet stop against a solid wall");
   Quaternion[] turns=new Quaternion[3];int[] rates={30,60,120};
   for(int r=0;r<rates.Length;r++)
   {
    player.Root.transform.rotation=Quaternion.identity;
    for(int i=0;i<rates[r]/2;i++)FaceTravel(player,Vector3.right,1f/rates[r]);
    turns[r]=player.Root.transform.rotation;
   }
   MotionCheck(Quaternion.Angle(turns[0],turns[1])<.05f && Quaternion.Angle(turns[1],turns[2])<.05f,"Equal turn after half a second at 30/60/120 fps");
   enemy=MovementFixture(0);yield return WarmMovementFixture();
   var second=enemies[1];second.hp=100;second.visual.Root.SetActive(true);
   enemy.visual.Root.transform.position=HeroPosition+Vector3.forward*.9f;
   second.visual.Root.transform.position=HeroPosition+Vector3.forward*.2f;
   MotionCheck(FindEnemyAtAim(HeroPosition)==second,"Overlapping target selection picks closest even when listed second");
   second.hp=0;MotionCheck(FindEnemyAtAim(HeroPosition)==enemy,"Dead overlapping target is ignored");
   MotionCheck(!ShouldCastPrimary(false,true,true),"Right mouse over HUD never casts into world");
   MotionCheck(ShouldCastPrimary(true,false,true) && ShouldCastPrimary(false,true,false),"Keyboard power and world right mouse remain usable");
   enemy.hp=1;enemy.visual.Root.transform.position=HeroPosition+Vector3.forward*1.8f;
   int defeatedBefore=Defeated;Damage(enemy,1000);float hideAt=enemy.corpseHideAt;
   MotionCheck(enemy.hp==0 && enemy.visual.Root.activeSelf && enemy.visual.CurrentClip=="Death_A","Killed enemy remains visible in death animation");
   MotionCheck(Defeated==defeatedBefore+1,"First death rewards once");
   TickDeadEnemy(enemy,hideAt-.01f);MotionCheck(enemy.visual.Root.activeSelf,"Corpse persists before deadline");
   var hips=Array.Find(enemy.visual.Root.GetComponentsInChildren<Transform>(),t=>t.name=="hips");
   Quaternion initial=hips ? hips.localRotation : Quaternion.identity;
   yield return new WaitForSeconds(.55f);
   MotionCheck(enemy.visual.CurrentClipTime>.3f,"Death clip advances on runtime animation clock");
   if(hips)MotionCheck(Quaternion.Angle(initial,hips.localRotation)>15,"Visible body actually falls");
   View.cullingMask=~(1<<30);View.transform.position=HeroPosition+new Vector3(9,15,-11);
   yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,"enemy-death-visible.png"));yield return null;
   var priest=enemies[1];priest.hp=priest.maxHp=100;priest.visual.Root.SetActive(true);priest.visual.Root.transform.position=enemy.Position+Vector3.right*4;
   priest.archetype=7;priest.boss=priest.elite=priest.sacrificial=false;priest.alerted=true;priest.stunUntil=0;priest.specialAt=Time.time-1;
   UpdateSpecialEnemy(priest);
   MotionCheck(enemy.hp>0 && enemy.visual.Root.activeSelf && enemy.corpseHideAt==0,"Actual priest resurrection cancels corpse timer");
   TickDeadEnemy(enemy,hideAt+10);MotionCheck(enemy.visual.Root.activeSelf,"Old death deadline cannot hide resurrected actor");
   int defeated=Defeated,experience=Experience;enemy.visual.Root.transform.position+=Vector3.forward*.5f;Vector3 lastPosition=enemy.Position;
   Damage(enemy,999999);
   MotionCheck(enemy.corpse==lastPosition,"Repeated death records current corpse location");
   MotionCheck(Defeated==defeated && Experience==experience,"Revived enemy cannot grant duplicate kill rewards");
   MotionCheck(enemy.visual.Root.activeSelf,"Repeated death also plays a visible fall");
   TickDeadEnemy(enemy,enemy.corpseHideAt+.01f);MotionCheck(!enemy.visual.Root.activeSelf,"Expired corpse hides cleanly");
   movementReport.evidence.Add("Five classes: actual gait, planted contact, delayed one-hit damage, cooldowns, responsive gait recovery, spell facing, immediate dodge cancellation.");
   movementReport.evidence.Add("Real wall sliding/facing, blocked foot cadence, frame-rate independent turns, closest living target and HUD mouse gate.");
   movementReport.evidence.Add("Visible animated death, corpse deadline, real priest resurrection, stale deadline safety, updated corpse position and no duplicate reward.");
  }
 }
}
