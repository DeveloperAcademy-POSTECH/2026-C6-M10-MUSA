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
    /// - #577 라운드별 곡: 라운드마다 자기 폴더 곡을 튼다. 1라운드 구미호 = GumihoBGM(대금), 2라운드 야광귀 = YaguangBGM(꽹과리),
    ///   3라운드 지귀 = JigwiBGM(진고 북). 크기는 각각 Gumiho / Yaguang / Jigwi Bgm Volume.
    /// - 야광귀 스테이지 (#396): Resources/Audio/YaguangBGM의 곡을 튼다(크기는 Yaguang Bgm Volume). LabConfig Inspector의
    ///   Yaguang Use New Bgm(기본 켜짐)을 끄면 기존 꽹과리 BGM. 전투 중에 바꾸면 바로 처음부터 바꿔 튼다.
    /// - 구미호 스테이지 (#351): Resources/Audio/GumihoBGM 폴더에 곡이 있으면 그 곡을 대신 튼다 (크기는 LabConfig의
    ///   Gumiho Bgm Volume). 땅따먹기 (#293)는 Resources/Audio/TerritoryBGM의 곡(#409에서 구미호 곡을 바꾸기 전의 곡)을
    ///   같은 크기로 틀고, 폴더가 비어 있으면 구미호 곡을 쓴다. 어느 곡을 틀지는 라운드가 시작될 때 정한다.
    ///   야광귀·지귀·Lab 단판은 기본 전투 BGM. 땅따먹기에는 피버타임 곡이 없다(팀 피버에도 곡을 바꾸지 않는다).
    /// - #577 피버 곡: 세 라운드 모두 그 라운드 곡을 반음 2개 높이고 점점 빠르게 한 20초 곡. 구미호 = GumihoFever, 야광귀 = Fever,
    ///   지귀 = JigwiFever. 지귀 피버는 방해가 끝나는 100초(#332, 스테이지 피버 #441과 같은 시각)부터다 (#629에서 연결).
    /// - 야광귀 피버타임 (#343): 피버가 시작되면 Resources/Audio/Fever 폴더의 첫 파일(피버 20초에 맞춘 곡)로 바꿔 처음부터 튼다. 늦게 들어온 기기는 피버가 흐른 만큼 건너뛰어 맞춘다.
    ///   전투 BGM은 그동안 줄여 둔다. 일시정지·승리/패배·새 라운드는 전투 BGM과 똑같이 다룬다.
    ///   피버가 시작되는 순간에는 콤보 성공 추임새 "좋다"(Resources/Audio/ComboClear)를 한 번 낸다. 늦게 들어와 이미 한참 지난
    ///   피버에서는 내지 않는다. 크기는 cueVolume × 일시정지 메뉴 효과음 설정.
    /// - 구미호 피버타임 (#334, #351): 남은 시간이 피버 시간(기본 20초)이 된 순간부터 Resources/Audio/GumihoFever 폴더의 곡을
    ///   야광귀 피버와 똑같이 튼다 (크기는 LabConfig의 Gumiho Fever Bgm Volume). 폴더가 비어 있으면 야광귀 피버 곡을 쓴다.
    ///   구미호 피버는 Host 시각(feverTimeAt)으로 정해지고 구미호 스테이지에만 있다.
    /// - 야광귀 콤보 BIG/SMALL 톤: BIG 배지가 떠 있는 동안 전투 BGM이 Inspector 반음(기본 2)만큼 높아지고, SMALL 동안
    ///   그만큼 낮아진다(AudioSource.pitch라 빠르기도 같이 바뀐다). 배지가 나오고 사라지는 때(LabYaguangSound의 SMALL/BIG
    ///   시작/끝 소리와 같은 때)에 Inspector 초(기본 0.3초)에 걸쳐 바뀐다. 모든 기기. 피버 곡은 바꾸지 않는다.
    /// - #745 남은 30초 빨라지기: "30초 남았소!"(LabThirtySecondsLeftHud와 같은 시각과 조건)부터 피버 곡으로 넘어갈 때까지 라운드 곡을
    ///   음높이는 그대로 빠르기만 1.15배로 튼다. AudioSource.pitch는 음높이도 같이 올리므로, 미리 늘려 둔 곡
    ///   (Resources/Audio/{Gumiho,Yaguang,Jigwi}BGMRush, rubberband tempo 1.15)을 같은 자리(지금 시각 ÷ 1.15)에서 이어 받아
    ///   rushCrossfadeSeconds에 걸쳐 바꾼다. 효과음 / 대사 / 피버 곡은 그대로. 튜토리얼 / 땅따먹기 / Lab 단판은 30초 알림이 없어 그대로.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabBattleBgm : MonoBehaviour
    {
        public const string ClipFolder = "Audio/BGM";
        public const string FeverClipFolder = "Audio/Fever";
        public const string GumihoClipFolder = "Audio/GumihoBGM";
        public const string GumihoFeverClipFolder = "Audio/GumihoFever";
        /// <summary>야광귀 스테이지 전투 BGM (#396). LabConfig Inspector의 Yaguang Use New Bgm(기본 켜짐)을 끄면 기존 BGM.</summary>
        public const string YaguangClipFolder = "Audio/YaguangBGM";
        /// <summary>땅따먹기 전투 BGM (#409): 구미호 스테이지 곡이 바뀌기 전의 곡. 크기는 Gumiho Bgm Volume.</summary>
        public const string TerritoryClipFolder = "Audio/TerritoryBGM";
        /// <summary>#577 지귀 스테이지 전투 BGM (진고 북이 중심인 무거운 곡). 크기는 LabConfig Jigwi Bgm Volume. 비어 있으면 기본 BGM.</summary>
        public const string JigwiClipFolder = "Audio/JigwiBGM";
        /// <summary>#577 지귀 피버(마지막 20초) 곡. 크기는 LabConfig Jigwi Fever Bgm Volume. 비어 있으면 지금처럼 구미호 피버 곡.</summary>
        public const string JigwiFeverClipFolder = "Audio/JigwiFever";
        /// <summary>#629 튜토리얼(연습판) 곡. 크기는 LabConfig Tutorial Bgm Volume. 비어 있으면 지금처럼 기본 BGM.</summary>
        public const string TutorialClipFolder = "Audio/TutorialBGM";
        /// <summary>#745 남은 30초부터 트는, 음높이는 그대로 1.15배 빠르게 늘린 라운드 곡.</summary>
        public const string GumihoRushClipFolder = "Audio/GumihoBGMRush", YaguangRushClipFolder = "Audio/YaguangBGMRush",
            JigwiRushClipFolder = "Audio/JigwiBGMRush";
        /// <summary>#745 빨라진 곡의 빠르기. 곡 파일을 이 빠르기로 미리 만들어 두었으므로 바꾸려면 파일도 다시 만든다.</summary>
        public const float RushSpeed = 1.15f;

        [Tooltip("LabConfig를 읽을 수 없을 때만 쓰는 기본 크기. 평소에는 LabConfig의 BGM Volume을 쓴다.")]
        [Range(0f, 1f)] public float volume = .2f;
        [Min(0f)] public float fadeOutSeconds = 1.5f;
        [Tooltip("야광귀 피버 곡 크기 (전투 BGM 크기에 곱한다). 구미호 피버는 LabConfig의 Gumiho Fever Bgm Volume을 쓴다.")]
        [Range(0f, 2f)] public float feverVolume = 1f;
        [Tooltip("피버가 시작될 때 전투 BGM에서 피버 곡으로 넘어가는 시간 (초)")]
        [Min(0f)] public float feverCrossfadeSeconds = .25f;
        [Tooltip("피버가 시작될 때 내는 추임새(콤보 성공과 같은 파일) 크기")]
        [Range(0f, 1f)] public float cueVolume = 1f;
        [Tooltip("#745 남은 30초부터 라운드 곡을 빠르게(음높이 그대로) 튼다")]
        public bool rushEnabled = true;
        [Tooltip("#745 평소 곡에서 빨라진 곡으로 넘어가는 시간 (초)")]
        [Min(0f)] public float rushCrossfadeSeconds = .4f;
        // 피버가 시작된 지 이 초 안에 알아챈 기기만 추임새를 낸다 (다시 접속한 기기가 뒤늦게 내지 않게).
        private const double CueWindowSeconds = 1d;

        private AudioSource source, feverSource, cueSource, rushSource;
        private AudioClip battleClip, gumihoClip, feverClip, gumihoFeverClip, yaguangClip, territoryClip, jigwiClip, jigwiFeverClip,
            tutorialClip, gumihoRushClip, yaguangRushClip, jigwiRushClip;
        private AudioClip[] cueClips;
        private AudioClip rushClip; // 이번 라운드 곡의 빨라진 판 (#745), 없으면 null
        private float rushBlend; // 0 = 평소 곡, 1 = 빨라진 곡
        private float rushPreviewUntil = float.NegativeInfinity;
        /// <summary>피버 시작 추임새를 낸 횟수 (테스트·확인용)</summary>
        public int FeverCueCount { get; private set; }
        private LabNetwork network;
        private string playingRound;
        private bool fading, feverOn;
        // 이번 라운드에 구미호 크기로 스테이지 곡(구미호 #351, 땅따먹기 #409)을 트는지, 지금 피버가 구미호 피버인지 (#351)
        private bool gumihoBgm, gumihoFever, yaguangBgm, jigwiBgm, jigwiFever, tutorialBgm;
        private float feverBlend; // 0 = 전투 BGM, 1 = 피버 곡
        private float toneSemitones; // 지금 전투 BGM 높이 (반음, 콤보 BIG/SMALL)

        public bool IsPlaying => source != null && source.isPlaying;
        /// <summary>피버 곡을 틀고 있는지 (#343)</summary>
        public bool FeverPlaying => feverSource != null && feverSource.isPlaying;
        public bool HasFeverClip => feverClip != null;
        /// <summary>지금 틀고 있는 전투 BGM과 피버 곡 (테스트·확인용, #351)</summary>
        public AudioClip BattleClip => source != null ? source.clip : null;
        public AudioClip FeverClip => feverSource != null ? feverSource.clip : null;
        /// <summary>지금 전투 BGM 높이 (반음, 테스트·확인용). BIG이면 +, SMALL이면 -</summary>
        public float ToneSemitones => toneSemitones;
        /// <summary>#745 빨라진 곡을 틀고 있는지 (테스트·확인용)</summary>
        public bool RushPlaying => rushSource != null && rushSource.isPlaying && rushBlend > 0f;

        // 기본 크기(LabConfig: Inspector·DEV, 구미호 전투 BGM은 Inspector) × 이 기기의 BGM 설정(일시정지 메뉴)
        private float BaseVolume => network == null || network.config == null ? volume
            : tutorialBgm ? network.config.TutorialBgmVolume
            : jigwiBgm ? network.config.JigwiBgmVolume
            : gumihoBgm ? network.config.GumihoBgmVolume : yaguangBgm ? network.config.YaguangBgmVolume : network.config.BgmVolume;
        private float TargetVolume => BaseVolume * LabAudioSettings.BgmVolume;
        private float FeverTargetVolume => (jigwiFever && network != null && network.config != null ? network.config.JigwiFeverBgmVolume
            : gumihoFever && network != null && network.config != null
            ? network.config.GumihoFeverBgmVolume
            : yaguangBgm && network != null && network.config != null ? network.config.YaguangFeverBgmVolume * feverVolume
            : BaseVolume * feverVolume) * LabAudioSettings.BgmVolume;

        // #629 라운드별 빠르기 (LabConfig Inspector). 땅따먹기·Lab 단판·예전 꽹과리 BGM은 원래 빠르기.
        private float BattleSpeed => network == null || network.config == null || territoryRound ? 1f
            : tutorialBgm ? network.config.TutorialBgmSpeed
            : jigwiBgm ? network.config.JigwiBgmSpeed : gumihoBgm ? network.config.GumihoBgmSpeed
            : yaguangBgm ? network.config.YaguangBgmSpeed : 1f;
        private float FeverSpeed => network == null || network.config == null ? 1f
            : jigwiFever ? network.config.JigwiFeverBgmSpeed : gumihoFever ? network.config.GumihoFeverBgmSpeed
            : yaguangBgm ? network.config.YaguangFeverBgmSpeed : 1f;
        private bool territoryRound;

        public void Bind(LabNetwork target) => network = target;

        private void Awake()
        {
            battleClip = FirstClip(ClipFolder);
            gumihoClip = FirstClip(GumihoClipFolder);
            feverClip = FirstClip(FeverClipFolder);
            gumihoFeverClip = FirstClip(GumihoFeverClipFolder);
            yaguangClip = FirstClip(YaguangClipFolder);
            territoryClip = FirstClip(TerritoryClipFolder);
            jigwiClip = FirstClip(JigwiClipFolder);
            jigwiFeverClip = FirstClip(JigwiFeverClipFolder);
            tutorialClip = FirstClip(TutorialClipFolder);
            gumihoRushClip = FirstClip(GumihoRushClipFolder);
            yaguangRushClip = FirstClip(YaguangRushClipFolder);
            jigwiRushClip = FirstClip(JigwiRushClipFolder);
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
            rushSource = gameObject.AddComponent<AudioSource>();
            rushSource.playOnAwake = false;
            rushSource.loop = true;
            rushSource.spatialBlend = 0f;
            rushSource.volume = 0f;
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
            bool jigwi = gumiho && jigwiFeverClip != null && network != null && network.GameStageYokai == LabStageYokai.Jigwi;
            AudioClip clip = jigwi ? jigwiFeverClip : gumiho && gumihoFeverClip != null ? gumihoFeverClip : feverClip;
            bool fever = snapshot.paused ? feverOn : elapsed >= 0d && clip != null;
            if (fever && !feverOn)
            {
                feverOn = true;
                gumihoFever = gumiho && !jigwi;
                jigwiFever = jigwi;
                feverSource.Stop();
                feverSource.clip = clip;
                feverSource.pitch = FeverSpeed;
                // At another speed the song has moved on that many times as far since the fever began.
                feverSource.time = Mathf.Clamp((float)elapsed * FeverSpeed, 0f, Mathf.Max(0f, clip.length - .05f));
                using (LabFirstUse.Measure(LabFirstUseKind.FeverMusic)) feverSource.Play();
                if (elapsed < CueWindowSeconds) PlayFeverCue();
            }
            else if (!fever && feverOn) StopFever();
            float step = feverCrossfadeSeconds <= 0f ? 1f : Time.unscaledDeltaTime / feverCrossfadeSeconds;
            feverBlend = Mathf.MoveTowards(feverBlend, feverOn ? 1f : 0f, step);
            source.volume = TargetVolume * (1f - feverBlend) * (1f - rushBlend);
            if (rushSource != null) rushSource.volume = TargetVolume * (1f - feverBlend) * rushBlend;
            feverSource.volume = FeverTargetVolume * feverBlend;
            if (!feverOn) return;
            feverSource.pitch = FeverSpeed;
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
            return FeverElapsedFor(snapshot, network.GameStageYokai, network.YaguangFeverElapsed(snapshot),
                network.BattlePresentationTime, out gumiho);
        }

        /// <summary>
        /// 피버 곡을 틀 시각 계산 (검사에서 스테이지를 정해 부를 수 있게 공개). stageFever는 야광귀가 아닌 피버(구미호·지귀)이면 true이고,
        /// 그 곡은 DriveFever가 스테이지를 보고 고른다.
        /// #629: 지귀 피버(#332, 방해가 끝나는 100초)는 feverTimeAt이 없어서 지귀 피버 곡(JigwiFever)이 한 번도 나오지 않았다.
        /// 이제 스테이지 피버(#441, LabNetwork.StageFeverElapsed)와 같은 시각에 튼다.
        /// </summary>
        public static double FeverElapsedFor(LabSnapshot snapshot, LabStageYokai stage, double yaguangElapsed, double now,
            out bool stageFever)
        {
            stageFever = false;
            if (snapshot == null) return -1d;
            if (yaguangElapsed >= 0d) return yaguangElapsed;
            if (snapshot.FeverTimeOn(now))
            {
                stageFever = true;
                return now - snapshot.feverTimeAt;
            }
            if (stage != LabStageYokai.Jigwi) return -1d;
            double jigwi = LabNetwork.StageFeverElapsed(snapshot, LabStageYokai.Jigwi, -1d);
            stageFever = jigwi >= 0d;
            return jigwi;
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
            jigwiFever = false;
            feverBlend = 0f;
            if (feverSource != null)
            {
                feverSource.Stop();
                feverSource.volume = 0f;
            }
        }

        /// <summary>#396: 야광귀 스테이지이고 Inspector에서 Yaguang Use New Bgm을 켰고 야광귀 곡 파일이 있으면 true.</summary>
        private bool WantsYaguangBgm() => yaguangClip != null && network != null && network.config != null
            && network.config.YaguangUseNewBgm && network.GameStageYokai == LabStageYokai.Yaguang;

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
                    // 새 라운드는 처음부터. 구미호 스테이지 (#351)는 구미호 전투 BGM, 땅따먹기 (#293)는 예전 구미호 곡 (#409).
                    playingRound = round;
                    fading = false;
                    StopFever();
                    SetTone(0f);
                    // #577: 라운드마다 자기 곡. 구미호 = 구미호 곡, 야광귀 = 야광귀 곡, 지귀 = 지귀 곡. 땅따먹기는 그대로.
                    AudioClip stageClip = snapshot.teamTerritory ? (territoryClip != null ? territoryClip : gumihoClip)
                        : network.GameStageYokai == LabStageYokai.Gumiho ? gumihoClip : null;
                    // #629: the practice board has its own song, whatever stage it borrows.
                    tutorialBgm = tutorialClip != null && network.IsTutorial;
                    if (tutorialBgm) stageClip = null;
                    gumihoBgm = stageClip != null;
                    territoryRound = snapshot.teamTerritory;
                    jigwiBgm = !tutorialBgm && !gumihoBgm && jigwiClip != null && network.GameStageYokai == LabStageYokai.Jigwi;
                    yaguangBgm = !tutorialBgm && !gumihoBgm && !jigwiBgm && WantsYaguangBgm();
                    source.Stop();
                    source.clip = tutorialBgm ? tutorialClip
                        : jigwiBgm ? jigwiClip : gumihoBgm ? stageClip : yaguangBgm ? yaguangClip : battleClip;
                    StopRush();
                    rushClip = RushClipFor(source.clip);
                    source.volume = TargetVolume;
                    source.time = 0f;
                    if (!snapshot.paused && source.clip != null)
                        using (LabFirstUse.Measure(LabFirstUseKind.BattleMusic)) source.Play();
                    return;
                }
                // 야광귀 새 BGM 후보 (#396): 전투 중에 Inspector 토글을 바꾸면 바로 다른 곡으로 처음부터 바꿔 들려준다.
                if (!tutorialBgm && !gumihoBgm && !jigwiBgm && WantsYaguangBgm() != yaguangBgm && !FeverPlaying)
                {
                    yaguangBgm = !yaguangBgm;
                    source.Stop();
                    source.clip = yaguangBgm ? yaguangClip : battleClip;
                    StopRush();
                    rushClip = RushClipFor(source.clip);
                    source.volume = TargetVolume;
                    source.time = 0f;
                    if (!snapshot.paused && source.clip != null) source.Play();
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
                if (!tutorialBgm)
                {
                    DriveRush(snapshot);
                    DriveFever(snapshot); // #629: the practice board keeps its own song to the end
                }
                if (!snapshot.paused) DriveTone(snapshot, network.BattlePresentationTime, Time.unscaledDeltaTime);
                return;
            }

            // 승리/패배: 서서히 끄기 (피버 곡 / 빨라진 곡도 함께)
            if (source.isPlaying || FeverPlaying || rushSource != null && rushSource.isPlaying)
            {
                fading = true;
                float step = fadeOutSeconds <= 0f ? 1f : TargetVolume * Time.unscaledDeltaTime / fadeOutSeconds;
                float feverStep = fadeOutSeconds <= 0f ? 1f : FeverTargetVolume * Time.unscaledDeltaTime / fadeOutSeconds;
                source.volume = Mathf.Max(0f, source.volume - step);
                if (feverSource != null) feverSource.volume = Mathf.Max(0f, feverSource.volume - feverStep);
                if (rushSource != null) rushSource.volume = Mathf.Max(0f, rushSource.volume - step);
                if (source.volume <= 0f && (feverSource == null || feverSource.volume <= 0f)
                    && (rushSource == null || rushSource.volume <= 0f)) StopNow();
            }
        }

        /// <summary>#745: 이번 라운드 곡의 빨라진 판. 구미호 / 야광귀 / 지귀 곡에만 있다.</summary>
        private AudioClip RushClipFor(AudioClip clip) =>
            clip == null ? null : clip == gumihoClip ? gumihoRushClip : clip == yaguangClip ? yaguangRushClip
            : clip == jigwiClip ? jigwiRushClip : null;

        /// <summary>
        /// #745: 빨라진 곡을 틀 때인지. 30초 알림이 붙는 판(LabThirtySecondsLeftHud.AppliesTo)에서 남은 시간이 알림 시각 이하가 된 뒤.
        /// 피버로 넘어가면 DriveFever가 빨라진 곡도 함께 줄인다.
        /// </summary>
        public static bool RushOn(bool applies, double remaining, float at) => applies && remaining <= at && remaining > 0d;

        /// <summary>#745: 평소 곡의 지금 자리(초)에 해당하는 빨라진 곡의 자리. 빨라진 곡은 speed배 짧다.</summary>
        public static float RushTimeFor(float sourceTime, float speed, float rushLength)
        {
            if (rushLength <= 0f || speed <= 0f) return 0f;
            return Mathf.Repeat(sourceTime / speed, rushLength);
        }

        /// <summary>
        /// VFX 실험실(#745): 남은 시간과 상관없이 seconds초 동안 빨라진 곡을 들려준다. 실제 빨라지기는 그대로 따로 된다.
        /// </summary>
        public bool PreviewRush(float seconds)
        {
            if (rushClip == null || source == null || !source.isPlaying || feverOn) return false;
            rushPreviewUntil = Time.unscaledTime + Mathf.Max(0f, seconds);
            return true;
        }

        /// <summary>#745: 빨라진 곡을 켜고 끄며 평소 곡과의 섞임(rushBlend)을 옮긴다. 크기는 DriveFever가 정한다.</summary>
        private void DriveRush(LabSnapshot snapshot)
        {
            if (rushSource == null) return;
            bool applies = network != null && LabThirtySecondsLeftHud.AppliesTo(network.GameFlowEnabled, network.IsTutorial,
                snapshot.teamTerritory, network.GameStageYokai);
            float at = network != null && network.config != null ? network.config.ThirtySecondsLeftAt : 30f;
            bool want = rushEnabled && rushClip != null && !feverOn
                && (RushOn(applies, snapshot.remaining, at) || Time.unscaledTime < rushPreviewUntil);
            if (want && !rushSource.isPlaying && rushBlend <= 0f && !snapshot.paused)
            {
                rushSource.clip = rushClip;
                rushSource.pitch = source.pitch;
                rushSource.time = RushTimeFor(source.time, RushSpeed, rushClip.length);
                rushSource.volume = 0f;
                rushSource.Play();
            }
            if (feverOn && feverBlend >= 1f) { StopRush(); return; }
            float step = rushCrossfadeSeconds <= 0f ? 1f : Time.unscaledDeltaTime / rushCrossfadeSeconds;
            if (!snapshot.paused) rushBlend = Mathf.MoveTowards(rushBlend, want || feverOn && rushSource.isPlaying ? 1f : 0f, step);
            if (snapshot.paused) { if (rushSource.isPlaying) rushSource.Pause(); }
            else if (rushBlend > 0f && !rushSource.isPlaying) rushSource.UnPause();
            if (rushBlend <= 0f && rushSource.isPlaying && !want) rushSource.Stop();
        }

        private void StopRush()
        {
            rushBlend = 0f;
            rushPreviewUntil = float.NegativeInfinity;
            if (rushSource == null) return;
            rushSource.Stop();
            rushSource.volume = 0f;
        }

        /// <summary>
        /// 콤보 결과로 전투 BGM 높이를 정한다. 목표 높이(ComboToneSemitones)까지 seconds초에 걸쳐 옮긴다. 검사에서 시각을 정해 부를 수 있게 공개한다.
        /// </summary>
        public void DriveTone(LabSnapshot snapshot, double now, float deltaTime)
        {
            LabConfig config = network != null ? network.config : null;
            float big = config != null ? config.YaguangComboBigBgmSemitones : 2f;
            float small = config != null ? config.YaguangComboSmallBgmSemitones : 2f;
            float seconds = config != null ? config.YaguangComboBgmToneSeconds : .3f;
            float target = ComboToneSemitones(snapshot, now, big, small);
            float range = Mathf.Max(Mathf.Abs(big), Mathf.Abs(small), Mathf.Abs(toneSemitones - target));
            float step = seconds <= 0f ? float.PositiveInfinity : range * Mathf.Max(0f, deltaTime) / seconds;
            SetTone(Mathf.MoveTowards(toneSemitones, target, step));
        }

        /// <summary>
        /// 콤보 결과에 맞는 전투 BGM 높이 (반음). 성공이면 BIG 배지가 떠 있는 동안 +big, 실패(틀린 색/시간 초과)면 SMALL 배지가
        /// 떠 있는 동안 -small, 그 밖에는 0. 배지는 결과 문구가 사라진 때(결과 + LabGourdComboHud.ResultSeconds)부터 효과가 가장 늦게
        /// 끝나는 때(LabYaguangSound.EffectUntil)까지 떠 있다. #664: 구미호 여우구슬 판정의 BIG/SMALL도 같다(둘 중 마지막 결과 기준).
        /// </summary>
        public static float ComboToneSemitones(LabSnapshot snapshot, double now, float big, float small)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing) return 0f;
            int effect = snapshot.LastEffectResult(out double at);
            if (effect == 0) return 0f;
            double start = at + LabGourdComboHud.ResultSeconds;
            double end = LabYaguangSound.EffectUntil(snapshot, effect, at);
            if (now < start || now >= end) return 0f;
            return effect > 0 ? Mathf.Clamp(big, 0f, 12f) : -Mathf.Clamp(small, 0f, 12f);
        }

        private void SetTone(float semitones)
        {
            toneSemitones = semitones;
            if (source != null) source.pitch = BattleSpeed * Mathf.Pow(2f, semitones / 12f);
            if (rushSource != null) rushSource.pitch = BattleSpeed * Mathf.Pow(2f, semitones / 12f);
        }

        private void StopNow()
        {
            if (source.isPlaying || fading) source.Stop();
            StopFever();
            StopRush();
            SetTone(0f);
            fading = false;
            source.volume = TargetVolume;
            if (network == null || network.Snapshot == null) playingRound = null;
        }
    }
}
