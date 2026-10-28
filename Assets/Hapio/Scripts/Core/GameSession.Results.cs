using System.Collections.Generic;
using UnityEngine;

namespace Hapio
{
    public sealed partial class GameSession
    {
        private readonly HashSet<ulong> resultViewers = new HashSet<ulong>();
        public bool IsAnyResultViewerOpen => IsViewerOpen || resultViewers.Count > 0;

        private void ResetResultAdvance()
        {
            resultViewers.Clear();
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
