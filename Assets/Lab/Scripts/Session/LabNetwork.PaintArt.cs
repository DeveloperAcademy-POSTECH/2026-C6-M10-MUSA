using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Unity.Netcode;
using UnityEngine;

namespace C6Lab
{
    public sealed partial class LabNetwork
    {
        LabPaintArtJournal paintArtJournal;
        LabWatercolorStampSettings pinnedWatercolorSettings;
        readonly Dictionary<ulong, LabPaintArtSendCursor> artPeers = new Dictionary<ulong, LabPaintArtSendCursor>();
        readonly HashSet<ulong> artFailedPeers = new HashSet<ulong>();
        string paintArtPath, paintArtError, paintArtRenderError, reportedArtError, paintArtEpoch;
        ulong artCommittedThrough, artRenderedThrough, artAppliedThrough;
        uint artCommittedRevision;
        uint artHashRevision;
        string artSurfaceHash;
        Color32[] artHashPixels;
        byte[] artHashOwners, artHashChunk;
        SHA256 artSha;
        double nextArtReceiptAt, nextArtErrorAt;
        bool paintArtVersionMismatch;

        public LabPaintArtJournal PaintArtJournal => paintArtJournal;
        public LabWatercolorStampSettings PinnedWatercolorSettings => pinnedWatercolorSettings;
        /// <summary>Recording/transfer failure, independent of the disposable live renderer.</summary>
        public string PaintArtRecordingError => paintArtError ?? paintArtJournal?.Error;
        public string PaintArtError => paintArtError ?? paintArtJournal?.Error ?? paintArtRenderError;
        public bool PaintArtInitialReady => !RequiresPaintArt || latest != null
            && paintArtEpoch == latest.sessionId + "/" + latest.round && !paintArtVersionMismatch
            && (PaintArtError != null || paintArtJournal != null && paintArtJournal.PageCount > 0
                && paintArtJournal.StyleVersion == pinnedWatercolorSettings?.Version);
        public bool RequiresPaintArt => latest != null && !latest.teamTerritory
            && (config == null || config.HighResolutionPaintEnabled)
            && (pinnedWatercolorSettings ?? LabWatercolorStampSettings.CaptureDefault()).enabled;

        void BeginPaintArt()
        {
            ResetPaintArt();
            if (model == null || model.PaintSurface == null) return;
            // Territory retains its categorical red/blue rendering and does not allocate a journal.
            if (config != null && config.TeamTerritoryEnabled) return;
            paintArtEpoch = sessionId + "/" + model.Round;
            pinnedWatercolorSettings = LabWatercolorStampSettings.CaptureDefault();
            try
            {
                LabPaintSurface surface = model.PaintSurface;
                if (pinnedWatercolorSettings.enabled && (pinnedWatercolorSettings.UsesPaperStickers || pinnedWatercolorSettings.UsesClayStickers)
                    && (config == null || config.HighResolutionPaintEnabled))
                    surface.SetPaperStickerContour();
                paintArtPath = NewPaintArtPath();
                paintArtJournal = new LabPaintArtJournal(paintArtPath, sessionId + "/" + model.Round,
                    surface.Columns, surface.Rows, pinnedWatercolorSettings.Version);
                if (!surface.AttachArtJournal(paintArtJournal))
                    paintArtJournal.Fail("Artwork could not attach to the empty round surface.");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException)
            { paintArtError = "Artwork recording could not start (" + ex.GetType().Name + ")."; }
        }

        static string NewPaintArtPath()
        {
            string directory = Path.Combine(Application.temporaryCachePath, "PaintArt");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, Guid.NewGuid().ToString("N") + ".art");
        }

        void CommitPaintArt(LabSnapshot snapshot)
        {
            if (paintArtJournal == null || model == null || snapshot == null || paintArtJournal.IsSealed
                || paintArtJournal.Error != null) return;
            LabPaintSurface surface = model.PaintSurface;
            bool terminal = snapshot.phase == LabPhase.Victory || snapshot.phase == LabPhase.Defeat;
            if (artCommittedThrough != paintArtJournal.LastSequence || artCommittedRevision != surface.Revision || terminal)
            {
                string hash = HashArtSurface(surface);
                if (terminal) paintArtJournal.Seal(surface.Revision, hash);
                else paintArtJournal.Commit(surface.Revision, hash);
                artCommittedThrough = paintArtJournal.LastSequence;
                artCommittedRevision = surface.Revision;
                paintArtJournal.Flush();
            }
        }

        string HashArtSurface(LabPaintSurface surface)
        {
            if (artHashPixels == null || artHashPixels.Length != surface.CellCount)
            { artHashPixels = new Color32[surface.CellCount]; artHashOwners = new byte[surface.CellCount]; }
            artSha ??= SHA256.Create(); artHashChunk ??= new byte[4096];
            surface.CopyPixels(artHashPixels); surface.CopyPainterSlots(artHashOwners);
            return LabPaintPublisher.Hash(artHashPixels, artHashOwners, artSha, artHashChunk);
        }

