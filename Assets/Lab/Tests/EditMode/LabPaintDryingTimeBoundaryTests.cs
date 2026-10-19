using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabPaintDryingTimeBoundaryTests
    {
        [Test]
        public void ErosionDueAtTheLastGrowthCellPreventsAFalseTransientCompletion()
        {
            var surface = new LabPaintSurface(Settings(3.125f), 225);
            surface.RegisterHit(Vector3.forward, Color.red, 1, 1);
            surface.RegisterHit(Vector3.forward, Color.blue, 2, 2);
            // 64 blank cells; growth fills 640/sec. The final cell and erosion share t=.1.
            Assert.That(surface.CellCount - surface.CoveredCount, Is.EqualTo(64));
            Assert.That(surface.Advance(.1d), Is.LessThan(0d));
            Assert.That(surface.CoveredCount, Is.EqualTo(surface.CellCount - 6));
            Assert.That(surface.IsComplete, Is.False);
        }

        [Test]
        public void ADeadlineTieStillDefersGrowthWhenDryingIsEnabledButItsRateIsZero()
        {
            var surface = new LabPaintSurface(Settings(0f), 226);
            surface.RegisterHit(Vector3.forward, Color.red, 1, 1);
            surface.RegisterHit(Vector3.forward, Color.blue, 2, 2);
            Assert.That(surface.Advance(.1d, false), Is.LessThan(0d));
            Assert.That(surface.CoveredCount, Is.EqualTo(surface.CellCount - 1));
            Assert.That(surface.Advance(0d), Is.EqualTo(0d));
            Assert.That(surface.IsComplete, Is.True);
            var pixels = new Color32[surface.CellCount]; surface.CopyPixels(pixels);
            uint revision = surface.Revision;
            Assert.That(surface.Advance(1000d), Is.Zero);
            var after = new Color32[surface.CellCount]; surface.CopyPixels(after);
            Assert.That(after, Is.EqualTo(pixels));
            Assert.That(surface.Revision, Is.EqualTo(revision));
        }

        private static LabPaintGrowthSettings Settings(float dryRate) => new LabPaintGrowthSettings
        {
            Resolution = 0, Pattern = LabPaintPattern.Legacy,
            InitialPercent = 96.875f, RepeatPercent = 10f, GrowthPercentPerSecond = 31.25f,
            DryingEnabled = true, DryingDelaySeconds = 0f,
            DryingPercentPerSecond = dryRate, NearbyHitRadiusDegrees = 20f
        };
    }
}
