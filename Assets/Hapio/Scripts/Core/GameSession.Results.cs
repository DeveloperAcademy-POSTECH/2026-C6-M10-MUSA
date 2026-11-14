using System.Collections.Generic;
using UnityEngine;

namespace Hapio
{
    public sealed partial class GameSession
    {
        private readonly HashSet<ulong> resultViewers = new HashSet<ulong>();
        private readonly HashSet<ulong> resultPresentations = new HashSet<ulong>();
        private bool localResultPresentationBusy;
        private readonly HashSet<ulong> sealParticipantsReady = new HashSet<ulong>();
        /// <summary>Members no longer counted on this result: they said 'exit', or never reported it in time.</summary>
        private readonly HashSet<ulong> resultExcluded = new HashSet<ulong>();
        // Host real times (negative: not started) for the result-ready limit and for a hold by other members.
        private double resultReadySince = -1d, resultHoldSince = -1d;
        private GameResultHold.Stage resultHoldStage;
        // Each device: when its open gourd viewer was last touched.
        private double viewerInputAt = -1d;
        public bool LocalSealReady { get; private set; }
        private bool RequiresSealReady => !IsTerritory && !tutorialRun
            && (state.phase == GameFlowPhase.StageResult || state.phase == GameFlowPhase.Completed);
        public int SealParticipantCount
        {
            get
            {
                int count = 0;
                if (network != null) foreach (ulong id in network.ConnectedPlayerIds)
                    if (!resultExcluded.Contains(id)) count++;
                return count;
            }
        }
        public int SealReadyCount
        {
            get
            {
                int count = 0;
                if (network != null) foreach (ulong id in network.ConnectedPlayerIds)
                    if (!resultExcluded.Contains(id) && sealParticipantsReady.Contains(id) && resultReady.Contains(id)
                        && !resultPresentations.Contains(id) && !resultViewers.Contains(id)) count++;
                return count;
            }
        }
        private bool AllSealParticipantsReady => !RequiresSealReady
            || SealParticipantCount > 0 && SealReadyCount == SealParticipantCount;
        public bool ConfirmSealReady()
        {
            if (!RequiresSealReady || network == null || !network.IsConnected || goingHome
                || IsLoadingScene || localResultPresentationBusy || IsViewerOpen || ResultSnapshot == null) return false;
            LocalSealReady = true;
            if (network.IsHost) SetSealParticipantReady(network.LocalPlayerId, true);
            else network.SendGameFlowToHost(JsonUtility.ToJson(MakeControl("seal_ready")));
            Changed?.Invoke();
            return true;
        }
        private void SetSealParticipantReady(ulong id, bool ready)
        {
            // True is sticky within one result generation; reordered old heartbeats cannot undo a tap.
            if (ready && RequiresSealReady) sealParticipantsReady.Add(id);
        }
        public void QuitStoryToRooms()
        {
            if (goingHome || tutorialRun || IsTerritory || !IsResultPresentationPhase) return;
            if (BeginGuestDeparture(true)) return;
            if (state.IsRunActive) ExitRun(); // Notify the other devices using the existing departure policy.
            FinishQuitToRooms();
        }

        private void FinishQuitToRooms()
        {
            goingHome = true;
            CancelGourdViewerPreparation();
            network.Stop(); network.UnbindBattleReferences();
            ResetRoomFlow();
            roomBrowserVisible = true;
            ClearDemoSelectionAfterLeave();
            SetLocalState(new GameFlowState());
            goingHome = !network.CanChooseConnection;
            if (!C6Lab.LabGameBuildVariant.IsStaffStageDemo) EnterRoomBrowser();
        }
        // A member leaving a shared result keeps its connection this long so its 'exit' (sent twice) reaches the Host
        // before the shutdown, instead of the Host learning it only from the 30 s transport timeout.
        private const double ExitLingerSeconds = .5d, ExitResendSeconds = .25d;
        private double departAt = -1d, departResendAt = -1d;
        private bool departToRooms;

