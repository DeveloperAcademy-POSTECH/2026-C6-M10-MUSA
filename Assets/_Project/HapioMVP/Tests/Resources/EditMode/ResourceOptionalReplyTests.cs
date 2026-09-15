using System;
using System.Text;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.Resources.Tests
{
    public sealed class ResourceOptionalReplyTests
    {
        [TestCase(false, false)] [TestCase(true, false)] [TestCase(true, true)]
        public void UnknownRejectedAndApprovedGenerateRepliesRemainValidThroughWire(bool known, bool accepted)
        {
            var reply = Reply(); reply.known = known; reply.accepted = accepted;
            reply.reason = accepted ? "GENERATED_ONE_RAW" : known ? "STAMINA_INSUFFICIENT" : "REQUEST_UNKNOWN";
            reply.confirmedOrb = accepted ? Orb() : null;
            reply.inventoryRevision = ulong.MaxValue; reply.resourceRevision = ulong.MaxValue - 1;
            Assert.That(ResourceWire.ValidReply(reply, 100), Is.True);
            using (var writer = ResourceWire.Write(reply))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(ResourceWire.TryRead<ResourceRequestReply>(reader, out var copy), Is.True);
                Assert.That(ResourceWire.ValidReply(copy, 100), Is.True);
                Assert.That(copy.confirmedOrb == null, Is.EqualTo(!accepted));
                Assert.That(copy.reason, Is.EqualTo(reply.reason));
                Assert.That(copy.staminaAfter, Is.EqualTo(reply.staminaAfter));
                Assert.That(copy.inventoryRevision, Is.EqualTo(ulong.MaxValue));
                Assert.That(copy.resourceRevision, Is.EqualTo(ulong.MaxValue - 1));
                if (accepted) Assert.That(copy.confirmedOrb.id, Is.EqualTo("generated-orb"));
            }
        }

        [TestCase("null")] [TestCase("two")] [TestCase("nullItem")] [TestCase("emptyItem")]
        public void MalformedOptionalNeverNormalizesToAValidRejection(string mutation)
        {
            var reply = Reply();
            if (mutation == "null") reply.confirmedOrbEntries = null;
            if (mutation == "two") reply.confirmedOrbEntries = new[] { Orb(), Orb() };
            if (mutation == "nullItem") reply.confirmedOrbEntries = new OrbWire[] { null };
            if (mutation == "emptyItem") reply.confirmedOrbEntries = new[] { new OrbWire() };
            Assert.That(ResourceWire.ValidReply(reply, 100), Is.False);
        }

        [TestCase("missing")] [TestCase("object")] [TestCase("two")] [TestCase("null")] [TestCase("nested")]
        public void MissingOrMalformedWireArrayIsInvalidAfterJsonDeserialization(string mutation)
        {
            string raw = JsonUtility.ToJson(Reply());
            const string field = "\"confirmedOrbEntries\":[]";
            Assert.That(raw, Does.Contain(field));
            if (mutation == "missing" || mutation == "nested")
            {
                raw = raw.Replace("," + field, "").Replace(field + ",", "");
                if (mutation == "nested") raw = raw.Insert(1, "\"spoof\":{\"confirmedOrbEntries\":[]},");
            }
            else raw = raw.Replace(field, "\"confirmedOrbEntries\":" + (mutation == "null" ? "null" : mutation == "object" ? "[{}]" : "[{},{}]"));
            using (var reader = new FastBufferReader(Frame(raw), Allocator.Temp))
                Assert.That(ResourceWire.TryRead<ResourceRequestReply>(reader, out _), Is.False, raw);
        }

        [Test]
        public void AcceptedReceiptStillRequiresActualAuthoritativeOrb()
        {
            var reply = Reply(); reply.known = reply.accepted = true;
            Assert.That(ResourceWire.ValidReply(reply, 100), Is.False);
        }

        private static byte[] Frame(string raw)
        {
            byte[] body = Encoding.UTF8.GetBytes(raw), frame = new byte[body.Length + 5]; frame[0] = 1;
            Array.Copy(BitConverter.GetBytes(body.Length), 0, frame, 1, 4); Array.Copy(body, 0, frame, 5, body.Length);
            return frame;
        }

        private static ResourceRequestReply Reply() => new ResourceRequestReply
        {
            nonce = "11111111111111111111111111111111", sessionId = "session", roundId = 1,
            requestId = "request", sequence = 1, operation = (int)ResourceRequestKind.Generate,
            reason = "STAMINA_INSUFFICIENT", staminaBefore = 0, staminaAfter = 0
        };
        private static OrbWire Orb() => new OrbWire
        {
            id = "generated-orb", owner = 41, kind = (int)OrbKind.Raw, polarity = (int)OrbPolarity.Yin,
            state = (int)OrbAuthorityState.Idle, pos = new Vector2(.5f, .5f)
        };
    }
}
