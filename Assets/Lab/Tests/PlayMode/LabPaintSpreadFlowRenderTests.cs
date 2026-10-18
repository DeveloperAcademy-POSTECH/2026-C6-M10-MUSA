using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// Explicit map-growth/render fixtures using the saved WhiteGourd mesh and production
    /// shader. No projectile, collision, network approval, or actual game victory is claimed.
    /// </summary>
    public sealed class LabPaintSpreadFlowRenderTests
    {
        private const uint Seed = 198u;
        private static readonly Vector3 HitDirection = new Vector3(0f, .42f, 1f).normalized;
        private static readonly Color[] Pigments =
        {
            new Color(.88f, .09f, .045f), new Color(.98f, .66f, .045f),
            new Color(.055f, .40f, .84f), new Color(.075f, .51f, .27f),
            new Color(.46f, .12f, .72f)
        };

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Spread-flow render cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator SavedShellComparesLegacyAndSpreadFlowWithIdenticalSeedAndAreaFunding()
        {
            RequireGraphics();
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabGourdPaint paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint, Is.Not.Null);
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            Assert.That(paint.paintMaterial.shader.isSupported, Is.True);
            Assert.That(paint.network.IsConnected, Is.False, "This is an explicitly isolated render fixture.");
            LabConfig saved = paint.network.config;
            var fixture = Object.Instantiate(saved);
            paint.network.config = fixture;
            try
            {
                using (var rig = new ShellRenderRig(paint))
                {
                    var counts = new int[2, 4];
                    var maps = new Color32[2][];
                    var frames = new Color32[2, 4][];
                    for (int style = 0; style <= 1; style++)
                    {
                        TuneGrain(fixture, false);
                        LabPaintSurface surface = paint.Surface;
                        surface.Reset(Settings(style, 3f, 6f, 10f), Seed);
                        surface.SetFlowDirection(Vector3.down);
                        paint.RefreshCommittedMap();
                        Color32[] paper = rig.Capture(paint, null);

                        Assert.That(surface.RegisterHit(HitDirection, Pigments[0], 1), Is.True);
                        RecordStage(paint, rig, style, 0, "initial", counts, frames);
                        Assert.That(surface.RegisterHit(HitDirection, Pigments[1], 2), Is.True);
                        Assert.That(surface.RegisterHit(HitDirection, Pigments[2], 3), Is.True);
                        Assert.That(surface.PaintCount, Is.EqualTo(3));
                        Assert.That(surface.Advance(.25d), Is.LessThan(0d));
                        RecordStage(paint, rig, style, 1, "early", counts, frames);
                        Assert.That(surface.Advance(.5d), Is.LessThan(0d));
                        RecordStage(paint, rig, style, 2, "middle", counts, frames);
                        Assert.That(surface.Advance(2d), Is.LessThan(0d));
                        RecordStage(paint, rig, style, 3, "settled", counts, frames);
                        Assert.That(surface.PendingCoverage, Is.LessThan(1d / surface.CellCount));
                        Assert.That(surface.Coverage, Is.LessThan(.16f));
                        maps[style] = Copy(surface);
                        Assert.That(maps[style].Where(pixel => pixel.a != 0).Distinct().Count(), Is.EqualTo(3),
                            "Repeated hit supplies must retain three opaque pigment colors without blending.");
                        Assert.That(frames[style, 3].Where((pixel, i) => !pixel.Equals(paper[i])).Count(),
                            Is.GreaterThan(200), "The production shader must visibly display the grown map.");

                        if (style == 1)
                        {
                            uint revision = surface.Revision;
                            TuneGrain(fixture, true);
                            paint.RefreshCommittedMap();
                            Color32[] grain = rig.Capture(paint, "spread-flow-settled-grain-ON.png");
                            int changedPigment = 0, unchangedPaper = 0;
                            for (int i = 0; i < grain.Length; i++)
                            {
                                if (grain[i].a != frames[style, 3][i].a)
                                    Assert.Fail("Grain altered saved shell alpha at pixel " + i + ".");
                                if (frames[style, 3][i].Equals(paper[i]))
                                {
                                    if (!grain[i].Equals(paper[i])) Assert.Fail("Grain altered unpainted paper/background.");
                                    unchangedPaper++;
                                }
                                else if (!grain[i].Equals(frames[style, 3][i])) changedPigment++;
                            }
                            Assert.That(changedPigment, Is.GreaterThan(100));
                            Assert.That(unchangedPaper, Is.GreaterThan(1000));
                            Assert.That(surface.Revision, Is.EqualTo(revision));
                            Assert.That(Copy(surface), Is.EqualTo(maps[style]), "Internal grain must not alter growth membership.");
                        }
                    }
                    for (int stage = 0; stage < 4; stage++)
                        Assert.That(counts[1, stage], Is.EqualTo(counts[0, stage]),
                            "The shape mode must not increase area funding at stage " + stage);
                    Assert.That(maps[1], Is.Not.EqualTo(maps[0]), "The selected growth mode must change actual cells.");
                    Assert.That(frames[1, 3], Is.Not.EqualTo(frames[0, 3]), "The saved shader must expose that shape change.");
                    rig.SaveComparison(frames, "spread-flow-comparison.png");
                }
            }
            finally
            {
                paint.network.config = saved;
                paint.ClearPaint();
                Object.Destroy(fixture);
            }
        }

        [UnityTest]
        public IEnumerator FivePigmentGrowthReachesEveryCellAndSavedMeshHasNoPaperHolesInFiveViews()
        {
            RequireGraphics();
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            LabGourdPaint paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            LabConfig saved = paint.network.config;
            var fixture = Object.Instantiate(saved);
            paint.network.config = fixture;
            try
            {
                TuneGrain(fixture, true);
                LabPaintSurface surface = paint.Surface;
                surface.Reset(Settings(1, 2f, 25f, 100f), Seed);
                surface.SetFlowDirection(Vector3.down);
                Assert.That(surface.RegisterHit(HitDirection, Pigments[0], 1), Is.True);
                for (int i = 1; i < Pigments.Length; i++)
                    Assert.That(surface.RegisterHit(HitDirection, Pigments[i], (ulong)(i + 1)), Is.True);
                Assert.That(surface.Advance(2d), Is.GreaterThanOrEqualTo(0d));
                Assert.That(surface.IsComplete, Is.True);
                Assert.That(surface.Coverage, Is.EqualTo(1f));
                Assert.That(surface.CoveredCount, Is.EqualTo(surface.CellCount));
                Assert.That(surface.PendingCoverage, Is.EqualTo(0d));
                Color32[] full = Copy(surface);
                Assert.That(full.All(pixel => pixel.a == 255), Is.True);
                Assert.That(full.Distinct().Count(), Is.EqualTo(5), "All five FIFO pigments must remain represented.");
                uint revision = surface.Revision;
                int paintCount = surface.PaintCount;
                int columns = surface.Columns, rows = surface.Rows;
                using (var rig = new ShellRenderRig(paint))
                {
                    var views = new[]
                    {
                        new Vector3(0f, 0f, 0f), new Vector3(0f, 120f, 0f),
                        new Vector3(0f, 240f, 0f), new Vector3(65f, 0f, 0f),
                        new Vector3(-65f, 0f, 0f)
                    };
                    for (int view = 0; view < views.Length; view++)
                    {
                        rig.Rotation = Quaternion.Euler(views[view]);
                        surface.Clear();
                        paint.RefreshCommittedMap();
                        Color32[] paper = rig.Capture(paint, null);
                        // Install the genuine completed-growth bytes only for camera comparison;
                        // this is a renderer replica fixture, not a simulated gameplay victory.
                        Assert.That(surface.TryReplacePixels(columns, rows, revision, full, paintCount), Is.True);
                        paint.RefreshCommittedMap();
                        Color32[] colored = rig.Capture(paint, "spread-flow-complete-view-" + view + ".png");
                        int shellPixels = 0;
                        for (int i = 0; i < colored.Length; i++)
                        {
                            if (paper[i].a < 250) continue;
                            shellPixels++;
                            if (colored[i].a != paper[i].a)
                                Assert.Fail("A complete map altered shell alpha in view " + view + ".");
                            if (colored[i].Equals(paper[i]))
                                Assert.Fail("A fully covered map rendered an unpainted paper hole in view " + view + ".");
                        }
                        Assert.That(shellPixels, Is.GreaterThan(10000), "The saved mesh must fill a meaningful camera area.");
                        Assert.That(surface.Coverage, Is.EqualTo(1f));
                        Assert.That(surface.CoveredCount, Is.EqualTo(surface.CellCount));
                        Assert.That(paint.PaintTexture.GetPixels32(), Is.EqualTo(full));
                    }
                }
            }
            finally
            {
                paint.network.config = saved;
                paint.ClearPaint();
                Object.Destroy(fixture);
            }
        }

        private static LabPaintGrowthSettings Settings(int style, float initial, float repeat, float speed) =>
            new LabPaintGrowthSettings
            {
                Resolution = 2, InitialPercent = initial, RepeatPercent = repeat,
                GrowthPercentPerSecond = speed, FillChancePercent = 60f, VerticalWeight = 1.35f,
                Style = style, Raggedness = .55f, FlowRatio = .35f, DripWidthDegrees = 3f,
                DripLengthDegrees = 60f, GravityWeight = 3f
            };

        private static void TuneGrain(LabConfig config, bool enabled)
        {
            float[] values = config.CaptureDeveloperValues();
            values[129] = enabled ? 1f : 0f;
            values[130] = 1f; values[131] = .3f; values[132] = .18f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
        }

        private static void RecordStage(LabGourdPaint paint, ShellRenderRig rig, int style, int stage,
            string name, int[,] counts, Color32[,][] frames)
        {
            paint.RefreshCommittedMap();
            counts[style, stage] = paint.Surface.CoveredCount;
            frames[style, stage] = rig.Capture(paint, (style == 0 ? "legacy" : "spread-flow") + "-" + name + ".png");
        }

        private static Color32[] Copy(LabPaintSurface surface)
        {
            var pixels = new Color32[surface.CellCount]; surface.CopyPixels(pixels); return pixels;
        }

        private static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("A real graphics device is required; spread-flow shader pixels were not tested.");
        }

        private static MaterialPropertyBlock Block(Renderer renderer)
        {
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); return block;
        }

        /// <summary>The actual saved curved mesh/material, with only a camera and isolated render layer added.</summary>
        private sealed class ShellRenderRig : IDisposable
        {
            private const int Size = 512;
            private readonly GameObject root, shell;
            private readonly Renderer renderer;
            private readonly Camera camera;
            private readonly RenderTexture texture;
            private readonly Texture2D readback;
            public Quaternion Rotation { set => shell.transform.localRotation = value; }

            public ShellRenderRig(LabGourdPaint paint)
            {
                root = new GameObject("Spread-flow saved-shell render fixture");
                root.transform.position = new Vector3(1000f, 1000f, 1000f);
                shell = new GameObject("Saved White Gourd shell");
                shell.layer = 30; shell.transform.SetParent(root.transform, false);
                Mesh mesh = paint.Shell.GetComponent<MeshFilter>().sharedMesh;
                shell.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = shell.AddComponent<MeshRenderer>(); renderer.sharedMaterial = paint.paintMaterial;
                var cameraObject = new GameObject("Spread-flow comparison camera");
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
                texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Assert.That(texture.Create(), Is.True);
                camera.targetTexture = texture;
                readback = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
            }

            public Color32[] Capture(LabGourdPaint paint, string evidenceName)
            {
                renderer.SetPropertyBlock(Block(paint.Shell.GetComponent<Renderer>()));
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = texture;
                    readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); readback.Apply(false, false);
                    Save(readback, evidenceName);
                    return readback.GetPixels32();
                }
                finally { RenderTexture.active = previous; }
            }

            public void SaveComparison(Color32[,][] frames, string evidenceName)
            {
                var pixels = new Color32[Size * 2 * Size * 4];
                for (int style = 0; style < 2; style++)
                    for (int stage = 0; stage < 4; stage++)
                        for (int y = 0; y < Size; y++)
                            Array.Copy(frames[style, stage], y * Size, pixels,
                                ((3 - stage) * Size + y) * Size * 2 + style * Size, Size);
                var sheet = new Texture2D(Size * 2, Size * 4, TextureFormat.RGBA32, false, true);
                try { sheet.SetPixels32(pixels); sheet.Apply(false, false); Save(sheet, evidenceName); }
                finally { Object.Destroy(sheet); }
            }

            private static void Save(Texture2D image, string evidenceName)
            {
                string directory = Environment.GetEnvironmentVariable("C6_SPREAD_FLOW_EVIDENCE_DIR");
                if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(evidenceName)) return;
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, evidenceName), image.EncodeToPNG());
            }

            public void Dispose()
            {
                camera.targetTexture = null; texture.Release();
                Object.Destroy(readback); Object.Destroy(texture); Object.Destroy(root);
            }
        }
    }
}
