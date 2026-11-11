using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio.Editor
{
    public static class GameOverCharacterUpgrade
    {
        private const string Art = "Assets/Hapio/Art/UI/GameOver/";
        private static Font font;

        [MenuItem("Hapio/Upgrade Character Game Over")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/ShillaCultureBold.otf");
            if (font == null) throw new InvalidOperationException("Shilla font missing.");
            foreach (string asset in new[] { "Parchment", "Cloud", "Shadow", "Bul", "Geumok" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(Art + asset + ".png");
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(GameScenePaths.GameOver);
            bool owned = !scene.IsValid() || !scene.isLoaded;
            if (owned) scene = EditorSceneManager.OpenScene(GameScenePaths.GameOver, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Save GameOver scene before upgrading.");
            try
            {
                var screen = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameScreen>(true)).Single();
                if (screen.gameOverView != null) return;
                var safe = screen.GetComponentInChildren<GameSafeArea>(true).transform;
                var design = Rect("CharacterGameOver", safe, 0, 0, 402, 874);
                design.anchorMin = design.anchorMax = design.pivot = new Vector2(.5f, .5f);
                design.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
                var view = design.gameObject.AddComponent<GameOverCharacterView>();
                view.design = design;
                Picture("Parchment", design, "Parchment", -74, -167, 704, 1041);
                Picture("CloudLeft", design, "Cloud", -250, 167, 337, 112);
                Picture("CloudBottom", design, "Cloud", -175, 744, 337, 112);
                Picture("CloudTop", design, "Cloud", 286, 31, 235, 78);
                Picture("CloudRight", design, "Cloud", 304, 666, 235, 78);
                Label("Title", design, "봉인 실패 ...", 50, 94, 118, 300, 65, Color.black, TextAnchor.UpperLeft);
                Label("Reason", design, "함께하던 동지가 이탈했소...", 20, 63, 187, 276, 36, new Color32(104, 100, 92, 255));
                view.bul = Character(design, "Bul", 65, 313, 274, 289, 63, 269, new Rect(.0003f, 1f - 1f / 1.0381f, 1f / 1.0006f, 1f / 1.0381f));
                view.geumok = Character(design, "Geumok", 93, 294, 214, 308, 87, 227, new Rect(0, 1f - (1f + .0198f) / 1.0418f, 1, 1f / 1.0418f));
                view.geumok.SetActive(false);
                view.dialogue = Label("Dialogue", design, "얼른 다시 돌아와!", 24, 56, 632, 276, 40, new Color32(49, 43, 35, 255));
                var buttonRect = Rect("ReturnHome", design, 27, 768, 349, 58);
                var background = buttonRect.gameObject.AddComponent<UnityEngine.UI.Image>();
                background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Hapio/Art/UI/StageResult/Capsule.png");
                background.color = new Color32(175, 71, 56, 255);
                view.homeButton = buttonRect.gameObject.AddComponent<UnityEngine.UI.Button>();
                view.homeButton.targetGraphic = background;
                view.homeButton.interactable = false;
                Label("Label", buttonRect, "수장고로 돌아가기", 24, 0, 0, 349, 58, new Color32(250, 246, 241, 255));
                screen.gameOverView = view;
                GameResultStrokes.Apply(view);
                design.gameObject.SetActive(false);
                EditorUtility.SetDirty(screen);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (owned) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static GameObject Character(Transform parent, string name, float x, float y, float width, float height, float shadowX, float shadowWidth, Rect crop)
        {
            var root = Rect(name, parent, 0, 0, 402, 874);
            Picture("Shadow", root, "Shadow", shadowX, 585, shadowWidth, 23).color = new Color(1, 1, 1, .4f);
            Picture("Portrait", root, name, x, y, width, height).uvRect = crop;
            return root.gameObject;
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static UnityEngine.UI.RawImage Picture(string name, Transform parent, string asset, float x, float y, float width, float height)
        {
            var image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + asset + ".png");
            if (image.texture == null) throw new InvalidOperationException("Missing " + asset);
            image.raycastTarget = false;
            return image;
        }

        private static UnityEngine.UI.Text Label(string name, Transform parent, string value, int size, float x, float y, float width, float height, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var text = Rect(name, parent, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
