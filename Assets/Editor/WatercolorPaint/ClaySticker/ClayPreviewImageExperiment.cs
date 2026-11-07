using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hapio.Experiments;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6Lab.Experiments.Editor
{
    public static class ClayPreviewImageExperiment
    {
        public const string Root = ClayPreviewBuild.Root + "/ImageSamples";
        public const string ScenePath = Root + "/Scenes/ImageClayPreview.unity";
        public const string ManifestPath = Root + "/ImagePreviewManifest.json";
        public const string BackgroundShaderPath = ClayPreviewBuild.Root + "/Runtime/Shaders/ClayPreviewBackground.shader";
        static readonly string[] Keys = { "FineMatteClay", "HandKneadedClay", "FineHanjiFiber" };
        static readonly string[] Labels = { "Fine clay", "Hand-made", "Fine fiber" };
        static readonly string[] RenderPaths =
        {
            "Assets/Lab/Resources/GourdPaint/ClaySticker/ClayStickerSurface.shader",
            ClayPreviewBuild.Root + "/Runtime/Shaders/RoundedClayStickerSurface.shader",
            StickerCoatingExperiment.SurfaceShaderPath,
            "Assets/Lab/Resources/GourdPaint/ClaySticker/ClayPreviewTexture.hlsl",
            BackgroundShaderPath
        };
        [Serializable] public sealed class FilePin { public string path, sha256; }
        [Serializable] public sealed class Sample
        { public string key, label, source, encoded, sha256; public int width, height; }
        [Serializable] public sealed class Manifest
        {
            public string format = "clay-preview-image-files-v1";
            public Sample[] samples;
            public FilePin[] renderSources;
            public string scope = "Mac comparison only. External surface grain and unlit 2D background; no gameplay/journal changes.";
        }

        [MenuItem("C6 Lab/Experiments/Clay Images/Prepare Texture and Background Preview")]
        public static void PrepareMenu() => Debug.Log("CLAY_IMAGE_PREVIEW_PREPARED " + Prepare());

        public static string Prepare()
        {
            ClayPreviewBuild.RequireStopped(); RequireSavedScenes();
            string buildScenes = ClayPreviewBuild.BuildScenesState();
            // Only display metadata is refreshed. Frozen color/height and all journal bytes remain intact.
            StickerCoatingExperiment.RefreshPreviewLightingMetadata();
            ClayPreviewBuild.EnsureFolder(Root + "/Encoded");
            ClayPreviewBuild.EnsureFolder(Root + "/Scenes");
            var manifest = new Manifest { samples = new Sample[Keys.Length] };
            for (int i = 0; i < Keys.Length; i++)
            {
                string source = Root + "/Sources/" + Keys[i] + ".png";
                byte[] bytes = File.ReadAllBytes(source);
                using (ClayPreviewImageImport.Result imported = ClayPreviewImageImport.LoadBytes(bytes, Keys[i] + ".png", ClayPreviewImageImport.Purpose.SurfaceTexture))
                {
                    Need(imported.Succeeded, imported.Error ?? "The sample could not be loaded.");
                    string encoded = Root + "/Encoded/" + Keys[i] + ".bytes";
                    if (!File.Exists(encoded) || !File.ReadAllBytes(encoded).SequenceEqual(bytes)) File.WriteAllBytes(encoded, bytes);
                    AssetDatabase.ImportAsset(encoded, ImportAssetOptions.ForceSynchronousImport);
                    manifest.samples[i] = new Sample { key = Keys[i], label = Labels[i], source = source, encoded = encoded,
                        sha256 = ClayPreviewBuild.Hash(bytes), width = imported.Texture.width, height = imported.Texture.height };
                }
            }
            manifest.renderSources = RenderPaths.Select(path => new FilePin { path = path, sha256 = FileHash(path) }).ToArray();
            File.WriteAllText(ManifestPath, JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(ManifestPath, ImportAssetOptions.ForceSynchronousImport);
            BindScene(manifest);
            Need(buildScenes == ClayPreviewBuild.BuildScenesState(), "Preparing must not change normal Build Settings.");
            return ValidatePrepared();
        }

        static void BindScene(Manifest manifest)
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(File.Exists(ScenePath) ? ScenePath : StickerCoatingExperiment.ScenePath, OpenSceneMode.Single);
                ClayPreviewController preview = Controller(scene);
                preview.enableImageFilePreview = true;
                preview.imageBackgroundShader = AssetDatabase.LoadAssetAtPath<Shader>(BackgroundShaderPath);
                preview.surfaceTextureSamples = manifest.samples.Select(s => AssetDatabase.LoadAssetAtPath<TextAsset>(s.encoded)).ToArray();
                preview.surfaceTextureSampleNames = manifest.samples.Select(s => s.label).ToArray();
                // V4 display-source refresh may have changed the shared sample metadata pin.
                TextAsset metadata = AssetDatabase.LoadAssetAtPath<TextAsset>(StickerCoatingExperiment.Folder + "/SampleInfo.json");
                preview.presets[3].sampleInfo = metadata;
                preview.presets[3].sampleMetadataHash = ClayPreviewSampleInfo.ContentHash(metadata.text);
                EditorUtility.SetDirty(preview);
                EditorSceneManager.MarkSceneDirty(scene);
                Need(EditorSceneManager.SaveScene(scene, ScenePath), "Could not save the image-only preview Scene.");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        public static string ValidatePrepared()
        {
            ClayPreviewBuild.RequireStopped();
            StickerCoatingExperiment.ValidatePrepared();
            Need(File.Exists(ManifestPath) && File.Exists(ScenePath), "Use Prepare Texture and Background Preview first.");
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            Need(manifest != null && manifest.format == "clay-preview-image-files-v1" && manifest.samples?.Length == 3
                && manifest.renderSources?.Length == RenderPaths.Length, "The image preview manifest is missing or incompatible.");
            for (int i = 0; i < Keys.Length; i++)
            {
                Sample sample = manifest.samples[i];
                Need(sample.key == Keys[i] && sample.label == Labels[i]
                    && sample.source == Root + "/Sources/" + Keys[i] + ".png"
                    && sample.encoded == Root + "/Encoded/" + Keys[i] + ".bytes", "The sample order or source changed; explicitly prepare again.");
                Need(FileHash(sample.source) == sample.sha256 && FileHash(sample.encoded) == sample.sha256,
                    "The source or packed sample changed; explicitly prepare the image preview again.");
                Need(ClayPreviewImageImport.TryGetDimensions(File.ReadAllBytes(sample.encoded), out int width, out int height, out string error)
                    && width == sample.width && height == sample.height, error ?? "The sample dimensions changed.");
            }
            for (int i = 0; i < RenderPaths.Length; i++)
                Need(manifest.renderSources[i].path == RenderPaths[i] && manifest.renderSources[i].sha256 == FileHash(RenderPaths[i]),
                    "A display shader or texture include changed. Explicitly prepare this preview; Build never refreshes it automatically.");
            string state = ClayPreviewBuild.SceneState(), buildScenes = ClayPreviewBuild.BuildScenesState();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            Scene active = SceneManager.GetActiveScene();
            try
            {
                if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                ClayPreviewController preview = Controller(scene);
                Need(preview.enableImageFilePreview && preview.imageBackgroundShader == AssetDatabase.LoadAssetAtPath<Shader>(BackgroundShaderPath),
                    "The image preview Scene has missing presentation references.");
                Need(preview.presets?.Length == 4 && preview.defaultPreset == 3 && preview.paletteData != null,
                    "The original four presets and palette must remain connected.");
                Need(preview.surfaceTextureSamples?.Length == 3 && preview.surfaceTextureSampleNames?.Length == 3,
                    "The three built-in samples are not connected.");
                for (int i = 0; i < Keys.Length; i++)
                    Need(AssetDatabase.GetAssetPath(preview.surfaceTextureSamples[i]) == manifest.samples[i].encoded
                        && preview.surfaceTextureSampleNames[i] == Labels[i], "A built-in sample reference changed.");
                Need(AssetDatabase.GetDependencies(ScenePath, true).Contains(BackgroundShaderPath), "The Player must retain its directly referenced background shader.");
                Need(!EditorBuildSettings.scenes.Any(s => s.path == ScenePath), "Do not add the experiment to normal game Build Settings.");
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
            Need(state == ClayPreviewBuild.SceneState() && buildScenes == ClayPreviewBuild.BuildScenesState(), "Validation must not change Scenes or normal Build Settings.");
            return JsonUtility.ToJson(manifest, true);
        }

        [MenuItem("C6 Lab/Experiments/Clay Images/Build Texture and Background Mac App")]
        public static void BuildMenu() => Debug.Log("CLAY_IMAGE_PREVIEW_BUILD " + BuildMac());
        public static string BuildMac() => ClayPreviewBuild.BuildMac(ScenePath, ValidatePrepared, ClayPreviewMacImagePickerBuild.StageForPreview);

        static void RequireSavedScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                Need(!scene.isDirty && !string.IsNullOrEmpty(scene.path), "Open saved Scenes and preserve any unsaved changes before preparing the image preview.");
            }
        }
        static ClayPreviewController Controller(Scene scene)
        {
            ClayPreviewController[] controllers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ClayPreviewController>(true)).ToArray();
            Need(controllers.Length == 1, "The experiment must contain exactly one preview controller.");
            return controllers[0];
        }
        static string FileHash(string path) { Need(File.Exists(path), "Missing image-preview source: " + path); return ClayPreviewBuild.Hash(File.ReadAllBytes(path)); }
        static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
