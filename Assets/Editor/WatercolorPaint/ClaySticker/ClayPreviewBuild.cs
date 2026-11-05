using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Hapio.Experiments;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>One frozen artwork, one isolated Scene, and an explicit Mac-only build.</summary>
    public static class ClayPreviewBuild
    {
        public const string Root = "Assets/Experiments/ClayPreview";
        public const string ScenePath = Root + "/Scenes/ClayPreview.unity";
        public const string ColorPath = Root + "/Sample/Color.png";
        public const string HeightPath = Root + "/Sample/Height.exr";
        public const string SampleInfoPath = Root + "/Sample/SampleInfo.json";
        public const string PanelSettingsPath = Root + "/ClayPreviewPanelSettings.asset";
        public const string StylePath = Root + "/Runtime/ClayPreview.uss";
        public const string ThemePath = Root + "/Runtime/ClayPreviewTheme.tss";
        private const string FontPath = "Assets/Lab/Art/Fonts/Shilla_Culture(M).otf";
        private const string LastBuildKey = "Hapio.ClayPreview.LastBuild";
        private const string ResultPath = "Temp/clay_preview_build_result.json";

        [Serializable]
        public sealed class SampleInfo
        {
            public string format = "clay-preview-sample-v1";
            public string unity, createdUtc, styleVersion, sourceConfigHash, authorityHash;
            public string colorFileHash, heightFileHash, colorRawHash, heightRawHash;
            public int seed = 123, width = 1024, height = 512, hits, cells, totalCells;
            public float colorRoundtripMaxError, heightRoundtripMaxError;
            public bool complete, sourceConfigPreserved, randomPreserved, scenesPreserved;
        }

        [Serializable]
        public sealed class Validation
        {
            public bool valid, complete, normalBuildScenesPreserved;
            public int width, height, hits, cells, checks;
            public string colorFileHash, heightFileHash;
        }

        [Serializable]
        public sealed class BuildStatus
        {
            public string state, output, error;
            public ulong bytes;
            public bool scenesPreserved, playerSettingsPreserved, playerSettingsDiskPreserved, activeTargetPreserved;
            public string playerSettingsBeforeHash, playerSettingsAfterHash;
        }

        [MenuItem("C6 Lab/Experiments/Clay Sticker/Prepare Mac Preview Scene")]
        public static void PrepareMenu() => Debug.Log("CLAY_PREVIEW_PREPARED " + Prepare());

        /// <summary>Existing sample maps remain frozen until an explicit rebake.</summary>
        public static string Prepare()
        {
            RequireStopped();
            bool any = File.Exists(ColorPath) || File.Exists(HeightPath) || File.Exists(SampleInfoPath);
            if (!any) BakeSample();
            else Require(File.Exists(ColorPath) && File.Exists(HeightPath) && File.Exists(SampleInfoPath),
                "The preview sample is incomplete. Use Rebake Completed Preview Sample to replace only its generated files.");
            CreateSceneIfMissing();
            return ValidatePreparedSample();
        }

        [MenuItem("C6 Lab/Experiments/Clay Sticker/Rebake Completed Preview Sample")]
        public static void RebakeMenu()
        {
            RequireStopped();
            BakeSample();
            CreateSceneIfMissing();
            Debug.Log("CLAY_PREVIEW_SAMPLE_REBAKED " + ValidatePreparedSample());
        }

        public static string BakeSample()
        {
            RequireStopped();
            Require(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null,
                "Sample baking needs the Editor's real graphics device.");
            EnsureFolder(Root + "/Sample");
            string scenes = SceneState(), random = JsonUtility.ToJson(UnityEngine.Random.state);
            LabConfig source = Resources.Load<LabConfig>("LabConfig");
            Require(source != null, "The saved LabConfig is missing.");
            string config = EditorJsonUtility.ToJson(source);
            var settings = new LabWatercolorStampSettings { mode = LabPaintStampMode.ClaySticker, revealSeconds = 0 };
            settings.Validate();
            var info = new SampleInfo
            {
                unity = Application.unityVersion, createdUtc = DateTime.UtcNow.ToString("O"),
                styleVersion = settings.Version, sourceConfigHash = Hash(System.Text.Encoding.UTF8.GetBytes(config))
            };
            using (var full = new LabClayVisualStudy.Fixture(source, settings, "standalone-frozen-five-color"))
            {
                const int samples = 256;
                for (int i = 0; i < samples && !full.Surface.IsComplete; i++)
                {
                    float y = 1 - 2 * ((i + .5f) / samples), angle = i * 2.39996323f;
                    float radius = Mathf.Sqrt(Mathf.Max(0, 1 - y * y));
                    var direction = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                    full.Hit(direction, (LabOrbElement)(1 + i % 5), 1 + i % 5, .3f + (i % 4) * .2f, true);
                }
                Require(full.Surface.IsComplete, "The bounded real-rule sample did not reach 100%.");
                full.Seal(); full.Finish();
                LabClayVisualStudy.Entry entry = full.Check();
                info.hits = entry.hits; info.cells = entry.cells; info.totalCells = full.Surface.CellCount;
                info.complete = entry.complete; info.authorityHash = entry.authorityAfter;
                Texture2D color = null, height = null;
                try
                {
                    color = ReadMap(full.Runtime.PaintTexture, TextureFormat.RGBA32);
                    height = ReadMap(full.Runtime.HeightTexture, TextureFormat.RGBAHalf);
                    Require(color.width == info.width && color.height == info.height
                        && height.width == info.width && height.height == info.height, "Unexpected sample map dimensions.");
                    Require(color.GetPixels32().All(p => p.a == 255), "The completed color map has transparent holes.");
                    info.colorRawHash = Hash(color.GetRawTextureData());
                    info.heightRawHash = Hash(height.GetRawTextureData());
                    File.WriteAllBytes(ColorPath, color.EncodeToPNG());
                    File.WriteAllBytes(HeightPath, height.EncodeToEXR(Texture2D.EXRFlags.None));
                    ImportMap(ColorPath, false, true); ImportMap(HeightPath, true, true);
                    info.colorRoundtripMaxError = Difference(color, AssetDatabase.LoadAssetAtPath<Texture2D>(ColorPath));
                    info.heightRoundtripMaxError = Difference(height, AssetDatabase.LoadAssetAtPath<Texture2D>(HeightPath));
                    Require(info.colorRoundtripMaxError <= .000001f, "Linear PNG import changed the color map.");
                    Require(info.heightRoundtripMaxError <= .00001f, "Half EXR import changed the relief map.");
                    ImportMap(ColorPath, false, false); ImportMap(HeightPath, true, false);
                    info.colorFileHash = Hash(File.ReadAllBytes(ColorPath));
                    info.heightFileHash = Hash(File.ReadAllBytes(HeightPath));
                }
                finally
                {
                    if (color != null) Object.DestroyImmediate(color);
                    if (height != null) Object.DestroyImmediate(height);
                }
                full.Check();
            }
            info.sourceConfigPreserved = config == EditorJsonUtility.ToJson(source);
            info.randomPreserved = random == JsonUtility.ToJson(UnityEngine.Random.state);
            info.scenesPreserved = scenes == SceneState();
            Require(info.sourceConfigPreserved && info.randomPreserved && info.scenesPreserved,
                "Sample baking changed shared Scene, LabConfig, or random state.");
            string json = JsonUtility.ToJson(info, true);
            File.WriteAllText(SampleInfoPath, json);
            AssetDatabase.ImportAsset(SampleInfoPath, ImportAssetOptions.ForceSynchronousImport);
            return json;
        }

        private static Texture2D ReadMap(RenderTexture source, TextureFormat format)
        {
            Require(source != null && source.IsCreated(), "The completed sample GPU map is unavailable.");
            var copy = new Texture2D(source.width, source.height, format, false, true)
                { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                copy.Apply(false, false);
                return copy;
            }
            catch { Object.DestroyImmediate(copy); throw; }
            finally { RenderTexture.active = previous; }
        }

        private static float Difference(Texture2D expected, Texture2D actual)
        {
            Require(actual != null && expected.width == actual.width && expected.height == actual.height,
                "The imported map dimensions do not match the actual GPU map.");
            Color[] a = expected.GetPixels(), b = actual.GetPixels(); float maximum = 0;
            for (int i = 0; i < a.Length; i++)
            {
                maximum = Mathf.Max(maximum, Mathf.Abs(a[i].r - b[i].r));
                maximum = Mathf.Max(maximum, Mathf.Abs(a[i].g - b[i].g));
                maximum = Mathf.Max(maximum, Mathf.Abs(a[i].b - b[i].b));
                maximum = Mathf.Max(maximum, Mathf.Abs(a[i].a - b[i].a));
            }
            Require(float.IsFinite(maximum), "Imported sample contains invalid numeric values.");
            return maximum;
        }

        private static void ImportMap(string path, bool height, bool readable)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, "The sample texture importer is missing.");
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.isReadable = readable;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.overridden = false;
            platform.format = height ? TextureImporterFormat.RGBAHalf : TextureImporterFormat.RGBA32;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            platform.maxTextureSize = 1024;
            importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport();
        }

        public static void CreateSceneIfMissing()
        {
            RequireStopped();
            if (File.Exists(ScenePath)) return;
            EnsureFolder(Root + "/Scenes");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabPaintStudyRenderer.PrefabPath);
            Texture2D color = AssetDatabase.LoadAssetAtPath<Texture2D>(ColorPath);
            Texture2D height = AssetDatabase.LoadAssetAtPath<Texture2D>(HeightPath);
            Shader shader = Resources.Load<Shader>(LabClayStickerRuntime.SurfaceShaderResource);
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            ThemeStyleSheet theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            Require(prefab != null && color != null && height != null && shader != null
                && font != null && style != null && theme != null, "One of the preview-only references is missing.");
            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.name = "Clay Preview Panel Settings";
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1280, 800);
                panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panel.match = .5f;
                panel.themeStyleSheet = theme;
                AssetDatabase.CreateAsset(panel, PanelSettingsPath);
                AssetDatabase.SaveAssetIfDirty(panel);
            }
            Scene active = SceneManager.GetActiveScene();
            string before = SceneState(), buildScenes = BuildScenesState();
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                var root = new GameObject("Clay Material Experiment");
                SceneManager.MoveGameObjectToScene(root, scene);
                var document = root.AddComponent<UIDocument>();
                document.panelSettings = panel;
                var controller = root.AddComponent<ClayPreviewController>();
                controller.sourcePrefab = prefab; controller.colorMap = color; controller.heightMap = height;
                controller.clayShader = shader; controller.uiFont = font; controller.styleSheet = style;
                Require(EditorSceneManager.SaveScene(scene, ScenePath), "Could not save the isolated preview Scene.");
            }
            finally
            {
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            Require(before == SceneState(), "Creating the preview changed a previously loaded Scene.");
            Require(buildScenes == BuildScenesState(), "Creating the preview changed normal Build Settings.");
        }

        public static string ValidatePreparedSample()
        {
            Require(File.Exists(ScenePath) && File.Exists(SampleInfoPath), "Prepare the preview Scene and sample first.");
            SampleInfo info = JsonUtility.FromJson<SampleInfo>(File.ReadAllText(SampleInfoPath));
            Require(info != null && info.format == "clay-preview-sample-v1" && info.complete
                && info.cells == info.totalCells && info.totalCells > 0, "The sample metadata is not an actual complete surface.");
            Require(info.sourceConfigPreserved && info.randomPreserved && info.scenesPreserved,
                "The sample bake did not preserve shared source state.");
            Require(info.colorFileHash == Hash(File.ReadAllBytes(ColorPath))
                && info.heightFileHash == Hash(File.ReadAllBytes(HeightPath)), "The frozen sample changed after baking.");
            ValidateMap(ColorPath, false); ValidateMap(HeightPath, true);
            Require(!EditorBuildSettings.scenes.Any(s => s.path.StartsWith(Root + "/", StringComparison.Ordinal)),
                "The experiment must not be added to the normal game Build Settings.");
            string[] normal = EditorBuildSettings.scenes.Where(s => s.enabled && !string.IsNullOrEmpty(s.path)).Select(s => s.path).ToArray();
            Require(!AssetDatabase.GetDependencies(normal, true).Any(p => p.StartsWith(Root + "/", StringComparison.Ordinal)),
                "A normal game Scene references preview-only sample assets.");
            return JsonUtility.ToJson(new Validation
            {
                valid = true, complete = true, normalBuildScenesPreserved = true,
                width = info.width, height = info.height, hits = info.hits, cells = info.cells,
                checks = 8, colorFileHash = info.colorFileHash, heightFileHash = info.heightFileHash
            }, true);
        }

        private static void ValidateMap(string path, bool height)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(texture != null && texture.width == 1024 && texture.height == 512 && importer != null,
                "The frozen sample map is missing or resized.");
            Require(!importer.sRGBTexture && !importer.mipmapEnabled && !importer.alphaIsTransparency
                && !importer.isReadable && importer.textureCompression == TextureImporterCompression.Uncompressed
                && importer.npotScale == TextureImporterNPOTScale.None && importer.filterMode == FilterMode.Bilinear
                && importer.wrapModeU == TextureWrapMode.Repeat && importer.wrapModeV == TextureWrapMode.Clamp,
                "The frozen map color-space, sampling, or compression settings changed.");
            TextureImporterFormat expected = height ? TextureImporterFormat.RGBAHalf : TextureImporterFormat.RGBA32;
            Require(importer.GetDefaultPlatformTextureSettings().format == expected,
                "The frozen map no longer preserves its source precision.");
        }

        [MenuItem("C6 Lab/Experiments/Clay Sticker/Build Mac Preview App")]
        public static void ScheduleBuild()
        {
            RequireStopped();
            WriteStatus(new BuildStatus { state = "queued" });
            EditorApplication.delayCall += RunScheduledBuild;
        }

        private static void RunScheduledBuild()
        {
            try { BuildMac(); }
            catch (Exception exception)
            {
                BuildStatus status = File.Exists(ResultPath)
                    ? JsonUtility.FromJson<BuildStatus>(File.ReadAllText(ResultPath)) : new BuildStatus();
                status ??= new BuildStatus();
                if (status.state != "failed")
                {
                    status.state = "failed"; status.error = exception.Message;
                    WriteStatus(status);
                }
                Debug.LogException(exception);
            }
        }

        public static string BuildMac()
        {
            RequireStopped();
            string output = Path.Combine(ProjectRoot, "Builds", "Experiments", "ClayPreview",
                DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                "Hapio-Clay-Experiment.app");
            var status = new BuildStatus { state = "running", output = output };
            WriteStatus(status);
            BuildTarget previousTarget = EditorUserBuildSettings.activeBuildTarget;
            EditorBuildSettingsScene[] originalScenes = EditorBuildSettings.scenes
                .Select(s => new EditorBuildSettingsScene(s.path, s.enabled)).ToArray();
            string scenes = BuildScenesState();
            var player = new PlayerState();
            try
            {
                Require(!Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty),
                    "Save or keep a separate copy of unsaved Scene edits before building. This tool never saves them automatically.");
                Prepare();
                if (previousTarget != BuildTarget.StandaloneOSX)
                    Require(EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX),
                        "Could not activate the installed Mac build platform.");
                Require(!File.Exists(output) && !Directory.Exists(output), "The preview build destination already exists.");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                player.ApplyPreviewDefaults();
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, locationPathName = output,
                    target = BuildTarget.StandaloneOSX, options = BuildOptions.Development
                });
                Require(report != null && report.summary.result == BuildResult.Succeeded,
                    "The standalone clay preview build did not succeed.");
                status.bytes = report.summary.totalSize;
                SessionState.SetString(LastBuildKey, output);
                status.state = "succeeded";
            }
            catch (Exception exception)
            {
                status.state = "failed"; status.error = exception.Message; throw;
            }
            finally
            {
                var restorationErrors = new List<string>();
                RestoreStep(() =>
                {
                    if (EditorUserBuildSettings.activeBuildTarget != previousTarget)
                        Require(EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(previousTarget), previousTarget),
                            "Could not restore the original active build platform.");
                }, "active platform", restorationErrors);
                RestoreStep(() =>
                {
                    if (scenes != BuildScenesState()) EditorBuildSettings.scenes = originalScenes;
                }, "build Scene list", restorationErrors);
                // Restore native values and the exact original disk file last: a platform
                // switch or BuildPlayer can serialize temporary PlayerSettings to disk.
                // SaveAssets is deliberately not used, since unrelated user assets may be dirty.
                RestoreStep(player.Restore, "PlayerSettings memory and disk file", restorationErrors);
                status.scenesPreserved = scenes == BuildScenesState();
                status.playerSettingsPreserved = player.Matches();
                status.playerSettingsDiskPreserved = player.DiskMatches();
                status.playerSettingsBeforeHash = player.OriginalDiskHash;
                status.playerSettingsAfterHash = player.CurrentDiskHash();
                status.activeTargetPreserved = EditorUserBuildSettings.activeBuildTarget == previousTarget;
                if (restorationErrors.Count != 0 || !status.scenesPreserved || !status.playerSettingsPreserved
                    || !status.playerSettingsDiskPreserved || !status.activeTargetPreserved)
                {
                    status.state = "failed";
                    status.error = "The build did not restore the original Editor build configuration. "
                        + string.Join("; ", restorationErrors);
                }
                WriteStatus(status);
            }
            Require(status.state == "succeeded", status.error ?? "The preview build failed.");
            return output;
        }

        [MenuItem("C6 Lab/Experiments/Clay Sticker/Open Last Mac Preview Build")]
        public static void OpenLastOutput()
        {
            string path = SessionState.GetString(LastBuildKey, string.Empty);
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) EditorUtility.RevealInFinder(path);
            else Debug.LogWarning("CLAY_PREVIEW_OUTPUT_UNAVAILABLE: Build the preview app first.");
        }

        private sealed class PlayerState
        {
            private readonly string product = PlayerSettings.productName;
            private readonly int width = PlayerSettings.defaultScreenWidth, height = PlayerSettings.defaultScreenHeight;
            private readonly FullScreenMode fullScreen = PlayerSettings.fullScreenMode;
            private readonly bool resizable = PlayerSettings.resizableWindow, background = PlayerSettings.runInBackground;
            private readonly string identifier = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone);
            private readonly string diskPath = Path.Combine(ProjectRoot, "ProjectSettings", "ProjectSettings.asset");
            private readonly byte[] originalDiskBytes;
            public string OriginalDiskHash { get; }
            public PlayerState()
            {
                Require(File.Exists(diskPath), "The original ProjectSettings file is missing; preview settings were not applied.");
                originalDiskBytes = File.ReadAllBytes(diskPath);
                OriginalDiskHash = Hash(originalDiskBytes);
            }
            public void ApplyPreviewDefaults()
            {
                PlayerSettings.productName = "Hapio Clay Experiment";
                PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 800;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.resizableWindow = true; PlayerSettings.runInBackground = false;
                PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.hapio.experiments.claypreview");
            }
            public void Restore()
            {
                try
                {
                    PlayerSettings.productName = product;
                    PlayerSettings.defaultScreenWidth = width; PlayerSettings.defaultScreenHeight = height;
                    PlayerSettings.fullScreenMode = fullScreen;
                    PlayerSettings.resizableWindow = resizable; PlayerSettings.runInBackground = background;
                    PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, identifier);
                    Require(Matches(), "Could not restore the original native PlayerSettings values.");
                }
                finally
                {
                    // Even if a native setter fails, still recover the original user file.
                    if (!DiskMatches()) File.WriteAllBytes(diskPath, originalDiskBytes);
                    Require(DiskMatches(), "Could not restore the exact original ProjectSettings file bytes.");
                }
            }
            public bool DiskMatches()
            {
                try { return File.Exists(diskPath) && originalDiskBytes.SequenceEqual(File.ReadAllBytes(diskPath)); }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
            public string CurrentDiskHash()
            {
                try { return File.Exists(diskPath) ? Hash(File.ReadAllBytes(diskPath)) : string.Empty; }
                catch (IOException) { return string.Empty; }
                catch (UnauthorizedAccessException) { return string.Empty; }
            }
            public bool Matches() => PlayerSettings.productName == product
                && PlayerSettings.defaultScreenWidth == width && PlayerSettings.defaultScreenHeight == height
                && PlayerSettings.fullScreenMode == fullScreen && PlayerSettings.resizableWindow == resizable
                && PlayerSettings.runInBackground == background
                && PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone) == identifier;
        }

        private static void RestoreStep(Action restore, string name, List<string> errors)
        {
            try { restore(); }
            catch (Exception exception) { errors.Add(name + ": " + exception.Message); }
        }

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string BuildScenesState() => string.Join("|", EditorBuildSettings.scenes.Select(s => s.path + ":" + s.enabled));
        private static string SceneState() => string.Join("|", Enumerable.Range(0, SceneManager.sceneCount)
            .Select(i => { Scene s = SceneManager.GetSceneAt(i); return s.handle + ":" + s.path + ":" + s.isDirty; }));
        private static void RequireStopped() => Require(!EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorApplication.isCompiling && !BuildPipeline.isBuildingPlayer, "Stop Play mode and finish compilation/building first.");
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static string Hash(byte[] data) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(data)).Replace("-", "").ToLowerInvariant(); }
        private static void WriteStatus(BuildStatus status)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, JsonUtility.ToJson(status, true));
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            Require(!string.IsNullOrEmpty(parent), "The experiment asset path is invalid.");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
