using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PostProcessFeature : ScriptableRendererFeature
{
    [Serializable]
    public class Settings
    {
        public Material blitMaterial;

        [Range(1, 8)]
        public int passCount = 1;

        [Range(1, 8)]
        public int downsample = 1;
    }

    private PostProcessRenderPass postProcessPass;

    [SerializeField]
    private Settings postSettings;

    public override void Create()
    {
        postProcessPass = new PostProcessRenderPass(postSettings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(postProcessPass);
    }

    void Update()
    {
        if (postSettings.blitMaterial != null)
            postProcessPass.UpdateSettings(postSettings);
    }
}

public class PostProcessRenderPass : ScriptableRenderPass
{
    private Material blitMaterial;

    private int tempID = Shader.PropertyToID("_Temp1");
    private int temp2ID = Shader.PropertyToID("_Temp2");

    private string destinationID = "_CameraColorTexture";

    private int passCount;
    private int downsample;

    public PostProcessRenderPass(PostProcessFeature.Settings blitSettings)
    {
        UpdateSettings(blitSettings);
    }

    public void UpdateSettings(PostProcessFeature.Settings blitSettings)
    {
        renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        blitMaterial = blitSettings.blitMaterial;
        passCount = blitSettings.passCount;
        downsample = blitSettings.downsample;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = new CommandBuffer {name = "Post Process Render Feature"};

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

            cmd.Blit(tempID, destinationID);
        }

        context.ExecuteCommandBuffer(cmd);
    }
}