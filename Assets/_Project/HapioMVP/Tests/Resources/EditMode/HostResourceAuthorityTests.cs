using System;
using System.Collections.Generic;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Resources.Tests
{
    public sealed class HostResourceAuthorityTests
    {
        private const double Rate = 20d / 3d;
        private HostOrbRegistry registry;
        private HostResourceAuthority authority;

        [SetUp]
        public void SetUp()
        {
            registry = new HostOrbRegistry(true);
            registry.BeginSession("resource-session", 1);
            authority = Make(registry);
            authority.BeginRound(new ulong[] { 0, 1 }, 0);
        }

        [Test]
        public void RoundStartsFullForEachPlayerWithNoPreGeneratedOrbsOrInitialBatch()
        {
            Assert.That(authority.IsPlaying, Is.True);
            Assert.That(authority.IsEnded, Is.False);
            Assert.That(registry.Snapshot(), Is.Empty);
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(100));
            Assert.That(authority.GetPlayer(1).Stamina, Is.EqualTo(100));
            Assert.That(authority.GetPlayer(0).GeneratedTotal, Is.Zero);
            Assert.That(authority.GetPlayer(0).LastSequence, Is.Zero);
            Assert.That(authority.GetPlayer(0).RegenerationRate, Is.EqualTo(Rate));
        }

        [Test]
        public void EveryTapIncludingFirstGeneratesOneRawAndCostsTwentyOnlyForSender()
        {
            for (ulong sequence = 1; sequence <= 5; sequence++)
            {
                var result = authority.Generate(0, Request(sequence), 0);
                Assert.That(result.Accepted, Is.True);
                Assert.That(result.Orb.Kind, Is.EqualTo(OrbKind.Raw));
                Assert.That(result.Orb.Polarity, Is.EqualTo(OrbPolarity.Yin).Or.EqualTo(OrbPolarity.Yang));
                Assert.That(result.Orb.OwnerPlayerId, Is.Zero);
                Assert.That(result.Orb.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
                Assert.That(result.StaminaBefore - result.StaminaAfter, Is.EqualTo(20));
                Assert.That(registry.Snapshot().Count, Is.EqualTo(sequence));
            }
            Assert.That(authority.GetPlayer(0).Stamina, Is.Zero);
            Assert.That(authority.GetPlayer(1).Stamina, Is.EqualTo(100));
            Assert.That(authority.Generate(0, Request(6), 0).Reason, Is.EqualTo("INSUFFICIENT_STAMINA"));
            Assert.That(registry.Snapshot().Count, Is.EqualTo(5));
            Assert.That(authority.GetPlayer(0).GeneratedTotal, Is.EqualTo(5));
        }

        [Test]
        public void GeneratedPositionsAreDistinctAndNormalizedAtFullStorageCapacity()
        {
            var positions = new HashSet<Vector2>();
            for (ulong sequence = 1; sequence <= 20; sequence++)
            {
                double now = (sequence - 1) * 3d;
                var result = authority.Generate(0, Request(sequence), now);
                Assert.That(result.Accepted, Is.True);
                Assert.That(positions.Add(result.Orb.NormalizedPosition), Is.True);
                Assert.That(result.Orb.NormalizedPosition.x, Is.InRange(0f, 1f));
                Assert.That(result.Orb.NormalizedPosition.y, Is.InRange(0f, 1f));
            }
            Assert.That(authority.CountStoredOrbs(0), Is.EqualTo(20));
            double before = authority.GetPlayer(0).Stamina;
            Assert.That(authority.Generate(0, Request(21), 57).Reason, Is.EqualTo("STORAGE_FULL"));
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(before));
        }

        [Test]
        public void ApprovedDuplicateAndQueryReturnOriginalOrbWithoutASecondCost()
        {
            var request = Request(1);
            var result = authority.Generate(0, request, 0);
            var duplicate = authority.Generate(0, request, 0);
            Assert.That(duplicate.Accepted, Is.True);
            Assert.That(duplicate.IsDuplicate, Is.True);
            Assert.That(duplicate.Orb, Is.SameAs(result.Orb));
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(80));
            Assert.That(registry.Snapshot().Count, Is.EqualTo(1));
            var query = authority.Query(0, "resource-session", 1, request.RequestId);
            Assert.That(query.Known, Is.True);
            Assert.That(query.Receipt.IsDuplicate, Is.True);
            Assert.That(query.Receipt.Orb.OrbId, Is.EqualTo(result.Orb.OrbId));
            Assert.That(authority.Query(1, "resource-session", 1, request.RequestId).Reason, Is.EqualTo("REQUEST_OWNER_MISMATCH"));
        }

        [Test]
        public void RejectedRequestStaysRejectedAfterRecoveryAndDoesNotAdvancePolarity()
        {
            for (ulong sequence = 1; sequence <= 5; sequence++) authority.Generate(0, Request(sequence), 0);
            var sixth = Request(6);
            Assert.That(authority.Generate(0, sixth, 0).Accepted, Is.False);
            authority.Advance(3, true);
            var duplicate = authority.Generate(0, sixth, 3);
            Assert.That(duplicate.Accepted, Is.False);
            Assert.That(duplicate.IsDuplicate, Is.True);
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(20).Within(1e-10));
            Assert.That(authority.GetPlayer(0).GeneratedTotal, Is.EqualTo(5));
            Assert.That(authority.Generate(0, Request(7), 3).Accepted, Is.True);
        }

        [TestCase("sender", "UNKNOWN_PLAYER")]
        [TestCase("session", "SESSION_MISMATCH")]
        [TestCase("round", "ROUND_MISMATCH")]
        [TestCase("empty-id", "INVALID_REQUEST_ID")]
        [TestCase("long-id", "INVALID_REQUEST_ID")]
        [TestCase("zero-sequence", "STALE_SEQUENCE")]
        public void InvalidGenerationDoesNotChangeResourcesOrRegistry(string scenario, string reason)
        {
            var request = new GenerateRequest(scenario == "session" ? "stale" : "resource-session",
                scenario == "round" ? 2u : 1u,
                scenario == "empty-id" ? " " : scenario == "long-id" ? new string('x', 129) : "request",
                scenario == "zero-sequence" ? 0UL : 1UL);
            Assert.That(authority.Generate(scenario == "sender" ? 7UL : 0UL, request, 0).Reason, Is.EqualTo(reason));
            Assert.That(registry.Snapshot(), Is.Empty);
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(100));
            Assert.That(authority.GetPlayer(0).GeneratedTotal, Is.Zero);
        }

        [Test]
        public void RequestIdCannotChangeSenderOrPayloadAndOldSequencesCannotBecomeNewActions()
        {
            authority.Generate(0, Request(2), 0);
            Assert.That(authority.Generate(1, Request(2), 0).Reason, Is.EqualTo("REQUEST_ID_CONFLICT"));
            Assert.That(authority.Generate(0, new GenerateRequest("resource-session", 1, "generate-2", 3), 0).Reason,
                Is.EqualTo("REQUEST_ID_CONFLICT"));
            Assert.That(authority.Generate(0, Request(1), 0).Reason, Is.EqualTo("STALE_SEQUENCE"));
            Assert.That(registry.Snapshot().Count, Is.EqualTo(1));
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(80));
        }

        [Test]
        public void PreviouslyOwnedRawAndCombinedIncludingPendingCountTowardLimit()
        {
            for (int i = 0; i < 19; i++) Add(0, OrbKind.Raw);
            var combined = Add(0);
            var reserved = registry.Reserve(0, new OrbActionRequest("resource-session", 1,
                "pending", combined.OrbId, null, OrbActionKind.Launch, 1, Vector2.one * .5f));
            Assert.That(reserved.Accepted, Is.True);
            Assert.That(authority.CountStoredOrbs(0), Is.EqualTo(20));
            Assert.That(authority.Generate(0, Request(1), 0).Reason, Is.EqualTo("STORAGE_FULL"));
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(100));
            Assert.That(authority.GetPlayer(0).GeneratedTotal, Is.Zero);
            Assert.That(authority.Generate(1, new GenerateRequest("resource-session", 1, "other", 1), 0).Accepted, Is.True);
        }

        [Test]
        public void LaunchingIsConservativelyStoredButProjectileAndConsumedDoNotOccupyStorage()
        {
            var attack = Attack();
            var combined = Add(0);
            var request = LaunchRequest(combined);
            Assert.That(attack.RequestLaunch(0, request).Accepted, Is.True);
            Assert.That(authority.CountStoredOrbs(0), Is.EqualTo(1));
            Assert.That(attack.MarkProjectileSpawned("resource-session", 1, combined.OrbId), Is.True);
            Assert.That(authority.CountStoredOrbs(0), Is.Zero);
            attack.ProcessHostHit("resource-session", 1, combined.OrbId);
            Assert.That(authority.CountStoredOrbs(0), Is.Zero);
        }

        [Test]
        public void SameSeedAndPlayerReproducePolarityDespiteOtherPlayerRequestsAndRejectedCommands()
        {
            var secondRegistry = new HostOrbRegistry(false);
            secondRegistry.BeginSession("resource-session", 1);
            var second = Make(secondRegistry);
            second.BeginRound(new ulong[] { 0, 1 }, 0);
            var observed = new List<OrbPolarity>();
            for (ulong sequence = 1; sequence <= 5; sequence++)
            {
                var expected = second.Generate(0, Request(sequence), 0).Orb.Polarity;
                authority.Generate(7, Request(999, "unknown-" + sequence), 0);
                authority.Generate(1, Request(sequence, "other-" + sequence), 0);
                var actual = authority.Generate(0, Request(sequence), 0);
                observed.Add(actual.Orb.Polarity);
                Assert.That(actual.Orb.Polarity, Is.EqualTo(expected));
            }
            Assert.That(observed.Distinct().Count(), Is.EqualTo(2), "Known deterministic seed should exercise both polarities.");
            Assert.That(observed, Is.EqualTo(new[] { OrbPolarity.Yang, OrbPolarity.Yang,
                OrbPolarity.Yin, OrbPolarity.Yin, OrbPolarity.Yang }));
        }

        [TestCase(0.15, 1)]
        [TestCase(1.5, 10)]
        [TestCase(3, 20)]
        public void AutomaticRecoveryIsContinuousAndUsesTrustedElapsedHostTime(double elapsed, double expected)
        {
            SpendAll();
            authority.Advance(elapsed, true);
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(expected).Within(1e-9));
            Assert.That(authority.GetPlayer(1).Stamina, Is.EqualTo(100));
        }

        [Test]
        public void FractionalAdvanceMatchesOneLargeAdvanceAndRepeatedTimeDoesNotHeal()
        {
            SpendAll();
            authority.Advance(.75, true);
            authority.Advance(1.5, true);
            authority.Advance(1.5, true);
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(10).Within(1e-9));
        }

        [Test]
        public void FloatConfigRateAtThreeSecondsCanPayTwentyWithoutNegativeRemainder()
        {
            var another = NewStarted(new ResourceTuning(100, 100, 20, 20f / 3f, 5, 20), out _);
            for (ulong i = 1; i <= 5; i++) another.Generate(0, Request(i), 0);
            var afterThree = another.Generate(0, Request(6), 3);
            Assert.That(afterThree.Accepted, Is.True);
            Assert.That(another.GetPlayer(0).Stamina, Is.Zero);
        }

        [Test]
        public void MaximumDiscardsExcessTimeAndRecoveryCannotBeBankedForLaterSpending()
        {
            authority.Advance(300, true);
            Assert.That(authority.Generate(0, Request(1), 300).StaminaAfter, Is.EqualTo(80));
            authority.Advance(300, true);
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(80));
            authority.Advance(301.5, true);
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(90).Within(1e-9));
            authority.Advance(double.MaxValue, true);
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(100));
        }

        [Test]
        public void PausedIntervalDoesNotRecoverAndResumeDoesNotCatchUpItsTime()
        {
            SpendAll();
            authority.Advance(1.5, false);
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(10).Within(1e-9));
            Assert.That(authority.Generate(0, Request(6), 50).Reason, Is.EqualTo("BATTLE_NOT_PLAYING"));
            authority.Advance(100, true);
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(10).Within(1e-9));
            authority.Advance(101.5, true);
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(20).Within(1e-9));
        }

        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidHostTimeCannotChangeAnyPlayer(double value)
        {
            SpendAll();
            Assert.Throws<ArgumentOutOfRangeException>(() => authority.Advance(value, true));
            Assert.That(authority.GetPlayer(0).Stamina, Is.Zero);
        }

        [Test]
        public void DecreasingHostTimeIsRejectedBeforeMutation()
        {
            SpendAll();
            authority.Advance(1.5, true);
            Assert.Throws<ArgumentOutOfRangeException>(() => authority.Advance(1, true));
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(10).Within(1e-9));
        }

        [Test]
        public void EndStopsGenerationAndRegenerationPermanentlyUntilNewRound()
        {
            SpendAll();
            authority.EndRound(0);
            authority.Advance(100, true);
            Assert.That(authority.IsEnded, Is.True);
            Assert.That(authority.IsPlaying, Is.False);
            Assert.That(authority.GetPlayer(0).Stamina, Is.Zero);
            Assert.That(authority.Generate(0, Request(6), 100).Reason, Is.EqualTo("BATTLE_NOT_PLAYING"));
        }

        [Test]
        public void ResetRequiresNewEmptyRoundRestoresFullAndRejectsOldCommandsAndQueries()
        {
            SpendAll();
            Assert.Throws<InvalidOperationException>(() => authority.BeginRound(new ulong[] { 0, 1 }, 0));
            registry.ResetRound(2);
            authority.BeginRound(new ulong[] { 0, 1 }, 10);
            Assert.That(registry.Snapshot(), Is.Empty);
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(100));
            Assert.That(authority.GetPlayer(0).LastSequence, Is.Zero);
            Assert.That(authority.GetPlayer(0).GeneratedTotal, Is.Zero);
            Assert.That(authority.Generate(0, Request(1), 10).Reason, Is.EqualTo("ROUND_MISMATCH"));
            Assert.That(authority.Query(0, "resource-session", 1, "generate-1").Reason, Is.EqualTo("ROUND_MISMATCH"));
            Assert.That(authority.Generate(0, new GenerateRequest("resource-session", 2, "generate-1", 1), 10).Accepted, Is.True);
        }

        [Test]
        public void RegistryResetWithoutResourceBindingCannotKeepOldResourcesRunning()
        {
            SpendAll();
            registry.ResetRound(2);
            authority.Advance(100, true);
            Assert.That(authority.GetPlayer(0).Stamina, Is.Zero);
            Assert.That(authority.IsPlaying, Is.False);
        }

        [Test]
        public void LateParticipantStartsFullAndEmptyWithoutResettingExistingPlayers()
        {
            var anotherRegistry = new HostOrbRegistry(false);
            anotherRegistry.BeginSession("resource-session", 1);
            var another = Make(anotherRegistry);
            another.BeginRound(new ulong[] { 0 }, 0);
            another.Generate(0, Request(1), 0);
            Assert.That(another.AddParticipant(1, 1.5), Is.True);
            Assert.That(another.GetPlayer(0).Stamina, Is.EqualTo(90).Within(1e-9));
            Assert.That(another.GetPlayer(1).Stamina, Is.EqualTo(100));
            Assert.That(another.CountStoredOrbs(1), Is.Zero);
            Assert.That(another.AddParticipant(1, 1.5), Is.False);
            Assert.That(another.AddParticipant(2, 1.5), Is.False);
            Assert.That(another.GetPlayer(1).GeneratedTotal, Is.Zero);
        }

        [Test]
        public void ValidHostHitAddsFiveOnlyToActualAttackerAndOnlyOnce()
        {
            authority.Generate(0, Request(1), 0);
            authority.Generate(1, Request(1, "player-1"), 0);
            var attack = Attack();
            var hit = Hit(attack, Add(1));
            var recovery = authority.ApplyValidHit(hit, 0);
            Assert.That(recovery.Accepted, Is.True);
            Assert.That(recovery.Added, Is.EqualTo(5));
            Assert.That(recovery.PlayerId, Is.EqualTo(1));
            Assert.That(authority.GetPlayer(1).Stamina, Is.EqualTo(85));
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(80));
            var duplicate = authority.ApplyValidHit(hit, 0);
            Assert.That(duplicate.IsDuplicate, Is.True);
            Assert.That(duplicate.Applied, Is.False);
            Assert.That(authority.GetPlayer(1).Stamina, Is.EqualTo(85));
        }

        [Test]
        public void HitAtMaximumStillConsumesRewardIdSoReplayAfterSpendingCannotHeal()
        {
            var hit = Hit(Attack(), Add(0));
            var full = authority.ApplyValidHit(hit, 0);
            Assert.That(full.Accepted, Is.True);
            Assert.That(full.Added, Is.Zero);
            authority.Generate(0, Request(1), 0);
            Assert.That(authority.ApplyValidHit(hit, 0).IsDuplicate, Is.True);
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(80));
        }

        [Test]
        public void HitRecoveryClampsAtMaximum()
        {
            authority.Generate(0, Request(1), 0);
            authority.Advance(2.7, true);
            var recovery = authority.ApplyValidHit(Hit(Attack(), Add(0)), 2.7);
            Assert.That(recovery.StaminaBefore, Is.EqualTo(98).Within(1e-9));
            Assert.That(recovery.StaminaAfter, Is.EqualTo(100));
            Assert.That(recovery.Added, Is.EqualTo(2).Within(1e-9));
        }

        [Test]
        public void FinalKillingHitCanRecoverAfterTargetClearedStopsTimerBeforeEndRound()
        {
            authority.Generate(0, Request(1), 0);
            var attack = Attack(20);
            var hit = Hit(attack, Add(0));
            Assert.That(attack.State, Is.EqualTo(AttackBattleState.TargetCleared));
            authority.Advance(0, false);
            Assert.That(authority.ApplyValidHit(hit, 0).Added, Is.EqualTo(5));
            authority.EndRound(0);
            authority.Advance(100, true);
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(85));
        }

        [Test]
        public void MissExpiryInvalidHitAndPostEndHitDoNotRecover()
        {
            authority.Generate(0, Request(1), 0);
            var attack = Attack();
            var miss = Add(0);
            Spawn(attack, miss);
            Assert.That(attack.ExpireProjectile("resource-session", 1, miss.OrbId), Is.True);
            Assert.That(authority.ApplyValidHit(attack.ProcessHostHit("resource-session", 1, miss.OrbId), 0).Accepted, Is.False);
            Assert.That(authority.ApplyValidHit(null, 0).Accepted, Is.False);
            var delayed = Hit(attack, Add(0));
            authority.EndRound(0);
            Assert.That(authority.ApplyValidHit(delayed, 0).Reason, Is.EqualTo("ROUND_ENDED"));
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(80));
        }

        [Test]
        public void OldRoundHitCannotRecoverAfterReset()
        {
            var oldHit = Hit(Attack(), Add(0));
            registry.ResetRound(2);
            authority.BeginRound(new ulong[] { 0, 1 }, 0);
            authority.Generate(0, new GenerateRequest("resource-session", 2, "new", 1), 0);
            Assert.That(authority.ApplyValidHit(oldHit, 0).Reason, Is.EqualTo("ROUND_MISMATCH"));
            Assert.That(authority.GetPlayer(0).Stamina, Is.EqualTo(80));
        }

        [Test]
        public void PlayerSnapshotsAreImmutableAndSorted()
        {
            var snapshot = authority.Snapshot();
            var before = authority.GetPlayer(0);
            authority.Generate(0, Request(1), 0);
            Assert.That(before.Stamina, Is.EqualTo(100));
            Assert.That(snapshot[0].PlayerId, Is.Zero);
            Assert.That(snapshot[0].Stamina, Is.EqualTo(100));
            Assert.That(snapshot[1].PlayerId, Is.EqualTo(1));
            Assert.That(authority.GetPlayer(7), Is.Null);
        }

        [Test]
        public void NormalRegistryCanGenerateWithoutEnablingDevelopmentFixtures()
        {
            var normal = new HostOrbRegistry(false);
            normal.BeginSession("resource-session", 1);
            var service = Make(normal);
            service.BeginRound(new ulong[] { 0 }, 0);
            Assert.That(service.Generate(0, Request(1), 0).Accepted, Is.True);
            Assert.Throws<InvalidOperationException>(() => normal.RegisterDevelopmentOrb(0, OrbKind.Combined,
                OrbPolarity.None, Vector2.one * .5f));
        }

        [Test]
        public void GeneratedRegistryPathRejectsWrongContextAndInvalidRawBeforeAddingAnything()
        {
            Assert.Throws<InvalidOperationException>(() => registry.RegisterGeneratedRaw("wrong", 1, 0, OrbPolarity.Yin, Vector2.zero));
            Assert.Throws<InvalidOperationException>(() => registry.RegisterGeneratedRaw("resource-session", 2, 0, OrbPolarity.Yin, Vector2.zero));
            Assert.Throws<ArgumentException>(() => registry.RegisterGeneratedRaw("resource-session", 1, 0, OrbPolarity.None, Vector2.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => registry.RegisterGeneratedRaw("resource-session", 1, 0, OrbPolarity.Yin, new Vector2(float.NaN, 0)));
            Assert.That(registry.Snapshot(), Is.Empty);
        }

        [Test]
        public void RoundStartValidationDoesNotClearAnExistingRoundOrAcceptNonemptyNewOne()
        {
            var fresh = new HostOrbRegistry(true);
            fresh.BeginSession("new", 1);
            var service = Make(fresh);
            Assert.Throws<ArgumentException>(() => service.BeginRound(new ulong[] { 0, 0 }, 0));
            Assert.Throws<ArgumentException>(() => service.BeginRound(new ulong[] { 0, 1, 2 }, 0));
            Assert.Throws<ArgumentException>(() => service.BeginRound(Array.Empty<ulong>(), 0));
            Assert.Throws<ArgumentNullException>(() => service.BeginRound(null, 0));
            fresh.RegisterDevelopmentOrb(0, OrbKind.Raw, OrbPolarity.Yin, Vector2.zero);
            Assert.Throws<InvalidOperationException>(() => service.BeginRound(new ulong[] { 0 }, 0));
            Assert.That(fresh.Snapshot().Count, Is.EqualTo(1));
        }

        [Test]
        public void InvalidTuningAndDefaultStructCannotEnableAResourceAuthority()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceTuning(0, 0, 20, Rate, 5, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceTuning(100, 101, 20, Rate, 5, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceTuning(100, 100, 0, Rate, 5, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceTuning(100, 100, 20, double.NaN, 5, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceTuning(100, 100, 20, Rate, -1, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceTuning(100, 100, 20, Rate, 5, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HostResourceAuthority(registry, default, 1));
            Assert.Throws<ArgumentNullException>(() => new HostResourceAuthority(null, new ResourceTuning(100, 100, 20, Rate, 5, 20), 1));
        }

        private HostResourceAuthority Make(HostOrbRegistry target)
            => new HostResourceAuthority(target, new ResourceTuning(100, 100, 20, Rate, 5, 20), 1707);

        private HostResourceAuthority NewStarted(ResourceTuning tuning, out HostOrbRegistry target)
        {
            target = new HostOrbRegistry(true);
            target.BeginSession("resource-session", 1);
            var service = new HostResourceAuthority(target, tuning, 1707);
            service.BeginRound(new ulong[] { 0, 1 }, 0);
            return service;
        }

        private GenerateRequest Request(ulong sequence, string requestId = null)
            => new GenerateRequest("resource-session", 1, requestId ?? "generate-" + sequence, sequence);
        private void SpendAll()
        { for (ulong sequence = 1; sequence <= 5; sequence++) Assert.That(authority.Generate(0, Request(sequence), 0).Accepted, Is.True); }
        private OrbRecord Add(ulong owner, OrbKind kind = OrbKind.Combined)
            => registry.RegisterDevelopmentOrb(owner, kind, kind == OrbKind.Raw ? OrbPolarity.Yin : OrbPolarity.None, Vector2.one * .5f);
        private AttackAuthority Attack(int hp = 100)
        {
            var attack = new AttackAuthority(registry, hp, 20);
            attack.BeginDevelopmentRound();
            return attack;
        }
        private OrbActionRequest LaunchRequest(OrbRecord orb) => new OrbActionRequest("resource-session", 1,
            "launch-" + orb.OrbId, orb.OrbId, null, OrbActionKind.Launch, 1, Vector2.one * .5f);
        private void Spawn(AttackAuthority attack, OrbRecord orb)
        {
            Assert.That(attack.RequestLaunch(orb.OwnerPlayerId, LaunchRequest(orb)).Accepted, Is.True);
            Assert.That(attack.MarkProjectileSpawned("resource-session", 1, orb.OrbId), Is.True);
        }
        private AttackHitResult Hit(AttackAuthority attack, OrbRecord orb)
        {
            Spawn(attack, orb);
            var hit = attack.ProcessHostHit("resource-session", 1, orb.OrbId);
            Assert.That(hit.Applied, Is.True);
            return hit;
        }
    }
}
