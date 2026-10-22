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
        public GameYokaiPresentation yokaiPresentation;
        private GameSession session;

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
                stageLabel.text = "STAGE " + session.State.stageIndex + " / " + session.State.totalStages
                    + (stage != null ? " · " + stage.displayName : string.Empty)
                    + (session.State.phase == GameFlowPhase.LoadingBattle ? " · WAITING" : string.Empty);
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
