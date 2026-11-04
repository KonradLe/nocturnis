using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace Krypta
{
 // Opt-in visual fixtures in the actual player, never screenshots assembled outside the game.
 public sealed class NocturnisRpg081RuntimeQA:MonoBehaviour
 {
  [Serializable]sealed class Capture {public string screen,image,hero;public int heroIndex,width,height,bytes,sampledBrightnessRange,portraitBrightnessRange;}
  [Serializable]sealed class Report
  {
   public bool passed,productionPreferencesUnchanged,testingNamespace,qaPreferencesRestored;
   public int assertions,expectedFrames;public string error="",scope="Actual Windows player captures of controlled level-eight fixtures, plus state/save checks. Not a campaign playthrough or visual approval.";
   public List<string> checks=new List<string>();public List<Capture> frames=new List<Capture>();
  }
  sealed class Shot {public string screen;public int hero,width,height;public Shot(string s,int h,int w=1280,int z=720){screen=s;hero=h;width=w;height=z;}}
  const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static readonly Dictionary<string,string> guarded=new Dictionary<string,string>(),qaStrings=new Dictionary<string,string>();
  static readonly Dictionary<string,int?> qaInts=new Dictionary<string,int?>();
  static readonly string[] stringPrefs={"Nocturnis.Profile1.Checkpoint","Nocturnis.Profile2.Checkpoint","Nocturnis.Profile3.Checkpoint","Nocturnis.Checkpoint.v1","Nocturnis.Checkpoint.v2","Nocturnis.SharedStash.v1"};
  static readonly string[] intPrefs={"Nocturnis.ActiveProfile","Nocturnis.ProfileMigrationDone","Nocturnis.Muted","Nocturnis.LargeText","Nocturnis.CameraShake","Nocturnis.MapExploration","Nocturnis.ReducedFlashes","Nocturnis.TutorialEnabled","Nocturnis.FullScreen","Nocturnis.Settings.Quality","Nocturnis.Settings.VSync","Nocturnis.Settings.FrameCap","Nocturnis.Settings.DamageNumbers","Nocturnis.Settings.Width","Nocturnis.Settings.Height","Nocturnis.Settings.DisplayMode"};
  static readonly string[] floatPrefs={"Nocturnis.EffectsVolume","Nocturnis.AmbientVolume","Nocturnis.MusicVolume","Nocturnis.Settings.Voice"};

  static bool Requested {get{return Array.IndexOf(Environment.GetCommandLineArgs(),"-rpg081-review")>=0;}}
  static void RememberString(string key){qaStrings[key]=PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;}
  static void RememberInt(string key){qaInts[key]=PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Isolate()
  {
   if(!Requested)return;KryptaGame.Testing=true;guarded.Clear();qaStrings.Clear();qaInts.Clear();
   foreach(string key in stringPrefs)guarded[key]=Preference(key,0);
   foreach(string key in intPrefs)guarded[key]=Preference(key,1);
   foreach(string key in floatPrefs)guarded[key]=Preference(key,2);
   foreach(string action in NocturnisInput.Actions)RememberInt("Nocturnis.QA.Key."+action);
   RememberString("Nocturnis.QA.Checkpoint");RememberString("Nocturnis.QA.Stash");
   for(int v=1;v<=4;v++)RememberString("Nocturnis.QA.Checkpoint.Backup.v"+v);
   for(int z=0;z<Campaign.Count;z++)RememberString("Nocturnis.QA.Explored.1."+z);
   RememberInt("Nocturnis.QA.Explored.1.quest");RememberInt("Nocturnis.QA.ActiveProfile");RememberInt("Nocturnis.QA.ProfileMigrationDone");
   PlayerPrefs.DeleteKey("Nocturnis.QA.Checkpoint");PlayerPrefs.SetString("Nocturnis.QA.Stash","{}");
   PlayerPrefs.SetInt("Nocturnis.QA.ActiveProfile",1);PlayerPrefs.SetInt("Nocturnis.QA.ProfileMigrationDone",1);
  }
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Launch()
  {if(Requested)new GameObject("RPG 081 runtime review").AddComponent<NocturnisRpg081RuntimeQA>();}
  static string Preference(string key,int type)
  {
   if(!PlayerPrefs.HasKey(key))return "<missing>";
   return type==0 ? PlayerPrefs.GetString(key,"") : type==1 ? PlayerPrefs.GetInt(key).ToString(CultureInfo.InvariantCulture) : PlayerPrefs.GetFloat(key).ToString("R",CultureInfo.InvariantCulture);
  }
  Report result=new Report();KryptaGame game;KryptaHUD hud;string folder;float started;bool done;int configuredHero=-1;
  static object Call(object target,string name,params object[] args)
  {
   MethodInfo method=target.GetType().GetMethod(name,Private);if(method==null)throw new MissingMethodException(target.GetType().Name,name);
   return method.Invoke(target,args);
  }
  static void Set(object target,string name,object value)
  {
   PropertyInfo property=target.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
   if(property==null)throw new MissingMemberException(target.GetType().Name,name);property.SetValue(target,value);
  }
  static void Field(object target,string name,object value)
  {
   FieldInfo field=target.GetType().GetField(name,Private);if(field==null)throw new MissingFieldException(target.GetType().Name,name);field.SetValue(target,value);
  }
  static object Get(object target,string name)
  {var field=target.GetType().GetField(name,Private);if(field==null)throw new MissingFieldException(target.GetType().Name,name);return field.GetValue(target);}
  void MenuPage(string name){var field=typeof(KryptaHUD).GetField("menuPage",Private);field.SetValue(hud,Enum.Parse(field.FieldType,name));}
  void Check(bool condition,string message)
  {result.assertions++;if(!condition)throw new Exception(message);result.checks.Add(message);}
  void Log(string text,string stack,LogType type)
  {if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)result.error+=text+"\n"+stack+"\n";}

  IEnumerator Start()
  {
   started=Time.realtimeSinceStartup;Application.logMessageReceived+=Log;
   var args=Environment.GetCommandLineArgs();int output=Array.IndexOf(args,"-rpg081-output");
   if(output<0 || output+1>=args.Length){result.error="Required -rpg081-output directory missing";Finish();yield break;}
   bool configured=false;try{folder=Path.GetFullPath(args[output+1]);Directory.CreateDirectory(folder);configured=true;}catch(Exception e){result.error=e.ToString();Finish();}
   if(!configured)yield break;var routine=Review();
   while(!done)
   {
    bool advance=false,failed=false;object current=null;
    try{advance=routine.MoveNext();if(advance)current=routine.Current;}
    catch(Exception e){result.error+=e.ToString();Finish();failed=true;}
    if(failed)yield break;if(!advance)break;yield return current;
   }
   if(!done){result.passed=result.error.Length==0 && result.frames.Count==result.expectedFrames;Finish();}
  }
  IEnumerator Review()
  {
   while(!(game=FindFirstObjectByType<KryptaGame>()) || !game.View || !(hud=game.GetComponent<KryptaHUD>()))yield return null;
   yield return null;game.enabled=false;Time.timeScale=0;hud.enabled=true;
   Check(KryptaGame.Testing && !NocturnisCinematic.BlocksInput,"QA namespace active before game startup; cinematic not blocking input");result.testingNamespace=true;
   Texture2D atlas=Resources.Load<Texture2D>("UI/RpgItemAtlas");Check(atlas && atlas.width>=500 && atlas.height>=500,"Actual 25-cell item atlas is packaged in Resources");
   var shots=new List<Shot>();
   for(int hero=0;hero<5;hero++){shots.Add(new Shot("talents",hero));shots.Add(new Shot("inventory",hero));}
   foreach(string screen in new[]{"journal-active","journal-chapter","market-buy","market-sell","forge","stash","book"})shots.Add(new Shot(screen,3));
   foreach(string screen in new[]{"talents","inventory","journal-active","market-buy"})shots.Add(new Shot(screen,3,960,540));
   result.expectedFrames=shots.Count;int width=0,height=0;
   foreach(Shot shot in shots)
   {
    if(configuredHero!=shot.hero)PrepareHero(shot.hero);
    if(width!=shot.width || height!=shot.height)
    {
     Screen.SetResolution(shot.width,shot.height,FullScreenMode.Windowed);
     for(int frame=0;frame<10;frame++)yield return new WaitForEndOfFrame();
     Check(Screen.width==shot.width && Screen.height==shot.height,"Native window resolution: "+shot.width+"x"+shot.height);width=shot.width;height=shot.height;
    }
    PrepareScreen(shot.screen);
    for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
    AssertScreen(shot.screen);CaptureFrame(shot);
    yield return new WaitForEndOfFrame();
   }
   foreach(var capture in result.frames)Check(File.Exists(Path.Combine(folder,capture.image)),"Screenshot exists: "+capture.image);
  }
  void PrepareHero(int hero)
  {
   game.Resume();Set(game,"State",KryptaGame.Phase.Title);Check(game.SelectProfile(1),"Select isolated profile for hero "+hero);game.SelectHero(hero);
   Call(game,"ResetCampaign");Set(game,"Level",8);Set(game,"Coins",1800);Set(game,"Materials",75);
   Call(game,"LoadZone",Campaign.Town);Set(game,"State",KryptaGame.Phase.Playing);Field(game,"introUntil",0f);
   Set(game,"Health",game.MaxHealth);game.OpenTalents();
   Check(game.SpendTalent(0) && game.SpendTalent(0) && game.SpendTalent(1) && game.SpendTalent(3),"Actual talent actions spend four ranks for hero "+hero);
   Check(game.TalentPoints==3 && game.TalentRank(0)==2 && game.TalentRank(1)==1 && game.TalentRank(3)==1,"Four ranks and three unspent points for hero "+hero);
   game.Resume();
   int legendKind=hero==2 ? 2 : hero==0 || hero==4 ? 1 : 3;
   for(int slot=0;slot<3;slot++)Call(game,"EquipRelicData",new KryptaGame.Relic{slot=slot,rank=slot==0 ? 4 : 3,enchant=slot+1,affix=slot+1,affixValue=4,legend=slot==0 ? legendKind : 0,legendHero=slot==0 ? hero : -1});
   game.Inventory.Clear();
   for(int i=0;i<15;i++)
   {
    int rank=1+i%5;game.Inventory.Add(new KryptaGame.Relic{slot=i%3,rank=rank,enchant=i%4,affix=rank>1 ? 1+i%3 : 0,affixValue=rank>1 ? rank+1 : 0,identified=i!=8 && i!=13,protectedItem=i==2 || i==9,legend=i==4 || i==14 ? legendKind : 0,legendHero=i==4 || i==14 ? hero : -1});
   }
   for(int q=0;q<3;q++)
   {Call(game,"Talk",SideQuestData.NPCs[q],SideQuestData.Offers[q],SideQuestData.Services[q]);Check(game.OpenSideQuest(q) && game.AcceptSideQuest(q),"Real NPC acceptance of quest "+q+" for hero "+hero);}
   game.Resume();Set(game,"Health",game.MaxHealth);Time.timeScale=0;
   Check((bool)Call(game,"SaveCheckpoint") && game.ContinueCheckpoint(),"Fixture save/load roundtrip for hero "+hero);Time.timeScale=0;
   Check(game.SelectedHero==hero && game.Level==8 && game.TalentPoints==3 && game.Inventory.Count==15 && game.TrackedQuestCount==3,"Roundtrip preserves class, ranks, 15 items and three pins");
   for(int slot=0;slot<3;slot++)Check(game.EquippedRelic(slot).rank>0,"Equipped actual relic in slot "+slot);
   Check(!game.Inventory[8].identified && game.Inventory[2].protectedItem && game.Inventory[4].legend>0,"Fixture contains unidentified, protected and legendary items");
   // Seed only the isolated stash; its previous exact JSON is restored on exit.
   PlayerPrefs.SetString("Nocturnis.QA.Stash","{\"items\":[{\"slot\":0,\"rank\":4,\"identified\":true},{\"slot\":1,\"rank\":3,\"identified\":true},{\"slot\":2,\"rank\":2,\"identified\":true}]}");
   Check(game.Stash.Count==3,"Isolated shared stash contains three actual relics");
   configuredHero=hero;Call(game,"LateUpdate");
  }
  void PrepareScreen(string screen)
  {
   game.Resume();Time.timeScale=0;MenuPage("Main");Set(game,"NoticeUntil",0f);
   if(screen=="talents")game.OpenTalents();
   else if(screen=="inventory"){game.OpenEquipment();Call(hud,"SelectRpgInventoryItem",4);}
   else if(screen.StartsWith("journal-",StringComparison.Ordinal))
   {
    game.OpenJournal();Field(hud,"questJournalTab",screen=="journal-active" ? 1 : 0);Field(hud,"questJournalSelection",1);Field(hud,"questChapterSelection",0);
    Field(hud,"questJournalScroll",Vector2.zero);Field(hud,"questListScroll",Vector2.zero);Field(hud,"journalStoryExpanded",false);Field(hud,"journalDirectionsExpanded",false);
   }
   else if(screen.StartsWith("market-",StringComparison.Ordinal))
   {
    Call(game,"Talk","Eryk \u2022 handlarz relikt\u00f3w","Targ Oakhaven","market");Field(hud,"marketTab",screen=="market-buy" ? 0 : 1);Field(hud,"marketSelection",screen=="market-buy" ? 0 : 4);Field(hud,"marketScroll",Vector2.zero);Field(hud,"marketDetailsScroll",Vector2.zero);Field(hud,"marketSellConfirmation",false);
   }
   else if(screen=="forge" || screen=="stash")
   {Call(game,"Talk",screen=="forge" ? SideQuestData.NPCs[1] : "Wsp\u00f3lna skrytka","Relikty Oakhaven",screen);Field(hud,"serviceScroll",Vector2.zero);}
   else if(screen=="book")game.OpenBook();
   Time.timeScale=0;Call(game,"LateUpdate");
  }
  void AssertScreen(string screen)
  {
   Check(hud.enabled && game.View && game.View.enabled && game.View.cullingMask==~(1<<30) && !NocturnisCinematic.BlocksInput,"Live HUD and correctly masked real camera render "+screen);
   Check(game.SelectedHero==configuredHero && game.Level==8 && game.Inventory.Count==15,"Expected class and fixture remain unchanged for "+screen);
   if(screen=="talents")Check(game.TalentsOpen && game.State==KryptaGame.Phase.Paused && game.TalentPoints==3,"Actual talent window has learned and available nodes");
   else if(screen=="inventory")
   {
    Check(game.EquipmentOpen && game.State==KryptaGame.Phase.Paused && (int)Get(hud,"equipmentSelection")==4,"Actual inventory selection is legendary item four");
    Camera portraitCamera=Get(hud,"portraitCamera") as Camera;RenderTexture portrait=Get(hud,"portrait") as RenderTexture;
    Check(portraitCamera && portraitCamera.enabled && portrait && portrait.IsCreated() && Get(hud,"model")!=null,"Live 3D character portrait camera and model exist");
   }
   else if(screen.StartsWith("journal-",StringComparison.Ordinal))Check(game.JournalOpen && game.TrackedQuestCount==3 && (int)Get(hud,"questJournalTab")== (screen=="journal-active" ? 1 : 0),"Actual journal filter and three tracked quests selected");
   else if(screen.StartsWith("market-",StringComparison.Ordinal))Check(game.Service=="market" && game.State==KryptaGame.Phase.Dialogue && game.MarketStock[0]!=null && (int)Get(hud,"marketTab")==(screen=="market-buy" ? 0 : 1),"Actual market stock or inventory tab selected");
   else if(screen=="book")Check(game.BookOpen && game.State==KryptaGame.Phase.Paused,"Actual power book is open");
   else Check(game.Service==screen && game.State==KryptaGame.Phase.Dialogue,"Actual artisan service open: "+screen);
  }
  void CaptureFrame(Shot shot)
  {
   Texture2D texture=ScreenCapture.CaptureScreenshotAsTexture();
   try
   {
    Check(texture && texture.width==shot.width && texture.height==shot.height,"Rendered capture dimensions match "+shot.screen);
    Color32[] pixels=texture.GetPixels32();int low=255,high=0;
    for(int i=0;i<pixels.Length;i+=193){var p=pixels[i];int v=(p.r+p.g+p.b)/3;low=Math.Min(low,v);high=Math.Max(high,v);}
    int portraitRange=0;
    if(shot.screen=="inventory")
    {
     int pLow=255,pHigh=0;
     for(int y=(int)(shot.height*.44f);y<(int)(shot.height*.74f);y+=7)
      for(int x=(int)(shot.width*.11f);x<(int)(shot.width*.30f);x+=7)
      {var p=pixels[y*shot.width+x];int v=(p.r+p.g+p.b)/3;pLow=Math.Min(pLow,v);pHigh=Math.Max(pHigh,v);}
     portraitRange=pHigh-pLow;Check(portraitRange>12,"Character viewport contains visible rendered content");
    }
    byte[] bytes=texture.EncodeToPNG();Check(high-low>32 && bytes.Length>15000,"Rendered capture contains visible content: "+shot.screen);
    string name=shot.screen+"-hero-"+shot.hero+"-"+shot.width+"x"+shot.height+".png";File.WriteAllBytes(Path.Combine(folder,name),bytes);
    result.frames.Add(new Capture{screen=shot.screen,image=name,hero=game.Hero.person,heroIndex=shot.hero,width=shot.width,height=shot.height,bytes=bytes.Length,sampledBrightnessRange=high-low,portraitBrightnessRange=portraitRange});
   }
   finally{if(texture)Destroy(texture);}
  }
  static bool RestoreQaPreferences()
  {
   foreach(var saved in qaStrings){if(saved.Value==null)PlayerPrefs.DeleteKey(saved.Key);else PlayerPrefs.SetString(saved.Key,saved.Value);}
   foreach(var saved in qaInts){if(saved.Value.HasValue)PlayerPrefs.SetInt(saved.Key,saved.Value.Value);else PlayerPrefs.DeleteKey(saved.Key);}PlayerPrefs.Save();
   foreach(var saved in qaStrings)if(saved.Value==null ? PlayerPrefs.HasKey(saved.Key) : !PlayerPrefs.HasKey(saved.Key) || PlayerPrefs.GetString(saved.Key)!=saved.Value)return false;
   foreach(var saved in qaInts)if(saved.Value.HasValue ? !PlayerPrefs.HasKey(saved.Key) || PlayerPrefs.GetInt(saved.Key)!=saved.Value.Value : PlayerPrefs.HasKey(saved.Key))return false;return true;
  }
  void Update()
  {
   if(done)return;
   if(Time.realtimeSinceStartup-started>300 || result.error.Length>0)
   {if(result.error.Length==0)result.error="Runtime QA timed out after 300 seconds";StopAllCoroutines();result.passed=false;Finish();}
  }
  void Finish()
  {
   if(done)return;done=true;Application.logMessageReceived-=Log;
   result.qaPreferencesRestored=RestoreQaPreferences();if(!result.qaPreferencesRestored)result.error+="QA preferences were not restored.";
   result.productionPreferencesUnchanged=true;
   foreach(string key in stringPrefs)if(!guarded.ContainsKey(key) || guarded[key]!=Preference(key,0))result.productionPreferencesUnchanged=false;
   foreach(string key in intPrefs)if(!guarded.ContainsKey(key) || guarded[key]!=Preference(key,1))result.productionPreferencesUnchanged=false;
   foreach(string key in floatPrefs)if(!guarded.ContainsKey(key) || guarded[key]!=Preference(key,2))result.productionPreferencesUnchanged=false;
   if(!result.productionPreferencesUnchanged)result.error+="Production save/preferences changed during QA.";
   if(result.error.Length>0)result.passed=false;
   if(folder!=null)File.WriteAllText(Path.Combine(folder,"results.json"),JsonUtility.ToJson(result,true));
   Debug.Log("RPG081_QA_COMPLETE "+result.passed+" assertions="+result.assertions+" frames="+result.frames.Count);
   Application.Quit(result.passed ? 0 : 1);
  }
 }
}
