// This file is Shared between C++ and openCL code.

#ifndef __UNITY_CPP_AND_OPENCL_SHARED_HEADER_H__
#define __UNITY_CPP_AND_OPENCL_SHARED_HEADER_H__

// IMPORTANT --------------------------------------------------------------------------
// Bump the version below when changing OpenCL include files.
// Use current date in "YYYYMMDD0" (last digit normally zero, bump it up if you want to
// change the version more than once per day) format for the version.
// If conflicts while merging, just enter current date + '0' as the version.
//
// Details:
// The Nvidia OpenCL driver is caching compiled kernels on the harddisc, unfortunately
// it does not take include files into account. However it does hash macro sent to the
// preprocessor. So we do that in LoadOpenCLProgramInternal() to force a recompilation
// when the version number below changes.
// see https://stackoverflow.com/questions/31338520/opencl-clbuildprogram-caches-source-and-does-not-recompile-if-included-source
#define UNITY_KERNEL_INCLUDES_VERSION "201801180"

#if defined(UNITY_EDITOR)
#include "Runtime/Math/Color.h"
#include "Runtime/Math/Vector2.h"
#include "Runtime/Math/Vector4.h"
#define SHARED_float3(name) float name[3]
#define SHARED_float4(name) float name[4]
#define SHARED_float2_type Vector2f
#define SHARED_float3_type Vector3f
#define SHARED_float4_type Vector4f
#define SHARED_INLINE inline
#define SHARED_Max std::max
#define SHARED_Normalize Normalize
#define INDEX_SAFE(buffer, index) buffer[index]
#else
#define SHARED_float3(name) float3 name
#define SHARED_float4(name) float4 name
#define SHARED_float2_type float2
#define SHARED_float3_type float3
#define SHARED_float4_type float4
#define SHARED_INLINE
#define SHARED_Max max
#define SHARED_Normalize normalize
#endif // UNITY_EDITOR

#ifndef APPLE // These functions are defined on OSX already

SHARED_INLINE SHARED_float2_type make_float2(float x, float y)
{
    SHARED_float2_type res;
    res.x = x;
    res.y = y;
    return res;
}

SHARED_INLINE SHARED_float3_type make_float3(float x, float y, float z)
{
    SHARED_float3_type res;
    res.x = x;
    res.y = y;
    res.z = z;
    return res;
}

SHARED_INLINE SHARED_float4_type make_float4(float x, float y, float z, float w)
{
    SHARED_float4_type res;
    res.x = x;
    res.y = y;
    res.z = z;
    res.w = w;
    return res;
}

#endif  // #ifndef(APPLE)

#define INTERSECT_BVH_WORKGROUPSIZE 64
#define INTERSECT_BVH_MAXSTACKSIZE 48

#define LIGHTMAPPER_FALLOFF_TEXTURE_WIDTH (1024)

typedef enum OpenCLKernelAssertReason
{
    kOpenCLKernelAssertReason_None = 0,
    kOpenCLKernelAssertReason_BufferAccessedOutOfBound,
    kOpenCLKernelAssertReason_AssertionFailed,
    kOpenCLKernelAssertReason_Count
} OpenCLKernelAssertReason;

