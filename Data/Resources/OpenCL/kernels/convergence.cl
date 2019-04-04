#include "commonCL.h"

__constant ConvergenceOutputData g_clearedConvergenceOutputData = { 0, 0, 0, 0, 0, 0, 0, 0, INT_MAX, INT_MAX, INT_MIN, INT_MIN};

__kernel void clearConvergenceData(
    __global ConvergenceOutputData*  convergenceOutputDataBuffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    INDEX_SAFE(convergenceOutputDataBuffer, 0) = g_clearedConvergenceOutputData;
}

__kernel void calculateConvergenceMap(
    //*** input ***
    /*00*/ __global float4* const           positionsWSBuffer,
    /*01*/ __global unsigned char* const    cullingMapBuffer,
    /*02*/ __global float* const            indirectSamplesBuffer,
    /*03*/ __global float4* const           outputDirectLightingBuffer,
    /*04*/ const int                        maxDirectSamplesPerPixel,
    /*05*/ const int                        maxGISamplesPerPixel,
    /*06*/ __global unsigned char* const    occupancyBuffer,
    //*** output ***
    /*07*/ __global ConvergenceOutputData*  convergenceOutputDataBuffer //Should be cleared properly before kernel is running
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    __local ConvergenceOutputData dataShared;

    int idx = get_global_id(0);

    if (get_local_id(0) == 0)
        dataShared = g_clearedConvergenceOutputData;

    barrier(CLK_LOCAL_MEM_FENCE);

    const int occupiedSamplesWithinTexel = INDEX_SAFE(occupancyBuffer, idx);

    if (occupiedSamplesWithinTexel != 0)
    {
        atomic_inc(&(dataShared.occupiedTexelCount));

        const bool isTexelVisible = !IsCulled(INDEX_SAFE(cullingMapBuffer, idx));
        if (isTexelVisible)
            atomic_inc(&(dataShared.visibleTexelCount));

        const int directSampleCount = (int)(ceil(INDEX_SAFE(outputDirectLightingBuffer, idx).w));
        atomic_min(&(dataShared.minDirectSamples), directSampleCount);
        atomic_max(&(dataShared.maxDirectSamples), directSampleCount);
        atomic_add(&(dataShared.totalDirectSamples), directSampleCount);

        const int giSampleCount = (int)(ceil(INDEX_SAFE(indirectSamplesBuffer, idx)));
        atomic_min(&(dataShared.minGISamples), giSampleCount);
        atomic_max(&(dataShared.maxGISamples), giSampleCount);
        atomic_add(&(dataShared.totalGISamples), giSampleCount);

        if (IsGIConverged(giSampleCount, maxGISamplesPerPixel))
        {
            atomic_inc(&(dataShared.convergedGITexelCount));

            if (isTexelVisible)
                atomic_inc(&(dataShared.visibleConvergedGITexelCount));
        }

        if (IsDirectConverged(directSampleCount, maxDirectSamplesPerPixel))
        {
            atomic_inc(&(dataShared.convergedDirectTexelCount));

            if (isTexelVisible)
                atomic_inc(&(dataShared.visibleConvergedDirectTexelCount));
        }
    }

    barrier(CLK_LOCAL_MEM_FENCE);
    if (get_local_id(0) == 0)
    {
        atomic_add(&(INDEX_SAFE(convergenceOutputDataBuffer, 0).occupiedTexelCount), dataShared.occupiedTexelCount);
        atomic_add(&(INDEX_SAFE(convergenceOutputDataBuffer, 0).visibleTexelCount), dataShared.visibleTexelCount);
        atomic_min(&(INDEX_SAFE(convergenceOutputDataBuffer, 0).minDirectSamples), dataShared.minDirectSamples);
        atomic_max(&(INDEX_SAFE(convergenceOutputDataBuffer, 0).maxDirectSamples), dataShared.maxDirectSamples);
        atomic_add(&(INDEX_SAFE(convergenceOutputDataBuffer, 0).totalDirectSamples), dataShared.totalDirectSamples);
        atomic_min(&(INDEX_SAFE(convergenceOutputDataBuffer, 0).minGISamples), dataShared.minGISamples);
        atomic_max(&(INDEX_SAFE(convergenceOutputDataBuffer, 0).maxGISamples), dataShared.maxGISamples);
        atomic_add(&(INDEX_SAFE(convergenceOutputDataBuffer, 0).totalGISamples), dataShared.totalGISamples);
        atomic_add(&(INDEX_SAFE(convergenceOutputDataBuffer, 0).convergedGITexelCount), dataShared.convergedGITexelCount);
        atomic_add(&(INDEX_SAFE(convergenceOutputDataBuffer, 0).visibleConvergedGITexelCount), dataShared.visibleConvergedGITexelCount);
        atomic_add(&(INDEX_SAFE(convergenceOutputDataBuffer, 0).convergedDirectTexelCount), dataShared.convergedDirectTexelCount);
        atomic_add(&(INDEX_SAFE(convergenceOutputDataBuffer, 0).visibleConvergedDirectTexelCount), dataShared.visibleConvergedDirectTexelCount);
    }
}
