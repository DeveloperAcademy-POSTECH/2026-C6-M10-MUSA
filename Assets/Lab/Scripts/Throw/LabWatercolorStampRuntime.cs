using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>
    /// Disposable ordered GPU image of the complete, disk-backed round journal. Gameplay is
    /// never read back from this image. A fixed texel-to-cell assignment makes pigment
    /// independent of update frequency, transport pages, and the number of cells in a batch.
    /// </summary>
    public sealed class LabWatercolorStampRuntime : IDisposable
    {
        public const string ShaderResource = "GourdPaint/WatercolorStampRuntime";
        public const int OperationReadBudget = 1024;
        public const int DrawBudget = 8;
        public const int CaptureDrawBudget = 16;
        public const int CaptureOperationBudget = 4096;
        private const int DescriptorCacheSize = 128;
        private static readonly string[] BrushNames = { "Round", "Growth", "Overlap" };
        private readonly Texture2D[] brushes = new Texture2D[3];
        private readonly List<LabPaintArtOperation> operations = new List<LabPaintArtOperation>(OperationReadBudget);
        private readonly Dictionary<uint, LabPaintVisualStamp> descriptors = new Dictionary<uint, LabPaintVisualStamp>();
        private readonly Queue<uint> descriptorOrder = new Queue<uint>();
        private readonly List<int> marked = new List<int>();
        private LabPaintArtJournal source;
        private LabWatercolorStampSettings applied;
        private Material material;
        private RenderTexture committed, displayed, revealBase, topStickers;
        private readonly Texture2D[] stickerBrushes = new Texture2D[4];
        private Texture2D cellMask, logicalCoverage, cellStrokes;
        private Color32[] maskPixels, coveragePixels, strokePixels;
        private int covered, fingerprint;
        private bool disposed, ready, initialized, animating, gpuCompletionPending, releaseReadbackTexture;
        private double revealAt, lastNow;
        private AsyncGPUReadbackRequest readback;
        private RenderTexture readbackTexture;
        private Texture2D completionPixel;
        private uint resourceGeneration;
        private ulong submittedThrough;
        private Texture previousCurrentPaint;
#if UNITY_EDITOR
        private readonly Shader studyShader;
        private readonly Action<MaterialPropertyBlock> configureStudyStamp;
        private readonly Action<Material> configureStudyMaterial;
        /// <summary>Private Editor comparison identity, never a network round version.</summary>
        public string StudyIdentity { get; }
#endif

        public LabWatercolorStampRuntime() { }
#if UNITY_EDITOR
        /// <summary>
        /// Reuses the ordered replay machinery for an isolated appearance study. The
        /// journal and its V2 settings are still validated normally. This opt-in is not
        /// compiled into players and never changes Resources or the game default.
        /// The caller must bind an immutable, versioned snapshot through the callback.
        /// Asset references remain owned by the caller.
        /// </summary>
        public LabWatercolorStampRuntime(Shader studyShader, Texture2D[] studyBrushes,
            Action<MaterialPropertyBlock> configureStudyStamp, string studyIdentity, Action<Material> configureStudyMaterial = null)
        {
            if (studyShader == null) throw new ArgumentNullException(nameof(studyShader));
            if (studyBrushes == null || studyBrushes.Length != brushes.Length)
                throw new ArgumentException("A complete private brush pack is required.", nameof(studyBrushes));
            for (int i = 0; i < brushes.Length; i++)
                if (studyBrushes[i] == null) throw new ArgumentException("A private brush is missing.", nameof(studyBrushes));
            if (configureStudyStamp == null) throw new ArgumentNullException(nameof(configureStudyStamp));
            if (string.IsNullOrWhiteSpace(studyIdentity)) throw new ArgumentException("A versioned study identity is required.", nameof(studyIdentity));
            this.studyShader = studyShader; this.configureStudyStamp = configureStudyStamp;
            this.configureStudyMaterial = configureStudyMaterial;
            StudyIdentity = studyIdentity;
            Array.Copy(studyBrushes, brushes, brushes.Length);
        }
#endif

        /// <summary>The last displayed image stays usable while additional journal pages are being rendered.</summary>
        public RenderTexture PaintTexture => !disposed && Created(displayed) ? displayed : null;
        private bool TopStickerResourceReady => applied == null || !applied.UsesPaperStickers || Created(topStickers);
        public bool Ready => TopStickerResourceReady && !disposed && Error == null && ready && Created(committed) && Created(displayed) && Created(revealBase) && !HasPendingWork && !IsAnimating;
        public bool HasPendingWork => !disposed && Error == null && source != null
            && (AppliedThrough < source.LastSequence || RenderedThrough < AppliedThrough || gpuCompletionPending);
        public bool IsAnimating => !disposed && animating;
        public ulong AppliedThrough { get; private set; }
        public ulong RenderedThrough { get; private set; }
        public uint AppliedRevision { get; private set; }
        public string Error { get; private set; }
        public int DrawCount { get; private set; }
        public int Columns => committed != null ? committed.width : 0;
        public int CachedDescriptorCount => descriptors.Count;

        public bool Update(LabPaintArtJournal journal, Texture currentPaint, int outputColumns,
            LabWatercolorStampSettings settings, double now, bool completeRebuild = false)
        {
            if (disposed || journal == null || !double.IsFinite(now)) return false;
            settings ??= new LabWatercolorStampSettings();
            if (!settings.enabled) return false;
            if (journal.Error != null) { Error = journal.Error; ready = false; return false; }
            try
            {
                if (outputColumns != 1024 && outputColumns != 2048)
                    throw new ArgumentOutOfRangeException(nameof(outputColumns));
                if (journal.Columns < 1 || journal.Rows < 1 || journal.Columns * (long)journal.Rows > 32768)
                    throw new InvalidOperationException("Invalid watercolor journal surface size.");
                if (journal.StyleVersion != settings.Version)
                    throw new InvalidOperationException("Watercolor brush/profile version differs from this round.");
                if (settings.PinnedBrushVersion != settings.CurrentBrushVersion)
                    throw new InvalidOperationException("Installed watercolor brush data changed during this round. Start a new round to use it.");
                bool reset = completeRebuild && Error != null || source != journal || !initialized || !Created(committed)
                    || !Created(displayed) || !Created(revealBase) || committed.width != outputColumns
                    || fingerprint != settings.Fingerprint
                    || settings.UsesPaperStickers && !Created(topStickers);
                if (reset)
                {
                    Error = null;
                    source = journal; applied = settings.SanitizedCopy(); fingerprint = settings.Fingerprint;
                    EnsureResources(outputColumns, journal.Columns, journal.Rows);
                    ResetImage();
                    initialized = true;
                }
                if (Error != null) return false;
                PollGpuCompletion();
                ulong beforeSequence = AppliedThrough;
                bool wasAnimating = animating;
                bool changed = false;
                int draws = 0, processed = 0;
                int drawLimit = completeRebuild ? CaptureDrawBudget : DrawBudget;
                int operationLimit = completeRebuild ? CaptureOperationBudget : OperationReadBudget;
                do
                {
                    if (!journal.ReadOperations(AppliedThrough, Math.Min(OperationReadBudget, operationLimit - processed), operations))
                        throw new InvalidOperationException(journal.Error ?? "Watercolor operation page could not be read.");
                    if (operations.Count == 0) break;
                    int index = 0;
                    while (index < operations.Count && draws < drawLimit)
                    {
                        LabPaintArtOperation op = operations[index];
                        if (op.Sequence != AppliedThrough + 1)
                            throw new InvalidOperationException("Watercolor operation sequence has a gap.");
                        if (op.Kind == LabPaintArtOperationKind.StrokeBegin)
                        {
                            Remember(op.StrokeId, op.Stamp);
                            if (covered > 0)
                            {
                                UploadCoverage();
                                DrawStamp(op.Stamp, logicalCoverage, true);
                                changed = true; draws += topStickers != null ? 2 : 1;
                            }
                            AppliedThrough = op.Sequence; index++; processed++;
                        }
                        else if (op.Kind == LabPaintArtOperationKind.PaintCells || op.Kind == LabPaintArtOperationKind.EraseCells)
                        {
                            bool erase = op.Kind == LabPaintArtOperationKind.EraseCells;
                            uint stroke = op.StrokeId;
                            ClearMask();
                            // A page boundary may split a batch. Different cells have disjoint
                            // high-resolution texels, so this changes draw count but not pigment.
                            do
                            {
                                LabPaintArtOperation item = operations[index];
                                if (item.Sequence != AppliedThrough + 1)
                                    throw new InvalidOperationException("Watercolor operation sequence has a gap.");
                                MarkCells(item.Cells, erase, stroke);
                                AppliedThrough = item.Sequence; AppliedRevision = item.Revision; index++; processed++;
                            }
                            while (index < operations.Count && operations[index].Kind == op.Kind
                                && (erase || operations[index].StrokeId == stroke));
                            cellMask.SetPixels32(maskPixels); cellMask.Apply(false, false);
                            if (erase) DrawErase();
                            else DrawStamp(Descriptor(stroke), cellMask, false);
                            changed = true; draws += topStickers != null ? 2 : 1;
                        }
                        else
                        {
                            if (op.Kind != LabPaintArtOperationKind.RoundBegin
                                && op.Kind != LabPaintArtOperationKind.SurfaceCommit
                                && op.Kind != LabPaintArtOperationKind.RoundSeal)
                                throw new InvalidOperationException("Unsupported watercolor operation.");
                            AppliedThrough = op.Sequence; AppliedRevision = op.Revision; index++; processed++;
                        }
                    }
                }
                while (completeRebuild && AppliedThrough < journal.LastSequence && draws < drawLimit && processed < operationLimit);

                bool currentChanged = previousCurrentPaint != currentPaint;
                if (changed)
                {
                    // Only the disposable reveal image is interpolated. Every operation is
                    // written exactly once into committed in journal order, at full opacity.
                    // Do not restart an in-flight reveal for every growth cell/frame:
                    // continuous growth would otherwise hold progress at zero forever.
                    // New operations join the current bounded reveal burst, while the
                    // retained final image remains independent of this presentation timing.
                    if (!animating)
                    {
                        Graphics.Blit(displayed, revealBase);
                        revealAt = now;
                    }
                    animating = !completeRebuild && !applied.UsesPaperStickers && applied.revealSeconds > 0f;
                    UploadCoverage();
                }
                if (completeRebuild) animating = false;
                float reveal = animating ? Mathf.Clamp01((float)((now - revealAt) / applied.revealSeconds)) : 1f;
                if (reveal >= 1f) animating = false;
                if (changed || currentChanged || wasAnimating || animating || beforeSequence != AppliedThrough
                    || double.IsNaN(lastNow))
                {
                    Present(currentPaint, reveal);
                    submittedThrough = AppliedThrough;
                    previousCurrentPaint = currentPaint;
                }
                lastNow = now;
                PollGpuCompletion();
                ready = AppliedThrough == journal.LastSequence && !animating;
                if (!gpuCompletionPending && !animating && RenderedThrough < AppliedThrough) RequestGpuCompletion();
                return Ready;
            }
            catch (Exception exception)
            {
                Error = "Watercolor artwork unavailable: " + exception.Message;
                ready = false;
                return false;
            }
        }

        private void MarkCells(int[] cells, bool erase, uint stroke)
        {
            if (cells == null) throw new InvalidOperationException("Missing watercolor cells.");
            foreach (int cell in cells)
            {
                if ((uint)cell >= coveragePixels.Length) throw new InvalidOperationException("Watercolor cell is outside the surface.");
                // Duplicate pages are removed by the journal. A unique paint operation must
                // never debit a cell twice; a dry cell is fully cleared before reuse.
                if (!erase && coveragePixels[cell].a != 0)
                    throw new InvalidOperationException("A watercolor cell was painted twice without an erasure.");
                bool was = coveragePixels[cell].a != 0;
                coveragePixels[cell] = erase ? default : new Color32(255, 255, 255, 255);
                strokePixels[cell] = erase ? default : new Color32((byte)stroke, (byte)(stroke >> 8), (byte)(stroke >> 16), (byte)(stroke >> 24));
                if (was != !erase) covered += erase ? -1 : 1;
                if (maskPixels[cell].a == 0) { maskPixels[cell] = new Color32(255, 255, 255, 255); marked.Add(cell); }
            }
        }
        private void ClearMask()
        {
            foreach (int cell in marked) maskPixels[cell] = default;
            marked.Clear();
        }
        private void UploadCoverage()
        {
            logicalCoverage.SetPixels32(coveragePixels); logicalCoverage.Apply(false, false);
            cellStrokes.SetPixels32(strokePixels); cellStrokes.Apply(false, false);
        }

        private void Remember(uint id, LabPaintVisualStamp stamp)
        {
            if (id == 0 || !LabPaintVisualStamp.Valid(new[] { stamp }))
                throw new InvalidOperationException("Invalid watercolor stroke descriptor.");
            if (descriptors.ContainsKey(id)) return;
            if (descriptors.Count >= DescriptorCacheSize) descriptors.Remove(descriptorOrder.Dequeue());
            descriptors.Add(id, stamp); descriptorOrder.Enqueue(id);
        }
        private LabPaintVisualStamp Descriptor(uint id)
        {
            if (descriptors.TryGetValue(id, out LabPaintVisualStamp value)) return value;
            if (!source.TryGetStroke(id, out value)) throw new InvalidOperationException("Missing watercolor stroke geometry.");
            Remember(id, value); return value;
        }
        private void DrawStamp(LabPaintVisualStamp stamp, Texture mask, bool overlapOnly)
        {
            uint seed = stamp.seed ^ unchecked(stamp.sequence * 0x9E3779B9u);
            int brush = (int)(Mix(seed) % 3);
            var block = new MaterialPropertyBlock();
            Texture2D selectedBrush = brushes[brush];
            if (applied.UsesPaperStickers)
            {
                int index = stamp.pattern >= LabPaintPattern.Broad && stamp.pattern <= LabPaintPattern.Flow ? (int)stamp.pattern : (int)LabPaintPattern.Splatter;
                if (stickerBrushes[index] == null)
                    stickerBrushes[index] = Resources.Load<Texture2D>(LabPaperStickerSettings.ResourceFolder + ((LabPaintPattern)index) + "Data");
                selectedBrush = stickerBrushes[index];
                if (selectedBrush == null) throw new InvalidOperationException("Missing paper sticker data for " + stamp.pattern);
            }
            block.SetTexture("_Brush", selectedBrush); block.SetTexture("_CellMask", mask);
            block.SetVector("_Brush_TexelSize", new Vector4(1f / selectedBrush.width, 1f / selectedBrush.height, selectedBrush.width, selectedBrush.height));
            block.SetVector("_Cells", new Vector4(source.Columns, source.Rows, 0, 0));
            block.SetVector("_Center", stamp.center); block.SetVector("_Right", stamp.right); block.SetVector("_Up", stamp.Up);
            block.SetFloat("_Radius", stamp.radius);
            Color tint = stamp.color;
            // Palette bytes are already the numerical pigment color used by the older
            // renderer. SetColor would apply an extra sRGB conversion in a Linear project.
            block.SetVector("_Tint", new Vector4(tint.r, tint.g, tint.b, 1f));
            block.SetVector("_Seed", new Vector4(seed & 1023, (seed >> 10) & 1023, seed >> 20, 0));
            block.SetFloat("_Density", applied.density); block.SetFloat("_Overlap", applied.overlap);
            block.SetFloat("_EdgeStrength", applied.edgeStrength); block.SetFloat("_EdgeWidth", applied.edgeWidth);
            block.SetFloat("_PigmentContrast", applied.pigmentContrast);
            block.SetFloat("_PigmentSaturation", applied.pigmentSaturation);
            block.SetFloat("_RimBreakup", applied.rimBreakup);
            block.SetFloat("_OverlapOnly", overlapOnly ? 1f : 0f);
            if (applied.UsesPaperStickers)
            {
                block.SetFloat("_StickerGrainStrength", applied.paperSticker.grainStrength);
                block.SetFloat("_StickerGrainScale", applied.paperSticker.grainScale);
                uint id = stamp.sequence;
                block.SetVector("_StickerId", new Vector4((id & 255) / 255f, ((id >> 8) & 255) / 255f,
                    ((id >> 16) & 255) / 255f, ((id >> 24) & 255) / 255f));
            }
#if UNITY_EDITOR
            configureStudyStamp?.Invoke(block);
#endif
            Execute(0, block);
            if (topStickers != null) Execute(3, block, topStickers);
        }
        private void DrawErase()
        {
            var block = new MaterialPropertyBlock(); block.SetTexture("_CellMask", cellMask);
            block.SetVector("_Cells", new Vector4(source.Columns, source.Rows, 0, 0));
            Execute(1, block);
            if (topStickers != null) Execute(1, block, topStickers);
        }
        private void Execute(int pass, MaterialPropertyBlock block, RenderTexture destination = null)
        {
            var commands = new CommandBuffer { name = "Ordered watercolor artwork operation" };
            try
            {
                commands.SetRenderTarget(destination != null ? destination : committed);
                commands.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3, 1, block);
                Graphics.ExecuteCommandBuffer(commands); DrawCount++;
            }
            finally { commands.Release(); }
        }
        private void Present(Texture currentPaint, float reveal)
        {
            material.SetVector("_Cells", new Vector4(source.Columns, source.Rows, 0, 0));
            material.SetTexture("_BuiltPaint", committed); material.SetTexture("_RevealBase", revealBase);
            material.SetTexture("_LogicalCoverage", logicalCoverage); material.SetTexture("_CellStrokes", cellStrokes);
            material.SetTexture("_CurrentPaint", currentPaint != null ? currentPaint : logicalCoverage);
            material.SetFloat("_ExternalCoverage", currentPaint != null ? 1f : 0f);
            material.SetFloat("_Reveal", reveal * reveal * (3f - 2f * reveal));
            if (applied.UsesPaperStickers)
            {
                material.SetFloat("_TopStickersEnabled", topStickers != null ? 1f : 0f);
                material.SetFloat("_StickerEdgeStrength", applied.paperSticker.edgeStrength);
                if (topStickers != null)
                {
                    material.SetTexture("_TopStickers", topStickers);
                    material.SetVector("_TopStickers_TexelSize", new Vector4(1f / topStickers.width, 1f / topStickers.height, topStickers.width, topStickers.height));
                }
            }
            var commands = new CommandBuffer { name = "Watercolor artwork presentation" };
            try
            {
                commands.SetRenderTarget(displayed);
                commands.DrawProcedural(Matrix4x4.identity, material, 2, MeshTopology.Triangles, 3);
                Graphics.ExecuteCommandBuffer(commands);
            }
            finally { commands.Release(); }
        }
        private static uint Mix(uint value)
        { unchecked { value ^= value >> 16; value *= 0x7FEB352Du; value ^= value >> 15; value *= 0x846CA68Bu; return value ^ value >> 16; } }
        private void RequestGpuCompletion()
        {
            // Metal can advertise graphics-fence support but reject CPU .passed checks
            // when async compute is absent. One pixel readback proves actual GPU completion
            // without reading the artwork into CPU memory or relying on submit order alone.
            if (gpuCompletionPending || animating) return;
            ulong through = submittedThrough;
            uint generation = resourceGeneration;
            if (!SystemInfo.supportsAsyncGPUReadback)
            {
                completionPixel ??= new Texture2D(1, 1, TextureFormat.RGBA32, false, true)
                    { name = "Watercolor GPU completion pixel", hideFlags = HideFlags.DontSave };
                RenderTexture prior = RenderTexture.active;
                try
                {
                    RenderTexture.active = displayed;
                    completionPixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
                    RenderedThrough = through;
                }
                finally { RenderTexture.active = prior; }
                return;
            }
            gpuCompletionPending = true;
            RenderTexture target = displayed;
            readbackTexture = target;
            readback = AsyncGPUReadback.Request(target, 0, 0, 1, 0, 1, 0, 1, TextureFormat.RGBA32, request =>
            {
                bool release = releaseReadbackTexture;
                releaseReadbackTexture = false;
                gpuCompletionPending = false; readbackTexture = null;
                if (!disposed && generation == resourceGeneration)
                {
                    if (request.hasError) { Error = "Watercolor GPU completion readback failed."; ready = false; }
                    else RenderedThrough = Math.Max(RenderedThrough, through);
                }
                if (release) ReleaseTexture(target);
            });
#if UNITY_EDITOR
            readback.forcePlayerLoopUpdate = true;
#endif
        }
        private void PollGpuCompletion()
        {
            // Callback completion, not a CPU submission, advances RenderedThrough.
            // Update also pumps the request while an Editor preview is otherwise idle.
            if (gpuCompletionPending) readback.Update();
        }

        private void EnsureResources(int columns, int cellsX, int cellsY)
        {
            Shader shader = Resources.Load<Shader>(applied.UsesPaperStickers ? LabPaperStickerSettings.ShaderResource : ShaderResource);
#if UNITY_EDITOR
            if (studyShader != null) shader = studyShader;
#endif
            if (material != null && material.shader != shader)
            { LabVisualSafety.DestroyVisualObject(material); material = null; }
            if (material == null)
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null
                    || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32)
                    || SystemInfo.maxTextureSize < columns || shader == null || !shader.isSupported)
                    throw new InvalidOperationException("The graphics device cannot render watercolor artwork.");
                material = new Material(shader) { name = "Ordered watercolor brush stamps", hideFlags = HideFlags.DontSave };
