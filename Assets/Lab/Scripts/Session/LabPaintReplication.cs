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
        public const int MaximumFragmentCount = (((256 * 128 * 5 + 2) / 3) * 4
            + FragmentCharacters - 1) / FragmentCharacters;
        private Color32[] published;
        private byte[] publishedPainters;
        private string session;
        private uint round, revision;
        private int columns, rows, paintCount, covered;
        private string hash;
        private long sequence, epoch;

        public LabPaintFrame[] Publish(LabPaintSurface source, string nextSession, uint nextRound, long nextEpoch)
        {
            var current = new Color32[source.CellCount];
            var painters = new byte[source.CellCount];
            source.CopyPixels(current);
            source.CopyPainterSlots(painters);
            bool fresh = published == null || session != nextSession || round != nextRound
                || columns != source.Columns || rows != source.Rows;
            if (!fresh && revision == source.Revision && paintCount == source.PaintCount) return Array.Empty<LabPaintFrame>();
            var delta = new List<byte>();
            if (!fresh)
                for (int i = 0; i < current.Length; i++)
                    if (!current[i].Equals(published[i]) || painters[i] != publishedPainters[i])
                    {
                        delta.Add((byte)i); delta.Add((byte)(i >> 8));
                        AddPixel(delta, current[i]);
                        delta.Add(painters[i]);
                    }
            bool full = fresh || delta.Count >= current.Length * 5;
            byte[] payload = full ? CellBytes(current, painters) : delta.ToArray();
            var frames = Frames(nextSession, nextRound, nextEpoch, ++sequence, full,
                fresh ? 0u : revision, source.Revision, source.Columns, source.Rows,
                source.PaintCount, source.CoveredCount, Hash(current, painters), payload);
            published = current; publishedPainters = painters;
            session = nextSession; round = nextRound; epoch = nextEpoch;
            columns = source.Columns; rows = source.Rows; revision = source.Revision;
            paintCount = source.PaintCount; covered = source.CoveredCount; hash = frames[0].hash;
            return frames;
        }

        public LabPaintFrame[] Checkpoint()
            => published == null ? Array.Empty<LabPaintFrame>()
                : Frames(session, round, epoch, ++sequence, true, 0, revision, columns, rows,
                    paintCount, covered, hash, CellBytes(published, publishedPainters));

        public void Reset()
        {
            published = null; publishedPainters = null; session = null;
            round = revision = 0; columns = rows = paintCount = covered = 0;
            hash = null; epoch = sequence = 0;
        }

        private static LabPaintFrame[] Frames(string session, uint round, long epoch, long sequence,
            bool full, uint from, uint revision, int columns, int rows, int paintCount,
            int coveredCount, string hash, byte[] payload)
        {
            string encoded = Convert.ToBase64String(payload);
            int count = Math.Max(1, (encoded.Length + FragmentCharacters - 1) / FragmentCharacters);
            var result = new LabPaintFrame[count];
            for (int i = 0; i < count; i++)
                result[i] = new LabPaintFrame
                {
                    session = session, round = round, epoch = epoch, sequence = sequence,
                    full = full, fromRevision = from, revision = revision, columns = columns, rows = rows,
                    paintCount = paintCount, coveredCount = coveredCount, hash = hash, part = i, parts = count,
                    data = encoded.Substring(i * FragmentCharacters,
                        Math.Min(FragmentCharacters, encoded.Length - i * FragmentCharacters))
                };
            return result;
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

        /// <summary>Transaction identity includes attribution even when pigment colors match.</summary>
        public static string Hash(Color32[] pixels, byte[] painters)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(CellBytes(pixels, painters)))
                    .Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>
    /// Client applies complete, checked transactions. Rendering is never a prerequisite.
    /// A gap cannot be repaired by locally rerunning randomness: request a Host checkpoint.
    /// </summary>
    public sealed class LabPaintReceiver
    {
        private readonly LabPaintSurface surface;
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
            string encoded = string.Concat(fragments);
            DiscardPending();
            byte[] bytes;
            try { bytes = Convert.FromBase64String(encoded); }
            catch (FormatException) { return LabPaintReceive.NeedCheckpoint; }
            int size = head.columns * head.rows;
            Color32[] next;
            byte[] nextPainters;
            if (head.full)
            {
                if (bytes.Length != size * 5) return LabPaintReceive.NeedCheckpoint;
                if (session == head.session && round == head.round && head.revision < surface.Revision)
                    return LabPaintReceive.Rejected;
                next = new Color32[size];
                nextPainters = new byte[size];
                for (int i = 0; i < size; i++)
                { next[i] = Pixel(bytes, i * 5); nextPainters[i] = bytes[i * 5 + 4]; }
            }
            else
            {
                if (head.columns != surface.Columns || head.rows != surface.Rows
                    || head.fromRevision != surface.Revision || head.revision < head.fromRevision
                    || bytes.Length % 7 != 0 || bytes.Length > size * 7)
                    return LabPaintReceive.NeedCheckpoint;
                next = new Color32[size]; surface.CopyPixels(next);
                nextPainters = new byte[size]; surface.CopyPainterSlots(nextPainters);
                int previous = -1;
                for (int at = 0; at < bytes.Length; at += 7)
                {
                    int index = bytes[at] | bytes[at + 1] << 8;
                    if (index >= size || index <= previous) return LabPaintReceive.NeedCheckpoint;
                    next[index] = Pixel(bytes, at + 2); nextPainters[index] = bytes[at + 6]; previous = index;
                }
            }
            int covered = 0;
            for (int i = 0; i < size; i++)
            {
                Color32 pixel = next[i];
                if (pixel.a != 0 && pixel.a != 255) return LabPaintReceive.NeedCheckpoint;
                if (pixel.a == 0 && (pixel.r != 0 || pixel.g != 0 || pixel.b != 0)) return LabPaintReceive.NeedCheckpoint;
                if (nextPainters[i] > 5 || (pixel.a == 0 && nextPainters[i] != 0)) return LabPaintReceive.NeedCheckpoint;
                if (pixel.a == 255) covered++;
            }
            if (covered != head.coveredCount || LabPaintPublisher.Hash(next, nextPainters) != head.hash
                || !surface.TryReplacePixels(head.columns, head.rows, head.revision, next, nextPainters, head.paintCount))
                return LabPaintReceive.NeedCheckpoint;
            session = head.session; round = head.round; epoch = head.epoch; completedSequence = head.sequence;
            return LabPaintReceive.Applied;
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
        }
        private void DiscardPending() { pending = null; fragments = null; received = 0; }
        private static Color32 Pixel(byte[] source, int at) => new Color32(source[at], source[at + 1], source[at + 2], source[at + 3]);
        private static bool Valid(LabPaintFrame f)
            => f != null && !string.IsNullOrEmpty(f.session) && f.session.Length <= 128 && f.round > 0
                && f.epoch > 0 && f.sequence > 0 && LabPaintPublisher.ValidDimensions(f.columns, f.rows)
                && f.paintCount >= 0 && f.coveredCount >= 0 && f.coveredCount <= f.columns * f.rows
                && f.parts >= 1 && f.parts <= LabPaintPublisher.MaximumFragmentCount && f.part >= 0 && f.part < f.parts
                && f.data != null && f.data.Length <= LabPaintPublisher.FragmentCharacters
                && f.hash != null && f.hash.Length == 64;
        private static bool SameHeader(LabPaintFrame a, LabPaintFrame b)
            => a.session == b.session && a.round == b.round && a.sequence == b.sequence && a.epoch == b.epoch
                && a.fromRevision == b.fromRevision && a.revision == b.revision && a.columns == b.columns
                && a.rows == b.rows && a.parts == b.parts && a.full == b.full && a.paintCount == b.paintCount
                && a.coveredCount == b.coveredCount && a.hash == b.hash;
    }
}
