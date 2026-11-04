using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace Krypta.Editor
{
 // Plays the first chapter through the actual controller, skills and interactions.
 // No teleportation, enemy-health edits or free healing. Editor-only QA.
 [InitializeOnLoad]public static class NocturnisJourney
 {
  const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
  static bool active,begin;
  static double started,next,telemetryAt;
  static int zone=-1,kills=-1;
  static float distance;
  static Vector3 previous;
  static NocturnisJourney(){EditorApplication.update+=Tick;if(SessionState.GetBool("Nocturnis.JourneyBatch",false)){active=begin=true;started=EditorApplication.timeSinceStartup;}}
  public static void RunBatch(){KryptaSetup.Prepare();UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool("Nocturnis.JourneyBatch",true);EditorApplication.isPlaying=true;}
  public static void Start(KryptaGame game)
  {
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/journey-result.txt","RUNNING: normal movement and combat\n");
   KryptaGame.Testing=true;active=true;begin=true;zone=-1;kills=-1;distance=0;started=EditorApplication.timeSinceStartup;next=started+1;game.Restart();
  }
  static void Set(KryptaGame g,string name,object value){typeof(KryptaGame).GetField(name,Private).SetValue(g,value);}
  static void Walk(KryptaGame g,int x,int z)
  {
   Set(g,"target",null);Set(g,"destination",new Vector3(x*1.8f,0,z*1.8f));Set(g,"hasDestination",true);
   ((DungeonGrid)typeof(KryptaGame).GetField("playerRoute",Private).GetValue(g)).BuildDistances(x,z);
  }
  static void Finish(KryptaGame g,bool success,string message)
  {
   active=false;Time.timeScale=1;
   File.AppendAllText("Logs/journey-result.txt",(success ? "PASS: " : "FAIL: ")+message+"; walked="+distance.ToString("0.0")+"m; kills="+g.Defeated+"; health="+g.Health.ToString("0")+"; potions="+g.Potions+"; elapsed="+g.Elapsed.ToString("0.0")+"s\n");
   if(SessionState.GetBool("Nocturnis.JourneyBatch",false)){SessionState.SetBool("Nocturnis.JourneyBatch",false);EditorApplication.Exit(success ? 0 : 1);}
  }
  static void Tick()
  {
   if(!active || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;
   next=EditorApplication.timeSinceStartup+.1;
   var g=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!g || !g.View)return;
   try
   {
    if(begin){if(g.State!=KryptaGame.Phase.Title)return;begin=false;KryptaGame.Testing=true;Directory.CreateDirectory("Logs");File.WriteAllText("Logs/journey-result.txt","RUNNING: Act I movement, combat, two optional branches; no teleport or health edits\n");g.Begin();previous=g.HeroPosition;}
    if(EditorApplication.timeSinceStartup>telemetryAt){telemetryAt=EditorApplication.timeSinceStartup+5;File.WriteAllText("Logs/journey-position.txt","Zone="+g.Zone+" state="+g.State+" position="+g.HeroPosition+" remaining="+g.Remaining+" exit="+g.CanExit+" time="+g.Elapsed+" dest="+typeof(KryptaGame).GetField("destination",Private).GetValue(g)+" waves="+typeof(KryptaGame).GetField("graveWaves",Private).GetValue(g));}
    if(EditorApplication.timeSinceStartup-started>1100){Finish(g,false,"Timeout");return;}
    if(g.State==KryptaGame.Phase.Won){Finish(g,true,"Act I completed with normal movement, resources and combat");return;}
    if(g.State==KryptaGame.Phase.Dead){Finish(g,false,"Hero died during normal playthrough");return;}
    if(zone==g.Zone)distance+=Vector3.Distance(previous,g.HeroPosition);previous=g.HeroPosition;
    if(zone!=g.Zone || kills!=g.Defeated)
    {zone=g.Zone;kills=g.Defeated;File.AppendAllText("Logs/journey-result.txt","Zone="+zone+" kills="+kills+" HP="+g.Health.ToString("0")+"\n");}
    if(g.State==KryptaGame.Phase.Dialogue)g.Resume();
    Time.timeScale=Application.isBatchMode ? 6 : 3;
    if(g.Zone==0)
    {
     Walk(g,g.QuestAccepted ? 20 : 15,g.QuestAccepted ? 10 : 7);
     if(Vector3.Distance(g.HeroPosition,g.QuestAccepted ? new Vector3(36,0,18) : new Vector3(27,0,14.4f))<2.6f)g.Interact();
     return;
    }
    if(g.Health<g.MaxHealth*.85f)g.CastDefense();
    if(g.Health<g.MaxHealth*.5f)g.UsePotion();
    KryptaGame.Enemy target=null;float nearest=float.MaxValue;
    foreach(var e in g.enemies)if(e.hp>0){float d=Vector3.Distance(e.Position,g.HeroPosition);if(d<nearest){nearest=d;target=e;}}
    // Focus resurrection priests and sacrifices when in reachable combat range.
    var priority=g.enemies.Find(e=>e.hp>0 && (e.archetype==7 || e.sacrificial) && Vector3.Distance(e.Position,g.HeroPosition)<8);
    if(priority!=null){target=priority;nearest=Vector3.Distance(target.Position,g.HeroPosition);}
    if(target!=null)
    {Set(g,"hasDestination",false);Set(g,"target",target);if(nearest<3.3f)g.CastSkill(target.Position);if(nearest<7){g.CastAdditional(0,target.Position);g.CastAdditional(1,target.Position);g.CastAdditional(2,target.Position);}}
    else
    {
     Vector3 goal=g.ExitPosition;
     if(g.Zone==Campaign.Cemetery && !g.CanExit)goal=new Vector3(21.6f,0,36);
     if(g.Zone==Campaign.Fields && (g.Cleared&(1<<Campaign.Forest))==0)goal=new Vector3(5.4f,0,36);
     else if(g.Zone==Campaign.Fields && (g.Cleared&(1<<Campaign.Cellar))==0)goal=new Vector3(36,0,27);
     Walk(g,Mathf.RoundToInt(goal.x/1.8f),Mathf.RoundToInt(goal.z/1.8f));if(Vector3.Distance(g.HeroPosition,goal)<2.6f)g.Interact();
    }
   }
   catch(Exception e){Finish(g,false,e.ToString());}
  }
 }
}
