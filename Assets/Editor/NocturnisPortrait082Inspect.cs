using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
namespace Krypta.Editor
{
 // Diagnostic on temporary clones only; no asset, camera preset or gameplay mutation.
 public static class NocturnisPortrait082Inspect
 {
  [Serializable]sealed class VertexLimit {public string mesh;public int pose,vertex,plane;public Vector3 point;public float value,radius;}
  [Serializable]sealed class MeshSample
  {
   public string name;public Vector3 scale,bakedMin,bakedMax,scaledBakeMin,scaledBakeMax,cpuMin,cpuMax;
   public float bakedCpuError,scaledBakeCpuError,frontBakedHeightPixels,frontCpuHeightPixels;
   public int vertices,blendShapes;
  }
  [Serializable]sealed class Hero
  {
   public int hero;public Vector3 root,camera,head,hips;public float idleLength,cameraDistance,centerY;
   public Vector3 bakedMin,bakedMax,cpuMin,cpuMax;
   public float[] requiredDistanceBaked,requiredDistanceCpu;
   public List<VertexLimit> limitsBaked=new List<VertexLimit>(),limitsCpu=new List<VertexLimit>();
   public List<MeshSample> meshes=new List<MeshSample>();
   public float updateOrderMaximumDifference;
  }
  [Serializable]sealed class Report {public bool passed;public string error="";public List<Hero> heroes=new List<Hero>();}
  sealed class Envelope
  {
   readonly Vector3 origin;readonly Vector3[] normals;
   public Vector3 min=Vector3.one*float.PositiveInfinity,max=Vector3.one*float.NegativeInfinity;
   public VertexLimit[] limits=new VertexLimit[4];
   public Envelope(Vector3 point,Camera camera)
   {
    origin=point;float t=Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
    Vector3 f=camera.transform.forward,r=camera.transform.right,u=camera.transform.up;
    float kx=t*camera.aspect*NocturnisPortraitFrame.Occupancy,ky=t*NocturnisPortraitFrame.Occupancy;
    normals=new[]{r/kx-f,-r/kx-f,u/ky-f,-u/ky-f};
    for(int i=0;i<4;i++)limits[i]=new VertexLimit{plane=i,value=float.NegativeInfinity};
   }
   public void Include(Vector3[] points,string name,int pose)
   {
    for(int j=0;j<points.Length;j++)
    {
     Vector3 p=points[j]-origin;min=Vector3.Min(min,p);max=Vector3.Max(max,p);float radius=Mathf.Sqrt(p.x*p.x+p.z*p.z);
     for(int i=0;i<4;i++)
     {
      var n=normals[i];float v=n.y*p.y+radius*Mathf.Sqrt(n.x*n.x+n.z*n.z);
      if(v>limits[i].value)limits[i]=new VertexLimit{plane=i,value=v,point=p,mesh=name,pose=pose,vertex=j,radius=radius};
     }
    }
   }
   public float[] Required(float y)
   {var values=new float[4];for(int i=0;i<4;i++)values[i]=limits[i].value-y*normals[i].y+.03f;return values;}
  }
  static Transform Find(GameObject root,string name)
  {foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name==name)return t;return null;}
  static Vector3[] World(Vector3[] local,Transform transform)
  {var result=new Vector3[local.Length];for(int i=0;i<local.Length;i++)result[i]=transform.TransformPoint(local[i]);return result;}
  static Vector3[] Cpu(SkinnedMeshRenderer renderer)
  {
   var mesh=renderer.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes;var bones=renderer.bones;
   var transforms=new Matrix4x4[bones.Length];for(int i=0;i<bones.Length;i++)transforms[i]=bones[i].localToWorldMatrix*bind[i];
   var points=new Vector3[vertices.Length];
   for(int i=0;i<vertices.Length;i++)
   {
    var w=weights[i];var p=vertices[i];
    points[i]=transforms[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+
     transforms[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+
     transforms[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+
     transforms[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
   }
   return points;
  }
  static void BoundsOf(Vector3[] p,Vector3 origin,out Vector3 min,out Vector3 max)
  {min=Vector3.one*float.PositiveInfinity;max=Vector3.one*float.NegativeInfinity;foreach(var v in p){min=Vector3.Min(min,v-origin);max=Vector3.Max(max,v-origin);}}
  static float Error(Vector3[] a,Vector3[] b)
  {float max=0;for(int i=0;i<a.Length;i++)max=Mathf.Max(max,Vector3.Distance(a[i],b[i]));return max;}
  static float PixelHeight(Camera camera,Vector3[] p)
  {float min=1,max=0;foreach(var v in p){float y=camera.WorldToViewportPoint(v).y;min=Mathf.Min(min,y);max=Mathf.Max(max,y);}return(max-min)*432;}
  public static void Run()
  {
   var report=new Report();
   try
   {
    var art=Resources.Load<KryptaAssets>("KryptaAssets");
    for(int hero=0;hero<art.heroes.Length;hero++)
    {
     var h=art.heroes[hero];var actor=new ActorVisual(h.model,h.animations,new Vector3(1000,0,1000),2.5f,"Portrait diagnostic");
     var cameraObject=new GameObject("Disabled geometry-only camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
     var scratch=new Mesh();var scaled=new Mesh();
     try
     {
      actor.Equip(h.weapon);actor.Root.transform.rotation=Quaternion.Euler(0,8,0);NocturnisPortraitFrame.Fit(camera,actor);
      var origin=actor.Root.transform.position;var animation=actor.Root.GetComponentInChildren<Animation>();
      var result=new Hero{hero=hero,root=origin,camera=camera.transform.position,idleLength=animation["Idle"].length};
      result.cameraDistance=Vector3.Dot(origin-camera.transform.position,camera.transform.forward);
      var bakedEnvelope=new Envelope(origin,camera);var cpuEnvelope=new Envelope(origin,camera);
      var skins=actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>();var filters=actor.Root.GetComponentsInChildren<MeshFilter>();
      for(int pose=0;pose<24;pose++)
      {
       actor.PreviewIdle(result.idleLength*pose/24f);
       foreach(var renderer in skins)
       {
        if(!renderer.enabled||!renderer.gameObject.activeInHierarchy||!renderer.sharedMesh)continue;
        renderer.BakeMesh(scratch);renderer.BakeMesh(scaled,true);
        var baked=World(scratch.vertices,renderer.transform);var scaledPoints=World(scaled.vertices,renderer.transform);var cpu=Cpu(renderer);
        bakedEnvelope.Include(baked,renderer.name,pose);cpuEnvelope.Include(cpu,renderer.name,pose);
        if(pose==0)
        {
         var m=new MeshSample{name=renderer.name,scale=renderer.transform.lossyScale,vertices=cpu.Length,blendShapes=renderer.sharedMesh.blendShapeCount,
          bakedCpuError=Error(baked,cpu),scaledBakeCpuError=Error(scaledPoints,cpu),
          frontBakedHeightPixels=PixelHeight(camera,baked),frontCpuHeightPixels=PixelHeight(camera,cpu)};
         BoundsOf(baked,origin,out m.bakedMin,out m.bakedMax);BoundsOf(scaledPoints,origin,out m.scaledBakeMin,out m.scaledBakeMax);BoundsOf(cpu,origin,out m.cpuMin,out m.cpuMax);
         result.meshes.Add(m);
        }
       }
       foreach(var filter in filters)
       {
        var renderer=filter.GetComponent<MeshRenderer>();if(!renderer||!renderer.enabled||!filter.gameObject.activeInHierarchy||!filter.sharedMesh)continue;
        var points=World(filter.sharedMesh.vertices,filter.transform);bakedEnvelope.Include(points,filter.name,pose);cpuEnvelope.Include(points,filter.name,pose);
        if(pose==0)
        {var m=new MeshSample{name=filter.name,scale=filter.transform.lossyScale,vertices=points.Length,frontBakedHeightPixels=PixelHeight(camera,points),frontCpuHeightPixels=PixelHeight(camera,points)};BoundsOf(points,origin,out m.bakedMin,out m.bakedMax);m.cpuMin=m.bakedMin;m.cpuMax=m.bakedMax;result.meshes.Add(m);}
       }
      }
      result.bakedMin=bakedEnvelope.min;result.bakedMax=bakedEnvelope.max;result.cpuMin=cpuEnvelope.min;result.cpuMax=cpuEnvelope.max;
      result.centerY=(result.bakedMin.y+result.bakedMax.y)*.5f;
      result.requiredDistanceBaked=bakedEnvelope.Required(result.centerY);
      result.requiredDistanceCpu=cpuEnvelope.Required((result.cpuMin.y+result.cpuMax.y)*.5f);
      result.limitsBaked.AddRange(bakedEnvelope.limits);result.limitsCpu.AddRange(cpuEnvelope.limits);
      actor.PreviewIdle(.75f);var before=Cpu(skins[0]);
      var fit=actor.Root.GetComponentInChildren<NocturnisWeaponGrip>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
      typeof(NocturnisWeaponGrip).GetMethod("Update",flags).Invoke(fit,null);typeof(NocturnisWeaponGrip).GetMethod("LateUpdate",flags).Invoke(fit,null);
      result.updateOrderMaximumDifference=Error(before,Cpu(skins[0]));
      var head=Find(actor.Root,"head");var hips=Find(actor.Root,"pelvis");
      if(head)result.head=head.position-origin;if(hips)result.hips=hips.position-origin;
      report.heroes.Add(result);
     }
     finally{UnityEngine.Object.DestroyImmediate(scratch);UnityEngine.Object.DestroyImmediate(scaled);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(actor.Root);}
    }
    report.passed=true;
   }
   catch(Exception e){report.error=e.ToString();Debug.LogException(e);}
   Directory.CreateDirectory("Logs/Portrait082Inspect");File.WriteAllText("Logs/Portrait082Inspect/framing.json",JsonUtility.ToJson(report,true));
   Debug.Log("PORTRAIT082_INSPECT_COMPLETE "+report.passed);EditorApplication.Exit(report.passed?0:1);
  }
 }
}
