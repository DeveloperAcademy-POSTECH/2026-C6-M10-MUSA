using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Tests
{
    /// <summary>Player paint statistics follow the existing Host-owned snapshot/board/receipt routes.</summary>
    public sealed class LabPaintContributionReplicationTests
    {
        private const string Session = "paint-contribution-wire";
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [TestCase(0L)]
        [TestCase(2147483664L)]
        [TestCase(long.MaxValue)]
        public void PlayerSnapshotBoardAndReceiptKeepTheWholeInt64CountAndOwnTheirCopies(long count)
        {
            var snapshot = Snapshot(count);
            LabPlayerState ownCopy = snapshot.players[0].Copy();
            Assert.That(ownCopy.paintedCellsTotal, Is.EqualTo(count));
            ownCopy.paintedCellsTotal = -1;
            Assert.That(snapshot.players[0].paintedCellsTotal, Is.EqualTo(count));
            LabSnapshot restored = JsonUtility.FromJson<LabSnapshot>(string.Concat(LabSessionSettings.ChunkSnapshot(snapshot)));
            Assert.That(restored.players[0].paintedCellsTotal, Is.EqualTo(count));
            Assert.That(LabBoardReplica.CopySnapshot(snapshot).players[0].paintedCellsTotal, Is.EqualTo(count));

            LabBoardFrame frame = new LabBoardPublisher().Build(snapshot, 10, 1, 1);
            frame = JsonUtility.FromJson<LabBoardFrame>(JsonUtility.ToJson(frame));
            var replica = new LabBoardReplica(); replica.SetActive(Session, 1, 10);
            Assert.That(replica.TryApply(frame, out string error), Is.True, error);
            snapshot.players[0].paintedCellsTotal = 0;
            Assert.That(replica.Overlay(Snapshot(0, 0)).players[0].paintedCellsTotal, Is.EqualTo(count),
                "A faster Host header owns an independent copy of the counter.");

            var receipt = new LabActionReceipt { sessionId = Session, round = 1, sender = 10,
                sequence = 2, requestId = 1, accepted = true, serverTime = 2, modelRevision = 2,
                players = Players(count) };
            LabActionReceipt copied = receipt.Copy();
            receipt.players[0].paintedCellsTotal = -1;
            copied = JsonUtility.FromJson<LabActionReceipt>(JsonUtility.ToJson(copied));
            Assert.That(copied.players[0].paintedCellsTotal, Is.EqualTo(count));
            Assert.That(replica.ApplyReceipt(copied), Is.True);
            Assert.That(replica.Overlay(Snapshot(0, 0)).players[0].paintedCellsTotal, Is.EqualTo(count));
        }

        [Test]
        public void NegativeBoardOrReceiptStatsDoNotReplaceAConfirmedCountOrConsumeItsSequence()
        {
            var replica = new LabBoardReplica(); replica.SetActive(Session, 1, 10);
            var publisher = new LabBoardPublisher();
            Assert.That(replica.TryApply(publisher.Build(Snapshot(11), 10, 1, 1), out _), Is.True);
            var invalid = publisher.Build(Snapshot(-1, 2), 10, 2, 2);
            Assert.That(replica.TryApply(invalid, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("INVALID_BOARD_FRAME"));
            var receipt = new LabActionReceipt { sessionId = Session, round = 1, sender = 10,
                sequence = 2, requestId = 1, accepted = true, serverTime = 2, modelRevision = 2,
                players = Players(-1) };
            Assert.That(replica.ApplyReceipt(receipt), Is.False);
            Assert.That(replica.LatestSequence, Is.EqualTo(1));
            Assert.That(replica.Overlay(Snapshot(0, 0)).players[0].paintedCellsTotal, Is.EqualTo(11));
        }

        [Test]
        public void SameTickTerminalAndOlderReceiptsCannotRewindPaintTotalsOrChangeOtherStatRules()
        {
            var replica = new LabBoardReplica(); replica.SetActive(Session, 1, 10);
            var publisher = new LabBoardPublisher();
            LabSnapshot board = Snapshot(10, 2);
            board.players[0].stamina = 60;
            board.players[1].paintedCellsTotal = 40;
            Assert.That(replica.TryApply(publisher.Build(board, 10, 1, 1), out _), Is.True);
            LabSnapshot terminal = Snapshot(20, 2); terminal.phase = LabPhase.Victory;
            terminal.players[0].stamina = 80;
            terminal.players[1].paintedCellsTotal = 30;
            LabSnapshot visible = replica.Overlay(terminal);
            Assert.That(visible.phase, Is.EqualTo(LabPhase.Victory));
            Assert.That(visible.players.Select(player => player.paintedCellsTotal), Is.EqualTo(new long[] { 20, 40 }));
            Assert.That(visible.players[0].stamina, Is.EqualTo(60), "Only the new cumulative statistic changes its merge policy.");
            Assert.That(terminal.players[0].paintedCellsTotal, Is.EqualTo(20));
            Assert.That(terminal.players[1].paintedCellsTotal, Is.EqualTo(30), "The confirmed input snapshot remains untouched.");

            var receipt = new LabActionReceipt { sessionId = Session, round = 1, sender = 10,
                sequence = 2, serverTime = 2, modelRevision = 2, players = Players(12) };
            receipt.players[0].stamina = 50;
            Assert.That(replica.ApplyReceipt(receipt), Is.True);
            Assert.That(replica.ApplyReceipt(receipt), Is.False, "Duplicate receipt still cannot consume the stream twice.");
            Assert.That(replica.TryApply(publisher.Build(Snapshot(long.MaxValue, 1), 10, 1, 1), out _), Is.False,
                "An old stream sequence cannot inject a different counter.");
            visible = replica.Overlay(Snapshot(5, 0));
            Assert.That(visible.players[0].paintedCellsTotal, Is.EqualTo(20), "A later lower receipt or older snapshot cannot undo a seen terminal total.");
            Assert.That(visible.players[0].stamina, Is.EqualTo(50), "Existing receipt/player timestamp behavior is retained.");
        }

        [Test]
        public void PaintTotalsStayWithPlayerIdentityAndResetAtANewRoundOrSession()
        {
            var replica = new LabBoardReplica(); replica.SetActive(Session, 1, 10);
            var publisher = new LabBoardPublisher();
            Assert.That(replica.TryApply(publisher.Build(Snapshot(20, 3), 10, 1, 1), out _), Is.True);
            var receipt = new LabActionReceipt { sessionId = Session, round = 1, sender = 10,
                sequence = 2, serverTime = 2, modelRevision = 2, players = Players(30).Reverse().ToArray() };
            receipt.players[1].stamina = 10;
            Assert.That(replica.ApplyReceipt(receipt), Is.True);
            LabSnapshot visible = replica.Overlay(Snapshot(0, 0));
            Assert.That(visible.players.Single(player => player.id == 10).paintedCellsTotal, Is.EqualTo(30),
                "Even an older player clock can add a greater confirmed cumulative total.");
            Assert.That(visible.players.Single(player => player.id == 10).stamina, Is.EqualTo(80),
                "An older clock does not replace the other player statistics.");
            Assert.That(visible.players.Single(player => player.id == 20).paintedCellsTotal, Is.Zero);

            replica.SetActive(Session, 2, 10);
            LabSnapshot nextRound = Snapshot(0, 0); nextRound.round = 2;
            Assert.That(replica.Overlay(nextRound).players[0].paintedCellsTotal, Is.Zero);
            Assert.That(replica.ApplyReceipt(receipt), Is.False);
            replica.SetActive("another-run", 1, 10);
            LabSnapshot nextSession = Snapshot(0, 0); nextSession.sessionId = "another-run";
            Assert.That(replica.Overlay(nextSession).players[0].paintedCellsTotal, Is.Zero);
        }

        [TestCase(0L)]
        [TestCase(2147483664L)]
        [TestCase(long.MaxValue)]
        public void ConfirmedHostSnapshotChunksMapTheExactCounterOnTheClient(long count)
        {
            using (var client = new SnapshotFixture(false))
            {
                client.DeliverSnapshot(0, count, 1);
                Assert.That(client.Network.Snapshot, Is.Not.Null);
                Assert.That(client.Network.Snapshot.players[0].paintedCellsTotal, Is.EqualTo(count));
                Assert.That(client.Network.Snapshot.playedSeconds, Is.EqualTo(12.5d));
            }
        }

        [Test]
        public void AClientRejectsNegativeStatsAndStatsSentByAnyoneOtherThanTheHost()
        {
            using (var client = new SnapshotFixture(false))
            {
                client.DeliverSnapshot(0, 73, 1);
                LabSnapshot accepted = client.Network.Snapshot;
                Assert.That(accepted.players[0].paintedCellsTotal, Is.EqualTo(73));
                client.DeliverSnapshot(0, -1, 2);
                Assert.That(client.Network.Snapshot, Is.SameAs(accepted), "Malformed Host statistics are rejected before installation.");
                client.DeliverSnapshot(23, 9999, 3);
                Assert.That(client.Network.Snapshot, Is.SameAs(accepted), "Only the existing authenticated Host route may supply the snapshot.");
            }
        }

        [TestCase("snapshot_chunk")]
        [TestCase("board_delta")]
        [TestCase("action_result")]
        public void AGuestCannotSendAuthoritativePlayerStatisticsBackIntoTheHost(string kind)
        {
            using (var host = new SnapshotFixture(true))
            {
                LabSnapshot before = host.Network.Snapshot;
                if (kind == "snapshot_chunk") host.DeliverSnapshot(23, long.MaxValue, 1);
                else host.DeliverForgedStats(23, kind);
                Assert.That(host.Network.Snapshot, Is.SameAs(before));
                Assert.That(host.Model.CopyPlayers().All(player => player.paintedCellsTotal == 0), Is.True);
            }
        }

        private static LabPlayerState[] Players(long count) => new[] {
            new LabPlayerState { id = 10, seat = 0, personalHp = 300, stamina = 80, paintedCellsTotal = count },
            new LabPlayerState { id = 20, seat = 1, personalHp = 300, stamina = 80, paintedCellsTotal = 0 }
        };
        private static LabSnapshot Snapshot(long count, double at = 1) => new LabSnapshot {
            sessionId = Session, round = 1, phase = LabPhase.Playing, serverTime = at,
            hp = 100, remaining = 107.5, playedSeconds = 12.5, players = Players(count)
        };

        /// <summary>In-process packet fixture: the actual reader/source guard and snapshot chunk receiver, no sockets.</summary>
        private sealed class SnapshotFixture : IDisposable
        {
            private readonly GameObject root;
            private readonly LabConfig config;
            private readonly Component manager;
            private readonly Type managerType;
            private readonly LabPaintSurface surface = new LabPaintSurface();
            public readonly LabNetwork Network;
            public readonly LabModel Model;
            public SnapshotFixture(bool host)
            {
                root = new GameObject("Inactive paint contribution wire fixture"); root.SetActive(false);
                Network = root.AddComponent<LabNetwork>();
                config = ScriptableObject.CreateInstance<LabConfig>(); Network.config = config;
                Set("sessionId", Session); Set("targetParticipantCount", 2);
                Set("configRevision", 7); Set("configFingerprint", "paint-stats-settings");
                Set("clientConfigReady", true);
                surface.Reset(new LabPaintGrowthSettings { Resolution = 0 }, 1);
                var receiver = (LabPaintReceiver)typeof(LabNetwork).GetProperty("PaintReceiver", Private).GetValue(Network);
                foreach (LabPaintFrame frame in new LabPaintPublisher().Publish(surface, Session, 1, 1))
                    Assert.That(receiver.Receive(frame), Is.Not.EqualTo(LabPaintReceive.Rejected));
                if (!host) return;
                Model = new LabModel(); Model.Begin(Session, new ulong[] { 0, 23 }, 0, config);
                Set("model", Model); Set("latest", Model.Snapshot(0));
                ((List<ulong>)Get("order")).AddRange(new ulong[] { 0, 23 });
                managerType = typeof(LabNetwork).GetField("manager").FieldType;
                manager = root.AddComponent(managerType); typeof(LabNetwork).GetField("manager").SetValue(Network, manager);
                object local = managerType.GetProperty("LocalClient").GetValue(manager);
                local.GetType().GetProperty("IsServer", Private).SetValue(local, true);
                local.GetType().GetProperty("IsClient", Private).SetValue(local, true);
                managerType.GetProperty("IsListening").SetValue(manager, true);
                Assert.That(Network.IsHost, Is.True);
            }
            public void DeliverSnapshot(ulong sender, long count, long sequence)
            {
                LabSnapshot snapshot = Snapshot(count);
                snapshot.paintColumns = surface.Columns; snapshot.paintRows = surface.Rows;
                snapshot.paintRevision = surface.Revision; snapshot.paintCoveredCount = surface.CoveredCount;
                snapshot.paintCellCount = surface.CellCount; snapshot.paintCount = surface.PaintCount;
                string[] chunks = LabSessionSettings.ChunkSnapshot(snapshot);
                for (int i = 0; i < chunks.Length; i++)
                {
                    object packet = Packet("snapshot_chunk");
                    Put(packet, "snapshotSequence", sequence); Put(packet, "chunkIndex", i);
                    Put(packet, "chunkCount", chunks.Length); Put(packet, "chunkData", chunks[i]);
                    Receive(sender, packet);
                }
            }
            public void DeliverForgedStats(ulong sender, string kind)
            {
                object packet = Packet(kind);
                if (kind == "action_result")
                    Put(packet, "payload", JsonUtility.ToJson(new LabActionReceipt { sessionId = Session,
                        round = 1, sequence = 1, players = Players(long.MaxValue) }));
                else
                {
                    string json = JsonUtility.ToJson(new LabBoardPublisher().Build(Snapshot(long.MaxValue), 0, 1, 1));
                    Put(packet, "snapshotSequence", 1L); Put(packet, "chunkIndex", 0);
                    Put(packet, "chunkCount", 1); Put(packet, "chunkData", json);
                }
                Receive(sender, packet);
            }
            private static object Packet(string kind)
            {
                object packet = Activator.CreateInstance(typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket", true));
                Put(packet, "type", kind); Put(packet, "sessionId", Session); Put(packet, "round", 1u);
                Put(packet, "configRevision", 7); Put(packet, "configFingerprint", "paint-stats-settings");
                return packet;
            }
            private void Receive(ulong sender, object packet)
            {
                byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
                var wire = new byte[payload.Length + 8];
                Buffer.BlockCopy(BitConverter.GetBytes(3), 0, wire, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(payload.Length), 0, wire, 4, 4);
                Buffer.BlockCopy(payload, 0, wire, 8, payload.Length);
                MethodInfo method = typeof(LabNetwork).GetMethod("Receive", Private);
                Type readerType = method.GetParameters()[1].ParameterType;
                ConstructorInfo constructor = readerType.GetConstructors().Single(candidate =>
                    candidate.GetParameters().Length == 4 && candidate.GetParameters()[0].ParameterType == typeof(byte[]));
                object reader = constructor.Invoke(new[] { (object)wire,
                    Enum.Parse(constructor.GetParameters()[1].ParameterType, "Temp"), -1, 0 });
                try { method.Invoke(Network, new[] { (object)sender, reader }); }
                finally { ((IDisposable)reader).Dispose(); }
            }
            private object Get(string name) => typeof(LabNetwork).GetField(name, Private).GetValue(Network);
            private void Set(string name, object value) => typeof(LabNetwork).GetField(name, Private).SetValue(Network, value);
            private static void Put(object packet, string name, object value) => packet.GetType().GetField(name).SetValue(packet, value);
            public void Dispose()
            {
                if (manager != null) managerType.GetProperty("IsListening").SetValue(manager, false);
                Object.DestroyImmediate(root); Object.DestroyImmediate(config);
            }
        }
    }
}
