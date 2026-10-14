using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// 전투 BGM. 방장이 보낸 스냅샷의 전투 상태만 보고 재생을 정한다.
    /// - 로비·방 만들기: 조용함
    /// - 전투 시작: 처음부터 반복 재생 (새 라운드마다 처음부터)
    /// - 일시정지: 멈춤 → 다시 시작하면 멈춘 지점부터 이어서
    /// - 승리/패배: 서서히 꺼짐
    /// 곡은 Resources/Audio/BGM 폴더의 첫 번째 소리 파일을 쓴다. 파일이 없으면 조용히 넘어간다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabBattleBgm : MonoBehaviour
    {
        public const string ClipFolder = "Audio/BGM";

        [Range(0f, 1f)] public float volume = .5f;
        [Min(0f)] public float fadeOutSeconds = 1.5f;

        private AudioSource source;
        private LabNetwork network;
        private string playingRound;
        private bool fading;

        public bool IsPlaying => source != null && source.isPlaying;

        // 기본 크기 × 이 기기의 BGM 설정
        private float TargetVolume => volume * LabAudioSettings.BgmVolume;

        public void Bind(LabNetwork target) => network = target;

        private void Awake()
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>(ClipFolder);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = TargetVolume;
            if (clips.Length > 0) source.clip = clips[0];
        }

        private void Update()
        {
            if (source == null || source.clip == null) return;
            LabSnapshot snapshot = network != null && network.IsConnected ? network.Snapshot : null;

            if (snapshot == null)
            {
                // 로비로 돌아왔거나 연결이 끊김
                StopNow();
                return;
            }

            if (snapshot.phase == LabPhase.Playing)
            {
                // 설정에서 크기를 바꾸면 바로 반영된다.
                source.volume = TargetVolume;
                string round = snapshot.sessionId + ":" + snapshot.round;
                if (round != playingRound)
                {
                    // 새 라운드는 처음부터
                    playingRound = round;
                    fading = false;
                    source.volume = TargetVolume;
                    source.Stop();
                    source.time = 0f;
                    if (!snapshot.paused) source.Play();
                    return;
                }
                if (snapshot.paused)
                {
                    if (source.isPlaying) source.Pause();
                }
                else if (!source.isPlaying)
                {
                    source.volume = TargetVolume;
                    source.UnPause();
                    if (!source.isPlaying) source.Play();
                }
                return;
            }

            // 승리/패배: 서서히 끄기
            if (source.isPlaying)
            {
                fading = true;
                float step = fadeOutSeconds <= 0f ? 1f : TargetVolume * Time.unscaledDeltaTime / fadeOutSeconds;
                source.volume = Mathf.Max(0f, source.volume - step);
                if (source.volume <= 0f) StopNow();
            }
        }

        private void StopNow()
        {
            if (source.isPlaying || fading) source.Stop();
            fading = false;
            source.volume = TargetVolume;
            if (network == null || network.Snapshot == null) playingRound = null;
        }
    }
}
