using System;
using System.Collections;
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
        public void GameRoomsUseAnOptInTokenWithTheCurrentSharedProtocolAndSettings()
        {
            var root = new GameObject("bridge-token-test");
            try
            {
                var network = root.AddComponent<LabNetwork>();
                Assert.That(network.GameFlowEnabled, Is.False);
                Assert.That(Token(network), Is.EqualTo("C6PhysicsLab/22"));
                Assert.That(typeof(LabNetwork).GetField("LabProtocolVersion",
                    BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue(), Is.EqualTo((ushort)5661));
                Assert.That(LabConfig.DeveloperFields.Count, Is.EqualTo(196));
                Assert.That(network.SetGameFlowEnabled(true), Is.True);
                Assert.That(Token(network), Is.EqualTo("C6HapioGame/4"));
                Assert.That(network.SetGameFlowEnabled(false), Is.True);
                Assert.That(Token(network), Is.EqualTo("C6PhysicsLab/22"));
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
                network.BindBattleReferences(board, target, null, floor, null, projectileContainer.transform);
                var actions = (LabLocalActionCoordinator)typeof(LabNetwork).GetField("localActions", Private).GetValue(network);
                actions.SetActive("finished-stage", 1, 20);
                Assert.That(actions.TryRegister(1, new[] { "fox", "yin" }, null, 0d, out _), Is.True);
                Assert.That(BoardListenerCount(board, "FoxFireTouched"), Is.EqualTo(1));
                Assert.That(BoardListenerCount(board, "JigwiBurnRequested"), Is.EqualTo(1));
                typeof(LabNetwork).GetField("latest", Private).SetValue(network, last);
                network.UnbindBattleReferences();
                Assert.That(network.HasBattleReferences, Is.False);
                Assert.That(network.board, Is.Null);
                Assert.That(network.target, Is.Null);
                Assert.That(network.battleFloor, Is.Null);
                Assert.That(network.projectileRoot, Is.Null);
                Assert.That(network.Snapshot, Is.SameAs(last));
                Assert.That(actions.Count, Is.Zero, "Old Scene previews and retries must retire with their board.");
                Assert.That(BoardListenerCount(board, "FoxFireTouched"), Is.Zero,
                    "An unloaded board must not keep sending fox contacts to the persistent session.");
                Assert.That(BoardListenerCount(board, "JigwiBurnRequested"), Is.Zero,
                    "Stage unloading must detach both-input Jigwi release callbacks too.");
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
        public void FrozenGameIgnoresLateReceiptWithoutUnlockingItsDependentAction()
        {
            using (var fixture = new BattleBridgeFixture())
            {
                object combine = fixture.PendingPacket("combine", 1);
                combine.GetType().GetField("provisionalId").SetValue(combine, "pending-combined");
                Assert.That(fixture.Actions.TryRegister(1, new[] { "yin", "yang" }, "pending-combined", 0d, out _), Is.True);
                fixture.PendingPacket("launch", 2);
                Assert.That(fixture.Actions.TryRegister(2, new[] { "pending-combined" }, null, 0d, out _), Is.True);
                int combined = 0;
                fixture.Network.LocalCombined += () => combined++;
                LabSnapshot before = fixture.Network.Snapshot;
                fixture.Network.FreezeGameBattle();

                Invoke(fixture.Network, "ReceiveActionReceipt", new LabActionReceipt
                {
                    sessionId = before.sessionId, round = before.round, sender = fixture.Network.LocalPlayerId,
                    requestId = 1, accepted = true, provisionalId = "pending-combined", outputId = "combined"
                });

                Assert.That(fixture.Actions.TryGetPending(1, out var first), Is.True);
                Assert.That(first.state, Is.EqualTo(LabPendingActionState.Ready));
                Assert.That(fixture.Actions.TryGetPending(2, out var dependent), Is.True);
                Assert.That(dependent.state, Is.EqualTo(LabPendingActionState.WaitingForDependencies));
                Assert.That(combined, Is.Zero);
                Assert.That(fixture.Network.Snapshot, Is.SameAs(before));
                Assert.That(fixture.Network.ActionRequestTransmissions, Is.Zero);
            }
        }

        [Test]
        public void FrozenGameIgnoresHostRetryForAnUnconfirmedSceneAction()
        {
            using (var fixture = new BattleBridgeFixture())
            {
                object retry = fixture.PendingPacket("launch", 1);
                Assert.That(fixture.Actions.TryRegister(1, new[] { "orb" }, null, 0d, out _), Is.True);
                for (int attempt = 0; attempt < 6; attempt++)
                    Assert.That(fixture.Actions.MarkSent(1, attempt * 3d), Is.True);
                Assert.That(fixture.Actions.GetDueRequestIds(100d, true), Is.Empty);
                Assert.That(fixture.Actions.GetRecoveryRequiredRequestIds(), Is.EqualTo(new long[] { 1 }));
                fixture.Network.FreezeGameBattle();

                Assert.That(Invoke(fixture.Network, "ReceiveActionRetry", retry), Is.False);
                Assert.That(fixture.Network.ActionRequestTransmissions, Is.Zero,
                    "An action_retry arriving after Game Over must not restart old gameplay traffic.");
            }
        }

        [Test]
        public void FrozenGameStillReceivesBoardDataWithoutPausingItsTerminalScreen()
        {
            float timeScale = Time.timeScale;
            try
            {
                Time.timeScale = .75f;
                using (var fixture = new BattleBridgeFixture())
                {
                    fixture.Network.FreezeGameBattle();
                    LabSnapshot before = fixture.Network.Snapshot;
                    Invoke(fixture.Network, "ReceiveBoardFrame", new LabBoardFrame
                    {
                        sessionId = before.sessionId, round = before.round, sequence = 1,
                        checkpoint = true, localOwner = fixture.Network.LocalPlayerId,
                        serverTime = 1d, remaining = 179d, phase = LabPhase.Playing, paused = true
                    });
                    Assert.That(fixture.Network.ReceivedBoardFrames, Is.EqualTo(1));
                    Assert.That(fixture.Network.Snapshot.paused, Is.True,
                        "The committed board data remains available for result synchronization.");
                    Assert.That(Time.timeScale, Is.EqualTo(.75f),
                        "A delayed paused board frame must not stop Game Over or scene-loading UI.");
                }
            }
            finally { Time.timeScale = timeScale; }
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

        [Test]
        public void RoomPresenceStaysClosedDuringModelNullGameLoadsAndResetsForANewRoom()
        {
            using (var fixture = new HostGateFixture(true, 2))
            {
                fixture.AddPlayer(0, 2);
                PropertyInfo busy = typeof(LabNetwork).GetProperty("RoomPresenceBusy", Private);
                Assert.That(busy.GetValue(fixture.Network), Is.EqualTo(false));
                fixture.Network.LockGameRoster();
                Assert.That(busy.GetValue(fixture.Network), Is.EqualTo(true),
                    "Loading/results remain closed even while their battle model is absent.");
                fixture.ResetRoom();
                Assert.That(busy.GetValue(fixture.Network), Is.EqualTo(false));
            }
        }

        [Test]
        public void OldSerializedFixedHostFlagCannotReserveP1OrStopTheHostFromClearing()
        {
            using (var fixture = new HostGateFixture(false, 2))
            {
                fixture.Network.fixedHostPosition = true;
                fixture.AddPlayer(0, 1);
                Assert.That(fixture.Network.RequestPlayerSlot(0), Is.True);
                Assert.That(fixture.Network.GetSlotOwner(1), Is.EqualTo(ulong.MaxValue));
                Assert.That(fixture.Network.RequestPlayerSlot(2), Is.True);
                Assert.That(fixture.Network.GetSlotOwner(2), Is.EqualTo(0ul));
                fixture.AddPlayer(23, 0);
                fixture.DeliverSlot(23, 1);
                Assert.That(fixture.Network.GetSlotOwner(1), Is.EqualTo(23ul),
                    "The real Host packet gate must let a non-Host claim P1.");
            }
        }

        [Test]
        public void StageLoadRequiresTheExactSettingsGenerationAndEveryParticipantAck()
        {
            using (var fixture = new HostGateFixture(true, 2))
            {
                fixture.AddPlayer(0, 1);
                fixture.AddPlayer(23, 2);
                typeof(LabNetwork).GetField("configRevision", Private).SetValue(fixture.Network, 7);
                typeof(LabNetwork).GetField("configFingerprint", Private).SetValue(fixture.Network, "current-profile");
                Assert.That(fixture.Network.MatchesGameStageConfig(7, "current-profile"), Is.True);
                Assert.That(fixture.Network.MatchesGameStageConfig(6, "current-profile"), Is.False);
                Assert.That(fixture.Network.MatchesGameStageConfig(7, "previous-profile"), Is.False);
                var acks = (HashSet<ulong>)typeof(LabNetwork).GetField("configAcknowledgements", Private)
                    .GetValue(fixture.Network);
                acks.Remove(23);
                Assert.That(fixture.Network.MatchesGameStageConfig(7, "current-profile"), Is.False,
                    "An otherwise valid scene-ready generation cannot release an unconfirmed effective profile.");
                acks.Add(23);
                Assert.That(fixture.Network.MatchesGameStageConfig(7, "current-profile"), Is.True);
            }
        }

        [TestCase(1, "stage-gumiho", LabStageYokai.Gumiho, true)]
        [TestCase(2, "stage-yaguang", LabStageYokai.Yaguang, true)]
        [TestCase(3, "stage-jigwi", LabStageYokai.Jigwi, true)]
        [TestCase(2, "stage-yaguang", LabStageYokai.Jigwi, false)]
        [TestCase(1, "", LabStageYokai.Gumiho, false)]
        [TestCase(0, "stage-gumiho", LabStageYokai.None, false)]
        [TestCase(3, "stage\njigwi", LabStageYokai.Jigwi, false)]
        public void StageIdentityRejectsMismatchedKindsAndMalformedIds(int index, string id,
            LabStageYokai yokai, bool expected)
        {
            MethodInfo valid = typeof(LabNetwork).GetMethod("ValidGameStage", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(valid.Invoke(null, new object[] { index, id, yokai }), Is.EqualTo(expected));
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

            public void DeliverLateClear(ulong sender) => DeliverSlot(sender, 0);

            public void DeliverSlot(ulong sender, int slot)
            {
                Type type = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket");
                object packet = Activator.CreateInstance(type);
                type.GetField("type").SetValue(packet, "slot_select");
                type.GetField("slotNumber").SetValue(packet, slot);
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

        private sealed class BattleBridgeFixture : IDisposable
        {
            private readonly GameObject root = new GameObject("Game battle bridge lifecycle");
            public readonly LabNetwork Network;
            public readonly LabLocalActionCoordinator Actions;

            public BattleBridgeFixture()
            {
                Network = root.AddComponent<LabNetwork>();
                Assert.That(Network.SetGameFlowEnabled(true), Is.True);
                var boardObject = new GameObject("board");
                boardObject.transform.SetParent(root.transform);
                var targetObject = new GameObject("target");
                targetObject.transform.SetParent(root.transform);
                var projectiles = new GameObject("projectiles");
                projectiles.transform.SetParent(root.transform);
                Network.BindBattleReferences(boardObject.AddComponent<LabOrbBoard>(),
                    targetObject.AddComponent<LabTarget>(), null, root.AddComponent<BoxCollider>(),
                    null, projectiles.transform);
                typeof(LabNetwork).GetField("latest", Private).SetValue(Network, new LabSnapshot
                {
                    sessionId = "battle-stage", round = 1, phase = LabPhase.Playing,
                    players = Array.Empty<LabPlayerState>(), orbs = Array.Empty<LabOrbState>(),
                    drums = Array.Empty<LabOrbState>()
                });
                Invoke(Network, "EnsureActionRound");
                Actions = (LabLocalActionCoordinator)typeof(LabNetwork).GetField("localActions", Private).GetValue(Network);
            }

            public object PendingPacket(string type, long id)
            {
                Type packetType = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket");
                object packet = Activator.CreateInstance(packetType);
                packetType.GetField("type").SetValue(packet, type);
                packetType.GetField("requestId").SetValue(packet, id);
                packetType.GetField("sessionId").SetValue(packet, Network.Snapshot.sessionId);
                packetType.GetField("round").SetValue(packet, Network.Snapshot.round);
                foreach (string field in new[] { "actionRequests", "actionWireRequests" })
                    ((IDictionary)typeof(LabNetwork).GetField(field, Private).GetValue(Network)).Add(id, packet);
                return packet;
            }

            public void Dispose()
            {
                Network.UnbindBattleReferences();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static int BoardListenerCount(LabOrbBoard board, string name) =>
            ((Delegate)typeof(LabOrbBoard).GetField(name, Private).GetValue(board))?.GetInvocationList().Length ?? 0;

        private static object Invoke(LabNetwork network, string method, params object[] arguments) =>
            typeof(LabNetwork).GetMethod(method, Private).Invoke(network, arguments);

        private static string Token(LabNetwork network) => Encoding.ASCII.GetString((byte[])typeof(LabNetwork)
            .GetProperty("ActiveConnectionToken", Private).GetValue(network));
    }
}
