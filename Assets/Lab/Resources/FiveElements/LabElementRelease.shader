Shader "C6Lab/Five Elements/Release"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+5" }
        Cull Off
        ZWrite On
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; half4 pigment : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                half4 pigment : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionOS = input.positionOS.xyz;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.pigment = input.pigment;
                #ifndef UNITY_COLORSPACE_GAMMA
                    output.pigment.rgb = SRGBToLinear(output.pigment.rgb);
                #endif
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 crossNormal = cross(ddy(input.positionWS), ddx(input.positionWS));
                half3 normal = crossNormal * rsqrt(max(dot(crossNormal, crossNormal), 1e-18));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                normal = dot(normal, view) < 0 ? -normal : normal;
                Light key = GetMainLight();
                half diffuse = saturate(dot(normal, key.direction));
                half specular = pow(saturate(dot(normal, SafeNormalize(view + key.direction))), 32);
                half3 color = input.pigment.rgb * lerp(0.80h, 1.08h, diffuse)
                    + specular * input.pigment.a * 0.46h * key.color;
                if (input.pigment.a < .04h)
                {
                    // Fine, object-locked inclusions on dry soil. Fade the texture below a screen pixel.
                    float3 grid = input.positionOS * 77.0;
                    float3 h = frac(floor(grid) * float3(.1031, .11369, .13787));
                    h += dot(h, h.yzx + 19.19);
                    half fleck = frac((h.x + h.y) * h.z);
                    float3 footprint = fwidth(grid);
                    half visible = 1.0h - smoothstep(.65h, 1.65h, max(footprint.x, max(footprint.y, footprint.z)));
                    color *= 1.0h + (fleck - .5h) * .34h * visible;
                }
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
