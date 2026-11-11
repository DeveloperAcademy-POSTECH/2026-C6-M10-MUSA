using System.Collections.Generic;
using C6Lab;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Hapio
{
    /// <summary>
    /// The hanging gourd swings and bounces below the ground plane (rope cycle + hit bounce), where the opaque ground
    /// hid its lower part with a flat line that read as a cut frame. Presentation only: after the opaque pass, the
    /// pixels where the ground is the visible surface are marked in the stencil, and the gourd is drawn again only
    /// there and only where it lies behind the ground. The gourd's height, motion, size, the camera, the ground and every
    /// other object (orbs, scenery partly sunk into the ground) draw exactly as before.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    public sealed class GameGourdOverGround : MonoBehaviour
    {
        // Rendering-layer bits only select renderers for this pass; light layers keep the renderers' existing bits.
        internal const uint GroundBit = 1u << 20, GourdBit = 1u << 21;
        private const float RescanSeconds = .25f;

        private LabTarget target;
        private Transform ground;
        private Scene scene;
        private GourdOverGroundPass pass;
        private float nextScan;
        private readonly List<Renderer> renderers = new List<Renderer>();

        public void Bind(LabTarget gourd, Collider battleFloor)
        {
            target = gourd;
            ground = battleFloor != null ? battleFloor.transform : null;
            scene = gameObject.scene;
            pass ??= new GourdOverGroundPass();
            nextScan = 0f;
        }

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += Enqueue;
        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= Enqueue;

        private void LateUpdate()
        {
            // The stage gourd, its paint/clay layers and the ground are created or swapped while loading; mark late arrivals.
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + RescanSeconds;
            Mark(target != null ? target.transform : null, GourdBit);
            Mark(ground, GroundBit);
        }

        private void Mark(Transform root, uint bit)
        {
            if (root == null) return;
            root.GetComponentsInChildren(true, renderers);
            foreach (Renderer renderer in renderers)
                if ((renderer.renderingLayerMask & bit) == 0) renderer.renderingLayerMask |= bit;
            renderers.Clear();
        }

        private void Enqueue(ScriptableRenderContext context, Camera camera)
        {
            // Every camera of this Battle Scene that draws the stage: the battle view, its continuation below the
            // board and the final-hit replay. Preview and UI cameras of other Scenes see none of the marked renderers.
            if (pass == null || target == null || camera.gameObject.scene != scene || camera.cameraType != CameraType.Game) return;
            if ((camera.cullingMask & (1 << target.gameObject.layer)) == 0) return;
            var data = camera.GetUniversalAdditionalCameraData();
            if (data == null || data.scriptableRenderer == null || data.renderType != CameraRenderType.Base) return;
            data.scriptableRenderer.EnqueuePass(pass);
        }

        private sealed class GourdOverGroundPass : ScriptableRenderPass
        {
            private const int StencilBit = 1 << 6;
            private static readonly ShaderTagId[] Tags =
                { new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit") };
            private readonly List<ShaderTagId> tags = new List<ShaderTagId>(Tags);
            private static readonly ShaderTagId[] TagValues = { ShaderTagId.none };
            private static readonly RenderStateBlock[] StateBuffer = new RenderStateBlock[1];

            private sealed class PassData
            {
                internal RendererListHandle ground, gourd;
            }

            internal GourdOverGroundPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                profilingSampler = new ProfilingSampler("Hapio gourd over ground");
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var lightData = frameData.Get<UniversalLightData>();
                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.activeColorTexture.IsValid() || !resources.activeDepthTexture.IsValid()) return;

                using var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
                if (resources.mainShadowsTexture.IsValid()) builder.UseTexture(resources.mainShadowsTexture, AccessFlags.Read);
                if (resources.additionalShadowsTexture.IsValid()) builder.UseTexture(resources.additionalShadowsTexture, AccessFlags.Read);

                DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(tags, renderingData, cameraData, lightData,
                    cameraData.defaultOpaqueSortFlags);

                // 1. The ground's own visible pixels (same mesh, same depth): no colour, stencil bit set.
                var groundState = new RenderStateBlock(RenderStateMask.Depth | RenderStateMask.Stencil | RenderStateMask.Blend)
                {
                    depthState = new DepthState(false, CompareFunction.Equal),
                    blendState = new BlendState { blendState0 = new RenderTargetBlendState((ColorWriteMask)0) },
                    stencilReference = StencilBit,
                    stencilState = new StencilState(true, (byte)StencilBit, (byte)StencilBit, CompareFunction.Always,
                        StencilOp.Replace, StencilOp.Keep, StencilOp.Keep)
                };
                // 2. The gourd only on those pixels and only where it is behind the ground (its sunk part).
                var gourdState = new RenderStateBlock(RenderStateMask.Depth | RenderStateMask.Stencil)
                {
                    depthState = new DepthState(true, CompareFunction.Greater),
                    stencilReference = StencilBit,
                    stencilState = new StencilState(true, (byte)StencilBit, 0, CompareFunction.Equal,
                        StencilOp.Keep, StencilOp.Keep, StencilOp.Keep)
                };
                passData.ground = List(renderGraph, renderingData, drawing, GroundBit, groundState);
                passData.gourd = List(renderGraph, renderingData, drawing, GourdBit, gourdState);
                builder.UseRendererList(passData.ground);
                builder.UseRendererList(passData.gourd);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.ground);
                    context.cmd.DrawRendererList(data.gourd);
                });
            }

            private static RendererListHandle List(RenderGraph renderGraph, UniversalRenderingData renderingData,
                DrawingSettings drawing, uint bit, RenderStateBlock state)
            {
                var filtering = new FilteringSettings(RenderQueueRange.opaque, ~0, bit);
                var tagValues = new Unity.Collections.NativeArray<ShaderTagId>(TagValues, Unity.Collections.Allocator.Temp);
                StateBuffer[0] = state;
                var states = new Unity.Collections.NativeArray<RenderStateBlock>(StateBuffer, Unity.Collections.Allocator.Temp);
                var parameters = new RendererListParams(renderingData.cullResults, drawing, filtering)
                {
                    tagValues = tagValues, stateBlocks = states, isPassTagName = false
                };
                return renderGraph.CreateRendererList(parameters);
            }
        }
    }
}
