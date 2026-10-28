Shader "C6Lab/Jigwi Screen Flame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Transparent ink flame", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlameTime ("Held battle clock", Float) = 0
        _FlameSpeed ("Flame speed", Float) = 2.4
        _Sway ("Upper flame sway", Float) = 0.03
        _Seed ("Phase", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct Attributes { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 worldPosition : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float _FlameTime, _FlameSpeed, _Sway, _Seed;
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }
            fixed4 Frag(Varyings input) : SV_Target
            {
                float age = max(0, _FlameTime) * max(0, _FlameSpeed);
                float2 uv = input.uv;
                float tip = smoothstep(.18, .98, uv.y);
                uv.x += sin(uv.y * 8.0 - age + _Seed) * tip * tip * _Sway;
                uv.y = .22 + (uv.y - .22) * (1.0 + sin(age * 1.7 + _Seed) * .025);
                fixed4 pigment = tex2D(_MainTex, uv) * input.color;
                pigment.a *= step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
                #ifdef UNITY_UI_CLIP_RECT
                pigment.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(pigment.a - .001);
                #endif
                return pigment;
            }
            ENDCG
        }
    }
}
