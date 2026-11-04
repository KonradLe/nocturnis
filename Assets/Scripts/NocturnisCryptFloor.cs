using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Krypta
{
 public sealed partial class KryptaGame
 {
  NocturnisCryptPalette cryptKit;
  NocturnisCryptPalette CryptKit { get { if(!cryptKit)cryptKit=Resources.Load<NocturnisCryptPalette>("CryptPalette");return cryptKit; } }
  NocturnisOriginalFloorKit originalFloorKit;
  NocturnisOriginalFloorKit OriginalFloorKit { get { if(!originalFloorKit)originalFloorKit=Resources.Load<NocturnisOriginalFloorKit>(NocturnisOriginalFloorKit.ResourceName);return originalFloorKit; } }
  GameObject CryptProp(GameObject prefab,Vector3 position,float footprint,float rotation=0)
  {
   var prop=Place(prefab,position,footprint,false,world);
   if(prop)prop.transform.rotation=Quaternion.Euler(0,rotation,0);
   return prop;
  }
  bool CryptSafeCell(int x,int z)
  {
   if(!grid.Border(x,z) || TerrainWaterCell(x,z))return false;
   Vector3 p=World(x,z);
   if(Vector3.Distance(p,SpawnPosition)<2.7f || Vector3.Distance(p,ExitPosition)<2.7f)return false;
   foreach(var shrine in shrines)if(Vector3.Distance(p,shrine.pos)<2.1f)return false;
   return true;
  }
  void BuildCryptFloor()
  {
   if(BuildIntegratedFloor())return;
   var original=OriginalFloorKit;
   if(original && original.IsReady){BuildOriginalCryptFloor(original);return;}
   var kit=CryptKit;
   var root=new GameObject("Posadzka katakumb");root.transform.SetParent(world,false);
   var owner=root.AddComponent<NocturnisCryptGeometry>();
   var groups=new Dictionary<Material,List<CombineInstance>>();
   var variations=new Dictionary<Material,Material>();
   var supportVertices=new List<Vector3>();var supportTriangles=new List<int>();
   for(int x=0;x<DungeonGrid.Width;x++)for(int z=0;z<DungeonGrid.Height;z++)
   {
    bool walk=grid.Walkable(x,z);
    if(!walk && !grid.Border(x,z))continue;
    int hash=CryptDressingHash(x,z);
    GameObject prefab=kit ? kit.floorPlain : art.cryptFloors[0];
    if(kit)
    {
     if(hash%9==0)prefab=kit.floorBrokenA;
     else if(hash%11==0)prefab=kit.floorBrokenB;
     // Raised stones and weeds belong to the masonry edge, never the walking surface.
     if(!walk && CryptForeground(x,z) && hash%4==0)prefab=kit.floorDecorated;
     else if(!walk && hash%4==1 && Zone!=Campaign.Kings)prefab=(hash/4)%2==0 ? kit.floorWeedsA : kit.floorWeedsB;
    }
    // KayKit's structural plate ends at Y=.05. Bounds.max also includes rubble,
    // so bounds-based top alignment must not be used for these floor modules.
    Matrix4x4 tile=Matrix4x4.TRS(World(x,z)+Vector3.down*(.05f*Tile/2),Quaternion.Euler(0,(hash%4)*90,0),Vector3.one*(Tile/2));
    foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
    {
     var renderer=filter.GetComponent<MeshRenderer>();if(!renderer || !filter.sharedMesh)continue;
     var materials=renderer.sharedMaterials;
     for(int sub=0;sub<filter.sharedMesh.subMeshCount && sub<materials.Length;sub++)
     {
      var source=materials[sub];if(!source)continue;
      Material material;
      if(!variations.TryGetValue(source,out material))
      {
       // One continuous stone tint per zone: changing it per module would cut
       // the interlocking atlas pattern into visible square patches.
       Color tint=Zone==Campaign.Cellar ? new Color(1,.94f,.84f) : Zone==Campaign.Crypt3 ? new Color(.95f,.87f,.94f) : Zone==Campaign.Kings ? new Color(1,.97f,.90f) : Zone==Campaign.Heart ? new Color(.88f,.87f,.97f) : Zone==Campaign.Crypt2 ? new Color(.90f,.97f,1) : new Color(.92f,.96f,1);
       material=new Material(source){name="Kamień katakumb"};owner.materials.Add(material);
       string property=source.HasProperty("baseColorFactor") ? "baseColorFactor" : source.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
       if(source.HasProperty(property))material.SetColor(property,source.GetColor(property)*tint);
       variations.Add(source,material);
      }
      List<CombineInstance> batch;
      if(!groups.TryGetValue(material,out batch)){batch=new List<CombineInstance>();groups.Add(material,batch);}
      batch.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=sub,transform=tile*prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix});
     }
    }
    AddCryptQuad(supportVertices,supportTriangles,World(x,z)+Vector3.down*.045f,Tile,Tile);
   }
   foreach(var group in groups)
   {
    var mesh=new Mesh{name="Łączona posadzka",indexFormat=IndexFormat.UInt32};
    mesh.CombineMeshes(group.Value.ToArray(),true,true);mesh.RecalculateBounds();
    CryptMesh(root.transform,mesh,group.Key,owner);
   }
   var grout=new Material(art.floorMaterial){name="Fugi i podparcie kamienia"};grout.color=new Color(.18f,.20f,.23f);owner.materials.Add(grout);
   CryptMesh(root.transform,CryptFlatMesh("Ciągłe podparcie",supportVertices,supportTriangles),grout,owner);
   if(Zone==Campaign.Kings || Zone==Campaign.Heart)BuildCryptInlay(root.transform,owner);
   BuildArtisanFloor(root.transform,owner);
  }
  void BuildOriginalCryptFloor(NocturnisOriginalFloorKit kit)
  {
   var root=new GameObject("Posadzka katakumb");root.transform.SetParent(world,false);
   var owner=root.AddComponent<NocturnisCryptGeometry>();owner.usesOriginalFloor=true;
   var batches=new List<CombineInstance>();
   var accents=new Dictionary<int,int>();
   bool ceremonial=Zone==Campaign.Kings || Zone==Campaign.Heart;
   foreach(var cell in ceremonial ? CryptRosetteCells() : CryptDrainCells())
    if(grid.Walkable(cell.x,cell.y))accents[cell.x+cell.y*DungeonGrid.Width]=ceremonial ? 5 : 4;
   for(int x=0;x<DungeonGrid.Width;x++)for(int z=0;z<DungeonGrid.Height;z++)
   {
    if(!grid.Walkable(x,z) && !grid.Border(x,z))continue;
    int hash=CryptDressingHash(x,z),module;
    if(!accents.TryGetValue(x+z*DungeonGrid.Width,out module))
    {
     // Most tiles carry the three quiet masonry rhythms; cracks remain occasional.
     int choice=hash%10;module=choice<3 ? 0 : choice<6 ? 1 : choice<9 ? 2 : 3;
    }
    owner.moduleCounts[module]++;
    var prefab=kit.Module(module);
    // These original modules already measure one 1.8 m grid cell, with their
    // highest surface at Y0. Do not apply the older KayKit .9 scale / Y offset.
    Matrix4x4 tile=Matrix4x4.TRS(World(x,z),Quaternion.Euler(0,((hash/10)%4)*90,0),Vector3.one);
    foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
    {
     var source=filter.sharedMesh;if(!source)continue;
     Matrix4x4 transform=tile*prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
     for(int sub=0;sub<source.subMeshCount;sub++)
      batches.Add(new CombineInstance{mesh=source,subMeshIndex=sub,transform=transform});
    }
   }
   var mesh=new Mesh{name="Łączona posadzka",indexFormat=IndexFormat.UInt32};
   mesh.CombineMeshes(batches.ToArray(),true,true);mesh.RecalculateBounds();
   // One atlas and material across the floor. The resource owns this material;
   // only the combined mesh belongs to the zone and is destroyed on departure.
   CryptMesh(root.transform,mesh,kit.darkFantasy,owner);
   // Each module includes closed backing, baked wear and inset metal/stone.
   // Legacy support quads, flat drains and large inlays would cover that art.
  }
  static void AddCryptQuad(List<Vector3> vertices,List<int> triangles,Vector3 center,float width,float depth)
  {
   int n=vertices.Count;float a=width/2,b=depth/2;
   vertices.Add(center+new Vector3(-a,0,-b));vertices.Add(center+new Vector3(-a,0,b));vertices.Add(center+new Vector3(a,0,b));vertices.Add(center+new Vector3(a,0,-b));
   triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
  }
  static Mesh CryptFlatMesh(string name,List<Vector3> vertices,List<int> triangles)
  {
   var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
  }
  static void CryptMesh(Transform parent,Mesh mesh,Material material,NocturnisCryptGeometry owner)
  {
   var obj=new GameObject(mesh.name);obj.transform.SetParent(parent,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
   var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;owner.meshes.Add(mesh);
  }
  void BuildCryptInlay(Transform parent,NocturnisCryptGeometry owner)
  {
   var vertices=new List<Vector3>();var triangles=new List<int>();
   var centers=Zone==Campaign.Heart ? new[]{World(12,25)} : new[]{World(5,5),World(8,20),World(16,34)};
   foreach(var center in centers)
   {
    float radius=Zone==Campaign.Heart ? 6.4f : 2.35f;
    for(int i=0;i<64;i++)
    {
     float a=i*Mathf.PI/32,b=(i+1)*Mathf.PI/32;
     Vector3 p=center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;
     Vector3 q=center+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*radius;
     if(!grid.Walkable(Cell(p.x),Cell(p.z)) || !grid.Walkable(Cell(q.x),Cell(q.z)))continue;
     int n=vertices.Count;Vector3 da=(p-center).normalized*.035f,db=(q-center).normalized*.035f;
     vertices.Add(p-da+Vector3.up*.008f);vertices.Add(p+da+Vector3.up*.008f);vertices.Add(q+db+Vector3.up*.008f);vertices.Add(q-db+Vector3.up*.008f);
     triangles.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});
    }
    // Four little square stone insets make the rooms legible without resembling a combat warning.
    for(int i=0;i<4;i++)
    {
     float a=i*Mathf.PI/2;Vector3 p=center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(radius-.36f);
     if(grid.Walkable(Cell(p.x),Cell(p.z)))AddCryptQuad(vertices,triangles,p+Vector3.up*.009f,.16f,.16f);
    }
   }
   var material=new Material(art.floorMaterial){name="Mosiężna intarsja"};material.color=Zone==Campaign.Kings ? new Color(.48f,.38f,.20f) : new Color(.36f,.32f,.43f);owner.materials.Add(material);
   CryptMesh(parent,CryptFlatMesh("Intarsje kamiennych sal",vertices,triangles),material,owner);
  }
 }
 public sealed class NocturnisCryptGeometry : MonoBehaviour
 {
  public bool usesOriginalFloor;
  // Slabs A/B/C, cracked, drain, rosette. Useful for runtime integration checks.
  public int[] moduleCounts=new int[6];
  public readonly List<Mesh> meshes=new List<Mesh>();
  public readonly List<Material> materials=new List<Material>();
  void OnDestroy(){foreach(var mesh in meshes)if(mesh)Destroy(mesh);foreach(var material in materials)if(material)Destroy(material);}
 }
}
