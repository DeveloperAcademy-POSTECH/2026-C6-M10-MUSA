using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace C6Lab
{
    /// <summary>
    /// Ordered, disposable clay artwork. One accumulated color/height pair is shared by a
    /// separate curved renderer; no GameObject or mesh is allocated for each hit/cell.
    /// Source journals remain authoritative facts, not inferred snapshot differences.
    /// </summary>
    public sealed class LabClayStickerRuntime : IDisposable
    {
        public const string ShaderResource = "GourdPaint/ClaySticker/ClayStickerRuntime";
        public const string SurfaceShaderResource = "GourdPaint/ClaySticker/ClayStickerSurface";
        public const int OperationReadBudget = 1024, DrawBudget = 8;
        public const int CaptureOperationBudget = 4096, CaptureDrawBudget = 32;
        public const int SupportHaloCells = 2;
        const int DescriptorCacheSize = 128;
        static readonly string[] BrushNames = { "BroadData", "LongData", "ClusterData" };
        readonly List<LabPaintArtOperation> operations = new List<LabPaintArtOperation>(OperationReadBudget);
        readonly Dictionary<uint, LabPaintVisualStamp> descriptors = new Dictionary<uint, LabPaintVisualStamp>();
        readonly Queue<uint> descriptorOrder = new Queue<uint>();
        readonly HashSet<int> pendingCells = new HashSet<int>();
        readonly Texture2D[] brushes = new Texture2D[3];
        readonly LabClayStickerSettings presentationPigment;
        readonly Shader presentationCompositor;
        readonly Func<Color32, Color32> presentationColorResolver;
        /// <summary>Default game renderer uses its round-pinned production assets and pigment.</summary>
        public LabClayStickerRuntime() { }
        /// <summary>Explicit isolated comparison dependencies; never installed into the shared game profile.</summary>
        public LabClayStickerRuntime(Texture2D[] presentationBrushes, LabClayStickerSettings presentationPigment = null,
            Shader presentationCompositor = null, Func<Color32, Color32> presentationColorResolver = null)
        {
            if (presentationCompositor != null && !presentationCompositor.isSupported)
                throw new ArgumentException("The isolated presentation compositor is unsupported.", nameof(presentationCompositor));
            this.presentationCompositor = presentationCompositor;
            this.presentationColorResolver = presentationColorResolver;
            if (presentationBrushes != null)
            {
                if (presentationBrushes.Length != 3) throw new ArgumentException("Exactly three presentation brushes are required.", nameof(presentationBrushes));
                for (int i = 0; i < 3; i++)
                {
                    if (presentationBrushes[i] == null) throw new ArgumentException("A presentation brush is missing.", nameof(presentationBrushes));
                    brushes[i] = presentationBrushes[i];
                }
            }
            if (presentationPigment != null)
            {
                presentationPigment.Validate();
                this.presentationPigment = presentationPigment.SanitizedCopy();
            }
        }
        LabPaintArtJournal source;
        LabWatercolorStampSettings applied;
        Material material;
        RenderTexture color, height, scratchColor, scratchHeight, displayed, displayedHeight;
        Texture2D cellMask, support, fallback;
        Color32[] maskPixels, supportPixels, fallbackPixels;
        // Presentation-only dense halo. Never use these buffers for gameplay area.
        Color32[] displayedSupportPixels;
        byte[] supportCounts;
        readonly List<int> previousMask = new List<int>();
        LabPaintArtOperationKind pendingKind;
        uint pendingStroke, highestFirstStroke, generation;
        int covered, fingerprint;
        bool disposed, initialized, gpuPending, releasePendingTarget;
        ulong submittedThrough;
        RenderTexture pendingTarget;
        AsyncGPUReadbackRequest gpuRequest;
        Texture2D completionPixel;
        public RenderTexture PaintTexture => !disposed && Created(displayed) ? displayed : null;
        public RenderTexture HeightTexture => !disposed && Created(displayedHeight) ? displayedHeight : null;
        public ulong AppliedThrough { get; private set; }
        public ulong RenderedThrough { get; private set; }
        public uint AppliedRevision { get; private set; }
        public string Error { get; private set; }
        public int DrawCount { get; private set; }
        public int Columns => color != null ? color.width : 0;
        public bool IsAnimating => false;
        public bool HasPendingWork => !disposed && source != null && Error == null &&
            (AppliedThrough < source.LastSequence || pendingCells.Count > 0 || RenderedThrough < AppliedThrough || gpuPending);
        public bool Ready => !disposed && Error == null && initialized && Created(color) && Created(height)
            && Created(displayed) && Created(displayedHeight) && !HasPendingWork;

        public bool Update(LabPaintArtJournal journal, Texture authorityPaint, Texture painterTexture,
            int outputColumns, LabWatercolorStampSettings settings, double now, bool completeRebuild = false)
        {
            if (disposed || journal == null || settings == null || !settings.enabled || !double.IsFinite(now)) return false;
            try
            {
                if (journal.Error != null) throw new InvalidOperationException(journal.Error);
                if (outputColumns != 1024 && outputColumns != 2048) throw new ArgumentOutOfRangeException(nameof(outputColumns));
                if (journal.Columns < 1 || journal.Rows < 1 || (long)journal.Columns * journal.Rows > 32768)
                    throw new InvalidOperationException("Invalid clay journal dimensions.");
                if (!settings.UsesClayStickers || journal.StyleVersion != settings.Version
                    || settings.PinnedBrushVersion != settings.CurrentBrushVersion)
                    throw new InvalidOperationException("The clay asset/profile identity differs from this round.");
                bool reset = source != journal || !initialized || !Created(color) || !Created(height)
                    || !Created(displayed) || !Created(displayedHeight) || !Created(scratchColor) || !Created(scratchHeight)
                    || color.width != outputColumns || fingerprint != settings.Fingerprint || completeRebuild && Error != null;
                if (reset)
                {
                    Error = null; source = journal; applied = settings.SanitizedCopy(); fingerprint = settings.Fingerprint;
                    EnsureResources(outputColumns); ResetImage(); initialized = true;
                }
                if (Error != null) return false;
                if (gpuPending) gpuRequest.Update();
                int operationLimit = completeRebuild ? CaptureOperationBudget : OperationReadBudget;
                int drawLimit = completeRebuild ? CaptureDrawBudget : DrawBudget;
                int processed = 0, draws = 0; bool changed = false;
                do
                {
                    if (!journal.ReadOperations(AppliedThrough, Math.Min(OperationReadBudget, operationLimit - processed), operations))
                        throw new InvalidOperationException(journal.Error ?? "Clay artwork page could not be read.");
                    if (operations.Count == 0) break;
                    bool exhausted = false;
                    foreach (LabPaintArtOperation op in operations)
                    {
                        if (op.Sequence != AppliedThrough + 1) throw new InvalidOperationException("Clay artwork sequence has a gap.");
                        bool cellOperation = op.Kind == LabPaintArtOperationKind.PaintCells || op.Kind == LabPaintArtOperationKind.EraseCells;
                        bool continues = cellOperation && pendingCells.Count > 0 && pendingKind == op.Kind
                            && (op.Kind == LabPaintArtOperationKind.EraseCells || pendingStroke == op.StrokeId);
                        if (pendingCells.Count > 0 && !continues)
                        {
                            if (draws + 2 > drawLimit) { exhausted = true; break; }
                            FlushPending(); draws += 2; changed = true;
                        }
                        if (op.Kind == LabPaintArtOperationKind.StrokeBegin) Remember(op.StrokeId, op.Stamp);
                        else if (cellOperation)
                        {
                            pendingKind = op.Kind; pendingStroke = op.StrokeId;
                            MarkCells(op.Cells, op.Kind == LabPaintArtOperationKind.EraseCells, op.StrokeId);
                        }
                        else if (op.Kind != LabPaintArtOperationKind.RoundBegin
                            && op.Kind != LabPaintArtOperationKind.SurfaceCommit && op.Kind != LabPaintArtOperationKind.RoundSeal)
                            throw new InvalidOperationException("Unsupported clay operation.");
                        AppliedThrough = op.Sequence; AppliedRevision = op.Revision; processed++;
                        if (processed >= operationLimit) { exhausted = true; break; }
                    }
                    if (exhausted) break;
                }
                while (completeRebuild && AppliedThrough < journal.LastSequence && processed < operationLimit);
                // Do not flush a partial PaintCells batch at an Update/page boundary. A
                // following semantic operation fixes the group independent of read budgets.
                if (changed || submittedThrough != AppliedThrough && pendingCells.Count == 0)
                {
                    Present(); submittedThrough = AppliedThrough;
                }
                if (pendingCells.Count == 0 && RenderedThrough < submittedThrough && !gpuPending) RequestCompletion();
                return Ready;
            }
            catch (Exception exception) { Error = "Clay artwork unavailable: " + exception.Message; return false; }
        }

        void MarkCells(int[] cells, bool erase, uint stroke)
        {
            if (cells == null) throw new InvalidOperationException("Missing clay cells.");
            LabPaintVisualStamp descriptor = erase ? default : Descriptor(stroke);
            Color pigment = erase ? Color.clear : Pigment(descriptor);
            foreach (int cell in cells)
            {
                if ((uint)cell >= supportPixels.Length) throw new InvalidOperationException("Clay cell outside the surface.");
                bool painted = supportPixels[cell].a != 0;
                if (!erase && painted) throw new InvalidOperationException("Duplicate clay paint cell without drying.");
                supportPixels[cell] = erase ? default : new Color32(255,255,255,255);
                fallbackPixels[cell] = erase ? default : (Color32)pigment;
                if (painted != !erase)
                {
                    covered += erase ? -1 : 1;
                    ChangeDisplayedSupport(cell, erase ? -1 : 1);
                }
                pendingCells.Add(cell);
            }
        }

        /// <summary>
        /// Incremental inclusive two-cell Chebyshev support. A source cell contributes
        /// at most once to each wrapped/clamped target, including near the poles.
        /// The exact same traversal is used for removal, so overlapping support survives
        /// another source drying and every count returns to zero after complete drying.
        /// </summary>
        void ChangeDisplayedSupport(int cell, int delta)
        {
            int columns=source.Columns, rows=source.Rows;
            int x=cell%columns, y=cell/columns;
            int radius=SupportHaloCells, horizontalCount=Math.Min(columns,radius*2+1);
            int previousY=-1;
            for(int dy=-radius;dy<=radius;dy++)
            {
                int targetY=Mathf.Clamp(y+dy,0,rows-1);
                if(targetY==previousY)continue; // Clamp duplicates add/remove only once.
                previousY=targetY;
                for(int offset=0;offset<horizontalCount;offset++)
                {
                    int targetX=((x-radius+offset)%columns+columns)%columns;
                    int target=targetY*columns+targetX;
                    int next=supportCounts[target]+delta;
                    if(next<0 || next>(radius*2+1)*(radius*2+1))
                        throw new InvalidOperationException("Clay display-support accounting is inconsistent.");
                    supportCounts[target]=(byte)next;
                    displayedSupportPixels[target]=next>0 ? new Color32(255,255,255,255) : default;
                }
            }
        }

        void FlushPending()
        {
            if (pendingCells.Count == 0) return;
            if (pendingKind == LabPaintArtOperationKind.EraseCells)
            {
                foreach (int cell in previousMask) maskPixels[cell] = default;
                previousMask.Clear();
                foreach (int cell in pendingCells) { maskPixels[cell] = new Color32(255,255,255,255); previousMask.Add(cell); }
                cellMask.SetPixels32(maskPixels); cellMask.Apply(false,false);
                material.SetTexture("_CellMask",cellMask);
                material.SetVector("_Cells",new Vector4(source.Columns,source.Rows,0,0));
                DrawInto(color, scratchColor, 2); DrawInto(height, scratchHeight, 2);
                if (covered == 0) { Clear(color); Clear(height); }
            }
            else
            {
                LabPaintVisualStamp descriptor = Descriptor(pendingStroke);
                LabPaintVisualStamp shape = descriptor;
                // A new hit retains its exact original frame. Later connected growth gets
                // one newly attached original silhouette, centered on its actual new cells.
                if (pendingStroke <= highestFirstStroke)
                {
                    Vector3 center = Vector3.zero;
                    foreach (int cell in pendingCells) center += CellDirection(cell);
                    center = center.sqrMagnitude > .000001f ? center.normalized : descriptor.center;
                    Vector3 right = Vector3.ProjectOnPlane(descriptor.right,center);
                    if (right.sqrMagnitude < .000001f) right = Vector3.Cross(center,Mathf.Abs(center.y)<.9f?Vector3.up:Vector3.right);
                    shape.center=center; shape.right=right.normalized;
                    // Tangent square area / opaque asset footprint. This controls visual
                    // coverage only: the journal's paid cells and area budgets are untouched.
                    shape.radius = Mathf.Clamp(Mathf.Sqrt(pendingCells.Count/(float)supportPixels.Length*4*Mathf.PI/.48f)*.5f,.018f,1.3f);
                }
                highestFirstStroke = Math.Max(highestFirstStroke,pendingStroke);
                uint salt = Mix(shape.seed ^ pendingStroke * 0x9e3779b9u ^ (uint)AppliedThrough);
                Texture2D brush = brushes[salt % (uint)brushes.Length];
                material.SetTexture("_Brush",brush);
                material.SetVector("_Center",shape.center);material.SetVector("_Right",shape.right);material.SetVector("_Up",shape.Up);
                material.SetFloat("_Radius",Mathf.Min(shape.radius,1.3f));
                Color tint = Pigment(descriptor);
                material.SetVector("_Tint",new Vector4(tint.r,tint.g,tint.b,1));
                DrawInto(color,scratchColor,0); DrawInto(height,scratchHeight,1);
            }
            pendingCells.Clear(); pendingStroke=0;
        }
        Color Pigment(LabPaintVisualStamp stamp)
        {
            float density = (presentationPigment ?? applied.claySticker).Density(stamp.ImpactStrength);
            Color pigment = presentationColorResolver != null ? presentationColorResolver(stamp.color) : stamp.color;
            // Density alters opaque clay pigment value, never the sticker's opacity.
            Color value = Color.Lerp(new Color(.82f,.82f,.82f,1),pigment,density);value.a=1;return value;
        }
        Vector3 CellDirection(int cell)
        {
            float u=(cell%source.Columns+.5f)/source.Columns,v=(cell/source.Columns+.5f)/source.Rows;
            float y=1-v*2,a=(u-.5f)*Mathf.PI*2,r=Mathf.Sqrt(Mathf.Max(0,1-y*y));
            return new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);
        }
        void Present()
        {
            support.SetPixels32(displayedSupportPixels);support.Apply(false,false);
            fallback.SetPixels32(fallbackPixels);fallback.Apply(false,false);
            material.SetTexture("_BuiltPaint",color);material.SetTexture("_BuiltHeight",height);
            material.SetTexture("_LogicalCoverage",support);material.SetTexture("_Fallback",fallback);
            material.SetVector("_Cells",new Vector4(source.Columns,source.Rows,0,0));
            material.SetFloat("_Complete",covered==supportPixels.Length?1:0);

            Graphics.Blit(color,displayed,material,3);Graphics.Blit(height,displayedHeight,material,4);
            DrawCount+=2;
        }
        void DrawInto(RenderTexture target,RenderTexture scratch,int pass)
        { Graphics.Blit(target,scratch,material,pass);Graphics.Blit(scratch,target);DrawCount++; }
        void Remember(uint id,LabPaintVisualStamp stamp)
        {
            if(id==0 || !LabPaintVisualStamp.Valid(new[]{stamp})) throw new InvalidOperationException("Invalid clay descriptor.");
            if(descriptors.ContainsKey(id)) return;
            if(descriptors.Count>=DescriptorCacheSize) descriptors.Remove(descriptorOrder.Dequeue());
            descriptors.Add(id,stamp);descriptorOrder.Enqueue(id);
        }
        LabPaintVisualStamp Descriptor(uint id)
        {
            if(descriptors.TryGetValue(id,out var result)) return result;
            if(!source.TryGetStroke(id,out result)) throw new InvalidOperationException("Missing clay stroke descriptor.");
            Remember(id,result);return result;
        }
        void EnsureResources(int columns)
        {
            Shader shader=presentationCompositor != null ? presentationCompositor : Resources.Load<Shader>(ShaderResource);
            if(shader==null || !shader.isSupported || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
                throw new InvalidOperationException("The graphics device or clay compositing shader is unavailable.");
            if(material==null) material=new Material(shader){name="Ordered clay sticker compositor",hideFlags=HideFlags.DontSave};
            for(int i=0;i<brushes.Length;i++)
            { brushes[i]??=Resources.Load<Texture2D>(LabClayStickerSettings.ResourceFolder+BrushNames[i]);if(brushes[i]==null)throw new InvalidOperationException("Missing clay mask "+BrushNames[i]); }
            ReleaseMaps();
            color=Allocate(columns,"Clay retained pigment",RenderTextureFormat.ARGB32);
            scratchColor=Allocate(columns,"Clay pigment scratch",RenderTextureFormat.ARGB32);
            displayed=Allocate(columns,"Clay displayed pigment",RenderTextureFormat.ARGB32);
            var format=SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)?RenderTextureFormat.ARGBHalf:RenderTextureFormat.ARGB32;
            height=Allocate(columns,"Clay retained rounded height",format);
            scratchHeight=Allocate(columns,"Clay height scratch",format);
            displayedHeight=Allocate(columns,"Clay displayed rounded height",format);
            cellMask=Mask("Clay erased cells",FilterMode.Point);support=Mask("Clay logical support",FilterMode.Bilinear);fallback=Mask("Clay completed undercoat",FilterMode.Bilinear);
            maskPixels=new Color32[source.Columns*source.Rows];supportPixels=new Color32[maskPixels.Length];fallbackPixels=new Color32[maskPixels.Length];
            displayedSupportPixels=new Color32[maskPixels.Length];supportCounts=new byte[maskPixels.Length];
            PrewarmStampPasses(format);
        }

        // FinalColor/FinalHeight already run while preparing an empty round. Warm the
        // two stamp PSOs and their source textures too, without painting the real maps.
        // The usual completion fence submitted after this also covers these warm draws.
        void PrewarmStampPasses(RenderTextureFormat heightFormat)
        {
            Material warmMaterial=null;
            RenderTexture pigmentTarget=null, heightTarget=null;
            RenderTexture previous=RenderTexture.active;
            try
            {
                warmMaterial=new Material(material) {name="Clay stamp prewarm (temporary)",hideFlags=HideFlags.DontSave};
                warmMaterial.SetVector("_Center",Vector3.right);
                warmMaterial.SetVector("_Right",Vector3.forward);
                warmMaterial.SetVector("_Up",Vector3.down);
                warmMaterial.SetFloat("_Radius",.25f);
                warmMaterial.SetVector("_Tint",Color.white);
                pigmentTarget=RenderTexture.GetTemporary(1,1,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
                heightTarget=RenderTexture.GetTemporary(1,1,0,heightFormat,RenderTextureReadWrite.Linear);
                foreach(Texture2D brush in brushes)
                {
                    warmMaterial.SetTexture("_Brush",brush);
                    Graphics.Blit(Texture2D.blackTexture,pigmentTarget,warmMaterial,0);
                    Graphics.Blit(Texture2D.blackTexture,heightTarget,warmMaterial,1);
                }
            }
            catch(Exception exception)
            {
                // Like the other #482 prewarms, failure keeps the existing first-hit path.
                // It must not change artwork readiness or a gameplay/rendering cursor.
                Debug.LogWarning("C6_PAINT_PREWARM runtime=clay result=failed reason="+exception.Message);
            }
            finally
            {
                RenderTexture.active=previous;
                if(pigmentTarget!=null)RenderTexture.ReleaseTemporary(pigmentTarget);
                if(heightTarget!=null)RenderTexture.ReleaseTemporary(heightTarget);
                if(warmMaterial!=null)LabVisualSafety.DestroyVisualObject(warmMaterial);
            }
        }
        Texture2D Mask(string name,FilterMode filter)=>new Texture2D(source.Columns,source.Rows,TextureFormat.RGBA32,false,true)
        {name=name,hideFlags=HideFlags.DontSave,filterMode=filter,wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp};
        static RenderTexture Allocate(int columns,string name,RenderTextureFormat format)
        {
            var rt=new RenderTexture(columns,columns/2,0,format,RenderTextureReadWrite.Linear)
            {name=name,hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear,wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp};
            if(!rt.Create()){LabVisualSafety.DestroyVisualObject(rt);throw new InvalidOperationException("Clay render target allocation failed.");}return rt;
        }
        void ResetImage()
        {
            generation++;AppliedThrough=RenderedThrough=submittedThrough=0;AppliedRevision=0;covered=0;highestFirstStroke=0;
            descriptors.Clear();descriptorOrder.Clear();pendingCells.Clear();previousMask.Clear();
            Array.Clear(supportCounts,0,supportCounts.Length);Array.Clear(displayedSupportPixels,0,displayedSupportPixels.Length);
            foreach(var rt in new[]{color,height,scratchColor,scratchHeight,displayed,displayedHeight})Clear(rt);
        }
        static void Clear(RenderTexture target)
        {var prior=RenderTexture.active;try{RenderTexture.active=target;GL.Clear(false,true,Color.clear);}finally{RenderTexture.active=prior;}}
        void RequestCompletion()
        {
            if(gpuPending || displayed==null)return;
            ulong through=submittedThrough;uint version=generation;
            if(!SystemInfo.supportsAsyncGPUReadback)
            {
                completionPixel??=new Texture2D(1,1,TextureFormat.RGBA32,false,true){hideFlags=HideFlags.DontSave};
                var prior=RenderTexture.active;try{RenderTexture.active=displayed;completionPixel.ReadPixels(new Rect(0,0,1,1),0,0,false);RenderedThrough=through;}finally{RenderTexture.active=prior;}return;
            }
            gpuPending=true;RenderTexture texture=displayed;pendingTarget=texture;
            gpuRequest=AsyncGPUReadback.Request(texture,0,0,1,0,1,0,1,TextureFormat.RGBA32,request=>
            {
                bool release=releasePendingTarget;releasePendingTarget=false;gpuPending=false;pendingTarget=null;
                if(!disposed && version==generation)
                {if(request.hasError)Error="Clay GPU completion readback failed.";else RenderedThrough=Math.Max(RenderedThrough,through);}
                if(release)Release(texture);
            });
#if UNITY_EDITOR
            gpuRequest.forcePlayerLoopUpdate=true;
#endif
        }
        void ReleaseMaps()
        {
            foreach(var rt in new[]{color,height,scratchColor,scratchHeight,displayed,displayedHeight})
            {if(gpuPending && rt==pendingTarget)releasePendingTarget=true;else Release(rt);}
            color=height=scratchColor=scratchHeight=displayed=displayedHeight=null;
            LabVisualSafety.DestroyVisualObject(cellMask);LabVisualSafety.DestroyVisualObject(support);LabVisualSafety.DestroyVisualObject(fallback);
            cellMask=support=fallback=null;
            supportCounts=null;displayedSupportPixels=null;
        }
        static bool Created(RenderTexture value)=>value!=null && value.IsCreated();
        static void Release(RenderTexture value){if(value==null)return;value.Release();LabVisualSafety.DestroyVisualObject(value);}
        static uint Mix(uint x){unchecked{x^=x>>16;x*=0x7feb352d;x^=x>>15;x*=0x846ca68b;return x^(x>>16);}}
        public void Dispose()
        {if(disposed)return;disposed=true;generation++;ReleaseMaps();LabVisualSafety.DestroyVisualObject(material);LabVisualSafety.DestroyVisualObject(completionPixel);material=null;completionPixel=null;descriptors.Clear();descriptorOrder.Clear();pendingCells.Clear();}
    }
}
