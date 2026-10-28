using UnityEngine;

namespace C6Lab
{
    /// <summary>구미호 스테이지 효과음의 종류 (#351). 숫자는 폴더·크기 순서다.</summary>
    public enum LabGumihoSfx { CharmCast, CharmCheck, CharmRelease, FoxFall, FoxClear, BombOpen }

    /// <summary>
    /// 구미호 스테이지 효과음 (#351). 소리마다 Resources/Audio 아래 자기 폴더의 파일을 쓰고(여러 개면 무작위), 폴더가 비어
    /// 있으면 그 소리만 조용히 넘어간다. 크기는 LabConfig Inspector의 소리별 크기 × 일시정지 메뉴 효과음 설정.
    /// - 매혹이 걸릴 때 (Audio/CharmCast): 예고(3초)가 끝나 하트가 뜨는 순간, 모든 기기. 걸린 지 1초가 지나 알아챈
    ///   기기(다시 접속)는 내지 않는다.
    /// - 내 몫을 다 눌러 체크가 뜰 때 (Audio/CharmCheck): 누른 사람 기기만, 매혹 한 번에 한 번 (LabCharmBubbles.OwnCheckShown)
    /// - 매혹이 풀릴 때 (Audio/CharmRelease): 걸린 사람(LabCharmHud.CharmReleased)과 양옆 사람(LabCharmBubbles.HelperCharmEnded)
    ///   기기. 양옆이 다 눌러 풀었든, 시간이 다 되었든, 피버타임이 시작되었든 같다. 라운드가 끝나 사라지는 것은 내지 않는다.
    /// - 여우 구슬이 떨어질 때 (Audio/FoxFall): 여우 구슬을 받은 판 주인 기기만, 한 번 떨어뜨릴 때마다 한 번 (SabotageCast)
    /// - 여우 구슬이 사라질 때 (Audio/FoxClear): 두 번 눌러 없앴거나 음·양 구슬과 닿아 함께 사라질 때, 그 판 주인 기기만
    ///   (LabOrbBoard.FoxFireCleared). 수명이 다하거나 피버타임 시작으로 사라질 때는 내지 않는다.
    /// - 해금 폭탄이 공격 구슬 2개로 바뀔 때 (Audio/BombOpen): 멈춘 폭탄이 연기로 사라지는 순간, 그 판 주인 기기만
    ///   (LabOrbBoard.BombOpening)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LabGumihoSound : MonoBehaviour
    {
        public const string CharmCastFolder = "Audio/CharmCast";
        public const string CharmCheckFolder = "Audio/CharmCheck";
        public const string CharmReleaseFolder = "Audio/CharmRelease";
        public const string FoxFallFolder = "Audio/FoxFall";
        public const string FoxClearFolder = "Audio/FoxClear";
        public const string BombOpenFolder = "Audio/BombOpen";
        private static readonly string[] Folders =
            { CharmCastFolder, CharmCheckFolder, CharmReleaseFolder, FoxFallFolder, FoxClearFolder, BombOpenFolder };
        // 매혹이 걸린 지 이 초 안에 알아챈 기기만 소리를 낸다.
        private const double LateSeconds = 1d;

        private AudioClip[][] clips;
        private readonly int[] counts = new int[Folders.Length];
        private AudioSource source;
        private LabNetwork network;
        private LabOrbBoard board;
        private LabCharmHud charmHud;
        private LabCharmBubbles charmBubbles;
        private bool subscribed;
        // 걸림 소리를 낸 마지막 매혹 (그 매혹이 걸린 Host 시각)
        private double castCharm = double.NaN;
        private double seenServerTime = -1d;
        private float seenAt;

        public static string Folder(LabGumihoSfx sfx) => Folders[(int)sfx];
        public int ClipCount(LabGumihoSfx sfx) => clips == null ? 0 : clips[(int)sfx].Length;
        /// <summary>이 소리를 낼 순간을 알아챈 횟수 (테스트·확인용). 소리 파일이 없어도 센다.</summary>
        public int PlayCount(LabGumihoSfx sfx) => counts[(int)sfx];

        /// <summary>스냅샷·방해 알림을 읽을 네트워크, 여우 구슬·폭탄을 보여 주는 내 판, 매혹 화면 둘을 연결한다.</summary>
        public void Bind(LabNetwork target, LabOrbBoard orbBoard, LabCharmHud hud, LabCharmBubbles bubbles)
        {
            Unsubscribe();
            network = target;
            board = orbBoard;
            charmHud = hud;
            charmBubbles = bubbles;
            if (isActiveAndEnabled) Subscribe();
        }

        private void Awake()
        {
            clips = new AudioClip[Folders.Length][];
            for (int i = 0; i < Folders.Length; i++) clips[i] = Resources.LoadAll<AudioClip>(Folders[i]);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 화면 기준 소리
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;
            if (network != null) network.SabotageCast += OnSabotageCast;
            if (board != null)
            {
                board.FoxFireCleared += OnFoxFireCleared;
                board.BombOpening += OnBombOpening;
            }
            if (charmHud != null) charmHud.CharmReleased += OnCharmReleased;
            if (charmBubbles != null)
            {
                charmBubbles.OwnCheckShown += OnOwnCheckShown;
                charmBubbles.HelperCharmEnded += OnCharmReleased;
            }
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            subscribed = false;
            if (network != null) network.SabotageCast -= OnSabotageCast;
            if (board != null)
            {
                board.FoxFireCleared -= OnFoxFireCleared;
                board.BombOpening -= OnBombOpening;
            }
            if (charmHud != null) charmHud.CharmReleased -= OnCharmReleased;
            if (charmBubbles != null)
            {
                charmBubbles.OwnCheckShown -= OnOwnCheckShown;
                charmBubbles.HelperCharmEnded -= OnCharmReleased;
            }
        }

        private void Update()
        {
            LabSnapshot snapshot = network != null ? network.Snapshot : null;
            if (snapshot == null || snapshot.phase != LabPhase.Playing || snapshot.paused) return;
            if (ObserveCharm(snapshot, ServerNow(snapshot))) Play(LabGumihoSfx.CharmCast);
        }

        /// <summary>
        /// 이 Host 시각에 매혹이 막 걸렸으면 true (매혹 한 번에 한 번). 예고 동안과 풀린 뒤에는 false이고, 걸린 지 1초가 지나
        /// 처음 본 매혹도 false다.
        /// </summary>
        public bool ObserveCharm(LabSnapshot snapshot, double now)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Playing) return false;
            if (!(snapshot.charmUntil > now && now >= snapshot.charmStartedAt) || castCharm.Equals(snapshot.charmStartedAt))
                return false;
            castCharm = snapshot.charmStartedAt;
            return now - snapshot.charmStartedAt <= LateSeconds;
        }

        // 여우 구슬은 받은 판 주인 기기에서만 (모든 기기가 알림을 받는다)
        private void OnSabotageCast(LabSabotageKind kind, ulong target)
        {
            if (kind == LabSabotageKind.FoxFire && network != null && target == network.LocalPlayerId) Play(LabGumihoSfx.FoxFall);
        }

        private void OnFoxFireCleared(string id) => Play(LabGumihoSfx.FoxClear);
        private void OnBombOpening(string id) => Play(LabGumihoSfx.BombOpen);
        private void OnOwnCheckShown() => Play(LabGumihoSfx.CharmCheck);
        private void OnCharmReleased() => Play(LabGumihoSfx.CharmRelease);

        /// <summary>이 소리를 한 번 낸다. 소리를 냈으면 true (파일이 없으면 세기만 하고 false).</summary>
        public bool Play(LabGumihoSfx sfx)
        {
            counts[(int)sfx]++;
            AudioClip[] set = clips != null ? clips[(int)sfx] : null;
            if (set == null || set.Length == 0 || source == null) return false;
            source.PlayOneShot(set[Random.Range(0, set.Length)], Volume(sfx) * LabAudioSettings.SfxVolume);
            return true;
        }

        private float Volume(LabGumihoSfx sfx)
        {
            LabConfig config = network != null ? network.config : null;
            if (config == null) return 1f;
            switch (sfx)
            {
                case LabGumihoSfx.CharmCast: return config.CharmCastSoundVolume;
                case LabGumihoSfx.CharmCheck: return config.CharmCheckSoundVolume;
                case LabGumihoSfx.CharmRelease: return config.CharmReleaseSoundVolume;
                case LabGumihoSfx.FoxFall: return config.FoxFallSoundVolume;
                case LabGumihoSfx.FoxClear: return config.FoxClearSoundVolume;
                default: return config.BombOpenSoundVolume;
            }
        }

        // 스냅샷 사이에도 Host 시각을 이어서 어림한다 (LabCharmHud와 같은 방식, 최대 1초).
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
    }
}
