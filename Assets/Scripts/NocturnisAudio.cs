using System;
using System.Collections.Generic;
using UnityEngine;

namespace Krypta
{
 // Real sample banks live in Resources/NocturnisAudio/<cue>/*.wav.
 // Gameplay and sound never share a random stream.
 public sealed class NocturnisAudio : MonoBehaviour
 {
  enum Bus { Combat, Protected, Interface, Footsteps }
  sealed class Bank
  {
   public string id; public AudioClip[] clips; public Bus bus;
   public int priority,last=-1; public float gain,cooldown,nextAt;
   public bool fallback;
  }
  sealed class Voice
  {
   public AudioSource source; public Bus bus; public string cue="";
   public int priority,serial; public float started,gain; public bool paused;
   public bool Busy {get{return paused || source.isPlaying;}}
  }
  internal static readonly string[] RequiredBanks={
   "attack0","attack1","attack2","attack3","attack4",
   "spell0","spell1","spell2","spell3","spell4",
   "ward0","ward1","ward2","ward3","ward4",
   "hit-flesh","hit-bone","hit-metal","hurt","enemy-swing","enemy-death",
   "dodge","potion","levelup","coin","shrine","victory","stone","chain","triptych","gate",
   "woodbreak","potbreak","metalbreak","foot-stone","foot-earth","foot-wood"
  };
  static readonly string[] AmbientBanks={"ambient-town","ambient-forest","ambient-open","ambient-crypt","ambient-forge"};
  readonly Dictionary<string,Bank> banks=new Dictionary<string,Bank>(StringComparer.Ordinal);
  readonly List<Voice> voices=new List<Voice>();
  readonly List<AudioClip> ownedClips=new List<AudioClip>();
  readonly System.Random random=new System.Random(904731);
  AudioSource ambience,ambienceNext; AudioClip[] environments;
  KryptaGame game; int environment=-1,serial;
  float blend=1,duck=1; bool simulationPaused;
  public bool Muted {get;private set;}
  public float EffectsVolume {get;private set;}
  public float AmbientVolume {get;private set;}

