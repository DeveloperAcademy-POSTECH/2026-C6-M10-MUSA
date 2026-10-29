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
    /// - 구미호 스테이지 (#351)와 2:2 땅따먹기 (#293): Resources/Audio/GumihoBGM 폴더에 곡이 있으면 그 곡을 대신 튼다
    ///   (크기는 LabConfig의 Gumiho Bgm Volume). 어느 곡을 틀지는 라운드가 시작될 때 정한다. 야광귀·지귀·Lab 단판은
    ///   기본 전투 BGM. 2:2에는 피버타임 곡이 없다(팀 피버에도 곡을 바꾸지 않는다).
    /// - 야광귀 피버타임 (#343): 피버가 시작되면 Resources/Audio/Fever 폴더의 첫 파일(휘모리처럼 점점 빨라지는 꽹과리 +
    ///   태평소 능게, 피버 20초에 맞춘 곡)로 바꿔 처음부터 튼다. 늦게 들어온 기기는 피버가 흐른 만큼 건너뛰어 맞춘다.
    ///   전투 BGM은 그동안 줄여 둔다. 일시정지·승리/패배·새 라운드는 전투 BGM과 똑같이 다룬다.
    ///   피버가 시작되는 순간에는 콤보 성공 추임새 "좋다"(Resources/Audio/ComboClear)를 한 번 낸다. 늦게 들어와 이미 한참 지난
    ///   피버에서는 내지 않는다. 크기는 cueVolume × 일시정지 메뉴 효과음 설정.
    /// - 구미호 피버타임 (#334, #351): 남은 시간이 피버 시간(기본 20초)이 된 순간부터 Resources/Audio/GumihoFever 폴더의 곡을
    ///   야광귀 피버와 똑같이 튼다 (크기는 LabConfig의 Gumiho Fever Bgm Volume). 폴더가 비어 있으면 야광귀 피버 곡을 쓴다.
    ///   구미호 피버는 Host 시각(feverTimeAt)으로 정해지고 구미호 스테이지에만 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabBattleBgm : MonoBehaviour
    {
        public const string ClipFolder = "Audio/BGM";
        public const string FeverClipFolder = "Audio/Fever";
        public const string GumihoClipFolder = "Audio/GumihoBGM";
        public const string GumihoFeverClipFolder = "Audio/GumihoFever";

        [Tooltip("LabConfig를 읽을 수 없을 때만 쓰는 기본 크기. 평소에는 LabConfig의 BGM Volume을 쓴다.")]
        [Range(0f, 1f)] public float volume = .2f;
        [Min(0f)] public float fadeOutSeconds = 1.5f;
        [Tooltip("야광귀 피버 곡 크기 (전투 BGM 크기에 곱한다). 구미호 피버는 LabConfig의 Gumiho Fever Bgm Volume을 쓴다.")]
        [Range(0f, 2f)] public float feverVolume = 1f;
        [Tooltip("피버가 시작될 때 전투 BGM에서 피버 곡으로 넘어가는 시간 (초)")]
        [Min(0f)] public float feverCrossfadeSeconds = .25f;
        [Tooltip("피버가 시작될 때 내는 추임새(콤보 성공과 같은 파일) 크기")]
        [Range(0f, 1f)] public float cueVolume = 1f;
        // 피버가 시작된 지 이 초 안에 알아챈 기기만 추임새를 낸다 (다시 접속한 기기가 뒤늦게 내지 않게).
        private const double CueWindowSeconds = 1d;

        private AudioSource source, feverSource, cueSource;
        private AudioClip battleClip, gumihoClip, feverClip, gumihoFeverClip;
        private AudioClip[] cueClips;
        /// <summary>피버 시작 추임새를 낸 횟수 (테스트·확인용)</summary>
        public int FeverCueCount { get; private set; }
        private LabNetwork network;
        private string playingRound;
        private bool fading, feverOn;
        // 이번 라운드에 구미호 전투 BGM을 트는지, 지금 피버가 구미호 피버인지 (#351)
        private bool gumihoBgm, gumihoFever;
        private float feverBlend; // 0 = 전투 BGM, 1 = 피버 곡
        private double seenServerTime = -1d;
        private float seenAt;

        public bool IsPlaying => source != null && source.isPlaying;
        /// <summary>피버 곡을 틀고 있는지 (#343)</summary>
        public bool FeverPlaying => feverSource != null && feverSource.isPlaying;
        public bool HasFeverClip => feverClip != null;
        /// <summary>지금 틀고 있는 전투 BGM과 피버 곡 (테스트·확인용, #351)</summary>
        public AudioClip BattleClip => source != null ? source.clip : null;
        public AudioClip FeverClip => feverSource != null ? feverSource.clip : null;

        // 기본 크기(LabConfig: Inspector·DEV, 구미호 전투 BGM은 Inspector) × 이 기기의 BGM 설정(일시정지 메뉴)
        private float BaseVolume => network == null || network.config == null ? volume
            : gumihoBgm ? network.config.GumihoBgmVolume : network.config.BgmVolume;
        private float TargetVolume => BaseVolume * LabAudioSettings.BgmVolume;
        private float FeverTargetVolume => (gumihoFever && network != null && network.config != null
            ? network.config.GumihoFeverBgmVolume : BaseVolume * feverVolume) * LabAudioSettings.BgmVolume;

        public void Bind(LabNetwork target) => network = target;

        private void Awake()
        {
            battleClip = FirstClip(ClipFolder);
            gumihoClip = FirstClip(GumihoClipFolder);
            feverClip = FirstClip(FeverClipFolder);
            gumihoFeverClip = FirstClip(GumihoFeverClipFolder);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = TargetVolume;
            source.clip = battleClip;
            feverSource = gameObject.AddComponent<AudioSource>();
            feverSource.playOnAwake = false;
            feverSource.loop = true; // 피버가 곡보다 길어지면(DEV로 전투를 늘린 경우) 다시 처음부터
            feverSource.spatialBlend = 0f;
            feverSource.volume = 0f;
            feverSource.clip = feverClip;
            cueClips = Resources.LoadAll<AudioClip>(LabComboClearSound.ClipFolder);
            cueSource = gameObject.AddComponent<AudioSource>();
            cueSource.playOnAwake = false;
            cueSource.spatialBlend = 0f;
        }

        private static AudioClip FirstClip(string folder)
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>(folder);
            return clips.Length > 0 ? clips[0] : null;
        }

        /// <summary>
        /// 피버 곡 쪽으로 얼마나 넘어갔는지(0~1)에 맞춰 두 곡의 크기를 정한다. 피버가 시작되면 피버 곡을 피버가 흐른 만큼에서 튼다.
        /// 일시정지 동안에는 피버를 켜거나 끄지 않는다 (멈춘 시계를 어림한 시각이 피버 시작을 앞질러도 흔들리지 않게).
        /// </summary>
        private void DriveFever(LabSnapshot snapshot)
        {
            bool gumiho = false;
            double elapsed = snapshot.paused ? -1d : FeverElapsed(snapshot, out gumiho);
            AudioClip clip = gumiho && gumihoFeverClip != null ? gumihoFeverClip : feverClip;
            bool fever = snapshot.paused ? feverOn : elapsed >= 0d && clip != null;
            if (fever && !feverOn)
            {
                feverOn = true;
                gumihoFever = gumiho;
                feverSource.Stop();
                feverSource.clip = clip;
                feverSource.time = Mathf.Clamp((float)elapsed, 0f, Mathf.Max(0f, clip.length - .05f));
                feverSource.Play();
                if (elapsed < CueWindowSeconds) PlayFeverCue();
            }
            else if (!fever && feverOn) StopFever();
            float step = feverCrossfadeSeconds <= 0f ? 1f : Time.unscaledDeltaTime / feverCrossfadeSeconds;
            feverBlend = Mathf.MoveTowards(feverBlend, feverOn ? 1f : 0f, step);
            source.volume = TargetVolume * (1f - feverBlend);
            feverSource.volume = FeverTargetVolume * feverBlend;
            if (!feverOn) return;
            if (snapshot.paused) { if (feverSource.isPlaying) feverSource.Pause(); }
            else if (!feverSource.isPlaying) { feverSource.UnPause(); if (!feverSource.isPlaying) feverSource.Play(); }
        }

        /// <summary>
        /// 이 스냅샷에서 피버가 시작된 지 몇 초가 지났는지. 피버가 아니면 -1. 야광귀 피버 (#343)는 방해가 끝난 뒤부터, 구미호
        /// 피버 (#334)는 남은 시간이 피버 시간이 된 Host 시각(feverTimeAt)부터다. gumiho는 구미호 피버이면 true.
        /// </summary>
        private double FeverElapsed(LabSnapshot snapshot, out bool gumiho)
        {
            gumiho = false;
            if (network == null) return -1d;
            double yaguang = network.YaguangFeverElapsed(snapshot);
            if (yaguang >= 0d) return yaguang;
            double now = ServerNow(snapshot);
            if (!snapshot.FeverTimeOn(now)) return -1d;
            gumiho = true;
            return now - snapshot.feverTimeAt;
        }

        // 스냅샷 사이에도 Host 시각을 이어서 어림한다 (LabFeverTimeHud와 같은 방식, 최대 1초).
        private double ServerNow(LabSnapshot snapshot)
        {
            float realtime = Time.realtimeSinceStartup;
            if (snapshot.serverTime != seenServerTime)
            {
                seenServerTime = snapshot.serverTime;
                seenAt = realtime;
            }
            return snapshot.serverTime + System.Math.Min(1d, realtime - seenAt);
        }

        /// <summary>피버 시작 추임새 "좋다"를 한 번 낸다 (콤보 성공과 같은 파일).</summary>
        private void PlayFeverCue()
        {
            FeverCueCount++;
            if (cueClips == null || cueClips.Length == 0 || cueSource == null) return;
            cueSource.PlayOneShot(cueClips[Random.Range(0, cueClips.Length)], cueVolume * LabAudioSettings.SfxVolume);
        }

        private void StopFever()
        {
            feverOn = false;
            gumihoFever = false;
            feverBlend = 0f;
            if (feverSource != null)
            {
                feverSource.Stop();
                feverSource.volume = 0f;
            }
        }

        private void Update()
        {
            if (source == null || battleClip == null && gumihoClip == null) return;
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
                    // 새 라운드는 처음부터. 구미호 스테이지 (#351)와 2:2 (#293)는 구미호 전투 BGM.
                    playingRound = round;
                    fading = false;
                    StopFever();
                    gumihoBgm = (network.GameStageYokai == LabStageYokai.Gumiho || snapshot.teamTerritory) && gumihoClip != null;
                    source.Stop();
                    source.clip = gumihoBgm ? gumihoClip : battleClip;
                    source.volume = TargetVolume;
                    source.time = 0f;
                    if (!snapshot.paused && source.clip != null) source.Play();
                    return;
                }
                if (source.clip == null) return;
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
                float feverStep = fadeOutSeconds <= 0f ? 1f : FeverTargetVolume * Time.unscaledDeltaTime / fadeOutSeconds;
                source.volume = Mathf.Max(0f, source.volume - step);
                if (feverSource != null) feverSource.volume = Mathf.Max(0f, feverSource.volume - feverStep);
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
