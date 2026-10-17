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

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void OneStationaryHitPointCanEventuallyCoverEveryCell(int resolution)
        {
            var surface = Growth(resolution);
            Assert.That(surface.RegisterHit(Vector3.forward, Color.red, 7), Is.True);
            for (int hit = 0; hit < 26 && !surface.IsComplete; hit++)
            {
                surface.RegisterHit(Vector3.forward, Color.blue, 9);
                surface.Advance(10);
            }
            Assert.That(surface.IsComplete, Is.True);
            Assert.That(surface.CoveredCount, Is.EqualTo(surface.CellCount));
            Assert.That(surface.PendingCoverage, Is.Zero);
            Assert.That(GrowthPixels(surface).All(pixel => pixel.a == 255), Is.True);
            Assert.That(surface.LastPainter, Is.EqualTo(9));
            Assert.That(surface.Advance(0), Is.Zero);
        }

        [Test]
        public void FiniteBudgetStopsAndIdleTimeCannotBeSavedForTheNextHit()
        {
            var settings = new LabPaintGrowthSettings
            {
                Resolution = 0, InitialPercent = .1f, RepeatPercent = .1f, GrowthPercentPerSecond = 1
            };
            var surface = new LabPaintSurface(settings, 91);
            surface.RegisterHit(Vector3.forward, Color.red, 1);
            Assert.That(surface.CoveredCount, Is.EqualTo(2));
            surface.Advance(1000000);
            Assert.That(surface.CoveredCount, Is.EqualTo(2));
            surface.RegisterHit(Vector3.forward, Color.blue, 2);
            surface.Advance(0);
            Assert.That(surface.CoveredCount, Is.EqualTo(2));
            surface.Advance(100);
            Assert.That(surface.CoveredCount, Is.EqualTo(4));
            Assert.That(surface.PendingCoverage * surface.CellCount, Is.EqualTo(.096d).Within(.000001d));
            surface.Advance(1000000);
            surface.RegisterHit(Vector3.forward, Color.green, 3);
            Assert.That(surface.Advance(0), Is.EqualTo(-1d));
            Assert.That(surface.CoveredCount, Is.EqualTo(4));
            surface.Advance(.05d);
            Assert.That(surface.CoveredCount, Is.EqualTo(5));
            Assert.That(surface.LastPainter, Is.EqualTo(3));
        }

        [Test]
        public void FractionCrossingUsesTheCompletingSupplyColorAndNeverRepaintsOldCells()
        {
            var surface = new LabPaintSurface(new LabPaintGrowthSettings
            { Resolution = 0, InitialPercent = .1f, RepeatPercent = .1f }, 101);
            surface.RegisterHit(Vector3.forward, Color.red, 1);
            Color32[] first = GrowthPixels(surface);
            surface.RegisterHit(Vector3.forward, Color.blue, 2);
            surface.Advance(100);
            Color32[] next = GrowthPixels(surface);
            Assert.That(next.Count(pixel => pixel.Equals((Color32)Color.red)), Is.EqualTo(2));
            Assert.That(next.Count(pixel => pixel.Equals((Color32)Color.blue)), Is.EqualTo(2));
            for (int i = 0; i < first.Length; i++)
                if (first[i].a != 0) Assert.That(next[i], Is.EqualTo(first[i]));
            Assert.That(surface.LastPainter, Is.EqualTo(2));
            Assert.That(next.All(pixel => pixel.a == 0 || pixel.a == 255), Is.True);
        }

        [Test]
        public void TimePartitionAndSeedProduceIdenticalPixelsAndResidualCredit()
        {
            var settings = new LabPaintGrowthSettings
            { Resolution = 0, InitialPercent = 2, RepeatPercent = 37, GrowthPercentPerSecond = 6 };
            var whole = new LabPaintSurface(settings, 729);
            var pieces = new LabPaintSurface(settings, 729);
            foreach (var surface in new[] { whole, pieces })
            {
                surface.RegisterHit(Vector3.forward, Color.red, 1);
                surface.RegisterHit(Vector3.forward, Color.blue, 2);
            }
            whole.Advance(6d);
            for (int i = 0; i < 60; i++) pieces.Advance(.1d);
            Assert.That(GrowthPixels(pieces), Is.EqualTo(GrowthPixels(whole)));
            Assert.That(pieces.CoveredCount, Is.EqualTo(whole.CoveredCount));
            Assert.That(pieces.PendingCoverage, Is.EqualTo(whole.PendingCoverage));
            Assert.That(pieces.Revision, Is.EqualTo(whole.Revision));
            whole.Advance(.02d);
            pieces.Advance(.01d); pieces.Advance(.01d);
            Assert.That(GrowthPixels(pieces), Is.EqualTo(GrowthPixels(whole)));
        }

        [Test]
        public void SeveralRegionsShareOneGlobalRateAndBothReceiveGrowth()
        {
            var surface = new LabPaintSurface(new LabPaintGrowthSettings
            { Resolution = 0, InitialPercent = .1f, RepeatPercent = 10, GrowthPercentPerSecond = 1 }, 23);
            surface.RegisterHit(Vector3.forward, Color.red, 1);
            surface.RegisterHit(Vector3.back, Color.blue, 2);
            surface.RegisterHit(Vector3.forward, Color.red, 1);
            surface.RegisterHit(Vector3.back, Color.blue, 2);
            Assert.That(surface.RegionCount, Is.EqualTo(2));
            surface.Advance(1);
            Assert.That(surface.CoveredCount, Is.EqualTo(24));
            Color32[] colors = GrowthPixels(surface);
            Assert.That(colors.Count(pixel => pixel.Equals((Color32)Color.red)), Is.EqualTo(12));
            Assert.That(colors.Count(pixel => pixel.Equals((Color32)Color.blue)), Is.EqualTo(12));
        }

        [Test]
        public void LongitudeSeamConnectsButNorthAndSouthCapsDoNotWrapToEachOther()
        {
            var settings = new LabPaintGrowthSettings { Resolution = 0, InitialPercent = .1f };
            var seam = new LabPaintSurface(settings, 17);
            seam.RegisterHit(CellDirection(0, 16, 64, 32), Color.red, 1);
            seam.RegisterHit(CellDirection(63, 16, 64, 32), Color.blue, 2);
            Assert.That(seam.RegionCount, Is.EqualTo(1));
            Color32[] seamPixels = GrowthPixels(seam);
            Assert.That(seamPixels[16 * 64].a, Is.EqualTo(255));
            Assert.That(seamPixels[16 * 64 + 63].a, Is.EqualTo(255));
            var poles = new LabPaintSurface(settings, 17);
            poles.RegisterHit(Vector3.up, Color.red, 1);
            Assert.That(GrowthPixels(poles).Skip(31 * 64).All(pixel => pixel.a == 0), Is.True);
            poles.RegisterHit(Vector3.down, Color.blue, 2);
            Assert.That(poles.RegionCount, Is.EqualTo(2));
            Assert.That(poles.CoveredCount, Is.EqualTo(4));
        }

        [Test]
        public void MeetingRegionsMergeBudgetsWithoutOverwritingEitherColor()
        {
            var surface = new LabPaintSurface(new LabPaintGrowthSettings
            { Resolution = 0, InitialPercent = .1f, RepeatPercent = 100, GrowthPercentPerSecond = 100 }, 77);
            surface.RegisterHit(Vector3.forward, Color.red, 1);
            surface.RegisterHit(Vector3.back, Color.blue, 2);
            Color32[] before = GrowthPixels(surface);
            surface.RegisterHit(Vector3.forward, Color.green, 3);
            surface.Advance(2);
            Assert.That(surface.IsComplete, Is.True);
            Assert.That(surface.RegionCount, Is.EqualTo(1));
            Color32[] after = GrowthPixels(surface);
            for (int i = 0; i < before.Length; i++)
                if (before[i].a != 0) Assert.That(after[i], Is.EqualTo(before[i]));
            Assert.That(surface.LastPainter, Is.EqualTo(3));
        }

        [Test]
        public void AnEnclosedInteriorHoleRemainsOnTheGrowthFrontierAndGetsFilled()
        {
            var surface = new LabPaintSurface(new LabPaintGrowthSettings
            {
                Resolution = 0, InitialPercent = .1f, RepeatPercent = 100,
                GrowthPercentPerSecond = 100, FillChancePercent = 100
            }, 33);
            foreach (var cell in new[] { new Vector2Int(31, 16), new Vector2Int(33, 16),
                new Vector2Int(32, 15), new Vector2Int(32, 17) })
                surface.RegisterHit(CellDirection(cell.x, cell.y, 64, 32), Color.red, 1);
            const int hole = 16 * 64 + 32;
            Color32[] before = GrowthPixels(surface);
            Assert.That(surface.RegionCount, Is.EqualTo(1));
            Assert.That(before[hole].a, Is.Zero, "The fixture starts with a closed one-cell hole.");
            Assert.That(before[hole - 1].a, Is.EqualTo(255));
            Assert.That(before[hole + 1].a, Is.EqualTo(255));
            Assert.That(before[hole - 64].a, Is.EqualTo(255));
            Assert.That(before[hole + 64].a, Is.EqualTo(255));
            surface.RegisterHit(CellDirection(31, 16, 64, 32), Color.blue, 2);
            surface.Advance(.1);
            Color32[] after = GrowthPixels(surface);
            Assert.That(after[hole].a, Is.EqualTo(255));
            Assert.That(surface.IsComplete, Is.False, "Filling a hole does not need whole-map completion.");
            for (int i = 0; i < before.Length; i++)
                if (before[i].a != 0) Assert.That(after[i], Is.EqualTo(before[i]));
        }

        [Test]
        public void ManySeparateSeedsCanMergeAndFinishWithoutAFrontierDeadEnd()
        {
            for (uint seed = 1; seed <= 12; seed++)
            {
                var surface = new LabPaintSurface(new LabPaintGrowthSettings
                {
                    Resolution = 0, InitialPercent = .1f, RepeatPercent = 100,
                    GrowthPercentPerSecond = 100, FillChancePercent = seed % 2 == 0 ? 100 : 0,
                    VerticalWeight = seed % 3 + 1
                }, seed);
                for (int hit = 0; hit < 24; hit++)
                {
                    float y = 1f - 2f * (hit + .5f) / 24f;
                    float ring = Mathf.Sqrt(1f - y * y);
                    float angle = hit * 2.39996323f;
                    surface.RegisterHit(new Vector3(Mathf.Cos(angle) * ring, y,
                        Mathf.Sin(angle) * ring), Color.red, (ulong)hit + 1);
                }
                surface.RegisterHit(CellDirection(32, 0, 64, 32), Color.blue, 99);
                // Supply the known first accepted seed rather than relying on the last hit's location.
                float firstY = 1f - 1f / 24f;
                surface.RegisterHit(new Vector3(Mathf.Sqrt(1f - firstY * firstY), firstY, 0), Color.green, 100);
                surface.Advance(2);
                Assert.That(surface.IsComplete, Is.True, "seed " + seed);
                Assert.That(surface.RegionCount, Is.EqualTo(1));
                Assert.That(GrowthPixels(surface).All(pixel => pixel.a == 255), Is.True);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void InitialAndTimedAreaPercentAreIndependentOfResolutionWithinOneCell(int resolution)
        {
            var surface = new LabPaintSurface(new LabPaintGrowthSettings
            { Resolution = resolution, InitialPercent = 2, RepeatPercent = 10, GrowthPercentPerSecond = 3 }, 5);
            surface.RegisterHit(Vector3.forward, Color.red, 1);
            Assert.That(Math.Abs(surface.Coverage - .02d), Is.LessThanOrEqualTo(1d / surface.CellCount));
            int initial = surface.CoveredCount;
            surface.RegisterHit(Vector3.forward, Color.blue, 2);
            surface.Advance(1);
            Assert.That(Math.Abs((surface.CoveredCount - initial) / (double)surface.CellCount - .03d),
                Is.LessThanOrEqualTo(1d / surface.CellCount));
        }

        [Test]
        public void VerticalWeightChangesTheShapeWhileHorizontalGrowthRemainsPossible()
        {
            var horizontal = new LabPaintSurface(new LabPaintGrowthSettings
            { Resolution = 0, InitialPercent = 2, VerticalWeight = 1, FillChancePercent = 0 }, 848);
            var vertical = new LabPaintSurface(new LabPaintGrowthSettings
            { Resolution = 0, InitialPercent = 2, VerticalWeight = 100, FillChancePercent = 0 }, 848);
            horizontal.RegisterHit(Vector3.forward, Color.red, 1);
            vertical.RegisterHit(Vector3.forward, Color.red, 1);
            Assert.That(GrowthPixels(vertical), Is.Not.EqualTo(GrowthPixels(horizontal)));
            int[] occupied = GrowthPixels(vertical).Select((value, index) => value.a == 255 ? index : -1)
                .Where(index => index >= 0).ToArray();
            Assert.That(occupied.Select(index => index / 64).Distinct().Count(), Is.GreaterThan(2));
            Assert.That(occupied.Select(index => index % 64).Distinct().Count(), Is.GreaterThan(1));
            vertical.RegisterHit(Vector3.forward, Color.blue, 2);
            for (int i = 0; i < 25 && !vertical.IsComplete; i++)
            {
                vertical.RegisterHit(Vector3.forward, Color.blue, 2);
                vertical.Advance(10);
            }
            Assert.That(vertical.IsComplete, Is.True);
        }

        [Test]
        public void ExactFinalCellCanBeDeferredAtAnExclusiveDeadline()
        {
            var settings = new LabPaintGrowthSettings
            { Resolution = 0, InitialPercent = 50, RepeatPercent = 50, GrowthPercentPerSecond = 50 };
            var exclusive = new LabPaintSurface(settings, 331);
            var inclusive = new LabPaintSurface(settings, 331);
            foreach (var surface in new[] { exclusive, inclusive })
            {
                surface.RegisterHit(Vector3.forward, Color.red, 1);
                surface.RegisterHit(Vector3.forward, Color.blue, 2);
            }
            Assert.That(exclusive.Advance(1d, false), Is.EqualTo(-1d));
            Assert.That(exclusive.IsComplete, Is.False);
            Assert.That(exclusive.CoveredCount, Is.EqualTo(exclusive.CellCount - 1));
            Assert.That(exclusive.Advance(0d, false), Is.EqualTo(-1d));
            Assert.That(exclusive.Advance(0d, true), Is.Zero);
            Assert.That(inclusive.Advance(1d), Is.EqualTo(1d));
            Assert.That(GrowthPixels(exclusive), Is.EqualTo(GrowthPixels(inclusive)));
            Assert.That(exclusive.LastPainter, Is.EqualTo(2));
        }

        [Test]
        public void InitialWholeSurfaceCompletesImmediatelyWithOpaqueCells()
        {
            var surface = new LabPaintSurface(new LabPaintGrowthSettings { Resolution = 0, InitialPercent = 100 }, 4);
            surface.RegisterHit(Vector3.up, new Color(.2f, .3f, .4f, .01f), 80);
            Assert.That(surface.IsComplete, Is.True);
            Assert.That(surface.LastPainter, Is.EqualTo(80));
            Assert.That(surface.Advance(0), Is.Zero);
            Assert.That(GrowthPixels(surface).All(pixel => pixel.a == 255), Is.True);
        }

        [Test]
        public void ReplicaReplacementValidatesAtomicallyAndDoesNotGrantGrowthAuthority()
        {
            var surface = Growth(0);
            surface.RegisterHit(Vector3.forward, Color.red, 1);
            Color32[] source = GrowthPixels(surface);
            var replica = new LabPaintSurface();
            Assert.That(replica.TryReplacePixels(64, 32, 80, source, 9), Is.True);
            Color32[] accepted = GrowthPixels(replica);
            Assert.That(replica.CoveredCount, Is.EqualTo(surface.CoveredCount));
            Assert.That(replica.Revision, Is.EqualTo(80));
            Assert.That(replica.PaintCount, Is.EqualTo(9));
            Assert.That(replica.RegisterHit(Vector3.back, Color.blue, 2), Is.False);
            Assert.That(replica.Advance(10), Is.EqualTo(-1d));
            source[0] = new Color32(0, 0, 0, 128);
            Assert.That(replica.TryReplacePixels(64, 32, 81, source, 10), Is.False);
            Assert.That(replica.TryReplacePixels(32, 64, 81, accepted, 10), Is.False);
            Assert.That(replica.TryReplacePixels(64, 32, 81, new Color32[1], 10), Is.False);
            Assert.That(replica.TryReplacePixels(64, 32, 81, accepted, -1), Is.False);
            Assert.That(replica.Revision, Is.EqualTo(80));
            Assert.That(replica.PaintCount, Is.EqualTo(9));
            Assert.That(GrowthPixels(replica), Is.EqualTo(accepted));
            replica.Clear();
            Assert.That(replica.Columns, Is.EqualTo(64));
            Assert.That(replica.Rows, Is.EqualTo(32));
            Assert.That(replica.CoveredCount, Is.Zero);
        }

        [Test]
        public void InvalidGrowthInputsAndSettingsLeaveAllStateUnchanged()
        {
            var surface = Growth(0);
            surface.RegisterHit(Vector3.forward, Color.red, 1);
            Color32[] before = GrowthPixels(surface);
            uint revision = surface.Revision;
            double pending = surface.PendingCoverage;
            int count = surface.PaintCount;
            Assert.That(surface.RegisterHit(Vector3.zero, Color.blue, 2), Is.False);
            Assert.That(surface.RegisterHit(new Vector3(float.NaN, 0, 1), Color.blue, 2), Is.False);
            Assert.That(surface.RegisterHit(Vector3.back, Color.clear, 2), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => surface.Advance(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => surface.Advance(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => surface.Reset(new LabPaintGrowthSettings { Resolution = 3 }, 1));
            Assert.That(surface.Revision, Is.EqualTo(revision));
            Assert.That(surface.PendingCoverage, Is.EqualTo(pending));
            Assert.That(surface.PaintCount, Is.EqualTo(count));
            Assert.That(GrowthPixels(surface), Is.EqualTo(before));
        }

        private static LabPaintSurface Growth(int resolution) => new LabPaintSurface(
            new LabPaintGrowthSettings { Resolution = resolution }, 333);

        private static Vector3 CellDirection(int x, int y, int columns, int rows)
        {
            float vertical = 1f - 2f * (y + .5f) / rows;
            float ring = Mathf.Sqrt(1f - vertical * vertical);
            float angle = ((x + .5f) / columns - .5f) * 2f * Mathf.PI;
            return new Vector3(Mathf.Cos(angle) * ring, vertical, Mathf.Sin(angle) * ring);
        }

        private static Color32[] GrowthPixels(LabPaintSurface surface)
        {
            var result = new Color32[surface.CellCount];
            surface.CopyPixels(result);
            return result;
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