        public void ReportPaintArtRenderingError(string error)
        {
            paintArtRenderError = string.IsNullOrEmpty(error) ? null : "Artwork rendering failed: " + error;
        }

        /// <summary>Map installed, all journal operations applied, and GPU work finished are distinct fences.</summary>
        public bool PaintArtCompletionReady
        {
            get
            {
                if (!RequiresPaintArt) return true;
                var journal = paintArtJournal;
                if (PaintArtError != null || journal == null || !journal.IsSealed || journal.Error != null
                    || latest == null || journal.Epoch != latest.sessionId + "/" + latest.round
                    || journal.SealRevision != clientPaintSurface.Revision || artRenderedThrough != journal.LastSequence
                    || artAppliedThrough != journal.LastSequence) return false;
                if (artSurfaceHash == null || artHashRevision != clientPaintSurface.Revision)
                {
                    artHashRevision = clientPaintSurface.Revision;
                    artSurfaceHash = HashArtSurface(clientPaintSurface);
                }
                return artSurfaceHash == journal.SealSurfaceHash;
            }
        }

        public void ReportPaintArtRendered(ulong applied, ulong rendered)
        {
            if (paintArtJournal == null || applied > paintArtJournal.LastSequence || rendered > applied) return;
            // GPU recreation legitimately rewinds the rendered cursor until the disk journal has replayed.
            artAppliedThrough = applied; artRenderedThrough = rendered;
        }

        void TickPaintArt(double now)
        {
            string error = PaintArtError;
            if (!string.IsNullOrEmpty(error) && reportedArtError != error)
            {
                reportedArtError = error;
                SetNotice("Artwork recording is unavailable. Gameplay continues; an exact completed artwork cannot be shown.");
                Debug.LogWarning("C6_PAINT_ART_UNAVAILABLE " + error);

            }
            if (!IsConnected || latest == null || latest.teamTerritory) return;
            if (IsHost && (paintArtError != null || paintArtJournal?.Error != null) && now >= nextArtErrorAt)
            {
                nextArtErrorAt = now + 1d;
                foreach (ulong peer in order) if (peer != LocalPlayerId)
                    Send(peer, new LabNetworkPacket { type = "paint_art_error", sessionId = latest.sessionId,
                        round = latest.round, payload = pinnedWatercolorSettings?.Version });
            }
            if (!IsHost)
            {
                if (now >= nextArtReceiptAt) { nextArtReceiptAt = now + .5d; SendPaintArtReceipt(); }
                return;
            }
            if (paintArtJournal == null || paintArtJournal.Error != null) return;
            foreach (ulong peer in order)
            {
                if (peer == LocalPlayerId || artFailedPeers.Contains(peer)) continue;
                if (!artPeers.TryGetValue(peer, out var cursor)) artPeers.Add(peer, cursor = new LabPaintArtSendCursor());
                int backlog = bulkSenders.TryGetValue(peer, out var sender) ? sender.QueuedBytes : 0;
                // Only one page enters Bulk, after this frame's game state/control has been queued.
                bool retry = cursor.PendingPage >= 0;
                if (retry ? !cursor.CanRetry(sender?.Acknowledged ?? 0, now, backlog)
                    : !cursor.CanQueue(paintArtJournal.PageCount, backlog)) continue;
                int index = retry ? cursor.PendingPage : cursor.ReceivedPages;
                if (!paintArtJournal.TryReadPage(index, out byte[] raw)) continue;
                LabPaintArtPage page = LabPaintArtTransfer.Encode(paintArtJournal.Epoch, paintArtJournal.StyleVersion,
                    paintArtJournal.Columns, paintArtJournal.Rows, index, raw);
                if (QueueBulk(peer, new LabNetworkPacket { type = "paint_art", sessionId = latest.sessionId,
                    round = latest.round, configRevision = configRevision, configFingerprint = configFingerprint,
                    payload = LabPacketPayload.Encode(page) }))
                {
                    if (!retry) cursor.Queued(index);
                    cursor.MarkTransmission(bulkSenders[peer].LastEnqueued, now);
                }
            }
        }

