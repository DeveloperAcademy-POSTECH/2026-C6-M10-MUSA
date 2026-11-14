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
        // #717: 1 s, so the styled "봉인 시작!" can be read; it never blocks input, like "시작!".
        internal const double DefaultStartedLabelSeconds = 1d;

        /// <summary>The visible 3/2/1, from the first digit to Playing. The three digits split it evenly.</summary>
        internal static double CountdownSeconds { get; private set; } = DefaultCountdownSeconds;
        /// <summary>Time reserved before the visible countdown for every receipt of a fresh reservation.</summary>
        internal static double InitialLeadSeconds { get; private set; } = DefaultInitialLeadSeconds;
        /// <summary>Cap of the lead that each reschedule for a missing receipt doubles.</summary>
        internal static double MaximumLeadSeconds { get; private set; } = DefaultMaximumLeadSeconds;
        /// <summary>How long "시작!" (territory) or "봉인 시작!" (story) stays after the shared start.</summary>
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

    /// <summary>
    /// Limits of a Guest's own wait at the battle start, so a stuck start always ends (2026-11-14). They are not
    /// part of <see cref="GameStartTiming"/>: they never change the shared start order or the Host's load budget.
    /// Both counters add only foreground frame time, each frame clamped to <see cref="MaximumFrameSeconds"/>, so an
    /// iPhone that was briefly in the background (#502) resumes with almost nothing added.
    /// </summary>
    public static class GameStartWaitLimits
    {
        /// <summary>The longest single frame a counter adds; a resume after the background adds only this.</summary>
        public const double MaximumFrameSeconds = .25d;
        /// <summary>A committed Guest still held this long after the shared start leaves the run.</summary>
        public const double StartHeldLimitSeconds = 10d;
        /// <summary>Margin above the Host's longest normal preparation (load budget + countdown + maximum lead).</summary>
        public const double PreparationMarginSeconds = 7d;
        /// <summary>How long after the shared start the Host retries its own initial-state check before aborting.</summary>
        public const double HostReleaseRetrySeconds = .5d;

        /// <summary>45 s with the defaults (30 + 3 + 5 + 7); it follows the test-only start lengths too.</summary>
        public static double PreparationLimitSeconds(double loadingTimeoutSeconds, double countdownSeconds,
            double maximumLeadSeconds) =>
            loadingTimeoutSeconds + countdownSeconds + maximumLeadSeconds + PreparationMarginSeconds;

        /// <summary>The foreground time one frame adds: never negative or non-finite, at most one clamped frame.</summary>
        public static double ForegroundDelta(double unscaledDeltaTime) =>
            double.IsNaN(unscaledDeltaTime) || unscaledDeltaTime <= 0d ? 0d
                : Math.Min(unscaledDeltaTime, MaximumFrameSeconds);

        /// <summary>Adds this frame while <paramref name="counting"/>; true once the total reaches the limit.</summary>
        public static bool Advance(ref double elapsed, bool counting, double unscaledDeltaTime, double limitSeconds)
        {
            if (!counting) return false;
            elapsed += ForegroundDelta(unscaledDeltaTime);
            return elapsed >= limitSeconds;
        }

        /// <summary>The Host waits for its own initial state only shortly after the shared start.</summary>
        public static bool HostMayRetryRelease(double serverNow, double battleStartsAt) =>
            serverNow < battleStartsAt + HostReleaseRetrySeconds;
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
        // Foreground-time counters of a Guest's own start waits (GameStartWaitLimits).
        private double startHeldAccum, preparingAccum;
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
                bool wasPreparing = previous.phase == GameFlowPhase.LoadingBattle
                    || previous.phase == GameFlowPhase.SynchronizingBattle || previous.phase == GameFlowPhase.Countdown;
                // One preparation counter covers loading, synchronization and countdown; a countdown reschedule keeps it.
                if (!wasPreparing || previous.runId != state.runId || previous.stageIndex != state.stageIndex
                    || state.phase == GameFlowPhase.LoadingBattle && previous.revision != state.revision)
                    preparingAccum = startHeldAccum = 0d;
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
            startHeldAccum = preparingAccum = 0d;
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
                    // The Host's own initial state may lag a frame behind the commit (a late owner board or paint
                    // install): stay committed in Countdown briefly, then keep the original release-or-abort.
                    if (countdownCommitted && countdownNow >= state.battleStartsAt
                        && (network.IsGameInitialStateReady(state.battleSessionId, state.battleRound)
                            || !GameStartWaitLimits.HostMayRetryRelease(countdownNow, state.battleStartsAt)))
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

            if (!network.IsHost && UpdateGuestStartWaits(Time.unscaledDeltaTime)) return;
            RefreshStartOverlay();
        }

        /// <summary>Preparation limit of this session: 45 s with the defaults.</summary>
        internal double GuestPreparationLimitSeconds => GameStartWaitLimits.PreparationLimitSeconds(
            loadingTimeoutSeconds, GameStartTiming.CountdownSeconds, GameStartTiming.MaximumLeadSeconds);

        /// <summary>
        /// A Guest leaves the run through the ordinary exit (the Host then stops it for everyone: GameOver, or the
        /// 2:2 lobby) when its own committed start stays held, or when the Host never finishes starting the stage.
        /// True when it left this frame.
        /// </summary>
        private bool UpdateGuestStartWaits(double unscaledDeltaTime)
        {
            bool held = countdownCommitted && network.GameStartHeld
                && (state.phase == GameFlowPhase.Countdown || state.phase == GameFlowPhase.Playing)
                && network.TryGetGameServerTime(out double serverNow) && serverNow >= state.battleStartsAt;
            if (GameStartWaitLimits.Advance(ref startHeldAccum, held, unscaledDeltaTime,
                GameStartWaitLimits.StartHeldLimitSeconds))
            {
                Debug.LogWarning("HAPIO_START_HELD_EXPIRED", this);
                ExitWithLocalReason("This device could not start the stage.");
                return true;
            }
            if (GameStartWaitLimits.Advance(ref preparingAccum, IsPreparingBattle, unscaledDeltaTime,
                GuestPreparationLimitSeconds))
            {
                Debug.LogWarning("HAPIO_START_PREPARATION_EXPIRED", this);
                ExitWithLocalReason("The host did not start the stage.");
                return true;
            }
            return false;
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
            // #717: a finished battle keeps its end words over the screen: "경기 종료!" until the territory result replaces it,
            // "시간 초과!" until the failure Scene replaces it, "봉인 성공!" until the final-hit effect starts.
            if (BattleEndCovering) { ShowBattleEnd(); return; }
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
                else if (now - state.battleStartsAt < StartedLabelSeconds)
                {
                    // #717: a story stage opens with "봉인 시작!"; the territory match keeps "시작!".
                    if (state.mode == GameMode.Story) startOverlay.ShowSealStarted(); else startOverlay.ShowStarted();
                }
                else startOverlay.Hide();
                return;
            }
            startOverlay.Hide();
        }
    }
}
