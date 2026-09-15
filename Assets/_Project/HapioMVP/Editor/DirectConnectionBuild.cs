using System;
using System.IO;
using System.Linq;
using C6.Prototype.Networking;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6.Editor
{
    public static class DirectConnectionBuild
    {
        public const string ScenePath = "Assets/_Project/HapioMVP/Scenes/DirectConnectionSmoke.unity";
        public const string LocalNetworkPurpose = "Connect to another iPhone or iPad on the same Wi-Fi for the C6 cooperative prototype.";

        [MenuItem("C6/T02/Prepare Direct Connection Scene")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before preparing T02.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                if (!Application.isBatchMode)
                    for (var i = 0; i < SceneManager.sceneCount; i++)
                        if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                            throw new InvalidOperationException("Save the open untitled scene or use a verification copy. T02 will not replace it.");
                var prior = SceneManager.GetActiveScene();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    var camera = new GameObject("T02 UI Camera", typeof(Camera)).GetComponent<Camera>();
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.045f, 0.065f, 0.1f);
                    camera.orthographic = true;
                    var root = new GameObject("T02 Direct Connection");
                    root.AddComponent<DirectConnectionSession>();
                    root.AddComponent<DirectConnectionView>();
                    root.AddComponent<DirectConnectionAutomation>();
                    if (!EditorSceneManager.SaveScene(scene, ScenePath))
                        throw new InvalidOperationException("Could not save the T02 scene.");
                }
                finally
                {
                    if (!Application.isBatchMode)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                        if (prior.IsValid() && prior.isLoaded) SceneManager.SetActiveScene(prior);
                    }
                }
            }
            PlayerSettings.productName = "C6 Prototype";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.iOS.buildNumber = "3";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            var bundleId = Environment.GetEnvironmentVariable("C6_IOS_BUNDLE_ID");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS,
                string.IsNullOrWhiteSpace(bundleId) ? "com.wolfuraark.c6prototype" : bundleId);
            var previous = EditorBuildSettings.scenes.Where(s => s.path != ScenePath)
                .Select(s => new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(previous).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("C6_T02_PREPARED scene=" + ScenePath);
        }

        [MenuItem("C6/T02/Export iOS")]
        public static void ExportIOS() => Build(BuildTarget.iOS, "iOS");

        [MenuItem("C6/T02/Build macOS Connection Test")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "macOS/C6Connection.app");

        private static void Build(BuildTarget target, string relativeOutput)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new InvalidOperationException("Missing build support: " + target);
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var outputRoot = Environment.GetEnvironmentVariable("C6_T02_OUTPUT_ROOT");
            if (string.IsNullOrWhiteSpace(outputRoot)) outputRoot = Path.Combine(root, "Builds", "T02");
            var path = Path.GetFullPath(Path.Combine(outputRoot, relativeOutput));
            if (File.Exists(path) || Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
                throw new InvalidOperationException("Output exists; use a new C6_T02_OUTPUT_ROOT to preserve previous builds.");
            Prepare();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = path,
                target = target, options = BuildOptions.Development
            });
            var receipt = new BuildReceipt
            {
                target = target.ToString(), result = report.summary.result.ToString(), output = path,
                unityVersion = Application.unityVersion, errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings, durationSeconds = report.summary.totalTime.TotalSeconds
            };
            var logs = Path.Combine(root, "Logs", "T02");
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "build-" + target + "-" + Guid.NewGuid().ToString("N") + ".json"),
                JsonUtility.ToJson(receipt, true));
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("T02 build failed: " + report.summary.result);
            if (target == BuildTarget.iOS)
            {
                if (!File.Exists(Path.Combine(path, "Unity-iPhone.xcodeproj/project.pbxproj")))
                    throw new FileNotFoundException("Missing exported Xcode project.");
                var plist = new PlistDocument();
                plist.ReadFromFile(Path.Combine(path, "Info.plist"));
                if (plist.root["NSLocalNetworkUsageDescription"].AsString() != LocalNetworkPurpose)
                    throw new InvalidOperationException("Local Network usage description was not applied.");
            }
            Debug.Log("C6_T02_BUILD_COMPLETE target=" + target + " output=" + path);
        }

        [PostProcessBuild(100)]
        public static void ApplyLocalNetworkPurpose(BuildTarget target, string output)
        {
            if (target != BuildTarget.iOS) return;
            var path = Path.Combine(output, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(path);
            plist.root.SetString("NSLocalNetworkUsageDescription", LocalNetworkPurpose);
            // Direct unicast only. Do not add Bonjour declarations or multicast entitlements.
            plist.WriteToFile(path);
            Debug.Log("C6_T02_LOCAL_NETWORK_PLIST_APPLIED");
        }

        [Serializable]
        private sealed class BuildReceipt
        {
            public string buildId = "C6-T02";
            public string target;
            public string unityVersion;
            public string result;
            public string output;
            public int errors;
            public int warnings;
            public double durationSeconds;
        }
    }
}
