using System.Collections.Generic;
using UnityEngine;
namespace Krypta {
 // Separate music clock and sources: SFX mute/pooling never determines battle state.
 public sealed class NocturnisMusic : MonoBehaviour {
  readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
  AudioSource current,next,accent;KryptaGame game;
  string selected="";int zone=-1;float blend=1,fromGain,duck=1,lastThreat=-100,lastBoss=-100;
  public string CurrentCue {get{return selected;}}
  public float Volume {get;private set;}
  public static readonly string[] Required={"menu","town","wilderness","crypt","ritual","combat","boss","finale","death","victory"};
  public int Transitions {get;private set;}
  public bool IsAudible {get{return (current && current.isPlaying && current.volume>.0001f)||(next && next.isPlaying && next.volume>.0001f);}}
  void Awake(){
   game=GetComponent<KryptaGame>();Volume=Mathf.Clamp01(PlayerPrefs.GetFloat("Nocturnis.MusicVolume",.65f));
   current=Source("Music A");next=Source("Music B");accent=Source("Music accent");
  }
  AudioSource Source(string name){var s=gameObject.AddComponent<AudioSource>();s.playOnAwake=false;s.spatialBlend=0;s.priority=180;s.dopplerLevel=0;s.volume=0;return s;}
  AudioClip Clip(string name){
   AudioClip clip;if(clips.TryGetValue(name,out clip))return clip;
   clip=Resources.Load<AudioClip>("NocturnisMusic/"+name);clips[name]=clip;
   if(!clip)Debug.LogError("MUSIC_MISSING "+name);
   return clip;
  }
  public void SetVolume(float value,bool save=true){
   Volume=Mathf.Clamp01(value);if(save){PlayerPrefs.SetFloat("Nocturnis.MusicVolume",Volume);PlayerPrefs.Save();}
  }
  public static string EnvironmentCue(int location){
   if(location==Campaign.Town)return "town";
   if(location==Campaign.Forge||location==Campaign.Threshold||location==Campaign.Heart)return "ritual";
   return Campaign.Outdoors(location) ? "wilderness" : "crypt";
  }
  void Update(){Tick(Time.time,Time.unscaledDeltaTime);}
  public void ReviewTick(float now,float delta){if(KryptaGame.Testing)Tick(now,delta);}
  void Tick(float now,float delta){
   if(!game)return;
   bool mute=game.Muted;current.mute=next.mute=accent.mute=mute;
   if(NocturnisCinematic.BlocksInput)return; // Video owns the listener until it finishes or is skipped.
   if(zone!=game.Zone){zone=game.Zone;lastThreat=lastBoss=-100;}
   string wanted=selected;var phase=game.State;
   if(phase==KryptaGame.Phase.Title){wanted="menu";lastThreat=lastBoss=-100;}
   else if(phase==KryptaGame.Phase.Dead)wanted="death";
   else if(phase==KryptaGame.Phase.Won)wanted="finale";
   else if(phase==KryptaGame.Phase.Playing){
    int threat=game.MusicThreat;
    if(threat>0||now-game.LastMusicContact<3)lastThreat=now;
    if(threat==2)lastBoss=now;
    wanted=zone==Campaign.Town ? "town" : now-lastBoss<9 ? "boss" : now-lastThreat<9 ? "combat" : EnvironmentCue(zone);
   }
   if(string.IsNullOrEmpty(wanted))wanted=EnvironmentCue(zone);
   if(wanted!=selected)Change(wanted);
   bool quiet=phase==KryptaGame.Phase.Dialogue||phase==KryptaGame.Phase.Paused||game.IntroActive;
   duck=Mathf.MoveTowards(duck,quiet?.35f:1,delta*(quiet?2:1));
   blend=Mathf.MoveTowards(blend,1,delta/(selected=="combat"||selected=="boss"?.9f:2.7f));
   float level=.72f*Volume*duck;
   current.volume=fromGain*Mathf.Sqrt(1-blend)*level;next.volume=Mathf.Sqrt(blend)*level;accent.volume=level*.8f;
   if(blend>=1 && current.isPlaying)current.Stop();
  }
  void Change(string name){
   var clip=Clip(name);if(!clip)return;
   // Retain the dominant old source when a map changes during a fade.
   if(next.isPlaying && (!current.isPlaying || blend>=.5f)){var swap=current;current=next;next=swap;fromGain=Mathf.Sqrt(blend);}
   else fromGain=current.isPlaying ? Mathf.Sqrt(1-blend)*fromGain : 0;
   next.Stop();next.clip=clip;next.loop=name!="death"&&name!="finale";
   next.volume=0;next.Play();selected=name;blend=0;Transitions++;
  }
  public void Victory(){var c=Clip("victory");if(c && game && !game.Muted){accent.Stop();accent.clip=c;accent.loop=false;accent.Play();}}
  public bool AssetsComplete(){foreach(var name in Required)if(!Clip(name))return false;return true;}
 }
 public sealed partial class KryptaGame {
  NocturnisMusic musicSystem;float lastMusicContact=-100;
  public float LastMusicContact {get{return lastMusicContact;}}
  public float MusicVolume {get{return musicSystem?musicSystem.Volume:PlayerPrefs.GetFloat("Nocturnis.MusicVolume",.65f);}}
  public void SetMusicVolume(float level){if(musicSystem)musicSystem.SetVolume(level);}
  public int MusicThreat {get{
   if(State!=Phase.Playing || player==null || Health<=0 || Zone==Campaign.Town)return 0;
   int value=0;
   foreach(var e in enemies){
    if(e.hp<=0 || !e.visual.Root || Vector3.Distance(e.Position,HeroPosition)>16)continue;
    bool engaged=e.alerted||e.impactAt>0||e.chargeAt>0||GuardianPhase(e)>0;
    if(!engaged || !Visible(e.Position,HeroPosition))continue;
    if(e.boss || IsStageGuardian(e))return 2;value=1;
   }
   return value;
  }}
  void MusicContact(){lastMusicContact=Time.time;}
 }
}
