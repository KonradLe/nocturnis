using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace Krypta
{
 public sealed class NocturnisAudioQA : MonoBehaviour
 {
  [Serializable] sealed class Result {
   public bool passed,qaIsolation,randomIsolated,noImmediateRepeat,protectedTailSurvives,
    mutedSources,mutedDoesNotQueue,pausedTailFrozen,pausedRejectsCombat,pausedAllowsUI,
    resumedTailAdvances,distanceAttenuates,worldPans,distantSoundCulled,preferencesPreserved,surfaceMapping;
   public int sampledClips,variantChecks;public string[] missingBanks;public string error;
  }
  static bool Requested {get{return Array.IndexOf(Environment.GetCommandLineArgs(),"-audio-review")>=0;}}
  static string Arg(string key,string fallback){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:fallback;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  static void Isolate(){if(Requested)KryptaGame.Testing=true;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Launch(){if(Requested)new GameObject("Audio review").AddComponent<NocturnisAudioQA>();}
  IEnumerator Start()
  {
   string folder=Arg("-audio-output","Logs/AudioReview");Directory.CreateDirectory(folder);
   var r=new Result{qaIsolation=KryptaGame.Testing};KryptaGame game=null;NocturnisAudio audio=null;
   float deadline=Time.realtimeSinceStartup+60;
   while(Time.realtimeSinceStartup<deadline){
    game=FindFirstObjectByType<KryptaGame>();audio=game?game.GetComponent<NocturnisAudio>():null;
    if(audio && game.View && game.State==KryptaGame.Phase.Title)break;yield return null;
   }
   if(!audio){r.error="Audio component unavailable";Finish(folder,r);yield break;}
   bool enabled=audio.enabled,muted=audio.Muted;float effects=audio.EffectsVolume,ambient=audio.AmbientVolume;
   int prefMute=PlayerPrefs.GetInt("Nocturnis.Muted",0);
   float prefEffects=PlayerPrefs.GetFloat("Nocturnis.EffectsVolume",1),prefAmbient=PlayerPrefs.GetFloat("Nocturnis.AmbientVolume",1);
   audio.enabled=false;audio.ReviewStop();audio.ReviewMute(false);audio.ReviewLevels(.7f,0);
   r.surfaceMapping=KryptaGame.AudioSurfaceAt(Campaign.Town,Vector3.zero,false)=="stone" && KryptaGame.AudioSurfaceAt(Campaign.Fields,Vector3.zero,false)=="earth" && KryptaGame.AudioSurfaceAt(Campaign.Road,new Vector3(21.6f,0,61.2f),true)=="wood" && KryptaGame.AudioSurfaceAt(Campaign.Road,new Vector3(21.6f,0,61.2f),false)=="earth";
   r.missingBanks=audio.ReviewMissingBanks();r.sampledClips=audio.ReviewSampleCount;
   var oldRandom=UnityEngine.Random.state;
   UnityEngine.Random.InitState(73619);float expected=UnityEngine.Random.value;
   UnityEngine.Random.InitState(73619);audio.ReviewPlay("attack0");float actual=UnityEngine.Random.value;
   UnityEngine.Random.state=oldRandom;r.randomIsolated=expected==actual;
   audio.ReviewStop();r.noImmediateRepeat=true;
   foreach(string cue in NocturnisAudio.RequiredBanks){
    if(audio.ReviewVariantCount(cue)<2)continue;
    int last=-1;
    for(int i=0;i<6;i++){
     audio.ReviewStop();bool played=audio.ReviewPlay(cue);int variant=audio.ReviewLastVariant(cue);
     r.noImmediateRepeat &= played && variant!=last;last=variant;r.variantChecks++;
    }
   }
   audio.ReviewStop();audio.ReviewPlay("ward0");int protectedSerial=audio.ReviewSerial;
   for(int i=0;i<48;i++){audio.ReviewPlay("foot-stone");audio.ReviewPlay("coin");audio.ReviewPlay("hit-flesh");}
   r.protectedTailSurvives=audio.ReviewPlayingSerial(protectedSerial);
   audio.ReviewMute(true);r.mutedSources=audio.ReviewAllMuted;
   int serial=audio.ReviewSerial;bool enqueued=audio.ReviewPlay("hurt");
   r.mutedDoesNotQueue=!enqueued && serial==audio.ReviewSerial;
   audio.ReviewMute(false);audio.ReviewStop();
   audio.ReviewPlay("spell0");yield return new WaitForSecondsRealtime(.04f);
   int before=audio.ReviewTimeSamples("spell0");
   audio.ReviewPause(true);yield return new WaitForSecondsRealtime(.14f);
   int paused=audio.ReviewTimeSamples("spell0");
   r.pausedTailFrozen=audio.ReviewHasPausedVoice && Mathf.Abs(paused-before)<=2048;
   r.pausedRejectsCombat=!audio.ReviewPlay("attack0");
   r.pausedAllowsUI=audio.ReviewPlay("stone");
   audio.ReviewPause(false);yield return new WaitForSecondsRealtime(.10f);
   r.resumedTailAdvances=audio.ReviewTimeSamples("spell0")>paused;
   audio.ReviewStop();
   Vector3 right=game.View.transform.right;right.y=0;right.Normalize();
   audio.PlayAt("hit-flesh",game.HeroPosition+right*3);
   float near=audio.ReviewGain("hit-flesh"),pan=audio.ReviewPan("hit-flesh");
   audio.ReviewStop();audio.PlayAt("hit-flesh",game.HeroPosition+right*20);
   float far=audio.ReviewGain("hit-flesh");
   r.distanceAttenuates=near>far && far>0;r.worldPans=pan>.15f;
   audio.ReviewStop();serial=audio.ReviewSerial;audio.PlayAt("hit-flesh",game.HeroPosition+right*40);
   r.distantSoundCulled=audio.ReviewSerial==serial;
   audio.ReviewStop();audio.ReviewLevels(effects,ambient);audio.ReviewMute(muted);audio.enabled=enabled;
   r.preferencesPreserved=PlayerPrefs.GetInt("Nocturnis.Muted",0)==prefMute &&
    PlayerPrefs.GetFloat("Nocturnis.EffectsVolume",1)==prefEffects && PlayerPrefs.GetFloat("Nocturnis.AmbientVolume",1)==prefAmbient;
   r.passed=r.qaIsolation && r.missingBanks.Length==0 && r.sampledClips>=NocturnisAudio.RequiredBanks.Length &&
    r.randomIsolated && r.noImmediateRepeat && r.variantChecks>0 && r.protectedTailSurvives &&
    r.mutedSources && r.mutedDoesNotQueue && r.pausedTailFrozen && r.pausedRejectsCombat &&
    r.pausedAllowsUI && r.resumedTailAdvances && r.distanceAttenuates && r.worldPans &&
    r.distantSoundCulled && r.preferencesPreserved && r.surfaceMapping;
   Finish(folder,r);
  }
  void Finish(string folder,Result result){
   File.WriteAllText(Path.Combine(folder,"audio-review.json"),JsonUtility.ToJson(result,true));
   Debug.Log("AUDIO_REVIEW "+JsonUtility.ToJson(result));Application.Quit(result.passed?0:1);
  }
 }
}
