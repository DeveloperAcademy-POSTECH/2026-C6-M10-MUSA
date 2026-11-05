using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace C6Lab
{
    public enum LabPaintArtOperationKind : byte
    {
        RoundBegin = 1, StrokeBegin = 2, PaintCells = 3, EraseCells = 4, SurfaceCommit = 5, RoundSeal = 6
    }

    /// <summary>Ordered presentation-only facts. Neither this record nor replay supplies gameplay ink.</summary>
    public struct LabPaintArtOperation
    {
        public ulong Sequence;
        public LabPaintArtOperationKind Kind;
        public uint StrokeId;
        public LabPaintVisualStamp Stamp;
        public int[] Cells;
        public uint Revision;
        public string SurfaceHash;
    }

    /// <summary>
    /// Append-only, round-lifetime artwork history. A bounded page cache fronts a disk file;
    /// old art is never replaced by a recent-stamp window. All failures latch Error instead
    /// of throwing into the simulation. Dispose closes and removes both temporary files.
    /// Main-thread use only, like LabPaintSurface itself.
    /// </summary>
    public sealed class LabPaintArtJournal : IDisposable
    {
        public const int MaximumPageBytes = 4096;
        public const long DefaultMaximumBytes = 64L * 1024 * 1024;
        public const int DefaultCacheBytes = 2 * 1024 * 1024;
        private const uint Magic = 0x4A504148; // HAPJ, little endian
        private const ushort FormatVersion = 1;
        private const int HeaderBytes = 64;
        private const int FooterBytes = 32;
        private const int MaximumTextBytes = 256;
        private readonly string path;
        private readonly long maximumBytes;
        private readonly int maximumCacheBytes;
        private readonly bool receiver;
        private FileStream file, strokeIndex;
        private BinaryWriter pendingWriter;
        private readonly List<PageIndex> pages = new List<PageIndex>();
        private readonly Dictionary<int, LinkedListNode<CachedPage>> cache = new Dictionary<int, LinkedListNode<CachedPage>>();
        private readonly LinkedList<CachedPage> lru = new LinkedList<CachedPage>();
        private readonly MemoryStream pending = new MemoryStream(MaximumPageBytes);
        private byte[] prefix = new byte[32];
        private ulong pendingFirst;
        private uint lastStroke;
        private int cacheBytes;
        private long rawBytes;
        private bool disposed, createdFile, createdIndex;
        public string Epoch { get; }
        public int Columns { get; }
        public int Rows { get; }
        public string StyleVersion { get; }
        public ulong LastSequence { get; private set; }
        public int PageCount => pages.Count;
        public long RawBytes => rawBytes + (pending.Length == 0 ? 0 : HeaderBytes + FooterBytes + pending.Length);
        public int CachedPageBytes => cacheBytes;
        public string Error { get; private set; }
        public bool IsSealed { get; private set; }
        public uint SealRevision { get; private set; }
        public string SealSurfaceHash { get; private set; }
        public uint CommitRevision { get; private set; }
        public string CommitSurfaceHash { get; private set; }
        public string PrefixHash => Hex(prefix);
        public bool IsReceiver => receiver;
        public bool HasPendingPage => pending.Length > 0;

        public LabPaintArtJournal(string path, string epoch, int columns, int rows, string styleVersion,
            bool receiver = false, long maxBytes = DefaultMaximumBytes, int cacheBytes = DefaultCacheBytes)
        {
            this.path = path;
            Epoch = epoch; Columns = columns; Rows = rows; StyleVersion = styleVersion;
            this.receiver = receiver;
            pendingWriter = new BinaryWriter(pending, Encoding.UTF8, true);
            maximumBytes = maxBytes; maximumCacheBytes = Math.Max(0, cacheBytes);
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !ValidText(epoch) || !ValidText(styleVersion)
                    || !ValidShape(columns, rows) || maxBytes < MaximumPageBytes)
                    throw new ArgumentException("Invalid artwork journal settings.");
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read); createdFile = true;
                strokeIndex = new FileStream(path + ".strokes", FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read); createdIndex = true;
                if (!receiver) Append(LabPaintArtOperationKind.RoundBegin, writer =>
                {
                    writer.Write(columns); writer.Write(rows); WriteText(writer, epoch); WriteText(writer, styleVersion);
                });
            }
            catch (Exception e) { Fail("Artwork journal storage could not be opened: " + e.Message); }
        }

        public void Fail(string message)
        {
            if (Error == null) Error = string.IsNullOrEmpty(message) ? "Artwork journal failed." : message;
        }

        public uint BeginStroke(LabPaintVisualStamp descriptor)
        {
            if (!Writable || lastStroke == uint.MaxValue) return 0;
            uint id = lastStroke + 1;
            descriptor.sequence = id;
            if (!LabPaintVisualStamp.Valid(new[] { descriptor })) { Fail("Invalid artwork stroke descriptor."); return 0; }
            byte[] bytes = LabPaintVisualStamp.Encode(new[] { descriptor });
            if (!Append(LabPaintArtOperationKind.StrokeBegin, w => { w.Write(id); w.Write(bytes); })) return 0;
            lastStroke = id;
            return id;
        }

        public bool AppendPaint(uint strokeId, int cell, uint revision)
        {
            if (!Writable) return false;
            if (strokeId == 0 || strokeId > lastStroke || cell < 0 || cell >= Columns * Rows)
            { Fail("Invalid artwork paint operation."); return false; }
            return AppendCell(LabPaintArtOperationKind.PaintCells, strokeId, cell, revision);
        }

        public bool AppendErase(int cell, uint revision)
        {
            if (!Writable) return false;
            if (cell < 0 || cell >= Columns * Rows) { Fail("Invalid artwork erase operation."); return false; }
            return AppendCell(LabPaintArtOperationKind.EraseCells, 0, cell, revision);
        }

        public bool Commit(uint revision, string surfaceHash) => AppendCommit(false, revision, surfaceHash);
        public bool Seal(uint revision, string surfaceHash)
        {
            if (IsSealed) return Error == null && revision == SealRevision && surfaceHash == SealSurfaceHash;
            return AppendCommit(true, revision, surfaceHash) && Flush();
        }

        private bool AppendCommit(bool seal, uint revision, string surfaceHash)
        {
            if (!Writable) return false;
            if (!TryHashBytes(surfaceHash, out byte[] hash)) { Fail("Invalid artwork surface digest."); return false; }
            bool result = Append(seal ? LabPaintArtOperationKind.RoundSeal : LabPaintArtOperationKind.SurfaceCommit,
                w => { w.Write(revision); w.Write(hash); });
            if (result)
            {
                CommitRevision = revision; CommitSurfaceHash = surfaceHash;
                if (seal) { IsSealed = true; SealRevision = revision; SealSurfaceHash = surfaceHash; }
            }
            return result;
        }

        private bool Writable => !disposed && Error == null && !receiver && !IsSealed;

        private bool Append(LabPaintArtOperationKind kind, Action<BinaryWriter> body)
        {
            if (!Writable) return false;
            try
            {
                byte[] record;
                using (var bytes = new MemoryStream())
                using (var writer = new BinaryWriter(bytes))
                { writer.Write((byte)kind); writer.Write(LastSequence + 1); body(writer); record = bytes.ToArray(); }
                if (!PrepareAppend(record.Length)) return false;
                int offset = (int)pending.Length;
                pendingWriter.Write((ushort)record.Length); pendingWriter.Write(record);
                LastSequence++;
                if (kind == LabPaintArtOperationKind.StrokeBegin)
                    WriteStrokeLocation(lastStroke + 1, pages.Count, offset);
                return true;
            }
            catch (Exception e) { Fail("Artwork journal append failed: " + e.Message); return false; }
        }

        private bool PrepareAppend(int recordLength)
        {
            if (LastSequence == ulong.MaxValue) { Fail("Artwork sequence limit reached."); return false; }
            if (recordLength + 2 > MaximumPageBytes - HeaderBytes - FooterBytes)
            { Fail("Artwork operation exceeds page size."); return false; }
            if (HeaderBytes + FooterBytes + pending.Length + recordLength + 2 > MaximumPageBytes && !Flush()) return false;
            if (rawBytes + HeaderBytes + FooterBytes + pending.Length + recordLength + 2 > maximumBytes)
            { Fail("Artwork journal reached its round storage limit; exact artwork is unavailable."); return false; }
            if (pending.Length == 0) pendingFirst = LastSequence + 1;
            return true;
        }

        // Cells are the hot simulation path. Write directly into the reusable page buffer;
        // do not allocate a MemoryStream, closure, or record array for each cell.
        private bool AppendCell(LabPaintArtOperationKind kind, uint stroke, int cell, uint revision)
        {
            try
            {
                int length = kind == LabPaintArtOperationKind.PaintCells ? 21 : 17;
                if (!PrepareAppend(length)) return false;
                pendingWriter.Write((ushort)length); pendingWriter.Write((byte)kind); pendingWriter.Write(LastSequence + 1);
                if (kind == LabPaintArtOperationKind.PaintCells) pendingWriter.Write(stroke);
                pendingWriter.Write(revision); pendingWriter.Write((ushort)1); pendingWriter.Write((ushort)cell);
                LastSequence++; return true;
            }
            catch (Exception e) { Fail("Artwork cell append failed: " + e.Message); return false; }
        }

        /// <summary>Publish the current partial page. Published pages never change.</summary>
        public bool Flush()
        {
            if (disposed || Error != null) return false;
            if (pending.Length == 0) return true;
            try
            {
                byte[] raw;
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(Magic); writer.Write(FormatVersion); writer.Write((ushort)0); writer.Write(pages.Count);
                    writer.Write(pendingFirst); writer.Write(LastSequence); writer.Write(prefix); writer.Write((int)pending.Length);
                    writer.Write(pending.GetBuffer(), 0, (int)pending.Length);
                    byte[] content = stream.ToArray(); writer.Write(Digest(content)); raw = stream.ToArray();
                }
                StorePage(raw, pendingFirst, LastSequence);
                pending.SetLength(0); pending.Position = 0; pendingFirst = 0;
                return true;
            }
            catch (Exception e) { Fail("Artwork journal flush failed: " + e.Message); return false; }
        }

        /// <summary>Caller owns the returned <=4KiB array. No historical queue is materialized.</summary>
        public bool TryReadPage(int index, out byte[] raw)
        {
            raw = null;
            if (disposed || Error != null || index < 0 || index >= pages.Count) return false;
            try
            {
                byte[] cached = ReadStoredPage(index);
                raw = (byte[])cached.Clone();
                return true;
            }
            catch (Exception e) { Fail("Artwork journal read failed: " + e.Message); return false; }
        }

        /// <summary>Contiguous pages only. An identical duplicate is idempotent; malformed data never partly applies.</summary>
        public bool TryImportPage(int index, byte[] raw)
        {
            if (disposed || Error != null || !receiver || raw == null || raw.Length > MaximumPageBytes || raw.Length <= HeaderBytes + FooterBytes
                || index < 0 || index > pages.Count) return false;
            try
            {
                if (index < pages.Count) return SameBytes(ReadStoredPage(index), raw);
                if (IsSealed || rawBytes + raw.Length > maximumBytes)
                { if (rawBytes + raw.Length > maximumBytes) Fail("Artwork journal reached its round storage limit; exact artwork is unavailable."); return false; }
                ulong first, last; var operations = new List<LabPaintArtOperation>();
                if (!ValidatePage(raw, index, LastSequence + 1, prefix, out first, out last, operations)) return false;
                uint nextStroke = lastStroke;
                bool sealedPage = false;
                foreach (var op in operations)
                {
                    if (sealedPage) return false;
                    if (op.Kind == LabPaintArtOperationKind.StrokeBegin)
                    { if (op.StrokeId != nextStroke + 1) return false; nextStroke = op.StrokeId; }
                    else if (op.Kind == LabPaintArtOperationKind.PaintCells && (op.StrokeId == 0 || op.StrokeId > nextStroke)) return false;
                    else if (op.Kind == LabPaintArtOperationKind.RoundSeal) sealedPage = true;
                }
                // Validation is complete before any public state or disk content changes.
                int recordOffset = 0;
                foreach (var op in operations)
                {
                    if (op.Kind == LabPaintArtOperationKind.StrokeBegin) WriteStrokeLocation(op.StrokeId, index, recordOffset);
                    recordOffset += 2 + BitConverter.ToUInt16(raw, HeaderBytes + recordOffset);
                }
                StorePage(raw, first, last);
                LastSequence = last; lastStroke = nextStroke;
                foreach (var op in operations)
                    if (op.Kind == LabPaintArtOperationKind.SurfaceCommit || op.Kind == LabPaintArtOperationKind.RoundSeal)
                    {
                        CommitRevision = op.Revision; CommitSurfaceHash = op.SurfaceHash;
                        if (op.Kind == LabPaintArtOperationKind.RoundSeal)
                        { IsSealed = true; SealRevision = op.Revision; SealSurfaceHash = op.SurfaceHash; }
                    }
                return true;
            }
            catch (Exception e) { Fail("Artwork journal import failed: " + e.Message); return false; }
        }

        /// <summary>Read a bounded ordered batch, including the Host's not-yet-published tail.</summary>
        public bool ReadOperations(ulong after, int maximum, List<LabPaintArtOperation> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            if (disposed || Error != null || maximum < 0 || after > LastSequence) return false;
            if (maximum == 0 || after == LastSequence) return true;
            try
            {
                int low = 0, high = pages.Count;
                while (low < high)
                { int mid = (low + high) / 2; if (pages[mid].Last <= after) low = mid + 1; else high = mid; }
                for (int i = low; i < pages.Count && destination.Count < maximum; i++)
                    DecodeOperations(ReadStoredPage(i), HeaderBytes, after, maximum, destination, pages[i].Length - FooterBytes);
                if (destination.Count < maximum && pending.Length > 0)
                    DecodeOperations(pending.GetBuffer(), 0, after, maximum, destination, (int)pending.Length);
                return true;
            }
            catch (Exception e) { Fail("Artwork journal replay read failed: " + e.Message); return false; }
        }

        /// <summary>Disk index allows very old growth jobs without an unbounded descriptor dictionary.</summary>
        public bool TryGetStroke(uint strokeId, out LabPaintVisualStamp stamp)
        {
            stamp = default;
            if (disposed || Error != null || strokeId == 0 || strokeId > lastStroke) return false;
            try
            {
                strokeIndex.Position = (strokeId - 1L) * 8;
                var reader = new BinaryReader(strokeIndex, Encoding.UTF8, true);
                int page = reader.ReadInt32(), offset = reader.ReadInt32();
                byte[] bytes = page < pages.Count ? ReadStoredPage(page) : pending.GetBuffer();
                int start = (page < pages.Count ? HeaderBytes : 0) + offset;
                if (!TryDecodeRecord(bytes, start, page < pages.Count ? bytes.Length - FooterBytes : (int)pending.Length,
                    out LabPaintArtOperation op, out _) || op.Kind != LabPaintArtOperationKind.StrokeBegin || op.StrokeId != strokeId) return false;
                stamp = op.Stamp; return true;
            }
            catch (Exception e) { Fail("Artwork stroke lookup failed: " + e.Message); return false; }
        }

        private void WriteStrokeLocation(uint id, int page, int offset)
        {
            strokeIndex.Position = (id - 1L) * 8;
            using (var writer = new BinaryWriter(strokeIndex, Encoding.UTF8, true)) { writer.Write(page); writer.Write(offset); }
        }

        private bool ValidatePage(byte[] raw, int index, ulong expectedFirst, byte[] previous,
            out ulong first, out ulong last, List<LabPaintArtOperation> destination)
        {
            first = last = 0;
            byte[] content = new byte[raw.Length - FooterBytes]; Array.Copy(raw, content, content.Length);
            byte[] checksum = new byte[FooterBytes]; Array.Copy(raw, content.Length, checksum, 0, checksum.Length);
            if (!SameBytes(Digest(content), checksum)) return false;
            using (var reader = new BinaryReader(new MemoryStream(raw, false)))
            {
                if (reader.ReadUInt32() != Magic || reader.ReadUInt16() != FormatVersion || reader.ReadUInt16() != 0
                    || reader.ReadInt32() != index) return false;
                first = reader.ReadUInt64(); last = reader.ReadUInt64();
                if (first != expectedFirst || last < first || !SameBytes(reader.ReadBytes(32), previous)
                    || reader.ReadInt32() != raw.Length - HeaderBytes - FooterBytes) return false;
            }
            int offset = HeaderBytes; ulong expected = first;
            while (offset < raw.Length - FooterBytes)
            {
                if (!TryDecodeRecord(raw, offset, raw.Length - FooterBytes, out var op, out int next) || op.Sequence != expected++) return false;
                if (op.Kind == LabPaintArtOperationKind.RoundBegin != (op.Sequence == 1)) return false;
                destination.Add(op); offset = next;
            }
            return offset == raw.Length - FooterBytes && expected - 1 == last;
        }

        private void DecodeOperations(byte[] raw, int start, ulong after, int maximum,
            List<LabPaintArtOperation> destination, int end = -1)
        {
            if (end < 0) end = raw.Length;
            int offset = start;
            while (offset < end && destination.Count < maximum)
            {
                if (!TryDecodeRecord(raw, offset, end, out var op, out int next)) throw new InvalidDataException("Artwork record is corrupt.");
                if (op.Sequence > after) destination.Add(op);
                offset = next;
            }
        }

        private bool TryDecodeRecord(byte[] bytes, int start, int end, out LabPaintArtOperation op, out int next)
        {
            op = default; next = start;
            if (start < 0 || end > bytes.Length || end - start < 11) return false;
            int length = BitConverter.ToUInt16(bytes, start); next = start + 2 + length;
            if (length < 9 || next > end) return false;
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(bytes, start + 2, length, false)))
                {
                    op.Kind = (LabPaintArtOperationKind)reader.ReadByte(); op.Sequence = reader.ReadUInt64();
                    switch (op.Kind)
                    {
                        case LabPaintArtOperationKind.RoundBegin:
                            if (reader.ReadInt32() != Columns || reader.ReadInt32() != Rows
                                || ReadText(reader) != Epoch || ReadText(reader) != StyleVersion) return false;
                            break;
                        case LabPaintArtOperationKind.StrokeBegin:
                            op.StrokeId = reader.ReadUInt32();
                            if (!LabPaintVisualStamp.TryDecode(reader.ReadBytes(LabPaintVisualStamp.ByteSize), out var decoded)
                                || decoded.Length != 1 || decoded[0].sequence != op.StrokeId) return false;
                            op.Stamp = decoded[0]; break;
                        case LabPaintArtOperationKind.PaintCells:
                            op.StrokeId = reader.ReadUInt32(); op.Revision = reader.ReadUInt32();
                            op.Cells = ReadCells(reader); if (op.Cells == null) return false; break;
                        case LabPaintArtOperationKind.EraseCells:
                            op.Revision = reader.ReadUInt32(); op.Cells = ReadCells(reader); if (op.Cells == null) return false; break;
                        case LabPaintArtOperationKind.SurfaceCommit:
                        case LabPaintArtOperationKind.RoundSeal:
                            op.Revision = reader.ReadUInt32(); byte[] hash = reader.ReadBytes(32);
                            if (hash.Length != 32) return false; op.SurfaceHash = Hex(hash); break;
                        default: return false;
                    }
                    return op.Sequence > 0 && reader.BaseStream.Position == reader.BaseStream.Length;
                }
            }
            catch (Exception e) when (e is IOException || e is ArgumentException) { return false; }
        }

        private int[] ReadCells(BinaryReader reader)
        {
            int count = reader.ReadUInt16();
            if (count == 0 || count > (MaximumPageBytes - HeaderBytes - FooterBytes) / 2 || reader.BaseStream.Length - reader.BaseStream.Position != count * 2) return null;
            var result = new int[count];
            for (int i = 0; i < count; i++) { result[i] = reader.ReadUInt16(); if (result[i] >= Columns * Rows) return null; }
            return result;
        }

        private void StorePage(byte[] raw, ulong first, ulong last)
        {
            long offset = file.Length;
            file.Position = offset;
            using (var writer = new BinaryWriter(file, Encoding.UTF8, true)) { writer.Write(raw.Length); writer.Write(raw); }
            file.Flush(); strokeIndex.Flush();
            pages.Add(new PageIndex { Offset = offset, Length = raw.Length, First = first, Last = last });
            rawBytes += raw.Length;
            prefix = Digest(raw);
            Cache(pages.Count - 1, (byte[])raw.Clone());
        }

        private byte[] ReadStoredPage(int index)
        {
            if (cache.TryGetValue(index, out var node))
            { lru.Remove(node); lru.AddFirst(node); return node.Value.Bytes; }
            PageIndex p = pages[index]; file.Position = p.Offset;
            using (var reader = new BinaryReader(file, Encoding.UTF8, true))
            {
                if (reader.ReadInt32() != p.Length) throw new InvalidDataException("Artwork page size changed on disk.");
                byte[] raw = reader.ReadBytes(p.Length);
                if (raw.Length != p.Length) throw new EndOfStreamException();
                // The next immutable page stores this page's SHA-256; the final digest is in memory.
                byte[] expected;
                if (index == pages.Count - 1) expected = prefix;
                else
                {
                    file.Position = pages[index + 1].Offset + 4 + 28;
                    expected = reader.ReadBytes(32);
                }
                if (!SameBytes(Digest(raw), expected)) throw new InvalidDataException("Artwork page digest changed on disk.");
                Cache(index, raw); return raw;
            }
        }

        private void Cache(int index, byte[] raw)
        {
            if (raw.Length > maximumCacheBytes) return;
            while (cacheBytes + raw.Length > maximumCacheBytes && lru.Last != null)
            { var old = lru.Last; lru.RemoveLast(); cache.Remove(old.Value.Index); cacheBytes -= old.Value.Bytes.Length; }
            var node = lru.AddFirst(new CachedPage { Index = index, Bytes = raw }); cache.Add(index, node); cacheBytes += raw.Length;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { file?.Dispose(); } catch (Exception e) { Fail("Artwork journal close failed: " + e.Message); }
            try { strokeIndex?.Dispose(); } catch (Exception e) { Fail("Artwork stroke index close failed: " + e.Message); }
            pendingWriter.Dispose(); pending.Dispose(); cache.Clear(); lru.Clear(); pages.Clear(); cacheBytes = 0;
            try { if (!string.IsNullOrEmpty(path)) { if (createdFile) File.Delete(path); if (createdIndex) File.Delete(path + ".strokes"); } }
            catch (Exception e) { Fail("Artwork journal cleanup failed: " + e.Message); }
        }

        private static bool ValidShape(int c, int r) => (c == 64 && r == 32) || (c == 128 && r == 64) || (c == 256 && r == 128);
        private static bool ValidText(string text) => !string.IsNullOrEmpty(text) && Encoding.UTF8.GetByteCount(text) <= MaximumTextBytes;
        private static void WriteText(BinaryWriter writer, string text)
        { byte[] bytes = Encoding.UTF8.GetBytes(text); writer.Write((ushort)bytes.Length); writer.Write(bytes); }
        private static string ReadText(BinaryReader reader)
        { int count = reader.ReadUInt16(); if (count == 0 || count > MaximumTextBytes) throw new InvalidDataException(); byte[] bytes = reader.ReadBytes(count); if (bytes.Length != count) throw new EndOfStreamException(); return Encoding.UTF8.GetString(bytes); }
        private static byte[] Digest(byte[] raw) { using (var sha = SHA256.Create()) return sha.ComputeHash(raw); }
        private static string Hex(byte[] raw) => BitConverter.ToString(raw).Replace("-", string.Empty).ToLowerInvariant();
        private static bool SameBytes(byte[] a, byte[] b)
        { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        private static bool TryHashBytes(string text, out byte[] bytes)
        {
            bytes = null; if (text == null || text.Length != 64) return false;
            var result = new byte[32];
            for (int i = 0; i < result.Length; i++)
            { if (!byte.TryParse(text.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out result[i])) return false; }
            bytes = result; return true;
        }
        private struct PageIndex { public long Offset; public int Length; public ulong First, Last; }
        private sealed class CachedPage { public int Index; public byte[] Bytes; }
    }
}
