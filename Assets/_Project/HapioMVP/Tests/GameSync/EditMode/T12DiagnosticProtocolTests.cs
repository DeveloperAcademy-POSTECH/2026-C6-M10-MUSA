using System;
using System.Globalization;
using System.Linq;
using System.Text;
using C6.Prototype.Attack;
using C6.Prototype.Orbs;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace C6.Prototype.GameSync.Tests
{
    // Protocol tests only. They do not execute commands, prove device input, or award an AT PASS.
    public sealed class T12DiagnosticProtocolTests
    {
        private const string Run = "device-run-01";
        private const string Session = "0123456789abcdef0123456789abcdef";
        private const string OrbId = "fedcba9876543210fedcba9876543210";
        private const ulong Peer = 41;

        [Test]
        public void MinimalCaptureRequiresAnExplicitRunAndCommandIdentity()
        {
            var command = Read("{\"schema\":1,\"id\":\"capture-01\",\"runId\":\"" + Run + "\",\"action\":\"capture\"}");
            Assert.That(command.id, Is.EqualTo("capture-01"));
            Assert.That(command.count, Is.EqualTo(1));
            Assert.That(command.expectedOwner, Is.EqualTo(ulong.MaxValue));
            Assert.That(command.screenshot, Is.False);
        }

        [Test]
        public void EachSupportedActionHasACompleteValidRoundTrip()
        {
            foreach (string action in new[] { "capture", "generate", "combine", "launch", "transfer", "transferSeries", "hostStart", "retry", "lobbyCreate", "joinDirect", "ready" })
            {
                var command = Series(); command.action = action;
                if (action != "transferSeries") { command.directions = Array.Empty<int>(); command.count = 1; }
                command.otherOrbId = "other-orb";
                Assert.That(Read(JsonUtility.ToJson(command)).PlanHash, Is.EqualTo(command.PlanHash), action);
            }
        }

        [Test]
        public void EveryTruncatedCommandPrefixIsRejectedWithoutCreatingACommand()
        {
            string json = JsonUtility.ToJson(Series());
            for (int length = 0; length < json.Length; length++) Reject(json.Substring(0, length));
            Assert.That(Read(json).action, Is.EqualTo("transferSeries"));
        }

        [Test]
        public void DuplicateKeysIncludingEscapedAliasesCannotOverrideAnEarlierValue()
        {
            string json = JsonUtility.ToJson(Series());
            Reject(json.Insert(1, "\"id\":\"duplicate\","));
            Reject(json.Insert(1, "\"\\u0069d\":\"duplicate\","));
            Reject(json.Insert(1, "\"expectedOwner\":99,"));
        }

        [Test]
        public void MalformedPrimitiveTokensCannotBeRepairedByJsonUtilityDefaults()
        {
            string json = JsonUtility.ToJson(Series());
            foreach (string token in new[] { "null", "NaN", "Infinity", "01", "1.", "1e", "true", "\"2\"", "{}" })
                Reject(json.Replace("\"count\":2", "\"count\":" + token));
            Reject(json.Replace("\"screenshot\":false", "\"screenshot\":1"));
            Reject(json + "garbage");
            Reject(json.Substring(0, json.Length - 1) + ",}");
        }

        [Test]
        public void MissingSchemaContextOrUnknownActionAndFieldsAreRejected()
        {
            string json = JsonUtility.ToJson(Series());
            Reject(json.Replace("\"schema\":1,", ""));
            Reject(json.Replace("\"schema\":1", "\"schema\":2"));
            Reject(json.Replace("\"runId\":\"" + Run + "\",", ""));
            Reject(json.Replace("\"action\":\"transferSeries\"", "\"action\":\"executeFile\""));
            Reject(json.Insert(1, "\"filePath\":\"/tmp/anything\","));
            Assert.That(T12DiagnosticCommand.TryRead(json, "another-run", out var command, out _), Is.False);
            Assert.That(command, Is.Null);
        }

        [Test]
        public void CommandIdentifiersCannotSelectPathsOrReservedDiagnosticFiles()
        {
            foreach (string id in new[] { "", "../result", "/absolute", "a/b", "a\\b", ".hidden", "_leading", "a b", "한글", "command", "status", "Command", "STATUS", new string('a', 65) })
            {
                var command = Series(); command.id = id; Reject(JsonUtility.ToJson(command));
            }
            var valid = Series(); valid.id = "A-0_" + new string('z', 60);
            Assert.That(Read(JsonUtility.ToJson(valid)).id.Length, Is.EqualTo(64));
        }

        [Test]
        public void OversizedOrInvalidEscapedInputCannotBecomeACommand()
        {
            string json = JsonUtility.ToJson(Series());
            Reject(new string(' ', 8192) + json);
            Reject(json.Replace(OrbId, "bad\\q"));
            Reject(json.Replace(OrbId, "bad\\u00"));
            Reject(json.Replace(OrbId, "bad\nline"));
        }

        [Test]
        public void GenerateCountIsBoundedAndOtherSingleActionsCannotRepeatImplicitly()
        {
            var command = Series(); command.action = "generate"; command.directions = Array.Empty<int>();
            foreach (int count in new[] { 1, 50 }) { command.count = count; Assert.That(Read(JsonUtility.ToJson(command)).count, Is.EqualTo(count)); }
            foreach (int count in new[] { 0, -1, 51, int.MaxValue }) { command.count = count; Reject(JsonUtility.ToJson(command)); }
            command.action = "capture"; command.count = 2; Reject(JsonUtility.ToJson(command));
        }

        [Test]
        public void SeriesRequiresExactExplicitStartingOwnerCountAndDirectionPlan()
        {
            string json = JsonUtility.ToJson(Series());
            Reject(json.Replace("\"expectedOwner\":0,", ""));
            Reject(json.Replace("\"initialTransferCount\":0,", ""));
            Reject(json.Replace("\"count\":2,", ""));
            var command = Series(); command.directions = new[] { (int)OrbActionKind.TransferLeft }; Reject(JsonUtility.ToJson(command));
            command = Series(); command.expectedOwner = ulong.MaxValue; Reject(JsonUtility.ToJson(command));
            command = Series(); command.directions[1] = (int)OrbActionKind.Launch; Reject(JsonUtility.ToJson(command));
        }

        [Test]
        public void FiftyStepPlanIsAcceptedButArrayOverflowOrMalformedEntriesAreRejected()
        {
            var command = Series(); command.count = 50;
            command.directions = Enumerable.Repeat((int)OrbActionKind.TransferLeft, 50).ToArray();
            Assert.That(Read(JsonUtility.ToJson(command)).directions, Has.Length.EqualTo(50));
            command.directions = Enumerable.Repeat((int)OrbActionKind.TransferLeft, 51).ToArray(); Reject(JsonUtility.ToJson(command));
            string json = JsonUtility.ToJson(Series());
            string plan = "\"directions\":[" + (int)OrbActionKind.TransferLeft + "," + (int)OrbActionKind.TransferRight + "]";
            foreach (string malformed in new[] { "[1,]", "[1.5,2]", "[[1],2]", "[true,2]", "[1 2]" })
                Reject(json.Replace(plan, "\"directions\":" + malformed));
        }

        [Test]
        public void UnsignedCountersRetainTheirFullPrecisionAndCannotOverflowThePlan()
        {
            var command = Series(); command.initialTransferCount = ulong.MaxValue - 2; command.expectedOwner = 9007199254740993UL;
            var read = Read(JsonUtility.ToJson(command));
            Assert.That(read.initialTransferCount, Is.EqualTo(ulong.MaxValue - 2));
            Assert.That(read.expectedOwner, Is.EqualTo(9007199254740993UL));
            command.initialTransferCount++; Reject(JsonUtility.ToJson(command));
            Reject(JsonUtility.ToJson(Series()).Replace("\"expectedOwner\":0", "\"expectedOwner\":-1"));
            Reject(JsonUtility.ToJson(Series()).Replace("\"initialTransferCount\":0", "\"initialTransferCount\":18446744073709551616"));
        }

        [Test]
        public void NormalizedCoordinatesIncludeBothEdgesAndRejectNonfiniteOrOutOfRangeValues()
        {
            foreach (float edge in new[] { 0f, 1f })
            { var command = Series(); command.height = command.x = command.y = edge; Assert.That(Read(JsonUtility.ToJson(command)).height, Is.EqualTo(edge)); }
            foreach (string field in new[] { "height", "x", "y" })
                foreach (string value in new[] { "-0.0001", "1.0001", "1e100", "NaN" })
                {
                    var command = Series(); command.height = command.x = command.y = .5f;
                    Reject(JsonUtility.ToJson(command).Replace("\"" + field + "\":0.5", "\"" + field + "\":" + value));
                }
        }

        [Test]
        public void RejectionAndSpendModifiersCannotChangeUnrelatedCommandMeaning()
        {
            var command = Series(); command.expectRejected = true; Reject(JsonUtility.ToJson(command));
            command = Series(); command.spendBeforeLaunch = true; Reject(JsonUtility.ToJson(command));
            command.action = "launch"; command.count = 1; command.directions = Array.Empty<int>();
            Assert.That(Read(JsonUtility.ToJson(command)).spendBeforeLaunch, Is.True);
            command.expectRejected = true; Reject(JsonUtility.ToJson(command));
            command.spendBeforeLaunch = false; Assert.That(Read(JsonUtility.ToJson(command)).expectRejected, Is.True);
        }

        [Test]
        public void SameOrbCombinationAndEmptyActionOperandsAreRejected()
        {
            var command = Series(); command.action = "combine"; command.count = 1; command.directions = Array.Empty<int>();
            command.otherOrbId = command.orbId; Reject(JsonUtility.ToJson(command));
            command.otherOrbId = ""; Reject(JsonUtility.ToJson(command));
            foreach (string action in new[] { "launch", "transfer", "transferSeries" })
            { command = Series(); command.action = action; command.orbId = ""; Reject(JsonUtility.ToJson(command)); }
            command = Series(); command.action = "transfer"; command.count = 1; command.directions = Array.Empty<int>();
            command.direction = (int)OrbActionKind.Combine; Reject(JsonUtility.ToJson(command));
        }

        [Test]
        public void EquivalentJsonOrderingWhitespaceAndDefaultsProduceTheSamePlanIdentity()
        {
            var first = Read("{\"schema\":1,\"id\":\"c1\",\"runId\":\"" + Run + "\",\"action\":\"capture\"}");
            var second = Read(" { \"action\" : \"capture\", \"runId\":\"" + Run + "\", \"id\":\"c1\",\"schema\":1 } ");
            Assert.That(second.PlanHash, Is.EqualTo(first.PlanHash));
            Assert.That(Read(JsonUtility.ToJson(first)).PlanHash, Is.EqualTo(first.PlanHash));
        }

        [Test]
        public void ChangedCommandOperandsCannotReuseTheOriginalPlanHash()
        {
            var original = Series(); string hash = original.PlanHash;
            Action<T12DiagnosticCommand>[] changes = { c => c.id += "x", c => c.runId += "x", c => c.orbId += "x",
                c => c.expectedOwner++, c => c.initialTransferCount++, c => c.directions[0] = (int)OrbActionKind.TransferRight,
                c => c.height = .25f, c => c.screenshot = true, c => c.action = "transfer" };
            foreach (var change in changes) { var command = Series(); change(command); Assert.That(command.PlanHash, Is.Not.EqualTo(hash)); }
        }

        [Test]
        public void RuntimeOptInRequiresSafeExplicitRunAndRejectsAmbiguousFlags()
        {
            Assert.That(T12DeviceDiagnostics.TryResolveRunId(Array.Empty<string>(), null, out _), Is.False);
            Assert.That(T12DeviceDiagnostics.TryResolveRunId(Array.Empty<string>(), Run, out string environment), Is.True);
            Assert.That(environment, Is.EqualTo(Run));
            Assert.That(T12DeviceDiagnostics.TryResolveRunId(new[] { "app", "-c6T12Run", "argument-run" }, Run, out string argument), Is.True);
            Assert.That(argument, Is.EqualTo("argument-run"));
            Assert.That(T12DeviceDiagnostics.TryResolveRunId(new[] { "app", "-c6T12Run" }, Run, out _), Is.False);
            Assert.That(T12DeviceDiagnostics.TryResolveRunId(new[] { "-c6T12Run", Run, "-c6T12Run", Run }, null, out _), Is.False);
            Assert.That(T12DeviceDiagnostics.TryResolveRunId(new[] { "-c6T12Run", "../escape" }, Run, out _), Is.False);
        }

        [Test]
        public void ActualAckWriterReaderRoundTripAcceptsItsNineFieldsAndPreservesLargeCounters()
        {
            var ack = Ack(); ack.roundId = uint.MaxValue; ack.transferCount = ulong.MaxValue;
            using (var writer = T12DiagnosticProtocol.Write(ack))
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
            {
                Assert.That(T12DiagnosticProtocol.TryRead(reader, out var received), Is.True, "The writer emits nine fields, including schema.");
                Assert.That(JsonUtility.ToJson(received), Is.EqualTo(JsonUtility.ToJson(ack)));
            }
        }

        [Test]
        public void AckRejectsTruncatedFramesBadLengthsTrailingBytesAndInvalidUtf8()
        {
            byte[] bytes = Frame(Encoding.UTF8.GetBytes(JsonUtility.ToJson(Ack())));
            foreach (int length in new[] { 0, 1, 3, 4, bytes.Length - 1 }) RejectAck(bytes.Take(length).ToArray());
            var wrong = (byte[])bytes.Clone(); Array.Copy(BitConverter.GetBytes(-1), wrong, 4); RejectAck(wrong);
            wrong = (byte[])bytes.Clone(); Array.Copy(BitConverter.GetBytes(bytes.Length), wrong, 4); RejectAck(wrong);
            RejectAck(bytes.Concat(new byte[] { 0 }).ToArray());
            RejectAck(Frame(new byte[] { 0xc3, 0x28 }));
            RejectAck(Frame(new byte[T12DiagnosticProtocol.MaximumAckBytes]));
        }

        [Test]
        public void AckRequiresExactlyItsSchemaFieldsAndValidHashes()
        {
            string json = JsonUtility.ToJson(Ack());
            RejectAckJson(json.Replace("\"schema\":1", "\"schema\":2"));
            RejectAckJson(json.Replace("\"roundId\":1", "\"roundId\":0"));
            RejectAckJson(json.Replace("\"roundId\":1", "\"roundId\":4294967296"));
            RejectAckJson(json.Insert(1, "\"extra\":1,"));
            RejectAckJson(json.Replace("\"runId\"", "\"unknown\""));
            RejectAckJson(json.Insert(1, "\"schema\":1,"));
            var ack = Ack(); ack.recordHash = "not-a-hash"; RejectAckJson(JsonUtility.ToJson(ack));
            ack = Ack(); ack.planHash = new string('A', 64); RejectAckJson(JsonUtility.ToJson(ack));
        }

        [Test]
        public void OnlyTheApprovedPeerCanSatisfyAnActiveSeriesAck()
        {
            var command = Series(); var ack = Ack();
            Assert.That(T12DiagnosticProtocol.Matches(ack, command, Session, 1, Peer, Peer), Is.True);
            Assert.That(T12DiagnosticProtocol.Matches(ack, command, Session, 1, 0, Peer), Is.False);
            Assert.That(T12DiagnosticProtocol.Matches(ack, command, Session, 1, 99, Peer), Is.False);
            command.action = "capture";
            Assert.That(T12DiagnosticProtocol.Matches(ack, command, Session, 1, Peer, Peer), Is.False);
        }

        [Test]
        public void StaleOrDifferentRunPlanSessionRoundAndOrbAcksCannotMatch()
        {
            Action<T12ValidationAck>[] changes = { a => a.runId += "x", a => a.commandId += "x", a => a.planHash = new string('b', 64),
                a => a.sessionId += "x", a => a.roundId++, a => a.orbId += "x", a => a.schema++ };
            foreach (var change in changes)
            { var ack = Ack(); change(ack); Assert.That(T12DiagnosticProtocol.Matches(ack, Series(), Session, 1, Peer, Peer), Is.False); }
            Assert.That(T12DiagnosticProtocol.Matches(null, Series(), Session, 1, Peer, Peer), Is.False);
            Assert.That(T12DiagnosticProtocol.Matches(Ack(), null, Session, 1, Peer, Peer), Is.False);
        }

        [Test]
        public void AckCountsMustRemainWithinTheExplicitInitialAndFinalRange()
        {
            var command = Series(); command.initialTransferCount = 10;
            var ack = Ack(); ack.planHash = command.PlanHash;
            foreach (ulong count in new[] { 10UL, 11UL, 12UL })
            { ack.transferCount = count; Assert.That(T12DiagnosticProtocol.Matches(ack, command, Session, 1, Peer, Peer), Is.True); }
            foreach (ulong count in new[] { 9UL, 13UL, ulong.MaxValue })
            { ack.transferCount = count; Assert.That(T12DiagnosticProtocol.Matches(ack, command, Session, 1, Peer, Peer), Is.False); }
        }

        [Test]
        public void AckRecordProofRequiresTheExactObservedTransferEpochAndState()
        {
            var orb = Record(); var ack = Ack(); ack.transferCount = orb.transferCount; ack.recordHash = T12DiagnosticProtocol.RecordHash(orb);
            Assert.That(T12DiagnosticProtocol.SameRecord(ack, orb), Is.True);
            ack.transferCount++; Assert.That(T12DiagnosticProtocol.SameRecord(ack, orb), Is.False);
            ack.transferCount--; ack.recordHash = new string('b', 64); Assert.That(T12DiagnosticProtocol.SameRecord(ack, orb), Is.False);
            Assert.That(T12DiagnosticProtocol.SameRecord(null, orb), Is.False);
            Assert.That(T12DiagnosticProtocol.SameRecord(Ack(), null), Is.False);
        }

        [Test]
        public void OrbRecordHashCoversIdentityOwnerStatePositionAndTransferMetadata()
        {
            string hash = T12DiagnosticProtocol.RecordHash(Record());
            Action<OrbWire>[] changes = { o => o.id += "x", o => o.owner++, o => o.kind++, o => o.polarity++, o => o.state++,
                o => o.pos.x += .01f, o => o.pos.y += .01f, o => o.sequence++, o => o.transferCount++, o => o.lastTransferSequence++, o => o.entrySide++ };
            foreach (var change in changes) { var orb = Record(); change(orb); Assert.That(T12DiagnosticProtocol.RecordHash(orb), Is.Not.EqualTo(hash)); }
        }

        [Test]
        public void RecordAndPlanHashesSurviveActualUnityJsonRoundTripsAndCultureChanges()
        {
            var orb = Record(); orb.sequence = ulong.MaxValue; orb.lastTransferSequence = ulong.MaxValue - 1;
            var command = Series(); command.expectedOwner = 9007199254740993UL; command.height = .44718465f;
            string recordHash = T12DiagnosticProtocol.RecordHash(orb), planHash = command.PlanHash;
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                for (int i = 0; i < 8; i++)
                {
                    orb = JsonUtility.FromJson<OrbWire>(JsonUtility.ToJson(orb)); command = Read(JsonUtility.ToJson(command));
                    Assert.That(T12DiagnosticProtocol.RecordHash(orb), Is.EqualTo(recordHash));
                    Assert.That(command.PlanHash, Is.EqualTo(planHash));
                }
            }
            finally { CultureInfo.CurrentCulture = culture; }
        }

        private static T12DiagnosticCommand Series() => new T12DiagnosticCommand { id = "raw-series-01", runId = Run, action = "transferSeries",
            orbId = OrbId, expectedOwner = 0, initialTransferCount = 0, count = 2, direction = (int)OrbActionKind.TransferLeft,
            directions = new[] { (int)OrbActionKind.TransferLeft, (int)OrbActionKind.TransferRight }, height = .5f };
        private static OrbWire Record() => new OrbWire { id = OrbId, owner = Peer, kind = (int)OrbKind.Raw, polarity = (int)OrbPolarity.Yin,
            state = (int)OrbAuthorityState.Idle, pos = new Vector2(.945f, .44718465f), sequence = 9, transferCount = 3, lastTransferSequence = 9, entrySide = (int)EntrySide.Right };
        private static T12ValidationAck Ack() => new T12ValidationAck { runId = Run, commandId = Series().id, planHash = Series().PlanHash,
            sessionId = Session, roundId = 1, orbId = OrbId, transferCount = 0, recordHash = T12DiagnosticProtocol.RecordHash(Record()) };
        private static T12DiagnosticCommand Read(string json)
        { Assert.That(T12DiagnosticCommand.TryRead(json, Run, out var command, out string reason), Is.True, reason + " / " + json); Assert.That(command, Is.Not.Null); return command; }
        private static void Reject(string json)
        { Assert.That(T12DiagnosticCommand.TryRead(json, Run, out var command, out _), Is.False, json); Assert.That(command, Is.Null); }
        private static byte[] Frame(byte[] payload)
        { var result = new byte[4 + payload.Length]; Array.Copy(BitConverter.GetBytes(payload.Length), result, 4); Array.Copy(payload, 0, result, 4, payload.Length); return result; }
        private static void RejectAck(byte[] bytes)
        { using (var reader = new FastBufferReader(bytes, Allocator.Temp)) { Assert.That(T12DiagnosticProtocol.TryRead(reader, out var ack), Is.False); Assert.That(ack, Is.Null); } }
        private static void RejectAckJson(string json) => RejectAck(Frame(Encoding.UTF8.GetBytes(json)));
    }
}
