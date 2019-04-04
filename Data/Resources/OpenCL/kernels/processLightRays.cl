#include "commonCL.h"
#include "directLighting.h"

__kernel void processLightRays  (
    // Inputs
    /*00*/__global const ray*            restrict lightRaysBuffer,
    /*01*/__global const float4*         restrict positionsWSBuffer,
    /*02*/__global const LightBuffer*    restrict directLightsBuffer,
    /*03*/__global const LightSample*    restrict lightSamples,
    /*04*/__global const float4*         restrict lightOcclusionBuffer,
    /*05*/__global const float*          restrict angularFalloffLUT_buffer,
    /*06*/__global const float*          restrict distanceFalloffs_buffer,
    /*07*/__global const uint*           restrict lightRaysCountBuffer,
    /*08*/ int                              passIndex,
#ifdef PROBES
    // Outputs
    /*09*/int                           numProbes,
    /*10*/int                           totalSampleCount,
    /*11*/__global float4*               restrict inputLightIndices,
    /*12*/__global float4*               restrict outputProbeDirectSHData,
    /*13*/__global float4*               restrict outputProbeOcclusion
#else
    /*09*/__global const PackedNormalOctQuad* restrict planeNormalsWSBuffer,
    /*10*/float                                   pushOff,
    /*11*/int                                     lightmapMode,
    /*12*/int                                     superSamplingMultiplier,
    /*13*/ __global const unsigned char* restrict gbufferInstanceIdToReceiveShadowsBuffer,
    // Outputs
    /*14*/__global float4*              outputShadowmaskFromDirectBuffer,
    /*15*/__global float4*              outputDirectionalFromDirectBuffer,
    /*16*/__global float4*              outputDirectLightingBuffer
#endif
    KERNEL_VALIDATOR_BUFFERS_DEF
                                )
{
    uint idx = get_global_id(0);
    if (idx >= INDEX_SAFE(lightRaysCountBuffer, 0) || Ray_IsInactive(GET_PTR_SAFE(lightRaysBuffer, idx)))
        return;

    int texelOrProbeIdx = Ray_GetIndex(GET_PTR_SAFE(lightRaysBuffer, idx));
    LightSample lightSample = INDEX_SAFE(lightSamples, texelOrProbeIdx);
    LightBuffer light = INDEX_SAFE(directLightsBuffer, lightSample.lightIdx);

#ifndef PROBES
    int ssIdx = GetSuperSampledIndex(texelOrProbeIdx, passIndex, superSamplingMultiplier);
    const float4 positionAndGbufferInstanceId = INDEX_SAFE(positionsWSBuffer, ssIdx);
    const int gBufferInstanceId = (int)(floor(positionAndGbufferInstanceId.w));
    const float3 P = positionAndGbufferInstanceId.xyz;
    const float3 planeNormal = DecodeNormal(INDEX_SAFE(planeNormalsWSBuffer, ssIdx));
    const float3 position = P + planeNormal * pushOff;
#else
    int ssIdx = texelOrProbeIdx;
    float3 position = INDEX_SAFE(positionsWSBuffer, ssIdx).xyz;
#endif

    bool useShadows = light.castShadow;
#ifndef PROBES
    useShadows &= INDEX_SAFE(gbufferInstanceIdToReceiveShadowsBuffer, gBufferInstanceId);
#endif
    float4 occlusions4 = useShadows ? INDEX_SAFE(lightOcclusionBuffer, idx) : make_float4(1.0f, 1.0f, 1.0f, 1.0f);
    const bool hit = occlusions4.w < TRANSMISSION_THRESHOLD;
    if(!hit)
    {
#ifdef PROBES
        const float weight = 1.0 / totalSampleCount;
        if (light.directBakeMode >= kDirectBakeMode_Subtractive)
        {
            int lightIdx = light.probeOcclusionLightIndex;
            const float4 lightIndicesFloat = INDEX_SAFE(inputLightIndices, texelOrProbeIdx);
            int4 lightIndices = (int4)((int)(lightIndicesFloat.x), (int)(lightIndicesFloat.y), (int)(lightIndicesFloat.z), (int)(lightIndicesFloat.w));
            float4 channelSelector = (float4)((lightIndices.x == lightIdx) ? 1.0f : 0.0f, (lightIndices.y == lightIdx) ? 1.0f : 0.0f, (lightIndices.z == lightIdx) ? 1.0f : 0.0f, (lightIndices.w == lightIdx) ? 1.0f : 0.0f);
            INDEX_SAFE(outputProbeOcclusion, texelOrProbeIdx) += channelSelector * weight;
        }
        else if (light.directBakeMode != kDirectBakeMode_None)
        {
            float4 D = (float4)(INDEX_SAFE(lightRaysBuffer, idx).d.x, INDEX_SAFE(lightRaysBuffer, idx).d.y, INDEX_SAFE(lightRaysBuffer, idx).d.z, 0);
            float3 L = ShadeLight(light, INDEX_SAFE(lightRaysBuffer, idx), position, angularFalloffLUT_buffer, distanceFalloffs_buffer KERNEL_VALIDATOR_BUFFERS);

            accumulateDirectSH(L, D, weight, outputProbeDirectSHData, texelOrProbeIdx, numProbes KERNEL_VALIDATOR_BUFFERS);
        }
#else
        if (light.directBakeMode >= kDirectBakeMode_OcclusionChannel0)
        {
            float4 channelSelector = (float4)(light.directBakeMode  == kDirectBakeMode_OcclusionChannel0 ? 1.0f : 0.0f, light.directBakeMode  == kDirectBakeMode_OcclusionChannel1 ? 1.0f : 0.0f, light.directBakeMode  == kDirectBakeMode_OcclusionChannel2 ? 1.0f : 0.0f, light.directBakeMode == kDirectBakeMode_OcclusionChannel3 ? 1.0f : 0.0f);
            INDEX_SAFE(outputShadowmaskFromDirectBuffer, texelOrProbeIdx) += occlusions4.w * channelSelector;
        }
        else if (light.directBakeMode != kDirectBakeMode_None)
        {
            INDEX_SAFE(outputDirectLightingBuffer, texelOrProbeIdx).xyz += occlusions4.xyz * ShadeLight(light, INDEX_SAFE(lightRaysBuffer, idx), position, angularFalloffLUT_buffer, distanceFalloffs_buffer KERNEL_VALIDATOR_BUFFERS);

            //compute directionality from direct lighting
            if (lightmapMode == LIGHTMAPMODE_DIRECTIONAL)
            {
                float lum = Luminance(INDEX_SAFE(outputDirectLightingBuffer, texelOrProbeIdx).xyz);

                INDEX_SAFE(outputDirectionalFromDirectBuffer, texelOrProbeIdx).xyz += INDEX_SAFE(lightRaysBuffer, idx).d.xyz * lum;
                INDEX_SAFE(outputDirectionalFromDirectBuffer, texelOrProbeIdx).w += lum;
            }
        }
#endif
    }
}
