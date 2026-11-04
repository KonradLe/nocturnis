using System;
using System.Collections.Generic;
using UnityEngine;
namespace Krypta
{
 // IDs belong to the save contract. Display titles may change without losing pins.
 public static class QuestJournalData
 {
  public const int MaxTracked=3;
  public static readonly string[] IDs={"side.thirteenth-lamp","side.embers","side.clean-water","side.forgotten-names","side.last-burial"};
  public static readonly int[] SuggestedLevels={5,3,7,9,4};
  public static readonly string[] ShortGoals={"Oczyść lampy","Ocal naczynia żaru","Zamknij dopływy","Odczytaj imiona","Odzyskaj znaczniki"};
  public static readonly string[] ChapterTitles={"I. Ostatnie światła","II. Droga przez popiół","III. Głosy pod cmentarzem","IV. Imiona królów","V. Serce pod katedrą"};
  public static readonly int[] ChapterStart={0,2,4,7,11};
  public static readonly int[] ChapterEnd={1,3,6,10,13};
  public static readonly int[] ChapterLevels={1,3,5,7,10};
  public static readonly string[] ChapterCopy={
   "Przeżyj zasadzkę na Trakcie Wisielców. Pokonaj Nadzorcę, przejdź przez most i porozmawiaj z Malakorem w Oakhaven. Mieszkańcy oferują usługi i zadania poboczne.",
   "Przejdź przez Martwe Błonia i Wąwóz Zgaszonych Sygnałów. Zabezpiecz sygnały i pokonaj strażnika przejścia. W Błoniach odchodzą ścieżki do lasu i spalonej farmy.",
   "Osłoń Orena przed trzema falami na cmentarzu, następnie zejdź do katakumb. W Krużganku Skrępowanych zerwij więzy i pokonaj strażnika. Boczne wejście na cmentarzu prowadzi do kaplicy Irmy.",
   "Przemierz dalsze katakumby, ucisz śpiewające krypty w Galeriach Dzwoniących Kości i dotrzyj do Krypty Królów-Biskupów. Cysterna i ossuarium kryją sprawy Vespery oraz Malakora.",
   "Wygaszaj paleniska Kuźni Czarnego Serca i zerwij pieczęcie na Progu Ostatniej Przysięgi. Pokonaj Malazara w Komnacie Pękniętego Serca, a potem podejdź do odłamka Varkosa i odzyskaj go [E]."};
  public static int Index(string id){return Array.IndexOf(IDs,id);}
  public static string StateLabel(int state){return state==3 ? "UKOŃCZONE" : state==2 ? "DO ODDANIA" : state==1 ? "W TOKU" : "DOSTĘPNE";}
 }
 public sealed partial class KryptaGame
 {
  readonly List<string> trackedQuestIds=new List<string>();
  readonly int[] journalObservedStates={-1,-1,-1,-1,-1};
  float nextJournalObservation;
  bool journalActFinished;
  public bool JournalActFinished {get{return journalActFinished || State==Phase.Won;}}
  public void RestoreJournalActFinished(bool finished){journalActFinished=finished;}
  public int TrackedQuestCount {get{PruneQuestTracking();return trackedQuestIds.Count;}}
  public int PrimaryTrackedQuest {get{PruneQuestTracking();return trackedQuestIds.Count==0 ? -1 : QuestJournalData.Index(trackedQuestIds[0]);}}
  public int TrackedQuestAt(int index){PruneQuestTracking();return index>=0 && index<trackedQuestIds.Count ? QuestJournalData.Index(trackedQuestIds[index]) : -1;}
  public bool IsQuestTracked(int q){return q>=0 && q<SideQuestData.Count && (SideQuestState(q)==1 || SideQuestState(q)==2) && trackedQuestIds.Contains(QuestJournalData.IDs[q]);}
  void PruneQuestTracking()
  {
   for(int i=trackedQuestIds.Count-1;i>=0;i--){int q=QuestJournalData.Index(trackedQuestIds[i]);if(q<0 || SideQuestState(q)<1 || SideQuestState(q)>2)trackedQuestIds.RemoveAt(i);}
  }
  public string[] CaptureQuestTracking(){PruneQuestTracking();return trackedQuestIds.ToArray();}
  public void ResetQuestJournal()
  {
   journalActFinished=false;trackedQuestIds.Clear();for(int q=0;q<SideQuestData.Count;q++)journalObservedStates[q]=-1;nextJournalObservation=0;
  }
  public void RestoreQuestTracking(string[] ids,int legacy=-1)
  {
   ResetQuestJournal();
   if(ids!=null)
    foreach(string id in ids){int q=QuestJournalData.Index(id);if(q>=0 && (SideQuestState(q)==1 || SideQuestState(q)==2) && !trackedQuestIds.Contains(id) && trackedQuestIds.Count<QuestJournalData.MaxTracked)trackedQuestIds.Add(id);}
   else if(legacy>=0 && legacy<SideQuestData.Count && (SideQuestState(legacy)==1 || SideQuestState(legacy)==2))trackedQuestIds.Add(QuestJournalData.IDs[legacy]);
   // Restoring a completed objective does not replay its feedback or grant rewards.
   for(int q=0;q<SideQuestData.Count;q++)journalObservedStates[q]=SideQuestState(q);
  }
  public bool ToggleQuestTracking(int q)
  {
   if(q<0 || q>=SideQuestData.Count || (SideQuestState(q)!=1 && SideQuestState(q)!=2))return false;
   PruneQuestTracking();string id=QuestJournalData.IDs[q];
   if(trackedQuestIds.Contains(id))trackedQuestIds.Remove(id);
   else
   {
    if(trackedQuestIds.Count>=QuestJournalData.MaxTracked){ShowNotice("Możesz śledzić 3 zadania. Odepnij jedno w dzienniku [J].");return false;}
    trackedQuestIds.Add(id);
   }
   SaveCheckpoint();return true;
  }
  public void OnJournalQuestAccepted(int q)
  {
   if(q<0 || q>=SideQuestData.Count || (SideQuestState(q)!=1 && SideQuestState(q)!=2))return;
   PruneQuestTracking();string id=QuestJournalData.IDs[q];
   if(!trackedQuestIds.Contains(id) && trackedQuestIds.Count<QuestJournalData.MaxTracked)trackedQuestIds.Add(id);
   journalObservedStates[q]=SideQuestState(q);
  }
  public void OnJournalQuestClaimed(int q)
  {
   if(q<0 || q>=SideQuestData.Count)return;
   trackedQuestIds.Remove(QuestJournalData.IDs[q]);journalObservedStates[q]=SideQuestState(q);
  }
  public void TickQuestJournal()
  {
   if(State!=Phase.Playing || Time.unscaledTime<nextJournalObservation)return;
   nextJournalObservation=Time.unscaledTime+.25f;
   for(int q=0;q<SideQuestData.Count;q++)
   {
    int state=SideQuestState(q),before=journalObservedStates[q];journalObservedStates[q]=state;
    if(before==1 && state==2){ShowNotice("Cele ukończone: "+SideQuestData.Titles[q]+" • Wróć do: "+SideQuestData.NPCs[q]);Sound("shrine");}
   }
   PruneQuestTracking();
  }
  public string QuestTrackerGoal(int q)
  {
   int state=SideQuestState(q);if(state==2)return "Nagroda: "+SideQuestData.NPCs[q];
   if(q==1 && SideQuestObjectCount(q)>=3)return "Zanieś żar do misy przy wejściu";
   if(q==3 && SideQuestObjectCount(q)>=4)return "Pokonaj Strażnika Wymazanych";
   if(q==4 && SideQuestObjectCount(q)>=3)return "Obroń Orena • trzy fale na cmentarzu";
   return QuestJournalData.ShortGoals[q]+": "+SideQuestObjectCount(q)+" / "+SideQuestData.Required[q];
  }
  public string QuestJournalObjectives(int q)
  {
   int state=SideQuestState(q),count=state==3 ? SideQuestData.Required[q] : SideQuestObjectCount(q);
   string result=SideQuestData.Objects[q]+": "+count+" / "+SideQuestData.Required[q];
   if(q==1)result+="\nDostarcz żar do misy: "+(state==3 || (sideObjects[1]&256)!=0 ? "gotowe" : "jeszcze nie");
   if(q==3)result+="\nPokonaj Strażnika Wymazanych: "+(state==3 || OssuaryGuardianDead ? "gotowe" : "jeszcze nie");
   if(q==4)result+="\nObroń Orena przed trzema falami: "+(state==3 || GraveDefenseComplete ? "gotowe" : "jeszcze nie");
   result+="\nOdbierz nagrodę u zleceniodawcy: "+(state==3 ? "odebrana" : state==2 ? "możesz wrócić" : "po wykonaniu celów");
   return result;
  }
  public int JournalMainChapter
  {
   get{for(int c=0;c<QuestJournalData.ChapterTitles.Length;c++)if(JournalChapterState(c)!=3)return c;return QuestJournalData.ChapterTitles.Length-1;}
  }
  public int JournalChapterState(int chapter)
  {
   if(chapter<0 || chapter>=QuestJournalData.ChapterTitles.Length)return 0;
   int first=QuestJournalData.ChapterStart[chapter],last=QuestJournalData.ChapterEnd[chapter];
   bool done=chapter==4 ? JournalActFinished : ZoneUnlocked(Campaign.Route[last+1]);
   if(chapter==0 && QuestAccepted)done=true;
   if(done)return 3;
   return ZoneUnlocked(Campaign.Route[first]) ? 1 : 0;
  }
  public string JournalChapterLocations(int chapter)
  {
   var names=new List<string>();for(int n=QuestJournalData.ChapterStart[chapter];n<=QuestJournalData.ChapterEnd[chapter];n++)names.Add(Campaign.Names[Campaign.Route[n]]);
   return string.Join(" → ",names.ToArray());
  }
  public string JournalChapterReward(int chapter)
  {
   if(chapter==0)return "Nadzorca: rzadka broń klasowa. Oakhaven: kowal, alchemik i usługi mieszkańców.";
   if(chapter==4)return "Łupy i doświadczenie z walk. Odzyskanie odłamka Varkosa kończy Akt I.";
   return "Łupy i doświadczenie z walk oraz dostęp do dalszej części szlaku. Pełne oczyszczenie lokacji daje złoto i odłamki; zadania poboczne mają oddzielne nagrody.";
  }
 }
}