using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Authoritative-cell regressions for mixed white/repeated impacts, not live device-play evidence.</summary>
    public sealed class LabPaintSplatterConsistencyTests
    {
        private static readonly Color[] Palette = { Color.red, Color.yellow, Color.green, Color.blue, Color.magenta };
        // Virgin first-hit maps captured from main 19289cbf before #211. These lock only
        // the approved initial SPLATTER silhouette, not the replacement growth timeline.
        private static readonly string[] ApprovedVirginHashes =
        {
            "906e24b353472d097612d67f8b7049674ffa8753cfb7c0e4efb514bba58083ee",
            "bb126779ca742bd74d2b6c345f989a19da01564bc9ac3a61282717c0ff0f9982",
            "98477080870afc1e5082578e43c7f4eb7d3817b4bc38d272a889fad7c8ac3532"
        };

        private static LabPaintGrowthSettings Settings(int resolution = 2) => new LabPaintGrowthSettings
        {
            Resolution = resolution, Pattern = LabPaintPattern.Splatter,
            InitialPercent = 2f, RepeatPercent = 4f, GrowthPercentPerSecond = 6f,
            ClusterCompactness = .85f, ClusterIrregularity = .25f
        };

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void DefaultRepeatAddsOneAndHalfPercentOfActualNewAreaAtEveryResolution(int resolution)
        {
            var settings = new LabPaintGrowthSettings { Resolution = resolution };
            Assert.That(settings.RepeatPercent, Is.EqualTo(1.5f));
            var surface = new LabPaintSurface(settings, 211u);
            surface.RegisterHit(Vector3.forward, Color.red, 11, 1);
            var before = Pixels(surface);
            int initial = surface.CoveredCount;
            surface.RegisterHit(Vector3.forward, Color.blue, 22, 2);
            surface.Advance(10d);
            int expectedTotal = (int)decimal.Floor(3.5m * surface.CellCount / 100m);
            Assert.That(surface.CoveredCount, Is.EqualTo(expectedTotal));
            Assert.That(surface.CoveredCount - initial,
                Is.EqualTo(.015d * surface.CellCount).Within(1d));
            AssertOldPaintUnchanged(before, Pixels(surface));
            Assert.That(surface.PendingCoverage, Is.LessThan(1d / surface.CellCount));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void VirginSplatterKeepsItsApprovedFootprintAndCountsDetachedDroplets(int resolution)
        {
            var surface = new LabPaintSurface(Settings(resolution), 200u);
            Assert.That(surface.RegisterHit(Vector3.forward, Color.red, 11, 1), Is.True);
            var pixels = Pixels(surface);
            int requested = (int)decimal.Floor(2m * surface.CellCount / 100m);
            TestContext.WriteLine($"VIRGIN_SPLATTER resolution={resolution} cells={requested} hash={LabPaintPublisher.Hash(pixels, Painters(surface))}");
            Assert.That(LabPaintPublisher.Hash(pixels, Painters(surface)), Is.EqualTo(ApprovedVirginHashes[resolution]),
                "The approved first SPLATTER must remain byte-identical to the checked main baseline.");
            Assert.That(surface.CoveredCount, Is.EqualTo(requested));
            Assert.That(pixels.Count(p => p.a != 0), Is.EqualTo(requested));
            Assert.That(surface.RegionCount, Is.GreaterThan(1), "The initial approved detached splashes remain actual paint.");
            Assert.That(pixels.Where(p => p.a != 0), Is.All.EqualTo((Color32)Color.red));
            Assert.That(Painters(surface).Where(p => p != 0), Is.All.EqualTo((byte)1));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void WhiteImpactKeepsTheSameImmediateMaskWhenItOverlapsOldPaint(int resolution)
        {
            var settings = Settings(resolution);
            var overlap = new LabPaintSurface(settings, 200u);
            overlap.RegisterHit(Vector3.forward, Color.red, 11, 1);
            var before = Pixels(overlap);
            Vector3 direction = OverlappingWhiteDirection(overlap, before);
            var isolated = new LabPaintSurface(settings, 200u);
            isolated.RegisterHit(Vector3.back, Color.red, 11, 1);
            isolated.RegisterHit(direction, Color.green, 33, 3);
            var isolatedPixels = Pixels(isolated);
            int[] sameMask = Enumerable.Range(0, isolated.CellCount)
                .Where(i => isolatedPixels[i].Equals((Color32)Color.green)).ToArray();
            Assert.That(sameMask.Any(i => before[i].a != 0), Is.True, "The fixture must overlap part of the old main stain.");
            Assert.That(sameMask.All(i => Vector3.Dot(Direction(overlap, i), Vector3.forward) > 0f), Is.True,
                "The opposite reference stain must not obstruct the new mask.");

            overlap.RegisterHit(direction, Color.green, 33, 3);
            var after = Pixels(overlap);
            int[] actual = Added(before, after);
            int[] expected = sameMask.Where(i => before[i].a == 0).ToArray();
            Assert.That(actual, Is.EqualTo(expected),
                "Overlap must subtract existing paint from the fixed splash; it cannot widen the mask to replace missing area immediately.");
            Assert.That(actual.Length, Is.LessThan(sameMask.Length));
            AssertOldPaintUnchanged(before, after);
            Assert.That(actual.All(i => after[i].Equals((Color32)Color.green) && overlap.GetPainterSlot(i) == 3), Is.True);
            double totalCredit = 4d / 100d - overlap.Coverage;
            Assert.That(overlap.PendingCoverage, Is.EqualTo(totalCredit).Within(2e-7d),
                "The local overlap shortfall remains timed growth credit, including the initial fractional cells.");
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ANewWhiteFootprintCannotSpendAnOlderQueuedRepeatsPigmentOrPainter(int resolution)
        {
            var surface = new LabPaintSurface(Settings(resolution), 200u);
            surface.RegisterHit(Vector3.forward, Color.red, 11, 1);
            surface.RegisterHit(Vector3.forward, Color.blue, 22, 2);
            var before = Pixels(surface);
            var beforePainters = Painters(surface);
            double budgetBefore = surface.PendingCoverage;
            Vector3 white = OverlappingWhiteDirection(surface, before);
            surface.RegisterHit(white, Color.green, 33, 3);
            var after = Pixels(surface);
            int[] added = Added(before, after);
            Assert.That(added, Is.Not.Empty);
            Assert.That(added.All(i => after[i].Equals((Color32)Color.green) && surface.GetPainterSlot(i) == 3), Is.True,
                "A mask component joining an older funded region still spends this impact's own pigment and P number.");
            Assert.That(after.Count(p => p.Equals((Color32)Color.blue)), Is.Zero,
                "No queued repeat may appear until timed growth selects and spends it.");
            AssertOldPaintUnchanged(before, after);
            foreach (int cell in Enumerable.Range(0, before.Length).Where(i => before[i].a != 0))
                Assert.That(surface.GetPainterSlot(cell), Is.EqualTo(beforePainters[cell]));
            Assert.That(surface.PendingCoverage,
                Is.EqualTo(budgetBefore + .02d - added.Length / (double)surface.CellCount).Within(1e-10d));
            surface.Advance(100d);
            Assert.That(Pixels(surface).Count(p => p.Equals((Color32)Color.blue)), Is.GreaterThan(0),
                "The earlier repeat remains funded after the immediate mask merges with its region.");
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void OverlapShortfallAndCellFractionsSpendOnceAndStopWithoutIdleCredit(int resolution)
        {
            var settings = Settings(resolution); settings.RepeatPercent = .1f;
            var surface = new LabPaintSurface(settings, 200u);
            surface.RegisterHit(Vector3.forward, Color.red, 11, 1);
            var before = Pixels(surface);
            surface.RegisterHit(OverlappingWhiteDirection(surface, before), Color.green, 33, 3);
            surface.RegisterHit(Vector3.forward, Color.blue, 22, 2);
            surface.Advance(100d);
            decimal expectedCredit = 4.1m * surface.CellCount / 100m;
            Assert.That(surface.CoveredCount, Is.EqualTo((int)decimal.Floor(expectedCredit)),
                "Merged fractional credit is retained, while the same old cell never counts twice.");
            Assert.That(surface.PendingCoverage,
                Is.EqualTo((double)(expectedCredit - decimal.Floor(expectedCredit)) / surface.CellCount).Within(1e-10d));
            var exhausted = Pixels(surface); var painters = Painters(surface); uint revision = surface.Revision;
            surface.Advance(100000d);
            Assert.That(Pixels(surface), Is.EqualTo(exhausted));
            Assert.That(Painters(surface), Is.EqualTo(painters));
            Assert.That(surface.Revision, Is.EqualTo(revision));
            surface.Clear();
            Assert.That(surface.CoveredCount, Is.Zero);
            Assert.That(surface.PendingCoverage, Is.Zero);
            Assert.That(Pixels(surface), Is.All.EqualTo(new Color32()));
            Assert.That(Painters(surface), Is.All.EqualTo((byte)0));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void WhiteHitNearCompletionCannotImmediatelyFillFarSeparatedHolesToMeetItsQuota(int resolution)
        {
            var settings = Settings(resolution); settings.RepeatPercent = 1f;
            var surface = new LabPaintSurface(settings, 200u);
            for (int hit = 0; hit < 49; hit++)
            {
                var pixels = Pixels(surface);
                Vector3 desired = FibonacciDirection(hit, 49);
                int white = ClosestCell(surface, pixels, desired, false);
                Assert.That(white, Is.GreaterThanOrEqualTo(0));
                surface.RegisterHit(Direction(surface, white), Color.red, 11, 1);
                surface.Advance(100d);
            }
            var filled = Pixels(surface);
            int oldPaint = Array.FindIndex(filled, p => p.a != 0);
            surface.RegisterHit(Direction(surface, oldPaint), Color.red, 11, 1);
            surface.Advance(100d);
            var before = Pixels(surface);
            int[] whites = Enumerable.Range(0, before.Length).Where(i => before[i].a == 0).ToArray();
            Assert.That(whites.Length, Is.GreaterThan(1));
            Assert.That(whites.Length, Is.LessThan(2m * surface.CellCount / 100m));
            int a = FarthestCell(surface, whites, Direction(surface, whites[0]));
            int b = FarthestCell(surface, whites, Direction(surface, a));
            double separation = Arc(Direction(surface, a), Direction(surface, b));
            Assert.That(separation, Is.GreaterThan(1d), "The fixture must contain genuinely separated late holes, not one local white dot.");
            surface.RegisterHit(Direction(surface, a), Color.green, 33, 3);
            Assert.That(surface.IsComplete, Is.False,
                "An initial area larger than the remaining whites must not inflate one splash over the whole object.");
            Assert.That(Pixels(surface)[b].a, Is.Zero, "A distant white hole waits for actual adjacent timed growth.");
            surface.Advance(100d);
            Assert.That(surface.IsComplete, Is.True, "Local footprint bounds must not strand the final empty surface cells.");
            Assert.That(surface.PendingCoverage, Is.Zero);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void MixedMovingHitsWithTwoAndFourPercentAreasReplicateEveryActualCellToCompletion(int resolution)
        {
            var settings = Settings(resolution);
            var source = new LabPaintSurface(settings, 745u);
            var replica = new LabPaintSurface(settings, 745u);
            var publisher = new LabPaintPublisher(); var receiver = new LabPaintReceiver(replica);
            int whiteHits = 0, repeatHits = 0;
            for (int hit = 0; hit < 90 && !source.IsComplete; hit++)
            {
                var before = Pixels(source);
                bool preferPainted = hit % 2 == 1;
                int cell = ClosestCell(source, before, FibonacciDirection(hit, 90), preferPainted);
                if (cell < 0) cell = Array.FindIndex(before, p => p.a == 0);
                bool painted = before[cell].a != 0;
                if (painted) repeatHits++; else whiteHits++;
                source.RegisterHit(Direction(source, cell), Palette[hit % 5], (ulong)(101 + hit), hit % 5 + 1);
                var immediate = Pixels(source);
                AssertOldPaintUnchanged(before, immediate);
                Assert.That(source.CoveredCount - before.Count(p => p.a != 0),
                    Is.LessThanOrEqualTo(painted ? 0 : (int)decimal.Floor(2m * source.CellCount / 100m)));
                int beforeTick = source.CoveredCount;
                source.Advance(.4d);
                Assert.That(source.CoveredCount - beforeTick,
                    Is.LessThanOrEqualTo((int)decimal.Ceiling(2.4m * source.CellCount / 100m)));
                AssertOldPaintUnchanged(immediate, Pixels(source));
                AssertPigmentsMatchPainters(source);
                Receive(receiver, publisher.Publish(source, "splatter-consistency", 1, hit + 1));
                AssertSame(source, replica);
            }
            source.Advance(100d);
            Receive(receiver, publisher.Publish(source, "splatter-consistency", 1, 100));
            Assert.That(whiteHits, Is.GreaterThanOrEqualTo(3));
            Assert.That(repeatHits, Is.GreaterThanOrEqualTo(3));
            Assert.That(source.IsComplete, Is.True);
            Assert.That(source.Coverage, Is.EqualTo(1f));
            Assert.That(source.PendingCoverage, Is.Zero);
            Assert.That(Pixels(source).All(p => p.a == 255), Is.True);
            AssertSame(source, replica);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void QueuedRepeatAndOverlappedWhiteHitRemainIdenticalAcrossTimePartitions(int resolution)
        {
            var one = MixedPendingFixture(resolution);
            var many = MixedPendingFixture(resolution);
            one.Advance(4d);
            for (int i = 0; i < 40; i++) many.Advance(.1d);
            AssertSame(one, many);
            Assert.That(many.PendingCoverage, Is.EqualTo(one.PendingCoverage));
        }

        private static LabPaintSurface MixedPendingFixture(int resolution)
        {
            var surface = new LabPaintSurface(Settings(resolution), 200u);
            surface.RegisterHit(Vector3.forward, Color.red, 11, 1);
            surface.RegisterHit(Vector3.forward, Color.blue, 22, 2);
            surface.RegisterHit(OverlappingWhiteDirection(surface, Pixels(surface)), Color.green, 33, 3);
            surface.RegisterHit(Vector3.forward, Color.yellow, 44, 4);
            return surface;
        }

        private static Vector3 OverlappingWhiteDirection(LabPaintSurface surface, Color32[] pixels)
        {
            int cell = Enumerable.Range(0, pixels.Length)
                .Where(i => pixels[i].a == 0 && Neighbors(surface, i).Any(next => pixels[next].a != 0))
                .OrderByDescending(i => Vector3.Dot(Direction(surface, i), Vector3.forward)).First();
            return Direction(surface, cell);
        }
        private static int ClosestCell(LabPaintSurface surface, Color32[] pixels, Vector3 desired, bool painted)
        {
            int result = -1; float best = float.NegativeInfinity;
            for (int cell = 0; cell < pixels.Length; cell++)
                if ((pixels[cell].a != 0) == painted)
                {
                    float dot = Vector3.Dot(Direction(surface, cell), desired);
                    if (dot > best) { result = cell; best = dot; }
                }
            return result;
        }
        private static int FarthestCell(LabPaintSurface surface, int[] cells, Vector3 point)
            => cells.OrderBy(cell => Vector3.Dot(Direction(surface, cell), point)).First();
        private static Vector3 FibonacciDirection(int index, int count)
        {
            float y = 1f - 2f * (index + .5f) / count;
            float angle = index * 2.39996323f;
            float radius = Mathf.Sqrt(1f - y * y);
            return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        }
        private static Color32[] Pixels(LabPaintSurface surface)
        { var values = new Color32[surface.CellCount]; surface.CopyPixels(values); return values; }
        private static byte[] Painters(LabPaintSurface surface)
        { var values = new byte[surface.CellCount]; surface.CopyPainterSlots(values); return values; }
        private static int[] Added(Color32[] before, Color32[] after) => Enumerable.Range(0, before.Length)
            .Where(i => before[i].a == 0 && after[i].a != 0).ToArray();
        private static Vector3 Direction(LabPaintSurface surface, int cell)
        {
            float y = 1f - (cell / surface.Columns + .5f) * 2f / surface.Rows;
            float angle = ((cell % surface.Columns + .5f) / surface.Columns - .5f) * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(1f - y * y);
            return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        }
        private static double Arc(Vector3 a, Vector3 b) => Math.Acos(Math.Max(-1d, Math.Min(1d, Vector3.Dot(a, b))));
        private static IEnumerable<int> Neighbors(LabPaintSurface surface, int cell)
        {
            int x = cell % surface.Columns, y = cell / surface.Columns;
            yield return y * surface.Columns + (x + surface.Columns - 1) % surface.Columns;
            yield return y * surface.Columns + (x + 1) % surface.Columns;
            if (y > 0) yield return cell - surface.Columns;
            if (y + 1 < surface.Rows) yield return cell + surface.Columns;
        }
        private static void AssertOldPaintUnchanged(Color32[] before, Color32[] after)
        {
            for (int cell = 0; cell < before.Length; cell++)
                if (before[cell].a != 0) Assert.That(after[cell], Is.EqualTo(before[cell]), "Existing pigment cannot be replaced.");
        }
        private static void AssertPigmentsMatchPainters(LabPaintSurface surface)
        {
            var pixels = Pixels(surface);
            for (int cell = 0; cell < pixels.Length; cell++)
            {
                int painter = surface.GetPainterSlot(cell);
                if (pixels[cell].a == 0) Assert.That(painter, Is.Zero);
                else Assert.That(pixels[cell], Is.EqualTo((Color32)Palette[painter - 1]), "Painter identity and pigment commit atomically.");
            }
        }
        private static void Receive(LabPaintReceiver receiver, LabPaintFrame[] frames)
        {
            // Publish emits no transaction after an unchanged completed map; callers still compare every replica field.
            if (frames.Length == 0) return;
            LabPaintReceive result = LabPaintReceive.Rejected;
            foreach (var frame in frames) result = receiver.Receive(frame);
            Assert.That(result, Is.EqualTo(LabPaintReceive.Applied));
        }
        private static void AssertSame(LabPaintSurface source, LabPaintSurface replica)
        {
            Assert.That(Pixels(replica), Is.EqualTo(Pixels(source)));
            Assert.That(Painters(replica), Is.EqualTo(Painters(source)));
            Assert.That(replica.CoveredCount, Is.EqualTo(source.CoveredCount));
            Assert.That(replica.PaintCount, Is.EqualTo(source.PaintCount));
            Assert.That(replica.Revision, Is.EqualTo(source.Revision));
        }
    }
}
