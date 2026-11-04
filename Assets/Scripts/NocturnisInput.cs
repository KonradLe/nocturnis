using System;
using UnityEngine;
namespace Krypta {
 public static class NocturnisInput {
  public static bool PerformanceSkipFormat;
  public static readonly string[] Actions={"up","down","left","right","skill","defense","extra0","extra1","extra2","dodge","potion","interact","inventory","book","journal","town","mute","talents"};
  public static readonly string[] Labels={"Ruch naprzód","Ruch w tył","Ruch w lewo","Ruch w prawo","Moc główna (także PPM)","Ochrona (także 1)","Druga moc","Trzecia moc","Moc ostateczna","Unik","Mikstura","Interakcja","Ekwipunek","Księga mocy","Dziennik","Zwój / modlitwa powrotu","Wyciszenie","Drzewo talentów"};
  static readonly KeyCode[] Defaults={KeyCode.W,KeyCode.S,KeyCode.A,KeyCode.D,KeyCode.Q,KeyCode.R,KeyCode.Alpha2,KeyCode.Alpha3,KeyCode.Alpha4,KeyCode.Space,KeyCode.H,KeyCode.E,KeyCode.I,KeyCode.K,KeyCode.J,KeyCode.T,KeyCode.M,KeyCode.V};
  const int FormatCacheLimit=256,FormatCacheMaxLength=4096;
  struct CachedFormat {public int revision;public string text;}
  static readonly System.Collections.Generic.Dictionary<string,int> actionIndex=new System.Collections.Generic.Dictionary<string,int>(StringComparer.Ordinal);
  static readonly System.Collections.Generic.Dictionary<string,CachedFormat> formattedHints=new System.Collections.Generic.Dictionary<string,CachedFormat>(StringComparer.Ordinal);
  static readonly KeyCode[] cachedKeys=new KeyCode[Defaults.Length];
  static readonly string[] cachedNames=new string[Defaults.Length],legacyTokens=new string[Defaults.Length],tokens=new string[Defaults.Length],rawTokens=new string[Defaults.Length],bracketed=new string[Defaults.Length];
  static string loadedPrefix,interactionHint;
  static int bindingsRevision;
  static NocturnisInput(){
   for(int i=0;i<Actions.Length;i++){
    actionIndex.Add(Actions[i],i);tokens[i]="{KEY:"+Actions[i]+"}";rawTokens[i]="{KEYRAW:"+Actions[i]+"}";
    string old=Defaults[i].ToString().ToUpperInvariant();legacyTokens[i]=old.Length==1 ? "["+old+"]" : null;
   }
  }
  static void RefreshName(int i){
   var key=cachedKeys[i];cachedNames[i]=key==KeyCode.Space ? "SPACJA" : key>=KeyCode.Alpha0 && key<=KeyCode.Alpha9 ? ((int)key-(int)KeyCode.Alpha0).ToString() : key.ToString().ToUpperInvariant();
   cachedNames[i]=NocturnisLanguage.Text(cachedNames[i]);bracketed[i]="["+cachedNames[i]+"]";
  }
  static void BindingsChanged(){bindingsRevision++;formattedHints.Clear();interactionHint=cachedNames[actionIndex["interact"]]+" \u2022";}
  static void EnsureBindings(){
   string prefix=Prefix;if(loadedPrefix==prefix)return;
   for(int i=0;i<Actions.Length;i++){
    int saved=PlayerPrefs.GetInt(prefix+Actions[i],(int)Defaults[i]);
    cachedKeys[i]=Enum.IsDefined(typeof(KeyCode),saved) ? (KeyCode)saved : Defaults[i];
   }
   // Left Shift is the fixed stationary-attack modifier. Repair older saved
   // bindings without assigning a second action to an already occupied key.
   bool repaired=false;
   int talents=actionIndex["talents"];
   if(!PlayerPrefs.HasKey(prefix+"talents")){
    bool conflict=false;for(int i=0;i<Actions.Length;i++)if(i!=talents && cachedKeys[i]==cachedKeys[talents])conflict=true;
    if(conflict)foreach(var candidate in new[]{KeyCode.V,KeyCode.B,KeyCode.N,KeyCode.U,KeyCode.F6,KeyCode.F7,KeyCode.F8})if(Array.IndexOf(cachedKeys,candidate)<0){cachedKeys[talents]=candidate;PlayerPrefs.SetInt(prefix+"talents",(int)candidate);repaired=true;break;}
   }
   for(int i=0;i<Actions.Length;i++)if(cachedKeys[i]==KeyCode.LeftShift){
    KeyCode replacement=Defaults[i];
    if(Array.IndexOf(cachedKeys,replacement)>=0)foreach(KeyCode candidate in Defaults)if(Array.IndexOf(cachedKeys,candidate)<0){replacement=candidate;break;}
    cachedKeys[i]=replacement;PlayerPrefs.SetInt(prefix+Actions[i],(int)replacement);repaired=true;
   }
   for(int i=0;i<Actions.Length;i++)RefreshName(i);
   if(repaired)PlayerPrefs.Save();loadedPrefix=prefix;BindingsChanged();
  }
  // Explicit reload is required after external PlayerPrefs edits (QA/fixtures).
  // Bind/Reset invalidate automatically; changing save profile does not split controls.
  public static void ReloadBindings(){loadedPrefix=null;EnsureBindings();}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void ResetBindingCache(){loadedPrefix=null;formattedHints.Clear();}

