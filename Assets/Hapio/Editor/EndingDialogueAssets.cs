using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio.Editor
{
    /// <summary>Explicit update from the 2026-11-15 Figma ending dialogue; preserves artwork and voice clips.</summary>
    public static class EndingDialogueAssets
    {
        public const string PaperPath = "Assets/Hapio/Art/Story/Opening/Revision28/b24cb.png";
        public const string BadgePath = "Assets/Hapio/Art/Story/Opening/Imported/9769e.png";
        [MenuItem("Hapio/Story/Update Ending Dialogue")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Stop playback and finish compilation first.");
            var preview = SceneManager.GetSceneByPath(StageStoryAssets.Preview);
            if (preview.IsValid() && preview.isDirty)
                throw new InvalidOperationException("Save the story preview before updating its dialogue reference.");

            ApplyCut("Ending_01", new Rect(81, 745, 237, 46)); // 4210:3318, 5346:6910
            ApplyCut("Ending_02", new Rect(81, 757, 148, 23)); // 4210:3333
            ApplyCut("Ending_05", new Rect(81, 757, 214, 23)); // 4210:3358
            var last = AssetDatabase.LoadAssetAtPath<StoryCutDefinition>(StageStoryAssets.Root + "/Cuts/Ending_05.asset");
            last.pages[0].dialogue = "이건... 우리가 깬게 아닌데...?";
            EditorUtility.SetDirty(last);
            AssetDatabase.SaveAssetIfDirty(last);

            var canvas = PrefabUtility.LoadPrefabContents(GameStageStoryUpgrade.CanvasPrefab);
            try
            {
                ConnectPaper(canvas.GetComponent<StoryPlayer>());
                PrefabUtility.SaveAsPrefabAsset(canvas, GameStageStoryUpgrade.CanvasPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(canvas); }

            bool opened = !preview.IsValid() || !preview.isLoaded;
            var previous = SceneManager.GetActiveScene();
            if (opened) preview = EditorSceneManager.OpenScene(StageStoryAssets.Preview, OpenSceneMode.Additive);
            try
            {
                var player = preview.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<StoryPlayer>(true)).Single();
                ConnectPaper(player);
                EditorSceneManager.MarkSceneDirty(preview);
                EditorSceneManager.SaveScene(preview);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(preview, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static void ConnectPaper(StoryPlayer player)
        {
            player.dialoguePaper = player.dialogueGroup.transform.Find("Dialogue Paper").GetComponent<UnityEngine.UI.Image>();
            player.speakerPalette = AssetDatabase.LoadAssetAtPath<StorySpeakerPalette>("Assets/Hapio/Story/Opening/SpeakerPalette.asset");
            EditorUtility.SetDirty(player);
        }

        private static void ApplyCut(string id, Rect body)
        {
            string path = StageStoryAssets.Root + "/Views/" + id + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<StoryCutView>();
                view.overrideDialogueLayout = true;
                view.dialoguePaper = AssetDatabase.LoadAssetAtPath<Sprite>(PaperPath);
                view.dialoguePaperRect = new Rect(11, 677, 379, 181);
                view.dialogueRect = body;
                view.dialogueFontSize = 20;
                view.dialogueNoWrap = true;
                view.speakerBadge = AssetDatabase.LoadAssetAtPath<Sprite>(BadgePath);
                view.speakerBadgeRect = new Rect(57, 713, 79.3400879f, 29.0109921f);
                view.speakerRect = new Rect(57, 720.032968f, 79.3400879f, 15);
                view.speakerFontSize = 13;
                view.centerSpeakerName = true;
                view.hideEmptyDialogue = true;
                if (view.dialoguePaper == null || view.speakerBadge == null)
                    throw new InvalidOperationException("Missing original Figma dialogue art.");
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
