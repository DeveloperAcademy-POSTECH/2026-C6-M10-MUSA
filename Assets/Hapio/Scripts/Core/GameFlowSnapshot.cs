using System;
using System.Collections.Generic;

namespace Hapio
{
    /// <summary>Validation for the authoritative run snapshot, before changing any Scene.</summary>
    public static class GameFlowSnapshot
    {
        public static bool IsValid(GameFlowState value, out string reason)
        {
            reason = string.Empty;
            if (value == null || !Enum.IsDefined(typeof(GameFlowPhase), value.phase))
                return Reject("invalid phase", out reason);
            if (value.runId == null || value.runId.Length > 64 || value.battleSessionId == null
                || value.battleSessionId.Length > 128 || value.endReason == null || value.endReason.Length > 256)
                return Reject("invalid text fields", out reason);
            if (value.totalStages != GameRunFlow.StageCount)
                return Reject("unsupported stage count", out reason);
            if (value.phase == GameFlowPhase.Home || value.phase == GameFlowPhase.Lobby)
            {
                if (value.stageIndex != 0 || !string.IsNullOrEmpty(value.runId)
                    || !string.IsNullOrEmpty(value.battleSessionId) || value.battleRound != 0)
                    return Reject("inactive state contains a battle", out reason);
                return true;
            }
            if (string.IsNullOrEmpty(value.runId) || value.revision == 0
                || value.stageIndex < 1 || value.stageIndex > GameRunFlow.StageCount)
                return Reject("invalid run generation", out reason);
            if (value.phase == GameFlowPhase.Completed && value.stageIndex != GameRunFlow.StageCount)
                return Reject("completed before final stage", out reason);
            if (value.phase == GameFlowPhase.StageResult && value.stageIndex >= GameRunFlow.StageCount)
                return Reject("final stage is not an intermediate result", out reason);
            if (value.phase == GameFlowPhase.LoadingBattle
                && (!string.IsNullOrEmpty(value.battleSessionId) || value.battleRound != 0))
                return Reject("load contains a previous battle", out reason);
            if ((value.phase == GameFlowPhase.Playing || value.phase == GameFlowPhase.StageResult
                || value.phase == GameFlowPhase.Completed)
                && (string.IsNullOrEmpty(value.battleSessionId) || value.battleRound == 0))
                return Reject("missing battle identity", out reason);
            if (value.phase == GameFlowPhase.GameOver && string.IsNullOrWhiteSpace(value.endReason))
                return Reject("missing game over reason", out reason);
            if (value.phase != GameFlowPhase.GameOver && !string.IsNullOrEmpty(value.endReason))
                return Reject("unexpected game over reason", out reason);
            if (string.IsNullOrEmpty(value.battleSessionId) != (value.battleRound == 0))
                return Reject("partial battle identity", out reason);
            return true;
        }

        public static bool TryAccept(GameFlowState current, GameFlowState incoming,
            bool allowNewRun, out string reason)
        {
            if (!IsValid(incoming, out reason)) return false;
            if (current == null) return true;
            if (incoming.runId != current.runId)
            {
                if (!allowNewRun) return Reject("different run", out reason);
                if (incoming.phase == GameFlowPhase.Home || incoming.phase == GameFlowPhase.Lobby)
                    return true;
                if ((current.phase != GameFlowPhase.Home && current.phase != GameFlowPhase.Lobby)
                    || incoming.phase != GameFlowPhase.LoadingBattle || incoming.stageIndex != 1)
                    return Reject("new run requires lobby", out reason);
                return true;
            }
            if (string.IsNullOrEmpty(current.runId) && allowNewRun
                && incoming.phase != current.phase) return true;
            if (incoming.revision <= current.revision)
                return Reject("duplicate or stale revision", out reason);
            if (incoming.stageIndex < current.stageIndex)
                return Reject("previous stage", out reason);
            if (current.phase == GameFlowPhase.GameOver || current.phase == GameFlowPhase.Completed)
                return Reject("terminal run", out reason);
            if (incoming.stageIndex > current.stageIndex + 1)
                return Reject("skipped stage", out reason);
            if (incoming.phase == GameFlowPhase.StageResult || incoming.phase == GameFlowPhase.Completed)
            {
                if (!string.IsNullOrEmpty(current.battleSessionId)
                    && (current.battleSessionId != incoming.battleSessionId
                        || current.battleRound != incoming.battleRound)
                    && current.stageIndex == incoming.stageIndex)
                    return Reject("different battle result", out reason);
            }
            return true;
        }

        private static bool Reject(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }

    /// <summary>
    /// Retains retired run IDs for one connected room. StartNewRoom is only called after leaving
    /// the old room, so delayed snapshots cannot reopen a completed or abandoned run.
    /// </summary>
    public sealed class GameFlowReplica
    {
        private readonly HashSet<string> retiredRuns = new HashSet<string>();
        private GameFlowState state;
        public GameFlowState State => state?.Copy();

        public bool Apply(GameFlowState incoming, bool allowNewRun, out string reason)
        {
            if (incoming != null && !string.IsNullOrEmpty(incoming.runId)
                && retiredRuns.Contains(incoming.runId))
            {
                reason = "retired run";
                return false;
            }
            if (!GameFlowSnapshot.TryAccept(state, incoming, allowNewRun, out reason)) return false;
            if (state != null && state.runId != incoming.runId && !string.IsNullOrEmpty(state.runId))
                retiredRuns.Add(state.runId);
            state = incoming.Copy();
            return true;
        }

        public void ResetForNewRoom()
        {
            state = null;
            retiredRuns.Clear();
        }
    }
}
