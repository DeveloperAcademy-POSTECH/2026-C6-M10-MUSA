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
            Assert.That(model.ConfirmJigwiABFireHit(water.id, mission.id, target, at, out bool extinguished), Is.True);
            Assert.That(extinguished, Is.True);
        }
        private void ClearPlayer(LabModel model, LabJigwiABRound mission, ulong owner, double at)
        {
            foreach (var fire in mission.players.Single(player => player.playerId == owner).targets)
                Extinguish(model, mission, owner, fire.id, at);
        }
        private void ClearTeam(LabModel model, LabJigwiABRound mission, double at)
        {
            foreach (var player in mission.players) ClearPlayer(model, mission, player.playerId, at);
        }

        [Test]
        public void SupplyBeginsAtActivationAndCostsNoStaminaOrGenerationStatistics()
        {
            LabModel model = Room(); LabSnapshot warning = model.Snapshot(24d);
            Assert.That(warning.jigwiAB.Warning(24d).kind, Is.EqualTo(LabJigwiABKind.GroupFire));
            Assert.That(Supplies(model, 24.999d), Is.Empty);
            LabSnapshot active = model.Snapshot(25d);
            foreach (ulong owner in Players)
            {
                LabOrbState supply = active.orbs.Single(orb => orb.owner == owner);
                Assert.That(supply.kind, Is.EqualTo(LabOrbKind.Combined));
                Assert.That(supply.element, Is.EqualTo(LabOrbElement.Water));
                Assert.That(supply.jigwiSupply, Is.True);
                Assert.That(supply.jigwiABSupplyEventId, Is.EqualTo(active.jigwiAB.Active(25d).id));
                Assert.That(supply.origin, Is.EqualTo(LabOrbOrigin.Gourd));
                Assert.That(supply.originOwner, Is.EqualTo(owner));
                Assert.That(supply.expiresAt, Is.GreaterThanOrEqualTo(35d));
                LabPlayerState player = active.players.Single(value => value.id == owner);
                LabPlayerState before = warning.players.Single(value => value.id == owner);
                Assert.That(player.stamina, Is.EqualTo(before.stamina));
                Assert.That(player.generatedTotal, Is.EqualTo(before.generatedTotal));
                Assert.That(player.combinedTotal, Is.EqualTo(before.combinedTotal));
            }
        }

        [Test]
        public void NormalCadenceHasExactlyFiveSupplyOpportunitiesAndRepeatedSnapshotsDoNotDuplicate()
        {
            LabModel model = Room(); var ids = new HashSet<string>();
            for (int step = 0; step < 5; step++)
            {
                double at = 25d + step * 2d;
                LabOrbState[] supplies = Supplies(model, at);
                foreach (ulong owner in Players)
                    Assert.That(supplies.Count(orb => orb.originOwner == owner), Is.EqualTo(step + 1));
                Assert.That(Supplies(model, at).Select(orb => orb.id), Is.EquivalentTo(supplies.Select(orb => orb.id)));
                foreach (var supply in supplies) ids.Add(supply.id);
                Assert.That(Supplies(model, at + 1.999d), Has.Length.EqualTo((step + 1) * 2));
            }
            Assert.That(ids.Count, Is.EqualTo(10));
            Assert.That(Supplies(model, 35d), Is.Empty, "The deadline fails the mission; it must not grant a sixth batch.");
        }

        [Test]
        public void SupplySettingsCanChangeTheInitialCountIntervalAndOutstandingCapIndependently()
        {
            SetConfig("jigwiABSupplyInitialCount", 0);
            SetConfig("jigwiABSupplyIntervalSeconds", 1f);
            SetConfig("jigwiABSupplyLimit", 2);
            LabModel model = Room();
            Assert.That(Supplies(model, 25d), Is.Empty);
            Assert.That(Supplies(model, 26d), Has.Length.EqualTo(2));
            Assert.That(Supplies(model, 27d), Has.Length.EqualTo(4));
            Assert.That(Supplies(model, 28d), Has.Length.EqualTo(4));
        }

        [Test]
        public void ADelayedUpdateSkipsOverdueBatchesRatherThanFloodingTheBoards()
        {
            LabModel model = Room(); Assert.That(Supplies(model, 25d), Has.Length.EqualTo(2));
            Assert.That(Supplies(model, 34d), Has.Length.EqualTo(4));
            Assert.That(Supplies(model, 34d), Has.Length.EqualTo(4));
            Assert.That(Supplies(model, 34.99d), Has.Length.EqualTo(4));
            Assert.That(Supplies(model, 35d), Is.Empty);
        }

        [Test]
        public void TheQuotaIncludesTransferredAndFlyingUnconsumedSupplies()
        {
            SetConfig("jigwiABSupplyInitialCount", 5);
            LabModel model = Room(); LabOrbState[] born = Supplies(model, 25d);
            LabOrbState passed = born.First(orb => orb.owner == Players[0]);
            Assert.That(model.TryTransferAt(passed.owner, passed.id, true, new Vector2(1f, .5f), Vector2.right,
                Request(), 25d, LabEdgeCrossingKind.Swipe, out LabOrbState received, out string error), Is.True, error);
            Assert.That(received.owner, Is.EqualTo(Players[1]));
            Assert.That(received.originOwner, Is.EqualTo(Players[0]));
            Assert.That(received.jigwiABSupplyEventId, Is.EqualTo(passed.jigwiABSupplyEventId));
            LabOrbState flying = born.First(orb => orb.owner == Players[0] && orb.id != passed.id); Launch(model, flying, 25d);
            Assert.That(Supplies(model, 27d), Has.Length.EqualTo(10), "Transfer/flight do not free an unconsumed supply quota.");
            Assert.That(model.ConfirmMiss(flying.id, 27d, false), Is.True);
            LabOrbState[] replenished = Supplies(model, 29d);
            Assert.That(replenished, Has.Length.EqualTo(10));
            Assert.That(replenished.Count(orb => orb.originOwner == Players[0]), Is.EqualTo(5));
            Assert.That(replenished.Any(orb => orb.id == passed.id), Is.True);
        }

        [Test]
        public void APersonalClearStopsItsSupplyAndRelightingResumesOnTheSharedCadence()
        {
            float[] values = config.CaptureDeveloperValues(); values[245] = 4f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            LabModel model = Room(); LabJigwiABRound mission = B(model);
            ClearPlayer(model, mission, Players[0], 25d);
            LabOrbState[] afterClear = Supplies(model, 27d);
            Assert.That(afterClear.Count(orb => orb.originOwner == Players[0]), Is.EqualTo(1));
            Assert.That(afterClear.Count(orb => orb.originOwner == Players[1]), Is.EqualTo(2));
            LabOrbState[] afterRelight = Supplies(model, 29d);
            Assert.That(B(model, 29d).players.Single(player => player.playerId == Players[0]).targets.All(fire => fire.lit), Is.True);
            Assert.That(afterRelight.Count(orb => orb.originOwner == Players[0]), Is.EqualTo(2));
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
        public void SuppliedWaterExtinguishesARealFireWithoutOrdinaryBossRewards()
        {
            LabModel model = Room(); LabJigwiABRound mission = B(model);
            LabOrbState supplied = Supplies(model, 25d).First(orb => orb.owner == Players[0]);
            LabPlayerState before = model.Snapshot(25d).players.Single(player => player.id == Players[0]);
            Launch(model, supplied, 25d);
            string fire = mission.players.Single(player => player.playerId == Players[0]).targets[0].id;
            Assert.That(model.ConfirmJigwiABFireHit(supplied.id, mission.id, fire, 25d, out bool extinguished), Is.True);
            Assert.That(extinguished, Is.True);
            Assert.That(model.ConfirmJigwiABFireHit(supplied.id, mission.id, fire, 25d, out _), Is.False);
            LabPlayerState after = model.Snapshot(25d).players.Single(player => player.id == Players[0]);
            Assert.That(after.stamina, Is.EqualTo(before.stamina));
            Assert.That(after.hitTotal, Is.EqualTo(before.hitTotal));
            Assert.That(after.throwTotal, Is.EqualTo(before.throwTotal));
            Assert.That(model.PaintSurface.PaintCount, Is.Zero);
        }

        [Test]
        public void SuppliedWaterCannotPaintTheBossEvenAfterItsOwnerClearsTheirCurtain()
        {
            LabModel model = Room(); LabJigwiABRound mission = B(model);
            LabOrbState supplied = Supplies(model, 25d).First(orb => orb.owner == Players[0]);
            ClearPlayer(model, mission, Players[0], 25d);
            Assert.That(model.JigwiABBlocksBoss(Players[0], 25d), Is.False);
            LabPlayerState before = model.Snapshot(25d).players.Single(player => player.id == Players[0]);
            int hp = model.Snapshot(25d).hp;
            Launch(model, supplied, 25d);
            Assert.That(model.ConfirmHit(supplied.id, 25d, Vector3.forward, out _, out _, out _), Is.False);
            LabSnapshot after = model.Snapshot(25d);
            Assert.That(after.orbs.Any(orb => orb.id == supplied.id), Is.False);
            Assert.That(after.hp, Is.EqualTo(hp));
            Assert.That(after.players.Single(player => player.id == Players[0]).hitTotal, Is.EqualTo(before.hitTotal));
            Assert.That(after.players.Single(player => player.id == Players[0]).stamina, Is.EqualTo(before.stamina));
            Assert.That(model.PaintSurface.PaintCount, Is.Zero);
            LabOrbState ordinary = Add(model, Players[0], 25d); Launch(model, ordinary, 25d);
            Assert.That(model.ConfirmHit(ordinary.id, 25d, Vector3.forward, out _, out _, out _), Is.True,
                "Ordinary Water keeps its normal boss attack after the personal clear.");
        }

        [Test]
        public void ADiscardedSupplyNeverChangesOrdinaryThrowAccuracyButAnOrdinaryMissStillDoes()
        {
            LabModel model = Room(); B(model);
            LabOrbState supplied = Supplies(model, 25d).First(orb => orb.owner == Players[0]);
            int before = model.Snapshot(25d).players.Single(player => player.id == Players[0]).throwTotal;
            Launch(model, supplied, 25d);
            Assert.That(model.ConfirmMiss(supplied.id, 25d), Is.True);
            Assert.That(model.Snapshot(25d).players.Single(player => player.id == Players[0]).throwTotal, Is.EqualTo(before));
            LabOrbState ordinary = Add(model, Players[0], 25d); Launch(model, ordinary, 25d);
            Assert.That(model.ConfirmMiss(ordinary.id, 25d), Is.True);
            Assert.That(model.Snapshot(25d).players.Single(player => player.id == Players[0]).throwTotal, Is.EqualTo(before + 1));
        }

        [Test]
        public void TeamSuccessRemovesOnlyThisMissionsSupplyIncludingFlightAndKeepsOrdinaryWater()
        {
            LabModel model = Room(); LabJigwiABRound mission = B(model);
            LabOrbState flying = Supplies(model, 25d).First(orb => orb.owner == Players[0]); Launch(model, flying, 25d);
            LabOrbState ordinary = Add(model, Players[0], 25d);
            ClearTeam(model, mission, 25d);
            LabSnapshot after = model.Snapshot(25d);
            Assert.That(after.jigwiAB.rounds.Single(value => value.id == mission.id).resolution, Is.EqualTo(LabJigwiABResolution.Completed));
            Assert.That(after.jigwiAB.rounds.Single(value => value.id == mission.id).rewardUntil, Is.EqualTo(30d));
            Assert.That(after.orbs.Select(orb => orb.id), Is.EquivalentTo(new[] { ordinary.id }));
            Assert.That(model.ConfirmMiss(flying.id, 25d), Is.False);
            Assert.That(Supplies(model, 34d), Is.Empty);
        }

        [Test]
        public void DeadlineFailureKeepsTheExistingWholeBoardAndFlightPurgeAndZeroStamina()
        {
            LabModel model = Room(); B(model);
            LabOrbState flying = Supplies(model, 25d).First(); Launch(model, flying, 25d);
            Add(model, Players[0], 25d); Add(model, Players[1], 25d, LabOrbKind.Potion);
            LabSnapshot after = model.Snapshot(35d);
            Assert.That(after.jigwiAB.rounds.Single(value => value.slot == 1).resolution, Is.EqualTo(LabJigwiABResolution.Expired));
            Assert.That(after.orbs, Is.Empty);
            Assert.That(after.players.All(player => player.stamina == 0f), Is.True);
            Assert.That(model.ConfirmMiss(flying.id, 35d), Is.False);
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
            Assert.That(next, Has.Length.EqualTo(2));
            Assert.That(next.All(orb => orb.jigwiABSupplyEventId != first), Is.True);
            Assert.That(next.GroupBy(orb => orb.originOwner).All(group => group.Count() == 1), Is.True);
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
