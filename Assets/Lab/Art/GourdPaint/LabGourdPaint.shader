Shader "C6Lab/GourdPaint"
{
    Properties
    {
        _BaseColor("White paper shell", Color) = (.96,.94,.86,1)
        [NoScaleOffset] _PaintMap("Cumulative premultiplied paint", 2D) = "black" {}
        [NoScaleOffset] _PainterMap("Committed painter P byte (linear point)", 2D) = "black" {}
        [HideInInspector] _PaintMapReady("Paint map ready", Float) = 0
        [HideInInspector] _PlayerPaintPatternsEnabled("Player paint patterns enabled", Float) = 1
        [HideInInspector] _PaintGrainEnabled("Paint grain enabled", Float) = 0
        [HideInInspector] _PaintGrainSize("Paint grain size", Float) = 1
        [HideInInspector] _PaintGrainStrength("Paint grain strength", Float) = .3
        [HideInInspector] _PaintMottleStrength("Paint mottle strength", Float) = .18
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
            TEXTURE2D(_PainterMap);
            SAMPLER(sampler_PainterMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _PaintMapReady;
                float _PlayerPaintPatternsEnabled;
                float _PaintGrainEnabled;
                float _PaintGrainSize;
                float _PaintGrainStrength;
                float _PaintMottleStrength;
            CBUFFER_END

            float GrainHash(float3 cell)
            {
                cell = frac(cell * .1031);
                cell += dot(cell, cell.yzx + 33.33);
                return frac((cell.x + cell.y) * cell.z);
            }

            // Three-dimensional value noise has no longitude seam or pole singularity.
            // No time, screen position, world position, or external random state is sampled.
            float GrainNoise(float3 position)
            {
                float3 cell = floor(position);
                float3 t = frac(position);
                t = t * t * (3.0 - 2.0 * t);
                return lerp(lerp(lerp(GrainHash(cell), GrainHash(cell + float3(1,0,0)), t.x),
                    lerp(GrainHash(cell + float3(0,1,0)), GrainHash(cell + float3(1,1,0)), t.x), t.y),
                    lerp(lerp(GrainHash(cell + float3(0,0,1)), GrainHash(cell + float3(1,0,1)), t.x),
                    lerp(GrainHash(cell + float3(0,1,1)), GrainHash(cell + float3(1,1,1)), t.x), t.y), t.z);
            }

            half3 GrainColor(half3 paintColor, float3 direction)
            {
                float3 grainPosition = direction * (160.0 / max(_PaintGrainSize, .25));
                float3 footprint = fwidth(grainPosition);
                float resolved = 1.0 - smoothstep(.3, 1.1, max(footprint.x, max(footprint.y, footprint.z)));
                float grain = smoothstep(.5, .8, GrainNoise(grainPosition));
                // Unresolved grain tends to its mean instead of sparkling at a distance.
                grain = lerp(.2, grain, resolved);
                float mottle = GrainNoise(direction * 11.0) - .5;
                float brightness = 1.0 - saturate(_PaintGrainStrength) * grain * .7
                    + saturate(_PaintMottleStrength) * mottle * .65;
                return saturate(paintColor * brightness);
            }

            float PeriodicLine(float coordinate, float width)
            {
                float distance = abs(frac(coordinate + .5) - .5);
                float aa = max(fwidth(coordinate), .015);
                return 1.0 - smoothstep(width - aa, width + aa, distance);
            }

            // Fixed P mapping also documented by LabPlayerPaintPatterns. This samples
            // the shell's local equal-area UV, so marks turn with the gourd and its
            // independent result copy. Integer longitude periods meet at the seam.
            float PainterInk(float painter, float2 uv)
            {
                float2 q = uv * float2(12.0, 6.0);
                if (painter < .5 || painter > 5.5) return 0;
                if (painter < 1.5)
                {
                    float distance = length(frac(q) - .5);
                    float aa = max(fwidth(distance), .015);
                    return 1.0 - smoothstep(.17 - aa, .17 + aa, distance);
                }
                if (painter < 2.5) return PeriodicLine(q.x + q.y, .115);
                if (painter < 3.5) return PeriodicLine(q.y, .115);
                if (painter < 4.5) return PeriodicLine(q.y + .2 * sin(q.x * PI), .115);
                return max(PeriodicLine(q.x, .075), PeriodicLine(q.y, .075));
            }

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
                // Modulate only premultiplied pigment RGB: coverage, alpha and unpainted
                // paper retain the original map. OFF and both strengths zero bypass exactly.
                if (_PaintGrainEnabled > .5 && (_PaintGrainStrength > 0 || _PaintMottleStrength > 0))
                    paint.rgb = GrainColor(paint.rgb, direction);
                // Ownership changes brightness only. It cannot punch holes into pigment,
                // alter coverage, replace element hues, or touch unpainted paper.
                if (_PlayerPaintPatternsEnabled > .5)
                {
                    float painter = round(SAMPLE_TEXTURE2D(_PainterMap, sampler_PainterMap, uv).r * 255.0);
                    paint.rgb *= 1.0 - .32 * PainterInk(painter, uv);
                }
                half3 color = _BaseColor.rgb * (1.0 - paint.a) + paint.rgb;
                float light = .72 + .28 * saturate(dot(normalize(input.normalWS), normalize(float3(-.4,.8,.5))));
                return half4(color * light, 1);
            }
            ENDHLSL
        }
    }
}
