using System;
using System.Collections.Generic;
using UnityEngine;

namespace C6Lab
{
    /// <summary>
    /// Round-owned, render-only work which survives Battle unload. It borrows the round's
    /// disk journal but owns its display hierarchy, CPU maps and bounded GPU replay. Nothing
    /// here reports rendering progress to the live simulation or changes the accepted map.
    /// </summary>
    public sealed class LabGourdPreviewPreparation : IDisposable
    {
        private readonly LabNetwork network;
        private readonly LabSnapshot result;
        private readonly bool needsArtwork, highResolution;
        private readonly int outputColumns;
        private readonly LabWatercolorStampSettings style;
        private readonly LabWatercolorPaintSettings washStyle;
        private LabPaintArtJournal journal;
        private LabPaintSurface frozenSurface;
        private string surfaceHash;
        private LabGourdResultPreview preview;
        private LabHighResolutionPaintRuntime resolution;
        private LabWatercolorPaintRuntime wash;
        private LabClayStickerRuntime clay;
        private LabWatercolorStampRuntime stamps;
        private bool disposed;
        private readonly List<LabPaintArtOperation> journalProbe = new List<LabPaintArtOperation>(0);

        public bool Ready { get; private set; }
        public bool Failed => Error != null;
        public string Error { get; private set; }
        public ulong AppliedThrough => clay != null ? clay.AppliedThrough : stamps != null ? stamps.AppliedThrough : Ready ? LastSequence : 0;
        public ulong RenderedThrough => clay != null ? clay.RenderedThrough : stamps != null ? stamps.RenderedThrough : Ready ? LastSequence : 0;
        public ulong LastSequence => journal != null ? journal.LastSequence : 0;

        private LabGourdPreviewPreparation(LabNetwork network, LabGourdPaint source)
        {
            this.network = network;
            result = JsonUtility.FromJson<LabSnapshot>(JsonUtility.ToJson(network.Snapshot));
            needsArtwork = network.RequiresPaintArt;
            highResolution = network.config.HighResolutionPaintEnabled;
            outputColumns = network.config.HighResolutionPaintColumns;
            style = (network.PinnedWatercolorSettings ?? LabWatercolorStampSettings.CaptureDefault()).Clone();
            washStyle = source.watercolorProfile != null ? source.watercolorProfile.settings?.SanitizedCopy() : null;
            journal = network.PaintArtJournal;
        }

        /// <summary>
        /// Capture the immutable display template before its Scene is released. A missing
        /// final journal page is pending work, not a failed capture. The old fast path still
        /// produces the exact same owned preview when the live presentation is already ready.
        /// </summary>
        public static bool TryCreate(LabNetwork network, out LabGourdPreviewPreparation preparation,
            out string error, int width = 512, int height = 512)
        {
            preparation = null;
            if (network == null || network.config == null || !network.config.CompletedGourdViewerEnabled
                || network.config.GourdMode != LabGourdMode.Paint || network.Snapshot == null
                || network.Snapshot.phase != LabPhase.Victory)
            { error = "A completed Paint result with viewing enabled is required."; return false; }
            LabGourdPaint source = network.target != null ? network.target.GetComponent<LabGourdPaint>() : null;
            if (source == null || !source.IsReady || source.VisualInstance == null)
            { error = "The completed gourd display template is not ready."; return false; }
            if (width < 1 || height < 1 || width > SystemInfo.maxTextureSize || height > SystemInfo.maxTextureSize)
            { error = "The requested preview texture dimensions are unsupported."; return false; }
            LabGourdPreviewPreparation candidate = null;
            try
            {
                candidate = new LabGourdPreviewPreparation(network, source);
                if (source.CompletionVisualReady && LabGourdResultPreview.SurfaceMatches(network, out _)
                    && LabGourdResultPreview.TryCreate(network, source, width, height, out var completed, out _))
                {
                    candidate.preview = completed;
                    candidate.Ready = true;
                }
                else candidate.preview = LabGourdResultPreview.CapturePendingModel(source, width, height);
                preparation = candidate;
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                candidate?.Dispose();
                error = "Could not preserve the gourd display: " + exception.Message;
                return false;
            }
        }

