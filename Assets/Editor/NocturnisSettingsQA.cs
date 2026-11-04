#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Krypta.EditorTools
{
 // Exercises the actual Unity quality/preference APIs in an isolated QA namespace.
 public static class NocturnisSettingsQA
 {
  static void Require(bool value,string message){if(!value)throw new Exception("Settings QA: "+message);}
  public static void Run()
  {
   bool testing=KryptaGame.Testing;
   int mip=QualitySettings.globalTextureMipmapLimit,aa=QualitySettings.antiAliasing,lights=QualitySettings.pixelLightCount,
    vsync=QualitySettings.vSyncCount,fps=Application.targetFrameRate,cascades=QualitySettings.shadowCascades;
   float bias=QualitySettings.lodBias,distance=QualitySettings.shadowDistance;
   var shadows=QualitySettings.shadows;var resolution=QualitySettings.shadowResolution;var anisotropic=QualitySettings.anisotropicFiltering;
   var cameras=Camera.allCameras;var cameraFlags=new bool[cameras.Length];for(int i=0;i<cameras.Length;i++)cameraFlags[i]=cameras[i].allowMSAA;
   const string prefix="Nocturnis.QA.Settings.";
   string[] ints={"Quality","VSync","FrameCap","DamageNumbers","DisplayMode","Width","Height"};
   var saved=new Dictionary<string,int>();foreach(string key in ints)if(PlayerPrefs.HasKey(prefix+key))saved[key]=PlayerPrefs.GetInt(prefix+key);
   bool hadVoice=PlayerPrefs.HasKey(prefix+"Voice");float oldVoice=PlayerPrefs.GetFloat(prefix+"Voice");
   GameObject host=null;
   try
   {
    KryptaGame.Testing=true;
    foreach(string key in ints)PlayerPrefs.DeleteKey(prefix+key);PlayerPrefs.DeleteKey(prefix+"Voice");
    host=new GameObject("Settings QA");var settings=NocturnisSettings.Ensure(host);
    Require(settings.Preset==NocturnisSettings.GraphicsPreset.High,"default high quality");
    settings.SetPreset(NocturnisSettings.GraphicsPreset.Low);
    Require(QualitySettings.shadows==ShadowQuality.Disable && QualitySettings.globalTextureMipmapLimit==2 && QualitySettings.antiAliasing==0,"low preset applies real rendering settings");
    settings.SetPreset(NocturnisSettings.GraphicsPreset.Medium);
    Require(QualitySettings.globalTextureMipmapLimit==1 && QualitySettings.antiAliasing==2 && QualitySettings.shadowDistance==32,"medium preset");
    settings.SetPreset(NocturnisSettings.GraphicsPreset.High);
    Require(QualitySettings.globalTextureMipmapLimit==0 && QualitySettings.antiAliasing==4 && QualitySettings.shadowDistance==52,"high preset");
    settings.SetVSync(false);settings.SetFrameCap(120);
    Require(Application.targetFrameRate==(NocturnisSettings.ServerPreview ? 30 : 120) && QualitySettings.vSyncCount==0,"frame cap");
    settings.SetVSync(true);
    Require(QualitySettings.vSyncCount==(NocturnisSettings.ServerPreview ? 0 : 1),"VSync");
    settings.SetFrameCap(999);Require(settings.FrameCap==60,"invalid frame cap normalized");
    NocturnisSettings.SetVoiceVolume(2);Require(NocturnisSettings.VoiceVolume==1,"voice clamps high");
    NocturnisSettings.SetVoiceVolume(-2);Require(NocturnisSettings.VoiceVolume==0,"voice clamps low");
    NocturnisSettings.SetDamageNumbers(false);Require(!NocturnisSettings.DamageNumbersEnabled,"damage numbers persists");
    settings.SetPreset(NocturnisSettings.GraphicsPreset.Medium);
    UnityEngine.Object.DestroyImmediate(host);host=new GameObject("Settings QA reload");settings=NocturnisSettings.Ensure(host);
    Require(settings.Preset==NocturnisSettings.GraphicsPreset.Medium && settings.VSync && settings.FrameCap==60,"preferences survive component reload");
    Require(NocturnisSettings.DisplayLocked,"editor display lock");
    Require(!settings.BeginDisplayPreview(1280,720,FullScreenMode.Windowed) && !settings.DisplayPending,"editor display requests rejected");
    Require(!PlayerPrefs.HasKey(prefix+"Width") && !PlayerPrefs.HasKey(prefix+"DisplayMode"),"unconfirmed display never saved");
    Require(NocturnisSettings.NormalizeDisplayMode(800)==FullScreenMode.Windowed,"invalid display mode normalized");
    var sizes=NocturnisSettings.AvailableResolutions();var unique=new HashSet<Vector2Int>(sizes);
    Require(unique.Count==sizes.Count,"resolution list deduplicates monitor refresh rates");
    Debug.Log("NOCTURNIS_SETTINGS_QA_PASS: real presets, frame pacing, persistence, volume bounds, display protection");
   }
   finally
   {
    if(host)UnityEngine.Object.DestroyImmediate(host);
    foreach(string key in ints){if(saved.ContainsKey(key))PlayerPrefs.SetInt(prefix+key,saved[key]);else PlayerPrefs.DeleteKey(prefix+key);}
    if(hadVoice)PlayerPrefs.SetFloat(prefix+"Voice",oldVoice);else PlayerPrefs.DeleteKey(prefix+"Voice");PlayerPrefs.Save();
    KryptaGame.Testing=testing;QualitySettings.globalTextureMipmapLimit=mip;QualitySettings.antiAliasing=aa;QualitySettings.pixelLightCount=lights;
    QualitySettings.vSyncCount=vsync;Application.targetFrameRate=fps;QualitySettings.shadowCascades=cascades;QualitySettings.lodBias=bias;
    QualitySettings.shadowDistance=distance;QualitySettings.shadows=shadows;QualitySettings.shadowResolution=resolution;QualitySettings.anisotropicFiltering=anisotropic;
    for(int i=0;i<cameras.Length;i++)if(cameras[i])cameras[i].allowMSAA=cameraFlags[i];
   }
  }
 }
}
#endif
