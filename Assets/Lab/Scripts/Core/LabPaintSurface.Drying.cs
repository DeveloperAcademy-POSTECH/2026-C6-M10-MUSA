using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    public sealed partial class LabPaintSurface
    {
        // Fixed Host battle-time boundaries keep erosion independent of render frame rate.
        // Only committed opaque cells are removed; the existing map remains authoritative.
        private const decimal DryingTickSeconds = .1m;
        private bool dryingEnabled;
        private decimal dryingClock, lastDryingTick, dryingDelay, dryingCellsPerSecond, dryingCredit;
        private float nearbyHitMinimumDot;
        private decimal[] lastNearbyHit, exposedAt;
        private readonly SortedSet<int> dryingBoundary = new SortedSet<int>();
        private readonly List<DryCell> dryingHeap = new List<DryCell>();
        private int[] dryingQueued, topologyQueue, previousRoots;
        private int dryingGeneration;

        private static bool ValidDryingSettings(LabPaintGrowthSettings settings) =>
            Finite(settings.DryingDelaySeconds) && settings.DryingDelaySeconds >= 0f && settings.DryingDelaySeconds <= 60f
            && Finite(settings.DryingPercentPerSecond) && settings.DryingPercentPerSecond >= 0f
            && settings.DryingPercentPerSecond <= 1000f
            && Finite(settings.NearbyHitRadiusDegrees) && settings.NearbyHitRadiusDegrees >= 1f
            && settings.NearbyHitRadiusDegrees <= 90f;

        private void ConfigureDrying(LabPaintGrowthSettings settings)
        {
            dryingEnabled = settings.DryingEnabled;
            dryingDelay = (decimal)settings.DryingDelaySeconds;
            dryingCellsPerSecond = (decimal)settings.DryingPercentPerSecond * CellCount / 100m;
            nearbyHitMinimumDot = Mathf.Cos(settings.NearbyHitRadiusDegrees * Mathf.Deg2Rad);
        }

        private void ClearDrying()
        {
            dryingClock = lastDryingTick = dryingCredit = 0m;
            dryingBoundary.Clear(); dryingHeap.Clear(); dryingGeneration = 0;
            if (!dryingEnabled) { ClearDryingReplica(); return; }
            lastNearbyHit = new decimal[CellCount];
            exposedAt = new decimal[CellCount];
            dryingQueued = new int[CellCount];
            topologyQueue = new int[CellCount];
            previousRoots = new int[CellCount];
        }

        private void ClearDryingReplica()
        {
            lastNearbyHit = exposedAt = null;
            dryingQueued = topologyQueue = previousRoots = null;
            dryingBoundary.Clear(); dryingHeap.Clear();
        }

        private void RefreshNearbyPaint(Vector3 hitDirection)
        {
            if (!dryingEnabled) return;
            // This is the real approved hit direction, not a randomly selected growth anchor.
            // All nearby pigment is protected without recoloring it or transferring its P byte.
            for (int i = 0; i < CellCount; i++)
                if (pixels[i].a != 0 && Vector3.Dot(hitDirection, CellDirection(i)) >= nearbyHitMinimumDot)
                    lastNearbyHit[i] = dryingClock;
        }

        private void RecordNewPaint(int cell)
        {
            if (!dryingEnabled) return;
            lastNearbyHit[cell] = dryingClock;
            RefreshDryingBoundary(cell);
            for (int side = 0; side < 4; side++)
            {
                int neighbor = Neighbor(cell, side);
                if (neighbor >= 0) RefreshDryingBoundary(neighbor);
            }
        }

        private void RefreshDryingBoundary(int cell)
        {
            bool boundary = false;
            if (pixels[cell].a != 0)
                for (int side = 0; side < 4; side++)
                {
                    int neighbor = Neighbor(cell, side);
                    if (neighbor >= 0 && pixels[neighbor].a == 0) { boundary = true; break; }
                }
            if (boundary)
            {
                if (dryingBoundary.Add(cell)) exposedAt[cell] = dryingClock;
            }
            else dryingBoundary.Remove(cell);
        }

        /// <summary>
        /// Host battle elapsed time. With drying disabled, use the original growth algorithm
        /// unchanged. With drying enabled, resolve erosion before growth due at the same tick.
        /// Returns the first actual completion offset, preserving the deadline tie rule.
        /// </summary>
        public double Advance(double elapsedSeconds, bool includeEnd = true)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d
                || elapsedSeconds >= (double)decimal.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (!dryingEnabled) return AdvanceGrowth(elapsedSeconds, includeEnd);
            if (IsComplete) return 0d;
            if (preview || replica) return -1d;
            decimal start = dryingClock, end;
            try { end = start + (decimal)elapsedSeconds; }
            catch (OverflowException) { throw new ArgumentOutOfRangeException(nameof(elapsedSeconds)); }
            if (dryingCellsPerSecond == 0m)
            {
                double completion = AdvanceGrowth(elapsedSeconds, includeEnd);
                if (completion < 0d) dryingClock = end;
                return completion;
            }
            while (true)
            {
                if (CoveredCount == 0 && activeRegions.Count == 0)
                {
                    dryingClock = end;
                    lastDryingTick = decimal.Floor(end / DryingTickSeconds) * DryingTickSeconds;
                    dryingCredit = 0m;
                    return -1d;
                }
                decimal nextTick = lastDryingTick + DryingTickSeconds;
                bool tickDue = nextTick <= end;
                decimal segmentEnd = tickDue ? nextTick : end;
                decimal segmentStart = dryingClock;
                double completed = AdvanceGrowth((double)(segmentEnd - segmentStart), tickDue ? false : includeEnd);
                if (completed >= 0d) return (double)(segmentStart - start) + completed;
                dryingClock = segmentEnd;
                if (!tickDue) return -1d;
                ErodeBoundaryTick();
                lastDryingTick = nextTick;
                completed = AdvanceGrowth(0d, nextTick < end || includeEnd);
                if (completed >= 0d) return (double)(nextTick - start);
                // Even a zero-length Advance can finish deferred end-boundary growth above.
                if (nextTick == end) return -1d;
            }
        }

        private void ErodeBoundaryTick()
        {
            dryingHeap.Clear();
            if (dryingGeneration == int.MaxValue)
            { Array.Clear(dryingQueued, 0, dryingQueued.Length); dryingGeneration = 0; }
            dryingGeneration++;
            decimal earliest = dryingClock;
            foreach (int cell in dryingBoundary)
            {
                decimal eligible = Math.Max(lastNearbyHit[cell] + dryingDelay, exposedAt[cell]);
                if (eligible < earliest) earliest = eligible;
                QueueDryCell(cell, 0);
            }
            if (dryingHeap.Count == 0) { dryingCredit = 0m; return; }
            decimal duration = Math.Max(0m, dryingClock - Math.Max(lastDryingTick, earliest));
            decimal work = dryingCredit + duration * dryingCellsPerSecond;
            int due = (int)Math.Min(decimal.Floor(work), CoveredCount);
            int removed = 0;
            while (removed < due && dryingHeap.Count > 0)
            {
                DryCell choice = PopDryCell();
                int cell = choice.Cell;
                if (pixels[cell].a == 0 || !dryingBoundary.Contains(cell)
                    || lastNearbyHit[cell] + dryingDelay > dryingClock) continue;
                pixels[cell] = default;
                painterSlots[cell] = 0;
                cellRegions[cell] = -1;
                dryingBoundary.Remove(cell);
                CoveredCount--; removed++;
                unchecked { Revision++; }
                for (int side = 0; side < 4; side++)
                {
                    int neighbor = Neighbor(cell, side);
                    if (neighbor < 0) continue;
                    RefreshDryingBoundary(neighbor);
                    QueueDryCell(neighbor, choice.Layer + 1);
                }
            }
            // No eligible boundary means no banked idle-time burst on a later impact.
            dryingCredit = dryingHeap.Count > 0 ? work - removed : 0m;
            if (removed > 0) RebuildAfterErosion();
        }

        private void QueueDryCell(int cell, int layer)
        {
            if (pixels[cell].a == 0 || dryingQueued[cell] == dryingGeneration
                || !dryingBoundary.Contains(cell) || lastNearbyHit[cell] + dryingDelay > dryingClock) return;
            dryingQueued[cell] = dryingGeneration;
            PushDryCell(new DryCell
            {
                Cell = cell, Layer = layer, Exposed = exposedAt[cell], LastHit = lastNearbyHit[cell],
                Tie = StableUnit(unchecked((uint)cell) ^ initialSeed ^ 0xC13FA9A9u)
            });
        }

        // Peel older exposed layers before newly exposed interior. Stable tie-breaking does
        // not consume the growth RNG and cannot create holes in an unexposed interior.
        private static bool EarlierDryCell(DryCell a, DryCell b) =>
            a.Exposed < b.Exposed || a.Exposed == b.Exposed && (a.Layer < b.Layer
                || a.Layer == b.Layer && (a.LastHit < b.LastHit || a.LastHit == b.LastHit
                    && (a.Tie < b.Tie || a.Tie == b.Tie && a.Cell < b.Cell)));

        private void PushDryCell(DryCell entry)
        {
            int at = dryingHeap.Count;
            dryingHeap.Add(entry);
            while (at > 0)
            {
                int parent = (at - 1) / 2;
                if (!EarlierDryCell(entry, dryingHeap[parent])) break;
                dryingHeap[at] = dryingHeap[parent]; at = parent;
            }
            dryingHeap[at] = entry;
        }

        private DryCell PopDryCell()
        {
            DryCell result = dryingHeap[0], tail = dryingHeap[dryingHeap.Count - 1];
            dryingHeap.RemoveAt(dryingHeap.Count - 1);
            if (dryingHeap.Count == 0) return result;
            int at = 0;
            while (at * 2 + 1 < dryingHeap.Count)
            {
                int child = at * 2 + 1;
                if (child + 1 < dryingHeap.Count && EarlierDryCell(dryingHeap[child + 1], dryingHeap[child])) child++;
                if (!EarlierDryCell(dryingHeap[child], tail)) break;
                dryingHeap[at] = dryingHeap[child]; at = child;
            }
            dryingHeap[at] = tail;
            return result;
        }

        private struct DryCell
        {
            public int Cell, Layer;
            public decimal Exposed, LastHit;
            public float Tie;
        }

        private void RebuildAfterErosion()
        {
            // Snapshot each unique FIFO supply once. A split must never duplicate its credit,
            // sequence, color or painter. Root IDs below only describe the pre-removal region.
            var jobs = new List<SurvivingSupply>();
            int servedRoot = lastServed >= 0 && lastServed < regions.Count ? Root(lastServed).Id : -1;
            for (int i = 0; i < CellCount; i++)
                previousRoots[i] = pixels[i].a == 0 ? -1 : Root(cellRegions[i]).Id;
            for (int i = 0; i < regions.Count; i++)
                if (regions[i].Parent == i)
                    foreach (Ink ink in regions[i].Supplies.Values)
                        jobs.Add(new SurvivingSupply { Ink = ink, OldRoot = i });
            var oldRegions = regions.ToArray();
            regions.Clear(); activeRegions.Clear();
            for (int i = 0; i < CellCount; i++) cellRegions[i] = -1;
            for (int first = 0; first < CellCount; first++)
            {
                if (pixels[first].a == 0 || cellRegions[first] >= 0) continue;
                var region = new Region(regions.Count) { PatternPatch = oldRegions[previousRoots[first]].PatternPatch };
                regions.Add(region);
                int read = 0, count = 1;
                topologyQueue[0] = first; cellRegions[first] = region.Id;
                while (read < count)
                {
                    int cell = topologyQueue[read++];
                    for (int side = 0; side < 4; side++)
                    {
                        int neighbor = Neighbor(cell, side);
                        if (neighbor < 0 || pixels[neighbor].a == 0 || cellRegions[neighbor] >= 0) continue;
                        cellRegions[neighbor] = region.Id; topologyQueue[count++] = neighbor;
                    }
                }
            }
            // Build the real blank frontier against the new connected components.
            for (int cell = 0; cell < CellCount; cell++)
            {
                if (pixels[cell].a != 0) continue;
                for (int side = 0; side < 4; side++)
                {
                    int neighbor = Neighbor(cell, side);
                    if (neighbor >= 0 && pixels[neighbor].a != 0)
                    {
                        Region region = regions[cellRegions[neighbor]];
                        // Each blank cell can touch the same region on several sides.
                        RefreshCandidate(region, cell);
                    }
                }
            }
            totalBudget = 0m; lastServed = -1;
            // The topology gets new IDs, but its growth scheduler must keep its turn.
            // Resetting this cursor every drying tick would starve later components.
            if (servedRoot >= 0)
                for (int cell = 0; cell < CellCount; cell++)
                    if (previousRoots[cell] == servedRoot) { lastServed = cellRegions[cell]; break; }
            foreach (SurvivingSupply job in jobs)
            {
                Ink ink = job.Ink;
                int nearest = -1;
                float best = float.NegativeInfinity;
                Vector3 center = CellDirection(ink.Anchor);
                if (pixels[ink.Anchor].a != 0 && previousRoots[ink.Anchor] == job.OldRoot)
                    nearest = ink.Anchor;
                else
                    for (int cell = 0; cell < CellCount; cell++)
                    {
                        if (previousRoots[cell] != job.OldRoot) continue;
                        float dot = Vector3.Dot(center, CellDirection(cell));
                        if (dot > best) { best = dot; nearest = cell; }
                    }
                // If no pigment from the old component survives, its unspent supply expires.
                if (nearest < 0) continue;
                Region destination = regions[cellRegions[nearest]];
                destination.Supplies.Add(ink.Sequence, ink);
                destination.Budget += ink.Remaining; totalBudget += ink.Remaining;
                if (destination.Count > 0) ink.Cluster?.Rebase(destination);
            }
            for (int i = 0; i < regions.Count; i++) RefreshActive(regions[i]);
            if (activeRegions.Count == 0) speedCredit = 0m;
        }

        private int NearestFrontier(Region region, Vector3 center)
        {
            int nearest = -1;
            float best = float.NegativeInfinity;
            for (int bucket = 0; bucket < region.Buckets.Length; bucket++)
                foreach (int cell in region.Buckets[bucket])
                {
                    float dot = Vector3.Dot(center, CellDirection(cell));
                    if (dot > best || dot == best && cell < nearest) { best = dot; nearest = cell; }
                }
            if (nearest < 0) throw new InvalidOperationException("A surviving paint supply needs a blank frontier.");
            return nearest;
        }

        private struct SurvivingSupply
        {
            public Ink Ink;
            public int OldRoot;
        }
    }
}
