#ifndef C6_CLAY_PREVIEW_TEXTURE_INCLUDED
#define C6_CLAY_PREVIEW_TEXTURE_INCLUDED
// Shared by the three comparison surfaces. All callers are guarded by the
// material's opt-in, so ordinary game materials keep their original arithmetic.
TEXTURE2D(_ClayPreviewTexture); SAMPLER(sampler_ClayPreviewTexture);

struct ClayPreviewTextureData
{
    float value;
    float coverage;
};

ClayPreviewTextureData ClayPreviewReadTexture(float3 source, float2 surfaceUv)
{
    ClayPreviewTextureData result;
    // The importer stores neutralized linear grayscale in RGBAHalf, keeping .5
    // exact: a flat image must never tint or bump the artwork.
    const float neutral = .5;
    float scale = clamp(_ClayPreviewTextureScale, .25, 12);
    float3 p = source * scale;
    float3 weights = pow(abs(SafeNormalize(source)), 4);
    weights /= max(weights.x + weights.y + weights.z, .0001);
    float4 x = SAMPLE_TEXTURE2D(_ClayPreviewTexture, sampler_ClayPreviewTexture, p.yz);
    float4 y = SAMPLE_TEXTURE2D(_ClayPreviewTexture, sampler_ClayPreviewTexture, p.zx);
    float4 z = SAMPLE_TEXTURE2D(_ClayPreviewTexture, sampler_ClayPreviewTexture, p.xy);
    // Gentle source textures need visible contrast at game scale. Bound the boost
    // so high-contrast external files never create an unbounded brightness gain.
    result.value = clamp(dot(float3((x.r-neutral)*x.a, (y.r-neutral)*y.a, (z.r-neutral)*z.a), weights) * 4, -.5, .5);
    // Use actual, unfiltered stamp support, not completed color alpha or a
    // filtered neighbour. Completion-only undercoat therefore stays unchanged.
    int2 size = int2(max(_HeightMap_TexelSize.zw, 1));
    int2 texel = int2(min(floor(float2(frac(surfaceUv.x), saturate(surfaceUv.y))*size), size-1));
    result.coverage = saturate(LOAD_TEXTURE2D(_HeightMap, texel).a);
    return result;
}

half3 ClayPreviewTexturePigment(half3 pigment, ClayPreviewTextureData data)
{
    float amount = saturate(_ClayPreviewTextureColorStrength) * data.coverage;
    if (!(amount > 0) || !(abs(data.value) > .000001)) return pigment;
    // A neutral-centered scalar preserves pigment hue and the recorded palette;
    // it is not another opaque colored layer or a change to the paint map.
    return pigment * max(.5, 1 + data.value * .7 * amount);
}

float3 ClayPreviewTextureNormal(float3 positionWS, float3 normalWS, ClayPreviewTextureData data)
{
    float3 dx = ddx(positionWS), dy = ddy(positionWS);
    float3 a = cross(dy, normalWS), b = cross(normalWS, dx);
    float determinant = dot(dx, a);
    float3 gradient = (ddx(data.value)*a + ddy(data.value)*b)
        * sign(determinant) / max(abs(determinant), 1e-8);
    gradient -= normalWS * dot(gradient, normalWS);
    gradient *= min(1, 1 / max(length(gradient), .0001));
    float amount = saturate(_ClayPreviewTextureNormalStrength) * data.coverage;
    if (!(amount > 0) || !(dot(gradient, gradient) > 1e-12)) return normalWS;
    // The imported microtexture can tilt the incoming normal by at most 3 deg.
    // Gloss and physical displacement are independent and never changed here.
    return SafeNormalize(normalWS - gradient * (.052407779 * amount));
}
#endif
