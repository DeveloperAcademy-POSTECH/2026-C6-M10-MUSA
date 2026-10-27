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
            public float dialogueFontSize;
            public string nodeId, dialogue, speaker, badgeKey;
            public Box dialogueRect, speakerRect, badgeRect;
            public Item[] items;
        }
        [Serializable] public class Item
        {
            public string nodeId, name, key;
            public Box rect;
            public float width, height, rotation, alpha;
            public bool vector, cover, bottom, crop;
            public float wPercent = 1, hPercent = 1, leftPercent, topPercent;
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
                foreach (int number in new[] { 1, 2 }.Concat(manifest.frames.Select(f => f.number)))
                    preview.cuts.Add(AssetDatabase.LoadAssetAtPath<StoryCutDefinition>($"{Root}/Opening/Cuts/OP_{number:000}.asset"));
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
            preview.cuts = Enumerable.Range(1, 24).Select(number =>
                AssetDatabase.LoadAssetAtPath<StoryCutDefinition>($"{Root}/Opening/Cuts/OP_{number:000}.asset")).ToList();
            EditorUtility.SetDirty(preview);
            AssetDatabase.SaveAssets();
            Debug.Log("HAPIO_STORY_OPENING_COMPLETE: all 24 cuts connected; cut 22 layout updated while retaining its Motion settings.");
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
            string path = $"{Root}/Opening/Views/OP_{frame.number:000}.prefab";
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
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void BuildFrame(Frame frame)
        {
            string viewPath = $"{Root}/Opening/Views/OP_{frame.number:000}.prefab";
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
                PrefabUtility.SaveAsPrefabAsset(root, viewPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var cut = AssetDatabase.LoadAssetAtPath<StoryCutDefinition>($"{Root}/Opening/Cuts/OP_{frame.number:000}.asset");
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
