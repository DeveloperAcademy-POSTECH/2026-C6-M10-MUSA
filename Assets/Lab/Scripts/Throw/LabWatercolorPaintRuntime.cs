using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>
    /// Disposable appearance of the existing committed paint maps. Colour-separated optical
    /// history cannot mix element RGB, change painter bytes, or write into the Host surface.
    /// Active stains are redrawn from descriptors; only their completed result is committed.
    /// </summary>
    public sealed class LabWatercolorPaintRuntime : IDisposable
    {
        public const string ShaderResource = "GourdPaint/WatercolorRuntime";
        public const int StampPassBudget = 8;
        private const int PaletteCount = 5;
        private readonly Texture2D[] brushes = new Texture2D[4];
        private readonly Texture2D[] distances = new Texture2D[4];
        private readonly Vector4[] palette = new Vector4[PaletteCount];
        private readonly List<ActiveStamp> active = new List<ActiveStamp>();
        private readonly LabWatercolorPaintSettings defaults = new LabWatercolorPaintSettings();
        private LabWatercolorPaintSettings applied;
        private Material material;
        private RenderTexture committed, live, paint;
        private LabPaintSurface cachedSurface;
        private LabPaintVisualStamp[] queued = Array.Empty<LabPaintVisualStamp>();
        private double[] births = Array.Empty<double>();
        private uint seenVisualVersion, renderedRevision;
        private int renderedPaintCount = -1, renderedCoveredCount = -1;
        private int built, settingsFingerprint, stampFrame = -1, stampPassesInFrame;
        private int liveSlices;
        private double lastNow = double.NaN, renderedAt = double.NaN;
        private Texture previousPaint, previousPainters;
        private bool cacheInitialized, ready, disposed, settingsInstalled;

        private struct ActiveStamp
        {
            public LabPaintVisualStamp Stamp;
            public double Birth;
            public int Slice;
        }

        public RenderTexture PaintTexture => Ready ? paint : null;
        public bool Ready => !disposed && ready && Error == null
            && Created(committed) && Created(live) && Created(paint);
        public bool HasPendingWork => !disposed && Error == null && built < queued.Length;
        public bool IsAnimating => !disposed && Error == null && active.Count > 0;
        public string Error { get; private set; }
        public int ReconcileCount { get; private set; }
        public int CommittedStampCount { get; private set; }
        public int ActiveStampCount => active.Count;
        public int RetainedStampCount => queued.Length;
        public int Columns => paint != null ? paint.width : 0;

        public LabWatercolorPaintRuntime()
        {
            for (int i = 0; i < PaletteCount; i++)
            {
                Color color = LabPaintPalette.ColorFor((LabOrbElement)(i + 1));
                palette[i] = new Vector4(color.r, color.g, color.b, color.a);
            }
        }

        /// <summary>
        /// Initial/replaced history is installed at its final appearance. Only a stable
        /// appended sequence is eligible for local animation. completeRebuild explicitly
        /// finishes all pending work for capture and is also the retry boundary after failure.
        /// </summary>
        public bool Update(LabPaintSurface source, Texture currentPaint, Texture currentPainters,
            LabWatercolorPaintSettings settings, double now, bool completeRebuild = false)
        {
            if (disposed || source == null || currentPaint == null || currentPainters == null
                || double.IsNaN(now) || double.IsInfinity(now)) return false;
            if (Error != null && !completeRebuild) return false;
            if (!Readable(currentPaint) || !Readable(currentPainters)) { ready = false; return false; }
            int columns = currentPaint.width;
            if ((columns != 1024 && columns != 2048) || currentPaint.height != columns / 2
                || currentPainters.width != columns || currentPainters.height != columns / 2)
            { Error = "Unsupported watercolor map dimensions."; ready = false; return false; }

            try
            {
                settings ??= defaults;
                int fingerprint = settings.Fingerprint;
                bool settingsChanged = !settingsInstalled || fingerprint != settingsFingerprint;
                if (settingsChanged)
                {
                    applied = settings.SanitizedCopy();
                    settingsFingerprint = fingerprint;
                    settingsInstalled = true;
                }
                if (!EnsureResources(columns)) return false;
                Error = null;
                bool clockReset = !double.IsNaN(lastNow) && now < lastNow;
                bool differentSurface = cachedSurface != source;
                bool historyChanged = differentSurface || !cacheInitialized
                    || seenVisualVersion != source.VisualVersion || settingsChanged || clockReset;
                bool opticsChanged = false;
                if (historyChanged)
                {
                    LabPaintVisualStamp[] next = source.CopyVisualStamps();
                    bool append = cacheInitialized && !differentSurface && !settingsChanged && !clockReset
                        && queued.Length <= next.Length;
                    if (append)
                        for (int i = 0; i < queued.Length; i++)
                            if (!queued[i].Equals(next[i])) { append = false; break; }
                    var nextBirths = new double[next.Length];
                    for (int i = 0; i < nextBirths.Length; i++) nextBirths[i] = double.NegativeInfinity;
                    if (append)
                    {
                        Array.Copy(births, nextBirths, births.Length);
                        if (ready && !completeRebuild && applied.spreadSeconds > 0f)
                            for (int i = queued.Length; i < next.Length; i++) nextBirths[i] = now;
                    }
                    else
                    {
                        ClearArrays();
                        built = 0;
                        active.Clear();
                        CommittedStampCount = 0;
                        opticsChanged = true;
                    }
                    queued = next; births = nextBirths;
                    cachedSurface = source; seenVisualVersion = source.VisualVersion;
                    cacheInitialized = true;
                    ready = false;
                }

                bool inputChanged = previousPaint != currentPaint || previousPainters != currentPainters
                    || renderedRevision != source.Revision || renderedPaintCount != source.PaintCount
                    || renderedCoveredCount != source.CoveredCount;
                bool animationTick = active.Count > 0 && renderedAt != now;
                if (!historyChanged && !inputChanged && !animationTick && !HasPendingWork && !completeRebuild)
                { lastNow = now; return Ready; }

                ApplySettings();
                var commands = new CommandBuffer { name = "Gourd watercolor bounded optical update" };
                try
                {
                    opticsChanged |= CommitFinished(commands, now, completeRebuild);
                    if (stampFrame != Time.frameCount)
                    { stampFrame = Time.frameCount; stampPassesInFrame = 0; }
                    int limit = completeRebuild ? queued.Length
                        : Math.Min(queued.Length, built + Math.Max(0, StampPassBudget - stampPassesInFrame));
                    int before = built;
                    for (; built < limit; built++)
                    {
                        LabPaintVisualStamp stamp = queued[built];
                        int slice = SliceFor(stamp.color);
                        double birth = births[built];
                        bool animate = !completeRebuild && applied.spreadSeconds > 0f
                            && !double.IsNegativeInfinity(birth) && now - birth < applied.spreadSeconds;
                        if (animate && Brush(stamp.pattern) != null)
                        {
                            // Bursts remain bounded; committing the oldest one changes only
                            // its presentation duration, never the final pigment or gameplay.
                            if (active.Count >= applied.maxActiveStains)
                            {
                                ActiveStamp oldest = active[0];
                                if (DrawStamp(commands, committed, oldest.Stamp, oldest.Slice, 1f)) CommittedStampCount++;
                                active.RemoveAt(0);
                            }
                            active.Add(new ActiveStamp { Stamp = stamp, Slice = slice, Birth = birth });
                        }
                        else if (DrawStamp(commands, committed, stamp, slice, 1f)) CommittedStampCount++;
                        opticsChanged = true;
                    }
                    stampPassesInFrame += built - before;
                    // live is a disposable view, cleared before every replay. A frame never
                    // adds pigment into committed except through one completed descriptor.
                    if (opticsChanged || animationTick || completeRebuild)
                        RebuildLive(commands, now);
                    Graphics.ExecuteCommandBuffer(commands);
                }
                finally { commands.Release(); }
                lastNow = now;
                if (HasPendingWork) { ready = false; return false; }

                if (!ready || inputChanged || opticsChanged || animationTick || completeRebuild)
                {
                    material.SetTexture("_CurrentPaint", currentPaint);
                    material.SetTexture("_CurrentPainters", currentPainters);
                    material.SetTexture("_CommittedOptics", committed);
                    material.SetTexture("_LiveOptics", live);
                    var composite = new CommandBuffer { name = "Gourd watercolor appearance reconciliation" };
                    try
                    {
                        composite.SetRenderTarget(paint);
                        composite.DrawProcedural(Matrix4x4.identity, material, 1, MeshTopology.Triangles, 3);
                        Graphics.ExecuteCommandBuffer(composite);
                    }
                    finally { composite.Release(); }
                    ReconcileCount++;
                    renderedRevision = source.Revision;
                    renderedPaintCount = source.PaintCount; renderedCoveredCount = source.CoveredCount;
                    previousPaint = currentPaint; previousPainters = currentPainters;
                    renderedAt = now;
                }
                ready = true;
                return true;
            }
            catch (Exception exception)
            {
                Error = "Watercolor paint unavailable: " + exception.Message;
                ReleaseTextures();
                return false;
            }
        }

        private bool CommitFinished(CommandBuffer commands, double now, bool force)
        {
            bool changed = false;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                ActiveStamp item = active[i];
                if (!force && now - item.Birth < applied.spreadSeconds) continue;
                if (DrawStamp(commands, committed, item.Stamp, item.Slice, 1f)) CommittedStampCount++;
                active.RemoveAt(i); changed = true;
            }
            return changed;
        }

        private void RebuildLive(CommandBuffer commands, double now)
        {
            int nextSlices = 0;
            foreach (ActiveStamp item in active) nextSlices |= 1 << item.Slice;
            for (int slice = 0; slice < PaletteCount; slice++)
            {
                if (((liveSlices | nextSlices) & 1 << slice) == 0) continue;
                commands.SetRenderTarget(live, 0, CubemapFace.Unknown, slice);
                commands.ClearRenderTarget(false, true, Color.clear);
            }
            foreach (ActiveStamp item in active)
            {
                float progress = applied.spreadSeconds <= 0f ? 1f
                    : Mathf.Clamp01((float)((now - item.Birth) / applied.spreadSeconds));
                DrawStamp(commands, live, item.Stamp, item.Slice, progress);
            }
            liveSlices = nextSlices;
        }

        private bool DrawStamp(CommandBuffer commands, RenderTexture destination,
            LabPaintVisualStamp stamp, int slice, float progress)
        {
            Texture2D brush = Brush(stamp.pattern);
            if (brush == null || stamp.radius <= 0f) return false;
            int index = (int)stamp.pattern;
            if (distances[index] == null) distances[index] = LabWatercolorBrushDistance.Create(brush);
            var block = new MaterialPropertyBlock();
            block.SetTexture("_Brush", brush); block.SetTexture("_InteriorDistance", distances[index]);
            block.SetVector("_Center", stamp.center); block.SetVector("_Right", stamp.right); block.SetVector("_Up", stamp.Up);
            block.SetFloat("_Radius", stamp.radius); block.SetFloat("_Progress", progress);
            block.SetFloat("_Mottle", applied.mottle); block.SetFloat("_EdgeWidth", applied.edgeWidth);
            block.SetFloat("_TextureScale", applied.textureScale);
            uint value = stamp.seed ^ unchecked(stamp.sequence * 0x9E3779B9u);
            block.SetVector("_Seed", new Vector4((value & 1023) * .017f,
                ((value >> 10) & 1023) * .019f, ((value >> 20) & 1023) * .023f, 0f));
            commands.SetRenderTarget(destination, 0, CubemapFace.Unknown, slice);
            commands.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, block);
            return true;
        }

        private Texture2D Brush(LabPaintPattern pattern)
        {
            int index = (int)pattern;
            if (index < 1 || index >= brushes.Length) return null;
            if (brushes[index] == null) brushes[index] = Resources.Load<Texture2D>("GourdPaint/Patterns/" + pattern);
            return brushes[index];
        }

        private int SliceFor(Color32 color)
        {
            Vector3 sample = NormalizeHue(new Vector3(color.r, color.g, color.b));
            int best = 0; float distance = float.PositiveInfinity;
            for (int i = 0; i < PaletteCount; i++)
            {
                Vector4 p = palette[i];
                float value = (sample - NormalizeHue(new Vector3(p.x, p.y, p.z))).sqrMagnitude;
                if (value < distance) { best = i; distance = value; }
            }
            return best;
        }

        private static Vector3 NormalizeHue(Vector3 value)
            => value / Mathf.Max(.00001f, Mathf.Max(value.x, Mathf.Max(value.y, value.z)));

        private void ApplySettings()
        {
            material.SetFloat("_Density", applied.density); material.SetFloat("_BroadVariation", applied.broadVariation);
            material.SetFloat("_Mottle", applied.mottle); material.SetFloat("_Grain", applied.grain);
            material.SetFloat("_GrainScale", applied.grainScale); material.SetFloat("_EdgeStrength", applied.edgeStrength);
            material.SetFloat("_EdgeWidth", applied.edgeWidth); material.SetFloat("_TextureScale", applied.textureScale);
            for (int i = 0; i < PaletteCount; i++) material.SetVector("_Palette" + i, palette[i]);
        }

        private bool EnsureResources(int columns)
        {
            if (material == null)
            {
                Shader shader = Resources.Load<Shader>(ShaderResource);
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !SystemInfo.supports2DArrayTextures
                    || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32)
                    || SystemInfo.maxTextureSize < columns || shader == null || !shader.isSupported)
                { Error = "The current graphics device does not support watercolor paint."; ready = false; return false; }
                material = new Material(shader) { name = "Gourd watercolor paint runtime", hideFlags = HideFlags.DontSave };
            }
            if (Created(committed) && Created(live) && Created(paint) && paint.width == columns) return true;
            ReleaseTextures();
            committed = Allocate(columns, "Gourd watercolor committed optics", true);
            live = Allocate(columns, "Gourd watercolor active optics", true);
            paint = Allocate(columns, "Gourd watercolor displayed paint", false);
            ClearArrays();
            return true;
        }

        private void ClearArrays()
        {
            var commands = new CommandBuffer { name = "Reset gourd watercolor optical history" };
            try
            {
                for (int i = 0; i < PaletteCount; i++)
                {
                    commands.SetRenderTarget(committed, 0, CubemapFace.Unknown, i);
                    commands.ClearRenderTarget(false, true, Color.clear);
                    commands.SetRenderTarget(live, 0, CubemapFace.Unknown, i);
                    commands.ClearRenderTarget(false, true, Color.clear);
                }
                Graphics.ExecuteCommandBuffer(commands);
            }
            finally { commands.Release(); }
            liveSlices = 0;
        }

        private static RenderTexture Allocate(int columns, string name, bool array)
        {
            var texture = new RenderTexture(columns, columns / 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = name, hideFlags = HideFlags.DontSave, dimension = array ? TextureDimension.Tex2DArray : TextureDimension.Tex2D,
                volumeDepth = array ? PaletteCount : 1, filterMode = FilterMode.Bilinear,
                wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp,
                useMipMap = false, autoGenerateMips = false, antiAliasing = 1
            };
            try
            {
                if (!texture.Create()) throw new InvalidOperationException("GPU watercolor texture allocation failed.");
                return texture;
            }
            catch
            {
                texture.Release(); LabVisualSafety.DestroyVisualObject(texture);
                throw;
            }
        }

        private static bool Created(RenderTexture texture) => texture != null && texture.IsCreated();
        private static bool Readable(Texture texture) => !(texture is RenderTexture target) || target.IsCreated();
        private static void Release(ref RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release(); LabVisualSafety.DestroyVisualObject(texture); texture = null;
        }
        private void ReleaseTextures()
        {
            Release(ref committed); Release(ref live); Release(ref paint);
            active.Clear(); queued = Array.Empty<LabPaintVisualStamp>(); births = Array.Empty<double>();
            cacheInitialized = ready = false; cachedSurface = null; built = 0; liveSlices = 0;
            previousPaint = previousPainters = null; renderedAt = lastNow = double.NaN;
            renderedPaintCount = renderedCoveredCount = -1; CommittedStampCount = 0;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; ReleaseTextures();
            for (int i = 0; i < distances.Length; i++)
                if (distances[i] != null) { LabVisualSafety.DestroyVisualObject(distances[i]); distances[i] = null; }
            if (material != null) LabVisualSafety.DestroyVisualObject(material);
            material = null;
            // Resources owns the original brushes; they are not destroyed or modified here.
        }
    }
}
