using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace C6Lab.Tests
{
    /// <summary>The control packet envelope stays lean and bodies survive the payload string (#428).</summary>
    public sealed class LabNetworkPacketTests
    {
        /// <summary>Ceiling for a body-less packet (measured 937 B); before #428 the same packet was 4,189 bytes.</summary>
        private const int MaximumMinimalPacketBytes = 1100;

        [Test]
        public void ABodylessPacketCarriesNoDefaultNestedObjectsAndStaysUnderTheByteBudget()
        {
            Type packetType = typeof(LabNetwork).Assembly.GetType("C6Lab.LabNetworkPacket", true);
            object packet = Activator.CreateInstance(packetType, true);
            packetType.GetField("type").SetValue(packet, "generate");
            string json = JsonUtility.ToJson(packet);
            Assert.That(Encoding.UTF8.GetByteCount(json), Is.LessThanOrEqualTo(MaximumMinimalPacketBytes), json);
            foreach (string removed in new[] { "\"snapshot\":", "\"boardFrame\":", "\"paintFrame\":", "\"actionReceipt\":" })
                Assert.That(json, Does.Not.Contain(removed), removed + " must not ride in every packet.");
            Assert.That(json, Does.Contain("\"payload\":\"\""), "A missing body is an empty string, never a default object.");
        }

        [Test]
        public void ActionReceiptAndPaintFrameSurviveThePayloadRoundTrip()
        {
            var receipt = new LabActionReceipt
            {
                sessionId = "payload-test", round = 3u, sender = 10ul, requestId = 42L, sequence = 7L, accepted = false,
                reason = "RECEIVER_FULL", serverTime = 12.5d, removedIds = new[] { "a", "b" },
                states = new[] { new LabOrbState { id = "orb", owner = 20ul, kind = LabOrbKind.Combined, x = .25f, y = .75f, ownershipVersion = 2ul } },
                players = new[] { new LabPlayerState { id = 10ul, seat = 5, stamina = 60f, personalHp = 237 } }
            };
            string encoded = LabPacketPayload.Encode(receipt);
            Assert.That(encoded, Does.Not.StartWith("\"").And.Not.Empty);
            LabActionReceipt decoded = LabPacketPayload.Decode<LabActionReceipt>(encoded);
            Assert.That(JsonUtility.ToJson(decoded), Is.EqualTo(JsonUtility.ToJson(receipt)));
            Assert.That(decoded.players.Single().seat, Is.EqualTo(5));
            Assert.That(decoded.states.Single().ownershipVersion, Is.EqualTo(2ul));

            var surface = new LabPaintSurface(new LabPaintGrowthSettings { Resolution = 0, Pattern = LabPaintPattern.Legacy }, 7u);
            surface.RegisterHit(Vector3.forward, Color.red, 123ul, 6);
            LabPaintFrame[] frames = new LabPaintPublisher().Publish(surface, "payload-test", 3u, 1L);
            foreach (LabPaintFrame frame in frames)
            {
                LabPaintFrame copy = LabPacketPayload.Decode<LabPaintFrame>(LabPacketPayload.Encode(frame));
                Assert.That(JsonUtility.ToJson(copy), Is.EqualTo(JsonUtility.ToJson(frame)));
            }
            var replica = new LabPaintSurface(new LabPaintGrowthSettings { Resolution = 0, Pattern = LabPaintPattern.Legacy }, 7u);
            var receiver = new LabPaintReceiver(replica);
            LabPaintReceive result = LabPaintReceive.Rejected;
            foreach (LabPaintFrame frame in frames)
                result = receiver.Receive(LabPacketPayload.Decode<LabPaintFrame>(LabPacketPayload.Encode(frame)));
            Assert.That(result, Is.EqualTo(LabPaintReceive.Applied));
            Assert.That(replica.CoveredCount, Is.EqualTo(surface.CoveredCount));
        }

        [Test]
        public void AMissingOrMalformedPayloadDecodesToNull()
        {
            Assert.That(LabPacketPayload.Encode<LabActionReceipt>(null), Is.Null);
            Assert.That(LabPacketPayload.Decode<LabActionReceipt>(null), Is.Null);
            Assert.That(LabPacketPayload.Decode<LabActionReceipt>(string.Empty), Is.Null);
            Assert.That(LabPacketPayload.Decode<LabActionReceipt>("{not json"), Is.Null);
        }

        [Test]
        public void OnlyANonCheckpointFrameWithoutChangedOrRemovedOrbsIsAnEmptyDelta()
        {
            var publisher = new LabBoardPublisher();
            var snapshot = new LabSnapshot { sessionId = "empty-delta", round = 1u, serverTime = 1d, phase = LabPhase.Playing };
            snapshot.orbs = new[] { new LabOrbState { id = "mine", owner = 10ul, kind = LabOrbKind.Combined, x = .5f, y = .5f, ownershipVersion = 1ul } };
            LabBoardFrame first = publisher.Build(snapshot, 10ul, 1, 1ul);
            Assert.That(first.checkpoint, Is.True);
            Assert.That(LabBoardPublisher.IsEmptyDelta(first), Is.False, "A checkpoint always goes out.");

            snapshot.serverTime = 1.1d; snapshot.orbs[0].x = .6f; snapshot.orbs[0].revision++;
            LabBoardFrame unchanged = publisher.Build(snapshot, 10ul, 2, 2ul);
            Assert.That(unchanged.states, Is.Empty);
            Assert.That(unchanged.removedIds, Is.Empty);
            Assert.That(LabBoardPublisher.IsEmptyDelta(unchanged), Is.True, "Pose-only motion changes nothing the peer must learn now.");

            snapshot.serverTime = 1.2d; snapshot.orbs[0].owner = 20ul; snapshot.orbs[0].ownershipVersion++;
            LabBoardFrame departed = publisher.Build(snapshot, 10ul, 3, 3ul);
            Assert.That(departed.removedIds, Is.EqualTo(new[] { "mine" }));
            Assert.That(LabBoardPublisher.IsEmptyDelta(departed), Is.False);

            snapshot.serverTime = 1.3d;
            snapshot.orbs = new[] { new LabOrbState { id = "arrived", owner = 10ul, kind = LabOrbKind.Yin, x = .5f, y = .5f, ownershipVersion = 1ul } };
            LabBoardFrame arrived = publisher.Build(snapshot, 10ul, 4, 4ul);
            Assert.That(arrived.states.Single().id, Is.EqualTo("arrived"));
            Assert.That(LabBoardPublisher.IsEmptyDelta(arrived), Is.False);

            snapshot.serverTime = 1.3d + LabBoardPublisher.CheckpointIntervalSeconds;
            Assert.That(LabBoardPublisher.IsEmptyDelta(publisher.Build(snapshot, 10ul, 5, 5ul)), Is.False, "The periodic checkpoint still goes out.");
            Assert.That(LabBoardPublisher.IsEmptyDelta(null), Is.False);
        }
    }
}
