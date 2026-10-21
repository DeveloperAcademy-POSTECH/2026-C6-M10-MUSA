Shader "C6Lab/Environment/InkCard"
{
 Properties { _BaseMap("PNG",2D)="white"{} _BaseColor("Color",Color)=(1,1,1,1) _Cutoff("Alpha Cutoff",Range(0,1))=.005 _WindAmount("Wind",Float)=0 _WindSpeed("Wind Speed",Float)=1 _Billboard("Billboard",Float)=0 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
  Pass
  {
   Tags { "LightMode"="SRPDefaultUnlit" }
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST; half4 _BaseColor; float _Cutoff; float _WindAmount; float _WindSpeed; float _Billboard;
   CBUFFER_END
   struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
   struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half alpha:TEXCOORD1; };
   Varyings vert(Attributes v)
   {
    Varyings o;float3 p=v.positionOS.xyz;float3 origin=TransformObjectToWorld(float3(0,0,0));
    p.x+=sin(_Time.y*_WindSpeed+origin.x*.73+origin.z*.51+v.uv.y*1.8)*_WindAmount*v.uv.y*v.uv.y;
    float3 world=TransformObjectToWorld(p);
    if(_Billboard>.5)
    {
     float3 toCamera=GetCameraPositionWS()-origin;toCamera.y=0;toCamera=normalize(toCamera+float3(.00001,0,0));
     float3 right=normalize(cross(float3(0,1,0),toCamera));
     float3 size=float3(length(TransformObjectToWorldDir(float3(1,0,0),false)),length(TransformObjectToWorldDir(float3(0,1,0),false)),length(TransformObjectToWorldDir(float3(0,0,1),false)));
     world=origin+right*p.x*size.x+float3(0,1,0)*p.y*size.y+toCamera*p.z*size.z;
    }
    o.positionCS=TransformWorldToHClip(world);o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.alpha=v.color.a;return o;
   }
   half4 frag(Varyings i):SV_Target { half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;c.a*=i.alpha;clip(c.a-_Cutoff);return c; }
   ENDHLSL
  }
 }
}
