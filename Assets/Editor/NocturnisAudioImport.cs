using UnityEditor;
using UnityEngine;
namespace Krypta.Editor
{
 // Scoped to this release's authored banks; movie/music and other assets are untouched.
 public sealed class NocturnisAudioImport : AssetPostprocessor
 {
  void OnPreprocessAudio()
  {
   if(!assetPath.StartsWith("Assets/Resources/NocturnisAudio/"))return;
   var importer=(AudioImporter)assetImporter;
   bool ambient=assetPath.Contains("/ambient-");
   importer.forceToMono=!ambient;
   var settings=importer.defaultSampleSettings;
   settings.loadType=ambient?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;
   settings.compressionFormat=ambient?AudioCompressionFormat.Vorbis:AudioCompressionFormat.PCM;
   settings.quality=.85f;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
   importer.defaultSampleSettings=settings;importer.loadInBackground=ambient;
  }
 }
}
