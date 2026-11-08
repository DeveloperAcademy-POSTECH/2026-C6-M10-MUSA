using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    public sealed class LabTerminalSnapshotCatchupTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void FrozenDefeatQueuesTheExactFinalMapAndSnapshotWhenBulkBacklogDrains()
        {
            using (var host = new Fixture(true))
            using (var guest = new Fixture(false))
            {
                var receiver = new LabBulkReceiver();
                var messages = new LabBulkMessageAssembler();
                var sender = new LabBulkSender();
                host.Senders.Add(23, sender);
                while (sender.QueuedBytes < LabNetwork.BulkBacklogLimitBytes)
                    Assert.That(sender.Enqueue(new string('x', 900), 0d), Is.True);
                long backlogEnd = sender.LastEnqueued;
                Assert.That(host.Model.PaintSurface.RegisterHit(Vector3.forward,
                    host.Model.PaintColorFor(LabOrbElement.Fire), 1, 1), Is.True);
                Assert.That(host.Model.PaintSurface.CoveredCount, Is.GreaterThan(0),
                    "Recovery must transfer actual painted cells, not just an empty terminal map.");
                host.Defeat();
                LabSnapshot expected = host.Network.Snapshot;
                host.Invoke("PublishSharedSnapshots", 1L);
                Assert.That(sender.LastEnqueued, Is.EqualTo(backlogEnd), "The full peer is skipped on the terminal publish.");
                Assert.That(host.Deferred, Does.Contain(23ul));
                host.Network.FreezeGameBattle();
                guest.Network.FreezeGameBattle();
                host.Invoke("FlushDeferredTerminalSnapshots");
                Assert.That(sender.LastEnqueued, Is.EqualTo(backlogEnd), "The terminal screen must not bypass Bulk backpressure.");
                Drain(sender, receiver, messages, guest);
                Assert.That(guest.Network.Snapshot.phase, Is.EqualTo(LabPhase.Playing), "The final snapshot was really absent, not preinstalled.");
                host.Invoke("FlushDeferredTerminalSnapshots");
                Assert.That(host.Deferred, Is.Empty);
                Assert.That(sender.LastEnqueued, Is.GreaterThan(backlogEnd));
                long finalQueued = sender.LastEnqueued;
                host.Invoke("FlushDeferredTerminalSnapshots");
                Assert.That(sender.LastEnqueued, Is.EqualTo(finalQueued), "Once reliably queued, the terminal transaction is not added every frame.");
                Drain(sender, receiver, messages, guest);
                LabSnapshot actual = guest.Network.Snapshot;
                Assert.That(actual.phase, Is.EqualTo(LabPhase.Defeat));
                Assert.That(actual.sessionId, Is.EqualTo(expected.sessionId));
                Assert.That(actual.round, Is.EqualTo(expected.round));
                Assert.That(actual.remaining, Is.EqualTo(expected.remaining));
                Assert.That(actual.paintCoverage, Is.EqualTo(expected.paintCoverage));
                Assert.That(actual.paintRevision, Is.EqualTo(expected.paintRevision));
                Assert.That(actual.players.Select(player => player.personalHp), Is.EqualTo(new[] { 0, 0 }));
                Assert.That(guest.Network.PaintSurface.CoveredCount, Is.EqualTo(host.Model.PaintSurface.CoveredCount));
                Assert.That(guest.Network.PaintSurface.Revision, Is.EqualTo(host.Model.PaintSurface.Revision));
                Assert.That(actual.phase, Is.Not.EqualTo(LabPhase.Playing),
                    "The matching Defeat snapshot can now satisfy the game's failure result readiness barrier.");
            }
        }

        [Test]
        public void ANewRoundOrTransportResetRetiresDeferredTerminalRecovery()
        {
            using (var host = new Fixture(true))
            {
                var sender = new LabBulkSender(); host.Senders.Add(23, sender);
                while (sender.QueuedBytes < LabNetwork.BulkBacklogLimitBytes) sender.Enqueue(new string('x', 900), 0d);
                host.Defeat(); host.Invoke("PublishSharedSnapshots", 1L);
                Assert.That(host.Deferred, Is.Not.Empty);
                host.Set("latest", new LabSnapshot { sessionId = "new-stage", round = 1, phase = LabPhase.Playing });
                host.Invoke("FlushDeferredTerminalSnapshots");
                Assert.That(host.Deferred, Is.Empty, "No terminal data may cross into another stage/round.");
                host.Set("latest", host.Model.Snapshot(0d));
                host.Invoke("PublishSharedSnapshots", 2L);
                Assert.That(host.Deferred, Is.Not.Empty);
                host.Invoke("ResetBulkTransport");
                Assert.That(host.Deferred, Is.Empty);
                Assert.That(host.Senders, Is.Empty);
            }
        }

        private static void Drain(LabBulkSender sender, LabBulkReceiver receiver, LabBulkMessageAssembler messages, Fixture guest)
        {
            double now = Time.realtimeSinceStartupAsDouble + 1d;
            for (int pass = 0; sender.Count > 0 && pass < 10000; pass++, now += .01d)
            {
                LabBulkFrame[] frames = sender.GetDue(now, 65536);
                Assert.That(frames, Is.Not.Empty);
                foreach (LabBulkFrame frame in frames)
                    foreach (string ready in receiver.Receive(frame.sequence, frame.payload))
                        if (messages.Receive(ready, out string encoded)) guest.Deliver(encoded);
                Assert.That(sender.Ack(receiver.AckThrough, receiver.SelectiveAck, now), Is.True);
            }
            Assert.That(sender.Count, Is.Zero);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private readonly LabConfig config;
            private readonly Component manager;
            private readonly Type managerType;
            public readonly LabNetwork Network;
            public readonly LabModel Model;
            public Dictionary<ulong, LabBulkSender> Senders => Get<Dictionary<ulong, LabBulkSender>>("bulkSenders");
            public HashSet<ulong> Deferred => Get<HashSet<ulong>>("deferredTerminalSnapshotPeers");

            public Fixture(bool host)
            {
                root = new GameObject("Inactive terminal snapshot catch-up fixture"); root.SetActive(false);
                Network = root.AddComponent<LabNetwork>();
                config = ScriptableObject.CreateInstance<LabConfig>(); Network.config = config;
                Assert.That(Network.SetGameFlowEnabled(true), Is.True);
                Model = new LabModel(); Model.Begin("failed-stage", new ulong[] { 0, 23 }, 0d, config);
                Set("model", host ? Model : null);
                Set("sessionId", "failed-stage"); Set("latest", Model.Snapshot(0d));
                Set("targetParticipantCount", 2); Set("configRevision", 7); Set("configFingerprint", "same-settings");
                Set("clientConfigReady", true);
                if (!host) return;
                Get<List<ulong>>("order").AddRange(new ulong[] { 0, 23 });
                managerType = typeof(LabNetwork).GetField("manager").FieldType;
                manager = root.AddComponent(managerType); typeof(LabNetwork).GetField("manager").SetValue(Network, manager);
                object local = managerType.GetProperty("LocalClient").GetValue(manager);
                local.GetType().GetProperty("IsServer", Private).SetValue(local, true);
                local.GetType().GetProperty("IsClient", Private).SetValue(local, true);
                managerType.GetProperty("IsListening").SetValue(manager, true);
            }
            public void Defeat()
            {
                Assert.That(Model.TryApplyPlayerDamage(0, config.PlayerMaxHp, 0d), Is.True);
                Assert.That(Model.TryApplyPlayerDamage(23, config.PlayerMaxHp, 0d), Is.True);
                Set("latest", Model.Snapshot(0d));
                Assert.That(Network.Snapshot.phase, Is.EqualTo(LabPhase.Defeat));
            }
            public void Deliver(string encoded)
            {
                Type bodyType = typeof(LabNetwork).Assembly.GetType("C6Lab.LabBulkPayload", true);
                object body = JsonUtility.FromJson(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)), bodyType);
                string kind = (string)bodyType.GetField("type").GetValue(body);
                Type packetType = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket", true);
                object packet = Activator.CreateInstance(packetType);
                foreach (FieldInfo field in bodyType.GetFields())
                    packetType.GetField(field.Name).SetValue(packet, field.GetValue(body));
                Invoke(kind == "paint_cells" ? "ReceivePaintFrame" : "ReceiveSnapshotChunk", packet);
            }
            public T Get<T>(string name) => (T)typeof(LabNetwork).GetField(name, Private).GetValue(Network);
            public void Set(string name, object value) => typeof(LabNetwork).GetField(name, Private).SetValue(Network, value);
            public object Invoke(string name, params object[] args) => typeof(LabNetwork).GetMethod(name, Private).Invoke(Network, args);
            public void Dispose()
            {
                if (manager != null) managerType.GetProperty("IsListening").SetValue(manager, false);
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
