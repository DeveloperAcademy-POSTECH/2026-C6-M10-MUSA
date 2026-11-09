using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using C6Lab;
using UnityEngine;

namespace Hapio.Experiments
{
    /// <summary>
    /// Recolors the sealed comparison journal using the original ordered compositor.
    /// No gameplay surface, RNG, network, saved game setting, or imported map is changed.
    /// </summary>
    public sealed class ClayPreviewPaletteReplay : IDisposable
    {
        const int PaletteSize = 5, PresetCount = 4, OutputColumns = 1024;
        const int MaximumPages = 16384;
        const int MaximumBinaryBytes = (int)LabPaintArtJournal.DefaultMaximumBytes + MaximumPages * 4 + 4;
        readonly Color32[] sourceSrgb, sourceLinear;
        readonly Dictionary<uint, int> sourceLookup = new Dictionary<uint, int>();
        readonly ClayPreviewPalettePreset[] presets;
        readonly LabWatercolorStampSettings settings;
        readonly LabPaintArtJournal journal;
        readonly string initializationError;
        LabPaintArtJournal firstHitJournal, repeatJournal, pendingJournal, publishedJournal;
        Texture2D[] pendingBrushes;
        ClayPreviewShapeDiagnostics diagnostics;
        ClayPreviewShapeScenario pendingScenario, publishedScenario;
        bool pendingOmitUndercoat, publishedOmitUndercoat;
        LabClayStickerRuntime pending, published;
        int pendingPreset = -1, publishedPreset = -1;
        ulong requestedGeneration, publishedGeneration;
        long ticks;
        bool disposed, acceptedPublishedFallback;

        /// <summary>Only true after the current request's sealed image reaches its GPU completion fence.</summary>
        public bool Ready => !disposed && Error == null && pending == null && published != null
            && published.Ready && (publishedGeneration == requestedGeneration || acceptedPublishedFallback);
        public bool Busy => !disposed && Error == null && pending != null;
        public bool HasPublished => !disposed && published != null && published.Ready;
        public ulong PublishedGeneration => publishedGeneration;
        public ClayPreviewShapeScenario CompletedScenario => publishedScenario;
        public bool CompletedWithoutUndercoat => publishedOmitUndercoat;
        public ClayPreviewShapeDiagnostics Diagnostics => diagnostics;
        public string Error { get; private set; }
        // While pending, retain the previous completed image instead of exposing a partial replay.
        public Texture PaintTexture => disposed ? null : published?.PaintTexture;
        public Texture HeightTexture => disposed ? null : published?.HeightTexture;
        public ulong RenderedThrough => disposed ? 0 : published?.RenderedThrough ?? 0;
        public int CompletedPreset => disposed ? -1 : publishedPreset;

