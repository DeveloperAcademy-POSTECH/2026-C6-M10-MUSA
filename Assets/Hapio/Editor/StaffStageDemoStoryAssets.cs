using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hapio.Editor
{
    /// <summary>Authors only demo copies and the final page; normal stories, Scenes and game tuning are read-only.</summary>
    public static class StaffStageDemoStoryAssets
    {
        public const string Root = "Assets/Hapio/Resources/Hapio/Demo/Staff";
        public const string FinalPagePath = Root + "/FinalPage.prefab";
        public const string FinalPageConfigPath = Root + "/FinalPageConfig.asset";
        public const string FinalPageArt = "Assets/Hapio/Art/UI/Showcase/";
        public const string FinalPageDestination = "https://apps.apple.com/kr/app/hap-io/id6827923355?l=en-GB";
        private static readonly string[] YokaiNames = { "구미호", "야광귀", "지귀" };

        public static string StageRoot(int stage) => Root + "/Stage" + RequireStage(stage);
        public static string EndingPath(int stage) => StageRoot(stage) + "/GameEnding.asset";
        public static string SummaryViewPath(int stage) => StageRoot(stage) + "/Views/SealRunSummary.prefab";
        public static string SummaryCutPath(int stage) => StageRoot(stage) + "/Cuts/SealRunSummary.asset";
        public static string FirstEndingCutPath(int stage) => StageRoot(stage) + "/Cuts/Ending_01.asset";
        public static string FirstEndingDialogue(int stage) => "이야~\n" + YokaiNames[RequireStage(stage) - 1] + "도 무사히 봉인했네~!";

        [MenuItem("Hapio/Demo/Update Staff Stage Demo Stories and Final Page")]
        public static void CreateOrUpdate()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before authoring demo assets.");
            var source = Require<StorySequence>(GameStageStoryUpgrade.GameEnding);
            if (!source.Validate(out string error) || source.cuts.Count != 7
                || source.cuts[0].cutId != "SealRunSummary" || source.cuts[1].cutId != "Ending_01")
                throw new InvalidOperationException("The source ending needs its summary and six ending cuts. " + error);
            for (int stage = 1; stage <= 3; stage++)
            {
                EnsureFolder(StageRoot(stage) + "/Views");
                EnsureFolder(StageRoot(stage) + "/Cuts");
                CreateSummaryView(stage);
                var summary = CopyAsset(source.cuts[0], SummaryCutPath(stage));
                summary.viewPrefab = Require<GameObject>(SummaryViewPath(stage)).GetComponent<StoryCutView>();
                summary.direction = YokaiNames[stage - 1] + " 한 판의 Host 확정 기록·개인 기여도 → 엔딩 → 시연 마무리.";
                Save(summary);
                var first = CopyAsset(source.cuts[1], FirstEndingCutPath(stage));
                first.pages[0].dialogue = FirstEndingDialogue(stage);
                Save(first);
                var ending = CopyAsset(source, EndingPath(stage));
                ending.storyId = "game-ending-staff-demo-stage-" + stage;
                ending.cuts[0] = summary;
                ending.cuts[1] = first;
                Save(ending);
            }
            UpdateFinalPage();
        }

        [MenuItem("Hapio/Demo/Update Showcase Final Page")]
        public static void UpdateFinalPage()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before authoring demo assets.");
            // This entry point intentionally leaves the retained summary/ending copies and every Scene alone.
            var qr = Require<Sprite>(FinalPageArt + "02cb8.png");
            Require<Sprite>(FinalPageArt + "a5d40.png");
            Require<Sprite>(FinalPageArt + "e101f.svg");
            Require<Sprite>(StageStoryAssets.Art + "624c4.png");
            Require<Sprite>(StageStoryAssets.Art + "5e97a.png");
            EnsureFolder(Root);
            var config = AssetDatabase.LoadAssetAtPath<StaffDemoFinalPageConfig>(FinalPageConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<StaffDemoFinalPageConfig>();
                AssetDatabase.CreateAsset(config, FinalPageConfigPath);
            }
            config.heading = StaffDemoFinalPageConfig.DefaultHeading;
            config.message = StaffDemoFinalPageConfig.DefaultMessage;
            config.qrSprite = qr;
            config.qrCaption = StaffDemoFinalPageConfig.DefaultQrCaption;
            config.returnCaption = StaffDemoFinalPageConfig.DefaultReturnCaption;
            // Decoded from the Figma source QR; the player displays that artwork without manufacturing a code.
            config.destinationUrl = FinalPageDestination;
            Save(config);
            CreateFinalPage();
            Validate();
        }

        public static void Validate() => ValidateRuntimeAssets(path => AssetDatabase.LoadAssetAtPath<Object>(path));

        // Retained stage-specific ending copies are authoring history, not dependencies of the direct-QR player.
        private static void ValidateRuntimeAssets(Func<string, Object> load)
        {
            string[] afterStories = { "AfterGumiho", "AfterYagwanggui", "AfterJigwi" };
            foreach (string name in afterStories)
            {
                var after = RequireRuntimeAsset<StorySequence>(StageStoryAssets.Root + "/" + name + ".asset", load);
                if (!after.Validate(out string error))
                    throw new InvalidOperationException("The staff demo needs its saved card and afterstory: " + name + ". " + error);
                var first = after.cuts[0].viewPrefab;
                var last = after.cuts[after.cuts.Count - 1].viewPrefab;
                var finish = last.GetComponent<StoryStageClearControls>();
                if (first.GetComponentInChildren<StoryCardRecords>(true) == null
                    || first.GetComponentInChildren<StoryCardFlip>(true) == null
                    || finish == null || finish.nextSeal == null || finish.waiting == null)
                    throw new InvalidOperationException("The staff demo afterstory needs its card records and final Finish action: " + name);
            }
            var config = RequireRuntimeAsset<StaffDemoFinalPageConfig>(FinalPageConfigPath, load);
            var page = RequireRuntimeAsset<GameObject>(FinalPagePath, load).GetComponent<StaffDemoFinalPage>();
            if (page == null || page.heading == null || page.message == null || page.qrCaption == null
                || page.qrImage == null || page.placeholder == null || page.returnButton == null
                || page.GetComponent<StoryPageFit>() == null)
                throw new InvalidOperationException("The saved staff demo final page is incomplete.");
            if (config.qrSprite == null || !config.HasDestination)
                throw new InvalidOperationException("The saved showcase page needs its real QR sprite and a verified absolute HTTP(S) destination.");
        }

        private static T RequireRuntimeAsset<T>(string path, Func<string, Object> load) where T : Object => load(path) as T
            ?? throw new InvalidOperationException("Missing saved asset: " + path);

        private static void CreateSummaryView(int stage)
        {
            var root = PrefabUtility.LoadPrefabContents(SealRunSummaryAssets.ViewPath);
            try
            {
                var summary = root.GetComponent<SealRunSummary>();
                var view = root.GetComponent<StoryCutView>();
                if (summary == null || view == null || summary.stageTimes.Length != 3 || summary.contributions.Length != 3)
                    throw new InvalidOperationException("The source summary needs its three canonical stage rows.");
                int keep = stage - 1;
                summary.stageIndices = new[] { stage };
                summary.stageTimes = new[] { summary.stageTimes[keep] };
                summary.contributions = new[] { summary.contributions[keep] };
                Label(root.transform, "Subtitle").text = YokaiNames[keep] + " 봉인 완료";
                Label(root.transform, "RecordsTitle").text = YokaiNames[keep] + " 봉인 기록";
                for (int index = 0; index < 3; index++)
                    if (index != keep)
                        foreach (string key in new[] { "SealedYokai", "Yokai", "Time", "Contribution", "RowRule" })
                            Object.DestroyImmediate(RequireNode(root.transform, key + index).gameObject);
                foreach (string key in new[] { "Yokai", "Time", "Contribution" })
                    SetTop(RequireNode(root.transform, key + keep), 452);
                SetTop(RequireNode(root.transform, "RowRule" + keep), 483.18f);
                var medal = RequireNode(root.transform, "SealedYokai" + keep);
                medal.anchoredPosition = new Vector2((402f - medal.sizeDelta.x) / 2f, medal.anchoredPosition.y);
                medal.GetComponent<StoryVisualBeat>().delay = .12f;
                view.beats = view.beats.Where(beat => beat != null).ToArray();
                root.name = "StaffDemoStage" + stage + "SealRunSummary";
                PrefabUtility.SaveAsPrefabAsset(root, SummaryViewPath(stage));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void CreateFinalPage()
        {
            var root = new GameObject("StaffDemoFinalPage", typeof(RectTransform), typeof(StoryPageFit));
            try
            {
                var view = root.AddComponent<StaffDemoFinalPage>();
                RectTransform page = Rect("Page", root.transform, new Rect(0, 0, 402, 874));
                var background = page.gameObject.AddComponent<UnityEngine.UI.Image>();
                background.color = GameBattleUiColors.Light; // TextColor/SecondaryLightIvory/200
                background.raycastTarget = true;
                page.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();

                // Figma UI / 시연용 5706:14518. Decorative art is clipped to the authored 402×874 page.
                Picture(page, "Parchment", new Rect(-74, -167, 704, 1041),
                    Require<Sprite>(StageStoryAssets.Art + "624c4.png"));
                var cloud = Require<Sprite>(StageStoryAssets.Art + "5e97a.png");
                Picture(page, "CloudLeft", new Rect(-250, 167, 337, 112), cloud);
                Picture(page, "CloudLowerLeft", new Rect(-175, 744, 337, 112), cloud);
                Picture(page, "CloudUpperRight", new Rect(286, 31, 235, 78), cloud);
                Picture(page, "CloudLowerRight", new Rect(304, 666, 235, 78), cloud);

                view.heading = FinalText(page, "Heading", StaffDemoFinalPageConfig.DefaultHeading,
                    new Rect(44, 141, 314, 44), 34, true, Color.black);
                view.message = FinalText(page, "Message", StaffDemoFinalPageConfig.DefaultMessage,
                    new Rect(64, 203, 276, 30), 20, true, Hex("68645C"));

                RectTransform gourd = Rect("GourdTop", page, new Rect(77, 267, 247.421f, 138.201f));
                gourd.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
                Picture(gourd, "Artwork", new Rect(-.0008f * 247.421f, 0, 1.0016f * 247.421f, 1.5294f * 138.201f),
                    Require<Sprite>(FinalPageArt + "a5d40.png"));
                Picture(page, "GourdSeparator", new Rect(119.56f, 392.36f, 162.287f, 4.02f),
                    Require<Sprite>(FinalPageArt + "e101f.svg"));

                view.qrImage = Picture(page, "QRImage", new Rect(109.25f, 396.52f, 182.009f, 185.388f),
                    Require<Sprite>(FinalPageArt + "02cb8.png"));
                view.qrImage.preserveAspect = true;
                var border = Rect("QRBorder", view.qrImage.transform, new Rect(0, 0, 182.009f, 185.388f))
                    .gameObject.AddComponent<GameUiFrame>();
                border.radius = 12.582f; border.stroke = 5.033f;
                border.color = Color.black; border.raycastTarget = false;

                // Kept for an explicitly unconfigured/null preview; the shipped page always has real QR art.
                RectTransform placeholder = Rect("QRPlaceholder", page, new Rect(109.25f, 396.52f, 182.009f, 185.388f));
                var placeholderFill = placeholder.gameObject.AddComponent<UnityEngine.UI.Image>();
                placeholderFill.color = GameLobbySeatColors.Disabled; // TextColor/PrimaryBrown/300
                placeholderFill.raycastTarget = false;
                FinalText(placeholder, "PlaceholderCaption", "QR\n준비 중", new Rect(8, 45, 166.009f, 90),
                    28, true, GameLobbySeatColors.Ink);
                view.placeholder = placeholder.gameObject;
                view.placeholder.SetActive(false);
                view.qrCaption = FinalText(page, "QRCaption", StaffDemoFinalPageConfig.DefaultQrCaption,
                    new Rect(100.8f, 608.91f, 200.4f, 30), 21.472f, false, Color.black);

                RectTransform action = Rect("ReturnToSelection", page, new Rect(27, 768, 349, 58));
                var fill = action.gameObject.AddComponent<C6Lab.LabRoundedGraphic>();
                fill.radius = 27; fill.color = Color.white; fill.raycastTarget = true;
                view.returnButton = action.gameObject.AddComponent<UnityEngine.UI.Button>();
                view.returnButton.targetGraphic = fill;
                var colors = view.returnButton.colors;
                // TextColor/DarkRed/500*** and SecondaryLightIvory/100 use their canonical token components.
                colors.normalColor = colors.highlightedColor = colors.selectedColor = GameLobbySeatColors.Ready;
                colors.pressedColor = Hex("692B21"); // TextColor/DarkRed/700
                colors.disabledColor = GameLobbySeatColors.Ready; colors.colorMultiplier = 1;
                view.returnButton.colors = colors;
                FinalText(action, "Caption", StaffDemoFinalPageConfig.DefaultReturnCaption,
                    new Rect(8, 0, 333, 58), 24, true, GameLobbySeatColors.LightText, TextAnchor.MiddleCenter);
                PrefabUtility.SaveAsPrefabAsset(root, FinalPagePath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static UnityEngine.UI.Image Picture(Transform parent, string name, Rect bounds, Sprite sprite)
        {
            var image = Rect(name, parent, bounds).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static UnityEngine.UI.Text FinalText(Transform parent, string name, string value, Rect bounds,
            float size, bool bold, Color color, TextAnchor alignment = TextAnchor.UpperCenter)
        {
            var placement = Rect(name, parent, bounds);
            var text = Rect("Text", placement, new Rect(0, 0, bounds.width, bounds.height))
                .gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = Require<Font>("Assets/Lab/Art/Fonts/" + (bold ? "ShillaCultureBold.otf" : "Shilla_Culture(M).otf"));
            // Match StageStoryAssets.Element: integer visual font snapping with a fixed, higher raster size.
            text.fontSize = Mathf.CeilToInt(Mathf.Max(32, size));
            float scale = Mathf.FloorToInt(size + .5f) / (float)text.fontSize;
            text.rectTransform.localScale = Vector3.one * scale;
            text.rectTransform.sizeDelta /= scale;
            text.text = value; text.color = color; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.alignment = alignment;
            return text;
        }

        private static Color Hex(string value) => ColorUtility.TryParseHtmlString("#" + value, out Color color)
            ? color : throw new InvalidOperationException("Invalid final-page colour: " + value);

        private static RectTransform Rect(string name, Transform parent, Rect bounds)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            StoryStageReadyView.Place(rect, bounds);
            return rect;
        }
        private static int RequireStage(int stage) => stage >= 1 && stage <= 3 ? stage : throw new ArgumentOutOfRangeException(nameof(stage));
        private static void SetTop(RectTransform rect, float y) => rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -y);
        private static RectTransform FindNode(Transform root, string key) => root.Cast<Transform>()
            .FirstOrDefault(child => child.name == key || child.name.StartsWith(key + " [", StringComparison.Ordinal)) as RectTransform;
        private static RectTransform RequireNode(Transform root, string key) => FindNode(root, key)
            ?? throw new InvalidOperationException("Missing source summary element: " + key);
        private static UnityEngine.UI.Text Label(Transform root, string key) => RequireNode(root, key)
            .GetComponentInChildren<UnityEngine.UI.Text>(true) ?? throw new InvalidOperationException("Missing text: " + key);
        private static T Require<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing saved asset: " + path);
        private static T CopyAsset<T>(T source, string path) where T : ScriptableObject
        {
            var copy = AssetDatabase.LoadAssetAtPath<T>(path);
            if (copy == null)
            {
                copy = Object.Instantiate(source); copy.name = source.name;
                AssetDatabase.CreateAsset(copy, path);
            }
            else EditorUtility.CopySerialized(source, copy);
            return copy;
        }
        private static void Save(Object asset) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
