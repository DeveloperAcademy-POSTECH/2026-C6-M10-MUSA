using C6Lab;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio
{
    /// <summary>
    /// 땅따먹기 경기 종료 (#717). 땅따먹기(1:1·2:2·3:3)가 끝나면(시간이 다 됐든 한 팀이 박을 다 칠했든) 이 기기가 그 끝을 본 순간
    /// 화면 가운데에 "경기 종료!"(진홍 글자, 크림색 가장자리, GameStartOverlay.ShowFinished)를 띄우고 호루라기를 한 번 분다.
    /// 그때부터 결과 화면이 열릴 때까지 전투 화면은 눌리지 않는다(덮개가 버튼을 막고, 조합판 손짓도 막는다). 결과 화면은 끝을 본 뒤
    /// territoryEndHoldSeconds(기본 1.8초, 호루라기 길이)가 지나야 열리고, 결과 준비(박 그림 복사)는 그 사이에 덮개 아래에서 한다.
    /// 스토리 스테이지의 끝(마지막 일격 다시보기, 게임 오버)은 그대로다.
    /// 예전에는 결과로 넘어가기 전에 "다 칠한 박" 그림을 기다렸는데, 시간이 다 되어 끝난 땅따먹기 박은 다 칠해지지 않아 그 기다림이
    /// 결과 준비 제한 시간까지 이어졌다(전투 화면이 멈춘 것처럼 보이던 원인). 이제 다 칠하지 않은 땅따먹기 결과는 그 그림을 기다리지
    /// 않고(ResultNeedsPreview), 다 칠해 끝났어도 그 그림은 "경기 종료!" 뒤 territoryPreviewGraceSeconds(기본 1초)까지만 기다린다.
    /// 결과로 넘어가는 데 걸린 시간은 콘솔의 HAPIO_RESULT_TRANSITION 한 줄로 남는다.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>호루라기 소리 (Resources). ElevenLabs Sound Effects로 만든 소리, 출처는 docs/AUDIO_CREDITS.md.</summary>
        public const string TerritoryWhistlePath = "Hapio/Audio/TerritoryWhistle";

        [Header("땅따먹기 경기 종료 (#717)")]
        [Tooltip("땅따먹기가 끝난 순간부터 \"경기 종료!\"를 보여 주는 시간 (초). 결과 화면은 그 뒤에 열린다. 호루라기 길이에 맞췄다.")]
        [SerializeField, Min(0f)] private float territoryEndHoldSeconds = 1.8f;
        [Tooltip("호루라기 소리 크기 (× 일시정지 메뉴 효과음 설정)")]
        [SerializeField, Range(0f, 1f)] private float territoryWhistleVolume = 1f;
        [Tooltip("박을 다 칠해 끝났을 때 다 칠한 박 그림(박 확인)이 이 기기에 도착하길 \"경기 종료!\" 뒤로 더 기다리는 시간 (초). " +
            "그때까지 오지 않으면 그 그림 없이 결과를 연다.")]
        [SerializeField, Min(0f)] private float territoryPreviewGraceSeconds = 1f;

        private double territoryEndAt = -1d;
        private string territoryEndSession;
        private uint territoryEndRound;
        private AudioSource territoryWhistle;
        private AudioClip territoryWhistleClip;
        private bool territoryWhistleLoaded;

        /// <summary>이 기기가 지금 땅따먹기 판의 끝을 보았다 ("경기 종료!"를 띄웠다).</summary>
        public bool TerritoryEndShown => territoryEndAt >= 0d && !string.IsNullOrEmpty(territoryEndSession)
            && territoryEndSession == state.battleSessionId && territoryEndRound == state.battleRound;
        /// <summary>호루라기를 분 횟수 (검사·확인용). 소리 파일이 없어도 센다.</summary>
        public int TerritoryWhistleCount { get; private set; }
        public float TerritoryEndHoldSeconds => territoryEndHoldSeconds;

        /// <summary>The territory battle is over on this device: the Host's snapshot says so, or the flow has already moved to its result.</summary>
        private bool TerritoryBattleEnded => IsTerritory && !string.IsNullOrEmpty(state.battleSessionId)
            && (state.phase == GameFlowPhase.Completed
                || state.phase == GameFlowPhase.Playing && MatchesBattle(network.Snapshot)
                    && network.Snapshot.phase == LabPhase.Victory);

        /// <summary>"경기 종료!" covers the battle screen until the result Scene replaces it.</summary>
        private bool TerritoryEndCovering => TerritoryEndShown
            && SceneManager.GetActiveScene().path == GameScenePaths.Battle;

        /// <summary>The result waits until the whistle has had its time.</summary>
        private bool TerritoryEndHolding(double realNow) => TerritoryEndShown
            && realNow < territoryEndAt + territoryEndHoldSeconds;

        private void UpdateTerritoryEnd(double realNow)
        {
            if (!IsTerritory) return;
            // Ready long before the end, so the whistle never waits on loading at the very moment it is needed.
            LoadTerritoryWhistle();
            if (TerritoryEndShown || !TerritoryBattleEnded
                || SceneManager.GetActiveScene().path != GameScenePaths.Battle) return;
            BeginTerritoryEnd(realNow);
        }

        private void BeginTerritoryEnd(double realNow)
        {
            if (TerritoryEndShown) return;
            territoryEndAt = realNow;
            territoryEndSession = state.battleSessionId;
            territoryEndRound = state.battleRound;
            // The overlay stops the battle UI's buttons; this stops a drag, throw or pass already under a finger.
            if (network.board != null) network.board.SetUiInputBlocked(true);
            PlayTerritoryWhistle();
            startOverlay?.ShowFinished();
        }

        private void LoadTerritoryWhistle()
        {
            if (territoryWhistleLoaded) return;
            territoryWhistleLoaded = true;
            territoryWhistleClip = Resources.Load<AudioClip>(TerritoryWhistlePath);
        }

        private void PlayTerritoryWhistle()
        {
            TerritoryWhistleCount++;
            LoadTerritoryWhistle();
            if (territoryWhistleClip == null) return;
            if (territoryWhistle == null)
            {
                // On the session itself, so the Scene change to the result never cuts it short.
                territoryWhistle = gameObject.AddComponent<AudioSource>();
                territoryWhistle.playOnAwake = false;
                territoryWhistle.spatialBlend = 0f;
            }
            territoryWhistle.PlayOneShot(territoryWhistleClip, territoryWhistleVolume * LabAudioSettings.SfxVolume);
        }

        /// <summary>
        /// A territory result waits for its completed-gourd preview only while "경기 종료!" is up and a short grace after it;
        /// it never sits on the frozen battle for the whole loading budget.
        /// </summary>
        private double TerritoryResultDeadline(double realNow) => (TerritoryEndShown ? territoryEndAt : realNow)
            + territoryEndHoldSeconds + territoryPreviewGraceSeconds;

        /// <summary>
        /// The completed-gourd preview (박 확인) needs a completely painted map. A territory battle usually ends at its time limit with
        /// part of the gourd unpainted (#717); that result has no completed map to wait for, so it opens without one. A sweep still
        /// gets its preview as before.
        /// </summary>
        private bool ResultNeedsPreview(LabSnapshot snapshot)
        {
            if (network.config == null || !network.config.CompletedGourdViewerEnabled
                || network.config.GourdMode != LabGourdMode.Paint) return false;
            bool territory = IsTerritory || snapshot != null && snapshot.teamTerritory;
            return !territory || snapshot != null && snapshot.paintCellCount > 0
                && snapshot.paintCoveredCount >= snapshot.paintCellCount;
        }
    }
}
