using System;

namespace Hapio
{
    public enum GameFlowPhase
    {
        Home,
        Lobby,
        LoadingBattle,
        Playing,
        StageResult,
        Completed,
        GameOver
    }

    /// <summary>Run progress, independent of the Lab battle model's session and round.</summary>
    [Serializable]
    public sealed class GameFlowState
    {
        public string runId = string.Empty;
        public uint revision;
        public int stageIndex;
        public int totalStages = GameRunFlow.StageCount;
        public GameFlowPhase phase = GameFlowPhase.Home;
        public string endReason = string.Empty;
        public string battleSessionId = string.Empty;
        public uint battleRound;

        public GameFlowState Copy()
        {
            return (GameFlowState)MemberwiseClone();
        }

        public bool IsRunActive => phase == GameFlowPhase.LoadingBattle
            || phase == GameFlowPhase.Playing || phase == GameFlowPhase.StageResult;
    }
}
