#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Purpose = Hapio.Experiments.ClayPreviewImageImport.Purpose;

namespace Hapio.Experiments.Tests
{
    /// <summary>Actual saved image experiment plus bounded importer fixtures. Never opens an OS file dialog.</summary>
    [Category("GPU")]
    public sealed class ClayPreviewImageFilesPlayModeTests
    {
        const string Root = "Assets/Experiments/ClayPreview/";
        const string ScenePath = Root + "ImageSamples/Scenes/ImageClayPreview.unity";
        const string ConfigPath = "Assets/Lab/Resources/LabConfig.asset";
        static readonly string[] SourceNames = { "FineMatteClay.png", "HandKneadedClay.png", "FineHanjiFiber.png" };
        static readonly string[] PresetKeys = { "OriginalV2", "OriginalV3", "RoundedV3", "CoatingV4" };
        readonly Dictionary<string, string> protectedFiles = new Dictionary<string, string>();
        ClayPreviewController preview;
        Scene scene;
        string temporaryDirectory;
        int previousFrameRate, previousVSync;
        bool previousBackground;
        VisualElement Ui => preview.GetComponent<UIDocument>().rootVisualElement;

        [UnitySetUp]
        public IEnumerator OpenOnlyTheSavedImageExperiment()
        {
            protectedFiles.Clear(); scene = default; preview = null; temporaryDirectory = null;
            previousFrameRate = Application.targetFrameRate; previousVSync = QualitySettings.vSyncCount;
            previousBackground = Application.runInBackground;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !SystemInfo.supportsAsyncGPUReadback)
                Assert.Ignore("The saved image experiment requires a real graphics device with readback support.");
            foreach (string name in SourceNames) Protect(Root + "ImageSamples/Sources/" + name);
            foreach (string key in PresetKeys)
            foreach (string file in new[] { "Color.png", "Height.exr", "SampleInfo.json" }) Protect(Root + "Rounded/" + key + "/" + file);
            Protect(ConfigPath);
            Assert.That(File.Exists(Absolute(ScenePath)), Is.True, "Prepare the separate image experiment Scene first.");
            temporaryDirectory = Path.Combine(Application.temporaryCachePath, "hapio-image-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            scene = SceneManager.GetSceneByPath(ScenePath);
            preview = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ClayPreviewController>(true)).Single();
            float deadline = Time.realtimeSinceStartup + 8f;
            while ((!preview.IsReady || Ui.panel == null) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(preview.Error, Is.Null.Or.Empty);
            Assert.That(preview.IsReady, Is.True);
            Assert.That(preview.ImageFilePreviewEnabled, Is.True);
            Assert.That(preview.PresetCount, Is.EqualTo(4));
            foreach (var sample in preview.surfaceTextureSamples ?? Array.Empty<TextAsset>())
                if (sample != null) Protect(AssetDatabase.GetAssetPath(sample));
        }

        [UnityTearDown]
        public IEnumerator ReleaseOnlyOwnedPreviewAndCheckFrozenFiles()
        {
            yield return Unload();
            if (!string.IsNullOrEmpty(temporaryDirectory) && Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true);
            foreach (var source in protectedFiles) Assert.That(FileHash(source.Key), Is.EqualTo(source.Value), "Do not alter source files: " + source.Key);
            Assert.That(Application.targetFrameRate, Is.EqualTo(previousFrameRate));
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(previousVSync));
            Assert.That(Application.runInBackground, Is.EqualTo(previousBackground));
        }

