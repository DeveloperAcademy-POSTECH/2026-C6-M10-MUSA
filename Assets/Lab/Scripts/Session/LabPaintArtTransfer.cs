using System;
using System.Security.Cryptography;

namespace C6Lab
{
    /// <summary>One low-priority, independently bounded journal page. Existing paint frames stay byte-compatible.</summary>
    [Serializable]
    public sealed class LabPaintArtPage
    {
        public string epoch, style, hash, data;
        public int columns, rows, index, rawBytes, encoding;
    }

    [Serializable]
    public sealed class LabPaintArtReceipt
    {
        public string epoch, style, error;
        public int receivedPages;
        // These are separate from Bulk's datagram acknowledgement and from each other.
        public ulong appliedThrough, renderedThrough;
    }

    public static class LabPaintArtTransfer
    {
        public const int MaximumRawPageBytes = 4096;
        public const int MaximumEncodedCharacters = 6000;
        public const int BackgroundBacklogBytes = 8192;

        public static LabPaintArtPage Encode(string epoch, string style, int columns, int rows, int index, byte[] raw)
        {
            if (string.IsNullOrEmpty(epoch) || string.IsNullOrEmpty(style) || index < 0 || raw == null
                || raw.Length < 1 || raw.Length > MaximumRawPageBytes) throw new ArgumentException("Invalid art page.");
            byte[] compressed = LabPaintPayloadCodec.Compress(raw);
            bool useCompressed = compressed.Length < raw.Length;
            return new LabPaintArtPage { epoch = epoch, style = style, columns = columns, rows = rows,
                index = index, rawBytes = raw.Length, hash = Hash(raw), encoding = useCompressed ? 1 : 0,
                data = Convert.ToBase64String(useCompressed ? compressed : raw) };
        }

        public static bool TryDecode(LabPaintArtPage page, out byte[] raw)
        {
            raw = null;
            if (page == null || string.IsNullOrEmpty(page.epoch) || page.epoch.Length > 128
                || string.IsNullOrEmpty(page.style) || page.style.Length > 256 || page.index < 0
                || page.index >= LabPaintArtJournal.DefaultMaximumBytes / 96 || page.columns < 1 || page.rows < 1
                || (long)page.columns * page.rows > 32768 || page.rawBytes < 1 || page.rawBytes > MaximumRawPageBytes
                || page.hash == null || page.hash.Length != 64 || string.IsNullOrEmpty(page.data)
                || page.data.Length > MaximumEncodedCharacters || page.encoding < 0 || page.encoding > 1) return false;
            try
            {
                byte[] encoded = Convert.FromBase64String(page.data);
                if (encoded.Length > MaximumRawPageBytes) return false;
                if (page.encoding == LabPaintPayloadCodec.Deflate)
                {
                    byte[] buffer = null;
                    if (!LabPaintPayloadCodec.TryInflate(encoded, encoded.Length, page.rawBytes, ref buffer, out int length)
                        || length != page.rawBytes) return false;
                    raw = new byte[length]; Buffer.BlockCopy(buffer, 0, raw, 0, length);
                }
                else
                {
                    if (encoded.Length != page.rawBytes) return false;
                    raw = encoded;
                }
                if (!string.Equals(Hash(raw), page.hash, StringComparison.Ordinal)) { raw = null; return false; }
                return true;
            }
            catch (FormatException) { return false; }
        }
        public static string Hash(byte[] raw)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(raw)).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>Stop-and-wait at page level, independent of the Bulk fragment window.</summary>
    public sealed class LabPaintArtSendCursor
    {
        public int ReceivedPages { get; private set; }
        public int PendingPage { get; private set; } = -1;
        long pendingBulkThrough;
        double pendingSentAt;
        public ulong AppliedThrough { get; private set; }
        public ulong RenderedThrough { get; private set; }
        public bool CanQueue(int pageCount, int bulkBytes) => PendingPage < 0 && ReceivedPages < pageCount
            && bulkBytes < LabPaintArtTransfer.BackgroundBacklogBytes;
        public bool CanRetry(long bulkAcknowledged, double now, int bulkBytes)
            => PendingPage >= 0 && bulkAcknowledged >= pendingBulkThrough && now - pendingSentAt >= 1d
                && bulkBytes < LabPaintArtTransfer.BackgroundBacklogBytes;
        public void MarkTransmission(long through, double now)
        { pendingBulkThrough = through; pendingSentAt = now; }
        public bool Queued(int index)
        {
            if (PendingPage >= 0 || index != ReceivedPages) return false;
            PendingPage = index; return true;
        }
        public bool Receive(LabPaintArtReceipt receipt, int publishedPages, ulong lastSequence)
        {
            if (receipt == null || receipt.receivedPages < ReceivedPages || receipt.receivedPages > publishedPages
                || receipt.receivedPages > ReceivedPages + (PendingPage >= 0 ? 1 : 0)
                || receipt.appliedThrough > lastSequence
                || receipt.renderedThrough > receipt.appliedThrough) return false;
            ReceivedPages = receipt.receivedPages;
            if (PendingPage >= 0 && ReceivedPages == PendingPage + 1) PendingPage = -1;
            AppliedThrough = receipt.appliedThrough; RenderedThrough = receipt.renderedThrough;
            return true;
        }
    }
}
