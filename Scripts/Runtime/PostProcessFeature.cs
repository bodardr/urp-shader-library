using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class PostProcessFeature : ScriptableRendererFeature
{
    [SerializeField]
    private Settings postSettings = new Settings();

    private PostProcessRenderPass postProcessPass;

    public override void Create()
    {
        postProcessPass = new PostProcessRenderPass(postSettings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (postSettings.blitMaterial != null)
            postProcessPass.UpdateSettings(postSettings);

        renderer.EnqueuePass(postProcessPass);
    }

    [Serializable]
    public class Settings
    {
        public Material blitMaterial;

        [Range(1, 8)]
        public int passCount = 1;
    }
}

public class PostProcessRenderPass : ScriptableRenderPass
{
    private int passCount;
    private Material postMaterial;

    private class BlitPassData
    {
        public Material material;
        public TextureHandle source;
    }

    public PostProcessRenderPass(PostProcessFeature.Settings settings)
    {
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        profilingSampler = new ProfilingSampler(nameof(PostProcessRenderPass));
        UpdateSettings(settings);
    }

    public void UpdateSettings(PostProcessFeature.Settings settings)
    {
        postMaterial = settings.blitMaterial;
        passCount = settings.passCount;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (postMaterial == null || passCount <= 0)
            return;

        var cameraData = frameData.Get<UniversalCameraData>();
        var resourceData = frameData.Get<UniversalResourceData>();

        if (!resourceData.cameraColor.IsValid())
            return;

        var desc = cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0;

        // 1. Allocate transient render targets via RenderGraph
        var temp1 = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_PostProcessTemp1", false);
        var temp2 = passCount > 1
            ? UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_PostProcessTemp2", false)
            : TextureHandle.nullHandle;

        TextureHandle src = resourceData.cameraColor;
        TextureHandle dst = temp1;

        // 2. Initial pass: CameraColor -> Temp1
        AddBlitPass(renderGraph, "PostProcess Blit 0", src, dst, postMaterial);

        src = temp1;
        dst = temp2;

        // 3. Intermediate ping-pong passes
        for (int i = 1; i < passCount; i++)
        {
            AddBlitPass(renderGraph, $"PostProcess Blit {i}", src, dst, postMaterial);
            (src, dst) = (dst, src);
        }

        // 4. Final pass: Copy result back to CameraColor
        AddBlitPass(renderGraph, "PostProcess Blit Final", src, resourceData.cameraColor, null);
    }

    private void AddBlitPass(RenderGraph renderGraph, string passName, TextureHandle source, TextureHandle destination, Material material)
    {
        using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>(passName, out var passData))
        {
            passData.material = material;
            passData.source = source;

            builder.UseTexture(source);
            builder.SetRenderAttachment(destination, 0);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) =>
            {
                if (data.material != null)
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                else
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0.0f, false);
            });
        }
    }
}