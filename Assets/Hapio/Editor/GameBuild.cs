using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hapio.Editor
{
    /// <summary>Separate development-only entry point. LabBuild and the global PlayerSettings stay untouched.</summary>
    public static class GameBuild
    {
        private const string LastOutput = "Hapio.LastDevelopmentBuild";
        [MenuItem("Hapio/Development Build/Build Mac App")]
        public static void Mac() => Build(BuildTarget.StandaloneOSX);
        [MenuItem("Hapio/Development Build/Export iOS Xcode Project")]
        public static void Ios() => Build(BuildTarget.iOS);
        [MenuItem("Hapio/Development Build/Open Last Output")]
        public static void OpenOutput()
        {
            string path = SessionState.GetString(LastOutput, string.Empty);
            if (!string.IsNullOrEmpty(path) && (Directory.Exists(path) || File.Exists(path))) EditorUtility.RevealInFinder(path);
            else Debug.LogWarning("HAPIO_BUILD_OUTPUT_UNAVAILABLE: complete a development build first.");
        }
        private static void Build(BuildTarget target)
        {
            foreach (string scene in GameScenePaths.All)
                if (!File.Exists(scene)) throw new InvalidOperationException("Create the missing game scenes first: Hapio/Create Missing Development Game Scenes.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string run = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string output = Path.Combine(project, "Builds", "Hapio", run, target == BuildTarget.iOS ? "iOS" : "macOS/Hapio-Development.app");
            if (File.Exists(output) || Directory.Exists(output)) throw new InvalidOperationException("The output already exists: " + output);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            Debug.Log("HAPIO_BUILD_START target=" + target + " output=" + output);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            { scenes = GameScenePaths.All, target = target, locationPathName = output, options = BuildOptions.Development });
            if (report == null || report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("HAPIO_BUILD_FAILED target=" + target + " output=" + output);
            SessionState.SetString(LastOutput, output);
            Debug.Log("HAPIO_BUILD_OK output=" + output);
        }
    }
}
