Shader "Blit/Alpha"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _OutlineColor("Outline Color", vector) = (0,0,0,1)
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
        }

        // No culling or depth
        Cull Off ZWrite On ZTest Always

        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            uniform float4 _MainTex_TexelSize;
            uniform sampler2D _PixelizeLayerDepth;
            sampler _MainTex;

            float4 _OutlineColor;
            float _OutlineThickness;

            float sobelDepth(float2 uv)
            {
                float2 shift = _MainTex_TexelSize.xy * _OutlineThickness;

                float2 shifts[4] =
                {
                    float2(0, shift.y),
                    float2(0, -shift.y),
                    float2(-shift.x, 0),
                    float2(shift.x, 0),
                };

                float up = tex2D(_MainTex, uv + shifts[0]).r;
                float down = tex2D(_MainTex, uv + shifts[1]).r;
                float left = tex2D(_MainTex, uv + shifts[2]).r;
                float right = tex2D(_MainTex, uv + shifts[3]).r;

                return max(max(up, down), max(left, right));
            }

            v2f vert(appdata i)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(i.positionOS);
                o.uv = i.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);

                //todo : scale outline thickness with _PixelsPerUnit
                float currentPixelDepth = tex2D(_PixelizeLayerDepth, i.uv).r;

                float depth = sobelDepth(i.uv);

                col = lerp(col, _OutlineColor, col.a > 0 ? 0 : step(0.01, depth));
                return col;
            }
            
            ENDHLSL
        }
    }
}