using System;
using System.Linq;
using NUnit.Framework;

namespace C6Lab.Tests
{
    public sealed class LabBulkChannelTests
    {
        [Test]
        public void LostFirstChunkDoesNotApplyLaterChunksUntilRetryAndDuplicatesNeverReapply()
        {
            var sender = new LabBulkSender();
            var receiver = new LabBulkReceiver();
            Assert.That(sender.Enqueue("first", 0d), Is.True);
            Assert.That(sender.Enqueue("second", 0d), Is.True);
            Assert.That(sender.Enqueue("third", 0d), Is.True);
            LabBulkFrame[] sent = sender.GetDue(0d, 1000);
            Assert.That(receiver.Receive(sent[2].sequence, sent[2].payload), Is.Empty);
            Assert.That(receiver.Receive(sent[1].sequence, sent[1].payload), Is.Empty);
            Assert.That(receiver.AckThrough, Is.Zero);
            LabBulkFrame firstRetry = sender.GetDue(.25d, 1000).First();
            Assert.That(receiver.Receive(firstRetry.sequence, firstRetry.payload),
                Is.EqualTo(new[] { "first", "second", "third" }));
            Assert.That(receiver.AckThrough, Is.EqualTo(3));
            Assert.That(receiver.Receive(2, "second"), Is.Empty);
            Assert.That(sender.Ack(receiver.AckThrough), Is.True);
            Assert.That(sender.Count, Is.Zero);
            Assert.That(sender.QueuedBytes, Is.Zero);
        }

