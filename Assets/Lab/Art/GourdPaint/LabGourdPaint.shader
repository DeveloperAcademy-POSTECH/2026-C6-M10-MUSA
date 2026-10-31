Shader "C6Lab/GourdPaint"
{
    Properties
    {
        [Header(Hanji Paper)]
        _BaseColor("Paper Tint", Color) = (.96,.94,.86,1)
        [NoScaleOffset] _PaperTex("Hanji PNG (Color and Height)", 2D) = "white" {}
        _PaperScale("Texture Repeat", Range(.25,16)) = 3
        _PaperRotation("Fiber Direction Variation", Range(0,1)) = .06
        _PaperSaturation("Paper Color Saturation", Range(0,1)) = .6
        _PaperLightDirection("Paper Light Direction", Vector) = (-.65,.8,.6,0)
        _PaperContrast("Fiber Contrast", Range(0,4)) = 1
        _PaperBump("Surface Relief", Range(0,.1)) = .012
        _PaperUnderPaint("Relief Under Paint", Range(0,1)) = .5
        _PaperLightContrast("Paper Shape Shading", Range(0,1)) = .55
        _PaperBrightness("Paper Brightness", Range(.5,1.5)) = 1.08
        _PaperSmoothness("Smoothness (0 = Matte)", Range(0,1)) = .05
        [HideInInspector] [NoScaleOffset] _PaintMap("Cumulative premultiplied paint", 2D) = "black" {}
        [HideInInspector] [NoScaleOffset] _PainterMap("Committed painter P byte (linear point)", 2D) = "black" {}
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
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionOS:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 normalOS:TEXCOORD2; float3 positionWS:TEXCOORD3; };
            TEXTURE2D(_PaperTex);
            SAMPLER(sampler_PaperTex);
            TEXTURE2D(_PaintMap);
            SAMPLER(sampler_PaintMap);
            TEXTURE2D(_PainterMap);
            SAMPLER(sampler_PainterMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _PaperScale, _PaperContrast, _PaperBump, _PaperUnderPaint, _PaperSmoothness;
                float _PaperLightContrast, _PaperBrightness;
                float _PaperRotation, _PaperSaturation;
                float4 _PaperLightDirection;
                float _PaintMapReady;
                float _PlayerPaintPatternsEnabled;
                float _PaintGrainEnabled;
                float _PaintGrainSize;
                float _PaintGrainStrength;
                float _PaintMottleStrength;
            CBUFFER_END

            float2 PaperOffset(float2 cell)
            {
                return frac(sin(float2(dot(cell, float2(127.1,311.7)),
                    dot(cell, float2(269.5,183.3)))) * 43758.5453);
            }

            half3 PaperTile(float2 uv, float2 cell, float2 dx, float2 dy)
            {
                float2 offset = PaperOffset(cell);
                float angle = (offset.x - .5) * 6.2831853 * _PaperRotation;
                float sn = sin(angle), cs = cos(angle);
                float2x2 rotation = float2x2(cs,-sn,sn,cs);
                return SAMPLE_TEXTURE2D_GRAD(_PaperTex, sampler_PaperTex,
                    mul(rotation,uv) + offset * 17.0, mul(rotation,dx), mul(rotation,dy)).rgb;
            }

            // Overlapping randomly offset samples on a triangular lattice break up
            // repeated/mirrored motifs. Shared vertices keep triangle boundaries continuous.
            half3 PaperPatch(float2 uv)
            {
                float2 grid = float2(uv.x - uv.y * .57735027, uv.y * 1.15470054);
                float2 cell = floor(grid);
                float2 f = frac(grid);
                float2 a, b, c;
                float3 w;
                if (f.x + f.y < 1.0)
                {
                    a = cell; b = cell + float2(1,0); c = cell + float2(0,1);
                    w = float3(1.0-f.x-f.y, f.x, f.y);
                }
                else
                {
                    a = cell + 1.0; b = cell + float2(0,1); c = cell + float2(1,0);
                    w = float3(f.x+f.y-1.0, 1.0-f.x, 1.0-f.y);
                }
                // Smooth weights avoid relief creases where a sample enters/leaves.
                w = w * w * (3.0 - 2.0 * w);
                w /= max(w.x+w.y+w.z, .0001);
                float2 dx = ddx(uv), dy = ddy(uv);
                return PaperTile(uv, a, dx, dy) * w.x
                     + PaperTile(uv, b, dx, dy) * w.y
                     + PaperTile(uv, c, dx, dy) * w.z;
            }

            // Object-space projection stays attached to the rotating gourd.
            half3 PaperSample(float3 position, float3 normal)
            {
                float3 weights = pow(abs(normal), 4);
                weights /= max(dot(weights, float3(1,1,1)), .0001);
                float3 p = position * _PaperScale;
                return PaperPatch(p.zy).rgb * weights.x
                     + PaperPatch(p.xz).rgb * weights.y
                     + PaperPatch(p.xy).rgb * weights.z;
            }

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
                if (painter < .5 || painter > 6.5) return 0;
                if (painter < 1.5)
                {
                    float distance = length(frac(q) - .5);
                    float aa = max(fwidth(distance), .015);
                    return 1.0 - smoothstep(.17 - aa, .17 + aa, distance);
                }
                if (painter < 2.5) return PeriodicLine(q.x + q.y, .115);
                if (painter < 3.5) return PeriodicLine(q.y, .115);
                if (painter < 4.5) return PeriodicLine(q.y + .2 * sin(q.x * PI), .115);
                if (painter < 5.5) return max(PeriodicLine(q.x, .075), PeriodicLine(q.y, .075));
                // P6 (3:3 territory, #394): the diagonal the other way from P2.
                return PeriodicLine(q.x - q.y, .115);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
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
                half3 paper = PaperSample(input.positionOS, normalize(input.normalOS));
                half paperLuma = dot(paper, half3(.2126,.7152,.0722));
                paper = lerp(paperLuma.xxx, paper, _PaperSaturation);
                half3 paperColor = saturate(1.0 + (paper - 1.0) * _PaperContrast);
                // Derive surface gradients from the same PNG, without a separate normal map.
                float height = dot(paper, float3(.2126,.7152,.0722));
                float3 n = normalize(input.normalWS);
                float3 dpdx = ddx(input.positionWS), dpdy = ddy(input.positionWS);
                float3 r1 = cross(dpdy, n), r2 = cross(n, dpdx);
                float det = dot(dpdx, r1);
                float relief = _PaperBump * lerp(1.0, _PaperUnderPaint, paint.a);
                float3 gradient = (ddx(height) * r1 + ddy(height) * r2) * sign(det) / max(abs(det), 1e-8);
                n = normalize(n - relief * gradient);
                float3 lightDirection = normalize(float3(-.4,.8,.5));
                float light = .72 + .28 * saturate(dot(n, lightDirection));
                float3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 halfway = SafeNormalize(lightDirection + viewDirection);
                float specular = pow(saturate(dot(n, halfway)), lerp(8.0,128.0,_PaperSmoothness))
                    * _PaperSmoothness * .15 * saturate(dot(n,lightDirection));
                // Paper-only art direction: the existing pigment lighting stays intact.
                float paperDiffuse = saturate(dot(n, SafeNormalize(_PaperLightDirection.xyz)));
                float paperLight = lerp(1.0, .45 + .65 * paperDiffuse, _PaperLightContrast) * _PaperBrightness;
                half3 shaded = _BaseColor.rgb * paperColor * (1.0-paint.a) * paperLight + paint.rgb * light;
                return half4(shaded + specular, 1);
            }
            ENDHLSL
        }
    }
}
