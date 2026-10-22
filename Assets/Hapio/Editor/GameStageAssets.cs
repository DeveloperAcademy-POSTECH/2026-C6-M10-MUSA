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
        public const string VisualPath = "Assets/Hapio/Art/GumihoVisual.prefab";
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
                    definition.defaultValues = i == 0 ? new[] { new GameStageValue(166, 1f) }
                        : i == 1 ? new[] { new GameStageValue(155, 1f), new GameStageValue(184, 1f), new GameStageValue(191, 1f) }
                        : Array.Empty<GameStageValue>();
                    AssetDatabase.CreateAsset(definition, path);
                }
                definitions[i] = definition;
            }
            definitions[0].yokaiVisual = EnsureGumihoVisual();
            EditorUtility.SetDirty(definitions[0]);
            catalog.stages = definitions; catalog.Validate(); EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            // Existing session assets retain GUIDs and all transport fields; catalog is loaded by its explicit resource path.
        }
        private static GameObject EnsureGumihoVisual()
        {
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
            if (saved != null)
            {
                // Clean an existing wrapper in place; its GUID and references must survive an upgrade.
                GameObject contents = PrefabUtility.LoadPrefabContents(VisualPath);
                try
                {
                    if (RemoveAuthoringViewComponents(contents)) PrefabUtility.SaveAsPrefabAsset(contents, VisualPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                return AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lab/Art/Monster/ninetailedfox.fbx");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Lab/Art/Monster/Gumiho_Mat.Mat");
            if (model == null || material == null) throw new InvalidOperationException("The saved Gumiho model/material is missing.");
            var wrapper = new GameObject("Gumiho Decorative Visual");
            try
            {
                var child = (GameObject)PrefabUtility.InstantiatePrefab(model);
                child.transform.SetParent(wrapper.transform, false);
                foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    renderer.sharedMaterials = materials;
                }
                // These are decorations only. The existing WhiteGourd owns physical hits, painting, rope and rotation.
                foreach (Collider collider in wrapper.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (Rigidbody body in wrapper.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
                foreach (Animator animation in wrapper.GetComponentsInChildren<Animator>(true)) animation.enabled = false;
                foreach (Animation animation in wrapper.GetComponentsInChildren<Animation>(true)) animation.enabled = false;
                RemoveAuthoringViewComponents(wrapper);
                return PrefabUtility.SaveAsPrefabAsset(wrapper, VisualPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(wrapper); }
        }

        private static bool RemoveAuthoringViewComponents(GameObject root)
        {
            bool changed = false;
            // A removed component on a nested imported FBX is not reliably persisted as an
            // override. Unpack only this wrapper's instances, keeping mesh/material references
            // to the source assets, before saving the sanitized wrapper at the same path.
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root.transform || !PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)) continue;
                PrefabUtility.UnpackPrefabInstance(child.gameObject, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
                changed = true;
            }
            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) continue;
                string type = component.GetType().FullName;
                if (type != "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData"
                    && type != "UnityEngine.Rendering.Universal.UniversalAdditionalLightData") continue;
                component.enabled = false;
                UnityEngine.Object.DestroyImmediate(component);
                changed = true;
            }
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            { camera.enabled = false; UnityEngine.Object.DestroyImmediate(camera); changed = true; }
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            { light.enabled = false; UnityEngine.Object.DestroyImmediate(light); changed = true; }
            foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
            { listener.enabled = false; UnityEngine.Object.DestroyImmediate(listener); changed = true; }
            return changed;
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
