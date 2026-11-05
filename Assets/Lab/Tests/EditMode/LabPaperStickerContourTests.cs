using System;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Actual contour changes with preserved area budgets, not palette/render assertions.</summary>
    public sealed class LabPaperStickerContourTests
    {
        static LabPaintGrowthSettings Rules(LabPaintPattern pattern = LabPaintPattern.Splatter) => new LabPaintGrowthSettings {
            Pattern = pattern, Resolution = 1, InitialPercent = 2f, RepeatPercent = 1.5f,
            GrowthPercentPerSecond = 100f, DryingEnabled = false
        };

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void NewContourChangesGeometryButKeepsFirstAndThreeRehitBudgetsDeterministic(int location)
        {
            Vector3 direction = location == 0 ? Vector3.forward : location == 1 ? Vector3.up : Vector3.left;
            var original = new LabPaintSurface(Rules(), 123u);
            var sticker = new LabPaintSurface(Rules(), 123u);
            var split = new LabPaintSurface(Rules(), 123u);
            sticker.SetPaperStickerContour(); split.SetPaperStickerContour();
            Assert.That(original.UsesPaperStickerContour, Is.False);
            Assert.That(sticker.UsesPaperStickerContour, Is.True);
            Hit(original, direction); Hit(sticker, direction); Hit(split, direction);
            Assert.That(Hash(sticker), Is.Not.EqualTo(Hash(original)), "The approved new silhouette must change the actual paid cells, not only a decorative overlay.");
            AssertBudget(original, sticker); AssertBudget(sticker, split);
            Assert.That(Hash(split), Is.EqualTo(Hash(sticker)));
            for (int rehit = 0; rehit < 3; rehit++) {
                Hit(original, direction); Hit(sticker, direction); Hit(split, direction);
                original.Advance(1d); sticker.Advance(1d); split.Advance(.5d); split.Advance(.5d);
                AssertBudget(original, sticker); AssertBudget(sticker, split);
                Assert.That(Hash(split), Is.EqualTo(Hash(sticker)), "A stable new contour cannot depend on elapsed-time batching.");
            }
            Assert.That(sticker.CoveredCount, Is.EqualTo((int)decimal.Floor(.065m * sticker.CellCount)));
        }

        [Test]
        public void ExplicitStickerContourDoesNotReplaceTheRetainedMaskCache()
        {
            var before = new LabPaintSurface(Rules(), 123u); Exercise(before); string expected = Hash(before);
            var sticker = new LabPaintSurface(Rules(), 123u); sticker.SetPaperStickerContour(); Exercise(sticker);
            var after = new LabPaintSurface(Rules(), 123u); Exercise(after);
            Assert.That(Hash(sticker), Is.Not.EqualTo(expected));
            Assert.That(Hash(after), Is.EqualTo(expected));
            Assert.That(Hash(before), Is.EqualTo(expected));
            AssertBudget(before, sticker); AssertBudget(before, after);
        }

        [TestCase(LabPaintPattern.Broad)]
        [TestCase(LabPaintPattern.Flow)]
        public void SelectingStickerPresentationDoesNotReplaceOtherPatternShapes(LabPaintPattern pattern)
        {
            var original = new LabPaintSurface(Rules(pattern), 456u);
            var selected = new LabPaintSurface(Rules(pattern), 456u);
            selected.SetPaperStickerContour();
            Assert.That(selected.UsesPaperStickerContour, Is.False);
            Exercise(original); Exercise(selected);
            Assert.That(Hash(selected), Is.EqualTo(Hash(original)));
            AssertBudget(original, selected);
        }

        [Test]
        public void LateContourReplacementIsRejectedWithoutChangingExistingPaint()
        {
            var surface = new LabPaintSurface(Rules(), 123u);
            Hit(surface, Vector3.forward); string expected = Hash(surface);
            Assert.Throws<InvalidOperationException>(surface.SetPaperStickerContour);
            Assert.That(Hash(surface), Is.EqualTo(expected));
            Assert.That(surface.UsesPaperStickerContour, Is.False);
        }

        static void Hit(LabPaintSurface surface, Vector3 direction) => Assert.That(surface.RegisterHit(direction, Color.blue, 1, 1), Is.True);
        static void Exercise(LabPaintSurface surface)
        { Hit(surface, Vector3.forward); for (int i = 0; i < 3; i++) { Hit(surface, Vector3.forward); surface.Advance(1d); } }
        static void AssertBudget(LabPaintSurface expected, LabPaintSurface actual)
        { Assert.That(actual.CoveredCount, Is.EqualTo(expected.CoveredCount)); Assert.That(actual.PaintCount, Is.EqualTo(expected.PaintCount)); Assert.That(actual.PendingCoverage, Is.EqualTo(expected.PendingCoverage)); }
        static string Hash(LabPaintSurface surface)
        { var colors = new Color32[surface.CellCount]; var slots = new byte[surface.CellCount]; surface.CopyPixels(colors); surface.CopyPainterSlots(slots); return LabPaintPublisher.Hash(colors, slots); }
    }
}
