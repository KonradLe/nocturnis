using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Krypta
{
 public sealed partial class KryptaGame
 {
  // Outdoor geometry extends beyond the camera footprint. It does not extend
  // navigation: DungeonGrid still defines all clearings and blocked banks.
  void BuildOrganicGround()
  {
   var root=new GameObject("Organic terrain");root.transform.SetParent(world,false);
   var owner=root.AddComponent<NocturnisCryptGeometry>();
   groundMesh=environmentLayout.Surface();
   var ground=new GameObject("Ground without seams");ground.transform.SetParent(root.transform,false);
   ground.AddComponent<MeshFilter>().sharedMesh=groundMesh;var renderer=ground.AddComponent<MeshRenderer>();renderer.sharedMaterial=art.earthMaterial;
   if(Zone==Campaign.Ravine)
   {var stoneSoil=new Material(art.earthMaterial){name="Cold shale and mountain dust"};TintTerrainMaterial(stoneSoil,new Color(.70f,.83f,1.04f));owner.materials.Add(stoneSoil);renderer.sharedMaterial=stoneSoil;}
   renderer.shadowCastingMode=ShadowCastingMode.Off;
   BuildTerrainPaths(root.transform,owner);
  }
  float TerrainDistance(float x,float z)
  {
   // Euclidean distance to cell centres is smooth enough for the non-walkable bank.
   int cx=Mathf.RoundToInt(x),cz=Mathf.RoundToInt(z);float best=64;
   for(int dx=-6;dx<=6;dx++)for(int dz=-6;dz<=6;dz++)
   {int a=cx+dx,b=cz+dz;if(!grid.Walkable(a,b))continue;float d=(x-a)*(x-a)+(z-b)*(z-b);if(d<best)best=d;}
   return Mathf.Sqrt(best);
  }
  int[] TerrainPath()
  {
   if(Zone==Campaign.Ravine)return new[]{5,3,7,9,20,9,20,18,5,18,5,25,17,23,20,28,10,34,17,38};
   if(Zone==Campaign.Cloister)return new[]{5,3,7,9,19,9,19,15,5,15,5,23,17,23,20,28,10,28,10,34,17,38};
   if(Zone==Campaign.Road)return new[]{12,3,14,8,10,13,12,16,15,21,11,26,12,30,12,37};
   if(Zone==Campaign.Fields)return new[]{5,3,6,7,12,10,14,15,9,17,8,20,10,26,17,30,17,38};
   if(Zone==Campaign.Cemetery)return new[]{12,3,12,11,10,16,12,20,12,27,16,32,17,38};
   return new[]{5,3,5,8,9,12,5,17,7,22,13,24,12,30,18,33,17,38};
  }
  void BuildTerrainPaths(Transform parent,NocturnisCryptGeometry owner)
  {
   var vertices=new List<Vector3>();var triangles=new List<int>();var path=TerrainPath();
   for(int i=0;i<path.Length-2;i+=2)
   {
    Vector3 a=World(path[i],path[i+1]),b=World(path[i+2],path[i+3]);float length=Vector3.Distance(a,b);
    Vector3 side=Vector3.Cross((b-a).normalized,Vector3.up);int steps=Mathf.CeilToInt(length/.5f);
    for(int step=0;step<steps;step++)
    {
     Vector3 p=Vector3.Lerp(a,b,step/(float)steps),q=Vector3.Lerp(a,b,(step+1f)/steps);
     float w=.82f+.1f*Mathf.Sin(p.z*.6f+p.x*.4f);
     Vector3[] quad={p-side*w,p+side*w,q+side*w,q-side*w};bool supported=true;
     foreach(var v in quad)if(!grid.CanStand(v.x/Tile,v.z/Tile,.05f)){supported=false;break;}
     if(!supported)continue;int n=vertices.Count;foreach(var v in quad)vertices.Add(v+Vector3.up*.006f);
     // Match the up-facing winding used by the ground mesh.
     triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
    }
   }
   var material=new Material(art.earthMaterial){name="Worn footpath"};
   TintTerrainMaterial(material,Zone==Campaign.Ravine?new Color(.79f,.87f,.98f):Zone==Campaign.Cemetery?new Color(.65f,.65f,.60f):Zone==Campaign.Forest?new Color(.70f,.67f,.53f):new Color(.84f,.77f,.62f));
   owner.materials.Add(material);var mesh=CryptFlatMesh("Winding worn path",vertices,triangles);
   var uv=new List<Vector2>();foreach(var v in vertices)uv.Add(new Vector2(v.x*.24f,v.z*.24f));mesh.SetUVs(0,uv);mesh.RecalculateTangents();CryptMesh(parent,mesh,material,owner);
  }
  static void TintTerrainMaterial(Material material,Color tint)
  {
   string property=material.HasProperty("baseColorFactor")?"baseColorFactor":material.HasProperty("_BaseColor")?"_BaseColor":"_Color";
   if(material.HasProperty(property))material.SetColor(property,material.GetColor(property)*tint);
  }
  bool TerrainWaterCell(int x,int z)
  {
   return Zone==Campaign.Cistern && (x>=7&&x<=9&&z>=18&&z<=20 || x>=14&&x<=16&&z>=32&&z<=34 || x>=20&&x<=22&&z>=35&&z<=37);
  }
  void BuildTerrainFloorAccents(Transform parent,NocturnisCryptGeometry owner)
  {
   if(Zone==Campaign.Forge || Zone==Campaign.Threshold || Zone==Campaign.Galleries)BuildProcessionalInlays(parent,owner);
   if(Zone==Campaign.Cistern)
   {
    var waterVertices=new List<Vector3>();var waterTriangles=new List<int>();
    foreach(var center in new[]{World(8,19),World(15,33),World(21,36)})AddCryptQuad(waterVertices,waterTriangles,center+Vector3.down*.055f,Tile*3-.16f,Tile*3-.16f);
    var water=new Material(art.floorMaterial){name="Still water in sunken cisterns",color=new Color(.055f,.16f,.15f)};
    if(water.HasProperty("_Glossiness"))water.SetFloat("_Glossiness",.88f);if(water.HasProperty("_Metallic"))water.SetFloat("_Metallic",.28f);
    owner.materials.Add(water);CryptMesh(parent,CryptFlatMesh("Three recessed water basins",waterVertices,waterTriangles),water,owner);
    foreach(var center in new[]{World(8,19),World(15,33),World(21,36)})
    {
     var ripples=new GameObject("Cistern water ripples");ripples.transform.SetParent(parent,false);ripples.transform.position=center+Vector3.up*.01f;
     for(int i=0;i<3;i++){var ring=Ring(ripples.transform,.45f+i*.55f,.012f,art.tealMaterial);ring.transform.localPosition=Vector3.up*.001f*i;}
     AmbientMotes(center+Vector3.up*.4f,new Color(.15f,.32f,.29f,.2f),1.6f,.65f);
    }
   }
   if(Zone==Campaign.Chapel || Zone==Campaign.Ossuary || Zone==Campaign.Kings || Zone==Campaign.Heart)
    BuildCryptInlay(parent,owner);
   if(Zone==Campaign.Chapel)
   {
    var v=new List<Vector3>();var t=new List<int>();
    for(int z=16;z<=22;z++)if(grid.Walkable(8,z))AddCryptQuad(v,t,World(8,z)+Vector3.up*.014f,1.3f,Tile);
    var mat=new Material(art.floorMaterial){name="Faded chapel processional cloth",color=new Color(.20f,.065f,.075f)};owner.materials.Add(mat);
    CryptMesh(parent,CryptFlatMesh("Worn chapel aisle cloth",v,t),mat,owner);
   }
  }
  void BuildTerrainLandmarks()
  {
   BuildExtendedTerrainLandmarks();
   if(Campaign.Outdoors(Zone))
   {
    // Boulders follow the valley, never sit on navigable cells or quest markers.
    int placed=0;
    for(int z=3;z<DungeonGrid.Height-2;z+=3)for(int x=1;x<DungeonGrid.Width-1;x++)
    {
     if(placed>=18 || !grid.Border(x,z)||CryptDressingHash(x,z)%7!=0)continue;
     var boulder=Place(art.rocks[(x+z)%art.rocks.Length],World(x,z)+Vector3.down*.12f,1.15f,false,world);
     if(boulder)boulder.transform.rotation=Quaternion.Euler(0,CryptDressingHash(x,z)%360,0);if(++placed>=18)break;
    }
    if(Zone==Campaign.Forest)
    {
     var lib=NocturnisActILibrary.Current;if(lib){Place(lib.Model("tree_corrupted"),World(23,35),4.5f,false,world);Place(lib.Model("tree_dead_oak"),World(-1,25),4,false,world);}
     AmbientMotes(World(14,18)+Vector3.up*.5f,new Color(.31f,.42f,.27f,.35f),4,1.2f);
    }
    return;
   }
   if(!Campaign.Underground(Zone))return;
   // Threshold arches have supports only on blocked cells and span above actors.
   TerrainArch(5,12,Zone==Campaign.Ossuary?4.0f:3.4f);
   if(Zone==Campaign.Cellar)TerrainArch(12,26,3.2f);
   else if(Zone==Campaign.Chapel)TerrainArch(10,25,3.9f);
   else if(Zone==Campaign.Cistern)TerrainArch(12,26,3.1f);
   else TerrainArch(13,27,3.6f);
   if(Zone==Campaign.Ossuary)
   {
    for(int z=16;z<=22;z+=3)foreach(int x in new[]{0,16})
    {if(!grid.Border(x,z))continue;var p=CryptProp(art.coffin,World(x,z),1.0f,90);if(p){CryptDressingHeight(p,.6f,false);CryptDressingBounds(p,x,z,0,.05f);}}
   }
  }
  void BuildProcessionalInlays(Transform parent,NocturnisCryptGeometry owner)
  {
   var vertices=new List<Vector3>();var triangles=new List<int>();
   foreach(var cell in new[]{new Vector2Int(7,9),new Vector2Int(17,23),new Vector2Int(10,34)})
   {
    var center=World(cell.x,cell.y);
    for(int i=0;i<32;i++)
    {
     float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;var p=center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*1.5f;var q=center+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*1.5f;
     if(!grid.CanStand(p.x/Tile,p.z/Tile,.06f) || !grid.CanStand(q.x/Tile,q.z/Tile,.06f))continue;
     var d=(p-center).normalized*.04f;var e=(q-center).normalized*.04f;int n=vertices.Count;
     vertices.Add(p-d+Vector3.up*.018f);vertices.Add(p+d+Vector3.up*.018f);vertices.Add(q+e+Vector3.up*.018f);vertices.Add(q-e+Vector3.up*.018f);
     triangles.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});
    }
   }
   var material=new Material(art.floorMaterial){name="Processional stone inlay",color=Zone==Campaign.Forge?new Color(.32f,.23f,.12f):Zone==Campaign.Threshold?new Color(.31f,.29f,.36f):new Color(.27f,.28f,.25f)};
   owner.materials.Add(material);CryptMesh(parent,CryptFlatMesh("Three weathered ritual landings",vertices,triangles),material,owner);
  }
  GameObject TerrainLandmark(string id,int x,int z,float footprint,float yaw=0)
  {
   // Large landmarks need an actual blocked island; authored pins may be too narrow.
   // Find the nearest safe bank instead of silently omitting the visual landmark.
   float clearance=footprint/Tile*.72f+.5f;
   int bx=x,bz=z;float best=float.MaxValue;
   for(int dx=-5;dx<=5;dx++)for(int dz=-5;dz<=5;dz++)
   {
    int px=x+dx,pz=z+dz;if(TerrainDistance(px,pz)<clearance)continue;
    float cost=dx*dx+dz*dz;if(cost<best){best=cost;bx=px;bz=pz;}
   }
   if(best==float.MaxValue)return null;
   var lib=NocturnisActILibrary.Current;if(!lib)return null;
   var prop=Place(lib.Model(id),World(bx,bz),footprint,false,world);if(prop)prop.transform.rotation=Quaternion.Euler(0,yaw,0);return prop;
  }
  void BuildExtendedTerrainLandmarks()
  {
   if(Zone==Campaign.Ravine)
   {
    // The pass is above the tree line. Standard forest crowns obscured its rock walls.
    foreach(var tree in world.GetComponentsInChildren<NocturnisForestTree>())
    {tree.gameObject.SetActive(false);Destroy(tree.gameObject);}
    int rocks=0;
    for(int z=2;z<DungeonGrid.Height-1;z++)for(int x=0;x<DungeonGrid.Width;x++)
    {
     if(rocks>=52 || !grid.Border(x,z) || CryptDressingHash(x,z)%3!=0)continue;
     var rock=Place(art.rocks[(x+z)%art.rocks.Length],World(x,z),1.6f,false,world);
     if(rock){rock.name="Mountain shale bank";rock.transform.rotation=Quaternion.Euler(0,CryptDressingHash(x,z)%360,0);CryptDressingHeight(rock,CryptForeground(x,z)?1.1f:2.6f,true);CryptDressingBounds(rock,x,z,0,.06f);}
     if(++rocks>52)break;
    }
    foreach(var pin in new[]{new Vector2Int(12,12),new Vector2Int(11,21),new Vector2Int(12,29),new Vector2Int(2,17),new Vector2Int(23,30)})
     TerrainLandmark("cliff_face",pin.x,pin.y,3.6f,pin.y*7);
    TerrainLandmark("cliff_face",12,13,5.0f,12);TerrainLandmark("mountain_ridge",11,29,4.5f,90);
    TerrainLandmark("cliff_face",1,11,4.5f,170);TerrainLandmark("mountain_peak",27,23,9,45);
    TerrainLandmark("rock_boulder",16,14,2.6f,40);TerrainLandmark("rock_boulder",2,32,3.2f,75);
    AmbientMotes(World(20,17)+Vector3.up*1.7f,new Color(.60f,.63f,.65f,.22f),4,.8f);
   }
   if(Zone==Campaign.Cloister)
   {
    // Stone courts sit in the shaped outdoor soil; broken walls let the ruins breathe.
    BuildIntegratedFloor();TerrainLandmark("building_chapel",12,19,5.0f,180);
    TerrainLandmark("building_watchtower",25,11,4.5f,25);
    for(int z=2;z<DungeonGrid.Height-2;z++)for(int x=0;x<DungeonGrid.Width;x++)
     if(grid.Border(x,z) && CryptDressingHash(x,z)%3!=0)BuildCryptWallSegment(x,z);
    TerrainArch(19,12,3.7f);TerrainArch(5,20,3.5f);TerrainArch(10,31,3.8f);
    TerrainNiche(16,12,art.pillar,1.7f);TerrainNiche(22,16,art.pillar,1.6f);
   }
   if(Zone==Campaign.Galleries)
   {
    TerrainArch(19,12,3.2f);TerrainArch(5,20,3.0f);TerrainArch(20,26,3.4f);TerrainArch(10,33,3.0f);
    foreach(var cell in new[]{new Vector2Int(16,11),new Vector2Int(22,14),new Vector2Int(2,19),new Vector2Int(8,21),new Vector2Int(16,27),new Vector2Int(23,29)})
     TerrainNiche(cell.x,cell.y,art.coffin,.66f);
   }
   if(Zone==Campaign.Forge)
   {
    var altar=TerrainLandmark("crypt_altar",12,12,2.8f);if(altar){var p=altar.transform.position;Flame(p+Vector3.up*.9f,.6f);TerrainAccentLight("Furnace core",p+Vector3.up*2,new Color(1,.28f,.055f),3.2f,11);AmbientMotes(p+Vector3.up*1.8f,new Color(.36f,.26f,.17f,.25f),2.2f,2);}
    TerrainLandmark("ichorite_cluster",15,19,2.3f);
    // Large interior islands have no backing floor: keep the forge boulder out of that void.
    foreach(var c in new[]{new Vector2Int(10,12),new Vector2Int(14,12)})TerrainLandmark("crypt_pillar",c.x,c.y,.8f);
    var chain=new GameObject("Black forge chain");chain.transform.SetParent(world,false);var line=chain.AddComponent<LineRenderer>();line.sharedMaterial=art.effectMaterial;
    line.startColor=line.endColor=new Color(.13f,.11f,.09f);line.widthMultiplier=.10f;line.positionCount=5;
    for(int i=0;i<5;i++)line.SetPosition(i,Vector3.Lerp(World(10,12),World(14,12),i/4f)+Vector3.up*(2.8f-.6f*Mathf.Sin(i*Mathf.PI/4)));
    TerrainArch(20,12,3.4f);TerrainArch(5,21,3.4f);TerrainArch(19,28,3.8f);
    TerrainNiche(7,21,art.torch,1.3f);TerrainNiche(22,29,art.torch,1.3f);
   }
   if(Zone==Campaign.Threshold)
   {
    TerrainArch(20,13,4.3f);TerrainArch(4,27,4.0f);TerrainArch(20,33,4.5f);
    foreach(var c in new[]{new Vector2Int(17,17),new Vector2Int(22,20),new Vector2Int(2,29),new Vector2Int(12,32),new Vector2Int(18,34)})TerrainNiche(c.x,c.y,art.pillar,2.4f);
    // Ritual pillars already sit on supported wall cells. Do not place heavy sarcophagus
    // or altar models in the unmeshed cutaway void between the processional corridors.
    // Processional steps climb only outside navigation, framing the final corridor.
    for(int side=0;side<2;side++)for(int step=0;step<3;step++)
    {
     int x=side==0?16:24,z=31+step;if(grid.Walkable(x,z)||grid.Border(x,z))continue;
     Cube("Steps beside ritual hall",World(x,z)+Vector3.up*(step*.12f),new Vector3(1.2f,.24f*(step+1),1.6f),art.wallMaterial);
    }
    AmbientMotes(World(17,23)+Vector3.up*1.3f,new Color(.31f,.24f,.39f,.22f),3.0f,1.0f);
   }
   if(Zone==Campaign.Galleries || Zone==Campaign.Forge || Zone==Campaign.Threshold)BuildStageAtmosphere();
  }
  void TerrainAccentLight(string name,Vector3 position,Color color,float intensity,float range)
  {
   var lamp=new GameObject(name).AddComponent<Light>();lamp.transform.SetParent(world,false);lamp.transform.position=position;
   lamp.type=LightType.Point;lamp.color=color;lamp.intensity=intensity;lamp.range=range;lamp.shadows=LightShadows.None;
   if(Zone==Campaign.Forge)lamp.gameObject.AddComponent<NocturnisFlicker>();
  }
  void BuildStageAtmosphere()
  {
   var root=new GameObject("Distinct atmosphere: "+Zone);root.transform.SetParent(world,false);var owner=root.AddComponent<NocturnisCryptGeometry>();
   var accent=new Material(art.effectMaterial){name=Zone==Campaign.Forge?"Forge embers":Zone==Campaign.Threshold?"Tarnished ritual gold":"Cold bone dust"};
   accent.color=Zone==Campaign.Forge?new Color(1,.28f,.045f,.75f):Zone==Campaign.Threshold?new Color(.56f,.36f,.14f,.7f):new Color(.38f,.58f,.56f,.6f);owner.materials.Add(accent);
   var used=new List<Vector2Int>();
   // Repeat themed wall niches along the route, including the centre crossing.
   foreach(var pin in new[]{new Vector2Int(7,8),new Vector2Int(13,17),new Vector2Int(18,23),new Vector2Int(7,29),new Vector2Int(15,35)})
   {
    int bx=-1,bz=0;float best=float.MaxValue;
    for(int x=1;x<DungeonGrid.Width-1;x++)for(int z=2;z<DungeonGrid.Height-2;z++)
    {
     if(!CryptSafeCell(x,z) || CryptForeground(x,z))continue;bool duplicate=false;
     foreach(var old in used)if(Mathf.Abs(x-old.x)+Mathf.Abs(z-old.y)<4)duplicate=true;if(duplicate)continue;
     float score=(x-pin.x)*(x-pin.x)+(z-pin.y)*(z-pin.y);if(score<best){best=score;bx=x;bz=z;}
    }
    if(bx<0)continue;used.Add(new Vector2Int(bx,bz));Vector3 p=World(bx,bz),inward=CryptInterior(bx,bz);
    if(Zone==Campaign.Forge)
    {
     var furnace=CryptProp(art.pillar,p,.92f);if(furnace){furnace.name="Black forge furnace";CryptDressingHeight(furnace,2,true);CryptDressingBounds(furnace,bx,bz,0,.06f);}
     Flame(p+Vector3.up*1.85f,.45f);TerrainAccentLight("Orange furnace glow",p+inward*1.1f+Vector3.up*2.4f,new Color(1,.32f,.055f),3.6f,10);
     var grate=Cube("Furnace hot mouth",p+inward*.71f+Vector3.up*1.35f,new Vector3(.55f,.42f,.08f),accent);grate.transform.rotation=Quaternion.LookRotation(inward);
     for(int link=0;link<8;link++)
     {var chain=Ring(root.transform,.12f,.032f,art.wallMaterial);chain.name="Hanging forge chain";chain.transform.position=p+inward*.56f+Vector3.up*(1.7f+link*.2f);chain.transform.rotation=Quaternion.Euler(90,link%2*90,0);}
     AmbientMotes(p+Vector3.up*2.3f,new Color(1,.35f,.06f,.65f),.7f,1.7f);
    }
    else if(Zone==Campaign.Threshold)
    {
     var pillar=CryptProp(art.pillar,p,.85f);if(pillar){pillar.name="Pillar of the last oath";CryptDressingHeight(pillar,2.5f,true);CryptDressingBounds(pillar,bx,bz,0,.06f);}
     TerrainAccentLight("Violet oath light",p+inward*1.2f+Vector3.up*2.3f,new Color(.51f,.22f,1),2.7f,10);
     for(int band=0;band<3;band++){var ring=Ring(root.transform,.46f,.045f,accent);ring.transform.position=p+Vector3.up*(1.1f+band*.4f);}
    }
    else
    {
     var coffin=CryptProp(art.coffin,p,.95f,Quaternion.LookRotation(inward).eulerAngles.y);if(coffin){coffin.name="Sealed burial niche";CryptDressingHeight(coffin,1.65f,true);CryptDressingBounds(coffin,bx,bz,.75f,.06f);}
     for(int bone=0;bone<3;bone++){var skull=CryptProp(art.skull,p+inward*.35f+Vector3.right*(bone-1)*.22f+Vector3.up*1.8f,.22f);if(skull)skull.transform.SetParent(root.transform,true);}
     TerrainAccentLight("Cold burial lamp",p+inward+Vector3.up*2.1f,new Color(.28f,.66f,.65f),1.8f,8);
    }
   }
   if(Zone==Campaign.Threshold)
   foreach(var pin in new[]{new Vector2Int(7,9),new Vector2Int(12,18),new Vector2Int(17,23),new Vector2Int(10,34)})
   {
    var cell=grid.NearestWalkable(pin.x,pin.y);Vector3 p=World(cell.X,cell.Z)+Vector3.up*.04f;
    foreach(float radius in new[]{.76f,.95f}){var seal=Ring(root.transform,radius,.025f,accent);seal.name="Gold oath inlay";seal.transform.position=p;}
    AmbientMotes(p+Vector3.up*.7f,new Color(.5f,.24f,.8f,.3f),1.1f,.7f);
   }
  }
  void TerrainNiche(int x,int z,GameObject prefab,float height)
  {
   if(!CryptSafeCell(x,z))return;var prop=CryptProp(prefab,World(x,z),.85f,Quaternion.LookRotation(CryptInterior(x,z)).eulerAngles.y);
   if(prop){CryptDressingHeight(prop,height,false);CryptDressingBounds(prop,x,z,0,.04f);}
  }
  void TerrainArch(int x,int z,float height)
  {
   if(!grid.Walkable(x,z))return;
   int left=x,right=x;while(left>0 && grid.Walkable(left,z))left--;while(right<DungeonGrid.Width-1 && grid.Walkable(right,z))right++;
   if(right-left<3 || right-left>6)return;
   Vector3 a=World(left,z),b=World(right,z);var root=new GameObject("Vaulted threshold");root.transform.SetParent(world,false);
   foreach(var p in new[]{a,b})
   {
    var pillar=Place(art.pillar,p,.65f,false,root.transform);if(pillar)CryptDressingHeight(pillar,height*.75f,true);
   }
   // Seven wedge-shaped voussoirs, with a clear 2.1 m route under the crown.
   float radius=(b.x-a.x)*.5f;Vector3 center=(a+b)*.5f+Vector3.up*(height-radius*.45f);
   for(int i=0;i<9;i++)
   {
    float angle=(20+i*17.5f)*Mathf.Deg2Rad;
    Vector3 p=center+new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius*.45f,0);
    var block=Cube("Vault masonry",p,new Vector3(.56f,.39f,.50f),art.wallMaterial);block.transform.SetParent(root.transform,true);block.transform.rotation=Quaternion.Euler(0,0,(angle*Mathf.Rad2Deg-90)*.5f);
   }
  }
 }
}
