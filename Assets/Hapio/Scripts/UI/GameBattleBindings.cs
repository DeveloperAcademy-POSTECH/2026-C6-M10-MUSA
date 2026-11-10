using System.Reflection;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    /// <summary>Reconnects a newly loaded Battle instance to the persistent room before legacy battle Awake runs.</summary>
    [DefaultExecutionOrder(-500)]
    public sealed class GameBattleBindings : MonoBehaviour
    {
        public LabOrbBoard board;
        public LabTarget target;
        public Camera battleCamera;
        public Collider battleFloor;
        public LabImpactFeedback impactFeedback;
        public Transform projectileRoot;
        public LabSceneController sceneController;
        public LabSeatCamera seatCamera;
        public LabHud hud;
        public LabDeveloperMode developerMode;
        public UnityEngine.UI.Text stageLabel;
        public UnityEngine.UI.Button developerNextStageButton;
        public GameYokaiPresentation yokaiPresentation;
        private GameSession session;
        private LabBattleRenderWarmup renderWarmup;

        /// <summary>#498: asked only once the presentation is ready, so the board's camera and the cover are in place.</summary>
        public bool FinishRenderWarmup(bool mayDraw)
        {
            if (renderWarmup == null) return true;
            renderWarmup.Request(mayDraw);
            return renderWarmup.Finished;
        }

        public bool IsPresentationReady
        {
            get
            {
                if (!isActiveAndEnabled || session == null || board == null || !board.isActiveAndEnabled
                    || target == null || battleCamera == null || !battleCamera.isActiveAndEnabled
                    || hud == null || !hud.isActiveAndEnabled || target.Hitbox == null) return false;
                var paint = target.GetComponent<LabGourdPaint>();
                return paint != null && paint.IsReady;
            }
        }

        private void Awake()
        {
            // A Battle refreshed from Lab may contain its authored standalone lobby. The
            // persistent game session has separate Home/Lobby Scenes and never runs this view.
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (LabLobbyView standaloneLobby in root.GetComponentsInChildren<LabLobbyView>(true))
                    standaloneLobby.gameObject.SetActive(false);
            // #507: every Hapio stage draws one curved rope from the fixed anchor to the gourd. Presentation only;
            // runs before LabHangingTarget.Awake builds the rig, and Battle.unity keeps its saved Segmented value.
            LabHangingTarget hanging = target != null ? target.GetComponent<LabHangingTarget>() : null;
            if (hanging != null) hanging.SetRopeStyle(LabRopeStyle.Curved);
            session = GameSession.EnsureExists();
            MountStageGourd(session);
            LabNetwork network = session.Network;
            GameDeveloperAccess.BindBattle(this);
            network.BindBattleReferences(board, target, battleCamera, battleFloor, impactFeedback, projectileRoot);
            RebindScene(network);
            if (!session.IsTutorial) gameObject.AddComponent<GameBattleUiPresentation>().Bind(this, session);
            else gameObject.AddComponent<GameTutorialBoardPresentation>().Bind(this);
            gameObject.AddComponent<GameHelpView>().Bind(this, session);
            // #492: the stage's sound data and 지귀 art load here, behind the opaque preparing overlay, not on first use.
            float warmupStarted = Time.realtimeSinceStartup;
            int warmed = LabBattleAudioWarmup.Preload(network.GameStageYokai, this);
            if (Debug.isDebugBuild)
                Debug.Log("C6_AUDIO_WARMUP yokai=" + network.GameStageYokai + " loaded=" + warmed
                    + " ms=" + ((Time.realtimeSinceStartup - warmupStarted) * 1000f).ToString("0"));
            renderWarmup = gameObject.AddComponent<LabBattleRenderWarmup>();
            renderWarmup.Bind(network, battleCamera, hud);
            session.NotifyBattleBound(board);
            network.ConfigApplied += OnConfigApplied;
            // The practice board (#477) dresses this Battle for the tutorial and runs its script.
            if (session.IsTutorial) gameObject.AddComponent<GameTutorialDirector>().Bind(session, this);
        }

        /// <summary>
        /// #507: the story stage's own gourd, set before LabSceneController.Awake (-100) attaches the paint, which only
        /// fills an empty prefab with the white gourd. 2:2, the tutorial (#477) and stages without a path keep the white gourd.
        /// </summary>
        private void MountStageGourd(GameSession session)
        {
            // The practice board turns the white gourd's yokai details to face the middle seat.
            if (session != null && session.IsTutorial) return;
            GameStageDefinition stage = session != null ? session.CurrentStageDefinition : null;
            string path = stage != null ? stage.gourdVisualResource : null;
            if (string.IsNullOrEmpty(path) || target == null) return;
            float started = Time.realtimeSinceStartup;
            GameObject prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("C6_GOURD_MOUNT missing=" + path + " (white gourd)");
                return;
            }
            LabGourdPaint.PrepareVisual(target, prefab);
            if (Debug.isDebugBuild)
                Debug.Log("C6_GOURD_MOUNT stage=" + stage.stageIndex + " prefab=" + prefab.name
                    + " ms=" + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0"));
        }

        private void RebindScene(LabNetwork network)
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null || component is GameBattleBindings) continue;
                    foreach (FieldInfo field in component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (field.IsInitOnly) continue;
                        if (field.FieldType == typeof(LabNetwork)) field.SetValue(component, network);
                        // DEV defaults must keep the saved LabConfig; every live config reference
                        // uses the persistent room's accepted runtime copy instead.
                        else if (field.FieldType == typeof(LabConfig)
                            && !(component is LabDeveloperMode && field.Name == nameof(LabDeveloperMode.sourceConfig)))
                            field.SetValue(component, network.config);
                    }
                }
        }

        private void OnConfigApplied(LabConfig _)
        {
            if (session == null) return;
            RebindScene(session.Network);
            GetComponent<GameBattleUiPresentation>()?.ApplyOrbAppearance();
            sceneController?.ReconfigureBoard();
            seatCamera?.RefreshPose();
        }

        private void Start()
        {
            // These original controls belong to Lab's single-scene flow; the new run owns navigation.
            if (hud != null)
            {
                Route(hud.returnLobbyButton, () => session.ExitRun(), hud.pauseMenu != null
                    && hud.pauseMenu.GetComponent<GamePauseView>() != null ? "나가기" : "QUIT RUN");
                Route(hud.leaveButton, () => session.ExitRun(), "QUIT RUN");
                Route(hud.pauseButton, () => session.Pause(), null);
                Route(hud.resumeButton, () => session.Resume(), null);
            }
            if (developerMode != null) Route(developerMode.endSoloButton, () => session.ExitRun(), "QUIT RUN");
            Route(developerNextStageButton, DeveloperNextStage, null);
            RefreshDeveloperNextStage();
        }
        private void LateUpdate()
        {
            if (hud != null)
            {
                if (hud.sessionControls != null) hud.sessionControls.SetActive(false);
                if (hud.resultControls != null) hud.resultControls.SetActive(false);
                if (hud.playerSlotPanel != null) hud.playerSlotPanel.SetActive(false);
                if (hud.returnLobbyButton != null)
                {
                    // Lab exposes Return All only to the Host. In the game every participant may quit the run.
                    hud.returnLobbyButton.gameObject.SetActive(true);
                    hud.returnLobbyButton.interactable = session != null && session.State.IsRunActive;
                }
            }
            if (session == null) return;
            GameStageDefinition stage = session.CurrentStageDefinition;
            if (yokaiPresentation != null) yokaiPresentation.Bind(target, stage);
            if (stageLabel != null && session.IsTutorial) stageLabel.text = GameTutorialScript.StageLabel;
            else if (stageLabel != null)
                stageLabel.text = (session.IsTerritory ? (session.TerritoryMatchLabel + " 땅따먹기").TrimStart()
                        : "STAGE " + session.State.stageIndex + " / " + session.State.totalStages
                            + (stage != null ? " · " + stage.displayName : string.Empty))
                    + (session.IsPreparingBattle ? " · WAITING" : string.Empty);
            RefreshDeveloperNextStage();
        }
        private void DeveloperNextStage()
        {
            // Do not read/apply the DEV form: only the room's accepted stage profiles carry forward.
            if (session != null) session.DeveloperNextStage();
            RefreshDeveloperNextStage();
        }

        private void RefreshDeveloperNextStage()
        {
            if (developerNextStageButton == null) return;
            bool visible = session != null && session.Network != null
                && session.Network.DeveloperBuildAvailable && session.Network.IsHost;
            developerNextStageButton.gameObject.SetActive(visible);
            developerNextStageButton.interactable = visible && session.CanDeveloperAdvanceStage;
            var label = developerNextStageButton.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (label == null || session == null) return;
            bool last = session.State.stageIndex >= session.State.totalStages;
            string value = last ? "LAST STAGE · " + session.State.totalStages + " / " + session.State.totalStages
                : "NEXT STAGE · " + (session.State.stageIndex + 1) + " / " + session.State.totalStages
                    + "\nAPPLIED SETTINGS ONLY";
            if (label.text != value) label.text = value;
        }

        private void OnDestroy()
        {
            LabBattleAudioWarmup.Release(this); // #492: the stage's preloaded data is not kept past its Battle.
            if (session != null && session.Network != null) session.Network.ConfigApplied -= OnConfigApplied;
        }
        private static void Route(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action, string label)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            var text = button.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (label != null && text != null) text.text = label;
        }
    }
}
