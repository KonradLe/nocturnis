using System;
using System.Collections.Generic;
using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaGame
 {
  // Gem grades and item enchants are kept with the save, not in global UI state.
  public readonly List<int> SocketedGems=new List<int>();
  public int[] GemBag {get;private set;}=new int[3];
  int[] gearEnchants=new int[3];
  public int Garb {get;private set;}
  public static readonly string[] GemNames={"Odłamek","Szlifowany","Doskonały"};
  public static readonly string[] EnchantNames={"Bez zaklęcia","Siła","Wytrzymałość","Odnowa"};
  public static readonly string[] GarbNames={"Strój pielgrzyma","Szata popiołów","Blask Ichoritu"};
  public int GearEnchant(int slot){return slot>=0 && slot<3 ? gearEnchants[slot] : 0;}
  public float EnchantBonus(int kind)
  {float n=0;for(int i=0;i<3;i++)if(gearEnchants[i]==kind)n+=GearRank(i)*(kind==2 ? 5 : kind==3 ? .15f : 2);return n;}
  public string EnchantLabel(int kind,int rank)
  {return kind==1 ? "+"+(rank*2)+" ataku" : kind==2 ? "+"+(rank*5)+" zdrowia" : kind==3 ? "+"+(rank*.15f).ToString("0.00")+" regeneracji/s" : "Bez zaklęcia";}
  public bool BuyGem()
  {if(Service!="alchemy" || Coins<30 || GemBag[0]>=99)return false;Coins-=30;GemBag[0]++;SaveCheckpoint();Sound("coin");return true;}
  public bool CombineGem(int grade)
  {
   if(Service!="alchemy" || grade<0 || grade>1 || GemBag[grade]<3 || GemBag[grade+1]>=99 || Coins<20)return false;
   GemBag[grade]-=3;GemBag[grade+1]++;Coins-=20;SaveCheckpoint();Sound("shrine");return true;
  }
  public bool SocketGem(int grade)
  {
   if(Service!="alchemy" || grade<0 || grade>2 || GemBag[grade]<=0 || SocketedGems.Count>=5)return false;
   GemBag[grade]--;SocketedGems.Add(grade+1);RecountGems();SaveCheckpoint();Sound("shrine");return true;
  }
  public bool ExtractGem()
  {
   if(Service!="alchemy" || SocketedGems.Count==0 || Coins<10)return false;
   int last=SocketedGems.Count-1,grade=SocketedGems[last]-1;if(GemBag[grade]>=99)return false;
   SocketedGems.RemoveAt(last);GemBag[grade]++;Coins-=10;RecountGems();SaveCheckpoint();Sound("chain");return true;
  }
  void RecountGems(){Gems=0;foreach(int rank in SocketedGems)Gems+=rank;}
  public bool EnchantGear(int slot,int kind)
  {
   if(Service!="alchemy" || slot<0 || slot>2 || kind<1 || kind>3 || GearRank(slot)==0 || gearEnchants[slot]==kind)return false;
   int cost=30+GearRank(slot)*10;if(Coins<cost)return false;
   Coins-=cost;gearEnchants[slot]=kind;Health=Mathf.Min(Health,MaxHealth);SaveCheckpoint();Sound("shrine");return true;
  }
  public bool ChangeGarb(int choice)
  {
   if(Service!="wardrobe" || choice<0 || choice>2)return false;
   Garb=choice;if(player!=null)ApplyGarb(player.Root,Garb);SaveCheckpoint();Sound("chain");return true;
  }
  public static void ApplyGarb(GameObject root,int choice)
  {
   Color color=choice==1 ? new Color(.55f,.62f,.7f) : choice==2 ? new Color(.8f,.48f,1) : Color.white;
   var profile=root.GetComponentInChildren<ActorModelProfile>();
   foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
   {
    var materials=renderer.sharedMaterials;
    for(int i=0;i<materials.Length;i++)
    {
     var material=materials[i];if(!material)continue;
     bool tint=profile==null || string.IsNullOrEmpty(profile.garbMaterialPrefix) || material.name.StartsWith(profile.garbMaterialPrefix,StringComparison.Ordinal);
     var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,i);
     if(material.HasProperty("_ClothGarb"))
     {block.SetColor("_ClothGarb",color);renderer.SetPropertyBlock(block,i);continue;}
     foreach(string property in new[]{"baseColorFactor","_BaseColor","_Color"})
      if(material.HasProperty(property))block.SetColor(property,material.GetColor(property)*(tint ? color : Color.white));
     renderer.SetPropertyBlock(block,i);
    }
   }
  }
  void ResetArtisans(){SocketedGems.Clear();GemBag=new int[3];gearEnchants=new int[3];Garb=0;}
  void RestoreArtisans(int[] bag,List<int> sockets,int[] enchants,int garb,int legacyGems)
  {
   ResetArtisans();Garb=Mathf.Clamp(garb,0,2);
   for(int i=0;i<3;i++){GemBag[i]=bag!=null && i<bag.Length ? Mathf.Clamp(bag[i],0,99) : 0;gearEnchants[i]=enchants!=null && i<enchants.Length ? Mathf.Clamp(enchants[i],0,3) : 0;}
   if(sockets!=null){foreach(int r in sockets)if(r>=1 && r<=3 && SocketedGems.Count<5)SocketedGems.Add(r);}
   else for(int i=0;i<Mathf.Clamp(legacyGems,0,5);i++)SocketedGems.Add(1);
   RecountGems();
  }
  public string SavedChronicle
  {get{var s=ReadCheckpoint();return s==null ? "Kronika czeka na pierwszą wyprawę." : NocturnisData.Heroes[s.hero].person+" • poziom "+s.level+"\n"+Campaign.Names[s.zone]+"\nPokonani: "+s.defeated+" • złoto: "+s.coins;}}
  public int SavedHeroLevel(int hero){var s=ReadCheckpoint();return s!=null && s.hero==hero ? s.level : 0;}
 }
}
