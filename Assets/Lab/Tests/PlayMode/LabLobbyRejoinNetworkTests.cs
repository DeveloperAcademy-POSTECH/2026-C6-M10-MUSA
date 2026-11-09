using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>
    /// #501 on real NGO and UTP sockets: a game-family Host and Guest in one Editor process, on loopback, with a 2 s
    /// transport timeout. A drop is made by stopping the Guest's NGO update loop, so it sends no heartbeats. A real
    /// iOS suspension, a lost Wi-Fi and a second device are not reproduced.
    /// </summary>
    [Category("Slow"), Category("Bonjour")]
    public sealed class LabLobbyRejoinNetworkTests
    {
        private const int DisconnectTimeoutMs = 2000;
        private Peer host, guest;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            guest?.Dispose();
            host?.Dispose();
            guest = host = null;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ALobbyGuestThatTimesOutComesBackToTheSameRoomAndP()
        {
            yield return StartRoom();
            ulong first = guest.Network.LocalPlayerId;
            guest.Freeze(true);
            yield return Until(() => host.Network.HeldSeatCount == 1, 10d, "The Host never held the dropped guest's place.");
            Assert.That(host.Network.IsSlotHeld(2), Is.True);
            Assert.That(host.Network.GetSlotOwner(2), Is.EqualTo(first));
            Assert.That(host.Network.ParticipantCount, Is.EqualTo(1));
            guest.Freeze(false);
            yield return UntilBackInTheRoom(first);
        }

        [UnityTest]
        public IEnumerator AResumeAfterAPauseLongerThanTheTimeoutRejoinsWithoutWaitingForTheTransport()
        {
            yield return StartRoom();
            ulong first = guest.Network.LocalPlayerId;
            MethodInfo resume = typeof(LabNetwork).GetMethod("ResumeLobbyAfterPause", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That((bool)resume.Invoke(guest.Network, new object[] { 1d }), Is.False,
                "A pause shorter than the transport timeout keeps the connection.");
            Assert.That(guest.Network.IsRejoiningLobby, Is.False);
            Assert.That((bool)resume.Invoke(guest.Network, new object[] { 2.5d }), Is.True);
            Assert.That(guest.Network.IsRejoiningLobby, Is.True);
            Assert.That(guest.Network.IsJoiningRoom, Is.True, "GameSession keeps a rejoining lobby.");
            yield return UntilBackInTheRoom(first);
        }

        [UnityTest]
        public IEnumerator ARejoinToAnUnreachableHostEndsAtItsBudgetEvenDuringAnAttempt()
        {
            yield return StartRoom();
            host.Freeze(true);
            yield return Until(() => guest.Network.IsRejoiningLobby, 10d, "The guest never noticed the silent Host.");
            // The 30 s budget is shortened to end 2 s from now, inside the first 12 s attempt to the silent Host.
            FieldInfo until = typeof(LabNetwork).GetField("lobbyRejoinUntil", BindingFlags.Instance | BindingFlags.NonPublic);
            double deadline = Time.realtimeSinceStartupAsDouble + 2d;
            until.SetValue(guest.Network, deadline);
            yield return Until(() => !guest.Network.IsJoiningRoom, 8d, "The rejoin ran past its budget.");
            Assert.That(Time.realtimeSinceStartupAsDouble - deadline, Is.LessThan(2d),
                "It ended at its budget, not when its 12 s attempt ran out.");
            Assert.That(guest.Network.IsRejoiningLobby, Is.False);
            Assert.That(guest.Network.IsConnected, Is.False, "GameSession now takes the existing room-list path.");
            host.Freeze(false);
        }

        [UnityTest]
        public IEnumerator AGuestThatLeavesFreesItsPAtOnce()
        {
            yield return StartRoom();
            Assert.That(Ticket(guest), Is.Not.Null);
            guest.Network.Stop();
            Assert.That(Ticket(guest), Is.Null, "A guest that said goodbye gave its ticket up.");
            yield return Until(() => host.Network.ParticipantCount == 1, 10d, "The Host never saw the guest leave.");
            Assert.That(host.Network.HeldSeatCount, Is.Zero, "나가기 does not hold the place.");
            Assert.That(host.Network.GetSlotOwner(2), Is.EqualTo(ulong.MaxValue));
            Assert.That(guest.Network.IsRejoiningLobby, Is.False);
        }

        [UnityTest]
        public IEnumerator AGuestDoesNotRejoinARoomItsHostClosed()
        {
            yield return StartRoom();
            host.Network.Stop();
            yield return Until(() => !guest.Network.IsConnected, 10d, "The guest never saw the Host close the room.");
            yield return null;
            Assert.That(guest.Network.IsRejoiningLobby, Is.False);
            Assert.That(guest.Network.IsJoiningRoom, Is.False, "GameSession goes back to the room list at once.");
        }

        private IEnumerator StartRoom()
        {
            var config = Resources.Load<LabConfig>("LabConfig");
            Assert.That(config, Is.Not.Null);
            ushort port = LabSavedSceneNetworkFixture.AvailableDualFamilyPort();
            host = new Peer("#501 Host", config, port);
            Assert.That(host.Network.SetTargetParticipantCount(2), Is.True);
            host.Network.Host();
            yield return Until(() => host.Network.IsHost && host.Network.IsConnected, 10d, "The Host did not start.");
            Assert.That(host.Network.RequestPlayerSlot(1), Is.True);
            guest = new Peer("#501 Guest", config, port);
            guest.Network.LobbyRejoinAllowed = true;
            guest.Network.Join("127.0.0.1");
            yield return Until(() => guest.Network.IsConnected && guest.Network.CanSelectPlayerSlot, 15d,
                "The guest did not reach the lobby.");
            Assert.That(guest.Network.RequestPlayerSlot(2), Is.True);
            yield return Until(() => guest.Network.LocalSlotNumber == 2
                && host.Network.GetSlotOwner(2) == guest.Network.LocalPlayerId, 10d, "The guest did not get P2.");
        }

        /// <summary>
        /// The guest is back on a new connection with P2, and in between it never looked lost (connected or joining
        /// every frame). Until the guest's transport notices the drop, its old connection still looks connected.
        /// </summary>
        private IEnumerator UntilBackInTheRoom(ulong previous)
        {
            bool lost = false;
            yield return Until(() =>
            {
                if (!guest.Network.IsConnected && !guest.Network.IsJoiningRoom) lost = true;
                return guest.Network.IsConnected && !guest.Network.IsJoiningRoom && guest.Network.LocalPlayerId != previous
                    && guest.Network.LocalSlotNumber == 2;
            }, 20d, "The guest did not get back in with P2.");
            Assert.That(lost, Is.False, "A rejoining lobby never looks lost; GameSession would leave it.");
            Assert.That(host.Network.HeldSeatCount, Is.Zero);
            Assert.That(host.Network.ParticipantCount, Is.EqualTo(2));
            Assert.That(host.Network.GetSlotOwner(2), Is.EqualTo(guest.Network.LocalPlayerId));
            Assert.That(guest.Network.IsSlotHeld(2), Is.False);
            Assert.That(Ticket(guest), Is.Not.Null, "A rejoin keeps the room's ticket.");
            Assert.That(typeof(LabNetwork).GetField("rejoinTicketAdmitted", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(guest.Network), Is.True);
        }

        private static string Ticket(Peer peer) => (string)typeof(LabNetwork)
            .GetField("rejoinTicket", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(peer.Network);

        private static IEnumerator Until(Func<bool> condition, double seconds, string failure)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline) Assert.Fail(failure);
                yield return null;
            }
        }

        /// <summary>One device: its own NGO manager, the dual-stack transport and LabNetwork in the game flow.</summary>
        private sealed class Peer : IDisposable
        {
            private readonly GameObject root;
            private readonly Component manager;
            public readonly LabNetwork Network;

            public Peer(string name, LabConfig config, ushort port)
            {
                root = new GameObject(name);
                root.SetActive(false);
                manager = root.AddComponent(typeof(LabNetwork).GetField("manager").FieldType);
                Type transportType = typeof(LabNetwork).Assembly.GetType("C6Lab.LabDualStackTransport", true);
                Component transport = root.AddComponent(transportType);
                transportType.GetProperty("DisconnectTimeoutMS").SetValue(transport, DisconnectTimeoutMs);
                Network = root.AddComponent<LabNetwork>();
                Network.config = config;
                Network.port = port;
                root.SetActive(true);
                Assert.That(Network.SetGameFlowEnabled(true), Is.True);
            }

            /// <summary>Stops (or restarts) every NGO stage of this peer; its MonoBehaviours keep running.</summary>
            public void Freeze(bool frozen)
            {
                Type loop = manager.GetType().Assembly.GetType("Unity.Netcode.NetworkUpdateLoop", true);
                loop.GetMethod(frozen ? "UnregisterAllNetworkUpdates" : "RegisterAllNetworkUpdates",
                    BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { manager });
            }

            public void Dispose()
            {
                if (root != null) Object.Destroy(root);
            }
        }
    }
}
