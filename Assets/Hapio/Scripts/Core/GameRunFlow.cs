using System;
using System.Collections.Generic;

namespace Hapio
{
    /// <summary>
    /// Host-only state transitions. Battle resource resets stay in the existing battle model.
    /// Each load generation has its own revision, so an old scene-ready acknowledgement cannot
    /// release the next stage or a developer restart.
    /// </summary>
    public sealed class GameRunFlow
    {
        public const int StageCount = 3;
        public static int StagesFor(GameMode mode) => mode == GameMode.Territory ? 1 : StageCount;
        private readonly HashSet<ulong> participants = new HashSet<ulong>();
        private readonly HashSet<ulong> ready = new HashSet<ulong>();
        private readonly HashSet<ulong> initialStateReady = new HashSet<ulong>();
        private readonly HashSet<ulong> countdownReady = new HashSet<ulong>();
        private GameFlowState state = new GameFlowState();

        public GameFlowState State => state.Copy();
        public IReadOnlyCollection<ulong> ParticipantIds => new List<ulong>(participants).AsReadOnly();
        public int ReadyCount => ready.Count;
        public int ParticipantCount => participants.Count;
        public int InitialStateReadyCount => initialStateReady.Count;
        public int CountdownReadyCount => countdownReady.Count;
        public bool IsInitialStateReady => state.phase == GameFlowPhase.SynchronizingBattle
            && Everyone(initialStateReady);
        public bool IsCountdownReady => state.phase == GameFlowPhase.Countdown && Everyone(countdownReady);
        public bool IsBattleReady => state.phase == GameFlowPhase.LoadingBattle
            && participants.Count > 0 && ready.Count == participants.Count;

        public bool EnterLobby(GameMode mode = GameMode.Story)
        {
            if (state.phase != GameFlowPhase.Home || !Enum.IsDefined(typeof(GameMode), mode)) return false;
            state = new GameFlowState { phase = GameFlowPhase.Lobby, mode = mode, totalStages = StagesFor(mode) };
            return true;
        }

        public bool BeginRun(IEnumerable<ulong> playerIds)
        {
            if (state.phase != GameFlowPhase.Home && state.phase != GameFlowPhase.Lobby)
                return false;
            if (playerIds == null) return false;
            var requested = new HashSet<ulong>();
            foreach (ulong id in playerIds)
                if (!requested.Add(id)) return false;
            if (requested.Count == 0) return false;

            participants.Clear();
            participants.UnionWith(requested);
            ClearReadiness();
            GameMode mode = state.mode;
            state = new GameFlowState
            {
                runId = Guid.NewGuid().ToString("N"),
                revision = 1,
                stageIndex = 1,
                mode = mode,
                totalStages = StagesFor(mode),
                phase = GameFlowPhase.LoadingBattle
            };
            return true;
        }

        public bool MarkBattleReady(ulong playerId, string runId, int stageIndex, uint revision)
        {
            return Matches(runId, stageIndex, revision)
                && state.phase == GameFlowPhase.LoadingBattle
                && participants.Contains(playerId) && ready.Add(playerId);
        }

        /// <summary>The Scene barrier is complete; the new round exists but its gameplay is held.</summary>
        public bool MarkSynchronizing(string runId, int stageIndex, uint revision,
            string battleSessionId, uint battleRound)
        {
            if (!Matches(runId, stageIndex, revision) || !IsBattleReady
                || string.IsNullOrEmpty(battleSessionId) || battleSessionId.Length > 128
                || battleRound == 0) return false;
            state.phase = GameFlowPhase.SynchronizingBattle;
            state.battleSessionId = battleSessionId;
            state.battleRound = battleRound;
            state.battleStartsAt = 0d;
            state.revision++;
            initialStateReady.Clear();
            countdownReady.Clear();
            return true;
        }

        public bool MarkInitialStateReady(ulong playerId, string runId, int stageIndex, uint revision) =>
            Matches(runId, stageIndex, revision) && state.phase == GameFlowPhase.SynchronizingBattle
            && participants.Contains(playerId) && initialStateReady.Add(playerId);

        public bool BeginCountdown(string runId, int stageIndex, uint revision, double battleStartsAt)
        {
            if (!Matches(runId, stageIndex, revision) || !IsInitialStateReady || !ValidDeadline(battleStartsAt))
                return false;
            state.phase = GameFlowPhase.Countdown;
            state.battleStartsAt = battleStartsAt;
            state.revision++;
            countdownReady.Clear();
            return true;
        }

