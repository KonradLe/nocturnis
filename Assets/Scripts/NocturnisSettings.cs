using System;
using System.Collections.Generic;
using UnityEngine;

namespace Krypta
{
 // Preferences belong to the player, not to a campaign save. Preview display modes
 // are deliberately never persisted until the player confirms that they can see them.
 public sealed class NocturnisSettings : MonoBehaviour
 {
  public enum GraphicsPreset { Low, Medium, High }
  public const float DisplayConfirmationSeconds=15f;
  public static readonly int[] FrameCaps={30,60,90,120,144,-1};
  public GraphicsPreset Preset { get; private set; }
  public bool VSync { get; private set; }
  public int FrameCap { get; private set; }
  public bool DisplayPending { get; private set; }
  public string DisplayNotice { get; private set; }="";
  public int DisplayRevision { get; private set; }
  bool initialized;
  int originalWidth,originalHeight;
  FullScreenMode originalMode;
  double displayDeadline;
  static string Prefix { get { return KryptaGame.Testing ? "Nocturnis.QA.Settings." : "Nocturnis.Settings."; } }
  public static bool ServerPreview { get { return HasArgument("-server-preview"); } }
  public static bool DisplayLocked { get { return Application.isEditor || Application.isBatchMode || ServerPreview ||
   HasArgument("-screen-width") || HasArgument("-screen-height") || HasArgument("-screen-fullscreen") ||
   HasArgument("-window-mode"); } }
  public static float VoiceVolume { get { return Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix+"Voice",.85f)); } }
  public static bool DamageNumbersEnabled { get { return PlayerPrefs.GetInt(Prefix+"DamageNumbers",1)!=0; } }
  public float DisplaySecondsRemaining { get { return DisplayPending ? Mathf.Max(0,(float)(displayDeadline-Time.realtimeSinceStartupAsDouble)) : 0; } }
  public static bool HasArgument(string argument)
  {return Array.Exists(Environment.GetCommandLineArgs(),s=>string.Equals(s,argument,StringComparison.OrdinalIgnoreCase) || s.StartsWith(argument+"=",StringComparison.OrdinalIgnoreCase));}
  public static NocturnisSettings Ensure(GameObject host)
  {
   var settings=host.GetComponent<NocturnisSettings>();
   if(!settings)settings=host.AddComponent<NocturnisSettings>();
   settings.Initialize();return settings;
  }
  public void Initialize()
  {
   if(initialized)return;initialized=true;
   Preset=(GraphicsPreset)Mathf.Clamp(PlayerPrefs.GetInt(Prefix+"Quality",2),0,2);
   VSync=PlayerPrefs.GetInt(Prefix+"VSync",0)!=0;
   FrameCap=NormalizeFrameCap(PlayerPrefs.GetInt(Prefix+"FrameCap",60));
   ApplySavedSettings();
  }
  public static int NormalizeFrameCap(int value){return Array.IndexOf(FrameCaps,value)>=0 ? value : 60;}
  public void ApplySavedSettings()
  {
   ApplyVisualQuality();ApplyFramePacing();
   if(DisplayLocked)return;
   bool hasMode=PlayerPrefs.HasKey(Prefix+"DisplayMode");
   // Compatibility with releases that only saved a fullscreen toggle.
   bool legacy=!KryptaGame.Testing && PlayerPrefs.HasKey("Nocturnis.FullScreen");
   if(!hasMode && !legacy)return;
   var mode=hasMode ? NormalizeDisplayMode(PlayerPrefs.GetInt(Prefix+"DisplayMode")) :
    PlayerPrefs.GetInt("Nocturnis.FullScreen")==1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
   int width=PlayerPrefs.GetInt(Prefix+"Width",Screen.width),height=PlayerPrefs.GetInt(Prefix+"Height",Screen.height);
   int desktopWidth=Screen.currentResolution.width,desktopHeight=Screen.currentResolution.height;
   // A monitor may have been disconnected since the last launch.
   if(desktopWidth>0 && desktopHeight>0 && (width>desktopWidth || height>desktopHeight))
   {width=desktopWidth;height=desktopHeight;}
   Screen.SetResolution(Mathf.Max(640,width),Mathf.Max(360,height),mode);
  }
  public void SetPreset(GraphicsPreset value)
  {Preset=(GraphicsPreset)Mathf.Clamp((int)value,0,2);ApplyVisualQuality();PlayerPrefs.SetInt(Prefix+"Quality",(int)Preset);PlayerPrefs.Save();}
  public void ApplyVisualQuality()
  {
   int quality=(int)Preset;
   QualitySettings.globalTextureMipmapLimit=quality==0 ? 2 : quality==1 ? 1 : 0;
   QualitySettings.antiAliasing=quality==0 ? 0 : quality==1 ? 2 : 4;
   QualitySettings.anisotropicFiltering=quality==0 ? AnisotropicFiltering.Disable : quality==1 ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable;
   QualitySettings.pixelLightCount=quality==0 ? 1 : quality==1 ? 2 : 4;
   QualitySettings.lodBias=quality==0 ? .7f : quality==1 ? 1f : 1.5f;
   QualitySettings.shadows=quality==0 || ServerPreview ? ShadowQuality.Disable : quality==1 ? ShadowQuality.HardOnly : ShadowQuality.All;
   QualitySettings.shadowResolution=quality==0 ? ShadowResolution.Low : quality==1 ? ShadowResolution.Medium : ShadowResolution.High;
   QualitySettings.shadowDistance=quality==0 ? 18f : quality==1 ? 32f : 52f;
   QualitySettings.shadowCascades=quality==0 ? 0 : quality==1 ? 2 : 4;
   foreach(var camera in Camera.allCameras)ConfigureCamera(camera);
  }
  public static void ConfigureCamera(Camera camera){if(camera)camera.allowMSAA=true;}
  public void SetVSync(bool value)
  {VSync=value;ApplyFramePacing();PlayerPrefs.SetInt(Prefix+"VSync",value ? 1 : 0);PlayerPrefs.Save();}
  public void SetFrameCap(int value)
  {FrameCap=NormalizeFrameCap(value);ApplyFramePacing();PlayerPrefs.SetInt(Prefix+"FrameCap",FrameCap);PlayerPrefs.Save();}
  public void ApplyFramePacing()
  {QualitySettings.vSyncCount=ServerPreview ? 0 : VSync ? 1 : 0;Application.targetFrameRate=ServerPreview ? 30 : FrameCap;}
  public static void SetVoiceVolume(float value)
  {PlayerPrefs.SetFloat(Prefix+"Voice",Mathf.Clamp01(value));PlayerPrefs.Save();}
  public static void SetDamageNumbers(bool value)
  {PlayerPrefs.SetInt(Prefix+"DamageNumbers",value ? 1 : 0);PlayerPrefs.Save();}
  public static FullScreenMode NormalizeDisplayMode(int value)
  {
   if(value==(int)FullScreenMode.ExclusiveFullScreen)return FullScreenMode.ExclusiveFullScreen;
   return value==(int)FullScreenMode.FullScreenWindow ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
  }
  public static List<Vector2Int> AvailableResolutions()
  {
   var result=new List<Vector2Int>();
   Action<int,int> add=(width,height)=>{var item=new Vector2Int(width,height);if(width>=640 && height>=360 && !result.Contains(item))result.Add(item);};
   int maxWidth=Screen.currentResolution.width,maxHeight=Screen.currentResolution.height;
   foreach(var r in Screen.resolutions)add(r.width,r.height);
   foreach(var size in new[]{new Vector2Int(960,540),new Vector2Int(1280,720),new Vector2Int(1600,900),new Vector2Int(1920,1080),new Vector2Int(2560,1440)})
    if(size.x<=maxWidth && size.y<=maxHeight)add(size.x,size.y);
   add(Screen.width,Screen.height);
   result.Sort((a,b)=>a.x==b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
   return result;
  }
  public bool BeginDisplayPreview(int width,int height,FullScreenMode mode)
  {
   if(DisplayLocked || DisplayPending || width<640 || height<360)return false;
   originalWidth=Screen.width;originalHeight=Screen.height;originalMode=Screen.fullScreenMode;
   mode=NormalizeDisplayMode((int)mode);
   if(mode==FullScreenMode.FullScreenWindow){width=Screen.currentResolution.width;height=Screen.currentResolution.height;}
   DisplayPending=true;DisplayNotice="";displayDeadline=Time.realtimeSinceStartupAsDouble+DisplayConfirmationSeconds;
   Screen.SetResolution(width,height,mode);DisplayRevision++;return true;
  }
  public void ConfirmDisplay()
  {
   if(!DisplayPending)return;
   // Read the actual mode accepted by the platform, not merely the requested values.
   PlayerPrefs.SetInt(Prefix+"Width",Screen.width);PlayerPrefs.SetInt(Prefix+"Height",Screen.height);
   PlayerPrefs.SetInt(Prefix+"DisplayMode",(int)Screen.fullScreenMode);PlayerPrefs.Save();
   DisplayPending=false;DisplayNotice="Zapisano ustawienia ekranu.";DisplayRevision++;
  }
  public void RevertDisplay()
  {
   if(!DisplayPending)return;
   DisplayPending=false;Screen.SetResolution(originalWidth,originalHeight,originalMode);
   DisplayNotice="Przywrócono poprzednie ustawienia ekranu.";DisplayRevision++;
  }
  void Update(){if(DisplayPending && Time.realtimeSinceStartupAsDouble>=displayDeadline)RevertDisplay();}
  void OnDisable(){RevertDisplay();}
 }
}
