using UnityEngine;
namespace Krypta
{
 public sealed class NocturnisCreature : MonoBehaviour
 {
  public Transform[] legs;
  Vector3 previous;
  void Start(){previous=transform.position;}
  void Update()
  {
   bool walking=(transform.position-previous).sqrMagnitude>.00001f;previous=transform.position;
   for(int i=0;i<legs.Length;i++)if(legs[i])legs[i].localRotation=Quaternion.Euler(walking ? Mathf.Sin(Time.time*12+(i%2)*Mathf.PI)*28 : 0,0,0);
  }
 }
 public sealed partial class KryptaGame
 {
  GameObject HoundModel()
  {
   var root=new GameObject("Ogar Ichoritu");root.transform.SetParent(world);root.transform.position=new Vector3(1000,0,0);
   var body=CubeOn(root.transform,"Żebrowany tułów",new Vector3(0,.8f,0),new Vector3(.72f,.7f,1.5f),art.wallMaterial);body.transform.localRotation=Quaternion.Euler(8,0,0);
   var neck=CubeOn(root.transform,"Karczek",new Vector3(0,1.05f,.65f),new Vector3(.65f,.7f,.6f),art.wallMaterial);neck.transform.localRotation=Quaternion.Euler(30,0,0);
   CubeOn(root.transform,"Wydłużony pysk",new Vector3(0,.95f,1.06f),new Vector3(.46f,.36f,.7f),art.wallMaterial);
   CubeOn(root.transform,"Szczęka",new Vector3(0,.74f,1.06f),new Vector3(.43f,.1f,.64f),art.goldMaterial);
   for(int side=-1;side<=1;side+=2){CubeOn(root.transform,"Oko Ichoritu",new Vector3(side*.26f,1.15f,.97f),new Vector3(.1f,.1f,.14f),art.violetMaterial);var ear=CubeOn(root.transform,"Ucho",new Vector3(side*.23f,1.4f,.67f),new Vector3(.16f,.45f,.22f),art.wallMaterial);ear.transform.localRotation=Quaternion.Euler(-20,0,side*20);}
   Transform[] legs=new Transform[4];
   for(int i=0;i<4;i++){var joint=new GameObject("Staw ogara");joint.transform.SetParent(root.transform,false);joint.transform.localPosition=new Vector3(i%2==0 ? -.32f : .32f,.8f,i<2 ? -.5f : .55f);legs[i]=joint.transform;CubeOn(joint.transform,"Łapa",new Vector3(0,-.4f,0),new Vector3(.22f,.8f,.25f),art.wallMaterial);}
   for(int i=0;i<5;i++){var spine=CubeOn(root.transform,"Kolec Ichoritu",new Vector3(0,1.25f,-.6f+i*.24f),new Vector3(.12f,.4f,.14f),art.violetMaterial);spine.transform.localRotation=Quaternion.Euler(-25,0,0);}
   root.AddComponent<NocturnisCreature>().legs=legs;return root;
  }
 }
}
