using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hapio.Editor
{
    /// <summary>Applies the Figma dialogue, continue cue, and shared wipe without replacing authored prefabs.</summary>
    internal static class StageStoryPolishAssets
    {
        private const string Art = StageStoryAssets.Art + "Polish/";
        private const string WipePath = StageStoryAssets.Root + "/Views/StoryCloudWipe.prefab";
        private const string ManifestPath = StageStoryAssets.Root + "/PolishFrames.json";
        private static readonly string[] Cards = {"Gumiho_SealCard", "Yagwanggui_SealCard", "Jigwi_SealCard"};

        private static readonly string[] Interludes = {"Gumiho_After_01", "Gumiho_After_02", "Yagwanggui_After_01",
            "Yagwanggui_After_02", "Jigwi_After_01", "Jigwi_After_02"};

        internal static void ApplyExisting()
        {
            EnsureWipe();
            Apply(Cards.Concat(Interludes).Concat(new[] {"Ending_06", "Ending_07"}));
        }

        internal static void ApplyExistingInterludes() => Apply(Interludes);

        private static void Apply(IEnumerable<string> ids)
        {
            foreach (var id in ids)
            {
                string path = StageStoryAssets.Root + "/Views/" + id + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Configure(id, root.GetComponent<StoryCutView>());
                    if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                        throw new InvalidOperationException("Could not save " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        internal static void Configure(string id, StoryCutView view)
        {
            if (!File.Exists(ManifestPath)) return;
            var manifest = JsonUtility.FromJson<StageStoryAssets.Manifest>(File.ReadAllText(ManifestPath));
            var patch = manifest.frames.SingleOrDefault(f => f.name == id);
            if (id == "Ending_07")
            {
                var dark = manifest.frames.Single(f => f.name == "Ending_06").nodes.Single(n => n.kind == "solid");
                ConfigureTitle(view, dark);
                return;
            }
            bool interlude = Interludes.Contains(id);
            if (patch == null && !interlude) return;
            if (Cards.Contains(id))
            {
                ConfigureCard(view, patch);
                return;
            }

            if (interlude) SoftenArtwork(view);
            if (id == "Yagwanggui_After_02")
            {
                var ember = view.transform.Find("Jigwi Ember");
                if (ember != null && ember.GetComponent<StoryVisualBeat>()?.motion == StoryVisualBeat.Motion.Ember)
                    UnityEngine.Object.DestroyImmediate(ember.gameObject);
            }
            float imageEnd = view.layers == null || view.layers.Length == 0
                ? 0 : view.layers.Max(l => l != null ? l.EntranceDuration : 0);
            if (id == "Ending_06")
            {
                var dark = patch.nodes.Single(n => n.kind == "solid");
                var shade = Find(view.transform, dark.nodeId);
                shade.GetComponent<Image>().color = ColorFor(dark);
                foreach (var layer in view.layers)
                {
                    layer.isBackground = layer.transform != shade;
                    if (layer.transform == shade)
                    {
                        layer.delay = 0;
                        layer.enterSeconds = 1.05f;
                        layer.enterOffset = Vector2.zero;
                        layer.startHeight = 1;
                        imageEnd = layer.EntranceDuration;
                    }
                }
            }
            foreach (var node in (patch?.nodes ?? Array.Empty<StageStoryAssets.Node>()).Where(n => n.kind != "solid"))
            {
                var rect = Find(view.transform, node.nodeId);
                if (node.kind == "image")
                {
                    if (rect == null) throw new InvalidOperationException("Missing original art layer " + node.nodeId);
                    SetBounds(rect, node.Rect);
                    var artwork = rect.Find("Artwork") as RectTransform;
                    if (artwork != null)
                    {
                        var sprite = artwork.GetComponent<Image>().sprite;
                        float scale = Mathf.Max(node.width / sprite.rect.width, node.height / sprite.rect.height);
                        artwork.sizeDelta = sprite.rect.size * scale;
                        artwork.anchoredPosition = new Vector2((node.width - artwork.sizeDelta.x) / 2,
                            (artwork.sizeDelta.y - node.height) / 2);
                    }
                    continue;
                }
                if (rect == null) rect = StageStoryAssets.Element(view.transform, node);
                rect.SetAsLastSibling();
                Reveal(rect, imageEnd + (id == "Ending_06" ? .30f : .12f), id == "Ending_06" ? .38f : .58f,
                    Vector2.zero);
            }
            if (view.continueButton != null)
            {
                var label = view.continueButton.GetComponentInChildren<Text>(true);
                label.text = "다음 요괴 잡으러 ...";
                // The next-story prompt should not race the character's line.
                Reveal((RectTransform)view.continueButton.transform, imageEnd + .62f, .28f, Vector2.zero);
                view.continueButton.gameObject.SetActive(true);
            }
            if (id == "Jigwi_After_02")
            {
                var prompt = Find(view.transform, "4210:3259");
                prompt.GetComponentInChildren<Text>(true).text = "다음 요괴 잡으러 ...";
                Reveal(prompt, imageEnd + .62f, .28f, Vector2.zero);
            }
            view.waitForEntrance = true;
            CollectBeats(view);
        }

        private static void SoftenArtwork(StoryCutView view)
        {
            int foreground = 0;
            foreach (var layer in view.layers)
            {
                if (layer == null || layer.isBackground) continue;
                layer.softFadeIn = true;
                // A close overlap keeps the cut from building up one isolated element at a time.
                layer.delay = .045f + .012f * Mathf.Min(foreground++, 7);
                layer.enterSeconds = .82f;
                layer.enterOffset = Vector2.zero;
                layer.startHeight = 1;
            }
        }

        private static void ConfigureCard(StoryCutView view, StageStoryAssets.Frame patch)
        {
            var button = view.continueButton;
            if (button == null)
            {
                var target = StoryWorkspaceBuilder.Rect("이야기 계속 Action", view.transform, new Rect(256, 764, 132, 51));
                var hit = target.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                button = target.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                foreach (var node in patch.nodes)
                    StageStoryAssets.Element(target, node, 256, 764);
            }
            button.transform.SetAsLastSibling();
            view.continueButton = button;
            view.requireContinueButton = true;
            view.exitCloudWipe = EnsureWipe();
        }

        private static void ConfigureTitle(StoryCutView view, StageStoryAssets.Node source)
        {
            const string name = "Title fade from ending";
            var rect = (RectTransform)view.transform.Find(name);
            if (rect == null)
            {
                rect = StoryWorkspaceBuilder.Rect(name, view.transform, new Rect(0, 0, 402, 874));
                var shade = rect.gameObject.AddComponent<C6Lab.LabRoundedGraphic>();
                shade.color = new Color(source.color.r, source.color.g, source.color.b);
                shade.raycastTarget = false;
            }
            var oldImage = rect.GetComponent<Image>();
            if (oldImage != null)
            {
                var color = oldImage.color;
                UnityEngine.Object.DestroyImmediate(oldImage);
                var shade = rect.gameObject.AddComponent<C6Lab.LabRoundedGraphic>();
                shade.color = color;
                shade.raycastTarget = false;
            }
            foreach (var layer in view.layers) layer.isBackground = true;
            var beat = GetBeat(rect);
            beat.motion = StoryVisualBeat.Motion.FadeOut;
            beat.duration = .66f;
            view.waitForEntrance = true;
            CollectBeats(view);
        }

        private static StoryCloudWipe EnsureWipe()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(WipePath);
            if (existing != null) return existing.GetComponent<StoryCloudWipe>();
            StoryWorkspaceBuilder.ImportSprite(Art + "CloudWipe.png");
            StoryWorkspaceBuilder.ImportSprite(Art + "ContinueArrow.png");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "CloudWipe.png");
            if (sprite == null) throw new InvalidOperationException("Missing Figma cloud image");
            var root = new GameObject("Story Cloud Wipe", typeof(RectTransform), typeof(RectMask2D), typeof(StoryCloudWipe));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var wipe = root.GetComponent<StoryCloudWipe>();
                wipe.upper = Cloud(root.transform, "Upper cloud [4411:2391]", new Rect(-278, -74, 908, 605), sprite);
                wipe.lower = Cloud(root.transform, "Lower cloud [4411:2411]", new Rect(-230, 412, 811, 540), sprite);
                return PrefabUtility.SaveAsPrefabAsset(root, WipePath).GetComponent<StoryCloudWipe>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static RectTransform Cloud(Transform parent, string name, Rect bounds, Sprite sprite)
        {
            var rect = StoryWorkspaceBuilder.Rect(name, parent, bounds);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return rect;
        }

        private static void SetBounds(RectTransform rect, Rect bounds)
        {
            rect.sizeDelta = bounds.size;
            rect.anchoredPosition = new Vector2(bounds.x + bounds.width * rect.pivot.x,
                -bounds.y - bounds.height * (1 - rect.pivot.y));
        }

        private static RectTransform Find(Transform root, string id) => root.Cast<Transform>()
            .FirstOrDefault(t => t.name.EndsWith("[" + id + "]", StringComparison.Ordinal)) as RectTransform;

        private static Color ColorFor(StageStoryAssets.Node n) => new Color(n.color.r, n.color.g, n.color.b, n.alpha);

        private static StoryVisualBeat GetBeat(RectTransform rect) =>
            rect.GetComponent<StoryVisualBeat>() ?? rect.gameObject.AddComponent<StoryVisualBeat>();

        private static void Reveal(RectTransform rect, float delay, float duration, Vector2 offset)
        {
            var beat = GetBeat(rect);
            beat.motion = StoryVisualBeat.Motion.Reveal;
            beat.delay = delay;
            beat.duration = duration;
            beat.offset = offset;
        }

        private static void CollectBeats(StoryCutView view) =>
            view.beats = view.GetComponentsInChildren<StoryVisualBeat>(true);
    }
}
