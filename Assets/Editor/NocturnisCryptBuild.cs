using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Krypta.Editor
{
    // Isolated review builds; installed releases and shared project settings remain unchanged.
    public static class NocturnisCryptBuild
    {
        public static void Linux()
        {
            Build(BuildTarget.StandaloneLinux64, "Builds/CryptReview/Linux/Nocturnis.x86_64");
        }

        public static void Windows()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/CryptReview/Windows/Nocturnis.exe");
        }

        static void Build(BuildTarget target, string path)
        {
            NocturnisCryptPaletteSetup.Prepare();
            var config = Resources.Load<KryptaAssets>("KryptaAssets");
            if (!config || config.version < 8 || config.heroes == null || config.heroes.Length != 5)
                throw new InvalidOperationException("Crypt review requires the five-hero KayKit configuration, version 8 or later.");
            foreach (var hero in config.heroes)
                if (hero == null || !hero.model || hero.animations == null || hero.animations.Length < 8 || hero.animations.Any(clip => !clip))
                    throw new InvalidOperationException("Every crypt-review hero must include a model and all eight animations.");
            if (!Resources.Load<Texture2D>("UI/StorybookMenu"))
                throw new InvalidOperationException("The StorybookMenu illustration is missing from runtime resources.");
            if (!Resources.Load<NocturnisCryptPalette>("CryptPalette"))
                throw new InvalidOperationException("The crypt palette is missing from runtime resources.");

            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled game scene is configured for the crypt review.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string previousVersion = PlayerSettings.bundleVersion;
            try
            {
                PlayerSettings.bundleVersion = "0.4.5-crypt.1";
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = path,
                    target = target,
                    options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Crypt review build failed: " + report.summary.result);
                Debug.Log("Crypt review 0.4.5-crypt.1 build succeeded: " + path);
            }
            finally
            {
                PlayerSettings.bundleVersion = previousVersion;
            }
        }
    }
}
