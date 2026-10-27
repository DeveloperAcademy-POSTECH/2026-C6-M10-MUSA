using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    public sealed partial class LabPaintSurface
    {
        private Func<ulong, Vector3, bool> paintBlocker;
        private readonly List<KeyValuePair<ulong, Ink>> blockSupplies = new List<KeyValuePair<ulong, Ink>>();
        private readonly List<ulong> blockFunding = new List<ulong>();

        /// <summary>
        /// Temporary Host-only admission policy. True blocks this owner's new pigment at
        /// the surface direction; it never erases existing paint or disables drying.
        /// Null retains the original simulation and random sequence. Clear/Reset removes it.
        /// </summary>
        public void SetPaintBlocker(Func<ulong, Vector3, bool> predicate)
        {
            if (paintBlocker == predicate) return;
            paintBlocker = predicate;
            // Speed is time, not pigment. A mask change must not release a banked burst;
            // all unspent per-impact area and fractional supplies remain in the ledger.
            speedCredit = 0m;
        }

        /// <summary>Read one committed surface cell without changing paint, ownership or growth.</summary>
        public bool IsPaintedAt(Vector3 direction) => Finite(direction)
            && direction.sqrMagnitude >= .00001f && Finite(direction.sqrMagnitude)
            && pixels[CellFor(direction)].a != 0;

        /// <summary>
        /// Query the exact admission rule used by RegisterHit, including its discrete cell
        /// centre. Invalid directions are not a paint-block decision and still fail normal
        /// hit validation. This query never supplies ink or refreshes drying protection.
        /// </summary>
        public bool WouldBlockHit(ulong owner, Vector3 direction) => Finite(direction)
            && direction.sqrMagnitude >= .00001f && Finite(direction.sqrMagnitude)
            && (PaintBlocked(owner, direction) || PaintCellBlocked(owner, CellFor(direction)));

        private bool PaintBlocked(ulong owner, Vector3 direction) =>
            paintBlocker != null && paintBlocker(owner, direction.normalized);

        private bool PaintCellBlocked(ulong owner, int cell) => PaintBlocked(owner, CellDirection(cell));

        private void RegisterBlockedLegacyHit(Vector3 direction, int cell, Color32 color,
            ulong owner, byte painterSlot, decimal initial)
        {
            var region = new Region(regions.Count);
            regions.Add(region);
            Ink ink = Supply(region, initial, color, owner, painterSlot);
            int immediate = (int)Math.Min(decimal.Floor(initial), CellCount - CoveredCount);
            region = PaintFundedCell(region, cell, ink);
            InitialPatch patch = flowStyle == 1 ? new InitialPatch(this, direction, cell) : null;
            for (int i = 1; i < immediate && !IsComplete; i++)
            {
                int candidate = patch != null ? patch.Take(region) : ChooseCell(region);
                if (PaintCellBlocked(owner, candidate)) break;
                region = PaintFundedCell(region, candidate, ink);
            }
        }

        private double AdvanceBlockedGrowth(double elapsedSeconds, bool includeEnd)
        {
            decimal duration = (decimal)elapsedSeconds;
            decimal segmentStart = dryingClock;
            decimal startCredit = speedCredit;
            decimal usefulDuration = Math.Min(duration, (CellCount - CoveredCount + 1m) / cellsPerSecond);
            decimal work = startCredit + cellsPerSecond * usefulDuration;
            int due = (int)decimal.Floor(work);
            if (!includeEnd && usefulDuration == duration && work == due && due > 0) due--;
            int painted = 0;
            bool stalled = false;
            while (painted < due && activeRegions.Count > 0)
            {
                // A blocked root or FIFO owner cannot prevent another region/owner painting.
                int rootsToTry = activeRegions.Count;
                bool committed = false;
                while (rootsToTry-- > 0)
                {
                    Region region = NextRegion();
                    lastServed = region.Id;
                    if (!TryOpenGrowth(region, out int cell, out List<ulong> funding)) continue;
                    if (dryingEnabled)
                        dryingClock = segmentStart + Math.Max(0m, (painted + 1m - startCredit) / cellsPerSecond);
                    PaintFundedCell(region, cell, null, funding);
                    painted++; committed = true;
                    if (IsComplete) return (double)((painted - startCredit) / cellsPerSecond);
                    break;
                }
                if (!committed) { stalled = true; break; }
            }
            // A blocked second earns no later paint speed. Pigment itself is not consumed.
            speedCredit = !stalled && activeRegions.Count > 0 ? work - painted : 0m;
            return -1d;
        }

        private bool TryOpenGrowth(Region region, out int cell, out List<ulong> funding)
        {
            cell = -1; funding = null;
            blockSupplies.Clear();
            foreach (var pair in region.Supplies) blockSupplies.Add(pair);
            List<KeyValuePair<ulong, Ink>> ordered = blockSupplies;
            // Try the oldest runnable contiguous ledger slice. The final funding Ink still
            // owns the cell, even when several fractional supplies jointly pay for it.
            for (int start = 0; start < ordered.Count; start++)
            {
                decimal amount = 0m;
                for (int end = start; end < ordered.Count; end++)
                {
                    amount += ordered[end].Value.Remaining;
                    if (amount < 1m) continue;
                    Ink final = ordered[end].Value;
                    if (!HasOpenFrontier(region, final.Owner)) break;
                    if (final.Cluster == null)
                    {
                        int anchor = ChooseCell(region);
                        if (PaintCellBlocked(final.Owner, anchor))
                            anchor = NearestOpenFrontier(region, CellDirection(anchor), final.Owner);
                        final.Anchor = anchor;
                        final.Cluster = new GrowthCluster(this, anchor, final.Sequence, final.Remaining);
                    }
                    int next = final.Cluster.Take(region, final.Owner);
                    if (next < 0) break;
                    blockFunding.Clear();
                    for (int i = start; i <= end; i++) blockFunding.Add(ordered[i].Key);
                    funding = blockFunding;
                    cell = next;
                    return true;
                }
            }
            return false;
        }

        private bool HasOpenFrontier(Region region, ulong owner)
        {
            for (int bucket = 0; bucket < region.Buckets.Length; bucket++)
                foreach (int cell in region.Buckets[bucket])
                    if (!PaintCellBlocked(owner, cell)) return true;
            return false;
        }

        private int NearestOpenFrontier(Region region, Vector3 center, ulong owner)
        {
            int best = -1;
            float dot = float.NegativeInfinity;
            for (int bucket = 0; bucket < region.Buckets.Length; bucket++)
                foreach (int cell in region.Buckets[bucket])
                {
                    if (PaintCellBlocked(owner, cell)) continue;
                    float candidate = Vector3.Dot(center, CellDirection(cell));
                    if (candidate > dot || candidate == dot && cell < best)
                    { best = cell; dot = candidate; }
                }
            return best;
        }

        private Ink ConsumeSelected(Region region, List<ulong> funding)
        {
            decimal needed = 1m;
            Ink final = null;
            foreach (ulong sequence in funding)
            {
                if (needed == 0m) break;
                if (!region.Supplies.TryGetValue(sequence, out final))
                    throw new InvalidOperationException("A selected paint supply disappeared before commit.");
                decimal take = Math.Min(needed, final.Remaining);
                needed -= take;
                final.Remaining -= take;
                if (final.Remaining == 0m)
                {
                    region.Supplies.Remove(sequence);
                    if (needed > 0m) final.Cluster = null;
                }
            }
            if (needed != 0m) throw new InvalidOperationException("Selected paint supplies must fund one cell.");
            region.Budget -= 1m;
            totalBudget -= 1m;
            return final;
        }
    }
}
