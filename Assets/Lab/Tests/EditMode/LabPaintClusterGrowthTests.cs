using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabPaintClusterGrowthTests
    {
        private static LabPaintGrowthSettings Settings(int resolution = 1) => new LabPaintGrowthSettings
        {
            Resolution = resolution, Pattern = LabPaintPattern.Legacy, Style = 1,
            InitialPercent = 6.25f, RepeatPercent = 3.125f, GrowthPercentPerSecond = 100f,
            Raggedness = 0f, ClusterCompactness = .85f, ClusterIrregularity = .25f
        };

        [TestCase(1u)] [TestCase(7u)] [TestCase(29u)] [TestCase(123u)] [TestCase(777u)]
        public void OneRepeatAddsOneCompactBlobWithBroadAttachmentInsteadOfAWholeBoundaryRim(uint seed)
        {
            var surface = new LabPaintSurface(Settings(), seed);
            Assert.That(surface.RegisterHit(Vector3.forward, Color.red, 1, 1), Is.True);
            Color32[] before = Pixels(surface);
            int initial = surface.CoveredCount;
            Assert.That(surface.RegisterHit(Vector3.forward, Color.blue, 2, 2), Is.True);
            surface.Advance(1d);
            Color32[] after = Pixels(surface);
            int[] added = Added(before, after);
            Assert.That(added.Length, Is.EqualTo(surface.CellCount / 32));
            Assert.That(surface.CoveredCount, Is.EqualTo(initial + added.Length));
            Assert.That(Components(surface, added), Is.EqualTo(1), "An unobstructed repeated hit must create one connected new blob.");
            Assert.That(added.All(i => after[i].Equals((Color32)Color.blue) && surface.GetPainterSlot(i) == 2), Is.True);
            foreach (int i in Enumerable.Range(0, before.Length).Where(i => before[i].a > 0))
                Assert.That(after[i], Is.EqualTo(before[i]), "Old paint must not be recolored or reattributed.");

            Vector3 center = Vector3.zero;
            foreach (int cell in added) center += Direction(surface, cell);
            center.Normalize();
            double rms = Math.Sqrt(added.Average(i => Math.Pow(Arc(center, Direction(surface, i)), 2)));
            double diskRadius = Math.Acos(1d - 2d * added.Length / surface.CellCount);
            Assert.That(rms, Is.LessThan(diskRadius * 1.3d), "The new area must stay locally concentrated, not spread around the old boundary.");
            int contact = 0, perimeter = 0;
            var set = new HashSet<int>(added);
            foreach (int cell in added)
                foreach (int other in Neighbors(surface, cell))
                {
                    if (!set.Contains(other)) perimeter++;
                    if (before[other].a != 0) contact++;
                }
            Assert.That(contact, Is.GreaterThanOrEqualTo(Mathf.FloorToInt(Mathf.Sqrt(added.Length) * .5f)), "The blob needs a broad neck to existing paint.");
            Assert.That(contact / (double)perimeter, Is.GreaterThan(.12d));
            Assert.That(surface.PendingCoverage, Is.Zero);
            TestContext.WriteLine($"CLUSTER_SHAPE seed={seed} cells={added.Length} rmsOverDisk={rms / diskRadius:F4} contactRatio={contact / (double)perimeter:F4}");
        }

        [Test]
        public void LaterQueuedHitsCannotMoveTheFirstHitsFrozenClusterOrStealItsInk()
        {
            var alone = new LabPaintSurface(Settings(), 8123);
            var queued = new LabPaintSurface(Settings(), 8123);
            foreach (LabPaintSurface surface in new[] { alone, queued })
            {
                surface.RegisterHit(Vector3.forward, Color.red, 1, 1);
                surface.RegisterHit(Vector3.forward, Color.blue, 2, 2);
            }
            queued.RegisterHit(Vector3.forward, Color.green, 3, 3);
            queued.RegisterHit(Vector3.forward, Color.yellow, 4, 4);
            alone.Advance(.03125d); queued.Advance(.03125d);
            Assert.That(Pixels(queued), Is.EqualTo(Pixels(alone)), "Later accepted hits must not replace or reseed an earlier pending cluster.");
            queued.Advance(1d);
            Color32[] pixels = Pixels(queued);
            Assert.That(pixels.Count(p => p.Equals((Color32)Color.red)), Is.EqualTo(queued.CellCount / 16));
            foreach (Color color in new[] { Color.blue, Color.green, Color.yellow })
                Assert.That(pixels.Count(p => p.Equals((Color32)color)), Is.EqualTo(queued.CellCount / 32));
            Assert.That(queued.PendingCoverage, Is.Zero);
        }

        [TestCase(true)] [TestCase(false)]
        public void EachClusterShapeControlKeepsAreaAndAnchorWhileChangingTheOutline(bool varyCompactness)
        {
            var roundSettings = Settings(); roundSettings.ClusterIrregularity = 0f;
            var organicSettings = Settings(); organicSettings.ClusterIrregularity = 0f;
            if (varyCompactness) organicSettings.ClusterCompactness = 0f;
            else organicSettings.ClusterIrregularity = 1f;
            var round = new LabPaintSurface(roundSettings, 555);
            var organic = new LabPaintSurface(organicSettings, 555);
            foreach (LabPaintSurface surface in new[] { round, organic })
            {
                surface.RegisterHit(Vector3.forward, Color.red, 1, 1);
                surface.RegisterHit(Vector3.forward, Color.blue, 2, 2);
            }
            Assert.That(Pixels(round), Is.EqualTo(Pixels(organic)), "The repeat-only controls must not alter the initial footprint.");
            round.Advance(1d / round.CellCount * 100d / roundSettings.GrowthPercentPerSecond);
            organic.Advance(1d / organic.CellCount * 100d / organicSettings.GrowthPercentPerSecond);
            Assert.That(Pixels(round), Is.EqualTo(Pixels(organic)), "The same seed chooses the same anchor independently of shape controls.");
            round.Advance(1d); organic.Advance(1d);
            Assert.That(round.CoveredCount, Is.EqualTo(organic.CoveredCount));
            Assert.That(round.PendingCoverage, Is.EqualTo(organic.PendingCoverage));
            Assert.That(Pixels(round), Is.Not.EqualTo(Pixels(organic)), "Independent cluster controls must produce an observable shape change.");
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void QueuedClustersRemainIdenticalWhenTimeIsPartitioned(int resolution)
        {
            LabPaintGrowthSettings settings = Settings(resolution); settings.GrowthPercentPerSecond = 2f;
            var one = new LabPaintSurface(settings, 152);
            var many = new LabPaintSurface(settings, 152);
            foreach (LabPaintSurface surface in new[] { one, many })
            {
                surface.RegisterHit(Vector3.forward, Color.red, 1, 1);
                surface.RegisterHit(Vector3.forward, Color.blue, 2, 2);
                surface.RegisterHit(Vector3.forward, Color.green, 3, 3);
                surface.RegisterHit(Vector3.forward, Color.yellow, 4, 4);
            }
            one.Advance(4d);
            for (int i = 0; i < 40; i++) many.Advance(.1d);
            Assert.That(Pixels(many), Is.EqualTo(Pixels(one)));
            var a = new byte[one.CellCount]; var b = new byte[one.CellCount];
            one.CopyPainterSlots(a); many.CopyPainterSlots(b);
            Assert.That(b, Is.EqualTo(a));
            Assert.That(many.Revision, Is.EqualTo(one.Revision));
            Assert.That(many.PendingCoverage, Is.EqualTo(one.PendingCoverage));
        }

        [TestCase(0, LabPaintPattern.Legacy)] [TestCase(1, LabPaintPattern.Legacy)] [TestCase(2, LabPaintPattern.Legacy)]
        [TestCase(0, LabPaintPattern.Broad)] [TestCase(1, LabPaintPattern.Broad)] [TestCase(2, LabPaintPattern.Broad)]
        [TestCase(0, LabPaintPattern.Splatter)] [TestCase(1, LabPaintPattern.Splatter)] [TestCase(2, LabPaintPattern.Splatter)]
        [TestCase(0, LabPaintPattern.Flow)] [TestCase(1, LabPaintPattern.Flow)] [TestCase(2, LabPaintPattern.Flow)]
        public void EveryInitialPatternAndResolutionCanFinishFromTheSameRepeatedHit(int resolution, LabPaintPattern pattern)
        {
            LabPaintGrowthSettings settings = Settings(resolution); settings.Pattern = pattern;
            settings.InitialPercent = 1f; settings.RepeatPercent = 100f;
            var surface = new LabPaintSurface(settings, 900);
            Assert.That(surface.RegisterHit(Vector3.left, Color.red, 1, 1), Is.True);
            Color32[] before = Pixels(surface);
            Assert.That(surface.RegisterHit(Vector3.left, Color.blue, 2, 2), Is.True);
            surface.Advance(2d);
            Assert.That(surface.IsComplete, Is.True);
            Assert.That(surface.CoveredCount, Is.EqualTo(surface.CellCount));
            Assert.That(surface.PendingCoverage, Is.Zero);
            Color32[] after = Pixels(surface);
            Assert.That(after.All(p => p.a == 255), Is.True);
            foreach (int cell in Enumerable.Range(0, before.Length).Where(i => before[i].a > 0))
                Assert.That(after[cell], Is.EqualTo(before[cell]));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void OneClusterCanCrossExistingPaintToFillSeparatedFinalWhitePockets(int resolution)
        {
            LabPaintSurface selected = null;
            for (uint seed = 1; seed <= 16 && selected == null; seed++)
            {
                var settings = Settings(resolution); settings.InitialPercent = 99f; settings.Style = 0; settings.RepeatPercent = 100f;
                var candidate = new LabPaintSurface(settings, seed);
                candidate.RegisterHit(Vector3.forward, Color.red, 1, 1);
                Color32[] pixels = Pixels(candidate);
                int[] empty = Enumerable.Range(0, pixels.Length).Where(i => pixels[i].a == 0).ToArray();
                if (Components(candidate, empty) > 1) selected = candidate;
            }
            Assert.That(selected, Is.Not.Null, "The fixed fixture must contain disconnected unpainted pockets.");
            selected.RegisterHit(Vector3.forward, Color.blue, 2, 2);
            selected.Advance(2d);
            Assert.That(selected.IsComplete, Is.True, "Old paint may separate empty pockets but must not strand the cluster's remaining credit.");
            Assert.That(selected.PendingCoverage, Is.Zero);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ManyQueuedHitsFinishEvenWhenTheirOriginalAnchorsBecomePainted(int resolution)
        {
            var settings = Settings(resolution); settings.RepeatPercent = 6.25f;
            var surface = new LabPaintSurface(settings, 42);
            surface.RegisterHit(Vector3.forward, Color.red, 1, 1);
            for (int hit = 0; hit < 24; hit++)
                Assert.That(surface.RegisterHit(Vector3.forward, hit % 2 == 0 ? Color.blue : Color.green,
                    (ulong)(hit % 2 + 2), hit % 2 + 2), Is.True);
            surface.Advance(2d);
            Assert.That(surface.IsComplete, Is.True);
            Assert.That(surface.PendingCoverage, Is.Zero);
        }

        private static Color32[] Pixels(LabPaintSurface surface)
        {
            var pixels = new Color32[surface.CellCount]; surface.CopyPixels(pixels); return pixels;
        }
        private static int[] Added(Color32[] before, Color32[] after) => Enumerable.Range(0, before.Length)
            .Where(i => before[i].a == 0 && after[i].a != 0).ToArray();
        private static double Arc(Vector3 a, Vector3 b) => Math.Acos(Math.Max(-1d, Math.Min(1d, Vector3.Dot(a, b))));
        private static Vector3 Direction(LabPaintSurface s, int cell)
        {
            float y = 1f - (cell / s.Columns + .5f) * 2f / s.Rows;
            float angle = ((cell % s.Columns + .5f) / s.Columns - .5f) * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(1f - y * y); return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        }
        private static IEnumerable<int> Neighbors(LabPaintSurface s, int cell)
        {
            int x = cell % s.Columns, y = cell / s.Columns;
            yield return y * s.Columns + (x + s.Columns - 1) % s.Columns;
            yield return y * s.Columns + (x + 1) % s.Columns;
            if (y > 0) yield return cell - s.Columns;
            if (y + 1 < s.Rows) yield return cell + s.Columns;
        }
        private static int Components(LabPaintSurface s, IEnumerable<int> cells)
        {
            var remaining = new HashSet<int>(cells); int count = 0;
            var queue = new Queue<int>();
            while (remaining.Count > 0)
            {
                int first = remaining.First(); remaining.Remove(first); queue.Enqueue(first); count++;
                while (queue.Count > 0)
                    foreach (int next in Neighbors(s, queue.Dequeue())) if (remaining.Remove(next)) queue.Enqueue(next);
            }
            return count;
        }
    }
}
