using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Krypta.Editor {
 public static class NocturnisPolish075QA {
  const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
  const string Session="Nocturnis.Polish075";
  static int stage,checks;static double start,next;static string error="";
  static List<string> evidence=new List<string>();
  [Serializable]class Report{public bool passed;public int assertions;public string error;public string[] evidence;}
  [InitializeOnLoadMethod]static void Restore(){if(!SessionState.GetBool(Session,false))return;start=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
  public static void Run(){NocturnisPolish075Build.MusicImport();EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool(Session,true);EditorApplication.isPlaying=true;}
  static void Log(string m,string s,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)error=m+"\n"+s;}
  static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
  static object Call(KryptaGame g,string n,params object[] args){return typeof(KryptaGame).GetMethod(n,P).Invoke(g,args);}
  static void Field(KryptaGame g,string n,object v){typeof(KryptaGame).GetField(n,P).SetValue(g,v);}
  static void Set(KryptaGame g,string n,object v){typeof(KryptaGame).GetProperty(n).SetValue(g,v);}
  static ActorVisual Player(KryptaGame g){return (ActorVisual)typeof(KryptaGame).GetField("player",P).GetValue(g);}
  static KryptaGame.Enemy Fixture(KryptaGame g,int hero){
   Set(g,"State",KryptaGame.Phase.Title);g.SelectHero(hero);Call(g,"LoadZone",Campaign.Crypt);Set(g,"State",KryptaGame.Phase.Playing);
   Call(g,"ResetCombatActions");g.Status.Clear();Field(g,"introUntil",0f);Set(g,"Health",g.MaxHealth);Set(g,"Level",5);Set(g,"Resource",100f);Set(g,"Shadow",100f);
   Player(g).Root.transform.position=new Vector3(9,0,9);
   var grid=(DungeonGrid)typeof(KryptaGame).GetField("grid",P).GetValue(g);for(int x=3;x<10;x++)for(int z=3;z<10;z++)grid.OpenCell(x,z);
   var e=g.enemies[0];foreach(var other in g.enemies){other.hp=0;other.visual.Root.SetActive(false);}
   e.hp=e.maxHp=10000;e.visual.Root.SetActive(true);e.visual.Root.transform.position=new Vector3(9,0,10.5f);e.boss=e.elite=false;e.archetype=0;e.alerted=true;e.impactAt=e.stunUntil=e.slowUntil=0;
   Time.timeScale=0;return e;
  }
  static void Tick(){
   if(EditorApplication.timeSinceStartup-start>180){Finish(false,"Timeout");return;}
   if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.15;
   var g=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!g||!g.View)return;
   try{
    if(error.Length>0)throw new Exception(error);KryptaGame.Testing=true;g.enabled=false;g.GetComponent<KryptaHUD>().enabled=false;
    if(stage<5)Actions(g,stage);else if(stage==5)Music(g);else{Finish(true,"");return;}stage++;
   }catch(Exception e){Finish(false,e.ToString());}
  }
  static void Actions(KryptaGame g,int hero){
   var e=Fixture(g,hero);g.CastSkill(e.Position);
   Check(g.PowerWindupActive,"Class "+hero+" winds up");
   Check(e.hp==10000 && g.PendingClassCasts==0,"No damage or released spell on button press");
   float paid=g.Resource;g.CastAdditional(1,e.Position);Check(g.Resource==paid && g.ExtraReady(1)==0,"Buffered power costs nothing before execution");
   Call(g,"TickPowerActions",Time.time+.20f);Check(e.hp==10000 && g.PendingClassCasts==0,"No early contact");
   g.ReviewCompletePower();Check(!g.PowerWindupActive,"Power release consumed");
   Check(hero==3?g.PendingClassCasts==1:e.hp<10000,"Effect at animation release");
   float hp=e.hp;Call(g,"TickPowerActions",Time.time+.6f);Check(e.hp==hp,"Release cannot replay");
   e=Fixture(g,hero);g.CastSkill(e.Position);g.CastDefense();g.ReviewCompletePower();
   Check(!g.PowerWindupActive && e.hp==10000 && g.PendingClassCasts==0,"Defense interrupts the unreleased power");
   e=Fixture(g,hero);g.CastSkill(e.Position);Field(g,"defenseAt",Time.time+10);g.CastDefense();
   Check(g.PowerWindupActive,"Unavailable defense cannot cancel a paid power");g.ReviewCompletePower();
   e=Fixture(g,hero);g.CastSkill(e.Position);Call(g,"BeginPlayerDodge",Vector3.right);g.ReviewCompletePower();
   Check(e.hp==10000 && !g.PowerWindupActive,"Dodge cancels unreleased damage");
   e=Fixture(g,hero);g.CastSkill(e.Position);g.Pause();Call(g,"TickPowerActions",Time.time+1);
   Check(e.hp==10000 && g.PowerWindupActive,"Pause retains but does not release");g.Resume();Time.timeScale=0;g.ReviewCompletePower();
   Check(!g.PowerWindupActive,"Resume releases only once");
   e=Fixture(g,hero);g.CastSkill(e.Position);Call(g,"LoadZone",Campaign.Crypt);Check(!g.PowerWindupActive && !g.CombatActionBusy,"Map load clears attack and buffer");
   evidence.Add("Class "+hero+": delayed release, one damage, free buffer, valid/invalid defense, dodge, pause, zone cancellation.");
  }
  static void Music(KryptaGame g){
   var e=Fixture(g,0);e.alerted=false;
   var music=g.GetComponent<NocturnisMusic>();if(!music)music=g.gameObject.AddComponent<NocturnisMusic>();music.enabled=false;
   Check(music.AssetsComplete(),"All ten music cues exist");
   for(int z=0;z<Campaign.Count;z++){string cue=NocturnisMusic.EnvironmentCue(z);Check(Resources.Load<AudioClip>("NocturnisMusic/"+cue),"Map "+z+" has a score");}
   Set(g,"State",KryptaGame.Phase.Title);music.ReviewTick(100,3);Check(music.CurrentCue=="menu","Menu music");
   Set(g,"State",KryptaGame.Phase.Playing);music.ReviewTick(101,3);Check(music.CurrentCue=="crypt","Crypt ambience");
   e.alerted=true;music.ReviewTick(102,1);Check(music.CurrentCue=="combat","Engagement starts combat music independently of SFX");
   e.boss=true;music.ReviewTick(103,1);Check(music.CurrentCue=="boss","Boss overrides combat");
   e.hp=0;music.ReviewTick(110,1);Check(music.CurrentCue=="boss","Grace avoids abrupt stop after kill");
   music.ReviewTick(113,3);Check(music.CurrentCue=="crypt","Calm returns after grace");
   Set(g,"State",KryptaGame.Phase.Paused);int transitions=music.Transitions;music.ReviewTick(114,1);
   Check(music.CurrentCue=="crypt" && transitions==music.Transitions,"Pause retains current cue");
   Set(g,"State",KryptaGame.Phase.Dead);music.ReviewTick(115,3);Check(music.CurrentCue=="death","Death cue");
   Set(g,"State",KryptaGame.Phase.Won);music.ReviewTick(116,3);Check(music.CurrentCue=="finale","Finale cue");
   music.SetVolume(0,false);music.ReviewTick(117,3);Check(!music.IsAudible,"Music volume zero is silent");
   evidence.Add("Ten score files, all 19 maps, menu/exploration/combat/boss/death/finale, grace, pause and independent gain.");
  }
  static void Finish(bool passed,string message){
   SessionState.SetBool(Session,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/polish075-qa.json",JsonUtility.ToJson(new Report{passed=passed,assertions=checks,error=message,evidence=evidence.ToArray()},true));
   if(!passed)Debug.LogError(message);Time.timeScale=1;EditorApplication.Exit(passed?0:1);
  }
 }
}
