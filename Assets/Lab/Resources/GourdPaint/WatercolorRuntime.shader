Shader "Hidden/C6Lab/WatercolorPaintRuntime"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_Brush); SAMPLER(sampler_Brush);
        TEXTURE2D(_InteriorDistance); SAMPLER(sampler_InteriorDistance);
        TEXTURE2D(_CurrentPaint); SAMPLER(sampler_CurrentPaint);
        TEXTURE2D(_CurrentPainters); SAMPLER(sampler_CurrentPainters);
        TEXTURE2D_ARRAY(_CommittedOptics); SAMPLER(sampler_CommittedOptics);
        TEXTURE2D_ARRAY(_LiveOptics); SAMPLER(sampler_LiveOptics);
        float4 _Center, _Right, _Up, _Seed;
        float4 _Palette0, _Palette1, _Palette2, _Palette3, _Palette4;
        float _Radius, _Progress, _Density, _BroadVariation, _Mottle, _Grain, _GrainScale;
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
        float3 Hue(float3 color)
        { return color/max(max(color.r,max(color.g,color.b)),.00001); }
        void ComparePalette(float3 hue, float3 color, float index, inout float best, inout float selected)
        {
            float3 delta=hue-Hue(color);
            float distance=dot(delta,delta);
            if(distance<best) { best=distance; selected=index; }
        }
        float PaletteSlice(float3 color)
        {
            float3 hue=Hue(color); float best=1e10, selected=0;
            ComparePalette(hue,_Palette0.rgb,0,best,selected);
            ComparePalette(hue,_Palette1.rgb,1,best,selected);
            ComparePalette(hue,_Palette2.rgb,2,best,selected);
            ComparePalette(hue,_Palette3.rgb,3,best,selected);
            ComparePalette(hue,_Palette4.rgb,4,best,selected);
            return selected;
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
            // Only a reveal threshold changes. The footprint, brush UV and surface grain
            // are never scaled, displaced or animated. Endpoints are explicitly exact.
            float progress=saturate(_Progress);
            float arrival=lerp(.08,.84,Noise(float3(uv*6.7,13.1)+_Seed.zxy));
            float reveal=progress<=0?0:progress>=1?1:smoothstep(arrival-.08,arrival+.12,progress);
            float broadInk=support*lerp(.12,.34,broad);
            float mediumInk=support*lerp(.10,.30,mottle);
            // Per-element scalar layers retain earlier internal rims. B/A track revealed
            // versus full support; no stamp writes an element color or a player number.
            return half4((broadInk*.95+mediumInk*.6*_Mottle)*reveal,
                support*rim*.65*reveal,support*reveal,support);
        }
        half4 Composite(Varyings i):SV_Target
        {
            float4 source=SAMPLE_TEXTURE2D_LOD(_CurrentPaint,sampler_CurrentPaint,i.uv,0);
            if(source.a<=0) return 0;
            float slice=PaletteSlice(source.rgb);
            float4 settled=SAMPLE_TEXTURE2D_ARRAY_LOD(_CommittedOptics,sampler_CommittedOptics,i.uv,slice,0);
            float4 live=SAMPLE_TEXTURE2D_ARRAY_LOD(_LiveOptics,sampler_LiveOptics,i.uv,slice,0);
            float2 history=saturate(settled.rg+live.rg);
            float3 d=Direction(i.uv);
            float broad=Noise(d*(8/max(_TextureScale,.25)));
            float medium=Noise(d*(35/max(_TextureScale,.25))+11.2);
            float3 p=d*(340/max(_GrainScale,.25));
            float footprint=max(fwidth(p.x),max(fwidth(p.y),fwidth(p.z)));
            float resolved=1-smoothstep(.4,1.5,footprint);
            float grain=lerp(.5,Noise(p),resolved);
            float wash=_Density*.62+history.r+_BroadVariation*(broad-.5)*.65
                +_Mottle*((medium-.5)*.55-.18*.6);
            wash+=_EdgeStrength*history.g*.85;
            wash-=_Grain*(grain-.3)*.38;
            float alpha=source.a*clamp(wash,.12,.98);
            alpha=max(alpha,min(source.a,1.0/255));
            // Existing settled paint stays visible during a later overlap. A restored
            // surface without retained geometry uses the current committed-map fallback.
            float reveal=settled.a>.00001?1:live.a>.00001?saturate(live.b/max(live.a,.00001)):1;
            alpha*=reveal;
            float3 pigment=source.rgb/max(source.a,.00001);
            float brightness=1-_EdgeStrength*saturate(history.g)*.13;
            // Authoritative categorical hue is the only pigment color. The P map stays
            // independently bound by LabGourdPaint and is never filtered or rewritten here.
            return half4(saturate(pigment*brightness)*alpha,alpha);
        }
        ENDHLSL
        Pass
        {
            Name "OpticalStamp"
            ZWrite Off ZTest Always Cull Off
            Blend One One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment OpticalStamp
            ENDHLSL
        }
        Pass
        {
            Name "Composite"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment Composite
            ENDHLSL
        }
    }
}
