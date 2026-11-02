using UnityEngine;

namespace C6Lab
{
    /// <summary>Local-only spoken forewarning. Cue positions mark the first sample of the final word,
    /// not the end of the file; they do not change the Host's activation schedule.</summary>
    [CreateAssetMenu(fileName = "PatternAudioProfile", menuName = "C6 Physics Lab/Jigwi Pattern Audio")]
    public sealed class LabJigwiPatternAudioProfile : ScriptableObject
    {
        public const string ResourcesPath = "Jigwi/PatternAudio/PatternAudioProfile";
        public const string PersonalClipResource = "Jigwi/PatternAudio/JigwiA";
        public const string GroupClipResource = "Jigwi/PatternAudio/JigwiB";
        [SerializeField] private AudioClip personalClip, groupClip;
        [Tooltip("User-approved initial cue. Adjust locally to align the start of '요' in the original recording.")]
        [SerializeField, Min(.01f)] private float personalCueSeconds = 1.05f;
        [Tooltip("User-approved initial cue. Adjust locally to align the start of '줘' in the original recording.")]
        [SerializeField, Min(.01f)] private float groupCueSeconds = 2.08f;
        [SerializeField, Range(0f, 1f)] private float personalVolume = 1f, groupVolume = 1f;

        public AudioClip ClipFor(LabJigwiABKind kind) => kind == LabJigwiABKind.PersonalBurn ? personalClip : groupClip;
        public float CueSecondsFor(LabJigwiABKind kind)
        {
            AudioClip clip = ClipFor(kind);
            float fallback = kind == LabJigwiABKind.PersonalBurn ? 1.05f : 2.08f;
            float value = kind == LabJigwiABKind.PersonalBurn ? personalCueSeconds : groupCueSeconds;
            value = float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(.01f, value);
            return clip != null ? Mathf.Min(value, Mathf.Max(.01f, clip.length)) : value;
        }
        public float VolumeFor(LabJigwiABKind kind)
        {
            float value = kind == LabJigwiABKind.PersonalBurn ? personalVolume : groupVolume;
            return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);
        }
        internal static LabJigwiPatternAudioProfile MakeFallback()
        {
            var value = CreateInstance<LabJigwiPatternAudioProfile>();
            value.name = "Jigwi pattern audio fallback"; value.hideFlags = HideFlags.HideAndDontSave;
            value.personalClip = Resources.Load<AudioClip>(PersonalClipResource);
            value.groupClip = Resources.Load<AudioClip>(GroupClipResource);
            return value;
        }
    }
}
