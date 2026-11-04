namespace Krypta
{
 public static class Campaign
 {
  // Stable IDs retain compatibility with the original town, forest and crypt save.
  public const int Town=0,Forest=1,Crypt=2,Road=3,Fields=4,Cemetery=5,Crypt2=6,Crypt3=7,Kings=8,Heart=9,Cellar=10,Chapel=11,Cistern=12,Ossuary=13,Ravine=14,Cloister=15,Galleries=16,Forge=17,Threshold=18,Count=19;
  public static readonly int[] Route={Road,Town,Fields,Ravine,Cemetery,Crypt,Cloister,Crypt2,Crypt3,Galleries,Kings,Forge,Threshold,Heart};
  public static readonly string[] Names={"Oakhaven","Las Ostatnich Szeptów","Płaczące Katakumby • I","Trakt Wisielców","Martwe Błonia","Cmentarzysko Zapomnianych","Płaczące Katakumby • II","Płaczące Katakumby • III","Krypta Królów-Biskupów","Komnata Pękniętego Serca","Piwnica pod spaloną farmą","Kaplica Trzynastej Lampy","Cysterna Czarnej Wody","Ossuarium Bez Imion","Wąwóz Zgaszonych Sygnałów","Krużganek Skrępowanych","Galerie Dzwoniących Kości","Kuźnia Czarnego Serca","Próg Ostatniej Przysięgi"};
  public static readonly string[] Lore={
   "Dwanaście lamp. Trzynastu pielgrzymów. Żaden nie oddychał.",
   "Pod korzeniami Edra słyszy imiona tych, których las nie oddał.",
   "Woda spływa po kamieniu. Pod kratami płynie coś gęstszego.",
   "Ocalałeś z rozbitego wozu. Za mostem płoną ostatnie lampy Oakhaven.",
   "Na polach nie rośnie już zboże. Studnie oddychają purpurą.",
   "Cztery mauzolea. Trzy groby. Jedna droga pod katedrę.",
   "Żelazo pamięta krzyki. Uważaj na zapowiedzi pułapek.",
   "Fanatycy niosą własną krew do serca pod katedrą.",
   "Czarny marmur, złote czaszki. Kapłani podnoszą poległych strażników.",
   "Malazar przykował odłamek Varkosa do serca Oakhaven.",
   "Ktoś ukrył zapasy pod popiołem. Ktoś jeszcze ich pilnuje.",
   "Trzy lampy płoną zimnym ogniem. Ich światło prowadzi zmarłych do miasta.",
   "Woda pamięta każdą kroplę krwi. Zamknij dopływy, nim dotrze do studni Oakhaven.",
   "Biskupi zeskrobali imiona z kości. Kamień zachował to, czego bali się żywi.",
   "Trzy wieże zgasły. Bez ich sygnałów karawany błądzą ku krawędzi przepaści.",
   "Przysięgi stały się łańcuchami. Rozbij więzy, zanim cienie zapomną własne głosy.",
   "Każda otwarta krypta jest gardłem. Trzy gardła śpiewają imię Malazara.",
   "Młoty wykuwają dla serca nowe żebra. Wygaszaj paleniska, zanim zamkną się wokół odłamka.",
   "Serce karmi się trzema przysięgami. Zerwij ich pieczęcie, a biskup usłyszy twoje kroki."
  };
  public static bool Outdoors(int z){return z==Road || z==Fields || z==Forest || z==Cemetery || z==Ravine || z==Cloister;}
  public static bool Underground(int z){return z==Crypt || z==Crypt2 || z==Crypt3 || z==Kings || z==Heart || z==Cellar || z==Chapel || z==Cistern || z==Ossuary || z==Galleries || z==Forge || z==Threshold;}
  public static int Next(int z){if(z==Forest || z==Cellar)return Fields;if(z==Chapel)return Cemetery;if(z==Cistern)return Crypt2;if(z==Ossuary)return Kings;for(int i=0;i<Route.Length-1;i++)if(Route[i]==z)return Route[i+1];return -1;}
  public static bool SideBranch(int z){return z==Forest || z==Cellar || z==Chapel || z==Cistern || z==Ossuary;}
  public static bool EventStage(int z){return z>=Ravine && z<=Threshold;}
  public static int EncounterBudget(int z){return z==Town ? 0 : EventStage(z) ? 26 : z==Road ? 10 : z==Heart ? 9 : z==Cemetery ? 19 : z==Cellar || z==Chapel ? 16 : z==Cistern ? 18 : z==Forest || z==Ossuary ? 20 : 22;}
  public static int MigrateLegacyKills(int zone,int legacyMask)
  {
   // Version 0.5.1 inserted actors in a different order. Preserve their identities,
   // including the road boss and each cemetery wave; do not hide added actors.
   int[] generic={0,1,2,5,6,7,8,12,13,14,15,-1};int migrated=0;
   for(int old=0;old<30;old++)if((legacyMask&(1<<old))!=0)
   {
    int current=-1;
    if(zone==Road && old<5)current=new[]{0,2,4,5,9}[old];
    else if(zone==Cemetery && old<13)current=old<4 ? old : 4+((old-4)/3)*5+((old-4)%3)+1;
    else if(zone==Heart)current=old==0 ? 0 : old<=20 ? old+8 : -1;
    else if((zone==Forest || zone==Crypt || zone==Fields || zone==Crypt2 || zone==Crypt3 || zone==Kings || zone==Cellar) && old<12)
     current=old==11 ? EncounterBudget(zone)-1 : zone==Cellar && old==10 ? 11 : generic[old];
    if(current>=0 && current<30)migrated|=1<<current;
   }
   return migrated;
  }
 }
}
