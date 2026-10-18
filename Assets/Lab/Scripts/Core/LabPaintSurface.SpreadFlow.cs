using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    public sealed partial class LabPaintSurface
    {
        private const int FrontierSamples = 24;
        private const int MaximumBandCells = 128;
        private int flowStyle;
        private float raggedness, flowRatio, dripHalfWidth, dripMaxLength, gravityWeight;
        private Vector3 flowDirection = Vector3.down;
        private ulong flowSequence;

        /// <summary>Last Host-sampled world-down direction expressed in the painted shell's local space.</summary>
        public Vector3 FlowDirection => flowDirection;

        /// <summary>Changing this input never rewrites pixels, spends ink, or advances time.</summary>
        public bool SetFlowDirection(Vector3 localDown)
        {
            if (!Finite(localDown) || !Finite(localDown.sqrMagnitude) || localDown.sqrMagnitude < .00001f)
                return false;
            flowDirection = localDown.normalized;
            return true;
        }

        private Vector3 CellDirection(int cell) => points[cell] * 2f;

        private static float Arc(Vector3 first, Vector3 second) =>
            Mathf.Acos(Mathf.Clamp(Vector3.Dot(first, second), -1f, 1f));

        // A compact connected seed patch, ordered by geodesic distance with stable, asymmetric
        // irregularity. Its local heap grows only around visited cells, not the full component.
        private sealed class InitialPatch
        {
            private readonly LabPaintSurface surface;
            private readonly Vector3 center;
            private readonly uint salt;
            private readonly List<PatchCell> heap = new List<PatchCell>();
            private readonly HashSet<int> queued = new HashSet<int>();

            public InitialPatch(LabPaintSurface surface, Vector3 center, int seedCell)
            {
                this.surface = surface;
                this.center = center;
                salt = surface.initialSeed ^ unchecked((uint)seedCell * 0x9E3779B9u);
                queued.Add(seedCell);
                Expand(seedCell);
            }

            public int Take(Region region)
            {
                // Existing paint can be traversed to reach a nearby frontier, but cannot make
                // one new-pixel decision scan an arbitrarily large previously painted region.
                for (int traversed = 0; traversed < 64 && heap.Count > 0; traversed++)
                {
                    int cell = Pop();
                    Expand(cell);
                    if (surface.pixels[cell].a == 0) return cell;
                }
                return surface.ChooseCell(region);
            }

            private void Expand(int cell)
            {
                for (int side = 0; side < 4; side++)
                {
                    int next = surface.Neighbor(cell, side);
                    if (next < 0 || !queued.Add(next)) continue;
                    Vector3 direction = surface.CellDirection(next);
                    float noise = StableUnit(unchecked((uint)next) ^ salt);
                    float lobe = Mathf.Sin(Vector3.Dot(direction, new Vector3(5.7f, 8.1f, 3.3f))
                        + (salt & 255u) * .024f);
                    float cost = Arc(center, direction)
                        * (1f + surface.raggedness * (.42f * (noise * 2f - 1f) + .28f * lobe));
                    Push(new PatchCell { Cell = next, Cost = cost });
                }
            }

            private static bool Earlier(PatchCell first, PatchCell second) =>
                first.Cost < second.Cost || first.Cost == second.Cost && first.Cell < second.Cell;

            private void Push(PatchCell item)
            {
                int at = heap.Count;
                heap.Add(item);
                while (at > 0)
                {
                    int parent = (at - 1) / 2;
                    if (!Earlier(item, heap[parent])) break;
                    heap[at] = heap[parent];
                    at = parent;
                }
                heap[at] = item;
            }

            private int Pop()
            {
                int result = heap[0].Cell;
                PatchCell tail = heap[heap.Count - 1];
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

            private struct PatchCell { public int Cell; public float Cost; }
        }

        private static float StableUnit(uint value)
        {
            unchecked
            {
                value ^= value >> 16; value *= 0x7FEB352Du;
                value ^= value >> 15; value *= 0x846CA68Bu;
                value ^= value >> 16;
            }
            return (value & 0xFFFFFFu) / 16777216f;
        }

        private int ChooseSpreadFlowCell(Region region)
        {
            // Keep a non-flow fallback available even with FlowRatio=1. A blocked chain,
            // pole or reached length is never a restriction on the component's total reach.
            if (NextUnit() < flowRatio && TryChooseFlowCell(region, out int flowing)) return flowing;
            return ChooseCell(region);
        }

        private bool TryChooseFlowCell(Region region, out int chosen)
        {
            chosen = -1;
            FlowTip tip = region.Flow;
            if (tip != null)
            {
                while (tip.BandIndex < tip.Band.Count)
                {
                    int bandCell = tip.Band[tip.BandIndex++];
                    if (pixels[bandCell].a == 0 && Touches(region, bandCell))
                    {
                        tip.Sequence = ++flowSequence;
                        chosen = bandCell;
                        return true;
                    }
                }
                if (tip.LastCell >= 0 && pixels[tip.LastCell].a != 0
                    && Root(cellRegions[tip.LastCell]).Id == region.Id && tip.Length < dripMaxLength
                    && ChooseNextTip(tip, out chosen))
                {
                    StartBand(tip, chosen);
                    tip.Sequence = ++flowSequence;
                    return true;
                }
                region.Flow = null;
                // Do not keep attempting a blocked tip in this same decision. Filling its
                // surroundings can free another starting edge on a later growth step.
                return false;
            }

            // Sample bounded component frontier entries. Favor a lower edge of the painted
            // component without inspecting all of its cells every time a drip starts.
            float best = float.NegativeInfinity;
            int parent = -1;
            for (int sample = 0; sample < FrontierSamples; sample++)
            {
                int candidate = SampleFrontier(region);
                int upstream = DownhillParent(region, candidate);
                if (upstream < 0) continue;
                float score = Vector3.Dot(CellDirection(candidate), flowDirection)
                    + raggedness * .08f * StableUnit(unchecked((uint)candidate) ^ initialSeed);
                if (score > best) { best = score; chosen = candidate; parent = upstream; }
            }
            if (chosen < 0 || parent < 0) return false;
            Vector3 from = CellDirection(parent), to = CellDirection(chosen);
            float firstArc = Arc(from, to);
            if (firstArc > dripMaxLength) return false;
            tip = new FlowTip
            {
                LastCell = chosen, Length = firstArc,
                Heading = Vector3.ProjectOnPlane(to - from, to).normalized,
                Sequence = ++flowSequence
            };
            region.Flow = tip;
            StartBand(tip, chosen);
            return true;
        }

        private int DownhillParent(Region region, int candidate)
        {
            int parent = -1;
            Vector3 destination = CellDirection(candidate);
            float destinationHeight = Vector3.Dot(destination, flowDirection);
            for (int side = 0; side < 4; side++)
            {
                int next = Neighbor(candidate, side);
                if (next < 0 || pixels[next].a == 0 || Root(cellRegions[next]).Id != region.Id) continue;
                Vector3 source = CellDirection(next);
                // The lowest available frontier may be uphill of an already-painted polar
                // cap. It is general spreading, not a valid source for a new flowing drip.
                if (destinationHeight <= Vector3.Dot(source, flowDirection) + .000001f
                    || Arc(source, destination) > dripMaxLength) continue;
                if (parent < 0 || Vector3.Dot(source, flowDirection)
                    < Vector3.Dot(CellDirection(parent), flowDirection)) parent = next;
            }
            return parent;
        }

        private int SampleFrontier(Region region)
        {
            int index = (int)(NextUInt() % (uint)region.Count);
            for (int bucket = 0; bucket < 6; bucket++)
            {
                List<int> entries = region.Buckets[bucket];
                if (index < entries.Count) return entries[index];
                index -= entries.Count;
            }
            throw new System.InvalidOperationException("A nonempty paint component needs a frontier entry.");
        }

        private bool ChooseNextTip(FlowTip tip, out int chosen)
        {
            chosen = -1;
            Vector3 current = CellDirection(tip.LastCell);
            Vector3 gravity = Vector3.ProjectOnPlane(flowDirection, current);
            if (gravity.sqrMagnitude < .0000001f) return false;
            gravity.Normalize();
            Vector3 heading = Vector3.ProjectOnPlane(tip.Heading, current).normalized;
            // Only four adjacent choices: the continuation can never teleport across unpainted cells.
            float total = 0f;
            bool downhillAvailable = false;
            for (int side = 0; side < 4; side++)
            {
                int next = Neighbor(tip.LastCell, side);
                if (!EligibleTipCell(tip, current, next, out float distance)) continue;
                float descent = Vector3.Dot(CellDirection(next) - current, flowDirection);
                if (descent < -.000001f) continue;
                if (descent > .000001f) downhillAvailable = true;
                float weight = TipWeight(current, CellDirection(next), gravity, heading);
                total += weight;
                if (NextUnit() * total < weight) chosen = next;
            }
            // A lateral option may soften an active downhill run. It cannot sustain a
            // radial/circumferential fan once the bottom or another obstacle blocks descent.
            if (chosen < 0 || !downhillAvailable) return false;
            Vector3 destination = CellDirection(chosen);
            tip.Length += Arc(current, destination);
            tip.Heading = Vector3.ProjectOnPlane(destination - current, destination).normalized;
            tip.LastCell = chosen;
            return true;
        }

        private bool EligibleTipCell(FlowTip tip, Vector3 current, int next, out float distance)
        {
            distance = 0f;
            if (next < 0 || pixels[next].a != 0) return false;
            distance = Arc(current, CellDirection(next));
            return tip.Length + distance <= dripMaxLength + .000001f;
        }

        private float TipWeight(Vector3 current, Vector3 next, Vector3 gravity, Vector3 heading)
        {
            Vector3 step = Vector3.ProjectOnPlane(next - current, current).normalized;
            return Mathf.Pow(gravityWeight, Vector3.Dot(step, gravity) * 2f)
                * Mathf.Exp(Vector3.Dot(step, heading) * (1.3f - raggedness * .6f));
        }

        private bool Touches(Region region, int cell)
        {
            for (int side = 0; side < 4; side++)
            {
                int next = Neighbor(cell, side);
                if (next >= 0 && pixels[next].a != 0 && Root(cellRegions[next]).Id == region.Id) return true;
            }
            return false;
        }

        private void StartBand(FlowTip tip, int centerCell)
        {
            tip.Band.Clear();
            tip.BandIndex = 0;
            var visited = new HashSet<int> { centerCell };
            var queue = new Queue<int>();
            queue.Enqueue(centerCell);
            Vector3 center = CellDirection(centerCell);
            float minimumDot = Mathf.Cos(dripHalfWidth);
            while (queue.Count > 0 && visited.Count <= MaximumBandCells)
            {
                int current = queue.Dequeue();
                for (int side = 0; side < 4; side++)
                {
                    int next = Neighbor(current, side);
                    if (next < 0 || visited.Contains(next)
                        || Vector3.Dot(center, CellDirection(next)) < minimumDot) continue;
                    // Leave the forward sector available to the next centerline step. A disk
                    // filling all four neighboring cells would otherwise stop its own drip.
                    Vector3 across = Vector3.ProjectOnPlane(CellDirection(next) - center, center);
                    if (across.sqrMagnitude > .000000001f
                        && Vector3.Dot(across.normalized, tip.Heading) > .25f) continue;
                    visited.Add(next);
                    queue.Enqueue(next);
                    if (pixels[next].a == 0) tip.Band.Add(next);
                    if (visited.Count >= MaximumBandCells) break;
                }
                if (visited.Count >= MaximumBandCells) break;
            }
        }

        private sealed class FlowTip
        {
            public int LastCell = -1;
            public float Length;
            public Vector3 Heading;
            public ulong Sequence;
            public readonly List<int> Band = new List<int>();
            public int BandIndex;
        }
    }
}
