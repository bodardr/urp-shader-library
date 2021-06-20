using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class Pixelizer : MonoBehaviour
{
    public static RenderTexture PixelizeTexture;
    public static RenderTexture PixelizeDepthTexture;

    [SerializeField]
    private LayerMask layerMask;

    private Camera pixelizeCamera;

    private GameObject pixelizeCameraGO;

    private int pixelizeTextureID = Shader.PropertyToID("_PixelizeTexture");

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += RenderPixelatedObjects;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= RenderPixelatedObjects;
    }

    private void RenderPixelatedObjects(ScriptableRenderContext context, Camera cam)
    {
        if (pixelizeCameraGO == null)
            InstantiateCamera();

        if (PixelizeTexture == null)
            CreateRenderTextures(cam);

        pixelizeCamera.CopyFrom(cam);
        pixelizeCamera.enabled = false;

        pixelizeCamera.cullingMask = layerMask;
        pixelizeCamera.clearFlags = CameraClearFlags.Color | CameraClearFlags.Depth;
        pixelizeCamera.depthTextureMode = DepthTextureMode.Depth;

        pixelizeCamera.worldToCameraMatrix = cam.worldToCameraMatrix;
        pixelizeCamera.projectionMatrix = cam.projectionMatrix;

        UniversalRenderPipeline.RenderSingleCamera(context, pixelizeCamera);

        var cmd = CommandBufferPool.Get();
        cmd.SetRenderTarget(PixelizeTexture, PixelizeDepthTexture);
        //context.DrawRenderers();
        cmd.SetGlobalTexture(pixelizeTextureID, PixelizeTexture);
        context.ExecuteCommandBuffer(cmd);

        CommandBufferPool.Release(cmd);
    }

    private void CreateRenderTextures(Camera cam)
    {
        PixelizeTexture = RenderTexture.GetTemporary(cam.pixelWidth, cam.pixelHeight, 0,
            GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.Default, true));

        PixelizeDepthTexture = RenderTexture.GetTemporary(cam.pixelWidth, cam.pixelHeight, 24,
            RenderTextureFormat.Depth);
    }

    private void InstantiateCamera()
    {
        pixelizeCameraGO = new GameObject("Pixelizer Camera") {hideFlags = HideFlags.HideAndDontSave};
        pixelizeCamera = pixelizeCameraGO.AddComponent<Camera>();

        var cameraData = pixelizeCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

        pixelizeCamera.enabled = false;
        pixelizeCamera.SetTargetBuffers(PixelizeTexture.colorBuffer, PixelizeDepthTexture.depthBuffer);
    }
}