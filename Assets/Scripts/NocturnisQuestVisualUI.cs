using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaHUD
 {
  // These are symbolic journal emblems. The cathedral image is atmosphere, not a map screenshot.
  static readonly int[] chapterJournalIcons={24,10,23,22,9};
  static readonly int[] questJournalIcons={19,0,17,22,23};
  static readonly Color[] journalTints={new Color(.66f,.75f,.96f),new Color(.95f,.65f,.39f),new Color(.48f,.80f,.78f),new Color(.68f,.53f,.87f),new Color(.73f,.74f,.50f)};
  GUIStyle journalTitle,journalCard,journalMeta,journalBody,journalObjective,journalAmount,journalCenter;
  bool journalLargeType;
  void JournalVisualStyles()
  {
   bool large=PlayerPrefs.GetInt("Nocturnis.LargeText",0)==1;
   if(journalTitle!=null && journalLargeType==large)return;
   journalLargeType=large;
   journalTitle=new GUIStyle(heading){fontSize=31};
   journalCard=new GUIStyle(text){fontSize=large ? 15 : 14,fontStyle=FontStyle.Bold,wordWrap=true};
   journalMeta=new GUIStyle(small){fontSize=large ? 14 : 12,wordWrap=true};
   journalBody=new GUIStyle(text){fontSize=large ? 20 : 18,wordWrap=true};
   journalObjective=new GUIStyle(text){fontSize=large ? 18 : 16,wordWrap=true};
   journalAmount=new GUIStyle(heading){fontSize=large ? 23 : 21};
   journalCenter=new GUIStyle(journalMeta){alignment=TextAnchor.MiddleCenter};
  }
  void JournalInset(Rect r)
  {
   Fill(r,new Color(.025f,.029f,.04f,.98f));
   if(menuArt)
   {
    Color old=GUI.color;GUI.color=new Color(.57f,.44f,.37f,.16f);
    GUI.DrawTexture(r,menuArt,ScaleMode.ScaleAndCrop,true);GUI.color=old;
   }
   Outline(new Rect(r.x+3,r.y+3,r.width-6,r.height-6),new Color(.35f,.27f,.18f,.4f));
  }
  bool JournalEntry(Rect r,int icon,string caption,string status,int state,bool selected,bool pinned)
  {
   bool hover=GUI.enabled && r.Contains(Event.current.mousePosition);
   JournalInset(r);
   if(selected || hover)Fill(r,new Color(.36f,.25f,.12f,selected ? .27f : .13f));
   if(selected){Outline(r,gold);Fill(new Rect(r.x,r.y,3,r.height),gold);}
   else Outline(r,new Color(.38f,.30f,.22f,.36f));
   RpgAtlasIcon(new Rect(r.x+8,r.y+11,52,52),icon);
   Label(r.x+68,r.y+8,r.width-78,39,caption,journalCard,state==3 ? muted : paper);
   Label(r.x+68,r.y+50,r.width-78,19,(pinned ? "• " : "")+status,journalMeta,JournalStateColor(state));
   bool clicked=GUI.Button(r,GUIContent.none,GUIStyle.none);
   if(clicked)game.UISound("ui-click");return clicked;
  }
  void JournalBanner(string titleText,string subtitle,string status,int level,int icon,int tint)
  {
   Rect r=new Rect(0,0,807,158);Color color=journalTints[Mathf.Clamp(tint,0,journalTints.Length-1)];
   Fill(r,new Color(.045f,.043f,.052f));
   Texture2D illustration=menuArt ? menuArt : bookArt;
   if(illustration)
   {
    Color old=GUI.color;GUI.color=new Color(color.r*.76f,color.g*.76f,color.b*.76f,1);
    GUI.DrawTextureWithTexCoords(r,illustration,new Rect(.03f+tint*.075f,.25f,.61f,.27f),true);GUI.color=old;
   }
   for(int n=0;n<10;n++)Fill(new Rect(0,n*15.8f,807,16),new Color(.008f,.009f,.016f,.25f+n*.054f));
   RpgAccentLine(new Vector2(0,157),new Vector2(807,157),color,2);
   RpgBadge(new Rect(18,13,181,27),"ZALECANY POZIOM  "+level,color);
   RpgBadge(new Rect(590,13,199,27),status,color);
   Circle(60,92,43,new Color(.03f,.029f,.04f,.88f));
   RpgAtlasIcon(new Rect(18,50,84,84),icon);
   Label(116,57,667,44,titleText,heading,paper);
   Label(118,108,661,40,subtitle,journalObjective,color);
  }
  void JournalEmptyState()
  {
   RpgAtlasIcon(new Rect(155,275,104,104),22);
   Label(95,399,226,46,"ŻADNYCH WPISÓW",journalCenter,gold);
   Rect banner=new Rect(371,183,827,176);
   if(menuArt)GUI.DrawTexture(banner,menuArt,ScaleMode.ScaleAndCrop,true);
   Fill(banner,new Color(.01f,.01f,.02f,.62f));
   RpgAtlasIcon(new Rect(720,206,96,96),24);
   string message=questJournalTab==1 ? "Twoja kronika czeka na nowe zadanie." : questJournalTab==2 ? "Żadna sprawa nie jest jeszcze gotowa do oddania." : questJournalTab==3 ? "Ukończone sprawy zapiszą się w tej kronice." : "Znasz już wszystkie sprawy mieszkańców.";
   Label(401,378,762,68,message,heading,gold);
   Label(404,459,750,80,questJournalTab==1 ? "Porozmawiaj z mieszkańcami. Ich prośby znajdziesz w zakładce Dostępne." : "Wybierz inną zakładkę. Droga przez Akt I pozostaje w zakładce Fabuła.",journalBody,paper);
  }
  float JournalBlock(float y,string value,GUIStyle style,Color color,bool draw)
  {
   const float width=775;
   float h=Mathf.Max(28,Mathf.Ceil(style.CalcHeight(new GUIContent(NocturnisInput.Format(value)),width))+6);
   // Match the accessibility growth performed by the shared Label helper.
   if(journalLargeType && style.fontSize<=17 && h>=40)
   {var measured=new GUIStyle(style){fontSize=style.fontSize+2};h=Mathf.Ceil(measured.CalcHeight(new GUIContent(NocturnisInput.Format(value)),width))+6;}
   if(draw)Label(16,y,width,h,value,style,color);return y+h+9;
  }
  float JournalFoldout(float y,string titleText,ref bool expanded,string copy,bool draw)
  {
   if(draw && Button(new Rect(12,y,783,31),(expanded ? "−  " : "+  ")+titleText,expanded))expanded=!expanded;
   y+=40;if(expanded)y=JournalBlock(y,copy,journalBody,paper,draw);return y;
  }
  float JournalQuestDetails(int q,bool draw)
  {
   if(q<0 || q>=SideQuestData.Count)return 0;
   int state=game.SideQuestState(q);
   if(draw)
   {
    JournalBanner(SideQuestData.Titles[q],SideQuestData.NPCs[q]+"  •  "+(q==4 ? "Cmentarzysko" : "Oakhaven"),QuestJournalData.StateLabel(state),QuestJournalData.SuggestedLevels[q],questJournalIcons[q],q);
    RpgAtlasIcon(new Rect(12,165,25,25),24);
    Label(44,169,747,24,Campaign.Names[SideQuestData.Zones[q]],journalMeta,muted);
   }
   float y=194;
   string[] objectives=game.QuestJournalObjectives(q).Split('\n');
   for(int i=0;i<objectives.Length;i++)
   {
    string objective=objectives[i];bool done=i==0 ? state==3 || game.SideQuestObjectCount(q)>=SideQuestData.Required[q] : objective.EndsWith(": gotowe") || objective.EndsWith(": odebrana");
    bool ready=!done && i==objectives.Length-1 && state==2;
    float rowHeight=Mathf.Max(30,Mathf.Ceil(journalObjective.CalcHeight(new GUIContent(NocturnisInput.Format(objective)),734))+8);
    if(draw)
    {
     Color stateColor=done ? JournalStateColor(3) : ready ? gold : muted;
     Fill(new Rect(16,y+4,18,18),new Color(.08f,.075f,.075f));Outline(new Rect(16,y+4,18,18),stateColor);
     if(done)
     {
      RpgAccentLine(new Vector2(20,y+13),new Vector2(24,y+17),stateColor,2);
      RpgAccentLine(new Vector2(24,y+17),new Vector2(32,y+7),stateColor,2);
     }
     else if(ready)Diamond(25,y+13,4,gold);
     Label(47,y,734,rowHeight,objective,journalObjective,done ? muted : ready ? gold : paper);
    }
    y+=rowHeight;
   }
   y+=6;
   if(draw)Label(16,y,775,21,state==3 ? "NAGRODA ODEBRANA" : "NAGRODA U ZLECENIODAWCY",journalMeta,state==3 ? JournalStateColor(3) : gold);
   y+=24;
   if(draw)JournalQuestRewards(q,y,state==3);y+=66;
   y=JournalFoldout(y,"SŁOWA ZLECENIODAWCY",ref journalStoryExpanded,state==3 ? SideQuestData.Endings[q] : SideQuestData.Offers[q],draw);
   y=JournalFoldout(y,"WSKAZÓWKI I DROGA DO CELU",ref journalDirectionsExpanded,SideQuestData.Directions[q],draw);
   if(state==0)y=JournalBlock(y,"Przyjmij zadanie w rozmowie ze zleceniodawcą. Nagrodę odbierzesz po powrocie do niego.",journalMeta,muted,draw);
   return y+12;
  }
  void JournalRewardTile(Rect r,int icon,string amount,string caption,bool claimed)
  {
   JournalInset(r);Outline(r,claimed ? new Color(.30f,.43f,.30f,.7f) : new Color(.46f,.34f,.19f,.6f));
   RpgAtlasIcon(new Rect(r.x+5,r.y+7,48,48),icon);
   Label(r.x+60,r.y+6,r.width-66,28,amount,journalAmount,claimed ? muted : gold);
   Label(r.x+60,r.y+35,r.width-66,22,caption,journalMeta,muted);
  }
  void JournalQuestRewards(int q,float y,bool claimed)
  {
   int tiles=q==0 ? 3 : 4;float width=(775-(tiles-1)*8)/tiles;
   JournalRewardTile(new Rect(16,y,width,62),13,SideQuestData.Gold[q].ToString(),"ZŁOTO",claimed);
   JournalRewardTile(new Rect(16+(width+8),y,width,62),14,SideQuestData.Materials[q].ToString(),"ODŁAMKI",claimed);
   JournalRewardTile(new Rect(16+(width+8)*2,y,width,62),22,SideQuestData.XP[q].ToString(),"PD",claimed);
   if(q>0)
   {
    int icon=q==1 ? 5 : q==2 ? 12 : q==3 ? 9 : 10;
    string amount=q==1 ? "Min. 2" : q==2 ? "Ulepszone" : q==3 ? "Min. 3" : "Bursztyn";
    string caption=q==1 ? "RANGA PANCERZA" : q==2 ? "MIKSTURY" : q==3 ? "RANGA PIECZĘCI" : "KLEJNOT";
    JournalRewardTile(new Rect(16+(width+8)*3,y,width,62),icon,amount,caption,claimed);
   }
  }
  float JournalChapterDetails(int chapter,bool draw)
  {
   int state=game.JournalChapterState(chapter),start=QuestJournalData.ChapterStart[chapter],end=QuestJournalData.ChapterEnd[chapter];
   if(draw)JournalBanner(QuestJournalData.ChapterTitles[chapter],"KRONIKA WYPRAWY  •  AKT I",state==3 ? "UKOŃCZONY" : state==1 ? "BIEŻĄCY ROZDZIAŁ" : "PRZED TOBĄ",QuestJournalData.ChapterLevels[chapter],chapterJournalIcons[chapter],chapter);
   float y=171;
   if(draw)Label(16,y,775,24,"SZLAK ROZDZIAŁU",journalMeta,gold);y+=29;
   int count=end-start+1;float width=(775-(count-1)*8)/count;
   if(draw)
    for(int n=start;n<=end;n++)
    {
     int zone=Campaign.Route[n];float x=16+(n-start)*(width+8);bool here=zone==game.Zone,unlocked=game.ZoneUnlocked(zone);
     Rect tile=new Rect(x,y,width,75);JournalInset(tile);Outline(tile,here ? gold : bronze*.55f);
     RpgAtlasIcon(new Rect(x+8,y+8,25,25),here ? 24 : unlocked ? chapterJournalIcons[chapter] : 21);
     Label(x+40,y+8,width-47,22,here ? "JESTEŚ TUTAJ" : unlocked ? "ODKRYTO" : "PRZED TOBĄ",journalMeta,here ? gold : muted);
     Label(x+9,y+36,width-18,38,Campaign.Names[zone],journalMeta,paper);
    }
   y+=87;
   if(chapter==game.JournalMainChapter && state!=3)
   {
    if(draw)RpgAtlasIcon(new Rect(12,y+1,30,30),24);
    float h=Mathf.Max(43,Mathf.Ceil(journalObjective.CalcHeight(new GUIContent(NocturnisInput.Format(game.CampaignObjective)),739))+8);
    if(draw)Label(49,y,739,h,game.CampaignObjective,journalObjective,gold);y+=h+10;
   }
   if(draw)
   {
    RpgAtlasIcon(new Rect(15,y,49,49),chapter==0 ? Mathf.Clamp(game.SelectedHero,0,4) : chapter==4 ? 9 : 14);
    Label(77,y+2,710,23,chapter==0 ? "RZADKA BROŃ KLASOWA • USŁUGI OAKHAVEN" : chapter==4 ? "ODŁAMEK VARKOSA • FINAŁ AKTU I" : "ŁUPY • DOŚWIADCZENIE • DALSZY SZLAK",journalMeta,gold);
   }
   y+=52;
   y=JournalFoldout(y,"HISTORIA I CELE ROZDZIAŁU",ref journalStoryExpanded,QuestJournalData.ChapterCopy[chapter],draw);
   string directions=game.JournalChapterLocations(chapter)+"\n\n"+game.JournalChapterReward(chapter);
   if(chapter==game.JournalMainChapter && state!=3 && Campaign.EventStage(game.Zone))directions+="\n\n"+game.StageDirections;
   y=JournalFoldout(y,"SZCZEGÓŁY SZLAKU I NAGRÓD",ref journalDirectionsExpanded,directions,draw);
   return y+12;
  }
 }
}