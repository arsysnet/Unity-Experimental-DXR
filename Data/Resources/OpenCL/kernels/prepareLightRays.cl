#include "commonCL.h"
#include "directLighting.h"

static int GetCellIndex(float3 position, float3 gridBias, float3 gridScale, int3 gridDims)
{
    const int3 cellPos = clamp(convert_int3(position * gridScale + gridBias), (int3)0, gridDims - 1);
    return cellPos.x + cellPos.y * gridDims.x + cellPos.z * gridDims.x * gridDims.y;
}

//Preparing shadowRays for direct lighting.
__kernel void prepareLightRays(
    /*00*/ __global ray*                lightRaysBuffer,
    /*01*/ __global LightSample*        lightSamples,
    /*02*/ __global float4* const       positionsWSBuffer,
    /*03*/ __global LightBuffer* const  directLightsBuffer,
    /*04*/ __global int* const          directLightsOffsetBuffer,
    /*05*/ __global int* const          directLightsCountPerCellBuffer,
    /*06*/ const float3                 lightGridBias,
    /*07*/ const float3                 lightGridScale,
    /*08*/ const int3                   lightGridDims,
#ifdef PROBES
    /*09*/ __global float*              directSamplesBuffer,
#else
    /*09*/ __global float4*             outputDirectLightingBuffer,
#endif
    /*10*/ int                          maxDirectSampleCount,
    /*11*/ int                          numTexelsOrProbes,
    /*12*/ const int                    directPassIndex,
    /*13*/ __global uint* restrict      random_buffer,
    /*14*/ __global uint    const*      sobol_buffer,
    /*15*/ __global uint*               totalRayCastBuffer,
    /*16*/ __global uint*               lightRaysCountBuffer,
    /*17*/ const int                    lightIndexInCell
#ifndef PROBES
    ,
    /*18*/ __global PackedNormalOctQuad* const interpNormalsWSBuffer,
    /*19*/ __global unsigned char* const cullingMapBuffer,
    /*20*/ __global const unsigned char* restrict occupancyBuffer,
    /*21*/ int                          shouldUseCullingMap,
    /*22*/ float                        pushOff,
    /*23*/ int                          superSamplingMultiplier
#endif
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    // Initialize local memory
    __local int numRayPreparedSharedMem;
    if (get_local_id(0) == 0)
        numRayPreparedSharedMem = 0;
    barrier(CLK_LOCAL_MEM_FENCE);

    // Prepare ray in private memory
    ray r;
    int idx = get_global_id(0), local_idx;

    // Should we prepare a new ray?
    bool shouldPrepareNewRay = true;

#ifndef PROBES
    const int occupiedSamplesWithinTexel = INDEX_SAFE(occupancyBuffer, idx);
    if (occupiedSamplesWithinTexel == 0)
        shouldPrepareNewRay = false;

    if (shouldUseCullingMap && shouldPrepareNewRay && IsCulled(INDEX_SAFE(cullingMapBuffer, idx)))
        shouldPrepareNewRay = false;
#endif

    if (shouldPrepareNewRay)
    {
#ifdef PROBES
        const int currentDirectSampleCount = INDEX_SAFE(directSamplesBuffer, idx);
#else
        const int currentDirectSampleCount = INDEX_SAFE(outputDirectLightingBuffer, idx).w;
#endif
        if (IsDirectConverged(currentDirectSampleCount, maxDirectSampleCount))
            shouldPrepareNewRay = false;
    }

    // Prepare the shadow ray
    if (shouldPrepareNewRay)
    {
#ifndef PROBES
        int ssIdx = GetSuperSampledIndex(idx, directPassIndex, superSamplingMultiplier);
#else
        int ssIdx = idx;
#endif
        float4 position = INDEX_SAFE(positionsWSBuffer, ssIdx);
        AssertPositionIsOccupied(position KERNEL_VALIDATOR_BUFFERS);

        const int cellIdx = GetCellIndex(position.xyz, lightGridBias, lightGridScale, lightGridDims);
        const int lightCountInCell = INDEX_SAFE(directLightsCountPerCellBuffer, cellIdx);

        // If we already did all the lights in the cell bail out
        if (lightIndexInCell >= lightCountInCell)
            shouldPrepareNewRay = false;
        else
        {
            // Select a light in a round robin fashion (no need for pdf)
            __global LightSample *lightSample = &INDEX_SAFE(lightSamples, idx);
            lightSample->lightIdx = INDEX_SAFE(directLightsOffsetBuffer, cellIdx) + lightIndexInCell;

            // Generate the shadow ray.
            const LightBuffer light = INDEX_SAFE(directLightsBuffer, lightSample->lightIdx);

            // Initialize sampler state
            uint scramble = GetScramble(idx, directPassIndex, numTexelsOrProbes, random_buffer KERNEL_VALIDATOR_BUFFERS);
            float2 sample2D = GetRandomSample2D(directPassIndex, UNITY_SAMPLE_DIM_CAMERA_OFFSET + lightIndexInCell, scramble, sobol_buffer);
#ifdef PROBES
            float3 notUsed3 = (float3)(0, 0, 0);
            PrepareShadowRay(light, sample2D, position.xyz, notUsed3, 0, idx, false, &r);
#else
            float3 normal = DecodeNormal(INDEX_SAFE(interpNormalsWSBuffer, ssIdx));
            PrepareShadowRay(light, sample2D, position.xyz, normal, pushOff, idx, false,&r);
#endif

            // Update local counter
            if (Ray_IsInactive_Private(&r))
                shouldPrepareNewRay = false;
            else
                local_idx = atomic_inc(&numRayPreparedSharedMem);
        }

#ifdef PROBES
        INDEX_SAFE(directSamplesBuffer, idx)+= (lightIndexInCell == 0) ? 1.0f : 0.0f;
#else
        INDEX_SAFE(outputDirectLightingBuffer, idx).w += (lightIndexInCell == 0) ? 1.0f : 0.0f;
#endif
    }

    // Compute write offset
    barrier(CLK_LOCAL_MEM_FENCE);
    if (get_local_id(0) == 0)
    {
        atomic_add(GET_PTR_SAFE(totalRayCastBuffer, 0), numRayPreparedSharedMem);
        numRayPreparedSharedMem = atomic_add(GET_PTR_SAFE(lightRaysCountBuffer, 0), numRayPreparedSharedMem);
    }
    barrier(CLK_LOCAL_MEM_FENCE);

    // Write the ray out to memory
    if (shouldPrepareNewRay)
        INDEX_SAFE(lightRaysBuffer, numRayPreparedSharedMem + local_idx) = r;
}

