using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Krypta
{
 // This component does not exist in normal play. It issues normal gameplay actions;
 // it never writes HP, stats, kill flags, inventories, unlocks or actor positions.
 [DefaultExecutionOrder(1000)]
 public sealed class NocturnisPacingBot : MonoBehaviour
 {
  [Serializable] public sealed class ZoneSample
  {
   public int zone,kills,potionsUsed,startLevel,endLevel,stageObjectivesCompleted;public string name;
   public float startSim,endSim,seconds,startHealth,endHealth,walkingMetres;
  }
  [Serializable] public sealed class NavigationSample
  {
   public float seconds,targetHP,danger,goalDistance,requestedMetres,movedMetres,movementSpeed;
   public int zone,targetId=-1,bfs;public string mode,goalKind;
   public bool windup,directRoute,blocked;public Vector3 position,goal,waypoint,direction;
  }
  [Serializable] public sealed class Sample
  {
   public string result="running",failure="",startedUtc,heroName,limitation="Automated Road -> Town -> Fields -> Ravine entrance sample only. No claim of a full campaign or a one-hour human playthrough. Bot uses walking evasion, does not use dash or town upgrades.";
   public bool valid,completed,qaSaveIsolation,noTeleports=true,noStatInjection=true;
   public bool mainRouteOnly,sideQuestsExcluded;public int requiredMainRouteZones,visitedMainRouteZones,stageObjectivesCompleted;public string scope="opening-sample";
   public Vector3 position,goal;public float goalDistance,maxObservedDeltaTime;public int goalBfsDistance=-1;public string goalKind="";public bool canExit;
   public bool maximumDeltaTimeOverridden;public float requestedMaximumDeltaTime,configuredMaximumDeltaTime;
   public int frameCount,deltaTimeSpikeCount,maximumDeltaTimeExceededFrames;public float meanDeltaTime,p99DeltaTime;
   public float deltaTimeSpikeThreshold=.1f;public float[] deltaTimeBucketUpperBounds={.033f,.05f,.1f,.25f,.5f};public int[] deltaTimeHistogram=new int[6];
   public string deltaTimeSampleScope="Frames with advancing gameplay elapsed time after Begin; scaled deltaTime in seconds. Histogram buckets: <=.033, (.033,.05], (.05,.1], (.1,.25], (.25,.5], >.5. Spikes exceed .1 seconds. p99 is the nearest-rank percentile.";
   public int hero,zone,remaining,kills,deaths,attacks,skillCasts,potionsUsed,level,containersBroken;
   public float simulatedSeconds,wallSeconds,health,maxHealth,minHealthFraction=1,walkingMetres,simulationSpeed;
   public List<ZoneSample> zones=new List<ZoneSample>();
   public bool sharesPlayerMovement=true;public int targetSwitches,routeBlockedFrames,movementLimitViolations,windupMovementFrames;
   public float routingSeconds,evasionSeconds,holdingSeconds;
   public string navigationTraceScope="Last 120 half-second samples; routing, avoidance, kiting and attack holding are separate. Movement uses the player's status/windup/facing/collision pipeline.";
   public List<NavigationSample> navigationTrace=new List<NavigationSample>();
  }
  const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
  static bool Enabled
  {
   get
   {
    if(Array.IndexOf(Environment.GetCommandLineArgs(),"-act1-pacing-qa")>=0 || Array.IndexOf(Environment.GetCommandLineArgs(),"-pacing-full-campaign")>=0)return true;
#if UNITY_EDITOR
    return UnityEditor.SessionState.GetBool("Nocturnis.PacingQA",false);
#else
    return false;
#endif
   }
  }
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  static void Isolate(){if(Enabled)KryptaGame.Testing=true;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Launch()
  {if(Enabled && !FindFirstObjectByType<NocturnisPacingBot>())new GameObject("Opt-in pacing QA bot").AddComponent<NocturnisPacingBot>();}
  static string Arg(string key,string fallback)
  {var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,key);return at>=0 && at+1<args.Length ? args[at+1] : fallback;}
  static int IntArg(string key,int fallback){int n;return int.TryParse(Arg(key,""),out n)?n:fallback;}
  static float FloatArg(string key,float fallback){float n;return float.TryParse(Arg(key,""),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out n)?n:fallback;}
  KryptaGame game;DungeonGrid route,liveGrid;ActorVisual actor;
  readonly Dictionary<string,MethodInfo> methods=new Dictionary<string,MethodInfo>();
  readonly Dictionary<string,float> bestGoalDistances=new Dictionary<string,float>();
  readonly List<float> frameDeltaTimes=new List<float>();
  Sample report=new Sample();ZoneSample current;
  KryptaGame.Enemy pursued;float targetCommittedUntil,lastTraceAt=-1;
  string navigationMode="idle";Vector3 navigationWaypoint,navigationDirection;float requestedMetres,movedMetres,movementSpeed;
  bool movementWindup,movementBlocked;
  double deltaTimeSum;float originalMaximumDeltaTime,lastSampledElapsed;bool restoreMaximumDeltaTime;string timingConfigurationError;
  int hero,lastZone=-1,baseKills,basePotions;float speed,maxWall,maxSim,startedWall,bootWall,startedSim,lootUntil=-1,lastWrite,lastProgress,lastHPsum,navAt;
  Vector3 previousPosition,progressPosition;string output;bool began,finished,fullCampaign;int navX,navZ;
  void Awake()
  {
   bootWall=Time.realtimeSinceStartup;hero=Mathf.Clamp(IntArg("-pacing-hero",0),0,4);
   fullCampaign=Array.IndexOf(Environment.GetCommandLineArgs(),"-pacing-full-campaign")>=0;
   speed=Mathf.Clamp(FloatArg("-pacing-speed",4),1,4);maxWall=Mathf.Clamp(FloatArg("-pacing-max-wall",fullCampaign ? 900 : 180),20,fullCampaign ? 900 : 180);maxSim=Mathf.Clamp(FloatArg("-pacing-max-sim",fullCampaign ? 7200 : 720),60,fullCampaign ? 7200 : 720);
   output=Arg("-pacing-output",Path.Combine("Logs","Pacing",(fullCampaign ? "main-route-hero-" : "hero-")+hero+".json"));
   report.hero=hero;report.heroName=NocturnisData.Heroes[hero].name;report.simulationSpeed=speed;report.qaSaveIsolation=KryptaGame.Testing;
   ConfigureMaximumDeltaTime();
   if(fullCampaign){report.mainRouteOnly=report.sideQuestsExcluded=true;report.requiredMainRouteZones=Campaign.Route.Length;report.scope="complete-main-route-only";report.limitation="Automated main route only: Road through all five event stages, cemetery defense and Malazar. All combat, walking, healing, looting and E interactions use normal game actions. Five side quests and optional branches are excluded. No dash or town upgrades; no claim of a full completionist or human one-hour playthrough.";}
  }
  void ConfigureMaximumDeltaTime()
  {
   originalMaximumDeltaTime=Time.maximumDeltaTime;report.configuredMaximumDeltaTime=originalMaximumDeltaTime;
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"-pacing-max-delta")<0)return;
   float value;
   if(!float.TryParse(Arg("-pacing-max-delta",""),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value) || float.IsNaN(value) || float.IsInfinity(value) || value<=0)
   {timingConfigurationError="-pacing-max-delta requires a positive finite number of seconds";return;}
   report.requestedMaximumDeltaTime=value;report.maximumDeltaTimeOverridden=restoreMaximumDeltaTime=true;
   Time.maximumDeltaTime=value;report.configuredMaximumDeltaTime=Time.maximumDeltaTime;
  }
  void RestoreMaximumDeltaTime()
  {if(restoreMaximumDeltaTime){Time.maximumDeltaTime=originalMaximumDeltaTime;restoreMaximumDeltaTime=false;}}
  void OnDestroy(){RestoreMaximumDeltaTime();}
  void RecordFrameTiming()
  {
   // Exclude the title/Begin frame and paused UI frames: they are not gameplay steps.
   if(game.Elapsed<=lastSampledElapsed)return;lastSampledElapsed=game.Elapsed;
   float dt=Time.deltaTime;if(dt<=0 || float.IsNaN(dt) || float.IsInfinity(dt))return;
   frameDeltaTimes.Add(dt);deltaTimeSum+=dt;report.frameCount++;
   report.maxObservedDeltaTime=Mathf.Max(report.maxObservedDeltaTime,dt);report.meanDeltaTime=(float)(deltaTimeSum/report.frameCount);
   int bucket=0;while(bucket<report.deltaTimeBucketUpperBounds.Length && dt>report.deltaTimeBucketUpperBounds[bucket])bucket++;
   report.deltaTimeHistogram[bucket]++;
   if(dt>report.deltaTimeSpikeThreshold)report.deltaTimeSpikeCount++;
   if(dt>report.configuredMaximumDeltaTime+.00001f)report.maximumDeltaTimeExceededFrames++;
  }
  object Read(object instance,string field){return instance.GetType().GetField(field,Flags).GetValue(instance);}
  object Call(string method,params object[] args)
  {MethodInfo m;if(!methods.TryGetValue(method,out m)){m=typeof(KryptaGame).GetMethod(method,Flags);if(m==null)throw new MissingMethodException(method);methods.Add(method,m);}return m.Invoke(game,args);}
  bool Visible(Vector3 a,Vector3 b){return (bool)Call("Visible",a,b);}
  void Update()
  {
   if(finished)return;
   try{Step();}catch(Exception error){Finish("error",error.GetBaseException().ToString());}
  }
  void Step()
  {
   if(timingConfigurationError!=null){Finish("error",timingConfigurationError);return;}
   if(!game)game=FindFirstObjectByType<KryptaGame>();
   if(!game || !game.View || !game.enabled){if(Time.realtimeSinceStartup-bootWall>35)Finish("error","Game did not become ready");return;}
   if(!began)
   {
    if(game.State!=KryptaGame.Phase.Title){Finish("error","Pacing QA must start from a fresh title, without preview/autoplay flags");return;}
    KryptaGame.Testing=true;game.SelectHero(hero);game.Begin();began=true;
    startedWall=Time.realtimeSinceStartup;startedSim=lastSampledElapsed=game.Elapsed;report.startedUtc=DateTime.UtcNow.ToString("o");previousPosition=progressPosition=game.HeroPosition;
    report.qaSaveIsolation=KryptaGame.Testing;lastProgress=game.Elapsed;Time.timeScale=speed;
   }
   report.wallSeconds=Time.realtimeSinceStartup-startedWall;report.simulatedSeconds=game.Elapsed-startedSim;
   report.zone=game.Zone;report.remaining=game.Remaining;report.kills=game.Defeated;report.health=game.Health;report.maxHealth=game.MaxHealth;report.level=game.Level;
   report.position=game.HeroPosition;report.canExit=game.CanExit;RecordFrameTiming();
   report.minHealthFraction=Mathf.Min(report.minHealthFraction,game.Health/Mathf.Max(1,game.MaxHealth));
   if(game.Zone!=lastZone)EnterZone();
   else {float distance=Vector3.Distance(previousPosition,game.HeroPosition);report.walkingMetres+=distance;if(current!=null)current.walkingMetres+=distance;}
   previousPosition=game.HeroPosition;
   if(game.State==KryptaGame.Phase.Dead){report.deaths++;Finish("dead");return;}
   if(!fullCampaign && game.Zone==Campaign.Ravine){Finish("completed");return;}
   if(fullCampaign && game.State==KryptaGame.Phase.Won)
   {if(game.Zone==Campaign.Heart && report.visitedMainRouteZones==Campaign.Route.Length && report.stageObjectivesCompleted==15)Finish("completed");else Finish("error","Won state without all main-route zones and fifteen stage objectives");return;}
   if(report.wallSeconds>=maxWall){Finish("wall_timeout");return;}
   if(report.simulatedSeconds>=maxSim){Finish("simulation_timeout");return;}
   if(game.State==KryptaGame.Phase.Dialogue)
   {if(game.Service=="lore" && game.QuestAccepted || fullCampaign && (game.Service=="stage" || game.Service=="grave")){game.Resume();Time.timeScale=speed;}else Finish("error","Unexpected dialogue "+game.Service);return;}
   if(game.State!=KryptaGame.Phase.Playing){Finish("error","Unexpected phase "+game.State);return;}
   Time.timeScale=speed;
   actor=(ActorVisual)Read(game,"player");liveGrid=(DungeonGrid)Read(game,"grid");
   navigationMode="hold-attack";navigationWaypoint=game.HeroPosition;navigationDirection=Vector3.zero;
   requestedMetres=movedMetres=movementSpeed=0;movementWindup=movementBlocked=false;
   if(game.Health<game.MaxHealth*.35f && game.UsePotion())report.potionsUsed++;
   if(game.Zone==Campaign.Town)
   {
    Vector3 goal=game.QuestAccepted?game.ExitPosition:new Vector3(15*1.8f,0,8*1.8f);
    if(Vector3.Distance(game.HeroPosition,goal)<2.2f)InteractReal();else WalkTo(goal,game.QuestAccepted ? "town-exit" : "malakor");
   }
   else FightAndAdvance();
   RecordNavigation();
   float hp=0;foreach(var e in game.enemies)hp+=Mathf.Max(0,e.hp);
   if(fullCampaign){if(hp<lastHPsum-.1f)lastProgress=game.Elapsed;lastHPsum=hp;}
   else if(Vector3.Distance(game.HeroPosition,progressPosition)>.5f || Mathf.Abs(hp-lastHPsum)>.1f){progressPosition=game.HeroPosition;lastProgress=game.Elapsed;lastHPsum=hp;}
   if(game.Elapsed-lastProgress>45){Finish("stalled",fullCampaign ? "No decreasing goal distance, damage or objective progress for 45 simulated seconds; goal="+report.goalKind+" bfs="+report.goalBfsDistance+" metres="+report.goalDistance.ToString("0.00") : "No walking or enemy HP progress for 45 simulated seconds");return;}
   if(report.wallSeconds-lastWrite>=5){lastWrite=report.wallSeconds;SaveReport();}
  }
  void EnterZone()
  {
   if(current!=null)CloseZone();
   lastZone=game.Zone;route=new DungeonGrid(game.Zone);pursued=null;targetCommittedUntil=0;lootUntil=-1;navAt=0;bestGoalDistances.Clear();report.goalKind="";report.goalBfsDistance=-1;
   current=new ZoneSample{zone=game.Zone,name=Campaign.Names[game.Zone],startSim=game.Elapsed-startedSim,startHealth=game.Health,startLevel=game.Level};
   report.zones.Add(current);baseKills=game.Defeated;basePotions=report.potionsUsed;previousPosition=progressPosition=game.HeroPosition;lastProgress=game.Elapsed;
   if(fullCampaign){var visited=new HashSet<int>();foreach(var z in report.zones)if(Array.IndexOf(Campaign.Route,z.zone)>=0)visited.Add(z.zone);report.visitedMainRouteZones=visited.Count;}
   Debug.Log("PACING_ZONE "+game.Zone+" sim="+report.simulatedSeconds.ToString("0.0")+" hp="+game.Health.ToString("0"));
  }
  void CloseZone()
  {current.endSim=game.Elapsed-startedSim;current.seconds=current.endSim-current.startSim;current.endHealth=game.Health;current.endLevel=game.Level;current.kills=game.Defeated-baseKills;current.potionsUsed=report.potionsUsed-basePotions;}
  void InteractReal()
  {
   int oldZone=game.Zone,kills=game.Defeated,level=game.Level,objectives=game.StageObjectiveCount;float health=game.Health,elapsed=game.Elapsed;
   navigationMode="interaction";game.Interact();
   if(fullCampaign && game.Zone==oldZone && game.StageObjectiveCount>objectives)
   {report.stageObjectivesCompleted+=game.StageObjectiveCount-objectives;if(current!=null)current.stageObjectivesCompleted=game.StageObjectiveCount;lastProgress=game.Elapsed;Debug.Log("PACING_STAGE_OBJECTIVE zone="+oldZone+" completed="+game.StageObjectiveCount);}
   if(game.Zone!=oldZone && current!=null)
   {
    current.endSim=elapsed-startedSim;current.seconds=current.endSim-current.startSim;current.endHealth=health;current.endLevel=level;
    current.kills=kills-baseKills;current.potionsUsed=report.potionsUsed-basePotions;current=null;
   }
  }
  void FightAndAdvance()
  {
   if(fullCampaign && game.Remaining>0 && Campaign.EventStage(game.Zone) && !game.StageObjectivesComplete)
   {
    Vector3 objective=game.ActiveStageObjectivePosition;
    if(Vector3.Distance(game.HeroPosition,objective)<2.2f && !game.enemies.Exists(e=>e.hp>0 && Vector3.Distance(e.Position,objective)<8 && Visible(e.Position,objective)))InteractReal();
   }
   if(game.Remaining==0)
   {
    if(fullCampaign && game.Zone==Campaign.Cemetery && !game.CanExit)
    {
     // Walking into the graveyard triggers its normal three-wave event in UpdateCampaign.
     // No direct spawning, deadline changes or grave-wave flag writes.
     Vector3 grave=new Vector3(12*1.8f,0,20*1.8f);if(Vector3.Distance(game.HeroPosition,grave)>3.5f)WalkTo(grave,"grave-defense");return;
    }
    if(lootUntil<0)lootUntil=game.Elapsed+8;
    Vector3 loot;
    if(game.Elapsed<lootUntil && NearbyLoot(out loot)){WalkTo(loot,"loot");return;}
    if(fullCampaign && Campaign.EventStage(game.Zone) && !game.StageObjectivesComplete)
    {Vector3 objective=game.ActiveStageObjectivePosition;if(Vector3.Distance(game.HeroPosition,objective)<2.2f)InteractReal();else WalkTo(objective,"stage-objective");return;}
    if(Vector3.Distance(game.HeroPosition,game.ExitPosition)<2.2f)InteractReal();else WalkTo(game.ExitPosition,"exit");return;
   }
   var target=NearestEnemy();if(target==null)return;
   if(fullCampaign){var cell=liveGrid.NearestWalkable(Mathf.RoundToInt(target.Position.x/1.8f),Mathf.RoundToInt(target.Position.z/1.8f));ObserveGoal(target.Position,"enemy:"+target.spawnId,liveGrid.Distance(cell.X,cell.Z));}
   float distance=Vector3.Distance(game.HeroPosition,target.Position);bool visible=Visible(game.HeroPosition,target.Position);
   int close=0;foreach(var e in game.enemies)if(e.hp>0 && Vector3.Distance(game.HeroPosition,e.Position)<8)close++;
   if(game.CanUseSkill(2) && (game.Health<game.MaxHealth*.8f || close>=2 && distance<5)){game.CastDefense();report.skillCasts++;}
   if(close>=2 && game.CanUseSkill(3) && game.SkillActiveRemaining(3)<=0){game.CastAdditional(0,target.Position);report.skillCasts++;}
   if(visible && distance<7.8f && (close>=2 || target.elite || target.boss) && game.CanUseSkill(5)){game.CastAdditional(2,target.Position);report.skillCasts++;}
   else if(visible && distance<7 && close>=2 && game.CanUseSkill(4)){game.CastAdditional(1,target.Position);report.skillCasts++;}
   if(target.hp>0 && visible && distance<(hero==2||hero==3?7.7f:3.3f) && game.CanUseSkill(1)){game.CastSkill(target.Position);report.skillCasts++;}
   if(target.hp>0 && visible && distance<game.Hero.range-.08f && game.CanUseSkill(0)){Call("Strike",target.Position);report.attacks++;}
   Vector3 escape;
   if(AvoidDanger(out escape,target.Position-game.HeroPosition)){navigationMode="avoid-danger";Walk(escape,fullCampaign ? .85f : float.PositiveInfinity);return;}
   if(target.hp<=0)return;
   if(game.Hero.range>3 && distance<3.4f && visible)
   {Vector3 away=(game.HeroPosition-target.Position).normalized;if(SafeStep(away)){navigationMode="kite";Walk(away,fullCampaign ? .85f : float.PositiveInfinity);return;}}
   if(!visible || distance>game.Hero.range*.78f)WalkTo(target.Position,"enemy:"+target.spawnId);
  }
  float EnemyScore(KryptaGame.Enemy e)
  {
   float d=Vector3.Distance(game.HeroPosition,e.Position);bool visible=Visible(game.HeroPosition,e.Position);
   if(!visible)
   {
    if(fullCampaign)
    {
     var cell=liveGrid.NearestWalkable(Mathf.RoundToInt(e.Position.x/1.8f),Mathf.RoundToInt(e.Position.z/1.8f));int steps=liveGrid.Distance(cell.X,cell.Z);
     d=steps>=0 ? steps*1.8f+2 : d+100;
    }
    else d+=12;
   }
   if(fullCampaign && visible){if(e.archetype==7)d-=4;if(e.sacrificial)d-=3;}
   return d;
  }
  KryptaGame.Enemy NearestEnemy()
  {
   KryptaGame.Enemy best=null;float score=float.MaxValue;
   foreach(var e in game.enemies)
   {if(e.hp<=0)continue;float d=EnemyScore(e);if(d<score){score=d;best=e;}}
   // Visibility scores jump at corners. Commit briefly instead of reversing
   // chase every frame; afterwards require a materially better alternative.
   if(pursued!=null && pursued.hp>0 && game.enemies.Contains(pursued) && best!=pursued)
   {
    float currentScore=EnemyScore(pursued);
    if(currentScore<100 && (game.Elapsed<targetCommittedUntil || score>currentScore*.65f-1))return pursued;
   }
   if(best!=pursued){if(pursued!=null)report.targetSwitches++;pursued=best;targetCommittedUntil=game.Elapsed+1.5f;}
   return best;
  }
  bool NearbyLoot(out Vector3 p)
  {
   p=Vector3.zero;float nearest=9;bool found=false;bool full=game.Inventory.Count>=KryptaGame.InventoryCapacity;
   foreach(string list in new[]{"pickups","relics","containerLoot"})
   {
    // Full backpacks deliberately leave intact relics on the ground for a player
    // to compare/swap. The route bot must not spend its bounded loot window on
    // an unavailable pickup; it neither deletes drops nor edits the inventory.
    if(full && list=="relics")continue;
    var entries=(IList)Read(game,list);
    foreach(object entry in entries)
    {
     if(full && list=="containerLoot" && (int)Read(entry,"kind")==3)continue;
     string field=list=="pickups"?"origin":"position";var point=(Vector3)Read(entry,field);float d=Vector3.Distance(point,game.HeroPosition);
     if(d<nearest && Visible(game.HeroPosition,point)){nearest=d;p=point;found=true;}
    }
   }
   return found;
  }
  void ObserveGoal(Vector3 goal,string purpose,int bfs)
  {
   report.position=game.HeroPosition;report.goal=goal;report.goalKind=purpose;report.goalDistance=Vector3.Distance(game.HeroPosition,goal);report.goalBfsDistance=bfs;
   if(!fullCampaign)return;
   string key=purpose.StartsWith("enemy:",StringComparison.Ordinal) ? purpose : purpose+":"+Mathf.RoundToInt(goal.x/1.8f)+":"+Mathf.RoundToInt(goal.z/1.8f);
   float distance=bfs>0 ? bfs*1.8f : report.goalDistance,previous;
   if(!bestGoalDistances.TryGetValue(key,out previous))bestGoalDistances[key]=distance;
   else if(distance<previous-.25f){bestGoalDistances[key]=distance;lastProgress=game.Elapsed;}
  }
  void WalkTo(Vector3 goal,string purpose="movement")
  {
   int x=Mathf.RoundToInt(goal.x/1.8f),z=Mathf.RoundToInt(goal.z/1.8f);
   if(!liveGrid.Walkable(x,z)){var nearest=liveGrid.NearestWalkable(x,z);x=nearest.X;z=nearest.Z;goal=new Vector3(x*1.8f,0,z*1.8f);}
   if(Time.time>=navAt || x!=navX || z!=navZ)
   {
    navAt=Time.time+.4f;navX=x;navZ=z;
    // Copy current container collision without disturbing the enemies' distance field.
    route=new DungeonGrid(game.Zone);
    for(int a=0;a<DungeonGrid.Width;a++)for(int b=0;b<DungeonGrid.Height;b++)if(!liveGrid.Walkable(a,b))route.CloseCell(a,b);else route.OpenCell(a,b);
    route.BuildDistances(x,z);
   }
   Vector3 waypoint=(Vector3)Call("RouteWaypoint",route,game.HeroPosition,goal);
   Vector3 direction=(waypoint-game.HeroPosition).normalized;navigationWaypoint=waypoint;navigationMode="route";
   int heroX=Mathf.RoundToInt(game.HeroPosition.x/1.8f),heroZ=Mathf.RoundToInt(game.HeroPosition.z/1.8f);ObserveGoal(goal,purpose,route.Distance(heroX,heroZ));
   float arrivalLimit=Vector3.Distance(game.HeroPosition,waypoint);
   Vector3 escape;if(AvoidDanger(out escape,direction)){direction=escape;arrivalLimit=.85f;navigationMode="avoid-danger";}
   Walk(direction,arrivalLimit);
  }
  bool SafeStep(Vector3 direction)
  {
   Vector3 goal=game.HeroPosition+direction*.85f;
   return liveGrid.CanTravel(game.HeroPosition.x/1.8f,game.HeroPosition.z/1.8f,goal.x/1.8f,goal.z/1.8f,.32f/1.8f);
  }
  float Danger(Vector3 p)
  {
   float score=0;
   foreach(var e in game.enemies)
   {
    if(fullCampaign && e.hp>0 && game.IsStageGuardian(e) && game.GuardianPhase(e)>0)
    {if(game.GuardianDangerAt(e,p,game.GuardianPhase(e)==1))score+=8;}
    if(fullCampaign && e.hp>0 && e.boss && game.Zone==Campaign.Heart && e.chargeAt>0)
    {float chargeDistance=PointSegmentDistance(p,e.Position,e.chargeTarget);if(chargeDistance<2.5f)score+=(2.5f-chargeDistance)*4;}
    if(e.hp<=0 || e.impactAt<=Time.time)continue;
    bool ranged=e.archetype==1||e.archetype==2||e.archetype==5||e.archetype==7;
    Vector3 center=ranged?e.chargeTarget:e.Position;float radius=ranged?2.1f:e.boss?3.1f:2.1f;
    float d=Vector3.Distance(p,center);if(d<radius)score+=(radius-d)*3;
   }
   foreach(object hazard in (IList)Read(game,"hazards"))
   {
    if(!(bool)Read(hazard,"hostile") || (float)Read(hazard,"end")<=Time.time)continue;
    float r=(float)Read(hazard,"radius")+.45f;float d=Vector3.Distance(p,(Vector3)Read(hazard,"pos"));if(d<r)score+=(r-d)*4;
   }
   return score;
  }
  static float PointSegmentDistance(Vector3 p,Vector3 start,Vector3 end)
  {Vector3 line=end-start;float t=line.sqrMagnitude<.001f ? 0 : Mathf.Clamp01(Vector3.Dot(p-start,line)/line.sqrMagnitude);return Vector3.Distance(p,start+line*t);}
  bool AvoidDanger(out Vector3 direction,Vector3 preferred)
  {
   direction=Vector3.zero;float baseline=Danger(game.HeroPosition);if(baseline<=.01f)return false;
   float best=baseline,bestAlignment=-2;preferred=preferred.normalized;
   for(int i=0;i<16;i++)
   {float angle=i*Mathf.PI/8;var d=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));if(!SafeStep(d))continue;float danger=Danger(game.HeroPosition+d*.85f);float alignment=Vector3.Dot(d,preferred);
    if(danger<baseline-.01f && (danger<best-.01f || Mathf.Abs(danger-best)<=.01f && alignment>bestAlignment))
    {best=danger;bestAlignment=alignment;direction=d;}}
   // Flat telegraph areas need a farther visible safe point: one short step can still
   // be inside the same circle. This only chooses walking direction; it never moves
   // the actor directly to that point or increases movement speed.
   if(fullCampaign && direction.sqrMagnitude<.1f)
   {
    float shortest=float.MaxValue;bestAlignment=-2;
    for(int i=0;i<16;i++)for(int step=1;step<=4;step++)
    {
     float angle=i*Mathf.PI/8;var d=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));float travel=step*1.1f;Vector3 point=game.HeroPosition+d*travel;
     if(travel>shortest+.01f || !liveGrid.CanTravel(game.HeroPosition.x/1.8f,game.HeroPosition.z/1.8f,point.x/1.8f,point.z/1.8f,.32f/1.8f))continue;
     float alignment=Vector3.Dot(d,preferred);
     if(Danger(point)<baseline-.01f && (travel<shortest-.01f || alignment>bestAlignment)){shortest=travel;bestAlignment=alignment;direction=d;}
    }
   }
   return direction.sqrMagnitude>.1f;
  }
  void Walk(Vector3 direction,float maxDistance=float.PositiveInfinity)
  {
   if(direction.sqrMagnitude<.01f){if(navigationMode=="route"){movementBlocked=true;report.routeBlockedFrames++;}return;}
   direction.Normalize();navigationDirection=direction;movementWindup=game.MeleeWindupActive;movementSpeed=game.PlayerMovementSpeed;
   requestedMetres=Mathf.Min(movementSpeed*Time.deltaTime,Mathf.Max(0,maxDistance));Vector3 before=game.HeroPosition;
   Call("MovePlayer",direction,Time.deltaTime,maxDistance);
   movedMetres=Vector3.Distance(before,game.HeroPosition);
   if(movementWindup)report.windupMovementFrames++;
   if(movedMetres>requestedMetres+.005f)report.movementLimitViolations++;
   movementBlocked=requestedMetres>.002f && movedMetres<requestedMetres*.05f;
   if(movementBlocked && navigationMode=="route")report.routeBlockedFrames++;
  }
  void RecordNavigation()
  {
   if(game.Zone!=lastZone)return;
   if(navigationMode=="route")report.routingSeconds+=Time.deltaTime;
   else if(navigationMode=="avoid-danger" || navigationMode=="kite")report.evasionSeconds+=Time.deltaTime;
   else report.holdingSeconds+=Time.deltaTime;
   if(game.Elapsed-lastTraceAt<.5f)return;lastTraceAt=game.Elapsed;
   var entry=new NavigationSample{seconds=game.Elapsed-startedSim,zone=game.Zone,position=game.HeroPosition,
    goal=report.goal,goalKind=report.goalKind,goalDistance=Vector3.Distance(game.HeroPosition,report.goal),bfs=report.goalBfsDistance,
    targetId=pursued==null ? -1 : pursued.spawnId,targetHP=pursued==null ? 0 : pursued.hp,danger=Danger(game.HeroPosition),
    mode=navigationMode,waypoint=navigationWaypoint,direction=navigationDirection,requestedMetres=requestedMetres,movedMetres=movedMetres,
    movementSpeed=movementSpeed,windup=movementWindup,blocked=movementBlocked,
    directRoute=liveGrid.CanTravel(game.HeroPosition.x/1.8f,game.HeroPosition.z/1.8f,report.goal.x/1.8f,report.goal.z/1.8f,.32f/1.8f)};
   report.navigationTrace.Add(entry);if(report.navigationTrace.Count>120)report.navigationTrace.RemoveAt(0);
  }
  void SaveReport()
  {
   if(frameDeltaTimes.Count>0){float[] sorted=frameDeltaTimes.ToArray();Array.Sort(sorted);report.p99DeltaTime=sorted[Mathf.Clamp(Mathf.CeilToInt(sorted.Length*.99f)-1,0,sorted.Length-1)];}
   var parent=Path.GetDirectoryName(output);if(!string.IsNullOrEmpty(parent))Directory.CreateDirectory(parent);
   report.containersBroken=0;if(game)foreach(string id in game.CaptureBrokenContainers())if(!id.StartsWith("L:",StringComparison.Ordinal))report.containersBroken++;
   File.WriteAllText(output,JsonUtility.ToJson(report,true));
  }
  void Finish(string result,string error="")
  {
   if(finished)return;finished=true;report.result=result;report.failure=error;report.valid=result!="error";report.completed=result=="completed";
   if(game){report.zone=game.Zone;report.remaining=game.Remaining;report.health=game.Health;report.maxHealth=game.MaxHealth;report.level=game.Level;report.simulatedSeconds=game.Elapsed-startedSim;report.kills=game.Defeated;if(current!=null)CloseZone();}
   report.wallSeconds=began?Time.realtimeSinceStartup-startedWall:Time.realtimeSinceStartup-bootWall;
   Time.timeScale=1;RestoreMaximumDeltaTime();SaveReport();Debug.Log("PACING_FINISHED "+result+" simulated="+report.simulatedSeconds.ToString("0.0")+" wall="+report.wallSeconds.ToString("0.0")+" hp="+report.health.ToString("0"));
#if UNITY_EDITOR
   if(UnityEditor.SessionState.GetBool("Nocturnis.PacingQA",false)){UnityEditor.SessionState.SetBool("Nocturnis.PacingQA",false);UnityEditor.EditorApplication.Exit(result=="error"?1:0);return;}
#endif
   Application.Quit(result=="error"?1:0);
  }
 }
}
