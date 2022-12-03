Shader "Hidden/PixelizeOutline"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
		
		[HideInInspector]		
		_CamBounds ("Camera Bounds", Vector) = (0,0,0,0)
		
		[HideInInspector]
		_PixelsPerUnit ("Pixels per Unit", float) = 16
    }
    SubShader 
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest NotEqual
        Blend One Zero
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            sampler2D _MainTex;
			float4 _CamBounds;
			float _PixelsPerUnit;   
			         
            float2 invLerp(float2 a, float2 b, float2 t)
            {
                return (t - a) / (b - a);
            }

            fixed4 frag (v2f i) : SV_Target
            {
				float4 col = tex2D(_MainTex, i.uv);                 
                return col;
            }
            ENDCG
        }
    }
}