using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Host cell provenance and model-copy fixtures; not transport or rendered-pixel evidence.</summary>
    public sealed class LabPaintContributionTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float OneCellShare = .390625f;
        private static LabPaintGrowthSettings Settings(LabPaintPattern pattern = LabPaintPattern.Legacy) =>
            new LabPaintGrowthSettings
            {
                Resolution = 0, Pattern = pattern, InitialPercent = .125f, RepeatPercent = 0f,
                GrowthPercentPerSecond = 100f, DryingEnabled = false
            };

        [TestCase(LabPaintPattern.Legacy)]
        [TestCase(LabPaintPattern.Broad)]
        [TestCase(LabPaintPattern.Splatter)]
        [TestCase(LabPaintPattern.Flow)]
        public void InitialAndTimedCellsUseTheirActualFundingOwnerIncludingHostZero(LabPaintPattern pattern)
        {
            var settings = Settings(pattern);
            settings.InitialPercent = 6f; settings.RepeatPercent = 3f;
            var surface = new LabPaintSurface(settings, 200u);
            Assert.That(surface.RegisterHit(Vector3.forward, Color.red, 0ul, 1), Is.True);
            int initial = surface.CoveredCount;
            Assert.That(initial, Is.GreaterThan(0));
            Assert.That(surface.NewlyPaintedCellsBy(0ul), Is.EqualTo(initial));
            Assert.That(surface.NewlyPaintedCellsBy(900ul), Is.Zero);
            Assert.That(surface.RegisterHit(Vector3.forward, Color.blue, 900ul, 5), Is.True);
            Assert.That(surface.CoveredCount, Is.EqualTo(initial), "A repeated impact only supplies growth.");
            Assert.That(surface.NewlyPaintedCellsBy(900ul), Is.Zero);
            surface.Advance(100d);
            Assert.That(surface.CoveredCount, Is.GreaterThan(initial));
            byte[] slots = Slots(surface);
            Assert.That(surface.NewlyPaintedCellsBy(0ul), Is.EqualTo(slots.Count(slot => slot == 1)));
            Assert.That(surface.NewlyPaintedCellsBy(900ul), Is.EqualTo(slots.Count(slot => slot == 5)));
            Assert.That(surface.NewlyPaintedCellsBy(0ul) + surface.NewlyPaintedCellsBy(900ul),
                Is.EqualTo(surface.CoveredCount), "Every actually added cell earns exactly one owner's credit.");
            if (pattern == LabPaintPattern.Splatter)
                Assert.That(initial, Is.GreaterThan(2), "The real mask and its initial splashes are included.");
        }

        [Test]
        public void ACellCompletedAcrossFractionalSuppliesBelongsOnlyToTheFinalSupplier()
        {
            var settings = Settings();
            settings.InitialPercent = settings.RepeatPercent = .1f;
            var surface = new LabPaintSurface(settings, 101u);
            surface.RegisterHit(Vector3.forward, Color.red, 100ul, 1);
            Assert.That(surface.NewlyPaintedCellsBy(100ul), Is.EqualTo(2));
            // The first owner has .048 of a cell left. Blue provides the remaining
            // fraction and the final Ink owns that whole cell, just like its color/P slot.
            surface.RegisterHit(Vector3.forward, Color.blue, 900ul, 5);
            surface.Advance(100d);
            Assert.That(surface.CoveredCount, Is.EqualTo(4));
            Assert.That(surface.NewlyPaintedCellsBy(100ul), Is.EqualTo(2));
            Assert.That(surface.NewlyPaintedCellsBy(900ul), Is.EqualTo(2));
            Assert.That(Slots(surface).Count(slot => slot == 5), Is.EqualTo(2));
            Assert.That(surface.LastPainter, Is.EqualTo(900ul));
        }

        [Test]
        public void RepeatedZeroGrowthHitsAndUnknownOwnersCannotClaimAlreadyPaintedCells()
        {
            var settings = Settings(); settings.GrowthPercentPerSecond = 0f;
            var surface = new LabPaintSurface(settings, 56u);
            surface.RegisterHit(Vector3.forward, Color.red, 7ul, 1, OneCellShare);
            Color32[] before = Pixels(surface);
            for (int i = 0; i < 4; i++)
                Assert.That(surface.RegisterHit(Vector3.forward, Color.blue, 19ul, 2), Is.True);
            surface.Advance(100d);
            Assert.That(surface.CoveredCount, Is.EqualTo(1));
            Assert.That(Pixels(surface), Is.EqualTo(before));
            Assert.That(surface.NewlyPaintedCellsBy(7ul), Is.EqualTo(1));
            Assert.That(surface.NewlyPaintedCellsBy(19ul), Is.Zero);
            Assert.That(surface.NewlyPaintedCellsBy(ulong.MaxValue), Is.Zero);
        }

        [Test]
        public void DryingRetainsCreditAndTheSameDriedCellCanEarnNewCreditOnRepaint()
        {
            var settings = Settings();
            settings.GrowthPercentPerSecond = 0f; settings.DryingEnabled = true;
            settings.DryingDelaySeconds = 1f; settings.DryingPercentPerSecond = 1000f;
            var surface = new LabPaintSurface(settings, 57u);
            surface.RegisterHit(Vector3.forward, Color.red, 7ul, 1, OneCellShare);
            Assert.That(surface.NewlyPaintedCellsBy(7ul), Is.EqualTo(1));
            surface.Advance(2d);
            Assert.That(surface.CoveredCount, Is.Zero);
            Assert.That(surface.NewlyPaintedCellsBy(7ul), Is.EqualTo(1));
            surface.RegisterHit(Vector3.forward, Color.blue, 19ul, 2, OneCellShare);
            Assert.That(surface.CoveredCount, Is.EqualTo(1));
            Assert.That(surface.NewlyPaintedCellsBy(7ul), Is.EqualTo(1));
            Assert.That(surface.NewlyPaintedCellsBy(19ul), Is.EqualTo(1));
            surface.Advance(2d);
            Assert.That(surface.CoveredCount, Is.Zero);
            surface.RegisterHit(Vector3.forward, Color.green, 7ul, 1, OneCellShare);
            Assert.That(surface.CoveredCount, Is.EqualTo(1));
            Assert.That(surface.NewlyPaintedCellsBy(7ul), Is.EqualTo(2));
            Assert.That(surface.NewlyPaintedCellsBy(19ul), Is.EqualTo(1));
        }

        [Test]
        public void ClearAndValidResetRetireRoundCreditWhileInvalidResetLeavesItUntouched()
        {
            var surface = new LabPaintSurface(Settings(), 58u);
            surface.RegisterHit(Vector3.forward, Color.red, 7ul, 1, OneCellShare);
            var invalid = Settings(); invalid.Resolution = 99;
            Assert.Throws<ArgumentOutOfRangeException>(() => surface.Reset(invalid, 59u));
            Assert.That(surface.NewlyPaintedCellsBy(7ul), Is.EqualTo(1));
            surface.Clear();
            Assert.That(surface.NewlyPaintedCellsBy(7ul), Is.Zero);
            surface.RegisterHit(Vector3.forward, Color.blue, 19ul, 2, OneCellShare);
            Assert.That(surface.NewlyPaintedCellsBy(19ul), Is.EqualTo(1));
            surface.Reset(Settings(), 59u);
            Assert.That(surface.NewlyPaintedCellsBy(7ul), Is.Zero);
            Assert.That(surface.NewlyPaintedCellsBy(19ul), Is.Zero);
            Assert.That(surface.CoveredCount, Is.Zero);
        }

        [Test]
        public void ReplicaInstallAndOfflinePreviewNeverInferCumulativeCreditFromVisiblePaint()
        {
            var source = new LabPaintSurface(Settings(), 60u);
            source.RegisterHit(Vector3.forward, Color.red, 7ul, 1, OneCellShare);
            var replica = new LabPaintSurface(Settings(), 61u);
            Assert.That(replica.TryReplacePixels(source.Columns, source.Rows, source.Revision,
                Pixels(source), Slots(source), source.PaintCount), Is.True);
            Assert.That(replica.CoveredCount, Is.EqualTo(1));
            Assert.That(replica.NewlyPaintedCellsBy(7ul), Is.Zero);
            Assert.That(replica.TryCopyReplicaFrom(source, source.PaintCount), Is.True);
            Assert.That(replica.NewlyPaintedCellsBy(7ul), Is.Zero);
            // Reusing an old authoritative surface as a read-only copy must not expose
            // its stale round statistic as if the received pixels rebuilt that history.
            Assert.That(source.TryCopyReplicaFrom(replica, replica.PaintCount), Is.True);
            Assert.That(source.NewlyPaintedCellsBy(7ul), Is.Zero);
            var preview = new LabPaintSurface(Settings(), 62u);
            Assert.That(preview.Stamp(Vector3.forward, Color.red), Is.True);
            Assert.That(preview.CoveredCount, Is.GreaterThan(0));
            Assert.That(preview.NewlyPaintedCellsBy(0ul), Is.Zero);
        }

        [Test]
        public void QueryingCreditAndPartitioningGrowthDoNotChangePixelsBudgetsOrGameRandomState()
        {
            var settings = Settings(); settings.InitialPercent = 2f; settings.RepeatPercent = 37f;
            settings.GrowthPercentPerSecond = 6f;
            var whole = new LabPaintSurface(settings, 729u);
            var pieces = new LabPaintSurface(settings, 729u);
            foreach (var surface in new[] { whole, pieces })
            {
                surface.RegisterHit(Vector3.forward, Color.red, 7ul, 1);
                surface.RegisterHit(Vector3.forward, Color.blue, 19ul, 2);
            }
            whole.Advance(6d);
            for (int i = 0; i < 60; i++)
            {
                long before = pieces.NewlyPaintedCellsBy(7ul) + pieces.NewlyPaintedCellsBy(19ul);
                pieces.Advance(.1d);
                Assert.That(pieces.NewlyPaintedCellsBy(7ul) + pieces.NewlyPaintedCellsBy(19ul),
                    Is.GreaterThanOrEqualTo(before));
            }
            Assert.That(Pixels(pieces), Is.EqualTo(Pixels(whole)));
            Assert.That(Slots(pieces), Is.EqualTo(Slots(whole)));
            Assert.That(pieces.CoveredCount, Is.EqualTo(whole.CoveredCount));
            Assert.That(pieces.PendingCoverage, Is.EqualTo(whole.PendingCoverage));
            Assert.That(pieces.Revision, Is.EqualTo(whole.Revision));
            Assert.That(pieces.LastPainter, Is.EqualTo(whole.LastPainter));
            Assert.That(typeof(LabPaintSurface).GetField("randomState", Private).GetValue(pieces),
                Is.EqualTo(typeof(LabPaintSurface).GetField("randomState", Private).GetValue(whole)));
            Assert.That(pieces.NewlyPaintedCellsBy(7ul), Is.EqualTo(whole.NewlyPaintedCellsBy(7ul)));
            Assert.That(pieces.NewlyPaintedCellsBy(19ul), Is.EqualTo(whole.NewlyPaintedCellsBy(19ul)));
        }

        [Test]
        public void ModelPlayerCopiesAndSnapshotsReadTheSurfaceLedgerWithoutMutatingOtherStatistics()
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                var model = new LabModel(); model.BeginSolo("paint-contribution", 7ul, 0d, config);
                model.PaintSurface.Reset(Settings(), 63u);
                Hit(model, "normal", false);
                Hit(model, "five-colors", true);
                long earned = model.PaintSurface.NewlyPaintedCellsBy(7ul);
                Assert.That(earned, Is.GreaterThan(0));
                Assert.That(earned, Is.EqualTo(model.PaintSurface.CoveredCount),
                    "All five color supplies retain the actual shooter, not a color-based owner.");
                var players = (Dictionary<ulong, LabPlayerState>)typeof(LabModel).GetField("players", Private).GetValue(model);
                LabPlayerState ledger = players[7ul];
                ledger.paintedCells = 17; ledger.stolenCells = 19;
                LabPlayerState copied = model.CopyPlayers().Single();
                Assert.That(copied.paintedCellsTotal, Is.EqualTo(earned));
                Assert.That(copied.paintedCells, Is.EqualTo(17));
                Assert.That(copied.stolenCells, Is.EqualTo(19));
                Assert.That(copied.damageTotal, Is.EqualTo(ledger.damageTotal));
                Assert.That(copied.damageTotal, Is.GreaterThan(0));
                Assert.That(ledger.paintedCellsTotal, Is.Zero, "Copying statistics cannot write back into the action ledger.");
                copied.paintedCellsTotal = long.MaxValue;
                Assert.That(model.CopyPlayers().Single().paintedCellsTotal, Is.EqualTo(earned));
                Assert.That(model.Snapshot(0d).players.Single().paintedCellsTotal, Is.EqualTo(earned));
                // Begin rejects a still-playing round. End this fixture through the
                // real timeout path before verifying that a fresh round retires credit.
                double nextRoundAt = config.BattleDurationSeconds + 1d;
                model.Tick(nextRoundAt);
                Assert.That(model.Phase, Is.EqualTo(LabPhase.Defeat));
                model.BeginSolo("paint-contribution-next-round", 7ul, nextRoundAt, config);
                Assert.That(model.CopyPlayers().Single().paintedCellsTotal, Is.Zero);
                Assert.That(model.Snapshot(nextRoundAt).players.Single().paintedCellsTotal, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        private static void Hit(LabModel model, string id, bool fiveColor)
        {
            var orbs = (Dictionary<string, LabOrbState>)typeof(LabModel).GetField("orbs", Private).GetValue(model);
            orbs.Add(id, new LabOrbState { id = id, owner = 7ul, kind = LabOrbKind.Combined,
                element = fiveColor ? LabOrbElement.None : LabOrbElement.Fire, fiveColor = fiveColor,
                inFlight = true, createdAt = 0d });
            Assert.That(model.ConfirmHit(id, 0d, fiveColor ? Vector3.back : Vector3.forward,
                out _, out _, out _), Is.True);
        }
        private static Color32[] Pixels(LabPaintSurface surface)
        { var result = new Color32[surface.CellCount]; surface.CopyPixels(result); return result; }
        private static byte[] Slots(LabPaintSurface surface)
        { var result = new byte[surface.CellCount]; surface.CopyPainterSlots(result); return result; }
    }
}
