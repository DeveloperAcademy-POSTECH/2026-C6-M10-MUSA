using System;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Round-pinned clay pigment appearance; thickness/gloss are shared live DEV settings.</summary>
    [Serializable]
    public sealed class LabClayStickerSettings
    {
        public const string ResourceFolder = "GourdPaint/ClaySticker/";
        public const string StyleVersion = "clay-stickers-v2";
        [Range(.1f,1f)] public float minimumDensity = 1f;
        [Range(.1f,1f)] public float maximumDensity = 1f;
        public LabClayStickerSettings SanitizedCopy()
        {
            float min = Mathf.Clamp(float.IsFinite(minimumDensity) ? minimumDensity : 1f,.1f,1f);
            float max = Mathf.Clamp(float.IsFinite(maximumDensity) ? maximumDensity : 1f,min,1f);
            return new LabClayStickerSettings { minimumDensity=min, maximumDensity=max };
        }
        public void Validate()
        {
            if (!float.IsFinite(minimumDensity) || !float.IsFinite(maximumDensity) || minimumDensity<.1f
                || maximumDensity>1 || maximumDensity<minimumDensity) throw new ArgumentOutOfRangeException(nameof(minimumDensity));
        }
        public float Density(float strength)
        { var s=SanitizedCopy(); return Mathf.Lerp(s.minimumDensity,s.maximumDensity,Mathf.Clamp01(float.IsFinite(strength)?strength:.5f)); }
        public int Fingerprint { get { var s=SanitizedCopy(); return unchecked(BitConverter.SingleToInt32Bits(s.minimumDensity)*397 ^ BitConverter.SingleToInt32Bits(s.maximumDensity)); } }
        public static string BakedBrushVersion
        {
            get
            {
                string hash=Resources.Load<TextAsset>(ResourceFolder+"Version")?.text.Trim();
                if(hash!=null && hash.Length==64)
                {
                    foreach(char c in hash) if(!(c>='0'&&c<='9'||c>='a'&&c<='f')) return "clay-sticker-unbaked";
                    return hash;
                }
                return "clay-sticker-unbaked";
            }
        }
    }
}
