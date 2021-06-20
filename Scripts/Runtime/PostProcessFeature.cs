using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
        renderer.EnqueuePass(postProcessPass);
    }

    void Update()
    {
        if (postSettings.blitMaterial != null)
            postProcessPass.UpdateSettings(postSettings);
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
    private int downsample;

    private int passCount;

    private Material postMaterial;
    private int temp2ID = Shader.PropertyToID("_Temp2");
    private int tempID = Shader.PropertyToID("_Temp1");

    public PostProcessRenderPass(PostProcessFeature.Settings settings)
    {
        renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        UpdateSettings(settings);
    }

    public void UpdateSettings(PostProcessFeature.Settings settings)
    {
        postMaterial = settings.blitMaterial;
        passCount = settings.passCount;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = new CommandBuffer {name = "Post Process Render Feature"};

        using (new ProfilingScope(cmd, profilingSampler))
        {
            var cameraData = renderingData.cameraData;
            var colorTarget = cameraData.renderer.cameraColorTarget;
            var targetDescriptor = cameraData.cameraTargetDescriptor;

            cmd.GetTemporaryRT(tempID, targetDescriptor);
            cmd.GetTemporaryRT(temp2ID, targetDescriptor);

            cmd.Blit(colorTarget, tempID, postMaterial);

            if (passCount > 1)
            {
                for (int i = 0; i < passCount - 1; i++)
                {
                    cmd.Blit(tempID, temp2ID, postMaterial);

                    var swap = tempID;

                    tempID = temp2ID;
                    temp2ID = swap;
                }
            }

            cmd.Blit(tempID, BuiltinRenderTextureType.CurrentActive);

            cmd.ReleaseTemporaryRT(tempID);
            cmd.ReleaseTemporaryRT(temp2ID);
        }

        context.ExecuteCommandBuffer(cmd);
    }
}