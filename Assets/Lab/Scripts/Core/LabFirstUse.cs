using Unity.Profiling;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Work that can stall the frame it first runs in (#481). Numbers index arrays; append only.</summary>
    public enum LabFirstUseKind
    {
        BrushDistance, PatternMask, BrushLoad, Projectile, BattleMusic, FeverMusic, RewardFire, Sound
    }

    /// <summary>
    /// Profiler markers and per-frame notes for one-time battle work (#481). Measurement only: nothing here changes
    /// what runs. A probe reads which kinds ran in a frame to explain a long frame; tests read the counts.
    /// </summary>
    public static class LabFirstUse
    {
        public const int KindCount = 8;
        private static readonly ProfilerMarker[] Markers =
        {
            new ProfilerMarker("Hapio.FirstUse.BrushDistance"), new ProfilerMarker("Hapio.FirstUse.PatternMask"),
            new ProfilerMarker("Hapio.FirstUse.BrushLoad"), new ProfilerMarker("Hapio.FirstUse.Projectile"),
            new ProfilerMarker("Hapio.FirstUse.BattleMusic"), new ProfilerMarker("Hapio.FirstUse.FeverMusic"),
            new ProfilerMarker("Hapio.FirstUse.RewardFire"), new ProfilerMarker("Hapio.FirstUse.Sound"),
        };
        private static readonly int[] Counts = new int[KindCount];
        private static int flags, flagsFrame = -1;

        /// <summary>Note the work and time it under its profiler marker: <c>using (LabFirstUse.Measure(kind)) { ... }</c>.</summary>
        public static ProfilerMarker.AutoScope Measure(LabFirstUseKind kind)
        {
            Note(kind);
            return Markers[(int)kind].Auto();
        }

        public static void Note(LabFirstUseKind kind)
        {
            int index = (int)kind;
            Counts[index]++;
            int frame = Time.frameCount;
            if (frame != flagsFrame) { flagsFrame = frame; flags = 0; }
            flags |= 1 << index;
        }

        /// <summary>The kinds noted during <paramref name="frame"/>, as a bit mask; 0 for any other frame.</summary>
        public static int FlagsFor(int frame) => frame == flagsFrame ? flags : 0;

        public static int CountOf(LabFirstUseKind kind) => Counts[(int)kind];

        public static string Describe(int mask)
        {
            if (mask == 0) return "-";
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < KindCount; i++)
                if ((mask & (1 << i)) != 0) text.Append(text.Length > 0 ? "," : string.Empty).Append((LabFirstUseKind)i);
            return text.ToString();
        }
    }
}
