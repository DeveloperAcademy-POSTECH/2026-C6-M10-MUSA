using System;
using System.IO;
using C6Lab;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace C6Lab.Editor
{
    /// <summary>
    /// One-time, Editor-only setup. Gameplay never creates or replaces the saved Canvas.
    /// Re-running this command does not overwrite a scene that a designer has edited.
    /// </summary>
    public static class LabSceneBuilder
    {
        private const string ScenePath = "Assets/Lab/Scenes/Lab.unity";
        private const string ConfigPath = "Assets/Lab/Resources/LabConfig.asset";
        private static readonly Color Ink = new Color(.94f, .96f, .98f, 1f);
        private static readonly Color DimInk = new Color(.65f, .73f, .78f, 1f);
        private static readonly Color Panel = new Color(.045f, .075f, .095f, .78f);
        private static readonly Color ButtonColor = new Color(.15f, .35f, .43f, .94f);

        [MenuItem("C6 Lab/Build Editable Lab Scene")]
        public static void Build()
        {
            EnsureFolder("Assets/Lab", "Scenes");
            EnsureFolder("Assets/Lab", "Resources");
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("The Lab Scene already exists. Open it and edit its saved objects; the builder will not overwrite them.");

            LabConfig config = AssetDatabase.LoadAssetAtPath<LabConfig>(ConfigPath);
            if (config == null)
            {
                if (File.Exists(ConfigPath))
                    throw new InvalidOperationException("The Lab Config path already contains another asset.");
                config = ScriptableObject.CreateInstance<LabConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            Scene previous = SceneManager.GetActiveScene();
            bool preservePrevious = previous.IsValid() && previous.isLoaded && !string.IsNullOrEmpty(previous.path);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                preservePrevious ? NewSceneMode.Additive : NewSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Could not open the new Lab Scene.");
            if (preservePrevious && SceneManager.GetActiveScene() != scene && !SceneManager.SetActiveScene(scene))
                throw new InvalidOperationException("Could not activate the new Lab Scene.");
            bool saved = false;
            try
            {
                BuildScene(config);
                EditorSceneManager.MarkSceneDirty(scene);
                saved = EditorSceneManager.SaveScene(scene, ScenePath);
                if (!saved) throw new IOException("Unity did not save the Lab Scene.");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                Debug.Log("C6_LAB_SCENE_CREATED scene=" + ScenePath + " config=" + ConfigPath);
            }
            finally
            {
                if (preservePrevious && previous.IsValid() && previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                    // Keep the caller's previously open Scene intact. The new saved Scene is opened explicitly.
                    if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                }
                if (!saved) Debug.LogWarning("C6_LAB_SCENE_INCOMPLETE; inspect the Editor error before retrying.");
            }
        }

        [MenuItem("C6 Lab/Rebind Saved UI Values")]
        public static void RebindSavedUiValues()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Lab Scene is missing.", ScenePath);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            LabHud hud = UnityEngine.Object.FindFirstObjectByType<LabHud>();
            if (hud == null || hud.generateButton == null)
                throw new InvalidOperationException("Saved Lab Canvas references are incomplete.");
            hud.generateLabel = hud.generateButton.GetComponentInChildren<UnityEngine.UI.Text>();
            if (hud.generateLabel == null) throw new InvalidOperationException("Generate label is missing.");
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

        [MenuItem("C6 Lab/Migrate Legacy Throw Tuning")]
        public static void MigrateLegacyThrowTuning()
        {
            LabConfig config = AssetDatabase.LoadAssetAtPath<LabConfig>(ConfigPath);
            if (config == null) throw new FileNotFoundException("Lab Config is missing.", ConfigPath);
            var serialized = new SerializedObject(config);
            SerializedProperty lateral = serialized.FindProperty("throwLateralGain");
            SerializedProperty forward = serialized.FindProperty("throwForwardGain");
            if (Mathf.Abs(lateral.floatValue - 6f) > .0001f ||
                Mathf.Abs(forward.floatValue - 8f) > .0001f)
                throw new InvalidOperationException("Throw tuning differs from the legacy values. Edit LabConfig in the Inspector instead.");
            SetFloat(serialized, "spawnRiseDistance", .65f);
            SetFloat(serialized, "spawnRiseDuration", .25f);
            SetFloat(serialized, "cameraRadius", 8f);
            SetFloat(serialized, "cameraHeightOffset", .65f);
            SetFloat(serialized, "throwOriginRadius", 4f);
            SetFloat(serialized, "throwOriginHeightOffset", -.5f);
            SetFloat(serialized, "throwOriginLateralRange", 1f);
            SetFloat(serialized, "throwSwipeMinSpeed", .8f);
            SetFloat(serialized, "throwSwipeMinDistance", .12f);
            SetFloat(serialized, "throwSwipeUpDominance", .65f);
            SetFloat(serialized, "throwMaxSwipeSpeed", 6f);
            SetFloat(serialized, "throwMinUpSpeed", 2f);
            SetFloat(serialized, "throwLateralGain", 2.5f);
            SetFloat(serialized, "throwUpGain", 2.1f);
            SetFloat(serialized, "throwForwardGain", 4.5f);
            SetFloat(serialized, "throwMaxWorldSpeed", 18f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("C6_LAB_THROW_TUNING_MIGRATED");
        }

        private static void SetFloat(SerializedObject serialized, string name, float value)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) throw new InvalidOperationException("Missing LabConfig field: " + name);
            property.floatValue = value;
        }

        [MenuItem("C6 Lab/Remove Unused Template Samples")]
        public static void RemoveTemplateSamples()
        {
            if (!File.Exists(ScenePath))
                throw new InvalidOperationException("Build the Lab Scene before removing template samples.");
            string[] unused =
            {
                "Assets/TutorialInfo", "Assets/Readme.asset",
                "Assets/Scenes", "Assets/InputSystem_Actions.inputactions"
            };
            foreach (string path in unused)
            {
                if (!File.Exists(path) && !Directory.Exists(path)) continue;
                if (!AssetDatabase.DeleteAsset(path)) throw new IOException("Could not remove unused template asset: " + path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("C6_LAB_TEMPLATE_SAMPLES_REMOVED");
        }

        private static void BuildScene(LabConfig config)
        {
            var stage = new GameObject("BattleStage");
            var target = LabTarget.CreateCylinder(stage.transform, new Vector3(0f, 1.35f, 10f), .6f, 2.7f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "BattleFloor";
            floor.transform.SetParent(stage.transform, false);
            floor.transform.position = new Vector3(0f, -.1f, 10f);
            floor.transform.localScale = new Vector3(1.8f, 1f, 1.8f);

            var lightObject = new GameObject("BattleLight", typeof(Light));
            lightObject.transform.SetParent(stage.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(42f, -30f, 0f);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;

            var projectileRoot = new GameObject("Projectiles").transform;
            projectileRoot.SetParent(stage.transform, false);
            Camera battleCamera = Camera("BattleCamera", stage.transform, new Rect(0f, .46f, 1f, .54f),
                new Color(.18f, .24f, .28f), false);
            battleCamera.transform.position = new Vector3(0f, 1.5f, 0f);
            battleCamera.transform.LookAt(target.transform.position + Vector3.up * .05f);
            battleCamera.fieldOfView = 52f;
            battleCamera.nearClipPlane = .3f;
            battleCamera.farClipPlane = 50f;

            var boardObject = new GameObject("OrbBoard", typeof(LabOrbBoard));
            Camera orbCamera = Camera("OrbCamera", boardObject.transform, new Rect(0f, 0f, 1f, .46f),
                new Color(.095f, .13f, .15f), true);
            orbCamera.transform.position = new Vector3(0f, 0f, -10f);
            orbCamera.transform.rotation = Quaternion.identity;
            orbCamera.orthographicSize = config.BoardHeight * .5f;
            orbCamera.nearClipPlane = .1f;
            orbCamera.farClipPlane = 12f;
            LabOrb3DSceneUpgrade.Configure(boardObject, orbCamera, battleCamera);

            var sceneRoot = new GameObject("LabSceneController", typeof(LabSceneController));
            var sceneController = sceneRoot.GetComponent<LabSceneController>();
            sceneController.config = config;
            sceneController.board = boardObject.GetComponent<LabOrbBoard>();
            sceneController.orbCamera = orbCamera;

            var networkObject = new GameObject("LabNetwork");
            var transport = networkObject.AddComponent<UnityTransport>();
            var manager = networkObject.AddComponent<NetworkManager>();
            var network = networkObject.AddComponent<LabNetwork>();
            network.config = config;
            network.board = sceneController.board;
            network.target = target;
            network.battleFloor = floor.GetComponent<Collider>();
            network.projectileRoot = projectileRoot;
            network.manager = manager;
            network.transport = transport;
            network.port = 7777;
            network.hostAddress = "127.0.0.1";

            var seatCamera = battleCamera.gameObject.AddComponent<LabSeatCamera>();
            seatCamera.network = network;
            seatCamera.target = target;
            seatCamera.battleCamera = battleCamera;
            seatCamera.config = config;

            BuildCanvas(network, config, sceneController, seatCamera);
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            eventSystem.transform.SetAsLastSibling();
        }

        private static Camera Camera(string name, Transform parent, Rect viewport, Color background, bool orthographic)
        {
            var gameObject = new GameObject(name, typeof(Camera));
            gameObject.transform.SetParent(parent, false);
            var camera = gameObject.GetComponent<Camera>();
            camera.rect = viewport;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.orthographic = orthographic;
            camera.allowHDR = false;
            return camera;
        }

        private static void BuildCanvas(LabNetwork network, LabConfig config,
            LabSceneController sceneController, LabSeatCamera seatCamera)
        {
            var canvasObject = new GameObject("LabCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(LabHud));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390f, 844f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            var hud = canvasObject.GetComponent<LabHud>();
            hud.network = network;

            // The bottom tint is a UI shape, not a texture asset. It does not intercept orb gestures.
            RectTransform boardTint = CreateRect("BoardAreaTint", canvasObject.transform,
                new Vector2(0f, 0f), new Vector2(1f, .46f), Vector2.zero, Vector2.zero);
            var boardImage = boardTint.gameObject.AddComponent<UnityEngine.UI.Image>();
            boardImage.color = new Color(.08f, .14f, .17f, .26f);
            boardImage.raycastTarget = false;

            RectTransform header = CreateRect("BattleHeader", canvasObject.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -118f), new Vector2(-12f, -46f));
            AddPanelImage(header, Panel);
            hud.hpText = Text("YokaiHpText", header, "YOKAI HP —", 17, Ink,
                new Vector2(0f, .5f), new Vector2(.55f, 1f), new Vector2(8f, 0f), new Vector2(-4f, -4f));
            hud.timeText = Text("BattleTimeText", header, "TIME —", 17, Ink,
                new Vector2(.55f, .5f), new Vector2(1f, 1f), new Vector2(4f, 0f), new Vector2(-8f, -4f));
            hud.seatText = Text("SeatText", header, "NO SESSION", 13, DimInk,
                new Vector2(0f, 0f), new Vector2(1f, .5f), new Vector2(8f, 4f), new Vector2(-8f, 0f));

            RectTransform lobby = CreateRect("SessionControls", canvasObject.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -262f), new Vector2(-12f, -126f));
            AddPanelImage(lobby, Panel);
            hud.sessionControls = lobby.gameObject;
            hud.hostAddress = Input("HostAddressInput", lobby, "Host IP", "127.0.0.1",
                new Vector2(.02f, .58f), new Vector2(.70f, .94f));
            hud.port = Input("PortInput", lobby, "Port", "7777",
                new Vector2(.73f, .58f), new Vector2(.98f, .94f));
            hud.hostButton = Button("HostButton", lobby, "HOST", 0f);
            hud.joinButton = Button("JoinButton", lobby, "JOIN", 1f);
            hud.startButton = Button("StartButton", lobby, "START", 2f);
            hud.leaveButton = Button("LeaveButton", lobby, "LEAVE", 3f);
            hud.peersText = Text("PlayerCountText", lobby, "PLAYERS 0 / 3", 12, DimInk,
                new Vector2(.02f, .01f), new Vector2(.98f, .19f), Vector2.zero, Vector2.zero);

            hud.statusText = Text("SessionStatusText", canvasObject.transform, "Choose HOST or JOIN.", 13, Ink,
                new Vector2(.03f, .64f), new Vector2(.97f, .68f), Vector2.zero, Vector2.zero);
            hud.noticeText = Text("NoticeText", canvasObject.transform, string.Empty, 13,
                new Color(1f, .76f, .44f), new Vector2(.03f, .60f), new Vector2(.97f, .64f), Vector2.zero, Vector2.zero);

            RectTransform footer = CreateRect("BoardControls", canvasObject.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 22f), new Vector2(-12f, 138f));
            AddPanelImage(footer, Panel);
            hud.generateButton = Button("GenerateButton", footer, "GENERATE / 20", 0f, 1f,
                new Vector2(.16f, .12f), new Vector2(.84f, .53f), new Color(.27f, .53f, .37f, .98f));
            hud.generateLabel = hud.generateButton.GetComponentInChildren<UnityEngine.UI.Text>();
            hud.staminaText = Text("StaminaText", footer, "STAMINA —", 15, Ink,
                new Vector2(.03f, .58f), new Vector2(.54f, .96f), Vector2.zero, Vector2.zero);
            hud.orbsText = Text("OrbCountText", footer, "ORBS —", 15, Ink,
                new Vector2(.55f, .58f), new Vector2(.97f, .96f), Vector2.zero, Vector2.zero);

            AddDeveloperUi(canvasObject.transform, network, config, sceneController, seatCamera);
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

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
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
