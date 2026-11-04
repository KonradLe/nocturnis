using System;
using System.Collections.Generic;
using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaGame
 {
  [Serializable] public sealed class MarketSave {public int generation=-1;public List<Relic> stock=new List<Relic>();public List<Relic> buyback=new List<Relic>();}
  MarketSave market=new MarketSave();
  public List<Relic> MarketStock {get{EnsureMarket();return market.stock;}}
  public List<Relic> MarketBuyback {get{return market.buyback;}}
  int MarketGeneration {get{int last=0;for(int i=0;i<Campaign.Route.Length;i++)if(ZoneUnlocked(Campaign.Route[i]))last=i;return Mathf.Clamp(Level,1,100)+last*128;}}
  void EnsureMarket()
  {
   if(market.generation==MarketGeneration && market.stock.Count==6)return;
   market.generation=MarketGeneration;market.stock.Clear();
   int rank=Mathf.Clamp(1+(market.generation/128)/4,1,4);
   for(int i=0;i<6;i++){var item=RollRelic(i%3,Mathf.Min(5,rank+i/3),market.generation*31+i*71);item.identified=true;market.stock.Add(item);}
  }
  public MarketSave CaptureMarket()
  {return new MarketSave{generation=market.generation,stock=CopyMarketList(market.stock,true),buyback=CopyMarketList(market.buyback,false)};}
  static List<Relic> CopyMarketList(List<Relic> source,bool emptySlots)
  {
   var result=new List<Relic>();if(source==null)return result;
   foreach(var r in source)
   {
    if(result.Count>=12)break;
    if(r==null){if(emptySlots)result.Add(null);continue;}
    if(r.slot<0 || r.slot>2 || r.rank<1 || r.rank>5){if(emptySlots)result.Add(null);continue;}
    var copy=CopyRelic(r);NormalizeRelic(copy);result.Add(copy);
   }
   return result;
  }
  public void RestoreMarket(MarketSave saved)
  {market=saved==null ? new MarketSave() : new MarketSave{generation=Mathf.Clamp(saved.generation,-1,2000),stock=CopyMarketList(saved.stock,true),buyback=CopyMarketList(saved.buyback,false)};}
  void ResetMarket(){market=new MarketSave();}
  bool AtMarket {get{return State==Phase.Dialogue && Service=="market" && Zone==Campaign.Town;}}
  public int MarketPrice(Relic item,bool sale=false)
  {if(item==null)return 0;return sale ? 5+item.rank*item.rank*4+(item.legend>0 ? 15 : 0) : 12+item.rank*item.rank*10+(item.legend>0 ? 35 : 0);}
  public bool BuyMarketItem(int index,bool buyback=false)
  {
   if(!AtMarket || Inventory.Count>=InventoryCapacity)return false;EnsureMarket();
   var list=buyback ? market.buyback : market.stock;if(index<0 || index>=list.Count || list[index]==null)return false;
   var item=list[index];int price=MarketPrice(item,buyback);if(Coins<price)return false;
   Coins-=price;Inventory.Add(CopyRelic(item));if(buyback)list.RemoveAt(index);else list[index]=null;
   SaveCheckpoint();Sound("coin");ShowNotice("Kupiono: "+RelicName(item)+" • załóż w ekwipunku [I]");return true;
  }
  public bool SellMarketItem(int index)
  {
   if(!AtMarket || index<0 || index>=Inventory.Count || Inventory[index].protectedItem)return false;
   var item=Inventory[index];Coins=Mathf.Min(1000000,Coins+MarketPrice(item,true));market.buyback.Add(CopyRelic(item));
   if(market.buyback.Count>12)market.buyback.RemoveAt(0);Inventory.RemoveAt(index);SaveCheckpoint();Sound("coin");return true;
  }
  public int SellCommonMarketItems()
  {
   if(!AtMarket)return 0;int count=0;
   for(int i=Inventory.Count-1;i>=0;i--)if(!Inventory[i].protectedItem && Inventory[i].identified && Inventory[i].rank==1 && Inventory[i].legend==0 && SellMarketItem(i))count++;
   return count;
  }
  public bool ToggleMarketLock(int index)
  {if(!AtMarket || index<0 || index>=Inventory.Count)return false;Inventory[index].protectedItem=!Inventory[index].protectedItem;SaveCheckpoint();return true;}
  void MarketOnZoneChange(int next)
  {if(Zone==Campaign.Town && next!=Campaign.Town)market.buyback.Clear();}
 }
 public sealed partial class KryptaHUD
 {
  int marketTab,marketSelection=-1;
  Vector2 marketScroll,marketDetailsScroll;
  bool marketSellConfirmation,marketDetailsLargeText;
  GUIStyle marketDetailsStyle;
  void MarketWindow()
  {
   Fill(new Rect(0,0,1280,720),new Color(.006f,.008f,.014f,.94f));RpgSurface(new Rect(32,22,1216,676));
   RpgAtlasIcon(new Rect(52,37,61,61),13);Label(126,43,760,44,"TARG OAKHAVEN",heading,paper);
   Label(128,91,760,25,"Eryk • relikty i pamiątki wypraw",small,muted);
   RpgBadge(new Rect(949,53,267,35),game.Coins+" ZŁ • PLECAK "+game.Inventory.Count+"/"+KryptaGame.InventoryCapacity,gold);
   bool outer=GUI.enabled;GUI.enabled=outer && !marketSellConfirmation;
   string[] tabs={"KUP","SPRZEDAJ","ODKUP"};
   for(int i=0;i<3;i++)if(Button(new Rect(66+i*224,135,214,37),tabs[i],marketTab==i)){marketTab=i;marketSelection=-1;marketScroll=marketDetailsScroll=Vector2.zero;}
   RpgAtlasIcon(new Rect(774,128,47,47),11);
   if(Button(new Rect(826,135,386,37),"ZWÓJ POWROTU • "+game.RecallScrollPrice+" ZŁ"))game.BuyRecallScroll();
   var items=marketTab==0 ? game.MarketStock : marketTab==1 ? game.Inventory : game.MarketBuyback;
   marketSelection=Mathf.Clamp(marketSelection,-1,items.Count-1);
   Rect marketViewport=new Rect(66,190,670,364);
   bool marketTooltips=marketViewport.Contains(Event.current.mousePosition);
   marketScroll=GUI.BeginScrollView(marketViewport,marketScroll,new Rect(0,0,646,Mathf.Max(354,((items.Count+3)/4)*178)));
   for(int i=0;i<items.Count;i++)
   {
    var item=items[i];float x=(i%4)*158,y=(i/4)*178;
    if(RpgItemTile(new Rect(x+14,y+5,124,124),item,marketSelection==i,allowTooltip:marketTooltips)){marketSelection=i;marketDetailsScroll=Vector2.zero;game.UISound("stone");}
    if(item==null){Label(x+4,y+137,148,27,"SPRZEDANE",center,muted);continue;}
    Label(x+4,y+130,148,24,game.MarketPrice(item,marketTab!=0)+" ZŁ",center,gold);
    Label(x+4,y+154,148,20,new[]{"ORĘŻ","PANCERZ","TALIZMAN"}[item.slot],center,RpgQualityColor(item));
   }
   GUI.EndScrollView();
   if(items.Count==0)
   {
    RpgAtlasIcon(new Rect(298,242,164,164),marketTab==2 ? 20 : 13);
    Label(122,423,555,65,marketTab==2 ? "Tu wrócą sprzedane relikty.\nOdkup jest dostępny do opuszczenia miasta." : "Plecak jest pusty. Przynieś znaleziska z wyprawy.",center,paper);
   }
   Rect detail=new Rect(762,190,450,364);Fill(detail,new Color(.025f,.027f,.037f,.96f));Outline(detail,bronze);
   var selected=marketSelection>=0 && marketSelection<items.Count ? items[marketSelection] : null;
   if(selected!=null)
   {
    RpgRelicIcon(new Rect(778,201,125,125),selected);RpgBadge(new Rect(919,207,274,25),RpgQualityName(selected),RpgQualityColor(selected));
    Label(919,244,274,77,game.RelicName(selected),text,paper);
    bool large=PlayerPrefs.GetInt("Nocturnis.LargeText",0)==1;
    if(marketDetailsStyle==null || marketDetailsLargeText!=large)
    {
     marketDetailsLargeText=large;
     marketDetailsStyle=new GUIStyle(small){fontSize=large ? 15 : 13,wordWrap=true};
    }
    string description=NocturnisInput.Format(game.RelicDescription(selected));
    string comparison=NocturnisInput.Format(game.RelicComparison(selected));
    float descriptionHeight=Mathf.Ceil(marketDetailsStyle.CalcHeight(new GUIContent(description),388));
    float comparisonHeight=Mathf.Ceil(marketDetailsStyle.CalcHeight(new GUIContent(comparison),388));
    float comparisonY=4+descriptionHeight+14;
    float full=Mathf.Max(200,comparisonY+comparisonHeight+12);
    marketDetailsScroll=GUI.BeginScrollView(new Rect(778,329,418,212),marketDetailsScroll,new Rect(0,0,396,full));
    // Use the measured style directly; Label would apply LargeText a second time.
    Color previousTextColor=GUI.contentColor;GUI.contentColor=paper;
    GUI.Label(new Rect(4,4,388,descriptionHeight),description,marketDetailsStyle);
    GUI.contentColor=muted;
    GUI.Label(new Rect(4,comparisonY,388,comparisonHeight),comparison,marketDetailsStyle);
    GUI.contentColor=previousTextColor;GUI.EndScrollView();
   }
   else
   {
    RpgAtlasIcon(new Rect(918,223,135,135),13);
    Label(794,383,385,107,"Wybierz relikt.\nZobacz jego właściwości i porównaj z wyposażeniem.\n\nOdkup: 12 ostatnio sprzedanych sztuk.",center,paper);
   }
   if(selected!=null)
   {
    GUI.enabled=outer && !marketSellConfirmation && (marketTab==1 ? !selected.protectedItem : game.Inventory.Count<KryptaGame.InventoryCapacity && game.Coins>=game.MarketPrice(selected,marketTab==2));
    if(Button(new Rect(762,570,450,39),marketTab==1 ? "SPRZEDAJ • "+game.MarketPrice(selected,true)+" ZŁ" : (marketTab==2 ? "ODKUP" : "KUP")+" • "+game.MarketPrice(selected,marketTab==2)+" ZŁ",true))
    {if(marketTab==1)game.SellMarketItem(marketSelection);else game.BuyMarketItem(marketSelection,marketTab==2);marketSelection=-1;}
   }
   GUI.enabled=outer && !marketSellConfirmation;
   if(marketTab==1)
   {
    GUI.enabled=GUI.enabled && selected!=null;
    if(Button(new Rect(66,570,325,39),selected!=null && selected.protectedItem ? "ZDEJMIJ OCHRONĘ" : "CHROŃ PRZED SPRZEDAŻĄ"))game.ToggleMarketLock(marketSelection);
    GUI.enabled=outer && !marketSellConfirmation;
    if(Button(new Rect(403,570,326,39),"SPRZEDAJ ZWYKŁE"))marketSellConfirmation=true;
   }
   else Label(72,574,655,42,"Kolor ramy pokazuje jakość. Wskaż ikonę, aby zobaczyć nazwę reliktu.",small,muted);
   Label(72,620,1133,24,"Chronione przedmioty pozostają w plecaku. Kupiony relikt założysz w ekwipunku [I].",small,muted);
   if(Button(new Rect(66,651,1146,30),"ZAMKNIJ TARG")){marketSelection=-1;marketSellConfirmation=false;game.Resume();}
   GUI.enabled=outer;
   if(marketSellConfirmation)
   {
    Fill(new Rect(0,0,1280,720),new Color(0,0,0,.82f));Frame(new Rect(335,250,610,215));
    Label(365,275,550,72,"Sprzedać wszystkie zwykłe, rozpoznane przedmioty? Chronione sztuki pozostaną w plecaku.",text,paper);
    if(Button(new Rect(365,376,260,43),"SPRZEDAJ",true)){game.SellCommonMarketItems();marketSellConfirmation=false;marketSelection=-1;}
    if(Button(new Rect(645,376,270,43),"ANULUJ"))marketSellConfirmation=false;
   }
  }
 }
}
