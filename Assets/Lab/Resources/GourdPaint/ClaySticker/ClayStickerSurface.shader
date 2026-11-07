Shader "C6Lab/ClayStickerSurface"
{
    Properties
    {
        [NoScaleOffset] _PaintMap("Retained Clay Color",2D)="black"{}
        [NoScaleOffset] _HeightMap("Rounded Clay Height",2D)="black"{}
        [NoScaleOffset] _PainterMap("Authoritative Painter",2D)="black"{}
        _PaintMapReady("Artwork Ready",Float)=0
        _ClayThickness("Clay Thickness",Range(0,.02))=.006
        _ClayGloss("Clay Gloss",Range(0,1))=.25
        [HideInInspector] [NoScaleOffset] _ClayPreviewTexture("Preview Neutral Texture",2D)="gray"{}
        [HideInInspector] _ClayPreviewTextureEnabled("Preview Texture Override",Float)=0
        [HideInInspector] _ClayPreviewTextureScale("Preview Texture Scale",Float)=1
        [HideInInspector] _ClayPreviewTextureColorStrength("Preview Texture Color Strength",Float)=.5
        [HideInInspector] _ClayPreviewTextureNormalStrength("Preview Texture Normal Strength",Float)=.2
        [HideInInspector] _ClayPreviewLighting("Preview Light Override",Float)=0
        [HideInInspector] _ClayPreviewLightDirection("Preview World Direction To Light",Vector)=(0,0,1,0)
        [HideInInspector] _ClayPreviewLightIntensity("Preview Direct Light Intensity",Float)=1
        [Toggle] _PlayerPaintPatternsEnabled("Player Ownership Patterns",Float)=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20" }
        Pass
        {
            Name "CurvedClaySurface"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite Off ZTest LEqual
            Offset -1,-1
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_PaintMap);SAMPLER(sampler_PaintMap);
            TEXTURE2D(_HeightMap);SAMPLER(sampler_HeightMap);
            TEXTURE2D(_PainterMap);SAMPLER(sampler_PainterMap);
            float4 _HeightMap_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                float _PaintMapReady,_ClayThickness,_ClayGloss,_PlayerPaintPatternsEnabled;
                float _ClayPreviewLighting,_ClayPreviewLightIntensity;
                float4 _ClayPreviewLightDirection;
                float _ClayPreviewTextureEnabled,_ClayPreviewTextureScale;
                float _ClayPreviewTextureColorStrength,_ClayPreviewTextureNormalStrength;
            CBUFFER_END
            #include "Assets/Lab/Resources/GourdPaint/ClaySticker/ClayPreviewTexture.hlsl"
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                float3 normalWS:TEXCOORD1;
                float3 sourceOS:TEXCOORD2;
                float fog:TEXCOORD3;
            };
            float2 SurfaceUv(float3 source)
            {float3 d=normalize(source);return float2(atan2(d.z,d.x)/(2*PI)+.5,.5-d.y*.5);}
            // Filter the presentation height, not the pigment silhouette. This avoids
            // moving sharp sub-texel steps through the existing body triangles. Repeat
            // on longitude and Clamp on latitude are owned by the runtime texture.
            float SoftHeight(float2 uv,float radius)
            {
                float2 step=_HeightMap_TexelSize.xy*radius;
                float value=SAMPLE_TEXTURE2D_LOD(_HeightMap,sampler_HeightMap,uv,0).r*.5;
                value+=SAMPLE_TEXTURE2D_LOD(_HeightMap,sampler_HeightMap,uv+float2(step.x,0),0).r*.125;
                value+=SAMPLE_TEXTURE2D_LOD(_HeightMap,sampler_HeightMap,uv-float2(step.x,0),0).r*.125;
                value+=SAMPLE_TEXTURE2D_LOD(_HeightMap,sampler_HeightMap,uv+float2(0,step.y),0).r*.125;
                value+=SAMPLE_TEXTURE2D_LOD(_HeightMap,sampler_HeightMap,uv-float2(0,step.y),0).r*.125;
                return saturate(value);
            }
            float4 SoftStickerData(float2 uv,float radius,out float specularCoverage)
            {
                // Filter premultiplied slopes and their real coverage together.
                // Unpremultiplying before filtering would pull empty texels toward
                // a false normal and put a glossy ring around the complete base.
                float2 step=_HeightMap_TexelSize.xy*radius;
                float4 center=SAMPLE_TEXTURE2D_LOD(_HeightMap,sampler_HeightMap,uv,0);
                // Filtering may borrow a neighbour's normal but must not extend its
                // specular support onto the completion-only undercoat.
                specularCoverage=saturate(center.a);
                float4 value=center*.5;
                value+=SAMPLE_TEXTURE2D_LOD(_HeightMap,sampler_HeightMap,uv+float2(step.x,0),0)*.125;
                value+=SAMPLE_TEXTURE2D_LOD(_HeightMap,sampler_HeightMap,uv-float2(step.x,0),0)*.125;
                value+=SAMPLE_TEXTURE2D_LOD(_HeightMap,sampler_HeightMap,uv+float2(0,step.y),0)*.125;
                value+=SAMPLE_TEXTURE2D_LOD(_HeightMap,sampler_HeightMap,uv-float2(0,step.y),0)*.125;
                return value;
            }
            float3 StickerSpecularNormal(float3 source,float3 baseNormal,float4 data)
            {
                const float maxSlope=.466307658; // tan(25 degrees)
                float support=saturate(data.a);
                if(!(support>.0001))return baseNormal;
                float2 encoded=data.gb/max(support,.0001);
                if(!all(abs(encoded)<2))return baseNormal;
                float2 slope=(saturate(encoded)*2-1)*maxSlope;
                slope*=min(1,maxSlope/max(length(slope),.0001));
                float3 d=SafeNormalize(source);float angle=atan2(d.z,d.x);
                float ring=sqrt(max(0,1-d.y*d.y));
                float3 uOS=float3(-sin(angle),0,cos(angle));
                float3 vOS=float3(d.y*cos(angle),-ring,d.y*sin(angle));
                float3 u=TransformObjectToWorldDir(uOS);
                u=SafeNormalize(u-baseNormal*dot(u,baseNormal));
                float3 v=SafeNormalize(cross(baseNormal,u));
                if(dot(v,TransformObjectToWorldDir(vOS))<0)v=-v;
                float3 tilt=u*slope.x+v*slope.y;
                // Keep finite, bounded normals even at a degenerate model tangent.
                if(!all(abs(tilt)<2))return baseNormal;
                tilt*=min(1,maxSlope/max(length(tilt),.0001));
                return SafeNormalize(baseNormal+tilt);
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;float2 uv=SurfaceUv(input.positionOS.xyz);
                float h=SoftHeight(uv,4);
                float3 position=input.positionOS.xyz+normalize(input.normalOS)*(.00015+saturate(h)*clamp(_ClayThickness,0,.02));
                output.positionCS=TransformObjectToHClip(position);
                output.positionWS=TransformObjectToWorld(position);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.sourceOS=input.positionOS.xyz;output.fog=ComputeFogFactor(output.positionCS.z);return output;
            }
            float PeriodicLine(float coordinate,float width)
            {float distance=abs(frac(coordinate+.5)-.5),aa=max(fwidth(coordinate),.015);return 1-smoothstep(width-aa,width+aa,distance);}
            float PainterInk(float painter,float2 uv)
            {
                float2 q=uv*float2(12,6);if(painter<.5||painter>6.5)return 0;
                if(painter<1.5){float d=length(frac(q)-.5),aa=max(fwidth(d),.015);return 1-smoothstep(.17-aa,.17+aa,d);}
                if(painter<2.5)return PeriodicLine(q.x+q.y,.115);
                if(painter<3.5)return PeriodicLine(q.y,.115);
                if(painter<4.5)return PeriodicLine(q.y+.2*sin(q.x*PI),.115);
                if(painter<5.5)return max(PeriodicLine(q.x,.075),PeriodicLine(q.y,.075));
                return PeriodicLine(q.x-q.y,.115);
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv=SurfaceUv(input.sourceOS);
                half4 paint=SAMPLE_TEXTURE2D(_PaintMap,sampler_PaintMap,uv)*saturate(_PaintMapReady);
                clip(paint.a-.002);
                half3 pigment=paint.rgb/max(paint.a,.0001);
                if(_PlayerPaintPatternsEnabled>.5)
                {float painter=round(SAMPLE_TEXTURE2D(_PainterMap,sampler_PainterMap,uv).r*255);pigment*=1-.32*PainterInk(painter,uv);}
                float footprint=max(length(ddx(uv)*_HeightMap_TexelSize.zw),length(ddy(uv)*_HeightMap_TexelSize.zw));
                float specularCoverage;
                float4 stickerData=SoftStickerData(uv,clamp(footprint,2,5),specularCoverage);
                float normalizedHeight=saturate(stickerData.r);
                float height=normalizedHeight*clamp(_ClayThickness,0,.02);
                float3 baseNormal=normalize(input.normalWS);
                float3 n=baseNormal,dx=ddx(input.positionWS),dy=ddy(input.positionWS);
                float3 a=cross(dy,n),b=cross(n,dx);float determinant=dot(dx,a);
                float3 gradient=(ddx(height)*a+ddy(height)*b)*sign(determinant)/max(abs(determinant),1e-8);
                // A quantized/overlapping height transition must never turn its normal
                // upside down at maximum DEV relief. Keep a broad rounded slope, and
                // suppress it only on unresolved alpha-edge fragments.
                gradient*=min(1,.55/max(length(gradient),.0001));
                gradient*=smoothstep(.2,.9,paint.a);
                n=SafeNormalize(n-gradient);
                ClayPreviewTextureData importedTexture;
                importedTexture.value=0;importedTexture.coverage=0;
                [branch] if(_ClayPreviewTextureEnabled>.5)
                {
                    importedTexture=ClayPreviewReadTexture(input.sourceOS,uv);
                    pigment=ClayPreviewTexturePigment(pigment,importedTexture);
                    n=ClayPreviewTextureNormal(input.positionWS,n,importedTexture);
                }
                float3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sceneKey=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float keyEnergy=dot(sceneKey.color,float3(.2126,.7152,.0722));
                float hasKey=step(.0001,keyEnergy);
                float3 fallbackKey=SafeNormalize(float3(-.4,.8,.5));
                // Keep the guaranteed camera-facing studio key dominant. Several Lab
                // scenes deliberately point their actual key away from the paint face;
                // letting it dominate would flatten both relief and DEV gloss previews.
                float3 keyDirection=SafeNormalize(lerp(fallbackKey,sceneKey.direction,hasKey*.25));
                // Only cloned comparison materials opt into this full world-space key.
                // The disabled branch leaves the original game's lighting math intact.
                float previewIntensity=1;
                [branch] if(_ClayPreviewLighting>.5)
                {
                    float3 suppliedDirection=_ClayPreviewLightDirection.xyz;
                    if(!(dot(suppliedDirection,suppliedDirection)>.000001)||!all(abs(suppliedDirection)<1000000))
                        suppliedDirection=float3(0,0,1);
                    keyDirection=SafeNormalize(suppliedDirection);
                    previewIntensity=clamp(_ClayPreviewLightIntensity,0,2);
                    if(!(previewIntensity>=0))previewIntensity=1;
                }
                // This is an art-directed lit clay surface. A controlled fill preserves
                // recorded pigment hue/value even in the existing custom-lit scenes;
                // URP key direction, shadow and SH still influence form and highlights.
                float ambient=dot(max(SampleSH(n),0),float3(.2126,.7152,.0722));
                float fill=lerp(.96,1.04,saturate(ambient));
                float diffuse=(.78+.22*saturate(dot(n,keyDirection)))*fill;
                [branch] if(_ClayPreviewLighting>.5)
                    diffuse=(.78+.22*saturate(dot(n,keyDirection))*previewIntensity)*fill;
                diffuse*=lerp(1,lerp(.92,1,sceneKey.shadowAttenuation),hasKey);
                float3 specularNormal=StickerSpecularNormal(input.sourceOS,baseNormal,stickerData);
                [branch] if(_ClayPreviewTextureEnabled>.5)
                    specularNormal=ClayPreviewTextureNormal(input.positionWS,specularNormal,importedTexture);
                float gloss=saturate(_ClayGloss);
                float exponent=lerp(12,64,gloss);
                float unresolved=saturate(length(fwidth(specularNormal))*2);
                exponent=lerp(exponent,12,unresolved);
                float3 halfway=SafeNormalize(keyDirection+view);
                // One coated-sticker lobe uses the stamp's own dome normal. The
                // original body-wide soft lobe is gone, and the completion-only
                // undercoat has no specular support. Gloss zero adds exactly zero.
                float highlight=pow(saturate(dot(specularNormal,halfway)),exponent)
                    *gloss*.3*(1-unresolved*.5)*saturate(dot(specularNormal,keyDirection))
                    *specularCoverage;
                [branch] if(_ClayPreviewLighting>.5)highlight*=previewIntensity;
                // A few nearby higher samples make an overlap/contact crease readable.
                // The factor is exactly zero at DEV thickness zero and does not depend
                // on gloss. Filtering and a small dead-zone suppress map quantization.
                float relief=saturate(_ClayThickness/.02);
                float2 contactStep=_HeightMap_TexelSize.xy*lerp(3,6,relief);
                float centerHeight=normalizedHeight;
                float upper=SAMPLE_TEXTURE2D_LOD(_HeightMap,sampler_HeightMap,uv+float2(contactStep.x,0),0).r;
                upper=max(upper,SAMPLE_TEXTURE2D_LOD(_HeightMap,sampler_HeightMap,uv+float2(0,contactStep.y),0).r);
                upper=max(upper,SAMPLE_TEXTURE2D_LOD(_HeightMap,sampler_HeightMap,uv+contactStep*.7071,0).r);
                float contact=smoothstep(.025,.45,max(0,upper-centerHeight))*.22*relief;
                half3 shaded=saturate(pigment)*diffuse*(1-contact)+highlight;
                shaded=MixFog(shaded,input.fog);
                return half4(shaded*paint.a,paint.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