#if UNITY_EDITOR
                configureStudyMaterial?.Invoke(material);
#endif
            }
            for (int i = 0; i < brushes.Length; i++)
            {
                if (brushes[i] == null) brushes[i] = Resources.Load<Texture2D>("GourdPaint/WatercolorBrushes/" + BrushNames[i]);
                if (brushes[i] == null) throw new InvalidOperationException("Missing baked watercolor brush: " + BrushNames[i]);
            }
            ReleaseTextures();
            committed = Allocate(columns, "Watercolor retained artwork");
            displayed = Allocate(columns, "Watercolor displayed artwork");
            revealBase = Allocate(columns, "Watercolor reveal start");
            if (applied.UsesPaperStickers)
            {
                topStickers = Allocate(columns, "Opaque paper top sticker IDs");
                topStickers.filterMode = FilterMode.Point;
            }
            cellMask = AllocateMask(cellsX, cellsY, "Watercolor operation cells");
            logicalCoverage = AllocateMask(cellsX, cellsY, "Watercolor logical coverage");
            cellStrokes = AllocateMask(cellsX, cellsY, "Watercolor final claim stroke IDs");
            maskPixels = new Color32[cellsX * cellsY]; coveragePixels = new Color32[cellsX * cellsY];
            strokePixels = new Color32[cellsX * cellsY];
        }
        private void ResetImage()
        {
            var commands = new CommandBuffer { name = "Reset complete watercolor journal replay" };
            try
            {
                foreach (RenderTexture target in new[] { committed, displayed, revealBase, topStickers })
                { if (target != null) { commands.SetRenderTarget(target); commands.ClearRenderTarget(false, true, Color.clear); } }
                Graphics.ExecuteCommandBuffer(commands);
            }
            finally { commands.Release(); }
            cellMask.SetPixels32(maskPixels); cellMask.Apply(false, false); UploadCoverage();
            descriptors.Clear(); descriptorOrder.Clear(); marked.Clear(); covered = 0;
            resourceGeneration++;
            AppliedThrough = RenderedThrough = submittedThrough = 0; AppliedRevision = 0;
            DrawCount = 0; ready = animating = false;
            previousCurrentPaint = null; lastNow = double.NaN;
        }
        private static Texture2D AllocateMask(int columns, int rows, string name)
            => new Texture2D(columns, rows, TextureFormat.RGBA32, false, true)
            { name = name, hideFlags = HideFlags.DontSave, filterMode = FilterMode.Point,
                wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
        private static RenderTexture Allocate(int columns, string name)
        {
            var value = new RenderTexture(columns, columns / 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            { name = name, hideFlags = HideFlags.DontSave, useMipMap = false, autoGenerateMips = false,
                filterMode = FilterMode.Bilinear, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp };
            if (value.Create()) return value;
            LabVisualSafety.DestroyVisualObject(value); throw new InvalidOperationException("Watercolor GPU allocation failed.");
        }
        private static bool Created(RenderTexture value) => value != null && value.IsCreated();
        private static void Release(ref RenderTexture value)
        { if (value != null) { value.Release(); LabVisualSafety.DestroyVisualObject(value); value = null; } }
        private static void ReleaseTexture(RenderTexture value)
        { if (value != null) { value.Release(); LabVisualSafety.DestroyVisualObject(value); } }
        private void ReleaseTextures()
        {
            resourceGeneration++;
            Release(ref committed);
            if (displayed != null && displayed == readbackTexture && gpuCompletionPending)
            {
                // The request owns its source until its callback. Retiring this one texture
                // avoids releasing GPU memory that is still referenced by an async readback.
                releaseReadbackTexture = true; displayed = null;
            }
            else Release(ref displayed);
            Release(ref revealBase);
            Release(ref topStickers);
            if (cellMask != null) LabVisualSafety.DestroyVisualObject(cellMask);
            if (logicalCoverage != null) LabVisualSafety.DestroyVisualObject(logicalCoverage);
            if (cellStrokes != null) LabVisualSafety.DestroyVisualObject(cellStrokes);
            cellMask = logicalCoverage = cellStrokes = null; maskPixels = coveragePixels = strokePixels = null;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; ReleaseTextures();
            if (completionPixel != null) LabVisualSafety.DestroyVisualObject(completionPixel);
            completionPixel = null;
            if (material != null) LabVisualSafety.DestroyVisualObject(material);
            material = null; source = null; descriptors.Clear(); descriptorOrder.Clear(); operations.Clear(); marked.Clear();
        }
    }
}
