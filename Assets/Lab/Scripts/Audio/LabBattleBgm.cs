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
    /// - 야광귀 피버타임 (#343): 피버가 시작되면 Resources/Audio/Fever 폴더의 첫 파일(휘모리처럼 점점 빨라지는 꽹과리 +
    ///   태평소 능게, 피버 20초에 맞춘 곡)로 바꿔 처음부터 튼다. 늦게 들어온 기기는 피버가 흐른 만큼 건너뛰어 맞춘다.
    ///   전투 BGM은 그동안 줄여 둔다. 일시정지·승리/패배·새 라운드는 전투 BGM과 똑같이 다룬다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabBattleBgm : MonoBehaviour
    {
        public const string ClipFolder = "Audio/BGM";
        public const string FeverClipFolder = "Audio/Fever";

        [Tooltip("LabConfig를 읽을 수 없을 때만 쓰는 기본 크기. 평소에는 LabConfig의 BGM Volume을 쓴다.")]
        [Range(0f, 1f)] public float volume = .2f;
        [Min(0f)] public float fadeOutSeconds = 1.5f;
        [Tooltip("피버 곡 크기 (전투 BGM 크기에 곱한다)")]
        [Range(0f, 2f)] public float feverVolume = 1f;
        [Tooltip("피버가 시작될 때 전투 BGM에서 피버 곡으로 넘어가는 시간 (초)")]
        [Min(0f)] public float feverCrossfadeSeconds = .25f;

        private AudioSource source, feverSource;
        private LabNetwork network;
        private string playingRound;
        private bool fading, feverOn;
        private float feverBlend; // 0 = 전투 BGM, 1 = 피버 곡

        public bool IsPlaying => source != null && source.isPlaying;
        /// <summary>피버 곡을 틀고 있는지 (#343)</summary>
        public bool FeverPlaying => feverSource != null && feverSource.isPlaying;
        public bool HasFeverClip => feverSource != null && feverSource.clip != null;

        // 기본 크기(LabConfig: Inspector·DEV) × 이 기기의 BGM 설정(일시정지 메뉴)
        private float BaseVolume => network != null && network.config != null ? network.config.BgmVolume : volume;
        private float TargetVolume => BaseVolume * LabAudioSettings.BgmVolume;

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
            AudioClip[] fever = Resources.LoadAll<AudioClip>(FeverClipFolder);
            feverSource = gameObject.AddComponent<AudioSource>();
            feverSource.playOnAwake = false;
            feverSource.loop = true; // 피버가 곡보다 길어지면(DEV로 전투를 늘린 경우) 다시 처음부터
            feverSource.spatialBlend = 0f;
            feverSource.volume = 0f;
            if (fever.Length > 0) feverSource.clip = fever[0];
        }

        /// <summary>
        /// 피버 곡 쪽으로 얼마나 넘어갔는지(0~1)에 맞춰 두 곡의 크기를 정한다. 피버가 시작되면 피버 곡을 피버가 흐른 만큼에서 튼다.
        /// </summary>
        private void DriveFever(LabSnapshot snapshot)
        {
            double elapsed = network != null ? network.YaguangFeverElapsed(snapshot) : -1d;
            bool fever = elapsed >= 0d && HasFeverClip;
            if (fever && !feverOn)
            {
                feverOn = true;
                feverSource.Stop();
                feverSource.time = Mathf.Clamp((float)elapsed, 0f, Mathf.Max(0f, feverSource.clip.length - .05f));
                if (!snapshot.paused) feverSource.Play();
            }
            else if (!fever && feverOn) StopFever();
            float step = feverCrossfadeSeconds <= 0f ? 1f : Time.unscaledDeltaTime / feverCrossfadeSeconds;
            feverBlend = Mathf.MoveTowards(feverBlend, feverOn ? 1f : 0f, step);
            source.volume = TargetVolume * (1f - feverBlend);
            feverSource.volume = TargetVolume * feverVolume * feverBlend;
            if (!feverOn) return;
            if (snapshot.paused) { if (feverSource.isPlaying) feverSource.Pause(); }
            else if (!feverSource.isPlaying) { feverSource.UnPause(); if (!feverSource.isPlaying) feverSource.Play(); }
        }

        private void StopFever()
        {
            feverOn = false;
            feverBlend = 0f;
            if (feverSource != null)
            {
                feverSource.Stop();
                feverSource.volume = 0f;
            }
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
                    StopFever();
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
                DriveFever(snapshot);
                return;
            }

            // 승리/패배: 서서히 끄기 (피버 곡도 함께)
            if (source.isPlaying || FeverPlaying)
            {
                fading = true;
                float step = fadeOutSeconds <= 0f ? 1f : TargetVolume * Time.unscaledDeltaTime / fadeOutSeconds;
                source.volume = Mathf.Max(0f, source.volume - step);
                if (feverSource != null) feverSource.volume = Mathf.Max(0f, feverSource.volume - step * Mathf.Max(1f, feverVolume));
                if (source.volume <= 0f && (feverSource == null || feverSource.volume <= 0f)) StopNow();
            }
        }

        private void StopNow()
        {
            if (source.isPlaying || fading) source.Stop();
            StopFever();
            fading = false;
            source.volume = TargetVolume;
            if (network == null || network.Snapshot == null) playingRound = null;
        }
    }
}
