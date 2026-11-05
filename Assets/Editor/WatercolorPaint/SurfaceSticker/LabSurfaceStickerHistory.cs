using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>
    /// Disposable still-study adapter over the existing authority journal. This deliberately
    /// handles recorded paid cells only: StrokeBegin does not invent an overlap footprint.
    /// The original surface, journal, gameplay RNG and saved assets are never modified.
    /// It is not a bounded-memory live-game replay implementation.
    /// </summary>
    public sealed class LabSurfaceStickerHistory
    {
        public sealed class Layer
        {
            public uint StrokeId { get; internal set; }
            public LabPaintVisualStamp Stamp { get; internal set; }
            public uint LastRevision { get; internal set; }
            public HashSet<int> Cells { get; } = new HashSet<int>();

            /// <summary>Stable selection: rendering the same operation again never rolls RNG.</summary>
            public int AssetIndex(int variantCount)
            {
                if (variantCount < 1) throw new ArgumentOutOfRangeException(nameof(variantCount));
                uint salt = unchecked(Stamp.seed ^ StrokeId * 0x9e3779b9u);
                salt ^= salt >> 16; salt = unchecked(salt * 0x85ebca6bu); salt ^= salt >> 13;
                return (int)(salt % (uint)variantCount);
            }
        }

        private readonly List<Layer> layers = new List<Layer>();
        private readonly Dictionary<uint, Layer> byId = new Dictionary<uint, Layer>();
        public int Columns { get; }
        public int Rows { get; }
        public int CellCount => checked(Columns * Rows);
        public IReadOnlyList<Layer> Layers => layers;
        public ulong AppliedThrough { get; private set; }
        public int EraseOperationCount { get; private set; }
        public bool IsSealed { get; private set; }

        public LabSurfaceStickerHistory(int columns, int rows)
        {
            if (columns < 1 || rows < 1 || (long)columns * rows > 65536)
                throw new ArgumentOutOfRangeException(nameof(columns));
            Columns = columns; Rows = rows;
        }

        public static LabSurfaceStickerHistory Read(LabPaintArtJournal journal, int batchSize = 256)
        {
            if (journal == null) throw new ArgumentNullException(nameof(journal));
            if (batchSize < 1 || batchSize > 4096) throw new ArgumentOutOfRangeException(nameof(batchSize));
            var history = new LabSurfaceStickerHistory(journal.Columns, journal.Rows);
            var batch = new List<LabPaintArtOperation>(batchSize);
            while (history.AppliedThrough < journal.LastSequence)
            {
                if (!journal.ReadOperations(history.AppliedThrough, batchSize, batch) || batch.Count == 0)
                    throw new InvalidOperationException(journal.Error ?? "The sticker-study journal made no progress.");
                foreach (LabPaintArtOperation operation in batch) history.Apply(operation);
            }
            return history;
        }

        /// <summary>
        /// Input is trusted, ordered decoded journal data. Exact/older deliveries are idempotent;
        /// validation of altered network duplicates belongs to LabPaintArtJournal.TryImportPage.
        /// </summary>
        public void Apply(LabPaintArtOperation operation)
        {
            if (operation.Sequence <= AppliedThrough) return;
            if (operation.Sequence != AppliedThrough + 1 || IsSealed)
                throw new InvalidDataException("Sticker-study operations must be contiguous and unsealed.");
            switch (operation.Kind)
            {
                case LabPaintArtOperationKind.RoundBegin:
                    if (operation.Sequence != 1) throw new InvalidDataException("Unexpected round begin.");
                    break;
                case LabPaintArtOperationKind.StrokeBegin:
                    if (operation.StrokeId == 0 || byId.ContainsKey(operation.StrokeId))
                        throw new InvalidDataException("Duplicate or empty sticker stroke ID.");
                    var layer = new Layer { StrokeId = operation.StrokeId, Stamp = operation.Stamp };
                    byId.Add(operation.StrokeId, layer); layers.Add(layer);
                    break;
                case LabPaintArtOperationKind.PaintCells:
                    if (!byId.TryGetValue(operation.StrokeId, out Layer painted))
                        throw new InvalidDataException("Paint refers to an unknown sticker stroke.");
                    ValidateCells(operation.Cells);
                    foreach (int cell in operation.Cells) painted.Cells.Add(cell);
                    painted.LastRevision = operation.Revision;
                    break;
                case LabPaintArtOperationKind.EraseCells:
                    ValidateCells(operation.Cells);
                    // All historical layers at that location are erased, not just the visible top.
                    foreach (Layer old in layers)
                        foreach (int cell in operation.Cells) old.Cells.Remove(cell);
                    EraseOperationCount++;
                    break;
                case LabPaintArtOperationKind.SurfaceCommit:
                    break;
                case LabPaintArtOperationKind.RoundSeal:
                    IsSealed = true;
                    break;
                default: throw new InvalidDataException("Unknown sticker-study operation.");
            }
            AppliedThrough = operation.Sequence;
        }

        private void ValidateCells(int[] cells)
        {
            if (cells == null) throw new InvalidDataException("Missing sticker cells.");
            foreach (int cell in cells)
                if (cell < 0 || cell >= CellCount) throw new InvalidDataException("Sticker cell is outside the map.");
        }

        public bool[] Coverage()
        {
            var covered = new bool[CellCount];
            foreach (Layer layer in layers) foreach (int cell in layer.Cells) covered[cell] = true;
            return covered;
        }

        /// <summary>Caller owns this point-sampled, binary-authority mask. No smooth extra area.</summary>
        public Texture2D CreateMask(Layer layer)
        {
            if (layer == null || !byId.TryGetValue(layer.StrokeId, out Layer found) || found != layer)
                throw new ArgumentException("Layer does not belong to this history.", nameof(layer));
            var texture = new Texture2D(Columns, Rows, TextureFormat.RGBA32, false, true)
            {
                name = "Surface-sticker paid cells " + layer.StrokeId,
                hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point,
                wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp
            };
            try
            {
                var pixels = new Color32[CellCount];
                foreach (int cell in layer.Cells) pixels[cell] = new Color32(255, 255, 255, 255);
                texture.SetPixels32(pixels); texture.Apply(false, false); return texture;
            }
            catch { Object.DestroyImmediate(texture); throw; }
        }

        /// <summary>Proves recorded masks cover exactly the real final color-map cells.</summary>
        public void AssertMatches(LabPaintSurface surface)
        {
            if (surface == null || surface.Columns != Columns || surface.Rows != Rows)
                throw new ArgumentException("Sticker and authority dimensions differ.", nameof(surface));
            var pixels = new Color32[CellCount]; surface.CopyPixels(pixels);
            bool[] coverage = Coverage(); int count = 0;
            for (int cell = 0; cell < CellCount; cell++)
            {
                if (coverage[cell] != (pixels[cell].a != 0))
                    throw new InvalidOperationException("Sticker history differs from authority at cell " + cell);
                if (coverage[cell]) count++;
            }
            if (count != surface.CoveredCount) throw new InvalidOperationException("Sticker history count differs.");
        }
    }

    /// <summary>Fast history checks without changing a Scene, loading a fixture or creating a texture.</summary>
    public static class LabSurfaceStickerHistoryChecks
    {
        public static string Run()
        {
            var history = new LabSurfaceStickerHistory(8, 4);
            ulong sequence = 0;
            history.Apply(new LabPaintArtOperation { Sequence = ++sequence, Kind = LabPaintArtOperationKind.RoundBegin });
            history.Apply(Begin(++sequence, 1));
            var first = Cells(++sequence, LabPaintArtOperationKind.PaintCells, 1, 0, 1, 2);
            history.Apply(first); history.Apply(first);
            Require(history.Layers[0].Cells.Count == 3 && history.AppliedThrough == sequence, "Duplicate repaint.");
            history.Apply(Begin(++sequence, 2));
            history.Apply(Cells(++sequence, LabPaintArtOperationKind.PaintCells, 2, 0, 2, 3));
            history.Apply(Cells(++sequence, LabPaintArtOperationKind.EraseCells, 0, 0, 2));
            foreach (var layer in history.Layers)
                Require(!layer.Cells.Contains(0) && !layer.Cells.Contains(2), "Erased lower layer survived.");
            history.Apply(Begin(++sequence, 3));
            history.Apply(Cells(++sequence, LabPaintArtOperationKind.PaintCells, 3, 0));
            Require(history.Layers[0].Cells.SetEquals(new[] { 1 })
                && history.Layers[1].Cells.SetEquals(new[] { 3 })
                && history.Layers[2].Cells.SetEquals(new[] { 0 }), "Repaint resurrected lower layers.");
            bool[] covered = history.Coverage();
            Require(covered[0] && covered[1] && !covered[2] && covered[3], "Coverage mismatch.");
            int asset = history.Layers[0].AssetIndex(3);
            Require(asset == history.Layers[0].AssetIndex(3) && asset >= 0 && asset < 3, "Unstable asset selection.");
            bool rejectedGap = false;
            try { history.Apply(Begin(sequence + 2, 4)); }
            catch (InvalidDataException) { rejectedGap = true; }
            Require(rejectedGap, "Out-of-order operation was accepted.");
            return "6 checks passed: duplicate / all-layer erase / repaint / coverage / stable asset / sequence gap";
        }

        private static LabPaintArtOperation Begin(ulong sequence, uint id) => new LabPaintArtOperation {
            Sequence = sequence, Kind = LabPaintArtOperationKind.StrokeBegin, StrokeId = id,
            Stamp = new LabPaintVisualStamp { sequence = id, seed = id * 31u }
        };
        private static LabPaintArtOperation Cells(ulong sequence, LabPaintArtOperationKind kind, uint id, params int[] cells)
            => new LabPaintArtOperation { Sequence = sequence, Kind = kind, StrokeId = id, Cells = cells };
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