        [Test]
        public void HeadersAndByteLimitsRejectBeforeDecodeAndDoNotReplaceAValidResult()
        {
            using (var valid = ClayPreviewImageImport.LoadBytes(Png(), "valid.png", Purpose.Background))
            {
                Assert.That(valid.Succeeded, Is.True, valid.Error);
                Texture2D original = valid.Texture;
                byte[][] invalid = { null, Array.Empty<byte>(), new byte[] { 1, 2, 3, 4 }, PngHeader(0, 8),
                    PngHeader(2049, 8), PngHeader(8, 2049), new byte[] { 0xff, 0xd8, 0xff, 0xc0, 0 }, JpegHeader(2049, 8),
                    new byte[ClayPreviewImageImport.MaximumFileBytes + 1] };
                foreach (byte[] bytes in invalid)
                {
                    Assert.That(ClayPreviewImageImport.TryGetDimensions(bytes, out _, out _, out string reason), Is.False);
                    Assert.That(reason, Is.Not.Null.And.Not.Empty);
                    using (var rejected = ClayPreviewImageImport.LoadBytes(bytes, "damaged.png", Purpose.SurfaceTexture))
                    {
                        Assert.That(rejected.Succeeded, Is.False); Assert.That(rejected.Cancelled, Is.False);
                        Assert.That(rejected.Texture, Is.Null); Assert.That(rejected.Error, Is.Not.Null.And.Not.Empty);
                    }
                    Assert.That(valid.Texture, Is.SameAs(original));
                }
                Assert.That(ClayPreviewImageImport.TryGetDimensions(PngHeader(2048, 2048), out int width, out int height, out _), Is.True,
                    "The header boundary is allowed without decoding/allocating a 2048-square image.");
                Assert.That(width, Is.EqualTo(2048)); Assert.That(height, Is.EqualTo(2048));
                string oversized = Path.Combine(temporaryDirectory, "oversized.png");
                using (var stream = File.Create(oversized)) stream.SetLength(ClayPreviewImageImport.MaximumFileBytes + 1L);
                using (var rejectedFile = ClayPreviewImageImport.Load(oversized, Purpose.Background))
                    Assert.That(rejectedFile.Succeeded, Is.False);
                using (var unknown = ClayPreviewImageImport.LoadBytes(Png(), "valid.png", (Purpose)99))
                    Assert.That(unknown.Succeeded, Is.False);
                Assert.That(valid.Texture, Is.SameAs(original));
            }
        }

