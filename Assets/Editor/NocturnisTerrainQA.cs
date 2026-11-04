using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
namespace Krypta.Editor
{
 public static class NocturnisTerrainQA
 {
  [Serializable] public sealed class ZoneReport {public int zone,cells,distanceToExit,eventRouteDistance,detourCells;public float fillRatio,gridMetres,eventGridMetres;public string name;}
  [Serializable] public sealed class Report {public bool passed;public int assertions;public float extendedMapsGridMetres,extendedEventRouteGridMetres,extendedEventWalkingSecondsAt4_8;public string failure;public List<ZoneReport> zones=new List<ZoneReport>();}
  static void Check(Report report,bool okay,string message){report.assertions++;if(!okay)throw new Exception(message);}
  public static Report Validate()
  {
   var report=new Report();var silhouettes=new HashSet<string>();
   for(int zone=0;zone<Campaign.Count;zone++)
   {
    var grid=new DungeonGrid(zone);var replica=new DungeonGrid(zone);
    int sx=zone==Campaign.Town?11:zone==Campaign.Road||zone==Campaign.Heart||zone==Campaign.Cemetery?12:5;
    int sz=zone==Campaign.Town?5:3;
    int ex=zone==Campaign.Town?20:zone==Campaign.Road||zone==Campaign.Heart?12:17;
    int ez=zone==Campaign.Town?10:zone==Campaign.Road||zone==Campaign.Heart?37:38;
    grid.BuildDistances(sx,sz);Check(report,grid.CanStand(sx,sz,.3f),"Spawn footprint zone "+zone);
    Check(report,grid.Distance(ex,ez)>0,"Reachable exit zone "+zone);
    int count=0,minX=25,maxX=0,minZ=41,maxZ=0;var signature=new System.Text.StringBuilder();
    for(int x=0;x<DungeonGrid.Width;x++)for(int z=0;z<DungeonGrid.Height;z++)
    {
     bool walk=grid.Walkable(x,z);signature.Append(walk?'1':'0');Check(report,walk==replica.Walkable(x,z),"Stable layout "+zone);
     if(!walk)continue;count++;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minZ=Math.Min(minZ,z);maxZ=Math.Max(maxZ,z);
     Check(report,grid.Distance(x,z)>=0,"Disconnected cell "+zone+" "+x+","+z);
     Check(report,grid.CanStand(x,z,.3f),"Actor footprint "+zone+" "+x+","+z);
     if(x+1<DungeonGrid.Width && grid.Walkable(x+1,z))Check(report,grid.CanTravel(x,z,x+1,z,.3f),"Horizontal route "+zone);
     if(z+1<DungeonGrid.Height && grid.Walkable(x,z+1))Check(report,grid.CanTravel(x,z,x,z+1,.3f),"Vertical route "+zone);
    }
    Check(report,silhouettes.Add(signature.ToString()),"Distinct map silhouette "+zone);
    float ratio=count/(float)((maxX-minX+1)*(maxZ-minZ+1));
    if(Campaign.Outdoors(zone))Check(report,ratio<.78f,"Outdoor outline must not become rectangular "+zone);
    foreach(var pin in ContractPins(zone))Check(report,grid.Distance(pin.X,pin.Z)>=0,"Reachable actor/quest anchor "+zone+" "+pin.X+","+pin.Z);
    int exitDistance=grid.Distance(ex,ez),eventDistance=exitDistance;
    if(zone>=Campaign.Ravine)
    {
     Check(report,exitDistance>=70,"Authored route must include a genuine detour "+zone);
     eventDistance=0;int px=sx,pz=sz;int[,] stops={{7,9},{17,23},{10,34},{17,38}};
     for(int i=0;i<stops.GetLength(0);i++){grid.BuildDistances(px,pz);int d=grid.Distance(stops[i,0],stops[i,1]);Check(report,d>=0,"Connected ordered event route "+zone);eventDistance+=d;px=stops[i,0];pz=stops[i,1];}
     report.extendedMapsGridMetres+=exitDistance*1.8f;report.extendedEventRouteGridMetres+=eventDistance*1.8f;
    }
    report.zones.Add(new ZoneReport{zone=zone,name=Campaign.Names[zone],cells=count,distanceToExit=exitDistance,eventRouteDistance=eventDistance,detourCells=exitDistance-Math.Abs(ex-sx)-Math.Abs(ez-sz),fillRatio=ratio,gridMetres=exitDistance*1.8f,eventGridMetres=eventDistance*1.8f});
   }
   // Dynamic breakable blockers are reversible; separate grids retain independent state.
   var first=new DungeonGrid(Campaign.Fields);var second=new DungeonGrid(Campaign.Fields);var c=first.NearestWalkable(0,0);
   first.CloseCell(c.X,c.Z);Check(report,!first.Walkable(c.X,c.Z)&&second.Walkable(c.X,c.Z),"Blockers affect only their own grid");
   first.OpenCell(c.X,c.Z);Check(report,first.CanStand(c.X,c.Z,.3f),"Broken blocker restores actor footprint");
   Check(report,!new DungeonGrid(Campaign.Cistern).Walkable(8,19),"Cistern pool one stays blocked");
   Check(report,!new DungeonGrid(Campaign.Cistern).Walkable(15,33),"Cistern pool two stays blocked");
   Check(report,!new DungeonGrid(Campaign.Cistern).Walkable(21,36),"Cistern pool three stays blocked");
   report.extendedEventWalkingSecondsAt4_8=report.extendedEventRouteGridMetres/4.8f;report.passed=true;return report;
  }
  static IEnumerable<DungeonGrid.Cell> ContractPins(int zone)
  {
   int[,] pins;
   if(zone>=Campaign.Ravine)pins=new int[,]{{5,3},{7,9},{17,23},{10,34},{17,38}};
   else if(zone==Campaign.Town)pins=new int[,]{{11,8},{8,6},{15,8},{8,13},{15,13},{7,10},{7,13}};
   else if(zone==Campaign.Road)pins=new int[,]{{13,8},{12,16},{10,25},{15,27},{12,30}};
   else if(zone==Campaign.Heart)pins=new int[,]{{12,27},{12,29},{5,24},{19,24}};
   else if(zone==Campaign.Cemetery)pins=new int[,]{{4,5},{20,5},{4,33},{20,34},{12,20},{11,15},{11,25},{12,25},{13,25},{19,16}};
   else pins=new int[,]{{3,7},{7,8},{5,9},{3,16},{10,16},{6,21},{13,23},{12,30},{18,31},{21,34},{15,35},{17,36},{7,6},{3,22},{21,30}};
   for(int i=0;i<pins.GetLength(0);i++)yield return new DungeonGrid.Cell(pins[i,0],pins[i,1]);
   if(zone==Campaign.Fields){yield return new DungeonGrid.Cell(3,20);yield return new DungeonGrid.Cell(20,15);}
   if(zone==Campaign.Forest)yield return new DungeonGrid.Cell(20,35);
   if(zone==Campaign.Kings)yield return new DungeonGrid.Cell(20,16);
  }
  public static void Run()
  {
   Report report;try{report=Validate();}catch(Exception error){report=new Report{passed=false,failure=error.ToString()};Debug.LogException(error);}
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/terrain-qa.json",JsonUtility.ToJson(report,true));
   Debug.Log("TERRAIN_QA "+(report.passed?"PASS":"FAIL")+" "+report.assertions+" assertions");EditorApplication.Exit(report.passed?0:1);
  }
 }
}