        private bool BeginGuestDeparture(bool toRooms)
        {
            if (network == null || network.IsHost || !network.IsConnected || tutorialRun || !IsResultPresentationPhase) return false;
            network.SendGameFlowToHost(JsonUtility.ToJson(MakeControl("exit")));
            double now = Time.realtimeSinceStartupAsDouble;
            departAt = now + ExitLingerSeconds;
            departResendAt = now + ExitResendSeconds;
            departToRooms = toRooms;
            goingHome = true; // No other input or Host state is taken while leaving.
            Changed?.Invoke();
            return true;
        }

        private void UpdateGuestDeparture(double now)
        {
            if (departResendAt >= 0d && now >= departResendAt && network.IsConnected)
            {
                network.SendGameFlowToHost(JsonUtility.ToJson(MakeControl("exit")));
                departResendAt = -1d;
            }
            if (now < departAt && network.IsConnected) return;
            departAt = departResendAt = -1d;
            goingHome = false;
            if (departToRooms) FinishQuitToRooms();
            else FinishGoHome();
        }

        public bool SummaryStarted { get; private set; }
        /// <summary>
        /// The Host left after the final clear, before starting the summary. Its start signal can no longer arrive,
        /// so this member goes on to the summary and ending by itself instead of waiting on a silent picture.
        /// </summary>
        public bool HostLostAfterClear { get; private set; }
        /// <summary>
        /// The summary may open: the Host started it, the Host was lost after the clear, or this device's connection
        /// ended on the completed result (#642 keeps the local result identity), so nobody is left to press 마치기.
        /// </summary>
        public bool SummaryAllowed => SummaryStarted || HostLostAfterClear
            || state.phase == GameFlowPhase.Completed && !tutorialRun && !IsTerritory && state.stageIndex == RunProfile.FinalStage
                && network != null && !network.IsConnected && !goingHome;
        public bool CanStartSummary => network != null && network.IsConnected && !goingHome
            && !tutorialRun && !IsTerritory && state.phase == GameFlowPhase.Completed
            && state.stageIndex == RunProfile.FinalStage && !SummaryStarted && CanAdvanceStage;
        public bool StartSummary()
        {
            if (!CanStartSummary) return false;
            SummaryStarted = true;
            BroadcastState();
            Changed?.Invoke();
            return true;
        }
        private void ReadSummaryStart(Control control)
        {
            // Sticky within this exact result generation; a delayed false heartbeat cannot undo it.
            if (!control.summaryStarted || SummaryStarted || tutorialRun || IsTerritory
                || state.phase != GameFlowPhase.Completed || state.stageIndex != RunProfile.FinalStage
                || control.state == null || control.state.phase != GameFlowPhase.Completed
                || !MatchesControlIdentity(control) || control.state.battleSessionId != state.battleSessionId
                || control.state.battleRound != state.battleRound) return;
            SummaryStarted = true;
            Changed?.Invoke();
        }

        /// <summary>
        /// A member who leaves the final result no longer reads or views it; without this its last busy/viewer
        /// report would keep the Host's 다음 봉인 disabled for good. The Host losing itself frees its members.
        /// </summary>
        private void ForgetResultPeer(ulong id, bool lostHost)
        {
            resultViewers.Remove(id);
            resultPresentations.Remove(id);
            resultReady.Remove(id);
            sealParticipantsReady.Remove(id);
            if (lostHost && !network.IsHost && !tutorialRun && !IsTerritory && state.stageIndex == RunProfile.FinalStage)
                HostLostAfterClear = true;
            Changed?.Invoke();
        }

        public bool IsAnyResultViewerOpen => IsViewerOpen || resultViewers.Count > 0;
        public bool IsAnyResultPresentationBusy => localResultPresentationBusy || resultPresentations.Count > 0;

        /// <summary>Normal Story defeat keeps this room. An exit/disconnect/failed load never enables retry.</summary>
        public bool IsStageRetryAvailable => network != null && network.IsConnected && !goingHome
            && !IsTerritory && !tutorialRun && state.phase == GameFlowPhase.GameOver && state.retryableDefeat;
        public bool CanRetryStage => IsStageRetryAvailable && network.IsHost && !IsLoadingScene && !restartPending
            && HasFailedStageSnapshot && SameRetryRoster && AllResultsReady
            && (ResultHoldOverridden || !IsAnyResultViewerOpen && !IsAnyResultPresentationBusy);

