Shader "Hidden/Hapio/YaguangFlashComposite"
{
    Properties { [PerRendererData] _MainTex("HDR light", 2D)="black" {} _Aspect("Frame aspect", Float)=1 _LightLimit("Maximum light coverage", Float)=1 _Visibility("Remaining glare", Float)=1 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcColor
        ColorMask RGB
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            sampler2D _MainTex;
            float _Aspect, _LightLimit, _Visibility;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
            float4 frag(v2f i):SV_Target
            {
                float2 halfSize=float2(_Aspect,1)*.5;
                float radius=min(_Aspect,1)*.09;
                float2 q=abs((i.uv-.5)*float2(_Aspect,1))-(halfSize-radius);
                float distance=length(max(q,0))+min(max(q.x,q.y),0)-radius;
                float aa=max(fwidth(distance)*1.5,.0005);
                float mask=1-smoothstep(-aa,aa,distance);
                float3 light=max(0,tex2D(_MainTex,i.uv).rgb);
                float peak=max(max(light.r,light.g),light.b);
                light*=min(1,_LightLimit/max(peak,.0001));
                return float4(light*mask*saturate(_Visibility),0);
            }
            ENDCG
        }
    }
}
