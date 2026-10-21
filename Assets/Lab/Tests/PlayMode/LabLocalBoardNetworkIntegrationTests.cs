using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace C6Lab.Tests
{
    /// <summary>
    /// Exercises the real Network receipt/board handlers and a physical board with synthetic packets.
    /// No socket, remote device, latency target or hotspot success is claimed by these fixtures.
    /// </summary>
    public sealed class LabLocalBoardNetworkIntegrationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Session = "local-board-network-fixture";
        private GameObject root;
        private LabConfig config;
        private LabOrbBoard board;
        private LabNetwork network;
        private LabSnapshot initial;
        private ulong owner;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Local board Network fixture");
            config = ScriptableObject.CreateInstance<LabConfig>();
            var cameraObject = new GameObject("Input camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            var boardObject = new GameObject("Board");
            boardObject.transform.SetParent(root.transform, false);
            board = boardObject.AddComponent<LabOrbBoard>();
            board.Configure(config, camera, new Rect(-4f, -2f, 8f, 4f));
            network = root.AddComponent<LabNetwork>();
            network.enabled = false;
            network.config = config;
            network.board = board;
            // With no transport this is a client-only fixture. Keep its actual local identity.
            owner = network.LocalPlayerId;
            initial = new LabSnapshot
            {
                sessionId = Session, round = 1u, phase = LabPhase.Playing, serverTime = 1d,
                remaining = 179d, hp = 800, paintCoverage = .25f, paintRevision = 1u,
                paintColumns = 64, paintRows = 32, paintCoveredCount = 512, paintCellCount = 2048,
                players = new[]
                {
                    new LabPlayerState { id = owner, seat = 0, personalHp = 300, stamina = 80f },
                    new LabPlayerState { id = 2ul, seat = 1, personalHp = 300, stamina = 100f }
                },
                orbs = new[] { Raw("yin", LabOrbKind.Yin, .4f), Raw("yang", LabOrbKind.Yang, .6f) }
            };
            Set("sessionId", Session);
            Set("latest", initial);
            Invoke("EnsureActionRound");
            Invoke("SyncBoard");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (network != null) network.Stop();
            if (root != null) UnityEngine.Object.Destroy(root);
            if (config != null) UnityEngine.Object.Destroy(config);
            Time.timeScale = 1f;
            yield return null;
        }

        [Test]
        public void CombinationReceiptUpdatesTheBoardWhileANewerPaintSnapshotIsStillWaiting()
        {
            var waiting = new LabSnapshot { sessionId = Session, round = 1u, phase = LabPhase.Victory,
                paintRevision = 5u, paintCoverage = 1f };
            Set("pendingPaintSnapshot", waiting);
            Set("awaitingPaintSession", Session);
            Set("awaitingPaintRound", 1u);
            int combinedFeedback = 0;
            network.LocalCombined += () => combinedFeedback++;
            object combine = Submit("combine", 1, "yin", "yang");
            string temporary = PacketField<string>(combine, "provisionalId");
            Assert.That(board.IsProvisional(temporary), Is.True);

            LabActionReceipt receipt = CombinedReceipt(1, temporary, 1);
            Invoke("ReceiveActionReceipt", receipt);
            Assert.That(board.Count, Is.EqualTo(1));
            Assert.That(network.Snapshot.orbs.Select(orb => orb.id), Is.EqualTo(new[] { "combined" }));
            Assert.That(board.IsProvisional(temporary), Is.False);
            Assert.That(Get<LabSnapshot>("pendingPaintSnapshot"), Is.SameAs(waiting));
            Assert.That(network.Snapshot.paintRevision, Is.EqualTo(1u));
            Assert.That(network.Snapshot.paintCoverage, Is.EqualTo(.25f));
            Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
            Assert.That(combinedFeedback, Is.EqualTo(1));
            Invoke("ReceiveActionReceipt", receipt.Copy());
            Assert.That(combinedFeedback, Is.EqualTo(1), "A repeated receipt must not replay feedback.");
            Assert.That(board.Count, Is.EqualTo(1));
        }

        [Test]
        public void RejectedCombinationRollsBackItsUnsentDependentLaunchAndRestoresMaterials()
        {
            object combine = Submit("combine", 1, "yin", "yang");
            string temporary = PacketField<string>(combine, "provisionalId");
            Submit("launch", 2, temporary);
            LabLocalActionCoordinator coordinator = Get<LabLocalActionCoordinator>("localActions");
            Assert.That(coordinator.TryGetPending(2, out var before), Is.True);
            Assert.That(before.state, Is.EqualTo(LabPendingActionState.WaitingForDependencies));
            Assert.That(board.Count, Is.Zero);

            Invoke("ReceiveActionReceipt", new LabActionReceipt
            {
                sessionId = Session, round = 1u, sender = owner, requestId = 1, sequence = 1,
                accepted = false, reason = "NOT_OWNED_IDLE", serverTime = 2d,
                states = initial.orbs, players = initial.players, provisionalId = temporary
            });
            Assert.That(coordinator.TryGetPending(2, out var after), Is.True);
            Assert.That(after.state, Is.EqualTo(LabPendingActionState.Rejected));
            Assert.That(board.Count, Is.EqualTo(2));
            Assert.That(board.IsSnapshotSuppressed("yin") || board.IsSnapshotSuppressed("yang"), Is.False);
            Assert.That(board.IsProvisional(temporary), Is.False);
            Assert.That(network.Snapshot.orbs.Select(orb => orb.id), Is.EquivalentTo(new[] { "yin", "yang" }));
        }

        [Test]
        public void ABoardCheckpointBeforeTheReceiptCannotDuplicateAProvisionalBodyOrItsDependentLaunch()
        {
            object combine = Submit("combine", 1, "yin", "yang");
            string temporary = PacketField<string>(combine, "provisionalId");
            Submit("launch", 2, temporary);
            LabOrbState combined = Combined();
            Invoke("ReceiveBoardFrame", new LabBoardFrame
            {
                sessionId = Session, round = 1u, sequence = 2, localOwner = owner, checkpoint = true,
                phase = LabPhase.Playing, serverTime = 2d, remaining = 178d,
                states = new[] { combined }, players = initial.players
            });
            Invoke("ReceiveActionReceipt", CombinedReceipt(1, temporary, 1));
            Assert.That(board.IsSnapshotSuppressed("combined"), Is.True,
                "The subsequent local launch remains hidden after the combination is promoted.");
            Assert.That(board.IsProvisional(temporary), Is.False);

            LabOrbState flying = Combined();
            flying.inFlight = true;
            Invoke("ReceiveActionReceipt", new LabActionReceipt
            {
                sessionId = Session, round = 1u, sender = owner, requestId = 2, sequence = 3,
                accepted = true, reason = "LAUNCHED", serverTime = 2.1d,
                states = new[] { flying }, removedIds = new[] { "combined" }, players = initial.players
            });
            Assert.That(board.Count, Is.Zero);
            Assert.That(board.TryGetMotion("combined", out _, out _), Is.False);
            Assert.That(board.TryGetMotion("yin", out _, out _), Is.False);
            Assert.That(board.TryGetMotion("yang", out _, out _), Is.False);
            Assert.That(network.Snapshot.orbs.Any(orb => orb.owner == owner && !orb.inFlight), Is.False);
        }

        [Test]
        public void ANewRoundDiscardsOldProvisionalBodiesAndRejectsTheirLateReceipt()
        {
            object combine = Submit("combine", 1, "yin", "yang");
            string temporary = PacketField<string>(combine, "provisionalId");
            Set("latest", new LabSnapshot { sessionId = Session, round = 2u, phase = LabPhase.Playing,
                remaining = 180d, hp = 800, players = initial.players });
            Invoke("EnsureActionRound");
            Invoke("SyncBoard");
            Invoke("ReceiveActionReceipt", CombinedReceipt(1, temporary, 1));
            Assert.That(network.Snapshot.round, Is.EqualTo(2u));
            Assert.That(board.Count, Is.Zero);
            Assert.That(board.IsProvisional(temporary), Is.False);
            Assert.That(board.TryGetMotion("yin", out _, out _), Is.False);
            Assert.That(board.TryGetMotion("yang", out _, out _), Is.False);
        }

        [Test]
        public void AHistoricalCombinationReceiptCannotResurrectAnOutputMissingFromTheNewerLedger()
        {
            object combine = Submit("combine", 1, "yin", "yang");
            string temporary = PacketField<string>(combine, "provisionalId");
            Invoke("ReceiveBoardFrame", EmptyCheckpoint(2));
            Invoke("ReceiveActionReceipt", CombinedReceipt(1, temporary, 1));
            Assert.That(board.Count, Is.Zero);
            Assert.That(board.OrbIds, Is.Empty);
            Assert.That(network.Snapshot.orbs, Is.Empty);
        }

        [Test]
        public void AHistoricalLaunchRejectionCannotRestoreAnOrbAlreadyGoneInTheNewerLedger()
        {
            LabOrbState combined = Combined();
            initial.orbs = new[] { combined };
            Invoke("SyncBoard");
            Submit("launch", 1, "combined");
            Assert.That(board.IsSnapshotSuppressed("combined"), Is.True);
            Invoke("ReceiveBoardFrame", EmptyCheckpoint(2));
            Invoke("ReceiveActionReceipt", new LabActionReceipt
            {
                sessionId = Session, round = 1u, sender = owner, requestId = 1, sequence = 1,
                accepted = false, reason = "NOT_OWNED_IDLE", serverTime = 2d,
                states = new[] { combined }, players = initial.players
            });
            Assert.That(board.Count, Is.Zero);
            Assert.That(board.OrbIds, Is.Empty);
            Assert.That(network.Snapshot.orbs, Is.Empty);
        }

        [Test]
        public void CapturingAFastBoardFrameRetainsTheCurrentNonzeroHangingTargetPose()
        {
            LabTarget target = LabTarget.CreateCylinder(root.transform, new Vector3(0f, 0f, 5f), .8f, 2f);
            LabHangingTarget hanging = target.gameObject.AddComponent<LabHangingTarget>();
            hanging.ConfigureSpin(80f, 1.2f, 720f);
            hanging.ConfigureRopeMotion(.2f, 3f, .18f, 45f, 3f, .5f);
            hanging.ReconcileYaw(35f, 90f, true);
            hanging.ReconcileRope(.7f, .15f, .4f, true);
            network.target = target;
            var actionSnapshot = new LabSnapshot();

            Invoke("CaptureTargetPose", actionSnapshot);

            Assert.That(actionSnapshot.targetYawDegrees, Is.EqualTo(35f).Within(.00001f));
            Assert.That(actionSnapshot.targetYawSpeedDegreesPerSecond, Is.EqualTo(90f).Within(.00001f));
            Assert.That(actionSnapshot.targetRopeCycleElapsed, Is.EqualTo(.7f).Within(.00001f));
            Assert.That(actionSnapshot.targetRopeHitOffset, Is.EqualTo(.15f).Within(.00001f));
            Assert.That(actionSnapshot.targetRopeHitVelocity, Is.EqualTo(.4f).Within(.00001f));
        }

        [Test]
        public void RecoveryResendsOnlyTheFrozenRequestAndIgnoresStaleOrCompletedRetryNotices()
        {
            object original = Submit("combine", 1, "yin", "yang");
            string temporary = PacketField<string>(original, "provisionalId");
            object retry = NewPacket("action_retry", 1);
            retry.GetType().GetField("sessionId").SetValue(retry, Session);
            retry.GetType().GetField("round").SetValue(retry, 1u);
            LabLocalActionCoordinator coordinator = Get<LabLocalActionCoordinator>("localActions");
            Assert.That((bool)Invoke("ReceiveActionRetry", retry), Is.False,
                "A request that has not exhausted retries is not a recovery request.");

            object wire = JsonUtility.FromJson(JsonUtility.ToJson(original), original.GetType());
            wire.GetType().GetField("ownershipVersion").SetValue(wire, 7ul);
            wire.GetType().GetField("x").SetValue(wire, .8f);
            Get<IDictionary>("actionWireRequests").Add(1L, wire);
            string frozenPayload = JsonUtility.ToJson(wire);
            for (int attempt = 0; attempt < 6; attempt++)
                Assert.That(coordinator.MarkSent(1, attempt * 3d), Is.True);
            Assert.That(coordinator.GetDueRequestIds(100d, true), Is.Empty);
            Assert.That(coordinator.TryGetPending(1, out var waiting), Is.True);
            Assert.That(waiting.state, Is.EqualTo(LabPendingActionState.RecoveryRequired));

            initial.orbs[0].ownershipVersion = 9ul;
            initial.orbs[0].x = .1f;
            Assert.That((bool)Invoke("ReceiveActionRetry", (object)null), Is.False);
            retry.GetType().GetField("round").SetValue(retry, 2u);
            Assert.That((bool)Invoke("ReceiveActionRetry", retry), Is.False);
            retry.GetType().GetField("round").SetValue(retry, 1u);
            retry.GetType().GetField("sessionId").SetValue(retry, "old-session");
            Assert.That((bool)Invoke("ReceiveActionRetry", retry), Is.False);
            retry.GetType().GetField("sessionId").SetValue(retry, Session);
            Assert.That(network.ActionRequestTransmissions, Is.Zero);

            Assert.That((bool)Invoke("ReceiveActionRetry", retry), Is.True);
            Assert.That(network.ActionRequestTransmissions, Is.EqualTo(1));
            Assert.That(Get<IDictionary>("actionWireRequests")[1L], Is.SameAs(wire));
            Assert.That(JsonUtility.ToJson(wire), Is.EqualTo(frozenPayload),
                "Recovery must not recapture the newer position or ownership version.");
            Invoke("ReceiveActionReceipt", CombinedReceipt(1, temporary, 1));
            Assert.That((bool)Invoke("ReceiveActionRetry", retry), Is.False);
            Assert.That(network.ActionRequestTransmissions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ABoardSequenceGapBlocksNewCommandsUntilCheckpointWithoutStoppingPassivePhysics()
        {
            LabOrbView moving = board.GetComponentsInChildren<LabOrbView>(true).Single(view => view.OrbId == "yang");
            moving.Body.linearVelocity = new Vector3(1.5f, 0f, 0f);
            float previousX = moving.Body.position.x;
            var gap = EmptyCheckpoint(3);
            gap.checkpoint = false;
            Invoke("ReceiveBoardFrame", gap);
            Assert.That(Get<LabBoardReplica>("boardReplica").NeedsCheckpoint, Is.True);
            Assert.That(board.InteractionEnabled, Is.False);
            LabLocalActionCoordinator coordinator = Get<LabLocalActionCoordinator>("localActions");
            Submit("combine", 1, "yin", "yang");
            Assert.That(coordinator.Count, Is.Zero, "No new critical command may register against a missing ledger baseline.");
            Assert.That(board.Count, Is.EqualTo(2));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(moving.Body.position.x, Is.GreaterThan(previousX), "Only input is gated, not free local physics.");

            LabBoardFrame checkpoint = EmptyCheckpoint(4);
            checkpoint.states = initial.orbs;
            Invoke("ReceiveBoardFrame", checkpoint);
            Assert.That(Get<LabBoardReplica>("boardReplica").NeedsCheckpoint, Is.False);
            Assert.That(board.InteractionEnabled, Is.True);
            Submit("combine", 2, "yin", "yang");
            Assert.That(coordinator.Count, Is.EqualTo(1));
            Assert.That(board.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AnActionTriggeredExpiryVanishesBeforeItsReceiptRemovesTheSourceBody()
        {
            float[] values = config.CaptureDeveloperValues();
            values[5] = 1f;
            values[10] = .1f;
            values[39] = values[40] = 50f;
            for (int i = 41; i <= 44; i++) values[i] = 0f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            double began = Time.realtimeSinceStartupAsDouble;
            var model = new LabModel();
            model.BeginSolo(Session, owner, began, config);
            Assert.That(model.TryGenerate(owner, "expiry-source", began, out var expiring, out error), Is.True, error);
            initial = model.Snapshot(began);
            Set("model", model);
            Set("latest", initial);
            Invoke("SyncBoard");
            Assert.That(board.TryGetMotion(expiring.id, out _, out _), Is.True);
            var vanished = new List<string>();
            var notices = new List<string>();
            board.OrbVanished += id => vanished.Add(id);
            network.OrbExpired += id => notices.Add(id);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(board.TryGetMotion(expiring.id, out _, out _), Is.True,
                "A local life countdown must not authoritatively remove an orb before the Host advances.");
            LabOrbState drum = initial.drums.Single();
            object request = Submit("generate", 500, null, drum.id);
            request.GetType().GetField("ownershipVersion").SetValue(request, drum.ownershipVersion);
            request.GetType().GetField("configRevision").SetValue(request, Get<int>("configRevision"));
            request.GetType().GetField("configFingerprint").SetValue(request, Get<string>("configFingerprint"));

            // Execute the actual Host rule handler without sockets. TryGenerate advances the real
            // model clock, so the earlier orb expires while this command is being processed.
            var receipt = (LabActionReceipt)Invoke("ExecuteBoardAction", owner, request);

            Assert.That(receipt.accepted, Is.True, receipt.reason);
            Assert.That(receipt.removedIds, Does.Contain(expiring.id));
            Assert.That(vanished, Is.EqualTo(new[] { expiring.id }),
                "The expiry must be announced before its action receipt is delivered, while Vanish can still find the body.");
            Assert.That(notices, Is.EqualTo(new[] { expiring.id }));
            Assert.That(board.GetComponentsInChildren<LabOrbVanishSmoke>(true).Length, Is.EqualTo(1));
            Assert.That(board.TryGetMotion(expiring.id, out _, out _), Is.False);
            receipt.sessionId = Session;
            receipt.round = 1u;
            receipt.sender = owner;
            receipt.requestId = 500;
            Invoke("ReceiveActionReceipt", receipt);
            Assert.That(vanished.Count, Is.EqualTo(1), "The later ledger removal must not create another expiry effect.");
            Assert.That(network.Snapshot.orbs.Any(orb => orb.id == expiring.id), Is.False);
            Assert.That(network.Snapshot.orbs.Any(orb => orb.id == receipt.outputId), Is.True);
        }

        [Test]
        public void AnOldRoundExpiryNoticeCannotVanishANewRoundOrbAndCurrentNoticePrecedesCheckpointRemoval()
        {
            initial.round = 2u;
            Set("latest", initial);
            Invoke("EnsureActionRound");
            Invoke("SyncBoard");
            var vanished = new List<string>();
            var notices = new List<string>();
            board.OrbVanished += id => vanished.Add(id);
            network.OrbExpired += id => notices.Add(id);
            ReceiveControl("orbExpired", "yin", Session, 1u);
            ReceiveControl("orbExpired", "yin", "previous-room", 2u);
            Assert.That(vanished, Is.Empty);
            Assert.That(notices, Is.Empty);
            Assert.That(board.TryGetMotion("yin", out _, out _), Is.True,
                "An old round/session cannot expire a current orb even if its ID is reused.");

            ReceiveControl("orbExpired", "yin", Session, 2u);

            Assert.That(vanished, Is.EqualTo(new[] { "yin" }));
            Assert.That(notices, Is.EqualTo(new[] { "yin" }));
            Assert.That(board.GetComponentsInChildren<LabOrbVanishSmoke>(true).Length, Is.EqualTo(1));
            Assert.That(board.TryGetMotion("yin", out _, out _), Is.False);
            LabBoardFrame checkpoint = EmptyCheckpoint(1);
            checkpoint.round = 2u;
            checkpoint.states = initial.orbs.Where(orb => orb.id == "yang").ToArray();
            Invoke("ReceiveBoardFrame", checkpoint);
            Assert.That(network.Snapshot.orbs.Select(orb => orb.id), Is.EqualTo(new[] { "yang" }));
            Assert.That(vanished, Is.EqualTo(new[] { "yin" }), "The owner-board removal is not a second expiry.");
            Assert.That(board.GetComponentsInChildren<LabOrbVanishSmoke>(true).Length, Is.EqualTo(1));
        }

        [TestCase("combine")]
        [TestCase("launch")]
        [TestCase("transfer")]
        public void AConsumedOrTransferredOrbDoesNotPlayAnExpirySmokeOrExpiryNotice(string action)
        {
            int vanished = 0, notices = 0;
            board.OrbVanished += _ => vanished++;
            network.OrbExpired += _ => notices++;
            if (action == "combine")
            {
                object request = Submit("combine", 1, "yin", "yang");
                Invoke("ReceiveActionReceipt", CombinedReceipt(1, PacketField<string>(request, "provisionalId"), 1));
                Assert.That(board.TryGetMotion("yin", out _, out _) || board.TryGetMotion("yang", out _, out _), Is.False);
                Assert.That(board.TryGetMotion("combined", out _, out _), Is.True);
            }
            else
            {
                initial.orbs = new[] { Combined() };
                Invoke("SyncBoard");
                Submit(action, 1, "combined");
                LabOrbState result = Combined();
                if (action == "launch") result.inFlight = true;
                else { result.owner = 2ul; result.ownershipVersion++; }
                Invoke("ReceiveActionReceipt", new LabActionReceipt
                {
                    sessionId = Session, round = 1u, sender = owner, requestId = 1, sequence = 1,
                    accepted = true, reason = action == "launch" ? "LAUNCHED" : "TRANSFERRED", serverTime = 2d,
                    states = new[] { result }, removedIds = new[] { "combined" }, players = initial.players
                });
                Assert.That(board.TryGetMotion("combined", out _, out _), Is.False);
            }
            Assert.That(vanished, Is.Zero, "Only a Host lifetime-expiry notice may invoke Vanish.");
            Assert.That(notices, Is.Zero);
            Assert.That(board.GetComponentsInChildren<LabOrbVanishSmoke>(true), Is.Empty);
        }

        private void ReceiveControl(string type, string orbId, string session, uint round)
        {
            object packet = NewPacket(type, 0, orbId);
            packet.GetType().GetField("sessionId").SetValue(packet, session);
            packet.GetType().GetField("round").SetValue(packet, round);
            byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
            var wire = new byte[payload.Length + 8];
            Buffer.BlockCopy(BitConverter.GetBytes(3), 0, wire, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(payload.Length), 0, wire, 4, 4);
            Buffer.BlockCopy(payload, 0, wire, 8, payload.Length);
            // The fixture intentionally references only the game assembly. Reflection constructs
            // the real NGO reader and invokes its actual authenticated receive path without a socket.
            MethodInfo receive = typeof(LabNetwork).GetMethod("Receive", Private);
            Type readerType = receive.GetParameters()[1].ParameterType;
            ConstructorInfo constructor = readerType.GetConstructors().Single(candidate =>
                candidate.GetParameters().Length == 4 && candidate.GetParameters()[0].ParameterType == typeof(byte[]));
            Type allocatorType = constructor.GetParameters()[1].ParameterType;
            object reader = constructor.Invoke(new[] { (object)wire, Enum.Parse(allocatorType, "Temp"), -1, 0 });
            try { receive.Invoke(network, new[] { (object)0ul, reader }); }
            finally { ((IDisposable)reader).Dispose(); }
        }

        private LabBoardFrame EmptyCheckpoint(long sequence) => new LabBoardFrame
        {
            sessionId = Session, round = 1u, sequence = sequence, localOwner = owner,
            checkpoint = true, phase = LabPhase.Playing, serverTime = 3d, remaining = 177d,
            players = initial.players
        };

        private LabOrbState Raw(string id, LabOrbKind kind, float x) => new LabOrbState
        { id = id, owner = owner, kind = kind, x = x, y = .5f, ownershipVersion = 1ul };

        private LabOrbState Combined() => new LabOrbState
        { id = "combined", owner = owner, kind = LabOrbKind.Combined, element = LabOrbElement.Fire,
            x = .5f, y = .5f, ownershipVersion = 1ul };

        private LabActionReceipt CombinedReceipt(long request, string temporary, long sequence) => new LabActionReceipt
        {
            sessionId = Session, round = 1u, sender = owner, requestId = request, sequence = sequence,
            accepted = true, reason = "COMBINED", serverTime = 2d,
            provisionalId = temporary, outputId = "combined", states = new[] { Combined() },
            removedIds = new[] { "yin", "yang" }, players = initial.players
        };

        private object Submit(string type, long request, string first, string second = null)
        {
            object packet = NewPacket(type, request, first, second);
            Invoke("SubmitBoardAction", packet);
            return packet;
        }

        private static object NewPacket(string type, long request, string first = null, string second = null)
        {
            Type packetType = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket", true);
            object packet = Activator.CreateInstance(packetType, true);
            packetType.GetField("type").SetValue(packet, type);
            packetType.GetField("requestId").SetValue(packet, request);
            packetType.GetField("firstId").SetValue(packet, first);
            packetType.GetField("secondId").SetValue(packet, second);
            packetType.GetField("x").SetValue(packet, .5f);
            packetType.GetField("y").SetValue(packet, .5f);
            return packet;
        }

        private static T PacketField<T>(object packet, string name) => (T)packet.GetType().GetField(name).GetValue(packet);
        private void Set(string field, object value) => typeof(LabNetwork).GetField(field, Private).SetValue(network, value);
        private T Get<T>(string field) => (T)typeof(LabNetwork).GetField(field, Private).GetValue(network);
        private object Invoke(string method, params object[] arguments) => typeof(LabNetwork)
            .GetMethod(method, Private).Invoke(network, arguments);
    }
}
