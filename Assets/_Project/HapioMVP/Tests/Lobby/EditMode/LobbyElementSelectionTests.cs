using System;
using System.Collections.Generic;
using System.Linq;
using C6.Prototype.Lobby;
using C6.Prototype.Lobby.Discovery;
using C6.Prototype.Presentation;
using NUnit.Framework;

namespace C6.Prototype.Tests.Lobby
{
    public sealed class LobbyElementSelectionTests
    {
        private const string Room = "11111111111111111111111111111111";
        private const string Session = "22222222222222222222222222222222";
        private LobbyAuthority authority;
        private readonly Dictionary<ulong, ulong> sequences = new Dictionary<ulong, ulong>();

        [SetUp] public void SetUp()
        {
            authority = new LobbyAuthority(Room, Session, "56", "{\"schema\":2}", 123, 5, true, true);
            sequences.Clear();
        }
        private void Admit(ulong id)
        {
            Assert.That(authority.TryAdmit(id, new LobbyHello { protocol = LobbyProtocol.ElementSelectionVersion,
                build = "56", roomId = Room, clientNonce = Guid.NewGuid().ToString("N") }, out string error), Is.True, error);
        }
        private LobbyRequest Request(ulong id, string kind, OrbElement element = OrbElement.None, bool ready = true)
        {
            sequences.TryGetValue(id, out ulong sequence); sequences[id] = ++sequence;
            return new LobbyRequest { protocol = LobbyProtocol.ElementSelectionVersion, roomId = Room, sessionId = Session,
                requestId = Guid.NewGuid().ToString("N"), sequence = sequence, revision = authority.Revision,
                selectionRevision = authority.SelectionRevision, kind = kind, ready = ready, selectedElement = element,
                configFingerprint = authority.ConfigFingerprint };
        }
        private void Accept(ulong id, string kind, OrbElement element = OrbElement.None)
            => Assert.That(authority.Handle(id, Request(id, kind, element), out string error), Is.True, error);
        private void Fill(int count)
        { for (ulong id = 1; id < (ulong)count; id++) { Admit(id); Accept(id, LobbyProtocol.AckInitial); } }
        private void SelectAll()
        { foreach (var player in authority.Snapshot().OrderedPlayers) Accept(player.clientId, LobbyProtocol.SelectElement, LobbyProtocol.Elements[player.playerNumber - 1]); }
        private void ReadyAll()
        { foreach (var player in authority.Snapshot().OrderedPlayers) Accept(player.clientId, LobbyProtocol.SetReady); }
        private void Start(int count = 3)
        { Fill(count); SelectAll(); ReadyAll(); Accept(0, LobbyProtocol.Start); }

