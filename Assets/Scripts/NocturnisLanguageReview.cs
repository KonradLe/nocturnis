using System;using System.IO;using System.Collections;using System.Collections.Generic;using System.Reflection;using UnityEngine;
namespace Krypta {
 // Opt-in captures and first-launch checks; never enabled in normal play.
 public sealed class NocturnisLanguageReview:MonoBehaviour {
  [Serializable]public sealed class TextRow {public string source,english;}
  [Serializable]sealed class TextReport {public List<TextRow> rows=new List<TextRow>();}
  static readonly Dictionary<string,string> texts=new Dictionary<string,string>();
  static bool Requested=>Array.IndexOf(Environment.GetCommandLineArgs(),"-language-review")>=0;
  public static void Record(string source,string english){if(texts.Count<12000)texts[source]=english;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Isolate(){texts.Clear();if(Requested)KryptaGame.Testing=true;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Launch(){
   if(Requested)new GameObject("Language review").AddComponent<NocturnisLanguageReview>();
   if(KryptaGame.Testing)Application.quitting+=SaveTexts;
  }
  static void SaveTexts(){
   var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-language-text-output");if(i<0||i+1>=args.Length)return;
   var report=new TextReport();foreach(var entry in texts)report.rows.Add(new TextRow{source=entry.Key,english=entry.Value});
   File.WriteAllText(args[i+1],JsonUtility.ToJson(report,true));
  }
  const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  string folder,error="";bool done;float started;int frames;KryptaGame game;KryptaHUD hud;
  void Call(string method,params object[] args){typeof(KryptaHUD).GetMethod(method,Private).Invoke(hud,args);}
  void Page(string name){var f=typeof(KryptaHUD).GetField("menuPage",Private);f.SetValue(hud,Enum.Parse(f.FieldType,name));}
  void Set(string field,object value){typeof(KryptaHUD).GetField(field,Private).SetValue(hud,value);}
  static void Check(bool value,string message){if(!value)throw new Exception(message);}
  void Log(string m,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)error=m+"\n"+stack;}
  IEnumerator Shot(string name){
   for(int i=0;i<6;i++)yield return new WaitForEndOfFrame();
   var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(folder,name+".png"),texture.EncodeToPNG());Destroy(texture);frames++;
  }
  IEnumerator Start(){
   var args=Environment.GetCommandLineArgs();folder=args[Array.IndexOf(args,"-language-output")+1];Directory.CreateDirectory(folder);
   started=Time.realtimeSinceStartup;Application.logMessageReceived+=Log;
   while(!(game=FindFirstObjectByType<KryptaGame>())||!game.View)yield return null;
   hud=game.GetComponent<KryptaHUD>();game.enabled=false;Time.timeScale=0;
   Check(NocturnisLanguage.NeedsChoice,"First-run chooser should be visible before intro");
   yield return Shot("01-language-choice");
   Call("ChooseStartupLanguage","en");Check(NocturnisLanguage.English&&!NocturnisLanguage.NeedsChoice,"Choose English");
   Check(PlayerPrefs.GetString(NocturnisLanguage.PreferenceKey)=="en","English preference persisted");
   yield return Shot("02-main-en");
   for(int hero=0;hero<5;hero++){hud.ReviewMotionHero(hero);yield return Shot("03-character-en-"+hero);}
   Page("Settings");Set("settingsTab",3);yield return Shot("04-language-settings-en");
   NocturnisLanguage.Select("pl");Check(!NocturnisLanguage.English,"Live Polish selection");
   yield return Shot("05-language-settings-pl");
   Page("Main");yield return Shot("06-main-pl");
   NocturnisLanguage.Select("en");Page("Controls");yield return Shot("07-controls-en");
   Page("Settings");for(int tab=0;tab<3;tab++){Set("settingsTab",tab);yield return Shot("08-settings-en-"+tab);}
   Page("Credits");yield return Shot("09-credits-en");
   Check(error.Length==0,error);done=true;Application.logMessageReceived-=Log;
   File.WriteAllText(Path.Combine(folder,"result.json"),"{\"passed\":true,\"screenshots\":"+frames+",\"languageSwitch\":true,\"choicePersisted\":true}");
   Application.Quit(0);
  }
  void Update(){if(done||folder==null)return;if(error.Length==0&&Time.realtimeSinceStartup-started<240)return;done=true;StopAllCoroutines();File.WriteAllText(Path.Combine(folder,"failure.txt"),error.Length>0?error:"Timeout");Application.Quit(1);}
 }
}
