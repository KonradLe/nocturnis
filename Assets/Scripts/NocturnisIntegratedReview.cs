using System;
using System.IO;
using System.Reflection;
using System.Collections;
using UnityEngine;

namespace Krypta {
 // Explicit automated review flag; normal player startup and saves are untouched.
 public sealed class NocturnisIntegratedReview : MonoBehaviour {
  static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  string output;string error="";
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){
   var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-integration-review");if(i<0)return;
   KryptaGame.Testing=true;var r=new GameObject("Integration Review").AddComponent<NocturnisIntegratedReview>();r.output=i+1<args.Length?args[i+1]:"Review";Directory.CreateDirectory(r.output);
  }
  void Awake(){Application.logMessageReceived+=Log;}
  void OnDestroy(){Application.logMessageReceived-=Log;}
  void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)error+=message+"\n";}
  static void Call(KryptaGame g,string name,params object[] args){typeof(KryptaGame).GetMethod(name,Private).Invoke(g,args);}
  IEnumerator Start(){
   yield return null;yield return null;var g=FindFirstObjectByType<KryptaGame>();
   yield return new WaitForSecondsRealtime(1);yield return Capture("00-menu");
   for(int hero=0;hero<5;hero++){
    typeof(KryptaGame).GetProperty("State").SetValue(g,KryptaGame.Phase.Title);g.SelectHero(hero);g.Begin();Call(g,"LoadZone",Campaign.Crypt);
    yield return new WaitForSecondsRealtime(.3f);g.CastDefense();yield return new WaitForSecondsRealtime(.15f);yield return Capture("hero-"+hero+"-buff");
   }
   int[] zones={Campaign.Road,Campaign.Town,Campaign.Forest,Campaign.Fields,Campaign.Cemetery,Campaign.Crypt,Campaign.Crypt2,Campaign.Crypt3,Campaign.Kings,Campaign.Heart,Campaign.Cellar};
   for(int i=0;i<zones.Length;i++){
    g.Resume();Call(g,"LoadZone",zones[i]);yield return new WaitForSecondsRealtime(.35f);
    var player=(ActorVisual)typeof(KryptaGame).GetField("player",Private).GetValue(g);
    if(zones[i]==Campaign.Town)player.Root.transform.position=new Vector3(21.6f,0,18);
    if(zones[i]==Campaign.Heart)player.Root.transform.position=new Vector3(21.6f,0,43.2f);
    yield return new WaitForSecondsRealtime(.4f);yield return Capture("zone-"+zones[i]);
    if(zones[i]==Campaign.Heart){var boss=g.enemies.Find(e=>e.boss);boss.hp=boss.maxHp*.49f;boss.alerted=true;Call(g,"UpdateSpecialEnemy",boss);yield return new WaitForSecondsRealtime(.2f);yield return Capture("boss-mutation");}
   }
   File.WriteAllText(Path.Combine(output,"runtime-review.json"),"{\"passed\":"+(error.Length==0?"true":"false")+",\"maps\":11,\"heroes\":5}");File.WriteAllText(Path.Combine(output,"runtime-errors.txt"),error);Application.Quit(error.Length==0?0:1);
  }
  IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));yield return null;}
 }
}
