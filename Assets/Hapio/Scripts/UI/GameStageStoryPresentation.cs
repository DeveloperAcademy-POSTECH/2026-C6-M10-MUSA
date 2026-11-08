using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using C6Lab;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio
{
    /// <summary>Saved story artwork bound to a committed game result; never owns battle or network rules.</summary>
    public sealed class GameStageStoryPresentation : MonoBehaviour
    {
        public GameScreen screen;
        public StoryPlayer player;
        public GameObject presentationRoot;
        public CanvasGroup presentationGroup;
        public StorySequence[] afterStages;
        public StorySequence ending, failure;
        public Button advanceButton, exitButton, skipButton, overallButton;
        public Text statusText;

        private enum Mode { None, Waiting, AfterStage, CardReview, Failure, FailureDashboard, Ending }
        private Mode mode;
        private GameSession session;
        private LabSnapshot boundResult;
        private string generation;
        private bool victory, initialized, showingOverall, subscribed, busy;
        private StorySequence cardReview, failureDashboard;
        private Coroutine pendingFinish;
        private StoryResultActions actions;
        public bool IsPresenting => mode != Mode.None;
        public bool IsShowingOverall => showingOverall;
        public bool IsLocalStoryComplete => mode == Mode.CardReview || mode == Mode.FailureDashboard;
        public LabSnapshot BoundResult => boundResult;

        public void Initialize(GameScreen owner, GameSession game)
        {
            if (initialized && screen == owner && session == game) return;
            StopPresentation();
            UnbindControls();
            screen = owner; session = game; initialized = true;
            if (player != null) player.playOnStart = false;
            if (presentationRoot != null) presentationRoot.SetActive(false);
            if (advanceButton != null) advanceButton.onClick.AddListener(Advance);
            if (exitButton != null) exitButton.onClick.AddListener(RequestExit);
            if (skipButton != null && (player == null || skipButton != player.skipButton)) skipButton.onClick.AddListener(Skip);
            if (overallButton != null) overallButton.onClick.AddListener(OpenOverall);
        }

        public bool TryRefresh(bool successful)
        {
            if (!initialized || session == null || session.Network == null || screen == null) return false;
            bool eligible = !session.IsTerritory && !session.IsTutorial && (successful
                ? session.State.phase == GameFlowPhase.StageResult || session.State.phase == GameFlowPhase.Completed
                : session.IsStageRetryAvailable);
            if (!eligible || !BindingsValid(successful))
            {
                if (IsPresenting) StopPresentation();
                return false;
            }
            string key = GenerationKey(session);
            if (generation != key)
            {
                StopPresentation();
                generation = key; victory = successful; mode = Mode.Waiting;
                SetBusy(true);
                screen.SetStoryContentVisible(false);
                presentationRoot.SetActive(true);
                Subscribe();
            }
            if (mode == Mode.Waiting)
            {
                LabSnapshot result = session.ResultSnapshot;
                if (MatchesResult(result, successful))
                {
                    boundResult = result;
                    mode = successful ? Mode.AfterStage : Mode.Failure;
                    if (!player.Play(successful ? afterStages[session.State.stageIndex - 1] : failure))
                    { StopPresentation(); return false; }
                }
            }
            screen.RefreshStoryViewer();
            if (showingOverall)
            {
                screen.ShowStoryOverall();
                ApplyVisibility();
                return true;
            }
            screen.SetStoryContentVisible(false);
            RefreshControls();
            return true;
        }

        private bool BindingsValid(bool successful)
        {
            if (player == null || presentationRoot == null || presentationGroup == null || player.cutHost == null
                || presentationRoot == gameObject || transform.IsChildOf(presentationRoot.transform)) return false;
            StorySequence sequence = successful && session.State.stageIndex >= 1 && afterStages != null
                && session.State.stageIndex <= afterStages.Length ? afterStages[session.State.stageIndex - 1] : failure;
            return sequence != null && sequence.Validate(out _)
                && (!successful || session.State.phase != GameFlowPhase.Completed || ending != null && ending.Validate(out _));
        }

        private bool MatchesResult(LabSnapshot result, bool successful) => result != null
            && result.sessionId == session.State.battleSessionId && result.round == session.State.battleRound
            && result.phase == (successful ? LabPhase.Victory : LabPhase.Defeat);

        private static string GenerationKey(GameSession owner) => owner.State.runId + ":" + owner.State.stageIndex
            + ":" + owner.State.battleSessionId + ":" + owner.State.battleRound + ":" + owner.State.phase;

        private void Subscribe()
        {
            if (subscribed) return;
            player.Finished += OnFinished; player.ViewChanged += BindView;
            subscribed = true;
        }

        private void BindView(StoryCutView view)
        {
            UnbindActions();
            if (view == null || boundResult == null) return;
            foreach (var records in view.GetComponentsInChildren<StoryCardRecords>(true))
                records.Bind(boundResult.paintCoverage, (float)boundResult.remaining, CardRecords(boundResult));
            var gauge = view.GetComponentInChildren<StorySealGauge>(true);
            if (gauge != null)
            {
                gauge.Show(boundResult.paintCoverage);
                if (!victory) { mode = Mode.FailureDashboard; SetBusy(false); }
            }
            actions = view.GetComponent<StoryResultActions>();
            if (actions != null)
            {
                actions.onInspectGourd.AddListener(InspectGourd);
                actions.onRetry.AddListener(Retry);
                actions.onExit.AddListener(RequestExit);
            }
            RefreshControls();
        }

        public static StoryCardRecords.Record[] CardRecords(LabSnapshot snapshot)
        {
            var order = new[] { LabResultTitle.MostDamage, LabResultTitle.MostAccurate,
                LabResultTitle.MostPassed, LabResultTitle.MostDrumPassed, LabResultTitle.MostDrum };
            var result = new StoryCardRecords.Record[order.Length];
            if (snapshot == null || snapshot.players == null) return result;
            IReadOnlyList<LabResultAward> awards = LabResultStats.Awards(snapshot.players, snapshot.phase == LabPhase.Victory);
            for (int i = 0; i < order.Length; i++)
            {
                foreach (var award in awards)
                    if (award.Title == order[i])
                    {
                        result[i] = new StoryCardRecords.Record { player = LabResultStats.WinnerNames(award),
                            value = LabResultStats.Record(award, snapshot.playedSeconds) };
                        break;
                    }
            }
            return result;
        }

        private void OnFinished(StoryExitReason reason)
        {
            if (!IsPresenting) return;
            if (mode == Mode.Ending)
            {
                SetBusy(false);
                if (pendingFinish == null) pendingFinish = StartCoroutine(ReturnHomeAfterRelease());
                return;
            }
            if (mode == Mode.CardReview) { RefreshControls(); return; }
            if (victory)
            {
                mode = Mode.CardReview;
                SetBusy(false);
                if (cardReview == null) cardReview = SingleCut(afterStages[session.State.stageIndex - 1], 0, "result-card-review");
                player.Play(cardReview);
            }
            else
            {
                mode = Mode.FailureDashboard;
                SetBusy(false);
                if (player.CurrentView == null || player.CurrentView.GetComponentInChildren<StorySealGauge>(true) == null)
                {
                    if (failureDashboard == null) failureDashboard = SingleCut(failure, failure.cuts.Count - 1, "failure-dashboard");
                    player.Play(failureDashboard);
                }
            }
            RefreshControls();
        }

        private static StorySequence SingleCut(StorySequence source, int index, string id)
        {
            var copy = ScriptableObject.CreateInstance<StorySequence>();
            copy.hideFlags = HideFlags.DontSave;
            copy.storyId = id;
            copy.cuts.Add(source.cuts[index]);
            return copy;
        }

        private IEnumerator ReturnHomeAfterRelease()
        {
            yield return null;
            while (StoryTapAdvance.HasHeldPointer) yield return null;
            while (IsPresenting && session != null && GenerationKey(session) == generation && !session.CanReturnHome) yield return null;
            pendingFinish = null;
            if (IsPresenting && session != null && GenerationKey(session) == generation) screen.TryStoryHome();
        }

        private void RefreshControls()
        {
            if (!IsPresenting || session == null) return;
            bool blocked = screen.StoryActionsBlocked || session.IsLoadingScene || session.IsViewerOpen || pendingFinish != null;
            player.SetInputBlocked(blocked || showingOverall);
            bool review = mode == Mode.CardReview;
            bool failed = mode == Mode.FailureDashboard;
            bool final = victory && session.State.phase == GameFlowPhase.Completed;
            Show(advanceButton, (review && (final || session.Network.IsHost)) || (failed && session.Network.IsHost));
            Set(advanceButton, !blocked && (failed ? session.CanRetryStage : final ? session.CanReturnHome : session.CanAdvanceStage));
            Label(advanceButton, failed ? "이 스테이지 재도전" : final ? "엔딩 보기" : "다음 스테이지");
            Show(overallButton, final && review && session.StageResults.Count > 0);
            Set(overallButton, !blocked);
            Show(skipButton, mode == Mode.AfterStage || mode == Mode.Failure || mode == Mode.Ending);
            Set(skipButton, !blocked);
            Show(exitButton, mode != Mode.Ending);
            Set(exitButton, !blocked && (!final || session.CanReturnHome));
            Label(exitButton, final ? "엔딩 보기" : "나가기");
            if (statusText != null) statusText.text = mode == Mode.Waiting ? "결과 기록을 받고 있소…"
                : failed ? session.Network.IsHost ? session.CanRetryStage ? "같은 방에서 이 스테이지를 다시 봉인할 수 있소"
                    : "모두가 결과를 확인하기를 기다리고 있소" : "방장이 재도전하기를 기다리고 있소"
                : review && !final ? !session.Network.IsHost ? "방장이 다음 스테이지를 시작하기를 기다리고 있소"
                    : session.IsAnyResultViewerOpen ? "모두가 박 관람을 마치면 진행할 수 있소"
                    : session.IsAnyResultPresentationBusy ? "모두가 이야기를 확인하기를 기다리고 있소" : "다음 스테이지로 진행할 수 있소" : string.Empty;
            RefreshActionButtons(blocked);
            ApplyVisibility();
        }

        private void RefreshActionButtons(bool blocked)
        {
            if (actions == null) return;
            foreach (var button in actions.GetComponentsInChildren<Button>(true))
            {
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    if (button.onClick.GetPersistentTarget(i) != actions) continue;
                    switch (button.onClick.GetPersistentMethodName(i))
                    {
                        case "Retry":
                            button.gameObject.SetActive(session.Network.IsHost && session.IsStageRetryAvailable);
                            button.interactable = !blocked && session.CanRetryStage; break;
                        case "InspectGourd": button.interactable = !blocked && victory && session.PreviewTexture != null; break;
                        case "Exit": button.interactable = !blocked && (session.State.IsRunActive || session.CanReturnHome); break;
                    }
                }
            }
        }

        private void ApplyVisibility()
        {
            if (presentationGroup == null) return;
            bool visible = IsPresenting && !showingOverall && !session.IsViewerOpen;
            presentationGroup.alpha = visible ? 1 : 0;
            presentationGroup.blocksRaycasts = visible;
            presentationGroup.interactable = visible && !screen.StoryActionsBlocked && pendingFinish == null;
        }

        public void Advance()
        {
            if (!IsPresenting || screen.StoryActionsBlocked) return;
            if (mode == Mode.FailureDashboard) { Retry(); return; }
            if (mode != Mode.CardReview) return;
            if (session.State.phase == GameFlowPhase.Completed) BeginEnding();
            else if (screen.TryStoryAdvance()) RefreshControls();
        }

        public void Skip()
        {
            if (!IsPresenting || screen.StoryActionsBlocked || session.IsViewerOpen) return;
            player.RequestSkip();
        }

        public void InspectGourd()
        {
            if (victory && screen.TryStoryViewer()) RefreshControls();
        }

        public void Retry()
        {
            if (mode != Mode.FailureDashboard || screen.StoryActionsBlocked) return;
            if (screen.TryStoryRetry()) StopPresentation();
        }

        public void RequestExit()
        {
            if (!IsPresenting || screen.StoryActionsBlocked) return;
            if (victory && session.State.phase == GameFlowPhase.Completed) BeginEnding();
            else screen.TryStoryHome();
        }

        private void BeginEnding()
        {
            if (mode == Mode.Ending || !session.CanReturnHome) return;
            showingOverall = false; mode = Mode.Ending; SetBusy(true);
            player.Play(ending); RefreshControls();
        }

        public void OpenOverall()
        {
            if (mode != Mode.CardReview || session.State.phase != GameFlowPhase.Completed || screen.StoryActionsBlocked) return;
            showingOverall = true; screen.ShowStoryOverall(); RefreshControls();
        }

        public void CloseOverall()
        {
            showingOverall = false; screen.SetStoryContentVisible(false); RefreshControls();
        }

        private void SetBusy(bool value)
        {
            if (busy == value) return;
            busy = value;
            if (session != null) session.SetResultPresentationBusy(value);
        }

        private void UnbindActions()
        {
            if (actions == null) return;
            actions.onInspectGourd.RemoveListener(InspectGourd);
            actions.onRetry.RemoveListener(Retry);
            actions.onExit.RemoveListener(RequestExit);
            actions = null;
        }

        public void StopPresentation()
        {
            if (pendingFinish != null) StopCoroutine(pendingFinish);
            pendingFinish = null;
            if (busy && session != null && GenerationKey(session) == generation) SetBusy(false);
            busy = false;
            UnbindActions();
            if (subscribed && player != null) { player.Finished -= OnFinished; player.ViewChanged -= BindView; }
            subscribed = false;
            if (player != null) { player.Cancel(); player.SetInputBlocked(false); }
            if (presentationRoot != null) presentationRoot.SetActive(false);
            if (cardReview != null) Destroy(cardReview);
            if (failureDashboard != null) Destroy(failureDashboard);
            cardReview = failureDashboard = null;
            boundResult = null; generation = null; showingOverall = false; mode = Mode.None;
        }

        private void UnbindControls()
        {
            if (advanceButton != null) advanceButton.onClick.RemoveListener(Advance);
            if (exitButton != null) exitButton.onClick.RemoveListener(RequestExit);
            if (skipButton != null && (player == null || skipButton != player.skipButton)) skipButton.onClick.RemoveListener(Skip);
            if (overallButton != null) overallButton.onClick.RemoveListener(OpenOverall);
        }

        private void OnDisable() => StopPresentation();
        private void OnDestroy() { StopPresentation(); UnbindControls(); }
        private static void Show(Button button, bool value) { if (button != null) button.gameObject.SetActive(value); }
        private static void Set(Button button, bool value) { if (button != null) button.interactable = value; }
        private static void Label(Button button, string value) { if (button != null) { var text = button.GetComponentInChildren<Text>(true); if (text != null) text.text = value; } }
    }
}
