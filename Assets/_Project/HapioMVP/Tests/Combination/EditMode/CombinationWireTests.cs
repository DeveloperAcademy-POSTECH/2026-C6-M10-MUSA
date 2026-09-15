using System;
using System.Linq;
using System.Text;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.Combination.Tests
{
    public sealed class CombinationWireTests
    {
        [Test]
        public void ExactRequestContextCoordinatesAndMaximumSequenceRoundTrip()
        {
            var request = Packet();
            request.sequence = ulong.MaxValue;
            request.roundId = uint.MaxValue;
            request.sourcePosition = new Vector2(.12345679f, .9876543f);
            using (var writer = CombinationWire.Write(request))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(CombinationWire.TryRead<CombinationPacket>(reader, out var received), Is.True);
                Assert.That(CombinationWire.ValidRequest(received), Is.True);
                Assert.That(CombinationWire.Matches(request, received), Is.True);
                var model = received.ToRequest();
                Assert.That(model.SourceOrbId, Is.EqualTo(request.sourceOrbId));
                Assert.That(model.TargetOrbId, Is.EqualTo(request.targetOrbId));
                Assert.That(model.SequenceNumber, Is.EqualTo(ulong.MaxValue));
                Assert.That(CombinationWire.Matches(received, CombinationPacket.FromRequest(received.nonce, model)), Is.True);
                string[] forbidden = { "sender", "owner", "playerId", "polarity", "cost", "stamina", "combinedId" };
                Assert.That(typeof(CombinationPacket).GetFields().Any(field => forbidden.Contains(field.Name)), Is.False);
            }
        }

        [Test]
        public void PayloadEqualityIncludesBothPositionsNotUnityApproximateVectorEquality()
        {
            var request = Packet();
            var copy = Copy(request);
            Assert.That(CombinationWire.Matches(request, copy), Is.True);
            copy.targetPosition.x += .0000001f;
            Assert.That(request.targetPosition == copy.targetPosition, Is.True, "Unity's approximate equality would miss the changed payload.");
            Assert.That(CombinationWire.Matches(request, copy), Is.False);
            copy = Copy(request);
            copy.sourcePosition.y += .0000001f;
            Assert.That(CombinationWire.Matches(request, copy), Is.False);
        }

        [Test]
        public void MissingContextOversizeIdsZeroSequenceAndNonfinitePositionsAreRejected()
        {
            Action<CombinationPacket>[] invalid =
            {
                value => value.nonce = Guid.Empty.ToString(), value => value.sessionId = "",
                value => value.roundId = 0, value => value.requestId = new string('x', 129),
                value => value.sourceOrbId = null, value => value.targetOrbId = " ", value => value.sequence = 0,
                value => value.sourcePosition = new Vector2(float.NaN, .5f),
                value => value.targetPosition = new Vector2(.5f, float.PositiveInfinity),
                value => value.sourcePosition = new Vector2(-.001f, .5f),
                value => value.targetPosition = new Vector2(.5f, 1.001f)
            };
            foreach (var change in invalid)
            { var request = Packet(); change(request); Assert.That(CombinationWire.ValidRequest(request), Is.False); }
        }

        [Test]
        public void AcceptedReceiptAndCurrentProjectileStateRemainDistinctAfterSerialization()
        {
            var request = Packet();
            var reply = Approved(request);
            reply.inventoryRevision = ulong.MaxValue;
            reply.currentCombined.state = (int)OrbAuthorityState.Projectile;
            reply.currentCombined.sequence = 7;
            using (var writer = CombinationWire.Write(reply))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(CombinationWire.TryRead<CombinationReply>(reader, out var received), Is.True);
                Assert.That(CombinationWire.MatchesReply(request, received, 41), Is.True);
                Assert.That(received.originalCombined.state, Is.EqualTo((int)OrbAuthorityState.Idle));
                Assert.That(received.currentCombined.state, Is.EqualTo((int)OrbAuthorityState.Projectile));
                Assert.That(received.currentCombined.sequence, Is.EqualTo(7));
                Assert.That(received.inventoryRevision, Is.EqualTo(ulong.MaxValue));
                Assert.That(received.currentSource.state, Is.EqualTo((int)OrbAuthorityState.Consumed));
                Assert.That(received.currentTarget.state, Is.EqualTo((int)OrbAuthorityState.Consumed));
            }
        }

        [Test]
        public void ReplyMustBindEveryPendingPayloadFieldAndActualLocalOwner()
        {
            var request = Packet();
            Action<CombinationReply>[] changes =
            {
                value => value.nonce = Guid.NewGuid().ToString("N"), value => value.sessionId = "other-session",
                value => value.roundId++, value => value.requestId = "other-request",
                value => value.sourceOrbId = "other-source", value => value.targetOrbId = "other-target", value => value.sequence++,
                value => value.sourcePosition.x += .0000001f, value => value.targetPosition.y += .0000001f,
                value => value.currentSource.owner = 0, value => value.currentTarget.owner = 0,
                value => value.originalCombined.owner = 0, value => value.currentCombined.owner = 0
            };
            foreach (var change in changes)
            { var reply = Approved(request); change(reply); Assert.That(CombinationWire.MatchesReply(request, reply, 41), Is.False); }
            Assert.That(CombinationWire.MatchesReply(request, Approved(request), 99), Is.False);
        }

        [Test]
        public void AcceptedReplyRequiresTwoConsumedOppositeRawAndNewCombinedIdentity()
        {
            var request = Packet();
            Action<CombinationReply>[] changes =
            {
                value => value.known = false, value => value.inventoryRevision = 0, value => value.currentSource = null, value => value.currentTarget = null,
                value => value.originalCombined = null, value => value.currentCombined = null,
                value => value.currentSource.state = (int)OrbAuthorityState.Idle,
                value => value.currentTarget.state = (int)OrbAuthorityState.Launching,
                value => value.currentTarget.polarity = (int)OrbPolarity.Yin,
                value => value.currentSource.kind = (int)OrbKind.Combined,
                value => value.originalCombined.state = (int)OrbAuthorityState.Projectile,
                value => value.currentCombined.polarity = (int)OrbPolarity.Yang,
                value => value.currentCombined.id = "different-combined",
                value => { value.originalCombined.id = request.sourceOrbId; value.currentCombined.id = request.sourceOrbId; },
                value => value.sourcePending = true, value => value.targetPending = true
            };
            foreach (var change in changes)
            { var reply = Approved(request); change(reply); Assert.That(CombinationWire.ValidReply(reply), Is.False); }
        }

        [Test]
        public void UnknownQueryCanReportCurrentMaterialsButCannotClaimAcceptedOrInventCombined()
        {
            var request = Packet();
            var reply = Rejected(request);
            reply.known = false;
            reply.reason = "REQUEST_UNKNOWN";
            reply.sourcePending = true;
            reply.targetPending = true;
            Assert.That(CombinationWire.MatchesReply(request, reply, 41), Is.True);
            Assert.That(reply.known, Is.False, "Unknown is delivered as unknown and must retain both controller locks.");
            reply.accepted = true;
            Assert.That(CombinationWire.ValidReply(reply), Is.False);
            reply.accepted = false;
            reply.originalCombined = Orb("made-up", OrbKind.Combined, OrbPolarity.None, OrbAuthorityState.Idle);
            Assert.That(CombinationWire.ValidReply(reply), Is.False);
        }

        [Test]
        public void ConclusiveRejectionMayContainOnlyOwnedSourceWhenTargetIsForeignOrAbsent()
        {
            var request = Packet();
            var reply = Rejected(request);
            reply.reason = "OWNER_MISMATCH";
            reply.currentTarget = null;
            Assert.That(CombinationWire.MatchesReply(request, reply, 41), Is.True);
            reply.currentSource.owner = 0;
            Assert.That(CombinationWire.MatchesReply(request, reply, 41), Is.False);
        }

        [Test]
        public void ReplyOrbShapeRejectsUnknownEnumsInvalidPositionAndUnboundedReasons()
        {
            var request = Packet();
            var reply = Rejected(request);
            reply.currentSource.state = 999;
            Assert.That(CombinationWire.ValidReply(reply), Is.False);
            reply = Rejected(request);
            reply.currentTarget.pos = new Vector2(float.NegativeInfinity, .5f);
            Assert.That(CombinationWire.ValidReply(reply), Is.False);
            reply = Rejected(request);
            reply.reason = new string('x', 129);
            Assert.That(CombinationWire.ValidReply(reply), Is.False);
            reply = Rejected(request);
            reply.currentSource.polarity = (int)OrbPolarity.None;
            Assert.That(CombinationWire.ValidReply(reply), Is.False);
        }

        [Test]
        public void ProtocolVersionLengthTruncationMalformedUtf8AndOversizeAreRejected()
        {
            byte[] body = Encoding.UTF8.GetBytes("{\"sessionId\":\"session\"}");
            AssertRejected(Frame(body, 2, body.Length));
            AssertRejected(Frame(body, 1, body.Length + 1));
            AssertRejected(Frame(body, 1, body.Length - 1));
            AssertRejected(new byte[] { 1, 0, 0 });
            AssertRejected(Frame(new byte[] { 0xff }, 1, 1));
            AssertRejected(Frame(new byte[CombinationWire.MaximumBytes], 1, CombinationWire.MaximumBytes));
            Assert.Throws<ArgumentException>(() =>
            {
                using (var ignored = CombinationWire.Write(new CombinationPacket { nonce = new string('x', CombinationWire.MaximumBytes) })) { }
            });
        }

        [Test]
        public void ApprovedReceiptWaitsForActualInventoryRevisionThenAllowsEqualOrNewerSnapshot()
        {
            var reply = Approved(Packet());
            var snapshot = new AttackSnapshot { nonce = reply.nonce, sessionId = reply.sessionId, roundId = reply.roundId,
                revision = reply.inventoryRevision - 1 };
            Assert.That(CombinationWire.InventoryConfirmed(reply, null), Is.False);
            Assert.That(CombinationWire.InventoryConfirmed(reply, snapshot), Is.False,
                "Approval alone must not unlock consumed materials before its complete inventory arrives.");
            Assert.That(snapshot.revision, Is.EqualTo(reply.inventoryRevision - 1), "A receipt must never advance the local snapshot.");
            snapshot.revision = reply.inventoryRevision;
            Assert.That(CombinationWire.InventoryConfirmed(reply, snapshot), Is.True);
            snapshot.revision++;
            Assert.That(CombinationWire.InventoryConfirmed(reply, snapshot), Is.True,
                "A newer full inventory can already contain a launched/consumed result.");
        }

        [Test]
        public void InventoryConfirmationRejectsDifferentConnectionSessionRoundUnknownAndZeroRevision()
        {
            var reply = Approved(Packet());
            var snapshot = new AttackSnapshot { nonce = reply.nonce, sessionId = reply.sessionId, roundId = reply.roundId,
                revision = ulong.MaxValue };
            snapshot.nonce = Guid.NewGuid().ToString("N");
            Assert.That(CombinationWire.InventoryConfirmed(reply, snapshot), Is.False);
            snapshot.nonce = reply.nonce; snapshot.sessionId = "different-session";
            Assert.That(CombinationWire.InventoryConfirmed(reply, snapshot), Is.False);
            snapshot.sessionId = reply.sessionId; snapshot.roundId++;
            Assert.That(CombinationWire.InventoryConfirmed(reply, snapshot), Is.False);
            snapshot.roundId = reply.roundId; reply.known = false;
            Assert.That(CombinationWire.InventoryConfirmed(reply, snapshot), Is.False);
            reply.known = true; reply.accepted = false;
            Assert.That(CombinationWire.InventoryConfirmed(reply, snapshot), Is.False);
            reply.accepted = true; reply.inventoryRevision = 0;
            Assert.That(CombinationWire.InventoryConfirmed(reply, snapshot), Is.False);
            Assert.That(CombinationWire.ValidReply(reply), Is.False);
        }

        private static CombinationPacket Packet() => new CombinationPacket
        {
            nonce = Guid.NewGuid().ToString("N"), sessionId = "session", roundId = 2, requestId = "combine-request",
            sourceOrbId = "yin-source", targetOrbId = "yang-target", sequence = 3,
            sourcePosition = new Vector2(.6f, .3f), targetPosition = new Vector2(.64f, .34f)
        };
        private static CombinationPacket Copy(CombinationPacket packet) => CombinationPacket.FromRequest(packet.nonce, packet.ToRequest());
        private static CombinationReply Rejected(CombinationPacket packet) => new CombinationReply
        {
            nonce = packet.nonce, sessionId = packet.sessionId, roundId = packet.roundId, requestId = packet.requestId,
            sourceOrbId = packet.sourceOrbId, targetOrbId = packet.targetOrbId, sequence = packet.sequence,
            sourcePosition = packet.sourcePosition, targetPosition = packet.targetPosition, known = true, reason = "REJECTED", inventoryRevision = 12,
            currentSource = Orb(packet.sourceOrbId, OrbKind.Raw, OrbPolarity.Yin, OrbAuthorityState.Idle),
            currentTarget = Orb(packet.targetOrbId, OrbKind.Raw, OrbPolarity.Yang, OrbAuthorityState.Idle)
        };
        private static CombinationReply Approved(CombinationPacket packet)
        {
            var reply = Rejected(packet);
            reply.accepted = true; reply.reason = "COMBINATION_APPROVED";
            reply.currentSource.state = reply.currentTarget.state = (int)OrbAuthorityState.Consumed;
            reply.originalCombined = Orb("new-combined", OrbKind.Combined, OrbPolarity.None, OrbAuthorityState.Idle);
            reply.currentCombined = Orb("new-combined", OrbKind.Combined, OrbPolarity.None, OrbAuthorityState.Idle);
            return reply;
        }
        private static OrbWire Orb(string id, OrbKind kind, OrbPolarity polarity, OrbAuthorityState state) => new OrbWire
        { id = id, owner = 41, kind = (int)kind, polarity = (int)polarity, state = (int)state, pos = new Vector2(.62f, .32f) };
        private static byte[] Frame(byte[] body, byte version, int declared)
        {
            var bytes = new byte[5 + body.Length]; bytes[0] = version;
            Array.Copy(BitConverter.GetBytes(declared), 0, bytes, 1, 4); Array.Copy(body, 0, bytes, 5, body.Length);
            return bytes;
        }
        private static void AssertRejected(byte[] bytes)
        {
            using (var reader = new FastBufferReader(bytes, Allocator.Temp))
                Assert.That(CombinationWire.TryRead<CombinationPacket>(reader, out _), Is.False);
        }
    }
}
