using System;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>
    /// How long each step of the shared battle start (#306) lasts. The game never assigns these: every build runs
    /// the defaults. Only the Hapio PlayMode test assembly shortens them for its own run (GameTestStartTiming) and
    /// restores them afterwards. Only the lengths change, never the order (initial snapshot/paint readiness →
    /// reservation → per-generation receipts → commit → 3/2/1 → Playing), the reschedules or the shared load budget.
    /// </summary>
    internal static class GameStartTiming
    {
        internal const double DefaultCountdownSeconds = 3d;
        internal const double DefaultInitialLeadSeconds = 1d;
        internal const double DefaultMaximumLeadSeconds = 5d;
        internal const double DefaultStartedLabelSeconds = .5d;

        /// <summary>The visible 3/2/1, from the first digit to Playing. The three digits split it evenly.</summary>
        internal static double CountdownSeconds { get; private set; } = DefaultCountdownSeconds;
        /// <summary>Time reserved before the visible countdown for every receipt of a fresh reservation.</summary>
        internal static double InitialLeadSeconds { get; private set; } = DefaultInitialLeadSeconds;
        /// <summary>Cap of the lead that each reschedule for a missing receipt doubles.</summary>
        internal static double MaximumLeadSeconds { get; private set; } = DefaultMaximumLeadSeconds;
        /// <summary>How long "시작!" stays after the shared start.</summary>
        internal static double StartedLabelSeconds { get; private set; } = DefaultStartedLabelSeconds;

        internal static bool IsDefault => CountdownSeconds == DefaultCountdownSeconds
            && InitialLeadSeconds == DefaultInitialLeadSeconds && MaximumLeadSeconds == DefaultMaximumLeadSeconds
            && StartedLabelSeconds == DefaultStartedLabelSeconds;

        /// <summary>Test-only. A session reads the values at each use, so change them only between battle starts.</summary>
        internal static void Configure(double countdownSeconds, double initialLeadSeconds, double maximumLeadSeconds,
            double startedLabelSeconds)
        {
            if (!Finite(countdownSeconds) || countdownSeconds <= 0d)
                throw new ArgumentOutOfRangeException(nameof(countdownSeconds));
            if (!Finite(initialLeadSeconds) || initialLeadSeconds <= 0d)
                throw new ArgumentOutOfRangeException(nameof(initialLeadSeconds));
            if (!Finite(maximumLeadSeconds) || maximumLeadSeconds < initialLeadSeconds)
                throw new ArgumentOutOfRangeException(nameof(maximumLeadSeconds));
            if (!Finite(startedLabelSeconds) || startedLabelSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(startedLabelSeconds));
            CountdownSeconds = countdownSeconds;
            InitialLeadSeconds = initialLeadSeconds;
            MaximumLeadSeconds = maximumLeadSeconds;
            StartedLabelSeconds = startedLabelSeconds;
        }

        internal static void Reset() => Configure(DefaultCountdownSeconds, DefaultInitialLeadSeconds,
            DefaultMaximumLeadSeconds, DefaultStartedLabelSeconds);

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        // Entering Play Mode skips the domain reload in this project (EditorSettings), so statics survive it:
        // never carry an interrupted test run's shortened start into the next Play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayModeEnter() => Reset();
    }

    public sealed partial class GameSession
    {
        private static double CountdownSeconds => GameStartTiming.CountdownSeconds;
        private static double StartedLabelSeconds => GameStartTiming.StartedLabelSeconds;
        private GameStartOverlay startOverlay;
        private int startReadyCount, startParticipantCount, initialReadyFrame = -1;
        private double nextInitialReadyAt, nextCountdownReadyAt, nextCountdownCommitAt;
        private double countdownLeadSeconds = GameStartTiming.InitialLeadSeconds;
        private bool countdownCommitted;
        private GameFrameSpikeProbe frameProbe;

        /// <summary>The Development/Editor battle frame probe (#481); null in a release player.</summary>
        public GameFrameSpikeProbe FrameProbe => frameProbe;

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
                        countdownLeadSeconds = GameStartTiming.InitialLeadSeconds;
                    }
                    if (state.phase == GameFlowPhase.SynchronizingBattle) initialReadyFrame = -1;
                    if (state.phase == GameFlowPhase.Countdown)
                    {
                        countdownCommitted = false;
                    }
                }
                // The practice board keeps its own plain cover, except under the opening's clouds (#635), which wait instead.
                if (tutorialRun) { if (CloudsUp) startOverlay?.Hide(); else startOverlay?.ShowPreparing(TutorialPreparingText); }
                else startOverlay?.ShowPreparing(StartReadyCount, StartParticipantCount);
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
            GameBattleBindings bindings = BattleBindings();
            return bindings != null && bindings.IsPresentationReady;
        }

        private GameBattleBindings BattleBindings()
        {
            var bindings = network.board != null ? network.board.GetComponent<GameBattleBindings>() : null;
            return bindings != null ? bindings : FindFirstObjectByType<GameBattleBindings>();
        }

        // #498: part of this device's initial-ready only, so it always ends before the Host can begin the countdown. A start
        // that has already used half the shared budget skips the drawing rather than risk aborting the room.
        private bool RenderWarmupFinished(double realNow)
        {
            GameBattleBindings bindings = BattleBindings();
            return bindings == null || bindings.FinishRenderWarmup(realNow - loadStartedAt <= loadingTimeoutSeconds * .5f);
        }

        private void UpdateBattleStart(double realNow)
        {
            // One budget covers loading, initial synchronization and start reservation.
            // A committed visible countdown is allowed to finish its own CountdownSeconds (three by default).
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
                if (!InitialPresentationReady() || !RenderWarmupFinished(realNow)) initialReadyFrame = -1;
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
                    if (!countdownCommitted && countdownNow >= state.battleStartsAt - ActiveCountdownSeconds)
                    {
                        // Reserve more lead time rather than ever starting with a missing schedule receipt.
                        countdownLeadSeconds = Math.Min(GameStartTiming.MaximumLeadSeconds, countdownLeadSeconds * 2d);
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
            Math.Ceiling((serverNow + ActiveCountdownSeconds + countdownLeadSeconds) * 1000d) / 1000d;

        /// <summary>The tutorial (#477) opens on its first line instead of a 3-2-1 countdown.</summary>
        private double ActiveCountdownSeconds => tutorialRun ? 0d : CountdownSeconds;

        private void RefreshStartOverlay()
        {
            if (startOverlay == null) return;
            if (tutorialRun)
            {
                // No ready count, countdown or "시작!" on the practice board: one plain cover until it is playable; a RESTART's
                // loading screen draws above it. Coming from the opening, the clouds (#635) cover the wait instead, with no words.
                bool playable = state.phase == GameFlowPhase.Playing && countdownCommitted && !network.GameStartHeld;
                if (!CloudsUp && (IsPreparingBattle || state.phase == GameFlowPhase.Playing && !playable))
                    startOverlay.ShowPreparing(TutorialPreparingText);
                else startOverlay.Hide();
                return;
            }
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
                    // Each digit is a third of the countdown: CountdownSeconds / 3 is exactly 1 for the default, so the
                    // default still shows ceil(remaining) seconds.
                    startOverlay.ShowCountdown(Mathf.Clamp((int)Math.Ceiling(remaining / (CountdownSeconds / 3d)), 1, 3));
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
