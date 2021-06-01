Shader "Hidden/KawaseBlur"
{
    Properties
    {
        [HideInInspector]_MainTex ("Texture", 2D) = "white" {}
        _Offset("Offset", float) = 0.5
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

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

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Offset;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f o) : SV_Target
            {
                float i = _Offset;

                fixed4 col = tex2D(_MainTex, o.uv);
                col += tex2D(_MainTex, o.uv + float2(i,i) * _MainTex_TexelSize);
                col += tex2D(_MainTex, o.uv + float2(-i,i) * _MainTex_TexelSize);
                col += tex2D(_MainTex, o.uv + float2(-i,-i) * _MainTex_TexelSize);
                col += tex2D(_MainTex, o.uv + float2(i,-i) * _MainTex_TexelSize);

                return col / 5.0f;
            }
            ENDCG
        }
    }
}