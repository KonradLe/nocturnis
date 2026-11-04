using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Krypta {
 // Opt-in player-only regression. Survives a real scene reload and exclusively
 // uses the QA profile; normal launch never creates this object.
 public sealed class NocturnisStartupSessionQA:MonoBehaviour {
  static bool launched;
  [Serializable]sealed class Result{
   public bool passed,initialOpened,decoded,firstMenu,actualSceneReload,noReplayAfterReload,continueDidNotReplay,newGameDidNotReplay,productionSavesUntouched;
   public string error;
  }
  static string Arg(string key,string fallback){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:fallback;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void ResetSession(){launched=false;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  static void Isolate(){if(NocturnisCinematic.SessionQA)KryptaGame.Testing=true;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Launch(){
   if(!NocturnisCinematic.SessionQA||launched)return;launched=true;
   var go=new GameObject("Opening session regression");DontDestroyOnLoad(go);go.AddComponent<NocturnisStartupSessionQA>();
  }
  IEnumerator Start(){
   var result=new Result();string folder=Arg("-cinematic-session-output","Logs/StartupSessionQA");Directory.CreateDirectory(folder);
   string[] production={"Nocturnis.Checkpoint.v1","Nocturnis.Checkpoint.v2","Nocturnis.Profile1.Checkpoint","Nocturnis.Profile2.Checkpoint","Nocturnis.Profile3.Checkpoint","Nocturnis.SharedStash.v1"};
   var snapshots=new Dictionary<string,string>();foreach(string key in production)snapshots[key]=PlayerPrefs.GetString(key,"");
   const string qa="Nocturnis.QA.Checkpoint";bool hadQa=PlayerPrefs.HasKey(qa);string oldQa=PlayerPrefs.GetString(qa,"");int oldProfile=PlayerPrefs.GetInt("Nocturnis.QA.ActiveProfile",1);
   KryptaGame game=null;float deadline=Time.realtimeSinceStartup+30;
   while(Time.realtimeSinceStartup<deadline){game=FindFirstObjectByType<KryptaGame>();if(game&&game.View)break;yield return null;}
   if(!game||!game.View){result.error="Game bootstrap timeout";Write(folder,result,hadQa,oldQa,oldProfile);yield break;}
   result.initialOpened=NocturnisCinematic.Active;
   deadline=Time.realtimeSinceStartup+15;
   while(NocturnisCinematic.Active&&Time.realtimeSinceStartup<deadline){
    if(NocturnisCinematic.DecodedFrame>2){result.decoded=true;NocturnisCinematic.Skip();}
    yield return null;
   }
   yield return null;
   result.firstMenu=!NocturnisCinematic.Active&&game.State==KryptaGame.Phase.Title&&Time.timeScale==0&&!AudioListener.pause;
   if(!result.firstMenu){result.error="Initial intro failed to return to menu";Write(folder,result,hadQa,oldQa,oldProfile);yield break;}
   game.SelectProfile(1);game.SelectHero(0);game.Begin();yield return null;
   int instance=game.GetInstanceID();game.SaveAndReturnToTitle();game=null;
   deadline=Time.realtimeSinceStartup+30;
   while(Time.realtimeSinceStartup<deadline){
    game=FindFirstObjectByType<KryptaGame>();
    if(game&&game.GetInstanceID()!=instance&&game.View)break;
    yield return null;
   }
   result.actualSceneReload=game&&game.GetInstanceID()!=instance&&game.View;
   if(!result.actualSceneReload){result.error="SaveAndReturnToTitle did not replace the game scene";Write(folder,result,hadQa,oldQa,oldProfile);yield break;}
   result.noReplayAfterReload=!NocturnisCinematic.Active&&game.State==KryptaGame.Phase.Title&&Time.timeScale==0;
   float watchUntil=Time.realtimeSinceStartup+1;
   while(Time.realtimeSinceStartup<watchUntil){result.noReplayAfterReload&=!NocturnisCinematic.Active;yield return null;}
   if(NocturnisCinematic.Active)NocturnisCinematic.Skip();
   yield return null;bool loaded=game.ContinueCheckpoint();yield return null;
   result.continueDidNotReplay=loaded&&!NocturnisCinematic.Active&&game.State==KryptaGame.Phase.Playing&&Time.timeScale==1;
   game.SaveAndReturnToTitle();game=null;yield return null;deadline=Time.realtimeSinceStartup+30;
   while(Time.realtimeSinceStartup<deadline){game=FindFirstObjectByType<KryptaGame>();if(game&&game.View&&game.State==KryptaGame.Phase.Title)break;yield return null;}
   if(game&&game.View){game.SelectHero(4);game.Begin();yield return null;result.newGameDidNotReplay=!NocturnisCinematic.Active&&game.SelectedHero==4&&game.State==KryptaGame.Phase.Playing;}
   result.productionSavesUntouched=true;foreach(string key in production)result.productionSavesUntouched&=PlayerPrefs.GetString(key,"")==snapshots[key];
   result.passed=result.initialOpened&&result.decoded&&result.firstMenu&&result.actualSceneReload&&result.noReplayAfterReload&&result.continueDidNotReplay&&result.newGameDidNotReplay&&result.productionSavesUntouched;
   Write(folder,result,hadQa,oldQa,oldProfile);
  }
  static void Write(string folder,Result result,bool hadQa,string oldQa,int oldProfile){
   if(hadQa)PlayerPrefs.SetString("Nocturnis.QA.Checkpoint",oldQa);else PlayerPrefs.DeleteKey("Nocturnis.QA.Checkpoint");
   PlayerPrefs.SetInt("Nocturnis.QA.ActiveProfile",oldProfile);PlayerPrefs.Save();
   File.WriteAllText(Path.Combine(folder,"result.json"),JsonUtility.ToJson(result,true));
   Debug.Log("STARTUP_SESSION_QA "+JsonUtility.ToJson(result));Application.Quit(result.passed?0:1);
  }
 }
}
