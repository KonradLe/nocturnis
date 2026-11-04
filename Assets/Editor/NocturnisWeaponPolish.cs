using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace Krypta.Editor
{
 // Runs after SurfacePolish. No rig, skin, clip or hand transform is regenerated.
 public static class NocturnisWeaponPolish
 {
  const string Root="Assets/NocturnisActI/Runtime/",Folder="Assets/NocturnisActI/WeaponPolish/";
  static readonly string[] Ids={"weapon_sword","weapon_scythe","weapon_bow","weapon_staff","weapon_axe"};
  static readonly Vector3[] Grips={new Vector3(0,.14f,0),new Vector3(0,.82f,0),new Vector3(-.29f,.72f,0),new Vector3(0,.88f,0),new Vector3(0,.38f,0)};
  [Serializable]sealed class Entry {public string weapon;public int vertices,triangles,drawCalls;public bool anchorsPreserved;}
  [Serializable]sealed class Report {public bool passed;public string scope="Bevelled blades, fittings and leather grips; original hand/weapon anchors and animation rigs retained.";public List<Entry> weapons=new List<Entry>();}
  sealed class Geometry
  {
   public readonly List<Vector3> vertices=new List<Vector3>();public readonly List<Vector2> uv=new List<Vector2>();public readonly List<int> triangles=new List<int>();
   public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
   {int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});uv.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right});triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
   public void Tri(Vector3 a,Vector3 b,Vector3 c)
   {int n=vertices.Count;vertices.AddRange(new[]{a,b,c});uv.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.one});triangles.AddRange(new[]{n,n+1,n+2});}
   public Mesh Mesh(string name)
   {var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(vertices);m.SetTriangles(triangles,0);m.SetUVs(0,uv);m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();return m;}
  }
  static Material Surface(string name)
  {
   var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/NocturnisActI/Materials/MotionWeapons/MotionWeapon_"+name+".mat");
   if(!m)throw new Exception("Run SurfacePolish first: "+name);return m;
  }
  static Material Accent(string name,Material source,Color color,float metal,float rough)
  {
   string path=Folder+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(source);AssetDatabase.CreateAsset(m,path);}
   EditorUtility.CopySerialized(source,m);m.name=name;m.SetColor("_Color",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Roughness",rough);EditorUtility.SetDirty(m);return m;
  }
  static Mesh SaveMesh(Mesh mesh,string path)
  {
   var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
   AssetDatabase.CreateAsset(mesh,path);return mesh;
  }
  static void Tube(Geometry g,IList<Vector3> points,float radius,int sides=8)
  {
   var rings=new Vector3[points.Count][];
   for(int i=0;i<points.Count;i++)
   {
    var axis=(points[Mathf.Min(points.Count-1,i+1)]-points[Mathf.Max(0,i-1)]).normalized;
    var right=Vector3.Cross(axis,Mathf.Abs(Vector3.Dot(axis,Vector3.forward))>.9f?Vector3.up:Vector3.forward).normalized;var forward=Vector3.Cross(axis,right).normalized;
    rings[i]=new Vector3[sides];for(int side=0;side<sides;side++){float a=side*Mathf.PI*2/sides;rings[i][side]=points[i]+(right*Mathf.Cos(a)+forward*Mathf.Sin(a))*radius;}
   }
   for(int i=0;i<points.Count-1;i++)for(int j=0;j<sides;j++)g.Quad(rings[i][j],rings[i][(j+1)%sides],rings[i+1][(j+1)%sides],rings[i+1][j]);
   for(int j=0;j<sides;j++){g.Tri(points[0],rings[0][(j+1)%sides],rings[0][j]);int end=points.Count-1;g.Tri(points[end],rings[end][j],rings[end][(j+1)%sides]);}
  }
  static void Ring(Geometry g,Vector3 center,float radius,float thickness)
  {var points=new List<Vector3>();for(int i=0;i<=20;i++){float a=i*Mathf.PI/10;points.Add(center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}Tube(g,points,thickness,6);}
  static Vector3 Curve(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)
  {float u=1-t;return a*u*u*u+b*3*u*u*t+c*3*u*t*t+d*t*t*t;}
  // Six-sided cross-sections form an actual thick ridge and four cutting bevels.
  // Bright bevel material is used on geometry, not as a flat outline or glow.
  static void Blade(Geometry steel,Geometry edge,Vector3[] centers,float[] widths,float depth)
  {
   var rings=new Vector3[centers.Length][];
   for(int i=0;i<centers.Length;i++)
   {
    var tangent=(centers[Mathf.Min(centers.Length-1,i+1)]-centers[Mathf.Max(0,i-1)]).normalized;var across=new Vector3(-tangent.y,tangent.x,0);
    float w=Mathf.Max(.001f,widths[i]),z=depth*Mathf.Clamp01(w/.04f);
    rings[i]=new[]{centers[i]+across*w*.5f,centers[i]+across*w*.30f+Vector3.forward*z,centers[i]-across*w*.30f+Vector3.forward*z,centers[i]-across*w*.5f,centers[i]-across*w*.30f-Vector3.forward*z,centers[i]+across*w*.30f-Vector3.forward*z};
   }
   for(int i=0;i<centers.Length-1;i++)for(int j=0;j<6;j++)
    (j==1||j==4?steel:edge).Quad(rings[i][j],rings[i][(j+1)%6],rings[i+1][(j+1)%6],rings[i+1][j]);
   for(int j=0;j<6;j++){steel.Tri(centers[0],rings[0][(j+1)%6],rings[0][j]);int k=centers.Length-1;steel.Tri(centers[k],rings[k][j],rings[k][(j+1)%6]);}
  }
  static void Grip(Geometry leather,Geometry bronze,Vector3 center,float radius,float length)
  {
   Tube(leather,new[]{center-Vector3.up*length*.5f,center+Vector3.up*length*.5f},radius,12);
   var spiral=new List<Vector3>();for(int i=0;i<=128;i++){float t=i/128f,a=t*Mathf.PI*14;spiral.Add(center+new Vector3(Mathf.Cos(a)*radius,(t-.5f)*length,Mathf.Sin(a)*radius));}Tube(leather,spiral,.0023f,5);
   Ring(bronze,center-Vector3.up*(length*.5f+.008f),radius+.001f,.004f);Ring(bronze,center+Vector3.up*(length*.5f+.008f),radius+.001f,.004f);
  }
  static void Ornament(int index,Geometry steel,Geometry edge,Geometry bronze,Geometry leather)
  {
   Grip(leather,bronze,Grips[index],index==4?.035f:index==3?.034f:index==2?.035f:.029f,index==4?.23f:.19f);
   if(index==0)
   {
    var p=new Vector3[25];var w=new float[p.Length];for(int i=0;i<p.Length;i++){float t=i/(float)(p.Length-1);p[i]=new Vector3(0,.29f+t*.99f,0);w[i]=t<.76f?.086f-.014f*t:Mathf.Lerp(.075f,.001f,(t-.76f)/.24f);}Blade(steel,edge,p,w,.016f);
    Tube(bronze,new[]{new Vector3(-.18f,.25f,0),new Vector3(-.12f,.28f,0),new Vector3(0,.29f,0),new Vector3(.12f,.28f,0),new Vector3(.18f,.25f,0)},.014f);
    Ring(bronze,new Vector3(0,.025f,0),.041f,.007f);
   }
   else if(index==1)
   {
    var p=new Vector3[33];var w=new float[p.Length];for(int i=0;i<p.Length;i++){float t=i/(float)(p.Length-1);p[i]=Curve(new Vector3(.015f,1.55f,0),new Vector3(-.28f,1.91f,0),new Vector3(-.78f,1.77f,0),new Vector3(-1.0f,1.42f,0),t);w[i]=(.15f+.045f*Mathf.Sin(t*Mathf.PI))*Mathf.Pow(1-t,.6f);}Blade(steel,edge,p,w,.022f);
    Ring(bronze,new Vector3(0,1.49f,0),.031f,.008f);Ring(bronze,new Vector3(0,1.64f,0),.031f,.008f);
    Tube(bronze,new[]{new Vector3(0,1.43f,.029f),new Vector3(-.08f,1.55f,.029f),new Vector3(-.21f,1.65f,.029f)},.008f);
   }
   else if(index==4)
   {
    var p=new Vector3[25];var w=new float[p.Length];
    for(int i=0;i<p.Length;i++) {float t=i/(float)(p.Length-1);Vector3 inside=new Vector3(-.07f,.82f+t*.30f,0),outside=new Vector3(-.35f-.14f*Mathf.Sin(t*Mathf.PI),.72f+t*.48f,0);p[i]=(inside+outside)*.5f;w[i]=Vector3.Distance(inside,outside);}
    Blade(steel,edge,p,w,.029f);
    Blade(steel,edge,new[]{new Vector3(.015f,.98f,0),new Vector3(.13f,1.0f,0),new Vector3(.22f,1.015f,0)},new[]{.10f,.055f,.001f},.025f);
    Ring(bronze,new Vector3(0,.84f,0),.040f,.010f);Ring(bronze,new Vector3(0,1.08f,0),.040f,.010f);
    foreach(float y in new[]{.90f,1.035f})Tube(bronze,new[]{new Vector3(-.10f,y,-.037f),new Vector3(-.10f,y,.037f)},.013f,10);
   }
   else if(index==2)
   {
    Ring(bronze,new Vector3(-.015f,.09f,0),.025f,.005f);Ring(bronze,new Vector3(-.015f,1.36f,0),.025f,.005f);
   }
   else
   {
    Ring(bronze,new Vector3(0,1.73f,0),.037f,.008f);
    for(int i=0;i<4;i++){float a=i*Mathf.PI/2;var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));Tube(bronze,new[]{Vector3.up*1.72f+radial*.025f,Vector3.up*1.84f+radial*.12f,Vector3.up*2.04f+radial*.09f,Vector3.up*2.08f+radial*.055f},.011f);}
   }
  }
  public static void Prepare()
  {
   Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var report=new Report();var steel=Surface("Nocturnis_BlackIron");var bronze=Surface("Nocturnis_OldBronze");
   var edge=Accent("MotionWeapon_CuttingEdge",steel,new Color(.62f,.66f,.71f),.55f,.40f);
   var leather=Accent("MotionWeapon_LeatherWrap",Surface("Nocturnis_DarkOak"),new Color(.12f,.072f,.042f),0,.88f);
   for(int index=0;index<Ids.Length;index++)
   {
    string path=Root+Ids[index]+"_equipped.prefab";var root=PrefabUtility.LoadPrefabContents(path);var temporary=new List<Mesh>();
    try
    {
     var old=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Polished weapon geometry");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
     var anchors=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="GripAnchor"||t.name=="TipAnchor").ToDictionary(t=>t,t=>root.transform.InverseTransformPoint(t.position));
     if(!anchors.Keys.Any(t=>t.name=="GripAnchor"))throw new Exception("Missing grip: "+Ids[index]);
     var cradle=root.transform.Find("BladeRestAngle");var parent=cradle?cradle:root.transform;
     var source=Enumerable.Range(0,parent.childCount).Select(i=>parent.GetChild(i)).First(t=>t.GetComponentsInChildren<MeshFilter>(true).Length>0);
     float scale=Mathf.Abs(source.localPosition.y/Grips[index].y);if(scale<=.01f||scale>10)throw new Exception("Unrecognized authored weapon scale "+Ids[index]);
     var polished=new GameObject("Polished weapon geometry");polished.transform.SetParent(parent,false);polished.transform.localPosition=source.localPosition;polished.transform.localScale=Vector3.one*scale;
     var groups=new Dictionary<Material,List<CombineInstance>>();
     Action<Material,CombineInstance> add=(mat,combine)=>{if(!groups.ContainsKey(mat))groups[mat]=new List<CombineInstance>();groups[mat].Add(combine);};
     foreach(var filter in source.GetComponentsInChildren<MeshFilter>(true))
     {
      var renderer=filter.GetComponent<MeshRenderer>();if(!renderer||!filter.sharedMesh)continue;var mats=renderer.sharedMaterials;
      var sourceMesh=filter.sharedMesh;var transform=polished.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
      if(transform.determinant<0)
      {
       sourceMesh=UnityEngine.Object.Instantiate(sourceMesh);temporary.Add(sourceMesh);
       for(int s=0;s<sourceMesh.subMeshCount;s++){var indices=sourceMesh.GetTriangles(s);for(int k=0;k<indices.Length;k+=3){int swap=indices[k];indices[k]=indices[k+2];indices[k+2]=swap;}sourceMesh.SetTriangles(indices,s);}
      }
      for(int sub=0;sub<sourceMesh.subMeshCount&&sub<mats.Length;sub++)
      {
       var material=mats[sub];if(!material)continue;
       if((index==0||index==1||index==4)&&material.name.Contains("BlackIron"))continue;
       add(material,new CombineInstance{mesh=sourceMesh,subMeshIndex=sub,transform=transform});
      }
      renderer.enabled=false;
     }
     var steelGeometry=new Geometry();var edgeGeometry=new Geometry();var bronzeGeometry=new Geometry();var leatherGeometry=new Geometry();Ornament(index,steelGeometry,edgeGeometry,bronzeGeometry,leatherGeometry);
     var shapes=new[]{steelGeometry,edgeGeometry,bronzeGeometry,leatherGeometry};var materials=new[]{steel,edge,bronze,leather};
     for(int i=0;i<shapes.Length;i++)if(shapes[i].vertices.Count>0){var mesh=shapes[i].Mesh("Weapon detail");temporary.Add(mesh);add(materials[i],new CombineInstance{mesh=mesh,transform=Matrix4x4.identity});}
     var perMaterial=new List<CombineInstance>();foreach(var pair in groups){var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);temporary.Add(mesh);perMaterial.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.identity});}
     var combined=new Mesh{name=Ids[index]+" — shaped blade and bindings",indexFormat=IndexFormat.UInt32};combined.CombineMeshes(perMaterial.ToArray(),false,true);combined.RecalculateBounds();
     // Imported UVs and normals of shafts, strings and the staff gem are retained.
     var saved=SaveMesh(combined,Folder+Ids[index]+".asset");polished.AddComponent<MeshFilter>().sharedMesh=saved;polished.AddComponent<MeshRenderer>().sharedMaterials=groups.Keys.ToArray();
     bool unchanged=anchors.All(a=>Vector3.Distance(a.Value,root.transform.InverseTransformPoint(a.Key.position))<.00001f);if(!unchanged)throw new Exception("Weapon anchor moved: "+Ids[index]);
     int count=saved.triangles.Length/3;if(count>10000||groups.Count>7)throw new Exception("Weapon render budget exceeded: "+Ids[index]+" "+count+" triangles / "+groups.Count+" materials");
     if(saved.normals.Any(n=>n.sqrMagnitude<.8f))throw new Exception("Invalid weapon surface normals: "+Ids[index]);
     PrefabUtility.SaveAsPrefabAsset(root,path);report.weapons.Add(new Entry{weapon=Ids[index],vertices=saved.vertexCount,triangles=count,drawCalls=groups.Count,anchorsPreserved=unchanged});
    }
    finally{foreach(var mesh in temporary)UnityEngine.Object.DestroyImmediate(mesh);PrefabUtility.UnloadPrefabContents(root);}
   }
   AssetDatabase.SaveAssets();report.passed=true;Directory.CreateDirectory("Logs");File.WriteAllText("Logs/weapon-polish-qa.json",JsonUtility.ToJson(report,true));Debug.Log("WEAPON_POLISH_PASS five anchored weapons");
  }
 }
}
