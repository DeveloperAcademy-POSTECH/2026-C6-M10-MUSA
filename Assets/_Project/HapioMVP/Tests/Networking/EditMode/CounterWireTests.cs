using System;
using C6.Prototype.Networking.Counter;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;

namespace C6.Prototype.Networking.Tests
{
    public sealed class CounterWireTests
    {
        private static readonly Guid Session = Guid.Parse("38543c7b-30df-4eea-8a4e-1c43920bd5e1");
        private static readonly Guid Nonce = Guid.Parse("f87b8d1a-5d52-43ce-8c0d-e2c0a9c55f64");

        [Test]
        public void FullSnapshotAndReceiptPreserveAllAuthorityFields()
        {
            var snapshot = new CounterSnapshot(Session.ToString("D"), 42, 44, 42, 2);
            using (var writer = CounterWire.CreateWriter())
            {
                CounterWire.WriteSnapshot(writer, Nonce, snapshot);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    Assert.That(CounterWire.TryReadSnapshot(reader, out var actualNonce, out var actual), Is.True);
                    Assert.That(actualNonce, Is.EqualTo(Nonce));
                    AssertSnapshot(actual, snapshot);
                }
            }
            // An original receipt may deliberately hold an older snapshot after a replay.
            var receipt = new CounterReceipt(false, true, "invalid-request-id", 2, 0, snapshot);
            using (var writer = CounterWire.CreateWriter())
            {
                CounterWire.WriteReceipt(writer, Nonce, receipt);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    Assert.That(CounterWire.TryReadReceipt(reader, out var actualNonce, out var actual), Is.True);
                    Assert.That(actualNonce, Is.EqualTo(Nonce));
                    Assert.That(actual.Approved, Is.False);
                    Assert.That(actual.Duplicate, Is.True);
                    Assert.That(actual.Reason, Is.EqualTo("invalid-request-id"));
                    Assert.That(actual.SenderId, Is.EqualTo(2));
                    Assert.That(actual.RequestId, Is.Zero);
                    AssertSnapshot(actual.Snapshot, snapshot);
                }
            }
        }

        [TestCase(0UL)]
        [TestCase(ulong.MaxValue)]
        public void RequestPreservesSessionAndWholeUnsignedRequestId(ulong requestId)
        {
            using (var writer = CounterWire.CreateWriter())
            {
                CounterWire.WriteRequest(writer, Session.ToString("D"), requestId);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    Assert.That(CounterWire.TryReadRequest(reader, out var actualSession, out var actualId), Is.True);
                    Assert.That(actualSession, Is.EqualTo(Session.ToString("D")));
                    Assert.That(actualId, Is.EqualTo(requestId));
                }
            }
        }

        [Test]
        public void EveryTruncatedPacketIsRejectedWithoutThrowing()
        {
            foreach (var kind in new[] { "nonce", "request", "snapshot", "receipt" })
            {
                var bytes = ValidPacket(kind);
                for (var length = 0; length < bytes.Length; ++length)
                {
                    var truncated = new byte[length];
                    Array.Copy(bytes, truncated, length);
                    Assert.That(Decode(kind, truncated), Is.False, kind + " length=" + length);
                }
            }
        }

        [Test]
        public void TrailingDataWrongVersionAndOversizedPacketsAreRejected()
        {
            foreach (var kind in new[] { "nonce", "request", "snapshot", "receipt" })
            {
                var valid = ValidPacket(kind);
                var extended = new byte[valid.Length + 1];
                Array.Copy(valid, extended, valid.Length);
                Assert.That(Decode(kind, extended), Is.False, kind + " extra byte");
                valid[0] = 255;
                Assert.That(Decode(kind, valid), Is.False, kind + " protocol version");
                Assert.That(Decode(kind, new byte[193]), Is.False, kind + " oversized");
            }
        }

        [TestCase(-1L, 1UL, 0UL, 1UL)]
        [TestCase(1L, 1UL, 0UL, 1UL)]
        [TestCase(1L, 3UL, 1UL, 1UL)]
        [TestCase(1L, 0UL, 1UL, ulong.MaxValue)]
        public void InconsistentOrOverflowingStateIsRejected(long value, ulong revision, ulong approved, ulong rejected)
        {
            using (var writer = CounterWire.CreateWriter())
            {
                CounterWire.WriteSnapshot(writer, Nonce, new CounterSnapshot(Session.ToString("D"), value, revision, approved, rejected));
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                    Assert.That(CounterWire.TryReadSnapshot(reader, out _, out _), Is.False);
            }
        }

        [Test]
        public void InvalidReceiptFlagsAndUnboundedReasonAreRejected()
        {
            var flags = ValidPacket("receipt");
            flags[81] = 4;
            Assert.That(Decode("receipt", flags), Is.False);
            var reason = ValidPacket("receipt");
            reason[82] = 255;
            Assert.That(Decode("receipt", reason), Is.False);
            var control = ValidPacket("receipt");
            control[83] = 1;
            Assert.That(Decode("receipt", control), Is.False);
        }

        [Test]
        public void EmptyBootstrapNonceAndSnapshotSessionAreRejected()
        {
            var nonce = ValidPacket("nonce");
            Array.Clear(nonce, 1, 16);
            Assert.That(Decode("nonce", nonce), Is.False);
            var snapshot = ValidPacket("snapshot");
            Array.Clear(snapshot, 17, 16);
            Assert.That(Decode("snapshot", snapshot), Is.False);
        }

        private static byte[] ValidPacket(string kind)
        {
            var snapshot = new CounterSnapshot(Session.ToString("D"), 3, 3, 3, 0);
            using (var writer = CounterWire.CreateWriter())
            {
                switch (kind)
                {
                    case "nonce": CounterWire.WriteNonce(writer, Nonce); break;
                    case "request": CounterWire.WriteRequest(writer, Session.ToString("D"), 1); break;
                    case "snapshot": CounterWire.WriteSnapshot(writer, Nonce, snapshot); break;
                    case "receipt": CounterWire.WriteReceipt(writer, Nonce, new CounterReceipt(true, false, "approved", 1, 1, snapshot)); break;
                    default: throw new ArgumentException(nameof(kind));
                }
                return writer.ToArray();
            }
        }

        private static bool Decode(string kind, byte[] bytes)
        {
            using (var reader = new FastBufferReader(bytes, Allocator.Temp))
            {
                switch (kind)
                {
                    case "nonce": return CounterWire.TryReadNonce(reader, out _);
                    case "request": return CounterWire.TryReadRequest(reader, out _, out _);
                    case "snapshot": return CounterWire.TryReadSnapshot(reader, out _, out _);
                    case "receipt": return CounterWire.TryReadReceipt(reader, out _, out _);
                    default: throw new ArgumentException(nameof(kind));
                }
            }
        }

        private static void AssertSnapshot(CounterSnapshot actual, CounterSnapshot expected)
        {
            Assert.That(actual.SessionId, Is.EqualTo(expected.SessionId));
            Assert.That(actual.Value, Is.EqualTo(expected.Value));
            Assert.That(actual.Revision, Is.EqualTo(expected.Revision));
            Assert.That(actual.ApprovedCount, Is.EqualTo(expected.ApprovedCount));
            Assert.That(actual.RejectedCount, Is.EqualTo(expected.RejectedCount));
        }
    }
}