        [Test]
        public void CumulativeAckOpensWindowAndCannotRemoveUnsentOrFutureChunks()
        {
            var sender = new LabBulkSender(windowSize: 2);
            for (int i = 0; i < 4; i++) Assert.That(sender.Enqueue("chunk" + i, 0d), Is.True);
            Assert.That(sender.Ack(1), Is.False);
            Assert.That(sender.GetDue(0d, 1000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(sender.GetDue(.1d, 1000), Is.Empty);
            Assert.That(sender.Ack(3), Is.False);
            Assert.That(sender.Ack(1), Is.True);
            Assert.That(sender.Count, Is.EqualTo(3));
            Assert.That(sender.GetDue(.1d, 1000).Select(frame => frame.sequence), Is.EqualTo(new long[] { 3 }));
            Assert.That(sender.Ack(3), Is.True);
            Assert.That(sender.Count, Is.EqualTo(1));
            Assert.That(sender.Ack(2), Is.False);
            Assert.That(sender.GetDue(.1d, 1000).Single().sequence, Is.EqualTo(4));
        }

        [Test]
        public void RetryBackoffStartsAtQuarterSecondAndCapsAtOneSecondWithoutEviction()
        {
            var sender = new LabBulkSender();
            sender.Enqueue("retain", 0d);
            Assert.That(sender.GetDue(0d, 100).Length, Is.EqualTo(1));
            Assert.That(sender.GetDue(.249d, 100), Is.Empty);
            Assert.That(sender.GetDue(.25d, 100).Length, Is.EqualTo(1));
            Assert.That(sender.GetDue(.749d, 100), Is.Empty);
            Assert.That(sender.GetDue(.75d, 100).Length, Is.EqualTo(1));
            Assert.That(sender.GetDue(1.749d, 100), Is.Empty);
            Assert.That(sender.GetDue(1.75d, 100).Length, Is.EqualTo(1));
            Assert.That(sender.GetDue(2.749d, 100), Is.Empty);
            Assert.That(sender.GetDue(2.75d, 100).Length, Is.EqualTo(1));
            Assert.That(sender.GetDue(100d, 100).Single().payload, Is.EqualTo("retain"));
            Assert.That(sender.Count, Is.EqualTo(1));
        }

        [Test]
        public void StrictPayloadBudgetDoesNotAdvanceSendStateWhenFirstChunkDoesNotFit()
        {
            var sender = new LabBulkSender();
            sender.Enqueue(new string('a', 100), 0d);
            sender.Enqueue(new string('b', 100), 0d);
            Assert.That(sender.GetDue(0d, 99), Is.Empty);
            Assert.That(sender.Ack(1), Is.False);
            LabBulkFrame[] first = sender.GetDue(0d, 150);
            Assert.That(first.Length, Is.EqualTo(1));
            Assert.That(first.Single().Utf8Bytes, Is.EqualTo(100));
            Assert.That(sender.GetDue(0d, 100).Single().sequence, Is.EqualTo(2));
            Assert.That(sender.QueuedBytes, Is.EqualTo(200));
        }

        [Test]
        public void PayloadAndQueueLimitsUseUtf8BytesRatherThanCharacterCount()
        {
            var sender = new LabBulkSender();
            Assert.That(sender.Enqueue(new string('한', 1666), 0d), Is.True);
            Assert.That(sender.QueuedBytes, Is.EqualTo(4998));
            Assert.That(sender.Enqueue(new string('한', 1667), 0d), Is.False);
            Assert.That(sender.Enqueue(new string('a', LabBulkSender.MaximumPayloadBytes + 1), 0d), Is.False);
            Assert.That(sender.GetDue(0d, 4997), Is.Empty);
            Assert.That(sender.GetDue(0d, 4998).Single().Utf8Bytes, Is.EqualTo(4998));
        }

        [Test]
        public void FullQueueRejectsNewChunkWithoutDroppingOldDataOrConsumingASequence()
        {
            var sender = new LabBulkSender(maximumQueuedBytes: 10);
            Assert.That(sender.Enqueue("123456", 0d, out var first), Is.True);
            Assert.That(sender.Enqueue("12345", 0d, out var refused), Is.False);
            Assert.That(refused, Is.Zero);
            Assert.That(sender.Enqueue("7890", 0d, out var second), Is.True);
            Assert.That(first, Is.EqualTo(1));
            Assert.That(second, Is.EqualTo(2));
            Assert.That(sender.Count, Is.EqualTo(2));
            Assert.That(sender.QueuedBytes, Is.EqualTo(10));
            Assert.That(sender.GetDue(0d, 10).Select(frame => frame.payload), Is.EqualTo(new[] { "123456", "7890" }));
            Assert.That(sender.Ack(1), Is.True);
            Assert.That(sender.QueuedBytes, Is.EqualTo(4));
            Assert.That(sender.Enqueue("abcdef", .1d), Is.True);
        }

        [Test]
        public void ReceiverBuffersOnlyBoundedFutureWindowAndDrainsContiguousOrder()
        {
            var receiver = new LabBulkReceiver(windowSize: 2);
            Assert.That(receiver.Receive(4, "fourth", out var ready), Is.True);
            Assert.That(ready, Is.Empty);
            Assert.That(receiver.Receive(5, "too-far", out _), Is.False);
            Assert.That(receiver.BufferedCount, Is.EqualTo(1));
            Assert.That(receiver.Receive(1, "first"), Is.EqualTo(new[] { "first" }));
            Assert.That(receiver.Receive(2, "second"), Is.EqualTo(new[] { "second" }));
            Assert.That(receiver.Receive(3, "third"), Is.EqualTo(new[] { "third", "fourth" }));
            Assert.That(receiver.AckThrough, Is.EqualTo(4));
            Assert.That(receiver.BufferedCount, Is.Zero);
        }

        [Test]
        public void ConflictingBufferedDuplicateDoesNotReplaceTheOriginalChunk()
        {
            var receiver = new LabBulkReceiver();
            Assert.That(receiver.Receive(2, "original", out _), Is.True);
            Assert.That(receiver.Receive(2, "changed", out _), Is.False);
            Assert.That(receiver.Receive(2, "original", out var duplicate), Is.True);
            Assert.That(duplicate, Is.Empty);
            Assert.That(receiver.Receive(1, "first"), Is.EqualTo(new[] { "first", "original" }));
        }

        [Test]
        public void ClearResetsBothConnectionStreamsAndAccounting()
        {
            var sender = new LabBulkSender();
            var receiver = new LabBulkReceiver();
            sender.Enqueue("old", 0d);
            sender.GetDue(0d, 100);
            receiver.Receive(2, "buffered-old");
            sender.Clear(); receiver.Clear();
            Assert.That(sender.Count, Is.Zero);
            Assert.That(sender.QueuedBytes, Is.Zero);
            Assert.That(receiver.AckThrough, Is.Zero);
            Assert.That(receiver.BufferedCount, Is.Zero);
            Assert.That(sender.Enqueue("new", 0d, out var sequence), Is.True);
            Assert.That(sequence, Is.EqualTo(1));
            Assert.That(receiver.Receive(sequence, "new"), Is.EqualTo(new[] { "new" }));
        }

        [Test]
        public void MaximumLogicalMessageSplitsIntoMtuBoundedFragmentsAndReassemblesExactly()
        {
            string encoded = Convert.ToBase64String(new byte[LabBulkMessageChunks.MaximumEncodedCharacters / 4 * 3]);
            string[] fragments = LabBulkMessageChunks.Split(encoded);
            Assert.That(fragments.Length, Is.LessThanOrEqualTo(LabBulkMessageChunks.MaximumParts));
            Assert.That(fragments.All(fragment => fragment.Length <= LabBulkMessageChunks.MaximumFragmentCharacters), Is.True);
            var assembler = new LabBulkMessageAssembler();
            for (int i = 0; i < fragments.Length; i++)
            {
                bool complete = assembler.Receive(fragments[i], out var actual);
                Assert.That(complete, Is.EqualTo(i == fragments.Length - 1));
                if (complete) Assert.That(actual, Is.EqualTo(encoded));
                else Assert.That(actual, Is.Null);
            }
            Assert.That(assembler.HasPending, Is.False);
        }

        [Test]
        public void LogicalMessageSurvivesWireLossReorderDuplicatesAndCumulativeAck()
        {
            string encoded = Convert.ToBase64String(new byte[5000]);
            var sender = new LabBulkSender();
            var receiver = new LabBulkReceiver();
            var assembler = new LabBulkMessageAssembler();
            foreach (string fragment in LabBulkMessageChunks.Split(encoded)) sender.Enqueue(fragment, 0d);
            LabBulkFrame[] frames = sender.GetDue(0d, 20000);
            foreach (LabBulkFrame frame in frames.Skip(1).Reverse())
                Assert.That(receiver.Receive(frame.sequence, frame.payload), Is.Empty);
            LabBulkFrame retry = sender.GetDue(.25d, 20000).First();
            string completed = null;
            int completions = 0;
            foreach (string ready in receiver.Receive(retry.sequence, retry.payload))
                if (assembler.Receive(ready, out var logical)) { completed = logical; completions++; }
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(completed, Is.EqualTo(encoded));
            Assert.That(receiver.Receive(frames[1].sequence, frames[1].payload), Is.Empty);
            Assert.That(sender.Ack(receiver.AckThrough), Is.True);
            Assert.That(sender.Count, Is.Zero);
        }

        [Test]
        public void LogicalAssemblerRejectsMissingPartAndCanStartAFreshCompleteMessage()
        {
            string encoded = Convert.ToBase64String(new byte[2000]);
            string[] fragments = LabBulkMessageChunks.Split(encoded);
            var assembler = new LabBulkMessageAssembler();
            Assert.That(assembler.Receive(fragments[0], out _), Is.False);
            Assert.That(assembler.HasPending, Is.True);
            Assert.That(assembler.Receive(fragments[2], out _), Is.False);
            Assert.That(assembler.HasPending, Is.False);
            Assert.That(assembler.Receive(fragments[1], out _), Is.False);
            int completed = 0;
            foreach (string fragment in fragments) if (assembler.Receive(fragment, out _)) completed++;
            Assert.That(completed, Is.EqualTo(1));
            assembler.Receive(fragments[0], out _);
            assembler.Clear();
            Assert.That(assembler.Receive(fragments[1], out _), Is.False);
        }

        [Test]
        public void LogicalMessageRejectsInvalidEncodingCountsPrefixesAndOversizedFragments()
        {
            Assert.Throws<ArgumentException>(() => LabBulkMessageChunks.Split(null));
            Assert.Throws<ArgumentException>(() => LabBulkMessageChunks.Split(string.Empty));
            Assert.Throws<ArgumentException>(() => LabBulkMessageChunks.Split("not-base64"));
            Assert.Throws<ArgumentException>(() => LabBulkMessageChunks.Split(new string('A', LabBulkMessageChunks.MaximumEncodedCharacters + 4)));
            var assembler = new LabBulkMessageAssembler();
            foreach (string invalid in new[] { "0|0|AAAA", "0|65|AAAA", "-1|1|AAAA", "0|1|!AAA", "0|1|AAA",
                "0|1|", "no-prefix", "0|1|" + new string('A', LabBulkMessageChunks.DataCharacters + 1) })
            {
                Assert.That(assembler.Receive(invalid, out var output), Is.False, invalid);
                Assert.That(output, Is.Null);
                Assert.That(assembler.HasPending, Is.False);
            }
        }

        [Test]
        public void InvalidInputsDoNotAllocateQueueSpaceAdvanceAckOrProduceReadyData()
        {
            var sender = new LabBulkSender();
            var receiver = new LabBulkReceiver();
            Assert.That(sender.Enqueue(null, 0d), Is.False);
            Assert.That(sender.Enqueue(string.Empty, 0d), Is.False);
            Assert.That(sender.Enqueue("valid", double.NaN), Is.False);
            Assert.That(sender.Enqueue("\uD800", 0d), Is.False);
            Assert.That(sender.Count, Is.Zero);
            Assert.That(receiver.Receive(0, "invalid", out _), Is.False);
            Assert.That(receiver.Receive(1, "\uD800", out _), Is.False);
            Assert.That(receiver.Receive(1, new string('a', 5001), out _), Is.False);
            Assert.That(receiver.AckThrough, Is.Zero);
            Assert.That(receiver.BufferedCount, Is.Zero);
            sender.Enqueue("valid", 0d);
            Assert.That(sender.GetDue(double.PositiveInfinity, 100), Is.Empty);
            Assert.That(sender.GetDue(0d, -1), Is.Empty);
        }
    }
}
