using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabModelTests
    {
        private LabConfig config;
        private LabModel model;

        [SetUp]
        public void SetUp()
        {
            // Rule tests below need Yin/Yang only; item chances have their own tests.
            // Cost 10 leaves stamina to keep drawing until both polarities appear.
            config = ConfigWith(10f, 50, 50, 0, 0, 0, 0);
            model = new LabModel();
            model.Begin("lab-test", new ulong[] { 10, 20, 30 }, 0d, config);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(config);

        [Test]
        public void DefaultsAndThreeSeatRoomAreVisibleInAnIndependentSnapshot()
        {
            var snapshot = model.Snapshot(0d);
            Assert.That(snapshot.phase, Is.EqualTo(LabPhase.Playing));
            Assert.That(snapshot.hp, Is.EqualTo(1000));
            Assert.That(snapshot.remaining, Is.EqualTo(180d));
            Assert.That(snapshot.players.Select(player => player.id), Is.EqualTo(new ulong[] { 10, 20, 30 }));
            Assert.That(snapshot.players.Select(player => player.seat), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(snapshot.players.All(player => player.stamina == 100f), Is.True);
            Assert.That(snapshot.players.All(player => player.personalHp == 300), Is.True);
            Assert.That(snapshot.orbs, Is.Empty);

            snapshot.players[0].stamina = 0f;
            snapshot.players[0].personalHp = 0;
            Assert.That(model.Snapshot(0d).players[0].stamina, Is.EqualTo(100f));
            Assert.That(model.Snapshot(0d).players[0].personalHp, Is.EqualTo(300));
            Assert.Throws<ArgumentException>(() => new LabModel().Begin("bad", new ulong[] { 1 }, 0d, config));
            Assert.Throws<ArgumentException>(() => new LabModel().Begin("bad", new ulong[] { 1, 1, 2 }, 0d, config));
            Assert.Throws<ArgumentException>(() => new LabModel().Begin("bad", new ulong[] { 1, 2, 3, 4, 5, 6 }, 0d, config));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void NetworkRoomAcceptsDistinctPlayersUpToFiveInSeatOrder(int count)
        {
            float[] values = config.CaptureDeveloperValues();
            values[48] = 120f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            ulong[] ids = Enumerable.Range(1, count).Select(index => (ulong)index).ToArray();
            var room = new LabModel();
            room.Begin("room-" + count, ids, 0d, config);
            LabSnapshot snapshot = room.Snapshot(0d);
            Assert.That(snapshot.players.Select(player => player.id), Is.EqualTo(ids));
            Assert.That(snapshot.players.Select(player => player.seat), Is.EqualTo(Enumerable.Range(0, count)));
            Assert.That(snapshot.players.Select(player => player.personalHp),
                Is.EqualTo(Enumerable.Repeat(120, count)));
        }

        [Test]
        public void TwoPlayerRoomTransfersToTheSameNeighborFromEitherEdge()
        {
            var room = new LabModel();
            room.Begin("two-player", new ulong[] { 10, 20 }, 0d, config);
            Assert.That(room.TryGenerate(10, "left-orb", 0d, out var left, out _), Is.True);
            Assert.That(room.TryGenerate(10, "right-orb", 0d, out var right, out _), Is.True);
            Assert.That(room.TryUpdateMotion(10, left.id, new Vector2(.01f, .3f), Vector2.left, .1d), Is.True);
            Assert.That(room.TryUpdateMotion(10, right.id, new Vector2(.99f, .7f), Vector2.right, .1d), Is.True);
            Assert.That(room.TryTransfer(10, left.id, false, .3f, Vector2.left, "to-left", .1d,
                out var receivedLeft, out _), Is.True);
            Assert.That(room.TryTransfer(10, right.id, true, .7f, Vector2.right, "to-right", .1d,
                out var receivedRight, out _), Is.True);
            Assert.That(receivedLeft.owner, Is.EqualTo(20ul));
            Assert.That(receivedLeft.x, Is.EqualTo(1f));
            Assert.That(receivedRight.owner, Is.EqualTo(20ul));
            Assert.That(receivedRight.x, Is.EqualTo(0f));
        }

        [TestCase(2)]
        [TestCase(4)]
        [TestCase(5)]
        public void LastAndFirstSeatsRemainNeighborsInEverySupportedRing(int count)
        {
            ulong[] ids = Enumerable.Range(1, count).Select(index => (ulong)index).ToArray();
            var room = new LabModel();
            room.Begin("ring-" + count, ids, 0d, config);
            ulong last = ids[count - 1];
            Assert.That(room.TryGenerate(last, "ring-orb", 0d, out var orb, out _), Is.True);
            Assert.That(room.TryUpdateMotion(last, orb.id, new Vector2(.99f, .4f), Vector2.right, .1d), Is.True);
            Assert.That(room.TryTransfer(last, orb.id, true, .4f, Vector2.right, "wrap-right", .1d,
                out var atFirst, out _), Is.True);
            Assert.That(atFirst.owner, Is.EqualTo(ids[0]));
            Assert.That(atFirst.id, Is.EqualTo(orb.id));
            Assert.That(room.TryUpdateMotion(ids[0], orb.id, new Vector2(.01f, .4f), Vector2.left, .2d), Is.True);
            Assert.That(room.TryTransfer(ids[0], orb.id, false, .4f, Vector2.left, "wrap-left", .2d,
                out var atLast, out _), Is.True);
            Assert.That(atLast.owner, Is.EqualTo(last));
            Assert.That(atLast.id, Is.EqualTo(orb.id));
        }

        [Test]
        public void DeveloperSoloHasOneRealPlayerAndCanCombineAndHitWithoutASecondDevice()
        {
            var solo = new LabModel();
            solo.BeginSolo("solo-test", 42ul, 0d, config);
            var start = solo.Snapshot(0d);
            Assert.That(start.players.Length, Is.EqualTo(1));
            Assert.That(start.players[0].id, Is.EqualTo(42ul));
            Assert.That(start.players[0].seat, Is.EqualTo(0));
            Assert.That(start.players[0].personalHp, Is.EqualTo(300));
            Assert.That(start.hp, Is.EqualTo(1000));

            LabOrbState yin = null, yang = null;
            for (int i = 0; i < 10 && (yin == null || yang == null); i++)
            {
                Assert.That(solo.TryGenerate(42ul, "solo-generate-" + i, 0d, out var raw, out _), Is.True);
                Assert.That(raw.y, Is.GreaterThanOrEqualTo(.43f),
                    "Generated orbs must be above the bottom HUD's covered part of the board.");
                if (raw.kind == LabOrbKind.Yin) yin = raw;
                else if (raw.kind == LabOrbKind.Yang) yang = raw;
            }
            Assert.That(yin, Is.Not.Null);
            Assert.That(yang, Is.Not.Null);
            Assert.That(solo.TryUpdateMotion(42ul, yin.id, new Vector2(.99f, .5f), Vector2.right, .1d), Is.True);
            Assert.That(solo.TryTransfer(42ul, yin.id, true, .5f, Vector2.right, "solo-edge", .1d,
                out _, out var transferReason), Is.False);
            Assert.That(transferReason, Is.EqualTo("NO_NEIGHBOR"));
            Assert.That(solo.Snapshot(.1d).orbs.Single(orb => orb.id == yin.id).owner, Is.EqualTo(42ul));

            Assert.That(solo.TryCombine(42ul, yin.id, yang.id, "solo-combine", 1d,
                out var combined, out _), Is.True);
            Assert.That(solo.TryLaunch(42ul, combined.id, "solo-launch", 1d, out _, out _), Is.True);
            Assert.That(solo.ConfirmHit(combined.id, 1d), Is.True);
            var hit = solo.Snapshot(1d);
            Assert.That(hit.hp, Is.EqualTo(980));
            Assert.That(hit.players.Length, Is.EqualTo(1));
            Assert.That(hit.orbs.Any(orb => orb.id == combined.id), Is.False);
        }

        [Test]
        public void CombinedOrbsReceiveEveryElementFromTheHost()
        {
            var seen = new System.Collections.Generic.HashSet<LabOrbElement>();
            for (int session = 0; session < 40; session++)
            {
                var room = new LabModel();
                room.Begin("element-" + session, new ulong[] { 10, 20 }, 0d, config);
                LabOrbState yin = null, yang = null;
                // Draws are independent now, so allow more than five tries to see both polarities.
                for (int i = 0; i < 10 && (yin == null || yang == null); i++)
                {
                    Assert.That(room.TryGenerate(10, "g" + i, 0d, out var raw, out _), Is.True);
                    if (raw.kind == LabOrbKind.Yin && yin == null) yin = raw;
                    if (raw.kind == LabOrbKind.Yang && yang == null) yang = raw;
                }
                Assert.That(room.TryCombine(10, yin.id, yang.id, "c", 0d, out var combined, out _), Is.True);
                Assert.That(combined.element, Is.Not.EqualTo(LabOrbElement.None));
                // JSON is the network format; the element must survive it.
                var copy = JsonUtility.FromJson<LabSnapshot>(JsonUtility.ToJson(room.Snapshot(0d)));
                Assert.That(copy.orbs.Single(orb => orb.id == combined.id).element, Is.EqualTo(combined.element));
                seen.Add(combined.element);
            }
            Assert.That(seen.Count, Is.EqualTo(5), "All five elements should appear across sessions.");
        }

        [Test]
        public void DeveloperSoloRetryResetsOnlyItsOwnPlayerAndKeepsMultiplayerMinimum()
        {
            var solo = new LabModel();
            solo.BeginSolo("solo-retry", 7ul, 0d, config);
            Assert.That(solo.TryGenerate(7ul, "first", 0d, out _, out _), Is.True);
            solo.Tick(180d);
            solo.BeginSolo("solo-retry", 7ul, 181d, config);
            var retry = solo.Snapshot(181d);
            Assert.That(retry.round, Is.EqualTo(2u));
            Assert.That(retry.players.Select(player => player.id), Is.EqualTo(new ulong[] { 7ul }));
            Assert.That(retry.orbs, Is.Empty);
            Assert.That(retry.players[0].stamina, Is.EqualTo(100f));
            Assert.That(retry.players[0].personalHp, Is.EqualTo(300));
            Assert.Throws<ArgumentException>(() => new LabModel().Begin("not-multiplayer", new[] { 7ul }, 0d, config));
        }

        [Test]
        public void GenerationPaysOneCostAndDuplicateCannotCreateOrChargeAgain()
        {
            Assert.That(model.TryGenerate(10, "one", 0d, out var first, out _), Is.True);
            Assert.That(first.kind == LabOrbKind.Yin || first.kind == LabOrbKind.Yang, Is.True);
            Assert.That(first.origin, Is.EqualTo(LabOrbOrigin.Generated));
            Assert.That(first.originOwner, Is.EqualTo(10ul));
            Assert.That(model.TryGenerate(10, "one", 0d, out _, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("DUPLICATE_REQUEST"));
            Assert.That(model.Snapshot(0d).players.Single(player => player.id == 10).stamina,
                Is.EqualTo(100f - config.GenerateCost));
            Assert.That(model.Snapshot(0d).orbs.Length, Is.EqualTo(1));

            model.Tick(3d);
            Assert.That(model.Snapshot(3d).players.Single(player => player.id == 10).stamina, Is.EqualTo(100f).Within(.001f));
            Assert.That(model.Snapshot(3d).players.Single(player => player.id == 20).stamina, Is.EqualTo(100f));
        }

        [Test]
        public void OnlyOwnersOppositeRawMayCombineAndCombinationStartsFreshLifetime()
        {
            // Three draws containing both polarities always include a same-polarity pair.
            var raw = new List<LabOrbState>();
            for (int i = 0; i < 10 && (raw.Count < 3 || raw.Select(orb => orb.kind).Distinct().Count() < 2); i++)
            {
                Assert.That(model.TryGenerate(10, "generate-" + i, 0d, out var generated, out _), Is.True);
                raw.Add(generated);
            }
            var yin = raw.First(orb => orb.kind == LabOrbKind.Yin);
            var yang = raw.First(orb => orb.kind == LabOrbKind.Yang);
            var samePair = raw.GroupBy(orb => orb.kind).First(group => group.Count() >= 2).Take(2).ToArray();

            Assert.That(model.TryCombine(20, yin.id, yang.id, "foreign", 1d, out _, out var foreignReason), Is.False);
            Assert.That(foreignReason, Is.EqualTo("NOT_OWNED_IDLE"));
            Assert.That(model.TryCombine(10, samePair[0].id, samePair[1].id, "same", 1d, out _, out var sameReason), Is.False);
            Assert.That(sameReason, Is.EqualTo("POLARITY_MISMATCH"));
            Assert.That(model.TryCombine(10, yin.id, yang.id, "valid", 1d, out var combined, out _), Is.True);
            Assert.That(combined.kind, Is.EqualTo(LabOrbKind.Combined));
            Assert.That(combined.origin, Is.EqualTo(LabOrbOrigin.Combined));
            Assert.That(combined.createdAt, Is.EqualTo(1d));
            Assert.That(yin.element, Is.EqualTo(LabOrbElement.None));
            Assert.That(yang.element, Is.EqualTo(LabOrbElement.None));
            Assert.That(combined.element, Is.Not.EqualTo(LabOrbElement.None));
            Assert.That(model.Snapshot(1d).orbs.Single(orb => orb.id == combined.id).element,
                Is.EqualTo(combined.element));
            Assert.That(model.Snapshot(1d).orbs.Any(orb => orb.id == yin.id || orb.id == yang.id), Is.False);
            Assert.That(model.TryCombine(10, yin.id, yang.id, "valid", 1d, out _, out var duplicateReason), Is.False);
            Assert.That(duplicateReason, Is.EqualTo("DUPLICATE_REQUEST"));

            // The two unused Raw orbs expire at t=8; the new Combined remains until t=9.
            Assert.That(model.Snapshot(8d).orbs.Select(orb => orb.id), Is.EqualTo(new[] { combined.id }));
            Assert.That(model.Snapshot(9d).orbs, Is.Empty);
        }

        [Test]
        public void ThreeSeatHandoffKeepsIdentityAgeAndMomentumAndChecksOwnership()
        {
            Assert.That(model.TryGenerate(10, "g", 0d, out var born, out _), Is.True);
            Assert.That(model.TryUpdateMotion(10, born.id, new Vector2(.99f, .4f), new Vector2(1.1f, .2f), .2d), Is.True);
            Assert.That(model.TryTransfer(10, born.id, true, .4f, new Vector2(1.1f, .2f), "right", .2d,
                out var atSecond, out _), Is.True);
            Assert.That(atSecond.owner, Is.EqualTo(20ul));
            Assert.That(atSecond.id, Is.EqualTo(born.id));
            Assert.That(atSecond.createdAt, Is.EqualTo(born.createdAt));
            Assert.That(atSecond.origin, Is.EqualTo(LabOrbOrigin.Generated));
            Assert.That(atSecond.originOwner, Is.EqualTo(10ul));
            Assert.That(atSecond.x, Is.EqualTo(0f));
            Assert.That(atSecond.y, Is.EqualTo(.4f));
            Assert.That(atSecond.vx, Is.EqualTo(1.1f));
            Assert.That(atSecond.vy, Is.EqualTo(.2f));
            Assert.That(model.TryUpdateMotion(10, born.id, new Vector2(.5f, .5f), Vector2.zero, .2d), Is.False);
            Assert.That(model.TryTransfer(10, born.id, true, .4f, new Vector2(1.1f, .2f), "again", .2d,
                out _, out var rejected), Is.False);
            Assert.That(rejected, Is.EqualTo("NOT_OWNED_IDLE"));

            Assert.That(model.TryUpdateMotion(20, born.id, new Vector2(.99f, .4f), new Vector2(1f, 0f), .3d), Is.True);
            Assert.That(model.TryTransfer(20, born.id, true, .4f, new Vector2(1f, 0f), "right-again", .3d,
                out var atThird, out _), Is.True);
            Assert.That(atThird.owner, Is.EqualTo(30ul));
            Assert.That(atThird.id, Is.EqualTo(born.id));
        }

        [Test]
        public void RawCannotLaunchAndARealHitRewardsOnlyTheProjectileOwnerOnce()
        {
            var pair = GeneratePair(10);
            Assert.That(model.TryLaunch(10, pair.yin.id, "raw-throw", 1d, out _, out var rawReason), Is.False);
            Assert.That(rawReason, Is.EqualTo("RAW_CANNOT_ATTACK"));
            Assert.That(model.TryCombine(10, pair.yin.id, pair.yang.id, "combine", 1d, out var combined, out _), Is.True);
            Assert.That(model.TryLaunch(10, combined.id, "launch", 1d, out var flying, out _), Is.True);
            Assert.That(flying.inFlight, Is.True);
            var beforeHit = model.Snapshot(1d);
            float before = beforeHit.players.Single(player => player.id == 10).stamina;
            int orbCountBefore = beforeHit.orbs.Length;
            Assert.That(model.ConfirmHit(combined.id, 1d), Is.True);
            var after = model.Snapshot(1d);
            Assert.That(after.hp, Is.EqualTo(980));
            Assert.That(after.players.Single(player => player.id == 10).stamina, Is.EqualTo(before + 5f).Within(.001f));
            Assert.That(after.players.Single(player => player.id == 20).stamina, Is.EqualTo(100f));
            Assert.That(after.orbs.Length, Is.EqualTo(orbCountBefore - 1));
            Assert.That(after.orbs.Any(orb => orb.id == combined.id), Is.False);
            Assert.That(model.ConfirmHit(combined.id, 1d), Is.False);
            Assert.That(model.Snapshot(1d).hp, Is.EqualTo(980));
        }

        [Test]
        public void ExpiredIdleOrbCannotTransferButProjectileMissNeverDamages()
        {
            Assert.That(model.TryGenerate(10, "old", 0d, out var old, out _), Is.True);
            Assert.That(model.TryUpdateMotion(10, old.id, new Vector2(.99f, .5f), Vector2.right, 1d), Is.True);
            Assert.That(model.TryTransfer(10, old.id, true, .5f, Vector2.right, "late", 8d,
                out _, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("ORB_NOT_FOUND"));

            var pair = GeneratePair(20, 8.1d);
            Assert.That(model.TryCombine(20, pair.yin.id, pair.yang.id, "new-combine", 8.1d,
                out var combined, out _), Is.True);
            Assert.That(model.TryLaunch(20, combined.id, "new-launch", 8.1d, out _, out _), Is.True);
            int hpBefore = model.Hp;
            Assert.That(model.ConfirmMiss(combined.id, 9d), Is.True);
            Assert.That(model.Hp, Is.EqualTo(hpBefore));
            Assert.That(model.ConfirmHit(combined.id, 9d), Is.False);
        }

        [Test]
        public void ClockEndsAtDeadlineAndRetryResetsRoundWithoutMovingOldResult()
        {
            model.Tick(179.9d);
            Assert.That(model.Snapshot(179.9d).phase, Is.EqualTo(LabPhase.Playing));
            model.Tick(180d);
            Assert.That(model.Snapshot(180d).phase, Is.EqualTo(LabPhase.Defeat));
            Assert.That(model.Snapshot(180d).remaining, Is.EqualTo(0d));
            Assert.That(model.TryGenerate(10, "too-late", 180d, out _, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("BATTLE_NOT_PLAYING"));
            model.Begin("lab-test", new ulong[] { 10, 20, 30 }, 181d, config);
            var retry = model.Snapshot(181d);
            Assert.That(retry.round, Is.EqualTo(2u));
            Assert.That(retry.phase, Is.EqualTo(LabPhase.Playing));
            Assert.That(retry.hp, Is.EqualTo(1000));
            Assert.That(retry.remaining, Is.EqualTo(180d));
            Assert.That(retry.players.All(player => player.stamina == 100f), Is.True);
            Assert.That(retry.players.All(player => player.personalHp == 300), Is.True);
            Assert.That(retry.orbs, Is.Empty);
        }

        [Test]
        public void HostDamageIsPerPlayerAndOnlyTheLastZeroEndsTheRound()
        {
            Assert.That(model.TryApplyPlayerDamage(999, 1, .1d), Is.False, "Unknown players cannot lose HP.");
            Assert.That(model.TryApplyPlayerDamage(10, 0, .1d), Is.False);
            Assert.That(model.TryApplyPlayerDamage(10, -1, .1d), Is.False);
            Assert.That(model.TryApplyPlayerDamage(10, 100, .1d), Is.True);
            var damaged = model.Snapshot(.1d);
            Assert.That(damaged.players.Select(player => player.personalHp), Is.EqualTo(new[] { 200, 300, 300 }));
            Assert.That(damaged.hp, Is.EqualTo(1000), "Player damage must not change monster HP.");
            Assert.That(damaged.phase, Is.EqualTo(LabPhase.Playing));

            Assert.That(model.TryApplyPlayerDamage(10, 999, .2d), Is.True);
            Assert.That(model.TryApplyPlayerDamage(10, 1, .2d), Is.False, "Already-zero HP cannot be damaged again.");
            Assert.That(model.TryApplyPlayerDamage(20, 300, .3d), Is.True);
            Assert.That(model.Snapshot(.3d).phase, Is.EqualTo(LabPhase.Playing));
            Assert.That(model.TryApplyPlayerDamage(30, 300, .4d), Is.True);
            var lost = model.Snapshot(.4d);
            Assert.That(lost.players.All(player => player.personalHp == 0), Is.True);
            Assert.That(lost.phase, Is.EqualTo(LabPhase.Defeat));
            Assert.That(lost.hp, Is.EqualTo(1000));
            Assert.That(model.TryApplyPlayerDamage(30, 1, .5d), Is.False, "A finished round cannot change HP.");
            Assert.That(model.TryGenerate(20, "after-party-defeat", .5d, out _, out _), Is.False);
        }

        [Test]
        public void PlayerHpUsesTheRoomSettingAndResetsForSoloRetry()
        {
            float[] values = config.CaptureDeveloperValues();
            values[48] = 120f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            var solo = new LabModel();
            solo.BeginSolo("solo-hp", 42, 0d, config);
            Assert.That(solo.Snapshot(0d).players.Single().personalHp, Is.EqualTo(120));
            Assert.That(solo.TryApplyPlayerDamage(42, 119, .1d), Is.True);
            Assert.That(solo.Snapshot(.1d).players.Single().personalHp, Is.EqualTo(1));
            Assert.That(solo.TryApplyPlayerDamage(42, 1, .2d), Is.True);
            Assert.That(solo.Snapshot(.2d).phase, Is.EqualTo(LabPhase.Defeat));
            solo.BeginSolo("solo-hp", 42, .3d, config);
            var retry = solo.Snapshot(.3d);
            Assert.That(retry.round, Is.EqualTo(2u));
            Assert.That(retry.phase, Is.EqualTo(LabPhase.Playing));
            Assert.That(retry.players.Single().personalHp, Is.EqualTo(120));
        }

        [Test]
        public void PlayerDamageRejectsBackwardAndExpiredHostTime()
        {
            Assert.That(model.TryApplyPlayerDamage(10, 10, 10d), Is.True);
            Assert.That(model.TryApplyPlayerDamage(10, 10, 9d), Is.False);
            Assert.That(model.Snapshot(10d).players.Single(player => player.id == 10).personalHp, Is.EqualTo(290));
            Assert.That(model.TryApplyPlayerDamage(10, 10, 180d), Is.False);
            Assert.That(model.Snapshot(180d).phase, Is.EqualTo(LabPhase.Defeat));
            Assert.That(model.Snapshot(180d).players.Single(player => player.id == 10).personalHp, Is.EqualTo(290));
        }

        [Test]
        public void NonFiniteAndBackwardMotionCannotChangeTheHostOrb()
        {
            Assert.That(model.TryGenerate(10, "g", 0d, out var orb, out _), Is.True);
            var before = model.Snapshot(0d).orbs.Single().Copy();
            Assert.That(model.TryUpdateMotion(10, orb.id, new Vector2(float.NaN, .5f), Vector2.right, 1d), Is.False);
            Assert.That(model.TryUpdateMotion(10, orb.id, new Vector2(.5f, .5f), new Vector2(float.PositiveInfinity, 0f), 1d), Is.False);
            Assert.That(model.TryUpdateMotion(10, orb.id, new Vector2(.5f, .5f), Vector2.right, .5d), Is.False);
            var after = model.Snapshot(1d).orbs.Single();
            Assert.That(after.x, Is.EqualTo(before.x));
            Assert.That(after.y, Is.EqualTo(before.y));
            Assert.That(after.revision, Is.EqualTo(before.revision));
        }

        [Test]
        public void GenerationFollowsDefaultChancesAndIncludesEveryItem()
        {
            var chances = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                float[] values = chances.CaptureDeveloperValues();
                values[5] = .001f; // generation cost
                values[9] = 100f;  // stored orb limit
                Assert.That(chances.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var room = new LabModel();
                room.Begin("chance-test", new ulong[] { 10, 20, 30 }, 0d, chances);
                var counts = new Dictionary<LabOrbKind, int>();
                var lockCounts = new HashSet<int>();
                foreach (ulong player in new ulong[] { 10, 20, 30 })
                    for (int i = 0; i < 100; i++)
                    {
                        Assert.That(room.TryGenerate(player, "draw-" + player + "-" + i, 0d, out var orb, out var reason),
                            Is.True, reason);
                        counts[orb.kind] = counts.TryGetValue(orb.kind, out int count) ? count + 1 : 1;
                        if (orb.kind.IsItem()) lockCounts.Add(orb.lockSwipes);
                        else Assert.That(orb.lockSwipes, Is.Zero, "Only items are locked.");
                    }

                // 300 draws at 40/40/5/5/5/5 percent: expected 120 each polarity and 15 each item.
                Assert.That(counts.Keys, Is.EquivalentTo(LabConfig.GeneratedKinds));
                Assert.That(counts[LabOrbKind.Yin], Is.InRange(90, 150));
                Assert.That(counts[LabOrbKind.Yang], Is.InRange(90, 150));
                foreach (LabOrbKind item in new[] { LabOrbKind.Relay, LabOrbKind.SecretBox, LabOrbKind.Potion, LabOrbKind.Bomb })
                    Assert.That(counts[item], Is.InRange(5, 30), item.ToString());
                Assert.That(lockCounts, Is.EquivalentTo(new[] { 2, 3, 4 }), "Items need 2 to 4 unlock swipes by default.");
            }
            finally { UnityEngine.Object.DestroyImmediate(chances); }
        }

        [Test]
        public void HundredPercentChanceAlwaysCreatesThatKind()
        {
            var bombsOnly = ConfigWith(10f, 0, 0, 0, 0, 0, 100);
            try
            {
                var room = new LabModel();
                room.Begin("bombs-only", new ulong[] { 10, 20, 30 }, 0d, bombsOnly);
                for (int i = 0; i < 5; i++)
                {
                    Assert.That(room.TryGenerate(10, "bomb-" + i, 0d, out var orb, out _), Is.True);
                    Assert.That(orb.kind, Is.EqualTo(LabOrbKind.Bomb));
                    Assert.That(orb.origin, Is.EqualTo(LabOrbOrigin.Generated));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(bombsOnly); }
        }

        [Test]
        public void ItemsCannotCombineWithRawOrBeThrownYet()
        {
            var mixed = ConfigWith(10f, 50, 0, 0, 0, 0, 50);
            try
            {
                var room = new LabModel();
                room.Begin("yin-and-bombs", new ulong[] { 10, 20, 30 }, 0d, mixed);
                LabOrbState yin = null, bomb = null;
                for (int i = 0; i < 10 && (yin == null || bomb == null); i++)
                {
                    Assert.That(room.TryGenerate(10, "mixed-" + i, 0d, out var orb, out _), Is.True);
                    if (orb.kind == LabOrbKind.Yin) yin = orb;
                    else if (orb.kind == LabOrbKind.Bomb) bomb = orb;
                }
                Assert.That(yin, Is.Not.Null);
                Assert.That(bomb, Is.Not.Null);

                Assert.That(room.TryCombine(10, yin.id, bomb.id, "yin-bomb", .1d, out _, out var combineReason), Is.False);
                Assert.That(combineReason, Is.EqualTo("POLARITY_MISMATCH"));
                Assert.That(room.TryLaunch(10, bomb.id, "bomb-throw", .1d, out _, out var launchReason), Is.False);
                Assert.That(launchReason, Is.EqualTo("RAW_CANNOT_ATTACK"));
                Assert.That(room.Snapshot(.1d).orbs.Select(orb => orb.id), Does.Contain(yin.id).And.Contain(bomb.id));
            }
            finally { UnityEngine.Object.DestroyImmediate(mixed); }
        }

        [Test]
        public void EachHandoffToANeighborRemovesOneLockAndSoloCannotUnlock()
        {
            var bombsOnly = ConfigWith(10f, 0, 0, 0, 0, 0, 100);
            try
            {
                var room = new LabModel();
                room.Begin("unlock-test", new ulong[] { 10, 20, 30 }, 0d, bombsOnly);
                Assert.That(room.TryGenerate(10, "bomb", 0d, out var bomb, out _), Is.True);
                int locks = bomb.lockSwipes;
                Assert.That(locks, Is.InRange(2, 4));

                // Pass the bomb rightward around the ring; one extra pass shows the count stops at zero.
                ulong[] ring = { 10, 20, 30 };
                for (int pass = 1; pass <= locks + 1; pass++)
                {
                    ulong holder = ring[(pass - 1) % ring.Length];
                    double at = pass * .1d;
                    Assert.That(room.TryUpdateMotion(holder, bomb.id, new Vector2(.99f, .5f), Vector2.right, at), Is.True);
                    Assert.That(room.TryTransfer(holder, bomb.id, true, .5f, Vector2.right, "pass-" + pass, at,
                        out var passed, out var reason), Is.True, reason);
                    Assert.That(passed.owner, Is.EqualTo(ring[pass % ring.Length]));
                    Assert.That(passed.lockSwipes, Is.EqualTo(Math.Max(0, locks - pass)));
                    Assert.That(room.Snapshot(at).orbs.Single(orb => orb.id == bomb.id).lockSwipes,
                        Is.EqualTo(passed.lockSwipes), "Every screen sees the remaining count.");
                }

                var solo = new LabModel();
                solo.BeginSolo("unlock-solo", 7ul, 0d, bombsOnly);
                Assert.That(solo.TryGenerate(7ul, "solo-bomb", 0d, out var soloBomb, out _), Is.True);
                Assert.That(solo.TryUpdateMotion(7ul, soloBomb.id, new Vector2(.99f, .5f), Vector2.right, .1d), Is.True);
                Assert.That(solo.TryTransfer(7ul, soloBomb.id, true, .5f, Vector2.right, "solo-pass", .1d,
                    out _, out var soloReason), Is.False);
                Assert.That(soloReason, Is.EqualTo("NO_NEIGHBOR"));
                Assert.That(solo.Snapshot(.1d).orbs.Single().lockSwipes, Is.EqualTo(soloBomb.lockSwipes),
                    "Solo play has no neighbor, so items stay locked.");
            }
            finally { UnityEngine.Object.DestroyImmediate(bombsOnly); }
        }

        [Test]
        public void ItemsLastTheItemLifetimeWhileRawOrbsKeepTheOrbLifetime()
        {
            var mixed = ConfigWith(10f, 50, 0, 0, 0, 0, 50);
            try
            {
                Assert.That(mixed.ItemLifetimeSeconds, Is.EqualTo(15f));
                var room = new LabModel();
                room.Begin("lifetime-test", new ulong[] { 10, 20, 30 }, 0d, mixed);
                LabOrbState yin = null, bomb = null;
                for (int i = 0; i < 10 && (yin == null || bomb == null); i++)
                {
                    Assert.That(room.TryGenerate(10, "life-" + i, 0d, out var orb, out _), Is.True);
                    if (orb.kind == LabOrbKind.Yin) yin = orb;
                    else if (orb.kind == LabOrbKind.Bomb) bomb = orb;
                }
                Assert.That(yin, Is.Not.Null);
                Assert.That(bomb, Is.Not.Null);

                var atEight = room.Snapshot(8d).orbs.Select(orb => orb.id).ToArray();
                Assert.That(atEight, Does.Not.Contain(yin.id));
                Assert.That(atEight, Does.Contain(bomb.id), "Items outlive the 8-second orb lifetime.");
                Assert.That(room.Snapshot(14.9d).orbs.Select(orb => orb.id), Does.Contain(bomb.id));
                Assert.That(room.Snapshot(15d).orbs.Select(orb => orb.id), Does.Not.Contain(bomb.id));
            }
            finally { UnityEngine.Object.DestroyImmediate(mixed); }
        }

        [Test]
        public void CombiningDrawsEveryElementEvenlyAndOtherOrbsHaveNone()
        {
            var rawOnly = ConfigWith(.001f, 50, 50, 0, 0, 0, 0);
            try
            {
                float[] values = rawOnly.CaptureDeveloperValues();
                values[9] = 100f; // stored orb limit
                Assert.That(rawOnly.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var room = new LabModel();
                room.Begin("element-test", new ulong[] { 10, 20, 30 }, 0d, rawOnly);
                var counts = new Dictionary<LabOrbElement, int>();
                foreach (ulong player in new ulong[] { 10, 20, 30 })
                {
                    var yins = new List<LabOrbState>();
                    var yangs = new List<LabOrbState>();
                    for (int i = 0; i < 100; i++)
                    {
                        Assert.That(room.TryGenerate(player, "raw-" + player + "-" + i, 0d, out var raw, out _), Is.True);
                        Assert.That(raw.element, Is.EqualTo(LabOrbElement.None));
                        (raw.kind == LabOrbKind.Yin ? yins : yangs).Add(raw);
                    }
                    for (int i = 0; i < Math.Min(yins.Count, yangs.Count); i++)
                    {
                        Assert.That(room.TryCombine(player, yins[i].id, yangs[i].id, "combine-" + player + "-" + i, 0d,
                            out var combined, out var reason), Is.True, reason);
                        Assert.That(combined.element, Is.Not.EqualTo(LabOrbElement.None));
                        Assert.That(room.Snapshot(0d).orbs.Single(orb => orb.id == combined.id).element,
                            Is.EqualTo(combined.element), "Clients receive the element through the snapshot.");
                        counts[combined.element] = counts.TryGetValue(combined.element, out int count) ? count + 1 : 1;
                    }
                }

                // 140 combines for this seed: about 28 per element at an even 20 percent.
                Assert.That(counts.Keys, Is.EquivalentTo(new[]
                    { LabOrbElement.Fire, LabOrbElement.Water, LabOrbElement.Wood, LabOrbElement.Metal, LabOrbElement.Earth }));
                foreach (var pair in counts) Assert.That(pair.Value, Is.InRange(14, 42), pair.Key.ToString());
            }
            finally { UnityEngine.Object.DestroyImmediate(rawOnly); }
        }

        [Test]
        public void PotionHealsOnlyItsUnlockedOwnerUpToMaxHpAndIsKeptWhenFull()
        {
            var potions = ConfigWith(10f, 0, 0, 0, 0, 100, 0);
            try
            {
                var locked = new LabModel();
                locked.Begin("potion-locked", new ulong[] { 10, 20, 30 }, 0d, potions);
                Assert.That(locked.TryGenerate(10, "locked", 0d, out var lockedPotion, out _), Is.True);
                Assert.That(lockedPotion.lockSwipes, Is.GreaterThan(0));
                Assert.That(locked.TryUseItem(10, lockedPotion.id, "use-locked", 0d, out _, out var lockedReason), Is.False);
                Assert.That(lockedReason, Is.EqualTo("ITEM_LOCKED"));

                float[] values = potions.CaptureDeveloperValues();
                values[46] = 0f; // unlock swipes min
                values[47] = 0f; // unlock swipes max
                Assert.That(potions.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var room = new LabModel();
                room.Begin("potion-open", new ulong[] { 10, 20, 30 }, 0d, potions);
                Assert.That(room.TryGenerate(10, "first", 0d, out var potion, out _), Is.True);
                Assert.That(potion.kind, Is.EqualTo(LabOrbKind.Potion));
                Assert.That(potion.lockSwipes, Is.Zero);

                Assert.That(room.TryUseItem(10, potion.id, "full", .1d, out _, out var fullReason), Is.False);
                Assert.That(fullReason, Is.EqualTo("HP_FULL"));
                Assert.That(room.Snapshot(.1d).orbs.Any(orb => orb.id == potion.id), Is.True, "A refused potion is kept.");

                Assert.That(room.TryApplyPlayerDamage(10, 25, .2d), Is.True);
                Assert.That(room.TryUseItem(20, potion.id, "foreign", .3d, out _, out var foreignReason), Is.False);
                Assert.That(foreignReason, Is.EqualTo("NOT_OWNED_IDLE"));
                Assert.That(room.TryUseItem(10, potion.id, "heal", .3d, out _, out var healReason), Is.True);
                Assert.That(healReason, Is.EqualTo("ITEM_USED"));
                var healed = room.Snapshot(.3d);
                Assert.That(healed.players.Single(player => player.id == 10).personalHp, Is.EqualTo(285));
                Assert.That(healed.players.Single(player => player.id == 20).personalHp, Is.EqualTo(300));
                Assert.That(healed.orbs.Any(orb => orb.id == potion.id), Is.False, "A used potion is consumed.");

                Assert.That(room.TryGenerate(10, "second", .4d, out var second, out _), Is.True);
                Assert.That(room.TryUseItem(10, second.id, "cap", .5d, out _, out _), Is.True);
                Assert.That(room.Snapshot(.5d).players.Single(player => player.id == 10).personalHp, Is.EqualTo(295));
                Assert.That(room.TryGenerate(10, "third", .6d, out var third, out _), Is.True);
                Assert.That(room.TryUseItem(10, third.id, "capped", .7d, out _, out _), Is.True);
                Assert.That(room.Snapshot(.7d).players.Single(player => player.id == 10).personalHp, Is.EqualTo(300),
                    "Healing never exceeds Player Max HP.");

                Assert.That(room.TryApplyPlayerDamage(20, 300, .8d), Is.True);
                Assert.That(room.Snapshot(.8d).phase, Is.EqualTo(LabPhase.Playing), "Others are still standing.");
                Assert.That(room.TryGenerate(20, "downed", .8d, out var downedPotion, out _), Is.True);
                Assert.That(room.TryUseItem(20, downedPotion.id, "revive", .9d, out _, out var downReason), Is.False);
                Assert.That(downReason, Is.EqualTo("PLAYER_DOWN"), "A potion cannot revive a player at 0 HP.");
                Assert.That(room.Snapshot(.9d).players.Single(player => player.id == 20).personalHp, Is.Zero);

                Assert.That(model.TryGenerate(10, "raw", 0d, out var raw, out _), Is.True);
                Assert.That(model.TryUseItem(10, raw.id, "raw-use", 0d, out _, out var rawReason), Is.False);
                Assert.That(rawReason, Is.EqualTo("NOT_USABLE"));
            }
            finally { UnityEngine.Object.DestroyImmediate(potions); }
        }

        [Test]
        public void SecretBoxOpensIntoTwoThrowableElementOrbsOnlyWhenThereIsRoom()
        {
            var boxes = ConfigWith(1f, 0, 0, 0, 100, 0, 0);
            try
            {
                float[] values = boxes.CaptureDeveloperValues();
                values[9] = 3f;  // stored orb limit
                values[46] = 0f; // unlock swipes min
                values[47] = 0f; // unlock swipes max
                Assert.That(boxes.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var room = new LabModel();
                room.Begin("box-open", new ulong[] { 10, 20, 30 }, 0d, boxes);
                Assert.That(room.TryGenerate(10, "box", 0d, out var box, out _), Is.True);
                Assert.That(box.kind, Is.EqualTo(LabOrbKind.SecretBox));

                Assert.That(room.TryUseItem(10, box.id, "open", .5d, out _, out var openReason), Is.True);
                Assert.That(openReason, Is.EqualTo("ITEM_USED"));
                var opened = room.Snapshot(.5d).orbs.Where(orb => orb.owner == 10).OrderBy(orb => orb.x).ToArray();
                Assert.That(opened.Length, Is.EqualTo(2), "The box is replaced by exactly two orbs.");
                Assert.That(opened.Any(orb => orb.id == box.id), Is.False);
                foreach (var orb in opened)
                {
                    Assert.That(orb.kind, Is.EqualTo(LabOrbKind.Combined));
                    Assert.That(orb.element, Is.Not.EqualTo(LabOrbElement.None));
                    Assert.That(orb.createdAt, Is.EqualTo(.5d), "Opened orbs start a fresh orb lifetime.");
                    Assert.That(orb.y, Is.EqualTo(box.y));
                }
                Assert.That(opened[0].x, Is.EqualTo(box.x - .05f).Within(.0001f));
                Assert.That(opened[1].x, Is.EqualTo(box.x + .05f).Within(.0001f));
                Assert.That(room.TryLaunch(10, opened[0].id, "throw", .6d, out _, out var launchReason), Is.True, launchReason);

                for (int i = 0; i < 3; i++)
                    Assert.That(room.TryGenerate(20, "full-" + i, .7d, out _, out _), Is.True);
                var fullBox = room.Snapshot(.7d).orbs.First(orb => orb.owner == 20);
                Assert.That(room.TryUseItem(20, fullBox.id, "no-room", .8d, out _, out var fullReason), Is.False);
                Assert.That(fullReason, Is.EqualTo("STORAGE_FULL"));
                Assert.That(room.Snapshot(.8d).orbs.Any(orb => orb.id == fullBox.id), Is.True, "A box without room is kept.");

                Assert.That(room.Snapshot(8.4d).orbs.Select(orb => orb.id), Does.Contain(opened[1].id));
                Assert.That(room.Snapshot(8.5d).orbs.Select(orb => orb.id), Does.Not.Contain(opened[1].id));
            }
            finally { UnityEngine.Object.DestroyImmediate(boxes); }
        }

        [Test]
        public void RelayBecomesX1OnUnlockThenX2AndMaxOnHandoffsAndHitsHarder()
        {
            var relays = ConfigWith(10f, 0, 0, 100, 0, 0, 0);
            try
            {
                var room = new LabModel();
                room.Begin("relay-stages", new ulong[] { 10, 20, 30 }, 0d, relays);
                Assert.That(room.TryGenerate(10, "relay", 0d, out var relay, out _), Is.True);
                int locks = relay.lockSwipes;
                Assert.That(locks, Is.InRange(2, 4));
                Assert.That(relay.relayStage, Is.Zero);
                Assert.That(room.TryLaunch(10, relay.id, "locked-throw", 0d, out _, out var lockedReason), Is.False);
                Assert.That(lockedReason, Is.EqualTo("RAW_CANNOT_ATTACK"), "A locked relay is not an attack orb yet.");

                // The unlocking handoff makes it x1, the next two make x2 and MAX, and MAX stays.
                ulong[] ring = { 10, 20, 30 };
                for (int pass = 1; pass <= locks + 3; pass++)
                {
                    var passed = PassRight(room, relay.id, ring[(pass - 1) % ring.Length], pass * .1d);
                    int expected = pass < locks ? 0 : Math.Min(3, pass - locks + 1);
                    Assert.That(passed.relayStage, Is.EqualTo(expected), "after handoff " + pass);
                }

                float[] values = relays.CaptureDeveloperValues();
                values[46] = 0f; // unlock swipes min
                values[47] = 0f; // unlock swipes max
                Assert.That(relays.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var hits = new LabModel();
                hits.Begin("relay-hits", new ulong[] { 10, 20, 30 }, 0d, relays);

                Assert.That(hits.TryGenerate(10, "x1", 0d, out var x1, out _), Is.True);
                Assert.That(x1.relayStage, Is.EqualTo(1), "An already unlocked relay starts at x1.");
                Assert.That(ThrowAndHit(hits, x1.id, 10, .1d), Is.EqualTo(20));

                Assert.That(hits.TryGenerate(10, "x2", .2d, out var x2, out _), Is.True);
                Assert.That(PassRight(hits, x2.id, 10, .3d).relayStage, Is.EqualTo(2));
                Assert.That(ThrowAndHit(hits, x2.id, 20, .4d), Is.EqualTo(40));

                Assert.That(hits.TryGenerate(10, "max", .5d, out var max, out _), Is.True);
                PassRight(hits, max.id, 10, .6d);
                Assert.That(PassRight(hits, max.id, 20, .7d).relayStage, Is.EqualTo(3));
                Assert.That(ThrowAndHit(hits, max.id, 30, .8d), Is.EqualTo(80));
                Assert.That(hits.Hp, Is.EqualTo(1000 - 20 - 40 - 80));
            }
            finally { UnityEngine.Object.DestroyImmediate(relays); }
        }

        [Test]
        public void UnthrownBombClearsItsCurrentHoldersBoardAtTheFuse()
        {
            var mixed = ConfigWith(1f, 50, 0, 0, 0, 0, 50);
            try
            {
                var room = new LabModel();
                room.Begin("bomb-holder", new ulong[] { 10, 20, 30 }, 0d, mixed);
                LabOrbState bomb = null;
                for (int i = 0; i < 10 && bomb == null; i++)
                {
                    Assert.That(room.TryGenerate(10, "draw-" + i, 0d, out var orb, out _), Is.True);
                    if (orb.kind == LabOrbKind.Bomb) bomb = orb;
                }
                Assert.That(bomb, Is.Not.Null);
                Assert.That(PassRight(room, bomb.id, 10, 1d).owner, Is.EqualTo(20ul), "The bomb moves to the next holder.");

                Assert.That(room.TryGenerate(10, "keep", 10d, out var kept, out _), Is.True);
                for (int i = 0; i < 2; i++)
                    Assert.That(room.TryGenerate(20, "lost-" + i, 10d, out _, out _), Is.True);
                Assert.That(room.Snapshot(14.9d).orbs.Count(orb => orb.owner == 20), Is.EqualTo(3));
                Assert.That(room.DrainBombExplosions(), Is.Empty);

                var after = room.Snapshot(15d);
                Assert.That(after.orbs.Any(orb => orb.owner == 20), Is.False,
                    "Every orb on the holder's board is lost, including the bomb.");
                Assert.That(after.orbs.Select(orb => orb.id), Does.Contain(kept.id), "Other boards are untouched.");
                Assert.That(room.DrainBombExplosions(), Is.EqualTo(new[] { 20ul }));
                Assert.That(room.DrainBombExplosions(), Is.Empty, "Each explosion is reported once.");
            }
            finally { UnityEngine.Object.DestroyImmediate(mixed); }
        }

        [Test]
        public void OnlyTheUnlockingHandoffStampsTheUnlockTimeAndTheNextHandoffClearsIt()
        {
            var relays = ConfigWith(10f, 0, 0, 100, 0, 0, 0);
            try
            {
                var room = new LabModel();
                room.Begin("unlock-time", new ulong[] { 10, 20, 30 }, 0d, relays);
                Assert.That(room.TryGenerate(10, "relay", 0d, out var relay, out _), Is.True);
                Assert.That(relay.unlockedAt, Is.EqualTo(-1d), "A new item has not been unlocked by a handoff.");
                int locks = relay.lockSwipes;
                ulong[] ring = { 10, 20, 30 };
                LabOrbState passed = null;
                for (int pass = 1; pass <= locks; pass++)
                {
                    passed = PassRight(room, relay.id, ring[(pass - 1) % ring.Length], pass);
                    if (pass < locks)
                        Assert.That(passed.unlockedAt, Is.EqualTo(-1d), "A handoff that leaves it locked is not an unlock.");
                }
                Assert.That(passed.unlockedAt, Is.EqualTo((double)locks));

                var seen = room.Snapshot(locks + .5d).orbs.Single(orb => orb.id == relay.id);
                Assert.That(seen.JustUnlocked(locks + .5d), Is.True, "The receiving screen plays the unlock effect.");
                Assert.That(seen.JustUnlocked(locks + LabOrbState.UnlockEffectWindowSeconds + .1d), Is.False,
                    "A screen that sees the unlock late shows the item already open.");

                passed = PassRight(room, relay.id, passed.owner, locks + .6d);
                Assert.That(passed.unlockedAt, Is.EqualTo(-1d), "The next holder receives it already open.");
                Assert.That(passed.JustUnlocked(locks + .6d), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(relays); }
        }

        [Test]
        public void UnlockedBombIsDefusedAndLivesAFreshOrbLifetimeLikeAnAttackOrb()
        {
            var bombs = ConfigWith(1f, 0, 0, 0, 0, 0, 100);
            try
            {
                var room = new LabModel();
                room.Begin("bomb-defuse", new ulong[] { 10, 20, 30 }, 0d, bombs);
                Assert.That(room.TryGenerate(10, "bomb", 0d, out var bomb, out _), Is.True);
                int locks = bomb.lockSwipes;
                ulong[] ring = { 10, 20, 30 };
                LabOrbState unlocked = null;
                for (int pass = 1; pass <= locks; pass++)
                    unlocked = PassRight(room, bomb.id, ring[(pass - 1) % ring.Length], pass);
                Assert.That(unlocked.lockSwipes, Is.Zero);
                Assert.That(unlocked.createdAt, Is.EqualTo((double)locks), "Unlocking starts a fresh lifetime.");

                ulong holder = unlocked.owner;
                Assert.That(room.TryGenerate(holder, "bystander", locks + 1d, out var bystander, out _), Is.True);
                Assert.That(room.Snapshot(locks + 7.9d).orbs.Select(orb => orb.id), Does.Contain(bomb.id));
                var after = room.Snapshot(locks + 8d);
                Assert.That(after.orbs.Select(orb => orb.id), Does.Not.Contain(bomb.id),
                    "An unlocked bomb keeps the 8-second orb lifetime, not the 15-second fuse.");
                Assert.That(after.orbs.Select(orb => orb.id), Does.Contain(bystander.id),
                    "A defused bomb leaves the holder's board untouched.");
                Assert.That(room.DrainBombExplosions(), Is.Empty);

                float[] values = bombs.CaptureDeveloperValues();
                values[46] = 0f; // unlock swipes min
                values[47] = 0f; // unlock swipes max
                Assert.That(bombs.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var open = new LabModel();
                open.Begin("bomb-open", new ulong[] { 10, 20, 30 }, 0d, bombs);
                Assert.That(open.TryGenerate(10, "open", 0d, out var openBomb, out _), Is.True);
                Assert.That(open.Snapshot(7.9d).orbs.Select(orb => orb.id), Does.Contain(openBomb.id));
                Assert.That(open.Snapshot(8d).orbs, Is.Empty, "A bomb created unlocked follows the same rule.");
                Assert.That(open.DrainBombExplosions(), Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(bombs); }
        }

        [Test]
        public void UnlockedBombHitsForTripleDamageAndAMissNeverExplodes()
        {
            var bombs = ConfigWith(1f, 0, 0, 0, 0, 0, 100);
            try
            {
                float[] values = bombs.CaptureDeveloperValues();
                values[46] = 0f; // unlock swipes min
                values[47] = 0f; // unlock swipes max
                Assert.That(bombs.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var room = new LabModel();
                room.Begin("bomb-throw", new ulong[] { 10, 20, 30 }, 0d, bombs);

                Assert.That(room.TryGenerate(10, "hit", 0d, out var hitBomb, out _), Is.True);
                Assert.That(ThrowAndHit(room, hitBomb.id, 10, .1d), Is.EqualTo(60));
                Assert.That(room.Hp, Is.EqualTo(940));

                Assert.That(room.TryGenerate(10, "miss", .2d, out var missBomb, out _), Is.True);
                Assert.That(room.TryLaunch(10, missBomb.id, "throw-miss", .3d, out _, out _), Is.True);
                Assert.That(room.ConfirmMiss(missBomb.id, .4d), Is.True);
                Assert.That(room.Snapshot(20d).orbs, Is.Empty);
                Assert.That(room.DrainBombExplosions(), Is.Empty, "A thrown bomb never explodes on a board.");
            }
            finally { UnityEngine.Object.DestroyImmediate(bombs); }
        }

        private static LabOrbState PassRight(LabModel room, string id, ulong holder, double at)
        {
            Assert.That(room.TryUpdateMotion(holder, id, new Vector2(.99f, .5f), Vector2.right, at), Is.True);
            Assert.That(room.TryTransfer(holder, id, true, .5f, Vector2.right, "pass-" + id + "-" + at, at,
                out var passed, out var reason), Is.True, reason);
            return passed;
        }

        private static int ThrowAndHit(LabModel room, string id, ulong thrower, double at)
        {
            Assert.That(room.TryLaunch(thrower, id, "throw-" + id, at, out _, out var reason), Is.True, reason);
            Assert.That(room.ConfirmHit(id, at, out _, out int damage), Is.True);
            return damage;
        }

        /// <summary>A default config with one generation cost and chances in GeneratedKinds order.</summary>
        private static LabConfig ConfigWith(float generateCost, params int[] chances)
        {
            var created = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = created.CaptureDeveloperValues();
            values[5] = generateCost;
            for (int i = 0; i < chances.Length; i++) values[39 + i] = chances[i];
            Assert.That(created.TryApplyDeveloperValues(values, out string error), Is.True, error);
            return created;
        }

        private (LabOrbState yin, LabOrbState yang) GeneratePair(ulong owner, double at = 0d)
        {
            LabOrbState yin = null, yang = null;
            for (int i = 0; i < 10 && (yin == null || yang == null); i++)
            {
                Assert.That(model.TryGenerate(owner, "pair-" + owner + "-" + at + "-" + i, at,
                    out var orb, out _), Is.True);
                if (orb.kind == LabOrbKind.Yin) yin = orb;
                else if (orb.kind == LabOrbKind.Yang) yang = orb;
            }
            Assert.That(yin, Is.Not.Null);
            Assert.That(yang, Is.Not.Null);
            return (yin, yang);
        }
    }
}
