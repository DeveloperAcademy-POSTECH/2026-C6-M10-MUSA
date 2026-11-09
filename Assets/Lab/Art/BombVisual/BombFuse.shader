Shader "C6Lab/Bomb Fuse"
{
    Properties
    {
        [PerRendererData] _MainTex ("Supplied fuse PNG", 2D) = "white" {}
        _BurnEnd ("Existing burn share mapped to curve x", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "CanUseSpriteAtlas"="False" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _BurnEnd;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); o.uv=input.uv; o.color=input.color; return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                clip(_BurnEnd - input.uv.x);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
                // The supplied PNG has a white matte (also present in its exported SVG).
                // Remove only that matte at draw time; keep the source file unchanged.
                half ink = 1 - smoothstep(.8h, .99h, min(tex.r,min(tex.g,tex.b)));
                return half4(tex.rgb * input.color.rgb, tex.a * ink * input.color.a);
            }
            ENDHLSL
        }
    }
}
