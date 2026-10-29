using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hapio.Editor
{
    /// <summary>Creates authoring-only yokai records without promoting planned content into gameplay.</summary>
    public static class GameYokaiCatalogValidator
    {
        public const string CatalogPath = "Assets/Hapio/Content/Yokai/YokaiCatalog.asset";
        private const string ContentRoot = "Assets/Hapio/Content/Yokai";
        private const string StageCatalogPath = "Assets/Hapio/Resources/Hapio/StageCatalog.asset";
        private const string StageRoot = "Assets/Hapio/Resources/Hapio/Stages/";
        private const string FigmaUrl = "https://www.figma.com/design/wW7ny7lNQ4DXNJ4kD46AuR/-C6--?node-id=";

        private sealed class Entry
        {
            public readonly string Id;
            public readonly string Folder;
            public readonly string Name;
            public readonly string Frame;
            public readonly bool Integrated;
            public readonly bool Boss;
            public readonly string[] Aliases;
            public string Path => ContentRoot + "/" + Folder + "/" + Folder + ".asset";

            public Entry(string id, string folder, string name, string frame, bool integrated = false,
                bool boss = false, params string[] aliases)
            {
                Id = id;
                Folder = folder;
                Name = name;
                Frame = frame;
                Integrated = integrated;
                Boss = boss;
                Aliases = aliases;
            }
        }

        // Reference order belongs to the design roster, not the three playable stage indices.
        private static readonly Entry[] Roster =
        {
            new Entry("gumiho", "Gumiho", "구미호", "2450-589", true),
            new Entry("jangsanbeom", "Jangsanbeom", "장산범", "2450-591"),
            new Entry("yaguang", "Yaguang", "야광귀", "2450-593", true),
            new Entry("geogugwi", "Geogugwi", "거구귀", "2450-595"),
            new Entry("eoduksini", "Eoduksini", "어둑시니", "2450-597"),
            new Entry("dueoksini", "Dueoksini", "두억시니", "2450-599"),
            new Entry("geuseundae", "Geuseundae", "그슨대", "2450-609", false, false, "그신새", "그신대"),
            new Entry("jigwi", "Jigwi", "지귀", "2450-611", true),
            new Entry("changgwi", "Changgwi", "창귀", "2450-613"),
            new Entry("jihagukdaejeok", "JihagukDaejeok", "지하국대적", "2450-615", false, true)
        };

        private static readonly string[] PlayableIds = { "gumiho", "yaguang", "jigwi" };
        private static readonly string[] StagePaths =
        {
            StageRoot + "Stage01_gumiho.asset",
            StageRoot + "Stage02_yaguang.asset",
            StageRoot + "Stage03_jigwi.asset"
        };

        [MenuItem("Hapio/Yokai/Create Missing Definitions")]
        public static void CreateMissingDefinitions()
        {
            // Complete path/type preflight before creating any asset or folder.
            LoadOptional<GameYokaiCatalog>(CatalogPath);
            CheckFolderPath(ContentRoot);
            foreach (Entry entry in Roster)
            {
                CheckFolderPath(ContentRoot + "/" + entry.Folder);
                LoadOptional<GameYokaiDefinition>(entry.Path);
            }
            GameStageCatalog stages = RequireStageLayout(false);

            EnsureFolder(ContentRoot);
            var definitions = new GameYokaiDefinition[Roster.Length];
            int created = 0;
            for (int i = 0; i < Roster.Length; i++)
            {
                Entry entry = Roster[i];
                definitions[i] = LoadOptional<GameYokaiDefinition>(entry.Path);
                if (definitions[i] != null) continue;

                EnsureFolder(ContentRoot + "/" + entry.Folder);
                var definition = ScriptableObject.CreateInstance<GameYokaiDefinition>();
                definition.contentId = entry.Id;
                definition.displayName = entry.Name;
                definition.aliases = (string[])entry.Aliases.Clone();
                definition.referenceOrder = i + 1;
                definition.description = string.Empty;
                definition.referenceUrl = FigmaUrl + entry.Frame;
                definition.isBoss = entry.Boss;
                definition.implementationState = entry.Integrated
                    ? GameYokaiImplementationState.Integrated : GameYokaiImplementationState.Planned;
                definition.Validate();
                CreateAsset(definition, entry.Path);
                definitions[i] = definition;
                created++;
            }

            if (LoadOptional<GameYokaiCatalog>(CatalogPath) == null)
            {
                var catalog = ScriptableObject.CreateInstance<GameYokaiCatalog>();
                catalog.entries = definitions;
                catalog.Validate();
                CreateAsset(catalog, CatalogPath);
                created++;
            }

            int linked = 0;
            for (int i = 0; i < stages.stages.Length; i++)
            {
                GameStageDefinition stage = stages.stages[i];
                if (stage.content != null) continue;
                GameYokaiDefinition definition = FindDefinition(definitions, stage.stageId);
                if (definition == null || !definition.IsIntegrated)
                    throw new InvalidOperationException("Cannot link an absent or non-integrated definition to " + StagePaths[i]);
                Undo.RecordObject(stage, "Link existing yokai content");
                stage.content = definition;
                EditorUtility.SetDirty(stage);
                AssetDatabase.SaveAssetIfDirty(stage);
                linked++;
            }
            Debug.Log($"Yokai scaffold: created {created} assets, linked {linked} existing stages. " +
                "Existing definitions and catalog entries were preserved. Run Validate Content Catalog to inspect saved data.");
        }

        [MenuItem("Hapio/Yokai/Validate Content Catalog")]
        public static void ValidateSavedCatalog()
        {
            if (Array.IndexOf(CatalogPath.Split('/'), "Resources") >= 0)
                throw new InvalidOperationException("The authoring catalog must remain outside Resources.");
            var catalog = LoadRequired<GameYokaiCatalog>(CatalogPath);
            catalog.Validate();
            if (catalog.entries.Length != Roster.Length)
                throw new InvalidOperationException("The authoring catalog must contain the ten reference yokai in reference order.");

            for (int i = 0; i < Roster.Length; i++)
            {
                Entry expected = Roster[i];
                GameYokaiDefinition definition = LoadRequired<GameYokaiDefinition>(expected.Path);
                if (catalog.entries[i] != definition || definition.contentId != expected.Id || definition.referenceOrder != i + 1)
                    throw new InvalidOperationException("Missing, misplaced or mismatched catalog entry: " + expected.Path);
                if (definition.displayName != expected.Name || definition.isBoss != expected.Boss)
                    throw new InvalidOperationException("Reference name/boss metadata differs for " + expected.Id);
                var expectedState = expected.Integrated
                    ? GameYokaiImplementationState.Integrated : GameYokaiImplementationState.Planned;
                if (definition.implementationState != expectedState)
                    throw new InvalidOperationException("Implementation state differs from the current three-playable/seven-planned scope: " + expected.Id);
                foreach (string alias in expected.Aliases)
                    if (Array.IndexOf(definition.aliases ?? Array.Empty<string>(), alias) < 0)
                        throw new InvalidOperationException("A documented reference alias is missing for " + expected.Id + ": " + alias);
            }

            GameStageCatalog stages = RequireStageLayout(true);
            for (int i = 0; i < stages.stages.Length; i++)
            {
                GameYokaiDefinition definition;
                if (!catalog.TryGet(PlayableIds[i], out definition) || stages.stages[i].content != definition)
                    throw new InvalidOperationException("Playable stage content must reference the matching saved definition: " + StagePaths[i]);
            }
            Debug.Log("Yokai content catalog validated: ten authoring records, seven planned, and the original three playable stages. " +
                "Optional art/audio/effect references may remain empty. This is asset validation, not a gameplay or device test.");
        }

        private static GameStageCatalog RequireStageLayout(bool requireContent)
        {
            GameStageCatalog catalog = LoadRequired<GameStageCatalog>(StageCatalogPath);
            catalog.Validate();
            if (catalog.stages.Length != PlayableIds.Length)
                throw new InvalidOperationException("The playable stage catalog must retain its original three entries.");
            for (int i = 0; i < PlayableIds.Length; i++)
            {
                GameStageDefinition stage = LoadRequired<GameStageDefinition>(StagePaths[i]);
                if (catalog.stages[i] != stage || stage.stageId != PlayableIds[i] || stage.stageIndex != i + 1)
                    throw new InvalidOperationException("The playable stage order/path/identity has changed at " + StagePaths[i]);
                if ((requireContent && stage.content == null) ||
                    (stage.content != null && (stage.content.contentId != stage.stageId || !stage.content.IsIntegrated)))
                    throw new InvalidOperationException("Playable stage content is missing, mismatched or not integrated: " + StagePaths[i]);
            }
            return catalog;
        }

        private static GameYokaiDefinition FindDefinition(GameYokaiDefinition[] definitions, string contentId)
        {
            foreach (GameYokaiDefinition definition in definitions)
                if (definition != null && definition.contentId == contentId) return definition;
            return null;
        }

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            T asset = LoadOptional<T>(path);
            if (asset == null) throw new InvalidOperationException("Required saved asset is missing: " + path);
            return asset;
        }

        private static T LoadOptional<T>(string path) where T : UnityEngine.Object
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing == null)
            {
                if (File.Exists(path) || Directory.Exists(path))
                    throw new InvalidOperationException("Asset path exists but is not an imported " + typeof(T).Name + ": " + path);
                return null;
            }
            var typed = existing as T;
            if (typed == null)
                throw new InvalidOperationException("Refusing to replace a different asset type at " + path);
            return typed;
        }

        private static void CheckFolderPath(string path)
        {
            if (path == "Assets") return;
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("..") || path.Contains("\\"))
                throw new InvalidOperationException("Only project-relative Assets folders are permitted: " + path);
            string parent = path.Substring(0, path.LastIndexOf('/'));
            CheckFolderPath(parent);
            if (!AssetDatabase.IsValidFolder(path) &&
                (File.Exists(path) || Directory.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path) != null))
                throw new InvalidOperationException("Folder path is occupied or not imported: " + path);
        }

        private static void EnsureFolder(string path)
        {
            if (path == "Assets" || AssetDatabase.IsValidFolder(path)) return;
            CheckFolderPath(path);
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, path.Substring(slash + 1))))
                throw new InvalidOperationException("Could not create authoring folder: " + path);
        }

        private static void CreateAsset(UnityEngine.Object asset, string path)
        {
            try
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(path) || Directory.Exists(path))
                    throw new InvalidOperationException("Refusing to replace an existing path: " + path);
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssetIfDirty(asset);
            }
            catch
            {
                if (!EditorUtility.IsPersistent(asset)) UnityEngine.Object.DestroyImmediate(asset);
                throw;
            }
        }
    }
}
