using System;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Installing a Host map on a Guest without copying it again (#444). <see cref="TryReplacePixels"/> keeps its copying
    /// contract; these paths take ownership of arrays the caller built, or copy into the arrays the surface already has.
    /// All three share the same cell rules and the same replica state.
    /// </summary>
    public sealed partial class LabPaintSurface
    {
        /// <summary>
        /// Take ownership of <paramref name="nextPixels"/> and <paramref name="nextPainters"/> as the new replica map,
        /// with no copy. A replica of the same size at <paramref name="fromRevision"/> checks only
        /// <paramref name="changedCells"/> (strictly increasing, the first <paramref name="changedCount"/> entries) and
        /// derives the covered count from them. The caller guarantees every other cell equals the current map.
        /// Otherwise, or when <paramref name="changedCells"/> is null, every cell is checked. The covered count must
        /// equal <paramref name="expectedCovered"/>. On success the previous arrays come back for reuse when the size
        /// is unchanged, and the caller must not touch the adopted arrays again. On failure nothing changes.
        /// </summary>
        public bool TryAdoptReplica(int columns, int rows, uint fromRevision, uint revision, Color32[] nextPixels,
            byte[] nextPainters, int paintCount, int expectedCovered, LabPaintVisualStamp[] visualStamps,
            int[] changedCells, int changedCount, out Color32[] recycledPixels, out byte[] recycledPainters)
        {
            recycledPixels = null; recycledPainters = null;
            var nextVisualStamps = visualStamps ?? Array.Empty<LabPaintVisualStamp>();
            if (!LabPaintVisualStamp.Valid(nextVisualStamps)) return false;
            if (!ValidReplicaShape(columns, rows, nextPixels, nextPainters, paintCount)) return false;
            // Adopting the surface's own arrays would make the incremental check compare a cell with itself.
            if (ReferenceEquals(nextPixels, pixels) || ReferenceEquals(nextPainters, painterSlots)) return false;
            bool sameSize = columns == Columns && rows == Rows;
            int covered;
            if (replica && sameSize && fromRevision == Revision && changedCells != null)
            {
                if (changedCount < 0 || changedCount > changedCells.Length || changedCount > nextPixels.Length) return false;
                covered = CoveredCount;
                int previous = -1;
                for (int k = 0; k < changedCount; k++)
                {
                    int cell = changedCells[k];
                    if (cell <= previous || cell >= nextPixels.Length) return false;
                    previous = cell;
                    if (!ValidReplicaCell(nextPixels[cell], nextPainters[cell])) return false;
                    covered += (nextPixels[cell].a == 255 ? 1 : 0) - (pixels[cell].a == 255 ? 1 : 0);
                }
            }
            else covered = CountValidReplicaCells(nextPixels, nextPainters);
            if (covered < 0 || covered != expectedCovered) return false;
            Color32[] oldPixels = pixels;
            byte[] oldPainters = painterSlots;
            InstallReplica(columns, rows, revision, nextPixels, nextPainters, covered, paintCount);
            ReplaceVisualStamps(nextVisualStamps);
            if (sameSize) { recycledPixels = oldPixels; recycledPainters = oldPainters; }
            return true;
        }

        /// <summary>
        /// Make this surface a replica of <paramref name="source"/> with <paramref name="paintCount"/>, the same result
        /// as <see cref="TryReplacePixels"/> with the source's maps and stamps. When the size is unchanged it writes into
        /// the arrays this surface already has, and identical stamps keep <see cref="VisualVersion"/>, so a steady
        /// display copy allocates nothing. On failure nothing changes.
        /// </summary>
        public bool TryCopyReplicaFrom(LabPaintSurface source, int paintCount)
        {
            if (source == null || ReferenceEquals(source, this)) return false;
            if (!ValidReplicaShape(source.Columns, source.Rows, source.pixels, source.painterSlots, paintCount)) return false;
            int covered = CountValidReplicaCells(source.pixels, source.painterSlots);
            if (covered < 0) return false;
            LabPaintVisualStamp[] nextVisualStamps = null;
            if (!SameVisualStampsAs(source))
            {
                nextVisualStamps = source.CopyVisualStamps();
                if (!LabPaintVisualStamp.Valid(nextVisualStamps)) return false;
            }
            Color32[] nextPixels = pixels;
            byte[] nextPainters = painterSlots;
            if (source.Columns != Columns || source.Rows != Rows)
            {
                nextPixels = new Color32[source.pixels.Length];
                nextPainters = new byte[source.painterSlots.Length];
            }
            Array.Copy(source.pixels, nextPixels, nextPixels.Length);
            Array.Copy(source.painterSlots, nextPainters, nextPainters.Length);
            InstallReplica(source.Columns, source.Rows, source.Revision, nextPixels, nextPainters, covered, paintCount);
            if (nextVisualStamps != null) ReplaceVisualStamps(nextVisualStamps);
            return true;
        }

        private static bool ValidReplicaShape(int columns, int rows, Color32[] source, byte[] painters, int paintCount)
            => ((columns == 64 && rows == 32) || (columns == 128 && rows == 64) || (columns == Width && rows == Height))
                && source != null && source.Length == columns * rows && painters != null
                && painters.Length == source.Length && paintCount >= 0;

        private static bool ValidReplicaCell(Color32 value, byte painter)
        {
            if (painter > MaxPainterSlot || value.a == 0 && painter != 0) return false;
            if (value.a != 0 && value.a != 255) return false;
            return value.a != 0 || value.r == 0 && value.g == 0 && value.b == 0;
        }

        /// <summary>The number of painted cells, or -1 when any cell breaks the replica rules.</summary>
        private static int CountValidReplicaCells(Color32[] source, byte[] painters)
        {
            int covered = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (!ValidReplicaCell(source[i], painters[i])) return -1;
                if (source[i].a == 255) covered++;
            }
            return covered;
        }

        /// <summary>The replica state every install path leaves: no growth authority, regions, drying or preview.</summary>
        private void InstallReplica(int columns, int rows, uint revision, Color32[] nextPixels, byte[] nextPainters,
            int covered, int paintCount)
        {
            decimal oldCount = CellCount;
            initialCells = initialCells / oldCount * nextPixels.Length;
            repeatCells = repeatCells / oldCount * nextPixels.Length;
            cellsPerSecond = cellsPerSecond / oldCount * nextPixels.Length;
            Columns = columns; Rows = rows; pixels = nextPixels;
            painterSlots = nextPainters;
            points = null; cellRegions = null;
            regions.Clear(); activeRegions.Clear();
            CoveredCount = covered; PaintCount = paintCount; Revision = revision;
            LastPainter = 0; speedCredit = totalBudget = 0m;
            preview = false; replica = true;
            ClearDryingReplica();
        }

        private bool SameVisualStampsAs(LabPaintSurface source)
        {
            if (source.visualStamps.Count != visualStamps.Count) return false;
            for (int i = 0; i < visualStamps.Count; i++)
                if (!SameStamp(source.visualStamps[i], visualStamps[i])) return false;
            return true;
        }
    }
}
