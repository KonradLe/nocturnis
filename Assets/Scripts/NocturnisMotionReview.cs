using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Krypta {
 public sealed class NocturnisMotionReview : MonoBehaviour {
  string directory,error;float started;bool finished;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Launch(){
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"-motion-review")<0)return;
   KryptaGame.Testing=true;new GameObject("Motion review opt-in").AddComponent<NocturnisMotionReview>();
  }
  IEnumerator Start(){
   started=Time.realtimeSinceStartup;Time.maximumDeltaTime=1f/30f;Application.logMessageReceived+=Log;
   var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-review-output");
   directory=Path.GetFullPath(at>=0 && at+1<args.Length ? args[at+1] : "Logs/MotionReview");Directory.CreateDirectory(directory);
   KryptaGame game;while(!(game=FindFirstObjectByType<KryptaGame>()) || !game.View)yield return null;
   yield return game.CaptureMotionReview(directory);
   if(error!=null){File.WriteAllText(Path.Combine(directory,"failure.txt"),error);finished=true;Application.Quit(1);yield break;}
   finished=true;Application.logMessageReceived-=Log;Application.Quit(0);
  }
  void Log(string m,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)error=m+"\n"+stack;}
  void Update(){if(finished || directory==null || (error==null && Time.realtimeSinceStartup-started<480))return;
   finished=true;StopAllCoroutines();File.WriteAllText(Path.Combine(directory,"failure.txt"),error ?? "Motion review timeout");Application.Quit(1);}
 }
 public sealed partial class KryptaHUD {
  public ActorVisual ReviewMotionHero(int hero){if(!KryptaGame.Testing)return null;game.SelectHero(hero);menuPage=MenuPage.Characters;Preview();return model;}
 }
 public sealed partial class KryptaGame {
  [Serializable]sealed class MotionEvidence {public int hero;public float pausedIdleDegrees,travelDistance,runSpeed,contactPhase;public string locomotionClip,contactClip;}
  [Serializable]sealed class MotionResult {public bool passed;public string note="Isolated runtime QA: actual paused menu animation, distance-driven gameplay gait and normal strike actions. Enemies frozen for captures.";public List<MotionEvidence> heroes=new List<MotionEvidence>();public List<string> screenshots=new List<string>();}
  MotionResult motionResult;
  IEnumerator MotionFrame(string directory,string name){
   float previous=Time.timeScale;Time.timeScale=0;yield return new WaitForEndOfFrame();
   string filename=name+".png",path=Path.Combine(directory,filename);if(File.Exists(path))File.Delete(path);ScreenCapture.CaptureScreenshot(path);
   for(int i=0;i<3;i++)yield return new WaitForEndOfFrame();
   float end=Time.realtimeSinceStartup+8;while(!File.Exists(path) && Time.realtimeSinceStartup<end)yield return null;
   if(!File.Exists(path) || new FileInfo(path).Length<1500)throw new Exception("Missing rendered frame "+name);
   motionResult.screenshots.Add(filename);Time.timeScale=previous;
  }
  public IEnumerator CaptureMotionReview(string directory){
   if(!Testing)yield break;motionResult=new MotionResult();var hud=GetComponent<KryptaHUD>();
   File.WriteAllText(Path.Combine(directory,"ready.txt"),"Runtime review started");
   for(int hero=0;hero<5;hero++){
    State=Phase.Title;Time.timeScale=0;var portrait=hud.ReviewMotionHero(hero);
    yield return new WaitForSecondsRealtime(.25f);
    var chest=portrait.Root.GetComponentsInChildren<Transform>().First(t=>t.name=="chest");Quaternion initial=chest.localRotation;
    yield return MotionFrame(directory,"class-"+hero);
    float degrees=0;
    for(int sample=0;sample<5;sample++){yield return new WaitForSecondsRealtime(.25f);degrees=Mathf.Max(degrees,Quaternion.Angle(initial,chest.localRotation));}
    if(degrees<.15f)throw new Exception("Paused menu has no skeletal idle: "+hero+" delta="+degrees);
    motionResult.heroes.Add(new MotionEvidence{hero=hero,pausedIdleDegrees=degrees});
    yield return MotionFrame(directory,"class-"+hero+"-idle");
   }
   for(int hero=0;hero<5;hero++){
    PrepareReviewScene(Campaign.Fields,hero,1);ReviewPosition(World(12,17));
    yield return new WaitForSeconds(.2f);Vector3 start=HeroPosition;bool found=false;
    foreach(var d in new[]{Vector3.forward,Vector3.right,Vector3.back,Vector3.left}){
     Vector3 end=start+d*4;if(!grid.CanTravel(start.x/Tile,start.z/Tile,end.x/Tile,end.z/Tile,.32f/Tile))continue;
     destination=end;hasDestination=true;playerRoute.BuildDistances(Cell(end.x),Cell(end.z));found=true;break;
    }
    if(!found)throw new Exception("No locomotion review path");
    float walkDeadline=Time.time+1.5f;
    while(Time.time<walkDeadline && (Vector3.Distance(start,HeroPosition)<.4f || player.LocomotionSpeed<1 || player.CurrentClip!="Running_A"))yield return null;
    var record=motionResult.heroes[hero];record.travelDistance=Vector3.Distance(start,HeroPosition);record.runSpeed=player.LocomotionSpeed;record.locomotionClip=player.CurrentClip;
    if(record.travelDistance<.4f || record.runSpeed<1 || record.locomotionClip!="Running_A")throw new Exception("Gait not following movement: "+hero+" distance="+record.travelDistance+" speed="+record.runSpeed+" clip="+record.locomotionClip+" destination="+hasDestination+" dt="+Time.deltaTime);
    yield return MotionFrame(directory,"walk-"+hero);yield return new WaitForSeconds(.4f);hasDestination=false;target=null;
    yield return new WaitForSeconds(.3f);facing=Vector3.back;player.Root.transform.rotation=Quaternion.LookRotation(facing);
    strikeAt=0;Strike(HeroPosition+facing*Hero.range);
    yield return new WaitForSeconds(.07f);yield return MotionFrame(directory,"attack-"+hero+"-windup");
    float until=Time.time+.8f;while(MeleeWindupActive && Time.time<until)yield return null;
    if(MeleeWindupActive)throw new Exception("Contact did not resolve");
    record.contactPhase=player.CurrentClipNormalizedTime;record.contactClip=player.CurrentClip;
    if(record.contactPhase<.35f || record.contactPhase>.58f || (record.contactClip!="1H_Ranged_Shoot" && record.contactClip!="1H_Melee_Attack_Slice_Horizontal"))throw new Exception("Captured wrong contact phase: "+hero+" "+record.contactClip+" "+record.contactPhase);
    yield return MotionFrame(directory,"attack-"+hero+"-contact");
    yield return new WaitForSeconds(.14f);yield return MotionFrame(directory,"attack-"+hero+"-followthrough");
    yield return new WaitForSeconds(.4f);
   }
   yield return CaptureSurfaceReview(Path.Combine(directory,"surfaces"));
   motionResult.passed=true;File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(motionResult,true));Debug.Log("MOTION_REVIEW_PASS");
  }
 }
}
