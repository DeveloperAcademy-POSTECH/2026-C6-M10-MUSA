using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using C6.Prototype.Orbs;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Attack.Tests
{
    public sealed class TransferAuthorityTests
    {
        private HostOrbRegistry registry;
        private AttackAuthority authority;
        private const string Session = "transfer-tests";
        private const ulong A = 7, B = 9;
        private const float Inset = .055f;
        [SetUp] public void SetUp()
        {
            registry = new HostOrbRegistry(true); registry.BeginSession(Session, 1);
            authority = new AttackAuthority(registry, 100, 20); authority.BeginDevelopmentRound();
        }
        private OrbRecord Add(ulong owner = A, OrbKind kind = OrbKind.Raw, OrbPolarity polarity = OrbPolarity.Yin)
            => registry.RegisterDevelopmentOrb(owner, kind, polarity, new Vector2(.4f, .5f));
        private OrbActionRequest Request(OrbRecord orb, OrbActionKind kind = OrbActionKind.TransferLeft,
            ulong sequence = 1, string id = null, float y = .37f)
            => new OrbActionRequest(Session, 1, id ?? Guid.NewGuid().ToString("N"), orb.OrbId, null, kind, sequence, new Vector2(.4f, y));
        private AttackLaunchResult Transfer(ulong sender, OrbActionRequest request, ulong receiver, bool connected = true, int limit = 20)
            => authority.RequestTransfer(sender, request, receiver, connected, limit, Inset);

        [TestCase(OrbKind.Raw, OrbPolarity.Yin, OrbActionKind.TransferLeft, EntrySide.Right)]
        [TestCase(OrbKind.Raw, OrbPolarity.Yang, OrbActionKind.TransferLeft, EntrySide.Right)]
        [TestCase(OrbKind.Combined, OrbPolarity.None, OrbActionKind.TransferLeft, EntrySide.Right)]
        [TestCase(OrbKind.Raw, OrbPolarity.Yin, OrbActionKind.TransferRight, EntrySide.Left)]
        [TestCase(OrbKind.Raw, OrbPolarity.Yang, OrbActionKind.TransferRight, EntrySide.Left)]
        [TestCase(OrbKind.Combined, OrbPolarity.None, OrbActionKind.TransferRight, EntrySide.Left)]
        public void BothDirectionsPreserveEveryLivingIdAndChangeOwnerOnce(OrbKind kind, OrbPolarity polarity,
            OrbActionKind direction, EntrySide entry)
        {
            var orb = Add(A, kind, polarity); Add(B);
            var ids = registry.Snapshot().Select(value => value.OrbId).ToArray();
            var request = Request(orb, direction);
            var result = Transfer(A, request, B);
            Assert.That(result.Accepted && !result.IsDuplicate, Is.True, result.Reason);
            Assert.That(result.SpawnRequired, Is.False);
            Assert.That(result.Kind, Is.EqualTo(direction));
            Assert.That(result.Orb.OrbId, Is.EqualTo(orb.OrbId));
            Assert.That(result.Orb.Kind, Is.EqualTo(kind)); Assert.That(result.Orb.Polarity, Is.EqualTo(polarity));
            Assert.That(result.Orb.OwnerPlayerId, Is.EqualTo(B)); Assert.That(result.Orb.AuthorityState, Is.EqualTo(OrbAuthorityState.Idle));
            Assert.That(result.Orb.EntrySide, Is.EqualTo(entry));
            Assert.That(result.Orb.NormalizedPosition, Is.EqualTo(new Vector2(entry == EntrySide.Left ? Inset : 1 - Inset, .37f)));
            Assert.That(result.Orb.SequenceNumber, Is.EqualTo(1)); Assert.That(result.Orb.TransferCount, Is.EqualTo(1));
            Assert.That(result.Orb.LastTransferSequence, Is.EqualTo(1));
            Assert.That(registry.Snapshot().Select(value => value.OrbId), Is.EqualTo(ids));
            Assert.That(registry.CountStoredOrbs(A), Is.Zero); Assert.That(registry.CountStoredOrbs(B), Is.EqualTo(2));
            Assert.That(registry.IsPending(orb.OrbId), Is.False);
        }

        [Test]
        public void DuplicateAndQueryAfterReturnNeverPerformASecondHandoff()
        {
            var orb = Add(); var first = Request(orb);
            Assert.That(Transfer(A, first, B).Accepted, Is.True);
            var returned = Transfer(B, Request(orb, OrbActionKind.TransferRight, 2), A);
            Assert.That(returned.Accepted, Is.True);
            var duplicate = Transfer(A, first, B);
            Assert.That(duplicate.Accepted && duplicate.IsDuplicate, Is.True);
            registry.TryGet(orb.OrbId, out var current);
            Assert.That(current.OwnerPlayerId, Is.EqualTo(A)); Assert.That(current.TransferCount, Is.EqualTo(2));
            var query = authority.QueryRequest(A, Session, 1, first.RequestId, orb.OrbId);
            Assert.That(query.Known && query.Receipt.Accepted, Is.True);
            Assert.That(query.ConfirmedOrb, Is.SameAs(current));
            Assert.That(authority.QueryRequest(B, Session, 1, first.RequestId, orb.OrbId).Known, Is.False);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ChangedDirectionSequenceOrPositionQueryCannotReuseTheOriginalApproval(int changedField)
        {
            var orb = Add(); var original = Request(orb, id: "query-full-payload");
            Assert.That(Transfer(A, original, B).Accepted, Is.True);
            var changed = new OrbActionRequest(Session, 1, original.RequestId, orb.OrbId, null,
                changedField == 0 ? OrbActionKind.TransferRight : original.Kind,
                changedField == 1 ? 2UL : original.SequenceNumber,
                changedField == 2 ? new Vector2(.4f, .8f) : original.NormalizedPosition);
            var rejected = authority.QueryRequest(A, changed);
            Assert.That(rejected.Known, Is.False); Assert.That(rejected.Reason, Is.EqualTo("REQUEST_PAYLOAD_MISMATCH"));
            Assert.That(authority.QueryRequest(A, original).Known, Is.True);
            Assert.That(authority.QueryRequest(A, Session, 1, original.RequestId, orb.OrbId).Known, Is.True,
                "The historical IDs-only diagnostic API is preserved.");
            registry.TryGet(orb.OrbId, out var current);
            Assert.That(current.OwnerPlayerId, Is.EqualTo(B)); Assert.That(current.TransferCount, Is.EqualTo(1));
        }

        [Test]
        public void ReceiverLaunchOwnsDamageAndKeepsTransferHistoryThroughConsumption()
        {
            var orb = Add(A, OrbKind.Combined, OrbPolarity.None); var first = Request(orb);
            Assert.That(Transfer(A, first, B).Accepted, Is.True);
            var launch = authority.RequestLaunch(B, Request(orb, OrbActionKind.Launch, 2));
            Assert.That(launch.Accepted && launch.SpawnRequired, Is.True);
            Assert.That(authority.MarkProjectileSpawned(Session, 1, orb.OrbId), Is.True);
            Assert.That(Transfer(B, Request(orb, OrbActionKind.TransferRight, 3), A).Accepted, Is.False);
            var hit = authority.ProcessHostHit(Session, 1, orb.OrbId);
            Assert.That(hit.Applied, Is.True); Assert.That(hit.AttackerPlayerId, Is.EqualTo(B));
            registry.TryGet(orb.OrbId, out var consumed);
            Assert.That(consumed.AuthorityState, Is.EqualTo(OrbAuthorityState.Consumed));
            Assert.That(consumed.TransferCount, Is.EqualTo(1)); Assert.That(consumed.LastTransferSequence, Is.EqualTo(1));
            Assert.That(consumed.SequenceNumber, Is.EqualTo(2)); Assert.That(consumed.EntrySide, Is.EqualTo(EntrySide.Right));
            var query = authority.QueryRequest(A, Session, 1, first.RequestId, orb.OrbId);
            Assert.That(query.Known && query.Receipt.Accepted, Is.True);
            Assert.That(query.ConfirmedOrb, Is.SameAs(consumed));
            Assert.That(authority.ProcessHostHit(Session, 1, orb.OrbId).Applied, Is.False);
        }

        [Test]
        public void DisconnectedOrFullReceiverDoesNotMoveOrLockTheSenderOrb()
        {
            var orb = Add(); Add(B);
            Assert.That(Transfer(A, Request(orb), B, false).Reason, Is.EqualTo("RECEIVER_NOT_CONNECTED"));
            Assert.That(Transfer(A, Request(orb), B, true, 1).Reason, Is.EqualTo("RECEIVER_STORAGE_FULL"));
            registry.TryGet(orb.OrbId, out var current);
            Assert.That(current, Is.SameAs(orb)); Assert.That(registry.IsPending(orb.OrbId), Is.False);
        }

        [Test]
        public void WrongOwnerAndStaleSequenceAreRejectedWithoutChangingTheReceiver()
        {
            var orb = Add();
            Assert.That(Transfer(B, Request(orb), A).Reason, Is.EqualTo("OWNER_MISMATCH"));
            Assert.That(Transfer(A, Request(orb, sequence: 4), B).Accepted, Is.True);
            Assert.That(Transfer(B, Request(orb, sequence: 4), A).Reason, Is.EqualTo("STALE_SEQUENCE"));
            registry.TryGet(orb.OrbId, out var current); Assert.That(current.OwnerPlayerId, Is.EqualTo(B));
            Assert.That(current.TransferCount, Is.EqualTo(1)); Assert.That(registry.IsPending(orb.OrbId), Is.False);
        }

        [TestCase(-.01f)] [TestCase(1.01f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void InvalidHeightIsRejectedRatherThanClamped(float height)
        {
            var orb = Add(); var result = Transfer(A, Request(orb, y: height), B);
            Assert.That(result.Accepted, Is.False); Assert.That(result.Reason, Is.EqualTo("INVALID_POSITION"));
            registry.TryGet(orb.OrbId, out var current); Assert.That(current, Is.SameAs(orb));
            Assert.That(registry.IsPending(orb.OrbId), Is.False);
        }

        [Test]
        public void ACombineReservationExcludesTransferAndPreservesTransferredMaterialMetadata()
        {
            var source = Add(); Assert.That(Transfer(A, Request(source), B).Accepted, Is.True);
            var target = Add(B, OrbKind.Raw, OrbPolarity.Yang);
            var combine = new OrbActionRequest(Session, 1, "combine-lock", source.OrbId, target.OrbId,
                OrbActionKind.Combine, 2, new Vector2(.3f, .4f));
            var reservation = registry.Reserve(B, combine); Assert.That(reservation.Accepted, Is.True);
            Assert.That(Transfer(B, Request(source, sequence: 3), A).Reason, Is.EqualTo("ORB_PENDING"));
            Assert.That(registry.TryCompleteReservedCombination(reservation.Reservation, new Vector2(.5f, .6f),
                out var consumed, out _, out var combined), Is.True);
            Assert.That(consumed.TransferCount, Is.EqualTo(1)); Assert.That(consumed.LastTransferSequence, Is.EqualTo(1));
            Assert.That(combined.TransferCount, Is.Zero); Assert.That(combined.EntrySide, Is.EqualTo(EntrySide.None));
            Assert.That(Transfer(B, Request(source, sequence: 4), A).Reason, Is.EqualTo("ORB_NOT_IDLE"));
        }

        [Test]
        public void LaunchReservationExcludesTransferAndTransferExcludesPreviousOwnersLaunch()
        {
            var first = Add(A, OrbKind.Combined, OrbPolarity.None);
            var reservation = registry.Reserve(A, Request(first, OrbActionKind.Launch));
            Assert.That(reservation.Accepted, Is.True);
            Assert.That(Transfer(A, Request(first, sequence: 2), B).Reason, Is.EqualTo("ORB_PENDING"));
            Assert.That(registry.TryBeginReservedLaunch(reservation.Reservation, out _), Is.True);
            Assert.That(Transfer(A, Request(first, sequence: 3), B).Reason, Is.EqualTo("ORB_NOT_IDLE"));
            var second = Add(A, OrbKind.Combined, OrbPolarity.None);
            Assert.That(Transfer(A, Request(second), B).Accepted, Is.True);
            Assert.That(authority.RequestLaunch(A, Request(second, OrbActionKind.Launch, 2)).Reason, Is.EqualTo("OWNER_MISMATCH"));
        }

        [Test]
        public void RequestIdIsSharedBetweenLaunchAndTransferAndOldRoundsCannotReplay()
        {
            var orb = Add(A, OrbKind.Combined, OrbPolarity.None);
            var request = Request(orb, id: "same-request"); Assert.That(Transfer(A, request, B).Accepted, Is.True);
            Assert.That(authority.RequestLaunch(B, Request(orb, OrbActionKind.Launch, 2, "same-request")).Reason, Is.EqualTo("REQUEST_ID_CONFLICT"));
            registry.ResetRound(2); authority.BeginDevelopmentRound();
            Assert.That(Transfer(A, request, B).Reason, Is.EqualTo("ROUND_MISMATCH"));
            Assert.That(registry.Snapshot(), Is.Empty);
        }

        [Test]
        public void ExhaustedCounterIsRejectedBeforeAnOverflowOrReservation()
        {
            var orb = Add();
            var all = (Dictionary<string, OrbRecord>)typeof(HostOrbRegistry).GetField("orbs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(registry);
            all[orb.OrbId] = new OrbRecord(orb.OrbId, orb.Kind, orb.Polarity, A, OrbAuthorityState.Idle,
                orb.NormalizedPosition, EntrySide.Left, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            Assert.That(Transfer(A, Request(orb, sequence: ulong.MaxValue), B).Reason, Is.EqualTo("TRANSFER_COUNT_EXHAUSTED"));
            Assert.That(registry.IsPending(orb.OrbId), Is.False);
            Assert.That(all[orb.OrbId].OwnerPlayerId, Is.EqualTo(A));
        }

        [Test]
        public void MaximumSequenceTransferCannotBeFollowedByAnOlderAction()
        {
            var orb = Add(); Assert.That(Transfer(A, Request(orb, sequence: ulong.MaxValue), B).Accepted, Is.True);
            Assert.That(Transfer(B, Request(orb, sequence: 1), A).Reason, Is.EqualTo("STALE_SEQUENCE"));
            registry.TryGet(orb.OrbId, out var current); Assert.That(current.SequenceNumber, Is.EqualTo(ulong.MaxValue));
        }
    }
}
