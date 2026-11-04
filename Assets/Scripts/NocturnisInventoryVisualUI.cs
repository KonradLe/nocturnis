using System.Collections.Generic;
using UnityEngine;

namespace Krypta
{
 public sealed partial class KryptaGame
 {
  // Inventory protection is available only in the paused equipment screen.
  // The existing merchant and smith still enforce their own action guards.
  public bool ToggleInventoryLock(int index)
  {
   if(State!=Phase.Paused || !EquipmentOpen || index<0 || index>=Inventory.Count || Inventory[index]==null)return false;
   Inventory[index].protectedItem=!Inventory[index].protectedItem;SaveCheckpoint();Sound("chain");return true;
  }
 }
 public sealed partial class KryptaHUD
 {
  int inspectedEquipmentSlot=-1;
  Vector2 equipmentCardScroll;
  GUIStyle inventoryCaption,inventoryBody,inventoryTitle,inventoryTiny;
  bool inventoryLargeText;
  static readonly string[] RpgEquipmentNames={"ORĘŻ","PANCERZ","PIECZĘĆ"};
  public static Rect RpgInventorySlotBounds(int index)
  {return new Rect(479+(index%6)*64,151+(index/6)*64,58,58);}
  public static Rect RpgEquippedSlotBounds(int index)
  {return new Rect(89+index*125,464,60,60);}
  void InventoryVisualStyles()
  {
   bool large=PlayerPrefs.GetInt("Nocturnis.LargeText",0)==1;
   if(inventoryBody!=null && inventoryLargeText==large)return;inventoryLargeText=large;
   inventoryCaption=new GUIStyle(small){fontSize=13,fontStyle=FontStyle.Bold,wordWrap=false};
   inventoryBody=new GUIStyle(text){fontSize=large ? 15 : 13,wordWrap=true};
   inventoryTitle=new GUIStyle(text){fontSize=17,fontStyle=FontStyle.Bold,wordWrap=true};
   inventoryTiny=new GUIStyle(small){fontSize=11,wordWrap=true};
  }
  void InventoryText(Rect rect,string value,GUIStyle style,Color color)
  {
   Color previous=GUI.contentColor;GUI.contentColor=color;
   GUI.Label(rect,NocturnisInput.Format(value),style);GUI.contentColor=previous;
  }
  float InventoryParagraph(float x,float y,float width,string value,GUIStyle style,Color color,bool draw)
  {
   value=NocturnisInput.Format(value);
   float height=Mathf.Ceil(style.CalcHeight(new GUIContent(value),width))+2;
   if(draw)InventoryText(new Rect(x,y,width,height),value,style,color);
   return height;
  }
  void SelectRpgInventoryItem(int index)
  {
   if(index<0 || index>=game.Inventory.Count)return;
   equipmentSelection=index;inspectGround=false;inspectedEquipmentSlot=-1;
   equipmentCardScroll=Vector2.zero;game.UISound("stone");
  }
  void SelectRpgEquippedItem(int slot)
  {
   if(slot<0 || slot>=3)return;
   inspectedEquipmentSlot=slot;inspectGround=false;equipmentCardScroll=Vector2.zero;game.UISound("stone");
  }
  bool EquipRpgInventorySelection()
  {
   if(inspectGround || inspectedEquipmentSlot>=0 || equipmentSelection<0 || equipmentSelection>=game.Inventory.Count)return false;
   int slot=game.Inventory[equipmentSelection].slot;
   if(!game.EquipItem(equipmentSelection))return false;
   equipmentSelection=Mathf.Clamp(equipmentSelection,0,Mathf.Max(0,game.Inventory.Count-1));
   inspectedEquipmentSlot=slot;equipmentCardScroll=Vector2.zero;return true;
  }
  // Pure selection normalization is shared by rendering and editor QA.
  // Empty collections keep their safe zero cursor and cannot trigger Equip.
  void NormalizeRpgInventorySelection(int nearbyCount)
  {
   equipmentSelection=Mathf.Clamp(equipmentSelection,0,Mathf.Max(0,game.Inventory.Count-1));
   groundSelection=Mathf.Clamp(groundSelection,0,Mathf.Max(0,nearbyCount-1));
   if(nearbyCount<=0)inspectGround=false;
  }
  void DrawRpgEquipment()
  {
   InventoryVisualStyles();bool previous=GUI.enabled;
   try
   {
    Fill(new Rect(0,0,1280,720),new Color(.006f,.008f,.014f,.88f));
    Frame(new Rect(22,22,1236,674));
    Label(48,38,690,41,"RELIKWIARZ PIELGRZYMA",heading,warmWhite);
    Label(815,43,410,30,"AKT I  •  POZIOM "+game.Level,center,gold);
    RpgAccentLine(new Vector2(46,87),new Vector2(1233,87),bronze,1);

    var nearby=game.NearbyRelics();
    NormalizeRpgInventorySelection(nearby.Count);
    RpgSurface(new Rect(36,101,410,485));
    RpgSurface(new Rect(464,101,408,485));
    RpgSurface(new Rect(890,101,354,485));
    DrawRpgEquipmentPortrait();
    DrawRpgBackpack(previous);
    var selected=inspectGround && nearby.Count>0 ? nearby[groundSelection] :
     inspectedEquipmentSlot>=0 ? game.EquippedRelic(inspectedEquipmentSlot) :
     game.Inventory.Count>0 ? game.Inventory[equipmentSelection] : null;
    DrawRpgEquipmentCard(selected);
    DrawRpgGroundItems(nearby,previous);
    if(Button(new Rect(1068,620,174,43),"ZAMKNIJ  [I]"))game.Resume();
   }
   finally{GUI.enabled=previous;}
  }
  void DrawRpgEquipmentPortrait()
  {
   InventoryText(new Rect(54,112,374,27),game.Hero.person,inventoryTitle,warmWhite);
   InventoryText(new Rect(54,139,374,22),game.Hero.name,inventoryTiny,gold);
   // Fit the complete render texture, including the weapon, without cropping.
   // This is deliberately larger than the former 243px character preview.
   if(portrait)GUI.DrawTexture(new Rect(49,151,384,304),portrait,ScaleMode.ScaleToFit,true);
   else Label(65,250,350,45,"Przygotowanie podglądu bohatera…",center,muted);
   for(int i=0;i<3;i++)
   {
    Rect slot=RpgEquippedSlotBounds(i);var worn=game.EquippedRelic(i);
    if(RpgItemTile(slot,worn,!inspectGround && inspectedEquipmentSlot==i,true))SelectRpgEquippedItem(i);
    Label(slot.x-31,slot.yMax+4,122,23,RpgEquipmentNames[i],center,gold);
   }
   RpgAccentLine(new Vector2(55,553),new Vector2(426,553),bronze,1);
   RpgAtlasIcon(new Rect(53,560,22,22),0);
   InventoryText(new Rect(79,559,93,25),game.AttackPower.ToString("0")+" ATK",inventoryCaption,paper);
   RpgAtlasIcon(new Rect(180,560,22,22),15);
   InventoryText(new Rect(206,559,94,25),game.MaxHealth+" HP",inventoryCaption,paper);
   InventoryText(new Rect(306,559,128,25),"+"+game.Regeneration.ToString("0.00")+" HP/s",inventoryCaption,paper);
  }
  void DrawRpgBackpack(bool outer)
  {
   InventoryText(new Rect(479,114,210,26),"PLECAK",inventoryCaption,gold);
   InventoryText(new Rect(731,114,124,26),game.Inventory.Count+" / "+KryptaGame.InventoryCapacity,inventoryCaption,paper);
   for(int i=0;i<KryptaGame.InventoryCapacity;i++)
   {
    var item=i<game.Inventory.Count ? game.Inventory[i] : null;
    // Pointer hover never equips. A tile click selects; only the separate action equips.
    if(RpgItemTile(RpgInventorySlotBounds(i),item,item!=null && equipmentSelection==i && inspectedEquipmentSlot<0) && item!=null)
     SelectRpgInventoryItem(i);
   }
   var bag=game.Inventory.Count>0 ? game.Inventory[equipmentSelection] : null;
   bool active=bag!=null && !inspectGround && inspectedEquipmentSlot<0;
   string caption=bag!=null ? game.RelicName(bag) : "Wolne miejsca na łupy z wyprawy";
   InventoryText(new Rect(479,416,378,37),caption,inventoryBody,bag!=null ? RpgQualityColor(bag) : muted);
   GUI.enabled=outer && active && bag.identified;
   if(Button(new Rect(479,459,184,35),bag!=null && !bag.identified ? "DO MALAKORA" : "ZAŁÓŻ",true))EquipRpgInventorySelection();
   GUI.enabled=outer && active;
   if(Button(new Rect(673,459,184,35),bag!=null && bag.protectedItem ? "ODCHROŃ" : "CHROŃ"))
    game.ToggleInventoryLock(equipmentSelection);
   GUI.enabled=outer;
   RpgAccentLine(new Vector2(480,505),new Vector2(856,505),bronze,1);
   RpgAtlasIcon(new Rect(480,513,26,26),13);
   InventoryText(new Rect(513,516,145,25),game.Coins+" złota",inventoryBody,paper);
   RpgAtlasIcon(new Rect(675,513,26,26),14);
   InventoryText(new Rect(708,516,150,25),game.Materials+" odłamków",inventoryBody,paper);
   InventoryText(new Rect(480,550,378,30),"Klejnoty w pieczęci: "+game.SocketedGems.Count+"/5  •  Zwoje Powrotu: "+game.RecallScrolls,inventoryTiny,muted);
  }
  void DrawRpgEquipmentCard(KryptaGame.Relic item)
  {
   string origin=inspectGround ? "RELIKT NA ZIEMI" : inspectedEquipmentSlot>=0 ? "ZAŁOŻONE WYPOSAŻENIE" : "WYBRANY PRZEDMIOT";
   InventoryText(new Rect(907,114,319,25),origin,inventoryCaption,gold);
   if(item==null)
   {
    RpgAtlasIcon(new Rect(1021,172,92,92),14);
    InventoryText(new Rect(912,290,310,132),"Wybierz ikonę przedmiotu w plecaku albo jeden z trzech slotów bohatera. Tutaj sprawdzisz jego właściwości i porównanie z wyposażeniem.",inventoryBody,muted);
    InventoryText(new Rect(912,470,310,70),"Przedmioty na ziemi pozostają dostępne także przy pełnym plecaku.",inventoryBody,muted);
    return;
   }
   RpgRelicIcon(new Rect(907,154,63,63),item);
   InventoryText(new Rect(983,151,243,51),game.RelicName(item),inventoryTitle,RpgQualityColor(item));
   InventoryText(new Rect(983,205,243,25),RpgQualityName(item)+(item.protectedItem ? "  •  CHRONIONY" : ""),inventoryTiny,RpgQualityColor(item));
   if(item.protectedItem)RpgAtlasIcon(new Rect(950,199,21,21),21);
   RpgAccentLine(new Vector2(907,230),new Vector2(1226,230),bronze,1);
   float height=EquipmentCardContent(item,false);
   equipmentCardScroll=GUI.BeginScrollView(new Rect(907,242,320,327),equipmentCardScroll,new Rect(0,0,299,Mathf.Max(325,height)));
   EquipmentCardContent(item,true);GUI.EndScrollView();
  }
  float EquipmentCardContent(KryptaGame.Relic item,bool draw)
  {
   const float width=291;float y=4;
   y+=InventoryParagraph(2,y,width,"WŁAŚCIWOŚCI",inventoryCaption,gold,draw)+8;
   y+=InventoryParagraph(2,y,width,game.RelicDescription(item),inventoryBody,paper,draw)+17;
   if(inspectedEquipmentSlot>=0 && !inspectGround)
   {
    y+=InventoryParagraph(2,y,width,"NA BOHATERZE",inventoryCaption,gold,draw)+8;
    y+=InventoryParagraph(2,y,width,"Wybierz przedmiot w plecaku, aby porównać jego właściwości i założyć go przyciskiem ZAŁÓŻ.",inventoryBody,muted,draw)+12;
    if(item.slot==2)
    {
     string gems="Gniazda pieczęci: "+game.SocketedGems.Count+"/5";
     for(int i=0;i<game.SocketedGems.Count;i++)gems+="\n• "+KryptaGame.GemNames[Mathf.Clamp(game.SocketedGems[i]-1,0,2)];
     y+=InventoryParagraph(2,y,width,gems,inventoryBody,paper,draw)+12;
    }
   }
   else
   {
    y+=InventoryParagraph(2,y,width,"PORÓWNANIE Z ZAŁOŻONYM",inventoryCaption,gold,draw)+8;
    y+=InventoryParagraph(2,y,width,game.RelicComparison(item),inventoryBody,paper,draw)+14;
    var worn=game.EquippedRelic(item.slot);
    y+=InventoryParagraph(2,y,width,"Obecnie: "+game.GearName(item.slot)+" +"+game.GearRank(item.slot),inventoryBody,muted,draw)+7;
    if(worn.legend>0)y+=InventoryParagraph(2,y,width,game.RelicLegendLabel(worn),inventoryBody,muted,draw)+10;
   }
   y+=InventoryParagraph(2,y,width,item.protectedItem ? "Ochrona blokuje sprzedaż i rozbiórkę. Właściwości pozostają zapisane także po schowaniu do kufra." :
    "Przycisk CHROŃ zabezpiecza przed sprzedażą i rozbiórką. Nie zmienia statystyk przedmiotu.",inventoryTiny,muted,draw)+12;
   return y;
  }
  void DrawRpgGroundItems(List<KryptaGame.Relic> nearby,bool outer)
  {
   RpgSurface(new Rect(36,603,1010,78));
   if(nearby.Count==0)
   {
    InventoryText(new Rect(53,616,965,24),"ŁUPY NA ZIEMI",inventoryCaption,gold);
    InventoryText(new Rect(53,645,965,24),"Pełny plecak? Podejdź do reliktu i otwórz ekwipunek. Możesz wymienić wybraną sztukę. Kufer w Oakhaven jest wspólny dla profili.",inventoryTiny,muted);
    return;
   }
   var item=nearby[groundSelection];
   if(RpgItemTile(new Rect(50,615,52,52),item,inspectGround))
   {inspectGround=true;inspectedEquipmentSlot=-1;equipmentCardScroll=Vector2.zero;}
   InventoryText(new Rect(114,612,564,22),"NA ZIEMI "+(groundSelection+1)+" / "+nearby.Count+"  •  "+game.RelicName(item),inventoryTiny,RpgQualityColor(item));
   if(Button(new Rect(114,639,38,27),"<"))
   {groundSelection=(groundSelection+nearby.Count-1)%nearby.Count;inspectGround=true;inspectedEquipmentSlot=-1;equipmentCardScroll=Vector2.zero;}
   if(Button(new Rect(162,639,452,27),"PORÓWNAJ RELIKT NA ZIEMI"))
   {inspectGround=true;inspectedEquipmentSlot=-1;equipmentCardScroll=Vector2.zero;}
   if(Button(new Rect(624,639,38,27),">"))
   {groundSelection=(groundSelection+1)%nearby.Count;inspectGround=true;inspectedEquipmentSlot=-1;equipmentCardScroll=Vector2.zero;}
   GUI.enabled=outer && game.Inventory.Count>0;
   if(Button(new Rect(685,615,344,32),"ZAMIEŃ ZA WYBRANY W PLECAKU"))
   {
    if(game.SwapGroundRelic(groundSelection,equipmentSelection))
    {inspectGround=false;inspectedEquipmentSlot=-1;equipmentCardScroll=Vector2.zero;}
   }
   GUI.enabled=outer;
   InventoryText(new Rect(685,651,344,20),"Wybrany przedmiot z plecaka pozostanie na ziemi.",inventoryTiny,muted);
  }
 }
}
