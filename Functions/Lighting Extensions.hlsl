#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

void SampleMainLight_float(float3 worldPosition, out float3 direction, out float3 color, out float distanceAttenuation,
                           out float shadowAttenuation)
{
    #ifdef SHADERGRAPH_PREVIEW
    direction = float3(0.5, 0.5, 0);
    color = 1;
    distanceAttenuation = 1;
    shadowAttenuation = 1;
    #else

    #if SHADOWS_SCREEN
        half4 clipPosition = TransformWorldToHClip(worldPosition);
        half4 shadowCoord = ComputeScreenPos(clipPosition);
    #else
    half4 shadowCoord = TransformWorldToShadowCoord(worldPosition);
    #endif

    Light mainLight = GetMainLight(shadowCoord);
    direction = mainLight.direction;
    color = mainLight.color.rgb;
    distanceAttenuation = mainLight.distanceAttenuation;
    shadowAttenuation = mainLight.shadowAttenuation;
    #endif
}

void SampleAllLights_float(float3 positionWS, float3 normalWS, float3 viewDir, float4 specColor, float smoothness, out float3 color, out float3 specularColor)
{
    #ifdef SHADERGRAPH_PREVIEW
    color = 1;
    specularColor = 1;
    #else

    #if SHADOWS_SCREEN
    half4 clipPosition = TransformWorldToHClip(positionWS);
    half4 shadowCoord = ComputeScreenPos(clipPosition);
    #else
    half4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    #endif


    Light mainLight = GetMainLight(shadowCoord);

    #ifdef _SCREEN_SPACE_OCCLUSION
    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(screenSpaceUVs);
    mainLight.color *= aoFactor.directAmbientOcclusion;
    #endif

    mainLight.color *= mainLight.distanceAttenuation * mainLight.shadowAttenuation;

    color = LightingLambert(mainLight.color, mainLight.direction, normalWS);
    specularColor = LightingSpecular(mainLight.color, mainLight.direction, normalWS, viewDir, specColor, smoothness);

    uint pixelLightCount = GetAdditionalLightsCount();
    for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
    {
        Light light = GetAdditionalLight(lightIndex, positionWS, shadowCoord);
        #ifdef _SCREEN_SPACE_OCCLUSION
            light.color *= aoFactor.directAmbientOcclusion;
        #endif
        light.color *= light.distanceAttenuation * light.shadowAttenuation;
        color += LightingLambert(light.color, light.direction, normalWS);
        specularColor += LightingSpecular(light.color, light.direction, normalWS, viewDir, specColor,
                                         smoothness);
    }
    color /= 1 + pixelLightCount;
    #endif
}
#endif
