using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Krypta
{
 // Explicit player-only review: never starts in normal play and never writes saves.
 public sealed class NocturnisUALRuntimeQA : MonoBehaviour
 {
  string output,error;float started;bool done;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  static void MarkTesting(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ual-review")>=0)KryptaGame.Testing=true;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Launch(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-ual-review")>=0)new GameObject("UAL runtime review opt-in").AddComponent<NocturnisUALRuntimeQA>();}
  IEnumerator Start()
  {
   started=Time.realtimeSinceStartup;Application.logMessageReceived+=Log;
   var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-ual-output");
   output=Path.GetFullPath(i>=0 && i+1<args.Length ? args[i+1] : "Logs/UALRuntime");Directory.CreateDirectory(output);
   KryptaGame game;while(!(game=FindFirstObjectByType<KryptaGame>()) || !game.View)yield return null;
   game.enabled=false;var hud=game.GetComponent<KryptaHUD>();if(hud)hud.enabled=false;
   yield return game.CaptureUALRuntimeQA(output);
   if(error!=null){Fail(error);yield break;}
   done=true;Debug.Log("UAL_QA_COMPLETE");Application.Quit(0);
  }
  void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)error=message+"\n"+stack;}
  void Update(){if(done || output==null)return;if(error!=null)Fail(error);else if(Time.realtimeSinceStartup-started>240)Fail("UAL runtime review timeout");}
  void Fail(string message){done=true;StopAllCoroutines();File.WriteAllText(Path.Combine(output,"failure.txt"),message);Application.Quit(1);}
  void OnDestroy(){Application.logMessageReceived-=Log;}
 }

 public sealed partial class KryptaGame
 {
  [Serializable]sealed class UALRuntimeReport
  {public bool passed;public int assertions;public List<UALHeroReport> heroes=new List<UALHeroReport>();public List<string> evidence=new List<string>();}
  [Serializable]sealed class UALGaitReport
  {public string expectedClip,actualClip;public int frames;public float distance,elapsed,speed,cycleProgress,clipRate;}
  [Serializable]sealed class UALHeroReport
  {
   public int hero;public string model;public float walkStridePerHeight,runStridePerHeight,idleMotion,deathMotion,deathClipSeconds;
   public bool interactionAvailable;public int interactionFrames;public float interactionWaitSeconds,interactionClipSeconds;
   public string interactionClip,basicAttackClip;public UALGaitReport walk,run;public List<string> captures=new List<string>();
  }
  UALRuntimeReport ualReport;string ualOutput;
  void UALCheck(bool condition,string message)
  {
   ualReport.assertions++;
   if(condition)return;
   File.WriteAllText(Path.Combine(ualOutput,"results.json"),JsonUtility.ToJson(ualReport,true));
   throw new Exception("UAL runtime QA: "+message);
  }
  Vector3 UALClearOrigin()
  {
   for(int z=2;z<DungeonGrid.Height-3;z++)for(int x=2;x<DungeonGrid.Width-5;x++)
   {
    Vector3 p=World(x,z);
    if(grid.CanTravel(p.x/Tile,p.z/Tile,(p.x+8)/Tile,p.z/Tile,.28f/Tile)
     && grid.CanTravel(p.x/Tile,p.z/Tile,p.x/Tile,(p.z+2)/Tile,.3f/Tile))
     // Use the interior of the verified eight-metre lane; its first tile
     // is next to the crypt brazier and obscures the close-up review pose.
     return p+Vector3.right*2f;
   }
   throw new Exception("UAL runtime QA: No clear review lane");
  }
  void UALFixture(int hero)
  {
   State=Phase.Title;Time.timeScale=1;ResetShrineUsage();SelectHero(hero);LoadZone(Campaign.Crypt);State=Phase.Playing;
   ResetCombatActions();Status.Clear();introUntil=0;Health=MaxHealth;Resource=SelectedHero==3?0:100;
   foreach(var enemy in enemies){enemy.hp=0;enemy.visual.Root.SetActive(false);}
   player.Root.transform.position=UALClearOrigin();player.Root.transform.rotation=Quaternion.identity;facing=Vector3.forward;
   target=null;propTarget=null;hasDestination=false;
  }
  IEnumerator UALWarm()
  {for(int i=0;i<2;i++){yield return null;player.Tick(false);}yield return null;}
  static Vector3[] UALBonePositions(ActorVisual actor)
  {
   var skin=actor.Root.GetComponentInChildren<SkinnedMeshRenderer>();var points=new Vector3[skin.bones.Length];
   for(int i=0;i<points.Length;i++)points[i]=skin.bones[i].position;return points;
  }
  static float UALPoseMotion(ActorVisual actor,Vector3[] before)
  {
   var skin=actor.Root.GetComponentInChildren<SkinnedMeshRenderer>();float motion=0;
   for(int i=0;i<before.Length;i++)motion=Mathf.Max(motion,Vector3.Distance(before[i],skin.bones[i].position));return motion;
  }
  void UALCapture(UALHeroReport report,string suffix,Vector3 center)
  {
   string name="class-"+report.hero+"-"+suffix+".png";
   var oldTarget=View.targetTexture;var oldActive=RenderTexture.active;float oldSize=View.orthographicSize;
   var oldPosition=View.transform.position;var oldRotation=View.transform.rotation;int oldMask=View.cullingMask;
   var texture=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32);
   var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
   try
   {
    View.cullingMask=~(1<<30);View.orthographicSize=2.15f;View.transform.position=center+new Vector3(3.3f,2.7f,4.8f);
    View.transform.LookAt(center+Vector3.up*.92f);View.targetTexture=texture;View.Render();RenderTexture.active=texture;
    pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);pixels.Apply(false,false);
    File.WriteAllBytes(Path.Combine(ualOutput,name),pixels.EncodeToPNG());report.captures.Add(name);
   }
   finally
   {
    View.targetTexture=oldTarget;RenderTexture.active=oldActive;View.orthographicSize=oldSize;View.cullingMask=oldMask;
    View.transform.SetPositionAndRotation(oldPosition,oldRotation);Destroy(pixels);RenderTexture.ReleaseTemporary(texture);
   }
  }
  IEnumerator UALGait(UALHeroReport report,bool running)
  {
   Vector3 origin=UALClearOrigin();player.Root.transform.position=origin;player.Play("Idle",true,0);yield return UALWarm();
   var evidence=new UALGaitReport{expectedClip=running?"Running_A":"Walking_A"};if(running)report.run=evidence;else report.walk=evidence;
   float start=Time.time,deadline=start+2.5f,firstPhase=0;int consecutive=0;bool sampling=false;
   float wantedSpeed=running?Hero.speed:1.5f;var anim=player.Root.GetComponentInChildren<Animation>();
   while(Time.time<deadline)
   {
    MovePlayer(Vector3.right*(wantedSpeed/Hero.speed),Time.deltaTime,float.PositiveInfinity);
    evidence.frames++;evidence.distance=Vector3.Distance(origin,HeroPosition);evidence.elapsed=Time.time-start;
    evidence.actualClip=player.CurrentClip;evidence.speed=player.LocomotionSpeed;
    if(evidence.actualClip==evidence.expectedClip)
    {
     consecutive++;evidence.clipRate=anim[evidence.actualClip].speed;
     if(!sampling){firstPhase=player.CurrentClipNormalizedTime;sampling=true;}
     evidence.cycleProgress=player.CurrentClipNormalizedTime-firstPhase;
    }
    else consecutive=0;
    if(evidence.distance>.85f && consecutive>=3 && evidence.cycleProgress>.12f)break;
    yield return null;
   }
   UALCheck(evidence.distance>.85f && consecutive>=3 && evidence.cycleProgress>.12f,
    "Class "+report.hero+" "+evidence.expectedClip+": distance="+evidence.distance+" actual="+evidence.actualClip+" frames="+evidence.frames+" elapsed="+evidence.elapsed+" cycle="+evidence.cycleProgress);
   UALCheck(Mathf.Abs(evidence.speed-wantedSpeed)<.25f,"Class "+report.hero+" gait follows real displacement: speed="+evidence.speed+" expected="+wantedSpeed);
   UALCapture(report,running?"running":"walking",HeroPosition);
   // A fresh stationary sample must stop the gait despite held animation phase.
   yield return null;MovePlayer(Vector3.zero,Time.deltaTime,float.PositiveInfinity);
   UALCheck(player.LocomotionSpeed<.01f && player.CurrentClip=="Idle","Class "+report.hero+" feet stop with displacement");
  }
  public IEnumerator CaptureUALRuntimeQA(string directory)
  {
   if(!Testing)yield break;ualOutput=directory;ualReport=new UALRuntimeReport();ResetCampaign();
   // Explicit class contract: only the Inquisitor's basic strike changes to UAL.
   string[] basicAttackClips={"1H_Melee_Attack_Chop","1H_Ranged_Shoot","1H_Ranged_Shoot","1H_Ranged_Shoot","1H_Melee_Attack_Slice_Horizontal"};
   for(int hero=0;hero<5;hero++)
   {
    UALFixture(hero);yield return UALWarm();
    var character=art.heroes[hero];var profile=character.model.GetComponent<ActorModelProfile>();
    var report=new UALHeroReport{hero=hero,model=character.model.name,walkStridePerHeight=profile?profile.walkStridePerHeight:0,runStridePerHeight=profile?profile.runStridePerHeight:0};ualReport.heroes.Add(report);
    UALCheck(profile && report.walkStridePerHeight>0 && report.runStridePerHeight>0,"Class "+hero+" supplies positive stride metadata");
    var animation=player.Root.GetComponentInChildren<Animation>();report.interactionAvailable=animation["Interact_A"]!=null;
    // Exercise the same unscaled preview path as the title portrait, on its own
    // actor, so a sampled zero-speed Idle cannot contaminate gameplay playback.
    Time.timeScale=0;Vector3 previewOrigin=HeroPosition;player.Root.SetActive(false);
    var preview=new ActorVisual(character.model,character.animations,previewOrigin,1.8f,"UAL paused portrait");preview.Root.transform.SetParent(world);preview.Equip(character.weapon);
    preview.PreviewIdle(.15f);var pose=UALBonePositions(preview);preview.PreviewIdle(.70f);report.idleMotion=UALPoseMotion(preview,pose);
    UALCheck(report.idleMotion>.0001f && Vector3.Distance(previewOrigin,preview.Root.transform.position)<.00001f,"Class "+hero+" portrait breathes on paused clock without root drift: "+report.idleMotion);
    UALCapture(report,"idle",previewOrigin);preview.Root.SetActive(false);Destroy(preview.Root);player.Root.SetActive(true);Time.timeScale=1;
    yield return UALGait(report,false);yield return UALGait(report,true);
    player.Root.transform.position=UALClearOrigin();player.Play("Idle",true,0);ResetCombatActions();Status.Clear();yield return UALWarm();
    if(report.interactionAvailable)
    {
     Vector3 origin=HeroPosition;float speed=PlayerMovementSpeed;
     UALCheck(PlayInteractionAt(origin+Vector3.forward*2),"Class "+hero+" optional interaction starts");
     UALCheck(!player.MovementCommitted && Mathf.Abs(PlayerMovementSpeed-speed)<.001f,"Interaction adds no movement commitment");
     // CrossFade is evaluated by Unity's animation update, which can follow
     // the coroutine on its first resumed frame. Observe a bounded real-clock
     // advance instead of assuming that one yield has evaluated the new state.
     float interactionStarted=Time.realtimeSinceStartup,interactionDeadline=interactionStarted+.25f;
     while(player.CurrentClip=="Interact_A" && player.CurrentClipTime<=.001f && Time.realtimeSinceStartup<interactionDeadline)
     {yield return null;report.interactionFrames++;}
     report.interactionWaitSeconds=Time.realtimeSinceStartup-interactionStarted;
     report.interactionClip=player.CurrentClip;report.interactionClipSeconds=player.CurrentClipTime;
     UALCheck(player.CurrentClip=="Interact_A" && player.CurrentClipTime>.001f && animation.IsPlaying("Interact_A"),
      "Interaction advances on the runtime clock: clip="+report.interactionClip+" time="+report.interactionClipSeconds+" frames="+report.interactionFrames+" elapsed="+report.interactionWaitSeconds);
     UALCapture(report,"interact",HeroPosition);
     // Re-prime after the synchronous screenshot, then restart the optional
     // gesture; screenshot encoding time is not movement latency.
     player.Play("Idle",true,0);yield return UALWarm();
     UALCheck(PlayInteractionAt(HeroPosition+Vector3.forward*2),"Interaction can restart after idle");
     origin=HeroPosition;MovePlayer(Vector3.right,Time.deltaTime,float.PositiveInfinity);
     UALCheck(Vector3.Distance(origin,HeroPosition)>.001f && (player.CurrentClip=="Walking_A" || player.CurrentClip=="Running_A"),"First movement sample cancels interaction into a gait");
     // Test the actual user entry point once; repeated E must not replay a used shrine.
     player.Play("Idle",true,0);ResetCombatActions();Status.Clear();
     Shrine chosen=null;Vector3 at=Vector3.zero;
     foreach(var shrine in shrines)
     {
      if(shrine.used)continue;
      foreach(var direction in new[]{Vector3.back,Vector3.forward,Vector3.left,Vector3.right})
      {
       Vector3 p=shrine.pos+direction*.75f;
       if(grid.CanStand(p.x/Tile,p.z/Tile,.28f/Tile) && NearbyCitizen(p)==null){chosen=shrine;at=p;break;}
      }
      if(chosen!=null)break;
     }
     UALCheck(chosen!=null,"A reachable unused shrine exists for interaction QA");player.Root.transform.position=at;yield return UALWarm();
     Interact();UALCheck(chosen.used && player.CurrentClip=="Interact_A","Successful shrine interaction plays gesture");
     int sequence=player.ActionSequence;Interact();UALCheck(player.ActionSequence==sequence,"Used shrine cannot retrigger gesture or reward");
    }
    // Missing optional clips are a supported fallback (older enemies/actors).
    var withoutGesture=Array.FindAll(character.animations,clip=>clip && clip.name!="Interact_A");
    var legacy=new ActorVisual(character.model,withoutGesture,new Vector3(1000,0,1000),1.8f,"UAL optional-clip fallback");
    int oldSequence=legacy.ActionSequence;UALCheck(!legacy.TryPlayInteraction() && legacy.ActionSequence==oldSequence && legacy.CurrentClip=="Idle","Missing optional gesture leaves actor unchanged");Destroy(legacy.Root);
    player.Root.transform.position=UALClearOrigin();player.Play("Idle",true,0);ResetCombatActions();Status.Clear();yield return UALWarm();
    Strike(HeroPosition+Vector3.forward*2);var pending=pendingMeleeStrike;int protectedSequence=player.ActionSequence;string protectedClip=player.CurrentClip;
    UALCheck(pending!=null && !PlayInteractionAt(HeroPosition+Vector3.right),"Interaction cannot interrupt an active strike");
    UALCheck(pendingMeleeStrike==pending && player.ActionSequence==protectedSequence && player.CurrentClip==protectedClip,"Interaction preserves attack identity, pose and delayed contact");
    float showAt=Time.time+.10f;
    while(Time.time<showAt){yield return null;TickMeleeActions(Time.time);}
    float attackClockDeadline=Time.realtimeSinceStartup+.25f;
    while(player.CurrentClip==basicAttackClips[hero] && player.CurrentClipTime<=.001f && Time.realtimeSinceStartup<attackClockDeadline)yield return null;
    report.basicAttackClip=player.CurrentClip;
    UALCheck(report.basicAttackClip==basicAttackClips[hero] && animation.IsPlaying(basicAttackClips[hero]) && player.CurrentClipTime>.001f,
     "Class "+hero+" actual basic strike plays "+basicAttackClips[hero]+": actual="+report.basicAttackClip+" time="+player.CurrentClipTime);
    UALCapture(report,"attack",HeroPosition);
    ResetCombatActions();player.Play("Idle",true,0);Resource=SelectedHero==3?0:100;CastSkill(HeroPosition+Vector3.forward*3);protectedSequence=player.ActionSequence;
    UALCheck(!PlayInteractionAt(HeroPosition+Vector3.right) && player.ActionSequence==protectedSequence,"Interaction cannot replace a committed spell");
    showAt=Time.time+.10f;while(Time.time<showAt)yield return null;
    UALCapture(report,"cast",HeroPosition);
    ResetCombatActions();player.Play("Idle",true,0);BeginPlayerDodge(Vector3.forward);protectedSequence=player.ActionSequence;
    UALCheck(!PlayInteractionAt(HeroPosition+Vector3.right) && player.ActionSequence==protectedSequence,"Interaction cannot replace a dodge");
    // Player death freezes simulation; its animation must finish on the same
    // unscaled path used by KryptaGame.LateUpdate, without moving the world root.
    ResetCombatActions();Status.Clear();player.Play("Idle",true,0);Health=1;Vector3 deathOrigin=HeroPosition;
    UALCheck(Hurt(999999) && State==Phase.Dead && Time.timeScale==0,"Actual fatal damage enters paused death");
    pose=UALBonePositions(player);float end=Time.realtimeSinceStartup+.70f;
    while(Time.realtimeSinceStartup<end){yield return null;player.AdvancePausedAnimation(Time.unscaledDeltaTime);}
    report.deathClipSeconds=player.CurrentClipTime;report.deathMotion=UALPoseMotion(player,pose);
    UALCheck(report.deathClipSeconds>.2f && report.deathMotion>.01f,"Class "+hero+" death advances while paused: time="+report.deathClipSeconds+" motion="+report.deathMotion);
    UALCheck(Vector3.Distance(deathOrigin,HeroPosition)<.00001f,"Death animation does not translate gameplay root");
    UALCapture(report,"death",HeroPosition);Time.timeScale=1;State=Phase.Playing;
   }
   ualReport.evidence.Add("Five classes: title preview while paused, walking/running on real frame clocks, gait stopping and paused player death.");
   ualReport.evidence.Add("Optional interaction: real shrine entry point, one use, no movement lock, immediate movement cancellation and missing-clip fallback.");
   ualReport.evidence.Add("Interaction cannot cancel pending melee contact, a committed spell or a dodge. Existing movement/combat QA remains separate.");
   ualReport.passed=true;File.WriteAllText(Path.Combine(ualOutput,"results.json"),JsonUtility.ToJson(ualReport,true));
  }
 }
}
