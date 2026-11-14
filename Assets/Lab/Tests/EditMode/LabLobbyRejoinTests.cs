using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// #501: the Host holds a dropped lobby guest's place and P number and gives them back to the same room ticket.
    /// The NGO callbacks are called directly on an inactive Host; the real connection is covered in PlayMode.
    /// </summary>
    public sealed class LabLobbyRejoinTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private const string TicketA = "0123456789abcdef0123456789abcdef";
        private const string TicketB = "fedcba9876543210fedcba9876543210";
        private const string TicketC = "00112233445566778899aabbccddeeff";

        [Test]
        public void AGameJoinMayAddARoomTicketAfterTheTokenButALabJoinMayNot()
        {
            byte[] token = Encoding.ASCII.GetBytes("C6HapioGame/55");
            Assert.That(Read(token, token, true, out string ticket), Is.True);
            Assert.That(ticket, Is.Null);
            Assert.That(Read(Bytes("C6HapioGame/55|" + TicketA), token, true, out ticket), Is.True);
            Assert.That(ticket, Is.EqualTo(TicketA));
            Assert.That(Read(Bytes("C6HapioGame/55|" + TicketA), token, false, out _), Is.False, "A Lab room accepts only its token.");
            Assert.That(Read(Bytes("C6HapioGame/55|" + TicketA.ToUpperInvariant()), token, true, out _), Is.False);
            Assert.That(Read(Bytes("C6HapioGame/55|" + TicketA.Substring(1)), token, true, out _), Is.False);
            Assert.That(Read(Bytes("C6HapioGame/55:" + TicketA), token, true, out _), Is.False);
            Assert.That(Read(Bytes("C6HapioGame/47|" + TicketA), token, true, out _), Is.False, "An older game token is still a mismatch.");
            Assert.That(Read(Bytes("C6HapioGame/48|" + TicketA), token, true, out _), Is.False, "The old player-stat contract is not compatible.");
            Assert.That(Read(Bytes("C6HapioGame/49|" + TicketA), token, true, out _), Is.False, "The separate summary-start branch does not carry this statistic yet.");
            Assert.That(Read(Bytes("C6HapioGame/51|" + TicketA), token, true, out _), Is.False,
                "#664: an app before the fox round and the second charm cannot join.");
            Assert.That(Read(Bytes("C6HapioGame/54|" + TicketA), token, true, out _), Is.False,
                "An app that waits forever on a member's result screen cannot join.");
            Assert.That(Read(Bytes("C6PhysicsLab/61"), Bytes("C6PhysicsLab/62"), false, out _), Is.False);
            Assert.That(Read(Bytes("C6PhysicsLab/62"), Bytes("C6PhysicsLab/62"), false, out _), Is.True);
            Assert.That(Read(null, token, true, out _), Is.False);
        }

        [Test]
        public void ADroppedLobbyGuestKeepsItsPlaceAndPForItsOwnTicketOnly()
        {
            using (var room = new HostRoom(3))
            {
                room.Join(5, TicketA, 2);
                room.Join(6, TicketB, 3);
                room.Drop(5);
                Assert.That(room.Network.HeldSeatCount, Is.EqualTo(1));
                Assert.That(room.Network.IsSlotHeld(2), Is.True);
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(5UL), "The old connection still owns the held P.");
                Assert.That(room.Network.ParticipantCount, Is.EqualTo(2), "A held place is not a connected participant.");
                Assert.That(room.Network.CanStartBattle, Is.False);
                Assert.That(room.Approve(7, null), Is.False, "A stranger cannot take the held place.");
                Assert.That(room.Approve(8, TicketC), Is.False, "Another device's ticket cannot take it either.");
                Assert.That(room.Approve(11, TicketB + "x"), Is.False, "A malformed ticket is a version mismatch.");
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(5UL));
                Assert.That(room.Approve(9, TicketA), Is.True, "The same room ticket gets its place back.");
                room.Connect(9);
                Assert.That(room.Network.HeldSeatCount, Is.Zero);
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(9UL), "The new connection gets the same P.");
                Assert.That(room.Network.ParticipantCount, Is.EqualTo(3));
            }
        }

        [Test]
        public void AStrangerInARoomWithSpaceDoesNotTakeAHeldP()
        {
            using (var room = new HostRoom(4))
            {
                room.Join(5, TicketA, 2);
                room.Drop(5);
                Assert.That(room.Approve(8, TicketC), Is.True, "The room still has a free place.");
                room.Connect(8);
                Assert.That(room.Network.HeldSeatCount, Is.EqualTo(1));
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(5UL));
                Assert.That(room.Slots.GetSlot(8), Is.Zero);
                Assert.That(room.Slots.TrySelect(8, 2, out string reason), Is.False, "The held P is taken.");
                Assert.That(reason, Is.EqualTo("SLOT_TAKEN"));
            }
        }

        [Test]
        public void TwoReturnsApprovedInTheSameFrameBothGetTheirPlacesBack()
        {
            using (var room = new HostRoom(3))
            {
                room.Join(5, TicketA, 2);
                room.Join(6, TicketB, 3);
                room.Drop(5);
                room.Drop(6);
                Assert.That(room.Network.HeldSeatCount, Is.EqualTo(2));
                // NGO approves every request polled in a frame before the first connect callback of the next frame.
                Assert.That(room.Approve(9, TicketA), Is.True);
                Assert.That(room.Approve(10, TicketB), Is.True, "A return waiting to connect is not a new place.");
                Assert.That(room.Approve(7, null), Is.False, "Both places are still taken.");
                room.Connect(9);
                room.Connect(10);
                Assert.That(room.Network.HeldSeatCount, Is.Zero);
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(9UL));
                Assert.That(room.Network.GetSlotOwner(3), Is.EqualTo(10UL));
                Assert.That(room.Network.ParticipantCount, Is.EqualTo(3));
            }
        }

        [Test]
        public void AGuestThatSaysGoodbyeFreesItsPAtOnceAndComesBackAsANewPlayer()
        {
            using (var room = new HostRoom(3))
            {
                room.Join(5, TicketA, 2);
                room.SayGoodbye(5);
                room.Drop(5);
                Assert.That(room.Network.HeldSeatCount, Is.Zero, "나가기 is not held.");
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(ulong.MaxValue));
                Assert.That(room.Approve(9, TicketA), Is.True);
                room.Connect(9);
                Assert.That(room.Slots.GetSlot(9), Is.Zero, "A place given up does not bring its P back.");
            }
        }

        [Test]
        public void AReturnAfterTheHoldEndedGetsItsPBackOnlyIfStillFree()
        {
            using (var room = new HostRoom(3))
            {
                room.Join(5, TicketA, 2);
                room.Drop(5);
                room.Expire(Time.realtimeSinceStartupAsDouble + LabNetwork.LobbySeatHoldSeconds + 1d);
                Assert.That(room.Network.HeldSeatCount, Is.Zero);
                Assert.That(room.Approve(9, TicketA), Is.True);
                room.Connect(9);
                room.Hear(9);
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(9UL), "The P it left with is still free.");
                room.Drop(9);
                room.Expire(Time.realtimeSinceStartupAsDouble + LabNetwork.LobbySeatHoldSeconds + 1d);
                room.Join(6, TicketB, 2);
                Assert.That(room.Approve(10, TicketA), Is.True);
                room.Connect(10);
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(6UL), "A P someone else took stays theirs.");
                Assert.That(room.Slots.GetSlot(10), Is.Zero);
            }
        }

        [Test]
        public void ADropAfterClearingItsPComesBackWithoutTheOldP()
        {
            using (var room = new HostRoom(3))
            {
                room.Join(5, TicketA, 2);
                room.Drop(5);
                Assert.That(room.Approve(9, TicketA), Is.True);
                room.Connect(9);
                room.Hear(9);
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(9UL));
                Assert.That(room.Slots.TrySelect(9, 0, out _), Is.True, "The player clears its own choice.");
                room.Drop(9);
                Assert.That(room.Network.HeldSeatCount, Is.EqualTo(1), "Held with no P.");
                Assert.That(room.Approve(10, TicketA), Is.True);
                room.Connect(10);
                Assert.That(room.Slots.GetSlot(10), Is.Zero, "It left with no P, so it comes back with none.");
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(ulong.MaxValue));
            }
        }

        [Test]
        public void AHeldPlaceOpensForAnyoneWhenItsHoldEnds()
        {
            using (var room = new HostRoom(2))
            {
                room.Join(5, TicketA, 2);
                room.Drop(5);
                Assert.That(room.Approve(7, null), Is.False);
                room.Expire(Time.realtimeSinceStartupAsDouble + LabNetwork.LobbySeatHoldSeconds + 1d);
                Assert.That(room.Network.HeldSeatCount, Is.Zero);
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(ulong.MaxValue));
                Assert.That(room.Approve(7, null), Is.True, "The place is open again.");
            }
        }

        [Test]
        public void AFullRoomCardOpensOnlyForATicketItsHostLetIn()
        {
            using (var room = new HostRoom(3, host: false))
            {
                const string key = "7777:합이오 방";
                LabDiscoveredRoom full = Room("합이오 방", 7777, 3, 3), other = Room("다른 방", 7778, 3, 3);
                LabDiscoveredRoom open = Room("다른 방", 7778, 2, 3), playing = Room("합이오 방", 7777, 3, 3, playing: true);
                Assert.That(room.Network.HoldsRoomTicket(key), Is.False, "No ticket before joining.");
                Assert.That(Open(room, open), Is.True);
                Assert.That(Open(room, full), Is.False);
                room.Invoke("PrepareRejoinTicket", key, (ushort)7777, false);
                string ticket = (string)room.Get("rejoinTicket");
                Assert.That(ticket, Does.Match("^[0-9a-f]{32}$"));
                Assert.That(room.Network.HoldsRoomTicket(key), Is.False, "A join the Host never let in holds no place.");
                Assert.That(Open(room, full), Is.False);
                // The Host lets this device into its lobby.
                room.Set("joinInProgress", true);
                room.Connect(0);
                Assert.That(room.Network.HoldsRoomTicket(key), Is.True);
                Assert.That(Open(room, full), Is.True, "The full room may be holding this device's place.");
                Assert.That(Open(room, other), Is.False, "Only the room the ticket was made for.");
                Assert.That(Open(room, playing), Is.False);
                room.Invoke("PrepareRejoinTicket", key, (ushort)7777, false);
                Assert.That(room.Get("rejoinTicket"), Is.EqualTo(ticket), "Joining the same room again keeps the ticket.");
                room.Invoke("PrepareRejoinTicket", "192.168.0.10:7777", (ushort)7777, true);
                Assert.That(room.Get("rejoinTicket"), Is.EqualTo(ticket), "Direct IP to the same Host port keeps an admitted ticket.");
                Assert.That(room.Network.HoldsRoomTicket(key), Is.True);
                // Lost outside a lobby (here: no lobby to return to): the Host holds nothing for it.
                room.Network.LobbyRejoinAllowed = false;
                room.Drop(0);
                Assert.That(room.Network.HoldsRoomTicket(key), Is.False);
                Assert.That(Open(room, full), Is.False);
            }
        }

        [Test]
        public void AReplacedConnectionThatClearedItsPComesBackWithoutTheOldP()
        {
            using (var room = new HostRoom(3))
            {
                room.Join(5, TicketA, 2);
                room.Drop(5);
                Assert.That(room.Approve(9, TicketA), Is.True);
                room.Connect(9);
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(9UL));
                Assert.That(room.Slots.TrySelect(9, 0, out _), Is.True, "The player clears its own choice.");
                // It comes back again while the Host still has connection 9.
                Assert.That(room.Approve(10, TicketA), Is.True);
                room.Connect(10);
                Assert.That(room.Slots.GetSlot(10), Is.Zero, "The connection it replaced had no P.");
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(ulong.MaxValue));
                room.Drop(9);
                Assert.That(room.Order, Is.EquivalentTo(new[] { 0UL, 10UL }));
            }
        }

        [Test]
        public void ADropDuringARunIsNeitherHeldNorRemembered()
        {
            using (var room = new HostRoom(3))
            {
                room.Join(5, TicketA, 2);
                room.Drop(5);
                Assert.That(room.Approve(9, TicketA), Is.True);
                room.Connect(9);
                room.Hear(9);
                Assert.That(room.Slots.TrySelect(9, 3, out _), Is.True, "It moves from P2 to P3.");
                room.Network.LockGameRoster();
                room.Drop(9);
                Assert.That(room.Network.HeldSeatCount, Is.Zero);
                // The room comes back to its lobby (2:2 #281), and the player returns from the room list.
                room.Network.UnlockGameRoster();
                Assert.That(room.Approve(10, TicketA), Is.True);
                room.Connect(10);
                Assert.That(room.Slots.GetSlot(10), Is.Zero, "#502: a drop in a run gets no P back, old or new.");
            }
        }

        [Test]
        public void AMemberTheHostNeverHeardFromIsNotHeld()
        {
            using (var room = new HostRoom(2))
            {
                // A join canceled just as the Host let it in: the device never learned it got in.
                Assert.That(room.Approve(5, TicketA), Is.True);
                room.Connect(5);
                room.Drop(5);
                Assert.That(room.Network.HeldSeatCount, Is.Zero);
                Assert.That(room.Approve(7, null), Is.True, "The place is free at once.");
            }
        }

        [Test]
        public void AReturnThatDropsBeforeItsFirstPacketKeepsItsPlaceHeld()
        {
            using (var room = new HostRoom(3))
            {
                room.Join(5, TicketA, 2);
                room.Drop(5);
                Assert.That(room.Approve(9, TicketA), Is.True);
                room.Connect(9);
                // Lost again before its config_ack reached the Host: it took back a place the Host had confirmed.
                room.Drop(9);
                Assert.That(room.Network.HeldSeatCount, Is.EqualTo(1));
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(9UL));
                Assert.That(room.Approve(10, TicketA), Is.True);
                room.Connect(10);
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(10UL));
            }
        }

        [Test]
        public void AReturnWithItsRememberedPThatDropsBeforeItsFirstPacketKeepsThatP()
        {
            using (var room = new HostRoom(3))
            {
                room.Join(5, TicketA, 2);
                room.Drop(5);
                room.Expire(Time.realtimeSinceStartupAsDouble + LabNetwork.LobbySeatHoldSeconds + 1d);
                Assert.That(room.Approve(9, TicketA), Is.True);
                room.Connect(9);
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(9UL), "The P it left with comes back from memory.");
                // Lost again before its config_ack reached the Host.
                room.Drop(9);
                Assert.That(room.Network.HeldSeatCount, Is.EqualTo(1));
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(9UL));
            }
        }

        [Test]
        public void ANewConnectionThatReplacesAHeardOneIsHeardToo()
        {
            using (var room = new HostRoom(3))
            {
                room.Join(5, TicketA, 2);
                Assert.That(room.Approve(9, TicketA), Is.True);
                room.Connect(9);
                room.Drop(9);
                Assert.That(room.Network.HeldSeatCount, Is.EqualTo(1), "It replaced a member the Host had heard from.");
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(9UL));
            }
        }

        [Test]
        public void ATwoVsTwoLobbyReturnDoesNotHoldAPlayerLostDuringTheBattle()
        {
            using (var room = new HostRoom(4))
            {
                room.Join(5, TicketA, 2);
                room.Join(6, TicketB, 3);
                room.Network.LockGameRoster();
                Assert.That(room.Network.ReturnGameToLobby(), Is.True, "The 2:2 battle ends and the room is a lobby again.");
                // 5 answers the settings the Host sends again; 6 was lost during the battle and never answers.
                room.Hear(5);
                room.Drop(6);
                Assert.That(room.Network.HeldSeatCount, Is.Zero, "#502: a player lost in the battle is not held.");
                Assert.That(room.Network.GetSlotOwner(3), Is.EqualTo(ulong.MaxValue));
                room.Drop(5);
                Assert.That(room.Network.HeldSeatCount, Is.EqualTo(1), "A player still in the reopened lobby is held.");
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(5UL));
            }
        }

        [Test]
        public void StaffRetryClearsAllSeatMemoryButKeepsTheRoomAndRejectsOldSelectionRequests()
        {
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            using (var room = new HostRoom(3))
            {
                room.Set("<DemoStageIndex>k__BackingField", 2);
                room.Join(5, TicketA, 2);
                room.Join(6, TicketB, 3);
                room.Drop(5);
                Assert.That(room.Network.HeldSeatCount, Is.EqualTo(1));
                Assert.That(((Dictionary<string, int>)room.Get("rememberedSlots")).Count, Is.EqualTo(1));
                var requestIds = (Dictionary<ulong, long>)room.Get("slotRequestIds");
                requestIds[6] = 100;
                var acknowledgements = (HashSet<ulong>)room.Get("configAcknowledgements");
                acknowledgements.UnionWith(new[] { 0UL, 6UL });
                var tickets = new Dictionary<ulong, string>((Dictionary<ulong, string>)room.Get("memberTickets"));
                room.Network.LockGameRoster();

                Assert.That(room.Network.ReturnStaffDemoGameToLobby(), Is.True);
                Assert.That(room.Order, Is.EqualTo(new[] { 0UL, 6UL }), "No connected device is removed from the room.");
                Assert.That(room.Network.LocalSlotNumber, Is.Zero, "Host must also choose again.");
                Assert.That(room.Network.SelectedParticipantCount, Is.Zero);
                Assert.That(room.Network.HeldSeatCount, Is.Zero);
                Assert.That(((Dictionary<string, int>)room.Get("rememberedSlots")), Is.Empty);
                Assert.That((Dictionary<ulong, string>)room.Get("memberTickets"), Is.EqualTo(tickets));
                Assert.That(requestIds[6], Is.EqualTo(100), "A late old slot request remains stale after the reset.");
                Assert.That(acknowledgements, Is.EquivalentTo(new[] { 0UL }), "Guests reconfirm their settings.");
                Assert.That(room.Network.CanStartBattle, Is.False);
                Assert.That(room.Network.CanSelectPlayerSlot, Is.True);
                Assert.That(room.Network.DemoStageIndex, Is.EqualTo(2));
                Assert.That(room.Approve(9, TicketA), Is.True);
                room.Connect(9);
                Assert.That(room.Slots.GetSlot(9), Is.Zero, "Returning ticket must not restore an old P.");
                Assert.That(room.Network.ParticipantCount, Is.EqualTo(3));
                Assert.That(room.Network.CanStartBattle, Is.False, "Three devices do not automatically choose or start.");
            }
        }

        [Test]
        public void StaffRetryResetCannotChangeANormalRoomOrAGuest()
        {
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(false))
            using (var normal = new HostRoom(3))
            {
                normal.Join(5, TicketA, 2);
                Assert.That(normal.Network.ReturnStaffDemoGameToLobby(), Is.False);
                Assert.That(normal.Slots.GetSlot(0), Is.EqualTo(1));
                Assert.That(normal.Slots.GetSlot(5), Is.EqualTo(2));
            }
            using (LabGameBuildVariant.OverrideStaffStageDemoForTests(true))
            using (var guest = new HostRoom(3, false))
            {
                guest.Set("<DemoStageIndex>k__BackingField", 2);
                Assert.That(guest.Network.ReturnStaffDemoGameToLobby(), Is.False);
            }
        }

        [Test]
        public void AGuestThatQuitTheRunIsNotHeldWhenTheRoomReturnsToItsLobby()
        {
            using (var room = new HostRoom(4))
            {
                room.Join(5, TicketA, 2);
                room.Network.LockGameRoster();
                // Its "exit" control during the battle (GameSession marks it), then the 2:2 room returns to its lobby.
                room.Network.MarkLobbyLeaver(5);
                room.Network.UnlockGameRoster();
                room.Drop(5);
                Assert.That(room.Network.HeldSeatCount, Is.Zero, "It quit on purpose; its place is not held.");
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(ulong.MaxValue));
            }
        }

        [Test]
        public void ADirectIpTicketAlsoOpensTheRoomCardOnceTheLobbyNamesTheRoom()
        {
            using (var room = new HostRoom(3, host: false))
            {
                room.Network.port = 54321;
                room.Invoke("PrepareRejoinTicket", "192.168.0.10:54321", (ushort)54321, true);
                room.Set("joinInProgress", true);
                room.Connect(0);
                Assert.That(room.Network.HoldsRoomTicket("54321:합이오 1"), Is.False, "The room name is not known yet.");
                Type type = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket", true);
                object packet = Activator.CreateInstance(type);
                void Field(string name, object value) => type.GetField(name).SetValue(packet, value);
                Field("type", "lobby");
                Field("targetParticipants", 3);
                Field("seats", new ulong[] { 0 });
                Field("slotAssignments", new[] { new LabPlayerSlotAssignment { playerId = 0, slotNumber = 1 } });
                Field("lobbyRevision", 1L);
                Field("roomName", "합이오 1");
                room.Invoke("ReceiveLobby", packet);
                Assert.That(room.Network.HoldsRoomTicket("54321:합이오 1"), Is.True, "The room card of the same room.");
                Assert.That(Open(room, Room("합이오 1", 54321, 3, 3)), Is.True);
                Assert.That(Open(room, Room("합이오 2", 54322, 3, 3)), Is.False);
            }
        }

        [Test]
        public void AReturnTheHostHasNotTimedOutYetReplacesTheOldConnectionAndKeepsItsP()
        {
            using (var room = new HostRoom(3))
            {
                room.Join(5, TicketA, 2);
                room.Join(6, TicketB, 3);
                Assert.That(room.Approve(7, null), Is.False, "The room is full.");
                Assert.That(room.Approve(9, TicketA), Is.True, "Its own old connection does not make the room full.");
                room.Connect(9);
                Assert.That(room.Order, Is.EquivalentTo(new[] { 0UL, 9UL, 6UL }));
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(9UL));
                room.Drop(5);
                Assert.That(room.Order, Is.EquivalentTo(new[] { 0UL, 9UL, 6UL }), "The replaced connection's late disconnect changes nothing.");
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(9UL));
                Assert.That(room.Network.HeldSeatCount, Is.Zero);
            }
        }

        [Test]
        public void ADropAfterTheRunStartedHoldsNothing()
        {
            using (var room = new HostRoom(2))
            {
                room.Join(5, TicketA, 2);
                room.Network.LockGameRoster();
                room.Drop(5);
                Assert.That(room.Network.HeldSeatCount, Is.Zero, "#502: a drop in a run keeps the existing Game Over or lobby return.");
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(ulong.MaxValue));
            }
        }

        [Test]
        public void AGuestLobbyCountsAHeldSeatAsTakenButNotConnected()
        {
            using (var room = new HostRoom(3, host: false))
            {
                Type type = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket", true);
                object packet = Activator.CreateInstance(type);
                void Field(string name, object value) => type.GetField(name).SetValue(packet, value);
                Field("type", "lobby");
                Field("targetParticipants", 3);
                Field("seats", new ulong[] { 0, 6, 5 });
                Field("ids", new[] { "5" });
                Field("slotAssignments", new[]
                {
                    new LabPlayerSlotAssignment { playerId = 0, slotNumber = 1 },
                    new LabPlayerSlotAssignment { playerId = 5, slotNumber = 2 },
                    new LabPlayerSlotAssignment { playerId = 6, slotNumber = 3 },
                });
                Field("lobbyRevision", 4L);
                Field("roomName", "합이오 방");
                MethodInfo receive = typeof(LabNetwork).GetMethod("ReceiveLobby", Private);
                receive.Invoke(room.Network, new[] { packet });
                Assert.That(room.Network.ParticipantCount, Is.EqualTo(2));
                Assert.That(room.Network.SelectedParticipantCount, Is.EqualTo(3));
                Assert.That(room.Network.HeldSeatCount, Is.EqualTo(1));
                Assert.That(room.Network.IsSlotHeld(2), Is.True);
                Assert.That(room.Network.IsSlotHeld(3), Is.False);
                Field("lobbyRevision", 5L);
                Field("ids", null);
                receive.Invoke(room.Network, new[] { packet });
                Assert.That(room.Network.ParticipantCount, Is.EqualTo(3), "A seat the Host no longer holds is a player again.");
                Assert.That(room.Network.HeldSeatCount, Is.Zero);
            }
        }

        [Test]
        public void AGuestLeavesALobbyWhoseSettingsFailTheirIntegrityCheck()
        {
            using (var room = new HostRoom(3, host: false))
            {
                room.Network.LobbyRejoinAllowed = true;
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("C6_LAB_CONFIG_REJECTED .*leave=True"));
                room.ReceiveConfig(Config(room, 2, fingerprint: "corrupt"));
                Assert.That(room.Get("stopAfterDisconnect"), Is.True, "The next Update stops and says lobby_leave.");
                Assert.That(room.Network.Status, Does.Contain("integrity"));
                Assert.That(room.Get("clientConfigReady"), Is.False);
            }
        }

        [Test]
        public void AGuestLeavesALobbyOrLabRoomWhoseSettingsCannotInstall()
        {
            using (var room = new HostRoom(3, host: false))
            {
                // No source config: TryInstallValues refuses, as it would for any setting this build cannot hold.
                room.Network.LobbyRejoinAllowed = true;
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("C6_LAB_CONFIG_REJECTED .*leave=True"));
                room.ReceiveConfig(Config(room, 2));
                Assert.That(room.Get("stopAfterDisconnect"), Is.True);
                Assert.That(room.Network.Status, Does.StartWith("Host settings rejected"));
            }
            using (var room = new HostRoom(3, host: false))
            {
                room.Set("gameFlowEnabled", false);
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("C6_LAB_CONFIG_REJECTED .*leave=True"));
                room.ReceiveConfig(Config(room, 2));
                Assert.That(room.Get("stopAfterDisconnect"), Is.True, "A Lab room has no run; it leaves too.");
            }
        }

        [Test]
        public void DuringAGameRunARejectedSettingKeepsTheConnectionForTheHostsBudget()
        {
            using (var room = new HostRoom(3, host: false))
            {
                Assert.That(room.Network.LobbyRejoinAllowed, Is.False, "GameSession turns it on only in the lobby.");
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("C6_LAB_CONFIG_REJECTED .*leave=False"));
                room.ReceiveConfig(Config(room, 2, fingerprint: "corrupt"));
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("C6_LAB_CONFIG_REJECTED .*leave=False"));
                room.ReceiveConfig(Config(room, 3));
                Assert.That(room.Get("stopAfterDisconnect"), Is.False);
                Assert.That(room.Network.Status, Does.StartWith("Host settings rejected"));
            }
        }

        [Test]
        public void StaleOrMalformedSettingsNeverMakeAGuestLeaveAndARepeatIsAcknowledgedAgain()
        {
            using (var room = new HostRoom(3, host: false))
            {
                var source = ScriptableObject.CreateInstance<LabConfig>();
                try
                {
                    room.Network.config = source;
                    room.Network.LobbyRejoinAllowed = true;
                    object current = Config(room, 4);
                    LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("C6_LAB_CONFIG_READY revision=4"));
                    room.ReceiveConfig(current);
                    Assert.That(room.Get("clientConfigReady"), Is.True);
                    Assert.That(room.Get("configRevision"), Is.EqualTo(4));
                    int acks = room.Acks;
                    room.ReceiveConfig(Config(room, 4));
                    Assert.That(room.Acks, Is.EqualTo(acks + 1), "A duplicate of the installed revision is acknowledged again.");
                    // Reordered: an older generation, even one that would fail its own check, changes nothing.
                    room.ReceiveConfig(Config(room, 3));
                    room.ReceiveConfig(Config(room, 3, fingerprint: "corrupt"));
                    // Malformed: dropped before any check.
                    object shortTuning = Config(room, 5);
                    Type type = shortTuning.GetType();
                    type.GetField("tuning").SetValue(shortTuning, new float[3]);
                    room.ReceiveConfig(shortTuning);
                    object noRevision = Config(room, 0, fingerprint: "corrupt");
                    room.ReceiveConfig(noRevision);
                    object oversize = Config(room, 6, fingerprint: "corrupt");
                    type.GetField("targetParticipants").SetValue(oversize, LabModel.TerritoryMaximumParticipants + 1);
                    room.ReceiveConfig(oversize);
                    Assert.That(room.Get("stopAfterDisconnect"), Is.False);
                    Assert.That(room.Get("configRevision"), Is.EqualTo(4));
                    Assert.That(room.Get("clientConfigReady"), Is.True);
                    LogAssert.NoUnexpectedReceived();
                }
                finally
                {
                    var installed = (UnityEngine.Object)room.Get("sessionConfig");
                    if (installed != null) UnityEngine.Object.DestroyImmediate(installed);
                    UnityEngine.Object.DestroyImmediate(source);
                }
            }
        }

        [Test]
        public void TheLargestLobbyAndConfigPacketsFitInOneControlMessage()
        {
            using (var room = new HostRoom(LabModel.TerritoryMaximumParticipants))
            {
                var source = ScriptableObject.CreateInstance<LabConfig>();
                try
                {
                    room.Network.config = source;
                    for (ulong id = ulong.MaxValue - 5; id != ulong.MaxValue; id++)
                    {
                        room.Order.Add(id);
                        Assert.That(room.Slots.TrySelect(id, room.Order.Count, out string reason), Is.True, reason);
                    }
                    Assert.That(room.Order.Count, Is.EqualTo(LabModel.TerritoryMaximumParticipants));
                    room.Set("connectedRoomName", new string('방', 63));
                    room.Set("configRevision", int.MaxValue);
                    room.Set("configFingerprint", new string('f', 64));
                    room.Set("snapshotSequence", long.MaxValue);
                    room.Set("lobbyRevision", long.MaxValue - 1);
                    int limit = (int)typeof(LabNetwork).GetField("MaximumPacketBytes", BindingFlags.NonPublic | BindingFlags.Static)
                        .GetRawConstantValue();
                    object lobby = room.Call("LobbyPacket");
                    // Every seat is held: the ids list is as long as it can be.
                    lobby.GetType().GetField("ids").SetValue(lobby, room.Order.Select(id => id.ToString()).ToArray());
                    object settings = room.Call("SessionConfigPacket");
                    float[] tuning = (float[])settings.GetType().GetField("tuning").GetValue(settings);
                    Assert.That(tuning.Length, Is.EqualTo(LabConfig.DeveloperFields.Count));
                    int lobbyBytes = Encoded(lobby), configBytes = Encoded(settings);
                    // The widest float text JsonUtility writes, in every field.
                    for (int i = 0; i < tuning.Length; i++) tuning[i] = -1.17549435E-38f;
                    int worstConfigBytes = Encoded(settings);
                    TestContext.WriteLine("lobby=" + lobbyBytes + " config=" + configBytes + " worstConfig=" + worstConfigBytes
                        + " limit=" + limit);
                    Assert.That(lobbyBytes, Is.LessThanOrEqualTo(limit));
                    Assert.That(configBytes, Is.LessThanOrEqualTo(limit));
                    Assert.That(worstConfigBytes, Is.LessThanOrEqualTo(limit));
                }
                finally { UnityEngine.Object.DestroyImmediate(source); }
            }
        }

        private static int Encoded(object packet) => (int)typeof(LabNetwork)
            .GetMethod("EncodedPacketBytes", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { packet });

        /// <summary>A Host config packet for this room, with a correct fingerprint unless one is given.</summary>
        private static object Config(HostRoom room, int revision, string fingerprint = null)
        {
            Type type = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket", true);
            object packet = Activator.CreateInstance(type);
            var defaults = ScriptableObject.CreateInstance<LabConfig>();
            float[] tuning;
            try { tuning = defaults.CaptureDeveloperValues(); }
            finally { UnityEngine.Object.DestroyImmediate(defaults); }
            ushort protocol = (ushort)typeof(LabNetwork).GetField("LabProtocolVersion", BindingFlags.NonPublic | BindingFlags.Static)
                .GetRawConstantValue();
            type.GetField("type").SetValue(packet, "config");
            type.GetField("targetParticipants").SetValue(packet, 3);
            type.GetField("configRevision").SetValue(packet, revision);
            type.GetField("tuning").SetValue(packet, tuning);
            type.GetField("configFingerprint").SetValue(packet, fingerprint ?? LabSessionSettings.Fingerprint(tuning, 3, protocol));
            return packet;
        }

        private static LabDiscoveredRoom Room(string name, ushort port, int participants, int capacity, bool playing = false) =>
            LabRoomCatalog.GroupTargets(new[] { new LabBonjourTarget("127.0.0.1", port, 1, name, participants, capacity, playing) })[0];

        private static bool Open(HostRoom room, LabDiscoveredRoom listed) =>
            (bool)typeof(LabNetwork).GetMethod("RoomOpenToThisDevice", Private).Invoke(room.Network, new object[] { listed });

        private static bool Read(byte[] payload, byte[] token, bool game, out string ticket)
        {
            object[] arguments = { payload, token, game, null };
            bool read = (bool)typeof(LabNetwork).GetMethod("TryReadConnectionPayload", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, arguments);
            ticket = (string)arguments[3];
            return read;
        }

        private static byte[] Bytes(string text) => Encoding.ASCII.GetBytes(text);

        /// <summary>An inactive LabNetwork whose fake NGO manager reports a listening Host (or a connected Guest).</summary>
        private sealed class HostRoom : IDisposable
        {
            private readonly GameObject root;
            private readonly Component manager;
            private readonly Type managerType;
            public readonly LabNetwork Network;

            public HostRoom(int capacity, bool host = true)
            {
                root = new GameObject("Inactive #501 lobby");
                root.SetActive(false);
                managerType = typeof(LabNetwork).GetField("manager").FieldType;
                manager = root.AddComponent(managerType);
                Network = root.AddComponent<LabNetwork>();
                typeof(LabNetwork).GetField("manager").SetValue(Network, manager);
                Assert.That(Network.SetGameFlowEnabled(true), Is.True);
                object client = managerType.GetProperty("LocalClient").GetValue(manager);
                client.GetType().GetProperty("IsServer", Private).SetValue(client, host);
                client.GetType().GetProperty("IsClient", Private).SetValue(client, true);
                managerType.GetProperty("IsListening").SetValue(manager, true);
                typeof(LabNetwork).GetMethod("ResetLocal", Private).Invoke(Network, null);
                typeof(LabNetwork).GetField("targetParticipantCount", Private).SetValue(Network, capacity);
                typeof(LabNetwork).GetField("playerSlots", Private).SetValue(Network, new LabPlayerSlots(capacity));
                if (!host) return;
                Order.Add(0);
                Assert.That(Slots.TrySelect(0, 1, out _), Is.True, "The Host sits at P1.");
            }

            public List<ulong> Order => (List<ulong>)typeof(LabNetwork).GetField("order", Private).GetValue(Network);
            public LabPlayerSlots Slots => (LabPlayerSlots)typeof(LabNetwork).GetField("playerSlots", Private).GetValue(Network);

            public bool Approve(ulong id, string ticket)
            {
                MethodInfo approve = typeof(LabNetwork).GetMethod("Approve", Private);
                Type requestType = approve.GetParameters()[0].ParameterType;
                Type responseType = approve.GetParameters()[1].ParameterType;
                object request = Activator.CreateInstance(requestType);
                string token = Encoding.ASCII.GetString((byte[])typeof(LabNetwork).GetProperty("ActiveConnectionToken", Private)
                    .GetValue(Network));
                requestType.GetField("Payload").SetValue(request, Bytes(ticket == null ? token : token + "|" + ticket));
                requestType.GetField("ClientNetworkId").SetValue(request, id);
                object response = Activator.CreateInstance(responseType);
                approve.Invoke(Network, new[] { request, response });
                return (bool)responseType.GetField("Approved").GetValue(response);
            }

            public object Get(string field) => typeof(LabNetwork).GetField(field, Private).GetValue(Network);
            public void Set(string field, object value) => typeof(LabNetwork).GetField(field, Private).SetValue(Network, value);
            public void Invoke(string method, params object[] arguments) =>
                typeof(LabNetwork).GetMethod(method, Private).Invoke(Network, arguments);
            public object Call(string method) => typeof(LabNetwork).GetMethod(method, Private).Invoke(Network, null);
            public void ReceiveConfig(object packet) => Invoke("ReceiveSessionConfig", packet);
            public int Acks => (int)typeof(LabNetwork).GetProperty("ConfigAcknowledgementsSent", Private).GetValue(Network);

            public void Connect(ulong id) => typeof(LabNetwork).GetMethod("OnConnected", Private).Invoke(Network, new object[] { id });
            public void Drop(ulong id) => typeof(LabNetwork).GetMethod("OnDisconnected", Private).Invoke(Network, new object[] { id });

            public void Join(ulong id, string ticket, int slot)
            {
                Assert.That(Approve(id, ticket), Is.True);
                Connect(id);
                Assert.That(Order, Does.Contain(id));
                Hear(id);
                Assert.That(Slots.TrySelect(id, slot, out string reason), Is.True, reason);
            }

            /// <summary>The Host received a packet from the member (its config_ack), so it saw its admission.</summary>
            public void Hear(ulong id) => ((HashSet<ulong>)Get("heardLobbyMembers")).Add(id);

            public void SayGoodbye(ulong id) =>
                ((HashSet<ulong>)typeof(LabNetwork).GetField("lobbyLeavers", Private).GetValue(Network)).Add(id);

            public void Expire(double now) =>
                typeof(LabNetwork).GetMethod("ExpireLobbySeatHolds", Private).Invoke(Network, new object[] { now });

            public void Dispose()
            {
                // The manager was never initialized. Do not ask its teardown to stop a fake listener.
                managerType.GetProperty("IsListening").SetValue(manager, false);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
