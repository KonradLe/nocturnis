using UnityEngine;
using UnityEngine.Rendering;
namespace Krypta {
 // The short steel wake follows the animated blade tip, never the actor root.
 public sealed class NocturnisWeaponTrail : MonoBehaviour {
  ActorVisual actor;TrailRenderer trail;bool wasEmitting;string previousClip;
  public void Initialize(ActorVisual owner,Material material){
   actor=owner;trail=gameObject.AddComponent<TrailRenderer>();trail.sharedMaterial=material;
   trail.time=.12f;trail.minVertexDistance=.018f;trail.startWidth=.105f;trail.endWidth=0;
   trail.numCapVertices=2;trail.numCornerVertices=2;trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;
   trail.startColor=new Color(.82f,.88f,1,.42f);trail.endColor=new Color(.65f,.72f,.85f,0);trail.emitting=false;
  }
  void LateUpdate(){
   if(actor==null || !trail)return;
   string clip=actor.CurrentClip;float phase=actor.CurrentClipNormalizedTime;
   bool melee=clip=="1H_Melee_Attack_Chop" || clip=="1H_Melee_Attack_Slice_Horizontal" || clip=="2H_Melee_Attack_Spin";
   bool emit=melee && phase>=.28f && phase<=.68f && Time.timeScale>0;
   if(emit && (!wasEmitting || previousClip!=clip))trail.Clear();
   trail.emitting=emit;wasEmitting=emit;previousClip=clip;
  }
 }
}
