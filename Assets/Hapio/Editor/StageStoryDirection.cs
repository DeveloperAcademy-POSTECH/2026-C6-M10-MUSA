using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hapio.Editor
{
    internal static class StageStoryDirection
    {
        private static AudioClip Sound(string name) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>(StageStoryAssets.Art + "Audio/" + name + ".wav");

        internal static void Configure(string id, StoryCutView view)
        {
            var root = view.transform;
            if (id == "Failure_01")
            {
                Still(view);
                Beat(Node(root, "4210:2874"), StoryVisualBeat.Motion.Impact, .42f, .4f, "EmptySeal");
            }
            else if (id == "Failure_Dashboard")
            {
                // Show the results and retry controls while the gauge counts up independently.
                Still(view);
                var text = view.GetComponent<StorySealGauge>().percentage.rectTransform;
                // Pulse the authored label around its centre without moving its baseline.
                if (text.pivot == new Vector2(0, 1))
                {
                    var half = text.sizeDelta * .5f;
                    text.anchoredPosition += new Vector2(half.x, -half.y) * text.localScale.x;
                    text.pivot = Vector2.one * .5f;
                }
            }
            else if (id.EndsWith("_SealCard", System.StringComparison.Ordinal))
            {
                var flip = view.GetComponentInChildren<StoryCardFlip>();
                var drop = Beat(flip.card, StoryVisualBeat.Motion.PaperDrop, 0, .42f, "PaperLand");
                drop.offset = new Vector2(-35, 58);
                drop.interactionDelay = .63f;
                flip.flipSound = Sound("PaperFlip");
                var hint = root.GetComponentsInChildren<Text>(true).Single(t => t.text == "좌우로 밀어 부적 뒤집기");
                hint.text = "밀어서 뒤집기";
                flip.swipeHint = hint.transform.parent.gameObject;
                Stamp(flip.front.transform);
                WireRecords(flip);
            }
            else if (id == "Gumiho_After_02")
            {
                var light = Glow(root, "Yagwanggui Light", new Rect(335, 191, 44, 44),
                    new Color(1f, .83f, .39f));
                Beat(light, StoryVisualBeat.Motion.LightOut, .48f, .25f, "LightOut");
            }

            if (id == "Ending_01")
            {
                Still(view);
                var animated = new[] {"4210:3323", "4210:3325", "4210:3326", "4210:3327"};
                foreach (var layer in view.layers.Where(l => animated.Any(key => l.name.Contains(key))))
                {
                    layer.isBackground = false;
                    layer.delay = 0;
                    layer.enterSeconds = .48f;
                    layer.startHeight = .95f;
                    layer.enterOffset = new Vector2(0, -14);
                }
            }
            if (id.StartsWith("Ending_", System.StringComparison.Ordinal) && id != "Ending_07")
            {
                view.music = Sound("EndingBed");
                view.musicVolume = id == "Ending_01" ? .32f : .075f;
            }
            WireTeaserButton(id, view);
            view.beats = root.GetComponentsInChildren<StoryVisualBeat>(true);
        }

        internal static void ConfigureCut(StoryCutDefinition cut)
        {
            if (cut.cutId == "Ending_01")
            {
                if (cut.PageCount == 1 && cut.GetPage(0).speaker == "불이")
                    cut.pages.Add(new StoryDialoguePage {speaker = "만복", dialogue = "얘들아.. 배 안고파?\n우리 몇마리 더 잡고 밥 먹을까?"});
                var manbok = cut.pages.FirstOrDefault(p => p != null && (p.speaker == "만복" || p.speaker == "만복이"));
                if (manbok != null && manbok.voice == null) manbok.voice = Sound("Hungry");
            }
            else if (cut.cutId == "Ending_04" && cut.pages.Count > 0 && cut.pages[0].voice == null)
                cut.pages[0].voice = Sound("BrokenGourd");
        }

        private static bool WireTeaserButton(string id, StoryCutView view)
        {
            if (view.continueButton != null || (id != "Gumiho_After_02" && id != "Yagwanggui_After_02"))
                return false;
            var caption = Node(view.transform, id == "Gumiho_After_02" ? "4210:2883" : "4210:3271");
            var label = caption.GetComponentInChildren<Text>();
            label.text = "다음 요괴 잡으러 ...";
            var hit = StoryWorkspaceBuilder.Rect("Continue Hit Area", caption,
                new Rect(-8, -10, caption.sizeDelta.x + 16, Mathf.Max(44, caption.sizeDelta.y + 20)));
            var surface = hit.gameObject.AddComponent<Image>();
            surface.color = Color.clear;
            view.continueButton = caption.gameObject.AddComponent<Button>();
            view.continueButton.targetGraphic = label;
            return true;
        }

        private static void Still(StoryCutView view)
        {
            foreach (var layer in view.layers) layer.isBackground = true;
        }

        private static RectTransform Node(Transform root, string id) =>
            (RectTransform)root.Cast<Transform>().Single(t => t.name.EndsWith("[" + id + "]", System.StringComparison.Ordinal));

        private static StoryVisualBeat Beat(RectTransform rect, StoryVisualBeat.Motion motion, float delay, float duration, string sound)
        {
            var beat = rect.gameObject.AddComponent<StoryVisualBeat>();
            beat.motion = motion;
            beat.delay = delay;
            beat.duration = duration;
            beat.sound = Sound(sound);
            return beat;
        }

        private static RectTransform Glow(Transform parent, string name, Rect bounds, Color color)
        {
            var glow = StoryWorkspaceBuilder.Rect(name, parent, bounds);
            glow.pivot = Vector2.one * .5f;
            glow.anchoredPosition += new Vector2(bounds.width * .5f, -bounds.height * .5f);
            for (int i = 0; i < 4; i++)
            {
                float inset = bounds.width * (i == 3 ? .41f : i * .12f);
                float size = bounds.width - inset * 2;
                var spot = StoryWorkspaceBuilder.Rect("Glow " + i, glow, new Rect(inset, inset, size, size))
                    .gameObject.AddComponent<C6Lab.LabRoundedGraphic>();
                spot.radius = size * .5f;
                spot.color = Color.Lerp(color, new Color(1f, .95f, .7f), i * .23f);
                spot.color = new Color(spot.color.r, spot.color.g, spot.color.b, i == 3 ? .92f : .12f + i * .07f);
                spot.raycastTarget = false;
            }
            return glow;
        }

        private static void Stamp(Transform front)
        {
            var backing = (RectTransform)front.Cast<Transform>().Single(t =>
                t.name.StartsWith("image 1638 [", System.StringComparison.Ordinal));
            var label = front.GetComponentsInChildren<Text>(true).Single(t => t.text == "봉\n인");
            var lettering = (RectTransform)label.transform.parent;
            var topLeft = backing.anchoredPosition + new Vector2(-backing.sizeDelta.x, backing.sizeDelta.y) * .5f;
            var stamp = StoryWorkspaceBuilder.Rect("Original Seal Stamp", front,
                new Rect(topLeft.x, -topLeft.y, backing.sizeDelta.x, backing.sizeDelta.y));
            stamp.pivot = Vector2.one * .5f;
            stamp.anchoredPosition = backing.anchoredPosition;
            stamp.SetSiblingIndex(backing.GetSiblingIndex());
            // Move the original red backing and lettering together, retaining their final placement.
            foreach (var layer in new[] {backing, lettering})
            {
                Vector2 position = layer.anchoredPosition - topLeft;
                layer.SetParent(stamp, false);
                layer.anchoredPosition = position;
            }
            var beat = Beat(stamp, StoryVisualBeat.Motion.SealStamp, .61f, .23f, "RedSeal");
            beat.offset = new Vector2(0, 20);
            beat.volume = .92f;
        }

        private static void WireRecords(StoryCardFlip flip)
        {
            var labels = flip.back.GetComponentsInChildren<Text>(true);
            var records = flip.gameObject.AddComponent<StoryCardRecords>();
            records.sealRate = labels.Single(t => t.text == "100%" && Y(t) < 200);
            records.timeRemaining = labels.Single(t => t.text == "16초");
            records.players = labels.Where(t => t.text.StartsWith("P")).OrderBy(Y).ToArray();
            records.values = labels.Where(t => Y(t) > 200 && Y(t) < 380 &&
                ((RectTransform)t.transform.parent).anchoredPosition.x > 194).OrderBy(Y).ToArray();
            if (records.players.Length != 5 || records.values.Length != 5)
                throw new System.InvalidOperationException("Unexpected seal card record layout: " + flip.transform.root.name);
        }

        private static float Y(Text label) => -((RectTransform)label.transform.parent).anchoredPosition.y;

        // Apply to the previously imported assets without rebuilding prefabs or dialogue.
        internal static void ApplyExisting()
        {
            foreach (var id in new[] {"Failure_01", "Failure_Dashboard", "Gumiho_SealCard", "Yagwanggui_SealCard",
                         "Jigwi_SealCard", "Gumiho_After_02", "Yagwanggui_After_02", "Ending_01", "Ending_02",
                         "Ending_03", "Ending_04", "Ending_05", "Ending_06"})
            {
                string path = StageStoryAssets.Root + "/Views/" + id + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view = root.GetComponent<StoryCutView>();
                    if (view.beats == null || view.beats.Length == 0)
                    {
                        Configure(id, view);
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    else if (WireTeaserButton(id, view))
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var id in new[] {"Ending_01", "Ending_04"})
            {
                var cut = AssetDatabase.LoadAssetAtPath<StoryCutDefinition>(StageStoryAssets.Root + "/Cuts/" + id + ".asset");
                ConfigureCut(cut);
                EditorUtility.SetDirty(cut);
                AssetDatabase.SaveAssetIfDirty(cut);
            }
            var scene = SceneManager.GetSceneByPath(StageStoryAssets.Preview);
            if (!scene.isLoaded || scene.isDirty) return;
            AddPreviewListener(scene);
            if (scene.isDirty) EditorSceneManager.SaveScene(scene);
        }

        internal static void AddPreviewListener(Scene scene)
        {
            if (scene.GetRootGameObjects().Any(o => o.GetComponentInChildren<AudioListener>(true) != null)) return;
            var player = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<StoryPlayer>(true)).Single();
            player.gameObject.AddComponent<AudioListener>();
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
