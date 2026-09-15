using System;
using System.Text;
using C6.Prototype.Lobby;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Tests.Lobby
{
    public sealed class LobbyOptionalDtoTests
    {
        private const string Room = "11111111111111111111111111111111";
        private const string Session = "22222222222222222222222222222222";
        private const string Nonce = "33333333333333333333333333333333";
        private static LobbySnapshot Snapshot(string phase)
        {
            var authority = new LobbyAuthority(Room, Session, "14", "{\"schema\":1}", 123);
            if (phase == "host-only") return authority.Snapshot();
            Assert.That(authority.TryAdmit(42, new LobbyHello { protocol = 10, build = "14", roomId = Room, clientNonce = Nonce }, out _), Is.True);
            if (phase == "playing")
            {
                Request(authority, 42, LobbyProtocol.AckInitial, 1);
                Request(authority, 42, LobbyProtocol.SetReady, 2);
                Request(authority, 0, LobbyProtocol.SetReady, 1);
                Request(authority, 0, LobbyProtocol.Start, 2);
            }
            return authority.Snapshot(Nonce);
        }
        private static void Request(LobbyAuthority authority, ulong sender, string kind, ulong sequence)
        {
            var request = new LobbyRequest { protocol = 10, roomId = Room, sessionId = Session, requestId = Guid.NewGuid().ToString("N"),
                revision = authority.Revision, sequence = sequence, kind = kind, ready = true, configFingerprint = authority.ConfigFingerprint };
            Assert.That(authority.Handle(sender, request, out var reason), Is.True, reason);
        }
        [TestCase("host-only")] [TestCase("connected")] [TestCase("playing")]
        public void DirectJsonCopiesUsedByProbePreserveAbsentAndPresentValues(string phase)
        {
            var source = Snapshot(phase); string raw = JsonUtility.ToJson(source);
            var copy = JsonUtility.FromJson<LobbySnapshot>(raw);
            TestContext.WriteLine("C6_T10A_JSON_ARRAY_COPY unity=" + Application.unityVersion + " phase=" + phase + " raw=" + raw);
            Assert.That(LobbyWire.ValidSnapshot(copy), Is.True, raw);
            Assert.That(copy.p2 == null, Is.EqualTo(source.p2 == null));
            Assert.That(copy.start == null, Is.EqualTo(source.start == null));
            Assert.That(copy.ParticipantCount, Is.EqualTo(source.ParticipantCount));
            Assert.That(copy.p2Entries.Length, Is.EqualTo(phase == "host-only" ? 0 : 1));
            Assert.That(copy.startEntries.Length, Is.EqualTo(phase == "playing" ? 1 : 0));
        }
        [TestCase("host-only")] [TestCase("connected")] [TestCase("playing")]
        public void ActualNestedLobbyReplyPreservesSnapshotOptionalsThroughWire(string phase)
        {
            var source = Snapshot(phase);
            var reply = new LobbyReply { requestId = Guid.NewGuid().ToString("N"), sequence = 1,
                accepted = phase == "playing", reason = phase == "playing" ? "" : "HOST_ONLY", snapshot = source };
            byte[] encoded = LobbyWire.Encode(reply);
            Assert.That(LobbyWire.TryDecode(encoded, out LobbyReply copy), Is.True);
            Assert.That(copy.requestId, Is.EqualTo(reply.requestId)); Assert.That(copy.sequence, Is.EqualTo(reply.sequence));
            Assert.That(LobbyWire.ValidSnapshot(copy.snapshot), Is.True, Encoding.UTF8.GetString(encoded));
            Assert.That(copy.snapshot.p2 == null, Is.EqualTo(source.p2 == null));
            Assert.That(copy.snapshot.start == null, Is.EqualTo(source.start == null));
        }
        [TestCase("nullP2Array")] [TestCase("nullStartArray")]
        [TestCase("twoP2")] [TestCase("twoStarts")]
        [TestCase("nullP2Item")] [TestCase("nullStartItem")]
        [TestCase("blankP2")] [TestCase("blankStart")]
        public void InvalidOptionalCardinalityOrItemNeverBecomesAnAbsentValidValue(string mutation)
        {
            var value = Snapshot("host-only");
            if (mutation == "nullP2Array") value.p2Entries = null;
            if (mutation == "nullStartArray") value.startEntries = null;
            if (mutation == "twoP2") value.p2Entries = new[] { new LobbyPlayer(), new LobbyPlayer() };
            if (mutation == "twoStarts") value.startEntries = new[] { new LobbyStartContract(), new LobbyStartContract() };
            if (mutation == "nullP2Item") value.p2Entries = new LobbyPlayer[] { null };
            if (mutation == "nullStartItem") value.startEntries = new LobbyStartContract[] { null };
            if (mutation == "blankP2") value.p2Entries = new[] { new LobbyPlayer() };
            if (mutation == "blankStart") value.startEntries = new[] { new LobbyStartContract() };
            Assert.That(LobbyWire.ValidSnapshot(value), Is.False);
        }
        [TestCase("p2Entries")] [TestCase("startEntries")]
        public void EmptyObjectInPresentOptionalWireArrayIsRejectedRatherThanNormalized(string field)
        {
            string raw = JsonUtility.ToJson(Snapshot("host-only")).Replace("\"" + field + "\":[]", "\"" + field + "\":[{}]");
            Assert.That(raw, Does.Contain("\"" + field + "\":[{}]"));
            Assert.That(LobbyWire.TryDecode(Encoding.UTF8.GetBytes(raw), out LobbySnapshot copy), Is.True);
            Assert.That(LobbyWire.ValidSnapshot(copy), Is.False, raw);
        }
        [TestCase("p2Entries")] [TestCase("startEntries")]
        public void MissingOptionalArrayFieldCannotMasqueradeAsExplicitEmptyArray(string field)
        {
            string raw = JsonUtility.ToJson(Snapshot("host-only"));
            raw = raw.Replace("\"" + field + "\":[],", "").Replace(",\"" + field + "\":[]", "");
            Assert.That(raw, Does.Not.Contain("\"" + field + "\""));
            Assert.That(LobbyWire.TryDecode(Encoding.UTF8.GetBytes(raw), out LobbySnapshot copy), Is.True);
            Assert.That(LobbyWire.ValidSnapshot(copy), Is.False, "C6_T10A_JSON_MISSING_ARRAY raw=" + raw + " parsed=" + JsonUtility.ToJson(copy));
        }
    }
}
