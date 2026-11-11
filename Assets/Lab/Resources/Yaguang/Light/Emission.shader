Shader "Hidden/Hapio/YaguangFlashEmission"
{
    Properties {
        _WhiteColor("Core color", Color)=(1,.985,.94,1)
        _GlowColor("Glow color", Color)=(1,.82,.38,1)
        _FlashRadiance("Flash HDR", Float)=10
        _WarningRadiance("Warning HDR", Float)=2.6
        _SourceArea("Source area", Float)=1
 _Warning("Warning", Float)=0 _WarningStage("Countdown stage", Float)=0 _Wave("Wave", Float)=-1 _Veil("Veil", Float)=0 _Aspect("Aspect", Float)=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Warning, _WarningStage, _Wave, _Veil, _Aspect;
                float4 _WhiteColor, _GlowColor;
                float _FlashRadiance, _WarningRadiance, _SourceArea;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o; }
            float4 Frag(Varyings i):SV_Target
            {
                float2 centered=i.uv-.5;
                float2 p=centered*float2(_Aspect,1);
                float distance=length(p);
                // Elliptical falloff avoids a beveled square frame behind the countdown.
                float inset=_WarningStage>=3 ? .9 : _WarningStage==2 ? .72 : .58;
                float edge=smoothstep(inset,1.12,length(centered*2));
                float3 white=_WhiteColor.rgb;
                float3 gold=_GlowColor.rgb;
                float warmth=_WarningStage>=3 ? .5 : _WarningStage==2 ? .2 : 0;
                float3 warningTint=lerp(white,gold,warmth);
                float3 emission=(warningTint*_WarningRadiance+gold*.35)*edge*_Warning;
                if (_Wave>=0)
                {
                    // Area is animated by the HUD; radiance stays strong until expansion completes.
                    float pulse=pow(1-saturate(_Wave),3);
                    float size=max(.01,_SourceArea)*length(float2(_Aspect,1))*.625;
                    float source=1-smoothstep(size*.8,size,distance);
                    emission+=(white*_FlashRadiance+gold*.12)*pulse*source;
                }
                // Residual glare adds white light rather than alpha-blending a yellow sheet.
                float center=exp(-distance*distance*14);
                float3 veilTint=lerp(gold,white,center);
                emission+=veilTint*(.46+.15*center)*_Veil;
                return float4(emission,1);
            }
            ENDHLSL
        }
    }
}
