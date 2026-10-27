using System;
using System.Linq;
using C6Lab;
using Hapio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6Lab.Editor
{
    /// <summary>Retire crack-only bindings without rebuilding or repositioning the saved UI.</summary>
    public static class LabPaintOnlyUpgrade
    {
        [MenuItem("Hapio/Upgrade Paint Only Bindings")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Stop Play mode and finish compilation before updating bindings.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or preserve unsaved Scene changes before updating bindings.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in new[] { "Assets/Lab/Scenes/Lab.unity", GameScenePaths.Battle,
                    GameScenePaths.Home, GameScenePaths.Lobby })
                {
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    bool changed = false;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        // Remove finish first: it has RequireComponent on the retired crack presenter.
                        foreach (string name in new[] { "C6Lab.LabGourdFinish", "C6Lab.LabGourdCrack" })
                        {
                            Type type = Type.GetType(name + ", C6Lab.Runtime");
                            if (type == null) continue;
                            foreach (Component component in root.GetComponentsInChildren(type, true))
                            { UnityEngine.Object.DestroyImmediate(component); changed = true; }
                        }
                        foreach (LabDeveloperMode dev in root.GetComponentsInChildren<LabDeveloperMode>(true))
                            changed |= RetireRow(dev.valueInputs, dev.valueSliders);
                        foreach (GameScreen screen in root.GetComponentsInChildren<GameScreen>(true))
                            changed |= RetireRow(screen.developerInputs, screen.developerSliders);
                    }
                    if (changed && !EditorSceneManager.SaveScene(scene))
                        throw new InvalidOperationException("Failed to save paint-only bindings: " + path);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("C6_PAINT_ONLY_BINDINGS_SAVED: retired crack components and DEV row; existing scene layout retained.");
        }

        private static bool RetireRow(UnityEngine.UI.InputField[] inputs, UnityEngine.UI.Slider[] sliders)
        {
            const int index = 79;
            if (inputs == null || inputs.Length <= index || inputs[index] == null) return false;
            Transform row = inputs[index].transform.parent;
            if (row == null) return false;
            foreach (UnityEngine.UI.Text text in row.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                if (text.text != null && (text.text.Contains("Gourd Mode") || text.text.Contains("CRACK")))
                    text.text = LabConfig.DeveloperFields[index].Label;
            inputs[index].SetTextWithoutNotify("0");
            inputs[index].interactable = false;
            if (sliders != null && sliders.Length > index && sliders[index] != null)
            {
                sliders[index].minValue = sliders[index].maxValue = 0f;
                sliders[index].SetValueWithoutNotify(0f);
                sliders[index].interactable = false;
            }
            row.gameObject.SetActive(false);
            return true;
        }
    }
}
