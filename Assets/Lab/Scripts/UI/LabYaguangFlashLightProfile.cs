using System;
using UnityEngine;

namespace C6Lab
{
    [Serializable]
    public sealed class LabYaguangFlashTuning
    {
        public Color coreColor = new Color(1f, .985f, .94f);
        public Color glowColor = new Color(1f, .96f, .8f);
        public Color bloomColor = new Color(1f, .985f, .94f);
        [Range(0f, 100f)] public float flashRadiance = 10f;
        [Range(0f, 10f)] public float warningRadiance = 2.6f;
        [Range(0f, 5f)] public float warningGain = 1f, afterglowGain = 1f;
        [Range(.02f, 1f)] public float sourceArea = 1f;
        [Range(0f, 10f)] public float flashBloom = 2.2f, otherBloom = .35f;
        [Range(0f, 1f)] public float scatter = .75f;
        [Range(0f, 3f)] public float threshold = .85f;
        public LabYaguangFlashTuning Copy() => JsonUtility.FromJson<LabYaguangFlashTuning>(JsonUtility.ToJson(this));
    }

    public sealed class LabYaguangFlashLightProfile : ScriptableObject
    {
        public const string ResourcePath = "Yaguang/Light/Tuning";
        public LabYaguangFlashTuning tuning = new LabYaguangFlashTuning();
        public static LabYaguangFlashTuning GameDefaults()
        {
            var saved = Resources.Load<LabYaguangFlashLightProfile>(ResourcePath);
            return saved != null && saved.tuning != null ? saved.tuning.Copy() : new LabYaguangFlashTuning();
        }
    }
}
