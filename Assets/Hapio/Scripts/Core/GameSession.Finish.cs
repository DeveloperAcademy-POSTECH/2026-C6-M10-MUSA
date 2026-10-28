using System;
using System.Collections.Generic;
using C6Lab;
using Unity.Netcode;
using UnityEngine;

namespace Hapio
{
    public sealed partial class GameSession
    {
        [Serializable]
        sealed class PaintFinishControl
        {
            public string kind, runId, sessionId;
            public int stageIndex, paintCount, columns, rows;
            public uint revision, round, paintRevision, visualVersion, generation;
            public double startsAt, deadline;
            public bool skipped;
        }
        readonly Queue<(ulong sender, PaintFinishControl value)> paintFinishControls
            = new Queue<(ulong, PaintFinishControl)>();
        PaintFinishControl finishIdentity;
        GamePaintFinishBarrier finishBarrier;
        LabPaintFinishPresentation finishPresentation;
        double nextFinishSendAt, finishWaitAt = -1d, finishStartsAt;
        double finishDeadlineAt = -1d, finishServerDeadline, nextResultCaptureAt;
        string capturedRunId;
        int capturedStage;
        uint capturedPaintRevision, capturedVisualVersion;
        uint finishGeneration;
        bool finishCommitted, finishReleased;
        public bool IsFinishingBattle => state.phase == GameFlowPhase.Playing && finishIdentity != null && !finishReleased;
        public float PaintFinishProgress => finishPresentation != null ? finishPresentation.Progress : 0f;

