using System;
using System.Text;
using C6.Prototype.Orbs;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.Attack.Tests
{
    public sealed class AttackOptionalReplyTests
    {
        [Serializable]
        private sealed class LegacyInlineReply { public OrbWire confirmedOrb; }

        [Test]
        public void LegacyInlineNullRoundTripInventsAnInvalidOrbOnInstalledUnity()
        {
            var legacy = new LegacyInlineReply { confirmedOrb = null };
            string raw = JsonUtility.ToJson(legacy);
            var copy = JsonUtility.FromJson<LegacyInlineReply>(raw);
            TestContext.WriteLine("C6_T10B_LEGACY_OPTIONAL unity=" + Application.unityVersion + " raw=" + raw);
            Assert.That(copy.confirmedOrb, Is.Not.Null, "Reproduces the pre-fix inline class serialization behavior.");
            Assert.That(AttackWire.ValidOrb(copy.confirmedOrb), Is.False,
                "The old reply guard rejected an authoritative absence as an invalid invented orb.");
        }

        [TestCase(false, false)] [TestCase(true, false)] [TestCase(true, true)]
        public void MissingOrRejectedOrApprovedAuthorityPreservesOptionalAcrossActualWire(bool known, bool present)
        {
            var reply = Reply(); reply.known = known; reply.accepted = present;
            reply.confirmedOrb = present ? Orb() : null; reply.inventoryRevision = ulong.MaxValue;
            using (var writer = AttackWire.Write(reply))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(AttackWire.TryRead<AttackRequestReply>(reader, out var copy), Is.True);
                Assert.That(copy.confirmedOrb == null, Is.EqualTo(!present));
                Assert.That(copy.known, Is.EqualTo(known));
                Assert.That(copy.accepted, Is.EqualTo(present));
                Assert.That(copy.requestId, Is.EqualTo(reply.requestId));
                Assert.That(copy.inventoryRevision, Is.EqualTo(ulong.MaxValue));
                if (present)
                {
                    Assert.That(copy.confirmedOrb.id, Is.EqualTo(reply.orbId));
                    Assert.That(copy.confirmedOrb.owner, Is.EqualTo(41));
                    Assert.That(AttackWire.ValidOrb(copy.confirmedOrb), Is.True);
                }
            }
        }

        [TestCase("null")] [TestCase("two")] [TestCase("nullItem")] [TestCase("emptyItem")]
        public void InvalidOptionalArrayCannotBecomeConfirmedAbsence(string mutation)
        {
            var reply = Reply();
            if (mutation == "null") reply.confirmedOrbEntries = null;
            if (mutation == "two") reply.confirmedOrbEntries = new[] { Orb(), Orb() };
            if (mutation == "nullItem") reply.confirmedOrbEntries = new OrbWire[] { null };
            if (mutation == "emptyItem") reply.confirmedOrbEntries = new[] { new OrbWire() };
            Assert.That(AttackWire.ValidOptionalOrb(reply.confirmedOrbEntries), Is.False);
        }

        [TestCase("missing")] [TestCase("object")] [TestCase("two")] [TestCase("null")] [TestCase("nested")]
        public void MalformedOptionalPayloadIsRejectedByAttackDecoder(string mutation)
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
            var body = Encoding.UTF8.GetBytes(raw);
            var frame = new byte[body.Length + 5]; frame[0] = 1;
            Array.Copy(BitConverter.GetBytes(body.Length), 0, frame, 1, 4); Array.Copy(body, 0, frame, 5, body.Length);
            using (var reader = new FastBufferReader(frame, Allocator.Temp))
                Assert.That(AttackWire.TryRead<AttackRequestReply>(reader, out _), Is.False, raw);
        }

        [Test]
        public void TokenGuardUnderstandsEscapedKeysAndWhitespace()
        {
            Assert.That(OrbReplyJsonShape.HasRequiredArrays(" { \"confirmed\\u004frbEntries\" : [ ] } ", "confirmedOrbEntries"), Is.True);
            Assert.That(OrbReplyJsonShape.HasRequiredArrays("{\"text\":\"confirmedOrbEntries:[]\"}", "confirmedOrbEntries"), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void DuplicateTopLevelOptionalKeyIsRejectedEvenWhenOneKeyIsEscaped(bool escaped)
        {
            string raw = JsonUtility.ToJson(Reply());
            string duplicate = escaped ? "\"confirmed\\u004frbEntries\":[]," : "\"confirmedOrbEntries\":[],";
            raw = raw.Insert(1, duplicate);
            byte[] body = Encoding.UTF8.GetBytes(raw), frame = new byte[body.Length + 5]; frame[0] = 1;
            Array.Copy(BitConverter.GetBytes(body.Length), 0, frame, 1, 4); Array.Copy(body, 0, frame, 5, body.Length);
            using (var reader = new FastBufferReader(frame, Allocator.Temp))
                Assert.That(AttackWire.TryRead<AttackRequestReply>(reader, out _), Is.False, raw);
        }

        private static AttackRequestReply Reply() => new AttackRequestReply
        {
            nonce = "11111111111111111111111111111111", sessionId = "session", roundId = 1,
            requestId = "request", orbId = "orb", reason = "REQUEST_UNKNOWN"
        };
        private static OrbWire Orb() => new OrbWire
        {
            id = "orb", owner = 41, kind = (int)OrbKind.Combined, polarity = (int)OrbPolarity.None,
            state = (int)OrbAuthorityState.Consumed, pos = new Vector2(.5f, .5f)
        };
    }
}
