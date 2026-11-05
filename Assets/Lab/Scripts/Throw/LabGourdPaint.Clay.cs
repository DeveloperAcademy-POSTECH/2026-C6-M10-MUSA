using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace C6Lab
{
    public sealed partial class LabGourdPaint
    {
        private LabClayStickerRuntime clayStickers;
        private sealed class ClayLayer
        {
            public Renderer source;
            public MeshRenderer coat;
        }
        // Each paintable rig part keeps a coat under its own animated transform. The
        // legacy white gourd has one entry; stage rigs also have their painted lids.
        private readonly List<ClayLayer> clayLayers = new List<ClayLayer>();
        private readonly HashSet<Renderer> unsupportedClaySources = new HashSet<Renderer>();
        private Material clayMaterial;
        private MaterialPropertyBlock clayBlock;
        private float appliedClayThickness = -1f, appliedClayGloss = -1f;
        public bool ClayStickersActive => appliedStampEnabled && clayStickers != null
            && clayStickers.PaintTexture != null && clayStickers.Error == null;
        public Texture VisualHeightTexture => ClayStickersActive ? clayStickers.HeightTexture : null;
        public string ClayError => clayStickers?.Error;

        bool ClaySettingsChanged()
        {
            if (clayLayers.Count == 0 || !ClayStickersActive) return false;
            if (appliedClayThickness != ClayThickness || appliedClayGloss != ClayGloss) return true;
            foreach (ClayLayer layer in clayLayers)
                if (layer.source != null && layer.coat != null && layer.coat.enabled != layer.source.enabled) return true;
            return false;
        }
        float ClayThickness => network != null ? network.ClayThickness : .006f;
        float ClayGloss => network != null ? network.ClayGloss : .25f;

        bool ClayPending(LabPaintArtJournal journal) => clayStickers == null || clayStickers.Error == null
            && (!clayStickers.Ready || clayStickers.AppliedThrough != journal.LastSequence
                || reportedStampApplied != clayStickers.AppliedThrough || reportedStampRendered != clayStickers.RenderedThrough);

        void UpdateClay(LabPaintArtJournal journal, LabWatercolorStampSettings settings, int columns, bool rebuild)
        {
            clayStickers ??= new LabClayStickerRuntime();
            clayStickers.Update(journal, HighResolutionActive ? highResolution.PaintTexture : paintTexture,
                VisualPainterTexture, columns, settings, network.BattlePresentationTime, rebuild);
            if (clayStickers.Error != null || clayStickers.Ready) network.ReportPaintArtRenderingError(clayStickers.Error);
            network.ReportPaintArtRendered(clayStickers.AppliedThrough, clayStickers.RenderedThrough);
            reportedStampApplied=clayStickers.AppliedThrough;reportedStampRendered=clayStickers.RenderedThrough;
        }

        void BindClay(bool active, bool playerPatterns)
        {
            if (!active)
            {
                foreach (ClayLayer layer in clayLayers) if (layer.coat != null) layer.coat.enabled=false;
                return;
            }
            if (clayMaterial == null)
            {
                Shader shader=Resources.Load<Shader>(LabClayStickerSettings.ResourceFolder+"ClayStickerSurface");
                if(shader==null || !shader.isSupported)
                { network.ReportPaintArtRenderingError("The clay overlay shader is unavailable.");return; }
                clayMaterial=new Material(shader) {name="Clay sticker overlay (runtime)",hideFlags=HideFlags.DontSave};
            }
            EnsureClayLayer(shell);
            foreach (Renderer extra in extras) if (extra != null) EnsureClayLayer(extra);
            clayBlock ??= new MaterialPropertyBlock();
            clayBlock.SetTexture("_PaintMap",clayStickers.PaintTexture);
            clayBlock.SetTexture("_HeightMap",clayStickers.HeightTexture);
            clayBlock.SetTexture("_PainterMap",VisualPainterTexture);
            clayBlock.SetFloat("_PaintMapReady",1f);
            clayBlock.SetFloat("_ClayThickness",ClayThickness);
            clayBlock.SetFloat("_ClayGloss",ClayGloss);
            clayBlock.SetFloat("_PlayerPaintPatternsEnabled",playerPatterns?1f:0f);
            foreach (ClayLayer layer in clayLayers)
            {
                if (layer.source == null || layer.coat == null) continue;
                layer.coat.SetPropertyBlock(clayBlock);
                layer.coat.enabled=layer.source.enabled;
            }
            appliedClayThickness=ClayThickness;appliedClayGloss=ClayGloss;
        }

        void EnsureClayLayer(Renderer source)
        {
            if (source == null || HasClayLayer(source) || unsupportedClaySources.Contains(source)) return;
            MeshFilter mesh = source.GetComponent<MeshFilter>();
            if (!(source is MeshRenderer) || mesh == null || mesh.sharedMesh == null)
            {
                // Rig validation currently permits Renderer extras. A future unsupported
                // part must retain its ordinary paint instead of being made invisible.
                unsupportedClaySources.Add(source);
                Debug.LogWarning("C6_CLAY_EXTRA_FALLBACK renderer="+source.name+" requires a MeshRenderer and MeshFilter.");
                return;
            }
            var go=new GameObject("Clay Sticker Overlay") {layer=source.gameObject.layer};
            go.transform.SetParent(source.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
            MeshRenderer coat=go.AddComponent<MeshRenderer>();
            coat.sharedMaterial=clayMaterial;
            coat.shadowCastingMode=ShadowCastingMode.Off;coat.receiveShadows=false;
            coat.renderingLayerMask=source.renderingLayerMask;
            coat.sortingLayerID=source.sortingLayerID;coat.sortingOrder=source.sortingOrder;
            // Preserve conservative visibility even when the vertex shader raises the coat.
            Bounds bounds=mesh.sharedMesh.bounds;bounds.Expand(.05f);coat.localBounds=bounds;
            clayLayers.Add(new ClayLayer {source=source,coat=coat});
        }

        bool HasClayLayer(Renderer source)
        {
            foreach (ClayLayer layer in clayLayers)
                if (layer.source == source && layer.coat != null) return true;
            return false;
        }

        void BindClayBase(Renderer source, bool clayActive)
        {
            bool coated=clayActive && HasClayLayer(source);
            block.SetFloat("_PaintMapReady",coated?0f:1f);
            Material material=source.sharedMaterial;
            if(material!=null && material.HasProperty("_PaperContrast"))
                block.SetFloat("_PaperContrast",coated?0f:material.GetFloat("_PaperContrast"));
            if(material!=null && material.HasProperty("_PaperBump"))
                block.SetFloat("_PaperBump",coated?0f:material.GetFloat("_PaperBump"));
            source.SetPropertyBlock(block);
        }

        void ReleaseClay()
        {
            foreach (ClayLayer layer in clayLayers) if(layer.coat!=null)layer.coat.enabled=false;
            clayStickers?.Dispose();clayStickers=null;
            foreach (ClayLayer layer in clayLayers)
                if(layer.coat!=null)LabVisualSafety.DestroyVisualObject(layer.coat.gameObject);
            if(clayMaterial!=null)LabVisualSafety.DestroyVisualObject(clayMaterial);
            clayLayers.Clear();unsupportedClaySources.Clear();clayMaterial=null;clayBlock=null;
            appliedClayThickness=appliedClayGloss=-1;
        }
    }
}
