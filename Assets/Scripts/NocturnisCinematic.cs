using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;
namespace Krypta {
 [DefaultExecutionOrder(-900)]
 public sealed class NocturnisCinematic : MonoBehaviour {
  static NocturnisCinematic current;static int releasedFrame=-1;static bool startupAttempted;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void ResetStartupSession(){current=null;releasedFrame=-1;startupAttempted=false;}
  internal static bool SessionQA=>Array.IndexOf(Environment.GetCommandLineArgs(),"-cinematic-session-qa")>=0;
  public static bool Active=>current!=null;
  public static long DecodedFrame=>current&&current.video?current.video.frame:-1;
  public static bool BlocksInput=>Active||Time.frameCount==releasedFrame;
  internal static bool QA=>Array.IndexOf(Environment.GetCommandLineArgs(),"-cinematic-qa")>=0;
  VideoPlayer video;RenderTexture picture;Action completed;
  bool finished,oldAudioPause,prepared;float openedAt,playAt,oldScale;
  GUIStyle label,button;
  public static bool TryPlay(KryptaGame game,Action next) {
   if(Active || startupAttempted || Application.isBatchMode || (KryptaGame.Testing&&!QA&&!SessionQA))return false;
   // Returning to the title reloads the scene, not the application session.
   startupAttempted=true;
   string path=Path.Combine(Application.streamingAssetsPath,"Nocturnis-Intro.mp4");
   if(!File.Exists(path)){Debug.Log("CINEMATIC_UNAVAILABLE");return false;}
   var obj=new GameObject("Nocturnis opening cinematic");
   var player=obj.AddComponent<NocturnisCinematic>();current=player;
   player.completed=next;player.oldScale=Time.timeScale;player.oldAudioPause=AudioListener.pause;
   Time.timeScale=0;AudioListener.pause=true;player.openedAt=Time.realtimeSinceStartup;
   try {
    player.picture=new RenderTexture(1280,720,0,RenderTextureFormat.ARGB32);player.picture.Create();
    player.video=obj.AddComponent<VideoPlayer>();var v=player.video;
    v.playOnAwake=false;v.isLooping=false;v.waitForFirstFrame=true;v.skipOnDrop=true;
    v.source=VideoSource.Url;v.url=path;v.renderMode=VideoRenderMode.RenderTexture;v.targetTexture=player.picture;
    v.timeUpdateMode=VideoTimeUpdateMode.UnscaledGameTime;v.audioOutputMode=VideoAudioOutputMode.Direct;
    v.controlledAudioTrackCount=1;v.SetDirectAudioVolume(0,game.Muted ? 0 : NocturnisSettings.VoiceVolume);
    v.SetDirectAudioMute(0,PlayerPrefs.GetInt("Nocturnis.Muted",0)==1);
    v.prepareCompleted+=player.Ready;v.loopPointReached+=player.Ended;v.errorReceived+=player.Failed;
    v.Prepare();Debug.Log("CINEMATIC_PREPARING");
   }catch(Exception e){Debug.LogWarning("CINEMATIC_ERROR "+e.Message);player.Finish("setup-error");}
   return true;
  }
  void Ready(VideoPlayer v){if(finished)return;prepared=true;playAt=Time.realtimeSinceStartup;v.Play();Debug.Log("CINEMATIC_PLAYING");}
  void Ended(VideoPlayer v){Finish("completed");}
  void Failed(VideoPlayer v,string error){Debug.LogWarning("CINEMATIC_ERROR "+error);Finish("decode-error");}
  void Update(){
   if(finished)return;
   float now=Time.realtimeSinceStartup;
   if(now-openedAt>.35f && (Input.GetKeyDown(KeyCode.Escape)||Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.Return))){Finish("skipped");return;}
   if(!prepared&&now-openedAt>12 || prepared&&now-playAt>35)Finish("timeout");
  }
  public static void Skip(){if(current)current.Finish("skipped");}
  void OnGUI(){
   if(finished)return;
   int depth=GUI.depth;Matrix4x4 matrix=GUI.matrix;Color color=GUI.color;
   GUI.depth=-1000;GUI.matrix=Matrix4x4.identity;GUI.color=Color.black;
   GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
   GUI.color=Color.white;
   if(prepared&&picture)GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),picture,ScaleMode.ScaleToFit,false);
   float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
   if(label==null){label=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter};button=new GUIStyle(GUI.skin.button);}
   label.fontSize=Mathf.Max(14,Mathf.RoundToInt(19*scale));button.fontSize=Mathf.Max(12,Mathf.RoundToInt(15*scale));
   if(!prepared)GUI.Label(new Rect(0,Screen.height*.46f,Screen.width,60*scale),"NOCTURNIS",label);
   if(GUI.Button(new Rect(Screen.width-245*scale,24*scale,220*scale,38*scale),"POMIŃ  ·  SPACJA / ESC",button))Finish("skipped");
   GUI.depth=depth;GUI.matrix=matrix;GUI.color=color;
  }
  void Finish(string reason){
   if(finished)return;finished=true;
   if(video){video.prepareCompleted-=Ready;video.loopPointReached-=Ended;video.errorReceived-=Failed;video.Stop();}
   AudioListener.pause=oldAudioPause;Time.timeScale=oldScale;
   if(current==this)current=null;releasedFrame=Time.frameCount;
   if(picture){picture.Release();Destroy(picture);}
   Debug.Log("CINEMATIC_END "+reason);
   var next=completed;completed=null;if(next!=null)next();Destroy(gameObject);
  }
  void OnDestroy(){if(!finished)Finish("destroyed");}
 }
}
