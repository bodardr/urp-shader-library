void MainLight_half(float3 worldPosition, out half3 direction, out half3 color, out half distanceAttenuation, out half shadowAttenuation)
{
#ifdef SHADERGRAPH_PREVIEW
    direction = half3(0.5, 0.5, 0);
    color = 1;
    distanceAttenuation = 1;
    shadowAttenuation = 1;
#else
    half4 shadowCoord = TransformWorldToShadowCoord(worldPosition);
    Light mainLight = GetMainLight(shadowCoord);
    direction = mainLight.direction;
    color = mainLight.color;
    distanceAttenuation = mainLight.distanceAttenuation;
    shadowAttenuation = mainLight.shadowAttenuation;
#endif
} 
