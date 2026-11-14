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
            if (AssetDatabase.LoadAssetAtPath<StaffDemoFinalPageConfig>(FinalPageConfigPath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<StaffDemoFinalPageConfig>(), FinalPageConfigPath);
            CreateFinalPage();
            Validate();
        }

        public static void Validate()
        {
            var source = Require<StorySequence>(GameStageStoryUpgrade.GameEnding);
            for (int stage = 1; stage <= 3; stage++)
            {
                var ending = Require<StorySequence>(EndingPath(stage));
                if (!ending.Validate(out string error) || ending.cuts.Count != 7
                    || ending.cuts[0] != Require<StoryCutDefinition>(SummaryCutPath(stage))
                    || ending.cuts[1] != Require<StoryCutDefinition>(FirstEndingCutPath(stage))
                    || ending.cuts[1] == source.cuts[1]
                    || ending.cuts[1].GetPage(0).dialogue != FirstEndingDialogue(stage))
                    throw new InvalidOperationException("Staff demo needs truthful, selected-stage summary and ending: " + stage + ". " + error);
                for (int i = 2; i < 7; i++)
                    if (ending.cuts[i] != source.cuts[i])
                        throw new InvalidOperationException("Staff demo must reuse the original ending picture: " + i);
                var prefab = Require<GameObject>(SummaryViewPath(stage));
                var summary = prefab.GetComponent<SealRunSummary>();
                var view = prefab.GetComponent<StoryCutView>();
                if (summary == null || view == null || summary.stageIndices == null
                    || !summary.stageIndices.SequenceEqual(new[] { stage })
                    || summary.stageTimes == null || summary.stageTimes.Length != 1 || summary.stageTimes[0] == null
                    || summary.contributions == null || summary.contributions.Length != 1 || summary.contributions[0] == null
                    || summary.totalTime == null || !view.requireContinueButton || view.continueButton == null
                    || view.exitCloudWipe == null || ending.cuts[0].viewPrefab != view
                    || view.beats.Count(beat => beat != null && beat.motion == StoryVisualBeat.Motion.SpinSeal) != 1
                    || Label(prefab.transform, "Subtitle").text != YokaiNames[stage - 1] + " 봉인 완료"
                    || Label(prefab.transform, "Yokai" + (stage - 1)).text != YokaiNames[stage - 1])
                    throw new InvalidOperationException("Staff demo summary must retain exactly its canonical stage: " + stage);
                if (prefab.GetComponentInChildren<SealRunSummaryPreview>(true) != null)
                    throw new InvalidOperationException("Demo results cannot contain preview sample data.");
            }
            var config = Require<StaffDemoFinalPageConfig>(FinalPageConfigPath);
            var page = Require<GameObject>(FinalPagePath).GetComponent<StaffDemoFinalPage>();
            if (page == null || page.heading == null || page.message == null || page.qrCaption == null
                || page.qrImage == null || page.placeholder == null || page.returnButton == null
                || page.GetComponent<StoryPageFit>() == null)
                throw new InvalidOperationException("The saved staff demo final page is incomplete.");
            if (!string.IsNullOrEmpty(config.destinationUrl) && !config.HasDestination)
                throw new InvalidOperationException("The demo QR destination must be an absolute HTTP(S) URL or empty.");
            if (config.qrSprite == null && !page.placeholder.GetComponentsInChildren<UnityEngine.UI.Text>(true)
                .Any(text => text.text.Contains("준비 중")))
                throw new InvalidOperationException("An unassigned QR must use an explicit placeholder.");
        }

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
                view.heading = Text(page, "Heading", "함께 봉인해 주어 고맙소!", new Rect(25, 120, 352, 58), 27, true);
                view.message = Text(page, "Message", "합이오 시연을 마쳤소.\n다음 만남을 기다리겠소.", new Rect(35, 197, 332, 70), 20, false);
                RectTransform qr = Rect("QRImage", page, new Rect(101, 330, 200, 200));
                view.qrImage = qr.gameObject.AddComponent<UnityEngine.UI.Image>();
                view.qrImage.color = Color.white; view.qrImage.raycastTarget = false;
                view.qrImage.preserveAspect = true; qr.gameObject.SetActive(false);
                RectTransform placeholder = Rect("QRPlaceholder", page, new Rect(101, 330, 200, 200));
                var placeholderFill = placeholder.gameObject.AddComponent<UnityEngine.UI.Image>();
                placeholderFill.color = GameLobbySeatColors.Disabled; // TextColor/PrimaryBrown/300
                placeholderFill.raycastTarget = false;
                Text(placeholder, "PlaceholderCaption", "QR\n준비 중", new Rect(15, 55, 170, 90), 28, true);
                view.placeholder = placeholder.gameObject;
                view.qrCaption = Text(page, "QRCaption", "QR 준비 중 · 추후 교체 예정", new Rect(30, 548, 342, 52), 16, false);
                RectTransform action = Rect("ReturnToSelection", page, new Rect(36, 712, 330, 62));
                var fill = action.gameObject.AddComponent<UnityEngine.UI.Image>();
                fill.color = GameLobbySeatColors.Ready; // TextColor/DarkRed/500***
                view.returnButton = action.gameObject.AddComponent<UnityEngine.UI.Button>();
                view.returnButton.targetGraphic = fill;
                var caption = Text(action, "Caption", "스테이지 선택으로", new Rect(8, 12, 314, 38), 23, true);
                caption.color = GameLobbySeatColors.LightText; // TextColor/SecondaryLightIvory/100
                PrefabUtility.SaveAsPrefabAsset(root, FinalPagePath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static RectTransform Rect(string name, Transform parent, Rect bounds)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            StoryStageReadyView.Place(rect, bounds);
            return rect;
        }
        private static UnityEngine.UI.Text Text(Transform parent, string name, string value, Rect bounds, float size, bool bold)
        {
            var ink = GameLobbySeatColors.Ink; // TextColor/PrimaryBrown/700
            return StageStoryAssets.Element(parent, new StageStoryAssets.Node { name = name, nodeId = "735", kind = "text", text = value,
                x = bounds.x, y = bounds.y, width = bounds.width, height = bounds.height, size = size, bold = bold, align = "CENTER",
                color = new StageStoryAssets.Rgb { r = ink.r, g = ink.g, b = ink.b } }).GetComponentInChildren<UnityEngine.UI.Text>();
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
