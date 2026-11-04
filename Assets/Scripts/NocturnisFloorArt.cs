using System.Collections.Generic;
using UnityEngine;

namespace Krypta
{
 public sealed partial class KryptaGame
 {
  void BuildArtisanFloor(Transform parent,NocturnisCryptGeometry owner)
  {
   if(owner.usesOriginalFloor)return;
   BuildFloorWear(parent,owner);
   if(Zone==Campaign.Kings || Zone==Campaign.Heart)BuildStoneRosettes(parent,owner);
   else BuildIronDrains(parent,owner);
  }
  void BuildFloorWear(Transform parent,NocturnisCryptGeometry owner)
  {
   var texture=Resources.Load<Texture2D>("Artisan/FloorWear");
   var shader=Resources.Load<Shader>("Shaders/FloorWear");
   if(!texture || !shader)return;
   var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
   var selected=new List<Vector3>();
   for(int x=1;x<DungeonGrid.Width-1;x++)for(int z=1;z<DungeonGrid.Height-1;z++)
   {
    if(!grid.Walkable(x,z))continue;
    int hash=CryptDressingHash(x,z);if(hash%11!=0)continue;
    bool supported=true,nearEdge=false;
    for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)
     if(!grid.Walkable(x+dx,z+dz) && !grid.Border(x+dx,z+dz))supported=false;
    for(int dx=-2;dx<=2;dx++)for(int dz=-2;dz<=2;dz++)if(grid.Border(x+dx,z+dz))nearEdge=true;
    if(!supported || !nearEdge)continue;
    Vector3 p=World(x,z);bool crowded=false;
    foreach(var other in selected)if(Vector3.Distance(other,p)<4.5f){crowded=true;break;}
    if(crowded)continue;
    selected.Add(p);
    float width=2.7f+(hash%5)*.18f,depth=2.1f+(hash%3)*.22f;
    Quaternion angle=Quaternion.Euler(0,hash%4*90,0);int n=vertices.Count;
    foreach(var local in new[]{new Vector3(-width/2,0,-depth/2),new Vector3(-width/2,0,depth/2),new Vector3(width/2,0,depth/2),new Vector3(width/2,0,-depth/2)})vertices.Add(p+angle*local+Vector3.up*.004f);
    uv.AddRange(new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)});
    triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
   }
   var mesh=CryptFlatMesh("Ślady zużycia",vertices,triangles);mesh.SetUVs(0,uv);
   var material=new Material(shader){name="Ręcznie malowany pył i mech",mainTexture=texture};
   material.SetColor("_Color",Zone==Campaign.Kings ? new Color(.86f,.82f,.72f,.24f) : Zone==Campaign.Cellar ? new Color(1,.91f,.74f,.48f) : new Color(.83f,.91f,.85f,.40f));
   owner.materials.Add(material);CryptMesh(parent,mesh,material,owner);
  }
  Material ArtisanFloorMaterial(NocturnisCryptGeometry owner,string name,Color color)
  {
   var material=new Material(art.floorMaterial){name=name,color=color};owner.materials.Add(material);return material;
  }
  void BuildIronDrains(Transform parent,NocturnisCryptGeometry owner)
  {
   var bed=new List<Vector3>();var bedTriangles=new List<int>();
   var iron=new List<Vector3>();var ironTriangles=new List<int>();
   var rim=new List<Vector3>();var rimTriangles=new List<int>();
   foreach(var cell in CryptDrainCells())
   {
    Vector3 p=World(cell.x,cell.y);
    AddCryptQuad(bed,bedTriangles,p+Vector3.up*.006f,1.10f,1.10f);
    for(int side=-1;side<=1;side+=2)
    {
     AddCryptQuad(rim,rimTriangles,p+new Vector3(side*.54f,.018f,0),.10f,1.18f);
     AddCryptQuad(rim,rimTriangles,p+new Vector3(0,.018f,side*.54f),1.0f,.10f);
    }
    for(int i=-3;i<=3;i++)AddCryptQuad(iron,ironTriangles,p+new Vector3(i*.14f,.025f,0),.045f,1.0f);
    for(int side=-1;side<=1;side+=2)AddCryptQuad(iron,ironTriangles,p+new Vector3(0,.027f,side*.27f),1.0f,.035f);
   }
   CryptMesh(parent,CryptFlatMesh("Odpływy — zagłębienia",bed,bedTriangles),ArtisanFloorMaterial(owner,"Cień odpływu",new Color(.065f,.09f,.10f)),owner);
   CryptMesh(parent,CryptFlatMesh("Odpływy — żeliwne kraty",iron,ironTriangles),ArtisanFloorMaterial(owner,"Żeliwo",new Color(.34f,.39f,.41f)),owner);
   CryptMesh(parent,CryptFlatMesh("Odpływy — kamienne ramy",rim,rimTriangles),ArtisanFloorMaterial(owner,"Rama odpływu",new Color(.39f,.43f,.44f)),owner);
  }
  List<Vector2Int> CryptDrainCells()
  {
   var used=new List<Vector2Int>();
   for(int z=5;z<DungeonGrid.Height-3;z++)for(int x=2;x<DungeonGrid.Width-2;x++)
   {
    if(!grid.Walkable(x,z) || (!grid.Border(x,z+1) && !grid.Border(x-1,z)))continue;
    if(CryptDressingHash(x,z)%5!=0)continue;
    Vector3 p=World(x,z);
    if(Vector3.Distance(p,SpawnPosition)<4 || Vector3.Distance(p,ExitPosition)<4)continue;
    bool nearby=false;foreach(var other in used)if(Vector3.Distance(p,World(other.x,other.y))<14){nearby=true;break;}
    if(nearby || used.Count>=5)continue;
    used.Add(new Vector2Int(x,z));
   }
   return used;
  }
  List<Vector2Int> CryptRosetteCells()
  {
   return Zone==Campaign.Heart ? new List<Vector2Int>{new Vector2Int(12,25)} : new List<Vector2Int>{new Vector2Int(5,5),new Vector2Int(8,20),new Vector2Int(16,34)};
  }
  void BuildStoneRosettes(Transform parent,NocturnisCryptGeometry owner)
  {
   var face=new List<Vector3>();var triangles=new List<int>();
   var edge=new List<Vector3>();var edgeTriangles=new List<int>();
   foreach(var cell in CryptRosetteCells())
   {
    Vector3 center=World(cell.x,cell.y);
    // Original sixteen-vertex compass rose, inset flush into the navigable stone.
    for(int i=0;i<16;i++)
    {
     float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;
     Vector3 p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(i%2==0 ? 1.55f : .60f);
     Vector3 q=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*(i%2==0 ? .60f : 1.55f);
     int n=face.Count;face.Add(center+Vector3.up*.006f);face.Add(center+q+Vector3.up*.006f);face.Add(center+p+Vector3.up*.006f);triangles.AddRange(new[]{n,n+1,n+2});
     int k=edge.Count;
     edge.Add(center+p+Vector3.up*.010f);edge.Add(center+q+Vector3.up*.010f);
     edge.Add(center+q*.91f+Vector3.up*.010f);edge.Add(center+p*.91f+Vector3.up*.010f);
     edgeTriangles.AddRange(new[]{k,k+2,k+1,k,k+3,k+2});
    }
    AddCryptQuad(face,triangles,center+Vector3.up*.012f,.30f,.30f);
   }
   CryptMesh(parent,CryptFlatMesh("Kamienne rozety",face,triangles),ArtisanFloorMaterial(owner,"Bazaltowe rozety",Zone==Campaign.Heart ? new Color(.28f,.27f,.35f) : new Color(.32f,.36f,.38f)),owner);
   CryptMesh(parent,CryptFlatMesh("Mosiężne krawędzie rozet",edge,edgeTriangles),ArtisanFloorMaterial(owner,"Wytarty mosiądz",new Color(.57f,.47f,.29f)),owner);
  }
 }
}
