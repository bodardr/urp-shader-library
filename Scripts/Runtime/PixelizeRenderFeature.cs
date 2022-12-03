using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PixelizeRenderFeature : ScriptableRendererFeature
{
    [Serializable]
    public class Settings
    {
        public RenderPassEvent PassEvent;
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
    private static readonly int renderTempID = Shader.PropertyToID("_PixelizeLayerRender");
    private static readonly int pixelizeTempID = Shader.PropertyToID("_PixelizeTex");
    private static readonly int camBoundsID = Shader.PropertyToID("_CamBounds");
    private static readonly int pixelsPerUnitID = Shader.PropertyToID("_PixelsPerUnit");

    private readonly List<ShaderTagId> shaderTagIdList;
    private RenderStateBlock renderStateBlock;

    private Material pixelizeMat;

    public PixelizeRenderFeature.Settings settings;

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

        renderStateBlock = new RenderStateBlock(RenderStateMask.Depth);
        renderStateBlock.depthState = new DepthState(true);
        profilingSampler = new ProfilingSampler(nameof(PixelizePass));
    }

    private void CreatePixelizeMat()
    {
        pixelizeMat = new Material(Shader.Find("Hidden/PixelizePass"));
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        cmd.GetTemporaryRT(renderTempID, renderingData.cameraData.cameraTargetDescriptor, FilterMode.Point);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get();

        using (new ProfilingScope(cmd, profilingSampler))
        {
            var cam = renderingData.cameraData.camera;

            if (!cam.TryGetCullingParameters(out var cullingParameters))
                return;

            cullingParameters.cullingMask |= Convert.ToUInt32(settings.LayerMask);
            var cullResults = context.Cull(ref cullingParameters);

            var sortingCriteria = SortingCriteria.CommonOpaque;

            var drawingSettings = CreateDrawingSettings(shaderTagIdList, ref renderingData, sortingCriteria);
            var filteringSettings = new FilteringSettings(RenderQueueRange.all, settings.LayerMask);

            cmd.SetRenderTarget(renderTempID);
            cmd.ClearRenderTarget(true, true, Color.clear);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            context.DrawRenderers(cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);
            context.Submit();

            var botLeft = cam.ViewportToWorldPoint(Vector2.zero);
            var topRight = cam.ViewportToWorldPoint(Vector2.one);

            if (pixelizeMat == null)
                CreatePixelizeMat();

            pixelizeMat.SetVector(camBoundsID, new Vector4(botLeft.x, botLeft.y, topRight.x, topRight.y));
            pixelizeMat.SetFloat(pixelsPerUnitID, settings.PixelsPerUnit);

            Blit(cmd, renderTempID, renderingData.cameraData.renderer.cameraColorTarget, pixelizeMat, 0);
            context.ExecuteCommandBuffer(cmd);

            CommandBufferPool.Release(cmd);
        }
    }

    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(renderTempID);
        //cmd.ReleaseTemporaryRT(pixelizeTempID);
    }
}