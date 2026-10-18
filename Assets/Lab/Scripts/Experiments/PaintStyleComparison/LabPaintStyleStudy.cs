using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6Lab.Experiments
{
    public enum LabPaintStudyStyle { Broad = 0, Splatter = 1, Flow = 2 }
    public enum LabPaintStudyStage { Single = 0, Multicolor = 1, Complete = 2 }

    /// <summary>Owned comparison pixels. Row zero represents local +Y, like LabPaintSurface.</summary>
    public sealed class LabPaintStudyMap
    {
        public readonly int Columns, Rows, CoveredCount;
        public readonly Color32[] Pixels;
        public float Coverage => CoveredCount / (float)Pixels.Length;
        internal LabPaintStudyMap(int columns, int rows, int covered, Color32[] pixels)
        {
            Columns = columns; Rows = rows; CoveredCount = covered; Pixels = pixels;
        }
    }

    /// <summary>
    /// Offline visual fixture only. Does not invoke or mutate session paint, growth, Config,
    /// victory or replication. A mask is projected geodesically onto equal-area sphere cells.
    /// All styles share hit positions, colors and exact surface-area budgets.
    /// </summary>
    public static class LabPaintStyleStudy
    {
        private static readonly Vector3[] Hits = {
            new Vector3(.40f, .24f, 1f).normalized,
            new Vector3(-.25f, .33f, 1f).normalized,
            new Vector3(-.36f, -.23f, 1f).normalized,
            new Vector3(.68f, -.23f, .65f).normalized,
            new Vector3(-.57f, -.52f, .60f).normalized
        };
        private static readonly Color32[] Colors = {
            new Color32(216, 87, 54, 255), new Color32(248, 183, 42, 255),
            new Color32(20, 113, 167, 255), new Color32(61, 130, 88, 255),
            new Color32(111, 74, 50, 255)
        };

        /// <summary>Authoring colors in sRGB, sampled from the user's profiled palette image.</summary>
        public static Color32 PaletteColor(int index)
        {
            if (index < 0 || index >= Colors.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return Colors[index];
        }

        public static LabPaintStudyMap Build(LabPaintStudyMask mask, LabPaintStudyStyle style,
            LabPaintStudyStage stage, int columns = 1024, int rows = 512, uint seed = 200)
        {
            if (mask == null) throw new ArgumentNullException(nameof(mask));
            if (style < LabPaintStudyStyle.Broad || style > LabPaintStudyStyle.Flow)
                throw new ArgumentOutOfRangeException(nameof(style));
            if (stage < LabPaintStudyStage.Single || stage > LabPaintStudyStage.Complete)
                throw new ArgumentOutOfRangeException(nameof(stage));
            if (columns < 4 || columns > 2048) throw new ArgumentOutOfRangeException(nameof(columns));
            if (rows < 2 || rows > 1024 || (long)columns * rows > 2097152)
                throw new ArgumentOutOfRangeException(nameof(rows));
            int total = columns * rows;
            var pixels = new Color32[total];
            Vector3[] points = MakePoints(columns, rows);
            int covered = 0;
            int stamps = stage == LabPaintStudyStage.Single ? 1 : Hits.Length;
            for (int stamp = 0; stamp < stamps; stamp++)
            {
                int cumulative = stage == LabPaintStudyStage.Single ? total * 6 / 100
                    : (int)((long)total * 18 * (stamp + 1) / (100 * Hits.Length));
                int requested = cumulative - covered;
                if (requested > 0)
                {
                    uint stampSeed = Hash(seed ^ ((uint)stamp + 1u) * 0x9E3779B9u);
                    float rotation = style == LabPaintStudyStyle.Flow ? 0f
                        : (Unit(stampSeed) - .5f) * (style == LabPaintStudyStyle.Broad ? .44f : 1f);
                    PaintStamp(mask, points, pixels, Hits[stamp], Colors[stamp], rotation,
                        requested, columns, rows, stampSeed);
                    covered += requested;
                }
            }
            if (stage == LabPaintStudyStage.Complete)
            {
                // Explicit completed-preview fixture, not a claim that gameplay growth completed.
                // Retain every existing cell; assign each remaining cell to its nearest fixed hit.
                for (int i = 0; i < total; i++)
                {
                    if (pixels[i].a != 0) continue;
                    int nearest = 0;
                    float best = Vector3.Dot(points[i], Hits[0]);
                    for (int hit = 1; hit < Hits.Length; hit++)
                    {
                        float dot = Vector3.Dot(points[i], Hits[hit]);
                        if (dot > best) { best = dot; nearest = hit; }
                    }
                    pixels[i] = Shade(Colors[nearest], points[i], seed);
                }
                covered = total;
            }
            return new LabPaintStudyMap(columns, rows, covered, pixels);
        }

        private static void PaintStamp(LabPaintStudyMask mask, Vector3[] points, Color32[] pixels,
            Vector3 center, Color32 color, float rotation, int requested, int columns, int rows, uint seed)
        {
            // Project once per hit. Radius calibration only scales these coordinates and samples
            // the immutable mask; it does not repeat spherical acos or allocate/sort the map.
            var planar = new Vector2[points.Length];
            Vector3 up = (Vector3.up - center * Vector3.Dot(Vector3.up, center)).normalized;
            if (up.sqrMagnitude < .5f) up = Vector3.forward;
            Vector3 right = Vector3.Cross(up, center).normalized;
            float cosine = Mathf.Cos(rotation), sine = Mathf.Sin(rotation);
            for (int i = 0; i < points.Length; i++)
            {
                float dot = Mathf.Clamp(Vector3.Dot(center, points[i]), -1f, 1f);
                float angle = Mathf.Acos(dot), length = Mathf.Sqrt(Mathf.Max(0f, 1f - dot * dot));
                float scale = length > .000001f ? angle / length : 0f;
                float x = Vector3.Dot(points[i], right) * scale;
                float y = Vector3.Dot(points[i], up) * scale;
                planar[i] = new Vector2(x * cosine + y * sine, -x * sine + y * cosine);
            }
            float estimate = Mathf.Sqrt(requested / (float)points.Length * Mathf.PI / mask.FootprintFraction);
            float low = 0f, high = Mathf.Max(.05f, estimate), bestRadius = 0f;
            int bestCount = int.MaxValue;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int count = Count(mask, planar, pixels, high);
                if (count >= requested)
                {
                    bestRadius = high; bestCount = count; break;
                }
                low = high; high *= 1.25f;
                if (high > Mathf.PI * 2f) break;
            }
            if (bestRadius == 0f)
                throw new ArgumentException("The mask footprint cannot cover the requested new area. Use a fuller, centered ink mask.", nameof(mask));
            for (int attempt = 0; attempt < 15; attempt++)
            {
                float radius = (low + high) * .5f;
                int count = Count(mask, planar, pixels, radius);
                if (count >= requested)
                {
                    high = radius;
                    if (count < bestCount) { bestCount = count; bestRadius = radius; }
                }
                else low = radius;
            }
            var footprint = new bool[points.Length];
            for (int i = 0; i < footprint.Length; i++)
                footprint[i] = pixels[i].a == 0 && Ink(mask, planar[i], bestRadius);

            // Radius quantization can cross several discrete cells at once. Remove only outer
            // boundary cells to reach the exact comparison area. Thin line/end-point cells are
            // lower removal priority than thick-body corners; never rank all ink by brightness.
            int excess = bestCount - requested;
            if (excess > 0)
            {
                var boundary = new BoundaryHeap();
                for (int i = 0; i < footprint.Length; i++)
                    if (footprint[i]) AddBoundary(boundary, footprint, i, columns, rows, seed);
                while (excess-- > 0)
                {
                    int cell;
                    do
                    {
                        if (boundary.Count == 0) throw new InvalidOperationException("Paint comparison boundary correction exhausted.");
                        cell = boundary.Pop();
                    } while (!footprint[cell] || !IsBoundary(footprint, cell, columns, rows));
                    footprint[cell] = false;
                    int x = cell % columns, y = cell / columns;
                    AddBoundary(boundary, footprint, y * columns + (x + columns - 1) % columns, columns, rows, seed);
                    AddBoundary(boundary, footprint, y * columns + (x + 1) % columns, columns, rows, seed);
                    if (y > 0) AddBoundary(boundary, footprint, cell - columns, columns, rows, seed);
                    if (y + 1 < rows) AddBoundary(boundary, footprint, cell + columns, columns, rows, seed);
                }
            }
            for (int i = 0; i < footprint.Length; i++)
                if (footprint[i]) pixels[i] = Shade(color, points[i], seed);
        }

        private static int Count(LabPaintStudyMask mask, Vector2[] points, Color32[] pixels, float radius)
        {
            int count = 0;
            for (int i = 0; i < points.Length; i++)
                if (pixels[i].a == 0 && Ink(mask, points[i], radius)) count++;
            return count;
        }

        private static bool Ink(LabPaintStudyMask mask, Vector2 point, float radius)
        {
            if (Mathf.Abs(point.x) > radius || Mathf.Abs(point.y) > radius) return false;
            return mask.Sample(.5f + point.x / (2f * radius), .5f + point.y / (2f * radius))
                >= LabPaintStudyMask.FootprintThreshold;
        }

        private static Vector3[] MakePoints(int columns, int rows)
        {
            var points = new Vector3[columns * rows];
            for (int y = 0; y < rows; y++)
            {
                float vertical = 1f - (y + .5f) * 2f / rows;
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - vertical * vertical));
                for (int x = 0; x < columns; x++)
                {
                    float angle = ((x + .5f) / columns - .5f) * (2f * Mathf.PI);
                    points[y * columns + x] = new Vector3(Mathf.Cos(angle) * ring, vertical, Mathf.Sin(angle) * ring);
                }
            }
            return points;
        }

        private static Color32 Shade(Color32 color, Vector3 point, uint seed)
        {
            float phase = Unit(Hash(seed)) * 6.2831853f;
            float density = .96f + .04f * Mathf.Sin(point.x * 17f + phase)
                * Mathf.Sin(point.y * 13f - point.z * 11f + phase);
            // The preview map is a linear-data texture. Convert the authored sRGB palette
            // before writing so the rendered hues are not brightened by a second transfer.
            Color linear = ((Color)color).linear;
            return (Color32)new Color(linear.r * density, linear.g * density, linear.b * density, 1f);
        }

        private static bool IsBoundary(bool[] footprint, int cell, int columns, int rows)
        {
            int x = cell % columns, y = cell / columns;
            return !footprint[y * columns + (x + columns - 1) % columns]
                || !footprint[y * columns + (x + 1) % columns]
                || y == 0 || y == rows - 1 || !footprint[cell - columns] || !footprint[cell + columns];
        }

        private static void AddBoundary(BoundaryHeap heap, bool[] footprint, int cell, int columns, int rows, uint seed)
        {
            if (!footprint[cell]) return;
            int x = cell % columns, y = cell / columns;
            bool left = footprint[y * columns + (x + columns - 1) % columns];
            bool right = footprint[y * columns + (x + 1) % columns];
            bool up = y > 0 && footprint[cell - columns], down = y + 1 < rows && footprint[cell + columns];
            int neighbors = (left ? 1 : 0) + (right ? 1 : 0) + (up ? 1 : 0) + (down ? 1 : 0);
            if (neighbors == 4) return;
            int category = neighbors == 3 ? 0 : neighbors == 2 && !((left && right) || (up && down)) ? 1
                : neighbors == 1 ? 3 : neighbors == 2 ? 4 : 5;
            heap.Push(cell, ((ulong)category << 32) | Hash((uint)cell ^ seed));
        }

        private static uint Hash(uint value)
        {
            value ^= value >> 16; value *= 0x7FEB352Du;
            value ^= value >> 15; value *= 0x846CA68Bu;
            return value ^ (value >> 16);
        }
        private static float Unit(uint value) => (value >> 8) * (1f / 16777216f);

        private sealed class BoundaryHeap
        {
            private struct Entry { public int Cell; public ulong Key; }
            private readonly List<Entry> entries = new List<Entry>();
            public int Count => entries.Count;
            public void Push(int cell, ulong key)
            {
                var entry = new Entry { Cell = cell, Key = key };
                int at = entries.Count;
                entries.Add(entry);
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
                Entry entry = entries[last]; entries.RemoveAt(last);
                if (last == 0) return cell;
                int at = 0;
                while (at * 2 + 1 < entries.Count)
                {
                    int child = at * 2 + 1;
                    if (child + 1 < entries.Count && Before(entries[child + 1], entries[child])) child++;
                    if (!Before(entries[child], entry)) break;
                    entries[at] = entries[child]; at = child;
                }
                entries[at] = entry;
                return cell;
            }
            private static bool Before(Entry a, Entry b) => a.Key < b.Key || (a.Key == b.Key && a.Cell < b.Cell);
        }
    }
}
