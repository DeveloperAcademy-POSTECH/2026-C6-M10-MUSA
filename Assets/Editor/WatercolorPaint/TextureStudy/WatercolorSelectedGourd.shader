Shader "Hidden/C6Lab/WatercolorSelectedGourdStudy"
{
    // Editor-only projection of the approved right-hand flat texture.
    // Replay, funded-cell gates, erasure and final reconstruction retain their existing semantics.
    Properties
    {
        _StudyLegacySilhouette("Use Previous Visual Silhouette", Range(0,1)) = 1
        _SourceGain("Selected Source Tone Dose",Range(1,12)) = 6
        _Enhancement("Selected Texture Strength",Range(0,1)) = 1
        _MediumGain("Selected Medium Contrast",Range(1,2)) = 1.4
        _FineGain("Selected Fine Grain",Range(0,1.5)) = .85
        _TintDisplay("Numeric Display Pigment RGB",Vector) = (.870588,.176471,.149020,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_Brush); SAMPLER(sampler_Brush);
        TEXTURE2D(_SourceBands); SAMPLER(sampler_SourceBands);
        TEXTURE2D(_CellMask); SAMPLER(sampler_CellMask);
        TEXTURE2D(_BuiltPaint); SAMPLER(sampler_BuiltPaint);
        TEXTURE2D(_RevealBase); SAMPLER(sampler_RevealBase);
        TEXTURE2D(_CurrentPaint); SAMPLER(sampler_CurrentPaint);
        TEXTURE2D(_LogicalCoverage); SAMPLER(sampler_LogicalCoverage);
        TEXTURE2D(_CellStrokes); SAMPLER(sampler_CellStrokes);
        float4 _Center, _Right, _Up, _Tint, _Cells, _Seed, _BuiltPaint_TexelSize;
        float _Radius, _Density, _Overlap, _EdgeStrength, _EdgeWidth;
        float _PigmentContrast, _PigmentSaturation, _RimBreakup;
        float _OverlapOnly, _Reveal, _ExternalCoverage;
        float _V3LayerOpacityCap, _V3WashOpacity, _V3PigmentDepth;
        float _V3PoolStrength, _V3FlowScale, _V3RimWobble;
        float _SourceGain, _Enhancement, _MediumGain, _FineGain, _StudyLegacySilhouette;
        float4 _TintDisplay;
        struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
        Varyings vert(uint id:SV_VertexID)
        {
            Varyings o; o.positionCS=GetFullScreenTriangleVertexPosition(id);
            o.uv=GetFullScreenTriangleTexCoord(id); return o;
        }
        int2 Cell(float2 uv)
        { return int2(min(floor(frac(uv.x)*_Cells.x),_Cells.x-1),clamp(floor(uv.y*_Cells.y),0,_Cells.y-1)); }
        float Mask(float2 uv) { return LOAD_TEXTURE2D(_CellMask,Cell(uv)).a; }
        float3 Direction(float2 uv)
        {
            float y=1-2*uv.y, ring=sqrt(max(0,1-y*y)), angle=(uv.x-.5)*2*PI;
            return float3(cos(angle)*ring,y,sin(angle)*ring);
        }
        float3 DisplayToLinear(float3 c)
        { return lerp(c/12.92,pow((c+.055)/1.055,2.4),step(.04045,c)); }
        float4 SelectedLayer(float2 uv)
        {
            if(any(uv<0)||any(uv>1)) return 0;
            // Mips contain premultiplied darkness/signed source bands, never display color.
            float4 source=SAMPLE_TEXTURE2D(_Brush,sampler_Brush,uv);
            float3 density=source.rgb;
            if(_Enhancement>0)
            {
                float4 bands=SAMPLE_TEXTURE2D(_SourceBands,sampler_SourceBands,uv);
                float original=dot(density,float3(.2126,.7152,.0722))/max(source.a,.000001);
                float adjusted=bands.r+bands.g*clamp(_MediumGain,1,2)+bands.b*clamp(_FineGain,0,1.5);
                adjusted=clamp(adjusted,original*.65,original*1.35);
                float ratio=original>.000001?adjusted/original:1;
                ratio=lerp(1,ratio,saturate(_Enhancement)*saturate(bands.a));
                density=min(max(density*ratio,0),source.aaa);
            }
            float gain=max(_SourceGain,1);
            float3 amount=gain==1?density:1-pow(max(1-density,0),gain);
            float3 approvedDisplay=1-amount*(1-saturate(_TintDisplay.rgb));
            float3 target=DisplayToLinear(approvedDisplay);
            // Valid premultiplied layer whose white-paper composition is exactly the
            // selected flat target. This is source-over, not a physical pigment model.
            float alpha=saturate(1-min(target.r,min(target.g,target.b)));
            return float4(clamp(target-(1-alpha),0,alpha),alpha);
        }
        float4 Stamp(Varyings i):SV_Target
        {
            if(Mask(i.uv)<.5) return 0;
            float3 direction=Direction(i.uv);
            float cosine=clamp(dot(direction,_Center.xyz),-1,1);
            if(cosine<=0||_Radius<=0) return 0;
            float sine=sqrt(max(0,1-cosine*cosine));
            float factor=sine>.000001?acos(cosine)/sine:1;
            // Native source UV undoes the old512/384 padding exactly. Journal geometry
            // and rotations remain unchanged; source support never extends by clamping.
            float2 uv=.5+float2(dot(direction,_Right.xyz),dot(direction,_Up.xyz))*factor/(2*_Radius);
            float4 layer=SelectedLayer(uv);
            if(_OverlapOnly>.5) layer*=saturate(_Overlap);
            return layer;
        }
        float4 Reference(Varyings i):SV_Target
        {
            float4 layer=SelectedLayer(i.uv);
            return float4(layer.rgb+(1-layer.a),1);
        }

        half4 Erase(Varyings i):SV_Target { return half4(0,0,0,Mask(i.uv)); }
        float2 CellUv(float2 uv,float2 delta)
        { return float2(frac(uv.x+delta.x/_Cells.x),clamp(uv.y+delta.y/_Cells.y,.5/_Cells.y,1-.5/_Cells.y)); }
        void BoundarySample(float2 uv,float2 delta,float weight,inout float covered,inout float4 pigment)
        {
            float2 sampleUv=CellUv(uv,delta);
            float logical=LOAD_TEXTURE2D(_LogicalCoverage,Cell(sampleUv)).a;
            covered+=weight*logical;
            if(logical>.5)
            {
                float4 now=SAMPLE_TEXTURE2D_LOD(_BuiltPaint,sampler_BuiltPaint,sampleUv,0);
                float4 before=SAMPLE_TEXTURE2D_LOD(_RevealBase,sampler_RevealBase,sampleUv,0);
                // Settled replay copies the retained value exactly. A base-dependent
                // lerp at t=1 can retain a one-ULP difference from reveal history.
                float4 settled=_Reveal>=1?now:lerp(before,now,saturate(_Reveal));
                pigment+=settled*weight;
            }
        }
        half4 Final(Varyings i):SV_Target
        {
            float4 now=SAMPLE_TEXTURE2D_LOD(_BuiltPaint,sampler_BuiltPaint,i.uv,0);
            float4 before=SAMPLE_TEXTURE2D_LOD(_RevealBase,sampler_RevealBase,i.uv,0);
            float4 color=_Reveal>=1?now:lerp(before,now,saturate(_Reveal));
            bool useLegacySilhouette=_ExternalCoverage>.5 && _StudyLegacySilhouette>.5;
            float externalSilhouette=useLegacySilhouette
                ?saturate(SAMPLE_TEXTURE2D_LOD(_CurrentPaint,sampler_CurrentPaint,i.uv,0).a/.82):1;
            float occupied=0, differentStroke=0;
            float4 ownStroke=LOAD_TEXTURE2D(_CellStrokes,Cell(i.uv));
            [unroll] for(int oy=-1;oy<=1;oy++)
                [unroll] for(int ox=-1;ox<=1;ox++)
                {
                    int2 neighbor=Cell(CellUv(i.uv,float2(ox,oy)));
                    occupied+=LOAD_TEXTURE2D(_LogicalCoverage,neighbor).a;
                    float4 otherStroke=LOAD_TEXTURE2D(_CellStrokes,neighbor);
                    differentStroke+=step(.001,dot(abs(ownStroke-otherStroke),float4(1,1,1,1)));
                }
            if(occupied>8.999&&differentStroke<.5) return color*externalSilhouette;
            if(occupied<.001) return 0;
            // The accumulated image retains exclusive fixed-cell ownership. Only the outer
            // boundary and adjacent different-stroke claims are reconstructed, using a continuous quadratic support field and
            // normalized neighboring premultiplied pigment. Interior brush grain/layers are
            // untouched; no palette selection and no whole-picture blur is performed.
            float2 p=i.uv*_Cells.xy-.5;
            float2 cell=floor(p+.5), f=p-cell;
            float3 wx=float3(.5*(.5-f.x)*(.5-f.x),.75-f.x*f.x,.5*(.5+f.x)*(.5+f.x));
            float3 wy=float3(.5*(.5-f.y)*(.5-f.y),.75-f.y*f.y,.5*(.5+f.y)*(.5+f.y));
            float coverage=0; float4 reconstructed=0;
            BoundarySample(i.uv,float2(-1,-1),wx.x*wy.x,coverage,reconstructed);
            BoundarySample(i.uv,float2( 0,-1),wx.y*wy.x,coverage,reconstructed);
            BoundarySample(i.uv,float2( 1,-1),wx.z*wy.x,coverage,reconstructed);
            BoundarySample(i.uv,float2(-1, 0),wx.x*wy.y,coverage,reconstructed);
            BoundarySample(i.uv,float2( 0, 0),wx.y*wy.y,coverage,reconstructed);
            BoundarySample(i.uv,float2( 1, 0),wx.z*wy.y,coverage,reconstructed);
            BoundarySample(i.uv,float2(-1, 1),wx.x*wy.z,coverage,reconstructed);
            BoundarySample(i.uv,float2( 0, 1),wx.y*wy.z,coverage,reconstructed);
            BoundarySample(i.uv,float2( 1, 1),wx.z*wy.z,coverage,reconstructed);
            if(coverage>.0001) color=reconstructed/coverage;
            float silhouette=useLegacySilhouette?externalSilhouette:smoothstep(.16,.68,coverage);
            if(coverage<=.0001) return 0;
            return color*saturate(silhouette);
        }
        ENDHLSL
        Pass
        {
            Name "OrderedPremultipliedStamp"
            ZWrite Off ZTest Always Cull Off
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment Stamp
            ENDHLSL
        }
        Pass
        {
            Name "EraseAllAccumulatedLayers"
            ZWrite Off ZTest Always Cull Off
            Blend Zero OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment Erase
            ENDHLSL
        }
        Pass
        {
            Name "RevealAndCoverage"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment Final
            ENDHLSL
        }
        Pass
        {
            Name "SelectedFlatWhiteReference"
            ZWrite Off ZTest Always Cull Off Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment Reference
            ENDHLSL
        }
    }
}
