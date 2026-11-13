using C6Lab;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio
{
    /// <summary>
    /// 전투의 끝 (#717). 판이 끝난 것을 이 기기가 본 순간 화면 가운데에 끝 문구(진홍 글자, 크림색 가장자리)를 띄우고 호루라기를 한 번
    /// 분다. 그 문구가 떠 있는 동안 전투 화면은 눌리지 않는다(덮개가 버튼을 막고, 조합판 손짓도 막는다). 연습판(튜토리얼)은 그대로다.
    /// - 땅따먹기(1:1·2:2·3:3): 시간이 다 됐든 한 팀이 박을 다 칠했든 "경기 종료!". 결과 화면은 끝을 본 뒤 battleEndHoldSeconds
    ///   (기본 1.8초, 호루라기 길이)가 지나야 열리고, 결과 준비(박 그림 복사)는 그 사이에 덮개 아래에서 한다.
    /// - 스토리 스테이지(1~3), 시간 안에 다 칠함: "봉인 성공!"(진홍). 막타 연출(마지막 일격 다시보기 또는 1.2초 고정 연출)이 이
    ///   기기에서 시작될 때 사라진다. 방장은 막타 연출의 공동 시작 시각을 끝에서 battleEndHoldSeconds 뒤보다 이르게 잡지 않는다
    ///   (FinishScheduleAllowed). 예전에 막타 연출을 준비하며 비던 시간을 이 문구가 채운다.
    /// - 스토리 스테이지(1~3), 다 칠하지 못하고 시간이 끝남: "시간 초과!"(짙은 청색). 막타 연출 없이, 끝을 본 뒤 battleEndHoldSeconds가
    ///   지나면 바로 실패 화면(게임 오버)으로 간다. 사람이 나가서 끝난 게임 오버 등 다른 끝에는 문구가 없다.
    /// 시작 쪽은 GameSession.Start: 3·2·1 뒤 스토리는 "봉인 시작!", 땅따먹기는 "시작!".
    /// 예전 땅따먹기는 결과로 넘어가기 전에 "다 칠한 박" 그림을 기다렸는데, 시간이 다 되어 끝난 박은 다 칠해지지 않아 그 기다림이
    /// 결과 준비 제한 시간까지 이어졌다(전투 화면이 멈춘 것처럼 보이던 원인). 이제 다 칠하지 않은 땅따먹기 결과는 그 그림을 기다리지
    /// 않고(ResultNeedsPreview), 다 칠해 끝났어도 그 그림은 "경기 종료!" 뒤 territoryPreviewGraceSeconds(기본 1초)까지만 기다린다.
    /// 결과로 넘어가는 데 걸린 시간은 콘솔의 HAPIO_RESULT_TRANSITION 한 줄로 남는다.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>끝 호루라기 (Resources). ElevenLabs Sound Effects로 만든 소리, 출처는 docs/AUDIO_CREDITS.md.</summary>
        public const string EndWhistlePath = "Hapio/Audio/TerritoryWhistle";

        [Header("전투 끝 문구와 호루라기 (#717)")]
        [Tooltip("판이 끝난 순간부터 끝 문구(\"경기 종료!\"·\"봉인 성공!\"·\"시간 초과!\")를 보여 주는 최소 시간 (초). 땅따먹기 결과 화면, 스토리 막타 연출과 실패 화면은 " +
            "그 뒤에 시작한다. 호루라기 길이에 맞췄다.")]
        [SerializeField, Min(0f)] private float battleEndHoldSeconds = 1.8f;
        [Tooltip("호루라기 소리 크기 (× 일시정지 메뉴 효과음 설정)")]
        [SerializeField, Range(0f, 1f)] private float endWhistleVolume = 1f;
        [Tooltip("땅따먹기에서 박을 다 칠해 끝났을 때 다 칠한 박 그림(박 확인)이 이 기기에 도착하길 \"경기 종료!\" 뒤로 더 기다리는 " +
            "시간 (초). 그때까지 오지 않으면 그 그림 없이 결과를 연다.")]
        [SerializeField, Min(0f)] private float territoryPreviewGraceSeconds = 1f;

        /// <summary>The Host's reason for a story defeat at the time limit (GameSession.Update).</summary>
        internal const string TimeRanOutReason = "Time ran out.";

        private enum BattleEndKind { None, TerritoryOver, SealSucceeded, TimeUp }

        private double battleEndAt = -1d;
        private BattleEndKind battleEndKind;
        private string battleEndSession;
        private uint battleEndRound;
        private AudioSource endWhistle;
        private AudioClip endWhistleClip;
        private bool endWhistleLoaded;

        /// <summary>이 기기가 지금 판의 끝을 보았다 (끝 문구를 띄웠다).</summary>
        public bool BattleEndShown => battleEndAt >= 0d && !string.IsNullOrEmpty(battleEndSession)
            && battleEndSession == state.battleSessionId && battleEndRound == state.battleRound;
        /// <summary>호루라기를 분 횟수 (검사·확인용). 소리 파일이 없어도 센다.</summary>
        public int EndWhistleCount { get; private set; }
        public float BattleEndHoldSeconds => battleEndHoldSeconds;

        /// <summary>The territory battle is over on this device: the Host's snapshot says so, or the flow has already moved to its result.</summary>
        private bool TerritoryBattleEnded => IsTerritory && !string.IsNullOrEmpty(state.battleSessionId)
            && (state.phase == GameFlowPhase.Completed
                || state.phase == GameFlowPhase.Playing && MatchesBattle(network.Snapshot)
                    && network.Snapshot.phase == LabPhase.Victory);

        /// <summary>A story stage (not the practice board) has just been painted in full on this device.</summary>
        private bool StoryBattleWon => state.mode == GameMode.Story && !tutorialRun
            && !string.IsNullOrEmpty(state.battleSessionId) && state.phase == GameFlowPhase.Playing
            && MatchesBattle(network.Snapshot) && network.Snapshot.phase == LabPhase.Victory;

        /// <summary>
        /// A story stage (not the practice board) ran out of time before its gourd was painted in full: the Host's snapshot says so,
        /// or the flow has already moved to its failure.
        /// </summary>
        private bool StoryTimedOut => state.mode == GameMode.Story && !tutorialRun && !string.IsNullOrEmpty(state.battleSessionId)
            && (state.phase == GameFlowPhase.Playing && MatchesBattle(network.Snapshot)
                    && network.Snapshot.phase == LabPhase.Defeat && network.Snapshot.remaining <= 0d
                || state.phase == GameFlowPhase.GameOver && state.retryableDefeat && state.endReason == TimeRanOutReason);

        /// <summary>This device's final-hit replay or fixed finish has begun (or the finish is over).</summary>
        private bool FinishEffectStarted => replayCovering || finishReleased
            || finishReplay != null && (finishReplay.Running || finishReplay.Complete)
            || finishPresentation != null && (finishPresentation.Running || finishPresentation.Complete);

        /// <summary>
        /// The end words cover the battle screen: "경기 종료!" until the result Scene replaces it, "시간 초과!" until the failure
        /// Scene replaces it, "봉인 성공!" until the final-hit effect starts on this device.
        /// </summary>
        private bool BattleEndCovering => BattleEndShown && SceneManager.GetActiveScene().path == GameScenePaths.Battle
            && (battleEndKind != BattleEndKind.SealSucceeded || !FinishEffectStarted);

        /// <summary>The territory result or the time-up failure waits until the whistle has had its time.</summary>
        private bool EndWordsHolding(double realNow) => BattleEndShown && battleEndKind != BattleEndKind.SealSucceeded
            && realNow < battleEndAt + battleEndHoldSeconds;

        private void UpdateBattleEnd(double realNow)
        {
            if (tutorialRun || !IsTerritory && state.mode != GameMode.Story) return;
            // Ready long before the end, so the whistle never waits on loading at the very moment it is needed.
            LoadEndWhistle();
            if (BattleEndShown || SceneManager.GetActiveScene().path != GameScenePaths.Battle) return;
            if (IsTerritory) { if (TerritoryBattleEnded) BeginBattleEnd(realNow, BattleEndKind.TerritoryOver); }
            else if (StoryBattleWon) BeginBattleEnd(realNow, BattleEndKind.SealSucceeded);
            else if (StoryTimedOut) BeginBattleEnd(realNow, BattleEndKind.TimeUp);
        }

        private void BeginBattleEnd(double realNow, BattleEndKind kind)
        {
            if (BattleEndShown) return;
            battleEndAt = realNow;
            battleEndKind = kind;
            battleEndSession = state.battleSessionId;
            battleEndRound = state.battleRound;
            // The overlay stops the battle UI's buttons; this stops a drag, throw or pass already under a finger.
            if (network.board != null) network.board.SetUiInputBlocked(true);
            PlayEndWhistle();
            ShowBattleEnd();
        }

        private void ShowBattleEnd()
        {
            if (startOverlay == null) return;
            if (battleEndKind == BattleEndKind.TerritoryOver) startOverlay.ShowFinished();
            else if (battleEndKind == BattleEndKind.TimeUp) startOverlay.ShowTimeUp();
            else startOverlay.ShowSealSucceeded();
        }

        /// <summary>
        /// Host, story: the shared start of the final-hit effect is not reserved before "봉인 성공!" has had its time. A shared
        /// reservation starts its lead (GamePaintFinishBarrier.LeadSeconds) later, so it may be made that much earlier.
        /// Without "봉인 성공!" (the practice board) nothing waits.
        /// </summary>
        private bool FinishScheduleAllowed(double realNow, bool singleLocal)
        {
            if (!BattleEndShown || battleEndKind != BattleEndKind.SealSucceeded) return true;
            double lead = singleLocal ? 0d : GamePaintFinishBarrier.LeadSeconds;
            return realNow >= battleEndAt + battleEndHoldSeconds - lead;
        }

        private void LoadEndWhistle()
        {
            if (endWhistleLoaded) return;
            endWhistleLoaded = true;
            endWhistleClip = Resources.Load<AudioClip>(EndWhistlePath);
        }

        private void PlayEndWhistle()
        {
            EndWhistleCount++;
            LoadEndWhistle();
            if (endWhistleClip == null) return;
            if (endWhistle == null)
            {
                // On the session itself, so the Scene change to the result never cuts it short.
                endWhistle = gameObject.AddComponent<AudioSource>();
                endWhistle.playOnAwake = false;
                endWhistle.spatialBlend = 0f;
            }
            endWhistle.PlayOneShot(endWhistleClip, endWhistleVolume * LabAudioSettings.SfxVolume);
        }

        /// <summary>
        /// A territory result waits for its completed-gourd preview only while "경기 종료!" is up and a short grace after it;
        /// it never sits on the frozen battle for the whole loading budget.
        /// </summary>
        private double TerritoryResultDeadline(double realNow) => (BattleEndShown ? battleEndAt : realNow)
            + battleEndHoldSeconds + territoryPreviewGraceSeconds;

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
