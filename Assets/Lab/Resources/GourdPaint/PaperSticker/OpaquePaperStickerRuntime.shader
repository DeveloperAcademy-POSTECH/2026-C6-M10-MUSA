Shader "Hidden/C6Lab/OpaquePaperStickerStamps"
{
    Properties
    {
        _StickerGrainStrength("Opaque Paper Grain", Range(0,.35)) = .12
        _StickerGrainScale("Object-Fixed Paper Grain Tiling", Range(.5,8)) = 2
        _StickerEdgeStrength("Thin Overlap Edge Tone", Range(0,1)) = 0
        _TopStickersEnabled("Actual Top Sticker IDs Available", Float) = 0
        _TopStickers("Actual Top Sticker ID Map", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        // Full source square, with no watercolor 384/512 padding:
        // R/A = the same numerical coverage as the CPU mask, G = .5-centred paper grain,
        // B = optional numerical interior distance. Grain remains defined outside the shape.
        TEXTURE2D(_Brush); SAMPLER(sampler_Brush);
        TEXTURE2D(_CellMask); SAMPLER(sampler_CellMask);
        TEXTURE2D(_BuiltPaint); SAMPLER(sampler_BuiltPaint);
        TEXTURE2D(_LogicalCoverage); SAMPLER(sampler_LogicalCoverage);
        TEXTURE2D(_TopStickers); SAMPLER(sampler_TopStickers);
        float4 _Brush_TexelSize, _BuiltPaint_TexelSize, _TopStickers_TexelSize;
        float4 _Center, _Right, _Up, _Tint, _Cells, _StickerId;
        float _Radius, _OverlapOnly;
        float _StickerGrainStrength, _StickerGrainScale, _StickerEdgeStrength, _TopStickersEnabled;

        struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
        Varyings vert(uint id:SV_VertexID)
        {
            Varyings o; o.positionCS=GetFullScreenTriangleVertexPosition(id);
            o.uv=GetFullScreenTriangleTexCoord(id); return o;
        }
        int2 Cell(float2 uv)
        {
            return int2(min(floor(frac(uv.x)*_Cells.x),_Cells.x-1),
                clamp(floor(uv.y*_Cells.y),0,_Cells.y-1));
        }
        float Mask(float2 uv) { return LOAD_TEXTURE2D(_CellMask,Cell(uv)).a; }
        float3 Direction(float2 uv)
        {
            float y=1-2*uv.y, ring=sqrt(max(0,1-y*y)), angle=(uv.x-.5)*2*PI;
            return float3(cos(angle)*ring,y,sin(angle)*ring);
        }
        float2 BrushTexelUv(float2 uv)
        {
            // CPU mask sampling is u*(width-1), not u*width-.5. Match its texel centres
            // exactly rather than inventing a different GPU silhouette at the threshold.
            return uv*(1-_Brush_TexelSize.xy)+.5*_Brush_TexelSize.xy;
        }
        bool ProjectBrush(float2 surfaceUv,out float2 brushUv)
        {
            brushUv=.5;
            if(_Radius<=0) return false;
            float3 direction=Direction(surfaceUv);
            float cosine=clamp(dot(direction,_Center.xyz),-1,1);
            if(cosine<=0) return false;
            float sine=sqrt(max(0,1-cosine*cosine));
            float factor=sine>.000001?acos(cosine)/sine:1;
            brushUv=.5+float2(dot(direction,_Right.xyz),dot(direction,_Up.xyz))*factor/(2*_Radius);
            return all(brushUv>=0)&&all(brushUv<=1);
        }
        bool CoveredByThisDraw(float2 surfaceUv)
        {
            // New paid cells always receive opaque pigment, including growth cells beyond
            // the initial descriptor. The art descriptor is not a second gameplay gate.
            if(Mask(surfaceUv)<.5) return false;
            if(_OverlapOnly<.5) return true;
            // An overlap may replace only already-covered surface inside this actual
            // splatter. It never paints beyond the existing logical coverage mask.
            float2 brushUv;
            if(!ProjectBrush(surfaceUv,brushUv)) return false;
            float coverage=SAMPLE_TEXTURE2D_LOD(_Brush,sampler_Brush,BrushTexelUv(brushUv),0).r;
            return coverage>=.15;
        }
        float3 PaperColor(float2 surfaceUv)
        {
            // Texture detail is fixed on the surface, not on screen or to the last frame.
            // The entire square's G channel is valid even when a growth cell is outside
            // the original splatter's UV support. Grain changes RGB only; never Alpha.
            float scale=max(_StickerGrainScale,.5);
            float2 paperUv=frac(surfaceUv*float2(scale,scale*.5));
            float grain=SAMPLE_TEXTURE2D(_Brush,sampler_Brush,BrushTexelUv(paperUv)).g;
            float tone=1+(grain-.5)*saturate(_StickerGrainStrength)*3;
            // _Tint is numerical linear RGB from the journal. No second sRGB conversion.
            return saturate(_Tint.rgb*tone);
        }
        half4 Stamp(Varyings i):SV_Target
        {
            if(!CoveredByThisDraw(i.uv)) return 0;
            return half4(PaperColor(i.uv),1);
        }
        half4 Erase(Varyings i):SV_Target { return half4(0,0,0,Mask(i.uv)); }
        half4 StampTopId(Varyings i):SV_Target
        {
            // Pass 3 is optional and writes an encoded uint with Blend Off. Discard is
            // required: returning zero would erase unrelated IDs outside this stamp.
            if(!CoveredByThisDraw(i.uv)) discard;
            return _StickerId;
        }
        float2 CellUv(float2 uv,float2 delta)
        {
            return float2(frac(uv.x+delta.x/_Cells.x),
                clamp(uv.y+delta.y/_Cells.y,.5/_Cells.y,1-.5/_Cells.y));
        }
        float4 ReadCommitted(float2 uv)
        {
            int2 size=int2(_BuiltPaint_TexelSize.zw);
            int2 pixel=int2(min(floor(frac(uv.x)*size.x),size.x-1),
                clamp(floor(uv.y*size.y),0,size.y-1));
            // Never interpolate red and blue across two unrelated sticker claims.
            return LOAD_TEXTURE2D(_BuiltPaint,pixel);
        }
        float4 ReadTop(float2 uv)
        {
            int2 size=int2(_TopStickers_TexelSize.zw);
            int2 pixel=int2(min(floor(frac(uv.x)*size.x),size.x-1),
                clamp(floor(uv.y*size.y),0,size.y-1));
            return LOAD_TEXTURE2D(_TopStickers,pixel);
        }
        float EdgeTone(float2 uv)
        {
            if(_TopStickersEnabled<.5||_StickerEdgeStrength<=0) return 1;
            float4 own=ReadTop(uv);
            if(dot(own,own)<.000001) return 1;
            float2 stepUv=_TopStickers_TexelSize.xy;
            float left=step(.001,dot(abs(own-ReadTop(uv-float2(stepUv.x,0))),float4(1,1,1,1)));
            float right=step(.001,dot(abs(own-ReadTop(uv+float2(stepUv.x,0))),float4(1,1,1,1)));
            float up=step(.001,dot(abs(own-ReadTop(uv+float2(0,stepUv.y))),float4(1,1,1,1)));
            float down=step(.001,dot(abs(own-ReadTop(uv-float2(0,stepUv.y))),float4(1,1,1,1)));
            return 1+saturate(_StickerEdgeStrength)*(.25*max(left,up)-.8*max(right,down));
        }
        void CoverageSample(float2 uv,float2 delta,float weight,
            inout float coverage,inout float bestWeight,inout float4 nearest)
        {
            float2 sampleUv=CellUv(uv,delta);
            float logical=LOAD_TEXTURE2D(_LogicalCoverage,Cell(sampleUv)).a;
            coverage+=weight*logical;
            if(logical>.5&&weight>bestWeight)
            {
                bestWeight=weight; nearest=ReadCommitted(sampleUv);
            }
        }
        half4 Final(Varyings i):SV_Target
        {
            // Coverage and color identity are separate fields. Quadratic support rounds
            // the categorical claim boundary; it never blends the colors of two stickers.
            float4 own=ReadCommitted(i.uv),nearest=0;
            float logical=LOAD_TEXTURE2D(_LogicalCoverage,Cell(i.uv)).a;
            float2 p=i.uv*_Cells.xy-.5,cell=floor(p+.5),f=p-cell;
            float3 wx=float3(.5*(.5-f.x)*(.5-f.x),.75-f.x*f.x,.5*(.5+f.x)*(.5+f.x));
            float3 wy=float3(.5*(.5-f.y)*(.5-f.y),.75-f.y*f.y,.5*(.5+f.y)*(.5+f.y));
            float4 ids[9]; float weights[9]; float scores[9]; float2 centers[9];
            float coverage=0,bestWeight=-1;
            [unroll] for(int y=0;y<3;y++)
            [unroll] for(int x=0;x<3;x++)
            {
                int index=y*3+x;
                float weight=wx[x]*wy[y];
                float2 sampleUv=CellUv(i.uv,float2(x-1,y-1));
                int2 claim=Cell(sampleUv);
                float paid=LOAD_TEXTURE2D(_LogicalCoverage,claim).a;
                coverage+=weight*paid;
                centers[index]=(float2(claim)+.5)/_Cells.xy;
                ids[index]=0; weights[index]=0; scores[index]=0;
                if(paid>.5)
                {
                    if(weight>bestWeight){bestWeight=weight;nearest=ReadCommitted(sampleUv);}
                    if(_TopStickersEnabled>.5)
                    {
                        float4 id=ReadTop(centers[index]);
                        if(dot(id,id)>.000001){ids[index]=id;weights[index]=weight;}
                    }
                }
            }
            float aa=max(fwidth(coverage),.0001);
            float alpha=smoothstep(.5-aa*.5,.5+aa*.5,coverage);
            // Aggregate all support belonging to the SAME encoded sticker, including
            // disconnected pieces in this local neighborhood. Equal scores keep the
            // first row-major candidate, so replay/refresh cannot flip a tied boundary.
            int winner=-1;float winnerWeight=-1;
            [unroll] for(int k=0;k<9;k++)
            {
                float score=0;
                if(weights[k]>0)
                {
                    [unroll] for(int n=0;n<9;n++)
                        if(dot(abs(ids[k]-ids[n]),float4(1,1,1,1))<.001)score+=weights[n];
                    if(score>winnerWeight){winnerWeight=score;winner=k;}
                }
                scores[k]=score;
            }
            int runner=-1;float runnerWeight=-1;
            if(winner>=0)
            {
                [unroll] for(int k=0;k<9;k++)
                    if(weights[k]>0&&dot(abs(ids[k]-ids[winner]),float4(1,1,1,1))>.001&&scores[k]>runnerWeight)
                    {runner=k;runnerWeight=scores[k];}
            }
            float gap=runner>=0?max(0,winnerWeight-runnerWeight):1;
            float lip=1-smoothstep(0,max(fwidth(gap)*1.5,.0001),gap);
            if(alpha<=0||bestWeight<0)return 0;
            float3 pigment=logical>.5?own.rgb:nearest.rgb;
            float tone=1;
            if(winner>=0)
            {
                float4 ownId=ReadTop(i.uv);
                // Retain the original per-pixel fine color inside the selected sticker.
                // Only a displaced boundary takes one representative neighbor color.
                if(dot(abs(ownId-ids[winner]),float4(1,1,1,1))>.001)
                    pigment=ReadCommitted(centers[winner]).rgb;
                if(runner>=0&&_StickerEdgeStrength>0)
                {
                    float4 newest=ids[winner],other=ids[runner];
                    bool above=newest.a!=other.a?newest.a>other.a:
                        newest.b!=other.b?newest.b>other.b:
                        newest.g!=other.g?newest.g>other.g:newest.r>other.r;
                    // The lip follows the continuous categorical tie field, not the old
                    // point-sampled stair-step IDs. It occupies only about 1–2 pixels.
                    tone=1+saturate(_StickerEdgeStrength)*lip*(above?.25:-.8);
                }
            }
            return half4(saturate(pigment*tone)*alpha,alpha);
        }
        ENDHLSL
        Pass
        {
            Name "OpaqueOrderedSticker"
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
            Name "EraseAllStickerLayers"
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
            Name "OpaqueStickerCoverage"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment Final
            ENDHLSL
        }
        Pass
        {
            Name "ActualTopStickerId"
            ZWrite Off ZTest Always Cull Off
            Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment StampTopId
            ENDHLSL
        }
    }
}
