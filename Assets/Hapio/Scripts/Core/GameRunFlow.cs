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
        private readonly HashSet<ulong> participants = new HashSet<ulong>();
        private readonly HashSet<ulong> ready = new HashSet<ulong>();
        private GameFlowState state = new GameFlowState();

        public GameFlowState State => state.Copy();
        public IReadOnlyCollection<ulong> ParticipantIds => new List<ulong>(participants).AsReadOnly();
        public int ReadyCount => ready.Count;
        public bool IsBattleReady => state.phase == GameFlowPhase.LoadingBattle
            && participants.Count > 0 && ready.Count == participants.Count;

        public bool EnterLobby()
        {
            if (state.phase != GameFlowPhase.Home) return false;
            state = new GameFlowState { phase = GameFlowPhase.Lobby };
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
            ready.Clear();
            state = new GameFlowState
            {
                runId = Guid.NewGuid().ToString("N"),
                revision = 1,
                stageIndex = 1,
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

        public bool MarkPlaying(string runId, int stageIndex, uint revision,
            string battleSessionId, uint battleRound)
        {
            if (!Matches(runId, stageIndex, revision) || !IsBattleReady
                || string.IsNullOrEmpty(battleSessionId) || battleSessionId.Length > 128
                || battleRound == 0) return false;
            state.phase = GameFlowPhase.Playing;
            state.battleSessionId = battleSessionId;
            state.battleRound = battleRound;
            state.revision++;
            return true;
        }

        public bool MarkVictory(string battleSessionId, uint battleRound)
        {
            if (state.phase != GameFlowPhase.Playing
                || state.battleSessionId != battleSessionId || state.battleRound != battleRound)
                return false;
            state.phase = state.stageIndex == StageCount
                ? GameFlowPhase.Completed : GameFlowPhase.StageResult;
            state.revision++;
            return true;
        }

        public bool NextStage(string runId, int stageIndex, uint revision)
        {
            if (!Matches(runId, stageIndex, revision) || state.phase != GameFlowPhase.StageResult
                || state.stageIndex >= StageCount) return false;
            state.stageIndex++;
            BeginLoad();
            return true;
        }

        /// <summary>DEV-only caller authorization belongs to GameSession; this transition never records victory.</summary>
        public bool DeveloperNextStage(string runId, int stageIndex, uint revision)
        {
            if (!Matches(runId, stageIndex, revision) || state.phase != GameFlowPhase.Playing
                || state.stageIndex >= StageCount) return false;
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
            ready.Clear();
            return true;
        }

        public bool ReturnHome()
        {
            if (state.phase == GameFlowPhase.Home) return false;
            state = new GameFlowState();
            participants.Clear();
            ready.Clear();
            return true;
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
            state.endReason = string.Empty;
            state.revision++;
            ready.Clear();
        }
    }
}
