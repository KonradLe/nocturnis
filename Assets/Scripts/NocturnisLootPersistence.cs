using System;
using System.Collections.Generic;
using UnityEngine;

namespace Krypta
{
 public sealed partial class KryptaGame
 {
  // Container rewards have stable IDs. These records also retain intact relics
  // transferred from full-bag containers and explicitly exchanged by the player.
  [Serializable]sealed class PendingWorldLoot
  {
   public int zone,kind,value,slot,rank;
   public Relic relic;
   public Vector3 position;
  }
  readonly List<PendingWorldLoot> pendingWorldLoot=new List<PendingWorldLoot>();
  bool worldLootSceneReady;
  void ResetWorldLoot(){pendingWorldLoot.Clear();worldLootSceneReady=false;}
  void StashWorldLoot()
  {
   if(!worldLootSceneReady || !world)return;
   pendingWorldLoot.RemoveAll(p=>p.zone==Zone);
   foreach(var p in pickups)if(p.obj)pendingWorldLoot.Add(new PendingWorldLoot{zone=Zone,kind=p.grace ? 1 : 0,value=p.value,position=p.origin});
   foreach(var r in relics)if(r.root)pendingWorldLoot.Add(new PendingWorldLoot{zone=Zone,kind=2,slot=r.slot,rank=r.rank,relic=CopyRelic(r.item),position=r.position});
  }
  List<PendingWorldLoot> CaptureWorldLoot()
  {StashWorldLoot();return new List<PendingWorldLoot>(pendingWorldLoot);}
  static bool ValidWorldLoot(PendingWorldLoot p)
  {
   if(p==null || p.zone<0 || p.zone>=Campaign.Count || p.kind<0 || p.kind>2)return false;
   Vector3 v=p.position;
   if(float.IsNaN(v.x) || float.IsInfinity(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.z))return false;
   if(v.x< -Tile || v.z< -Tile || v.x>DungeonGrid.Width*Tile || v.z>DungeonGrid.Height*Tile || Mathf.Abs(v.y)>10)return false;
   return p.kind==2 ? p.slot>=0 && p.slot<3 && p.rank>=1 && p.rank<=5 : p.value>0 && p.value<=10000;
  }
  void RestoreWorldLoot(List<PendingWorldLoot> saved)
  {
   ResetWorldLoot();if(saved==null)return;
   var counts=new int[Campaign.Count];
   foreach(var p in saved)if(ValidWorldLoot(p) && counts[p.zone]<128)
   {
    pendingWorldLoot.Add(new PendingWorldLoot{zone=p.zone,kind=p.kind,value=p.value,slot=p.slot,rank=p.rank,relic=CopyRelic(p.relic),position=p.position});counts[p.zone]++;
   }
  }
  void RestoreZoneWorldLoot()
  {
   foreach(var p in pendingWorldLoot)if(p.zone==Zone)
   {
    if(p.kind==2)
    {
     var item=CopyRelic(p.relic) ?? new Relic{slot=p.slot,rank=p.rank,identified=p.rank<4};
     item.slot=p.slot;item.rank=p.rank;NormalizeRelic(item);CreateRelicDropData(p.position,item);
    }
    else Drop(p.position,p.value,p.kind==1);
   }
   worldLootSceneReady=true;
  }
 }
}