        void ReceivePaintArtPage(LabNetworkPacket packet)
        {
            if (IsHost || latest == null || latest.teamTerritory || !clientConfigReady
                || packet.configRevision != configRevision || packet.configFingerprint != configFingerprint
                || packet.sessionId != latest.sessionId || packet.round != latest.round) return;
            LabPaintArtPage page = LabPacketPayload.Decode<LabPaintArtPage>(packet.payload);
            if (page == null || page.epoch != latest.sessionId + "/" + latest.round) return;
            if (paintArtEpoch != page.epoch)
            { ResetPaintArt(); paintArtEpoch = page.epoch; }
            if (pinnedWatercolorSettings == null) pinnedWatercolorSettings = LabWatercolorStampSettings.CaptureDefault();
            if (page.style != pinnedWatercolorSettings.Version)
            { paintArtVersionMismatch = true; paintArtError = "The watercolor brush/profile version does not match the Host."; return; }
            if (!LabPaintArtTransfer.TryDecode(page, out byte[] raw))
            { paintArtError = "Artwork page data failed validation."; return; }
            try
            {
                if (paintArtJournal == null)
                {
                    if (page.index != 0) { SendPaintArtReceipt(); return; }
                    paintArtPath = NewPaintArtPath();
                    paintArtJournal = new LabPaintArtJournal(paintArtPath, page.epoch, page.columns, page.rows,
                        page.style, receiver: true);
                }
                if (paintArtJournal.Columns != page.columns || paintArtJournal.Rows != page.rows
                    || !paintArtJournal.TryImportPage(page.index, raw))
                { paintArtError = paintArtJournal.Error ?? "Artwork page order or contents are invalid."; return; }
                // Do not send a rendered-ready acknowledgement here: the GPU has not consumed these operations.
                SendPaintArtReceipt();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            { paintArtError = "Artwork page could not be stored (" + ex.GetType().Name + ")."; }
        }

        void ReceivePaintArtError(LabNetworkPacket packet)
        {
            if (latest == null || packet.sessionId != latest.sessionId || packet.round != latest.round) return;
            string epoch = latest.sessionId + "/" + latest.round;
            if (paintArtEpoch != epoch) { ResetPaintArt(); paintArtEpoch = epoch; }
            pinnedWatercolorSettings ??= LabWatercolorStampSettings.CaptureDefault();
            paintArtVersionMismatch = packet.payload != pinnedWatercolorSettings.Version;
            paintArtError = paintArtVersionMismatch ? "The watercolor brush/profile version does not match the Host."
                : "The Host could not preserve this round artwork.";
        }

        void SendPaintArtReceipt()
        {
            if (IsHost || latest == null || !IsConnected || latest.teamTerritory) return;
            Send(NetworkManager.ServerClientId, new LabNetworkPacket { type = "paint_art_ack", sessionId = latest.sessionId,
                round = latest.round, configRevision = configRevision, configFingerprint = configFingerprint,
                payload = LabPacketPayload.Encode(new LabPaintArtReceipt { epoch = latest.sessionId + "/" + latest.round,
                    style = pinnedWatercolorSettings?.Version, receivedPages = paintArtJournal?.PageCount ?? 0,
                    appliedThrough = artAppliedThrough, renderedThrough = artRenderedThrough, error = paintArtError ?? paintArtJournal?.Error }) });
        }

        void ReceivePaintArtReceipt(ulong sender, LabNetworkPacket packet)
        {
            if (paintArtJournal == null || latest == null || packet.sessionId != latest.sessionId
                || packet.round != latest.round || packet.configRevision != configRevision
                || packet.configFingerprint != configFingerprint || !artPeers.TryGetValue(sender, out var cursor)) return;
            LabPaintArtReceipt receipt = LabPacketPayload.Decode<LabPaintArtReceipt>(packet.payload);
            if (receipt == null || receipt.epoch != paintArtJournal.Epoch) return;
            if (!string.IsNullOrEmpty(receipt.error))
            { artFailedPeers.Add(sender); return; }
            if (receipt.style != paintArtJournal.StyleVersion) return;
            cursor.Receive(receipt, paintArtJournal.PageCount, paintArtJournal.LastSequence);
        }

        void ResetPaintArt()
        {
            if (model?.PaintSurface != null) model.PaintSurface.AttachArtJournal(null);
            paintArtJournal?.Dispose(); paintArtJournal = null;
            if (paintArtPath != null)
            {
                try { if (File.Exists(paintArtPath)) File.Delete(paintArtPath); }
                catch (IOException) { Debug.LogWarning("C6_PAINT_ART_TEMP_CLEANUP_FAILED"); }
                catch (UnauthorizedAccessException) { Debug.LogWarning("C6_PAINT_ART_TEMP_CLEANUP_FAILED"); }
            }
            paintArtPath = paintArtEpoch = null; pinnedWatercolorSettings = null; paintArtError = paintArtRenderError = reportedArtError = null;
            artSha?.Dispose(); artSha = null; artHashPixels = null; artHashOwners = artHashChunk = null;
            artPeers.Clear(); artFailedPeers.Clear(); artCommittedThrough = artRenderedThrough = artAppliedThrough = 0;
            artCommittedRevision = artHashRevision = 0; artSurfaceHash = null; nextArtReceiptAt = nextArtErrorAt = 0; paintArtVersionMismatch = false;
        }
    }
}
