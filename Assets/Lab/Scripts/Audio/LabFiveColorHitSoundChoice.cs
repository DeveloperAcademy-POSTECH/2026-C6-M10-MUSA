using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 오색구슬 명중 / 콤보 BIG 큰 공 명중 소리와 오색구슬 명중 효과 시험 (#464, 시험용).
    /// Play 중에 Inspector에서 바꾸면 다음 명중부터 바로 반영된다.
    /// LabConfig는 판마다 복사본을 쓰므로 Play 중 변경이 반영되지 않아, 시험용 설정을 따로 둔다.
    /// 에셋이 없으면 기본 소리(Audio/FiveColorHit, 25현 가야금) + 별가루를 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "C6 Physics Lab/Five Color Hit Sound Choice")]
    public sealed class LabFiveColorHitSoundChoice : ScriptableObject
    {
        /// <summary>시험 소리. 숫자는 Audio/FiveColorHitTest/FiveColorHitTest_번호 파일과 같다 (0은 기본 소리).</summary>
        public enum Sound
        {
            [InspectorName("0 · 기본 (25현 가야금)")] Gong = 0,
            [InspectorName("1 · 건고 타격음 (강)")] Test1 = 1,
            [InspectorName("2 · 교방고 타격음 (강)")] Test2 = 2,
            [InspectorName("3 · 바라 타격음 (강)")] Test3 = 3,
            [InspectorName("4 · 뿌직 up")] Test4 = 4,
            [InspectorName("5 · 진고 타격음 (강)")] Test5 = 5,
            [InspectorName("6 · 가야금 오음 C D F G B♭ (오르내림 번갈아)")] Test6 = 6,
            [InspectorName("7 · 가야금 오음 띠리리리링 (낮게)")] Test7 = 7,
            [InspectorName("8 · 새 소리 1/2 번갈아")] Test8 = 8
        }

        public enum HitEffect
        {
            [InspectorName("별가루")] Stars = 0,
            [InspectorName("먼지 털림")] Dust = 1
        }

        public const string ResourcesPath = "SoundCompare/FiveColorHitChoice";
        public const string TestFolder = "Audio/FiveColorHitTest";
        public const float DefaultDustPuffScale = 2f;

        [Header("시험 편의")]
        [Tooltip("켜면 어느 스테이지든 북을 누르면 바로 오색구슬이 나온다 (피버와 같은 규칙). 에디터/개발 빌드에서만, 판정하는 Host 기기 기준. Test Runner 돌릴 때는 끌 것.")]
        public bool drumAlwaysFiveColor;

        [Header("소리 (오색구슬 명중 + BIG 큰 공 명중)")]
        [Tooltip("0 = 기본 소리 (Audio/FiveColorHit, #464부터 25현 가야금), 1~8 = 시험 소리 (Audio/FiveColorHitTest/FiveColorHitTest_번호, 같은 번호가 여러 개면 번갈아)")]
        public Sound sound = Sound.Test1;
        [Tooltip("시험 소리 크기 (기본 소리 크기는 기존 설정 그대로)")]
        [Range(0f, 1f)] public float testVolume = 1f;

        [Header("오색구슬 명중 효과")]
        public HitEffect effect = HitEffect.Dust;
        [Tooltip("별가루 크기 배율 (별 크기와 퍼지는 거리 함께)")]
        [Range(.5f, 5f)] public float starScale = 1f;
        [Tooltip("먼지 뭉게구름 크기 배율")]
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

        private static int testTurn;

        /// <summary>
        /// 이번에 낼 시험 소리. 징(0)을 골랐거나 파일이 없으면 null.
        /// 같은 번호 파일이 여러 개면(FiveColorHitTest_6, FiveColorHitTest_6_2, ...) 부를 때마다 이름 순서대로 번갈아 낸다.
        /// </summary>
        public static AudioClip NextTestClip()
        {
            int number = Current != null ? (int)Current.sound : 0;
            if (number <= 0) return null;
            string name = "FiveColorHitTest_" + number;
            var matches = new System.Collections.Generic.List<AudioClip>();
            foreach (AudioClip clip in TestClips)
                if (clip != null && (clip.name == name || clip.name.StartsWith(name + "_", System.StringComparison.Ordinal))) matches.Add(clip);
            if (matches.Count == 0) return null;
            return matches[testTurn++ % matches.Count];
        }

        /// <summary>시험용: 북에서 늘 오색구슬이 나오는지 (에디터/개발 빌드에서만)</summary>
        public static bool DrumAlwaysFiveColor => Debug.isDebugBuild && Current != null && Current.drumAlwaysFiveColor;
        public static float TestVolume => Current != null ? Mathf.Clamp01(Current.testVolume) : 1f;
        public static bool UseDust => Current != null && Current.effect == HitEffect.Dust;
        public static float StarScale => Current != null ? Mathf.Max(.1f, Current.starScale) : 1f;
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
