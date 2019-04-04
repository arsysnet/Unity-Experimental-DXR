#ifndef DIRECT_LIGHTING_H
#define DIRECT_LIGHTING_H

#include "commonCL.h"

static float3 CalculateJitteredLightVec(bool forceHardShadow, LightBuffer light, float3 rayDir, float maxT, float2 sample2D)
{
    int lightType = light.lightType;

    bool shouldSkipJittering = (forceHardShadow || lightType == kLightRectangle || lightType == kLightDisc);
    if (shouldSkipJittering)
        return rayDir;

    float lightDist = (lightType == kLightDirectional) ? 1.0f : maxT;
    float3 toLight = rayDir * lightDist;
    float shadowRadius = light.pos.w;

    // Construct basis
    float3 u;
    float3 v;
    CreateOrthoNormalBasis(rayDir, &u, &v);

    // Jitter the position of the light to fake a volume (for punctual lights)
    // TODO(RadeonRays) This jitter on a square were we would like a circle
    // CPU lightmapper seems to do the same however and both should be investigated and fixed at the same time
    // see favro https://favro.com/organization/c564ede4ed3337f7b17986b6/c49f564b3ca1630d4475d838?card=Uni-55286
    float2 jitterOffset = shadowRadius * (sample2D * 2.0f - 1.0f);
    toLight += jitterOffset.x * u + jitterOffset.y * v;

    return normalize(toLight);
}

static void PrepareShadowRay(
    const LightBuffer       light,
    const float2            sample2D,
    const float3            surfacePosition,
    const float3            surfaceNormal,
    float                   pushOff,
    int                     idx,
    bool                    forceHardShadow,
    __private ray*          dstRay
)
{
#ifdef PROBES
    const float3 pushOffPos = surfacePosition;
#else
    const float3 pushOffPos = surfacePosition + surfaceNormal * pushOff;
#endif

    // Limit ray length to remove length checking in process (for all but kLightDirectional)
    const float lightRange = light.dir.w;
    const float maxt = (light.lightType == 1) ? 1000000.0f : length(light.pos.xyz - surfacePosition);
    const bool outOfRange = (light.lightType == 1) ? false : (maxt > lightRange);
    if (outOfRange)
    {
        Ray_SetInactive(dstRay);
        return;
    }

    if (light.lightType == kLightSpot)
    {
        // Shoot rays against spot light cone.
        float3 rayDir = normalize(light.pos.xyz - surfacePosition);
        float nDotL = 1.0f;
        const float cosConeAng = light.dataUnion.spotLightData.cosineConeAngle;
        float dval = dot(rayDir, -light.dir.xyz);
        if (dval < cosConeAng)
        {
            Ray_SetInactive(dstRay);
        }
        else
        {
#ifndef PROBES
            nDotL = IsNormalValid(surfaceNormal) ? dot(rayDir, surfaceNormal) : 1.0f;
            bool shouldCancelRay = nDotL < 0.0f || any(isnan(nDotL));
            if (shouldCancelRay)
            {
                Ray_SetInactive(dstRay);
            }
            else
#endif
            {
                rayDir = CalculateJitteredLightVec(forceHardShadow, light, rayDir, maxt, sample2D);
                Ray_Init(dstRay, pushOffPos, rayDir, maxt, nDotL, 0xFFFFFFFF);
            }
        }
    }
    else if (light.lightType == kLightDirectional)
    {
        float3 rayDir = -normalize(light.dir.xyz);
        float nDotL = 1.0f;
#ifndef PROBES
        nDotL = IsNormalValid(surfaceNormal) ? dot(rayDir, surfaceNormal) : 1.0f;
        bool shouldCancelRay = nDotL < 0.0f || any(isnan(nDotL));
        if (shouldCancelRay)
        {
            Ray_SetInactive(dstRay);
        }
        else
#endif
        {
            rayDir = CalculateJitteredLightVec(forceHardShadow, light, rayDir, maxt, sample2D);
            Ray_Init(dstRay, pushOffPos, rayDir, maxt, nDotL, 0xFFFFFFFF);
        }
    }
    else if (light.lightType == kLightPoint)
    {
        float3 rayDir = normalize(light.pos.xyz - surfacePosition);
        float nDotL = 1.0f;
#ifndef PROBES
        nDotL = IsNormalValid(surfaceNormal) ? dot(rayDir, surfaceNormal) : 1.0f;
        bool shouldCancelRay = nDotL < 0.0f || any(isnan(nDotL));
        if (shouldCancelRay)
        {
            Ray_SetInactive(dstRay);
        }
        else
#endif
        {
            rayDir = CalculateJitteredLightVec(forceHardShadow, light, rayDir, maxt, sample2D);
            Ray_Init(dstRay, pushOffPos, rayDir, maxt, nDotL, 0xFFFFFFFF);
        }
    }
    else if (light.lightType == kLightRectangle)
    {
        const float3 lightDir = normalize(light.dir.xyz);
        const float3 lightPos = light.pos.xyz;
        // light backfacing ?
        if (dot(lightDir, surfacePosition.xyz - lightPos) < 0.f)
        {
            Ray_SetInactive(dstRay);
        }
        else
        {
            const float width = light.dataUnion.areaLightData.areaWidth;
            const float height = light.dataUnion.areaLightData.areaHeight;
            const float2 sq = sample2D;
            const float3 lightTan = light.dataUnion.areaLightData.Tangent.xyz;
            const float3 lightBitan = light.dataUnion.areaLightData.Bitangent.xyz;
            const float3 s = lightPos - (.5f * width * lightBitan) - (.5f * height * lightTan);
            float solidAngle;
            float nDotL = 1.0f;
            float3 rayDir = SphQuadSample(s, lightTan * height, lightBitan * width, surfacePosition.xyz, sq.x, sq.y, &solidAngle) - surfacePosition.xyz;

            if (isnan(solidAngle))
            {
                Ray_SetInactive(dstRay);
            }
            else
            {
                const float maxT = length(rayDir);
                rayDir /= maxT;
#ifndef PROBES
                // light ray backfacing ?
                nDotL = IsNormalValid(surfaceNormal) ? dot(rayDir, surfaceNormal) : 1.0f;
                bool shouldCancelRay = nDotL < 0.0f || any(isnan(nDotL));
                if (shouldCancelRay)
                {
                    Ray_SetInactive(dstRay);
                }
                else
#endif
                {
                    Ray_Init(dstRay, pushOffPos, rayDir, maxT, solidAngle * nDotL, 0xFFFFFFFF);
                }
            }
        }
    }
    else if (light.lightType == kLightDisc)
    {
        const float3 lightDir = normalize(light.dir.xyz);
        const float3 lightPos = light.pos.xyz;

        // is light backfacing?
        if (dot(lightDir, surfacePosition.xyz - lightPos) < 0.f)
        {
            Ray_SetInactive(dstRay);
        }
        else
        {
            // Sample uniformly on 2d disc area
            const float radius = light.dataUnion.discLightData.radius;
            float2 sq = sample2D;
            float rLocal = sqrt(sq.x);
            float thetaLocal = 2.0 * PI * sq.y;
            float2 samplePointLocal = make_float2(cos(thetaLocal), sin(thetaLocal)) * rLocal * radius;

            // Convert sample point to world space
            float3 lightTan = -normalize(light.dataUnion.discLightData.Tangent.xyz);
            float3 lineCross = cross(lightDir, lightTan);
            float3 samplePointWorld = lightPos + samplePointLocal.x * lightTan + samplePointLocal.y * lineCross;

            // Limit ray length to remove length checking in process.
            float3 rayDir = samplePointWorld - surfacePosition;
            const float maxT = length(rayDir);
            float nDotL = 1.0f;
            rayDir /= maxT;
#ifndef PROBES
            // light ray backfacing ?
            nDotL = IsNormalValid(surfaceNormal) ? dot(rayDir, surfaceNormal) : 1.0f;
            bool shouldCancelRay = nDotL < 0.0f || any(isnan(nDotL));
            if (shouldCancelRay)
            {
                Ray_SetInactive(dstRay);
            }
            else
#endif
            {
                Ray_Init(dstRay, pushOffPos, rayDir, maxT, nDotL, 0xFFFFFFFF);
            }
        }
    }
    // Set the index so we can map to the originating texel
    Ray_SetIndex(dstRay, idx);
}