        private bool SameRetryRoster => hostFlow.ParticipantCount > 0
            && new HashSet<ulong>(hostFlow.ParticipantIds).SetEquals(network.ConnectedPlayerIds);
        private bool HasFailedStageSnapshot => ResultSnapshot != null && ResultSnapshot.phase == C6Lab.LabPhase.Defeat
            && ResultSnapshot.sessionId == state.battleSessionId && ResultSnapshot.round == state.battleRound;
        private bool IsResultPresentationPhase => state.phase == GameFlowPhase.StageResult
            || state.phase == GameFlowPhase.Completed || state.phase == GameFlowPhase.GameOver && state.retryableDefeat;

        public bool RetryStage()
        {
            if (!CanRetryStage || !hostFlow.RetryFailedStage(state.runId, state.stageIndex, state.revision)) return false;
            // A failed attempt is never a completed-stage record; prior successful stages remain intact.
            stageResults.Remove(state.stageIndex);
            PrepareStage();
            return true;
        }

        public bool CanRetryTerritory => IsTerritory && state.phase == GameFlowPhase.Completed
            && network != null && network.IsConnected && network.IsHost && !goingHome && !IsLoadingScene
            && AllResultsReady && (ResultHoldOverridden || !IsAnyResultViewerOpen);

        public bool RetryTerritory()
        {
            if (!CanRetryTerritory) return false;
            if (!network.IsDeveloperSolo) return ReturnTerritoryToLobby("한 판이 끝났소. 다시 자리를 고르시오.");
            if (!hostFlow.RestartCurrentStage(state.runId, state.stageIndex, state.revision)) return false;
            stageResults.Remove(state.stageIndex);
            PrepareStage();
            return true;
        }

        public void SetResultPresentationBusy(bool busy)
        {
            if (!IsResultPresentationPhase || localResultPresentationBusy == busy) return;
            localResultPresentationBusy = busy;
            if (network != null && network.IsConnected)
            {
                if (network.IsHost) SetResultPresentation(network.LocalPlayerId, busy);
                else network.SendGameFlowToHost(JsonUtility.ToJson(MakeControl("result_presentation")));
            }
            Changed?.Invoke();
        }

        private void ResetResultAdvance()
        {
            LocalSealReady = false;
            sealParticipantsReady.Clear();
            SummaryStarted = false;
            HostLostAfterClear = false;
            resultViewers.Clear();
            resultPresentations.Clear();
            localResultPresentationBusy = false;
            resultExcluded.Clear();
            resultReadySince = resultHoldSince = -1d;
            resultHoldStage = GameResultHold.Stage.None;
            viewerInputAt = -1d;
        }

        /// <summary>
        /// After the long hold (#687 exception, user decision 2026-11-13) the Host may press start without members who
        /// are still reading, viewing or have not pressed 다음 봉인. Nothing starts by itself and nobody is marked ready.
        /// </summary>
        public bool ResultHoldOverridden => resultHoldStage == GameResultHold.Stage.Override;

        /// <summary>The Host's waiting heading after the notice time: what still holds the room (empty: nothing to say).</summary>
        public string ResultHoldNotice
        {
            get
            {
                if (resultHoldStage == GameResultHold.Stage.None || network == null || !network.IsHost) return string.Empty;
                foreach (ulong id in resultViewers)
                    if (id != network.LocalPlayerId) return "한 동지가 아직 봉인 박을 보고 있소.";
                foreach (ulong id in resultPresentations)
                    if (id != network.LocalPlayerId) return "한 동지가 아직 이야기를 보고 있소.";
                return "한 동지가 아직 채비하지 않았소.";
            }
        }

