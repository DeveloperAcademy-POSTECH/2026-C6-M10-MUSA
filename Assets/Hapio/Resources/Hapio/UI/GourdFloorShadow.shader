Shader "Hidden/Hapio/GourdFloorShadow"
{
    Properties { _MainTex ("Silhouette", 2D) = "black" {} }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Floor projection"
            HLSLPROGRAM
            #pragma vertex Project
            #pragma fragment Silhouette

            float4x4 _ObjectToFloorClip;

            float4 Project(float3 position : POSITION) : SV_POSITION
            {
                return mul(_ObjectToFloorClip, float4(position, 1));
            }

            half4 Silhouette() : SV_Target
            {
                return 1;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Soften edge"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Soften
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            half4 Soften(v2f_img input) : SV_Target
            {
                float2 uv = input.uv;
                // Compensate for the shallow floor rectangle when sampling the penumbra.
                float2 x = float2(1.4 * _MainTex_TexelSize.x, 0);
                float2 y = float2(0, 3.6 * _MainTex_TexelSize.y);
                half alpha = 4 * tex2D(_MainTex, uv).r;
                alpha += 2 * (tex2D(_MainTex, uv - x).r + tex2D(_MainTex, uv + x).r
                    + tex2D(_MainTex, uv - y).r + tex2D(_MainTex, uv + y).r);
                alpha += tex2D(_MainTex, uv - x - y).r + tex2D(_MainTex, uv - x + y).r
                    + tex2D(_MainTex, uv + x - y).r + tex2D(_MainTex, uv + x + y).r;
                return half4(1, 1, 1, alpha / 16);
            }
            ENDCG
        }
    }
}
