using System;
using System.IO;
using System.Linq;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio.Editor
{
    public static class GamePauseUpgrade
    {
        private const string Art = "Assets/Hapio/Art/UI/Pause/";
        private static Font font;
        private static Material trackMaterial;

        [MenuItem("Hapio/Upgrade Pause UI")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/ShillaCultureBold.otf");
            Rounded("Panel", 348, 260, 30);
            Rounded("Title", 194, 59, 29.5f);
            Rounded("TitleInner", 176, 41, 20.5f);
            Rounded("Action", 147, 48, 24);
            foreach (string path in Directory.GetFiles(Art, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            trackMaterial = AssetDatabase.LoadAssetAtPath<Material>(Art + "TrackAlpha.mat");
            if (trackMaterial == null)
            {
                trackMaterial = new Material(Shader.Find("Hapio/UI/Pause Track Alpha"));
                AssetDatabase.CreateAsset(trackMaterial, Art + "TrackAlpha.mat");
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(GameScenePaths.Battle);
            bool owned = !scene.IsValid() || !scene.isLoaded;
            if (owned) scene = EditorSceneManager.OpenScene(GameScenePaths.Battle, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Save Battle first.");
            try
            {
                var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameBattleBindings>(true)).Single().hud;
                if (hud.pauseMenu.GetComponent<GamePauseView>() != null) return;
                var menu = hud.pauseMenu;
                menu.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .5f);
                var view = menu.AddComponent<GamePauseView>();
                var design = Rect("PauseDesign", menu.transform, 0, 0, 402, 874);
                design.anchorMin = design.anchorMax = design.pivot = new Vector2(.5f, .5f);
                view.design = design;
                Image("Panel", design, "Panel", 27, 316, 348, 260, Html("EDDBC7"));
                Image("TitleBorder", design, "Title", 101, 298, 194, 59, Html("E8D2B9"));
                Image("TitleInner", design, "TitleInner", 110, 307, 176, 41, Html("D5BEA5"));
                Text("Title", design, "일시정지", 30, 101, 301, 194, 53, Html("312B23"));
                Text("MusicLabel", design, "배경음악", 20, 55, 394, 65, 30, Html("312B23"));
                Text("EffectsLabel", design, "효과음", 20, 55, 445, 65, 30, Html("312B23"));
                Slider(design, "BGMVolume", 405, "b3e1b", "c263c", "2830c");
                Slider(design, "SFXVolume", 457, "6cc9d", "88716", "d9ddc");
                Reparent(hud.resumeButton, design, 322, 330, 37, 37);
                foreach (Transform child in hud.resumeButton.transform) child.gameObject.SetActive(false);
                hud.resumeButton.GetComponent<UnityEngine.UI.Image>().sprite = Sprite("08662");
                hud.resumeButton.GetComponent<UnityEngine.UI.Image>().color = Color.white;
                Reparent(hud.returnLobbyButton, design, 48, 507, 147, 48);
                StyleAction(hud.returnLobbyButton, "나가기", Html("746C62"));
                var restart = Rect("Restart", design, 204, 507, 147, 48).gameObject.AddComponent<UnityEngine.UI.Image>();
                view.restartButton = restart.gameObject.AddComponent<UnityEngine.UI.Button>();
                view.restartButton.targetGraphic = restart;
                Text("Label", restart.transform, "재시작", 20, 0, 0, 147, 48, Html("FAF6F1"));
                StyleAction(view.restartButton, "재시작", Html("51473B"));
                view.restartButton.interactable = false;
                menu.transform.Find("PausePanel").gameObject.SetActive(false);
                if (hud.developerSettingsButton != null)
                    Reparent(hud.developerSettingsButton, design, 121, 600, 160, 36);
                menu.SetActive(false);
                EditorUtility.SetDirty(hud);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (owned) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static void Slider(Transform parent, string name, float y, string track, string normal, string pressed)
        {
            var root = Rect(name, parent, 137.94f, y - 18, 208.06f, 36);
            var slider = root.gameObject.AddComponent<UnityEngine.UI.Slider>();
            var hit = root.gameObject.AddComponent<UnityEngine.UI.Image>(); hit.color = Color.clear;
            var background = Image("Track", root, track, 0, 10.5f, 208.06f, 15, Html("B5A38D"));
            background.material = trackMaterial;
            var fill = Image("Fill", root, track, 0, 10.5f, 208.06f, 15, Html("312B23"));
            fill.material = trackMaterial;
            var clip = Rect("FillClip", root, 0, 10.5f, 208.06f, 15);
            clip.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            clip.anchorMin = new Vector2(0, .5f); clip.anchorMax = new Vector2(1, .5f);
            clip.pivot = new Vector2(0, .5f); clip.sizeDelta = new Vector2(0, -21); clip.anchoredPosition = Vector2.zero;
            fill.transform.SetParent(clip, false);
            fill.rectTransform.anchoredPosition = Vector2.zero;
            slider.fillRect = clip;
            var handleArea = Rect("HandleArea", root, 0, 0, 208.06f, 36);
            var handle = Image("Handle", handleArea, normal, 0, 0, 36, 36, Color.white);
            handle.rectTransform.pivot = new Vector2(.5f, .5f);
            handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(.5f, .5f);
            handle.rectTransform.anchoredPosition = Vector2.zero;
            handle.rectTransform.sizeDelta = new Vector2(36, 0);
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
            slider.spriteState = new UnityEngine.UI.SpriteState { highlightedSprite = Sprite(normal), pressedSprite = Sprite(pressed), selectedSprite = Sprite(normal), disabledSprite = Sprite(normal) };
            slider.minValue = 0; slider.maxValue = 1; slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(.6f);
        }

        private static void StyleAction(UnityEngine.UI.Button button, string value, Color color)
        {
            var image = button.GetComponent<UnityEngine.UI.Image>(); image.sprite = Sprite("Action"); image.color = color;
            button.targetGraphic = image;
            var label = button.GetComponentInChildren<UnityEngine.UI.Text>(true);
            label.gameObject.SetActive(true); label.text = value; label.font = font; label.fontSize = 20;
            label.resizeTextForBestFit = false; label.color = Html("FAF6F1"); label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        }
        private static void Reparent(UnityEngine.UI.Button button, Transform parent, float x, float y, float width, float height)
        {
            button.transform.SetParent(parent, false); Place((RectTransform)button.transform, x, y, width, height);
        }
        private static void Rounded(string name, int width, int height, float radius)
        {
            string path = Art + name + ".png";
            if (File.Exists(path)) return;
            var texture = new Texture2D(width * 3, height * 3, TextureFormat.RGBA32, false);
            var pixels = new Color[texture.width * texture.height];
            for (int row = 0; row < texture.height; row++) for (int column = 0; column < texture.width; column++)
            {
                float x = (column + .5f) / 3, y = (row + .5f) / 3;
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(Mathf.Clamp(x, radius, width - radius), Mathf.Clamp(y, radius, height - radius)));
                pixels[row * texture.width + column] = new Color(1, 1, 1, Mathf.Clamp01((radius - distance) * 3 + .5f));
            }
            texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
        }
        private static Color Html(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");
        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer; Place(rect, x, y, width, height); return rect;
        }
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
        private static UnityEngine.UI.Image Image(string name, Transform parent, string sprite, float x, float y, float width, float height, Color color)
        {
            var image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = Sprite(sprite); image.color = color; image.raycastTarget = false; return image;
        }
        private static void Text(string name, Transform parent, string value, int size, float x, float y, float width, float height, Color color)
        {
            var text = Rect(name, parent, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Text>();
            text.text = value; text.font = font; text.fontSize = size; text.color = color;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
        }
    }
}
