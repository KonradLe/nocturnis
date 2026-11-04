using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Krypta.Editor
{
 // Run with -executeMethod Krypta.Editor.NocturnisPortrait082QA.Run -portrait082-editor.
 // This is a PlayMode fixture; no IMGUI drawing method is called by the test.
 public static class NocturnisPortrait082QA
 {
  const string Session="Nocturnis.Portrait082",SnapshotKey=Session+".Preferences";
  const string CheckpointKey="Nocturnis.QA.Profile3.Checkpoint";
  const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
  const BindingFlags S=BindingFlags.Static|BindingFlags.NonPublic;
  static int stage,checks;
  static double start;
  static string error="";
  static bool finishing;
  static readonly List<string> evidence=new List<string>();
  [Serializable]sealed class Report
  {public bool passed;public int assertions;public string error;public string[] evidence;}
  [Serializable]sealed class Preference
  {public string key,value;public bool exists,isInt;public int number;}
  [Serializable]sealed class Preferences
  {public List<Preference> entries=new List<Preference>();}
  [Serializable]sealed class SavedInventory
  {public List<KryptaGame.Relic> inventory;}
  [Serializable]sealed class Bag
  {public List<KryptaGame.Relic> items;}

  [InitializeOnLoadMethod]
  static void Restore()
  {
   if(!SessionState.GetBool(Session,false))return;
   KryptaGame.Testing=true;
   Attach();
  }
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  static void BeforeScene()
  {
   if(SessionState.GetBool(Session,false) ||
      Array.IndexOf(Environment.GetCommandLineArgs(),"-portrait082-editor")>=0)
    KryptaGame.Testing=true;
  }
  public static void Run()
  {
   KryptaGame.Testing=true;stage=checks=0;error="";finishing=false;evidence.Clear();
   CapturePreferences();
   PlayerPrefs.SetInt("Nocturnis.QA.ActiveProfile",3);
   PlayerPrefs.SetInt("Nocturnis.QA.ProfileMigrationDone",1);PlayerPrefs.Save();
   SessionState.SetBool(Session,true);
   Attach();
   EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");
   EditorApplication.isPlaying=true;
  }
  static void Attach()
  {
   start=EditorApplication.timeSinceStartup;
   EditorApplication.update-=Tick;EditorApplication.update+=Tick;
   Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
  }
  static void Log(string message,string stack,LogType type)
  {if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)error=message+"\n"+stack;}
  static void Check(bool condition,string label)
  {checks++;if(!condition)throw new Exception(label);}
  static object Call(object target,string name,params object[] args)
  {
   var method=target.GetType().GetMethod(name,P);
   if(method==null)throw new MissingMethodException(target.GetType().Name,name);
   return method.Invoke(target,args);
  }
  static object Field(object target,string name)
  {return target.GetType().GetField(name,P).GetValue(target);}
  static void Field(object target,string name,object value)
  {target.GetType().GetField(name,P).SetValue(target,value);}
  static void Set(KryptaGame game,string name,object value)
  {typeof(KryptaGame).GetProperty(name).SetValue(game,value);}
  static string Relic(KryptaGame.Relic item){return JsonUtility.ToJson(item);}
  static string Inventory(KryptaGame game){return JsonUtility.ToJson(new Bag{items=game.Inventory});}
  static string Gear(KryptaGame game)
  {return Relic(game.EquippedRelic(0))+Relic(game.EquippedRelic(1))+Relic(game.EquippedRelic(2));}

  static void Tick()
  {
   if(finishing)return;
   if(EditorApplication.timeSinceStartup-start>300){Finish(false,"Timeout waiting for PORTRAIT082 fixture");return;}
   if(!EditorApplication.isPlaying)return;
   var game=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();
   if(!game || !game.View)return;
   var hud=game.GetComponent<KryptaHUD>();
   if(!hud || Field(hud,"game")==null)return;
   try
   {
    if(error.Length>0)throw new Exception(error);
    Check(KryptaGame.Testing,"QA namespace must be enabled before the fixture");
    Check(game.ActiveProfile==3,"QA must use its isolated third profile");
    game.enabled=false;hud.enabled=false;Time.timeScale=0;
    switch(stage)
    {
     case 0: Hero(game,hud,0);break;
     case 1: Hero(game,hud,1);break;
     case 2: Hero(game,hud,2);break;
     case 3: Hero(game,hud,3);break;
     case 4: Hero(game,hud,4);break;
     case 5: NocturnisWeapon082DirectionQA.Verify(Check);evidence.Add("Independent cutting-edge trajectory, palm attachment, bow/staff/scythe facing and adverse Update order checks passed.");break;
     default:Finish(true,"");return;
    }
    stage++;
   }
   catch(Exception exception){Finish(false,exception.ToString());}
  }

  static Transform Find(Transform root,string name)
  {foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name==name)return t;return null;}
  static void Hero(KryptaGame game,KryptaHUD hud,int hero)
  {
   Set(game,"State",KryptaGame.Phase.Title);
   var actor=hud.ReviewMotionHero(hero);
   Check(actor!=null,"Actual hero preview created: "+hero);
   var camera=(Camera)Field(hud,"portraitCamera");
   // Geometry-only batch QA has no graphics device. Native render evidence comes from the Windows player.
   camera.enabled=false;
   var fitting=actor.Root.GetComponentInChildren<NocturnisWeaponGrip>();
   Check(fitting!=null,"Equipped grip exists for hero "+hero);
   var animation=actor.Root.GetComponentInChildren<Animation>();
   Check(animation && animation["Idle"],"Actual idle animation exists");
   Vector3 cameraPosition=camera.transform.position;Quaternion cameraRotation=camera.transform.rotation;
   float minimum=1,maximumSkinningError=0;int tested=0;var scratch=new Mesh();
   try
   {
    // These interpolation times differ from the 24 key times used by Fit.
    for(int pose=0;pose<8;pose++)
    {
     actor.Root.transform.rotation=Quaternion.identity;
     actor.PreviewIdle(animation["Idle"].length*(pose+.37f)/8);
     var points=new List<Vector3>();
     foreach(var renderer in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
     {
      if(!renderer.enabled || !renderer.sharedMesh || !renderer.gameObject.activeInHierarchy)continue;
      renderer.BakeMesh(scratch,true);
      if(pose==0)maximumSkinningError=Mathf.Max(maximumSkinningError,VerifySkinnedPose(renderer,scratch.vertices));
      foreach(Vector3 v in scratch.vertices)points.Add(actor.Root.transform.InverseTransformPoint(renderer.transform.TransformPoint(v)));
     }
     foreach(var filter in actor.Root.GetComponentsInChildren<MeshFilter>())
     {
      var renderer=filter.GetComponent<MeshRenderer>();
      if(!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy || !filter.sharedMesh)continue;
      foreach(Vector3 v in filter.sharedMesh.vertices)points.Add(actor.Root.transform.InverseTransformPoint(filter.transform.TransformPoint(v)));
     }
     Check(points.Count>1000,"Hero and weapon have actual mesh geometry");
     for(int turn=0;turn<24;turn++)
     {
      Quaternion yaw=Quaternion.Euler(0,turn*15+3.7f,0);
      float margin=1,depthMin=float.MaxValue,depthMax=0;
      foreach(Vector3 point in points)
      {
       Vector3 v=camera.WorldToViewportPoint(actor.Root.transform.position+yaw*point);
       margin=Mathf.Min(margin,Mathf.Min(Mathf.Min(v.x,1-v.x),Mathf.Min(v.y,1-v.y)));
       depthMin=Mathf.Min(depthMin,v.z);depthMax=Mathf.Max(depthMax,v.z);
      }
      Check(margin>.025f && depthMin>camera.nearClipPlane && depthMax<camera.farClipPlane,
       "Full hero and weapon stay in frame: hero="+hero+" pose="+pose+" yaw="+(turn*15+3.7f)+" margin="+margin);
      minimum=Mathf.Min(minimum,margin);tested++;
     }
    }
   }
   finally{UnityEngine.Object.Destroy(scratch);}
   actor.Root.transform.rotation=Quaternion.Euler(0,8,0);actor.PreviewIdle(.15f);
   var chest=Find(actor.Root.transform,"chest");Check(chest,"Chest bone exists");
   Quaternion idleA=chest.localRotation;actor.PreviewIdle(.75f);
   Check(Quaternion.Angle(idleA,chest.localRotation)>.1f,"Paused preview retains skeletal idle");
   Check(Vector3.Distance(cameraPosition,camera.transform.position)<.0001f && Quaternion.Angle(cameraRotation,camera.transform.rotation)<.001f,"Camera does not pump while character rotates or breathes");
   // Contact stays attached and never drifts when the same pose is sampled repeatedly.
   var hand=Find(actor.Root.transform,"handR");Check(hand,"Right hand exists");

   Quaternion localWeapon=Quaternion.Inverse(actor.Root.transform.rotation)*fitting.transform.rotation;
   Vector3 localGrip=actor.Root.transform.InverseTransformPoint(fitting.GripCenter);
   foreach(float yaw in new[]{61f,151f,241f,331f})
   {
    actor.Root.transform.rotation=Quaternion.Euler(0,yaw,0);actor.PreviewIdle(.75f);
    Check(Quaternion.Angle(localWeapon,Quaternion.Inverse(actor.Root.transform.rotation)*fitting.transform.rotation)<.05f &&
      Vector3.Distance(localGrip,actor.Root.transform.InverseTransformPoint(fitting.GripCenter))<.001f,
      "Actual resampled grip rotates with hero, not world axes, at yaw "+yaw);
   }
   Vector3 hold=hand.InverseTransformPoint(fitting.GripCenter);
   Quaternion weapon=fitting.transform.rotation;
   for(int i=0;i<10;i++)actor.PreviewIdle(.75f);
   Check(Vector3.Distance(hold,hand.InverseTransformPoint(fitting.GripCenter))<.0001f &&
    Quaternion.Angle(weapon,fitting.transform.rotation)<.05f,"Repeated idle sampling cannot accumulate wrist or weapon rotation");
   evidence.Add("Hero "+hero+": "+tested+" complete geometry views, minimum viewport margin "+minimum.ToString("F4")+", stable grip and skeletal idle. Independent bone skinning maximum error "+maximumSkinningError.ToString("F6")+" m.");
  }
  static float VerifySkinnedPose(SkinnedMeshRenderer renderer,Vector3[] measured)
  {
   // The oracle is the skinning equation, not another BakeMesh call. This catches
   // double-applied renderer scale even when Fit and its viewport test agree.
   var mesh=renderer.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
   var bind=mesh.bindposes;var bones=renderer.bones;
   Check(vertices.Length==measured.Length && weights.Length==vertices.Length && bind.Length==bones.Length,"Skinning fixture has complete vertices, weights and bind poses");
   for(int i=0;i<mesh.blendShapeCount;i++)Check(Mathf.Abs(renderer.GetBlendShapeWeight(i))<.0001f,"Independent skinning fixture uses no active blend shapes");
   var matrices=new Matrix4x4[bones.Length];
   for(int i=0;i<bones.Length;i++)matrices[i]=bones[i].localToWorldMatrix*bind[i];
   float maximum=0;
   for(int i=0;i<vertices.Length;i++)
   {
    var v=vertices[i];var w=weights[i];
    Vector3 expected=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+
     matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+
     matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+
     matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
    maximum=Mathf.Max(maximum,Vector3.Distance(expected,renderer.transform.TransformPoint(measured[i])));
   }
   Check(maximum<.001f,"Portrait measurement agrees with independent bone skinning within 1 mm: "+renderer.name+" error="+maximum);
   return maximum;
  }
  static void CapturePreferences()
  {
   var snapshot=new Preferences();
   foreach(string prefix in new[]{"Nocturnis.QA","Nocturnis"})
   {
    foreach(string tail in new[]{".ActiveProfile",".ProfileMigrationDone"})
    {
     string key=prefix+tail;
     snapshot.entries.Add(new Preference{key=key,isInt=true,exists=PlayerPrefs.HasKey(key),number=PlayerPrefs.GetInt(key,0)});
    }
    foreach(string tail in new[]{".Checkpoint",".Profile1.Checkpoint",".Profile2.Checkpoint",".Profile3.Checkpoint"})
     foreach(string suffix in new[]{"",".Backup.v1",".Backup.v2",".Backup.v3",".Backup.v4"})
     {
      string key=prefix+tail+suffix;
      snapshot.entries.Add(new Preference{key=key,exists=PlayerPrefs.HasKey(key),value=PlayerPrefs.GetString(key,"")});
     }
   }
   SessionState.SetString(SnapshotKey,JsonUtility.ToJson(snapshot));
  }
  static void RestorePreferences()
  {
   string raw=SessionState.GetString(SnapshotKey,"");
   if(string.IsNullOrEmpty(raw))throw new Exception("QA preference snapshot is missing");
   var snapshot=JsonUtility.FromJson<Preferences>(raw);
   bool productionUnchanged=true;
   foreach(var p in snapshot.entries)
   {
    if(!p.key.StartsWith("Nocturnis.QA.",StringComparison.Ordinal))
    {
     productionUnchanged&=PlayerPrefs.HasKey(p.key)==p.exists &&
      (p.isInt ? PlayerPrefs.GetInt(p.key,0)==p.number : PlayerPrefs.GetString(p.key,"")==p.value);
     continue;
    }
    if(!p.exists)PlayerPrefs.DeleteKey(p.key);
    else if(p.isInt)PlayerPrefs.SetInt(p.key,p.number);
    else PlayerPrefs.SetString(p.key,p.value);
   }
   PlayerPrefs.Save();SessionState.EraseString(SnapshotKey);
   Check(productionUnchanged,"Production save keys and profile selection must remain unchanged");
   evidence.Add("Production save keys unchanged; prior QA checkpoints, backups and profile preferences restored.");
  }
  static void Finish(bool passed,string message)
  {
   if(finishing)return;finishing=true;
   SessionState.SetBool(Session,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
   try{RestorePreferences();}
   catch(Exception exception){passed=false;message+="\n"+exception;}
   Directory.CreateDirectory("Logs");
   File.WriteAllText("Logs/portrait082-qa.json",JsonUtility.ToJson(new Report{passed=passed,assertions=checks,error=message,evidence=evidence.ToArray()},true));
   if(!passed)Debug.LogError(message);
   Time.timeScale=1;EditorApplication.Exit(passed?0:1);
  }
 }
}