typedef enum _RadeonRaysBufferID
{
    // From OpenCLRenderBuffer
    kRRBuf_invalid = 0,
    kRRBuf_lightSamples,
    kRRBuf_outputShadowmaskFromDirectBuffer,
    kRRBuf_pathRaysBuffer_0,
    kRRBuf_pathRaysBuffer_1,
    kRRBuf_pathIntersectionsBuffer,
    kRRBuf_pathThoughputBuffer,
    kRRBuf_pathLastPlaneNormalBuffer,
    kRRBuf_pathLastInterpNormalBuffer,
    kRRBuf_pathLastNormalFacingTheRayBuffer,
    kRRBuf_directSamplesBuffer,
    kRRBuf_indirectSamplesBuffer,
    kRRBuf_activePathCountBuffer_0,
    kRRBuf_activePathCountBuffer_1,
    kRRBuf_indexRemapBuffer,
    kRRBuf_totalRayCastBuffer,
    kRRBuf_lightRaysCountBuffer,
    kRRBuf_lightRaysBuffer,
    kRRBuf_lightOcclusionBuffer,
    kRRBuf_positionsWSBuffer,
    kRRBuf_kernelDebugHelperBuffer,
    kRRBuf_kernelAssertHelperBuffer,
    kRRBuf_bufferIDToBufferSizeBuffer,
    // From OpenCLRenderLightmapBuffers
    kRRBuf_outputDirectLightingBuffer,
    kRRBuf_outputIndirectLightingBuffer,
    kRRBuf_outputDirectionalFromDirectBuffer,
    kRRBuf_outputDirectionalFromGiBuffer,
    kRRBuf_outputAoBuffer,
    kRRBuf_outputValidityBuffer,
    kRRBuf_cullingMapBuffer,
    kRRBuf_visibleTexelCountBuffer,
    kRRBuf_convergenceOutputDataBuffer,
    kRRBuf_interpNormalsWSBuffer,
    kRRBuf_planeNormalsWSBuffer,
    kRRBuf_chartIndexBuffer,
    kRRBuf_occupancyBuffer,
    // From OpenCLRenderLightProbeBuffers
    kRRBuf_outputProbeDirectSHData,
    kRRBuf_outputProbeIndirectSHData,
    kRRBuf_outputProbeOcclusion,
    kRRBuf_inputLightIndices,
    kRRBuf_originalRaysBuffer,

    // From OpenCLCommonBuffer
    kRRBuf_bvhStackBuffer,
    kRRBuf_random_buffer,
    kRRBuf_goldenSample_buffer,
    kRRBuf_sobol_buffer,
    kRRBuf_distanceFalloffs_buffer,
    kRRBuf_angularFalloffLUT_buffer,
    kRRBuf_albedoTextures_buffer, // Albedo is stored in gamma space.
    kRRBuf_emissiveTextures_buffer,
    kRRBuf_transmissionTextures_buffer,
    kRRBuf_gbufferInstanceIdToReceiveShadowsBuffer,

    // From RadeonRaysLightGrid
    kRRBuf_directLightsOffsetBuffer,
    kRRBuf_directLightsBuffer,
    kRRBuf_directLightsCountPerCellBuffer,
    kRRBuf_indirectLightsOffsetBuffer,
    kRRBuf_indirectLightsBuffer,
    kRRBuf_indirectLightDistributionOffsetBuffer,
    kRRBuf_indirectLightsDistribution,
    kRRBuf_usePowerSamplingBuffer,

    // From RadeonRaysMeshManager
    kRRBuf_instanceIdToAlbedoTextureProperties,
    kRRBuf_instanceIdToEmissiveTextureProperties,
    kRRBuf_instanceIdToTransmissionTextureProperties,
    kRRBuf_instanceIdToTransmissionTextureSTs,
    kRRBuf_geometryUV0sBuffer,
    kRRBuf_geometryUV1sBuffer,
    kRRBuf_geometryPositionsBuffer,
    kRRBuf_geometryNormalsBuffer,
    kRRBuf_geometryIndicesBuffer,
    kRRBuf_instanceIdToMeshDataOffsets,
    kRRBuf_instanceIdToInvTransposedMatrices,

    // From OpenCLEnvironmentBuffers
    kRRBuf_env_mipped_cube_texels_buffer,
    kRRBuf_env_mip_offsets_buffer,

    // Dynamic argument buffers, e.g. an argument that alternates between two buffers declared above
    kRRBuf_dynarg_begin,
    kRRBuf_dynarg_countBuffer,
    kRRBuf_dynarg_directionalityBuffer,     // shared between kRRBuf_outputDirectionalFromDirectBuffer and kRRBuf_outputDirectionalFromGiBuffer
    kRRBuf_dynarg_filterWeights,
    kRRBuf_w_dynarg_dstImage,
    kRRBuf_h_dynarg_dstImage,
    kRRBuf_w_dynarg_srcImage,
    kRRBuf_h_dynarg_srcImage,
    kRRBuf_w_dynarg_dstTile,
    kRRBuf_h_dynarg_dstTile,
    kRRBuf_w_dynarg_dstTile0,
    kRRBuf_h_dynarg_dstTile0,
    kRRBuf_w_dynarg_dstTile1,
    kRRBuf_h_dynarg_dstTile1,
    kRRBuf_w_dynarg_srcTile,
    kRRBuf_h_dynarg_srcTile,
    kRRBuf_w_dynarg_srcTile0,
    kRRBuf_h_dynarg_srcTile0,
    kRRBuf_w_dynarg_srcTile1,
    kRRBuf_h_dynarg_srcTile1,
    kRRBuf_w_dynarg_srcTile2,
    kRRBuf_h_dynarg_srcTile2,
    kRRBuf_w_dynarg_srcTile3,
    kRRBuf_h_dynarg_srcTile3,

    // Denoising buffers
    kRRBuf_denoisedDirect,
    kRRBuf_denoisedIndirect,
    kRRBuf_denoisedAO,

    // Count
    kRRBuf_Count
} RadeonRaysBufferID;

