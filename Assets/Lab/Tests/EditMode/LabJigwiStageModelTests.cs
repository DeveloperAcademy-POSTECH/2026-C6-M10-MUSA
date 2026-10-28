using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Stage A/B use real Host clocks and normal generation/launch/hit paths.</summary>
    public sealed class LabJigwiStageModelTests
    {
        private LabConfig config;
        private int request;
        private static readonly ulong[] Players = { 0ul, 20ul };

        [SetUp]
        public void Create()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            values[3] = 1000f; values[4] = 100f;
            values[5] = 1f; values[6] = 0f; values[7] = 1f;
            values[9] = 100f;
            values[74] = 2f; // Two real drums give both possible A targets the normal generation input.
            values[39] = values[40] = 50f;
            for (int i = 41; i <= 44; i++) values[i] = 0f;
            values[207] = values[208] = 0f;
            values[228] = 1f; values[229] = 1f; values[230] = 7f;
            values[231] = 18f; values[232] = 30f; values[233] = 1f; values[234] = 3f; values[235] = 2f;
            Apply(values);
        }
        [TearDown] public void Dispose() => Object.DestroyImmediate(config);
        private void Apply(float[] values) => Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
        private LabModel Room(LabStageYokai stage = LabStageYokai.Jigwi, string id = "jigwi-stage")
        {
            var room = new LabModel();
            room.Begin(id, Players, 0d, config, stage);
            return room;
        }
        private static LabJigwiStageSnapshot State(LabModel room, double now) => room.Snapshot(now).jigwiStage;
        private static void AssertSurfacePolicyDisabled(LabModel room, double now)
        {
            // Screen-policy tests must never silently activate the retained mesh-root migration code.
            Assert.That(State(room, now).enabled, Is.False);
            Assert.That(State(room, now).groups, Is.Empty);
        }
        private static LabJigwiABSnapshot ScreenState(LabModel room, double now) => room.Snapshot(now).jigwiAB;
        private void Extinguish(LabModel room, LabJigwiABRound round, ulong owner, string targetId, double now)
        {
            var water = Combined(room, owner, now);
            Assert.That(water.element, Is.EqualTo(LabOrbElement.Water));
            Launch(room, owner, water, now);
            Assert.That(room.ConfirmJigwiABFireHit(water.id, round.id, targetId, now, out bool off), Is.True);
            Assert.That(off, Is.True);
        }
        private void ExtinguishAll(LabModel room, LabJigwiABRound round, double now)
        {
            foreach (var player in round.players)
                foreach (var fire in player.targets) Extinguish(room, round, player.playerId, fire.id, now);
        }
        private string NextRequest() => "stage-" + ++request;
        private LabOrbState Combined(LabModel room, ulong owner, double now)
        {
            LabOrbState yin = null, yang = null;
            for (int i = 0; i < 24 && (yin == null || yang == null); i++)
            {
                Assert.That(room.TryGenerate(owner, NextRequest(), now, out var orb, out string reason), Is.True, reason);
                if (orb.kind == LabOrbKind.Yin) yin = orb;
                else if (orb.kind == LabOrbKind.Yang) yang = orb;
            }
            Assert.That(yin, Is.Not.Null); Assert.That(yang, Is.Not.Null);
            Assert.That(room.TryCombineAt(owner, yin.id, yang.id, NextRequest(), now, new Vector2(.5f, .5f),
                Vector2.zero, out var combined, out string combineReason), Is.True, combineReason);
            return combined;
        }
        private void Launch(LabModel room, ulong owner, LabOrbState combined, double now)
        {
            Assert.That(room.TryLaunch(owner, combined.id, NextRequest(), now, out _, out string reason), Is.True, reason);
        }

        [TestCase(LabStageYokai.None, false)]
        [TestCase(LabStageYokai.Gumiho, false)]
        [TestCase(LabStageYokai.Yaguang, false)]
        [TestCase(LabStageYokai.Jigwi, true)]
        public void ScreenPolicyIsScopedToJigwiAndHasNoFreeWaterSupply(LabStageYokai stage, bool enabled)
        {
            // #341 replaces surface roots with screen A/B, while preserving stage scope and no free supplies.
            var room = Room(stage);
            var snapshot = room.Snapshot(11d);
            Assert.That(room.JigwiABEnabled, Is.EqualTo(enabled));
            Assert.That(snapshot.jigwiAB.enabled, Is.EqualTo(enabled));
            Assert.That(snapshot.jigwiStage.enabled, Is.False);
            AssertSurfacePolicyDisabled(room, 11d);
            if (enabled)
            {
                Assert.That(room.Snapshot(11d).remaining, Is.EqualTo(109d));
                Assert.That(snapshot.jigwi, Is.Empty);
                Assert.That(snapshot.orbs.Any(orb => orb.jigwiSupply || orb.jigwiGenerationBurn), Is.False);
            }
        }

        [Test]
        public void PersonalFireHasOneSecondWarningTenSecondLimitAndNoSurfaceSpread()
        {
            // The bounded-lifetime assertion now covers A's 10-second window, not retired 7-second mesh patches.
            var room = Room();
            Assert.That(ScreenState(room, 9.99d).rounds, Is.Empty);
            var warning = ScreenState(room, 10d).rounds.Single();
            Assert.That(warning.Warning(10d), Is.True);
            Assert.That(warning.Active(10d), Is.False);
            var active = ScreenState(room, 11d).rounds.Single();
            Assert.That(active.kind, Is.EqualTo(LabJigwiABKind.PersonalBurn));
            Assert.That(active.Active(11d), Is.True);
            Assert.That(room.JigwiABRecoveryMultiplier(active.targetPlayer, 11d), Is.EqualTo(4f));
            Assert.That(ScreenState(room, 20.99d).rounds.Single().Active(20.99d), Is.True);
            var end = ScreenState(room, 21d).rounds.Single();
            Assert.That(end.resolution, Is.EqualTo(LabJigwiABResolution.Expired));
            Assert.That(end.resolvedAt, Is.EqualTo(21d));
            Assert.That(room.Snapshot(21d).jigwiStage.groups, Is.Empty);
        }

        [Test]
        public void OneLateTickRecordsEightSlotsAndSkipsSlotsBusyWithTheGroupChallenge()
        {
            // Busy slots are recorded once; B now persists until joint success or the 100-second cutoff.
            var room = Room();
            var state = ScreenState(room, 98d);
            Assert.That(state.rounds, Has.Length.EqualTo(8));
            Assert.That(state.rounds.Select(item => item.slot), Is.EqualTo(Enumerable.Range(0, 8)));
            Assert.That(state.rounds.Select(item => item.id).Distinct().Count(), Is.EqualTo(8));
            Assert.That(state.rounds[0].resolution, Is.EqualTo(LabJigwiABResolution.Expired));
            Assert.That(state.rounds[1].kind, Is.EqualTo(LabJigwiABKind.GroupFire));
            Assert.That(state.rounds[1].Active(98d), Is.True);
            Assert.That(state.rounds.Skip(2).All(item => item.resolution == LabJigwiABResolution.Skipped), Is.True);
            Assert.That(ScreenState(room, 98d).rounds.Length, Is.EqualTo(8));
            Assert.That(LabJigwiABNetworkValidation.Valid(state, room.Snapshot(98d).players), Is.True);
        }

        [Test]
        public void ScreenFireLeavesPaintOpenAndOnlyThePersonalTargetGetsBurnDeadlines()
        {
            // Surface paint blocking was removed; personal A now marks orb expiry for one owner only.
            var room = Room();
            var active = ScreenState(room, 11d).rounds.Single();
            ulong other = Players.Single(player => player != active.targetPlayer);
            Assert.That(room.JigwiStagePaintBlocked(active.targetPlayer, Vector3.forward), Is.False);
            Assert.That(room.JigwiStagePaintBlocked(other, Vector3.forward), Is.False);
            Assert.That(room.TryGenerate(active.targetPlayer, NextRequest(), 11d, out var burning, out string reason), Is.True, reason);
            Assert.That(room.TryGenerate(other, NextRequest(), 11d, out var ordinary, out reason), Is.True, reason);
            Assert.That(burning.jigwiABBurnAt, Is.EqualTo(16d));
            Assert.That(ordinary.jigwiABBurnAt, Is.EqualTo(-1d));
            var before = room.Snapshot(11d);
            var ended = room.Snapshot(21d);
            Assert.That(ended.players.Select(p => p.personalHp), Is.EqualTo(before.players.Select(p => p.personalHp)));
            Assert.That(ended.jigwi, Is.Empty, "A expiry does not invoke the legacy overheat experiment.");
            Assert.That(ended.jigwiAB.rounds.Single().resolution, Is.EqualTo(LabJigwiABResolution.Expired));
        }

        [Test]
        public void GroupChallengeUsesEveryParticipantsOwnThreeTargetsInsteadOfHelperSwaps()
        {
            // B replaced helper/victim pairs with the same all-player challenge on every screen.
            var room = Room();
            var group = ScreenState(room, 31d).rounds.Single(item => item.slot == 1);
            Assert.That(group.kind, Is.EqualTo(LabJigwiABKind.GroupFire));
            Assert.That(group.players.Select(player => player.playerId), Is.EquivalentTo(Players));
            Assert.That(group.players.All(player => player.targets.Length == 3), Is.True);
            Assert.That(group.players.SelectMany(player => player.targets).Select(fire => fire.id).Distinct().Count(), Is.EqualTo(6));
            Assert.That(group.players.All(player => player.targets.All(fire => fire.lit)), Is.True);
            Assert.That(room.Snapshot(31d).orbs.Any(orb => orb.jigwiSupply), Is.False);
        }

        [Test]
        public void EachOwnersTargetsRemainIndependentUntilEveryoneHasExtinguishedAllOfThem()
        {
            // Preserve owner isolation and independent progress, then require joint success under the new B rule.
            var room = Room();
            var group = ScreenState(room, 31d).rounds.Single(item => item.slot == 1);
            var first = group.players[0]; var second = group.players[1];
            var wrongOwnerShot = Combined(room, first.playerId, 31d);
            Launch(room, first.playerId, wrongOwnerShot, 31d);
            Assert.That(room.ConfirmJigwiABFireHit(wrongOwnerShot.id, group.id, second.targets[0].id, 31d, out _), Is.False);
            foreach (var fire in first.targets) Extinguish(room, group, first.playerId, fire.id, 31d);
            var halfway = ScreenState(room, 31d).rounds.Single(item => item.id == group.id);
            Assert.That(halfway.players[0].targets.All(fire => !fire.lit), Is.True);
            Assert.That(halfway.players[1].targets.All(fire => fire.lit), Is.True);
            Assert.That(halfway.resolution, Is.EqualTo(LabJigwiABResolution.None));
            foreach (var fire in second.targets) Extinguish(room, group, second.playerId, fire.id, 31d);
            var completed = ScreenState(room, 31d).rounds.Single(item => item.id == group.id);
            Assert.That(completed.resolution, Is.EqualTo(LabJigwiABResolution.Completed));
            Assert.That(completed.players.All(player => player.targets.All(fire => !fire.lit)), Is.True);
        }

        [TestCase(LabOrbKind.Relay, 41)]
        public void UnlockedRelayHitsCannotCountTowardPersonalCombinedRecovery(LabOrbKind kind, int chanceField)
        {
            // The original non-paintable item exclusion now protects A's Combined-only recovery counter.
            float[] values = config.CaptureDeveloperValues();
            for (int i = 39; i <= 44; i++) values[i] = 0f;
            values[chanceField] = 100f; values[46] = values[47] = 0f; Apply(values);
            var room = Room();
            var active = ScreenState(room, 11d).rounds.Single();
            Assert.That(room.TryGenerate(active.targetPlayer, NextRequest(), 11d, out var item, out string reason), Is.True, reason);
            Assert.That(item.kind, Is.EqualTo(kind)); Assert.That(item.lockSwipes, Is.Zero);
            Assert.That(item.element, Is.EqualTo(LabOrbElement.None));
            Launch(room, active.targetPlayer, item, 11d);
            Assert.That(room.ConfirmHit(item.id, 11d, Vector3.forward, out _, out _, out _), Is.True);
            var after = ScreenState(room, 11d).rounds.Single();
            Assert.That(after.hitCount, Is.Zero); Assert.That(after.Active(11d), Is.True);
            Assert.That(room.PaintSurface.PaintCount, Is.Zero);
            Assert.That(room.PaintSurface.CoveredCount, Is.Zero);
        }


        [Test]
        public void UnlockedBombMustOpenIntoTwoCombinedOrbsBeforeItCanCountOnePersonalHit()
        {
            // Preserve main's bomb policy; the resulting Combined uses normal A hit counting, not a surface root.
            float[] values = config.CaptureDeveloperValues();
            for (int i = 39; i <= 44; i++) values[i] = 0f;
            values[44] = 100f; values[46] = values[47] = 0f; Apply(values);
            var room = Room(); var active = ScreenState(room, 11d).rounds.Single();
            Assert.That(room.TryGenerate(active.targetPlayer, NextRequest(), 11d, out var bomb, out string reason), Is.True, reason);
            Assert.That(bomb.kind, Is.EqualTo(LabOrbKind.Bomb));
            Assert.That(room.TryLaunch(active.targetPlayer, bomb.id, NextRequest(), 11d, out _, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("RAW_CANNOT_ATTACK"));
            Assert.That(room.ConfirmHit(bomb.id, 11d, Vector3.forward, out _, out _, out _), Is.False);
            Assert.That(room.TryGetMovableState(bomb.id, out _), Is.True);
            Assert.That(room.TryUseItemAt(active.targetPlayer, bomb.id, NextRequest(), 11d,
                new Vector2(.5f, .5f), out _, out reason), Is.True, reason);
            Assert.That(reason, Is.EqualTo("BOMB_OPENED"));
            var attacks = room.Snapshot(11d).orbs.Where(orb => orb.kind == LabOrbKind.Combined).ToArray();
            Assert.That(attacks, Has.Length.EqualTo(2));
            Assert.That(attacks.All(orb => orb.jigwiABBurnAt == 16d), Is.True);
            Launch(room, active.targetPlayer, attacks[0], 11d);
            Assert.That(room.ConfirmHit(attacks[0].id, 11d, Vector3.forward, out _, out _, out _), Is.True);
            Assert.That(ScreenState(room, 11d).rounds.Single().hitCount, Is.EqualTo(1));
            Assert.That(ScreenState(room, 11d).rounds.Single().Active(11d), Is.True);
            Assert.That(room.PaintSurface.CoveredCount, Is.GreaterThan(0));
        }

        [Test]
        public void ThreeRealCombinedBossHitsConsumeRewardAndPaintOnceEachThenCompleteA()
        {
            // Replace one root hit with A's three real boss hits; each retains its original one-time transaction checks.
            var room = Room(); var active = ScreenState(room, 11d).rounds.Single();
            for (int i = 0; i < 3; i++)
            {
                var combined = Combined(room, active.targetPlayer, 11d);
                Launch(room, active.targetPlayer, combined, 11d);
                int paints = room.PaintSurface.PaintCount;
                var before = room.Snapshot(11d).players.Single(player => player.id == active.targetPlayer);
                Assert.That(room.ConfirmHit(combined.id, 11d, Vector3.forward, out _, out _, out _), Is.True);
                var after = room.Snapshot(11d).players.Single(player => player.id == active.targetPlayer);
                Assert.That(room.PaintSurface.PaintCount, Is.EqualTo(paints + 1));
                Assert.That(after.hitTotal, Is.EqualTo(before.hitTotal + 1));
                Assert.That(after.stamina, Is.EqualTo(Mathf.Min(config.StaminaMax, before.stamina + config.HitRecovery)));
                Assert.That(room.ConfirmHit(combined.id, 11d, Vector3.forward, out _, out _, out _), Is.False);
                Assert.That(room.PaintSurface.PaintCount, Is.EqualTo(paints + 1));
                Assert.That(ScreenState(room, 11d).rounds.Single().hitCount, Is.EqualTo(i + 1));
            }
            Assert.That(ScreenState(room, 11d).rounds.Single().resolution, Is.EqualTo(LabJigwiABResolution.Completed));
        }

        [Test]
        public void ScreenFireCollisionConsumesWithoutNormalPaintOrBossHitRewards()
        {
            // B's screen flame is now the separate fire-collision path, with no boss hit rewards or paint.
            var room = Room(); var group = ScreenState(room, 31d).rounds.Single(item => item.slot == 1);
            ulong owner = group.players[0].playerId;
            var water = Combined(room, owner, 31d); Launch(room, owner, water, 31d);
            var before = room.Snapshot(31d).players.Single(player => player.id == owner);
            int paints = room.PaintSurface.PaintCount;
            Assert.That(room.ConfirmJigwiABFireHit(water.id, group.id, group.players[0].targets[0].id,
                31d, out bool off), Is.True);
            Assert.That(off, Is.True);
            var after = room.Snapshot(31d).players.Single(player => player.id == owner);
            Assert.That(room.PaintSurface.PaintCount, Is.EqualTo(paints));
            Assert.That(after.hitTotal, Is.EqualTo(before.hitTotal));
            Assert.That(after.throwTotal, Is.EqualTo(before.throwTotal));
            Assert.That(after.stamina, Is.EqualTo(before.stamina));
            Assert.That(room.TryGetMovableState(water.id, out _), Is.False);
            Assert.That(ScreenState(room, 31d).rounds.Single(item => item.slot == 1).Active(31d), Is.True);
        }

        [Test]
        public void ExtinguishingOneScreenFlameDoesNotClearItsSiblingTargets()
        {
            // Independent progress now belongs to B's individual screen flames rather than multiple mesh roots.
            var room = Room(); var group = ScreenState(room, 31d).rounds.Single(item => item.slot == 1);
            var owner = group.players[0];
            Extinguish(room, group, owner.playerId, owner.targets[0].id, 31d);
            var after = ScreenState(room, 31d).rounds.Single(item => item.id == group.id);
            Assert.That(after.players[0].targets[0].lit, Is.False);
            Assert.That(after.players[0].targets.Skip(1).All(fire => fire.lit), Is.True);
            Assert.That(after.players[1].targets.All(fire => fire.lit), Is.True);
            Assert.That(after.resolution, Is.EqualTo(LabJigwiABResolution.None));
            Assert.That(ScreenState(room, 43d).rounds.Single(item => item.id == group.id).players[0].targets[0].lit, Is.True);
        }

        [Test]
        public void CutoffCancelsScreenChallengeBeforeAnIdenticallyTimedCommonBombExpiry()
        {
            // Keep the 100-second ordering guarantee using an actual common bomb whose fuse also ends at 100.
            float[] values = config.CaptureDeveloperValues();
            values[45] = 20f; values[46] = values[47] = 2f; values[207] = 1f; values[208] = 0f; Apply(values);
            var room = Room(); var before = room.Snapshot(99.9d);
            var last = before.jigwiAB.rounds.Single(item => item.slot == 1);
            Assert.That(last.Active(99.9d), Is.True);
            var bomb = before.orbs.Single(orb => orb.kind == LabOrbKind.Bomb && orb.origin == LabOrbOrigin.Gourd && orb.createdAt == 80d);
            room.DrainBombExplosions();
            var atCutoff = room.Snapshot(100d);
            Assert.That(atCutoff.jigwiAB.rounds.Single(item => item.id == last.id).resolution, Is.EqualTo(LabJigwiABResolution.Cancelled));
            Assert.That(atCutoff.jigwiAB.Active(100d), Is.Null);
            Assert.That(atCutoff.jigwiAB.fever, Is.True);
            Assert.That(atCutoff.orbs.Any(orb => orb.id == bomb.id), Is.False);
            Assert.That(room.DrainBombExplosions().Select(blast => blast.BombId), Does.Not.Contain(bomb.id));
            Assert.That(room.JigwiStagePaintBlocked(Players[0], Vector3.forward), Is.False);
        }

        [Test]
        public void FeverDoublesOnlyAutomaticRecoveryAfterTheExactBoundary()
        {
            // Isolate the inherited 100-second fever boundary from A's independent recovery multiplier.
            float[] values = config.CaptureDeveloperValues(); values[4] = 0f; values[6] = values[7] = 1f;
            values[239] = 1f; Apply(values);
            var room = Room();
            Assert.That(room.Snapshot(99.5d).players[0].stamina, Is.EqualTo(99.5f).Within(.0001f));
            Assert.That(room.Snapshot(100d).players[0].stamina, Is.EqualTo(100f).Within(.0001f));
            Assert.That(room.Snapshot(100.5d).players[0].stamina, Is.EqualTo(101f).Within(.0001f));
            Assert.That(room.Snapshot(101d).players[0].stamina, Is.EqualTo(102f).Within(.0001f));
            Assert.That(config.StaminaMax, Is.EqualTo(1000f));
            Assert.That(room.JigwiStageRecoveryMultiplier(99.99d), Is.EqualTo(1f));
            Assert.That(room.JigwiStageRecoveryMultiplier(100d), Is.EqualTo(2f));
        }

        [Test]
        public void ScreenPolicyNeedsNoSurfaceGeometryAndRetiredRootsCannotChangeItsState()
        {
            // Geometry readiness is no longer a prerequisite for screen A; retired root APIs cannot resolve it.
            var room = Room(); var active = ScreenState(room, 11d).rounds.Single();
            Assert.That(active.Active(11d), Is.True);
            Assert.That(State(room, 11d).groups, Is.Empty);
            Assert.That(room.JigwiStagePaintBlocked(active.targetPlayer, Vector3.forward), Is.False);
            Assert.That(room.TryExtinguishJigwiStageRoot(active.targetPlayer, Vector3.forward, 11d, out _), Is.False);
            Assert.That(room.SetJigwiStageGeometry(active.id, Vector3.forward, new[] { Vector3.forward }, 11d), Is.False);
            Assert.That(room.RejectJigwiStageGeometry(active.id, 11d), Is.False);
            Assert.That(ScreenState(room, 11d).rounds.Single().hitCount, Is.Zero);
            Assert.That(room.JigwiABRecoveryMultiplier(active.targetPlayer, 11d), Is.EqualTo(4f));
        }

        [Test]
        public void ScreenAnchorsStayBoundedAndRetiredGeometryRejectsEvenValidOrMalformedInputs()
        {
            // Keep malformed/stale anchor protection while testing the replacement screen-coordinate contract.
            var room = Room(); var warning = ScreenState(room, 10d).rounds.Single();
            Assert.That(room.SetJigwiStageGeometry(warning.id, Vector3.up, new[] { Vector3.forward }, 10d), Is.False);
            Assert.That(room.SetJigwiStageGeometry(warning.id, Vector3.zero, new[] { Vector3.forward }, 10d), Is.False);
            Assert.That(room.SetJigwiStageGeometry(warning.id, Vector3.up, new[] { new Vector3(float.NaN, 0, 0) }, 10d), Is.False);
            Assert.That(room.SetJigwiStageGeometry("old/round/root", Vector3.up, new[] { Vector3.forward }, 10d), Is.False);
            var snapshot = room.Snapshot(31d); var group = snapshot.jigwiAB.Active(31d);
            foreach (var player in group.players)
                for (int i = 0; i < player.targets.Length; i++)
                {
                    var point = LabJigwiABFireMotion.Position(group, player.targets[i], i, 31d);
                    Assert.That(point.x, Is.InRange(.1f, .9f)); Assert.That(point.y, Is.InRange(.48f, .86f));
                }
            Assert.That(LabJigwiABNetworkValidation.Valid(snapshot.jigwiAB, snapshot.players), Is.True);
        }

        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void LargerRoomsKeepAllActualParticipantsAndBoundedScreenHistory(int count)
        {
            // Preserve 3/4/5-player membership and bounded-history coverage without obsolete helper pairs.
            ulong[] ids = Enumerable.Range(0, count).Select(n => (ulong)n * 10ul).ToArray();
            var room = new LabModel(); room.Begin("jigwi-many", ids, 0d, config, LabStageYokai.Jigwi);
            var snapshot = room.Snapshot(98d);
            Assert.That(snapshot.jigwiAB.rounds, Has.Length.EqualTo(8));
            var group = snapshot.jigwiAB.rounds.Single(item => item.slot == 1);
            Assert.That(group.players.Select(player => player.playerId), Is.EquivalentTo(ids));
            Assert.That(group.players.All(player => player.targets.Length == 3), Is.True);
            Assert.That(group.players.SelectMany(player => player.targets).Select(fire => fire.id).Distinct().Count(), Is.EqualTo(count * 3));
            Assert.That(LabJigwiABNetworkValidation.Valid(snapshot.jigwiAB, snapshot.players), Is.True);
        }

        [Test]
        public void RetryRotatesThePersonalTargetAndClearsAllOldScreenRounds()
        {
            // Preserve role rotation and reset coverage with the new personal target identity.
            var room = Room();
            ulong first = ScreenState(room, 10d).rounds.Single().targetPlayer;
            string previousId = ScreenState(room, 10d).rounds.Single().id;
            room.Tick(120d);
            room.Begin("jigwi-stage", Players, 121d, config, LabStageYokai.Jigwi);
            Assert.That(ScreenState(room, 121d).rounds, Is.Empty);
            var next = ScreenState(room, 131d).rounds.Single();
            Assert.That(next.targetPlayer, Is.Not.EqualTo(first));
            Assert.That(next.id, Is.Not.EqualTo(previousId));
            Assert.That(next.hitCount, Is.Zero);
        }

        [Test]
        public void DisabledTimelineRetainsTheLegacyStagePolicyAndNullValidationIsCompatible()
        {
            float[] values = config.CaptureDeveloperValues(); values[228] = 0f; Apply(values);
            var room = Room();
            Assert.That(room.JigwiStageEnabled, Is.False);
            Assert.That(State(room, 10d).enabled, Is.False);
            Assert.That(LabJigwiStageNetworkValidation.Valid(null), Is.True);
            Assert.That(LabJigwiStageNetworkValidation.Copy(null).enabled, Is.False);
        }

        [Test]
        public void ScreenSnapshotCopyAndValidationRejectMalformedTargetsOrNonMembers()
        {
            // Preserve independent copies, member validation, duplicate IDs and finite-clock checks on the active DTO.
            var room = Room(); var snapshot = room.Snapshot(31d);
            var copied = LabJigwiABNetworkValidation.Copy(snapshot.jigwiAB);
            copied.rounds[1].players[0].targets[0].x = .89f;
            Assert.That(snapshot.jigwiAB.rounds[1].players[0].targets[0].x, Is.Not.EqualTo(.89f));
            copied = snapshot.jigwiAB.Copy(); copied.rounds[1].players[0].playerId = 9999;
            Assert.That(LabJigwiABNetworkValidation.Valid(copied, snapshot.players), Is.False);
            copied = snapshot.jigwiAB.Copy(); copied.rounds[1].players[0].targets[0].x = float.NaN;
            Assert.That(LabJigwiABNetworkValidation.Valid(copied, snapshot.players), Is.False);
            copied = snapshot.jigwiAB.Copy(); copied.rounds = new[] { copied.rounds[0], copied.rounds[0].Copy() };
            Assert.That(LabJigwiABNetworkValidation.Valid(copied, snapshot.players), Is.False);
            copied = snapshot.jigwiAB.Copy(); copied.cutoffAt = double.NaN;
            Assert.That(LabJigwiABNetworkValidation.Valid(copied, snapshot.players), Is.False);
            Assert.That(LabJigwiABNetworkValidation.Valid(snapshot.jigwiAB, snapshot.players), Is.True);
        }
        [Test]
        public void ScreenEventIdentityIncludesItsRoomEvenWhenBothModelsAreOnRoundOne()
        {
            // Keep session-generation isolation and the maximum room-key boundary on the replacement event IDs.
            var first = Room(); var second = new LabModel();
            second.Begin(new string('x', 128), Players, 0d, config, LabStageYokai.Jigwi);
            var a = first.Snapshot(10d); var b = second.Snapshot(10d);
            Assert.That(a.round, Is.EqualTo(1)); Assert.That(b.round, Is.EqualTo(1));
            Assert.That(a.jigwiAB.rounds[0].id, Is.Not.EqualTo(b.jigwiAB.rounds[0].id));
            Assert.That(LabJigwiABNetworkValidation.Valid(b.jigwiAB, b.players), Is.True,
                "The maximum 128-character room key still fits bounded screen-event metadata.");
        }
    }
}
