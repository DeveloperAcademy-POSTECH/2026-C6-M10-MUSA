using System;
using System.IO;
using C6Lab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace C6Lab.Editor
{
    /// <summary>
    /// Editor-only upgrades of the saved Lab Scene. Gameplay never creates or replaces the saved Canvas.
    /// The original scene builder, legacy throw-tuning migration and template-sample cleanup were
    /// one-time steps that can no longer run against the current project, so they were removed.
    /// </summary>
    public static class LabSceneBuilder
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";
        private const string ConfigPath = "Assets/Lab/Resources/LabConfig.asset";
        private static readonly Color Ink = new Color(.94f, .96f, .98f, 1f);
        private static readonly Color DimInk = new Color(.65f, .73f, .78f, 1f);
        private static readonly Color ButtonColor = new Color(.15f, .35f, .43f, .94f);

        [MenuItem("C6 Lab/Rebind Saved UI Values")]
        public static void RebindSavedUiValues()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Lab Scene is missing.", ScenePath);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LabHud hud = UnityEngine.Object.FindFirstObjectByType<LabHud>();
            if (hud == null) throw new InvalidOperationException("Saved Lab Canvas references are incomplete.");
            Transform sessionControls = hud.transform.Find("SessionControls");
            if (sessionControls == null) throw new InvalidOperationException("Saved SessionControls panel is missing.");
            hud.sessionControls = sessionControls.gameObject;
            AssetDatabase.ForceReserializeAssets(new[] { ConfigPath });
            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save UI references.");
            Debug.Log("C6_LAB_UI_BINDING_OK");
        }

        [MenuItem("C6 Lab/Upgrade Saved Battle References")]
        public static void UpgradeSavedBattleReferences()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Lab Scene is missing.", ScenePath);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LabNetwork network = UnityEngine.Object.FindFirstObjectByType<LabNetwork>();
            LabTarget target = UnityEngine.Object.FindFirstObjectByType<LabTarget>();
            GameObject cameraObject = GameObject.Find("BattleCamera");
            GameObject floorObject = GameObject.Find("BattleFloor");
            LabConfig config = AssetDatabase.LoadAssetAtPath<LabConfig>(ConfigPath);
            if (network == null || target == null || cameraObject == null || floorObject == null || config == null)
                throw new InvalidOperationException("Saved battle camera, floor, target, network, or config is missing.");
            Camera battleCamera = cameraObject.GetComponent<Camera>();
            Collider floor = floorObject.GetComponent<Collider>();
            if (battleCamera == null || floor == null)
                throw new InvalidOperationException("Saved battle camera or floor Collider is missing.");
            LabSeatCamera seatCamera = cameraObject.GetComponent<LabSeatCamera>();
            if (seatCamera == null) seatCamera = cameraObject.AddComponent<LabSeatCamera>();
            seatCamera.network = network;
            seatCamera.target = target;
            seatCamera.battleCamera = battleCamera;
            seatCamera.config = config;
            network.battleFloor = floor;
            EditorUtility.SetDirty(seatCamera);
            EditorUtility.SetDirty(network);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save battle references.");
            Debug.Log("C6_LAB_BATTLE_BINDING_OK");
        }

        [MenuItem("C6 Lab/Upgrade Saved Developer Panel")]
        public static void UpgradeSavedDeveloperPanel()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Lab Scene is missing.", ScenePath);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject canvas = GameObject.Find("LabCanvas");
            LabNetwork network = UnityEngine.Object.FindFirstObjectByType<LabNetwork>();
            LabSceneController sceneController = UnityEngine.Object.FindFirstObjectByType<LabSceneController>();
            LabSeatCamera seatCamera = UnityEngine.Object.FindFirstObjectByType<LabSeatCamera>();
            LabConfig config = AssetDatabase.LoadAssetAtPath<LabConfig>(ConfigPath);
            if (canvas == null || canvas.GetComponent<LabHud>() == null || network == null
                || sceneController == null || seatCamera == null || config == null)
                throw new InvalidOperationException("The saved Lab Canvas, battle references, or Config are incomplete.");
            LabDeveloperMode existing = canvas.GetComponent<LabDeveloperMode>();
            if (existing != null)
            {
                if (existing.panel == null || !existing.EnsureValueInputs())
                    throw new InvalidOperationException("Existing developer UI is incomplete; inspect its saved references.");
                PositionDeveloperUi(existing);
                EditorUtility.SetDirty(existing);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save developer UI layout.");
                Debug.Log("C6_LAB_DEV_PANEL_LAYOUT_SAVED");
                return;
            }

            AddDeveloperUi(canvas.transform, network, config, sceneController, seatCamera);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save developer UI.");
            Debug.Log("C6_LAB_DEV_PANEL_SAVED fields=" + LabConfig.DeveloperFields.Count);
        }

        private static void AddDeveloperUi(Transform canvasRoot, LabNetwork network,
            LabConfig config, LabSceneController sceneController, LabSeatCamera seatCamera)
        {
            var developer = canvasRoot.gameObject.AddComponent<LabDeveloperMode>();
            developer.sourceConfig = config;
            developer.network = network;
            developer.sceneController = sceneController;
            developer.seatCamera = seatCamera;

            developer.toggleButton = Button("DeveloperModeButton", canvasRoot, "DEV", 0f, 1f,
                new Vector2(.78f, .51f), new Vector2(.97f, .56f), new Color(.38f, .30f, .55f, .98f));

            RectTransform panel = CreateRect("DeveloperPanel", canvasRoot,
                Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            AddPanelImage(panel, new Color(.035f, .055f, .075f, 1f));
            panel.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            developer.panel = panel.gameObject;
            panel.SetAsLastSibling();

            var title = Text("DeveloperTitle", panel, "DEVELOPER SOLO", 21, Ink,
                new Vector2(.04f, .92f), new Vector2(.76f, .985f), Vector2.zero, Vector2.zero);
            title.alignment = TextAnchor.MiddleLeft;
            developer.closeButton = Button("CloseDeveloperButton", panel, "CLOSE", 0f, 1f,
                new Vector2(.78f, .925f), new Vector2(.96f, .982f));
            var hint = Text("DeveloperHint", panel,
                "Edit values, then apply and restart. Changes last until this app closes.",
                12, DimInk, new Vector2(.04f, .86f), new Vector2(.96f, .92f), Vector2.zero, Vector2.zero);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;

            RectTransform scroll = CreateRect("DeveloperScrollView", panel,
                new Vector2(.04f, .205f), new Vector2(.96f, .85f), Vector2.zero, Vector2.zero);
            var scrollRect = scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 28f;

            RectTransform viewport = CreateRect("Viewport", scroll, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            var viewportImage = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            viewportImage.color = new Color(.10f, .14f, .18f, .55f);
            viewportImage.raycastTarget = true;
            var mask = viewport.gameObject.AddComponent<UnityEngine.UI.Mask>();
            mask.showMaskGraphic = true;
            scrollRect.viewport = viewport;

            RectTransform content = CreateRect("Content", viewport,
                new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var vertical = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vertical.padding = new RectOffset(6, 6, 6, 6);
            vertical.spacing = 4f;
            vertical.childAlignment = TextAnchor.UpperCenter;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = content;

            developer.valueInputs = new UnityEngine.UI.InputField[LabConfig.DeveloperFields.Count];
            for (int i = 0; i < developer.valueInputs.Length; i++)
            {
                LabDeveloperField field = LabConfig.DeveloperFields[i];
                RectTransform row = CreateRect("Setting" + i.ToString("D2"), content,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 42f;
                var horizontal = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                horizontal.padding = new RectOffset(3, 3, 2, 2);
                horizontal.spacing = 6f;
                horizontal.childAlignment = TextAnchor.MiddleLeft;
                horizontal.childControlWidth = true;
                horizontal.childControlHeight = true;
                horizontal.childForceExpandWidth = false;
                horizontal.childForceExpandHeight = false;

                var label = Text("SettingLabel", row, field.Label, 12, Ink,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                label.alignment = TextAnchor.MiddleLeft;
                label.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 217f;
                UnityEngine.UI.InputField input = Input("SettingValue", row, field.Label,
                    string.Empty, Vector2.zero, Vector2.one);
                input.contentType = field.WholeNumber
                    ? UnityEngine.UI.InputField.ContentType.IntegerNumber
                    : UnityEngine.UI.InputField.ContentType.DecimalNumber;
                input.characterLimit = 14;
                var inputLayout = input.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                inputLayout.preferredWidth = 104f;
                inputLayout.preferredHeight = 34f;
                developer.valueInputs[i] = input;
            }

            developer.feedbackText = Text("DeveloperFeedback", panel, string.Empty, 12,
                new Color(1f, .76f, .44f), new Vector2(.04f, .15f), new Vector2(.96f, .205f),
                Vector2.zero, Vector2.zero);
            developer.feedbackText.horizontalOverflow = HorizontalWrapMode.Wrap;
            developer.defaultsButton = Button("DeveloperDefaultsButton", panel, "DEFAULTS", 0f, 1f,
                new Vector2(.04f, .09f), new Vector2(.46f, .145f));
            developer.endSoloButton = Button("EndSoloButton", panel, "END SOLO", 0f, 1f,
                new Vector2(.54f, .09f), new Vector2(.96f, .145f));
            developer.applyStartButton = Button("ApplyDeveloperButton", panel,
                "APPLY + START SOLO", 0f, 1f, new Vector2(.04f, .015f),
                new Vector2(.96f, .082f), new Color(.27f, .53f, .37f, 1f));
            developer.applyStartLabel = developer.applyStartButton.GetComponentInChildren<UnityEngine.UI.Text>();
            PositionDeveloperUi(developer);
            panel.gameObject.SetActive(false);
        }

        private static void PositionDeveloperUi(LabDeveloperMode developer)
        {
            SetDeveloperRect(developer.toggleButton.transform, new Vector2(.78f, .51f), new Vector2(.97f, .56f));
            Transform panel = developer.panel.transform;
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.035f, .055f, .075f, 1f);
            foreach (var input in developer.valueInputs)
            {
                if (input == null) throw new InvalidOperationException("Developer UI has a missing tuning input.");
                input.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 34f;
            }
            SetDeveloperRect(panel.Find("DeveloperTitle"), new Vector2(.04f, .875f), new Vector2(.76f, .93f));
            SetDeveloperRect(developer.closeButton.transform, new Vector2(.78f, .875f), new Vector2(.96f, .93f));
            SetDeveloperRect(panel.Find("DeveloperHint"), new Vector2(.04f, .81f), new Vector2(.96f, .865f));
            SetDeveloperRect(panel.Find("DeveloperScrollView"), new Vector2(.04f, .23f), new Vector2(.96f, .80f));
            SetDeveloperRect(developer.feedbackText.transform, new Vector2(.04f, .185f), new Vector2(.96f, .225f));
            SetDeveloperRect(developer.defaultsButton.transform, new Vector2(.04f, .13f), new Vector2(.46f, .18f));
            SetDeveloperRect(developer.endSoloButton.transform, new Vector2(.54f, .13f), new Vector2(.96f, .18f));
            SetDeveloperRect(developer.applyStartButton.transform, new Vector2(.04f, .045f), new Vector2(.96f, .12f));
        }

        private static void SetDeveloperRect(Transform target, Vector2 minimum, Vector2 maximum)
        {
            if (target == null) throw new InvalidOperationException("Developer UI has a missing layout element.");
            var rect = target.GetComponent<RectTransform>();
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RectTransform CreateRect(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static void AddPanelImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static UnityEngine.UI.Text Text(string name, Transform parent, string value, int fontSize,
            Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            var text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.text = value;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static UnityEngine.UI.Button Button(string name, Transform parent, string caption, float index,
            float count = 4f, Vector2? anchorMin = null, Vector2? anchorMax = null, Color? color = null)
        {
            float left = .02f + index * (.96f / count);
            float right = .02f + (index + 1f) * (.96f / count);
            RectTransform rect = CreateRect(name, parent,
                anchorMin ?? new Vector2(left + .006f, .25f), anchorMax ?? new Vector2(right - .006f, .52f),
                Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color ?? ButtonColor;
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            Text("Label", rect, caption, 13, Ink, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        private static UnityEngine.UI.InputField Input(string name, Transform parent, string hint,
            string initial, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(.95f, .96f, .97f, 1f);
            image.raycastTarget = true;
            var input = rect.gameObject.AddComponent<UnityEngine.UI.InputField>();
            input.targetGraphic = image;
            var text = Text("Text", rect, initial, 14, new Color(.06f, .10f, .13f),
                Vector2.zero, Vector2.one, new Vector2(10f, 2f), new Vector2(-10f, -2f));
            text.alignment = TextAnchor.MiddleLeft;
            var placeholder = Text("Placeholder", rect, hint, 14, new Color(.45f, .49f, .53f, .8f),
                Vector2.zero, Vector2.one, new Vector2(10f, 2f), new Vector2(-10f, -2f));
            placeholder.alignment = TextAnchor.MiddleLeft;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = initial;
            return input;
        }
    }
}
