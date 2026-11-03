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
            var root = new GameObject("Animated Title", typeof(RectTransform), typeof(RectMask2D), typeof(TitleCloudDrift),
                typeof(TitleArtCover));
            Stretch((RectTransform)root.transform);
            try
            {
                Image(root.transform, "Paper Landscape", new Rect(0, 0, 402, 874), "a763a.png");
                // #467: clouds drift left with the wind across the whole width, so the name is drawn in front of them.
                var tracks = new List<TitleCloudDrift.Cloud>();
                Cloud(root.transform, tracks, "Cloud Left", new Rect(-182, 257, 459, 153), "5e97a.png", 16);
                Cloud(root.transform, tracks, "Cloud Right", new Rect(193, 67, 459, 153), "5e97a.png", 12);
                Cloud(root.transform, tracks, "Cloud Lower", new Rect(233, 766, 218, 87), "4c1b0.png", 20);
                Cloud(root.transform, tracks, "Cloud Upper", new Rect(15, 9, 145, 91), "31590.png", 14);
                root.GetComponent<TitleCloudDrift>().clouds = tracks.ToArray();
                var logo = StoryWorkspaceBuilder.Rect("합이오", root.transform, new Rect(82, 164, 238, 113)).gameObject.AddComponent<Text>();
                logo.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/ShillaCultureBold.otf");
                logo.fontSize = 96; logo.text = "합이오"; logo.color = Color.black;
                logo.alignment = TextAnchor.MiddleLeft; logo.horizontalOverflow = HorizontalWrapMode.Overflow;
                logo.verticalOverflow = VerticalWrapMode.Overflow; logo.raycastTarget = false;
                PrefabUtility.SaveAsPrefabAsset(root, TitlePath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // #467: the landscape no longer paints the flag; these pieces are placed over it and move in the wind.
        private const string Pole = "FlagPole.png";
        private static readonly string[] FlagArt = { Pole, "FlagBanner.png", "RibbonRedYellow.png", "RibbonBlue.png" };
        // FlagPole.png (318x1024): finial and crossbar in the top 212px, base cap in the bottom 24px; only the shaft stretches.
        private static readonly Vector4 PoleBorder = new Vector4(0, 24, 0, 212);
        private const float PoleWidth = 58;

        [MenuItem("Hapio/Story/Add Title Wind Flag")]
        public static void AddWindFlag()
        {
            foreach (var file in FlagArt) StoryWorkspaceBuilder.ImportSprite(Art + file);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Art + Pole);
            if (importer.spriteBorder != PoleBorder) { importer.spriteBorder = PoleBorder; importer.SaveAndReimport(); }
            var root = PrefabUtility.LoadPrefabContents(TitlePath);
            try
            {
                if (root.transform.Find("Wind Flag") != null) return;
                // The 3x landscape is exactly 402x874, so it fills its frame without the old sub-pixel crop.
                var landscape = (RectTransform)root.transform.Find("Paper Landscape/Artwork");
                landscape.anchoredPosition = Vector2.zero; landscape.sizeDelta = new Vector2(402, 874);
                var flag = StoryWorkspaceBuilder.Rect("Wind Flag", root.transform, new Rect(0, 0, 402, 874));
                flag.SetSiblingIndex(1); // over the landscape, under the name and the clouds
                var ribbon = Wave(flag, "Ribbon Red Yellow", new Rect(219, 472, 112, 63), "RibbonRedYellow.png");
                Ribbon(ribbon, 0);
                ribbon = Wave(flag, "Ribbon Blue", new Rect(216, 490, 90, 65), "RibbonBlue.png");
                Ribbon(ribbon, .37f);
                var pole = Picture(flag, "Pole", new Rect(333 - PoleWidth * .5f, 455, PoleWidth, 335), Pole);
                pole.type = UnityEngine.UI.Image.Type.Sliced;
                pole.pixelsPerUnitMultiplier = pole.sprite.rect.width / PoleWidth;
                var banner = Wave(flag, "Banner", new Rect(311, 484, 44, 167), "FlagBanner.png");
                banner.pinned = new Vector2(.5f, 1); banner.tip = new Vector2(.5f, 0);
                banner.amplitude = 2.5f; banner.lean = -3; banner.waves = .8f; banner.speed = .5f; banner.stiffness = 1.6f;
                banner.gustStrength = 1.5f; banner.columns = 4; banner.rows = 16;
                PrefabUtility.SaveAsPrefabAsset(root, TitlePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static readonly string[] LeafArt = { "LeafMapleRed.png", "LeafMapleYellow.png", "LeafLongRed.png", "LeafLongYellow.png" };

        // #467: leaves blow across the whole title in front of the clouds; the start button stays above the title art.
        [MenuItem("Hapio/Story/Add Title Falling Leaves")]
        public static void AddFallingLeaves()
        {
            foreach (var file in LeafArt) StoryWorkspaceBuilder.ImportSprite(Art + file);
            var root = PrefabUtility.LoadPrefabContents(TitlePath);
            try
            {
                if (root.transform.Find("Falling Leaves") != null) return;
                var area = StoryWorkspaceBuilder.Rect("Falling Leaves", root.transform, new Rect(0, 0, 402, 874));
                area.SetAsLastSibling();
                var leaves = area.gameObject.AddComponent<TitleFallingLeaves>();
                leaves.sprites = LeafArt.Select(file => AssetDatabase.LoadAssetAtPath<Sprite>(Art + file)).ToArray();
                if (leaves.sprites.Any(sprite => sprite == null)) throw new InvalidOperationException("Missing title leaf image");
                // The same gust as the flag, so everything on the title is hit at once.
                leaves.gustSeconds = root.transform.Find("Wind Flag/Banner").GetComponent<TitleWindWave>().gustSeconds;
                PrefabUtility.SaveAsPrefabAsset(root, TitlePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Tied at the pole end (upper right of the art), the tail streams left and lifts in a gust.
        private static void Ribbon(TitleWindWave ribbon, float phase)
        {
            ribbon.pinned = new Vector2(.98f, .97f); ribbon.tip = new Vector2(.02f, .03f);
            ribbon.amplitude = 4; ribbon.lean = -4; ribbon.waves = 1.5f; ribbon.speed = 1.2f; ribbon.stiffness = 1.2f;
            ribbon.phase = phase; ribbon.gustStrength = 1.5f; ribbon.columns = 16; ribbon.rows = 9;
        }

        private static TitleWindWave Wave(Transform parent, string name, Rect box, string file) =>
            Picture(parent, name, box, file).gameObject.AddComponent<TitleWindWave>();

        private static Image Picture(Transform parent, string name, Rect box, string file)
        {
            var image = StoryWorkspaceBuilder.Rect(name, parent, box).gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + file);
            if (image.sprite == null) throw new InvalidOperationException("Missing title image " + file);
            image.raycastTarget = false;
            return image;
        }

        private static void Cloud(Transform parent, List<TitleCloudDrift.Cloud> tracks, string name, Rect box,
            string image, float speed)
        {
            var frame = Image(parent, name, box, image);
            tracks.Add(new TitleCloudDrift.Cloud { target = frame, speed = speed });
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
