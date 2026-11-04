using UnityEngine;

namespace Krypta
{
 public sealed partial class KryptaGame
 {
  static bool ShouldCastPrimary(bool keyboard,bool mouse,bool overHud)
  {return keyboard || (mouse && !overHud);}

  Enemy FindEnemyAtAim(Vector3 aim)
  {
   Enemy closest=null;float distance=1.4f*1.4f;
   foreach(var enemy in enemies)
   {
    if(enemy.hp<=0 || enemy.visual==null || !enemy.visual.Root || !enemy.visual.Root.activeInHierarchy)continue;
    Vector3 offset=enemy.Position-aim;offset.y=0;float squared=offset.sqrMagnitude;
    if(squared<distance){distance=squared;closest=enemy;}
   }
   return closest;
  }

  void FaceActionAim(Vector3 aim)
  {
   if(player==null || !player.Root)return;
   CancelMeleeActions();
   Vector3 direction=aim-HeroPosition;direction.y=0;
   if(direction.sqrMagnitude<.0001f)return;
   facing=direction.normalized;player.Root.transform.rotation=Quaternion.LookRotation(facing);
  }

  void BeginPlayerDodge(Vector3 direction)
  {
   if(State!=Phase.Playing || player==null || DashReady>0)return;
   CancelCombatAction();dashAt=Time.time+Cooldown(2.2f);dashEnd=Time.time+.2f;
   direction.y=0;dashDirection=direction.sqrMagnitude>.001f ? direction.normalized : facing;
   player.Play("Dodge_Forward",false,.25f);Sound("dodge");Burst(HeroPosition,1,classMaterial);
  }

  bool PlayInteractionAt(Vector3 point)
  {
   if(State!=Phase.Playing || Health<=0 || player==null || !player.Root || CombatActionBusy)return false;
   if(!player.TryPlayInteraction())return false;
   Vector3 direction=point-HeroPosition;direction.y=0;
   if(direction.sqrMagnitude>.0001f){facing=direction.normalized;player.Root.transform.rotation=Quaternion.LookRotation(facing);}
   return true;
  }

  static void FaceTravel(ActorVisual actor,Vector3 displacement,float seconds)
  {
   displacement.y=0;
   if(actor==null || !actor.Root || displacement.sqrMagnitude<.0000001f)return;
   // Exponential response gives the same turn at 30, 60 and 120 fps.
   float weight=1-Mathf.Exp(-18*Mathf.Max(0,seconds));
   actor.Root.transform.rotation=Quaternion.Slerp(actor.Root.transform.rotation,Quaternion.LookRotation(displacement.normalized),weight);
  }

  void BeginEnemyDeath(Enemy enemy)
  {
   CancelEnemyMelee(enemy);enemy.impactAt=0;enemy.corpse=enemy.Position;
   enemy.warning.SetActive(false);
   enemy.visual.Play("Death_A",false,9999);
   // Death_A lasts 1.1 s; retain the fallen body briefly so a kill has weight.
   // No coroutine captures an old death: a resurrection clears this deadline.
   enemy.corpseHideAt=Time.time+3.2f;
   SoundAt("enemy-death",enemy.Position);
  }

  void TickDeadEnemy(Enemy enemy,float now)
  {
   if(enemy.hp>0){enemy.corpseHideAt=0;return;}
   if(enemy.corpseHideAt<=0 || now<enemy.corpseHideAt)return;
   enemy.corpseHideAt=0;
   if(enemy.visual!=null && enemy.visual.Root)enemy.visual.Root.SetActive(false);
  }
 }
}
