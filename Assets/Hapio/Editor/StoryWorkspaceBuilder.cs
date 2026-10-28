using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hapio.Editor
{
    /// <summary>Creates missing assets only. Re-running never overwrites authored cuts or scenes.</summary>
    public static class StoryWorkspaceBuilder
    {
        public const string Root = "Assets/Hapio/Story";
        public const string PreviewScene = Root + "/StoryPreview.unity";
        private const string Art = "Assets/Hapio/Art/Story/Opening/OP_001/";
        private static Font Font => AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/Shilla_Culture(M).otf");

        [MenuItem("Hapio/Story/Create Missing Story Workspace")]
        public static void CreateMissing()
        {
            Folder(Root + "/Opening/Cuts");
            Folder(Root + "/Opening/Views");
            ImportSprite(Art + "Background.png");
            ImportSprite(Art + "Temple.png");
            ImportSprite(Art + "DialoguePaper.png");
            var cuts = new List<StoryCutDefinition>();
            for (int i = 1; i <= 24; i++)
            {
                string id = $"OP_{i:000}";
                string viewPath = Root + "/Opening/Views/" + id + ".prefab";
                if (!File.Exists(viewPath)) CreateCutPrefab(viewPath, i == 1);
                string dataPath = Root + "/Opening/Cuts/" + id + ".asset";
                var cut = AssetDatabase.LoadAssetAtPath<StoryCutDefinition>(dataPath);
                if (cut == null)
                {
                    cut = ScriptableObject.CreateInstance<StoryCutDefinition>();
                    cut.cutId = id;
                    cut.viewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(viewPath).GetComponent<StoryCutView>();
                    cut.pages.Add(new StoryDialoguePage
                    {
                        dialogue = i == 1 ? "백 년에 한 번 열리는 대제사, D-7" : ""
                    });
                    AssetDatabase.CreateAsset(cut, dataPath);
                }
                cuts.Add(cut);
            }
            CreateSequence(Root + "/Opening/Opening.asset", "opening", cuts);
            var preview = CreateSequence(Root + "/Opening/FirstCutPreview.asset", "opening-first-cut-preview",
                new List<StoryCutDefinition> { cuts[0] });
            if (!File.Exists(PreviewScene)) CreatePreviewScene(preview);
            AssetDatabase.SaveAssets();
            Debug.Log("HAPIO_STORY_WORKSPACE_READY: 24 editable cut slots; OP_001 has Figma art. StoryPreview plays only the connected first cut.");
        }

        [MenuItem("Hapio/Story/Open Story Preview")]
        public static void OpenPreview()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateMissing();
            EditorSceneManager.OpenScene(PreviewScene);
        }

        private static void CreateCutPrefab(string path, bool first)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path), typeof(RectTransform), typeof(StoryCutView));
            try
            {
                Stretch((RectTransform)root.transform);
                var layers = new List<StoryLayer>();
                if (first)
                {
                    layers.Add(Layer(root.transform, "Background", new Rect(0, 0, 402, 874),
                        Sprite("Background.png"), 0, .35f, Vector2.zero, 1f));
                    layers.Add(Layer(root.transform, "Temple", new Rect(60, 259, 281, 500),
                        Sprite("Temple.png"), .25f, .9f, new Vector2(0, -15), .08f));
                }
                else
                {
                    string[] names = { "Background", "Far", "Middle", "Cloud", "Building", "Character", "Foreground" };
                    for (int i = 0; i < names.Length; i++)
                        layers.Add(Layer(root.transform, names[i], new Rect(0, 0, 402, 874), null,
                            i * .12f, .6f, new Vector2(0, -15), i == 0 ? 1f : .08f));
                }
                root.GetComponent<StoryCutView>().layers = layers.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        internal static StoryLayer Layer(Transform parent, string name, Rect geometry, Sprite sprite,
            float delay, float duration, Vector2 offset, float height)
        {
            var placement = Rect(name + " Placement", parent, geometry);
            var motion = Rect("Motion", placement, new Rect(0, 0, geometry.width, geometry.height));
            // Bottom pivot gives the paper an edge to unfold from; preserve its final position.
            motion.pivot = new Vector2(.5f, 0f);
            motion.anchoredPosition = new Vector2(geometry.width * .5f, -geometry.height);
            var layer = motion.gameObject.AddComponent<StoryLayer>();
            layer.delay = delay; layer.enterSeconds = duration; layer.enterOffset = offset; layer.startHeight = height;
            var image = Rect("Artwork", motion, new Rect(0, 0, geometry.width, geometry.height))
                .gameObject.AddComponent<Image>();
            // Rect() positions from a parent's top-left, regardless of the parent's pivot.
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0f, 1f);
            image.rectTransform.anchoredPosition = Vector2.zero;
            image.sprite = sprite;
            image.raycastTarget = false;
            image.enabled = sprite != null;
            layer.artwork = image;
            return layer;
        }

        private static void CreatePreviewScene(StorySequence sequence)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var camera = new GameObject("Story Camera", typeof(Camera));
                camera.tag = "MainCamera";
                camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
                camera.GetComponent<Camera>().backgroundColor = new Color(.12f, .10f, .08f);
                camera.transform.position = new Vector3(0, 0, -10);
                var light = new GameObject("Directional Light", typeof(Light));
                light.GetComponent<Light>().type = LightType.Directional;
                var canvas = new GameObject("Story Player", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                    typeof(GraphicRaycaster), typeof(StoryPlayer), typeof(AudioSource));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(402, 874);
                scaler.matchWidthOrHeight = 0f;
                var page = Rect("Page 402x874", canvas.transform, new Rect(0, 0, 402, 874));
                page.anchorMin = page.anchorMax = page.pivot = Vector2.one * .5f;
                page.anchoredPosition = Vector2.zero;
                page.gameObject.AddComponent<StoryPageFit>();
                page.gameObject.AddComponent<RectMask2D>();
                var background = Rect("Paper Base", page, new Rect(0, 0, 402, 874)).gameObject.AddComponent<Image>();
                background.color = new Color(.93f, .86f, .74f);
                background.raycastTarget = false;
                var host = Rect("Cut Host", page, new Rect(0, 0, 402, 874));
                Stretch(host);
                var dialogue = Rect("Dialogue", page, new Rect(0, 0, 402, 874));
                Stretch(dialogue);
                var group = dialogue.gameObject.AddComponent<CanvasGroup>();
                var paper = Rect("Dialogue Paper", dialogue, new Rect(11, 680, 379, 176)).gameObject.AddComponent<Image>();
                paper.sprite = Sprite("DialoguePaper.png");
                paper.raycastTarget = false;
                var shadow = paper.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, .25f);
                shadow.effectDistance = new Vector2(0, -5);
                var speaker = Text("Speaker", dialogue, new Rect(84, 716, 240, 24), "", 15);
                var body = Text("Dialogue Text", dialogue, new Rect(84, 740, 259, 56), "", 21);
                // Legacy Text takes integer font sizes; retain Figma's fractional visible size and wrapping.
                const float dialogueScale = 20.696f / 21f;
                body.rectTransform.localScale = Vector3.one * dialogueScale;
                body.rectTransform.sizeDelta = new Vector2(259, 56) / dialogueScale;
                var safe = Rect("Safe Controls", canvas.transform, new Rect(0, 0, 402, 874));
                Stretch(safe);
                safe.gameObject.AddComponent<GameSafeArea>();
                var toolbar = Rect("Toolbar", safe, new Rect(0, 0, 402, 34));
                toolbar.anchorMin = new Vector2(0, 1); toolbar.anchorMax = new Vector2(1, 1);
                toolbar.sizeDelta = new Vector2(0, 34);
                var progress = Text("Progress", toolbar, new Rect(8, 5, 62, 24), "", 14);
                var auto = Button("Auto", toolbar, new Rect(74, 2, 80, 30), "자동 꺼짐");
                var replay = Button("Replay", toolbar, new Rect(158, 2, 68, 30), "다시 보기");
                var skip = Button("Skip", toolbar, new Rect(230, 2, 64, 30), "건너뛰기");
                var previousButton = Button("Previous", toolbar, new Rect(298, 2, 46, 30), "이전");
                var next = Button("Next", toolbar, new Rect(348, 2, 46, 30), "다음");
                var player = canvas.GetComponent<StoryPlayer>();
                player.sequence = sequence; player.cutHost = host; player.dialogueGroup = group;
                player.speakerText = speaker; player.dialogueText = body; player.progressText = progress;
                player.speakerPalette = AssetDatabase.LoadAssetAtPath<StorySpeakerPalette>(Root + "/Opening/SpeakerPalette.asset");
                player.nextButton = next; player.previousButton = previousButton; player.skipButton = skip; player.autoButton = auto;
                player.autoLabel = auto.GetComponentInChildren<Text>();
                player.voiceSource = canvas.GetComponent<AudioSource>();
                player.voiceSource.playOnAwake = false;
                UnityEventTools.AddPersistentListener(replay.onClick, player.Replay);
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                EditorSceneManager.SaveScene(scene, PreviewScene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static StorySequence CreateSequence(string path, string id, List<StoryCutDefinition> cuts)
        {
            var sequence = AssetDatabase.LoadAssetAtPath<StorySequence>(path);
            if (sequence != null) return sequence;
            sequence = ScriptableObject.CreateInstance<StorySequence>();
            sequence.storyId = id; sequence.cuts = cuts;
            AssetDatabase.CreateAsset(sequence, path);
            return sequence;
        }

        internal static RectTransform Rect(string name, Transform parent, Rect geometry)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(geometry.x, -geometry.y);
            rect.sizeDelta = geometry.size;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Text Text(string name, Transform parent, Rect geometry, string value, int size)
        {
            var text = Rect(name, parent, geometry).gameObject.AddComponent<Text>();
            text.font = Font; text.fontSize = size; text.text = value;
            text.color = Color.black; text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Button Button(string name, Transform parent, Rect geometry, string label)
        {
            var rect = Rect(name, parent, geometry);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.96f, .91f, .81f, .92f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = Text("Label", rect, new Rect(0, 0, geometry.width, geometry.height), label, 13);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name);

        internal static void ImportSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
