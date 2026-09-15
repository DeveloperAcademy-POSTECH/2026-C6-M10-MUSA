using System;
using System.IO;
using System.Linq;
using C6.Prototype.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace C6.Editor
{
    public static class BattleLayoutBuild
    {
        public const string ScenePath = "Assets/_Project/HapioMVP/Scenes/BattleLayout.unity";
        public const string ConfigPath = "Assets/_Project/HapioMVP/Config/ScreenLayoutConfig.asset";
        public const string MaterialFolder = "Assets/_Project/HapioMVP/Presentation/Materials";
        public const string LocalNetworkPurpose = DirectConnectionBuild.LocalNetworkPurpose;

        [MenuItem("C6/T04/Prepare Battle Layout Scene")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before preparing T04.");

            var createScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null;
            if (createScene && File.Exists(ScenePath))
                throw new InvalidOperationException("The T04 scene path is occupied by an unreadable or different asset; preserving it.");
            if (createScene && !Application.isBatchMode)
                for (var i = 0; i < SceneManager.sceneCount; i++)
                    if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                        throw new InvalidOperationException("Save the open untitled scene or use a verification copy. T04 will not replace it.");

            EnsureFolder(Path.GetDirectoryName(ConfigPath).Replace('\\', '/'));
            var config = AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath);
            if (config == null)
            {
                if (File.Exists(ConfigPath))
                    throw new InvalidOperationException("The T04 Config path is occupied by a different asset; preserving it.");
                config = ScriptableObject.CreateInstance<ScreenLayoutConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            AssetDatabase.SaveAssets();
            var layers = EnsureLayers();
            if (createScene)
            {
                EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
                EnsureFolder(MaterialFolder);
                var prior = SceneManager.GetActiveScene();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    // A Single scene switch can unload the newly created asset instance.
                    config = AssetDatabase.LoadAssetAtPath<ScreenLayoutConfig>(ConfigPath);
                    if (config == null) throw new InvalidOperationException("T04 Config could not be reloaded after scene creation.");
                    CreatePresentation(config, layers[0], layers[1], layers[2]);
                    if (!EditorSceneManager.SaveScene(scene, ScenePath))
                        throw new InvalidOperationException("Could not save the T04 scene.");
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
            PlayerSettings.iOS.buildNumber = "6";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            var bundleId = Environment.GetEnvironmentVariable("C6_IOS_BUNDLE_ID");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS,
                string.IsNullOrWhiteSpace(bundleId) ? "com.wolfuraark.c6prototype" : bundleId);
            var previous = EditorBuildSettings.scenes.Where(s => s.path != ScenePath)
                .Select(s => new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(previous).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("C6_T04_PREPARED scene=" + ScenePath + " config=" + ConfigPath + " upperFraction=" + config.UpperFraction);
        }

        private static void CreatePresentation(ScreenLayoutConfig config, int battleLayer, int orbLayer, int backgroundLayer)
        {
            var battleCamera = CreateCamera("T04 Battle Camera", false, 0f,
                (1 << battleLayer) | (1 << backgroundLayer), new Color(0.035f, 0.057f, 0.082f));
            battleCamera.fieldOfView = 42f;
            battleCamera.transform.position = new Vector3(0f, 3.1f, -9f);
            battleCamera.transform.LookAt(new Vector3(0f, 2.2f, 0f));
            battleCamera.gameObject.AddComponent<AudioListener>();
            battleCamera.tag = "MainCamera";

            var orbCamera = CreateCamera("T04 Orb Camera", true, 1f, 1 << orbLayer,
                new Color(0.029f, 0.045f, 0.069f));
            orbCamera.orthographicSize = 3.2f;
            orbCamera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);

            var root = new GameObject("T04 Battle Presentation");
            var layout = root.AddComponent<SplitScreenLayout>();
            layout.Configure(config, battleCamera, orbCamera);
            root.AddComponent<T04Hud>().Configure(layout);
            root.AddComponent<T04Capture>().Configure(layout);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.37f, 0.42f, 0.49f);
            RenderSettings.fog = false;
            var light = new GameObject("T04 Key Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.94f, 0.82f);
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
            light.cullingMask = (1 << battleLayer) | (1 << backgroundLayer);
            light.transform.rotation = Quaternion.Euler(42f, -32f, 0f);

            var wood = Material("DummyOchre", new Color(0.75f, 0.49f, 0.22f), false, 0.18f);
            var ivory = Material("DummyIvory", new Color(0.92f, 0.85f, 0.66f), false, 0.16f);
            var dark = Material("StageNavy", new Color(0.065f, 0.1f, 0.15f), false, 0.2f);
            var backdrop = Material("Backdrop", new Color(0.08f, 0.13f, 0.18f), false, 0.1f);
            var teal = Material("TealAccent", new Color(0.22f, 0.71f, 0.65f), false, 0.25f);
            var eye = Material("DummyEyes", new Color(0.045f, 0.055f, 0.07f), false, 0.12f);
            var grid = Material("OrbAreaGrid", new Color(0.048f, 0.073f, 0.098f), true, 0f);

            var stage = new GameObject("T04 Visual Stage").transform;
            Primitive("Floor", PrimitiveType.Plane, stage, backgroundLayer, backdrop,
                new Vector3(0f, -0.04f, 1f), new Vector3(1.4f, 1f, 1.4f));
            Primitive("Left Backdrop Pillar", PrimitiveType.Cube, stage, backgroundLayer, dark,
                new Vector3(-2.5f, 1.6f, 2f), new Vector3(0.38f, 3.2f, 0.38f));
            Primitive("Right Backdrop Pillar", PrimitiveType.Cube, stage, backgroundLayer, dark,
                new Vector3(2.5f, 1.6f, 2f), new Vector3(0.38f, 3.2f, 0.38f));
            Primitive("Training Platform", PrimitiveType.Cylinder, stage, battleLayer, dark,
                new Vector3(0f, 0.06f, 0f), new Vector3(2.2f, 0.06f, 2.2f));
            Primitive("Platform Teal Rim", PrimitiveType.Cylinder, stage, battleLayer, teal,
                new Vector3(0f, 0.13f, 0f), new Vector3(2.05f, 0.012f, 2.05f));
            Primitive("Platform Top", PrimitiveType.Cylinder, stage, battleLayer, dark,
                new Vector3(0f, 0.148f, 0f), new Vector3(1.98f, 0.012f, 1.98f));

            var dummy = new GameObject("T04 Training Dummy - Visual Only").transform;
            dummy.SetParent(stage, false);
            Primitive("Left Leg", PrimitiveType.Cylinder, dummy, battleLayer, wood,
                new Vector3(-0.27f, 0.55f, 0f), new Vector3(0.2f, 0.39f, 0.2f));
            Primitive("Right Leg", PrimitiveType.Cylinder, dummy, battleLayer, wood,
                new Vector3(0.27f, 0.55f, 0f), new Vector3(0.2f, 0.39f, 0.2f));
            Primitive("Torso", PrimitiveType.Capsule, dummy, battleLayer, wood,
                new Vector3(0f, 1.39f, 0f), new Vector3(0.8f, 0.62f, 0.56f));
            Primitive("Head", PrimitiveType.Sphere, dummy, battleLayer, ivory,
                new Vector3(0f, 2.25f, 0f), new Vector3(0.7f, 0.7f, 0.66f));
            Primitive("Left Arm", PrimitiveType.Cylinder, dummy, battleLayer, ivory,
                new Vector3(-0.66f, 1.58f, 0f), new Vector3(0.21f, 0.39f, 0.21f), new Vector3(0f, 0f, 90f));
            Primitive("Right Arm", PrimitiveType.Cylinder, dummy, battleLayer, ivory,
                new Vector3(0.66f, 1.58f, 0f), new Vector3(0.21f, 0.39f, 0.21f), new Vector3(0f, 0f, 90f));
            Primitive("Chest Target Rim", PrimitiveType.Cylinder, dummy, battleLayer, ivory,
                new Vector3(0f, 1.43f, -0.29f), new Vector3(0.3f, 0.012f, 0.3f), new Vector3(90f, 0f, 0f));
            Primitive("Chest Target Center", PrimitiveType.Cylinder, dummy, battleLayer, teal,
                new Vector3(0f, 1.43f, -0.31f), new Vector3(0.13f, 0.012f, 0.13f), new Vector3(90f, 0f, 0f));
            Primitive("Left Eye", PrimitiveType.Sphere, dummy, battleLayer, eye,
                new Vector3(-0.12f, 2.3f, -0.303f), new Vector3(0.063f, 0.079f, 0.04f));
            Primitive("Right Eye", PrimitiveType.Sphere, dummy, battleLayer, eye,
                new Vector3(0.12f, 2.3f, -0.303f), new Vector3(0.063f, 0.079f, 0.04f));

            var lowerArea = new GameObject("T04 Empty Orb Area").transform;
            // Presentation geometry is in world units. Input bounds always come from the camera pixel rect.
            for (var offset = -6; offset <= 6; offset++)
            {
                Primitive("Grid Vertical " + offset, PrimitiveType.Quad, lowerArea, orbLayer, grid,
                    new Vector3(offset * 0.6f, 0f, 0f), new Vector3(0.007f, 12f, 1f));
                Primitive("Grid Horizontal " + offset, PrimitiveType.Quad, lowerArea, orbLayer, grid,
                    new Vector3(0f, offset * 0.6f, 0f), new Vector3(12f, 0.007f, 1f));
            }
        }

        private static Camera CreateCamera(string name, bool orthographic, float depth, int mask, Color color)
        {
            var camera = new GameObject(name, typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = color;
            camera.orthographic = orthographic;
            camera.depth = depth;
            camera.cullingMask = mask;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            camera.targetTexture = null;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Base;
            data.SetRenderer(-1);
            data.renderPostProcessing = false;
            data.cameraStack.Clear();
            return camera;
        }

        private static void Primitive(string name, PrimitiveType type, Transform parent, int layer, Material material,
            Vector3 position, Vector3 scale, Vector3 euler = default)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.layer = layer;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.transform.localEulerAngles = euler;
            item.GetComponent<Renderer>().sharedMaterial = material;
            foreach (var collider in item.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
        }

        private static Material Material(string name, Color color, bool unlit, float smoothness)
        {
            var path = MaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            if (File.Exists(path)) throw new InvalidOperationException("Material path is occupied; preserving " + path);
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The installed URP shader is unavailable.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static int[] EnsureLayers()
        {
            var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").FirstOrDefault();
            if (tagManager == null) throw new InvalidOperationException("Could not read project layers.");
            var serialized = new SerializedObject(tagManager);
            var layers = serialized.FindProperty("layers");
            var names = new[] { "C6Battle", "C6Orbs", "C6Background" };
            var result = new int[names.Length];
            for (var n = 0; n < names.Length; n++)
            {
                var existing = -1;
                var free = -1;
                for (var i = 8; i < layers.arraySize; i++)
                {
                    var value = layers.GetArrayElementAtIndex(i).stringValue;
                    if (value == names[n]) existing = i;
                    if (free < 0 && string.IsNullOrEmpty(value)) free = i;
                }
                if (existing >= 0) result[n] = existing;
                else
                {
                    if (free < 0) throw new InvalidOperationException("No free user layer for " + names[n] + "; no occupied layer was replaced.");
                    layers.GetArrayElementAtIndex(free).stringValue = names[n];
                    result[n] = free;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return result;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [MenuItem("C6/T04/Export iOS")]
        public static void ExportIOS() => Build(BuildTarget.iOS, "iOS");

        [MenuItem("C6/T04/Build macOS Layout")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "macOS/C6Layout.app");

        private static void Build(BuildTarget target, string relativeOutput)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new InvalidOperationException("Missing build support: " + target);
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var outputRoot = Environment.GetEnvironmentVariable("C6_T04_OUTPUT_ROOT");
            if (string.IsNullOrWhiteSpace(outputRoot)) outputRoot = Path.Combine(root, "Builds", "T04");
            var path = Path.GetFullPath(Path.Combine(outputRoot, relativeOutput));
            if (File.Exists(path) || Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
                throw new InvalidOperationException("Output exists; use a new C6_T04_OUTPUT_ROOT to preserve previous builds.");
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
                warnings = report.summary.totalWarnings, durationSeconds = report.summary.totalTime.TotalSeconds,
                executedAtUtc = DateTime.UtcNow.ToString("O")
            };
            var logs = Path.Combine(root, "Logs", "T04");
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "build-" + target + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".json"),
                JsonUtility.ToJson(receipt, true));
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("T04 build failed: " + report.summary.result);
            if (target == BuildTarget.iOS)
            {
                if (!File.Exists(Path.Combine(path, "Unity-iPhone.xcodeproj/project.pbxproj")))
                    throw new FileNotFoundException("Missing exported Xcode project.");
                var plist = new PlistDocument();
                plist.ReadFromFile(Path.Combine(path, "Info.plist"));
                if (plist.root["NSLocalNetworkUsageDescription"].AsString() != LocalNetworkPurpose)
                    throw new InvalidOperationException("Local Network usage description was not applied.");
            }
            Debug.Log("C6_T04_BUILD_COMPLETE target=" + target + " output=" + path);
        }

        [Serializable]
        private sealed class BuildReceipt
        {
            public string buildId = "C6-T04";
            public string buildNumber = "6";
            public string target;
            public string unityVersion;
            public string result;
            public string output;
            public string executedAtUtc;
            public int errors;
            public int warnings;
            public double durationSeconds;
        }
    }
}
