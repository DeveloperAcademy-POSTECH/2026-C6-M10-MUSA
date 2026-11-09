#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Hapio.Experiments.Tests
{
    [Category("GPU")]
    public sealed class ClayPreviewShapeControllerTests
    {
        const string ScenePath = "Assets/Experiments/ClayPreview/ImageSamples/Scenes/ImageClayPreview.unity";
        ClayPreviewController preview;
        Scene scene;
        string temporaryDirectory;
        [UnitySetUp]
        public IEnumerator LoadPreview()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("The real preview renderer needs a GPU.");
            temporaryDirectory = Path.Combine(Application.temporaryCachePath, "clay-shape-controller-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            scene = SceneManager.GetSceneByPath(ScenePath);
            preview = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ClayPreviewController>(true)).Single();
            yield return Until(() => preview.IsReady, "Saved image preview must initialize.");
            preview.restoreSavedShapeOnStart = false; preview.ResetShapeProfile();
            yield return Until(() => !preview.ShapeBusy && preview.PaletteReady, "Reset must settle without touching saved user settings.");
            Assert.That(preview.Error, Is.Null.Or.Empty);
        }
        [UnityTearDown]
        public IEnumerator ReleasePreview()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                var cleanup = SceneManager.CreateScene("Shape test cleanup"); SceneManager.SetActiveScene(cleanup);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            yield return null; preview = null;
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true);
        }

        [UnityTest]
        public IEnumerator ReplacingImagesKeepsAppearanceAndLatestSelectionAndInvalidInputKeepsItsPublishedPicture()
        {
            preview.SelectPreset(0); preview.SetAppearance(.011f, .42f); preview.SetSurfaceDetails(.11f, .63f);
            preview.SetLighting(34, 51, .9f); preview.SetPaletteRgb(0, 212, 66, 44);
            yield return Until(() => preview.PaletteReady, "Earlier palette request must settle.");
            Assert.That(preview.LoadShapeImageBytes(ClayPreviewShapeMaskTests.FixturePng(Color.red), "first.png"), Is.True, preview.ShapeError);
            Assert.That(preview.LoadShapeImageBytes(SecondPng(), "second.png"), Is.True, preview.ShapeError);
            yield return Until(() => !preview.ShapeBusy && preview.PaletteReady && preview.ShapeRevision > 0, "Only the newest PNG may be published.");
            Assert.That(preview.ShapeImageFileName, Is.EqualTo("second.png"));
            Assert.That(preview.ActivePreset, Is.EqualTo(3)); Assert.That(preview.ShapeSource, Is.EqualTo(ClayPreviewShapeSource.Image));
            Assert.That(preview.Thickness, Is.EqualTo(.011f).Within(.0001f)); Assert.That(preview.Gloss, Is.EqualTo(.42f).Within(.0001f));
            Assert.That(preview.ContactStrength, Is.EqualTo(.11f).Within(.0001f)); Assert.That(preview.DetailStrength, Is.EqualTo(.63f).Within(.0001f));
            Assert.That(preview.LightAzimuth, Is.EqualTo(34).Within(.0001f)); Assert.That(preview.LightElevation, Is.EqualTo(51).Within(.0001f));
            Assert.That(preview.LightIntensity, Is.EqualTo(.9f).Within(.0001f));
            using (var expected = ClayPreviewShapeMask.LoadBytes(SecondPng(), "expected.png"))
                CollectionAssert.AreEqual(expected.Brush.GetPixels32(), preview.ShapeMaskTexture.GetPixels32());
            Texture heldMap = preview.ClayMaterial.GetTexture("_PaintMap"), heldMask = preview.ShapeMaskTexture;
            int revision = preview.ShapeRevision;
            Assert.That(preview.LoadShapeImageBytes(new byte[] { 9, 4, 2 }, "broken.png"), Is.False);
            yield return null; yield return null;
            Assert.That(preview.ShapeError, Is.Not.Null.And.Not.Empty);
            Assert.That(preview.ShapeMaskTexture, Is.SameAs(heldMask)); Assert.That(preview.ShapeRevision, Is.EqualTo(revision));
            Assert.That(preview.ClayMaterial.GetTexture("_PaintMap"), Is.SameAs(heldMap));
            var replay = (ClayPreviewPaletteReplay)typeof(ClayPreviewController).GetField("paletteReplay",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(preview);
            replay.Request(-1, (Color32[])preview.paletteData.sourceSrgb.Clone());
            yield return null; yield return null;
            Assert.That(preview.PaletteReady, Is.True, "A failed replacement explicitly restores acceptance of the published picture.");
            Assert.That(preview.ShapeError, Is.Not.Null.And.Not.Empty);
            Assert.That(preview.ClayMaterial.GetTexture("_PaintMap"), Is.SameAs(heldMap));
            ((RenderTexture)heldMap).Release();
            yield return Until(() => preview.PaletteReady && ((RenderTexture)preview.ClayMaterial.GetTexture("_PaintMap")).IsCreated(),
                "A released GPU map restores from the published journal even after replacement cancellation.");
            Assert.That(preview.ShapeMaskTexture, Is.SameAs(heldMask));
        }

        [UnityTest]
        public IEnumerator ExplicitProfileReloadRestoresEmbeddedImageAfterSourceDeletionAndOriginalModeRemainsAvailable()
        {
            string source = Path.Combine(temporaryDirectory, "original.png"), profile = Path.Combine(temporaryDirectory, "saved.json");
            File.WriteAllBytes(source, ClayPreviewShapeMaskTests.FixturePng(Color.red));
            Assert.That(preview.LoadShapeImageFile(source), Is.True, preview.ShapeError);
            yield return Until(() => !preview.ShapeBusy && preview.PaletteReady && preview.ShapeRevision > 0, "Image must finish before save.");
            var expected = preview.ShapeMaskTexture.GetPixels32();
            Assert.That(preview.SaveShapeProfile(profile), Is.True, preview.ShapeError); File.Delete(source);
            preview.SetReferenceSplatter();
            yield return Until(() => !preview.ShapeBusy && preview.PaletteReady, "Procedural mode remains available.");
            Assert.That(preview.ShapeSource, Is.EqualTo(ClayPreviewShapeSource.Procedural));
            Assert.That(preview.LoadShapeProfile(profile), Is.True, preview.ShapeError);
            yield return Until(() => !preview.ShapeBusy && preview.PaletteReady, "Saved PNG bytes can be restored without their source path.");
            Assert.That(preview.ShapeSource, Is.EqualTo(ClayPreviewShapeSource.Image));
            CollectionAssert.AreEqual(expected, preview.ShapeMaskTexture.GetPixels32());
            Assert.That(preview.SetShapeSource(ClayPreviewShapeSource.Original), Is.True);
            yield return Until(() => !preview.ShapeBusy && preview.PaletteReady, "Original mode must still bind the saved original artwork.");
            Assert.That(preview.ClayMaterial.GetTexture("_PaintMap"), Is.SameAs(preview.colorMap));
        }

        [UnityTest]
        public IEnumerator PartialScenariosReportActualCellsAndMaximumSupportAndCompletionUndercoatIsIndependent()
        {
            Assert.That(preview.LoadShapeImageBytes(ClayPreviewShapeMaskTests.FixturePng(Color.red), "shape.png"), Is.True);
            yield return Until(() => !preview.ShapeBusy && preview.PaletteReady && preview.ShapeRevision > 0, "Initial image replay must finish.");
            preview.SelectShapeScenario(ClayPreviewShapeScenario.FirstHit);
            yield return Until(() => preview.PaletteReady && !preview.ShapeBusy && preview.ShapeDiagnostics != null && !preview.ShapeDiagnostics.Complete,
                "One real diagnostic hit must publish a partial result.");
            int first = preview.ShapeDiagnostics.PaintedCells;
            Assert.That(first, Is.GreaterThan(0));
            Assert.That(preview.ShapeDiagnostics.MaximumSupportCells, Is.GreaterThanOrEqualTo(first));
            Assert.That(preview.ShapeDiagnostics.MaximumSupportCells, Is.LessThan(256 * 128));
            Assert.That(preview.ShapeDiagnostics.HasFootprint, Is.True);
            Assert.That(preview.ShapeDiagnostics.SupportTexture, Is.Not.Null); Assert.That(preview.ShapeDiagnostics.FootprintTexture, Is.Not.Null);
            yield return null; yield return null; Capture("first-hit");
            preview.SelectShapeScenario(ClayPreviewShapeScenario.FirstHitThreeRepeats);
            yield return Until(() => preview.PaletteReady && !preview.ShapeBusy && preview.ShapeDiagnostics.PaintedCells > first,
                "Three real repeat hits supply more cells without replacing the original frozen record.");
            Assert.That(preview.ShapeDiagnostics.Complete, Is.False); yield return null; yield return null; Capture("three-repeats");
            var ui = preview.GetComponent<UIDocument>().rootVisualElement;
            Assert.That(ui.Query<Label>().ToList().Any(label => label.text.Contains("MASK: 512")), Is.True, "The input canvas specification must be visible separately from cell support.");
            Assert.That(ui.Q<VisualElement>("shapeSupportDiagnostics"), Is.Not.Null);
            preview.SelectShapeScenario(ClayPreviewShapeScenario.Full67);
            yield return Until(() => preview.PaletteReady && !preview.ShapeBusy && preview.ShapeDiagnostics.Complete, "The original frozen 67-hit completion remains available.");
            yield return null; yield return null; Capture("full67");
            preview.SetShapeUndercoatVisible(false);
            yield return Until(() => preview.PaletteReady && !preview.ShapeBusy, "Removing the comparison undercoat must finish both maps.");
            Assert.That(preview.OmitShapeUndercoat, Is.True);
            Assert.That(preview.ShapeDiagnostics.Complete, Is.True); yield return null; yield return null; Capture("full67-without-undercoat");
            preview.SetShapeUndercoatVisible(true);
            yield return Until(() => preview.PaletteReady && !preview.ShapeBusy, "The completion undercoat can be restored.");
        }

        [UnityTest]
        public IEnumerator NestedButtonsKeepTheirHeightAndHitAreaAtTwoWindowSizes()
        {
            int oldWidth = Screen.width, oldHeight = Screen.height;
            var oldMode = Screen.fullScreenMode;
            try
            {
                preview.SetReferenceSplatter();
                yield return Until(() => !preview.ShapeBusy && preview.PaletteReady, "Procedural settings must finish applying.");
                var ui = preview.GetComponent<UIDocument>().rootVisualElement;
                ui.Q<Foldout>("proceduralShapeSettings").value = true;
                foreach (var size in new[] { new Vector2Int(1280, 800), new Vector2Int(960, 600) })
                {
                    Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                    for (int i = 0; i < 5; i++) yield return null;
                    string[] names = { "chooseShapeImage", "newSplatter", "referenceSplatter", "flowingClay", "broadClay",
                        "saveShapeProfile", "loadShapeProfile", "resetShapeProfile" };
                    var buttons = names.Select(name => ui.Q<Button>(name)).ToArray();
                    foreach (var button in buttons)
                    {
                        Assert.That(button, Is.Not.Null);
                        Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(39.5f), button.name + " must not collapse inside nested vertical content.");
                    }
                    for (int i = 1; i < buttons.Length; i++)
                        Assert.That(buttons[i - 1].worldBound.yMax, Is.LessThanOrEqualTo(buttons[i].worldBound.yMin + .5f), "Adjacent vertical actions cannot share a hit region.");
                    var panel = ui.Q<ScrollView>("materialPanel");
                    foreach (var button in new[] { buttons[0], buttons[5], buttons[7] })
                    {
                        panel.ScrollTo(button); yield return null; yield return null;
                        Vector2 center = button.worldBound.center;
                        Assert.That(panel.contentViewport.worldBound.Contains(center), Is.True, button.name + " can be scrolled into view.");
                        var picked = ui.panel.Pick(center);
                        Button hit = picked as Button ?? picked?.GetFirstAncestorOfType<Button>();
                        Assert.That(hit, Is.SameAs(button), button.name + " must own the visible centre click.");
                    }
                    foreach (var row in ui.Query<VisualElement>(className: "clay-button-row").ToList())
                    {
                        var actions = row.Children().OfType<Button>().ToArray();
                        for (int i = 1; i < actions.Length; i++)
                        {
                            Assert.That(actions[i].worldBound.xMin, Is.GreaterThanOrEqualTo(actions[i - 1].worldBound.xMax - .5f));
                            Assert.That(Mathf.Abs(actions[i].worldBound.yMin - actions[0].worldBound.yMin), Is.LessThan(1));
                        }
                    }
                }
            }
            finally { Screen.SetResolution(oldWidth, oldHeight, oldMode); }
        }

        [UnityTest]
        public IEnumerator ImportedPngRemainsStableAcrossSixViews()
        {
            const string demo = "Assets/Experiments/ClayPreview/Rounded/Masks/BroadWhiteMask.png";
            Assert.That(preview.LoadShapeImageBytes(File.ReadAllBytes(demo), "existing-broad-mask-demo.png"), Is.True, preview.ShapeError);
            yield return Until(() => !preview.ShapeBusy && preview.PaletteReady && preview.ShapeRevision > 0, "Existing transparent asset is a demonstration input, not a new approved reference.");
            var mask = preview.ShapeMaskTexture.GetPixels32();
            var texture = preview.ClayMaterial.GetTexture("_PaintMap");
            float[] yaws = { 0, 90, 180, -90, 35, 35 };
            float[] pitches = { 0, 0, 0, 0, 15, 50 };
            for (int i = 0; i < yaws.Length; i++)
            {
                preview.SetView(yaws[i], pitches[i]);
                yield return null; yield return null;
                Capture("existing-mask-demo-view-" + i);
                CollectionAssert.AreEqual(mask, preview.ShapeMaskTexture.GetPixels32(), "Rotating the gourd never regenerates or stretches the source mask.");
                Assert.That(preview.ClayMaterial.GetTexture("_PaintMap"), Is.SameAs(texture));
            }
        }

        [Test]
        public void MaximumSupportWrapsTheSeamAndStopsAtPolesInsteadOfBeingAFixedImageBox()
        {
            const int columns = 8, rows = 6;
            var painted = new bool[columns * rows]; painted[3 * columns] = true;
            var support = ClayPreviewShapeDiagnostics.CalculateMaximumSupport(painted, columns, rows);
            Assert.That(support.Count(x => x), Is.EqualTo(25));
            Assert.That(support[3 * columns + 7], Is.True, "The left neighbour wraps across the gourd seam.");
            Assert.That(support[3 * columns + 3], Is.False, "Three cells away lies outside the two-cell allowance.");
            Array.Clear(painted, 0, painted.Length); painted[0] = true;
            support = ClayPreviewShapeDiagnostics.CalculateMaximumSupport(painted, columns, rows);
            Assert.That(support.Count(x => x), Is.EqualTo(15), "At the top pole only three rows are available.");
            Assert.That(support[(rows - 1) * columns], Is.False, "The top and bottom do not wrap.");
        }

        void Capture(string name)
        {
            string directory = Environment.GetEnvironmentVariable("C6_SHAPE_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) return;
            Directory.CreateDirectory(directory);
            Save(preview.PreviewTexture, Path.Combine(directory, name + ".png"));
            if (preview.ShapePreviewTexture != null) File.WriteAllBytes(Path.Combine(directory, "source-outline.png"), preview.ShapePreviewTexture.EncodeToPNG());
            if (preview.ShapeDiagnostics?.SupportTexture != null) SaveTexture(preview.ShapeDiagnostics.SupportTexture, Path.Combine(directory, name + "-support.png"));
            if (preview.ShapeDiagnostics?.FootprintTexture != null) SaveTexture(preview.ShapeDiagnostics.FootprintTexture, Path.Combine(directory, name + "-cutoff.png"));
        }
        static void SaveTexture(Texture source, string path)
        {
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            try { Graphics.Blit(source, target); Save(target, path); }
            finally { RenderTexture.ReleaseTemporary(target); }
        }
        static void Save(RenderTexture map, string path)
        {
            var texture = new Texture2D(map.width, map.height, TextureFormat.RGBA32, false);
            var old = RenderTexture.active;
            try { RenderTexture.active = map; texture.ReadPixels(new Rect(0, 0, map.width, map.height), 0, 0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { RenderTexture.active = old; Object.DestroyImmediate(texture); }
        }
        static byte[] SecondPng()
        {
            var texture = new Texture2D(32, 64, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[32 * 64];
                for (int y = 5; y < 59; y++) for (int x = 7; x < 25; x++) pixels[y * 32 + x] = Color.green;
                texture.SetPixels32(pixels); texture.Apply(); return texture.EncodeToPNG();
            }
            finally { Object.DestroyImmediate(texture); }
        }
        static IEnumerator Until(Func<bool> condition, string message)
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
#endif
