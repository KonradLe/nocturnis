using System;
using System.Collections;
using System.IO;
using System.Diagnostics;
using UnityEngine;
namespace Krypta {
 public sealed class NocturnisVideoCapture : MonoBehaviour {
  static string Arg(string k,string d){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,k);return i>=0&&i+1<a.Length?a[i+1]:d;}
  static bool Enabled=>Array.IndexOf(Environment.GetCommandLineArgs(),"-trailer-record")>=0;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  static void Isolate(){if(Enabled)KryptaGame.Testing=true;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Launch(){if(Enabled)new GameObject("Opt-in trailer recorder").AddComponent<NocturnisVideoCapture>();}
  Process encoder;Texture2D frame;int frames;bool closed;string output;float oldCapture;
  IEnumerator Start(){
   float deadline=Time.realtimeSinceStartup+90;KryptaGame game=null;
   while(Time.realtimeSinceStartup<deadline){game=FindFirstObjectByType<KryptaGame>();if(game&&game.State==KryptaGame.Phase.Playing)break;yield return null;}
   if(!game||game.State!=KryptaGame.Phase.Playing){UnityEngine.Debug.LogError("MEDIA_RECORD_BOOT_TIMEOUT");Application.Quit(2);yield break;}
   output=Path.GetFullPath(Arg("-trailer-output","Nocturnis-gameplay.mp4"));Directory.CreateDirectory(Path.GetDirectoryName(output));
   int seconds;if(!int.TryParse(Arg("-trailer-seconds","40"),out seconds))seconds=40;seconds=Mathf.Clamp(seconds,3,120);
   int width=Screen.width,height=Screen.height;
   var info=new ProcessStartInfo(Arg("-trailer-ffmpeg","ffmpeg"),"-hide_banner -loglevel warning -y -f rawvideo -pixel_format rgb24 -video_size "+width+"x"+height+" -framerate 30 -i pipe:0 -an -vf vflip -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p -movflags +faststart \""+output+"\"");
   info.UseShellExecute=false;info.CreateNoWindow=true;info.RedirectStandardInput=true;info.RedirectStandardError=true;
   encoder=new Process{StartInfo=info};encoder.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)UnityEngine.Debug.Log("MEDIA_ENCODER "+e.Data);};
   try{encoder.Start();encoder.BeginErrorReadLine();}catch(Exception e){UnityEngine.Debug.LogException(e);Application.Quit(3);yield break;}
   oldCapture=Time.captureDeltaTime;Time.captureFramerate=30;Application.runInBackground=true;
   frame=new Texture2D(width,height,TextureFormat.RGB24,false);var end=new WaitForEndOfFrame();
   for(frames=0;frames<seconds*30;frames++){
    yield return end;bool failed=false;
    try{
     if(Screen.width!=width||Screen.height!=height)throw new Exception("Capture resolution changed");
     frame.ReadPixels(new Rect(0,0,width,height),0,0,false);frame.Apply(false,false);
     byte[] pixels=frame.GetRawTextureData();encoder.StandardInput.BaseStream.Write(pixels,0,pixels.Length);
    }catch(Exception e){UnityEngine.Debug.LogException(e);failed=true;}
    if(failed){Close(false);Application.Quit(4);yield break;}
    if(frames%150==0)UnityEngine.Debug.Log("MEDIA_RECORD_FRAME "+frames+" zone="+game.Zone+" kills="+game.Defeated);
   }
   Close(true);Application.Quit(0);
  }
  void Close(bool success){
   if(closed)return;closed=true;Time.captureDeltaTime=oldCapture;int exit=-1;
   if(encoder!=null){try{encoder.StandardInput.Close();if(!encoder.WaitForExit(60000))encoder.Kill();else exit=encoder.ExitCode;}catch(Exception e){UnityEngine.Debug.LogException(e);}encoder.Dispose();}
   if(frame)Destroy(frame);
   if(output!=null)File.WriteAllText(output+".json","{\"frames\":"+frames+",\"fps\":30,\"encoderExit\":"+exit+",\"complete\":"+(success&&exit==0?"true":"false")+",\"source\":\"Unity framebuffer; normal pacing bot actions; isolated QA save\"}");
   UnityEngine.Debug.Log("MEDIA_RECORD_COMPLETE "+output+" frames="+frames+" encoder="+exit);
  }
  void OnDestroy(){Close(false);}
 }
}
