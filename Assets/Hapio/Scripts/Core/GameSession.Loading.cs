using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio
{
    /// <summary>
    /// 공용 로딩 화면 (#478). 판(인게임) 밖에서 화면을 바꾸느라 기다려야 하는 순간에 같은 로딩 화면을 씌운다:
    /// 타이틀·방 목록 ↔ 자리 고르기, 자리 고르기 → 첫 전투, 튜토리얼 → 방 목록·타이틀, 결과·게임오버 → 타이틀·방,
    /// 튜토리얼 RESTART.
    /// 씌우지 않는 곳(사용자 결정): 앱을 켜서 첫 화면이 뜰 때(바로 메인 화면), 오프닝 → 튜토리얼(나중에 따로 만든다),
    /// 판 안에서 전투를 떠나는 전환(전투 → 스테이지 결과·게임오버). 전투로 들어가는 길은 모두 씌운다(#607: 다음 스테이지·재도전·DEV 다음 스테이지).
    /// 막대는 씬 불러오기로 60%까지 차고, 그다음 준비로 나머지가 찬다:
    /// Home·자리 고르기는 화면이 다 그려지면, 첫 전투는 3·2·1이 시작되면(그동안 "준비 n/m"), 튜토리얼은 첫 대사를 띄울 수 있게
    /// 되면 100%다. 아무리 짧아도 1초는 보이고(0→100%가 한 번 차 보임), 100%를 잠깐 보여 준 뒤 살짝 사라진다.
    /// 화면 전환 막(#418)·시작 가림막은 그대로 돌고, 로딩 화면이 그 위에 그려진다.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>How much of the bar loading the scene itself fills; getting it ready fills the rest.</summary>
        public const float LoadingSceneShare = .6f;
        /// <summary>The ready count fills the bar up to here; the countdown starting makes it 100%.</summary>
        public const float LoadingReadyShare = .95f;
        /// <summary>A first battle's loading screen starts closing this long before its "3".</summary>
        public const double LoadingCloseLeadSeconds = .5d;

        private GameLoadingView loadingView;
        private GameLoadingArt loadingArt;
        private bool loadingActive, loadingReady;
        private float loadingShownAt, loadingFullAt = -1f;
        private int loadingReadyFrame = -1;
        private string loadingFor;

        public GameLoadingView LoadingView => loadingView;
        /// <summary>The shared loading screen is up, covering everything until the next screen is ready.</summary>
        public bool IsLoadingScreenUp => loadingView != null && loadingView.Visible;
        /// <summary>The loading screen is still waiting for its screen (it closes once this turns false and it has shown long enough).</summary>
        public bool IsLoadingScreenWaiting => loadingActive;

        private void CreateLoadingView()
        {
            loadingArt = GameLoadingArt.Load();
            if (loadingArt == null)
            {
                Debug.LogWarning("HAPIO_LOADING_ART_MISSING: Resources/" + GameLoadingArt.ResourcePath, this);
                return;
            }
            loadingView = GameLoadingView.Create(transform, loadingArt);
        }

        /// <summary>
        /// Whether moving from scene <paramref name="from"/> to <paramref name="to"/> shows the loading screen: always,
        /// except leaving a battle for its own result or game over, which keep their own presentation. Every way into a
        /// battle shows it (#607): the first one, the next stage, a retry from game over and any reload of Battle.
        /// </summary>
        public static bool ShowsLoading(string from, string to, bool tutorialStarting = false)
        {
            // The opening's hand-over to the practice board gets its own transition later (user decision).
            if (tutorialStarting) return false;
            // #607: the black "전투 준비 중" cover is no longer seen between stages; the loading screen counts players in.
            if (to == GameScenePaths.Battle) return true;
            bool fromRun = InRun(from), toRun = InRun(to);
            return !(fromRun && toRun && from != to);
        }

        /// <summary>Home is handing over to the practice board (the opening → tutorial), not restarting it.</summary>
        private bool TutorialStarting(string destination) => tutorialRun && destination == GameScenePaths.Battle
            && SceneManager.GetActiveScene().path == GameScenePaths.Home;

        private static bool InRun(string scene) => scene == GameScenePaths.Battle || scene == GameScenePaths.StageResult
            || scene == GameScenePaths.GameOver;

        /// <summary>The bar while a first battle waits for its players: from the scene's share up to 95% as they get ready.</summary>
        public static float BattleReadyProgress(int ready, int total) =>
            LoadingSceneShare + (LoadingReadyShare - LoadingSceneShare) * Mathf.Clamp01(ready / (float)Mathf.Max(1, total));

        /// <summary>Whether a shown loading screen may close: full, held at 100% a moment, and up for the minimum time.</summary>
        public static bool LoadingMayClose(float shownFor, float fullFor, float minimumSeconds, float fullHoldSeconds) =>
            fullFor >= 0f && fullFor >= fullHoldSeconds && shownFor >= minimumSeconds;

        private void BeginLoading(string destination)
        {
            if (loadingView == null) return;
            loadingFor = destination;
            loadingReady = false;
            loadingFullAt = -1f;
            loadingReadyFrame = -1;
            if (!loadingActive || !loadingView.Visible || loadingView.FadingOut)
            {
                loadingShownAt = Time.unscaledTime;
                loadingView.Show(0f);
                Debug.Log("HAPIO_LOADING_SHOW to=" + SceneName(destination));
            }
            loadingActive = true;
        }

        private void ReportSceneLoad(float asyncProgress)
        {
            if (loadingActive && loadingView != null)
                loadingView.SetProgress(LoadingSceneShare * Mathf.Clamp01(asyncProgress / .9f));
        }

        /// <summary>Every frame: once the scene is in, waits for it to be ready, fills to 100% and closes.</summary>
        private void UpdateLoading()
        {
            if (!loadingActive || loadingView == null || sceneRoutine != null) return;
            if (!loadingReady)
            {
                if (LoadedScreenReady(out float progress, out string detail))
                {
                    loadingReady = true;
                    loadingView.SetDetail(string.Empty);
                    loadingView.SetProgress(1f);
                }
                else
                {
                    loadingView.SetProgress(progress);
                    loadingView.SetDetail(detail);
                    return;
                }
            }
            if (loadingView.DisplayedProgress < 1f) return;
            if (loadingFullAt < 0f) loadingFullAt = Time.unscaledTime;
            if (!LoadingMayClose(Time.unscaledTime - loadingShownAt, Time.unscaledTime - loadingFullAt,
                loadingArt.minimumSeconds, loadingArt.fullHoldSeconds)) return;
            loadingActive = false;
            loadingView.FadeOut();
            Debug.Log("HAPIO_LOADING_HIDE at=" + SceneName(SceneManager.GetActiveScene().path)
                + " shown=" + (Time.unscaledTime - loadingShownAt).ToString("0.00") + "s");
        }

        /// <summary>
        /// Whether the screen just loaded is ready to be seen: Home and the seat screen once drawn; a first battle once its
        /// 3·2·1 begins (meanwhile the ready count); the tutorial once its first line can come.
        /// </summary>
        private bool LoadedScreenReady(out float progress, out string detail)
        {
            progress = LoadingSceneShare;
            detail = string.Empty;
            if (SceneManager.GetActiveScene().path != GameScenePaths.Battle)
            {
                if (loadingReadyFrame < 0) loadingReadyFrame = Time.frameCount;
                return Time.frameCount > loadingReadyFrame + 1;
            }
            if (tutorialRun)
            {
                bool playable = state.phase == GameFlowPhase.Playing && countdownCommitted && !network.GameStartHeld;
                progress = playable ? 1f : LoadingReadyShare;
                return playable;
            }
            if (!IsPreparingBattle) return true;
            // Close just before the first digit, so "3" is not covered and the old "전투 준비 중" never shows.
            if (state.phase == GameFlowPhase.Countdown && countdownCommitted && network.TryGetGameServerTime(out double now)
                && state.battleStartsAt - now <= CountdownSeconds + LoadingCloseLeadSeconds) return true;
            int total = Mathf.Max(1, StartParticipantCount), ready = Mathf.Clamp(StartReadyCount, 0, total);
            progress = BattleReadyProgress(ready, total);
            detail = "준비 " + ready + "/" + total;
            return false;
        }

        private static string SceneName(string path) =>
            string.IsNullOrEmpty(path) ? "?" : System.IO.Path.GetFileNameWithoutExtension(path);
    }
}
