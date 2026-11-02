Shader "C6Lab/Board Paint Stain"
{
    Properties
    {
        _StainA ("Main stain A (transparent neutral wash)", 2D) = "white" {}
        _StainB ("Main stain B (transparent neutral wash)", 2D) = "white" {}
        _StainC ("Main stain C (transparent neutral wash)", 2D) = "white" {}
        _SpatterA ("Small spatter A", 2D) = "white" {}
        _SpatterB ("Small spatter B", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _WashContrast ("Neutral wash contrast", Range(0,1)) = 0.35
        _EdgePooling ("Soft edge pigment pooling", Range(0,0.3)) = 0.06
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "BoardPaintStain"
            Tags { "LightMode"="UniversalForward" }
            // Preserve coverage alpha in the transparent board RenderTexture/HUD composite.
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_StainA); SAMPLER(sampler_StainA);
            TEXTURE2D(_StainB); SAMPLER(sampler_StainB);
            TEXTURE2D(_StainC); SAMPLER(sampler_StainC);
            TEXTURE2D(_SpatterA); SAMPLER(sampler_SpatterA);
            TEXTURE2D(_SpatterB); SAMPLER(sampler_SpatterB);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _WashContrast;
                float _EdgePooling;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                // x: texture variant (0..4), -1: falling bead; y: reveal; z: birth seed.
                float4 stamp : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 stamp : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.stamp = input.stamp;
                output.color = input.color;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float bottom = lerp(Hash21(cell), Hash21(cell + float2(1,0)), f.x);
                float top = lerp(Hash21(cell + float2(0,1)), Hash21(cell + 1.0), f.x);
                return lerp(bottom, top, f.y);
            }
            half4 ReadWash(float2 uv, float variant)
            {
                // One renderer/material per orb; a quad selects one authored RGBA texture.
                half4 wash = half4(0, 0, 0, 0);
                if (variant < 0.5) wash = SAMPLE_TEXTURE2D(_StainA, sampler_StainA, uv);
                else if (variant < 1.5) wash = SAMPLE_TEXTURE2D(_StainB, sampler_StainB, uv);
                else if (variant < 2.5) wash = SAMPLE_TEXTURE2D(_StainC, sampler_StainC, uv);
                else if (variant < 3.5) wash = SAMPLE_TEXTURE2D(_SpatterA, sampler_SpatterA, uv);
                else wash = SAMPLE_TEXTURE2D(_SpatterB, sampler_SpatterB, uv);
                return wash;
            }
            float Reveal(float2 uv, float amount, float seed)
            {
                // Keep the final silhouette and its wash anchored to the board. Only the
                // wetting front advances: neither the whole stamp nor its texture expands.
                if (amount <= 0.0) return 0.0;
                if (amount >= 1.0) return 1.0;
                float noise = Noise(uv * 7.0 + seed * 31.0) * 0.7
                            + Noise(uv * 17.0 - seed * 13.0) * 0.3;
                float metric = length((uv - 0.5) * 1.41421356) + (noise - 0.5) * 0.18;
                float front = lerp(-0.15, 1.15, saturate(amount));
                return 1.0 - smoothstep(front - 0.065, front + 0.065, metric);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 tint = input.color * _BaseColor;
                if (input.stamp.x < -0.5)
                {
                    // A small round paint bead, deliberately unrelated to the old tear icon.
                    float distance = length(input.uv - 0.5);
                    float coverage = 1.0 - smoothstep(0.40, 0.47, distance);
                    float wash = lerp(0.89, 1.04, saturate(1.0 - distance * 2.0));
                    return half4(tint.rgb * wash, tint.a * coverage);
                }
                half4 brush = ReadWash(input.uv, input.stamp.x);
                float washLuma = dot(brush.rgb, float3(0.2126, 0.7152, 0.0722));
                // RGB carries neutral watercolor density as well as alpha carrying coverage.
                // Lift the darkest wash gently so small stamps stay readable on paper.
                float density = lerp(1.0, lerp(0.65, 1.05, washLuma), _WashContrast);
                float edge = saturate(brush.a * (1.0 - brush.a) * 4.0);
                float wet = density - edge * _EdgePooling;
                float coverage = brush.a * Reveal(input.uv, input.stamp.y, input.stamp.z);
                if (input.stamp.x < 2.5)
                {
                    // Preserve the falling bead at touchdown while the wash begins to wet.
                    // A .28-diameter falling quad's .47-radius bead is .132 of this quad.
                    // The seed bead fades only on the main stain, never on satellite spatter.
                    float bead = (1.0 - smoothstep(0.11, 0.132, length(input.uv - 0.5)))
                               * (1.0 - smoothstep(0.0, 0.3, saturate(input.stamp.y)));
                    coverage = max(coverage, bead);
                }
                return half4(tint.rgb * wet, tint.a * coverage);
            }
            ENDHLSL
        }
    }
}
