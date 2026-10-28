Shader "Hidden/C6Lab/Experiments/WatercolorPaint"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_Brush); SAMPLER(sampler_Brush);
        TEXTURE2D(_InteriorDistance); SAMPLER(sampler_InteriorDistance);
        TEXTURE2D(_CurrentPaint); SAMPLER(sampler_CurrentPaint);
        TEXTURE2D(_OpticalHistory); SAMPLER(sampler_OpticalHistory);
        float4 _Center, _Right, _Up, _Seed;
        float _Radius, _Density, _BroadVariation, _Mottle, _Grain, _GrainScale;
        float _EdgeStrength, _EdgeWidth, _TextureScale;
        struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
        Varyings vert(uint id:SV_VertexID)
        {
            Varyings o; o.positionCS=GetFullScreenTriangleVertexPosition(id);
            o.uv=GetFullScreenTriangleTexCoord(id); return o;
        }
        float Hash(float3 p)
        { p=frac(p*.1031); p+=dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z); }
        float Noise(float3 p)
        {
            float3 a=floor(p), f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(lerp(Hash(a),Hash(a+float3(1,0,0)),f.x),lerp(Hash(a+float3(0,1,0)),Hash(a+float3(1,1,0)),f.x),f.y),
                lerp(lerp(Hash(a+float3(0,0,1)),Hash(a+float3(1,0,1)),f.x),lerp(Hash(a+float3(0,1,1)),Hash(a+1),f.x),f.y),f.z);
        }
        float3 Direction(float2 uv)
        {
            float y=1-2*uv.y, ring=sqrt(max(0,1-y*y)), angle=(uv.x-.5)*2*PI;
            return float3(cos(angle)*ring,y,sin(angle)*ring);
        }
        half4 OpticalStamp(Varyings i):SV_Target
        {
            float3 d=Direction(i.uv); float cosine=clamp(dot(d,_Center.xyz),-1,1);
            if(cosine<=0||_Radius<=0) return 0;
            float sine=sqrt(max(0,1-cosine*cosine));
            float factor=sine>.000001?acos(cosine)/sine:1;
            float2 uv=.5+float2(dot(d,_Right.xyz),dot(d,_Up.xyz))*factor/(2*_Radius);
            if(any(uv<0)||any(uv>1)) return 0;
            float4 brush=SAMPLE_TEXTURE2D_LOD(_Brush,sampler_Brush,uv,0);
            float ink=dot(brush.rgb,float3(.2126,.7152,.0722))*brush.a;
            float aa=max(fwidth(ink),.012), support=smoothstep(.08-aa,.08+aa,ink);
            float scale=max(_TextureScale,.25);
            float broad=Noise(float3(uv*(3.3/scale),.3)+_Seed.xyz);
            float mottle=Noise(float3(uv*(14.5/scale),4.1)+_Seed.yzx);
            float distance=SAMPLE_TEXTURE2D_LOD(_InteriorDistance,sampler_InteriorDistance,uv,0).r/8;
            float width=max(_EdgeWidth*lerp(.35,1.5,broad),.0002);
            float broken=lerp(.04,1,smoothstep(.22,.78,Noise(float3(uv*8.5,9)+_Seed.zxy)));
            float rim=exp2(-distance/width*2.2)*broken;
            // Scalar bounded optical contributions. No element RGB or ownership is mixed here.
            // Additive replay leaves earlier individual rims inside the union of later marks.
            return half4(support*lerp(.12,.34,broad), support*rim*.65,
                support*lerp(.10,.30,mottle), 0);
        }
        half4 Composite(Varyings i):SV_Target
        {
            float4 source=SAMPLE_TEXTURE2D_LOD(_CurrentPaint,sampler_CurrentPaint,i.uv,0);
            if(source.a<=0) return 0;
            float3 history=SAMPLE_TEXTURE2D_LOD(_OpticalHistory,sampler_OpticalHistory,i.uv,0).rgb;
            float3 d=Direction(i.uv);
            float broad=Noise(d*(8/max(_TextureScale,.25)));
            float medium=Noise(d*(35/max(_TextureScale,.25))+11.2);
            // Micro grain follows the surface, independently of every stamp's placement/rotation.
            float3 p=d*(340/max(_GrainScale,.25));
            float footprint=max(fwidth(p.x),max(fwidth(p.y),fwidth(p.z)));
            float resolved=1-smoothstep(.4,1.5,footprint);
            float grain=lerp(.5,Noise(p),resolved);
            float wash=_Density*.62 + history.r*.95 + _BroadVariation*(broad-.5)*.65
                + _Mottle*((medium-.5)*.55 + (history.b-.18)*.6);
            wash+=_EdgeStrength*history.g*.85;
            wash-=_Grain*(grain-.3)*.38;
            float alpha=source.a*clamp(wash,.12,.98);
            // Preserve positive support even at the last quantized antialias pixel.
            alpha=max(alpha,min(source.a,1.0/255));
            float3 ink=source.rgb/max(source.a,.00001);
            float brightness=1-_EdgeStrength*history.g*.13;
            return half4(saturate(ink*brightness)*alpha,alpha);
        }
        ENDHLSL
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            Blend One One
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment OpticalStamp
            ENDHLSL
        }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment Composite
            ENDHLSL
        }
    }
}
