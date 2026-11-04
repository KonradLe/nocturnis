using System;using System.IO;using System.Collections.Generic;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
namespace Krypta.Editor {
 public static class NocturnisMasonryQA {
  const string Session="Nocturnis.Masonry083";
  static int zone;static double start;static string error="";static List<KryptaGame.MasonryResult> results=new List<KryptaGame.MasonryResult>();
  [Serializable]sealed class Report{public bool passed;public string error;public List<KryptaGame.MasonryResult> zones;}
  [InitializeOnLoadMethod]static void Restore(){if(!SessionState.GetBool(Session,false))return;KryptaGame.Testing=true;start=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
  public static void Run(){KryptaGame.Testing=true;EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool(Session,true);EditorApplication.isPlaying=true;}
  static void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)error=m+"\n"+s;}
  static void Tick() {
   if(EditorApplication.timeSinceStartup-start>240){Finish(false,"Timeout");return;}
   if(!EditorApplication.isPlaying)return;var g=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!g||!g.View)return;
   try {
    if(error.Length>0)throw new Exception(error);g.enabled=false;
    while(zone<19&&!Campaign.Underground(zone))zone++;
    if(zone>=19){Finish(true,"");return;}
    g.MasonryFixture(zone);Time.timeScale=0;var row=g.MasonryInspect();results.Add(row);if(!row.passed)throw new Exception("Zone "+zone+": "+row.error);zone++;
   }catch(Exception e){Finish(false,e.ToString());}
  }
  static void Finish(bool passed,string message) {
   EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SessionState.SetBool(Session,false);
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/masonry-qa.json",JsonUtility.ToJson(new Report{passed=passed,error=message,zones=results},true));
   EditorApplication.Exit(passed?0:1);
  }
 }
}
