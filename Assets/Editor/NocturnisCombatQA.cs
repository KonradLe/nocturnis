using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Krypta.Editor
{
 // Exercises the real player, enemies and public casts in Play Mode. A frozen simulation
 // clock isolates each cast; expiry is advanced explicitly rather than waiting for timers.
 public static class NocturnisCombatQA
 {
  const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  const string Session="Nocturnis.CombatQA";
  static int step,checks;
  static double started,next;
  static string runtimeError="";
  static readonly List<HeroReport> heroes=new List<HeroReport>();
  [Serializable] sealed class HeroReport {public int hero,checks;public string name;}
  [Serializable] sealed class Report {public bool passed;public int assertions;public string error;public HeroReport[] heroes;}
  [InitializeOnLoadMethod] static void Restore()
  {
   if(!SessionState.GetBool(Session,false))return;
   started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
  }
  public static void Run()
  {
   EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");
   SessionState.SetBool(Session,true);EditorApplication.isPlaying=true;
  }
  static void Log(string message,string stack,LogType type)
  {if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert)runtimeError=message+"\n"+stack;}
  static void Check(bool condition,string name){checks++;if(!condition)throw new Exception("Hero "+step+": "+name);}
  static void Near(float actual,float expected,string name)
  {Check(Mathf.Abs(actual-expected)<.035f,name+"; expected="+expected+", actual="+actual);}
  static object Call(KryptaGame game,string method,params object[] args)
  {return typeof(KryptaGame).GetMethod(method,Private).Invoke(game,args);}
  static object Get(KryptaGame game,string field){return typeof(KryptaGame).GetField(field,Private).GetValue(game);}
  static void Field(KryptaGame game,string field,object value){typeof(KryptaGame).GetField(field,Private).SetValue(game,value);}
  static void Set(KryptaGame game,string property,object value){typeof(KryptaGame).GetProperty(property).SetValue(game,value);}
  static IList Hazards(KryptaGame game){return (IList)Get(game,"hazards");}
  static void HazardField(object hazard,string field,object value){hazard.GetType().GetField(field).SetValue(hazard,value);}
  static void ClearHazards(KryptaGame game)
  {
   foreach(object h in Hazards(game))UnityEngine.Object.Destroy((GameObject)h.GetType().GetField("ring").GetValue(h));
   Hazards(game).Clear();
  }
  static void Expire(KryptaGame game,string id)
  {foreach(var status in game.Status.Entries)if(status.id==id)status.until=Time.time-.01f;game.Status.Expire(Time.time);}
  static void Place(KryptaGame game,Vector3 pos)
  {((ActorVisual)Get(game,"player")).Root.transform.position=pos;}
  static void ResetTarget(KryptaGame.Enemy enemy)
  {enemy.hp=enemy.maxHp=10000;enemy.armorUntil=enemy.stunUntil=enemy.slowUntil=enemy.impactAt=0;enemy.boss=enemy.elite=false;enemy.archetype=0;enemy.visual.Root.SetActive(true);}
  static void Reset(KryptaGame game,KryptaGame.Enemy enemy)
  {
   game.Status.Clear();Call(game,"ResetCombatActions");ClearHazards(game);game.combatText.Clear();
   Set(game,"State",KryptaGame.Phase.Playing);Set(game,"Level",5);Set(game,"Experience",0);Set(game,"Resource",50f);Set(game,"Shadow",60f);Set(game,"Health",game.MaxHealth-90);
   game.Rune=0;Field(game,"introUntil",0f);Field(game,"regenAt",Time.time+100);ResetTarget(enemy);
  }
  static void Notice(KryptaGame game,int slot,bool success,string contains)
  {
   Check(game.LastCastSlot==slot && game.CastNoticeSucceeded==success,"Feedback slot/success "+slot);
   Check(game.CastNotice.Contains(contains),"Feedback explains result: "+contains+"; "+game.CastNotice);
   Check(game.CastNoticeUntil>Time.unscaledTime,"Feedback survives paused simulation clock");
  }
  static float Taken(KryptaGame game,float amount)
  {
   Set(game,"Health",game.MaxHealth);Field(game,"hurtUntil",0f);float before=game.Health;Call(game,"Hurt",amount);return before-game.Health;
  }
  static void Tick()
  {
   if(EditorApplication.timeSinceStartup-started>210){Finish(false,"Timeout entering/completing Play Mode");return;}
   if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;
   next=EditorApplication.timeSinceStartup+.2;
   var game=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!game || !game.View)return;
   try
   {
    if(runtimeError.Length>0)throw new Exception(runtimeError);
    KryptaGame.Testing=true;game.enabled=false;var hud=game.GetComponent<KryptaHUD>();if(hud)hud.enabled=false;Time.timeScale=0;
    if(step>=5){StatusBookChecks();Finish(true,"");return;}
    int before=checks;RunHero(game,step);heroes.Add(new HeroReport{hero=step,checks=checks-before,name=game.Hero.name});step++;
   }
   catch(Exception e){Finish(false,e.ToString());}
  }
  static void RunHero(KryptaGame game,int hero)
  {
   Set(game,"State",KryptaGame.Phase.Title);game.SelectHero(hero);Call(game,"ResetCampaign");Call(game,"LoadZone",Campaign.Crypt);
   Set(game,"Level",5);Set(game,"State",KryptaGame.Phase.Playing);Time.timeScale=0;
   var enemy=game.enemies[0];foreach(var other in game.enemies)if(other!=enemy){other.hp=0;other.visual.Root.SetActive(false);}
   Place(game,new Vector3(9,0,9));enemy.visual.Root.transform.position=new Vector3(9,0,10.8f);
   Check(game.MapFloor(5,5) && game.MapFloor(5,6),"Fixture stands on open crypt floor");
   Check((bool)Call(game,"Visible",game.HeroPosition,enemy.Position),"Fixture has line of sight");
   Reset(game,enemy);Basic(game,enemy,hero);
   Reset(game,enemy);Active(game,enemy,hero,0);Reset(game,enemy);Active(game,enemy,hero,1);
   Reset(game,enemy);Defense(game,enemy,hero);
   Reset(game,enemy);Buff(game,enemy,hero);
   for(int extra=1;extra<3;extra++){Reset(game,enemy);Additional(game,enemy,hero,extra);}
   Reset(game,enemy);Rejections(game,enemy,hero);
   Reset(game,enemy);Slaughter(game,enemy,hero);
   if(hero==0 || hero==3){Reset(game,enemy);Mitigation(game);}
   if(hero==2 || hero==3){Reset(game,enemy);DamageBuff(game,enemy,hero);}
   if(hero==1)
   {
    Reset(game,enemy);{game.CastDefense();game.ReviewCompletePower();}{game.CastAdditional(0,enemy.Position);game.ReviewCompletePower();}
    Near(game.SkillActiveRemaining(2),8,"R shares refreshed drain timer");Near(game.SkillActiveRemaining(3),8,"2 shares refreshed drain timer");
    Check(game.Status.Entries.Count==1,"Shared drain does not stack twice");
    Set(game,"Health",game.MaxHealth-90);enemy.hp=10;float hp=game.Health;Call(game,"Damage",enemy,100f);Near(game.Health-hp,1.5f,"Drain heals actual damage, not overkill");
   }
   if(hero==2)
   {
    Reset(game,enemy);{game.CastSkill(new Vector3(-200,0,-200));game.ReviewCompletePower();}Check(Hazards(game).Count==0,"Piercing Q never creates an unrelated circular field");Near(enemy.hp,10000,"Arrow aimed away does not hit a nearby enemy");
    Reset(game,enemy);var fixtureGrid=(DungeonGrid)Get(game,"grid");fixtureGrid.CloseCell(5,6);enemy.visual.Root.transform.position=new Vector3(9,0,12.6f);
    {game.CastSkill(enemy.Position);game.ReviewCompletePower();}Near(enemy.hp,10000,"Piercing Q respects a blocked tile");fixtureGrid.OpenCell(5,6);enemy.visual.Root.transform.position=new Vector3(9,0,10.8f);
   }
   Reset(game,enemy);{game.CastSkill(enemy.Position);game.ReviewCompletePower();}{game.CastDefense();game.ReviewCompletePower();}Set(game,"Resource",100f);{game.CastAdditional(0,enemy.Position);game.ReviewCompletePower();}
   Field(game,"rejectionAt",Time.unscaledTime+100);Field(game,"healFeedbackAt",Time.time+100);Call(game,"ResetCampaign");
   Near(game.PulseReady+game.DefenseReady+game.ExtraReady(0)+game.ExtraReady(1)+game.ExtraReady(2),0,"New run clears all power cooldowns");
   Check((float)Get(game,"rejectionAt")<Time.unscaledTime && (float)Get(game,"healFeedbackAt")<Time.time,"New run resets feedback clocks");
   Check(game.CastNotice.Length==0,"New run clears stale cast feedback");
   if(hero==4)
   {
    Reset(game,enemy);{game.CastSkill(enemy.Position);game.ReviewCompletePower();}{game.CastDefense();game.ReviewCompletePower();}Set(game,"Resource",100f);
    for(int i=0;i<3;i++){game.CastAdditional(i,enemy.Position);game.ReviewCompletePower();}
    Check(game.ExtraReady(0)>0 && game.ExtraReady(1)>0 && game.ExtraReady(2)>0 && game.CastNotice.Length>0,"Checkpoint fixture has active extra cooldowns and feedback");
    Set(game,"Shadow",0f);float savedResource=game.Resource;Call(game,"SaveCheckpoint");Check(game.ContinueCheckpoint(),"Saved checkpoint loads in isolated QA namespace");Time.timeScale=0;
    Near(game.PulseReady+game.DefenseReady+game.ExtraReady(0)+game.ExtraReady(1)+game.ExtraReady(2),0,"Checkpoint load clears all power cooldowns");
    Near(game.Resource,savedResource,"Checkpoint load preserves spent primary resource");Near(game.Shadow,0,"Checkpoint load preserves depleted shadow");
    Check(game.CastNotice.Length==0 && game.LastCastSlot==-1,"Checkpoint load clears stale cast feedback");
    Check(game.Status.Entries.Count==0,"Checkpoint load clears previous active buffs");
   }
  }
  static void Basic(KryptaGame game,KryptaGame.Enemy enemy,int hero)
  {
   float hp=game.Health;Call(game,"Strike",enemy.Position);game.ReviewCompleteStrike();Near(10000-enemy.hp,game.AttackPower,"Basic attack damage");
   Near(game.Resource,hero==3 ? 70 : 60,"Basic attack resource generation");Near(game.Health-hp,hero==1 ? 3 : 0,"Mira basic attack healing");
   Near((float)Get(game,"strikeAt")-Time.time,game.Hero.interval,"Basic attack cooldown");
   float enemyHP=enemy.hp;Call(game,"Strike",enemy.Position);game.ReviewCompleteStrike();Near(enemy.hp,enemyHP,"Basic attack respects its cooldown");
  }
  static void Active(KryptaGame game,KryptaGame.Enemy enemy,int hero,int rune)
  {
   game.Rune=rune;Check(game.CanUseSkill(1),"Q initially ready for HUD");float hp=game.Health;{game.CastSkill(enemy.Position);game.ReviewCompletePower();}
   Check(!game.CanUseSkill(1) && game.SkillBlockReason(1).Contains("Odnowienie"),"HUD matches real Q cooldown rejection");
   Near(game.Resource,hero==3 ? 75 : 25,"Q resource policy");Near(game.PulseReady,5,"Q cooldown");
   Notice(game,1,true,hero==0 || hero==4 ? "Trafienia w stożku: 1" : hero==1 ? "Detonacja: 1" : hero==2 ? "Przebicia: 1" : "osobliwość po 0,85");
   var player=(ActorVisual)Get(game,"player");Check(player.Root.GetComponentInChildren<Animation>()["2H_Melee_Attack_Spin"]!=null,"Q animation exists on actual player");
   if(hero==3){Near(enemy.hp,10000,"Void damage waits for singularity");Check(game.PendingClassCasts==1,"One pending singularity");Call(game,"TickClassMechanics",Time.time+.86f);}
   Near(10000-enemy.hp,hero==1 ? 78*.32f : 78,"Q class-specific damage");
   if(hero==0)Near((rune==0 ? enemy.armorUntil : enemy.stunUntil)-Time.time,rune==0 ? 5 : 3,"Inquisitor rune duration");
   if(hero==1)Near(game.Health,hp,"Unmarked detonation grants no mark healing");
   if(hero==2){Check(Hazards(game).Count==0,"Pierce is not a lingering field");if(rune==1)Near(enemy.slowUntil-Time.time,3,"Wide arrow slows");}
   if(hero==3){Near(enemy.slowUntil-Time.time,2,"Singularity slow");if(rune==1)Near(enemy.armorUntil-Time.time,5,"Void armor rune");}
   if(hero==4){Near(game.EnemyRootRemaining(enemy),rune==0 ? 2 : 4,"Root duration");Check(enemy.stunUntil<=Time.time,"Roots preserve attacks");Near(game.Health-hp,rune==0 ? 5 : 0,"Root rune healing");}
   if(hero==0 && rune==0 || hero==3 && rune==1){float before=enemy.hp;Call(game,"Damage",enemy,100f);Near(before-enemy.hp,125,"Armor break amplifies later damage");}
   float remainingHP=enemy.hp,resource=game.Resource;int sequence=game.CastNoticeSequence;{game.CastSkill(enemy.Position);game.ReviewCompletePower();}
   Notice(game,1,false,"Odnowienie");Check(game.CastNoticeSequence==sequence+1,"Rejected cast produces feedback");Near(enemy.hp,remainingHP,"Cooldown rejection deals no damage");Near(game.Resource,resource,"Cooldown rejection costs nothing");
  }
  static void Defense(KryptaGame game,KryptaGame.Enemy enemy,int hero)
  {
   if(hero==1)for(int i=0;i<3;i++)game.Status.Apply("ichor","Skaza",Time.time,10,10,true);
   float hp=game.Health;{game.CastDefense();game.ReviewCompletePower();}Notice(game,2,true," s");Near(game.DefenseReady,10,"R cooldown");Near(game.Resource,50,"R primary resource unchanged");Near(game.Shadow,hero==2 ? 40 : 60,"R shadow cost");
   Near(game.Health-hp,hero==1 ? 25 : hero==4 ? 15 : 0,"R healing");
   string status=hero==1 ? "drain" : hero==2 ? "shadow" : "armor";float duration=hero==2 ? 2 : hero==3 ? 3 : hero==4 ? 6 : 5;
   Near(game.SkillActiveRemaining(2),duration,"R active timer");Near(game.StatusRemaining(status),duration,"R status duration");
   if(hero==1)Check(game.Status.Count("ichor",Time.time)==0,"Mira removes ichor before healing");
   Near(Taken(game,40),hero==2 ? 0 : hero==0 ? 18*.15f : hero==3 ? 18*.55f : hero==4 ? 18 : 40,"R actual incoming damage");
   Expire(game,status);Expire(game,"counter");Expire(game,"voidshell");Field(game,"voidShellUntil",0f);Near(game.SkillActiveRemaining(2),0,"R expiry clears active timer");Near(Taken(game,40),40,"R expiry restores incoming damage");
   {game.CastDefense();game.ReviewCompletePower();}Notice(game,2,false,"Odnowienie");
  }
  static void Buff(KryptaGame game,KryptaGame.Enemy enemy,int hero)
  {
   {game.CastAdditional(0,enemy.Position);game.ReviewCompletePower();}Notice(game,3,true," s");Near(game.Resource,hero==3 ? 70 : 30,"Buff resource cost");Near(game.ExtraReady(0),14,"Buff cooldown");
   string status=hero==1 ? "drain" : hero==2 ? "fury" : hero==4 ? "regrowth" : "ward";Near(game.SkillActiveRemaining(3),hero==1 ? 8 : 7,"Buff active duration");
   if(hero==0 || hero==3)Near(Taken(game,40),28,"Ward reduces actual incoming damage by 30%");
   if(hero==1){float hp=game.Health;Call(game,"Damage",enemy,100f);Near(game.Health-hp,15,"Drain heals 15% of actual damage");}
   if(hero==2){Near(game.Shadow,90,"Fury restores 30 shadow");Call(game,"Damage",enemy,100f);Near(10000-enemy.hp,130,"Fury damage bonus");}
   if(hero==4)
   {float hp=game.Health;Field(game,"regenAt",Time.time-1);Call(game,"UpdateCampaign");Near(game.Health-hp,6,"Regrowth heals 6 HP on regeneration tick");Call(game,"TickClassMechanics",Time.time);Check(game.ActiveRootFields==1 && game.EnemyRootRemaining(enemy)>0,"Regrowth creates a root field");Check(enemy.stunUntil<=Time.time,"Root field preserves attacks");}
   Expire(game,status);Near(game.SkillActiveRemaining(3),0,"Buff timer expires");
   if(hero==0 || hero==3)Near(Taken(game,40),40,"Ward expiry removes mitigation");
   if(hero==1){float hp=game.Health;Call(game,"Damage",enemy,100f);Near(game.Health,hp,"Drain expiry stops healing");}
   if(hero==2){ResetTarget(enemy);Call(game,"Damage",enemy,100f);Near(10000-enemy.hp,100,"Fury expiry removes bonus");}
   if(hero==4){float hp=game.Health;Field(game,"regenAt",Time.time-1);Call(game,"UpdateCampaign");Near(game.Health,hp,"Regrowth expiry stops extra regeneration");}
  }
  static void Additional(KryptaGame game,KryptaGame.Enemy enemy,int hero,int extra)
  {
   Set(game,"Resource",hero==3 ? 0f : 100f);float hp=game.Health;{game.CastAdditional(extra,enemy.Position);game.ReviewCompletePower();}int cost=extra==1 ? 20 : 50;
   Notice(game,extra+3,true,"");Near(game.Resource,hero==3 ? cost : 100-cost,"Extra resource policy");Near(game.ExtraReady(extra),extra==1 ? 9 : 24,"Extra cooldown");
   if(hero==0 || hero==3 || hero==4){Check(game.PendingClassCasts>0,"Visible delayed extra");Near(enemy.hp,10000,"No premature impact");Call(game,"TickClassMechanics",Time.time+2);}
   Check(enemy.hp<10000,"Class extra resolves damage");
   if(hero==1 && extra==1)Check(game.BloodMarkCount(enemy)==2,"Preparation applies two marks");
   if(hero==4)Check(game.EnemyRootRemaining(enemy)>0 && enemy.stunUntil<=Time.time,"Root without stun");
   if(hero==2)Check(enemy.slowUntil>Time.time,"Hunter hamstring");
   Near(game.Health-hp,hero==4 && extra==2 ? 25 : 0,"Extra healing");Near(game.Shadow,hero==2 ? 75 : 60,"Shadow recovery");
   float remaining=enemy.hp,resource=game.Resource;{game.CastAdditional(extra,enemy.Position);game.ReviewCompletePower();}Notice(game,extra+3,false,"Odnowienie");Near(enemy.hp,remaining,"Extra cooldown respected");Near(game.Resource,resource,"Rejected extra costs nothing");
  }
  static void Rejections(KryptaGame game,KryptaGame.Enemy enemy,int hero)
  {
   if(hero!=3)
   {
    Set(game,"Resource",0f);{game.CastSkill(enemy.Position);game.ReviewCompletePower();}Notice(game,1,false,"Za mało");Near(game.PulseReady,0,"No resource Q has no cooldown");Near(enemy.hp,10000,"No resource Q deals no damage");
    for(int i=0;i<3;i++){{game.CastAdditional(i,enemy.Position);game.ReviewCompletePower();}Notice(game,i+3,false,"Za mało");Near(game.ExtraReady(i),0,"No resource extra has no cooldown");}
   }
   if(hero==2){Set(game,"Shadow",0f);{game.CastDefense();game.ReviewCompletePower();}Notice(game,2,false,"Mrok");Near(game.DefenseReady,0,"No shadow R has no cooldown");Near(game.StatusRemaining("shadow"),0,"No shadow grants no protection");}
   Set(game,"Resource",100f);Set(game,"Level",1);{game.CastAdditional(2,enemy.Position);game.ReviewCompletePower();}Notice(game,5,false,"poziom 4");Near(game.Resource,100,"Locked ultimate consumes nothing");Near(game.ExtraReady(2),0,"Locked ultimate has no cooldown");
   Set(game,"State",KryptaGame.Phase.Paused);{game.CastSkill(enemy.Position);game.ReviewCompletePower();}Notice(game,1,false,"Wznów grę");
   Set(game,"State",KryptaGame.Phase.Playing);Set(game,"Zone",Campaign.Town);{game.CastSkill(enemy.Position);game.ReviewCompletePower();}Notice(game,1,false,"mieście");Set(game,"Zone",Campaign.Crypt);
  }
  static void Slaughter(KryptaGame game,KryptaGame.Enemy enemy,int hero)
  {
   Set(game,"Resource",0f);Set(game,"Shadow",0f);game.Status.Apply("slaughter","Pylon",Time.time,20);{game.CastSkill(enemy.Position);game.ReviewCompletePower();}Near(game.PulseReady,1.25f,"Pylon Q cooldown");Near(game.Resource,0,"Pylon Q costs/generates no resource");
   {game.CastDefense();game.ReviewCompletePower();}Near(game.DefenseReady,2.5f,"Pylon R cooldown");Near(game.Shadow,0,"Pylon removes shadow cost");
   for(int i=0;i<3;i++){{game.CastAdditional(i,enemy.Position);game.ReviewCompletePower();}Near(game.ExtraReady(i),(i==0 ? 14 : i==1 ? 9 : 24)*.25f,"Pylon extra cooldown");Near(game.Resource,0,"Pylon extra costs/generates no resource");Check(game.CastNoticeSucceeded,"Pylon permits zero-resource extra "+i);}
   Check(game.Status.Count("entropy",Time.time)==0,"Free pylon powers do not overflow entropy");
  }
  static void Mitigation(KryptaGame game)
  {
   {game.CastDefense();game.ReviewCompletePower();}{game.CastAdditional(0,game.HeroPosition);game.ReviewCompletePower();}Expire(game,"counter");Field(game,"voidShellUntil",0f);Near(Taken(game,100),31.5f,"Armor and ward multiply once");
   Expire(game,"ward");Near(Taken(game,100),45,"Ward expiry leaves armor only");Expire(game,"armor");Near(Taken(game,100),100,"Both mitigation effects expire");
  }
  static void DamageBuff(KryptaGame game,KryptaGame.Enemy enemy,int hero)
  {
   string status=hero==2 ? "fury" : "entropy";float multiplier=hero==2 ? 1.3f : 1.5f;
   if(hero==2){game.CastAdditional(0,enemy.Position);game.ReviewCompletePower();}else{Set(game,"Resource",80f);Call(game,"GainResource",20f);Near(game.Resource,0,"Entropy overflow resets resource");Near(game.StatusRemaining("entropy"),6,"Entropy duration");}
   ResetTarget(enemy);Call(game,"Strike",enemy.Position);game.ReviewCompleteStrike();Near(10000-enemy.hp,game.AttackPower*multiplier,"Basic attack applies bonus exactly once");
   ResetTarget(enemy);Set(game,"Resource",hero==3 ? 0f : 100f);{game.CastAdditional(1,enemy.Position);game.ReviewCompletePower();}if(hero==3)Call(game,"TickClassMechanics",Time.time+.66f);Near(10000-enemy.hp,60*multiplier,"Additional spell applies bonus exactly once");
   ResetTarget(enemy);Set(game,"Resource",hero==3 ? 0f : 100f);{game.CastSkill(enemy.Position);game.ReviewCompletePower();}
   if(hero==3)Call(game,"TickClassMechanics",Time.time+.86f);Near(10000-enemy.hp,78*multiplier,"Q applies bonus exactly once");
   ResetTarget(enemy);ClearHazards(game);Call(game,"AddHazard",enemy.Position,3f,4f,false,Color.cyan,"thorns");Call(game,"UpdateHazards");Near(10000-enemy.hp,24*multiplier,"Periodic field applies bonus exactly once");
   Expire(game,status);ResetTarget(enemy);Call(game,"Damage",enemy,100f);Near(10000-enemy.hp,100,"Expired damage buff restores baseline");
  }
  static void StatusBookChecks()
  {
   var book=new StatusBook();book.Apply("drain","Transfuzja",10,5);book.Apply("drain","Pieczęć transfuzji",12,8);
   Check(book.Entries.Count==1 && book.Entries[0].label=="Pieczęć transfuzji","Refresh changes label without duplicating shared status");Near(book.Entries[0].until,20,"Refresh extends expiry");Near(book.Entries[0].duration,8,"Refresh exposes actual duration");Near(book.Entries[0].started,12,"Refresh exposes start time");Check(book.Count("drain",20)==0,"Status inactive exactly at expiry");book.Expire(20);Check(book.Entries.Count==0,"Expired status is removed");
  }
  static void Finish(bool passed,string error)
  {
   SessionState.SetBool(Session,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
   if(runtimeError.Length>0){passed=false;error+="\n"+runtimeError;}
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/combat-qa-result.json",JsonUtility.ToJson(new Report{passed=passed,assertions=checks,error=error,heroes=heroes.ToArray()},true));
   Debug.Log((passed ? "PASS" : "FAIL")+" Combat QA: "+checks+" assertions. "+error);EditorApplication.Exit(passed ? 0 : 1);
  }
 }
}
