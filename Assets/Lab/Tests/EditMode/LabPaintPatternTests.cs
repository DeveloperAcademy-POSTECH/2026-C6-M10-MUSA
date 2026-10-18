using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Real runtime masks and authoritative cell/packet fixtures, not device play evidence.</summary>
    public sealed class LabPaintPatternTests
    {
        private static LabPaintGrowthSettings Settings(LabPaintPattern pattern, int resolution = 2) =>
            new LabPaintGrowthSettings
            {
                Pattern = pattern, Resolution = resolution, InitialPercent = 6f, RepeatPercent = 4f,
                GrowthPercentPerSecond = 100f
            };

        [Test]
        public void FreshGrowthSettingsDefaultToTheApprovedSplatterPattern()
        {
            Assert.That(new LabPaintGrowthSettings().Pattern, Is.EqualTo(LabPaintPattern.Splatter));
            Assert.That((int)LabPaintPattern.Legacy, Is.Zero);
            Assert.That((int)LabPaintPattern.Broad, Is.EqualTo(1));
            Assert.That((int)LabPaintPattern.Splatter, Is.EqualTo(2));
            Assert.That((int)LabPaintPattern.Flow, Is.EqualTo(3));
        }

        [TestCase(LabPaintPattern.Broad)]
        [TestCase(LabPaintPattern.Splatter)]
        [TestCase(LabPaintPattern.Flow)]
        public void RuntimeMaskIsIncludedAsAPlayerResource(LabPaintPattern pattern)
        {
            var mask = Resources.Load<Texture2D>("GourdPaint/Patterns/" + pattern);
            Assert.That(mask, Is.Not.Null, "The gameplay implementation must not depend on an Editor-only comparison asset.");
            Assert.That(Math.Min(mask.width, mask.height), Is.GreaterThanOrEqualTo(1024));
        }

        [TestCase(LabPaintPattern.Legacy, 0)]
        [TestCase(LabPaintPattern.Legacy, 2)]
        [TestCase(LabPaintPattern.Broad, 0)]
        [TestCase(LabPaintPattern.Broad, 1)]
        [TestCase(LabPaintPattern.Broad, 2)]
        [TestCase(LabPaintPattern.Splatter, 0)]
        [TestCase(LabPaintPattern.Splatter, 1)]
        [TestCase(LabPaintPattern.Splatter, 2)]
        [TestCase(LabPaintPattern.Flow, 0)]
        [TestCase(LabPaintPattern.Flow, 1)]
        [TestCase(LabPaintPattern.Flow, 2)]
        public void InitialAndTimedAreaRemainResolutionIndependentAndFinite(LabPaintPattern pattern, int resolution)
        {
            LabPaintGrowthSettings settings = Settings(pattern, resolution);
            settings.GrowthPercentPerSecond = 3f;
            var surface = new LabPaintSurface(settings, 200u);
            Assert.That(surface.RegisterHit(Vector3.forward, Color.red, 7), Is.True);
            int initial = (int)decimal.Floor(6m * surface.CellCount / 100m);
            Assert.That(surface.CoveredCount, Is.EqualTo(initial), "All islands share the original initial-area budget.");
            Assert.That(Pixels(surface).Count(pixel => pixel.a == 255), Is.EqualTo(initial));
            Assert.That(surface.RegisterHit(Vector3.forward, Color.blue, 9), Is.True);
            int before = surface.CoveredCount;
            surface.Advance(1d);
            Assert.That((surface.CoveredCount - before) / (double)surface.CellCount,
                Is.EqualTo(.03d).Within(1d / surface.CellCount));
            surface.Advance(1000d);
            Assert.That(surface.Coverage, Is.EqualTo(.10f).Within(2f / surface.CellCount));
            Color32[] finished = Pixels(surface);
            surface.Advance(1000000d);
            Assert.That(Pixels(surface), Is.EqualTo(finished), "Elapsed idle time cannot create uncredited paint.");
            Assert.That(finished.All(pixel => pixel.a == 0 || pixel.a == 255), Is.True);
        }

        [Test]
        public void SplatterSatellitesAreRealSeparateCountedRegionsAndRepeatGrowthStaysWithTheHitComponent()
        {
            LabPaintGrowthSettings settings = Settings(LabPaintPattern.Splatter);
            settings.RepeatPercent = 1f;
            var surface = new LabPaintSurface(settings, 200u);
            surface.RegisterHit(Vector3.forward, Color.red, 7);
            Color32[] before = Pixels(surface);
            List<HashSet<int>> components = Components(surface, before);
            Assert.That(components.Count, Is.GreaterThan(1), "The approved detached droplets must exist in the actual cell map.");
            Assert.That(surface.RegionCount, Is.EqualTo(components.Count), "A detached droplet is not logically linked across white surface.");
            Assert.That(components.Sum(component => component.Count), Is.EqualTo(surface.CoveredCount));
            Assert.That(before.Where(pixel => pixel.a != 0), Is.All.EqualTo((Color32)Color.red));
            HashSet<int> hitComponent = components.OrderBy(component => component.Count).First();
            int hitCell = hitComponent.First();
            uint revision = surface.Revision;
            int initial = surface.CoveredCount;
            surface.RegisterHit(Direction(surface, hitCell), Color.blue, 9);
            Assert.That(surface.Revision, Is.EqualTo(revision), "A repeat hit supplies growth instead of stamping again.");
            Assert.That(Pixels(surface), Is.EqualTo(before));
            var reachable = new HashSet<int>(hitComponent);
            // Ten single-cell steps must attach to the struck component. Natural merges are
            // permitted, but growth cannot start at another unconnected droplet.
            for (int tick = 0; tick < 10; tick++)
            {
                Color32[] previous = Pixels(surface);
                surface.Advance(1d / surface.CellCount);
                Color32[] after = Pixels(surface);
                var added = Enumerable.Range(0, after.Length).Where(cell => previous[cell].a == 0 && after[cell].a != 0).ToArray();
                Assert.That(added.Length, Is.EqualTo(1));
                Assert.That(Neighbors(surface, added[0]).Any(reachable.Contains), Is.True,
                    "Only the connected component struck by the repeat hit receives its growth credit.");
                reachable.Add(added[0]);
                foreach (HashSet<int> component in components)
                    if (component.Any(cell => Neighbors(surface, added[0]).Contains(cell))) reachable.UnionWith(component);
                for (int cell = 0; cell < before.Length; cell++)
                    if (before[cell].a != 0) Assert.That(after[cell], Is.EqualTo(before[cell]));
            }
            Assert.That(surface.CoveredCount, Is.EqualTo(initial + 10));
        }

        [TestCase(LabPaintPattern.Broad)]
        [TestCase(LabPaintPattern.Splatter)]
        [TestCase(LabPaintPattern.Flow)]
        public void RealMaskGeometryIsDeterministicAndSeededWithoutChangingTheArea(LabPaintPattern pattern)
        {
            var first = new LabPaintSurface(Settings(pattern), 74u);
            var same = new LabPaintSurface(Settings(pattern), 74u);
            var another = new LabPaintSurface(Settings(pattern), 75u);
            foreach (LabPaintSurface surface in new[] { first, same, another })
            {
                surface.RegisterHit(Vector3.forward, Color.red, 1);
                surface.RegisterHit(Vector3.forward, Color.blue, 2);
            }
            first.Advance(.02d); same.Advance(.01d); same.Advance(.01d);
            another.Advance(.02d);
            Assert.That(Pixels(same), Is.EqualTo(Pixels(first)));
            Assert.That(same.Revision, Is.EqualTo(first.Revision));
            Assert.That(same.PendingCoverage, Is.EqualTo(first.PendingCoverage));
            Assert.That(another.CoveredCount, Is.EqualTo(first.CoveredCount));
            Assert.That(Pixels(another), Is.Not.EqualTo(Pixels(first)));
        }

        [Test]
        public void ThreeMasksProduceDifferentActualGeometryAtTheSameImpactAndArea()
        {
            var hashes = new HashSet<string>();
            foreach (LabPaintPattern pattern in new[] { LabPaintPattern.Broad, LabPaintPattern.Splatter, LabPaintPattern.Flow })
            {
                var surface = new LabPaintSurface(Settings(pattern), 200u);
                surface.RegisterHit(Vector3.forward, Color.red, 1);
                hashes.Add(LabPaintPublisher.Hash(Pixels(surface)));
            }
            Assert.That(hashes.Count, Is.EqualTo(3));
        }

        [TestCase(LabPaintPattern.Broad, 0)]
        [TestCase(LabPaintPattern.Broad, 1)]
        [TestCase(LabPaintPattern.Broad, 2)]
        [TestCase(LabPaintPattern.Splatter, 0)]
        [TestCase(LabPaintPattern.Splatter, 1)]
        [TestCase(LabPaintPattern.Splatter, 2)]
        [TestCase(LabPaintPattern.Flow, 0)]
        [TestCase(LabPaintPattern.Flow, 1)]
        [TestCase(LabPaintPattern.Flow, 2)]
        public void OneStationaryImpactCanReachBothPolesTheBackAndTheLastCell(LabPaintPattern pattern, int resolution)
        {
            LabPaintGrowthSettings settings = Settings(pattern, resolution);
            settings.RepeatPercent = 100;
            var surface = new LabPaintSurface(settings, 175u);
            surface.RegisterHit(Vector3.forward, Color.red, 1);
            Color32[] original = Pixels(surface);
            surface.RegisterHit(Vector3.forward, Color.blue, 2);
            double beforeLast = (surface.CellCount - surface.CoveredCount - 1) / (double)surface.CellCount;
            Assert.That(surface.Advance(beforeLast), Is.EqualTo(-1d));
            Assert.That(surface.CoveredCount, Is.EqualTo(surface.CellCount - 1));
            Assert.That(surface.IsComplete, Is.False);
            surface.Advance(1d / surface.CellCount);
            Assert.That(surface.IsComplete, Is.True);
            Assert.That(surface.Coverage, Is.EqualTo(1f));
            Assert.That(surface.PendingCoverage, Is.Zero);
            Assert.That(surface.RegionCount, Is.EqualTo(1));
            Color32[] complete = Pixels(surface);
            Assert.That(complete.All(pixel => pixel.a == 255), Is.True);
            for (int cell = 0; cell < original.Length; cell++)
                if (original[cell].a != 0) Assert.That(complete[cell], Is.EqualTo(original[cell]));
        }

        [TestCase(LabPaintPattern.Broad, 0)]
        [TestCase(LabPaintPattern.Broad, 1)]
        [TestCase(LabPaintPattern.Broad, 2)]
        [TestCase(LabPaintPattern.Splatter, 0)]
        [TestCase(LabPaintPattern.Splatter, 1)]
        [TestCase(LabPaintPattern.Splatter, 2)]
        [TestCase(LabPaintPattern.Flow, 0)]
        [TestCase(LabPaintPattern.Flow, 1)]
        [TestCase(LabPaintPattern.Flow, 2)]
        public void ProjectionAtSeamAndBothPolesStillUsesOnlyItsFiniteInitialBudget(LabPaintPattern pattern, int position)
        {
            foreach (Vector3 direction in new[] { Vector3.left, Vector3.up, Vector3.down })
            {
                var surface = new LabPaintSurface(Settings(pattern, position), 215u);
                Assert.That(surface.RegisterHit(direction, Color.red, 1), Is.True);
                Assert.That(surface.CoveredCount, Is.EqualTo((int)decimal.Floor(6m * surface.CellCount / 100m)));
                Assert.That(surface.RegionCount, Is.EqualTo(Components(surface, Pixels(surface)).Count));
                Color32[] pixels = Pixels(surface);
                foreach (int cell in Enumerable.Range(0, surface.CellCount).Where(cell => pixels[cell].a != 0))
                    Assert.That(Vector3.Angle(direction, Direction(surface, cell)), Is.LessThan(85f),
                        "A six-percent impact cannot paint the opposite hemisphere as a projection artifact.");
            }
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void InvalidPatternCannotPartiallyResetExistingPaint(int value)
        {
            var surface = new LabPaintSurface(Settings(LabPaintPattern.Splatter), 200u);
            surface.RegisterHit(Vector3.forward, Color.red, 1);
            Color32[] previous = Pixels(surface);
            uint revision = surface.Revision;
            var invalid = Settings((LabPaintPattern)value, 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => surface.Reset(invalid, 999u));
            Assert.That(surface.Columns, Is.EqualTo(256));
            Assert.That(Pixels(surface), Is.EqualTo(previous));
            Assert.That(surface.Revision, Is.EqualTo(revision));
        }

        [TestCase(LabPaintPattern.Legacy)]
        [TestCase(LabPaintPattern.Broad)]
        [TestCase(LabPaintPattern.Splatter)]
        [TestCase(LabPaintPattern.Flow)]
        public void AuthoritativePatternMapReplicatesExactlyWithoutClientGrowth(LabPaintPattern pattern)
        {
            var host = new LabPaintSurface(Settings(pattern, 0), 200u);
            host.RegisterHit(Vector3.forward, LabPaintPalette.ColorFor(LabOrbElement.Fire), 1);
            var publisher = new LabPaintPublisher();
            var replica = new LabPaintSurface(Settings(LabPaintPattern.Legacy, 0), 1);
            var receiver = new LabPaintReceiver(replica);
            foreach (LabPaintFrame frame in publisher.Publish(host, "patterns", 1u, 1u).Reverse()) receiver.Receive(frame);
            Assert.That(Pixels(replica), Is.EqualTo(Pixels(host)));
            Assert.That(replica.CoveredCount, Is.EqualTo(host.CoveredCount));
            Color32[] frozen = Pixels(replica);
            Assert.That(replica.RegisterHit(Vector3.back, Color.yellow, 99), Is.False);
            replica.Advance(100d);
            Assert.That(Pixels(replica), Is.EqualTo(frozen));
            host.RegisterHit(Vector3.forward, LabPaintPalette.ColorFor(LabOrbElement.Water), 2);
            host.Advance(.02d);
            foreach (LabPaintFrame frame in publisher.Publish(host, "patterns", 1u, 2u)) receiver.Receive(frame);
            Assert.That(Pixels(replica), Is.EqualTo(Pixels(host)));
            Assert.That(replica.Coverage, Is.EqualTo(host.Coverage));
            Assert.That(replica.Revision, Is.EqualTo(host.Revision));
        }

        private static Color32[] Pixels(LabPaintSurface surface)
        { var pixels = new Color32[surface.CellCount]; surface.CopyPixels(pixels); return pixels; }

        private static Vector3 Direction(LabPaintSurface surface, int cell)
        {
            int x = cell % surface.Columns, y = cell / surface.Columns;
            float height = 1f - 2f * (y + .5f) / surface.Rows;
            float ring = Mathf.Sqrt(1f - height * height);
            float angle = ((x + .5f) / surface.Columns - .5f) * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle) * ring, height, Mathf.Sin(angle) * ring);
        }

        private static IEnumerable<int> Neighbors(LabPaintSurface surface, int cell)
        {
            int x = cell % surface.Columns, y = cell / surface.Columns;
            yield return y * surface.Columns + (x + surface.Columns - 1) % surface.Columns;
            yield return y * surface.Columns + (x + 1) % surface.Columns;
            if (y > 0) yield return cell - surface.Columns;
            if (y + 1 < surface.Rows) yield return cell + surface.Columns;
        }

        private static List<HashSet<int>> Components(LabPaintSurface surface, Color32[] pixels)
        {
            var all = new List<HashSet<int>>();
            var seen = new HashSet<int>();
            for (int start = 0; start < pixels.Length; start++)
            {
                if (pixels[start].a == 0 || seen.Contains(start)) continue;
                var component = new HashSet<int> { start };
                var queue = new Queue<int>(); queue.Enqueue(start); seen.Add(start);
                while (queue.Count > 0)
                    foreach (int next in Neighbors(surface, queue.Dequeue()))
                        if (pixels[next].a != 0 && seen.Add(next)) { component.Add(next); queue.Enqueue(next); }
                all.Add(component);
            }
            return all;
        }
    }
}
