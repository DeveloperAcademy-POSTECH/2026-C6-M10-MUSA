using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Stage A/B use real Host clocks, normal A generation and B-supplied Water launch/hit paths.</summary>
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
            // These owner/fire-isolation fixtures begin with one shot per fire. The default
            // one initial shot / two-second cadence is covered by LabJigwiABSupplyModelTests.
            values[254] = 3f;
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
        /// <summary>
        /// #509: free Water lands on the helpers' boards; these fixtures stand in for rolling one to the picked
        /// <paramref name="owner"/> by moving it there directly (the real pass is covered by the supply tests).
        /// </summary>
        private static LabOrbState SuppliedWater(LabModel room, LabJigwiABRound round, ulong owner, double now)
        {
            var copy = room.Snapshot(now).orbs.FirstOrDefault(orb => !orb.inFlight && orb.jigwiABSupplyEventId == round.id);
            Assert.That(copy, Is.Not.Null, "The Host must provide B ammunition to a helper.");
            var orbs = (System.Collections.Generic.Dictionary<string, LabOrbState>)typeof(LabModel)
                .GetField("orbs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(room);
            var water = orbs[copy.id]; water.owner = owner;
            Assert.That(water.kind, Is.EqualTo(LabOrbKind.Combined));
            Assert.That(water.element, Is.EqualTo(LabOrbElement.Water));
            Assert.That(water.jigwiSupply, Is.True);
            return water;
        }
        private void Extinguish(LabModel room, LabJigwiABRound round, ulong owner, string targetId, double now, bool synced = true)
        {
            var water = SuppliedWater(room, round, owner, now);
            Launch(room, owner, water, now);
            // #509: the supplied Water hits the boss itself; it counts and is consumed without paint.
            int before = ScreenState(room, now).rounds.Single(item => item.id == round.id).syncCount;
            Assert.That(room.ConfirmHit(water.id, now, Vector3.forward, out _, out _, out _), Is.False);
            var after = ScreenState(room, now).rounds.Single(item => item.id == round.id);
            if (synced && after.resolution == LabJigwiABResolution.None) Assert.That(after.syncCount, Is.EqualTo(before + 1));
        }
        /// <summary>#509: three synchronized clears by every picked seat.</summary>
        private void ExtinguishAll(LabModel room, LabJigwiABRound round, double now)
        {
            for (int k = 0; k < LabJigwiABTimeline.GroupSyncRequired; k++)
                for (int i = 0; i < round.players.Length; i++)
                    Extinguish(room, round, round.players[i].playerId, round.players[i].targets[0].id, now,
                        i == round.players.Length - 1);
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
        public void ScreenPolicyIsScopedToJigwiAndDoesNotSupplyWaterDuringA(LabStageYokai stage, bool enabled)
        {
            // #412 supplies Water only during B; A still preserves its normal generation path.
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
        public void PersonalFireHasOneSecondWarningFifteenSecondLimitAndNoSurfaceSpread()
        {
            var room = Room();
            Assert.That(ScreenState(room, 3.99d).rounds, Is.Empty);
            var warning = ScreenState(room, 4d).rounds.Single();
            Assert.That(warning.Warning(4d), Is.True);
            Assert.That(warning.Active(4d), Is.False);
            var active = ScreenState(room, 5d).rounds.Single();
            Assert.That(active.kind, Is.EqualTo(LabJigwiABKind.PersonalBurn));
            Assert.That(active.Active(5d), Is.True);
            Assert.That(ScreenState(room, 19.99d).rounds.Single().Active(19.99d), Is.True);
            var end = ScreenState(room, 20d).rounds.Single();
            Assert.That(end.resolution, Is.EqualTo(LabJigwiABResolution.Expired));
            Assert.That(end.resolvedAt, Is.EqualTo(20d));
            Assert.That(room.Snapshot(20d).jigwiStage.groups, Is.Empty);
        }

        [Test]
        public void OneLateTickRecordsAllSixTimedMissionsWithoutSkippingBoundarySlots()
        {
            var room = Room();
            var state = ScreenState(room, 98d);
            Assert.That(state.rounds, Has.Length.EqualTo(6));
            Assert.That(state.rounds.Select(item => item.slot), Is.EqualTo(Enumerable.Range(0, 6)));
            Assert.That(state.rounds.Select(item => item.id).Distinct().Count(), Is.EqualTo(6));
            Assert.That(state.rounds.Select(item => item.activeAt), Is.EqualTo(new[] { 5d, 25d, 42d, 55d, 70d, 85d }));
            // #509: B3 now runs until the 100 s cutoff, so at 98 it is still open; the rest expired.
            Assert.That(state.rounds.Where(item => item.slot != 5).All(item => item.resolution == LabJigwiABResolution.Expired), Is.True);
            Assert.That(state.rounds.Single(item => item.slot == 5).Active(98d), Is.True);
            // #509: a failed B sets the boards on fire for 3 s instead of wiping them (no purge fence).
            Assert.That(state.rounds.Where(item => item.kind == LabJigwiABKind.GroupFire && item.slot != 5)
                .All(item => item.clearedThroughRevision == 0 && Math.Abs(item.boardFireUntil - (item.resolvedAt + 3d)) < .00001d), Is.True);
            Assert.That(ScreenState(room, 98d).rounds.Length, Is.EqualTo(6));
            Assert.That(LabJigwiABNetworkValidation.Valid(state, room.Snapshot(98d).players, room.Revision), Is.True);
        }

        [Test]
        public void ScreenFireLeavesPaintOpenAndEveryOwnerGetsBurnDeadlines()
        {
            // Surface paint blocking was removed; #479 A marks orb expiry for every owner at once.
            var room = Room();
            ulong first = Players[0], other = Players[1];
            Assert.That(room.JigwiStagePaintBlocked(first, Vector3.forward), Is.False);
            Assert.That(room.JigwiStagePaintBlocked(other, Vector3.forward), Is.False);
            Assert.That(room.TryGenerate(first, NextRequest(), 11d, out var burning, out string reason), Is.True, reason);
            Assert.That(room.TryGenerate(other, NextRequest(), 11d, out var alsoBurning, out reason), Is.True, reason);
            Assert.That(burning.jigwiABBurnAt, Is.EqualTo(16d));
            Assert.That(alsoBurning.jigwiABBurnAt, Is.EqualTo(16d));
            var before = room.Snapshot(11d);
            var ended = room.Snapshot(21d);
            Assert.That(ended.players.Select(p => p.personalHp), Is.EqualTo(before.players.Select(p => p.personalHp)));
            Assert.That(ended.jigwi, Is.Empty, "A expiry does not invoke the legacy overheat experiment.");
            Assert.That(ended.jigwiAB.rounds.Single().resolution, Is.EqualTo(LabJigwiABResolution.Expired));
        }

        [Test]
        public void GroupChallengePicksOneSeatWithOnePillarAndSuppliesOnlyTheHelper()
        {
            // #509: two players → one picked seat (one pillar), the other is the helper who gets the free Water.
            var room = Room();
            var group = ScreenState(room, 31d).rounds.Single(item => item.slot == 1);
            Assert.That(group.kind, Is.EqualTo(LabJigwiABKind.GroupFire));
            Assert.That(group.players, Has.Length.EqualTo(1));
            Assert.That(Players, Does.Contain(group.players[0].playerId));
            Assert.That(group.players[0].targets, Has.Length.EqualTo(1));
            Assert.That(group.players[0].targets[0].lit, Is.True);
            ulong helper = Players.Single(id => id != group.players[0].playerId);
            var supplies = room.Snapshot(31d).orbs.Where(orb => orb.jigwiABSupplyEventId == group.id).ToArray();
            Assert.That(supplies.Length, Is.InRange(3, config.JigwiABSupplyLimit));
            Assert.That(supplies.All(orb => orb.owner == helper && orb.kind == LabOrbKind.Combined
                && orb.element == LabOrbElement.Water && orb.jigwiSupply && orb.originOwner == helper), Is.True);
        }

        [Test]
        public void AHelperCannotHitThePickedPillarAndThreeSyncsCompleteB()
        {
            var room = Room();
            var group = ScreenState(room, 31d).rounds.Single(item => item.slot == 1);
            var picked = group.players[0]; ulong helper = Players.Single(id => id != picked.playerId);
            var helperShot = SuppliedWater(room, group, helper, 31d);
            Launch(room, helper, helperShot, 31d);
            Assert.That(room.ConfirmHit(helperShot.id, 31d, Vector3.forward, out _, out _, out _), Is.False, "Supply never paints.");
            Assert.That(ScreenState(room, 31d).rounds.Single(item => item.id == group.id).syncCount, Is.Zero,
                "A helper's Water on the boss does not count.");
            Extinguish(room, group, picked.playerId, picked.targets[0].id, 31d);
            var halfway = ScreenState(room, 31d).rounds.Single(item => item.id == group.id);
            Assert.That(halfway.syncCount, Is.EqualTo(1));
            Assert.That(halfway.players[0].targets[0].lit, Is.True);
            Assert.That(halfway.resolution, Is.EqualTo(LabJigwiABResolution.None));
            Extinguish(room, group, picked.playerId, picked.targets[0].id, 31d);
            Extinguish(room, group, picked.playerId, picked.targets[0].id, 31d);
            var completed = ScreenState(room, 31d).rounds.Single(item => item.id == group.id);
            Assert.That(completed.resolution, Is.EqualTo(LabJigwiABResolution.Completed));
            Assert.That(completed.players.All(player => player.targets.All(fire => !fire.lit)), Is.True);
        }

        [TestCase(LabOrbKind.Relay, 41)]
        public void UnlockedRelayHitsCannotCountTowardPersonalCombinedRecovery(LabOrbKind kind, int chanceField)
        {
            // A non-paintable item still neither paints nor ends A (#479 removed A's hit counter).
            float[] values = config.CaptureDeveloperValues();
            for (int i = 39; i <= 44; i++) values[i] = 0f;
            values[chanceField] = 100f; values[46] = values[47] = 0f; Apply(values);
            var room = Room();
            Assert.That(room.TryGenerate(Players[0], NextRequest(), 11d, out var item, out string reason), Is.True, reason);
            Assert.That(item.kind, Is.EqualTo(kind)); Assert.That(item.lockSwipes, Is.Zero);
            Assert.That(item.element, Is.EqualTo(LabOrbElement.None));
            Launch(room, Players[0], item, 11d);
            Assert.That(room.ConfirmHit(item.id, 11d, Vector3.forward, out _, out _, out _), Is.True);
            var after = ScreenState(room, 11d).rounds.Single();
            Assert.That(after.Active(11d), Is.True);
            Assert.That(room.PaintSurface.PaintCount, Is.Zero);
            Assert.That(room.PaintSurface.CoveredCount, Is.Zero);
        }


        [Test]
        public void UnlockedBombMustOpenIntoTwoCombinedOrbsBeforeItCanPaintDuringA()
        {
            // Preserve main's bomb policy; the resulting Combined paints normally and does not end A (#479).
            float[] values = config.CaptureDeveloperValues();
            for (int i = 39; i <= 44; i++) values[i] = 0f;
            values[44] = 100f; values[46] = values[47] = 0f; Apply(values);
            var room = Room();
            Assert.That(room.TryGenerate(Players[0], NextRequest(), 11d, out var bomb, out string reason), Is.True, reason);
            Assert.That(bomb.kind, Is.EqualTo(LabOrbKind.Bomb));
            Assert.That(room.TryLaunch(Players[0], bomb.id, NextRequest(), 11d, out _, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("RAW_CANNOT_ATTACK"));
            Assert.That(room.ConfirmHit(bomb.id, 11d, Vector3.forward, out _, out _, out _), Is.False);
            Assert.That(room.TryGetMovableState(bomb.id, out _), Is.True);
            Assert.That(room.TryUseItemAt(Players[0], bomb.id, NextRequest(), 11d,
                new Vector2(.5f, .5f), out _, out reason), Is.True, reason);
            Assert.That(reason, Is.EqualTo("BOMB_OPENED"));
            var attacks = room.Snapshot(11d).orbs.Where(orb => orb.kind == LabOrbKind.Combined).ToArray();
            Assert.That(attacks, Has.Length.EqualTo(2));
            // #479: the two opened attacks catch fire one after the other (default stagger 1 s).
            Assert.That(attacks.Select(orb => orb.jigwiABBurnAt).OrderBy(value => value), Is.EqualTo(new[] { 16d, 17d }));
            Launch(room, Players[0], attacks[0], 11d);
            Assert.That(room.ConfirmHit(attacks[0].id, 11d, Vector3.forward, out _, out _, out _), Is.True);
            Assert.That(ScreenState(room, 11d).rounds.Single().Active(11d), Is.True);
            Assert.That(room.PaintSurface.CoveredCount, Is.GreaterThan(0));
        }

        [Test]
        public void ThreeRealCombinedBossHitsConsumeRewardAndPaintOnceEachButNoLongerCompleteA()
        {
            // Each boss hit keeps its one-time transaction checks; #479 A no longer ends on three hits.
            var room = Room(); ulong owner = Players[0];
            for (int i = 0; i < 3; i++)
            {
                var combined = Combined(room, owner, 11d);
                Launch(room, owner, combined, 11d);
                int paints = room.PaintSurface.PaintCount;
                var before = room.Snapshot(11d).players.Single(player => player.id == owner);
                Assert.That(room.ConfirmHit(combined.id, 11d, Vector3.forward, out _, out _, out _), Is.True);
                var after = room.Snapshot(11d).players.Single(player => player.id == owner);
                Assert.That(room.PaintSurface.PaintCount, Is.EqualTo(paints + 1));
                Assert.That(after.hitTotal, Is.EqualTo(before.hitTotal + 1));
                Assert.That(after.stamina, Is.EqualTo(Mathf.Min(config.StaminaMax, before.stamina + config.HitRecovery)));
                Assert.That(room.ConfirmHit(combined.id, 11d, Vector3.forward, out _, out _, out _), Is.False);
                Assert.That(room.PaintSurface.PaintCount, Is.EqualTo(paints + 1));
            }
            Assert.That(ScreenState(room, 11d).rounds.Single().resolution, Is.EqualTo(LabJigwiABResolution.None));
        }

        [Test]
        public void ScreenFireCollisionConsumesWithoutNormalPaintOrBossHitRewards()
        {
            // B's screen flame is now the separate fire-collision path, with no boss hit rewards or paint.
            var room = Room(); var group = ScreenState(room, 31d).rounds.Single(item => item.slot == 1);
            ulong owner = group.players[0].playerId;
            var water = SuppliedWater(room, group, owner, 31d); Launch(room, owner, water, 31d);
            var before = room.Snapshot(31d).players.Single(player => player.id == owner);
            int paints = room.PaintSurface.PaintCount;
            Assert.That(room.ConfirmHit(water.id, 31d, Vector3.forward, out _, out _, out _), Is.False,
                "#509: supplied Water on the boss counts for B but never paints.");
            Assert.That(ScreenState(room, 31d).rounds.Single(item => item.id == group.id).syncCount, Is.EqualTo(1));
            var after = room.Snapshot(31d).players.Single(player => player.id == owner);
            Assert.That(room.PaintSurface.PaintCount, Is.EqualTo(paints));
            Assert.That(after.hitTotal, Is.EqualTo(before.hitTotal));
            Assert.That(after.throwTotal, Is.EqualTo(before.throwTotal));
            Assert.That(after.stamina, Is.EqualTo(before.stamina));
            Assert.That(room.TryGetMovableState(water.id, out _), Is.False);
            Assert.That(ScreenState(room, 31d).rounds.Single(item => item.slot == 1).Active(31d), Is.True);
        }

        [Test]
        public void OneSyncDoesNotOpenThePillarOrEndB()
        {
            // #509: the pillar stays until the third synchronized clear; nothing relights any more.
            var room = Room(); var group = ScreenState(room, 31d).rounds.Single(item => item.slot == 1);
            var owner = group.players[0];
            Extinguish(room, group, owner.playerId, owner.targets[0].id, 31d);
            var after = ScreenState(room, 31d).rounds.Single(item => item.id == group.id);
            Assert.That(after.players[0].targets[0].lit, Is.True);
            Assert.That(after.syncCount, Is.EqualTo(1));
            Assert.That(after.resolution, Is.EqualTo(LabJigwiABResolution.None));
            Assert.That(room.JigwiABBlocksBoss(owner.playerId, 33d), Is.False, "#509: B never blocks the boss.");
        }

        [Test]
        public void CutoffPreservesCompletedMissionsAndClearsAnIdenticallyTimedCommonBombBeforeExplosion()
        {
            float[] values = config.CaptureDeveloperValues();
            values[4] = 1000f; values[45] = 20f; values[46] = values[47] = 2f;
            values[207] = 1f; values[208] = 0f; Apply(values);
            var room = Room();
            foreach (double at in new[] { 25d, 55d, 85d })
                ExtinguishAll(room, ScreenState(room, at).Active(at), at);
            var before = room.Snapshot(99.9d);
            Assert.That(before.jigwiAB.Active(99.9d), Is.Null);
            var bomb = before.orbs.Single(orb => orb.kind == LabOrbKind.Bomb
                && orb.origin == LabOrbOrigin.Gourd && orb.createdAt == 80d);
            room.DrainBombExplosions();
            var atCutoff = room.Snapshot(100d);
            Assert.That(atCutoff.jigwiAB.rounds.Where(item => item.kind == LabJigwiABKind.GroupFire)
                .All(item => item.resolution == LabJigwiABResolution.Completed), Is.True);
            Assert.That(atCutoff.jigwiAB.Active(100d), Is.Null);
            Assert.That(atCutoff.jigwiAB.fever, Is.True);
            Assert.That(atCutoff.orbs.Any(orb => orb.id == bomb.id), Is.False);
            Assert.That(room.DrainBombExplosions().Select(blast => blast.BombId), Does.Not.Contain(bomb.id));
            Assert.That(room.JigwiStagePaintBlocked(Players[0], Vector3.forward), Is.False);
        }

        [Test]
        public void FeverDoublesAutomaticRecoveryAfterTheExactBoundaryWithoutRepeatingTheLastFailure()
        {
            float[] values = config.CaptureDeveloperValues(); values[4] = 0f; values[6] = values[7] = 1f;
            Apply(values);
            var room = Room();
            // #509: B2 is the last B that can fail (at 70); B3 runs into the cutoff and is cancelled without penalty.
            // A failed B no longer empties stamina, so compare the recovery steps around the cutoff.
            Assert.That(room.Snapshot(70d).jigwiAB.rounds.Single(item => item.slot == 3).boardFireUntil, Is.EqualTo(73d).Within(.00001d));
            ulong fence = room.JigwiABFailureRevision;
            float before = room.Snapshot(99.5d).players[0].stamina;
            float atCutoff = room.Snapshot(100d).players[0].stamina;
            float half = room.Snapshot(100.5d).players[0].stamina;
            float one = room.Snapshot(101d).players[0].stamina;
            Assert.That(atCutoff - before, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(half - atCutoff, Is.EqualTo(1f).Within(.0001f));
            Assert.That(one - half, Is.EqualTo(1f).Within(.0001f));
            Assert.That(room.JigwiABFailureRevision, Is.EqualTo(fence));
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
            Assert.That(room.JigwiStagePaintBlocked(Players[0], Vector3.forward), Is.False);
            Assert.That(room.TryExtinguishJigwiStageRoot(Players[0], Vector3.forward, 11d, out _), Is.False);
            Assert.That(room.SetJigwiStageGeometry(active.id, Vector3.forward, new[] { Vector3.forward }, 11d), Is.False);
            Assert.That(room.RejectJigwiStageGeometry(active.id, 11d), Is.False);
            Assert.That(ScreenState(room, 11d).rounds.Single().resolution, Is.EqualTo(LabJigwiABResolution.None));
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
            Assert.That(snapshot.jigwiAB.rounds, Has.Length.EqualTo(6));
            var group = snapshot.jigwiAB.rounds.Single(item => item.slot == 1);
            // #509: 3 players → 2 picked, 4/5 → 3 picked; one pillar each.
            int picked = LabJigwiABTimeline.GroupPickCount(count);
            Assert.That(group.players, Has.Length.EqualTo(picked));
            Assert.That(ids, Is.SupersetOf(group.players.Select(player => player.playerId)));
            Assert.That(group.players.All(player => player.targets.Length == 1), Is.True);
            Assert.That(group.players.SelectMany(player => player.targets).Select(fire => fire.id).Distinct().Count(), Is.EqualTo(picked));
            Assert.That(LabJigwiABNetworkValidation.Valid(snapshot.jigwiAB, snapshot.players), Is.True);
        }

        [Test]
        public void RetryClearsAllOldScreenRounds()
        {
            // #479 A has no target to rotate; a retry still starts from fresh round identities.
            var room = Room();
            string previousId = ScreenState(room, 10d).rounds.Single().id;
            room.Tick(120d);
            room.Begin("jigwi-stage", Players, 121d, config, LabStageYokai.Jigwi);
            Assert.That(ScreenState(room, 121d).rounds, Is.Empty);
            var next = ScreenState(room, 131d).rounds.Single();
            Assert.That(next.id, Is.Not.EqualTo(previousId));
            Assert.That(next.resolution, Is.EqualTo(LabJigwiABResolution.None));
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