typedef struct _OpenCLKernelAssert
{
    int assertionValue;
    int lineNumber;
    int index;
    int bufferSize;
    RadeonRaysBufferID bufferID;
    int dibs;
    int padding0;
    int padding1;
} OpenCLKernelAssert;

typedef struct _EnvironmentLightingInputData
{
    float       aoMaxDistance;
    int         envDim;
    int         numMips;
    float       sMipOffs;
    int         lightmapWidth;
} EnvironmentLightingInputData;

typedef struct _AreaLightData
{
    float areaHeight;
    float areaWidth;
    float pad0;
    float pad1;
    SHARED_float4(Normal);
    SHARED_float4(Tangent);
    SHARED_float4(Bitangent);
} AreaLightData;

typedef struct _DiscLightData
{
    float radius;
    float pad0;
    float pad1;
    float pad2;
    SHARED_float4(Normal);
    SHARED_float4(Tangent);
} DiscLightData;

typedef struct _SpotLightData
{
    int LightFalloffIndex;
    float cosineConeAngle;
    float inverseCosineConeAngle;
    float cotanConeAngle;
} SpotLightData;

typedef struct _PointLightData
{
    int LightFalloffIndex;
    float pad0;
    float pad1;
    float pad2;
} PointLightData;

typedef union
{
    AreaLightData areaLightData;
    SpotLightData spotLightData;
    PointLightData pointLightData;
    DiscLightData discLightData;
} LightDataUnion;

typedef struct LightSample
{
    int lightIdx;
    float lightPdf;
} LightSample;

typedef enum DirectBakeMode
{
    kDirectBakeMode_None = -3,
    kDirectBakeMode_Shaded = -2,
    kDirectBakeMode_Subtractive = -1,
    kDirectBakeMode_OcclusionChannel0 = 0,
    kDirectBakeMode_OcclusionChannel1 = 1,
    kDirectBakeMode_OcclusionChannel2 = 2,
    kDirectBakeMode_OcclusionChannel3 = 3
} DirectBakeMode;

typedef struct LightBuffer
{
#if defined(UNITY_EDITOR)
    LightBuffer()
    {
        memset(this, 0, sizeof(LightBuffer));
    }

    void SetPositionAndShadowAngle(float x, float y, float z, float shadowAngle)
    {
        pos[0] = x;
        pos[1] = y;
        pos[2] = z;
        pos[3] = shadowAngle;
    }

    void SetColor(float r, float g, float b, float a)
    {
        col[0] = r;
        col[1] = g;
        col[2] = b;
        col[3] = a;
    }

    void SetDirection(float x, float y, float z, float w)
    {
        dir[0] = x;
        dir[1] = y;
        dir[2] = z;
        dir[3] = w;
    }

#endif

    SHARED_float4(pos); // .rgb is position, .w is shadow angle
    SHARED_float4(col); // .rgb is color, .w is intensity
    SHARED_float4(dir); // .xyz is direction, .w is range

    int lightType;
    DirectBakeMode directBakeMode;
    int probeOcclusionLightIndex;
    int castShadow;

    LightDataUnion dataUnion;
} LightBuffer;

typedef struct _MeshDataOffsets
{
    int vertexOffset;
    int indexOffset;
} MeshDataOffsets;

typedef struct _MaterialTextureProperties
{
    int textureOffset;
    int textureWidth;
    int textureHeight;
    int materialProperties;
} MaterialTextureProperties;

typedef enum _MaterialInstanceProperties
{
    kMaterialInstanceProperties_UseTransmission = 0,
    kMaterialInstanceProperties_WrapModeU_Clamp = 1,//Repeat is the default
    kMaterialInstanceProperties_WrapModeV_Clamp = 2,//Repeat is the default
    kMaterialInstanceProperties_FilerMode_Point = 3,//Linear is the default
    kMaterialInstanceProperties_CastShadows = 4,
    kMaterialInstanceProperties_DoubleSidedGI = 5,
    kMaterialInstanceProperties_OddNegativeScale = 6
} MaterialInstanceProperties;

SHARED_INLINE bool GetMaterialProperty(MaterialTextureProperties matTextureProperties, MaterialInstanceProperties selectedProperty)
{
    int materialProperties = matTextureProperties.materialProperties;
    return (bool)(materialProperties & 1 << selectedProperty);
}

