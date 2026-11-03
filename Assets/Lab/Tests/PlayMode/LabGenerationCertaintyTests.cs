using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>The Guest reads its tap certainty from the snapshot and its still-unanswered requests (#457).</summary>
    public sealed class LabGenerationCertaintyTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Session = "certainty";
        private GameObject root;
        private LabNetwork network;
        private LabConfig config;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Generation certainty fixture");
            config = ScriptableObject.CreateInstance<LabConfig>();
            network = root.AddComponent<LabNetwork>();
            network.enabled = false;
            network.config = config;
            Coordinator().SetActive(Session, 1u, network.LocalPlayerId);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
        }

        private int Reserve => LabGenerationPrediction.ArrivalReserve(config.FoxFireCountMax, config.YaguangFiveColorOrbCount, 0);

        [Test]
        public void AnsweredRequestsOfTheRoundDoNotCountAsPending()
        {
            float cost = config.GenerateCost;
            SetSnapshot(cost, 0);
            // Earlier taps of the round stay in actionRequests after their receipts; only one is still waiting.
            for (long id = 1; id <= 5; id++) Request(id, "generate", registered: false);
            Request(6, "generate", registered: true);
            Assert.That(Certain(), Is.True, "Stamina for one pending tap; the five answered ones are not counted.");
            Request(7, "generate", registered: true);
            Assert.That(Certain(), Is.False, "Two still waiting need two taps' stamina.");
        }

        [Test]
        public void StoredOrbsAndItemsBeingOpenedMustLeaveTheArrivalReserve()
        {
            SetSnapshot(config.GenerateCost * 10f, config.OrbStorageLimit - Reserve - 1);
            Request(1, "generate", registered: true);
            Assert.That(Certain(), Is.True);
            Request(2, "use", registered: true);
            Assert.That(Certain(), Is.False, "An opened bomb or box adds an orb before the Host reaches the tap.");

            SetSnapshot(config.GenerateCost * 10f, config.OrbStorageLimit - Reserve - 1, inFlight: 4);
            ClearRequests(); Request(3, "generate", registered: true);
            Assert.That(Certain(), Is.True, "In-flight orbs are not stored, as on the Host.");
        }

        [Test]
        public void PausedNotPlayingOrAJigwiFireIsNeverCertain()
        {
            Request(1, "generate", registered: true);
            SetSnapshot(config.GenerateCost * 10f, 0);
            Assert.That(Certain(), Is.True);
            Snapshot().paused = true;
            Assert.That(Certain(), Is.False, "Paused.");
            SetSnapshot(config.GenerateCost * 10f, 0);
            Snapshot().phase = LabPhase.Victory;
            Assert.That(Certain(), Is.False, "Not playing.");
            SetSnapshot(config.GenerateCost * 10f, 0);
            Snapshot().jigwi = new[] { new LabJigwiState { playerId = network.LocalPlayerId, fireStartedAt = 9d, fireEndsAt = 12d,
                targets = new[] { new LabJigwiFireTarget() } } };
            Assert.That(Certain(), Is.False, "A burning fire can overheat the player before the Host reaches the tap.");
        }

        private bool Certain() => (bool)typeof(LabNetwork).GetMethod("GenerationCertain", Private).Invoke(network, null);

        private LabSnapshot Snapshot() => (LabSnapshot)typeof(LabNetwork).GetField("latest", Private).GetValue(network);

        private LabLocalActionCoordinator Coordinator()
            => (LabLocalActionCoordinator)typeof(LabNetwork).GetField("localActions", Private).GetValue(network);

        private void SetSnapshot(float stamina, int stored, int inFlight = 0)
        {
            ulong me = network.LocalPlayerId;
            var orbs = new List<LabOrbState>();
            for (int i = 0; i < stored; i++) orbs.Add(new LabOrbState { id = "stored" + i, owner = me, kind = LabOrbKind.Yin });
            for (int i = 0; i < inFlight; i++) orbs.Add(new LabOrbState { id = "flying" + i, owner = me, kind = LabOrbKind.Yin, inFlight = true });
            orbs.Add(new LabOrbState { id = "theirs", owner = 7ul, kind = LabOrbKind.Yang });
            var snapshot = new LabSnapshot
            {
                sessionId = Session, round = 1u, phase = LabPhase.Playing, serverTime = 10d,
                players = new[] { new LabPlayerState { id = me, stamina = stamina }, new LabPlayerState { id = 7ul, stamina = 0f } },
                orbs = orbs.ToArray()
            };
            typeof(LabNetwork).GetField("latest", Private).SetValue(network, snapshot);
        }

        private System.Collections.IDictionary Requests()
            => (System.Collections.IDictionary)typeof(LabNetwork).GetField("actionRequests", Private).GetValue(network);

        private void ClearRequests() { Requests().Clear(); Coordinator().SetActive(Session, 1u, network.LocalPlayerId); }

        private void Request(long id, string type, bool registered)
        {
            System.Type packetType = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket", true);
            object packet = System.Activator.CreateInstance(packetType, true);
            packetType.GetField("type").SetValue(packet, type);
            packetType.GetField("requestId").SetValue(packet, id);
            Requests().Add(id, packet);
            if (registered)
                Assert.That(Coordinator().TryRegister(id, new[] { "input" + id }, null, 0d, out string reason), Is.True, reason);
        }
    }
}
