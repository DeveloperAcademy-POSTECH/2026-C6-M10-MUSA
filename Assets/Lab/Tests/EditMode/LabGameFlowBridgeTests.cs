using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabGameFlowBridgeTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [Test]
        public void GameRoomsUseAnOptInTokenWhileLegacyLabProtocolAndSettingsStayUnchanged()
        {
            var root = new GameObject("bridge-token-test");
            try
            {
                var network = root.AddComponent<LabNetwork>();
                Assert.That(network.GameFlowEnabled, Is.False);
                Assert.That(Token(network), Is.EqualTo("C6PhysicsLab/16"));
                Assert.That(typeof(LabNetwork).GetField("LabProtocolVersion",
                    BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue(), Is.EqualTo((ushort)5653));
                Assert.That(network.SetGameFlowEnabled(true), Is.True);
                Assert.That(Token(network), Is.EqualTo("C6HapioGame/1"));
                Assert.That(network.SetGameFlowEnabled(false), Is.True);
                Assert.That(Token(network), Is.EqualTo("C6PhysicsLab/16"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ControlPayloadBoundsUseUtf8BytesAndRejectMalformedUtf16()
        {
            MethodInfo validate = typeof(LabNetwork).GetMethod("ValidGameFlowJson",
                BindingFlags.Static | BindingFlags.NonPublic);
            bool Valid(string json) => (bool)validate.Invoke(null, new object[] { json });
            Assert.That(Valid(null), Is.False);
            Assert.That(Valid("   "), Is.False);
            Assert.That(Valid("{\"type\":\"scene_ready\"}"), Is.True);
            Assert.That(Valid(new string('a', 8192)), Is.True);
            Assert.That(Valid(new string('a', 8193)), Is.False);
            Assert.That(Valid(new string('한', 2730)), Is.True);
            Assert.That(Valid(new string('한', 2731)), Is.False);
            Assert.That(Valid("\ud800"), Is.False);
        }

        [Test]
        public void UnbindingKeepsTheLastResultAndMembersButDropsOnlySceneReferences()
        {
            var root = new GameObject("bridge-lifetime-test");
            try
            {
                var network = root.AddComponent<LabNetwork>();
                Assert.That(network.SetGameFlowEnabled(true), Is.True);
                var boardObject = new GameObject("board");
                boardObject.transform.SetParent(root.transform);
                var board = boardObject.AddComponent<LabOrbBoard>();
                var targetObject = new GameObject("target");
                targetObject.transform.SetParent(root.transform);
                var target = targetObject.AddComponent<LabTarget>();
                var floor = root.AddComponent<BoxCollider>();
                var projectileContainer = new GameObject("projectiles");
                projectileContainer.transform.SetParent(root.transform);
                var last = new LabSnapshot { sessionId = "finished-stage", phase = LabPhase.Victory,
                    players = Array.Empty<LabPlayerState>(), orbs = Array.Empty<LabOrbState>(),
                    drums = Array.Empty<LabOrbState>() };
                var members = (List<ulong>)typeof(LabNetwork).GetField("order", Private).GetValue(network);
                members.Add(20);
                members.Add(40);
                network.BindBattleReferences(board, target, null, floor, null, projectileContainer.transform);
                typeof(LabNetwork).GetField("latest", Private).SetValue(network, last);
                network.UnbindBattleReferences();
                Assert.That(network.HasBattleReferences, Is.False);
                Assert.That(network.board, Is.Null);
                Assert.That(network.target, Is.Null);
                Assert.That(network.battleFloor, Is.Null);
                Assert.That(network.projectileRoot, Is.Null);
                Assert.That(network.Snapshot, Is.SameAs(last));
                Assert.That(network.ConnectedPlayerIds, Is.EqualTo(new ulong[] { 20, 40 }));
                ulong[] copy = network.ConnectedPlayerIds;
                copy[0] = 999;
                Assert.That(network.ConnectedPlayerIds[0], Is.EqualTo(20));
                Assert.That(projectileContainer != null, Is.True,
                    "An explicitly supplied Scene container belongs to its Scene owner.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void LoadingTheGameLocksLatePositionChangesWithoutBlockingTheBattleStartPredicate()
        {
            using (var fixture = new HostGateFixture(true, 2))
            {
                fixture.AddPlayer(0, 2);
                fixture.AddPlayer(23, 1);
                Assert.That(fixture.Network.CanSelectPlayerSlot, Is.True);
                Assert.That(fixture.Network.CanStartBattle, Is.True);

                // No Scene or socket is simulated here: exercise the real Host gate during
                // its model-null load barrier with a request sent just before START.
                Assert.That(fixture.Network.PrepareGameStage(), Is.True);
                fixture.DeliverLateClear(23);

                Assert.That(fixture.Network.GetSlotOwner(1), Is.EqualTo(23ul),
                    "A delayed CLEAR must not remove a participant after the run starts loading.");
                Assert.That(fixture.Network.GetSlotOwner(2), Is.EqualTo(0ul));
                Assert.That(fixture.Network.CanSelectPlayerSlot, Is.False);
                Assert.That(fixture.Network.CanStartBattle, Is.True,
                    "The load barrier must still start the preserved complete roster after settings ACKs.");
            }
        }

        [Test]
        public void GameLoadRejectsNewJoinsEvenWithRoomSpaceAndLeavingResetsThatGate()
        {
            using (var fixture = new HostGateFixture(true, 3))
            {
                fixture.AddPlayer(0, 1);
                Assert.That(fixture.Approves(23), Is.True, "The ordinary game lobby admits a matching peer.");
                Assert.That(fixture.Network.PrepareGameStage(), Is.True);
                Assert.That(fixture.Approves(24), Is.False,
                    "Loading is part of the active run even though its battle model is temporarily null.");

                fixture.ResetRoom();
                fixture.AddPlayer(0, 1);
                Assert.That(fixture.Network.CanSelectPlayerSlot, Is.True,
                    "A new room starts with normal position selection.");
                Assert.That(fixture.Approves(25), Is.True,
                    "ResetLocal must retire the previous run's roster lock.");
            }
        }

        [Test]
        public void GameRosterLockDoesNotCloseLegacyLabPositionOrConnectionGates()
        {
            using (var fixture = new HostGateFixture(false, 2))
            {
                fixture.AddPlayer(0, 2);
                fixture.Network.LockGameRoster();
                Assert.That(fixture.Network.CanSelectPlayerSlot, Is.True);
                Assert.That(fixture.Approves(23), Is.True,
                    "The Lab's ordinary lobby keeps its original approval path.");
                Assert.That(fixture.Network.RequestPlayerSlot(1), Is.True);
                Assert.That(fixture.Network.GetSlotOwner(1), Is.EqualTo(0ul));
            }
        }

        /// <summary>
        /// Inactive NGO role fixture for pure gate checks. It does not start a transport,
        /// create remote participants, or claim connection/latency verification.
        /// </summary>
        private sealed class HostGateFixture : IDisposable
        {
            private readonly GameObject root;
            private readonly Component manager;
            private readonly Type managerType;
            private readonly int capacity;
            public readonly LabNetwork Network;

            public HostGateFixture(bool gameMode, int capacity)
            {
                this.capacity = capacity;
                root = new GameObject("Inactive Host game roster gate");
                root.SetActive(false);
                managerType = typeof(LabNetwork).GetField("manager").FieldType;
                manager = root.AddComponent(managerType);
                Network = root.AddComponent<LabNetwork>();
                typeof(LabNetwork).GetField("manager").SetValue(Network, manager);
                Assert.That(Network.SetGameFlowEnabled(gameMode), Is.True);
                object client = managerType.GetProperty("LocalClient").GetValue(manager);
                client.GetType().GetProperty("IsServer", Private).SetValue(client, true);
                client.GetType().GetProperty("IsClient", Private).SetValue(client, true);
                managerType.GetProperty("IsListening").SetValue(manager, true);
                ResetRoom();
            }

            public void AddPlayer(ulong id, int slot)
            {
                var order = (List<ulong>)typeof(LabNetwork).GetField("order", Private).GetValue(Network);
                order.Add(id);
                var slots = (LabPlayerSlots)typeof(LabNetwork).GetField("playerSlots", Private).GetValue(Network);
                Assert.That(slots.TrySelect(id, slot, out _), Is.True);
                var acks = (HashSet<ulong>)typeof(LabNetwork).GetField("configAcknowledgements", Private).GetValue(Network);
                acks.Add(id);
            }

            public void DeliverLateClear(ulong sender)
            {
                Type type = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket");
                object packet = Activator.CreateInstance(type);
                type.GetField("type").SetValue(packet, "slot_select");
                type.GetField("slotNumber").SetValue(packet, 0);
                type.GetField("requestId").SetValue(packet, 100L);
                typeof(LabNetwork).GetMethod("HandleSlotSelectionRequest", Private)
                    .Invoke(Network, new[] { (object)sender, packet });
            }

            public bool Approves(ulong id)
            {
                MethodInfo approve = typeof(LabNetwork).GetMethod("Approve", Private);
                Type requestType = approve.GetParameters()[0].ParameterType;
                Type responseType = approve.GetParameters()[1].ParameterType;
                object request = Activator.CreateInstance(requestType);
                requestType.GetField("Payload").SetValue(request,
                    Encoding.ASCII.GetBytes(Token(Network)));
                requestType.GetField("ClientNetworkId").SetValue(request, id);
                object response = Activator.CreateInstance(responseType);
                approve.Invoke(Network, new[] { request, response });
                return (bool)responseType.GetField("Approved").GetValue(response);
            }

            public void ResetRoom()
            {
                typeof(LabNetwork).GetMethod("ResetLocal", Private).Invoke(Network, null);
                typeof(LabNetwork).GetField("targetParticipantCount", Private).SetValue(Network, capacity);
                typeof(LabNetwork).GetField("playerSlots", Private).SetValue(Network, new LabPlayerSlots(capacity));
            }

            public void Dispose()
            {
                // The manager was never initialized. Do not ask its teardown to stop a fake listener.
                managerType.GetProperty("IsListening").SetValue(manager, false);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static string Token(LabNetwork network) => Encoding.ASCII.GetString((byte[])typeof(LabNetwork)
            .GetProperty("ActiveConnectionToken", Private).GetValue(network));
    }
}
