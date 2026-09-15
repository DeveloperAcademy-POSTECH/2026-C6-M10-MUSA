using System;
using System.Collections.Generic;
using System.Linq;
using C6.Prototype.Lobby;
using C6.Prototype.Lobby.Discovery;
using C6.Prototype.Networking;
using NUnit.Framework;

namespace C6.Prototype.Tests.Lobby
{
    public sealed class P4LobbyTests
    {
        private const string Room = "11111111111111111111111111111111";
        private const string Session = "22222222222222222222222222222222";
        private LobbyAuthority authority;
        private readonly Dictionary<ulong, ulong> sequences = new Dictionary<ulong, ulong>();
        private readonly Dictionary<ulong, string> nonces = new Dictionary<ulong, string>();
        [SetUp] public void SetUp()
        {
            authority = new LobbyAuthority(Room, Session, "24", "{\"schema\":2}", 789, 5, true);
            sequences.Clear(); nonces.Clear();
        }
        private LobbyHello Hello(ulong id)
        {
            if (!nonces.ContainsKey(id)) nonces[id] = Guid.NewGuid().ToString("N");
            return new LobbyHello { protocol = 24, build = "24", roomId = Room, clientNonce = nonces[id] };
        }
        private void Admit(ulong id) => Assert.That(authority.TryAdmit(id, Hello(id), out var error), Is.True, error);
        private LobbyRequest Request(ulong id, string kind, bool ready = true)
        {
            sequences.TryGetValue(id, out ulong sequence); sequences[id] = ++sequence;
            return new LobbyRequest { protocol = 24, roomId = Room, sessionId = Session, sequence = sequence,
                requestId = Guid.NewGuid().ToString("N"), revision = authority.Revision, kind = kind, ready = ready,
                configFingerprint = authority.ConfigFingerprint };
        }
        private void Accept(ulong id, string kind) => Assert.That(authority.Handle(id, Request(id, kind), out var error), Is.True, error);
        private void Fill(int count)
        { for (ulong id = 1; id < (ulong)count; id++) { Admit(id); Accept(id, LobbyProtocol.AckInitial); } }
        private void ReadyAll()
        { foreach (var player in authority.Snapshot().OrderedPlayers) Accept(player.clientId, LobbyProtocol.SetReady); }

