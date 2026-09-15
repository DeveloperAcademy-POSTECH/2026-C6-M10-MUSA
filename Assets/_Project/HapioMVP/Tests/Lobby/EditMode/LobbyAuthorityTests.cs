using System;
using C6.Prototype.Lobby;
using NUnit.Framework;

namespace C6.Prototype.Tests.Lobby
{
    public sealed class LobbyAuthorityTests
    {
        private const ulong Peer = 42;
        private const string Room = "11111111111111111111111111111111";
        private const string Session = "22222222222222222222222222222222";
        private const string Nonce = "33333333333333333333333333333333";
        private LobbyAuthority authority;
        private ulong hostSequence;
        private ulong peerSequence;
        [SetUp] public void SetUp()
        { authority = new LobbyAuthority(Room, Session, "14", "{\"schema\":1,\"staminaStart\":100}", 13579); hostSequence = peerSequence = 0; }
        private LobbyHello Hello(string room = Room) => new LobbyHello
        { protocol = LobbyProtocol.Version, build = "14", roomId = room, clientNonce = Nonce };
        private void Admit() => Assert.That(authority.TryAdmit(Peer, Hello(), out _), Is.True);
        private LobbyRequest Request(ulong sender, string kind, bool ready = true) => new LobbyRequest
        {
            protocol = LobbyProtocol.Version, roomId = Room, sessionId = Session, requestId = Guid.NewGuid().ToString("N"),
            sequence = sender == 0 ? ++hostSequence : ++peerSequence, revision = authority.Revision,
            kind = kind, ready = ready, configFingerprint = authority.ConfigFingerprint
        };
        private void Accept(ulong sender, string kind, bool ready = true)
        { Assert.That(authority.Handle(sender, Request(sender, kind, ready), out var reason), Is.True, reason); }
        private void BothReady()
        { Admit(); Accept(Peer, LobbyProtocol.AckInitial); Accept(0, LobbyProtocol.SetReady); Accept(Peer, LobbyProtocol.SetReady); }

