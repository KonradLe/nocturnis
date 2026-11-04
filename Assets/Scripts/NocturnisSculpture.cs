using UnityEngine;
namespace Krypta
{
 public sealed class NocturnisSculpture : MonoBehaviour
 {
  public Mesh ownedMesh;
  public bool pulse;
  void Update(){if(pulse)transform.localScale=Vector3.one*(1+.035f*Mathf.Sin(Time.time*2.4f));}
  void OnDestroy(){if(ownedMesh)Destroy(ownedMesh);}
 }
 public sealed partial class KryptaGame
 {
  GameObject StoneHeart(Vector3 p)
  {
   const int count=40;var v=new Vector3[count*2+2];var indices=new System.Collections.Generic.List<int>();
   for(int i=0;i<count;i++)
   {
    float t=i*Mathf.PI*2/count;float x=16*Mathf.Pow(Mathf.Sin(t),3)/16*1.55f;
    float y=(13*Mathf.Cos(t)-5*Mathf.Cos(2*t)-2*Mathf.Cos(3*t)-Mathf.Cos(4*t))/17*1.8f;
    v[i]=new Vector3(x,y,-.45f);v[count+i]=new Vector3(x,y,.45f);
    int n=(i+1)%count;indices.AddRange(new[]{count*2,i,n,count*2+1,count+n,count+i,i,count+i,n,n,count+i,count+n});
   }
   v[count*2]=new Vector3(0,0,-.85f);v[count*2+1]=new Vector3(0,0,.85f);
   var mesh=new Mesh{name="Serce Varkosa • rzeźba"};mesh.vertices=v;mesh.triangles=indices.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
   var root=new GameObject("Pęknięte serce Varkosa");root.transform.SetParent(world);root.transform.position=p+Vector3.up*3;root.transform.rotation=Quaternion.Euler(0,-25,0);root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterial=art.wallMaterial;
   var owner=root.AddComponent<NocturnisSculpture>();owner.ownedMesh=mesh;owner.pulse=true;
   for(int i=0;i<3;i++)
   {var crack=new GameObject("Żyła Ichoritu");crack.transform.SetParent(root.transform,false);var l=crack.AddComponent<LineRenderer>();l.useWorldSpace=false;l.sharedMaterial=art.effectMaterial;l.startColor=l.endColor=art.violetMaterial.color;l.widthMultiplier=.07f;l.positionCount=4;float side=i-1;l.SetPositions(new[]{new Vector3(side*.7f,1,-.5f),new Vector3(side*.4f,.4f,-.75f),new Vector3(.15f,-.4f,-.8f),new Vector3(0,-1.55f,-.5f)});}
   return root;
  }
  void Gargoyle(Enemy e)
  {
   foreach(var r in e.visual.Root.GetComponentsInChildren<Renderer>())r.sharedMaterial=art.wallMaterial;
   for(int side=-1;side<=1;side+=2)
   {
    var go=new GameObject("Kamienne skrzydło");go.transform.SetParent(e.visual.Root.transform,false);go.transform.localPosition=new Vector3(side*.25f,1.2f,.15f);
    var mesh=new Mesh{name="Skrzydło gargulca"};mesh.vertices=new[]{Vector3.zero,new Vector3(side*1.5f,.9f,.2f),new Vector3(side*1.25f,-.3f,.3f),new Vector3(side*.65f,.05f,.15f),new Vector3(side*.4f,-.5f,0)};mesh.triangles=new[]{0,1,3,1,2,3,0,3,4,3,1,0,3,2,1,4,3,0};mesh.RecalculateNormals();mesh.RecalculateBounds();go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=art.wallMaterial;go.AddComponent<NocturnisSculpture>().ownedMesh=mesh;
   }
  }
  void OverseerAnchor(Enemy e)
  {
   var arm=CubeOn(e.visual.Root.transform,"Kotwica zamiast ramienia",new Vector3(.9f,1.1f,.1f),new Vector3(.2f,1.8f,.2f),art.wallMaterial);
   CubeOn(e.visual.Root.transform,"Poprzeczka kotwicy",new Vector3(.9f,.3f,.1f),new Vector3(1.2f,.2f,.2f),art.wallMaterial);
   foreach(int side in new[]{-1,1}){var hook=CubeOn(e.visual.Root.transform,"Hak kotwicy",new Vector3(.9f+side*.55f,.5f,.1f),new Vector3(.17f,.6f,.22f),art.wallMaterial);hook.transform.localRotation=Quaternion.Euler(0,0,-side*25);}
  }
 }
}
