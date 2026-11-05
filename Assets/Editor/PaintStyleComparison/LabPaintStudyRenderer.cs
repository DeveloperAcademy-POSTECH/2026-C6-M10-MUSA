using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace C6Lab.Experiments.Editor
{
    /// <summary>
    /// A disposable, isolated view of the saved gourd prefab and production paint shader.
    /// All materials and maps are private copies. No Lab scene or gameplay object is used.
    /// </summary>
    public sealed class LabPaintStudyRenderer : IDisposable
    {
        public const string PrefabPath = "Assets/Lab/Resources/GourdPaint/WhiteGourd.prefab";
        public const string ShaderPath = "Assets/Lab/Art/GourdPaint/LabGourdPaint.shader";
        public const string MaskDirectory = "Assets/Lab/Art/GourdPaint/Experiments/StyleComparison";
        public const int RenderSize = 512;
        public const int MapColumns = 1024;
        public const int MapRows = 512;
        public const uint Seed = 200u;

        private Scene previewScene;
        private GameObject instance;
        private Camera camera;
        private RenderTexture target;
        private Material paintMaterial;
        private Texture2D runtimeMapTexture;
        private readonly Dictionary<LabPaintStudyMap, Texture2D> mapTextures = new Dictionary<LabPaintStudyMap, Texture2D>();
        private readonly List<Material> materialCopies = new List<Material>();
        private bool disposed;
        private readonly bool frameSavedGourd;
        private readonly Action<Material> configureStudySurface;
        private Renderer body;

        public LabPaintStudyRenderer(bool frameSavedGourd = false, Shader studyShader = null,
            Action<Material> configureStudySurface = null, Action<GameObject> configureStudyInstance = null)
        {
            this.frameSavedGourd = frameSavedGourd;
            this.configureStudySurface = configureStudySurface;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("The paint comparison requires a real graphics device.");
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
                if (prefab == null || shader == null || !shader.isSupported)
                    throw new InvalidOperationException("Saved gourd prefab or supported production shader is missing.");

                previewScene = EditorSceneManager.NewPreviewScene();
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, previewScene);
                instance.name = "Paint study — saved gourd copy";
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = 30;
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == null) continue;
                        Material copy = new Material(materials[i]) { hideFlags = HideFlags.HideAndDontSave };
                        materialCopies.Add(copy);
                        if (copy.shader == shader || copy.shader.name == "C6Lab/GourdPaint")
                        {
                            if (paintMaterial != null)
                                throw new InvalidOperationException("The saved prefab has more than one paint shell material.");
                            paintMaterial = copy;
                            body = renderer;
                            if (studyShader != null)
                            {
                                if (!studyShader.isSupported)
                                    throw new InvalidOperationException("The private study surface shader is not supported.");
                                paintMaterial.shader = studyShader;
                            }
                        }
                        materials[i] = copy;
                    }
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                if (paintMaterial == null)
                    throw new InvalidOperationException("The saved gourd's shell is not connected to the production paint shader.");

                GameObject cameraObject = EditorUtility.CreateGameObjectWithHideFlags(
                    "Paint study preview camera", HideFlags.HideAndDontSave, typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
                camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.cameraType = CameraType.Preview;
                camera.scene = previewScene;
                camera.orthographic = true;
                camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.075f, .095f, .115f, 1f);
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 20f;
                camera.allowHDR = false;
                camera.allowMSAA = false;

                GameObject lightObject = EditorUtility.CreateGameObjectWithHideFlags(
                    "Paint study stem light", HideFlags.HideAndDontSave, typeof(Light));
                SceneManager.MoveGameObjectToScene(lightObject, previewScene);
                Light light = lightObject.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1f;
                light.cullingMask = 1 << 30;
                lightObject.transform.rotation = Quaternion.Euler(35f, -35f, 0f);

                target = new RenderTexture(RenderSize, RenderSize, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB) { hideFlags = HideFlags.HideAndDontSave };
                if (!target.Create()) throw new InvalidOperationException("Could not create the paint comparison render target.");
                camera.targetTexture = target;
                // Editor-only attachment hook; default callers retain identical behavior.
                configureStudyInstance?.Invoke(instance);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>The caller owns the returned image. Rotation never touches the saved prefab.</summary>
        public Texture2D Render(LabPaintStudyMap map, bool grain, float yaw = 0f, float zoom = 1f)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LabPaintStudyRenderer));
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (!mapTextures.TryGetValue(map, out Texture2D mapTexture))
            {
                mapTexture = new Texture2D(map.Columns, map.Rows, TextureFormat.RGBA32, false, true)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapModeU = TextureWrapMode.Repeat,
                    wrapModeV = TextureWrapMode.Clamp
                };
                mapTexture.SetPixels32(map.Pixels);
                mapTexture.Apply(false, false);
                mapTextures.Add(map, mapTexture);
            }
            return RenderMap(mapTexture, grain, yaw, zoom);
        }

        /// <summary>Render genuine Host-rule bytes at their native resolution and Point sampling.</summary>
        public Texture2D RenderSurface(LabPaintSurface surface, bool grain, float yaw = 0f, float zoom = 1f, float pitch = 0f)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LabPaintStudyRenderer));
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            if (runtimeMapTexture == null || runtimeMapTexture.width != surface.Columns
                || runtimeMapTexture.height != surface.Rows)
            {
                if (runtimeMapTexture != null) Object.DestroyImmediate(runtimeMapTexture);
                runtimeMapTexture = new Texture2D(surface.Columns, surface.Rows, TextureFormat.RGBA32, false, true)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapModeU = TextureWrapMode.Repeat,
                    wrapModeV = TextureWrapMode.Clamp
                };
            }
            var pixels = new Color32[surface.CellCount];
            surface.CopyPixels(pixels);
            runtimeMapTexture.SetPixels32(pixels);
            runtimeMapTexture.Apply(false, false);
            return RenderMap(runtimeMapTexture, grain, yaw, zoom, pitch);
        }

        /// <summary>Caller owns the continuous visual texture; the preview never uploads it into a game surface.</summary>
        public Texture2D RenderVisual(Texture map, float yaw = 0f, float zoom = 1f, float pitch = 0f,
            LabWatercolorStampSettings stampSettings = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LabPaintStudyRenderer));
            if (map == null) throw new ArgumentNullException(nameof(map));
            return RenderMap(map, false, yaw, zoom, pitch, stampSettings);
        }

        private Texture2D RenderMap(Texture mapTexture, bool grain, float yaw, float zoom, float pitch = 0f,
            LabWatercolorStampSettings stampSettings = null)
        {
            // These are private material copies. V2 grain is deliberately applied by the
            // production shell shader, after the accumulated map has been reconstructed.
            paintMaterial.SetFloat("_WatercolorV2Enabled", stampSettings != null && stampSettings.enabled && !stampSettings.UsesPaperStickers ? 1f : 0f);
            paintMaterial.SetFloat("_PaperStickerEnabled", stampSettings != null && stampSettings.enabled && stampSettings.UsesPaperStickers ? 1f : 0f);
            paintMaterial.SetFloat("_PaperStickerGrainStrength", stampSettings?.paperSticker?.grainStrength ?? 0f);
            paintMaterial.SetFloat("_WatercolorGranulationStrength", stampSettings?.granulationStrength ?? 0f);
            paintMaterial.SetFloat("_WatercolorGranulationScale", stampSettings?.granulationScale ?? 120f);
            paintMaterial.SetTexture("_PaintMap", mapTexture);
            paintMaterial.SetFloat("_PaintMapReady", 1f);
            paintMaterial.SetTexture("_PainterMap", Texture2D.blackTexture);
            paintMaterial.SetFloat("_PlayerPaintPatternsEnabled", 0f);
            paintMaterial.SetFloat("_PaintGrainEnabled", grain ? 1f : 0f);
            paintMaterial.SetFloat("_PaintGrainSize", 1f);
            paintMaterial.SetFloat("_PaintGrainStrength", .3f);
            paintMaterial.SetFloat("_PaintMottleStrength", .18f);
            configureStudySurface?.Invoke(paintMaterial);
            instance.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            camera.transform.SetPositionAndRotation(new Vector3(0f, .035f, 4f), Quaternion.Euler(0f, 180f, 0f));
            camera.orthographicSize = 1.08f / Mathf.Clamp(zoom, .65f, 3f);
            if (frameSavedGourd)
            {
                // New study only: pivot/scale come from the actual saved prefab, rather than
                // assuming its shell is a unit sphere at the prefab root.
                instance.transform.position -= body.bounds.center;
                Bounds bounds = body.bounds;
                foreach (Renderer part in instance.GetComponentsInChildren<Renderer>())
                    if (part.enabled) bounds.Encapsulate(part.bounds);
                camera.transform.position = bounds.center + Vector3.forward * 4f;
                camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.12f / Mathf.Clamp(zoom, .65f, 3f);
            }
            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(RenderSize, RenderSize, TextureFormat.RGBA32, false, false)
                    { hideFlags = HideFlags.HideAndDontSave };
                image.ReadPixels(new Rect(0, 0, RenderSize, RenderSize), 0, 0);
                image.Apply(false, false);
                return image;
            }
            catch
            {
                if (image != null) Object.DestroyImmediate(image);
                throw;
            }
            finally { RenderTexture.active = previous; }
        }

        /// <summary>
        /// Captures the already configured private study through the production PNG
        /// clone. Unlike the V2 carrier test below, this keeps every study property.
        /// </summary>
        public Texture2D CaptureStudyPng(Texture map, LabWatercolorStampSettings settings,
            float yaw = 0f, float pitch = 0f, float zoom = 1f)
        {
            Texture2D framing = RenderVisual(map, yaw, zoom, pitch, settings);
            Object.DestroyImmediate(framing);
            string before = EditorJsonUtility.ToJson(paintMaterial);
            Material[] materials = body.sharedMaterials;
            Texture2D image = Hapio.EditorTools.GourdPngCapture.Capture(instance, camera, RenderSize);
            if (EditorJsonUtility.ToJson(paintMaterial) != before || !materials.SequenceEqual(body.sharedMaterials))
            {
                Object.DestroyImmediate(image);
                throw new InvalidOperationException("Production PNG capture changed the private study material.");
            }
            return image;
        }

        /// <summary>
        /// Exercises the production transparent PNG clone with V2 values first on the
        /// source material, then only in its per-material-slot property block. Pixel
        /// equality proves that the export path carries the surface-grain overrides.
        /// The caller owns the image; no source material or block is left changed.
        /// </summary>
        public Texture2D CaptureProductionPng(Texture map, LabWatercolorStampSettings settings, string diagnosticDirectory = null)
        {
            if (settings == null || !settings.enabled) throw new ArgumentException("An enabled watercolor profile is required.", nameof(settings));
            Texture2D framing = RenderVisual(map, 0f, 1f, 0f, settings);
            Object.DestroyImmediate(framing);
            Material[] sourceMaterials = body.sharedMaterials;
            var originalGlobal = new MaterialPropertyBlock(); body.GetPropertyBlock(originalGlobal);
            var originalSlots = new MaterialPropertyBlock[sourceMaterials.Length];
            for (int i = 0; i < originalSlots.Length; i++)
            { originalSlots[i] = new MaterialPropertyBlock(); body.GetPropertyBlock(originalSlots[i], i); }
            float enabled = paintMaterial.GetFloat("_WatercolorV2Enabled");
            float strength = paintMaterial.GetFloat("_WatercolorGranulationStrength");
            float scale = paintMaterial.GetFloat("_WatercolorGranulationScale");
            string materialState = EditorJsonUtility.ToJson(paintMaterial);
            Texture2D expected = null, warmupControl = null, disabledControl = null, actual = null;
            try
            {
                // The first production capture can differ from its immediately
                // repeated unchanged input. Retain that observed difference without
                // assigning a cause; compare subsequent material/slot carriers exactly.
                warmupControl = Hapio.EditorTools.GourdPngCapture.Capture(instance, camera, RenderSize);
                expected = Hapio.EditorTools.GourdPngCapture.Capture(instance, camera, RenderSize);
                if (EditorJsonUtility.ToJson(paintMaterial) != materialState || !sourceMaterials.SequenceEqual(body.sharedMaterials))
                    throw new InvalidOperationException("Production PNG capture changed the source material.");
                // Deliberately neutral material. The PNG can match the first image only
                // if the production clone preserves these per-slot V2 overrides.
                paintMaterial.SetFloat("_WatercolorV2Enabled", 0f);
            paintMaterial.SetFloat("_WatercolorGranulationStrength", 0f);
                disabledControl = Hapio.EditorTools.GourdPngCapture.Capture(instance, camera, RenderSize);
                for (int i = 0; i < sourceMaterials.Length; i++)
                {
                    if (sourceMaterials[i] != paintMaterial) continue;
                    var block = new MaterialPropertyBlock(); body.GetPropertyBlock(block, i);
                    block.SetFloat("_WatercolorV2Enabled", enabled);
                    block.SetFloat("_WatercolorGranulationStrength", strength);
                    block.SetFloat("_WatercolorGranulationScale", scale);
                    block.SetTexture("_PaintMap", map);
                    block.SetFloat("_PaintMapReady", 1f);
                    body.SetPropertyBlock(block, i);
                }
                string slottedMaterialState = EditorJsonUtility.ToJson(paintMaterial);
                actual = Hapio.EditorTools.GourdPngCapture.Capture(instance, camera, RenderSize);
                Color32[] a = expected.GetPixels32(), b = actual.GetPixels32();
                string diagnostics = "WARMED_MATERIAL_VS_SLOT " + PixelDifference(expected, actual)
                    + "\nINITIAL_CAPTURE_VS_IDENTICAL_REPEAT " + PixelDifference(warmupControl, expected)
                    + "\nV2_ENABLED_VS_DISABLED " + PixelDifference(expected, disabledControl)
                    + "\nglobalBlockEmpty=" + originalGlobal.isEmpty + " materialV2=" + enabled
                    + "/" + strength + "/" + scale + " materialSlots=" + sourceMaterials.Length
                    + "\nInitial-capture difference is recorded as observed; its cause is not established by this check.";
                if (!string.IsNullOrEmpty(diagnosticDirectory))
                {
                    Directory.CreateDirectory(diagnosticDirectory);
                    File.WriteAllBytes(Path.Combine(diagnosticDirectory, "png-clone-expected-material.png"), expected.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(diagnosticDirectory, "png-clone-actual-slot.png"), actual.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(diagnosticDirectory, "png-clone-initial-capture.png"), warmupControl.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(diagnosticDirectory, "png-clone-disabled-control.png"), disabledControl.EncodeToPNG());
                    File.WriteAllText(Path.Combine(diagnosticDirectory, "png-clone-diagnostic.txt"), diagnostics
                        + "\nEXPECTED MATERIAL\n" + materialState + "\nNEUTRAL MATERIAL\n" + slottedMaterialState);
                }
                if (!a.SequenceEqual(b))
                    throw new InvalidOperationException("Production PNG clone did not preserve V2 per-slot property-block rendering: " + diagnostics);
                if (EditorJsonUtility.ToJson(paintMaterial) != slottedMaterialState || !sourceMaterials.SequenceEqual(body.sharedMaterials))
                    throw new InvalidOperationException("Production PNG clone modified its source materials.");
                var observed = new MaterialPropertyBlock(); body.GetPropertyBlock(observed);
                if (observed.isEmpty != originalGlobal.isEmpty)
                    throw new InvalidOperationException("Production PNG clone modified its source global property block.");
                for (int i = 0; i < sourceMaterials.Length; i++)
                {
                    if (sourceMaterials[i] != paintMaterial) continue;
                    body.GetPropertyBlock(observed, i);
                    if (observed.GetFloat("_WatercolorV2Enabled") != enabled
                        || observed.GetFloat("_WatercolorGranulationStrength") != strength
                        || observed.GetFloat("_WatercolorGranulationScale") != scale
                        || observed.GetFloat("_PaintMapReady") != 1f || observed.GetTexture("_PaintMap") != map)
                        throw new InvalidOperationException("Production PNG clone modified its source V2 property-block overrides.");
                }
                Texture2D result = actual; actual = null; return result;
            }
            finally
            {
                paintMaterial.SetFloat("_WatercolorV2Enabled", enabled);
            paintMaterial.SetFloat("_WatercolorGranulationStrength", strength);
                paintMaterial.SetFloat("_WatercolorGranulationScale", scale);
                body.SetPropertyBlock(originalGlobal.isEmpty ? null : originalGlobal);
                for (int i = 0; i < originalSlots.Length; i++) body.SetPropertyBlock(originalSlots[i].isEmpty ? null : originalSlots[i], i);
                if (expected != null) Object.DestroyImmediate(expected);
                if (warmupControl != null) Object.DestroyImmediate(warmupControl);
                if (disabledControl != null) Object.DestroyImmediate(disabledControl);
                if (actual != null) Object.DestroyImmediate(actual);
            }
        }

        private static string PixelDifference(Texture2D expected, Texture2D actual)
        {
            Color32[] a = expected.GetPixels32(), b = actual.GetPixels32();
            int changed = 0, maxDelta = 0, maxAlpha = 0, alphaChanged = 0, first = -1;
            long totalDelta = 0;
            for (int i = 0; i < a.Length; i++)
            {
                int dr = Math.Abs(a[i].r - b[i].r), dg = Math.Abs(a[i].g - b[i].g);
                int db = Math.Abs(a[i].b - b[i].b), da = Math.Abs(a[i].a - b[i].a);
                int delta = Math.Max(Math.Max(dr, dg), Math.Max(db, da));
                if (delta > 0) { changed++; if (first < 0) first = i; }
                if (da > 0) alphaChanged++;
                maxAlpha = Math.Max(maxAlpha, da); maxDelta = Math.Max(maxDelta, delta); totalDelta += dr + dg + db + da;
            }
            return "changed=" + changed + "/" + a.Length + " maxChannelDelta=" + maxDelta
                + " totalChannelDelta=" + totalDelta + " alphaChanged=" + alphaChanged + " maxAlphaDelta=" + maxAlpha
                + (first < 0 ? " firstPixel=none" : " firstPixel=" + first % expected.width + "," + first / expected.width
                    + " expected=" + a[first] + " actual=" + b[first]);
        }

        public static LabPaintStudyMask LoadMask(LabPaintStudyStyle style)
        {
            string path = Path.Combine(MaskDirectory, style + ".png");
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false, true)
                { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                if (!image.LoadImage(File.ReadAllBytes(path), false))
                    throw new InvalidOperationException("Could not decode paint mask " + path);
                return new LabPaintStudyMask(image.width, image.height, image.GetPixels32());
            }
            finally { Object.DestroyImmediate(image); }
        }

        public static Texture2D Combine(Texture2D[] images)
        {
            if (images == null || images.Length == 0 || images[0] == null)
                throw new ArgumentException("At least one paint style image is required.", nameof(images));
            int width = images[0].width, height = images[0].height;
            var pixels = new Color32[width * height * images.Length];
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null || images[i].width != width || images[i].height != height)
                    throw new ArgumentException("Comparison images must have identical sizes.", nameof(images));
                Color32[] source = images[i].GetPixels32();
                for (int y = 0; y < height; y++)
                    Array.Copy(source, y * width, pixels, y * width * images.Length + i * width, width);
            }
            var combined = new Texture2D(width * images.Length, height, TextureFormat.RGBA32, false, false)
                { hideFlags = HideFlags.HideAndDontSave };
            combined.SetPixels32(pixels);
            combined.Apply(false, false);
            return combined;
        }

        public void ClearMapCache()
        {
            if (paintMaterial != null) paintMaterial.SetTexture("_PaintMap", null);
            foreach (Texture2D texture in mapTextures.Values)
                if (texture != null) Object.DestroyImmediate(texture);
            mapTextures.Clear();
            if (runtimeMapTexture != null) Object.DestroyImmediate(runtimeMapTexture);
            runtimeMapTexture = null;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (camera != null) camera.targetTexture = null;
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            ClearMapCache();
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
            foreach (Material material in materialCopies)
                if (material != null) Object.DestroyImmediate(material);
            materialCopies.Clear();
            target = null;
            camera = null;
            instance = null;
            paintMaterial = null;
        }
    }
}
