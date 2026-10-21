using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    public sealed partial class LabPaintSurface
    {
        private int flowStyle;
        private float raggedness;
        private Vector3 flowDirection = Vector3.down;

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

        private bool Touches(Region region, int cell)
        {
            for (int side = 0; side < 4; side++)
            {
                int next = Neighbor(cell, side);
                if (next >= 0 && pixels[next].a != 0 && Root(cellRegions[next]).Id == region.Id) return true;
            }
            return false;
        }

    }
}
