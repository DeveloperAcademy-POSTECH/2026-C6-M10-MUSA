using System;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    public sealed partial class GameSession
    {
        private const double CountdownSeconds = 3d;
        private const double StartedLabelSeconds = .5d;
        private GameStartOverlay startOverlay;
        private int startReadyCount, startParticipantCount, initialReadyFrame = -1;
        private double nextInitialReadyAt, nextCountdownReadyAt, nextCountdownCommitAt;
        private double countdownLeadSeconds = 1d;
        private bool countdownCommitted;

        public bool IsPreparingBattle => state.phase == GameFlowPhase.LoadingBattle
            || state.phase == GameFlowPhase.SynchronizingBattle || state.phase == GameFlowPhase.Countdown;
        public GameStartOverlay StartOverlay => startOverlay;
        public int StartParticipantCount => startParticipantCount;
        public int StartReadyCount => network != null && network.IsHost
            ? state.phase == GameFlowPhase.LoadingBattle ? hostFlow.ReadyCount
                : state.phase == GameFlowPhase.SynchronizingBattle ? hostFlow.InitialStateReadyCount
                : state.phase == GameFlowPhase.Countdown ? startParticipantCount : 0
            : startReadyCount;

        private void OnStartStateChanged(GameFlowState previous)
        {
            if (IsPreparingBattle)
            {
                network.SetGameStartHeld(true);
                startParticipantCount = network.IsDeveloperSolo ? 1 : network.TargetParticipantCount;
                if (previous.runId != state.runId || previous.stageIndex != state.stageIndex
                    || previous.revision != state.revision)
                {
                    startReadyCount = 0;
                    nextInitialReadyAt = nextCountdownReadyAt = nextCountdownCommitAt = 0d;
                    if (state.phase == GameFlowPhase.LoadingBattle)
                    {
                        countdownCommitted = false;
                        initialReadyFrame = -1;
                        countdownLeadSeconds = 1d;
                    }
                    if (state.phase == GameFlowPhase.SynchronizingBattle) initialReadyFrame = -1;
                    if (state.phase == GameFlowPhase.Countdown)
                    {
                        countdownCommitted = false;
                    }
                }
                startOverlay?.ShowPreparing(StartReadyCount, StartParticipantCount);
            }
            else if (state.phase == GameFlowPhase.Playing)
            {
                // The committed Playing state is also a replayable start authorization.
                countdownCommitted = true;
            }
            else ResetBattleStart();
        }

        private void ResetBattleStart()
        {
            countdownCommitted = false;
            initialReadyFrame = -1;
            startReadyCount = startParticipantCount = 0;
            nextInitialReadyAt = nextCountdownReadyAt = nextCountdownCommitAt = 0d;
            startOverlay?.Hide();
            network?.SetGameStartHeld(false);
        }

        private void ReadStartProgress(Control control)
        {
            if (!IsPreparingBattle || control.participantCount != StartParticipantCount
                || control.readyCount < 0 || control.readyCount > control.participantCount) return;
            startReadyCount = control.readyCount;
        }

        private bool InitialPresentationReady()
        {
            if (sceneRoutine != null || !StageConfigurationReady || !network.HasBattleReferences
                || !network.IsGameInitialStateReady(state.battleSessionId, state.battleRound)) return false;
            var bindings = network.board != null ? network.board.GetComponent<GameBattleBindings>() : null;
            if (bindings == null) bindings = FindFirstObjectByType<GameBattleBindings>();
            return bindings != null && bindings.IsPresentationReady;
        }

        private void UpdateBattleStart(double realNow)
        {
            // One budget covers loading, initial synchronization and start reservation.
            // A committed visible countdown is allowed to finish its own three seconds.
            if (network.IsHost && IsPreparingBattle && !countdownCommitted
                && realNow - loadStartedAt > loadingTimeoutSeconds)
            {
                AbortHost("A player could not finish preparing the stage.", "누군가 판을 준비하지 못해 로비로 돌아왔소.");
                RefreshStartOverlay();
                return;
            }
            if (state.phase == GameFlowPhase.LoadingBattle)
            {
                if (localBattleReady && realNow >= nextReadyAt) SendReady();
                if (network.IsHost && hostFlow.IsBattleReady && sceneRoutine == null
                    && (network.IsDeveloperSolo || network.SessionConfigReady))
                {
                    GameFlowState loading = hostFlow.State;
                    Identity(loading, out string stageId, out LabStageYokai yokai);
                    if (network.PrepareCurrentGameStage(loading.stageIndex, stageId, yokai,
                        expectedSettingsRevision, expectedSettingsFingerprint)
                        && network.Snapshot != null
                        && hostFlow.MarkSynchronizing(loading.runId, loading.stageIndex, loading.revision,
                            network.Snapshot.sessionId, network.Snapshot.round)) SetHostState();
                }
            }
            if (state.phase == GameFlowPhase.SynchronizingBattle)
            {
                if (!InitialPresentationReady()) initialReadyFrame = -1;
                else if (initialReadyFrame < 0) initialReadyFrame = Time.frameCount;
                // Give Start/LateUpdate/Canvas a frame to install the accepted initial presentation.
                else if (Time.frameCount > initialReadyFrame && realNow >= nextInitialReadyAt)
                {
                    nextInitialReadyAt = realNow + .5d;
                    if (network.IsHost)
                        hostFlow.MarkInitialStateReady(network.LocalPlayerId, state.runId, state.stageIndex, state.revision);
                    else network.SendGameFlowToHost(JsonUtility.ToJson(MakeControl("initial_ready")));
                }
                if (network.IsHost && hostFlow.IsInitialStateReady && network.TryGetGameServerTime(out double serverNow))
                {
                    GameFlowState sync = hostFlow.State;
                    if (hostFlow.BeginCountdown(sync.runId, sync.stageIndex, sync.revision,
                        ReserveStartAt(serverNow))) SetHostState();
                }
            }
            if (state.phase == GameFlowPhase.Countdown && network.TryGetGameServerTime(out double countdownNow))
            {
                if (InitialPresentationReady() && realNow >= nextCountdownReadyAt)
                {
                    nextCountdownReadyAt = realNow + .25d;
                    if (network.IsHost)
                        hostFlow.MarkCountdownReady(network.LocalPlayerId, state.runId, state.stageIndex, state.revision);
                    else network.SendGameFlowToHost(JsonUtility.ToJson(MakeControl("countdown_ready")));
                }
                if (network.IsHost)
                {
                    if (!countdownCommitted && countdownNow >= state.battleStartsAt - CountdownSeconds)
                    {
                        // Reserve more lead time rather than ever starting with a missing schedule receipt.
                        countdownLeadSeconds = Math.Min(5d, countdownLeadSeconds * 2d);
                        if (hostFlow.RescheduleCountdown(state.runId, state.stageIndex, state.revision,
                            ReserveStartAt(countdownNow))) SetHostState();
                    }
                    else if (hostFlow.IsCountdownReady)
                    {
                        countdownCommitted = true;
                        if (realNow >= nextCountdownCommitAt)
                        {
                            nextCountdownCommitAt = realNow + .25d;
                            network.BroadcastGameFlow(JsonUtility.ToJson(MakeControl("countdown_commit")));
                        }
                    }
                    if (countdownCommitted && countdownNow >= state.battleStartsAt)
                    {
                        GameFlowState countdown = hostFlow.State;
                        if (hostFlow.MarkPlaying(countdown.runId, countdown.stageIndex, countdown.revision,
                            countdown.battleSessionId, countdown.battleRound))
                        {
                            if (network.ReleasePreparedGameStage(countdown.battleSessionId, countdown.battleRound))
                                SetHostState();
                            else AbortHost("The prepared battle could not be started.");
                        }
                    }
                }
                else if (countdownCommitted && countdownNow >= state.battleStartsAt && InitialPresentationReady())
                    network.SetGameStartHeld(false);
            }
            if (state.phase == GameFlowPhase.Playing && network.GameStartHeld
                && network.TryGetGameServerTime(out double playingNow) && playingNow >= state.battleStartsAt
                && InitialPresentationReady()) network.SetGameStartHeld(false);

            RefreshStartOverlay();
        }

        // Integer-ms schedules are stable through Unity JSON on Editor and Player. Receipts
        // are identified by the monotonically increasing flow revision, never by double equality.
        private double ReserveStartAt(double serverNow) =>
            Math.Ceiling((serverNow + CountdownSeconds + countdownLeadSeconds) * 1000d) / 1000d;

        private void RefreshStartOverlay()
        {
            if (startOverlay == null) return;
            if (state.phase == GameFlowPhase.LoadingBattle || state.phase == GameFlowPhase.SynchronizingBattle)
            {
                startOverlay.ShowPreparing(StartReadyCount, StartParticipantCount);
                return;
            }
            if (state.phase == GameFlowPhase.Countdown || state.phase == GameFlowPhase.Playing && state.battleStartsAt > 0d)
            {
                if (!network.TryGetGameServerTime(out double now))
                { startOverlay.ShowPreparing(StartReadyCount, StartParticipantCount); return; }
                double remaining = state.battleStartsAt - now;
                if (remaining > CountdownSeconds || !countdownCommitted)
                    startOverlay.ShowPreparing(StartParticipantCount, StartParticipantCount);
                else if (remaining > 0d)
                    startOverlay.ShowCountdown(Mathf.Clamp((int)Math.Ceiling(remaining), 1, 3));
                else if (!countdownCommitted || network.GameStartHeld)
                    startOverlay.ShowPreparing(StartParticipantCount, StartParticipantCount);
                else if (now - state.battleStartsAt < StartedLabelSeconds) startOverlay.ShowStarted();
                else startOverlay.Hide();
                return;
            }
            startOverlay.Hide();
        }
    }
}