        [Test] public void NewRoomAndNewParticipantStayUnselectedAndCannotReady()
        {
            Fill(3); var snapshot = authority.Snapshot();
            Assert.That(snapshot.OrderedPlayers.All(p => p.selectedElement == OrbElement.None && !p.ready), Is.True);
            Assert.That(LobbyWire.ValidSnapshot(snapshot), Is.True);
            foreach (var player in snapshot.OrderedPlayers)
            {
                Assert.That(authority.Handle(player.clientId, Request(player.clientId, LobbyProtocol.SetReady), out string error), Is.False);
                Assert.That(error, Is.EqualTo("ELEMENT_SELECTION_REQUIRED"));
            }
            Assert.That(authority.CanStart, Is.False);
            Assert.That(snapshot.LocalSeatNumber(0), Is.Zero);
            Assert.That(snapshot.LeftPlayerNumber(0), Is.Zero, "Unconfirmed seats must not appear as join-order neighbours.");
        }
        [TestCase(OrbElement.Fire)] [TestCase(OrbElement.Water)] [TestCase(OrbElement.Wood)] [TestCase(OrbElement.Metal)] [TestCase(OrbElement.Earth)]
        public void EveryAvailableElementIsHostConfirmedForTheAuthenticatedSender(OrbElement element)
        {
            Fill(2); Accept(1, LobbyProtocol.SelectElement, element);
            Assert.That(authority.Snapshot().Find(1).selectedElement, Is.EqualTo(element));
            Assert.That(authority.Snapshot().p1.selectedElement, Is.EqualTo(OrbElement.None));
            Assert.That(LobbyWire.ValidSnapshot(authority.Snapshot()), Is.True);
        }
        [TestCase(OrbElement.None)] [TestCase((OrbElement)99)] [TestCase((OrbElement)(-1))]
        public void InvalidChoiceCannotChangeRevisionOrOccupancy(OrbElement element)
        {
            Fill(2); Accept(1, LobbyProtocol.SelectElement, OrbElement.Water);
            ulong revision = authority.Revision;
            Assert.That(authority.Handle(1, Request(1, LobbyProtocol.SelectElement, element), out string error), Is.False);
            Assert.That(error, Is.EqualTo("INVALID_ELEMENT"));
            Assert.That(authority.Revision, Is.EqualTo(revision));
            Assert.That(authority.Snapshot().Find(1).selectedElement, Is.EqualTo(OrbElement.Water));
        }
        [Test] public void SelectBeforeConfigurationAckAndNonParticipantRequestsCannotMutate()
        {
            Admit(1);
            Assert.That(authority.Handle(1, Request(1, LobbyProtocol.SelectElement, OrbElement.Fire), out string error), Is.False);
            Assert.That(error, Is.EqualTo("INITIAL_STATE_REQUIRED"));
            Assert.That(authority.Handle(900, Request(900, LobbyProtocol.SelectElement, OrbElement.Fire), out error), Is.False);
            Assert.That(error, Is.EqualTo("NOT_A_PARTICIPANT"));
            Assert.That(authority.Snapshot().Find(1).selectedElement, Is.EqualTo(OrbElement.None));
        }
        [Test] public void SimultaneousSameChoiceApprovesExactlyOneAndDoesNotAutoAssignLoser()
        {
            Fill(2);
            var hostRequest = Request(0, LobbyProtocol.SelectElement, OrbElement.Fire);
            var peerRequest = Request(1, LobbyProtocol.SelectElement, OrbElement.Fire);
            Assert.That(authority.Handle(0, hostRequest, out string error), Is.True, error);
            ulong revision = authority.Revision;
            Assert.That(authority.Handle(1, peerRequest, out error), Is.False);
            Assert.That(error, Is.EqualTo("ELEMENT_ALREADY_SELECTED"));
            Assert.That(authority.Snapshot().Find(1).selectedElement, Is.EqualTo(OrbElement.None));
            Assert.That(authority.Revision, Is.EqualTo(revision));
        }
        [Test] public void OccupiedChangeAndSameChoicePreserveExistingChoiceAndEveryReady()
        {
            Fill(3); SelectAll(); ReadyAll(); ulong revision = authority.Revision; ulong barrier = authority.SelectionRevision;
            Assert.That(authority.Handle(1, Request(1, LobbyProtocol.SelectElement, OrbElement.Fire), out string error), Is.False);
            Assert.That(error, Is.EqualTo("ELEMENT_ALREADY_SELECTED"));
            Accept(1, LobbyProtocol.SelectElement, OrbElement.Water);
            Assert.That(authority.Revision, Is.EqualTo(revision));
            Assert.That(authority.SelectionRevision, Is.EqualTo(barrier));
            Assert.That(authority.Snapshot().Find(1).selectedElement, Is.EqualTo(OrbElement.Water));
            Assert.That(authority.Snapshot().OrderedPlayers.All(p => p.ready), Is.True);
            Assert.That(authority.CanStart, Is.True);
        }
        [Test] public void SuccessfulChoiceChangeAtomicallyReleasesOldElementAndResetsEveryReady()
        {
            Fill(3); SelectAll(); ReadyAll(); ulong revision = authority.Revision;
            Accept(1, LobbyProtocol.SelectElement, OrbElement.Metal);
            var snapshot = authority.Snapshot();
            Assert.That(snapshot.revision, Is.EqualTo(revision + 1));
            Assert.That(snapshot.selectionRevision, Is.EqualTo(snapshot.revision));
            Assert.That(snapshot.OrderedPlayers.All(p => !p.ready), Is.True);
            Assert.That(snapshot.Find(1).selectedElement, Is.EqualTo(OrbElement.Metal));
            Accept(2, LobbyProtocol.SelectElement, OrbElement.Water);
            Assert.That(authority.Snapshot().Find(2).selectedElement, Is.EqualTo(OrbElement.Water));
            Assert.That(LobbyWire.ValidSnapshot(authority.Snapshot()), Is.True);
        }
        [Test] public void ReadySentBeforeAnElementChangeCannotRestoreReadinessAfterIt()
        {
            Fill(3); SelectAll();
            var oldReady = Request(1, LobbyProtocol.SetReady);
            Accept(0, LobbyProtocol.SelectElement, OrbElement.Metal);
            Assert.That(authority.Handle(1, oldReady, out string error), Is.False);
            Assert.That(error, Is.EqualTo("SELECTION_CHANGED_READY_AGAIN"));
            Assert.That(authority.Snapshot().Find(1).ready, Is.False);
            Accept(1, LobbyProtocol.SetReady);
            Assert.That(authority.Snapshot().Find(1).ready, Is.True);
        }
        [Test] public void ReadyFromSameSelectionBarrierRemainsIndependentAcrossParticipants()
        {
            Fill(5); SelectAll();
            var requests = authority.Snapshot().OrderedPlayers.Select(p => (p.clientId, request: Request(p.clientId, LobbyProtocol.SetReady))).ToArray();
            foreach (var request in requests) Assert.That(authority.Handle(request.clientId, request.request, out string error), Is.True, error);
            Assert.That(authority.CanStart, Is.True);
        }
        [Test] public void ClearSelectionEnablesFivePlayerChoiceSwapWithoutAutomaticAssignments()
        {
            Fill(5); SelectAll(); ReadyAll();
            Assert.That(authority.Handle(1, Request(1, LobbyProtocol.SelectElement, OrbElement.Fire), out string error), Is.False);
            Assert.That(error, Is.EqualTo("ELEMENT_ALREADY_SELECTED"));
            Accept(0, LobbyProtocol.ClearElement);
            Assert.That(authority.Snapshot().p1.selectedElement, Is.EqualTo(OrbElement.None));
            Assert.That(authority.Snapshot().OrderedPlayers.All(p => !p.ready), Is.True);
            Assert.That(authority.CanStart, Is.False);
            Accept(1, LobbyProtocol.SelectElement, OrbElement.Fire);
            Accept(0, LobbyProtocol.SelectElement, OrbElement.Water);
            Assert.That(authority.Snapshot().p1.selectedElement, Is.EqualTo(OrbElement.Water));
            Assert.That(authority.Snapshot().Find(1).selectedElement, Is.EqualTo(OrbElement.Fire));
        }
        [Test] public void LastAvailableElementIsNotAssignedUntilTheFifthPlayerExplicitlySelectsIt()
        {
            Fill(5);
            foreach (var player in authority.Snapshot().OrderedPlayers.Take(4))
                Accept(player.clientId, LobbyProtocol.SelectElement, LobbyProtocol.Elements[player.playerNumber-1]);
            Assert.That(authority.Snapshot().Find(4).selectedElement, Is.EqualTo(OrbElement.None));
            Accept(4, LobbyProtocol.SelectElement, OrbElement.Earth);
            Assert.That(authority.Snapshot().Find(4).selectedElement, Is.EqualTo(OrbElement.Earth));
        }
        [Test] public void ParticipantRemovalReleasesElementAndJoiningReplacementStartsUnselected()
        {
            Fill(3); SelectAll(); ReadyAll();
            Assert.That(authority.RemoveParticipant(1, out string error), Is.True, error);
            Assert.That(authority.Snapshot().OrderedPlayers.Any(p => p.selectedElement == OrbElement.Water), Is.False);
            Admit(8); Accept(8, LobbyProtocol.AckInitial);
            Assert.That(authority.Snapshot().Find(8).selectedElement, Is.EqualTo(OrbElement.None));
            Accept(8, LobbyProtocol.SelectElement, OrbElement.Water);
            Assert.That(LobbyWire.ValidSnapshot(authority.Snapshot()), Is.True);
        }
        [TestCase(2)] [TestCase(3)] [TestCase(5)]
        public void StartFreezesUniqueChoicesAndSeparateSeatPermutationWithoutChangingPIdentity(int count)
        {
            Start(count); var snapshot = authority.Snapshot();
            Assert.That(LobbyWire.ValidSnapshot(snapshot), Is.True);
            Assert.That(snapshot.start.elementSelection, Is.True);
            Assert.That(snapshot.start.participantIds, Is.EqualTo(Enumerable.Range(0,count).Select(i=>(ulong)i)));
            Assert.That(snapshot.start.selectedElements, Is.EqualTo(LobbyProtocol.Elements.Take(count)));
            Assert.That(snapshot.start.roundSeatOrder, Is.EquivalentTo(snapshot.start.participantIds));
            Assert.That(snapshot.start.roundSeatOrder.Distinct().Count(), Is.EqualTo(count));
            Assert.That(snapshot.p1.clientId, Is.Zero);
            Assert.That(snapshot.p1.playerNumber, Is.EqualTo(1));
            var priorSeats = (ulong[])snapshot.start.roundSeatOrder.Clone();
            Assert.That(authority.Handle(1, Request(1, LobbyProtocol.SelectElement, OrbElement.Earth), out string error), Is.False);
            Assert.That(error, Is.EqualTo("BATTLE_IN_PROGRESS"));
            Assert.That(authority.Handle(1, Request(1, LobbyProtocol.ClearElement), out error), Is.False);
            Assert.That(authority.Snapshot().start.roundSeatOrder, Is.EqualTo(priorSeats));
            snapshot.start.selectedElements[0] = OrbElement.None; snapshot.start.roundSeatOrder[0] = 800;
            Assert.That(authority.Snapshot().start.selectedElements[0], Is.EqualTo(OrbElement.Fire));
            Assert.That(authority.Snapshot().start.roundSeatOrder, Is.EqualTo(priorSeats));
        }
        private sealed class ZeroRandom : Random { public override int Next(int maxValue) => 0; }
        [Test] public void HostCanOccupyLastSeatAndNeighboursUseSeatsRatherThanPlayerNumbers()
        {
            Start(); var snapshot = authority.Snapshot();
            snapshot.start.roundSeatOrder = LobbyAuthority.ShuffleSeats(snapshot.start.participantIds, new ZeroRandom());
            Assert.That(snapshot.start.roundSeatOrder, Is.EqualTo(new ulong[]{1,2,0}));
            Assert.That(snapshot.LocalSeatNumber(0), Is.EqualTo(3));
            Assert.That(snapshot.LeftPlayerNumber(0), Is.EqualTo(3));
            Assert.That(snapshot.RightPlayerNumber(0), Is.EqualTo(2));
            Assert.That(snapshot.p1.playerNumber, Is.EqualTo(1));
            Assert.That(LobbyWire.ValidSnapshot(snapshot), Is.True);
            Assert.That(LobbyAuthority.ShuffleSeats(snapshot.start.participantIds,new ZeroRandom()), Is.EqualTo(snapshot.start.roundSeatOrder),
                "Repeating an arrangement is permitted; the implementation must not force a different result.");
        }
        [Test] public void SameRoomLobbyReturnRetainsChoiceButRevokesReadyAndNextRoundCannotReuseOldId()
        {
            Start(); var started = authority.Snapshot();
            Assert.That(authority.ReturnToLobby(4), Is.True, "Game retries can advance beyond the initial lobby start round.");
            var returned = authority.Snapshot();
            Assert.That(returned.phase, Is.EqualTo(LobbyProtocol.Lobby));
            Assert.That(returned.start, Is.Null); Assert.That(returned.roundId, Is.Zero);
            Assert.That(returned.OrderedPlayers.Select(p=>p.selectedElement), Is.EqualTo(started.start.selectedElements));
            Assert.That(returned.OrderedPlayers.All(p=>!p.ready), Is.True);
            Assert.That(LobbyWire.AcceptsSnapshot(started,returned,"56","",Room), Is.True);
            ReadyAll(); Accept(0,LobbyProtocol.Start);
            Assert.That(authority.Snapshot().roundId, Is.EqualTo(5));
            Assert.That(LobbyWire.ValidSnapshot(authority.Snapshot()), Is.True);
            Assert.That(LobbyWire.AcceptsSnapshot(authority.Snapshot(),started,"56","",Room), Is.False);
        }
        [Test] public void LobbyReturnBeforeStartAndStaleRoundAreRejectedWithoutStateChanges()
        {
            Assert.That(authority.ReturnToLobby(1), Is.False);
            Start(); ulong revision = authority.Revision;
            Assert.That(authority.ReturnToLobby(0), Is.False);
            Assert.That(authority.ReturnToLobby(uint.MaxValue), Is.False);
            Assert.That(authority.Revision, Is.EqualTo(revision));
        }
        [Test] public void DuplicateSelectionRequestCannotRunAgainAfterItsElementBecomesAvailable()
        {
            Fill(2); Accept(0,LobbyProtocol.SelectElement,OrbElement.Fire);
            var rejected = Request(1,LobbyProtocol.SelectElement,OrbElement.Fire);
            Assert.That(authority.Handle(1,rejected,out _), Is.False);
            Accept(0,LobbyProtocol.ClearElement); rejected.revision = authority.Revision;
            Assert.That(authority.Handle(1,rejected,out string error), Is.False);
            Assert.That(error, Is.EqualTo("DUPLICATE_REQUEST"));
            Assert.That(authority.Snapshot().Find(1).selectedElement, Is.EqualTo(OrbElement.None));
        }
        [TestCase("duplicate")] [TestCase("invalid")] [TestCase("missing")]
        public void MalformedSelectionSnapshotsAreRejected(string mutation)
        {
            Fill(3); SelectAll(); var snapshot = authority.Snapshot();
            if (mutation=="duplicate") snapshot.players[2].selectedElement=OrbElement.Fire;
            if (mutation=="invalid") snapshot.players[2].selectedElement=(OrbElement)99;
            if (mutation=="missing") snapshot.selectionRevision=0;
            Assert.That(LobbyWire.ValidSnapshot(snapshot), Is.False);
        }
        [TestCase("duplicateSeat")] [TestCase("missingSeat")] [TestCase("foreignSeat")] [TestCase("choiceMismatch")] [TestCase("disabled")]
        public void MalformedFrozenStartChoicesOrSeatsCannotBeAccepted(string mutation)
        {
            Start(); var snapshot = authority.Snapshot();
            if (mutation=="duplicateSeat") snapshot.start.roundSeatOrder[1]=snapshot.start.roundSeatOrder[0];
            if (mutation=="missingSeat") snapshot.start.roundSeatOrder=Array.Empty<ulong>();
            if (mutation=="foreignSeat") snapshot.start.roundSeatOrder[1]=400;
            if (mutation=="choiceMismatch") snapshot.start.selectedElements[2]=OrbElement.Earth;
            if (mutation=="disabled") snapshot.start.elementSelection=false;
            Assert.That(LobbyWire.ValidSnapshot(snapshot), Is.False);
        }
        [Test] public void WireRoundTripCarriesChoicesSeatOrderAndSelectionBarrier()
        {
            Start(5); var snapshot = authority.Snapshot();
            Assert.That(LobbyWire.TryDecode(LobbyWire.Encode(snapshot),out LobbySnapshot parsed), Is.True);
            Assert.That(LobbyWire.ValidSnapshot(parsed), Is.True);
            Assert.That(parsed.selectionRevision, Is.EqualTo(snapshot.selectionRevision));
            Assert.That(parsed.start.roundSeatOrder, Is.EqualTo(snapshot.start.roundSeatOrder));
            Assert.That(parsed.start.selectedElements, Is.EqualTo(snapshot.start.selectedElements));
            Assert.That(LobbyWire.AcceptsReceipt(snapshot,parsed,0,"56","",Room,snapshot.revision), Is.True);
        }
        [Test] public void SameRevisionReceiptWithDifferentSeatOrderCannotResolvePendingRequest()
        {
            Start(3); var current = authority.Snapshot(); var forged = authority.Snapshot();
            Array.Reverse(forged.start.roundSeatOrder);
            Assert.That(LobbyWire.ValidSnapshot(forged), Is.True);
            Assert.That(LobbyWire.AcceptsReceipt(current,forged,0,"56","",Room,current.revision), Is.False);
        }
        [Test] public void PlayingSeatOrSelectionMutationAndSelectionRollbackAreRejected()
        {
            Start(); var current = authority.Snapshot(); var changed = authority.Snapshot();
            changed.revision++; Array.Reverse(changed.start.roundSeatOrder);
            Assert.That(LobbyWire.AcceptsSnapshot(current,changed,"56","",Room), Is.False);
            changed = authority.Snapshot(); changed.revision++; changed.selectionRevision--;
            Assert.That(LobbyWire.AcceptsSnapshot(current,changed,"56","",Room), Is.False);
        }
        [Test] public void LegacyP4CannotJoinElementRoomAndNewChoiceFieldsCannotDowngradeIntoOldRoom()
        {
            var hello = new LobbyHello{protocol=24,build="56",roomId=Room,clientNonce=Guid.NewGuid().ToString("N")};
            Assert.That(authority.TryAdmit(1,hello,out string error), Is.False);
            Assert.That(error, Is.EqualTo("PROTOCOL_MISMATCH"));
            var snapshot = authority.Snapshot(); snapshot.protocol=24;
            Assert.That(LobbyWire.ValidSnapshot(snapshot), Is.False);
            Assert.Throws<ArgumentException>(()=>new LobbyAuthority(Room,Session,"56","{}",1,5,false,true));
        }
        [Test] public void BonjourElementContractSupportsFiveAndRejectsSixParticipants()
        {
            var room = new RoomAdvertisement{RoomId=Room,Name="C6",ProtocolVersion=56,Build="56",
                ConfigHash=new string('a',64),Participants=5,Port=7777};
            Assert.That(RoomAdvertisementCodec.TryDecode(RoomAdvertisementCodec.Encode(room),7777,out var parsed,out _), Is.True);
            Assert.That(parsed.Participants, Is.EqualTo(5));
            room.Participants=6; Assert.That(RoomAdvertisementCodec.Validate(room,out _), Is.False);
        }
    }
}
