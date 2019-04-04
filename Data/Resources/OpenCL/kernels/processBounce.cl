#include "commonCL.h"
#include "colorSpace.h"
#include "directLighting.h"
#include "emissiveLighting.h"

__constant sampler_t linear2DSampler = CLK_NORMALIZED_COORDS_TRUE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_LINEAR;

static void AccumulateLightFromBounce(float3 albedo, float3 directLightingAtHit, int idx, __global float4* outputIndirectLightingBuffer, int lightmapMode,
    __global float4* outputDirectionalFromGiBuffer, float3 direction KERNEL_VALIDATOR_BUFFERS_DEF)
{
    //Purely diffuse surface reflect the unabsorbed light evenly on the hemisphere.
    float3 energyFromHit = albedo * directLightingAtHit;
    INDEX_SAFE(outputIndirectLightingBuffer, idx).xyz += energyFromHit;

    //compute directionality from indirect
    if (lightmapMode == LIGHTMAPMODE_DIRECTIONAL)
    {
        float lum = Luminance(energyFromHit);

        INDEX_SAFE(outputDirectionalFromGiBuffer, idx).xyz += direction * lum;
        INDEX_SAFE(outputDirectionalFromGiBuffer, idx).w += lum;
    }
}

__kernel void processLightRaysFromBounce(
    //*** input ***
    //lighting
    /*00*/ __global LightBuffer*    const   indirectLightsBuffer,
    /*01*/ __global LightSample*    const   lightSamples,
    /*02*/__global uint*                    usePowerSamplingBuffer,
    /*03*/ __global float*          const   angularFalloffLUT_buffer,
    /*04*/ __global float* restrict const   distanceFalloffs_buffer,
    //ray
    /*05*/ __global ray*            const   pathRaysBuffer_0,
    /*06*/ __global Intersection*   const   pathIntersectionsBuffer,
    /*07*/ __global ray*            const   lightRaysBuffer,
    /*08*/ __global float4*         const   lightOcclusionBuffer,
    /*09*/ __global float4*         const   pathThoughputBuffer,
    /*10*/ __global uint*           const   indexRemapBuffer,
    /*11*/ __global uint*           const   lightRaysCountBuffer,
#ifdef PROBES
    /*12*/ int                              numProbes,
    /*13*/ int                              totalSampleCount,
    /*14*/ __global float4*         const   originalRaysBuffer,
    /*15*/ __global float4*                 outputProbeIndirectSHData
#else
    //directional lightmap
    /*12*/          int                     lightmapMode,
    //*** output ***
    /*13*/ __global float4*                 outputIndirectLightingBuffer,
    /*14*/ __global float4*                 outputDirectionalFromGiBuffer
#endif
    KERNEL_VALIDATOR_BUFFERS_DEF
    )
{
    uint idx = get_global_id(0);
    __local int numLightHitCountSharedMem;
    __local int numLightRayCountSharedMem;
    if (get_local_id(0) == 0)
    {
        numLightHitCountSharedMem = 0;
        numLightRayCountSharedMem = 0;
    }
    barrier(CLK_LOCAL_MEM_FENCE);

    if (idx < INDEX_SAFE(lightRaysCountBuffer, 0) && !Ray_IsInactive(GET_PTR_SAFE(lightRaysBuffer, idx)))
    {
        const int prev_idx = INDEX_SAFE(indexRemapBuffer, idx);

        const bool  hit = INDEX_SAFE(pathIntersectionsBuffer, prev_idx).shapeid > 0;

        const int texelOrProbeIdx = Ray_GetIndex(GET_PTR_SAFE(lightRaysBuffer, idx));
        LightSample lightSample = INDEX_SAFE(lightSamples, texelOrProbeIdx);
        LightBuffer light = INDEX_SAFE(indirectLightsBuffer, lightSample.lightIdx);

        bool useShadows = light.castShadow;
        const float4 occlusions4 = useShadows ? INDEX_SAFE(lightOcclusionBuffer, idx) : make_float4(1.0f, 1.0f, 1.0f, 1.0f);
        const bool  isLightOccludedFromBounce = occlusions4.w < TRANSMISSION_THRESHOLD;

        if (hit && !isLightOccludedFromBounce)
        {
            const float t = INDEX_SAFE(pathIntersectionsBuffer, prev_idx).uvwt.w;
            //We need to compute direct lighting on the fly
            float3 surfacePosition = INDEX_SAFE(pathRaysBuffer_0, prev_idx).o.xyz + INDEX_SAFE(pathRaysBuffer_0, prev_idx).d.xyz * t;
            float3 albedoAttenuation = INDEX_SAFE(pathThoughputBuffer, texelOrProbeIdx).xyz;

            float3 directLightingAtHit = occlusions4.xyz * ShadeLight(light, INDEX_SAFE(lightRaysBuffer, idx), surfacePosition, angularFalloffLUT_buffer, distanceFalloffs_buffer KERNEL_VALIDATOR_BUFFERS) / lightSample.lightPdf;
#ifdef PROBES
            float3 L = albedoAttenuation * directLightingAtHit;

            // The original direction from which the rays was shot from the probe position
            float4 originalRayDirection = INDEX_SAFE(originalRaysBuffer, texelOrProbeIdx);
            float weight = 4.0 / totalSampleCount;

            accumulateIndirectSH(L, originalRayDirection, weight, outputProbeIndirectSHData, texelOrProbeIdx, numProbes KERNEL_VALIDATOR_BUFFERS);
#else
            AccumulateLightFromBounce(albedoAttenuation, directLightingAtHit, texelOrProbeIdx, outputIndirectLightingBuffer, lightmapMode, outputDirectionalFromGiBuffer, -INDEX_SAFE(lightRaysBuffer, idx).d.xyz KERNEL_VALIDATOR_BUFFERS);
#endif
            atomic_inc(&numLightHitCountSharedMem);
        }
        atomic_inc(&numLightRayCountSharedMem);
    }

    // Collect stats to disable power sampling in pathological case.
    barrier(CLK_LOCAL_MEM_FENCE);
    if (get_local_id(0) == 0)
    {
        atomic_add(GET_PTR_SAFE(usePowerSamplingBuffer, UsePowerSamplingBufferSlot_LightHitCount), numLightHitCountSharedMem);
        atomic_add(GET_PTR_SAFE(usePowerSamplingBuffer, UsePowerSamplingBufferSlot_LightRayCount), numLightRayCountSharedMem);
    }
}

