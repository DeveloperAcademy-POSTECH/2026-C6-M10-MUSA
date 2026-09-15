using System;
using System.IO;
using System.Linq;
using C6.Prototype.Networking;
using C6.Prototype.Orbs;
using C6.Prototype.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace C6.Editor
{
    /// <summary>Builds a separate T05 fixture scene while keeping the saved T04 presentation intact.</summary>
    public static class OrbInputBuild
    {
        public const string ScenePath = "Assets/_Project/HapioMVP/Scenes/OrbInputSmoke.unity";
        public const string ConfigPath = "Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset";
        public const string SourceScenePath = "Assets/_Project/HapioMVP/Scenes/BattleLayout.unity";
        public const string SpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        public const string LocalNetworkPurpose = DirectConnectionBuild.LocalNetworkPurpose;

        [MenuItem("C6/T05/Prepare Orb Input Scene")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before preparing T05.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
                throw new InvalidOperationException("T05 requires the existing saved T04 scene; it will not recreate or replace it.");
            var config = AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath);
            if (config == null)
                throw new InvalidOperationException("T05 requires the existing shared ScreenLayoutConfig asset.");
            var spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            if (spriteMaterial == null)
                throw new InvalidOperationException("The installed URP Sprite Unlit material is unavailable.");

            bool createScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null;
            if (createScene && File.Exists(ScenePath))
                throw new InvalidOperationException("The T05 scene path is occupied by another or unreadable asset; preserving it.");
            if (createScene && !Application.isBatchMode)
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                        throw new InvalidOperationException("Save the open untitled scene or use a verification copy. T05 will not replace it.");

            // Serialize newly introduced field defaults on the same asset and GUID. Existing tuning
            // values, including the upper/lower split, are never assigned by this setup operation.
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            if (createScene)
            {
                EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
                CreateDerivedScene();
            }

            PlayerSettings.productName = "C6 Prototype";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.iOS.buildNumber = "7";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            string bundleId = Environment.GetEnvironmentVariable("C6_IOS_BUNDLE_ID");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS,
                string.IsNullOrWhiteSpace(bundleId) ? "com.wolfuraark.c6prototype" : bundleId);
            var previous = EditorBuildSettings.scenes.Where(s => s.path != ScenePath)
                .Select(s => new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(previous).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("C6_T05_PREPARED scene=" + ScenePath + " config=" + ConfigPath + " build=7");
        }

        static void CreateDerivedScene()
        {
            Scene originalActive = SceneManager.GetActiveScene();
            Scene preview = default;
            Scene target = default;
            try
            {
                // Preview loads the saved asset independently of any open, possibly dirty T04 scene.
                preview = EditorSceneManager.OpenPreviewScene(SourceScenePath);
                if (!preview.IsValid() || !preview.isLoaded)
                    throw new InvalidOperationException("Could not open the saved T04 scene as a preview.");
                var sourceRoots = preview.GetRootGameObjects();
                var sourceLayouts = sourceRoots.SelectMany(root => root.GetComponentsInChildren<SplitScreenLayout>(true)).ToArray();
                if (sourceLayouts.Length != 1 || sourceLayouts[0].BattleCamera == null || sourceLayouts[0].OrbCamera == null)
                    throw new InvalidOperationException("The saved T04 scene must contain one wired SplitScreenLayout.");
                string battleCameraName = sourceLayouts[0].BattleCamera.name;
                string orbCameraName = sourceLayouts[0].OrbCamera.name;
                if (battleCameraName == orbCameraName)
                    throw new InvalidOperationException("T04 cameras need distinct names for safe scene reference remapping.");

                target = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
                SceneManager.SetActiveScene(target);
                foreach (var source in sourceRoots)
                {
                    var copy = UnityEngine.Object.Instantiate(source);
                    copy.name = source.name;
                    SceneManager.MoveGameObjectToScene(copy, target);
                }

                var roots = target.GetRootGameObjects();
                foreach (var oldHud in roots.SelectMany(root => root.GetComponentsInChildren<T04Hud>(true)).ToArray())
                    UnityEngine.Object.DestroyImmediate(oldHud);
                foreach (var oldCapture in roots.SelectMany(root => root.GetComponentsInChildren<T04Capture>(true)).ToArray())
                    UnityEngine.Object.DestroyImmediate(oldCapture);

                var layouts = roots.SelectMany(root => root.GetComponentsInChildren<SplitScreenLayout>(true)).ToArray();
                var cameras = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).ToArray();
                var battleCameras = cameras.Where(camera => camera.name == battleCameraName).ToArray();
                var orbCameras = cameras.Where(camera => camera.name == orbCameraName).ToArray();
                if (layouts.Length != 1 || battleCameras.Length != 1 || orbCameras.Length != 1 || cameras.Length != 2)
                    throw new InvalidOperationException("The copied T04 layout/cameras could not be remapped unambiguously.");
                var config = AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath);
                var material = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
                if (config == null || material == null)
                    throw new InvalidOperationException("T05 shared assets could not be reloaded after scene creation.");

                var layout = layouts[0];
                layout.gameObject.name = "T05OrbInput";
                layout.Configure(config, battleCameras[0], orbCameras[0]);
                var hud = layout.gameObject.AddComponent<T05Hud>();
                hud.Configure(layout);
                var session = layout.gameObject.AddComponent<DirectConnectionSession>();
                var controller = layout.gameObject.AddComponent<T05OrbController>();
                controller.Configure(layout, session, hud, material);
                layout.gameObject.AddComponent<OrbPointerInput>().Configure(controller);
                layout.gameObject.AddComponent<T05Probe>().Configure(controller);

                // New scene render settings belong to the new scene, not to the preview or open T04.
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.37f, .42f, .49f);
                RenderSettings.fog = false;
                if (!EditorSceneManager.SaveScene(target, ScenePath))
                    throw new InvalidOperationException("Could not save the separate T05 scene.");
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                if (!Application.isBatchMode)
                {
                    if (target.IsValid() && target.isLoaded) EditorSceneManager.CloseScene(target, true);
                    if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
                }
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("Cannot create asset folder " + path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [MenuItem("C6/T05/Export iOS")]
        public static void ExportIOS() => Build(BuildTarget.iOS, "iOS");

        [MenuItem("C6/T05/Build macOS Orb Input")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "macOS/C6OrbInput.app");

        static void Build(BuildTarget target, string relativeOutput)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new InvalidOperationException("Missing build support: " + target);
            // URP resource/variant collection uses the Editor's active target. BuildPlayer's
            // requested target alone can collect iOS/Mobile resources for a macOS/PC player.
            // Reject before Prepare or any output writes; callers must select the real target.
            if (EditorUserBuildSettings.activeBuildTarget != target)
                throw new InvalidOperationException("Active build target is " + EditorUserBuildSettings.activeBuildTarget +
                    " but T05 requested " + target + ". Select the matching platform, or restart batch Unity with -buildTarget " + target + ".");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputRoot = Environment.GetEnvironmentVariable("C6_T05_OUTPUT_ROOT");
            if (string.IsNullOrWhiteSpace(outputRoot)) outputRoot = Path.Combine(root, "Builds", "T05");
            string path = Path.GetFullPath(Path.Combine(outputRoot, relativeOutput));
            // Do this before Prepare so a build-path collision cannot change scenes or settings.
            if (File.Exists(path) || Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
                throw new InvalidOperationException("Output exists; use a new C6_T05_OUTPUT_ROOT to preserve previous builds.");
            Prepare();
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var receipt = new BuildReceipt
            {
                target = target.ToString(), activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                output = path, unityVersion = Application.unityVersion,
                executedAtUtc = DateTime.UtcNow.ToString("O")
            };
            BuildReport report = null;
            DateTime started = DateTime.UtcNow;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, locationPathName = path,
                    target = target, options = BuildOptions.Development
                });
                receipt.result = report.summary.result.ToString();
                receipt.errors = report.summary.totalErrors;
                receipt.warnings = report.summary.totalWarnings;
                receipt.durationSeconds = report.summary.totalTime.TotalSeconds;
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("T05 build failed: " + report.summary.result);
                if (target == BuildTarget.iOS)
                {
                    if (!File.Exists(Path.Combine(path, "Unity-iPhone.xcodeproj/project.pbxproj")))
                        throw new FileNotFoundException("Missing exported Xcode project.");
                    string plistPath = Path.Combine(path, "Info.plist");
                    if (!File.Exists(plistPath)) throw new FileNotFoundException("Missing exported iOS Info.plist.");
                    var plist = new PlistDocument();
                    plist.ReadFromFile(plistPath);
                    if (!plist.root.values.TryGetValue("NSLocalNetworkUsageDescription", out var purpose) ||
                        purpose.AsString() != LocalNetworkPurpose)
                        throw new InvalidOperationException("Local Network usage description was not applied.");
                }
                receipt.outputValidation = "PASS";
                Debug.Log("C6_T05_BUILD_COMPLETE target=" + target + " output=" + path);
            }
            catch (Exception exception)
            {
                receipt.outputValidation = "FAIL";
                receipt.exceptionType = exception.GetType().FullName;
                if (report == null)
                {
                    receipt.result = "Exception";
                    receipt.errors = 1;
                    receipt.durationSeconds = (DateTime.UtcNow - started).TotalSeconds;
                }
                throw;
            }
            finally
            {
                string logs = Path.Combine(root, "Logs", "T05");
                Directory.CreateDirectory(logs);
                File.WriteAllText(Path.Combine(logs, "build-" + target + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".json"),
                    JsonUtility.ToJson(receipt, true));
            }
        }

        [Serializable]
        sealed class BuildReceipt
        {
            public string buildId = "C6-T05";
            public string buildNumber = "7";
            public string target;
            public string activeBuildTarget;
            public string unityVersion;
            public string result = "NOT_RUN";
            public string output;
            public string executedAtUtc;
            public string outputValidation = "NOT_RUN";
            public string exceptionType;
            public int errors;
            public int warnings;
            public double durationSeconds;
        }
    }
}