        [UnityTest]
        public IEnumerator PngAndJpegKeepPurposeSpecificColorAlphaAndOwnedTextures()
        {
            byte[] png = Png(); byte[] jpeg = Jpeg();
            using (var surface = ClayPreviewImageImport.LoadBytes(png, "folder/surface.png", Purpose.SurfaceTexture))
            using (var background = ClayPreviewImageImport.LoadBytes(png, "folder/background.png", Purpose.Background))
            using (var jpg = ClayPreviewImageImport.LoadBytes(jpeg, "background.jpg", Purpose.Background))
            {
                Assert.That(surface.Succeeded, Is.True, surface.Error); Assert.That(background.Succeeded, Is.True, background.Error);
                Assert.That(jpg.Succeeded, Is.True, jpg.Error);
                Assert.That(surface.FileName, Is.EqualTo("surface.png"));
                Assert.That(surface.Texture.format, Is.EqualTo(TextureFormat.RGBAHalf));
                Assert.That(GraphicsFormatUtility.IsSRGBFormat(surface.Texture.graphicsFormat), Is.False);
                Assert.That(GraphicsFormatUtility.IsSRGBFormat(background.Texture.graphicsFormat), Is.True);
                Assert.That(surface.Texture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
                Assert.That(background.Texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(surface.Texture.isReadable || background.Texture.isReadable || jpg.Texture.isReadable, Is.False);
                Assert.That(surface.Texture.mipmapCount, Is.GreaterThan(1));
                Assert.That(surface.Texture.filterMode, Is.EqualTo(FilterMode.Trilinear));
                Assert.That(surface.Texture.isReadable, Is.False, "The generated mip chain must not keep an extra CPU copy alive.");
                Assert.That(jpg.Texture.width, Is.EqualTo(4)); Assert.That(jpg.Texture.height, Is.EqualTo(2));
                var rgba = AsyncGPUReadback.Request(background.Texture, 0, TextureFormat.RGBA32); yield return Readback(rgba);
                Color32[] original = rgba.GetData<Color32>().ToArray();
                CollectionAssert.AreEqual(Pixels(), original, "Background PNG keeps authored sRGB bytes and alpha instead of neutralizing it.");
                var data = AsyncGPUReadback.Request(surface.Texture, 0, TextureFormat.RGBAHalf); yield return Readback(data);
                byte[] packed = data.GetData<byte>().ToArray(); double weighted = 0, alphaSum = 0, sourceLuma = 0;
                for (int i = 0; i < original.Length; i++)
                {
                    int offset = i * 8;
                    ushort r = BitConverter.ToUInt16(packed, offset), g = BitConverter.ToUInt16(packed, offset + 2), b = BitConverter.ToUInt16(packed, offset + 4);
                    float gray = Mathf.HalfToFloat(r), alpha = Mathf.HalfToFloat(BitConverter.ToUInt16(packed, offset + 6));
                    Assert.That(g, Is.EqualTo(r)); Assert.That(b, Is.EqualTo(r)); Assert.That(float.IsFinite(gray), Is.True);
                    Assert.That(alpha, Is.EqualTo(original[i].a / 255f).Within(.0005f));
                    if (original[i].a == 0) Assert.That(gray, Is.EqualTo(.5f));
                    weighted += gray * alpha; alphaSum += alpha;
                    sourceLuma += Mathf.GammaToLinearSpace(original[i].r / 255f) * (original[i].a / 255f);
                }
                Assert.That(weighted / alphaSum, Is.EqualTo(.5).Within(.001), "Surface data is centered around neutral without a persistent overall darkening.");
                Assert.That(surface.MeanLuminance, Is.EqualTo(sourceLuma / Pixels().Sum(pixel => pixel.a / 255f)).Within(.00001));
                Texture2D released = surface.Texture; surface.Dispose();
                Assert.That(surface.Texture, Is.Null);
                yield return null;
                Assert.That(released == null, Is.True, "A disposed Result releases only its owned converted texture.");
                Assert.That(background.Texture != null && jpg.Texture != null, Is.True);
            }
            foreach (Color32 fill in new[] { new Color32(128, 128, 128, 255), new Color32(173, 173, 173, 0) })
            using (var uniform = ClayPreviewImageImport.LoadBytes(UniformPng(fill), "uniform.png", Purpose.SurfaceTexture))
            {
                Assert.That(uniform.Succeeded, Is.True, uniform.Error);
                Assert.That(uniform.Texture.mipmapCount, Is.GreaterThan(1));
                Assert.That(uniform.Texture.filterMode, Is.EqualTo(FilterMode.Trilinear));
                Assert.That(uniform.Texture.isReadable, Is.False);
                for (int mip = 0; mip < uniform.Texture.mipmapCount; mip++)
                {
                    var request = AsyncGPUReadback.Request(uniform.Texture, mip, TextureFormat.RGBAHalf);
                    yield return Readback(request);
                    byte[] values = request.GetData<byte>().ToArray();
                    int pixels = Mathf.Max(1, uniform.Texture.width >> mip) * Mathf.Max(1, uniform.Texture.height >> mip);
                    Assert.That(values.Length, Is.EqualTo(pixels * 8));
                    for (int pixel = 0; pixel < pixels; pixel++)
                    {
                        int at = pixel * 8;
                        for (int channel = 0; channel < 3; channel++)
                            Assert.That(Mathf.HalfToFloat(BitConverter.ToUInt16(values, at + channel * 2)), Is.EqualTo(.5f),
                                "Uniform opaque or transparent data must stay exactly neutral at every mip level.");
                        Assert.That(Mathf.HalfToFloat(BitConverter.ToUInt16(values, at + 6)), Is.EqualTo(fill.a == 0 ? 0f : 1f));
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator SavedUiSelectsThreeDistinctSamplesWithoutStartingAnyGameSession()
        {
            Assert.That(preview.defaultPreset, Is.EqualTo(3)); Assert.That(preview.PaletteAvailable, Is.True);
            Assert.That(preview.surfaceTextureSamples, Has.Length.EqualTo(3));
            Assert.That(preview.surfaceTextureSamples.All(sample => sample != null), Is.True);
            Assert.That(preview.surfaceTextureSampleNames, Has.Length.EqualTo(3));
            foreach (string name in new[] { "chooseSurfaceTextureButton", "clearSurfaceTextureButton", "chooseBackgroundButton", "clearBackgroundButton" })
                Assert.That(Ui.Q<Button>(name), Is.Not.Null, name);
            Assert.That(Ui.Q<Toggle>("surfaceTextureToggle"), Is.Not.Null);
            Assert.That(Ui.Q<Slider>("textureScaleSlider"), Is.Not.Null);
            Assert.That(Ui.Q<Slider>("textureColorSlider"), Is.Not.Null);
            Assert.That(Ui.Q<Slider>("textureNormalSlider"), Is.Not.Null);
            Assert.That(Ui.Q<Toggle>("backgroundFillToggle"), Is.Not.Null);
            Texture2D previous = null;
            var sampleHashes = new HashSet<string>();
            for (int i = 0; i < 3; i++)
            {
                sampleHashes.Add(ByteHash(preview.surfaceTextureSamples[i].bytes));
                Submit(Ui.Q<Button>("textureSample" + i + "Button")); yield return null;
                Assert.That(preview.SurfaceTextureError, Is.Null.Or.Empty);
                Assert.That(preview.SurfaceTextureImage, Is.Not.Null);
                Assert.That(preview.SurfaceTextureEnabled, Is.True);
                Assert.That(preview.ClayMaterial.GetTexture("_ClayPreviewTexture"), Is.SameAs(preview.SurfaceTextureImage));
                Assert.That(Ui.Q<Image>("surfaceTextureThumbnail").image, Is.SameAs(preview.SurfaceTextureImage));
                Assert.That(Ui.Q<Label>("surfaceTextureFileName").text, Is.EqualTo(preview.SurfaceTextureFileName));
                if (!ReferenceEquals(previous, null)) Assert.That(previous == null, Is.True, "Replacing a sample releases the previous owned conversion.");
                previous = preview.SurfaceTextureImage;
            }
            Assert.That(sampleHashes.Count, Is.EqualTo(3));
            Assert.That(preview.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component != null
                && (component.GetType().FullName == "Hapio.GameSession" || component.GetType().FullName == "C6Lab.LabNetwork")), Is.False);
            Assert.That(EditorBuildSettings.scenes.Any(entry => entry.path == ScenePath), Is.False);
        }

        [UnityTest]
        public IEnumerator FileErrorsAndPickerCancellationKeepPreviousImagesAndClearSeparately()
        {
            string surfacePath = Path.Combine(temporaryDirectory, "surface.png"), backgroundPath = Path.Combine(temporaryDirectory, "background.jpg");
            byte[] png = Png(), jpg = Jpeg(); File.WriteAllBytes(surfacePath, png); File.WriteAllBytes(backgroundPath, jpg);
            Assert.That(preview.LoadSurfaceTextureFile(surfacePath), Is.True);
            Assert.That(preview.LoadBackgroundImageFile(backgroundPath), Is.True);
            Texture2D surface = preview.SurfaceTextureImage, background = preview.BackgroundTextureImage;
            string surfaceName = preview.SurfaceTextureFileName, backgroundName = preview.BackgroundImageFileName;
            preview.SetSurfaceTextureSettings(false, 7, .4f, .6f); preview.SetBackgroundFill(false);
            Assert.That(preview.LoadSurfaceTextureBytes(new byte[] { 2, 3 }, "broken.png"), Is.False);
            Assert.That(preview.LoadBackgroundImageFile(Path.Combine(temporaryDirectory, "missing.png")), Is.False);
            Assert.That(preview.SurfaceTextureError, Is.Not.Null.And.Not.Empty); Assert.That(preview.BackgroundImageError, Is.Not.Null.And.Not.Empty);
            Assert.That(preview.SurfaceTextureImage, Is.SameAs(surface)); Assert.That(preview.BackgroundTextureImage, Is.SameAs(background));
            Assert.That(preview.SurfaceTextureFileName, Is.EqualTo(surfaceName)); Assert.That(preview.BackgroundImageFileName, Is.EqualTo(backgroundName));
            Assert.That(preview.SurfaceTextureEnabled, Is.False); Assert.That(preview.BackgroundFill, Is.False);
            string beforeError = preview.SurfaceTextureError;
            using (var cancelled = CancelledResult())
            {
                Assert.That(cancelled.Cancelled, Is.True); Assert.That(cancelled.Succeeded, Is.False); Assert.That(cancelled.Error, Is.Null);
                MethodInfo accept = typeof(ClayPreviewController).GetMethod("AcceptImportedImage", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(accept, Is.Not.Null);
                Assert.That((bool)accept.Invoke(preview, new object[] { cancelled, true }), Is.False,
                    "A simulated native-picker cancellation traverses the real controller acceptance path without opening a dialog.");
            }
            Assert.That(preview.SurfaceTextureImage, Is.SameAs(surface)); Assert.That(preview.SurfaceTextureError, Is.EqualTo(beforeError));
            Assert.That(File.ReadAllBytes(surfacePath), Is.EqualTo(png)); Assert.That(File.ReadAllBytes(backgroundPath), Is.EqualTo(jpg));
            Submit(Ui.Q<Button>("clearSurfaceTextureButton")); yield return null;
            Assert.That(preview.SurfaceTextureImage, Is.Null); Assert.That(surface == null, Is.True);
            Assert.That(preview.BackgroundTextureImage, Is.SameAs(background)); Assert.That(preview.BackgroundFill, Is.False);
            Submit(Ui.Q<Button>("clearBackgroundButton")); yield return null;
            Assert.That(preview.BackgroundTextureImage, Is.Null); Assert.That(background == null, Is.True);
        }

        [UnityTest, Category("Slow")]
        public IEnumerator TextureValuesAndFilesStayIndependentAcrossPresetsPaletteLightingViewAndReset()
        {
            Assert.That(preview.LoadSurfaceTextureBytes(Png(), "surface.png"), Is.True);
            Assert.That(preview.LoadBackgroundImageBytes(Png(), "background.png"), Is.True);
            Texture2D surface = preview.SurfaceTextureImage, background = preview.BackgroundTextureImage;
            Ui.Q<Slider>("textureScaleSlider").value = 7;
            Ui.Q<Slider>("textureColorSlider").value = .45f;
            Ui.Q<Slider>("textureNormalSlider").value = .65f;
            Ui.Q<Toggle>("surfaceTextureToggle").value = false;
            Ui.Q<Toggle>("backgroundFillToggle").value = false;
            AssertImageSettings(surface, background, false, 7, .45f, .65f, false);
            preview.SetPaletteRgb(0, 100, 87, 54);
            for (int preset = 0; preset < 4; preset++)
            {
                preview.SelectPreset(preset); preview.SetLighting(85, 30, 1.2f); preview.SetView(50, 15, 1.2f);
                AssertImageSettings(surface, background, false, 7, .45f, .65f, false);
                float deadline = Time.realtimeSinceStartup + 12f;
                while (!preview.PaletteReady && string.IsNullOrEmpty(preview.PaletteError) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(preview.PaletteError, Is.Null.Or.Empty); Assert.That(preview.PaletteReady, Is.True);
                AssertImageSettings(surface, background, false, 7, .45f, .65f, false);
                Ui.Q<Toggle>("surfaceTextureToggle").value = true; yield return null;
                AssertImageSettings(surface, background, true, 7, .45f, .65f, false);
                Ui.Q<Toggle>("surfaceTextureToggle").value = false;
            }
            Submit(Ui.Q<Button>("resetButton")); yield return null;
            Assert.That(preview.SurfaceTextureImage, Is.SameAs(surface)); Assert.That(preview.BackgroundTextureImage, Is.SameAs(background));
            Assert.That(preview.SurfaceTextureFileName, Is.EqualTo("surface.png")); Assert.That(preview.BackgroundImageFileName, Is.EqualTo("background.png"));
            Assert.That(preview.GetPaletteSrgb(0), Is.EqualTo(new Color32(216, 87, 54, 255)));
            Assert.That(preview.LightIntensity, Is.EqualTo(1)); Assert.That(preview.Yaw, Is.Zero); Assert.That(preview.Pitch, Is.Zero);
            Assert.That(preview.ClayMaterial.GetTexture("_ClayPreviewTexture"), Is.SameAs(surface));
            Assert.That(preview.SurfaceTextureError, Is.Null.Or.Empty); Assert.That(preview.BackgroundImageError, Is.Null.Or.Empty);
        }

        [UnityTest]
        public IEnumerator SceneUnloadReleasesConvertedFilesAndBackgroundResourcesButNotSourceAssets()
        {
            Assert.That(preview.LoadSurfaceTextureSample(0), Is.True);
            Assert.That(preview.LoadBackgroundImageBytes(Png(), "background.png"), Is.True);
            Texture2D oldSurface = preview.SurfaceTextureImage, oldBackground = preview.BackgroundTextureImage;
            Assert.That(preview.LoadSurfaceTextureSample(1), Is.True);
            Assert.That(preview.LoadBackgroundImageBytes(Jpeg(), "replacement.jpg"), Is.True); yield return null;
            Assert.That(oldSurface == null && oldBackground == null, Is.True);
            Texture2D surface = preview.SurfaceTextureImage, background = preview.BackgroundTextureImage;
            MeshRenderer backgroundRenderer = preview.GetComponentsInChildren<MeshRenderer>(true).Single(renderer => renderer.name == "Imported preview background");
            Mesh mesh = backgroundRenderer.GetComponent<MeshFilter>().sharedMesh;
            Material material = backgroundRenderer.sharedMaterial, clay = preview.ClayMaterial;
            var sourceMaps = preview.presets.SelectMany(preset => new[] { preset.colorMap, preset.heightMap }).ToArray();
            var sourceSamples = (TextAsset[])preview.surfaceTextureSamples.Clone();
            yield return Unload();
            Assert.That(surface == null && background == null, Is.True, "Converted texture results belong to this preview session only.");
            Assert.That(backgroundRenderer == null && mesh == null && material == null && clay == null, Is.True,
                "The background mesh/material and clay clone must be released with their owner.");
            Assert.That(sourceMaps.All(map => map != null) && sourceSamples.All(sample => sample != null), Is.True,
                "Disposal must never destroy referenced source textures or PNG-byte assets.");
        }

        void AssertImageSettings(Texture2D surface, Texture2D background, bool enabled, float scale, float contrast, float relief, bool fill)
        {
            Assert.That(preview.SurfaceTextureImage, Is.SameAs(surface)); Assert.That(preview.BackgroundTextureImage, Is.SameAs(background));
            Assert.That(preview.SurfaceTextureEnabled, Is.EqualTo(enabled)); Assert.That(preview.SurfaceTextureScale, Is.EqualTo(scale).Within(.00001));
            Assert.That(preview.SurfaceTextureColorStrength, Is.EqualTo(contrast).Within(.00001));
            Assert.That(preview.SurfaceTextureNormalStrength, Is.EqualTo(relief).Within(.00001)); Assert.That(preview.BackgroundFill, Is.EqualTo(fill));
            Assert.That(preview.ClayMaterial.GetTexture("_ClayPreviewTexture"), Is.SameAs(surface));
            Assert.That(preview.ClayMaterial.GetFloat("_ClayPreviewTextureEnabled"), Is.EqualTo(enabled ? 1 : 0));
            Assert.That(preview.ClayMaterial.GetFloat("_ClayPreviewTextureScale"), Is.EqualTo(scale).Within(.00001));
            Assert.That(preview.ClayMaterial.GetFloat("_ClayPreviewTextureColorStrength"), Is.EqualTo(contrast).Within(.00001));
            Assert.That(preview.ClayMaterial.GetFloat("_ClayPreviewTextureNormalStrength"), Is.EqualTo(relief).Within(.00001));
        }
        static ClayPreviewImageImport.Result CancelledResult()
        {
            ConstructorInfo ctor = typeof(ClayPreviewImageImport.Result).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(Texture2D), typeof(string), typeof(string), typeof(bool), typeof(float) }, null);
            Assert.That(ctor, Is.Not.Null);
            return (ClayPreviewImageImport.Result)ctor.Invoke(new object[] { null, null, null, true, .5f });
        }
        static byte[] Png() => Encoded(false);
        static byte[] Jpeg() => Encoded(true);
        static byte[] Encoded(bool jpeg)
        {
            var texture = new Texture2D(4, 2, TextureFormat.RGBA32, false, false) { name = "Image importer test fixture" };
            try { texture.SetPixels32(Pixels()); texture.Apply(); return jpeg ? texture.EncodeToJPG(100) : texture.EncodeToPNG(); }
            finally { Object.Destroy(texture); }
        }
        static byte[] UniformPng(Color32 value)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false, false) { name = "Uniform image neutral-mip fixture" };
            try { texture.SetPixels32(Enumerable.Repeat(value, 16).ToArray()); texture.Apply(); return texture.EncodeToPNG(); }
            finally { Object.Destroy(texture); }
        }
        static Color32[] Pixels() => new[] { new Color32(64, 64, 64, 255), new Color32(192, 192, 192, 128),
            new Color32(230, 230, 230, 0), new Color32(128, 128, 128, 255), new Color32(64, 64, 64, 255),
            new Color32(192, 192, 192, 128), new Color32(230, 230, 230, 0), new Color32(128, 128, 128, 255) };
        static byte[] PngHeader(uint width, uint height)
        {
            var bytes = new byte[33]; byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            Array.Copy(signature, bytes, 8); bytes[11] = 13; bytes[12] = (byte)'I'; bytes[13] = (byte)'H'; bytes[14] = (byte)'D'; bytes[15] = (byte)'R';
            for (int i = 0; i < 4; i++) { bytes[16 + i] = (byte)(width >> ((3 - i) * 8)); bytes[20 + i] = (byte)(height >> ((3 - i) * 8)); }
            bytes[24] = 8; bytes[25] = 6; return bytes;
        }
        static byte[] JpegHeader(int width, int height) => new byte[] { 0xff, 0xd8, 0xff, 0xc0, 0, 11, 8,
            (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 1, 1, 0x11, 0, 0xff, 0xda };
        static void Submit(Button button)
        {
            Assert.That(button, Is.Not.Null); Assert.That(button.panel, Is.Not.Null); Assert.That(button.enabledInHierarchy, Is.True);
            button.Focus(); using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
        static IEnumerator Readback(AsyncGPUReadbackRequest request)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!request.done && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(request.done, Is.True); Assert.That(request.hasError, Is.False);
        }
        IEnumerator Unload()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                Scene cleanup = SceneManager.CreateScene("Image preview test cleanup"); SceneManager.SetActiveScene(cleanup);
                yield return SceneManager.UnloadSceneAsync(scene); yield return null;
            }
            preview = null;
        }
        void Protect(string path)
        {
            Assert.That(string.IsNullOrEmpty(path), Is.False); Assert.That(File.Exists(Absolute(path)), Is.True, path);
            protectedFiles[path] = FileHash(path);
        }
        static string Absolute(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        static string FileHash(string path) => ByteHash(File.ReadAllBytes(Absolute(path)));
        static string ByteHash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant(); }
    }
}
#endif
