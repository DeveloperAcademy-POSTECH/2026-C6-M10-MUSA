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
        public Button advanceButton, exitButton, skipButton, overallButton, viewerButton;
        public Text statusText;

        private enum Mode { None, Waiting, AfterStage, ReadyToAdvance, Failure, FailureDashboard, Ending }
        private Mode mode;
        private GameSession session;
        private LabSnapshot boundResult;
        private string generation;
        private bool victory, initialized, showingOverall, subscribed, busy;
        private StorySequence failureDashboard;
        private Coroutine pendingReady;
        private bool resultActionsReady, transitionPending;
        private readonly List<BoundAction> boundActions = new List<BoundAction>();
        private sealed class BoundAction
        {
            public Button button;
            public string method;
            public Text caption;
            public string originalCaption;
            public Color originalColor;
        }
        private Coroutine pendingFinish;
        private StoryResultActions actions;
        public bool IsPresenting => mode != Mode.None;
        public bool IsShowingOverall => showingOverall;
        public bool IsLocalStoryComplete => resultActionsReady && (mode == Mode.ReadyToAdvance || mode == Mode.FailureDashboard);
        public bool IsResultActionReady => IsLocalStoryComplete;
        public LabSnapshot BoundResult => boundResult;

        public void Initialize(GameScreen owner, GameSession game)
        {
            if (initialized && screen == owner && session == game) return;
            StopPresentation();
            UnbindControls();
            screen = owner; session = game; initialized = true;
            if (player != null)
            {
                player.playOnStart = false;
                player.hideInlineContinueButtons = true;
                if (player.CurrentView != null) Show(player.CurrentView.continueButton, false);
                Show(player.nextButton, false); Set(player.nextButton, false);
            }
            if (presentationRoot != null) presentationRoot.SetActive(false);
            if (advanceButton != null) advanceButton.onClick.AddListener(Advance);
            if (exitButton != null) exitButton.onClick.AddListener(RequestExit);
            if (skipButton != null && (player == null || skipButton != player.skipButton)) skipButton.onClick.AddListener(Skip);
            if (overallButton != null) overallButton.onClick.AddListener(OpenOverall);
            if (viewerButton != null) viewerButton.onClick.AddListener(InspectGourd);
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
            StoryCardRecords.Record[] rows = CardRecords(boundResult, session.Network.LocalPlayerId);
            StoryCardRecords[] cards = view.GetComponentsInChildren<StoryCardRecords>(true);
            foreach (var records in cards)
                records.Bind(boundResult.paintCoverage, (float)boundResult.remaining, rows);
            // The failure dashboard has no record component, so its table is filled here from the same rows.
            StoryRecordHighlight.Apply(view.transform, rows, cards.Length == 0);
            var gauge = view.GetComponentInChildren<StorySealGauge>(true);
            if (gauge != null)
            {
                gauge.Show(boundResult.paintCoverage);
                if (!victory) BeginResultReady(Mode.FailureDashboard);
            }
            actions = view.GetComponent<StoryResultActions>();
            if (actions != null)
            {
                actions.onInspectGourd.AddListener(InspectGourd);
                actions.onRetry.AddListener(Retry);
                actions.onExit.AddListener(RequestExit);
                BindActionButtons();
            }
            RefreshControls();
        }

        public static StoryCardRecords.Record[] CardRecords(LabSnapshot snapshot, ulong localPlayerId = ulong.MaxValue)
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
                        result[i] = new StoryCardRecords.Record { player = LabResultStats.WinnerNames(award, localPlayerId),
                            value = LabResultStats.Record(award, snapshot.playedSeconds),
                            mine = award.Winners != null && award.Winners.Any(winner => winner != null && winner.id == localPlayerId) };
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
                if (pendingFinish == null) pendingFinish = StartCoroutine(ReturnHomeAfterRelease());
                return;
            }
            if (mode == Mode.ReadyToAdvance || mode == Mode.FailureDashboard) { RefreshControls(); return; }
            if (victory)
            {
                // Finish/Skip already retain CurrentView. Never replay the first card or its audio/entrance.
                if (reason == StoryExitReason.Skipped && player.CurrentView != null)
                    player.CurrentView.ShowCompletedPose();
                BeginResultReady(Mode.ReadyToAdvance);
            }
            else if (player.CurrentView == null || player.CurrentView.GetComponentInChildren<StorySealGauge>(true) == null)
            {
                // Failure Skip still lands once on the authored seal-rate dashboard and its real buttons.
                if (failureDashboard == null) failureDashboard = SingleCut(failure, failure.cuts.Count - 1, "failure-dashboard");
                player.Play(failureDashboard);
            }
            else BeginResultReady(Mode.FailureDashboard);
            RefreshControls();
        }

        private void BeginResultReady(Mode next)
        {
            if (mode == next && (pendingReady != null || resultActionsReady)) return;
            mode = next; resultActionsReady = false;
            if (player.CurrentView != null)
                foreach (var card in player.CurrentView.GetComponentsInChildren<StoryCardFlip>(true)) card.SetHintVisible(false);
            if (pendingReady != null) StopCoroutine(pendingReady);
            pendingReady = StartCoroutine(ReadyAfterRelease(generation));
        }

        private IEnumerator ReadyAfterRelease(string expectedGeneration)
        {
            // The gesture ending a story cannot also press the newly shown result action.
            yield return null;
            while (IsPresenting && generation == expectedGeneration
                && (StoryTapAdvance.HasHeldPointer || screen.StoryActionsBlocked)) yield return null;
            pendingReady = null;
            if (!IsPresenting || generation != expectedGeneration || session == null
                || GenerationKey(session) != expectedGeneration) yield break;
            resultActionsReady = true;
            SetBusy(false);
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
            if (IsPresenting && session != null && GenerationKey(session) == generation)
            { SetBusy(false); screen.TryStoryHome(); }
        }

        private bool ActionsBlocked => screen.StoryActionsBlocked || session.IsLoadingScene
            || session.IsViewerOpen || pendingFinish != null || transitionPending
            || ((mode == Mode.ReadyToAdvance || mode == Mode.FailureDashboard) && !resultActionsReady);

        private void RefreshControls()
        {
            if (!IsPresenting || session == null) return;
            bool blocked = ActionsBlocked;
            bool review = mode == Mode.ReadyToAdvance;
            bool failed = mode == Mode.FailureDashboard;
            bool final = victory && session.State.phase == GameFlowPhase.Completed;
            player.SetInputBlocked(blocked || showingOverall || review || failed);
            // The game uses tap-to-read; the authoring preview keeps its own toolbar unchanged.
            Show(player.nextButton, false); Set(player.nextButton, false);
            bool primary = review && (final || session.Network.IsHost) || failed && session.Network.IsHost && !HasAction("Retry");
            Show(advanceButton, primary);
            Set(advanceButton, primary && !blocked && (failed ? session.CanRetryStage : final ? session.CanReturnHome : session.CanAdvanceStage));
            Label(advanceButton, failed ? "이 스테이지 재도전" : final ? "엔딩 보기" : "다음 스테이지");
            bool overall = final && review && session.StageResults.Count > 0;
            Show(overallButton, overall); Set(overallButton, overall && !blocked);
            bool inspect = review && !HasAction("InspectGourd");
            Show(viewerButton, inspect); Set(viewerButton, inspect && !blocked && session.PreviewTexture != null);
            bool skip = player.IsPlaying && (mode == Mode.AfterStage || mode == Mode.Failure || mode == Mode.Ending);
            Show(skipButton, skip); Set(skipButton, skip && !blocked);
            bool exit = review && !final || failed && !HasAction("Exit");
            Show(exitButton, exit); Set(exitButton, exit && !blocked);
            Label(exitButton, "나가기");
            // The approved result design has no status line, so the saved label stays hidden.
            if (statusText != null && statusText.gameObject.activeSelf) statusText.gameObject.SetActive(false);
            RefreshActionButtons(blocked);
            ApplyVisibility();
        }

        private void BindActionButtons()
        {
            foreach (var button in actions.GetComponentsInChildren<Button>(true))
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    if (button.onClick.GetPersistentTarget(i) != actions) continue;
                    string method = button.onClick.GetPersistentMethodName(i);
                    if (method != "Retry" && method != "Exit" && method != "InspectGourd") continue;
                    Text caption = button.GetComponentInChildren<Text>(true);
                    if (caption == null)
                    {
                        string label = method == "Retry" ? "다시 도전" : method == "Exit" ? "나가기" : "봉인 박 둘러보기";
                        caption = actions.GetComponentsInChildren<Text>(true).FirstOrDefault(text => text.text == label);
                    }
                    boundActions.Add(new BoundAction { button = button, method = method, caption = caption,
                        originalCaption = caption != null ? caption.text : null, originalColor = caption != null ? caption.color : Color.white });
                }
        }

        private bool HasAction(string method) => boundActions.Exists(action => action.method == method && action.button != null);

        private void RefreshActionButtons(bool blocked)
        {
            foreach (var action in boundActions)
            {
                if (action.button == null) continue;
                bool interactable = !blocked && (action.method == "Retry" ? session.Network.IsHost && session.CanRetryStage
                    : action.method == "InspectGourd" ? victory && session.PreviewTexture != null
                    : session.State.IsRunActive || session.CanReturnHome);
                action.button.gameObject.SetActive(true);
                action.button.interactable = interactable;
                if (action.caption == null) continue;
                action.caption.text = action.method == "Retry" && !session.Network.IsHost ? "방장이 재도전" : action.originalCaption;
                Color color = action.originalColor;
                color.a *= interactable ? 1 : .45f;
                action.caption.color = color;
            }
        }

        private void ApplyVisibility()
        {
            if (presentationGroup == null) return;
            bool visible = IsPresenting && !showingOverall && !session.IsViewerOpen;
            presentationGroup.alpha = visible ? 1 : 0;
            presentationGroup.blocksRaycasts = visible;
            presentationGroup.interactable = visible && !screen.StoryActionsBlocked && pendingFinish == null
                && pendingReady == null && !transitionPending;
        }

        public void Advance()
        {
            if (!IsPresenting || ActionsBlocked) return;
            if (mode == Mode.FailureDashboard) { Retry(); return; }
            if (mode != Mode.ReadyToAdvance) return;
            if (session.State.phase == GameFlowPhase.Completed) BeginEnding();
            else
            {
                transitionPending = true;
                if (!screen.TryStoryAdvance()) transitionPending = false;
                RefreshControls();
            }
        }

        public void Skip()
        {
            if (!IsPresenting || ActionsBlocked) return;
            player.RequestSkip();
        }

        public void InspectGourd()
        {
            if (victory && IsPresenting && !ActionsBlocked && screen.TryStoryViewer()) RefreshControls();
        }

        public void Retry()
        {
            if (mode != Mode.FailureDashboard || ActionsBlocked) return;
            transitionPending = true;
            if (screen.TryStoryRetry()) StopPresentation();
            else { transitionPending = false; RefreshControls(); }
        }

        public void RequestExit()
        {
            if (!IsPresenting || ActionsBlocked) return;
            if (victory && session.State.phase == GameFlowPhase.Completed)
            { if (mode == Mode.ReadyToAdvance) BeginEnding(); }
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
            if (mode != Mode.ReadyToAdvance || session.State.phase != GameFlowPhase.Completed || ActionsBlocked) return;
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
            boundActions.Clear();
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
            if (pendingReady != null) StopCoroutine(pendingReady);
            pendingReady = null; resultActionsReady = false; transitionPending = false;
            if (busy && session != null && GenerationKey(session) == generation) SetBusy(false);
            busy = false;
            UnbindActions();
            if (subscribed && player != null) { player.Finished -= OnFinished; player.ViewChanged -= BindView; }
            subscribed = false;
            if (player != null) { player.Cancel(); player.SetInputBlocked(false); }
            if (presentationRoot != null) presentationRoot.SetActive(false);
            if (failureDashboard != null) Destroy(failureDashboard);
            failureDashboard = null;
            boundResult = null; generation = null; showingOverall = false; mode = Mode.None;
        }

        private void UnbindControls()
        {
            if (advanceButton != null) advanceButton.onClick.RemoveListener(Advance);
            if (exitButton != null) exitButton.onClick.RemoveListener(RequestExit);
            if (skipButton != null && (player == null || skipButton != player.skipButton)) skipButton.onClick.RemoveListener(Skip);
            if (overallButton != null) overallButton.onClick.RemoveListener(OpenOverall);
            if (viewerButton != null) viewerButton.onClick.RemoveListener(InspectGourd);
        }

        private void OnDisable() => StopPresentation();
        private void OnDestroy() { StopPresentation(); UnbindControls(); }
        private static void Show(Button button, bool value) { if (button != null) button.gameObject.SetActive(value); }
        private static void Set(Button button, bool value) { if (button != null) button.interactable = value; }
        private static void Label(Button button, string value) { if (button != null) { var text = button.GetComponentInChildren<Text>(true); if (text != null) text.text = value; } }
    }
}
