using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using C6Lab;
using Hapio;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>
    /// Visual fixtures inside an already running, genuinely bound Battle. It never installs
    /// fixture pixels into the game model. All renderer/component changes are temporary.
    /// </summary>
    [InitializeOnLoad]
    public static class LabWatercolorStampBattleCapture
    {
        static Context active;
        public static bool IsRunning => active != null;
        public static string Status { get; private set; } = "Idle";
        public static string Error { get; private set; }
        static LabWatercolorStampBattleCapture()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode && active != null)
                {
                    Error = "Battle capture was interrupted by leaving Play mode.";
                    active.Dispose(); active = null; Status = "Interrupted";
                }
            };
        }

        public static void Start(string directory)
        {
            StartInternal(directory, null);
        }

        /// <summary>Six temporary visual frames in the actual Battle; the production default stays V2.</summary>
        public static void StartV3(string directory, LabWatercolorV3Profile profile = null)
        {
            if (profile == null) profile = LabWatercolorV3Baker.LoadProfile();
            LabWatercolorV3Profile capturedProfile = profile;
            StartInternal(directory, context => CaptureV3(context, capturedProfile));
        }

        static void StartInternal(string directory, Func<Context, IEnumerator> capture)
        {
            if (IsRunning) throw new InvalidOperationException("A Battle capture is already running.");
            if (!Application.isPlaying || string.IsNullOrEmpty(directory) || !Path.IsPathRooted(directory))
                throw new InvalidOperationException("Enter the actual Battle in Editor Play mode and provide an absolute output directory.");
            var bindings = Object.FindFirstObjectByType<GameBattleBindings>();
            var session = GameSession.Instance;
            if (bindings == null || session == null || bindings.gameObject.scene.path != "Assets/Hapio/Scenes/Battle.unity"
                || session.Network == null || !session.Network.IsDeveloperSolo || session.Network.target != bindings.target)
                throw new InvalidOperationException("The actual saved Battle and its persistent session are not bound.");
            var paint = bindings.target.GetComponent<LabGourdPaint>();
            if (paint == null || !paint.IsReady || !paint.WatercolorStampsActive)
                throw new InvalidOperationException("Wait for the real Battle watercolor renderer to initialize.");
            // CLI evaluation can report the Editor pane's Screen dimensions rather
            // than the rendering Game view. Inspect its selected target resolution.
            Type gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow gameView = gameViewType == null ? null : EditorWindow.GetWindow(gameViewType);
            if (gameView != null) gameView.Focus();
            PropertyInfo targetSize = gameViewType?.GetProperty("targetRenderSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object target = targetSize?.GetValue(gameView);
            Vector2 renderSize = target is Vector2 size ? size : target is Vector2Int integerSize ? (Vector2)integerSize : Vector2.zero;
            if (renderSize.x < 1f || renderSize.y <= renderSize.x)
                throw new InvalidOperationException("Use a portrait Game view target resolution for the game-size comparison.");
            // WaitForEndOfFrame is suspended while the Scene tab owns an Editor Play
            // session. The focused Game view supplies the actual rendered frames.
            Directory.CreateDirectory(directory);
            Error = null; Status = "Preparing";
            active = new Context(bindings, session, paint, directory);
            bindings.StartCoroutine(GuardedCapture(active, capture));
        }

        static IEnumerator GuardedCapture(Context context, Func<Context, IEnumerator> capture = null)
        {
            IEnumerator routine = capture == null ? Capture(context) : capture(context);
            while (true)
            {
                bool next; object yielded = null;
                try { next = routine.MoveNext(); if (next) yielded = routine.Current; }
                catch (Exception exception) { Error = exception.ToString(); next = false; }
                if (!next) break;
                yield return yielded;
            }
            try { (routine as IDisposable)?.Dispose(); }
            finally
            {
                try { context.Dispose(); }
                catch (Exception exception) { Error = Error ?? exception.ToString(); }
                if (context.V3Report != null)
                {
                    try { context.WriteV3Report(Error); }
                    catch (Exception exception) { Error = Error ?? exception.ToString(); }
                }
                active = null;
                Status = Error == null ? "Completed" : "Failed";
            }
        }

        static IEnumerator CaptureV3(Context context, LabWatercolorV3Profile profile)
        {
            context.V3Report = new V3BattleReport {
                unity = Application.unityVersion, scene = context.Report.scene,
                classification = "Actual running developer-solo Battle camera/layout with temporary private Host-rule visual fixtures only. Columns use the same V2 source journal/authority and saved gourd geometry. V3 is a private immutable appearance overlay; the game default remains V2. This is not live-gameplay, network, victory, iOS or device-performance validation.",
                gameAuthorityBefore = context.AuthorityHash, profileAsset = AssetDatabase.GetAssetPath(profile),
                profileSnapshot = EditorJsonUtility.ToJson(profile), columnOrder = LabWatercolorV3Study.ColumnNames,
                stampShaderSha256 = FileHash(LabWatercolorV3Study.StampShaderPath),
                surfaceShaderSha256 = FileHash(LabWatercolorV3Study.SurfaceShaderPath)
            };
            using (var study = new LabWatercolorV3Study(profile))
            {
                LabWatercolorStampSettings settings = study.SourceSettings;
                context.V3Report.sourceProfile = settings.Version;
                context.V3Report.variantIdentities = new[] { settings.Version,
                    study.GetSnapshot(1).Identity, study.GetSnapshot(2).Identity };
                foreach (LabWatercolorStampFixture fixture in new[] {
                    LabWatercolorStampFixture.BlueRehits, LabWatercolorStampFixture.FiveColorComplete })
                {
                    study.Prepare(fixture, 0, 123u);
                    string authority = LabWatercolorStampStudy.Hash(study.Surface);
                    string stem = fixture.ToString().ToLowerInvariant() + "-first-plus-0";
                    var frames = new Texture2D[3];
                    try
                    {
                        for (int column = 0; column < frames.Length; column++)
                        {
                            Status = "V3 Battle " + fixture + " / " + LabWatercolorV3Study.ColumnNames[column];
                            context.BindV3(study.GetDisplayMap(column), study.AuthorityPainterMap, settings, study.GetSnapshot(column));
                            Canvas.ForceUpdateCanvases();
                            yield return new WaitForEndOfFrame();
                            frames[column] = ScreenCapture.CaptureScreenshotAsTexture();
                        }
                        if (authority != LabWatercolorStampStudy.Hash(study.Surface))
                            throw new InvalidOperationException("Battle rendering changed the private source color/P map.");
                        context.SaveV3Triptych(frames, stem, authority, study.SourceJournal);
                    }
                    finally { foreach (Texture2D image in frames) if (image != null) Object.Destroy(image); }
                }
            }
            if (LabWatercolorStampStudy.Hash(context.Network.PaintSurface) != context.AuthorityHash)
                throw new InvalidOperationException("The V3 visual capture changed the actual game color/P map.");
            context.V3Report.gameAuthorityPreserved = true;
        }

        static string FileHash(string path)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        static IEnumerator Capture(Context context)
        {
            using (var before = new LabWatercolorStampStudy())
            using (var after = new LabWatercolorStampStudy())
            {
                LabWatercolorStampSettings oldSettings = LabWatercolorStampStudy.V1EquivalentSettings();
                LabWatercolorStampSettings newSettings = LabWatercolorStampSettings.CaptureDefault();
                foreach (LabWatercolorStampFixture fixture in Enum.GetValues(typeof(LabWatercolorStampFixture)))
                {
                    int[] counts = fixture == LabWatercolorStampFixture.BlueRehits ? new[] { 0, 1, 3 } : new[] { 3 };
                    foreach (int repeats in counts)
                    {
                        Status = fixture + " / " + repeats;
                        before.Prepare(fixture, repeats, 123u, oldSettings);
                        after.Prepare(fixture, repeats, 123u, newSettings);
                        string authority = LabWatercolorStampStudy.Hash(before.Surface);
                        if (authority != LabWatercolorStampStudy.Hash(after.Surface))
                            throw new InvalidOperationException("V1/V2 fixture color/P maps differ.");
                        Texture2D[] frames = new Texture2D[2];
                        try
                        {
                            context.Bind(before.CandidateMap, before.HighResolutionPainterMap, oldSettings);
                            Canvas.ForceUpdateCanvases();
                            yield return new WaitForEndOfFrame();
                            frames[0] = ScreenCapture.CaptureScreenshotAsTexture();
                            context.Bind(after.CandidateMap, after.HighResolutionPainterMap, newSettings);
                            Canvas.ForceUpdateCanvases();
                            yield return new WaitForEndOfFrame();
                            frames[1] = ScreenCapture.CaptureScreenshotAsTexture();
                            string stem = fixture.ToString().ToLowerInvariant() + "-" + repeats;
                            context.SavePair(frames, stem, authority);
                        }
                        finally { foreach (Texture2D image in frames) if (image != null) Object.Destroy(image); }
                    }
                }
            }
            context.Report.profile = LabWatercolorStampSettings.CaptureDefault().Version;
            if (LabWatercolorStampStudy.Hash(context.Network.PaintSurface) != context.AuthorityHash)
                throw new InvalidOperationException("The visual capture changed the actual game color/P map.");
            context.Report.gameAuthorityPreserved = true;
            File.WriteAllText(Path.Combine(context.Directory, "battle-validation.json"), JsonUtility.ToJson(context.Report, true));
        }

        sealed class Context : IDisposable
        {
            public readonly LabNetwork Network;
            public readonly string Directory, AuthorityHash;
            public readonly BattleReport Report;
            public V3BattleReport V3Report;
            readonly GameSession session;
            readonly LabGourdPaint paint;
            readonly Renderer shell;
            readonly Camera camera;
            readonly Material[] materials;
            readonly MaterialPropertyBlock original = new MaterialPropertyBlock();
            readonly MaterialPropertyBlock[] originalSlots;
            readonly MaterialPropertyBlock fixtureBlock = new MaterialPropertyBlock();
            readonly Material privateMaterial;
            readonly Material originalPaintMaterial;
            readonly LabHangingTarget hanging;
            readonly bool networkEnabled, sessionEnabled, hangingEnabled;
            readonly Vector3 position;
            readonly Quaternion rotation;
            bool disposed;

            public Context(GameBattleBindings bindings, GameSession session, LabGourdPaint paint, string directory)
            {
                this.session = session; this.paint = paint;
                Network = session.Network; Directory = directory;
                AuthorityHash = LabWatercolorStampStudy.Hash(Network.PaintSurface);
                shell = paint.Shell.GetComponent<Renderer>(); camera = bindings.battleCamera;
                materials = shell.sharedMaterials; shell.GetPropertyBlock(original); shell.GetPropertyBlock(fixtureBlock);
                originalSlots = new MaterialPropertyBlock[materials.Length];
                for (int i = 0; i < materials.Length; i++)
                { originalSlots[i] = new MaterialPropertyBlock(); shell.GetPropertyBlock(originalSlots[i], i); }
                networkEnabled = Network.enabled; sessionEnabled = session.enabled;
                hanging = bindings.target.GetComponent<LabHangingTarget>(); hangingEnabled = hanging != null && hanging.enabled;
                position = bindings.target.transform.localPosition; rotation = bindings.target.transform.localRotation;
                originalPaintMaterial = paint.paintMaterial;
                privateMaterial = new Material(originalPaintMaterial) { hideFlags = HideFlags.DontSave };
                var copies = (Material[])materials.Clone();
                for (int i = 0; i < copies.Length; i++) if (copies[i] == paint.paintMaterial) copies[i] = privateMaterial;
                // A distinct material makes LabGourdPaint.IsReady false; its ordinary Update
                // cannot rebind fixture maps. Never disable it: OnDisable would destroy the model.
                shell.sharedMaterials = copies;
                Network.enabled = false; session.enabled = false;
                if (hanging != null) hanging.enabled = false;
                Report = new BattleReport { unity = Application.unityVersion,
                    scene = bindings.gameObject.scene.path,
                    classification = "Actual running Battle camera/layout and production gourd shader; temporary private Host-rule visual fixtures. This is not a gameplay victory or device test.",
                    gameAuthorityBefore = AuthorityHash };
            }
            public void Bind(Texture map, Texture owners, LabWatercolorStampSettings settings)
            {
                fixtureBlock.SetTexture("_PaintMap", map); fixtureBlock.SetFloat("_PaintMapReady", 1f);
                fixtureBlock.SetTexture("_PainterMap", owners);
                fixtureBlock.SetFloat("_WatercolorV2Enabled", settings.enabled ? 1f : 0f);
                fixtureBlock.SetFloat("_WatercolorGranulationStrength", settings.granulationStrength);
                fixtureBlock.SetFloat("_WatercolorGranulationScale", settings.granulationScale);
                for (int i = 0; i < materials.Length; i++) shell.SetPropertyBlock(null, i);
                shell.SetPropertyBlock(fixtureBlock);
            }
            public void BindV3(Texture map, Texture owners, LabWatercolorStampSettings settings, LabWatercolorV3Snapshot snapshot)
            {
                // Reset every column from the original material and block. In particular,
                // the flat V3 paper must never carry into a subsequent V2 column.
                privateMaterial.shader = originalPaintMaterial.shader;
                privateMaterial.CopyPropertiesFromMaterial(originalPaintMaterial);
                fixtureBlock.Clear();
                shell.SetPropertyBlock(original.isEmpty ? null : original);
                shell.GetPropertyBlock(fixtureBlock);
                if (snapshot != null)
                {
                    Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(LabWatercolorV3Study.SurfaceShaderPath);
                    if (shader == null || !shader.isSupported) throw new InvalidOperationException("The private V3 surface shader is unavailable.");
                    privateMaterial.shader = shader;
                    snapshot.ApplySurface(privateMaterial);
                }
                Bind(map, owners, settings);
            }

            public void SaveV3Triptych(Texture2D[] frames, string stem, string authority, LabPaintArtJournal journal)
            {
                if (frames == null || frames.Length != 3 || frames.Any(image => image == null))
                    throw new InvalidOperationException("Three complete V2/V3 Battle frames are required.");
                int width = frames[0].width, height = frames[0].height;
                if (frames.Any(image => image.width != width || image.height != height) || width != 390 || height != 844)
                    throw new InvalidOperationException("Use the actual 390x844 Game view resolution for the V3 Battle comparison.");
                V3Report.width = width; V3Report.height = height;
                var entry = new V3BattleEntry { fixture = stem, fixtureAuthorityHash = authority,
                    sourceStyleVersion = journal.StyleVersion, sourceJournalPrefixHash = journal.PrefixHash,
                    sourceLastSequence = journal.LastSequence, battleFrames = new string[3], gourdCrops = new string[3],
                    battleComparison = stem + "-battle-v2-v3-075-v3-085.png",
                    cropComparison = stem + "-battle-gourd-v2-v3-075-v3-085.png" };
                RectInt bounds = ScreenBounds(width, height);
                entry.cropX = bounds.x; entry.cropY = bounds.y; entry.cropWidth = bounds.width; entry.cropHeight = bounds.height;
                Texture2D combined = null, cropCombined = null;
                var close = new Texture2D[3];
                try
                {
                    for (int i = 0; i < frames.Length; i++)
                    {
                        entry.battleFrames[i] = stem + "-" + LabWatercolorV3Study.ColumnNames[i] + "-battle.png";
                        entry.gourdCrops[i] = stem + "-" + LabWatercolorV3Study.ColumnNames[i] + "-battle-gourd-crop.png";
                        File.WriteAllBytes(Path.Combine(Directory, entry.battleFrames[i]), frames[i].EncodeToPNG());
                        close[i] = Crop(frames[i], bounds);
                        File.WriteAllBytes(Path.Combine(Directory, entry.gourdCrops[i]), close[i].EncodeToPNG());
                    }
                    combined = LabPaintStudyRenderer.Combine(frames); cropCombined = LabPaintStudyRenderer.Combine(close);
                    File.WriteAllBytes(Path.Combine(Directory, entry.battleComparison), combined.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(Directory, entry.cropComparison), cropCombined.EncodeToPNG());
                    V3Report.entries.Add(entry);
                }
                finally
                {
                    if (combined != null) Object.Destroy(combined); if (cropCombined != null) Object.Destroy(cropCombined);
                    foreach (Texture2D image in close) if (image != null) Object.Destroy(image);
                }
            }

            public void WriteV3Report(string error)
            {
                V3Report.captureError = error;
                File.WriteAllText(Path.Combine(Directory, "battle-v3-validation.json"), JsonUtility.ToJson(V3Report, true));
            }
            public void SavePair(Texture2D[] frames, string stem, string authority)
            {
                if (frames[0] == null || frames[1] == null || frames[0].width != frames[1].width || frames[0].height != frames[1].height)
                    throw new InvalidOperationException("A complete pair of game-view captures is required.");
                if (frames[0].width < 1 || frames[0].height <= frames[0].width)
                    throw new InvalidOperationException("The actual Game view capture must be portrait.");
                if (Report.width != 0 && (Report.width != frames[0].width || Report.height != frames[0].height))
                    throw new InvalidOperationException("Keep the Game view target resolution unchanged during comparison.");
                Report.width = frames[0].width; Report.height = frames[0].height;
                Texture2D combined = null; Texture2D[] close = new Texture2D[2]; Texture2D closePair = null;
                try
                {
                    for (int i = 0; i < frames.Length; i++)
                        File.WriteAllBytes(Path.Combine(Directory, stem + (i == 0 ? "-v1-battle.png" : "-v2-battle.png")), frames[i].EncodeToPNG());
                    combined = LabPaintStudyRenderer.Combine(frames);
                    File.WriteAllBytes(Path.Combine(Directory, stem + "-battle-comparison.png"), combined.EncodeToPNG());
                    RectInt bounds = ScreenBounds(frames[0].width, frames[0].height);
                    for (int i = 0; i < 2; i++) close[i] = Crop(frames[i], bounds);
                    closePair = LabPaintStudyRenderer.Combine(close);
                    File.WriteAllBytes(Path.Combine(Directory, stem + "-battle-gourd-crop.png"), closePair.EncodeToPNG());
                    Report.entries.Add(new BattleEntry { fixture = stem, fixtureAuthorityHash = authority,
                        cropX = bounds.x, cropY = bounds.y, cropWidth = bounds.width, cropHeight = bounds.height });
                }
                finally
                {
                    if (combined != null) Object.Destroy(combined); if (closePair != null) Object.Destroy(closePair);
                    foreach (Texture2D image in close) if (image != null) Object.Destroy(image);
                }
            }
            RectInt ScreenBounds(int width, int height)
            {
                Bounds b = shell.bounds; Vector2 min = new Vector2(width, height), max = Vector2.zero;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1,
                        (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 q = camera.WorldToScreenPoint(p);
                    if (q.z <= 0f) throw new InvalidOperationException("The actual gourd is behind its Battle camera.");
                    min = Vector2.Min(min, q); max = Vector2.Max(max, q);
                }
                int x = Mathf.Clamp(Mathf.FloorToInt(min.x) - 8, 0, width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt(min.y) - 8, 0, height - 1);
                return new RectInt(x, y, Mathf.Clamp(Mathf.CeilToInt(max.x) + 8, x + 1, width) - x,
                    Mathf.Clamp(Mathf.CeilToInt(max.y) + 8, y + 1, height) - y);
            }
            static Texture2D Crop(Texture2D source, RectInt bounds)
            {
                var input = source.GetPixels32(); var result = new Color32[bounds.width * bounds.height];
                for (int y = 0; y < bounds.height; y++)
                    Array.Copy(input, (bounds.y + y) * source.width + bounds.x, result, y * bounds.width, bounds.width);
                var image = new Texture2D(bounds.width, bounds.height, TextureFormat.RGBA32, false, false);
                image.SetPixels32(result); image.Apply(false, false); return image;
            }
            public void Dispose()
            {
                if (disposed) return; disposed = true;
                bool restoreFailed = false;
                if (shell != null)
                {
                    shell.sharedMaterials = materials; shell.SetPropertyBlock(original);
                    for (int i = 0; i < originalSlots.Length; i++) shell.SetPropertyBlock(originalSlots[i].isEmpty ? null : originalSlots[i], i);
                }
                if (paint != null && paint.target != null)
                { paint.target.transform.localPosition = position; paint.target.transform.localRotation = rotation; }
                if (hanging != null) hanging.enabled = hangingEnabled;
                if (Network != null) Network.enabled = networkEnabled;
                if (session != null) session.enabled = sessionEnabled;
                if (V3Report != null)
                {
                    V3Report.materialBindingsRestored = shell != null && materials.SequenceEqual(shell.sharedMaterials);
                    var observed = new MaterialPropertyBlock();
                    if (shell != null) shell.GetPropertyBlock(observed);
                    V3Report.paintBindingsRestored = shell != null && observed.isEmpty == original.isEmpty
                        && observed.GetTexture("_PaintMap") == original.GetTexture("_PaintMap")
                        && observed.GetTexture("_PainterMap") == original.GetTexture("_PainterMap")
                        && observed.GetFloat("_PaintMapReady") == original.GetFloat("_PaintMapReady")
                        && observed.GetFloat("_WatercolorV2Enabled") == original.GetFloat("_WatercolorV2Enabled")
                        && observed.GetFloat("_WatercolorGranulationStrength") == original.GetFloat("_WatercolorGranulationStrength")
                        && observed.GetFloat("_WatercolorGranulationScale") == original.GetFloat("_WatercolorGranulationScale");
                    V3Report.componentStatesRestored = Network != null && Network.enabled == networkEnabled
                        && session != null && session.enabled == sessionEnabled && (hanging == null || hanging.enabled == hangingEnabled);
                    V3Report.targetPoseRestored = paint != null && paint.target != null
                        && paint.target.transform.localPosition == position && paint.target.transform.localRotation == rotation;
                    if (!V3Report.materialBindingsRestored || !V3Report.paintBindingsRestored
                        || !V3Report.componentStatesRestored || !V3Report.targetPoseRestored)
                        restoreFailed = true;
                }
                if (privateMaterial != null) Object.Destroy(privateMaterial);
                if (restoreFailed) throw new InvalidOperationException("The temporary V3 Battle fixture did not restore its original bindings and pose.");
            }
        }
        [Serializable] sealed class BattleReport
        {
            public string unity, scene, profile, classification, gameAuthorityBefore;
            public int width, height;
            public bool gameAuthorityPreserved;
            public List<BattleEntry> entries = new List<BattleEntry>();
        }
        [Serializable] sealed class BattleEntry
        {
            public string fixture, fixtureAuthorityHash;
            public int cropX, cropY, cropWidth, cropHeight;
        }
        [Serializable] sealed class V3BattleReport
        {
            public string unity, scene, classification, gameAuthorityBefore, sourceProfile, profileAsset, profileSnapshot;
            public string stampShaderSha256, surfaceShaderSha256, captureError;
            public string[] columnOrder, variantIdentities;
            public int width, height;
            public bool gameAuthorityPreserved, materialBindingsRestored, paintBindingsRestored, componentStatesRestored, targetPoseRestored;
            public List<V3BattleEntry> entries = new List<V3BattleEntry>();
        }
        [Serializable] sealed class V3BattleEntry
        {
            public string fixture, fixtureAuthorityHash, sourceStyleVersion, sourceJournalPrefixHash;
            public string battleComparison, cropComparison;
            public string[] battleFrames, gourdCrops;
            public ulong sourceLastSequence;
            public int cropX, cropY, cropWidth, cropHeight;
        }
    }
}