        [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void EverySupportedCountStartsOnlyAfterEveryConfigurationAndReady(int count)
        {
            Fill(count); Accept(0, LobbyProtocol.SetReady);
            Assert.That(authority.Handle(0, Request(0, LobbyProtocol.Start), out var error), Is.False);
            Assert.That(error, Is.EqualTo("ALL_READY_REQUIRED"));
            foreach (var player in authority.Snapshot().OrderedPlayers.Skip(1)) Accept(player.clientId, LobbyProtocol.SetReady);
            Assert.That(authority.CanStart, Is.True);
            Accept(0, LobbyProtocol.Start);
            var snapshot = authority.Snapshot();
            Assert.That(LobbyWire.ValidSnapshot(snapshot), Is.True);
            Assert.That(snapshot.Capacity, Is.EqualTo(5));
            Assert.That(snapshot.start.participantIds, Is.EqualTo(Enumerable.Range(0, count).Select(i => (ulong)i)));
            snapshot.start.participantIds[1] = 900;
            Assert.That(authority.Snapshot().start.participantIds[1], Is.EqualTo(1));
        }
        [Test]
        public void JoinOrderDefinesDifferentLeftAndRightNeighboursAndRoundTripKeepsIt()
        {
            Admit(91); Admit(7); Admit(43); Admit(8);
            var snapshot = authority.Snapshot();
            Assert.That(snapshot.OrderedPlayers.Select(p => p.clientId), Is.EqualTo(new ulong[] { 0, 91, 7, 43, 8 }));
            Assert.That(snapshot.LeftPlayerNumber(0), Is.EqualTo(5));
            Assert.That(snapshot.RightPlayerNumber(0), Is.EqualTo(2));
            Assert.That(snapshot.LeftPlayerNumber(7), Is.EqualTo(2));
            Assert.That(snapshot.RightPlayerNumber(7), Is.EqualTo(4));
            Assert.That(snapshot.LeftPlayerNumber(900), Is.Zero);
            Assert.That(LobbyWire.TryDecode(LobbyWire.Encode(snapshot), out LobbySnapshot parsed), Is.True);
            Assert.That(LobbyWire.ValidSnapshot(parsed), Is.True);
            Assert.That(parsed.OrderedPlayers.Select(p => p.clientId), Is.EqualTo(snapshot.OrderedPlayers.Select(p => p.clientId)));
        }
        [Test]
        public void SixthAndDuplicateIdOrNonceCannotConsumeOrReplaceASeat()
        {
            Admit(1);
            var duplicate = Hello(2); duplicate.clientNonce = nonces[1];
            Assert.That(authority.TryAdmit(2, duplicate, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("DUPLICATE_PARTICIPANT"));
            Assert.That(authority.TryAdmit(1, Hello(1), out _), Is.False);
            Admit(2); Admit(3); Admit(4); ulong revision = authority.Revision;
            Assert.That(authority.TryAdmit(5, Hello(5), out reason), Is.False);
            Assert.That(reason, Is.EqualTo("ROOM_FULL"));
            Assert.That(authority.ParticipantCount, Is.EqualTo(5));
            Assert.That(authority.Revision, Is.EqualTo(revision));
        }
        [Test]
        public void LobbyLeaveCompactsSeatsKeepsConfigAcknowledgementAndResetsEveryReady()
        {
            Fill(5); ReadyAll(); var before = authority.Snapshot();
            Assert.That(authority.RemoveParticipant(2, out _), Is.True);
            var after = authority.Snapshot();
            Assert.That(after.phase, Is.EqualTo(LobbyProtocol.Lobby));
            Assert.That(after.OrderedPlayers.Select(p => p.clientId), Is.EqualTo(new ulong[] { 0, 1, 3, 4 }));
            Assert.That(after.OrderedPlayers.Select(p => p.playerNumber), Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(after.OrderedPlayers.All(p => p.initialStateReceived && !p.ready), Is.True);
            Assert.That(after.canStart, Is.False);
            Assert.That(authority.NonceFor(2), Is.Null);
            Assert.That(LobbyWire.AcceptsSnapshot(before, after, "24", ""), Is.True);
            Admit(9); Assert.That(authority.Snapshot().LocalPlayerNumber(9), Is.EqualTo(5));
            Assert.That(authority.Handle(2, Request(2, LobbyProtocol.SetReady), out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("NOT_A_PARTICIPANT"));
        }
        [Test]
        public void P2SeatMayLeaveAndSurvivingClientCanBecomeP2()
        {
            Fill(3); var before = authority.Snapshot();
            Assert.That(authority.RemoveParticipant(1, out _), Is.True);
            var after = authority.Snapshot();
            Assert.That(after.p2.clientId, Is.EqualTo(2));
            Assert.That(LobbyWire.AcceptsSnapshot(before, after, "24", ""), Is.True);
        }
        [Test]
        public void JoiningAnotherPlayerRevokesExistingReadyWithoutRollingBackAcknowledgement()
        {
            Fill(2); ReadyAll(); Admit(7);
            Assert.That(authority.CanStart, Is.False);
            Assert.That(authority.Snapshot().OrderedPlayers.All(p => !p.ready), Is.True);
            Assert.That(authority.Snapshot().Find(1).initialStateReceived, Is.True);
            Assert.That(authority.Handle(7, Request(7, LobbyProtocol.SetReady), out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("INITIAL_STATE_REQUIRED"));
        }
        [Test]
        public void BattleRosterIsFrozenAndDisconnectClosesWholeRoom()
        {
            Fill(3); ReadyAll(); Accept(0, LobbyProtocol.Start); var started = authority.Snapshot();
            Assert.That(authority.TryAdmit(9, Hello(9), out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("BATTLE_IN_PROGRESS"));
            Assert.That(authority.RemoveParticipant(1, out _), Is.True);
            var closed = authority.Snapshot();
            Assert.That(closed.phase, Is.EqualTo(LobbyProtocol.Closed));
            Assert.That(closed.start.participantIds, Is.EqualTo(started.start.participantIds));
            Assert.That(closed.OrderedPlayers.Select(p => p.clientId), Is.EqualTo(started.start.participantIds));
            Assert.That(LobbyWire.ValidSnapshot(closed), Is.True);
        }
        [TestCase("duplicate")] [TestCase("seat")] [TestCase("mirror")]
        [TestCase("host")] [TestCase("null")] [TestCase("oversize")]
        public void MalformedCanonicalRosterOrLegacyMirrorIsRejected(string mutation)
        {
            Fill(5); var value = authority.Snapshot();
            if (mutation == "duplicate") value.players[4].clientId = value.players[3].clientId;
            if (mutation == "seat") value.players[4].playerNumber = 2;
            if (mutation == "mirror") value.p2 = new LobbyPlayer { clientId = 99, playerNumber = 2, connected = true };
            if (mutation == "host") value.players[4].clientId = 0;
            if (mutation == "null") value.players[3] = null;
            if (mutation == "oversize") value.players = value.players.Concat(new[] { new LobbyPlayer { clientId = 50, playerNumber = 6, connected = true } }).ToArray();
            Assert.That(LobbyWire.ValidSnapshot(value), Is.False);
        }
        [Test]
        public void ChangedFrozenStartIdsAndReorderingSurvivorsAreRejected()
        {
            Fill(3); var before = authority.Snapshot(); var reordered = authority.Snapshot(); reordered.revision++;
            (reordered.players[1], reordered.players[2]) = (reordered.players[2], reordered.players[1]);
            reordered.players[1].playerNumber = 2; reordered.players[2].playerNumber = 3; reordered.p2 = reordered.players[1];
            Assert.That(LobbyWire.ValidSnapshot(reordered), Is.True);
            Assert.That(LobbyWire.AcceptsSnapshot(before, reordered, "24", ""), Is.False);
            ReadyAll(); Accept(0, LobbyProtocol.Start); var started = authority.Snapshot();
            started.start.participantIds[2] = 900;
            Assert.That(LobbyWire.ValidSnapshot(started), Is.False);
        }
        [Test]
        public void FiveConcurrentReadyRequestsFromOneRosterRevisionAllSucceed()
        {
            Fill(5);
            var requests = authority.Snapshot().OrderedPlayers.Select(p => (p.clientId, request: Request(p.clientId, LobbyProtocol.SetReady))).ToArray();
            foreach (var item in requests)
                Assert.That(authority.Handle(item.clientId, item.request, out var error), Is.True, error);
            Assert.That(authority.CanStart, Is.True);
            Assert.That(authority.Snapshot().OrderedPlayers.All(p => p.ready), Is.True);
        }
        [TestCase(true)] [TestCase(false)]
        public void ReadyFromBeforeAJoinOrLeaveCannotRestoreResetReadiness(bool join)
        {
            Fill(3); var oldReady = Request(1, LobbyProtocol.SetReady);
            if (join) Admit(7); else Assert.That(authority.RemoveParticipant(2, out _), Is.True);
            Assert.That(authority.Handle(1, oldReady, out var error), Is.False);
            Assert.That(error, Is.EqualTo("STALE_REVISION"));
            Assert.That(authority.Snapshot().Find(1).ready, Is.False);
        }
        [Test]
        public void ConfigurationAckSurvivesUnrelatedJoinButRemovedSenderAndOldSessionDoNot()
        {
            Admit(1); var ack = Request(1, LobbyProtocol.AckInitial); Admit(2);
            Assert.That(authority.Handle(1, ack, out var error), Is.True, error);
            Assert.That(authority.Snapshot().Find(1).initialStateReceived, Is.True);
            var removedAck = Request(2, LobbyProtocol.AckInitial);
            Assert.That(authority.RemoveParticipant(2, out _), Is.True);
            Assert.That(authority.Handle(2, removedAck, out error), Is.False);
            Assert.That(error, Is.EqualTo("NOT_A_PARTICIPANT"));
            var foreign = Request(1, LobbyProtocol.SetReady); foreign.sessionId = Guid.NewGuid().ToString("N");
            Assert.That(authority.Handle(1, foreign, out error), Is.False);
            Assert.That(error, Is.EqualTo("STALE_SESSION"));
        }
        [Test]
        public void FutureReadyAndAckBeforeOwnAdmissionAndStaleHostStartRemainRejected()
        {
            Admit(1); var earlyAck = Request(1, LobbyProtocol.AckInitial); earlyAck.revision = 1;
            Assert.That(authority.Handle(1, earlyAck, out var error), Is.False);
            Assert.That(error, Is.EqualTo("STALE_REVISION"));
            Accept(1, LobbyProtocol.AckInitial);
            var future = Request(1, LobbyProtocol.SetReady); future.revision = authority.Revision + 1;
            Assert.That(authority.Handle(1, future, out error), Is.False);
            Assert.That(error, Is.EqualTo("STALE_REVISION"));
            var staleStart = Request(0, LobbyProtocol.Start);
            ReadyAll(); Assert.That(authority.Handle(0, staleStart, out error), Is.False);
            // ReadyAll advances the host's sequence, so old Start is rejected even before revision validation.
            Assert.That(error, Is.EqualTo("STALE_SEQUENCE"));
            var latestSequenceOldRevision = Request(0, LobbyProtocol.Start); latestSequenceOldRevision.revision--;
            Assert.That(authority.Handle(0, latestSequenceOldRevision, out error), Is.False);
            Assert.That(error, Is.EqualTo("STALE_REVISION"));
        }
        [Test]
        public void ReadyReceiptAfterAnotherPlayersReadyResolvesAgainstCurrentRoster()
        {
            Fill(3); var current = authority.Snapshot(nonces[1]);
            var ready = Request(1, LobbyProtocol.SetReady); Accept(0, LobbyProtocol.SetReady);
            Assert.That(authority.Handle(1, ready, out _), Is.True);
            var receipt = authority.Snapshot(nonces[1]);
            Assert.That(LobbyWire.AcceptsReceipt(current, receipt, 1, "24", nonces[1], Room, ready.revision), Is.True);
        }
        [Test]
        public void LegacyProtocolCannotEnterP4OrCarryItsRoster()
        {
            var hello = Hello(1); hello.protocol = LobbyProtocol.Version;
            Assert.That(authority.TryAdmit(1, hello, out var error), Is.False);
            Assert.That(error, Is.EqualTo("PROTOCOL_MISMATCH"));
            var legacy = new LobbyAuthority(Room, Session, "24", "{}", 1).Snapshot();
            legacy.players = legacy.OrderedPlayers;
            Assert.That(LobbyWire.ValidSnapshot(legacy), Is.False);
        }
        [Test]
        public void P3AndP4CannotMixEvenWhenBuildStringsMatch()
        {
            var hello=Hello(1); hello.protocol=23;
            Assert.That(authority.TryAdmit(1,hello,out var error),Is.False);
            Assert.That(error,Is.EqualTo("PROTOCOL_MISMATCH"));
            var older=new LobbyAuthority(Room,Session,"24","{}",1,5);
            Assert.That(older.TryAdmit(1,Hello(1),out error),Is.False);
            Assert.That(error,Is.EqualTo("PROTOCOL_MISMATCH"));
            Assert.Throws<ArgumentException>(()=>new LobbyAuthority(Room,Session,"24","{}",1,2,true));
        }
        [Test]
        public void P4StartCarriesFrozenTransferModeAndCannotDowngradeIt()
        {
            Fill(3); ReadyAll(); Accept(0,LobbyProtocol.Start);
            var started=authority.Snapshot();
            Assert.That(started.start.continuousTransfers,Is.True);
            Assert.That(started.protocol,Is.EqualTo(LobbyProtocol.ContinuousTransferVersion));
            started.start.continuousTransfers=false;
            Assert.That(LobbyWire.ValidSnapshot(started),Is.False);
            started.start.continuousTransfers=true; started.protocol=23;
            Assert.That(LobbyWire.ValidSnapshot(started),Is.False);
        }
        [Test]
        public void GenericReservationPoolBoundsFiveConcurrentApprovalsAndReusesReleasedSeat()
        {
            var policy = new TwoParticipantAdmissionPolicy(5); policy.BeginHostSession();
            for (ulong id = 1; id <= 4; id++) Assert.That(policy.TryReserve(id), Is.True);
            Assert.That(policy.TryReserve(5), Is.False);
            Assert.That(policy.ReservedCount, Is.EqualTo(5));
            policy.Release(2); Assert.That(policy.TryReserve(5), Is.True);
            policy.EndSession(); Assert.That(policy.TryReserve(6), Is.False);
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void BonjourP4CountRoundTripsAndSixIsRejected(int count)
        {
            var room = new RoomAdvertisement { RoomId = Room, Name = "C6", ProtocolVersion = 24, Build = "24",
                ConfigHash = new string('a', 64), Participants = count, Port = 7777 };
            Assert.That(RoomAdvertisementCodec.TryDecode(RoomAdvertisementCodec.Encode(room), 7777, out var parsed, out _), Is.True);
            Assert.That(parsed.Participants, Is.EqualTo(count));
            room.Participants = 6; Assert.That(RoomAdvertisementCodec.Validate(room, out _), Is.False);
        }
    }
}
