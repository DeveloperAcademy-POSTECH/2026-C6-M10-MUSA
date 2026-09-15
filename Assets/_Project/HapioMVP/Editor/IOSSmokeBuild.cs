using System;
using System.IO;
using System.Linq;
using C6.Prototype;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6.Editor
{
    public static class IOSSmokeBuild
    {
        public const string ScenePath = "Assets/_Project/HapioMVP/Scenes/IOSBuildSmoke.unity";
        public const string StampPath = "Assets/_Project/HapioMVP/Config/IOSBuildStamp.asset";
        public const string DevelopmentBundleId = "com.wolfuraark.c6prototype";

        [MenuItem("C6/T01/Prepare Build A Scene")]
        public static void PrepareA() => Prepare("C6-T01-A", "1");

        [MenuItem("C6/T01/Prepare Build B Scene")]
        public static void PrepareB() => Prepare("C6-T01-B", "2");

        [MenuItem("C6/T01/Export iOS Build A")]
        public static void ExportA() => Export("A", "C6-T01-A", "1");

        [MenuItem("C6/T01/Export iOS Build B")]
        public static void ExportB() => Export("B", "C6-T01-B", "2");

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void Prepare(string buildId, string revision)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before preparing the T01 scene.");
            if (!Application.isBatchMode && AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                for (var i = 0; i < SceneManager.sceneCount; i++)
                    if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                        throw new InvalidOperationException("Save the open untitled scene first, or prepare T01 in an isolated verification copy. The open scene will not be replaced.");
            }
            EnsureFolder("Assets/_Project/HapioMVP/Scenes");
            EnsureFolder("Assets/_Project/HapioMVP/Config");
            var stamp = AssetDatabase.LoadAssetAtPath<IOSBuildStamp>(StampPath);
            if (stamp == null)
            {
                stamp = ScriptableObject.CreateInstance<IOSBuildStamp>();
                AssetDatabase.CreateAsset(stamp, StampPath);
            }
            stamp.BuildId = buildId;
            stamp.Revision = revision;
            EditorUtility.SetDirty(stamp);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                // Create the dedicated test scene additively so an open user scene is not replaced.
                var prior = SceneManager.GetActiveScene();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    var cameraObject = new GameObject("T01 UI Camera", typeof(Camera));
                    var camera = cameraObject.GetComponent<Camera>();
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.04f, 0.06f, 0.1f);
                    camera.orthographic = true;
                    var root = new GameObject("T01 Smoke UI");
                    root.AddComponent<IOSBuildSmokeController>().Configure(stamp);
                    if (!EditorSceneManager.SaveScene(scene, ScenePath))
                        throw new InvalidOperationException("Could not save the T01 scene.");
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
            PlayerSettings.iOS.buildNumber = revision;
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
                string.IsNullOrWhiteSpace(bundleId) ? DevelopmentBundleId : bundleId);

            // Preserve existing scene entries but build only the smoke scene in T01.
            var previous = EditorBuildSettings.scenes.Where(scene => scene.path != ScenePath)
                .Select(scene => new EditorBuildSettingsScene(scene.path, false));
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(previous).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("C6_T01_PREPARED " + buildId + " scene=" + ScenePath);
        }

        [Serializable]
        private sealed class ExportSummary
        {
            public string buildId;
            public string revision;
            public string unityVersion;
            public string result;
            public string outputPath;
            public int errors;
            public int warnings;
            public double durationSeconds;
            public string scope = "Unity Xcode project export only; not Xcode app build, signing, or device run.";
        }

        private static void Export(string variant, string buildId, string revision)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
                throw new InvalidOperationException("The Editor does not support the iOS build target.");
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var outputRoot = Environment.GetEnvironmentVariable("C6_T01_OUTPUT_ROOT");
            if (string.IsNullOrWhiteSpace(outputRoot)) outputRoot = Path.Combine(projectRoot, "Builds", "iOS", "T01");
            var outputPath = Path.GetFullPath(Path.Combine(outputRoot, variant));
            // Existing exports may contain manual signing changes. A new explicit output root is required.
            if (Directory.Exists(outputPath) && Directory.EnumerateFileSystemEntries(outputPath).Any())
                throw new InvalidOperationException("Export folder is not empty; choose a new C6_T01_OUTPUT_ROOT to preserve prior output.");
            Prepare(buildId, revision);
            Directory.CreateDirectory(outputPath);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                options = BuildOptions.Development
            });
            var summary = new ExportSummary
            {
                buildId = buildId, revision = revision, unityVersion = Application.unityVersion,
                result = report.summary.result.ToString(), outputPath = outputPath,
                errors = report.summary.totalErrors, warnings = report.summary.totalWarnings,
                durationSeconds = report.summary.totalTime.TotalSeconds
            };
            var logs = Path.Combine(projectRoot, "Logs", "T01");
            Directory.CreateDirectory(logs);
            var receipt = "export-" + variant + "-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N") + ".json";
            File.WriteAllText(Path.Combine(logs, receipt), JsonUtility.ToJson(summary, true));
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("T01 iOS export failed: " + report.summary.result);
            if (!File.Exists(Path.Combine(outputPath, "Unity-iPhone.xcodeproj", "project.pbxproj")))
                throw new FileNotFoundException("Successful export did not produce the expected Xcode project.");
            Debug.Log("C6_T01_EXPORT_COMPLETE " + buildId);
        }
    }
}
