using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace C6Lab.Editor
{
    /// <summary>Adds only the eight editable paint-pattern sample children to the saved Canvas.</summary>
    public static class LabPlayerPaintPatternUiUpgrade
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";
        private const string SampleName = "PaintPatternSample";

        [MenuItem("C6 Lab/Upgrade Player Paint Pattern Samples")]
        public static void UpgradeScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before updating the saved Canvas.");
            Scene current = SceneManager.GetActiveScene();
            if (current.IsValid() && current.isDirty)
                throw new InvalidOperationException("Save the open Scene before adding paint-pattern samples.");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LabHud hud = UnityEngine.Object.FindFirstObjectByType<LabHud>();
            if (hud == null || hud.GetComponent<Canvas>() == null
                || hud.playerSlotButtons == null || hud.playerSlotButtons.Length != LabModel.MaximumParticipants
                || hud.localPlayerCircle == null || hud.leftPlayerCircle == null || hud.rightPlayerCircle == null)
                throw new InvalidOperationException("The saved Lab Canvas is missing player slot or circle references.");
            foreach (Button button in hud.playerSlotButtons)
                if (button == null || !(button.transform is RectTransform))
                    throw new InvalidOperationException("All five saved P buttons are required.");

            var serialized = new SerializedObject(hud);
            SerializedProperty slots = serialized.FindProperty(nameof(LabHud.playerSlotPatternSamples));
            slots.arraySize = LabModel.MaximumParticipants;
            for (int i = 0; i < LabModel.MaximumParticipants; i++)
                slots.GetArrayElementAtIndex(i).objectReferenceValue = EnsureSample(
                    hud.playerSlotButtons[i].transform, new Vector2(.5f, .83f), 18f);
            serialized.FindProperty(nameof(LabHud.localPlayerPatternSample)).objectReferenceValue = EnsureSample(
                hud.localPlayerCircle.transform, new Vector2(.83f, .17f), 18f);
            serialized.FindProperty(nameof(LabHud.leftPlayerPatternSample)).objectReferenceValue = EnsureSample(
                hud.leftPlayerCircle.transform, new Vector2(.83f, .17f), 18f);
            serialized.FindProperty(nameof(LabHud.rightPlayerPatternSample)).objectReferenceValue = EnsureSample(
                hud.rightPlayerCircle.transform, new Vector2(.83f, .17f), 18f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity could not save the paint-pattern sample references.");
            Debug.Log("C6_LAB_PLAYER_PAINT_PATTERN_UI_SAVED: five P buttons and three player circles are bound.");
        }

        private static Image EnsureSample(Transform parent, Vector2 anchor, float size)
        {
            Transform saved = parent.Find(SampleName);
            if (saved != null)
            {
                Image existing = saved.GetComponent<Image>();
                if (existing == null || !(saved is RectTransform))
                    throw new InvalidOperationException(parent.name + "/" + SampleName + " must be a UI Image.");
                return existing;
            }

            var node = new GameObject(SampleName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            node.layer = parent.gameObject.layer;
            var rect = node.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.one * size;
            Image image = node.GetComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.type = Image.Type.Simple;
            // The fixed sprite cache is assigned by LabHud at runtime. Never serialize
            // a transient generated Sprite reference into the Scene.
            image.enabled = false;
            return image;
        }
    }
}