  void Awake()
  {
   game=GetComponent<KryptaGame>();
   CreateVoices(Bus.Combat,14);CreateVoices(Bus.Protected,5);
   CreateVoices(Bus.Interface,4);CreateVoices(Bus.Footsteps,3);
   ambience=NewSource("Ambience A",220);ambience.loop=true;
   ambienceNext=NewSource("Ambience B",220);ambienceNext.loop=true;
   foreach(string cue in RequiredBanks)LoadBank(cue);
   ResolveMissingBanks();
   environments=new AudioClip[AmbientBanks.Length];
   for(int i=0;i<environments.Length;i++){
    AudioClip[] clips=Resources.LoadAll<AudioClip>("NocturnisAudio/"+AmbientBanks[i]);
    if(clips.Length>0)environments[i]=clips[0];
   }
   Muted=PlayerPrefs.GetInt("Nocturnis.Muted",0)==1;
   SetLevels(PlayerPrefs.GetFloat("Nocturnis.EffectsVolume",1),PlayerPrefs.GetFloat("Nocturnis.AmbientVolume",1),false);
   ApplyMute();
   ChangeEnvironment(0);blend=1;ApplyAmbientVolume();
  }
  AudioSource NewSource(string label,int priority)
  {
   var source=gameObject.AddComponent<AudioSource>();
   source.playOnAwake=false;source.loop=false;source.priority=priority;
   source.spatialBlend=0;source.dopplerLevel=0;source.reverbZoneMix=0;
   return source;
  }
  void CreateVoices(Bus bus,int count)
  {
   for(int i=0;i<count;i++)voices.Add(new Voice{source=NewSource(bus.ToString(),bus==Bus.Protected?40:bus==Bus.Footsteps?170:90),bus=bus});
  }
  void LoadBank(string cue)
  {
   var bank=new Bank{id=cue,clips=Resources.LoadAll<AudioClip>("NocturnisAudio/"+cue),bus=Bus.Combat,priority=2,gain=.72f,cooldown=.055f};
   Array.Sort(bank.clips,(a,b)=>string.CompareOrdinal(a.name,b.name));
   if(cue.StartsWith("foot-")){bank.bus=Bus.Footsteps;bank.priority=1;bank.gain=.36f;bank.cooldown=.085f;}
   else if(cue=="stone" || cue=="chain" || cue=="triptych" || cue=="coin" || cue=="shrine" || cue=="gate")
   {bank.bus=Bus.Interface;bank.gain=cue=="stone"?.27f:cue=="coin"?.42f:.58f;bank.cooldown=cue=="coin"?.075f:.06f;}
   else if(cue=="hurt" || cue=="levelup" || cue=="victory" || cue.StartsWith("ward") || cue=="potion")
   {bank.bus=Bus.Protected;bank.priority=4;bank.gain=cue=="hurt"?.85f:.78f;bank.cooldown=.12f;}
   else if(cue.StartsWith("attack")){bank.priority=3;bank.gain=.78f;bank.cooldown=.045f;}
   else if(cue.StartsWith("spell")){bank.priority=3;bank.gain=.88f;bank.cooldown=.075f;}
   else if(cue=="enemy-swing"){bank.gain=.50f;bank.cooldown=.10f;}
   else if(cue=="enemy-death"){bank.gain=.68f;bank.cooldown=.10f;}
   else if(cue=="dodge"){bank.priority=3;bank.gain=.65f;bank.cooldown=.15f;}
   banks.Add(cue,bank);
  }
  void ResolveMissingBanks()
  {
   AudioClip emergency=null;
   foreach(var bank in banks.Values)
   {
    if(bank.clips.Length>0)continue;
    bank.fallback=true;
    string alias=bank.id.StartsWith("ward")?"shrine":bank.id.StartsWith("spell")?"spell3":
      bank.id.StartsWith("attack")?"attack0":bank.id.StartsWith("foot-")?"foot-stone":
      bank.id.StartsWith("hit-")?"hit-flesh":bank.id.EndsWith("break")?"woodbreak":"stone";
    Bank candidate;
    if(alias!=bank.id && banks.TryGetValue(alias,out candidate) && candidate.clips.Length>0 && !candidate.fallback)bank.clips=candidate.clips;
    else {
     if(!emergency){emergency=EmergencyCue();ownedClips.Add(emergency);}
     bank.clips=new[]{emergency};
    }
    Debug.LogWarning("AUDIO_BANK_FALLBACK "+bank.id);
   }
  }
  // A missing optional asset must not break gameplay. This tiny unpitched click is
  // only an emergency cue; release QA rejects banks which require it.
  static AudioClip EmergencyCue()
  {
   const int rate=24000;var data=new float[2400];var rng=new System.Random(131);
   float low=0;
   for(int i=0;i<data.Length;i++){low=Mathf.Lerp(low,(float)rng.NextDouble()*2-1,.2f);data[i]=low*.15f*Mathf.Pow(1-i/(float)data.Length,3);}
   var clip=AudioClip.Create("Missing audio fallback",data.Length,1,rate,false);clip.SetData(data,0);return clip;
  }
  public void Toggle(){Muted=!Muted;PlayerPrefs.SetInt("Nocturnis.Muted",Muted?1:0);PlayerPrefs.Save();ApplyMute();}
  public void SetVolumes(float effects,float ambient){SetLevels(effects,ambient,true);}
  void SetLevels(float effects,float ambient,bool persist)
  {
   EffectsVolume=Mathf.Clamp01(effects);AmbientVolume=Mathf.Clamp01(ambient);
   foreach(var voice in voices)voice.source.volume=.62f*EffectsVolume*voice.gain;
   ApplyAmbientVolume();
   if(persist){PlayerPrefs.SetFloat("Nocturnis.EffectsVolume",EffectsVolume);PlayerPrefs.SetFloat("Nocturnis.AmbientVolume",AmbientVolume);}
  }
  void ApplyMute()
  {
   foreach(var voice in voices)voice.source.mute=Muted;
   if(ambience)ambience.mute=Muted;if(ambienceNext)ambienceNext.mute=Muted;
  }
  void OnApplicationFocus(bool focused){if(!focused && !NocturnisAudioShowcase.Requested)PlayerPrefs.Save();}
  void OnApplicationQuit(){if(NocturnisAudioShowcase.Requested)return;GetComponent<KryptaGame>()?.SaveBeforeQuit();PlayerPrefs.Save();}
  void ChangeEnvironment(int wanted)
  {
   if(wanted==environment)return;
   if(blend>=.5f && ambienceNext.isPlaying){var swap=ambience;ambience=ambienceNext;ambienceNext=swap;}
   ambienceNext.Stop();ambienceNext.clip=environments[wanted];
   if(ambienceNext.clip)ambienceNext.Play();
   blend=0;environment=wanted;
  }
  void ApplyAmbientVolume()
  {
   float level=.30f*AmbientVolume*duck*(game && game.State==KryptaGame.Phase.Title?.7f:1);
   if(ambience)ambience.volume=level*Mathf.Sqrt(1-blend);
   if(ambienceNext)ambienceNext.volume=level*Mathf.Sqrt(blend);
  }
  void Update()
  {
   if(!game)return;
   bool paused=game.State==KryptaGame.Phase.Paused || game.State==KryptaGame.Phase.Dialogue;
   SetSimulationPaused(paused);
   int wanted=game.Zone==Campaign.Town?0:game.Zone==Campaign.Forest || game.Zone==Campaign.Fields?1:
    game.Zone==Campaign.Forge || game.Zone==Campaign.Heart?4:Campaign.Underground(game.Zone)?3:2;
   ChangeEnvironment(wanted);
   blend=Mathf.MoveTowards(blend,1,Time.unscaledDeltaTime/2.5f);
   bool combat=false;
   foreach(var voice in voices)if(voice.bus==Bus.Combat && voice.source.isPlaying && voice.priority>=3){combat=true;break;}
   duck=Mathf.MoveTowards(duck,combat?.66f:1,Time.unscaledDeltaTime*2.2f);
   ApplyAmbientVolume();
   if(blend>=1 && ambience.isPlaying)ambience.Stop();
   string surface;int foot;
   if(game.PollFootstep(out surface,out foot))PlayInternal("foot-"+surface,.9f,foot==0?-.055f:.055f,false);
  }
  void SetSimulationPaused(bool paused)
  {
   if(paused==simulationPaused)return;
   simulationPaused=paused;
   foreach(var voice in voices){
    if(voice.bus==Bus.Interface)continue;
    if(paused){
     if(voice.bus==Bus.Footsteps){voice.source.Stop();voice.paused=false;}
     else if(voice.source.isPlaying){voice.source.Pause();voice.paused=true;}
    }else if(voice.paused){voice.paused=false;voice.source.UnPause();}
   }
  }
  static string Canonical(string cue){return cue=="ui-click"?"stone":cue=="attack"?"attack0":cue=="spell"?"spell3":cue=="hit"?"hit-flesh":cue;}
  public void Play(string cue){PlayInternal(Canonical(cue),1,0,false);}
  public void PlayAt(string cue,Vector3 point)
  {
   Vector3 listener=game?game.HeroPosition:transform.position;
   Vector3 delta=point-listener;delta.y=0;float distance=delta.magnitude;
   if(distance>=34)return;
   float gain=distance<=2?1:Mathf.Clamp01(1-(distance-2)/32f);
   gain*=gain;
   Vector3 right=game && game.View?game.View.transform.right:Vector3.right;right.y=0;right.Normalize();
   float pan=Mathf.Clamp(Vector3.Dot(delta,right)/Mathf.Max(5,distance),-.72f,.72f);
   PlayInternal(Canonical(cue),gain,pan,false);
  }
  bool PlayInternal(string cue,float gain,float pan,bool bypassThrottle)
  {
   Bank bank;
   if(Muted || EffectsVolume<=0 || !banks.TryGetValue(cue,out bank))return false;
   if(simulationPaused && bank.bus!=Bus.Interface)return false;
   float now=Time.unscaledTime;
   if(!bypassThrottle && now<bank.nextAt)return false;
   Voice chosen=SelectVoice(bank.bus,bank.priority,now);
   if(chosen==null)return false;
   int index=PickVariant(bank);
   AudioClip clip=bank.clips[index];if(!clip)return false;
   bank.nextAt=now+bank.cooldown;bank.last=index;
   chosen.source.Stop();chosen.paused=false;chosen.cue=cue;chosen.priority=bank.priority;chosen.started=now;chosen.serial=++serial;
   chosen.gain=gain*bank.gain;
   chosen.source.clip=clip;
   // Tiny variation avoids a machine-gun effect without turning voices into chipmunks.
   chosen.source.pitch=bank.bus==Bus.Interface?1:(float)(.975+random.NextDouble()*.05);
   chosen.source.panStereo=pan;chosen.source.volume=.62f*EffectsVolume*chosen.gain;chosen.source.Play();
   return true;
  }
  Voice SelectVoice(Bus bus,int priority,float now)
  {
   Voice candidate=null;
   foreach(var voice in voices){
    if(voice.bus!=bus)continue;
    if(!voice.Busy)return voice;
    if(voice.priority>priority || (voice.priority==priority && now-voice.started<.12f))continue;
    if(candidate==null || voice.priority<candidate.priority || (voice.priority==candidate.priority && voice.started<candidate.started))candidate=voice;
   }
   return candidate;
  }
  int PickVariant(Bank bank)
  {
   if(bank.clips.Length<2)return 0;
   int next=random.Next(bank.clips.Length-(bank.last>=0?1:0));
   return bank.last>=0 && next>=bank.last?next+1:next;
  }
  void OnDestroy(){foreach(var clip in ownedClips)if(clip)Destroy(clip);}

