using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Orbs.Tests
{
    public sealed class HostOrbRegistryTests
    {
        private HostOrbRegistry registry;

        [SetUp]
        public void SetUp()
        {
            registry = new HostOrbRegistry(true);
            registry.BeginSession("session-a", 1);
        }

        [Test]
        public void DevelopmentSupplyRequiresExplicitModeAndAnActiveSession()
        {
            var normal = new HostOrbRegistry(false);
            normal.BeginSession("normal", 1);
            Assert.Throws<InvalidOperationException>(() => normal.RegisterDevelopmentOrb(0, OrbKind.Raw, OrbPolarity.Yin, Vector2.one * .5f));
            var inactive = new HostOrbRegistry(true);
            Assert.Throws<InvalidOperationException>(() => inactive.RegisterDevelopmentOrb(0, OrbKind.Raw, OrbPolarity.Yin, Vector2.one * .5f));
            Assert.That(normal.Snapshot(), Is.Empty);
            Assert.That(inactive.Snapshot(), Is.Empty);
        }

        [TestCase(OrbKind.Raw, OrbPolarity.None)]
        [TestCase(OrbKind.Combined, OrbPolarity.Yin)]
        [TestCase(OrbKind.Combined, OrbPolarity.Yang)]
        [TestCase((OrbKind)99, OrbPolarity.None)]
        public void InvalidFixtureKindPolarityCannotEnterRegistry(OrbKind kind, OrbPolarity polarity)
        {
            Assert.Throws<ArgumentException>(() => registry.RegisterDevelopmentOrb(0, kind, polarity, Vector2.one * .5f));
            Assert.That(registry.Snapshot(), Is.Empty);
        }

        [Test]
        public void FixtureIdsAreUniqueAndSnapshotsAreStableAndReadOnly()
        {
            var first = Add(OrbKind.Raw, OrbPolarity.Yin);
            var snapshot = registry.Snapshot();
            var second = Add(OrbKind.Raw, OrbPolarity.Yin);
            Assert.That(first.OrbId, Is.Not.Empty.And.Not.EqualTo(second.OrbId));
            Assert.That(snapshot.Count, Is.EqualTo(1));
            Assert.That(registry.Snapshot().Count, Is.EqualTo(2));
            Assert.Throws<NotSupportedException>(() => ((IList<OrbRecord>)snapshot).Add(second));
            Assert.That(first.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
            Assert.That(first.EntrySide, Is.EqualTo(EntrySide.None));
            Assert.That(first.SequenceNumber, Is.Zero);
        }

        [TestCase("other-session", 1u, "SESSION_MISMATCH")]
        [TestCase("session-a", 0u, "ROUND_MISMATCH")]
        [TestCase("session-a", 2u, "ROUND_MISMATCH")]
        public void WrongSessionOrRoundCannotReserve(string session, uint round, string reason)
        {
            var orb = Add();
            var result = registry.Reserve(0, Request(orb, session: session, round: round));
            AssertRejected(result, reason);
            Assert.That(registry.IsPending(orb.OrbId), Is.False);
            Assert.That(registry.Reserve(0, Request(orb)).Accepted, Is.True);
        }

        [Test]
        public void MissingRequestsIdsOrSourceCannotReserveAndDoNotChangeFixture()
        {
            var orb = Add();
            AssertRejected(registry.Reserve(0, null), "MISSING_REQUEST");
            AssertRejected(registry.Reserve(0, Request(orb, id: "  ")), "MISSING_REQUEST_ID");
            var missing = new OrbActionRequest("session-a", 1, "missing", "unknown", null,
                OrbActionKind.TransferLeft, 1, Vector2.one * .5f);
            AssertRejected(registry.Reserve(0, missing), "ORB_NOT_FOUND");
            Assert.That(registry.Snapshot().Count, Is.EqualTo(1));
            Assert.That(registry.IsPending(orb.OrbId), Is.False);
            Assert.That(registry.TryGet(null, out _), Is.False);
            Assert.That(registry.TryGetPending(null, out _), Is.False);
        }

        [Test]
        public void AuthenticatedSenderMustOwnTheOrb()
        {
            var orb = Add(owner: 7);
            AssertRejected(registry.Reserve(8, Request(orb)), "OWNER_MISMATCH");
            Assert.That(registry.IsPending(orb.OrbId), Is.False);
            Assert.That(registry.Reserve(7, Request(orb, id: "owner-request")).Accepted, Is.True);
        }

        [Test]
        public void RawLaunchRejectsWithoutDeletingOrAdvancingSequence()
        {
            var orb = Add(OrbKind.Raw, OrbPolarity.Yin);
            AssertRejected(registry.Reserve(0, Request(orb, OrbActionKind.Launch)), "RAW_CANNOT_LAUNCH");
            Assert.That(registry.TryGet(orb.OrbId, out var retained), Is.True);
            Assert.That(retained, Is.SameAs(orb));
            Assert.That(registry.IsPending(orb.OrbId), Is.False);
            Assert.That(registry.Reserve(0, Request(orb, OrbActionKind.TransferLeft, id: "transfer", sequence: 1)).Accepted, Is.True);
        }

        [TestCase(OrbActionKind.Launch)]
        [TestCase(OrbActionKind.TransferLeft)]
        [TestCase(OrbActionKind.TransferRight)]
        public void ActionReservationKeepsConfirmedIdentityOwnershipPositionAndState(OrbActionKind kind)
        {
            var orb = Add();
            var request = Request(orb, kind, sequence: 4, position: new Vector2(.9f, .8f));
            var result = registry.Reserve(0, request);
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.IsDuplicate, Is.False);
            Assert.That(result.Reason, Is.EqualTo("RESERVED_ONLY"));
            Assert.That(result.Reservation.Request, Is.SameAs(request));
            Assert.That(result.Reservation.ReservedOrbIds, Is.EqualTo(new[] { orb.OrbId }));
            Assert.That(registry.TryGet(orb.OrbId, out var confirmed), Is.True);
            Assert.That(confirmed, Is.SameAs(orb));
            Assert.That(confirmed.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
            Assert.That(confirmed.NormalizedPosition, Is.EqualTo(Vector2.one * .5f));
            Assert.That(confirmed.SequenceNumber, Is.Zero);
            Assert.That(registry.TryGetPending(orb.OrbId, out var pending), Is.True);
            Assert.That(pending, Is.SameAs(result.Reservation));
        }

        [Test]
        public void CombineReservesBothMaterialsAtomicallyWithoutConsumingOrCreating()
        {
            var yin = Add(OrbKind.Raw, OrbPolarity.Yin);
            var yang = Add(OrbKind.Raw, OrbPolarity.Yang);
            var result = registry.Reserve(0, Request(yin, OrbActionKind.Combine, yang.OrbId));
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Reservation.ReservedOrbIds, Is.EquivalentTo(new[] { yin.OrbId, yang.OrbId }));
            Assert.That(registry.TryGetPending(yin.OrbId, out var yinReservation), Is.True);
            Assert.That(registry.TryGetPending(yang.OrbId, out var yangReservation), Is.True);
            Assert.That(yinReservation, Is.SameAs(yangReservation));
            Assert.That(registry.Snapshot().Count, Is.EqualTo(2));
            Assert.That(registry.TryGet(yin.OrbId, out var confirmedYin), Is.True);
            Assert.That(registry.TryGet(yang.OrbId, out var confirmedYang), Is.True);
            Assert.That(confirmedYin, Is.SameAs(yin));
            Assert.That(confirmedYang, Is.SameAs(yang));
        }

        [TestCase("same-id", "SAME_ORB")]
        [TestCase("same-polarity", "INVALID_COMBINATION")]
        [TestCase("other-owner", "OTHER_OWNER_MISMATCH")]
        [TestCase("raw-combined", "INVALID_COMBINATION")]
        [TestCase("combined-combined", "INVALID_COMBINATION")]
        [TestCase("missing", "OTHER_ORB_NOT_FOUND")]
        public void InvalidCombinationPreservesBothMaterialsAndCreatesNoLock(string scenario, string reason)
        {
            var first = scenario == "combined-combined" ? Add() : Add(OrbKind.Raw, OrbPolarity.Yin);
            var second = scenario == "same-id" ? first :
                scenario == "raw-combined" || scenario == "combined-combined" ? Add() :
                Add(OrbKind.Raw, scenario == "same-polarity" ? OrbPolarity.Yin : OrbPolarity.Yang,
                    scenario == "other-owner" ? 1ul : 0ul);
            var before = registry.Snapshot();
            var result = registry.Reserve(0, Request(first, OrbActionKind.Combine,
                scenario == "missing" ? "unknown" : second.OrbId));
            AssertRejected(result, reason);
            Assert.That(registry.Snapshot(), Is.EqualTo(before));
            Assert.That(registry.IsPending(first.OrbId), Is.False);
            Assert.That(registry.IsPending(second.OrbId), Is.False);
        }

        [Test]
        public void AlreadyReservedCombinationTargetCannotPartiallyLockSource()
        {
            var yin = Add(OrbKind.Raw, OrbPolarity.Yin);
            var yang = Add(OrbKind.Raw, OrbPolarity.Yang);
            var targetReservation = registry.Reserve(0, Request(yang, id: "target-transfer"));
            Assert.That(targetReservation.Accepted, Is.True);
            AssertRejected(registry.Reserve(0, Request(yin, OrbActionKind.Combine, yang.OrbId)), "OTHER_ORB_PENDING");
            Assert.That(registry.IsPending(yin.OrbId), Is.False);
            Assert.That(registry.TryGetPending(yang.OrbId, out var current), Is.True);
            Assert.That(current, Is.SameAs(targetReservation.Reservation));
            Assert.That(registry.Reserve(0, Request(yin, id: "source-transfer")).Accepted, Is.True);
        }

        [Test]
        public void ExactDuplicateReturnsOriginalReceiptWithoutAnotherReservation()
        {
            var orb = Add();
            var first = registry.Reserve(0, Request(orb));
            var duplicate = registry.Reserve(0, Request(orb));
            Assert.That(duplicate.Accepted, Is.True);
            Assert.That(duplicate.IsDuplicate, Is.True);
            Assert.That(duplicate.Reason, Is.EqualTo(first.Reason));
            Assert.That(duplicate.Reservation, Is.SameAs(first.Reservation));
            Assert.That(registry.Snapshot().Count, Is.EqualTo(1));
            AssertRejected(registry.Reserve(0, Request(orb, id: "other-request", sequence: 2)), "ORB_PENDING");
        }

        [TestCase("sender")]
        [TestCase("kind")]
        [TestCase("position")]
        [TestCase("sequence")]
        [TestCase("orb")]
        [TestCase("other-orb")]
        public void ReusedRequestIdWithChangedPayloadOrSenderCannotReplaceReceipt(string field)
        {
            var orb = Add();
            var other = Add();
            var first = registry.Reserve(0, Request(orb));
            var changed = Request(field == "orb" ? other : orb,
                field == "kind" ? OrbActionKind.TransferRight : OrbActionKind.TransferLeft,
                field == "other-orb" ? other.OrbId : null,
                sequence: field == "sequence" ? 2ul : 1ul,
                position: field == "position" ? new Vector2(.1f, .2f) : Vector2.one * .5f);
            AssertRejected(registry.Reserve(field == "sender" ? 1ul : 0ul, changed), "REQUEST_ID_CONFLICT");
            Assert.That(registry.IsPending(other.OrbId), Is.False);
            var duplicate = registry.Reserve(0, Request(orb));
            Assert.That(duplicate.Reservation, Is.SameAs(first.Reservation));
            Assert.That(duplicate.IsDuplicate, Is.True);
        }

        [Test]
        public void IdenticalRejectedRequestHasStableReceiptAndDifferentRequestCanSucceed()
        {
            var orb = Add(OrbKind.Raw, OrbPolarity.Yang);
            var first = registry.Reserve(0, Request(orb, OrbActionKind.Launch));
            var duplicate = registry.Reserve(0, Request(orb, OrbActionKind.Launch));
            Assert.That(first.Accepted, Is.False);
            Assert.That(duplicate.Accepted, Is.False);
            Assert.That(duplicate.IsDuplicate, Is.True);
            Assert.That(duplicate.Reason, Is.EqualTo(first.Reason));
            Assert.That(registry.Reserve(0, Request(orb, id: "valid-transfer")).Accepted, Is.True);
        }

        [TestCase(-.001f, .5f)]
        [TestCase(.5f, 1.001f)]
        [TestCase(float.NaN, .5f)]
        [TestCase(.5f, float.PositiveInfinity)]
        public void InvalidCoordinatesNeverEnterFixtureOrReserve(float x, float y)
        {
            var invalid = new Vector2(x, y);
            Assert.Throws<ArgumentOutOfRangeException>(() => registry.RegisterDevelopmentOrb(0, OrbKind.Raw, OrbPolarity.Yin, invalid));
            var orb = Add();
            AssertRejected(registry.Reserve(0, Request(orb, position: invalid)), "INVALID_POSITION");
            Assert.That(registry.IsPending(orb.OrbId), Is.False);
        }

        [Test]
        public void InvalidActionAndUnexpectedSecondOrbRejectBeforeAnyLock()
        {
            var orb = Add();
            var other = Add();
            AssertRejected(registry.Reserve(0, Request(orb, (OrbActionKind)99, id: "bad-kind")), "INVALID_ACTION");
            AssertRejected(registry.Reserve(0, Request(orb, OrbActionKind.TransferLeft, other.OrbId, id: "transfer-extra")), "UNEXPECTED_SECOND_ORB");
            AssertRejected(registry.Reserve(0, Request(orb, OrbActionKind.Launch, other.OrbId, id: "launch-extra")), "UNEXPECTED_SECOND_ORB");
            Assert.That(registry.IsPending(orb.OrbId), Is.False);
            Assert.That(registry.IsPending(other.OrbId), Is.False);
        }

        [Test]
        public void SequenceMustIncreaseFromConfirmedBaselineAndRejectDoesNotSpendIt()
        {
            var orb = Add();
            AssertRejected(registry.Reserve(0, Request(orb, id: "zero", sequence: 0)), "STALE_SEQUENCE");
            var accepted = registry.Reserve(0, Request(orb, id: "max", sequence: ulong.MaxValue));
            Assert.That(accepted.Accepted, Is.True);
            Assert.That(accepted.Reservation.Request.SequenceNumber, Is.EqualTo(ulong.MaxValue));
            AssertRejected(registry.Reserve(0, Request(orb, id: "wrapped", sequence: 0)), "ORB_PENDING");
        }

        [Test]
        public void PendingPersistsUntilExplicitNewRoundAndOldRoundRequestsStayInvalid()
        {
            var orb = Add();
            var oldRequest = Request(orb);
            registry.Reserve(0, oldRequest);
            Assert.Throws<ArgumentOutOfRangeException>(() => registry.ResetRound(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => registry.ResetRound(0));
            Assert.Throws<InvalidOperationException>(() => registry.BeginSession("session-a", 2));
            Assert.That(registry.IsPending(orb.OrbId), Is.True);
            registry.ResetRound(2);
            Assert.That(registry.RoundId, Is.EqualTo(2));
            Assert.That(registry.Snapshot(), Is.Empty);
            Assert.That(registry.IsPending(orb.OrbId), Is.False);
            AssertRejected(registry.Reserve(0, oldRequest), "ROUND_MISMATCH");
            var replacement = Add();
            Assert.That(replacement.OrbId, Is.Not.EqualTo(orb.OrbId));
            Assert.That(registry.Reserve(0, Request(replacement, round: 2)).Accepted, Is.True);
        }

        [Test]
        public void SessionTeardownClearsFixturesLocksAndReceipts()
        {
            var orb = Add();
            var oldRequest = Request(orb);
            registry.Reserve(0, oldRequest);
            registry.ClearSession();
            Assert.That(registry.HasSession, Is.False);
            Assert.That(registry.Snapshot(), Is.Empty);
            Assert.That(registry.IsPending(orb.OrbId), Is.False);
            AssertRejected(registry.Reserve(0, oldRequest), "NO_ACTIVE_SESSION");
            Assert.Throws<InvalidOperationException>(() => registry.ResetRound(2));
            registry.BeginSession("session-b", 1);
            AssertRejected(registry.Reserve(0, oldRequest), "SESSION_MISMATCH");
        }

        private OrbRecord Add(OrbKind kind = OrbKind.Combined, OrbPolarity polarity = OrbPolarity.None, ulong owner = 0)
        {
            return registry.RegisterDevelopmentOrb(owner, kind, polarity, Vector2.one * .5f);
        }

        private static OrbActionRequest Request(OrbRecord orb, OrbActionKind kind = OrbActionKind.TransferLeft,
            string other = null, string id = "request", ulong sequence = 1, Vector2? position = null,
            string session = "session-a", uint round = 1)
        {
            return new OrbActionRequest(session, round, id, orb.OrbId, other, kind, sequence,
                position ?? Vector2.one * .5f);
        }

        private static void AssertRejected(OrbReservationResult result, string reason)
        {
            Assert.That(result.Accepted, Is.False);
            Assert.That(result.IsDuplicate, Is.False);
            Assert.That(result.Reservation, Is.Null);
            Assert.That(result.Reason, Is.EqualTo(reason));
        }
    }
}
