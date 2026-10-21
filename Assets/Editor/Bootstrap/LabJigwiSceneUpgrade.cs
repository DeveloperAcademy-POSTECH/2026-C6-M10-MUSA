using System;
using System.Linq;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6Lab.Editor
{
    /// <summary>Add only the Jigwi controls and new serialized defaults to the existing Lab.</summary>
    public static class LabJigwiSceneUpgrade
    {
        [MenuItem("C6 Lab/Upgrade Jigwi Controls")]
        public static void Apply()
        {
            const string path = "Assets/Lab/Scenes/Lab.unity";
            Scene prior = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(path);
            bool owned = !scene.IsValid() || !scene.isLoaded;
            if (!owned && scene.isDirty) throw new InvalidOperationException("Save Lab before adding Jigwi controls.");
            if (owned) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var network = roots.SelectMany(item => item.GetComponentsInChildren<LabNetwork>(true)).Single();
                var hud = roots.SelectMany(item => item.GetComponentsInChildren<LabHud>(true)).Single();
                Canvas canvas = hud.GetComponentInParent<Canvas>();
                LabJigwiHud jigwi = canvas.GetComponentInChildren<LabJigwiHud>(true);
                if (jigwi == null) jigwi = canvas.gameObject.AddComponent<LabJigwiHud>();
                jigwi.network = network;
                jigwi.canvas = canvas;
                jigwi.EnsureWidgets();
                EditorUtility.SetDirty(jigwi);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Could not save Jigwi controls.");
                LabConfig config = AssetDatabase.LoadAssetAtPath<LabConfig>("Assets/Lab/Resources/LabConfig.asset");
                if (config == null) throw new InvalidOperationException("Saved LabConfig is missing.");
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssetIfDirty(config);
                Debug.Log("C6_LAB_JIGWI_SCENE_READY: existing Scene objects and Lab build entry preserved.");
            }
            finally
            {
                if (owned && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (prior.IsValid() && prior.isLoaded) SceneManager.SetActiveScene(prior);
            }
        }
    }
}
