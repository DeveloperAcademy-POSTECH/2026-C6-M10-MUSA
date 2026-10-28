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
    public static class StoryTitleBuilder
    {
        public const string TitlePath = "Assets/Hapio/Story/AnimatedTitle.prefab";
        private const string Art = "Assets/Hapio/Art/Story/Title/";
        private const string Lobby = "Assets/Hapio/Art/UI/GameLobby.prefab";
        [Serializable] private class Draft { public Line[] pages; }
        [Serializable] private class Line { public int number; public string speaker, dialogue; public float seconds; }

        [MenuItem("Hapio/Story/Create Animated Title")]
        public static void Build()
        {
            foreach (var path in Directory.GetFiles(Art, "*.png")) StoryWorkspaceBuilder.ImportSprite(path);
            CreateTitle();
            BindPreview();
            BindHome();
            AssetDatabase.SaveAssets();
            Debug.Log("HAPIO_ANIMATED_TITLE_READY: shared title art and four independently moving clouds connected to Home and StoryPreview.");
        }

        private static void CreateTitle()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TitlePath) != null) return;
            var root = new GameObject("Animated Title", typeof(RectTransform), typeof(RectMask2D), typeof(TitleCloudDrift));
            Stretch((RectTransform)root.transform);
            try
            {
                Image(root.transform, "Paper Landscape", new Rect(0, 0, 402, 874), "a763a.png");
                var logo = StoryWorkspaceBuilder.Rect("합이오", root.transform, new Rect(82, 164, 238, 113)).gameObject.AddComponent<Text>();
                logo.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/ShillaCultureBold.otf");
                logo.fontSize = 96; logo.text = "합이오"; logo.color = Color.black;
                logo.alignment = TextAnchor.MiddleLeft; logo.horizontalOverflow = HorizontalWrapMode.Overflow;
                logo.verticalOverflow = VerticalWrapMode.Overflow; logo.raycastTarget = false;
                var tracks = new List<TitleCloudDrift.Cloud>();
                Cloud(root.transform, tracks, "Cloud Left", new Rect(-182, 257, 459, 153), "5e97a.png", 24, 38, false);
                Cloud(root.transform, tracks, "Cloud Right", new Rect(193, 67, 459, 153), "5e97a.png", 20, 44, true);
                Cloud(root.transform, tracks, "Cloud Lower", new Rect(233, 766, 218, 87), "4c1b0.png", 12, 32, false);
                Cloud(root.transform, tracks, "Cloud Upper", new Rect(15, 9, 145, 91), "31590.png", 10, 36, true);
                root.GetComponent<TitleCloudDrift>().clouds = tracks.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, TitlePath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Cloud(Transform parent, List<TitleCloudDrift.Cloud> tracks, string name, Rect box,
            string image, float distance, float seconds, bool reverse)
        {
            var frame = Image(parent, name, box, image);
            tracks.Add(new TitleCloudDrift.Cloud { target = frame, distance = distance, seconds = seconds, reverse = reverse });
        }

        private static RectTransform Image(Transform parent, string name, Rect box, string file)
        {
            var frame = StoryWorkspaceBuilder.Rect(name, parent, box);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + file);
            if (sprite == null) throw new InvalidOperationException("Missing title image " + file);
            float scale = Mathf.Max(box.width / sprite.rect.width, box.height / sprite.rect.height);
            var size = sprite.rect.size * scale;
            frame.gameObject.AddComponent<RectMask2D>();
            var image = StoryWorkspaceBuilder.Rect("Artwork", frame, new Rect((box.width - size.x) * .5f,
                (box.height - size.y) * .5f, size.x, size.y)).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false;
            return frame;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one * .5f; rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.zero;
        }

        private static void BindHome()
        {
            var root = PrefabUtility.LoadPrefabContents(Lobby);
            try
            {
                var view = root.GetComponentInChildren<GameLobbyView>(true);
                var prompt = view.titlePage.transform.Find("SealPrompt");
                if (prompt != null) prompt.gameObject.SetActive(false);
                if (view.titlePage.transform.Find("Animated Title") != null) return;
                var art = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TitlePath), view.titlePage.transform);
                art.name = "Animated Title"; art.transform.SetAsFirstSibling();
                var oldTitle = view.titlePage.transform.Find("Title");
                if (oldTitle != null) oldTitle.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, Lobby);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void BindPreview()
        {
            var scene = SceneManager.GetSceneByPath(StoryWorkspaceBuilder.PreviewScene);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(StoryWorkspaceBuilder.PreviewScene, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save the story preview before adding its title.");
                var player = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<StoryPlayer>(true)).Single();
                if (player.GetComponent<StoryOpeningTitle>() != null) return;
                var page = player.cutHost.parent;
                var title = StoryWorkspaceBuilder.Rect("Opening Title", page, new Rect(0, 0, 402, 874));
                Stretch(title);
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TitlePath), title);
                var tap = StoryWorkspaceBuilder.Rect("Begin Story", title, new Rect(0, 0, 402, 874));
                var image = tap.gameObject.AddComponent<Image>(); image.color = Color.clear;
                var button = tap.gameObject.AddComponent<Button>(); button.transition = Selectable.Transition.None;
                button.targetGraphic = image;
                var gate = player.gameObject.AddComponent<StoryOpeningTitle>();
                gate.player = player; gate.titlePage = title.gameObject; gate.beginButton = button;
                gate.controls = player.transform.Find("Safe Controls").gameObject;
                player.playOnStart = false;
                gate.controls.SetActive(false);
                EditorUtility.SetDirty(player);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        // Applied explicitly for the requested writing pass; never runs on import or scene load.
        public static void ApplyDialogueDraft() => StoryApprovedScriptBuilder.Apply();
    }
}
