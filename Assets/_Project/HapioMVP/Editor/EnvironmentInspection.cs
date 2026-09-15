using System;
using System.IO;
using System.Linq;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

namespace C6.Editor
{
    // Read-only project inspection. Does not change settings, build a player, or run a scene.
    public static class EnvironmentInspection
    {
        [Serializable]
        private sealed class PackageRecord
        {
            public string name;
            public string version;
        }

        [Serializable]
        private sealed class Report
        {
            public string observedAtUtc;
            public string unityVersion;
            public string activeBuildTarget;
            public bool iosBuildTargetSupported;
            public string productName;
            public string defaultOrientation;
            public string iosBundleIdentifier;
            public string[] enabledBuildScenes;
            public PackageRecord[] packages;
            public string scope;
        }

        [MenuItem("C6/Inspect Development Environment")]
        public static void WriteReport()
        {
            var report = new Report
            {
                observedAtUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                iosBuildTargetSupported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS),
                productName = PlayerSettings.productName,
                defaultOrientation = PlayerSettings.defaultInterfaceOrientation.ToString(),
                iosBundleIdentifier = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS),
                enabledBuildScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                packages = PackageInfo.GetAllRegisteredPackages().OrderBy(package => package.name)
                    .Select(package => new PackageRecord { name = package.name, version = package.version }).ToArray(),
                scope = "T00 environment inspection only. No player build, gameplay test, signing, or device execution."
            };

            var outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "T00"));
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "environment.json"), JsonUtility.ToJson(report, true));
            Debug.Log("C6_T00_INSPECTION_COMPLETE iosBuildTargetSupported=" + report.iosBuildTargetSupported);
            if (!report.iosBuildTargetSupported)
                throw new InvalidOperationException("The installed Editor does not report iOS build support.");
        }
    }
}
