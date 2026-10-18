using System;
using UnityEngine;

namespace C6Lab
{
    /// <summary>Observe a shared opt-in setting and committed results without writing gameplay state.</summary>
    [DisallowMultipleComponent]
    public sealed class LabGourdResultViewer : MonoBehaviour
    {
        public const string ResourcePath = "GourdResultViewer/ViewerUI";
        public LabNetwork Network { get; private set; }
        public GourdResultViewerUI Ui { get; private set; }
        public LabGourdResultPreview Preview { get; private set; }
        public bool IsOpen => Preview != null && !Preview.IsDisposed;
        public string LastError { get; private set; }
        private GameObject ownedUi;
        private LabNetwork subscribedNetwork;
        private string uiAttemptKey, loggedFailureKey;

        /// <summary>Attach one lightweight observer to this network's Scene; UI is loaded only for an eligible result.</summary>
        public static LabGourdResultViewer Attach(LabNetwork network)
        {
            if (!Application.isPlaying || network == null) return null;
            foreach (var viewer in network.GetComponentsInChildren<LabGourdResultViewer>(true))
                if (viewer.Network == network)
                    return viewer;
            var root = new GameObject("Completed Gourd Viewer");
            root.transform.SetParent(network.transform, false);
            var result = root.AddComponent<LabGourdResultViewer>();
            result.Network = network;
            result.Subscribe();
            result.RefreshState();
            return result;
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
                || !network.config.CompletedGourdViewerEnabled || network.config.GourdMode != LabGourdMode.Paint
                || !IsEligible(network.Snapshot, network.PaintSurface)) return false;
            var source = network.target == null ? null : network.target.GetComponent<LabGourdPaint>();
            return source != null && source.IsReady;
        }

        private void OnEnable()
        {
            Subscribe();
            RefreshState();
        }

        private void Subscribe()
        {
            if (Network == null || subscribedNetwork == Network || !isActiveAndEnabled) return;
            Unsubscribe();
            subscribedNetwork = Network;
            subscribedNetwork.ConfigApplied += OnConfigApplied;
        }

        private void Unsubscribe()
        {
            if (!ReferenceEquals(subscribedNetwork, null)) subscribedNetwork.ConfigApplied -= OnConfigApplied;
            subscribedNetwork = null;
        }

        private void OnConfigApplied(LabConfig _)
        {
            // Config is installed before some restart paths replace their old result snapshot.
            // Release now, then let LateUpdate observe the fully committed boundary before loading UI.
            ReleaseUi();
            uiAttemptKey = loggedFailureKey = null;
            LastError = null;
        }
        private void LateUpdate() => RefreshState();

        public void RefreshState()
        {
            if (!isActiveAndEnabled) return;
            if (!CanInspect(Network))
            {
                ReleaseUi();
                uiAttemptKey = loggedFailureKey = null;
                LastError = null;
                return;
            }
            LabSnapshot snapshot = Network.Snapshot;
            string key = ResultKey(snapshot);
            if (IsOpen && !MatchesPreview(snapshot)) CloseViewer();
            if (ownedUi != null && uiAttemptKey != key) ReleaseUi();
            if (EnsureUi(key, false)) Ui.SetEntryVisible(!IsOpen);
        }

        private bool MatchesPreview(LabSnapshot snapshot) => snapshot != null && Preview != null
            && snapshot.sessionId == Preview.SessionId && snapshot.round == Preview.Round
            && snapshot.paintRevision == Preview.PaintRevision;

        private static string ResultKey(LabSnapshot snapshot)
            => snapshot.sessionId + ":" + snapshot.round + ":" + snapshot.paintRevision;

        private bool EnsureUi(string key, bool retry)
        {
            if (ownedUi != null && Ui != null)
            {
                if (uiAttemptKey == key) return true;
                ReleaseUi();
            }
            if (!retry && uiAttemptKey == key) return false;
            uiAttemptKey = key;
            try
            {
                GameObject prefab = Resources.Load<GameObject>(ResourcePath);
                if (prefab == null)
                    return ReportFailure(key, "The completed gourd viewer UI prefab is missing.");
                ownedUi = Instantiate(prefab, transform);
                Ui = ownedUi.GetComponent<GourdResultViewerUI>();
                if (Ui == null || !Ui.HasRequiredReferences)
                {
                    ReleaseUi();
                    return ReportFailure(key, "The completed gourd viewer UI has missing references.");
                }
                Ui.Bind(OpenFromButton, CloseViewer, ResetView, Rotate, Zoom);
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                ReleaseUi();
                return ReportFailure(key, "Could not create the completed gourd viewer UI: " + exception.Message);
            }
        }

        private bool ReportFailure(string key, string error)
        {
            LastError = error;
            if (loggedFailureKey != key)
            {
                loggedFailureKey = key;
                Debug.LogWarning("C6_GOURD_VIEWER_UNAVAILABLE: " + error);
            }
            return false;
        }

        public bool OpenViewer()
        {
            if (!isActiveAndEnabled || !CanInspect(Network))
            {
                ReleaseUi();
                return false;
            }
            LabSnapshot snapshot = Network.Snapshot;
            if (IsOpen && MatchesPreview(snapshot)) return true;
            CloseViewer();
            string key = ResultKey(snapshot);
            // Direct, explicit OpenViewer calls may retry a restored prefab; frame polling never spams retries.
            if (!EnsureUi(key, true)) return false;
            if (!LabGourdResultPreview.TryCreate(Network, out var preview, out string error, Ui.previewTextureSize))
                return ReportFailure(key, error);
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

        private void ReleaseUi()
        {
            CloseViewer();
            if (Ui != null) Ui.SetEntryVisible(false);
            if (ownedUi != null)
            {
                ownedUi.SetActive(false);
                Destroy(ownedUi);
            }
            Ui = null;
            ownedUi = null;
        }

        private void OnDisable()
        {
            Unsubscribe();
            ReleaseUi();
            uiAttemptKey = loggedFailureKey = null;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            ReleaseUi();
        }
    }
}
