Shader "Hidden/C6Lab/WatercolorFlatTextureStudy"
{
    // Isolated flat, unlit single-imprint study. Never used by a live round.
    // All source values are authored numerical RGB/alpha, not an albedo sampler.
    Properties
    {
        _MainTex("Original Numerical RGBA", 2D) = "white" {}
        _CurrentBrushV2("Preserved V2 Data Diagnostic", 2D) = "black" {}
        _CurrentBrush("Current V3 Baked Data", 2D) = "black" {}
        _SourceDarkness("Source Premultiplied RGB Darkness and Alpha", 2D) = "black" {}
        _SourceBands("Source Broad, Medium, Fine and Interior", 2D) = "black" {}
        _Mode("0 Current Formula, 1 Source Wash, 2 Source Texture", Range(0,5)) = 1
        _TintDisplay("Display-encoded Pigment RGB (SetVector)", Vector) = (.870588,.176471,.149020,1)
        _PaperDisplay("Display-encoded Flat Paper RGB (SetVector)", Vector) = (1,1,1,1)
        _SourceGain("Shared Source Tone Dose", Range(1,12)) = 6
        _Enhancement("Source-derived Texture Strength", Range(0,1)) = 1
        _MediumGain("Medium Wash Contrast", Range(1,2)) = 1.4
        _FineGain("Fine Source Grain", Range(0,1.5)) = .85
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off Blend Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _CurrentBrush, _CurrentBrushV2, _SourceDarkness, _SourceBands;
            float _Mode, _SourceGain, _Enhancement, _MediumGain, _FineGain;
            float4 _TintDisplay, _PaperDisplay;

            // Exactly one display-to-linear conversion. The harness writes a linear
            // float RT and performs one linear-to-display conversion for its PNGs.
            float3 DisplayToLinear(float3 c)
            {
                c=saturate(c);
                return lerp(c/12.92,pow((c+.055)/1.055,2.4),step(.04045,c));
            }
            float Noise(float2 p)
            { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float BrushNoise(float2 p)
            {
                float2 cell=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Noise(cell),Noise(cell+float2(1,0)),f.x),
                    lerp(Noise(cell+float2(0,1)),Noise(cell+1),f.x),f.y);
            }
            float3 ExistingPigment(float3 c)
            {
                float luma=dot(c,float3(.2126,.7152,.0722));
                float3 chroma=c-luma;
                float3 bounds=min((1-luma)/max(chroma,.000001),luma/max(-chroma,.000001));
                return saturate(luma+chroma*min(1.15,min(bounds.x,min(bounds.y,bounds.z))));
            }
            float3 CurrentFormula(float2 uv)
            {
                // Explicitly a flat transplant of TransmissionStudy's stamp arithmetic:
                // no sphere, game cells, reveal, final-shell noise, lighting or added gloss.
                float2 brushUv=.125+uv*.75;
                float4 brush=tex2D(_CurrentBrush,brushUv);
                float coverage=saturate(brush.g);
                float raw=saturate(brush.r/max(coverage,.0001));
                float pigment=1-exp2(-raw*1.6*4);
                float2 seedOffset=float2(123,0)*.137;
                float2 p=brushUv*4+seedOffset;
                float flow=BrushNoise(p), crossFlow=BrushNoise(p*1.91+float2(17.3,41.7));
                float load=saturate(pigment*.9*lerp(.8,1.2,saturate(flow*.75+crossFlow*.25)));
                float rimNoise=BrushNoise(brushUv*9+seedOffset+float2(5.3,23.1));
                float rimWidth=max(.0012,.025*.38*lerp(1,.45+1.3*rimNoise,.35));
                float edge=(1-smoothstep(rimWidth*.18,rimWidth,brush.b*.125))*coverage;
                float breaks=smoothstep(.25,.68,BrushNoise(brushUv*11+seedOffset+float2(29.7,3.9)));
                edge*=lerp(1,breaks,saturate(.45+.35*.65));
                float pool=edge*.55*.5;
                float mass=saturate(load*1.8*(1+pool*.6));
                float tauWash=-log(.98), tauCap=-log(.05);
                float opacity=1-exp(-(tauWash+(tauCap-tauWash)*coverage*mass));
                float3 tint=pow(max(ExistingPigment(DisplayToLinear(_TintDisplay.rgb)),.004),1+pool*.25);
                return DisplayToLinear(_PaperDisplay.rgb)*(1-opacity)+tint*opacity;
            }
            float3 SourceWash(float2 uv)
            {
                float4 source=tex2D(_SourceDarkness,uv);
                float3 density=source.rgb;
                // The explicit zero branch is a regression invariant: disabling the
                // enhancement returns the same floating-point path as the faithful view.
                if(_Mode>1.5 && _Enhancement>0)
                {
                    float4 bands=tex2D(_SourceBands,uv);
                    float original=dot(density,float3(.2126,.7152,.0722))/max(source.a,.000001);
                    float adjusted=bands.r+bands.g*clamp(_MediumGain,1,2)+bands.b*clamp(_FineGain,0,1.5);
                    // Bound changes to existing source ink. No random speckles, new
                    // silhouettes, pigment floors or halos can be generated outside it.
                    adjusted=clamp(adjusted,original*.65,original*1.35);
                    float ratio=original>.000001?adjusted/original:1;
                    ratio=lerp(1,ratio,saturate(_Enhancement)*saturate(bands.a));
                    density=min(max(density*ratio,0),source.aaa);
                }
                float gain=max(_SourceGain,1);
                float3 amount=gain==1?density:1-pow(max(1-density,0),gain);
                float3 display=_PaperDisplay.rgb-amount*(_PaperDisplay.rgb-_TintDisplay.rgb);
                return DisplayToLinear(display);
            }
            float4 frag(v2f_img i):SV_Target
            {
                if(_Mode>2.5)
                {
                    float3 display;
                    if(_Mode<3.5) display=1-tex2D(_SourceDarkness,i.uv).rgb;
                    else
                    {
                        float2 registered=.125+i.uv*.75;
                        float density=_Mode<4.5?tex2D(_CurrentBrushV2,registered).r:tex2D(_CurrentBrush,registered).r;
                        display=(1-density).xxx;
                    }
                    return float4(DisplayToLinear(display),1);
                }
                return float4(_Mode<.5?CurrentFormula(i.uv):SourceWash(i.uv),1);
            }
            ENDCG
        }
    }
}
