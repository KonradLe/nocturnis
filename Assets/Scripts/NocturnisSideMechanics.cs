using System.Collections.Generic;
using UnityEngine;
namespace Krypta {
 public sealed partial class KryptaGame {
  QuestMarker defendedLamp;float lampRemaining;int mechanicZone=-1;GameObject carriedEmber;
  readonly List<Enemy> lampGuards=new List<Enemy>();Vector3 emberBasin;
  static readonly int[] ValveOrder={0,2,1};
  int NextValveIndex(){foreach(int index in ValveOrder)if((sideObjects[2]&(1<<index))==0)return index;return -1;}
  Vector3 EmberBasin {get{return emberBasin;}}
  public bool CarryingEmber {get{return Zone==Campaign.Cellar && SideQuestObjectCount(1)>0 && (sideObjects[1]&256)==0;}}
  public string SideMechanicHint {get{
   if(defendedLamp!=null)return "Broń lampy • "+Mathf.CeilToInt(lampRemaining)+" s • pozostań w kręgu";
   if(CarryingEmber)return "Żar spowalnia ruch. Zanieś wszystkie trzy naczynia do misy przy wejściu.";
   return "";
  }}
  void CompleteQuestMarker(QuestMarker marker){sideObjects[marker.quest]|=1<<marker.index;marker.root.SetActive(false);Burst(marker.p,1,art.goldMaterial);Sound("shrine");SaveCheckpoint();ShowNotice(SideQuestData.Titles[marker.quest]+" • "+SideQuestProgress(marker.quest));}
  bool HandleSideMechanic(QuestMarker m){
   if(m.quest==0){
    if(defendedLamp!=null){ShowNotice("Dokończ obronę zapalonej lampy.");return true;}
    defendedLamp=m;lampRemaining=12;Ring(m.root.transform,7,.04f,art.tealMaterial).transform.localPosition=Vector3.up*.08f;lampGuards.Clear();
    for(int i=0;i<2;i++){var c=grid.NearestWalkable(Cell(m.p.x)+(i==0 ? -3 : 3),Cell(m.p.z)+2);var enemy=SpawnEncounterAt(c.X,c.Z,4,false,"Cień trzynastej lampy");enemy.spawnId=30+i;enemy.alerted=true;lampGuards.Add(enemy);}
    ShowNotice("Zimny ogień wzywa cienie. Broń lampy przez 12 sekund i pokonaj przywołanych.");Sound("chain");return true;
   }
   if(m.quest==2){int next=NextValveIndex();if(next>=0 && m.index!=next){ShowNotice("Ciśnienie jest zbyt wysokie. Kolejność odcięcia: I → III → II.");Sound("gate");return true;}}
   return false;
  }
  bool DeliverEmbers(){
   if(Zone!=Campaign.Cellar || !CarryingEmber || Vector3.Distance(HeroPosition,EmberBasin)>2.8f)return false;
   if(SideQuestObjectCount(1)<3){ShowNotice("W misie muszą znaleźć się wszystkie trzy naczynia żaru.");return true;}
   if(enemies.Exists(e=>e.hp>0 && Vector3.Distance(e.Position,HeroPosition)<7)){ShowNotice("Najpierw zabezpiecz misę przed wrogami.");return true;}
   sideObjects[1]|=256;if(carriedEmber)Destroy(carriedEmber);SaveCheckpoint();Sound("shrine");Burst(EmberBasin,2,art.goldMaterial);ShowNotice("Żar zabezpieczony. Wróć do Hargryma w Oakhaven.");return true;
  }
  void UpdateSideMechanics(float dt){
   if(mechanicZone!=Zone){defendedLamp=null;lampRemaining=0;lampGuards.Clear();mechanicZone=Zone;if(carriedEmber)Destroy(carriedEmber);}
   if(defendedLamp!=null && defendedLamp.root){
    if(Vector3.Distance(HeroPosition,defendedLamp.p)<=7)lampRemaining=Mathf.Max(0,lampRemaining-dt);
    if(lampRemaining<=0 && !lampGuards.Exists(enemy=>enemy.hp>0) && !enemies.Exists(e=>e.hp>0 && Vector3.Distance(e.Position,defendedLamp.p)<9)){var done=defendedLamp;defendedLamp=null;CompleteQuestMarker(done);}
   }
   if(CarryingEmber && player!=null){
    if(!carriedEmber){carriedEmber=Ring(player.Root.transform,.7f,.06f,art.goldMaterial);carriedEmber.name="Ocalony żar";carriedEmber.transform.localPosition=Vector3.up*.8f;}
    carriedEmber.transform.localRotation=Quaternion.Euler(70,Time.time*50,0);
   }else if(carriedEmber)Destroy(carriedEmber);
  }
  void BuildSideMechanicProps(){
   defendedLamp=null;lampRemaining=0;lampGuards.Clear();mechanicZone=Zone;carriedEmber=null;emberBasin=Vector3.zero;
   if(Zone==Campaign.Cellar){var cell=grid.NearestWalkable(5,5);emberBasin=World(cell.X,cell.Z);var bowl=Place(art.altar,EmberBasin,.8f,false,world);Ring(world,.85f,.08f,art.goldMaterial).transform.position=EmberBasin+Vector3.up*.1f;}
  }
  void BuildQuestConsequences(){
   if(EdraChoice==1)AddCitizen(14,11,4,"Edra • ocalona wędrowczyni","edra-home","Pamiętam chłód korzeni. Dziś pomagam Vesperze. Ci, których imiona ocaliłeś, nie będą już liczbą w księdze.");
   if(EdraChoice==2){var tree=Place(art.trees[1],World(10,11),2.3f,false,world);Ring(world,1,.05f,art.greenMaterial).transform.position=World(10,11)+Vector3.up*.08f;AddCitizen(14,11,4,"Pamięć Edry","memory-tree","Bursztynowe drzewo zapuściło korzenie w Oakhaven. Pod korą słychać głos Edry: pamięć lasu przetrwała.");}
   if((sideClaimed&1)!=0)for(int i=0;i<3;i++)Torch(World(10+i,7));
   if((sideClaimed&2)!=0)Flame(World(7,7)+Vector3.up*1.4f,.35f);
   if((sideClaimed&4)!=0){var ring=Ring(world,1,.08f,art.tealMaterial);ring.transform.position=World(12,11)+Vector3.up*.12f;}
  }
  public string ChronicleEnding {get{return "Oakhaven pamięta: "+CompletedSideQuests+" / 5 spraw mieszkańców zakończonych.\n"+(EdraChoice==1 ? "Edra wróciła do miasta i pomaga ocalałym." : EdraChoice==2 ? "Pamięć Edry zakwitła w bursztynowym drzewie." : "Los Edry pozostał nierozstrzygnięty.");}}
 }
 public sealed partial class KryptaHUD {
  void QuestMechanicPanel(){if(game.State!=KryptaGame.Phase.Playing || string.IsNullOrEmpty(game.SideMechanicHint))return;Panel(new Rect(360,110,550,60));Label(376,119,518,45,game.SideMechanicHint,text,paper);}
 }

}
