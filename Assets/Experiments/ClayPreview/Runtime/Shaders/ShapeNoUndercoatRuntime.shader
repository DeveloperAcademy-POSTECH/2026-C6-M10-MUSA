Shader "Hidden/C6Lab/Experiments/ShapeNoUndercoatRuntime"
{
    Properties
    {
        [HideInInspector] _MainTex("Source",2D)="black"{}
        [HideInInspector] _Brush("Prepared Original Silhouette",2D)="black"{}
        [HideInInspector] _CellMask("Erased Cells",2D)="black"{}
        [HideInInspector] _BuiltPaint("Accumulated Color",2D)="black"{}
        [HideInInspector] _BuiltHeight("Accumulated Height",2D)="black"{}
        [HideInInspector] _LogicalCoverage("Actual Cells",2D)="black"{}
        [HideInInspector] _Fallback("Complete Undercoat",2D)="black"{}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
        TEXTURE2D(_Brush);SAMPLER(sampler_Brush);
        TEXTURE2D(_CellMask);SAMPLER(sampler_CellMask);
        TEXTURE2D(_BuiltPaint);SAMPLER(sampler_BuiltPaint);
        TEXTURE2D(_BuiltHeight);SAMPLER(sampler_BuiltHeight);
        TEXTURE2D(_LogicalCoverage);SAMPLER(sampler_LogicalCoverage);
        TEXTURE2D(_Fallback);SAMPLER(sampler_Fallback);
        float4 _Center,_Right,_Up,_Tint,_Cells;
        float _Radius,_Complete;
        float4 _Brush_TexelSize;
        struct Attributes { float4 positionOS:POSITION;float2 uv:TEXCOORD0; };
        struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0; };
        Varyings Vert(Attributes input)
        { Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);output.uv=input.uv;return output; }
        float3 Direction(float2 uv)
        {
            float y=1-2*uv.y,ring=sqrt(max(0,1-y*y)),angle=(uv.x-.5)*2*PI;
            return float3(cos(angle)*ring,y,sin(angle)*ring);
        }
        float4 Brush(float2 uv,out float2 p)
        {
            p=.5;
            float3 d=Direction(uv);float cosine=clamp(dot(d,_Center.xyz),-1,1);
            if(cosine<=0 || _Radius<=0)return 0;
            float sine=sqrt(max(0,1-cosine*cosine));
            float factor=sine>.000001?acos(cosine)/sine:1;
            p=.5+float2(dot(d,_Right.xyz),dot(d,_Up.xyz))*factor/(2*_Radius);
            if(any(p<0)||any(p>1))return 0;
            return SAMPLE_TEXTURE2D_LOD(_Brush,sampler_Brush,p,0);
        }
        float4 Brush(float2 uv)
        {float2 p;return Brush(uv,p);}
        float2 OctSignNotZero(float2 v)
        {return float2(v.x>=0?1:-1,v.y>=0?1:-1);}
        float2 OctEncode(float3 normal)
        {
            float3 n=SafeNormalize(normal);
            if(!(dot(n,n)>.5)||!all(abs(n)<2))n=float3(0,0,1);
            n/=max(abs(n.x)+abs(n.y)+abs(n.z),.000001);
            float2 encoded=n.xy;
            if(n.z<0)encoded=(1-abs(encoded.yx))*OctSignNotZero(encoded);
            return encoded*.5+.5;
        }
        float3 OctDecode(float2 encoded)
        {
            float2 p=saturate(encoded)*2-1;
            float3 n=float3(p,1-abs(p.x)-abs(p.y));
            float fold=saturate(-n.z);
            n.xy+=float2(n.x>=0?-fold:fold,n.y>=0?-fold:fold);
            return SafeNormalize(n);
        }
        float3 StickerCoatingNormal(float2 p)
        {
            // V4 is a stylized sticker plane in the recorded centre frame, not an
            // offset added to the gourd's per-fragment spherical/body normal.
            float3 center=SafeNormalize(_Center.xyz);
            if(!(dot(center,center)>.5)||!all(abs(center)<2))center=float3(0,0,1);
            float3 right=_Right.xyz-center*dot(_Right.xyz,center);
            if(!(dot(right,right)>.000001))right=cross(center,abs(center.y)<.9?float3(0,1,0):float3(1,0,0));
            right=SafeNormalize(right);
            float3 up=SafeNormalize(cross(center,right));
            if(dot(up,_Up.xyz)<0)up=-up;
            float2 texel=max(_Brush_TexelSize.xy,float2(1.0/512,1.0/512));
            float left=SAMPLE_TEXTURE2D_LOD(_Brush,sampler_Brush,p-float2(texel.x,0),0).b;
            float rightHeight=SAMPLE_TEXTURE2D_LOD(_Brush,sampler_Brush,p+float2(texel.x,0),0).b;
            float down=SAMPLE_TEXTURE2D_LOD(_Brush,sampler_Brush,p-float2(0,texel.y),0).b;
            float upHeight=SAMPLE_TEXTURE2D_LOD(_Brush,sampler_Brush,p+float2(0,texel.y),0).b;
            // Existing brush B describes the actual outline's rounded bevel. A small
            // interior dome adds curvature without reverting to a radial-disc rim.
            float2 slope=float2(rightHeight-left,upHeight-down)/(2*texel)*.012;
            slope-=(p-.5)*2*.4;
            if(!all(abs(slope)<100))slope=0;
            const float maximumSlope=.466307658; // tan(25 degrees) relative to this stamp's centre.
            slope*=min(1,maximumSlope/max(length(slope),.0001));
            return SafeNormalize(center-right*slope.x-up*slope.y);
        }
        half4 StampColor(Varyings input):SV_Target
        {
            float4 old=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
            float4 brush=Brush(input.uv);
            float coverage=smoothstep(.12,.88,brush.r);
            // Opaque clay source-over: strength changes pigment RGB, never alpha.
            return float4(_Tint.rgb*coverage+old.rgb*(1-coverage),coverage+old.a*(1-coverage));
        }
        half4 StampHeight(Varyings input):SV_Target
        {
            float4 old=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv);
            float2 p;float4 brush=Brush(input.uv,p);float coverage=smoothstep(.12,.88,brush.r);
            // Rounded source rim plus a small, bounded lift over the previous sheet.
            // Hundreds of layers never increase displacement without bound.
            float shape=saturate(brush.b),raised=saturate(shape*.88+min(old.r*.12,.12));
            float actualCoverage=coverage+old.a*(1-coverage);
            float2 packed=old.gb;
            if(coverage>0)
            {
                float3 incoming=StickerCoatingNormal(p);
                float3 previous=old.a>.0001?OctDecode(old.gb/max(old.a,.0001)):incoming;
                // Oct coordinates must never be averaged across a fold. Composite
                // unit vectors in 3D first, then store the re-encoded unit direction.
                float3 mixed=incoming*coverage+previous*(old.a*(1-coverage));
                if(!(dot(mixed,mixed)>.000001)||!all(abs(mixed)<2))mixed=incoming;
                packed=OctEncode(mixed)*actualCoverage;
            }
            // Color, physical height R and actual coverage A retain the legacy math.
            return float4(lerp(old.r,raised,coverage),packed,coverage+old.a*(1-coverage));
        }
        int2 Cell(float2 uv)
        {return int2(min(floor(frac(uv.x)*_Cells.x),_Cells.x-1),clamp(floor(uv.y*_Cells.y),0,_Cells.y-1));}
        half4 Erase(Varyings input):SV_Target
        {
            float erased=LOAD_TEXTURE2D(_CellMask,Cell(input.uv)).a;
            return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv)*(1-erased);
        }
        float Support(float2 uv)
        {
            // This is already the dense inclusive two-cell presentation support.
            // CPU updates its cached counts only when actual cells toggle. One bilinear
            // lookup preserves the original asset without sparse +/-2 sampling gaps.
            return saturate(SAMPLE_TEXTURE2D(_LogicalCoverage,sampler_LogicalCoverage,uv).a);
        }
        half4 FinalColor(Varyings input):SV_Target
        {
            float4 art=SAMPLE_TEXTURE2D(_BuiltPaint,sampler_BuiltPaint,input.uv);
            if(_Complete>.5)
            {
                // Comparison-only variant: display accumulated stickers, without completion fill.
                return art;
            }
            return art*Support(input.uv);
        }
        half4 FinalHeight(Varyings input):SV_Target
        {
            float4 h=SAMPLE_TEXTURE2D(_BuiltHeight,sampler_BuiltHeight,input.uv);
            // Comparison-only: remove the completion base from physical R as well as color.
            // Coating normal GB and actual sticker A remain unchanged.
            if(_Complete>.5)return h;
            return h*Support(input.uv);
        }
        ENDHLSL
        Pass { Name "OriginalClayColor" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment StampColor
            ENDHLSL }
        Pass { Name "RoundedClayHeight" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment StampHeight
            ENDHLSL }
        Pass { Name "EraseAllHistoricalLayers" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Erase
            ENDHLSL }
        Pass { Name "DisplayClayColor" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FinalColor
            ENDHLSL }
        Pass { Name "DisplayClayHeight" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FinalHeight
            ENDHLSL }
    }
    FallBack Off
}
