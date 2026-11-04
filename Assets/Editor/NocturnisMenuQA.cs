using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Krypta.Editor
{
 // Complements actual mouse/keyboard checks in the isolated standalone player.
 public static class NocturnisMenuQA
 {
  const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
  static int checks;
  static double started;
  static bool errors;
  [InitializeOnLoadMethod]static void Restore()
  {
   if(!SessionState.GetBool("Nocturnis.MenuQA",false))return;
   started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
  }
  public static void Run()
  {
   EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");
   SessionState.SetBool("Nocturnis.MenuQA",true);EditorApplication.isPlaying=true;
  }
  static void Log(string message,string stack,LogType type){if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert)errors=true;}
  static void Check(bool pass,string message){checks++;if(!pass)throw new Exception(message);}
  static object Field(KryptaHUD hud,string field){return typeof(KryptaHUD).GetField(field,Private).GetValue(hud);}
  static void Set(KryptaHUD hud,string field,string value)
  {var info=typeof(KryptaHUD).GetField(field,Private);info.SetValue(hud,Enum.Parse(info.FieldType,value));}
  static void Call(KryptaHUD hud,string method){typeof(KryptaHUD).GetMethod(method,Private).Invoke(hud,null);}
  static void Tick()
  {
   if(!EditorApplication.isPlaying)return;
   if(EditorApplication.timeSinceStartup-started>90){Finish(false,"Timeout");return;}
   var game=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();
   if(!game || !game.View)return;
   var hud=game.GetComponent<KryptaHUD>();if(!hud)return;
   try
   {
    KryptaGame.Testing=true;
    foreach(string resource in new[]{"MenuCathedral","GothicHUD","ClassicPanel","Grimoire","SkillAtlas"})Check(Resources.Load<Texture2D>("UI/"+resource),"Gothic UI texture "+resource);
    Check(!KryptaHUD.BeltContains(new Vector2(640,520)),"World above the compact belt receives clicks");
    for(int slot=0;slot<6;slot++)Check(KryptaHUD.BeltContains(KryptaHUD.BeltSkillBounds(slot).center),"Compact HUD skill hitbox "+slot);
    Check(Field(hud,"menuPage").ToString()=="Main","Title opens at main menu");
    Check(hud.HandleMenuInput(),"Title consumes gameplay shortcuts");
    for(int i=0;i<5;i++){game.SelectHero(i);Check(game.SelectedHero==i,"Class selection "+i);}
    Set(hud,"menuPage","Characters");Call(hud,"BackFromMenu");
    Check(Field(hud,"menuPage").ToString()=="Main" && game.State==KryptaGame.Phase.Title,"Class back leaves game stopped");
    Set(hud,"menuPage","Characters");Set(hud,"menuConfirmation","NewJourney");Call(hud,"BackFromMenu");
    Check(Field(hud,"menuConfirmation").ToString()=="None" && Field(hud,"menuPage").ToString()=="Characters","Cancel new journey keeps selected class");
    Check((float)Field(hud,"gateAt")<0,"Cancelled new journey never starts transition");
    game.Begin();Check(game.State==KryptaGame.Phase.Playing && KryptaGame.HasCheckpoint,"New journey starts and saves");
    Set(hud,"menuPage","Main");Check(!hud.HandleMenuInput(),"Normal gameplay receives input");
    game.Pause();Check(Time.timeScale==0,"Pause freezes the world");
    foreach(string screen in new[]{"Settings","Controls","Credits"})
    {
     Set(hud,"menuPage",screen);Set(hud,"returnPage","Main");
     Check(hud.HandleMenuInput(),screen+" prevents gameplay input");
     Call(hud,"BackFromMenu");
     Check(game.State==KryptaGame.Phase.Paused && Time.timeScale==0 && Field(hud,"menuPage").ToString()=="Main",screen+" returns to pause, not combat");
    }
    Set(hud,"menuConfirmation","MainMenu");Check(hud.HandleMenuInput(),"Exit confirmation blocks shortcuts");
    Call(hud,"BackFromMenu");Check(game.State==KryptaGame.Phase.Paused,"Exit cancellation stays paused");
    game.OpenEquipment();game.OpenJournal();Check(game.JournalOpen && !game.EquipmentOpen && !game.BookOpen,"Journal replaces inventory");
    game.OpenBook();game.OpenJournal();Check(game.JournalOpen && !game.BookOpen,"Journal replaces book");
    game.Resume();Check(!game.JournalOpen && !game.BookOpen && !game.EquipmentOpen && Time.timeScale==1,"Resume clears every game overlay");
    int chosen=game.SelectedHero;Check(game.ContinueCheckpoint() && game.SelectedHero==chosen && game.State==KryptaGame.Phase.Playing,"Continue restores selected class");
    Check(!errors,"No runtime errors during menu state checks");
    Finish(true,checks+" assertions: menu navigation, modal input, five heroes, pause, overlay exclusivity and checkpoint continuation.");
   }
   catch(Exception error){Finish(false,error.ToString());}
  }
  static void Finish(bool pass,string message)
  {
   SessionState.SetBool("Nocturnis.MenuQA",false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/classic-menu-qa-result.txt",(pass ? "PASS: " : "FAIL: ")+message);
   Debug.Log(message);EditorApplication.Exit(pass ? 0 : 1);
  }
 }
}
