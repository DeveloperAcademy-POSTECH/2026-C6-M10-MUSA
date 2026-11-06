using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>#509 B special Water (one per picked seat, one chance) lifecycle fixtures; not device/playability evidence.</summary>
    public sealed class LabJigwiABSupplyModelTests
    {
        private static readonly ulong[] Players = { 10ul, 20ul };
        private static readonly ulong[] Three = { 10ul, 20ul, 30ul };
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

        private LabModel Room(LabStageYokai stage = LabStageYokai.Jigwi, ulong[] seats = null)
        {
            var model = new LabModel(); model.Begin("supply-412", seats ?? Players, 0d, config, stage); return model;
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
        private LabOrbState Special(LabModel model, LabJigwiABRound mission, ulong seat) =>
            Orbs(model).Values.Single(orb => orb.jigwiABSupplyEventId == mission.id && orb.originOwner == seat);
        private static ulong Helper(LabJigwiABRound mission, IEnumerable<ulong> seats) =>
            seats.First(id => mission.players.All(player => player.playerId != id));

        [Test]
        public void EachPickedSeatGetsOneSpecialWaterAtActivationWithoutStaminaOrStatistics()
        {
            LabModel model = Room(Stage(), Three); LabSnapshot warning = model.Snapshot(24d);
            Assert.That(warning.jigwiAB.Warning(24d).kind, Is.EqualTo(LabJigwiABKind.GroupFire));
            Assert.That(Supplies(model, 24.999d), Is.Empty);
            LabSnapshot before = model.Snapshot(24.999d);
            LabJigwiABRound mission = B(model);
            LabOrbState[] specials = Supplies(model, 25d);
            Assert.That(specials.Select(orb => orb.originOwner), Is.EquivalentTo(mission.players.Select(player => player.playerId)));
            Assert.That(specials.All(orb => orb.owner == orb.originOwner && orb.jigwiSupply && orb.kind == LabOrbKind.Combined
                && orb.element == LabOrbElement.Water && orb.origin == LabOrbOrigin.Gourd), Is.True);
            Assert.That(specials.Any(orb => orb.originOwner == Helper(mission, Three)), Is.False, "Helpers get no Water.");
            LabSnapshot after = model.Snapshot(25d);
            foreach (LabPlayerState player in after.players)
            {
                LabPlayerState old = before.players.Single(value => value.id == player.id);
                Assert.That(player.stamina, Is.EqualTo(old.stamina)); Assert.That(player.throwTotal, Is.EqualTo(old.throwTotal));
            }
            Assert.That(Supplies(model, 39d), Has.Length.EqualTo(specials.Length), "No cadence: still one each later.");
            Assert.That(LabJigwiABNetworkValidation.Valid(after.jigwiAB, after.players, model.Revision), Is.True);
        }
        private static LabStageYokai Stage() => LabStageYokai.Jigwi;

        [Test]
        public void TheSpecialWaterIsNotRegrantedAfterItIsThrownOrPassed()
        {
            LabModel model = Room(); LabJigwiABRound mission = B(model);
            LabOrbState mine = Special(model, mission, Players[0]);
            Assert.That(model.TryTransferAt(Players[0], mine.id, true, new Vector2(1, .5f), Vector2.right, Request(), 25.2d,
                LabEdgeCrossingKind.Swipe, out LabOrbState received, out string error), Is.True, error);
            Assert.That(received.owner, Is.EqualTo(Players[1]));
            Assert.That(Supplies(model, 30d).Count(orb => orb.originOwner == Players[0]), Is.EqualTo(1), "Passing does not make a new one.");
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
        public void APassedSpecialWaterStillCountsForItsPickedSeatWithoutOrdinaryBossRewards()
        {
            LabModel model = Room(); LabJigwiABRound mission = B(model);
            LabOrbState special = Special(model, mission, Players[0]);
            special.owner = Players[1]; // as if rolled over to the other seat, who throws it
            LabPlayerState before = model.Snapshot(25d).players.Single(player => player.id == Players[1]);
            int hp = model.Snapshot(25d).hp;
            Launch(model, special, 25d);
            Assert.That(model.ConfirmHit(special.id, 25d, Vector3.forward, out _, out _, out _), Is.False);
            Assert.That(model.ConfirmHit(special.id, 25d, Vector3.forward, out _, out _, out _), Is.False, "Consumed once.");
            LabJigwiABRound now = B(model, 25d);
            Assert.That(now.syncWindowAt, Is.EqualTo(25d));
            Assert.That(now.players.Single(player => player.playerId == Players[0]).targets[0].extinguishedAt, Is.EqualTo(25d),
                "It counts for the seat it was given to.");
            LabSnapshot after = model.Snapshot(25d);
            LabPlayerState thrower = after.players.Single(player => player.id == Players[1]);
            Assert.That(thrower.stamina, Is.EqualTo(before.stamina));
            Assert.That(thrower.hitTotal, Is.EqualTo(before.hitTotal));
            Assert.That(thrower.throwTotal, Is.EqualTo(before.throwTotal));
            Assert.That(after.hp, Is.EqualTo(hp));
            Assert.That(model.PaintSurface.PaintCount, Is.Zero);
        }

        [Test]
        public void AMissedSpecialWaterFailsBWithoutChangingThrowAccuracy()
        {
            LabModel model = Room(); LabJigwiABRound mission = B(model);
            LabOrbState special = Special(model, mission, Players[0]);
            int before = model.Snapshot(25d).players.Single(player => player.id == Players[0]).throwTotal;
            Launch(model, special, 25d);
            Assert.That(model.ConfirmMiss(special.id, 25.5d), Is.True);
            LabSnapshot after = model.Snapshot(25.5d);
            Assert.That(after.players.Single(player => player.id == Players[0]).throwTotal, Is.EqualTo(before));
            LabJigwiABRound failed = after.jigwiAB.rounds.Single(value => value.id == mission.id);
            Assert.That(failed.resolution, Is.EqualTo(LabJigwiABResolution.Expired));
            Assert.That(failed.boardFireUntil, Is.EqualTo(28.5d).Within(1e-9));
            Assert.That(Supplies(model, 25.5d), Is.Empty, "The other seat's special Water goes with the failed B.");
            LabOrbState ordinary = Add(model, Players[0], 26d); Launch(model, ordinary, 26d);
            Assert.That(model.ConfirmMiss(ordinary.id, 26d), Is.True);
            Assert.That(model.Snapshot(26d).players.Single(player => player.id == Players[0]).throwTotal, Is.EqualTo(before + 1));
        }

        [Test]
        public void TeamSuccessRemovesOnlyThisMissionsSupplyAndKeepsOrdinaryWater()
        {
            LabModel model = Room(Stage(), Three); LabJigwiABRound mission = B(model);
            LabOrbState ordinary = Add(model, Helper(mission, Three), 25d);
            foreach (var picked in mission.players)
            {
                LabOrbState special = Special(model, mission, picked.playerId); Launch(model, special, 25d);
                Assert.That(model.ConfirmHit(special.id, 25d, Vector3.back, out _, out _, out _), Is.False);
            }
            LabSnapshot after = model.Snapshot(25d);
            LabJigwiABRound done = after.jigwiAB.rounds.Single(value => value.id == mission.id);
            Assert.That(done.resolution, Is.EqualTo(LabJigwiABResolution.Completed));
            Assert.That(done.rewardUntil, Is.EqualTo(-1d), "#509: no launch reward.");
            Assert.That(after.orbs.Where(orb => !orb.kind.IsDrum()).Select(orb => orb.id), Is.EquivalentTo(new[] { ordinary.id }));
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
            LabOrbState fire = Add(model, Players[0], 25d); LabOrbState potion = Add(model, Players[1], 39.5d, LabOrbKind.Potion); // items have their own short lifetime
            float[] stamina = model.Snapshot(39.9d).players.Select(player => player.stamina).ToArray();
            Assert.That(Supplies(model, 39.9d), Has.Length.EqualTo(2), "Nobody threw the special Water.");
            LabSnapshot after = model.Snapshot(40d);
            LabJigwiABRound failed = after.jigwiAB.rounds.Single(value => value.slot == 1);
            Assert.That(failed.resolution, Is.EqualTo(LabJigwiABResolution.Expired));
            Assert.That(failed.boardFireUntil, Is.EqualTo(43d), "#509: the boards burn for 3 s instead of being emptied.");
            Assert.That(after.orbs.Where(orb => !orb.kind.IsDrum()).Select(orb => orb.id), Is.EquivalentTo(new[] { fire.id, potion.id }));
            Assert.That(after.players.Select(player => player.stamina), Is.EqualTo(stamina));
            Assert.That(Supplies(model, 41d), Is.Empty);
        }

        [Test]
        public void BattleCancellationCleansFlightSuppliesButPreservesOrdinaryOrbsAndStamina()
        {
            LabModel model = Room(); LabJigwiABRound mission = B(model);
            LabOrbState supply = Special(model, mission, Players[0]); Launch(model, supply, 25d);
            LabOrbState ordinary = Add(model, Players[0], 25d);
            foreach (ulong owner in Players)
                Assert.That(model.TryApplyPlayerDamage(owner, config.PlayerMaxHp, 26d), Is.True);
            LabSnapshot after = model.Snapshot(26d);
            Assert.That(after.phase, Is.EqualTo(LabPhase.Defeat));
            Assert.That(after.orbs.Where(orb => !orb.kind.IsDrum()).Select(orb => orb.id), Is.EquivalentTo(new[] { ordinary.id }));
            Assert.That(after.players.All(player => player.stamina == 100f), Is.True);
            Assert.That(after.jigwiAB.rounds.Single(value => value.id == mission.id).resolution, Is.EqualTo(LabJigwiABResolution.Cancelled));
        }

        [Test]
        public void ANewRoundStartsANewSupplyIdentity()
        {
            LabModel model = Room(); string first = B(model).id;
            model.Tick(120d);
            Assert.That(model.Snapshot(120d).phase, Is.EqualTo(LabPhase.Defeat));
            model.Begin("supply-reset", Players, 200d, config, LabStageYokai.Jigwi);
            Assert.That(Supplies(model, 224.99d), Is.Empty);
            LabOrbState[] next = Supplies(model, 225d);
            Assert.That(next, Has.Length.EqualTo(2));
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
