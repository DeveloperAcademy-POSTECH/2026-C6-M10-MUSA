using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 박 색상 콤보 히트 효과음 (#338). 콤보에서 맞는 칸을 맞힐 때마다 같은 소리를 칸이 오를수록 높게 낸다
    /// (기본 1칸 도 → 2칸 레 → 3칸 미 → 4칸 솔). 팀이 함께 채우는 콤보라 "좋다"(LabComboClearSound)처럼 모든 기기에서 난다.
    /// 마지막 칸을 맞혀 성공하면 4칸 소리와 "좋다"가 함께 난다.
    /// Host 스냅샷의 콤보 순번(comboCount)과 맞힌 칸 수(comboProgress)가 같은 콤보 안에서 늘었을 때 한 번만 낸다.
    /// 새 라운드·세션·콤보면 처음부터 다시 세고, 접속 직후 이미 맞혀 둔 칸은 내지 않는다.
    /// 소리 파일은 Resources/Audio/ComboHit 폴더의 파일을 쓰고(여러 개면 첫 파일), 없으면 조용히 넘어간다.
    /// 크기는 volume × 일시정지 메뉴 효과음 설정.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabComboHitSound : MonoBehaviour
    {
        public const string ClipFolder = "Audio/ComboHit";

        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("1칸~4칸을 맞혔을 때 원래 소리보다 몇 반음 높일지. 기본 도·레·미·솔 (0, 2, 4, 7).")]
        public float[] stepSemitones = { 0f, 2f, 4f, 7f };

        private AudioClip clip;
        private AudioSource[] sources;
        private LabNetwork network;
        private string session;
        private uint round;
        private uint comboCount;
        private int progress;

        public bool HasClip => clip != null;
        /// <summary>낸 횟수 (테스트·확인용). 소리 파일이 없어도 칸을 맞힌 횟수는 센다.</summary>
        public int PlayCount { get; private set; }
        /// <summary>마지막으로 낸 칸 (1~4). 아직 없으면 0.</summary>
        public int LastStep { get; private set; }

        public void Bind(LabNetwork target) => network = target;

        private void Awake()
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>(ClipFolder);
            clip = clips != null && clips.Length > 0 ? clips[0] : null;
            // 칸마다 높이가 다르다. 앞 칸 소리가 끝나기 전에 다음 칸이 와도 앞 소리 높이가 바뀌지 않게 칸마다 따로 둔다.
            sources = new AudioSource[LabGourdCombo.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f; // 화면 기준 소리
            }
        }

        private void Update()
        {
            if (network != null) Observe(network.Snapshot);
        }

        /// <summary>
        /// 스냅샷에서 새로 맞힌 칸을 찾으면 그 칸(1~4)의 소리를 내고 그 칸을 돌려준다. 없으면 0.
        /// 스냅샷 사이에 칸이 여럿 늘었으면 마지막 칸 소리 하나만 낸다.
        /// </summary>
        public int Observe(LabSnapshot snapshot)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing) return 0;
            if (snapshot.sessionId != session || snapshot.round != round || snapshot.comboCount < comboCount)
            {
                session = snapshot.sessionId;
                round = snapshot.round;
                comboCount = snapshot.comboCount;
                progress = snapshot.comboProgress;
                return 0;
            }
            if (snapshot.comboCount != comboCount)
            {
                // 새 콤보: 처음부터 센다. 첫 스냅샷이 이미 칸을 맞힌 뒤라면 그 칸을 낸다.
                comboCount = snapshot.comboCount;
                progress = 0;
            }
            if (snapshot.comboProgress <= progress)
            {
                progress = Mathf.Min(progress, snapshot.comboProgress);
                return 0;
            }
            progress = snapshot.comboProgress;
            int step = Mathf.Clamp(progress, 1, LabGourdCombo.Length);
            PlayStep(step);
            return step;
        }

        /// <summary>step칸(1~4)의 높이로 소리를 낸다.</summary>
        public void PlayStep(int step)
        {
            step = Mathf.Clamp(step, 1, LabGourdCombo.Length);
            PlayCount++;
            LastStep = step;
            if (clip == null || sources == null) return;
            AudioSource source = sources[step - 1];
            source.pitch = PitchFor(step);
            source.PlayOneShot(clip, volume * LabAudioSettings.SfxVolume);
        }

        /// <summary>step칸(1~4)의 재생 속도. 반음 하나는 2^(1/12)배다.</summary>
        public float PitchFor(int step)
        {
            int index = Mathf.Clamp(step, 1, LabGourdCombo.Length) - 1;
            float semitones = stepSemitones != null && index < stepSemitones.Length && !float.IsNaN(stepSemitones[index])
                ? Mathf.Clamp(stepSemitones[index], -24f, 24f) : 0f;
            return Mathf.Pow(2f, semitones / 12f);
        }
    }
}
