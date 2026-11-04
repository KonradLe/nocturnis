using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Krypta.Editor
{
 public static class NocturnisDestructionQA
 {
  const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;static int step,checks;static bool errors;static double started,next;
  [InitializeOnLoadMethod]static void Restore(){if(!SessionState.GetBool("Nocturnis.DestructionQA",false))return;started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
  public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool("Nocturnis.DestructionQA",true);EditorApplication.isPlaying=true;}
  static void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors=true;}
  static void Check(bool yes,string why){checks++;if(!yes)throw new Exception(why);}
  static object Call(KryptaGame g,string name,params object[] args){return typeof(KryptaGame).GetMethod(name,P).Invoke(g,args);}
  static object Field(KryptaGame g,string name){return typeof(KryptaGame).GetField(name,P).GetValue(g);}
  static void Set(KryptaGame g,string name,object value){typeof(KryptaGame).GetProperty(name).SetValue(g,value);}
  static void Pos(KryptaGame g,Vector3 p){((ActorVisual)Field(g,"player")).Root.transform.position=p;}
  static void Tick()
  {
   if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.15;
   var g=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!g||!g.View)return;
   if(EditorApplication.timeSinceStartup-started>210){Finish(false,"Timeout");return;}
   try
   {
    KryptaGame.Testing=true;
    if(step==0)
    {
     g.Begin();int count=0,dropMaps=0;
     for(int z=0;z<Campaign.Count;z++)
     {
      Call(g,"LoadZone",z);var grid=(DungeonGrid)Field(g,"grid");var spawn=g.SpawnPosition;grid.BuildDistances(Mathf.RoundToInt(spawn.x/1.8f),Mathf.RoundToInt(spawn.z/1.8f));
      for(int x=0;x<DungeonGrid.Width;x++)for(int y=0;y<DungeonGrid.Height;y++)if(grid.Walkable(x,y))Check(grid.Distance(x,y)>=0,"Props severed navigation "+z+"/"+x+"/"+y);
      var points=g.ReviewContainerPositions();count+=points.Length;
      if(z!=Campaign.Town && z!=Campaign.Heart){Check(points.Length>=5,"Containers populated map "+z);dropMaps++;}
      foreach(var p in points)Check(!g.MapFloor(Mathf.RoundToInt(p.x/1.8f),Mathf.RoundToInt(p.z/1.8f)),"Intact container blocks its own cell");
      foreach(var e in g.enemies)Check(g.MapFloor(Mathf.RoundToInt(e.Position.x/1.8f),Mathf.RoundToInt(e.Position.z/1.8f)),"Prop overlaps enemy "+z);
      foreach(var point in new[]{g.SpawnPosition,g.ExitPosition})Check(g.MapFloor(Mathf.RoundToInt(point.x/1.8f),Mathf.RoundToInt(point.z/1.8f)),"Prop overlaps entrance/exit");
     }
     Check(count>=100 && dropMaps==Campaign.Count-2,"World contains numerous destructibles");
    }
    else if(step==1)
    {
     Call(g,"LoadZone",Campaign.Fields);var points=g.ReviewContainerPositions();
     Check(!g.ReviewBreakContainer(0,1),"First light hit cracks rather than deletes container");
     var objects=(IList)Field(g,"containers");var first=objects[0];Check((bool)first.GetType().GetField("cracked").GetValue(first),"Crack geometry state");
     for(int hero=0;hero<5;hero++)
     {
      Set(g,"State",KryptaGame.Phase.Title);g.SelectHero(hero);g.Resume();Vector3 target=points[hero];Vector3 start=target;
      foreach(var d in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right}){Vector3 test=target+d*1.8f;if(g.MapFloor(Mathf.RoundToInt(test.x/1.8f),Mathf.RoundToInt(test.z/1.8f))){start=test;break;}}
      Pos(g,start);for(int hit=0;hit<8;hit++){typeof(KryptaGame).GetField("strikeAt",P).SetValue(g,0f);Call(g,"Strike",target);g.ReviewCompleteStrike();}
      Check(g.MapFloor(Mathf.RoundToInt(target.x/1.8f),Mathf.RoundToInt(target.z/1.8f)),"Class basic attack breaks prop "+hero);
     }
     Pos(g,g.SpawnPosition);
     for(int i=0;i<points.Length;i++)g.ReviewBreakContainer(i,1000);
     Check(g.ContainersStanding==0,"All breakable hitpoints depleted");Check(g.ContainerLootOnGround>0 && g.ContainerLootOnGround<points.Length,"Only occasional container loot");
     int pending=g.ContainerLootOnGround;string[] ids=g.CaptureBrokenContainers();Call(g,"SaveCheckpoint");Check(g.ContinueCheckpoint(),"Container checkpoint exists");
     Check(g.ContainersStanding==0 && g.ContainerLootOnGround==pending,"Broken state and pending loot restored");
     Check(points.SequenceEqual(g.ReviewContainerPositions()),"Container placement IDs deterministic after break/reload");Check(ids.SequenceEqual(g.CaptureBrokenContainers()),"No new loot roll on reload");
     Call(g,"LoadZone",Campaign.Cemetery);Call(g,"LoadZone",Campaign.Fields);Check(points.SequenceEqual(g.ReviewContainerPositions()),"Placement stable across travel");Check(g.ContainerLootOnGround==pending,"Travel preserves uncollected container loot");
     foreach(var p in points){Pos(g,p);Call(g,"UpdateContainerLoot");}
     Check(g.ContainerLootOnGround==0,"Container loot actually collects");int coins=g.Coins,materials=g.Materials,items=g.Inventory.Count,potions=g.Potions;
     Check(g.ContinueCheckpoint(),"Loot autosaved");Check(g.ContainerLootOnGround==0 && g.ContainersStanding==0,"Claimed loot does not duplicate");
     Check(g.Coins==coins&&g.Materials==materials&&g.Inventory.Count==items&&g.Potions==potions,"Container rewards persisted exactly");
    }
    else if(step==2)
    {
     // A revived enemy cannot grant repeated XP/materials/drops.
     Call(g,"LoadZone",Campaign.Kings);var e=g.enemies.First(x=>x.hp>0 && !x.boss);Call(g,"Damage",e,99999f);
     int defeated=g.Defeated,materials=g.Materials,xp=g.Experience;int coins=((IList)Field(g,"pickups")).Count,relics=((IList)Field(g,"relics")).Count;
     e.hp=e.maxHp*.5f;e.visual.Root.SetActive(true);Call(g,"Damage",e,99999f);
     Check(e.hp==0&&!e.visual.Root.activeSelf,"Revived enemy dies normally");Check(g.Defeated==defeated&&g.Materials==materials&&g.Experience==xp,"Revival cannot farm progress");
     Check(((IList)Field(g,"pickups")).Count==coins&&((IList)Field(g,"relics")).Count==relics,"Revival cannot duplicate drops");
    }
    else {Finish(!errors,"PASS "+checks+" assertions: reachable maps with blockers; five real basic attacks; cracks/debris; occasional loot; deterministic placement; break/pending/claimed save and travel; resurrection rewards.");return;}
    step++;
   }
   catch(Exception e){Finish(false,e.ToString());}
  }
  static void Finish(bool ok,string text){SessionState.SetBool("Nocturnis.DestructionQA",false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Directory.CreateDirectory("Logs");File.WriteAllText("Logs/destruction-qa-result.txt",(ok?"PASS: ":"FAIL: ")+text);Debug.Log(text);EditorApplication.Exit(ok?0:1);}
 }
}
