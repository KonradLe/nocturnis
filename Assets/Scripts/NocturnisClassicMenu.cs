using UnityEngine;

namespace Krypta
{
 public sealed partial class KryptaHUD
 {
  enum MenuPage { Main, Characters, Settings, Controls, Credits }
  enum MenuConfirmation { None, NewJourney, MainMenu, Quit }
  MenuPage menuPage,returnPage;
  MenuConfirmation menuConfirmation;
  Texture2D classicPanel;
  GUIStyle menuHeading,menuTitle,menuText,menuSmall,menuCentered,sliderTrack,sliderThumb;
  Vector2 creditsScroll;
  TextAsset creditsText;
  string menuNotice="";
  bool resetMenuFocus;
  readonly Color bronze=new Color(.54f,.39f,.22f),warmWhite=new Color(.94f,.88f,.74f);

  void MenuStyles()
  {
   if(menuHeading!=null)return;
   classicPanel=Resources.Load<Texture2D>("UI/ClassicPanel");
   creditsText=Resources.Load<TextAsset>("Credits");
   menuHeading=new GUIStyle(heading){fontSize=26};
   menuTitle=new GUIStyle(title){fontSize=67};
   menuText=new GUIStyle(text){fontSize=16};
   menuSmall=new GUIStyle(small){fontSize=13};
   menuCentered=new GUIStyle(menuHeading){alignment=TextAnchor.MiddleCenter};
   sliderTrack=new GUIStyle{fixedHeight=26,padding=new RectOffset(0,0,0,0)};
   sliderThumb=new GUIStyle{fixedWidth=20,fixedHeight=26};
  }

