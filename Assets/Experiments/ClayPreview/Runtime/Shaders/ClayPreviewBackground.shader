Shader "C6Lab/Experiments/ClayPreviewBackground"
{
    Properties
    {
        [NoScaleOffset] _MainTex("Imported Preview Background", 2D) = "white" {}
        [HideInInspector] _UvScaleOffset("Centered Fit / Fill", Vector) = (1,1,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Background" }
        Pass
        {
            Name "UnlitPreviewBackground"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _UvScaleOffset;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv*_UvScaleOffset.xy+_UvScaleOffset.zw;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            { return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv); }
            ENDHLSL
        }
    }
    FallBack Off
}
