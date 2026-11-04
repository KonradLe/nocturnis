using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Krypta.Editor {
 public static class NocturnisQualityOfLifeQA {
  const string Session="Nocturnis.QualityOfLifeQA",Output="Logs/quality-of-life-qa.json";
  const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic,StaticHidden=BindingFlags.Static|BindingFlags.NonPublic;
  [Serializable]sealed class ClipResult {public string name;public int samples,channels;public float peak,seamDifference;}
  [Serializable]sealed class Preference {public string key,text;public int number;public bool exists,integer;}
  [Serializable]sealed class Report {public bool passed;public int assertions;public string failure;public List<ClipResult> audio=new List<ClipResult>();public List<Preference> saved=new List<Preference>();}
  static Report report;static double started;static bool running;
  static void Check(bool yes,string message){report.assertions++;if(!yes)throw new Exception(message);}
  static void Remember(string key,bool integer) {
   if(report.saved.Any(p=>p.key==key))return;
   report.saved.Add(new Preference{key=key,integer=integer,exists=PlayerPrefs.HasKey(key),number=integer?PlayerPrefs.GetInt(key):0,text=integer?null:PlayerPrefs.GetString(key)});
  }
  static void RestorePreferences() {
   foreach(var p in report.saved){if(!p.exists)PlayerPrefs.DeleteKey(p.key);else if(p.integer)PlayerPrefs.SetInt(p.key,p.number);else PlayerPrefs.SetString(p.key,p.text);}PlayerPrefs.Save();NocturnisInput.ReloadBindings();
  }
  static void CheckBindings() {
   foreach(string action in NocturnisInput.Actions)Remember("Nocturnis.QA.Key."+action,true);
   NocturnisInput.Reset();Check(NocturnisInput.Key("skill")==KeyCode.Q&&NocturnisInput.Key("interact")==KeyCode.E,"Default bindings");
   Check(NocturnisInput.Bind("skill",KeyCode.E),"Rebind occupied key");
   Check(NocturnisInput.Key("skill")==KeyCode.E&&NocturnisInput.Key("interact")==KeyCode.Q,"Binding conflict swaps both actions");
   Check(NocturnisInput.Format("[Q] / [E]")=="[E] / [Q]","Binding hints must not cascade substitutions");
   string hint="[Q] / [E] / {KEY:skill} / E \u2022",swapped="[E] / [Q] / [E] / Q \u2022";
   var hudHints=(string[])typeof(KryptaHUD).GetField("beltKeyHints",StaticHidden).GetValue(null);
   var navHints=(string[])typeof(KryptaHUD).GetField("beltNavigationHints",StaticHidden).GetValue(null);
   Check(NocturnisInput.Format(hudHints[1])=="E / PPM" && NocturnisInput.Format(hudHints[2])=="R / 1","Actual HUD templates reflect Q/E swap and retain PPM / 1 aliases");
   Check(NocturnisInput.Format("{KEY:skill} + {KEYRAW:skill}")=="[E] + E","Bracketed descriptions and compact HUD tokens keep separate contracts");
   Check(NocturnisInput.Format("{KEYRAW:skill} \u2022 moc / E \u2022 rozmowa")=="E \u2022 moc / Q \u2022 rozmowa","Raw HUD output does not cascade into the legacy interaction hint");
   string hudMemo=NocturnisInput.Format(hudHints[1]);bool hudRepeated=true;
   for(int i=0;i<128;i++)hudRepeated &= ReferenceEquals(hudMemo,NocturnisInput.Format(hudHints[1]));
   Check(hudRepeated,"Compact HUD captions use the same bounded description cache");
   string memo=NocturnisInput.Format(hint);bool repeated=memo==swapped;
   for(int i=0;i<128;i++)repeated &= ReferenceEquals(memo,NocturnisInput.Format(hint));
   Check(repeated,"Repeated descriptions reuse the correct cached Q/E swap");
   string plain="Gold 123";Check(ReferenceEquals(plain,NocturnisInput.Format(plain)),"Plain HUD text needs no formatting allocation");
   Check(NocturnisInput.Bind("skill",KeyCode.C),"Rebind cached skill to C");
   Check(NocturnisInput.Text("skill")=="C" && NocturnisInput.Format(hint)=="[C] / [Q] / [C] / Q \u2022","C invalidates old formatted descriptions and display names");
   Check(NocturnisInput.Format(hudHints[1])=="C / PPM" && NocturnisInput.Format(hudHints[2])=="R / 1","Actual HUD caption updates again after binding C");
   string cMemo=NocturnisInput.Format(hint);repeated=true;
   for(int i=0;i<128;i++)repeated &= ReferenceEquals(cMemo,NocturnisInput.Format(hint));
   Check(repeated,"Repeated C hints retain the new binding revision");
   Remember("Nocturnis.QA.ActiveProfile",true);int priorProfile=PlayerPrefs.GetInt("Nocturnis.QA.ActiveProfile",1);
   PlayerPrefs.SetInt("Nocturnis.QA.ActiveProfile",priorProfile==1 ? 2 : 1);
   Check(NocturnisInput.Key("skill")==KeyCode.C,"Controls stay global across save profiles");
   PlayerPrefs.SetInt("Nocturnis.QA.ActiveProfile",priorProfile);
   bool wasTesting=KryptaGame.Testing;
   try{
    KryptaGame.Testing=false;int stored=PlayerPrefs.GetInt("Nocturnis.Key.skill",(int)KeyCode.Q);KeyCode expected=Enum.IsDefined(typeof(KeyCode),stored) ? (KeyCode)stored : KeyCode.Q;
    Check(NocturnisInput.Key("skill")==expected,"Production namespace does not reuse QA cached keys");
   }finally{KryptaGame.Testing=wasTesting;}
   Check(NocturnisInput.Key("skill")==KeyCode.C && NocturnisInput.Format(hint)=="[C] / [Q] / [C] / Q \u2022","Returning to QA reloads its own binding revision");
   bool boundedCorrect=true;for(int i=0;i<640;i++)boundedCorrect &= NocturnisInput.Format(hint+" "+i)=="[C] / [Q] / [C] / Q \u2022 "+i;
   var cache=(System.Collections.IDictionary)typeof(NocturnisInput).GetField("formattedHints",StaticHidden).GetValue(null);
   Check(boundedCorrect && cache.Count<=256,"Description cache stays bounded under changing text");
   Check(NocturnisInput.Bind("skill",KeyCode.E) && NocturnisInput.Format(hint)==swapped,"Returning to E invalidates the bounded cache");
   Check(NocturnisInput.Bind("inventory",KeyCode.B) && NocturnisInput.Format(navHints[0])=="B  EKWIPUNEK","Actual inventory HUD caption uses its current key");
   PlayerPrefs.Save();Check(PlayerPrefs.GetInt("Nocturnis.QA.Key.skill")== (int)KeyCode.E&&PlayerPrefs.GetInt("Nocturnis.QA.Key.interact")== (int)KeyCode.Q,"Swapped bindings persist in isolated preferences");
   Check(NocturnisInput.Actions.Select(a=>NocturnisInput.Key(a)).Distinct().Count()==NocturnisInput.Actions.Length,"No duplicate mapped actions");
   foreach(var key in new[]{KeyCode.None,KeyCode.Escape,KeyCode.Return,KeyCode.Alpha1,KeyCode.Mouse0,KeyCode.UpArrow})Check(!NocturnisInput.Bind("skill",key),"Reserved input rejected: "+key);
   Check(!NocturnisInput.Bind("unknown",KeyCode.B),"Unknown action rejected");
   PlayerPrefs.SetInt("Nocturnis.QA.Key.skill",int.MaxValue);NocturnisInput.ReloadBindings();Check(NocturnisInput.Key("skill")==KeyCode.Q,"Corrupt persisted key falls back safely");
   NocturnisInput.Reset();Check(NocturnisInput.Key("skill")==KeyCode.Q&&NocturnisInput.Key("interact")==KeyCode.E,"Reset restores defaults");Check(NocturnisInput.Format(hint)=="[Q] / [E] / [Q] / E \u2022","Reset invalidates all formatted hints");
  }
  static string InspectClip(AudioClip clip,bool loop) {
   Check(clip&&clip.samples>0,"Generated audio exists");var samples=new float[clip.samples*clip.channels];Check(clip.GetData(samples,0),"Generated audio readable");
   float peak=0,seam=0;double energy=0,signature=0,stereo=0;
   for(int i=0;i<samples.Length;i++){
    float s=samples[i];if(float.IsNaN(s)||float.IsInfinity(s)||Mathf.Abs(s)>1)throw new Exception("Invalid / clipped sample in "+clip.name);
    peak=Mathf.Max(peak,Mathf.Abs(s));energy+=s*s;if(i%97==0)signature+=s*(1+i%37);
   }
   Check(energy/samples.Length>.000001,"Non-silent audio: "+clip.name);
   if(loop){Check(clip.channels==2&&Mathf.Abs(clip.length-24)<.01f,"Stereo 24-second biome layer");for(int c=0;c<clip.channels;c++)seam=Mathf.Max(seam,Mathf.Abs(samples[c]-samples[samples.Length-clip.channels+c]));Check(seam<.01f,"Loop seam has no large discontinuity: "+clip.name);for(int i=0;i<samples.Length;i+=2)stereo+=Math.Abs(samples[i]-samples[i+1]);Check(stereo>10,"Stereo environment is not duplicated mono");}
   report.audio.Add(new ClipResult{name=clip.name,samples=clip.samples,channels=clip.channels,peak=peak,seamDifference=seam});return signature.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
  }
  static void CheckAudio() {
   var environments=new HashSet<string>();var steps=new HashSet<string>();var attacks=new HashSet<string>();var type=typeof(NocturnisAudio);
   for(int biome=0;biome<5;biome++){var clip=(AudioClip)type.GetMethod("GenerateEnvironment",StaticHidden).Invoke(null,new object[]{biome});try{Check(environments.Add(InspectClip(clip,true)),"Distinct biome sound "+biome);}finally{UnityEngine.Object.DestroyImmediate(clip);}}
   foreach(string surface in new[]{"stone","earth","wood"})for(int n=0;n<4;n++){var clip=(AudioClip)type.GetMethod("GenerateFootstep",StaticHidden).Invoke(null,new object[]{surface,n});try{Check(steps.Add(InspectClip(clip,false)),"Distinct footstep "+surface+n);}finally{UnityEngine.Object.DestroyImmediate(clip);}}
   for(int i=0;i<5;i++){var clip=(AudioClip)type.GetMethod("Generate",StaticHidden).Invoke(null,new object[]{"attack"+i});try{Check(attacks.Add(InspectClip(clip,false)),"Distinct class attack "+i);}finally{UnityEngine.Object.DestroyImmediate(clip);}}
  }
  public static void Run() {
   report=new Report();KryptaGame.Testing=true;Directory.CreateDirectory("Logs");
   try {
    CheckBindings();CheckAudio();Remember("Nocturnis.QA.ActiveProfile",true);Remember("Nocturnis.MapExploration",true);
    for(int p=1;p<=2;p++){for(int z=0;z<Campaign.Count;z++)Remember("Nocturnis.QA.Explored."+p+"."+z,false);Remember("Nocturnis.QA.Explored."+p+".quest",true);}
    Remember("Nocturnis.QA.Checkpoint",false);Remember("Nocturnis.QA.Profile2.Checkpoint",false);
    File.WriteAllText(Output,JsonUtility.ToJson(report,true));EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");SessionState.SetBool(Session,true);EditorApplication.isPlaying=true;
   }catch(Exception e){Finish(false,e.ToString());}
  }
  [InitializeOnLoadMethod]static void Restore() {
   if(!SessionState.GetBool(Session,false))return;report=JsonUtility.FromJson<Report>(File.ReadAllText(Output));KryptaGame.Testing=true;started=EditorApplication.timeSinceStartup;running=false;EditorApplication.update+=Tick;
  }
  static void Profile(KryptaGame game,int profile) {
   typeof(KryptaGame).GetProperty("State").SetValue(game,KryptaGame.Phase.Title);Check(game.SelectProfile(profile),"Select profile "+profile);game.RefreshTutorialProfile();typeof(KryptaGame).GetProperty("State").SetValue(game,KryptaGame.Phase.Playing);
  }
  static void Reveal(KryptaGame game,Vector3 point) {
   var actor=(ActorVisual)typeof(KryptaGame).GetField("player",Hidden).GetValue(game);actor.Root.transform.position=point;typeof(KryptaGame).GetMethod("RevealMap",Hidden).Invoke(game,null);
  }
  static void CheckFootstepGate(KryptaGame game) {
   var actor=(ActorVisual)typeof(KryptaGame).GetField("player",Hidden).GetValue(game);var animation=(Animation)typeof(ActorVisual).GetField("animation",Hidden).GetValue(actor);
   foreach(string clip in new[]{"Walking_A","Running_A"}) {
    Check(animation[clip]!=null,"Actual movement clip available "+clip);actor.Play(clip,true,0);typeof(ActorVisual).GetField("locomotionSpeed",Hidden).SetValue(actor,4f);typeof(KryptaGame).GetField("previousFoot",Hidden).SetValue(game,-1);
    string surface;int foot;animation[clip].normalizedTime=.10f;Check(!game.PollFootstep(out surface,out foot),"No duplicated step at clip start");
    animation[clip].normalizedTime=.60f;Check(game.PollFootstep(out surface,out foot)&&foot==1&&surface=="earth","First foot contact from actual "+clip);
    Check(!game.PollFootstep(out surface,out foot),"One footstep per contact");animation[clip].normalizedTime=1.10f;Check(game.PollFootstep(out surface,out foot)&&foot==0,"Alternating foot contact");
   }
   typeof(ActorVisual).GetField("locomotionSpeed",Hidden).SetValue(actor,0f);string s;int f;Check(!game.PollFootstep(out s,out f),"Standing still cannot play steps");
  }
  static void Tick() {
   if(!EditorApplication.isPlaying||running)return;if(EditorApplication.timeSinceStartup-started>90){Finish(false,"Timed out");return;}
   var game=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();if(!game||!game.View)return;running=true;
   try {
    PlayerPrefs.SetInt("Nocturnis.MapExploration",1);for(int p=1;p<=2;p++){PlayerPrefs.DeleteKey("Nocturnis.QA.Explored."+p+"."+Campaign.Road);PlayerPrefs.DeleteKey("Nocturnis.QA.Explored."+p+".quest");}
    Profile(game,1);typeof(KryptaGame).GetMethod("LoadZone",Hidden).Invoke(game,new object[]{Campaign.Road});Reveal(game,new Vector3(12*1.8f,0,3*1.8f));
    Check(game.MapDiscovered(12,3)&&!game.MapDiscovered(12,35)&&!game.MapDiscovered(-1,0),"Local reveal and safe map bounds");PlayerPrefs.Save();
    game.RefreshTutorialProfile();Reveal(game,new Vector3(12*1.8f,0,3*1.8f));Check(game.MapDiscovered(12,3)&&!game.MapDiscovered(12,35),"Exploration restores after cache reset");
    game.TrackQuest(2);Check(game.TrackedQuest==2,"Quest can be tracked");game.TrackQuest(2);Check(game.TrackedQuest==-1,"Clicking tracked quest clears tracking");game.TrackQuest(1);
    Profile(game,2);Reveal(game,new Vector3(12*1.8f,0,35*1.8f));Check(game.MapDiscovered(12,35)&&!game.MapDiscovered(12,3)&&game.TrackedQuest==-1,"Exploration and tracking isolated by profile");
    Profile(game,1);Reveal(game,new Vector3(12*1.8f,0,3*1.8f));Check(game.MapDiscovered(12,3)&&!game.MapDiscovered(12,35)&&game.TrackedQuest==1,"Original profile retained its own discoveries and quest");
    PlayerPrefs.SetString("Nocturnis.QA.Explored.2."+Campaign.Road,"invalid base64");Profile(game,2);Reveal(game,new Vector3(12*1.8f,0,3*1.8f));Check(game.MapDiscovered(12,3),"Malformed exploration save recovers safely");
    Profile(game,1);Reveal(game,new Vector3(12*1.8f,0,35*1.8f));game.TrackQuest(3);
    Check((bool)typeof(KryptaGame).GetMethod("SaveCheckpoint",Hidden).Invoke(game,null),"Checkpoint captured for Continue test");
    Check(game.ContinueCheckpoint(),"Continue loads the existing expedition");typeof(KryptaGame).GetMethod("RevealMap",Hidden).Invoke(game,null);
    Check(game.MapDiscovered(12,35)&&game.TrackedQuest==3,"Continue preserves discoveries and tracked quest");
    string secondProfile=PlayerPrefs.GetString("Nocturnis.QA.Explored.2."+Campaign.Road);
    for(int z=0;z<Campaign.Count;z++)PlayerPrefs.SetString("Nocturnis.QA.Explored.1."+z,"AQ==");
    typeof(KryptaGame).GetProperty("State").SetValue(game,KryptaGame.Phase.Title);game.Begin();
    for(int z=0;z<Campaign.Count;z++)Check(!PlayerPrefs.HasKey("Nocturnis.QA.Explored.1."+z),"New expedition clears map "+z);
    Check(game.TrackedQuest==-1,"New expedition clears tracked quest");Check(PlayerPrefs.GetString("Nocturnis.QA.Explored.2."+Campaign.Road)==secondProfile,"New expedition preserves other save profile");
    typeof(KryptaGame).GetMethod("RevealMap",Hidden).Invoke(game,null);Check(!game.MapDiscovered(12,35),"New expedition cache does not leak previous discoveries");
    CheckFootstepGate(game);Finish(true,null);
   }catch(Exception e){Finish(false,e.ToString());}
  }
  static void Finish(bool passed,string failure) {
   SessionState.SetBool(Session,false);EditorApplication.update-=Tick;if(report==null)report=new Report();RestorePreferences();report.passed=passed;report.failure=failure;
   Directory.CreateDirectory("Logs");File.WriteAllText(Output,JsonUtility.ToJson(report,true));Debug.Log("QUALITY_OF_LIFE_QA "+(passed?"PASS":"FAIL")+" "+report.assertions+" "+failure);EditorApplication.Exit(passed?0:1);
  }
 }
}
