using System;
using UnityEngine;

namespace C6Lab.Experiments
{
    /// <summary>Explicit opt-in experiment. It observes committed results and never writes gameplay state.</summary>
    public sealed class GourdResultViewerExperiment : MonoBehaviour
    {
        public const string ResourcePath = "GourdResultViewer/ViewerUI";
        public const string CommandLineFlag = "--lab-gourd-result-viewer";
        public LabNetwork Network { get; private set; }
        public GourdResultViewerUI Ui { get; private set; }
        public LabGourdResultPreview Preview { get; private set; }
        public bool IsOpen => Preview != null && !Preview.IsDisposed;
        public string LastError { get; private set; }
        private GameObject ownedUi;
        private static GourdResultViewerExperiment active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Debug.isDebugBuild || Application.isEditor) return;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), CommandLineFlag) >= 0) StartInCurrentScene();
        }

        public static GourdResultViewerExperiment StartInCurrentScene()
        {
            if (!Application.isPlaying || (!Application.isEditor && !Debug.isDebugBuild)) return null;
            if (active != null) return active;
            LabNetwork network = FindFirstObjectByType<LabNetwork>();
            GameObject prefab = Resources.Load<GameObject>(ResourcePath);
            if (network == null || prefab == null)
            {
                Debug.LogWarning("C6_GOURD_VIEWER_EXPERIMENT_UNAVAILABLE: Open Lab and create the experiment UI prefab first.");
                return null;
            }
            var root = new GameObject("Gourd Result Viewer Experiment");
            var experiment = root.AddComponent<GourdResultViewerExperiment>();
            experiment.ownedUi = Instantiate(prefab, root.transform);
            experiment.Ui = experiment.ownedUi.GetComponent<GourdResultViewerUI>();
            if (experiment.Ui == null || !experiment.Ui.HasRequiredReferences)
            {
                Debug.LogWarning("C6_GOURD_VIEWER_EXPERIMENT_UNAVAILABLE: The UI prefab has missing references.");
                Destroy(root);
                return null;
            }
            active = experiment;
            experiment.Network = network;
            experiment.Ui.Bind(experiment.OpenFromButton, experiment.CloseViewer,
                experiment.ResetView, experiment.Rotate, experiment.Zoom);
            experiment.RefreshState();
            return experiment;
        }

        public static void StopInCurrentScene()
        {
            foreach (var experiment in FindObjectsByType<GourdResultViewerExperiment>(FindObjectsSortMode.None))
            {
                experiment.CloseViewer();
                experiment.gameObject.SetActive(false);
                Destroy(experiment.gameObject);
            }
            active = null;
        }

        public static bool IsEligible(LabSnapshot snapshot, LabPaintSurface surface)
        {
            return snapshot != null && surface != null && snapshot.phase == LabPhase.Victory
                && !string.IsNullOrEmpty(snapshot.sessionId) && snapshot.round > 0
                && snapshot.paintGoalPercent == 100 && snapshot.paintCellCount > 0
                && snapshot.paintCoveredCount == snapshot.paintCellCount && surface.IsComplete
                && snapshot.paintCellCount == surface.CellCount && snapshot.paintCoveredCount == surface.CoveredCount
                && snapshot.paintColumns == surface.Columns && snapshot.paintRows == surface.Rows
                && snapshot.paintRevision == surface.Revision && snapshot.paintCount == surface.PaintCount;
        }

        public static bool CanInspect(LabNetwork network)
        {
            if (network == null || !network.IsConnected || network.config == null
                || network.config.GourdMode != LabGourdMode.Paint || !IsEligible(network.Snapshot, network.PaintSurface))
                return false;
            var source = network.target == null ? null : network.target.GetComponent<LabGourdPaint>();
            return source != null && source.IsReady;
        }

        private void LateUpdate() => RefreshState();

        public void RefreshState()
        {
            if (Ui == null) return;
            bool eligible = CanInspect(Network);
            LabSnapshot snapshot = Network != null ? Network.Snapshot : null;
            if (IsOpen && (!eligible || snapshot.sessionId != Preview.SessionId || snapshot.round != Preview.Round
                || snapshot.paintRevision != Preview.PaintRevision)) CloseViewer();
            Ui.SetEntryVisible(eligible && !IsOpen);
        }

        public bool OpenViewer()
        {
            if (IsOpen) return true;
            if (Ui == null || !CanInspect(Network)) return false;
            if (!LabGourdResultPreview.TryCreate(Network, out var preview, out string error, Ui.previewTextureSize))
            {
                LastError = error;
                Debug.LogWarning("C6_GOURD_VIEWER_EXPERIMENT_UNAVAILABLE: " + error);
                return false;
            }
            LastError = null;
            Preview = preview;
            Ui.Show(preview.Texture);
            return true;
        }

        private void OpenFromButton() => OpenViewer();
        public void ResetView() { if (IsOpen) Preview.ResetView(); }
        public void Rotate(Vector2 delta) { if (IsOpen) Preview.Rotate(delta); }
        public void Zoom(float delta) { if (IsOpen) Preview.Zoom(delta); }

        public void CloseViewer()
        {
            if (Ui != null) Ui.Hide();
            Preview?.Dispose();
            Preview = null;
        }

        private void OnDisable() => CloseViewer();
        private void OnDestroy()
        {
            CloseViewer();
            if (active == this) active = null;
            if (ownedUi != null) Destroy(ownedUi);
            ownedUi = null;
        }
    }
}
