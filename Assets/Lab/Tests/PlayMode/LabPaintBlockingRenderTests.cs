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
    public sealed class LabPaintBlockingRenderTests
    {
        [UnityTest]
        public IEnumerator RetainedInitialBrushCannotShowPaintInsideAnAuthoritativelyBlockedPatch()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("A graphics device is required.");
            var settings = new LabPaintGrowthSettings
                { Resolution = 0, Pattern = LabPaintPattern.Splatter, InitialPercent = 12.5f, DryingEnabled = false };
            var surface = new LabPaintSurface(settings, 342);
            surface.SetPaintBlocker((owner, direction) => owner == 1 && direction.x > .06f);
            Assert.That(surface.RegisterHit(Vector3.forward, Color.blue, 1, 1), Is.True);
            Assert.That(surface.CopyVisualStamps(), Is.Not.Empty, "The original full brush remains as a visual hint.");
            Color32[] authority = Pixels(surface); uint revision = surface.Revision;
            var runtime = new LabHighResolutionPaintRuntime();
            Texture2D colors = Upload(surface, false), painters = Upload(surface, true);
            try
            {
                Assert.That(runtime.Update(surface, colors, painters, 1024, true), Is.True, runtime.Error);
                Color32[] result = Read(runtime.PaintTexture), owners = Read(runtime.PainterTexture);
                int checkedPixels = 0;
                for (int y = 0; y < runtime.PaintTexture.height; y++)
                    for (int x = 0; x < runtime.PaintTexture.width; x++)
                    {
                        Vector3 direction = Direction((x + .5f) / runtime.PaintTexture.width,
                            (y + .5f) / runtime.PaintTexture.height);
                        // Stay well inside the blocked area; existing brush antialiasing at
                        // the cell edge is presentation and is not a new authoritative cell.
                        if (direction.x < .35f || direction.z < .4f) continue;
                        int i = y * runtime.PaintTexture.width + x;
                        Assert.That(result[i].a, Is.Zero);
                        Assert.That(owners[i].r, Is.Zero);
                        checkedPixels++;
                    }
                Assert.That(checkedPixels, Is.GreaterThan(1000));
                Assert.That(result.Any(pixel => pixel.a != 0), Is.True, "The open side still has visible paint.");
                Assert.That(Pixels(surface), Is.EqualTo(authority));
                Assert.That(surface.Revision, Is.EqualTo(revision), "Rendering cannot modify Host coverage.");
            }
            finally { runtime.Dispose(); Object.Destroy(colors); Object.Destroy(painters); }
            yield return null;
        }

        private static Color32[] Pixels(LabPaintSurface surface)
        { var result = new Color32[surface.CellCount]; surface.CopyPixels(result); return result; }
        private static Texture2D Upload(LabPaintSurface surface, bool owners)
        {
            var texture = new Texture2D(surface.Columns, surface.Rows, TextureFormat.RGBA32, false, true)
                { filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
            var colors = Pixels(surface);
            if (owners)
            {
                var p = new byte[surface.CellCount]; surface.CopyPainterSlots(p);
                for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(p[i], 0, 0, 255);
            }
            texture.SetPixels32(colors); texture.Apply(false, false); return texture;
        }
        private static Color32[] Read(RenderTexture texture)
        {
            RenderTexture previous = RenderTexture.active;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
            try
            {
                RenderTexture.active = texture;
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); copy.Apply();
                return copy.GetPixels32();
            }
            finally { RenderTexture.active = previous; Object.Destroy(copy); }
        }
        private static Vector3 Direction(float u, float v)
        {
            float y = 1f - 2f * v, ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float angle = (u - .5f) * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle) * ring, y, Mathf.Sin(angle) * ring);
        }
    }
}