        public ClayPreviewPaletteReplay(ClayPreviewPaletteData data)
        {
            LabPaintArtJournal imported = null;
            try
            {
                Require(data != null && data.format == ClayPreviewPaletteData.Format, "Unsupported frozen palette data.");
                Require(data.journal != null && !string.IsNullOrEmpty(data.epoch), "Missing frozen journal or epoch.");
                Require(data.pageCount > 0 && data.pageCount <= MaximumPages && data.lastSequence > 0,
                    "Invalid frozen page count or final sequence.");
                Require(data.columns > 0 && data.rows > 0 && (long)data.columns * data.rows <= 32768,
                    "Invalid frozen journal dimensions.");
                Require(HashText(data.recordHash) && HashText(data.journalSha256), "Invalid frozen journal fingerprints.");
                Require(!string.IsNullOrEmpty(data.settingsJson), "Missing frozen source presentation settings.");
                settings = JsonUtility.FromJson<LabWatercolorStampSettings>(data.settingsJson);
                Require(settings != null && settings.enabled && settings.UsesClayStickers,
                    "The frozen settings are not enabled clay-sticker settings.");
                settings.Validate();
                Require(settings.Version == data.styleVersion, "Frozen presentation version differs from the current source settings.");

                Require(data.sourceSrgb != null && data.sourceSrgb.Length == PaletteSize,
                    "Exactly five source sRGB colors are required.");
                sourceSrgb = (Color32[])data.sourceSrgb.Clone(); sourceLinear = new Color32[PaletteSize];
                for (int i = 0; i < PaletteSize; i++)
                {
                    Require(sourceSrgb[i].a == 255, "Source palette colors must be opaque.");
                    sourceLinear[i] = (Color32)((Color)sourceSrgb[i]).linear;
                    Require(!sourceLookup.ContainsKey(Key(sourceLinear[i])), "Source palette colors collide in linear-byte storage.");
                    sourceLookup.Add(Key(sourceLinear[i]), i);
                }
                Require(data.presets != null && data.presets.Length == PresetCount, "Exactly four frozen presets are required.");
                presets = new ClayPreviewPalettePreset[PresetCount];
                for (int i = 0; i < PresetCount; i++) presets[i] = CopyPreset(data.presets[i]);

                byte[] bytes = data.journal.bytes;
                Require(bytes.Length >= 8 && bytes.Length <= MaximumBinaryBytes, "Frozen journal binary is empty or exceeds its bound.");
                Require(string.Equals(Sha256(bytes), data.journalSha256, StringComparison.OrdinalIgnoreCase),
                    "Frozen journal binary checksum differs.");
                string path = Path.Combine(Application.temporaryCachePath, "hapio-palette-" + Guid.NewGuid().ToString("N") + ".journal");
                imported = new LabPaintArtJournal(path, data.epoch, data.columns, data.rows, data.styleVersion, receiver: true);
                Require(imported.Error == null, imported.Error ?? "Could not create the private replay journal.");
                using (var reader = new BinaryReader(new MemoryStream(bytes, false)))
                {
                    Require(reader.ReadInt32() == data.pageCount, "Frozen binary page count differs from its manifest.");
                    long total = 0;
                    for (int i = 0; i < data.pageCount; i++)
                    {
                        Require(reader.BaseStream.Length - reader.BaseStream.Position >= 4, "Truncated frozen page header.");
                        int length = reader.ReadInt32();
                        Require(length > 96 && length <= LabPaintArtJournal.MaximumPageBytes
                            && length <= reader.BaseStream.Length - reader.BaseStream.Position, "Invalid or truncated frozen page.");
                        total += length;
                        Require(total <= LabPaintArtJournal.DefaultMaximumBytes, "Frozen journal exceeds its raw storage limit.");
                        Require(imported.TryImportPage(i, reader.ReadBytes(length)), imported.Error ?? "Frozen page chain is invalid.");
                    }
                    Require(reader.BaseStream.Position == reader.BaseStream.Length, "Unexpected trailing frozen journal bytes.");
                }
                Require(imported.IsSealed && imported.PageCount == data.pageCount && imported.LastSequence == data.lastSequence
                    && string.Equals(imported.PrefixHash, data.recordHash, StringComparison.OrdinalIgnoreCase),
                    "Frozen journal seal, sequence or record fingerprint differs.");
                ValidateSourceColors(imported);
                journal = imported; imported = null;
            }
            catch (Exception exception)
            {
                initializationError = "Palette replay unavailable: " + exception.Message;
                Error = initializationError;
            }
            finally { imported?.Dispose(); }
        }

        /// <summary>Imports and validates the private journal without allocating any renderer or GPU map.</summary>
        public static bool TryValidate(ClayPreviewPaletteData data, out string error)
        {
            using (var replay = new ClayPreviewPaletteReplay(data))
            { error = replay.Error; return error == null; }
        }