SHARED_INLINE void BuildMaterialProperties(MaterialTextureProperties* matTextureProperties, MaterialInstanceProperties selectedProperty, bool value)
{
    int* materialProperties = &(matTextureProperties->materialProperties);
    int mask = 1 << selectedProperty;

    //clear the corresponding MaterialInstanceProperties slot
    (*materialProperties) &= ~(mask);

    //set it to 1 if value is true
    if (value)
    {
        (*materialProperties) |= mask;
    }
}

SHARED_INLINE int GetTextureFetchOffset(const MaterialTextureProperties matProperty, int x, int y, bool gBufferFiltering)
{
    //Clamp
    const int clampedWidth = clamp(x, 0, matProperty.textureWidth - 1);
    const int clampedHeight = clamp(y, 0, matProperty.textureHeight - 1);

    //Repeat
    int repeatedWidth = x % matProperty.textureWidth;
    int repeatedHeight = y % matProperty.textureHeight;
    repeatedWidth = (repeatedWidth >= 0) ? repeatedWidth : matProperty.textureWidth + repeatedWidth;
    repeatedHeight = (repeatedHeight >= 0) ? repeatedHeight : matProperty.textureHeight + repeatedHeight;

    //Select based on material properties
    const int usedWidth = (gBufferFiltering || GetMaterialProperty(matProperty, kMaterialInstanceProperties_WrapModeU_Clamp)) ? clampedWidth : repeatedWidth;
    const int usedHeight = (gBufferFiltering || GetMaterialProperty(matProperty, kMaterialInstanceProperties_WrapModeV_Clamp)) ? clampedHeight : repeatedHeight;
    const int fetchOffset = matProperty.textureOffset + (matProperty.textureWidth * usedHeight) + usedWidth;
    return fetchOffset;
}

// Returns ±1
SHARED_INLINE SHARED_float2_type signNotZero(SHARED_float2_type v)
{
    return make_float2((v.x >= 0.0f) ? +1.0f : -1.0f, (v.y >= 0.0f) ? +1.0f : -1.0f);
}

// Assume normalized input. Output is on [-1, 1] for each component.
// The representation maps the octants of a sphere to the faces of an octahedron,
// which it then projects to the plane and unfolds into a unit square.
// Ref: A Survey of Efﬁcient Representations for Independent Unit Vectors
// http://jcgt.org/published/0003/02/01/paper.pdf
SHARED_INLINE SHARED_float2_type PackNormalOctQuadEncoded(const SHARED_float3_type v)
{
#if defined(UNITY_EDITOR)
    DebugAssert(IsNormalized(v));
#endif

    // Project the sphere onto the octahedron, and then onto the xy plane
    const SHARED_float2_type p = make_float2(v.x, v.y) * (1.0f / (fabs(v.x) + fabs(v.y) + fabs(v.z)));
    const SHARED_float2_type one = make_float2(1.f, 1.f);
    const SHARED_float2_type pyx = make_float2(fabs(p.y), fabs(p.x));

    // Reflect the folds of the lower hemisphere over the diagonals
    return (v.z <= 0.0f) ? ((one - pyx) * signNotZero(p)) : p;
}

SHARED_INLINE SHARED_float3_type UnpackNormalOctQuadEncoded(const SHARED_float2_type e)
{
    SHARED_float3_type v = make_float3(e.x, e.y, 1.0f - fabs(e.x) - fabs(e.y));
    if (v.z < 0.f)
    {
        const SHARED_float2_type vyxAbs = make_float2(fabs(v.y), fabs(v.x));
        SHARED_float2_type vxy = make_float2(v.x, v.y);
        const SHARED_float2_type one = make_float2(1.f, 1.f);
        vxy = (one - vyxAbs) * signNotZero(vxy);
        v = make_float3(vxy.x, vxy.y, v.z);
    }
    return SHARED_Normalize(v);
}

typedef struct _PackedNormalOctQuad
{
    unsigned char x;
    unsigned char y;
    unsigned char z;
    unsigned char padding;  // Nvidia Windows OpenCL driver throws CL_INVALID_ARG_SIZE when we try and clear a 3 byte struct.
#if defined(UNITY_EDITOR)
    void SetZero() { x = 0.0f; y = 0.0f; z = 0.0f; }
#endif
} PackedNormalOctQuad;

