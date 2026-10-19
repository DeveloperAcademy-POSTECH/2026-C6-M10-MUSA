using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    public sealed partial class LabPaintSurface
    {
        private float clusterCompactness, clusterIrregularity;

        private int ChooseClusterCell(Region region)
        {
            // Read the ledger without spending it: fractional crossing cells belong to the
            // final funding Ink for geometry, opaque color and painter provenance together.
            decimal needed = 1m;
            foreach (var pair in region.Supplies)
            {
                Ink ink = pair.Value;
                needed -= ink.Remaining;
                if (needed > 0m) continue;
                // Initial footprints keep their original immediate choices. A merge can
                // leave initial ink for later growth, so only that uncommon case anchors here.
                if (ink.Cluster == null)
                    ink.Cluster = new GrowthCluster(this, ink.Anchor = ChooseCell(region), pair.Key, ink.Remaining);
                return ink.Cluster.Take(region);
            }
            throw new InvalidOperationException("Missing paint supply for cluster growth.");
        }

        // One supply owns one stable blob. Only committed paint opens further blank cells;
        // existing paint in the current union can be crossed to reach another white pocket.
        // Neither a preferred radius nor a traversal limit restricts eventual surface reach.
        private sealed class GrowthCluster
        {
            private readonly LabPaintSurface surface;
            private int anchor;
            private readonly Vector3 center, waveA, waveB;
            private readonly float phaseA, phaseB, cellAngle;
            private readonly PatternPatch patch;
            private readonly List<Entry> heap = new List<Entry>();
            private readonly Dictionary<int, Entry> queued = new Dictionary<int, Entry>();
            private readonly HashSet<int> seen = new HashSet<int>();
            private int nextVersion, pendingCell = -1;
            private bool started;

            // Keep this supply's shape, size and original center while discarding traversal
            // through cells that a drying boundary may have removed or disconnected.
            public void Rebase(Region region)
            {
                heap.Clear(); queued.Clear(); seen.Clear();
                pendingCell = -1; nextVersion = 0; started = false;
                anchor = surface.NearestFrontier(region, center);
            }

            public GrowthCluster(LabPaintSurface surface, int anchor, ulong sequence, decimal budget)
            {
                this.surface = surface;
                this.anchor = anchor;
                center = surface.CellDirection(anchor);
                uint salt = surface.initialSeed ^ unchecked((uint)sequence * 0x9E3779B9u)
                    ^ unchecked((uint)(sequence >> 32) * 0x85EBCA6Bu);
                waveA = WaveAxis(salt ^ 0xA511E9B3u);
                waveB = WaveAxis(salt ^ 0x63D83595u);
                phaseA = StableUnit(salt ^ 0xB5297A4Du) * Mathf.PI * 2f;
                phaseB = StableUnit(salt ^ 0x68E31DA4u) * Mathf.PI * 2f;
                cellAngle = Mathf.Sqrt(4f * Mathf.PI / surface.CellCount);
                if (surface.pattern != LabPaintPattern.Legacy)
                {
                    // The same mask and rotation family shape initial and repeated bursts.
                    // Its scale is fixed from this job's amount; older paint cannot enlarge
                    // the descriptor in search of far-away white cells.
                    patch = new PatternPatch(surface.patternMask, center,
                        surface.pattern == LabPaintPattern.Flow ? -surface.flowDirection : Vector3.up,
                        surface.pattern == LabPaintPattern.Flow ? 0f
                            : (StableUnit(salt) - .5f) * Mathf.PI * 2f, sequence, salt);
                    patch.Radius = Mathf.Max(cellAngle, Mathf.Sqrt((float)(budget / surface.CellCount)
                        * Mathf.PI / surface.patternMask.FootprintFraction));
                }
            }

            public int Take(Region region)
            {
                if (!started)
                {
                    started = true;
                    Queue(region, anchor);
                }
                while (heap.Count > 0)
                {
                    Entry entry = Pop();
                    if (!queued.TryGetValue(entry.Cell, out Entry current) || current.Version != entry.Version)
                        continue;
                    queued.Remove(entry.Cell);
                    if (surface.pixels[entry.Cell].a != 0)
                    {
                        if (surface.Root(surface.cellRegions[entry.Cell]).Id == region.Id)
                            Expand(region, entry.Cell);
                        continue;
                    }
                    if (!surface.Touches(region, entry.Cell)) continue;
                    pendingCell = entry.Cell;
                    return entry.Cell;
                }
                // The heap can traverse the entire connected painted root; an incomplete
                // root with a funded frontier must always expose an adjacent blank cell.
                throw new InvalidOperationException("A paint cluster lost its reachable frontier.");
            }

            public void Commit(Region region, int cell)
            {
                if (pendingCell != cell) return;
                pendingCell = -1;
                Expand(region, cell);
                if (heap.Count > queued.Count * 3 + 64) CompactHeap();
            }

            private void Expand(Region region, int cell)
            {
                for (int side = 0; side < 4; side++)
                {
                    int next = surface.Neighbor(cell, side);
                    if (next >= 0) Queue(region, next);
                }
            }

            private void Queue(Region region, int cell)
            {
                bool painted = surface.pixels[cell].a != 0;
                if (painted && surface.Root(surface.cellRegions[cell]).Id != region.Id) return;
                bool fresh = seen.Add(cell);
                if (!fresh && !queued.TryGetValue(cell, out _)) return;
                Entry entry;
                if (fresh) entry = new Entry { Cell = cell, BaseCost = GeometryCost(cell) };
                else entry = queued[cell];
                float cost = entry.BaseCost + (painted ? 0f : ContactCost(region, cell));
                if (!fresh && entry.Cost == cost) return;
                entry.Cost = cost;
                entry.Version = ++nextVersion;
                queued[cell] = entry;
                Push(entry);
            }

            private float GeometryCost(int cell)
            {
                Vector3 direction = surface.CellDirection(cell);
                // Continuous low-frequency waves give an organic outline without cell hash
                // noise, UV axes, animated resampling or a long thin directional preference.
                float noise = (Mathf.Sin(Vector3.Dot(direction, waveA) * 6f + phaseA)
                    + .5f * Mathf.Sin(Vector3.Dot(direction, waveB) * 9f + phaseB)) / 1.5f;
                float variation = surface.clusterIrregularity * (.4f - .15f * surface.clusterCompactness);
                float distance = Arc(center, direction);
                float shape = 1f;
                if (patch != null)
                {
                    // A radial, softly weighted central-body envelope retains the selected
                    // splash's lobes at every growth amount. It never forbids a cell, so a
                    // nearly complete map can still reach its last separated white pocket.
                    // Coarse angular filtering lives in PatternPatch rather than cell noise.
                    float locality = 1f - Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(patch.Radius * 2f, patch.Radius * 4f, distance));
                    float strength = (.85f + .15f * surface.raggedness) * locality;
                    shape = Mathf.Lerp(1f, patch.ClusterShapeFactor(direction), strength);
                }
                return distance * shape * (1f + variation * noise);
            }

            private float ContactCost(Region region, int cell)
            {
                int contacts = 0;
                for (int side = 0; side < 4; side++)
                {
                    int next = surface.Neighbor(cell, side);
                    if (next >= 0 && surface.pixels[next].a != 0
                        && surface.Root(surface.cellRegions[next]).Id == region.Id) contacts++;
                }
                // Favor broad attachment and close concavities while keeping distance the
                // main shape order. This is a preference, so single-contact growth remains legal.
                float penalty = contacts >= 4 ? -1.1f : contacts == 3 ? -.7f
                    : contacts == 2 ? .2f : 1.5f;
                return cellAngle * surface.clusterCompactness * penalty;
            }

            private static Vector3 WaveAxis(uint salt)
            {
                Vector3 axis = new Vector3(StableUnit(salt) * 2f - 1f,
                    StableUnit(salt ^ 0x9E3779B9u) * 2f - 1f,
                    StableUnit(salt ^ 0x85EBCA6Bu) * 2f - 1f);
                return axis.sqrMagnitude < .000001f ? Vector3.right : axis.normalized;
            }

            private void CompactHeap()
            {
                heap.Clear();
                foreach (var pair in queued) Push(pair.Value);
            }

            private static bool Earlier(Entry first, Entry second) => first.Cost < second.Cost
                || first.Cost == second.Cost && (first.Cell < second.Cell
                    || first.Cell == second.Cell && first.Version < second.Version);

            private void Push(Entry entry)
            {
                int at = heap.Count;
                heap.Add(entry);
                while (at > 0)
                {
                    int parent = (at - 1) / 2;
                    if (!Earlier(entry, heap[parent])) break;
                    heap[at] = heap[parent];
                    at = parent;
                }
                heap[at] = entry;
            }

            private Entry Pop()
            {
                Entry result = heap[0], tail = heap[heap.Count - 1];
                heap.RemoveAt(heap.Count - 1);
                if (heap.Count == 0) return result;
                int at = 0;
                while (at * 2 + 1 < heap.Count)
                {
                    int child = at * 2 + 1;
                    if (child + 1 < heap.Count && Earlier(heap[child + 1], heap[child])) child++;
                    if (!Earlier(heap[child], tail)) break;
                    heap[at] = heap[child];
                    at = child;
                }
                heap[at] = tail;
                return result;
            }

            private struct Entry
            {
                public int Cell, Version;
                public float BaseCost, Cost;
            }
        }
    }
}
