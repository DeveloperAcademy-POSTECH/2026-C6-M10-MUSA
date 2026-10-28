using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hapio.Editor
{
    /// <summary>Explicitly apply the user's approved script; never runs on asset import.</summary>
    public static class StoryApprovedScriptBuilder
    {
        [Serializable] private class Script { public Frame[] frames; }
        [Serializable] private class Frame { public int number; public string cutId; public Page[] pages; public string direction; }
        [Serializable] private class Page { public string speaker, dialogue; public float seconds; }
        private const string Root = "Assets/Hapio/Story/Opening/";
        public static void Apply()
        {
            var script = JsonUtility.FromJson<Script>(File.ReadAllText(Root + "ApprovedScript.json"));
            var measureObject = new GameObject("Temporary dialogue measurement", typeof(RectTransform), typeof(Text));
            var measure = measureObject.GetComponent<Text>();
            measure.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/Shilla_Culture(M).otf");
            measure.fontSize = 21;
            var settings = measure.GetGenerationSettings(Vector2.zero);
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.generateOutOfBounds = true;
            settings.scaleFactor = 1;
            try
            {
                foreach (var frame in script.frames)
                {
                    string id = string.IsNullOrEmpty(frame.cutId) ? $"OP_{frame.number:000}" : frame.cutId;
                    var cut = AssetDatabase.LoadAssetAtPath<StoryCutDefinition>(Root + $"Cuts/{id}.asset");
                    var old = cut.pages;
                    cut.pages = frame.pages.Select((page, index) => new StoryDialoguePage {
                        speaker = page.speaker, dialogue = page.dialogue, autoReadSeconds = page.seconds,
                        voice = old != null && index < old.Count ? old[index].voice : null
                    }).ToList();
                    cut.speaker = frame.pages[0].speaker; cut.dialogue = frame.pages[0].dialogue;
                    cut.direction = frame.direction;
                    EditorUtility.SetDirty(cut);
                    string path = AssetDatabase.GetAssetPath(cut.viewPrefab);
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var view = root.GetComponent<StoryCutView>();
                        view.hideEmptyDialogue = true;
                        // Reapplying the script must preserve layout edited in Unity.
                        if (!view.overrideDialogueLayout)
                            view.dialogueRect = new Rect(84, 720, 270, 100);
                        view.overrideDialogueLayout = true;
                        float size = frame.pages.Any(page => !string.IsNullOrEmpty(page.dialogue) &&
                            page.dialogue.Count(character => character == '\n') >= 2) ? 18f : 20.696f;
                        foreach (var page in frame.pages)
                        {
                            if (string.IsNullOrEmpty(page.dialogue) || !page.dialogue.Contains("\n")) continue;
                            foreach (string line in page.dialogue.Split('\n'))
                            {
                                float width = measure.cachedTextGeneratorForLayout.GetPreferredWidth(line, settings);
                                if (width > 0) size = Mathf.Min(size, 21f * 268f / width);
                            }
                        }
                        view.dialogueFontSize = Mathf.Floor(size * 100) / 100;
                        view.speakerRect = new Rect(57, 721, 91, 18);
                        view.speakerBadgeRect = new Rect(57, 713, 91, 33);
                        view.speakerBadge = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Hapio/Art/Story/Opening/OP_002/SpeakerBadge.png");
                        view.speakerColor = Color.white;
                        view.centerSpeakerName = true;
                        if (frame.number == 14) AddImpact(root.transform, view);
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
            }
            finally { Object.DestroyImmediate(measureObject); }
            RemoveTitleSubtitle();
            AssetDatabase.SaveAssets();
            Debug.Log("HAPIO_APPROVED_SCRIPT_READY: opening script applied, centred updated names, two dialogue pages on cut 17, silent cuts 13-14, no title subtitle.");
        }

        private static void AddImpact(Transform root, StoryCutView view)
        {
            if (root.Find("Screen Effect Placement") != null) return;
            var layer = StoryWorkspaceBuilder.Layer(root, "Screen Effect", new Rect(105, 510, 270, 85), null,
                .15f, .25f, Vector2.zero, .5f);
            Object.DestroyImmediate(layer.artwork.gameObject); layer.artwork = null;
            var rect = StoryWorkspaceBuilder.Rect("와장창!!", layer.transform, new Rect(0, 0, 270, 85));
            rect.localRotation = Quaternion.Euler(0, 0, -10);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = "와장창!!";
            text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/ShillaCultureBold.otf");
            text.fontSize = 42; text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1, .88f, .64f); text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.12f, .07f, .03f, 1); outline.effectDistance = new Vector2(2, -2);
            view.layers = (view.layers ?? new StoryLayer[0]).Concat(new[] {layer}).ToArray();
        }

        private static void RemoveTitleSubtitle()
        {
            var scene = SceneManager.GetSceneByPath(StoryWorkspaceBuilder.PreviewScene);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(StoryWorkspaceBuilder.PreviewScene, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save the preview scene before removing the subtitle.");
                var gate = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<StoryOpeningTitle>(true)).Single();
                var hint = gate.beginButton.transform.Find("Tap Hint");
                if (hint != null) Object.DestroyImmediate(hint.gameObject);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            const string lobby = "Assets/Hapio/Art/UI/GameLobby.prefab";
            var home = PrefabUtility.LoadPrefabContents(lobby);
            try
            {
                var view = home.GetComponentInChildren<GameLobbyView>(true);
                var prompt = view.titlePage.transform.Find("SealPrompt");
                if (prompt != null) prompt.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(home, lobby);
            }
            finally { PrefabUtility.UnloadPrefabContents(home); }
        }
    }
}
