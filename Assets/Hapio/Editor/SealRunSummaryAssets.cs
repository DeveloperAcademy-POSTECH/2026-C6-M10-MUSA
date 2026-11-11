using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hapio.Editor
{
    /// <summary>Figma 4593:1603, 402 × 874. Only creates the summary and links it before the six ending cuts.</summary>
    public static class SealRunSummaryAssets
    {
        public const string ViewPath = StageStoryAssets.Root + "/Views/SealRunSummary.prefab";
        public const string CutPath = StageStoryAssets.Root + "/Cuts/SealRunSummary.asset";
        [MenuItem("Hapio/Story/Create Seal Run Summary")]
        public static void Build()
        {
            foreach (string file in new[] {"5e97a", "d04b4", "6ed9d", "cef76", "74a37", "16ddd"})
                StoryWorkspaceBuilder.ImportSprite(StageStoryAssets.Art + file + ".png");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ViewPath) == null) CreateView();
            var cut = AssetDatabase.LoadAssetAtPath<StoryCutDefinition>(CutPath);
            if (cut == null)
            {
                cut = ScriptableObject.CreateInstance<StoryCutDefinition>();
                cut.cutId = "SealRunSummary";
                cut.viewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ViewPath).GetComponent<StoryCutView>();
                cut.pages.Add(new StoryDialoguePage { dialogue = "", autoReadSeconds = 30 });
                cut.direction = "세 판의 성공 기록과 로컬 플레이어 기여도. 이야기 계속 → 구름 → 엔딩.";
                AssetDatabase.CreateAsset(cut, CutPath);
            }
            var ending = AssetDatabase.LoadAssetAtPath<StorySequence>(GameStageStoryUpgrade.GameEnding);
            if (!ending.cuts.Contains(cut))
            {
                ending.cuts.Insert(0, cut);
                EditorUtility.SetDirty(ending);
                AssetDatabase.SaveAssetIfDirty(ending);
            }
        }

        public const string PreviewPath = StageStoryAssets.Root + "/SealRunSummaryPreview.unity";
        [MenuItem("Hapio/Story/Open Seal Run Summary Preview")]
        public static void OpenPreview()
        {
            if (!System.IO.File.Exists(PreviewPath)) CreatePreview();
            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(PreviewPath);
        }
        public static void CreatePreview()
        {
            if (System.IO.File.Exists(PreviewPath)) return;
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
            try
            {
                var canvas = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GameStageStoryUpgrade.CanvasPrefab));
                canvas.name = "Seal Summary Preview (sample data only)";
                var player = canvas.GetComponent<StoryPlayer>();
                player.sequence = AssetDatabase.LoadAssetAtPath<StorySequence>(GameStageStoryUpgrade.GameEnding);
                player.playOnStart = true;
                var controls = player.cutHost.parent.Find("Result Controls");
                if (controls != null) Object.DestroyImmediate(controls.gameObject);
                if (player.skipButton != null) player.skipButton.gameObject.SetActive(false);
                canvas.AddComponent<SealRunSummaryPreview>().player = player;
                canvas.SetActive(true);
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                var camera = new GameObject("PreviewCamera", typeof(Camera)); camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
                var light = new GameObject("PreviewLight", typeof(Light)); light.GetComponent<Light>().type = LightType.Directional;
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, PreviewPath);
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
            }
        }

        private static void CreateView()
        {
            var root = new GameObject("SealRunSummary", typeof(RectTransform), typeof(StoryCutView), typeof(SealRunSummary));
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(402, 874);
                var view = root.GetComponent<StoryCutView>();
                view.hideEmptyDialogue = true;
                view.sourceFigmaNodeId = "4593:1603";
                view.requireContinueButton = view.waitForEntrance = true;
                view.exitCloudWipe = AssetDatabase.LoadAssetAtPath<GameObject>(StageStoryAssets.Root + "/Views/StoryCloudWipe.prefab").GetComponent<StoryCloudWipe>();
                var summary = root.GetComponent<SealRunSummary>();
                var beats = new List<StoryVisualBeat>();
                Image(root.transform, "enhanced_0e5896 Paper", "624c4.png", -74, -167, 704, 1041);
                Image(root.transform, "CloudTop", "5e97a.png", 286, 31, 235, 78);
                Image(root.transform, "CloudLeft", "5e97a.png", -240, 95, 337, 112);
                Image(root.transform, "CloudBottom", "5e97a.png", -175, 744, 337, 112);
                Image(root.transform, "CloudRight", "5e97a.png", 304, 666, 235, 78);
                Text(root.transform, "Title", "봉인 총평", 116, 79, 170, 56, 48, "000000");
                Text(root.transform, "Subtitle", "세 요괴 봉인 완료", 139, 161, 160, 23, 18.898f, "3f3d38");
                var divider = Node("TitleDivider", "d04b4.png", 85, 131, 231, 28);
                divider.crop = true; divider.wPercent = 1.0012f; divider.hPercent = 2.7534f; divider.topPercent = -.8571f;
                StageStoryAssets.Element(root.transform, divider);
                string[] keys = {"6ed9d.png", "cef76.png", "74a37.png"};
                float[] xs = {18, 136, 261}, widths = {123, 135, 133};
                for (int i = 0; i < 3; i++)
                {
                    var medal = Image(root.transform, "SealedYokai" + i, keys[i], xs[i], 214, widths[i], 123);
                    var beat = medal.gameObject.AddComponent<StoryVisualBeat>();
                    beat.motion = StoryVisualBeat.Motion.SpinSeal; beat.delay = .12f + i * .38f; beat.duration = .68f;
                    beats.Add(beat);
                }
                // TextColor/DarkRed/900; 5OrbsColor/FireRed/900; TextColor/SecondaryLightIvory/800.
                Text(root.transform, "RecordsTitle", "전체 봉인 기록", 47, 364, 200, 26, 20, "230e0b");
                Text(root.transform, "YokaiHeading", "요괴", 79, 407, 60, 25, 18.898f, "2a0904");
                Text(root.transform, "TimeHeading", "봉인 시간", 168.04f, 407, 100, 25, 18.898f, "2a0904");
                Text(root.transform, "ContributionHeading", "내 기여도", 276, 407, 100, 25, 18.898f, "2a0904");
                Line(root.transform, "HeaderRule", "svg_816b1.png", 45.44f, 395.58f, 311.627f);
                Line(root.transform, "HeadingRule", "svg_e17c6.png", 45.44f, 435.79f, 311.627f);
                summary.stageTimes = new UnityEngine.UI.Text[3]; summary.contributions = new UnityEngine.UI.Text[3];
                string[] names = {"구미호", "야광귀", "지귀"}; float[] nameXs = {73,71,81};
                for (int i = 0; i < 3; i++)
                {
                    float y = 452 + i * 46;
                    Text(root.transform, "Yokai" + i, names[i], nameXs[i], y, 70, 22, 17.008f, "5d544a");
                    summary.stageTimes[i] = Text(root.transform, "Time" + i, "—", 180, y, 75, 22, 17.008f, "5d544a");
                    summary.contributions[i] = Text(root.transform, "Contribution" + i, "—", 292, y, 60, 22, 17.008f, "5d544a");
                    Line(root.transform, "RowRule" + i, "svg_d479c.png", 45.44f, 483.18f + i * 46.5f, 311.627f);
                }
                Line(root.transform, "LeftFooterRule", "svg_e17c6.png", 48, 605, 129);
                Line(root.transform, "RightFooterRule", "svg_e17c6.png", 220, 605, 137);
                var seal = Node("SquareSeal", "0b794.png", 180, 587, 38.049f, 38.049f);
                seal.crop = true; seal.wPercent = 1.2994f; seal.leftPercent = -.1459f; seal.topPercent = .0089f;
                StageStoryAssets.Element(root.transform, seal);
                // Figma 4593:3256: explicit two-line appraisal, 22px type / 35px leading.
                summary.appraisal = Text(root.transform, "Appraisal", "", 27, 658, 211, 70, 22, "51473b", true);
                summary.appraisal.horizontalOverflow = HorizontalWrapMode.Overflow;
                SetAppraisalLeading(summary.appraisal);
                var stamp = StoryWorkspaceBuilder.Rect("TotalTimeStamp", root.transform, new Rect(220, 615, 170.511f, 156));
                stamp.pivot = Vector2.one * .5f; stamp.anchoredPosition += new Vector2(85.2555f, -78);
                stamp.gameObject.AddComponent<CanvasGroup>();
                Image(stamp, "StampInk", "16ddd.png", 0, 0, 170.511f, 156);
                var caption = Text(stamp, "TotalLabel", "총 소요 시간", 47.0255f, 54.139f, 72.811f, 16, 13.906f, "900805", true);
                caption.transform.parent.localRotation = Quaternion.Euler(0, 0, 13.14f);
                summary.totalTime = Text(stamp, "TotalTime", "—", 44.3355f, 65.171f, 81.637f, 39, 32.911f, "900805", true);
                summary.totalTime.transform.parent.localRotation = Quaternion.Euler(0, 0, 13.14f);
                var slam = stamp.gameObject.AddComponent<StoryVisualBeat>();
                slam.motion = StoryVisualBeat.Motion.SealStamp; slam.delay = 1.7f; slam.duration = .30f; slam.offset = new Vector2(0, 25);
                beats.Add(slam);
                view.beats = beats.ToArray();
                var hit = StoryWorkspaceBuilder.Rect("Continue", root.transform, new Rect(256, 764, 132, 52));
                var surface = hit.gameObject.AddComponent<UnityEngine.UI.Image>(); surface.color = Color.clear;
                view.continueButton = hit.gameObject.AddComponent<UnityEngine.UI.Button>(); view.continueButton.targetGraphic = surface;
                Text(hit, "ContinueLabel", "이야기 계속", 22, 14, 81, 22, 18.898f, "3f3d38", false, false);
                var arrow = Node("ContinueArrow", "Polish/ContinueArrow.png", 105.446f, 19.866f, 12.1613f, 10.2676f);
                arrow.rotation = -90.5455f; arrow.cover = false; StageStoryAssets.Element(hit, arrow);
                PrefabUtility.SaveAsPrefabAsset(root, ViewPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void SetAppraisalLeading(UnityEngine.UI.Text text)
        {
            var gen = new TextGenerator();
            gen.Populate("가\n가", text.GetGenerationSettings(new Vector2(1000,1000)));
            if (gen.lines.Count > 1) text.lineSpacing = 35 / (text.rectTransform.localScale.x * (gen.lines[0].topY - gen.lines[1].topY));
        }

        // Targeted refresh: preserve all other authored elements and their references.
        public static void UpdateStampAndAppraisal()
        {
            var root = PrefabUtility.LoadPrefabContents(ViewPath);
            try
            {
                var summary = root.GetComponent<SealRunSummary>();
                var thanks = root.transform.Find("Thanks [4593:1603]");
                if (thanks != null) thanks.gameObject.SetActive(false);
                var appraisal = (RectTransform)summary.appraisal.transform.parent;
                SetBounds(appraisal, new Rect(27,658,211,70));
                SetText(summary.appraisal, 22, new Vector2(211,70));
                summary.appraisal.horizontalOverflow = HorizontalWrapMode.Overflow;
                SetAppraisalLeading(summary.appraisal);
                var stamp = (RectTransform)root.transform.Find("TotalTimeStamp");
                SetBounds(stamp, new Rect(220,615,170.511f,156));
                var ink = (RectTransform)stamp.Find("StampInk [4593:1603]");
                SetBounds(ink, new Rect(0,0,170.511f,156));
                var image = ink.GetComponentInChildren<UnityEngine.UI.Image>();
                image.color = Color.white;
                ink.GetComponent<CanvasGroup>().alpha = 1;
                image.rectTransform.sizeDelta = new Vector2(170.511f,156);
                image.rectTransform.anchoredPosition = Vector2.zero;
                var caption = stamp.Find("TotalLabel [4593:1603]").GetComponentInChildren<UnityEngine.UI.Text>();
                SetBounds((RectTransform)caption.transform.parent, new Rect(47.0255f,54.139f,72.811f,16));
                SetText(caption,13.906f,new Vector2(72.811f,16));
                SetBounds((RectTransform)summary.totalTime.transform.parent, new Rect(44.3355f,65.171f,81.637f,39));
                SetText(summary.totalTime,32.911f,new Vector2(81.637f,39));
                PrefabUtility.SaveAsPrefabAsset(root,ViewPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void SetBounds(RectTransform rect, Rect bounds)
        {
            rect.sizeDelta = bounds.size;
            rect.anchoredPosition = new Vector2(bounds.x + bounds.width * rect.pivot.x, -bounds.y - bounds.height * (1 - rect.pivot.y));
        }
        private static void SetText(UnityEngine.UI.Text text, float size, Vector2 bounds)
        {
            text.fontSize = Mathf.Max(32,Mathf.CeilToInt(size));
            float scale = size / text.fontSize;
            text.rectTransform.localScale = Vector3.one * scale;
            text.rectTransform.sizeDelta = bounds / scale;
        }

        private static StageStoryAssets.Node Node(string name, string key, float x, float y, float w, float h) =>
            new StageStoryAssets.Node { name = name, nodeId = "4593:1603", kind = "image", key = key, x=x, y=y, width=w, height=h, cover=true };
        private static RectTransform Image(Transform parent, string name, string key, float x, float y, float w, float h) =>
            StageStoryAssets.Element(parent, Node(name, key, x, y, w, h));
        private static void Line(Transform parent, string name, string key, float x, float y, float w)
        {
            var node = Node(name, key, x, y, w, 2.84f); node.cover=false;
            StageStoryAssets.Element(parent, node);
        }
        private static UnityEngine.UI.Text Text(Transform parent, string name, string value, float x, float y, float w, float h, float size, string hex, bool centered=false, bool bold=true)
        {
            Color color; ColorUtility.TryParseHtmlString("#" + hex, out color);
            var node = new StageStoryAssets.Node { name=name, nodeId="4593:1603", kind="text", text=value, x=x,y=y,width=w,height=h,size=size,bold=bold,
                align=centered ? "CENTER" : "LEFT", color=new StageStoryAssets.Rgb {r=color.r,g=color.g,b=color.b} };
            return StageStoryAssets.Element(parent, node).GetComponentInChildren<UnityEngine.UI.Text>();
        }
    }
}
