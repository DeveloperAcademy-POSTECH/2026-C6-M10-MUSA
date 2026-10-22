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
                for (int j = 0; j < i; j++) if (stages[j].stageId == stage.stageId)
                    throw new InvalidOperationException("Stage identities must be unique.");
                var seen = new System.Collections.Generic.HashSet<int>();
                foreach (GameStageValue value in stage.defaultValues ?? Array.Empty<GameStageValue>())
                    if (!GameStageSettings.IsFieldVisible(i + 1, value.index) || !seen.Add(value.index))
                        throw new InvalidOperationException("Stage defaults may only override their own unique fields once.");
            }
        }
    }
}
