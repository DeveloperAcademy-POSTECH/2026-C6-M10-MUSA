using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    [Serializable]
    public sealed class LabPaintGrowthSettings
    {
        public int Resolution = 2;
        public float InitialPercent = 2f;
        public float RepeatPercent = 4f;
        public float GrowthPercentPerSecond = 6f;
        public float FillChancePercent = 60f;
        public float VerticalWeight = 1.35f;
        public LabPaintPattern Pattern = LabPaintPattern.Splatter;
        public int Style = 0;
        public float Raggedness = .55f;
        public float FlowRatio = .35f;
        public float DripWidthDegrees = 3f;
        public float DripLengthDegrees = 60f;
        public float GravityWeight = 3f;
    }

    /// <summary>
    /// Equal-area sphere cells shared by Host rules and rendering. Growth writes opaque
    /// cells; Stamp retains the translucent brush for isolated offline previews only.
    /// </summary>
    public sealed partial class LabPaintSurface
    {
        // Legacy preview defaults. Sessions use the instance dimensions below.
        public const int Width = 256, Height = 128, PixelCount = Width * Height;
        public const float DefaultRadius = .15f;
        private Color32[] pixels;
        private byte[] painterSlots;
        private Vector3[] points;
        private int[] cellRegions;
        private readonly List<Region> regions = new List<Region>();
        private readonly SortedSet<int> activeRegions = new SortedSet<int>();
        private decimal initialCells, repeatCells, cellsPerSecond, speedCredit, totalBudget;
        private float fillChance, verticalWeight;
        private uint initialSeed, randomState;
        private ulong supplySequence;
        private int lastServed = -1;
        private bool preview, replica;

        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public int CellCount => pixels.Length;
        public int CoveredCount { get; private set; }
        public int PaintCount { get; private set; }
        public uint Revision { get; private set; }
        public float Coverage => CoveredCount / (float)CellCount;
        public bool IsComplete => CoveredCount == CellCount;
        public ulong LastPainter { get; private set; }
        public double PendingCoverage => (double)Math.Min(totalBudget / CellCount,
            (CellCount - CoveredCount) / (decimal)CellCount);
        public int RegionCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < regions.Count; i++) if (regions[i].Parent == i) count++;
                return count;
            }
        }

        public LabPaintSurface() : this(new LabPaintGrowthSettings()) { }
        public LabPaintSurface(LabPaintGrowthSettings settings, uint seed = 1u) => Reset(settings, seed);

        /// <summary>Configure at a round boundary. Invalid settings leave the map unchanged.</summary>
        public void Reset(LabPaintGrowthSettings settings, uint seed)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (settings.Resolution < 0 || settings.Resolution > 2
                || !Finite(settings.InitialPercent) || settings.InitialPercent < .1f || settings.InitialPercent > 100f
                || !Finite(settings.RepeatPercent) || settings.RepeatPercent < 0f || settings.RepeatPercent > 100f
                || !Finite(settings.GrowthPercentPerSecond) || settings.GrowthPercentPerSecond < 0f
                || settings.GrowthPercentPerSecond > 1000f
                || !Finite(settings.FillChancePercent) || settings.FillChancePercent < 0f || settings.FillChancePercent > 100f
                || !Finite(settings.VerticalWeight) || settings.VerticalWeight <= 0f || settings.VerticalWeight > 1000f
                || settings.Pattern < LabPaintPattern.Legacy || settings.Pattern > LabPaintPattern.Flow
                || settings.Style < 0 || settings.Style > 1
                || !Finite(settings.Raggedness) || settings.Raggedness < 0f || settings.Raggedness > 1f
                || !Finite(settings.FlowRatio) || settings.FlowRatio < 0f || settings.FlowRatio > 1f
                || !Finite(settings.DripWidthDegrees) || settings.DripWidthDegrees < .5f || settings.DripWidthDegrees > 12f
                || !Finite(settings.DripLengthDegrees) || settings.DripLengthDegrees < 5f || settings.DripLengthDegrees > 135f
                || !Finite(settings.GravityWeight) || settings.GravityWeight < 1f || settings.GravityWeight > 8f)
                throw new ArgumentOutOfRangeException(nameof(settings), "Invalid paint growth settings.");
            int columns = 64 << settings.Resolution, rows = 32 << settings.Resolution;
            if (pixels == null || Columns != columns || Rows != rows)
            {
                Columns = columns; Rows = rows;
                pixels = new Color32[columns * rows];
                painterSlots = new byte[pixels.Length];
                cellRegions = new int[pixels.Length];
                points = MakePoints(columns, rows);
            }
            initialCells = (decimal)settings.InitialPercent * CellCount / 100m;
            repeatCells = (decimal)settings.RepeatPercent * CellCount / 100m;
            cellsPerSecond = (decimal)settings.GrowthPercentPerSecond * CellCount / 100m;
            fillChance = settings.FillChancePercent / 100f;
            verticalWeight = settings.VerticalWeight;
            pattern = settings.Pattern;
            // Models are also constructed from MonoBehaviour field initializers while Unity
            // deserializes a scene. Resource APIs are illegal there; resolve the mask only for
            // an actual new impact, before that impact changes authoritative state.
            patternMask = null;
            flowStyle = settings.Style;
            raggedness = settings.Raggedness;
            flowRatio = settings.FlowRatio;
            dripHalfWidth = settings.DripWidthDegrees * .5f * Mathf.Deg2Rad;
            dripMaxLength = settings.DripLengthDegrees * Mathf.Deg2Rad;
            gravityWeight = settings.GravityWeight;
            initialSeed = seed;
            Clear();
        }

        /// <summary>
        /// A new seed spends floor(initial area) immediately. A painted hit supplies its
        /// connected component with FIFO growth credit. Cell fractions survive between hits.
        /// A cell crossing supplies uses the final supply's opaque color, connection owner and
        /// P number together, never a blend. Zero P is reserved for neutral offline fixtures.
        /// </summary>
        public bool RegisterHit(Vector3 direction, Color color, ulong attackerId, int painterSlot = 0)
        {
            if (painterSlot < 0 || painterSlot > 5) return false;
            if (preview || replica || IsComplete || !ValidHit(direction, color)) return false;
            int cell = CellFor(direction);
            if (pattern != LabPaintPattern.Legacy && pixels[cell].a == 0 && patternMask == null)
                patternMask = LabPaintPatternMask.Load(pattern);
            if (PaintCount < int.MaxValue) PaintCount++;
            var opaque = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f), 255);
            if (pixels[cell].a != 0)
            {
                Region existing = Root(cellRegions[cell]);
                Supply(existing, repeatCells, opaque, attackerId, (byte)painterSlot);
                RefreshActive(existing);
                return true;
            }
            if (pattern != LabPaintPattern.Legacy)
            {
                RegisterPatternHit(direction.normalized, cell, opaque, attackerId, (byte)painterSlot);
                return true;
            }
            var region = new Region(regions.Count);
            regions.Add(region);
            Supply(region, initialCells, opaque, attackerId, (byte)painterSlot);
            int immediate = (int)Math.Min(decimal.Floor(initialCells), CellCount - CoveredCount);
            region = PaintCell(region, cell);
            InitialPatch patch = flowStyle == 1 ? new InitialPatch(this, direction.normalized, cell) : null;
            for (int i = 1; i < immediate && !IsComplete; i++)
                region = PaintCell(region, patch != null ? patch.Take(region) : ChooseCell(region));
            return true;
        }

        /// <summary>
        /// Spend one global surface-%/second allowance in round-robin region order. Idle time
        /// earns no speed credit. An end-boundary cell can be deferred until the next call.
        /// Returns completion offset, -1 while incomplete, or 0 if already complete.
        /// </summary>
        public double Advance(double elapsedSeconds, bool includeEnd = true)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d
                || elapsedSeconds >= (double)decimal.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (IsComplete) return 0d;
            if (preview || replica || cellsPerSecond == 0m || activeRegions.Count == 0) return -1d;
            decimal duration = (decimal)elapsedSeconds;
            decimal startCredit = speedCredit;
            // Only at most the remaining map can be emitted. Bound arithmetic even for a huge dt.
            decimal usefulDuration = Math.Min(duration, (CellCount - CoveredCount + 1m) / cellsPerSecond);
            decimal work = startCredit + cellsPerSecond * usefulDuration;
            int due = (int)decimal.Floor(work);
            if (!includeEnd && usefulDuration == duration && work == due && due > 0) due--;
            int painted = 0;
            while (painted < due && activeRegions.Count > 0)
            {
                Region region = NextRegion();
                lastServed = region.Id;
                PaintCell(region, pattern == LabPaintPattern.Legacy
                    ? (flowStyle == 1 ? ChooseSpreadFlowCell(region) : ChooseCell(region))
                    : ChoosePatternCell(region));
                painted++;
                if (IsComplete) return (double)((painted - startCredit) / cellsPerSecond);
            }
            // Remainders are retained, but time after finite budget exhaustion is discarded.
            speedCredit = activeRegions.Count > 0 ? work - painted : 0m;
            return -1d;
        }

        private Region NextRegion()
        {
            var after = activeRegions.GetViewBetween(lastServed + 1, int.MaxValue);
            using (var iterator = after.GetEnumerator())
                if (iterator.MoveNext()) return regions[iterator.Current];
            return regions[activeRegions.Min];
        }

        private void Supply(Region region, decimal amount, Color32 color, ulong owner, byte painterSlot)
        {
            // Cap each component independently: a global cap can strand separate cell fractions.
            amount = Math.Max(0m, Math.Min(amount, CellCount - CoveredCount - region.Budget));
            if (amount == 0m) return;
            region.Supplies.Add(++supplySequence, new Ink { Remaining = amount, Color = color, Owner = owner, PainterSlot = painterSlot });
            region.Budget += amount;
            totalBudget += amount;
        }

        private Ink Consume(Region region)
        {
            decimal needed = 1m;
            Ink final = null;
            while (needed > 0m)
            {
                ulong key;
                using (var iterator = region.Supplies.GetEnumerator())
                {
                    if (!iterator.MoveNext()) throw new InvalidOperationException("Missing paint supply.");
                    key = iterator.Current.Key;
                    final = iterator.Current.Value;
                }
                decimal take = Math.Min(needed, final.Remaining);
                needed -= take;
                final.Remaining -= take;
                if (final.Remaining == 0m) region.Supplies.Remove(key);
            }
            region.Budget -= 1m;
            totalBudget -= 1m;
            return final;
        }

        private Region PaintCell(Region region, int cell)
        {
            region = Root(region.Id);
            if (pixels[cell].a != 0 || region.Budget < 1m)
                throw new InvalidOperationException("Growth must fund a new surface cell.");
            Ink ink = Consume(region);
            pixels[cell] = ink.Color;
            painterSlots[cell] = ink.PainterSlot;
            cellRegions[cell] = region.Id;
            region.Remove(cell);
            CoveredCount++;
            LastPainter = ink.Owner;
            unchecked { Revision++; }
            for (int side = 0; side < 4; side++)
            {
                int other = Neighbor(cell, side);
                if (other >= 0 && pixels[other].a != 0)
                {
                    Region touching = Root(cellRegions[other]);
                    if (touching.Id != region.Id) region = Merge(region, touching);
                }
            }
            for (int side = 0; side < 4; side++)
            {
                int next = Neighbor(cell, side);
                if (next >= 0 && pixels[next].a == 0) RefreshCandidate(region, next);
            }
            // A pre-existing component may have listed the new seed as its own frontier
            // before it absorbed the seed's otherwise empty component.
            region.Remove(cell);
            RefreshActive(region);
            if (IsComplete)
            {
                activeRegions.Clear(); totalBudget = speedCredit = 0m;
                for (int i = 0; i < regions.Count; i++)
                {
                    regions[i].Budget = 0m;
                    regions[i].Supplies.Clear();
                }
            }
            return region;
        }

        private Region Merge(Region first, Region second)
        {
            // Small-to-large frontier merging; fixed ID breaks ties deterministically.
            Region keep = first.Count > second.Count || first.Count == second.Count && first.Id < second.Id
                ? first : second;
            Region donor = keep == first ? second : first;
            donor.Parent = keep.Id;
            activeRegions.Remove(donor.Id);
            keep.Budget += donor.Budget;
            donor.Budget = 0m;
            foreach (var pair in donor.Supplies) keep.Supplies.Add(pair.Key, pair.Value);
            donor.Supplies.Clear();
            if (donor.Flow != null && (keep.Flow == null || donor.Flow.Sequence > keep.Flow.Sequence))
                keep.Flow = donor.Flow;
            if (donor.PatternPatch != null && (keep.PatternPatch == null
                || donor.PatternPatch.Sequence > keep.PatternPatch.Sequence))
                keep.PatternPatch = donor.PatternPatch;
            for (int bucket = 0; bucket < 6; bucket++)
            {
                List<int> cells = donor.Buckets[bucket];
                for (int i = 0; i < cells.Count; i++) RefreshCandidate(keep, cells[i]);
            }
            donor.ClearCandidates();
            RefreshActive(keep);
            return keep;
        }

        private void RefreshActive(Region region)
        {
            if (region.Budget >= 1m && region.Count > 0) activeRegions.Add(region.Id);
            else activeRegions.Remove(region.Id);
        }

        private Region Root(int id)
        {
            int root = id;
            while (regions[root].Parent != root) root = regions[root].Parent;
            while (id != root)
            {
                int parent = regions[id].Parent;
                regions[id].Parent = root;
                id = parent;
            }
            return regions[root];
        }

        private void RefreshCandidate(Region region, int cell)
        {
            region.Remove(cell);
            if (pixels[cell].a != 0) return;
            int count = 0, mask = 0;
            for (int side = 0; side < 4; side++)
            {
                int neighbor = Neighbor(cell, side);
                if (neighbor < 0 || pixels[neighbor].a == 0 || Root(cellRegions[neighbor]).Id != region.Id) continue;
                count++;
                mask |= side < 2 ? 1 : 2;
            }
            if (count == 0) return;
            int direction = mask == 1 ? 0 : mask == 2 ? 1 : 2;
            region.Add(cell, (count >= 2 ? 3 : 0) + direction);
        }

        private int ChooseCell(Region region)
        {
            bool fill = NextUnit() < fillChance;
            int start = fill ? 3 : 0;
            if (region.Buckets[start].Count + region.Buckets[start + 1].Count + region.Buckets[start + 2].Count == 0)
                start = 3 - start;
            double both = (1d + verticalWeight) * .5d;
            double horizontalMass = region.Buckets[start].Count;
            double verticalMass = region.Buckets[start + 1].Count * (double)verticalWeight;
            double total = horizontalMass + verticalMass + region.Buckets[start + 2].Count * both;
            if (total <= 0d) throw new InvalidOperationException("An incomplete connected map needs a frontier.");
            double roll = NextUnit() * total;
            int bucket = roll < horizontalMass ? start
                : roll < horizontalMass + verticalMass ? start + 1 : start + 2;
            List<int> cells = region.Buckets[bucket];
            return cells[(int)(NextUInt() % (uint)cells.Count)];
        }

        private int Neighbor(int cell, int side)
        {
            int x = cell % Columns, y = cell / Columns;
            switch (side)
            {
                case 0: return y * Columns + (x + Columns - 1) % Columns;
                case 1: return y * Columns + (x + 1) % Columns;
                case 2: return y > 0 ? cell - Columns : -1;
                default: return y + 1 < Rows ? cell + Columns : -1;
            }
        }

        private int CellFor(Vector3 direction)
        {
            direction.Normalize();
            float u = Mathf.Atan2(direction.z, direction.x) / (2f * Mathf.PI) + .5f;
            int x = Mathf.FloorToInt(u * Columns) % Columns;
            int y = Mathf.Clamp(Mathf.FloorToInt((.5f - direction.y * .5f) * Rows), 0, Rows - 1);
            return y * Columns + x;
        }

        private uint NextUInt()
        {
            uint value = randomState;
            value ^= value << 13; value ^= value >> 17; value ^= value << 5;
            randomState = value;
            return value;
        }
        private double NextUnit() => NextUInt() / 4294967296d;

        public void Clear()
        {
            Array.Clear(pixels, 0, pixels.Length);
            if (painterSlots == null || painterSlots.Length != pixels.Length) painterSlots = new byte[pixels.Length];
            else Array.Clear(painterSlots, 0, painterSlots.Length);
            if (cellRegions == null || cellRegions.Length != pixels.Length) cellRegions = new int[pixels.Length];
            if (points == null || points.Length != pixels.Length) points = MakePoints(Columns, Rows);
            for (int i = 0; i < cellRegions.Length; i++) cellRegions[i] = -1;
            regions.Clear(); activeRegions.Clear();
            CoveredCount = PaintCount = 0;
            LastPainter = 0;
            speedCredit = totalBudget = 0m;
            supplySequence = 0; lastServed = -1;
            randomState = initialSeed == 0u ? 0x9E3779B9u : initialSeed;
            preview = replica = false;
            flowDirection = Vector3.down;
            flowSequence = 0;
            patternSequence = 0;
            unchecked { Revision++; }
        }

        /// <summary>Copy premultiplied RGBA bytes; session cells are transparent or opaque.</summary>
        public void CopyPixels(Color32[] destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (destination.Length != CellCount)
                throw new ArgumentException("A paint map needs exactly Columns * Rows pixels.", nameof(destination));
            Array.Copy(pixels, destination, CellCount);
        }

        /// <summary>Copy stable P1..P5 paint provenance; zero means an unpainted or offline-preview cell.</summary>
        public void CopyPainterSlots(byte[] destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (destination.Length != CellCount)
                throw new ArgumentException("A painter map needs exactly Columns * Rows entries.", nameof(destination));
            Array.Copy(painterSlots, destination, CellCount);
        }

        public byte GetPainterSlot(int cell)
        {
            if (cell < 0 || cell >= CellCount) throw new ArgumentOutOfRangeException(nameof(cell));
            return painterSlots[cell];
        }

        /// <summary>Compatibility path for neutral, offline color-only fixtures.</summary>
        public bool TryReplacePixels(int columns, int rows, uint revision, Color32[] source, int paintCount)
            => TryReplacePixels(columns, rows, revision, source,
                source == null ? null : new byte[source.Length], paintCount);

        /// <summary>
        /// Atomically install a validated Client replica. It has no reconstructed growth
        /// authority. Reset/Clear is needed before the instance can paint a fresh local map.
        /// </summary>
        public bool TryReplacePixels(int columns, int rows, uint revision, Color32[] source, byte[] painterSlots, int paintCount)
        {
            if (!((columns == 64 && rows == 32) || (columns == 128 && rows == 64)
                || (columns == Width && rows == Height)) || source == null
                || source.Length != columns * rows || painterSlots == null
                || painterSlots.Length != source.Length || paintCount < 0) return false;
            int covered = 0;
            for (int i = 0; i < source.Length; i++)
            {
                Color32 value = source[i];
                if (painterSlots[i] > 5 || value.a == 0 && painterSlots[i] != 0) return false;
                if (value.a != 0 && value.a != 255) return false;
                if (value.a == 0 && (value.r != 0 || value.g != 0 || value.b != 0)) return false;
                if (value.a == 255) covered++;
            }
            var replacement = new Color32[source.Length];
            Array.Copy(source, replacement, source.Length);
            var replacementSlots = new byte[painterSlots.Length];
            Array.Copy(painterSlots, replacementSlots, painterSlots.Length);
            decimal oldCount = CellCount;
            initialCells = initialCells / oldCount * replacement.Length;
            repeatCells = repeatCells / oldCount * replacement.Length;
            cellsPerSecond = cellsPerSecond / oldCount * replacement.Length;
            Columns = columns; Rows = rows; pixels = replacement;
            this.painterSlots = replacementSlots;
            points = null; cellRegions = null;
            regions.Clear(); activeRegions.Clear();
            CoveredCount = covered; PaintCount = paintCount; Revision = revision;
            LastPainter = 0; speedCredit = totalBudget = 0m;
            preview = false; replica = true;
            return true;
        }

        /// <summary>Legacy offline brush. Never mixed with authoritative growth or a replica.</summary>
        public bool Stamp(Vector3 direction, Color color, float radius = DefaultRadius)
        {
            if (replica || regions.Count != 0 || !ValidHit(direction, color) || !Finite(radius) || radius <= 0f)
                return false;
            preview = true;
            Vector3 center = direction.normalized * .5f;
            float safeRadius = Mathf.Min(radius, 2f), outer = safeRadius * 1.1f;
            float outerSquared = outer * outer;
            float red = Mathf.Clamp01(color.r), green = Mathf.Clamp01(color.g), blue = Mathf.Clamp01(color.b);
            float opacity = Mathf.Clamp01(color.a) * .93f;
            for (int i = 0; i < pixels.Length; i++)
            {
                Vector3 delta = points[i] - center;
                float distanceSquared = delta.sqrMagnitude;
                if (distanceSquared >= outerSquared) continue;
                float ripple = 1f + .1f * Mathf.Sin(delta.x * 135f + Mathf.Sin(delta.y * 91f))
                    * Mathf.Sin(delta.z * 119f + delta.y * 57f);
                float edge = safeRadius * ripple;
                if (distanceSquared >= edge * edge) continue;
                float t = Mathf.InverseLerp(edge * .83f, edge, Mathf.Sqrt(distanceSquared));
                float alpha = (1f - t * t * (3f - 2f * t)) * opacity;
                Color32 old = pixels[i];
                float inverse = 1f - alpha;
                Color32 next = new Color(red * alpha + old.r / 255f * inverse,
                    green * alpha + old.g / 255f * inverse, blue * alpha + old.b / 255f * inverse,
                    alpha + old.a / 255f * inverse);
                if (next.a == 0) next = default;
                if (old.a == 0 && next.a != 0) CoveredCount++;
                pixels[i] = next;
                painterSlots[i] = 0;
            }
            if (PaintCount < int.MaxValue) PaintCount++;
            unchecked { Revision++; }
            return true;
        }

        private static Vector3[] MakePoints(int columns, int rows)
        {
            var result = new Vector3[columns * rows];
            for (int y = 0; y < rows; y++)
            {
                float vertical = .5f - (y + .5f) / rows;
                float ring = Mathf.Sqrt(.25f - vertical * vertical);
                for (int x = 0; x < columns; x++)
                {
                    float angle = ((x + .5f) / columns - .5f) * (2f * Mathf.PI);
                    result[y * columns + x] = new Vector3(Mathf.Cos(angle) * ring, vertical,
                        Mathf.Sin(angle) * ring);
                }
            }
            return result;
        }

        private sealed class Ink
        {
            public decimal Remaining;
            public Color32 Color;
            public ulong Owner;
            public byte PainterSlot;
        }
        private sealed class Region
        {
            public readonly int Id;
            public int Parent;
            public decimal Budget;
            public FlowTip Flow;
            public PatternPatch PatternPatch;
            public readonly SortedDictionary<ulong, Ink> Supplies = new SortedDictionary<ulong, Ink>();
            public readonly List<int>[] Buckets = new List<int>[6];
            private readonly Dictionary<int, Entry> candidates = new Dictionary<int, Entry>();
            public int Count => candidates.Count;
            public Region(int id)
            {
                Id = Parent = id;
                for (int i = 0; i < Buckets.Length; i++) Buckets[i] = new List<int>();
            }
            public void Add(int cell, int bucket)
            {
                List<int> list = Buckets[bucket];
                candidates.Add(cell, new Entry { Bucket = bucket, Index = list.Count });
                list.Add(cell);
            }
            public void Remove(int cell)
            {
                if (!candidates.TryGetValue(cell, out Entry entry)) return;
                List<int> list = Buckets[entry.Bucket];
                int last = list[list.Count - 1];
                list[entry.Index] = last;
                list.RemoveAt(list.Count - 1);
                candidates.Remove(cell);
                if (last != cell)
                {
                    Entry moved = candidates[last]; moved.Index = entry.Index;
                    candidates[last] = moved;
                }
            }
            public void ClearCandidates()
            {
                candidates.Clear();
                for (int i = 0; i < Buckets.Length; i++) Buckets[i].Clear();
            }
            private struct Entry { public int Bucket, Index; }
        }
        private static bool ValidHit(Vector3 direction, Color color) => Finite(direction) && Finite(color)
            && direction.sqrMagnitude >= .00001f && Finite(direction.sqrMagnitude) && color.a > 0f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(Color value) => Finite(value.r) && Finite(value.g) && Finite(value.b) && Finite(value.a);
    }
}
