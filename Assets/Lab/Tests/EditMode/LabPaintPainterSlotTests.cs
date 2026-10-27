using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>Authoritative paint provenance fixtures; these are not device or shader evidence.</summary>
    public sealed class LabPaintPainterSlotTests
    {
        private static LabPaintGrowthSettings Settings(LabPaintPattern pattern = LabPaintPattern.Legacy) =>
            new LabPaintGrowthSettings
            {
                Pattern = pattern, Resolution = 0, InitialPercent = .1f, RepeatPercent = .1f,
                GrowthPercentPerSecond = 100f
            };

        [TestCase(LabPaintPattern.Legacy)]
        [TestCase(LabPaintPattern.Broad)]
        [TestCase(LabPaintPattern.Splatter)]
        [TestCase(LabPaintPattern.Flow)]
        public void ActualPatternCellsIncludingDetachedSplashesCarryTheSupplyingPlayer(LabPaintPattern pattern)
        {
            LabPaintGrowthSettings settings = Settings(pattern);
            settings.InitialPercent = 6f;
            var surface = new LabPaintSurface(settings, 200u);
            Assert.That(surface.RegisterHit(Vector3.forward, Color.red, 900ul, 5), Is.True);
            Color32[] pixels = Pixels(surface);
            byte[] slots = Slots(surface);
            Assert.That(surface.CoveredCount, Is.GreaterThan(2));
            for (int cell = 0; cell < pixels.Length; cell++)
                Assert.That(slots[cell], Is.EqualTo(pixels[cell].a == 0 ? 0 : 5), "cell " + cell);
            Assert.That(surface.LastPainter, Is.EqualTo(900ul), "Statistics retain the connection ID, not the P number.");
            if (pattern == LabPaintPattern.Splatter)
                Assert.That(surface.RegionCount, Is.GreaterThan(1), "Detached counted splashes also retain provenance.");
        }

        [Test]
        public void SameColorCanBelongToDifferentPlayersWithoutReassigningTheExistingPaint()
        {
            var surface = new LabPaintSurface(Settings(), 101u);
            surface.RegisterHit(Vector3.forward, Color.red, 100ul, 1);
            Color32[] before = Pixels(surface);
            surface.RegisterHit(Vector3.forward, Color.red, 300ul, 3);
            surface.Advance(100d);
            Color32[] after = Pixels(surface);
            byte[] slots = Slots(surface);
            Assert.That(slots.Count(slot => slot == 1), Is.EqualTo(2));
            Assert.That(slots.Count(slot => slot == 3), Is.EqualTo(2));
            for (int cell = 0; cell < before.Length; cell++)
            {
                if (before[cell].a != 0) Assert.That(slots[cell], Is.EqualTo(1));
                if (after[cell].a != 0) Assert.That(after[cell], Is.EqualTo((Color32)Color.red));
            }
        }

        [Test]
        public void DifferentColorsKeepTheSamePlayerPatternAndFinalFractionSupplyOwnsBothFields()
        {
            var surface = new LabPaintSurface(Settings(), 101u);
            surface.RegisterHit(Vector3.forward, Color.red, 100ul, 4);
            Color32[] before = Pixels(surface);
            // The red supply retains .048 cells. The next whole cell consumes that fraction
            // plus blue ink and must use the completing blue supply's color AND P number.
            surface.RegisterHit(Vector3.forward, Color.blue, 900ul, 4);
            surface.Advance(100d);
            Color32[] after = Pixels(surface);
            byte[] slots = Slots(surface);
            Assert.That(after.Count(pixel => pixel.Equals((Color32)Color.red)), Is.EqualTo(2));
            Assert.That(after.Count(pixel => pixel.Equals((Color32)Color.blue)), Is.EqualTo(2));
            for (int cell = 0; cell < after.Length; cell++)
            {
                if (after[cell].a != 0) Assert.That(slots[cell], Is.EqualTo(4));
                if (before[cell].a != 0) Assert.That(after[cell], Is.EqualTo(before[cell]));
            }
            Assert.That(surface.LastPainter, Is.EqualTo(900ul));
        }

        [Test]
        public void FractionCompletingSupplyOwnsTheNewCellEvenWhenAnEarlierPlayerFundedPartOfIt()
        {
            var surface = new LabPaintSurface(Settings(), 101u);
            surface.RegisterHit(Vector3.forward, Color.red, 100ul, 1);
            Color32[] before = Pixels(surface);
            surface.RegisterHit(Vector3.forward, Color.blue, 900ul, 5);
            surface.Advance(100d);
            Color32[] after = Pixels(surface);
            byte[] slots = Slots(surface);
            int added = 0;
            for (int cell = 0; cell < before.Length; cell++)
            {
                if (before[cell].a == 0 && after[cell].a != 0)
                {
                    added++;
                    Assert.That(after[cell], Is.EqualTo((Color32)Color.blue));
                    Assert.That(slots[cell], Is.EqualTo(5));
                }
            }
            Assert.That(added, Is.EqualTo(2));
            Assert.That(surface.LastPainter, Is.EqualTo(900ul));
        }

        [Test]
        public void MeetingRegionsPreserveExistingOwnersWhileNewGrowthUsesItsOwnSupply()
        {
            LabPaintGrowthSettings settings = Settings();
            settings.RepeatPercent = 100f;
            var surface = new LabPaintSurface(settings, 77u);
            surface.RegisterHit(Vector3.forward, Color.red, 100ul, 1);
            surface.RegisterHit(Vector3.back, Color.blue, 200ul, 2);
            Color32[] before = Pixels(surface);
            byte[] ownersBefore = Slots(surface);
            Assert.That(surface.RegionCount, Is.EqualTo(2));
            surface.RegisterHit(Vector3.forward, Color.green, 500ul, 5);
            surface.Advance(2d);
            Assert.That(surface.IsComplete, Is.True);
            Assert.That(surface.RegionCount, Is.EqualTo(1));
            Color32[] after = Pixels(surface);
            byte[] ownersAfter = Slots(surface);
            for (int cell = 0; cell < before.Length; cell++)
            {
                if (before[cell].a == 0)
                {
                    Assert.That(after[cell], Is.EqualTo((Color32)Color.green));
                    Assert.That(ownersAfter[cell], Is.EqualTo(5));
                }
                else
                {
                    Assert.That(after[cell], Is.EqualTo(before[cell]));
                    Assert.That(ownersAfter[cell], Is.EqualTo(ownersBefore[cell]));
                }
            }
        }

        [TestCase(LabPaintPattern.Legacy)]
        [TestCase(LabPaintPattern.Broad)]
        [TestCase(LabPaintPattern.Splatter)]
        [TestCase(LabPaintPattern.Flow)]
        public void OwnershipDoesNotAlterPixelsGrowthClockRandomChoicesOrCompletion(LabPaintPattern pattern)
        {
            LabPaintGrowthSettings settings = Settings(pattern);
            settings.InitialPercent = 2f; settings.RepeatPercent = 100f;
            var neutral = new LabPaintSurface(settings, 55u);
            var owned = new LabPaintSurface(settings, 55u);
            neutral.RegisterHit(Vector3.forward, Color.red, 17ul);
            owned.RegisterHit(Vector3.forward, Color.red, 17ul, 4);
            neutral.RegisterHit(Vector3.forward, Color.blue, 19ul);
            owned.RegisterHit(Vector3.forward, Color.blue, 19ul, 5);
            for (int tick = 0; tick < 10; tick++)
            {
                Assert.That(owned.Advance(.1d), Is.EqualTo(neutral.Advance(.1d)));
                Assert.That(Pixels(owned), Is.EqualTo(Pixels(neutral)));
                Assert.That(owned.CoveredCount, Is.EqualTo(neutral.CoveredCount));
                Assert.That(owned.PendingCoverage, Is.EqualTo(neutral.PendingCoverage));
                Assert.That(owned.Revision, Is.EqualTo(neutral.Revision));
                Assert.That(owned.LastPainter, Is.EqualTo(neutral.LastPainter));
            }
            Assert.That(owned.IsComplete, Is.True);
            Assert.That(Slots(neutral), Is.All.EqualTo((byte)0));
        }

        [TestCase(-1)]
        [TestCase(6)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void InvalidPainterSlotCannotMutateAnyHitOrGrowthState(int slot)
        {
            var surface = new LabPaintSurface(Settings(), 23u);
            surface.RegisterHit(Vector3.forward, Color.red, 7ul, 2);
            Color32[] before = Pixels(surface); byte[] owners = Slots(surface);
            uint revision = surface.Revision;
            int hitCount = surface.PaintCount;
            double pending = surface.PendingCoverage;
            Assert.That(surface.RegisterHit(Vector3.back, Color.blue, 8ul, slot), Is.False);
            Assert.That(surface.RegisterHit(Vector3.forward, Color.blue, 8ul, slot), Is.False);
            Assert.That(Pixels(surface), Is.EqualTo(before));
            Assert.That(Slots(surface), Is.EqualTo(owners));
            Assert.That(surface.Revision, Is.EqualTo(revision));
            Assert.That(surface.PaintCount, Is.EqualTo(hitCount));
            Assert.That(surface.PendingCoverage, Is.EqualTo(pending));
            Assert.That(surface.LastPainter, Is.EqualTo(7ul));
        }

        [Test]
        public void ClearResetAndLegacyPreviewCannotLeakPreviousPlayerPatterns()
        {
            var surface = new LabPaintSurface(Settings(), 5u);
            surface.RegisterHit(Vector3.forward, Color.red, 44ul, 4);
            surface.Clear();
            Assert.That(Slots(surface), Is.All.EqualTo((byte)0));
            Assert.That(surface.Stamp(Vector3.forward, Color.red), Is.True);
            Assert.That(surface.CoveredCount, Is.GreaterThan(0));
            Assert.That(Slots(surface), Is.All.EqualTo((byte)0));
            surface.Reset(Settings(), 5u);
            surface.RegisterHit(Vector3.back, Color.blue, 55ul, 5);
            LabPaintGrowthSettings resized = Settings(); resized.Resolution = 1;
            surface.Reset(resized, 23u);
            Assert.That(surface.CellCount, Is.EqualTo(128 * 64));
            Assert.That(Slots(surface), Is.All.EqualTo((byte)0));
            Assert.That(surface.CoveredCount, Is.Zero);
            Assert.That(surface.PendingCoverage, Is.Zero);
        }

        [Test]
        public void PainterReadMethodsAreBoundedAndDoNotExposeTheBackingArray()
        {
            var surface = new LabPaintSurface(Settings(), 5u);
            surface.RegisterHit(Vector3.forward, Color.red, 44ul, 4);
            byte[] owners = Slots(surface);
            int ownedCell = Array.FindIndex(owners, slot => slot == 4);
            Assert.That(surface.GetPainterSlot(ownedCell), Is.EqualTo(4));
            owners[ownedCell] = 5;
            Assert.That(surface.GetPainterSlot(ownedCell), Is.EqualTo(4));
            Assert.Throws<ArgumentNullException>(() => surface.CopyPainterSlots(null));
            Assert.Throws<ArgumentException>(() => surface.CopyPainterSlots(new byte[1]));
            Assert.Throws<ArgumentOutOfRangeException>(() => surface.GetPainterSlot(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => surface.GetPainterSlot(surface.CellCount));
        }

        [Test]
        public void ValidReplicaCopiesColorAndOwnershipTogetherAndNeutralFixturesStaySupported()
        {
            var source = new LabPaintSurface(Settings(), 4u);
            source.RegisterHit(Vector3.forward, Color.red, 27ul, 3);
            Color32[] colors = Pixels(source); byte[] owners = Slots(source);
            var replica = new LabPaintSurface();
            Assert.That(replica.TryReplacePixels(64, 32, 80u, colors, owners, 9), Is.True);
            Assert.That(Pixels(replica), Is.EqualTo(colors));
            Assert.That(Slots(replica), Is.EqualTo(owners));
            int first = Array.FindIndex(owners, slot => slot != 0);
            owners[first] = 5; colors[first] = Color.blue;
            Assert.That(replica.GetPainterSlot(first), Is.EqualTo(3));
            Assert.That(Pixels(replica)[first], Is.EqualTo((Color32)Color.red));
            Assert.That(replica.RegisterHit(Vector3.back, Color.green, 8ul, 5), Is.False);
            Assert.That(replica.Advance(1d), Is.EqualTo(-1d));
            Assert.That(replica.TryReplacePixels(64, 32, 81u, Pixels(source), new byte[source.CellCount], 10), Is.True);
            Assert.That(Slots(replica), Is.All.EqualTo((byte)0), "Old offline color-only fixtures explicitly install neutral provenance.");
        }

        [TestCase("null")]
        [TestCase("short")]
        [TestCase("long")]
        [TestCase("above-five")]
        [TestCase("unpainted-owner")]
        [TestCase("invalid-alpha")]
        [TestCase("invalid-dimensions")]
        [TestCase("negative-hit-count")]
        public void InvalidReplicaCannotPartiallyReplaceEitherMapOrMetadata(string mutation)
        {
            var source = new LabPaintSurface(Settings(), 4u);
            source.RegisterHit(Vector3.forward, Color.red, 27ul, 3);
            Color32[] colors = Pixels(source); byte[] owners = Slots(source);
            var replica = new LabPaintSurface();
            Assert.That(replica.TryReplacePixels(64, 32, 80u, colors, owners, 9), Is.True);
            Color32[] accepted = Pixels(replica); byte[] acceptedOwners = Slots(replica);
            int painted = Array.FindIndex(owners, slot => slot != 0);
            int empty = Array.FindIndex(owners, slot => slot == 0);
            int columns = 64, rows = 32, count = 10;
            switch (mutation)
            {
                case "null": owners = null; break;
                case "short": owners = new byte[colors.Length - 1]; break;
                case "long": owners = new byte[colors.Length + 1]; break;
                case "above-five": owners[painted] = 6; break;
                case "unpainted-owner": owners[empty] = 1; break;
                case "invalid-alpha": colors[painted].a = 128; break;
                case "invalid-dimensions": columns = 32; rows = 64; break;
                case "negative-hit-count": count = -1; break;
            }
            Assert.That(replica.TryReplacePixels(columns, rows, 81u, colors, owners, count), Is.False);
            Assert.That(Pixels(replica), Is.EqualTo(accepted));
            Assert.That(Slots(replica), Is.EqualTo(acceptedOwners));
            Assert.That(replica.Columns, Is.EqualTo(64));
            Assert.That(replica.Rows, Is.EqualTo(32));
            Assert.That(replica.Revision, Is.EqualTo(80u));
            Assert.That(replica.PaintCount, Is.EqualTo(9));
            Assert.That(replica.CoveredCount, Is.EqualTo(source.CoveredCount));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void ConfirmHitUsesApprovedPNumberRatherThanTheConnectionIdentifier(int slot)
        {
            LabConfig config = PaintConfig();
            try
            {
                ulong[] players = { 901ul, 0ul, 502ul, 333ul, 84ul };
                var model = new LabModel(); model.Begin("paint-p-number", players, 0d, config);
                LabOrbState shot = CreateElementOrb(model, players[slot - 1]);
                Assert.That(model.TryLaunch(players[slot - 1], shot.id, "launch", 0d, out _, out string error), Is.True, error);
                Assert.That(model.ConfirmHit(shot.id, 0d, Vector3.forward, out _, out _, out _), Is.True);
                byte[] slots = Slots(model.PaintSurface);
                Assert.That(slots.Where(value => value != 0), Is.Not.Empty);
                Assert.That(slots.Where(value => value != 0), Is.All.EqualTo((byte)slot));
                Assert.That(model.PaintSurface.LastPainter, Is.EqualTo(players[slot - 1]));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void PassedOrbPaintBelongsToTheActualThrowerAndDuplicateImpactDoesNotReassignIt()
        {
            LabConfig config = PaintConfig();
            try
            {
                // Host connection 0 deliberately occupies P2, while the creator is P1.
                var model = new LabModel(); model.Begin("paint-passed-owner", new ulong[] { 901ul, 0ul }, 0d, config);
                LabOrbState shot = CreateElementOrb(model, 901ul);
                Assert.That(shot.originOwner, Is.EqualTo(901ul));

                Assert.That(model.TryTransferAt(901ul, shot.id, true, new Vector2(.99f, .5f), Vector2.right, "pass", 0d,
                LabEdgeCrossingKind.Swipe, out var passed, out string error), Is.True, error);
                Assert.That(passed.owner, Is.Zero);
                Assert.That(passed.originOwner, Is.EqualTo(901ul));
                Assert.That(model.TryLaunch(0ul, shot.id, "launch", 0d, out _, out error), Is.True, error);
                Assert.That(model.ConfirmHit(shot.id, 0d, Vector3.forward, out var element, out _, out _), Is.True);
                Assert.That(element, Is.EqualTo(shot.element));
                byte[] owners = Slots(model.PaintSurface); Color32[] colors = Pixels(model.PaintSurface);
                Assert.That(owners.Where(value => value != 0), Is.All.EqualTo((byte)2));
                Assert.That(colors.Where(value => value.a != 0), Is.All.EqualTo((Color32)model.PaintColorFor(element)));
                Assert.That(model.PaintSurface.LastPainter, Is.Zero, "Existing statistics still identify the real Host connection.");
                uint revision = model.PaintSurface.Revision;
                Assert.That(model.ConfirmHit(shot.id, 0d, Vector3.back, out _, out _, out _), Is.False);
                Assert.That(model.PaintSurface.Revision, Is.EqualTo(revision));
                Assert.That(Slots(model.PaintSurface), Is.EqualTo(owners));
                Assert.That(Pixels(model.PaintSurface), Is.EqualTo(colors));
                model.Tick(config.BattleDurationSeconds + 1d);
                model.Begin("paint-passed-owner", new ulong[] { 901ul, 0ul }, config.BattleDurationSeconds + 1d, config);
                Assert.That(Slots(model.PaintSurface), Is.All.EqualTo((byte)0));
                Assert.That(model.PaintSurface.CoveredCount, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        private static LabConfig PaintConfig()
        {
            var config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            values[5] = .001f; values[9] = 100f; values[74] = 5f;
            for (int i = 39; i <= 44; i++) values[i] = 0f;
            values[42] = 100f; // A secret box supplies deterministic element orbs for the model fixture.
            values[46] = values[47] = 0f;
            values[79] = 0f; values[205] = 0f;
            values[104] = 0f; values[105] = .1f; values[134] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            return config;
        }

        private static LabOrbState CreateElementOrb(LabModel model, ulong owner)
        {
            Assert.That(model.TryGenerate(owner, "box", 0d, out var box, out string error), Is.True, error);
            Assert.That(box.kind, Is.EqualTo(LabOrbKind.SecretBox));
            Assert.That(model.TryUseItemAt(owner, box.id, "open", 0d, new Vector2(.5f, .5f), out _, out error), Is.True, error);
            return model.Snapshot(0d).orbs.First(orb => orb.owner == owner && orb.kind == LabOrbKind.Combined);
        }

        private static Color32[] Pixels(LabPaintSurface surface)
        {
            var pixels = new Color32[surface.CellCount]; surface.CopyPixels(pixels); return pixels;
        }
        private static byte[] Slots(LabPaintSurface surface)
        {
            var owners = new byte[surface.CellCount]; surface.CopyPainterSlots(owners); return owners;
        }
    }
}
