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
    /// The receiver also reports the chunks it already holds after a gap (selective ACK), so only
    /// chunks that are really missing go out again.
    /// </summary>
    public sealed class LabBulkSender
    {
        public const int MaximumPayloadBytes = 5000;
        public const int DefaultMaximumQueuedBytes = 4 * 1024 * 1024;
        /// <summary>
        /// Chunks after the cumulative ACK that may be in flight. A gap holds the window for about two round trips,
        /// so 32 stalled a 3-player peer from about 230 ms RTT even without loss. <see cref="LabBulkReceiver.SelectiveAck"/>
        /// covers the whole window.
        /// </summary>
        public const int DefaultWindowSize = 64;
        public const double InitialRetrySeconds = .25d;
        public const double MaximumRetrySeconds = 1d;
        /// <summary>Floor of the measured retry timeout; below it a 30 fps receiver's ACK frame alone could cause spurious resends (#437).</summary>
        public const double MinimumRetrySeconds = .08d;
        private const int MaximumAttempts = 8;
        private const int MaximumBackoff = 16;
        private sealed class Pending
        {
            public long sequence;
            public string payload;
            public int bytes;
            public int attempts;
            public double dueAt;
            public double sentAt;
            /// <summary>Send order of the first and of the latest transmission; one counter numbers every chunk sent.</summary>
            public long firstOrder, lastOrder;
            /// <summary>Lost: a chunk first sent after this one's latest send has arrived. Due at once, not a timeout.</summary>
            public bool fastResend;
            /// <summary>Held by the receiver after a gap. It is never sent again and its bytes are released.</summary>
            public bool delivered;
        }

        private readonly LinkedList<Pending> pending = new LinkedList<Pending>();
        private readonly int maximumQueuedBytes;
        private readonly int windowSize;
        private long lastEnqueued;
        private long highestSent;
        private long acknowledged;
        private double smoothedRtt, rttVariance;
        private bool hasRttSample;
        private int backoff = 1;
        private double lastBackoffAt = double.NegativeInfinity;
        private long sendOrder;
        /// <summary>The latest first-send order among confirmed chunks; a chunk last sent before it was lost.</summary>
        private long deliveredOrder;
        /// <summary>Chunks not yet acknowledged cumulatively, including the ones the receiver already holds after a gap.</summary>
        public int Count => pending.Count;
        /// <summary>Bytes of chunks the receiver has not confirmed yet, cumulatively or selectively.</summary>
        public int QueuedBytes { get; private set; }
        public long LastEnqueued => lastEnqueued;
        public long Acknowledged => acknowledged;
        public bool HasRttSample => hasRttSample;
        public double SmoothedRttSeconds => smoothedRtt;

        /// <summary>
        /// The delay before a chunk's first resend. Until an RTT sample exists it is the fixed quarter second.
        /// After that it is RFC 6298's srtt + 4·rttvar, kept at 0.08 s or more, times a shared backoff, capped at one second.
        /// The backoff doubles on a timeout resend, at most once per current timeout, and resets on the next valid
        /// sample. So new chunks wait longer too after the RTT jumps above a stale timeout (Karn). Without it, every
        /// send would be resent before its ACK and no sample would ever recover the estimate. Each further resend of
        /// the same chunk doubles its own wait, up to one second.
        /// </summary>
        public double RetryTimeoutSeconds => !hasRttSample ? InitialRetrySeconds
            : Math.Min(MaximumRetrySeconds, Math.Max(MinimumRetrySeconds, smoothedRtt + 4d * rttVariance) * backoff);

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
                if (entry.delivered || (!entry.fastResend && now < entry.dueAt)) continue;
                if (entry.bytes > byteBudget) break;
                byteBudget -= entry.bytes;
                result.Add(new LabBulkFrame(entry.sequence, entry.payload, entry.bytes));
                highestSent = Math.Max(highestSent, entry.sequence);
                if (entry.attempts == 0) entry.sentAt = now;
                // A timeout of any chunk backs off the shared timer, but a window of chunks expiring together counts once.
                else if (!entry.fastResend && hasRttSample && now - lastBackoffAt >= RetryTimeoutSeconds)
                {
                    backoff = Math.Min(MaximumBackoff, backoff * 2);
                    lastBackoffAt = now;
                }
                entry.lastOrder = ++sendOrder;
                if (entry.attempts == 0) entry.firstOrder = entry.lastOrder;
                entry.fastResend = false;
                // Attempts and the exponent saturate; the one-second cap is reached long before either does.
                entry.attempts = Math.Min(MaximumAttempts, entry.attempts + 1);
                entry.dueAt = now + Math.Min(MaximumRetrySeconds,
                    RetryTimeoutSeconds * (1 << (entry.attempts - 1)));
            }
            return result.ToArray();
        }

        /// <summary>A cumulative acknowledgement cannot discard chunks that have never been sent.</summary>
        public bool Ack(long highestContiguous) => Ack(highestContiguous, 0L, double.NaN);

        /// <summary>A cumulative acknowledgement from a receiver that holds nothing after its first gap.</summary>
        public bool Ack(long highestContiguous, double now) => Ack(highestContiguous, 0L, now);

        /// <summary>
        /// Applies one receiver acknowledgement. Every chunk through <paramref name="highestContiguous"/> arrived in
        /// order, and bit i of <paramref name="selective"/> says the receiver holds chunk highestContiguous + 2 + i
        /// after the gap (<see cref="LabBulkReceiver.SelectiveAck"/>). Held chunks are never sent again. An older
        /// ACK still adds what it held. An ACK of a chunk never sent is invalid as a whole.
        /// <para>
        /// The path keeps order, so a chunk whose latest send went out before a chunk that has now arrived was lost:
        /// it is due again at once, without backing off the timer like a timeout. That covers every gap in the window
        /// at once, not only the first. After that resend, only the arrival of a chunk sent later marks it lost again.
        /// </para>
        /// With a valid <paramref name="now"/> it also measures the RTT, but only when the most recently sent chunk
        /// this ACK newly confirms was sent once (Karn). An ACK answering a resend would time the recovery, not the path.
        /// </summary>
        public bool Ack(long highestContiguous, long selective, double now)
        {
            if (highestContiguous < 0 || highestContiguous > highestSent) return false;
            Pending newest = null;
            bool changed = false;
            if (highestContiguous > acknowledged)
            {
                acknowledged = highestContiguous;
                changed = true;
                while (pending.First != null && pending.First.Value.sequence <= highestContiguous)
                {
                    Pending entry = pending.First.Value;
                    if (!entry.delivered) Confirm(entry, ref newest);
                    pending.RemoveFirst();
                }
            }
            if (selective != 0L)
                foreach (Pending entry in pending)
                {
                    long bit = entry.sequence - highestContiguous - 2;
                    if (bit < 0L) continue;
                    if (bit >= LabBulkReceiver.SelectiveAckBits || entry.sequence > highestSent) break;
                    if (entry.delivered || (selective & (1L << (int)bit)) == 0L) continue;
                    entry.delivered = true;
                    // The receiver keeps a held chunk until the gap before it fills, so the copy is no longer needed.
                    entry.payload = null;
                    Confirm(entry, ref newest);
                    changed = true;
                }
            if (newest == null) return changed;
            if (ValidTime(now) && newest.attempts == 1 && now >= newest.sentAt) Sample(now - newest.sentAt);
            int eligible = 0;
            foreach (Pending entry in pending)
            {
                if (++eligible > windowSize) break;
                if (entry.attempts > 0 && !entry.delivered && entry.lastOrder < deliveredOrder) entry.fastResend = true;
            }
            return changed;
        }

        private void Confirm(Pending entry, ref Pending newest)
        {
            QueuedBytes -= entry.bytes;
            // Whichever copy arrived was sent no earlier than the first one.
            deliveredOrder = Math.Max(deliveredOrder, entry.firstOrder);
            if (newest == null || entry.lastOrder > newest.lastOrder) newest = entry;
        }

        private void Sample(double rtt)
        {
            backoff = 1;
            if (!hasRttSample)
            {
                smoothedRtt = rtt; rttVariance = rtt / 2d; hasRttSample = true;
                return;
            }
            rttVariance = .75d * rttVariance + .25d * Math.Abs(smoothedRtt - rtt);
            smoothedRtt = .875d * smoothedRtt + .125d * rtt;
        }

        /// <summary>Reset only when the peer connection is replaced, alongside its receiver.</summary>
        public void Clear()
        {
            pending.Clear(); QueuedBytes = 0;
            lastEnqueued = highestSent = acknowledged = 0;
            smoothedRtt = rttVariance = 0d; hasRttSample = false; backoff = 1; lastBackoffAt = double.NegativeInfinity;
            sendOrder = deliveredOrder = 0;
        }

        private static bool ValidTime(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
    }

    /// <summary>Per-connection bounded reordering buffer with cumulative and selective acknowledgements.</summary>
    public sealed class LabBulkReceiver
    {
        /// <summary>Bits in <see cref="SelectiveAck"/>. They reach AckThrough + 65, past the sender's whole default window.</summary>
        public const int SelectiveAckBits = 64;
        private readonly Dictionary<long, string> buffered = new Dictionary<long, string>();
        private readonly int maximumBuffered;
        public long AckThrough { get; private set; }
        public int BufferedCount => buffered.Count;

        /// <summary>
        /// Bit i is set when this receiver holds chunk AckThrough + 2 + i. AckThrough + 1 is the gap itself, so it has no
        /// bit. A held chunk stays until the gap fills, so a sender may stop resending it. Sent with every ACK.
        /// </summary>
        public long SelectiveAck
        {
            get
            {
                long bits = 0L;
                foreach (long sequence in buffered.Keys)
                {
                    long bit = sequence - AckThrough - 2;
                    if (bit >= 0L && bit < SelectiveAckBits) bits |= 1L << (int)bit;
                }
                return bits;
            }
        }

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
