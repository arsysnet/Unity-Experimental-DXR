#include "commonCL.h"

__kernel void clearPathThoughputBuffer(
    /*00*/ __global float4*         const   pathThoughputBuffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    int idx = get_global_id(0);
    INDEX_SAFE(pathThoughputBuffer, idx) = make_float4(1.0f, 1.0f, 1.0f, 1.0f);
}

// Used for multiple global count buffers, so can't use INDEX_SAFE. Buffer size equal to 1 checked before kernel invocation though.
__kernel void clearRayCount(
    /*00*/ __global uint*                   dynarg_countBuffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    INDEX_SAFE(dynarg_countBuffer, 0) = 0;
}

__kernel void preparePathRays(
    //*** output ***
    /*00*/ __global ray*                       pathRaysBuffer_0,
    /*01*/ __global float*                     indirectSamplesBuffer,
    //*** input ***
    /*02*/ __global float4*            const   positionsWSBuffer,
    /*03*/ int                                 maxGISampleCount,
    /*04*/ int                                 numTexelsOrProbes,
    /*05*/ int                                 frame,
    /*06*/ int                                 bounce,
    /*07*/ __global uint* restrict             random_buffer,
    /*08*/ __global uint                const* sobol_buffer,
    /*09*/ __global float*                     goldenSample_buffer,
    /*10*/ int                                 numGoldenSample,
    //ray statistics
    /*11*/ __global uint*                      totalRayCastBuffer,
    /*12*/ __global uint*                      activePathCountBuffer_0,
#ifndef PROBES
    /*13*/ __global PackedNormalOctQuad* const interpNormalsWSBuffer,
    /*14*/ __global PackedNormalOctQuad* const planeNormalsWSBuffer,
    /*15*/ __global unsigned char*     const   cullingMapBuffer,
    /*16*/ int                                 shouldUseCullingMap,
    /*17*/ float                               pushOff,
    /*18*/ int                                 superSamplingMultiplier,
    /*19*/ __global const unsigned char* restrict occupancyBuffer
#else
    /*13*/ __global float4*                    originalRaysBuffer
#endif
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    ray r;  // prepare ray in private memory
#if DISALLOW_PATHRAYS_COMPACTION
    Ray_SetInactive(&r);
#endif

    int idx = get_global_id(0), local_idx;

    __local uint numRayPreparedSharedMem;
    if (get_local_id(0) == 0)
        numRayPreparedSharedMem = 0;
    barrier(CLK_LOCAL_MEM_FENCE);

    bool shouldPrepareNewRay = true;

#ifndef PROBES
    const int occupiedSamplesWithinTexel = INDEX_SAFE(occupancyBuffer, idx);
    if (occupiedSamplesWithinTexel == 0)
        shouldPrepareNewRay = false;

    if (shouldUseCullingMap && shouldPrepareNewRay && IsCulled(INDEX_SAFE(cullingMapBuffer, idx)))
        shouldPrepareNewRay = false;
#endif

    int currentGISampleCount = 0;
    if (shouldPrepareNewRay)
    {
        //TODO(RadeonRays) use giSamplesSoFar instead of frame. Will not work in view prio.
        currentGISampleCount = INDEX_SAFE(indirectSamplesBuffer, idx);
        if (IsGIConverged(currentGISampleCount, maxGISampleCount))
            shouldPrepareNewRay = false;
    }

    if (shouldPrepareNewRay)
    {
#ifndef PROBES
        int ssIdx = GetSuperSampledIndex(idx, frame, superSamplingMultiplier);
#else
        int ssIdx = idx;
#endif
        float4 position = INDEX_SAFE(positionsWSBuffer, ssIdx);
        AssertPositionIsOccupied(position KERNEL_VALIDATOR_BUFFERS);

        INDEX_SAFE(indirectSamplesBuffer, idx) = currentGISampleCount + 1;

        //Random numbers
        int dimensionOffset = UNITY_SAMPLE_DIM_SURFACE_OFFSET + bounce * UNITY_SAMPLE_DIMS_PER_BOUNCE;
        uint scramble = GetScramble(idx, frame, numTexelsOrProbes, random_buffer KERNEL_VALIDATOR_BUFFERS);
        float2 sample2D = GetRandomSample2D(frame, dimensionOffset, scramble, sobol_buffer);

#ifdef PROBES
        float3 D = Sample_MapToSphere(sample2D);
        const float3 P = position.xyz;
#else
        const float3 interpNormal = DecodeNormal(INDEX_SAFE(interpNormalsWSBuffer, ssIdx));
        //Map to hemisphere directed toward normal
        float3 D = GetRandomDirectionOnHemisphere(sample2D, scramble, interpNormal, numGoldenSample, goldenSample_buffer);
        const float3 planeNormal = DecodeNormal(INDEX_SAFE(planeNormalsWSBuffer, ssIdx));
        const float3 P = position.xyz + planeNormal * pushOff;

        // if plane normal is too different from interpolated normal, the hemisphere orientation will be wrong and the sample could be under the surface.
        float dotVal = dot(D, planeNormal);
        if (dotVal <= 0.0 || isnan(dotVal))
        {
            shouldPrepareNewRay = false;
        }
        else
#endif
        {
            const float kMaxt = 1000000.0f;
            Ray_Init(&r, P, D, kMaxt, 0.f, 0xFFFFFFFF);
            Ray_SetIndex(&r, idx);

            local_idx = atomic_inc(&numRayPreparedSharedMem);
        }
    }

    barrier(CLK_LOCAL_MEM_FENCE);
    if (get_local_id(0) == 0)
    {
        atomic_add(GET_PTR_SAFE(totalRayCastBuffer, 0), numRayPreparedSharedMem);
        int numRayToAdd = numRayPreparedSharedMem;
#if DISALLOW_PATHRAYS_COMPACTION
        numRayToAdd = get_local_size(0);
#endif
        numRayPreparedSharedMem = atomic_add(GET_PTR_SAFE(activePathCountBuffer_0, 0), numRayToAdd);
    }
    barrier(CLK_LOCAL_MEM_FENCE);

#if DISALLOW_PATHRAYS_COMPACTION
    INDEX_SAFE(pathRaysBuffer_0, idx) = r;
#else
    // Write the ray out to memory
    if (shouldPrepareNewRay)
    {
#ifdef PROBES
        INDEX_SAFE(originalRaysBuffer, idx) = (float4)(r.d.x, r.d.y, r.d.z, 0);
#endif
        INDEX_SAFE(pathRaysBuffer_0, numRayPreparedSharedMem + local_idx) = r;
    }
#endif
}

