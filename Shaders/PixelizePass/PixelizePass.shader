Shader "Hidden/PixelizePass"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "gray" {}
		
		[HideInInspector]		
		_CamBounds ("Camera Bounds", Vector) = (0,0,0,0)
		
		[HideInInspector]
		_PixelsPerUnit ("Pixels per Unit", float) = 16
		
		[HideInInspector]
		_OutlineRadius ("Outline Radius", float) = 2
		
		[HideInInspector]
		_OutlineColor ("Outline Color", Color) = (0,0,0,1)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest LEqual

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
        
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float2 invLerp(float2 a, float2 b, float2 t)
            {
                return (t - a) / (b - a);
            }
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
			float4 _CamBounds;
			float _PixelsPerUnit;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
				float2 worldPos = lerp(_CamBounds.xy, _CamBounds.zw, i.uv);
				worldPos = round(worldPos * _PixelsPerUnit) / _PixelsPerUnit;
				
				float2 uv = invLerp(_CamBounds.xy, _CamBounds.zw, worldPos);
				float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
				col.a = clamp(lerp(0, 1, (col.r + col.g + col.b) * 100),0,1);
                return col;
            }
            ENDHLSL
        }
        
        Pass
            {
                Blend One One 
            
                HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    
                struct appdata
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };
    
                struct v2f
                {
                    float2 uv : TEXCOORD0;
                    float4 vertex : SV_POSITION;
                };
    
                float2 invLerp(float2 a, float2 b, float2 t)
                {
                    return (t - a) / (b - a);
                }
                
                TEXTURE2D(_MainTex);
                SAMPLER(sampler_MainTex);
                
                float _OutlineRadius;
                float4 _OutlineColor;
                
                v2f vert (appdata v)
                {
                    v2f o;
                    o.vertex = TransformObjectToHClip(v.vertex);
                    o.uv = v.uv;
                    return o;
                }
    
                float4 frag (v2f i) : SV_Target
                {
                    float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);             
                    return col;
                }
                ENDHLSL
            }
    }
}