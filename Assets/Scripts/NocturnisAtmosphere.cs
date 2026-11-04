using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Krypta
{
 public sealed partial class KryptaGame
 {
  Mesh groundMesh;
  void BuildGroundBacking()
  {
   if(groundMesh)Destroy(groundMesh);
   if(Campaign.Outdoors(Zone)){BuildOrganicGround();return;}
   // A continuous, independently rendered bed prevents voids between imported tiles.
   var vertices=new List<Vector3>();var triangles=new List<int>();
   int margin=Campaign.Outdoors(Zone) ? 12 : 0;
   for(int x=-margin;x<DungeonGrid.Width+margin;x++)for(int z=-margin;z<DungeonGrid.Height+margin;z++)
   {
    if(!Campaign.Outdoors(Zone) && !grid.Walkable(x,z) && !grid.Border(x,z))continue;
    Vector3 p=World(x,z)-Vector3.up*(Campaign.Outdoors(Zone) ? .025f : .12f);int n=vertices.Count;float r=Tile*.5f;
    vertices.Add(p+new Vector3(-r,0,-r));vertices.Add(p+new Vector3(-r,0,r));
    vertices.Add(p+new Vector3(r,0,r));vertices.Add(p+new Vector3(r,0,-r));
    triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
   }
   groundMesh=new Mesh{name="Ciągłe podłoże lokacji"};groundMesh.SetVertices(vertices);groundMesh.SetTriangles(triangles,0);
   var uvs=new List<Vector2>();foreach(var v in vertices)uvs.Add(new Vector2(v.x*.24f,v.z*.24f));groundMesh.SetUVs(0,uvs);
   groundMesh.RecalculateNormals();groundMesh.RecalculateTangents();groundMesh.RecalculateBounds();
   var o=new GameObject("Podłoże bez szczelin");o.transform.SetParent(world,false);
   o.AddComponent<MeshFilter>().sharedMesh=groundMesh;o.AddComponent<MeshRenderer>().sharedMaterial=Campaign.Outdoors(Zone) ? art.earthMaterial : art.floorMaterial;
  }
  void CryptWall(int x,int z)
  {
   BuildCryptWallSegment(x,z);
  }
  void BuildCryptDetails()
  {
   BuildCryptDressing();
  }
  void BuildForestDetails()
  {
   // Ground clutter stays along the verges; the navigable route remains unobstructed.
   for(int x=1;x<DungeonGrid.Width-1;x++)for(int z=1;z<DungeonGrid.Height-1;z++)
    if(Zone!=Campaign.Forest && grid.Border(x,z) && (x*13+z)%4==0)Place(art.graves[(x+z)%art.graves.Length],World(x,z),.7f,false,world);
   foreach(Vector3 p in new[]{World(5,5),World(8,20),World(16,34)})
    AmbientMotes(p+Vector3.up*1.1f,Zone==Campaign.Forest ? new Color(.35f,1,.55f,.7f) : new Color(.65f,.6f,.75f,.35f),6,4);
  }
  void Flame(Vector3 p,float size)
  {
   var ps=Emitter("Płomień",world,p,new Color(1,.42f,.065f,.9f),true,15,.7f,size*3,.35f);
   var shape=ps.shape;shape.radius=size*.3f;
   var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.y=new ParticleSystem.MinMaxCurve(.7f);
   var color=ps.colorOverLifetime;color.enabled=true;
   Gradient g=new Gradient();g.SetKeys(new[]{new GradientColorKey(new Color(1,.9f,.4f),0),new GradientColorKey(new Color(1,.22f,.015f),1)},new[]{new GradientAlphaKey(.85f,0),new GradientAlphaKey(0,1)});color.color=g;
   var sizeLife=ps.sizeOverLifetime;sizeLife.enabled=true;sizeLife.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));ps.Play();
  }
  void AmbientMotes(Vector3 p,Color color,float radius,float rate)
  {
   var ps=Emitter("Pył i świetliki",world,p,color,true,rate,5,.16f,.07f);
   var shape=ps.shape;shape.radius=radius;ps.Play();
  }
  void PortalSeal(Transform parent)
  {
   var ring=Ring(parent,1.1f,.055f,art.tealMaterial);ring.name="Runy przejścia";
   for(int i=0;i<6;i++)
   {float a=i*Mathf.PI/3;var rune=Ring(ring.transform,.14f,.035f,art.goldMaterial);rune.transform.localPosition=new Vector3(Mathf.Cos(a)*.85f,.02f,Mathf.Sin(a)*.85f);}
   var ps=Emitter("Światło przejścia",parent,parent.position, new Color(.4f,.85f,1,.6f),true,8,1.8f,.25f,.3f);
   var shape=ps.shape;shape.radius=1;ps.Play();
  }
 }
 public sealed class NocturnisFlicker : MonoBehaviour
 {
  Light lamp;float intensity,phase;
  void Start(){lamp=GetComponent<Light>();intensity=lamp.intensity;phase=transform.position.x+transform.position.z;}
  void Update(){lamp.intensity=intensity*(.9f+.08f*Mathf.Sin(Time.time*7+phase)+.05f*Mathf.Sin(Time.time*13+phase));}
 }
}
