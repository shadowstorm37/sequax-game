using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Full-screen post-process pass that copies the camera's color into a global
/// texture (_VisionSceneColor) and draws VisionDesaturate.shader, which reads
/// it back and writes the desaturated result to the camera color target.
///
/// Written by hand instead of using URP's built-in Full Screen Pass Renderer
/// Feature (whose "Fetch Color Buffer" didn't return real scene data here) and
/// instead of _CameraOpaqueTexture (Renderer2D doesn't support it - see
/// ScriptableRenderer.SupportsCameraOpaque(), unoverridden by Renderer2D).
///
/// The copy texture is a persistent, imported RTHandle (allocated once, reused
/// every frame) rather than a fresh transient texture per frame - an earlier
/// version using a transient renderGraph.CreateTexture() caused a feedback-loop
/// strobe, most visible while moving.
///
/// SETUP (in the Unity Editor, on Assets/Settings/Renderer2D.asset):
/// 1. Add Renderer Feature -> Vision Desaturate Feature.
/// 2. Create a Material using the FullScreen/VisionDesaturate shader, assign
///    it to this feature's "Material" field.
/// </summary>
public class VisionDesaturateFeature : ScriptableRendererFeature
{
    [SerializeField] private Material material;
    [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

    private VisionDesaturatePass pass;

    public override void Create()
    {
        pass = new VisionDesaturatePass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null) return;

        pass.renderPassEvent = renderPassEvent;
        pass.Setup(material);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
    }

    private class VisionDesaturatePass : ScriptableRenderPass
    {
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly MaterialPropertyBlock s_PropertyBlock = new MaterialPropertyBlock();

        private Material material;
        private RTHandle colorCopy;

        public void Setup(Material mat)
        {
            material = mat;
        }

        public void Dispose()
        {
            colorCopy?.Release();
        }

        private class CopyPassData
        {
            public TextureHandle source;
        }

        private class ApplyPassData
        {
            public Material material;
            public TextureHandle source;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle source = resourceData.activeColorTexture;

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateHandleIfNeeded(ref colorCopy, desc, name: "_VisionSceneColorCopy");

            TextureHandle colorCopyHandle = renderGraph.ImportTexture(colorCopy);

            using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>("VisionDesaturate_Copy", out var copyData))
            {
                copyData.source = source;
                builder.UseTexture(copyData.source, AccessFlags.Read);
                builder.SetRenderAttachment(colorCopyHandle, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((CopyPassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), 0f, false);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<ApplyPassData>("VisionDesaturate_Apply", out var applyData))
            {
                applyData.material = material;
                applyData.source = colorCopyHandle;
                builder.UseTexture(applyData.source, AccessFlags.Read);
                builder.SetRenderAttachment(source, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((ApplyPassData data, RasterGraphContext ctx) =>
                {
                    s_PropertyBlock.Clear();
                    s_PropertyBlock.SetTexture(BlitTextureId, data.source);
                    s_PropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1, 1, 0, 0));
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, s_PropertyBlock);
                });
            }
        }
    }
}
