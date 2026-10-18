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
    /// <summary>Render-only map fixtures, not projectile or network approval tests.</summary>
    public sealed class LabPaintGrainRenderTests
    {
        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Grain render cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator SavedGourdShaderChangesOnlyPigmentAndZeroStrengthMatchesOffExactly()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("A real graphics device is required; shader pixels were not tested.");
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            Assert.That(paint.paintMaterial.shader.isSupported, Is.True);
            LabConfig saved = paint.network.config;
            var fixture = Object.Instantiate(saved);
            paint.network.config = fixture;
            using (var rig = new ShellRenderRig(paint))
            {
                try
                {
                    Tune(fixture, false, 1f, .3f, .18f);
                    paint.ClearPaint();
                    paint.RefreshCommittedMap();
                    Color32[] paper = rig.Capture(paint, null);
                    Assert.That(paint.Paint(new Vector3(-.3f, .2f, 1f), new Color(.8f, .12f, .07f)), Is.True);
                    Assert.That(paint.Paint(new Vector3(.35f, -.1f, 1f), new Color(.08f, .38f, .72f)), Is.True);
                    Color32[] cpuMap = paint.PaintTexture.GetPixels32();
                    uint revision = paint.Surface.Revision;
                    float coverage = paint.Coverage;
                    int count = paint.PaintCount;
                    Color32[] off = rig.Capture(paint, "grain-partial-OFF.png");
                    Assert.That(off.Where((pixel, i) => !pixel.Equals(paper[i])).Count(), Is.GreaterThan(100),
                        "The saved White Gourd mesh and material must render a visible partial pigment map.");

                    Tune(fixture, true, 1f, .3f, .18f);
                    paint.RefreshCommittedMap();
                    Color32[] on = rig.Capture(paint, "grain-partial-ON-default.png");
                    int changed = 0, unchangedPaper = 0;
                    for (int i = 0; i < off.Length; i++)
                    {
                        if (on[i].a != off[i].a) Assert.Fail("Grain changed opaque shell alpha at pixel " + i);
                        if (off[i].Equals(paper[i]))
                        {
                            if (!on[i].Equals(off[i])) Assert.Fail("Grain changed unpainted paper/background at pixel " + i);
                            unchangedPaper++;
                        }
                        else if (!on[i].Equals(off[i])) changed++;
                    }
                    Assert.That(changed, Is.GreaterThan(100), "The actual shader must show pigment variation.");
                    Assert.That(unchangedPaper, Is.GreaterThan(1000));
                    yield return null;
                    Assert.That(rig.Capture(paint, null), Is.EqualTo(on), "Grain is object-local and does not animate over time.");
                    Tune(fixture, true, 3f, .3f, .18f);
                    paint.RefreshCommittedMap();
                    Assert.That(rig.Capture(paint, "grain-partial-ON-size3.png"), Is.Not.EqualTo(on));
                    Tune(fixture, true, 1f, 0f, 0f);
                    paint.RefreshCommittedMap();
                    Assert.That(rig.Capture(paint, "grain-partial-ON-zero-strength.png"), Is.EqualTo(off),
                        "Both strengths zero must use the original pigment arithmetic exactly.");
                    Tune(fixture, false, 4f, 1f, 1f);
                    paint.RefreshCommittedMap();
                    Assert.That(rig.Capture(paint, null), Is.EqualTo(off), "OFF ignores all grain tuning.");
                    Assert.That(paint.PaintTexture.GetPixels32(), Is.EqualTo(cpuMap));
                    Assert.That(paint.Surface.Revision, Is.EqualTo(revision));
                    Assert.That(paint.Coverage, Is.EqualTo(coverage));
                    Assert.That(paint.PaintCount, Is.EqualTo(count));
                }
                finally
                {
                    paint.network.config = saved;
                    paint.RefreshCommittedMap();
                    Object.Destroy(fixture);
                }
            }
        }

        [UnityTest]
        public IEnumerator UnchangedMapRebindsGrainWithoutEditingSharedMaterialAndViewerCopiesOverrides()
        {
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            LabNetwork network = paint.network;
            LabConfig saved = network.config;
            var fixture = Object.Instantiate(saved);
            float[] originalMaterial = MaterialGrain(paint.paintMaterial);
            network.config = fixture;
            LabGourdResultPreview preview = null;
            try
            {
                Tune(fixture, true, 2.25f, .7f, .4f);
                Set(fixture, "Completed Gourd Viewer (0 off, 1 on)", 1f);
                paint.RefreshCommittedMap();
                uint revision = paint.Surface.Revision;
                Color32[] before = paint.PaintTexture.GetPixels32();
                MaterialPropertyBlock block = Block(paint.Shell.GetComponent<Renderer>());
                block.SetFloat("_UnrelatedOverride", .37f);
                paint.Shell.GetComponent<Renderer>().SetPropertyBlock(block);
                Tune(fixture, true, 3f, .8f, .6f);
                Assert.That(paint.ApplyMode(), Is.True);
                block = Block(paint.Shell.GetComponent<Renderer>());
                AssertGrain(block, 1f, 3f, .8f, .6f);
                Assert.That(block.GetFloat("_UnrelatedOverride"), Is.EqualTo(.37f));
                Assert.That(block.GetTexture("_PaintMap"), Is.SameAs(paint.PaintTexture));
                Assert.That(paint.Surface.Revision, Is.EqualTo(revision));
                Assert.That(paint.PaintTexture.GetPixels32(), Is.EqualTo(before));

                // A fixture creates a matching complete result; this does not claim a real hit.
                network.port = 17896;
                Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
                LabPaintSurface map = network.PaintSurface;
                map.Reset(new LabPaintGrowthSettings { Pattern = LabPaintPattern.Legacy, Resolution = 0, InitialPercent = 100f }, 196u);
                Assert.That(map.RegisterHit(Vector3.forward, new Color(.25f, .45f, .7f), 1), Is.True);
                var result = new LabSnapshot
                {
                    sessionId = "grain-viewer-fixture", round = 1, phase = LabPhase.Victory,
                    paintGoalPercent = 100, paintCoverage = map.Coverage, paintColumns = map.Columns,
                    paintRows = map.Rows, paintRevision = map.Revision, paintCellCount = map.CellCount,
                    paintCoveredCount = map.CoveredCount, paintCount = map.PaintCount
                };
                typeof(LabNetwork).GetField("latest", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(network, result);
                paint.RefreshCommittedMap();
                Assert.That(LabGourdResultPreview.TryCreate(network, out preview, out string error, 256), Is.True, error);
                Renderer copy = preview.ModelRoot.GetComponentsInChildren<Renderer>(true)
                    .Single(renderer => renderer.sharedMaterial != null && renderer.sharedMaterial.shader == paint.paintMaterial.shader);
                MaterialPropertyBlock copyBlock = Block(copy);
                AssertGrain(copyBlock, 1f, 3f, .8f, .6f);
                Assert.That(copyBlock.GetFloat("_UnrelatedOverride"), Is.EqualTo(.37f));
                Assert.That(copyBlock.GetTexture("_PaintMap"), Is.SameAs(preview.PaintTexture));
                Assert.That(preview.PaintTexture, Is.Not.SameAs(paint.PaintTexture));
                Assert.That(preview.PaintTexture.GetPixels32(), Is.EqualTo(paint.PaintTexture.GetPixels32()));
                Assert.That(copy.sharedMaterial, Is.Not.SameAs(paint.paintMaterial));
                Tune(fixture, false, 1f, .1f, .1f);
                paint.RefreshCommittedMap();
                AssertGrain(Block(paint.Shell.GetComponent<Renderer>()), 0f, 1f, .1f, .1f);
                AssertGrain(Block(copy), 1f, 3f, .8f, .6f);
                Assert.That(MaterialGrain(paint.paintMaterial), Is.EqualTo(originalMaterial));
                Assert.That(saved.PaintGrainEnabled, Is.False, "The saved config remains opt-in OFF.");
            }
            finally
            {
                preview?.Dispose();
                if (network.IsConnected) network.Stop();
                network.config = saved;
                paint.RefreshCommittedMap();
                Object.Destroy(fixture);
            }
        }

        private static void Tune(LabConfig config, bool enabled, float size, float strength, float mottle)
        {
            float[] values = config.CaptureDeveloperValues();
            values[Field("Paint Grain (0 off, 1 on)")] = enabled ? 1f : 0f;
            values[Field("Paint Grain Size")] = size;
            values[Field("Paint Grain Strength")] = strength;
            values[Field("Paint Mottle Strength")] = mottle;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
        }

        private static void Set(LabConfig config, string label, float value)
        {
            float[] values = config.CaptureDeveloperValues(); values[Field(label)] = value;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
        }

        private static int Field(string label) => Enumerable.Range(0, LabConfig.DeveloperFields.Count)
            .Single(index => LabConfig.DeveloperFields[index].Label == label);

        private static MaterialPropertyBlock Block(Renderer renderer)
        {
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); return block;
        }

        private static float[] MaterialGrain(Material material) => new[]
        {
            material.GetFloat("_PaintGrainEnabled"), material.GetFloat("_PaintGrainSize"),
            material.GetFloat("_PaintGrainStrength"), material.GetFloat("_PaintMottleStrength")
        };

        private static void AssertGrain(MaterialPropertyBlock block, float enabled, float size, float strength, float mottle)
        {
            Assert.That(block.GetFloat("_PaintGrainEnabled"), Is.EqualTo(enabled));
            Assert.That(block.GetFloat("_PaintGrainSize"), Is.EqualTo(size));
            Assert.That(block.GetFloat("_PaintGrainStrength"), Is.EqualTo(strength));
            Assert.That(block.GetFloat("_PaintMottleStrength"), Is.EqualTo(mottle));
        }

        /// <summary>Saved curved shell mesh/material, isolated camera; CPU partial map is a preview fixture.</summary>
        private sealed class ShellRenderRig : IDisposable
        {
            private readonly GameObject root;
            private readonly Renderer renderer;
            private readonly Camera camera;
            private readonly RenderTexture texture;
            private readonly Texture2D readback;

            public ShellRenderRig(LabGourdPaint paint)
            {
                root = new GameObject("Grain saved-shell render fixture");
                root.transform.position = new Vector3(1000f, 1000f, 1000f);
                var shell = new GameObject("Saved White Gourd shell");
                shell.layer = 30; shell.transform.SetParent(root.transform, false);
                Mesh mesh = paint.Shell.GetComponent<MeshFilter>().sharedMesh;
                shell.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = shell.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = paint.paintMaterial;
                var cameraObject = new GameObject("Grain camera");
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

            public Color32[] Capture(LabGourdPaint paint, string evidenceName)
            {
                renderer.SetPropertyBlock(Block(paint.Shell.GetComponent<Renderer>()));
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = texture;
                    readback.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); readback.Apply(false, false);
                    string directory = Environment.GetEnvironmentVariable("C6_GRAIN_EVIDENCE_DIR");
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