        /// <summary>
        /// Host: ends a result that a member never reported (30 s) and times how long other members hold the room.
        /// Each device: closes its own gourd viewer left untouched (60 s).
        /// </summary>
        private void UpdateResultHolds(double now)
        {
            if (IsViewerOpen)
            {
                if (viewerInputAt < 0d || StoryTapAdvance.HasHeldPointer) viewerInputAt = now;
                else if (GameResultHold.IdleExpired(viewerInputAt, now)) { CloseGourdViewer(); viewerInputAt = -1d; }
            }
            else viewerInputAt = -1d;
            if (network == null || !network.IsHost || !network.IsConnected || !IsResultPresentationPhase || goingHome)
            {
                SetResultHoldStage(GameResultHold.Stage.None);
                return;
            }
            if (!AllResultsReady)
            {
                resultHoldSince = -1d;
                SetResultHoldStage(GameResultHold.Stage.None);
                if (GameResultHold.ResultReadyExpired(resultReadySince, now)) ExpireMissingResults();
                return;
            }
            bool hostReady = !IsViewerOpen && !localResultPresentationBusy && (!RequiresSealReady || LocalSealReady);
            if (hostReady && OthersHoldResult())
            {
                if (resultHoldSince < 0d) resultHoldSince = now;
            }
            else resultHoldSince = -1d;
            SetResultHoldStage(GameResultHold.HoldStage(resultHoldSince, now));
        }

        private bool OthersHoldResult()
        {
            ulong self = network.LocalPlayerId;
            foreach (ulong id in resultViewers) if (id != self) return true;
            if (!IsTerritory)
            {
                foreach (ulong id in resultPresentations) if (id != self) return true;
                if (RequiresSealReady && SealReadyCount < SealParticipantCount) return true;
            }
            return false;
        }

        private void SetResultHoldStage(GameResultHold.Stage stage)
        {
            if (resultHoldStage == stage) return;
            resultHoldStage = stage;
            Changed?.Invoke();
        }

        /// <summary>A member who never reports its result (user decision 4): an intermediate stage ends for everyone,
        /// a completed run goes on without it, and a retryable defeat can no longer be retried.</summary>
        private void ExpireMissingResults()
        {
            resultReadySince = -1d;
            var missing = new List<ulong>();
            foreach (ulong id in network.ConnectedPlayerIds)
                if (!resultExcluded.Contains(id) && !resultReady.Contains(id)) missing.Add(id);
            if (missing.Count == 0) return;
            Debug.LogWarning("HAPIO_RESULT_READY_TIMEOUT phase=" + state.phase + " stage=" + state.stageIndex
                + " missing=" + string.Join(",", missing), this);
            if (state.phase == GameFlowPhase.StageResult) { AbortHost("The stage result could not be synchronized."); return; }
            if (state.phase == GameFlowPhase.GameOver)
            {
                if (hostFlow.EndDefeatRetry(GameRunFlow.PlayerDisconnectedReason)) SetHostState();
                return;
            }
            foreach (ulong id in missing) resultExcluded.Add(id);
            Changed?.Invoke();
        }

        /// <summary>The Host's own first result report starts the 30 s limit for everyone else's.</summary>
        private void NoteHostResultReady(double now)
        {
            if (resultReadySince < 0d) resultReadySince = now;
        }

        /// <summary>A member left this result screen ('exit'): it no longer holds the Host's buttons.</summary>
        private void ExcludeResultPeer(ulong id)
        {
            resultExcluded.Add(id);
            ForgetResultPeer(id, false);
        }

        private void SetResultViewer(ulong playerId, bool open)
        {
            if (open) resultViewers.Add(playerId);
            else resultViewers.Remove(playerId);
        }

        private void SetResultPresentation(ulong playerId, bool busy)
        {
            if (busy) resultPresentations.Add(playerId);
            else resultPresentations.Remove(playerId);
        }

        private void NotifyResultViewer()
        {
            if (!IsResultPresentationPhase || IsTerritory || !network.IsConnected) return;
            if (network.IsHost) SetResultViewer(network.LocalPlayerId, IsViewerOpen);
            else network.SendGameFlowToHost(JsonUtility.ToJson(MakeControl("result_viewer")));
        }
    }
}
