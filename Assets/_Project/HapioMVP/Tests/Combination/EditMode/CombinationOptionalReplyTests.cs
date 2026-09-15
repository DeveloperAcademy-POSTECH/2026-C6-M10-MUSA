using System;
using System.Text;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.Combination.Tests
{
    public sealed class CombinationOptionalReplyTests
    {
        [TestCase(false, false, false)] [TestCase(false, true, true)]
        [TestCase(true, true, false)] [TestCase(true, true, true)]
        public void UnknownAndRejectedQueriesPreserveAbsentAndOwnedMaterialsThroughWire(bool known, bool sourcePresent, bool targetPresent)
        {
            var reply = Reply(); reply.known = known;
            reply.reason = known ? "OWNER_MISMATCH" : "REQUEST_UNKNOWN";
            reply.currentSource = sourcePresent ? Orb("source", OrbKind.Raw, OrbPolarity.Yin) : null;
            reply.currentTarget = targetPresent ? Orb("target", OrbKind.Raw, OrbPolarity.Yang) : null;
            Assert.That(CombinationWire.ValidReply(reply), Is.True);
            using (var writer = CombinationWire.Write(reply))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(CombinationWire.TryRead<CombinationReply>(reader, out var copy), Is.True);
                Assert.That(CombinationWire.ValidReply(copy), Is.True);
                Assert.That(copy.currentSource == null, Is.EqualTo(!sourcePresent));
                Assert.That(copy.currentTarget == null, Is.EqualTo(!targetPresent));
                Assert.That(copy.originalCombined, Is.Null); Assert.That(copy.currentCombined, Is.Null);
                Assert.That(copy.known, Is.EqualTo(known)); Assert.That(copy.accepted, Is.False);
            }
        }

        [TestCase("originalCombined")] [TestCase("currentSource")] [TestCase("currentTarget")] [TestCase("currentCombined")]
        public void EveryOptionalFieldRejectsMissingNullMultipleAndInvalidPresentItems(string name)
        {
            var field = typeof(CombinationReply).GetField(name + "Entries");
            Assert.That(field, Is.Not.Null);
            OrbWire[][] mutations = { null, new[] { Orb("a", OrbKind.Raw, OrbPolarity.Yin), Orb("b", OrbKind.Raw, OrbPolarity.Yang) },
                new OrbWire[] { null }, new[] { new OrbWire() } };
            foreach (var entries in mutations)
            {
                var reply = Reply(); field.SetValue(reply, entries);
                Assert.That(CombinationWire.ValidReply(reply), Is.False, name);
            }
            string raw = JsonUtility.ToJson(Reply());
            string text = "\"" + name + "Entries\":[]";
            Assert.That(raw, Does.Contain(text));
            raw = raw.Replace("," + text, "").Replace(text + ",", "");
            using (var reader = new FastBufferReader(Frame(raw), Allocator.Temp))
                Assert.That(CombinationWire.TryRead<CombinationReply>(reader, out _), Is.False, raw);
        }

        [TestCase("originalCombined")] [TestCase("currentSource")] [TestCase("currentTarget")] [TestCase("currentCombined")]
        public void PresentEmptyObjectWireItemIsInvalidRatherThanAbsent(string name)
        {
            string raw = JsonUtility.ToJson(Reply());
            string text = "\"" + name + "Entries\":[]";
            raw = raw.Replace(text, "\"" + name + "Entries\":[{}]");
            using (var reader = new FastBufferReader(Frame(raw), Allocator.Temp))
                Assert.That(CombinationWire.TryRead<CombinationReply>(reader, out _), Is.False, raw);
        }

        [TestCase("originalCombined")] [TestCase("currentSource")] [TestCase("currentTarget")] [TestCase("currentCombined")]
        public void ExplicitNullAndNestedSpoofNeverSupplyARequiredOptionalArray(string name)
        {
            string raw = JsonUtility.ToJson(Reply());
            string text = "\"" + name + "Entries\":[]";
            string nullValue = raw.Replace(text, "\"" + name + "Entries\":null");
            string absent = raw.Replace("," + text, "").Replace(text + ",", "");
            string nested = absent.Insert(1, "\"spoof\":{" + text + "},");
            foreach (string value in new[] { nullValue, nested })
                using (var reader = new FastBufferReader(Frame(value), Allocator.Temp))
                    Assert.That(CombinationWire.TryRead<CombinationReply>(reader, out _), Is.False, value);
        }

        [Test]
        public void ApprovedReceiptKeepsOriginalAndCurrentCombinedSeparateAcrossWire()
        {
            var reply = Reply(); reply.known = reply.accepted = true;
            reply.currentSource = Orb("source", OrbKind.Raw, OrbPolarity.Yin);
            reply.currentTarget = Orb("target", OrbKind.Raw, OrbPolarity.Yang);
            reply.currentSource.state = reply.currentTarget.state = (int)OrbAuthorityState.Consumed;
            reply.originalCombined = Orb("combined", OrbKind.Combined, OrbPolarity.None);
            reply.currentCombined = Orb("combined", OrbKind.Combined, OrbPolarity.None);
            reply.currentCombined.state = (int)OrbAuthorityState.Consumed;
            using (var writer = CombinationWire.Write(reply))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(CombinationWire.TryRead<CombinationReply>(reader, out var copy), Is.True);
                Assert.That(CombinationWire.ValidReply(copy), Is.True);
                Assert.That(copy.originalCombined.state, Is.EqualTo((int)OrbAuthorityState.Idle));
                Assert.That(copy.currentCombined.state, Is.EqualTo((int)OrbAuthorityState.Consumed));
                Assert.That(copy.currentSource.state, Is.EqualTo((int)OrbAuthorityState.Consumed));
                Assert.That(copy.currentTarget.state, Is.EqualTo((int)OrbAuthorityState.Consumed));
            }
        }

        private static byte[] Frame(string raw)
        {
            byte[] body = Encoding.UTF8.GetBytes(raw), frame = new byte[body.Length + 5]; frame[0] = 1;
            Array.Copy(BitConverter.GetBytes(body.Length), 0, frame, 1, 4); Array.Copy(body, 0, frame, 5, body.Length);
            return frame;
        }

        private static CombinationReply Reply() => new CombinationReply
        {
            nonce = "11111111111111111111111111111111", sessionId = "session", roundId = 1,
            requestId = "request", sourceOrbId = "source", targetOrbId = "target", sequence = 1, inventoryRevision = 1,
            sourcePosition = new Vector2(.5f, .5f), targetPosition = new Vector2(.51f, .5f), reason = "REQUEST_UNKNOWN"
        };
        private static OrbWire Orb(string id, OrbKind kind, OrbPolarity polarity) => new OrbWire
        { id = id, owner = 41, kind = (int)kind, polarity = (int)polarity, state = (int)OrbAuthorityState.Idle, pos = new Vector2(.5f, .5f) };
    }
}
