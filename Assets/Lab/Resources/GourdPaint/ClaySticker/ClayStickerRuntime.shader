Shader "Hidden/C6Lab/ClayStickerRuntime"
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
        struct Attributes { float4 positionOS:POSITION;float2 uv:TEXCOORD0; };
        struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0; };
        Varyings Vert(Attributes input)
        { Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);output.uv=input.uv;return output; }
        float3 Direction(float2 uv)
        {
            float y=1-2*uv.y,ring=sqrt(max(0,1-y*y)),angle=(uv.x-.5)*2*PI;
            return float3(cos(angle)*ring,y,sin(angle)*ring);
        }
        float4 Brush(float2 uv)
        {
            float3 d=Direction(uv);float cosine=clamp(dot(d,_Center.xyz),-1,1);
            if(cosine<=0 || _Radius<=0)return 0;
            float sine=sqrt(max(0,1-cosine*cosine));
            float factor=sine>.000001?acos(cosine)/sine:1;
            float2 p=.5+float2(dot(d,_Right.xyz),dot(d,_Up.xyz))*factor/(2*_Radius);
            if(any(p<0)||any(p>1))return 0;
            return SAMPLE_TEXTURE2D_LOD(_Brush,sampler_Brush,p,0);
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
            float4 brush=Brush(input.uv);float coverage=smoothstep(.12,.88,brush.r);
            // Rounded source rim plus a small, bounded lift over the previous sheet.
            // Hundreds of layers never increase displacement without bound.
            float shape=saturate(brush.b),raised=saturate(shape*.88+min(old.r*.12,.12));
            return float4(lerp(old.r,raised,coverage),0,0,coverage+old.a*(1-coverage));
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
                float4 coat=SAMPLE_TEXTURE2D(_Fallback,sampler_Fallback,input.uv);
                return float4(art.rgb+coat.rgb*(1-art.a),1);
            }
            return art*Support(input.uv);
        }
        half4 FinalHeight(Varyings input):SV_Target
        {
            float4 h=SAMPLE_TEXTURE2D(_BuiltHeight,sampler_BuiltHeight,input.uv);
            if(_Complete>.5)return float4(max(h.r,.16*(1-h.a)),0,0,1);
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
