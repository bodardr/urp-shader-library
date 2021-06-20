using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

        public string destinationName = "_Blit";
    }
}

public class BlitRenderPass : ScriptableRenderPass
{
    private Material blitMaterial;

    private int destinationID = 0;
    private int downsample;

    private int passCount;
    private int temp2ID = Shader.PropertyToID("_Temp2");

    private int tempID = Shader.PropertyToID("_Temp1");

    public BlitRenderPass(BlitFeature.Settings settings)
    {
        UpdateSettings(settings);
    }

    public void UpdateSettings(BlitFeature.Settings settings)
    {
        renderPassEvent = settings.renderingEvent;

        blitMaterial = settings.blitMaterial;

        if (destinationID > 0)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            cmd.ReleaseTemporaryRT(destinationID);
            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        destinationID = Shader.PropertyToID(settings.destinationName);
        passCount = settings.passCount;
        downsample = settings.downsample;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get("Blit Render Feature");

        using (new ProfilingScope(cmd, profilingSampler))
        {
            var cameraData = renderingData.cameraData;

            var colorTarget = cameraData.renderer.cameraColorTarget;

            var targetDescriptor = cameraData.cameraTargetDescriptor;
            targetDescriptor.width /= downsample;
            targetDescriptor.height /= downsample;

            cmd.GetTemporaryRT(tempID, targetDescriptor);
            cmd.GetTemporaryRT(temp2ID, targetDescriptor);
            cmd.GetTemporaryRT(destinationID, targetDescriptor);

            cmd.Blit(colorTarget, tempID, blitMaterial);

            if (passCount > 1)
            {
                for (int i = 0; i < passCount - 1; i++)
                {
                    cmd.Blit(tempID, temp2ID, blitMaterial);

                    var swap = tempID;

                    tempID = temp2ID;
                    temp2ID = swap;
                }
            }

            cmd.Blit(tempID, destinationID);
            cmd.SetGlobalTexture(destinationID, tempID);
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}