        [Test] public void NewRoomHasOnlyVerifiedUnreadyP1AndNoStartContract()
        {
            var value = authority.Snapshot();
            Assert.That(LobbyWire.ValidSnapshot(value), Is.True);
            Assert.That(value.ParticipantCount, Is.EqualTo(1));
            Assert.That(value.p1.playerNumber, Is.EqualTo(1));
            Assert.That(value.p1.clientId, Is.Zero);
            Assert.That(value.p1.initialStateReceived, Is.True);
            Assert.That(value.p1.ready, Is.False);
            Assert.That(value.p2, Is.Null);
            Assert.That(value.canStart, Is.False);
            Assert.That(value.start, Is.Null);
            Assert.That(value.roundId, Is.Zero);
        }
        [Test] public void P1P2AndBothDirectionalNeighboursStayFixedForArbitraryNgoId()
        {
            Admit(); var value = authority.Snapshot(Nonce);
            Assert.That(value.LocalPlayerNumber(0), Is.EqualTo(1));
            Assert.That(value.LocalPlayerNumber(Peer), Is.EqualTo(2));
            Assert.That(value.LeftPlayerNumber(0), Is.EqualTo(2));
            Assert.That(value.RightPlayerNumber(0), Is.EqualTo(2));
            Assert.That(value.LeftPlayerNumber(Peer), Is.EqualTo(1));
            Assert.That(value.RightPlayerNumber(Peer), Is.EqualTo(1));
            Assert.That(value.LocalPlayerNumber(99), Is.Zero);
            Assert.That(value.LeftPlayerNumber(99), Is.Zero);
            Assert.That(authority.NonceFor(Peer), Is.EqualTo(Nonce));
            Assert.That(authority.NonceFor(99), Is.Null);
        }
        [Test] public void DirectIpWildcardRoomCanNegotiateHostIdentity()
        { Assert.That(authority.TryAdmit(Peer, Hello(""), out _), Is.True); Assert.That(authority.Snapshot().roomId, Is.EqualTo(Room)); }
        [TestCase("protocol", "PROTOCOL_MISMATCH")]
        [TestCase("build", "BUILD_MISMATCH")]
        [TestCase("room", "STALE_ROOM")]
        [TestCase("nonce", "INVALID_HELLO")]
        public void AdmissionRejectsIncompatibleOrMalformedHelloWithoutAllocatingP2(string mutate, string expected)
        {
            var hello = Hello();
            if (mutate == "protocol") hello.protocol++;
            if (mutate == "build") hello.build = "13";
            if (mutate == "room") hello.roomId = Guid.NewGuid().ToString("N");
            if (mutate == "nonce") hello.clientNonce = "";
            Assert.That(authority.TryAdmit(Peer, hello, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo(expected)); Assert.That(authority.ParticipantCount, Is.EqualTo(1));
            Assert.That(authority.Revision, Is.EqualTo(1));
        }
        [Test] public void HostNetworkIdCannotBeClaimedByJoiner()
        { Assert.That(authority.TryAdmit(0, Hello(), out var reason), Is.False); Assert.That(reason, Is.EqualTo("HOST_ID_RESERVED")); }
        [Test] public void ThirdParticipantAndDuplicateAdmissionCannotReplaceP2()
        {
            Admit(); var revision = authority.Revision;
            Assert.That(authority.TryAdmit(99, Hello(), out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("ROOM_FULL"));
            Assert.That(authority.TryAdmit(Peer, Hello(), out _), Is.False);
            Assert.That(authority.Snapshot().p2.clientId, Is.EqualTo(Peer));
            Assert.That(authority.Revision, Is.EqualTo(revision));
        }
        [Test] public void JoiningClientCannotReadyUntilItsHostConfigurationSnapshotIsAcknowledged()
        {
            Admit(); Assert.That(authority.Handle(Peer, Request(Peer, LobbyProtocol.SetReady), out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("INITIAL_STATE_REQUIRED"));
            Accept(Peer, LobbyProtocol.AckInitial); Accept(Peer, LobbyProtocol.SetReady);
            Assert.That(authority.Snapshot().p2.ready, Is.True);
        }
        [TestCase(LobbyProtocol.AckInitial)]
        [TestCase(LobbyProtocol.SetReady)]
        [TestCase(LobbyProtocol.Start)]
        public void ConfigMismatchCannotAdvanceAcknowledgementReadyOrStart(string kind)
        {
            BothReady(); ulong sender = kind == LobbyProtocol.Start ? 0ul : Peer;
            var request = Request(sender, kind, false); request.configFingerprint = new string('0', 64);
            ulong revision = authority.Revision;
            Assert.That(authority.Handle(sender, request, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("CONFIG_MISMATCH")); Assert.That(authority.Revision, Is.EqualTo(revision));
            Assert.That(authority.Phase, Is.EqualTo(LobbyProtocol.Lobby));
        }
        [Test] public void HostCanReadyAloneButCannotStartWithoutSecondVerifiedReadyParticipant()
        {
            Accept(0, LobbyProtocol.SetReady); Assert.That(authority.CanStart, Is.False);
            Assert.That(authority.Handle(0, Request(0, LobbyProtocol.Start), out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("BOTH_READY_REQUIRED"));
        }
        [Test] public void BothReadyPermitOnlyHostToCommitOneImmutableStartContract()
        {
            BothReady(); Assert.That(authority.CanStart, Is.True);
            Assert.That(authority.Handle(Peer, Request(Peer, LobbyProtocol.Start), out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("HOST_ONLY"));
            Accept(0, LobbyProtocol.Start);
            var value = authority.Snapshot(Nonce);
            Assert.That(value.phase, Is.EqualTo(LobbyProtocol.Playing)); Assert.That(value.canStart, Is.False);
            Assert.That(value.start.roundId, Is.EqualTo(1)); Assert.That(value.start.seed, Is.EqualTo(13579));
            Assert.That(value.start.configFingerprint, Is.EqualTo(authority.ConfigFingerprint));
            Assert.That(LobbyWire.ValidSnapshot(value), Is.True);
            Assert.That(authority.Handle(0, Request(0, LobbyProtocol.Start), out reason), Is.False);
            Assert.That(reason, Is.EqualTo("BATTLE_IN_PROGRESS"));
        }
        [Test] public void ReadyCanBeWithdrawnAndStartIsBlockedAgain()
        {
            BothReady(); Accept(Peer, LobbyProtocol.SetReady, false);
            Assert.That(authority.CanStart, Is.False);
            Assert.That(authority.Handle(0, Request(0, LobbyProtocol.Start), out _), Is.False);
            Accept(Peer, LobbyProtocol.SetReady); Assert.That(authority.CanStart, Is.True);
        }
        [Test] public void PlayingAdmissionExplainsBattleRatherThanOfferingReconnect()
        {
            BothReady(); Accept(0, LobbyProtocol.Start);
            Assert.That(authority.TryAdmit(99, Hello(), out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("BATTLE_IN_PROGRESS"));
        }
        [TestCase(0ul)] [TestCase(Peer)]
        public void EitherParticipantLeavingClosesEntireRoomAndDoesNotReassignRoles(ulong sender)
        {
            BothReady(); Accept(sender, LobbyProtocol.Leave);
            var value = authority.Snapshot(); Assert.That(value.phase, Is.EqualTo(LobbyProtocol.Closed));
            Assert.That(value.p2.clientId, Is.EqualTo(Peer)); Assert.That(value.canStart, Is.False);
            Assert.That(LobbyWire.ValidSnapshot(value), Is.True);
            Assert.That(authority.TryAdmit(99, Hello(), out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("ROOM_CLOSED"));
        }
        [Test] public void ClosingAfterStartPreservesContractButRevokesReadiness()
        {
            BothReady(); Accept(0, LobbyProtocol.Start); authority.Close("TRANSPORT_ENDED");
            var value = authority.Snapshot(); Assert.That(value.start, Is.Not.Null);
            Assert.That(value.p1.ready, Is.False); Assert.That(value.p2.ready, Is.False);
            Assert.That(LobbyWire.ValidSnapshot(value), Is.True);
            var revision = authority.Revision; authority.Close("OTHER");
            Assert.That(authority.Revision, Is.EqualTo(revision)); Assert.That(authority.CloseReason, Is.EqualTo("TRANSPORT_ENDED"));
        }
        [TestCase("session", "STALE_SESSION")]
        [TestCase("room", "STALE_SESSION")]
        [TestCase("protocol", "PROTOCOL_MISMATCH")]
        [TestCase("revision", "STALE_REVISION")]
        [TestCase("futureRevision", "STALE_REVISION")]
        [TestCase("sender", "NOT_A_PARTICIPANT")]
        public void StaleForeignOrFutureRequestCannotChangeLobby(string mutate, string expected)
        {
            Admit(); var request = Request(Peer, LobbyProtocol.AckInitial); ulong sender = Peer;
            if (mutate == "session") request.sessionId = Guid.NewGuid().ToString("N");
            if (mutate == "room") request.roomId = Guid.NewGuid().ToString("N");
            if (mutate == "protocol") request.protocol++;
            if (mutate == "revision") request.revision--;
            if (mutate == "futureRevision") request.revision++;
            if (mutate == "sender") sender = 99;
            var revision = authority.Revision;
            Assert.That(authority.Handle(sender, request, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo(expected)); Assert.That(authority.Revision, Is.EqualTo(revision));
            Assert.That(authority.Snapshot().p2.initialStateReceived, Is.False);
        }
        [Test] public void DuplicateAcceptedRequestAndOlderSequenceCannotMutateAgain()
        {
            Admit(); var request = Request(Peer, LobbyProtocol.AckInitial);
            Assert.That(authority.Handle(Peer, request, out _), Is.True);
            request.revision = authority.Revision;
            Assert.That(authority.Handle(Peer, request, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("DUPLICATE_REQUEST"));
            request.requestId = Guid.NewGuid().ToString("N");
            Assert.That(authority.Handle(Peer, request, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("STALE_SEQUENCE"));
        }
        [Test] public void DuplicateRejectedIntentCannotBecomeValidWhenStateChanges()
        {
            Admit(); var prematureReady = Request(Peer, LobbyProtocol.SetReady);
            Assert.That(authority.Handle(Peer, prematureReady, out _), Is.False);
            Accept(Peer, LobbyProtocol.AckInitial); prematureReady.revision = authority.Revision;
            Assert.That(authority.Handle(Peer, prematureReady, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("DUPLICATE_REQUEST"));
            Assert.That(authority.Snapshot().p2.ready, Is.False);
        }
        [Test] public void ConcurrentReadyChangeRequiresFreshRevisionAndNewRequestIdentity()
        {
            Admit(); Accept(Peer, LobbyProtocol.AckInitial);
            var stale = Request(Peer, LobbyProtocol.SetReady);
            Accept(0, LobbyProtocol.SetReady);
            Assert.That(authority.Handle(Peer, stale, out var reason), Is.False); Assert.That(reason, Is.EqualTo("STALE_REVISION"));
            Accept(Peer, LobbyProtocol.SetReady); Assert.That(authority.CanStart, Is.True);
        }
        [Test] public void ReturnedSnapshotsCannotMutateAuthorityPlayersOrStart()
        {
            BothReady(); var before = authority.Snapshot(); before.p2.clientId = 99; before.p1.ready = false;
            Assert.That(authority.CanStart, Is.True); Accept(0, LobbyProtocol.Start);
            var started = authority.Snapshot(); started.start.seed = 0; started.start.hostConfigJson = "{}";
            Assert.That(authority.Snapshot().start.seed, Is.EqualTo(13579));
            Assert.That(authority.Snapshot().p2.clientId, Is.EqualTo(Peer));
        }
    }
}
