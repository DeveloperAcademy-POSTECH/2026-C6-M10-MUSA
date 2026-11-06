#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>CPU contour fixtures: changed geometry, unchanged funded area and instance isolation.</summary>
    [Category("Legacy")]
    public sealed class LabWatercolorContourOverrideTests
    {
        private static LabPaintGrowthSettings Settings() => new LabPaintGrowthSettings
        {
            Pattern = LabPaintPattern.Splatter, Resolution = 2,
            InitialPercent = 2f, RepeatPercent = 1.5f,
            GrowthPercentPerSecond = 100f, DryingEnabled = false
        };

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void WatercolorContourChangesFirstAndThreeRehitGeometryWithoutChangingBudgets(int position)
        {
            Vector3 direction = position == 0 ? Vector3.forward : position == 1 ? Vector3.up : Vector3.left;
            Texture2D mask = Ellipse();
            try
            {
                var baseline = new LabPaintSurface(Settings(), 123u);
                var study = new LabPaintSurface(Settings(), 123u);
                var duplicate = new LabPaintSurface(Settings(), 123u);
                study.SetPatternForAppearanceStudy(mask);
                duplicate.SetPatternForAppearanceStudy(mask);
                Hit(baseline, direction); Hit(study, direction); Hit(duplicate, direction);
                AssertSameBudget(baseline, study);
                Assert.That(study.CoveredCount, Is.EqualTo((int)decimal.Floor(.02m * study.CellCount)));
                Assert.That(Hash(study), Is.Not.EqualTo(Hash(baseline)), "The CPU footprint must actually change.");
                Assert.That(Hash(duplicate), Is.EqualTo(Hash(study)));
                for (int i = 0; i < 3; i++)
                {
                    Hit(baseline, direction); Hit(study, direction); Hit(duplicate, direction);
                    baseline.Advance(1d); study.Advance(1d);
                    duplicate.Advance(.5d); duplicate.Advance(.5d);
                    AssertSameBudget(baseline, study);
                    AssertSameBudget(study, duplicate);
                    Assert.That(Hash(duplicate), Is.EqualTo(Hash(study)), "Splitting elapsed time must preserve the result.");
                    Assert.That(Hash(study), Is.Not.EqualTo(Hash(baseline)), "Rehit growth must retain the selected contour family.");
                }
                Assert.That(study.PaintCount, Is.EqualTo(4));
                Assert.That(study.CoveredCount, Is.EqualTo((int)decimal.Floor(.065m * study.CellCount)));
            }
            finally { Object.DestroyImmediate(mask); }
        }

        [Test]
        public void WatercolorContourOverrideDoesNotChangeCachedDefaultOnOtherSurfaces()
        {
            var before = new LabPaintSurface(Settings(), 123u);
            Exercise(before);
            string expected = Hash(before);
            Texture2D mask = Ellipse();
            try
            {
                var study = new LabPaintSurface(Settings(), 123u);
                study.SetPatternForAppearanceStudy(mask);
                Exercise(study);
                Assert.That(Hash(study), Is.Not.EqualTo(expected));
                var after = new LabPaintSurface(Settings(), 123u);
                Exercise(after);
                Assert.That(Hash(after), Is.EqualTo(expected), "A local study must not replace the static Splatter cache.");
                AssertSameBudget(before, after);
                Assert.That(Hash(before), Is.EqualTo(expected), "An existing surface must also retain its original mask.");
            }
            finally { Object.DestroyImmediate(mask); }
        }

        [Test]
        public void WatercolorContourCopiesPixelsBeforeSourceMutationAndDestruction()
        {
            Texture2D source = Ellipse(), control = Ellipse();
            try
            {
                var expected = new LabPaintSurface(Settings(), 123u);
                var actual = new LabPaintSurface(Settings(), 123u);
                expected.SetPatternForAppearanceStudy(control);
                actual.SetPatternForAppearanceStudy(source);
                source.SetPixels32(new Color32[source.width * source.height]);
                source.Apply(false, false);
                Object.DestroyImmediate(source); source = null;
                Exercise(expected); Exercise(actual);
                Assert.That(Hash(actual), Is.EqualTo(Hash(expected)), "Hits and later growth must use the immutable CPU snapshot.");
                AssertSameBudget(expected, actual);
            }
            finally
            {
                if (source != null) Object.DestroyImmediate(source);
                Object.DestroyImmediate(control);
            }
        }

        [Test]
        public void WatercolorContourRejectsInvalidOrLateChangesAtomicallyAndResetRestoresDefault()
        {
            Texture2D mask = Ellipse(), empty = new Texture2D(65, 65, TextureFormat.RGBA32, false, true);
            Texture2D unreadable = Ellipse();
            try
            {
                empty.SetPixels32(new Color32[65 * 65]); empty.Apply(false, false);
                unreadable.Apply(false, true);
                var study = new LabPaintSurface(Settings(), 123u);
                var control = new LabPaintSurface(Settings(), 123u);
                study.SetPatternForAppearanceStudy(mask);
                control.SetPatternForAppearanceStudy(mask);
                Assert.Catch<ArgumentException>(() => study.SetPatternForAppearanceStudy(null));
                Assert.Catch<ArgumentException>(() => study.SetPatternForAppearanceStudy(unreadable));
                Assert.Throws<InvalidOperationException>(() => study.SetPatternForAppearanceStudy(empty));
                Assert.That(study.PaintCount, Is.Zero);
                Assert.That(study.CoveredCount, Is.Zero);
                Exercise(study); Exercise(control);
                Assert.That(Hash(study), Is.EqualTo(Hash(control)), "A rejected replacement must preserve the installed mask.");
                string painted = Hash(study);
                Assert.Throws<InvalidOperationException>(() => study.SetPatternForAppearanceStudy(mask));
                Assert.That(Hash(study), Is.EqualTo(painted));
                var legacyRules = Settings(); legacyRules.Pattern = LabPaintPattern.Legacy;
                var legacy = new LabPaintSurface(legacyRules, 123u);
                Assert.Throws<InvalidOperationException>(() => legacy.SetPatternForAppearanceStudy(mask));
                Assert.That(legacy.PaintCount, Is.Zero);

                study.Clear(); Exercise(study);
                Assert.That(Hash(study), Is.EqualTo(painted), "Clear retains the same local contour for a fresh fixture.");
                study.Reset(Settings(), 123u); Exercise(study);
                var baseline = new LabPaintSurface(Settings(), 123u); Exercise(baseline);
                Assert.That(Hash(study), Is.EqualTo(Hash(baseline)), "Reset must restore the configured default mask.");
                AssertSameBudget(study, baseline);
            }
            finally
            {
                Object.DestroyImmediate(mask); Object.DestroyImmediate(empty); Object.DestroyImmediate(unreadable);
            }
        }

        private static Texture2D Ellipse()
        {
            const int size = 65;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x / (float)(size - 1) - .5f) / .43f;
                    float v = (y / (float)(size - 1) - .5f) / .16f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(u * u + v * v <= 1f ? 255 : 0));
                }
            texture.SetPixels32(pixels); texture.Apply(false, false);
            return texture;
        }

        private static void Hit(LabPaintSurface surface, Vector3 direction) =>
            Assert.That(surface.RegisterHit(direction, Color.blue, 1u, 1), Is.True);

        private static void Exercise(LabPaintSurface surface)
        {
            Hit(surface, Vector3.forward);
            for (int i = 0; i < 3; i++) { Hit(surface, Vector3.forward); surface.Advance(1d); }
        }

        private static void AssertSameBudget(LabPaintSurface expected, LabPaintSurface actual)
        {
            Assert.That(actual.CoveredCount, Is.EqualTo(expected.CoveredCount));
            Assert.That(actual.PaintCount, Is.EqualTo(expected.PaintCount));
            Assert.That(actual.PendingCoverage, Is.EqualTo(expected.PendingCoverage));
        }

        private static string Hash(LabPaintSurface surface)
        {
            var pixels = new Color32[surface.CellCount]; var slots = new byte[surface.CellCount];
            surface.CopyPixels(pixels); surface.CopyPainterSlots(slots);
            return LabPaintPublisher.Hash(pixels, slots);
        }
    }
}
#endif