static float SampleFalloff(const int falloffIndex, const float normalizedSamplePosition, __global const float* restrict distanceFalloffs_buffer KERNEL_VALIDATOR_BUFFERS_DEF)
{
    const int sampleCount = LIGHTMAPPER_FALLOFF_TEXTURE_WIDTH;
    float index = normalizedSamplePosition * (float)sampleCount;

    // compute the index pair
    int loIndex = min((int)index, (sampleCount - 1));
    int hiIndex = min((int)index + 1, (sampleCount - 1));
    float hiFraction = (index - (float)loIndex);

    const int offset = falloffIndex * LIGHTMAPPER_FALLOFF_TEXTURE_WIDTH;
    const float sampleLo = INDEX_SAFE(distanceFalloffs_buffer, offset + loIndex);
    const float sampleHi = INDEX_SAFE(distanceFalloffs_buffer, offset + hiIndex);

    // do the lookup
    return (1.0 - hiFraction) * sampleLo + hiFraction * sampleHi;
}

static float3 ShadeDirectionalLight(
    const LightBuffer        light,
    const ray                surfaceToLightRay
)
{
    const float nDotL = surfaceToLightRay.d.w;
    return nDotL * light.col.xyz;
}

static float3 ShadeRectangularAreaLight(
    const LightBuffer       light,
    const ray               surfaceToLightRay
)
{
    const float nDotLAndSolidAngle = surfaceToLightRay.d.w;
    return nDotLAndSolidAngle / PI * light.col.xyz;
}

