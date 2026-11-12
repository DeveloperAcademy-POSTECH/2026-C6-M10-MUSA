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
        public bool LocalSealReady { get; private set; }
        private bool RequiresSealReady => !IsTerritory && !tutorialRun
            && (state.phase == GameFlowPhase.StageResult || state.phase == GameFlowPhase.Completed);
        public int SealParticipantCount => network == null ? 0 : network.ConnectedPlayerIds.Length;
        public int SealReadyCount
        {
            get
            {
                int count = 0;
                if (network != null) foreach (ulong id in network.ConnectedPlayerIds)
                    if (sealParticipantsReady.Contains(id) && resultReady.Contains(id)
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
            if (state.IsRunActive) ExitRun(); // Notify the other devices using the existing departure policy.
            goingHome = true;
            network.Stop(); network.UnbindBattleReferences();
            ResetRoomFlow();
            roomBrowserVisible = true;
            SetLocalState(new GameFlowState());
            goingHome = !network.CanChooseConnection;
            EnterRoomBrowser();
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
            LocalSealReady = false;
            sealParticipantsReady.Clear();
            SummaryStarted = false;
            HostLostAfterClear = false;
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
