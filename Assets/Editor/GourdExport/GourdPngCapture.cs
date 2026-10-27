using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hapio.EditorTools
{
    /// <summary>
    /// Synchronous, render-only snapshot. Does not instantiate gameplay scripts or change a live camera.
    /// The caller owns the returned RGBA image. The live textures are read only for this single render.
    /// </summary>
    public static class GourdPngCapture
    {
        public static Texture2D Capture(GameObject visualRoot, Camera viewCamera, int size = CurrentGourdPngExport.Resolution)
        {
            if (visualRoot == null || !visualRoot.activeInHierarchy || EditorUtility.IsPersistent(visualRoot))
                throw new ArgumentException("An active scene gourd visual is required.", nameof(visualRoot));
            if (size < 1 || size > SystemInfo.maxTextureSize)
                throw new ArgumentOutOfRangeException(nameof(size), "The graphics device does not support the requested image size.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("PNG export requires an Editor with a graphics device.");
            using (var capture = new CaptureRig(visualRoot, viewCamera, size))
                return capture.Render();
        }

        private sealed class CaptureRig : IDisposable
        {
            private readonly List<Material> materials = new List<Material>();
            private readonly List<Mesh> bakedMeshes = new List<Mesh>();
            private readonly Dictionary<Material, Material> materialCopies = new Dictionary<Material, Material>();
            private readonly List<Renderer> renderers = new List<Renderer>();
            private Scene scene;
            private Camera camera;
            private RenderTexture target;
            private RenderTexture resolved;
            private readonly int size;
            private int modelMask;

            public CaptureRig(GameObject source, Camera reference, int size)
            {
                this.size = size;
                try
                {
                    scene = EditorSceneManager.NewPreviewScene();
                    CopyHierarchy(source.transform, null);
                    if (renderers.Count == 0) throw new InvalidOperationException("The gourd contains no visible mesh renderers.");
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    if (!Finite(bounds.center) || !Finite(bounds.extents) || bounds.extents.sqrMagnitude < .000001f)
                        throw new InvalidOperationException("The gourd mesh bounds are invalid.");

                    camera = NewObject("Gourd PNG camera").AddComponent<Camera>();
                    camera.enabled = false;
                    camera.scene = scene;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = Color.clear;
                    camera.cullingMask = modelMask;
                    camera.aspect = 1f;
                    camera.allowHDR = false;
                    camera.allowMSAA = true;
                    camera.useOcclusionCulling = false;
                    camera.forceIntoRenderTexture = true;
                    ConfigureCamera(bounds, reference);
                    UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                    data.renderType = CameraRenderType.Base;
                    data.renderPostProcessing = false;
                    data.renderShadows = false;
                    data.requiresColorOption = CameraOverrideOption.Off;
                    data.requiresDepthOption = CameraOverrideOption.Off;
                    data.antialiasing = AntialiasingMode.None;
                    data.volumeLayerMask = 0;
                    data.allowXRRendering = false;
                    CopyLights(source.scene);

                    var descriptor = new RenderTextureDescriptor(size, size, RenderTextureFormat.ARGB32, 24)
                    {
                        msaaSamples = 4,
                        sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
                        useMipMap = false,
                        autoGenerateMips = false,
                        useDynamicScale = false
                    };
                    descriptor.msaaSamples = SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
                    target = new RenderTexture(descriptor) { name = "Gourd PNG 4096 RGBA", hideFlags = HideFlags.HideAndDontSave };
                    if (!target.Create()) throw new InvalidOperationException("Could not allocate the PNG render texture.");
                    camera.targetTexture = target;
                    if (target.antiAliasing > 1)
                    {
                        descriptor.msaaSamples = 1;
                        descriptor.depthBufferBits = 0;
                        resolved = new RenderTexture(descriptor) { name = "Gourd PNG resolved RGBA", hideFlags = HideFlags.HideAndDontSave };
                        if (!resolved.Create()) throw new InvalidOperationException("Could not allocate the PNG resolve texture.");
                    }
                }
                catch { Dispose(); throw; }
            }

            private GameObject NewObject(string name)
            {
                GameObject value = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(value, scene);
                return value;
            }

            private void CopyHierarchy(Transform original, Transform parent)
            {
                if (!original.gameObject.activeInHierarchy) return;
                GameObject copy = NewObject(original.name);
                copy.layer = original.gameObject.layer;
                if (parent == null)
                {
                    copy.transform.SetPositionAndRotation(original.position, original.rotation);
                    copy.transform.localScale = original.lossyScale;
                }
                else
                {
                    copy.transform.SetParent(parent, false);
                    copy.transform.localPosition = original.localPosition;
                    copy.transform.localRotation = original.localRotation;
                    copy.transform.localScale = original.localScale;
                }

                MeshRenderer meshRenderer = original.GetComponent<MeshRenderer>();
                MeshFilter meshFilter = original.GetComponent<MeshFilter>();
                if (meshRenderer != null && meshRenderer.enabled && !meshRenderer.forceRenderingOff && meshFilter != null && meshFilter.sharedMesh != null)
                    CopyRenderer(meshRenderer, meshFilter.sharedMesh, copy);
                SkinnedMeshRenderer skinned = original.GetComponent<SkinnedMeshRenderer>();
                if (skinned != null && skinned.enabled && !skinned.forceRenderingOff && skinned.sharedMesh != null)
                {
                    Mesh baked = new Mesh { name = skinned.name + " (PNG pose)", hideFlags = HideFlags.HideAndDontSave };
                    bakedMeshes.Add(baked);
                    skinned.BakeMesh(baked);
                    CopyRenderer(skinned, baked, copy);
                }
                for (int i = 0; i < original.childCount; i++) CopyHierarchy(original.GetChild(i), copy.transform);
            }

            private void CopyRenderer(Renderer original, Mesh mesh, GameObject copy)
            {
                copy.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = copy.AddComponent<MeshRenderer>();
                Material[] originals = original.sharedMaterials;
                var copies = new Material[originals.Length];
                for (int i = 0; i < originals.Length; i++)
                {
                    Material originalMaterial = originals[i];
                    if (originalMaterial == null) continue;
                    if (!materialCopies.TryGetValue(originalMaterial, out Material material))
                    {
                        material = new Material(originalMaterial) { name = originalMaterial.name + " (PNG copy)", hideFlags = HideFlags.HideAndDontSave };
                        materialCopies.Add(originalMaterial, material);
                        materials.Add(material);
                    }
                    copies[i] = material;
                }
                renderer.sharedMaterials = copies;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.renderingLayerMask = original.renderingLayerMask;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                var block = new MaterialPropertyBlock();
                original.GetPropertyBlock(block);
                renderer.SetPropertyBlock(block);
                for (int i = 0; i < copies.Length; i++)
                {
                    original.GetPropertyBlock(block, i);
                    if (!block.isEmpty) renderer.SetPropertyBlock(block, i);
                }
                renderers.Add(renderer);
                modelMask |= 1 << copy.layer;
            }

            private void ConfigureCamera(Bounds bounds, Camera reference)
            {
                Quaternion rotation = reference != null ? reference.transform.rotation : Quaternion.Euler(0f, 180f, 0f);
                camera.transform.rotation = rotation;
                camera.orthographic = reference != null && reference.orthographic;
                camera.fieldOfView = reference != null ? Mathf.Clamp(reference.fieldOfView, 10f, 100f) : 35f;
                float tanHalf = Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
                float distance = 0f;
                float ortho = 0f;
                float radius = bounds.extents.magnitude;
                camera.nearClipPlane = Mathf.Max(.001f, radius * .001f);
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 offset = new Vector3((corner & 1) == 0 ? -bounds.extents.x : bounds.extents.x,
                        (corner & 2) == 0 ? -bounds.extents.y : bounds.extents.y,
                        (corner & 4) == 0 ? -bounds.extents.z : bounds.extents.z);
                    Vector3 local = Quaternion.Inverse(rotation) * offset;
                    float extent = Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.y)) * 1.1f;
                    distance = Mathf.Max(distance, extent / tanHalf - local.z, camera.nearClipPlane - local.z);
                    ortho = Mathf.Max(ortho, extent);
                }
                if (camera.orthographic) distance = radius * 3f + 1f;
                camera.orthographicSize = ortho;
                camera.transform.position = bounds.center - camera.transform.forward * distance;
                camera.farClipPlane = distance + radius * 3f + 1f;
            }

            private void CopyLights(Scene sourceScene)
            {
                int count = 0;
                foreach (GameObject root in sourceScene.GetRootGameObjects())
                foreach (Light original in root.GetComponentsInChildren<Light>())
                {
                    if (!original.isActiveAndEnabled || (original.cullingMask & modelMask) == 0) continue;
                    Light light = NewObject(original.name + " (PNG light)").AddComponent<Light>();
                    EditorUtility.CopySerialized(original, light);
                    light.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
                    light.transform.localScale = original.transform.lossyScale;
                    light.shadows = LightShadows.None;
                    count++;
                }
                if (count == 0)
                {
                    Light light = NewObject("Gourd PNG fallback key").AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.intensity = 1f;
                    light.cullingMask = modelMask;
                    light.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
                }
            }

            public Texture2D Render()
            {
                RenderTexture previous = RenderTexture.active;
                Texture2D image = null;
                try
                {
                    if (GraphicsSettings.currentRenderPipeline == null) camera.Render();
                    else
                    {
                        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                        if (!RenderPipeline.SupportsRenderRequest(camera, request))
                            throw new InvalidOperationException("The active render pipeline does not support the gourd capture request.");
                        RenderPipeline.SubmitRenderRequest(camera, request);
                    }
                    if (resolved != null) Graphics.Blit(target, resolved);
                    RenderTexture.active = resolved != null ? resolved : target;
                    image = new Texture2D(size, size, TextureFormat.RGBA32, false, false) { name = "Gourd PNG export", hideFlags = HideFlags.HideAndDontSave };
                    image.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
                    image.Apply(false, false);
                    Color32[] pixels = image.GetPixels32();
                    if (pixels[0].a != 0 || pixels[size - 1].a != 0 || pixels[pixels.Length - size].a != 0 || pixels[pixels.Length - 1].a != 0)
                        throw new InvalidOperationException("The render pipeline did not preserve the transparent background. No PNG was written.");
                    bool visible = false;
                    bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
                    // MSAA on a clear background yields premultiplied edge RGB. PNG needs straight alpha.
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        byte alpha = pixels[i].a;
                        if (alpha == 0) { pixels[i] = default; continue; }
                        visible = true;
                        if (alpha == 255) continue;
                        Color color = pixels[i];
                        if (linear) color = color.linear;
                        float inverse = 255f / alpha;
                        color.r *= inverse; color.g *= inverse; color.b *= inverse;
                        if (linear) color = color.gamma;
                        color.a = alpha / 255f;
                        pixels[i] = color;
                    }
                    if (!visible) throw new InvalidOperationException("The capture contains no visible gourd pixels. No PNG was written.");
                    image.SetPixels32(pixels);
                    image.Apply(false, false);
                    return image;
                }
                catch { if (image != null) Object.DestroyImmediate(image); throw; }
                finally { RenderTexture.active = previous; }
            }

            public void Dispose()
            {
                if (camera != null) camera.targetTexture = null;
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                scene = default;
                foreach (Material material in materials) if (material != null) Object.DestroyImmediate(material);
                foreach (Mesh mesh in bakedMeshes) if (mesh != null) Object.DestroyImmediate(mesh);
                Release(target); Release(resolved);
                target = null; resolved = null;
            }

            private static void Release(RenderTexture texture)
            {
                if (texture == null) return;
                texture.Release();
                Object.DestroyImmediate(texture);
            }

            private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }
}
