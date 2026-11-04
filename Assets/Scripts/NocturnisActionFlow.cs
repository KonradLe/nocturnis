using System;
using UnityEngine;
namespace Krypta {
 public sealed partial class KryptaGame {
  sealed class PowerRelease {
   public ActorVisual actor;public int zone,hero,sequence;public float at;public Action resolve;
  }
  PowerRelease pendingPower;
  int queuedPower=-1;Vector3 queuedAim;float queueUntil,actionUntil;
  public bool PowerWindupActive {get{return pendingPower!=null;}}
  public bool CombatActionBusy {get{return pendingMeleeStrike!=null || pendingPower!=null || Time.time<actionUntil || Time.time<dashEnd;}}
  public void CastSkill(Vector3 aim){RequestPower(1,aim);}
  public void CastDefense(){RequestPower(2,HeroPosition+facing);}
  public void CastAdditional(int i,Vector3 aim){if(i>=0 && i<3)RequestPower(i+3,aim);}
  void RequestPower(int slot,Vector3 aim) {
   // Validate first: an unavailable defensive button must not cancel a valid attack.
   string reason=SkillBlockReason(slot);
   if(reason.Length>0){BeginPower(slot);return;}
   if(slot==2){CancelCombatAction();StartDefense();return;}
   if(CombatActionBusy) {
    // One short input buffer, no resource charge until the action actually begins.
    queuedPower=slot;queuedAim=aim;queueUntil=Time.time+.22f;return;
   }
   if(slot==1)StartPrimary(aim);else StartAdditional(slot-3,aim);
  }
  void ReservePower(int slot){actionUntil=Time.time+(slot==2||slot==3?.25f:slot==1?.31f:.39f);}
  void SchedulePower(float delay,Action resolve){
   pendingPower=new PowerRelease{actor=player,zone=Zone,hero=SelectedHero,sequence=player.ActionSequence,at=Time.time+delay,resolve=resolve};
  }
  void CancelCombatAction(){pendingMeleeStrike=null;pendingPower=null;queuedPower=-1;actionUntil=0;}
  void TickPowerActions(float now){
   var release=pendingPower;
   if(release!=null) {
    if(player!=release.actor || player==null || !player.Root || !player.Root.activeInHierarchy || Health<=0 || Zone!=release.zone || SelectedHero!=release.hero || player.ActionSequence!=release.sequence || State==Phase.Dead || State==Phase.Won || State==Phase.Title || Time.time<dashEnd)
     {pendingPower=null;queuedPower=-1;}
    else if(State==Phase.Playing && now>=release.at){pendingPower=null;release.resolve();}
   }
   if(queuedPower<0)return;
   if(State==Phase.Dead || State==Phase.Title || State==Phase.Won || now>queueUntil){queuedPower=-1;return;}
   if(State!=Phase.Playing || CombatActionBusy)return;
   int slot=queuedPower;var aim=queuedAim;queuedPower=-1;RequestPower(slot,aim);
  }
  public void ReviewCompletePower(){
   if(!Testing)return;
   if(pendingPower!=null)TickPowerActions(pendingPower.at);
   // Frozen-clock editor fixtures explicitly advance recovery, never runtime.
   actionUntil=0;
  }
 }
}
