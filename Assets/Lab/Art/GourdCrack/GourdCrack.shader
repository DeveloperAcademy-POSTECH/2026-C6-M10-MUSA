Shader "C6Lab/GourdCrack"
{
 Properties { _BaseColor("Shell",Color)=(0.94,0.75,0.39,1) _CrackStage("Crack stage",Range(0,3))=0 _FinishGlow("Finish glow",Float)=0 _SplitSide("Split side",Float)=0 _SplitPlane("Split plane",Vector)=(1,0,0,0) }
 SubShader
 {
 Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
 Cull Off
 Pass
 {
 Tags { "LightMode"="UniversalForward" }
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
 struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; float3 objectPosition:TEXCOORD2; };
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor; float _CrackStage; float _FinishGlow; float _SplitSide; float4 _SplitPlane;
 CBUFFER_END
 Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.uv=v.uv; o.objectPosition=v.positionOS.xyz; return o; }
 float2 hash(float2 p) { return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453); }
 half4 frag(Varyings i):SV_Target
 {
 if(abs(_SplitSide)>.5) clip(_SplitSide * dot(float4(i.objectPosition,1),_SplitPlane));
 float2 p=i.uv*9; float2 cell=floor(p); float2 f=frac(p); float first=100; float second=100;
 for(int y=-1;y<=1;y++) for(int x=-1;x<=1;x++) { float2 g=float2(x,y); float2 delta=g+hash(cell+g)-f; float d=dot(delta,delta); if(d<first){second=first;first=d;}else if(d<second) second=d; }
 float edge=sqrt(second)-sqrt(first);
 float fissure=1-smoothstep(.014,.037,edge);
 // A deterministic reveal field retains old fissures as new branches spread.
 float reveal=frac(sin(dot(cell,float2(53.7,91.3)))*3124.17);
 float coverage=_CrackStage<.5?0:(_CrackStage<1.5?.16:(_CrackStage<2.5?.4:.75));
 float crack=fissure*step(reveal,coverage)*step(.5,_CrackStage);
 float light=.68+.32*saturate(dot(normalize(i.normalWS),normalize(float3(-.4,.8,.5))));
 half3 color=_BaseColor.rgb*light;
 color=lerp(color,half3(.19,.09,.035),crack);
 color=lerp(color,half3(1,.85,.4),crack*saturate(_FinishGlow));
 return half4(color,1);
 }
 ENDHLSL
 }
 }
}
