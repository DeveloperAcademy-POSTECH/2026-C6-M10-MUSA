// 보여주기용 (#452 preview): 기존 흰 박(White Gourd)에 칠해지는 물감 지도를 새 박 모델 위에 덧그린다.
// 물감 지도는 흰 박 메시 원점에서 본 방향으로 찾는다(C6Lab/GourdPaint와 같은 계산).
// 새 모델의 월드 위치를 흰 박의 로컬 좌표로 바꿔(_ShellWorldToObject) 같은 방향의 물감을 가져온다.
Shader "Hapio/GourdPreviewPaintOverlay"
{
    Properties
    {
        [HideInInspector] [NoScaleOffset] _PaintMap("Cumulative premultiplied paint", 2D) = "black" {}
        [HideInInspector] _PaintMapReady("Paint map ready", Float) = 0
        _PaintStrength("Paint Strength", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };

            TEXTURE2D(_PaintMap);
            SAMPLER(sampler_PaintMap);
            float _PaintMapReady;
            float _PaintStrength;
            float4x4 _ShellWorldToObject;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input):SV_Target
            {
                float3 shellPosition = mul(_ShellWorldToObject, float4(input.positionWS, 1)).xyz;
                float3 direction = normalize(shellPosition);
                float2 uv = float2(atan2(direction.z, direction.x) / (2.0 * PI) + .5, .5 - direction.y * .5);
                half4 paint = SAMPLE_TEXTURE2D(_PaintMap, sampler_PaintMap, uv) * saturate(_PaintMapReady) * saturate(_PaintStrength);
                // 흰 박 셰이더의 물감 조명과 같은 고정 광원.
                float light = .72 + .28 * saturate(dot(normalize(input.normalWS), normalize(float3(-.4, .8, .5))));
                return half4(paint.rgb * light, paint.a);
            }
            ENDHLSL
        }
    }
}