// Pack float2 (each quantized to 12 bits) into 888. The input floats are quantized in this function.
// Input should be normalized.
SHARED_INLINE PackedNormalOctQuad PackFloat2To888(const SHARED_float2_type f)
{
    SHARED_float2_type scaled = (f * 4095.5f);  // 2^12 range
    unsigned int ix = (unsigned int)scaled.x;
    unsigned int iy = (unsigned int)scaled.y;
    unsigned int hix = ix >> 8;
    unsigned int hiy = iy >> 8;
    unsigned int lox = ix & 255;
    unsigned int loy = iy & 255;
    // 8 bit in lo, 4 bit in hi
    PackedNormalOctQuad out;
    out.x = lox;
    out.y = loy;
    out.z = hix | (hiy << 4);
    return out;
}

// Unpack 2 float of 12bit packed into a 888
SHARED_INLINE SHARED_float2_type Unpack888ToFloat2(const PackedNormalOctQuad i)
{
    // 8 bit in lo, 4 bit in hi
    unsigned int hi = i.z >> 4;
    unsigned int lo = i.z & 15;
    //uint2 cb = i.xy | uint2(lo << 8, hi << 8);

    SHARED_float2_type cb;
    cb.x = i.x | lo << 8;
    cb.y = i.y | hi << 8;

    return cb / 4095.0f;
}

static float saturatev1(const float x)
{
    return x > 1.0f ? 1.0f : (x < 0.0f ? 0.0f : x);
}

static SHARED_float2_type saturate(const SHARED_float2_type in)
{
    return make_float2(saturatev1(in.x), saturatev1(in.y));
}

SHARED_INLINE PackedNormalOctQuad EncodeNormalTo888(const SHARED_float3_type normalIn)
{
    const SHARED_float2_type v2half = make_float2(0.5f, 0.5f);
    const SHARED_float2_type octNormal = PackNormalOctQuadEncoded(normalIn);
    return PackFloat2To888(saturate(octNormal * v2half + v2half));
}

SHARED_INLINE SHARED_float3_type DecodeNormal(const PackedNormalOctQuad packedNormal)
{
    const SHARED_float2_type octNormalUnpacked = Unpack888ToFloat2(packedNormal);
    const SHARED_float2_type two = make_float2(2.f, 2.f);
    const SHARED_float2_type one = make_float2(1.f, 1.f);
    return UnpackNormalOctQuadEncoded(octNormalUnpacked * two - one);
}

// From https://github.com/Unity-Technologies/ScriptableRenderPipeline/blob/master/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl
#if defined(UNITY_EDITOR)
SHARED_INLINE Vector4f Unpack8888ToFloat4(const ColorRGBA32 packed)
{
    const float x = (float)packed.r;
    const float y = (float)packed.g;
    const float z = (float)packed.b;
    const float w = (float)packed.a;
#else
float4 Unpack8888ToFloat4(const uchar4 packed)
{
    const float x = (float)packed.x;
    const float y = (float)packed.y;
    const float z = (float)packed.z;
    const float w = (float)packed.w;
#endif
    const float oneOver255 = 1.f / 255.f;
    const float fx = x * oneOver255;
    const float fy = y * oneOver255;
    const float fz = z * oneOver255;
    const float fw = w * oneOver255;
    return make_float4(fx, fy, fz, fw);
}

typedef struct _ConvergenceOutputData
{
    unsigned int occupiedTexelCount;
    unsigned int visibleConvergedDirectTexelCount;
    unsigned int visibleConvergedGITexelCount;
    unsigned int visibleTexelCount;
    unsigned int convergedDirectTexelCount;
    unsigned int convergedGITexelCount;
    unsigned int totalDirectSamples;
    unsigned int totalGISamples;
    int          minDirectSamples;
    int          minGISamples;
    int          maxDirectSamples;
    int          maxGISamples;
} ConvergenceOutputData;

enum UsePowerSamplingBufferSlot
{
    UsePowerSamplingBufferSlot_PowerSampleEnabled = 0,
    UsePowerSamplingBufferSlot_LightHitCount = 1,
    UsePowerSamplingBufferSlot_LightRayCount = 2,
    UsePowerSamplingBufferSlot_BufferSize = 3
};

enum ValidityBufferMode
{
    ValidityBufferMode_DontGenerate = 0,
    ValidityBufferMode_Generate = 1,
};

#if defined(UNITY_EDITOR)
#undef SHARED_float3
#undef SHARED_float4
#undef SHARED_float2_type
#undef SHARED_float3_type
#undef SHARED_float4_type
#undef SHARED_INLINE
#undef SHARED_Max
#undef SHARED_Normalize
#undef INDEX_SAFE
#endif // UNITY_EDITOR

#endif // __UNITY_CPP_AND_OPENCL_SHARED_HEADER_H__