        public void Tick()
        {
            if (disposed || Ready || Failed) return;
            try
            {
                if (network == null) throw new InvalidOperationException("The artwork preparation owner is unavailable.");
                LabSnapshot current = network.Snapshot;
                if (current != null && (current.sessionId != result.sessionId || current.round != result.round
                    || current.phase != LabPhase.Victory || current.paintRevision != result.paintRevision))
                    throw new InvalidOperationException("The accepted result changed before its artwork was prepared.");
                if (current == null && network.IsConnected) return;
                if (frozenSurface == null)
                {
                    // The committed result was captured before disconnect. Its final CPU map
                    // can still be copied without asking a live connection for permission.
                    if (!MapMatchesResult(network.PaintSurface))
                    {
                        if (!network.IsConnected) throw new InvalidOperationException("The final map was not received before the connection ended.");
                        return;
                    }
                    FreezeMap(network.PaintSurface);
                }
                if (needsArtwork)
                {
                    if (network.PaintArtRecordingError != null) throw new InvalidOperationException(network.PaintArtRecordingError);
                    var available = network.PaintArtJournal;
                    if (available == null && journal == null)
                    {
                        if (!network.IsConnected) throw new InvalidOperationException("The artwork record was not received before the connection ended.");
                        return;
                    }
                    if (journal == null) journal = available;
                    if (available != null && !ReferenceEquals(journal, available)
                        || journal.Epoch != result.sessionId + "/" + result.round
                        || journal.Columns != frozenSurface.Columns || journal.Rows != frozenSurface.Rows
                        || journal.StyleVersion != style.Version)
                        throw new InvalidOperationException("The artwork journal no longer belongs to this result.");
                    // A disposed borrowed journal need not carry Error. A zero-read checks its
                    // lifetime without touching disk, changing progress, or taking ownership.
                    if (!journal.ReadOperations(journal.LastSequence, 0, journalProbe))
                        throw new InvalidOperationException(journal.Error ?? "The artwork record is no longer available.");
                }

                Texture colors = preview.PaintTexture, painters = preview.PainterTexture, heights = null;
                if (highResolution)
                {
                    resolution ??= new LabHighResolutionPaintRuntime();
                    resolution.Update(frozenSurface, preview.PaintTexture, preview.PainterTexture, outputColumns);
                    if (resolution.Error != null) throw new InvalidOperationException(resolution.Error);
                    if (!resolution.Ready || resolution.HasPendingWork) return;
                    colors = resolution.PaintTexture; painters = resolution.PainterTexture;
                }
                if (needsArtwork)
                {
                    double now = Time.realtimeSinceStartupAsDouble;
                    if (style.UsesClayStickers)
                    {
                        clay ??= new LabClayStickerRuntime();
                        clay.Update(journal, colors, painters, outputColumns, style, now);
                        if (clay.Error != null) throw new InvalidOperationException(clay.Error);
                        if (!clay.Ready || clay.AppliedThrough != journal.LastSequence || clay.RenderedThrough != journal.LastSequence) return;
                        colors = clay.PaintTexture; heights = clay.HeightTexture;
                    }
                    else
                    {
                        stamps ??= new LabWatercolorStampRuntime();
                        stamps.Update(journal, colors, outputColumns, style, now);
                        if (stamps.Error != null) throw new InvalidOperationException(stamps.Error);
                        if (!stamps.Ready || stamps.IsAnimating || stamps.AppliedThrough != journal.LastSequence
                            || stamps.RenderedThrough != journal.LastSequence) return;
                        colors = stamps.PaintTexture;
                    }
                    if (!journal.IsSealed) return;
                    if (journal.SealRevision != result.paintRevision || journal.SealSurfaceHash != surfaceHash)
                        throw new InvalidOperationException("The sealed artwork and accepted final map do not match.");
                }
                else if (!result.teamTerritory && highResolution && washStyle != null && washStyle.enabled)
                {
                    wash ??= new LabWatercolorPaintRuntime();
                    wash.Update(frozenSurface, colors, painters, washStyle, Time.realtimeSinceStartupAsDouble, true);
                    if (wash.Error != null) throw new InvalidOperationException(wash.Error);
                    if (!wash.Ready || wash.HasPendingWork || wash.IsAnimating) return;
                    colors = wash.PaintTexture;
                }
                preview.CompletePending(highResolution ? colors : null, painters, heights);
                Ready = true;
                ReleaseReplay();
            }
            catch (Exception exception) { Fail(exception.Message); }
        }

        private bool MapMatchesResult(LabPaintSurface surface) => surface != null
            && result.phase == LabPhase.Victory && !string.IsNullOrEmpty(result.sessionId) && result.round != 0
            && result.paintGoalPercent == 100 && result.paintCellCount > 0
            && result.paintCoveredCount == result.paintCellCount
            && surface.Revision == result.paintRevision && surface.Columns == result.paintColumns
            && surface.Rows == result.paintRows && surface.CellCount == result.paintCellCount
            && surface.CoveredCount == result.paintCoveredCount && surface.PaintCount == result.paintCount && surface.IsComplete;

        private void FreezeMap(LabPaintSurface surface)
        {
            var pixels = new Color32[surface.CellCount];
            var owners = new byte[surface.CellCount];
            surface.CopyPixels(pixels); surface.CopyPainterSlots(owners);
            var copy = new LabPaintSurface();
            if (!copy.TryReplacePixels(surface.Columns, surface.Rows, surface.Revision, pixels, owners,
                surface.PaintCount, surface.CopyVisualStamps()))
                throw new InvalidOperationException("The accepted final map could not be copied.");
            surfaceHash = LabPaintPublisher.Hash(pixels, owners);
            frozenSurface = copy;
            preview.InitializeCpu(result, copy);
        }

        public LabGourdResultPreview TakePreview()
        {
            if (disposed || !Ready || Failed) return null;
            LabGourdResultPreview completed = preview;
            preview = null;
            return completed;
        }

        private void Fail(string error)
        {
            Error = string.IsNullOrEmpty(error) ? "The completed artwork could not be prepared." : error;
            preview?.Dispose(); preview = null;
            ReleaseReplay();
            frozenSurface = null;
        }

        private void ReleaseReplay()
        {
            clay?.Dispose(); clay = null;
            stamps?.Dispose(); stamps = null;
            wash?.Dispose(); wash = null;
            resolution?.Dispose(); resolution = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            preview?.Dispose(); preview = null;
            ReleaseReplay();
            frozenSurface = null;
            // The Network owns the borrowed journal and removes it at the next round/Home.
            journal = null;
        }
    }
}
