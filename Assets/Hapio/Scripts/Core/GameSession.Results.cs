using System.Collections.Generic;
using UnityEngine;

namespace Hapio
{
    public sealed partial class GameSession
    {
        private readonly HashSet<ulong> resultViewers = new HashSet<ulong>();
        private readonly HashSet<ulong> resultPresentations = new HashSet<ulong>();
        private bool localResultPresentationBusy;
        public bool SummaryStarted { get; private set; }
        public bool CanStartSummary => network != null && network.IsConnected && !goingHome
            && !tutorialRun && !IsTerritory && state.phase == GameFlowPhase.Completed
            && state.stageIndex == state.totalStages && !SummaryStarted && CanAdvanceStage;
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
                || state.phase != GameFlowPhase.Completed || state.stageIndex != state.totalStages
                || control.state == null || control.state.phase != GameFlowPhase.Completed
                || !MatchesControlIdentity(control) || control.state.battleSessionId != state.battleSessionId
                || control.state.battleRound != state.battleRound) return;
            SummaryStarted = true;
            Changed?.Invoke();
        }

        public bool IsAnyResultViewerOpen => IsViewerOpen || resultViewers.Count > 0;
        public bool IsAnyResultPresentationBusy => localResultPresentationBusy || resultPresentations.Count > 0;

        /// <summary>Normal Story defeat keeps this room. An exit/disconnect/failed load never enables retry.</summary>
        public bool IsStageRetryAvailable => network != null && network.IsConnected && !goingHome
            && !IsTerritory && !tutorialRun && state.phase == GameFlowPhase.GameOver && state.retryableDefeat;
        public bool CanRetryStage => IsStageRetryAvailable && network.IsHost && !IsLoadingScene && !restartPending
            && HasFailedStageSnapshot && SameRetryRoster && AllResultsReady
            && !IsAnyResultViewerOpen && !IsAnyResultPresentationBusy;

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
            SummaryStarted = false;
            resultViewers.Clear();
            resultPresentations.Clear();
            localResultPresentationBusy = false;
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
