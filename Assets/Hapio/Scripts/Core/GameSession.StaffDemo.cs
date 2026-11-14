using C6Lab;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio
{
    public sealed partial class GameSession
    {
        /// <summary>The selected catalog stage. Not a seat, route position, or local developer override.</summary>
        public int SelectedDemoStage { get; private set; }

        private bool staffRetryRequested, staffRecoveryPrepared;
        private double nextStaffRetryAt, staffRetryDeadline;
        private const double StaffRetryAcknowledgementSeconds = 5d;
        private int staffRetryEntryStage;
        private readonly HashSet<ulong> staffRecoveryMembers = new HashSet<ulong>();
        // Kept on every lobby heartbeat until the next run, so losing one commit packet is recoverable.
        private string staffLobbyPreviousRun;
        private int staffLobbyPreviousStage;
        private uint staffLobbyPreviousRevision;

        public bool IsStaffRetryPending => staffRetryRequested || staffRetryEntryStage != 0;
        public bool CanRetryStaffDemo => GameRunFlow.IsStaffDemoInterrupted(state)
            && !goingHome && !IsLoadingScene && !IsStaffRetryPending && !IsDemoEntryPending
            && SelectedDemoStage == state.demoStageIndex;

        /// <summary>Any member may ask the Host to reopen this stage's seat lobby. Never starts a battle.</summary>
        public bool RetryStaffDemo()
        {
            if (!CanRetryStaffDemo) return false;
            if (!network.IsConnected) { ReenterStaffRetryStage(); return true; }
            if (network.IsHost) return ReturnStaffRetryToLobby();
            staffRetryRequested = true;
            staffRetryDeadline = Time.realtimeSinceStartupAsDouble + StaffRetryAcknowledgementSeconds;
            nextStaffRetryAt = 0d;
            SendStaffRetryRequest();
            Changed?.Invoke();
            return true;
        }

        private void SendStaffRetryRequest()
        {
            network.SendGameFlowToHost(JsonUtility.ToJson(MakeControl("staff_retry_lobby")));
            nextStaffRetryAt = Time.realtimeSinceStartupAsDouble + .5d;
        }

        private void UpdateStaffDemoRetry()
        {
            if (!LabGameBuildVariant.IsStaffStageDemo) return;
            if (staffRetryEntryStage != 0)
            {
                if (goingHome || !network.CanChooseConnection || state.phase != GameFlowPhase.Home
                    || IsLoadingScene || SceneManager.GetActiveScene().path != GameScenePaths.Home) return;
                int stage = staffRetryEntryStage;
                staffRetryEntryStage = 0;
                SelectDemoStage(stage);
                return;
            }
            if (staffRetryRequested)
            {
                if (!network.IsConnected || Time.realtimeSinceStartupAsDouble >= staffRetryDeadline)
                { ReenterStaffRetryStage(); return; }
                if (GameRunFlow.IsStaffDemoInterrupted(state) && Time.realtimeSinceStartupAsDouble >= nextStaffRetryAt)
                    SendStaffRetryRequest();
            }
            if (!network.IsHost || !staffRecoveryPrepared || !GameRunFlow.IsStaffDemoInterrupted(state)) return;
            // A lost peer cannot send a control message. Its deliberate same-stage re-entry is also a retry request.
            foreach (ulong id in network.ConnectedPlayerIds)
                if (!staffRecoveryMembers.Contains(id)) { ReturnStaffRetryToLobby(); break; }
        }

        private void ReenterStaffRetryStage()
        {
            int stage = SelectedDemoStage;
            staffRetryRequested = false;
            // Preserve the intent separately: FinishGoHome must still retire the old room/stage identity.
            FinishGoHome();
            staffRetryEntryStage = stage;
            UiNotice = "같은 스테이지의 방을 다시 준비하고 있소...";
            Changed?.Invoke();
        }

        private void ObserveStaffRetryState(GameFlowState previous)
        {
            if (!LabGameBuildVariant.IsStaffStageDemo) return;
            if (state.phase == GameFlowPhase.GameOver && network.IsHost
                && GameRunFlow.IsStaffDemoInterrupted(state) && !staffRecoveryPrepared)
            {
                // Reopen admission without changing the terminal UI. Otherwise a disconnected Guest's retry
                // could never join a room still advertised as Playing with a locked battle roster.
                if (network.ReturnStaffDemoGameToLobby())
                {
                    staffRecoveryPrepared = true;
                    staffRecoveryMembers.Clear();
                    foreach (ulong id in network.ConnectedPlayerIds) staffRecoveryMembers.Add(id);
                }
            }
            if (state.phase == GameFlowPhase.Lobby && previous.phase != GameFlowPhase.Home
                && previous.phase != GameFlowPhase.Lobby)
            {
                staffRetryRequested = staffRecoveryPrepared = false;
                staffRecoveryMembers.Clear();
                restartPending = battleReadyPending = localBattleReady = forceBattleReload = false;
                ResetBattleStart();
                stageResults.Clear(); stageResultsRunId = null;
                resultLocalPlayerId = ulong.MaxValue;
            }
            if (state.phase != GameFlowPhase.Lobby)
            {
                staffLobbyPreviousRun = null;
                staffLobbyPreviousStage = 0;
                staffLobbyPreviousRevision = 0;
            }
        }

        private bool ReturnStaffRetryToLobby()
        {
            if (!network.IsHost || !network.IsConnected || !GameRunFlow.IsStaffDemoInterrupted(state)) return false;
            GameFlowState ended = hostFlow.State;
            if (!hostFlow.ReturnStaffDemoToLobby(ended.runId, ended.stageIndex, ended.revision,
                "다시 도전하오. 세 명 모두 자리를 고른 뒤 방장이 시작하시오.")) return false;
            staffLobbyPreviousRun = ended.runId;
            staffLobbyPreviousStage = ended.stageIndex;
            staffLobbyPreviousRevision = ended.revision;
            CancelGourdViewerPreparation();
            network.ReturnStaffDemoGameToLobby();
            SetHostState();
            return true;
        }

        private bool IsStaffLobbyReturn(Control control)
        {
            GameFlowState incoming = control.state;
            return LabGameBuildVariant.IsStaffStageDemo && incoming != null
                && incoming.phase == GameFlowPhase.Lobby && incoming.mode == GameMode.Story
                && (state.IsRunActive || GameRunFlow.IsStaffDemoInterrupted(state))
                && incoming.demoStageIndex == SelectedDemoStage && state.demoStageIndex == SelectedDemoStage
                && control.previousRunId == state.runId && !string.IsNullOrEmpty(state.runId)
                && control.previousStageIndex == state.stageIndex
                && control.previousRevision >= state.revision && control.previousRevision < uint.MaxValue
                && incoming.revision == control.previousRevision + 1;
        }

        private void ResetStaffRetryRoom()
        {
            staffRetryRequested = staffRecoveryPrepared = false;
            staffRecoveryMembers.Clear();
            staffLobbyPreviousRun = null;
            staffLobbyPreviousStage = 0;
            staffLobbyPreviousRevision = 0;
        }

        private bool SetSelectedDemoStage(int stage)
        {
            if (!LabGameBuildVariant.IsStaffStageDemo || stage < 1 || stage > 3 || goingHome
                || state.phase != GameFlowPhase.Home || !network.CanChooseConnection
                || !hostFlow.ConfigureDemoStage(stage)) return false;
            SelectedDemoStage = stage;
            return true;
        }

        private void ClearDemoSelectionAfterLeave()
        {
            if (!LabGameBuildVariant.IsStaffStageDemo) return;
            SelectedDemoStage = 0;
            hostFlow.ConfigureDemoStage(0);
            if (network.CanChooseConnection) network.SetDemoStageIdentity(0);
            roomBrowserVisible = false;
        }

        /// <summary>Only a deliberate menu action returns the completed demo to its staff screen.</summary>
        public void ReturnToDemoSelection()
        {
            if (!LabGameBuildVariant.IsStaffStageDemo) return;
            GoHome();
        }
    }
}
