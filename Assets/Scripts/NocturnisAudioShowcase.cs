using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Krypta
{
 // Diagnostic capture of the real Unity listener mix, not a rendered trailer.
 public sealed class NocturnisAudioShowcase : MonoBehaviour
 {
  public static bool Requested {get{return Array.IndexOf(Environment.GetCommandLineArgs(),"-audio-showcase")>=0;}}
  public static string Arg(string key,string fallback){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:fallback;}
  string directory,error;float opened;bool finished;NocturnisListenerCapture recorder;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  static void Isolate(){if(Requested)KryptaGame.Testing=true;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Launch(){if(Requested)new GameObject("Runtime audio showcase").AddComponent<NocturnisAudioShowcase>();}
  IEnumerator Start()
  {
   directory=Path.GetFullPath(Arg("-audio-output","Logs/AudioShowcase"));Directory.CreateDirectory(directory);
   opened=Time.realtimeSinceStartup;Application.logMessageReceived+=Log;Application.runInBackground=true;
   KryptaGame game=null;float timeout=Time.realtimeSinceStartup+60;
   while(Time.realtimeSinceStartup<timeout){game=FindFirstObjectByType<KryptaGame>();if(game && game.View)break;yield return null;}
   if(!game || !game.View){Fail("Game startup timeout");yield break;}
   game.enabled=false;var hud=game.GetComponent<KryptaHUD>();if(hud)hud.enabled=false;
   recorder=game.View.gameObject.AddComponent<NocturnisListenerCapture>();
   yield return game.CaptureAudioShowcase(directory,recorder);
   if(error!=null || !game.AudioShowcasePassed){Fail(error??"See audio-showcase.json for failed capture checks");yield break;}
   finished=true;Application.logMessageReceived-=Log;Application.Quit(0);
  }
  void Log(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)error=message+"\n"+stack;}
  void Update(){if(!finished && directory!=null && (error!=null || Time.realtimeSinceStartup-opened>120))Fail(error??"Audio showcase timeout");}
  void Fail(string message){
   if(finished)return;finished=true;StopAllCoroutines();Application.logMessageReceived-=Log;
   if(directory!=null){File.WriteAllText(Path.Combine(directory,"failure.txt"),message);if(recorder)recorder.Save(Path.Combine(directory,"partial-mix.wav"));}
   Application.Quit(1);
  }
 }
 public sealed class NocturnisListenerCapture : MonoBehaviour
 {
  [Serializable]public sealed class Stats {
   public int sampleRate,channels,samples,clippedSamples;public float seconds,peak,rms,clippedFraction;
   public bool receivedAudio,capacityExceeded;
  }
  readonly object gate=new object();float[] samples;int count,rate,channels;bool active,overflow;
  public float Seconds {get{lock(gate)return channels>0?count/(float)(rate*channels):0;}}
  public void Begin(int maximumSeconds){
   lock(gate){rate=AudioSettings.outputSampleRate;samples=new float[rate*8*maximumSeconds];count=channels=0;overflow=false;active=true;}
  }
  // Unity calls this on the audio thread after mixing the AudioListener inputs.
  // It copies samples only: no file IO, Unity calls or modification of the sound.
  void OnAudioFilterRead(float[] data,int channelCount)
  {
   lock(gate){
    if(!active || samples==null)return;
    if(channels==0)channels=channelCount;
    if(channelCount!=channels){overflow=true;return;}
    int take=Math.Min(data.Length,samples.Length-count);
    if(take<data.Length)overflow=true;
    Array.Copy(data,0,samples,count,take);count+=take;
   }
  }
  public Stats Save(string path)
  {
   float[] data;int length,channelCount,sampleRate;bool exceeded;
   lock(gate){active=false;data=samples;length=count;channelCount=channels;sampleRate=rate;exceeded=overflow;}
   var stats=new Stats{sampleRate=sampleRate,channels=channelCount,samples=length,capacityExceeded=exceeded};
   if(data==null || length==0 || channelCount==0)return stats;
   length-=length%channelCount;double squares=0;
   using(var stream=new BinaryWriter(File.Create(path))){
    int bytes=length*2;stream.Write(new[]{(byte)'R',(byte)'I',(byte)'F',(byte)'F'});stream.Write(36+bytes);
    stream.Write(new[]{(byte)'W',(byte)'A',(byte)'V',(byte)'E',(byte)'f',(byte)'m',(byte)'t',(byte)' '});
    stream.Write(16);stream.Write((short)1);stream.Write((short)channelCount);stream.Write(sampleRate);
    stream.Write(sampleRate*channelCount*2);stream.Write((short)(channelCount*2));stream.Write((short)16);
    stream.Write(new[]{(byte)'d',(byte)'a',(byte)'t',(byte)'a'});stream.Write(bytes);
    for(int i=0;i<length;i++){
     float sample=data[i],absolute=Math.Abs(sample);stats.peak=Math.Max(stats.peak,absolute);
     if(absolute>=1)stats.clippedSamples++;squares+=sample*(double)sample;
     stream.Write((short)Math.Round(Math.Max(-1,Math.Min(1,sample))*32767));
    }
   }
   stats.seconds=length/(float)(sampleRate*channelCount);stats.rms=(float)Math.Sqrt(squares/length);
   stats.clippedFraction=stats.clippedSamples/(float)length;stats.receivedAudio=stats.peak>.001f;return stats;
  }
 }
 public sealed partial class KryptaGame
 {
  [Serializable]sealed class AudioShot {
   public int hero,zone;public float at;public string action,file;
  }
  [Serializable]sealed class AudioShowcaseResult {
   public bool passed,qaIsolation,noGameplayUpdate;public string source="Actual Unity AudioListener mix and rendered frames. Scripted QA movement and target setup; enemy AI frozen; not normal playthrough or marketing footage.";
   public string mix="runtime-mix.wav";public NocturnisListenerCapture.Stats audio;
   public List<AudioShot> shots=new List<AudioShot>();public List<string> errors=new List<string>();
  }
  public bool AudioShowcasePassed {get;private set;}
  void AudioShowcaseTick(Vector3 direction)
  {
   bool moving=direction.sqrMagnitude>.001f;
   if(moving && !MeleeWindupActive){
    Vector3 before=HeroPosition;
    Move(player.Root.transform,direction*Hero.speed*Time.deltaTime,.32f);
    FaceTravel(player,HeroPosition-before,Time.deltaTime);facing=direction;
   }
   player.Tick(moving);TickMeleeActions(Time.time);UpdateEffects();
   foreach(var enemy in enemies)if(enemy.hp<=0)TickDeadEnemy(enemy,Time.time);
   LateUpdate();
  }
  IEnumerator AudioShowcaseWait(float seconds,Vector3 direction)
  {
   float until=Time.realtimeSinceStartup+seconds;
   while(Time.realtimeSinceStartup<until){AudioShowcaseTick(direction);yield return null;}
  }
  void AudioShowcaseShot(string directory,AudioShowcaseResult result,NocturnisListenerCapture capture,int hero,string action){
   string filename="hero-"+hero+"-"+action+".png";
   result.shots.Add(new AudioShot{hero=hero,zone=Zone,at=capture.Seconds,action=action,file=filename});
   ScreenCapture.CaptureScreenshot(Path.Combine(directory,filename));
  }
  public IEnumerator CaptureAudioShowcase(string directory,NocturnisListenerCapture recorder)
  {
   if(!Testing || !NocturnisAudioShowcase.Requested)yield break;
   var result=new AudioShowcaseResult{qaIsolation=Testing,noGameplayUpdate=!enabled};
   bool mute=audioSystem.Muted;float fx=audioSystem.EffectsVolume,ambient=audioSystem.AmbientVolume;
   audioSystem.ReviewMute(false);audioSystem.ReviewLevels(1,1);recorder.Begin(55);
   for(int hero=0;hero<5;hero++){
    PrepareReviewScene(Campaign.Fields,hero,1);ReviewPosition(World(12,17));
    foreach(var old in enemies){old.hp=0;old.visual.Root.SetActive(false);}
    yield return AudioShowcaseWait(.3f,Vector3.zero);
    Vector3 direction=Vector3.forward;bool found=false;
    foreach(var candidate in new[]{Vector3.forward,Vector3.right,Vector3.back,Vector3.left}){
     Vector3 end=HeroPosition+candidate*7;
     if(!grid.CanTravel(HeroPosition.x/Tile,HeroPosition.z/Tile,end.x/Tile,end.z/Tile,.32f/Tile))continue;
     direction=candidate;found=true;break;
    }
    if(!found){result.errors.Add("No long walking path for class "+hero);direction=Vector3.forward;}
    yield return AudioShowcaseWait(.6f,direction);
    AudioShowcaseShot(directory,result,recorder,hero,"walk");
    yield return AudioShowcaseWait(.7f,direction);
    yield return AudioShowcaseWait(.2f,Vector3.zero);
    Vector3 point=HeroPosition-direction*1.8f;
    var cell=grid.NearestWalkable(Cell(point.x),Cell(point.z));
    Enemy target=SpawnEnemy(cell.X,cell.Z,hero==1,false,false,hero==0?6:0,"Audio review target");
    target.hp=target.maxHp=AttackPower*1.35f;target.alerted=false;target.stunUntil=Time.time+20;
    float initial=target.hp;strikeAt=0;ReviewCastVfx(0,target.Position);
    yield return AudioShowcaseWait(.22f,Vector3.zero);
    AudioShowcaseShot(directory,result,recorder,hero,"attack");
    yield return AudioShowcaseWait(.48f,Vector3.zero);
    if(target.hp>=initial)result.errors.Add("Basic attack missed in class "+hero);
    Resource=SelectedHero==3?0:100;Shadow=100;ReviewCastVfx(1,target.Position);
    yield return AudioShowcaseWait(.30f,Vector3.zero);
    AudioShowcaseShot(directory,result,recorder,hero,"spell");
    yield return AudioShowcaseWait(1.0f,Vector3.zero);
    AudioShowcaseShot(directory,result,recorder,hero,"death");
    if(target.hp>0)result.errors.Add("Spell did not finish target in class "+hero);
    yield return AudioShowcaseWait(.2f,Vector3.zero);
    ReviewCastVfx(2,HeroPosition);yield return AudioShowcaseWait(.25f,Vector3.zero);
    AudioShowcaseShot(directory,result,recorder,hero,"ward");
    yield return AudioShowcaseWait(.7f,Vector3.zero);
   }
   yield return AudioShowcaseWait(.6f,Vector3.zero);
   result.audio=recorder.Save(Path.Combine(directory,result.mix));
   audioSystem.ReviewStop();audioSystem.ReviewLevels(fx,ambient);audioSystem.ReviewMute(mute);
   result.passed=result.qaIsolation && result.noGameplayUpdate && result.audio.receivedAudio &&
    !result.audio.capacityExceeded && result.audio.seconds>=20 && result.audio.clippedFraction<.005f && result.errors.Count==0;
   AudioShowcasePassed=result.passed;
   File.WriteAllText(Path.Combine(directory,"audio-showcase.json"),JsonUtility.ToJson(result,true));
   Debug.Log("AUDIO_SHOWCASE "+JsonUtility.ToJson(result));
  }
 }
}
