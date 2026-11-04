using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Krypta.Editor
{
 public static class NocturnisClassMechanicsQA
 {
  const string Session="Nocturnis.ClassMechanicsQA";const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
  static double started,next;static int stage,checks;static string runtimeError="";
  static KryptaGame.Enemy rootTarget;static float rootRecoveryAt;
  [InitializeOnLoadMethod]static void Restore()
  {if(!SessionState.GetBool(Session,false))return;started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
  public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool(Session,true);EditorApplication.isPlaying=true;}
  static void Log(string m,string s,LogType t){if(t==LogType.Error || t==LogType.Exception || t==LogType.Assert)runtimeError=m+"\n"+s;}
  static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
  static void Near(float actual,float expected,string label){Check(Mathf.Abs(actual-expected)<.06f,label+" expected="+expected+" actual="+actual);}
  static object Call(KryptaGame g,string method,params object[] args){return typeof(KryptaGame).GetMethod(method,P).Invoke(g,args);}
  static object Get(KryptaGame g,string name){return typeof(KryptaGame).GetField(name,P).GetValue(g);}
  static void Field(KryptaGame g,string name,object value){typeof(KryptaGame).GetField(name,P).SetValue(g,value);}
  static void Set(KryptaGame g,string name,object value){typeof(KryptaGame).GetProperty(name).SetValue(g,value);}
  static ActorVisual Player(KryptaGame g){return (ActorVisual)Get(g,"player");}
  static KryptaGame.Enemy[] Fixture(KryptaGame g,int hero)
  {
   Set(g,"State",KryptaGame.Phase.Title);g.SelectHero(hero);Call(g,"ResetCampaign");Call(g,"LoadZone",Campaign.Crypt);
   Set(g,"State",KryptaGame.Phase.Playing);Set(g,"Level",5);Set(g,"Health",g.MaxHealth-50);Set(g,"Resource",50f);Set(g,"Shadow",60f);
   Field(g,"introUntil",0f);g.Status.Clear();Call(g,"ResetCombatActions");Time.timeScale=0;
   var grid=(DungeonGrid)Get(g,"grid");for(int x=3;x<=9;x++)for(int z=3;z<=9;z++)grid.OpenCell(x,z);
   Player(g).Root.transform.position=new Vector3(9,0,9);Field(g,"facing",Vector3.forward);
   var targets=new KryptaGame.Enemy[5];for(int i=0;i<g.enemies.Count;i++)
   {
    var e=g.enemies[i];e.hp=i<5 ? 10000 : 0;e.maxHp=10000;e.visual.Root.SetActive(i<5);e.boss=e.elite=e.zombie=false;e.archetype=0;
    e.stunUntil=e.slowUntil=e.impactAt=e.armorUntil=0;e.attackAt=e.spellAt=Time.time+100;e.alerted=true;
    if(i<5){targets[i]=e;e.visual.Root.transform.position=new Vector3(9,0,10.5f+i*1.2f);}
   }
   return targets;
  }
  static void Basic(KryptaGame g,KryptaGame.Enemy e)
  {Field(g,"strikeAt",0f);Call(g,"Strike",e.Position);g.ReviewCompleteStrike();}
  static void Tick()
  {
   if(EditorApplication.timeSinceStartup-started>180){Finish(false,"Timeout");return;}
   if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.2;
   var g=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!g || !g.View)return;
   try
   {
    if(runtimeError.Length>0)throw new Exception(runtimeError);KryptaGame.Testing=true;g.enabled=false;var hud=g.GetComponent<KryptaHUD>();if(hud)hud.enabled=false;Time.timeScale=0;
    if(stage==0)Inquisitor(g);else if(stage==1)Blood(g);else if(stage==2)Hunter(g);else if(stage==3)Void(g);else if(stage==4){if(!Roots(g))return;}else if(stage==5)Control(g);else{Finish(true,"");return;}stage++;
   }
   catch(Exception error){Finish(false,error.ToString());}
  }
  static void Inquisitor(KryptaGame g)
  {
   var e=Fixture(g,0)[0];{g.CastDefense();g.ReviewCompletePower();}Check(g.Status.Count("counter",Time.time)>0,"R opens counter window");
   float hp=g.Health;Call(g,"Hurt",20f);Near(hp-g.Health,20*.45f*.15f,"Parry mitigates one hit");
   Check(g.Status.Count("counter",Time.time)==0 && g.Status.Count("riposte",Time.time)>0,"Parry becomes one consumable riposte");
   float enemyHP=e.hp;Basic(g,e);Near(enemyHP-e.hp,g.AttackPower*1.75f,"Riposte damage is applied at actual weapon contact");
   enemyHP=e.hp;Basic(g,e);Near(enemyHP-e.hp,g.AttackPower,"Riposte cannot apply twice");
   Call(g,"ResetCombatActions");g.Status.Clear();float speed=g.PlayerMovementSpeed;Field(g,"strikeAt",0f);Call(g,"Strike",e.Position);
   Near(g.PlayerMovementSpeed,0,"Human and bot plant through weapon contact");Vector3 before=g.HeroPosition;
   Call(g,"MovePlayer",Vector3.right,.1f,float.PositiveInfinity);Near(Vector3.Distance(before,g.HeroPosition),0,"An attack cannot skate sideways before contact");
  }
  static void Blood(KryptaGame g)
  {
   var e=Fixture(g,1)[0];Basic(g,e);Basic(g,e);Basic(g,e);Basic(g,e);Check(g.BloodMarkCount(e)==3,"Blood marks cap at three");
   float hp=e.hp,baseDamage=48+g.Level*6+g.CharmRank*7;{g.CastSkill(e.Position);g.ReviewCompletePower();}Near(hp-e.hp,baseDamage*2.05f,"Q detonates three marks");Check(g.BloodMarkCount(e)==0,"Detonation consumes marks");
   Field(g,"pulseAt",0f);Set(g,"Resource",100f);hp=e.hp;{g.CastSkill(e.Position);g.ReviewCompletePower();}Near(hp-e.hp,baseDamage*.32f,"Unprepared Q is weaker");
   Basic(g,e);Call(g,"ResetClassMechanics");Check(g.BloodMarkCount(e)==0,"Reset clears marks and glyphs");
  }
  static void Hunter(KryptaGame g)
  {
   var e=Fixture(g,2);{g.CastSkill(e[4].Position);g.ReviewCompletePower();}Check(e[0].hp<10000 && e[1].hp<10000 && e[2].hp<10000,"Arrow pierces three aligned targets");Near(e[3].hp,10000,"Fourth target exceeds unbuffed pierce limit");
   foreach(var target in e)target.hp=10000;Field(g,"pulseAt",0f);Set(g,"Resource",100f);{g.CastDefense();g.ReviewCompletePower();}{g.CastSkill(e[4].Position);g.ReviewCompletePower();}
   Check(e[4].hp<10000,"Leaving shadow adds two pierces");Check(g.Status.Count("precision",Time.time)==0,"Precision is consumed once");
   foreach(var target in e)target.hp=10000;Field(g,"pulseAt",0f);Set(g,"Resource",100f);e[2].visual.Root.transform.position+=Vector3.right*2;
   {g.CastSkill(e[4].Position);g.ReviewCompletePower();}Near(e[2].hp,10000,"Off-line target is not hit by a circular fallback");
  }
  static void Void(KryptaGame g)
  {
   var e=Fixture(g,3)[0];{g.CastSkill(e.Position);g.ReviewCompletePower();}Check(g.PendingClassCasts==1,"Q creates one visible delayed singularity");Near(e.hp,10000,"No immediate singularity damage");
   Call(g,"TickClassMechanics",Time.time+.4f);Near(e.hp,10000,"No damage before delay");
   Set(g,"State",KryptaGame.Phase.Paused);Call(g,"TickClassMechanics",Time.time+1);Near(e.hp,10000,"Pause cannot resolve queued damage");
   Set(g,"State",KryptaGame.Phase.Playing);Call(g,"TickClassMechanics",Time.time+.86f);Check(e.hp<10000 && g.PendingClassCasts==0,"Singularity resolves at delay");
   float hp=e.hp;Call(g,"TickClassMechanics",Time.time+2);Near(e.hp,hp,"Delayed cast resolves only once");
   Field(g,"pulseAt",0f);{g.CastSkill(e.Position);g.ReviewCompletePower();}Call(g,"ResetCombatActions");Check(g.PendingClassCasts==0,"New session or checkpoint cancels delayed damage");
  }
  static bool Roots(KryptaGame g)
  {
   if(rootTarget==null)
   {
    rootTarget=Fixture(g,4)[0];var target=rootTarget;
    target.visual.Root.transform.position=g.HeroPosition+Vector3.forward*1.2f;{g.CastSkill(target.Position);g.ReviewCompletePower();}
    Check(g.EnemyRootRemaining(target)>1.9f,"Q roots ordinary enemy");Check(target.stunUntil<=Time.time,"Root is not a stun");
    Vector3 original=target.Position;Call(g,"Move",target.visual.Root.transform,Vector3.right*.5f,.3f);Near(Vector3.Distance(original,target.Position),0,"Roots prevent actual movement");
    target.attackAt=Time.time-1;Call(g,"UpdateEnemy",target);Check(target.impactAt>Time.time,"Rooted enemy can still start a melee attack");
    Check(Player(g).MovementCommitted,"Root spell has a brief casting commitment");
    original=g.HeroPosition;Call(g,"MovePlayer",Vector3.right,.1f,float.PositiveInfinity);
    Near(Vector3.Distance(original,g.HeroPosition),0,"Casting commitment plants the player before recovery");
    // Advance the real scaled animation clock with the game simulation disabled.
    // The root lasts 2 s; movement must return well before the enemy is released.
    rootRecoveryAt=Time.time+.32f;Time.timeScale=1;return false;
   }
   if(Time.time<rootRecoveryAt){Time.timeScale=1;return false;}
   var e=rootTarget;
   Check(!Player(g).MovementCommitted,"Casting commitment expires on the real runtime clock");
   Check(g.EnemyRootRemaining(e)>0,"Enemy root is still active when checking player freedom");
   Vector3 before=g.HeroPosition;Call(g,"MovePlayer",Vector3.right,.1f,float.PositiveInfinity);
   Check(Vector3.Distance(before,g.HeroPosition)>.4f,"Roots do not trap the player after cast recovery");
   before=e.Position;Call(g,"Move",e.visual.Root.transform,Vector3.right*.5f,.3f);
   Near(Vector3.Distance(before,e.Position),0,"Enemy remains physically rooted while the player moves");
   e.boss=true;Call(g,"ResetClassMechanics");Check(!(bool)Call(g,"ApplyEnemyRoot",e,4f),"Boss root becomes slow, not immobilization");Check(g.EnemyRootRemaining(e)==0,"Boss charge is not frozen by roots");
   rootTarget=null;return true;
  }
  static void Control(KryptaGame g)
  {
   var e=Fixture(g,0)[0];e.elite=true;int accepted=0;float hardSeconds=0;
   for(int i=0;i<40;i++)
   {
    float now=10+i*1.25f;bool success=(bool)Call(g,"ApplyEnemyStunAt",e,3f,now);
    if(success){accepted++;float duration=e.stunUntil-now;Near(duration,.75f,"Elite stun duration cap");hardSeconds+=duration;}
   }
   Check(accepted<10 && hardSeconds<7,"Repeated Pylon Q cannot chain-lock an elite over fifty seconds");
   Call(g,"ResetClassMechanics");e.elite=false;e.boss=true;Check((bool)Call(g,"ApplyEnemyStunAt",e,4f,Time.time),"Boss can be interrupted once");
   Check(!(bool)Call(g,"ApplyEnemyStunAt",e,4f,Time.time+1.25f),"Boss resists second Pylon stun");
   Call(g,"ResetClassMechanics");e.boss=false;e.stunUntil=0;Check((bool)Call(g,"ApplyEnemyStunAt",e,3f,Time.time),"Ordinary enemy retains full control duration");Near(e.stunUntil-Time.time,3,"Ordinary stun duration preserved");
  }
  static void Finish(bool passed,string error)
  {
   SessionState.SetBool(Session,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Directory.CreateDirectory("Logs");
   string result=(passed ? "PASS: " : "FAIL: ")+checks+" assertions; five distinct class mechanics, weapon contact, shared movement, roots vs attacks, delayed casts and boss CC immunity. "+error;
   File.WriteAllText("Logs/class-mechanics-result.txt",result);if(passed)Debug.Log(result);else Debug.LogError(result);Time.timeScale=1;EditorApplication.Exit(passed?0:1);
  }
 }
}