__kernel void updatePowerSamplingBuffer(
    /*00*/ int                              resetPowerSamplingBuffer,
    /*01*/__global uint*                    usePowerSamplingBuffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    if (resetPowerSamplingBuffer)
    {
        //Reset counter and re-enable power sampling
        INDEX_SAFE(usePowerSamplingBuffer, UsePowerSamplingBufferSlot_LightHitCount) = 0;
        INDEX_SAFE(usePowerSamplingBuffer, UsePowerSamplingBufferSlot_LightRayCount) = 0;
        INDEX_SAFE(usePowerSamplingBuffer, UsePowerSamplingBufferSlot_PowerSampleEnabled) = 0xFFFFFFFF;
        return;
    }

    const float kPowerSamplingMinimumRatio = 0.2f;
    const int   kPowerSamplingMinimumRaysCountBeforeDisabling = 100;

    uint totalLightHitCount = INDEX_SAFE(usePowerSamplingBuffer, UsePowerSamplingBufferSlot_LightHitCount);
    uint totalLightRayCount = INDEX_SAFE(usePowerSamplingBuffer, UsePowerSamplingBufferSlot_LightRayCount);
    if (totalLightRayCount > kPowerSamplingMinimumRaysCountBeforeDisabling)
    {
        float ratio = (float)totalLightHitCount / (float)totalLightRayCount;
        if (ratio < kPowerSamplingMinimumRatio)
        {
            //Disable power sampling
            INDEX_SAFE(usePowerSamplingBuffer, UsePowerSamplingBufferSlot_PowerSampleEnabled) = 0;
        }
    }
}

