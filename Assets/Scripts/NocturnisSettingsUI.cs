using System.Collections.Generic;
using UnityEngine;

namespace Krypta
{
 public sealed partial class KryptaHUD
 {
  int settingsTab,settingsResolution;
  int observedDisplayWidth,observedDisplayHeight;
  FullScreenMode settingsDisplayMode,observedDisplayMode;
  List<Vector2Int> settingsResolutions;
  static readonly string[] SettingsTabs={"DŹWIĘK","OBRAZ","DOSTĘPNOŚĆ"};
  static readonly string[] SettingsPresets={"NISKIE","ŚREDNIE","WYSOKIE"};
  void DrawExpandedSettings()
  {
   var preferences=NocturnisSettings.Ensure(game.gameObject);
   bool previous=GUI.enabled;GUI.enabled=previous && !preferences.DisplayPending;
   for(int i=0;i<SettingsTabs.Length;i++)
    if(Button(new Rect(282+i*241,179,234,37),SettingsTabs[i],settingsTab==i)){settingsTab=i;menuNotice="";}
   accessibilityPage=settingsTab==2;
   if(settingsTab==0)ExpandedAudioSettings();
   else if(settingsTab==1)ExpandedGraphicsSettings(preferences);
   else ExpandedAccessibilitySettings();
   GUI.enabled=previous;
   if(preferences.DisplayPending)DisplayPreviewConfirmation(preferences);
  }
  void ExpandedAudioSettings()
  {
   Label(282,228,480,27,"DŹWIĘK VAELORII",menuText,gold);
   if(Button(new Rect(792,223,205,30),game.Muted ? "WYCISZENIE: TAK" : "WYCISZENIE: NIE"))game.ToggleAudio();
   float effects=VolumeSlider(263,"Efekty i interfejs",game.EffectsVolume);
   float music=VolumeSlider(323,"Muzyka",game.MusicVolume);
   float ambient=VolumeSlider(383,"Dźwięki otoczenia",game.AmbientVolume);
   float voice=VolumeSlider(443,"Film i lektor",NocturnisSettings.VoiceVolume);
   if(!Mathf.Approximately(music,game.MusicVolume))game.SetMusicVolume(music);
   if(!Mathf.Approximately(effects,game.EffectsVolume) || !Mathf.Approximately(ambient,game.AmbientVolume))game.SetAudioVolumes(effects,ambient);
   if(!Mathf.Approximately(voice,NocturnisSettings.VoiceVolume))NocturnisSettings.SetVoiceVolume(voice);
   Label(283,517,714,49,"Muzyka reaguje na eksplorację, walkę i starcia z bossami.\nZmiany głośności zapisują się automatycznie.",center,muted);
  }
  void ExpandedGraphicsSettings(NocturnisSettings preferences)
  {
   Label(282,233,260,29,"Jakość grafiki",menuText,paper);
   for(int i=0;i<3;i++)if(Button(new Rect(577+i*142,228,136,34),SettingsPresets[i],(int)preferences.Preset==i))
    preferences.SetPreset((NocturnisSettings.GraphicsPreset)i);
   string description=preferences.Preset==NocturnisSettings.GraphicsPreset.Low ?
    "Większa płynność: bez cieni, mniejsze tekstury, bez wygładzania krawędzi." :
    preferences.Preset==NocturnisSettings.GraphicsPreset.Medium ?
    "Równowaga: cienie, połowa rozdzielczości tekstur i wygładzanie 2×." :
    "Pełna rozdzielczość tekstur, miękkie cienie i wygładzanie 4×.";
   Label(282,273,715,39,description,menuSmall,muted);
   Label(282,321,450,29,"Synchronizacja pionowa",menuText,paper);
   if(Button(new Rect(792,316,205,34),preferences.VSync ? "WŁĄCZONA" : "WYŁĄCZONA"))preferences.SetVSync(!preferences.VSync);
   Label(282,361,450,29,"Limit klatek na sekundę",menuText,paper);
   bool previous=GUI.enabled;GUI.enabled=previous && !preferences.VSync && !NocturnisSettings.ServerPreview;
   if(Button(new Rect(792,356,205,34),preferences.FrameCap<0 ? "BEZ LIMITU" : preferences.FrameCap+" FPS"))
   {int at=System.Array.IndexOf(NocturnisSettings.FrameCaps,preferences.FrameCap);preferences.SetFrameCap(NocturnisSettings.FrameCaps[(at+1)%NocturnisSettings.FrameCaps.Length]);}
   GUI.enabled=previous;
   if(preferences.VSync)Label(282,392,715,21,"Synchronizacja dopasowuje liczbę klatek do odświeżania monitora.",menuSmall,muted);
   RefreshDisplayChoices();
   Label(282,423,450,29,"Tryb wyświetlania",menuText,paper);
   GUI.enabled=previous && !NocturnisSettings.DisplayLocked;
   if(Button(new Rect(731,418,266,34),DisplayModeLabel(settingsDisplayMode)))
    settingsDisplayMode=settingsDisplayMode==FullScreenMode.Windowed ? FullScreenMode.FullScreenWindow :
     settingsDisplayMode==FullScreenMode.FullScreenWindow ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
   Label(282,463,390,29,"Rozdzielczość",menuText,paper);
   GUI.enabled=previous && !NocturnisSettings.DisplayLocked && settingsDisplayMode!=FullScreenMode.FullScreenWindow;
   if(Button(new Rect(731,458,45,34),"‹"))settingsResolution=(settingsResolution+settingsResolutions.Count-1)%settingsResolutions.Count;
   if(Button(new Rect(952,458,45,34),"›"))settingsResolution=(settingsResolution+1)%settingsResolutions.Count;
   Vector2Int resolution=settingsResolutions[settingsResolution];
   string size=settingsDisplayMode==FullScreenMode.FullScreenWindow ? Screen.currentResolution.width+" × "+Screen.currentResolution.height : resolution.x+" × "+resolution.y;
   Label(780,458,168,34,size,center,gold);
   GUI.enabled=previous && !NocturnisSettings.DisplayLocked;
   bool changed=settingsDisplayMode!=Screen.fullScreenMode ||
    (settingsDisplayMode!=FullScreenMode.FullScreenWindow && (resolution.x!=Screen.width || resolution.y!=Screen.height));
   GUI.enabled=GUI.enabled && changed;
   if(Button(new Rect(282,508,715,34),"ZASTOSUJ TRYB EKRANU",true))preferences.BeginDisplayPreview(resolution.x,resolution.y,settingsDisplayMode);
   GUI.enabled=previous;
   string note=NocturnisSettings.DisplayLocked ? "Rozmiar okna został ustalony przez uruchomienie gry lub podgląd." :
    preferences.DisplayNotice.Length>0 ? preferences.DisplayNotice : "Nowy tryb ekranu wymaga potwierdzenia w ciągu 15 sekund.";
   Label(282,546,715,30,note,center,muted);
  }
  void RefreshDisplayChoices()
  {
   if(settingsResolutions!=null && observedDisplayWidth==Screen.width &&
      observedDisplayHeight==Screen.height && observedDisplayMode==Screen.fullScreenMode)return;
   settingsResolutions=NocturnisSettings.AvailableResolutions();
   if(settingsResolutions.Count==0)settingsResolutions.Add(new Vector2Int(1280,720));
   observedDisplayWidth=Screen.width;observedDisplayHeight=Screen.height;observedDisplayMode=Screen.fullScreenMode;
   settingsDisplayMode=NocturnisSettings.NormalizeDisplayMode((int)Screen.fullScreenMode);
   settingsResolution=Mathf.Max(0,settingsResolutions.IndexOf(new Vector2Int(Screen.width,Screen.height)));
  }
  static string DisplayModeLabel(FullScreenMode mode)
  {return mode==FullScreenMode.Windowed ? "OKNO" : mode==FullScreenMode.FullScreenWindow ? "PEŁNY EKRAN BEZ RAMEK" : "PEŁNY EKRAN";}
  void DisplayPreviewConfirmation(NocturnisSettings preferences)
  {
   Fill(new Rect(0,0,1280,720),new Color(.009f,.006f,.004f,.92f));Frame(new Rect(324,206,632,302));
   Label(350,230,580,43,"ZACHOWAĆ TEN OBRAZ?",menuCentered,warmWhite);Divider(363,286,554);
   Label(369,311,542,74,"Jeżeli obraz jest poprawny, potwierdź zmianę.\nPowrót do poprzedniego trybu za "+Mathf.CeilToInt(preferences.DisplaySecondsRemaining)+" s.",menuText,paper);
   if(Button(new Rect(367,425,260,42),"COFNIJ  [ESC]"))preferences.RevertDisplay();
   if(Button(new Rect(651,425,260,42),"ZACHOWAJ",true))preferences.ConfirmDisplay();
  }
  void ExpandedAccessibilitySettings()
  {
   bool large=PlayerPrefs.GetInt("Nocturnis.LargeText",0)==1;
   Label(282,238,480,30,"Większy tekst rozmów i zadań",menuText,paper);
   if(Button(new Rect(792,233,205,34),large ? "WIĘKSZY" : "STANDARDOWY"))SetAccessibilityPreference("Nocturnis.LargeText",!large);
   bool shake=PlayerPrefs.GetInt("Nocturnis.CameraShake",1)==1;
   Label(282,284,480,30,"Wstrząsy kamery",menuText,paper);
   if(Button(new Rect(792,279,205,34),shake ? "WŁĄCZONE" : "WYŁĄCZONE"))SetAccessibilityPreference("Nocturnis.CameraShake",!shake);
   Label(282,330,480,30,"Liczby obrażeń i leczenia",menuText,paper);
   if(Button(new Rect(792,325,205,34),NocturnisSettings.DamageNumbersEnabled ? "WŁĄCZONE" : "WYŁĄCZONE"))NocturnisSettings.SetDamageNumbers(!NocturnisSettings.DamageNumbersEnabled);
   Label(282,376,480,30,"Łagodniejsze błyski zaklęć",menuText,paper);
   if(Button(new Rect(792,371,205,34),game.ReducedFlashes ? "WŁĄCZONE" : "WYŁĄCZONE"))
   {game.ReducedFlashes=!game.ReducedFlashes;SetAccessibilityPreference("Nocturnis.ReducedFlashes",game.ReducedFlashes);}
   bool exploration=PlayerPrefs.GetInt("Nocturnis.MapExploration",1)==1;
   Label(282,422,480,30,"Odkrywanie mapy",menuText,paper);
   if(Button(new Rect(792,417,205,34),exploration ? "PODCZAS WĘDRÓWKI" : "CAŁY TEREN"))SetAccessibilityPreference("Nocturnis.MapExploration",!exploration);
   if(Button(new Rect(282,471,715,37),"POWTÓRZ WPROWADZENIE DO GRY"))
   {game.RestartTutorial();menuNotice="Wskazówki pojawią się po wznowieniu wyprawy.";}
   Label(282,523,715,45,menuNotice.Length>0 ? menuNotice : "Dopasuj czytelność i intensywność efektów.\nZmiany zapisują się automatycznie.",center,muted);
  }
  static void SetAccessibilityPreference(string key,bool enabled)
  {PlayerPrefs.SetInt(key,enabled ? 1 : 0);PlayerPrefs.Save();}
 }
}
