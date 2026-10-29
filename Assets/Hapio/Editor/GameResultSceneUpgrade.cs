using System;
using System.IO;
using System.Linq;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hapio.Editor
{
    /// <summary>One-time, scoped upgrade. Existing result references and all other Scenes are preserved.</summary>
    public static class GameResultSceneUpgrade
    {
        public const string Art = "Assets/Hapio/Art/UI/StageResult/";
        private static readonly Color Ink = Html("3f3d38"), Secondary = Html("68645c");
        private static Font font;

        [MenuItem("Hapio/Upgrade Figma Stage Results")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before upgrading results.");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/ShillaCultureBold.otf");
            if (font == null) throw new InvalidOperationException("The existing Shilla Culture Bold font is missing.");
            ConfigureArt();
            UpgradeScene(GameScenePaths.StageResult, true);
            UpgradeScene(GameScenePaths.GameOver, false);
            AssetDatabase.SaveAssets();
            Debug.Log("HAPIO_STAGE_RESULT_UI_READY: editable victory/defeat UI saved; Lab, Home, Lobby and Battle preserved.");
        }

        private static void ConfigureArt()
        {
            Rounded("RoundedPanel", 344, 337, 24);
            Rounded("Capsule", 280, 58, 27);
            foreach (string path in Directory.GetFiles(Art, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static void Rounded(string name, int width, int height, float radius)
        {
            string path = Art + name + ".png";
            if (File.Exists(path)) return;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                float cx = Mathf.Clamp(x + .5f, radius, width - radius);
                float cy = Mathf.Clamp(y + .5f, radius, height - radius);
                float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
                pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance + .5f));
            }
            texture.SetPixels32(pixels); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        private static void UpgradeScene(string path, bool victory)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(path);
            bool owned = !scene.IsValid() || !scene.isLoaded;
            if (owned) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Save the result Scene before upgrading: " + path);
            try
            {
                var screen = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameScreen>(true)).Single();
                if (screen.resultView != null) return; // Keep subsequent Inspector edits.
                var canvas = screen.GetComponent<Canvas>();
                Transform safe = screen.GetComponentInChildren<GameSafeArea>(true).transform;
                RectTransform legacy = Rect("LegacyResultContent", safe); Stretch(legacy);
                foreach (Transform child in safe.Cast<Transform>().ToArray())
                    if (child != legacy && (screen.viewerPanel == null || child != screen.viewerPanel.transform)) child.SetParent(legacy, false);
                screen.legacyResultContent = legacy.gameObject;
                legacy.gameObject.SetActive(false);
                RawImage background = Rect("ResultBackground", canvas.transform).gameObject.AddComponent<RawImage>();
                Stretch(background.rectTransform); background.transform.SetSiblingIndex(1);
                background.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + (victory ? "VictoryBackground.png" : "DefeatBackground.png"));
                background.raycastTarget = false;
                var root = Rect("FigmaStageResult", safe); Stretch(root);
                var view = root.gameObject.AddComponent<GameStageResultView>();
                view.background = background;
                view.design = Rect("Design402x874", root);
                view.design.anchorMin = view.design.anchorMax = view.design.pivot = new Vector2(.5f, .5f);
                view.design.sizeDelta = new Vector2(402, 874);
                view.design.localScale = Vector3.one * (390f / 402f);
                view.stage = Text("Stage", view.design, "STAGE 1", 22, 35, 30, 180, 28);
                view.yokai = Text("Yokai", view.design, "구미호", 40, 30, 80, 342, 50, TextAnchor.MiddleCenter);
                view.judgment = Text("Judgment", view.design, victory ? "봉인 성공 !" : "봉인 실패...", 40, 20, 133, 362, 50, TextAnchor.MiddleCenter);
                Image panel = Image("RecordPanel", view.design, "RoundedPanel", 29, 208, 344, 337);
                panel.color = new Color(217f / 255f, 192f / 255f, 143f / 255f, .18f);
                view.paint = Text("Paint", view.design, victory ? "PAINT 100.0%" : "PAINT 82.3%", 18, 59, 229, 150, 26);
                view.timeLeft = Text("TimeLeft", view.design, "TIME LEFT 16s", 18, 211, 229, 137, 26, TextAnchor.MiddleRight);
                var table = Rect("AwardTable", view.design); Place(table, 52, 265, 297, 245);
                Image("HeaderFill", table, null, 0, 0, 297, 45).color = new Color(1f, .9725f, .9294f, .15f);
                Text("TitleHeader", table, "칭호", 20, 9, 6, 90, 32);
                Text("PlayerHeader", table, "견습생", 20, 101, 6, 80, 32);
                Text("RecordHeader", table, "기록", 20, 181, 6, 114, 32);
                Image("TopRule", table, "HeaderRule", 0, -1.5f, 297, 3);
                Image("HeaderRule", table, "HeaderRule", 0, 43.5f, 297, 3);
                var viewport = Rect("Viewport", table); Place(viewport, 0, 45, 297, 200);
                viewport.gameObject.AddComponent<RectMask2D>();
                view.scroll = table.gameObject.AddComponent<ScrollRect>();
                view.scroll.viewport = viewport;
                view.scroll.horizontal = false; view.scroll.vertical = false;
                view.scroll.movementType = ScrollRect.MovementType.Clamped;
                view.scroll.scrollSensitivity = 24;
                // An invisible raycast surface lets long/tied award lists scroll without blocking buttons.
                Image hit = viewport.gameObject.AddComponent<Image>(); hit.color = Color.clear;
                view.rows = Rect("Rows", viewport); Place(view.rows, 0, 0, 297, 200);
                view.scroll.content = view.rows;
                var template = Rect("AwardTemplate", view.rows); Place(template, 0, 0, 297, 40);
                Image("Highlight", template, "MyRow", 2, 0, 294, 40).color = new Color(.85f, .8f, .75f, 1f);
                Text("Title", template, "딜왕", 18, 9, 0, 91, 40);
                Text("Player", template, "P5(나)", 18, 101, 0, 79, 40);
                Text("Record", template, "440(20회 타격)", 18, 181, 0, 114, 40);
                Image("RowRule", template, "RowRule", 0, 38.5f, 296, 3);
                view.rowTemplate = template.gameObject; template.gameObject.SetActive(false);
                view.emptyLabel = Text("NoRecords", viewport, "기록 없음", 18, 0, 0, 297, 40, TextAnchor.MiddleCenter);
                view.guidance = Text("Guidance", view.design, victory ? "결과를 확인한 뒤\n방장이 다음 스테이지를 시작하시오" : "요괴를 다시 봉인하고싶다면\n게임을 새로 시작하시오", 18, 35, 561, 332, 52, TextAnchor.UpperCenter);
                view.guidance.color = Secondary;
                view.nextButton = Button("NextStageOrOverall", view.design, "다음 스테이지", 71, 616, 260, 36, Html("9d9586"), 18);
                view.nextButton.gameObject.SetActive(false);
                view.nextButton.interactable = false; // GameScreen initializes Host/Guest actions before the first frame.
                if (victory) view.viewerButton = Button("InspectSealedGourd", view.design, "구미호 봉인 박 확인", 61, 664, 280, 58, Ink, 24);
                if (victory)
                {
                    // 2:2 (#293) shares this page: the winning team's watercolour corners, blue for 청 and red for 홍.
                    view.blueTeamBackground = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "DefeatBackground.png");
                    view.redTeamBackground = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "VictoryBackground.png");
                }
                view.homeButton = Button("LeaveRun", view.design, "나가기", 61, 734, 280, 58, Html("9d9586"), 24);
                screen.resultView = view;
                if (screen.viewerPanel != null) screen.viewerPanel.transform.SetAsLastSibling();
                if (!scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Camera>(true)).Any())
                {
                    var camera = new GameObject("Result UI Camera", typeof(Camera)).GetComponent<Camera>();
                    SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.cullingMask = 0;
                    camera.backgroundColor = Html("fff7e5"); camera.depth = -100;
                }
                EditorUtility.SetDirty(screen);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Could not save " + path);
            }
            finally
            {
                if (owned && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); return rect;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
        private static Image Image(string name, Transform parent, string sprite, float x, float y, float width, float height)
        {
            RectTransform rect = Rect(name, parent); Place(rect, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>(); image.raycastTarget = false;
            if (sprite != null) image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + sprite + ".png");
            return image;
        }
        private static Text Text(string name, Transform parent, string value, int size, float x, float y, float width, float height, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            RectTransform rect = Rect(name, parent); Place(rect, x, y, width, height);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.text = value; text.color = Ink;
            text.alignment = alignment; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 12; text.resizeTextMaxSize = size;
            return text;
        }
        private static Button Button(string name, Transform parent, string label, float x, float y, float width, float height, Color color, int size)
        {
            Image image = Image(name, parent, "Capsule", x, y, width, height); image.color = color; image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            ColorBlock colors = button.colors; colors.disabledColor = new Color(1, 1, 1, .45f); button.colors = colors;
            var text = Text("Label", image.transform, label, size, 5, 0, width - 10, height, TextAnchor.MiddleCenter);
            text.color = Html("fff7e5");
            return button;
        }
        private static Color Html(string value) { ColorUtility.TryParseHtmlString("#" + value, out Color color); return color; }
    }
}
