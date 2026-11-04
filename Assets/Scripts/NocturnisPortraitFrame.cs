using UnityEngine;

namespace Krypta
{
 // Fit once across resting poses and every possible yaw. Rotation never changes
 // the camera distance, so neither dragging nor breathing makes the view zoom.
 public static class NocturnisPortraitFrame
 {
  public const int Width=1004,Height=864;
  public const float Occupancy=.88f;
  sealed class OrbitEnvelope
  {
   public readonly Vector3 forward;
   readonly Vector3[] normals;
   readonly float[] horizontal,maximum;
   readonly float depthRadius;
   public float bottom=float.PositiveInfinity,top=float.NegativeInfinity;
   public float minimumDepth=float.PositiveInfinity,maximumDepth=float.NegativeInfinity;
   public bool found;
   public OrbitEnvelope(Quaternion view,float aspect,float tangent)
   {
    forward=view*Vector3.forward;
    Vector3 right=view*Vector3.right,up=view*Vector3.up;
    float kx=tangent*aspect*Occupancy,ky=tangent*Occupancy;
    normals=new[]{right/kx-forward,-right/kx-forward,up/ky-forward,-up/ky-forward};
    horizontal=new float[4];maximum=new float[4];
    for(int i=0;i<4;i++)
    {
     horizontal[i]=Mathf.Sqrt(normals[i].x*normals[i].x+normals[i].z*normals[i].z);
     maximum[i]=float.NegativeInfinity;
    }
    depthRadius=Mathf.Sqrt(forward.x*forward.x+forward.z*forward.z);
   }
   public void Include(Vector3[] vertices,Transform transform,Vector3 origin)
   {
    foreach(var vertex in vertices)
    {
     Vector3 p=transform.TransformPoint(vertex)-origin;
     float radius=Mathf.Sqrt(p.x*p.x+p.z*p.z);
     bottom=Mathf.Min(bottom,p.y);top=Mathf.Max(top,p.y);found=true;
     // For all yaw angles, dot(n, rotatedVertex) reaches exactly
     // n.y*y + radius*length(n.xz). These four plane inequalities are
     // the horizontal and vertical perspective limits, including depth.
     for(int i=0;i<4;i++)
      maximum[i]=Mathf.Max(maximum[i],p.y*normals[i].y+radius*horizontal[i]);
     minimumDepth=Mathf.Min(minimumDepth,p.y*forward.y-radius*depthRadius);
     maximumDepth=Mathf.Max(maximumDepth,p.y*forward.y+radius*depthRadius);
    }
   }
   public float Distance(float centerY,float nearClip)
   {
    float distance=nearClip+.03f-minimumDepth+centerY*forward.y;
    for(int i=0;i<4;i++)distance=Mathf.Max(distance,maximum[i]-centerY*normals[i].y+.03f);
    return distance;
   }
  }
  public static void Fit(Camera camera,ActorVisual actor)
  {
   if(!camera || actor==null || !actor.Root)return;
   var root=actor.Root.transform;
   Quaternion view=Quaternion.LookRotation(new Vector3(-1.4f,-.45f,-5.8f));
   Vector3 origin=root.position;
   float aspect=(float)Width/Height,tangent=Mathf.Tan(31*Mathf.Deg2Rad*.5f);
   var envelope=new OrbitEnvelope(view,aspect,tangent);
   var scratch=new Mesh();
   var skin=actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>();
   var filters=actor.Root.GetComponentsInChildren<MeshFilter>();
   var animation=actor.Root.GetComponentInChildren<Animation>();
   float length=animation && animation["Idle"] ? animation["Idle"].length : 1;
   try
   {
    for(int pose=0;pose<24;pose++)
    {
     actor.PreviewIdle(length*pose/24f);
     foreach(var renderer in skin)
     {
      if(!renderer.enabled || !renderer.gameObject.activeInHierarchy || !renderer.sharedMesh)continue;
      // Compensate renderer scale before TransformPoint applies it once.
      renderer.BakeMesh(scratch,true);
      envelope.Include(scratch.vertices,renderer.transform,origin);
     }
     foreach(var filter in filters)
     {
      var renderer=filter.GetComponent<MeshRenderer>();
      if(!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy || !filter.sharedMesh)continue;
      envelope.Include(filter.sharedMesh.vertices,filter.transform,origin);
     }
    }
   }
   finally
   {
    if(Application.isPlaying)Object.Destroy(scratch);else Object.DestroyImmediate(scratch);
    actor.PreviewIdle(Time.unscaledTime);
   }
   if(!envelope.found)return;
   float centerY=(envelope.bottom+envelope.top)*.5f;
   const float nearClip=.05f;
   float distance=envelope.Distance(centerY,nearClip);
   camera.orthographic=false;camera.fieldOfView=31;camera.aspect=aspect;
   camera.transform.SetPositionAndRotation(origin+Vector3.up*centerY-envelope.forward*distance,view);
   camera.nearClipPlane=nearClip;
   camera.farClipPlane=Mathf.Max(20,distance+envelope.maximumDepth-centerY*envelope.forward.y+4);
  }
 }
}
