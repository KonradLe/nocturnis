using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaHUD
 {
  Vector2 questJournalScroll,questListScroll;
  int questJournalTab,questJournalSelection=-1,questChapterSelection=-1;
  static readonly string[] journalTabs={"FABUŁA","W TOKU","DO ODDANIA","UKOŃCZONE","DOSTĘPNE"};
  void QuestServiceLink()
  {
   int q=game.SideQuestForService(game.Service);if(q<0)return;
   string state=new[]{"PRZYJMIJ ZADANIE","ZADANIE W TOKU","ODBIERZ NAGRODĘ","ZADANIE UKOŃCZONE"}[game.SideQuestState(q)];
   if(Button(new Rect(260,606,370,35),state+" • "+SideQuestData.Titles[q]))game.OpenSideQuest(q);
  }
  bool SideQuestDialogue()
  {
   int q=game.ActiveSideQuest;if(q<0)return false;
   Frame(new Rect(205,60,870,610));Label(245,84,790,45,SideQuestData.Titles[q],heading,gold);
   Label(245,135,790,28,SideQuestData.NPCs[q],small,muted);
   Label(245,179,790,138,game.DialogueText,text,paper);
   Label(245,330,790,74,SideQuestData.Directions[q],text,gold);
   Label(245,421,790,55,game.SideQuestProgress(q),text,paper);
   Label(245,485,790,38,"Nagroda: "+game.SideQuestReward(q),small,gold);
   int state=game.SideQuestState(q);
   if(state==0){if(Button(new Rect(245,535,790,43),"PRZYJMUJĘ ZADANIE",true))game.AcceptSideQuest(q);}
   else if(state==2){if(Button(new Rect(245,535,790,43),"ODBIERZ NAGRODĘ",true))game.ClaimSideQuest(q);}
   else Label(245,540,790,38,state==3 ? "Ta nagroda została już odebrana." : "Postęp jest zapisywany. Wskazówki znajdziesz w dzienniku [J].",small,muted);
   if(Button(new Rect(245,600,385,37),"WRÓĆ DO ROZMOWY"))game.ReturnFromSideQuest();
   if(Button(new Rect(650,600,385,37),"WRÓĆ DO WYPRAWY"))game.Resume();
   return true;
  }
  int JournalFilterState(int tab){return tab==1 ? 1 : tab==2 ? 2 : tab==3 ? 3 : 0;}
  int JournalFilterCount(int tab)
  {
   if(tab==0)return QuestJournalData.ChapterTitles.Length;
   int n=0;for(int q=0;q<SideQuestData.Count;q++)if(game.SideQuestState(q)==JournalFilterState(tab))n++;return n;
  }
  Color JournalStateColor(int state){return state==3 ? new Color(.53f,.74f,.58f) : state==2 ? new Color(.95f,.76f,.37f) : state==1 ? paper : muted;}
  bool journalStoryExpanded,journalDirectionsExpanded;
  void SideQuestJournal()
  {
   JournalVisualStyles();
   Fill(new Rect(0,0,1280,720),new Color(.008f,.008f,.016f,.94f));
   Frame(new Rect(40,22,1200,676));
   RpgAtlasIcon(new Rect(68,43,58,58),22);
   Label(139,42,830,43,"DZIENNIK WYPRAWY",journalTitle,gold);
   Label(141,88,825,24,"AKT I  •  SERCE POD KATEDRĄ",small,muted);
   RpgBadge(new Rect(963,56,246,35),"SPRAWY MIESZKAŃCÓW  "+game.CompletedSideQuests+" / "+SideQuestData.Count,gold);
   for(int tab=0;tab<journalTabs.Length;tab++)
    if(Button(new Rect(69+tab*230,119,222,37),journalTabs[tab]+"  "+JournalFilterCount(tab),questJournalTab==tab))
    {questJournalTab=tab;questJournalSelection=-1;questJournalScroll=questListScroll=Vector2.zero;journalStoryExpanded=journalDirectionsExpanded=false;}
   RpgSurface(new Rect(68,172,279,435));RpgSurface(new Rect(359,172,852,435));
   if(questJournalTab==0)JournalChapters();else JournalSideEntries();
   RpgAccentLine(new Vector2(70,619),new Vector2(1208,619),bronze,1);
   RpgAtlasIcon(new Rect(371,631,39,39),24);
   Label(418,630,450,22,"ŚLEDZONE ZADANIA  "+game.TrackedQuestCount+" / "+QuestJournalData.MaxTracked,small,gold);
   Label(418,653,450,23,"Pierwszy przypięty cel prowadzi na minimapie.",small,muted);
   JournalTrackingAction();
   if(Button(new Rect(69,636,278,38),"WRÓĆ DO WYPRAWY  •  [J]"))game.Resume();
  }
  void JournalChapters()
  {
   if(questChapterSelection<0 || questChapterSelection>=QuestJournalData.ChapterTitles.Length)questChapterSelection=game.JournalMainChapter;
   questListScroll=GUI.BeginScrollView(new Rect(79,183,257,413),questListScroll,new Rect(0,0,236,QuestJournalData.ChapterTitles.Length*81));
   for(int c=0;c<QuestJournalData.ChapterTitles.Length;c++)
   {
    int state=game.JournalChapterState(c);float y=c*81;
    if(c<QuestJournalData.ChapterTitles.Length-1)RpgAccentLine(new Vector2(33,y+52),new Vector2(33,y+91),state==3 ? JournalStateColor(3) : bronze*.55f,2);
    if(JournalEntry(new Rect(0,y,235,74),chapterJournalIcons[c],QuestJournalData.ChapterTitles[c],state==3 ? "UKOŃCZONY" : state==1 ? "BIEŻĄCY ROZDZIAŁ" : "PRZED TOBĄ",state,questChapterSelection==c,false))
    {questChapterSelection=c;questJournalScroll=Vector2.zero;journalStoryExpanded=journalDirectionsExpanded=false;}
   }
   GUI.EndScrollView();
   int chapter=questChapterSelection;float height=JournalChapterDetails(chapter,false);
   questJournalScroll=GUI.BeginScrollView(new Rect(370,183,830,413),questJournalScroll,new Rect(0,0,807,Mathf.Max(413,height)));
   JournalChapterDetails(chapter,true);GUI.EndScrollView();
  }
  void JournalSideEntries()
  {
   int count=JournalFilterCount(questJournalTab),desired=JournalFilterState(questJournalTab);
   if(questJournalSelection<0 || questJournalSelection>=SideQuestData.Count || game.SideQuestState(questJournalSelection)!=desired)
   {questJournalSelection=-1;for(int q=0;q<SideQuestData.Count;q++)if(game.SideQuestState(q)==desired){questJournalSelection=q;break;}}
   if(count==0){JournalEmptyState();return;}
   questListScroll=GUI.BeginScrollView(new Rect(79,183,257,413),questListScroll,new Rect(0,0,236,count*81));
   int row=0;
   for(int q=0;q<SideQuestData.Count;q++)
   {
    if(game.SideQuestState(q)!=desired)continue;float y=row++*81;
    if(JournalEntry(new Rect(0,y,235,74),questJournalIcons[q],SideQuestData.Titles[q],QuestJournalData.StateLabel(desired),desired,questJournalSelection==q,game.IsQuestTracked(q)))
    {questJournalSelection=q;questJournalScroll=Vector2.zero;journalStoryExpanded=journalDirectionsExpanded=false;}
   }
   GUI.EndScrollView();
   int selected=questJournalSelection;float height=JournalQuestDetails(selected,false);
   questJournalScroll=GUI.BeginScrollView(new Rect(370,183,830,413),questJournalScroll,new Rect(0,0,807,Mathf.Max(413,height)));
   JournalQuestDetails(selected,true);GUI.EndScrollView();
  }
  void JournalTrackingAction()
  {
   Rect action=new Rect(903,636,305,38);int q=questJournalSelection;
   if(questJournalTab>0 && q>=0 && q<SideQuestData.Count)
   {
    int state=game.SideQuestState(q);
    if(state==1 || state==2)
    {
     bool enabled=GUI.enabled,tracked=game.IsQuestTracked(q);
     GUI.enabled=enabled && (tracked || game.TrackedQuestCount<QuestJournalData.MaxTracked);
     if(Button(action,tracked ? "ODEPNIJ Z EKRANU" : game.TrackedQuestCount>=QuestJournalData.MaxTracked ? "ODEPNIJ INNE • LIMIT 3" : "ŚLEDŹ NA EKRANIE",tracked))game.ToggleQuestTracking(q);
     GUI.enabled=enabled;
    }
    else RpgBadge(action,state==3 ? "NAGRODA ODEBRANA" : "PRZYJMIJ U ZLECENIODAWCY",JournalStateColor(state));
   }
   else RpgBadge(action,"GŁÓWNY CEL WYPRAWY JEST NA HUD",muted);
  }
  void QuestTrackerPanel()
  {
   if(game.State!=KryptaGame.Phase.Playing || game.IntroActive)return;
   int count=game.TrackedQuestCount;if(count==0)return;
   // Below the minimap, clear of left-hand buffs/tutorial and central cast feedback.
   Panel(new Rect(1000,342,255,34+count*50));
   if(Button(new Rect(1011,350,233,24),"ZADANIA "+count+" / 3  •  [J]"))game.OpenJournal();
   for(int i=0;i<count;i++)
   {
    int q=game.TrackedQuestAt(i);if(q<0)continue;float y=377+i*50;int state=game.SideQuestState(q);
    Label(1013,y,229,20,SideQuestData.Titles[q],small,state==2 ? gold : paper);
    Label(1013,y+20,229,27,game.QuestTrackerGoal(q),small,state==2 ? new Color(.75f,.91f,.62f) : muted);
   }
  }
 }
}