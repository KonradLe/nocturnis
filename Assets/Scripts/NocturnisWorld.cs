using System.Collections.Generic;
using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaGame
 {
  sealed class Shrine { public Vector3 pos; public string id,name,key; public bool used; public GameObject ring; }
  readonly List<Shrine> shrines=new List<Shrine>();
  readonly HashSet<string> usedShrines=new HashSet<string>();
  void ResetShrineUsage(){usedShrines.Clear();}
  string[] CaptureShrineUsage(){var keys=new List<string>(usedShrines);keys.Sort(System.StringComparer.Ordinal);return keys.ToArray();}
  void RestoreShrineUsage(string[] saved)
  {
   usedShrines.Clear();if(saved==null)return;
   foreach(string key in saved)
   {
    if(usedShrines.Count>=256)break;if(string.IsNullOrEmpty(key))continue;
    string[] parts=key.Split(':');int zone,x,z;
    if(parts.Length!=4 || !int.TryParse(parts[0],out zone) || !int.TryParse(parts[2],out x) || !int.TryParse(parts[3],out z))continue;
    if(zone<0 || zone>=Campaign.Count || x<0 || x>=DungeonGrid.Width || z<0 || z>=DungeonGrid.Height)continue;
    if(parts[1]!="slaughter" && parts[1]!="grace" && parts[1]!="renewal")continue;
    usedShrines.Add(zone+":"+parts[1]+":"+x+":"+z);
   }
  }
  ActorVisual irma;
  GameObject portal;
  void CreateCamera()
  {
   if(Camera.main) Destroy(Camera.main.gameObject);
   var go=new GameObject("Kamera izometryczna");go.transform.SetParent(transform);View=go.AddComponent<Camera>();go.tag="MainCamera";
   View.orthographic=true;View.orthographicSize=9;View.farClipPlane=150;View.clearFlags=CameraClearFlags.SolidColor;
   go.AddComponent<AudioListener>();
   var sun=new GameObject("Światło martwego słońca").AddComponent<Light>();sun.transform.SetParent(transform);
   sun.type=LightType.Directional;sun.color=new Color(.90f,.91f,1f);sun.intensity=.85f;sun.cullingMask=~(1<<30);
   sun.transform.rotation=Quaternion.Euler(48,-32,0);sun.shadows=LightShadows.Soft;QualitySettings.shadowDistance=32;
   CreateActorFillLight();NocturnisSettings.ConfigureCamera(View);NocturnisSettings.Ensure(gameObject).ApplyVisualQuality();
   RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
  }
  void LoadZone(int zone)
  {
   CancelRecall();MarketOnZoneChange(zone);CancelCombatAction();lastMusicContact=-100;StashWorldLoot();ResetStageGuardians();ResetCraftedVfx();float previousHealth=Health;
   bool firstVisit=(Unlocked&(1<<zone))==0;
   Zone=zone;Unlocked|=1<<zone;ResetZoneEvents();
   if(world) { world.gameObject.SetActive(false);Destroy(world.gameObject); }
   player=null;irma=null;buffHalo=null;Zone=zone;enemies.Clear();pickups.Clear();effects.Clear();hazards.Clear();shrines.Clear();relics.Clear();combatText.Clear();
   target=null;hasDestination=false;navAt=0;Status.Clear();
   grid=new DungeonGrid(zone);playerRoute=new DungeonGrid(zone);
   environmentLayout=new NocturnisEnvironmentLayout(grid,zone);
   world=new GameObject(NocturnisData.Zones[zone]).transform;world.SetParent(transform);
   ConfigureEnvironmentAtmosphere();
   for(int x=0;x<DungeonGrid.Width;x++) for(int z=0;z<DungeonGrid.Height;z++)
   {
    if(grid.Walkable(x,z))
    {
     if(!Campaign.Outdoors(zone) && !Campaign.Underground(zone))Place(art.floor,World(x,z),Tile,true,world);
     // Outdoor soil is one continuous mesh, with no tile edges or cube sides.
    }
    else if(grid.Border(x,z))
    {
     if(Campaign.Outdoors(zone))
     {
      if(zone==Campaign.Road && x>=7 && x<=11 && z>=3 && z<=7)continue;
      if((zone!=Campaign.Cemetery && (x+z)%2==0) || (zone==Campaign.Cemetery && (x+z)%7==0))ForestTree(x,z);
      if(zone!=Campaign.Cemetery && zone!=Campaign.Forest && (x+z)%3==0) Place(art.rocks[(x+z)%art.rocks.Length],World(x,z)+Vector3.right*.4f,1.2f,false,world);
     }
     else if(Campaign.Underground(zone))
     {
      CryptWall(x,z);
     }
     else if(x==1 || x==22 || z==1 || z==21) Cube("Mury Oakhaven",World(x,z)+Vector3.up*.7f,new Vector3(Tile,1.4f,Tile),art.wallMaterial);
    }
   }
   if(Campaign.Underground(zone))BuildCryptFloor();
   BuildGroundBacking();
   if(zone==Campaign.Forest)BuildForestDepth();
   if(zone==0) BuildTown();
   else BuildEncounter();
   if(Campaign.Underground(zone))BuildCryptDetails();
   if(Campaign.Outdoors(zone))BuildForestDetails();
   BuildCampaignScenery();BuildIntegratedScenery();BuildSideQuestWorld();BuildBreakables();BuildEnvironmentDressing();
   exit=ExitPosition;
   portal=new GameObject("Przejście");portal.transform.SetParent(world);portal.transform.position=exit;
   Ring(portal.transform,1.3f,.1f,art.goldMaterial);
   PortalSeal(portal.transform);
   if(zone>0) Place(art.chest,exit,1,false,world);
   CreateHero();
   ApplyActorReadability(player.Root);
   // Oakhaven remains a safe full-heal haven. The outdoor/crypt entrance
   // bonus belongs to first discovery, never repeated waypoint use.
   if(zone>0) Health=Mathf.Min(MaxHealth,previousHealth+(firstVisit ? 30 : 0));
   HideDefeated();RestoreZoneWorldLoot();
   if(State==Phase.Playing)SaveCheckpoint();
   View.transform.position=player.Root.transform.position+new Vector3(9,15,-11);View.transform.LookAt(player.Root.transform.position);
   ShowNotice(NocturnisData.Zones[zone]);
  }
  void BuildTown()
  {
   Place(art.buildings[0],World(4,6),7.8f,false,world);
   Place(art.buildings[1],World(19,6),7.8f,false,world);
   Place(art.buildings[2],World(4,14),7.8f,false,world);
   Place(art.buildings[3],World(19,14),7.8f,false,world);
   Place(art.church,World(11,18),9,false,world);
   // Buildings are normalized to blocked grid footprints; the central square stays walkable.
   for(int i=0;i<4;i++) Torch(World(i%2==0 ? 8 : 15,i<2 ? 5 : 14));
   BuildTownsfolk();
   for(int i=0;i<4;i++)Place(art.trees[i%art.trees.Length],World(3+i*5,22),3,false,world);
  }
  void Torch(Vector3 p)
  {
   Place(art.torch,p,.7f,false,world);
   Light l=new GameObject("Żar latarni").AddComponent<Light>();l.transform.SetParent(world);l.transform.position=p+Vector3.up*2;
   l.type=LightType.Point;l.range=7;l.intensity=2.6f;l.color=new Color(1,.49f,.2f);
   Flame(p+Vector3.up*1.5f,.23f);
   l.gameObject.AddComponent<NocturnisFlicker>();
  }
  GameObject Cube(string name,Vector3 pos,Vector3 scale,Material mat)
  {
   GameObject o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(world);
   o.transform.position=pos;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=mat;Destroy(o.GetComponent<Collider>());return o;
  }
  static GameObject Place(GameObject prefab,Vector3 pos,float footprint,bool ground,Transform parent)
  {
   if(!prefab)return null;
   GameObject root=new GameObject(ground ? "Posadzka" : prefab.name);root.transform.SetParent(parent);root.transform.position=pos;
   GameObject obj=Instantiate(prefab,root.transform);ModelSurface.Apply(obj);Bounds b;
   if(!ModelBounds.TryGet(obj,out b))return root;
   float scale=footprint/Mathf.Max(.01f,Mathf.Max(b.size.x,b.size.z));obj.transform.localScale*=scale;
   obj.transform.localPosition=new Vector3((pos.x-b.center.x)*scale,(pos.y-(ground ? b.max.y : b.min.y))*scale,(pos.z-b.center.z)*scale);
   return root;
  }
  KryptaAssets.Character SkeletonVariant(int archetype,bool elite)
  {
   // Rogues represent archers/apparitions, mages represent casters and priests.
   // Armoured guards and other elites use Warriors; ordinary melee uses Minions.
   int index=archetype==1 || archetype==4 ? 2 : archetype==2 || archetype==7 ? 3 : archetype==6 || elite ? 1 : 0;
   if(art.skeletonVariants==null || index>=art.skeletonVariants.Length)return null;
   var variant=art.skeletonVariants[index];
   return variant!=null && variant.model && variant.animations!=null && variant.animations.Length>0 ? variant : null;
  }
  Enemy SpawnEnemy(int x,int z,bool zombie=false,bool boss=false,bool elite=false,int archetype=0,string label=null)
  {
   float hp=(boss ? (Zone==Campaign.Heart ? 1400 : 230) : elite ? 145 : zombie ? 65 : 75)*(1+CampaignDepth*.07f);
   hp*=StageGuardianHealthMultiplier(Zone,enemies.Count,elite);
   Enemy e=new Enemy {spawnId=enemies.Count,archetype=archetype,zombie=zombie,boss=boss,elite=elite,hp=hp,maxHp=hp,spellAt=Time.time+4,specialAt=Time.time+3,label=label ?? (boss ? "MALAZAR • KRWAWY BISKUP" : elite ? "HEROLD ICHORITU" : zombie ? "Wygłodniały Pokutnik" : "Zaprzysiężony szkielet")};
   var a=boss && Zone==Campaign.Heart || archetype==2 || archetype==7 ? art.bishop : zombie ? art.zombie : null;
   if(!boss && !zombie && archetype!=3 && archetype!=10)a=SkeletonVariant(archetype,elite) ?? a;
   var integrated=IntegratedEnemy(archetype,zombie,boss,elite);if(integrated!=null)a=integrated;
   GameObject hound=integrated==null && archetype==3 ? HoundModel() : null;
   e.visual=new ActorVisual(hound ? hound : a!=null ? a.model : art.skeleton,hound ? null : a!=null ? a.animations : art.skeletonAnimations,World(x,z),boss ? 2.8f : archetype==3 ? 1.15f : 1.75f,e.label);
   if(hound)Destroy(hound);
   e.visual.Root.transform.SetParent(world);if(integrated==null && !zombie && archetype!=10)e.visual.Equip(a!=null ? a.weapon : art.sword);
   if(integrated==null && archetype==8)OverseerAnchor(e);
   if(integrated==null && archetype==10)Gargoyle(e);
   if(archetype==3)e.visual.Root.transform.localScale=Vector3.one*1.2f;
   if(archetype==6 && SkeletonVariant(archetype,elite)==null)CubeOn(e.visual.Root.transform,"Tarcza przysięgi",new Vector3(-.5f,1,.2f),new Vector3(.65f,1,.18f),art.wallMaterial);
   e.warning=Ring(e.visual.Root.transform,boss ? 2.5f : 1.5f,.07f,art.redMaterial);e.warning.SetActive(false);
   if(elite || boss) Ring(e.visual.Root.transform,.8f,.08f,art.violetMaterial);
   ApplyActorReadability(e.visual.Root);
   enemies.Add(e);return e;
  }
  void AddShrine(int x,int z,string id,string name,Material mat)
  {
   Vector3 pos=World(x,z);Place(art.pillar,pos,.8f,false,world);
   var ring=Ring(world,1,.08f,mat);ring.transform.position=pos;
   string key=Zone+":"+id+":"+x+":"+z;
   var s=new Shrine {pos=pos,id=id,name=name,key=key,ring=ring,used=usedShrines.Contains(key)};shrines.Add(s);
   var orb=GameObject.CreatePrimitive(PrimitiveType.Sphere);orb.name=name;orb.transform.SetParent(ring.transform);
   orb.transform.localPosition=Vector3.up*2.4f;orb.transform.localScale=Vector3.one*.35f;orb.GetComponent<Renderer>().sharedMaterial=mat;Destroy(orb.GetComponent<Collider>());ring.SetActive(!s.used);
  }
  void UpdateInteraction(){UpdateCampaignInteraction();}
  public void Interact(){InteractCampaign();}
  void ConsumeShrine(Shrine shrine)
  {
   if(shrine==null || shrine.used || State!=Phase.Playing)return;
   shrine.used=true;usedShrines.Add(shrine.key);shrine.ring.SetActive(false);
   ActivateShrine(shrine.id);SaveCheckpoint();
  }
  void ActivateShrine(string id)
  {
   if(id=="slaughter")Status.Apply(id,"Pylon Rzezi",Time.time,30);
   else if(id=="grace")Status.Apply(id,"Łaska Upadłych",Time.time,15,3);
   else {Status.Remove("ichor");Heal(MaxHealth);Potions=Mathf.Min(5,Potions+1);}
   Sound("shrine");SpellCrown(player.Root.transform.position,2.2f,art.goldMaterial,1.2f);
   Burst(player.Root.transform.position,3,art.goldMaterial);ShowNotice(id=="slaughter" ? "Pylon Rzezi • 30 s • darmowe moce, odnowienie −75%" : id=="grace" ? "Łaska Upadłych • szybkość i łańcuchy światła" : "Źródło Życia • oczyszczenie i mikstura");
  }
 }
}
