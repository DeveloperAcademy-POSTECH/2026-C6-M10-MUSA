Shader "Hidden/C6Lab/WatercolorTransmissionStudy"
{
    // Editor-only transmission experiment. Geometry, cell gates and final reconstruction reuse V3.
    // No game resource or round uses this shader. Ordinary source-over, not physical pigment mixing.
    Properties
    {
        _StudyPigmentLoad("Source Pigment Loading", Range(.5,3)) = 1.8
        _StudyGlaze("Gentle Overlap Glazing (0 or 1)", Range(0,1)) = 0
        _StudyWashOpacity("Minimum Funded Wash Opacity", Range(0,.15)) = .02
        _StudyFlowStrength("Multiplicative Wash Variation", Range(0,.5)) = .2
        _StudyOverlapDose("Overlap Optical Thickness", Range(.1,1)) = .55
        _V3LayerOpacityCap("Layer Opacity Cap", Range(.05,.95)) = .75
        _V3WashOpacity("Funded Dry-Brush Wash", Range(.01,.3)) = .12
        _V3PigmentDepth("Pigment Depth", Range(0,3)) = 1.4
        _V3PoolStrength("Pigment Pool Strength", Range(0,1)) = .5
        _V3FlowScale("Brush Flow Scale", Range(1,12)) = 4
        _V3RimWobble("Seeded Pool Variation", Range(0,1)) = .35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_Brush); SAMPLER(sampler_Brush);
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
        float _StudyGlaze, _StudyWashOpacity, _StudyFlowStrength, _StudyOverlapDose, _StudyPigmentLoad;
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
        float Noise(float2 p)
        { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float BrushNoise(float2 position)
        {
            float2 cell=floor(position), f=frac(position);
            f=f*f*(3-2*f);
            return lerp(lerp(Noise(cell),Noise(cell+float2(1,0)),f.x),
                lerp(Noise(cell+float2(0,1)),Noise(cell+1),f.x),f.y);
        }
        float3 PigmentColor(float3 color)
        {
            // Palette bytes already contain linear pigment. Expand chroma around its
            // luminance, reducing the requested gain only at the RGB gamut boundary.
            // Scaling the whole chroma vector avoids per-channel clipping and hue shifts.
            if(abs(_PigmentSaturation-1)<.000001) return color;
            float luma=dot(color,float3(.2126,.7152,.0722));
            float3 chroma=color-luma;
            float3 bounds=min((1-luma)/max(chroma,.000001),luma/max(-chroma,.000001));
            float maximum=min(bounds.x,min(bounds.y,bounds.z));
            return saturate(luma+chroma*min(_PigmentSaturation,maximum));
        }
        half4 Stamp(Varyings i):SV_Target
        {
            if(Mask(i.uv)<.5) return 0;
            float3 d=Direction(i.uv); float cosine=clamp(dot(d,_Center.xyz),-1,1);
            float4 brush=0;
            float2 brushUv=.5;
            if(cosine>0&&_Radius>0)
            {
                float sine=sqrt(max(0,1-cosine*cosine));
                float factor=sine>.000001?acos(cosine)/sine:1;
                brushUv=.5+float2(dot(d,_Right.xyz),dot(d,_Up.xyz))*factor*.75/(2*_Radius);
                if(all(brushUv>=0)&&all(brushUv<=1)) brush=SAMPLE_TEXTURE2D_LOD(_Brush,sampler_Brush,brushUv,0);
            }
            // Baked data remains linear: R is shape-weighted density, G/A is
            // shape and B is distance inward from its boundary. No gamma transform.
            float coverage=saturate(brush.g);
            float rawPigment=saturate(brush.r/max(coverage,.0001));
            // Empirical monotonic calibration for the V3 no-floor density bake.
            // Its faint original pigment must not be removed by a mid-gray pivot.
            float pigment=1-exp2(-rawPigment*max(_PigmentContrast,0)*4);

            float2 seedOffset=_Seed.xy*.137+_Seed.z*float2(.731,.379);
            float scale=max(_V3FlowScale,1);
            float2 flowPosition=brushUv*scale+seedOffset;
            float flow=BrushNoise(flowPosition);
            float crossFlow=BrushNoise(flowPosition*1.91+float2(17.3,41.7));
            // Preserve the source wash's quiet internal variation. Multiplication cannot
            // invent a dense stain in a source-light region, unlike additive noise.
            float flowVariation=lerp(1-saturate(_StudyFlowStrength),1+saturate(_StudyFlowStrength),
                saturate(flow*.75+crossFlow*.25));
            float pigmentLoad=saturate(pigment*saturate(_Density)*flowVariation);

            // A narrow inward band pools pigment. Seeded width and interruptions
            // prevent a uniform contour; pooling changes RGB, never shape/alpha.
            float rimNoise=BrushNoise(brushUv*9.0+seedOffset+float2(5.3,23.1));
            float rimWobble=saturate(_V3RimWobble);
            float rimWidth=max(.0012,min(max(_EdgeWidth,0),.08)*.38
                *lerp(1,.45+1.3*rimNoise,rimWobble));
            float distanceInside=brush.b*.125;
            float edge=(1-smoothstep(rimWidth*.18,rimWidth,distanceInside))*coverage;
            float breaks=smoothstep(.25,.68,
                BrushNoise(brushUv*11.0+seedOffset+float2(29.7,3.9)));
            edge*=lerp(1,breaks,saturate(_RimBreakup+rimWobble*.65));
            float pool=edge*saturate(_EdgeStrength)*saturate(_V3PoolStrength);

            // Optical thickness controls dilution: pale source regions now transmit
            // more paper/earlier pigment. The cap is an upper bound, not filled-shape alpha.
            // .02 wash is visual only and stays inside the exact paid-cell mask.
            float cap=clamp(_V3LayerOpacityCap,.01,.95);
            float wash=min(saturate(_StudyWashOpacity),cap);
            float tauCap=-log(max(1-cap,.001));
            float tauWash=-log(max(1-wash,.001));
            float mass=saturate(pigmentLoad*clamp(_StudyPigmentLoad,.5,3)*(1+pool*.6));
            float tau=tauWash+(tauCap-tauWash)*coverage*mass;
            if(_OverlapOnly>.5)
            {
                // One dose per stroke, not per growth cell or display update. The two
                // improved columns differ only here; initial/new-cell washes are identical.
                float retention=lerp(1,clamp(_StudyOverlapDose,.1,1),saturate(_StudyGlaze));
                tau=tauCap*coverage*mass*saturate(_Overlap)*retention;
            }
            float opacity=1-exp(-max(tau,0));
            // No concentration floor or whitening of RGB. Dilution follows transmission.
            // A small seeded pool strengthens the pigment, never changes logical coverage.
            float3 pigmentColor=pow(max(PigmentColor(_Tint.rgb),.004),1+pool*.25);
            return half4(pigmentColor*opacity,opacity);
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
            float externalSilhouette=_ExternalCoverage>.5
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
            float silhouette=_ExternalCoverage>.5?externalSilhouette:smoothstep(.16,.68,coverage);
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
    }
}
