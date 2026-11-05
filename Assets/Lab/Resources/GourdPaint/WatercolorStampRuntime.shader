Shader "Hidden/C6Lab/WatercolorBrushStamps"
{
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
            float coverage=saturate(brush.g);
            float pigment=saturate(brush.r/max(coverage,.0001));
            if(abs(_PigmentContrast-1)>.000001)
                pigment=saturate((pigment-.45)*_PigmentContrast+.45);
            // A stable faint wash covers funded cells even where the visual brush has a dry
            // hole. It is never used to decide gameplay area. Source-over preserves paper.
            float baseWash=_OverlapOnly>.5?0:.14;
            float edge=(1-smoothstep(0,max(_EdgeWidth,.0001),brush.b*.125))*coverage;
            if(_RimBreakup>0)
            {
                float broken=smoothstep(.27,.73,BrushNoise(brushUv*18+_Seed.xy*.137));
                edge*=lerp(1,broken,saturate(_RimBreakup));
            }
            float opacity=_Density*(baseWash+coverage*(.32+.9*pigment)+_EdgeStrength*edge*.28);
            opacity*=lerp(.88,1.1,frac(_Seed.x*.17+_Seed.y*.31));
            if(_OverlapOnly>.5) opacity*=coverage*_Overlap;
            // Dense pigment has stronger optical absorption than a thin wash. Keep the
            // low-density paper-through areas, while concentrated pools lose the pale
            // paper veil. Contrast=1 bypasses this path and retains the V1 arithmetic.
            if(_PigmentContrast>1.000001)
            {
                float concentration=smoothstep(.1,.7,pigment);
                if(concentration>.000001)
                {
                    float opticalDepth=1+1.2*saturate(_PigmentContrast-1)*concentration;
                    opacity=1-pow(max(1-opacity,0),opticalDepth);
                }
            }
            opacity=clamp(opacity,0,.94);
            float edgeAbsorption=.10+.12*saturate(_PigmentContrast-1);
            float brightness=1-_EdgeStrength*edge*edgeAbsorption;
            return half4(saturate(PigmentColor(_Tint.rgb)*brightness)*opacity,opacity);
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
                pigment+=lerp(before,now,saturate(_Reveal))*weight;
            }
        }
        half4 Final(Varyings i):SV_Target
        {
            float4 now=SAMPLE_TEXTURE2D_LOD(_BuiltPaint,sampler_BuiltPaint,i.uv,0);
            float4 before=SAMPLE_TEXTURE2D_LOD(_RevealBase,sampler_RevealBase,i.uv,0);
            float4 color=lerp(before,now,saturate(_Reveal));
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
