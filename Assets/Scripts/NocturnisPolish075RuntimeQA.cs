using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace Krypta {
 public sealed class NocturnisPolish075RuntimeQA:MonoBehaviour {
  [Serializable]class Cue{public string name;public float seconds,peak;public bool streaming,playing;}
  [Serializable]class Report{public bool passed,preferencesPreserved,combatIndependentOfSfx,bossPriority,grace,returnsToCalm,pauseRetainsCue,pauseGripStable,volumeZero;public string error;public List<Cue> cues=new List<Cue>();}
  const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
  static bool Requested{get{return Array.IndexOf(Environment.GetCommandLineArgs(),"-polish075-review")>=0;}}
  static string Arg(string key){var a=Environment.GetCommandLineArgs();int n=Array.IndexOf(a,key);return n>=0&&n+1<a.Length?a[n+1]:"Logs/Polish075Runtime";}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Isolate(){if(Requested)KryptaGame.Testing=true;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Launch(){if(Requested)new GameObject("Polish 075 runtime QA").AddComponent<NocturnisPolish075RuntimeQA>();}
  static void Set(KryptaGame g,string name,object value){typeof(KryptaGame).GetProperty(name).SetValue(g,value);}
  static object Call(KryptaGame g,string name,params object[] args){return typeof(KryptaGame).GetMethod(name,P).Invoke(g,args);}
  Report result=new Report();string folder,errors="";
  void Log(string m,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors+=m+"\n"+stack+"\n";}
  IEnumerator Start(){
   folder=Arg("-polish075-output");Directory.CreateDirectory(folder);Application.logMessageReceived+=Log;
   float until=Time.realtimeSinceStartup+60;KryptaGame game=null;NocturnisMusic music=null;
   while(Time.realtimeSinceStartup<until){game=FindFirstObjectByType<KryptaGame>();music=game?game.GetComponent<NocturnisMusic>():null;if(music&&game.View)break;yield return null;}
   if(!music){Finish("Missing music system");yield break;}
   game.enabled=false;var sfx=game.GetComponent<NocturnisAudio>();sfx.enabled=false;sfx.ReviewStop();sfx.ReviewMute(false);sfx.ReviewLevels(0,0);
   float preference=PlayerPrefs.GetFloat("Nocturnis.MusicVolume",.65f);music.SetVolume(.75f,false);music.enabled=false;
   var next=typeof(NocturnisMusic).GetField("next",P);var current=typeof(NocturnisMusic).GetField("current",P);
   foreach(string cue in NocturnisMusic.Required){
    ((AudioSource)current.GetValue(music)).Stop();((AudioSource)next.GetValue(music)).Stop();
    typeof(NocturnisMusic).GetMethod("Change",P).Invoke(music,new object[]{cue});
    var source=(AudioSource)next.GetValue(music);source.volume=.5f;source.mute=false;
    var entry=new Cue{name=cue,seconds=source.clip.length,streaming=source.clip.loadType==AudioClipLoadType.Streaming};result.cues.Add(entry);
    yield return new WaitForSecondsRealtime(.35f);
    var samples=new float[2048];
    for(int i=0;i<12;i++){source.GetOutputData(samples,0);foreach(float sample in samples)entry.peak=Mathf.Max(entry.peak,Mathf.Abs(sample));entry.playing|=source.isPlaying;yield return new WaitForSecondsRealtime(.05f);}
   }
   music.enabled=true;Set(game,"State",KryptaGame.Phase.Playing);Call(game,"LoadZone",Campaign.Crypt);Set(game,"Health",game.MaxHealth);Time.timeScale=1;
   foreach(var enemy in game.enemies){enemy.hp=0;enemy.alerted=false;enemy.visual.Root.SetActive(false);enemy.warning.SetActive(false);}
   yield return new WaitForSecondsRealtime(3);
   var target=game.enemies[0];target.hp=10000;target.boss=false;target.elite=false;target.archetype=0;target.alerted=true;target.visual.Root.SetActive(true);target.visual.Root.transform.position=game.HeroPosition+Vector3.forward*1.5f;
   yield return new WaitForSecondsRealtime(1.2f);result.combatIndependentOfSfx=music.CurrentCue=="combat"&&music.IsAudible;
   ScreenCapture.CaptureScreenshot(Path.Combine(folder,"battle-music.png"));
   target.boss=true;yield return new WaitForSecondsRealtime(1.2f);result.bossPriority=music.CurrentCue=="boss";
   target.hp=0;yield return new WaitForSecondsRealtime(2);result.grace=music.CurrentCue=="boss";
   yield return new WaitForSecondsRealtime(7.5f);result.returnsToCalm=music.CurrentCue=="crypt";
   game.Pause();string held=music.CurrentCue;yield return new WaitForSecondsRealtime(.6f);result.pauseRetainsCue=music.CurrentCue==held;
   var actor=(ActorVisual)typeof(KryptaGame).GetField("player",P).GetValue(game);
   var fitting=actor.Root.GetComponentInChildren<NocturnisWeaponGrip>();
   Quaternion pausedRotation=fitting.transform.rotation;Vector3 pausedPosition=fitting.transform.position;
   yield return new WaitForSecondsRealtime(.4f);
   result.pauseGripStable=Quaternion.Angle(pausedRotation,fitting.transform.rotation)<.1f&&Vector3.Distance(pausedPosition,fitting.transform.position)<.001f;
   var hud=game.GetComponent<KryptaHUD>();var enumType=typeof(KryptaHUD).GetNestedType("MenuPage",BindingFlags.NonPublic);
   if(enumType!=null){typeof(KryptaHUD).GetMethod("OpenMenuPage",P)?.Invoke(hud,new[]{Enum.Parse(enumType,"Settings")});yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"music-settings.png"));}
   music.SetVolume(0,false);yield return new WaitForSecondsRealtime(.2f);result.volumeZero=!music.IsAudible;
   result.preferencesPreserved=PlayerPrefs.GetFloat("Nocturnis.MusicVolume",.65f)==preference;
   music.SetVolume(preference,false);yield return new WaitForSecondsRealtime(.2f);
   result.passed=result.cues.Count==10&&result.cues.TrueForAll(c=>c.playing&&c.peak>.00001f)&&result.combatIndependentOfSfx&&result.bossPriority&&result.grace&&result.returnsToCalm&&result.pauseRetainsCue&&result.pauseGripStable&&result.volumeZero&&result.preferencesPreserved&&errors.Length==0;
   Finish(errors);
  }
  void Finish(string error){result.error=error;if(error.Length>0)result.passed=false;File.WriteAllText(Path.Combine(folder,"results.json"),JsonUtility.ToJson(result,true));Application.logMessageReceived-=Log;Debug.Log("POLISH075_QA_COMPLETE "+result.passed);Application.Quit(result.passed?0:1);}
 }
}
