using System.Collections;
using C6Lab;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio
{
    /// <summary>
    /// 방에 들어가고 나올 때의 화면 전환 (#418). Home에서 자리 고르기(Lobby) 화면으로 넘어갈 때(방이 열린 순간)와, 자리 고르기
    /// 화면에서 뒤로 가기·방이 끝나 Home으로 돌아갈 때, 화면을 바로 검게 가리고 곧장 다음 씬을 불러온 뒤 새 화면이 그려지면
    /// 밝아진다. 떠나는 화면은 가려질 때까지 그대로 둔다(GameLobbyView가 HoldsSceneForFade를 본다. 먼저 사라지면 빈 화면이
    /// 비친다). Inspector에서 어두워지는 시간을 주면 그만큼 어두워진 뒤에 씬을 바꾼다. 가리는 동안에는 눌리지 않는다.
    /// 그사이 입장이 실패해 Home에 남으면 다시 밝아진다.
    /// </summary>
    public sealed partial class GameSession
    {
        [Header("방에 들어가고 나올 때 화면 전환 (#418)")]
        [Tooltip("Home과 자리 고르기 화면 사이를 오가기 전에 어두워지는 시간 (초). 0(기본)이면 넘어가는 순간 바로 가리고 씬을 바꾼다.")]
        [SerializeField, Min(0f)] private float lobbyFadeOutSeconds;
        [Tooltip("다음 화면이 뜬 뒤 밝아지는 시간 (초). 0이면 바로 걷힌다.")]
        [SerializeField, Min(0f)] private float lobbyFadeInSeconds = .35f;
        [SerializeField] private Color lobbyFadeColor = Color.black;
        [Header("마지막 일격 다시보기 뒤 결과로 넘어갈 때")]
        [Tooltip("다시보기 끝 0.5초 동안 덮는 막의 색. 결과 화면의 한지와 이어지게 밝은 색이다.")]
        [SerializeField] private Color replayCoverColor = new Color(.96f, .93f, .86f, 1f);
        [Tooltip("다시보기를 탭으로 건너뛴 뒤 막이 덮이는 시간 (초).")]
        [SerializeField, Min(.05f)] private float replaySkipCoverSeconds = .3f;
        public const string ReplayWaitingStatus = "다른 참가자를 기다리는 중…";
        private bool replayCovering;
        private float replayCoverSeconds = .5f;
        private float holdFadeOutSeconds;
        // 씬을 불러온 직후의 긴 프레임에 막이 한 번에 걷히지 않게, 밝아질 때 한 프레임에 쓰는 시간을 줄인다.
        private const float MaxFadeInStep = 1f / 30f;
        private GameSceneFade sceneFade;
        private Coroutine fadeInRoutine;
        private bool holdForFade;
        private string heldScenePath;

        public GameSceneFade SceneFade => sceneFade;
        /// <summary>이 씬의 화면이 가려지는 중이라 지금 모습 그대로 두어야 하는지.</summary>
        public bool HoldsSceneForFade(string scenePath) => holdForFade && scenePath == heldScenePath;

        /// <summary>Home ↔ Lobby 사이를 오가는지. 그때만 막을 씌운다 (전투·결과 전환은 그대로).</summary>
        private bool FadesBetweenHomeAndLobby(string path)
        {
            string active = SceneManager.GetActiveScene().path;
            return sceneFade != null && (path == GameScenePaths.Lobby && active == GameScenePaths.Home
                || path == GameScenePaths.Home && active == GameScenePaths.Lobby);
        }

        /// <summary>다시보기가 막을 덮기 시작한 전투에서 결과·게임 오버로 가는지. 그때는 막 아래에서 씬을 바꾼다.</summary>
        private bool ReplayCoversSceneChange(string path) => sceneFade != null && replayCovering
            && SceneManager.GetActiveScene().path == GameScenePaths.Battle
            && (path == GameScenePaths.StageResult || path == GameScenePaths.GameOver);

        /// <summary>이 프레임부터 떠나는 화면을 그대로 두고, 가려진 뒤에 씬을 바꾸게 한다.</summary>
        private void HoldForFade(string path)
        {
            bool lobby = FadesBetweenHomeAndLobby(path);
            bool replay = !lobby && ReplayCoversSceneChange(path);
            holdForFade = lobby || replay;
            holdFadeOutSeconds = replay ? replayCoverSeconds : lobbyFadeOutSeconds;
            if (lobby) sceneFade.SetColor(lobbyFadeColor);
            heldScenePath = holdForFade ? SceneManager.GetActiveScene().path : null;
        }

        /// <summary>
        /// 마지막 일격 다시보기: 남은 0.5초(전환)에 들어가거나 이 기기에서 건너뛰면 막을 한지색으로 덮는다. 다 덮였는데 아직
        /// 결과로 넘어가지 않았으면(다른 참가자가 보는 중) 기다림 문구를 띄운다. 씬을 바꾸기 시작하면 FadeOutOfScene이 이어받는다.
        /// </summary>
        private void UpdateReplayCover()
        {
            if (sceneFade == null || holdForFade || quitting || finishReplay == null || replayShot == null
                || SceneManager.GetActiveScene().path != GameScenePaths.Battle) return;
            bool cover = finishReplay.Skipped || finishReplay.Complete
                || finishReplay.Running && finishReplay.Remaining <= LabFinishReplayTimeline.TransitionSeconds;
            if (!cover) return;
            if (!replayCovering)
            {
                replayCovering = true;
                replayCoverSeconds = finishReplay.Skipped ? replaySkipCoverSeconds
                    : Mathf.Max(.05f, finishReplay.Remaining);
                if (fadeInRoutine != null) { StopCoroutine(fadeInRoutine); fadeInRoutine = null; }
                sceneFade.SetColor(replayCoverColor);
            }
            float alpha = Mathf.Min(1f, sceneFade.Alpha + Time.unscaledDeltaTime / replayCoverSeconds);
            sceneFade.Set(alpha, true);
            bool waiting = alpha >= 1f && !finishReleased && !network.IsDeveloperSolo;
            sceneFade.SetStatus(waiting ? ReplayWaitingStatus : null,
                finishReplay.Overlay != null ? LabGuideText.FontOf(finishReplay.Overlay) : null);
        }

        /// <summary>떠나는 화면을 그대로 둔 채 어두워진다. 그사이 지금 씬에 머물게 되면(입장 실패·취소) 멈춘다.</summary>
        private IEnumerator FadeOutOfScene()
        {
            if (fadeInRoutine != null) { StopCoroutine(fadeInRoutine); fadeInRoutine = null; }
            float alpha = sceneFade.Alpha;
            while (alpha < 1f && !quitting && wantedScene != SceneManager.GetActiveScene().path)
            {
                alpha = holdFadeOutSeconds <= 0f ? 1f : alpha + Time.unscaledDeltaTime / holdFadeOutSeconds;
                sceneFade.Set(alpha, true);
                if (alpha < 1f) yield return null;
            }
        }

        /// <summary>새 화면(또는 입장이 실패해 남은 Home)을 보여 주며 밝아진다. 밝아지는 동안에는 눌림을 막지 않는다.</summary>
        private void FadeIntoScene()
        {
            holdForFade = false;
            heldScenePath = null;
            replayCovering = false;
            if (sceneFade != null) sceneFade.SetStatus(null);
            if (sceneFade == null || !sceneFade.Visible || quitting) return;
            if (fadeInRoutine != null) StopCoroutine(fadeInRoutine);
            fadeInRoutine = StartCoroutine(FadeIn());
        }

        private IEnumerator FadeIn()
        {
            float alpha = sceneFade.Alpha;
            while (alpha > 0f)
            {
                alpha = lobbyFadeInSeconds <= 0f ? 0f
                    : alpha - Mathf.Min(Time.unscaledDeltaTime, MaxFadeInStep) / lobbyFadeInSeconds;
                sceneFade.Set(alpha, false);
                if (alpha > 0f) yield return null;
            }
            fadeInRoutine = null;
        }
    }
}
