Shader "C6Lab/Jigwi Ink Flame"
{
    Properties
    {
        [MainTexture] _BaseMap ("Generated transparent ink flame", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint / opacity", Color) = (1,1,1,1)
        _FlameTime ("Battle clock (held while paused)", Float) = 0
        _Sway ("Upper flame sway", Range(0,0.1)) = 0.03
        _FlameSpeed ("Flame speed", Range(0,8)) = 2.4
        _Seed ("Group phase", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "JigwiInkFlameURP"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _FlameTime, _Sway, _FlameSpeed, _Seed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Warp pigment, not the authoritative surface or collider. No Unity _Time:
                // the Host battle sample supplies this clock, including pause/countdown holds.
                float age = max(0, _FlameTime) * max(0, _FlameSpeed);
                float2 uv = input.uv;
                float tip = smoothstep(0.18, 0.98, uv.y);
                uv.x += sin(uv.y * 8.0 - age + _Seed) * tip * tip * _Sway;
                uv.y = 0.22 + (uv.y - 0.22) * (1.0 + sin(age * 1.7 + _Seed) * 0.025);
                half inside = step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
                half4 pigment = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                pigment.a *= inside;
                return pigment;
            }
            ENDHLSL
        }
    }
    // Keep the same asset usable if the Editor switches to its built-in quality profile.
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "JigwiInkFlameBuiltIn"
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _BaseMap;
            float4 _BaseMap_ST, _BaseColor;
            float _FlameTime, _Sway, _FlameSpeed, _Seed;
            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            fixed4 Frag(Varyings input) : SV_Target
            {
                float age = max(0, _FlameTime) * max(0, _FlameSpeed);
                float2 uv = input.uv;
                float tip = smoothstep(0.18, 0.98, uv.y);
                uv.x += sin(uv.y * 8.0 - age + _Seed) * tip * tip * _Sway;
                uv.y = 0.22 + (uv.y - 0.22) * (1.0 + sin(age * 1.7 + _Seed) * 0.025);
                fixed4 pigment = tex2D(_BaseMap, uv) * _BaseColor;
                pigment.a *= step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
                return pigment;
            }
            ENDCG
        }
    }
    Fallback Off
}
