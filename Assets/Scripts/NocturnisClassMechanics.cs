using System.Collections.Generic;
using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaGame
 {
  sealed class BloodMark {public int count;public float until;public GameObject glyph;}
  sealed class ControlResistance {public float immuneUntil;}
  sealed class ClassCast
  {public int slot;public bool weaken;public string kind;public Vector3 center;public float born,at,radius,damage,duration;public GameObject marker;}
  sealed class RootField {public Vector3 center;public float radius,end,tickAt;public GameObject marker;}
  readonly Dictionary<Enemy,BloodMark> bloodMarks=new Dictionary<Enemy,BloodMark>();
  readonly Dictionary<Enemy,ControlResistance> controlResistance=new Dictionary<Enemy,ControlResistance>();
  readonly Dictionary<Transform,float> rootedActors=new Dictionary<Transform,float>();
  readonly List<Enemy> expiredBlood=new List<Enemy>();
  readonly List<ClassCast> classCasts=new List<ClassCast>();
  readonly List<RootField> rootFields=new List<RootField>();
  Transform classMechanicsWorld;int classMechanicsHero=-1;float classTickAt,voidShellUntil,voidStored;
  public int PendingClassCasts {get{return classCasts.Count;}}
  public int ActiveRootFields {get{return rootFields.Count;}}
  public int BloodMarkCount(Enemy e)
  {BloodMark m;return e!=null && bloodMarks.TryGetValue(e,out m) && m.until>Time.time ? m.count : 0;}
  public float EnemyRootRemaining(Enemy e)
  {float end;return e!=null && e.visual!=null && e.visual.Root && rootedActors.TryGetValue(e.visual.Root.transform,out end) ? Mathf.Max(0,end-Time.time) : 0;}
  public float EnemyControlImmuneRemaining(Enemy e)
  {ControlResistance c;return e!=null && controlResistance.TryGetValue(e,out c) ? Mathf.Max(0,c.immuneUntil-Time.time) : 0;}
  void ResetClassMechanics()
  {
   foreach(var m in bloodMarks.Values)if(m.glyph)Destroy(m.glyph);
   foreach(var c in classCasts)if(c.marker)Destroy(c.marker);
   foreach(var f in rootFields)if(f.marker)Destroy(f.marker);
   bloodMarks.Clear();controlResistance.Clear();rootedActors.Clear();classCasts.Clear();rootFields.Clear();
   classMechanicsWorld=world;classMechanicsHero=SelectedHero;classTickAt=Time.time;voidShellUntil=voidStored=0;
   Status.Remove("counter");Status.Remove("riposte");Status.Remove("precision");Status.Remove("voidshell");
  }
  void EnsureClassMechanics()
  {if(classMechanicsWorld!=world || classMechanicsHero!=SelectedHero)ResetClassMechanics();}
  bool EnemyRootBlocksMovement(Transform actor)
  {float until;return actor && rootedActors.TryGetValue(actor,out until) && Time.time<until;}
  bool ApplyEnemyStun(Enemy enemy,float duration)
  {return ApplyEnemyStunAt(enemy,duration,Time.time);}
  bool ApplyEnemyStunAt(Enemy enemy,float duration,float now)
  {
   EnsureClassMechanics();if(enemy==null || enemy.hp<=0 || duration<=0)return false;
   if(enemy.boss || enemy.elite || IsStageGuardian(enemy))
   {
    ControlResistance state;if(!controlResistance.TryGetValue(enemy,out state)){state=new ControlResistance();controlResistance.Add(enemy,state);}
    if(now<state.immuneUntil){enemy.slowUntil=Mathf.Max(enemy.slowUntil,now+.6f);return false;}
    duration=Mathf.Min(.75f,duration);state.immuneUntil=now+duration+5;
   }
   enemy.stunUntil=Mathf.Max(enemy.stunUntil,now+duration);return true;
  }
  bool ApplyEnemyRoot(Enemy enemy,float duration)
  {
   EnsureClassMechanics();if(enemy==null || enemy.hp<=0 || duration<=0)return false;
   // Large guardians retain their telegraphed attacks; incorporeal apparitions
   // ignore physical roots. Neither case is converted into a hidden full stun.
   if(enemy.boss || enemy.elite || IsStageGuardian(enemy) || enemy.archetype==4)
   {enemy.slowUntil=Mathf.Max(enemy.slowUntil,Time.time+Mathf.Min(duration,1.5f));return false;}
   rootedActors[enemy.visual.Root.transform]=Time.time+duration;return true;
  }
  void AddBloodMark(Enemy enemy,int count=1)
  {
   EnsureClassMechanics();if(enemy==null || enemy.hp<=0)return;BloodMark mark;
   if(!bloodMarks.TryGetValue(enemy,out mark))
   {
    mark=new BloodMark();bloodMarks.Add(enemy,mark);mark.glyph=Ring(enemy.visual.Root.transform,.43f,.055f,art.redMaterial);
    mark.glyph.name="Znamie krwi";mark.glyph.transform.localPosition=Vector3.up*.2f;
   }
   if(mark.until<=Time.time)mark.count=0;mark.count=Mathf.Min(3,mark.count+count);mark.until=Time.time+7;
   mark.glyph.transform.localScale=Vector3.one*(.7f+mark.count*.18f);
   Floating(enemy.Position,"ZNAMIĘ "+mark.count+"/3",Hero.color);
  }
  int ConsumeBloodMarks(Enemy enemy)
  {
   int count=BloodMarkCount(enemy);BloodMark mark;
   if(bloodMarks.TryGetValue(enemy,out mark)){if(mark.glyph)Destroy(mark.glyph);bloodMarks.Remove(enemy);}return count;
  }
  float ConsumeRiposte()
  {if(Status.Count("riposte",Time.time)==0)return 1;Status.Remove("riposte");return 1.75f;}
  float ClassContactMultiplier()
  {return SelectedHero==0 ? ConsumeRiposte() : 1;}
  void ClassBasicContact(Enemy enemy)
  {
   if(enemy==null || enemy.hp<=0)return;
   if(SelectedHero==1)AddBloodMark(enemy);
   if(SelectedHero==4)ApplyEnemyRoot(enemy,.65f);
  }
  float ApplyClassProtection(float amount)
  {
   if(SelectedHero==0 && Status.Count("counter",Time.time)>0)
   {
    Status.Remove("counter");Status.Apply("riposte","Kontra gotowa",Time.time,4);
    Floating(HeroPosition,"KONTRA",art.goldMaterial.color);ClassSpellVfx(2,HeroPosition,1.25f);return amount*.15f;
   }
   if(SelectedHero==3 && Time.time<voidShellUntil)
   {float stored=amount*.45f;voidStored=Mathf.Min(80,voidStored+stored);return amount-stored;}
   return amount;
  }
  Vector3 AimDirection(Vector3 aim)
  {Vector3 d=aim-HeroPosition;d.y=0;return d.sqrMagnitude>.001f ? d.normalized : facing;}
  int PierceEnemies(Vector3 aim,float length,float width,float damage,int maximum,bool precision,bool slow)
  {
   Vector3 origin=HeroPosition,direction=AimDirection(aim);var candidates=new List<Enemy>();
   foreach(var e in enemies)
   {Vector3 delta=e.Position-origin;float along=Vector3.Dot(delta,direction);if(e.hp>0 && along>0 && along<=length && (delta-direction*along).magnitude<=width && Visible(origin,e.Position))candidates.Add(e);}
   candidates.Sort((a,b)=>(a.Position-origin).sqrMagnitude.CompareTo((b.Position-origin).sqrMagnitude));
   if(precision && Status.Count("precision",Time.time)>0){Status.Remove("precision");maximum+=2;damage*=1.25f;}
   int hits=0;foreach(var e in candidates)
   {if(hits>=maximum)break;Damage(e,damage*Mathf.Pow(.88f,hits));if(slow)e.slowUntil=Mathf.Max(e.slowUntil,Time.time+3);hits++;}
   Vector3 end=origin+direction*length;Beam(origin+Vector3.up,end+Vector3.up,classMaterial);
   StrikeBreakables(origin,direction,length,damage);return hits;
  }
  int BloodDetonation(Vector3 center,float radius,float baseDamage,bool ultimate)
  {
   int hits=0,marks=0;var bursts=new List<Vector3>();
   foreach(var e in enemies)
   {
    if(e.hp<=0 || Vector3.Distance(e.Position,center)>radius || !Visible(center,e.Position))continue;
    int count=ConsumeBloodMarks(e);marks+=count;hits++;
    Damage(e,baseDamage*(count==0 ? .32f : .7f+count*.45f));
    if(ultimate && count>0)bursts.Add(e.Position);ClassImpactVfx(e.Position,count>0);
   }
   // One secondary wave per victim, even when several marked enemies overlap.
   if(ultimate)foreach(var e in enemies)
   {if(e.hp<=0)continue;foreach(var p in bursts)if(Vector3.Distance(e.Position,p)<2.5f && Visible(p,e.Position)){Damage(e,baseDamage*.35f);break;}}
   if(marks>0)Heal(MaxHealth*Mathf.Min(Rune==1 ? .24f : .16f,marks*(Rune==1 ? .04f : .025f)));return hits;
  }
  GameObject ClassAreaMarker(Vector3 center,float radius,string name)
  {var marker=new GameObject(name);marker.transform.SetParent(world);marker.transform.position=center;Ring(marker.transform,radius,.055f,classMaterial);return marker;}
  void QueueClassCast(string kind,Vector3 center,float radius,float damage,float delay,float duration=0,int slot=1)
  {
   EnsureClassMechanics();var cast=new ClassCast{slot=slot,weaken=kind=="void" && Rune==1,kind=kind,center=center,radius=radius,damage=damage,born=Time.time,at=Time.time+delay,duration=duration};
   cast.marker=ClassAreaMarker(center,radius,kind=="void" ? "Nadchodzaca osobliwosc" : kind=="root" ? "Narastajace korzenie" : "Nadchodzacy wyrok");
   classCasts.Add(cast);
  }
  void CreateRootField(Vector3 center,float radius,float duration)
  {
   EnsureClassMechanics();rootFields.Add(new RootField{center=center,radius=radius,end=Time.time+duration,tickAt=Time.time,marker=ClassAreaMarker(center,radius,"Splot korzeni")});
  }
  void TickClassMechanics(float now)
  {
   EnsureClassMechanics();if(State!=Phase.Playing)return;
   float step=Mathf.Clamp(now-classTickAt,0,.1f);classTickAt=now;
   expiredBlood.Clear();foreach(var pair in bloodMarks)if(pair.Key.hp<=0 || pair.Value.until<=now)expiredBlood.Add(pair.Key);
   foreach(var e in expiredBlood)ConsumeBloodMarks(e);
   if(voidShellUntil>0 && now>=voidShellUntil)
   {
    voidShellUntil=0;float damage=voidStored;voidStored=0;
    if(damage>0){foreach(var e in enemies)if(e.hp>0 && Vector3.Distance(e.Position,HeroPosition)<3 && Visible(HeroPosition,e.Position))Damage(e,damage);Burst(HeroPosition,3,art.violetMaterial);GainResource(10);}
   }
   for(int i=classCasts.Count-1;i>=0;i--)
   {
    var c=classCasts[i];
    if(c.kind=="void" && now<c.at)
     foreach(var e in enemies)if(e.hp>0 && !e.boss && !e.elite && e.archetype!=4 && Vector3.Distance(e.Position,c.center)<c.radius && Visible(c.center,e.Position))
     {Vector3 delta=c.center-e.Position;Move(e.visual.Root.transform,delta.normalized*Mathf.Min(delta.magnitude,step*1.6f),.3f);}
    if(now<c.at)continue;classCasts.RemoveAt(i);if(c.marker)Destroy(c.marker);
    foreach(var e in enemies)if(e.hp>0 && Vector3.Distance(c.center,e.Position)<c.radius && Visible(c.center,e.Position))
    {Damage(e,c.damage);if(c.kind=="void"){e.slowUntil=Mathf.Max(e.slowUntil,Time.time+2);if(c.weaken)e.armorUntil=Time.time+5;}if(c.kind=="judgment")ApplyEnemyStun(e,.45f);if(c.kind=="root")ApplyEnemyRoot(e,3);}
    DamageBreakables(c.center,c.radius,c.damage);ClassSpellVfx(c.kind=="judgment" ? 4 : 1,c.center,c.radius);Sound(ClassSpellSound);
    if(c.kind=="root")CreateRootField(c.center,c.radius,c.duration);
   }
   for(int i=rootFields.Count-1;i>=0;i--)
   {
    var f=rootFields[i];if(now>=f.end){if(f.marker)Destroy(f.marker);rootFields.RemoveAt(i);continue;}
    if(now<f.tickAt)continue;f.tickAt=now+.4f;
    foreach(var e in enemies)if(e.hp>0 && Vector3.Distance(e.Position,f.center)<f.radius && Visible(f.center,e.Position))ApplyEnemyRoot(e,.65f);
   }
  }
  string CastClassAdditional(int i,Vector3 aim,Vector3 center,float damage)
  {
   int hits=0;Vector3 direction=AimDirection(aim);
   if(SelectedHero==0)
   {
    float boost=ConsumeRiposte();int pulses=i==2 ? 3 : 1;
    for(int n=0;n<pulses;n++){Vector3 point=HeroPosition+direction*(2.5f+n*2);if(Visible(HeroPosition,point))QueueClassCast("judgment",point,2.2f,damage*(i==2 ? .55f : 1)*boost,.25f+n*.25f,0,i+3);}
    return i==2 ? "trzy kolejne wyroki na linii" : "wyrok po 0,25 s";
   }
   if(SelectedHero==1)
   {
    if(i==1){foreach(var e in enemies)if(e.hp>0 && Vector3.Distance(e.Position,center)<4 && Visible(center,e.Position)){AddBloodMark(e,2);Damage(e,damage*.45f);hits++;}return "Znaki krwi: "+hits;}
    hits=BloodDetonation(center,7,damage,true);return "Krwawa detonacja: "+hits+" • zużywa znamiona";
   }
   if(SelectedHero==2)
   {
    if(i==1)hits=PierceEnemies(aim,10,1.1f,damage,4,false,true);
    else
    {
     // Stagger three true lanes rather than a circular explosion. Each lane has
     // reduced damage so crossing lanes cannot triple the former ultimate burst.
     foreach(float angle in new[]{-16f,0f,16f})hits+=PierceEnemies(HeroPosition+Quaternion.Euler(0,angle,0)*direction*12,12,.6f,damage*.48f,5,false,true);
    }
    Shadow=Mathf.Min(100,Shadow+15);return "Przebicia: "+hits+" • spowolnienie 3 s";
   }
   if(SelectedHero==3)
   {QueueClassCast("void",center,i==2 ? 5.5f : 3.2f,damage*(i==2 ? 1.35f : 1),i==2 ? 1.25f : .65f,0,i+3);return "Osobliwość po "+(i==2 ? "1,25" : "0,65")+" s • przyciąga zwykłych wrogów";}
   if(i==1){QueueClassCast("root",center,2.7f,damage,.2f,2.5f,i+3);return "splot korzeni • 2,5 s";}
   for(int n=0;n<3;n++){Vector3 point=HeroPosition+direction*(2+n*2.5f);if(Visible(HeroPosition,point))QueueClassCast("root",point,2.5f,damage*.5f,n*.25f,4,i+3);}
   Heal(25);return "trzy sploty korzeni • blokują ruch zwykłych wrogów";
  }
 }
}
