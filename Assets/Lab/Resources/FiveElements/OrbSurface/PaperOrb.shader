Shader "C6Lab/Five Elements/Paper Orb"
{
    Properties
    {
        _InkColor ("Ink at silhouette", Color) = (.12,.12,.10,1)
        _ShadowColor ("Shadow pigment", Color) = (.16,.26,.17,1)
        [MainColor] _BaseColor ("Middle pigment", Color) = (.25,.40,.26,1)
        _LitColor ("Lit pigment", Color) = (.41,.54,.36,1)
        _LightColor ("Light pigment", Color) = (.55,.64,.44,1)
        _GlintColor ("Dry brush", Color) = (.98,.91,.81,1)
        [NoScaleOffset] _FiberTex ("Paper tooth (linear RGB)", 2D) = "gray" {}
        _FiberScale ("Paper repeats per diameter", Range(.3,3)) = 1.15
        _GrainStrength ("Fiber relief and ink breakup", Range(0,1.6)) = 1
        _BandStrength ("Paint layer edges", Range(0,1)) = 1
        _RimWidth ("Ink outline width", Range(.2,1.7)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back
        ZWrite On

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _InkColor;
            half4 _ShadowColor;
            half4 _BaseColor;
            half4 _LitColor;
            half4 _LightColor;
            half4 _GlintColor;
            float _FiberScale;
            half _GrainStrength;
            half _BandStrength;
            half _RimWidth;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "PaperForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_FiberTex);
            SAMPLER(sampler_FiberTex);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 paperPoint : TEXCOORD1;
                half3 normalOS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.paperPoint = v.positionOS;
                o.normalOS = v.normalOS;
                return o;
            }

            half3 PaperTooth(float3 p, half3 normal)
            {
                // Object-space projections keep the threads continuous across UV seams and poles.
                half3 weights = abs(normal);
                weights *= weights;
                weights *= weights;
                weights /= max(dot(weights, half3(1,1,1)), .001h);
                float3 uv = p * _FiberScale;
                half3 x = SAMPLE_TEXTURE2D(_FiberTex, sampler_FiberTex, uv.zy + float2(.13,.42)).rgb;
                half3 y = SAMPLE_TEXTURE2D(_FiberTex, sampler_FiberTex, uv.xz + float2(.58,.21)).rgb;
                half3 z = SAMPLE_TEXTURE2D(_FiberTex, sampler_FiberTex, uv.xy + float2(.07,.73)).rgb;
                return x * weights.x + y * weights.y + z * weights.z;
            }

            half3 PaperNormal(float3 p, half3 n, float height)
            {
                // Differentiate the filtered height to make the pulp catch directional light.
                // In object units a fiber is shallow; slopes stay bounded at grazing angles.
                float3 dx = ddx(p), dy = ddy(p);
                float3 cotangentX = cross(dy, n), cotangentY = cross(n, dx);
                float determinant = dot(dx, cotangentX);
                float3 slope = (cotangentX * ddx(height) + cotangentY * ddy(height)) *
                    (sign(determinant) / max(abs(determinant), 1.e-9));
                float3 relief = slope * (.0018 * _GrainStrength);
                return normalize(n - relief * rsqrt(max(1., dot(relief, relief) * 6.)));
            }

            half PaintStep(float field, float cutoff, float aa)
            {
                return smoothstep(cutoff - aa, cutoff + aa, field);
            }

            // Brush marks curl around the highlighted side of the sphere. The pigment gaps
            // come from the same stationary fibers that break up the edges of the painted bands.
            half BrushStroke(half3 n, half3 v, half3 lightDir, half3 fiber, float3 p)
            {
                half3 center = SafeNormalize(v * .30h + lightDir * .70h);
                half3 tangent = cross(center, v);
                half tangentLength = length(tangent);
                if (tangentLength < .003h) return 0;
                tangent /= tangentLength;
                half3 across = cross(center, tangent);
                float u = dot(n, tangent);
                float w = dot(n, across);
                // Positive u is the lifted, split end of the brush on the lower-left.
                // Bend its tips back toward the disk center to leave a broad curved swath.
                float curve = -.67 * u * u + .045 * u;
                float rough = (fiber.b - .5) * .042 + sin(dot(p, float3(55., -38., 21.))) * .004;
                float s = w - curve + rough * _GrainStrength;
                float lifted = smoothstep(.09, .39, u);
                float bristleEnds = .061 * sin(75. * s + 1.8 * sin(26. * s));
                float brushU = (u + .003 + lifted * bristleEnds) / .46;
                float taper = pow(saturate(1. - brushU * brushU), .70);
                float width = .165 * taper * (.94 + .21 * u);
                float aa = max(.003, .62 * (fwidth(u) + fwidth(w)));
                float body = smoothstep(-aa, aa, width - abs(s)) * smoothstep(0., .1, taper);
                // Short bristles remain attached to the main mark, and vanish fully beyond their tips.
                float bristle = saturate(1. - abs((u - .24) / .18));
                float bristleW = s - .103 + .105 * u;
                float sliver = smoothstep(-aa, aa, .036 * bristle - abs(bristleW)) *
                               smoothstep(0., .18, bristle);
                float hairs = sin(144. * s + 1.8 * sin(u * 9.) + 2.5 * fiber.b);
                float stroke = lerp(.86, 1., smoothstep(-1., -.1, hairs));
                float split = lifted * smoothstep(.25, .9, hairs) * smoothstep(.17, .43, u);
                float dry = 1. - .44 * smoothstep(.65, .91, fiber.b) * _GrainStrength;
                return saturate(max(body * (1. - .88 * split), sliver * .92) * dry * stroke)
                    * step(.65h, dot(n, center));
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 radial = normalize(i.paperPoint);
                half3 shape = normalize(lerp(radial, normalize(i.normalOS), .46h));
                half3 fiber = PaperTooth(i.paperPoint, radial);
                half3 n = TransformObjectToWorldNormal(shape);
                half3 bumped = TransformObjectToWorldNormal(PaperNormal(i.paperPoint, shape, fiber.r));
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light key = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 studio = SafeNormalize(v * .55h + TransformViewToWorldDir(half3(-.54h,.66h,0), false));
                half keyStrength = saturate(Luminance(key.color));
                half3 lightDir = SafeNormalize(lerp(studio, key.direction, .72h * keyStrength));

                // A broad stroke fans around the rounded volume. Its crooked edge is
                // anchored to the object, while its value still follows the key light.
                float3 p = i.paperPoint * 2.;
                float brushWarp = .081 * sin(dot(p, float3(2.7, -1.1, 1.9)) + .85 * sin(2.9 * p.y))
                    + .024 * sin(dot(p, float3(-6.1, 5.2, 3.8)));
                float toothNoise = (fiber.b - .5) * .062 + (fiber.r - .5) * .032;
                float field = dot(n, lightDir) + brushWarp + toothNoise * _GrainStrength;
                float aa = max(.006, min(.055, fwidth(field) * .57));
                // Paint layer widths are optical values, independent of texture resolution.
                float blendWidth = aa + .18 * (1. - _BandStrength);
                half3 paint = lerp(_ShadowColor.rgb, _BaseColor.rgb,
                    .48h * PaintStep(field, -.11, blendWidth));
                paint = lerp(paint, _BaseColor.rgb, PaintStep(field, .24, blendWidth));
                paint = lerp(paint, _LitColor.rgb, PaintStep(field, .63, blendWidth));
                paint = lerp(paint, _LightColor.rgb, PaintStep(field, .85, blendWidth));
                paint = lerp(paint, _InkColor.rgb, .47h * (1.h - smoothstep(-.50h, -.02h, field)));
                float edgeDistance = min(min(abs(field + .11), abs(field - .24)),
                                         min(abs(field - .63), abs(field - .85)));
                float lip = 1. - smoothstep(.003, aa + .026, edgeDistance);

                half relief = dot(bumped - n, lightDir);
                half grain = (fiber.b - .5h) * .61h + (fiber.r - .5h) * .34h + relief * 1.16h;
                paint *= max(.56h, 1.h + grain * _GrainStrength);
                half dryFibers = saturate((fiber.g - .055h) * 1.5h) * _GrainStrength;
                // Tiny linen fibers pick up the local pigment and turn paler on a torn edge.
                paint = lerp(paint, lerp(_LightColor.rgb, _GlintColor.rgb, .28h),
                    saturate(dryFibers * (.10h + lip * .20h)));
                paint *= 1.h + lip * (.035h + fiber.g * .10h) * _BandStrength;

                half contactShade = lerp(.63h, 1.h, key.shadowAttenuation);
                paint *= contactShade;
                half mark = BrushStroke(n, v, lightDir, fiber, p);
                paint = lerp(paint, _GlintColor.rgb, mark * lerp(.30h, 1.h, key.shadowAttenuation));

                // An inner ink contour gives a clean silhouette without a second mesh pass.
                half facing = saturate(dot(n, v));
                float rimThreshold = (.24 + .035 * (1. - saturate(field))) * sqrt(_RimWidth);
                rimThreshold += ((fiber.b - .5) * .044 + .010 * sin(dot(p, float3(15.,9.,-6.)))) * _GrainStrength;
                float rimAA = max(.006, .65 * fwidth(facing));
                float outline = 1. - smoothstep(rimThreshold - rimAA, rimThreshold + rimAA, facing);
                float inner = (1. - smoothstep(rimThreshold + .025, rimThreshold + .09, facing)) * (1. - outline);
                paint = lerp(paint, _ShadowColor.rgb, inner * .52h);
                half3 ink = lerp(_InkColor.rgb, _ShadowColor.rgb, saturate(.12h + fiber.g * .55h));
                paint = lerp(paint, ink, outline);
                // A broken ochre paper edge shows through the printed contour on the key side.
                float tornEdge = 1. - smoothstep(.010, rimAA + .027, abs(facing - rimThreshold - .020));
                half exposed = saturate((fiber.g + .23h * fiber.b - .10h) * 3.4h);
                half3 paperEdge = _GlintColor.rgb * half3(.56h,.32h,.115h);
                paint = lerp(paint, paperEdge, tornEdge * exposed * (.08h + .58h * saturate(field)));
                return half4(paint, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;
            struct ShadowInput
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            float4 ShadowVert(ShadowInput v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 p = TransformObjectToWorld(v.positionOS);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 l = normalize(_LightPosition - p);
                #else
                    float3 l = _LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p, n, l)));
            }
            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthInput
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            float4 DepthVert(DepthInput v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformObjectToHClip(v.positionOS);
            }
            half4 DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
