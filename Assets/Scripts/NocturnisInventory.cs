using System;
using System.Collections.Generic;
using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaGame
 {
  [Serializable]public sealed class Relic {public int slot,rank,enchant,affix,affixValue,legend;public int legendHero=-1;public bool identified=true;public bool protectedItem;}
  [Serializable]sealed class StashData {public List<Relic> items=new List<Relic>();}
  public const int InventoryCapacity=24;
  public readonly List<Relic> Inventory=new List<Relic>();
  public List<Relic> Stash {get{return ReadStash().items;}}
  static string StashKey {get{return Testing || Application.isBatchMode ? "Nocturnis.QA.Stash" : "Nocturnis.SharedStash.v1";}}
  StashData ReadStash()
  {
   try{var data=JsonUtility.FromJson<StashData>(PlayerPrefs.GetString(StashKey,"{}")) ?? new StashData();if(data.items==null)data.items=new List<Relic>();data.items.RemoveAll(r=>r==null || r.slot<0 || r.slot>2 || r.rank<1 || r.rank>5);foreach(var item in data.items)NormalizeRelic(item);if(data.items.Count>InventoryCapacity)data.items.RemoveRange(InventoryCapacity,data.items.Count-InventoryCapacity);return data;}catch(ArgumentException){return new StashData();}
  }
  public string RelicName(Relic r)
  {return !r.identified ? "Nieznany relikt +"+r.rank : (r.legend>0 ? RelicTitles[r.legendHero] : (r.rank>=4 ? "Epicki " : r.rank>=2 ? "Rzadki " : "")+new[]{"oręż","pancerz","talizman"}[r.slot])+" +"+r.rank+(r.enchant>0 ? " • "+EnchantNames[Mathf.Clamp(r.enchant,0,3)] : "");}
  public bool EquipItem(int index)
  {
   if(!EquipmentOpen || index<0 || index>=Inventory.Count || !Inventory[index].identified)return false;
   var r=Inventory[index];var old=EquippedRelic(r.slot);EquipRelicData(r);Health=Mathf.Min(Health,MaxHealth);Inventory.RemoveAt(index);if(old.rank>0)Inventory.Add(old);SaveCheckpoint();Sound("shrine");return true;
  }
  public bool Salvage(int index)
  {
   if(Service!="forge" || index<0 || index>=Inventory.Count || Inventory[index].protectedItem)return false;Materials+=Inventory[index].rank*3;Inventory.RemoveAt(index);SaveCheckpoint();Sound("hit");return true;
  }
  public bool Craft()
  {
   if(Service!="forge" || Materials<12 || Inventory.Count>=InventoryCapacity)return false;Materials-=12;Inventory.Add(RollRelic((Level+Defeated)%3,Mathf.Min(5,1+Level/3),Materials+Coins+Defeated*7));SaveCheckpoint();Sound("shrine");return true;
  }
  public bool Identify()
  {
   if(Service!="lore")return false;var r=Inventory.Find(i=>!i.identified);if(r==null || Coins<10)return false;Coins-=10;r.identified=true;SaveCheckpoint();Sound("shrine");return true;
  }
  public bool StoreItem(int index,bool withdraw)
  {
   if(Service!="stash")return false;var data=ReadStash();var source=withdraw ? data.items : Inventory;var target=withdraw ? Inventory : data.items;
   if(index<0 || index>=source.Count || target.Count>=InventoryCapacity)return false;target.Add(source[index]);source.RemoveAt(index);PlayerPrefs.SetString(StashKey,JsonUtility.ToJson(data));SaveCheckpoint();Sound("coin");return true;
  }
 }
}
