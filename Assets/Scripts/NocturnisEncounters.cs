using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaGame
 {
  bool UpdateSpecialEnemy(Enemy e)
  {
   if(UpdateStageGuardian(e))return true;
   if(e.hp<=0)return true;
   if(Time.time<e.stunUntil)return false;
   Vector3 p=e.Position,goal=HeroPosition;float distance=Vector3.Distance(p,goal);
   if(distance<12 && Visible(p,goal))e.alerted=true;
   if(!e.alerted)return false;
   if(e.sacrificial)
   {
    Vector3 altar=World(12,29);var route=new DungeonGrid(Zone);route.BuildDistances(12,29);
    Vector3 direction=RouteDirection(route,p,altar);Move(e.visual.Root.transform,direction*1.4f*Time.deltaTime,.25f);FaceTravel(e.visual,e.Position-p,Time.deltaTime);e.visual.Tick(true);
    if(Vector3.Distance(p,altar)<1.2f)
    {var boss=enemies.Find(b=>b.boss && b.hp>0);if(boss!=null){boss.hp=Mathf.Min(boss.maxHp,boss.hp+boss.maxHp*.06f);Beam(p+Vector3.up,boss.Position+Vector3.up,art.redMaterial);Floating(boss.Position,"OFIARA +6%",Color.red);}e.hp=0;e.visual.Root.SetActive(false);}
    return true;
   }
   if(e.boss && Zone==Campaign.Heart)
   {
    if(!e.mutated && e.hp<=e.maxHp*.5f)
    {
     e.mutated=true;e.impactAt=0;ShowNotice("MALAZAR • ROZDARCIE • unikaj szarży i żaru posadzki");
     if(!IntegratedMutation(e))for(int side=-1;side<=1;side+=2)for(int i=0;i<2;i++)
     {var arm=CubeOn(e.visual.Root.transform,"Ramię po mutacji",new Vector3(side*.85f,1.5f+i*.45f,0),new Vector3(1.4f,.23f,.25f),art.wallMaterial);arm.transform.localRotation=Quaternion.Euler(0,side*20,side*(i==0 ? -25 : 30));}
     SpellCrown(p,4,art.redMaterial,1.5f);
    }
    if(!e.mutated && Time.time>bossRitualAt)
    {
     bossRitualAt=Time.time+13;
     for(int i=0;i<2 && enemies.Count<29;i++)
     {
      var f=SpawnEnemy(i==0 ? 5 : 19,24,true,false,false,9,"Fanatyk ofiarny");
      // Keep the same summon IDs across visits, including already defeated slots.
      if((slain[Zone]&(1<<f.spawnId))!=0)
      {f.hp=0;f.rewarded=true;f.visual.Root.SetActive(false);}
      else {f.sacrificial=true;f.alerted=true;}
     }
     ShowNotice("Fanatycy idą do serca — zabij ich, zanim uleczą Malazara");
    }
    if(e.mutated && Time.time>e.specialAt && e.chargeAt==0)
    {
     e.specialAt=Time.time+8;e.chargeTarget=goal;e.chargeAt=Time.time+1.25f;
     Beam(p+Vector3.up*.1f,goal+Vector3.up*.1f,art.redMaterial);e.warning.SetActive(true);
     // At most two separated hot circles: the arena always retains a walkable safe corridor.
     int sector=((int)Time.time/8)%4;
     for(int i=0;i<2;i++){float a=(sector*90+i*180)*Mathf.Deg2Rad;AddHazard(World(12,25)+new Vector3(Mathf.Cos(a)*8,0,Mathf.Sin(a)*8),3.2f,5,true,Color.red,"heat");}
    }
    if(e.chargeAt>0)
    {
     if(Time.time<e.chargeAt){e.visual.Tick(false);return true;}
     Vector3 d=e.chargeTarget-p;Move(e.visual.Root.transform,d.normalized*13*Time.deltaTime,.4f);FaceTravel(e.visual,e.Position-p,Time.deltaTime);e.visual.Tick(true);
     if(Vector3.Distance(e.Position,goal)<2)Hurt(32);
     for(int i=arenaColumns.Count-1;i>=0;i--){var c=arenaColumns[i];if(c && Vector3.Distance(e.Position,c.transform.position)<2.5f){Sparks(c.transform.position,art.goldMaterial.color,20);int x=Cell(c.transform.position.x),z=Cell(c.transform.position.z);grid.OpenCell(x,z);playerRoute.OpenCell(x,z);navAt=0;Destroy(c);arenaColumns.RemoveAt(i);}}
     if(d.magnitude<.7f || Time.time>e.chargeAt+1.5f){e.chargeAt=0;e.warning.SetActive(false);}return true;
    }
   }
   if(e.archetype==7 && Time.time>e.specialAt)
   {
    e.specialAt=Time.time+11;
    var corpse=enemies.Find(v=>v!=e && v.hp<=0 && !v.boss && v.archetype!=7 && Vector3.Distance(v.corpse,p)<10);
    if(corpse!=null){corpse.hp=corpse.maxHp*.5f;corpse.corpseHideAt=0;corpse.visual.Root.SetActive(true);corpse.visual.Play("Idle",true,0);corpse.alerted=true;corpse.rewarded=true;Beam(p+Vector3.up,corpse.Position+Vector3.up,art.violetMaterial);ShowNotice("Kapłan wskrzesił strażnika — przerwij jego rytuał");}
   }
   if(e.archetype==4)
   {
    // Apparitions ignore wall collision but stay inside the world footprint.
    if(distance>1.6f){e.visual.Root.transform.position=Vector3.MoveTowards(p,goal,1.8f*Time.deltaTime);FaceTravel(e.visual,e.Position-p,Time.deltaTime);e.visual.Tick(true);return true;}
   }
   if((e.archetype==1 || e.archetype==2 || e.archetype==5 || e.archetype==7) && (e.impactAt>0 || (distance<10 && distance>3 && Visible(p,goal))))
   {
    if(e.impactAt<=0)e.visual.Tick(false);
    if(e.impactAt>0 && AdvanceEnemyAttack(e,Time.time,e.archetype==1 ? "1H_Ranged_Shoot" : "Cast_A"))
    {e.impactAt=0;e.warning.SetActive(false);Beam(p+Vector3.up,e.chargeTarget+Vector3.up,art.redMaterial);if(Vector3.Distance(goal,e.chargeTarget)<1.4f && Visible(p,goal))Hurt(11+CampaignDepth);if(e.archetype==5)AddHazard(e.chargeTarget,1.5f,3,true,Hero.color,"ichor");}
    if(Time.time>e.attackAt && e.impactAt==0){e.attackAt=Time.time+3;e.impactAt=Time.time+.9f;e.chargeTarget=goal;BeginEnemyMelee(e,goal);e.warning.SetActive(true);Burst(goal,1.3f,art.redMaterial);}
    return true;
   }
   if(e.archetype==3 && distance>2 && distance<7 && Time.time>e.specialAt)
   {e.specialAt=Time.time+5;AddHazard(goal,1.2f,1.6f,true,Color.red,"hound");Move(e.visual.Root.transform,(goal-p).normalized*1.5f,.25f);}
   return false;
  }
  GameObject CubeOn(Transform parent,string name,Vector3 position,Vector3 size,Material material)
  {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;Destroy(g.GetComponent<Collider>());return g;}
  void BuildCampaignScenery()
  {
   if(Zone==Campaign.Road)
   {
    BuildRoadWagon();var road=RoadPalette();
    for(int z=10;z<28;z+=6)
    {Vector3 p=World(8,z);Cube("Szubienica",p+Vector3.up*2,new Vector3(.25f,4,.25f),road.wood);Cube("Belka",p+new Vector3(1,4,0),new Vector3(2.2f,.25f,.25f),road.wood);for(int j=0;j<6;j++){float a=j*Mathf.PI/3;Cube("Żelazna klatka",p+new Vector3(1+Mathf.Cos(a)*.5f,2.7f,Mathf.Sin(a)*.5f),new Vector3(.08f,1.5f,.08f),road.iron);}Place(art.skull,p+new Vector3(1,2.1f,0),.4f,false,world);}
    drawbridge=Cube("Most zwodzony",World(12,34),new Vector3(4,.2f,4.7f),art.wallMaterial);drawbridge.transform.rotation=Quaternion.Euler(-65,0,0);DressRoadBridge();
    Place(art.church,World(12,40),9,false,world);
    var rain=Emitter("Deszcz nad Traktem",world,World(12,20)+Vector3.up*8,new Color(.6f,.65f,.85f,.4f),true,70,2,.08f,0);var shape=rain.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(22,.1f,60);var velocity=rain.velocityOverLifetime;velocity.enabled=true;velocity.y=-12;rain.Play();
   }
   if(Zone==Campaign.Fields)
   {
    Place(art.buildings[1],World(5,11),7,false,world);Place(art.buildings[2],World(19,20),7,false,world);Place(art.buildings[3],World(5,30),6,false,world);
    foreach(var p in new[]{World(5,11),World(19,20),World(5,30)}){Flame(p+Vector3.up*1.2f,.7f);AmbientMotes(p+Vector3.up*2,Color.gray,2,5);}
    Place(art.well,World(11,21),2.5f,false,world);Flame(World(11,21)+Vector3.up,.3f);
    foreach(var p in new[]{World(3,20),World(20,15)}){var r=Ring(world,1,.08f,art.tealMaterial);r.transform.position=p;PortalSeal(r.transform);}
   }
   if(Zone==Campaign.Cemetery)
   {
    for(int x=0;x<DungeonGrid.Width;x++)for(int z=0;z<DungeonGrid.Height;z++)if(grid.Border(x,z) && (x+z)%2==0){var f=Place(art.fence,World(x,z),1.8f,false,world);if(!NocturnisActILibrary.Current)foreach(var r in f.GetComponentsInChildren<Renderer>())r.sharedMaterial=art.wallMaterial;}
    foreach(var p in new[]{World(4,3),World(20,3),World(4,35),World(20,37)}){var tomb=Place(art.crypt,p,3.4f,false,world);if(!NocturnisActILibrary.Current)foreach(var r in tomb.GetComponentsInChildren<Renderer>())r.sharedMaterial=art.floorMaterial;Place(art.candles,p+Vector3.forward*2,.5f,false,world);Flame(p+Vector3.forward*2+Vector3.up*.6f,.2f);}
    for(int z=12;z<24;z+=3){Place(art.graves[z%3],World(7,z),1,false,world);Place(art.graves[z%3],World(17,z),1,false,world);}
   }
   if(Zone==Campaign.Heart)
   {
    Vector3 p=World(12,29);heart=NocturnisActILibrary.Current ? Place(NocturnisActILibrary.Current.Model("prop_god_heart"),p,3.6f,false,world) : StoneHeart(p);
    SpellCrown(p,2.3f,art.violetMaterial,99999);
    foreach(var q in new[]{World(5,18),World(19,18),World(5,32),World(19,32)}){var column=Place(art.pillar,q,1.2f,false,world);arenaColumns.Add(column);var chain=new GameObject("Łańcuch serca");chain.transform.SetParent(world);var l=chain.AddComponent<LineRenderer>();l.sharedMaterial=art.effectMaterial;l.startColor=l.endColor=new Color(.45f,.32f,.2f);l.widthMultiplier=.16f;l.positionCount=3;l.SetPosition(0,q+Vector3.up*4);l.SetPosition(1,Vector3.Lerp(q,p,.5f)+Vector3.up*2);l.SetPosition(2,p+Vector3.up*3);}
    for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Vector3 q=World(12,25)+new Vector3(Mathf.Cos(a)*17,0,Mathf.Sin(a)*17);Torch(q);}
   }
  }
 }
}
