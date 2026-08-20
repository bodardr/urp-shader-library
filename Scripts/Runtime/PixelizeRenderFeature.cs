using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class PixelizeRenderFeature : ScriptableRendererFeature
{
    [Serializable]
    public class Settings
    {
        public RenderPassEvent PassEvent = RenderPassEvent.AfterRenderingOpaques;
        public LayerMask LayerMask;
        public float PixelsPerUnit;
    }

    [SerializeField]
    private Settings settings;

    private PixelizePass pass;

    public override void Create()
    {
        pass = new PixelizePass
        {
            renderPassEvent = settings.PassEvent,
            settings = settings
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(pass);
    }
}

public class PixelizePass : ScriptableRenderPass
{
    private static readonly int camBoundsID = Shader.PropertyToID("_CamBounds");
    private static readonly int pixelsPerUnitID = Shader.PropertyToID("_PixelsPerUnit");

    private readonly List<ShaderTagId> shaderTagIdList;
    private RenderStateBlock renderStateBlock;

    private Material pixelizeMat;

    public PixelizeRenderFeature.Settings settings;

    private class GeometryPassData
    {
        public RendererListHandle rendererList;
    }

    private class BlitPassData
    {
        public Material material;
        public TextureHandle source;
        public Vector4 camBounds;
        public float pixelsPerUnit;
    }

    public PixelizePass()
    {
        CreatePixelizeMat();

        shaderTagIdList = new List<ShaderTagId>
        {
            new("UniversalForward"),
            new("UniversalGBuffer"),
            new("LightweightForward"),
            new("SRPDefaultUnlit"),
        };

        renderStateBlock = new RenderStateBlock(RenderStateMask.Depth)
        {
            depthState = new DepthState(true)
        };

        profilingSampler = new ProfilingSampler(nameof(PixelizePass));
    }

    private void CreatePixelizeMat()
    {
        var shader = Shader.Find("Hidden/PixelizePass");
        if (shader != null)
            pixelizeMat = new Material(shader);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var cameraData = frameData.Get<UniversalCameraData>();
        var renderingData = frameData.Get<UniversalRenderingData>();
        var lightData = frameData.Get<UniversalLightData>();
        var resourceData = frameData.Get<UniversalResourceData>();

        if (pixelizeMat == null)
            CreatePixelizeMat();

        if (pixelizeMat == null)
            return;

        var cam = cameraData.camera;
        var botLeft = cam.ViewportToWorldPoint(Vector2.zero);
        var topRight = cam.ViewportToWorldPoint(Vector2.one);

        // 1. Allocate the temporary render target via RenderGraph
        var desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
        desc.name = "_PixelizeLayerRender";
        var tempTexture = renderGraph.CreateTexture(desc);

        // 2. Build the RendererList for the specified layer mask
        var drawingSettings = RenderingUtils.CreateDrawingSettings(shaderTagIdList, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
        var filteringSettings = new FilteringSettings(RenderQueueRange.all, settings.LayerMask);
        var rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
        var rendererListHandle = renderGraph.CreateRendererList(rendererListParams);

        // 3. Raster Pass: Draw objects to the temporary texture
        using (var builder = renderGraph.AddRasterRenderPass<GeometryPassData>("Pixelize Render Objects", out var passData))
        {
            passData.rendererList = rendererListHandle;

            builder.UseRendererList(rendererListHandle);
            builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write);
            builder.SetRenderAttachmentDepth(tempTexture, AccessFlags.Write);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc((GeometryPassData data, RasterGraphContext context) =>
            {
                context.cmd.ClearRenderTarget(true, true, Color.clear);
                context.cmd.DrawRendererList(data.rendererList);
            });
        }

        // 4. Raster Pass: Blit from temporary texture to camera color target
        using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>("Pixelize Blit", out var passData))
        {
            passData.material = pixelizeMat;
            passData.source = tempTexture;
            passData.camBounds = new Vector4(botLeft.x, botLeft.y, topRight.x, topRight.y);
            passData.pixelsPerUnit = settings.PixelsPerUnit;

            builder.UseTexture(tempTexture);
            builder.SetRenderAttachment(resourceData.cameraColor, 0);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) =>
            {
                data.material.SetVector(camBoundsID, data.camBounds);
                data.material.SetFloat(pixelsPerUnitID, data.pixelsPerUnit);
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
            });
        }
    }
}