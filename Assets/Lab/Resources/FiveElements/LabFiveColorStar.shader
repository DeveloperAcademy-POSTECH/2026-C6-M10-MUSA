Shader "C6Lab/Five Elements/Reward Star"
{
    Properties
    {
        [MainTexture] _MainTex ("Authored star alpha", 2D) = "white" {}
        [MainColor] _BaseColor ("Pigment and opacity", Color) = (1,1,1,1)
        _Spin ("Billboard spin (radians)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+5" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _BaseColor;
                float _Spin;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float s, c;
                sincos(_Spin,s,c);
                float2 p = float2(c*input.positionOS.x-s*input.positionOS.y,
                    s*input.positionOS.x+c*input.positionOS.y);
                float sx = length(unity_ObjectToWorld._m00_m10_m20);
                float sy = length(unity_ObjectToWorld._m01_m11_m21);
                float3 right = UNITY_MATRIX_I_V._m00_m10_m20;
                float3 up = UNITY_MATRIX_I_V._m01_m11_m21;
                float3 center = TransformObjectToWorld(float3(0,0,0));
                output.positionCS = TransformWorldToHClip(center + right*p.x*sx + up*p.y*sy);
                output.uv = TRANSFORM_TEX(input.uv,_MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 source = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
                return half4(source.rgb*_BaseColor.rgb, source.a*_BaseColor.a);
            }
            ENDHLSL
        }
    }
}
