using System;
using UnityEngine;
namespace Krypta {
 // Post-animation hand fitting preserves the authored arm pose and grip center.
 // The glove curl and grip center share the same physical cross-section.
 public sealed class NocturnisWeaponGrip : MonoBehaviour {
  ActorVisual actor;Transform slot,weapon,hand,upper,lower,model;Animation animation;
  float height,worldHeight;Quaternion correction;Vector3 originalSlot;string kind;Mesh owned;
  static readonly string[] RestClips={"Idle","Walking_A","Running_A"};
  int appliedFrame=-1;Transform[] adjusted;Quaternion[] basePose;bool fitted;
  public Vector3 GripCenter {get{return slot?slot.position:transform.position;}}
  static Transform Find(Transform root,string name){foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;return null;}
  public void Initialize(ActorVisual owner,Transform item,float modelHeight) {
   actor=owner;weapon=item;kind=item.name;height=modelHeight;slot=item.parent;hand=slot.parent;
   model=hand.GetComponentInParent<ActorModelProfile>().transform;
   worldHeight=height*model.lossyScale.y;
   upper=Find(model,"armR");lower=Find(model,"forearmR");animation=owner.Root.GetComponentInChildren<Animation>();
   originalSlot=new Vector3(slot.localPosition.x,-.04f*height,.024f*height);
   slot.localPosition=new Vector3(originalSlot.x,-.048f*height,.022f*height);
   var cradle=Find(item,"BladeRestAngle");
   Vector3 shaft=cradle?cradle.localRotation*Vector3.up:Vector3.up;
   correction=Quaternion.FromToRotation(shaft,-Vector3.right);
   // Roll only the weapon around its own handle: wrist fitting stays unchanged.
   // The asymmetric axe and scythe have a physical cutting side; a bow has a draw side.
   // Profiles were checked against the active meshes and the actual attack-tip velocity.
   item.localRotation=correction*Quaternion.AngleAxis(FacingRoll(kind),shaft);item.localPosition=Vector3.zero;
   adjusted=new[]{hand,upper,Find(model,"armL"),Find(model,"forearmL"),Find(model,"handL")};basePose=new Quaternion[adjusted.Length];
   FitFingers();
  }
  static float FacingRoll(string weaponName){
   if(weaponName.Contains("weapon_axe"))return 165f; // Leading edge for Slice and Spin.
   if(weaponName.Contains("weapon_sword"))return -30f; // Both edges align with Chop and Spin.
   if(weaponName.Contains("weapon_scythe"))return 90f; // Hook faces away from the casting body.
   if(weaponName.Contains("weapon_bow"))return 0f; // String already faces the drawing hand.
   if(weaponName.Contains("weapon_staff"))return 0f; // Upright radial head has no cutting edge.
   return 0f; // Unknown assets retain their authored orientation.
  }
  float RestPoseAngle(){
   if(!animation)return 0;
   float weight=0,angle=0;
   foreach(string name in RestClips){
    var state=animation[name];if(state==null || !state.enabled)continue;
    weight+=state.weight;angle+=state.weight*(name=="Running_A"?50f:45f);
   }
   return weight>1 ? angle/weight : angle;
  }
  void FitFingers(){
   var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();if(!skin)return;
   var original=skin.sharedMesh;int index=Array.IndexOf(skin.bones,hand);if(index<0)return;
   owned=Instantiate(original);owned.name=original.name+" hand fit";
   var vertices=owned.vertices;var normals=owned.normals;var weights=owned.boneWeights;
   Vector3 joint=original.bindposes[index].inverse.MultiplyPoint3x4(Vector3.zero);
   Vector3 palm=joint+new Vector3(0,-.04f*height,.006f*height);
   float knuckle=palm.y-.008f*height,oldRadius=.027f*height,newRadius=.016f*height;
   for(int i=0;i<vertices.Length;i++){
    var w=weights[i];float influence=(w.boneIndex0==index?w.weight0:0)+(w.boneIndex1==index?w.weight1:0)+(w.boneIndex2==index?w.weight2:0)+(w.boneIndex3==index?w.weight3:0);
    var v=vertices[i];if(influence<.98f || v.y>=knuckle || Math.Abs(v.x-palm.x)>.052f*height)continue;
    float dy=knuckle-v.y,dz=palm.z+oldRadius-v.z;
    float angle=Mathf.Atan2(dy,dz);if(angle<0 || angle>2.55f)continue;
    float depth=oldRadius-Mathf.Sqrt(dy*dy+dz*dz);
    if(Mathf.Abs(depth)>.018f*height)continue;
    float next=Mathf.Min(2.9f,angle*oldRadius/newRadius);
    float radius=Mathf.Max(.004f*height,newRadius-depth);
    vertices[i]=new Vector3(v.x,knuckle-Mathf.Sin(next)*radius,palm.z+newRadius-Mathf.Cos(next)*radius);
    normals[i]=Quaternion.AngleAxis(-(next-angle)*Mathf.Rad2Deg,Vector3.right)*normals[i];
   }
   owned.vertices=vertices;owned.normals=normals;owned.RecalculateBounds();owned.RecalculateTangents();skin.sharedMesh=owned;
  }
  // Remove last frame's additive fitting before Unity evaluates the next pose.
  // This also prevents accumulated wrist rotation while the animation clock is paused.
  void Update(){
   if(!fitted)return;
   for(int i=0;i<adjusted.Length;i++)if(adjusted[i])adjusted[i].localRotation=basePose[i];
   fitted=false;
   // A paused portrait can sample/apply in another component's Update before this
   // callback. Restoring that pose invalidates its same-frame LateUpdate shortcut.
   appliedFrame=-1;
  }
  void LateUpdate(){Apply(false);}
  public void Apply(bool sampled=true){
   if(!actor.Root || !hand || (!sampled && appliedFrame==Time.frameCount))return;
   appliedFrame=Time.frameCount;
   for(int i=0;i<adjusted.Length;i++)if(adjusted[i])basePose[i]=adjusted[i].localRotation;fitted=true;
   slot.localPosition=new Vector3(originalSlot.x,-.048f*height,.022f*height);
   // A single-edged axe must rest outside the legs after its cutting face is
   // corrected. Abduct the complete arm from the shoulder; never detach the
   // handle or twist the wrist. Animation blend weights fade this carrying pose
   // out before a strike and back in during locomotion, preserving its contact.
   if(kind.Contains("weapon_axe") && upper){
    float carry=RestPoseAngle();
    if(carry>0)upper.rotation=Quaternion.AngleAxis(-carry,actor.Root.transform.forward)*upper.rotation;
   }
   // Animation has just sampled the original skeleton.
   Quaternion weaponRotation=hand.rotation;
   Vector3 grip=hand.TransformPoint(originalSlot);
   bool locomotion=actor.CurrentClip=="Idle"||actor.CurrentClip=="Walking_A"||actor.CurrentClip=="Running_A";
   bool longWeapon=kind.Contains("staff")||kind.Contains("scythe")||kind.Contains("bow");
   // Keep the authored shoulder/elbow animation and armor silhouette.
   // Only the wrist rotates so the curled fingers surround the original shaft axis.
   hand.rotation=weaponRotation*Quaternion.Inverse(correction);
   // The second hand follows an actual handle point during two-handed holds.
   if(!locomotion && (longWeapon || kind.Contains("axe")) && actor.CurrentClip!="Death_A" && actor.CurrentClip!="Dodge_Forward"){
    var left=Find(model,"handL");var leftSlot=Find(model,"handslot.l");
    if(left && leftSlot){
     Vector3 target=slot.position+weaponRotation*Vector3.up*(kind.Contains("bow")?.02f:.22f);
     if(kind.Contains("bow"))target=weapon.position+weapon.rotation*Vector3.right*(.29f*1.25f/1.37327f);
     // Keep the open off-hand close to the shaft/string; no wrist teleports outside arm reach.
     Quaternion leftRotation=Quaternion.LookRotation(-actor.Root.transform.right,actor.Root.transform.forward);
     Vector3 offset=Vector3.Scale(leftSlot.localPosition,left.lossyScale);
     if(Vector3.Distance(leftSlot.position,target)<worldHeight*.12f)Solve(Find(model,"armL"),Find(model,"forearmL"),left,target-leftRotation*offset,
      actor.Root.transform.TransformDirection(new Vector3(1,-.2f,-.4f)),leftRotation);
    }
   }
  }
  static void Solve(Transform a,Transform b,Transform c,Vector3 goal,Vector3 pole,Quaternion wrist){
   if(!a||!b||!c)return;
   Vector3 origin=a.position;float first=Vector3.Distance(origin,b.position),second=Vector3.Distance(b.position,c.position);
   Vector3 delta=goal-origin;float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(first-second)+.002f,first+second-.002f);
   Vector3 direction=delta.normalized;if(direction.sqrMagnitude<.5f)return;
   Vector3 bend=Vector3.ProjectOnPlane(pole,direction).normalized;
   if(bend.sqrMagnitude<.5f)bend=Vector3.Cross(direction,Vector3.up).normalized;
   float along=(first*first-second*second+distance*distance)/(2*distance);
   Vector3 elbow=origin+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,first*first-along*along));
   a.rotation=Quaternion.FromToRotation(b.position-origin,elbow-origin)*a.rotation;
   b.rotation=Quaternion.FromToRotation(c.position-b.position,origin+direction*distance-b.position)*b.rotation;
   c.rotation=wrist;
  }
  void OnDestroy(){if(owned){if(Application.isPlaying)Destroy(owned);else DestroyImmediate(owned);}}
 }
}