//Preparing shadowRays for indirect lighting.
__kernel void prepareLightRaysFromBounce(
    //*** input ***
    /*00*/ __global LightBuffer*         const indirectLightsBuffer,
    /*01*/ __global int*                 const indirectLightsOffsetBuffer,
    /*02*/ __global int*                 const indirectLightsDistribution,
    /*03*/ __global int*                 const indirectLightDistributionOffsetBuffer,
    /*04*/ __global bool*                const usePowerSamplingBuffer,
    /*05*/ const float3                        lightGridBias,
    /*06*/ const float3                        lightGridScale,
    /*07*/ const int3                          lightGridDims,
    /*08*/ __global ray*                 const pathRaysBuffer_0,
    /*09*/ __global Intersection*        const pathIntersectionsBuffer,
    /*10*/ __global PackedNormalOctQuad* const pathLastInterpNormalBuffer,
    /*11*/ __global unsigned char* const restrict pathLastNormalFacingTheRayBuffer,
    /*12*/    const int                        lightmapSize,
    /*13*/    const int                        giPassIndex,
    /*14*/    const int                        bounce,
    /*15*/ __global uint* restrict             random_buffer,
    /*16*/ __global uint                const* sobol_buffer,
    /*17*/ float                               pushOff,
    /*18*/ __global uint*               const  activePathCountBuffer_0,
    //*** output ***
    /*19*/ __global ray*                       lightRaysBuffer,
    /*20*/ __global LightSample*               lightSamples,
    /*21*/ __global uint*                      indexRemapBuffer,
    /*22*/ __global uint*                      totalRayCastBuffer,
    /*23*/ __global uint*                      lightRaysCountBuffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    // Initialize local memory
    __local uint numRayPreparedSharedMem;
    if (get_local_id(0) == 0)
        numRayPreparedSharedMem = 0;
    barrier(CLK_LOCAL_MEM_FENCE);

    // Prepare ray in private memory
    ray r;
    uint idx = get_global_id(0), local_idx;

    // Should we prepare a new ray?
    bool shouldPrepareNewRay = idx < INDEX_SAFE(activePathCountBuffer_0, 0) && !Ray_IsInactive(GET_PTR_SAFE(pathRaysBuffer_0, idx));
    const bool  hitFromLastToCurrent = INDEX_SAFE(pathIntersectionsBuffer, idx).shapeid > 0;
    if (shouldPrepareNewRay && !hitFromLastToCurrent)
        shouldPrepareNewRay = false;

    // We hit an invalid triangle (from the back, no double sided GI), stop the path.
    const bool isNormalFacingTheRay = INDEX_SAFE(pathLastNormalFacingTheRayBuffer, idx);
    shouldPrepareNewRay = (shouldPrepareNewRay && isNormalFacingTheRay);

    // Prepare the shadow ray
    if (shouldPrepareNewRay)
    {
        const float3 surfaceNormal = DecodeNormal(INDEX_SAFE(pathLastInterpNormalBuffer, idx));
        const float t = INDEX_SAFE(pathIntersectionsBuffer, idx).uvwt.w;
        float3 surfacePosition = INDEX_SAFE(pathRaysBuffer_0, idx).o.xyz + t * INDEX_SAFE(pathRaysBuffer_0, idx).d.xyz;

        // Retrieve the light distribution at the shading site
        const int cellIdx = GetCellIndex(surfacePosition, lightGridBias, lightGridScale, lightGridDims);
        __global const int *lightDistributionPtr = GET_PTR_SAFE(indirectLightsDistribution, INDEX_SAFE(indirectLightDistributionOffsetBuffer, cellIdx));
        const int lightDistribution = *lightDistributionPtr; // safe to dereference, as GET_PTR_SAFE above does the validation

        // If there is no light in the cell, bail out
        if (!lightDistribution)
            shouldPrepareNewRay = false;
        else
        {
            int texelIdx = Ray_GetIndex(GET_PTR_SAFE(pathRaysBuffer_0, idx));

            // Initialize sampler state
            uint dimension = UNITY_SAMPLE_DIM_CAMERA_OFFSET + bounce * UNITY_SAMPLE_DIMS_PER_BOUNCE + UNITY_SAMPLE_DIM_SURFACE_OFFSET;
            uint scramble = GetScramble(texelIdx, giPassIndex, lightmapSize, random_buffer KERNEL_VALIDATOR_BUFFERS);

            // Select a light
            __global LightSample *lightSample = &INDEX_SAFE(lightSamples, texelIdx);
            float selectionPdf;
            float sample1D = GetRandomSample1D(giPassIndex, dimension++, scramble, sobol_buffer);
            if (INDEX_SAFE(usePowerSamplingBuffer, UsePowerSamplingBufferSlot_PowerSampleEnabled))
            {
                lightSample->lightIdx = INDEX_SAFE(indirectLightsOffsetBuffer, cellIdx) + Distribution1D_SampleDiscrete(sample1D, lightDistributionPtr, &selectionPdf);
                lightSample->lightPdf = selectionPdf;
            }
            else
            {
                const int offset = min(lightDistribution - 1, (int)(sample1D * (float)lightDistribution));
                lightSample->lightIdx = INDEX_SAFE(indirectLightsOffsetBuffer, cellIdx) + offset;
                lightSample->lightPdf = 1.0f / lightDistribution;
            }

            // Generate the shadow ray
            const LightBuffer light = INDEX_SAFE(indirectLightsBuffer, lightSample->lightIdx);
            float2 sample2D = GetRandomSample2D(giPassIndex, dimension++, scramble, sobol_buffer);
            PrepareShadowRay(light, sample2D, surfacePosition, surfaceNormal, pushOff, texelIdx, false, &r);

            // Update local counter
            if (Ray_IsInactive_Private(&r))
                shouldPrepareNewRay = false;
            else
                local_idx = atomic_inc(&numRayPreparedSharedMem);
        }
    }

    // Compute write offset
    barrier(CLK_LOCAL_MEM_FENCE);
    if (get_local_id(0) == 0)
    {
        atomic_add(GET_PTR_SAFE(totalRayCastBuffer, 0), numRayPreparedSharedMem);
        numRayPreparedSharedMem = atomic_add(GET_PTR_SAFE(lightRaysCountBuffer, 0), numRayPreparedSharedMem);
    }
    barrier(CLK_LOCAL_MEM_FENCE);

    // Write the ray out to memory
    if (shouldPrepareNewRay)
    {
        //TODO(RadeonRays) We could store the idx of the previous ray in the ray itself (in the padding), this would optimize and save memory.
        INDEX_SAFE(lightRaysBuffer, numRayPreparedSharedMem + local_idx) = r;
        INDEX_SAFE(indexRemapBuffer, numRayPreparedSharedMem + local_idx) = idx;
    }
}
