using System;
using UnityEngine;

namespace C6Lab
{
    public sealed partial class LabPaintSurface
    {
        /// <summary>Round-owned presentation history. Clear detaches it; its owner disposes it.</summary>
        public LabPaintArtJournal ArtJournal { get; private set; }

        /// <summary>
        /// An authoritative observer must attach before the first hit. A display replica can
        /// bind its separate received journal at any revision; installing pixels never invents art.
        /// Storage errors remain inspectable on the journal and do not block gameplay.
        /// </summary>
        public bool AttachArtJournal(LabPaintArtJournal journal)
        {
            if (journal != null && (journal.Columns != Columns || journal.Rows != Rows
                || !replica && (preview || PaintCount != 0 || CoveredCount != 0))) return false;
            ArtJournal = journal;
            return true;
        }

        private uint BeginArtStroke(PatternPatch patch, Color32 color, byte painterSlot, byte impact01 = 0)
        {
            if (ArtJournal == null || ArtJournal.Error != null || ArtJournal.IsReceiver) return 0;
            try { return ArtJournal.BeginStroke(patch.VisualStamp(1, color, painterSlot, pattern, impact01)); }
            catch (Exception e) { ArtJournal.Fail("Artwork observation failed: " + e.Message); return 0; }
        }

        private void RecordArtPaint(Ink ink, int cell)
        {
            if (ArtJournal == null || ArtJournal.Error != null || ArtJournal.IsReceiver) return;
            try
            {
                if (ink.ArtStroke == 0)
                {
                    // Frozen supply geometry uses only a stable salt. No gameplay random draw,
                    // additional supply, budget debit, or new cell selection occurs here.
                    uint salt = initialSeed ^ unchecked((uint)ink.Sequence * 0x9E3779B9u)
                        ^ unchecked((uint)(ink.Sequence >> 32) * 0x85EBCA6Bu);
                    Vector3 center = CellDirection(ink.Anchor >= 0 ? ink.Anchor : cell);
                    var patch = new PatternPatch(patternMask, center,
                        pattern == LabPaintPattern.Flow ? -flowDirection : Vector3.up,
                        pattern == LabPaintPattern.Flow ? 0f : (StableUnit(salt) - .5f) * Mathf.PI * 2f, salt);
                    float footprint = patternMask != null ? patternMask.FootprintFraction : .25f;
                    patch.Radius = Mathf.Min(LabPaintVisualStamp.MaximumRadius, Mathf.Max(Mathf.Sqrt(4f * Mathf.PI / CellCount),
                        Mathf.Sqrt((float)(ink.VisualBudget / CellCount) * Mathf.PI / footprint)));
                    var stamp = patch.VisualStamp(1, ink.Color, ink.PainterSlot, pattern, ink.Impact01);
                    ink.ArtStroke = ArtJournal.BeginStroke(stamp);
                }
                if (ink.ArtStroke != 0) ArtJournal.AppendPaint(ink.ArtStroke, cell, Revision);
            }
            catch (Exception e) { ArtJournal.Fail("Artwork paint observation failed: " + e.Message); }
        }

        private void RecordArtErase(int cell)
        {
            if (ArtJournal == null || ArtJournal.Error != null || ArtJournal.IsReceiver) return;
            try { ArtJournal.AppendErase(cell, Revision); }
            catch (Exception e) { ArtJournal.Fail("Artwork drying observation failed: " + e.Message); }
        }
    }
}
