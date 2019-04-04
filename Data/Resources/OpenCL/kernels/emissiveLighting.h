#ifndef EMISSIVE_LIGHTING_H
#define EMISSIVE_LIGHTING_H

#include "commonCL.h"
#include "textureFetch.h"

typedef struct _atlasInfo
{
    float4 color;
    float2 textureUVs;
} AtlasInfo;

static AtlasInfo FetchEmissionFromRayIntersection(
    int              const   rayIndex,
    __global Intersection*    const   pathIntersectionsBuffer,
    __global MaterialTextureProperties* const instanceIdToEmissiveTextureProperties,
    __global MeshDataOffsets* const   instanceIdToMeshDataOffsets,
    __global float2*          const   geometryUV1sBuffer,
    __global uint*            const   geometryIndicesBuffer,
    __global float4*          const   emissiveTextures_buffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    const int   instanceId = GetInstanceIdFromIntersection(GET_PTR_SAFE(pathIntersectionsBuffer, rayIndex));
    const MaterialTextureProperties matProperty = INDEX_SAFE(instanceIdToEmissiveTextureProperties, instanceId);

    float2 textureUVs = GetUVsAtRayIntersection(rayIndex,
        pathIntersectionsBuffer,
        instanceIdToMeshDataOffsets,
        geometryUV1sBuffer,
        geometryIndicesBuffer
        KERNEL_VALIDATOR_BUFFERS);

    AtlasInfo atlasInfo;
    atlasInfo.color = FetchTextureFromMaterialAndUVs(emissiveTextures_buffer, textureUVs, matProperty, true KERNEL_VALIDATOR_BUFFERS);
    atlasInfo.textureUVs = textureUVs;

    return atlasInfo;
}

static AtlasInfo FetchAlbedoFromRayIntersection(
    int              const   rayIndex,
    __global const Intersection* restrict pathIntersectionsBuffer,
    __global const MaterialTextureProperties* restrict instanceIdToEmissiveTextureProperties,
    __global const MeshDataOffsets* restrict instanceIdToMeshDataOffsets,
    __global const float2* restrict geometryUV1sBuffer,
    __global const int* restrict geometryIndicesBuffer,
    __global const uchar4* restrict albedoTextures_buffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    const int   instanceId = GetInstanceIdFromIntersection(GET_PTR_SAFE(pathIntersectionsBuffer, rayIndex));
    const MaterialTextureProperties matProperty = INDEX_SAFE(instanceIdToEmissiveTextureProperties, instanceId);

    float2 textureUVs = GetUVsAtRayIntersection(rayIndex,
        pathIntersectionsBuffer,
        instanceIdToMeshDataOffsets,
        geometryUV1sBuffer,
        geometryIndicesBuffer
        KERNEL_VALIDATOR_BUFFERS);

    AtlasInfo atlasInfo;
    atlasInfo.color = FetchTextureFromMaterialAndUVsUint32(albedoTextures_buffer, textureUVs, matProperty, true KERNEL_VALIDATOR_BUFFERS);
    atlasInfo.textureUVs = textureUVs;

    return atlasInfo;
}

#endif // EMISSIVE_LIGHTING_H
