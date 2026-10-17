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

        [Test]
        public void ChosenPOrderRatherThanConnectionOrderControlsBothTransferDirections()
        {
            var slots = new LabPlayerSlots(3);
            Assert.That(slots.TrySelect(10ul, 3, out _), Is.True); // Host chooses P3.
            Assert.That(slots.TrySelect(20ul, 1, out _), Is.True);
            Assert.That(slots.TrySelect(30ul, 2, out _), Is.True);
            var room = new LabModel();
            room.Begin("chosen-p-order", slots.PlayersInSlotOrder(), 0d, config);
            Assert.That(room.Snapshot(0d).players.Select(player => player.id),
                Is.EqualTo(new ulong[] { 20ul, 30ul, 10ul }));

            Assert.That(room.TryGenerate(10ul, "right", 0d, out var right, out _), Is.True);
            Assert.That(room.TryGenerate(10ul, "left", 0d, out var left, out _), Is.True);
            Assert.That(room.TryUpdateMotion(10ul, right.id, new Vector2(.99f, .5f), Vector2.right, .1d), Is.True);
            Assert.That(room.TryUpdateMotion(10ul, left.id, new Vector2(.01f, .5f), Vector2.left, .1d), Is.True);
            Assert.That(room.TryTransfer(10ul, right.id, true, .5f, Vector2.right, "p3-right", .1d,
                out var atP1, out _), Is.True);
            Assert.That(room.TryTransfer(10ul, left.id, false, .5f, Vector2.left, "p3-left", .1d,
                out var atP2, out _), Is.True);
            Assert.That(atP1.owner, Is.EqualTo(20ul));
            Assert.That(atP2.owner, Is.EqualTo(30ul));

            room.Tick(181d);
            room.Begin("chosen-p-order", slots.PlayersInSlotOrder(), 182d, config);
            LabSnapshot retry = room.Snapshot(182d);
            Assert.That(retry.round, Is.EqualTo(2u));
            Assert.That(retry.players.Select(player => player.id),
                Is.EqualTo(new ulong[] { 20ul, 30ul, 10ul }));
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
            Assert.That(start.drums.Length, Is.EqualTo(config.DrumCount));
            Assert.That(start.drums.All(drum => drum.owner == 42ul), Is.True, "A solo player holds every drum.");
            Assert.That(start.drums.Select(drum => drum.x).Distinct().Count(), Is.EqualTo(start.drums.Length),
                "Drums sharing a board start apart.");

            LabOrbState yin = null, yang = null;
            for (int i = 0; i < 10 && (yin == null || yang == null); i++)
            {
                Assert.That(solo.TryGenerate(42ul, "solo-generate-" + i, 0d, out var raw, out _), Is.True);
                Assert.That(new Vector2(raw.x, raw.y), Is.EqualTo(new Vector2(.5f, .5f)),
                    "Generated orbs come out of the first drum, which starts in the middle of the board.");
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
        public void CombinedOrbKeepsRequestedVelocityWithinMotionLimit()
        {
            LabOrbState yin = null, yang = null;
            for (int i = 0; i < 10 && (yin == null || yang == null); i++)
            {
                Assert.That(model.TryGenerate(10, "keep-" + i, 0d, out var raw, out _), Is.True);
                if (raw.kind == LabOrbKind.Yin && yin == null) yin = raw;
                if (raw.kind == LabOrbKind.Yang && yang == null) yang = raw;
            }
            Assert.That(yin, Is.Not.Null);
            Assert.That(yang, Is.Not.Null);

            var requested = new Vector2(.6f, -.4f);
            Assert.That(model.TryCombine(10, yin.id, yang.id, "keep", 1d, requested, out var combined, out _), Is.True);
            Assert.That(combined.vx, Is.EqualTo(requested.x).Within(.0001f));
            Assert.That(combined.vy, Is.EqualTo(requested.y).Within(.0001f));
            var stored = model.Snapshot(1d).orbs.Single(orb => orb.id == combined.id);
            Assert.That(new Vector2(stored.vx, stored.vy), Is.EqualTo(requested));

            // Huge requested speeds are limited. Player 20 has its own full stamina.
            LabOrbState a = null, b = null;
            for (int i = 0; i < 10 && (a == null || b == null); i++)
            {
                Assert.That(model.TryGenerate(20, "unsafe-" + i, 1d, out var raw, out _), Is.True);
                if (raw.kind == LabOrbKind.Yin && a == null) a = raw;
                if (raw.kind == LabOrbKind.Yang && b == null) b = raw;
            }
            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            Assert.That(model.TryCombine(20, a.id, b.id, "huge", 1d, new Vector2(1e6f, 0f), out var fast, out _), Is.True);
            Assert.That(new Vector2(fast.vx, fast.vy).magnitude, Is.LessThan(1e6f));
            Assert.That(float.IsNaN(fast.vx) || float.IsInfinity(fast.vx), Is.False);
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
            Assert.That(after.players.Single(player => player.id == 10).stamina,
                Is.EqualTo(Mathf.Min(config.StaminaMax, before + config.HitRecovery)).Within(.001f));
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
        public void GeneratedOrbsLeaveTheDrumInEveryDirectionWithoutCrossingASideEdge()
        {
            var spray = ConfigWith(.001f, 50, 50, 0, 0, 0, 0);
            try
            {
                float[] values = spray.CaptureDeveloperValues();
                values[9] = 100f; // stored orb limit
                Assert.That(spray.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var room = new LabModel();
                room.Begin("spray", new ulong[] { 10, 20, 30 }, 0d, spray);
                LabOrbState drum = room.Snapshot(0d).drums.First(candidate => candidate.owner == 10);
                float deceleration = spray.OrbFloorDeceleration;

                // The owner's screen reports where its drum is now; the Host starts the orb there.
                var reported = new Vector2(.35f, .6f);
                var velocities = new List<Vector2>();
                for (int i = 0; i < 40; i++)
                {
                    Assert.That(room.TryGenerate(10, drum.id, reported, "spray-" + i, 0d, out var orb, out var reason),
                        Is.True, reason);
                    Assert.That(new Vector2(orb.x, orb.y), Is.EqualTo(reported), "The orb starts where the drum is.");
                    var velocity = new Vector2(orb.vx, orb.vy);
                    Assert.That(velocity.magnitude, Is.LessThanOrEqualTo(spray.SpawnMaxSpeed + .0001f));
                    float room01 = velocity.x > 0f ? 1f - .08f - reported.x : reported.x - .08f;
                    float sideTravel = velocity.magnitude * Mathf.Abs(velocity.x) / (2f * deceleration);
                    Assert.That(sideTravel, Is.LessThanOrEqualTo(room01 + .0001f),
                        "A launch stops before the side edge it heads for.");
                    velocities.Add(velocity);
                }
                Assert.That(velocities.Any(v => v.y > 0f) && velocities.Any(v => v.y < 0f), Is.True, "Orbs fly up and down.");
                Assert.That(velocities.Any(v => v.x > 0f) && velocities.Any(v => v.x < 0f), Is.True, "Orbs fly left and right.");
                Assert.That(velocities.Select(v => v.magnitude).Distinct().Count(), Is.GreaterThan(1),
                    "Launch speed varies between min and max.");

                var byTheEdge = new Vector2(.95f, .5f);
                for (int i = 0; i < 20; i++)
                {
                    Assert.That(room.TryGenerate(10, drum.id, byTheEdge, "edge-" + i, 0d, out var orb, out var reason),
                        Is.True, reason);
                    Assert.That(orb.vx, Is.LessThanOrEqualTo(0f), "A drum by the right edge never sends an orb across it.");
                }

                Assert.That(room.TryGenerate(20, drum.id, reported, "not-mine", 0d, out _, out var refused), Is.False);
                Assert.That(refused, Is.EqualTo("NO_DRUM"), "Only the drum's holder can tap it.");
                Assert.That(room.TryGenerate(10, drum.id, new Vector2(1.2f, .5f), "off-board", 0d, out _, out refused),
                    Is.False);
                Assert.That(refused, Is.EqualTo("INVALID_MOTION"));
                Assert.That(room.TryGenerate(10, drum.id, new Vector2(float.NaN, .5f), "not-finite", 0d, out _, out refused),
                    Is.False);
                Assert.That(refused, Is.EqualTo("INVALID_MOTION"));
            }
            finally { UnityEngine.Object.DestroyImmediate(spray); }
        }

        [Test]
        public void OneDrumGoesToARandomPlayerAndOnlyItsHolderCanGenerate()
        {
            var single = ConfigWith(10f, 50, 50, 0, 0, 0, 0);
            try
            {
                float[] values = single.CaptureDeveloperValues();
                values[74] = 1f; // drum count
                Assert.That(single.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var holders = new HashSet<ulong>();
                for (int session = 0; session < 12; session++)
                {
                    var room = new LabModel();
                    room.Begin("drum-" + session, new ulong[] { 10, 20, 30 }, 0d, single);
                    LabOrbState drum = room.Snapshot(0d).drums.Single();
                    Assert.That(drum.kind, Is.EqualTo(LabOrbKind.Drum));
                    Assert.That(new Vector2(drum.x, drum.y), Is.EqualTo(new Vector2(.5f, .5f)),
                        "The drum starts in the middle of its holder's board.");
                    holders.Add(drum.owner);
                    foreach (ulong player in new ulong[] { 10, 20, 30 })
                    {
                        bool generated = room.TryGenerate(player, "g-" + player, 0d, out _, out var reason);
                        Assert.That(generated, Is.EqualTo(player == drum.owner), reason);
                        if (!generated) Assert.That(reason, Is.EqualTo("NO_DRUM"));
                    }
                }
                Assert.That(holders.Count, Is.GreaterThan(1), "The Host draws the holder anew for each round.");
            }
            finally { UnityEngine.Object.DestroyImmediate(single); }
        }

        [TestCase(2, 3)]
        [TestCase(3, 3)]
        [TestCase(5, 3)]
        [TestCase(4, 2)]
        public void DrumsGoToDifferentPlayersBeforeAnyoneGetsASecond(int drumCount, int playerCount)
        {
            var drums = ConfigWith(10f, 50, 50, 0, 0, 0, 0);
            try
            {
                float[] values = drums.CaptureDeveloperValues();
                values[74] = drumCount;
                Assert.That(drums.TryApplyDeveloperValues(values, out string error), Is.True, error);
                ulong[] ids = Enumerable.Range(1, playerCount).Select(index => (ulong)index).ToArray();
                var room = new LabModel();
                room.Begin("drums-" + drumCount + "-" + playerCount, ids, 0d, drums);
                LabOrbState[] placed = room.Snapshot(0d).drums;
                Assert.That(placed.Length, Is.EqualTo(drumCount));
                int[] perPlayer = ids.Select(id => placed.Count(drum => drum.owner == id)).ToArray();
                if (drumCount <= playerCount) Assert.That(perPlayer.Max(), Is.EqualTo(1), "Each drum has its own holder.");
                else Assert.That(perPlayer.Min(), Is.GreaterThanOrEqualTo(1), "Everyone holds a drum first.");
                foreach (ulong id in ids)
                {
                    float[] xs = placed.Where(drum => drum.owner == id).Select(drum => drum.x).ToArray();
                    Assert.That(xs.Distinct().Count(), Is.EqualTo(xs.Length), "Drums sharing a board start apart.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(drums); }
        }

        [Test]
        public void DrumPassesToANeighborButIsNeverStoredThrownExpiredOrBlownUp()
        {
            var bombs = ConfigWith(1f, 0, 0, 0, 0, 0, 100);
            try
            {
                float[] values = bombs.CaptureDeveloperValues();
                values[9] = 1f;  // stored orb limit: one orb fills a board
                values[74] = 3f; // every player holds one drum
                Assert.That(bombs.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var room = new LabModel();
                room.Begin("drum-rules", new ulong[] { 10, 20, 30 }, 0d, bombs);
                LabOrbState drum = room.Snapshot(0d).drums.Single(candidate => candidate.owner == 10);

                Assert.That(room.TryGenerate(20, "fill", 0d, out var bomb, out var reason), Is.True, reason);
                Assert.That(bomb.lockSwipes, Is.Zero, "A bomb has no lock; its fuse alone decides when it blasts.");
                Assert.That(room.TryGenerate(20, "over", 0d, out _, out reason), Is.False);
                Assert.That(reason, Is.EqualTo("STORAGE_FULL"), "Player 20's board is full of orbs.");
                Assert.That(room.TryGenerate(10, "beside-drum", 0d, out _, out reason), Is.True,
                    "A drum does not take a storage slot. " + reason);
                Assert.That(room.TryLaunch(10, drum.id, "throw-drum", 0d, out _, out _), Is.False, "A drum is never thrown.");
                Assert.That(room.TryUseItem(10, drum.id, "use-drum", 0d, out _, out _), Is.False);

                Assert.That(PassRight(room, drum.id, 10, .1d).owner, Is.EqualTo(20ul), "A full board still receives the drum.");
                Assert.That(room.TryGenerate(10, "no-drum", .2d, out _, out reason), Is.False);
                Assert.That(reason, Is.EqualTo("NO_DRUM"), "Passing the drum away stops its old holder generating.");

                LabSnapshot afterFuse = room.Snapshot(bombs.ItemLifetimeSeconds + 1d);
                Assert.That(room.DrainBombExplosions().Select(blast => blast.Owner), Does.Contain(20ul));
                Assert.That(afterFuse.orbs.Any(orb => orb.owner == 20ul), Is.False);
                Assert.That(afterFuse.drums.Count(candidate => candidate.owner == 20ul), Is.EqualTo(2),
                    "A bomb never takes a drum.");
                Assert.That(room.Snapshot(170d).drums.Length, Is.EqualTo(3), "Drums never expire.");
            }
            finally { UnityEngine.Object.DestroyImmediate(bombs); }
        }

        [Test]
        public void SpawnLaunchIsTheSameForTheSameRoomAndDrawIndex()
        {
            var again = new LabModel();
            again.Begin("lab-test", new ulong[] { 10, 20, 30 }, 0d, config);
            Assert.That(model.TryGenerate(10, "a", 0d, out var first, out _), Is.True);
            Assert.That(again.TryGenerate(10, "b", 0d, out var repeat, out _), Is.True);
            Assert.That(new Vector2(repeat.vx, repeat.vy), Is.EqualTo(new Vector2(first.vx, first.vy)),
                "The Host draw depends only on the room seed, player and draw index.");
            Assert.That(model.TryGenerate(20, "c", 0d, out var other, out _), Is.True);
            Assert.That(new Vector2(other.vx, other.vy), Is.Not.EqualTo(new Vector2(first.vx, first.vy)),
                "Another player's first orb has its own launch.");
        }

        [Test]
        public void ZeroFanLaunchesStraightUp()
        {
            float[] values = config.CaptureDeveloperValues();
            values[60] = 0f;
            values[61] = 1.2f;
            values[62] = 1.2f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            var straight = new LabModel();
            straight.Begin("straight", new ulong[] { 10, 20 }, 0d, config);
            Assert.That(straight.TryGenerate(10, "up", 0d, out var orb, out _), Is.True);
            Assert.That(orb.vx, Is.EqualTo(0f).Within(.0001f));
            Assert.That(orb.vy, Is.EqualTo(1.2f).Within(.0001f));
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
        public void DrumMakesOnlyYinAndYangByDefault()
        {
            var chances = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                Assert.That(chances.GenerationChances(), Is.EqualTo(new[] { 50, 50, 0, 0, 0, 0 }));
                float[] values = chances.CaptureDeveloperValues();
                values[5] = .001f; // generation cost
                values[9] = 100f;  // stored orb limit
                values[74] = 3f;   // every player holds a drum
                Assert.That(chances.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var room = new LabModel();
                room.Begin("default-chance-test", new ulong[] { 10, 20, 30 }, 0d, chances);
                var counts = new Dictionary<LabOrbKind, int>();
                foreach (ulong player in new ulong[] { 10, 20, 30 })
                    for (int i = 0; i < 100; i++)
                    {
                        Assert.That(room.TryGenerate(player, "draw-" + player + "-" + i, 0d, out var orb, out var reason),
                            Is.True, reason);
                        counts[orb.kind] = counts.TryGetValue(orb.kind, out int count) ? count + 1 : 1;
                        Assert.That(orb.lockSwipes, Is.Zero);
                    }

                // 300 draws at 50/50 percent: expected 150 each, and never an item.
                Assert.That(counts.Keys, Is.EquivalentTo(new[] { LabOrbKind.Yin, LabOrbKind.Yang }));
                Assert.That(counts[LabOrbKind.Yin], Is.InRange(115, 185));
                Assert.That(counts[LabOrbKind.Yang], Is.InRange(115, 185));
            }
            finally { UnityEngine.Object.DestroyImmediate(chances); }
        }

        [Test]
        public void GenerationFollowsConfiguredItemChancesAndIncludesEveryItem()
        {
            var chances = ScriptableObject.CreateInstance<LabConfig>();
            try
            {
                float[] values = chances.CaptureDeveloperValues();
                values[5] = .001f; // generation cost
                values[9] = 100f;  // stored orb limit
                values[74] = 3f;   // every player holds a drum
                // Items are off by default (#177) but the DEV form can still turn them back on.
                values[39] = 40f; values[40] = 40f;
                for (int i = 41; i <= 44; i++) values[i] = 5f;
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
                        if (orb.kind.IsItem() && orb.kind != LabOrbKind.Bomb) lockCounts.Add(orb.lockSwipes);
                        else Assert.That(orb.lockSwipes, Is.Zero, "Only items other than the bomb are locked.");
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
            var potionsOnly = ConfigWith(10f, 0, 0, 0, 0, 100, 0);
            try
            {
                var room = new LabModel();
                room.Begin("unlock-test", new ulong[] { 10, 20, 30 }, 0d, potionsOnly);
                Assert.That(room.TryGenerate(10, "potion", 0d, out var potion, out _), Is.True);
                int locks = potion.lockSwipes;
                Assert.That(locks, Is.InRange(2, 4));

                // Pass the potion rightward around the ring; one extra pass shows the count stops at zero.
                ulong[] ring = { 10, 20, 30 };
                for (int pass = 1; pass <= locks + 1; pass++)
                {
                    ulong holder = ring[(pass - 1) % ring.Length];
                    double at = pass * .1d;
                    Assert.That(room.TryUpdateMotion(holder, potion.id, new Vector2(.99f, .5f), Vector2.right, at), Is.True);
                    Assert.That(room.TryTransfer(holder, potion.id, true, .5f, Vector2.right, "pass-" + pass, at,
                        out var passed, out var reason), Is.True, reason);
                    Assert.That(passed.owner, Is.EqualTo(ring[pass % ring.Length]));
                    Assert.That(passed.lockSwipes, Is.EqualTo(Math.Max(0, locks - pass)));
                    Assert.That(room.Snapshot(at).orbs.Single(orb => orb.id == potion.id).lockSwipes,
                        Is.EqualTo(passed.lockSwipes), "Every screen sees the remaining count.");
                }

                var solo = new LabModel();
                solo.BeginSolo("unlock-solo", 7ul, 0d, potionsOnly);
                Assert.That(solo.TryGenerate(7ul, "solo-potion", 0d, out var soloPotion, out _), Is.True);
                Assert.That(solo.TryUpdateMotion(7ul, soloPotion.id, new Vector2(.99f, .5f), Vector2.right, .1d), Is.True);
                Assert.That(solo.TryTransfer(7ul, soloPotion.id, true, .5f, Vector2.right, "solo-pass", .1d,
                    out _, out var soloReason), Is.False);
                Assert.That(soloReason, Is.EqualTo("NO_NEIGHBOR"));
                Assert.That(solo.Snapshot(.1d).orbs.Single().lockSwipes, Is.EqualTo(soloPotion.lockSwipes),
                    "Solo play has no neighbor, so items stay locked.");
            }
            finally { UnityEngine.Object.DestroyImmediate(potionsOnly); }
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
        public void UnthrownBombBlastsWhereItsHolderLastHadItWithoutRemovingAnyOrb()
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
                var near = new List<string>();
                for (int i = 0; i < 2; i++)
                {
                    Assert.That(room.TryGenerate(20, "near-" + i, 10d, out var orb, out _), Is.True);
                    near.Add(orb.id);
                }
                Assert.That(room.TryUpdateMotion(20, bomb.id, new Vector2(.3f, .6f), Vector2.zero, 12d), Is.True);
                Assert.That(room.Snapshot(14.9d).orbs.Count(orb => orb.owner == 20), Is.EqualTo(3));
                Assert.That(room.DrainBombExplosions(), Is.Empty);

                var after = room.Snapshot(15d);
                Assert.That(after.orbs.Select(orb => orb.id), Does.Not.Contain(bomb.id), "The bomb itself is gone.");
                Assert.That(after.orbs.Select(orb => orb.id), Is.SupersetOf(near),
                    "The blast loses no orb on the holder's board; it only pushes them.");
                Assert.That(after.orbs.Select(orb => orb.id), Does.Contain(kept.id), "Other boards are untouched.");
                LabBombBlast[] blasts = room.DrainBombExplosions();
                Assert.That(blasts.Length, Is.EqualTo(1));
                Assert.That(blasts[0].Owner, Is.EqualTo(20ul), "The current holder's board takes the blast.");
                Assert.That(blasts[0].BombId, Is.EqualTo(bomb.id));
                Assert.That(blasts[0].Position, Is.EqualTo(new Vector2(.3f, .6f)),
                    "It blasts where the holder's screen last had the bomb.");
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
        public void ABombHasNoLockItsFuseBurnsOnHoweverOftenItIsPassedAndItIsNeverThrown()
        {
            var bombs = ConfigWith(1f, 0, 0, 0, 0, 0, 100);
            try
            {
                var room = new LabModel();
                room.Begin("bomb-fuse", new ulong[] { 10, 20, 30 }, 0d, bombs);
                Assert.That(room.TryGenerate(10, "bomb", 0d, out var bomb, out _), Is.True);
                Assert.That(bomb.lockSwipes, Is.Zero, "A bomb has no swipes to unlock.");
                Assert.That(room.TryLaunch(10, bomb.id, "throw-new", .1d, out _, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo("RAW_CANNOT_ATTACK"), "It cannot be thrown at the target.");

                ulong[] ring = { 10, 20, 30 };
                LabOrbState passed = null;
                for (int pass = 1; pass <= 5; pass++)
                {
                    passed = PassRight(room, bomb.id, ring[(pass - 1) % ring.Length], pass);
                    Assert.That(passed.lockSwipes, Is.Zero);
                    Assert.That(passed.unlockedAt, Is.EqualTo(-1d), "Passing never plays an unlock.");
                    Assert.That(passed.createdAt, Is.Zero, "Passing never resets the fuse.");
                }
                ulong holder = passed.owner;
                Assert.That(room.TryLaunch(holder, bomb.id, "throw-passed", 6d, out _, out reason), Is.False,
                    "However often it is passed, it never opens up for throwing.");
                Assert.That(room.Snapshot(14.9d).orbs.Select(orb => orb.id), Does.Contain(bomb.id));
                Assert.That(room.Snapshot(15d).orbs.Select(orb => orb.id), Does.Not.Contain(bomb.id));
                LabBombBlast[] blasts = room.DrainBombExplosions();
                Assert.That(blasts.Length, Is.EqualTo(1), "It blasts when the item lifetime (its fuse) runs out.");
                Assert.That(blasts[0].Owner, Is.EqualTo(holder), "On the board of whoever holds it then.");

                // The DEV unlock swipes still lock the other items, never the bomb.
                float[] values = bombs.CaptureDeveloperValues();
                values[46] = 3f; // unlock swipes min
                values[47] = 3f; // unlock swipes max
                Assert.That(bombs.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var locked = new LabModel();
                locked.Begin("bomb-swipes", new ulong[] { 10, 20, 30 }, 0d, bombs);
                Assert.That(locked.TryGenerate(10, "bomb", 0d, out var stillOpen, out _), Is.True);
                Assert.That(stillOpen.lockSwipes, Is.Zero);
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

        /// <summary>
        /// A default config with one generation cost and chances in GeneratedKinds order. Five drums
        /// let every player in any room generate; drum placement has its own tests.
        /// </summary>
        [Test]
        public void RoundRecordsCountOnlySuccessfulGenerateCombineAndPass()
        {
            var (yin, yang) = GeneratePair(10);
            LabPlayerState Me(double at) => model.Snapshot(at).players.Single(p => p.id == 10);
            int generated = model.Snapshot(0d).orbs.Count(orb => orb.owner == 10);
            Assert.That(Me(0d).generatedTotal, Is.EqualTo(generated));

            Assert.That(model.TryCombine(10, yin.id, yin.id, "bad-combine", 0d, out _, out _), Is.False);
            Assert.That(Me(0d).combinedTotal, Is.Zero, "A rejected combine is not counted.");
            Assert.That(model.TryCombine(10, yin.id, yang.id, "combine", .1d, out var combined, out _), Is.True);
            Assert.That(Me(.1d).combinedTotal, Is.EqualTo(1));

            Assert.That(model.TryUpdateMotion(10, combined.id, new Vector2(.99f, .5f), Vector2.right, .2d), Is.True);
            Assert.That(model.TryTransfer(10, combined.id, true, .5f, Vector2.right, "pass", .2d, out _, out var reason),
                Is.True, reason);
            Assert.That(Me(.2d).passedTotal, Is.EqualTo(1));
            Assert.That(model.Snapshot(.2d).players.Where(p => p.id != 10).All(p => p.passedTotal == 0), Is.True);

            // A new round starts every record from zero.
            model.Tick(500d);
            Assert.That(model.Phase, Is.Not.EqualTo(LabPhase.Playing));
            model.Begin("lab-test", new ulong[] { 10, 20, 30 }, 500d, config);
            var fresh = model.Snapshot(500d).players.Single(p => p.id == 10);
            Assert.That(new[] { fresh.generatedTotal, fresh.combinedTotal, fresh.passedTotal, fresh.ItemsUsed },
                Is.EqualTo(new[] { 0, 0, 0, 0 }));
        }

        [Test]
        public void ItemsCountWhenUsedOrThrownButNotWhenRefused()
        {
            // GeneratedKinds order: Yin, Yang, Relay, SecretBox, Potion, Bomb
            void Check(int[] chances, Func<LabModel, string, bool> use, Func<LabPlayerState, int> counter, int expected)
            {
                var items = ConfigWith(1f, chances);
                try
                {
                    float[] values = items.CaptureDeveloperValues();
                    values[46] = 0f; // unlock swipes min
                    values[47] = 0f; // unlock swipes max
                    Assert.That(items.TryApplyDeveloperValues(values, out string error), Is.True, error);
                    var room = new LabModel();
                    room.Begin("items", new ulong[] { 10, 20 }, 0d, items);
                    Assert.That(room.TryGenerate(10, "item", 0d, out var item, out _), Is.True);
                    use(room, item.id);
                    var me = room.Snapshot(.5d).players.Single(p => p.id == 10);
                    Assert.That(counter(me), Is.EqualTo(expected));
                    Assert.That(me.ItemsUsed, Is.EqualTo(expected));
                }
                finally { UnityEngine.Object.DestroyImmediate(items); }
            }

            Check(new[] { 0, 0, 100, 0, 0, 0 }, (room, id) => room.TryLaunch(10, id, "relay", .5d, out _, out _),
                p => p.relayUsed, 1);
            Check(new[] { 0, 0, 0, 100, 0, 0 }, (room, id) => room.TryUseItem(10, id, "box", .5d, out _, out _),
                p => p.secretBoxUsed, 1);
            // A bomb cannot be thrown, so the refused throw is not counted.
            Check(new[] { 0, 0, 0, 0, 0, 100 }, (room, id) => room.TryLaunch(10, id, "bomb", .5d, out _, out _),
                p => p.bombUsed, 0);
            // A potion at full HP is refused and kept, so it is not counted.
            Check(new[] { 0, 0, 0, 0, 100, 0 }, (room, id) => room.TryUseItem(10, id, "potion", .5d, out _, out _),
                p => p.potionUsed, 0);
        }

        [Test]
        public void MvpAwardsShareTiesAndSkipEmptyCategories()
        {
            var players = new[]
            {
                new LabPlayerState { id = 10, seat = 0, generatedTotal = 9, combinedTotal = 7, relayUsed = 4,
                    damageTotal = 60, passedTotal = 3, drumSeconds = 40.2d },
                new LabPlayerState { id = 20, seat = 1, generatedTotal = 5, damageTotal = 60, passedTotal = 3,
                    drumSeconds = 39.8d, lastHit = true },
                new LabPlayerState { id = 30, seat = 2, generatedTotal = 2, potionUsed = 1, drumSeconds = 12d,
                    drumPassedTotal = 2 }
            };
            var won = LabResultStats.Awards(players, true);
            Assert.That(won.Select(a => a.Title), Is.EqualTo(new[]
                { LabResultTitle.MostDamage, LabResultTitle.MostPassed, LabResultTitle.MostDrumPassed,
                    LabResultTitle.LastHit, LabResultTitle.MostDrum }),
                "Generating, combining and items are records only; they give no title.");
            Assert.That(won.Select(a => LabResultStats.Label(a.Title)),
                Is.EqualTo(new[] { "딜왕", "구슬 패스왕", "북 패스왕", "막타", "북 독점" }));
            Assert.That(won[0].Winners.Select(LabResultStats.PlayerName), Is.EqualTo(new[] { "P1", "P2" }), "Ties share the title.");
            Assert.That(won[0].Value, Is.EqualTo(60));
            Assert.That(won[1].Winners.Select(LabResultStats.PlayerName), Is.EqualTo(new[] { "P1", "P2" }));
            Assert.That(won[2].Winners.Single().id, Is.EqualTo(30ul));
            Assert.That(won[3].Winners.Single().id, Is.EqualTo(20ul));
            Assert.That(won[4].Winners.Select(LabResultStats.PlayerName), Is.EqualTo(new[] { "P1", "P2" }),
                "40.2s and 39.8s both show as 40s, so the drum title is shared.");

            // 칭호 줄 글자: 누구 · 기록
            players[0].hitTotal = 2;
            players[1].hitTotal = 3;
            Assert.That(LabResultStats.WinnerNames(won[0]), Is.EqualTo("P1, P2"));
            Assert.That(LabResultStats.Record(won[0], 100d), Is.EqualTo("60 (2·3회 타격)"));
            Assert.That(LabResultStats.Record(won[1], 100d), Is.EqualTo("3회"));
            Assert.That(LabResultStats.Record(won[2], 100d), Is.EqualTo("2회"));
            Assert.That(LabResultStats.Record(won[3], 100d), Is.Empty);
            Assert.That(LabResultStats.Record(won[4], 100d), Is.EqualTo("40초 · 40%"));
            var solo = new LabResultAward(LabResultTitle.MostDamage, 40,
                new[] { new LabPlayerState { id = 30, seat = 2, damageTotal = 40, hitTotal = 2 } });
            Assert.That(LabResultStats.Record(solo, 100d), Is.EqualTo("40 (2회 타격)"));
            Assert.That(LabResultStats.WinnerNames(solo), Is.EqualTo("P3"));

            players[1].lastHit = false;
            players[0].damageTotal = players[1].damageTotal = 0;
            players[0].passedTotal = players[1].passedTotal = 0;
            players[2].drumPassedTotal = 0;
            Assert.That(LabResultStats.Awards(players, true).Select(a => a.Title),
                Is.EqualTo(new[] { LabResultTitle.MostDrum }), "Empty categories give no title.");

            players[1].lastHit = true;
            Assert.That(LabResultStats.Awards(players, false).Any(a => a.Title == LabResultTitle.LastHit), Is.False,
                "The last hit title is only for a victory.");
            Assert.That(LabResultStats.Awards(new LabPlayerState[0], true), Is.Empty);
        }

        [TestCase(1)]
        [TestCase(80)]
        [TestCase(100)]
        public void AtomicElementHitsReachCapturedPaintGoalAndResetOnNextRound(int goal)
        {
            var paintConfig = PaintFixture(goal);
            try
            {
                var room = new LabModel();
                room.BeginSolo("paint-goal-" + goal, 10, 0d, paintConfig);
                int request = 0;
                const int directions = 512;
                int hits = 0;
                for (int i = 0; i < directions && room.Phase == LabPhase.Playing; i++)
                {
                    var shot = LaunchElementShot(room, ref request, 0d);
                    Assert.That(room.ConfirmHit(shot.id, 0d, SurfaceDirection(i, directions),
                        out LabOrbElement element, out int actualDamage, out int impactValue), Is.True);
                    Assert.That(element, Is.Not.EqualTo(LabOrbElement.None));
                    Assert.That(actualDamage, Is.EqualTo(i == 0 ? 1 : 0));
                    Assert.That(impactValue, Is.EqualTo(20));
                    hits++;
                    LabSnapshot current = room.Snapshot(0d);
                    Assert.That(current.paintGoalPercent, Is.EqualTo(goal));
                    Assert.That(current.paintCoverage, Is.EqualTo(room.PaintSurface.Coverage));
                    if (current.phase == LabPhase.Playing)
                        Assert.That(current.paintCoverage, Is.LessThan(goal / 100f));
                    Assert.That(room.ConfirmHit(shot.id, 0d, Vector3.up, out _, out _, out _), Is.False,
                        "An already consumed projectile cannot repaint or receive another reward.");
                }
                LabSnapshot won = room.Snapshot(0d);
                Assert.That(won.phase, Is.EqualTo(LabPhase.Victory), "Every configured goal is reachable with distributed hits.");
                Assert.That(won.paintCoverage, Is.GreaterThanOrEqualTo(goal / 100f));
                Assert.That(won.players.Single().lastHit, Is.True);
                Assert.That(won.players.Single().hitTotal, Is.EqualTo(hits));
                Assert.That(won.players.Single().damageTotal, Is.EqualTo(hits * 20),
                    "Paint result damage remains the nominal attack score after diagnostic HP reaches zero.");
                uint completedRound = won.round;
                room.BeginSolo("paint-goal-" + goal, 10, 1d, paintConfig);
                LabSnapshot retry = room.Snapshot(1d);
                Assert.That(retry.round, Is.EqualTo(completedRound + 1));
                Assert.That(retry.phase, Is.EqualTo(LabPhase.Playing));
                Assert.That(retry.paintGoalPercent, Is.EqualTo(goal));
                Assert.That(retry.paintCoverage, Is.Zero);
                Assert.That(room.PaintSurface.PaintCount, Is.Zero);
                Assert.That(room.PaintSurface.CoveredCount, Is.Zero);
                Assert.That(retry.players.Single().lastHit, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(paintConfig); }
        }

        [Test]
        public void RepeatedPaintPast128HitsStillAcceptsNewSurfaceAndPositiveImpactAfterHpZero()
        {
            var paintConfig = PaintFixture(100);
            try
            {
                var room = new LabModel();
                room.BeginSolo("paint-repeat", 10, 0d, paintConfig);
                int request = 0;
                float firstCoverage = 0f;
                for (int i = 0; i < 128; i++)
                {
                    LabOrbState shot = LaunchElementShot(room, ref request, 0d);
                    float stamina = room.Snapshot(0d).players.Single().stamina;
                    Assert.That(room.ConfirmHit(shot.id, 0d, Vector3.forward,
                        out _, out int actualDamage, out int impactValue), Is.True);
                    Assert.That(actualDamage, Is.EqualTo(i == 0 ? 1 : 0));
                    Assert.That(impactValue, Is.EqualTo(20));
                    if (i == 0) firstCoverage = room.PaintSurface.Coverage;
                    Assert.That(room.PaintSurface.Coverage, Is.EqualTo(firstCoverage));
                    Assert.That(room.Snapshot(0d).players.Single().stamina,
                        Is.EqualTo(Mathf.Min(paintConfig.StaminaMax, stamina + paintConfig.HitRecovery)).Within(.001f));
                }
                Assert.That(firstCoverage, Is.GreaterThan(0f));
                Assert.That(room.PaintSurface.PaintCount, Is.EqualTo(128));
                LabOrbState lastShot = LaunchElementShot(room, ref request, 0d);
                Assert.That(room.ConfirmHit(lastShot.id, 0d, Vector3.back, out _, out int damage, out int impact), Is.True);
                Assert.That(damage, Is.Zero);
                Assert.That(impact, Is.EqualTo(20));
                Assert.That(room.PaintSurface.PaintCount, Is.EqualTo(129));
                Assert.That(room.PaintSurface.Coverage, Is.GreaterThan(firstCoverage));
                Assert.That(room.Phase, Is.EqualTo(LabPhase.Playing));
                Assert.That(room.Snapshot(0d).players.Single().lastHit, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(paintConfig); }
        }

        [TestCase(.999d, true)]
        [TestCase(1d, false)]
        [TestCase(1.001d, false)]
        public void PaintHitUsesTheSameAtomicDeadlineAsDamage(double hitTime, bool expected)
        {
            var paintConfig = PaintFixture(1, 1f);
            try
            {
                var room = new LabModel();
                room.BeginSolo("paint-deadline", 10, 0d, paintConfig);
                int request = 0;
                LabOrbState shot = LaunchElementShot(room, ref request, 0d);
                Assert.That(room.ConfirmHit(shot.id, hitTime, Vector3.forward,
                    out _, out int damage, out int impact), Is.EqualTo(expected));
                Assert.That(room.Phase, Is.EqualTo(expected ? LabPhase.Victory : LabPhase.Defeat));
                Assert.That(room.PaintSurface.PaintCount, Is.EqualTo(expected ? 1 : 0));
                Assert.That(damage, Is.EqualTo(expected ? 1 : 0));
                Assert.That(impact, Is.EqualTo(expected ? 20 : 0));
                Assert.That(room.Snapshot(hitTime).players.Single().lastHit, Is.EqualTo(expected));
            }
            finally { UnityEngine.Object.DestroyImmediate(paintConfig); }
        }

        [Test]
        public void PausedHostClockCannotTurnAnOtherwiseValidPaintHitIntoTimeout()
        {
            var paintConfig = PaintFixture(1, 2f);
            try
            {
                var clock = new LabBattleClock();
                var room = new LabModel();
                room.BeginSolo("paint-clock", 10, clock.Now(100d), paintConfig);
                int request = 0;
                LabOrbState shot = LaunchElementShot(room, ref request, clock.Now(100d));
                Assert.That(clock.Pause(101d), Is.True);
                LabSnapshot frozen = room.Snapshot(clock.Now(10001d));
                Assert.That(frozen.phase, Is.EqualTo(LabPhase.Playing));
                Assert.That(frozen.remaining, Is.EqualTo(1d));
                Assert.That(clock.Resume(10001d), Is.True);
                Assert.That(room.ConfirmHit(shot.id, clock.Now(10001.1d), Vector3.forward,
                    out _, out _, out _), Is.True);
                Assert.That(room.Phase, Is.EqualTo(LabPhase.Victory));
            }
            finally { UnityEngine.Object.DestroyImmediate(paintConfig); }
        }

        [Test]
        public void NoneElementAndCompatibilityOrInvalidDirectionHitsCannotCreatePaintVictory()
        {
            var paintConfig = PaintFixture(1);
            try
            {
                var room = new LabModel();
                room.BeginSolo("paint-invalid", 10, 0d, paintConfig);
                int request = 0;
                LabOrbState compatibility = LaunchElementShot(room, ref request, 0d);
                Assert.That(room.ConfirmHit(compatibility.id, 0d, out _, out int firstDamage), Is.True);
                Assert.That(firstDamage, Is.EqualTo(1));
                foreach (Vector3 invalid in new[] { Vector3.zero, new Vector3(float.NaN, 0f, 0f),
                    new Vector3(float.PositiveInfinity, 0f, 0f) })
                {
                    LabOrbState shot = LaunchElementShot(room, ref request, 0d);
                    Assert.That(room.ConfirmHit(shot.id, 0d, invalid, out _, out int damage, out int impact), Is.True);
                    Assert.That(damage, Is.Zero);
                    Assert.That(impact, Is.EqualTo(20));
                }
                Assert.That(room.PaintSurface.PaintCount, Is.Zero);
                Assert.That(room.Phase, Is.EqualTo(LabPhase.Playing));

                float[] values = paintConfig.CaptureDeveloperValues();
                values[42] = 0f; values[41] = 100f;
                Assert.That(paintConfig.TryApplyDeveloperValues(values, out string error), Is.True, error);
                room = new LabModel();
                room.BeginSolo("paint-none", 10, 0d, paintConfig);
                Assert.That(room.TryGenerate(10, "relay", 0d, out var relay, out error), Is.True, error);
                Assert.That(room.TryLaunch(10, relay.id, "relay-launch", 0d, out _, out error), Is.True, error);
                Assert.That(room.ConfirmHit(relay.id, 0d, Vector3.forward,
                    out LabOrbElement element, out int actual, out int feedback), Is.True);
                Assert.That(element, Is.EqualTo(LabOrbElement.None));
                Assert.That(actual, Is.EqualTo(1));
                Assert.That(feedback, Is.EqualTo(20));
                Assert.That(room.PaintSurface.Coverage, Is.Zero);
                Assert.That(room.Phase, Is.EqualTo(LabPhase.Playing));
            }
            finally { UnityEngine.Object.DestroyImmediate(paintConfig); }
        }

        [Test]
        public void CrackModeKeepsActualHpVictoryAndNeverPaintsItsHitDirection()
        {
            var crackConfig = PaintFixture(1);
            try
            {
                float[] values = crackConfig.CaptureDeveloperValues();
                values[79] = 1f;
                Assert.That(crackConfig.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var room = new LabModel();
                room.BeginSolo("crack-atomic", 10, 0d, crackConfig);
                int request = 0;
                LabOrbState shot = LaunchElementShot(room, ref request, 0d);
                Assert.That(room.ConfirmHit(shot.id, 0d, Vector3.forward,
                    out _, out int actual, out int impact), Is.True);
                Assert.That(actual, Is.EqualTo(1));
                Assert.That(impact, Is.EqualTo(1));
                Assert.That(room.Snapshot(0d).players.Single().damageTotal, Is.EqualTo(1));
                Assert.That(room.Phase, Is.EqualTo(LabPhase.Victory));
                Assert.That(room.PaintSurface.Coverage, Is.Zero);
                Assert.That(room.PaintSurface.PaintCount, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(crackConfig); }
        }

        [Test]
        public void PaintGoalAndPaletteAreCapturedUntilTheRoundRestarts()
        {
            var paintConfig = PaintFixture(100);
            var appearance = ScriptableObject.CreateInstance<LabOrbAppearance>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            try
            {
                Color firstColor = new Color(.12f, .34f, .56f, 1f);
                material.SetColor("_BaseColor", firstColor);
                typeof(LabOrbAppearance).GetField("fireMaterial", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(appearance, material);
                typeof(LabConfig).GetField("orbAppearance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(paintConfig, appearance);
                var room = new LabModel();
                room.BeginSolo("paint-capture", 10, 0d, paintConfig);
                Assert.That(Vector4.Distance(room.PaintColorFor(LabOrbElement.Fire), firstColor), Is.LessThan(.00001f));
                Assert.That(room.PaintColorFor(LabOrbElement.None), Is.EqualTo(Color.clear));
                material.SetColor("_BaseColor", Color.magenta);
                float[] values = paintConfig.CaptureDeveloperValues();
                values[87] = 1f;
                Assert.That(paintConfig.TryApplyDeveloperValues(values, out string error), Is.True, error);
                int request = 0;
                LabOrbState shot = LaunchElementShot(room, ref request, 0d);
                Assert.That(room.ConfirmHit(shot.id, 0d, Vector3.forward, out _, out _, out _), Is.True);
                Assert.That(room.Snapshot(0d).paintGoalPercent, Is.EqualTo(100));
                Assert.That(room.Phase, Is.EqualTo(LabPhase.Playing));
                Assert.That(Vector4.Distance(room.PaintColorFor(LabOrbElement.Fire), firstColor), Is.LessThan(.00001f));
                room.Tick(30d);
                room.BeginSolo("paint-capture", 10, 31d, paintConfig);
                Assert.That(room.Snapshot(31d).paintGoalPercent, Is.EqualTo(1));
                Assert.That(room.PaintColorFor(LabOrbElement.Fire), Is.EqualTo(Color.magenta));
                Assert.That(room.PaintSurface.Coverage, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(appearance);
                UnityEngine.Object.DestroyImmediate(paintConfig);
            }
        }

        private static LabConfig PaintFixture(int goal, float duration = 30f)
        {
            var created = ConfigWith(.001f, 0, 0, 0, 100, 0, 0);
            float[] values = created.CaptureDeveloperValues();
            values[0] = 1f;
            values[1] = 20f;
            values[2] = duration;
            values[9] = 100f;
            values[46] = 0f; values[47] = 0f;
            values[79] = 0f;
            values[87] = goal;
            Assert.That(created.TryApplyDeveloperValues(values, out string error), Is.True, error);
            return created;
        }

        private static LabOrbState LaunchElementShot(LabModel room, ref int request, double now)
        {
            var shot = room.Snapshot(now).orbs.FirstOrDefault(orb => orb.kind == LabOrbKind.Combined && !orb.inFlight);
            if (shot == null)
            {
                Assert.That(room.TryGenerate(10, "paint-box-" + request++, now, out var box, out string error), Is.True, error);
                Assert.That(box.kind, Is.EqualTo(LabOrbKind.SecretBox));
                Assert.That(room.TryUseItem(10, box.id, "paint-open-" + request++, now, out _, out error), Is.True, error);
                shot = room.Snapshot(now).orbs.First(orb => orb.kind == LabOrbKind.Combined && !orb.inFlight);
            }
            Assert.That(room.TryLaunch(10, shot.id, "paint-launch-" + request++, now, out _, out string reason), Is.True, reason);
            return shot;
        }

        private static Vector3 SurfaceDirection(int index, int count)
        {
            float y = 1f - 2f * (index + .5f) / count;
            float angle = index * 2.39996323f;
            float ring = Mathf.Sqrt(1f - y * y);
            return new Vector3(Mathf.Cos(angle) * ring, y, Mathf.Sin(angle) * ring);
        }

        [Test]
        public void HitsAndDamageAreRecordedAndOnlyTheKillingBlowIsTheLastHit()
        {
            var shortFight = ConfigWith(10f, 50, 50, 0, 0, 0, 0);
            try
            {
                float[] values = shortFight.CaptureDeveloperValues();
                values[0] = 30f; // Monster Max HP
                values[1] = 20f; // Damage per Hit
                values[79] = 1f; // Crack keeps the legacy HP finish.
                Assert.That(shortFight.TryApplyDeveloperValues(values, out string error), Is.True, error);
                model = new LabModel();
                model.Begin("hit-records", new ulong[] { 10, 20, 30 }, 0d, shortFight);

                var first = GeneratePair(10);
                Assert.That(model.TryCombine(10, first.yin.id, first.yang.id, "c1", 0d, out var a, out _), Is.True);
                Assert.That(ThrowAndHit(model, a.id, 10, .1d), Is.EqualTo(20));
                var afterFirst = model.Snapshot(.1d).players.Single(p => p.id == 10);
                Assert.That(afterFirst.hitTotal, Is.EqualTo(1));
                Assert.That(afterFirst.damageTotal, Is.EqualTo(20));
                Assert.That(afterFirst.lastHit, Is.False);

                var second = GeneratePair(20, .2d);
                Assert.That(model.TryCombine(20, second.yin.id, second.yang.id, "c2", .2d, out var b, out _), Is.True);
                ThrowAndHit(model, b.id, 20, .3d);
                var done = model.Snapshot(.3d);
                Assert.That(done.phase, Is.EqualTo(LabPhase.Victory));
                var killer = done.players.Single(p => p.id == 20);
                Assert.That(killer.hitTotal, Is.EqualTo(1));
                Assert.That(killer.damageTotal, Is.EqualTo(10), "Only the HP that was left counts as damage.");
                Assert.That(done.players.Where(p => p.lastHit).Select(p => p.id), Is.EqualTo(new ulong[] { 20 }));
                Assert.That(done.players.Single(p => p.id == 30).hitTotal, Is.EqualTo(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(shortFight); }
        }

        [Test]
        public void DefeatHasNoLastHitAndANewRoundClearsHitRecords()
        {
            var pair = GeneratePair(10);
            Assert.That(model.TryCombine(10, pair.yin.id, pair.yang.id, "c", 0d, out var combined, out _), Is.True);
            ThrowAndHit(model, combined.id, 10, .1d);
            model.Tick(180d);
            var lost = model.Snapshot(180d);
            Assert.That(lost.phase, Is.EqualTo(LabPhase.Defeat));
            Assert.That(lost.players.Single(p => p.id == 10).hitTotal, Is.EqualTo(1));
            Assert.That(lost.players.Any(p => p.lastHit), Is.False);

            model.Begin("lab-test", new ulong[] { 10, 20, 30 }, 181d, config);
            var retry = model.Snapshot(181d);
            Assert.That(retry.players.All(p => p.hitTotal == 0 && p.damageTotal == 0 && !p.lastHit), Is.True);
        }

        [Test]
        public void DrumHoldTimeFollowsTheDrumAndStopsWhenTheRoundEnds()
        {
            var single = ConfigWith(10f, 50, 50, 0, 0, 0, 0);
            try
            {
                float[] values = single.CaptureDeveloperValues();
                values[74] = 1f; // drum count
                Assert.That(single.TryApplyDeveloperValues(values, out string error), Is.True, error);
                model = new LabModel();
                model.Begin("drum-time", new ulong[] { 10, 20, 30 }, 0d, single);
                LabOrbState drum = model.Snapshot(0d).drums.Single();
                ulong first = drum.owner;

                model.Tick(10d);
                Assert.That(model.TryUpdateMotion(first, drum.id, new Vector2(.99f, .5f), Vector2.right, 10d), Is.True);
                Assert.That(model.TryTransfer(first, drum.id, true, .5f, Vector2.right, "drum-pass", 10d,
                    out var passed, out var reason), Is.True, reason);
                ulong second = passed.owner;
                Assert.That(second, Is.Not.EqualTo(first));
                var afterPass = model.Snapshot(10d).players.Single(p => p.id == first);
                Assert.That(afterPass.drumPassedTotal, Is.EqualTo(1), "Passing a drum counts as a drum pass.");
                Assert.That(afterPass.passedTotal, Is.EqualTo(0), "A drum is not counted as an orb pass.");

                var mid = model.Snapshot(40d);
                Assert.That(mid.playedSeconds, Is.EqualTo(40d).Within(1e-6));
                var a = mid.players.Single(p => p.id == first);
                var b = mid.players.Single(p => p.id == second);
                Assert.That(a.drumSeconds, Is.EqualTo(10d).Within(1e-6));
                Assert.That(b.drumSeconds, Is.EqualTo(30d).Within(1e-6));
                Assert.That(mid.players.Where(p => p.id != first && p.id != second).Single().drumSeconds, Is.EqualTo(0d));
                Assert.That(LabResultStats.DrumSharePercent(a, mid.playedSeconds), Is.EqualTo(25));
                Assert.That(LabResultStats.DrumSharePercent(b, mid.playedSeconds), Is.EqualTo(75));
                Assert.That(LabResultStats.DrumText(b, mid.playedSeconds), Is.EqualTo("30초 · 75%"));

                var ended = model.Snapshot(200d);
                Assert.That(ended.phase, Is.EqualTo(LabPhase.Defeat));
                Assert.That(ended.playedSeconds, Is.EqualTo(180d).Within(1e-6), "Time after the round ends is not counted.");
                Assert.That(ended.players.Single(p => p.id == second).drumSeconds, Is.EqualTo(170d).Within(1e-6));
                Assert.That(LabResultStats.DrumSharePercent(new LabPlayerState { drumSeconds = 5d }, 0d), Is.EqualTo(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(single); }
        }

        [Test]
        public void GourdDropsABombOnOnePlayerEveryTwentyToThirtySeconds()
        {
            var gourd = GourdConfig(20f, 30f);
            try
            {
                var ids = new ulong[] { 10, 20, 30 };
                var room = new LabModel();
                room.Begin("gourd-test", ids, 0d, gourd);
                room.Tick(19.9d);
                Assert.That(room.DrainGourdBombs(), Is.Empty, "No bomb falls before the minimum interval.");

                // Check every second so each new bomb is read well before its fuse.
                var dropTimes = new List<double>();
                for (int second = 20; second <= 180; second++)
                {
                    LabSnapshot snapshot = room.Snapshot(second);
                    foreach (LabGourdBomb drop in room.DrainGourdBombs())
                    {
                        Assert.That(ids, Does.Contain(drop.Target));
                        Assert.That(drop.Position.x, Is.InRange(.2f, .8f));
                        Assert.That(drop.Position.y, Is.InRange(0f, 1f));
                        LabOrbState bomb = snapshot.orbs.Single(orb => orb.id == drop.BombId);
                        Assert.That(bomb.kind, Is.EqualTo(LabOrbKind.Bomb));
                        Assert.That(bomb.owner, Is.EqualTo(drop.Target));
                        Assert.That(bomb.origin, Is.EqualTo(LabOrbOrigin.Gourd));
                        Assert.That(bomb.lockSwipes, Is.Zero, "A gourd bomb has no lock, only its fuse.");
                        Assert.That(bomb.inFlight, Is.False);
                        Assert.That(new Vector2(bomb.x, bomb.y), Is.EqualTo(drop.Position));
                        dropTimes.Add(bomb.createdAt);
                    }
                    Assert.That(room.DrainGourdBombs(), Is.Empty, "Each drop is reported once.");
                }

                Assert.That(dropTimes.Count, Is.InRange(6, 9), "A 180 second round has 6 to 9 gourd bombs.");
                Assert.That(dropTimes[0], Is.InRange(20d, 30d));
                for (int i = 1; i < dropTimes.Count; i++)
                    Assert.That(dropTimes[i] - dropTimes[i - 1], Is.InRange(20d - 1e-6, 30d + 1e-6));
                LabSnapshot end = room.Snapshot(180d);
                Assert.That(end.players.All(player => player.generatedTotal == 0), Is.True,
                    "Gourd bombs are not anyone's generation.");
            }
            finally { UnityEngine.Object.DestroyImmediate(gourd); }
        }

        [Test]
        public void GourdBombsReachEveryPlayerAndCanRepeatOnTheSamePlayer()
        {
            // The shortest interval (1 s) drops 180 bombs in a round, enough to see the spread.
            var gourd = GourdConfig(1f, 1f);
            try
            {
                var ids = new ulong[] { 10, 20, 30 };
                var room = new LabModel();
                room.Begin("gourd-spread", ids, 0d, gourd);
                room.Tick(180d);
                LabGourdBomb[] drops = room.DrainGourdBombs();
                Assert.That(drops.Length, Is.EqualTo(180));
                foreach (ulong id in ids)
                    Assert.That(drops.Count(drop => drop.Target == id), Is.InRange(35, 85), id.ToString());
                Assert.That(drops.Zip(drops.Skip(1), (a, b) => a.Target == b.Target).Any(same => same), Is.True,
                    "The same player may receive two bombs in a row.");
                Assert.That(drops.Min(drop => drop.Position.x), Is.LessThan(.35f));
                Assert.That(drops.Max(drop => drop.Position.x), Is.GreaterThan(.65f));
                Assert.That(drops.Select(drop => drop.BombId).Distinct().Count(), Is.EqualTo(180));

                // The same session and round always pick the same players at the same times.
                var again = new LabModel();
                again.Begin("gourd-spread", ids, 0d, gourd);
                again.Tick(180d);
                Assert.That(again.DrainGourdBombs().Select(drop => drop.Target), Is.EqualTo(drops.Select(drop => drop.Target)));
            }
            finally { UnityEngine.Object.DestroyImmediate(gourd); }
        }

        [Test]
        public void GourdBombLandsOnAFullBoardWithoutSpendingStamina()
        {
            var gourd = GourdConfig(5f, 5f);
            try
            {
                float[] values = gourd.CaptureDeveloperValues();
                values[9] = 1f; // stored orb limit
                Assert.That(gourd.TryApplyDeveloperValues(values, out string error), Is.True, error);
                var room = new LabModel();
                room.BeginSolo("gourd-full", 7ul, 0d, gourd);
                Assert.That(room.TryGenerate(7ul, "fill", 4d, out _, out string reason), Is.True, reason);
                Assert.That(room.TryGenerate(7ul, "over", 4d, out _, out reason), Is.False);
                Assert.That(reason, Is.EqualTo("STORAGE_FULL"));
                float stamina = room.Snapshot(4.9d).players[0].stamina;

                LabSnapshot after = room.Snapshot(5d);
                LabGourdBomb drop = room.DrainGourdBombs().Single();
                Assert.That(drop.Target, Is.EqualTo(7ul));
                Assert.That(after.orbs.Length, Is.EqualTo(2), "The gourd ignores the stored orb limit.");
                Assert.That(after.orbs.Single(orb => orb.id == drop.BombId).origin, Is.EqualTo(LabOrbOrigin.Gourd));
                Assert.That(after.players[0].stamina, Is.GreaterThanOrEqualTo(stamina), "Gourd bombs cost no stamina.");
            }
            finally { UnityEngine.Object.DestroyImmediate(gourd); }
        }

        [Test]
        public void GourdBombsStopWhenTheMaximumIntervalIsZeroOrTheRoundIsOver()
        {
            var off = ConfigWith(10f, 50, 50, 0, 0, 0, 0);
            var gourd = GourdConfig(20f, 30f);
            try
            {
                var quiet = new LabModel();
                quiet.Begin("gourd-off", new ulong[] { 10, 20, 30 }, 0d, off);
                quiet.Tick(180d);
                Assert.That(quiet.DrainGourdBombs(), Is.Empty);
                Assert.That(quiet.Snapshot(180d).orbs, Is.Empty);

                var ended = new LabModel();
                ended.Begin("gourd-ended", new ulong[] { 10, 20, 30 }, 0d, gourd);
                ended.Tick(180d);
                ended.DrainGourdBombs();
                Assert.That(ended.Phase, Is.EqualTo(LabPhase.Defeat));
                ended.Tick(400d);
                Assert.That(ended.DrainGourdBombs(), Is.Empty, "No bomb falls after the round ends.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(off);
                UnityEngine.Object.DestroyImmediate(gourd);
            }
        }

        private static LabConfig GourdConfig(float minimumSeconds, float maximumSeconds)
        {
            LabConfig created = ConfigWith(10f, 50, 50, 0, 0, 0, 0);
            float[] values = created.CaptureDeveloperValues();
            values[88] = minimumSeconds;
            values[89] = maximumSeconds;
            Assert.That(created.TryApplyDeveloperValues(values, out string error), Is.True, error);
            return created;
        }

        private static LabConfig ConfigWith(float generateCost, params int[] chances)
        {
            var created = ScriptableObject.CreateInstance<LabConfig>();
            float[] values = created.CaptureDeveloperValues();
            values[5] = generateCost;
            values[74] = LabModel.MaximumParticipants;
            // The gourd's sabotage bombs are off here; GourdConfig turns them on for their own tests.
            values[88] = 0f;
            values[89] = 0f;
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
