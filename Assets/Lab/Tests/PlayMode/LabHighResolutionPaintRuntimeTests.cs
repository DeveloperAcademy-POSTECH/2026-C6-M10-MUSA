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
    /// <summary>Actual GPU output, authoritative isolation, replay budget and resource lifetime.</summary>
    public sealed class LabHighResolutionPaintRuntimeTests
    {
        [UnityTest]
        public IEnumerator CommittedGeometryMakesContinuousPaintAndErasureAndOverwriteUseCurrentAuthority()
        {
            RequireGraphics();
            var surface = new LabPaintSurface(new LabPaintGrowthSettings
                { Resolution = 0, Pattern = LabPaintPattern.Splatter, InitialPercent = 5f, DryingEnabled = false }, 299u);
            Assert.That(surface.RegisterHit(Vector3.forward, new Color(.05f, .2f, .7f), 10, 1), Is.True);
            Assert.That(surface.CopyVisualStamps(), Is.Not.Empty);
            string authorityHash = Hash(surface);
            Texture2D colors = Upload(surface, false), owners = Upload(surface, true);
            var runtime = new LabHighResolutionPaintRuntime();
            try
            {
                Assert.That(runtime.Update(surface, colors, owners, 1024, true), Is.True, runtime.Error);
                Assert.That(runtime.PaintTexture.width, Is.EqualTo(1024));
                Assert.That(runtime.PaintTexture.height, Is.EqualTo(512));
                Assert.That(runtime.PainterTexture.filterMode, Is.EqualTo(FilterMode.Point));
                Color32[] withGeometry = Read(runtime.PaintTexture);
                Assert.That(withGeometry.Any(pixel => pixel.a > 0), Is.True);
                Assert.That(withGeometry.Any(pixel => pixel.a == 0), Is.True);
                Assert.That(withGeometry.Count(pixel => pixel.a > 0 && pixel.a < 255), Is.GreaterThan(100),
                    "The runtime output contains a continuous edge and translucent watercolor pigment.");
                Assert.That(Read(runtime.PainterTexture).All(pixel => pixel.r == 0 || pixel.r == 1), Is.True,
                    "Smoothing must not invent a P number.");
                Assert.That(Hash(surface), Is.EqualTo(authorityHash), "GPU appearance cannot change gameplay bytes.");

                LabPaintVisualStamp[] stamps = surface.CopyVisualStamps();
                Assert.That(surface.ReplaceVisualStamps(Array.Empty<LabPaintVisualStamp>()), Is.True);
                Assert.That(runtime.Update(surface, colors, owners, 1024, true), Is.True, runtime.Error);
                Color32[] reconstructed = Read(runtime.PaintTexture);
                Assert.That(withGeometry.Where((pixel, index) => !pixel.Equals(reconstructed[index])).Any(), Is.True,
                    "The Host's actual continuous brush must affect the silhouette, beyond enlarging the cell map.");
                Assert.That(Hash(surface), Is.EqualTo(authorityHash));

                var empty = new Color32[surface.CellCount]; var emptyP = new byte[surface.CellCount];
                Assert.That(surface.TryReplacePixels(surface.Columns, surface.Rows, surface.Revision + 1,
                    empty, emptyP, surface.PaintCount), Is.True);
                Assert.That(surface.ReplaceVisualStamps(stamps), Is.True, "Retained old shapes exercise stale-paint removal.");
                Reupload(surface, colors, owners);
                Assert.That(runtime.Update(surface, colors, owners, 1024, true), Is.True, runtime.Error);
                Assert.That(Read(runtime.PaintTexture).All(pixel => pixel.a == 0 && pixel.r == 0 && pixel.g == 0 && pixel.b == 0), Is.True,
                    "An erased authoritative map cannot retain a high-resolution ghost.");
                Assert.That(Read(runtime.PainterTexture).All(pixel => pixel.r == 0), Is.True);

                var red = Enumerable.Repeat(new Color32(210, 28, 12, 255), surface.CellCount).ToArray();
                var redP = Enumerable.Repeat((byte)3, surface.CellCount).ToArray();
                Assert.That(surface.TryReplacePixels(surface.Columns, surface.Rows, surface.Revision + 1,
                    red, redP, surface.PaintCount + 1), Is.True);
                Assert.That(surface.ReplaceVisualStamps(stamps), Is.True, "Old blue geometry must not preserve blue ownership.");
                Reupload(surface, colors, owners);
                authorityHash = Hash(surface);
                Assert.That(runtime.Update(surface, colors, owners, 1024, true), Is.True, runtime.Error);
                Color32[] completed = Read(runtime.PaintTexture);
                Assert.That(completed.All(pixel => pixel.a > 0 && pixel.r > pixel.g * 3 && pixel.r > pixel.b * 3), Is.True,
                    "Completed/overwritten surfaces keep pigment everywhere and only the current team hue.");
                Assert.That(completed.Any(pixel => pixel.a < 255), Is.True,
                    "Watercolor opacity is separate from the fully covered Host surface.");
                Assert.That(Read(runtime.PainterTexture).All(pixel => pixel.r == 3), Is.True);
                Assert.That(Hash(surface), Is.EqualTo(authorityHash));
            }
            finally { runtime.Dispose(); Object.Destroy(colors); Object.Destroy(owners); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CheckpointRebuildIsBoundedAndQualityAndLostTexturesRecreateOwnedResources()
        {
            RequireGraphics();
            var surface = new LabPaintSurface(new LabPaintGrowthSettings { Resolution = 0 }, 300u);
            var pixels = Enumerable.Repeat(new Color32(90, 140, 60, 255), surface.CellCount).ToArray();
            var p = Enumerable.Repeat((byte)2, surface.CellCount).ToArray();
            Assert.That(surface.TryReplacePixels(surface.Columns, surface.Rows, 1, pixels, p, 1), Is.True);
            var records = new LabPaintVisualStamp[20];
            for (int i = 0; i < records.Length; i++) records[i] = new LabPaintVisualStamp
            {
                sequence = (uint)(i + 1), center = Vector3.forward, right = Vector3.right,
                radius = .15f + i * .001f, color = pixels[0], painterSlot = 2,
                pattern = LabPaintPattern.Splatter, seed = (uint)i
            };
            Assert.That(surface.ReplaceVisualStamps(records), Is.True);
            Texture2D colors = Upload(surface, false), owners = Upload(surface, true);
            var runtime = new LabHighResolutionPaintRuntime();
            RenderTexture first = null, large = null;
            try
            {
                Assert.That(runtime.Update(surface, colors, owners, 1024), Is.False);
                Assert.That(runtime.HasPendingWork, Is.True);
                Assert.That(runtime.Ready, Is.False, "A partly rebuilt checkpoint is not published as a completed image.");
                Assert.That(runtime.Update(surface, colors, owners, 1024), Is.False,
                    "Repeated refresh in one frame cannot bypass the stamp pass budget.");
                Assert.That(runtime.Update(surface, colors, owners, 513), Is.False);
                Assert.That(runtime.Error, Is.Not.Null);
                Assert.That(runtime.HasPendingWork, Is.False,
                    "A failed presentation must not drive an automatic per-frame allocation retry.");
                Assert.That(runtime.Update(surface, colors, owners, 1024), Is.False,
                    "An explicit valid retry clears the error but must still finish the remaining budgeted work.");
                Assert.That(runtime.HasPendingWork, Is.True);
                for (int frame = 0; frame < 5 && !runtime.Ready; frame++)
                { yield return null; runtime.Update(surface, colors, owners, 1024); }
                Assert.That(runtime.Ready, Is.True, runtime.Error);
                Assert.That(runtime.RetainedStampCount, Is.EqualTo(20));
                first = runtime.PaintTexture;
                first.Release();
                Assert.That(runtime.Ready, Is.False);
                Assert.That(runtime.Update(surface, colors, owners, 1024, true), Is.True, runtime.Error);
                Assert.That(runtime.PaintTexture, Is.Not.SameAs(first));
                Assert.That(runtime.Update(surface, colors, owners, 2048, true), Is.True, runtime.Error);
                large = runtime.PaintTexture;
                Assert.That(large.width, Is.EqualTo(2048)); Assert.That(large.height, Is.EqualTo(1024));
                runtime.Dispose();
                Assert.That(runtime.Ready, Is.False);
                Assert.That(large == null || !large.IsCreated(), Is.True);
                Assert.That(runtime.Update(surface, colors, owners), Is.False, "A disposed renderer cannot recreate GPU resources.");
            }
            finally { runtime.Dispose(); Object.Destroy(colors); Object.Destroy(owners); }
            yield return null;
        }

        private static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("A real graphics device is required.");
            Shader shader = Resources.Load<Shader>(LabHighResolutionPaintRuntime.ShaderResource);
            Assert.That(shader, Is.Not.Null); Assert.That(shader.isSupported, Is.True);
        }
        private static string Hash(LabPaintSurface surface)
        {
            var colors = new Color32[surface.CellCount]; var painters = new byte[surface.CellCount];
            surface.CopyPixels(colors); surface.CopyPainterSlots(painters);
            return LabPaintPublisher.Hash(colors, painters);
        }
        private static Texture2D Upload(LabPaintSurface surface, bool owners)
        {
            var texture = new Texture2D(surface.Columns, surface.Rows, TextureFormat.RGBA32, false, true)
            { filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
            var colors = new Color32[surface.CellCount];
            if (owners)
            {
                var painters = new byte[surface.CellCount]; surface.CopyPainterSlots(painters);
                for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(painters[i], 0, 0, 255);
            }
            else surface.CopyPixels(colors);
            texture.SetPixels32(colors); texture.Apply(false, false); return texture;
        }
        private static void Reupload(LabPaintSurface surface, Texture2D colors, Texture2D owners)
        {
            var pixels = new Color32[surface.CellCount]; var painters = new byte[surface.CellCount];
            surface.CopyPixels(pixels); colors.SetPixels32(pixels); colors.Apply(false, false);
            surface.CopyPainterSlots(painters);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(painters[i], 0, 0, 255);
            owners.SetPixels32(pixels); owners.Apply(false, false);
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
    }
}
