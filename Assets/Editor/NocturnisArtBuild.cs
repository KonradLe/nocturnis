using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Krypta.Editor
{
    public static class NocturnisArtBuild
    {
        const string FloorWearPath = "Assets/Resources/Artisan/FloorWear.png";

        public static void Linux()
        {
            Build(BuildTarget.StandaloneLinux64, "Builds/ArtisanReview/Linux/Nocturnis.x86_64");
        }

        public static void Windows()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/ArtisanReview/Windows/Nocturnis.exe");
        }

        static void PrepareFloorWear()
        {
            var texture = AssetImporter.GetAtPath(FloorWearPath) as TextureImporter;
            if (!texture) throw new InvalidOperationException("The original floor-wear texture is required: " + FloorWearPath);
            texture.textureType = TextureImporterType.Default;
            texture.alphaSource = TextureImporterAlphaSource.FromInput;
            texture.alphaIsTransparency = true;
            texture.mipmapEnabled = true;
            texture.maxTextureSize = 1024;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.textureCompression = TextureImporterCompression.Compressed;
            texture.sRGBTexture = true;
            texture.SaveAndReimport();
            if (!Resources.Load<Texture2D>("Artisan/FloorWear"))
                throw new InvalidOperationException("The floor-wear texture is missing from runtime resources.");
        }

        public static void Prepare()
        {
            PrepareFloorWear();
            NocturnisCryptPaletteSetup.Prepare();
            NocturnisArtisanSetup.Prepare();
            var config = Resources.Load<KryptaAssets>("KryptaAssets");
            if (!config || config.version < 8 || config.heroes == null || config.heroes.Length != 5)
                throw new InvalidOperationException("The art review requires the five-hero KayKit configuration, version 8 or later.");
            foreach (var hero in config.heroes)
                if (hero == null || !hero.model || hero.animations == null || hero.animations.Length < 8 || hero.animations.Any(clip => !clip))
                    throw new InvalidOperationException("Every art-review hero must include a model and all eight animations.");
            if (!Resources.Load<Texture2D>("UI/StorybookMenu"))
                throw new InvalidOperationException("The StorybookMenu illustration is missing from runtime resources.");
            if (!Resources.Load<NocturnisCryptPalette>("CryptPalette"))
                throw new InvalidOperationException("The crypt palette is missing from runtime resources.");
            var artisan = Resources.Load<NocturnisArtisanKit>("ArtisanKit");
            if (!artisan || !artisan.relicShrine || !artisan.brokenPillar || !artisan.urnNiche)
                throw new InvalidOperationException("All three original artisan models are required for the art review.");
            Debug.Log("Art review resources prepared: floor wear, crypt palette and three original models.");
        }

        static void Build(BuildTarget target, string path)
        {
            Prepare();
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled game scene is configured for the art review.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string previousVersion = PlayerSettings.bundleVersion;
            try
            {
                PlayerSettings.bundleVersion = "0.4.5-art.1";
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = path,
                    target = target,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Art review build failed: " + report.summary.result);
                Debug.Log("Art review 0.4.5-art.1 build succeeded: " + path);
            }
            finally
            {
                PlayerSettings.bundleVersion = previousVersion;
            }
        }
    }
}
