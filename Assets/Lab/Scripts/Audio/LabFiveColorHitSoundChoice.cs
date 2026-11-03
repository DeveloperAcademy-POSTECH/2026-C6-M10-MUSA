using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 오색구슬 명중 / 콤보 BIG 큰 공 명중 소리 시험과 먼지 효과 크기 (#464, 시험용).
    /// Play 중에 Inspector에서 바꾸면 다음 명중부터 바로 반영된다.
    /// LabConfig는 판마다 복사본을 쓰므로 Play 중 변경이 반영되지 않아, 시험용 설정을 따로 둔다.
    /// 에셋이 없으면 지금처럼 징을 쓰고, 먼지 크기는 기본값을 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "C6 Physics Lab/Five Color Hit Sound Choice")]
    public sealed class LabFiveColorHitSoundChoice : ScriptableObject
    {
        public const string ResourcesPath = "SoundCompare/FiveColorHitChoice";
        public const string TestFolder = "Audio/FiveColorHitTest";
        public const float DefaultDustPuffScale = 2f;

        [Tooltip("0 = 지금 징 (Audio/FiveColorHit), 1~5 = 시험 소리 (Audio/FiveColorHitTest/FiveColorHitTest_번호)")]
        [Range(0, 5)] public int sound = 1;
        [Tooltip("시험 소리 크기 (징 크기는 기존 설정 그대로)")]
        [Range(0f, 1f)] public float testVolume = 1f;
        [Tooltip("오색구슬 명중 먼지 뭉게구름 크기 배율")]
        [Range(.5f, 5f)] public float dustPuffScale = DefaultDustPuffScale;

        private static LabFiveColorHitSoundChoice cached;
        private static bool loaded;
        private static AudioClip[] testClips;

        public static LabFiveColorHitSoundChoice Current
        {
            get
            {
                if (!loaded) { cached = Resources.Load<LabFiveColorHitSoundChoice>(ResourcesPath); loaded = true; }
                return cached;
            }
        }

        /// <summary>고른 시험 소리. 0번(징)을 골랐거나 파일이 없으면 null.</summary>
        public static AudioClip TestClip
        {
            get
            {
                if (Current == null || Current.sound <= 0) return null;
                AudioClip[] clips = TestClips;
                return Current.sound <= clips.Length ? clips[Current.sound - 1] : null;
            }
        }

        public static float TestVolume => Current != null ? Mathf.Clamp01(Current.testVolume) : 1f;
        public static float DustPuffScale => Current != null ? Mathf.Max(.1f, Current.dustPuffScale) : DefaultDustPuffScale;

        public static AudioClip[] TestClips
        {
            get
            {
                if (testClips == null)
                {
                    testClips = Resources.LoadAll<AudioClip>(TestFolder);
                    System.Array.Sort(testClips, (a, b) => string.CompareOrdinal(a.name, b.name));
                }
                return testClips;
            }
        }
    }
}
