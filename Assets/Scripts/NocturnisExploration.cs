using System;
using UnityEngine;
namespace Krypta {
 public sealed partial class KryptaGame {
  bool[][] explored=new bool[Campaign.Count][];int explorationProfile=-1,lastMapX=-999,lastMapZ=-999,lastMapZone=-1;float nextMapStore;
  string ExplorePrefix {get{return (Testing ? "Nocturnis.QA.Explored." : "Nocturnis.Explored.")+ActiveProfile+".";}}
  public int TrackedQuest {get{return PrimaryTrackedQuest;}}
  public void TrackQuest(int quest){ToggleQuestTracking(quest);}
  public void RefreshTutorialProfile(){tutorialLoaded=false;explorationProfile=-1;}
  public void ResetExploration(){
   // A new expedition replaces discoveries only in its selected save slot.
   // Continue and ordinary map transitions deliberately never call this method.
   for(int zone=0;zone<Campaign.Count;zone++)PlayerPrefs.DeleteKey(ExplorePrefix+zone);
   PlayerPrefs.DeleteKey(ExplorePrefix+"quest");explored=new bool[Campaign.Count][];
   explorationProfile=ActiveProfile;lastMapX=lastMapZ=-999;lastMapZone=-1;nextMapStore=0;PlayerPrefs.Save();
  }
  public bool MapDiscovered(int x,int z){if(x<0 || z<0 || x>=DungeonGrid.Width || z>=DungeonGrid.Height)return false;return PlayerPrefs.GetInt("Nocturnis.MapExploration",1)==0 || Zone==Campaign.Town || explored[Zone]!=null && explored[Zone][z*DungeonGrid.Width+x];}
  void RevealMap(){
   if(explorationProfile!=ActiveProfile){explored=new bool[Campaign.Count][];explorationProfile=ActiveProfile;lastMapZone=-1;}
   if(explored[Zone]==null){int size=DungeonGrid.Width*DungeonGrid.Height;explored[Zone]=new bool[size];try{byte[] bytes=Convert.FromBase64String(PlayerPrefs.GetString(ExplorePrefix+Zone,""));for(int i=0;i<size && i/8<bytes.Length;i++)explored[Zone][i]=(bytes[i/8]&(1<<(i%8)))!=0;}catch(FormatException){}}
   int cx=Cell(HeroPosition.x),cz=Cell(HeroPosition.z);if(cx==lastMapX && cz==lastMapZ && Zone==lastMapZone)return;lastMapX=cx;lastMapZ=cz;lastMapZone=Zone;
   for(int x=Mathf.Max(0,cx-6);x<=Mathf.Min(DungeonGrid.Width-1,cx+6);x++)for(int z=Mathf.Max(0,cz-6);z<=Mathf.Min(DungeonGrid.Height-1,cz+6);z++)if((x-cx)*(x-cx)+(z-cz)*(z-cz)<=36)explored[Zone][z*DungeonGrid.Width+x]=true;
   var data=new byte[(explored[Zone].Length+7)/8];for(int i=0;i<explored[Zone].Length;i++)if(explored[Zone][i])data[i/8]|=(byte)(1<<(i%8));PlayerPrefs.SetString(ExplorePrefix+Zone,Convert.ToBase64String(data));
  }
  public int MapCitizenCount {get{return citizens.Count;}}
  public Vector3 MapCitizenPosition(int i){return citizens[i].p;}
  public bool TrackedQuestPosition(out Vector3 position){
   position=ObjectivePosition;int q=TrackedQuest;if(q<0 || SideQuestState(q)==3)return false;
   if(SideQuestState(q)==0 || SideQuestState(q)==2){string service=SideQuestData.Services[q];foreach(var c in citizens)if(c.service==service){position=c.p;return true;}return false;}
   if(SideQuestData.Zones[q]==Zone){
    if(q==1 && SideQuestObjectCount(q)>=3 && (sideObjects[q]&256)==0){position=EmberBasin;return true;}
    foreach(var marker in questMarkers)if(marker.quest==q && marker.root && marker.root.activeSelf && (q!=2 || marker.index==NextValveIndex())){position=marker.p;return true;}
   }
   if(Zone==Campaign.Fields && (q==1 || q==4)){position=World(q==4 ? 3 : 20,q==4 ? 20 : 15);return true;}
   if(q==0 && Zone==Campaign.Cemetery){position=World(19,16);return true;}
   if(q==2 && Zone==Campaign.Crypt2){position=World(3,22);return true;}
   if(q==3 && Zone==Campaign.Kings){position=World(20,16);return true;}return false;
  }
 }
}
