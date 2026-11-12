using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hapio.Editor
{
    /// <summary>Explicitly authors demo copies. Normal story assets and game scenes are never written.</summary>
    public static class YaguangDemoStoryAssets
    {
        public const string Root = "Assets/Hapio/Resources/Hapio/Demo/Yaguang";
        public const string EndingPath = Root + "/GameEnding.asset";
        public const string SummaryViewPath = Root + "/Views/SealRunSummary.prefab";
        public const string SummaryCutPath = Root + "/Cuts/SealRunSummary.asset";
        public const string FirstEndingCutPath = Root + "/Cuts/Ending_01.asset";
        public const string FirstEndingDialogue = "이야~\n야광귀도 무사히 봉인했네~!";

        [MenuItem("Hapio/Demo/Update Yaguang Demo Stories")]
        public static void CreateOrUpdate()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before authoring demo story copies.");
            var source = Require<StorySequence>(GameStageStoryUpgrade.GameEnding);
            if (!source.Validate(out string error) || source.cuts.Count != 7
                || source.cuts[0].cutId != "SealRunSummary" || source.cuts[1].cutId != "Ending_01")
                throw new InvalidOperationException("The source game ending must contain its summary and six story cuts. " + error);
            EnsureFolder(Root + "/Views");
            EnsureFolder(Root + "/Cuts");
            CreateSummaryView();

            var summary = CopyAsset(source.cuts[0], SummaryCutPath);
            summary.viewPrefab = Require<GameObject>(SummaryViewPath).GetComponent<StoryCutView>();
            summary.direction = "야광귀 한 판의 실제 성공 기록과 개인 기여도. 이야기 계속 → 구름 → 엔딩.";
            Save(summary);
            var first = CopyAsset(source.cuts[1], FirstEndingCutPath);
            first.pages[0].dialogue = FirstEndingDialogue;
            Save(first);
            var ending = CopyAsset(source, EndingPath);
            ending.storyId = "game-ending-yaguang-demo";
            ending.cuts[0] = summary;
            ending.cuts[1] = first;
            Save(ending);
            Validate();
        }

        /// <summary>Read-only pre-build validation. Builds do not create or update these assets.</summary>
        public static void Validate()
        {
            var ending = Require<StorySequence>(EndingPath);
            var source = Require<StorySequence>(GameStageStoryUpgrade.GameEnding);
            if (!ending.Validate(out string error) || ending.cuts.Count != 7)
                throw new InvalidOperationException("Yaguang demo ending is missing or invalid. " + error);
            if (ending.cuts[0] != Require<StoryCutDefinition>(SummaryCutPath)
                || ending.cuts[1] != Require<StoryCutDefinition>(FirstEndingCutPath)
                || ending.cuts[1] == source.cuts[1] || ending.cuts[1].GetPage(0).dialogue != FirstEndingDialogue)
                throw new InvalidOperationException("Yaguang demo must use its own summary and truthful first ending page.");
            for (int i = 2; i < 7; i++)
                if (ending.cuts[i] != source.cuts[i])
                    throw new InvalidOperationException("Yaguang demo must preserve original ending cuts 2–6.");

            var prefab = Require<GameObject>(SummaryViewPath);
            var summary = prefab.GetComponent<SealRunSummary>();
            var view = prefab.GetComponent<StoryCutView>();
            if (summary == null || view == null || !summary.stageIndices.SequenceEqual(new[] { 2 })
                || summary.stageTimes == null || summary.stageTimes.Length != 1 || summary.stageTimes[0] == null
                || summary.contributions == null || summary.contributions.Length != 1 || summary.contributions[0] == null
                || summary.totalTime == null || !view.requireContinueButton || view.continueButton == null || view.exitCloudWipe == null
                || view.beats.Count(beat => beat != null && beat.motion == StoryVisualBeat.Motion.SpinSeal) != 1
                || FindNode(prefab.transform, "SealedYokai0") != null || FindNode(prefab.transform, "SealedYokai2") != null
                || Label(prefab.transform, "Subtitle").text != "야광귀 봉인 완료"
                || Label(prefab.transform, "Yokai1").text != "야광귀")
                throw new InvalidOperationException("Yaguang demo summary must show one original-stage-2 row and one seal.");
            if (ending.cuts[0].viewPrefab != view)
                throw new InvalidOperationException("Yaguang demo summary cut must reference the demo summary prefab.");
            if (prefab.GetComponentInChildren<SealRunSummaryPreview>(true) != null)
                throw new InvalidOperationException("Preview sample data cannot be included in the demo ending.");
        }

        private static void CreateSummaryView()
        {
            var root = PrefabUtility.LoadPrefabContents(SealRunSummaryAssets.ViewPath);
            try
            {
                var summary = root.GetComponent<SealRunSummary>();
                var view = root.GetComponent<StoryCutView>();
                if (summary == null || view == null || summary.stageTimes.Length != 3 || summary.contributions.Length != 3)
                    throw new InvalidOperationException("The source summary must retain its three canonical stage rows.");
                summary.stageIndices = new[] { 2 };
                summary.stageTimes = new[] { summary.stageTimes[1] };
                summary.contributions = new[] { summary.contributions[1] };
                Label(root.transform, "Subtitle").text = "야광귀 봉인 완료";
                Label(root.transform, "RecordsTitle").text = "야광귀 봉인 기록";
                foreach (string key in new[] { "SealedYokai0", "SealedYokai2", "Yokai0", "Yokai2", "Time0", "Time2",
                    "Contribution0", "Contribution2", "RowRule0", "RowRule2" })
                    Object.DestroyImmediate(RequireNode(root.transform, key).gameObject);
                foreach (string key in new[] { "Yokai1", "Time1", "Contribution1" }) SetTop(RequireNode(root.transform, key), 452);
                SetTop(RequireNode(root.transform, "RowRule1"), 483.18f);
                var medal = RequireNode(root.transform, "SealedYokai1");
                medal.anchoredPosition = new Vector2((402f - medal.sizeDelta.x) / 2f, medal.anchoredPosition.y);
                var spin = medal.GetComponent<StoryVisualBeat>();
                spin.delay = .12f;
                view.beats = view.beats.Where(beat => beat != null).ToArray();
                root.name = "YaguangDemoSealRunSummary";
                PrefabUtility.SaveAsPrefabAsset(root, SummaryViewPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

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
                copy = Object.Instantiate(source);
                copy.name = source.name;
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
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
