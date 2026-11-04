using System.Collections.Generic;
using UnityEngine;

namespace Krypta
{
 public sealed partial class KryptaGame
 {
  sealed class StageGuardian
  {
   public Enemy enemy;public int pattern,phase,cycle;public float started,activeAt,endAt,recoverAt,radius,angle,width,lastSample;
   public Vector3 origin,target,second,direction,previous;public bool hit,resolved;public GameObject marker;
   public LineRenderer primary,secondary,progress;
  }
  readonly Dictionary<Enemy,StageGuardian> stageGuardians=new Dictionary<Enemy,StageGuardian>();
  StageGuardian activeStageGuardian;
  static int StageGuardianPattern(int zone){return zone>=14 && zone<=18 ? zone-14 : -1;}
  static float StageGuardianHealthMultiplier(int zone,int spawnId,bool elite)
  {return StageGuardianPattern(zone)>=0 && elite && spawnId>=22 && spawnId<=25 ? spawnId==25 ? 1.65f : 1.2f : 1;}
  public bool IsStageGuardian(Enemy e)
  {return e!=null && StageGuardianPattern(Zone)>=0 && e.elite && e.spawnId>=22 && e.spawnId<=25;}
  public int ActiveStageGuardianCasts {get{return activeStageGuardian!=null && activeStageGuardian.phase>0 ? 1 : 0;}}
  public int GuardianPhase(Enemy e){StageGuardian s;return stageGuardians.TryGetValue(e,out s) ? s.phase : 0;}
  public float GuardianWindupRemaining(Enemy e){StageGuardian s;return stageGuardians.TryGetValue(e,out s) && s.phase==1 ? Mathf.Max(0,s.activeAt-Time.time) : 0;}
  void ResetStageGuardians()
  {foreach(var s in stageGuardians.Values)EndGuardianMarker(s);stageGuardians.Clear();activeStageGuardian=null;}
  void UpdateStageGuardianCleanup()
  {
   foreach(var s in stageGuardians.Values)if(s.enemy.hp<=0 || !s.enemy.visual.Root)
   {EndGuardianMarker(s);s.phase=0;if(activeStageGuardian==s)activeStageGuardian=null;}
  }
  void EndGuardianMarker(StageGuardian s)
  {if(s.marker){s.marker.SetActive(false);Destroy(s.marker);}s.marker=null;}
  StageGuardian GuardianState(Enemy e)
  {
   StageGuardian s;if(stageGuardians.TryGetValue(e,out s))return s;
   s=new StageGuardian{enemy=e,pattern=StageGuardianPattern(Zone),recoverAt=Time.time+1.5f+(e.spawnId%3)*.45f};stageGuardians.Add(e,s);return s;
  }
  bool UpdateStageGuardian(Enemy e)
  {
   if(!IsStageGuardian(e))return false;
   var s=GuardianState(e);e.warning.SetActive(false);UpdateStageGuardianCleanup();
   if(e.hp<=0)return true;
   if(Time.time<e.stunUntil)
   {
    if(s.phase>0){EndGuardianMarker(s);s.phase=0;s.recoverAt=Time.time+1.8f;if(activeStageGuardian==s)activeStageGuardian=null;Floating(e.Position,"PRZERWANO",new Color(.5f,.9f,1));}
    e.visual.Tick(false);return true;
   }
   float distance=Vector3.Distance(e.Position,HeroPosition);
   if(distance<12 && Visible(e.Position,HeroPosition))e.alerted=true;
   if(!e.alerted){e.visual.Tick(false);return true;}
   if(s.phase==0)
   {
    if(activeStageGuardian==null && Time.time>=s.recoverAt && distance<9 && Visible(e.Position,HeroPosition))
    {BeginGuardianAttack(s,HeroPosition);return true;}
    // Recovery is an intentional damage opportunity. Waiting guardians do not layer melee hits over a special.
    if(distance>5 && s.recoverAt<Time.time)
    {Vector3 d=RouteDirection(grid,e.Position,HeroPosition);Move(e.visual.Root.transform,d*1.9f*(Time.time<e.slowUntil ? .65f : 1)*Time.deltaTime,.35f);if(d.sqrMagnitude>.01f)e.visual.Root.transform.rotation=Quaternion.LookRotation(d);e.visual.Tick(true);}
    else e.visual.Tick(false);
    return true;
   }
   // A stalled frame must not consume an unseen warning or skip an active attack.
   // Discard the unobserved time instead of dealing retrospective damage. Geometry,
   // movement and hit checks all advance by the same bounded, scaled-time sample.
   float step=ObserveCombatWindow(ref s.lastSample,ref s.started,ref s.activeAt,ref s.endAt);
   if(s.phase==1)
   {
    e.visual.Tick(false);DrawGuardianMarker(s,Mathf.Clamp01((Time.time-s.started)/(s.activeAt-s.started)));
    if(Time.time<s.activeAt)return true;
    s.phase=2;s.previous=e.Position;e.visual.Play(s.pattern==0 ? "Dodge_Forward" : s.pattern==3 ? "1H_Melee_Attack_Chop" : "Cast_A",false,s.endAt-s.activeAt);
    Sound(s.pattern==0 ? "chain" : s.pattern==1 ? "shrine" : s.pattern==3 ? "stone" : "spell3");
   }
   if(s.phase==2)
   {
    if(Time.time>=s.endAt){EndGuardianMarker(s);s.phase=0;s.recoverAt=Time.time+2.4f+(e.spawnId==25 ? 0 : .7f);activeStageGuardian=null;return true;}
    if(s.pattern==0)
    {
     s.previous=e.Position;Vector3 to=s.target-e.Position;
     if(to.sqrMagnitude>.09f)Move(e.visual.Root.transform,to.normalized*Mathf.Min(to.magnitude,11*step*(Time.time<e.slowUntil ? .65f : 1)),.4f);
     e.visual.Tick(true);
    }
    DrawGuardianMarker(s,1);
    // Ritual / hammer are one impact, not a lingering invisible damage zone.
    // Sweeping attacks may hit once while crossing the current player position.
    bool singleImpact=s.pattern==2 || s.pattern==3;
    if(!s.hit && (!singleImpact || !s.resolved))
    {
     if(singleImpact)s.resolved=true;
     if(GuardianDangerContains(s,HeroPosition,false) && Visible(s.origin,HeroPosition))
     {s.hit=true;Hurt(e.spawnId==25 ? 29 : 22);}
    }
   }
   return true;
  }
  void BeginGuardianAttack(StageGuardian s,Vector3 aim)
  {
   if(activeStageGuardian!=null || s.enemy.hp<=0)return;
   s.origin=s.enemy.Position;s.previous=s.origin;s.direction=aim-s.origin;s.direction.y=0;
   if(s.direction.sqrMagnitude<.01f)s.direction=s.enemy.visual.Root.transform.forward;s.direction.Normalize();
   s.target=s.origin+s.direction*Mathf.Min(8,Vector3.Distance(s.origin,aim));s.second=aim+Vector3.Cross(s.direction,Vector3.up)*3.6f;
   if(s.pattern==2){s.target=aim;var cell=grid.NearestWalkable(Cell(s.second.x),Cell(s.second.z));s.second=World(cell.X,cell.Z);}
   s.cycle++;s.radius=s.pattern==1 ? 5.4f : s.pattern==2 ? 1.8f : s.pattern==3 ? 5.2f : s.pattern==4 ? 6.8f : 8;
   s.width=s.pattern==0 ? .9f : s.pattern==1 ? .42f : .48f;s.angle=s.pattern==3 ? 33 : 50;
   if(s.pattern==1)s.direction=Quaternion.Euler(0,s.cycle%2==0 ? 90 : -90,0)*s.direction;
   if(!GuardianEscapeAvailable(s)){s.recoverAt=Time.time+1.2f;return;}
   s.started=Time.time;s.activeAt=Time.time+(s.pattern==0 ? 1.2f : s.pattern==4 ? 1.5f : 1.3f);
   s.endAt=s.activeAt+(s.pattern==0 ? .85f : s.pattern==1 ? 1.65f : s.pattern==4 ? 1.9f : .35f);
   s.hit=s.resolved=false;s.lastSample=Time.time;s.phase=1;s.enemy.impactAt=0;s.enemy.visual.Root.transform.rotation=Quaternion.LookRotation(s.direction);activeStageGuardian=s;
   MakeGuardianMarker(s);DrawGuardianMarker(s,0);
   string[] warning={"ŁAŃCUCH • zejdź z linii szarży","DZWON • luka w pierścieniu daje schronienie","RYTUAŁ • opuść oba kręgi","MŁOT • przejdź za strażnika","PRÓŻNIA • omiń promień lub wejdź blisko strażnika"};
   ShowNotice(warning[s.pattern]);Sound(s.pattern==0 ? "chain" : "triptych");
  }
  bool GuardianEscapeAvailable(StageGuardian s)
  {
   // Do not begin a special in a narrow corner unless an unobstructed safe floor point
   // can be reached during the warning. No extra invulnerability or damage forgiveness.
   float travel=Hero.speed*(s.pattern==4 ? 1.5f : 1.2f)*.8f;
   int x=Cell(HeroPosition.x),z=Cell(HeroPosition.z);
   for(int dx=-4;dx<=4;dx++)for(int dz=-4;dz<=4;dz++)
   {
    if(!grid.Walkable(x+dx,z+dz))continue;Vector3 p=World(x+dx,z+dz);
    if(Vector3.Distance(HeroPosition,p)>travel || !Visible(HeroPosition,p))continue;
    Vector3 d=p-s.origin;float distance=d.magnitude;
    bool danger=s.pattern==1 ? distance>.28f && distance<s.radius+s.width && Vector3.Angle(s.direction,d)>s.angle :
     s.pattern==4 ? distance>=1.2f && distance<s.radius+.5f && Vector3.Angle(s.direction,d)<42 : GuardianDangerContains(s,p,true);
    if(!danger)return true;
   }
   return false;
  }
  void MakeGuardianMarker(StageGuardian s)
  {
   EndGuardianMarker(s);s.marker=VfxRoot("Guardian telegraph "+s.pattern,Vector3.zero);
   Color amber=new Color(1,.53f,.16f,.85f);
   s.primary=VfxLine(s.marker.transform,"Actual damage boundary",new[]{Vector3.zero,Vector3.zero},amber,.07f);
   s.secondary=VfxLine(s.marker.transform,"Second boundary / safe gap",new[]{Vector3.zero,Vector3.zero},amber,.05f);
   s.progress=VfxLine(s.marker.transform,"Windup / active edge",new[]{Vector3.zero,Vector3.zero},amber,.055f);
  }
  static void GuardianLine(LineRenderer line,Vector3[] points,bool loop=false)
  {line.loop=loop;line.positionCount=points.Length;line.SetPositions(points);}
  static Vector3[] GuardianArc(Vector3 center,float radius,Vector3 direction,float start,float end,int steps=48)
  {
   var p=new Vector3[steps+1];for(int i=0;i<=steps;i++){float a=Mathf.Lerp(start,end,i/(float)steps);p[i]=center+Quaternion.Euler(0,a,0)*direction*radius+Vector3.up*.15f;}return p;
  }
  void DrawGuardianMarker(StageGuardian s,float charge)
  {
   if(!s.marker)return;Color c=s.phase==1 ? new Color(1,.52f,.17f,.78f) : new Color(1,.2f,.12f,.9f);
   if(ReducedFlashes)c.a*=.8f;s.primary.startColor=s.primary.endColor=s.secondary.startColor=s.secondary.endColor=s.progress.startColor=s.progress.endColor=c;
   s.primary.widthMultiplier=s.phase==1 ? Mathf.Lerp(.055f,.10f,charge) : .12f;
   Vector3 up=Vector3.up*.15f;float t=Mathf.Clamp01((Time.time-s.activeAt)/(s.endAt-s.activeAt));
   if(s.pattern==0)
   {
    Vector3 side=Vector3.Cross(s.direction,Vector3.up)*s.width;
    GuardianLine(s.primary,new[]{s.origin+side+up,s.target+side+up,s.target-side+up,s.origin-side+up},true);
    GuardianLine(s.secondary,new[]{s.target+side*1.7f-s.direction*.6f+up,s.target+up,s.target-side*1.7f-s.direction*.6f+up});
    GuardianLine(s.progress,new[]{s.origin+up,Vector3.Lerp(s.origin,s.target,charge)+up});
   }
   else if(s.pattern==1)
   {
    float radius=s.phase==1 ? s.radius : Mathf.Lerp(.7f,s.radius,t);
    GuardianLine(s.primary,GuardianArc(s.origin,radius+s.width,s.direction,s.angle,360-s.angle));
    GuardianLine(s.secondary,GuardianArc(s.origin,Mathf.Max(.1f,radius-s.width),s.direction,s.angle,360-s.angle));
    GuardianLine(s.progress,new[]{s.origin+Quaternion.Euler(0,-s.angle,0)*s.direction*s.radius+up,s.origin+up,s.origin+Quaternion.Euler(0,s.angle,0)*s.direction*s.radius+up});
    s.progress.startColor=s.progress.endColor=new Color(.35f,.85f,.65f,.55f);
   }
   else if(s.pattern==2)
   {
    GuardianLine(s.primary,GuardianArc(s.target,s.radius,Vector3.forward,0,360));GuardianLine(s.secondary,GuardianArc(s.second,s.radius,Vector3.forward,0,360));
    GuardianLine(s.progress,GuardianArc(s.target,s.radius*Mathf.Lerp(.15f,.9f,charge),Vector3.forward,0,360));
   }
   else if(s.pattern==3)
   {
    var arc=GuardianArc(s.origin,s.radius,s.direction,-s.angle,s.angle,24);var cone=new Vector3[arc.Length+2];cone[0]=s.origin+up;for(int i=0;i<arc.Length;i++)cone[i+1]=arc[i];cone[cone.Length-1]=s.origin+up;
    GuardianLine(s.primary,cone);GuardianLine(s.secondary,new[]{s.origin+up,s.origin+s.direction*s.radius+up});
    GuardianLine(s.progress,GuardianArc(s.origin,s.radius*charge,s.direction,-s.angle,s.angle,24));
   }
   else
   {
    Vector3 beam=Quaternion.Euler(0,Mathf.Lerp(-32,32,t),0)*s.direction;
    if(s.phase==1)beam=Quaternion.Euler(0,-32,0)*s.direction;
    Vector3 side=Vector3.Cross(beam,Vector3.up)*s.width;Vector3 a=s.origin+beam*1.2f,b=s.origin+beam*s.radius;
    GuardianLine(s.primary,new[]{a+side+up,b+side+up,b-side+up,a-side+up},true);
    GuardianLine(s.secondary,GuardianArc(s.origin,s.radius,s.direction,-32,32,24));
    GuardianLine(s.progress,GuardianArc(s.origin,1.2f,Vector3.forward,0,360,32));
    s.progress.startColor=s.progress.endColor=new Color(.35f,.85f,.65f,.6f);
   }
  }
  static float SegmentDistance(Vector3 point,Vector3 a,Vector3 b)
  {point.y=a.y=b.y=0;Vector3 d=b-a;float t=d.sqrMagnitude<.001f ? 0 : Mathf.Clamp01(Vector3.Dot(point-a,d)/d.sqrMagnitude);return Vector3.Distance(point,a+d*t);}
  bool GuardianDangerContains(StageGuardian s,Vector3 point,bool planned)
  {
   if(!planned && s.phase!=2)return false;point.y=s.origin.y;
   Vector3 d=point-s.origin;float distance=d.magnitude;
   if(s.pattern==0)
   {
    float chargeAlong=Vector3.Dot(d,s.direction),length=Vector3.Distance(s.origin,s.target);
    return chargeAlong>=0 && chargeAlong<=length && SegmentDistance(point,planned ? s.origin : s.previous,planned ? s.target : s.enemy.Position)<s.width;
   }
   if(s.pattern==1)
   {float r=planned ? s.radius : Mathf.Lerp(.7f,s.radius,Mathf.Clamp01((Time.time-s.activeAt)/(s.endAt-s.activeAt)));return Mathf.Abs(distance-r)<s.width && Vector3.Angle(s.direction,d)>s.angle;}
   if(s.pattern==2)return Vector3.Distance(point,s.target)<s.radius || Vector3.Distance(point,s.second)<s.radius;
   if(s.pattern==3)return distance<s.radius && Vector3.Angle(s.direction,d)<s.angle;
   float phase=planned ? 0 : Mathf.Clamp01((Time.time-s.activeAt)/(s.endAt-s.activeAt));Vector3 ray=Quaternion.Euler(0,Mathf.Lerp(-32,32,phase),0)*s.direction;
   float along=Vector3.Dot(d,ray);return along>=1.2f && along<=s.radius && Vector3.Cross(d,ray).magnitude<s.width;
  }
  public bool GuardianDangerAt(Enemy e,Vector3 point,bool planned=false)
  {StageGuardian s;return stageGuardians.TryGetValue(e,out s) && GuardianDangerContains(s,point,planned);}
  public bool ReviewStartGuardianAttack(Enemy e,Vector3 target)
  {
   if(!Testing || State!=Phase.Playing || !IsStageGuardian(e) || e.hp<=0 || Time.time<e.stunUntil || activeStageGuardian!=null)return false;
   e.alerted=true;var s=GuardianState(e);BeginGuardianAttack(s,target);return s.phase==1;
  }
 }
}
