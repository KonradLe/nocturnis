using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Krypta
{
 // Opt-in only: a standalone capture rig. No component or stat changes without the launch flag.
 public sealed class NocturnisAct1VisualReview : MonoBehaviour
 {
  KryptaGame game;string directory,error;bool finished;float started;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Launch()
  {
   string[] args=Environment.GetCommandLineArgs();if(Array.IndexOf(args,"-act1-visual-review")<0)return;
   KryptaGame.Testing=true;new GameObject("Act I opt-in visual review").AddComponent<NocturnisAct1VisualReview>();
  }
  IEnumerator Start()
  {
   started=Time.realtimeSinceStartup;Application.logMessageReceived+=OnLog;
   string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-review-output");directory=at>=0 && at+1<args.Length ? args[at+1] : "Logs/Act1VisualReview";
   directory=Path.GetFullPath(directory);Directory.CreateDirectory(directory);
   while(!(game=FindFirstObjectByType<KryptaGame>()) || !game.View)yield return null;
   yield return game.CaptureAct1VisualReview(directory);
   if(!finished){finished=true;Application.logMessageReceived-=OnLog;Application.Quit(0);}
  }
  void OnLog(string message,string stack,LogType type)
  {if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)error=message+"\n"+stack;}
  void Update()
  {
   if(finished || directory==null)return;
   if(string.IsNullOrEmpty(error) && Time.realtimeSinceStartup-started<240)return;
   finished=true;StopAllCoroutines();Application.logMessageReceived-=OnLog;
   File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new Failure{passed=false,error=error ?? "Visual review timed out"},true));Application.Quit(1);
  }
  [Serializable]sealed class Failure{public bool passed;public string error;}
 }
 public sealed partial class KryptaGame
 {
  [Serializable]sealed class Act1VisualResult
  {public bool passed;public string note="Screenshots from real runtime actions in an isolated opt-in QA run; review framing and character preparation are not a normal player session.";public List<string> screenshots=new List<string>();public List<Act1VisualFrame> frames=new List<Act1VisualFrame>();}
  [Serializable]sealed class Act1VisualFrame
  {public string filename;public int zone,hero,activeVfx,containersStanding,containerLoot,castSlot;public bool castSucceeded;public string castNotice;}
  Act1VisualResult act1VisualResult;
  bool reviewCaptureFreezeEnemies;Enemy reviewCaptureActiveGuardian;
  void PrepareReviewScene(int zone,int hero,int level=12)
  {
   if(!Testing)throw new InvalidOperationException("Review scene is available only in QA mode.");
   reviewCaptureFreezeEnemies=true;reviewCaptureActiveGuardian=null;
   Time.timeScale=1;State=Phase.Playing;SelectedHero=hero;Level=level;Resource=100;Shadow=100;WeaponRank=ArmorRank=CharmRank=Gems=0;Inventory.Clear();ResetCombatActions();
   ResetWorldLoot();ResetBreakablesProgress();LoadZone(zone);Health=MaxHealth;introUntil=0;hasDestination=false;target=null;cameraZoom=8.1f;
   // Freeze opponents in the capture rig; casts still execute their actual costs, cooldowns and damage.
   foreach(var e in enemies){e.alerted=false;e.stunUntil=0;}
   ReducedFlashes=true;
  }
  void ReviewPosition(Vector3 p)
  {
   var cell=grid.NearestWalkable(Cell(p.x),Cell(p.z));player.Root.transform.position=World(cell.X,cell.Z);
   hasDestination=false;target=null;facing=Vector3.forward;player.Root.transform.rotation=Quaternion.LookRotation(facing);
   View.transform.position=HeroPosition+new Vector3(9,15,-11);View.transform.LookAt(HeroPosition);
  }
  Vector3 ReviewFloorNear(Vector3 point,float distance)
  {
   for(int i=0;i<16;i++)
   {
    float a=i*Mathf.PI/8;Vector3 p=point+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*distance;
    var cell=grid.NearestWalkable(Cell(p.x),Cell(p.z));p=World(cell.X,cell.Z);
    if(Vector3.Distance(p,point)<distance*.7f || Vector3.Distance(p,point)>distance+1.5f)continue;
    if(Visible(p,point))return p;
   }
   var fallback=grid.NearestWalkable(Cell(point.x),Cell(point.z)-2);return World(fallback.X,fallback.Z);
  }
  IEnumerator SaveReviewFrame(string directory,string name)
  {
   Time.timeScale=0;yield return new WaitForEndOfFrame();
   string filename=name+".png",path=Path.Combine(directory,filename);
   if(File.Exists(path))File.Delete(path);
   ScreenCapture.CaptureScreenshot(path);
   for(int i=0;i<4;i++)yield return new WaitForEndOfFrame();
   float until=Time.realtimeSinceStartup+8;while(!File.Exists(path) && Time.realtimeSinceStartup<until)yield return new WaitForSecondsRealtime(.1f);
   if(!File.Exists(path) || new FileInfo(path).Length<1000)throw new InvalidOperationException("Screenshot was not written: "+filename);
   act1VisualResult.screenshots.Add(filename);act1VisualResult.frames.Add(new Act1VisualFrame{filename=filename,zone=Zone,hero=SelectedHero,activeVfx=ActiveCraftedVfx,containersStanding=ContainersStanding,containerLoot=ContainerLootOnGround,castSlot=LastCastSlot,castSucceeded=CastNoticeSucceeded,castNotice=CastNotice});
   File.WriteAllText(Path.Combine(directory,"progress.json"),JsonUtility.ToJson(act1VisualResult,true));
   Debug.Log("ACT1_VISUAL_FRAME "+filename);Time.timeScale=1;
  }
  public IEnumerator CaptureAct1VisualReview(string directory)
  {
   if(!Testing)yield break;act1VisualResult=new Act1VisualResult();
   State=Phase.Title;Begin();
   for(int hero=0;hero<5;hero++)
   {
    PrepareReviewScene(Campaign.Fields,hero);ReviewPosition(World(12,17));
    yield return new WaitForSeconds(.25f);
    ReviewCastVfx(1,HeroPosition+Vector3.forward*2);
    if(!CastNoticeSucceeded)throw new InvalidOperationException("Class primary cast failed: "+hero+" "+CastNotice);
    if(hero==3){yield return new WaitForSeconds(.2f);yield return SaveReviewFrame(directory,"spells-3-telegraph");yield return new WaitForSeconds(.85f);}else yield return new WaitForSeconds(.22f);yield return SaveReviewFrame(directory,"spells-"+hero);
    ResetCraftedVfx();Resource=100;Shadow=100;ReviewCastVfx(2,HeroPosition);
    yield return new WaitForSeconds(.25f);yield return SaveReviewFrame(directory,"defense-"+hero);
   }
   // Use a real crate with deterministic drops and the normal low-level melee strike.
   PrepareReviewScene(Campaign.Cellar,0,1);
   Breakable prop=containers.Find(b=>!b.broken && b.kind==2 && b.roll<35) ?? containers.Find(b=>!b.broken && b.kind==2) ?? containers.Find(b=>!b.broken);
   if(prop==null)throw new InvalidOperationException("No container for the visual review.");
   Vector3 approach=prop.p;
   foreach(var offset in new[]{Vector2Int.down,Vector2Int.left,Vector2Int.right,Vector2Int.up})if(grid.Walkable(prop.x+offset.x,prop.z+offset.y)){approach=World(prop.x+offset.x,prop.z+offset.y);break;}
   ReviewPosition(approach);facing=(prop.p-HeroPosition).normalized;player.Root.transform.rotation=Quaternion.LookRotation(facing);cameraZoom=7.1f;
   yield return new WaitForSeconds(.2f);yield return SaveReviewFrame(directory,"props-before");
   ReviewCastVfx(0,prop.p);yield return new WaitForSeconds(.08f);
   if(!prop.broken)yield return SaveReviewFrame(directory,"props-cracked");
   for(int attempt=0;!prop.broken && attempt<6;attempt++){yield return new WaitForSeconds(.55f);ReviewCastVfx(0,prop.p);}
   if(!prop.broken)throw new InvalidOperationException("Normal strikes did not break the selected prop.");
   yield return new WaitForSeconds(.08f);yield return SaveReviewFrame(directory,"props-after");
   for(int zone=14;zone<=18;zone++)
   {
    PrepareReviewScene(zone,0);ReviewPosition(World(12,18));yield return new WaitForSeconds(.25f);
    yield return SaveReviewFrame(directory,"map-"+zone);
    var guardian=enemies.Find(e=>e.spawnId==25);if(guardian==null)throw new InvalidOperationException("Stage guardian missing: "+zone);
    ReviewPosition(ReviewFloorNear(guardian.Position,4));guardian.stunUntil=0;guardian.alerted=false;reviewCaptureActiveGuardian=guardian;
    if(!ReviewStartGuardianAttack(guardian,HeroPosition))throw new InvalidOperationException("Guardian review attack unavailable: "+zone);
    yield return new WaitForSeconds(.35f);yield return SaveReviewFrame(directory,"guardian-"+zone);
   }
   for(int zone=0;zone<14;zone++){
    PrepareReviewScene(zone,0);ReviewPosition(zone==Campaign.Town ? World(12,9) : World(12,18));yield return new WaitForSeconds(.25f);yield return SaveReviewFrame(directory,"map-"+zone);
   }
   act1VisualResult.passed=true;File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(act1VisualResult,true));
   Debug.Log("ACT1_VISUAL_REVIEW_PASS "+act1VisualResult.screenshots.Count+" screenshots");
  }
 }
}
