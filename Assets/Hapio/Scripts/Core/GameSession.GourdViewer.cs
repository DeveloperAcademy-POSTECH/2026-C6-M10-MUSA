using System;
using C6Lab;
using UnityEngine;

namespace Hapio
{
    public enum GameGourdViewerState { Disabled, Pending, Ready, Failed }

    public sealed partial class GameSession
    {
        private LabGourdPreviewPreparation viewerPreparation;
        private GameGourdViewerState viewerState;
        private string viewerError;
        private string viewerRun, viewerSession;
        private int viewerStage;
        private uint viewerRound;
        private double viewerProgressAt, nextViewerTick, nextViewerCreateAt;
        private ulong viewerLastSequence, viewerApplied, viewerRendered;
        // A result may receive pages after Battle unload. Only a stalled job times out;
        // this is a background recovery budget, never a new full-screen wait.
        private const double ViewerIdleTimeout = 30d;

        public GameGourdViewerState GourdViewerState => PreviewTexture != null
            ? GameGourdViewerState.Ready : viewerState;
        public string GourdViewerError => viewerError;
        private bool WantsGourdViewer => network != null && network.config != null
            && network.config.CompletedGourdViewerEnabled && network.config.GourdMode == LabGourdMode.Paint;
        private bool SameViewerGeneration => viewerRun == state.runId && viewerStage == state.stageIndex
            && viewerSession == state.battleSessionId && viewerRound == state.battleRound;

        private void BeginGourdViewerPreparation()
        {
            if (viewerPreparation != null || viewerState == GameGourdViewerState.Failed || !WantsGourdViewer) return;
            viewerState = GameGourdViewerState.Pending;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextViewerCreateAt) return;
            nextViewerCreateAt = now + .2d;
            if (!LabGourdPreviewPreparation.TryCreate(network, out var job, out string error))
            {
                // A model can be mounting during the finishing frames. The existing finish
                // deadline bounds retries before the Scene handoff decides the final failure.
                viewerError = error;
                return;
            }
            viewerPreparation = job;
            viewerRun = state.runId; viewerStage = state.stageIndex;
            viewerSession = state.battleSessionId; viewerRound = state.battleRound;
            viewerError = null;
            viewerLastSequence = viewerApplied = viewerRendered = 0;
            viewerProgressAt = Time.realtimeSinceStartupAsDouble;
            nextViewerTick = 0d;
            Changed?.Invoke();
        }

        private void UpdateGourdViewerPreparation(double now)
        {
            if (viewerPreparation == null || now < nextViewerTick) return;
            nextViewerTick = now + .02d;
            if (goingHome || !SameViewerGeneration || state.phase != GameFlowPhase.Playing
                && state.phase != GameFlowPhase.StageResult && state.phase != GameFlowPhase.Completed)
            {
                CancelGourdViewerPreparation();
                return;
            }
            viewerPreparation.Tick();
            if (viewerPreparation.Failed)
            {
                FailGourdViewer(viewerPreparation.Error);
                return;
            }
            if (viewerPreparation.Ready)
            {
                LabGourdResultPreview completed = viewerPreparation.TakePreview();
                if (completed == null || completed.IsDisposed || completed.Texture == null)
                { FailGourdViewer("The completed preview has no valid output texture."); return; }
                preview?.Dispose();
                preview = completed;
                CancelGourdViewerPreparation();
                viewerState = GameGourdViewerState.Ready; viewerError = null;
                GameGourdPreviewPresentation.Apply(preview, transform);
                RememberCapturedResult();
                Changed?.Invoke();
                return;
            }
            ulong sequence = network.PaintArtJournal?.LastSequence ?? 0;
            ulong applied = viewerPreparation.AppliedThrough, rendered = viewerPreparation.RenderedThrough;
            if (sequence != viewerLastSequence || applied != viewerApplied || rendered != viewerRendered)
            {
                viewerLastSequence = sequence; viewerApplied = applied; viewerRendered = rendered;
                viewerProgressAt = now;
            }
            if (now - viewerProgressAt >= ViewerIdleTimeout)
                FailGourdViewer("Completed artwork preparation stopped making progress.");
        }

        private void FinishGourdViewerHandoff()
        {
            if (!WantsGourdViewer || PreviewTexture != null || viewerPreparation != null) return;
            FailGourdViewer(viewerError ?? "The completed model could not be preserved before leaving Battle.");
        }

        private void FailGourdViewer(string error)
        {
            CancelGourdViewerPreparation();
            viewerState = GameGourdViewerState.Failed;
            viewerError = string.IsNullOrEmpty(error) ? "Completed artwork preparation failed." : error;
            UiNotice = "봉인에는 성공했지만, 완성 박 관람을 준비하지 못했소.";
            Debug.LogWarning("C6_GOURD_VIEWER_FAILED stage=" + state.stageIndex + " reason=" + viewerError);
            Changed?.Invoke();
        }

        private void CancelGourdViewerPreparation()
        {
            viewerPreparation?.Dispose();
            viewerPreparation = null;
            nextViewerTick = nextViewerCreateAt = 0d;
        }
    }
}
