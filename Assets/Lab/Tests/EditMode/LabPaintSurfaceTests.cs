using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabPaintSurfaceTests
    {
        [Test]
        public void NewSurfaceHasTransparentPixelsAndNoProgress()
        {
            var surface = new LabPaintSurface();
            Assert.That(LabPaintSurface.PixelCount, Is.EqualTo(256 * 128));
            Assert.That(surface.CoveredCount, Is.Zero);
            Assert.That(surface.PaintCount, Is.Zero);
            Assert.That(surface.Coverage, Is.Zero);
            Assert.That(Pixels(surface), Is.All.EqualTo(new Color32(0, 0, 0, 0)));
        }

        [Test]
        public void OverlappingColorsAccumulateWithoutCountingTheSameAreaTwice()
        {
            var surface = new LabPaintSurface();
            Assert.That(surface.Stamp(Vector3.forward, Color.red), Is.True);
            Color32[] red = Pixels(surface);
            int covered = surface.CoveredCount;
            uint revision = surface.Revision;
            Assert.That(covered, Is.GreaterThan(0));
            Assert.That(surface.Stamp(Vector3.forward, Color.blue), Is.True);
            Color32[] blue = Pixels(surface);
            int center = LabPaintSurface.Height / 2 * LabPaintSurface.Width + LabPaintSurface.Width * 3 / 4;
            Assert.That(surface.PaintCount, Is.EqualTo(2));
            Assert.That(surface.CoveredCount, Is.EqualTo(covered));
            Assert.That(surface.Revision, Is.Not.EqualTo(revision));
            Assert.That(blue[center].r, Is.LessThan(red[center].r));
            Assert.That(blue[center].b, Is.GreaterThan(220));
            Assert.That(blue[center].a, Is.GreaterThan(red[center].a));
            AssertAlphaMatchesCoverage(surface, blue);
            Assert.That(blue.All(pixel => pixel.r <= pixel.a && pixel.g <= pixel.a && pixel.b <= pixel.a), Is.True,
                "The shader expects premultiplied RGB bytes.");
        }

        [Test]
        public void NewLocationsStillPaintAfter128RepeatedHitsAndKeepEarlierPaint()
        {
            var surface = new LabPaintSurface();
            Assert.That(surface.Stamp(Vector3.forward, Color.red), Is.True);
            float firstCoverage = surface.Coverage;
            for (int i = 1; i < 128; i++) Assert.That(surface.Stamp(Vector3.forward, Color.red), Is.True);
            Assert.That(surface.Coverage, Is.EqualTo(firstCoverage));
            Color32[] before = Pixels(surface);
            Assert.That(surface.Stamp(Vector3.back, Color.green), Is.True);
            Assert.That(surface.PaintCount, Is.EqualTo(129));
            Assert.That(surface.Coverage, Is.GreaterThan(firstCoverage));
            Color32[] after = Pixels(surface);
            for (int i = 0; i < before.Length; i++)
                if (before[i].a > 0) Assert.That(after[i].a, Is.GreaterThan(0));
            AssertAlphaMatchesCoverage(surface, after);
        }

        [Test]
        public void StampsCrossTheLongitudeSeamAndCoverBothPolarCaps()
        {
            var surface = new LabPaintSurface();
            Assert.That(surface.Stamp(Vector3.left, Color.red), Is.True);
            Color32[] seam = Pixels(surface);
            int middleRow = LabPaintSurface.Height / 2 * LabPaintSurface.Width;
            Assert.That(seam[middleRow].a, Is.GreaterThan(0));
            Assert.That(seam[middleRow + LabPaintSurface.Width - 1].a, Is.GreaterThan(0),
                "The same physical mark must be stored on both sides of wrapped U.");
            surface.Clear();
            Assert.That(surface.Stamp(Vector3.up, Color.blue), Is.True);
            Color32[] north = Pixels(surface);
            Assert.That(north.Take(LabPaintSurface.Width).All(pixel => pixel.a > 0), Is.True);
            Assert.That(north.Skip(LabPaintSurface.PixelCount - LabPaintSurface.Width).All(pixel => pixel.a == 0), Is.True);
            Assert.That(surface.Stamp(Vector3.down, Color.green), Is.True);
            Color32[] both = Pixels(surface);
            Assert.That(both.Take(LabPaintSurface.Width).All(pixel => pixel.a > 0), Is.True);
            Assert.That(both.Skip(LabPaintSurface.PixelCount - LabPaintSurface.Width).All(pixel => pixel.a > 0), Is.True);
            AssertAlphaMatchesCoverage(surface, both);
        }

        [Test]
        public void DistributedDefaultBrushStampsCanReachAllCellsWithoutAHitLimit()
        {
            var surface = new LabPaintSurface();
            const int stamps = 256;
            for (int i = 0; i < stamps; i++)
            {
                float y = 1f - 2f * (i + .5f) / stamps;
                float ring = Mathf.Sqrt(1f - y * y);
                float angle = i * 2.39996323f;
                Assert.That(surface.Stamp(new Vector3(Mathf.Cos(angle) * ring, y,
                    Mathf.Sin(angle) * ring), Color.red), Is.True);
            }
            Assert.That(surface.PaintCount, Is.EqualTo(stamps));
            Assert.That(surface.CoveredCount, Is.EqualTo(LabPaintSurface.PixelCount));
            Assert.That(surface.Coverage, Is.EqualTo(1f));
            AssertAlphaMatchesCoverage(surface, Pixels(surface));
        }

        [Test]
        public void ClearResetsTheSameMapAndAdvancesItsRevision()
        {
            var surface = new LabPaintSurface();
            surface.Stamp(Vector3.forward, Color.red);
            uint revision = surface.Revision;
            surface.Clear();
            Assert.That(surface.CoveredCount, Is.Zero);
            Assert.That(surface.PaintCount, Is.Zero);
            Assert.That(surface.Coverage, Is.Zero);
            Assert.That(surface.Revision, Is.Not.EqualTo(revision));
            Assert.That(Pixels(surface), Is.All.EqualTo(new Color32(0, 0, 0, 0)));
        }

        [Test]
        public void InvalidInputsCannotChangeCountRevisionOrPixels()
        {
            var surface = new LabPaintSurface();
            surface.Stamp(Vector3.forward, Color.red);
            Color32[] before = Pixels(surface);
            uint revision = surface.Revision;
            int count = surface.CoveredCount;
            foreach (float radius in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                Assert.That(surface.Stamp(Vector3.back, Color.green, radius), Is.False);
            foreach (Vector3 direction in new[] { Vector3.zero, new Vector3(float.NaN, 0, 1),
                new Vector3(float.PositiveInfinity, 1, 0), new Vector3(float.MaxValue, 0, 0) })
                Assert.That(surface.Stamp(direction, Color.green), Is.False);
            Assert.That(surface.Stamp(Vector3.back, new Color(float.NaN, 0, 0, 1)), Is.False);
            Assert.That(surface.Stamp(Vector3.back, new Color(0, 0, 0, float.PositiveInfinity)), Is.False);
            Assert.That(surface.Stamp(Vector3.back, Color.clear), Is.False);
            Assert.That(surface.PaintCount, Is.EqualTo(1));
            Assert.That(surface.CoveredCount, Is.EqualTo(count));
            Assert.That(surface.Revision, Is.EqualTo(revision));
            Assert.That(Pixels(surface), Is.EqualTo(before));
        }

        [Test]
        public void FiniteWholeSphereRadiusIsSafeAndThePixelCopyCannotMutateState()
        {
            var surface = new LabPaintSurface();
            Assert.That(surface.Stamp(Vector3.forward, Color.red, float.MaxValue), Is.True);
            Assert.That(surface.Coverage, Is.EqualTo(1f));
            Color32[] copy = Pixels(surface);
            AssertAlphaMatchesCoverage(surface, copy);
            Color32 first = copy[0];
            copy[0] = default;
            Assert.That(Pixels(surface)[0], Is.EqualTo(first));
            Assert.Throws<ArgumentNullException>(() => surface.CopyPixels(null));
            Assert.Throws<ArgumentException>(() => surface.CopyPixels(new Color32[1]));
        }

        private static Color32[] Pixels(LabPaintSurface surface)
        {
            var result = new Color32[LabPaintSurface.PixelCount];
            surface.CopyPixels(result);
            return result;
        }

        private static void AssertAlphaMatchesCoverage(LabPaintSurface surface, Color32[] pixels)
        {
            int covered = pixels.Count(pixel => pixel.a > 0);
            Assert.That(surface.CoveredCount, Is.EqualTo(covered));
            Assert.That(surface.Coverage, Is.EqualTo(covered / (float)LabPaintSurface.PixelCount));
        }
    }
}