  static string Prefix {get{return KryptaGame.Testing ? "Nocturnis.QA.Key." : "Nocturnis.Key.";}}
  public static KeyCode Key(string action){int i;if(action==null || !actionIndex.TryGetValue(action,out i))return KeyCode.None;EnsureBindings();return cachedKeys[i];}
  public static bool Down(string action){return Input.GetKeyDown(Key(action));}
  public static bool Held(string action){return Input.GetKey(Key(action));}
  public static string Text(string action){int i;if(action==null || !actionIndex.TryGetValue(action,out i))return "NONE";EnsureBindings();return cachedNames[i];}
  public static bool Bind(string action,KeyCode key){
   int index;if(action==null || !actionIndex.TryGetValue(action,out index) || key==KeyCode.None || key==KeyCode.LeftShift || key==KeyCode.Escape || key==KeyCode.Return || key==KeyCode.Alpha1 || key>=KeyCode.Mouse0 || key>=KeyCode.UpArrow && key<=KeyCode.LeftArrow || !Enum.IsDefined(typeof(KeyCode),key))return false;
   EnsureBindings();KeyCode previous=cachedKeys[index];
   for(int i=0;i<Actions.Length;i++)if(i!=index && cachedKeys[i]==key){cachedKeys[i]=previous;RefreshName(i);PlayerPrefs.SetInt(Prefix+Actions[i],(int)previous);}
   cachedKeys[index]=key;RefreshName(index);PlayerPrefs.SetInt(Prefix+action,(int)key);BindingsChanged();PlayerPrefs.Save();return true;
  }
  public static void Reset(){foreach(string action in Actions)PlayerPrefs.DeleteKey(Prefix+action);PlayerPrefs.Save();ReloadBindings();}
  public static string Format(string value){
   value=NocturnisLanguage.Text(value);
   if(PerformanceSkipFormat && NocturnisPerformanceProbe.Active)return value;
   if(string.IsNullOrEmpty(value) || (value.IndexOf('[')<0 && value.IndexOf("{KEY:",StringComparison.Ordinal)<0 && value.IndexOf("{KEYRAW:",StringComparison.Ordinal)<0 && value.IndexOf("E \u2022",StringComparison.Ordinal)<0))return value;
   EnsureBindings();CachedFormat cached;
   if(formattedHints.TryGetValue(value,out cached) && cached.revision==bindingsRevision)return cached.text;
   string original=value;
   // Two passes preserve swaps: [Q] -> token -> [E] must not become [Q] again.
   for(int i=0;i<Actions.Length;i++)if(legacyTokens[i]!=null && value.IndexOf(legacyTokens[i],StringComparison.Ordinal)>=0)value=value.Replace(legacyTokens[i],tokens[i]);
   for(int i=0;i<Actions.Length;i++)if(value.IndexOf(tokens[i],StringComparison.Ordinal)>=0)value=value.Replace(tokens[i],bracketed[i]);
   if(value.IndexOf("E \u2022",StringComparison.Ordinal)>=0)value=value.Replace("E \u2022",interactionHint);
   // Raw HUD captions stay compact and cannot cascade into the legacy interaction hint.
   for(int i=0;i<Actions.Length;i++)if(value.IndexOf(rawTokens[i],StringComparison.Ordinal)>=0)value=value.Replace(rawTokens[i],cachedNames[i]);
   if(original.Length<=FormatCacheMaxLength){if(formattedHints.Count>=FormatCacheLimit)formattedHints.Clear();formattedHints[original]=new CachedFormat{revision=bindingsRevision,text=value};}
   return value;
  }
 }
 public sealed partial class KryptaGame {
  int tutorialFlags,tutorialZone=-1;Vector3 tutorialPosition;float tutorialDistance;bool tutorialLoaded;
  string TutorialKey {get{return (Testing ? "Nocturnis.QA.Tutorial." : "Nocturnis.Tutorial.")+ActiveProfile;}}
  public bool TutorialEnabled {get{return PlayerPrefs.GetInt("Nocturnis.TutorialEnabled",1)==1;}}
  public int TutorialStep {get{for(int i=0;i<6;i++)if((tutorialFlags&(1<<i))==0)return i;return 6;}}
  public void SkipTutorial(){tutorialFlags=63;PlayerPrefs.SetInt(TutorialKey,tutorialFlags);PlayerPrefs.Save();}
  public void RestartTutorial(){tutorialFlags=0;tutorialDistance=0;tutorialLoaded=true;PlayerPrefs.SetInt(TutorialKey,0);PlayerPrefs.SetInt("Nocturnis.TutorialEnabled",1);}
  void UpdateQualityOfLife(){
   if(!tutorialLoaded){tutorialFlags=PlayerPrefs.GetInt(TutorialKey,0);tutorialLoaded=true;}
   if(tutorialZone==Zone){float d=Vector3.Distance(HeroPosition,tutorialPosition);if(d<2)tutorialDistance+=d;}
   tutorialZone=Zone;tutorialPosition=HeroPosition;int old=tutorialFlags;
   if(tutorialDistance>8)tutorialFlags|=1;if(Defeated>0)tutorialFlags|=2;if(PulseReady>0)tutorialFlags|=4;if(DashReady>0 || DefenseReady>0)tutorialFlags|=8;if(Zone!=Campaign.Road)tutorialFlags|=16;if(QuestAccepted)tutorialFlags|=32;
   if(old!=tutorialFlags)PlayerPrefs.SetInt(TutorialKey,tutorialFlags);
   UpdateSideMechanics(Time.deltaTime);RevealMap();
  }
 }
 public sealed partial class KryptaHUD {
  Vector2 bindingsScroll;int rebinding=-1;bool accessibilityPage,suppressRebindEscape;
  void TutorialPanel(){
   if(game.State!=KryptaGame.Phase.Playing || !game.TutorialEnabled || game.TutorialStep>=6 || game.IntroActive || KryptaGame.Testing)return;
   string[] copy={"Ruszaj traktem. Użyj "+NocturnisInput.Text("up")+" / "+NocturnisInput.Text("left")+" / "+NocturnisInput.Text("down")+" / "+NocturnisInput.Text("right")+" albo kliknij drogę.","Kliknij przeciwnika, aby podejść i uderzyć. Shift + LPM pozwala atakować bez ruchu.","Wyceluj i użyj "+NocturnisInput.Text("skill")+" lub PPM. Zasób i czas odnowienia widać na pasie mocy.","Unik: "+NocturnisInput.Text("dodge")+". Ochrona: "+NocturnisInput.Text("defense")+". Reaguj na zapowiedzi ciosów; "+NocturnisInput.Text("potion")+" leczy miksturą.","Pokonaj Nadzorcę. Podejdź do pieczęci i wciśnij "+NocturnisInput.Text("interact")+", aby przejść do Oakhaven.","Porozmawiaj z mieszkańcami. Dziennik: "+NocturnisInput.Text("journal")+". Relikty: "+NocturnisInput.Text("inventory")+". Księga mocy: "+NocturnisInput.Text("book")+"."};
   Frame(new Rect(20,358,330,158));Label(35,369,300,24,"PIERWSZA WYPRAWA  •  "+(game.TutorialStep+1)+" / 6",small,gold);Label(35,397,300,76,copy[game.TutorialStep],text,paper);
   if(Button(new Rect(35,480,300,25),"ZNAM PODSTAWY • POMIŃ"))game.SkipTutorial();
  }
  void KeyBindingsMenu(){
   Label(282,179,710,36,"Kliknij klawisz, potem naciśnij nowy. Zajęte klawisze zamieniają się miejscami.",small,paper);
   bindingsScroll=GUI.BeginScrollView(new Rect(280,221,720,306),bindingsScroll,new Rect(0,0,695,NocturnisInput.Actions.Length*36));
   for(int i=0;i<NocturnisInput.Actions.Length;i++){float y=i*36;Label(12,y+4,440,28,NocturnisInput.Labels[i],menuText,paper);if(Button(new Rect(466,y,212,30),rebinding==i ? "NACIŚNIJ KLAWISZ…" : NocturnisInput.Text(NocturnisInput.Actions[i])))rebinding=i;}
   GUI.EndScrollView();
   if(rebinding>=0 && Event.current.type==EventType.KeyDown){if(Event.current.keyCode==KeyCode.Escape){rebinding=-1;suppressRebindEscape=true;}else if(NocturnisInput.Bind(NocturnisInput.Actions[rebinding],Event.current.keyCode))rebinding=-1;Event.current.Use();}
   if(Button(new Rect(282,539,713,33),"PRZYWRÓĆ DOMYŚLNE STEROWANIE")){NocturnisInput.Reset();rebinding=-1;}
  }
  void AccessibilitySettings(){
   Label(283,232,700,40,"CZYTELNOŚĆ I WPROWADZENIE",menuHeading,gold);
   bool large=PlayerPrefs.GetInt("Nocturnis.LargeText",0)==1;
   Label(283,297,480,40,"Większy tekst rozmów, zadań i opisów",menuText,paper);
   if(Button(new Rect(792,292,205,36),large ? "WIĘKSZY" : "STANDARDOWY")){PlayerPrefs.SetInt("Nocturnis.LargeText",large ? 0 : 1);PlayerPrefs.Save();}
   bool shake=PlayerPrefs.GetInt("Nocturnis.CameraShake",1)==1;
   Label(283,362,480,40,"Wstrząsy kamery podczas trafień",menuText,paper);
   if(Button(new Rect(792,357,205,36),shake ? "WŁĄCZONE" : "WYŁĄCZONE")){PlayerPrefs.SetInt("Nocturnis.CameraShake",shake ? 0 : 1);PlayerPrefs.Save();}
   if(Button(new Rect(283,426,714,42),"POWTÓRZ WPROWADZENIE DO GRY")){game.RestartTutorial();menuNotice="Wskazówki pojawią się po wznowieniu wyprawy.";}
   if(Button(new Rect(283,480,714,34),PlayerPrefs.GetInt("Nocturnis.MapExploration",1)==1 ? "MAPA: ODKRYWANA PODCZAS WĘDRÓWKI" : "MAPA: CAŁY TEREN")){PlayerPrefs.SetInt("Nocturnis.MapExploration",1-PlayerPrefs.GetInt("Nocturnis.MapExploration",1));PlayerPrefs.Save();}
   Label(283,520,714,52,menuNotice.Length>0 ? menuNotice : "Wskazówki kończą się po wykonaniu czynności. Możesz je pominąć w dowolnej chwili.",menuText,paper);
  }
 }
}
