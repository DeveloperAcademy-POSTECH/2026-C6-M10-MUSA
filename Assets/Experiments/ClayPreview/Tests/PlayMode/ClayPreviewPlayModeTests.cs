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
    public sealed class ClayPreviewPlayModeTests
    {
        const string Root = "Assets/Experiments/ClayPreview/";
        const string ScenePath = Root + "Scenes/ClayPreview.unity";
        const string ColorPath = Root + "Sample/Color.png";
        const string HeightPath = Root + "Sample/Height.exr";
        const string SourcePrefab = "Assets/Lab/Resources/GourdPaint/WhiteGourd.prefab";
        const string SourceConfig = "Assets/Lab/Resources/LabConfig.asset";
        ClayPreviewController preview;
        Scene scene;
        string colorHash, heightHash, configHash, prefabHash;
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
            Assert.That(preview.clayShader.name, Is.EqualTo("C6Lab/ClayStickerSurface"));
            string[] experimentDependencies = AssetDatabase.GetDependencies(ScenePath, true);
            Assert.That(experimentDependencies, Does.Contain(AssetDatabase.GetAssetPath(preview.clayShader)),
                "The production clay shader needs a direct Scene dependency so Player stripping keeps it.");
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

        static string AbsolutePath(string relativePath) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
    }
}
#endif
