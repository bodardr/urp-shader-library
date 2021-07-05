using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PixelizeFeature : ScriptableRendererFeature
{
    [SerializeField]
    private Settings pixelizeSettings;

    private PixelizeRenderPass pixelizePass;

    void OnValidate()
    {
        if (pixelizePass != null && pixelizeSettings != null)
            pixelizePass.UpdateSettings(pixelizeSettings);
    }

    public override void Create()
    {
        pixelizePass = new PixelizeRenderPass(pixelizeSettings);
        OnValidate();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(pixelizePass);
    }

    [Serializable]
    public class Settings
    {
        public LayerMask layerMask = 0;
        public float pixelsPerUnit = 16;
        public RenderPassEvent renderPassEvent;

        public Color outlineColor;
        public float outlineThickness = 1;
    }
}

public class PixelizeRenderPass : ScriptableRenderPass
{
    private static readonly int outlineColorID = Shader.PropertyToID("_OutlineColor");
    private static readonly int pixelizeTextureID = Shader.PropertyToID("_PixelizeTexture");
    private static readonly int pixelizeDepthID = Shader.PropertyToID("_PixelizeLayerDepth");

    private static readonly int pixelsPerUnitID = Shader.PropertyToID("_PixelsPerUnit");
    private static readonly int camToWorld = Shader.PropertyToID("_CamToWorld");

    private static readonly int tempTextureID = Shader.PropertyToID("_Temp1");
    private static readonly int outlineThicknessID = Shader.PropertyToID("_OutlineThickness");

    private FilteringSettings filteringSettings;

    private int layerMask = 0;
    private Material outlineMaterial;

    private Material pixelizeMaterial;
    private RenderStateBlock renderStateBlock;
    private List<ShaderTagId> shaderTagIdList;


    public PixelizeRenderPass(PixelizeFeature.Settings settings)
    {
        profilingSampler = new ProfilingSampler("Pixelize Feature");
        pixelizeMaterial = new Material(Shader.Find("CustomPostProcess/PixelizeEffect"));
        outlineMaterial = new Material(Shader.Find("Blit/Alpha"));

        filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        shaderTagIdList = new List<ShaderTagId>
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalGBuffer"),
            new ShaderTagId("LightweightForward"),
            new ShaderTagId("SRPDefaultUnlit"),
        };

        renderStateBlock = new RenderStateBlock(RenderStateMask.Depth);

        UpdateSettings(settings);
    }

    public void UpdateSettings(PixelizeFeature.Settings settings)
    {
        renderPassEvent = settings.renderPassEvent;

        layerMask = settings.layerMask;

        filteringSettings.layerMask = layerMask;

        pixelizeMaterial.SetFloat(pixelsPerUnitID, settings.pixelsPerUnit);

        outlineMaterial.SetColor(outlineColorID, settings.outlineColor);
        outlineMaterial.SetFloat(outlineThicknessID, settings.outlineThickness);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        ref var camData = ref renderingData.cameraData;
        var cam = camData.camera;

        CommandBuffer cmd = CommandBufferPool.Get("Pixelize Cmd");

        var drawingSettings =
            CreateDrawingSettings(shaderTagIdList, ref renderingData, SortingCriteria.CommonTransparent);

        var cameraTargetDescriptor = camData.cameraTargetDescriptor;

        using (new ProfilingScope(cmd, profilingSampler))
        {
            cameraTargetDescriptor.depthBufferBits = 24;
            cmd.GetTemporaryRT(tempTextureID, cameraTargetDescriptor, FilterMode.Point);

            if (!cam.TryGetCullingParameters(out var culling))
                return;

            culling.cullingMask = Convert.ToUInt32(layerMask);
            var cullingResults = context.Cull(ref culling);

            cameraTargetDescriptor.depthBufferBits = 0;
            cmd.GetTemporaryRT(pixelizeTextureID, cameraTargetDescriptor, FilterMode.Point);

            cameraTargetDescriptor.depthBufferBits = 24;
            cameraTargetDescriptor.colorFormat = RenderTextureFormat.Depth;
            cmd.GetTemporaryRT(pixelizeDepthID, cameraTargetDescriptor, FilterMode.Point);

            cmd.SetRenderTarget(pixelizeTextureID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                pixelizeDepthID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            cmd.ClearRenderTarget(true, true, Color.clear);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);

            var viewToWorldMatrix = cam.worldToCameraMatrix.inverse;
            var screenToViewMatrix = cam.projectionMatrix.inverse;

            screenToViewMatrix[1, 1] *= -1;

            pixelizeMaterial.SetMatrixArray(camToWorld, new[]
            {
                screenToViewMatrix,
                viewToWorldMatrix,
                cam.worldToCameraMatrix,
                cam.projectionMatrix
            });

            cmd.Blit(pixelizeTextureID, tempTextureID, pixelizeMaterial);
            cmd.SetRenderTarget(camData.renderer.cameraColorTarget, camData.renderer.cameraDepthTarget);
            cmd.Blit(tempTextureID, BuiltinRenderTextureType.CurrentActive, outlineMaterial);

            cmd.ReleaseTemporaryRT(tempTextureID);
            cmd.ReleaseTemporaryRT(pixelizeTextureID);
            cmd.ReleaseTemporaryRT(pixelizeDepthID);
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}