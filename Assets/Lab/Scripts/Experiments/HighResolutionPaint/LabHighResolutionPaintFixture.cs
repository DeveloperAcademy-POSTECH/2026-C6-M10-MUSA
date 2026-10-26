#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6Lab.Experiments
{
    /// <summary>Offline input fixture; its surfaces are never installed in a network or model.</summary>
    public sealed class LabHighResolutionPaintFixture
    {
        public sealed class Stamp
        {
            public Vector3 Center, Right, Up;
            public float Radius;
            public Color Color;
        }

        public readonly LabPaintSurface[] Stages = new LabPaintSurface[4];
        public readonly Stamp[] Stamps = new Stamp[4];
        public readonly uint Seed;
        public readonly Vector3 HitDirection;
        public readonly float InitialPercent, RepeatPercent;

        public LabHighResolutionPaintFixture(LabPaintGrowthSettings settings, LabPaintStudyMask mask,
            Vector3 direction, Color linearColor, uint seed = 123)
        {
            if (settings == null || mask == null) throw new ArgumentNullException();
            if (!Finite(direction) || direction.sqrMagnitude < .00001f) throw new ArgumentException("Invalid hit direction.");
            if (!Finite(new Vector3(linearColor.r, linearColor.g, linearColor.b)) || !float.IsFinite(linearColor.a) || linearColor.a <= 0)
                throw new ArgumentException("A visible finite linear pigment is required.");
            Seed = seed; HitDirection = direction.normalized;
            InitialPercent = settings.InitialPercent; RepeatPercent = settings.RepeatPercent;
            // Stage copies replay the same input through the unchanged production class.
            // Callers pass a private settings copy; even that copy is never modified here.
            for (int stage = 0; stage < 4; stage++)
            {
                var surface = new LabPaintSurface(settings, seed);
                surface.RegisterHit(HitDirection, linearColor, 1, 1);
                for (int repeat = 0; repeat < stage; repeat++)
                {
                    surface.RegisterHit(HitDirection, linearColor, 1, 1);
                    surface.Advance(1d);
                }
                Stages[stage] = surface;
            }
            for (int stage = 0; stage < 4; stage++)
            {
                Vector3 center = HitDirection;
                if (stage > 0)
                {
                    Color32[] before = Pixels(Stages[stage - 1]), after = Pixels(Stages[stage]);
                    Vector3 sum = Vector3.zero;
                    for (int i = 0; i < after.Length; i++)
                        if (before[i].a == 0 && after[i].a != 0) sum += Direction(i, Stages[stage].Columns, Stages[stage].Rows);
                    if (sum.sqrMagnitude > .00001f) center = sum.normalized;
                }
                Vector3 up = Vector3.up - center * Vector3.Dot(Vector3.up, center);
                if (up.sqrMagnitude < .00001f) up = Vector3.forward - center * Vector3.Dot(Vector3.forward, center);
                up.Normalize();
                Vector3 right = Vector3.Cross(up, center).normalized;
                float angle = Unit(Hash(seed ^ (uint)(stage + 1) * 0x9E3779B9u)) * Mathf.PI * 2f;
                Stamps[stage] = new Stamp {
                    Center = center, Right = right * Mathf.Cos(angle) + up * Mathf.Sin(angle),
                    Up = -right * Mathf.Sin(angle) + up * Mathf.Cos(angle), Color = linearColor
                };
                float requested = (stage == 0 ? InitialPercent : RepeatPercent) / 100f;
                Stamps[stage].Radius = Calibrate(mask, stage, requested);
            }
        }

        // Radius is estimated on a small equal-area sampling grid, without trimming or
        // turning the actual high-resolution brush into binary cells. It is visual only.
        private float Calibrate(LabPaintStudyMask mask, int stage, float requested)
        {
            if (requested <= 0) return 0;
            const int columns = 128, rows = 64;
            var available = new List<Vector3>();
            for (int i = 0; i < columns * rows; i++)
            {
                Vector3 point = Direction(i, columns, rows);
                bool painted = false;
                for (int old = 0; old < stage && !painted; old++) painted = Sample(mask, Stamps[old], point) >= .08f;
                if (!painted) available.Add(point);
            }
            int target = Mathf.CeilToInt(requested * columns * rows);
            float low = .01f, high = .35f;
            for (int attempt = 0; attempt < 8 && Count(high) < target; attempt++) high *= 1.3f;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float radius = (low + high) * .5f;
                if (Count(radius) >= target) high = radius; else low = radius;
            }
            return high;

            int Count(float radius)
            {
                Stamps[stage].Radius = radius;
                int count = 0;
                foreach (Vector3 point in available) if (Sample(mask, Stamps[stage], point) >= .08f) count++;
                return count;
            }
        }

        public static float Sample(LabPaintStudyMask mask, Stamp stamp, Vector3 direction)
        {
            if (stamp.Radius <= 0) return 0;
            float dot = Mathf.Clamp(Vector3.Dot(stamp.Center, direction), -1f, 1f);
            if (dot <= 0) return 0;
            float sine = Mathf.Sqrt(Mathf.Max(0f, 1f - dot * dot));
            float factor = sine > .000001f ? Mathf.Acos(dot) / sine : 1f;
            float u = .5f + Vector3.Dot(direction, stamp.Right) * factor / (2f * stamp.Radius);
            float v = .5f + Vector3.Dot(direction, stamp.Up) * factor / (2f * stamp.Radius);
            return mask.Sample(u, v);
        }

        public static Vector3 Direction(int cell, int columns, int rows)
        {
            float vertical = 1f - (cell / columns + .5f) * 2f / rows;
            float ring = Mathf.Sqrt(Mathf.Max(0, 1f - vertical * vertical));
            float angle = ((cell % columns + .5f) / columns - .5f) * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle) * ring, vertical, Mathf.Sin(angle) * ring);
        }

        public static Color32[] Pixels(LabPaintSurface surface)
        {
            var pixels = new Color32[surface.CellCount]; surface.CopyPixels(pixels); return pixels;
        }
        private static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
        private static uint Hash(uint v) { v ^= v >> 16; v *= 0x7FEB352Du; v ^= v >> 15; v *= 0x846CA68Bu; return v ^ (v >> 16); }
        private static float Unit(uint v) => (v >> 8) / 16777216f;
    }
}
#endif