  // Nine-slicing preserves the artwork's fine corners at every window size.
  void ClassicFrame(Rect r,float corner=25)
  {
   MenuStyles();
   if(!classicPanel){Fill(r,ink);Outline(r,bronze);return;}
   float b=Mathf.Min(corner,Mathf.Min(r.width,r.height)*.45f);
   const float cut=.15f;
   for(int row=0;row<3;row++)for(int col=0;col<3;col++)
   {
    float x=col==0 ? r.x : col==1 ? r.x+b : r.xMax-b;
    float y=row==0 ? r.y : row==1 ? r.y+b : r.yMax-b;
    float w=col==1 ? r.width-2*b : b,h=row==1 ? r.height-2*b : b;
    float u=col==0 ? 0 : col==1 ? cut : 1-cut;
    float v=row==0 ? 1-cut : row==1 ? cut : 0;
    GUI.DrawTextureWithTexCoords(new Rect(x,y,w,h),classicPanel,new Rect(u,v,col==1 ? 1-2*cut : cut,row==1 ? 1-2*cut : cut),true);
   }
  }
  void Outline(Rect r,Color c)
  {
   Fill(new Rect(r.x,r.y,r.width,1),c);Fill(new Rect(r.x,r.yMax-1,r.width,1),c);
   Fill(new Rect(r.x,r.y,1,r.height),c);Fill(new Rect(r.xMax-1,r.y,1,r.height),c);
  }
  void Diamond(float x,float y,float size,Color color)
  {
   Line(new Vector2(x,y-size),new Vector2(x+size,y),1,color);
   Line(new Vector2(x+size,y),new Vector2(x,y+size),1,color);
   Line(new Vector2(x,y+size),new Vector2(x-size,y),1,color);
   Line(new Vector2(x-size,y),new Vector2(x,y-size),1,color);
  }
  void Divider(float x,float y,float width)
  {
   Fill(new Rect(x,y,width*.5f-12,1),bronze);Fill(new Rect(x+width*.5f+12,y,width*.5f-12,1),bronze);
   Diamond(x+width*.5f,y,5,gold);
  }
  void Navigate(MenuPage destination)
  {CancelPortraitDrag();menuPage=destination;menuNotice="";creditsScroll=Vector2.zero;resetMenuFocus=true;}
  void OpenMenuPage(MenuPage destination)
  {returnPage=game.State==KryptaGame.Phase.Title ? menuPage : MenuPage.Main;Navigate(destination);}
  void BackFromMenu()
  {
   var preferences=GetComponent<NocturnisSettings>();
   if(menuPage==MenuPage.Settings && preferences && preferences.DisplayPending){preferences.RevertDisplay();return;}
   if(menuConfirmation!=MenuConfirmation.None){menuConfirmation=MenuConfirmation.None;return;}
   Navigate(menuPage==MenuPage.Characters ? MenuPage.Main : returnPage);
   PlayerPrefs.Save();
  }
  // Called before gameplay input, so Escape never resumes combat behind settings.
  public bool HandleMenuInput()
  {
   if(!game.TalentsOpen)talentRespecConfirm=false;
   if(game.Service!="market")marketSellConfirmation=false;
   if((talentRespecConfirm || marketSellConfirmation) && Input.GetKeyDown(KeyCode.Escape)){talentRespecConfirm=marketSellConfirmation=false;return true;}
   bool titleOpen=game.State==KryptaGame.Phase.Title;
   bool subpage=menuPage==MenuPage.Settings || menuPage==MenuPage.Controls || menuPage==MenuPage.Credits;
   if(!titleOpen && !(game.State==KryptaGame.Phase.Paused && (subpage || menuConfirmation!=MenuConfirmation.None)))return false;
   // IMGUI can consume Escape before Update sees that same native key press.
   // Keep cancellation separate from menu navigation until the key is released.
   if(suppressRebindEscape)
   {
    if(!Input.GetKey(KeyCode.Escape) && !Input.GetKeyDown(KeyCode.Escape))suppressRebindEscape=false;
    return true;
   }
   if(gateAt>=0 || rebinding>=0)return true;
   if(Input.GetKeyDown(KeyCode.Escape))
   {
    if(menuConfirmation!=MenuConfirmation.None || menuPage!=MenuPage.Main)BackFromMenu();
    else if(!Application.isEditor)menuConfirmation=MenuConfirmation.Quit;
   }
   return true;
  }
  void ClassicTitleScreen()
  {
   MenuStyles();
   if(resetMenuFocus){GUI.FocusControl(null);resetMenuFocus=false;}
   Fill(new Rect(-offsetX/scale,-offsetY/scale,Screen.width/scale,Screen.height/scale),Color.black);
   if(menuArt)GUI.DrawTexture(new Rect(0,0,1280,720),menuArt,ScaleMode.ScaleAndCrop);
   Fill(new Rect(0,0,1280,720),new Color(.02f,.015f,.012f,menuPage==MenuPage.Main ? .12f : .52f));
   for(int i=0;i<18;i++)
   {
    float x=460+(i*137)%760,y=700-Mathf.Repeat(i*43+Time.unscaledTime*(game.ReducedFlashes ? 3 : 9),550);
    Circle(x,y,1+(i%3)*.45f,new Color(.82f,.56f,.25f,.1f+(i%4)*.05f));
   }
   bool enabled=GUI.enabled;GUI.enabled=enabled && gateAt<0 && menuConfirmation==MenuConfirmation.None;
   if(menuPage==MenuPage.Main)MainMenu();
   else if(menuPage==MenuPage.Characters)CharacterMenu();
   else AuxiliaryMenu();
   GUI.enabled=enabled;
   if(menuConfirmation!=MenuConfirmation.None)ConfirmationMenu();
   Gates();
  }
  void MainMenu()
  {
   Label(58,54,760,92,"NOCTURNIS",menuTitle,warmWhite);
   Label(68,145,700,32,"Ż N I W O   U P A D Ł Y C H   G W I A Z D",menuText,gold);
   Divider(68,197,535);
   Frame(new Rect(52,226,375,420));
   bool enabled=GUI.enabled;GUI.enabled=enabled && KryptaGame.HasCheckpoint;
   if(Button(new Rect(81,255,317,46),"KONTYNUUJ WYPRAWĘ",true))
   {if(game.ContinueCheckpoint())Navigate(MenuPage.Main);else menuNotice="Nie udało się odczytać zapisu.";}
   GUI.enabled=enabled;
   if(Button(new Rect(81,309,317,46),"NOWA WYPRAWA"))Navigate(MenuPage.Characters);
   if(Button(new Rect(81,363,317,46),"USTAWIENIA"))OpenMenuPage(MenuPage.Settings);
   if(Button(new Rect(81,417,317,46),"STEROWANIE"))OpenMenuPage(MenuPage.Controls);
   if(Button(new Rect(81,471,317,46),"AUTORZY I LICENCJE"))OpenMenuPage(MenuPage.Credits);
   GUI.enabled=enabled && !Application.isEditor;
   if(Button(new Rect(81,525,317,46),"ZAKOŃCZ GRĘ"))menuConfirmation=MenuConfirmation.Quit;
   GUI.enabled=enabled;
   Label(85,594,309,26,"AKT I  /  SERCE POD KATEDRĄ",center,gold);
   Frame(new Rect(817,497,410,149));
   for(int profile=1;profile<=3;profile++)if(Button(new Rect(824+(profile-1)*135,443,130,34),"PROFIL "+profile,game.ActiveProfile==profile)){if(game.SelectProfile(profile))game.RefreshTutorialProfile();}
   Label(843,519,355,24,KryptaGame.HasCheckpoint ? "TWOJA OSTATNIA WYPRAWA" : "KRONIKA JESZCZE NIE ZOSTAŁA ZAPISANA",menuSmall,gold);
   Label(843,553,350,70,KryptaGame.HasCheckpoint ? game.SavedChronicle : "Pięcioro pielgrzymów. Jedno bijące serce.\nWybierz bohatera i wyrusz ku Oakhaven.",menuText,paper);
   Label(66,674,700,24,string.IsNullOrEmpty(menuNotice) ? "VAELORIA  •  TRZY STULECIA PO GWIAZDOSPADZIE" : menuNotice,menuSmall,muted);
   Label(890,674,328,24,"NOCTURNIS  /  "+Application.version,center,muted);
  }
  void CharacterMenu()
  {
   Label(48,34,1100,48,"KRONIKA PIELGRZYMÓW",heading,warmWhite);
   Label(51,88,1070,28,"Wybierz tego, kto poniesie ostatni żar upadłych gwiazd.",menuText,paper);
   Divider(48,135,1184);
   Frame(new Rect(40,160,305,478));
   for(int i=0;i<5;i++)
   {
    var hero=NocturnisData.Heroes[i];Rect r=new Rect(58,183+i*86,269,73);
    bool selected=game.SelectedHero==i,hover=GUI.enabled && r.Contains(Event.current.mousePosition);
    ClassicFrame(r,13);
    if(selected || hover)Fill(new Rect(r.x+4,r.y+4,r.width-8,r.height-8),new Color(.43f,.3f,.15f,selected ? .3f : .15f));
    Diamond(r.x+17,r.center.y,5,selected ? hero.color : bronze);
    Label(r.x+33,r.y+13,228,25,hero.person,menuText,selected ? warmWhite : paper);
    Label(r.x+33,r.y+42,228,21,hero.name,small,hero.color);
    if(GUI.Button(r,GUIContent.none,GUIStyle.none)){game.SelectHero(i);game.UISound("chain");}
   }
   DrawRotatablePortrait(NocturnisPortraitOrbit.MenuBounds);
   Divider(400,582,430);
   Label(365,591,500,35,game.Hero.person,menuCentered,warmWhite);
   Label(365,628,500,18,"Przytrzymaj LPM i przeciągnij, aby obrócić postać",center,muted);
   Frame(new Rect(887,160,353,478));
   Label(917,189,291,65,game.Hero.name,menuHeading,warmWhite);
   Label(917,262,291,44,game.Hero.role,menuSmall,game.Hero.color);
   Divider(917,320,290);
   Label(917,340,291,107,game.Hero.story,menuText,paper);
   Label(917,463,291,25,"ŻYCIE  "+game.Hero.health+"     •     "+game.Hero.resource.ToUpper(),menuSmall,gold);
   Label(917,505,291,23,"SZEŚĆ MOCY  /  DWIE ŚCIEŻKI RUN",small,muted);
   for(int i=0;i<6;i++){Rect slot=new Rect(918+i*47,548,40,40);SkillIcon(slot,i);Outline(slot,bronze);QueueStoryTooltip(slot,i,true);}
   if(Button(new Rect(42,655,278,39),"WRÓĆ DO MENU"))Navigate(MenuPage.Main);
   if(Button(new Rect(422,650,430,49),"ROZPOCZNIJ WYPRAWĘ",true))
   {if(KryptaGame.HasCheckpoint)menuConfirmation=MenuConfirmation.NewJourney;else EnterWorld();}
   Label(915,661,300,25,"NOWA HISTORIA  •  POZIOM 1",center,muted);
  }
  bool ClassicPauseMenu()
  {
   if(game.State!=KryptaGame.Phase.Paused || game.EquipmentOpen || game.BookOpen || game.JournalOpen || game.TalentsOpen || !string.IsNullOrEmpty(game.Service))return false;
   MenuStyles();Fill(new Rect(0,0,1280,720),new Color(.012f,.009f,.007f,.79f));
   if(resetMenuFocus){GUI.FocusControl(null);resetMenuFocus=false;}
   bool enabled=GUI.enabled;GUI.enabled=enabled && menuConfirmation==MenuConfirmation.None;
   if(menuPage==MenuPage.Settings || menuPage==MenuPage.Controls || menuPage==MenuPage.Credits)AuxiliaryMenu();
   else
   {
    Frame(new Rect(402,77,476,570));
    Label(427,112,426,46,"CHWILA CISZY",menuCentered,warmWhite);
    Label(427,168,426,25,game.Hero.person+"  •  POZIOM "+game.Level,center,gold);
    Divider(445,208,390);
    if(Button(new Rect(445,235,390,43),"WRÓĆ DO WYPRAWY  [ESC]",true))game.Resume();
    if(Button(new Rect(445,292,187,38),"RELIKWIARZ  [I]"))game.OpenEquipment();
    if(Button(new Rect(648,292,187,38),"KSIĘGA MOCY  [K]"))game.OpenBook();
    if(Button(new Rect(445,344,187,38),"DZIENNIK  [J]"))game.OpenJournal();
    if(Button(new Rect(648,344,187,38),"TALENTY  [V]"))game.OpenTalents();
    if(Button(new Rect(445,396,390,38),"USTAWIENIA"))OpenMenuPage(MenuPage.Settings);
    if(Button(new Rect(445,448,390,38),"STEROWANIE"))OpenMenuPage(MenuPage.Controls);
    if(Button(new Rect(445,514,390,43),"ZAPISZ I WRÓĆ DO MENU"))menuConfirmation=MenuConfirmation.MainMenu;
    Label(443,578,394,40,"Wyprawa zatrzymana.\nMożesz odetchnąć — mrok zaczeka.",center,muted);
   }
   GUI.enabled=enabled;
   if(menuConfirmation!=MenuConfirmation.None)ConfirmationMenu();
   return true;
  }
  void AuxiliaryMenu()
  {
   Frame(new Rect(235,64,810,588));
   string name=menuPage==MenuPage.Settings ? "USTAWIENIA" : menuPage==MenuPage.Controls ? "STEROWANIE" : "AUTORZY I LICENCJE";
   Label(280,95,720,47,name,menuCentered,warmWhite);Divider(280,161,720);
   if(menuPage==MenuPage.Settings)SettingsMenu();
   else if(menuPage==MenuPage.Controls)ControlsMenu();
   else CreditsMenu();
   bool previous=GUI.enabled;
   var preferences=GetComponent<NocturnisSettings>();
   GUI.enabled=previous && !(menuPage==MenuPage.Settings && preferences && preferences.DisplayPending);
   if(Button(new Rect(433,590,414,39),game.State==KryptaGame.Phase.Title ? "WRÓĆ DO MENU  [ESC]" : "WRÓĆ DO PAUZY  [ESC]"))BackFromMenu();
   GUI.enabled=previous;
  }
  void SettingsMenu(){DrawExpandedSettings();}
  float VolumeSlider(float y,string label,float volume)
  {
   Label(282,y,580,26,label,menuText,paper);
   Label(913,y,83,26,Mathf.RoundToInt(volume*100)+"%",center,gold);
   Rect r=new Rect(285,y+26,710,22);
   float value=GUI.HorizontalSlider(r,volume,0,1,sliderTrack,sliderThumb);
   Fill(new Rect(r.x+10,r.y+11,r.width-20,5),new Color(.065f,.052f,.04f));
   Fill(new Rect(r.x+10,r.y+12,(r.width-20)*value,3),bronze);
   Diamond(r.x+10+(r.width-20)*value,r.y+13,8,gold);
   return value;
  }
  void ControlsMenu(){KeyBindingsMenu();}
  void CreditsMenu()
  {
   var localizedCredits=NocturnisLanguage.English ? Resources.Load<TextAsset>("Credits-en") : creditsText;
   string value=localizedCredits ? localizedCredits.text : "Licencje znajdują się w katalogu Licenses gry.";
   float height=menuText.CalcHeight(new GUIContent(NocturnisInput.Format(value)),674)+24;
   creditsScroll=GUI.BeginScrollView(new Rect(280,185,720,375),creditsScroll,new Rect(0,0,691,height));
   Label(5,4,674,height,value,menuText,paper);GUI.EndScrollView();
  }
  void ConfirmationMenu()
  {
   Fill(new Rect(0,0,1280,720),new Color(.009f,.006f,.004f,.88f));Frame(new Rect(346,217,588,286));
   string caption=menuConfirmation==MenuConfirmation.NewJourney ? "NOWA WYPRAWA" : menuConfirmation==MenuConfirmation.MainMenu ? "ZAKOŃCZYĆ WYPRAWĘ?" : "OPUŚCIĆ VAELORIĘ?";
   string copy=menuConfirmation==MenuConfirmation.NewJourney ? "Rozpoczęcie nowej historii zastąpi zapis obecną postacią. Dotychczasowa wyprawa zostanie utracona." : menuConfirmation==MenuConfirmation.MainMenu ? "Zapiszemy postęp wyprawy. Po powrocie rozpoczniesz od wejścia do bieżącej lokacji." : "Gra zostanie zamknięta. Zapisana wyprawa będzie czekać na Twój powrót.";
   Label(376,248,528,44,caption,menuCentered,warmWhite);Divider(387,305,506);
   Label(389,329,500,78,copy,menuText,paper);
   if(Button(new Rect(390,435,234,39),"ZOSTAJĘ"))menuConfirmation=MenuConfirmation.None;
   if(Button(new Rect(656,435,234,39),menuConfirmation==MenuConfirmation.NewJourney ? "ROZPOCZNIJ" : "POTWIERDZAM",true))
   {
    var action=menuConfirmation;menuConfirmation=MenuConfirmation.None;
    if(action==MenuConfirmation.NewJourney)EnterWorld();
    else if(action==MenuConfirmation.MainMenu){PlayerPrefs.Save();game.SaveAndReturnToTitle();}
    else {game.SaveBeforeQuit();PlayerPrefs.Save();Application.Quit();}
   }
  }
 }
}
