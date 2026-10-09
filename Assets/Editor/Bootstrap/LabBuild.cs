using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace C6Lab.Editor
{
    public static class LabBuild
    {
        private const string Scene = "Assets/Lab/Scenes/Lab.unity";

        [MenuItem("C6 Lab/Build Mac App")]
        public static void Mac()
        {
            string output = Environment.GetEnvironmentVariable("C6_LAB_BUILD_OUT");
            if (string.IsNullOrWhiteSpace(output)) output = "/private/tmp/C6_Physics_Lab.app";
            Build(output, BuildTarget.StandaloneOSX);
        }

        [MenuItem("C6 Lab/Export iOS Xcode Project")]
        public static void Ios()
        {
            string output = Environment.GetEnvironmentVariable("C6_LAB_BUILD_OUT");
            if (string.IsNullOrWhiteSpace(output)) output = "/private/tmp/C6_Physics_Lab_iOS";
            Build(output, BuildTarget.iOS);
        }

        private static void Build(string output, BuildTarget target)
        {
            if (!System.IO.File.Exists(Scene)) throw new InvalidOperationException("Create Lab.unity first: C6 Lab/Build Scene.");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = output,
                target = target,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("C6_LAB_BUILD_FAILED target=" + target + " result=" + report.summary.result + " errors=" + report.summary.totalErrors);
            UnityEngine.Debug.Log("C6_LAB_BUILD_OK target=" + target + " output=" + output + " size=" + report.summary.totalSize);
        }
    }
}
