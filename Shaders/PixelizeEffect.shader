Shader "CustomPostProcess/PixelizeEffect"
{
    Properties
    {
        [HideInInspector]
        _MainTex("Main Texture", 2D) = "white" {}

        [HideInInspector]
        _PixelsPerUnit("Resolution",float) = 1
    }
    SubShader
    {
        Tags
        {
            "LightTags" = "SRPDefaultUnlit" "RenderType" = "Opaque"
        }

        Cull Off ZWrite On ZTest Always
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            struct output
            {
                float4 col : COLOR;
                float depth : DEPTH;
            };

            uniform float4 _MainTex_TexelSize;
            uniform sampler2D _PixelizeLayerDepth;
            float4x4 _CamToWorld[4];

            float _PixelsPerUnit;
            
            sampler2D _MainTex;

            v2f vert(appdata i)
            {
                v2f o;
                o.positionHCS = TransformObjectToHClip(i.positionOS);
                o.uv = i.uv;
                return o;
            }

            output frag(v2f i)
            {
                output o;

                float depth = tex2D(_PixelizeLayerDepth, i.uv);
                float4 adjustedUV = float4(i.uv * 2.0 - 1.0, depth, 1);
                
                float4 viewPos = mul(_CamToWorld[0], adjustedUV);
                float w = viewPos.w;

                viewPos /= w;

                float3 worldPos = mul(_CamToWorld[1], viewPos).xyz;
                worldPos = round(worldPos * _PixelsPerUnit) / _PixelsPerUnit;
                   
                float4 adjustedClipPos = mul(_CamToWorld[2], float4(worldPos, 1));
                adjustedClipPos = mul(_CamToWorld[3], adjustedClipPos);

                adjustedClipPos /= w;
                
                float2 finalPos = float2(adjustedClipPos.x, -adjustedClipPos.y) / 2 + 0.5;
                float4 col = tex2D(_MainTex, finalPos);
                depth = 0;
                
                o.col = col;
                o.depth = col.a;
                return o;
            }
            ENDHLSL
        }
    }
}