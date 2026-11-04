using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Krypta.Editor
{
    public static class NocturnisCombatBuild
    {
        public static void Prepare() { NocturnisArtBuild.Prepare(); }
        public static void Linux() { Build(BuildTarget.StandaloneLinux64, "Builds/CombatReview/Linux/Nocturnis.x86_64"); }
        public static void Windows() { Build(BuildTarget.StandaloneWindows64, "Builds/CombatReview/Windows/Nocturnis.exe"); }

        static void Build(BuildTarget target, string path)
        {
            Prepare();
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No enabled scene for the combat and menu review.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string previousVersion = PlayerSettings.bundleVersion;
            try
            {
                PlayerSettings.bundleVersion = "0.4.6-feedback.1";
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes, locationPathName = path, target = target, options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Combat/menu review build failed: " + report.summary.result);
                Debug.Log("Combat/menu review 0.4.6-feedback.1 built: " + path);
            }
            finally { PlayerSettings.bundleVersion = previousVersion; }
        }
    }
}
