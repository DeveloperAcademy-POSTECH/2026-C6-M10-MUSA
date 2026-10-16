Shader "C6Lab/GourdPaint"
{
    Properties
    {
        _BaseColor("White paper shell", Color) = (.96,.94,.86,1)
        [NoScaleOffset] _PaintMap("Cumulative premultiplied paint", 2D) = "black" {}
        [HideInInspector] _PaintMapReady("Paint map ready", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionOS:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            TEXTURE2D(_PaintMap);
            SAMPLER(sampler_PaintMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _PaintMapReady;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input):SV_Target
            {
                float3 direction = normalize(input.positionOS);
                // Matches the Host's equal-area cells. The runtime texture uses Point sampling,
                // Repeat on U and Clamp on V; there are no per-fragment decal loops or mipmaps.
                float2 uv = float2(atan2(direction.z, direction.x) / (2.0 * PI) + .5,
                    .5 - direction.y * .5);
                half4 paint = SAMPLE_TEXTURE2D(_PaintMap, sampler_PaintMap, uv) * saturate(_PaintMapReady);
                half3 color = _BaseColor.rgb * (1.0 - paint.a) + paint.rgb;
                float light = .72 + .28 * saturate(dot(normalize(input.normalWS), normalize(float3(-.4,.8,.5))));
                return half4(color * light, 1);
            }
            ENDHLSL
        }
    }
}
