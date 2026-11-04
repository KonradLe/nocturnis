using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Krypta
{
 public sealed partial class KryptaGame
 {
  Transform cryptDressingRoot;
  NocturnisArtisanKit artisanKit;
  NocturnisArtisanKit ArtisanModels { get { if(!artisanKit)artisanKit=Resources.Load<NocturnisArtisanKit>("ArtisanKit");return artisanKit; } }

  int CryptDressingHash(int x,int z)
  {
   // The same map is dressed identically after loading a checkpoint.
   unchecked{return (int)(((uint)(x*73856093)^(uint)(z*19349663)^(uint)(Zone*83492791))&0x7fffffff);}
  }
  bool CryptForeground(int x,int z){return grid.Walkable(x,z+1) || grid.Walkable(x-1,z);}
  int CryptFloorNeighbours(int x,int z)
  {
   return (grid.Walkable(x+1,z) ? 1 : 0)+(grid.Walkable(x-1,z) ? 1 : 0)+(grid.Walkable(x,z+1) ? 1 : 0)+(grid.Walkable(x,z-1) ? 1 : 0);
  }
  Vector3 CryptInterior(int x,int z)
  {
   if(grid.Walkable(x,z-1))return Vector3.back;
   if(grid.Walkable(x+1,z))return Vector3.right;
   if(grid.Walkable(x,z+1))return Vector3.forward;
   return Vector3.left;
  }
  bool CryptPillarCell(int x,int z)
  {
   return !CryptForeground(x,z) && CryptSafeCell(x,z) && (x*3+z)%6==0;
  }
  bool CryptShelfCell(int x,int z)
  {
   return (Zone==Campaign.Cellar || Zone==Campaign.Crypt2) && !CryptForeground(x,z) && CryptSafeCell(x,z) && CryptDressingHash(x,z)%7==0;
  }
  void BuildCryptWallSegment(int x,int z)
  {
   if(!grid.Border(x,z) || TerrainWaterCell(x,z))return;
   // The four isolated Heart cells belong to the boss's destructible columns.
   if(Zone==Campaign.Heart && CryptFloorNeighbours(x,z)>=3)return;
   bool low=CryptForeground(x,z);
   Vector3 inward=CryptInterior(x,z);float rotation=Quaternion.LookRotation(inward).eulerAngles.y;
   var kit=CryptKit;
   bool cracked=(Zone==Campaign.Crypt3 || Zone==Campaign.Cellar) ? CryptDressingHash(x,z)%3==0 : CryptDressingHash(x,z)%11==0;
   GameObject prefab=kit ? (low ? kit.wallLow : CryptShelfCell(x,z) ? kit.wallShelves : cracked ? kit.wallCracked : kit.wall) : null;
   if(!prefab)prefab=art.cryptWall;
   var wall=CryptProp(prefab,World(x,z)-inward*.17f,Tile,rotation);
   if(wall)
   {
    wall.name="Mur katakumb ["+x+","+z+"]";
    CryptDressingHeight(wall,low ? .62f : Zone==Campaign.Kings ? 1.9f : 1.75f,true);
    CryptDressingBounds(wall,x,z,0,0);
    if(low)foreach(var renderer in wall.GetComponentsInChildren<Renderer>())renderer.shadowCastingMode=ShadowCastingMode.Off;
   }
   if(CryptPillarCell(x,z))
   {
    GameObject pillar=kit ? (Zone==Campaign.Kings || Zone==Campaign.Heart ? kit.pillarDecorated : kit.pillar) : art.pillar;
    var post=CryptProp(pillar,World(x,z)-inward*.1f,.82f,rotation);
    if(post){post.name="Filar katakumb ["+x+","+z+"]";CryptDressingHeight(post,1.9f,false);CryptDressingBounds(post,x,z,0,.04f);}
   }
  }
  void BuildCryptDressing()
  {
   cryptDressingRoot=new GameObject("Dekoracje katakumb").transform;cryptDressingRoot.SetParent(world,false);
   var candidates=new List<DungeonGrid.Cell>();
   for(int x=0;x<DungeonGrid.Width;x++)for(int z=0;z<DungeonGrid.Height;z++)
    if(grid.Border(x,z) && CryptSafeCell(x,z) && !CryptForeground(x,z) && !CryptPillarCell(x,z) && !CryptShelfCell(x,z) && CryptFloorNeighbours(x,z)<3)
     candidates.Add(new DungeonGrid.Cell(x,z));
   // Sorting by a fixed hash spreads details across the whole route, not just its entrance.
   candidates.Sort((a,b)=>CryptDressingHash(a.X,a.Z).CompareTo(CryptDressingHash(b.X,b.Z)));
   var selected=new List<DungeonGrid.Cell>();int flames=0;
   int limit=Zone==Campaign.Heart ? 12 : Zone==Campaign.Cellar ? 22 : 20;
   foreach(var cell in candidates)
   {
    bool nearby=false;
    foreach(var other in selected)if(Mathf.Abs(other.X-cell.X)<=2 && Mathf.Abs(other.Z-cell.Z)<=2){nearby=true;break;}
    if(nearby)continue;
    DressCryptNiche(cell.X,cell.Z,selected.Count,ref flames);selected.Add(cell);
    if(selected.Count>=limit)break;
   }
  }
  void DressCryptNiche(int x,int z,int index,ref int flames)
  {
   var kit=CryptKit;Vector3 inward=CryptInterior(x,z),along=new Vector3(inward.z,0,-inward.x);
   float angle=Quaternion.LookRotation(inward).eulerAngles.y;
   bool royal=Zone==Campaign.Kings || Zone==Campaign.Chapel,storage=Zone==Campaign.Cellar || Zone==Campaign.Crypt2;
   GameObject main=art.rubble;
   if(storage && kit)
    main=index%4==0 ? kit.crates : index%4==1 ? kit.barrels : index%4==2 ? kit.barrel : kit.tableBroken;
   else if(royal)main=index%3==0 ? art.chest : index%3==1 ? art.skull : art.rubble;
   else if(Zone==Campaign.Crypt3 && kit && index%3==0)main=kit.tableBroken;
   var authored=ArtisanModels;bool original=false;
   if(authored && index%5==0)
   {
    int form=(index/5+Zone)%3;
    GameObject model=form==0 ? authored.relicShrine : form==1 ? authored.brokenPillar : authored.urnNiche;
    if(model)
    {
     // These larger silhouettes replace the clutter group; they never share its banner or rubble.
     main=model;original=true;
     CryptNicheProp(model,x,z,inward*.48f,form==1 ? .70f : .78f,angle,1.4f);
    }
   }
   if(!original)
   {
    float width=main==art.skull ? .31f : .66f;
    CryptNicheProp(main,x,z,inward*.5f-along*.24f,width,angle+(index%3-1)*8,main==art.rubble ? .29f : .72f);
   }
   if(!original && index%2==0)
    CryptNicheProp(royal || Zone==Campaign.Heart ? art.skull : art.rubble,x,z,inward*.5f+along*.39f,.3f,angle+25,.28f);
   GameObject banner=kit ? (royal ? kit.bannerGold : Zone==Campaign.Crypt3 || Zone==Campaign.Heart ? kit.bannerRed : kit.bannerBlue) : art.banner;
   if(!original && Zone!=Campaign.Cellar && index%3==0)
    CryptNicheProp(banner,x,z,inward*.10f+Vector3.up*.38f,.63f,angle,1.34f);
   // Six small particle flames at most; the encounter supplies the actual point lights.
   if(!original && flames<6 && index%3==1)
   {
    GameObject candle;
    if(kit && kit.torchMounted && (Zone==Campaign.Crypt2 || Zone==Campaign.Heart))
     candle=CryptNicheProp(kit.torchMounted,x,z,inward*.12f+Vector3.up*.91f,.31f,angle,.65f);
    else if(kit && kit.shelfCandles)
     candle=CryptNicheProp(kit.shelfCandles,x,z,inward*.15f+Vector3.up*.85f,.52f,angle,.60f);
    else candle=CryptNicheProp(art.candles,x,z,inward*.5f+along*.37f,.28f,angle,.45f);
    Bounds bounds;
    if(candle && ModelBounds.TryGet(candle,out bounds))
    {Flame(new Vector3(bounds.center.x,bounds.max.y+.025f,bounds.center.z),.075f);flames++;}
   }
  }
  GameObject CryptNicheProp(GameObject prefab,int x,int z,Vector3 offset,float footprint,float rotation,float height)
  {
   if(!prefab || !grid.Border(x,z) || !CryptSafeCell(x,z))return null;
   var prop=CryptProp(prefab,World(x,z)+offset,footprint,rotation);
   if(!prop)return null;
   prop.name="Nisza ["+x+","+z+"] "+prefab.name;prop.transform.SetParent(cryptDressingRoot,true);
   CryptDressingHeight(prop,height,false);
   CryptDressingBounds(prop,x,z,offset.y,.04f);
   return prop;
  }
  static void CryptDressingHeight(GameObject prop,float height,bool exact)
  {
   Bounds bounds;if(!ModelBounds.TryGet(prop,out bounds) || bounds.size.y<.001f)return;
   float factor=height/bounds.size.y;if(!exact)factor=Mathf.Min(1,factor);
   Vector3 scale=prop.transform.localScale;scale.y*=factor;prop.transform.localScale=scale;
  }
  void CryptDressingBounds(GameObject prop,int x,int z,float baseHeight,float margin)
  {
   Bounds bounds;if(!ModelBounds.TryGet(prop,out bounds))return;
   Vector3 cell=World(x,z);float half=Tile*.5f-margin;
   float fit=Mathf.Min(1,(half*2)/Mathf.Max(.001f,Mathf.Max(bounds.size.x,bounds.size.z)));
   if(fit<1){prop.transform.localScale*=fit;if(!ModelBounds.TryGet(prop,out bounds))return;}
   // The renderer mesh bounds, not pivot guesses, keep every object on its backing cell.
   float dx=Mathf.Clamp(bounds.center.x,cell.x-half+bounds.extents.x,cell.x+half-bounds.extents.x)-bounds.center.x;
   float dz=Mathf.Clamp(bounds.center.z,cell.z-half+bounds.extents.z,cell.z+half-bounds.extents.z)-bounds.center.z;
   prop.transform.position+=new Vector3(dx,cell.y+baseHeight-bounds.min.y,dz);
  }
 }
}