static float3 ShadeDiscLight(
    const LightBuffer       light,
    const ray               surfaceToLightRay
)
{
    const float radius = light.dataUnion.discLightData.radius;
    const float maxTOut = surfaceToLightRay.o.w;
    const float3 lightDir = normalize(light.dataUnion.discLightData.Normal.xyz);
    const float3 lightVecOut = surfaceToLightRay.d.xyz;
    const float nDotL = surfaceToLightRay.d.w;

    // * (Pi / Pi) removed from the expression below as it cancels out.
    return (nDotL * radius * radius * dot(lightDir, -lightVecOut)) / (maxTOut * maxTOut) * light.col.xyz;
}

// This code must be kept in sync with FalloffLUT.cpp::LookupFalloffLUT
static float LookupAngularFalloffLUT(float angularScale, __global const float* restrict angularFalloffLUT_buffer KERNEL_VALIDATOR_BUFFERS_DEF)
{
    const int gAngularFalloffTableLength = 128; // keep in sync with Editor\Src\GI\Progressive\RadeonRays\RRBakeTechnique.cpp
    const int sampleCount = gAngularFalloffTableLength;

    //======================================
    // light distance falloff lookup:
    //   d = Max(0, distance - m_Radius) / (m_CutOff - m_Radius)
    //   index = (g_SampleCount - 1) / (1 + d * d * (g_SampleCount - 2))
    float tableDist = max(angularScale, 0.0f);
    float index = (float)(sampleCount - 1) / (1.0f + tableDist * tableDist * (float)(sampleCount - 2));

    // compute the index pair
    int loIndex = min((int)(index), (sampleCount - 1));
    int hiIndex = min((int)(index) + 1, (sampleCount - 1));
    float hiFraction = (index - (float)(loIndex));

    // do the lookup
    return (1.0 - hiFraction) * INDEX_SAFE(angularFalloffLUT_buffer, loIndex) + hiFraction * INDEX_SAFE(angularFalloffLUT_buffer, hiIndex);
}

static float3 ShadePointLight(
    const LightBuffer        light,
    const ray                surfaceToLightRay,
    const int                falloffIndex,
    __global const float* restrict distanceFalloffs_buffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    const float distance = surfaceToLightRay.o.w;
    const float lightRange = light.dir.w;
    const float distScale = distance / lightRange;
    if (distScale > 1.0f)
        return make_float3(0.0f, 0.0f, 0.0f);

    const float nDotL = surfaceToLightRay.d.w;
    const float falloff = SampleFalloff(falloffIndex, distScale, distanceFalloffs_buffer KERNEL_VALIDATOR_BUFFERS);

    return falloff * nDotL * light.col.xyz;
}

static float3 ShadeSpotLight(
    const LightBuffer        light,
    const ray                surfaceToLightConeRay,
    float3                   surfacePosition,
    __global const float* restrict angularFalloffLUT_buffer,
    __global const float* restrict distanceFalloffs_buffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    const float3 pointColor = ShadePointLight(light, surfaceToLightConeRay, light.dataUnion.spotLightData.LightFalloffIndex, distanceFalloffs_buffer KERNEL_VALIDATOR_BUFFERS);

    const float cosConeAng = light.dataUnion.spotLightData.cosineConeAngle;
    const float invCosConeAng = light.dataUnion.spotLightData.inverseCosineConeAngle;
    const float3 unJitteredDirToLight = normalize(light.pos.xyz - surfacePosition);
    const float dval = dot(unJitteredDirToLight, -light.dir.xyz);
    const float angScale = (dval - cosConeAng) / invCosConeAng;
    const float angFalloff = 1.0f - LookupAngularFalloffLUT(angScale, angularFalloffLUT_buffer KERNEL_VALIDATOR_BUFFERS);

    return pointColor * angFalloff;
}

static float3 ShadeLight(
    const LightBuffer        light,
    const ray                surfaceToLightRay,
    float3                   surfacePosition,
    __global const float* restrict angularFalloffLUT_buffer,
    __global const float* restrict distanceFalloffs_buffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    if (light.lightType == kLightSpot)
    {
        return ShadeSpotLight(light, surfaceToLightRay, surfacePosition, angularFalloffLUT_buffer, distanceFalloffs_buffer KERNEL_VALIDATOR_BUFFERS);
    }
    else if (light.lightType == kLightDirectional)
    {
        return ShadeDirectionalLight(light, surfaceToLightRay);
    }
    else if (light.lightType == kLightPoint)
    {
        return ShadePointLight(light, surfaceToLightRay, light.dataUnion.pointLightData.LightFalloffIndex, distanceFalloffs_buffer KERNEL_VALIDATOR_BUFFERS);
    }
    else if (light.lightType == kLightRectangle)
    {
        return ShadeRectangularAreaLight(light, surfaceToLightRay);
    }
    //else if (light.lightType == kLightDisc)
    {
        return ShadeDiscLight(light, surfaceToLightRay);
    }
}

#endif
