using System;
using System.IO;
using C6Lab;
using UnityEditor;
using UnityEngine;

namespace Hapio.Editor
{
    /// <summary>Explicit, idempotent creation of separate game assets. Never opens or saves Lab.</summary>
    public static class GameStageAssets
    {
        public const string CatalogPath = "Assets/Hapio/Resources/Hapio/StageCatalog.asset";
        [MenuItem("Hapio/Upgrade Three Yokai Stages")]
        public static void Upgrade()
        {
            EnsureAssets();
            GameSceneBuilder.UpgradeScenesForCurrentLab();
            AssetDatabase.SaveAssets();
            Debug.Log("HAPIO_THREE_YOKAI_READY: Gumiho -> Yaguang -> Jigwi; original Lab preserved.");
        }
        public static void EnsureAssets()
        {
            Folder("Assets/Hapio/Resources/Hapio/Stages");
            Folder("Assets/Hapio/Art");
            var catalog = AssetDatabase.LoadAssetAtPath<GameStageCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<GameStageCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            string[] ids = { "gumiho", "yaguang", "jigwi" };
            string[] names = { "구미호", "야광귀", "지귀" };
            var definitions = new GameStageDefinition[3];
            for (int i = 0; i < 3; i++)
            {
                string path = "Assets/Hapio/Resources/Hapio/Stages/Stage0" + (i + 1) + "_" + ids[i] + ".asset";
                var definition = AssetDatabase.LoadAssetAtPath<GameStageDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<GameStageDefinition>();
                    definition.stageIndex = i + 1; definition.stageId = ids[i]; definition.displayName = names[i];
                    definition.yokai = (LabStageYokai)(i + 1);
                    definition.defaultValues = i == 1 ? YaguangDefaults()
                        : Array.Empty<GameStageValue>();
                    AssetDatabase.CreateAsset(definition, path);
                }
                // Every stage uses the animated black yokai already inside WhiteGourd.
                // Clear old per-stage overrides so an upgrade cannot bring the fox back.
                if (definition.yokaiVisual != null)
                {
                    definition.yokaiVisual = null;
                    EditorUtility.SetDirty(definition);
                }
                definitions[i] = definition;
            }
            catalog.stages = definitions; catalog.Validate(); EditorUtility.SetDirty(catalog);
            // Save only the assets this updater owns, preserving unrelated Inspector drafts.
            foreach (var definition in definitions) AssetDatabase.SaveAssetIfDirty(definition);
            AssetDatabase.SaveAssetIfDirty(catalog);
            // Existing session assets retain GUIDs and all transport fields; catalog is loaded by its explicit resource path.
        }
        private static GameStageValue[] YaguangDefaults() => new[]
        {
            new GameStageValue(155, 1f), new GameStageValue(184, 1f), new GameStageValue(191, 1f),
            new GameStageValue(164, 4f), new GameStageValue(158, 13f),
            new GameStageValue(159, 8f), new GameStageValue(162, 8f),
            new GameStageValue(188, 3f), new GameStageValue(189, 11f), new GameStageValue(190, 1f),
            new GameStageValue(211, 120f), new GameStageValue(212, 100f),
            new GameStageValue(213, 10f), new GameStageValue(214, 0f),
            new GameStageValue(215, 35f), new GameStageValue(216, 1f),
            new GameStageValue(217, 60f), new GameStageValue(218, 0f),
            new GameStageValue(219, 75f), new GameStageValue(220, 1f),
            new GameStageValue(221, 25f), new GameStageValue(222, 40f),
            new GameStageValue(223, 60f), new GameStageValue(224, 80f)
        };
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
