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
            session = GameSession.EnsureExists();
            LabNetwork network = session.Network;
            network.BindBattleReferences(board, target, battleCamera, battleFloor, impactFeedback, projectileRoot);
            RebindScene(network);
            session.NotifyBattleBound(board);
            network.ConfigApplied += OnConfigApplied;
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
            sceneController?.ReconfigureBoard();
            seatCamera?.RefreshPose();
        }

        private void Start()
        {
            // These original controls belong to Lab's single-scene flow; the new run owns navigation.
            if (hud != null)
            {
                Route(hud.returnLobbyButton, () => session.ExitRun(), "QUIT RUN");
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
            if (stageLabel != null)
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
