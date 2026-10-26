using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    public sealed partial class LabPaintSurface
    {
        private readonly List<LabPaintVisualStamp> visualStamps = new List<LabPaintVisualStamp>();
        private uint nextVisualSequence;
        public uint VisualVersion { get; private set; }

        /// <summary>Owned snapshot. Mutating it cannot affect the Host ledger or its visual state.</summary>
        public LabPaintVisualStamp[] CopyVisualStamps() => visualStamps.ToArray();

        /// <summary>Install already validated render hints without changing gameplay bytes or counters.</summary>
        public bool ReplaceVisualStamps(LabPaintVisualStamp[] stamps)
        {
            if (!LabPaintVisualStamp.Valid(stamps)) return false;
            // Keep a stable version for identical checkpoints; no renderer work is required.
            if (SameVisualStamps(stamps)) return true;
            visualStamps.Clear(); visualStamps.AddRange(stamps);
            nextVisualSequence = stamps.Length == 0 ? 0 : stamps[stamps.Length - 1].sequence;
            unchecked { VisualVersion++; }
            return true;
        }

        private bool SameVisualStamps(LabPaintVisualStamp[] stamps)
        {
            if (stamps.Length != visualStamps.Count) return false;
            for (int i = 0; i < stamps.Length; i++)
            {
                var a = stamps[i]; var b = visualStamps[i];
                if (a.sequence != b.sequence || !SameVectorBits(a.center, b.center) || !SameVectorBits(a.right, b.right)
                    || !SameFloatBits(a.radius, b.radius)
                    || !a.color.Equals(b.color) || a.painterSlot != b.painterSlot || a.pattern != b.pattern || a.seed != b.seed)
                    return false;
            }
            return true;
        }

        private static bool SameVectorBits(Vector3 a, Vector3 b) => SameFloatBits(a.x, b.x)
            && SameFloatBits(a.y, b.y) && SameFloatBits(a.z, b.z);
        private static bool SameFloatBits(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);

        private void ClearVisualStamps()
        {
            visualStamps.Clear(); nextVisualSequence = 0;
            unchecked { VisualVersion++; }
        }

        private void RecordVisualStamp(PatternPatch patch, Color32 color, byte painterSlot)
        {
            if (patch == null || nextVisualSequence == uint.MaxValue) return;
            LabPaintVisualStamp stamp = patch.VisualStamp(++nextVisualSequence, color, painterSlot, pattern);
            if (visualStamps.Count == LabPaintVisualStamp.MaximumCount) visualStamps.RemoveAt(0);
            visualStamps.Add(stamp);
            unchecked { VisualVersion++; }
        }

        private void RecordGrowthVisual(Ink ink, int firstCell)
        {
            if (ink.VisualRecorded || pattern == LabPaintPattern.Legacy || patternMask == null) return;
            ink.VisualRecorded = true;
            uint salt = initialSeed ^ unchecked((uint)ink.Sequence * 0x9E3779B9u)
                ^ unchecked((uint)(ink.Sequence >> 32) * 0x85EBCA6Bu);
            Vector3 center = CellDirection(ink.Anchor >= 0 ? ink.Anchor : firstCell);
            var patch = new PatternPatch(patternMask, center,
                pattern == LabPaintPattern.Flow ? -flowDirection : Vector3.up,
                pattern == LabPaintPattern.Flow ? 0f : (StableUnit(salt) - .5f) * Mathf.PI * 2f, salt);
            patch.Radius = Mathf.Max(Mathf.Sqrt(4f * Mathf.PI / CellCount),
                Mathf.Sqrt((float)(ink.VisualBudget / CellCount) * Mathf.PI / patternMask.FootprintFraction));
            RecordVisualStamp(patch, ink.Color, ink.PainterSlot);
        }
    }
}
