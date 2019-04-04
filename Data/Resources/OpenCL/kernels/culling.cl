#include "commonCL.h"

__kernel void clearLightmapCulling(
    __global unsigned char* cullingMapBuffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    int idx = get_global_id(0);
    INDEX_SAFE(cullingMapBuffer, idx) = 255;
}

__kernel void prepareLightmapCulling(
    //input
    __global unsigned char* const occupancyBuffer,
    __global float4* const  positionsWSBuffer,
    __global PackedNormalOctQuad* const interpNormalsWSBuffer,
    const Matrix4x4         worldToClip,
    const float4            cameraPosition,
    const int               superSamplingMultiplier,
    //output
    __global ray*           lightRaysBuffer,
    __global uint*          lightRaysCountBuffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    ray r;  // prepare ray in private memory
    int idx = get_global_id(0);

    __local int numRayPreparedSharedMem;
    if (get_local_id(0) == 0)
        numRayPreparedSharedMem = 0;
    barrier(CLK_LOCAL_MEM_FENCE);
    atomic_inc(&numRayPreparedSharedMem);

    //TODO(RadeonRays) on spot compaction (guillaume v1 style)

    const int occupiedSamplesWithinTexel = INDEX_SAFE(occupancyBuffer, idx);
    if (occupiedSamplesWithinTexel == 0) // Reject texels that are invalid.
    {
        Ray_SetInactive(&r);
    }
    else
    {
        // Just fetch one sample, we know the position is within an occupied texel.
        int ssIdx = GetSuperSampledIndex(idx, 0, superSamplingMultiplier);
        float4 position = INDEX_SAFE(positionsWSBuffer, ssIdx);

        //Clip space position
        float4 clipPos = transform_point(position.xyz, worldToClip);
        clipPos.xyz /= clipPos.w;

        //Camera to texel
        //float3 camToPos = (position.xyz - cameraPosition.xyz);

        //Normal
        float3 normal = CalculateSuperSampledInterpolatedNormal(idx, superSamplingMultiplier, interpNormalsWSBuffer KERNEL_VALIDATOR_BUFFERS);
        //float normalDotCamToPos = dot(normal, camToPos);

        //Is the texel visible?
        if (clipPos.x >= -1.0f && clipPos.x <= 1.0f &&
            clipPos.y >= -1.0f && clipPos.y <= 1.0f &&
            clipPos.z >= 0.0f && clipPos.z <= 1.0f)
            //TODO(RadeonRays) understand why this does not work.
            //&& normalDotCamToPos < 0.0f)
        {
            const float kMinPushOffDistance = 0.001f;
            float3 targetPos = position.xyz + normal * kMinPushOffDistance;
            float3 camToTarget = (targetPos - cameraPosition.xyz);
            float camToTargetDist = length(camToTarget);
            if (camToTargetDist > 0)
            {
                Ray_Init(&r, cameraPosition.xyz, camToTarget/ camToTargetDist, camToTargetDist, 0.f, 0xFFFFFFFF);
            }
            else
            {
                Ray_SetInactive(&r);
            }
        }
        else
        {
            Ray_SetInactive(&r);
        }
    }

    barrier(CLK_LOCAL_MEM_FENCE);
    if (get_local_id(0) == 0)
    {
        atomic_add(GET_PTR_SAFE(lightRaysCountBuffer, 0), numRayPreparedSharedMem);
    }
    INDEX_SAFE(lightRaysBuffer, idx) = r;
}

