using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C6Lab;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Hapio.Editor
{
    /// <summary>Creates missing game assets through Unity APIs; existing assets change only through explicit upgrades.</summary>
    public static class GameSceneBuilder
    {
        public const string SessionPrefab = "Assets/Hapio/Resources/Hapio/GameSession.prefab";
        private const string SourceBattle = "Assets/Lab/Scenes/Lab.unity";
        public const string SharedFontPath = "Assets/Lab/Art/Fonts/ShillaCultureBold.otf";
        private static readonly Color Background = new Color(.045f, .07f, .095f, 1f);
        private static readonly Color Paper = new Color(.94f, .93f, .87f, 1f);
        private static readonly Color Accent = new Color(.18f, .43f, .47f, 1f);
        private static readonly Color Panel = new Color(.075f, .11f, .14f, 1f);
        private static readonly Color StageInk = new Color(.16f, .12f, .09f, 1f);

        [MenuItem("Hapio/Create Missing Development Game Scenes")]
        public static void CreateMissing()
        {
            EnsureFolder("Assets/Hapio/Scenes");
            EnsureFolder("Assets/Hapio/Resources/Hapio");
            // Do not save or close somebody's unsaved scene while producing separate game assets.
            Scene previousActive = SceneManager.GetActiveScene();
            Scene source = SceneManager.GetSceneByPath(SourceBattle);
            bool openedSource = !source.IsValid() || !source.isLoaded;
            if (openedSource) source = EditorSceneManager.OpenScene(SourceBattle,
                Application.isBatchMode ? OpenSceneMode.Single : OpenSceneMode.Additive);
            try
            {
                LabNetwork original = Find<LabNetwork>(source);
                if (original == null) throw new InvalidOperationException("The saved Lab network is missing.");
                if (!File.Exists(SessionPrefab)) CreateSessionPrefab(original);
                if (!File.Exists(GameScenePaths.Battle)) CreateBattle();
                CreateMenu(GameScenePaths.Home, GameScreenKind.Home);
                CreateMenu(GameScenePaths.Lobby, GameScreenKind.Lobby);
                CreateMenu(GameScenePaths.StageResult, GameScreenKind.StageResult);
                CreateMenu(GameScenePaths.GameOver, GameScreenKind.GameOver);
                var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                foreach (string path in GameScenePaths.All)
                {
                    int index = scenes.FindIndex(item => item.path == path);
                    if (index < 0) scenes.Add(new EditorBuildSettingsScene(path, true));
                    else if (!scenes[index].enabled) scenes[index] = new EditorBuildSettingsScene(path, true);
                }
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log("HAPIO_GAME_SCENES_READY: existing Lab and its default build entry are preserved. Use Hapio/Open Home or Hapio/Development Build.");
            }
            finally
            {
                if (openedSource && source.IsValid() && source.isLoaded) EditorSceneManager.CloseScene(source, true);
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            }
        }

        [MenuItem("Hapio/Open Home")]
        public static void OpenHome()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(GameScenePaths.Home)) CreateMissing();
            EditorSceneManager.OpenScene(GameScenePaths.Home);
        }

        /// <summary>Upgrade this one saved game label; the original Lab Scene is never opened or saved.</summary>
        public static void UpgradeBattleStageLabelContrast()
        {
            UpgradeScene(GameScenePaths.Battle, scene =>
            {
                GameBattleBindings bindings = Find<GameBattleBindings>(scene);
                if (bindings == null || bindings.stageLabel == null)
                    throw new InvalidOperationException("The saved Battle stage label is missing.");
                bindings.stageLabel.color = StageInk;
                EditorUtility.SetDirty(bindings.stageLabel);
            });
        }

        /// <summary>Keep both existing result actions visible without overlapping their saved hit targets.</summary>
        public static void UpgradeStageResultQuitLayout()
        {
            UpgradeScene(GameScenePaths.StageResult, scene =>
            {
                GameScreen screen = Find<GameScreen>(scene);
                if (screen == null || screen.nextButton == null || screen.homeButton == null)
                    throw new InvalidOperationException("The saved result actions are missing.");
                Rect((RectTransform)screen.nextButton.transform, new Vector2(.08f, .15f), new Vector2(.49f, .225f), Vector2.zero, Vector2.zero);
                Rect((RectTransform)screen.homeButton.transform, new Vector2(.51f, .15f), new Vector2(.92f, .225f), Vector2.zero, Vector2.zero);
                EditorUtility.SetDirty(screen.nextButton.transform);
                EditorUtility.SetDirty(screen.homeButton.transform);
            });
        }

        /// <summary>Explicitly refresh the separate Battle copy and extend only the menu DEV settings.</summary>
        [MenuItem("Hapio/Upgrade Existing Game Scenes From Current Lab")]
        public static void UpgradeScenesForCurrentLab()
        {
            UpgradeBattleFromSavedLab();
            UpgradeMenuDeveloperSchema();
            UpgradeScene(GameScenePaths.StageResult, UpgradeFonts);
            UpgradeScene(GameScenePaths.GameOver, UpgradeFonts);
        }

        /// <summary>Copy the current saved Lab through Unity while keeping the existing Battle asset identity.</summary>
        public static void UpgradeBattleFromSavedLab()
        {
            if (!File.Exists(GameScenePaths.Battle))
                throw new InvalidOperationException("Create the separate game Scenes before upgrading Battle.");
            Scene loadedBattle = SceneManager.GetSceneByPath(GameScenePaths.Battle);
            if (loadedBattle.IsValid() && loadedBattle.isLoaded)
                throw new InvalidOperationException("Close Battle before refreshing its saved Lab copy.");
            string guid = AssetDatabase.AssetPathToGUID(GameScenePaths.Battle);
            Scene previous = SceneManager.GetActiveScene();
            Scene source = SceneManager.GetSceneByPath(SourceBattle);
            bool owned = !source.IsValid() || !source.isLoaded;
            if (!owned && source.isDirty)
                throw new InvalidOperationException("Save the current Lab edits before copying its saved Scene.");
            if (owned) source = EditorSceneManager.OpenScene(SourceBattle, OpenSceneMode.Additive);
            try
            {
                // saveAsCopy leaves the loaded source path and Lab file untouched. Saving over the
                // existing destination preserves its .meta/GUID, unlike deleting and copying an asset.
                if (!EditorSceneManager.SaveScene(source, GameScenePaths.Battle, true))
                    throw new InvalidOperationException("Could not refresh the separate Battle copy.");
            }
            finally
            {
                if (owned && source.IsValid() && source.isLoaded) EditorSceneManager.CloseScene(source, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            if (AssetDatabase.AssetPathToGUID(GameScenePaths.Battle) != guid)
                throw new InvalidOperationException("Refreshing Battle unexpectedly changed its asset GUID.");
            UpgradeScene(GameScenePaths.Battle, ConfigureBattle);
        }

        /// <summary>Append new DEV fields without regenerating either menu or its existing rows and layout.</summary>
        public static void UpgradeMenuDeveloperSchema()
        {
            UpgradeScene(GameScenePaths.Home, UpgradeDeveloperRows);
            UpgradeScene(GameScenePaths.Lobby, UpgradeDeveloperRows);
        }

        private static void UpgradeDeveloperRows(Scene scene)
        {
            GameScreen screen = Find<GameScreen>(scene);
            if (screen == null || screen.developerPanel == null)
                throw new InvalidOperationException("The saved menu DEV panel is missing.");
            var scroll = screen.developerPanel.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
            if (scroll == null || scroll.content == null)
                throw new InvalidOperationException("The saved menu DEV settings scroll is missing.");
            int count = LabConfig.DeveloperFields.Count;
            int existing = screen.developerInputs == null ? 0 : screen.developerInputs.Length;
            if (existing > count || screen.developerSliders == null || screen.developerSliders.Length != existing)
                throw new InvalidOperationException("The saved DEV inputs and sliders have an incompatible schema.");
            // Validate first, so an unsupported custom row cannot leave a partially upgraded asset.
            for (int i = 0; i < existing; i++)
                if (screen.developerInputs[i] == null || screen.developerSliders[i] == null
                    || screen.developerInputs[i].transform.parent != screen.developerSliders[i].transform.parent
                    || screen.developerInputs[i].transform.parent.parent != scroll.content
                    || screen.developerInputs[i].transform.parent.Find("Label") == null)
                    throw new InvalidOperationException("The saved DEV row is incomplete: " + i);
            Array.Resize(ref screen.developerInputs, count);
            Array.Resize(ref screen.developerSliders, count);
            float[] values = AssetDatabase.LoadAssetAtPath<LabConfig>("Assets/Lab/Resources/LabConfig.asset").CaptureDeveloperValues();
            for (int i = 0; i < count; i++)
            {
                if (i >= existing) CreateDeveloperRow(screen, scroll.content, i, values[i]);
                else
                {
                    LabDeveloperField field = LabConfig.DeveloperFields[i];
                    var label = screen.developerInputs[i].transform.parent.Find("Label").GetComponent<UnityEngine.UI.Text>();
                    label.text = DeveloperLabel(field);
                    screen.developerInputs[i].SetTextWithoutNotify(values[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
                    UnityEngine.UI.Slider slider = screen.developerSliders[i];
                    slider.minValue = field.Min; slider.maxValue = field.Max; slider.wholeNumbers = field.WholeNumber;
                    slider.SetValueWithoutNotify(values[i]);
                }
            }
            ConfigureDeveloperScopes(screen);
            UpgradeFonts(scene);
            EditorUtility.SetDirty(screen);
        }

        private static void UpgradeFonts(Scene scene)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(SharedFontPath);
            if (font == null) throw new InvalidOperationException("The existing Shilla font is required for stage and DEV labels.");
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (UnityEngine.UI.Text text in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                { text.font = font; EditorUtility.SetDirty(text); }
        }

        private static void ConfigureDeveloperScopes(GameScreen screen)
        {
            Transform panel = screen.developerPanel.transform;
            if (screen.developerScopeButtons != null && screen.developerScopeButtons.Length != 0
                && screen.developerScopeButtons.Length != 4)
                throw new InvalidOperationException("The saved DEV scope buttons must have exactly four entries.");
            if (screen.developerScopeButtons == null || screen.developerScopeButtons.Length == 0)
                screen.developerScopeButtons = new UnityEngine.UI.Button[4];
            string[] labels = { "공통", "구미호", "야광귀", "지귀" };
            for (int scope = 0; scope < labels.Length; scope++)
            {
                UnityEngine.UI.Button button = screen.developerScopeButtons[scope];
                if (button == null)
                {
                    Transform saved = panel.Find("Scope" + scope);
                    button = saved != null ? saved.GetComponent<UnityEngine.UI.Button>() : null;
                    if (button == null) button = Button("Scope" + scope, panel, labels[scope],
                        .035f + scope * .24f, .835f, .245f + scope * .24f, .89f);
                    screen.developerScopeButtons[scope] = button;
                }
                button.GetComponentInChildren<UnityEngine.UI.Text>(true).text = labels[scope];
            }
            Transform title = panel.Find("Title");
            screen.developerScopeLabel = title != null ? title.GetComponent<UnityEngine.UI.Text>() : null;
            if (screen.developerScopeLabel == null) screen.developerScopeLabel = Label("Title", panel, "공통 설정", 19);
            screen.developerScopeLabel.text = "공통 설정";
            var scroll = panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
            if (scroll != null)
            {
                var rect = (RectTransform)scroll.transform;
                rect.anchorMax = new Vector2(rect.anchorMax.x, .82f);
            }
            for (int i = 0; i < screen.developerInputs.Length; i++)
                screen.developerInputs[i].transform.parent.gameObject.SetActive(GameStageSettings.IsFieldVisible(0, i));
        }

        private static void UpgradeScene(string path, Action<Scene> change)
        {
            if (!File.Exists(path)) throw new InvalidOperationException("The saved game Scene is missing: " + path);
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(path);
            bool owned = !scene.IsValid() || !scene.isLoaded;
            if (!owned && scene.isDirty)
                throw new InvalidOperationException("Save the current edits in " + path + " before applying this targeted upgrade.");
            if (owned) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                change(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Could not save " + path);
            }
            finally
            {
                if (owned && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static void CreateSessionPrefab(LabNetwork original)
        {
            GameObject copy = UnityEngine.Object.Instantiate(original.gameObject);
            copy.name = "GameSession";
            try
            {
                if (copy.GetComponentsInChildren<LabOrbBoard>(true).Length > 0
                    || copy.GetComponentsInChildren<Camera>(true).Length > 0
                    || copy.GetComponentsInChildren<LabSceneController>(true).Length > 0)
                    throw new InvalidOperationException("The source transport root also contains battle objects. Extract its transport explicitly before creating the persistent prefab.");
                LabNetwork network = copy.GetComponent<LabNetwork>();
                network.board = null; network.target = null; network.battleFloor = null;
                network.impactFeedback = null; network.projectileRoot = null;
                var manager = copy.GetComponent<NetworkManager>();
                manager.NetworkConfig.EnableSceneManagement = false;
                GameSession session = copy.AddComponent<GameSession>();
                var serialized = new SerializedObject(session);
                SerializedProperty reference = serialized.FindProperty("network");
                if (reference == null) throw new InvalidOperationException("GameSession needs its serialized network reference.");
                reference.objectReferenceValue = network;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.SaveAsPrefabAsset(copy, SessionPrefab) == null)
                    throw new InvalidOperationException("The persistent game session prefab could not be saved.");
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        private static void CreateBattle()
        {
            if (!AssetDatabase.CopyAsset(SourceBattle, GameScenePaths.Battle))
                throw new InvalidOperationException("Could not copy the saved Lab scene into the separate Battle scene.");
            Scene scene = EditorSceneManager.OpenScene(GameScenePaths.Battle, OpenSceneMode.Additive);
            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                ConfigureBattle(scene);
                EditorSceneManager.SaveScene(scene, GameScenePaths.Battle);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static void ConfigureBattle(Scene scene)
        {
            // Copy the current battle/environment, excluding Lab's standalone navigation.
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (LabLobbyView standaloneLobby in root.GetComponentsInChildren<LabLobbyView>(true))
                    UnityEngine.Object.DestroyImmediate(standaloneLobby.gameObject);
            LabNetwork old = Find<LabNetwork>(scene);
            LabSceneController controller = Find<LabSceneController>(scene);
            if (old == null || controller == null) throw new InvalidOperationException("Battle source references are incomplete.");
            GameBattleBindings bindings = controller.gameObject.AddComponent<GameBattleBindings>();
            bindings.board = old.board; bindings.target = old.target; bindings.battleFloor = old.battleFloor;
            bindings.impactFeedback = old.impactFeedback; bindings.projectileRoot = old.projectileRoot;
            bindings.sceneController = controller; bindings.hud = Find<LabHud>(scene);
            bindings.developerMode = Find<LabDeveloperMode>(scene); bindings.seatCamera = Find<LabSeatCamera>(scene);
            bindings.battleCamera = bindings.seatCamera == null ? null : bindings.seatCamera.battleCamera;
            bindings.yokaiPresentation = controller.gameObject.AddComponent<GameYokaiPresentation>();
            if (bindings.hud != null)
            {
                bindings.hud.gameObject.name = "BattleCanvas";
                Transform parent = bindings.hud.transform;
                bindings.stageLabel = Label("StageLabel", parent, "STAGE 1 / 3", 14);
                bindings.stageLabel.color = StageInk;
                Rect(bindings.stageLabel.rectTransform, new Vector2(.32f, .945f), new Vector2(.7f, .98f), Vector2.zero, Vector2.zero);
            }
            // Only this new copy loses its standalone session. Existing assets and file IDs are preserved.
            bool ownsBattleHierarchy = new Component[] { bindings.board, bindings.target, controller, bindings.hud, bindings.seatCamera, bindings.developerMode }
                .Any(component => component != null && component.transform.IsChildOf(old.transform));
            if (ownsBattleHierarchy)
            {
                NetworkManager manager = old.manager;
                var transport = old.transport;
                UnityEngine.Object.DestroyImmediate(old);
                if (manager != null) UnityEngine.Object.DestroyImmediate(manager);
                if (transport != null) UnityEngine.Object.DestroyImmediate(transport);
            }
            else UnityEngine.Object.DestroyImmediate(old.gameObject);
        }

        private static void CreateMenu(string path, GameScreenKind kind)
        {
            if (File.Exists(path)) return;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                var canvasObject = new GameObject(kind + "Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(GameScreen));
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(390f, 844f); scaler.matchWidthOrHeight = .5f;
                GameScreen screen = canvasObject.GetComponent<GameScreen>(); screen.kind = kind;
                Transform background = Box("Background", canvasObject.transform, Background);
                Stretch((RectTransform)background);
                Transform safe = new GameObject("SafeArea", typeof(RectTransform), typeof(GameSafeArea)).transform;
                safe.SetParent(canvasObject.transform, false); Stretch((RectTransform)safe);
                screen.title = Label("Title", safe, kind.ToString().ToUpperInvariant(), 28);
                Rect(screen.title.rectTransform, new Vector2(.06f, .87f), new Vector2(.94f, .95f), Vector2.zero, Vector2.zero);
                screen.status = Label("Status", safe, string.Empty, 16);
                Rect(screen.status.rectTransform, new Vector2(.06f, .76f), new Vector2(.94f, .86f), Vector2.zero, Vector2.zero);
                screen.details = Label("Details", safe, string.Empty, 14);
                Rect(screen.details.rectTransform, new Vector2(.08f, .3f), new Vector2(.92f, .74f), Vector2.zero, Vector2.zero);
                screen.details.alignment = TextAnchor.UpperLeft;
                switch (kind)
                {
                    case GameScreenKind.Home: Home(screen, safe); break;
                    case GameScreenKind.Lobby: Lobby(screen, safe); break;
                    case GameScreenKind.StageResult: Result(screen, safe); break;
                    case GameScreenKind.GameOver:
                        screen.homeButton = Button("HomeButton", safe, "HOME", .1f, .15f, .9f, .24f); break;
                }
                if (kind == GameScreenKind.Home || kind == GameScreenKind.Lobby) Developer(screen, safe);
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                EditorSceneManager.SaveScene(scene, path);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static void Home(GameScreen s, Transform parent)
        {
            Rect(s.details.rectTransform, new Vector2(.08f, .69f), new Vector2(.92f, .76f), Vector2.zero, Vector2.zero);
            s.playerCount = Label("PlayerCount", parent, "ROOM PLAYERS 3 / 5", 16);
            Rect(s.playerCount.rectTransform, new Vector2(.2f, .63f), new Vector2(.8f, .68f), Vector2.zero, Vector2.zero);
            s.fewerPlayersButton = Button("FewerPlayers", parent, "−", .06f, .63f, .18f, .68f);
            s.morePlayersButton = Button("MorePlayers", parent, "+", .82f, .63f, .94f, .68f);
            s.hostButton = Button("CreateRoom", parent, "CREATE ROOM", .06f, .55f, .49f, .62f);
            s.searchButton = Button("FindRooms", parent, "FIND / REFRESH", .51f, .55f, .94f, .62f);
            RectTransform content = Scroll("RoomList", parent, .06f, .31f, .94f, .54f);
            s.roomButtons = new UnityEngine.UI.Button[LabBonjourDiscovery.MaximumServices];
            for (int i = 0; i < s.roomButtons.Length; i++)
            {
                s.roomButtons[i] = RowButton("Room" + (i + 1), content, "ROOM · JOIN", 48f);
                s.roomButtons[i].gameObject.SetActive(false);
            }
            s.cancelButton = Button("CancelSearch", parent, "CANCEL CONNECTION", .06f, .255f, .94f, .3f);
            s.addressInput = Input("DirectAddress", parent, "192.168.0.1");
            Rect(s.addressInput.GetComponent<RectTransform>(), new Vector2(.06f, .19f), new Vector2(.7f, .245f), Vector2.zero, Vector2.zero);
            s.portInput = Input("Port", parent, "7777");
            Rect(s.portInput.GetComponent<RectTransform>(), new Vector2(.72f, .19f), new Vector2(.94f, .245f), Vector2.zero, Vector2.zero);
            s.joinDirectButton = Button("DirectJoin", parent, "JOIN DIRECT IP", .06f, .125f, .94f, .18f);
            s.soloButton = Button("DevelopmentSolo", parent, "DEV · SOLO RUN", .06f, .06f, .7f, .115f);
        }

        private static void Lobby(GameScreen s, Transform parent)
        {
            Rect(s.details.rectTransform, new Vector2(.08f, .23f), new Vector2(.92f, .46f), Vector2.zero, Vector2.zero);
            s.slotButtons = new UnityEngine.UI.Button[5];
            for (int i = 0; i < 5; i++) s.slotButtons[i] = Button("PositionP" + (i + 1), parent, "P" + (i + 1), .1f, .7f - i * .05f, .9f, .74f - i * .05f);
            s.startButton = Button("StartGame", parent, "START · 3 STAGES", .08f, .14f, .92f, .215f);
            s.homeButton = Button("LeaveLobby", parent, "LEAVE ROOM · HOME", .08f, .06f, .72f, .125f);
        }

        private static void Result(GameScreen s, Transform parent)
        {
            s.nextButton = Button("NextStage", parent, "NEXT STAGE", .08f, .15f, .49f, .225f);
            s.homeButton = Button("Home", parent, "HOME", .51f, .15f, .92f, .225f);
            s.viewerButton = Button("InspectCompletedGourd", parent, "VIEW COMPLETED GOURD", .08f, .245f, .92f, .305f);
            Transform overlay = Box("CompletedGourdView", parent, Background); Stretch((RectTransform)overlay);
            s.viewerPanel = overlay.gameObject;
            var raw = new GameObject("GourdPreview", typeof(RectTransform), typeof(UnityEngine.UI.RawImage), typeof(GamePreviewInput));
            raw.transform.SetParent(overlay, false); s.viewerImage = raw.GetComponent<UnityEngine.UI.RawImage>();
            Rect((RectTransform)raw.transform, new Vector2(.06f, .25f), new Vector2(.94f, .86f), Vector2.zero, Vector2.zero);
            var hint = Label("GestureHint", overlay, "DRAG TO ROTATE · PINCH / SCROLL TO ZOOM", 13);
            Rect(hint.rectTransform, new Vector2(.08f, .19f), new Vector2(.92f, .24f), Vector2.zero, Vector2.zero);
            s.resetViewerButton = Button("ResetView", overlay, "RESET VIEW", .08f, .09f, .48f, .16f);
            s.closeViewerButton = Button("BackToResult", overlay, "BACK TO RESULT", .52f, .09f, .92f, .16f);
            overlay.gameObject.SetActive(false);
        }

        private static void Developer(GameScreen s, Transform parent)
        {
            s.developerButton = Button("DeveloperSettings", parent, "DEV", .76f, .06f, .94f, .115f);
            Transform panel = Box("DeveloperSettingsPanel", parent, Background); Stretch((RectTransform)panel); s.developerPanel = panel.gameObject;
            var heading = Label("Title", panel, "DEVELOPMENT · SESSION SETTINGS", 19);
            Rect(heading.rectTransform, new Vector2(.05f, .9f), new Vector2(.95f, .97f), Vector2.zero, Vector2.zero);
            RectTransform content = Scroll("SettingsScroll", panel, .035f, .2f, .965f, .9f);
            int count = LabConfig.DeveloperFields.Count;
            s.developerInputs = new UnityEngine.UI.InputField[count]; s.developerSliders = new UnityEngine.UI.Slider[count];
            float[] values = AssetDatabase.LoadAssetAtPath<LabConfig>("Assets/Lab/Resources/LabConfig.asset").CaptureDeveloperValues();
            for (int i = 0; i < count; i++)
                CreateDeveloperRow(s, content, i, values[i]);
            s.developerFeedback = Label("Feedback", panel, "Run settings only; the saved LabConfig is not changed.", 12);
            Rect(s.developerFeedback.rectTransform, new Vector2(.05f, .12f), new Vector2(.95f, .19f), Vector2.zero, Vector2.zero);
            s.applyDeveloperButton = Button("ApplySettings", panel, "APPLY SETTINGS", .05f, .035f, .69f, .11f);
            s.closeDeveloperButton = Button("CloseSettings", panel, "CLOSE", .72f, .035f, .95f, .11f);
            ConfigureDeveloperScopes(s);
            panel.gameObject.SetActive(false);
        }

        private static string DeveloperLabel(LabDeveloperField field) =>
            field.Label + (field.IsChoice ? " · " + string.Join(" / ", field.Choices) : "");

        private static void CreateDeveloperRow(GameScreen screen, Transform content, int index, float value)
        {
            LabDeveloperField field = LabConfig.DeveloperFields[index];
            Transform row = Box("Setting" + index.ToString("000"), content, Panel);
            row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 88f;
            var label = Label("Label", row, DeveloperLabel(field), 11);
            label.alignment = TextAnchor.MiddleLeft;
            Rect(label.rectTransform, new Vector2(.025f, .57f), new Vector2(.975f, .98f), Vector2.zero, Vector2.zero);
            screen.developerInputs[index] = Input("Value", row, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Rect(screen.developerInputs[index].GetComponent<RectTransform>(), new Vector2(.025f, .07f), new Vector2(.34f, .55f), Vector2.zero, Vector2.zero);
            screen.developerSliders[index] = Slider(row, field.Min, field.Max, field.WholeNumber, value);
        }

        private static UnityEngine.UI.Slider Slider(Transform parent, float min, float max, bool whole, float value)
        {
            Transform root = Box("Slider", parent, new Color(.2f, .25f, .28f));
            Rect((RectTransform)root, new Vector2(.38f, .17f), new Vector2(.96f, .45f), Vector2.zero, Vector2.zero);
            var slider = root.gameObject.AddComponent<UnityEngine.UI.Slider>();
            Transform fill = Box("Fill", root, Accent); Stretch((RectTransform)fill);
            Transform handle = Box("Handle", root, Paper);
            Rect((RectTransform)handle, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, new Vector2(14f, 30f));
            slider.fillRect = (RectTransform)fill; slider.handleRect = (RectTransform)handle;
            slider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
            slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = whole; slider.value = value;
            return slider;
        }

        private static RectTransform Scroll(string name, Transform parent, float left, float bottom, float right, float top)
        {
            Transform root = Box(name, parent, Panel); Rect((RectTransform)root, new Vector2(left, bottom), new Vector2(right, top), Vector2.zero, Vector2.zero);
            var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            Transform viewport = Box("Viewport", root, Color.white); Stretch((RectTransform)viewport);
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            Transform content = new GameObject("Content", typeof(RectTransform), typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(UnityEngine.UI.ContentSizeFitter)).transform;
            content.SetParent(viewport, false);
            Rect((RectTransform)content, new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero); ((RectTransform)content).pivot = new Vector2(.5f, 1f);
            var layout = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(5, 5, 5, 5); layout.spacing = 6f; layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = (RectTransform)viewport; scroll.content = (RectTransform)content; scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            return (RectTransform)content;
        }
        private static UnityEngine.UI.Button RowButton(string name, Transform parent, string text, float height)
        {
            Transform obj = Box(name, parent, Accent); var button = obj.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = obj.GetComponent<UnityEngine.UI.Image>();
            obj.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            UnityEngine.UI.Text label = Label("Label", obj, text, 14); Stretch(label.rectTransform);
            return button;
        }
        private static UnityEngine.UI.Button Button(string name, Transform parent, string text, float left, float bottom, float right, float top)
        {
            Transform obj = Box(name, parent, Accent); Rect((RectTransform)obj, new Vector2(left, bottom), new Vector2(right, top), Vector2.zero, Vector2.zero);
            var button = obj.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = obj.GetComponent<UnityEngine.UI.Image>();
            var label = Label("Label", obj, text, 14); Stretch(label.rectTransform);
            return button;
        }
        private static UnityEngine.UI.InputField Input(string name, Transform parent, string value)
        {
            Transform obj = Box(name, parent, new Color(.16f, .2f, .23f));
            var field = obj.gameObject.AddComponent<UnityEngine.UI.InputField>();
            var text = Label("Text", obj, value, 13); Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(7, 0); text.rectTransform.offsetMax = new Vector2(-7, 0);
            text.alignment = TextAnchor.MiddleLeft; text.supportRichText = false; field.textComponent = text; field.text = value;
            field.targetGraphic = obj.GetComponent<UnityEngine.UI.Image>();
            return field;
        }
        private static UnityEngine.UI.Text Label(string name, Transform parent, string value, int size)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text)); obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<UnityEngine.UI.Text>(); text.font = AssetDatabase.LoadAssetAtPath<Font>(SharedFontPath)
                ?? throw new InvalidOperationException("The existing Shilla font is missing."); text.fontSize = size;
            text.color = Paper; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; text.text = value;
            return text;
        }
        private static Transform Box(string name, Transform parent, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); obj.transform.SetParent(parent, false);
            obj.GetComponent<UnityEngine.UI.Image>().color = color; return obj.transform;
        }
        private static void Stretch(RectTransform rect) => Rect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        private static void Rect(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        { rect.anchorMin = min; rect.anchorMax = max; rect.anchoredPosition = position; rect.sizeDelta = size; }
        private static T Find<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
