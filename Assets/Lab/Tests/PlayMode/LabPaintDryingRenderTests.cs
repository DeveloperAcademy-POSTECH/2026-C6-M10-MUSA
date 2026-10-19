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
    /// GPU comparison of production CPU maps on the saved WhiteGourd mesh and paint shader.
    /// The clock/map are controlled fixtures; this is not projectile or remote-device evidence.
    /// </summary>
    public sealed class LabPaintDryingRenderTests
    {
        [UnityTearDown]
        public IEnumerator Unload()
        {
            Scene scene = SceneManager.GetSceneByName("Lab");
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Paint drying render cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator SavedGourdShowsProgressiveErosionNearbyProtectionAndPaperRestorationWhileOffIsUnchanged()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("A real graphics device is required; no GPU paint-drying evidence was produced.");
            yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
            yield return null;
            var paint = Object.FindFirstObjectByType<LabGourdPaint>();
            Assert.That(paint, Is.Not.Null);
            Assert.That(paint.IsReady, Is.True, paint.InitializationError);
            Assert.That(paint.paintMaterial.shader.isSupported, Is.True);
            Assert.That(new LabPaintGrowthSettings().DryingEnabled, Is.False);
            var on = new LabPaintSurface(Settings(true), 225u);
            var off = new LabPaintSurface(Settings(false), 225u);
            Color pigment = LabPaintPalette.ColorFor(LabOrbElement.Fire);
            using (var rig = new SavedShellRig(paint))
            {
                Color32[] paper = rig.Capture(on, "00-drying-paper.png");
                Assert.That(on.RegisterHit(Vector3.forward, pigment, 101ul, 3), Is.True);
                Assert.That(off.RegisterHit(Vector3.forward, pigment, 101ul, 3), Is.True);
                Assert.That(Pixels(on), Is.EqualTo(Pixels(off)));
                Assert.That(Slots(on), Is.EqualTo(Slots(off)));
                Color32[] initialMap = Pixels(on);
                int initialCovered = on.CoveredCount;
                Color32[] initial = rig.Capture(on, "01-drying-initial.png");
                int initialVisible = Changed(initial, paper);
                Assert.That(initialVisible, Is.GreaterThan(1000), "The actual shader must visibly render the initial pigment.");

                on.Advance(2d); // One second of grace, then one second at 2 surface-%/second.
                Assert.That(on.CoveredCount, Is.InRange(1, initialCovered - 1));
                AssertErasedCellsHaveNoColorOrOwner(initialMap, on);
                Color32[] partialMap = Pixels(on);
                Color32[] partial = rig.Capture(on, "02-drying-partly-eroded.png");
                Assert.That(Changed(partial, paper), Is.InRange(1, initialVisible - 1),
                    "Fewer painted texels must be visible after real boundary deletion.");
                Assert.That(Changed(partial, initial), Is.GreaterThan(100));
                AssertSameShellAlpha(initial, partial);

                // Repeat supplies are zero in this fixture: only the approved-nearby-hit
                // timer changes. Radius 90 includes this front-facing splash, not its antipode.
                Assert.That(on.RegisterHit(Vector3.forward, pigment, 102ul, 5), Is.True);
                on.Advance(.75d);
                Assert.That(Pixels(on), Is.EqualTo(partialMap),
                    "Refreshing a nearby patch pauses erosion without repainting or changing its P number.");
                Color32[] protectedFrame = rig.Capture(on, "03-drying-nearby-hit-protected.png");
                Assert.That(protectedFrame, Is.EqualTo(partial));

                on.Advance(10d);
                Assert.That(on.CoveredCount, Is.Zero);
                Assert.That(Pixels(on), Is.All.EqualTo(new Color32(0, 0, 0, 0)));
                Assert.That(Slots(on), Is.All.EqualTo((byte)0));
                Color32[] disappeared = rig.Capture(on, "04-drying-fully-erased.png");
                Assert.That(disappeared, Is.EqualTo(paper),
                    "Deleting color and ownership must restore the exact unpainted saved shell.");

                off.Advance(100d);
                Assert.That(Pixels(off), Is.EqualTo(initialMap));
                Assert.That(off.CoveredCount, Is.EqualTo(initialCovered));
                Assert.That(rig.Capture(off, "05-drying-OFF-unchanged.png"), Is.EqualTo(initial),
                    "OFF must retain both the actual CPU paint and its GPU presentation.");
            }
        }

        private static LabPaintGrowthSettings Settings(bool drying) => new LabPaintGrowthSettings
        {
            Resolution = 2, Pattern = LabPaintPattern.Splatter, InitialPercent = 6f,
            RepeatPercent = 0f, GrowthPercentPerSecond = 0f,
            DryingEnabled = drying, DryingDelaySeconds = 1f,
            DryingPercentPerSecond = 2f, NearbyHitRadiusDegrees = 90f
        };

        private static Color32[] Pixels(LabPaintSurface surface)
        { var pixels = new Color32[surface.CellCount]; surface.CopyPixels(pixels); return pixels; }
        private static byte[] Slots(LabPaintSurface surface)
        { var slots = new byte[surface.CellCount]; surface.CopyPainterSlots(slots); return slots; }
        private static int Changed(Color32[] left, Color32[] right)
            => left.Where((pixel, index) => !pixel.Equals(right[index])).Count();
        private static void AssertSameShellAlpha(Color32[] left, Color32[] right)
        {
            for (int i = 0; i < left.Length; i++) Assert.That(right[i].a, Is.EqualTo(left[i].a));
        }
        private static void AssertErasedCellsHaveNoColorOrOwner(Color32[] before, LabPaintSurface surface)
        {
            Color32[] after = Pixels(surface); byte[] slots = Slots(surface);
            Assert.That(after.Count(pixel => pixel.a != 0), Is.EqualTo(surface.CoveredCount));
            int erased = 0;
            for (int i = 0; i < after.Length; i++)
            {
                if (before[i].a != 0 && after[i].a == 0) erased++;
                if (after[i].a == 0)
                {
                    Assert.That(after[i], Is.EqualTo(new Color32(0, 0, 0, 0)));
                    Assert.That(slots[i], Is.Zero);
                }
                else Assert.That(slots[i], Is.EqualTo(3));
            }
            Assert.That(erased, Is.GreaterThan(0));
        }

        private sealed class SavedShellRig : IDisposable
        {
            private const int Size = 640;
            private readonly GameObject root;
            private readonly Renderer renderer;
            private readonly Camera camera;
            private readonly RenderTexture texture;
            private readonly Texture2D readback, colorMap, painterMap;

            public SavedShellRig(LabGourdPaint paint)
            {
                root = new GameObject("Saved gourd drying GPU fixture");
                root.transform.position = new Vector3(1000f, 1000f, 1000f);
                var shell = new GameObject("Saved WhiteGourd shell");
                shell.layer = 30; shell.transform.SetParent(root.transform, false);
                Mesh mesh = paint.Shell.GetComponent<MeshFilter>().sharedMesh;
                shell.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = shell.AddComponent<MeshRenderer>(); renderer.sharedMaterial = paint.paintMaterial;
                var cameraObject = new GameObject("Drying evidence camera");
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.localPosition = mesh.bounds.center + Vector3.forward * 3f;
                cameraObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(mesh.bounds.extents.x, mesh.bounds.extents.y) * 1.1f;
                camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.04f, .05f, .07f, 0f);
                camera.nearClipPlane = .01f; camera.farClipPlane = 10f; camera.allowHDR = false; camera.allowMSAA = false;
                texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Assert.That(texture.Create(), Is.True); camera.targetTexture = texture;
                readback = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
                colorMap = NewMap(256, 128); painterMap = NewMap(256, 128);
            }

            public Color32[] Capture(LabPaintSurface surface, string name)
            {
                Assert.That(surface.Columns, Is.EqualTo(colorMap.width));
                Assert.That(surface.Rows, Is.EqualTo(colorMap.height));
                Color32[] pixels = Pixels(surface); byte[] slots = Slots(surface);
                colorMap.SetPixels32(pixels); colorMap.Apply(false, false);
                var ownerPixels = new Color32[slots.Length];
                for (int i = 0; i < slots.Length; i++) ownerPixels[i] = new Color32(slots[i], 0, 0, 255);
                painterMap.SetPixels32(ownerPixels); painterMap.Apply(false, false);
                Assert.That(colorMap.GetPixels32(), Is.EqualTo(pixels));
                Assert.That(painterMap.GetPixels32(), Is.EqualTo(ownerPixels));
                var block = new MaterialPropertyBlock();
                block.SetTexture("_PaintMap", colorMap); block.SetTexture("_PainterMap", painterMap);
                block.SetFloat("_PaintMapReady", 1f); block.SetFloat("_PlayerPaintPatternsEnabled", 1f);
                block.SetFloat("_PaintGrainEnabled", 0f); renderer.SetPropertyBlock(block);
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = texture;
                    readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); readback.Apply(false, false);
                    string directory = Environment.GetEnvironmentVariable("C6_DRYING_EVIDENCE_DIR");
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                        File.WriteAllBytes(Path.Combine(directory, name), readback.EncodeToPNG());
                    }
                    return readback.GetPixels32();
                }
                finally { RenderTexture.active = previous; }
            }

            private static Texture2D NewMap(int width, int height) => new Texture2D(width, height,
                TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point,
                    wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
            public void Dispose()
            {
                camera.targetTexture = null; texture.Release();
                Object.Destroy(root); Object.Destroy(texture); Object.Destroy(readback);
                Object.Destroy(colorMap); Object.Destroy(painterMap);
            }
        }
    }
}
