using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace C6Lab
{
    /// <summary>One bounded application-level bulk chunk; transport/session identity lives in its envelope.</summary>
    [Serializable]
    public sealed class LabBulkFrame
    {
        public long sequence;
        public string payload;
        public int Utf8Bytes { get; private set; }

        internal LabBulkFrame(long sequence, string payload, int bytes)
        {
            this.sequence = sequence;
            this.payload = payload;
            Utf8Bytes = bytes;
        }
    }

    /// <summary>
    /// Reliable ordered bulk delivery above an unreliable transport pipeline. A lost paint chunk
    /// blocks only this channel, never NGO's separate reliable control/action stream. Unacknowledged
    /// chunks are retained until acknowledged or explicitly cleared on connection shutdown.
    /// </summary>
    public sealed class LabBulkSender
    {
        public const int MaximumPayloadBytes = 5000;
        public const int DefaultMaximumQueuedBytes = 4 * 1024 * 1024;
        public const int DefaultWindowSize = 32;
        public const double InitialRetrySeconds = .25d;
        public const double MaximumRetrySeconds = 1d;
        private sealed class Pending
        {
            public long sequence;
            public string payload;
            public int bytes;
            public int attempts;
            public double dueAt;
        }

        private readonly LinkedList<Pending> pending = new LinkedList<Pending>();
        private readonly int maximumQueuedBytes;
        private readonly int windowSize;
        private long lastEnqueued;
        private long highestSent;
        private long acknowledged;
        public int Count => pending.Count;
        public int QueuedBytes { get; private set; }

        public LabBulkSender(int maximumQueuedBytes = DefaultMaximumQueuedBytes, int windowSize = DefaultWindowSize)
        {
            if (maximumQueuedBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumQueuedBytes));
            if (windowSize <= 0 || windowSize > 4096) throw new ArgumentOutOfRangeException(nameof(windowSize));
            this.maximumQueuedBytes = maximumQueuedBytes;
            this.windowSize = windowSize;
        }

        public bool Enqueue(string payload, double now) => Enqueue(payload, now, out _);

        public bool Enqueue(string payload, double now, out long sequence)
        {
            sequence = 0;
            if (!ValidTime(now) || lastEnqueued == long.MaxValue || !LabBulkPayloadEncoding.TrySize(payload, out int bytes)
                || bytes > maximumQueuedBytes - QueuedBytes) return false;
            sequence = ++lastEnqueued;
            pending.AddLast(new Pending { sequence = sequence, payload = payload, bytes = bytes, dueAt = now });
            QueuedBytes += bytes;
            return true;
        }

        /// <summary>
        /// The payload byte budget is strict. If a due chunk does not fit, it remains queued and
        /// unsent; callers must eventually offer a budget at least as large as one maximum chunk.
        /// </summary>
        public LabBulkFrame[] GetDue(double now, int byteBudget)
        {
            if (!ValidTime(now) || byteBudget <= 0 || pending.Count == 0) return Array.Empty<LabBulkFrame>();
            var result = new List<LabBulkFrame>();
            int eligible = 0;
            foreach (Pending entry in pending)
            {
                if (++eligible > windowSize) break;
                if (now < entry.dueAt) continue;
                if (entry.bytes > byteBudget) break;
                byteBudget -= entry.bytes;
                result.Add(new LabBulkFrame(entry.sequence, entry.payload, entry.bytes));
                highestSent = Math.Max(highestSent, entry.sequence);
                // Backoff saturates after three sends; no unbounded exponent or attempts overflow.
                entry.attempts = Math.Min(3, entry.attempts + 1);
                entry.dueAt = now + Math.Min(MaximumRetrySeconds,
                    InitialRetrySeconds * (1 << (entry.attempts - 1)));
            }
            return result.ToArray();
        }

        /// <summary>A cumulative acknowledgement cannot discard chunks that have never been sent.</summary>
        public bool Ack(long highestContiguous)
        {
            if (highestContiguous <= acknowledged || highestContiguous > highestSent) return false;
            acknowledged = highestContiguous;
            while (pending.First != null && pending.First.Value.sequence <= highestContiguous)
            {
                QueuedBytes -= pending.First.Value.bytes;
                pending.RemoveFirst();
            }
            return true;
        }

        /// <summary>Reset only when the peer connection is replaced, alongside its receiver.</summary>
        public void Clear()
        {
            pending.Clear(); QueuedBytes = 0;
            lastEnqueued = highestSent = acknowledged = 0;
        }

        private static bool ValidTime(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
    }

    /// <summary>Per-connection bounded reordering buffer with cumulative acknowledgements.</summary>
    public sealed class LabBulkReceiver
    {
        private readonly Dictionary<long, string> buffered = new Dictionary<long, string>();
        private readonly int maximumBuffered;
        public long AckThrough { get; private set; }
        public int BufferedCount => buffered.Count;

        public LabBulkReceiver(int windowSize = LabBulkSender.DefaultWindowSize)
        {
            if (windowSize <= 0 || windowSize > 4096) throw new ArgumentOutOfRangeException(nameof(windowSize));
            maximumBuffered = windowSize * 2;
        }

        public string[] Receive(long sequence, string payload)
        {
            Receive(sequence, payload, out var ready);
            return ready;
        }

        public bool Receive(long sequence, string payload, out string[] ready)
        {
            ready = Array.Empty<string>();
            if (sequence <= 0 || !LabBulkPayloadEncoding.TrySize(payload, out _)) return false;
            if (sequence <= AckThrough) return true; // Resend the same acknowledgement after ACK loss.
            if (sequence - AckThrough > maximumBuffered) return false;
            if (buffered.TryGetValue(sequence, out var previous))
                return string.Equals(previous, payload, StringComparison.Ordinal);
            if (buffered.Count >= maximumBuffered) return false;
            buffered.Add(sequence, payload);
            var output = new List<string>();
            while (AckThrough < long.MaxValue && buffered.TryGetValue(AckThrough + 1, out var next))
            {
                buffered.Remove(++AckThrough);
                output.Add(next);
            }
            ready = output.ToArray();
            return true;
        }

        /// <summary>Reset only when the peer connection is replaced, alongside its sender.</summary>
        public void Clear() { buffered.Clear(); AckThrough = 0; }
    }

    /// <summary>
    /// Split one Base64-encoded logical bulk message before it enters the ordered chunk channel.
    /// Every payload, including its index/count prefix, stays below NGO's nonfragmented message MTU.
    /// </summary>
    public static class LabBulkMessageChunks
    {
        public const int MaximumEncodedCharacters = 32768;
        public const int MaximumParts = 64;
        public const int MaximumFragmentCharacters = 900;
        // At most two decimal digits for index/count plus two separators.
        public const int DataCharacters = MaximumFragmentCharacters - 6;

        public static string[] Split(string encoded)
        {
            if (!ValidEncoded(encoded))
                throw new ArgumentException("A nonempty bounded Base64 bulk message is required.", nameof(encoded));
            int count = (encoded.Length + DataCharacters - 1) / DataCharacters;
            if (count > MaximumParts) throw new ArgumentException("Too many bulk message fragments.", nameof(encoded));
            var fragments = new string[count];
            for (int i = 0; i < count; i++)
            {
                int start = i * DataCharacters;
                fragments[i] = i.ToString(CultureInfo.InvariantCulture) + "|"
                    + count.ToString(CultureInfo.InvariantCulture) + "|"
                    + encoded.Substring(start, Math.Min(DataCharacters, encoded.Length - start));
            }
            return fragments;
        }

        internal static bool ValidEncoded(string encoded)
        {
            if (string.IsNullOrEmpty(encoded) || encoded.Length > MaximumEncodedCharacters || encoded.Length % 4 != 0)
                return false;
            if (!Base64Characters(encoded)) return false;
            try { Convert.FromBase64String(encoded); return true; }
            catch (FormatException) { return false; }
        }

        internal static bool Base64Characters(string value)
        {
            foreach (char character in value)
                if (!(character >= 'A' && character <= 'Z') && !(character >= 'a' && character <= 'z')
                    && !(character >= '0' && character <= '9') && character != '+' && character != '/' && character != '=')
                    return false;
            return true;
        }
    }

    /// <summary>
    /// Receives fragments only after LabBulkReceiver has restored transport order and removed
    /// duplicates. No missing part is skipped. True means one complete logical message is ready.
    /// </summary>
    public sealed class LabBulkMessageAssembler
    {
        private readonly StringBuilder pending = new StringBuilder();
        private int parts;
        private int next;
        public bool HasPending => parts > 0;

        public bool Receive(string fragment, out string encoded)
        {
            encoded = null;
            if (string.IsNullOrEmpty(fragment) || fragment.Length > LabBulkMessageChunks.MaximumFragmentCharacters)
            { Clear(); return false; }
            int first = fragment.IndexOf('|');
            int second = first >= 0 ? fragment.IndexOf('|', first + 1) : -1;
            if (first <= 0 || second <= first + 1 || second == fragment.Length - 1
                || !int.TryParse(fragment.Substring(0, first), NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                || !int.TryParse(fragment.Substring(first + 1, second - first - 1), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int count)
                || count < 1 || count > LabBulkMessageChunks.MaximumParts || index < 0 || index >= count)
            { Clear(); return false; }
            string data = fragment.Substring(second + 1);
            if (data.Length > LabBulkMessageChunks.DataCharacters || !LabBulkMessageChunks.Base64Characters(data))
            { Clear(); return false; }
            if (index == 0) { Clear(); parts = count; }
            if (parts == 0 || count != parts || index != next
                || data.Length > LabBulkMessageChunks.MaximumEncodedCharacters - pending.Length)
            { Clear(); return false; }
            pending.Append(data);
            next++;
            if (next < parts) return false;
            string complete = pending.ToString();
            Clear();
            if (!LabBulkMessageChunks.ValidEncoded(complete)) return false;
            encoded = complete;
            return true;
        }

        public void Clear() { pending.Clear(); parts = next = 0; }
    }

    internal static class LabBulkPayloadEncoding
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        public static bool TrySize(string payload, out int bytes)
        {
            bytes = 0;
            if (string.IsNullOrEmpty(payload) || payload.Length > LabBulkSender.MaximumPayloadBytes) return false;
            try { bytes = Utf8.GetByteCount(payload); }
            catch (EncoderFallbackException) { return false; }
            return bytes <= LabBulkSender.MaximumPayloadBytes;
        }
    }
}
