using System;
using System.Linq;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using C6.Prototype.Resources;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Combination.Tests
{
    public sealed class HostCombinationAuthorityTests
    {
        private HostOrbRegistry registry;
        private HostCombinationAuthority authority;
        private HostResourceAuthority resources;

        [SetUp]
        public void SetUp()
        {
            registry = new HostOrbRegistry(true);
            registry.BeginSession("session-a", 1);
            resources = new HostResourceAuthority(registry, new ResourceTuning(100, 100, 20, 20d / 3d, 5, 20), 731);
            resources.BeginRound(new[] { 0ul, 7ul }, 0);
            authority = new HostCombinationAuthority(registry);
            authority.BeginRound();
        }

        [TestCase(OrbPolarity.Yin, OrbPolarity.Yang)]
        [TestCase(OrbPolarity.Yang, OrbPolarity.Yin)]
        public void OppositeRawBothOrdersAtomicallyConsumeTwoAndCreateFreshCombined(OrbPolarity first, OrbPolarity second)
        {
            var source = Add(first, owner: 7); var target = Add(second, owner: 7);
            var result = authority.Combine(7, Request(source, target));
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.IsDuplicate, Is.False);
            Assert.That(result.Reason, Is.EqualTo("COMBINATION_APPROVED"));
            Assert.That(registry.Snapshot().Count, Is.EqualTo(3));
            Assert.That(result.Source.OrbId, Is.EqualTo(source.OrbId));
            Assert.That(result.Target.OrbId, Is.EqualTo(target.OrbId));
            Assert.That(result.Source.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
            Assert.That(result.Target.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
            Assert.That(result.Combined.OrbId, Is.Not.EqualTo(source.OrbId).And.Not.EqualTo(target.OrbId));
            Assert.That(Guid.TryParseExact(result.Combined.OrbId, "N", out _), Is.True);
            Assert.That(result.Combined.Kind, Is.EqualTo(OrbKind.Combined));
            Assert.That(result.Combined.Polarity, Is.EqualTo(OrbPolarity.None));
            Assert.That(result.Combined.CanAttack, Is.True);
            Assert.That(result.Combined.CanCombine, Is.False);
            Assert.That(source.CanAttack, Is.False);
            Assert.That(source.CanCombine, Is.True);
            Assert.That(result.Combined.OwnerPlayerId, Is.EqualTo(7ul));
            Assert.That(result.Combined.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
            Assert.That(result.Combined.SequenceNumber, Is.Zero);
            Assert.That(registry.IsPending(source.OrbId) || registry.IsPending(target.OrbId), Is.False);
            Assert.That(source.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle), "Previously delivered immutable snapshot stays intact.");
            Assert.That(resources.GetPlayer(7).Stamina, Is.EqualTo(100));
        }

        [Test]
        public void ResultUsesValidatedLocalDropMidpointRatherThanStaleGridPositions()
        {
            var source = Add(OrbPolarity.Yin); var target = Add(OrbPolarity.Yang);
            var result = authority.Combine(0, Request(source, target, sourcePosition: new Vector2(.4f, .6f), targetPosition: new Vector2(.6f, .8f)));
            Assert.That(result.Combined.NormalizedPosition.x, Is.EqualTo(.5f).Within(.000001f));
            Assert.That(result.Combined.NormalizedPosition.y, Is.EqualTo(.7f).Within(.000001f));
            Assert.That(result.Source.NormalizedPosition, Is.EqualTo(new Vector2(.4f, .6f)));
            Assert.That(result.Target.NormalizedPosition, Is.EqualTo(new Vector2(.6f, .8f)));
        }

        [TestCase(OrbPolarity.Yin)]
        [TestCase(OrbPolarity.Yang)]
        public void SamePolarityDenialPreservesBothRecordsAndAllResources(OrbPolarity polarity)
        {
            resources.Generate(0, new GenerateRequest("session-a", 1, "spend", 1), 0);
            var source = Add(polarity); var target = Add(polarity);
            AssertUnchanged(source, target, () => authority.Combine(0, Request(source, target)), "INVALID_COMBINATION");
            Assert.That(resources.GetPlayer(0).Stamina, Is.EqualTo(80));
            Assert.That(resources.GetPlayer(0).GeneratedTotal, Is.EqualTo(1));
        }

        [TestCase("same-id", "SAME_ORB")]
        [TestCase("other-owner", "OTHER_OWNER_MISMATCH")]
        [TestCase("wrong-sender", "OWNER_MISMATCH")]
        [TestCase("raw-combined", "INVALID_COMBINATION")]
        [TestCase("combined-raw", "INVALID_COMBINATION")]
        [TestCase("combined-combined", "INVALID_COMBINATION")]
        [TestCase("missing-target", "OTHER_ORB_NOT_FOUND")]
        [TestCase("missing-source", "ORB_NOT_FOUND")]
        public void ForbiddenInputsNeverPartiallyConsumeOrReserve(string scenario, string reason)
        {
            var source = scenario.StartsWith("combined", StringComparison.Ordinal) ? Combined() : Add(OrbPolarity.Yin);
            var target = scenario == "same-id" ? source : scenario.EndsWith("combined", StringComparison.Ordinal)
                ? Combined() : Add(OrbPolarity.Yang, scenario == "other-owner" ? 7ul : 0ul);
            var request = new CombinationRequest("session-a", 1, "combine", scenario == "missing-source" ? "missing" : source.OrbId,
                scenario == "missing-target" ? "missing" : target.OrbId, 1, Vector2.one * .5f, Vector2.one * .5f);
            AssertUnchanged(source, target, () => authority.Combine(scenario == "wrong-sender" ? 7ul : 0ul, request), reason);
        }

        [TestCase(false, -.001f, .5f)]
        [TestCase(false, .5f, 1.001f)]
        [TestCase(false, float.NaN, .5f)]
        [TestCase(true, .5f, float.PositiveInfinity)]
        [TestCase(true, float.NegativeInfinity, .5f)]
        [TestCase(true, .5f, float.NaN)]
        public void InvalidEitherPositionRejectsBeforeAnyReservation(bool targetInvalid, float x, float y)
        {
            var source = Add(OrbPolarity.Yin); var target = Add(OrbPolarity.Yang);
            AssertUnchanged(source, target, () => authority.Combine(0, Request(source, target,
                sourcePosition: targetInvalid ? Vector2.one * .5f : new Vector2(x, y),
                targetPosition: targetInvalid ? new Vector2(x, y) : Vector2.one * .5f)), "INVALID_POSITION");
            Assert.That(authority.Combine(0, Request(source, target, id: "corrected")).Accepted, Is.True);
        }

        [Test]
        public void BothNormalizedBoundaryPositionsAreAccepted()
        {
            var result = authority.Combine(0, Request(Add(OrbPolarity.Yin), Add(OrbPolarity.Yang),
                sourcePosition: Vector2.zero, targetPosition: Vector2.one));
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Combined.NormalizedPosition, Is.EqualTo(Vector2.one * .5f));
        }

        [Test]
        public void ExactDuplicatePreservesOriginalNewIdAndNeverConsumesAgain()
        {
            var source = Add(OrbPolarity.Yin); var target = Add(OrbPolarity.Yang);
            var first = authority.Combine(0, Request(source, target));
            authority.SetPlaying(false);
            var duplicate = authority.Combine(0, Request(source, target));
            Assert.That(duplicate.Accepted && duplicate.IsDuplicate, Is.True);
            Assert.That(duplicate.Combined, Is.SameAs(first.Combined));
            Assert.That(registry.Snapshot().Count, Is.EqualTo(3));
            Assert.That(registry.Snapshot().Count(x => x.AuthorityState == OrbAuthorityState.Idle), Is.EqualTo(1));
        }

        [TestCase("sender")]
        [TestCase("source")]
        [TestCase("target")]
        [TestCase("sequence")]
        [TestCase("source-position")]
        [TestCase("target-position")]
        public void ReusedRequestIdWithChangedPayloadCannotReplaceReceipt(string field)
        {
            var source = Add(OrbPolarity.Yin); var target = Add(OrbPolarity.Yang); var extra = Add(OrbPolarity.Yang);
            var original = authority.Combine(0, Request(source, target));
            var changed = Request(field == "source" ? extra : source, field == "target" ? extra : target,
                sequence: field == "sequence" ? 2ul : 1ul,
                sourcePosition: field == "source-position" ? Vector2.zero : Vector2.one * .5f,
                targetPosition: field == "target-position" ? Vector2.one : Vector2.one * .5f);
            AssertRejected(authority.Combine(field == "sender" ? 7ul : 0ul, changed), "REQUEST_ID_CONFLICT");
            Assert.That(authority.Combine(0, Request(source, target)).Combined, Is.SameAs(original.Combined));
            Assert.That(registry.TryGet(extra.OrbId, out var retained), Is.True);
            Assert.That(retained, Is.SameAs(extra));
        }

        [Test]
        public void RejectedReceiptIsStableAndFreshCorrectedRequestCanSucceed()
        {
            var source = Add(OrbPolarity.Yin); var same = Add(OrbPolarity.Yin); var opposite = Add(OrbPolarity.Yang);
            AssertRejected(authority.Combine(0, Request(source, same)), "INVALID_COMBINATION");
            var duplicate = authority.Combine(0, Request(source, same));
            Assert.That(duplicate.IsDuplicate, Is.True);
            AssertRejected(duplicate, "INVALID_COMBINATION");
            Assert.That(authority.Combine(0, Request(source, opposite, id: "corrected")).Accepted, Is.True);
        }

        [TestCase(false, "ORB_NOT_IDLE")]
        [TestCase(true, "OTHER_ORB_NOT_IDLE")]
        public void AlreadyConsumedMaterialCannotBeUsedByAnotherRequest(bool consumedTarget, string reason)
        {
            var source = Add(OrbPolarity.Yin); var target = Add(OrbPolarity.Yang);
            authority.Combine(0, Request(source, target));
            var fresh = Add(consumedTarget ? OrbPolarity.Yin : OrbPolarity.Yang);
            AssertRejected(authority.Combine(0, Request(consumedTarget ? fresh : source, consumedTarget ? target : fresh,
                id: "again", sequence: 2)), reason);
            Assert.That(registry.TryGet(fresh.OrbId, out var retained), Is.True);
            Assert.That(retained, Is.SameAs(fresh));
            Assert.That(registry.IsPending(fresh.OrbId), Is.False);
        }

        [TestCase(false, "ORB_PENDING")]
        [TestCase(true, "OTHER_ORB_PENDING")]
        public void TransferReservationOnEitherMaterialWinsWithoutPartiallyLockingOther(bool targetReserved, string reason)
        {
            var source = Add(OrbPolarity.Yin); var target = Add(OrbPolarity.Yang);
            var locked = targetReserved ? target : source; var free = targetReserved ? source : target;
            var transfer = registry.Reserve(0, new OrbActionRequest("session-a", 1, "transfer", locked.OrbId, null,
                OrbActionKind.TransferRight, 1, Vector2.one * .5f));
            Assert.That(transfer.Accepted, Is.True);
            var before = registry.Snapshot();
            AssertRejected(authority.Combine(0, Request(source, target, sequence: 2)), reason);
            Assert.That(registry.Snapshot(), Is.EqualTo(before));
            Assert.That(registry.IsPending(free.OrbId), Is.False);
            Assert.That(registry.TryGetPending(locked.OrbId, out var held), Is.True);
            Assert.That(held, Is.SameAs(transfer.Reservation));
        }

        [Test]
        public void CombinationReservationBlocksAnotherPairAndLaunchOnEitherMaterial()
        {
            var source = Add(OrbPolarity.Yin); var target = Add(OrbPolarity.Yang); var extra = Add(OrbPolarity.Yang);
            var held = registry.Reserve(0, new OrbActionRequest("session-a", 1, "reserved", source.OrbId, target.OrbId,
                OrbActionKind.Combine, 1, Vector2.one * .5f));
            Assert.That(held.Accepted, Is.True);
            AssertRejected(authority.Combine(0, Request(source, extra, sequence: 2)), "ORB_PENDING");
            var launch = registry.Reserve(0, new OrbActionRequest("session-a", 1, "launch", target.OrbId, null,
                OrbActionKind.Launch, 2, Vector2.one * .5f));
            Assert.That(launch.Accepted, Is.False);
            Assert.That(launch.Reason, Is.EqualTo("ORB_PENDING"));
            Assert.That(registry.IsPending(extra.OrbId), Is.False);
        }

        [Test]
        public void StaleSequenceRejectedAndNewCombinedHasIndependentLaunchSequence()
        {
            var source = Add(OrbPolarity.Yin); var target = Add(OrbPolarity.Yang);
            AssertUnchanged(source, target, () => authority.Combine(0, Request(source, target, sequence: 0)), "STALE_SEQUENCE");
            var result = authority.Combine(0, Request(source, target, id: "valid", sequence: 11));
            var attack = new AttackAuthority(registry, 100, 20); attack.BeginDevelopmentRound();
            var launch = attack.RequestLaunch(0, new OrbActionRequest("session-a", 1, "launch", result.Combined.OrbId,
                null, OrbActionKind.Launch, 1, Vector2.one * .5f));
            Assert.That(launch.Accepted, Is.True);
            Assert.That(launch.Orb.OrbId, Is.EqualTo(result.Combined.OrbId));
            Assert.That(attack.MarkProjectileSpawned("session-a", 1, result.Combined.OrbId), Is.True);
            Assert.That(attack.ProcessHostHit("session-a", 1, result.Combined.OrbId).Applied, Is.True);
            Assert.That(attack.MonsterHp, Is.EqualTo(80));
        }

        [Test]
        public void RawAttackRejectionStillPreservesMaterialsForRealCombination()
        {
            var source = Add(OrbPolarity.Yin); var target = Add(OrbPolarity.Yang);
            var attack = new AttackAuthority(registry, 100, 20); attack.BeginDevelopmentRound();
            var raw = attack.RequestLaunch(0, new OrbActionRequest("session-a", 1, "raw", source.OrbId, null,
                OrbActionKind.Launch, 1, Vector2.one * .5f));
            Assert.That(raw.Accepted, Is.False);
            Assert.That(raw.Reason, Is.EqualTo("RAW_CANNOT_LAUNCH"));
            Assert.That(attack.MonsterHp, Is.EqualTo(100));
            Assert.That(authority.Combine(0, Request(source, target)).Accepted, Is.True);
        }

        [Test]
        public void ActualCombinedHitUsesExistingAttackerOnlyFiveRecoveryOnce()
        {
            resources.Generate(7, new GenerateRequest("session-a", 1, "spend", 1), 0);
            var combination = authority.Combine(7, Request(Add(OrbPolarity.Yin, 7), Add(OrbPolarity.Yang, 7)));
            Assert.That(resources.GetPlayer(7).Stamina, Is.EqualTo(80));
            var attack = new AttackAuthority(registry, 100, 20); attack.BeginDevelopmentRound();
            attack.RequestLaunch(7, new OrbActionRequest("session-a", 1, "launch", combination.Combined.OrbId, null,
                OrbActionKind.Launch, 1, Vector2.one * .5f));
            attack.MarkProjectileSpawned("session-a", 1, combination.Combined.OrbId);
            var hit = attack.ProcessHostHit("session-a", 1, combination.Combined.OrbId);
            Assert.That(resources.ApplyValidHit(hit, 0).Added, Is.EqualTo(5));
            Assert.That(resources.ApplyValidHit(hit, 0).IsDuplicate, Is.True);
            Assert.That(resources.GetPlayer(7).Stamina, Is.EqualTo(85));
            Assert.That(resources.GetPlayer(0).Stamina, Is.EqualTo(100));
        }

        [TestCase("session-b", 1u, "SESSION_MISMATCH")]
        [TestCase("session-a", 0u, "ROUND_MISMATCH")]
        [TestCase("session-a", 2u, "ROUND_MISMATCH")]
        public void StaleContextDoesNotConsumeOrCacheRequest(string session, uint round, string reason)
        {
            var source = Add(OrbPolarity.Yin); var target = Add(OrbPolarity.Yang);
            var request = new CombinationRequest(session, round, "combine", source.OrbId, target.OrbId, 1,
                Vector2.one * .5f, Vector2.one * .5f);
            AssertUnchanged(source, target, () => authority.Combine(0, request), reason);
            Assert.That(authority.Combine(0, Request(source, target)).Accepted, Is.True);
        }

        [Test]
        public void PausedAndEndedRoundRejectFreshRequestsWithoutResourceOrOrbChanges()
        {
            var source = Add(OrbPolarity.Yin); var target = Add(OrbPolarity.Yang);
            authority.SetPlaying(false);
            AssertUnchanged(source, target, () => authority.Combine(0, Request(source, target)), "BATTLE_NOT_PLAYING");
            authority.SetPlaying(true);
            authority.EndRound(); authority.SetPlaying(true);
            Assert.That(authority.IsPlaying, Is.False);
            AssertUnchanged(source, target, () => authority.Combine(0, Request(source, target, id: "ended")), "BATTLE_NOT_PLAYING");
        }

        [Test]
        public void ResetInvalidatesOldReceiptsAndRequiresExplicitNewRoundBinding()
        {
            var source = Add(OrbPolarity.Yin); var target = Add(OrbPolarity.Yang);
            authority.Combine(0, Request(source, target));
            Assert.Throws<InvalidOperationException>(() => authority.BeginRound());
            registry.ResetRound(2);
            AssertRejected(authority.Combine(0, Request(source, target)), "ROUND_MISMATCH");
            var freshSource = Add(OrbPolarity.Yin); var freshTarget = Add(OrbPolarity.Yang);
            var fresh = new CombinationRequest("session-a", 2, "combine", freshSource.OrbId, freshTarget.OrbId, 1,
                Vector2.one * .5f, Vector2.one * .5f);
            AssertRejected(authority.Combine(0, fresh), "ROUND_NOT_STARTED");
            authority.BeginRound();
            Assert.That(authority.Query(0, "session-a", 2, "combine", source.OrbId, target.OrbId).Known, Is.False);
            Assert.That(authority.Combine(0, fresh).Accepted, Is.True);
        }

        [Test]
        public void UnknownQueryNeverAuthorizesSuccessAndDoesNotExposeAnotherOwnersRecords()
        {
            var source = Add(OrbPolarity.Yin); var target = Add(OrbPolarity.Yang, 7);
            var unknown = authority.Query(0, "session-a", 1, "unknown", source.OrbId, target.OrbId);
            Assert.That(unknown.Known, Is.False); Assert.That(unknown.Receipt, Is.Null);
            Assert.That(unknown.Source, Is.SameAs(source)); Assert.That(unknown.Target, Is.Null);
            Assert.That(registry.IsPending(source.OrbId), Is.False);
            Assert.That(registry.Snapshot().Count, Is.EqualTo(2));
        }

        [Test]
        public void KnownQueryValidatesSenderAndBothIdsAndReturnsCurrentCombinedState()
        {
            var source = Add(OrbPolarity.Yin); var target = Add(OrbPolarity.Yang);
            var first = authority.Combine(0, Request(source, target));
            Assert.That(authority.Query(7, "session-a", 1, "combine", source.OrbId, target.OrbId).Reason, Is.EqualTo("REQUEST_OWNER_MISMATCH"));
            Assert.That(authority.Query(0, "session-a", 1, "combine", target.OrbId, source.OrbId).Reason, Is.EqualTo("REQUEST_ORB_MISMATCH"));
            var attack = new AttackAuthority(registry, 100, 20); attack.BeginDevelopmentRound();
            attack.RequestLaunch(0, new OrbActionRequest("session-a", 1, "launch", first.Combined.OrbId, null,
                OrbActionKind.Launch, 1, Vector2.one * .5f));
            var known = authority.Query(0, "session-a", 1, "combine", source.OrbId, target.OrbId);
            Assert.That(known.Known && known.Receipt.Accepted && known.Receipt.IsDuplicate, Is.True);
            Assert.That(known.Combined.AuthorityState, Is.EqualTo(OrbAuthorityState.Launching));
            Assert.That(known.Receipt.Combined.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
            Assert.That(known.Source.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
            Assert.That(known.SourcePending || known.TargetPending, Is.False);
        }

        [Test]
        public void RegistryCommitRejectsStaleForeignAndMalformedReservationWithoutPartialMutation()
        {
            var source = Add(OrbPolarity.Yin); var target = Add(OrbPolarity.Yang);
            var action = new OrbActionRequest("session-a", 1, "held", source.OrbId, target.OrbId,
                OrbActionKind.Combine, 1, Vector2.one * .5f);
            var held = registry.Reserve(0, action);
            var before = registry.Snapshot();
            Assert.That(registry.TryCompleteReservedCombination(held.Reservation, new Vector2(float.NaN, 0), out _, out _, out _), Is.False);
            Assert.That(registry.TryCompleteReservedCombination(null, Vector2.zero, out _, out _, out _), Is.False);
            var foreign = new HostOrbRegistry(true); foreign.BeginSession("session-a", 1);
            Assert.That(foreign.TryCompleteReservedCombination(held.Reservation, Vector2.zero, out _, out _, out _), Is.False);
            Assert.That(registry.Snapshot(), Is.EqualTo(before));
            Assert.That(registry.IsPending(source.OrbId) && registry.IsPending(target.OrbId), Is.True);
            Assert.That(registry.TryCompleteReservedCombination(held.Reservation, Vector2.zero, out _, out _, out var combined), Is.True);
            Assert.That(registry.TryCompleteReservedCombination(held.Reservation, Vector2.zero, out _, out _, out _), Is.False);
            Assert.That(registry.Snapshot().Count, Is.EqualTo(3));
            Assert.That(registry.TryGet(combined.OrbId, out _), Is.True);
            registry.ResetRound(2);
            Assert.That(registry.TryCompleteReservedCombination(held.Reservation, Vector2.zero, out _, out _, out _), Is.False);
            Assert.That(registry.Snapshot(), Is.Empty);
        }

        [Test]
        public void FullInventoryCombinationFreesOneSlotAndGenerationReturnsToTwenty()
        {
            var source = Add(OrbPolarity.Yin); var target = Add(OrbPolarity.Yang);
            for (int index = 2; index < 20; index++) Add(OrbPolarity.Yin);
            Assert.That(resources.CountStoredOrbs(0), Is.EqualTo(20));
            Assert.That(authority.Combine(0, Request(source, target)).Accepted, Is.True);
            Assert.That(resources.CountStoredOrbs(0), Is.EqualTo(19));
            Assert.That(resources.GetPlayer(0).Stamina, Is.EqualTo(100));
            var generation = resources.Generate(0, new GenerateRequest("session-a", 1, "generate", 1), 0);
            Assert.That(generation.Accepted, Is.True);
            Assert.That(resources.CountStoredOrbs(0), Is.EqualTo(20));
            Assert.That(resources.GetPlayer(0).Stamina, Is.EqualTo(80));
        }

        [Test]
        public void ReceiptBoundRejectsNewWorkButPreservesEarlierDecisionAndQuery()
        {
            var source = Add(OrbPolarity.Yin); var target = Add(OrbPolarity.Yin); var opposite = Add(OrbPolarity.Yang);
            for (int index = 0; index < HostCombinationAuthority.MaximumRequestsPerPlayer; index++)
                AssertRejected(authority.Combine(0, Request(source, target, id: "reject-" + index)), "INVALID_COMBINATION");
            AssertRejected(authority.Combine(0, Request(source, opposite, id: "limit")), "REQUEST_LIMIT");
            var prior = authority.Combine(0, Request(source, target, id: "reject-0"));
            Assert.That(prior.IsDuplicate, Is.True);
            Assert.That(authority.Query(0, "session-a", 1, "reject-0", source.OrbId, target.OrbId).Known, Is.True);
            Assert.That(registry.IsPending(source.OrbId), Is.False);
            Assert.That(registry.Snapshot().Count, Is.EqualTo(3));
        }

        [Test]
        public void MissingSessionRoundOrRequestCannotStartGameplay()
        {
            var fresh = new HostCombinationAuthority(registry);
            AssertRejected(fresh.Combine(0, Request(Add(OrbPolarity.Yin), Add(OrbPolarity.Yang))), "ROUND_NOT_STARTED");
            AssertRejected(authority.Combine(0, null), "MISSING_REQUEST");
            AssertRejected(authority.Combine(0, Request(Add(OrbPolarity.Yin), Add(OrbPolarity.Yang), id: " ")), "INVALID_REQUEST_ID");
            registry.ClearSession();
            AssertRejected(authority.Combine(0, new CombinationRequest("session-a", 1, "missing", "a", "b", 1,
                Vector2.zero, Vector2.zero)), "NO_ACTIVE_SESSION");
            Assert.Throws<InvalidOperationException>(() => fresh.BeginRound());
        }

        private OrbRecord Add(OrbPolarity polarity, ulong owner = 0)
            => registry.RegisterDevelopmentOrb(owner, OrbKind.Raw, polarity, new Vector2(.1f, .125f));
        private OrbRecord Combined() => registry.RegisterDevelopmentOrb(0, OrbKind.Combined, OrbPolarity.None, Vector2.one * .5f);
        private static CombinationRequest Request(OrbRecord source, OrbRecord target, string id = "combine", ulong sequence = 1,
            Vector2? sourcePosition = null, Vector2? targetPosition = null)
            => new CombinationRequest("session-a", 1, id, source.OrbId, target.OrbId, sequence,
                sourcePosition ?? Vector2.one * .5f, targetPosition ?? Vector2.one * .5f);
        private void AssertUnchanged(OrbRecord source, OrbRecord target, Func<CombinationResult> action, string reason)
        {
            var before = registry.Snapshot(); var p0 = resources.GetPlayer(0); var p7 = resources.GetPlayer(7);
            AssertRejected(action(), reason);
            Assert.That(registry.Snapshot(), Is.EqualTo(before));
            Assert.That(registry.IsPending(source.OrbId) || registry.IsPending(target.OrbId), Is.False);
            Assert.That(resources.GetPlayer(0).Stamina, Is.EqualTo(p0.Stamina));
            Assert.That(resources.GetPlayer(7).Stamina, Is.EqualTo(p7.Stamina));
            Assert.That(resources.GetPlayer(0).GeneratedTotal, Is.EqualTo(p0.GeneratedTotal));
            Assert.That(resources.GetPlayer(7).GeneratedTotal, Is.EqualTo(p7.GeneratedTotal));
        }
        private static void AssertRejected(CombinationResult result, string reason)
        { Assert.That(result.Accepted, Is.False); Assert.That(result.Reason, Is.EqualTo(reason)); Assert.That(result.Combined, Is.Null); }
    }
}