__kernel void processEmissiveFromBounce(
    //input
    /*00*/ __global ray*            const   pathRaysBuffer_0,
    /*01*/ __global Intersection*   const   pathIntersectionsBuffer,
    /*02*/ __global MaterialTextureProperties* const instanceIdToEmissiveTextureProperties,
    /*03*/ __global float2*         const   geometryUV1sBuffer,
    /*04*/ __global float4*         const   emissiveTextures_buffer,
    /*05*/ __global MeshDataOffsets* const  instanceIdToMeshDataOffsets,
    /*06*/ __global uint*           const   geometryIndicesBuffer,
    /*07*/ __global float4*         const   pathThoughputBuffer,
    /*08*/ __global uint*           const   activePathCountBuffer_0,
    /*09*/ __global const unsigned char* restrict pathLastNormalFacingTheRayBuffer,
#ifdef PROBES
    /*10*/ int                              numProbes,
    /*11*/ int                              totalSampleCount,
    /*12*/ __global float4*         const   originalRaysBuffer,
    //output
    /*13*/ __global float4*                 outputProbeIndirectSHData
#else
    /*10*/          int                     lightmapMode,
    //output
    /*11*/ __global float4*                 outputIndirectLightingBuffer,
    /*12*/ __global float4*                 outputDirectionalFromGiBuffer
#endif
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    uint idx = get_global_id(0);

    if (idx >= INDEX_SAFE(activePathCountBuffer_0, 0) || Ray_IsInactive(GET_PTR_SAFE(pathRaysBuffer_0, idx)))
        return;

    const int texelOrProbeIdx = Ray_GetIndex(GET_PTR_SAFE(pathRaysBuffer_0, idx));
    const bool  hit = INDEX_SAFE(pathIntersectionsBuffer, idx).shapeid > 0;
    if (hit)
    {
        AtlasInfo emissiveContribution = FetchEmissionFromRayIntersection(idx,
            pathIntersectionsBuffer,
            instanceIdToEmissiveTextureProperties,
            instanceIdToMeshDataOffsets,
            geometryUV1sBuffer,
            geometryIndicesBuffer,
            emissiveTextures_buffer
            KERNEL_VALIDATOR_BUFFERS
        );

        // If hit an invalid triangle (from the back, no double sided GI) we do not apply emissive.
        const unsigned char isNormalFacingTheRay = INDEX_SAFE(pathLastNormalFacingTheRayBuffer, idx);

#ifdef PROBES
        float3 L = emissiveContribution.color.xyz * INDEX_SAFE(pathThoughputBuffer, texelOrProbeIdx).xyz;

        // the original direction from which the rays was shot from the probe position
        float4 originalRayDirection = INDEX_SAFE(originalRaysBuffer, texelOrProbeIdx);
        float weight = 4.0 / totalSampleCount;

        accumulateIndirectSH(L, originalRayDirection, weight, outputProbeIndirectSHData, texelOrProbeIdx, numProbes KERNEL_VALIDATOR_BUFFERS);
#else
        float3 output = isNormalFacingTheRay * emissiveContribution.color.xyz * INDEX_SAFE(pathThoughputBuffer, texelOrProbeIdx).xyz;

        //compute directionality from indirect
        if (lightmapMode == LIGHTMAPMODE_DIRECTIONAL)
        {
            float lum = Luminance(output);

            // TODO(RadeonRays) Directionality will be wrong with more than one bounce because
            // raysFromLastToCurrentHit is the current path direction used to accumulate directionality
            // instead of the direction of the first section of the path.
            // This is a problem in many places (probes etc. look for //compute directionality from indirect)
            INDEX_SAFE(outputDirectionalFromGiBuffer, texelOrProbeIdx).xyz += INDEX_SAFE(pathRaysBuffer_0, idx).d.xyz * lum;
            INDEX_SAFE(outputDirectionalFromGiBuffer, texelOrProbeIdx).w += lum;
        }

        // Write Result
        INDEX_SAFE(outputIndirectLightingBuffer, texelOrProbeIdx).xyz += output.xyz;
#endif
    }
}

__kernel void advanceInPathAndAdjustPathProperties(
    //input
    /*00*/ __global ray*            const   pathRaysBuffer_0,
    /*01*/ __global Intersection*   const   pathIntersectionsBuffer,
    /*02*/ __global MaterialTextureProperties* const instanceIdToAlbedoTextureProperties,
    /*03*/ __global MeshDataOffsets* const  instanceIdToMeshDataOffsets,
    /*04*/ __global float2*         const   geometryUV1sBuffer,
    /*05*/ __global uint*           const   geometryIndicesBuffer,
    /*06*/ __global uchar4*         const   albedoTextures_buffer,
    /*07*/ __global uint*           const   activePathCountBuffer_0,
    //in/output
    /*08*/ __global float4*         const   pathThoughputBuffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    uint idx = get_global_id(0);

    if (idx >= INDEX_SAFE(activePathCountBuffer_0, 0) || Ray_IsInactive(GET_PTR_SAFE(pathRaysBuffer_0, idx)))
        return;

    const int texelIndex = Ray_GetIndex(GET_PTR_SAFE(pathRaysBuffer_0, idx));
    const bool  hit = INDEX_SAFE(pathIntersectionsBuffer, idx).shapeid > 0;
    if (!hit)
        return;

    AtlasInfo albedoAtHit = FetchAlbedoFromRayIntersection(idx,
        pathIntersectionsBuffer,
        instanceIdToAlbedoTextureProperties,
        instanceIdToMeshDataOffsets,
        geometryUV1sBuffer,
        geometryIndicesBuffer,
        albedoTextures_buffer
        KERNEL_VALIDATOR_BUFFERS);

    const float throughputAttenuation = dot(albedoAtHit.color.xyz, kAverageFactors);
    INDEX_SAFE(pathThoughputBuffer, texelIndex) *= (float4)(albedoAtHit.color.x, albedoAtHit.color.y, albedoAtHit.color.z, throughputAttenuation);
}

