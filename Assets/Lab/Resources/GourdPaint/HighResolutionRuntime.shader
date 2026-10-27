Shader "Hidden/C6Lab/HighResolutionPaintRuntime"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_Brush); SAMPLER(sampler_Brush);
        TEXTURE2D(_StampMap); SAMPLER(sampler_StampMap);
        TEXTURE2D(_AuthorityMap); SAMPLER(sampler_AuthorityMap);
        TEXTURE2D(_AuthorityPainters); SAMPLER(sampler_AuthorityPainters);
        float4 _Center, _Right, _Up, _AuthoritySize;
        float _Radius, _CoveredCount, _CellCount;
        struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
        Varyings vert(uint id:SV_VertexID)
        {
            Varyings o; o.positionCS=GetFullScreenTriangleVertexPosition(id);
            o.uv=GetFullScreenTriangleTexCoord(id); return o;
        }
        float3 Direction(float2 uv)
        {
            float y=1-2*uv.y, ring=sqrt(max(0,1-y*y)), angle=(uv.x-.5)*2*PI;
            return float3(cos(angle)*ring,y,sin(angle)*ring);
        }
        float Hash(float3 p)
        { p=frac(p*.1031); p+=dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z); }
        float Noise(float3 p)
        {
            float3 a=floor(p),f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(lerp(Hash(a),Hash(a+float3(1,0,0)),f.x),
                lerp(Hash(a+float3(0,1,0)),Hash(a+float3(1,1,0)),f.x),f.y),
                lerp(lerp(Hash(a+float3(0,0,1)),Hash(a+float3(1,0,1)),f.x),
                lerp(Hash(a+float3(0,1,1)),Hash(a+1),f.x),f.y),f.z);
        }
        float2 CellUv(float2 cell)
        { return float2(frac((cell.x+.5)*_AuthoritySize.z),(clamp(cell.y,0,_AuthoritySize.y-1)+.5)*_AuthoritySize.w); }
        void Candidate(float2 cell,float weight,inout float coverage,inout float best,inout float3 ink,inout float owner)
        {
            float2 uv=CellUv(cell);
            float4 c=SAMPLE_TEXTURE2D_LOD(_AuthorityMap,sampler_AuthorityMap,uv,0);
            float w=weight*c.a; coverage+=w;
            // Color and P are categorical. Interpolation may shape the coverage envelope,
            // but it must never invent a blended team color or a fractional player number.
            if(w>best)
            { best=w; ink=c.rgb/max(c.a,.00001); owner=round(SAMPLE_TEXTURE2D_LOD(_AuthorityPainters,sampler_AuthorityPainters,uv,0).r*255); }
        }
        void Reconstruct(float2 uv,out float coverage,out float3 ink,out float owner)
        {
            float3 d=Direction(uv);
            // A quadratic B-spline reconstructs a continuously curved support field across
            // cell boundaries. Nine bounded samples avoid the old four-point grid corners.
            float2 jitter=float2(Noise(d*29)-.5,Noise(d*29+17)-.5)*.20;
            float2 p=uv*_AuthoritySize.xy-.5+jitter;
            float2 cell=floor(p+.5), f=p-cell;
            float3 wx=float3(.5*(.5-f.x)*(.5-f.x),.75-f.x*f.x,.5*(.5+f.x)*(.5+f.x));
            float3 wy=float3(.5*(.5-f.y)*(.5-f.y),.75-f.y*f.y,.5*(.5+f.y)*(.5+f.y));
            coverage=0; float best=-1; ink=0; owner=0;
            Candidate(cell+float2(-1,-1),wx.x*wy.x,coverage,best,ink,owner);
            Candidate(cell+float2( 0,-1),wx.y*wy.x,coverage,best,ink,owner);
            Candidate(cell+float2( 1,-1),wx.z*wy.x,coverage,best,ink,owner);
            Candidate(cell+float2(-1, 0),wx.x*wy.y,coverage,best,ink,owner);
            Candidate(cell,wx.y*wy.y,coverage,best,ink,owner);
            Candidate(cell+float2( 1, 0),wx.z*wy.y,coverage,best,ink,owner);
            Candidate(cell+float2(-1, 1),wx.x*wy.z,coverage,best,ink,owner);
            Candidate(cell+float2( 0, 1),wx.y*wy.z,coverage,best,ink,owner);
            Candidate(cell+float2( 1, 1),wx.z*wy.z,coverage,best,ink,owner);
        }
        float OpticalCoverage(float2 uv,float coverage)
        {
            if(_CoveredCount<.5) return 0;
            if(_CoveredCount>=_CellCount-.5) return 1;
            // R is the actual continuous brush, G is its known projection domain. A zero
            // in a retained brush is an intentional gap, not permission to repaint the
            // coarse cell outline over it. Only missing/evicted geometry uses the fallback.
            float2 geometry=SAMPLE_TEXTURE2D_LOD(_StampMap,sampler_StampMap,uv,0).rg;
            float erasureGate=smoothstep(.008,.30,coverage);
            float continuousBrush=geometry.r*erasureGate;
            float fallback=smoothstep(.16,.68,coverage);
            return saturate(lerp(fallback,continuousBrush,saturate(geometry.g)));
        }
        half4 Stamp(Varyings i):SV_Target
        {
            float3 d=Direction(i.uv); float cosine=clamp(dot(d,_Center.xyz),-1,1);
            if(cosine<=0||_Radius<=0) return 0;
            float sine=sqrt(max(0,1-cosine*cosine));
            float factor=sine>.000001?acos(cosine)/sine:1;
            float2 uv=.5+float2(dot(d,_Right.xyz),dot(d,_Up.xyz))*factor/(2*_Radius);
            if(any(uv<0)||any(uv>1)) return 0;
            float4 sample=SAMPLE_TEXTURE2D_LOD(_Brush,sampler_Brush,uv,0);
            float ink=dot(sample.rgb,float3(.2126,.7152,.0722))*sample.a;
            float aa=max(fwidth(ink),.012);
            float alpha=smoothstep(.08-aa,.08+aa,ink);
            // The cache keeps brush coverage separate from the projection domain. G's
            // additive/clamped union under this blend remains bounded in the UNorm target.
            float edge=max(abs(uv.x-.5),abs(uv.y-.5))*2;
            float domain=1-smoothstep(.98,1,edge);
            return half4(alpha,domain,0,alpha);
        }
        half4 Paint(Varyings i):SV_Target
        {
            float coverage,owner; float3 ink; Reconstruct(i.uv,coverage,ink,owner);
            float alpha=OpticalCoverage(i.uv,coverage);
            float3 d=Direction(i.uv);
            float mottle=Noise(d*18), grain=Noise(d*145);
            // Optical pigment density is independent from the binary Host coverage.
            // Even a completed surface retains positive pigment everywhere while the
            // translucent wash lets the paper show through, as in the comparison study.
            float density=saturate(.82+.22*(mottle-.5)-.25*grain*.16);
            alpha*=density;
            float brightness=lerp(.88,1.04,mottle);
            return half4(saturate(ink*brightness)*alpha,alpha);
        }
        half4 Painter(Varyings i):SV_Target
        {
            float coverage,owner; float3 ink; Reconstruct(i.uv,coverage,ink,owner);
            float alpha=OpticalCoverage(i.uv,coverage);
            return half4(alpha>.00001?owner/255:0,0,0,1);
        }
        ENDHLSL
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment Stamp
            ENDHLSL
        }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment Paint
            ENDHLSL
        }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment Painter
            ENDHLSL
        }
    }
}
