using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Hapio.Experiments
{
    /// <summary>Owned presentation only: imported textures remain owned by the importer.</summary>
    public sealed class ClayPreviewImagePresentation : IDisposable
    {
        public const string BackgroundShaderName = "C6Lab/Experiments/ClayPreviewBackground";
        static readonly int TextureEnabledId = Shader.PropertyToID("_ClayPreviewTextureEnabled");
        static readonly int TextureId = Shader.PropertyToID("_ClayPreviewTexture");
        static readonly int TextureScaleId = Shader.PropertyToID("_ClayPreviewTextureScale");
        static readonly int TextureColorId = Shader.PropertyToID("_ClayPreviewTextureColorStrength");
        static readonly int TextureNormalId = Shader.PropertyToID("_ClayPreviewTextureNormalStrength");
        static readonly int BackgroundTextureId = Shader.PropertyToID("_MainTex");
        static readonly int BackgroundUvId = Shader.PropertyToID("_UvScaleOffset");
        readonly Transform ownerRoot;
        readonly Camera camera;
        readonly Shader backgroundShader;
        readonly Vector3[] vertices = new Vector3[4];
        GameObject quad;
        Mesh mesh;
        Material material;
        MeshRenderer renderer;
        Texture background;
        bool fill, disposed;

        public MeshRenderer BackgroundRenderer => renderer;
        public Texture BackgroundTexture => background;
        public bool Fill => fill;

        public ClayPreviewImagePresentation(Transform ownerRoot, Camera camera, Shader backgroundShader)
        {
            if (ownerRoot == null) throw new ArgumentNullException(nameof(ownerRoot));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (backgroundShader == null || backgroundShader.name != BackgroundShaderName || !backgroundShader.isSupported)
                throw new ArgumentException("The directly referenced preview background shader is missing or unsupported.", nameof(backgroundShader));
            this.ownerRoot = ownerRoot;
            this.camera = camera;
            this.backgroundShader = backgroundShader;
        }

        public static void ApplyStickerTexture(Material material, Texture texture, bool enabled,
            float scale = 1f, float colorStrength = .5f, float normalStrength = .2f)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (!Finite(scale) || !Finite(colorStrength) || !Finite(normalStrength))
                throw new ArgumentOutOfRangeException(nameof(scale), "Texture settings must be finite.");
            if (texture != null && texture.dimension != TextureDimension.Tex2D)
                throw new ArgumentException("The imported clay texture must be a two-dimensional image.", nameof(texture));
            foreach (int property in new[] { TextureEnabledId, TextureId, TextureScaleId, TextureColorId, TextureNormalId })
                if (!material.HasProperty(property)) throw new ArgumentException("The material does not support preview texture overrides.", nameof(material));
            // Validate every input first. A rejected setting cannot partially change a clone.
            material.SetTexture(TextureId, texture);
            material.SetFloat(TextureScaleId, Mathf.Clamp(scale, .25f, 12f));
            material.SetFloat(TextureColorId, Mathf.Clamp01(colorStrength));
            material.SetFloat(TextureNormalId, Mathf.Clamp01(normalStrength));
            material.SetFloat(TextureEnabledId, enabled && texture != null ? 1f : 0f);
        }

        public void SetBackground(Texture texture, bool fill)
        {
            ThrowIfDisposed();
            if (texture == null) { ClearBackground(); return; }
            if (texture.dimension != TextureDimension.Tex2D || texture.width < 1 || texture.height < 1)
                throw new ArgumentException("The background must be a valid two-dimensional image.", nameof(texture));
            ValidateCamera();
            EnsureObjects();
            background = texture;
            this.fill = fill;
            material.SetTexture(BackgroundTextureId, texture);
            renderer.enabled = true;
            UpdateLayout();
        }

        public void ClearBackground()
        {
            ThrowIfDisposed();
            background = null;
            if (renderer != null) renderer.enabled = false;
            if (material != null) material.SetTexture(BackgroundTextureId, null);
        }

        public void UpdateLayout()
        {
            ThrowIfDisposed();
            if (background == null || renderer == null) return;
            ValidateCamera();
            float viewportHeight = camera.orthographicSize * 2f;
            float viewportWidth = viewportHeight * camera.aspect;
            float imageAspect = background.width / (float)background.height;
            float width = viewportWidth, height = viewportHeight;
            Vector4 uv = new Vector4(1f, 1f, 0f, 0f);
            if (fill)
            {
                if (imageAspect > camera.aspect) uv.x = camera.aspect / imageAspect;
                else uv.y = imageAspect / camera.aspect;
                uv.z = (1f - uv.x) * .5f; uv.w = (1f - uv.y) * .5f;
            }
            else if (imageAspect > camera.aspect) height = width / imageAspect;
            else width = height * imageAspect;
            material.SetVector(BackgroundUvId, uv);
            float range = camera.farClipPlane - camera.nearClipPlane;
            float distance = camera.farClipPlane - Mathf.Max(.01f, range * .001f);
            distance = Mathf.Max(camera.nearClipPlane + range * .5f, distance);
            Transform view = camera.transform;
            Vector3 center = view.position + view.forward * distance;
            Vector3 right = view.right * (width * .5f), up = view.up * (height * .5f);
            // Vertices are camera-fixed in world space, converted into the owned root.
            // No model-pivot transform, rotation, zoom state or camera value is changed.
            vertices[0] = ownerRoot.InverseTransformPoint(center - right - up);
            vertices[1] = ownerRoot.InverseTransformPoint(center + right - up);
            vertices[2] = ownerRoot.InverseTransformPoint(center + right + up);
            vertices[3] = ownerRoot.InverseTransformPoint(center - right + up);
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        void ValidateCamera()
        {
            if (ownerRoot == null || camera == null) throw new InvalidOperationException("The preview presentation has been released.");
            if (!camera.orthographic || !Finite(camera.orthographicSize) || camera.orthographicSize <= 0f
                || !Finite(camera.aspect) || camera.aspect <= 0f || !Finite(camera.nearClipPlane)
                || !Finite(camera.farClipPlane) || camera.farClipPlane <= camera.nearClipPlane || camera.cullingMask == 0)
                throw new InvalidOperationException("A valid orthographic preview camera is required.");
        }

        void EnsureObjects()
        {
            if (quad != null) return;
            try
            {
                mesh = new Mesh { name = "Imported preview background quad", hideFlags = HideFlags.DontSave };
                mesh.MarkDynamic();
                mesh.vertices = vertices;
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                material = new Material(backgroundShader) { name = "Imported preview background material", hideFlags = HideFlags.DontSave };
                quad = new GameObject("Imported preview background") { hideFlags = HideFlags.DontSave };
                quad.transform.SetParent(ownerRoot, false);
                for (int layer = 0; layer < 32; layer++)
                    if ((camera.cullingMask & (1 << layer)) != 0) { quad.layer = layer; break; }
                quad.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = quad.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.enabled = false;
            }
            catch { ReleaseOwned(); throw; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ReleaseOwned();
        }

        void ReleaseOwned()
        {
            background = null;
            if (renderer != null) { renderer.enabled = false; renderer.sharedMaterial = null; }
            if (quad != null) DestroyOwned(quad);
            if (mesh != null) DestroyOwned(mesh);
            if (material != null) { material.SetTexture(BackgroundTextureId, null); DestroyOwned(material); }
            quad = null; mesh = null; material = null; renderer = null;
        }

        void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(ClayPreviewImagePresentation)); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void DestroyOwned(Object value) { if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
    }
}
