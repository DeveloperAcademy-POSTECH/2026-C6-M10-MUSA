using System;
using C6.Prototype.Lobby;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Tests.Lobby
{
    public sealed class LobbyReceiptTests
    {
        private const string Room = "11111111111111111111111111111111";
        private const string Session = "22222222222222222222222222222222";
        private const string Nonce = "33333333333333333333333333333333";
        private LobbyAuthority authority;
        private ulong hostSequence, peerSequence;
        [SetUp] public void SetUp()
        {
            authority = new LobbyAuthority(Room, Session, "14", "{\"schema\":1}", 123);
            hostSequence = peerSequence = 0;
            Assert.That(authority.TryAdmit(42, new LobbyHello { protocol = 10, build = "14", roomId = Room, clientNonce = Nonce }, out _), Is.True);
        }
        private void Apply(ulong sender, string kind, bool ready = true)
        {
            Assert.That(authority.Handle(sender, new LobbyRequest { protocol = 10, roomId = Room, sessionId = Session,
                requestId = Guid.NewGuid().ToString("N"), sequence = sender == 0 ? ++hostSequence : ++peerSequence,
                revision = authority.Revision, kind = kind, ready = ready, configFingerprint = authority.ConfigFingerprint }, out var reason), Is.True, reason);
        }
        private LobbySnapshot Snapshot() => authority.Snapshot(Nonce);
        private bool Receipt(LobbySnapshot current, LobbySnapshot receipt, ulong minimum = 1)
            => LobbyWire.AcceptsReceipt(current, receipt, 42, "14", Nonce, Room, minimum);
        private void ReadyBoth()
        { Apply(42, LobbyProtocol.AckInitial); Apply(0, LobbyProtocol.SetReady); Apply(42, LobbyProtocol.SetReady); }

        [Test] public void SameRevisionRejectionCanResolvePendingWithoutWeakeningSnapshotMonotonicity()
        {
            var current = Snapshot(); var rejection = Snapshot();
            Assert.That(Receipt(current, rejection, current.revision), Is.True);
            Assert.That(LobbyWire.AcceptsSnapshot(current, rejection, "14", Nonce, Room), Is.False);
        }
        [Test] public void AcceptedReplyAlreadySeenInBroadcastCanResolvePending()
        {
            ulong requestedRevision = authority.Revision;
            Apply(42, LobbyProtocol.AckInitial);
            var broadcast = Snapshot(); var reply = Snapshot();
            Assert.That(Receipt(broadcast, reply, requestedRevision), Is.True);
            Assert.That(broadcast.p2.initialStateReceived, Is.True);
        }
        [Test] public void OlderReceiptAfterNewerBroadcastResolvesIntentWithoutRollingBackReady()
        {
            Apply(42, LobbyProtocol.AckInitial);
            var receipt = Snapshot();
            Apply(0, LobbyProtocol.SetReady); Apply(42, LobbyProtocol.SetReady);
            var current = Snapshot(); string preserved = JsonUtility.ToJson(current);
            Assert.That(Receipt(current, receipt, receipt.revision), Is.True);
            Assert.That(JsonUtility.ToJson(current), Is.EqualTo(preserved));
            Assert.That(current.canStart, Is.True);
            Assert.That(LobbyWire.AcceptsSnapshot(current, receipt, "14", Nonce, Room), Is.False);
        }
        [Test] public void NewerReceiptMustAlsoPassNormalStateTransitionValidation()
        {
            var current = Snapshot(); Apply(42, LobbyProtocol.AckInitial); var receipt = Snapshot();
            Assert.That(Receipt(current, receipt, current.revision), Is.True);
            Assert.That(LobbyWire.AcceptsSnapshot(current, receipt, "14", Nonce, Room), Is.True);
        }
        [Test] public void ReceiptBeforeRequestedRevisionCannotResolveANewerIntent()
        {
            var receipt = Snapshot(); Apply(42, LobbyProtocol.AckInitial); var current = Snapshot();
            Assert.That(Receipt(current, receipt, current.revision), Is.False);
            Assert.That(Receipt(current, receipt, 0), Is.False);
            Assert.That(Receipt(current, receipt, current.revision + 1), Is.False);
        }
        [TestCase("nonce")] [TestCase("build")] [TestCase("room")]
        [TestCase("session")] [TestCase("config")] [TestCase("seed")]
        [TestCase("player")] [TestCase("malformed")]
        public void ReceiptCannotResolveAcrossForeignOrMalformedContext(string mutate)
        {
            var current = Snapshot(); var receipt = Snapshot();
            if (mutate == "nonce") receipt.recipientNonce = Guid.NewGuid().ToString("N");
            if (mutate == "build") receipt.build = "13";
            if (mutate == "room") receipt.roomId = Guid.NewGuid().ToString("N");
            if (mutate == "session") receipt.sessionId = Guid.NewGuid().ToString("N");
            if (mutate == "config") { receipt.hostConfigJson = "{}"; receipt.configFingerprint = LobbyWire.Fingerprint("{}"); }
            if (mutate == "seed") receipt.seed++;
            if (mutate == "player") receipt.p2.clientId = 99;
            if (mutate == "malformed") receipt.p2.playerNumber = 1;
            Assert.That(Receipt(current, receipt), Is.False);
        }
        [TestCase("initial")] [TestCase("ready")] [TestCase("phase")]
        public void IdenticalRevisionCannotDescribeDifferentValidState(string mutation)
        {
            Apply(42, LobbyProtocol.AckInitial);
            var current = Snapshot(); var receipt = Snapshot();
            if (mutation == "initial") receipt.p2.initialStateReceived = false;
            if (mutation == "ready") receipt.p2.ready = true;
            if (mutation == "phase") { receipt.phase = LobbyProtocol.Closed; receipt.closeReason = "CLOSED"; }
            Assert.That(LobbyWire.ValidSnapshot(receipt), Is.True);
            Assert.That(Receipt(current, receipt), Is.False);
        }
        [Test] public void ReceiptRequiresLocalPlayerInBothBoundSnapshots()
        {
            var current = Snapshot(); var receipt = Snapshot();
            Assert.That(LobbyWire.AcceptsReceipt(current, receipt, 99, "14", Nonce, Room), Is.False);
            receipt.p2 = null;
            Assert.That(Receipt(current, receipt), Is.False);
            Assert.That(Receipt(null, Snapshot()), Is.False);
        }
        [Test] public void HostReceiptBeforeP2AdmissionRemainsValidForHostLocalContext()
        {
            var other = new LobbyAuthority(Room, Session, "14", "{}", 1); var receipt = other.Snapshot();
            other.TryAdmit(42, new LobbyHello { protocol = 10, build = "14", roomId = Room, clientNonce = Nonce }, out _);
            var current = other.Snapshot();
            Assert.That(LobbyWire.AcceptsReceipt(current, receipt, 0, "14", "", Room, 1), Is.True);
        }
        [Test] public void OlderReceiptCannotClaimInitialAckNotPresentInCurrentState()
        {
            var current = Snapshot(); current.revision = 4;
            var receipt = Snapshot(); receipt.p2.initialStateReceived = true;
            Assert.That(Receipt(current, receipt), Is.False);
        }
        [Test] public void OlderReceiptCannotClaimPlayingWhileCurrentRoomHasNeverStarted()
        {
            ReadyBoth(); var current = Snapshot();
            Apply(0, LobbyProtocol.Start); var receipt = Snapshot(); receipt.revision = current.revision - 1;
            Assert.That(LobbyWire.ValidSnapshot(receipt), Is.True);
            Assert.That(Receipt(current, receipt), Is.False);
        }
        [Test] public void OlderClosedReceiptCannotClaimTerminalStateWasLaterReopened()
        {
            ReadyBoth(); Apply(0, LobbyProtocol.Start); var current = Snapshot();
            authority.Close("LEFT"); var receipt = Snapshot(); receipt.revision = current.revision - 1;
            Assert.That(LobbyWire.ValidSnapshot(receipt), Is.True);
            Assert.That(Receipt(current, receipt), Is.False);
        }
        [Test] public void PreClosePlayingReceiptMayResolveButCannotReplaceClosedCurrentSnapshot()
        {
            ReadyBoth(); Apply(0, LobbyProtocol.Start); var receipt = Snapshot();
            authority.Close("LEFT"); var current = Snapshot();
            Assert.That(Receipt(current, receipt), Is.True);
            Assert.That(LobbyWire.AcceptsSnapshot(current, receipt, "14", Nonce, Room), Is.False);
            Assert.That(current.phase, Is.EqualTo(LobbyProtocol.Closed));
        }
    }
}
