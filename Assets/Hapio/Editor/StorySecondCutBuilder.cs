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
    public static class StorySecondCutBuilder
    {
        private const string Root = StoryWorkspaceBuilder.Root;
        private const string Art = "Assets/Hapio/Art/Story/Opening/OP_002/";
        private const string ViewPath = Root + "/Opening/Views/OP_002.prefab";
        public const string PreviewPath = Root + "/Opening/OpeningPreview.asset";

        [MenuItem("Hapio/Story/Create Second Cut From Imported Art")]
        public static void CreateSecondCut()
        {
            foreach (string path in Directory.GetFiles(Art, "*.png")) StoryWorkspaceBuilder.ImportSprite(path);
            GameObject root = PrefabUtility.LoadPrefabContents(ViewPath);
            try
            {
                var view = root.GetComponent<StoryCutView>();
                if (view.GetComponentsInChildren<Image>(true).Any(image => image.sprite != null))
                    Debug.Log("Second cut already has artwork; existing layout and timing are preserved.");
                else
                {
                    while (root.transform.childCount > 0) Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
                    var layers = new List<StoryLayer>();
                    Add(root.transform, layers, "Background", new Rect(-45, 0, 492, 874), 0, .25f, 1);
                    var building = Add(root.transform, layers, "Building", new Rect(101, 116, 301, 409), .1f, .55f, .8f);
                    building.artwork.color = new Color(1, 1, 1, .9f);
                    Add(root.transform, layers, "TeacherAndRed", new Rect(49, 334, 226, 206), .3f, .7f, .08f);
                    Add(root.transform, layers, "Yellow", new Rect(1, 466, 128.018875f, 230), .5f, .7f, .08f, true);
                    Add(root.transform, layers, "Brown", new Rect(91, 531, 130, 186), .65f, .7f, .08f, true);
                    Add(root.transform, layers, "Blue", new Rect(284, 393, 126, 231), .8f, .7f, .08f);
                    Add(root.transform, layers, "Green", new Rect(219, 492, 127, 221), .95f, .7f, .08f);
                    // Small original vector marks travel with their associated figure's Motion.
                    AddOriginalDetails(root.transform);
                    view.layers = layers.ToArray();
                    view.overrideDialogueLayout = true;
                    view.dialogueRect = new Rect(94, 745, 259, 48);
                    view.speakerRect = new Rect(84, 721, 36, 18);
                    view.speakerBadgeRect = new Rect(57, 713, 91, 33);
                    view.speakerBadge = Sprite("SpeakerBadge");
                    view.speakerColor = Color.white;
                    PrefabUtility.SaveAsPrefabAsset(root, ViewPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var cut = AssetDatabase.LoadAssetAtPath<StoryCutDefinition>(Root + "/Opening/Cuts/OP_002.asset");
            if (cut.pages == null) cut.pages = new List<StoryDialoguePage>();
            if (cut.pages.Count == 0) cut.pages.Add(new StoryDialoguePage());
            if (cut.pages.Count == 1 && string.IsNullOrWhiteSpace(cut.pages[0].dialogue))
            {
                cut.pages[0].speaker = "사부님";
                cut.pages[0].dialogue = "이레 뒤에 돌아올 테니,\n수장고를 깨끗이 청소해 놓거라!";
                cut.pages[0].autoReadSeconds = 6f;
                EditorUtility.SetDirty(cut);
            }
            var preview = AssetDatabase.LoadAssetAtPath<StorySequence>(PreviewPath);
            if (preview == null)
            {
                preview = ScriptableObject.CreateInstance<StorySequence>();
                preview.storyId = "opening-preview";
                preview.cuts.Add(AssetDatabase.LoadAssetAtPath<StoryCutDefinition>(Root + "/Opening/Cuts/OP_001.asset"));
                preview.cuts.Add(cut);
                AssetDatabase.CreateAsset(preview, PreviewPath);
            }
            AssetDatabase.SaveAssets();
            UpgradePreviewScene(preview);
            Debug.Log("HAPIO_STORY_SECOND_CUT_READY: original character layers and speaker badge connected; OpeningPreview contains the first two cuts.");
        }

        private static StoryLayer Add(Transform root, List<StoryLayer> layers, string name, Rect rect,
            float delay, float duration, float height, bool shadow = false)
        {
            var layer = StoryWorkspaceBuilder.Layer(root, name, rect, Sprite(name), delay, duration,
                height == 1 ? Vector2.zero : new Vector2(0, -10), height);
            if (shadow)
            {
                var effect = layer.artwork.gameObject.AddComponent<Shadow>();
                effect.effectColor = new Color(0, 0, 0, .25f);
                effect.effectDistance = new Vector2(0, -4);
            }
            layers.Add(layer);
            return layer;
        }

        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");

        private static void Detail(Transform root, string owner, string id, Rect rect)
        {
            Transform placement = root.Find(owner + " Placement");
            var ownerRect = (RectTransform)placement;
            Transform motion = placement.Find("Motion");
            // All Artwork children use the owner's top-left origin and inherit its unfolding.
            var local = new Rect(rect.x - ownerRect.anchoredPosition.x,
                rect.y + ownerRect.anchoredPosition.y, rect.width, rect.height);
            var image = StoryWorkspaceBuilder.Rect("Detail " + id, motion, local).gameObject.AddComponent<Image>();
            image.sprite = Sprite("Detail_" + id);
            image.raycastTarget = false;
        }

        private static void AddOriginalDetails(Transform root)
        {
            Detail(root, "Blue", "3182_11942", new Rect(349.526611f, 546.803833f, 7.957764f, 7.378052f));
            Detail(root, "Yellow", "3182_11948", new Rect(62f, 507.999512f, 4.455566f, 7.324341f));
            Detail(root, "TeacherAndRed", "3182_11950", new Rect(218.443115f, 362.725708f, 1.191162f, 1.583618f));
            Detail(root, "TeacherAndRed", "3182_11951", new Rect(218.691406f, 361.971802f, 1.362061f, 2.075562f));
            Detail(root, "TeacherAndRed", "3182_11952", new Rect(219.383423f, 364.775146f, 1.306396f, 1.680176f));
            Detail(root, "TeacherAndRed", "3182_11953", new Rect(206.456787f, 359.373047f, .762573f, 1.041382f));
            Detail(root, "TeacherAndRed", "3182_11954", new Rect(205.059937f, 359.455933f, .881592f, .922119f));
            Detail(root, "TeacherAndRed", "3182_11955", new Rect(213.784546f, 360.710205f, 1.905640f, 1.310547f));
            Detail(root, "TeacherAndRed", "3182_11956", new Rect(216.819824f, 361.125122f, .536255f, 1.039307f));
            Detail(root, "TeacherAndRed", "3182_11957", new Rect(217.380005f, 362.548218f, 2.336548f, 2.356934f));
        }

        private static void UpgradePreviewScene(StorySequence sequence)
        {
            Scene scene = SceneManager.GetSceneByPath(StoryWorkspaceBuilder.PreviewScene);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(StoryWorkspaceBuilder.PreviewScene, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save the preview scene before adding its speaker badge.");
                var player = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<StoryPlayer>(true)).Single();
                if (player.speakerBadge == null)
                {
                    var rect = StoryWorkspaceBuilder.Rect("Speaker Badge", player.dialogueGroup.transform, new Rect(57, 713, 91, 33));
                    rect.SetSiblingIndex(player.speakerText.transform.GetSiblingIndex());
                    player.speakerBadge = rect.gameObject.AddComponent<Image>();
                    player.speakerBadge.raycastTarget = false;
                    player.speakerBadge.enabled = false;
                }
                if (AssetDatabase.GetAssetPath(player.sequence) == Root + "/Opening/FirstCutPreview.asset") player.sequence = sequence;
                EditorUtility.SetDirty(player);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
