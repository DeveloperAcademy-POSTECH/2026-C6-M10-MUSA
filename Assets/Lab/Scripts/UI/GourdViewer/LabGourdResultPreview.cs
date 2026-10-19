using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>
    /// A local, disposable render-only copy of one committed Paint victory. It owns every
    /// texture and material it creates; shared meshes, the live target and game state are read-only.
    /// The independent camera renders through the normal pipeline on the next rendered frame.
    /// </summary>
    public sealed class LabGourdResultPreview : IDisposable
    {
        private const int PreviewLayer = 31;
        private const float FieldOfView = 35f;
        private const float DefaultZoom = .5f;
        private const float RotationDegreesPerView = 180f;
        private static readonly List<LabGourdResultPreview> ActivePreviews = new List<LabGourdResultPreview>();
        private static readonly int PaintMapId = Shader.PropertyToID("_PaintMap");
        private static readonly int PainterMapId = Shader.PropertyToID("_PainterMap");
        private static readonly int PaintReadyId = Shader.PropertyToID("_PaintMapReady");

        private readonly List<Material> ownedMaterials = new List<Material>();
        private GameObject root;
        private Transform modelPivot;
        private Camera previewCamera;
        private RenderTexture texture;
        private Texture2D paintTexture;
        private Texture2D painterTexture;
        private float zoomNormalized = DefaultZoom;
        private float fitDistance;
        private int previewSlot = -1;
        private bool disposed;

        public RenderTexture Texture => texture;
        public Texture2D PaintTexture => paintTexture;
        public Texture2D PainterTexture => painterTexture;
        public Quaternion ModelRotation => modelPivot != null ? modelPivot.localRotation : Quaternion.identity;
        public float ZoomNormalized => zoomNormalized;
        public bool IsDisposed => disposed;
        public string SessionId { get; private set; }
        public uint Round { get; private set; }
        public uint PaintRevision { get; private set; }
        public int PaintCellCount { get; private set; }
        public string PaintPixelHash { get; private set; }
        /// <summary>Read-only observation points for completed-result rendering/isolation tests.</summary>
        public Transform ModelRoot => modelPivot;
        public Camera PreviewCamera => previewCamera;

        private LabGourdResultPreview() { }

        public static bool TryCreate(LabNetwork network, out LabGourdResultPreview preview,
            out string error, int size = 512)
            => TryCreate(network, out preview, out error, size, size);

        public static bool TryCreate(LabNetwork network, out LabGourdResultPreview preview,
            out string error, int textureWidth, int textureHeight)
        {
            LabGourdPaint source = network != null && network.target != null
                ? network.target.GetComponent<LabGourdPaint>() : null;
            return TryCreate(network, source, textureWidth, textureHeight, out preview, out error);
        }

        public static bool TryCreate(LabNetwork network, LabGourdPaint source, int textureWidth,
            int textureHeight, out LabGourdResultPreview preview, out string error)
        {
            preview = null;
            if (!SurfaceMatches(network, out error)) return false;
            if (source == null || source.network != network || source.target != network.target
                || !source.IsReady || source.VisualInstance == null)
            {
                error = "The completed gourd presentation is not ready.";
                return false;
            }
            int maximumSize = SystemInfo.maxTextureSize;
            if (textureWidth < 1 || textureHeight < 1 || maximumSize < 1
                || textureWidth > maximumSize || textureHeight > maximumSize)
            {
                error = "The requested preview texture dimensions are unsupported.";
                return false;
            }

            var candidate = new LabGourdResultPreview();
            try
            {
                candidate.Build(network, source, textureWidth, textureHeight);
                preview = candidate;
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                candidate.Dispose();
                error = "Could not create the gourd preview: " + exception.Message;
                return false;
            }
        }

        /// <summary>Only an authoritative, complete map committed with its matching result can be viewed.</summary>
        public static bool SurfaceMatches(LabNetwork network, out string error)
        {
            if (network == null || !network.IsConnected || network.config == null
                || !network.config.CompletedGourdViewerEnabled || network.config.GourdMode != LabGourdMode.Paint)
            {
                error = "A connected Paint result with completed gourd viewing enabled is required.";
                return false;
            }
            LabSnapshot snapshot = network.Snapshot;
            LabPaintSurface surface = network.PaintSurface;
            if (snapshot == null || snapshot.phase != LabPhase.Victory || string.IsNullOrEmpty(snapshot.sessionId)
                || snapshot.round == 0 || snapshot.paintGoalPercent != 100 || snapshot.paintCellCount <= 0
                || snapshot.paintCoveredCount != snapshot.paintCellCount)
            {
                error = "The round has no completed Paint victory.";
                return false;
            }
            if (surface == null || snapshot.paintRevision != surface.Revision
                || snapshot.paintColumns != surface.Columns || snapshot.paintRows != surface.Rows
                || snapshot.paintCellCount != surface.CellCount || snapshot.paintCoveredCount != surface.CoveredCount
                || snapshot.paintCount != surface.PaintCount || !surface.IsComplete)
            {
                error = "The completed map and result have not been committed together.";
                return false;
            }
            error = null;
            return true;
        }

        private void Build(LabNetwork network, LabGourdPaint source, int width, int height)
        {
            LabSnapshot snapshot = network.Snapshot;
            LabPaintSurface surface = network.PaintSurface;
            var pixels = new Color32[surface.CellCount];
            surface.CopyPixels(pixels);
            foreach (Color32 pixel in pixels)
                if (pixel.a != 255) throw new InvalidOperationException("A completed session map must contain only opaque cells.");
            SessionId = snapshot.sessionId;
            Round = snapshot.round;
            PaintRevision = snapshot.paintRevision;
            PaintCellCount = snapshot.paintCellCount;
            PaintPixelHash = HashPixels(pixels);
            paintTexture = new Texture2D(surface.Columns, surface.Rows, TextureFormat.RGBA32, false, true)
            {
                name = "Completed gourd paint copy", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp
            };
            paintTexture.SetPixels32(pixels);
            paintTexture.Apply(false, false);
            var painterSlots = new byte[surface.CellCount];
            var painterPixels = new Color32[surface.CellCount];
            surface.CopyPainterSlots(painterSlots);
            for (int i = 0; i < painterSlots.Length; i++)
                painterPixels[i] = new Color32(painterSlots[i], 0, 0, 255);
            painterTexture = new Texture2D(surface.Columns, surface.Rows, TextureFormat.RGBA32, false, true)
            {
                name = "Completed gourd painter copy", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp
            };
            painterTexture.SetPixels32(painterPixels);
            painterTexture.Apply(false, false);

            root = NewObject("Completed gourd preview", null);
            root.SetActive(false);
            modelPivot = NewObject("Local model rotation", root.transform).transform;
            Transform normalization = NewObject("Centered model", modelPivot).transform;
            var materialCopies = new Dictionary<Material, Material>();
            var renderers = new List<MeshRenderer>();
            int paintedRenderers = 0;
            CopyHierarchy(source.VisualInstance.transform, normalization, materialCopies, renderers, ref paintedRenderers);
            if (renderers.Count == 0 || paintedRenderers == 0)
                throw new InvalidOperationException("The completed presentation has no renderable paint mesh.");

            // Renderer bounds are unavailable for an inactive hierarchy. Mesh-local bounds provide
            // the same conservative enclosure without enabling any temporary world objects.
            Bounds bounds = MeshBounds(renderers);
            float radius = bounds.extents.magnitude;
            if (!Finite(radius) || radius <= .00001f || !Finite(bounds.center))
                throw new InvalidOperationException("The completed mesh has invalid bounds.");
            normalization.localScale = Vector3.one / radius;
            normalization.localPosition = -bounds.center / radius;

            // Existing Scene cameras clip at 50/12 units. Each preview is also separated from
            // every other preview, so neither the live cameras nor another viewer sees this copy.
            AcquirePreviewSlot();
            previewCamera = NewObject("Local result camera", root.transform).AddComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(.08f, .09f, .11f, 1f);
            previewCamera.orthographic = false;
            previewCamera.fieldOfView = FieldOfView;
            previewCamera.aspect = width / (float)height;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = false;
            previewCamera.useOcclusionCulling = false;
            previewCamera.depth = -100f;
            float verticalHalf = FieldOfView * .5f * Mathf.Deg2Rad;
            float horizontalHalf = Mathf.Atan(Mathf.Tan(verticalHalf) * previewCamera.aspect);
            fitDistance = 1.12f / Mathf.Sin(Mathf.Min(verticalHalf, horizontalHalf));
            previewCamera.nearClipPlane = .03f;
            previewCamera.farClipPlane = fitDistance * 2f + 3f;
            texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Completed gourd view", hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 1, useMipMap = false, autoGenerateMips = false,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            if (!texture.Create()) throw new InvalidOperationException("The preview render texture could not be allocated.");
            previewCamera.targetTexture = texture;
            ResetView();
            // The existing paint shader has a fixed world-space key light toward +Z.
            // Turn the staging rig, camera and model together so a single-color result still
            // shows its spherical shading. Relative framing and local gesture axes stay unchanged.
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            root.SetActive(true);
            previewCamera.enabled = true;
        }

        private void CopyHierarchy(Transform source, Transform parent, Dictionary<Material, Material> materialCopies,
            List<MeshRenderer> renderers, ref int paintedRenderers)
        {
            GameObject copy = NewObject(source.name, parent);
            copy.transform.localPosition = source.localPosition;
            copy.transform.localRotation = source.localRotation;
            copy.transform.localScale = source.localScale;
            copy.SetActive(source.gameObject.activeSelf);
            MeshFilter originalMesh = source.GetComponent<MeshFilter>();
            MeshRenderer originalRenderer = source.GetComponent<MeshRenderer>();
            if (originalMesh != null && originalMesh.sharedMesh != null && originalRenderer != null && originalRenderer.enabled)
            {
                copy.AddComponent<MeshFilter>().sharedMesh = originalMesh.sharedMesh;
                MeshRenderer renderer = copy.AddComponent<MeshRenderer>();
                Material[] originals = originalRenderer.sharedMaterials;
                var materials = new Material[originals.Length];
                bool painted = false;
                for (int i = 0; i < originals.Length; i++)
                {
                    Material original = originals[i];
                    if (original == null) continue;
                    if (!materialCopies.TryGetValue(original, out Material material))
                    {
                        material = new Material(original)
                        {
                            name = original.name + " (result copy)", hideFlags = HideFlags.HideAndDontSave
                        };
                        materialCopies.Add(original, material);
                        ownedMaterials.Add(material);
                    }
                    materials[i] = material;
                    if (material.HasProperty(PaintMapId) && material.HasProperty(PaintReadyId)) painted = true;
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.allowOcclusionWhenDynamic = false;
                var block = new MaterialPropertyBlock();
                originalRenderer.GetPropertyBlock(block);
                if (painted)
                {
                    block.SetTexture(PaintMapId, paintTexture);
                    block.SetTexture(PainterMapId, painterTexture);
                    block.SetFloat(PaintReadyId, 1f);
                    paintedRenderers++;
                }
                renderer.SetPropertyBlock(block);
                // Preserve any per-submesh overrides without allowing their old paint texture
                // to override the independent copy when the live round is later cleared.
                for (int i = 0; i < materials.Length; i++)
                {
                    originalRenderer.GetPropertyBlock(block, i);
                    if (block.isEmpty) continue;
                    if (materials[i] != null && materials[i].HasProperty(PaintMapId)
                        && materials[i].HasProperty(PaintReadyId))
                    {
                        block.SetTexture(PaintMapId, paintTexture);
                        block.SetTexture(PainterMapId, painterTexture);
                        block.SetFloat(PaintReadyId, 1f);
                    }
                    renderer.SetPropertyBlock(block, i);
                }
                renderers.Add(renderer);
            }
            for (int i = 0; i < source.childCount; i++)
                CopyHierarchy(source.GetChild(i), copy.transform, materialCopies, renderers, ref paintedRenderers);
        }

        private Bounds MeshBounds(List<MeshRenderer> renderers)
        {
            bool any = false;
            Bounds result = default;
            foreach (MeshRenderer renderer in renderers)
            {
                // activeInHierarchy is false while assembling; only the source's local active
                // chain determines whether this renderer belongs in the initial framing.
                bool visible = true;
                for (Transform item = renderer.transform; item != root.transform; item = item.parent)
                    if (!item.gameObject.activeSelf) { visible = false; break; }
                if (!visible) continue;
                Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Bounds local = mesh.bounds;
                Matrix4x4 toRoot = root.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var offset = new Vector3((corner & 1) == 0 ? -local.extents.x : local.extents.x,
                        (corner & 2) == 0 ? -local.extents.y : local.extents.y,
                        (corner & 4) == 0 ? -local.extents.z : local.extents.z);
                    Vector3 point = toRoot.MultiplyPoint3x4(local.center + offset);
                    if (!Finite(point)) throw new InvalidOperationException("The visual hierarchy contains invalid mesh transforms.");
                    if (any) result.Encapsulate(point);
                    else { result = new Bounds(point, Vector3.zero); any = true; }
                }
            }
            if (!any) throw new InvalidOperationException("The completed presentation has no active mesh.");
            return result;
        }

        /// <summary>Drag deltas are fractions of the viewing area; both axes can turn through full revolutions.</summary>
        public void Rotate(Vector2 normalizedDelta)
        {
            if (disposed || modelPivot == null || !Finite(normalizedDelta.x) || !Finite(normalizedDelta.y)) return;
            float yaw = -Mathf.Clamp(normalizedDelta.x, -1f, 1f) * RotationDegreesPerView;
            float pitch = Mathf.Clamp(normalizedDelta.y, -1f, 1f) * RotationDegreesPerView;
            modelPivot.localRotation = (Quaternion.AngleAxis(pitch, Vector3.right)
                * Quaternion.AngleAxis(yaw, Vector3.up) * modelPivot.localRotation).normalized;
        }

        /// <summary>Positive deltas zoom in; zoom changes this camera only.</summary>
        public void Zoom(float normalizedDelta)
        {
            if (disposed || !Finite(normalizedDelta)) return;
            zoomNormalized = Mathf.Clamp01(zoomNormalized + normalizedDelta);
            ApplyCameraPose();
        }

        public void ResetView()
        {
            if (disposed) return;
            if (modelPivot != null) modelPivot.localRotation = Quaternion.identity;
            zoomNormalized = DefaultZoom;
            ApplyCameraPose();
        }

        private void ApplyCameraPose()
        {
            if (previewCamera == null) return;
            float distance = fitDistance * Mathf.Pow(2f, 1f - zoomNormalized * 2f);
            previewCamera.transform.localPosition = new Vector3(0f, 0f, -distance);
            previewCamera.transform.localRotation = Quaternion.identity;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (previewCamera != null)
            {
                previewCamera.enabled = false;
                previewCamera.targetTexture = null;
            }
            if (root != null) root.SetActive(false);
            ReleasePreviewSlot();
            DestroyOwned(root);
            foreach (Material material in ownedMaterials) DestroyOwned(material);
            ownedMaterials.Clear();
            if (texture != null) texture.Release();
            DestroyOwned(texture);
            DestroyOwned(paintTexture);
            DestroyOwned(painterTexture);
            root = null;
            modelPivot = null;
            previewCamera = null;
            texture = null;
            paintTexture = null;
            painterTexture = null;
        }

        private void AcquirePreviewSlot()
        {
            previewSlot = ActivePreviews.Count;
            ActivePreviews.Add(this);
            PositionPreviewSlot();
        }

        private void ReleasePreviewSlot()
        {
            if (previewSlot < 0) return;
            int released = previewSlot;
            ActivePreviews.RemoveAt(released);
            previewSlot = -1;
            // Keep active slots compact. Camera and model move together, so the rendered view
            // is unchanged while a lone surviving preview returns to the nearest staging spot.
            for (int i = released; i < ActivePreviews.Count; i++)
            {
                ActivePreviews[i].previewSlot = i;
                ActivePreviews[i].PositionPreviewSlot();
            }
        }

        private void PositionPreviewSlot()
        {
            if (root != null)
                root.transform.position = new Vector3(10000f + previewSlot * 64f, 10000f, 10000f);
        }

        private static GameObject NewObject(string name, Transform parent)
        {
            var result = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = PreviewLayer };
            if (parent != null) result.transform.SetParent(parent, false);
            return result;
        }

        private static void DestroyOwned(UnityEngine.Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(item);
            else UnityEngine.Object.DestroyImmediate(item);
        }

        private static string HashPixels(Color32[] pixels)
        {
            var bytes = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                bytes[i * 4] = pixels[i].r; bytes[i * 4 + 1] = pixels[i].g;
                bytes[i * 4 + 2] = pixels[i].b; bytes[i * 4 + 3] = pixels[i].a;
            }
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
