using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// First-hit paint work moved into preparation (#482). These check that the prewarm draws nothing visible and does
    /// the brush/distance work up front; Metal pipeline creation itself is only observable on a device.
    /// </summary>
    public sealed class LabFirstHitPaintPrewarmTests
    {
        [SetUp]
        public void ResetSharedFields() => LabWatercolorBrushDistance.ReleaseShared();

        [TearDown]
        public void ReleaseSharedFields() => LabWatercolorBrushDistance.ReleaseShared();

        [UnityTest]
        public IEnumerator TheSharedDistanceFieldMatchesAFreshBuildAndOutlivesItsRuntime()
        {
            RequireGraphics();
            Texture2D brush = Resources.Load<Texture2D>("GourdPaint/Patterns/" + LabPaintPattern.Splatter);
            Assert.That(LabWatercolorBrushDistance.IsShared(LabPaintPattern.Splatter), Is.False);
            Texture2D shared = LabWatercolorBrushDistance.GetOrCreate(LabPaintPattern.Splatter, brush);
            Texture2D fresh = LabWatercolorBrushDistance.Create(brush);
            try
            {
                Assert.That(shared.GetPixels32().SequenceEqual(fresh.GetPixels32()), Is.True, "Same field as the old per-runtime build.");
                Assert.That(LabWatercolorBrushDistance.GetOrCreate(LabPaintPattern.Splatter, brush), Is.SameAs(shared));
            }
            finally { Object.Destroy(fresh); }
            using (var fixture = new Fixture(hit: true))
            {
                Assert.That(fixture.UpdateWatercolor(0d, true), Is.True, fixture.Watercolor.Error);
            }
            yield return null;
            Assert.That(LabWatercolorBrushDistance.IsShared(LabPaintPattern.Splatter), Is.True, "A disposed runtime leaves it for the next round.");
            Assert.That(LabWatercolorBrushDistance.GetOrCreate(LabPaintPattern.Splatter, brush), Is.SameAs(shared));
            Assert.Throws<ArgumentOutOfRangeException>(() => LabWatercolorBrushDistance.GetOrCreate(LabPaintPattern.Legacy, brush));
        }

        [UnityTest]
        public IEnumerator AfterPrewarmTheFirstStampLoadsNoBrushAndBuildsNoDistanceField()
        {
            RequireGraphics();
            using (var fixture = new Fixture(hit: false))
            {
                Assert.That(fixture.UpdateWatercolor(0d, true), Is.True, fixture.Watercolor.Error);
                Assert.That(fixture.Baseline.Prewarm(LabPaintPattern.Splatter), Is.True, fixture.Baseline.PrewarmError);
                Assert.That(fixture.Watercolor.Prewarm(LabPaintPattern.Splatter), Is.True, fixture.Watercolor.PrewarmError);
                Assert.That(LabWatercolorBrushDistance.IsShared(LabPaintPattern.Splatter), Is.True);
                int loads = LabFirstUse.CountOf(LabFirstUseKind.BrushLoad), fields = LabFirstUse.CountOf(LabFirstUseKind.BrushDistance);

                fixture.Hit();
                Assert.That(fixture.UpdateWatercolor(1d, true), Is.True, fixture.Watercolor.Error);
                Assert.That(fixture.Watercolor.CommittedStampCount, Is.GreaterThan(0), "The first stamp was drawn.");
                Assert.That(LabFirstUse.CountOf(LabFirstUseKind.BrushLoad), Is.EqualTo(loads), "Neither runtime loads its brush on the hit.");
                Assert.That(LabFirstUse.CountOf(LabFirstUseKind.BrushDistance), Is.EqualTo(fields), "The field was built before the hit.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PrewarmLeavesTheRuntimesAndTheirImagesExactlyAsWithout()
        {
            RequireGraphics();
            using (var warm = new Fixture(hit: true))
            using (var cold = new Fixture(hit: true))
            {
                Assert.That(warm.UpdateWatercolor(0d, true), Is.True, warm.Watercolor.Error);
                Assert.That(cold.UpdateWatercolor(0d, true), Is.True, cold.Watercolor.Error);
                string before = Describe(warm);
                Assert.That(warm.Baseline.Prewarm(LabPaintPattern.Splatter), Is.True, warm.Baseline.PrewarmError);
                Assert.That(warm.Watercolor.Prewarm(LabPaintPattern.Splatter), Is.True, warm.Watercolor.PrewarmError);
                Assert.That(Describe(warm), Is.EqualTo(before), "Ready, errors and stamp counts are untouched.");
                Assert.That(Read(warm.Watercolor.PaintTexture).SequenceEqual(Read(cold.Watercolor.PaintTexture)), Is.True);
                Assert.That(Read(warm.Baseline.PaintTexture).SequenceEqual(Read(cold.Baseline.PaintTexture)), Is.True);
                warm.Hit(); cold.Hit();
                Assert.That(warm.UpdateWatercolor(1d, true), Is.True, warm.Watercolor.Error);
                Assert.That(cold.UpdateWatercolor(1d, true), Is.True, cold.Watercolor.Error);
                Assert.That(Read(warm.Watercolor.PaintTexture).SequenceEqual(Read(cold.Watercolor.PaintTexture)), Is.True,
                    "A prewarmed runtime paints the next hit identically.");
                Assert.That(warm.Watercolor.Prewarm(LabPaintPattern.Legacy), Is.False, "Legacy has no brush to prewarm.");
                Assert.That(warm.Watercolor.PrewarmError, Is.Null);
            }
            yield return null;
        }

        [Test]
        public void TheTemporaryTargetsUseTheFormatsOfTheRealCaches()
        {
            RenderTextureDescriptor high = LabHighResolutionPaintRuntime.PrewarmDescriptor();
            Assert.That(high.colorFormat, Is.EqualTo(RenderTextureFormat.ARGB32));
            Assert.That(high.sRGB, Is.False); Assert.That(high.msaaSamples, Is.EqualTo(1));
            Assert.That(high.depthBufferBits, Is.Zero); Assert.That(high.dimension, Is.EqualTo(TextureDimension.Tex2D));
            RenderTextureDescriptor water = LabWatercolorPaintRuntime.PrewarmDescriptor();
            Assert.That(water.colorFormat, Is.EqualTo(RenderTextureFormat.ARGB32));
            Assert.That(water.sRGB, Is.False); Assert.That(water.msaaSamples, Is.EqualTo(1));
            Assert.That(water.depthBufferBits, Is.Zero); Assert.That(water.dimension, Is.EqualTo(TextureDimension.Tex2DArray));
            Assert.That(water.volumeDepth, Is.EqualTo(5));
        }

        [Test]
        public void TheHostResolvesTheJudgmentMaskBeforeTheFirstHit()
        {
            var surface = new LabPaintSurface(new LabPaintGrowthSettings { Resolution = 0, Pattern = LabPaintPattern.Splatter }, 482u);
            FieldInfo mask = typeof(LabPaintSurface).GetField("patternMask", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(mask.GetValue(surface), Is.Null, "A new round resolves nothing until asked.");
            surface.PreloadPatternMask();
            object loaded = mask.GetValue(surface);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(surface.RegisterHit(Vector3.forward, LabPaintPalette.ColorFor(LabOrbElement.Water), 1, 1), Is.True);
            Assert.That(mask.GetValue(surface), Is.SameAs(loaded), "The hit judges with the same mask.");
            var legacy = new LabPaintSurface(new LabPaintGrowthSettings { Resolution = 0, Pattern = LabPaintPattern.Legacy }, 482u);
            legacy.PreloadPatternMask();
            Assert.That(mask.GetValue(legacy), Is.Null, "Legacy paint has no mask.");
        }

        private sealed class Fixture : IDisposable
        {
            public readonly LabPaintSurface Surface;
            public readonly LabHighResolutionPaintRuntime Baseline = new LabHighResolutionPaintRuntime();
            public readonly LabWatercolorPaintRuntime Watercolor = new LabWatercolorPaintRuntime();
            private readonly LabWatercolorPaintSettings settings = new LabWatercolorPaintSettings();
            private readonly Texture2D colors, owners;

            public Fixture(bool hit)
            {
                Surface = new LabPaintSurface(new LabPaintGrowthSettings {
                    Resolution = 0, Pattern = LabPaintPattern.Splatter, InitialPercent = 5f,
                    RepeatPercent = 3f, GrowthPercentPerSecond = 6f, DryingEnabled = false
                }, 482u);
                colors = Map(Surface.Columns, Surface.Rows);
                owners = Map(Surface.Columns, Surface.Rows);
                if (hit) Hit(); else SyncBaseline();
            }

            public void Hit()
            {
                Assert.That(Surface.RegisterHit(Vector3.forward, LabPaintPalette.ColorFor(LabOrbElement.Water), 1, 1), Is.True);
                SyncBaseline();
            }

            private void SyncBaseline()
            {
                var pixels = new Color32[Surface.CellCount]; var slots = new byte[Surface.CellCount];
                Surface.CopyPixels(pixels); colors.SetPixels32(pixels); colors.Apply(false, false);
                Surface.CopyPainterSlots(slots);
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(slots[i], 0, 0, 255);
                owners.SetPixels32(pixels); owners.Apply(false, false);
                Assert.That(Baseline.Update(Surface, colors, owners, 1024, true), Is.True, Baseline.Error);
            }

            public bool UpdateWatercolor(double now, bool complete) =>
                Watercolor.Update(Surface, Baseline.PaintTexture, Baseline.PainterTexture, settings, now, complete);

            public void Dispose() { Watercolor.Dispose(); Baseline.Dispose(); Object.Destroy(colors); Object.Destroy(owners); }
        }

        private static string Describe(Fixture fixture) =>
            fixture.Baseline.Ready + ":" + fixture.Baseline.Error + ":" + fixture.Baseline.RetainedStampCount + ":"
            + fixture.Watercolor.Ready + ":" + fixture.Watercolor.Error + ":" + fixture.Watercolor.CommittedStampCount + ":"
            + fixture.Watercolor.RetainedStampCount + ":" + fixture.Watercolor.ActiveStampCount + ":" + fixture.Watercolor.IsAnimating;

        private static Texture2D Map(int width, int height) => new Texture2D(width, height, TextureFormat.RGBA32, false, true) {
            filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp
        };

        private static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !SystemInfo.supports2DArrayTextures)
                Assert.Ignore("A real graphics device with texture-array support is required.");
        }

        private static Color32[] Read(RenderTexture texture)
        {
            Assert.That(texture != null && texture.IsCreated(), Is.True);
            RenderTexture previous = RenderTexture.active;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
            try
            {
                RenderTexture.active = texture;
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); copy.Apply(false, false);
                return copy.GetPixels32();
            }
            finally { RenderTexture.active = previous; Object.Destroy(copy); }
        }
    }
}
