using System;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public class BlitFeature : ScriptableRendererFeature
{
    [SerializeField]
    private Settings blitSettings = new Settings();

    private BlitRenderPass blitPass;

    public override void Create()
    {
        blitPass = new BlitRenderPass(blitSettings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(blitPass);
    }

    void Update()
    {
        if (blitSettings.blitMaterial != null)
            blitPass.UpdateSettings(blitSettings);
    }

    [Serializable]
    public class Settings
    {
        public RenderPassEvent renderingEvent;

        public Material blitMaterial;

        [Range(1, 8)]
        public int passCount = 1;

        [Range(1, 8)]
        public int downsample = 1;
        
        public bool useCustomInputTexture = false;
        public string customInputTextureName = "";

        public bool blitToActiveFramebuffer;
        public bool setGlobalTexture;
        public string outputTextureName = "_Blit";
    }
}

public class BlitRenderPass : ScriptableRenderPass
{
    private BlitFeature.Settings settings;

    public class PassData
    {
        public bool useCustomInput;

        public TextureHandle inputTexture;
        public TextureHandle outputTexture;
        public TextureHandle tempTexture;
        public TextureHandle temp2Texture;

        public int passCount;

        public Material blitMaterial;
        public string outputTextureName;
        public bool setGlobalTexture;
    }

    public BlitRenderPass(BlitFeature.Settings settings)
    {
        UpdateSettings(settings);
    }

    public void UpdateSettings(BlitFeature.Settings settings)
    {
        this.settings = settings;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        base.RecordRenderGraph(renderGraph, frameData);

        var resourceData = frameData.Get<UniversalResourceData>();

        using (var builder = renderGraph.AddUnsafePass("Blit Render Feature", out PassData passData))
        {
            passData.blitMaterial = settings.blitMaterial;
            passData.setGlobalTexture = settings.setGlobalTexture;

            passData.inputTexture = resourceData.activeColorTexture;
            builder.UseTexture(passData.inputTexture);

            var textureDescriptor = renderGraph.GetTextureDesc(passData.outputTexture);
            textureDescriptor.width /= settings.downsample;
            textureDescriptor.height /= settings.downsample;

            textureDescriptor.name = "Blit_Temp1";
            var tempTexture1 = renderGraph.CreateTexture(textureDescriptor);
            passData.tempTexture = tempTexture1;
            
            textureDescriptor.name = "Blit_Temp2";
            var tempTexture2 = renderGraph.CreateTexture(textureDescriptor);
            passData.temp2Texture = tempTexture2;

            if (!settings.blitToActiveFramebuffer)
            {
                textureDescriptor.name = settings.outputTextureName;
                passData.outputTextureName = settings.outputTextureName;
                passData.outputTexture = renderGraph.CreateTexture(textureDescriptor);
            }
            passData.outputTexture = resourceData.activeColorTexture;

            passData.setGlobalTexture = settings.setGlobalTexture;
            passData.outputTextureName = settings.outputTextureName;
            
            builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => Execute(data, context));
        }
    }


    private static void Execute(PassData passData, UnsafeGraphContext context)
    {
        Blit(passData.outputTexture, passData.tempTexture, passData.blitMaterial);

        var tempTexture = passData.tempTexture;
        var temp2Texture = passData.temp2Texture;

        if (passData.passCount > 1)
        {
            for (int i = 0; i < passData.passCount - 1; i++)
            {
                Blit(tempTexture, temp2Texture, passData.blitMaterial);
                (tempTexture, temp2Texture) = (temp2Texture, tempTexture);
            }
        }

        Blit(tempTexture, passData.outputTexture);

        if (passData.setGlobalTexture)
            context.cmd.SetGlobalTexture(passData.outputTextureName, passData.outputTexture);


        void Blit(TextureHandle source, TextureHandle destination, Material blitMaterial = null,
            int blitMaterialPass = 0)
        {
            context.cmd.SetRenderTarget(destination);
            Blitter.BlitTexture(context.cmd, source, new Vector4(1, 1, 0, 0), blitMaterial, blitMaterialPass);
        }
    }
}
