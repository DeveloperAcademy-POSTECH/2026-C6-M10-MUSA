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
    /// <summary>One-time import of numbered Figma frames. Existing authored prefabs are never rebuilt.</summary>
    public static class StoryBatchBuilder
    {
        private const string Root = StoryWorkspaceBuilder.Root;
        private const string MaterialPath = "Assets/Hapio/Art/Story/Materials/FigmaPaperBackground.mat";
        private const string Art = "Assets/Hapio/Art/Story/Opening/Imported/";
        public const string ManifestPath = Root + "/Opening/ImportedFrames.json";
        public const string PreviewPath = Root + "/Opening/BatchPreview.asset";
        [Serializable] public class Manifest { public Frame[] frames; }
        [Serializable] public class Box
        {
            public float x, y, width, height;
            public Rect Rect => new Rect(x, y, width, height);
        }
        [Serializable] public class Frame
        {
            public int number;
            public string cutId;
            public string CutId => string.IsNullOrEmpty(cutId) ? $"OP_{number:000}" : cutId;
            public float dialogueFontSize;
            public string nodeId, dialogue, speaker, badgeKey;
            public Box dialogueRect, speakerRect, badgeRect;
            public Item[] items;
            public ArtText[] texts;
        }
        [Serializable] public class Item
        {
            public string nodeId, name, key;
            public Box rect;
            public float width, height, rotation, alpha;
            public bool vector, cover, bottom, crop;
            public float wPercent = 1, hPercent = 1, leftPercent, topPercent;
        }

        [Serializable] public class ArtText
        {
            public string nodeId, name, text, parentNodeId, fontPath;
            public Box rect;
            public float width, height, rotation, fontSize, lineHeight;
        }

        private static void AddArtTexts(Transform root, Frame frame)
        {
            if (frame.texts == null) return;
            foreach (var item in frame.texts)
            {
                var source = frame.items.Single(image => image.nodeId == item.parentNodeId);
                var parent = root.Find(source.name + " [" + source.nodeId + "] Placement/Motion");
                string name = item.name + " [" + item.nodeId + "]";
                var old = parent.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var bounds = item.rect.Rect;
                var rect = StoryWorkspaceBuilder.Rect(name, parent, new Rect(
                    bounds.center.x - source.rect.x - item.width * .5f,
                    bounds.center.y - source.rect.y - item.height * .5f, item.width, item.height));
                rect.pivot = Vector2.one * .5f;
                rect.anchoredPosition += new Vector2(item.width * .5f, -item.height * .5f);
                rect.localRotation = Quaternion.Euler(0, 0, item.rotation);
                var textRect = StoryWorkspaceBuilder.Rect("Text", rect, new Rect(0, 0, item.width, item.height));
                var text = textRect.gameObject.AddComponent<Text>();
                text.font = AssetDatabase.LoadAssetAtPath<Font>(item.fontPath);
                // Render tiny scroll lettering from a larger glyph atlas for clear strokes.
                text.fontSize = Mathf.Max(32, Mathf.CeilToInt(item.fontSize));
                float scale = item.fontSize / text.fontSize;
                textRect.localScale = new Vector3(scale, scale, 1);
                textRect.sizeDelta /= scale;
                text.alignment = TextAnchor.MiddleLeft;
                text.color = Color.black;
                text.raycastTarget = false;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                // Match Figma's explicit line height while retaining editable native text.
                var generator = new TextGenerator();
                generator.Populate("가\n가", text.GetGenerationSettings(new Vector2(1000, 1000)));
                if (generator.lines.Count > 1)
                {
                    float advance = generator.lines[0].topY - generator.lines[1].topY;
                    if (advance > 0) text.lineSpacing = item.lineHeight / (scale * advance);
                }
                text.text = item.text;
            }
        }

        [MenuItem("Hapio/Story/Import Prepared Figma Cuts 6-24")]
        public static void Build()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            foreach (string path in Directory.GetFiles(Art, "*.png")) StoryWorkspaceBuilder.ImportSprite(path);
            var paperMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (paperMaterial == null)
            {
                paperMaterial = new Material(Shader.Find("Hapio/Story/Figma Paper Background"));
                AssetDatabase.CreateAsset(paperMaterial, MaterialPath);
            }
            foreach (var frame in manifest.frames) BuildFrame(frame);
            var preview = AssetDatabase.LoadAssetAtPath<StorySequence>(PreviewPath);
            if (preview == null)
            {
                preview = ScriptableObject.CreateInstance<StorySequence>();
                preview.storyId = "opening-prepared-preview";
                foreach (string id in new[] { "OP_001", "OP_002" }.Concat(manifest.frames.Select(f => f.CutId)))
                    preview.cuts.Add(AssetDatabase.LoadAssetAtPath<StoryCutDefinition>($"{Root}/Opening/Cuts/{id}.asset"));
                AssetDatabase.CreateAsset(preview, PreviewPath);
            }
            AssetDatabase.SaveAssets();
            BindPreview(preview);
            Debug.Log("HAPIO_STORY_BATCH_READY: Figma 6-24 imported; preview order 1, 2, 6-24. Existing artwork remains editable and is preserved on rerun.");
        }

        [MenuItem("Hapio/Story/Complete Opening And Update Cut 22")]
        public static void CompleteOpening()
        {
            Build(); // Missing art only; existing cuts and dialogue are preserved.
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            UpdateFrameGeometry(manifest.frames.Single(f => f.number == 22));
            var preview = AssetDatabase.LoadAssetAtPath<StorySequence>(PreviewPath);
            preview.cuts = new[] { "OP_001", "OP_002" }.Concat(manifest.frames.Select(f => f.CutId)).Select(id =>
                AssetDatabase.LoadAssetAtPath<StoryCutDefinition>($"{Root}/Opening/Cuts/{id}.asset")).ToList();
            EditorUtility.SetDirty(preview);
            AssetDatabase.SaveAssets();
            Debug.Log("HAPIO_STORY_OPENING_COMPLETE: manifest cuts connected; cut 22 layout updated while retaining its Motion settings.");
        }

        private static void PlaceLayer(StoryLayer layer, Rect bounds)
        {
            var placement = (RectTransform)layer.transform.parent;
            placement.anchoredPosition = new Vector2(bounds.x, -bounds.y);
            placement.sizeDelta = bounds.size;
            var motion = (RectTransform)layer.transform;
            motion.sizeDelta = bounds.size;
            motion.anchoredPosition = new Vector2(bounds.width * .5f, -bounds.height);
        }

        private static void UpdateFrameGeometry(Frame frame)
        {
            string path = $"{Root}/Opening/Views/{frame.CutId}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<StoryCutView>();
                for (int i = 0; i < frame.items.Length; i++)
                {
                    var item = frame.items[i];
                    if (item.vector)
                    {
                        int end = i;
                        Rect bounds = item.rect.Rect;
                        while (end + 1 < frame.items.Length && frame.items[end + 1].vector)
                        {
                            end++;
                            var next = frame.items[end].rect.Rect;
                            bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, next.xMin), Mathf.Min(bounds.yMin, next.yMin),
                                Mathf.Max(bounds.xMax, next.xMax), Mathf.Max(bounds.yMax, next.yMax));
                        }
                        var placement = root.transform.Find("Ink Details " + item.nodeId + " Placement");
                        if (placement == null) throw new InvalidOperationException("Missing source detail group " + item.nodeId);
                        var layer = placement.GetComponentInChildren<StoryLayer>(true);
                        PlaceLayer(layer, bounds);
                        while (layer.transform.childCount > 0) Object.DestroyImmediate(layer.transform.GetChild(0).gameObject);
                        for (int j = i; j <= end; j++) AddImage(layer.transform, frame.items[j], bounds.position);
                        i = end;
                    }
                    else
                    {
                        var placement = root.transform.Find(item.name + " [" + item.nodeId + "] Placement");
                        if (placement == null) throw new InvalidOperationException("Missing source layer " + item.nodeId);
                        var layer = placement.GetComponentInChildren<StoryLayer>(true);
                        PlaceLayer(layer, item.rect.Rect);
                        while (layer.transform.childCount > 0) Object.DestroyImmediate(layer.transform.GetChild(0).gameObject);
                        layer.artwork = AddImage(layer.transform, item, item.rect.Rect.position);
                    }
                }
                AddArtTexts(root.transform, frame);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void BuildFrame(Frame frame)
        {
            string viewPath = $"{Root}/Opening/Views/{frame.CutId}.prefab";
            var root = PrefabUtility.LoadPrefabContents(viewPath);
            try
            {
                var view = root.GetComponent<StoryCutView>();
                if (view.GetComponentsInChildren<Image>(true).Any(i => i.sprite != null)) return;
                while (root.transform.childCount > 0) Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
                var backing = StoryWorkspaceBuilder.Rect("Paper Base", root.transform, new Rect(0, 0, 402, 874)).gameObject.AddComponent<Image>();
                backing.color = Color.white;
                backing.raycastTarget = false;
                var layers = new List<StoryLayer>();
                for (int i = 0; i < frame.items.Length; i++)
                {
                    var item = frame.items[i];
                    if (item.vector)
                    {
                        // Consecutive ink details share one animation, preserving drawing order.
                        int end = i;
                        Rect bounds = item.rect.Rect;
                        while (end + 1 < frame.items.Length && frame.items[end + 1].vector)
                        {
                            end++;
                            var next = frame.items[end].rect.Rect;
                            bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, next.xMin), Mathf.Min(bounds.yMin, next.yMin),
                                Mathf.Max(bounds.xMax, next.xMax), Mathf.Max(bounds.yMax, next.yMax));
                        }
                        var layer = NewLayer(root.transform, layers, "Ink Details " + item.nodeId, bounds, true);
                        for (int j = i; j <= end; j++) AddImage(layer.transform, frame.items[j], bounds.position);
                        i = end;
                    }
                    else
                    {
                        bool background = item.rect.width >= 400 && item.rect.height >= 870;
                        var layer = NewLayer(root.transform, layers, item.name + " [" + item.nodeId + "]", item.rect.Rect, background);
                        layer.artwork = AddImage(layer.transform, item, item.rect.Rect.position);
                    }
                }
                view.layers = layers.ToArray();
                view.hideEmptyDialogue = true;
                view.sourceFigmaNumber = frame.number;
                view.sourceFigmaNodeId = frame.nodeId;
                view.overrideDialogueLayout = true;
                view.dialogueFontSize = frame.dialogueFontSize;
                if (frame.dialogueRect != null) view.dialogueRect = frame.dialogueRect.Rect;
                if (frame.speakerRect != null) view.speakerRect = frame.speakerRect.Rect;
                if (frame.badgeRect != null) view.speakerBadgeRect = frame.badgeRect.Rect;
                view.speakerBadge = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Hapio/Art/Story/Opening/OP_002/SpeakerBadge.png");
                view.speakerColor = Color.white;
                AddArtTexts(root.transform, frame);
                PrefabUtility.SaveAsPrefabAsset(root, viewPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var cut = AssetDatabase.LoadAssetAtPath<StoryCutDefinition>($"{Root}/Opening/Cuts/{frame.CutId}.asset");
            if (cut.pages == null) cut.pages = new List<StoryDialoguePage>();
            if (cut.pages.Count == 0) cut.pages.Add(new StoryDialoguePage());
            if (cut.pages.Count == 1 && string.IsNullOrWhiteSpace(cut.pages[0].dialogue))
            {
                cut.pages[0].speaker = frame.speaker;
                cut.pages[0].dialogue = frame.dialogue;
                cut.pages[0].autoReadSeconds = string.IsNullOrWhiteSpace(frame.dialogue) ? 3 : 6;
                EditorUtility.SetDirty(cut);
            }
        }

        private static StoryLayer NewLayer(Transform parent, List<StoryLayer> layers, string name, Rect bounds, bool stable)
        {
            var layer = StoryWorkspaceBuilder.Layer(parent, name, bounds, null,
                stable ? 0 : Mathf.Min(.1f + layers.Count * .06f, .5f), stable ? .25f : .5f,
                stable ? Vector2.zero : new Vector2(0, -8), stable ? 1 : .94f);
            Object.DestroyImmediate(layer.artwork.gameObject);
            layer.artwork = null;
            layers.Add(layer);
            return layer;
        }

        private static Image AddImage(Transform parent, Item item, Vector2 origin)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + item.key);
            if (sprite == null) throw new InvalidOperationException("Missing prepared source: " + item.key);
            var frame = StoryWorkspaceBuilder.Rect(item.name + " [" + item.nodeId + "]", parent,
                new Rect(item.rect.x - origin.x + (item.rect.width - item.width) * .5f,
                    item.rect.y - origin.y + (item.rect.height - item.height) * .5f, item.width, item.height));
            // Rotate around the centre, retaining the exact Figma bounding-box centre.
            frame.pivot = Vector2.one * .5f;
            frame.anchoredPosition += new Vector2(item.width * .5f, -item.height * .5f);
            frame.localRotation = Quaternion.Euler(0, 0, item.rotation);
            Rect imageRect = new Rect(0, 0, item.width, item.height);
            bool mask = false;
            if (item.crop)
            {
                mask = true;
                imageRect = new Rect(item.width * item.leftPercent, item.height * item.topPercent,
                    item.width * item.wPercent, item.height * item.hPercent);
            }
            else if (item.cover)
            {
                float scale = Mathf.Max(item.width / sprite.rect.width, item.height / sprite.rect.height);
                Vector2 size = sprite.rect.size * scale;
                imageRect = new Rect((item.width - size.x) * .5f, item.bottom ? item.height - size.y : (item.height - size.y) * .5f, size.x, size.y);
                mask = size.x > item.width + .01f || size.y > item.height + .01f;
            }
            if (mask) frame.gameObject.AddComponent<RectMask2D>();
            var image = StoryWorkspaceBuilder.Rect("Artwork", frame, imageRect).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = new Color(1, 1, 1, item.alpha);
            image.raycastTarget = false;
            if (item.rect.width >= 400 && item.rect.height >= 870 && item.alpha < 1)
                image.material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            return image;
        }

        private static void BindPreview(StorySequence sequence)
        {
            Scene scene = SceneManager.GetSceneByPath(StoryWorkspaceBuilder.PreviewScene);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(StoryWorkspaceBuilder.PreviewScene, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save the preview scene before connecting the prepared cuts.");
                var player = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<StoryPlayer>(true)).Single();
                string previous = AssetDatabase.GetAssetPath(player.sequence);
                if (previous == StorySecondCutBuilder.PreviewPath || previous == Root + "/Opening/FirstCutPreview.asset" || previous == PreviewPath)
                {
                    player.sequence = sequence;
                    EditorUtility.SetDirty(player);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [MenuItem("Hapio/Story/Select Prepared Opening Preview")]
        private static void SelectPreview() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<StorySequence>(PreviewPath);
    }
}
