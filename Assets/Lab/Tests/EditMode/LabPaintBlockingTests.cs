using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Host surface admission/ledger tests, not device or LAN verification.</summary>
    public sealed class LabPaintBlockingTests
    {
        private const float OneCellShare = .390625f;
        private static LabPaintGrowthSettings Settings() => new LabPaintGrowthSettings
        {
            Resolution = 0, Pattern = LabPaintPattern.Legacy, Style = 1,
            InitialPercent = 3.125f, RepeatPercent = 3.125f,
            GrowthPercentPerSecond = 3.125f, DryingEnabled = false
        };

        [Test]
        public void BlockedImpactDoesNotSupplyInkStampsCountersOrNearbyDryingProtection()
        {
            var settings = Settings(); settings.InitialPercent = .125f; settings.RepeatPercent = 0f;
            settings.DryingEnabled = true; settings.DryingDelaySeconds = 1f;
            settings.DryingPercentPerSecond = 1000f;
            var surface = new LabPaintSurface(settings, 332);
            Assert.That(surface.RegisterHit(Vector3.forward, Color.red, 1, 1, OneCellShare), Is.True);
            surface.Advance(.75d);
            Color32[] before = Pixels(surface);
            uint revision = surface.Revision, visual = surface.VisualVersion;
            int paints = surface.PaintCount;
            surface.SetPaintBlocker((owner, _) => owner == 1);
            Assert.That(surface.RegisterHit(Vector3.forward, Color.blue, 1, 1), Is.False);
            Assert.That(Pixels(surface), Is.EqualTo(before));
            Assert.That(surface.Revision, Is.EqualTo(revision));
            Assert.That(surface.VisualVersion, Is.EqualTo(visual));
            Assert.That(surface.PaintCount, Is.EqualTo(paints));
            Assert.That(surface.PendingCoverage, Is.Zero);
            surface.Advance(.5d);
            Assert.That(surface.CoveredCount, Is.Zero, "A blocked hit cannot refresh the seed's drying grace.");
        }

        [Test]
        public void AnotherPlayerCanPaintWhileTheAffectedPlayerCannotAndOldPaintIsUntouched()
        {
            var surface = new LabPaintSurface(Settings(), 333);
            surface.RegisterHit(Vector3.forward, Color.red, 1, 1);
            Color32[] before = Pixels(surface);
            surface.SetPaintBlocker((owner, _) => owner == 1);
            Assert.That(surface.RegisterHit(Vector3.forward, Color.yellow, 1, 1), Is.False);
            Assert.That(surface.RegisterHit(Vector3.back, Color.blue, 2, 2), Is.True);
            Color32[] after = Pixels(surface);
            Assert.That(surface.CoveredCount, Is.EqualTo(128));
            for (int i = 0; i < before.Length; i++)
                if (before[i].a != 0)
                {
                    Assert.That(after[i], Is.EqualTo(before[i]));
                    Assert.That(surface.GetPainterSlot(i), Is.EqualTo(1));
                }
            Assert.That(surface.CountPainterSlots()[2], Is.EqualTo(64));
        }

        [TestCase(LabPaintPattern.Broad)]
        [TestCase(LabPaintPattern.Splatter)]
        [TestCase(LabPaintPattern.Flow)]
        public void InitialSplashClipsOnlyOriginalBlockedCellsWithoutInventingReplacementSeeds(LabPaintPattern pattern)
        {
            var settings = Settings(); settings.Pattern = pattern; settings.InitialPercent = 6.25f;
            var baseline = new LabPaintSurface(settings, 334);
            var blocked = new LabPaintSurface(settings, 334);
            baseline.RegisterHit(Vector3.forward, Color.green, 1, 1);
            blocked.SetPaintBlocker((owner, direction) => owner == 1 && direction.x > .06f);
            Assert.That(blocked.RegisterHit(Vector3.forward, Color.green, 1, 1), Is.True);
            Color32[] original = Pixels(baseline), clipped = Pixels(blocked);
            int removed = 0;
            for (int i = 0; i < original.Length; i++)
            {
                bool forbidden = Direction(blocked, i).x > .06f;
                if (original[i].a != 0 && forbidden) removed++;
                Assert.That(clipped[i].a != 0, Is.EqualTo(original[i].a != 0 && !forbidden),
                    "Clipping may remove an original satellite, but cannot move it to a different cell.");
            }
            Assert.That(removed, Is.GreaterThan(0));
            Assert.That(blocked.CoveredCount + blocked.PendingCoverage * blocked.CellCount,
                Is.EqualTo(baseline.CoveredCount + baseline.PendingCoverage * baseline.CellCount).Within(.000001d));
            blocked.Advance(3d);
            Color32[] after = Pixels(blocked);
            Assert.That(Enumerable.Range(0, after.Length).All(i => Direction(blocked, i).x <= .06f || after[i].a == 0), Is.True);
        }

        [Test]
        public void BlockedOldestOwnerCannotStallLaterOwnersInTheSameConnectedComponent()
        {
            var surface = new LabPaintSurface(Settings(), 335);
            surface.RegisterHit(Vector3.forward, Color.red, 1, 1);
            surface.RegisterHit(Vector3.forward, Color.blue, 1, 1);
            surface.RegisterHit(Vector3.forward, Color.green, 2, 2);
            surface.SetPaintBlocker((owner, _) => owner == 1);
            surface.Advance(1d);
            Assert.That(surface.CoveredCount, Is.EqualTo(128));
            Assert.That(surface.CountPainterSlots()[2], Is.EqualTo(64));
            Assert.That(Pixels(surface).Count(pixel => pixel.Equals((Color32)Color.blue)), Is.Zero);
            Assert.That(surface.PendingCoverage * surface.CellCount, Is.EqualTo(64d).Within(.000001d));
            surface.SetPaintBlocker(null);
            surface.Advance(1d);
            Assert.That(surface.CoveredCount, Is.EqualTo(192));
            Assert.That(surface.CountPainterSlots()[1], Is.EqualTo(128));
            Assert.That(surface.CountPainterSlots()[2], Is.EqualTo(64));
            Assert.That(surface.PendingCoverage, Is.Zero);
        }

        [Test]
        public void ABlockedIntervalBanksPigmentButNeverBanksTimeForAnUnblockBurst()
        {
            var surface = new LabPaintSurface(Settings(), 336);
            surface.RegisterHit(Vector3.forward, Color.red, 1, 1);
            surface.RegisterHit(Vector3.forward, Color.blue, 1, 1);
            bool blocked = true;
            surface.SetPaintBlocker((owner, _) => blocked && owner == 1);
            surface.Advance(1000d);
            Assert.That(surface.CoveredCount, Is.EqualTo(64));
            Assert.That(surface.PendingCoverage * surface.CellCount, Is.EqualTo(64d).Within(.000001d));
            blocked = false; // Same delegate: dynamic group expiry must also be safe.
            surface.Advance(1d / 64d);
            Assert.That(surface.CoveredCount, Is.EqualTo(65));
            surface.Advance(1d);
            Assert.That(surface.CoveredCount, Is.EqualTo(128));
            Assert.That(surface.PendingCoverage, Is.Zero);
        }

        [Test]
        public void FractionalSuppliesKeepTheFinalFundingOwnersColorAndCanCompleteOneCell()
        {
            var settings = Settings(); settings.InitialPercent = .125f;
            settings.RepeatPercent = .125f; // 2.56 cells before the exact binary share below.
            var surface = new LabPaintSurface(settings, 337);
            surface.RegisterHit(Vector3.forward, Color.red, 1, 1, OneCellShare);
            // Seven significant digits survive the float-to-decimal settings boundary.
            surface.RegisterHit(Vector3.forward, Color.blue, 1, 1, .1953125f);
            surface.RegisterHit(Vector3.forward, Color.green, 2, 2, .1953125f);
            surface.SetPaintBlocker((owner, _) => owner == 1);
            surface.Advance(1d);
            Assert.That(surface.CoveredCount, Is.EqualTo(2));
            Assert.That(surface.CountPainterSlots()[1], Is.EqualTo(1));
            Assert.That(surface.CountPainterSlots()[2], Is.EqualTo(1));
            Assert.That(Pixels(surface).Count(pixel => pixel.Equals((Color32)Color.green)), Is.EqualTo(1));
            Assert.That(surface.PendingCoverage, Is.Zero);
        }

        [Test]
        public void ABlockedAdjacentRingCannotBeCrossedByCreatingADistantGrowthSeed()
        {
            var settings = Settings(); settings.InitialPercent = .125f; settings.RepeatPercent = 50f;
            var surface = new LabPaintSurface(settings, 338);
            Vector3 seed = Direction(surface, 16 * surface.Columns + 48);
            surface.RegisterHit(seed, Color.red, 1, 1, OneCellShare);
            surface.RegisterHit(seed, Color.blue, 1, 1);
            int[] ring = { 16 * surface.Columns + 47, 16 * surface.Columns + 49,
                15 * surface.Columns + 48, 17 * surface.Columns + 48 };
            surface.SetPaintBlocker((owner, direction) => owner == 1
                && ring.Any(cell => Vector3.Dot(direction, Direction(surface, cell)) > .999999f));
            surface.Advance(1000d);
            Assert.That(surface.CoveredCount, Is.EqualTo(1));
            Assert.That(surface.PendingCoverage * surface.CellCount, Is.EqualTo(1024d).Within(.000001d));
            surface.SetPaintBlocker(null);
            surface.Advance(20d);
            Assert.That(surface.CoveredCount, Is.EqualTo(1025));
            Assert.That(surface.PendingCoverage, Is.Zero);
        }

        [Test]
        public void LastBlockedCellCanBePaintedAfterExpiryWithoutAnotherDirectHit()
        {
            var settings = Settings(); settings.InitialPercent = 100f;
            settings.GrowthPercentPerSecond = 1000f;
            var surface = new LabPaintSurface(settings, 339);
            Vector3 last = Direction(surface, 0);
            surface.SetPaintBlocker((owner, direction) => owner == 1 && Vector3.Dot(direction, last) > .999999f);
            Assert.That(surface.RegisterHit(Vector3.forward, Color.red, 1, 1), Is.True);
            surface.Advance(1000d);
            Assert.That(surface.CoveredCount, Is.EqualTo(surface.CellCount - 1));
            Assert.That(surface.IsComplete, Is.False);
            Assert.That(surface.PendingCoverage * surface.CellCount, Is.EqualTo(1d).Within(.000001d));
            surface.SetPaintBlocker(null);
            surface.Advance(1d);
            Assert.That(surface.IsComplete, Is.True);
            Assert.That(surface.PendingCoverage, Is.Zero);
        }

        [TestCase(LabPaintPattern.Legacy)]
        [TestCase(LabPaintPattern.Splatter)]
        public void ADisabledPolicyKeepsTheExistingPixelAndPainterSequence(LabPaintPattern pattern)
        {
            var settings = Settings(); settings.Pattern = pattern;
            var baseline = new LabPaintSurface(settings, 340);
            var disabled = new LabPaintSurface(settings, 340);
            disabled.SetPaintBlocker(null);
            foreach (LabPaintSurface surface in new[] { baseline, disabled })
            {
                surface.RegisterHit(Vector3.forward, Color.red, 1, 1);
                surface.RegisterHit(Vector3.forward, Color.blue, 2, 2);
                surface.RegisterHit(Vector3.back, Color.green, 3, 3);
                surface.Advance(2d);
            }
            Assert.That(Pixels(disabled), Is.EqualTo(Pixels(baseline)));
            var expected = new byte[baseline.CellCount]; var actual = new byte[disabled.CellCount];
            baseline.CopyPainterSlots(expected); disabled.CopyPainterSlots(actual);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(disabled.CopyVisualStamps(), Is.EqualTo(baseline.CopyVisualStamps()));
        }

        [Test]
        public void ClearRemovesThePreviousRoundsAdmissionPolicy()
        {
            var surface = new LabPaintSurface(Settings(), 341);
            surface.SetPaintBlocker((_, __) => true);
            Assert.That(surface.RegisterHit(Vector3.forward, Color.red, 1, 1), Is.False);
            surface.Clear();
            Assert.That(surface.RegisterHit(Vector3.forward, Color.red, 1, 1), Is.True);
        }

        [Test]
        public void SurfaceQueryUsesCommittedCellsAndRejectsInvalidDirectionsWithoutMutation()
        {
            var surface = new LabPaintSurface(Settings(), 343);
            surface.RegisterHit(Vector3.forward, Color.red, 1, 1);
            uint revision = surface.Revision;
            Assert.That(surface.IsPaintedAt(Vector3.forward), Is.True);
            Assert.That(surface.IsPaintedAt(Vector3.forward * 5f), Is.True);
            Assert.That(surface.IsPaintedAt(Vector3.back), Is.False);
            Assert.That(surface.IsPaintedAt(Vector3.zero), Is.False);
            Assert.That(surface.IsPaintedAt(new Vector3(float.NaN, 1f, 0f)), Is.False);
            Assert.That(surface.IsPaintedAt(new Vector3(float.PositiveInfinity, 1f, 0f)), Is.False);
            Assert.That(surface.IsPaintedAt(new Vector3(float.MaxValue, 1f, 0f)), Is.False);
            Assert.That(surface.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void HitQueryIncludesBlockedCellCentreEvenWhenTheExactImpactIsOutsideTheMask()
        {
            var surface = new LabPaintSurface(Settings(), 344);
            Vector3 impact = Vector3.forward;
            Vector3 centre = Direction(surface, surface.Rows / 2 * surface.Columns + surface.Columns * 3 / 4);
            Assert.That(impact.x, Is.GreaterThanOrEqualTo(-.02f));
            Assert.That(centre.x, Is.LessThan(-.02f), "The near-boundary impact quantizes to a blocked centre.");
            surface.SetPaintBlocker((owner, direction) => owner == 1 && direction.x < -.02f);
            uint revision = surface.Revision, visual = surface.VisualVersion;
            Assert.That(surface.WouldBlockHit(1, impact), Is.True);
            Assert.That(surface.WouldBlockHit(2, impact), Is.False);
            Assert.That(surface.WouldBlockHit(1, Vector3.zero), Is.False);
            Assert.That(surface.WouldBlockHit(1, new Vector3(float.NaN, 0f, 1f)), Is.False);
            Assert.That(surface.RegisterHit(impact, Color.red, 1, 1), Is.False);
            Assert.That(surface.CoveredCount, Is.Zero);
            Assert.That(surface.PaintCount, Is.Zero);
            Assert.That(surface.PendingCoverage, Is.Zero);
            Assert.That(surface.Revision, Is.EqualTo(revision));
            Assert.That(surface.VisualVersion, Is.EqualTo(visual));
        }

        private static Color32[] Pixels(LabPaintSurface surface)
        { var pixels = new Color32[surface.CellCount]; surface.CopyPixels(pixels); return pixels; }
        private static Vector3 Direction(LabPaintSurface surface, int cell)
        {
            int x = cell % surface.Columns, y = cell / surface.Columns;
            float vertical = 1f - 2f * (y + .5f) / surface.Rows;
            float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - vertical * vertical));
            float angle = ((x + .5f) / surface.Columns - .5f) * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle) * ring, vertical, Mathf.Sin(angle) * ring);
        }
    }
}
