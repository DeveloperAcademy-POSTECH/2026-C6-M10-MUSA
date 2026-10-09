using System;
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
            config = ScriptableObject.CreateInstance<LabConfig>();
            model = new LabModel();
            model.Begin("lab-test", new ulong[] { 10, 20, 30 }, 0d, config);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(config);

        [Test]
        public void DefaultsAndExactlyThreeSeatsAreVisibleInAnIndependentSnapshot()
        {
            var snapshot = model.Snapshot(0d);
            Assert.That(snapshot.phase, Is.EqualTo(LabPhase.Playing));
            Assert.That(snapshot.hp, Is.EqualTo(1000));
            Assert.That(snapshot.remaining, Is.EqualTo(180d));
            Assert.That(snapshot.players.Select(player => player.id), Is.EqualTo(new ulong[] { 10, 20, 30 }));
            Assert.That(snapshot.players.Select(player => player.seat), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(snapshot.players.All(player => player.stamina == 100f), Is.True);
            Assert.That(snapshot.orbs, Is.Empty);

            snapshot.players[0].stamina = 0f;
            Assert.That(model.Snapshot(0d).players[0].stamina, Is.EqualTo(100f));
            Assert.Throws<ArgumentException>(() => new LabModel().Begin("bad", new ulong[] { 1, 2 }, 0d, config));
            Assert.Throws<ArgumentException>(() => new LabModel().Begin("bad", new ulong[] { 1, 1, 2 }, 0d, config));
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
            Assert.That(start.hp, Is.EqualTo(1000));

            LabOrbState yin = null, yang = null;
            for (int i = 0; i < 4 && (yin == null || yang == null); i++)
            {
                Assert.That(solo.TryGenerate(42ul, "solo-generate-" + i, 0d, out var raw, out _), Is.True);
                Assert.That(raw.y, Is.GreaterThanOrEqualTo(.43f),
                    "Generated orbs must be above the bottom HUD's covered part of the board.");
                if (raw.kind == LabOrbKind.Yin) yin = raw;
                else yang = raw;
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
        public void DeveloperSoloRetryResetsOnlyItsOwnPlayerAndKeepsThreeSeatBeginStrict()
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
            Assert.Throws<ArgumentException>(() => new LabModel().Begin("not-three", new[] { 7ul }, 0d, config));
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
            Assert.That(model.Snapshot(0d).players.Single(player => player.id == 10).stamina, Is.EqualTo(80f));
            Assert.That(model.Snapshot(0d).orbs.Length, Is.EqualTo(1));

            model.Tick(3d);
            Assert.That(model.Snapshot(3d).players.Single(player => player.id == 10).stamina, Is.EqualTo(100f).Within(.001f));
            Assert.That(model.Snapshot(3d).players.Single(player => player.id == 20).stamina, Is.EqualTo(100f));
        }

        [Test]
        public void OnlyOwnersOppositeRawMayCombineAndCombinationStartsFreshLifetime()
        {
            var raw = new LabOrbState[4];
            for (int i = 0; i < raw.Length; i++)
                Assert.That(model.TryGenerate(10, "generate-" + i, 0d, out raw[i], out _), Is.True);
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
            Assert.That(retry.orbs, Is.Empty);
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

        private (LabOrbState yin, LabOrbState yang) GeneratePair(ulong owner, double at = 0d)
        {
            LabOrbState yin = null, yang = null;
            for (int i = 0; i < 4 && (yin == null || yang == null); i++)
            {
                Assert.That(model.TryGenerate(owner, "pair-" + owner + "-" + at + "-" + i, at,
                    out var orb, out _), Is.True);
                if (orb.kind == LabOrbKind.Yin) yin = orb;
                else yang = orb;
            }
            Assert.That(yin, Is.Not.Null);
            Assert.That(yang, Is.Not.Null);
            return (yin, yang);
        }
    }
}
