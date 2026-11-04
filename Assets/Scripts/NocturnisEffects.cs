using System.Collections.Generic;
using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaGame
 {
  sealed class Pickup { public GameObject obj;public Vector3 origin;public int value;public bool grace; }
  sealed class Effect { public GameObject obj;public float end,duration;public bool expand;public float spin;public LineRenderer[] lines;public Color[] starts,ends; }
  sealed class Hazard { public Vector3 pos;public float radius,end,activeAt,tickAt,born,lastSample;public bool hostile;public string kind;public GameObject ring;public LineRenderer edge,charge; }
  readonly List<Pickup> pickups=new List<Pickup>();
  readonly List<Effect> effects=new List<Effect>();
  readonly List<Hazard> hazards=new List<Hazard>();
  public sealed class CombatText { public Vector3 position;public string text;public Color color;public float until; }
  public readonly List<CombatText> combatText=new List<CombatText>();
  GameObject buffHalo;
  LineRenderer[] buffLines;
  float buffSparkAt;
  // Preserve readable combat windows across a hitch. The discarded interval is
  // never a debt of damage to apply to somebody who entered the area afterwards.
  // At >=10 FPS this leaves the original timing unchanged; pause uses scaled time.
  static float ObserveCombatWindow(ref float lastSample,ref float born,ref float activeAt,ref float end)
  {
   float elapsed=Mathf.Max(0,Time.time-lastSample),step=Mathf.Min(.1f,elapsed),lost=elapsed-step;
   lastSample=Time.time;born+=lost;activeAt+=lost;end+=lost;return step;
  }
  GameObject Ring(Transform parent,float radius,float width,Material material)
  {
   GameObject obj=new GameObject("Pieczęć");obj.transform.SetParent(parent,false);
   LineRenderer l=obj.AddComponent<LineRenderer>();l.useWorldSpace=false;l.loop=true;l.positionCount=48;l.widthMultiplier=width;l.sharedMaterial=art.effectMaterial;
   l.startColor=l.endColor=material.color;l.numCornerVertices=2;
   l.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
   for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;l.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,.08f,Mathf.Sin(a)*radius));}
   return obj;
  }
  void Burst(Vector3 pos,float radius,Material mat)
  {
   var obj=Ring(world,radius,.18f,mat);obj.transform.position=pos;effects.Add(new Effect{obj=obj,end=Time.time+.65f,duration=.65f,expand=true});
  }
  void Beam(Vector3 from,Vector3 to,Material mat)
  {
   var go=new GameObject("Łańcuch energii");go.transform.SetParent(world);var l=go.AddComponent<LineRenderer>();
   l.useWorldSpace=true;l.positionCount=5;l.widthMultiplier=.12f;l.sharedMaterial=art.effectMaterial;l.startColor=l.endColor=mat.color;
   for(int i=0;i<5;i++)l.SetPosition(i,Vector3.Lerp(from,to,i/4f)+Vector3.up*(i%2)*.22f);
   effects.Add(new Effect{obj=go,end=Time.time+.28f,duration=.28f});
  }
  void Sparks(Vector3 pos,Color color,int count)
  {
   if(ReducedFlashes){count=Mathf.Max(2,count/2);color.a*=.65f;}
   var ps=Emitter("Iskry Ichoritu",world,pos+Vector3.up*.6f,color,false,0,.8f,.24f,3);var go=ps.gameObject;
   var main=ps.main;main.gravityModifier=.35f;
   var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});
   var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.5f;
   ps.Play();effects.Add(new Effect{obj=go,end=Time.time+1,duration=1});
  }
  ParticleSystem Emitter(string name,Transform parent,Vector3 pos,Color color,bool loop,float rate,float lifetime,float size,float speed)
  {
   var go=new GameObject(name);go.transform.SetParent(parent);go.transform.position=pos;
   var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   var main=ps.main;main.loop=loop;main.duration=loop ? 5 : 1;main.startLifetime=lifetime;main.startSpeed=speed;main.startSize=new ParticleSystem.MinMaxCurve(size*.6f,size);main.startColor=color;main.maxParticles=160;main.simulationSpace=ParticleSystemSimulationSpace.World;main.playOnAwake=false;main.useUnscaledTime=false;
   var emission=ps.emission;emission.rateOverTime=rate;
   var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.3f;
   var r=ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=art.particleMaterial;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
   return ps;
  }
  void SpellCrown(Vector3 pos,float radius,Material material,float duration=.95f)
  {
   var root=new GameObject("Krąg mocy");root.transform.SetParent(world);root.transform.position=pos;
   Ring(root.transform,radius,.14f,material);Ring(root.transform,radius*.88f,.045f,material);
   for(int i=0;i<8;i++)
   {
    float a=i*Mathf.PI/4;Vector3 p=new Vector3(Mathf.Cos(a)*radius*.76f,0,Mathf.Sin(a)*radius*.76f);
    var rune=Ring(root.transform,.18f,.055f,material);rune.transform.localPosition=p;
    var column=new GameObject("Promień runy");column.transform.SetParent(root.transform,false);
    var line=column.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=2;line.SetPosition(0,p);line.SetPosition(1,p+Vector3.up*2.8f);
    line.startWidth=.16f;line.endWidth=.02f;line.sharedMaterial=art.effectMaterial;line.startColor=material.color;line.endColor=new Color(material.color.r,material.color.g,material.color.b,0);
   }
   effects.Add(new Effect{obj=root,end=Time.time+duration,duration=duration,spin=25});
   Sparks(pos,material.color,28);
  }
  void Slash(Vector3 pos,Vector3 direction)
  {
   var root=new GameObject("Łuk ostrza");root.transform.SetParent(world);root.transform.position=pos+Vector3.up*.8f;root.transform.rotation=Quaternion.LookRotation(direction);
   var line=root.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=18;line.widthMultiplier=.17f;line.sharedMaterial=art.effectMaterial;line.startColor=line.endColor=Hero.color;
   for(int i=0;i<18;i++){float a=Mathf.Lerp(-1.2f,1.2f,i/17f);line.SetPosition(i,new Vector3(Mathf.Sin(a)*1.7f,.2f*Mathf.Sin(a),Mathf.Cos(a)*1.7f));}
   effects.Add(new Effect{obj=root,end=Time.time+.28f,duration=.28f});
  }
  void Floating(Vector3 p,string message,Color color)
  {if(combatText.Count>40)combatText.RemoveAt(0);combatText.Add(new CombatText{position=p+Vector3.up*1.8f,text=message,color=color,until=Time.time+1});}
  void UpdateBuffVisual()
  {
   bool active=Status.Entries.Count>0;
   if(!buffHalo)
   {
    buffHalo=new GameObject("Aura aktywnych mocy");buffHalo.transform.SetParent(player.Root.transform,false);
    // Different persistent silhouettes for the five classes, remaining below shoulder height.
    RunicSeal(buffHalo.transform,.76f,Hero.color,SelectedHero==3 ? 7 : 5);
    if(SelectedHero==0 || SelectedHero==4)
     for(int i=0;i<4;i++){float a=i*Mathf.PI/2;Vector3 p=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*.65f;VfxLine(buffHalo.transform,"Ward",new[]{p+Vector3.up*.18f,p*.9f+Vector3.up*.8f,p*.7f+Vector3.up*1.35f},Hero.color,.045f);}
    else if(SelectedHero==3)
     for(int i=0;i<2;i++){var l=VfxArc(buffHalo.transform,"Entropic orbit",.66f,15,330,0,Hero.color,.035f,28);l.transform.localPosition=Vector3.up*.8f;l.transform.localRotation=Quaternion.Euler(25+i*70,0,35);}
    else
     for(int i=0;i<3;i++)VfxArc(buffHalo.transform,SelectedHero==1 ? "Blood helix" : "Shadow feather",.65f,i*120+10,i*120+83,.35f+i*.2f,Hero.color,.045f,12);
    buffLines=buffHalo.GetComponentsInChildren<LineRenderer>();
   }
   buffHalo.SetActive(active);if(!active)return;
   Color c=Status.Count("ichor",Time.time)>0 ? art.violetMaterial.color : Status.Count("slaughter",Time.time)>0 ? art.redMaterial.color : Status.Count("grace",Time.time)>0 ? art.goldMaterial.color : Hero.color;
   c.a=ReducedFlashes ? .48f : .7f;
   buffHalo.transform.localRotation=Quaternion.Euler(0,Time.time*(ReducedFlashes ? 8 : 22),0);
   if(buffLines!=null)foreach(var line in buffLines)if(line)line.startColor=line.endColor=c;
   if(Time.time>buffSparkAt){buffSparkAt=Time.time+1.2f;VfxParticles(HeroPosition+Vector3.up*.5f,c,4,.35f,.09f,.8f);}
  }
  void UpdateEffects()
  {
   UpdateStageGuardianCleanup();UpdateCraftedVfx();
   // Discard only old transient effects under extreme AoE load; persistent encounter seals remain.
   for(int i=0;effects.Count>96 && i<effects.Count;)
    if(effects[i].duration<10){if(effects[i].obj)Destroy(effects[i].obj);effects.RemoveAt(i);}else i++;
   for(int i=effects.Count-1;i>=0;i--)
   {
    var e=effects[i];if(!e.obj || Time.time>=e.end){if(e.obj)Destroy(e.obj);effects.RemoveAt(i);continue;}
    float life=Mathf.Clamp01((e.end-Time.time)/e.duration);
    if(e.expand)e.obj.transform.localScale=Vector3.one*Mathf.Lerp(.2f,1,1-life);
    if(e.spin!=0)e.obj.transform.Rotate(Vector3.up,Time.deltaTime*e.spin);
    if(e.lines==null){e.lines=e.obj.GetComponentsInChildren<LineRenderer>();e.starts=new Color[e.lines.Length];e.ends=new Color[e.lines.Length];for(int j=0;j<e.lines.Length;j++){e.starts[j]=e.lines[j].startColor;e.ends[j]=e.lines[j].endColor;}}
    for(int j=0;j<e.lines.Length;j++)if(e.lines[j])
    {Color c=e.starts[j];c.a*=life*(ReducedFlashes ? .7f : 1);e.lines[j].startColor=c;c=e.ends[j];c.a*=life*(ReducedFlashes ? .7f : 1);e.lines[j].endColor=c;}
   }
   for(int i=combatText.Count-1;i>=0;i--)if(combatText[i].until<Time.time)combatText.RemoveAt(i);
   UpdateBuffVisual();
  }
  void Drop(Vector3 pos,int value,bool grace)
  {
   var o=GameObject.CreatePrimitive(grace ? PrimitiveType.Sphere : PrimitiveType.Cylinder);o.name=grace ? "Kula Łaski" : "Złoto";o.transform.SetParent(world);
   o.transform.localScale=grace ? Vector3.one*.38f : new Vector3(.35f,.07f,.35f);o.GetComponent<Renderer>().sharedMaterial=art.goldMaterial;Destroy(o.GetComponent<Collider>());
   pickups.Add(new Pickup{obj=o,origin=pos,value=value,grace=grace});
  }
  void UpdatePickups()
  {
   bool collected=false;
   for(int i=pickups.Count-1;i>=0;i--)
   {
    var p=pickups[i];float d=Vector3.Distance(p.origin,player.Root.transform.position);
    if(d<3 && Visible(p.origin,player.Root.transform.position))p.origin=Vector3.MoveTowards(p.origin,player.Root.transform.position,8*Time.deltaTime);
    p.obj.transform.position=p.origin+Vector3.up*(.4f+Mathf.Sin(Time.time*4+i)*.1f);p.obj.transform.Rotate(Vector3.up,Time.deltaTime*90);
    if(d<.65f)
    {if(p.grace){Status.Apply("grace","Łaska Upadłych",Time.time,15,3);ShowNotice("Łaska Upadłych • warstwy: "+Status.Count("grace",Time.time));}else {Coins+=p.value;Floating(HeroPosition,"+"+p.value+" zł",art.goldMaterial.color);}Sound("coin");Destroy(p.obj);pickups.RemoveAt(i);collected=true;}
   }
   if(collected)SaveCheckpoint();
  }
  void AddHazard(Vector3 pos,float radius,float duration,bool hostile,Color color,string kind)
  {
   if(!grid.Walkable(Cell(pos.x),Cell(pos.z)))return;
   if(hazards.Count>=20){if(hazards[0].ring)Destroy(hazards[0].ring);hazards.RemoveAt(0);}
   var ring=VfxRoot(hostile ? "Enemy danger zone" : "Lingering thorns",pos);
   Color tint=hostile ? new Color(1,.25f,.12f,.88f) : color;
   var edge=VfxArc(ring.transform,"Exact damage boundary",radius,0,360,.14f,tint,.10f,64);
   var charge=VfxArc(ring.transform,"Telegraph countdown",radius*.9f,0,360,.15f,tint,.045f,48);
   for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Vector3 d=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));VfxLine(ring.transform,"Danger tooth",new[]{d*radius*.77f+Vector3.up*.16f,d*radius*.94f+Vector3.up*.16f},tint,.07f);}
   hazards.Add(new Hazard{pos=pos,radius=radius,end=Time.time+duration,born=Time.time,lastSample=Time.time,activeAt=Time.time+(hostile ? 1 : 0),tickAt=Time.time+(hostile ? 1 : 0),hostile=hostile,kind=kind,ring=ring,edge=edge,charge=charge});
  }
  void UpdateHazards()
  {
   for(int i=hazards.Count-1;i>=0;i--)
   {
    var h=hazards[i];
    if(h.hostile)
    {
     float priorEnd=h.end;
     ObserveCombatWindow(ref h.lastSample,ref h.born,ref h.activeAt,ref h.end);
     h.tickAt+=h.end-priorEnd;
    }
    if(Time.time>=h.end){Destroy(h.ring);hazards.RemoveAt(i);continue;}
    float charge=h.hostile ? Mathf.Clamp01((Time.time-h.born)/Mathf.Max(.01f,h.activeAt-h.born)) : 1;
    h.edge.widthMultiplier=Mathf.Lerp(.075f,.16f,charge);
    h.charge.transform.localScale=Vector3.one*Mathf.Lerp(.1f,1,charge);
    // Player fields retain their real scaled-time lifetime, matching skill cooldowns.
    // After a hitch skip missed ticks, resolve only the current position, and keep
    // the original cadence. No backlog and no prolonged stacking of friendly fields.
    if(Time.time<h.tickAt)continue;
    h.tickAt+=.6f*(1+Mathf.Floor(Mathf.Max(0,Time.time-h.tickAt)/.6f));
    if(h.hostile)
    {
     if(Vector3.Distance(player.Root.transform.position,h.pos)<h.radius && Visible(h.pos,player.Root.transform.position))
     {if(Hurt(9)){ApplyIchor();Status.Apply("slow","Pętla Czasu",Time.time,2,1,true);}}
     VfxParticles(h.pos+Vector3.up*.15f,new Color(.9f,.25f,.12f),8,.7f,.12f,.55f,false,h.radius*.7f);
    }
    else
    {
     DamageBreakables(h.pos,h.radius,14+Level*2);
     foreach(var e in enemies)if(e.hp>0 && Vector3.Distance(e.Position,h.pos)<h.radius && Visible(h.pos,e.Position))
     {Damage(e,14+Level*2);if(h.kind=="web")e.slowUntil=Time.time+2;Beam(e.Position+Vector3.up*4,e.Position,classMaterial);}
    }
   }
  }
 }
}
