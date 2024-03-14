using System;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

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

        public bool useCustomInputTexture = false;
        
        public string customInputTextureName = "";
    }
}

public class BlitRenderPass : ScriptableRenderPass
{
    private int destinationID = 0;
    private int temp2ID = Shader.PropertyToID("_Temp2");
    private int tempID = Shader.PropertyToID("_Temp1");

    private Material blitMaterial;
    private int downsample;
    private int passCount;
    
    private bool useCustomInput;
    private RTHandle customInputID;

    public BlitRenderPass(BlitFeature.Settings settings)
    {
        UpdateSettings(settings);
    }

    public void UpdateSettings(BlitFeature.Settings settings)
    {
        renderPassEvent = settings.renderingEvent;

        blitMaterial = settings.blitMaterial;

        if (destinationID != 0)
        {
            var cmd = CommandBufferPool.Get();
            cmd.ReleaseTemporaryRT(destinationID);
            Graphics.ExecuteCommandBuffer(cmd);
        }

        useCustomInput = settings.useCustomInputTexture;
        
        customInputID?.Release();
        customInputID = RTHandles.Alloc(settings.customInputTextureName);
        
        destinationID = Shader.PropertyToID(settings.destinationName);
        passCount = settings.passCount;
        downsample = settings.downsample;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        var cameraTextureDescriptor = renderingData.cameraData.cameraTargetDescriptor;

        cmd.GetTemporaryRT(destinationID, cameraTextureDescriptor);
        cmd.GetTemporaryRT(tempID, cameraTextureDescriptor);
        cmd.GetTemporaryRT(temp2ID, cameraTextureDescriptor);
    }

    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(destinationID);
        cmd.ReleaseTemporaryRT(tempID);
        cmd.ReleaseTemporaryRT(temp2ID);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get("Blit Render Feature");
        
        using (new ProfilingScope(cmd, profilingSampler))
        {
            var cameraData = renderingData.cameraData;

            var cam = cameraData.camera;
            
#if UNITY_EDITOR
            if (SceneView.GetAllSceneCameras().Contains(cam))
                return;
#endif

            var colorTarget = useCustomInput ? customInputID : cameraData.renderer.cameraColorTargetHandle;

            var targetDescriptor = cameraData.cameraTargetDescriptor;
            targetDescriptor.width /= downsample;
            targetDescriptor.height /= downsample;
            
            cmd.Blit(colorTarget, tempID, blitMaterial);

            if (passCount > 1)
            {
                for (int i = 0; i < passCount - 1; i++)
                {
                    cmd.Blit(tempID, temp2ID, blitMaterial);
                    (tempID, temp2ID) = (temp2ID, tempID);
                }
            }

            cmd.Blit(tempID, destinationID);
            cmd.SetGlobalTexture(destinationID, destinationID);
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}