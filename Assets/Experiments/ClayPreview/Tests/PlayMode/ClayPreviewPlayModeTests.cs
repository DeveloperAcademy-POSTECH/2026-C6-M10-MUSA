#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Hapio.Experiments.Tests
{
    /// <summary>Saved experiment assets and actual runtime UI/rendering; no gameplay session is started.</summary>
    [Category("GPU"), Category("Legacy"), Category("Slow")]
    public sealed class ClayPreviewPlayModeTests
    {
        const string Root = "Assets/Experiments/ClayPreview/";
        const string ScenePath = Root + "Scenes/ClayPreview.unity";
        const string RoundedScenePath = Root + "Rounded/Scenes/RoundedClayPreview.unity";
        const string CoatingScenePath = Root + "Rounded/Scenes/CoatingClayPreview.unity";
        const string ColorPath = Root + "Sample/Color.png";
        const string HeightPath = Root + "Sample/Height.exr";
        const string SourcePrefab = "Assets/Lab/Resources/GourdPaint/WhiteGourd.prefab";
        const string SourceConfig = "Assets/Lab/Resources/LabConfig.asset";
        ClayPreviewController preview;
        Scene scene;
        string colorHash, heightHash, configHash, prefabHash;
        MonoBehaviour[] existingSessions;
        int previousFrameRate, previousVSync;
        bool previousBackground;

        [UnitySetUp]
        public IEnumerator LoadSavedExperiment()
        {
            Assert.That(File.Exists(AbsolutePath(ScenePath)), Is.True, "Prepare the isolated Clay Preview sample and Scene first.");
            colorHash = FileHash(ColorPath);
            heightHash = FileHash(HeightPath);
            configHash = FileHash(SourceConfig);
            prefabHash = AssetDatabase.GetAssetDependencyHash(SourcePrefab).ToString();
            previousFrameRate = Application.targetFrameRate;
            previousVSync = QualitySettings.vSyncCount;
            previousBackground = Application.runInBackground;
            existingSessions = FindSessions();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            scene = SceneManager.GetSceneByPath(ScenePath);
            preview = scene.GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<ClayPreviewController>(true)).Single();
            float deadline = Time.realtimeSinceStartup + 5f;
            while ((!preview.IsReady || preview.GetComponent<UIDocument>().rootVisualElement.panel == null)
                && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(preview.Error, Is.Null.Or.Empty, "The actual saved experiment must initialize without a fallback image.");
            Assert.That(preview.IsReady, Is.True);
            Assert.That(preview.PreviewTexture, Is.Not.Null);
            Assert.That(preview.PreviewTexture.IsCreated(), Is.True);
        }

        [UnityTearDown]
        public IEnumerator UnloadAndPreserveSourceAssets()
        {
            yield return UnloadPreview();
            Assert.That(FileHash(ColorPath), Is.EqualTo(colorHash));
            Assert.That(FileHash(HeightPath), Is.EqualTo(heightHash));
            Assert.That(FileHash(SourceConfig), Is.EqualTo(configHash));
            Assert.That(AssetDatabase.GetAssetDependencyHash(SourcePrefab).ToString(), Is.EqualTo(prefabHash));
            Assert.That(Application.targetFrameRate, Is.EqualTo(previousFrameRate));
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(previousVSync));
            Assert.That(Application.runInBackground, Is.EqualTo(previousBackground));
        }

        [Test]
        public void SavedSampleUsesLinearNativeMapsAndStaysOutsideNormalBuildScenes()
        {
            Assert.That(preview.sampleInfo, Is.Not.Null);
            Assert.That(ClayPreviewSampleInfo.TryValidate(preview.sampleInfo.text, preview.sampleMetadataHash,
                out ClayPreviewSampleInfo info, out string metadataError), Is.True, metadataError);
            Assert.That(info.format, Is.EqualTo("clay-preview-sample-v2"));
            Assert.That(info.heightChannels, Is.EqualTo(ClayPreviewSampleInfo.HeightChannels));
            Assert.That(info.styleVersion, Does.StartWith("clay-stickers-v2/"));
            Assert.That(info.colorFileHash, Is.EqualTo(colorHash.ToLowerInvariant()));
            Assert.That(info.heightFileHash, Is.EqualTo(heightHash.ToLowerInvariant()));
            foreach (string path in new[] { ColorPath, HeightPath })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer, Is.Not.Null);
                Assert.That(importer.sRGBTexture, Is.False, path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
                Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None), path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear), path);
                Assert.That(importer.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat), path);
                Assert.That(importer.wrapModeV, Is.EqualTo(TextureWrapMode.Clamp), path);
                Texture2D map = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(map.width, Is.EqualTo(1024));
                Assert.That(map.height, Is.EqualTo(512));
            }
            Assert.That(preview.heightMap.format, Is.EqualTo(TextureFormat.RGBAHalf),
                "The saved height map must not be reduced to an eight-bit color texture.");
            // Checked whatever the active target is: with iOS active, a Half EXR that only follows Default is
            // RGBM-encoded on import and its coverage A is lost (the packed-sample readback test then fails).
            var ios = ((TextureImporter)AssetImporter.GetAtPath(HeightPath)).GetPlatformTextureSettings("iPhone");
            Assert.That(ios.overridden, Is.True, "The height map keeps an explicit iOS override.");
            Assert.That(ios.format, Is.EqualTo(TextureImporterFormat.RGBAHalf), "iOS imports the height map as RGBA Half.");
            Assert.That(ios.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(preview.clayShader.name, Is.EqualTo("C6Lab/ClayStickerSurface"));
            string[] experimentDependencies = AssetDatabase.GetDependencies(ScenePath, true);
            Assert.That(experimentDependencies, Does.Contain(AssetDatabase.GetAssetPath(preview.clayShader)),
                "The production clay shader needs a direct Scene dependency so Player stripping keeps it.");
            Assert.That(experimentDependencies, Does.Contain(AssetDatabase.GetAssetPath(preview.sampleInfo)),
                "The Player must carry the same generated sample contract as its textures.");
            foreach (var entry in EditorBuildSettings.scenes)
            {
                Assert.That(entry.path, Is.Not.EqualTo(ScenePath), "The normal game's Scene list must stay unchanged.");
                Assert.That(AssetDatabase.GetDependencies(entry.path, true).Any(x => x.StartsWith(Root, StringComparison.Ordinal)),
                    Is.False, "A normal build Scene must not pull in the comparison sample.");
            }
            Assert.That(scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true))
                .Any(x => x != null && (x.GetType().FullName == "Hapio.GameSession" || x.GetType().FullName == "C6Lab.LabNetwork")),
                Is.False, "The experiment must not create a room or a gameplay session.");
        }

        [UnityTest]
        public IEnumerator LegacyOrChangedSampleMetadataIsRejectedBeforeCreatingMaterials()
        {
            string original = preview.sampleInfo.text;
            string[] invalid =
            {
                original.Replace("clay-preview-sample-v2", "clay-preview-sample-v1"),
                original.Replace("clay-stickers-v2/", "clay-stickers-v1/"),
                original.Replace(ClayPreviewSampleInfo.HeightChannels, "R:legacy-height"),
                original + " " // Well-formed metadata, but not the manifest pinned by the saved Scene.
            };
            for (int i = 0; i < invalid.Length; i++)
            {
                string pinnedHash = i == invalid.Length - 1 ? preview.sampleMetadataHash : ClayPreviewSampleInfo.ContentHash(invalid[i]);
                Assert.That(ClayPreviewSampleInfo.TryValidate(invalid[i], pinnedHash, out _, out string reason), Is.False);
                var metadata = new TextAsset(invalid[i]);
                var root = new GameObject("Rejected clay preview sample");
                root.SetActive(false);
                var rejected = root.AddComponent<ClayPreviewController>();
                rejected.sourcePrefab = preview.sourcePrefab;
                rejected.colorMap = preview.colorMap; rejected.heightMap = preview.heightMap;
                rejected.clayShader = preview.clayShader;
                rejected.sampleInfo = metadata; rejected.sampleMetadataHash = pinnedHash;
                int frameRate = Application.targetFrameRate, vsync = QualitySettings.vSyncCount;
                bool background = Application.runInBackground;
                try
                {
                    LogAssert.Expect(LogType.Error, "CLAY_PREVIEW_FAILED: " + reason);
                    root.SetActive(true);
                    Assert.That(rejected.Error, Is.EqualTo(reason));
                    Assert.That(rejected.IsReady, Is.False);
                    Assert.That(rejected.ClayMaterial, Is.Null, "Never interpret legacy GB/A as a local coating normal.");
                    Assert.That(rejected.ModelRoot, Is.Null);
                    Assert.That(Application.targetFrameRate, Is.EqualTo(frameRate));
                    Assert.That(QualitySettings.vSyncCount, Is.EqualTo(vsync));
                    Assert.That(Application.runInBackground, Is.EqualTo(background));
                }
                finally { Object.Destroy(root); Object.Destroy(metadata); }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator PackedSampleKeepsAllFourHalfChannelsAndActualCoatingCoverage()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !SystemInfo.supportsAsyncGPUReadback)
                Assert.Ignore("This sample precision test needs a real graphics device with asynchronous readback.");
            Assert.That(ClayPreviewSampleInfo.TryValidate(preview.sampleInfo.text, preview.sampleMetadataHash,
                out ClayPreviewSampleInfo info, out string error), Is.True, error);
            var request = AsyncGPUReadback.Request(preview.heightMap, 0, TextureFormat.RGBAHalf);
            yield return Readback(request);
            byte[] packed = request.GetData<byte>().ToArray();
            Assert.That(packed.Length, Is.EqualTo(1024 * 512 * 8), "RGBA Half preserves every packed surface channel.");
            Assert.That(ByteHash(packed), Is.EqualTo(info.heightRawHash), "EXR import preserves R, G, B and A exactly.");
            var physical = new byte[1024 * 512 * 2];
            int coated = 0, tilted = 0, invalidPixels = 0;
            float minNormal = float.PositiveInfinity, maxNormal = float.NegativeInfinity;
            for (int i = 0; i < 1024 * 512; i++)
            {
                physical[i * 2] = packed[i * 8]; physical[i * 2 + 1] = packed[i * 8 + 1];
                float r = Mathf.HalfToFloat(BitConverter.ToUInt16(packed, i * 8));
                float g = Mathf.HalfToFloat(BitConverter.ToUInt16(packed, i * 8 + 2));
                float b = Mathf.HalfToFloat(BitConverter.ToUInt16(packed, i * 8 + 4));
                float a = Mathf.HalfToFloat(BitConverter.ToUInt16(packed, i * 8 + 6));
                if (!float.IsFinite(r) || !float.IsFinite(g) || !float.IsFinite(b) || !float.IsFinite(a)
                    || r < 0 || a < 0 || a > 1) invalidPixels++;
                if (a <= .01f) continue;
                coated++;
                minNormal = Mathf.Min(minNormal, Mathf.Min(g / a, b / a));
                maxNormal = Mathf.Max(maxNormal, Mathf.Max(g / a, b / a));
                if (Mathf.Abs(g - .5f * a) + Mathf.Abs(b - .5f * a) > .001f) tilted++;
            }
            Assert.That(ByteHash(physical), Is.EqualTo(info.physicalHeightRawHash), "The independent physical-height R channel is preserved.");
            Assert.That(invalidPixels, Is.Zero, "Packed height, normal and coverage values must be finite, with nonnegative height and coverage in [0,1].");
            Assert.That(coated, Is.GreaterThan(0), "The completed sample has actual stamped coating, not only its completion backing.");
            Assert.That(tilted, Is.GreaterThan(100), "Local stamp slopes must survive the saved sample.");
            Assert.That(maxNormal - minNormal, Is.GreaterThan(.01f));
        }

        [UnityTest]
        public IEnumerator ActualSlidersIndependentlyChangeTheProductionMaterialAndResetBothValues()
        {
            var root = preview.GetComponent<UIDocument>().rootVisualElement;
            Slider thickness = root.Q<Slider>("thicknessSlider");
            Slider gloss = root.Q<Slider>("glossSlider");
            Assert.That(thickness, Is.Not.Null); Assert.That(gloss, Is.Not.Null);
            Assert.That(thickness.lowValue, Is.Zero); Assert.That(thickness.highValue, Is.EqualTo(.02f));
            Assert.That(gloss.lowValue, Is.Zero); Assert.That(gloss.highValue, Is.EqualTo(1f));
            AssertAppearance(.01f, .35f);
            thickness.value = .02f; yield return null;
            AssertAppearance(.02f, .35f);
            gloss.value = 1f; yield return null;
            AssertAppearance(.02f, 1f);
            thickness.value = 0f; yield return null;
            AssertAppearance(0f, 1f);
            gloss.value = 0f; yield return null;
            AssertAppearance(0f, 0f);
            preview.SetView(65f, 25f, 1.4f);
            Submit(root.Q<Button>("resetButton"));
            yield return null;
            AssertAppearance(.01f, .35f);
            Assert.That(thickness.value, Is.EqualTo(.01f));
            Assert.That(gloss.value, Is.EqualTo(.35f));
            Assert.That(preview.Yaw, Is.Zero); Assert.That(preview.Pitch, Is.Zero); Assert.That(preview.Zoom, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator SixViewButtonsRotateTheRealModelAndKeepEveryPartWithinTheCamera()
        {
            var root = preview.GetComponent<UIDocument>().rootVisualElement;
            string[] buttons = { "frontView", "rightView", "backView", "leftView", "diagonalView", "upperView" };
            float[] yaws = { 0f, 90f, 180f, -90f, 45f, 45f };
            for (int i = 0; i < buttons.Length; i++)
            {
                Submit(root.Q<Button>(buttons[i]));
                yield return null;
                Assert.That(Mathf.DeltaAngle(preview.Yaw, yaws[i]), Is.EqualTo(0f).Within(.001f), buttons[i]);
                Assert.That(preview.Pitch, Is.EqualTo(i == 5 ? 40f : 0f).Within(.001f), buttons[i]);
                Assert.That(preview.Zoom, Is.EqualTo(1f));
                AssertModelFitsCamera(preview.ModelRoot, preview.PreviewCamera);
                AssertAppearance(.01f, .35f);
            }
        }

        [Category("GPU")]
        [UnityTest]
        public IEnumerator AppearanceChangesActualGpuOutputWithoutRepaintingTheFrozenSample()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !SystemInfo.supportsAsyncGPUReadback)
                Assert.Ignore("This render test requires a real graphics device with asynchronous readback.");
            preview.SetView(35f, 12f, 1.1f);
            preview.SetAppearance(0f, 0f);
            yield return null; yield return null;
            var first = AsyncGPUReadback.Request(preview.PreviewTexture, 0, TextureFormat.RGBA32);
            yield return Readback(first);
            byte[] before = first.GetData<byte>().ToArray();
            preview.SetAppearance(.02f, 1f);
            yield return null; yield return null;
            var second = AsyncGPUReadback.Request(preview.PreviewTexture, 0, TextureFormat.RGBA32);
            yield return Readback(second);
            byte[] after = second.GetData<byte>().ToArray();
            Assert.That(after.Length, Is.EqualTo(before.Length));
            Assert.That(after.Where((value, i) => value != before[i]).Take(100).Count(), Is.EqualTo(100),
                "Thickness and gloss must change actual 3D shading, not only the slider labels.");
            Assert.That(FileHash(ColorPath), Is.EqualTo(colorHash));
            Assert.That(FileHash(HeightPath), Is.EqualTo(heightHash));
            Assert.That(preview.ClayMaterial.GetTexture("_PaintMap"), Is.SameAs(preview.colorMap));
            Assert.That(preview.ClayMaterial.GetTexture("_HeightMap"), Is.SameAs(preview.heightMap));
        }

        [UnityTest]
        public IEnumerator ThreePreparedPresetsShareOneRecordAndSwitchWithoutReplacingTheModelOrMaterial()
        {
            yield return LoadComparison();
            Assert.That(preview.PresetCount, Is.EqualTo(3));
            Assert.That(preview.ActivePreset, Is.EqualTo(2), "The rounded contour experiment is the initial comparison.");
            Assert.That(preview.IsEnhanced, Is.True);
            AssertAppearance(.014f, .08f);
            AssertSurfaceDetails(.15f, 1f);
            var root = preview.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q<ScrollView>("materialPanel"), Is.Not.Null,
                "All controls remain reachable when the comparison panel is taller than the window.");
            string[] buttons = { "originalPreset", "materialPreset", "roundedPreset" };
            Material sharedPreviewMaterial = preview.ClayMaterial;
            Transform model = preview.ModelRoot;
            preview.SetView(37f, 18f, 1.3f);
            ClayPreviewSampleInfo first = null;
            string[] paths = preview.presets.SelectMany(p => new[] { AssetDatabase.GetAssetPath(p.colorMap),
                AssetDatabase.GetAssetPath(p.heightMap), AssetDatabase.GetAssetPath(p.sampleInfo) }).ToArray();
            string[] hashes = paths.Select(FileHash).ToArray();
            string[] dependencies = AssetDatabase.GetDependencies(RoundedScenePath, true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Submit(root.Q<Button>(buttons[i]));
                yield return null;
                ClayPreviewPreset preset = preview.presets[i];
                Assert.That(preview.ActivePreset, Is.EqualTo(i));
                Assert.That(preview.ClayMaterial, Is.SameAs(sharedPreviewMaterial));
                Assert.That(preview.ModelRoot, Is.SameAs(model));
                Assert.That(preview.colorMap, Is.SameAs(preset.colorMap));
                Assert.That(preview.heightMap, Is.SameAs(preset.heightMap));
                Assert.That(preview.ClayMaterial.shader, Is.SameAs(preset.shader));
                Assert.That(preview.ClayMaterial.GetTexture("_PaintMap"), Is.SameAs(preset.colorMap));
                Assert.That(preview.ClayMaterial.GetTexture("_HeightMap"), Is.SameAs(preset.heightMap));
                Assert.That(preview.ClayMaterial.shader.name, Is.EqualTo(i == 0 ? ClayPreviewController.OriginalShader : ClayPreviewController.EnhancedShader));
                Assert.That(dependencies, Does.Contain(AssetDatabase.GetAssetPath(preset.shader)), "Both shaders must survive Player stripping.");
                Assert.That(root.Q<VisualElement>("enhancedControls").enabledInHierarchy, Is.EqualTo(i > 0));
                AssertAppearance(i == 0 ? .01f : .014f, i == 0 ? .35f : .08f);
                if (i > 0) AssertSurfaceDetails(.15f, 1f);
                Assert.That(preview.Yaw, Is.EqualTo(37f));
                Assert.That(preview.Pitch, Is.EqualTo(18f));
                Assert.That(preview.Zoom, Is.EqualTo(1.3f));
                Assert.That(ClayPreviewSampleInfo.TryValidate(preset.sampleInfo.text, preset.sampleMetadataHash,
                    out ClayPreviewSampleInfo info, out string error), Is.True, error);
                Assert.That(info.format, Is.EqualTo(i == 0 ? ClayPreviewSampleInfo.FormatVersion : ClayPreviewSampleInfo.ExperimentFormatVersion));
                if (first == null) first = info;
                else
                {
                    Assert.That(info.seed, Is.EqualTo(first.seed));
                    Assert.That(info.sourceConfigHash, Is.EqualTo(first.sourceConfigHash));
                    Assert.That(info.authorityHash, Is.EqualTo(first.authorityHash));
                    Assert.That(info.recordHash, Is.EqualTo(first.recordHash));
                }
            }
            Assert.That(paths.Select(FileHash).ToArray(), Is.EqualTo(hashes), "Comparison never repaints or overwrites any saved preset.");
        }

        [UnityTest]
        public IEnumerator EnhancedSlidersAreIndependentAndResetOnlyTheCurrentPreset()
        {
            yield return LoadComparison();
            var root = preview.GetComponent<UIDocument>().rootVisualElement;
            Slider contact = root.Q<Slider>("contactSlider"), detail = root.Q<Slider>("detailSlider");
            Assert.That(contact, Is.Not.Null); Assert.That(detail, Is.Not.Null);
            Assert.That(contact.lowValue, Is.Zero); Assert.That(contact.highValue, Is.EqualTo(.5f));
            Assert.That(detail.lowValue, Is.Zero); Assert.That(detail.highValue, Is.EqualTo(1f));
            Texture color = preview.colorMap, height = preview.heightMap;
            contact.value = .5f; yield return null;
            AssertSurfaceDetails(.5f, 1f); AssertAppearance(.014f, .08f);
            detail.value = 0f; yield return null;
            AssertSurfaceDetails(.5f, 0f); AssertAppearance(.014f, .08f);
            root.Q<Slider>("thicknessSlider").value = 0f;
            root.Q<Slider>("glossSlider").value = .4f;
            yield return null;
            AssertAppearance(0f, .4f); AssertSurfaceDetails(.5f, 0f);
            preview.SetView(65f, 25f, 1.4f);
            Submit(root.Q<Button>("resetButton")); yield return null;
            Assert.That(preview.ActivePreset, Is.EqualTo(2));
            AssertAppearance(.014f, .08f); AssertSurfaceDetails(.15f, 1f);
            Assert.That(preview.Yaw, Is.Zero); Assert.That(preview.Pitch, Is.Zero); Assert.That(preview.Zoom, Is.EqualTo(1f));
            Assert.That(preview.colorMap, Is.SameAs(color)); Assert.That(preview.heightMap, Is.SameAs(height));
            preview.SelectPreset(0);
            Assert.That(contact.enabledInHierarchy, Is.False); Assert.That(detail.enabledInHierarchy, Is.False);
            AssertAppearance(.01f, .35f);
            preview.SelectPreset(1);
            Assert.That(contact.enabledInHierarchy, Is.True); Assert.That(detail.enabledInHierarchy, Is.True);
            AssertAppearance(.014f, .08f); AssertSurfaceDetails(.15f, 1f);
        }

        [UnityTest]
        public IEnumerator ComparisonRejectsAChangedArtworkRecordBeforeCreatingAnyPresentation()
        {
            yield return LoadComparison();
            var presets = preview.presets.Select(p => new ClayPreviewPreset
            {
                label = p.label, colorMap = p.colorMap, heightMap = p.heightMap, sampleInfo = p.sampleInfo,
                sampleMetadataHash = p.sampleMetadataHash, shader = p.shader, improvedMaterial = p.improvedMaterial
            }).ToArray();
            var changedInfo = JsonUtility.FromJson<ClayPreviewSampleInfo>(presets[2].sampleInfo.text);
            changedInfo.recordHash = (changedInfo.recordHash[0] == 'a' ? "b" : "a") + changedInfo.recordHash.Substring(1);
            var changedMetadata = new TextAsset(JsonUtility.ToJson(changedInfo));
            presets[2].sampleInfo = changedMetadata;
            presets[2].sampleMetadataHash = ClayPreviewSampleInfo.ContentHash(changedMetadata.text);
            var root = new GameObject("Rejected comparison record"); root.SetActive(false);
            var rejected = root.AddComponent<ClayPreviewController>();
            rejected.sourcePrefab = preview.sourcePrefab; rejected.presets = presets; rejected.defaultPreset = preview.defaultPreset;
            int frameRate = Application.targetFrameRate, vsync = QualitySettings.vSyncCount;
            try
            {
                LogAssert.Expect(LogType.Error, "CLAY_PREVIEW_FAILED: Comparison presets must replay the same fixed artwork and game judgement.");
                root.SetActive(true);
                Assert.That(rejected.IsReady, Is.False);
                Assert.That(rejected.ClayMaterial, Is.Null); Assert.That(rejected.ModelRoot, Is.Null);
                Assert.That(Application.targetFrameRate, Is.EqualTo(frameRate)); Assert.That(QualitySettings.vSyncCount, Is.EqualTo(vsync));
            }
            finally { Object.Destroy(root); Object.Destroy(changedMetadata); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FourthStickerLocalCoatingPresetUsesItsOwnPackingAndKeepsAllPreviousComparisons()
        {
            yield return LoadComparison(CoatingScenePath);
            Assert.That(preview.PresetCount, Is.EqualTo(4));
            Assert.That(preview.ActivePreset, Is.EqualTo(3));
            Assert.That(preview.defaultPreset, Is.EqualTo(3));
            AssertAppearance(.014f, .08f); AssertSurfaceDetails(.15f, 1f);
            Material material = preview.ClayMaterial; Transform model = preview.ModelRoot;
            string[] paths = preview.presets.SelectMany(p => new[] { AssetDatabase.GetAssetPath(p.colorMap),
                AssetDatabase.GetAssetPath(p.heightMap), AssetDatabase.GetAssetPath(p.sampleInfo) }).ToArray();
            string[] hashes = paths.Select(FileHash).ToArray();
            var root = preview.GetComponent<UIDocument>().rootVisualElement;
            string[] buttons = { "originalPreset", "materialPreset", "roundedPreset", "coatingPreset" };
            string[] dependencies = AssetDatabase.GetDependencies(CoatingScenePath, true);
            CoatingTestInfo first = null, enhanced = null;
            preview.SetView(41f, 21f, 1.2f);
            for (int i = 0; i < buttons.Length; i++)
            {
                Submit(root.Q<Button>(buttons[i])); yield return null;
                ClayPreviewPreset preset = preview.presets[i];
                Assert.That(preview.ActivePreset, Is.EqualTo(i));
                Assert.That(preview.ClayMaterial, Is.SameAs(material)); Assert.That(preview.ModelRoot, Is.SameAs(model));
                Assert.That(material.GetTexture("_PaintMap"), Is.SameAs(preset.colorMap));
                Assert.That(material.GetTexture("_HeightMap"), Is.SameAs(preset.heightMap));
                Assert.That(material.shader.name, Is.EqualTo(i == 3 ? ClayPreviewController.CoatingShader
                    : i > 0 ? ClayPreviewController.EnhancedShader : ClayPreviewController.OriginalShader));
                Assert.That(dependencies, Does.Contain(AssetDatabase.GetAssetPath(preset.shader)));
                Assert.That(ClayPreviewSampleInfo.TryValidate(preset.sampleInfo.text, preset.sampleMetadataHash, out _, out string error), Is.True, error);
                var info = JsonUtility.FromJson<CoatingTestInfo>(preset.sampleInfo.text);
                Assert.That(info.format, Is.EqualTo(i == 3 ? ClayPreviewSampleInfo.CoatingFormatVersion
                    : i > 0 ? ClayPreviewSampleInfo.ExperimentFormatVersion : ClayPreviewSampleInfo.FormatVersion));
                Assert.That(info.heightChannels, Is.EqualTo(i == 3 ? ClayPreviewSampleInfo.CoatingHeightChannels : ClayPreviewSampleInfo.HeightChannels));
                if (first == null) first = info;
                else
                {
                    Assert.That(info.seed, Is.EqualTo(first.seed)); Assert.That(info.sourceConfigHash, Is.EqualTo(first.sourceConfigHash));
                    Assert.That(info.authorityHash, Is.EqualTo(first.authorityHash)); Assert.That(info.recordHash, Is.EqualTo(first.recordHash));
                }
                if (i > 0)
                {
                    if (enhanced == null) enhanced = info;
                    Assert.That(info.minimumDensity, Is.EqualTo(enhanced.minimumDensity));
                    Assert.That(info.maximumDensity, Is.EqualTo(enhanced.maximumDensity));
                    AssertSurfaceDetails(.15f, 1f);
                }
                AssertAppearance(i == 0 ? .01f : .014f, i == 0 ? .35f : .08f);
                Assert.That(preview.Yaw, Is.EqualTo(41f)); Assert.That(preview.Pitch, Is.EqualTo(21f)); Assert.That(preview.Zoom, Is.EqualTo(1.2f));
            }
            Assert.That(preview.ClayMaterial.GetFloat("_SpecularOnly"), Is.Zero, "Diagnostic export shading is never enabled in the app.");
            root.Q<Slider>("contactSlider").value = .4f;
            root.Q<Slider>("detailSlider").value = .25f;
            root.Q<Slider>("thicknessSlider").value = .02f;
            root.Q<Slider>("glossSlider").value = .6f;
            yield return null;
            AssertAppearance(.02f, .6f); AssertSurfaceDetails(.4f, .25f);
            Submit(root.Q<Button>("resetButton")); yield return null;
            Assert.That(preview.ActivePreset, Is.EqualTo(3));
            AssertAppearance(.014f, .08f); AssertSurfaceDetails(.15f, 1f);
            Assert.That(preview.Yaw, Is.Zero); Assert.That(preview.Pitch, Is.Zero); Assert.That(preview.Zoom, Is.EqualTo(1f));
            Assert.That(paths.Select(FileHash).ToArray(), Is.EqualTo(hashes), "All v2/v3/v4 frozen maps remain unchanged by switching and sliders.");
        }

        [UnityTest]
        public IEnumerator FourthPresetRejectsWrongNormalEncodingShaderSlotAndPigmentBeforeRendering()
        {
            yield return LoadComparison(CoatingScenePath);
            string[] expectedErrors =
            {
                "The preview format and packed normal encoding do not match. Prepare the matching v2/v3/v4 sample.",
                "A preview preset's shader and packed sample versions do not match.",
                "The preview preset's clay shader is unavailable or has a different contract.",
                "A preview preset's shader and packed sample versions do not match.",
                "Enhanced presets must use the same pigment density range."
            };
            for (int scenario = 0; scenario < expectedErrors.Length; scenario++)
            {
                var presets = preview.presets.Select(p => new ClayPreviewPreset
                {
                    label = p.label, colorMap = p.colorMap, heightMap = p.heightMap, sampleInfo = p.sampleInfo,
                    sampleMetadataHash = p.sampleMetadataHash, shader = p.shader, improvedMaterial = p.improvedMaterial
                }).ToArray();
                TextAsset temporaryMetadata = null;
                if (scenario == 0 || scenario == 4)
                {
                    var info = JsonUtility.FromJson<CoatingTestInfo>(presets[3].sampleInfo.text);
                    if (scenario == 0) info.heightChannels = ClayPreviewSampleInfo.HeightChannels;
                    else info.minimumDensity = info.minimumDensity > .8f ? .8f : .9f;
                    temporaryMetadata = new TextAsset(JsonUtility.ToJson(info));
                    presets[3].sampleInfo = temporaryMetadata;
                    presets[3].sampleMetadataHash = ClayPreviewSampleInfo.ContentHash(temporaryMetadata.text);
                }
                else if (scenario == 1)
                {
                    presets[3].sampleInfo = presets[2].sampleInfo;
                    presets[3].sampleMetadataHash = presets[2].sampleMetadataHash;
                }
                else if (scenario == 2) presets[3].shader = presets[2].shader;
                else
                {
                    presets[2].sampleInfo = presets[3].sampleInfo; presets[2].sampleMetadataHash = presets[3].sampleMetadataHash;
                    presets[2].shader = presets[3].shader;
                }
                var root = new GameObject("Rejected coating contract"); root.SetActive(false);
                var rejected = root.AddComponent<ClayPreviewController>();
                rejected.sourcePrefab = preview.sourcePrefab; rejected.presets = presets; rejected.defaultPreset = 3;
                int frameRate = Application.targetFrameRate, vsync = QualitySettings.vSyncCount;
                try
                {
                    LogAssert.Expect(LogType.Error, "CLAY_PREVIEW_FAILED: " + expectedErrors[scenario]);
                    root.SetActive(true);
                    Assert.That(rejected.Error, Is.EqualTo(expectedErrors[scenario])); Assert.That(rejected.IsReady, Is.False);
                    Assert.That(rejected.ClayMaterial, Is.Null); Assert.That(rejected.ModelRoot, Is.Null);
                    Assert.That(Application.targetFrameRate, Is.EqualTo(frameRate)); Assert.That(QualitySettings.vSyncCount, Is.EqualTo(vsync));
                }
                finally { Object.Destroy(root); if (temporaryMetadata != null) Object.Destroy(temporaryMetadata); }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ActualLightControlsClampRejectNonfiniteAndPreserveLightingAcrossPresetSwitches()
        {
            yield return LoadComparison(CoatingScenePath);
            var root = preview.GetComponent<UIDocument>().rootVisualElement;
            Slider azimuth = root.Q<Slider>("lightAzimuthSlider"), elevation = root.Q<Slider>("lightElevationSlider");
            Slider intensity = root.Q<Slider>("lightIntensitySlider");
            Assert.That(azimuth, Is.Not.Null); Assert.That(elevation, Is.Not.Null); Assert.That(intensity, Is.Not.Null);
            Assert.That(root.Q<Label>("lightingHeading").worldBound.yMin, Is.GreaterThanOrEqualTo(root.Q<Slider>("detailSlider").worldBound.yMax),
                "The light section must scroll below the material controls, rather than compressing their headings.");
            Assert.That(azimuth.lowValue, Is.EqualTo(-180f)); Assert.That(azimuth.highValue, Is.EqualTo(180f));
            Assert.That(elevation.lowValue, Is.EqualTo(-80f)); Assert.That(elevation.highValue, Is.EqualTo(80f));
            Assert.That(intensity.lowValue, Is.Zero); Assert.That(intensity.highValue, Is.EqualTo(2f));
            Light ownedLight = preview.PreviewLight;
            Assert.That(ownedLight, Is.Not.Null); Assert.That(ownedLight.type, Is.EqualTo(LightType.Directional));
            Assert.That(ownedLight.transform.IsChildOf(preview.transform), Is.True, "The experiment owns its light, rather than changing a shared Scene light.");
            AssertLighting(-41.4914f, 70.0484f, 1f);
            Assert.That(Quaternion.Angle(ownedLight.transform.rotation, Quaternion.Euler(70.0484f, -221.4914f, 0f)), Is.LessThan(.01f));

            preview.SetView(53f, 19f, 1.2f); preview.SetAppearance(.017f, .22f); preview.SetSurfaceDetails(.27f, .6f);
            azimuth.value = -71f; elevation.value = 22f; intensity.value = 1.35f;
            yield return null;
            AssertLighting(-71f, 22f, 1.35f);
            Submit(root.Q<Button>("resetLightButton")); yield return null;
            AssertLighting(-41.4914f, 70.0484f, 1f);
            AssertAppearance(.017f, .22f); AssertSurfaceDetails(.27f, .6f);
            Assert.That(preview.Yaw, Is.EqualTo(53f)); Assert.That(preview.Pitch, Is.EqualTo(19f)); Assert.That(preview.Zoom, Is.EqualTo(1.2f));
            Assert.That(preview.ActivePreset, Is.EqualTo(3), "Reset light must leave the artwork/material preset alone.");

            preview.SetLighting(999f, -999f, 99f);
            AssertLighting(180f, -80f, 2f);
            Assert.That(azimuth.value, Is.EqualTo(180f)); Assert.That(elevation.value, Is.EqualTo(-80f)); Assert.That(intensity.value, Is.EqualTo(2f));
            Quaternion rotation = ownedLight.transform.rotation;
            foreach (float[] invalid in new[] { new[] { float.NaN, 35f, 1f }, new[] { 0f, float.PositiveInfinity, 1f }, new[] { 0f, 35f, float.NegativeInfinity } })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => preview.SetLighting(invalid[0], invalid[1], invalid[2]));
                AssertLighting(180f, -80f, 2f);
                Assert.That(Quaternion.Angle(ownedLight.transform.rotation, rotation), Is.LessThan(.0001f), "Reject the entire request before changing any light field.");
            }

            preview.SetLighting(-46f, 28f, 1.25f);
            foreach (string button in new[] { "originalPreset", "materialPreset", "roundedPreset", "coatingPreset" })
            {
                Submit(root.Q<Button>(button)); yield return null;
                Assert.That(preview.PreviewLight, Is.SameAs(ownedLight));
                AssertLighting(-46f, 28f, 1.25f);
                Assert.That(preview.Yaw, Is.EqualTo(53f)); Assert.That(preview.Pitch, Is.EqualTo(19f)); Assert.That(preview.Zoom, Is.EqualTo(1.2f));
            }
            Submit(root.Q<Button>("resetButton")); yield return null;
            AssertLighting(-41.4914f, 70.0484f, 1f);
            AssertAppearance(.014f, .08f); AssertSurfaceDetails(.15f, 1f);
            Assert.That(preview.Yaw, Is.Zero); Assert.That(preview.Pitch, Is.Zero); Assert.That(preview.Zoom, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator MovingPreviewLightChangesActualGourdPixelsWithoutChangingBackgroundOrFrozenPatterns()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !SystemInfo.supportsAsyncGPUReadback)
                Assert.Ignore("This lighting render test needs a real graphics device with asynchronous readback.");
            yield return LoadComparison(CoatingScenePath);
            var root = preview.GetComponent<UIDocument>().rootVisualElement;
            string[] paths = preview.presets.SelectMany(p => new[] { AssetDatabase.GetAssetPath(p.colorMap),
                AssetDatabase.GetAssetPath(p.heightMap), AssetDatabase.GetAssetPath(p.sampleInfo) }).ToArray();
            string[] files = paths.Select(FileHash).ToArray();
            Texture2D pigment = preview.colorMap, height = preview.heightMap;
            var colorBefore = AsyncGPUReadback.Request(pigment, 0, TextureFormat.RGBA32); yield return Readback(colorBefore);
            string colorPixels = ByteHash(colorBefore.GetData<byte>().ToArray());
            var heightBefore = AsyncGPUReadback.Request(height, 0, TextureFormat.RGBAHalf); yield return Readback(heightBefore);
            string heightPixels = ByteHash(heightBefore.GetData<byte>().ToArray());
            Color background = preview.PreviewCamera.backgroundColor, ambient = RenderSettings.ambientSkyColor;
            float ambientIntensity = RenderSettings.ambientIntensity;
            Material skybox = RenderSettings.skybox;
            preview.SetView(28f, 12f, 1.1f);
            root.Q<Slider>("lightElevationSlider").value = 35f;
            root.Q<Slider>("lightIntensitySlider").value = 1f;
            root.Q<Slider>("lightAzimuthSlider").value = -60f;
            yield return null; yield return null;
            RenderTexture target = preview.PreviewTexture;
            var first = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32); yield return Readback(first);
            byte[] left = first.GetData<byte>().ToArray();
            root.Q<Slider>("lightAzimuthSlider").value = 60f;
            yield return null; yield return null;
            var second = AsyncGPUReadback.Request(preview.PreviewTexture, 0, TextureFormat.RGBA32); yield return Readback(second);
            byte[] right = second.GetData<byte>().ToArray();
            Assert.That(preview.PreviewTexture, Is.SameAs(target));
            Assert.That(right.Length, Is.EqualTo(left.Length));
            Assert.That(right.Where((value, index) => value != left[index]).Take(100).Count(), Is.EqualTo(100),
                "Moving the actual light must change the rendered gourd, not only the sliders or a DirectionalLight Inspector field.");
            Assert.That(BackgroundHash(right), Is.EqualTo(BackgroundHash(left)), "The whole clear-background border stays independent of the light.");
            Assert.That(preview.PreviewCamera.backgroundColor, Is.EqualTo(background));
            Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(ambient)); Assert.That(RenderSettings.ambientIntensity, Is.EqualTo(ambientIntensity));
            Assert.That(RenderSettings.skybox, Is.SameAs(skybox));
            Assert.That(preview.colorMap, Is.SameAs(pigment)); Assert.That(preview.heightMap, Is.SameAs(height));
            var colorAfter = AsyncGPUReadback.Request(pigment, 0, TextureFormat.RGBA32); yield return Readback(colorAfter);
            Assert.That(ByteHash(colorAfter.GetData<byte>().ToArray()), Is.EqualTo(colorPixels), "Light movement cannot repaint the pigment map.");
            var heightAfter = AsyncGPUReadback.Request(height, 0, TextureFormat.RGBAHalf); yield return Readback(heightAfter);
            Assert.That(ByteHash(heightAfter.GetData<byte>().ToArray()), Is.EqualTo(heightPixels), "Light movement cannot alter the packed normal/height map.");
            Assert.That(paths.Select(FileHash).ToArray(), Is.EqualTo(files), "Lighting stays local; saved v2/v3/v4 maps and metadata remain frozen.");

            string BackgroundHash(byte[] image)
            {
                var border = new byte[(2 * target.width + 2 * (target.height - 2)) * 4];
                Buffer.BlockCopy(image, 0, border, 0, target.width * 4);
                Buffer.BlockCopy(image, (target.height - 1) * target.width * 4, border, target.width * 4, target.width * 4);
                int offset = 2 * target.width * 4;
                for (int y = 1; y < target.height - 1; y++)
                {
                    Buffer.BlockCopy(image, y * target.width * 4, border, offset, 4);
                    Buffer.BlockCopy(image, (y * target.width + target.width - 1) * 4, border, offset + 4, 4);
                    offset += 8;
                }
                return ByteHash(border);
            }
        }

        void AssertLighting(float azimuth, float elevation, float intensity)
        {
            Assert.That(preview.LightAzimuth, Is.EqualTo(azimuth).Within(.0001f));
            Assert.That(preview.LightElevation, Is.EqualTo(elevation).Within(.0001f));
            Assert.That(preview.LightIntensity, Is.EqualTo(intensity).Within(.0001f));
            Vector3 direction = new Vector3(Mathf.Sin(azimuth * Mathf.Deg2Rad) * Mathf.Cos(elevation * Mathf.Deg2Rad),
                Mathf.Sin(elevation * Mathf.Deg2Rad), Mathf.Cos(azimuth * Mathf.Deg2Rad) * Mathf.Cos(elevation * Mathf.Deg2Rad));
            Assert.That(Vector3.Distance(-preview.PreviewLight.transform.forward, direction), Is.LessThan(.0001f));
            Assert.That(preview.PreviewLight.intensity, Is.EqualTo(intensity).Within(.0001f));
            Assert.That(preview.ClayMaterial.HasProperty("_ClayPreviewLighting"), Is.True);
            Assert.That(preview.ClayMaterial.GetFloat("_ClayPreviewLighting"), Is.EqualTo(1f));
            Assert.That(preview.ClayMaterial.GetFloat("_ClayPreviewLightIntensity"), Is.EqualTo(intensity).Within(.0001f));
            Vector4 packed = preview.ClayMaterial.GetVector("_ClayPreviewLightDirection");
            Vector3 shaderDirection = new Vector3(packed.x, packed.y, packed.z);
            Assert.That(shaderDirection.magnitude, Is.EqualTo(1f).Within(.0001f));
            Assert.That(Vector3.Distance(shaderDirection, direction), Is.LessThan(.0001f));
        }

        [UnityTest]
        public IEnumerator FiveActualRgbEditorsClampSrgbExactlyAndKeepPaletteAcrossPresetsWithIndependentReset()
        {
            Assert.That(preview.PaletteAvailable, Is.False, "The preserved single-preset Scene has no palette data dependency.");
            Assert.That(preview.GetComponent<UIDocument>().rootVisualElement.Q<IntegerField>("palette0RField"), Is.Null);
            yield return LoadComparison(CoatingScenePath);
            Assert.That(preview.PaletteAvailable, Is.True); Assert.That(preview.paletteData, Is.Not.Null);
            yield return WaitPaletteReady();
            Color32[] defaults = Enumerable.Range(0, 5).Select(preview.GetPaletteSrgb).ToArray();
            Color32[] edited =
            {
                new Color32(128, 42, 215, 255), new Color32(32, 128, 74, 255), new Color32(197, 61, 128, 255),
                new Color32(12, 221, 139, 255), new Color32(144, 75, 33, 255)
            };
            var root = preview.GetComponent<UIDocument>().rootVisualElement;
            string sourceData = EditorJsonUtility.ToJson(preview.paletteData);
            string[] sourcePaths = PaletteSourcePaths(); string[] sourceHashes = sourcePaths.Select(FileHash).ToArray();
            for (int i = 0; i < edited.Length; i++)
            {
                foreach (string channel in new[] { "R", "G", "B" })
                {
                    IntegerField field = root.Q<IntegerField>("palette" + i + channel + "Field");
                    Assert.That(field, Is.Not.Null); Assert.That(field.isDelayed, Is.True, "Commit integer edits on Enter/focus loss.");
                }
                root.Q<IntegerField>("palette" + i + "RField").value = edited[i].r;
                root.Q<IntegerField>("palette" + i + "GField").value = edited[i].g;
                root.Q<IntegerField>("palette" + i + "BField").value = edited[i].b;
                AssertPaletteColor(i, edited[i]);
            }
            Assert.That(preview.GetPaletteLinear(0).r, Is.EqualTo(.2158605f).Within(.000001f),
                "sRGB integer 128 must become linear .21586, rather than being stored as linear .50196.");
            root.Q<IntegerField>("palette4RField").value = -1;
            root.Q<IntegerField>("palette4BField").value = 300;
            edited[4] = new Color32(0, edited[4].g, 255, 255);
            AssertPaletteColor(4, edited[4]);
            yield return WaitPaletteReady();
            Color32[] beforeInvalid = Enumerable.Range(0, 5).Select(preview.GetPaletteSrgb).ToArray();
            Assert.Throws<ArgumentOutOfRangeException>(() => preview.SetPaletteRgb(-1, 1, 2, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => preview.SetPaletteRgb(5, 1, 2, 3));
            Assert.That(Enumerable.Range(0, 5).Select(preview.GetPaletteSrgb).ToArray(), Is.EqualTo(beforeInvalid));
            Assert.That(preview.PaletteReady, Is.True); Assert.That(preview.PaletteBusy, Is.False);

            TextField hex = root.Q<TextField>("palette0HexField");
            Assert.That(hex, Is.Not.Null); Assert.That(hex.isDelayed, Is.True);
            hex.value = "12abEF"; edited[0] = new Color32(18, 171, 239, 255); AssertPaletteColor(0, edited[0]);
            Assert.That(preview.TrySetPaletteHex(1, " \t#0080ff\r\n"), Is.True);
            edited[1] = new Color32(0, 128, 255, 255); AssertPaletteColor(1, edited[1]);
            for (int i = 0; i < 5; i++)
            {
                TextField field = root.Q<TextField>("palette" + i + "HexField");
                Assert.That(field, Is.Not.Null); Assert.That(field.isDelayed, Is.True);
                field.value = "ffffff"; AssertPaletteColor(i, new Color32(255, 255, 255, 255));
                field.value = "000000"; AssertPaletteColor(i, new Color32(0, 0, 0, 255));
                field.value = ColorUtility.ToHtmlStringRGB(edited[i]); AssertPaletteColor(i, edited[i]);
            }
            yield return WaitPaletteReady();
            Texture published = preview.ClayMaterial.GetTexture("_PaintMap");
            Color32[] beforeInvalidHex = Enumerable.Range(0, 5).Select(preview.GetPaletteSrgb).ToArray();
            foreach (string input in new[] { null, "", "abc", "12345678", "12GHef", "#", "ＡＢＣＤＥＦ", "１２ABEF", "12éDEF", "12😀34" })
            {
                Assert.That(preview.TrySetPaletteHex(0, input), Is.False, "Reject invalid ASCII six-digit HEX without a new replay: " + input);
                Assert.That(Enumerable.Range(0, 5).Select(preview.GetPaletteSrgb).ToArray(), Is.EqualTo(beforeInvalidHex));
                Assert.That(preview.ClayMaterial.GetTexture("_PaintMap"), Is.SameAs(published));
                Assert.That(preview.PaletteReady, Is.True); Assert.That(preview.PaletteBusy, Is.False);
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => preview.TrySetPaletteHex(-1, ""));
            Assert.Throws<ArgumentOutOfRangeException>(() => preview.TrySetPaletteHex(5, "000000"));
            hex.value = "xyz123";
            Assert.That(hex.value, Is.EqualTo("xyz123"), "Keep the rejected text so the player can correct it.");
            Assert.That(hex.ClassListContains("clay-hex-invalid"), Is.True);
            Assert.That(root.Q<Label>("paletteStatus").text, Does.Contain("HEX"));
            Assert.That(preview.GetPaletteSrgb(0), Is.EqualTo(edited[0]));
            Assert.That(preview.ClayMaterial.GetTexture("_PaintMap"), Is.SameAs(published));
            hex.value = " #0080ff "; edited[0] = new Color32(0, 128, 255, 255); AssertPaletteColor(0, edited[0]);
            hex.value = "123";
            Assert.That(hex.ClassListContains("clay-hex-invalid"), Is.True);
            root.Q<IntegerField>("palette0RField").value = 64;
            edited[0] = new Color32(64, 128, 255, 255); AssertPaletteColor(0, edited[0]);
            yield return WaitPaletteReady();

            preview.SetView(33f, 15f, 1.1f); preview.SetLighting(-35f, 50f, 1.2f);
            foreach (string button in new[] { "originalPreset", "materialPreset", "roundedPreset", "coatingPreset" })
            {
                Submit(root.Q<Button>(button)); yield return WaitPaletteReady();
                for (int i = 0; i < 5; i++) AssertPaletteColor(i, edited[i]);
                Assert.That(preview.ClayMaterial.GetTexture("_HeightMap"), Is.SameAs(preview.heightMap), "Palette replay never replaces the frozen height/normal binding.");
                AssertLighting(-35f, 50f, 1.2f);
            }
            Submit(root.Q<Button>("resetLightButton"));
            for (int i = 0; i < 5; i++) AssertPaletteColor(i, edited[i]);
            preview.SetLighting(-35f, 50f, 1.2f); preview.SetAppearance(.016f, .2f); preview.SetSurfaceDetails(.26f, .6f);
            root.Q<TextField>("palette4HexField").value = "123";
            Assert.That(root.Q<TextField>("palette4HexField").ClassListContains("clay-hex-invalid"), Is.True);
            Submit(root.Q<Button>("resetPaletteButton"));
            Assert.That(preview.PaletteReady, Is.True, "Reset to the frozen source palette uses the immediate default fast path.");
            Assert.That(preview.PaletteBusy, Is.False);
            for (int i = 0; i < 5; i++) AssertPaletteColor(i, defaults[i]);
            AssertAppearance(.016f, .2f); AssertSurfaceDetails(.26f, .6f); AssertLighting(-35f, 50f, 1.2f);
            Assert.That(preview.Yaw, Is.EqualTo(33f)); Assert.That(preview.Pitch, Is.EqualTo(15f)); Assert.That(preview.Zoom, Is.EqualTo(1.1f));
            preview.SetPaletteRgb(0, edited[0].r, edited[0].g, edited[0].b); yield return WaitPaletteReady();
            Submit(root.Q<Button>("resetButton"));
            Assert.That(preview.PaletteReady, Is.True);
            for (int i = 0; i < 5; i++) AssertPaletteColor(i, defaults[i]);
            AssertAppearance(.014f, .08f); AssertLighting(-41.4914f, 70.0484f, 1f);
            Assert.That(preview.Yaw, Is.Zero); Assert.That(preview.Pitch, Is.Zero); Assert.That(preview.Zoom, Is.EqualTo(1f));
            Assert.That(EditorJsonUtility.ToJson(preview.paletteData), Is.EqualTo(sourceData));
            Assert.That(sourcePaths.Select(FileHash).ToArray(), Is.EqualTo(sourceHashes));
        }

        [UnityTest]
        public IEnumerator PaletteReplayChangesActualPixelsKeepsFrozenHeightAndJournalAndResetRestoresTheOriginalImage()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !SystemInfo.supportsAsyncGPUReadback)
                Assert.Ignore("This palette rendering test needs a real graphics device with asynchronous readback.");
            yield return LoadComparison(CoatingScenePath);
            AssertNoNewSessions();
            Assert.That(preview.PaletteAvailable, Is.True); yield return WaitPaletteReady();
            string[] sourcePaths = PaletteSourcePaths(); string[] sourceHashes = sourcePaths.Select(FileHash).ToArray();
            string sourceData = EditorJsonUtility.ToJson(preview.paletteData);
            Texture2D frozenColor = preview.colorMap, frozenHeight = preview.heightMap;
            Assert.That(preview.ClayMaterial.GetTexture("_PaintMap"), Is.SameAs(frozenColor));
            Assert.That(preview.ClayMaterial.GetTexture("_HeightMap"), Is.SameAs(frozenHeight));
            preview.SetView(24f, 10f, 1f); preview.SetLighting(-28f, 55f, 1f);
            var heightBefore = AsyncGPUReadback.Request(frozenHeight, 0, TextureFormat.RGBAHalf); yield return Readback(heightBefore);
            string heightHashBefore = ByteHash(heightBefore.GetData<byte>().ToArray());
            var pigmentBefore = AsyncGPUReadback.Request(frozenColor, 0, TextureFormat.RGBA32); yield return Readback(pigmentBefore);
            string pigmentHashBefore = ByteHash(pigmentBefore.GetData<byte>().ToArray());
            yield return null; yield return null;
            var initial = AsyncGPUReadback.Request(preview.PreviewTexture, 0, TextureFormat.RGBA32); yield return Readback(initial);
            byte[] before = initial.GetData<byte>().ToArray();
            Color background = preview.PreviewCamera.backgroundColor;
            Color32[] alternate = { new Color32(42, 225, 200, 255), new Color32(224, 45, 170, 255), new Color32(190, 220, 40, 255),
                new Color32(30, 70, 240, 255), new Color32(235, 90, 220, 255) };
            for (int i = 0; i < alternate.Length; i++) preview.SetPaletteRgb(i, alternate[i].r, alternate[i].g, alternate[i].b);
            yield return WaitPaletteReady(); yield return null; yield return null;
            Texture replayPaint = preview.ClayMaterial.GetTexture("_PaintMap");
            Assert.That(replayPaint, Is.InstanceOf<RenderTexture>()); Assert.That(replayPaint, Is.Not.SameAs(frozenColor));
            Assert.That(((RenderTexture)replayPaint).IsCreated(), Is.True);
            Assert.That(preview.colorMap, Is.SameAs(frozenColor)); Assert.That(preview.heightMap, Is.SameAs(frozenHeight));
            Assert.That(preview.ClayMaterial.GetTexture("_HeightMap"), Is.SameAs(frozenHeight));
            var edited = AsyncGPUReadback.Request(preview.PreviewTexture, 0, TextureFormat.RGBA32); yield return Readback(edited);
            byte[] after = edited.GetData<byte>().ToArray();
            Assert.That(after.Length, Is.EqualTo(before.Length));
            Assert.That(after.Where((value, index) => value != before[index]).Take(100).Count(), Is.EqualTo(100),
                "An edited palette must change the rendered artwork after the requested replay is fully ready.");
            Assert.That(preview.PreviewCamera.backgroundColor, Is.EqualTo(background));
            var heightAfter = AsyncGPUReadback.Request(frozenHeight, 0, TextureFormat.RGBAHalf); yield return Readback(heightAfter);
            Assert.That(ByteHash(heightAfter.GetData<byte>().ToArray()), Is.EqualTo(heightHashBefore));
            var pigmentAfter = AsyncGPUReadback.Request(frozenColor, 0, TextureFormat.RGBA32); yield return Readback(pigmentAfter);
            Assert.That(ByteHash(pigmentAfter.GetData<byte>().ToArray()), Is.EqualTo(pigmentHashBefore), "The original frozen pigment remains a read-only source.");
            Submit(preview.GetComponent<UIDocument>().rootVisualElement.Q<Button>("resetPaletteButton"));
            Assert.That(preview.PaletteReady, Is.True); Assert.That(preview.PaletteBusy, Is.False);
            Assert.That(preview.ClayMaterial.GetTexture("_PaintMap"), Is.SameAs(frozenColor));
            Assert.That(preview.ClayMaterial.GetTexture("_HeightMap"), Is.SameAs(frozenHeight));
            yield return null; yield return null;
            var restored = AsyncGPUReadback.Request(preview.PreviewTexture, 0, TextureFormat.RGBA32); yield return Readback(restored);
            Assert.That(ByteHash(restored.GetData<byte>().ToArray()), Is.EqualTo(ByteHash(before)), "Reset must restore the exact original fixed-view image, not another random replay.");
            Assert.That(replayPaint == null || replayPaint is RenderTexture released && !released.IsCreated(), Is.True,
                "Returning to the native frozen map releases the owned palette replay maps.");
            Assert.That(EditorJsonUtility.ToJson(preview.paletteData), Is.EqualTo(sourceData));
            Assert.That(sourcePaths.Select(FileHash).ToArray(), Is.EqualTo(sourceHashes), "Palette replay cannot rewrite frozen files, the recorded journal or saved metadata.");
            AssertNoNewSessions();
        }

        static MonoBehaviour[] FindSessions() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(component => component != null && (component.GetType().FullName == "C6Lab.LabNetwork" || component.GetType().FullName == "Hapio.GameSession")).ToArray();

        void AssertNoNewSessions()
        {
            // A previous fixture may own a DontDestroyOnLoad session. Do not delete it or mistake it for ours.
            string[] added = FindSessions().Where(component => !existingSessions.Any(existing => ReferenceEquals(existing, component)))
                .Select(component => component.GetType().FullName + " / " + component.name + " / " + component.gameObject.scene.name).ToArray();
            Assert.That(added, Is.Empty, "Local palette inspection must not create a game room or network session.");
        }

        IEnumerator WaitPaletteReady()
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while ((!preview.PaletteReady || preview.PaletteBusy) && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(preview.PaletteError, Is.Null.Or.Empty); yield return null;
            }
            Assert.That(preview.PaletteError, Is.Null.Or.Empty);
            Assert.That(preview.PaletteReady, Is.True, "The latest palette/preset request must finish within fifteen seconds.");
            Assert.That(preview.PaletteBusy, Is.False);
        }

        void AssertPaletteColor(int slot, Color32 expected)
        {
            Assert.That(preview.GetPaletteSrgb(slot), Is.EqualTo(expected));
            Color actual = preview.GetPaletteLinear(slot), linear = ((Color)expected).linear;
            Assert.That(actual.r, Is.EqualTo(linear.r).Within(.000001f)); Assert.That(actual.g, Is.EqualTo(linear.g).Within(.000001f));
            Assert.That(actual.b, Is.EqualTo(linear.b).Within(.000001f)); Assert.That(actual.a, Is.EqualTo(1f));
            var root = preview.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(root.Q<IntegerField>("palette" + slot + "RField").value, Is.EqualTo(expected.r));
            Assert.That(root.Q<IntegerField>("palette" + slot + "GField").value, Is.EqualTo(expected.g));
            Assert.That(root.Q<IntegerField>("palette" + slot + "BField").value, Is.EqualTo(expected.b));
            TextField hex = root.Q<TextField>("palette" + slot + "HexField"); Assert.That(hex, Is.Not.Null);
            Assert.That(hex.value, Is.EqualTo(ColorUtility.ToHtmlStringRGB(expected)));
            Assert.That(hex.ClassListContains("clay-hex-invalid"), Is.False);
            Image swatch = root.Q<Image>("palette" + slot + "Swatch"); Assert.That(swatch, Is.Not.Null);
            Color shown = swatch.tintColor, srgb = expected;
            Assert.That(shown.r, Is.EqualTo(srgb.r).Within(.000001f)); Assert.That(shown.g, Is.EqualTo(srgb.g).Within(.000001f));
            Assert.That(shown.b, Is.EqualTo(srgb.b).Within(.000001f)); Assert.That(shown.a, Is.EqualTo(1f));
        }

        string[] PaletteSourcePaths()
        {
            string data = AssetDatabase.GetAssetPath(preview.paletteData);
            Assert.That(data, Is.Not.Empty, "The comparison must reference the saved, immutable palette/journal data asset.");
            return preview.presets.SelectMany(p => new[] { AssetDatabase.GetAssetPath(p.colorMap), AssetDatabase.GetAssetPath(p.heightMap),
                AssetDatabase.GetAssetPath(p.sampleInfo) }).Concat(AssetDatabase.GetDependencies(data, true))
                .Where(path => File.Exists(AbsolutePath(path))).Distinct().ToArray();
        }

        [Serializable] private sealed class CoatingTestInfo : ClayPreviewSampleInfo
        { public float minimumDensity, maximumDensity; }

        [UnityTest]
        public IEnumerator UnloadingReleasesOnlyOwnedObjectsAndRestoresApplicationSettings()
        {
            Transform model = preview.ModelRoot;
            RenderTexture target = preview.PreviewTexture;
            Material material = preview.ClayMaterial;
            Camera camera = preview.PreviewCamera;
            GameObject prefab = preview.sourcePrefab;
            Texture2D color = preview.colorMap, height = preview.heightMap;
            Assert.That(Application.targetFrameRate, Is.EqualTo(30));
            Assert.That(QualitySettings.vSyncCount, Is.Zero);
            Assert.That(Application.runInBackground, Is.False);
            yield return UnloadPreview();
            Assert.That(model == null, Is.True);
            Assert.That(camera == null, Is.True);
            Assert.That(material == null, Is.True);
            Assert.That(target == null, Is.True);
            Assert.That(prefab != null && color != null && height != null, Is.True,
                "Disposing the preview must not destroy its shared saved assets.");
            Assert.That(Application.targetFrameRate, Is.EqualTo(previousFrameRate));
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(previousVSync));
            Assert.That(Application.runInBackground, Is.EqualTo(previousBackground));
        }

        void AssertAppearance(float thickness, float gloss)
        {
            Assert.That(preview.Thickness, Is.EqualTo(thickness).Within(.000001f));
            Assert.That(preview.Gloss, Is.EqualTo(gloss).Within(.000001f));
            Assert.That(preview.ClayMaterial.GetFloat("_ClayThickness"), Is.EqualTo(thickness).Within(.000001f));
            Assert.That(preview.ClayMaterial.GetFloat("_ClayGloss"), Is.EqualTo(gloss).Within(.000001f));
        }

        void AssertSurfaceDetails(float contact, float detail)
        {
            Assert.That(preview.ContactStrength, Is.EqualTo(contact).Within(.000001f));
            Assert.That(preview.DetailStrength, Is.EqualTo(detail).Within(.000001f));
            Assert.That(preview.ClayMaterial.GetFloat("_ClayContactStrength"), Is.EqualTo(contact).Within(.000001f));
            Assert.That(preview.ClayMaterial.GetFloat("_ClayDetailStrength"), Is.EqualTo(detail).Within(.000001f));
        }

        IEnumerator LoadComparison(string path = RoundedScenePath)
        {
            yield return UnloadPreview();
            Assert.That(File.Exists(AbsolutePath(path)), Is.True, "Prepare the isolated clay comparison first.");
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            scene = SceneManager.GetSceneByPath(path);
            preview = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ClayPreviewController>(true)).Single();
            float deadline = Time.realtimeSinceStartup + 5f;
            while ((!preview.IsReady || preview.GetComponent<UIDocument>().rootVisualElement.panel == null)
                && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(preview.Error, Is.Null.Or.Empty);
            Assert.That(preview.IsReady, Is.True);
        }

        static void Submit(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.panel, Is.Not.Null, "Submit must target a button attached to the live panel.");
            Assert.That(button.enabledInHierarchy, Is.True);
            button.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        static IEnumerator Readback(AsyncGPUReadbackRequest request)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!request.done && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(request.done, Is.True, "GPU readback did not complete within five seconds.");
            Assert.That(request.hasError, Is.False);
        }

        IEnumerator UnloadPreview()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                Scene cleanup = SceneManager.CreateScene("Clay preview test cleanup");
                SceneManager.SetActiveScene(cleanup);
                yield return SceneManager.UnloadSceneAsync(scene);
                yield return null;
            }
            preview = null;
        }

        static void AssertModelFitsCamera(Transform model, Camera camera)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(false).Where(x => x.enabled).ToArray();
            Assert.That(renderers.Length, Is.GreaterThan(0));
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer part in renderers.Skip(1)) bounds.Encapsulate(part.bounds);
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 point = camera.WorldToViewportPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                Assert.That(point.z, Is.GreaterThan(camera.nearClipPlane).And.LessThan(camera.farClipPlane));
                Assert.That(point.x, Is.InRange(-.015f, 1.015f), "A body, lid or rope part is horizontally clipped.");
                Assert.That(point.y, Is.InRange(-.015f, 1.015f), "A body, lid or rope part is vertically clipped.");
            }
        }

        static string FileHash(string relativePath)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(AbsolutePath(relativePath)))).Replace("-", string.Empty);
        }

        static string ByteHash(byte[] data)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(data)).Replace("-", string.Empty).ToLowerInvariant();
        }

        static string AbsolutePath(string relativePath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
    }
}
#endif
