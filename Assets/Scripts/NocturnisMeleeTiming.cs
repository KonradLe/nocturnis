using System.Collections.Generic;
using UnityEngine;

namespace Krypta
{
 public sealed partial class KryptaGame
 {
  // Kept in step with the contact poses authored in NocturnisIntegratedBuild.
  const float MeleeContactFraction=.42f;
  const float EnemyMeleeDuration=.45f;
  sealed class MeleeStrike
  {
   public ActorVisual actor;public int zone,hero,actionSequence;
   public Vector3 direction;public float damage,range,contactAt;
  }
  sealed class EnemyMeleeMotion
  {
   public bool swinging;public float lastSample,recoverUntil;
  }
  MeleeStrike pendingMeleeStrike;
  readonly Dictionary<Enemy,EnemyMeleeMotion> enemyMeleeMotions=new Dictionary<Enemy,EnemyMeleeMotion>();
  Transform enemyMeleeWorld;
  public bool MeleeWindupActive {get{return pendingMeleeStrike!=null;}}
  public Vector3 MeleeWindupFacing {get{return pendingMeleeStrike!=null ? pendingMeleeStrike.direction : facing;}}
  void CancelMeleeActions(){pendingMeleeStrike=null;}
  void TickMeleeActions(float now)
  {
   TickPowerActions(now);TickClassMechanics(Time.time);
   var strike=pendingMeleeStrike;if(strike==null)return;
   // Locomotion can resume before this Update on a long frame. That does not
   // cancel a valid contact; only a newer action (cast, hit, dodge, death) does.
   if(player==null || player!=strike.actor || !player.Root || !player.Root.activeInHierarchy || Zone!=strike.zone || SelectedHero!=strike.hero || Health<=0 || State==Phase.Title || State==Phase.Dead || State==Phase.Won || Time.time<dashEnd || player.ActionSequence!=strike.actionSequence)
   {CancelMeleeActions();return;}
   // Pausing freezes the scaled clock and retains a valid windup for resume.
   if(State!=Phase.Playing || now<strike.contactAt)return;
   // Clear before dealing damage: deaths, zone changes or repeated Update calls cannot replay a contact.
   pendingMeleeStrike=null;
   ResolveStrikeContact(strike);
  }
  // Explicit deterministic clock advancement for frozen-clock QA; the real runtime always waits.
  public void ReviewCompleteStrike()
  {if(Testing && pendingMeleeStrike!=null){TickMeleeActions(pendingMeleeStrike.contactAt);actionUntil=0;}}
  EnemyMeleeMotion EnemyMotion(Enemy enemy)
  {
   if(enemyMeleeWorld!=world){enemyMeleeMotions.Clear();enemyMeleeWorld=world;}
   EnemyMeleeMotion motion;
   if(!enemyMeleeMotions.TryGetValue(enemy,out motion))
   {motion=new EnemyMeleeMotion{lastSample=Time.time};enemyMeleeMotions.Add(enemy,motion);}
   return motion;
  }
  void CancelEnemyMelee(Enemy enemy){enemyMeleeMotions.Remove(enemy);}
  void BeginEnemyMelee(Enemy enemy,Vector3 goal)
  {
   var motion=EnemyMotion(enemy);motion.swinging=false;motion.recoverUntil=0;motion.lastSample=Time.time;
   Vector3 direction=goal-enemy.Position;direction.y=0;
   if(direction.sqrMagnitude>.001f)enemy.visual.Root.transform.rotation=Quaternion.LookRotation(direction.normalized);
  }
  bool EnemyMeleeRecovering(Enemy enemy,float now)
  {
   var motion=EnemyMotion(enemy);
   return enemy.impactAt<=0 && now<motion.recoverUntil;
  }
  bool AdvanceEnemyMelee(Enemy enemy,float now){return AdvanceEnemyAttack(enemy,now,"1H_Melee_Attack_Chop");}
  bool AdvanceEnemyAttack(Enemy enemy,float now,string clip)
  {
   var motion=EnemyMotion(enemy);
   if(!motion.swinging)
   {
    // Do not consume an unseen warning on a stalled frame. Guardian timing has its own existing policy.
    float elapsed=Mathf.Max(0,now-motion.lastSample);
    if(elapsed>.1f){float missed=elapsed-.1f;enemy.impactAt+=missed;enemy.attackAt+=missed;}
    motion.lastSample=now;
    float windup=EnemyMeleeDuration*MeleeContactFraction;
    if(now<enemy.impactAt-windup){enemy.visual.Tick(false);return false;}
    // The strike must start visibly before its damage; never start a chop after applying damage.
    enemy.visual.Play(clip,false,EnemyMeleeDuration);
     SoundAt("enemy-swing",enemy.Position);
    motion.swinging=true;enemy.impactAt=Mathf.Max(enemy.impactAt,now+windup);
    motion.recoverUntil=enemy.impactAt+EnemyMeleeDuration*(1-MeleeContactFraction);
    return false;
   }
   return now>=enemy.impactAt;
  }
 }
}
