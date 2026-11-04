using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Krypta
{
 // A scene-only landscape. Navigation always remains in DungeonGrid; no collider
 // is created for these distant banks, rock recesses or silhouettes.
 public sealed class NocturnisEnvironmentLayout
 {
  public const int Margin=22,PropBudget=36;
  public const float CellSize=1.8f;
  public readonly int zone;
  readonly List<Vector2> walkable=new List<Vector2>();
  readonly Dictionary<Vector2Int,float> distances=new Dictionary<Vector2Int,float>();
  public NocturnisEnvironmentLayout(DungeonGrid grid,int zone)
  {
   this.zone=zone;
   for(int x=0;x<DungeonGrid.Width;x++)for(int z=0;z<DungeonGrid.Height;z++)if(grid.Walkable(x,z))walkable.Add(new Vector2(x,z));
  }
  public float Distance(float x,float z)
  {
   var key=new Vector2Int(Mathf.RoundToInt(x*4),Mathf.RoundToInt(z*4));float result;
   if(distances.TryGetValue(key,out result))return result;
   float best=float.MaxValue;
   foreach(var p in walkable){float dx=x-p.x,dz=z-p.y;best=Mathf.Min(best,dx*dx+dz*dz);}
   result=Mathf.Sqrt(best);distances[key]=result;return result;
  }
  public float Height(float x,float z)
  {
   float d=Distance(x,z),noise=Mathf.Clamp01(Mathf.PerlinNoise(x*.12f+zone*.73f+50,z*.14f+70));
   if(Campaign.Underground(zone))return -1.15f-noise*1.7f;
   if(zone==Campaign.Town)return -.30f-Mathf.SmoothStep(0,1,Mathf.Clamp01((d-.8f)/3))*(.2f+noise*.65f);
   // Footing stays level. Low verges frame actors, then give way to deeper hills.
   float bank=Mathf.SmoothStep(0,1,Mathf.Clamp01((d-.8f)/2.6f))*(.24f+noise*.48f);
   float distant=Mathf.SmoothStep(0,1,Mathf.Clamp01((d-5)/8));
   return -.025f+bank+distant*(-1.7f+noise*3.1f);
  }
  public Mesh Surface()
  {
   var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
   var ids=new Dictionary<Vector2Int,int>();
   int min=-Margin,maxX=DungeonGrid.Width+Margin,maxZ=DungeonGrid.Height+Margin;
   for(int x=min;x<maxX;x++)for(int z=min;z<maxZ;z++)
   {
    // The outer outline remains organic, but lies >30 m beyond every playable
    // edge, safely outside the normal isometric framing instead of behind a tree.
    float edge=Margin-2+1.4f*Mathf.Sin(x*.17f+zone)+.7f*Mathf.Cos(z*.23f);
    if(Distance(x+.5f,z+.5f)>edge)continue;
    int[] quad=new int[4];int[,] corners={{0,0},{0,1},{1,1},{1,0}};
    for(int i=0;i<4;i++)
    {
     var p=new Vector2Int(x+corners[i,0],z+corners[i,1]);int id;
     if(!ids.TryGetValue(p,out id)){id=v.Count;ids.Add(p,id);v.Add(new Vector3(p.x*CellSize,Height(p.x,p.y),p.y*CellSize));uv.Add(new Vector2(p.x*CellSize*.24f,p.y*CellSize*.24f));}
     quad[i]=id;
    }
    t.AddRange(new[]{quad[0],quad[1],quad[2],quad[0],quad[2],quad[3]});
   }
   var mesh=new Mesh{name="Distant landscape and continuous banks",indexFormat=IndexFormat.UInt32};
   mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
  }
 }

 // Ownership is explicit: every generated mesh/material dies with its zone.
 public sealed class NocturnisEnvironmentRoot : MonoBehaviour
 {
  public int zone,backdropProps;
  public readonly List<Mesh> meshes=new List<Mesh>();
  public readonly List<Material> materials=new List<Material>();
  void OnDestroy(){foreach(var m in meshes)if(m)Destroy(m);foreach(var m in materials)if(m)Destroy(m);}
 }
 public sealed class NocturnisBackdropProp : MonoBehaviour { }

 public sealed partial class KryptaGame
 {
  NocturnisEnvironmentLayout environmentLayout;
  public const int ActorReadabilityLayer=29;
  static readonly string[] EnvironmentColorProperties={"baseColorFactor","_BaseColor","_Color"};
  public static void ApplyActorReadability(GameObject root)
  {
   if(!root)return;
   foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
    if(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)renderer.gameObject.layer=ActorReadabilityLayer;
  }
  void CreateActorFillLight()
  {
   var light=new GameObject("Soft moon fill on actors").AddComponent<Light>();light.transform.SetParent(transform,false);
   light.type=LightType.Directional;light.color=new Color(.83f,.89f,1);light.intensity=.43f;
   light.cullingMask=1<<ActorReadabilityLayer;light.shadows=LightShadows.None;
   light.transform.rotation=Quaternion.Euler(28,150,0);
  }
  void ConfigureEnvironmentAtmosphere()
  {
   bool outdoors=Campaign.Outdoors(Zone),town=Zone==Campaign.Town;
   Color haze=Zone==Campaign.Ravine?new Color(.075f,.09f,.11f):Zone==Campaign.Cloister?new Color(.09f,.085f,.075f):
    Zone==Campaign.Forest?new Color(.045f,.065f,.057f):outdoors?new Color(.075f,.070f,.063f):town?new Color(.085f,.082f,.074f):new Color(.025f,.030f,.037f);
   View.backgroundColor=haze;RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=haze;
   RenderSettings.fogStartDistance=outdoors||town?32:28;RenderSettings.fogEndDistance=outdoors||town?88:64;
   RenderSettings.ambientLight=outdoors||town?new Color(.32f,.345f,.37f):new Color(.34f,.36f,.39f);
  }
  void BuildEnvironmentDressing()
  {
   var root=new GameObject("Environment depth — "+Campaign.Names[Zone]);root.transform.SetParent(world,false);
   var owner=root.AddComponent<NocturnisEnvironmentRoot>();owner.zone=Zone;
   if(!Campaign.Outdoors(Zone))
   {
    var mesh=environmentLayout.Surface();owner.meshes.Add(mesh);
    var source=Zone==Campaign.Town?art.earthMaterial:art.wallMaterial;
    var mat=new Material(source){name=Zone==Campaign.Town?"Earth below Oakhaven masonry":"Recessed crypt bedrock"};
    TintTerrainMaterial(mat,Zone==Campaign.Town?new Color(.78f,.76f,.70f):new Color(.34f,.39f,.43f));owner.materials.Add(mat);
    EnvironmentMesh(root.transform,mesh,mat);
    BuildEnvironmentFoundation(owner);
   }
   BuildEnvironmentSilhouettes(owner);
  }
  static void EnvironmentMesh(Transform parent,Mesh mesh,Material mat)
  {
   var go=new GameObject(mesh.name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
   var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;
  }
  void BuildEnvironmentFoundation(NocturnisEnvironmentRoot owner)
  {
   var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
   int[] dx={-1,1,0,0},dz={0,0,-1,1};
   for(int x=0;x<DungeonGrid.Width;x++)for(int z=0;z<DungeonGrid.Height;z++)
   {
    if(!grid.Walkable(x,z)&&!grid.Border(x,z))continue;
    for(int side=0;side<4;side++)
    {
     int nx=x+dx[side],nz=z+dz[side];if(grid.Walkable(nx,nz)||grid.Border(nx,nz))continue;
     Vector3 center=World(x,z)+new Vector3(dx[side],0,dz[side])*Tile*.5f;
     Vector3 across=new Vector3(-dz[side],0,dx[side])*Tile*.5f;
     Vector3 a=center-across,b=center+across;float ya=environmentLayout.Height(a.x/Tile,a.z/Tile),yb=environmentLayout.Height(b.x/Tile,b.z/Tile);
     int n=vertices.Count;vertices.Add(a+Vector3.down*.13f);vertices.Add(b+Vector3.down*.13f);vertices.Add(b+Vector3.up*(yb-.03f));vertices.Add(a+Vector3.up*(ya-.03f));
     uv.AddRange(new[]{new Vector2(0,1),new Vector2(1,1),new Vector2(1,0),new Vector2(0,0)});
     // Winding points away from the supported floor towards the recessed rock.
     triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
    }
   }
   var mesh=CryptFlatMesh("Cutaway masonry foundations",vertices,triangles);mesh.SetUVs(0,uv);mesh.RecalculateTangents();owner.meshes.Add(mesh);
   var mat=new Material(art.wallMaterial){name="Shadowed stone foundations"};TintTerrainMaterial(mat,new Color(.51f,.53f,.52f));owner.materials.Add(mat);
   EnvironmentMesh(owner.transform,mesh,mat);
  }
  void BuildEnvironmentSilhouettes(NocturnisEnvironmentRoot owner)
  {
   var lib=NocturnisActILibrary.Current;bool underground=Campaign.Underground(Zone);
   // A deterministic, sparse ring. Near-camera banks stay low; distant objects
   // are not animated and reuse the existing library's materials and textures.
   var candidates=new List<Vector2Int>();
   for(int z=-6;z<DungeonGrid.Height+7;z+=3)for(int x=-6;x<DungeonGrid.Width+7;x+=3)
    if(environmentLayout.Distance(x,z)>=2.5f && environmentLayout.Distance(x,z)<=7.2f)candidates.Add(new Vector2Int(x,z));
   candidates.Sort((a,b)=>CryptDressingHash(a.x+80,a.y+90).CompareTo(CryptDressingHash(b.x+80,b.y+90)));
   foreach(var cell in candidates)
   {
    if(owner.backdropProps>=NocturnisEnvironmentLayout.PropBudget)break;
    int x=cell.x,z=cell.y;float distance=environmentLayout.Distance(x,z);
    int hash=CryptDressingHash(x+30,z+30);if(hash%3==0)continue;
    string id=underground?"rock_boulder":Zone==Campaign.Ravine?"cliff_face":Zone==Campaign.Cloister?"crypt_pillar":
     Zone==Campaign.Forest?(hash%5==0?"tree_corrupted":"tree_dead_oak"):Zone==Campaign.Cemetery?"rock_boulder":hash%3==1?"tree_dead_oak":"rock_boulder";
    var prefab=lib?lib.Model(id):null;if(!prefab && art.rocks.Length>0)prefab=art.rocks[hash%art.rocks.Length];if(!prefab)continue;
    float footprint=underground?1.8f:distance<3.5f?2.1f:3.0f;
    Vector3 pos=World(x,z);pos.y=environmentLayout.Height(x,z)-.08f;
    var prop=Place(prefab,pos,footprint,false,owner.transform);if(!prop)continue;
    prop.name="Backdrop • "+id;prop.transform.rotation=Quaternion.Euler(0,hash%360,0);
    Bounds bounds;if(ModelBounds.TryGet(prop,out bounds))
    {
     float limit=underground?1.05f:distance<3.5f?2.6f:Zone==Campaign.Ravine?5.4f:4.6f;
     float scale=Mathf.Min(1,footprint/Mathf.Max(.01f,Mathf.Max(bounds.size.x,bounds.size.z)),limit/Mathf.Max(.01f,bounds.size.y));prop.transform.localScale*=scale;
     if(ModelBounds.TryGet(prop,out bounds))prop.transform.position+=Vector3.up*(pos.y-bounds.min.y);
    }
    foreach(var c in prop.GetComponentsInChildren<Collider>()){c.enabled=false;Destroy(c);}
    prop.AddComponent<NocturnisBackdropProp>();owner.backdropProps++;
    if(id.StartsWith("tree",StringComparison.Ordinal))PolishForestColors(prop);
   }
  }
  static void PolishForestColors(GameObject tree)
  {
   foreach(var renderer in tree.GetComponentsInChildren<Renderer>())
   {
    var materials=renderer.sharedMaterials;
    for(int i=0;i<materials.Length;i++)
    {
     var material=materials[i];if(!material)continue;var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,i);
     foreach(string property in EnvironmentColorProperties)if(material.HasProperty(property))
      block.SetColor(property,material.GetColor(property)*new Color(.76f,.79f,.69f,1));
     renderer.SetPropertyBlock(block,i);
    }
   }
  }
 }
}
