#include "commonCL.h"
#include "environmentLighting.h"

__kernel void processEnvironmentLightingFromBounce(
    /*00*/ __global float4*              outputIndirectLightingBuffer,
    /*01*/ __global ray*           const pathRaysBuffer_0,
    /*02*/ __global Intersection*  const pathIntersectionsBuffer,
    /*03*/ __global float4*        const env_mipped_cube_texels_buffer,
    /*04*/ __global int*           const env_mip_offsets_buffer,
    /*05*/ __global float*               outputAoBuffer,
    /*06*/  EnvironmentLightingInputData environmentInputData,
    /*07*/ int                           lightmapMode,
    /*08*/ __global float4*              outputDirectionalFromGiBuffer,
    /*09*/ int                           lightmapSize,
    /*10*/ __global uint*          const activePathCountBuffer_0,
    //normals
    /*11*/ __global PackedNormalOctQuad* const pathLastInterpNormalBuffer,
    /*12*/ __global uint*          const indexRemapBuffer,
    /*13*/ __global float4*        const pathThoughputBuffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    uint idx = get_global_id(0);

    if (idx >= INDEX_SAFE(activePathCountBuffer_0, 0) || Ray_IsInactive(GET_PTR_SAFE(pathRaysBuffer_0, idx)))
        return;

    int texelIdx = Ray_GetIndex(GET_PTR_SAFE(pathRaysBuffer_0, idx));

    const bool hit = INDEX_SAFE(pathIntersectionsBuffer, idx).shapeid > 0;

    if (!hit)
    {
        // Environment intersection
        const int prev_idx = INDEX_SAFE(indexRemapBuffer, idx);
        const float3 N = DecodeNormal(INDEX_SAFE(pathLastInterpNormalBuffer, prev_idx));
        const float3 D = INDEX_SAFE(pathRaysBuffer_0, idx).d.xyz;
        const float4 color = FinalGather(env_mipped_cube_texels_buffer, env_mip_offsets_buffer, D, N, environmentInputData.envDim, environmentInputData.numMips, environmentInputData.sMipOffs KERNEL_VALIDATOR_BUFFERS);
        INDEX_SAFE(outputIndirectLightingBuffer, texelIdx).xyz += (color.xyz * INDEX_SAFE(pathThoughputBuffer, texelIdx).xyz);

        //compute directionality from indirect
        if (lightmapMode == LIGHTMAPMODE_DIRECTIONAL)
        {
            float luminance = Luminance(color.xyz);

            INDEX_SAFE(outputDirectionalFromGiBuffer, texelIdx).xyz += INDEX_SAFE(pathRaysBuffer_0, idx).d.xyz * luminance;
            INDEX_SAFE(outputDirectionalFromGiBuffer, texelIdx).w += luminance;
        }
    }
}

__kernel void processEnvironmentLightingAndIncrementIndirectSampleCount(
    /*00*/ __global float4*              outputIndirectLightingBuffer,
    /*01*/ __global ray*           const pathRaysBuffer_0,
    /*02*/ __global Intersection*  const pathIntersectionsBuffer,
    /*03*/ __global PackedNormalOctQuad* const interpNormalsWSBuffer,
    /*04*/ __global float4*        const env_mipped_cube_texels_buffer,
    /*05*/ __global int*           const env_mip_offsets_buffer,
    /*06*/ __global float*               outputAoBuffer,
    /*07*/  EnvironmentLightingInputData environmentInputData,
    /*08*/ int                           lightmapMode,
    /*09*/ __global float4*              outputDirectionalFromGiBuffer,
    /*10*/ int                           lightmapSize,
    /*11*/ __global uint*          const activePathCountBuffer_0,
    /*12*/ int                           passIndex,
    /*13*/ int                           superSamplingMultiplier
    KERNEL_VALIDATOR_BUFFERS_DEF
    )
{
    uint idx = get_global_id(0);

    if (idx >= INDEX_SAFE(activePathCountBuffer_0, 0) || Ray_IsInactive(GET_PTR_SAFE(pathRaysBuffer_0, idx)))
        return;

    int texelidx = Ray_GetIndex(GET_PTR_SAFE(pathRaysBuffer_0, idx));

    const bool hit = INDEX_SAFE(pathIntersectionsBuffer, idx).shapeid > 0;
    const float t = INDEX_SAFE(pathIntersectionsBuffer, idx).uvwt.w;

    if (!hit || (t > environmentInputData.aoMaxDistance))
    {
        INDEX_SAFE(outputAoBuffer, texelidx) += 1.0f;
    }

    if (!hit)
    {
        int ssIdx = GetSuperSampledIndex(texelidx, passIndex, superSamplingMultiplier);

        // Environment intersection
        const float3 N = DecodeNormal(INDEX_SAFE(interpNormalsWSBuffer, ssIdx));
        const float3 D = INDEX_SAFE(pathRaysBuffer_0, idx).d.xyz;
        const float4 color = FinalGather(env_mipped_cube_texels_buffer, env_mip_offsets_buffer, D, N, environmentInputData.envDim, environmentInputData.numMips, environmentInputData.sMipOffs KERNEL_VALIDATOR_BUFFERS);
        INDEX_SAFE(outputIndirectLightingBuffer, texelidx).xyz += color.xyz;

        //compute directionality from indirect
        if (lightmapMode == LIGHTMAPMODE_DIRECTIONAL)
        {
            float luminance = Luminance(color.xyz);

            INDEX_SAFE(outputDirectionalFromGiBuffer, texelidx).xyz += INDEX_SAFE(pathRaysBuffer_0, idx).d.xyz * luminance;
            INDEX_SAFE(outputDirectionalFromGiBuffer, texelidx).w += luminance;
        }
    }

    INDEX_SAFE(outputIndirectLightingBuffer, texelidx).w += 1.0f;
}

