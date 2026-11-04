using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace Krypta
{
 // Explicit opt-in player review. Screenshots are captured from the actual rendered
 // IMGUI windows. Simulation freezes between fixtures; production preferences are read only.
 public sealed class NocturnisDevelopment080RuntimeQA:MonoBehaviour
 {
  [Serializable]sealed class Capture {public string screen,image;public int width,height,bytes,sampledBrightnessRange,worldBrightnessRange;}
  [Serializable]sealed class Report
  {
   public bool passed,productionPreferencesUnchanged,testingNamespace,displayLocked,displayTimeoutPassed,displayConfirmationPassed,qaDisplayPreferencesRestored;
   public float displayRollbackSeconds;
   public int assertions;public string error="",scope="Windows player functional checks and actual HUD captures; no claim of a full campaign playthrough.";
   public List<string> checks=new List<string>();public List<Capture> frames=new List<Capture>();
  }
  const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static readonly Dictionary<string,string> guarded=new Dictionary<string,string>();
  static readonly string[] stringPrefs={"Nocturnis.Profile1.Checkpoint","Nocturnis.Profile2.Checkpoint","Nocturnis.Profile3.Checkpoint","Nocturnis.Checkpoint.v1","Nocturnis.Checkpoint.v2","Nocturnis.SharedStash.v1"};
  static readonly string[] intPrefs={"Nocturnis.ActiveProfile","Nocturnis.ProfileMigrationDone","Nocturnis.Muted","Nocturnis.LargeText","Nocturnis.CameraShake","Nocturnis.MapExploration","Nocturnis.ReducedFlashes","Nocturnis.TutorialEnabled","Nocturnis.FullScreen","Nocturnis.Settings.Quality","Nocturnis.Settings.VSync","Nocturnis.Settings.FrameCap","Nocturnis.Settings.DamageNumbers","Nocturnis.Settings.Width","Nocturnis.Settings.Height","Nocturnis.Settings.DisplayMode"};
  static readonly string[] floatPrefs={"Nocturnis.EffectsVolume","Nocturnis.AmbientVolume","Nocturnis.MusicVolume","Nocturnis.Settings.Voice"};
  static bool Requested {get{return Array.IndexOf(Environment.GetCommandLineArgs(),"-development080-review")>=0;}}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Isolate()
  {
   if(!Requested)return;KryptaGame.Testing=true;guarded.Clear();
   foreach(string key in stringPrefs)guarded[key]=Preference(key,0);
   foreach(string key in intPrefs)guarded[key]=Preference(key,1);
   foreach(string key in floatPrefs)guarded[key]=Preference(key,2);
  }
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Launch()
  {if(Requested)new GameObject("Development 080 runtime review").AddComponent<NocturnisDevelopment080RuntimeQA>();}
  static string Preference(string key,int type)
  {
   if(!PlayerPrefs.HasKey(key))return "<missing>";
   return type==0 ? PlayerPrefs.GetString(key,"") : type==1 ? PlayerPrefs.GetInt(key).ToString(CultureInfo.InvariantCulture) : PlayerPrefs.GetFloat(key).ToString("R",CultureInfo.InvariantCulture);
  }
  Report result=new Report();KryptaGame game;KryptaHUD hud;string folder;float started;bool done;
  readonly Dictionary<string,int?> qaDisplayBefore=new Dictionary<string,int?>();
  bool qaDisplayRestoreNeeded;
  static readonly string[] qaDisplayKeys={"Nocturnis.QA.Settings.Width","Nocturnis.QA.Settings.Height","Nocturnis.QA.Settings.DisplayMode"};
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
   var args=Environment.GetCommandLineArgs();int output=Array.IndexOf(args,"-development080-output");
   if(output<0 || output+1>=args.Length){result.error="Required -development080-output directory missing";Finish();yield break;}
   bool configured=false;try{folder=Path.GetFullPath(args[output+1]);Directory.CreateDirectory(folder);configured=true;}catch(Exception e){result.error=e.ToString();Finish();}
   if(!configured)yield break;
   var routine=Review();
   while(!done)
   {
    bool advance=false,failed=false;object current=null;
    try{advance=routine.MoveNext();if(advance)current=routine.Current;}
    catch(Exception e){result.error+=e.ToString();Finish();failed=true;}
    if(failed)yield break;
    if(!advance)break;yield return current;
   }
   if(!done){result.passed=result.error.Length==0 && result.frames.Count==14;Finish();}
  }
  IEnumerator Review()
  {
   while(!(game=FindFirstObjectByType<KryptaGame>()) || !game.View || !(hud=game.GetComponent<KryptaHUD>()))yield return null;
   yield return null;
   game.enabled=false;Time.timeScale=0;hud.enabled=true;
   Check(KryptaGame.Testing && !NocturnisCinematic.BlocksInput,"Isolated QA namespace active and cinematic not blocking HUD");result.testingNamespace=true;
   result.displayLocked=NocturnisSettings.DisplayLocked;
   Set(game,"State",KryptaGame.Phase.Title);Check(game.SelectProfile(1),"Select isolated QA profile");game.SelectHero(3);
   Call(game,"ResetCampaign");Set(game,"Level",8);Set(game,"Coins",1200);Call(game,"LoadZone",Campaign.Town);Set(game,"State",KryptaGame.Phase.Playing);
   Field(game,"introUntil",0f);Time.timeScale=0;
   TalentChecks();QuestChecks();MarketChecks();RecallChecks();
   // Put the stable presentation fixture back in town; quest state and learned ranks persist.
   Call(game,"LoadZone",Campaign.Town);Set(game,"State",KryptaGame.Phase.Playing);Set(game,"Health",game.MaxHealth);Time.timeScale=0;
   // The game's disabled LateUpdate normally opens the world camera after Title.
   // Keep the real camera/shader state correct without advancing combat simulation.
   Call(game,"LateUpdate");
   var settings=NocturnisSettings.Ensure(game.gameObject);
   Check(!NocturnisSettings.DisplayLocked,"Display preview is exercised without CLI screen overrides");
   Screen.SetResolution(1280,720,FullScreenMode.Windowed);
   for(int frame=0;frame<10;frame++)yield return new WaitForEndOfFrame();
   Check(Screen.width==1280 && Screen.height==720 && Screen.fullScreenMode==FullScreenMode.Windowed,"Display test starts at 1280x720 windowed");
   RememberQaDisplay();PrepareScreen("settings-graphics");
   float frozen=Time.time;double previewStarted=Time.realtimeSinceStartupAsDouble;
   Check(settings.BeginDisplayPreview(960,540,FullScreenMode.Windowed),"Real display preview starts");
   for(int frame=0;frame<10;frame++)yield return new WaitForEndOfFrame();
   Check(settings.DisplayPending && Screen.width==960 && Screen.height==540,"Pending display preview actually changes native window size");
   Check(!settings.BeginDisplayPreview(800,600,FullScreenMode.Windowed),"Second display preview cannot replace pending rollback state");
   while(settings.DisplayPending && Time.realtimeSinceStartupAsDouble-previewStarted<22)yield return null;
   for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
   result.displayRollbackSeconds=(float)(Time.realtimeSinceStartupAsDouble-previewStarted);
   Check(!settings.DisplayPending && Screen.width==1280 && Screen.height==720 && Screen.fullScreenMode==FullScreenMode.Windowed,"Unconfirmed display rolls back to original native window");
   Check(result.displayRollbackSeconds>=14.5f && Mathf.Abs(Time.time-frozen)<.001f,"Fifteen-second rollback uses real time while gameplay is paused");
   Check(QaDisplayUnchanged(),"Unconfirmed preview does not save display preferences");result.displayTimeoutPassed=true;
   Check(settings.BeginDisplayPreview(960,540,FullScreenMode.Windowed),"Second real display preview starts after rollback");
   for(int frame=0;frame<10;frame++)yield return new WaitForEndOfFrame();
   Check(Screen.width==960 && Screen.height==540,"Native window accepts second preview");settings.ConfirmDisplay();
   Check(!settings.DisplayPending && PlayerPrefs.GetInt(qaDisplayKeys[0])==960 && PlayerPrefs.GetInt(qaDisplayKeys[1])==540 && PlayerPrefs.GetInt(qaDisplayKeys[2])==(int)FullScreenMode.Windowed,"Confirm persists actual accepted resolution in isolated QA preferences");
   Screen.SetResolution(1280,720,FullScreenMode.Windowed);
   for(int frame=0;frame<8;frame++)yield return new WaitForEndOfFrame();
   settings.ApplySavedSettings();
   for(int frame=0;frame<10;frame++)yield return new WaitForEndOfFrame();
   Check(Screen.width==960 && Screen.height==540 && Screen.fullScreenMode==FullScreenMode.Windowed,"Applying saved settings restores confirmed native resolution");result.displayConfirmationPassed=true;
   RestoreQaDisplay();Check(QaDisplayUnchanged(),"Display test restores pre-existing QA preference values and missing keys");result.qaDisplayPreferencesRestored=true;
   foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(960,540)})
   {
    Screen.SetResolution(size.x,size.y,false);
    for(int frame=0;frame<10;frame++)yield return new WaitForEndOfFrame();
    Check(Screen.width==size.x && Screen.height==size.y,"Requested resolution applied: "+size.x+"x"+size.y);
    foreach(string screen in new[]{"talents","journal","market","settings-graphics","settings-audio","settings-accessibility","quest-tracker"})
    {
     PrepareScreen(screen);
     for(int frame=0;frame<4;frame++)yield return new WaitForEndOfFrame();
     AssertScreen(screen);CaptureFrame(screen,size);
     yield return new WaitForEndOfFrame();
    }
   }
   foreach(var capture in result.frames)Check(File.Exists(Path.Combine(folder,capture.image)),"Screenshot exists: "+capture.image);
  }
  void TalentChecks()
  {
   game.OpenTalents();Check(game.TalentsOpen && game.TalentPoints==7,"Level eight has seven unspent talent points");
   Check(game.SpendTalent(0) && game.SpendTalent(0) && game.SpendTalent(1) && game.SpendTalent(3),"Spend four valid talent ranks through real actions");
   Check(game.TalentPoints==3 && game.TalentRank(0)==2 && game.TalentRank(1)==1 && game.TalentRank(3)==1,"Ranks and remaining point budget match");
   Check(!game.SpendTalent(2) && game.TalentPoints==3,"Locked next node cannot spend points");
   Check((bool)Call(game,"SaveCheckpoint") && game.ContinueCheckpoint(),"Talent checkpoint roundtrip succeeds");Time.timeScale=0;
   Check(game.SelectedHero==3 && game.Level==8 && game.TalentPoints==3 && game.TalentRank(0)==2 && game.TalentRank(1)==1 && game.TalentRank(3)==1,"Learned talents persist in player save");
  }
  void QuestChecks()
  {
   for(int q=0;q<3;q++)
   {
    Call(game,"Talk",SideQuestData.NPCs[q],SideQuestData.Offers[q],SideQuestData.Services[q]);
    Check(game.OpenSideQuest(q) && game.AcceptSideQuest(q),"Accepted side quest "+q+" through NPC dialogue");
   }
   Check(game.TrackedQuestCount==3,"Three accepted quests auto-pin");string before=string.Join("|",game.CaptureQuestTracking());
   game.Resume();Time.timeScale=0;Check((bool)Call(game,"SaveCheckpoint") && game.ContinueCheckpoint(),"Quest checkpoint roundtrip succeeds");Time.timeScale=0;
   Check(game.TrackedQuestCount==3 && string.Join("|",game.CaptureQuestTracking())==before,"Pinned stable quest IDs and order persist");
  }
  void MarketChecks()
  {
   Call(game,"Talk","Eryk • handlarz reliktów","Targ Oakhaven","market");
   int count=game.Inventory.Count,coins=game.Coins,price=game.MarketPrice(game.MarketStock[0]);
   Check(game.BuyMarketItem(0) && game.Inventory.Count==count+1 && game.Coins==coins-price && game.MarketStock[0]==null,"Purchase moves one offer to inventory for exact price");
   coins=game.Coins;Check(!game.BuyMarketItem(0) && game.Coins==coins && game.Inventory.Count==count+1,"Bought offer cannot be purchased twice");
   int sale=game.MarketPrice(game.Inventory[count],true);
   Check(game.SellMarketItem(count) && game.Inventory.Count==count && game.Coins==coins+sale && game.MarketBuyback.Count==1,"Sale moves one inventory item to buyback");
   coins=game.Coins;Check(!game.SellMarketItem(count) && game.Coins==coins && game.MarketBuyback.Count==1,"Sold inventory index cannot pay twice");
   Check(game.BuyMarketItem(0,true) && game.Inventory.Count==count+1 && game.MarketBuyback.Count==0 && game.Coins==coins-sale,"Buyback restores exactly one item");
   coins=game.Coins;Check(!game.BuyMarketItem(0,true) && game.Coins==coins,"Empty buyback cannot duplicate item");
   game.Resume();Time.timeScale=0;Check((bool)Call(game,"SaveCheckpoint") && game.ContinueCheckpoint(),"Market checkpoint roundtrip succeeds");Time.timeScale=0;
   Check(game.MarketStock[0]==null && game.Inventory.Count==count+1,"Purchased offer remains sold after reload");
  }
  void RecallChecks()
  {
   game.Resume();Call(game,"LoadZone",Campaign.Fields);Time.timeScale=0;
   int scrolls=game.RecallScrolls;Vector3 origin=game.HeroPosition;
   Check(game.StartRecall() && game.RecallActive,"Scroll channel starts outside town");
   Check(game.RecallScrolls==scrolls,"Beginning channel does not consume scroll");game.CancelRecall();
   Check(game.RecallScrolls==scrolls && !game.RecallActive,"Cancelled channel preserves scroll");
   Check(game.StartRecall(),"Recall restarts after cancellation");Call(game,"CompleteRecall");Time.timeScale=0;
   Check(game.Zone==Campaign.Town && game.RecallScrolls==scrolls-1 && game.HasRecallTrace,"Completed channel consumes one scroll and records return trace");
   Call(game,"CompleteRecall");Check(game.RecallScrolls==scrolls-1,"Repeated completion cannot consume another scroll");
   Check(game.ContinueCheckpoint() && game.HasRecallTrace && game.RecallScrolls==scrolls-1,"Recall count and trace survive player save reload");Time.timeScale=0;
   Call(game,"Talk","Neme • kartografka","Ślad powrotu","waypoint");
   Check(game.ReturnAlongRecallTrace() && game.Zone==Campaign.Fields && !game.HasRecallTrace,"Neme returns player along trace exactly once");Time.timeScale=0;
   Check(Vector3.Distance(game.HeroPosition,origin)<.1f && game.RecallScrolls==scrolls-1,"Return trace restores position without extra scroll cost");
   Check(!game.ReturnAlongRecallTrace(),"Used trace cannot teleport again");
  }
  void PrepareScreen(string screen)
  {
   game.Resume();Time.timeScale=0;MenuPage("Main");Set(game,"NoticeUntil",0f);
   if(screen=="talents")game.OpenTalents();
   else if(screen=="journal")
   {
    game.OpenJournal();Field(hud,"questJournalTab",1);Field(hud,"questJournalSelection",1);
    Field(hud,"questJournalScroll",Vector2.zero);Field(hud,"questListScroll",Vector2.zero);
   }
   else if(screen=="market")
   {
    Call(game,"Talk","Eryk • handlarz reliktów","Targ Oakhaven","market");
    Field(hud,"marketTab",0);Field(hud,"marketSelection",1);Field(hud,"marketScroll",Vector2.zero);
   }
   else if(screen.StartsWith("settings-",StringComparison.Ordinal))
   {
    game.Pause();MenuPage("Settings");Field(hud,"settingsTab",screen=="settings-audio" ? 0 : screen=="settings-graphics" ? 1 : 2);
   }
   else if(screen=="quest-tracker")Call(game,"RevealMap");
   Time.timeScale=0;Call(game,"LateUpdate");
  }
  void AssertScreen(string screen)
  {
   Check(hud.enabled && game.View && game.View.enabled && game.View.cullingMask==~(1<<30) && !NocturnisCinematic.BlocksInput,"Live HUD and world camera render "+screen);
   if(screen=="talents")Check(game.TalentsOpen && game.State==KryptaGame.Phase.Paused && game.TalentPoints==3,"Talent window open with learned and available nodes");
   else if(screen=="journal")Check(game.JournalOpen && (int)Get(hud,"questJournalTab")==1 && game.TrackedQuestCount==3,"Journal active filter open with three pinned quests");
   else if(screen=="market")Check(game.Service=="market" && game.State==KryptaGame.Phase.Dialogue && game.MarketStock[1]!=null,"Market window shows an actual selected offer");
   else if(screen.StartsWith("settings-",StringComparison.Ordinal))
   {
    int expected=screen=="settings-audio" ? 0 : screen=="settings-graphics" ? 1 : 2;
    Check(game.State==KryptaGame.Phase.Paused && Get(hud,"menuPage").ToString()=="Settings" && (int)Get(hud,"settingsTab")==expected,"Actual settings tab selected: "+screen);
   }
   else Check(game.State==KryptaGame.Phase.Playing && game.TrackedQuestCount==3 && game.RecallScrolls>0,"Gameplay HUD shows three quest pins and recall scroll control");
  }
  void CaptureFrame(string screen,Vector2Int size)
  {
   Texture2D texture=ScreenCapture.CaptureScreenshotAsTexture();
   try
   {
    Check(texture && texture.width==size.x && texture.height==size.y,"Rendered capture dimensions match "+screen);
    Color32[] pixels=texture.GetPixels32();int low=255,high=0;
    for(int i=0;i<pixels.Length;i+=193){var p=pixels[i];int v=(p.r+p.g+p.b)/3;low=Math.Min(low,v);high=Math.Max(high,v);}
    int worldRange=0;
    if(screen=="quest-tracker")
    {
     int worldLow=255,worldHigh=0;
     for(int y=(int)(size.y*.38f);y<(int)(size.y*.80f);y+=11)
      for(int x=(int)(size.x*.28f);x<(int)(size.x*.72f);x+=11)
      {var sample=pixels[y*size.x+x];int value=(sample.r+sample.g+sample.b)/3;worldLow=Math.Min(worldLow,value);worldHigh=Math.Max(worldHigh,value);}
     worldRange=worldHigh-worldLow;Check(worldRange>12,"Gameplay world contains rendered scene content beyond HUD frames");
    }
    byte[] bytes=texture.EncodeToPNG();Check(high-low>32 && bytes.Length>15000,"Rendered capture contains visible content: "+screen);
    string name=screen+"-"+size.x+"x"+size.y+".png";File.WriteAllBytes(Path.Combine(folder,name),bytes);
    result.frames.Add(new Capture{screen=screen,image=name,width=size.x,height=size.y,bytes=bytes.Length,sampledBrightnessRange=high-low,worldBrightnessRange=worldRange});
   }
   finally{if(texture)Destroy(texture);}
  }
  void RememberQaDisplay()
  {
   qaDisplayBefore.Clear();foreach(string key in qaDisplayKeys)qaDisplayBefore[key]=PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null;
   qaDisplayRestoreNeeded=true;
  }
  bool QaDisplayUnchanged()
  {
   foreach(var saved in qaDisplayBefore)
    if(saved.Value.HasValue ? !PlayerPrefs.HasKey(saved.Key) || PlayerPrefs.GetInt(saved.Key)!=saved.Value.Value : PlayerPrefs.HasKey(saved.Key))return false;
   return true;
  }
  void RestoreQaDisplay()
  {
   if(!qaDisplayRestoreNeeded)return;
   foreach(var saved in qaDisplayBefore){if(saved.Value.HasValue)PlayerPrefs.SetInt(saved.Key,saved.Value.Value);else PlayerPrefs.DeleteKey(saved.Key);}
   PlayerPrefs.Save();qaDisplayRestoreNeeded=false;
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
   RestoreQaDisplay();result.qaDisplayPreferencesRestored=QaDisplayUnchanged();
   if(!result.qaDisplayPreferencesRestored){result.passed=false;result.error+="QA display preferences were not restored.";}
   result.productionPreferencesUnchanged=true;
   foreach(string key in stringPrefs)if(!guarded.ContainsKey(key) || guarded[key]!=Preference(key,0))result.productionPreferencesUnchanged=false;
   foreach(string key in intPrefs)if(!guarded.ContainsKey(key) || guarded[key]!=Preference(key,1))result.productionPreferencesUnchanged=false;
   foreach(string key in floatPrefs)if(!guarded.ContainsKey(key) || guarded[key]!=Preference(key,2))result.productionPreferencesUnchanged=false;
   if(!result.productionPreferencesUnchanged){result.passed=false;result.error+="Production save/preferences changed during QA.";}
   if(result.error.Length>0)result.passed=false;
   if(folder!=null)File.WriteAllText(Path.Combine(folder,"results.json"),JsonUtility.ToJson(result,true));
   Debug.Log("DEVELOPMENT080_QA_COMPLETE "+result.passed+" assertions="+result.assertions+" frames="+result.frames.Count);
   Application.Quit(result.passed ? 0 : 1);
  }
 }
}