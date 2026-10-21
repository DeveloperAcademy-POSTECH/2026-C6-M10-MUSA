using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    public sealed partial class LabPaintSurface
    {
        private LabPaintPattern pattern;
        private LabPaintPatternMask patternMask;
        /// <summary>Local growth setting. A pixel-only display replica takes its pattern name from shared LabConfig.</summary>
        public LabPaintPattern Pattern => pattern;

        private void RegisterPatternHit(Vector3 direction, int hitCell, Color32 color, ulong owner, byte painterSlot,
            decimal initial)
        {
            int requested = (int)Math.Min(decimal.Floor(initial), CellCount - CoveredCount);
            uint salt = initialSeed ^ unchecked((uint)PaintCount * 0x9E3779B9u);
            var patch = new PatternPatch(patternMask, direction, pattern == LabPaintPattern.Flow
                ? -flowDirection : Vector3.up, pattern == LabPaintPattern.Flow ? 0f
                : (StableUnit(salt) - .5f) * Mathf.PI * 2f, salt);
            bool[] footprint = MakePatternFootprint(patch, hitCell, (int)decimal.Floor(initial));

            // An impact may seed detached splashes, but those are not one connected region.
            // Each receives only the integer ink it immediately spends. The impact component
            // retains the fractional initial credit, matching ordinary connected seed semantics.
            var visited = new bool[CellCount];
            var queue = new Queue<int>();
            var component = new List<int>();
            decimal fraction = requested == decimal.Floor(initial)
                ? initial - requested : 0m;
            PaintPatternComponent(hitCell, footprint, visited, queue, component, color, owner, painterSlot, fraction);
            for (int i = 0; i < footprint.Length; i++)
                if (footprint[i] && !visited[i])
                    PaintPatternComponent(i, footprint, visited, queue, component, color, owner, painterSlot, 0m);

            // Size the splash by the impact's area, not by how many white cells remain under
            // it. Overlap never stretches one splash across distant holes to recover its quota.
            // Its unspent whole cells continue locally at the usual timed growth rate instead.
            int immediate = 0;
            for (int i = 0; i < footprint.Length; i++) if (footprint[i]) immediate++;
            int shortfall = requested - immediate;
            if (shortfall > 0 && !IsComplete)
            {
                Region root = Root(cellRegions[hitCell]);
                Supply(root, shortfall, color, owner, painterSlot, true, hitCell);
                RefreshActive(root);
            }
        }

        private void PaintPatternComponent(int first, bool[] footprint, bool[] visited,
            Queue<int> queue, List<int> component, Color32 color, ulong owner, byte painterSlot, decimal fraction)
        {
            component.Clear(); queue.Clear();
            visited[first] = true; queue.Enqueue(first);
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                component.Add(cell);
                for (int side = 0; side < 4; side++)
                {
                    int next = Neighbor(cell, side);
                    if (next < 0 || visited[next] || !footprint[next]) continue;
                    visited[next] = true; queue.Enqueue(next);
                }
            }
            var region = new Region(regions.Count);
            regions.Add(region);
            Ink immediateInk = Supply(region, component.Count + fraction, color, owner, painterSlot);
            // BFS order is connected inside this component. PaintCell also merges components
            // that are joined by pre-existing opaque paint. Debit this footprint's own ink;
            // older queued growth must keep its frozen location, color and painter together.
            for (int i = 0; i < component.Count; i++) region = PaintFundedCell(region, component[i], immediateInk);
        }

        private bool[] MakePatternFootprint(PatternPatch patch, int hitCell, int requested)
        {
            var planar = new Vector2[CellCount];
            for (int i = 0; i < planar.Length; i++) planar[i] = patch.Project(CellDirection(i));
            float estimate = Mathf.Sqrt(requested / (float)CellCount * Mathf.PI / patternMask.FootprintFraction);
            float low = 0f, high = Mathf.Max(.02f, estimate), bestRadius = high;
            int bestCount = int.MaxValue;
            // Calibrate the complete mask including already painted cells. This preserves the
            // virgin-surface footprint and keeps a late white hit local rather than inflating
            // until it finds the last scattered white cells across the whole gourd.
            for (int attempt = 0; attempt < 32; attempt++)
            {
                int count = CountPattern(planar, high, hitCell);
                if (count >= requested) { bestRadius = high; bestCount = count; break; }
                low = high; high *= 1.4f;
            }
            if (bestCount == int.MaxValue)
                throw new InvalidOperationException("A runtime paint mask cannot supply the configured initial area.");
            for (int attempt = 0; attempt < 15; attempt++)
            {
                float radius = (low + high) * .5f;
                int count = CountPattern(planar, radius, hitCell);
                if (count >= requested)
                {
                    high = radius;
                    if (count < bestCount) { bestRadius = radius; bestCount = count; }
                }
                else low = radius;
            }
            patch.Radius = bestRadius;
            var footprint = new bool[CellCount];
            for (int i = 0; i < footprint.Length; i++)
                footprint[i] = i == hitCell || PatternInk(planar[i], bestRadius);

            // Correct discrete cell rounding at the outer boundary, never by ranking all ink
            // brightness. Thin endpoints and isolated splashes are removed after thick corners.
            int excess = bestCount - requested;
            if (excess > 0)
            {
                var boundary = new PatternBoundaryHeap();
                for (int i = 0; i < footprint.Length; i++)
                    AddPatternBoundary(boundary, footprint, i, hitCell, patch.Salt);
                while (excess-- > 0)
                {
                    int cell;
                    do
                    {
                        if (boundary.Count == 0)
                            throw new InvalidOperationException("Runtime paint boundary correction exhausted.");
                        cell = boundary.Pop();
                    } while (!footprint[cell] || PatternNeighborCount(footprint, cell) == 4);
                    footprint[cell] = false;
                    for (int side = 0; side < 4; side++)
                    {
                        int next = Neighbor(cell, side);
                        if (next >= 0) AddPatternBoundary(boundary, footprint, next, hitCell, patch.Salt);
                    }
                }
            }
            for (int i = 0; i < footprint.Length; i++) if (pixels[i].a != 0) footprint[i] = false;
            return footprint;
        }

        private int CountPattern(Vector2[] planar, float radius, int hitCell)
        {
            int count = 0;
            for (int i = 0; i < planar.Length; i++)
                if (i == hitCell || PatternInk(planar[i], radius)) count++;
            return count;
        }

        private bool PatternInk(Vector2 point, float radius) =>
            patternMask.Sample(.5f + point.x / (2f * radius), .5f + point.y / (2f * radius))
                >= LabPaintPatternMask.Threshold;

        private int PatternNeighborCount(bool[] footprint, int cell)
        {
            int count = 0;
            for (int side = 0; side < 4; side++)
            {
                int next = Neighbor(cell, side);
                if (next >= 0 && footprint[next]) count++;
            }
            return count;
        }

        private void AddPatternBoundary(PatternBoundaryHeap heap, bool[] footprint, int cell, int protectedCell, uint salt)
        {
            if (cell == protectedCell || !footprint[cell]) return;
            int count = PatternNeighborCount(footprint, cell);
            if (count == 4) return;
            bool straight = count == 2 && ((footprint[Neighbor(cell, 0)] && footprint[Neighbor(cell, 1)])
                || (Neighbor(cell, 2) >= 0 && Neighbor(cell, 3) >= 0
                    && footprint[Neighbor(cell, 2)] && footprint[Neighbor(cell, 3)]));
            int category = count == 3 ? 0 : count == 2 && !straight ? 1 : count == 1 ? 3 : count == 2 ? 4 : 5;
            uint hash = PatternHash(unchecked((uint)cell) ^ salt);
            heap.Push(cell, ((ulong)category << 32) | hash);
        }

        private static uint PatternHash(uint value)
        {
            unchecked
            {
                value ^= value >> 16; value *= 0x7FEB352Du;
                value ^= value >> 15; value *= 0x846CA68Bu;
                return value ^ (value >> 16);
            }
        }

        private sealed class PatternPatch
        {
            private const int ClusterAngles = 128;
            public readonly uint Salt;
            public float Radius;
            private readonly LabPaintPatternMask mask;
            private readonly Vector3 center, right, up;
            private readonly float cosine, sine;
            private float[] clusterRadii;
            private float meanClusterRadius;
            public PatternPatch(LabPaintPatternMask mask, Vector3 center, Vector3 upHint,
                float rotation, uint salt)
            {
                this.mask = mask; this.center = center; Salt = salt;
                Vector3 tangent = Vector3.ProjectOnPlane(upHint, center);
                if (tangent.sqrMagnitude < .000001f) tangent = Vector3.ProjectOnPlane(Vector3.forward, center);
                if (tangent.sqrMagnitude < .000001f) tangent = Vector3.ProjectOnPlane(Vector3.right, center);
                up = tangent.normalized; right = Vector3.Cross(up, center).normalized;
                cosine = Mathf.Cos(rotation); sine = Mathf.Sin(rotation);
            }
            public Vector2 Project(Vector3 direction)
            {
                float dot = Mathf.Clamp(Vector3.Dot(center, direction), -1f, 1f);
                float angle = Mathf.Acos(dot), length = Mathf.Sqrt(Mathf.Max(0f, 1f - dot * dot));
                if (length < .000001f) return dot < 0f ? new Vector2(Mathf.PI, 0f) : Vector2.zero;
                float scale = angle / length;
                float x = Vector3.Dot(direction, right) * scale, y = Vector3.Dot(direction, up) * scale;
                return new Vector2(x * cosine + y * sine, -x * sine + y * cosine);
            }
            public float ClusterShapeFactor(Vector3 direction)
            {
                if (clusterRadii == null) BuildClusterRadii();
                Vector2 point = Project(direction);
                float position = (Mathf.Atan2(point.y, point.x) / (2f * Mathf.PI) + 1f) * ClusterAngles;
                int first = Mathf.FloorToInt(position) % ClusterAngles;
                float radius = Mathf.Lerp(clusterRadii[first], clusterRadii[(first + 1) % ClusterAngles],
                    position - Mathf.Floor(position));
                return meanClusterRadius / radius;
            }

            private void BuildClusterRadii()
            {
                // Keep the central splash's broad lobes from the same runtime mask as the
                // first hit. A sustained empty run ends a ray: detached satellite dots and
                // sub-pixel nicks must not turn connected growth into spikes or many islands.
                var sampled = new float[ClusterAngles];
                for (int angle = 0; angle < sampled.Length; angle++)
                {
                    float radians = angle * (2f * Mathf.PI / ClusterAngles);
                    Vector2 axis = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                    float lastInk = .04f;
                    int empty = 0;
                    for (int step = 1; step <= 128; step++)
                    {
                        float distance = step * (.7f / 128f);
                        if (mask.Sample(.5f + axis.x * distance, .5f + axis.y * distance)
                            >= LabPaintPatternMask.Threshold)
                        { lastInk = distance; empty = 0; }
                        else if (++empty >= 3) break;
                    }
                    sampled[angle] = Mathf.Max(.08f, lastInk);
                }
                clusterRadii = new float[ClusterAngles];
                meanClusterRadius = 0f;
                for (int angle = 0; angle < sampled.Length; angle++)
                {
                    float radius = sampled[(angle + ClusterAngles - 2) % ClusterAngles] * .125f
                        + sampled[(angle + ClusterAngles - 1) % ClusterAngles] * .25f
                        + sampled[angle] * .25f + sampled[(angle + 1) % ClusterAngles] * .25f
                        + sampled[(angle + 2) % ClusterAngles] * .125f;
                    clusterRadii[angle] = radius;
                    meanClusterRadius += radius / ClusterAngles;
                }
            }
        }

        private sealed class PatternBoundaryHeap
        {
            private struct Entry { public int Cell; public ulong Key; }
            private readonly List<Entry> entries = new List<Entry>();
            public int Count => entries.Count;
            public void Push(int cell, ulong key)
            {
                var entry = new Entry { Cell = cell, Key = key };
                int at = entries.Count; entries.Add(entry);
                while (at > 0)
                {
                    int parent = (at - 1) / 2;
                    if (!Before(entry, entries[parent])) break;
                    entries[at] = entries[parent]; at = parent;
                }
                entries[at] = entry;
            }
            public int Pop()
            {
                int cell = entries[0].Cell, last = entries.Count - 1;
                Entry tail = entries[last]; entries.RemoveAt(last);
                if (last == 0) return cell;
                int at = 0;
                while (at * 2 + 1 < entries.Count)
                {
                    int child = at * 2 + 1;
                    if (child + 1 < entries.Count && Before(entries[child + 1], entries[child])) child++;
                    if (!Before(entries[child], tail)) break;
                    entries[at] = entries[child]; at = child;
                }
                entries[at] = tail;
                return cell;
            }
            private static bool Before(Entry a, Entry b) => a.Key < b.Key || a.Key == b.Key && a.Cell < b.Cell;
        }
    }
}
