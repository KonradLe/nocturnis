using System.Collections.Generic;
using UnityEngine;

namespace Krypta
{
 public sealed partial class KryptaGame
 {
  Relic[] equippedRelics=new Relic[3];
  float relicResourceAt=-1;
  static readonly string[] RelicTitles={"Przysięga Popiołów","Serce Transfuzji","Oko Dalekiego Zmierzchu","Węzeł Entropii","Pamięć Starego Dębu"};
  public static Relic CopyRelic(Relic item)
  {return item==null ? null : new Relic{slot=item.slot,rank=item.rank,enchant=item.enchant,identified=item.identified,protectedItem=item.protectedItem,affix=item.affix,affixValue=item.affixValue,legend=item.legend,legendHero=item.legendHero};}
  static void NormalizeRelic(Relic item)
  {
   if(item==null)return;item.enchant=Mathf.Clamp(item.enchant,0,3);
   if(item.affix<1 || item.affix>3 || item.affixValue<=0){item.affix=0;item.affixValue=0;}
   else item.affixValue=Mathf.Clamp(item.affixValue,1,10);
   if(item.rank<4 || item.legend<1 || item.legend>3 || item.legendHero<0 || item.legendHero>4){item.legend=0;item.legendHero=-1;}
  }
  public Relic EquippedRelic(int slot)
  {
   if(slot<0 || slot>2)return null;
   var item=CopyRelic(equippedRelics[slot]) ?? new Relic();
   item.slot=slot;item.rank=GearRank(slot);item.enchant=GearEnchant(slot);item.identified=true;return item;
  }
  void EquipRelicData(Relic item)
  {
   var copy=CopyRelic(item);NormalizeRelic(copy);SetGear(copy.slot,copy.rank);gearEnchants[copy.slot]=copy.enchant;equippedRelics[copy.slot]=copy;
  }
  public void ResetRelicGear(){equippedRelics=new Relic[3];ResetRelicCombat();}
  void RestoreRelicGear(Relic[] saved)
  {
   equippedRelics=new Relic[3];if(saved==null)return;
   for(int i=0;i<3 && i<saved.Length;i++)if(saved[i]!=null){var item=CopyRelic(saved[i]);item.slot=i;item.rank=GearRank(i);item.enchant=GearEnchant(i);NormalizeRelic(item);equippedRelics[i]=item;}
  }
  Relic[] CaptureRelicGear(){return new[]{EquippedRelic(0),EquippedRelic(1),EquippedRelic(2)};}
  Relic RollRelic(int slot,int rank,int salt)
  {
   uint seed=PropHash(Zone,slot+salt,rank+salt*7);
   var item=new Relic{slot=slot,rank=Mathf.Clamp(rank,1,5),identified=rank<4};
   if(rank>=2){item.affix=1+(int)(seed%3);item.affixValue=Mathf.Clamp(rank+(int)((seed>>4)%3),1,10);}
   if(rank>=4 && (seed&1)==0)
   {item.legendHero=SelectedHero;item.legend=SelectedHero==2 ? 2 : SelectedHero==0 || SelectedHero==4 ? 1 : 3;}
   return item;
  }
  float RelicAffixTotal(int kind)
  {float total=0;for(int slot=0;slot<3;slot++)if(GearRank(slot)>0 && equippedRelics[slot]!=null && equippedRelics[slot].affix==kind)total+=equippedRelics[slot].affixValue;return total;}
  bool HasRelicLegend(int kind)
  {for(int slot=0;slot<3;slot++)if(GearRank(slot)>0 && equippedRelics[slot]!=null && equippedRelics[slot].legend==kind && equippedRelics[slot].legendHero==SelectedHero)return true;return false;}
  public float RelicDamageMultiplier(Enemy enemy)
  {
   float bonus=Mathf.Min(30,RelicAffixTotal(1))*.01f;
   if(enemy!=null && enemy.maxHp>0 && enemy.hp<=enemy.maxHp*.35f)bonus+=Mathf.Min(30,RelicAffixTotal(3))*.01f;
   if(enemy!=null && HasRelicLegend(2) && Vector3.Distance(HeroPosition,enemy.Position)>=5)bonus+=.15f;
   return 1+bonus;
  }
  public void OnRelicEnemyKilled(Enemy enemy)
  {
   if(State!=Phase.Playing || Health<=0)return;
   if(HasRelicLegend(1))Status.Apply("relic_ward","Relikt • osłona jednego ciosu",Time.time,8);
   if(HasRelicLegend(3) && Time.time>=relicResourceAt)
   {relicResourceAt=Time.time+2;GainResource(10);Floating(HeroPosition,"RELIKT +10 "+Hero.resource.ToUpper(),Hero.color);}
  }
  public float ApplyRelicProtection(float amount)
  {
   amount*=1-Mathf.Min(12,RelicAffixTotal(2))*.01f;
   if(amount>0 && HasRelicLegend(1) && Status.Count("relic_ward",Time.time)>0)
   {amount*=.75f;Status.Remove("relic_ward");Floating(HeroPosition,"OSŁONA RELIKTU",Hero.color);}
   return amount;
  }
  public void ResetRelicCombat(){relicResourceAt=-1;Status.Remove("relic_ward");}
  public string RelicAffixLabel(Relic item)
  {
   if(item==null || !item.identified)return "Właściwości ujawni Malakor.";
   if(item.affix==1)return "Żar: +"+item.affixValue+"% obrażeń";
   if(item.affix==2)return "Hart: -"+item.affixValue+"% otrzymywanych obrażeń (łącznie do 12%)";
   if(item.affix==3)return "Wyrok: +"+item.affixValue+"% obrażeń przeciw wrogom poniżej 35% zdrowia";
   return "Bez dodatkowego affiksu";
  }
  public string RelicLegendLabel(Relic item)
  {
   if(item==null || !item.identified || item.legend==0)return "";
   string effect=item.legend==1 ? "Zabójstwo daje osłonę: następny cios zadaje o 25% mniej obrażeń. Osłona trwa do 8 s; nie kumuluje się." : item.legend==2 ? "+15% obrażeń przy dystansie co najmniej 5 m. Ten efekt nie kumuluje się." : "Zabójstwo przywraca 10 zasobu, najwyżej raz na 2 s. Ten efekt nie kumuluje się.";
   return NocturnisData.Heroes[item.legendHero].name+": "+effect+(item.legendHero!=SelectedHero ? "\nNieaktywne dla obecnej klasy." : "");
  }
  public string RelicDescription(Relic item)
  {
   if(item==null)return "Brak przedmiotu.";
   if(!item.identified)return "Nieznany relikt • identyfikacja u Malakora w Oakhaven.";
   string core=item.slot==0 ? "+"+(item.rank*5)+" ataku" : item.slot==1 ? "-"+(item.rank*6)+"% obrażeń od pancerza" : "+"+(item.rank*.12f).ToString("0.00")+" zdrowia/s";
   return core+"\n"+EnchantLabel(item.enchant,item.rank)+"\n"+RelicAffixLabel(item)+(item.legend>0 ? "\n"+RelicLegendLabel(item) : "");
  }
  public string RelicComparison(Relic item)
  {
   if(item==null || !item.identified)return "Zidentyfikuj przedmiot, aby porównać właściwości.";
   int difference=item.rank-GearRank(item.slot);
   string sign=difference>0 ? "+" : "";
   string core=item.slot==0 ? sign+(difference*5)+" ataku" : item.slot==1 ? sign+(difference*6)+" pkt proc. redukcji pancerza" : sign+(difference*.12f).ToString("0.00")+" zdrowia/s";
   return "Zmiana podstawy: "+core+".\nZaklęcie: "+EnchantNames[GearEnchant(item.slot)]+" → "+EnchantNames[item.enchant]+".\nAffiks obecnego: "+RelicAffixLabel(EquippedRelic(item.slot))+".\nWybrany: "+RelicAffixLabel(item)+".";
  }
  List<RelicDrop> NearbyRelicDrops()
  {return relics.FindAll(drop=>drop.root && Vector3.Distance(drop.position,HeroPosition)<=4 && Visible(drop.position,HeroPosition));}
  public List<Relic> NearbyRelics()
  {var result=new List<Relic>();foreach(var drop in NearbyRelicDrops())result.Add(CopyRelic(drop.item));return result;}
  public bool SwapGroundRelic(int groundIndex,int inventoryIndex)
  {
   if(!EquipmentOpen || State!=Phase.Paused || inventoryIndex<0 || inventoryIndex>=Inventory.Count)return false;
   var nearby=NearbyRelicDrops();if(groundIndex<0 || groundIndex>=nearby.Count)return false;
   var drop=nearby[groundIndex];var outgoing=CopyRelic(Inventory[inventoryIndex]);Inventory[inventoryIndex]=CopyRelic(drop.item);
   Vector3 position=drop.position;Destroy(drop.root);relics.Remove(drop);CreateRelicDropData(position,outgoing);
   SaveCheckpoint();ShowNotice("Wymieniono relikty • poprzedni przedmiot czeka na ziemi");Sound("shrine");return true;
  }
 }
}
