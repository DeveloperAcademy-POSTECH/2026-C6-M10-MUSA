Shader "C6Lab/GourdPaint"
{
 Properties { _BaseColor("White paper shell",Color)=(.96,.94,.86,1) }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
 Pass { Tags {"LightMode"="UniversalForward"}
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;};
 struct V{float4 positionCS:SV_POSITION;float3 p:TEXCOORD0;float3 n:TEXCOORD1;};
 float4 _BaseColor;int _PaintCount;float4 _PaintPositions[128];float4 _PaintColors[128];
 V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.p=i.positionOS.xyz;o.n=TransformObjectToWorldNormal(i.normalOS);return o;}
 half4 frag(V i):SV_Target
 {
 half3 color=_BaseColor.rgb;
 for(int k=0;k<_PaintCount;k++)
 {
 float3 delta=i.p-_PaintPositions[k].xyz;
 float ripple=1+.1*sin(delta.x*135+sin(delta.y*91))*sin(delta.z*119+delta.y*57);
 float radius=_PaintPositions[k].w*ripple;
 float ink=1-smoothstep(radius*.83,radius,length(delta));
 color=lerp(color,_PaintColors[k].rgb,ink*.93);
 }
 float light=.72+.28*saturate(dot(normalize(i.n),normalize(float3(-.4,.8,.5))));
 return half4(color*light,1);
 }
 ENDHLSL
 } }
}