__kernel void processLightmapCulling(
    //input
    __global ray*     const lightRaysBuffer,
    __global float4*  const lightOcclusionBuffer,
    //output
    __global unsigned char* cullingMapBuffer,
    __global unsigned int*  visibleTexelCountBuffer //Need to have been cleared to 0 before the kernel is called.
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    __local int visibleTexelCountSharedMem;
    int idx = get_global_id(0);
    if (get_local_id(0) == 0)
        visibleTexelCountSharedMem = 0;
    barrier(CLK_LOCAL_MEM_FENCE);

    const bool rayActive = !Ray_IsInactive(GET_PTR_SAFE(lightRaysBuffer, idx));
    const bool hit = rayActive && INDEX_SAFE(lightOcclusionBuffer, idx).w < TRANSMISSION_THRESHOLD;
    const bool texelVisible = rayActive && !hit;

    if (texelVisible)
    {
        INDEX_SAFE(cullingMapBuffer, idx) = 255;
    }
    else
    {
        INDEX_SAFE(cullingMapBuffer, idx) = 0;
    }

    // nvidia+macOS hack (atomic operation in the if above break the write to cullingMapBuffer!).
    int intTexelVisible = texelVisible?1:0;
    atomic_add(&visibleTexelCountSharedMem,intTexelVisible);

    barrier(CLK_LOCAL_MEM_FENCE);
    if (get_local_id(0) == 0)
        atomic_add(GET_PTR_SAFE(visibleTexelCountBuffer, 0), visibleTexelCountSharedMem);
}

__kernel void lightmapCulling(
    //input
    __global unsigned char* const occupancyBuffer,
    __global float4* const  positionsWSBuffer,
    __global PackedNormalOctQuad* const interpNormalsWSBuffer,
    const Matrix4x4         worldToClip,
    const float4            cameraPosition,
    const int               cullingMapSize,
    const int               superSamplingMultiplier,
    //output
    __global unsigned char* cullingMapBuffer,
    __global unsigned int*  visibleTexelCountBuffer,//Need to have been cleared to 0 before the kernel is called.
    __global float4*        kernelDebugHelperBuffer//debug
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    __local int visibleTexelCountSharedMem;
    int idx = get_global_id(0);
    if (get_local_id(0) == 0)
        visibleTexelCountSharedMem = 0;
    barrier(CLK_LOCAL_MEM_FENCE);

    const int occupiedSamplesWithinTexel = INDEX_SAFE(occupancyBuffer, idx);
    if (occupiedSamplesWithinTexel == 0) // Reject texels that are invalid.
    {
        INDEX_SAFE(cullingMapBuffer, idx) = 0;
    }
    else
    {
        // Just fetch one sample, we know the position is within an occupied texel.
        int ssIdx = GetSuperSampledIndex(idx, 0, superSamplingMultiplier);
        float4 position = INDEX_SAFE(positionsWSBuffer, ssIdx);
        AssertPositionIsOccupied(position KERNEL_VALIDATOR_BUFFERS);

        //Clip space position
        float4 clipPos = transform_point(position.xyz, worldToClip);
        clipPos.xyz /= clipPos.w;

        //Camera to texel
        float3 camToPos = (position.xyz - cameraPosition.xyz);

        float3 normal = CalculateSuperSampledInterpolatedNormal(idx, superSamplingMultiplier, interpNormalsWSBuffer KERNEL_VALIDATOR_BUFFERS);

        float normalDotCamToPos = dot(normal, camToPos);

        if (kernelDebugHelperBuffer)
        {
            INDEX_SAFE(kernelDebugHelperBuffer, idx).xyz = clipPos.xyz;
            INDEX_SAFE(kernelDebugHelperBuffer, idx).w = normalDotCamToPos;
        }

        //Is the texel visible?
        if (clipPos.x >= -1.0f && clipPos.x <= 1.0f &&
            clipPos.y >= -1.0f && clipPos.y <= 1.0f &&
            clipPos.z >= 0.0f && clipPos.z <= 1.0f &&
            normalDotCamToPos < 0.0f)
        {
            INDEX_SAFE(cullingMapBuffer, idx) = 255;
            atomic_inc(&visibleTexelCountSharedMem);
        }
        else
        {
            INDEX_SAFE(cullingMapBuffer, idx) = 0;
        }
    }

    barrier(CLK_LOCAL_MEM_FENCE);
    if (get_local_id(0) == 0)
        atomic_add(GET_PTR_SAFE(visibleTexelCountBuffer, 0), visibleTexelCountSharedMem);
}
