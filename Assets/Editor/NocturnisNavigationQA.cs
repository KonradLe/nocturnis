using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
#endif
namespace Krypta.Editor
{
 // Runs against the production grid both inside Unity and in a standalone C# check.
 public static class NocturnisNavigationQA
 {
  static int checks,routes;const float Tile=1.8f,Body=.28f/Tile,Clearance=.32f/Tile;
  static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
  static float Length(float x,float z){return (float)Math.Sqrt(x*x+z*z);}
  static DungeonGrid Empty()
  {var g=new DungeonGrid(Campaign.Town);for(int x=1;x<DungeonGrid.Width-1;x++)for(int z=1;z<DungeonGrid.Height-1;z++)g.CloseCell(x,z);return g;}
  static void Room(DungeonGrid g,int x1,int z1,int x2,int z2)
  {for(int x=x1;x<=x2;x++)for(int z=z1;z<=z2;z++)g.OpenCell(x,z);}
  static void Follow(DungeonGrid g,float x,float z,float gx,float gz,float dt,string label)
  {
   g.BuildDistances((int)Math.Floor(gx+.5f),(int)Math.Floor(gz+.5f));
   Check(g.CanStand(x,z,Body),label+": invalid start");Check(g.CanStand(gx,gz,Clearance),label+": invalid goal");
   float walked=0;int frames=0;int bfs=g.Distance((int)Math.Floor(x+.5f),(int)Math.Floor(z+.5f));
   Check(bfs>=0,label+": unreachable fixture");
   while(Length(gx-x,gz-z)>.07f && frames++<10000)
   {
    float wx,wz;Check(g.TryWaypoint(g,x,z,gx,gz,Clearance,out wx,out wz),label+": no waypoint at "+x+","+z);
    float dx=wx-x,dz=wz-z,len=Length(dx,dz);Check(len>.00001f,label+": stationary waypoint");
    float step=Math.Min(4.8f/Tile*dt,len);dx*=step/len;dz*=step/len;
    int slices=Math.Max(1,(int)Math.Ceiling(step*Tile/.12f));float sx=dx/slices,sz=dz/slices;
    float beforeX=x,beforeZ=z;
    for(int i=0;i<slices;i++)
    {
     if(g.CanStand(x+sx,z,Body))x+=sx;if(g.CanStand(x,z+sz,Body))z+=sz;
     Check(g.CanStand(x,z,Body),label+": crossed collision");
    }
    float moved=Length(x-beforeX,z-beforeZ);Check(moved<=step+.0001f,label+": speed exceeded");walked+=moved;
   }
   Check(frames<10000,label+": chase loops");
   Check(walked<Math.Max(5,bfs*1.5f+4),label+": excessive detour "+walked+" vs BFS "+bfs);routes++;
  }
  public static string RunCore()
  {
   checks=routes=0;
   var cloister=new DungeonGrid(Campaign.Cloister);
   foreach(float dt in new[]{1f/60,.1f,1f/3})
    Follow(cloister,15.6773901f/Tile,29.0390549f/Tile,9.6335926f/Tile,30.1452942f/Tile,dt,"reported Cloister position dt="+dt);
   var corner=Empty();Room(corner,2,2,9,4);Room(corner,7,3,9,12);Room(corner,3,10,8,12);corner.CloseCell(8,6);
   foreach(float dt in new[]{1f/60,.1f,1f/3})
   {Follow(corner,2.3f,2.2f,3,11,dt,"staggered corner + obstacle");Follow(corner,3,11,2.3f,2.2f,dt,"reverse corner + obstacle");}
   var margin=Empty();Room(margin,1,1,5,5);margin.CloseCell(3,2);
   float edge=2.5f-Body-.001f;
   Check(margin.CanStand(edge,2,Body) && !margin.CanStand(edge,2,Clearance),"Body/route margin regression fixture");
   Follow(margin,edge,2,2,4,1f/3,"recover from manual wall clearance");
   var live=Empty();Room(live,2,2,8,8);var stale=Empty();Room(stale,2,2,8,8);stale.BuildDistances(7,4);live.CloseCell(5,4);
   float wx1,wz1;Check(live.TryWaypoint(stale,3,4,7,4,Clearance,out wx1,out wz1),"Live blocker still permits a safe approach");
   Check(live.CanTravel(3,4,wx1,wz1,Clearance),"Stale distance field steered through new live blocker");
   for(int zone=0;zone<Campaign.Count;zone++)
   {
    var g=new DungeonGrid(zone);var start=g.NearestWalkable(zone==Campaign.Road||zone==Campaign.Cemetery||zone==Campaign.Heart?12:5,3);
    var end=g.NearestWalkable(zone==Campaign.Town?11:17,zone==Campaign.Town?5:38);
    foreach(float dt in new[]{1f/60,.1f,1f/3})
    {Follow(g,start.X,start.Z,end.X,end.Z,dt,"zone "+zone+" forward");Follow(g,end.X,end.Z,start.X,start.Z,dt,"zone "+zone+" reverse");}
   }
   var disconnected=Empty();disconnected.OpenCell(2,2);disconnected.OpenCell(5,5);disconnected.BuildDistances(5,5);
   Check(!disconnected.TryWaypoint(disconnected,2,2,5,5,Clearance,out wx1,out wz1),"Unreachable destination must not invent a path");
   return "PASS: "+checks+" assertions; "+routes+" routes; reported Cloister coordinates, obstacle corners, live blockers, narrow clearance and all 19 maps at 60/10/3 FPS.";
  }
#if UNITY_EDITOR
  public static void Run()
  {
   try
   {
    string result=RunCore();RuntimeChecks();result+=" Runtime RouteWaypoint/Move collision, corner chase and full-inventory pacing loot selection passed.";
    Directory.CreateDirectory("Logs");File.WriteAllText("Logs/navigation-result.txt",result);Debug.Log(result);EditorApplication.Exit(0);
   }
   catch(Exception error){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/navigation-result.txt","FAIL: "+error);Debug.LogException(error);EditorApplication.Exit(1);}
  }
  static object LootEntry(KryptaGame game,string listName,string typeName,Vector3 position,int kind=-1)
  {
   const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var list=(System.Collections.IList)typeof(KryptaGame).GetField(listName,flags).GetValue(game);
   var item=Activator.CreateInstance(typeof(KryptaGame).GetNestedType(typeName,BindingFlags.NonPublic),true);
   item.GetType().GetField(listName=="pickups" ? "origin" : "position").SetValue(item,position);
   if(kind>=0)item.GetType().GetField("kind").SetValue(item,kind);list.Add(item);return item;
  }
  static bool BotLoot(NocturnisPacingBot bot,out Vector3 selected)
  {
   object[] args={Vector3.zero};bool found=(bool)typeof(NocturnisPacingBot).GetMethod("NearbyLoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(bot,args);
   selected=(Vector3)args[0];return found;
  }
  static void PacingLootChecks(KryptaGame game,GameObject owner)
  {
   const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var grid=Empty();Room(grid,1,1,12,12);typeof(KryptaGame).GetField("grid",flags).SetValue(game,grid);
   var prefab=new GameObject("Pacing loot fixture model");prefab.transform.SetParent(owner.transform);
   var visual=new ActorVisual(prefab,null,new Vector3(4*Tile,0,4*Tile),1.8f,"Pacing loot fixture actor");visual.Root.transform.SetParent(owner.transform);
   typeof(KryptaGame).GetField("player",flags).SetValue(game,visual);
   var bot=owner.AddComponent<NocturnisPacingBot>();bot.enabled=false;typeof(NocturnisPacingBot).GetField("game",flags).SetValue(bot,game);
   for(int i=0;i<KryptaGame.InventoryCapacity;i++)game.Inventory.Add(new KryptaGame.Relic{slot=0,rank=1});
   Vector3 origin=game.HeroPosition,selected;LootEntry(game,"relics","RelicDrop",origin+Vector3.right*2);
   Check(!BotLoot(bot,out selected),"Full backpack skips ground relic instead of waiting on it");
   var container=LootEntry(game,"containerLoot","ContainerLoot",origin+Vector3.right,3);
   Check(!BotLoot(bot,out selected),"Full backpack also skips uncollectable container relic");
   LootEntry(game,"pickups","Pickup",origin+Vector3.right*5);
   Check(BotLoot(bot,out selected) && Vector3.Distance(selected,origin+Vector3.right*5)<.001f,"Full backpack still selects collectible currency beyond nearer relics");
   container.GetType().GetField("kind").SetValue(container,1);
   Check(BotLoot(bot,out selected) && Vector3.Distance(selected,origin+Vector3.right)<.001f,"Crafting materials remain collectible with a full backpack");
   container.GetType().GetField("kind").SetValue(container,3);game.Inventory.RemoveAt(0);
   Check(BotLoot(bot,out selected) && Vector3.Distance(selected,origin+Vector3.right)<.001f,"Freeing one slot makes the nearest container relic eligible again");
   var containers=(System.Collections.IList)typeof(KryptaGame).GetField("containerLoot",flags).GetValue(game);containers.Clear();
   Check(BotLoot(bot,out selected) && Vector3.Distance(selected,origin+Vector3.right*2)<.001f,"Free slot also admits normal ground relics");
   Check(((System.Collections.IList)typeof(KryptaGame).GetField("relics",flags).GetValue(game)).Count==1 && game.Inventory.Count==KryptaGame.InventoryCapacity-1,"Bot selection does not delete drops or modify inventory");
  }
  static void RuntimeChecks()
  {
   var root=new GameObject("Navigation regression fixture");var actor=new GameObject("Collision probe");
   try
   {
    var game=root.AddComponent<KryptaGame>();game.enabled=false;
    const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    var field=typeof(KryptaGame).GetField("grid",flags);var waypoint=typeof(KryptaGame).GetMethod("RouteWaypoint",flags);var move=typeof(KryptaGame).GetMethod("Move",flags);
    var grid=Empty();Room(grid,2,2,9,4);Room(grid,7,3,9,12);Room(grid,3,10,8,12);grid.CloseCell(8,6);grid.BuildDistances(3,11);field.SetValue(game,grid);
    Vector3 goal=new Vector3(3*Tile,0,11*Tile);actor.transform.position=new Vector3(2.3f*Tile,0,2.2f*Tile);
    int frames=0;while(Vector3.Distance(actor.transform.position,goal)>.12f && frames++<2000)
    {
     Vector3 before=actor.transform.position,target=(Vector3)waypoint.Invoke(game,new object[]{grid,before,goal});
     Vector3 delta=Vector3.ClampMagnitude(target-before,4.8f/3);move.Invoke(game,new object[]{actor.transform,delta,.28f});
     Check(grid.CanStand(actor.transform.position.x/Tile,actor.transform.position.z/Tile,Body),"Runtime Move crossed wall");
     Check(Vector3.Distance(before,actor.transform.position)<=delta.magnitude+.001f,"Runtime Move exceeded budget");
    }
    Check(frames<2000,"Runtime corner chase stalled");
    PacingLootChecks(game,root);
   }
   finally{UnityEngine.Object.DestroyImmediate(actor);UnityEngine.Object.DestroyImmediate(root);}
  }
#endif
 }
}
