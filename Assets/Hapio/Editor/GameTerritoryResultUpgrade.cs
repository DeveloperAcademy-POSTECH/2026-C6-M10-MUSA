using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hapio.Editor
{
    public static class GameTerritoryResultUpgrade
    {
        private const string Art = "Assets/Hapio/Art/UI/TerritoryResult/";
        private const string StoryArt = "Assets/Hapio/Art/Story/StageStories/";
        private static readonly Color Ink = new Color32(63, 61, 56, 255);
        private static Font bold, medium;

        [MenuItem("Hapio/Upgrade Territory Results")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            bold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/ShillaCultureBold.otf");
            medium = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/Shilla_Culture(M).otf");
            foreach (string path in System.IO.Directory.GetFiles(Art, "*.png"))
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
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(GameScenePaths.StageResult);
            bool owned = !scene.IsValid() || !scene.isLoaded;
            if (owned) scene = EditorSceneManager.OpenScene(GameScenePaths.StageResult, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Save StageResult first.");
            try
            {
                var screen = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameScreen>(true)).Single();
                if (screen.territoryView != null) return;
                var table = UnityEngine.Object.Instantiate(screen.resultView, screen.resultView.transform.parent, false);
                table.name = "TerritoryResult";
                var view = table.gameObject.AddComponent<GameTerritoryResultView>();
                view.table = table;
                var design = table.design;
                design.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
                table.background = null;
                foreach (string name in new[] { "Stage", "RecordPanel", "Paint", "TimeLeft", "Guidance" })
                    design.Find(name)?.gameObject.SetActive(false);
                var paper = Picture("Parchment", design, "Assets/Hapio/Art/UI/GameOver/Parchment.png", -74, -167, 704, 1041);
                paper.transform.SetAsFirstSibling();
                Picture("CloudLeft", design, "Assets/Hapio/Art/UI/GameOver/Cloud.png", -250, 167, 337, 112).transform.SetSiblingIndex(1);
                Picture("CloudBottom", design, "Assets/Hapio/Art/UI/GameOver/Cloud.png", -175, 744, 337, 112).transform.SetSiblingIndex(2);
                Picture("CloudRight", design, "Assets/Hapio/Art/UI/GameOver/Cloud.png", 304, 666, 235, 78).transform.SetSiblingIndex(3);
                view.blueDecoration = Decorations(design, false);
                view.redDecoration = Decorations(design, true);
                Place(table.yokai.rectTransform, 40, 95, 322, 53);
                table.yokai.text = "땅따먹기 결과";
                table.yokai.fontSize = 40;
                Place(table.judgment.rectTransform, 40, 157, 322, 53);
                table.judgment.fontSize = 40;
                table.judgment.supportRichText = true;
                var awards = (RectTransform)design.Find("AwardTable");
                Place(awards, 45, 320, 312, 274);
                awards.Find("HeaderFill").gameObject.SetActive(false);
                SetHeader(awards, "TitleHeader", "칭호", 0, 110);
                SetHeader(awards, "PlayerHeader", "견습생", 110, 80);
                SetHeader(awards, "RecordHeader", "기록", 190, 122);
                SetRule(awards.Find("TopRule"), "816b1", 0);
                SetRule(awards.Find("HeaderRule"), "e17c6", 41);
                Place(table.scroll.viewport, 0, 43, 312, 232);
                Place(table.rows, 0, 0, 312, 232);
                table.rowHeight = 46;
                table.secondaryInk = new Color32(93, 84, 74, 255);
                var template = (RectTransform)table.rowTemplate.transform;
                Place(template, 0, 0, 312, 46);
                SetCell(template, "Title", 0, 110);
                SetCell(template, "Player", 110, 80);
                SetCell(template, "Record", 190, 122);
                var highlight = template.Find("Highlight").GetComponent<UnityEngine.UI.Image>();
                highlight.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "cecf7.png");
                highlight.color = Color.white;
                Place(highlight.rectTransform, 0, 0, 312, 43);
                SetRule(template.Find("RowRule"), "d479c", 45);
                Place(table.emptyLabel.rectTransform, 0, 0, 312, 46);
                Button(table.viewerButton, "봉인 박 둘러보기", 664);
                Button(table.nextButton, "다시 도전", 721);
                Button(table.homeButton, "나가기", 774);
                Place((RectTransform)table.homeButton.transform, 151, 774, 92, 40);
                table.homeButton.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
                var exitLabel = table.homeButton.GetComponentInChildren<UnityEngine.UI.Text>();
                exitLabel.color = Ink;
                exitLabel.fontSize = 19;
                Picture("ExitUnderline", design, StoryArt + "svg_82302.png", 169, 803.58f, 55, 2.84f);
                var frame = Solid("TerritoryBarFrame", design, 29.5f, 249, 342, 27, new Color32(49, 43, 35, 255));
                var track = Solid("Track", frame, 3, 3, 336, 21, new Color32(237, 230, 218, 255));
                view.blueFill = Solid("BlueFill", track, 0, 0, 0, 21, new Color32(14, 97, 146, 255));
                view.redFill = Solid("RedFill", track, 336, 0, 0, 21, new Color32(211, 46, 21, 255));
                for (int index = 1; index < 10; index++)
                    Picture("Tick" + index, frame, "Assets/Hapio/Art/UI/Battle/" + (index == 5 ? "0e7c5" : "ead0b") + ".png", 34.2f * index, 0, index == 5 ? 4 : 1, 26);
                view.bluePercent = Label("BluePercent", frame, 85, -1, 58, 28, 18, new Color32(159, 192, 211, 255));
                view.redPercent = Label("RedPercent", frame, 143, -1, 58, 28, 18, new Color32(237, 171, 161, 255));
                view.blueWinnerFlag = Flag(design, "BlueWinner", Art + "d7661.png", 16, 211, 73, 76, "청팀", 30, new Color32(14, 97, 146, 255));
                view.redWinnerFlag = Flag(design, "RedWinner", Art + "d5704.png", 312, 209, 73, 76, "홍팀", 30, new Color32(211, 46, 21, 255));
                AlignWinnerFlags(view);
                view.blueSmallFlag = Flag(design, "BlueSmall", "Assets/Hapio/Art/UI/Battle/50224.png", 24, 228, 44, 45, "청팀", 18, new Color32(14, 97, 146, 255));
                view.redSmallFlag = Flag(design, "RedSmall", "Assets/Hapio/Art/UI/Battle/3aff5.png", 334, 228, 44, 45, "홍팀", 18, new Color32(211, 46, 21, 255));
                table.gameObject.SetActive(false);
                screen.territoryView = view;
                if (screen.viewerPanel != null) screen.viewerPanel.transform.SetAsLastSibling();
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

        public static void AlignWinnerFlags(GameTerritoryResultView view)
        {
            AlignWinnerFlag(view.blueWinnerFlag, new Vector2(52.60f, 240.87f), new Vector2(52.19f, 237.91f));
            AlignWinnerFlag(view.redWinnerFlag, new Vector2(348.56f, 238.59f), new Vector2(348.97f, 236.62f));
        }

        private static void AlignWinnerFlag(GameObject flag, Vector2 center, Vector2 textCenter)
        {
            var rect = (RectTransform)flag.transform;
            rect.sizeDelta = new Vector2(73.472f, 75.205f);
            rect.anchoredPosition = new Vector2(center.x, -center.y);
            var label = flag.GetComponentInChildren<UnityEngine.UI.Text>(true).rectTransform;
            label.sizeDelta = new Vector2(97.44f, 47.04f);
            label.localPosition = rect.InverseTransformPoint(rect.parent.TransformPoint(rect.localPosition
                + new Vector3(textCenter.x - center.x, center.y - textCenter.y, 0)));
        }

        private static GameObject Decorations(Transform parent, bool red)
        {
            var root = Rect(red ? "RedWatercolor" : "BlueWatercolor", parent, 0, 0, 402, 874);
            var left = Picture("Left", root, Art + "Watercolor.png", red ? -136 : -142, red ? -129 : -135, 279, 269);
            left.uvRect = red ? new Rect(.0318f / 2.0161f, 1 - 1.9561f / 2.0096f, 1 / 2.0161f, 1 / 2.0096f)
                : new Rect(1.0457f / 2.0161f, 1 - 1.0038f / 2.0096f, 1 / 2.0161f, 1 / 2.0096f);
            if (red)
            {
                left.rectTransform.pivot = new Vector2(.5f, .5f);
                left.rectTransform.anchoredPosition = new Vector2(3.48f, -5.74f);
                left.rectTransform.localEulerAngles = new Vector3(0, 0, 132.86f);
            }
            var right = Picture("Right", root, Art + "Watercolor.png", red ? 268 : 278, red ? -150 : -135, 279, 269);
            right.uvRect = red ? new Rect(1.0075f / 2.0161f, 1 - 1.8987f / 2.0096f, 1 / 2.0161f, 1 / 2.0096f)
                : new Rect(.0422f / 2.0161f, 1 - 1.0037f / 2.0096f, 1 / 2.0161f, 1 / 2.0096f);
            return root.gameObject;
        }

        private static GameObject Flag(Transform parent, string name, string path, float x, float y, float width, float height, string title, int size, Color ink)
        {
            var image = Picture(name, parent, path, x, y, width, height);
            bool blue = name.StartsWith("Blue", StringComparison.Ordinal);
            image.rectTransform.pivot = new Vector2(.5f, .5f);
            image.rectTransform.anchoredPosition += new Vector2(width / 2, -height / 2);
            image.rectTransform.localEulerAngles = new Vector3(0, 0, blue ? -15 : 15);
            image.rectTransform.localScale = new Vector3(blue ? 1 : -1, 1, 1);
            var label = Label("Team", image.transform, 0, 0, width, height, size, ink);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(.5f, .5f);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.localScale = new Vector3(blue ? 1 : -1, 1, 1);
            label.rectTransform.localEulerAngles = new Vector3(0, 0, blue ? 20.81f : 21.45f);
            label.text = title;
            return image.gameObject;
        }

        private static void Button(UnityEngine.UI.Button button, string title, float y)
        {
            button.gameObject.SetActive(true);
            button.interactable = false;
            Place((RectTransform)button.transform, 77, y, 234, 45);
            button.GetComponent<UnityEngine.UI.Image>().color = new Color32(140, 57, 45, 255);
            var label = button.GetComponentInChildren<UnityEngine.UI.Text>(true);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.text = title; label.font = medium; label.fontSize = 21; label.resizeTextForBestFit = false;
            label.color = new Color32(245, 238, 225, 255);
        }
        private static void SetCell(Transform parent, string name, float x, float width)
        {
            var text = parent.Find(name).GetComponent<UnityEngine.UI.Text>();
            Place(text.rectTransform, x, 0, width, 43);
            text.fontSize = 17; text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 12; text.resizeTextMaxSize = 17;
        }
        private static void SetHeader(Transform parent, string name, string title, float x, float width)
        {
            var text = parent.Find(name).GetComponent<UnityEngine.UI.Text>();
            Place(text.rectTransform, x, 0, width, 41);
            text.text = title; text.fontSize = 19; text.alignment = TextAnchor.MiddleCenter;
        }
        private static void SetRule(Transform item, string asset, float y)
        {
            Place((RectTransform)item, 0, y - 1.42f, 312, 2.84f);
            var image = item.GetComponent<UnityEngine.UI.Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(StoryArt + "svg_" + asset + ".png");
            image.color = Color.white;
        }
        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
            Place(rect, x, y, width, height); return rect;
        }
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
        private static UnityEngine.UI.RawImage Picture(string name, Transform parent, string path, float x, float y, float width, float height)
        {
            var image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (image.texture == null) throw new InvalidOperationException("Missing " + path);
            image.raycastTarget = false; return image;
        }
        private static RectTransform Solid(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var rect = Rect(name, parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false;
            return rect;
        }
        private static UnityEngine.UI.Text Label(string name, Transform parent, float x, float y, float width, float height, int size, Color color)
        {
            var text = Rect(name, parent, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = size >= 30 ? bold : medium; text.fontSize = size; text.color = color;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text;
        }
    }
}
