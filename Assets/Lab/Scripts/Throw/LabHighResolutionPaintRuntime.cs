using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>
    /// Bounded, disposable GPU presentation of one committed Host surface. The authoritative
    /// textures remain unchanged. Stamp geometry supplies sub-cell brush detail; the current
    /// committed color/P maps constrain every output, including erasure and territory changes.
    /// </summary>
    public sealed class LabHighResolutionPaintRuntime : IDisposable
    {
        public const string ShaderResource = "GourdPaint/HighResolutionRuntime";
        public const int DefaultColumns = 1024;
        public const int StampPassBudget = 8;
        private Material material;
        private RenderTexture stamps, paint, painters;
        private LabPaintSurface cachedSurface;
        private LabPaintVisualStamp[] queued = Array.Empty<LabPaintVisualStamp>();
        private uint seenVisualVersion, renderedRevision;
        private int renderedPaintCount = -1, renderedCoveredCount = -1;
        private int built;
        private int stampFrame = -1, stampPassesInFrame;
        private bool cacheInitialized, disposed, ready;
        private readonly Texture2D[] brushes = new Texture2D[4];
        public RenderTexture PaintTexture => Ready ? paint : null;
        public RenderTexture PainterTexture => Ready ? painters : null;
        public bool Ready => !disposed && ready && paint != null && painters != null
            && paint.IsCreated() && painters.IsCreated();
        public bool HasPendingWork => !disposed && Error == null && built < queued.Length;
        public string Error { get; private set; }
        public int Columns => paint != null ? paint.width : 0;
        public int RetainedStampCount => queued.Length;

        public bool Update(LabPaintSurface source, Texture2D colors, Texture2D owners,
            int columns = DefaultColumns, bool completeRebuild = false)
        {
            if (disposed || source == null || colors == null || owners == null) return false;
            if (columns != 1024 && columns != 2048) { Error = "Unsupported high-resolution paint dimensions."; return false; }
            if (colors.width != source.Columns || colors.height != source.Rows
                || owners.width != source.Columns || owners.height != source.Rows) return false;
            try
            {
                if (!EnsureResources(columns)) return false;
                // A valid explicit refresh may retry a prior allocation failure. Clear the
                // old error before pending-work checks so a partial retry cannot look ready.
                Error = null;
                bool differentSurface = cachedSurface != source;
                bool newVisual = differentSurface || !cacheInitialized || seenVisualVersion != source.VisualVersion;
                if (newVisual)
                {
                    LabPaintVisualStamp[] next = source.CopyVisualStamps();
                    bool append = !differentSurface && cacheInitialized && queued.Length <= next.Length;
                    if (append)
                        for (int i = 0; i < queued.Length; i++)
                            if (!queued[i].Equals(next[i])) { append = false; break; }
                    if (!append)
                    {
                        Clear(stamps);
                        built = 0;
                    }
                    queued = next;
                    cachedSurface = source;
                    seenVisualVersion = source.VisualVersion;
                    cacheInitialized = true;
                    ready = false;
                }
                bool changed = !ready || renderedRevision != source.Revision
                    || renderedPaintCount != source.PaintCount || renderedCoveredCount != source.CoveredCount;
                if (!changed && !HasPendingWork) return true;
                if (stampFrame != Time.frameCount)
                { stampFrame = Time.frameCount; stampPassesInFrame = 0; }
                int limit = completeRebuild ? queued.Length
                    : Math.Min(queued.Length, built + Math.Max(0, StampPassBudget - stampPassesInFrame));
                int beforeBatch = built;
                if (built < limit)
                {
                    var commands = new CommandBuffer { name = "Committed gourd brush geometry" };
                    try
                    {
                        commands.SetRenderTarget(stamps);
                        for (; built < limit; built++)
                        {
                            LabPaintVisualStamp stamp = queued[built];
                            Texture2D brush = Brush(stamp.pattern);
                            if (brush == null || stamp.radius <= 0f) continue;
                            var block = new MaterialPropertyBlock();
                            block.SetTexture("_Brush", brush);
                            block.SetVector("_Center", stamp.center);
                            block.SetVector("_Right", stamp.right);
                            block.SetVector("_Up", Vector3.Cross(stamp.center, stamp.right));
                            block.SetFloat("_Radius", stamp.radius);
                            commands.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, block);
                        }
                        Graphics.ExecuteCommandBuffer(commands);
                    }
                    finally { commands.Release(); }
                    stampPassesInFrame += built - beforeBatch;
                }
                // A restored checkpoint may need several bounded brush batches. Until the
                // complete image exists the owner binds the exact committed low-res maps.
                if (HasPendingWork) { ready = false; return false; }
                material.SetTexture("_StampMap", stamps);
                material.SetTexture("_AuthorityMap", colors);
                material.SetTexture("_AuthorityPainters", owners);
                material.SetVector("_AuthoritySize", new Vector4(source.Columns, source.Rows,
                    1f / source.Columns, 1f / source.Rows));
                material.SetFloat("_CoveredCount", source.CoveredCount);
                material.SetFloat("_CellCount", source.CellCount);
                var reconcile = new CommandBuffer { name = "Committed gourd high-resolution reconciliation" };
                try
                {
                    reconcile.SetRenderTarget(paint);
                    reconcile.DrawProcedural(Matrix4x4.identity, material, 1, MeshTopology.Triangles, 3);
                    reconcile.SetRenderTarget(painters);
                    reconcile.DrawProcedural(Matrix4x4.identity, material, 2, MeshTopology.Triangles, 3);
                    Graphics.ExecuteCommandBuffer(reconcile);
                }
                finally { reconcile.Release(); }
                renderedRevision = source.Revision;
                renderedPaintCount = source.PaintCount;
                renderedCoveredCount = source.CoveredCount;
                ready = true;
                Error = null;
                return true;
            }
            catch (Exception exception)
            {
                // Graphics availability is a presentation concern. Never mutate the surface,
                // block input, or turn a failed GPU allocation into a failed game session.
                Error = "High-resolution paint unavailable: " + exception.Message;
                ReleaseTextures();
                return false;
            }
        }

        private Texture2D Brush(LabPaintPattern pattern)
        {
            int index = (int)pattern;
            if (index < 1 || index >= brushes.Length) return null;
            if (brushes[index] == null)
                brushes[index] = Resources.Load<Texture2D>("GourdPaint/Patterns/" + pattern);
            return brushes[index];
        }

        private bool EnsureResources(int columns)
        {
            if (material == null)
            {
                Shader shader = Resources.Load<Shader>(ShaderResource);
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || shader == null || !shader.isSupported
                    || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32)
                    || SystemInfo.maxTextureSize < columns)
                { Error = "The current graphics device does not support high-resolution paint."; return false; }
                material = new Material(shader) { name = "Gourd high-resolution paint (runtime)", hideFlags = HideFlags.DontSave };
            }
            if (stamps != null && paint != null && painters != null && paint.width == columns
                && stamps.IsCreated() && paint.IsCreated() && painters.IsCreated()) return true;
            ReleaseTextures();
            stamps = Allocate(columns, "Gourd continuous brush cache", FilterMode.Bilinear);
            paint = Allocate(columns, "Gourd high-resolution committed paint", FilterMode.Bilinear);
            painters = Allocate(columns, "Gourd high-resolution committed P numbers", FilterMode.Point);
            Clear(stamps); Clear(paint); Clear(painters);
            cacheInitialized = ready = false;
            renderedPaintCount = renderedCoveredCount = -1;
            built = 0;
            return true;
        }

        private static RenderTexture Allocate(int columns, string name, FilterMode filter)
        {
            var texture = new RenderTexture(columns, columns / 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = name, hideFlags = HideFlags.DontSave, filterMode = filter,
                wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp,
                useMipMap = false, autoGenerateMips = false, antiAliasing = 1
            };
            if (!texture.Create())
            { LabVisualSafety.DestroyVisualObject(texture); throw new InvalidOperationException("GPU paint texture allocation failed."); }
            return texture;
        }

        private static void Clear(RenderTexture texture)
        {
            RenderTexture previous = RenderTexture.active;
            try { RenderTexture.active = texture; GL.Clear(false, true, Color.clear); }
            finally { RenderTexture.active = previous; }
        }
        private static void Release(ref RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release(); LabVisualSafety.DestroyVisualObject(texture); texture = null;
        }
        private void ReleaseTextures()
        {
            Release(ref stamps); Release(ref paint); Release(ref painters);
            ready = cacheInitialized = false; built = 0;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ReleaseTextures();
            if (material != null) LabVisualSafety.DestroyVisualObject(material);
            material = null; cachedSurface = null;
            queued = Array.Empty<LabPaintVisualStamp>();
        }
    }
}
