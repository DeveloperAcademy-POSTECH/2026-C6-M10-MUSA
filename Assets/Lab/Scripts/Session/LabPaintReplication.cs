using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Bounded paint-only fragments, separate from the frequently sent game snapshot.</summary>
    [Serializable]
    public sealed class LabPaintFrame
    {
        // sequence is a connection-wide paint transfer ordinal; epoch is the game snapshot ordinal.
        public long sequence, epoch;
        public string session;
        public uint round, fromRevision, revision;
        public int columns, rows, paintCount, coveredCount, part, parts;
        public bool full;
        public string hash, data;
        // -1 keeps the previous hints for a delta; >=0 is a complete bounded hint checkpoint.
        public int visualBytes;
        public string visualHash;
        /// <summary>0 raw cell bytes, 1 deflate of them (#461). The hash still covers the inflated map.</summary>
        public int encoding;
    }

    public enum LabPaintReceive { Rejected, Pending, Applied, NeedCheckpoint }

    /// <summary>
    /// Host sends cell IDs + RGBA + painter P number (7 bytes/cell), or full 5-byte cells.
    /// Color and painter identity share one hash and commit; partial maps are never displayed.
    /// Frames have <=2048 ASCII base64 characters, leaving room for the bounded bulk envelope.
    /// </summary>
    public sealed class LabPaintPublisher
    {
        public const int FragmentCharacters = 2048;
        /// <summary>Enough fragments for the largest supported full RGBA/P-number checkpoint.</summary>
        public const int MaximumFragmentCount = (((256 * 128 * 5 + LabPaintVisualStamp.MaximumBytes + 2) / 3) * 4
            + FragmentCharacters - 1) / FragmentCharacters;
        private readonly LabPaintPublishCache cache;
        private readonly LabPaintEncodingPolicy policy;
        private LabPaintPublishCapture published;
        private string session;
        private uint round;
        private long sequence, epoch;

        /// <param name="cache">Shared by every peer's publisher on the Host so one publish pass copies, hashes and
        /// encodes the surface once instead of once per peer (#439). Null keeps a private capture per call.</param>
        /// <param name="policy">Raw keeps every frame's bytes as before; the Host's peers use Auto (#461).</param>
        public LabPaintPublisher(LabPaintPublishCache cache = null, LabPaintEncodingPolicy policy = LabPaintEncodingPolicy.RawOnly)
        {
            this.cache = cache;
            this.policy = policy;
        }

        public LabPaintFrame[] Publish(LabPaintSurface source, string nextSession, uint nextRound, long nextEpoch)
        {
            bool sameSource = published != null && ReferenceEquals(published.Source, source);
            if (sameSource && session == nextSession && round == nextRound
                && published.Columns == source.Columns && published.Rows == source.Rows && published.Revision == source.Revision
                && published.PaintCount == source.PaintCount && published.VisualVersion == source.VisualVersion)
                return Array.Empty<LabPaintFrame>();
            LabPaintPublishCapture current = cache != null ? cache.Capture(source, nextEpoch)
                : LabPaintPublishCapture.Take(source, nextEpoch, published);
            bool fresh = published == null || !sameSource || session != nextSession || round != nextRound
                || published.Columns != source.Columns || published.Rows != source.Rows;
            LabPaintPublishCapture.Body body = fresh ? current.FullBody : current.BodyFrom(published);
            if (body == null) return Array.Empty<LabPaintFrame>();
            string encoded = body.EncodedFor(policy, out int encoding);
            var frames = Frames(nextSession, nextRound, nextEpoch, ++sequence, body.Full,
                fresh ? 0u : published.Revision, current.Revision, current.Columns, current.Rows,
                current.PaintCount, current.CoveredCount, current.Hash, encoded, body.VisualSize, current.VisualHash, encoding);
            published = current;
            session = nextSession; round = nextRound; epoch = nextEpoch;
            return frames;
        }

        public LabPaintFrame[] Checkpoint()
        {
            if (published == null) return Array.Empty<LabPaintFrame>();
            LabPaintPublishCapture.Body body = published.FullBody;
            string encoded = body.EncodedFor(policy, out int encoding);
            return Frames(session, round, epoch, ++sequence, true, 0, published.Revision, published.Columns, published.Rows,
                published.PaintCount, published.CoveredCount, published.Hash, encoded, body.VisualSize, published.VisualHash, encoding);
        }

        public void Reset()
        {
            published = null; session = null;
            round = 0;
            epoch = sequence = 0;
        }

        private static LabPaintFrame[] Frames(string session, uint round, long epoch, long sequence,
            bool full, uint from, uint revision, int columns, int rows, int paintCount,
            int coveredCount, string hash, string encoded, int visualSize, string visualHash, int encoding)
        {
            int count = Math.Max(1, (encoded.Length + FragmentCharacters - 1) / FragmentCharacters);
            var result = new LabPaintFrame[count];
            for (int i = 0; i < count; i++)
                result[i] = new LabPaintFrame
                {
                    session = session, round = round, epoch = epoch, sequence = sequence,
                    full = full, fromRevision = from, revision = revision, columns = columns, rows = rows,
                    paintCount = paintCount, coveredCount = coveredCount, hash = hash, part = i, parts = count,
                    visualBytes = visualSize, visualHash = visualHash, encoding = encoding,
                    data = encoded.Substring(i * FragmentCharacters,
                        Math.Min(FragmentCharacters, encoded.Length - i * FragmentCharacters))
                };
            return result;
        }

        /// <summary>Cell payload plus the visual descriptor when it is included, as one body.</summary>
        internal static byte[] BuildBody(byte[] cellPayload, byte[] visual, bool includeVisual, out int visualSize)
        {
            visualSize = includeVisual ? visual.Length : visual.Length == 0 ? 0 : -1;
            byte[] payload = cellPayload;
            if (visualSize > 0)
            {
                payload = new byte[cellPayload.Length + visual.Length];
                Array.Copy(cellPayload, payload, cellPayload.Length);
                Array.Copy(visual, 0, payload, cellPayload.Length, visual.Length);
            }
            return payload;
        }

        internal static bool SameBytes(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        internal static bool ValidDimensions(int columns, int rows)
            => columns == rows * 2 && (rows == 32 || rows == 64 || rows == 128);
        internal static void AddPixel(List<byte> bytes, Color32 color)
        { bytes.Add(color.r); bytes.Add(color.g); bytes.Add(color.b); bytes.Add(color.a); }
        internal static byte[] PixelBytes(Color32[] pixels)
        {
            var result = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            { result[i * 4] = pixels[i].r; result[i * 4 + 1] = pixels[i].g; result[i * 4 + 2] = pixels[i].b; result[i * 4 + 3] = pixels[i].a; }
            return result;
        }
        public static string Hash(Color32[] pixels)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(PixelBytes(pixels))).Replace("-", "").ToLowerInvariant();
        }

        internal static byte[] CellBytes(Color32[] pixels, byte[] painters)
        {
            if (pixels == null || painters == null || pixels.Length != painters.Length)
                throw new ArgumentException("Paint color and painter maps must have equal lengths.");
            var bytes = new byte[pixels.Length * 5];
            for (int i = 0; i < pixels.Length; i++)
            {
                int at = i * 5;
                bytes[at] = pixels[i].r; bytes[at + 1] = pixels[i].g;
                bytes[at + 2] = pixels[i].b; bytes[at + 3] = pixels[i].a;
                bytes[at + 4] = painters[i];
            }
            return bytes;
        }

        /// <summary>
        /// The same digest as <see cref="Hash(Color32[], byte[])"/>, streamed through <paramref name="chunk"/> instead of one
        /// 5-bytes-per-cell copy of the map (#449). Both buffers are the caller's to reuse; <paramref name="chunk"/> holds at
        /// least one cell.
        /// </summary>
        public static string Hash(Color32[] pixels, byte[] painters, SHA256 sha, byte[] chunk)
        {
            if (pixels == null || painters == null || pixels.Length != painters.Length)
                throw new ArgumentException("Paint color and painter maps must have equal lengths.");
            if (sha == null) throw new ArgumentNullException(nameof(sha));
            if (chunk == null || chunk.Length < 5) throw new ArgumentException("The chunk must hold at least one cell.", nameof(chunk));
            int cellsPerChunk = chunk.Length / 5;
            sha.Initialize();
            for (int start = 0; start < pixels.Length; start += cellsPerChunk)
            {
                int end = Math.Min(pixels.Length, start + cellsPerChunk), at = 0;
                for (int i = start; i < end; i++, at += 5)
                {
                    chunk[at] = pixels[i].r; chunk[at + 1] = pixels[i].g;
                    chunk[at + 2] = pixels[i].b; chunk[at + 3] = pixels[i].a;
                    chunk[at + 4] = painters[i];
                }
                sha.TransformBlock(chunk, 0, at, null, 0);
            }
            sha.TransformFinalBlock(chunk, 0, 0);
            return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>Transaction identity includes attribution even when pigment colors match.</summary>
        public static string Hash(Color32[] pixels, byte[] painters)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(CellBytes(pixels, painters)))
                    .Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>
    /// One Host copy of a paint surface for a publish pass: colors, painter slots and visual stamps, with the hash,
    /// the full body and the delta bodies computed on first use and shared by every peer's publisher (#439).
    /// Delta bodies are keyed by the earlier capture's id, never its reference, so captures do not chain in memory.
    /// </summary>
    public sealed class LabPaintPublishCapture
    {
        internal sealed class Body
        {
            public bool Full;
            public byte[] Payload;
            public int VisualSize;
            private string raw, compressed;
            private bool compressionTried;

            public string Encoded => raw ?? (raw = Convert.ToBase64String(Payload));

            /// <summary>The Base64 body for a policy, compressed once and shared by every peer that uses it (#461).</summary>
            public string EncodedFor(LabPaintEncodingPolicy policy, out int encoding)
            {
                encoding = LabPaintPayloadCodec.Raw;
                if (policy != LabPaintEncodingPolicy.Auto) return Encoded;
                if (!compressionTried)
                {
                    compressionTried = true;
                    string candidate = Convert.ToBase64String(LabPaintPayloadCodec.Compress(Payload));
                    // Compare with the raw Base64 length without building that string; it is only made if raw wins.
                    if (candidate.Length < (Payload.Length + 2) / 3 * 4) compressed = candidate;
                }
                if (compressed == null) return Encoded;
                encoding = LabPaintPayloadCodec.Deflate;
                return compressed;
            }
        }

        private const int MaximumCachedBodies = 8;
        private static long nextId;
        private readonly Dictionary<long, Body> bodies = new Dictionary<long, Body>();
        private string hash;
        private Body fullBody;
        internal readonly long Id;
        internal readonly LabPaintSurface Source;
        internal readonly long Epoch;
        internal readonly uint Revision, VisualVersion;
        internal readonly int Columns, Rows, PaintCount, CoveredCount;
        internal readonly Color32[] Pixels;
        internal readonly byte[] Painters;
        internal readonly byte[] Visual;
        internal readonly string VisualHash;

        private LabPaintPublishCapture(LabPaintSurface source, long epoch, LabPaintPublishCapture reuse)
        {
            Id = ++nextId;
            Source = source; Epoch = epoch;
            Revision = source.Revision; VisualVersion = source.VisualVersion;
            Columns = source.Columns; Rows = source.Rows; PaintCount = source.PaintCount; CoveredCount = source.CoveredCount;
            Pixels = new Color32[source.CellCount];
            Painters = new byte[source.CellCount];
            source.CopyPixels(Pixels);
            source.CopyPainterSlots(Painters);
            bool sameVisual = reuse != null && ReferenceEquals(reuse.Source, source) && reuse.VisualVersion == source.VisualVersion;
            Visual = sameVisual ? reuse.Visual : LabPaintVisualStamp.Encode(source.CopyVisualStamps());
            VisualHash = Visual.Length == 0 ? null : sameVisual ? reuse.VisualHash : LabPaintVisualStamp.Hash(Visual);
        }

        /// <summary>Copy the surface now; the visual descriptor is reused from <paramref name="reuse"/> when unchanged.</summary>
        internal static LabPaintPublishCapture Take(LabPaintSurface source, long epoch, LabPaintPublishCapture reuse)
            => new LabPaintPublishCapture(source ?? throw new ArgumentNullException(nameof(source)), epoch, reuse);

        internal bool Matches(LabPaintSurface source, long epoch) => ReferenceEquals(Source, source) && Epoch == epoch
            && Revision == source.Revision && PaintCount == source.PaintCount && VisualVersion == source.VisualVersion
            && Columns == source.Columns && Rows == source.Rows;

        internal string Hash => hash ?? (hash = LabPaintPublisher.Hash(Pixels, Painters));

        internal Body FullBody => fullBody ?? (fullBody = new Body
        {
            Full = true,
            Payload = LabPaintPublisher.BuildBody(LabPaintPublisher.CellBytes(Pixels, Painters), Visual, true, out int size),
            VisualSize = size
        });

        /// <summary>
        /// The body that brings a peer from <paramref name="previous"/> (same source, round and size) to this capture,
        /// or null when nothing a peer sees changed. A delta as large as a full map is sent as the full map.
        /// </summary>
        internal Body BodyFrom(LabPaintPublishCapture previous)
        {
            if (bodies.TryGetValue(previous.Id, out Body cached)) return cached;
            bool visualChanged = !LabPaintPublisher.SameBytes(Visual, previous.Visual);
            Body body = null;
            if (previous.Revision != Revision || previous.PaintCount != PaintCount || visualChanged)
            {
                var delta = new List<byte>();
                Color32[] before = previous.Pixels;
                byte[] beforePainters = previous.Painters;
                for (int i = 0; i < Pixels.Length; i++)
                {
                    Color32 now = Pixels[i], was = before[i];
                    // Field comparison: Color32.Equals(object) boxes every cell.
                    if (now.r != was.r || now.g != was.g || now.b != was.b || now.a != was.a || Painters[i] != beforePainters[i])
                    {
                        delta.Add((byte)i); delta.Add((byte)(i >> 8));
                        LabPaintPublisher.AddPixel(delta, now);
                        delta.Add(Painters[i]);
                    }
                }
                if (delta.Count >= Pixels.Length * 5) body = FullBody;
                else body = new Body
                {
                    Full = false,
                    Payload = LabPaintPublisher.BuildBody(delta.ToArray(), Visual, visualChanged, out int size),
                    VisualSize = size
                };
            }
            if (bodies.Count >= MaximumCachedBodies) bodies.Clear();
            bodies[previous.Id] = body;
            return body;
        }
    }

    /// <summary>
    /// The Host's shared capture for one publish pass. Peers published with the same snapshot epoch from an
    /// unchanged surface get the same capture; any change of epoch or surface state takes a new one.
    /// </summary>
    public sealed class LabPaintPublishCache
    {
        private LabPaintPublishCapture latest;
        public int CapturesTaken { get; private set; }

        internal LabPaintPublishCapture Capture(LabPaintSurface source, long epoch)
        {
            if (latest != null && latest.Matches(source, epoch)) return latest;
            latest = LabPaintPublishCapture.Take(source, epoch, latest);
            CapturesTaken++;
            return latest;
        }

        public void Clear() => latest = null;
    }

    /// <summary>
    /// Client applies complete, checked transactions. Rendering is never a prerequisite.
    /// A gap cannot be repaired by locally rerunning randomness: request a Host checkpoint.
    /// </summary>
    public sealed class LabPaintReceiver
    {
        private readonly LabPaintSurface surface;
        // Reused across transactions so a delta allocates no map-sized buffer (#449). The spare maps are the surface's
        // previous arrays handed back by TryAdoptReplica; they are refilled before every use.
        private readonly SHA256 sha = SHA256.Create();
        private readonly byte[] hashChunk = new byte[4096 * 5];
        private Color32[] sparePixels;
        private byte[] sparePainters;
        private int[] changedCells = Array.Empty<int>();
        private byte[] payload = Array.Empty<byte>();
        private byte[] inflated = Array.Empty<byte>();
        private uint hashedVisualVersion;
        private bool visualHashed;
        private int currentVisualLength;
        private string currentVisualHash;
        private LabPaintFrame pending;
        private string[] fragments;
        private int received;
        private long completedSequence, epoch;
        private string session;
        private uint round;
        public string Session => session;
        public uint Round => round;
        public long Epoch => epoch;
        public bool HasPending => pending != null;
        /// <summary>The transfer ordinal of the partly received frame, or 0 when none is pending.</summary>
        public long PendingSequence => pending?.sequence ?? 0;
        /// <summary>Fragments of the pending frame received so far; it grows while the frame is still arriving.</summary>
        public int PendingReceivedParts => pending == null ? 0 : received;

        public LabPaintReceiver(LabPaintSurface surface) => this.surface = surface ?? throw new ArgumentNullException(nameof(surface));

        public LabPaintReceive Receive(LabPaintFrame frame)
        {
            if (!Valid(frame) || frame.sequence <= completedSequence || frame.epoch < epoch)
                return LabPaintReceive.Rejected;
            if (frame.epoch == epoch && session != null && (session != frame.session || round != frame.round))
                return LabPaintReceive.Rejected;
            if (!frame.full && (session != frame.session || round != frame.round))
                return LabPaintReceive.NeedCheckpoint;
            if (session == frame.session && round > frame.round) return LabPaintReceive.Rejected;
            if (pending != null && frame.sequence < pending.sequence) return LabPaintReceive.Rejected;
            if (pending == null || pending.sequence != frame.sequence)
            {
                pending = frame; fragments = new string[frame.parts]; received = 0;
            }
            if (!SameHeader(pending, frame)) { DiscardPending(); return LabPaintReceive.NeedCheckpoint; }
            if (fragments[frame.part] == null) { fragments[frame.part] = frame.data; received++; }
            else if (fragments[frame.part] != frame.data) { DiscardPending(); return LabPaintReceive.NeedCheckpoint; }
            if (received != frame.parts) return LabPaintReceive.Pending;
            LabPaintFrame head = pending;
            string[] parts = fragments;
            DiscardPending();
            if (!TryDecodePayload(parts, out int length)) return LabPaintReceive.NeedCheckpoint;
            byte[] bytes = payload;
            if (head.encoding == LabPaintPayloadCodec.Deflate)
            {
                // The header fixes the most a valid body can inflate to: a full map exactly, a delta at most every cell.
                int cells = head.columns * head.rows, visual = Math.Max(0, head.visualBytes);
                int limit = head.full ? cells * 5 + visual : cells * 7 + visual;
                if (!LabPaintPayloadCodec.TryInflate(payload, length, limit, ref inflated, out length))
                    return LabPaintReceive.NeedCheckpoint;
                bytes = inflated;
            }
            // null keeps the surface's current stamps: no copy, no re-encoding of an unchanged descriptor.
            LabPaintVisualStamp[] nextVisual = null;
            int cellLength = length;
            if (head.visualBytes == -1)
            {
                if (!CurrentVisualMatches(head.visualHash)) return LabPaintReceive.NeedCheckpoint;
            }
            else if (head.visualBytes == 0)
            {
                nextVisual = Array.Empty<LabPaintVisualStamp>();
                if (!string.IsNullOrEmpty(head.visualHash)) return LabPaintReceive.NeedCheckpoint;
            }
            else
            {
                if (length < head.visualBytes) return LabPaintReceive.NeedCheckpoint;
                var visual = new byte[head.visualBytes];
                Array.Copy(bytes, length - visual.Length, visual, 0, visual.Length);
                if (!LabPaintVisualStamp.TryDecode(visual, out nextVisual)) return LabPaintReceive.NeedCheckpoint;
                cellLength = length - visual.Length;
                byte[] checkedVisual = LabPaintVisualStamp.Encode(nextVisual);
                if (checkedVisual.Length > 0 && LabPaintVisualStamp.Hash(checkedVisual) != head.visualHash)
                    return LabPaintReceive.NeedCheckpoint;
                if (checkedVisual.Length == 0 && !string.IsNullOrEmpty(head.visualHash))
                    return LabPaintReceive.NeedCheckpoint;
            }
            int size = head.columns * head.rows;
            int changedCount = 0;
            if (head.full)
            {
                if (cellLength != size * 5) return LabPaintReceive.NeedCheckpoint;
                if (session == head.session && round == head.round && head.revision < surface.Revision)
                    return LabPaintReceive.Rejected;
                EnsureSpare(size);
                for (int i = 0; i < size; i++)
                { sparePixels[i] = Pixel(bytes, i * 5); sparePainters[i] = bytes[i * 5 + 4]; }
            }
            else
            {
                if (head.columns != surface.Columns || head.rows != surface.Rows
                    || head.fromRevision != surface.Revision || head.revision < head.fromRevision
                    || cellLength % 7 != 0 || cellLength > size * 7)
                    return LabPaintReceive.NeedCheckpoint;
                EnsureSpare(size);
                surface.CopyPixels(sparePixels);
                surface.CopyPainterSlots(sparePainters);
                if (changedCells.Length < cellLength / 7) changedCells = new int[cellLength / 7];
                int previous = -1;
                for (int at = 0; at < cellLength; at += 7)
                {
                    int index = bytes[at] | bytes[at + 1] << 8;
                    if (index >= size || index <= previous) return LabPaintReceive.NeedCheckpoint;
                    sparePixels[index] = Pixel(bytes, at + 2); sparePainters[index] = bytes[at + 6];
                    changedCells[changedCount++] = index; previous = index;
                }
            }
            // The surface checks the cells (only the changed ones for a delta on its own base), the P6 bound
            // (#394, #417) and the covered count, and changes nothing when any of them fails.
            if (LabPaintPublisher.Hash(sparePixels, sparePainters, sha, hashChunk) != head.hash
                || !surface.TryAdoptReplica(head.columns, head.rows, head.fromRevision, head.revision, sparePixels, sparePainters,
                    head.paintCount, head.coveredCount, nextVisual, head.full ? null : changedCells, changedCount,
                    out Color32[] recycled, out byte[] recycledPainters))
                return LabPaintReceive.NeedCheckpoint;
            sparePixels = recycled; sparePainters = recycledPainters;
            session = head.session; round = head.round; epoch = head.epoch; completedSequence = head.sequence;
            return LabPaintReceive.Applied;
        }

        private void EnsureSpare(int size)
        {
            if (sparePixels == null || sparePixels.Length != size) sparePixels = new Color32[size];
            if (sparePainters == null || sparePainters.Length != size) sparePainters = new byte[size];
        }

        /// <summary>
        /// Decode the fragments into the reused payload buffer, each on its own: when every fragment but the last is a whole
        /// number of plain Base64 quanta (no padding, no whitespace), this equals decoding their concatenation. Anything else takes the
        /// concatenating path, which accepts and rejects exactly what it did before (#449).
        /// </summary>
        private bool TryDecodePayload(string[] parts, out int length)
        {
            length = 0;
            int characters = 0;
            bool aligned = true;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                characters += part.Length;
                if (i < parts.Length - 1 && (part.Length % 4 != 0 || !PlainBase64(part))) aligned = false;
            }
            int capacity = characters / 4 * 3 + 3;
            if (payload.Length < capacity) payload = new byte[capacity];
            if (aligned)
            {
                bool decoded = true;
                foreach (string part in parts)
                {
                    if (!Convert.TryFromBase64String(part, new Span<byte>(payload, length, payload.Length - length), out int written))
                    { decoded = false; break; }
                    length += written;
                }
                if (decoded) return true;
                length = 0;
            }
            byte[] bytes;
            try { bytes = Convert.FromBase64String(string.Concat(parts)); }
            catch (FormatException) { return false; }
            if (payload.Length < bytes.Length) payload = new byte[bytes.Length];
            Array.Copy(bytes, payload, bytes.Length);
            length = bytes.Length;
            return true;
        }

        /// <summary>
        /// Only the 64 data characters. Padding or whitespace inside a non-final fragment is something the concatenated
        /// decode rejects but a per-fragment decode could accept, so such a fragment takes the old path.
        /// </summary>
        private static bool PlainBase64(string part)
        {
            foreach (char c in part)
                if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '+' || c == '/')) return false;
            return true;
        }

        /// <summary>
        /// A delta that keeps the stamps (visualBytes -1) must still match the hash of the stamps this surface has. That
        /// hash is computed once per <see cref="LabPaintSurface.VisualVersion"/>, which changes whenever the stamps do.
        /// </summary>
        private bool CurrentVisualMatches(string expectedHash)
        {
            if (!visualHashed || hashedVisualVersion != surface.VisualVersion)
            {
                byte[] current = LabPaintVisualStamp.Encode(surface.CopyVisualStamps());
                currentVisualLength = current.Length;
                currentVisualHash = current.Length == 0 ? null : LabPaintVisualStamp.Hash(current);
                hashedVisualVersion = surface.VisualVersion; visualHashed = true;
            }
            if (currentVisualLength > 0 && currentVisualHash != expectedHash) return false;
            return currentVisualLength > 0 || string.IsNullOrEmpty(expectedHash);
        }

        public bool Matches(LabSnapshot snapshot)
            => snapshot != null && session == snapshot.sessionId && round == snapshot.round
                && surface.Columns == snapshot.paintColumns && surface.Rows == snapshot.paintRows
                && surface.Revision == snapshot.paintRevision && surface.CoveredCount == snapshot.paintCoveredCount
                && surface.CellCount == snapshot.paintCellCount && surface.PaintCount == snapshot.paintCount;

        public void Reset()
        {
            DiscardPending(); completedSequence = epoch = 0; session = null; round = 0;
            surface.Reset(new LabPaintGrowthSettings(), 1);
            sparePixels = null; sparePainters = null; visualHashed = false;
        }
        private void DiscardPending() { pending = null; fragments = null; received = 0; }
        private static Color32 Pixel(byte[] source, int at) => new Color32(source[at], source[at + 1], source[at + 2], source[at + 3]);
        private static bool Valid(LabPaintFrame f)
            => f != null && !string.IsNullOrEmpty(f.session) && f.session.Length <= 128 && f.round > 0
                && f.epoch > 0 && f.sequence > 0 && LabPaintPublisher.ValidDimensions(f.columns, f.rows)
                && f.paintCount >= 0 && f.coveredCount >= 0 && f.coveredCount <= f.columns * f.rows
                && f.parts >= 1 && f.parts <= LabPaintPublisher.MaximumFragmentCount && f.part >= 0 && f.part < f.parts
                && f.data != null && f.data.Length <= LabPaintPublisher.FragmentCharacters
                && f.hash != null && f.hash.Length == 64
                && f.visualBytes >= -1 && f.visualBytes <= LabPaintVisualStamp.MaximumBytes
                && (!f.full || f.visualBytes >= 0)
                && (f.visualBytes <= 0 || f.visualBytes % LabPaintVisualStamp.ByteSize == 0)
                && (f.visualBytes == 0 ? string.IsNullOrEmpty(f.visualHash)
                    : f.visualHash != null && f.visualHash.Length == 64)
                && (f.encoding == LabPaintPayloadCodec.Raw || f.encoding == LabPaintPayloadCodec.Deflate);
        private static bool SameHeader(LabPaintFrame a, LabPaintFrame b)
            => a.session == b.session && a.round == b.round && a.sequence == b.sequence && a.epoch == b.epoch
                && a.fromRevision == b.fromRevision && a.revision == b.revision && a.columns == b.columns
                && a.rows == b.rows && a.parts == b.parts && a.full == b.full && a.paintCount == b.paintCount
                && a.coveredCount == b.coveredCount && a.hash == b.hash
                && a.visualBytes == b.visualBytes && (a.visualHash ?? string.Empty) == (b.visualHash ?? string.Empty)
                && a.encoding == b.encoding;
    }
}
