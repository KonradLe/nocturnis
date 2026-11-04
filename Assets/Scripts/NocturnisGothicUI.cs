using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaHUD
 {
  static readonly string[] beltKeyHints={"LPM","{KEYRAW:skill} / PPM","{KEYRAW:defense} / 1","{KEYRAW:extra0}","{KEYRAW:extra1}","{KEYRAW:extra2}"};
  static readonly string[] beltNavigationHints={"{KEYRAW:inventory}  EKWIPUNEK","{KEYRAW:book}  KSIĘGA","{KEYRAW:journal}  DZIENNIK"};
  Vector2 serviceScroll,waypointScroll;
  readonly Color parchment=new Color(.19f,.12f,.08f);
  public const float BeltScale=.60f;
  const float BeltOffsetX=640*(1-BeltScale),BeltOffsetY=710-722*BeltScale;
  GUIStyle beltKey,beltNumber,beltStatus,orbValue,orbLabel;
  static Rect BeltRect(Rect r){return new Rect(BeltOffsetX+r.x*BeltScale,BeltOffsetY+r.y*BeltScale,r.width*BeltScale,r.height*BeltScale);}
  public static Rect BeltSkillBounds(int i){return BeltRect(new Rect(365+i*96,506,82,81));}
  public static bool BeltContains(Vector2 point)
  {
   Vector2 p=(point-new Vector2(BeltOffsetX,BeltOffsetY))/BeltScale;
   return new Rect(355,480,585,205).Contains(p) || (p-new Vector2(235,544)).sqrMagnitude<90*90 || (p-new Vector2(1035,544)).sqrMagnitude<90*90;
  }
  void Frame(Rect r){ClassicFrame(r,Mathf.Min(r.height<160 ? 14 : 28,r.height*.25f));}
  void TitleScreen(){ClassicTitleScreen();}
  void Orb(float x,float y,float radius,float amount,Color color,string value,string label)
  {
   if(orbValue==null){orbValue=new GUIStyle(heading){alignment=TextAnchor.MiddleCenter,fontSize=Mathf.RoundToInt(16/BeltScale)};orbLabel=new GUIStyle(center){fontSize=Mathf.RoundToInt(10/BeltScale),wordWrap=false};}
   Circle(x,y,radius,new Color(.025f,.012f,.025f));
   float height=radius*2*Mathf.Clamp01(amount);
   if(height>0){GUI.BeginGroup(new Rect(x-radius,y+radius-height,radius*2,height));Color previous=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(0,-radius*2+height,radius*2,radius*2),orbSurface);GUI.color=previous;GUI.EndGroup();}
   Circle(x-radius*.3f,y-radius*.42f,radius*.13f,new Color(1,1,1,.13f));
   Label(x-radius,y-21,radius*2,37,value,orbValue,paper);
   Label(x-radius-12,y+19,radius*2+24,28,label,orbLabel,paper);
  }
  void GothicBelt()
  {
   bool previousEnabled=GUI.enabled;
   try{DrawGothicBelt();}finally{GUI.enabled=previousEnabled;}
   LowHealthVignette();
   if(game.Health/game.MaxHealth<.25f)Label(465,97,350,25,"NISKIE ZDROWIE  •  {KEYRAW:potion}: MIKSTURA",center,new Color(1,.62f,.57f));
  }
  void DrawGothicBelt()
  {
   if(beltKey==null){beltKey=new GUIStyle(center){fontSize=11,fontStyle=FontStyle.Bold,wordWrap=false};beltNumber=new GUIStyle(center){fontSize=13,fontStyle=FontStyle.Bold};beltStatus=new GUIStyle(center){fontSize=11,wordWrap=false};}
   // Scale only the decorative frame and glass. Controls and text use virtual-canvas coordinates.
   Matrix4x4 previous=GUI.matrix;
   GUI.matrix=previous*Matrix4x4.TRS(new Vector3(BeltOffsetX,BeltOffsetY,0),Quaternion.identity,Vector3.one*BeltScale);
   try{
    Orb(235,544,70,game.Health/game.MaxHealth,new Color(.68f,.035f,.085f),Mathf.CeilToInt(game.Health).ToString(),"/ "+game.MaxHealth+"  ŻYCIE");
    ResourceOrb(1035,544);
    if(hudArt)GUI.DrawTexture(new Rect(40,322,1200,400),hudArt,ScaleMode.StretchToFill,true);
    CrackedHealth();
   }finally{GUI.matrix=previous;}
   string[] keys=beltKeyHints;
   float[] cooldowns={0,game.PulseReady,game.DefenseReady,game.ExtraReady(0),game.ExtraReady(1),game.ExtraReady(2)};
   float[] durations={1,5,10,14,9,24};
   bool allowInput=GUI.enabled && game.State==KryptaGame.Phase.Playing;
   for(int i=0;i<6;i++)
   {
    Rect slot=BeltSkillBounds(i),inner=new Rect(slot.x+2,slot.y+2,slot.width-4,slot.height-4);
    float active=game.SkillActiveRemaining(i),cd=cooldowns[i];bool ready=game.CanUseSkill(i),locked=i==5 && game.Level<4;
    SkillIcon(inner,i);
    Fill(new Rect(inner.x,inner.y,inner.width,13),new Color(0,0,0,.85f));
    Label(slot.x,slot.y+1,slot.width,14,keys[i],beltKey,warmWhite);
    if(active>0){Fill(new Rect(inner.x,inner.yMax-19,inner.width,19),new Color(.10f,.28f,.12f,.94f));Label(slot.x,slot.yMax-21,slot.width,20,active.ToString("0.0"),beltNumber,new Color(.65f,1,.56f));}
    else if(!ready){Fill(new Rect(inner.x,inner.y+14,inner.width,inner.height-14),new Color(0,0,0,.77f));string reason=game.SkillBlockReason(i);string status=locked ? "POZ.4" : cd>0 ? cd.ToString("0.0") : game.Zone==Campaign.Town ? "MIASTO" : reason.StartsWith("Za mało") ? "ZASÓB" : "CHWILA";Label(slot.x,slot.y+19,slot.width,24,status,beltKey,locked ? gold : new Color(1,.68f,.45f));}
    if(cd>0)Fill(new Rect(inner.x,slot.yMax-3,inner.width*Mathf.Clamp01(cd/durations[i]),2),gold);
    else if(ready)Fill(new Rect(inner.x,slot.yMax-3,inner.width,2),new Color(.43f,.73f,.38f));
    if(allowInput && slot.Contains(Event.current.mousePosition))Outline(slot,warmWhite);
    QueueStoryTooltip(slot,i);
    GUI.enabled=allowInput;
    // Keep unavailable powers clickable to explain cooldown, resource and town restrictions.
    if(GUI.Button(slot,GUIContent.none,GUIStyle.none)){if(i==0)game.HUDStrike();else if(i==1)game.CastSkill(game.HUDAim);else if(i==2)game.CastDefense();else game.CastAdditional(i-3,game.HUDAim);}
    GUI.enabled=allowInput;
   }
   Rect xp=BeltRect(new Rect(365,491,562,7));Bar(xp,game.Experience/(game.Level*60f),gold);
   Rect statusLine=BeltRect(new Rect(355,604,585,28));
   Fill(statusLine,new Color(.025f,.018f,.015f,.85f));
   Label(statusLine.x,statusLine.y,statusLine.width,statusLine.height,game.Hero.person+"  •  POZ. "+game.Level+"  •  {KEYRAW:potion}: mikstura ×"+game.Potions,beltStatus,paper);
   string[] nav=beltNavigationHints;
   for(int i=0;i<3;i++){
    Rect r=BeltRect(new Rect(365+i*192,645,178,35));
    Fill(r,new Color(.035f,.026f,.02f,.93f));Outline(r,bronze);
    Label(r.x,r.y,r.width,r.height,nav[i],beltKey,paper);
    if(GUI.Button(r,GUIContent.none,GUIStyle.none)){if(i==0)game.OpenEquipment();else if(i==1)game.OpenBook();else game.OpenJournal();}
   }
  }
  void Equipment(){DetailedEquipment();}
  void PowerBook(){DetailedBook();}
  void Services()
  {
   if(game.Service=="market"){MarketWindow();return;}
   if(SideQuestDialogue())return;
   Frame(new Rect(225,75,830,585));Label(260,98,760,50,game.DialogueName,heading,gold);Label(260,155,755,135,game.DialogueText,text,paper);
   string service=game.Service;
   if(service=="forge")
   {
    Label(260,280,750,25,"Złoto "+game.Coins+" • Odłamki "+game.Materials,small,gold);
    for(int i=0;i<3;i++){int slot=i;GUI.enabled=game.GearRank(i)<5 && game.Coins>=game.ForgePrice(i);if(Button(new Rect(260+i*251,315,240,43),game.GearName(i)+" • "+game.ForgePrice(i)+" zł"))game.Forge(slot);}GUI.enabled=true;
    if(Button(new Rect(260,370,755,35),"WYKUJ RELIKT • 12 ODŁAMKÓW"))game.Craft();
    ItemServiceList(false);
   }
   else if(service=="alchemy")
   {
    Alchemist();
   }
   else if(service=="waypoint")
   {if(Button(new Rect(260,280,370,33),"ZWÓJ POWROTU • "+game.RecallScrollPrice+" ZŁ"))game.BuyRecallScroll();
    bool on=GUI.enabled;GUI.enabled=on && game.HasRecallTrace;if(Button(new Rect(641,280,374,33),"WRÓĆ PO ŚLADZIE"))game.ReturnAlongRecallTrace();GUI.enabled=on;
    Label(260,318,755,25,game.HasRecallTrace ? "Ślad: "+game.RecallTraceName : "Zwój zapisuje miejsce powrotu; ślad jest jednorazowy.",small,muted);
    int count=0;for(int z=0;z<Campaign.Count;z++)if(game.ZoneUnlocked(z))count++;waypointScroll=GUI.BeginScrollView(new Rect(260,348,755,240),waypointScroll,new Rect(0,0,732,Mathf.Max(234,((count+1)/2)*42)));int n=0;for(int z=0;z<Campaign.Count;z++)if(game.ZoneUnlocked(z)){if(Button(new Rect((n%2)*370,(n/2)*42,359,35),Campaign.Names[z])){game.Travel(z);break;}n++;}GUI.EndScrollView();}
   else if(service=="lore")
   {Label(260,310,750,70,"Cztery mauzolea. Malazar jest dalej niż groby królów. Najpierw ucisz kapłanów, którzy wskrzeszają tarczowników.",text,muted);if(Button(new Rect(260,410,755,45),"IDENTYFIKUJ PIERWSZY NIEZNANY RELIKT • 10 ZŁ"))game.Identify();}
   else if(service=="stash")ItemServiceList(true);
   else if(service=="wardrobe"){for(int i=0;i<3;i++)if(Button(new Rect(260,315+i*70,755,50),(game.Garb==i ? "● " : "")+KryptaGame.GarbNames[i]))game.ChangeGarb(i);}
   else if(service=="edra")
   {GUI.enabled=game.EdraChoice==0 && game.Remaining==0;if(Button(new Rect(260,330,755,50),"UWOLNIJ EDRĘ • 12 ODŁAMKÓW"))game.ChooseEdra(1);if(Button(new Rect(260,400,755,50),"OCZYŚĆ DRZEWO • KAMIEŃ PAMIĘCI"))game.ChooseEdra(2);GUI.enabled=true;Label(260,470,750,40,game.Remaining>0 ? "Najpierw pokonaj strażników korzeni." : "Decyzja zostanie zapisana.",small,muted);}
   QuestServiceLink();
   if(Button(game.SideQuestForService(game.Service)>=0 ? new Rect(641,606,374,35) : new Rect(260,606,755,35),"ZAMKNIJ • WRÓĆ DO WYPRAWY"))game.Resume();
  }
  void ItemServiceList(bool stash)
  {
   var stored=stash ? game.Stash : null;float top=stash ? 295 : 418,height=stash ? 295 : 175;
   Rect serviceViewport=new Rect(260,top,755,height);
   bool serviceTooltips=serviceViewport.Contains(Event.current.mousePosition);
   serviceScroll=GUI.BeginScrollView(serviceViewport,serviceScroll,new Rect(0,0,727,Mathf.Max(height-5,(game.Inventory.Count+(stash ? stored.Count : 0))*62)));
   int row=0;bool enabled=GUI.enabled;
   for(int i=0;i<game.Inventory.Count;i++,row++)
   {
    var item=game.Inventory[i];RpgItemTile(new Rect(3,row*62,54,54),item,false,allowTooltip:serviceTooltips);
    Label(68,row*62+2,396,30,game.RelicName(item),small,RpgQualityColor(item));
    Label(68,row*62+32,396,20,item.protectedItem ? "CHRONIONY" : RpgQualityName(item),small,muted);
    GUI.enabled=enabled && (stash || !item.protectedItem);
    if(Button(new Rect(475,row*62+9,235,34),stash ? "ODŁÓŻ DO KUFRA" : "ROZŁÓŻ NA ODŁAMKI")){if(stash)game.StoreItem(i,false);else game.Salvage(i);GUI.enabled=enabled;break;}
    GUI.enabled=enabled;
   }
   if(stash)for(int i=0;i<stored.Count;i++,row++)
   {
    RpgItemTile(new Rect(3,row*62,54,54),stored[i],false,allowTooltip:serviceTooltips);
    Label(68,row*62+2,396,30,game.RelicName(stored[i]),small,RpgQualityColor(stored[i]));
    Label(68,row*62+32,396,20,"W KUFRZE • "+RpgQualityName(stored[i]),small,muted);
    if(Button(new Rect(475,row*62+9,235,34),"ZABIERZ")){game.StoreItem(i,true);break;}
   }
   GUI.enabled=enabled;GUI.EndScrollView();
  }
 }
 public sealed partial class KryptaGame
 {
  public Vector3 HUDAim {get{Enemy e=enemies.Find(v=>v.hp>0 && Vector3.Distance(v.Position,HeroPosition)<10 && Visible(HeroPosition,v.Position));return e!=null ? e.Position : HeroPosition+facing*4;}}
  public void HUDStrike(){if(State==Phase.Playing)Strike(HUDAim);}
 }
}
