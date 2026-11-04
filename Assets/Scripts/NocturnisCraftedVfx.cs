using System.Collections.Generic;
using UnityEngine;

namespace Krypta
{
 public sealed partial class KryptaGame
 {
  // Authored geometry and particle motion. Shared asset materials, no per-cast material instances.
  sealed class CraftedEffect
  {
   public GameObject root;public float born,duration,spin,rise,delay,scaleFrom=1,scaleTo=1;
   public Vector3 origin;public LineRenderer[] lines;public Color[] starts,ends;public float[] widths;
   public bool growLines;public Transform follow;public NocturnisSoftVfxSprite[] volumes;
  }
  const int CraftedEffectBudget=64;
  readonly List<CraftedEffect> craftedEffects=new List<CraftedEffect>();
  float impactCameraUntil,impactCameraStrength,nextImpactVisual;
  int slashSequence;
  public int ActiveCraftedVfx {get{return craftedEffects.Count;}}
  public int CraftedVfxBudget {get{return CraftedEffectBudget;}}
  public string ClassSpellSound {get{return "spell"+SelectedHero;}}
  Vector3 CombatCameraOffset
  {
   get
   {
    if(ReducedFlashes || State!=Phase.Playing || Time.time>=impactCameraUntil)return Vector3.zero;
    float fade=Mathf.Clamp01((impactCameraUntil-Time.time)/.18f);
    return new Vector3(Mathf.Sin(Time.time*43),.35f*Mathf.Sin(Time.time*37),Mathf.Cos(Time.time*47))*impactCameraStrength*fade;
   }
  }
  void CombatImpactCamera(float strength)
  {if(ReducedFlashes)return;impactCameraStrength=Mathf.Clamp(strength,0,.10f);impactCameraUntil=Time.time+.18f;}
  GameObject VfxRoot(string name,Vector3 position)
  {var root=new GameObject("VFX • "+name);root.transform.SetParent(world,false);root.transform.position=position;return root;}
  LineRenderer VfxLine(Transform parent,string name,Vector3[] points,Color color,float width,bool loop=false)
  {
   var node=new GameObject(name);node.transform.SetParent(parent,false);var line=node.AddComponent<LineRenderer>();
   line.useWorldSpace=false;line.loop=loop;line.positionCount=points.Length;line.SetPositions(points);
   line.sharedMaterial=art.effectMaterial;line.startColor=line.endColor=color;line.widthMultiplier=width;
   line.numCornerVertices=2;line.numCapVertices=2;
   line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
   return line;
  }
  LineRenderer VfxArc(Transform parent,string name,float radius,float from,float to,float y,Color color,float width,int segments=36)
  {
   var points=new Vector3[segments+1];
   for(int j=0;j<=segments;j++){float a=Mathf.Lerp(from,to,j/(float)segments)*Mathf.Deg2Rad;points[j]=new Vector3(Mathf.Sin(a)*radius,y,Mathf.Cos(a)*radius);}
   return VfxLine(parent,name,points,color,width);
  }
  void RegisterVfx(GameObject root,float duration,float spin=0,float rise=0,float scaleFrom=1,float scaleTo=1,float delay=0,bool growLines=false,Transform follow=null)
  {
   if(craftedEffects.Count>=CraftedEffectBudget){var first=craftedEffects[0];if(first.root){first.root.SetActive(false);Destroy(first.root);}craftedEffects.RemoveAt(0);}
   var lines=root.GetComponentsInChildren<LineRenderer>();var start=new Color[lines.Length];var end=new Color[lines.Length];var width=new float[lines.Length];
   for(int i=0;i<lines.Length;i++){start[i]=lines[i].startColor;end[i]=lines[i].endColor;width[i]=lines[i].widthMultiplier;}
   var effect=new CraftedEffect{root=root,born=Time.time+delay,duration=Mathf.Max(.05f,duration),spin=spin,rise=rise,origin=root.transform.position,scaleFrom=scaleFrom,scaleTo=scaleTo,lines=lines,starts=start,ends=end,widths=width,growLines=growLines,follow=follow,volumes=root.GetComponentsInChildren<NocturnisSoftVfxSprite>()};
   root.transform.localScale=Vector3.one*scaleFrom;craftedEffects.Add(effect);if(delay>0)root.SetActive(false);
  }
  void UpdateCraftedVfx()
  {
   for(int n=craftedEffects.Count-1;n>=0;n--)
   {
    var v=craftedEffects[n];if(!v.root){craftedEffects.RemoveAt(n);continue;}
    float elapsed=Time.time-v.born;if(elapsed<0)continue;
    float t=elapsed/v.duration;if(t>=1){Destroy(v.root);craftedEffects.RemoveAt(n);continue;}
    if(!v.root.activeSelf)v.root.SetActive(true);
    float fade=Mathf.SmoothStep(1,0,Mathf.InverseLerp(.28f,1,t));
    if(ReducedFlashes)fade*=.72f;
    v.root.transform.position=(v.follow ? v.follow.position : v.origin)+Vector3.up*v.rise*t;
    v.root.transform.localScale=Vector3.one*Mathf.Lerp(v.scaleFrom,v.scaleTo,Mathf.SmoothStep(0,1,t));
    if(v.spin!=0)v.root.transform.Rotate(Vector3.up,v.spin*Time.deltaTime,Space.World);
    foreach(var volume in v.volumes)if(volume)volume.Advance(View,t,fade,ReducedFlashes);
    for(int i=0;i<v.lines.Length;i++)if(v.lines[i])
    {
     Color a=v.starts[i],b=v.ends[i];a.a*=fade;b.a*=fade;v.lines[i].startColor=a;v.lines[i].endColor=b;
     v.lines[i].widthMultiplier=v.widths[i]*(v.growLines ? Mathf.Min(1,t*6) : Mathf.Lerp(1,.2f,t));
    }
   }
  }
  void ResetCraftedVfx()
  {foreach(var v in craftedEffects)if(v.root){v.root.SetActive(false);Destroy(v.root);}craftedEffects.Clear();impactCameraUntil=0;}
  void VfxParticles(Vector3 center,Color color,int count,float speed,float size,float lifetime,bool falling=false,float radius=.4f)
  {
   if(ReducedFlashes){count=Mathf.Max(3,count/2);color.a*=.72f;}
   var ps=Emitter("VFX • Ember / leaf / shard",world,center,color,false,0,lifetime,size,speed);
   var main=ps.main;main.startLifetime=new ParticleSystem.MinMaxCurve(lifetime*.5f,lifetime);main.gravityModifier=falling ? .8f : -.08f;main.maxParticles=Mathf.Min(80,count+1);main.useUnscaledTime=false;
   var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)Mathf.Min(80,count))});
   var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=radius;
   var fade=ps.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(color,.6f)},new[]{new GradientAlphaKey(.85f,0),new GradientAlphaKey(.45f,.5f),new GradientAlphaKey(0,1)});fade.color=gradient;
   var shrink=ps.sizeOverLifetime;shrink.enabled=true;shrink.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,0));
   ps.Play();RegisterVfx(ps.gameObject,lifetime+.12f);
  }
  void RunicSeal(Transform parent,float radius,Color color,int points)
  {
   // Engravings support the volume instead of drawing a bright wire diagram across the whole cast.
   color.a*=.30f;
   VfxArc(parent,"Outer inscription",radius,0,360,.11f,color,.055f,64);
   VfxArc(parent,"Broken inner circle",radius*.84f,15,325,.115f,new Color(color.r,color.g,color.b,color.a*.65f),.027f,48);
   for(int i=0;i<points;i++)
   {
    float a=i*Mathf.PI*2/points;var outward=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));var side=new Vector3(outward.z,0,-outward.x);
    Vector3 c=outward*radius*.93f+Vector3.up*.12f;float s=Mathf.Min(.23f,radius*.09f);
    VfxLine(parent,"Engraved rune",new[]{c-side*s,c+outward*s,c+side*s,c-outward*s,c-side*s,c+outward*s*1.8f},color,.035f);
   }
  }
  void ClassAttackVfx(Vector3 origin,Vector3 target,bool ranged)
  {
   Color c=Hero.color;
   if(!ranged)
   {
    slashSequence++;var slash=VfxRoot(SelectedHero==4 ? "Thorn cleaver" : "Ash blade",origin);slash.transform.rotation=Quaternion.LookRotation(facing);
    if(SelectedHero==0){
     var path=new Vector3[20];for(int i=0;i<path.Length;i++){float t=i/(float)(path.Length-1);path[i]=new Vector3(0,Mathf.Lerp(1.8f,.2f,t),.6f+Mathf.Sin(t*Mathf.PI)*1.1f);}
     var edge=VfxLine(slash.transform,"Falling blade",path,new Color(c.r,c.g,c.b,.65f),.12f);edge.widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.5f,1),new Keyframe(1,0));RegisterVfx(slash,.2f);return;
    }
    float sign=slashSequence%2==0 ? 1 : -1;
    var ribbon=VfxArc(slash.transform,"Cutting ribbon",Hero.range*.83f,-76,76,.85f,c,.24f,26);
    ribbon.widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.55f,1),new Keyframe(1,0));
    SoftLineGlow(ribbon,new Color(c.r,c.g,c.b,.65f),.75f);
    VfxArc(slash.transform,"Silver edge",Hero.range*.86f,-70,72,.88f,Color.Lerp(c,Color.white,.65f),.045f,24);
    VfxArc(slash.transform,"Dissipating wake",Hero.range*.68f,-63,66,.77f,new Color(c.r,c.g,c.b,.45f),.15f,20);
    RegisterVfx(slash,.34f,sign*70,0,.86f,1.06f);
    VfxParticles(origin+facing*1.3f+Vector3.up*.8f,c,SelectedHero==4 ? 8 : 6,1.1f,.1f,.5f,true);
   }
   else
   {
    var bolt=VfxRoot(SelectedHero==1 ? "Blood filament" : SelectedHero==2 ? "Moonbolt" : "Void lance",Vector3.zero);
    Vector3 from=player.HandPosition(SelectedHero==2),targetPoint=target+Vector3.up;Vector3 delta=targetPoint-from;Vector3 side=Vector3.Cross(delta.normalized,Vector3.up)*.08f;
    var path=new Vector3[9];for(int i=0;i<path.Length;i++){float u=i/8f;path[i]=Vector3.Lerp(from,targetPoint,u)+side*Mathf.Sin(u*Mathf.PI*4)*(SelectedHero==2 ? 0 : 1);}
    var line=VfxLine(bolt.transform,"Tracer",path,c,SelectedHero==2 ? .045f : .10f);line.startColor=new Color(c.r,c.g,c.b,.15f);line.endColor=Color.Lerp(c,Color.white,.45f);
    SoftLineGlow(line,new Color(c.r,c.g,c.b,.65f),SelectedHero==2 ? .20f : .35f);
    if(SelectedHero==2)VfxLine(bolt.transform,"Arrowhead",new[]{targetPoint-delta.normalized*.4f+side*2,targetPoint,targetPoint-delta.normalized*.4f-side*2},c,.07f);
    else VfxArc(bolt.transform,"Muzzle seal",.3f,0,350,.9f,c,.04f,16).transform.position=from-Vector3.up*.9f;
    RegisterVfx(bolt,.22f);VfxParticles(from,c,5,.8f,.1f,.35f);
   }
  }
  void ClassImpactVfx(Vector3 point,bool heavy=false)
  {
   // A large AoE shares a burst budget instead of allocating a light and material for every victim.
   if(!heavy && Time.time<nextImpactVisual)return;nextImpactVisual=Time.time+.035f;
   Color color=Hero.color;var root=VfxRoot("Confirmed hit",point+Vector3.up*.8f);
   for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;Vector3 d=new Vector3(Mathf.Cos(a),Mathf.Sin(a)*.6f,Mathf.Sin(a));VfxLine(root.transform,"Impact splinter",new[]{d*.06f,d*(heavy ? .7f : .45f)},color,.065f);}
   RegisterVfx(root,.27f,0,0,.6f,1.3f);VfxParticles(point+Vector3.up*.7f,color,heavy ? 15 : 5,heavy ? 2.2f : 1.3f,.09f,.65f,true);
   if(heavy)CombatImpactCamera(.075f);
  }
  void PaintEnemyTelegraph(Enemy enemy,float reach,float windup)
  {
   if(!enemy.warning)return;var line=enemy.warning.GetComponent<LineRenderer>();if(!line)return;
   // Integrated creatures inherited a 2.5m warning; display the actual damage reach instead.
   float progress=Mathf.Clamp01(1-(enemy.impactAt-Time.time)/Mathf.Max(.01f,windup));
   Color tint=Color.Lerp(new Color(.8f,.25f,.09f,.65f),new Color(1,.4f,.1f,.95f),progress);
   line.startColor=line.endColor=tint;line.widthMultiplier=Mathf.Lerp(.045f,.14f,progress);
   int count=line.positionCount;for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;line.SetPosition(i,new Vector3(Mathf.Cos(a)*(reach+.3f),.14f,Mathf.Sin(a)*(reach+.3f)));}
  }
  public void ClassSpellVfx(int slot,Vector3 center,float radius)
  {
   if(player==null || world==null)return;
   Color c=Hero.color;bool defense=slot==2 || slot==3,ultimate=slot==5;
   var root=VfxRoot(Hero.name+" • "+slot,center);float duration=defense ? 1.35f : ultimate ? 1.8f : 1.25f;
   radius=Mathf.Clamp(radius,.8f,7f);RunicSeal(root.transform,radius,c,SelectedHero==3 ? 7 : SelectedHero==4 ? 5 : 8);
   switch(SelectedHero)
   {
    case 0: AshJudgement(root.transform,radius,c,defense,ultimate);break;
    case 1: BloodWeave(root.transform,radius,c,defense,ultimate);break;
    case 2: DuskArrows(root.transform,radius,c,defense,ultimate);break;
    case 3: VoidOrrery(root.transform,radius,c,defense,ultimate);break;
    case 4: RootAwakening(root.transform,radius,c,defense,ultimate);break;
   }
   SoftCastVolume(root.transform,SelectedHero,radius,defense,ultimate);
   RegisterVfx(root,duration,SelectedHero==3 ? -34 : SelectedHero==1 ? 13 : 0,0,.88f,1,0,true,defense ? player.Root.transform : null);
   VfxParticles(center+Vector3.up*.5f,c,ultimate ? 56 : defense ? 18 : 38,defense ? .6f : SelectedHero==0 ? 2.3f : 1.5f,SelectedHero==0 ? .23f : .19f,1.2f,SelectedHero!=3,defense ? .65f : radius*.55f);
   if(!defense)CombatImpactCamera(ultimate ? .10f : .045f);
  }
  void AshJudgement(Transform root,float r,Color c,bool defense,bool ultimate)
  {
   int n=defense ? 4 : ultimate ? 10 : 6;
   for(int i=0;i<n;i++)
   {
    float a=i*Mathf.PI*2/n;Vector3 d=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));Vector3 side=new Vector3(d.z,0,-d.x);
    Vector3 p=d*r*.7f;float height=defense ? 1.6f : ultimate ? 3.7f : 2.2f;
    Color edge=new Color(c.r,c.g,c.b,.35f);
    var blade=VfxLine(root,"Flaming sword",new[]{p+Vector3.up*.14f,p+Vector3.up*height,p+side*.12f+Vector3.up*(height-.25f),p+Vector3.up*height,p-side*.12f+Vector3.up*(height-.25f)},edge,.055f);
    SoftLineGlow(blade,new Color(1,.35f,.04f,.7f),.48f);
    VfxLine(root,"Sword crossguard",new[]{p-side*.32f+Vector3.up*.55f,p+side*.32f+Vector3.up*.55f},edge,.04f);
    if(!defense){var crack=VfxLine(root,"Ground fracture",new[]{d*.2f+Vector3.up*.13f,d*r*.36f+side*.15f+Vector3.up*.13f,d*r*.52f-side*.13f+Vector3.up*.13f,d*r*.95f+Vector3.up*.13f},edge,.045f);SoftLineGlow(crack,new Color(1,.23f,.025f,.65f),.32f);}
   }
   if(ultimate){VfxLine(root,"Judgement column",new[]{Vector3.up*.12f,Vector3.up*5.8f},new Color(1,.7f,.32f,.65f),.4f);}
  }
  void BloodWeave(Transform root,float r,Color c,bool defense,bool ultimate)
  {
   int strands=ultimate ? 9 : 5;
   for(int n=0;n<strands;n++)
   {
    var pts=new Vector3[25];for(int j=0;j<pts.Length;j++){float t=j/24f,a=t*Mathf.PI*1.3f+n*Mathf.PI*2/strands;float d=r*Mathf.Lerp(.2f,.9f,t);pts[j]=new Vector3(Mathf.Sin(a)*d,.15f+Mathf.Sin(t*Mathf.PI)*(defense ? 1.45f : .8f),Mathf.Cos(a)*d);}
    var l=VfxLine(root,"Arterial ribbon",pts,c,ultimate ? .16f : .10f);l.widthCurve=AnimationCurve.EaseInOut(0,1,1,.12f);
    SoftLineGlow(l,new Color(.8f,.04f,.09f,.65f),.55f);
    l.endColor=new Color(.4f,.02f,.07f,.15f);
   }
   VfxArc(root,"Sanguine inner seal",r*.35f,0,360,.19f,new Color(.8f,.08f,.13f),.18f,32);
  }
  void DuskArrows(Transform root,float r,Color c,bool defense,bool ultimate)
  {
   int n=defense ? 5 : ultimate ? 13 : 8;
   for(int i=0;i<n;i++)
   {
    float a=i*2.399963f;Vector3 p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(defense ? r*.7f : r*.84f*Mathf.Sqrt((i+1f)/n));
    if(defense)
    {VfxLine(root,"Shadow veil",new[]{p+Vector3.up*.15f,p*.85f+Vector3.up*1.3f,p*.5f+Vector3.up*2.15f},new Color(c.r*.5f,c.g*.7f,c.b,.65f),.14f);continue;}
    VfxLine(root,"Falling thorn",new[]{p+Vector3.up*(ultimate ? 4 : 2.4f)+Vector3.back*.6f,p+Vector3.up*.16f},c,.055f);
    VfxLine(root,"Barbed landing",new[]{p+new Vector3(-.2f,.38f,0),p+Vector3.up*.15f,p+new Vector3(.2f,.38f,0)},Color.Lerp(c,Color.white,.3f),.05f);
   }
   for(int i=0;i<4;i++)VfxArc(root,"Moon feather",r*.62f,i*90+12,i*90+66,.14f,c,.075f,10);
  }
  void VoidOrrery(Transform root,float r,Color c,bool defense,bool ultimate)
  {
   for(int i=0;i<3;i++)
   {
    float d=defense ? .9f : Mathf.Min(r*.54f,2.5f);var l=VfxArc(root,"Tilted orbit",d,0,350,0,new Color(c.r,c.g,c.b,.45f),.045f,48);
    l.transform.localPosition=Vector3.up*(defense ? 1 : ultimate ? 2.25f : 1.4f);l.transform.localRotation=Quaternion.Euler(30+i*48,i*60,24+i*22);
    SoftLineGlow(l,new Color(c.r,c.g,c.b,.75f),.42f);
   }
   int n=ultimate ? 12 : 7;
   for(int i=0;i<n;i++)
   {
    float a=i*Mathf.PI*2/n;Vector3 p=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*r*.8f;
    VfxLine(root,"Infall filament",new[]{p+Vector3.up*.15f,p*.65f+Vector3.up*.4f,p*.25f+Vector3.up*1.1f,Vector3.up*(ultimate ? 2.25f : 1.4f)},new Color(c.r,c.g,c.b,.22f),.035f);
   }
   var horizon=VfxArc(root,"Event horizon",defense ? .34f : .65f,0,360,ultimate ? 2.25f : 1.4f,new Color(c.r,c.g,c.b,.7f),.12f,32);SoftLineGlow(horizon,new Color(.6f,.25f,1,.85f),.6f);
  }
  void RootAwakening(Transform root,float r,Color c,bool defense,bool ultimate)
  {
   int n=defense ? 5 : ultimate ? 11 : 7;
   for(int i=0;i<n;i++)
   {
    float a=i*Mathf.PI*2/n;Vector3 d=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));Vector3 side=new Vector3(d.z,0,-d.x);
    Vector3 p=d*r*.65f;float h=defense ? 1.7f : ultimate ? 3.2f : 1.9f;
    var stem=VfxLine(root,"Rising thorn root",new[]{p+Vector3.up*.1f,p*.96f+side*.16f+Vector3.up*h*.35f,p*.8f-side*.16f+Vector3.up*h*.7f,p*.6f+Vector3.up*h},new Color(.31f,.42f,.17f),.25f);
    stem.widthCurve=AnimationCurve.EaseInOut(0,1,1,.05f);
    VfxLine(root,"Living sap",new[]{p+Vector3.up*.12f,p*.96f+side*.14f+Vector3.up*h*.35f,p*.8f-side*.14f+Vector3.up*h*.7f,p*.6f+Vector3.up*h},c,.045f);
    VfxLine(root,"Root branches",new[]{p*.7f+side*.6f+Vector3.up*h*.76f,p*.8f+Vector3.up*h*.55f,p*.7f-side*.5f+Vector3.up*h*.82f},c,.075f);
   }
  }
  // Review hook executes the real action and its real resource, damage and cooldown logic.
  // Test rigs choose their level/resources/zone explicitly; this method grants no invulnerability.
  public void ReviewCastVfx(int slot,Vector3 targetPoint)
  {
   if(!Testing || State!=Phase.Playing)return;
   if(slot==0){if(BeginPower(0))Strike(targetPoint);}else if(slot==1)CastSkill(targetPoint);else if(slot==2)CastDefense();else if(slot>=3 && slot<=5)CastAdditional(slot-3,targetPoint);
  }
 }
}