        public void Request(int preset, Color32[] srgb, Texture2D[] brushOverride = null,
            ClayPreviewShapeScenario scenario = ClayPreviewShapeScenario.Full67, bool omitUndercoat = false,
            Shader diagnosticCompositor = null)
        {
            if (disposed) { Error = "Palette replay has been disposed."; return; }
            DisposeRuntime(ref pending); pendingPreset = -1;
            requestedGeneration++; acceptedPublishedFallback = false;
            Error = initializationError;
            if (Error != null) return;
            try
            {
                Require(preset >= 0 && preset < PresetCount, "Unknown comparison preset.");
                Require(srgb != null && srgb.Length == PaletteSize, "Exactly five replacement sRGB colors are required.");
                var replacement = new Color32[PaletteSize];
                for (int i = 0; i < PaletteSize; i++)
                {
                    Color32 value = srgb[i]; value.a = 255;
                    // Returning the original bytes explicitly makes the default palette bit-exact.
                    replacement[i] = SameRgb(value, sourceSrgb[i]) ? sourceLinear[i] : (Color32)((Color)value).linear;
                }
                Func<Color32, Color32> resolver = original =>
                {
                    if (!sourceLookup.TryGetValue(Key(original), out int index))
                        throw new InvalidDataException("A frozen stroke has no exact source palette color.");
                    return replacement[index];
                };
                Require(Enum.IsDefined(typeof(ClayPreviewShapeScenario), scenario), "Unknown shape comparison scenario.");
                Require(!omitUndercoat || preset == 3 && diagnosticCompositor != null && diagnosticCompositor.isSupported,
                    "The comparison-only undercoat shader is not connected.");
                ClayPreviewPalettePreset selected = presets[preset];
                if (brushOverride != null)
                    Require(preset == 3 && brushOverride.Length == 3 && Array.TrueForAll(brushOverride, b => b != null && b.width == 512 && b.height == 512), "Splash brushes require three 512px coating masks.");
                pending = new LabClayStickerRuntime(brushOverride ?? selected.brushes,
                    new LabClayStickerSettings { minimumDensity = selected.minimumDensity, maximumDensity = selected.maximumDensity },
                    omitUndercoat ? diagnosticCompositor : selected.compositor, resolver);
                pendingJournal = ScenarioJournal(scenario);
                pendingScenario = scenario; pendingOmitUndercoat = omitUndercoat;
                pendingBrushes = brushOverride;
                pendingPreset = preset; ticks = 0;
            }
            catch (Exception exception) { Error = "Palette replay unavailable: " + exception.Message; DisposeRuntime(ref pending); }
        }

        /// <summary>One bounded normal-runtime replay budget; never blocks on a GPU readback.</summary>
        public void Tick()
        {
            if (disposed || Error != null) return;
            LabClayStickerRuntime current = pending ?? published;
            if (current == null || pending == null && current.Ready) return;
            RenderTexture previous = RenderTexture.active;
            try
            {
                current.Update(pending != null ? pendingJournal : publishedJournal, null, null, OutputColumns, settings, ticks++ * .016, false);
                Require(current.Error == null, current.Error ?? "The ordered compositor failed.");
                if (pending != null && pending.Ready)
                {
                    Require(pending.AppliedThrough == pendingJournal.LastSequence && pending.RenderedThrough == pendingJournal.LastSequence,
                        "A partial image reached publication before the frozen final sequence.");
                    ClayPreviewShapeDiagnostics nextDiagnostics = ClayPreviewShapeDiagnostics.Create(pendingJournal,
                        pendingBrushes != null ? pendingBrushes[0] : null);
                    LabClayStickerRuntime old = published;
                    published = pending; publishedJournal = pendingJournal; pending = null;
                    publishedPreset = pendingPreset; pendingPreset = -1;
                    publishedGeneration = requestedGeneration;
                    publishedScenario = pendingScenario; publishedOmitUndercoat = pendingOmitUndercoat;
                    diagnostics?.Dispose(); diagnostics = nextDiagnostics;
                    if (old != null && (previous == old.PaintTexture || previous == old.HeightTexture)) previous = null;
                    DisposeRuntime(ref old);
                }
            }
            catch (Exception exception)
            {
                Error = "Palette replay unavailable: " + exception.Message;
                DisposeRuntime(ref pending); pendingPreset = -1;
            }
            finally { RenderTexture.active = previous; }
        }

        /// <summary>Cancel only an unpublished replacement; a previously completed picture stays valid.</summary>
        public void CancelPending()
        {
            if (disposed) return;
            DisposeRuntime(ref pending); pendingPreset = -1; pendingJournal = null; pendingBrushes = null;
            requestedGeneration++; acceptedPublishedFallback = false; Error = initializationError;
        }

