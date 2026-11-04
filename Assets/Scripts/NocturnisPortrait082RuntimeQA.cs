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
 public sealed class NocturnisPortrait082RuntimeQA:MonoBehaviour
 {
  [Serializable]sealed class Capture {public string screen,image,hero;public int heroIndex,width,height,bytes,sampledBrightnessRange,portraitBrightnessRange;}
  [Serializable]sealed class Report
  {
   public bool passed,productionPreferencesUnchanged,testingNamespace,qaPreferencesRestored;
   public int assertions,expectedFrames;public string error="",scope="Actual Windows player character previews and native mouse events at 1280x720 and 960x540. All five weapons and paused idle; this is not a campaign playthrough.";
   public List<string> checks=new List<string>();public List<Capture> frames=new List<Capture>();
  }
  sealed class Shot {public string screen;public int hero,width,height;public Shot(string s,int h,int w=1280,int z=720){screen=s;hero=h;width=w;height=z;}}
  const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static readonly Dictionary<string,string> guarded=new Dictionary<string,string>(),qaStrings=new Dictionary<string,string>();
  static readonly Dictionary<string,int?> qaInts=new Dictionary<string,int?>();
  static readonly string[] stringPrefs={"Nocturnis.Profile1.Checkpoint","Nocturnis.Profile2.Checkpoint","Nocturnis.Profile3.Checkpoint","Nocturnis.Checkpoint.v1","Nocturnis.Checkpoint.v2","Nocturnis.SharedStash.v1"};
  static readonly string[] intPrefs={"Nocturnis.ActiveProfile","Nocturnis.ProfileMigrationDone","Nocturnis.Muted","Nocturnis.LargeText","Nocturnis.CameraShake","Nocturnis.MapExploration","Nocturnis.ReducedFlashes","Nocturnis.TutorialEnabled","Nocturnis.FullScreen","Nocturnis.Settings.Quality","Nocturnis.Settings.VSync","Nocturnis.Settings.FrameCap","Nocturnis.Settings.DamageNumbers","Nocturnis.Settings.Width","Nocturnis.Settings.Height","Nocturnis.Settings.DisplayMode"};
  static readonly string[] floatPrefs={"Nocturnis.EffectsVolume","Nocturnis.AmbientVolume","Nocturnis.MusicVolume","Nocturnis.Settings.Voice"};

  static bool Requested {get{return Array.IndexOf(Environment.GetCommandLineArgs(),"-portrait082-review")>=0;}}
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
  {if(Requested)new GameObject("RPG 081 runtime review").AddComponent<NocturnisPortrait082RuntimeQA>();}
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
   var args=Environment.GetCommandLineArgs();int output=Array.IndexOf(args,"-portrait082-output");
   if(output<0 || output+1>=args.Length){result.error="Required -portrait082-output directory missing";Finish();yield break;}
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

  [Serializable]sealed class InputRequest
  {public int seq,width,height;public string action;public int[] from,to;}
  [Serializable]sealed class InputResponse {public int seq;public bool passed;public string error;}
  int requestSequence;
  IEnumerator Native(string action,Vector2 from,Vector2 to)
  {
   var request=new InputRequest{seq=++requestSequence,width=Screen.width,height=Screen.height,action=action,
    from=new[]{Mathf.RoundToInt(from.x),Mathf.RoundToInt(from.y)},to=new[]{Mathf.RoundToInt(to.x),Mathf.RoundToInt(to.y)}};
   string tmp=Path.Combine(folder,"input-request.tmp"),dest=Path.Combine(folder,"input-request.json");
   File.WriteAllText(tmp,JsonUtility.ToJson(request));
   if(File.Exists(dest))File.Delete(dest);File.Move(tmp,dest);
   float deadline=Time.realtimeSinceStartup+18;
   InputResponse response=null;
   while(Time.realtimeSinceStartup<deadline)
   {
    string path=Path.Combine(folder,"input-response.json");
    if(File.Exists(path))
    {try{response=JsonUtility.FromJson<InputResponse>(File.ReadAllText(path));}catch(IOException){}catch(ArgumentException){}}
    if(response!=null && response.seq==request.seq)break;
    yield return null;
   }
   Check(response!=null && response.seq==request.seq && response.passed,"Native "+action+" dispatch seq="+request.seq+" "+(response!=null?response.error:"timeout"));
   for(int i=0;i<5;i++)yield return new WaitForEndOfFrame();
  }
  static Transform Find(Transform root,string name)
  {foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name==name)return t;return null;}
  IEnumerator Review()
  {
   while(!(game=FindFirstObjectByType<KryptaGame>()) || !game.View || !(hud=game.GetComponent<KryptaHUD>()))yield return null;
   yield return null;game.enabled=false;Time.timeScale=0;hud.enabled=true;
   Check(KryptaGame.Testing && !NocturnisCinematic.BlocksInput,"QA namespace active before game startup");result.testingNamespace=true;
   result.expectedFrames=17;
   Screen.SetResolution(1280,720,FullScreenMode.Windowed);
   for(int i=0;i<10;i++)yield return new WaitForEndOfFrame();
   Check(Screen.width==1280 && Screen.height==720,"Native 1280x720 window applied");
   Set(game,"State",KryptaGame.Phase.Title);
   for(int hero=0;hero<5;hero++)
   {
    configuredHero=hero;var actor=hud.ReviewMotionHero(hero);
    for(int i=0;i<6;i++)yield return new WaitForEndOfFrame();
    Check(Mathf.Abs(Mathf.DeltaAngle(hud.PortraitYaw,8))<.05f && !hud.PortraitDragging,"Class "+hero+" starts front-facing with no stale mouse capture");
    var chest=Find(actor.Root.transform,"chest");Check(chest,"Skeletal chest bone exists");
    Quaternion chestStart=chest.localRotation;
    yield return new WaitForSecondsRealtime(.45f);
    Check(Quaternion.Angle(chestStart,chest.localRotation)>.1f,"Idle continues while menu time is paused for hero "+hero);
    var grip=actor.Root.GetComponentInChildren<NocturnisWeaponGrip>();
    Check(grip && Find(actor.Root.transform,"handR"),"Equipped weapon and hand exist for hero "+hero);
    if(hero==0)yield return Native("move",new Vector2(615,350),new Vector2(615,350));
    CaptureFrame(new Shot("front",hero));
    Vector3 cameraPos=((Camera)Get(hud,"portraitCamera")).transform.position;
    float before=hud.PortraitYaw;
    yield return Native("drag",new Vector2(615,350),new Vector2(735,350));
    Check(Mathf.Abs(Mathf.DeltaAngle(before+78,hud.PortraitYaw))<3,"Real LMB drag turns hero by expected amount: "+hero+" yaw="+hud.PortraitYaw);
    Check(!hud.PortraitDragging && game.State==KryptaGame.Phase.Title && game.SelectedHero==hero,"Drag releases without starting game or selecting a different hero");
    Check(Quaternion.Angle(actor.Root.transform.rotation,Quaternion.Euler(0,hud.PortraitYaw,0))<.05f,"Visible root follows user yaw");
    CaptureFrame(new Shot("quarter",hero));
    yield return Native("drag",new Vector2(615,350),new Vector2(815,350));
    Check(Mathf.Abs(Mathf.DeltaAngle(before+208,hud.PortraitYaw))<4,"Second drag accumulates rotation across the rear view");
    CaptureFrame(new Shot("back",hero));
    Check(Vector3.Distance(cameraPos,((Camera)Get(hud,"portraitCamera")).transform.position)<.0001f,"Turning does not move or zoom portrait camera");
   }
   // Release outside the preview, then move without a held button: no stuck drag.
   yield return Native("drag",new Vector2(615,350),new Vector2(1110,350));
   Check(!hud.PortraitDragging,"MouseUp outside the portrait releases capture");
   float released=hud.PortraitYaw;
   yield return Native("move",new Vector2(1110,350),new Vector2(615,350));
   Check(Mathf.Abs(Mathf.DeltaAngle(released,hud.PortraitYaw))<.05f,"Mouse move after release cannot continue rotation");

   yield return Native("drag",new Vector2(615,350),new Vector2(637,675));
   Check(game.State==KryptaGame.Phase.Title && Get(hud,"menuConfirmation").ToString()=="None" &&
    (float)Get(hud,"gateAt")<0 && !hud.PortraitDragging,"Release over Begin Journey cannot activate its button or confirmation");
   yield return Native("click",new Vector2(180,218),new Vector2(180,218));
   Check(game.SelectedHero==0 && Mathf.Abs(Mathf.DeltaAngle(hud.PortraitYaw,8))<.05f,"Real class button click works after drag and resets preview");
   // Hold actual LMB across frames so lifecycle cleanup is tested with live capture.
   yield return Native("press",new Vector2(615,350),new Vector2(615,350));
   yield return new WaitForSecondsRealtime(.1f);
   Check(hud.PortraitDragging && hud.PortraitControlId!=0,"Held LMB owns portrait capture before HUD disable");
   hud.enabled=false;
   Check(!hud.PortraitDragging && hud.PortraitControlId==0,"Disabling HUD releases active portrait capture immediately");
   hud.enabled=true;
   yield return Native("release",new Vector2(615,350),new Vector2(615,350));
   Check(!hud.PortraitDragging && hud.PortraitControlId==0,"Re-enabled HUD remains released after native MouseUp");

   yield return Native("press",new Vector2(615,350),new Vector2(615,350));
   yield return new WaitForSecondsRealtime(.1f);
   Check(hud.PortraitDragging && hud.PortraitControlId!=0,"Held LMB owns portrait capture before focus loss");
   Call(hud,"OnApplicationFocus",false);
   Check(!hud.PortraitDragging && hud.PortraitControlId==0,"Focus-loss callback releases active portrait capture immediately");
   yield return Native("release",new Vector2(615,350),new Vector2(615,350));
   Check(!hud.PortraitDragging && hud.PortraitControlId==0,"Focus-loss cleanup stays released after native MouseUp");
   // Scaling regression: 90 physical pixels at .75 UI scale equal 120 GUI pixels.
   Screen.SetResolution(960,540,FullScreenMode.Windowed);
   for(int i=0;i<12;i++)yield return new WaitForEndOfFrame();
   Check(Screen.width==960 && Screen.height==540,"Native 960x540 window applied");
   configuredHero=4;hud.ReviewMotionHero(4);
   for(int i=0;i<5;i++)yield return new WaitForEndOfFrame();
   CaptureFrame(new Shot("small-front",4,960,540));
   yield return Native("drag",new Vector2(461,263),new Vector2(551,263));
   Check(Mathf.Abs(Mathf.DeltaAngle(86,hud.PortraitYaw))<3 && !hud.PortraitDragging,"Scaled portrait drag preserves sensitivity and releases");
   CaptureFrame(new Shot("small-quarter",4,960,540));
   float stationary=hud.PortraitYaw;
   yield return Native("drag",new Vector2(700,280),new Vector2(800,280));
   Check(Mathf.Abs(Mathf.DeltaAngle(stationary,hud.PortraitYaw))<.05f,"Dragging the lore panel cannot rotate the character");
   yield return Native("click",new Vector2(100,505),new Vector2(100,505));
   Check(Get(hud,"menuPage").ToString()=="Main" && !hud.PortraitDragging,"Back button remains clickable after portrait interaction");
   foreach(var capture in result.frames)Check(File.Exists(Path.Combine(folder,capture.image)),"Screenshot exists: "+capture.image);
  }
  void CaptureFrame(Shot shot)
  {
   var camera=(Camera)Get(hud,"portraitCamera");var portrait=(RenderTexture)Get(hud,"portrait");
   Check(camera && camera.enabled && portrait && portrait.IsCreated(),"Actual portrait camera is rendering");
   camera.Render();
   RenderTexture previous=RenderTexture.active;RenderTexture.active=portrait;
   var pixels=new Texture2D(portrait.width,portrait.height,TextureFormat.RGBA32,false);
   pixels.ReadPixels(new Rect(0,0,portrait.width,portrait.height),0,0);pixels.Apply();RenderTexture.active=previous;
   int visible=0;bool border=true;var colors=pixels.GetPixels32();
   for(int y=0;y<portrait.height;y++)for(int x=0;x<portrait.width;x++)
   {if(colors[y*portrait.width+x].a<32)continue;visible++;if(x<8 || y<8 || x>=portrait.width-8 || y>=portrait.height-8)border=false;}
   Destroy(pixels);
   Check(visible>10000 && border,"Rendered body and weapon are visible with transparent border: "+shot.screen+" hero "+shot.hero);
   var texture=ScreenCapture.CaptureScreenshotAsTexture();
   try
   {
    Check(texture && texture.width==shot.width && texture.height==shot.height,"Rendered screen dimensions match");
    byte[] bytes=texture.EncodeToPNG();Check(bytes.Length>30000,"Screenshot contains real image content");
    string name=shot.screen+"-hero-"+shot.hero+"-"+shot.width+"x"+shot.height+".png";
    File.WriteAllBytes(Path.Combine(folder,name),bytes);
    result.frames.Add(new Capture{screen=shot.screen,image=name,hero=game.Hero.person,heroIndex=shot.hero,width=shot.width,height=shot.height,bytes=bytes.Length});
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
   Debug.Log("PORTRAIT082_QA_COMPLETE "+result.passed+" assertions="+result.assertions+" frames="+result.frames.Count);
   Application.Quit(result.passed ? 0 : 1);
  }
 }
}
