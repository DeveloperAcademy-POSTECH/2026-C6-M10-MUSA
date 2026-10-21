Shader "C6Lab/PaperShadow" {
 Properties {_Color("Shadow",Color)=(.24,.17,.09,.24)}
 SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 Pass { HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial) float4 _Color; CBUFFER_END
 struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
 V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv*2-1;return o;}
 half4 frag(V i):SV_Target {float r=dot(i.uv,i.uv);return half4(_Color.rgb,_Color.a*exp(-3*r)*smoothstep(1,.45,r));}
 ENDHLSL }
 }
}
