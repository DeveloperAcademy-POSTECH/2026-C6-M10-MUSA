using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

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

        [Test]
        public void AGameJoinMayAddARoomTicketAfterTheTokenButALabJoinMayNot()
        {
            byte[] token = Encoding.ASCII.GetBytes("C6HapioGame/45");
            Assert.That(Read(token, token, true, out string ticket), Is.True);
            Assert.That(ticket, Is.Null);
            Assert.That(Read(Bytes("C6HapioGame/45|" + TicketA), token, true, out ticket), Is.True);
            Assert.That(ticket, Is.EqualTo(TicketA));
            Assert.That(Read(Bytes("C6HapioGame/45|" + TicketA), token, false, out _), Is.False, "A Lab room accepts only its token.");
            Assert.That(Read(Bytes("C6HapioGame/45|" + TicketA.ToUpperInvariant()), token, true, out _), Is.False);
            Assert.That(Read(Bytes("C6HapioGame/45|" + TicketA.Substring(1)), token, true, out _), Is.False);
            Assert.That(Read(Bytes("C6HapioGame/45:" + TicketA), token, true, out _), Is.False);
            Assert.That(Read(Bytes("C6HapioGame/44|" + TicketA), token, true, out _), Is.False, "An older game token is still a mismatch.");
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
                Assert.That(room.Approve(8, TicketB + "x"), Is.False);
                Assert.That(room.Approve(9, TicketA), Is.True, "The same room ticket gets its place back.");
                room.Connect(9);
                Assert.That(room.Network.HeldSeatCount, Is.Zero);
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(9UL), "The new connection gets the same P.");
                Assert.That(room.Network.ParticipantCount, Is.EqualTo(3));
            }
        }

        [Test]
        public void AGuestThatSaysGoodbyeFreesItsPAtOnceAndGetsItBackLaterOnlyIfStillFree()
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
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(9UL), "A free remembered P comes back.");
                room.SayGoodbye(9);
                room.Drop(9);
                room.Join(6, TicketB, 2);
                Assert.That(room.Approve(10, TicketA), Is.True);
                room.Connect(10);
                Assert.That(room.Network.GetSlotOwner(2), Is.EqualTo(6UL), "A P someone else took stays theirs.");
                Assert.That(room.Slots.GetSlot(10), Is.Zero);
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

            public void Connect(ulong id) => typeof(LabNetwork).GetMethod("OnConnected", Private).Invoke(Network, new object[] { id });
            public void Drop(ulong id) => typeof(LabNetwork).GetMethod("OnDisconnected", Private).Invoke(Network, new object[] { id });

            public void Join(ulong id, string ticket, int slot)
            {
                Assert.That(Approve(id, ticket), Is.True);
                Connect(id);
                Assert.That(Order, Does.Contain(id));
                Assert.That(Slots.TrySelect(id, slot, out string reason), Is.True, reason);
            }

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
