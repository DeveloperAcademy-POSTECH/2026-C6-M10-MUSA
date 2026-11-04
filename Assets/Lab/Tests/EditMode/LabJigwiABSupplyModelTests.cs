using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Authoritative B ammunition cadence and lifecycle fixtures; not device/playability evidence.</summary>
    public sealed class LabJigwiABSupplyModelTests
    {
        private static readonly ulong[] Players = { 10ul, 20ul };
        private LabConfig config;
        private int serial;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = config.CaptureDeveloperValues();
            values[3] = 1000; values[4] = 100; values[5] = 1; values[6] = 0; values[7] = 1;
            values[9] = 100; values[10] = 120; values[74] = 2;
            values[39] = values[40] = 50;
            for (int i = 41; i <= 44; i++) values[i] = 0;
            values[155] = values[184] = values[166] = values[207] = values[208] = 0;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(config);

        private LabModel Room(LabStageYokai stage = LabStageYokai.Jigwi)
        {
            var model = new LabModel(); model.Begin("supply-412", Players, 0d, config, stage); return model;
        }
        private string Request() => "supply-request-" + ++serial;
        private Dictionary<string, LabOrbState> Orbs(LabModel model) =>
            (Dictionary<string, LabOrbState>)typeof(LabModel).GetField("orbs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(model);
        private void SetConfig(string name, object value)
        {
            FieldInfo field = typeof(LabConfig).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name); field.SetValue(config, value);
        }
        private LabOrbState[] Supplies(LabModel model, double at) => model.Snapshot(at).orbs
            .Where(orb => !string.IsNullOrEmpty(orb.jigwiABSupplyEventId)).ToArray();
        private LabOrbState Add(LabModel model, ulong owner, double at, LabOrbKind kind = LabOrbKind.Combined,
            LabOrbElement element = LabOrbElement.Water)
        {
            var orb = new LabOrbState { id = "supply-fixture-" + ++serial, owner = owner, kind = kind,
                element = kind == LabOrbKind.Combined ? element : LabOrbElement.None, x = .5f, y = .5f, createdAt = at };
            Orbs(model).Add(orb.id, orb); return orb;
        }
        private void Launch(LabModel model, LabOrbState orb, double at) =>
            Assert.That(model.TryLaunch(orb.owner, orb.id, Request(), at, out _, out string error), Is.True, error);
        private LabJigwiABRound B(LabModel model, double at = 25d) => model.Snapshot(at).jigwiAB.Active(at);
        private void Extinguish(LabModel model, LabJigwiABRound mission, ulong owner, string target, double at)
        {
            LabOrbState water = Add(model, owner, at); Launch(model, water, at);
            // #509: a picked seat's ordinary Water on the boss counts (and paints as usual).
            Assert.That(model.ConfirmHit(water.id, at, Vector3.back, out _, out _, out _), Is.True);
        }
        /// <summary>#509: two players → one picked seat; three of its own hits are three synchronized clears.</summary>
        private void ClearTeam(LabModel model, LabJigwiABRound mission, double at)
        {
            var picked = mission.players.Single();
            for (int i = 0; i < LabJigwiABTimeline.GroupSyncRequired; i++)
                Extinguish(model, mission, picked.playerId, picked.targets[0].id, at);
        }
        private static ulong Picked(LabJigwiABRound mission) => mission.players.Single().playerId;
        private static ulong Helper(LabJigwiABRound mission) => Players.Single(id => id != Picked(mission));

        [Test]
        public void SupplyBeginsAtActivationOnTheHelperOnlyAndCostsNoStaminaOrGenerationStatistics()
        {
            LabModel model = Room(); LabSnapshot warning = model.Snapshot(24d);
            Assert.That(warning.jigwiAB.Warning(24d).kind, Is.EqualTo(LabJigwiABKind.GroupFire));
            Assert.That(Supplies(model, 24.999d), Is.Empty);
            LabSnapshot active = model.Snapshot(25d);
            LabJigwiABRound mission = active.jigwiAB.Active(25d);
            ulong helper = Helper(mission);
            LabOrbState supply = active.orbs.Single(orb => !string.IsNullOrEmpty(orb.jigwiABSupplyEventId));
            Assert.That(supply.owner, Is.EqualTo(helper), "#509: free Water lands on the helper, who rolls it over.");
            Assert.That(supply.kind, Is.EqualTo(LabOrbKind.Combined));
            Assert.That(supply.element, Is.EqualTo(LabOrbElement.Water));
            Assert.That(supply.jigwiSupply, Is.True);
            Assert.That(supply.jigwiABSupplyEventId, Is.EqualTo(mission.id));
            Assert.That(supply.origin, Is.EqualTo(LabOrbOrigin.Gourd));
            Assert.That(supply.originOwner, Is.EqualTo(helper));
            Assert.That(supply.expiresAt, Is.GreaterThanOrEqualTo(40d));
            foreach (ulong owner in Players)
            {
                LabPlayerState player = active.players.Single(value => value.id == owner);
                LabPlayerState before = warning.players.Single(value => value.id == owner);
                Assert.That(player.stamina, Is.EqualTo(before.stamina));
                Assert.That(player.generatedTotal, Is.EqualTo(before.generatedTotal));
                Assert.That(player.combinedTotal, Is.EqualTo(before.combinedTotal));
            }
        }

        [Test]
        public void NormalCadenceStopsAtTheOutstandingCapAndRepeatedSnapshotsDoNotDuplicate()
        {
            LabModel model = Room(); ulong helper = Helper(B(model)); var ids = new HashSet<string>();
            for (int step = 0; step < 5; step++)
            {
                double at = 25d + step * 2d;
                LabOrbState[] supplies = Supplies(model, at);
                Assert.That(supplies.Count(orb => orb.originOwner == helper), Is.EqualTo(step + 1));
                Assert.That(supplies.All(orb => orb.originOwner == helper), Is.True);
                Assert.That(Supplies(model, at).Select(orb => orb.id), Is.EquivalentTo(supplies.Select(orb => orb.id)));
                foreach (var supply in supplies) ids.Add(supply.id);
                Assert.That(Supplies(model, at + 1.999d), Has.Length.EqualTo(step + 1));
            }
            Assert.That(ids.Count, Is.EqualTo(5));
            Assert.That(Supplies(model, 39d), Has.Length.EqualTo(5), "The cap of five unconsumed supplies holds.");
            Assert.That(Supplies(model, 40d), Is.Empty, "The 15-second deadline fails the mission and removes them.");
        }

        [Test]
        public void SupplySettingsCanChangeTheInitialCountIntervalAndOutstandingCapIndependently()
        {
            SetConfig("jigwiABSupplyInitialCount", 0);
            SetConfig("jigwiABSupplyIntervalSeconds", 1f);
            SetConfig("jigwiABSupplyLimit", 2);
            LabModel model = Room();
            Assert.That(Supplies(model, 25d), Is.Empty);
            Assert.That(Supplies(model, 26d), Has.Length.EqualTo(1));
            Assert.That(Supplies(model, 27d), Has.Length.EqualTo(2));
            Assert.That(Supplies(model, 28d), Has.Length.EqualTo(2));
        }

        [Test]
        public void ADelayedUpdateSkipsOverdueBatchesRatherThanFloodingTheBoards()
        {
            LabModel model = Room(); Assert.That(Supplies(model, 25d), Has.Length.EqualTo(1));
            Assert.That(Supplies(model, 34d), Has.Length.EqualTo(2));
            Assert.That(Supplies(model, 34d), Has.Length.EqualTo(2));
            Assert.That(Supplies(model, 34.99d), Has.Length.EqualTo(2));
            Assert.That(Supplies(model, 40d), Is.Empty);
        }

        [Test]
        public void TheQuotaIncludesTransferredAndFlyingUnconsumedSupplies()
        {
            SetConfig("jigwiABSupplyInitialCount", 5);
            LabModel model = Room(); LabJigwiABRound mission = B(model);
            ulong helper = Helper(mission), picked = Picked(mission);
            LabOrbState[] born = Supplies(model, 25d);
            LabOrbState passed = born.First();
            bool right = picked == Players[1];
            Assert.That(model.TryTransferAt(passed.owner, passed.id, right, new Vector2(right ? 1f : 0f, .5f),
                right ? Vector2.right : Vector2.left, Request(), 25d, LabEdgeCrossingKind.Swipe,
                out LabOrbState received, out string error), Is.True, error);
            Assert.That(received.owner, Is.EqualTo(picked));
            Assert.That(received.originOwner, Is.EqualTo(helper));
            Assert.That(received.jigwiABSupplyEventId, Is.EqualTo(passed.jigwiABSupplyEventId));
            LabOrbState flying = born.First(orb => orb.id != passed.id); Launch(model, flying, 25d);
            Assert.That(Supplies(model, 27d), Has.Length.EqualTo(5), "Transfer/flight do not free an unconsumed supply quota.");
            Assert.That(model.ConfirmMiss(flying.id, 27d, false), Is.True);
            LabOrbState[] replenished = Supplies(model, 29d);
            Assert.That(replenished, Has.Length.EqualTo(5));
            Assert.That(replenished.All(orb => orb.originOwner == helper), Is.True);
            Assert.That(replenished.Any(orb => orb.id == passed.id), Is.True);
        }

        [Test]
        public void OrdinaryCombinationKeepsItsNormalElementSequenceDuringB()
        {
            LabModel model = Room(), normal = Room(LabStageYokai.None);
            B(model, 25d); normal.Tick(25d);
            var elements = new List<LabOrbElement>();
            for (int i = 0; i < 12; i++)
            {
                LabOrbElement expected = Combine(normal, 25d).element;
                LabOrbState actual = Combine(model, 25d);
                Assert.That(actual.element, Is.EqualTo(expected));
                Assert.That(actual.jigwiABSupplyEventId, Is.Null.Or.Empty);
                Assert.That(actual.jigwiSupply, Is.False);
                elements.Add(actual.element);
            }
            Assert.That(elements.Any(value => value != LabOrbElement.Water), Is.True);
        }
        private LabOrbState Combine(LabModel model, double at)
        {
            var yin = Add(model, Players[0], at, LabOrbKind.Yin);
            var yang = Add(model, Players[0], at, LabOrbKind.Yang);
            Assert.That(model.TryCombineAt(Players[0], yin.id, yang.id, Request(), at,
                new Vector2(.5f, .5f), Vector2.zero, out LabOrbState combined, out string error), Is.True, error);
            return combined;
        }

        [Test]
        public void APassedSupplyCountsAsASyncHitWithoutOrdinaryBossRewards()
        {
            LabModel model = Room(); LabJigwiABRound mission = B(model);
            ulong picked = Picked(mission);
            LabOrbState supplied = Supplies(model, 25d).First();
            supplied = Orbs(model)[supplied.id]; supplied.owner = picked; // as if rolled over to the picked seat
            LabPlayerState before = model.Snapshot(25d).players.Single(player => player.id == picked);
            Launch(model, supplied, 25d);
            // #509: the supply hits the boss: it counts as the sync hit and is consumed without paint.
            Assert.That(model.ConfirmHit(supplied.id, 25d, Vector3.forward, out _, out _, out _), Is.False);
            Assert.That(model.ConfirmHit(supplied.id, 25d, Vector3.forward, out _, out _, out _), Is.False);
            Assert.That(B(model, 25d).syncCount, Is.EqualTo(1));
            LabPlayerState after = model.Snapshot(25d).players.Single(player => player.id == picked);
            Assert.That(after.stamina, Is.EqualTo(before.stamina));
            Assert.That(after.hitTotal, Is.EqualTo(before.hitTotal));
            Assert.That(after.throwTotal, Is.EqualTo(before.throwTotal));
            Assert.That(model.PaintSurface.PaintCount, Is.Zero);
        }

        [Test]
        public void SuppliedWaterThrownAtTheBossByTheHelperNeverPaints()
        {
            LabModel model = Room(); LabJigwiABRound mission = B(model); ulong helper = Helper(mission);
            LabOrbState supplied = Supplies(model, 25d).First();
            Assert.That(model.JigwiABBlocksBoss(helper, 25d), Is.False);
            LabPlayerState before = model.Snapshot(25d).players.Single(player => player.id == helper);
            int hp = model.Snapshot(25d).hp;
            Launch(model, supplied, 25d);
            Assert.That(model.ConfirmHit(supplied.id, 25d, Vector3.forward, out _, out _, out _), Is.False);
            LabSnapshot after = model.Snapshot(25d);
            Assert.That(after.orbs.Any(orb => orb.id == supplied.id), Is.False);
            Assert.That(after.hp, Is.EqualTo(hp));
            Assert.That(after.players.Single(player => player.id == helper).hitTotal, Is.EqualTo(before.hitTotal));
            Assert.That(after.players.Single(player => player.id == helper).stamina, Is.EqualTo(before.stamina));
            Assert.That(model.PaintSurface.PaintCount, Is.Zero);
            LabOrbState ordinary = Add(model, helper, 25d); Launch(model, ordinary, 25d);
            Assert.That(model.ConfirmHit(ordinary.id, 25d, Vector3.forward, out _, out _, out _), Is.True,
                "Ordinary Water keeps its normal boss attack for a helper.");
        }

        [Test]
        public void ADiscardedSupplyNeverChangesOrdinaryThrowAccuracyButAnOrdinaryMissStillDoes()
        {
            LabModel model = Room(); ulong helper = Helper(B(model));
            LabOrbState supplied = Supplies(model, 25d).First();
            int before = model.Snapshot(25d).players.Single(player => player.id == helper).throwTotal;
            Launch(model, supplied, 25d);
            Assert.That(model.ConfirmMiss(supplied.id, 25d), Is.True);
            Assert.That(model.Snapshot(25d).players.Single(player => player.id == helper).throwTotal, Is.EqualTo(before));
            LabOrbState ordinary = Add(model, helper, 25d); Launch(model, ordinary, 25d);
            Assert.That(model.ConfirmMiss(ordinary.id, 25d), Is.True);
            Assert.That(model.Snapshot(25d).players.Single(player => player.id == helper).throwTotal, Is.EqualTo(before + 1));
        }

        [Test]
        public void TeamSuccessRemovesOnlyThisMissionsSupplyIncludingFlightAndKeepsOrdinaryWater()
        {
            LabModel model = Room(); LabJigwiABRound mission = B(model);
            LabOrbState flying = Supplies(model, 25d).First(); Launch(model, flying, 25d);
            LabOrbState ordinary = Add(model, Helper(mission), 25d);
            ClearTeam(model, mission, 25d);
            LabSnapshot after = model.Snapshot(25d);
            Assert.That(after.jigwiAB.rounds.Single(value => value.id == mission.id).resolution, Is.EqualTo(LabJigwiABResolution.Completed));
            Assert.That(after.jigwiAB.rounds.Single(value => value.id == mission.id).rewardUntil, Is.EqualTo(-1d), "#509: no launch reward.");
            Assert.That(after.orbs.Select(orb => orb.id), Is.EquivalentTo(new[] { ordinary.id }));
            Assert.That(model.ConfirmMiss(flying.id, 25d), Is.False);
            Assert.That(Supplies(model, 34d), Is.Empty);
        }

        [Test]
        public void DeadlineFailureRemovesOnlyThisMissionsSupplyAndKeepsBoardsAndStamina()
        {
            // #509: the 40 s common gimmick becomes a swipe block (no orb) so only B's own result is checked.
            float[] values = config.CaptureDeveloperValues();
            int Field(string label) => LabConfig.DeveloperFields.Select((field, index) => (field, index)).First(pair => pair.field.Label == label).index;
            values[Field("Gimmick Chance Gourd Bomb (%)")] = 0; values[Field("Gimmick Chance Swipe Block (%)")] = 100;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            LabModel model = Room(); B(model);
            LabOrbState flying = Supplies(model, 25d).First(); Launch(model, flying, 25d);
            LabOrbState fire = Add(model, Players[0], 25d); LabOrbState potion = Add(model, Players[1], 25d, LabOrbKind.Potion);
            float[] stamina = model.Snapshot(39.9d).players.Select(player => player.stamina).ToArray();
            LabSnapshot after = model.Snapshot(40d);
            LabJigwiABRound failed = after.jigwiAB.rounds.Single(value => value.slot == 1);
            Assert.That(failed.resolution, Is.EqualTo(LabJigwiABResolution.Expired));
            Assert.That(failed.boardFireUntil, Is.EqualTo(43d), "#509: the boards burn for 3 s instead of being emptied.");
            Assert.That(after.orbs.Where(orb => !orb.kind.IsDrum()).Select(orb => orb.id), Is.EquivalentTo(new[] { fire.id, potion.id }));
            Assert.That(after.players.Select(player => player.stamina), Is.EqualTo(stamina));
            Assert.That(model.ConfirmMiss(flying.id, 40d), Is.False, "This B's supplied Water still disappears with it.");
            Assert.That(Supplies(model, 41d), Is.Empty);
        }

        [Test]
        public void BattleCancellationCleansFlightSuppliesButPreservesOrdinaryOrbsAndStamina()
        {
            LabModel model = Room(); LabJigwiABRound mission = B(model);
            LabOrbState supply = Supplies(model, 25d).First(); Launch(model, supply, 25d);
            LabOrbState ordinary = Add(model, Players[0], 25d);
            foreach (ulong owner in Players)
                Assert.That(model.TryApplyPlayerDamage(owner, config.PlayerMaxHp, 26d), Is.True);
            LabSnapshot after = model.Snapshot(26d);
            Assert.That(after.phase, Is.EqualTo(LabPhase.Defeat));
            Assert.That(after.orbs.Select(orb => orb.id), Is.EquivalentTo(new[] { ordinary.id }));
            Assert.That(after.players.All(player => player.stamina == 100f), Is.True);
            Assert.That(after.jigwiAB.rounds.Single(value => value.id == mission.id).resolution, Is.EqualTo(LabJigwiABResolution.Cancelled));
        }

        [Test]
        public void ANewRoundStartsANewSupplyIdentityAndDoesNotCarryOverCadenceOrQuota()
        {
            LabModel model = Room(); string first = B(model).id;
            model.Tick(120d);
            Assert.That(model.Snapshot(120d).phase, Is.EqualTo(LabPhase.Defeat));
            model.Begin("supply-reset", Players, 200d, config, LabStageYokai.Jigwi);
            Assert.That(Supplies(model, 224.99d), Is.Empty);
            LabOrbState[] next = Supplies(model, 225d);
            Assert.That(next, Has.Length.EqualTo(1));
            Assert.That(next.All(orb => orb.jigwiABSupplyEventId != first), Is.True);
        }

        [TestCase(LabStageYokai.None)]
        [TestCase(LabStageYokai.Gumiho)]
        [TestCase(LabStageYokai.Yaguang)]
        public void OtherStagesDoNotReceiveTheBWaterSupply(LabStageYokai stage)
        {
            LabModel model = Room(stage); Assert.That(Supplies(model, 25d), Is.Empty);
        }
    }
}
