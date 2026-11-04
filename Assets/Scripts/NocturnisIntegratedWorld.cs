using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Krypta {
 public sealed partial class KryptaGame {
  KryptaAssets.Character IntegratedEnemy(int type,bool zombie,bool boss,bool elite) {
   var lib=NocturnisActILibrary.Current;if(!lib)return null;
   string id=boss?(Zone==Campaign.Heart?"boss_malazar":"enemy_rotten_overseer"):
    type==1?"enemy_skeleton_archer":type==2||type==7||type==9?"enemy_blood_cultist":type==3?"enemy_mutant_hound":
    type==4?"enemy_penitent_wraith":type==5?"enemy_ichor_spitter":type==6?"enemy_royal_guard":type==8?"enemy_rotten_overseer":type==10?"enemy_gargoyle":
    elite&&!zombie?"enemy_horned_demon":zombie?"enemy_penitent":"enemy_royal_guard";
   return lib.Character(id);
  }
  KryptaAssets.Character IntegratedCitizen(string service,KryptaAssets.Character fallback) {
   var lib=NocturnisActILibrary.Current;if(!lib)return fallback;
   string id=service=="forge"?"npc_blacksmith":service=="alchemy"?"npc_alchemist":service=="lore"?"npc_chronicler":null;
   return id==null?fallback:lib.Character(id);
  }
  bool IntegratedMutation(Enemy e) {
   var lib=NocturnisActILibrary.Current;if(!lib)return false;
   var a=lib.Character("boss_malazar_mutated");var old=e.visual.Root;var rotation=old.transform.rotation;
   e.visual=new ActorVisual(a.model,a.animations,old.transform.position,3.35f,e.label+" • Rozdarcie");
   e.visual.Root.transform.SetParent(world);e.visual.Root.transform.rotation=rotation;ApplyActorReadability(e.visual.Root);
   e.warning=Ring(e.visual.Root.transform,2.5f,.07f,art.redMaterial);e.warning.SetActive(false);
   Ring(e.visual.Root.transform,.8f,.08f,art.violetMaterial);old.SetActive(false);Destroy(old);return true;
  }

  bool BuildIntegratedFloor() {
   var lib=NocturnisActILibrary.Current;if(!lib)return false;
   var root=new GameObject("Posadzka katakumb");root.transform.SetParent(world,false);
   var owner=root.AddComponent<NocturnisCryptGeometry>();owner.usesOriginalFloor=true;
   var groups=new Dictionary<Material,List<CombineInstance>>();
   var tinted=new Dictionary<Material,Material>();
   var stonePrefab=lib.Model("crypt_floor_slabs");
   var stoneSource=stonePrefab.GetComponentInChildren<Renderer>(true).sharedMaterial;
   var scannedStone=NocturnisMasonry.Create(Zone);
   var floorVertices=new List<Vector3>();var floorTriangles=new List<int>();
   float surfaceY=scannedStone ? NocturnisMasonry.TopHeight(stonePrefab)-.001f : 0;
   if(scannedStone)owner.materials.Add(scannedStone);
   for(int x=0;x<DungeonGrid.Width;x++)for(int z=0;z<DungeonGrid.Height;z++){
    if(!grid.Walkable(x,z)&&!grid.Border(x,z) || TerrainWaterCell(x,z))continue;
    int hash=CryptDressingHash(x,z);int variant=Zone==Campaign.Forge&&hash%7==0?2:(Zone==Campaign.Kings||Zone==Campaign.Heart||Zone==Campaign.Threshold)&&hash%17==0?1:hash%23==0?2:0;
    owner.moduleCounts[variant]++;var prefab=lib.Model(variant==1?"crypt_floor_seal":variant==2?"crypt_floor_grate":"crypt_floor_slabs");
    var tile=Matrix4x4.TRS(World(x,z),Quaternion.Euler(0,hash%4*90,0),Vector3.one);
    // One continuous stone surface follows exactly the original walkable/border
    // cells, including water cutouts. Authored iron/gold submeshes remain intact.
    if(scannedStone)AddCryptQuad(floorVertices,floorTriangles,World(x,z)+Vector3.up*surfaceY,Tile,Tile);
    foreach(var f in prefab.GetComponentsInChildren<MeshFilter>(true)){
     var materials=f.GetComponent<Renderer>().sharedMaterials;
     for(int sub=0;sub<f.sharedMesh.subMeshCount;sub++){
      var source=materials[sub];if(scannedStone&&source==stoneSource)continue;
      Material m;
      if(!tinted.TryGetValue(source,out m)){
       m=new Material(source){name="Act I masonry: "+Zone};
       Color tint=Zone==Campaign.Forge?new Color(1.03f,.74f,.47f):Zone==Campaign.Threshold?new Color(.86f,.70f,1.05f):Zone==Campaign.Galleries?new Color(.69f,.87f,.83f):Zone==Campaign.Cloister?new Color(.88f,.84f,.72f):Zone==Campaign.Cistern?new Color(.67f,.86f,.82f):Zone==Campaign.Chapel?new Color(.94f,.82f,.74f):Zone==Campaign.Ossuary?new Color(.81f,.80f,.73f):Zone==Campaign.Cellar?new Color(.85f,.76f,.60f):Zone==Campaign.Crypt3?new Color(.82f,.71f,.83f):Zone==Campaign.Kings?new Color(1,.91f,.76f):Color.white;
       TintTerrainMaterial(m,tint);tinted.Add(source,m);owner.materials.Add(m);
      }
      if(!groups.ContainsKey(m))groups[m]=new List<CombineInstance>();
      var detailMatrix=tile*f.transform.localToWorldMatrix;
      if(scannedStone)detailMatrix=NocturnisMasonry.ExposeMetalDetail(f.sharedMesh,sub,detailMatrix,surfaceY);
      groups[m].Add(new CombineInstance{mesh=f.sharedMesh,subMeshIndex=sub,transform=detailMatrix});
     }
    }
   }
   foreach(var entry in groups){var mesh=new Mesh{name="Metalowe detale posadzki",indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(entry.Value.ToArray(),true,true);CryptMesh(root.transform,mesh,entry.Key,owner);}
   if(scannedStone) {
    var mesh=CryptFlatMesh("Ciągła zużyta posadzka",floorVertices,floorTriangles);
    NocturnisMasonry.MapMesh(mesh,scannedStone);CryptMesh(root.transform,mesh,scannedStone,owner);
   }
   BuildTerrainFloorAccents(root.transform,owner);
   return true;
  }
  void BuildIntegratedScenery() {
   BuildTerrainLandmarks();
   var lib=NocturnisActILibrary.Current;if(!lib)return;
   if(Campaign.Outdoors(Zone)||Zone==Campaign.Town){
    // Backdrop stays outside the navigation grid and behind the playable route.
    for(int z=0;z<46;z+=11){Place(lib.Model(z%2==0?"mountain_ridge":"cliff_face"),World(-7,z),12,false,world);Place(lib.Model("cliff_face"),World(31,z),10,false,world);}
    Place(lib.Model("mountain_peak"),World(12,49),25,false,world);
   }
   if(Zone==Campaign.Town){Place(lib.Model("prop_astrolabe"),World(12,13),1.3f,false,world);Place(lib.Model("building_watchtower"),World(24,21),5,false,world);}
   if(Zone==Campaign.Forest){Place(lib.Model("tree_corrupted"),World(22,35),4.2f,false,world);}
   if(Campaign.Underground(Zone)){
    int count=0;
    for(int z=6;z<DungeonGrid.Height-3;z+=6)for(int x=2;x<DungeonGrid.Width-2;x++){
     if(!CryptSafeCell(x,z)||CryptForeground(x,z))continue;
     string id=count%3==0?"prop_sarcophagus":count%3==1?"ichorite_cluster":"crypt_iron_gate";
     var prop=CryptProp(lib.Model(id),World(x,z),.85f,Quaternion.LookRotation(CryptInterior(x,z)).eulerAngles.y);
     CryptDressingHeight(prop,count%3==0?.65f:1.5f,false);CryptDressingBounds(prop,x,z,0,0);count++;break;
    }
   }
  }
 }
}