__kernel void processEnvironmentLightingForLightProbes(
    /*01*/ __global float4*              outputProbeIndirectSHData,
    /*02*/ __global ray*           const pathRaysBuffer_0,
    /*03*/ __global Intersection*  const pathIntersectionsBuffer,
    /*04*/ __global float4*        const positionsWSBuffer,
    /*05*/ int                           numProbes,
    /*06*/ __global float4*        const env_mipped_cube_texels_buffer,
    /*07*/ __global int*           const env_mip_offsets_buffer,
    /*08*/ EnvironmentLightingInputData  environmentInputData,
    /*09*/ __global uint*     restrict   random_buffer,
    /*10*/ __global uint        const*   sobol_buffer,
    /*11*/ int                           totalSampleCount,
    /*12*/ __global uint*          const activePathCountBuffer_0
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    uint idx = get_global_id(0);

    if (idx >= INDEX_SAFE(activePathCountBuffer_0, 0) || Ray_IsInactive(GET_PTR_SAFE(pathRaysBuffer_0, idx)))
        return;

    int probeId = Ray_GetIndex(GET_PTR_SAFE(pathRaysBuffer_0, idx));

    const bool hit = INDEX_SAFE(pathIntersectionsBuffer, idx).shapeid > 0;

    if (!hit)
    {
        float weight = 4.0 / totalSampleCount;
        const float4 D = (float4)(INDEX_SAFE(pathRaysBuffer_0, probeId).d.x, INDEX_SAFE(pathRaysBuffer_0, probeId).d.y, INDEX_SAFE(pathRaysBuffer_0, probeId).d.z, 0);

        // sample environment
        float3 L = EnvCubeMapSample(env_mipped_cube_texels_buffer, env_mip_offsets_buffer, environmentInputData.envDim, environmentInputData.numMips, D.xyz, 0 KERNEL_VALIDATOR_BUFFERS).xyz;
        accumulateIndirectSH(L, D, weight, outputProbeIndirectSHData, probeId, numProbes KERNEL_VALIDATOR_BUFFERS);
    }
}

__kernel void processEnvironmentLightingForLightProbesFromBounce(
    /*00*/ __global float4*              outputProbeIndirectSHData,
    /*01*/ __global ray*           const pathRaysBuffer_0,
    /*02*/ __global Intersection*  const pathIntersectionsBuffer,
    /*03*/ __global float4*        const env_mipped_cube_texels_buffer,
    /*04*/ __global int*           const env_mip_offsets_buffer,
    /*05*/  EnvironmentLightingInputData environmentInputData,
    /*06*/ __global uint*          const activePathCountBuffer_0,
    //normals
    /*07*/ __global float4*        const pathLastInterpNormalBuffer,
    /*08*/ __global float4*        const pathThoughputBuffer,
    /*09*/ int                           numProbes,
    /*10*/ int                           totalSampleCount,
    /*11*/ __global float4*        const originalRaysBuffer,
    /*12*/ __global uint*          const indexRemapBuffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    uint idx = get_global_id(0);

    if (idx >= INDEX_SAFE(activePathCountBuffer_0, 0) || Ray_IsInactive(GET_PTR_SAFE(pathRaysBuffer_0, idx)))
        return;

    int probeId = Ray_GetIndex(GET_PTR_SAFE(pathRaysBuffer_0, idx));

    const bool hit = INDEX_SAFE(pathIntersectionsBuffer, idx).shapeid > 0;

    if (!hit)
    {
        // Environment intersection
        const int prev_idx = INDEX_SAFE(indexRemapBuffer, idx);
        const float3 N = INDEX_SAFE(pathLastInterpNormalBuffer, prev_idx).xyz;
        const float3 D = INDEX_SAFE(pathRaysBuffer_0, idx).d.xyz;
        const float3 L = FinalGather(env_mipped_cube_texels_buffer, env_mip_offsets_buffer, D, N, environmentInputData.envDim, environmentInputData.numMips, environmentInputData.sMipOffs KERNEL_VALIDATOR_BUFFERS).xyz;

        const float4 oD = INDEX_SAFE(originalRaysBuffer, probeId);
        const float weight = 4.0 / totalSampleCount;
        accumulateIndirectSH(L, oD, weight, outputProbeIndirectSHData, probeId, numProbes KERNEL_VALIDATOR_BUFFERS);
    }
}
