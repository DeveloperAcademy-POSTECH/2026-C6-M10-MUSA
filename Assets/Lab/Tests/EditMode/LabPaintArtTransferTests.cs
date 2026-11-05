using System;
using NUnit.Framework;

namespace C6Lab.Tests
{
    public sealed class LabPaintArtTransferTests
    {
        [Test]
        public void RawAndDeflatedPagesRoundTripWithoutChangingExistingPaintCodec()
        {
            foreach (bool compressible in new[] { false, true })
            {
                var raw = new byte[4096];
                if (!compressible) new Random(533).NextBytes(raw);
                var page = LabPaintArtTransfer.Encode("round/1", "brush-v1", 64, 32, 0, raw);
                Assert.That(page.encoding, Is.EqualTo(compressible ? 1 : 0));
                Assert.That(LabPaintArtTransfer.TryDecode(page, out var copy), Is.True);
                Assert.That(copy, Is.EqualTo(raw));
                page.hash = new string('0', 64);
                Assert.That(LabPaintArtTransfer.TryDecode(page, out _), Is.False);
            }
        }
        [Test]
        public void InvalidOrOverinflatedPageIsRejectedBeforeJournalImport()
        {
            var page = LabPaintArtTransfer.Encode("round/1", "v1", 64, 32, 0, new byte[4096]);
            page.rawBytes = 4095;
            Assert.That(LabPaintArtTransfer.TryDecode(page, out _), Is.False);
            page.rawBytes = 4097;
            Assert.That(LabPaintArtTransfer.TryDecode(page, out _), Is.False);
            page.rawBytes = 4096; page.encoding = 2;
            Assert.That(LabPaintArtTransfer.TryDecode(page, out _), Is.False);
        }
        [Test]
        public void ApplicationIgnoredPageRetriesOnlyAfterBulkCompletionAndOneSecondWithoutQueuingHistory()
        {
            var sender = new LabPaintArtSendCursor();
            Assert.That(sender.Queued(0), Is.True);
            sender.MarkTransmission(12, 10d);
            Assert.That(sender.CanRetry(11, 12d, 0), Is.False, "Bulk still owns recovery of these bytes.");
            Assert.That(sender.CanRetry(12, 10.9d, 0), Is.False);
            Assert.That(sender.CanRetry(12, 11d, 0), Is.True);
            Assert.That(sender.CanRetry(12, 12d, 8192), Is.False);
            Assert.That(sender.PendingPage, Is.Zero);
            Assert.That(sender.CanQueue(10000, 0), Is.False);
            Assert.That(sender.Receive(new LabPaintArtReceipt { receivedPages=1, appliedThrough=20, renderedThrough=19 },10000,90),Is.True);
            Assert.That(sender.Receive(new LabPaintArtReceipt { receivedPages=1, appliedThrough=0, renderedThrough=0 },10000,90),Is.True,
                "Recreating GPU resources may rewind rendering while retaining all received pages.");
            Assert.That(sender.CanQueue(10000,0),Is.True);
        }

        [Test]
        public void OnlyOnePageWaitsForApplicationReceiptAndGameplayBacklogTakesPriority()
        {
            var sender = new LabPaintArtSendCursor();
            Assert.That(sender.CanQueue(10000, 8192), Is.False);
            Assert.That(sender.CanQueue(10000, 0), Is.True);
            Assert.That(sender.Queued(0), Is.True);
            Assert.That(sender.CanQueue(10000, 0), Is.False);
            Assert.That(sender.Queued(1), Is.False);
            Assert.That(sender.Receive(new LabPaintArtReceipt { receivedPages = 2 }, 10000, 90), Is.False);
            Assert.That(sender.Receive(new LabPaintArtReceipt { receivedPages = 1, appliedThrough = 20, renderedThrough = 21 }, 10000, 90), Is.False);
            Assert.That(sender.Receive(new LabPaintArtReceipt { receivedPages = 1, appliedThrough = 20, renderedThrough = 19 }, 10000, 90), Is.True);
            Assert.That(sender.CanQueue(10000, 0), Is.True);
            Assert.That(sender.AppliedThrough, Is.EqualTo(20));
            Assert.That(sender.RenderedThrough, Is.EqualTo(19));
        }
    }
}
