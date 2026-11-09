Shader "C6Lab/Five Elements/Wet Pigment"
{
    Properties
    {
        [MainColor] _BaseColor ("Pigment", Color) = (.8,.2,.04,1)
        _Gloss ("Wet glints", Range(0,1)) = .65
        _Sag ("Gravity at tips", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Gloss;
            float4 _Sag;
            CBUFFER_END

            struct Attributes {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half2 grainCavity : TEXCOORD2;
                half3 clayPoint : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = v.positionOS + _Sag.xyz * v.color.r;
                o.positionWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.grainCavity = v.color.gb;
                o.clayPoint = v.positionOS;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light key = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half nl = dot(n, key.direction);
                half wrap = saturate((nl + .15h) / 1.15h);
                half3 direct = min(key.color, half3(1.20h,1.20h,1.20h));
                half occlusion = i.grainCavity.y;
                half3 fill = clamp(SampleSH(n) * .40h, half3(.12h,.13h,.16h), half3(.23h,.22h,.25h));
                half3 diffuse = fill + direct * (.045h + .81h * wrap * key.shadowAttenuation);
                half3 h = SafeNormalize(key.direction + v);
                half nh = saturate(dot(n,h));
                // A thin, broken reflection reads as a damp skin over dense pigment.
                half striation = sin(38.h*i.clayPoint.x + 7.h*sin(10.h*i.clayPoint.z+4.h*i.clayPoint.y));
                half damp = .5h + .5h*sin(8.h*i.clayPoint.z - 11.h*i.clayPoint.x + 2.h*sin(5.h*i.clayPoint.y));
                half glint = pow(nh, lerp(40.h, 113.h, _Gloss)) * (.31h+.12h*striation)
                    + pow(nh, 18.h) * (.064h+.11h*damp);
                half grain = saturate(i.grainCavity.x + striation*.032h);
                half3 pigment = _BaseColor.rgb * diffuse * lerp(.69h,1.03h,grain) * occlusion;
                return half4(pigment + _Gloss * direct * glint * occlusion * key.shadowAttenuation, 1.h);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Gloss;
            float4 _Sag;
            CBUFFER_END
            float3 _LightDirection, _LightPosition;
            struct Input
            {
                float3 pos : POSITION;
                float3 normal : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            float4 ShadowVert(Input v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 p = TransformObjectToWorld(v.pos + _Sag.xyz * v.color.r);
                float3 n = TransformObjectToWorldNormal(v.normal);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 l = normalize(_LightPosition-p);
                #else
                    float3 l = _LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p,n,l)));
            }
            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