__kernel void preparePathRaysFromBounce(
    //*** input ***
    /*00*/ __global const ray* restrict                 pathRaysBuffer_0,
    /*01*/ __global const Intersection* restrict        pathIntersectionsBuffer,
    /*02*/ __global const uint* restrict                activePathCountBuffer_0,
    /*03*/ __global const PackedNormalOctQuad* restrict pathLastPlaneNormalBuffer,
    /*04*/ __global const unsigned char* restrict       pathLastNormalFacingTheRayBuffer,
    //randomization
    /*05*/ int                                 lightmapSize,
    /*06*/ int                                 frame,
    /*07*/ int                                 bounce,
    /*08*/ __global const uint* restrict       random_buffer,
    /*09*/ __global const uint* restrict       sobol_buffer,
    /*10*/ __global const float* restrict      goldenSample_buffer,
    /*11*/ int                                 numGoldenSample,
    /*12*/ float                               pushOff,
    //*** output ***
    /*13*/ __global ray* restrict              pathRaysBuffer_1,
    /*14*/ __global uint* restrict             totalRayCastBuffer,
    /*15*/ __global uint* restrict             activePathCountBuffer_1,
    /*16*/ __global uint* restrict             indexRemapBuffer,
    //*** in/output ***
    /*17*/ __global float4* restrict           pathThoughputBuffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    ray r;// Prepare ray in private memory
#if DISALLOW_PATHRAYS_COMPACTION
    Ray_SetInactive(&r);
#endif
    uint idx = get_global_id(0), local_idx;
    int texelIdx = Ray_GetIndex(GET_PTR_SAFE(pathRaysBuffer_0, idx));

    __local uint numRayPreparedSharedMem;
    if (get_local_id(0) == 0)
        numRayPreparedSharedMem = 0;
    barrier(CLK_LOCAL_MEM_FENCE);

    bool shouldPrepareNewRay = idx < INDEX_SAFE(activePathCountBuffer_0, 0) && !Ray_IsInactive(GET_PTR_SAFE(pathRaysBuffer_0, idx));

    const bool hit = INDEX_SAFE(pathIntersectionsBuffer, idx).shapeid > 0;
    if (!hit)
        shouldPrepareNewRay = false;

    int dimensionOffset;
    uint scramble;

    const bool doRussianRoulette = (bounce >= 1) && shouldPrepareNewRay;
    if (doRussianRoulette || shouldPrepareNewRay)
    {
        dimensionOffset = UNITY_SAMPLE_DIM_SURFACE_OFFSET + bounce * UNITY_SAMPLE_DIMS_PER_BOUNCE;
        scramble = GetScramble(texelIdx, frame, lightmapSize, random_buffer KERNEL_VALIDATOR_BUFFERS);
    }

    if (doRussianRoulette)
    {
        float4 pathThroughput = INDEX_SAFE(pathThoughputBuffer, texelIdx);
        float p = max(max(pathThroughput.x, pathThroughput.y), pathThroughput.z);
        float rand = GetRandomSample1D(frame, dimensionOffset++, scramble, sobol_buffer);

        if (p < rand)
            shouldPrepareNewRay = false;
        else
            INDEX_SAFE(pathThoughputBuffer, texelIdx).xyz *= (1 / p);
    }

    // We hit an invalid triangle (from the back, no double sided GI), stop the path.
    const unsigned char isNormalFacingTheRay = INDEX_SAFE(pathLastNormalFacingTheRayBuffer, idx);
    shouldPrepareNewRay = (shouldPrepareNewRay && isNormalFacingTheRay);

    if (shouldPrepareNewRay)
    {
        const float3 planeNormal = DecodeNormal(INDEX_SAFE(pathLastPlaneNormalBuffer, idx));
        const float t = INDEX_SAFE(pathIntersectionsBuffer, idx).uvwt.w;
        float3 position = INDEX_SAFE(pathRaysBuffer_0,idx).o.xyz + INDEX_SAFE(pathRaysBuffer_0, idx).d.xyz * t;

        const float kMaxt = 1000000.0f;

        // Random numbers
        float2 sample2D = GetRandomSample2D(frame, dimensionOffset, scramble, sobol_buffer);

        // Map to hemisphere directed toward plane normal
        float3 D = GetRandomDirectionOnHemisphere(sample2D, scramble, planeNormal, numGoldenSample, goldenSample_buffer);

        if (any(isnan(D)))  // TODO(RadeonRays) gboisse: we're generating some NaN directions somehow, fix it!!
            shouldPrepareNewRay = false;
        else
        {
            const float3 P = position.xyz + planeNormal * pushOff;
            Ray_Init(&r, P, D, kMaxt, 0.f, 0xFFFFFFFF);
            Ray_SetIndex(&r, texelIdx);

            local_idx = atomic_inc(&numRayPreparedSharedMem);
        }
    }

    barrier(CLK_LOCAL_MEM_FENCE);
    if (get_local_id(0) == 0)
    {
        atomic_add(GET_PTR_SAFE(totalRayCastBuffer, 0), numRayPreparedSharedMem);
        int numRayToAdd = numRayPreparedSharedMem;
#if DISALLOW_PATHRAYS_COMPACTION
        numRayToAdd = get_local_size(0);
#endif
        numRayPreparedSharedMem = atomic_add(GET_PTR_SAFE(activePathCountBuffer_1, 0), numRayToAdd);
    }
    barrier(CLK_LOCAL_MEM_FENCE);

#if DISALLOW_PATHRAYS_COMPACTION
    INDEX_SAFE(pathRaysBuffer_1, idx) = r;
#else
    // Write the ray out to memory
    if (shouldPrepareNewRay)
    {
        //TODO(RadeonRays) We could store the idx of the previous ray in the ray itself (in the padding), this would optimize and save memory.
        INDEX_SAFE(pathRaysBuffer_1, numRayPreparedSharedMem + local_idx) = r;
        INDEX_SAFE(indexRemapBuffer, numRayPreparedSharedMem + local_idx) = idx;
    }
#endif
}
