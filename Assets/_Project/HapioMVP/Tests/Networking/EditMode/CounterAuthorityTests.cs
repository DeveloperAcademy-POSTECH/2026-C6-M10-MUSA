using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using C6.Prototype.Networking.Counter;
using NUnit.Framework;

namespace C6.Prototype.Networking.Tests
{
    public sealed class CounterAuthorityTests
    {
        private const ulong Host = 0;
        private const ulong Client = 41;
        private CounterAuthority authority;
        private string sessionId;

        [SetUp]
        public void SetUp()
        {
            authority = new CounterAuthority(Guid.NewGuid(), Host);
            sessionId = authority.GetSnapshot().SessionId;
            Assert.That(authority.AddParticipant(Client), Is.True);
        }

        [Test]
        public void SequentialHostAndClientRequestsShareOneValidationPathAndValue()
        {
            var initial = authority.GetSnapshot();
            for (ulong requestId = 1; requestId <= 50; requestId++)
            {
                var hostResult = authority.Apply(Host, sessionId, requestId);
                var clientResult = authority.Apply(Client, sessionId, requestId);
                Assert.That(hostResult.Approved && clientResult.Approved, Is.True);
                Assert.That(hostResult.Snapshot.Value, Is.EqualTo((long)requestId * 2 - 1));
                Assert.That(clientResult.Snapshot.Value, Is.EqualTo((long)requestId * 2));
            }

            AssertSnapshot(authority.GetSnapshot(), 100, 100, 100, 0);
            AssertSnapshot(initial, 0, 0, 0, 0);
        }

        [Test]
        public void SimultaneousRequestsFromTwoSendersProduceExactlyOneHundredUniqueRevisions()
        {
            var results = new ConcurrentBag<CounterReceipt>();
            Parallel.For(0, 100, index =>
            {
                ulong senderId = index % 2 == 0 ? Host : Client;
                ulong requestId = (ulong)(index / 2 + 1);
                results.Add(authority.Apply(senderId, sessionId, requestId));
            });

            Assert.That(results.Count, Is.EqualTo(100));
            Assert.That(results.All(receipt => receipt.Approved && !receipt.Duplicate), Is.True);
            Assert.That(results.Select(receipt => receipt.Snapshot.Revision).Distinct().Count(), Is.EqualTo(100));
            Assert.That(results.All(receipt => receipt.Snapshot.Value == (long)receipt.Snapshot.ApprovedCount), Is.True);
            AssertSnapshot(authority.GetSnapshot(), 100, 100, 100, 0);
        }

        [Test]
        public void DuplicateReturnsOriginalReceiptSnapshotAfterOtherRequestsAdvanceState()
        {
            var original = authority.Apply(Client, sessionId, 1);
            authority.Apply(Host, sessionId, 1);
            authority.Apply(Client, sessionId, 2);
            var duplicate = authority.Apply(Client, sessionId, 1);

            Assert.That(duplicate.Approved, Is.True);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(duplicate.Reason, Is.EqualTo(original.Reason));
            Assert.That(duplicate.SenderId, Is.EqualTo(Client));
            Assert.That(duplicate.RequestId, Is.EqualTo(1));
            AssertSnapshot(duplicate.Snapshot, 1, 1, 1, 0);
            AssertSnapshot(authority.GetSnapshot(), 3, 3, 3, 0);
            Assert.That(authority.TrackedRequestCount, Is.EqualTo(3));
        }

        [Test]
        public void ConcurrentRetransmissionsApplyOnlyOnceAndAllReturnOriginalResult()
        {
            var results = new ConcurrentBag<CounterReceipt>();
            Parallel.For(0, 64, _ => results.Add(authority.Apply(Client, sessionId, 7)));

            Assert.That(results.Count(receipt => !receipt.Duplicate), Is.EqualTo(1));
            Assert.That(results.All(receipt => receipt.Approved && receipt.Snapshot.Value == 1), Is.True);
            AssertSnapshot(authority.GetSnapshot(), 1, 1, 1, 0);
            Assert.That(authority.TrackedRequestCount, Is.EqualTo(1));
        }

