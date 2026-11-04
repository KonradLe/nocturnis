using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Krypta {
 // Opt-in diagnostics only. No changes to ordinary play or shipped quality defaults.
 public sealed class NocturnisPerformanceProbe : MonoBehaviour {
  public static bool Active {get;private set;}
  [Serializable] public sealed class Geometry {
   public int renderers,frustumRenderers,materialSlots,shadowCasters;
   public long triangles,frustumTriangles,backdropTriangles;public int backdropRenderers;
   public List<HeavyObject> heaviestBackdropObjects=new List<HeavyObject>();
  }
  [Serializable] public sealed class HeavyObject {public string name;public long triangles;public int renderers,materialSlots;}
  [Serializable] public sealed class Window {
   public int zone,frames,unfocusedFrames;public string name,zoneName;
   public bool hud,skipInputFormat,actorFill,backdrop,backdropShadows,newEnvironment;
   public double seconds,meanMs,medianMs,p95Ms,p99Ms,minMs,maxMs,fps;
   public int timingSamples,gpuTimingSamples;public double cpuFrameMs,gpuFrameMs;
   public Geometry geometry;public Vector3 cameraPosition;public float cameraSize;
  }
  [Serializable] public sealed class Report {
   public bool passed,qaIsolation;public string error,platform,gpu,version="0.7.0",startedUtc;
   public int width,height,originalTargetFrameRate,originalVsync,qualityLevel;
   public float sampleSeconds,warmupSeconds;
   public string note="Static render-cost ablation, not gameplay FPS. Identical frozen scene/camera per zone. Time scale 0; gameplay scripts, animators and particles frozen. VSync off and targetFrameRate unlimited in this opt-in process only. HUD-on isolates IMGUI cost; all other variants hide HUD. Geometry counts include mesh instances and material slots, not actual GPU draw calls. FrameTimingManager values appear only when supported; wall-frame changes alone do not prove a GPU bottleneck.";
   public List<Window> windows=new List<Window>();
  }
  sealed class RenderState {public Renderer renderer;public bool enabled;public ShadowCastingMode shadows;}
  sealed class BehaviourState {public Behaviour behaviour;public bool enabled;}
  readonly List<RenderState> renderStates=new List<RenderState>();
  readonly List<BehaviourState> frozen=new List<BehaviourState>();
  readonly List<Renderer> backdrop=new List<Renderer>(),environment=new List<Renderer>();
  readonly FrameTiming[] timing=new FrameTiming[1];
  KryptaGame game;KryptaHUD hud;Light fill;GameObject world;bool fillEnabled,done,hadQaProfile;
  int originalQaProfile;float originalTimeScale,started;string directory,error;
  Report report=new Report();

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Launch(){
   if(Array.IndexOf(System.Environment.GetCommandLineArgs(),"-performance-probe")<0)return;
   Active=true;KryptaGame.Testing=true;new GameObject("Performance probe opt-in").AddComponent<NocturnisPerformanceProbe>();
  }
  static string Arg(string key,string fallback){var a=System.Environment.GetCommandLineArgs();int i=Array.IndexOf(a,key);return i>=0 && i+1<a.Length ? a[i+1] : fallback;}
  static float Seconds(string key,float fallback){float f;return float.TryParse(Arg(key,""),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out f) && !float.IsNaN(f) && !float.IsInfinity(f) ? Mathf.Clamp(f,1,30) : fallback;}
  IEnumerator Start(){
   started=Time.realtimeSinceStartup;Application.logMessageReceived+=OnLog;
   directory=Path.GetFullPath(Arg("-performance-output","Logs/PerformanceProbe"));Directory.CreateDirectory(directory);
   report.startedUtc=DateTime.UtcNow.ToString("o");report.sampleSeconds=Seconds("-performance-seconds",10);report.warmupSeconds=Seconds("-performance-warmup",2);
   report.originalTargetFrameRate=Application.targetFrameRate;report.originalVsync=QualitySettings.vSyncCount;originalTimeScale=Time.timeScale;
   while(!(game=FindFirstObjectByType<KryptaGame>()) || !game.View || !game.GetComponent<KryptaHUD>())yield return null;
   // Wait through game.Start before overriding the game's usual 60-FPS cap.
   yield return null;
   report.originalTargetFrameRate=Application.targetFrameRate;report.originalVsync=QualitySettings.vSyncCount;
   QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
   report.platform=Application.platform.ToString();report.gpu=SystemInfo.graphicsDeviceName;report.width=Screen.width;report.height=Screen.height;report.qualityLevel=QualitySettings.GetQualityLevel();
   hadQaProfile=PlayerPrefs.HasKey("Nocturnis.QA.ActiveProfile");originalQaProfile=PlayerPrefs.GetInt("Nocturnis.QA.ActiveProfile",1);
   PlayerPrefs.SetInt("Nocturnis.QA.ActiveProfile",3);report.qaIsolation=KryptaGame.Testing && game.ActiveProfile==3;
   if(!report.qaIsolation)throw new Exception("Performance probe did not enter isolated QA profile");
   hud=game.GetComponent<KryptaHUD>();
   foreach(int zone in new[]{Campaign.Road,Campaign.Town}){
    RestoreScene();world=game.PreparePerformanceScene(zone);Time.timeScale=0;
    yield return null;yield return null;
    CaptureScene();
    yield return Measure(zone,"baseline-hud",true,true,true,true,true);
    yield return Measure(zone,"hud-without-input-format",true,true,true,true,true,true);
    yield return Measure(zone,"baseline",false,true,true,true,true);
    yield return Measure(zone,"no-actor-fill",false,false,true,true,true);
    yield return Measure(zone,"no-backdrop",false,true,false,true,true);
    yield return Measure(zone,"no-backdrop-shadows",false,true,true,false,true);
    yield return Measure(zone,"no-new-environment",false,false,false,false,false);
    yield return Measure(zone,"baseline-repeat",false,true,true,true,true);
   }
   report.passed=error==null && report.windows.Count==16;report.error=error??"";Save();Finish(report.passed?0:1);
  }
  void CaptureScene(){
   fill=null;foreach(var light in game.GetComponentsInChildren<Light>(true))if(light.name=="Soft moon fill on actors"){fill=light;fillEnabled=fill.enabled;break;}
   if(!fill)throw new Exception("Expected actor fill light missing");
   foreach(var renderer in world.GetComponentsInChildren<Renderer>(true))renderStates.Add(new RenderState{renderer=renderer,enabled=renderer.enabled,shadows=renderer.shadowCastingMode});
   foreach(var prop in world.GetComponentsInChildren<NocturnisBackdropProp>(true))backdrop.AddRange(prop.GetComponentsInChildren<Renderer>(true));
   foreach(var root in world.GetComponentsInChildren<NocturnisEnvironmentRoot>(true))environment.AddRange(root.GetComponentsInChildren<Renderer>(true));
   foreach(Transform child in world.transform)if(child.name=="Organic terrain")environment.AddRange(child.GetComponentsInChildren<Renderer>(true));
   foreach(var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)){
    if(behaviour==this || behaviour==hud || !behaviour.enabled)continue;
    frozen.Add(new BehaviourState{behaviour=behaviour,enabled=true});behaviour.enabled=false;
   }
   foreach(var animator in world.GetComponentsInChildren<Animator>(true)){
    frozen.Add(new BehaviourState{behaviour=animator,enabled=animator.enabled});animator.enabled=false;
   }
   foreach(var particles in world.GetComponentsInChildren<ParticleSystem>(true))particles.Pause(true);
   foreach(var audio in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))audio.Pause();
   game.enabled=false;hud.enabled=false;
  }
  void RestoreToggles(){
   foreach(var state in renderStates)if(state.renderer){state.renderer.enabled=state.enabled;state.renderer.shadowCastingMode=state.shadows;}
   if(fill)fill.enabled=fillEnabled;
  }
  void RestoreScene(){
   RestoreToggles();foreach(var state in frozen)if(state.behaviour)state.behaviour.enabled=state.enabled;
   renderStates.Clear();frozen.Clear();backdrop.Clear();environment.Clear();
   if(hud)hud.enabled=true;
  }
  IEnumerator Measure(int zone,string name,bool showHud,bool showFill,bool showBackdrop,bool castBackdropShadows,bool showEnvironment,bool skipFormat=false){
   RestoreToggles();NocturnisInput.PerformanceSkipFormat=skipFormat;hud.enabled=showHud;fill.enabled=showFill && fillEnabled;
   if(!showEnvironment)foreach(var renderer in environment)if(renderer)renderer.enabled=false;
   if(!showBackdrop)foreach(var renderer in backdrop)if(renderer)renderer.enabled=false;
   if(!castBackdropShadows)foreach(var renderer in backdrop)if(renderer)renderer.shadowCastingMode=ShadowCastingMode.Off;
   float warm=Time.realtimeSinceStartup+report.warmupSeconds;while(Time.realtimeSinceStartup<warm)yield return null;
   var row=new Window{zone=zone,zoneName=Campaign.Names[zone],name=name,hud=showHud,skipInputFormat=skipFormat,actorFill=showFill,backdrop=showBackdrop,backdropShadows=castBackdropShadows,newEnvironment=showEnvironment,
    cameraPosition=game.View.transform.position,cameraSize=game.View.orthographicSize,geometry=CountGeometry()};
   var samples=new List<double>();double last=Time.realtimeSinceStartupAsDouble,end=last+report.sampleSeconds,cpuSum=0,gpuSum=0;ulong lastTimestamp=0;
   while(Time.realtimeSinceStartupAsDouble<end){
    FrameTimingManager.CaptureFrameTimings();yield return null;
    double now=Time.realtimeSinceStartupAsDouble,ms=(now-last)*1000;last=now;
    if(ms<=0 || double.IsNaN(ms) || double.IsInfinity(ms))continue;samples.Add(ms);row.seconds+=ms/1000;
    if(!Application.isFocused)row.unfocusedFrames++;
    if(FrameTimingManager.GetLatestTimings(1,timing)>0 && timing[0].frameStartTimestamp!=lastTimestamp){
     lastTimestamp=timing[0].frameStartTimestamp;
     if(timing[0].cpuFrameTime>0){cpuSum+=timing[0].cpuFrameTime;row.timingSamples++;}
     if(timing[0].gpuFrameTime>0){gpuSum+=timing[0].gpuFrameTime;row.gpuTimingSamples++;}
    }
   }
   if(samples.Count<5)throw new Exception("Insufficient performance samples: "+name);
   samples.Sort();row.frames=samples.Count;row.meanMs=row.seconds*1000/row.frames;row.fps=row.frames/row.seconds;
   row.minMs=samples[0];row.maxMs=samples[samples.Count-1];row.medianMs=Percentile(samples,.5);row.p95Ms=Percentile(samples,.95);row.p99Ms=Percentile(samples,.99);
   row.cpuFrameMs=row.timingSamples>0?cpuSum/row.timingSamples:0;row.gpuFrameMs=row.gpuTimingSamples>0?gpuSum/row.gpuTimingSamples:0;
   report.windows.Add(row);Save();Debug.Log("PERFORMANCE_PROBE zone="+zone+" window="+name+" meanMs="+row.meanMs.ToString("F2")+" tris="+row.geometry.triangles+" backdropTris="+row.geometry.backdropTriangles);
  }
  static double Percentile(List<double> sorted,double percentile){return sorted[Mathf.Clamp((int)Math.Ceiling(sorted.Count*percentile)-1,0,sorted.Count-1)];}
  static long Triangles(Renderer renderer){
   Mesh mesh=null;var skin=renderer as SkinnedMeshRenderer;if(skin)mesh=skin.sharedMesh;else {var filter=renderer.GetComponent<MeshFilter>();if(filter)mesh=filter.sharedMesh;}
   if(!mesh)return 0;long triangles=0;for(int i=0;i<mesh.subMeshCount;i++)if(mesh.GetTopology(i)==MeshTopology.Triangles)triangles+=(long)mesh.GetIndexCount(i)/3;return triangles;
  }
  Geometry CountGeometry(){
   var result=new Geometry();var planes=GeometryUtility.CalculateFrustumPlanes(game.View);var backdropSet=new HashSet<Renderer>(backdrop);
   foreach(var state in renderStates){
    var r=state.renderer;if(!r || !r.enabled || !r.gameObject.activeInHierarchy)continue;long triangles=Triangles(r);
    result.renderers++;result.triangles+=triangles;result.materialSlots+=r.sharedMaterials.Length;if(r.shadowCastingMode!=ShadowCastingMode.Off)result.shadowCasters++;
    if(GeometryUtility.TestPlanesAABB(planes,r.bounds)){result.frustumRenderers++;result.frustumTriangles+=triangles;}
    if(backdropSet.Contains(r)){result.backdropRenderers++;result.backdropTriangles+=triangles;}
   }
   foreach(var prop in world.GetComponentsInChildren<NocturnisBackdropProp>(true)){
    var row=new HeavyObject{name=prop.name};foreach(var r in prop.GetComponentsInChildren<Renderer>(true))if(r.enabled && r.gameObject.activeInHierarchy){row.triangles+=Triangles(r);row.renderers++;row.materialSlots+=r.sharedMaterials.Length;}
    if(row.renderers>0)result.heaviestBackdropObjects.Add(row);
   }
   result.heaviestBackdropObjects.Sort((a,b)=>b.triangles.CompareTo(a.triangles));if(result.heaviestBackdropObjects.Count>12)result.heaviestBackdropObjects.RemoveRange(12,result.heaviestBackdropObjects.Count-12);
   return result;
  }
  void Save(){File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(report,true));}
  void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)error=message+"\n"+stack;}
  void Update(){
   if(done || directory==null || (error==null && Time.realtimeSinceStartup-started<480))return;
   report.error=error??"Performance probe watchdog";report.passed=false;StopAllCoroutines();Save();Finish(1);
  }
  void Finish(int code){
   done=true;NocturnisInput.PerformanceSkipFormat=false;Active=false;RestoreScene();if(game)game.EndPerformanceProbe();
   QualitySettings.vSyncCount=report.originalVsync;Application.targetFrameRate=report.originalTargetFrameRate;Time.timeScale=originalTimeScale;
   if(hadQaProfile)PlayerPrefs.SetInt("Nocturnis.QA.ActiveProfile",originalQaProfile);else PlayerPrefs.DeleteKey("Nocturnis.QA.ActiveProfile");
   Application.logMessageReceived-=OnLog;Application.Quit(code);
  }
 }
 public sealed partial class KryptaGame {
  public GameObject PreparePerformanceScene(int zone){
   if(!Testing)throw new InvalidOperationException("Performance preparation requires QA isolation");
   enabled=false;State=Phase.Title;SelectedHero=0;LoadZone(zone);State=Phase.Playing;Time.timeScale=0;introUntil=0;
   hasDestination=false;target=null;View.enabled=true;View.cullingMask=~(1<<30);View.orthographicSize=9;
   View.transform.position=HeroPosition+new Vector3(9,15,-11);View.transform.LookAt(HeroPosition);
   Shader.SetGlobalVector("_NocturnisHero",new Vector4(HeroPosition.x,HeroPosition.y,HeroPosition.z,1));
   explored[zone]=new bool[DungeonGrid.Width*DungeonGrid.Height];for(int i=0;i<explored[zone].Length;i++)explored[zone][i]=true;
   return world.gameObject;
  }
  public void EndPerformanceProbe(){if(Testing){State=Phase.Title;enabled=false;}}
 }
}
