// Editor-only surface-attached sticker study. Not a production gourd shader.
// UV0 = per-patch asset coordinates, UV1 = stable authoritative surface coordinates.
// Separate MeshRenderers, not a recolor of the saved gourd material or its paint map.
Shader "Hidden/C6Lab/Studies/SurfaceSticker"
{
    Properties
    {
        [NoScaleOffset] _StickerTex("Prepared RGBA Sticker", 2D) = "white" {}
        _Tint("Recorded Linear Sticker Tint", Vector) = (1,1,1,1)
        [Toggle] _TintFromLuminance("Tint Neutral Asset By Luminance", Float) = 0
        _FiberReference("Neutral Asset Mean Linear Luminance", Float) = .5
        [Toggle] _UseAssetAlpha("Use Original Asset Silhouette", Float) = 1
        [NoScaleOffset] _CoverageTex("Optional Patch Coverage (A)", 2D) = "white" {}
        [Toggle] _UseCoverage("Use Surface UV Coverage", Float) = 0
        [NoScaleOffset] _TopIdTex("Final Study Sticker IDs (not player IDs)", 2D) = "black" {}
        [Toggle] _UseTopOwner("Categorical Paper Joint", Float) = 0
        _LayerIndex("Current Study Sticker Index", Float) = 0
        [Toggle] _ShadowOnly("Thin Contact Shadow Study", Float) = 0
        _ShadowOffsetUV("Contact Shadow Asset Offset", Vector) = (-.012,.012,0,0)
        [Toggle] _SurfaceCoordinatesFromPosition("Seam-Safe Gourd Surface Coordinates", Float) = 1
        _AlphaThreshold("Asset Alpha Cutoff", Range(.01,.99)) = .15
        _EdgeFeather("Asset Edge Feather", Range(.001,.2)) = .025
        _ShadowStrength("Thin Paper Edge Tone", Range(0,.4)) = .08
        _PaperLightStrength("Gentle Shape Light", Range(0,.5)) = .14
        _LightDirection("Study Light Direction", Vector) = (-.4,.8,.5,0)
        _DepthBias("Depth Bias (same for all patches)", Range(-2,0)) = -1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Name "SurfaceSticker"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Offset -1, [_DepthBias]
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_StickerTex); SAMPLER(sampler_StickerTex);
            TEXTURE2D(_CoverageTex); SAMPLER(sampler_CoverageTex);
            TEXTURE2D(_TopIdTex); SAMPLER(sampler_TopIdTex);
            float4 _TopIdTex_TexelSize;
            float4 _StickerTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint, _LightDirection;
                float _TintFromLuminance, _UseAssetAlpha, _UseCoverage, _SurfaceCoordinatesFromPosition;
                float _AlphaThreshold, _EdgeFeather, _ShadowStrength, _PaperLightStrength;
                float _DepthBias;
                float _FiberReference;
                float _UseTopOwner, _LayerIndex;
                float _ShadowOnly;
                float4 _ShadowOffsetUV;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 assetUV : TEXCOORD0;
                float2 surfaceUV : TEXCOORD1;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 assetUV : TEXCOORD1;
                float2 surfaceUV : TEXCOORD2;
                float4 color : COLOR;
                float3 positionOS : TEXCOORD3;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.assetUV = input.assetUV;
                output.surfaceUV = input.surfaceUV;
                output.color = input.color;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 asset = SAMPLE_TEXTURE2D(_StickerTex, sampler_StickerTex, input.assetUV + _ShadowOffsetUV.xy * _ShadowOnly);
                float coverage = 1;
                if (_UseAssetAlpha > .5)
                {
                    float feather = max(_EdgeFeather, fwidth(asset.a));
                    coverage *= smoothstep(_AlphaThreshold-feather, _AlphaThreshold+feather, asset.a);
                }
                if (_UseCoverage > .5)
                {
                    float2 surfaceUV = input.surfaceUV;
                    if (_SurfaceCoordinatesFromPosition > .5)
                    {
                        // Match the production GourdPaint shader, not the mesh's native
                        // UV seam or interpolated wrap coordinates. Geometry must remain
                        // in the same body-local space as the saved gourd shell.
                        float3 direction = normalize(input.positionOS);
                        surfaceUV = float2(atan2(direction.z,direction.x)/(2*PI)+.5,.5-direction.y*.5);
                    }
                    float paid = SAMPLE_TEXTURE2D(_CoverageTex, sampler_CoverageTex, surfaceUV).a;
                    if (_UseTopOwner > .5)
                    {
                        // Reconstruct one top paper at joints; independently alpha blending
                        // two touching masks would expose a hairline of the base paper.
                        float2 p = surfaceUV * _TopIdTex_TexelSize.zw - .5;
                        float2 baseCell = floor(p), f = frac(p);
                        float4 ids, weights;
                        ids.x = round(SAMPLE_TEXTURE2D(_TopIdTex,sampler_TopIdTex,(baseCell+float2(.5,.5))*_TopIdTex_TexelSize.xy).r*255);
                        ids.y = round(SAMPLE_TEXTURE2D(_TopIdTex,sampler_TopIdTex,(baseCell+float2(1.5,.5))*_TopIdTex_TexelSize.xy).r*255);
                        ids.z = round(SAMPLE_TEXTURE2D(_TopIdTex,sampler_TopIdTex,(baseCell+float2(.5,1.5))*_TopIdTex_TexelSize.xy).r*255);
                        ids.w = round(SAMPLE_TEXTURE2D(_TopIdTex,sampler_TopIdTex,(baseCell+float2(1.5,1.5))*_TopIdTex_TexelSize.xy).r*255);
                        weights = float4((1-f.x)*(1-f.y),f.x*(1-f.y),(1-f.x)*f.y,f.x*f.y);
                        float winner=0, best=-1; paid=0;
                        [unroll] for(int i=0;i<4;i++)
                        {
                            if(ids[i]<.5) continue;
                            paid+=weights[i];
                            float mass=0;
                            [unroll] for(int j=0;j<4;j++) if(abs(ids[i]-ids[j])<.1) mass+=weights[j];
                            if(mass>best || (abs(mass-best)<.0001 && ids[i]>winner)) {best=mass;winner=ids[i];}
                        }
                        clip(.1-abs(winner-_LayerIndex));
                    }
                    float aa = max(.025, fwidth(paid));
                    coverage *= smoothstep(.5-aa, .5+aa, paid);
                }
                coverage *= saturate(_Tint.a);
                clip(coverage-.001);
                if (_ShadowOnly > .5) return half4(.045,.035,.025,coverage*.24);

                // RGB is entirely the prepared sticker's own material. Never sample
                // the underlying gourd paper or the old cumulative paint map here.
                float luminance = dot(asset.rgb, float3(.2126,.7152,.0722));
                float fiber = clamp(luminance / max(_FiberReference,.01), .65, 1.25);
                half3 pigment = lerp(asset.rgb, fiber.xxx, saturate(_TintFromLuminance)) * _Tint.rgb;
                float3 n = normalize(input.normalWS);
                float diffuse = saturate(dot(n, SafeNormalize(_LightDirection.xyz)));
                float shapeLight = lerp(1, .78+.22*diffuse, saturate(_PaperLightStrength));

                // Keep this as a subtle rim in the sticker itself, not a cast shadow
                // on the saved gourd. The inside is opaque; only the silhouette is AA.
                float rim = 0;
                if (_UseAssetAlpha > .5)
                {
                    float width = max(.025, fwidth(asset.a)*2);
                    rim = 1-smoothstep(_AlphaThreshold, _AlphaThreshold+width, asset.a);
                }
                else if (_UseCoverage > .5)
                    rim = 1-smoothstep(.05,.95,coverage);
                pigment *= shapeLight * (1-saturate(_ShadowStrength)*rim);
                return half4(pigment,coverage);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
