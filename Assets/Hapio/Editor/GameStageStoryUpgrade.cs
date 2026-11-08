using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hapio.Editor
{
    public static class GameStageStoryUpgrade
    {
        public const string CanvasPrefab = "Assets/Hapio/Art/UI/GameStageStoryCanvas.prefab";
        public const string GameEnding = StageStoryAssets.Root + "/GameEnding.asset";

        [MenuItem("Hapio/Story/Connect Stage Stories To Game Results")]
        public static void Connect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Stop Play and finish compilation before connecting the saved story views.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Preserve unsaved Scene changes before connecting result stories.");
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            Scene originalActiveScene = SceneManager.GetActiveScene();
            bool canRestoreSetup = setup.Length > 0 && setup.All(scene => !string.IsNullOrEmpty(scene.path));
            try
            {
                PrepareEnding();
                if (!File.Exists(CanvasPrefab)) PrepareCanvas();
                UpgradeResultControls();
                ConnectScene(GameScenePaths.StageResult);
                ConnectScene(GameScenePaths.GameOver);
                Debug.Log("HAPIO_STAGE_STORY_RESULTS_READY: saved result/failure views connected; normal game Scene list unchanged.");
            }
            finally
            {
                // A batch Editor may begin with an unsaved, empty-path Scene. RestoreSceneManagerSetup
                // rejects that setup; our additive work closes only the Scenes it opened, so preserve it.
                if (canRestoreSetup) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
                    SceneManager.SetActiveScene(originalActiveScene);
            }
        }

        private static void PrepareEnding()
        {
            if (File.Exists(GameEnding)) return;
            StorySequence source = AssetDatabase.LoadAssetAtPath<StorySequence>(StageStoryAssets.Root + "/Ending.asset");
            if (source == null || source.cuts.Count != 7)
                throw new InvalidOperationException("The authored six ending cuts and title reference are missing.");
            StorySequence result = ScriptableObject.CreateInstance<StorySequence>();
            result.name = "GameEnding";
            result.storyId = "game-ending";
            result.cuts = source.cuts.Take(6).ToList();
            AssetDatabase.CreateAsset(result, GameEnding);
            AssetDatabase.SaveAssetIfDirty(result);
        }

        private static void PrepareCanvas()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene source = EditorSceneManager.OpenScene(StageStoryAssets.Preview, OpenSceneMode.Additive);
            GameObject copy = null;
            try
            {
                StoryPlayer template = source.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<StoryPlayer>(true)).Single();
                copy = Object.Instantiate(template.gameObject);
                copy.name = "Game Stage Story Canvas";
                foreach (StageStoryShowcase showcase in copy.GetComponentsInChildren<StageStoryShowcase>(true))
                    Object.DestroyImmediate(showcase);
                StoryPlayer player = copy.GetComponent<StoryPlayer>();
                player.playOnStart = false;
                player.touchOnly = true;
                player.sequence = null;
                player.onCompleted = new UnityEvent();
                player.onSkipped = new UnityEvent();
                Transform toolbar = player.nextButton.transform.parent;
                foreach (Transform control in toolbar.Cast<Transform>().ToArray())
                    if (control != player.nextButton.transform && control != player.skipButton.transform)
                        Object.DestroyImmediate(control.gameObject);
                player.previousButton = null;
                player.autoButton = null;
                player.autoLabel = null;
                player.progressText = null;
                player.nextButton.onClick = new Button.ButtonClickedEvent();
                player.skipButton.onClick = new Button.ButtonClickedEvent();
                var backdrop = new GameObject("Viewport Paper", typeof(RectTransform), typeof(Image));
                backdrop.transform.SetParent(copy.transform, false);
                backdrop.transform.SetAsFirstSibling();
                var backdropRect = (RectTransform)backdrop.transform;
                backdropRect.anchorMin = Vector2.zero; backdropRect.anchorMax = Vector2.one;
                backdropRect.offsetMin = backdropRect.offsetMax = Vector2.zero;
                backdrop.GetComponent<Image>().color = GameBattleUiColors.Light; // SecondaryLightIvory/200
                var tap = new GameObject("Story Tap Surface", typeof(RectTransform), typeof(Image), typeof(StoryTapAdvance));
                tap.transform.SetParent(player.cutHost.parent, false);
                tap.transform.SetAsFirstSibling();
                var tapRect = (RectTransform)tap.transform;
                tapRect.anchorMin = Vector2.zero; tapRect.anchorMax = Vector2.one;
                tapRect.offsetMin = tapRect.offsetMax = Vector2.zero;
                tap.GetComponent<Image>().color = Color.clear;
                tap.GetComponent<StoryTapAdvance>().player = player;
                copy.GetComponent<Canvas>().sortingOrder = 30000;
                if (copy.GetComponent<CanvasGroup>() == null) copy.AddComponent<CanvasGroup>();
                Transform safe = copy.transform.Find("Safe Controls");
                if (safe == null) throw new InvalidOperationException("The saved story Canvas requires its safe controls.");
                var footer = new GameObject("Result Controls", typeof(RectTransform)).GetComponent<RectTransform>();
                footer.SetParent(safe, false);
                footer.anchorMin = new Vector2(.04f, 0);
                footer.anchorMax = new Vector2(.96f, 0);
                footer.pivot = new Vector2(.5f, 0);
                footer.anchoredPosition = new Vector2(0, 10);
                footer.sizeDelta = new Vector2(0, 100);
                Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/Shilla_Culture(M).otf");
                Label(footer, "Status", font, 0, 68, 1, 100, "");
                ButtonAt(footer, "Advance", font, 0, 24, .58f, 64, "다음 스테이지");
                ButtonAt(footer, "Exit", font, .62f, 24, 1, 64, "홈으로");
                ButtonAt(footer, "Overall", font, 0, 0, 1, 22, "전체 기록");
                copy.SetActive(false);
                if (PrefabUtility.SaveAsPrefabAsset(copy, CanvasPrefab) == null)
                    throw new InvalidOperationException("Could not save the separate runtime story Canvas.");
            }
            finally
            {
                if (copy != null) Object.DestroyImmediate(copy);
                EditorSceneManager.CloseScene(source, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static void UpgradeResultControls()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefab);
            try
            {
                StoryPlayer player = root.GetComponent<StoryPlayer>();
                Transform page = player != null && player.cutHost != null ? player.cutHost.parent : null;
                if (page == null || page.GetComponent<StoryPageFit>() == null)
                    throw new InvalidOperationException("The saved result Canvas needs its authored story page.");
                Transform controls = page.Find("Result Controls") ?? root.transform.Find("Safe Controls/Result Controls");
                if (controls == null) throw new InvalidOperationException("Preserve the saved result controls before upgrading them.");
                controls.SetParent(page, false);
                controls.SetAsLastSibling();
                if (player.nextButton != null) player.nextButton.gameObject.SetActive(false);
                player.hideInlineContinueButtons = true;
                Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Lab/Art/Fonts/Shilla_Culture(M).otf");
                if (controls.Find("Viewer") == null)
                    ButtonAt(controls, "Viewer", font, 0, 0, 1, 36, "완성 박 보기");
                var layout = controls.GetComponent<GameResultActionLayout>() ?? controls.gameObject.AddComponent<GameResultActionLayout>();
                layout.player = player;
                layout.advanceButton = controls.Find("Advance").GetComponent<Button>();
                layout.exitButton = controls.Find("Exit").GetComponent<Button>();
                layout.overallButton = controls.Find("Overall").GetComponent<Button>();
                layout.viewerButton = controls.Find("Viewer").GetComponent<Button>();
                layout.statusText = controls.Find("Status").GetComponent<Text>();
                layout.statusText.fontSize = 14;
                layout.statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
                layout.statusText.verticalOverflow = VerticalWrapMode.Overflow;
                layout.Apply();
                root.SetActive(false);
                if (PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefab) == null)
                    throw new InvalidOperationException("Could not save the result control upgrade.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConnectScene(string path)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                GameScreen screen = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<GameScreen>(true)).Single();
                GameStageStoryPresentation adapter = screen.GetComponent<GameStageStoryPresentation>();
                if (adapter == null) adapter = screen.gameObject.AddComponent<GameStageStoryPresentation>();
                GameObject root = adapter.presentationRoot;
                if (root == null)
                    root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefab), scene);
                adapter.screen = screen;
                screen.stageStory = adapter;
                EditorUtility.SetDirty(screen);
                adapter.presentationRoot = root;
                adapter.presentationGroup = root.GetComponent<CanvasGroup>();
                adapter.player = root.GetComponent<StoryPlayer>();
                adapter.afterStages = new[] { "AfterGumiho", "AfterYagwanggui", "AfterJigwi" }
                    .Select(x => AssetDatabase.LoadAssetAtPath<StorySequence>(StageStoryAssets.Root + "/" + x + ".asset")).ToArray();
                adapter.ending = AssetDatabase.LoadAssetAtPath<StorySequence>(GameEnding);
                adapter.failure = AssetDatabase.LoadAssetAtPath<StorySequence>(StageStoryAssets.Root + "/Failure.asset");
                Transform controls = adapter.player.cutHost.parent.Find("Result Controls");
                adapter.advanceButton = controls.Find("Advance").GetComponent<Button>();
                adapter.exitButton = controls.Find("Exit").GetComponent<Button>();
                adapter.overallButton = controls.Find("Overall").GetComponent<Button>();
                adapter.viewerButton = controls.Find("Viewer").GetComponent<Button>();
                adapter.statusText = controls.Find("Status").GetComponent<Text>();
                adapter.skipButton = adapter.player.skipButton;
                root.SetActive(false);
                EditorUtility.SetDirty(adapter);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save story references in " + path);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void ButtonAt(Transform parent, string name, Font font, float x0, float y0, float x1, float y1, string value)
        {
            RectTransform rect = Area(parent, name, x0, y0, x1, y1);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = GameBattleUiColors.Brown; // TextColor/PrimaryBrown/500***
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text text = Label(rect, "Label", font, 0, 0, 1, y1 - y0, value);
            text.color = GameBattleUiColors.Light; // SecondaryLightIvory/200
            text.raycastTarget = false;
        }

        private static Text Label(Transform parent, string name, Font font, float x0, float y0, float x1, float y1, string value)
        {
            Text text = Area(parent, name, x0, y0, x1, y1).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 16;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = GameBattleUiColors.Ink; // TextColor/PrimaryBrown/700
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Area(Transform parent, string name, float x0, float y0, float x1, float y1)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x0, 0);
            rect.anchorMax = new Vector2(x1, 0);
            rect.pivot = new Vector2(.5f, 0);
            rect.offsetMin = new Vector2(0, y0);
            rect.offsetMax = new Vector2(0, y1);
            return rect;
        }
    }
}
