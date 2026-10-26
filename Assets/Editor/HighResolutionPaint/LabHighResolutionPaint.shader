Shader "Hidden/C6Lab/Experiments/HighResolutionPaint"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Brush); SAMPLER(sampler_Brush);
            float4 _Center, _Right, _Up, _Ink;
            float _Radius, _Density, _Mottle, _Grain;
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(uint id : SV_VertexID)
            {
                Varyings o;
                o.positionCS = GetFullScreenTriangleVertexPosition(id);
                o.uv = GetFullScreenTriangleTexCoord(id);
                return o;
            }
            float hash(float3 p)
            {
                p = frac(p * .1031); p += dot(p, p.yzx + 33.33); return frac((p.x + p.y) * p.z);
            }
            float noise(float3 p)
            {
                float3 a = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(hash(a), hash(a + float3(1,0,0)), f.x),
                    lerp(hash(a + float3(0,1,0)), hash(a + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(hash(a + float3(0,0,1)), hash(a + float3(1,0,1)), f.x),
                    lerp(hash(a + float3(0,1,1)), hash(a + 1), f.x), f.y), f.z);
            }
            half4 frag(Varyings i) : SV_Target
            {
                float y = 1 - 2 * i.uv.y, ring = sqrt(max(0, 1 - y * y));
                float angle = (i.uv.x - .5) * 2 * PI;
                float3 d = float3(cos(angle) * ring, y, sin(angle) * ring);
                float cosine = clamp(dot(d, _Center.xyz), -1, 1);
                if (cosine <= 0 || _Radius <= 0) return 0;
                float sine = sqrt(max(0, 1 - cosine * cosine));
                float factor = sine > .000001 ? acos(cosine) / sine : 1;
                float2 uv = .5 + float2(dot(d, _Right.xyz), dot(d, _Up.xyz)) * factor / (2 * _Radius);
                if (any(uv < 0) || any(uv > 1)) return 0;
                float4 brush = SAMPLE_TEXTURE2D_LOD(_Brush, sampler_Brush, uv, 0);
                float ink = dot(brush.rgb, float3(.2126,.7152,.0722)) * brush.a;
                float aa = max(fwidth(ink), .012);
                float coverage = smoothstep(.08 - aa, .08 + aa, ink);
                float mottle = noise(d * 18 + _Center.xyz * 9);
                float grain = noise(d * 145);
                float edge = (1 - smoothstep(.08, .38, ink)) * .1;
                float alpha = coverage * saturate(_Density + _Mottle * (mottle - .5) + edge
                    - _Grain * grain * .16);
                float3 color = saturate(_Ink.rgb * lerp(.88, 1.04, mottle));
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
