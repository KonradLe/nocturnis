using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Krypta.Editor
{
 public static class NocturnisArtisanSetup
 {
  const string Root="Assets/Generated/Artisan20261024";
  const string Resource="Assets/Resources/ArtisanKit.asset";
  const int Slate=0,Edge=1,Sand=2,Teal=3,Recess=4,Brass=5;

  [MenuItem("Nocturnis/Prepare artisan models")]
  public static void Prepare()
  {
   Folder(Root+"/Meshes");Folder(Root+"/Materials");Folder(Root+"/Prefabs");Folder("Assets/Resources");
   Material[] materials={
    Matte("Slate",new Color(.36f,.43f,.47f)),Matte("StoneEdge",new Color(.53f,.58f,.56f)),
    Matte("WarmCeramic",new Color(.65f,.47f,.29f)),Matte("TealCeramic",new Color(.18f,.43f,.43f)),
    Matte("DeepRecess",new Color(.10f,.16f,.19f)),Matte("OldBrass",new Color(.66f,.52f,.27f))};
   GameObject shrine=SaveModel("RelicShrine",RelicShrine(),materials);
   GameObject pillar=SaveModel("BrokenPillar",BrokenPillar(),materials);
   GameObject niche=SaveModel("UrnNiche",UrnNiche(),materials);
   var kit=AssetDatabase.LoadAssetAtPath<NocturnisArtisanKit>(Resource);
   if(!kit)
   {
    if(AssetDatabase.LoadMainAssetAtPath(Resource))throw new InvalidOperationException("Unrelated asset at "+Resource);
    kit=ScriptableObject.CreateInstance<NocturnisArtisanKit>();kit.name="ArtisanKit";AssetDatabase.CreateAsset(kit,Resource);
   }
   kit.relicShrine=shrine;kit.brokenPillar=pillar;kit.urnNiche=niche;
   EditorUtility.SetDirty(kit);AssetDatabase.SaveAssetIfDirty(kit);
   Debug.Log("Artisan kit ready: three original beveled meshes, six matte materials; existing GUIDs preserved.");
  }
  static Material Matte(string name,Color color)
  {
   string path=Root+"/Materials/"+name+".mat";
   var material=AssetDatabase.LoadAssetAtPath<Material>(path);
   Shader shader=Shader.Find("Standard");if(!shader)throw new InvalidOperationException("Standard shader unavailable");
   if(!material)
   {
    if(AssetDatabase.LoadMainAssetAtPath(path))throw new InvalidOperationException("Unrelated asset at "+path);
    material=new Material(shader);AssetDatabase.CreateAsset(material,path);
   }
   material.name="Nocturnis Artisan "+name;material.shader=shader;material.color=color;
   material.SetFloat("_Metallic",0);material.SetFloat("_Glossiness",.12f);
   material.enableInstancing=true;EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);return material;
  }
  static GameObject SaveModel(string name,MeshData geometry,Material[] materials)
  {
   string path=Root+"/Meshes/"+name+".asset";Mesh fresh=geometry.Build(name);
   var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
   if(mesh){EditorUtility.CopySerialized(fresh,mesh);UnityEngine.Object.DestroyImmediate(fresh);EditorUtility.SetDirty(mesh);}
   else
   {
    if(AssetDatabase.LoadMainAssetAtPath(path)){UnityEngine.Object.DestroyImmediate(fresh);throw new InvalidOperationException("Unrelated asset at "+path);}
    mesh=fresh;AssetDatabase.CreateAsset(mesh,path);
   }
   AssetDatabase.SaveAssetIfDirty(mesh);
   var model=new GameObject("Artisan_"+name);
   try
   {
    model.AddComponent<MeshFilter>().sharedMesh=mesh;
    var renderer=model.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials;
    renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
    return PrefabUtility.SaveAsPrefabAsset(model,Root+"/Prefabs/"+name+".prefab");
   }
   finally{UnityEngine.Object.DestroyImmediate(model);}
  }
  static void Folder(string path)
  {
   if(AssetDatabase.IsValidFolder(path))return;
   int slash=path.LastIndexOf('/');string parent=path.Substring(0,slash);Folder(parent);
   AssetDatabase.CreateFolder(parent,path.Substring(slash+1));
  }
  static MeshData RelicShrine()
  {
   var m=new MeshData();
   m.BevelBox(new Vector3(0,.08f,0),new Vector3(1.24f,.16f,.78f),.045f,Edge);
   m.BevelBox(new Vector3(0,.62f,-.025f),new Vector3(.97f,.96f,.58f),.07f,Slate);
   m.BevelBox(new Vector3(0,.62f,.285f),new Vector3(.78f,.75f,.075f),.055f,Recess);
   m.BevelBox(new Vector3(0,.64f,.335f),new Vector3(.64f,.60f,.04f),.045f,Teal);
   m.Extrude(new[]{new Vector2(-.60f,1.06f),new Vector2(.60f,1.06f),new Vector2(.60f,1.19f),new Vector2(0,1.50f),new Vector2(-.60f,1.19f)},-.39f,.39f,Edge);
   m.Extrude(new[]{new Vector2(-.43f,1.15f),new Vector2(.43f,1.15f),new Vector2(0,1.38f)},.391f,.411f,Slate);
   m.BevelBox(new Vector3(0,1.075f,.03f),new Vector3(1.09f,.10f,.71f),.025f,Sand);
   var star=new Vector2[16];
   for(int i=0;i<star.Length;i++)
   {
    float a=Mathf.PI*.5f+i*Mathf.PI/8;float r=i%2==0 ? .235f : .095f;
    star[i]=new Vector2(Mathf.Cos(a)*r,.68f+Mathf.Sin(a)*r);
   }
   m.Extrude(star,.36f,.395f,Brass);
   m.BevelBox(new Vector3(0,.68f,.405f),new Vector3(.075f,.075f,.035f),.012f,Edge);
   m.BevelBox(new Vector3(0,.32f,.365f),new Vector3(.035f,.09f,.025f),.008f,Brass);
   // Two inset seals and a broad plinth read at the game's distant camera scale.
   for(int side=-1;side<=1;side+=2)
    m.BevelBox(new Vector3(side*.43f,.63f,.30f),new Vector3(.075f,.55f,.055f),.018f,Sand);
   return m;
  }
  static MeshData BrokenPillar()
  {
   var m=new MeshData();
   m.BevelBox(new Vector3(0,.10f,0),new Vector3(1.05f,.20f,1.05f),.07f,Edge);
   m.Lathe(Vector3.zero,new[]{new Vector2(.40f,.19f),new Vector2(.43f,.24f),new Vector2(.43f,.29f),new Vector2(.34f,.34f)},8,Slate);
   float[] top={1.14f,1.26f,1.10f,1.40f,1.31f,1.46f,1.22f,1.18f};
   var bottom=new Vector3[8];var shoulder=new Vector3[8];var broken=new Vector3[8];
   for(int i=0;i<8;i++)
   {
    float a=(22.5f+i*45)*Mathf.Deg2Rad;Vector3 v=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
    bottom[i]=v*.335f+Vector3.up*.33f;shoulder[i]=v*.305f+Vector3.up*.40f;broken[i]=v*.305f+Vector3.up*top[i];
   }
   for(int i=0;i<8;i++)
   {
    int j=(i+1)%8;Vector3 outwards=(bottom[i]+bottom[j])*.5f;outwards.y=0;
    m.Quad(bottom[i],bottom[j],shoulder[j],shoulder[i],Edge,outwards);
    m.Quad(shoulder[i],shoulder[j],broken[j],broken[i],Slate,outwards);
    m.Triangle(new Vector3(.025f,1.13f,-.025f),broken[i],broken[j],i%3==0 ? Sand : Edge,Vector3.up);
   }
   // A separate narrow seam follows the flat front facet, with a short branch.
   m.Ribbon(new[]{new Vector2(-.055f,.40f),new Vector2(.015f,.64f),new Vector2(-.04f,.85f),new Vector2(.055f,1.13f)},.2835f,.025f,Recess);
   m.Ribbon(new[]{new Vector2(.015f,.64f),new Vector2(.09f,.73f),new Vector2(.115f,.82f)},.284f,.018f,Recess);
   m.BevelBox(new Vector3(-.055f,.55f,.289f),new Vector3(.10f,.035f,.012f),.004f,Teal);
   m.Extrude(new[]{new Vector2(.28f,.20f),new Vector2(.49f,.20f),new Vector2(.43f,.37f),new Vector2(.32f,.31f)},.16f,.35f,Edge);
   return m;
  }
  static MeshData UrnNiche()
  {
   var m=new MeshData();
   m.BevelBox(new Vector3(0,.07f,0),new Vector3(1.30f,.14f,.68f),.045f,Edge);
   Vector2[] outer=Arch(1.24f,.13f,.98f,.51f),inner=Arch(.94f,.25f,.96f,.38f);
   m.Extrude(outer,-.27f,-.18f,Slate);
   m.Extrude(inner,-.179f,-.168f,Recess);
   m.Frame(outer,inner,-.18f,.12f,Edge,Slate);
   m.BevelBox(new Vector3(0,1.39f,.04f),new Vector3(.20f,.26f,.38f),.035f,Sand);
   m.BevelBox(new Vector3(0,.205f,.02f),new Vector3(1.04f,.10f,.56f),.025f,Sand);
   Urn(m,new Vector3(-.245f,.255f,.075f),1,Sand);
   Urn(m,new Vector3(.245f,.255f,.095f),.79f,Teal);
   return m;
  }
  static Vector2[] Arch(float width,float bottom,float spring,float rise)
  {
   var p=new List<Vector2>{new Vector2(-width*.5f,bottom),new Vector2(width*.5f,bottom)};
   for(int i=0;i<=8;i++){float a=i*Mathf.PI/8;p.Add(new Vector2(Mathf.Cos(a)*width*.5f,spring+Mathf.Sin(a)*rise));}
   return p.ToArray();
  }
  static void Urn(MeshData m,Vector3 position,float size,int material)
  {
   Vector2[] profile={new Vector2(.10f,0),new Vector2(.135f,.035f),new Vector2(.12f,.08f),new Vector2(.185f,.17f),new Vector2(.195f,.32f),new Vector2(.145f,.43f),new Vector2(.10f,.48f),new Vector2(.14f,.51f),new Vector2(.14f,.56f),new Vector2(.102f,.57f)};
   for(int i=0;i<profile.Length;i++)profile[i]*=size;
   m.Lathe(position,profile,10,material,false);
   m.Lathe(position,new[]{new Vector2(.142f*size,.513f*size),new Vector2(.142f*size,.54f*size)},10,Brass,false);
   m.Disc(position+Vector3.up*(.553f*size),.10f*size,10,Recess,Vector3.up);
  }
  sealed class MeshData
  {
   readonly List<Vector3> positions=new List<Vector3>();
   readonly List<Vector2> uv=new List<Vector2>();
   readonly List<int>[] triangles=new List<int>[6];
   public MeshData(){for(int i=0;i<triangles.Length;i++)triangles[i]=new List<int>();}
   public Mesh Build(string name)
   {
    var mesh=new Mesh{name="Nocturnis Artisan "+name,indexFormat=IndexFormat.UInt32};
    mesh.SetVertices(positions);mesh.SetUVs(0,uv);mesh.subMeshCount=triangles.Length;
    for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i);
    mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
    if(mesh.vertexCount<30 || mesh.bounds.size.sqrMagnitude<.01f)throw new InvalidOperationException("Empty artisan geometry: "+name);
    return mesh;
   }
   public void Triangle(Vector3 a,Vector3 b,Vector3 c,int material,Vector3 outward)
   {
    if(Vector3.Dot(Vector3.Cross(b-a,c-a),outward)<0){Vector3 swap=b;b=c;c=swap;}
    int n=positions.Count;positions.Add(a);positions.Add(b);positions.Add(c);
    uv.Add(Vector2.zero);uv.Add(Vector2.right);uv.Add(new Vector2(.5f,1));
    triangles[material].AddRange(new[]{n,n+1,n+2});
   }
   public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material,Vector3 outward)
   {
    if(Vector3.Dot(Vector3.Cross(b-a,c-a),outward)<0){Vector3 swap=b;b=d;d=swap;}
    int n=positions.Count;positions.Add(a);positions.Add(b);positions.Add(c);positions.Add(d);
    uv.Add(Vector2.zero);uv.Add(Vector2.right);uv.Add(Vector2.one);uv.Add(Vector2.up);
    triangles[material].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
   }
   public void BevelBox(Vector3 center,Vector3 size,float bevel,int material)
   {
    bevel=Mathf.Min(bevel,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.45f);
    var rings=new Vector3[4][];
    for(int ring=0;ring<4;ring++)
    {
     float inset=ring==0 || ring==3 ? bevel*.65f : 0;
     float y=ring==0 ? -size.y*.5f : ring==1 ? -size.y*.5f+bevel : ring==2 ? size.y*.5f-bevel : size.y*.5f;
     float x=size.x*.5f-inset,z=size.z*.5f-inset,cut=bevel*.75f;
     rings[ring]=new[]{new Vector3(x-cut,y,z),new Vector3(x,y,z-cut),new Vector3(x,y,-z+cut),new Vector3(x-cut,y,-z),new Vector3(-x+cut,y,-z),new Vector3(-x,y,-z+cut),new Vector3(-x,y,z-cut),new Vector3(-x+cut,y,z)};
     for(int i=0;i<8;i++)rings[ring][i]+=center;
    }
    for(int ring=0;ring<3;ring++)for(int i=0;i<8;i++)
    {
     int j=(i+1)%8;Vector3 hint=(rings[ring][i]+rings[ring][j])*.5f-center;hint.y=0;
     Quad(rings[ring][i],rings[ring][j],rings[ring+1][j],rings[ring+1][i],material,hint);
    }
    for(int i=0;i<8;i++)
    {
     Triangle(center-Vector3.up*size.y*.5f,rings[0][i],rings[0][(i+1)%8],material,Vector3.down);
     Triangle(center+Vector3.up*size.y*.5f,rings[3][i],rings[3][(i+1)%8],material,Vector3.up);
    }
   }
   public void Extrude(Vector2[] outline,float back,float front,int material)
   {
    Vector2 center=Vector2.zero;foreach(var point in outline)center+=point;center/=outline.Length;
    for(int i=0;i<outline.Length;i++)
    {
     Vector2 p=outline[i],q=outline[(i+1)%outline.Length];Vector3 a=new Vector3(p.x,p.y,front),b=new Vector3(q.x,q.y,front);
     Vector3 c=new Vector3(q.x,q.y,back),d=new Vector3(p.x,p.y,back);
     Triangle(new Vector3(center.x,center.y,front),a,b,material,Vector3.forward);
     Triangle(new Vector3(center.x,center.y,back),d,c,material,Vector3.back);
     Vector2 side=(p+q)*.5f-center;Quad(a,b,c,d,material,new Vector3(side.x,side.y,0));
    }
   }
   public void Frame(Vector2[] outer,Vector2[] inner,float back,float front,int faceMaterial,int sideMaterial)
   {
    for(int i=0;i<outer.Length;i++)
    {
     int j=(i+1)%outer.Length;
     Vector3 a=new Vector3(outer[i].x,outer[i].y,front),b=new Vector3(outer[j].x,outer[j].y,front);
     Vector3 c=new Vector3(inner[j].x,inner[j].y,front),d=new Vector3(inner[i].x,inner[i].y,front);
     Vector3 depth=Vector3.back*(front-back);
     Quad(a,b,c,d,i>3 && i%3==0 ? Sand : faceMaterial,Vector3.forward);
     Quad(a+depth,b+depth,c+depth,d+depth,sideMaterial,Vector3.back);
     Quad(a,b,b+depth,a+depth,sideMaterial,new Vector3((a.x+b.x)*.5f,(a.y+b.y)*.5f-.75f,0));
     Quad(c,d,d+depth,c+depth,sideMaterial,new Vector3(-(c.x+d.x)*.5f,.75f-(c.y+d.y)*.5f,0));
    }
   }
   public void Lathe(Vector3 center,Vector2[] profile,int sides,int material,bool caps=true)
   {
    for(int level=0;level<profile.Length-1;level++)for(int i=0;i<sides;i++)
    {
     float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
     Vector3 p=new Vector3(Mathf.Cos(a)*profile[level].x,profile[level].y,Mathf.Sin(a)*profile[level].x);
     Vector3 q=new Vector3(Mathf.Cos(b)*profile[level].x,profile[level].y,Mathf.Sin(b)*profile[level].x);
     Vector3 r=new Vector3(Mathf.Cos(b)*profile[level+1].x,profile[level+1].y,Mathf.Sin(b)*profile[level+1].x);
     Vector3 s=new Vector3(Mathf.Cos(a)*profile[level+1].x,profile[level+1].y,Mathf.Sin(a)*profile[level+1].x);
     Quad(center+p,center+q,center+r,center+s,material,new Vector3(Mathf.Cos((a+b)*.5f),0,Mathf.Sin((a+b)*.5f)));
    }
    if(caps){Disc(center+Vector3.up*profile[0].y,profile[0].x,sides,material,Vector3.down);int last=profile.Length-1;Disc(center+Vector3.up*profile[last].y,profile[last].x,sides,material,Vector3.up);}
   }
   public void Disc(Vector3 center,float radius,int sides,int material,Vector3 normal)
   {
    for(int i=0;i<sides;i++)
    {float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;Triangle(center,center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius,center+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*radius,material,normal);}
   }
   public void Ribbon(Vector2[] points,float z,float width,int material)
   {
    for(int i=0;i<points.Length-1;i++)
    {
     Vector2 direction=(points[i+1]-points[i]).normalized,side=new Vector2(-direction.y,direction.x)*width*.5f;
     Vector2 a=points[i]-side,b=points[i]+side,c=points[i+1]+side,d=points[i+1]-side;
     Quad(new Vector3(a.x,a.y,z),new Vector3(b.x,b.y,z),new Vector3(c.x,c.y,z),new Vector3(d.x,d.y,z),material,Vector3.forward);
    }
   }
  }
 }
}
