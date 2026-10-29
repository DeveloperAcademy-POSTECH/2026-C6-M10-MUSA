using System;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    [CreateAssetMenu(menuName = "Hapio/Stage Catalog")]
    public sealed class GameStageCatalog : ScriptableObject
    {
        public const string ResourcePath = "Hapio/StageCatalog";
        public GameStageDefinition[] stages = Array.Empty<GameStageDefinition>();
        [Tooltip("2:2 땅따먹기 (#305)가 Common 대신 쓰는 값. 스태미나·생성·구슬 수명과 2:2 시간·피버만 넣을 수 있다.")]
        public GameStageValue[] territoryDefaultValues = Array.Empty<GameStageValue>();
        public static GameStageCatalog RequireDefault()
        {
            var catalog = Resources.Load<GameStageCatalog>(ResourcePath);
            if (catalog == null) throw new InvalidOperationException("The saved Hapio StageCatalog is missing.");
            catalog.Validate();
            return catalog;
        }
        public GameStageDefinition Get(int stageIndex)
        {
            Validate();
            if (stageIndex < 1 || stageIndex > stages.Length) throw new ArgumentOutOfRangeException(nameof(stageIndex));
            return stages[stageIndex - 1];
        }
        public void Validate()
        {
            if (stages == null || stages.Length != 3) throw new InvalidOperationException("Exactly three Hapio stages are required.");
            for (int i = 0; i < stages.Length; i++)
            {
                var stage = stages[i];
                if (stage == null || stage.stageIndex != i + 1 || string.IsNullOrWhiteSpace(stage.stageId)
                    || string.IsNullOrWhiteSpace(stage.displayName) || stage.yokai != (LabStageYokai)(i + 1))
                    throw new InvalidOperationException("Stage order must be Gumiho, Yaguang, Jigwi with explicit identities.");
                // Authoring a future yokai is not a runnable combat implementation. Keep
                // old stage fixtures compatible while checking every supplied content link.
                if (stage.content != null)
                {
                    stage.content.Validate();
                    if (stage.content.contentId != stage.stageId || !stage.content.IsIntegrated)
                        throw new InvalidOperationException("A playable stage must reference its own integrated yokai content.");
                }
                for (int j = 0; j < i; j++) if (stages[j].stageId == stage.stageId)
                    throw new InvalidOperationException("Stage identities must be unique.");
                var seen = new System.Collections.Generic.HashSet<int>();
                foreach (GameStageValue value in stage.defaultValues ?? Array.Empty<GameStageValue>())
                    if (!GameStageSettings.IsFieldVisible(i + 1, value.index) || !seen.Add(value.index))
                        throw new InvalidOperationException("Stage defaults may only override their own unique fields once.");
            }
            var territorySeen = new System.Collections.Generic.HashSet<int>();
            foreach (GameStageValue value in territoryDefaultValues ?? Array.Empty<GameStageValue>())
                if (!GameStageSettings.IsFieldVisible(GameStageSettings.TerritoryScope, value.index) || !territorySeen.Add(value.index))
                    throw new InvalidOperationException("2:2 defaults may only override the territory room's own unique fields once.");
        }
    }
}
