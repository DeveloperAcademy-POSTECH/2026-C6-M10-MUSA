using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace C6Lab.Editor
{
    public static class LabBuild
    {
        private const string Scene = "Assets/Lab/Scenes/Lab.unity";
        private const string OutputOverrideVariable = "C6_LAB_BUILD_OUT";
        private const string LastBuildOutputKey = "C6Lab.LastBuildOutput";

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        [MenuItem("C6 Lab/Build Mac App")]
        public static void Mac()
        {
            Build(ResolveOutput(BuildTarget.StandaloneOSX), BuildTarget.StandaloneOSX);
        }

        [MenuItem("C6 Lab/Export iOS Xcode Project")]
        public static void Ios()
        {
            Build(ResolveOutput(BuildTarget.iOS), BuildTarget.iOS);
        }

        [MenuItem("C6 Lab/Open Last Build Folder")]
        public static void OpenLastBuildFolder()
        {
            string output = SessionState.GetString(LastBuildOutputKey, string.Empty);
            if (string.IsNullOrEmpty(output) || (!Directory.Exists(output) && !File.Exists(output)))
            {
                Debug.LogWarning("C6_LAB_BUILD_FOLDER_UNAVAILABLE: Build once in this Editor session, then try again.");
                return;
            }

            EditorUtility.RevealInFinder(output);
        }

        private static string ResolveOutput(BuildTarget target)
        {
            string requested = Environment.GetEnvironmentVariable(OutputOverrideVariable);
            if (!string.IsNullOrWhiteSpace(requested))
            {
                return Path.GetFullPath(Path.IsPathRooted(requested)
                    ? requested
                    : Path.Combine(ProjectRoot, requested));
            }

            string buildsRoot = Path.Combine(ProjectRoot, "Builds", "Lab");
            string runName = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string runRoot = Path.Combine(buildsRoot, runName);
            if (target == BuildTarget.StandaloneOSX)
                return Path.Combine(runRoot, "macOS", "C6_Physics_Lab.app");
            if (target == BuildTarget.iOS)
                return Path.Combine(runRoot, "iOS");
            throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported C6 Lab build target.");
        }

        private static void Build(string output, BuildTarget target)
        {
            if (!File.Exists(Path.Combine(ProjectRoot, Scene)))
                throw new InvalidOperationException("Create Lab.unity first: C6 Lab/Build Scene.");

            // Never replace an earlier export, including an explicit C6_LAB_BUILD_OUT destination.
            if (File.Exists(output) || Directory.Exists(output))
                throw new InvalidOperationException("C6_LAB_BUILD_OUTPUT_EXISTS output=" + output
                    + ". Choose a new C6_LAB_BUILD_OUT path to preserve the previous build.");

            string parent = Path.GetDirectoryName(output);
            if (string.IsNullOrEmpty(parent))
                throw new InvalidOperationException("C6_LAB_BUILD_OUTPUT_INVALID output=" + output);
            Directory.CreateDirectory(parent);

            Debug.Log("C6_LAB_BUILD_START target=" + target + " output=" + output);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = output,
                target = target,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report == null || report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("C6_LAB_BUILD_FAILED target=" + target
                    + " result=" + (report == null ? "NoReport" : report.summary.result.ToString())
                    + " errors=" + (report == null ? "Unknown" : report.summary.totalErrors.ToString())
                    + " output=" + output);

            SessionState.SetString(LastBuildOutputKey, output);
            Debug.Log("C6_LAB_BUILD_OK target=" + target + " output=" + output + " size=" + report.summary.totalSize
                + ". Open with C6 Lab/Open Last Build Folder.");
        }
    }
}
