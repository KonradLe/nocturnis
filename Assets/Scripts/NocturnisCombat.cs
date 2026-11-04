using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaGame
 {
  float Cooldown(float seconds) { return (seconds*(Status.Count("slaughter",Time.time)>0 ? .25f : 1)+(Status.Count("slow",Time.time)>0 ? 2 : 0))*TalentCooldownMultiplier; }
  void GainResource(float amount)
  {
   Resource=Mathf.Min(100,Resource+amount*TalentResourceMultiplier);
   if(SelectedHero==3 && Resource>=100)
   {Resource=0;Status.Apply("entropy","Przepełnienie",Time.time,6);Burst(player.Root.transform.position,4,art.violetMaterial);ShowNotice("Przepełnienie • +50% obrażeń przez 6 s");}
  }
  void Strike(Vector3 aim)
  {
   if(State!=Phase.Playing || player==null || !player.Root || Health<=0 || Time.time<strikeAt || Time.time<dashEnd || Zone==0 || CombatActionBusy)return;
   strikeAt=Time.time+Hero.interval*(Status.Count("slow",Time.time)>0 ? 2 : 1);
   Vector3 pos=player.Root.transform.position;facing=aim-pos;facing.y=0;facing.Normalize();
   if(facing.sqrMagnitude<.1f)facing=player.Root.transform.forward;
   player.Root.transform.rotation=Quaternion.LookRotation(facing);
   string clip=SelectedHero==0 ? "1H_Melee_Attack_Chop" : Hero.range>3 ? "1H_Ranged_Shoot" : "1H_Melee_Attack_Slice_Horizontal";
   float duration=Mathf.Clamp(Hero.interval*.8f,.32f,.48f);
   player.Play(clip,false,duration);actionUntil=Time.time+duration*.58f;
   pendingMeleeStrike=new MeleeStrike{actor=player,zone=Zone,hero=SelectedHero,actionSequence=player.ActionSequence,direction=facing,damage=AttackPower,range=Hero.range,contactAt=Time.time+duration*MeleeContactFraction};
  }
  void ResolveStrikeContact(MeleeStrike strike)
  {
   Vector3 pos=player.Root.transform.position,direction=strike.direction;
   float damage=strike.damage*ClassContactMultiplier();bool ranged=strike.range>3;
   Sound("attack"+SelectedHero);
   Enemy first=null;float nearest=float.MaxValue;
   foreach(var e in enemies)
   {
    if(e.hp<=0)continue;Vector3 d=e.Position-pos;
    if(d.magnitude>strike.range || !Visible(pos,e.Position))continue;
    if(ranged)
    {float align=Vector3.Dot(direction,d.normalized);if(align>.94f && d.magnitude<nearest){first=e;nearest=d.magnitude;}}
    else if(Vector3.Dot(direction,d.normalized)>.2f){Damage(e,damage);ClassBasicContact(e);if(first==null)first=e;}
   }
   Vector3 end=first!=null ? first.Position : pos+direction*strike.range;
   ClassAttackVfx(pos,end,ranged);
   if(ranged && first!=null){Damage(first,damage);ClassBasicContact(first);}
   StrikeBreakables(pos,direction,strike.range,damage);
   if(first!=null)
   {
    GainResource(SelectedHero==3 ? 20 : 10);
    if(SelectedHero==1)Heal(3);
    int chains=Status.Count("grace",Time.time);
    if(chains>0)foreach(var e in enemies)if(e!=first && e.hp>0 && Vector3.Distance(e.Position,first.Position)<5 && Visible(first.Position,e.Position))
    { Beam(first.Position+Vector3.up,e.Position+Vector3.up,art.goldMaterial);Damage(e,12);if(--chains==0)break; }
   }
  }
  void StartPrimary(Vector3 aim)
  {
   if(!BeginPower(1))return;EnsureClassMechanics();
   bool free=Status.Count("slaughter",Time.time)>0;
   if(!free){if(SelectedHero==3)GainResource(25);else Resource-=25;}
   pulseAt=Time.time+Cooldown(5);Vector3 pos=HeroPosition,center=pos;
   if(SelectedHero==1 || SelectedHero==3)
   {Vector3 delta=Vector3.ClampMagnitude(aim-pos,8);if(Visible(pos,pos+delta))center=pos+delta;}
   float damage=48+Level*6+CharmRank*7;
   FaceActionAim(aim);player.Play(Hero.range>3 ? "Cast_A" : "2H_Melee_Attack_Spin",false,.5f);
   SchedulePower(.5f*MeleeContactFraction,()=>ResolvePrimary(aim,center,damage));
  }
  void ResolvePrimary(Vector3 aim,Vector3 center,float damage)
  {
   Vector3 pos=HeroPosition;int hits=0;string result;Sound(ClassSpellSound);
   if(SelectedHero==2)
   {
    hits=PierceEnemies(aim,12,Rune==1 ? 1.1f : .7f,damage,3,true,Rune==1);
    ClassSpellVfx(1,pos+AimDirection(aim)*3,1.1f);PowerConfirmed(1,"Przebicia: "+hits+" • maks. 3 cele, 5 po Kroku Cienia",pos);return;
   }
   if(SelectedHero==3)
   {
    QueueClassCast("void",center,4.2f,damage,.85f);PowerConfirmed(1,"osobliwość po 0,85 s • przyciąga zwykłych wrogów",center);return;
   }
   if(SelectedHero==1)
   {
    hits=BloodDetonation(center,4,damage,false);DamageBreakables(center,4,damage*.5f);
    ClassSpellVfx(1,center,4);PowerConfirmed(1,"Detonacja: "+hits+" • do 3 znamion na cel; trafienia podstawowe nakładają znamiona",center);return;
   }
   Vector3 direction=AimDirection(aim);float radius=SelectedHero==4 ? 4.8f : 3.6f;
   if(SelectedHero==0)damage*=ConsumeRiposte();
   foreach(var e in enemies)
   {
    Vector3 delta=e.Position-pos;
    if(e.hp<=0 || delta.magnitude>=radius || Vector3.Dot(direction,delta.normalized)<.15f || !Visible(pos,e.Position))continue;
    hits++;Damage(e,damage);
    if(SelectedHero==0){if(Rune==1)ApplyEnemyStun(e,3);else e.armorUntil=Time.time+5;}
    else {ApplyEnemyRoot(e,Rune==1 ? 4 : 2);if(Rune==0 && hits<=4)Heal(5);}
   }
   StrikeBreakables(pos,direction,radius,damage);ClassSpellVfx(1,pos+direction*1.3f,radius*.65f);
   result="Trafienia w stożku: "+hits+(SelectedHero==0 ? Rune==1 ? " • ogłuszenie; elity mają odporność" : " • osłabienie pancerza 5 s" : " • korzenie "+(Rune==1 ? 4 : 2)+" s; wrogowie nadal atakują");
   PowerConfirmed(1,result,pos);
  }
  void StartDefense()
  {
   if(!BeginPower(2))return;EnsureClassMechanics();
   if(SelectedHero==2 && Status.Count("slaughter",Time.time)==0)Shadow-=20;
   float healthBefore=Health;
   defenseAt=Time.time+Cooldown(10);
   CancelMeleeActions();player.Play("Defense_A",false,.45f);
   if(SelectedHero==0){Status.Apply("armor","Żelazna Pokuta",Time.time,5);Status.Apply("counter","Okno kontry",Time.time,1.25f);}
   if(SelectedHero==1){Status.Remove("ichor");Heal(25);Status.Apply("drain","Transfuzja",Time.time,5);}
   if(SelectedHero==2){Status.Apply("shadow","Krok Cienia",Time.time,2);Status.Apply("precision","Celny powrót",Time.time,5);}
   if(SelectedHero==3){Status.Apply("armor","Płaszcz Próżni",Time.time,3);Status.Apply("voidshell","Pochłonięta energia",Time.time,3);voidShellUntil=Time.time+3;voidStored=0;}
   if(SelectedHero==4){Status.Apply("armor","Kora Przodków",Time.time,6);Heal(15);CreateRootField(HeroPosition,2.5f,3);}
   ClassSpellVfx(2,HeroPosition,1.35f);Sound("ward"+SelectedHero);
   PowerConfirmed(2,SkillActiveSummary(2)+" • "+SkillActiveRemaining(2).ToString("0")+" s"+(Health>healthBefore ? " • +"+Mathf.RoundToInt(Health-healthBefore)+" HP" : ""),HeroPosition);
  }
  void UpdateEnemy(Enemy e)
  {
   if(e.hp<=0){TickDeadEnemy(e,Time.time);return;}
   if(Testing && reviewCaptureFreezeEnemies && e!=reviewCaptureActiveGuardian){e.visual.Tick(false);return;}
   if(UpdateSpecialEnemy(e))return;
   Vector3 pos=e.Position,goal=player.Root.transform.position;float range=Vector3.Distance(pos,goal),reach=e.boss ? 2.5f : 1.5f;
   if(Time.time<e.stunUntil){if(e.impactAt>0)e.visual.Play("Hit_A",false,.16f);CancelEnemyMelee(e);e.warning.SetActive(false);e.impactAt=0;e.visual.Tick(false);return;}
   if(EnemyMeleeRecovering(e,Time.time)){e.visual.Tick(false);return;}
   if(range<10 && Visible(pos,goal))e.alerted=true;
   if(!e.alerted){e.visual.Tick(false);return;}
   if((e.elite || e.boss) && Time.time>=e.spellAt)
   {e.spellAt=Time.time+(e.boss ? 5 : 8);AddHazard(goal,e.boss ? 2.3f : 1.8f,4,true,new Color(.7f,.2f,1),"ichor");}
   if(e.impactAt>0)
   {
    PaintEnemyTelegraph(e,reach,e.boss ? .9f : .65f);
    if(AdvanceEnemyMelee(e,Time.time))
    {
     e.impactAt=0;e.warning.SetActive(false);
     if(range<reach+.3f && Vector3.Dot(e.visual.Root.transform.forward,(goal-pos).normalized)>.2f && Visible(pos,goal))
     {bool landed=Hurt(e.boss ? 25 : e.zombie ? 9 : 12);if(landed && e.zombie)ApplyIchor();}
    }
    return;
   }
   if(range<=reach && Visible(pos,goal))
   {
    if(Time.time>=e.attackAt)
    {e.attackAt=Time.time+(e.boss ? 2.2f : 1.7f);e.impactAt=Time.time+(e.boss ? .9f : .65f);BeginEnemyMelee(e,goal);e.warning.SetActive(true);}
    e.visual.Tick(false);return;
   }
   Vector3 direction=RouteDirection(grid,pos,goal);
   foreach(var other in enemies)if(other!=e && other.hp>0)
   {Vector3 separation=pos-other.Position;if(separation.sqrMagnitude>.001f && separation.sqrMagnitude<1)direction+=separation.normalized*.5f;}
   direction=direction.normalized;
   Move(e.visual.Root.transform,direction*(e.zombie ? 2 : 2.6f)*(Time.time<e.slowUntil ? .4f : 1)*Time.deltaTime,.3f);
   Vector3 travelled=e.Position-pos;FaceTravel(e.visual,travelled,Time.deltaTime);e.visual.Tick(travelled.sqrMagnitude>.0000001f);
  }
  void Damage(Enemy e,float amount)
  {
   if(e.hp<=0)return;
   MusicContact();
   amount*=PowerDamageMultiplier*RelicDamageMultiplier(e)*TalentDamageMultiplier(e);
   if(Time.time<e.armorUntil)amount*=1.25f;
   if(e.archetype==6 && Time.time>=e.stunUntil && Vector3.Dot(e.visual.Root.transform.forward,(HeroPosition-e.Position).normalized)>.4f)amount*=.25f;
   float dealt=Mathf.Min(e.hp,amount);e.alerted=true;e.hp=Mathf.Max(0,e.hp-amount);
   ClassImpactVfx(e.Position,amount>AttackPower*1.8f);
   Floating(e.Position,Mathf.RoundToInt(amount).ToString(),new Color(1,.85f,.55f));SoundAt(EnemyHitCue(e),e.Position);
   if(e.hp>0 && e.impactAt<=0)e.visual.React("Hit_A",.13f);
   if(Status.Count("drain",Time.time)>0)Heal(dealt*.15f);
   if(e.hp>0)return;
   BeginEnemyDeath(e);
   // A priest can revive a guard, but the same spawn only grants its rewards once.
   if(e.rewarded)return;
   e.rewarded=true;
   if((e.boss || IsStageGuardian(e)) && musicSystem)musicSystem.Victory();
   RecordKill(e);RecallLootForEnemy(e);
   Defeated++;Experience+=e.boss ? 100 : e.elite ? 45 : 20;GainResource(SelectedHero==1 ? 25 : 8);OnRelicEnemyKilled(e);
   Drop(e.Position,e.boss ? 18 : e.elite ? 8 : 3,false);
   if(e.elite || e.boss || Defeated%4==0)DropRelic(e.Position,e.boss ? 3 : e.elite ? 2 : 1);
   killStreak=Time.time<=graceAt ? killStreak+1 : 1;graceAt=Time.time+8;
   if(killStreak%3==0)Drop(e.Position,1,true);
   while(Experience>=Level*60)
   {Experience-=Level*60;Level++;Health=MaxHealth;Sound("levelup");ShowNotice("Poziom "+Level+" • punkt talentu [V] • odnowione zdrowie");Sparks(player.Root.transform.position,Hero.color,20);}
   if(Remaining==0)ShowNotice(Zone==Campaign.Heart ? "Malazar upadł • odzyskaj odłamek serca [E]" : "Szlak oczyszczony • złota pieczęć wskazuje przejście");
  }
  bool Hurt(float amount)
  {
   if(State!=Phase.Playing || Time.time<dashEnd || Time.time<hurtUntil || Status.Count("shadow",Time.time)>0)return false;
   if(Status.Count("armor",Time.time)>0)amount*=.45f;
   amount*=1-Mathf.Min(.45f,ArmorRank*.06f);
   if(Status.Count("ward",Time.time)>0)amount*=.7f;
   amount=ApplyRelicProtection(ApplyClassProtection(amount))*TalentTakenMultiplier;CancelRecall(true);
   MusicContact();hurtUntil=Time.time+.35f;Health=Mathf.Max(0,Health-amount);if(SelectedHero==0)GainResource(8);
   Floating(HeroPosition,"−"+Mathf.RoundToInt(amount),new Color(1,.25f,.28f));Sound("hurt");
   Burst(player.Root.transform.position,.7f,art.redMaterial);CombatImpactCamera(.06f);
   if(Health<=0){OnRecallHeroDied();CancelCombatAction();State=Phase.Dead;Time.timeScale=0;player.Play("Death_A",false,9999);}
   else player.React("Hit_A",.18f);
   return true;
  }
  void Heal(float amount)
  {
   float before=Health;Health=Mathf.Min(MaxHealth,Health+amount*Mathf.Max(0,1-.1f*Status.Count("ichor",Time.time)));
   if(player!=null && Health-before>=.5f && Time.time>=healFeedbackAt)
   {healFeedbackAt=Time.time+.15f;Floating(HeroPosition,"+"+Mathf.RoundToInt(Health-before)+" HP",new Color(.52f,1,.60f));}
  }
  void ApplyIchor()
  {
   Status.Apply("ichor","Skaza Ichoritu",Time.time,10,10,true);
   if(Status.Count("ichor",Time.time)>=10)
   {CancelRecall(true);Status.Remove("ichor");Health=Mathf.Max(1,Health-25);Burst(player.Root.transform.position,3,art.violetMaterial);ShowNotice("Eksplozja Ichoritu • skaza rozproszona");}
  }
 }
}
