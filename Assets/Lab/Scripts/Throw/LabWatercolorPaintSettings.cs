using System;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Local presentation tuning. These values never enter the shared gameplay settings or paint ledger.</summary>
    [Serializable]
    public sealed class LabWatercolorPaintSettings
    {
        public bool enabled = true;
        [Range(.05f, 1f)] public float density = .62f;
        [Range(0f, 1f)] public float broadVariation = .55f;
        [Range(0f, 1f)] public float mottle = .38f;
        [Range(0f, 1f)] public float grain = .35f;
        [Range(.25f, 4f)] public float grainScale = 1f;
        [Range(0f, 1f)] public float edgeStrength = .6f;
        [Range(.002f, .12f)] public float edgeWidth = .022f;
        [Range(.25f, 4f)] public float textureScale = 1f;
        [Tooltip("Local reveal duration. Zero installs new stains immediately; already restored stains never replay their reveal.")]
        [Range(0f, 2f)] public float spreadSeconds = .4f;
        [Tooltip("Maximum simultaneously revealing stains. Additional work may settle immediately without changing game paint.")]
        [Range(1, 32)] public int maxActiveStains = 12;

        public LabWatercolorPaintSettings SanitizedCopy() => new LabWatercolorPaintSettings
        {
            enabled = enabled,
            density = Safe(density, .62f, .05f, 1f),
            broadVariation = Safe(broadVariation, .55f, 0f, 1f),
            mottle = Safe(mottle, .38f, 0f, 1f),
            grain = Safe(grain, .35f, 0f, 1f),
            grainScale = Safe(grainScale, 1f, .25f, 4f),
            edgeStrength = Safe(edgeStrength, .6f, 0f, 1f),
            edgeWidth = Safe(edgeWidth, .022f, .002f, .12f),
            textureScale = Safe(textureScale, 1f, .25f, 4f),
            spreadSeconds = Safe(spreadSeconds, .4f, 0f, 2f),
            maxActiveStains = Mathf.Clamp(maxActiveStains, 1, 32)
        };

        public void Validate()
        {
            Check(density, .05f, 1f); Check(broadVariation, 0f, 1f);
            Check(mottle, 0f, 1f); Check(grain, 0f, 1f);
            Check(grainScale, .25f, 4f); Check(edgeStrength, 0f, 1f);
            Check(edgeWidth, .002f, .12f); Check(textureScale, .25f, 4f);
            Check(spreadSeconds, 0f, 2f);
            if (maxActiveStains < 1 || maxActiveStains > 32)
                throw new ArgumentOutOfRangeException(nameof(maxActiveStains));
        }

        /// <summary>Stable local cache key; no process-randomized hash or frame-dependent state.</summary>
        public int Fingerprint
        {
            get
            {
                unchecked
                {
                    int hash = enabled ? 1 : 0;
                    hash = Mix(hash, Safe(density, .62f, .05f, 1f));
                    hash = Mix(hash, Safe(broadVariation, .55f, 0f, 1f));
                    hash = Mix(hash, Safe(mottle, .38f, 0f, 1f));
                    hash = Mix(hash, Safe(grain, .35f, 0f, 1f));
                    hash = Mix(hash, Safe(grainScale, 1f, .25f, 4f));
                    hash = Mix(hash, Safe(edgeStrength, .6f, 0f, 1f));
                    hash = Mix(hash, Safe(edgeWidth, .022f, .002f, .12f));
                    hash = Mix(hash, Safe(textureScale, 1f, .25f, 4f));
                    hash = Mix(hash, Safe(spreadSeconds, .4f, 0f, 2f));
                    return hash * 397 ^ Mathf.Clamp(maxActiveStains, 1, 32);
                }
            }
        }

        private static int Mix(int hash, float value) => unchecked(hash * 397 ^ BitConverter.SingleToInt32Bits(value));
        private static float Safe(float value, float fallback, float min, float max) =>
            Mathf.Clamp(float.IsFinite(value) ? value : fallback, min, max);
        private static void Check(float value, float min, float max)
        {
            if (!float.IsFinite(value) || value < min || value > max)
                throw new ArgumentOutOfRangeException(nameof(value), "Watercolor settings must be finite and within their Inspector range.");
        }
    }
}
