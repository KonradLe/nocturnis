using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace Krypta {
 public sealed class NocturnisCinematicQA:MonoBehaviour {
  [Serializable]class Result{public bool passed,opened,decoded,frozen,returnedToMenu,newGameDidNotReplay,continueDidNotReplay,qaIsolation;public int hero;public string mode;}
  static string Arg(string k,string d){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,k);return i>=0&&i+1<a.Length?a[i+1]:d;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  static void Isolate(){if(NocturnisCinematic.QA)KryptaGame.Testing=true;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Launch(){if(NocturnisCinematic.QA)new GameObject("Startup cinematic QA").AddComponent<NocturnisCinematicQA>();}
  IEnumerator Start(){
   string folder=Arg("-cinematic-output","Logs/StartupQA");Directory.CreateDirectory(folder);
   int hero;int.TryParse(Arg("-cinematic-hero","0"),out hero);
   string mode=Arg("-cinematic-mode","full");var r=new Result{hero=hero,mode=mode,qaIsolation=KryptaGame.Testing};
   KryptaGame game=null;float deadline=Time.realtimeSinceStartup+60;
   while(Time.realtimeSinceStartup<deadline){game=FindFirstObjectByType<KryptaGame>();if(game&&game.View&&game.State==KryptaGame.Phase.Title)break;yield return null;}
   if(!game){File.WriteAllText(Path.Combine(folder,"result.json"),JsonUtility.ToJson(r,true));Application.Quit(2);yield break;}
   r.opened=NocturnisCinematic.Active;float elapsed=game.Elapsed;float started=Time.realtimeSinceStartup;
   bool screenshot=false,wasFrozen=true;
   while(NocturnisCinematic.Active && Time.realtimeSinceStartup-started<38){
    wasFrozen &= Time.timeScale==0 && Mathf.Approximately(game.Elapsed,elapsed);
    if(NocturnisCinematic.DecodedFrame>2)r.decoded=true;
    if(r.decoded&&!screenshot&&Time.realtimeSinceStartup-started>2){
     yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"intro-frame.png"));screenshot=true;
    }
    if(mode=="skip"&&r.decoded&&Time.realtimeSinceStartup-started>3)NocturnisCinematic.Skip();
    yield return null;
   }
   r.frozen=wasFrozen;yield return null;
   r.returnedToMenu=!NocturnisCinematic.Active&&Time.timeScale==0&&!AudioListener.pause&&game.State==KryptaGame.Phase.Title;
   yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"menu-frame.png"));yield return null;
   game.SelectHero(hero);game.Begin();yield return null;
   r.newGameDidNotReplay=!NocturnisCinematic.Active&&game.State==KryptaGame.Phase.Playing&&Time.timeScale==1;
   bool loaded=game.ContinueCheckpoint();yield return null;
   r.continueDidNotReplay=loaded&&!NocturnisCinematic.Active&&game.State==KryptaGame.Phase.Playing;
   r.passed=r.qaIsolation&&r.returnedToMenu&&r.newGameDidNotReplay&&r.continueDidNotReplay&&(mode=="missing"?!r.opened:r.opened&&r.decoded&&r.frozen);
   yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"game-frame.png"));yield return null;yield return null;
   File.WriteAllText(Path.Combine(folder,"result.json"),JsonUtility.ToJson(r,true));
   Debug.Log("STARTUP_QA "+JsonUtility.ToJson(r));Application.Quit(r.passed?0:1);
  }
 }
}