        /// <summary>Explicitly accept the last completed picture after a replacement failed.</summary>
        public bool RestorePublished()
        {
            CancelPending();
            acceptedPublishedFallback = !disposed && Error == null && published != null;
            return acceptedPublishedFallback;
        }

        /// <summary>Drop experimental results when returning to the original frozen maps.</summary>
        public void Clear()
        {
            if (disposed) return;
            DisposeRuntime(ref pending); DisposeRuntime(ref published);
            diagnostics?.Dispose(); diagnostics = null; pendingJournal = publishedJournal = null; pendingBrushes = null;
            pendingScenario = publishedScenario = ClayPreviewShapeScenario.Full67;
            pendingOmitUndercoat = publishedOmitUndercoat = false;
            pendingPreset = publishedPreset = -1; requestedGeneration++; publishedGeneration = 0; ticks = 0; acceptedPublishedFallback = false;
            Error = initializationError;
        }

        public void Dispose()
        {
            if (disposed) return;
            Clear(); disposed = true; journal?.Dispose(); firstHitJournal?.Dispose(); repeatJournal?.Dispose();
        }

        LabPaintArtJournal ScenarioJournal(ClayPreviewShapeScenario scenario)
        {
            if (scenario == ClayPreviewShapeScenario.Full67) return journal;
            if (scenario == ClayPreviewShapeScenario.FirstHit)
                return firstHitJournal ??= ClayPreviewShapeDiagnostics.CreateScenarioJournal(scenario, settings.Version, sourceLinear[0]);
            return repeatJournal ??= ClayPreviewShapeDiagnostics.CreateScenarioJournal(scenario, settings.Version, sourceLinear[0]);
        }

        ClayPreviewPalettePreset CopyPreset(ClayPreviewPalettePreset source)
        {
            Require(source != null && source.brushes != null && source.brushes.Length == 3,
                "A frozen preset must provide its three original presentation brushes.");
            foreach (Texture2D brush in source.brushes)
                Require(brush != null && brush.width == 512 && brush.height == 512, "A frozen presentation brush is missing or has the wrong dimensions.");
            Require(source.compositor != null && source.compositor.isSupported, "A frozen compositor is missing or unsupported.");
            var pigment = new LabClayStickerSettings { minimumDensity = source.minimumDensity, maximumDensity = source.maximumDensity };
            pigment.Validate();
            Require(HashText(source.sampleMetadataHash) && HashText(source.colorRawHash) && HashText(source.heightRawHash),
                "Frozen preset fingerprints are missing.");
            return new ClayPreviewPalettePreset { brushes = (Texture2D[])source.brushes.Clone(), compositor = source.compositor,
                minimumDensity = source.minimumDensity, maximumDensity = source.maximumDensity,
                sampleMetadataHash = source.sampleMetadataHash, colorRawHash = source.colorRawHash, heightRawHash = source.heightRawHash };
        }

        void ValidateSourceColors(LabPaintArtJournal source)
        {
            var operations = new List<LabPaintArtOperation>(256); ulong after = 0;
            while (after < source.LastSequence)
            {
                Require(source.ReadOperations(after, 256, operations) && operations.Count > 0,
                    source.Error ?? "Could not read a bounded source journal batch.");
                foreach (LabPaintArtOperation operation in operations)
                    if (operation.Kind == LabPaintArtOperationKind.StrokeBegin)
                        Require(sourceLookup.ContainsKey(Key(operation.Stamp.color)), "A frozen stroke has an unknown source palette color.");
                after = operations[operations.Count - 1].Sequence;
            }
        }

        static void DisposeRuntime(ref LabClayStickerRuntime runtime)
        {
            if (runtime == null) return;
            if (RenderTexture.active == runtime.PaintTexture || RenderTexture.active == runtime.HeightTexture)
                RenderTexture.active = null;
            runtime.Dispose(); runtime = null;
        }
        static bool SameRgb(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;
        static uint Key(Color32 value) => ((uint)value.r << 24) | ((uint)value.g << 16) | ((uint)value.b << 8) | value.a;
        static bool HashText(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')) return false;
            return true;
        }
        static string Sha256(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant(); }
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidDataException(message); }
    }
}
