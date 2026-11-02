using System;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>Switches only authored scenery; gameplay and camera objects stay shared.</summary>
    [DefaultExecutionOrder(-450), DisallowMultipleComponent]
    public sealed class GameStageBackgrounds : MonoBehaviour
    {
        [Serializable]
        public sealed class BackgroundSet
        {
            public LabBackgroundStudio studio;
            public GameObject ground;
            public GameObject boardBacking;

            public void SetVisible(bool visible)
            {
                if (studio != null) studio.gameObject.SetActive(visible);
                if (ground != null) ground.SetActive(visible);
                if (boardBacking != null) boardBacking.SetActive(visible);
            }
        }

        [Serializable]
        public sealed class StageOverride
        {
            [Min(1)] public int stageIndex = 2;
            public BackgroundSet background = new BackgroundSet();
        }

        public BackgroundSet defaultBackground = new BackgroundSet();
        public StageOverride[] stageOverrides = Array.Empty<StageOverride>();
        public int AppliedStage { get; private set; }
        private GameSession session;

        private void Awake()
        {
            session = GameSession.Instance;
            if (session != null) session.Changed += RefreshFromSession;
            RefreshFromSession();
        }

        private void RefreshFromSession()
        {
            ApplyStage(session != null && !session.IsTerritory ? session.State.stageIndex : 1);
        }

        public BackgroundSet Resolve(int stageIndex)
        {
            foreach (StageOverride entry in stageOverrides)
                if (entry != null && entry.stageIndex == stageIndex
                    && entry.background != null && entry.background.studio != null)
                    return entry.background;
            return defaultBackground;
        }

        public void ApplyStage(int stageIndex)
        {
            BackgroundSet chosen = Resolve(stageIndex);
            defaultBackground?.SetVisible(ReferenceEquals(chosen, defaultBackground));
            foreach (StageOverride entry in stageOverrides)
                if (entry != null && entry.background != null && !ReferenceEquals(entry.background, defaultBackground))
                    entry.background.SetVisible(ReferenceEquals(chosen, entry.background));
            AppliedStage = stageIndex;
        }

        private void OnDestroy()
        {
            if (session != null) session.Changed -= RefreshFromSession;
        }
    }
}
