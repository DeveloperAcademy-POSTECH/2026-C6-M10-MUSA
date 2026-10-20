using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Authoritative surface-model fixtures; these are not live-session or device checks.</summary>
    public sealed class LabPaintDryingTests
    {
        // These finite values also survive C# float-to-decimal conversion without rounding:
        // at resolution 0, .125 percent * this share funds exactly one equal-area cell.
        private const float OneCellShare = .390625f;

        private static LabPaintGrowthSettings Settings() => new LabPaintGrowthSettings
        {
            Resolution = 0, Pattern = LabPaintPattern.Legacy, Style = 1,
            InitialPercent = .125f, RepeatPercent = 0f, GrowthPercentPerSecond = 0f,
            DryingEnabled = true, DryingDelaySeconds = 1f,
            DryingPercentPerSecond = 1000f, NearbyHitRadiusDegrees = 20f
        };

        [Test]
        public void DisabledDryingRetainsTheExistingAccumulatedPaintAfterLongIdle()
        {
            LabPaintGrowthSettings settings = Settings(); settings.DryingEnabled = false;
            var surface = new LabPaintSurface(settings, 21);
            Seed(surface, 32, 16, Color.red, 1);
            Seed(surface, 34, 16, Color.blue, 2);
            Color32[] before = Pixels(surface); byte[] owners = Owners(surface);
            uint revision = surface.Revision;
            surface.Advance(1000d);
            Assert.That(Pixels(surface), Is.EqualTo(before));
            Assert.That(Owners(surface), Is.EqualTo(owners));
            Assert.That(surface.Revision, Is.EqualTo(revision));
            Assert.That(surface.CoveredCount, Is.EqualTo(2));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void GracePeriodDoesNotEarnDeletionCreditAndRateUsesTotalSurfaceArea(int resolution)
        {
            LabPaintGrowthSettings settings = Settings(); settings.Resolution = resolution;
            settings.InitialPercent = 12.5f; settings.DryingPercentPerSecond = 3.125f;
            var surface = new LabPaintSurface(settings, 45);
            Assert.That(surface.RegisterHit(Vector3.forward, Color.red, 1, 1), Is.True);
            int initial = surface.CoveredCount;
            Color32[] before = Pixels(surface);
            surface.Advance(1d);
            Assert.That(Pixels(surface), Is.EqualTo(before), "The grace interval cannot accumulate a future deletion burst.");
            surface.Advance(.5d);
            Assert.That(initial - surface.CoveredCount, Is.EqualTo(surface.CellCount / 64),
                "Half a second at 3.125 percentage points per second removes 1.5625 percent of the whole surface.");
            AssertCountsAndTransparentOwners(surface);
        }

        [Test]
        public void ErosionRemovesOneExposedCellAtATimeWithoutTreatingColorSeamsAsHoles()
        {
            LabPaintGrowthSettings settings = Settings(); settings.DryingPercentPerSecond = .09765625f;
            var surface = new LabPaintSurface(settings, 97);
            for (int y = 13; y <= 19; y++)
                for (int x = 29; x <= 35; x++)
                    Seed(surface, x, y, (x + y) % 2 == 0 ? Color.red : Color.blue,
                        (x + y) % 2 == 0 ? 1 : 2);
            Assert.That(surface.CoveredCount, Is.EqualTo(49));
            int paints = surface.PaintCount;
            surface.Advance(1d);
            for (int step = 0; step < 20; step++)
            {
                Color32[] before = Pixels(surface); byte[] owners = Owners(surface);
                uint revision = surface.Revision;
                surface.Advance(.5d);
                Color32[] after = Pixels(surface);
                int[] erased = Enumerable.Range(0, before.Length)
                    .Where(i => before[i].a != 0 && after[i].a == 0).ToArray();
                Assert.That(erased.Length, Is.EqualTo(1), "The two-cell-per-second fixture removes one cell in each half-second step.");
                Assert.That(Neighbors(surface, erased[0]).Any(i => before[i].a == 0), Is.True,
                    "A color or P-number seam inside paint is not an exposed empty-surface boundary.");
                Assert.That(after[erased[0]], Is.EqualTo(default(Color32)));
                Assert.That(surface.GetPainterSlot(erased[0]), Is.Zero);
                for (int i = 0; i < after.Length; i++)
                    if (after[i].a != 0)
                    {
                        Assert.That(after[i], Is.EqualTo(before[i]));
                        Assert.That(surface.GetPainterSlot(i), Is.EqualTo(owners[i]));
                    }
                Assert.That(surface.Revision, Is.EqualTo(revision + 1u));
                Assert.That(surface.CoveredCount, Is.EqualTo(48 - step));
                Assert.That(surface.PaintCount, Is.EqualTo(paints), "Drying is not a new impact.");
            }
            AssertCountsAndTransparentOwners(surface);
        }

        [Test]
        public void NearbyHitProtectsAnotherColorAndDisconnectedPatchWithoutProtectingTheBack()
        {
            // Combo rewards change paint supply, not the nearby-hit protection radius or provenance.
            foreach (float share in new[] { 1f, 1.5f, 3f })
            {
                var surface = new LabPaintSurface(Settings(), 64);
                Seed(surface, 32, 16, Color.red, 1);
                Seed(surface, 34, 16, Color.blue, 2);
                Seed(surface, 0, 16, Color.green, 3);
                Assert.That(surface.RegionCount, Is.EqualTo(3));
                surface.Advance(.75d);
                Assert.That(surface.RegisterHit(Direction(surface, 32, 16), Color.yellow, 4, 4, share), Is.True);
                surface.Advance(.5d);
                Color32[] after = Pixels(surface);
                Assert.That(after[Index(surface, 32, 16)], Is.EqualTo((Color32)Color.red));
                Assert.That(after[Index(surface, 34, 16)], Is.EqualTo((Color32)Color.blue));
                Assert.That(surface.GetPainterSlot(Index(surface, 34, 16)), Is.EqualTo(2));
                Assert.That(after[Index(surface, 0, 16)].a, Is.Zero,
                    "A hit in front must not protect the geometrically opposite side of the gourd.");
                Assert.That(surface.CoveredCount, Is.EqualTo(2));
                Assert.That(surface.PendingCoverage, Is.Zero);
            }
        }

        [Test]
        public void ProtectionUsesSurfaceAngleAndCrossesTheLongitudeSeam()
        {
            var surface = new LabPaintSurface(Settings(), 65);
            Seed(surface, 0, 16, Color.red, 1);
            Seed(surface, 63, 16, Color.blue, 2);
            Seed(surface, 3, 16, Color.green, 3);
            Seed(surface, 4, 16, Color.yellow, 4);
            surface.Advance(.75d);
            Assert.That(surface.RegisterHit(Direction(surface, 0, 16), Color.white, 5, 5), Is.True);
            surface.Advance(.5d);
            Color32[] after = Pixels(surface);
            Assert.That(after[Index(surface, 63, 16)], Is.EqualTo((Color32)Color.blue),
                "Wrapped U neighbors remain physically close on the gourd.");
            Assert.That(after[Index(surface, 3, 16)], Is.EqualTo((Color32)Color.green),
                "Three columns at the equator lie inside the 20 degree surface radius.");
            Assert.That(after[Index(surface, 4, 16)].a, Is.Zero,
                "Four columns at the equator lie outside the 20 degree surface radius.");
            Assert.That(surface.CoveredCount, Is.EqualTo(3));
        }

        [Test]
        public void AZeroGrowthRepeatStopsLocalDryingButDoesNotRestoreAlreadyRemovedPaint()
        {
            LabPaintGrowthSettings settings = Settings(); settings.DryingPercentPerSecond = .09765625f;
            var surface = new LabPaintSurface(settings, 66);
            Seed(surface, 32, 16, Color.red, 1);
            Seed(surface, 40, 16, Color.blue, 2);
            surface.Advance(1.5d);
            Assert.That(surface.CoveredCount, Is.EqualTo(1));
            // Pick a remaining cell rather than asking a repeat hit to repaint an empty one.
            Color32[] survivors = Pixels(surface);
            int remaining = Array.FindIndex(survivors, p => p.a != 0);
            Assert.That(remaining, Is.GreaterThanOrEqualTo(0));
            int initial = surface.CoveredCount;
            Color32 old = survivors[remaining]; byte owner = surface.GetPainterSlot(remaining);
            Assert.That(surface.RegisterHit(Direction(surface, remaining), Color.yellow, 4, 4), Is.True);
            surface.Advance(.75d);
            Assert.That(surface.CoveredCount, Is.EqualTo(initial));
            Assert.That(Pixels(surface)[remaining], Is.EqualTo(old));
            Assert.That(surface.GetPainterSlot(remaining), Is.EqualTo(owner));
            Assert.That(surface.PendingCoverage, Is.Zero);
            surface.Advance(1.5d);
            Assert.That(surface.CoveredCount, Is.Zero);
        }

        [TestCase("zero-share")] [TestCase("negative-share")] [TestCase("large-share")]
        [TestCase("nan-share")] [TestCase("negative-slot")] [TestCase("large-slot")]
        [TestCase("zero-direction")] [TestCase("nan-direction")] [TestCase("transparent-color")]
        public void RejectedHitsCannotRefreshNearbyProtection(string invalid)
        {
            var surface = new LabPaintSurface(Settings(), 67);
            Seed(surface, 32, 16, Color.red, 1);
            surface.Advance(.75d);
            Vector3 direction = Direction(surface, 32, 16); Color color = Color.blue;
            float share = 1f; int slot = 2;
            switch (invalid)
            {
                case "zero-share": share = 0f; break;
                case "negative-share": share = -1f; break;
                case "large-share": share = 3.001f; break;
                case "nan-share": share = float.NaN; break;
                case "negative-slot": slot = -1; break;
                case "large-slot": slot = 6; break;
                case "zero-direction": direction = Vector3.zero; break;
                case "nan-direction": direction = new Vector3(float.NaN, 0, 1); break;
                case "transparent-color": color = Color.clear; break;
                default: Assert.Fail("Unknown invalid input fixture."); break;
            }
            int count = surface.PaintCount;
            Assert.That(surface.RegisterHit(direction, color, 2, slot, share), Is.False);
            Assert.That(surface.PaintCount, Is.EqualTo(count));
            surface.Advance(.5d);
            Assert.That(surface.CoveredCount, Is.Zero);
            Assert.That(surface.RegionCount, Is.Zero);
            AssertCountsAndTransparentOwners(surface);
        }

        [Test]
        public void RemovedPaintCanBePaintedAgainWithNewProvenanceAndANewGraceInterval()
        {
            var surface = new LabPaintSurface(Settings(), 68);
            Seed(surface, 32, 16, Color.red, 1);
            surface.Advance(2d);
            Assert.That(surface.CoveredCount, Is.Zero);
            Assert.That(surface.RegionCount, Is.Zero);
            Seed(surface, 32, 16, Color.blue, 2);
            surface.Advance(.75d);
            Assert.That(surface.CoveredCount, Is.EqualTo(1));
            Assert.That(surface.RegionCount, Is.EqualTo(1));
            Assert.That(Pixels(surface)[Index(surface, 32, 16)], Is.EqualTo((Color32)Color.blue));
            Assert.That(surface.GetPainterSlot(Index(surface, 32, 16)), Is.EqualTo(2));
            surface.Advance(.5d);
            Assert.That(surface.CoveredCount, Is.Zero);
            Assert.That(surface.PendingCoverage, Is.Zero);
        }

        [Test]
        public void NewlyGrownCellsReceiveGraceWithoutRefreshingTheOldConnectedPaint()
        {
            LabPaintGrowthSettings settings = Settings(); settings.InitialPercent = 6.25f;
            settings.RepeatPercent = 25f; settings.GrowthPercentPerSecond = 6.25f;
            settings.DryingPercentPerSecond = 6.25f;
            var surface = new LabPaintSurface(settings, 69);
            Assert.That(surface.RegisterHit(Vector3.forward, Color.red, 1, 1), Is.True);
            int originalRed = surface.CoveredCount;
            Assert.That(surface.RegisterHit(Vector3.forward, Color.blue, 2, 2), Is.True);
            surface.Advance(.5d); Color32[] earlier = Pixels(surface);
            surface.Advance(.25d); Color32[] later = Pixels(surface);
            int[] young = Enumerable.Range(0, later.Length)
                .Where(i => earlier[i].a == 0 && later[i].Equals((Color32)Color.blue)).ToArray();
            Assert.That(young.Length, Is.GreaterThan(0));
            surface.Advance(.75d);
            Color32[] after = Pixels(surface);
            Assert.That(after.Count(p => p.Equals((Color32)Color.red)), Is.LessThan(originalRed),
                "Automatic neighboring growth must not refresh the old component's dry timer.");
            Assert.That(young.All(i => after[i].Equals((Color32)Color.blue) && surface.GetPainterSlot(i) == 2), Is.True,
                "Cells born between .5 and .75 seconds have not reached their one-second grace limit at 1.5 seconds.");
        }

        [TestCase(0f)] [TestCase(4f)]
        public void TimePartitionKeepsDryingGrowthOwnershipAndRemainingBudgetIdentical(float growthRate)
        {
            LabPaintGrowthSettings settings = Settings(); settings.InitialPercent = 6.25f;
            settings.RepeatPercent = 12.5f; settings.GrowthPercentPerSecond = growthRate;
            settings.DryingDelaySeconds = .5f; settings.DryingPercentPerSecond = 3.125f;
            var whole = new LabPaintSurface(settings, 70);
            var pieces = new LabPaintSurface(settings, 70);
            foreach (LabPaintSurface surface in new[] { whole, pieces })
            {
                surface.RegisterHit(Vector3.forward, Color.red, 1, 1);
                surface.RegisterHit(Vector3.forward, Color.blue, 2, 2);
            }
            whole.Advance(4d);
            for (int i = 0; i < 64; i++) pieces.Advance(.0625d);
            Assert.That(Pixels(pieces), Is.EqualTo(Pixels(whole)));
            Assert.That(Owners(pieces), Is.EqualTo(Owners(whole)));
            Assert.That(pieces.Revision, Is.EqualTo(whole.Revision));
            Assert.That(pieces.PendingCoverage, Is.EqualTo(whole.PendingCoverage));
            Assert.That(pieces.RegionCount, Is.EqualTo(whole.RegionCount));
            Assert.That(pieces.CoveredCount, Is.EqualTo(whole.CoveredCount));
            AssertCountsAndTransparentOwners(whole);
        }

        [Test]
        public void ErodingAnUnprotectedNeckSplitsComponentsWithoutDuplicatingTheirPendingInk()
        {
            LabPaintGrowthSettings settings = Settings(); settings.RepeatPercent = 3.125f;
            var surface = new LabPaintSurface(settings, 71);
            for (int y = 14; y <= 18; y++)
                for (int x = 22; x <= 40; x++)
                    if (x <= 26 || x >= 36 || y == 16)
                        Seed(surface, x, y, x <= 31 ? Color.red : Color.blue, x <= 31 ? 1 : 2);
            Assert.That(surface.RegionCount, Is.EqualTo(1));
            Assert.That(surface.RegisterHit(Direction(surface, 24, 16), Color.green, 3, 3), Is.True);
            surface.Advance(.75d);
            // Zero share is invalid, so use valid full repeats and account for their finite budget.
            Assert.That(surface.RegisterHit(Direction(surface, 24, 16), Color.green, 3, 3), Is.True);
            Assert.That(surface.RegisterHit(Direction(surface, 38, 16), Color.yellow, 4, 4), Is.True);
            double pending = surface.PendingCoverage;
            surface.Advance(.5d);
            Assert.That(surface.RegionCount, Is.EqualTo(2));
            Assert.That(Pixels(surface)[Index(surface, 31, 16)].a, Is.Zero);
            Assert.That(Pixels(surface)[Index(surface, 24, 16)], Is.EqualTo((Color32)Color.red));
            Assert.That(Pixels(surface)[Index(surface, 38, 16)], Is.EqualTo((Color32)Color.blue));
            Assert.That(surface.PendingCoverage, Is.EqualTo(pending).Within(1e-12d),
                "Splitting a painted component must assign each queued supply once, without cloning or losing it while either side survives.");
            AssertCountsAndTransparentOwners(surface);
        }

        [Test]
        public void RebuildingErodedComponentsDoesNotStarveTheLaterRegionsGrowthTurn()
        {
            LabPaintGrowthSettings settings = Settings(); settings.InitialPercent = 12.5f;
            settings.RepeatPercent = 12.5f; settings.GrowthPercentPerSecond = .5f;
            settings.DryingDelaySeconds = 0f; settings.DryingPercentPerSecond = .5f;
            var surface = new LabPaintSurface(settings, 821);
            Assert.That(surface.RegisterHit(Vector3.forward, Color.red, 1, 1), Is.True);
            Assert.That(surface.RegisterHit(Vector3.back, Color.green, 2, 2), Is.True);
            Assert.That(surface.RegionCount, Is.EqualTo(2));
            Assert.That(surface.RegisterHit(Vector3.forward, Color.blue, 3, 3), Is.True);
            Assert.That(surface.RegisterHit(Vector3.back, Color.yellow, 4, 4), Is.True);
            surface.Advance(.25d);
            Color32[] warm = Pixels(surface);
            int blueBefore = warm.Count(p => p.Equals((Color32)Color.blue));
            int yellowBefore = warm.Count(p => p.Equals((Color32)Color.yellow));
            surface.Advance(1d);
            Color32[] after = Pixels(surface);
            Assert.That(after.Count(p => p.Equals((Color32)Color.blue)), Is.GreaterThan(blueBefore),
                "The first queued region must receive some of the global growth allowance.");
            Assert.That(after.Count(p => p.Equals((Color32)Color.yellow)), Is.GreaterThan(yellowBefore),
                "Topology reconstruction on every erosion tick must not restart round robin at the first region.");
            Assert.That(surface.RegionCount, Is.GreaterThanOrEqualTo(2),
                "Boundary erosion may detach small islands without joining the initially separate painted regions.");
            AssertCountsAndTransparentOwners(surface);
        }

        [Test]
        public void ErasingTheLastCellDropsItsUnusableGrowthBudgetInsteadOfGivingItToANewSeed()
        {
            LabPaintGrowthSettings settings = Settings(); settings.RepeatPercent = 25f;
            var surface = new LabPaintSurface(settings, 72);
            Seed(surface, 32, 16, Color.red, 1);
            surface.RegisterHit(Direction(surface, 32, 16), Color.blue, 2, 2);
            Assert.That(surface.PendingCoverage, Is.GreaterThan(0d));
            surface.Advance(2d);
            Assert.That(surface.CoveredCount, Is.Zero);
            Assert.That(surface.RegionCount, Is.Zero);
            Assert.That(surface.PendingCoverage, Is.Zero);
            Seed(surface, 32, 16, Color.green, 3);
            Assert.That(surface.CoveredCount, Is.EqualTo(1));
            Assert.That(surface.PendingCoverage, Is.Zero);
        }

        [Test]
        public void CompletionFreezesTheFinishedGourdAndReplicaNeverRunsLocalDrying()
        {
            LabPaintGrowthSettings fullSettings = Settings(); fullSettings.InitialPercent = 100f;
            var finished = new LabPaintSurface(fullSettings, 73);
            Assert.That(finished.RegisterHit(Vector3.forward, Color.red, 1, 1), Is.True);
            Color32[] completed = Pixels(finished); uint revision = finished.Revision;
            Assert.That(finished.Advance(100d), Is.Zero);
            Assert.That(finished.IsComplete, Is.True);
            Assert.That(Pixels(finished), Is.EqualTo(completed));
            Assert.That(finished.Revision, Is.EqualTo(revision));

            var host = new LabPaintSurface(Settings(), 73);
            var replica = new LabPaintSurface(Settings(), 73);
            Seed(host, 32, 16, Color.blue, 2);
            Assert.That(replica.TryReplacePixels(host.Columns, host.Rows, host.Revision,
                Pixels(host), Owners(host), host.PaintCount), Is.True);
            replica.Advance(100d);
            Assert.That(replica.CoveredCount, Is.EqualTo(1), "Only the Host's cell updates may remove replica paint.");
            host.Advance(2d);
            Assert.That(replica.TryReplacePixels(host.Columns, host.Rows, host.Revision,
                Pixels(host), Owners(host), host.PaintCount), Is.True);
            Assert.That(replica.CoveredCount, Is.Zero);
            Assert.That(Pixels(replica), Is.EqualTo(Pixels(host)));
            Assert.That(Owners(replica), Is.EqualTo(Owners(host)));
        }

        [Test]
        public void ClearResetsDryingTimeAndOldProtectionBeforeANewRound()
        {
            var surface = new LabPaintSurface(Settings(), 74);
            Seed(surface, 32, 16, Color.red, 1);
            surface.Advance(100d);
            surface.Clear();
            Seed(surface, 32, 16, Color.blue, 2);
            surface.Advance(.75d);
            Assert.That(surface.CoveredCount, Is.EqualTo(1));
            surface.Advance(.5d);
            Assert.That(surface.CoveredCount, Is.Zero);
            Assert.That(surface.PendingCoverage, Is.Zero);
        }

        private static void Seed(LabPaintSurface surface, int x, int y, Color color, int slot)
        {
            int before = surface.CoveredCount;
            Assert.That(surface.RegisterHit(Direction(surface, x, y), color, (ulong)slot, slot, OneCellShare), Is.True);
            Assert.That(surface.CoveredCount, Is.EqualTo(before + 1), "A seed fixture must add exactly its requested cell.");
        }

        private static int Index(LabPaintSurface surface, int x, int y) => y * surface.Columns + x;
        private static Vector3 Direction(LabPaintSurface surface, int x, int y) => Direction(surface, Index(surface, x, y));
        private static Vector3 Direction(LabPaintSurface surface, int cell)
        {
            float y = 1f - (cell / surface.Columns + .5f) * 2f / surface.Rows;
            float angle = ((cell % surface.Columns + .5f) / surface.Columns - .5f) * Mathf.PI * 2f;
            float ring = Mathf.Sqrt(1f - y * y);
            return new Vector3(Mathf.Cos(angle) * ring, y, Mathf.Sin(angle) * ring);
        }

        private static IEnumerable<int> Neighbors(LabPaintSurface surface, int cell)
        {
            int x = cell % surface.Columns, y = cell / surface.Columns;
            yield return y * surface.Columns + (x + surface.Columns - 1) % surface.Columns;
            yield return y * surface.Columns + (x + 1) % surface.Columns;
            if (y > 0) yield return cell - surface.Columns;
            if (y + 1 < surface.Rows) yield return cell + surface.Columns;
        }

        private static Color32[] Pixels(LabPaintSurface surface)
        { var values = new Color32[surface.CellCount]; surface.CopyPixels(values); return values; }
        private static byte[] Owners(LabPaintSurface surface)
        { var values = new byte[surface.CellCount]; surface.CopyPainterSlots(values); return values; }
        private static void AssertCountsAndTransparentOwners(LabPaintSurface surface)
        {
            Color32[] pixels = Pixels(surface); byte[] owners = Owners(surface);
            Assert.That(pixels.Count(p => p.a != 0), Is.EqualTo(surface.CoveredCount));
            for (int i = 0; i < pixels.Length; i++)
                if (pixels[i].a == 0)
                {
                    Assert.That(pixels[i], Is.EqualTo(default(Color32)));
                    Assert.That(owners[i], Is.Zero);
                }
        }
    }
}
