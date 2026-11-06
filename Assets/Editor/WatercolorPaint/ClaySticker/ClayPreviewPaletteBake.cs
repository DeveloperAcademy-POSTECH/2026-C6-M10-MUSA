using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Hapio.Experiments;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Explicit, isolated palette preparation. Strict builds never generate or replay maps.</summary>
    public static class ClayPreviewPaletteBake
    {
        public const string Folder = ClayPreviewBuild.Root + "/Palette";
        public const string JournalPath = Folder + "/FrozenJournal.bytes";
        public const string DataPath = Folder + "/PaletteReplayData.asset";
        public const string SourcePinsPath = Folder + "/ReplaySourcePins.json";
        const string PinsFormat = "clay-preview-palette-source-pins-v1";
        const int PresetCount = 4, ReplayTickLimit = 4096;
        static readonly string[] Keys = { "OriginalV2", "OriginalV3", "RoundedV3", "CoatingV4" };
        static readonly string[] BrushNames = { "BroadData", "LongData", "ClusterData" };

        [Serializable] sealed class SourcePin { public string path, sha256; }
        [Serializable] sealed class SourcePins
        { public string format = PinsFormat, journalSha256, recordHash; public SourcePin[] sources; }
        [Serializable] public sealed class ReplayResult
        { public int preset, ticks; public string colorRawHash, heightRawHash; public bool exactColor, exactAllHeightChannels; public double elapsedMilliseconds; }
        [Serializable] public sealed class PreparationResult
        {
            public string format = ClayPreviewPaletteData.Format, journalSha256, recordHash, authorityHash;
            public int hits, pages, journalBytes;
            public ulong lastSequence;
            public bool sourceConfigPreserved, randomPreserved, frozenFilesPreserved, sceneAndBuildStatePreserved;
            public List<ReplayResult> replays = new List<ReplayResult>();
            public string scope = "Explicit stopped-Editor preparation. Four complete default-palette GPU replays are compared with frozen raw RGBA32 color and RGBAHalf height data. Timings include bounded GPU waits; they are not gameplay frame or GPU profiler timings. Existing PNG/EXR/JSON sources are not rebaked.";
        }
        sealed class Fingerprint { public string hash; public long writeTicks; }
        sealed class OwnedBackup
        {
            public string path; public bool existed; public byte[] bytes; public DateTime writeTime;
            public OwnedBackup(string path)
            { this.path = path; existed = File.Exists(path); if (existed) { bytes = File.ReadAllBytes(path); writeTime = File.GetLastWriteTimeUtc(path); } }
            public void Restore()
            { if (existed) { File.WriteAllBytes(path, bytes); File.SetLastWriteTimeUtc(path, writeTime); } else if (File.Exists(path)) File.Delete(path); }
        }

        [MenuItem("C6 Lab/Experiments/Rounded Clay/Prepare Palette Preview")]
        public static void PrepareMenu() => Debug.Log("CLAY_PALETTE_PREPARED " + PreparePalettePreview());

        public static string PreparePalettePreview()
        {
            ClayPreviewBuild.RequireStopped(); Preflight();
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Palette preparation requires a real graphics device.");
            // Existing palette data may be stale after an explicit source update. Validate all frozen
            // inputs strictly, but do not make rebuilding this owned dataset depend on its old pins.
            StickerCoatingExperiment.ValidatePreparedForPaletteBaking();
            var preserved = PreservedFiles();
            string sceneState = ClayPreviewBuild.SceneState(), buildState = ClayPreviewBuild.BuildScenesState();
            string randomState = JsonUtility.ToJson(UnityEngine.Random.state);
            var source = Resources.Load<LabConfig>("LabConfig"); Require(source != null, "The saved source LabConfig is missing.");
            string sourceJson = EditorJsonUtility.ToJson(source);
            var samples = LoadSamples(); var settings = RoundedClayExperiment.Settings();
            Require(ClayPreviewBuild.Hash(Encoding.UTF8.GetBytes(sourceJson)) == samples[0].sourceConfigHash
                && settings.Version == samples[0].sourceJournalStyle,
                "The preserved fixture source settings differ. Review the fixed artwork before preparing a palette replay.");
            var result = new PreparationResult { authorityHash = samples[0].authorityHash };
            ClayPreviewPaletteData pending = null, previousDataCopy = null;
            TextAsset sourceJournal = null;
            var existingData = AssetDatabase.LoadAssetAtPath<ClayPreviewPaletteData>(DataPath);
            if (existingData != null) previousDataCopy = Object.Instantiate(existingData);
            ClayPreviewPaletteData previousBinding = ReadBoundData();
            string[] ownedPaths = { JournalPath, SourcePinsPath, DataPath, StickerCoatingExperiment.ScenePath };
            var backups = ownedPaths.SelectMany(path => new[] { path, path + ".meta" }).Select(path => new OwnedBackup(path)).ToArray();
            bool wroteAssets = false, boundScene = false;
            try
            {
                using (var fixture = new LabClayVisualStudy.Fixture(source, settings, "standalone-frozen-five-color"))
                {
                    RoundedClayExperiment.Complete(fixture);
                    string authority = RoundedClayExperiment.SurfaceHash(fixture.Surface);
                    Require(fixture.Surface.PaintCount == 67 && fixture.Surface.IsComplete
                        && authority == samples[0].authorityHash && fixture.Journal.PrefixHash == samples[0].recordHash,
                        "The same seed123/67-hit authority or sealed journal no longer matches the frozen artwork.");
                    Color32[] sourceSrgb = SourcePalette(); ValidateStrokeColors(fixture.Journal, sourceSrgb);
                    byte[] binary = PackJournal(fixture.Journal);
                    pending = ScriptableObject.CreateInstance<ClayPreviewPaletteData>();
                    pending.epoch = fixture.Journal.Epoch; pending.styleVersion = fixture.Journal.StyleVersion;
                    pending.columns = fixture.Journal.Columns; pending.rows = fixture.Journal.Rows;
                    pending.pageCount = fixture.Journal.PageCount; pending.lastSequence = fixture.Journal.LastSequence;
                    pending.recordHash = fixture.Journal.PrefixHash; pending.journalSha256 = ClayPreviewBuild.Hash(binary);
                    pending.settingsJson = JsonUtility.ToJson(settings); pending.sourceSrgb = sourceSrgb;
                    pending.presets = Enumerable.Range(0, PresetCount).Select(index => MakePreset(index, samples[index])).ToArray();
                    result.hits = fixture.Surface.PaintCount; result.pages = pending.pageCount; result.journalBytes = binary.Length;
                    result.journalSha256 = pending.journalSha256; result.recordHash = pending.recordHash; result.lastSequence = pending.lastSequence;

                    // TextAsset(string) would corrupt arbitrary journal bytes. Import only the new
                    // owned .bytes asset, then prove all four replays before publishing data/Scene.
                    ClayPreviewBuild.EnsureFolder(Folder); wroteAssets = true;
                    File.WriteAllBytes(JournalPath, binary); AssetDatabase.ImportAsset(JournalPath, ImportAssetOptions.ForceSynchronousImport);
                    sourceJournal = AssetDatabase.LoadAssetAtPath<TextAsset>(JournalPath);
                    Require(sourceJournal != null && ClayPreviewBuild.Hash(sourceJournal.bytes) == pending.journalSha256,
                        "The binary journal import changed the original page bytes.");
                    pending.journal = sourceJournal;
                    Require(ClayPreviewPaletteReplay.TryValidate(pending, out string dataError), dataError);
                    ValidateData(pending, samples, false);
                    using (var replay = new ClayPreviewPaletteReplay(pending))
                    {
                        Require(replay.Error == null, replay.Error);
                        for (int preset = 0; preset < PresetCount; preset++) result.replays.Add(CheckDefaultReplay(replay, pending, preset));
                    }
                    Require(authority == RoundedClayExperiment.SurfaceHash(fixture.Surface)
                        && fixture.Journal.PrefixHash == result.recordHash, "Presentation replay changed authority or the source journal.");
                }
                result.sourceConfigPreserved = sourceJson == EditorJsonUtility.ToJson(source);
                result.randomPreserved = randomState == JsonUtility.ToJson(UnityEngine.Random.state);
                Require(result.sourceConfigPreserved && result.randomPreserved, "Preparing the isolated palette changed shared settings or RNG.");
                RequirePreserved(preserved);
                var pins = MakeSourcePins(pending);
                File.WriteAllText(SourcePinsPath, JsonUtility.ToJson(pins, true), new UTF8Encoding(false));
                AssetDatabase.ImportAsset(SourcePinsPath, ImportAssetOptions.ForceSynchronousImport);
                ClayPreviewPaletteData saved = existingData;
                if (saved == null) { saved = pending; AssetDatabase.CreateAsset(saved, DataPath); pending = null; }
                else { EditorUtility.CopySerialized(pending, saved); EditorUtility.SetDirty(saved); AssetDatabase.SaveAssetIfDirty(saved); }
                ValidateData(saved, samples, true);
                boundScene = true; BindData(saved);
                ValidateBoundData(); StickerCoatingExperiment.ValidatePrepared(); RequirePreserved(preserved);
                result.frozenFilesPreserved = true;
                result.sceneAndBuildStatePreserved = sceneState == ClayPreviewBuild.SceneState() && buildState == ClayPreviewBuild.BuildScenesState();
                Require(result.sceneAndBuildStatePreserved, "Palette preparation changed unrelated loaded Scene/build state.");
                return JsonUtility.ToJson(result, true);
            }
            catch
            {
                // Roll back only this tool's owned outputs. All loaded Scenes were clean on entry;
                // never save or discard an unrelated Scene or any original texture/metadata asset.
                if (boundScene) BindData(previousBinding, false);
                if (existingData != null && previousDataCopy != null)
                { EditorUtility.CopySerialized(previousDataCopy, existingData); EditorUtility.SetDirty(existingData); AssetDatabase.SaveAssetIfDirty(existingData); }
                if (wroteAssets)
                {
                    foreach (var backup in backups) backup.Restore();
                    foreach (string path in ownedPaths.Where(path => path != StickerCoatingExperiment.ScenePath))
                    { if (File.Exists(path)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); else AssetDatabase.DeleteAsset(path); }
                }
                throw;
            }
            finally
            {
                if (pending != null) Object.DestroyImmediate(pending);
                if (previousDataCopy != null) Object.DestroyImmediate(previousDataCopy);
            }
        }

        /// <summary>Optional read-only build guard. Does not execute the compositor or generate data.</summary>
        public static void ValidateBoundData()
        {
            ClayPreviewBuild.RequireStopped(); Preflight();
            ClayPreviewPaletteData data = ReadBoundData();
            if (data == null) return; // The preserved four-preset Scene is valid before palette preparation.
            Require(data == AssetDatabase.LoadAssetAtPath<ClayPreviewPaletteData>(DataPath), "The coating Scene references an unrecognized palette dataset.");
            ValidateData(data, LoadSamples(), true);
        }

        /// <summary>Explicit surface-only transaction. The journal/compositor/maps remain immutable.</summary>
        internal static LightingMetadataRebind BeginLightingMetadataRebind(string previousMetadataHash)
        {
            ClayPreviewBuild.RequireStopped(); Preflight();
            ClayPreviewPaletteData data = ReadBoundData();
            if (data == null) return null;
            Require(data == AssetDatabase.LoadAssetAtPath<ClayPreviewPaletteData>(DataPath), "The coating Scene references an unrecognized palette dataset.");
            Require(!EditorUtility.IsDirty(data), "Save/preserve unsaved palette data before refreshing lighting metadata.");
            ValidateData(data, LoadSamples(), true);
            Require(data.presets[3].sampleMetadataHash == previousMetadataHash, "The original V4 palette metadata pin differs; explicitly prepare the palette data first.");
            return new LightingMetadataRebind(data, previousMetadataHash);
        }

        internal sealed class LightingMetadataRebind : IDisposable
        {
            readonly ClayPreviewPaletteData data;
            readonly string originalSerialized, previousMetadataHash;
            readonly OwnedBackup[] backups;
            readonly Dictionary<string, Fingerprint> preserved;
            bool touched, committed, disposed;
            internal LightingMetadataRebind(ClayPreviewPaletteData data, string previousMetadataHash)
            {
                this.data = data; this.previousMetadataHash = previousMetadataHash;
                originalSerialized = EditorJsonUtility.ToJson(data);
                backups = new[] { new OwnedBackup(DataPath), new OwnedBackup(DataPath + ".meta") };
                preserved = PreservedFiles();
                // Only this JSON's surface hash and the current coating Scene pins may change.
                preserved.Remove(StickerCoatingExperiment.Folder + "/SampleInfo.json");
                foreach (string path in new[] { JournalPath, JournalPath + ".meta", SourcePinsPath, SourcePinsPath + ".meta", DataPath + ".meta" })
                {
                    Require(File.Exists(path), "A bound palette dependency is missing during the display-only refresh.");
                    preserved[path] = new Fingerprint { hash = ClayPreviewBuild.Hash(File.ReadAllBytes(path)), writeTicks = File.GetLastWriteTimeUtc(path).Ticks };
                }
            }
            internal void Apply(string nextMetadataHash)
            {
                Require(!disposed && !touched && !committed, "The palette lighting rebind is not available.");
                RequireUnchangedExceptPin(previousMetadataHash);
                TextAsset metadata = AssetDatabase.LoadAssetAtPath<TextAsset>(SamplePath(3));
                Require(metadata != null && ClayPreviewSampleInfo.ContentHash(metadata.text) == nextMetadataHash,
                    "The updated V4 JSON does not match the requested palette metadata pin.");
                touched = true;
                data.presets[3].sampleMetadataHash = nextMetadataHash;
                RequireUnchangedExceptPin(nextMetadataHash);
                // This includes raw color/height hashes, settings, source RGB, shape and all
                // brush/compositor source pins; the only changed dependency is V4's JSON hash.
                ValidateData(data, LoadSamples(), true); RequirePreserved(preserved);
                EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
                Require(!EditorUtility.IsDirty(data), "Could not save the explicit palette metadata rebind.");
                string originalAsset = Encoding.UTF8.GetString(backups[0].bytes);
                string updatedAsset = Encoding.UTF8.GetString(File.ReadAllBytes(DataPath));
                Require(updatedAsset.Split(new[] { nextMetadataHash }, StringSplitOptions.None).Length == 2
                    && updatedAsset.Replace(nextMetadataHash, previousMetadataHash) == originalAsset,
                    "The palette asset rewrite changed more than the fourth metadata fingerprint.");
            }
            internal void Complete()
            {
                Require(touched && !disposed && !committed, "No palette metadata update was applied.");
                RequireUnchangedExceptPin(data.presets[3].sampleMetadataHash);
                ValidateData(data, LoadSamples(), true); RequirePreserved(preserved);
                committed = true;
            }
            void RequireUnchangedExceptPin(string expectedPin)
            {
                Require(data != null && data.presets != null && data.presets.Length == PresetCount
                    && data.presets[3] != null && data.presets[3].sampleMetadataHash == expectedPin,
                    "The palette metadata pin changed outside the explicit lighting refresh.");
                string current = data.presets[3].sampleMetadataHash;
                string serialized;
                data.presets[3].sampleMetadataHash = previousMetadataHash;
                try { serialized = EditorJsonUtility.ToJson(data); }
                finally { data.presets[3].sampleMetadataHash = current; }
                Require(serialized == originalSerialized, "A surface-only refresh changed palette data beyond the fourth metadata pin.");
            }
            internal void Rollback()
            {
                if (!touched || committed || disposed) return;
                // Restore both the existing in-memory object and its exact asset bytes/timestamps.
                // Saving just this owned object first clears its dirty state without touching others.
                try
                {
                    EditorJsonUtility.FromJsonOverwrite(originalSerialized, data);
                    EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
                }
                finally
                {
                    foreach (var backup in backups) backup.Restore();
                    AssetDatabase.ImportAsset(DataPath, ImportAssetOptions.ForceSynchronousImport);
                    foreach (var backup in backups) if (backup.existed) File.SetLastWriteTimeUtc(backup.path, backup.writeTime);
                }
                Require(EditorJsonUtility.ToJson(data) == originalSerialized && backups.All(backup => backup.existed
                    && File.Exists(backup.path) && ClayPreviewBuild.Hash(File.ReadAllBytes(backup.path)) == ClayPreviewBuild.Hash(backup.bytes)
                    && File.GetLastWriteTimeUtc(backup.path) == backup.writeTime), "Could not restore the palette's original memory/asset metadata after a failed lighting refresh.");
                touched = false;
            }
            public void Dispose()
            {
                if (disposed) return;
                try { if (!committed) Rollback(); }
                finally { disposed = true; }
            }
        }

        static RoundedClayExperiment.Sample[] LoadSamples()
        {
            var result = new RoundedClayExperiment.Sample[PresetCount];
            for (int i = 0; i < PresetCount; i++)
            {
                TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(SamplePath(i));
                Require(text != null, "A frozen palette comparison sample is missing.");
                string metadataHash = ClayPreviewSampleInfo.ContentHash(text.text);
                Require(ClayPreviewSampleInfo.TryValidate(text.text, metadataHash, out _, out string error), error);
                var sample = JsonUtility.FromJson<RoundedClayExperiment.Sample>(text.text);
                Require(sample != null && sample.seed == 123 && sample.hits == 67 && sample.complete && sample.cells == sample.totalCells,
                    "A palette source is not the preserved complete 67-hit artwork.");
                if (i != 0) Require(sample.sourceConfigHash == result[0].sourceConfigHash && sample.authorityHash == result[0].authorityHash
                    && sample.recordHash == result[0].recordHash && sample.sourceJournalStyle == result[0].sourceJournalStyle,
                    "The four palette sources do not share the same source config, authority and sealed journal.");
                result[i] = sample;
            }
            return result;
        }
        static ClayPreviewPalettePreset MakePreset(int index, RoundedClayExperiment.Sample sample)
        {
            Texture2D[] brushes = index == 0
                ? BrushNames.Select(name => Resources.Load<Texture2D>(LabClayStickerSettings.ResourceFolder + name)).ToArray()
                : RoundedClayExperiment.Brushes(index == 1 ? "OriginalBevel" : "RoundedBevel");
            Shader compositor = index < 3 ? Resources.Load<Shader>(LabClayStickerRuntime.ShaderResource)
                : AssetDatabase.LoadAssetAtPath<Shader>(StickerCoatingExperiment.RuntimeShaderPath);
            var metadata = AssetDatabase.LoadAssetAtPath<TextAsset>(SamplePath(index));
            Require(brushes.All(brush => brush != null) && compositor != null, "Explicit brush/compositor dependencies are missing.");
            Require(sample.minimumDensity == (index == 0 ? .65f : .9f) && sample.maximumDensity == 1,
                "A frozen source pigment range differs from the four approved presets.");
            return new ClayPreviewPalettePreset { brushes = brushes, compositor = compositor,
                minimumDensity = sample.minimumDensity, maximumDensity = sample.maximumDensity,
                sampleMetadataHash = ClayPreviewSampleInfo.ContentHash(metadata.text), colorRawHash = sample.colorRawHash, heightRawHash = sample.heightRawHash };
        }
        static void ValidateData(ClayPreviewPaletteData data, RoundedClayExperiment.Sample[] samples, bool requirePins)
        {
            Require(ClayPreviewPaletteReplay.TryValidate(data, out string error), error);
            Require(data.journal == AssetDatabase.LoadAssetAtPath<TextAsset>(JournalPath)
                && File.Exists(JournalPath) && ClayPreviewBuild.Hash(File.ReadAllBytes(JournalPath)) == data.journalSha256,
                "The palette journal reference or source bytes differ from its manifest.");
            Require(data.epoch == "clay-visual-standalone-frozen-five-color" && data.recordHash == samples[0].recordHash
                && data.styleVersion == samples[0].sourceJournalStyle && data.columns * data.rows == samples[0].totalCells,
                "The palette dataset is not the same fixed source journal.");
            var expectedPalette = SourcePalette();
            Require(data.sourceSrgb != null && data.sourceSrgb.Length == expectedPalette.Length
                && data.sourceSrgb.Select(ColorKey).SequenceEqual(expectedPalette.Select(ColorKey)), "The source sRGB palette must match the canonical five element colors exactly.");
            var expectedSettings = RoundedClayExperiment.Settings();
            Require(data.settingsJson == JsonUtility.ToJson(expectedSettings), "The frozen palette settings differ from the original presentation settings.");
            for (int i = 0; i < PresetCount; i++)
            {
                ClayPreviewPalettePreset actual = data.presets[i], expected = MakePreset(i, samples[i]);
                Require(actual != null && actual.brushes.SequenceEqual(expected.brushes) && actual.compositor == expected.compositor
                    && actual.minimumDensity == expected.minimumDensity && actual.maximumDensity == expected.maximumDensity
                    && actual.sampleMetadataHash == expected.sampleMetadataHash && actual.colorRawHash == expected.colorRawHash && actual.heightRawHash == expected.heightRawHash,
                    "Palette preset dependencies or frozen output fingerprints changed. Explicitly run Prepare Palette Preview again.");
            }
            if (!requirePins) return;
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(SourcePinsPath);
            Require(text != null && File.Exists(SourcePinsPath), "Palette source pins are missing; explicitly prepare the palette preview.");
            var pins = JsonUtility.FromJson<SourcePins>(text.text); var expectedPins = MakeSourcePins(data);
            Require(pins != null && pins.format == PinsFormat && pins.journalSha256 == data.journalSha256 && pins.recordHash == data.recordHash
                && pins.sources != null && pins.sources.Length == expectedPins.sources.Length, "Palette source-pin manifest differs from the frozen journal.");
            for (int i = 0; i < expectedPins.sources.Length; i++)
                Require(pins.sources[i] != null && pins.sources[i].path == expectedPins.sources[i].path && pins.sources[i].sha256 == expectedPins.sources[i].sha256,
                    "A palette brush, importer or compositor source changed. Explicitly run Prepare Palette Preview; building never rebakes it.");
        }
        static ReplayResult CheckDefaultReplay(ClayPreviewPaletteReplay replay, ClayPreviewPaletteData data, int preset)
        {
            var result = new ReplayResult { preset = preset }; var timer = Stopwatch.StartNew(); replay.Request(preset, data.sourceSrgb);
            for (int tick = 0; tick < ReplayTickLimit; tick++)
            {
                replay.Tick(); Require(replay.Error == null, replay.Error); result.ticks = tick + 1;
                if (replay.Ready) break;
                AsyncGPUReadback.WaitAllRequests(); // Explicit stopped-Editor preparation only; never called by Build.
            }
            timer.Stop(); result.elapsedMilliseconds = timer.Elapsed.TotalMilliseconds;
            Require(replay.Ready && replay.CompletedPreset == preset && replay.RenderedThrough == data.lastSequence,
                "The default palette replay did not reach its final GPU fence within the bounded preparation budget.");
            Texture2D color = null, height = null;
            try
            {
                color = ClayPreviewBuild.ReadMap(replay.PaintTexture as RenderTexture, TextureFormat.RGBA32);
                height = ClayPreviewBuild.ReadMap(replay.HeightTexture as RenderTexture, TextureFormat.RGBAHalf);
                result.colorRawHash = ClayPreviewBuild.Hash(color.GetRawTextureData()); result.heightRawHash = ClayPreviewBuild.Hash(height.GetRawTextureData());
                result.exactColor = result.colorRawHash == data.presets[preset].colorRawHash;
                result.exactAllHeightChannels = result.heightRawHash == data.presets[preset].heightRawHash;
                Require(result.exactColor && result.exactAllHeightChannels, "Default replay differs from frozen preset " + preset + " color/height bytes; the palette dataset was not published.");
                return result;
            }
            finally { if (color != null) Object.DestroyImmediate(color); if (height != null) Object.DestroyImmediate(height); }
        }
        static byte[] PackJournal(LabPaintArtJournal journal)
        {
            Require(journal.Error == null && journal.IsSealed && !journal.HasPendingPage && journal.PageCount > 0,
                "Only a complete, sealed journal may be packaged for the palette preview.");
            using (var bytes = new MemoryStream()) using (var writer = new BinaryWriter(bytes))
            {
                writer.Write(journal.PageCount);
                for (int i = 0; i < journal.PageCount; i++)
                { Require(journal.TryReadPage(i, out byte[] page), journal.Error ?? "Could not read a frozen journal page."); writer.Write(page.Length); writer.Write(page); }
                writer.Flush(); return bytes.ToArray();
            }
        }
        static void ValidateStrokeColors(LabPaintArtJournal journal, Color32[] srgb)
        {
            var keys = new HashSet<uint>(srgb.Select(color => ColorKey((Color32)((Color)color).linear)));
            Require(keys.Count == 5, "The canonical palette collides in the journal's linear-byte storage.");
            var operations = new List<LabPaintArtOperation>(256); ulong after = 0;
            while (after < journal.LastSequence)
            {
                Require(journal.ReadOperations(after, 256, operations) && operations.Count > 0, journal.Error ?? "Could not inspect original stroke identities.");
                foreach (var operation in operations) if (operation.Kind == LabPaintArtOperationKind.StrokeBegin)
                    Require(keys.Contains(ColorKey(operation.Stamp.color)), "An original stamp uses an unknown palette color; composite RGB/painter IDs cannot identify its element.");
                after = operations[operations.Count - 1].Sequence;
            }
        }
        static SourcePins MakeSourcePins(ClayPreviewPaletteData data)
        {
            var paths = data.presets.SelectMany(preset => preset.brushes.Select(AssetDatabase.GetAssetPath)
                    .Concat(new[] { AssetDatabase.GetAssetPath(preset.compositor) }))
                .SelectMany(path => new[] { path, path + ".meta" }).Distinct().OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Require(paths.All(path => path.StartsWith("Assets/", StringComparison.Ordinal) && File.Exists(path)), "A palette dependency lacks a persistent source/importer file.");
            return new SourcePins { journalSha256 = data.journalSha256, recordHash = data.recordHash,
                sources = paths.Select(path => new SourcePin { path = path, sha256 = ClayPreviewBuild.Hash(File.ReadAllBytes(path)) }).ToArray() };
        }
        static Color32[] SourcePalette() => Enumerable.Range(1, 5).Select(element => LabPaintPalette.SrgbColorFor((LabOrbElement)element)).ToArray();
        static uint ColorKey(Color32 color) => ((uint)color.r << 24) | ((uint)color.g << 16) | ((uint)color.b << 8) | color.a;
        static string SamplePath(int index) => RoundedClayExperiment.Root + "/" + Keys[index] + "/SampleInfo.json";
        static ClayPreviewPaletteData ReadBoundData() => WithController(controller => controller.paletteData);
        static void BindData(ClayPreviewPaletteData data, bool requireClean = true) => WithController(controller =>
        {
            controller.paletteData = data; EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            Require(EditorSceneManager.SaveScene(controller.gameObject.scene, StickerCoatingExperiment.ScenePath), "Could not save the palette binding to its dedicated comparison Scene.");
            return data;
        }, requireClean);
        static T WithController<T>(Func<ClayPreviewController, T> action, bool requireClean = true)
        {
            Scene previous = SceneManager.GetActiveScene(), scene = SceneManager.GetSceneByPath(StickerCoatingExperiment.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded; string before = ClayPreviewBuild.SceneState();
            try
            {
                if (opened) scene = EditorSceneManager.OpenScene(StickerCoatingExperiment.ScenePath, OpenSceneMode.Additive);
                Require(!requireClean || !scene.isDirty, "Preserve unsaved comparison Scene changes before preparing or validating palette data.");
                var controllers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ClayPreviewController>(true)).ToArray();
                Require(controllers.Length == 1, "The dedicated coating Scene must contain one preview controller.");
                return action(controllers[0]);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                Require(before == ClayPreviewBuild.SceneState(), "Palette binding changed unrelated loaded Scene state.");
            }
        }
        static void Preflight()
        {
            Require(File.Exists(StickerCoatingExperiment.ScenePath), "Explicitly prepare the four-preset coating comparison first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                Require(!scene.isLoaded || !string.IsNullOrEmpty(scene.path), "Open a saved Scene first; unsaved Untitled content is preserved.");
                if (scene.path == StickerCoatingExperiment.ScenePath) Require(!scene.isDirty, "Save/preserve unsaved coating Scene changes before palette preparation or validation.");
            }
        }
        static Dictionary<string, Fingerprint> PreservedFiles()
        {
            var original = new[] { ClayPreviewBuild.ColorPath, ClayPreviewBuild.HeightPath, ClayPreviewBuild.SampleInfoPath, ClayPreviewBuild.ScenePath };
            var paths = Directory.GetFiles(RoundedClayExperiment.Root, "*", SearchOption.AllDirectories)
                .Where(path => path.Replace('\\', '/') != StickerCoatingExperiment.ScenePath)
                .Concat(original.SelectMany(path => new[] { path, path + ".meta" })).Distinct();
            return paths.Where(File.Exists).ToDictionary(path => path, path => new Fingerprint
                { hash = ClayPreviewBuild.Hash(File.ReadAllBytes(path)), writeTicks = File.GetLastWriteTimeUtc(path).Ticks });
        }
        static void RequirePreserved(Dictionary<string, Fingerprint> originals)
        {
            foreach (var original in originals) Require(File.Exists(original.Key)
                && original.Value.hash == ClayPreviewBuild.Hash(File.ReadAllBytes(original.Key))
                && original.Value.writeTicks == File.GetLastWriteTimeUtc(original.Key).Ticks,
                "Palette preparation changed an existing source map, metadata, brush or earlier Scene: " + original.Key);
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message ?? "Palette preparation failed."); }
    }
}
