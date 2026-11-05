using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Saved-gourd surface checks. A one-byte RGB bound accommodates the existing Metal shell's rounding;
    /// alpha, input maps and unpainted regions are never inferred from a visual threshold.</summary>
    [Category("GPU"), Category("Legacy")]
    public sealed class LabWatercolorSurfaceGrainTests
    {
        [UnityTest]
        public IEnumerator SurfaceGrainChangesPigmentDetailWithoutChangingInputAlphaOrUnpaintedPaper()
        {
            RequireGraphics();
            using (var rig = new SurfaceRig())
            {
                rig.SetMap(new Color(.17f, .32f, .44f, .7f));
                Color32[] original = rig.Map.GetPixels32();
                Color32[] off = rig.Capture(false, 0f);
                AssertNear(rig.Capture(true, 0f), off, 1, "Zero grain strength preserves the original pigment arithmetic.");
                Color32[] grain = rig.Capture(true, .5f);
                int changed = 0, detail = 0;
                for (int y = 1; y < 511; y++)
                    for (int x = 1; x < 511; x++)
                    {
                        int i = y * 512 + x;
                        Assert.That(grain[i].a, Is.EqualTo(off[i].a), "Pigment texture cannot change rendered shell alpha.");
                        if (off[i].a != 255 || off[i + 1].a != 255 || off[i + 512].a != 255) continue;
                        int delta = MaxDelta(grain[i], off[i]);
                        if (delta > 2) changed++;
                        // Relative to the untextured shell, neighboring pigment differences
                        // prove this is local texture rather than a uniform darker tint.
                        int here = grain[i].b - off[i].b;
                        int next = grain[i + 1].b - off[i + 1].b;
                        if (Mathf.Abs(here - next) > 1) detail++;
                    }
                Assert.That(changed, Is.GreaterThan(500), "The saved gourd must show actual pigment texture.");
                Assert.That(detail, Is.GreaterThan(100), "Pigment must contain fine spatial variation, not just a color gain.");
                Assert.That(rig.Map.GetPixels32(), Is.EqualTo(original), "Surface shading does not rewrite the paid map or its alpha.");
                rig.SetMap(Color.clear);
                Color32[] paper = rig.Capture(false, 0f);
                AssertNear(rig.Capture(true, .6f), paper, 1, "Unpainted paper is unaffected by pigment-only grain.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PigmentTextureIsStaticAndFollowsTheObjectAcrossScreenTranslationAndRigRotation()
        {
            RequireGraphics();
            using (var rig = new SurfaceRig())
            {
                // Opaque neutral paint isolates the texture multiplier from the underlying
                // paper blend. Lighting still changes naturally when the object rotates.
                rig.SetMap(new Color(.35f, .35f, .35f, 1f));
                Color32[] off = rig.Capture(false, 0f);
                Color32[] on = rig.Capture(true, .5f);
                yield return new WaitForSecondsRealtime(.12f);
                AssertNear(rig.Capture(true, .5f), on, 1, "Holding the surface must not animate its pigment grain.");
                // A constant projection translation moves the same surface to different
                // screen pixels without changing magnification, world pose or derivatives.
                // Camera.pixelRect is not used: cameras rendering to a targetTexture can
                // ignore it. Thirteen pixels also avoids common power-of-two noise periods.
                const int shift = 13;
                rig.ProjectionOffsetPixels = shift;
                Color32[] shiftedOff = rig.Capture(false, 0f);
                Color32[] shiftedOn = rig.Capture(true, .5f);
                int mismatchedShiftedSilhouette = 0, mismatchedUnshiftedSilhouette = 0, screenSamples = 0;
                double screenRatioDifference = 0d;
                for (int y = 16; y < 496; y++)
                    for (int x = 32; x < 480 - shift; x++)
                    {
                        int i = y * 512 + x, j = i + shift;
                        if (off[i].a != shiftedOff[j].a) mismatchedShiftedSilhouette++;
                        if (off[i].a != shiftedOff[i].a) mismatchedUnshiftedSilhouette++;
                        if (off[i].a != 255 || shiftedOff[j].a != 255 || off[i].b < 32 || shiftedOff[j].b < 32) continue;
                        Assert.That(shiftedOn[j].a, Is.EqualTo(on[i].a), "Repositioning pigment cannot change its shell alpha.");
                        screenRatioDifference += Math.Abs(on[i].b / (double)off[i].b - shiftedOn[j].b / (double)shiftedOff[j].b);
                        screenSamples++;
                    }
                Assert.That(mismatchedUnshiftedSilhouette, Is.GreaterThan(500), "The fixture must actually move the object across screen pixels.");
                Assert.That(mismatchedShiftedSilhouette, Is.LessThan(mismatchedUnshiftedSilhouette / 20 + 8),
                    "A matching screen offset must recover the same projected surface, not a different zoom or view.");
                Assert.That(screenSamples, Is.GreaterThan(1000));
                Assert.That(screenRatioDifference / screenSamples, Is.LessThan(.02d),
                    "Screen-anchored noise would remain on the old pixels instead of following this thirteen-pixel surface translation.");
                rig.ProjectionOffsetPixels = 0;
                rig.Rotation = Quaternion.Euler(0f, 37f, 0f);
                Color32[] rotatedOff = rig.Capture(false, 0f);
                Color32[] rotatedOn = rig.Capture(true, .5f);
                double ratioDifference = 0d; int samples = 0;
                for (int i = 0; i < off.Length; i++)
                {
                    if (off[i].a != 255 || rotatedOff[i].a != 255 || off[i].b < 32 || rotatedOff[i].b < 32) continue;
                    ratioDifference += Math.Abs(on[i].b / (double)off[i].b - rotatedOn[i].b / (double)rotatedOff[i].b);
                    samples++;
                }
                Assert.That(samples, Is.GreaterThan(1000));
                Assert.That(ratioDifference / samples, Is.LessThan(.02d),
                    "The pigment multiplier follows local surface coordinates; world-anchored noise would slide when the rig rotates.");
            }
        }

        static void AssertNear(Color32[] actual, Color32[] expected, int rgbLimit, string message)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            int maximum = 0;
            for (int i = 0; i < actual.Length; i++)
            {
                Assert.That(actual[i].a, Is.EqualTo(expected[i].a), message + " (alpha)");
                maximum = Math.Max(maximum, MaxDelta(actual[i], expected[i]));
            }
            Assert.That(maximum, Is.LessThanOrEqualTo(rgbLimit), message);
        }
        static int MaxDelta(Color32 a, Color32 b) => Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Abs(a.b - b.b)));
        static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires an actual graphics device.");
            var material = Resources.Load<Material>("GourdPaint/WhiteGourd");
            Assert.That(material, Is.Not.Null); Assert.That(material.shader.isSupported, Is.True);
        }

        sealed class SurfaceRig : IDisposable
        {
            readonly GameObject root;
            readonly MeshRenderer renderer;
            readonly Material material;
            readonly Camera camera;
            readonly RenderTexture target;
            readonly Texture2D readback;
            readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            readonly Matrix4x4 baseProjection;
            public Texture2D Map { get; }
            public float ProjectionOffsetPixels
            {
                set
                {
                    Matrix4x4 translation = Matrix4x4.identity;
                    translation.m03 = 2f * value / target.width;
                    camera.projectionMatrix = translation * baseProjection;
                }
            }
            public Quaternion Rotation { set => root.transform.rotation = value; }
            public SurfaceRig()
            {
                var sourceMaterial = Resources.Load<Material>("GourdPaint/WhiteGourd");
                var prefab = Resources.Load<GameObject>("GourdPaint/WhiteGourd");
                Assert.That(prefab, Is.Not.Null);
                var filter = prefab.GetComponentsInChildren<MeshFilter>(true)
                    .Where(f => f.GetComponent<Renderer>() != null && f.GetComponent<Renderer>().sharedMaterials.Any(m => m != null && m.shader == sourceMaterial.shader))
                    .OrderByDescending(f => f.sharedMesh.bounds.size.sqrMagnitude).First();
                Mesh mesh = filter.sharedMesh;
                root = new GameObject("Watercolor V2 isolated saved-gourd surface fixture");
                root.transform.position = new Vector3(12f, 0f, 0f);
                var shell = new GameObject("Saved gourd mesh"); shell.layer = 30; shell.transform.SetParent(root.transform, false);
                shell.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = shell.AddComponent<MeshRenderer>();
                material = new Material(sourceMaterial); renderer.sharedMaterial = material;
                // Fixed relief avoids the known far-from-origin derivative rounding in
                // older shell fixtures while preserving the actual authored paper color.
                material.SetFloat("_PaperBump", 0f); material.SetFloat("_PaperSmoothness", 0f);
                var cameraObject = new GameObject("Surface comparison camera"); cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.localPosition = mesh.bounds.center + Vector3.forward * 3f;
                cameraObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(mesh.bounds.extents.x, mesh.bounds.extents.y) * 1.1f;
                camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.03f, .04f, .05f, 0f); camera.nearClipPlane = .01f; camera.farClipPlane = 10f;
                camera.allowHDR = false; camera.allowMSAA = false;
                target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Assert.That(target.Create(), Is.True); camera.targetTexture = target;
                camera.aspect = 1f; baseProjection = camera.projectionMatrix;
                readback = new Texture2D(512, 512, TextureFormat.RGBA32, false, true);
                Map = new Texture2D(4, 2, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Bilinear };
            }
            public void SetMap(Color value)
            {
                Map.SetPixels(Enumerable.Repeat(value, 8).ToArray()); Map.Apply(false, false);
            }
            public Color32[] Capture(bool mode, float strength)
            {
                block.SetTexture("_PaintMap", Map); block.SetFloat("_PaintMapReady", 1f);
                block.SetFloat("_PlayerPaintPatternsEnabled", 0f); block.SetFloat("_PaintGrainEnabled", 0f);
                block.SetFloat("_WatercolorV2Enabled", mode ? 1f : 0f);
                block.SetFloat("_WatercolorGranulationStrength", strength); block.SetFloat("_WatercolorGranulationScale", 120f);
                renderer.SetPropertyBlock(block); camera.Render();
                RenderTexture old = RenderTexture.active;
                try { RenderTexture.active = target; readback.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); readback.Apply(false, false); return readback.GetPixels32(); }
                finally { RenderTexture.active = old; }
            }
            public void Dispose()
            {
                camera.targetTexture = null; target.Release();
                Object.Destroy(Map); Object.Destroy(readback); Object.Destroy(target); Object.Destroy(material); Object.Destroy(root);
            }
        }
    }
}
