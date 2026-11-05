using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Disposable Editor comparison only. Does not enable a new runtime paint mode.</summary>
    public static class LabSurfaceStickerPrototype
    {
        public const string Folder = "Assets/Editor/WatercolorPaint/SurfaceSticker";
        public static readonly string[] AssetNames = { "BroadPaper", "LongPaper", "ClusterPaper" };

        [MenuItem("C6 Lab/Experiments/Surface Sticker/Export Comparison")]
        public static void ExportMenu()
        {
            string directory = EditorUtility.OpenFolderPanel("Surface sticker comparison outside Assets", "", "");
            if (!string.IsNullOrEmpty(directory)) Export(directory);
        }

        public static void ImportAssets()
        {
            foreach (string name in AssetNames)
            {
                string path = Folder + "/Assets/" + name + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null) throw new InvalidOperationException("Missing prepared sticker " + path);
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.isReadable = true;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
        }

        public static string Export(string directory)
        {
            if (!Path.IsPathRooted(directory) || Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Application.dataPath), StringComparison.Ordinal))
                throw new ArgumentException("Choose an evidence directory outside Assets.");
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Run in the stopped, compiled Editor.");
            string beforeScenes = Scenes(), beforeRandom = JsonUtility.ToJson(UnityEngine.Random.state);
            var config = Resources.Load<LabConfig>("LabConfig");
            string beforeConfig = EditorJsonUtility.ToJson(config);
            Directory.CreateDirectory(directory);
            var report = new Report { unity = Application.unityVersion, gpu = SystemInfo.graphicsDeviceType.ToString(),
                scope = "Editor-only real WhiteGourd mesh + prepared neutral RGBA assets tinted with recorded elemental colors. No live Battle, network, viewer, iOS or Mac Player integration. Raw-asset column intentionally has no gameplay coverage claim. Fitted column reads actual paid cells without changing them. Overlap repaint inside previously covered cells is not reconstructed in this prototype.",
                checks = LabSurfaceStickerHistoryChecks.Run() };
            var images = new List<Texture2D>();
            try
            {
                var settings = LabWatercolorStampSettings.CaptureDefault(); settings.revealSeconds = 0;
                LabWatercolorStampFixture[] fixtures = { LabWatercolorStampFixture.BlueRehits, LabWatercolorStampFixture.BlueRehits, LabWatercolorStampFixture.TwoColorOverlap };
                int[] repeats = { 0, 3, 0 };
                for (int row = 0; row < fixtures.Length; row++)
                {
                    using (var study = new LabWatercolorStampStudy())
                    {
                        study.Prepare(fixtures[row], repeats[row], 123u, settings);
                        string hash = LabWatercolorStampStudy.Hash(study.Surface);
                        int covered = study.Surface.CoveredCount, painted = study.Surface.PaintCount;
                        var history = LabSurfaceStickerHistory.Read(study.Journal);
                        history.AssertMatches(study.Surface);
                        var entry = new Entry { fixture = fixtures[row].ToString(), reHits = repeats[row], cells = covered, paintCount = painted, authorityBefore = hash };
                        using (var old = new LabPaintStudyRenderer(true))
                            images.Add(old.RenderVisual(study.CandidateMap, 0, 1.48f, 0, settings));
                        using (var raw = new Preview(history, true))
                        {
                            images.Add(raw.Render(0, 1.48f));
                            entry.originalShapeTriangles = raw.Triangles;
                        }
                        using (var fitted = new Preview(history, false))
                        {
                            images.Add(fitted.Render(0, 1.48f));
                            entry.fittedTriangles = fitted.Triangles; entry.stickerLayers = fitted.LayerCount;
                            entry.generatedMeshBytes = fitted.MeshBytes;
                            if (row == 1)
                            {
                                var six = new List<Texture2D>();
                                try
                                {
                                    float[] yaw = { 0, 90, 180, -90, 45, 45 };
                                    for (int view = 0; view < 6; view++) six.Add(fitted.Render(yaw[view], 1f, view == 5 ? 40f : 0));
                                    Grid(directory, "surface-sticker-six-views.png", six, 3);
                                }
                                finally { foreach (var frame in six) Object.DestroyImmediate(frame); }
                                Save(directory, "surface-sticker-detail.png", fitted.Render(0, 2.05f), true);
                            }
                        }
                        history.AssertMatches(study.Surface);
                        entry.authorityAfter = LabWatercolorStampStudy.Hash(study.Surface);
                        if (hash != entry.authorityAfter || covered != study.Surface.CoveredCount || painted != study.Surface.PaintCount)
                            throw new InvalidOperationException("Presentation changed gameplay authority.");
                        report.entries.Add(entry);
                    }
                }
                // Rows: first / first+3 re-hits / two colors. Columns: flat map / asset shape only / fitted paid cells.
                Grid(directory, "surface-sticker-comparison.png", images, 3);
                for (int i = 0; i < images.Count; i++) Save(directory, "row-" + (i / 3) + "-column-" + (i % 3) + ".png", images[i]);
                report.scenePreserved = beforeScenes == Scenes();
                report.randomPreserved = beforeRandom == JsonUtility.ToJson(UnityEngine.Random.state);
                report.configPreserved = beforeConfig == EditorJsonUtility.ToJson(config);
                if (!report.scenePreserved || !report.randomPreserved || !report.configPreserved)
                    throw new InvalidOperationException("A study changed shared Editor/game inputs.");
                File.WriteAllText(Path.Combine(directory, "surface-sticker-report.json"), JsonUtility.ToJson(report, true));
                Debug.Log("C6_SURFACE_STICKER_PROTOTYPE_COMPLETE " + directory);
                return JsonUtility.ToJson(report);
            }
            finally { foreach (var frame in images) if (frame != null) Object.DestroyImmediate(frame); }
        }

        sealed class Preview : IDisposable
        {
            readonly LabPaintStudyRenderer renderer;
            readonly List<Object> resources = new List<Object>();
            readonly List<LabSurfaceStickerMesh.Result> meshes = new List<LabSurfaceStickerMesh.Result>();
            public int Triangles { get; private set; }
            public int LayerCount { get; private set; }
            public long MeshBytes { get; private set; }
            public Preview(LabSurfaceStickerHistory history, bool originalShape)
            {
                try
                {
                    renderer = new LabPaintStudyRenderer(true, configureStudyInstance: root => Attach(root, history, originalShape));
                }
                catch { Dispose(); throw; }
            }
            void Attach(GameObject root, LabSurfaceStickerHistory history, bool originalShape)
            {
                MeshRenderer body = root.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.sharedMaterials.Any(m => m && m.shader.name == "C6Lab/GourdPaint"));
                Mesh source = body.GetComponent<MeshFilter>().sharedMesh;
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/SurfaceSticker.shader");
                if (shader == null || !shader.isSupported || ShaderUtil.GetShaderMessages(shader).Any(m => m.severity.ToString() == "Error"))
                    throw new InvalidOperationException("Surface sticker shader is missing or failed compilation.");
                Texture2D top = null;
                if (!originalShape)
                {
                    var ids = new Color32[history.CellCount]; int index = 0;
                    foreach (var item in history.Layers)
                    {
                        if (item.Cells.Count == 0) continue;
                        if (++index > 255) throw new InvalidOperationException("This small still prototype supports at most 255 visible layers.");
                        foreach (int cell in item.Cells) ids[cell] = new Color32((byte)index,0,0,255);
                    }
                    top = new Texture2D(history.Columns,history.Rows,TextureFormat.RGBA32,false,true) { name="Study top sticker IDs",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Point,wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp };
                    top.SetPixels32(ids);top.Apply(); resources.Add(top);
                }
                foreach (var layer in history.Layers)
                {
                    if (layer.Cells.Count == 0) continue;
                    var paid = layer.Cells.OrderBy(i => i).ToArray();
                    var projection = LabSurfaceStickerMesh.Fit(layer.Stamp, paid, history.Columns, history.Rows);
                    int[] geometryCells = paid;
                    if (originalShape)
                        geometryCells = Enumerable.Range(0, history.CellCount).Where(i => {
                            Vector3 d = LabSurfaceStickerMesh.CellDirection(i, history.Columns, history.Rows);
                            if (Vector3.Dot(d, layer.Stamp.center) < .1f) return false;
                            Vector2 uv = projection.Project(d);
                            return uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1;
                        }).ToArray();
                    var mesh = LabSurfaceStickerMesh.Build(source, geometryCells, history.Columns, history.Rows, projection);
                    meshes.Add(mesh); Triangles += mesh.SelectedTriangleCount;
                    MeshBytes += mesh.Mesh.vertexCount * (12 + 12 + 8 + 8 + 16) + mesh.SelectedTriangleCount * 12;
                    string path = Folder + "/Assets/" + AssetNames[layer.AssetIndex(AssetNames.Length)] + ".png";
                    Texture2D sourceAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (sourceAsset == null || !sourceAsset.isReadable) throw new InvalidOperationException("Import prepared asset first: " + path);
                    Texture2D texture = FilledRgb(sourceAsset); resources.Add(texture);
                    var material = new Material(shader) { name = "Surface Sticker " + layer.StrokeId, hideFlags = HideFlags.HideAndDontSave };
                    resources.Add(material);
                    material.SetTexture("_StickerTex", texture);
                    Color ink = layer.Stamp.color; // descriptor bytes are already linear pigment values.
                    material.SetVector("_Tint", new Vector4(ink.r,ink.g,ink.b,ink.a));
                    material.SetFloat("_TintFromLuminance", 1);
                    material.SetFloat("_FiberReference", FiberReference(sourceAsset));
                    material.SetFloat("_UseAssetAlpha", originalShape ? 1 : 0);
                    material.SetFloat("_UseCoverage", originalShape ? 0 : 1);
                    if (!originalShape)
                    {
                        Texture2D mask = history.CreateMask(layer); resources.Add(mask);
                        mask.filterMode = FilterMode.Bilinear;
                        material.SetTexture("_CoverageTex", mask);
                        material.SetTexture("_TopIdTex", top);
                        material.SetFloat("_UseTopOwner", 1);
                        material.SetFloat("_LayerIndex", LayerCount+1);
                    }
                    var go = new GameObject("Surface Sticker " + layer.StrokeId) { hideFlags = HideFlags.HideAndDontSave, layer = body.gameObject.layer };
                    go.transform.SetParent(body.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh.Mesh;
                    var draw = go.AddComponent<MeshRenderer>(); draw.sharedMaterial = material;
                    draw.sortingOrder = ++LayerCount * 2;
                    draw.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    draw.receiveShadows = false;
                    if (originalShape)
                    {
                        var shadowMaterial = new Material(material) { name="Thin paper contact shade",hideFlags=HideFlags.HideAndDontSave };
                        resources.Add(shadowMaterial);shadowMaterial.SetFloat("_ShadowOnly",1);
                        var shade = new GameObject("Contact shade "+layer.StrokeId) { hideFlags=HideFlags.HideAndDontSave,layer=body.gameObject.layer };
                        shade.transform.SetParent(body.transform,false);
                        shade.AddComponent<MeshFilter>().sharedMesh=mesh.Mesh;
                        var shadowDraw=shade.AddComponent<MeshRenderer>();shadowDraw.sharedMaterial=shadowMaterial;
                        shadowDraw.sortingOrder=LayerCount*2-1;
                        shadowDraw.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;shadowDraw.receiveShadows=false;
                    }
                }
            }
            public Texture2D Render(float yaw, float zoom, float pitch = 0) => renderer.RenderVisual(Texture2D.blackTexture, yaw, zoom, pitch);
            public void Dispose()
            {
                renderer?.Dispose();
                foreach (var mesh in meshes) mesh.Dispose(); meshes.Clear();
                foreach (Object value in resources) if (value != null) Object.DestroyImmediate(value); resources.Clear();
            }
        }

        // Technical RGB edge dilation only. Preserve the original asset and its alpha;
        // the fitted mode may sample outside the artist's contour after fitting to authority.
        static Texture2D FilledRgb(Texture2D source)
        {
            Color32[] original = source.GetPixels32(), pixels = (Color32[])original.Clone();
            int w = source.width, h = source.height;
            var queue = new Queue<int>(); var visited = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) if (pixels[i].a > 220) { queue.Enqueue(i); visited[i] = true; }
            if (queue.Count == 0 || queue.Count == pixels.Length) throw new InvalidOperationException("Prepared RGBA requires an opaque interior and a transparent exterior.");
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % w, y = i / w;
                if (x > 0) Fill(i - 1, i); if (x + 1 < w) Fill(i + 1, i);
                if (y > 0) Fill(i - w, i); if (y + 1 < h) Fill(i + w, i);
            }
            void Fill(int to, int from)
            {
                if (visited[to]) return; visited[to] = true;
                Color32 c = pixels[from]; c.a = original[to].a; pixels[to] = c; queue.Enqueue(to);
            }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { name = source.name + " (RGB padding copy)", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels); texture.Apply(false, false); return texture;
        }
        static float FiberReference(Texture2D texture)
        {
            double sum = 0; int count = 0;
            foreach (Color32 p in texture.GetPixels32()) if (p.a > 220) { Color c = ((Color)p).linear; sum += c.r * .2126 + c.g * .7152 + c.b * .0722; count++; }
            return count > 0 ? (float)(sum / count) : 1;
        }
        static string Scenes() => string.Join("|", Enumerable.Range(0, SceneManager.sceneCount).Select(i => { var s = SceneManager.GetSceneAt(i); return s.handle + ":" + s.path + ":" + s.isDirty; }));
        static void Save(string directory, string name, Texture2D frame, bool release = false)
        { try { File.WriteAllBytes(Path.Combine(directory, name), frame.EncodeToPNG()); } finally { if (release) Object.DestroyImmediate(frame); } }
        static void Grid(string directory, string name, IList<Texture2D> frames, int columns)
        {
            int size = LabPaintStudyRenderer.RenderSize, rows = (frames.Count + columns - 1) / columns;
            var grid = new Texture2D(size * columns, size * rows, TextureFormat.RGBA32, false, false);
            try { for (int i = 0; i < frames.Count; i++) grid.SetPixels32(i % columns * size, (rows - 1 - i / columns) * size, size, size, frames[i].GetPixels32()); grid.Apply(); Save(directory, name, grid); }
            finally { Object.DestroyImmediate(grid); }
        }
        [Serializable] class Report
        { public string unity, gpu, scope, checks; public bool scenePreserved, randomPreserved, configPreserved; public List<Entry> entries = new List<Entry>(); }
        [Serializable] class Entry
        { public string fixture, authorityBefore, authorityAfter; public int reHits, cells, paintCount, originalShapeTriangles, fittedTriangles, stickerLayers; public long generatedMeshBytes; }
    }
}
