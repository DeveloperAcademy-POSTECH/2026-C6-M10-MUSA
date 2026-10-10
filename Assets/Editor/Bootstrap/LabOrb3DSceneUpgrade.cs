using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6Lab.Editor
{
    /// <summary>Updates the saved scene in place; it never rebuilds the designer's Canvas.</summary>
    public static class LabOrb3DSceneUpgrade
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";
        private const int BoardLayer = 8;
        private const string BoardLayerName = "LabOrbBoard";

        [MenuItem("C6 Lab/Upgrade Saved 3D Orb Board")]
        public static void UpgradeSavedScene()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Lab Scene is missing.", ScenePath);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject board = GameObject.Find("OrbBoard");
            Camera orbCamera = GameObject.Find("OrbCamera")?.GetComponent<Camera>();
            Camera battleCamera = GameObject.Find("BattleCamera")?.GetComponent<Camera>();
            if (board == null || orbCamera == null || battleCamera == null)
                throw new InvalidOperationException("The saved orb board or its cameras are missing.");
            Configure(board, orbCamera, battleCamera);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the 3D orb board scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("C6_LAB_3D_ORB_SCENE_SAVED layer=" + BoardLayerName);
        }

        public static void Configure(GameObject board, Camera orbCamera, Camera battleCamera)
        {
            if (board == null || orbCamera == null || battleCamera == null)
                throw new ArgumentNullException("Board and both cameras are required.");
            EnsureBoardLayer();
            board.layer = BoardLayer;
            // The lower camera sees only board spheres. The upper camera never renders them.
            orbCamera.cullingMask = 1 << BoardLayer;
            battleCamera.cullingMask &= ~(1 << BoardLayer);
            EditorUtility.SetDirty(board);
            EditorUtility.SetDirty(orbCamera);
            EditorUtility.SetDirty(battleCamera);
        }

        private static void EnsureBoardLayer()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0) throw new InvalidOperationException("Unity TagManager is unavailable.");
            var tags = new SerializedObject(assets[0]);
            SerializedProperty layers = tags.FindProperty("layers");
            if (layers == null || layers.arraySize <= BoardLayer)
                throw new InvalidOperationException("Unity layers are unavailable.");
            SerializedProperty slot = layers.GetArrayElementAtIndex(BoardLayer);
            if (!string.IsNullOrEmpty(slot.stringValue) && slot.stringValue != BoardLayerName)
                throw new InvalidOperationException("Layer 8 is already used: " + slot.stringValue);
            if (slot.stringValue == BoardLayerName) return;
            slot.stringValue = BoardLayerName;
            tags.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
