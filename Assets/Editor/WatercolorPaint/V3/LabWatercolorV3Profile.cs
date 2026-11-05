using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Editor comparison authoring only. No Resources asset or game default points here.</summary>
    [CreateAssetMenu(menuName = "C6 Lab/Experiments/Watercolor V3 Study Profile")]
    public sealed class LabWatercolorV3Profile : ScriptableObject
    {
        public const string StyleVersion = "watercolor-stamps-v3-study-r3";
        public const float DefaultOpacityCap = .75f;
        public const float AlternateOpacityCap = .85f;

        [Tooltip("Private cloned V2 geometry/reveal controls. Capturing this profile never edits the V2 asset.")]
        public LabWatercolorStampSettings baseStampSettings = new LabWatercolorStampSettings();
        [Header("V3 transparent pigment")]
        [Range(.05f, .95f)] public float layerOpacityCap = DefaultOpacityCap;
        [Range(0f, .5f)] public float washOpacity = .12f;
        [Range(.1f, 4f)] public float pigmentDepth = 1.4f;
        [Range(0f, 1f)] public float poolStrength = .5f;
        [Range(.25f, 16f)] public float flowScale = 4f;
        [Range(0f, 1f)] public float rimWobble = .35f;
        [Header("V3 flat fine-fiber paper")]
        [Range(0f, .005f)] public float paperRelief = .0005f;
        [Range(0f, 1f)] public float paperFiberInfluence = .15f;
        [Range(.5f, 1.5f)] public float paperBrightness = 1f;
        [Range(0f, 3f)] public float paperContrast = 1.4f;
        [Range(0f, .6f)] public float pigmentGrain = .32f;
        [Range(48f, 320f)] public float pigmentGrainScale = 96f;
        [Header("Comparison-only baked data")]
        public Texture2D roundBrush;
        public Texture2D growthBrush;
        public Texture2D overlapBrush;
        public Texture2D paperTexture;

        public LabWatercolorV3Profile Clone() => SanitizedCopy();
        public LabWatercolorV3Snapshot Snapshot() => new LabWatercolorV3Snapshot(this, LabWatercolorV3Baker.ReadBakedVersion());
        public LabWatercolorV3Snapshot Snapshot(float opacityCap) => Snapshot().WithLayerOpacityCap(opacityCap);
        public int Fingerprint => Snapshot().Fingerprint;
        public string Version => Snapshot().Version;
        public string Identity => Version;
        public void ApplyStamp(MaterialPropertyBlock block) => Snapshot().ApplyStamp(block);
        public void ApplySurface(Material material) => Snapshot().ApplySurface(material);

        /// <summary>A detached transient authoring copy. The caller owns and destroys this object.</summary>
        public LabWatercolorV3Profile SanitizedCopy()
        {
            var copy = CreateInstance<LabWatercolorV3Profile>();
            copy.hideFlags = HideFlags.HideAndDontSave;
            copy.baseStampSettings = (baseStampSettings ?? new LabWatercolorStampSettings()).SanitizedCopy();
            copy.layerOpacityCap = Safe(layerOpacityCap, DefaultOpacityCap, .05f, .95f);
            copy.washOpacity = Safe(washOpacity, .12f, 0f, .5f);
            copy.pigmentDepth = Safe(pigmentDepth, 1.4f, .1f, 4f);
            copy.poolStrength = Safe(poolStrength, .5f, 0f, 1f);
            copy.flowScale = Safe(flowScale, 4f, .25f, 16f);
            copy.rimWobble = Safe(rimWobble, .35f, 0f, 1f);
            copy.paperRelief = Safe(paperRelief, .0005f, 0f, .005f);
            copy.paperFiberInfluence = Safe(paperFiberInfluence, .15f, 0f, 1f);
            copy.paperBrightness = Safe(paperBrightness, 1f, .5f, 1.5f);
            copy.paperContrast = Safe(paperContrast, 1.4f, 0f, 3f);
            copy.pigmentGrain = Safe(pigmentGrain, .32f, 0f, .6f);
            copy.pigmentGrainScale = Safe(pigmentGrainScale, 96f, 48f, 320f);
            copy.roundBrush = roundBrush; copy.growthBrush = growthBrush; copy.overlapBrush = overlapBrush;
            copy.paperTexture = paperTexture;
            return copy;
        }

        public void Validate()
        {
            if (baseStampSettings == null) throw new InvalidOperationException("V3 base stamp settings are missing.");
            baseStampSettings.Validate();
            Check(layerOpacityCap, .05f, .95f, nameof(layerOpacityCap)); Check(washOpacity, 0f, .5f, nameof(washOpacity));
            Check(pigmentDepth, .1f, 4f, nameof(pigmentDepth)); Check(poolStrength, 0f, 1f, nameof(poolStrength));
            Check(flowScale, .25f, 16f, nameof(flowScale)); Check(rimWobble, 0f, 1f, nameof(rimWobble));
            Check(paperRelief, 0f, .005f, nameof(paperRelief)); Check(paperFiberInfluence, 0f, 1f, nameof(paperFiberInfluence));
            Check(paperBrightness, .5f, 1.5f, nameof(paperBrightness)); Check(paperContrast, 0f, 3f, nameof(paperContrast));
            Check(pigmentGrain, 0f, .6f, nameof(pigmentGrain)); Check(pigmentGrainScale, 48f, 320f, nameof(pigmentGrainScale));
        }

        internal static float Safe(float value, float fallback, float minimum, float maximum)
            => Mathf.Clamp(float.IsFinite(value) ? value : fallback, minimum, maximum);
        internal static void Check(float value, float minimum, float maximum, string name)
        {
            if (!float.IsFinite(value) || value < minimum || value > maximum)
                throw new ArgumentOutOfRangeException(name, value, "Outside the V3 study profile range.");
        }
    }

    /// <summary>
    /// A captured presentation configuration. Numbers, fingerprints and data version never follow later
    /// Inspector edits or rebakes. Mutable base settings and brush arrays are returned as detached copies.
    /// Texture references are read-only asset handles; the captured DataVersion detects a later rebake.
    /// </summary>
    public sealed class LabWatercolorV3Snapshot
    {
        private readonly LabWatercolorStampSettings baseSettings;
        private readonly Texture2D[] brushes;
        private readonly int textureFingerprint;
        public LabWatercolorStampSettings BaseStampSettings => baseSettings.Clone();
        public Texture2D[] Brushes => (Texture2D[])brushes.Clone();
        public Texture2D PaperTexture { get; }
        public float LayerOpacityCap { get; }
        public float WashOpacity { get; }
        public float PigmentDepth { get; }
        public float PoolStrength { get; }
        public float FlowScale { get; }
        public float RimWobble { get; }
        public float PaperRelief { get; }
        public float PaperFiberInfluence { get; }
        public float PaperBrightness { get; }
        public float PaperContrast { get; }
        public float PigmentGrain { get; }
        public float PigmentGrainScale { get; }
        public int Fingerprint { get; }
        public string DataVersion { get; }
        public string BakedVersion => DataVersion;
        public string Version => LabWatercolorV3Profile.StyleVersion + "/" + DataVersion + "/"
            + unchecked((uint)Fingerprint).ToString("x8", CultureInfo.InvariantCulture);
        public string Identity => Version;

        internal LabWatercolorV3Snapshot(LabWatercolorV3Profile profile, string dataVersion)
        {
            baseSettings = (profile.baseStampSettings ?? new LabWatercolorStampSettings()).Clone();
            brushes = new[] { profile.roundBrush, profile.growthBrush, profile.overlapBrush };
            PaperTexture = profile.paperTexture;
            LayerOpacityCap = LabWatercolorV3Profile.Safe(profile.layerOpacityCap, .75f, .05f, .95f);
            WashOpacity = LabWatercolorV3Profile.Safe(profile.washOpacity, .12f, 0f, .5f);
            PigmentDepth = LabWatercolorV3Profile.Safe(profile.pigmentDepth, 1.4f, .1f, 4f);
            PoolStrength = LabWatercolorV3Profile.Safe(profile.poolStrength, .5f, 0f, 1f);
            FlowScale = LabWatercolorV3Profile.Safe(profile.flowScale, 4f, .25f, 16f);
            RimWobble = LabWatercolorV3Profile.Safe(profile.rimWobble, .35f, 0f, 1f);
            PaperRelief = LabWatercolorV3Profile.Safe(profile.paperRelief, .0005f, 0f, .005f);
            PaperFiberInfluence = LabWatercolorV3Profile.Safe(profile.paperFiberInfluence, .15f, 0f, 1f);
            PaperBrightness = LabWatercolorV3Profile.Safe(profile.paperBrightness, 1f, .5f, 1.5f);
            PaperContrast = LabWatercolorV3Profile.Safe(profile.paperContrast, 1.4f, 0f, 3f);
            PigmentGrain = LabWatercolorV3Profile.Safe(profile.pigmentGrain, .32f, 0f, .6f);
            PigmentGrainScale = LabWatercolorV3Profile.Safe(profile.pigmentGrainScale, 96f, 48f, 320f);
            DataVersion = dataVersion;
            int textureHash = 0;
            foreach (Texture2D texture in brushes) textureHash = Mix(textureHash, TextureIdentity(texture));
            textureFingerprint = Mix(textureHash, TextureIdentity(PaperTexture));
            Fingerprint = ComputeFingerprint();
        }

        private LabWatercolorV3Snapshot(LabWatercolorV3Snapshot source, float opacityCap)
        {
            baseSettings = source.baseSettings.Clone(); brushes = source.Brushes; PaperTexture = source.PaperTexture;
            LayerOpacityCap = opacityCap; WashOpacity = source.WashOpacity; PigmentDepth = source.PigmentDepth;
            PoolStrength = source.PoolStrength; FlowScale = source.FlowScale; RimWobble = source.RimWobble;
            PaperRelief = source.PaperRelief; PaperFiberInfluence = source.PaperFiberInfluence;
            PaperBrightness = source.PaperBrightness; PaperContrast = source.PaperContrast;
            PigmentGrain = source.PigmentGrain; PigmentGrainScale = source.PigmentGrainScale;
            DataVersion = source.DataVersion; textureFingerprint = source.textureFingerprint; Fingerprint = ComputeFingerprint();
        }

        public LabWatercolorV3Snapshot Clone() => new LabWatercolorV3Snapshot(this, LayerOpacityCap);

        public LabWatercolorV3Snapshot WithLayerOpacityCap(float opacityCap)
        {
            LabWatercolorV3Profile.Check(opacityCap, .05f, .95f, nameof(opacityCap));
            return new LabWatercolorV3Snapshot(this, opacityCap);
        }

        public void Validate()
        {
            baseSettings.Validate();
            LabWatercolorV3Profile.Check(LayerOpacityCap, .05f, .95f, nameof(LayerOpacityCap));
            LabWatercolorV3Profile.Check(WashOpacity, 0f, .5f, nameof(WashOpacity));
            LabWatercolorV3Profile.Check(PigmentDepth, .1f, 4f, nameof(PigmentDepth));
            LabWatercolorV3Profile.Check(PoolStrength, 0f, 1f, nameof(PoolStrength));
            LabWatercolorV3Profile.Check(FlowScale, .25f, 16f, nameof(FlowScale));
            LabWatercolorV3Profile.Check(RimWobble, 0f, 1f, nameof(RimWobble));
            LabWatercolorV3Profile.Check(PaperRelief, 0f, .005f, nameof(PaperRelief));
            LabWatercolorV3Profile.Check(PaperFiberInfluence, 0f, 1f, nameof(PaperFiberInfluence));
            LabWatercolorV3Profile.Check(PaperBrightness, .5f, 1.5f, nameof(PaperBrightness));
            LabWatercolorV3Profile.Check(PaperContrast, 0f, 3f, nameof(PaperContrast));
            LabWatercolorV3Profile.Check(PigmentGrain, 0f, .6f, nameof(PigmentGrain));
            LabWatercolorV3Profile.Check(PigmentGrainScale, 48f, 320f, nameof(PigmentGrainScale));
        }

        public void ApplyStamp(MaterialPropertyBlock block)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            // Raw numerical setters preserve the authored optical controls in a Linear project.
            block.SetFloat("_Density", baseSettings.density); block.SetFloat("_Overlap", baseSettings.overlap);
            block.SetFloat("_EdgeStrength", baseSettings.edgeStrength); block.SetFloat("_EdgeWidth", baseSettings.edgeWidth);
            block.SetFloat("_PigmentContrast", baseSettings.pigmentContrast);
            block.SetFloat("_PigmentSaturation", baseSettings.pigmentSaturation); block.SetFloat("_RimBreakup", baseSettings.rimBreakup);
            block.SetFloat("_V3LayerOpacityCap", LayerOpacityCap); block.SetFloat("_V3WashOpacity", WashOpacity);
            block.SetFloat("_V3PigmentDepth", PigmentDepth); block.SetFloat("_V3PoolStrength", PoolStrength);
            block.SetFloat("_V3FlowScale", FlowScale); block.SetFloat("_V3RimWobble", RimWobble);
            // _Brush is selected by the journal's deterministic brush index, from the captured Brushes array.
        }

        public void ApplySurface(Material material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (PaperTexture == null) throw new InvalidOperationException("Bake V3 fine-fiber paper before rendering the study.");
            material.SetTexture("_PaperTex", PaperTexture);
            material.SetFloat("_PaperBrightness", PaperBrightness); material.SetFloat("_PaperContrast", PaperContrast);
            material.SetFloat("_V3PaperRelief", PaperRelief); material.SetFloat("_V3PaperFiberInfluence", PaperFiberInfluence);
            material.SetFloat("_V3PigmentGrain", PigmentGrain); material.SetFloat("_V3PigmentGrainScale", PigmentGrainScale);
        }

        private int ComputeFingerprint()
        {
            int h = baseSettings.Fingerprint;
            h = Mix(h, LayerOpacityCap); h = Mix(h, WashOpacity); h = Mix(h, PigmentDepth);
            h = Mix(h, PoolStrength); h = Mix(h, FlowScale); h = Mix(h, RimWobble);
            h = Mix(h, PaperRelief); h = Mix(h, PaperFiberInfluence); h = Mix(h, PaperBrightness); h = Mix(h, PaperContrast);
            h = Mix(h, PigmentGrain); h = Mix(h, PigmentGrainScale);
            return unchecked(h * 397 ^ textureFingerprint);
        }
        private static string TextureIdentity(Texture2D texture)
        {
            if (texture == null) return "missing";
            string path = AssetDatabase.GetAssetPath(texture);
            return string.IsNullOrEmpty(path) ? texture.name + "/" + texture.width + "x" + texture.height
                : AssetDatabase.AssetPathToGUID(path) + "/" + AssetDatabase.GetAssetDependencyHash(path);
        }
        private static int Mix(int h, float value) => unchecked(h * 397 ^ BitConverter.SingleToInt32Bits(value));
        private static int Mix(int h, string value)
        {
            unchecked { foreach (char c in value) h = h * 397 ^ c; return h; }
        }
    }
}
