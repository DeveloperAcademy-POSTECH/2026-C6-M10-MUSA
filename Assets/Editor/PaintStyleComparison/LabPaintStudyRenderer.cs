using System;
using System.Collections.Generic;
using System.IO;
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

        public LabPaintStudyRenderer()
        {
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
        public Texture2D RenderSurface(LabPaintSurface surface, bool grain, float yaw = 0f, float zoom = 1f)
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
            return RenderMap(runtimeMapTexture, grain, yaw, zoom);
        }

        private Texture2D RenderMap(Texture2D mapTexture, bool grain, float yaw, float zoom)
        {
            paintMaterial.SetTexture("_PaintMap", mapTexture);
            paintMaterial.SetFloat("_PaintMapReady", 1f);
            paintMaterial.SetFloat("_PaintGrainEnabled", grain ? 1f : 0f);
            paintMaterial.SetFloat("_PaintGrainSize", 1f);
            paintMaterial.SetFloat("_PaintGrainStrength", .3f);
            paintMaterial.SetFloat("_PaintMottleStrength", .18f);
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            camera.transform.SetPositionAndRotation(new Vector3(0f, .035f, 4f), Quaternion.Euler(0f, 180f, 0f));
            camera.orthographicSize = 1.08f / Mathf.Clamp(zoom, .65f, 1.55f);
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