  // Opt-in runtime QA uses actual loaded banks and actual AudioSources.
  internal string[] ReviewMissingBanks(){
   var missing=new List<string>();foreach(var bank in banks.Values)if(bank.fallback)missing.Add(bank.id);
   for(int i=0;i<environments.Length;i++)if(!environments[i])missing.Add(AmbientBanks[i]);
   return missing.ToArray();
  }
  internal int ReviewSampleCount {get{int count=0;foreach(var b in banks.Values)if(!b.fallback)count+=b.clips.Length;return count;}}
  internal int ReviewSerial {get{return serial;}}
  internal bool ReviewPlay(string cue){return PlayInternal(cue,1,0,true);}
  internal void ReviewMute(bool muted){Muted=muted;ApplyMute();}
  internal void ReviewLevels(float effects,float ambient){SetLevels(effects,ambient,false);}
  internal bool ReviewAllMuted {get{foreach(var voice in voices)if(!voice.source.mute)return false;return ambience.mute&&ambienceNext.mute;}}
  internal int ReviewLastVariant(string cue){return banks[cue].last;}
  internal int ReviewVariantCount(string cue){return banks[cue].clips.Length;}
  internal bool ReviewPlayingSerial(int value){foreach(var voice in voices)if(voice.serial==value && voice.Busy)return true;return false;}
  internal void ReviewPause(bool paused){SetSimulationPaused(paused);}
  internal bool ReviewSimulationPaused {get{return simulationPaused;}}
  internal bool ReviewHasPausedVoice {get{foreach(var voice in voices)if(voice.paused)return true;return false;}}
  internal int ReviewTimeSamples(string cue){foreach(var voice in voices)if(voice.cue==cue && voice.Busy)return voice.source.timeSamples;return 0;}
  internal void ReviewStop(){
   foreach(var voice in voices){voice.source.Stop();voice.paused=false;}simulationPaused=false;
   foreach(var bank in banks.Values)bank.nextAt=0;
  }
  internal float ReviewPan(string cue){foreach(var voice in voices)if(voice.cue==cue && voice.Busy)return voice.source.panStereo;return 0;}
  internal float ReviewGain(string cue){foreach(var voice in voices)if(voice.cue==cue && voice.Busy)return voice.source.volume;return 0;}
 }

