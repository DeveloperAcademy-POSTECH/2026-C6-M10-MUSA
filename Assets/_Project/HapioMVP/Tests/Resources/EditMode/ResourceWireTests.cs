using System;
using System.Linq;
using System.Text;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.Resources.Tests
{
    public sealed class ResourceWireTests
    {
        [Test]
        public void ContinuousFractionalStaminaAndLargeSequenceSurviveNetworkSerialization()
        {
            var original = Snapshot();
            original.players[1].stamina = 5.12345678901234d;
            original.players[1].lastSequence = ulong.MaxValue;
            using (var writer = ResourceWire.Write(original))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(ResourceWire.TryRead<ResourceSnapshot>(reader, out var received), Is.True);
                Assert.That(ResourceWire.ValidSnapshot(received), Is.True);
                Assert.That(received.players[1].stamina, Is.EqualTo(original.players[1].stamina).Within(1e-12));
                Assert.That(received.players[1].lastSequence, Is.EqualTo(ulong.MaxValue));
                Assert.That(received.regenerationRate, Is.EqualTo(20d / 3d).Within(1e-12));
                Assert.That(received.seed, Is.EqualTo(uint.MaxValue));
                Assert.That(received.debugTestMode, Is.False);
            }
        }

        [Test]
        public void SnapshotRejectsNaNInfinityOverMaximumNegativeAndThirdPlayer()
        {
            var snapshot = Snapshot();
            foreach (double value in new[] { double.NaN, double.PositiveInfinity, -1d, 100.0001d })
            {
                snapshot.players[0].stamina = value;
                Assert.That(ResourceWire.ValidSnapshot(snapshot), Is.False);
            }
            snapshot.players[0].stamina = 100;
            snapshot.players = snapshot.players.Concat(new[] { new ResourcePlayerWire { playerId = 99, stamina = 100 } }).ToArray();
            Assert.That(ResourceWire.ValidSnapshot(snapshot), Is.False);
        }

        [Test]
        public void SnapshotRejectsDuplicateOwnersAndInvalidStorageCounts()
        {
            var snapshot = Snapshot();
            snapshot.players[1].playerId = snapshot.players[0].playerId;
            Assert.That(ResourceWire.ValidSnapshot(snapshot), Is.False);
            snapshot.players[1].playerId = 41;
            snapshot.players[0].storedOrbs = 21;
            Assert.That(ResourceWire.ValidSnapshot(snapshot), Is.False);
            snapshot.players[0].storedOrbs = 20;
            Assert.That(ResourceWire.ValidSnapshot(snapshot), Is.True);
            snapshot.players[0].generatedTotal = 257;
            Assert.That(ResourceWire.ValidSnapshot(snapshot), Is.False);
        }

        [Test]
        public void RequestsCarryContextAndOperationButNeverClientOwnerPolarityOrCost()
        {
            var request = new ResourceRequestPacket { nonce = Guid.NewGuid().ToString("N"), sessionId = "session",
                roundId = 2, requestId = "request", sequence = ulong.MaxValue, operation = (int)ResourceRequestKind.Generate };
            using (var writer = ResourceWire.Write(request))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(ResourceWire.TryRead<ResourceRequestPacket>(reader, out var received), Is.True);
                Assert.That(ResourceWire.ValidRequest(received), Is.True);
                Assert.That(received.sequence, Is.EqualTo(ulong.MaxValue));
                Assert.That(received.operation, Is.EqualTo((int)ResourceRequestKind.Generate));
                string[] forbidden = { "sender", "owner", "playerId", "polarity", "cost", "stamina", "position" };
                Assert.That(typeof(ResourceRequestPacket).GetFields().Any(field => forbidden.Contains(field.Name)), Is.False);
            }
            request.sequence = 0;
            Assert.That(ResourceWire.ValidRequest(request), Is.False);
            request.sequence = 1;
            request.operation = 99;
            Assert.That(ResourceWire.ValidRequest(request), Is.False);
        }

        [Test]
        public void AcceptedReplyRequiresKnownActualOrbAndFiniteBoundedResourceReceipt()
        {
            var reply = Reply();
            Assert.That(ResourceWire.ValidReply(reply, 100), Is.True);
            reply.known = false;
            Assert.That(ResourceWire.ValidReply(reply, 100), Is.False);
            reply.known = true;
            var orb = reply.confirmedOrb;
            reply.confirmedOrb = null;
            Assert.That(ResourceWire.ValidReply(reply, 100), Is.False);
            reply.confirmedOrb = orb;
            reply.staminaBefore = double.NaN;
            Assert.That(ResourceWire.ValidReply(reply, 100), Is.False);
            reply.staminaBefore = 100;
            reply.confirmedOrb.polarity = (int)OrbPolarity.None;
            Assert.That(ResourceWire.ValidReply(reply, 100), Is.False);
        }

        [Test]
        public void ExplicitDebugModeAndNormalModeAreIndependentOfDebugToolAvailability()
        {
            var normal = Snapshot();
            Assert.That(normal.debugToolsEnabled, Is.True);
            Assert.That(normal.debugTestMode, Is.False);
            normal.debugTestMode = true;
            using (var writer = ResourceWire.Write(normal))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(ResourceWire.TryRead<ResourceSnapshot>(reader, out var received), Is.True);
                Assert.That(received.debugTestMode, Is.True);
                Assert.That(received.players.All(player => player.stamina == 100), Is.True);
            }
        }

        [Test]
        public void ChangedProtocolLengthMalformedUtf8AndOversizeAreRejected()
        {
            byte[] body = Encoding.UTF8.GetBytes("{\"sessionId\":\"session\"}");
            AssertRejected(Frame(body, 2, body.Length));
            AssertRejected(Frame(body, 1, body.Length + 1));
            AssertRejected(Frame(body, 1, body.Length - 1));
            AssertRejected(Frame(new byte[] { 0xff }, 1, 1));
            AssertRejected(Frame(new byte[ResourceWire.MaximumBytes], 1, ResourceWire.MaximumBytes));
            Assert.Throws<ArgumentException>(() =>
            {
                using (var ignored = ResourceWire.Write(new ResourceSyncPacket { nonce = new string('x', ResourceWire.MaximumBytes) })) { }
            });
        }

        [Test]
        public void MissingConnectionNonceOrRoundCannotBecomeAuthorityState()
        {
            var snapshot = Snapshot();
            snapshot.nonce = Guid.Empty.ToString();
            Assert.That(ResourceWire.ValidSnapshot(snapshot), Is.False);
            snapshot.nonce = Guid.NewGuid().ToString("N");
            snapshot.roundId = 0;
            Assert.That(ResourceWire.ValidSnapshot(snapshot), Is.False);
            snapshot.roundId = 1;
            snapshot.revision = 0;
            Assert.That(ResourceWire.ValidSnapshot(snapshot), Is.False);
        }

        private static ResourceSnapshot Snapshot() => new ResourceSnapshot
        {
            nonce = Guid.NewGuid().ToString("N"), sessionId = "session", roundId = 1, revision = 1,
            seed = uint.MaxValue, playing = true, debugTestMode = false, debugToolsEnabled = true,
            maximum = 100, generateCost = 20, regenerationRate = 20d / 3d, hitRecovery = 5, storageLimit = 20,
            players = new[] { new ResourcePlayerWire { playerId = 0, stamina = 100 }, new ResourcePlayerWire { playerId = 41, stamina = 100 } }
        };

        private static ResourceRequestReply Reply() => new ResourceRequestReply
        {
            nonce = Guid.NewGuid().ToString("N"), sessionId = "session", roundId = 1, requestId = "request", sequence = 1,
            operation = (int)ResourceRequestKind.Generate, known = true, accepted = true, duplicate = false,
            reason = "GENERATED_ONE_RAW", staminaBefore = 100, staminaAfter = 80,
            confirmedOrb = new OrbWire { id = "orb", owner = 41, kind = (int)OrbKind.Raw, polarity = (int)OrbPolarity.Yin,
                state = (int)OrbAuthorityState.Idle, pos = new Vector2(.25f, .375f) }
        };

        private static byte[] Frame(byte[] body, byte version, int declared)
        {
            var bytes = new byte[5 + body.Length];
            bytes[0] = version;
            Array.Copy(BitConverter.GetBytes(declared), 0, bytes, 1, 4);
            Array.Copy(body, 0, bytes, 5, body.Length);
            return bytes;
        }

        private static void AssertRejected(byte[] bytes)
        {
            using (var reader = new FastBufferReader(bytes, Allocator.Temp))
                Assert.That(ResourceWire.TryRead<ResourceSyncPacket>(reader, out _), Is.False);
        }
    }
}
