using System.Collections.Generic;
using UnityEngine;
namespace Krypta {
 // One palette belongs to one zone instance; rebuilding a zone disposes every
 // generated texture, material and mesh instead of growing a static cache.
 public sealed class NocturnisRoadSurfaceOwner : MonoBehaviour {
  public readonly List<Object> owned=new List<Object>();
  public Material wood,woodPale,iron,cloth,brass;
  void OnDestroy(){foreach(var item in owned)if(item)Destroy(item);owned.Clear();}
 }
 public sealed partial class KryptaGame {
  NocturnisRoadSurfaceOwner RoadPalette() {
   var p=world.GetComponent<NocturnisRoadSurfaceOwner>();if(p)return p;
   p=world.gameObject.AddComponent<NocturnisRoadSurfaceOwner>();
   var wood=RoadTexture(p,0);var iron=RoadTexture(p,1);var cloth=RoadTexture(p,2);
   p.wood=RoadMaterial(p,"Worn chestnut timber",new Color(.47f,.265f,.115f),wood,0,.24f);
   p.woodPale=RoadMaterial(p,"Split timber edge",new Color(.63f,.42f,.21f),wood,0,.20f);
   p.iron=RoadMaterial(p,"Forged iron and rust",new Color(.20f,.225f,.24f),iron,.72f,.29f);
   p.cloth=RoadMaterial(p,"Weathered oxblood canvas",new Color(.39f,.065f,.045f),cloth,0,.09f);
   p.brass=RoadMaterial(p,"Tarnished brass fittings",new Color(.43f,.275f,.09f),iron,.7f,.33f);return p;
  }
  static Texture2D RoadTexture(NocturnisRoadSurfaceOwner owner,int kind) {
   const int size=128;var t=new Texture2D(size,size,TextureFormat.RGB24,true);t.name=kind==0?"Timber grain":kind==1?"Forged metal pitting":"Woven canvas";t.wrapMode=TextureWrapMode.Repeat;t.anisoLevel=4;
   var pixels=new Color[size*size];
   for(int y=0;y<size;y++)for(int x=0;x<size;x++) {
    float n=Mathf.PerlinNoise(x*.19f+kind*7,y*.19f+11);
    float grain=.75f+.13f*Mathf.Sin(x*.83f+Mathf.Sin(y*.09f)*1.7f)+.12f*n;
    if(kind==0){float crack=Mathf.Pow(Mathf.Max(0,Mathf.Sin(x*2.37f+y*.013f)),22)*.30f;grain-=crack;}
    else if(kind==1)grain=.53f+.40f*n+.09f*Mathf.Sin(x*2.2f+y*3.4f);
    else grain=.68f+.12f*n+.08f*(x%2==0?1:-1)+.07f*(y%2==0?1:-1);
    pixels[y*size+x]=new Color(grain,grain,grain);
   }
   t.SetPixels(pixels);t.Apply(true,true);owner.owned.Add(t);return t;
  }
  static Material RoadMaterial(NocturnisRoadSurfaceOwner owner,string name,Color color,Texture texture,float metal,float smooth) {
   var m=new Material(Shader.Find("Standard")){name=name,color=color,mainTexture=texture};m.SetFloat("_Metallic",metal);m.SetFloat("_Glossiness",smooth);owner.owned.Add(m);return m;
  }
  GameObject RoadBox(Transform parent,string name,Vector3 position,Vector3 size,Material material,float yaw=0) {
   var piece=CubeOn(parent,name,position,size,material);piece.transform.localRotation=Quaternion.Euler(0,yaw,0);return piece;
  }
  void RoadWheelRing(Transform parent,Vector3 center,float inner,float outer,float depth,Material material,NocturnisRoadSurfaceOwner owner,string name) {
   const int segments=32;var v=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
   for(int i=0;i<segments;i++)for(int side=0;side<4;side++) {
    float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
    float r0=side==0||side==3?inner:outer,r1=side==0||side==1?outer:inner;
    float x0=side<2?-depth:depth,x1=side==0||side==3?-depth:depth;int k=v.Count;
    v.Add(center+new Vector3(x0,Mathf.Cos(a)*r0,Mathf.Sin(a)*r0));v.Add(center+new Vector3(x0,Mathf.Cos(b)*r0,Mathf.Sin(b)*r0));
    v.Add(center+new Vector3(x1,Mathf.Cos(b)*r1,Mathf.Sin(b)*r1));v.Add(center+new Vector3(x1,Mathf.Cos(a)*r1,Mathf.Sin(a)*r1));
    uv.Add(new Vector2(0,i*.2f));uv.Add(new Vector2(0,(i+1)*.2f));uv.Add(new Vector2(1,(i+1)*.2f));uv.Add(new Vector2(1,i*.2f));
    triangles.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
   }
   var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();owner.owned.Add(mesh);
   var o=new GameObject(name);o.transform.SetParent(parent,false);o.AddComponent<MeshFilter>().sharedMesh=mesh;o.AddComponent<MeshRenderer>().sharedMaterial=material;
  }
  void BuildRoadWagon() {
   var p=RoadPalette();var wagon=new GameObject("Rozbity opancerzony wóz");wagon.transform.SetParent(world);wagon.transform.position=World(9,5);wagon.transform.rotation=Quaternion.Euler(0,22,7);
   for(int side=-1;side<=1;side+=2)RoadBox(wagon.transform,"Dębowa rama",new Vector3(side*1.05f,.60f,0),new Vector3(.20f,.25f,3.55f),p.wood);
   for(int board=0;board<8;board++)RoadBox(wagon.transform,"Deska podłogi",new Vector3(-1.2f+board*.34f,.78f,0),new Vector3(.32f,.12f,3.4f),board%3==0?p.woodPale:p.wood);
   for(int side=-1;side<=1;side+=2) {
    for(int layer=0;layer<3;layer++)RoadBox(wagon.transform,"Deska burty",new Vector3(side*1.36f,.98f+layer*.23f,layer==2&&side==1?-.35f:0),new Vector3(.13f,.20f,layer==2&&side==1?2.5f:3.4f),layer==2?p.woodPale:p.wood);
    foreach(float z in new[]{-1.3f,0,1.3f}) {
     RoadBox(wagon.transform,"Żelazne okucie",new Vector3(side*1.44f,1.12f,z),new Vector3(.05f,.91f,.12f),p.iron);
     foreach(float y in new[]{.91f,1.16f,1.42f})RoadBox(wagon.transform,"Nit",new Vector3(side*1.48f,y,z),Vector3.one*.075f,p.brass);
    }
   }
   for(int i=0;i<4;i++) {
    Vector3 center=new Vector3(i%2==0?-1.60f:1.60f,.50f,i<2?-1.13f:1.13f);
    RoadWheelRing(wagon.transform,center,.43f,.56f,.095f,p.wood,p,"Drewniana obręcz koła");RoadWheelRing(wagon.transform,center,.55f,.60f,.105f,p.iron,p,"Żelazna obręcz koła");
    for(int spoke=0;spoke<8;spoke++){float a=spoke*Mathf.PI/4;var s=RoadBox(wagon.transform,"Szprycha",center+new Vector3(0,Mathf.Cos(a)*.25f,Mathf.Sin(a)*.25f),new Vector3(.12f,.51f,.075f),p.woodPale);s.transform.localRotation=Quaternion.Euler(a*Mathf.Rad2Deg,0,0);}
    RoadBox(wagon.transform,"Piasta",center,new Vector3(.30f,.21f,.21f),p.iron);
   }
   RoadBox(wagon.transform,"Złamany dyszel",new Vector3(.3f,.52f,2.24f),new Vector3(.20f,.20f,1.50f),p.woodPale,13);
   RoadBox(wagon.transform,"Skrzynia ładunku",new Vector3(-.55f,1.02f,-.7f),new Vector3(.9f,.4f,.7f),p.wood);
   RoadBox(wagon.transform,"Pas skrzyni",new Vector3(-.55f,1.24f,-.7f),new Vector3(.14f,.05f,.72f),p.iron);
   // A draped, uneven canvas makes the broken silhouette readable from above.
   var verts=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
   for(int z=0;z<=6;z++)for(int x=0;x<=12;x++) {
    float u=x/12f,v=z/6f;float height=1.18f+.35f*Mathf.Sin(u*Mathf.PI)+.055f*Mathf.Sin(u*27+v*7);
    verts.Add(new Vector3(Mathf.Lerp(-1.36f,1.38f,u),height,Mathf.Lerp(-.25f,1.42f,v)+(z==6?.10f*Mathf.Sin(x*1.7f):0)));uv.Add(new Vector2(u*3,v*2));
    if(x<12&&z<6){int k=z*13+x;tri.AddRange(new[]{k,k+13,k+1,k+1,k+13,k+14});}
   }
   var cloth=new Mesh{name="Podarta płachta transportowa"};cloth.SetVertices(verts);cloth.SetUVs(0,uv);cloth.SetTriangles(tri,0);cloth.RecalculateNormals();cloth.RecalculateTangents();cloth.RecalculateBounds();p.owned.Add(cloth);
   var cover=new GameObject(cloth.name);cover.transform.SetParent(wagon.transform,false);cover.AddComponent<MeshFilter>().sharedMesh=cloth;cover.AddComponent<MeshRenderer>().sharedMaterial=p.cloth;
  }
  void DressRoadBridge() {
   if(!drawbridge)return;var p=RoadPalette();drawbridge.GetComponent<Renderer>().sharedMaterial=p.wood;
   // drawbridge is scaled already; child transforms are in its normalized frame.
   for(int i=0;i<9;i++)RoadBox(drawbridge.transform,"Deska pomostu",new Vector3(0,.56f,-.445f+i*.111f),new Vector3(.98f,.25f,.103f),i%4==0?p.woodPale:p.wood);
   foreach(float x in new[]{-.37f,.37f})RoadBox(drawbridge.transform,"Okucie mostu",new Vector3(x,.72f,0),new Vector3(.028f,.18f,1),p.iron);
  }
 }
}
