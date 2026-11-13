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
            public uint revision, round, paintRevision, visualSequence, generation;
            public double startsAt, deadline;
            public bool skipped;
            /// <summary>The Host's final-hit replay record (prepare/commit only). Empty keeps the fixed 1.2 s finish.</summary>
            public string replay;
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
        // Final-hit replay (story stages): one Host record, played by every device from the shared start.
        LabFinishReplay finishReplay;
        LabFinishReplayShot replayShot;
        bool replayDecided;
        double replayWaitLimit = -1d;
        /// <summary>Server-time margin kept after the replay inside the shared finish budget.</summary>
        const double ReplayBudgetMargin = 1.5d;
        public bool IsFinishingBattle => state.phase == GameFlowPhase.Playing && finishIdentity != null && !finishReleased;
        public float PaintFinishProgress => ReplayPlaying
            ? Mathf.Clamp01(finishReplay.Elapsed / LabFinishReplay.Duration)
            : finishPresentation != null ? finishPresentation.Progress : 0f;
        /// <summary>This device plays the final-hit replay for the current finish (not the fixed 1.2 s effect).</summary>
        public bool FinishUsesReplay => replayShot != null;
        public LabFinishReplay FinishReplay => finishReplay;
        bool ReplayPlaying => replayShot != null && finishReplay != null;
        bool ReplayFinish => finishIdentity != null && !string.IsNullOrEmpty(finishIdentity.replay);
        bool LocalFinishComplete => ReplayPlaying ? finishReplay.Complete
            : finishPresentation != null && finishPresentation.Complete;

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
            && value.visualSequence == finishIdentity.visualSequence && value.paintCount == finishIdentity.paintCount
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
                finishReplay = LabFinishReplay.Attach(network);
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
                replayDecided = false;
            }
            // Guests adopt the record with the first prepare/commit, so nothing is sent until it is decided.
            if (network.IsHost && finishIdentity != null && !replayDecided) DecideFinishReplay(realNow);
            while (paintFinishControls.Count > 0)
            {
                var incoming = paintFinishControls.Dequeue();
                ApplyPaintFinishControl(incoming.sender, incoming.value, realNow);
            }
            if (finishIdentity == null)
            {
                // A corrupt/incomplete terminal presentation must not hold the run forever.
                if (network.IsHost && realNow >= finishDeadlineAt)
                {
                    finishReleased = true; finishPresentation?.Skip(); finishReplay?.Skip();
                    if (finishPresentation != null) finishPresentation.ResultReleased = true;
                }
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
                if (!finishBarrier.Released && replayDecided && network.TryGetGameServerTime(out double scheduleNow))
                {
                    bool singleLocal = network.IsDeveloperSolo;
                    if (singleLocal && ready && !finishBarrier.Committed)
                        finishBarrier.BeginLocal(network.LocalPlayerId, scheduleNow);
                    // Commit a valid last-moment receipt before considering a replacement reservation.
                    if (!singleLocal && !finishBarrier.Commit(scheduleNow) && !finishBarrier.Committed
                        && finishBarrier.Schedule(scheduleNow)) nextFinishSendAt = 0d;
                    finishGeneration = finishBarrier.Generation;
                    finishStartsAt = finishBarrier.StartsAt;
                    // A late reservation can no longer fit the replay in the shared budget: fall back to the fixed
                    // effect before this generation is committed. Guests adopt the change with its prepare.
                    if (ReplayFinish && !finishBarrier.Committed && finishGeneration > 0
                        && finishStartsAt + LabFinishReplay.Duration + ReplayBudgetMargin > finishServerDeadline)
                    {
                        finishIdentity.replay = string.Empty;
                        AdoptFinishReplay(string.Empty);
                        nextFinishSendAt = 0d;
                        Debug.Log("C6_FINISH_REPLAY fallback reason=late_schedule generation=" + finishGeneration);
                    }
                    if (ready) finishBarrier.ReadyForSchedule(network.LocalPlayerId, finishGeneration);
                    if (!singleLocal) finishBarrier.Commit(scheduleNow);
                    if (finishBarrier.Committed && !finishCommitted)
                    { finishCommitted = true; nextFinishSendAt = 0d; }
                }
            }
            if (finishCommitted && ready && network.TryGetGameServerTime(out double now) && now >= finishStartsAt)
            {
                // A device that cannot start the replay falls back to the fixed effect on its own screen.
                if (ReplayPlaying && !finishReplay.Running && !finishReplay.Complete && !finishReplay.Begin(replayShot))
                    replayShot = null;
                if (ReplayPlaying) finishReplay.Sample(now - finishStartsAt);
                else
                {
                    if (finishPresentation != null && !finishPresentation.Running && !finishPresentation.Complete)
                        finishPresentation.Begin();
                    finishPresentation?.Sample(now - finishStartsAt);
                }
                if (LocalFinishComplete && network.IsHost)
                    finishBarrier.Completed(network.LocalPlayerId, finishGeneration);
            }
            if (network.IsHost)
            {
                if (network.TryGetGameServerTime(out double releaseNow))
                    finishBarrier.TryRelease(releaseNow, ReplayFinish ? 0d : LabPaintFinishPresentation.Duration);
                bool expired = finishBarrier.Expire(realNow) || finishBarrier.Skipped;
                if (expired) UiNotice = "The completed paint was preserved; a finishing effect could not be synchronized.";
                if (finishBarrier.Released && !finishReleased)
                {
                    finishReleased = true;
                    if (finishBarrier.Skipped) { finishPresentation?.Skip(); finishReplay?.Skip(); }
                    if (finishPresentation != null) finishPresentation.ResultReleased = true;
                    SendFinish("release", finishBarrier.Skipped);
                    Changed?.Invoke();
                }
            }
            if (realNow >= nextFinishSendAt && !finishReleased)
            {
                nextFinishSendAt = realNow + .2d;
                if (network.IsHost) { if (replayDecided) SendFinish(finishCommitted ? "commit" : "prepare"); }
                else if (LocalFinishComplete && finishCommitted) SendFinish("done");
                else if (ready && finishGeneration > 0) SendFinish("schedule_ready");
            }
            if (!network.IsHost && realNow >= finishDeadlineAt && !finishReleased)
            {
                // Wait for the authoritative result state, with local resources released after a finite budget.
                finishPresentation?.Skip();
                finishReplay?.Skip();
            }
        }
        PaintFinishControl MakeFinishIdentity(LabSnapshot snapshot) => new PaintFinishControl
        {
            runId = state.runId, revision = state.revision, stageIndex = state.stageIndex,
            sessionId = snapshot.sessionId, round = snapshot.round, paintRevision = snapshot.paintRevision,
            visualSequence = network.PaintSurface.LastVisualSequence, paintCount = snapshot.paintCount,
            columns = snapshot.paintColumns, rows = snapshot.paintRows
        };
        bool PrepareFinalPaint()
        {
            if (finishIdentity == null || !LabPaintFinishPresentation.IsCompletedSurface(network)) return false;
            return FinalPaintMatches() && finishPresentation != null && finishPresentation.Prepare();
        }
        bool FinalPaintMatches()
        {
            LabSnapshot s = network.Snapshot;
            return s.paintRevision == finishIdentity.paintRevision && s.paintCount == finishIdentity.paintCount
                && s.paintColumns == finishIdentity.columns && s.paintRows == finishIdentity.rows
                && network.PaintSurface.LastVisualSequence == finishIdentity.visualSequence;
        }
        void ApplyPaintFinishControl(ulong sender, PaintFinishControl value, double realNow)
        {
            if (!network.IsHost && finishIdentity == null
                && (value.kind == "paint_finish_prepare" || value.kind == "paint_finish_commit"
                    || value.kind == "paint_finish_release"))
            {
                finishIdentity = value; ObserveFinishDeadline(value.deadline, realNow); nextFinishSendAt = 0d;
                AdoptFinishReplay(value.replay);
            }
            if (!FinishMatches(value) || finishReleased) return;
            if (network.IsHost)
            {
                if (finishBarrier == null) return;
                if (value.kind == "paint_finish_schedule_ready")
                    finishBarrier.ReadyForSchedule(sender, value.generation);
                // A replay may be skipped on that device, so its receipt counts from the shared start.
                else if (value.kind == "paint_finish_done"
                    && network.TryGetGameServerTime(out double now)
                    && now >= finishBarrier.StartsAt + (ReplayFinish ? 0d : LabPaintFinishPresentation.Duration))
                    finishBarrier.Completed(sender, value.generation);
                return;
            }
            if (value.kind == "paint_finish_release")
            {
                // Release is authoritative, including the bounded failure path before scheduling.
                if (value.generation < finishGeneration) return;
                finishReleased = true;
                if (value.skipped) { finishPresentation?.Skip(); finishReplay?.Skip(); }
                if (finishPresentation != null) finishPresentation.ResultReleased = true;
                return;
            }
            if (value.kind != "paint_finish_prepare" && value.kind != "paint_finish_commit") return;
            if (value.generation == 0 || value.generation < finishGeneration || value.startsAt <= 0d) return;
            // The trusted Host assigns one schedule per generation. ACK identity never compares JSON doubles.
            if (finishCommitted && value.generation != finishGeneration) return;
            if (value.generation > finishGeneration)
            { finishGeneration = value.generation; finishStartsAt = value.startsAt; nextFinishSendAt = 0d; }
            // The Host may drop the replay for a late reservation; follow it until the commit fixes the mode.
            if (!finishCommitted && (finishIdentity.replay ?? string.Empty) != (value.replay ?? string.Empty))
            {
                finishIdentity.replay = value.replay ?? string.Empty;
                AdoptFinishReplay(finishIdentity.replay);
            }
            if (value.kind == "paint_finish_commit") finishCommitted = true;
        }
        void SendFinish(string suffix, bool skipped = false)
        {
            if (finishIdentity == null) return;
            var value = new PaintFinishControl
            {
                kind = "paint_finish_" + suffix, runId = finishIdentity.runId, stageIndex = finishIdentity.stageIndex,
                revision = finishIdentity.revision, sessionId = finishIdentity.sessionId, round = finishIdentity.round,
                paintRevision = finishIdentity.paintRevision, visualSequence = finishIdentity.visualSequence,
                paintCount = finishIdentity.paintCount, columns = finishIdentity.columns, rows = finishIdentity.rows,
                generation = finishGeneration, startsAt = finishStartsAt,
                deadline = finishServerDeadline, skipped = skipped,
                replay = suffix == "prepare" || suffix == "commit" ? finishIdentity.replay : null
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
            if (finishReplay != null) finishReplay.ResetPresentation();
            finishPresentation = null; finishIdentity = null; finishBarrier = null;
            finishReplay = null; replayShot = null; replayDecided = false; replayWaitLimit = -1d;
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
            if (IsTerritory) return TerritoryResultDeadline(realNow); // #717
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
            return !ResultNeedsPreview(ResultSnapshot) || preview != null && !preview.IsDisposed
                && preview.PaintRevision == capturedPaintRevision;
        }
        static bool FiniteFinishTime(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        /// <summary>
        /// Host: wait (at most 1 s) for the gourd's poses after the final impact, then fix the record in the identity.
        /// The replay is used only when it ends well inside the shared finish budget; otherwise the fixed effect plays.
        /// </summary>
        void DecideFinishReplay(double realNow)
        {
            if (replayWaitLimit < 0d) replayWaitLimit = realNow + 1d;
            bool force = realNow >= replayWaitLimit;
            if (!network.TryCaptureFinishReplay(force, out string encoded, out bool waiting) && waiting && !force) return;
            bool fits = !string.IsNullOrEmpty(encoded) && network.TryGetGameServerTime(out double serverNow)
                && finishServerDeadline > 0d && serverNow + GamePaintFinishBarrier.LeadSeconds + LabFinishReplay.Duration
                    + ReplayBudgetMargin <= finishServerDeadline;
            finishIdentity.replay = fits ? encoded : string.Empty;
            replayDecided = true;
            // Whether the replay plays, and why not, so a playtest log tells the cause. A later late_schedule line replaces it.
            Debug.Log(fits ? "C6_FINISH_REPLAY replay"
                : "C6_FINISH_REPLAY fallback reason=" + (string.IsNullOrEmpty(encoded) ? network.FinishReplayMissReason ?? "no_record" : "budget"));
            AdoptFinishReplay(finishIdentity.replay);
            nextFinishSendAt = 0d;
        }

        void AdoptFinishReplay(string encoded)
        {
            replayShot = LabFinishReplayShot.TryDecode(encoded, out LabFinishReplayShot shot) ? shot : null;
        }
    }
}
