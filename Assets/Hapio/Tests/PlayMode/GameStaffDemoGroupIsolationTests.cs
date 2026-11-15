using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using C6Lab;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hapio.Tests
{
    /// <summary>
    /// Two real NGO/dual-stack rooms and six steady participants on loopback. This validates transport,
    /// admission and authoritative round separation, not six independent displays or wireless devices.
    /// </summary>
    [Category("Bonjour")]
    public sealed class GameStaffDemoGroupIsolationTests
    {
        private readonly List<Peer> peers = new List<Peer>();
        private IDisposable variant;
        private LabConfig config;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(GameSession.Instance, Is.Null, "This fixture owns network peers, not an extra global GameSession.");
            variant = LabGameBuildVariant.OverrideStaffStageDemoForTests(true);
            config = Object.Instantiate(Resources.Load<LabConfig>("LabConfig"));
            float[] values = config.CaptureDeveloperValues();
            Set(values, "Battle Duration (seconds)", 600f);
            // AUTO gives only one drum to a random seat in a three-player round. This fixture
            // exercises a Host-owned board action, so give each of the three P places a drum.
            Set(values, "Drum Count", 3f);
            Set(values, "Paint Surface Resolution (0 low 1 medium 2 high)", 0f);
            Set(values, "Paint Initial Coverage (%)", 100f);
            Set(values, "Paint Drying (0 off, 1 on)", 0f);
            Set(values, "High Resolution Paint (0 off, 1 on)", 0f);
            Assert.That(config.TryApplyDeveloperValues(values, out string error), Is.True, error);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = peers.Count - 1; index >= 0; index--) peers[index].Dispose();
            peers.Clear();
            yield return null;
            yield return null;
            if (config != null) Object.Destroy(config);
            config = null;
            variant?.Dispose(); variant = null;
        }

        [UnityTest]
        public IEnumerator SixParticipantsPlayInTwoIndependentThreePlayerGroupsAndOnlyAReturnsToSeats()
        {
            ushort aPort = AvailablePort(), bPort;
            do { bPort = AvailablePort(); } while (bPort == aPort);
            Peer a = NewPeer("A Host", aPort, 1);
            Peer b = NewPeer("B Host", bPort, 2);
            Assert.That(a.Network.ConfigureHostSession(config, 3), Is.True);
            Assert.That(b.Network.ConfigureHostSession(config, 3), Is.True);
            a.Network.Host(); b.Network.Host();
            yield return Until(() => a.Network.IsHost && b.Network.IsHost, "Both group Hosts start on independent ports");
            yield return Until(() => !string.IsNullOrEmpty(a.Network.HostRoomName) && !string.IsNullOrEmpty(b.Network.HostRoomName),
                "Each group keeps its separately confirmed fixed advertisement name");
            Assert.That(a.Network.HostRoomName, Is.Not.EqualTo(b.Network.HostRoomName));
            Assert.That(a.Network.HostRoomName, Does.Contain("Group-1"));
            Assert.That(b.Network.HostRoomName, Does.Contain("Group-2"));

            Peer wrongGroup = NewPeer("B cannot join A", a.Network.port, 2);
            wrongGroup.Network.Join("127.0.0.1");
            yield return Until(() => !wrongGroup.Network.IsJoiningRoom && wrongGroup.Network.RoomSearchFailed,
                "A rejects B's group token even at a direct endpoint");
            Assert.That(wrongGroup.Network.IsConnected, Is.False);
            Assert.That(a.Network.ParticipantCount, Is.EqualTo(1));
            Assert.That(b.Network.ParticipantCount, Is.EqualTo(1));
            wrongGroup.Dispose(); peers.Remove(wrongGroup);
            yield return null;

            Peer a2 = JoinPeer("A P2", a.Network.port, 1);
            Peer a3 = JoinPeer("A P3", a.Network.port, 1);
            Peer b2 = JoinPeer("B P2", b.Network.port, 2);
            Peer b3 = JoinPeer("B P3", b.Network.port, 2);
            yield return Until(() => new[] { a2, a3, b2, b3 }.All(peer => peer.Network.CanSelectPlayerSlot),
                "All four Guests reach their own group's lobby");
            Assert.That(a.Network.RequestPlayerSlot(1), Is.True);
            Assert.That(a2.Network.RequestPlayerSlot(2), Is.True);
            Assert.That(a3.Network.RequestPlayerSlot(3), Is.True);
            Assert.That(b.Network.RequestPlayerSlot(1), Is.True);
            Assert.That(b2.Network.RequestPlayerSlot(2), Is.True);
            Assert.That(b3.Network.RequestPlayerSlot(3), Is.True);
            yield return Until(() => a.Network.CanStartBattle && b.Network.CanStartBattle,
                "Both groups independently hold exactly three selected, configured P places");
            foreach (Peer peer in new[] { a, a2, a3 })
            {
                Assert.That(peer.Network.DemoGroupId, Is.EqualTo(1));
                Assert.That(peer.Network.ParticipantCount, Is.EqualTo(3));
            }
            foreach (Peer peer in new[] { b, b2, b3 })
            {
                Assert.That(peer.Network.DemoGroupId, Is.EqualTo(2));
                Assert.That(peer.Network.ParticipantCount, Is.EqualTo(3));
            }
            Assert.That(a.Network.SetDemoStageIdentity(2, 2), Is.False, "An active Host cannot change groups.");
            Assert.That(a2.Network.SetDemoStageIdentity(2, 2), Is.False, "An active Guest cannot change groups.");

            a.BindBoard(config, new Vector3(-30f, 0f, 0f));
            b.BindBoard(config, new Vector3(30f, 0f, 0f));
            Assert.That(a.Network.PrepareGameStage(config, 2, "yaguang", LabStageYokai.Yaguang), Is.True);
            Assert.That(b.Network.PrepareGameStage(config, 2, "yaguang", LabStageYokai.Yaguang), Is.True);
            yield return Until(() => a.Network.SessionConfigReady && b.Network.SessionConfigReady,
                "Both rooms receive their own stage-profile confirmations");
            Assert.That(a.Network.StartCurrentGameStage(), Is.True);
            Assert.That(b.Network.Snapshot, Is.Null, "Starting A does not automatically start B.");
            Assert.That(b.Network.StartCurrentGameStage(), Is.True);
            yield return Until(() => new[] { a, a2, a3, b, b2, b3 }.All(peer => peer.Network.Snapshot?.phase == LabPhase.Playing),
                "Six participants receive their respective Playing snapshots");
            string aBattle = a.Network.Snapshot.sessionId;
            string bBattle = b.Network.Snapshot.sessionId;
            Assert.That(aBattle, Is.Not.EqualTo(bBattle));
            foreach (Peer guest in new[] { a2, a3 }) Assert.That(guest.Network.Snapshot.sessionId, Is.EqualTo(aBattle));
            foreach (Peer guest in new[] { b2, b3 }) Assert.That(guest.Network.Snapshot.sessionId, Is.EqualTo(bBattle));

            Assert.That(a.Network.Snapshot.drums.Any(drum => drum.owner == a.Network.LocalPlayerId), Is.True,
                "The deterministic fixture must give the A Host a drum before its real Generate action.");
            a.Network.Generate();
            yield return Until(() => a.Network.Snapshot.players.Sum(player => player.generatedTotal) == 1,
                "An A board action is accepted by A's Host");
            Assert.That(b.Network.Snapshot.players.Sum(player => player.generatedTotal), Is.Zero);
            Assert.That(b.Network.Snapshot.orbs, Is.Empty);
            double bBeforePause = b.Network.Snapshot.remaining;
            Assert.That(a.Network.PauseBattle(), Is.True);
            yield return Until(() => a2.Network.Snapshot.paused && a3.Network.Snapshot.paused,
                "A's pause reaches both A Guests");
            foreach (Peer peer in new[] { b, b2, b3 }) Assert.That(peer.Network.Snapshot.paused, Is.False);
            yield return Until(() => b.Network.Snapshot.remaining < bBeforePause - .05d,
                "B continues its own battle clock while A is paused");
            Assert.That(a.Network.Snapshot.paused, Is.True);
            Assert.That(a.Network.ResumeBattle(), Is.True);

            LabModel aModel = (LabModel)typeof(LabNetwork).GetField("model", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(a.Network);
            Assert.That(aModel.PaintSurface.RegisterHit(Vector3.forward, aModel.PaintColorFor(LabOrbElement.Fire),
                a.Network.LocalPlayerId, 1), Is.True);
            yield return Until(() => new[] { a, a2, a3 }.All(peer => peer.Network.Snapshot?.phase == LabPhase.Victory),
                "Only A reaches its authoritative completed result");
            foreach (Peer peer in new[] { b, b2, b3 })
            {
                Assert.That(peer.Network.Snapshot.sessionId, Is.EqualTo(bBattle));
                Assert.That(peer.Network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
                Assert.That(peer.Network.Snapshot.paintCoveredCount, Is.Zero);
            }

            Assert.That(a.Network.ReturnStaffDemoGameToLobby(), Is.True);
            yield return Until(() => new[] { a, a2, a3 }.All(peer => peer.Network.Snapshot == null
                && peer.Network.SelectedParticipantCount == 0 && peer.Network.CanSelectPlayerSlot),
                "A alone returns to the three unselected seats for a retry");
            foreach (Peer peer in new[] { a, a2, a3 })
            {
                Assert.That(peer.Network.DemoGroupId, Is.EqualTo(1));
                Assert.That(peer.Network.ParticipantCount, Is.EqualTo(3));
            }
            foreach (Peer peer in new[] { b, b2, b3 })
            {
                Assert.That(peer.Network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
                Assert.That(peer.Network.Snapshot.sessionId, Is.EqualTo(bBattle));
                Assert.That(peer.Network.SelectedParticipantCount, Is.EqualTo(3));
            }
        }

        private Peer NewPeer(string name, ushort port, int group)
        {
            var peer = new Peer(name, config, port, group);
            peers.Add(peer);
            return peer;
        }

        private Peer JoinPeer(string name, ushort port, int group)
        {
            Peer peer = NewPeer(name, port, group);
            peer.Network.Join("127.0.0.1");
            return peer;
        }

        private static void Set(float[] values, string label, float value)
            => values[Enumerable.Range(0, LabConfig.DeveloperFields.Count).Single(index => LabConfig.DeveloperFields[index].Label == label)] = value;

        private static IEnumerator Until(Func<bool> condition, string message)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 30d;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }

        private static ushort AvailablePort()
        {
            for (int attempt = 0; attempt < 16; attempt++)
                using (var ipv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    ipv4.Bind(new IPEndPoint(IPAddress.Any, 0));
                    int port = ((IPEndPoint)ipv4.LocalEndPoint).Port;
                    try
                    {
                        if (Socket.OSSupportsIPv6)
                            using (var ipv6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp))
                            { ipv6.DualMode = false; ipv6.Bind(new IPEndPoint(IPAddress.IPv6Any, port)); }
                        return checked((ushort)port);
                    }
                    catch (SocketException error) when (error.SocketErrorCode == SocketError.AddressAlreadyInUse) { }
                }
            throw new InvalidOperationException("No available dual-family fixture port.");
        }

        private sealed class Peer : IDisposable
        {
            private readonly GameObject root;
            public readonly LabNetwork Network;

            public Peer(string name, LabConfig config, ushort port, int group)
            {
                root = new GameObject(name); root.SetActive(false);
                root.AddComponent(typeof(LabNetwork).GetField("manager").FieldType);
                root.AddComponent(typeof(LabNetwork).Assembly.GetType("C6Lab.LabDualStackTransport", true));
                Network = root.AddComponent<LabNetwork>(); Network.config = config; Network.port = port;
                root.SetActive(true);
                Assert.That(Network.SetGameFlowEnabled(true), Is.True);
                Assert.That(Network.SetDemoStageIdentity(2, group), Is.True);
            }

            public void BindBoard(LabConfig config, Vector3 offset)
            {
                var cameraObject = new GameObject("Fixture board camera"); cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.position = offset + new Vector3(0f, 0f, -10f);
                Camera camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.enabled = false;
                var boardObject = new GameObject("Fixture owner board"); boardObject.transform.SetParent(root.transform, false);
                LabOrbBoard board = boardObject.AddComponent<LabOrbBoard>();
                board.Configure(config, camera, new Rect(offset.x - 4f, -2f, 8f, 4f));
                var targetObject = new GameObject("Fixture target"); targetObject.transform.SetParent(root.transform, false);
                targetObject.transform.position = offset + Vector3.forward * 10f;
                LabTarget target = targetObject.AddComponent<LabTarget>();
                var floorObject = new GameObject("Fixture throw floor"); floorObject.transform.SetParent(root.transform, false);
                floorObject.transform.position = offset + Vector3.down * 10f;
                Collider floor = floorObject.AddComponent<BoxCollider>();
                Network.BindBattleReferences(board, target, camera, floor, null);
            }

            public void Dispose()
            {
                if (root == null) return;
                Network.Stop(); Object.Destroy(root);
            }
        }
    }
}