 public sealed partial class KryptaGame
 {
  NocturnisAudio audioSystem;
  int previousFoot=-1;string previousFootClip="";
  internal static string AudioSurfaceAt(int zone,Vector3 point,bool lowered){
   Vector3 bridge=World(12,34);
   if(zone==Campaign.Road && lowered && Mathf.Abs(point.x-bridge.x)<=2 && Mathf.Abs(point.z-bridge.z)<=2.35f)return "wood";
   return Campaign.Outdoors(zone) && zone!=Campaign.Cloister?"earth":"stone";
  }
  public bool PollFootstep(out string surface,out int foot)
  {
   surface=AudioSurfaceAt(Zone,HeroPosition,bridgeLowered);foot=0;
   if(State!=Phase.Playing || Time.deltaTime<=0 || player==null || player.LocomotionSpeed<.1f || MeleeWindupActive){previousFoot=-1;return false;}
   string clip=player.CurrentClip;if(clip!="Walking_A" && clip!="Running_A"){previousFoot=-1;return false;}
   int phase=Mathf.FloorToInt(player.CurrentClipNormalizedTime*2);foot=phase&1;
   bool changed=previousFoot>=0 && previousFootClip==clip && previousFoot!=phase;
   previousFoot=phase;previousFootClip=clip;return changed;
  }
  public bool Muted {get{return audioSystem && audioSystem.Muted;}}
  public float EffectsVolume {get{return audioSystem?audioSystem.EffectsVolume:1;}}
  public float AmbientVolume {get{return audioSystem?audioSystem.AmbientVolume:1;}}
  public void SetAudioVolumes(float effects,float ambient){if(audioSystem)audioSystem.SetVolumes(effects,ambient);}
  public void ToggleAudio(){if(audioSystem)audioSystem.Toggle();}
  public void UISound(string cue){Sound(cue);}
  void Sound(string cue){if(audioSystem)audioSystem.Play(cue);}
  void SoundAt(string cue,Vector3 point){if(audioSystem)audioSystem.PlayAt(cue,point);}
  string EnemyHitCue(Enemy enemy){
   if(enemy.archetype==6 || (enemy.elite && !enemy.zombie))return "hit-metal";
   return enemy.zombie || enemy.archetype==2 || enemy.archetype==3 || enemy.archetype==7 || enemy.boss?"hit-flesh":"hit-bone";
  }
 }
}
