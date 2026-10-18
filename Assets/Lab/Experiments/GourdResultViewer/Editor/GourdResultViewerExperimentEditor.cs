#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Creates only the experiment asset and keeps Play-mode opt-in local to this Editor session.</summary>
    [InitializeOnLoad]
    public static class GourdResultViewerExperimentEditor
    {
        public const string PrefabPath = "Assets/Lab/Experiments/GourdResultViewer/Resources/GourdResultViewer/ViewerUI.prefab";
        public const string MenuRoot = "C6 Lab/Experiments/Gourd Result Viewer/";
        private const string EnableMenu = MenuRoot + "Enable in Play Mode";
        private const string SessionKey = "C6Lab.Experiments.GourdResultViewer.Enabled.v1";
        private static readonly Color Ink = new Color(.94f, .96f, .98f, 1f);
        private static readonly Color DimInk = new Color(.65f, .73f, .78f, 1f);
        private static readonly Color Background = new Color(.035f, .055f, .075f, 1f);
        private static readonly Color ButtonColor = new Color(.15f, .35f, .43f, 1f);
        private static readonly Color ExperimentColor = new Color(.38f, .30f, .55f, 1f);

        public static bool EnabledForPlayMode => SessionState.GetBool(SessionKey, false);

        static GourdResultViewerExperimentEditor()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += RefreshMenuCheck;
        }

        [MenuItem(MenuRoot + "Build UI Prefab", false, 0)]
        public static void BuildUiPrefab()
        {
            string path = EnsureUiPrefab();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log("C6_GOURD_RESULT_VIEWER_UI_READY path=" + path
                + " enabled=" + EnabledForPlayMode);
        }

        [MenuItem(MenuRoot + "Build UI Prefab", true)]
        private static bool CanBuildUiPrefab() => !EditorApplication.isPlayingOrWillChangePlaymode;

        /// <summary>
        /// Create the initial prefab through Unity APIs. An existing file is never overwritten.
        /// The temporary hierarchy lives in a preview Scene; the current Scene is never saved.
        /// This method does not enable the experiment.
        /// </summary>
        public static string EnsureUiPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build the viewer UI prefab outside Play mode.");

            var existing = AssetDatabase.LoadMainAssetAtPath(PrefabPath);
            if (existing != null)
            {
                if (!(existing is GameObject))
                    throw new InvalidOperationException("The viewer prefab path already contains another asset. It was preserved.");
                return PrefabPath;
            }
            string absolutePath = Path.Combine(Application.dataPath, PrefabPath.Substring("Assets/".Length));
            if (File.Exists(absolutePath))
                throw new InvalidOperationException("The viewer prefab file already exists but could not be loaded. It was preserved.");

            EnsureAssetFolders(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = EditorUtility.CreateGameObjectWithHideFlags("GourdResultViewerUI", HideFlags.HideAndDontSave,
                    typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler),
                    typeof(UnityEngine.UI.GraphicRaycaster), typeof(GourdResultViewerUI));
                SceneManager.MoveGameObjectToScene(root, previewScene);
                root.hideFlags = HideFlags.None;
                int uiLayer = LayerMask.NameToLayer("UI");
                root.layer = uiLayer >= 0 ? uiLayer : 0;

                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;
                var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(390f, 844f);
                scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = .5f;
                var raycaster = root.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                raycaster.blockingObjects = UnityEngine.UI.GraphicRaycaster.BlockingObjects.None;

                GourdResultViewerUI ui = root.GetComponent<GourdResultViewerUI>();
                ui.entryButton = MakeButton("EntryButton", root.transform,
                    "EXPERIMENT · VIEW GOURD", new Vector2(.5f, .88f), new Vector2(250f, 40f), 14, ExperimentColor);
                ui.entryButton.gameObject.SetActive(false);

                RectTransform panel = StretchRect("ViewerPanel", root.transform, Vector2.zero, Vector2.one);
                var backdrop = panel.gameObject.AddComponent<UnityEngine.UI.Image>();
                backdrop.color = Background;
                backdrop.raycastTarget = true;
                ui.viewerPanel = panel.gameObject;

                var heading = MakeText("ExperimentHeading", panel, "EXPERIMENT · COMPLETED GOURD", 16,
                    Ink, new Vector2(.04f, .875f), new Vector2(.96f, .93f), FontStyle.Bold);
                ui.experimentTag = heading.gameObject;
                MakeText("CompletionLabel", panel, "100% PAINTED", 24,
                    Ink, new Vector2(.06f, .825f), new Vector2(.94f, .88f), FontStyle.Bold);
                ui.statusText = MakeText("StatusText", panel, "Completed gourd", 14,
                    DimInk, new Vector2(.06f, .775f), new Vector2(.94f, .815f));

                // Fit the image into a responsive region; the actual input surface stays square.
                RectTransform previewRegion = StretchRect("PreviewRegion", panel,
                    new Vector2(.06f, .29f), new Vector2(.94f, .77f));
                RectTransform preview = FixedRect("PreviewImage", previewRegion, new Vector2(.5f, .5f),
                    new Vector2(343.2f, 343.2f));
                ui.previewImage = preview.gameObject.AddComponent<UnityEngine.UI.RawImage>();
                ui.previewImage.color = Color.white;
                ui.previewImage.raycastTarget = true;
                var square = preview.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
                square.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
                square.aspectRatio = 1f;

                MakeText("GestureHint", panel, "DRAG TO ROTATE\nPINCH OR SCROLL TO ZOOM", 14,
                    DimInk, new Vector2(.05f, .215f), new Vector2(.95f, .275f));
                ui.resetButton = MakeButton("ResetViewButton", panel, "RESET VIEW",
                    new Vector2(.275f, .105f), new Vector2(150f, 46f), 14, ButtonColor);
                ui.closeButton = MakeButton("BackToResultsButton", panel, "BACK TO RESULTS",
                    new Vector2(.725f, .105f), new Vector2(150f, 46f), 14, ButtonColor);

                // Keep the canvas ready for the opt-in runtime, with both entry and modal hidden.
                panel.gameObject.SetActive(false);
                root.SetActive(true);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success || saved == null)
                    throw new InvalidOperationException("The viewer UI prefab could not be saved.");
                return PrefabPath;
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        [MenuItem(EnableMenu, false, 20)]
        public static void EnableInPlayMode()
        {
            SessionState.SetBool(SessionKey, true);
            RefreshMenuCheck();
            if (EditorApplication.isPlaying)
                GourdResultViewerExperiment.StartInCurrentScene();
            Debug.Log("C6_GOURD_RESULT_VIEWER_OPT_IN enabled=true");
        }

        [MenuItem(EnableMenu, true)]
        private static bool CanEnableInPlayMode()
        {
            RefreshMenuCheck();
            return !EditorApplication.isCompiling;
        }

        [MenuItem(MenuRoot + "Disable", false, 21)]
        public static void Disable()
        {
            SessionState.SetBool(SessionKey, false);
            RefreshMenuCheck();
            if (EditorApplication.isPlaying)
                GourdResultViewerExperiment.StopInCurrentScene();
            Debug.Log("C6_GOURD_RESULT_VIEWER_OPT_IN enabled=false");
        }

        [MenuItem(MenuRoot + "Disable", true)]
        private static bool CanDisable() => !EditorApplication.isCompiling;

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            RefreshMenuCheck();
            if (state == PlayModeStateChange.EnteredPlayMode && EnabledForPlayMode)
                GourdResultViewerExperiment.StartInCurrentScene();
        }

        private static void RefreshMenuCheck() => Menu.SetChecked(EnableMenu, EnabledForPlayMode);

        private static void EnsureAssetFolders(string folder)
        {
            string[] segments = folder.Split('/');
            string parent = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = parent + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, segments[i]);
                parent = next;
            }
        }

        private static RectTransform StretchRect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform FixedRect(string name, Transform parent, Vector2 anchor, Vector2 size)
        {
            RectTransform rect = StretchRect(name, parent, anchor, anchor);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private static UnityEngine.UI.Text MakeText(string name, Transform parent, string content, int size,
            Color color, Vector2 min, Vector2 max, FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = StretchRect(name, parent, min, max);
            var text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static UnityEngine.UI.Button MakeButton(string name, Transform parent, string caption,
            Vector2 anchor, Vector2 size, int fontSize, Color color)
        {
            RectTransform rect = FixedRect(name, parent, anchor, size);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(.78f, .78f, .78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.5f, .5f, .5f, .6f);
            button.colors = colors;
            var label = MakeText("Text", rect, caption, fontSize, Ink, Vector2.zero, Vector2.one, FontStyle.Bold);
            label.rectTransform.offsetMin = new Vector2(8f, 2f);
            label.rectTransform.offsetMax = new Vector2(-8f, -2f);
            return button;
        }
    }
}
#endif
