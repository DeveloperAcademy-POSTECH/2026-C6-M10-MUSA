using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hapio
{
    public sealed partial class GameSession
    {
        public const double StageResultDisplaySeconds = 10d;
        private readonly HashSet<ulong> resultViewers = new HashSet<ulong>();
        private double resultDisplayElapsed, resultDisplayUpdatedAt;

        private void ResetResultAdvance()
        {
            resultViewers.Clear();
            resultDisplayElapsed = resultDisplayUpdatedAt = 0d;
        }

        private void UpdateResultAdvance(double now)
        {
            double elapsed = resultDisplayUpdatedAt > 0d ? Math.Max(0d, now - resultDisplayUpdatedAt) : 0d;
            resultDisplayUpdatedAt = now;
            if (state.phase != GameFlowPhase.StageResult || IsTerritory) return;
            // Count result-display time only after everyone is ready. Any participant may inspect the gourd.
            if (!CanAdvanceStage || IsViewerOpen || resultViewers.Count > 0) return;
            resultDisplayElapsed += elapsed;
            if (resultDisplayElapsed >= StageResultDisplaySeconds) NextStage();
        }

        private void SetResultViewer(ulong playerId, bool open)
        {
            if (open) resultViewers.Add(playerId);
            else resultViewers.Remove(playerId);
        }

        private void NotifyResultViewer()
        {
            if (state.phase != GameFlowPhase.StageResult || IsTerritory || !network.IsConnected) return;
            if (network.IsHost) SetResultViewer(network.LocalPlayerId, IsViewerOpen);
            else network.SendGameFlowToHost(JsonUtility.ToJson(MakeControl("result_viewer")));
        }
    }
}
