using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Real saved-mesh/shader pixels; fixtures do not claim projectile or network approval.</summary>
    public sealed class LabPlayerPaintPatternRenderTests
    {
        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Painter render cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [Test]
        public void SixCachedLegendBadgesHaveDistinctPatternsAndIdenticalCircularAlpha()
        {
            Assert.That(LabPlayerPaintPatterns.SpriteFor(0), Is.Null);
            Assert.That(LabPlayerPaintPatterns.SpriteFor(7), Is.Null);
            Assert.That(LabPlayerPaintPatterns.NameFor(0), Is.EqualTo("None"));
            Assert.That(LabPlayerPaintPatterns.NameFor(7), Is.EqualTo("None"));
            var pixels = new Color32[6][];
            string[] expected = { "Dots", "Diagonal", "Horizontal", "Waves", "Grid", "Back diagonal" };
            for (int slot = 1; slot <= 6; slot++)
            {
                Sprite sprite = LabPlayerPaintPatterns.SpriteFor(slot);
                Assert.That(sprite, Is.SameAs(LabPlayerPaintPatterns.SpriteFor(slot)), "Badges must be cached, not allocated per HUD refresh.");
                Assert.That(LabPlayerPaintPatterns.NameFor(slot), Is.EqualTo(expected[slot - 1]));
                pixels[slot - 1] = sprite.texture.GetPixels32();
                Assert.That(pixels[slot - 1].Count(pixel => pixel.a == 0), Is.GreaterThan(100));
                Assert.That(pixels[slot - 1].Count(pixel => pixel.a == 255 && pixel.r < 100), Is.GreaterThan(100));
                for (int other = 0; other < slot - 1; other++)
                {
                    Assert.That(pixels[slot - 1], Is.Not.EqualTo(pixels[other]));
                    Assert.That(pixels[slot - 1].Select(pixel => pixel.a), Is.EqualTo(pixels[other].Select(pixel => pixel.a)));
                }
            }
        }

        [UnityTest]
        public IEnumerator ActualSavedShaderDistinguishesAllSixPaintersWithoutChangingPaperAlphaOrCoverage()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("A real graphics device is required; shader pixels were not tested.");
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            Assert.That(paint.paintMaterial.shader.isSupported, Is.True);
            LabPaintSurface surface = paint.Surface;
            var pixels = new Color32[surface.CellCount];
            var owners = new byte[surface.CellCount];
            using (var rig = new ShellRenderRig(paint))
            {
                paint.ClearPaint();
                Color32[] paper = rig.Capture(paint, null);
                for (int y = surface.Rows / 5; y < surface.Rows * 4 / 5; y++)
                    for (int x = surface.Columns * 3 / 5; x < surface.Columns * 9 / 10; x++)
                        pixels[y * surface.Columns + x] = new Color32(220, 110, 55, 255);
                Replace(surface, pixels, owners);
                paint.RefreshCommittedMap();
                Color32[] neutral = rig.Capture(paint, "painter-P0-neutral.png");
                Assert.That(neutral.Where((pixel, i) => !pixel.Equals(paper[i])).Count(), Is.GreaterThan(1000));
                float coverage = surface.Coverage;
                int covered = surface.CoveredCount;
                var captures = new Color32[LabPaintSurface.MaxPainterSlot][];
                for (int slot = 1; slot <= LabPaintSurface.MaxPainterSlot; slot++)
                {
                    for (int i = 0; i < owners.Length; i++) owners[i] = pixels[i].a > 0 ? (byte)slot : (byte)0;
                    Replace(surface, pixels, owners);
                    paint.RefreshCommittedMap();
                    Assert.That(paint.PaintTexture.GetPixels32(), Is.EqualTo(pixels));
                    Assert.That(paint.PainterTexture.filterMode, Is.EqualTo(FilterMode.Point));
                    Assert.That(paint.PainterTexture.mipmapCount, Is.EqualTo(1));
                    Assert.That(paint.PainterTexture.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat));
                    Assert.That(paint.PainterTexture.wrapModeV, Is.EqualTo(TextureWrapMode.Clamp));
                    Color32[] upload = paint.PainterTexture.GetPixels32();
                    Assert.That(upload.Select(pixel => pixel.r), Is.EqualTo(owners), "P byte must survive linear upload exactly.");
                    MaterialPropertyBlock block = ReadBlock(paint.Shell.GetComponent<Renderer>());
                    Assert.That(block.GetTexture("_PainterMap"), Is.SameAs(paint.VisualPainterTexture));
                    Color32[] current = captures[slot - 1] = rig.Capture(paint, $"painter-P{slot}-{LabPlayerPaintPatterns.NameFor(slot)}.png");
                    int changed = 0, paperCount = 0, nearPaperDelta = 0;
                    for (int i = 0; i < current.Length; i++)
                    {
                        if (current[i].a != neutral[i].a) Assert.Fail("Painter pattern changed shell/background alpha at " + i);
                        if (neutral[i].Equals(paper[i]))
                        {
                            nearPaperDelta = Mathf.Max(nearPaperDelta, Mathf.Abs(current[i].r - neutral[i].r),
                                Mathf.Abs(current[i].g - neutral[i].g), Mathf.Abs(current[i].b - neutral[i].b));
                            paperCount++;
                        }
                        else
                        {
                            if (current[i].r > neutral[i].r || current[i].g > neutral[i].g || current[i].b > neutral[i].b)
                                Assert.Fail("The ownership motif must modulate pigment brightness only at " + i);
                            // The darkest ink retains at least 60% of each element-color
                            // channel. There are no black gaps or alpha cutouts in pigment.
                            if (current[i].r < neutral[i].r * .6f || current[i].g < neutral[i].g * .6f || current[i].b < neutral[i].b * .6f)
                                Assert.Fail("Ownership ink hid the element color or punched a dark hole at " + i);
                            if (!current[i].Equals(neutral[i])) changed++;
                        }
                    }
                    Assert.That(changed, Is.GreaterThan(100), $"P{slot} must show actual visible shader ink.");
                    Assert.That(paperCount, Is.GreaterThan(1000));
                    Assert.That(nearPaperDelta, Is.LessThanOrEqualTo(1),
                        "Sub-byte translucent edges cannot be classified as unpainted from RGB equality alone.");
                    Assert.That(surface.Coverage, Is.EqualTo(coverage));
                    Assert.That(surface.CoveredCount, Is.EqualTo(covered));
                    for (int other = 0; other < slot - 1; other++)
                        Assert.That(current.Where((pixel, i) => !pixel.Equals(captures[other][i])).Count(), Is.GreaterThan(100),
                            $"P{slot} and P{other + 1} must differ on the same saved gourd and color.");
                }
                Array.Clear(owners, 0, owners.Length);
                Replace(surface, pixels, owners);
                paint.RefreshCommittedMap();
                Assert.That(rig.Capture(paint, null), Is.EqualTo(neutral), "Neutral owner 0 must retain the original shader arithmetic.");
                yield return null;
                Assert.That(rig.Capture(paint, null), Is.EqualTo(neutral),
                    "Neutral owner 0 must remain exact after a frame without moving the shell or camera.");
            }
        }

        [UnityTest]
        public IEnumerator AllSixPainterPatternsStayOnTheShellWhenCameraAndModelMoveTogether()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("A real graphics device is required; shader pixels were not tested.");
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            Assert.That(paint.paintMaterial.shader.isSupported, Is.True);
            LabPaintSurface surface = paint.Surface;
            var pixels = new Color32[surface.CellCount];
            var owners = new byte[surface.CellCount];
            using (var rig = new ShellRenderRig(paint))
            {
                paint.ClearPaint();
                for (int y = surface.Rows / 5; y < surface.Rows * 4 / 5; y++)
                    for (int x = surface.Columns * 3 / 5; x < surface.Columns * 9 / 10; x++)
                        pixels[y * surface.Columns + x] = new Color32(220, 110, 55, 255);
                Replace(surface, pixels, owners);
                paint.RefreshCommittedMap();
                Color32[] neutral = rig.Capture(paint, null);
                float coverage = surface.Coverage;
                int covered = surface.CoveredCount;
                for (int slot = 1; slot <= LabPaintSurface.MaxPainterSlot; slot++)
                {
                    for (int i = 0; i < owners.Length; i++) owners[i] = pixels[i].a > 0 ? (byte)slot : (byte)0;
                    Replace(surface, pixels, owners);
                    paint.RefreshCommittedMap();
                    rig.SetOffset(Vector3.zero);
                    Color32[] current = rig.Capture(paint, null);
                    Assert.That(current.Where((pixel, i) => !pixel.Equals(neutral[i])).Count(), Is.GreaterThan(100),
                        $"P{slot} must show actual visible shader ink before movement.");
                    Vector3 move = new Vector3(2.37f, -1.13f, .71f);
                    rig.SetOffset(move);
                    yield return null;
                    Color32[] moved = rig.Capture(paint, null);
                    long error = 0;
                    for (int i = 0; i < current.Length; i++)
                        error += Math.Abs(current[i].r - moved[i].r) + Math.Abs(current[i].g - moved[i].g)
                            + Math.Abs(current[i].b - moved[i].b) + Math.Abs(current[i].a - moved[i].a);
                    Assert.That(error / (float)(current.Length * 4), Is.LessThan(.1f),
                        "Pattern must stay on the shell when model and camera move together. Allow sub-byte projection rounding at the isolated staging coordinates.");
                    Assert.That(paint.PaintTexture.GetPixels32(), Is.EqualTo(pixels));
                    Assert.That(paint.PainterTexture.GetPixels32().Select(pixel => pixel.r), Is.EqualTo(owners),
                        "Moving the render fixture must not change the committed painter bytes.");
                    Assert.That(surface.Coverage, Is.EqualTo(coverage));
                    Assert.That(surface.CoveredCount, Is.EqualTo(covered));
                    rig.SetOffset(Vector3.zero);
                }
            }
        }

        [UnityTest]
        public IEnumerator PatternToggleChangesOnlyRenderedInkWithoutReuploadingOrRevisingMaps()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("A real graphics device is required; shader pixels were not tested.");
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            LabConfig config = paint.network.config;
            float[] original = config.CaptureDeveloperValues();
            int field = Enumerable.Range(0, LabConfig.DeveloperFields.Count)
                .Single(i => LabConfig.DeveloperFields[i].Label == "Player Paint Patterns (0 off, 1 on)");
            LabPaintSurface surface = paint.Surface;
            var pixels = new Color32[surface.CellCount];
            var owners = new byte[surface.CellCount];
            for (int y = surface.Rows / 5; y < surface.Rows * 4 / 5; y++)
                for (int x = surface.Columns * 3 / 5; x < surface.Columns * 9 / 10; x++)
                {
                    int i = y * surface.Columns + x;
                    pixels[i] = new Color32(220, 110, 55, 255);
                    owners[i] = 2;
                }
            Replace(surface, pixels, owners);
            paint.RefreshCommittedMap();
            uint revision = surface.Revision;
            int count = surface.CoveredCount;
            Texture2D colorTexture = paint.PaintTexture, ownerTexture = paint.PainterTexture;
            try
            {
                using (var rig = new ShellRenderRig(paint))
                {
                    Color32[] on = null, off = null;
                    foreach (bool enabled in new[] { true, false, true, false })
                    {
                        float[] values = config.CaptureDeveloperValues(); values[field] = enabled ? 1f : 0f;
                        Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
                        // No growth, map replacement, new revision, or frame delay: a setting-only
                        // change must invalidate the presentation cache on this existing surface.
                        paint.RefreshCommittedMap();
                        Assert.That(ReadBlock(paint.Shell.GetComponent<Renderer>()).GetFloat("_PlayerPaintPatternsEnabled"),
                            Is.EqualTo(enabled ? 1f : 0f));
                        Color32[] current = rig.Capture(paint, enabled ? "pattern-toggle-ON.png" : "pattern-toggle-OFF.png");
                        if (enabled)
                        {
                            if (on == null) on = current;
                            else Assert.That(current, Is.EqualTo(on), "Restoring ON must restore the same visible ink.");
                        }
                        else
                        {
                            if (off == null) off = current;
                            else Assert.That(current, Is.EqualTo(off), "OFF must consistently render color without ownership ink.");
                            Assert.That(current.Where((pixel, i) => !pixel.Equals(on[i])).Count(), Is.GreaterThan(100));
                            Assert.That(current.Select(pixel => pixel.a), Is.EqualTo(on.Select(pixel => pixel.a)));
                        }
                        Assert.That(surface.Revision, Is.EqualTo(revision));
                        Assert.That(surface.CoveredCount, Is.EqualTo(count));
                        Assert.That(paint.PaintTexture, Is.SameAs(colorTexture));
                        Assert.That(paint.PainterTexture, Is.SameAs(ownerTexture));
                        Assert.That(colorTexture.GetPixels32(), Is.EqualTo(pixels));
                        Assert.That(ownerTexture.GetPixels32().Select(pixel => pixel.r), Is.EqualTo(owners));
                    }
                }
            }
            finally
            {
                Assert.That(config.TryApplyDeveloperValues(original, out string error), Is.True, error);
                paint.RefreshCommittedMap();
            }
        }

        [UnityTest]
        public IEnumerator CompletedViewerOwnsItsPainterMapAcrossLiveResetRotationAndDisposal()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            LabNetwork network = paint.network;
            LabConfig saved = network.config;
            var config = Object.Instantiate(saved);
            float[] values = config.CaptureDeveloperValues();
            int index = Enumerable.Range(0, LabConfig.DeveloperFields.Count)
                .Single(i => LabConfig.DeveloperFields[i].Label == "Completed Gourd Viewer (0 off, 1 on)");
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            values[index] = 1f;
            Assert.That(config.TryApplyDeveloperValues(values, out string configError), Is.True, configError);
            network.config = config;
            LabGourdResultPreview preview = null;
            try
            {
                network.port = 17946;
                Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
                LabPaintSurface surface = network.PaintSurface;
                var pixels = new Color32[surface.CellCount];
                var owners = new byte[surface.CellCount];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(55, 140, 180, 255);
                    owners[i] = (byte)(i % 5 + 1);
                }
                Replace(surface, pixels, owners);
                var snapshot = new LabSnapshot
                {
                    sessionId = "owner-viewer-fixture", round = 1, phase = LabPhase.Victory,
                    paintGoalPercent = 100, paintCoverage = surface.Coverage,
                    paintColumns = surface.Columns, paintRows = surface.Rows,
                    paintRevision = surface.Revision, paintCellCount = surface.CellCount,
                    paintCoveredCount = surface.CoveredCount, paintCount = surface.PaintCount
                };
                typeof(LabNetwork).GetField("latest", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(network, snapshot);
                paint.RefreshCommittedMap();
                // Per-submesh property blocks take precedence over renderer-level blocks.
                // The viewer must preserve the display switch while replacing both textures.
                Renderer liveRenderer = paint.Shell.GetComponent<Renderer>();
                foreach (bool patternsEnabled in new[] { false, true })
                {
                    float[] toggleValues = config.CaptureDeveloperValues();
                    toggleValues[137] = patternsEnabled ? 1f : 0f;
                    Assert.That(config.TryApplyDeveloperValues(toggleValues, out configError), Is.True, configError);
                    paint.RefreshCommittedMap();
                    liveRenderer.SetPropertyBlock(ReadBlock(liveRenderer), 0);
                    Assert.That(LabGourdResultPreview.TryCreate(network, out var togglePreview, out string toggleError, 256), Is.True, toggleError);
                    using (togglePreview)
                    {
                        Renderer copy = togglePreview.ModelRoot.GetComponentsInChildren<Renderer>(true)
                            .Single(item => item.sharedMaterial != null && item.sharedMaterial.shader == paint.paintMaterial.shader);
                        Assert.That(ReadBlock(copy).GetFloat("_PlayerPaintPatternsEnabled"), Is.EqualTo(patternsEnabled ? 1f : 0f));
                        var perMaterial = new MaterialPropertyBlock(); copy.GetPropertyBlock(perMaterial, 0);
                        Assert.That(perMaterial.GetFloat("_PlayerPaintPatternsEnabled"), Is.EqualTo(patternsEnabled ? 1f : 0f));
                        Assert.That(perMaterial.GetTexture("_PainterMap"), Is.SameAs(togglePreview.VisualPainterTexture));
                        Assert.That(togglePreview.PainterTexture.GetPixels32().Select(pixel => pixel.r), Is.EqualTo(owners));
                    }
                }
                liveRenderer.SetPropertyBlock(null, 0);
                Assert.That(LabGourdResultPreview.TryCreate(network, out preview, out string error, 256), Is.True, error);
                Texture2D owned = preview.PainterTexture;
                Texture visualOwned = preview.VisualPainterTexture;
                Assert.That(visualOwned, Is.Not.SameAs(paint.VisualPainterTexture));
                Assert.That(owned, Is.Not.SameAs(paint.PainterTexture));
                Assert.That(owned.GetPixels32().Select(pixel => pixel.r), Is.EqualTo(owners));
                Renderer renderer = preview.ModelRoot.GetComponentsInChildren<Renderer>(true)
                    .Single(item => item.sharedMaterial != null && item.sharedMaterial.shader == paint.paintMaterial.shader);
                Assert.That(ReadBlock(renderer).GetTexture("_PainterMap"), Is.SameAs(visualOwned));
                surface.Clear();
                paint.RefreshCommittedMap();
                Assert.That(paint.PainterTexture.GetPixels32().All(pixel => pixel.r == 0), Is.True);
                Assert.That(owned.GetPixels32().Select(pixel => pixel.r), Is.EqualTo(owners));
                preview.Rotate(new Vector2(.25f, -.3f));
                preview.Zoom(.2f);
                Assert.That(ReadBlock(renderer).GetTexture("_PainterMap"), Is.SameAs(visualOwned));
                Assert.That(owned.GetPixels32().Select(pixel => pixel.r), Is.EqualTo(owners));
                preview.Dispose();
                yield return null;
                Assert.That(owned == null, Is.True, "Closing the viewer must destroy its independent owner texture.");
                Assert.That(visualOwned == null, Is.True, "Closing also releases the independent high-resolution P map.");
                Assert.That(preview.PainterTexture, Is.Null);
            }
            finally
            {
                preview?.Dispose();
                if (network.IsConnected) network.Stop();
                network.config = saved;
                Object.Destroy(config);
            }
        }

        private static void Replace(LabPaintSurface surface, Color32[] pixels, byte[] owners) =>
            Assert.That(surface.TryReplacePixels(surface.Columns, surface.Rows, surface.Revision + 1, pixels, owners, 1), Is.True);

        private static MaterialPropertyBlock ReadBlock(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block;
        }

        private sealed class ShellRenderRig : IDisposable
        {
            private readonly GameObject root;
            private readonly Vector3 initialPosition;
            private readonly Renderer renderer;
            private readonly Camera camera;
            private readonly RenderTexture texture;
            private readonly Texture2D readback;

            public ShellRenderRig(LabGourdPaint paint)
            {
                root = new GameObject("Painter saved-shell GPU fixture");
                root.transform.position = new Vector3(1000f, 1000f, 1000f);
                initialPosition = root.transform.position;
                var shell = new GameObject("Saved White Gourd shell");
                shell.layer = 30; shell.transform.SetParent(root.transform, false);
                Mesh mesh = paint.Shell.GetComponent<MeshFilter>().sharedMesh;
                shell.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = shell.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = paint.paintMaterial;
                var cameraObject = new GameObject("Painter camera");
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.localPosition = mesh.bounds.center + Vector3.forward * 3f;
                cameraObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(mesh.bounds.extents.x, mesh.bounds.extents.y) * 1.1f;
                camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.03f, .04f, .05f, 0f);
                camera.nearClipPlane = .01f; camera.farClipPlane = 10f;
                camera.allowHDR = false; camera.allowMSAA = false;
                texture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Assert.That(texture.Create(), Is.True);
                camera.targetTexture = texture;
                readback = new Texture2D(512, 512, TextureFormat.RGBA32, false, true);
            }

            public void SetOffset(Vector3 offset) => root.transform.position = initialPosition + offset;

            public Color32[] Capture(LabGourdPaint paint, string evidenceName)
            {
                RenderTexture previous = RenderTexture.active;
                try
                {
                    renderer.SetPropertyBlock(ReadBlock(paint.Shell.GetComponent<Renderer>()));
                    camera.Render();
                    RenderTexture.active = texture;
                    readback.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); readback.Apply(false, false);
                    string directory = Environment.GetEnvironmentVariable("C6_PAINTER_EVIDENCE_DIR");
                    if (string.IsNullOrEmpty(directory)) directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/PlayerPaintPatterns"));
                    if (!string.IsNullOrEmpty(directory) && !string.IsNullOrEmpty(evidenceName))
                    {
                        Directory.CreateDirectory(directory);
                        File.WriteAllBytes(Path.Combine(directory, evidenceName), readback.EncodeToPNG());
                    }
                    return readback.GetPixels32();
                }
                finally { RenderTexture.active = previous; }
            }

            public void Dispose()
            {
                camera.targetTexture = null; texture.Release();
                Object.Destroy(readback); Object.Destroy(texture); Object.Destroy(root);
            }
        }
    }
}
