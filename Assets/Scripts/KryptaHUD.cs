using UnityEngine;
namespace Krypta
{
 public sealed partial class KryptaHUD : MonoBehaviour
 {
  KryptaGame game;
  GUIStyle text,small,heading,title,button,center,buffCaption,buffEffect,castFeedbackStyle;
  readonly Color ink=new Color(.027f,.027f,.038f,.96f),muted=new Color(.68f,.65f,.60f),gold=new Color(.80f,.65f,.42f),paper=new Color(.91f,.87f,.80f);
  float scale,offsetX,offsetY;
  Texture2D disc,orbSurface,menuArt,hudArt,bookArt,skillArt;
  Camera portraitCamera;
  RenderTexture portrait;
  ActorVisual model;
  int lastHero=-1;
  void Awake(){game=GetComponent<KryptaGame>();}
  void Styles()
  {
   if(text!=null)return;
   Font font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
   Font journal=Resources.Load<Font>("Fonts/NocturnisTitle");
   const string glyphs="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyzĄĆĘŁŃÓŚŹŻąćęłńóśźż0123456789 /:;,.!?+-−×%[]()•→←●○›";
   if(font.dynamic)foreach(int size in new[]{10,12,14,17}){font.RequestCharactersInTexture(glyphs,size,FontStyle.Normal);font.RequestCharactersInTexture(glyphs,size,FontStyle.Bold);}
   if(font.dynamic)foreach(int size in new[]{22,24,28,68})font.RequestCharactersInTexture(glyphs,size,FontStyle.Bold);
   text=new GUIStyle(GUI.skin.label){font=font,fontSize=17,wordWrap=true};text.normal.textColor=Color.white;
   small=new GUIStyle(text){fontSize=13};heading=new GUIStyle(text){font=journal ? journal : font,fontSize=27,fontStyle=journal ? FontStyle.Normal : FontStyle.Bold};
   title=new GUIStyle(heading){fontSize=68};center=new GUIStyle(small){alignment=TextAnchor.MiddleCenter};
   button=new GUIStyle(text){alignment=TextAnchor.MiddleCenter,fontSize=14,fontStyle=FontStyle.Bold};
   buffCaption=new GUIStyle(small){fontSize=12,fontStyle=FontStyle.Bold,wordWrap=false};
   buffEffect=new GUIStyle(small){fontSize=12};
   castFeedbackStyle=new GUIStyle(text){fontSize=14,fontStyle=FontStyle.Bold};
   disc=new Texture2D(128,128,TextureFormat.RGBA32,false);disc.wrapMode=TextureWrapMode.Clamp;
   for(int y=0;y<128;y++)for(int x=0;x<128;x++)
   {float d=Vector2.Distance(new Vector2(x,y),new Vector2(63.5f,63.5f))/63;disc.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01((1-d)*60)));}
   disc.Apply();
   orbSurface=new Texture2D(128,128,TextureFormat.RGBA32,false);orbSurface.wrapMode=TextureWrapMode.Clamp;
   for(int y=0;y<128;y++)for(int x=0;x<128;x++)
   {
    float nx=(x-63.5f)/63,ny=(y-63.5f)/63,d=nx*nx+ny*ny;
    float z=Mathf.Sqrt(Mathf.Max(0,1-d)),shade=.18f+.73f*z+.09f*Mathf.Sin(nx*12+ny*7);
    orbSurface.SetPixel(x,y,new Color(shade,shade,shade,Mathf.Clamp01((1-d)*55)));
   }
   orbSurface.Apply();skillArt=Resources.Load<Texture2D>("UI/SkillAtlas");
   menuArt=Resources.Load<Texture2D>("UI/MenuCathedral");hudArt=Resources.Load<Texture2D>("UI/GothicHUD");bookArt=Resources.Load<Texture2D>("UI/Grimoire");
  }
  void Update()
  {
   TickPortraitOrbit();
   if(Application.isBatchMode)return;
   if(!game || !game.View)return;
   if(gateAt>=0 && Time.unscaledTime-gateAt>=1.25f){gateAt=-1;menuPage=MenuPage.Main;game.Begin();}
   bool show=(game.State==KryptaGame.Phase.Title && menuPage==MenuPage.Characters) || game.EquipmentOpen;
   if(show && (lastHero!=game.SelectedHero || previewGarb!=game.Garb))Preview();
   if(portraitCamera)portraitCamera.enabled=show;
   if(show && model!=null){ApplyPortraitYaw();model.PreviewIdle(Time.unscaledTime);}
  }
  void Preview()
  {
   if(model!=null)Destroy(model.Root);
   if(!portraitCamera)
   {
    var o=new GameObject("Portret bohatera");o.transform.SetParent(transform);portraitCamera=o.AddComponent<Camera>();
    portrait=new RenderTexture(NocturnisPortraitFrame.Width,NocturnisPortraitFrame.Height,24);portraitCamera.targetTexture=portrait;portraitCamera.cullingMask=1<<30;
    portraitCamera.clearFlags=CameraClearFlags.SolidColor;portraitCamera.backgroundColor=Color.clear;portraitCamera.fieldOfView=31;portraitCamera.nearClipPlane=.1f;portraitCamera.farClipPlane=10;
    Light key=new GameObject("Światło portretu").AddComponent<Light>();key.transform.SetParent(o.transform,false);
    key.type=LightType.Directional;key.cullingMask=1<<30;key.intensity=1.05f;key.color=new Color(1,.95f,.89f);
    key.transform.localRotation=Quaternion.Euler(15,-30,0);
    Light rim=new GameObject("Światło konturu portretu").AddComponent<Light>();rim.transform.SetParent(o.transform,false);
    rim.type=LightType.Directional;rim.cullingMask=1<<30;rim.intensity=.5f;rim.color=new Color(.65f,.76f,1);
    rim.transform.localRotation=Quaternion.Euler(5,145,0);
   }
   var art=Resources.Load<KryptaAssets>("KryptaAssets");var h=art.heroes[game.SelectedHero];
   Vector3 p=new Vector3(1000,0,1000);model=new ActorVisual(h.model,h.animations,p,2.5f,"Podgląd klasy");model.Root.transform.SetParent(transform);model.Equip(h.weapon);
   foreach(var t in model.Root.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
   lastHero=game.SelectedHero;previewGarb=game.Garb;KryptaGame.ApplyGarb(model.Root,game.Garb);
   ResetPortraitOrbit();NocturnisPortraitFrame.Fit(portraitCamera,model);
  }
  void OnGUI()
  {
   BeginPortraitOrbitGUI();
   if(NocturnisCinematic.BlocksInput)return;
   if(!game || !game.View)return;Styles();storyTooltipSkill=-1;RpgHoverReset();
   Matrix4x4 previous=GUI.matrix;scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
   offsetX=(Screen.width-1280*scale)*.5f;offsetY=(Screen.height-720*scale)*.5f;
   GUI.matrix=Matrix4x4.TRS(new Vector3(offsetX,offsetY,0),Quaternion.identity,Vector3.one*scale);
   if(game.State==KryptaGame.Phase.Title)TitleScreen();
   else {InGame();if(game.IntroActive){Fill(new Rect(0,0,1280,91),Color.black);Fill(new Rect(0,629,1280,91),Color.black);Label(300,25,680,40,"TRAKT WISIELCÓW • DOLINA POPIOŁÓW",center,StoryPaleGold);Label(300,652,680,30,"SPACJA — POMIŃ WPROWADZENIE",center,warmWhite);}if(game.State!=KryptaGame.Phase.Playing)Overlay();}
   DrawStoryTooltip();RpgHoverDraw();GUI.matrix=previous;
  }
  void InGame()
  {
   float objectiveHeight=Mathf.Max(40,text.CalcHeight(new GUIContent(game.Objective),355)+8);
   Panel(new Rect(24,22,385,42+objectiveHeight));Label(40,32,355,22,"AKT I  /  "+NocturnisData.Zones[game.Zone].ToUpper(),small,gold);
   Label(40,58,355,objectiveHeight,game.Objective,text,paper);
   Panel(new Rect(1075,22,180,68));Label(1091,30,150,19,"ZŁOTO",small,gold);Label(1091,47,150,36,game.Coins.ToString("000"),heading,paper);
   MiniMap();TutorialPanel();QuestMechanicPanel();QuestTrackerPanel();RecallHUD();
   if(game.State==KryptaGame.Phase.Playing && !game.IntroActive && Button(new Rect(24,535,254,28),"[V] TALENTY • punkty: "+game.TalentPoints))game.OpenTalents();
   if(game.NoticeUntil>Time.time)
   {
    Label(431,29,610,64,game.Notice,text,new Color(.015f,.025f,.04f,.95f));
    Label(430,28,610,64,game.Notice,text,warmWhite);
   }
   foreach(var e in game.enemies)
   {
    if(e.hp<=0)continue;Vector3 p=game.View.WorldToScreenPoint(e.Position+Vector3.up*(e.boss ? 3.2f : 2.1f));
    float x=(p.x-offsetX)/scale,y=(Screen.height-p.y-offsetY)/scale;if(p.z<0 || x<0 || x>1280 || y<100 || y>550)continue;
    float w=e.boss ? 175 : e.elite ? 100 : 52;
    if(e.boss || e.elite){Label(x-124,y-24,250,23,e.label,center,Color.black);Label(x-125,y-25,250,23,e.label,center,StoryPaleGold);}
    Bar(new Rect(x-w/2,y,w,5),e.hp/e.maxHp,e.elite ? new Color(.7f,.4f,.9f) : new Color(.65f,.16f,.22f));
    float tagY=y+8;int marks=game.BloodMarkCount(e);float roots=game.EnemyRootRemaining(e),immune=game.EnemyControlImmuneRemaining(e);
    if(e.stunUntil>Time.time){Label(x-58,tagY,116,18,"OGŁUSZENIE",center,StoryPaleGold);tagY+=17;}
    else if(roots>0){Label(x-58,tagY,116,18,"KORZENIE "+roots.ToString("0.0")+" s",center,new Color(.65f,1,.68f));tagY+=17;}
    if(marks>0)
    {for(int m=0;m<3;m++)Circle(x-43+m*8,tagY+8,2.5f,m<marks ? new Color(1,.35f,.43f) : new Color(.32f,.18f,.22f));Label(x-20,tagY,82,18,"KREW "+marks+"/3",center,new Color(1,.67f,.71f));tagY+=17;}
    if(immune>0 && e.stunUntil<=Time.time)Label(x-70,tagY,140,18,"ODP. OGŁ. "+immune.ToString("0.0")+" s",center,new Color(.78f,.86f,1));
   }
   if(NocturnisSettings.DamageNumbersEnabled)foreach(var f in game.combatText)
   {
    Vector3 p=game.View.WorldToScreenPoint(f.position+Vector3.up*(1-(f.until-Time.time)));
    float x=(p.x-offsetX)/scale,y=(Screen.height-p.y-offsetY)/scale;if(p.z<0 || y<100 || y>570)continue;
    Color c=f.color;c.a=Mathf.Clamp01((f.until-Time.time)*3);
    Label(x-79,y-1,160,28,f.text,button,Color.black);Label(x-80,y-2,160,28,f.text,button,c);
   }
   if(!string.IsNullOrEmpty(game.Interaction)){Panel(new Rect(430,448,420,42));Label(445,455,390,29,game.Interaction,button,paper);}
   int n=0;foreach(var s in game.Status.Entries)
   {
    float x=24+(n%3)*218,y=76+objectiveHeight+(n/3)*66;Panel(new Rect(x,y,210,60));
    Color c=s.harmful ? new Color(1,.38f,.30f) : new Color(.53f,.86f,.49f);float remaining=Mathf.Max(0,s.until-Time.time);
    Label(x+9,y+5,154,20,s.label+(s.stacks>1 ? " ×"+s.stacks : ""),buffCaption,c);
    Label(x+163,y+5,37,20,Mathf.CeilToInt(remaining)+" s",center,paper);
    Label(x+9,y+25,190,29,game.BuffDescription(s.id),buffEffect,muted);
    Fill(new Rect(x+9,y+54,192*Mathf.Clamp01(remaining/Mathf.Max(.01f,s.duration)),2),c);n++;
   }
   GothicBelt();
   if(game.CastNoticeUntil>Time.unscaledTime && !string.IsNullOrEmpty(game.CastNotice) && game.State==KryptaGame.Phase.Playing)
   {
    float height=Mathf.Clamp(Mathf.Ceil(castFeedbackStyle.CalcHeight(new GUIContent(game.CastNotice),465))+20,44,80);
    Rect feedback=new Rect(425,552-height,500,height);StoryPanel(feedback,0);
    Color c=game.CastNoticeSucceeded ? new Color(.58f,.88f,.51f) : new Color(1,.46f,.34f);
    Fill(new Rect(feedback.x+6,feedback.y+6,4,feedback.height-12),c);
    Label(feedback.x+20,feedback.y+9,465,feedback.height-18,game.CastNotice,castFeedbackStyle,c);
   }
  }
  void Skill(float x,string key,string name,float remaining,string detail)
  {
   Panel(new Rect(x,599,121,88));Label(x+9,606,105,18,key,small,gold);
   Label(x+9,630,105,33,remaining>0 ? remaining.ToString("0.0")+" s" : name,small,paper);Label(x+9,666,105,16,detail,small,muted);
  }
  void MiniMap()
  {
   Panel(new Rect(1118,110,137,220));Label(1129,119,115,20,"SZLAK",small,gold);
   for(int x=0;x<DungeonGrid.Width;x++)for(int z=0;z<DungeonGrid.Height;z++)
    if(game.MapFloor(x,z) && game.MapDiscovered(x,z))Fill(new Rect(1135+x*4,305-z*4,3.5f,3.5f),new Color(.23f,.24f,.28f));
   Vector3 p=game.HeroPosition/1.8f,o=game.ObjectivePosition/1.8f;
   Fill(new Rect(1134+o.x*4,304-o.z*4,6,6),gold);
   foreach(var e in game.enemies)if(e.hp>0 && Vector3.Distance(e.Position,game.HeroPosition)<12){Vector3 q=e.Position/1.8f;Fill(new Rect(1135+q.x*4,305-q.z*4,3,3),new Color(.8f,.2f,.25f));}
   for(int i=0;i<game.MapCitizenCount;i++){Vector3 n=game.MapCitizenPosition(i)/1.8f;if(game.MapDiscovered(Mathf.RoundToInt(n.x),Mathf.RoundToInt(n.z)))Diamond(1136+n.x*4,306-n.z*4,3,new Color(.3f,.7f,1));}
   Vector3 quest;if(game.TrackedQuestPosition(out quest)){quest/=1.8f;Diamond(1136+quest.x*4,306-quest.z*4,4,new Color(.3f,1,.7f));}
   Fill(new Rect(1134+p.x*4,304-p.z*4,5,5),game.Hero.color);
   Label(1127,308,126,22,"◆ Cel  ◇ NPC",small,gold);
  }
  void Overlay()
  {
   if(ClassicPauseMenu())return;
   if(game.TalentsOpen){TalentPanel();return;}
   if(game.EquipmentOpen){DetailedEquipment();return;}
   if(game.JournalOpen){SideQuestJournal();return;}
   Fill(new Rect(0,0,1280,720),new Color(.008f,.008f,.016f,.88f));Frame(new Rect(260,128,760,460));
   if(game.BookOpen){DetailedBook();return;}
   if(!string.IsNullOrEmpty(game.Service)){Services();return;}
   bool dialogue=game.State==KryptaGame.Phase.Dialogue,won=game.State==KryptaGame.Phase.Won,dead=game.State==KryptaGame.Phase.Dead;
   string h=dialogue ? game.DialogueName : game.JournalOpen ? "DZIENNIK • SERCE POD KATEDRĄ" : won ? "SERCE WCIĄŻ BIJE" : dead ? "MROK UPOMNIAŁ SIĘ O CIEBIE" : "CHWILA CISZY";
   Label(298,166,688,70,h,heading,paper);
   string copy=dialogue ? game.DialogueText : game.JournalOpen ? "Cel: "+game.Objective+"\n\nIrma poprosiła o odzyskanie odłamka serca spod katedry. Szlak prowadzi przez Martwe Błonia i cmentarz, gdzie pielgrzymi wracają z martwych. Krwawy Biskup Malazar przygotowuje koronację poległych królów.\n\nKapliczki: Rzeź skraca odnowienia i znosi koszt mocy. Łaska daje szybkość i łańcuchy światła. Źródło Życia oczyszcza Skazę Ichoritu."
    : won ? "Malazar upadł, lecz odłamek Varkosa wciąż bije. Za piaskami Kar-Darak ktoś czeka na wieść z Oakhaven.\n\nAkt I ukończony.\n\n"+game.ChronicleEnding
    : dead ? "Czytaj zapowiedzi ataków, korzystaj z kapliczek i oczyszczaj Skazę Ichoritu.\n\nUnik chroni podczas doskoku. Moc R osłania lub leczy.\n\nMożesz wrócić do zapisu z wejścia do lokacji. Jej przeciwnicy i łupy zostaną odtworzeni."
    : "WASD / kliknięcie — ruch\nLPM na wrogu lub Shift + LPM — atak\nQ / PPM — moc klasowa • R — ochrona\nSpacja — unik • H — mikstura\nE — interakcja • J — dziennik • I — ekwipunek • K — księga mocy\nT — zwój powrotu • V — talenty\n\nZapis automatyczny przy wejściu do lokacji.";
   Label(298,240,684,225,copy,text,muted);
   if(won || dead)Label(298,457,680,25,"Złoto: "+game.Coins+"     Pokonani: "+game.Defeated+"     Poziom: "+game.Level,small,gold);
   if(dead && KryptaGame.HasCheckpoint && Button(new Rect(298,479,684,25),"PONÓW OD POCZĄTKU LOKACJI"))game.ContinueCheckpoint();
   if(!dead && !won && !dialogue && !game.JournalOpen)
   {
    if(Button(new Rect(298,465,210,28),"RELIKTY [I]"))game.OpenEquipment();
    if(Button(new Rect(528,465,210,28),game.Muted ? "WŁĄCZ DŹWIĘK" : "WYCISZ"))game.ToggleAudio();
    if(Button(new Rect(758,465,224,28),"MENU GŁÓWNE"))game.Restart();
   }
   if(Button(new Rect(298,520,684,40),won || dead ? "POWRÓT DO WYBORU BOHATERA" : dialogue ? "WYRUSZAM" : "WRÓĆ DO WYPRAWY",true))
   {if(won || dead)game.Restart();else game.Resume();}
  }
  void Panel(Rect r){ClassicFrame(r,Mathf.Min(16,r.height*.3f));}
  static void Fill(Rect r,Color c){Color old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
  void Circle(float x,float y,float radius,Color c){Color old=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(x-radius,y-radius,radius*2,radius*2),disc);GUI.color=old;}
  void Bar(Rect r,float value,Color c){Fill(r,new Color(.12f,.11f,.14f));Fill(new Rect(r.x,r.y,r.width*Mathf.Clamp01(value),r.height),c);}
  static readonly System.Collections.Generic.Dictionary<GUIStyle,GUIStyle> largerStyles=new System.Collections.Generic.Dictionary<GUIStyle,GUIStyle>();
  static void Label(float x,float y,float w,float h,string value,GUIStyle style,Color color)
  {Color old=GUI.contentColor;GUI.contentColor=color;GUIStyle drawStyle=style;if(PlayerPrefs.GetInt("Nocturnis.LargeText",0)==1 && w>=280 && h>=40 && style.fontSize<=17){if(!largerStyles.TryGetValue(style,out drawStyle)){drawStyle=new GUIStyle(style);drawStyle.fontSize=style.fontSize+2;largerStyles[style]=drawStyle;}}GUI.Label(new Rect(x,y,w,h),NocturnisInput.Format(value),drawStyle);GUI.contentColor=old;}
  bool Button(Rect r,string label,bool primary=false)
  {
   bool enabled=GUI.enabled,hover=enabled && r.Contains(Event.current.mousePosition);
   Fill(r,!enabled ? new Color(.045f,.04f,.035f) : hover ? new Color(.24f,.155f,.075f) : primary ? new Color(.17f,.095f,.045f) : new Color(.073f,.055f,.043f));
   Outline(r,enabled ? hover || primary ? gold : bronze : bronze*.45f);
   Outline(new Rect(r.x+3,r.y+3,r.width-6,r.height-6),new Color(.48f,.32f,.16f,enabled ? .32f : .12f));
   if(r.width>230){Diamond(r.x+12,r.center.y,3,enabled ? bronze : bronze*.4f);Diamond(r.xMax-12,r.center.y,3,enabled ? bronze : bronze*.4f);}
   bool clicked=GUI.Button(r,GUIContent.none,GUIStyle.none);
   // Keep the disabled caption legible without enabling the actual control.
   GUI.enabled=true;
   Label(r.x+12,r.y,r.width-24,r.height,label,button,!enabled ? new Color(.53f,.49f,.42f) : hover || primary ? warmWhite : paper);
   GUI.enabled=enabled;
   if(clicked)game.UISound("ui-click");return clicked;
  }
  void OnDestroy(){CancelPortraitDrag();if(portrait){portrait.Release();Destroy(portrait);}if(disc)Destroy(disc);if(orbSurface)Destroy(orbSurface);if(storyPaperTexture)Destroy(storyPaperTexture);if(storyLeatherTexture)Destroy(storyLeatherTexture);}
 }
}
