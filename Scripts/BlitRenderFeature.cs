using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BlitRenderFeature : ScriptableRendererFeature
{
    [Serializable]
    public class Settings
    {
        public RenderPassEvent renderingEvent;

        public Material blitMaterial;

        [Range(1, 8)]
        public int passCount = 1;

        [Range(1, 8)]
        public int downsample = 1;

        public string destinationID;
    }

    private BlitRenderPass blitPass;

    [SerializeField]
    private Settings blitSettings;

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
}

public class BlitRenderPass : ScriptableRenderPass
{
    private Material blitMaterial;

    private int tempID = Shader.PropertyToID("_Temp1");
    private int temp2ID = Shader.PropertyToID("_Temp2");

    private string destination;

    private int passCount;
    private int downsample;

    public BlitRenderPass(BlitRenderFeature.Settings blitSettings)
    {
        UpdateSettings(blitSettings);
    }

    public void UpdateSettings(BlitRenderFeature.Settings blitSettings)
    {
        renderPassEvent = blitSettings.renderingEvent;

        blitMaterial = blitSettings.blitMaterial;
        destination = blitSettings.destinationID;
        passCount = blitSettings.passCount;
        downsample = blitSettings.downsample;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = new CommandBuffer {name = "Blit Render Feature"};

        using (new ProfilingScope(cmd, profilingSampler))
        {
            var cameraData = renderingData.cameraData;

            var colorTarget = cameraData.renderer.cameraColorTarget;

            var targetDescriptor = cameraData.cameraTargetDescriptor;
            targetDescriptor.width /= downsample;
            targetDescriptor.height /= downsample;

            cmd.GetTemporaryRT(tempID, targetDescriptor);
            cmd.GetTemporaryRT(temp2ID, targetDescriptor);

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

            cmd.Blit(tempID, destination);
            cmd.SetGlobalTexture(destination, tempID);
        }

        context.ExecuteCommandBuffer(cmd);
    }
}