        [Test]
        public void UnknownSenderCannotChangeStateOrPoisonTheSameIdForARealParticipant()
        {
            for (ulong requestId = 1; requestId <= 20; requestId++)
            {
                var result = authority.Apply(999, sessionId, requestId);
                Assert.That(result.Approved, Is.False);
                Assert.That(result.Reason, Is.EqualTo("unknown-sender"));
            }

            AssertSnapshot(authority.GetSnapshot(), 0, 0, 0, 0);
            Assert.That(authority.TrackedRequestCount, Is.Zero);
            Assert.That(authority.Apply(Client, sessionId, 1).Approved, Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("old-session")]
        public void InvalidSessionCannotChangeStateOrPoisonAnActiveRequest(string wrongSession)
        {
            var result = authority.Apply(Client, wrongSession, 1);
            Assert.That(result.Approved, Is.False);
            Assert.That(result.Reason, Is.EqualTo("stale-session"));
            AssertSnapshot(authority.GetSnapshot(), 0, 0, 0, 0);
            Assert.That(authority.TrackedRequestCount, Is.Zero);
            Assert.That(authority.Apply(Client, sessionId, 1).Approved, Is.True);
        }

        [Test]
        public void ReservedZeroRequestIdCountsOneRejectionPerSenderAndPreservesReplayResult()
        {
            var rejected = authority.Apply(Client, sessionId, 0);
            Assert.That(rejected.Approved, Is.False);
            Assert.That(rejected.Reason, Is.EqualTo("invalid-request-id"));
            AssertSnapshot(rejected.Snapshot, 0, 1, 0, 1);
            authority.Apply(Host, sessionId, 1);
            var replay = authority.Apply(Client, sessionId, 0);
            Assert.That(replay.Duplicate, Is.True);
            Assert.That(replay.Approved, Is.False);
            AssertSnapshot(replay.Snapshot, 0, 1, 0, 1);
            AssertSnapshot(authority.GetSnapshot(), 1, 2, 1, 1);

            authority.Apply(Host, sessionId, 0);
            AssertSnapshot(authority.GetSnapshot(), 1, 3, 1, 2);
        }

        [Test]
        public void AdmissionAllowsOnlyHostAndOneActualParticipant()
        {
            Assert.That(authority.AddParticipant(Host), Is.True);
            Assert.That(authority.AddParticipant(Client), Is.True);
            Assert.That(authority.AddParticipant(42), Is.False);
            Assert.That(authority.Apply(42, sessionId, 1).Reason, Is.EqualTo("unknown-sender"));
            Assert.That(authority.RemoveParticipant(Host), Is.False);
            Assert.That(authority.RemoveParticipant(Client), Is.True);
            Assert.That(authority.Apply(Client, sessionId, 1).Reason, Is.EqualTo("unknown-sender"));
            Assert.That(authority.AddParticipant(42), Is.True);
            Assert.That(authority.Apply(42, sessionId, 1).Approved, Is.True);
        }

        [Test]
        public void RemovingAndReadmittingAnIdCannotEraseItsDeduplicationHistory()
        {
            authority.Apply(Client, sessionId, 1);
            authority.RemoveParticipant(Client);
            authority.AddParticipant(Client);
            Assert.That(authority.Apply(Client, sessionId, 1).Duplicate, Is.True);
            AssertSnapshot(authority.GetSnapshot(), 1, 1, 1, 0);
        }

        [Test]
        public void FullLedgerRefusesNewRequestsWithoutEvictingAnyOriginalReceipt()
        {
            var limited = new CounterAuthority(Guid.NewGuid(), Host, 2);
            string id = limited.GetSnapshot().SessionId;
            limited.Apply(Host, id, 0);
            limited.Apply(Host, id, 1);
            for (ulong requestId = 2; requestId < 20; requestId++)
            {
                var rejected = limited.Apply(Host, id, requestId);
                Assert.That(rejected.Approved, Is.False);
                Assert.That(rejected.Reason, Is.EqualTo("ledger-full"));
            }

            Assert.That(limited.TrackedRequestCount, Is.EqualTo(2));
            AssertSnapshot(limited.GetSnapshot(), 1, 2, 1, 1);
            Assert.That(limited.Apply(Host, id, 0).Duplicate, Is.True);
            Assert.That(limited.Apply(Host, id, 1).Duplicate, Is.True);
            AssertSnapshot(limited.GetSnapshot(), 1, 2, 1, 1);
        }

        [Test]
        public void SessionEndClearsRecordsAndANewSessionRejectsOldRequests()
        {
            authority.Apply(Client, sessionId, 1);
            authority.EndSession();
            authority.EndSession();
            Assert.That(authority.IsActive, Is.False);
            Assert.That(authority.TrackedRequestCount, Is.Zero);
            Assert.That(authority.GetSnapshot().SessionId, Is.Empty);
            AssertSnapshot(authority.GetSnapshot(), 0, 0, 0, 0);
            Assert.That(authority.AddParticipant(Client), Is.False);
            Assert.That(authority.Apply(Client, sessionId, 1).Reason, Is.EqualTo("session-ended"));

            var next = new CounterAuthority(Guid.NewGuid(), Host);
            next.AddParticipant(Client);
            Assert.That(next.Apply(Client, sessionId, 1).Reason, Is.EqualTo("stale-session"));
            Assert.That(next.Apply(Client, next.GetSnapshot().SessionId, 1).Approved, Is.True);
            AssertSnapshot(next.GetSnapshot(), 1, 1, 1, 0);
        }

        [Test]
        public void ConstructorRejectsEmptySessionAndUnusableLedgerLimit()
        {
            Assert.Throws<ArgumentException>(() => new CounterAuthority(Guid.Empty, Host));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CounterAuthority(Guid.NewGuid(), Host, 0));
        }

        private static void AssertSnapshot(CounterSnapshot snapshot, long value,
            ulong revision, ulong approved, ulong rejected)
        {
            Assert.That(snapshot.Value, Is.EqualTo(value));
            Assert.That(snapshot.Revision, Is.EqualTo(revision));
            Assert.That(snapshot.ApprovedCount, Is.EqualTo(approved));
            Assert.That(snapshot.RejectedCount, Is.EqualTo(rejected));
        }
    }
}
