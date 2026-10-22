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

    /// <summary>Story is the three-yokai run; Territory is one 2:2 team territory battle (#281). Numbers are serialized.</summary>
    public enum GameMode { Story, Territory }

    /// <summary>Run progress, independent of the Lab battle model's session and round.</summary>
    [Serializable]
    public sealed class GameFlowState
    {
        public string runId = string.Empty;
        public uint revision;
        public int stageIndex;
        public int totalStages = GameRunFlow.StageCount;
        public GameMode mode;
        public GameFlowPhase phase = GameFlowPhase.Home;
        public string endReason = string.Empty;
        public string battleSessionId = string.Empty;
        public uint battleRound;
        /// <summary>2:2 (#281): why everyone came back to the lobby, shown there. Empty in every other phase.</summary>
        public string notice = string.Empty;

        public GameFlowState Copy()
        {
            return (GameFlowState)MemberwiseClone();
        }

        public bool IsRunActive => phase == GameFlowPhase.LoadingBattle
            || phase == GameFlowPhase.Playing || phase == GameFlowPhase.StageResult;
    }
}