__kernel void getPlaneNormalFromLastBounceAndDoValidity(
    //input
    /*00*/ __global const ray* restrict              pathRaysBuffer_0,              // rays from last to current hit
    /*01*/ __global const Intersection* restrict     pathIntersectionsBuffer,       // intersections from last to current hit
    /*02*/ __global const MeshDataOffsets* restrict  instanceIdToMeshDataOffsets,
    /*03*/ __global const Matrix4x4* restrict        instanceIdToInvTransposedMatrices,
    /*04*/ __global const Vector3f_storage* restrict geometryPositionsBuffer,
    /*05*/ __global const PackedNormalOctQuad* restrict geometryNormalsBuffer,
    /*06*/ __global const uint* restrict             geometryIndicesBuffer,
    /*07*/ __global const uint* restrict             activePathCountBuffer,
    /*08*/ __global const MaterialTextureProperties* restrict instanceIdToTransmissionTextureProperties,
    /*09*/                int                        validitybufferMode,
    //output
    /*10*/ __global PackedNormalOctQuad* restrict    pathLastPlaneNormalBuffer,
    /*11*/ __global PackedNormalOctQuad* restrict    pathLastInterpNormalBuffer,
    /*12*/ __global unsigned char* const restrict    pathLastNormalFacingTheRayBuffer,
    /*13*/ __global float*  restrict                 outputValidityBuffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    uint idx = get_global_id(0);

    if (idx >= *activePathCountBuffer)
        return;

    if (Ray_IsInactive(GET_PTR_SAFE(pathRaysBuffer_0, idx)) || INDEX_SAFE(pathIntersectionsBuffer, idx).shapeid <= 0)
    {
        PackedNormalOctQuad zero;
        zero.x = 0;  zero.y = 0; zero.z = 0; // Will yield a decoded value of float3(0, 0, -1)
        INDEX_SAFE(pathLastPlaneNormalBuffer, idx) = zero;
        INDEX_SAFE(pathLastInterpNormalBuffer, idx) = zero;
        INDEX_SAFE(pathLastNormalFacingTheRayBuffer, idx) = 0;
        return;
    }

    const int instanceId = GetInstanceIdFromIntersection(GET_PTR_SAFE(pathIntersectionsBuffer, idx));
    float3 planeNormalWS;
    float3 interpVertexNormalWS;
    GetNormalsAtRayIntersection(idx,
        instanceId,
        pathIntersectionsBuffer,
        instanceIdToMeshDataOffsets,
        instanceIdToInvTransposedMatrices,
        geometryPositionsBuffer,
        geometryNormalsBuffer,
        geometryIndicesBuffer,
        &planeNormalWS,
        &interpVertexNormalWS
        KERNEL_VALIDATOR_BUFFERS);

    unsigned char isNormalFacingTheRay = 1;
    const bool frontFacing = dot(planeNormalWS, INDEX_SAFE(pathRaysBuffer_0, idx).d.xyz) <= 0.0f;
    if (!frontFacing)
    {
        const MaterialTextureProperties matProperty = INDEX_SAFE(instanceIdToTransmissionTextureProperties, instanceId);
        bool isDoubleSidedGI = GetMaterialProperty(matProperty, kMaterialInstanceProperties_DoubleSidedGI);
        planeNormalWS =        isDoubleSidedGI ? -planeNormalWS        : planeNormalWS;
        interpVertexNormalWS = isDoubleSidedGI ? -interpVertexNormalWS : interpVertexNormalWS;
        isNormalFacingTheRay = isDoubleSidedGI? 1 : 0;
        if (validitybufferMode == ValidityBufferMode_Generate && !isDoubleSidedGI)
        {
            const int texelIdx = Ray_GetIndex(GET_PTR_SAFE(pathRaysBuffer_0, idx));
            INDEX_SAFE(outputValidityBuffer, texelIdx) += 1.0f;
        }
    }

    // Store normals for various kernels to use later
    INDEX_SAFE(pathLastPlaneNormalBuffer, idx) = EncodeNormalTo888(planeNormalWS);
    INDEX_SAFE(pathLastInterpNormalBuffer, idx) = EncodeNormalTo888(interpVertexNormalWS);
    INDEX_SAFE(pathLastNormalFacingTheRayBuffer, idx) = isNormalFacingTheRay;
}
