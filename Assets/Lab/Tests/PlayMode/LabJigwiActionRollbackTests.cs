using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    public sealed class LabJigwiActionRollbackTests
    {
        private GameObject root;
        private LabConfig config;
        private LabNetwork network;
        private LabOrbBoard board;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void Prepare()
        {
            root = new GameObject("Jigwi local rejection fixture");
            config = ScriptableObject.CreateInstance<LabConfig>();
            var cameraObject = new GameObject("Input camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = Vector3.back * 10f;
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
            var boardObject = new GameObject("Board"); boardObject.transform.SetParent(root.transform, false);
            board = boardObject.AddComponent<LabOrbBoard>();
            board.Configure(config, camera, new Rect(-4f, -2f, 8f, 4f));
            var networkObject = new GameObject("Network"); networkObject.transform.SetParent(root.transform, false);
            network = networkObject.AddComponent<LabNetwork>(); network.enabled = false;
            network.board = board; network.config = config;
            typeof(LabNetwork).GetField("latest", Private).SetValue(network, new LabSnapshot
            { sessionId = "jigwi", round = 1, phase = LabPhase.Playing, serverTime = 1d });
            typeof(LabNetwork).GetMethod("EnsureActionRound", Private).Invoke(network, null);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) Object.Destroy(root);
            if (config != null) Object.Destroy(config);
            yield return null;
        }


        [TestCase(true)]
        [TestCase(false)]
        public void SerializedPassiveContactUsesNormalImmediatePreviewWhileManualAttemptBurns(bool passive)
        {
            board.UpsertOrb("yin", LabOrbKind.Yin, new Vector2(.35f, .5f));
            board.UpsertOrb("yang", LabOrbKind.Yang, new Vector2(.65f, .5f));
            network.Snapshot.jigwi = new[] { new LabJigwiState { playerId = network.LocalPlayerId, overheatUntil = 10d } };
            board.SetJigwiOverheat(true);
            object packet = CombinePacket("yin", "yang", passive);
            Type packetType = packet.GetType();
            packet = JsonUtility.FromJson(JsonUtility.ToJson(packet), packetType);
            Assert.That((bool)packetType.GetField("passiveCombine").GetValue(packet), Is.EqualTo(passive));
            typeof(LabNetwork).GetMethod("SubmitBoardAction", Private).Invoke(network, new[] { packet });
            string temporary = (string)packetType.GetField("provisionalId").GetValue(packet);
            Assert.That(board.IsProvisional(temporary), Is.EqualTo(passive));
            Assert.That(board.Count, Is.EqualTo(passive ? 1 : 0));
            Assert.That(board.GetComponentsInChildren<LabJigwiBurnEffect>().Length, Is.EqualTo(passive ? 0 : 2),
                "Passive local contact must not flash the manual-action punishment while awaiting the Host.");
        }

        [TestCase(true, true)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void HostUsesContactSettingAndPreservesPassiveCombinationDuringOverheat(bool contactEnabled, bool passive)
        {
            float[] values = config.CaptureDeveloperValues();
            values[3] = values[4] = 1000f; values[5] = 1f; values[6] = 0f; values[9] = 100f;
            values[39] = values[40] = 50f;
            for (int i = 41; i <= 44; i++) values[i] = 0f;
            values[51] = contactEnabled ? 1f : 0f;
            values[205] = 0f; // no common gimmicks (#311)
            values[178] = 1f; values[179] = 1f; values[180] = 4f;
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            // Use an explicit positive Host-clock epoch. A filtered run can start before
            // realtime reaches two seconds; subtracting two produced an invalid negative Begin.
            const double began = 100d;
            var clock = (LabBattleClock)typeof(LabNetwork).GetField("battleClock", Private).GetValue(network);
            typeof(LabBattleClock).GetField("pausedDuration", Private).SetValue(clock,
                Time.realtimeSinceStartupAsDouble - (began + 2d));
            var model = new LabModel();
            model.BeginSolo("jigwi", network.LocalPlayerId, began, config);
            LabOrbState yin = null, yang = null;
            for (int i = 0; i < 32 && (yin == null || yang == null); i++)
            {
                Assert.That(model.TryGenerate(network.LocalPlayerId, "raw-" + i, began, out var orb, out error), Is.True, error);
                if (orb.kind == LabOrbKind.Yin) yin = orb;
                if (orb.kind == LabOrbKind.Yang) yang = orb;
            }
            Assert.That(yin, Is.Not.Null); Assert.That(yang, Is.Not.Null);
            Assert.That(model.TryStartJigwiFire(network.LocalPlayerId, began, out error), Is.True, error);
            typeof(LabNetwork).GetField("model", Private).SetValue(network, model);
            typeof(LabNetwork).GetField("latest", Private).SetValue(network, model.Snapshot(began));
            typeof(LabNetwork).GetField("configFingerprint", Private).SetValue(network, "jigwi-settings");
            object packet = CombinePacket(yin.id, yang.id, passive);
            packet.GetType().GetField("configFingerprint").SetValue(packet, "jigwi-settings");
            packet.GetType().GetField("ownershipVersion").SetValue(packet, yin.ownershipVersion);
            packet.GetType().GetField("secondOwnershipVersion").SetValue(packet, yang.ownershipVersion);
            packet = JsonUtility.FromJson(JsonUtility.ToJson(packet), packet.GetType());
            var receipt = (LabActionReceipt)typeof(LabNetwork).GetMethod("ExecuteBoardAction", Private)
                .Invoke(network, new[] { (object)network.LocalPlayerId, packet });
            if (passive && !contactEnabled)
            {
                Assert.That(receipt.accepted, Is.False);
                Assert.That(receipt.reason, Is.EqualTo("CONTACT_COMBINE_DISABLED"));
                Assert.That(model.TryGetMovableState(yin.id, out _), Is.True);
                Assert.That(model.TryGetMovableState(yang.id, out _), Is.True);
            }
            else if (passive)
            {
                Assert.That(receipt.accepted, Is.True, receipt.reason);
                Assert.That(receipt.consumedWithoutOutput, Is.False);
                Assert.That(receipt.outputId, Is.Not.Null.And.Not.Empty);
                Assert.That(model.TryGetMovableState(receipt.outputId, out var combined), Is.True);
                Assert.That(combined.kind, Is.EqualTo(LabOrbKind.Combined));
            }
            else
            {
                Assert.That(receipt.accepted, Is.True, receipt.reason);
                Assert.That(receipt.consumedWithoutOutput, Is.True);
                Assert.That(receipt.outputId, Is.Null);
            }
        }

        private static object CombinePacket(string first, string second, bool passive)
        {
            Type type = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket", true);
            object packet = Activator.CreateInstance(type);
            type.GetField("type").SetValue(packet, "combine");
            type.GetField("requestId").SetValue(packet, 1L);
            type.GetField("firstId").SetValue(packet, first);
            type.GetField("secondId").SetValue(packet, second);
            type.GetField("x").SetValue(packet, .5f);
            type.GetField("y").SetValue(packet, .5f);
            type.GetField("passiveCombine").SetValue(packet, passive);
            return packet;
        }

        [TestCase("checkpoint")]
        [TestCase("ledger")]
        [TestCase("paused")]
        public void EarlyLocalRejectionRestoresBothPrimedBurnBodies(string guard)
        {
            LabOrbView yin = board.UpsertOrb("yin", LabOrbKind.Yin, new Vector2(.35f, .5f));
            LabOrbView yang = board.UpsertOrb("yang", LabOrbKind.Yang, new Vector2(.65f, .5f));
            board.SetJigwiOverheat(true);
            Assert.That(board.TryBeginDragAtWorld(yin.Body.position, 1d), Is.True);
            string first = null, second = null;
            board.JigwiBurnRequested += (a, b) => { first = a; second = b; };
            Assert.That(board.EndDragAtWorld(yang.Body.position, 1.2d), Is.True);
            Assert.That(first, Is.EqualTo("yin")); Assert.That(second, Is.EqualTo("yang"));
            Assert.That(board.IsSnapshotSuppressed(first) && board.IsSnapshotSuppressed(second), Is.True);

            if (guard == "checkpoint")
            {
                var replica = (LabBoardReplica)typeof(LabNetwork).GetField("boardReplica", Private).GetValue(network);
                Assert.That(replica.TryApply(new LabBoardFrame { sessionId = "jigwi", round = 1,
                    sequence = 2, localOwner = network.LocalPlayerId, phase = LabPhase.Playing, serverTime = 1d }, out _), Is.False);
                Assert.That(replica.NeedsCheckpoint, Is.True);
            }
            else if (guard == "ledger")
            {
                var coordinator = (LabLocalActionCoordinator)typeof(LabNetwork).GetField("localActions", Private).GetValue(network);
                Assert.That(coordinator.TryRegister(1, new[] { "other" }, null, 1d, out _), Is.True);
            }
            else network.Snapshot.paused = true;

            Type packetType = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket", true);
            object packet = Activator.CreateInstance(packetType);
            packetType.GetField("type").SetValue(packet, "jigwi_burn");
            packetType.GetField("requestId").SetValue(packet, 1L);
            packetType.GetField("firstId").SetValue(packet, first);
            packetType.GetField("secondId").SetValue(packet, second);
            typeof(LabNetwork).GetMethod("SubmitBoardAction", Private).Invoke(network, new[] { packet });
            Assert.That(board.IsSnapshotSuppressed(first) || board.IsSnapshotSuppressed(second), Is.False);
            Assert.That(yin.gameObject.activeSelf && yang.gameObject.activeSelf, Is.True);
            Assert.That(yin.Body.detectCollisions && yang.Body.detectCollisions, Is.True);
            Assert.That(board.TryBeginDragAtWorld(yang.Body.position, 2d), Is.True,
                "The optional partner must not remain locked after a local guard rejection.");
        }
    }
}
