Shader "Hapio/UI/Pause Track Alpha"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; float4 position:TEXCOORD1; };
            sampler2D _MainTex;
            float4 _ClipRect;
            Output vert(Input input)
            {
                Output output;
                output.position=input.vertex;
                output.vertex=UnityObjectToClipPos(input.vertex);
                output.uv=input.uv;
                output.color=input.color;
                return output;
            }
            fixed4 frag(Output input):SV_Target
            {
                fixed4 color=input.color;
                color.a*=tex2D(_MainTex,input.uv).a;
                #ifdef UNITY_UI_CLIP_RECT
                color.a*=UnityGet2DClipping(input.position.xy,_ClipRect);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