        // The transport already filters Host-only messages; retain authorization here as well.
        bool ReceivePaintFinishControl(ulong sender, string json)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 4096) return false;
            PaintFinishControl value;
            try { value = JsonUtility.FromJson<PaintFinishControl>(json); }
            catch (ArgumentException) { return false; }
            if (value == null || value.kind == null || !value.kind.StartsWith("paint_finish_", StringComparison.Ordinal)) return false;
            if (network == null || !network.IsConnected || goingHome || paintFinishControls.Count >= 128) return true;
            if (network.IsHost)
            {
                if (!ContainsFinishPlayer(sender) || sender == network.LocalPlayerId) return true;
            }
            else if (sender != NetworkManager.ServerClientId) return true;
            if (value.runId != state.runId || value.stageIndex != state.stageIndex || value.revision != state.revision
                || value.sessionId != state.battleSessionId || value.round != state.battleRound
                || state.phase != GameFlowPhase.Playing || state.mode != GameMode.Story
                || value.columns <= 0 || value.rows <= 0 || value.paintCount < 0
                || !FiniteFinishTime(value.startsAt) || value.startsAt < 0d
                || !FiniteFinishTime(value.deadline) || value.deadline <= 0d) return true;
            if (!network.IsHost) ObserveFinishDeadline(value.deadline, Time.realtimeSinceStartupAsDouble);
            paintFinishControls.Enqueue((sender, value));
            return true;
        }
        bool ContainsFinishPlayer(ulong player)
        {
            foreach (ulong id in network.ConnectedPlayerIds) if (id == player) return true;
            return false;
        }
        bool FinishMatches(PaintFinishControl value) => finishIdentity != null && value != null
            && value.runId == finishIdentity.runId && value.stageIndex == finishIdentity.stageIndex
            && value.revision == finishIdentity.revision && value.sessionId == finishIdentity.sessionId
            && value.round == finishIdentity.round && value.paintRevision == finishIdentity.paintRevision
            && value.visualVersion == finishIdentity.visualVersion && value.paintCount == finishIdentity.paintCount
            && value.columns == finishIdentity.columns && value.rows == finishIdentity.rows;

        void UpdatePaintFinish(double realNow)
        {
            if (network == null || !network.IsConnected || state.phase != GameFlowPhase.Playing || state.mode != GameMode.Story)
            {
                // Keep the final identity, copy and deadline until the result Scene has used them.
                if (finishIdentity != null && state.phase != GameFlowPhase.StageResult
                    && state.phase != GameFlowPhase.Completed) ResetPaintFinish();
                return;
            }
            if (network.target != null)
            {
                var value = LabPaintFinishPresentation.Attach(network);
                if (value != finishPresentation) { finishPresentation = value; if (value != null) value.ExternallyCoordinated = true; }
                if (value != null) value.ExternallyCoordinated = true;
            }
            LabSnapshot snapshot = network.Snapshot;
            if (!MatchesBattle(snapshot) || snapshot.phase != LabPhase.Victory) return;
            ObserveFinishDeadline(0d, realNow);
            if (network.IsHost && finishServerDeadline <= 0d && network.TryGetGameServerTime(out double budgetNow))
                finishServerDeadline = budgetNow + Math.Max(0d, finishDeadlineAt - realNow);
            if (network.IsHost && finishIdentity == null && LabPaintFinishPresentation.IsCompletedSurface(network))
            {
                finishIdentity = MakeFinishIdentity(snapshot);
                finishBarrier = new GamePaintFinishBarrier(network.ConnectedPlayerIds, finishWaitAt);
                nextFinishSendAt = 0d;
            }
            while (paintFinishControls.Count > 0)
            {
                var incoming = paintFinishControls.Dequeue();
                ApplyPaintFinishControl(incoming.sender, incoming.value, realNow);
            }
            if (finishIdentity == null)
            {
                // A corrupt/incomplete terminal presentation must not hold the run forever.
                if (network.IsHost && realNow >= finishDeadlineAt)
                { finishReleased = true; finishPresentation?.Skip(); if (finishPresentation != null) finishPresentation.ResultReleased = true; }
                return;
            }
            bool ready = PrepareFinalPaint();
            // The Battle still owns the live mesh/GPU maps here. Copy once during preparation or
            // the finish, so result navigation does not repeat this work after the effect.
            if (ready && realNow >= nextResultCaptureAt && !HasCapturedResult())
            {
                nextResultCaptureAt = realNow + .2d;
                CaptureResult();
            }
            if (network.IsHost)
            {
                if (ready) finishBarrier.Prepared(network.LocalPlayerId);
                // One budget starts at Victory, not again when the surface becomes ready.
                if (realNow >= finishDeadlineAt) finishBarrier.Expire(finishWaitAt + GamePaintFinishBarrier.TimeoutSeconds);
                if (!finishBarrier.Released && network.TryGetGameServerTime(out double scheduleNow))
                {
                    bool singleLocal = network.IsDeveloperSolo;
                    if (singleLocal && ready && !finishBarrier.Committed)
                        finishBarrier.BeginLocal(network.LocalPlayerId, scheduleNow);
                    // Commit a valid last-moment receipt before considering a replacement reservation.
                    if (!singleLocal && !finishBarrier.Commit(scheduleNow) && !finishBarrier.Committed
                        && finishBarrier.Schedule(scheduleNow)) nextFinishSendAt = 0d;
                    finishGeneration = finishBarrier.Generation;
                    finishStartsAt = finishBarrier.StartsAt;
                    if (ready) finishBarrier.ReadyForSchedule(network.LocalPlayerId, finishGeneration);
                    if (!singleLocal) finishBarrier.Commit(scheduleNow);
                    if (finishBarrier.Committed && !finishCommitted)
                    { finishCommitted = true; nextFinishSendAt = 0d; }
                }
            }
            if (finishCommitted && ready && network.TryGetGameServerTime(out double now) && now >= finishStartsAt)
            {
                if (finishPresentation != null && !finishPresentation.Running && !finishPresentation.Complete)
                    finishPresentation.Begin();
                finishPresentation?.Sample(now - finishStartsAt);
                if (finishPresentation != null && finishPresentation.Complete && network.IsHost)
                    finishBarrier.Completed(network.LocalPlayerId, finishGeneration);
            }
            if (network.IsHost)
            {
                if (network.TryGetGameServerTime(out double releaseNow))
                    finishBarrier.TryRelease(releaseNow, LabPaintFinishPresentation.Duration);
                bool expired = finishBarrier.Expire(realNow) || finishBarrier.Skipped;
                if (expired) UiNotice = "The completed paint was preserved; a finishing effect could not be synchronized.";
                if (finishBarrier.Released && !finishReleased)
                {
                    finishReleased = true;
                    if (finishBarrier.Skipped) finishPresentation?.Skip();
                    if (finishPresentation != null) finishPresentation.ResultReleased = true;
                    SendFinish("release", finishBarrier.Skipped);
                    Changed?.Invoke();
                }
            }
            if (realNow >= nextFinishSendAt && !finishReleased)
            {
                nextFinishSendAt = realNow + .2d;
                if (network.IsHost) SendFinish(finishCommitted ? "commit" : "prepare");
                else if (finishPresentation != null && finishPresentation.Complete && finishCommitted) SendFinish("done");
                else if (ready && finishGeneration > 0) SendFinish("schedule_ready");
            }
            if (!network.IsHost && realNow >= finishDeadlineAt && !finishReleased)
            {
                // Wait for the authoritative result state, with local resources released after a finite budget.
                finishPresentation?.Skip();
            }
        }
        PaintFinishControl MakeFinishIdentity(LabSnapshot snapshot) => new PaintFinishControl
        {
            runId = state.runId, revision = state.revision, stageIndex = state.stageIndex,
            sessionId = snapshot.sessionId, round = snapshot.round, paintRevision = snapshot.paintRevision,
            visualVersion = network.PaintSurface.VisualVersion, paintCount = snapshot.paintCount,
            columns = snapshot.paintColumns, rows = snapshot.paintRows
        };
        bool PrepareFinalPaint()
        {
            if (finishIdentity == null || !LabPaintFinishPresentation.IsCompletedSurface(network)) return false;
            LabSnapshot s = network.Snapshot;
            if (s.paintRevision != finishIdentity.paintRevision || s.paintCount != finishIdentity.paintCount
                || s.paintColumns != finishIdentity.columns || s.paintRows != finishIdentity.rows
                || network.PaintSurface.VisualVersion != finishIdentity.visualVersion) return false;
            return finishPresentation != null && finishPresentation.Prepare();
        }
        void ApplyPaintFinishControl(ulong sender, PaintFinishControl value, double realNow)
        {
            if (!network.IsHost && finishIdentity == null
                && (value.kind == "paint_finish_prepare" || value.kind == "paint_finish_commit"
                    || value.kind == "paint_finish_release"))
            { finishIdentity = value; ObserveFinishDeadline(value.deadline, realNow); nextFinishSendAt = 0d; }
            if (!FinishMatches(value) || finishReleased) return;
            if (network.IsHost)
            {
                if (finishBarrier == null) return;
                if (value.kind == "paint_finish_schedule_ready")
                    finishBarrier.ReadyForSchedule(sender, value.generation);
                else if (value.kind == "paint_finish_done"
                    && network.TryGetGameServerTime(out double now)
                    && now >= finishBarrier.StartsAt + LabPaintFinishPresentation.Duration)
                    finishBarrier.Completed(sender, value.generation);
                return;
            }
            if (value.kind == "paint_finish_release")
            {
                // Release is authoritative, including the bounded failure path before scheduling.
                if (value.generation < finishGeneration) return;
                finishReleased = true;
                if (value.skipped) finishPresentation?.Skip();
                if (finishPresentation != null) finishPresentation.ResultReleased = true;
                return;
            }
            if (value.kind != "paint_finish_prepare" && value.kind != "paint_finish_commit") return;
            if (value.generation == 0 || value.generation < finishGeneration || value.startsAt <= 0d) return;
            // The trusted Host assigns one schedule per generation. ACK identity never compares JSON doubles.
            if (finishCommitted && value.generation != finishGeneration) return;
            if (value.generation > finishGeneration)
            { finishGeneration = value.generation; finishStartsAt = value.startsAt; nextFinishSendAt = 0d; }
            if (value.kind == "paint_finish_commit") finishCommitted = true;
        }
        void SendFinish(string suffix, bool skipped = false)
        {
            if (finishIdentity == null) return;
            var value = new PaintFinishControl
            {
                kind = "paint_finish_" + suffix, runId = finishIdentity.runId, stageIndex = finishIdentity.stageIndex,
                revision = finishIdentity.revision, sessionId = finishIdentity.sessionId, round = finishIdentity.round,
                paintRevision = finishIdentity.paintRevision, visualVersion = finishIdentity.visualVersion,
                paintCount = finishIdentity.paintCount, columns = finishIdentity.columns, rows = finishIdentity.rows,
                generation = finishGeneration, startsAt = finishStartsAt,
                deadline = finishServerDeadline, skipped = skipped
            };
            string json = JsonUtility.ToJson(value);
            if (network.IsHost) network.BroadcastGameFlow(json); else network.SendGameFlowToHost(json);
        }
        bool CanLeavePaintFinish(LabSnapshot snapshot)
        {
            if (snapshot == null || snapshot.phase != LabPhase.Victory) return false;
            if (state.mode == GameMode.Territory || snapshot.teamTerritory) return true;
            return finishReleased && MatchesBattle(snapshot);
        }
        void ResetPaintFinish()
        {
            paintFinishControls.Clear();
            if (finishPresentation != null) { finishPresentation.ResetPresentation(); }
            finishPresentation = null; finishIdentity = null; finishBarrier = null;
            finishWaitAt = -1d; nextFinishSendAt = finishStartsAt = 0d; finishGeneration = 0;
            finishDeadlineAt = -1d; finishServerDeadline = nextResultCaptureAt = 0d;
            capturedRunId = null; capturedStage = 0; capturedPaintRevision = capturedVisualVersion = 0;
            finishCommitted = finishReleased = false;
        }
        void ObserveFinishDeadline(double sharedDeadline, double realNow)
        {
            if (finishWaitAt < 0d)
            {
                finishWaitAt = realNow;
                finishDeadlineAt = realNow + GamePaintFinishBarrier.TimeoutSeconds;
            }
            if (sharedDeadline <= 0d || !FiniteFinishTime(sharedDeadline)
                || !network.TryGetGameServerTime(out double serverNow)) return;
            // A repeated prepare/state or a new schedule generation must never restart the budget.
            finishServerDeadline = finishServerDeadline > 0d
                ? Math.Min(finishServerDeadline, sharedDeadline) : sharedDeadline;
            finishDeadlineAt = Math.Min(finishDeadlineAt,
                realNow + Math.Max(0d, finishServerDeadline - serverNow));
        }
        double ResultCaptureDeadline(double realNow)
        {
            if (state.mode != GameMode.Story) return realNow + loadingTimeoutSeconds;
            ObserveFinishDeadline(finishServerDeadline, realNow);
            return finishDeadlineAt;
        }
        void ReadFinishDeadline(Control control)
        {
            if (state.mode == GameMode.Story && control.finishDeadline > 0d
                && (state.phase == GameFlowPhase.Playing || state.phase == GameFlowPhase.StageResult
                    || state.phase == GameFlowPhase.Completed))
                ObserveFinishDeadline(control.finishDeadline, Time.realtimeSinceStartupAsDouble);
        }
        bool HasCapturedResult()
        {
            if (ResultSnapshot == null || capturedRunId != state.runId || capturedStage != state.stageIndex
                || ResultSnapshot.sessionId != state.battleSessionId || ResultSnapshot.round != state.battleRound
                || ResultSnapshot.paintRevision != capturedPaintRevision) return false;
            if (network.PaintSurface != null && network.PaintSurface.Revision == capturedPaintRevision
                && network.PaintSurface.VisualVersion != capturedVisualVersion) return false;
            bool needsPreview = network.config != null && network.config.CompletedGourdViewerEnabled
                && network.config.GourdMode == LabGourdMode.Paint;
            return !needsPreview || preview != null && !preview.IsDisposed
                && preview.PaintRevision == capturedPaintRevision;
        }
        static bool FiniteFinishTime(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
