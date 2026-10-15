using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6Lab.Editor
{
    /// <summary>Connect the existing gourd presentation to the current Lab without replacing its layout.</summary>
    public static class LabGourdMainSceneUpgrade
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";
        private const string PrefabPath = "Assets/Lab/Art/Target2/Prefabs/Target2_Intact.prefab";
        private const string MaterialPath = "Assets/Lab/Art/GourdCrack/GourdCrack.mat";

        [MenuItem("C6 Lab/Apply Gourd To Main Scene")]
        public static void Apply()
        {
            Scene scene = RequireScene();
            LabNetwork network = RequireNetwork(scene);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            RequireAssets(prefab, material);

            GameObject target = network.target.gameObject;
            if (target.GetComponents<LabGourdCrack>().Length > 1
                || target.GetComponents<LabGourdFinish>().Length > 1)
                throw new InvalidOperationException("Remove duplicate gourd presentation components before applying.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Connect Gourd To Main Lab");
            LabGourdCrack crack = target.GetComponent<LabGourdCrack>();
            if (crack == null) crack = Undo.AddComponent<LabGourdCrack>(target);
            Undo.RecordObject(crack, "Connect Gourd To Main Lab");
            crack.network = network;
            crack.target = network.target;
            crack.gourdPrefab = prefab;
            crack.crackMaterial = material;
            EditorUtility.SetDirty(crack);
            if (target.GetComponent<LabGourdFinish>() == null)
                Undo.AddComponent<LabGourdFinish>(target);

            ValidateBindings(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity could not save the main Lab gourd references.");
            Undo.CollapseUndoOperations(group);
            Debug.Log("C6_LAB_MAIN_GOURD_SAVED: existing Lab Target connected to gourd/cracks/finish; UI, board, hitbox and hanging components preserved.");
        }

        [MenuItem("C6 Lab/Validate Main Gourd References")]
        public static void Validate()
        {
            ValidateBindings(RequireScene());
            Debug.Log("C6_LAB_MAIN_GOURD_VALID: main Lab target, network, gourd prefab, crack material and finish references agree.");
        }

        private static void ValidateBindings(Scene scene)
        {
            LabNetwork network = RequireNetwork(scene);
            LabTarget target = network.target;
            LabGourdCrack[] cracks = target.GetComponents<LabGourdCrack>();
            if (cracks.Length != 1 || target.GetComponents<LabGourdFinish>().Length != 1
                || !cracks[0].enabled || !target.GetComponent<LabGourdFinish>().enabled)
                throw new InvalidOperationException("The main Lab Target needs one enabled crack and finish component.");
            LabGourdCrack crack = cracks[0];
            if (crack.network != network || crack.target != target
                || crack.gourdPrefab != AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)
                || crack.crackMaterial != AssetDatabase.LoadAssetAtPath<Material>(MaterialPath))
                throw new InvalidOperationException("Gourd presentation must reference this Lab's network/target and the existing art assets.");
            RequireAssets(crack.gourdPrefab, crack.crackMaterial);
        }

        private static LabNetwork RequireNetwork(Scene scene)
        {
            LabNetwork[] networks = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LabNetwork>(true)).ToArray();
            if (networks.Length != 1 || networks[0].target == null)
                throw new InvalidOperationException("The main Lab must have one connected network and Target.");
            LabTarget target = networks[0].target;
            if (target.gameObject.scene != scene || target.Hitbox == null || target.VisualMount == null
                || target.GetComponent<LabHangingTarget>() == null)
                throw new InvalidOperationException("Keep the existing Lab Target hitbox, Visual Mount and hanging system.");
            return networks[0];
        }

        private static void RequireAssets(GameObject prefab, Material material)
        {
            if (prefab == null || material == null || material.shader == null
                || material.shader.name != "C6Lab/GourdCrack" || !material.HasProperty("_CrackStage"))
                throw new InvalidOperationException("Existing gourd prefab and crack shader material are required.");
            if (!prefab.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Any(item => item != null && item.name.Contains("Shell")))
                throw new InvalidOperationException("The gourd prefab must contain its Shell renderer material slots.");
        }

        private static Scene RequireScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before saving main Lab gourd references.");
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
                throw new InvalidOperationException("Open the current Assets/Lab/Scenes/Lab.unity first.");
            return scene;
        }
    }
}
