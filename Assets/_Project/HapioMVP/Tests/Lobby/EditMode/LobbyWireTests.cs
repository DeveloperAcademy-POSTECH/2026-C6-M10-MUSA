using System;
using System.Text;
using C6.Prototype.Lobby;
using NUnit.Framework;
using UnityEngine;

namespace C6.Prototype.Tests.Lobby
{
    public sealed class LobbyWireTests
    {
        private const string Room = "11111111111111111111111111111111";
        private const string Session = "22222222222222222222222222222222";
        private const string Nonce = "33333333333333333333333333333333";
        private const string Config = "{\"schema\":1,\"staminaStart\":100}";
        private static LobbyHello Hello() => new LobbyHello { protocol = 10, build = "14", roomId = Room, clientNonce = Nonce };
        private static LobbyAuthority Authority() => new LobbyAuthority(Room, Session, "14", Config, 123);
        private static LobbySnapshot Incoming()
        {
            var authority = Authority(); authority.TryAdmit(42, Hello(), out _); return authority.Snapshot(Nonce);
        }
        [Test] public void HelloAndSnapshotRoundTripKeepAllBoundIdentityAndConfigFields()
        {
            Assert.That(LobbyWire.TryDecode(LobbyWire.Encode(Hello()), out LobbyHello hello), Is.True);
            Assert.That(LobbyWire.ValidHello(hello), Is.True); Assert.That(hello.clientNonce, Is.EqualTo(Nonce));
            var initial = Incoming();
            Assert.That(LobbyWire.TryDecode(LobbyWire.Encode(initial), out LobbySnapshot received), Is.True);
            string details = OptionalDiagnostic("two-participant", initial, received);
            Assert.That(LobbyWire.ValidSnapshot(received), Is.True, details);
            Assert.That(received.p2.clientId, Is.EqualTo(42)); Assert.That(received.hostConfigJson, Is.EqualTo(Config));
            Assert.That(received.start, Is.Null); Assert.That(received.roundId, Is.Zero);
            Assert.That(received.p2.initialStateReceived, Is.False);
        }
        [Test] public void HostOnlySnapshotRoundTripPreservesEmptyP2Slot()
        {
            Assert.That(LobbyWire.TryDecode(LobbyWire.Encode(Authority().Snapshot()), out LobbySnapshot received), Is.True);
            string details = OptionalDiagnostic("host-only", Authority().Snapshot(), received);
            Assert.That(LobbyWire.ValidSnapshot(received), Is.True, details); Assert.That(received.p2, Is.Null);
            Assert.That(received.ParticipantCount, Is.EqualTo(1));
        }
        private static string OptionalDiagnostic(string label, LobbySnapshot before, LobbySnapshot after)
        {
            string details = "C6_T10A_JSON_OPTIONAL label=" + label + " unity=" + Application.unityVersion
                + " beforeP2Null=" + (before.p2 == null) + " beforeStartNull=" + (before.start == null)
                + " parsedP2Null=" + (after.p2 == null) + " parsedStartNull=" + (after.start == null)
                + " raw=" + Encoding.UTF8.GetString(LobbyWire.Encode(before))
                + " parsed=" + JsonUtility.ToJson(after);
            TestContext.WriteLine(details);
            return details;
        }
        [Test] public void MaximumUnsignedSenderSequenceAndRevisionSurviveJsonRoundTrip()
        {
            var request = new LobbyRequest { protocol = 10, roomId = Room, sessionId = Session,
                requestId = Nonce, sequence = ulong.MaxValue, revision = ulong.MaxValue,
                kind = LobbyProtocol.SetReady, ready = true, configFingerprint = LobbyWire.Fingerprint(Config) };
            Assert.That(LobbyWire.TryDecode(LobbyWire.Encode(request), out LobbyRequest parsed), Is.True);
            Assert.That(LobbyWire.ValidRequest(parsed), Is.True); Assert.That(parsed.sequence, Is.EqualTo(ulong.MaxValue));
            Assert.That(parsed.revision, Is.EqualTo(ulong.MaxValue));
        }
        [TestCase("")] [TestCase("[]")] [TestCase("null")] [TestCase("{]")]
        [TestCase("{broken}")] [TestCase("{}garbage")]
        public void InvalidJsonOrNonObjectEnvelopeIsRejected(string json)
        { Assert.That(LobbyWire.TryDecode(Encoding.UTF8.GetBytes(json), out LobbyHello _), Is.False); }
        [Test] public void MissingFieldsMayParseButNeverPassSemanticValidation()
        {
            Assert.That(LobbyWire.TryDecode(Encoding.UTF8.GetBytes("{}"), out LobbyHello hello), Is.True);
            Assert.That(LobbyWire.ValidHello(hello), Is.False);
            Assert.That(LobbyWire.ValidRequest(new LobbyRequest()), Is.False);
            Assert.That(LobbyWire.ValidSnapshot(new LobbySnapshot()), Is.False);
        }
        [Test] public void InvalidUtf8IsRejectedWithoutReplacementCharacters()
        { Assert.That(LobbyWire.TryDecode(new byte[] { 123, 34, 0xc3, 0x28, 34, 58, 49, 125 }, out LobbyHello _), Is.False); }
        [Test] public void PacketLimitsApplyBeforeJsonParsingAndPerControlMessageType()
        {
            Assert.That(LobbyWire.TryDecode(new byte[LobbyWire.MaximumHelloBytes + 1], out LobbyHello _), Is.False);
            Assert.That(LobbyWire.TryDecode(new byte[LobbyWire.MaximumRequestBytes + 1], out LobbyRequest _), Is.False);
            Assert.That(LobbyWire.TryDecode(new byte[LobbyWire.MaximumBytes + 1], out LobbySnapshot _), Is.False);
            Assert.That(LobbyWire.TryDecode(null, out LobbyHello _), Is.False);
        }
        [Test] public void OversizedEncodeAndConfigAreRejected()
        {
            var hello = Hello(); hello.build = new string('a', LobbyWire.MaximumHelloBytes);
            Assert.Throws<ArgumentException>(() => LobbyWire.Encode(hello));
            Assert.That(LobbyWire.ValidConfigJson("{\"text\":\"" + new string('a', LobbyWire.MaximumConfigBytes) + "\"}"), Is.False);
            Assert.Throws<ArgumentException>(() => new LobbyAuthority(Room, Session, "14", "[]", 1));
        }
        [Test] public void FingerprintBindsExactHostPayloadAndNeverAcceptsChangedValues()
        {
            Assert.That(LobbyWire.Fingerprint(Config), Is.EqualTo(LobbyWire.Fingerprint(Config)));
            Assert.That(LobbyWire.Fingerprint(Config), Has.Length.EqualTo(64));
            var snapshot = Incoming(); snapshot.hostConfigJson = "{\"schema\":1,\"staminaStart\":0}";
            Assert.That(LobbyWire.ValidSnapshot(snapshot), Is.False);
        }
        [TestCase("nonce")] [TestCase("build")] [TestCase("room")]
        public void FirstSnapshotMustMatchSelectedBuildConnectionNonceAndDiscoveredRoom(string mismatch)
        {
            var value = Incoming(); string build = "14", nonce = Nonce, room = Room;
            if (mismatch == "nonce") nonce = Guid.NewGuid().ToString("N");
            if (mismatch == "build") build = "13";
            if (mismatch == "room") room = Guid.NewGuid().ToString("N");
            Assert.That(LobbyWire.AcceptsSnapshot(null, value, build, nonce, room), Is.False);
        }
        [Test] public void DirectIpFirstSnapshotPinsHostRoomAndThenRejectsSessionReplacement()
        {
            var value = Incoming(); Assert.That(LobbyWire.AcceptsSnapshot(null, value, "14", Nonce), Is.True);
            var next = Incoming(); next.revision++; next.sessionId = Guid.NewGuid().ToString("N");
            Assert.That(LobbyWire.AcceptsSnapshot(value, next, "14", Nonce), Is.False);
        }
        [TestCase("sameRevision")] [TestCase("olderRevision")] [TestCase("seed")]
        [TestCase("config")] [TestCase("participant")]
        public void BoundSnapshotRejectsDuplicateOlderOrChangedImmutableContract(string mutation)
        {
            var current = Incoming(); var next = Incoming(); next.revision++;
            if (mutation == "sameRevision") next.revision = current.revision;
            if (mutation == "olderRevision") next.revision = current.revision - 1;
            if (mutation == "seed") next.seed++;
            if (mutation == "config") { next.hostConfigJson = "{}"; next.configFingerprint = LobbyWire.Fingerprint("{}"); }
            if (mutation == "participant") next.p2.clientId = 99;
            Assert.That(LobbyWire.AcceptsSnapshot(current, next, "14", Nonce), Is.False);
        }
        [TestCase("p1Number")] [TestCase("p2Number")] [TestCase("p2HostId")]
        [TestCase("readyWithoutAck")] [TestCase("fakeCanStart")] [TestCase("fakePlaying")]
        [TestCase("fakeRound")] [TestCase("badFingerprint")]
        public void SemanticallyInvalidStateCannotPassWireValidation(string mutation)
        {
            var value = Incoming();
            if (mutation == "p1Number") value.p1.playerNumber = 2;
            if (mutation == "p2Number") value.p2.playerNumber = 1;
            if (mutation == "p2HostId") value.p2.clientId = 0;
            if (mutation == "readyWithoutAck") value.p2.ready = true;
            if (mutation == "fakeCanStart") value.canStart = true;
            if (mutation == "fakePlaying") value.phase = LobbyProtocol.Playing;
            if (mutation == "fakeRound") value.roundId = 1;
            if (mutation == "badFingerprint") value.configFingerprint = new string('g', 64);
            Assert.That(LobbyWire.ValidSnapshot(value), Is.False);
        }
        [Test] public void SuccessfulInitialAckCannotBeRolledBackByANewerRevision()
        {
            var current = Incoming(); current.p2.initialStateReceived = true;
            var next = Incoming(); next.revision++;
            Assert.That(LobbyWire.AcceptsSnapshot(current, next, "14", Nonce), Is.False);
        }
        [Test] public void ClosedRoomCannotBeRevivedByNewerLobbySnapshot()
        {
            var authority = Authority(); authority.TryAdmit(42, Hello(), out _); authority.Close("PEER_LEFT");
            var current = authority.Snapshot(Nonce); var next = Incoming(); next.revision = current.revision + 1;
            Assert.That(LobbyWire.ValidSnapshot(current), Is.True);
            Assert.That(LobbyWire.AcceptsSnapshot(current, next, "14", Nonce), Is.False);
        }
    }
}