        /// <summary>Confirms receipt of this particular schedule, separately from initial-state readiness.</summary>
        public bool MarkCountdownReady(ulong playerId, string runId, int stageIndex, uint revision) =>
            Matches(runId, stageIndex, revision) && state.phase == GameFlowPhase.Countdown
            && participants.Contains(playerId) && countdownReady.Add(playerId);

        /// <summary>A late schedule ACK cannot release a later schedule after its start was deferred.</summary>
        public bool RescheduleCountdown(string runId, int stageIndex, uint revision, double battleStartsAt)
        {
            if (!Matches(runId, stageIndex, revision) || state.phase != GameFlowPhase.Countdown
                || !ValidDeadline(battleStartsAt) || battleStartsAt <= state.battleStartsAt) return false;
            state.battleStartsAt = battleStartsAt;
            state.revision++;
            countdownReady.Clear();
            return true;
        }

        /// <summary>GameSession additionally checks that the common server-time deadline has arrived.</summary>
        public bool MarkPlaying(string runId, int stageIndex, uint revision,
            string battleSessionId, uint battleRound)
        {
            if (!Matches(runId, stageIndex, revision) || !IsCountdownReady
                || state.battleSessionId != battleSessionId || state.battleRound != battleRound) return false;
            state.phase = GameFlowPhase.Playing;
            state.revision++;
            return true;
        }

        public bool MarkVictory(string battleSessionId, uint battleRound)
        {
            if (state.phase != GameFlowPhase.Playing
                || state.battleSessionId != battleSessionId || state.battleRound != battleRound)
                return false;
            state.phase = state.stageIndex == state.totalStages
                ? GameFlowPhase.Completed : GameFlowPhase.StageResult;
            state.revision++;
            return true;
        }

        public bool NextStage(string runId, int stageIndex, uint revision)
        {
            if (!Matches(runId, stageIndex, revision) || state.phase != GameFlowPhase.StageResult
                || state.stageIndex >= state.totalStages) return false;
            state.stageIndex++;
            BeginLoad();
            return true;
        }

        /// <summary>DEV-only caller authorization belongs to GameSession; this transition never records victory.</summary>
        public bool DeveloperNextStage(string runId, int stageIndex, uint revision)
        {
            if (!Matches(runId, stageIndex, revision) || state.phase != GameFlowPhase.Playing
                || state.stageIndex >= state.totalStages) return false;
            state.stageIndex++;
            BeginLoad();
            return true;
        }

        public bool RestartCurrentStage(string runId, int stageIndex, uint revision)
        {
            if (!Matches(runId, stageIndex, revision)
                || (state.phase != GameFlowPhase.Playing && state.phase != GameFlowPhase.StageResult))
                return false;
            BeginLoad();
            return true;
        }

        public bool Abort(string reason)
        {
            if (!state.IsRunActive || string.IsNullOrWhiteSpace(reason) || reason.Length > 256) return false;
            state.phase = GameFlowPhase.GameOver;
            state.endReason = reason;
            state.revision++;
            ClearReadiness();
            return true;
        }

        /// <summary>
        /// 2:2 (#281): end the battle or its result and keep the room. The lobby state's revision follows the run's,
        /// so a lobby state from before this run can never pull a peer back out of a later battle.
        /// </summary>
        public bool ReturnToLobby(string notice)
        {
            if (state.mode != GameMode.Territory || notice == null || notice.Length > 128
                || !(state.IsRunActive || state.phase == GameFlowPhase.Completed)) return false;
            state = new GameFlowState
            {
                phase = GameFlowPhase.Lobby, mode = GameMode.Territory, totalStages = StagesFor(GameMode.Territory),
                revision = state.revision + 1, notice = notice
            };
            participants.Clear();
            ClearReadiness();
            return true;
        }

        public bool ReturnHome()
        {
            if (state.phase == GameFlowPhase.Home) return false;
            state = new GameFlowState();
            participants.Clear();
            ClearReadiness();
            return true;
        }

        private bool Everyone(HashSet<ulong> acknowledgements) =>
            participants.Count > 0 && acknowledgements.Count == participants.Count;

        private static bool ValidDeadline(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d && value <= 1e12d;

        private void ClearReadiness()
        {
            ready.Clear();
            initialStateReady.Clear();
            countdownReady.Clear();
        }

        private bool Matches(string runId, int stageIndex, uint revision)
        {
            return !string.IsNullOrEmpty(runId) && state.runId == runId
                && state.stageIndex == stageIndex && state.revision == revision;
        }

        private void BeginLoad()
        {
            state.phase = GameFlowPhase.LoadingBattle;
            state.battleSessionId = string.Empty;
            state.battleRound = 0;
            state.battleStartsAt = 0d;
            state.endReason = string.Empty;
            state.revision++;
            ClearReadiness();
        }
    }
}
