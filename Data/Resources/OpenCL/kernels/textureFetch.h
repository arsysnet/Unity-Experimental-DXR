#ifndef TEXTURE_FETCH_H
#define TEXTURE_FETCH_H

#include "CPPsharedCLincludes.h"

#if defined(UNITY_EDITOR)
#include "Runtime/Math/Color.h"
#include "Runtime/Math/Vector2.h"
#include "Runtime/Math/Vector4.h"
#define SHARED_float2_type Vector2f
#define SHARED_float3_type Vector3f
#define SHARED_float4_type Vector4f
#define INDEX_SAFE(buffer, index) buffer[index]
#else
#include "colorSpace.h"
#define SHARED_float2_type float2
#define SHARED_float3_type float3
#define SHARED_float4_type float4
#endif // UNITY_EDITOR

#if defined(UNITY_EDITOR)
static Vector4f GetNearestPixelColor(
    const          Vector4f* const           emissiveTextures_buffer,
    const          Vector2f                  textureUVs,
    const          MaterialTextureProperties matProperty,
    const          bool                      gBufferFiltering)
#else
SHARED_INLINE float4 GetNearestPixelColor(
    const __global float4* const             emissiveTextures_buffer,
    const          float2                    textureUVs,
    const          MaterialTextureProperties matProperty,
    const          bool                      gBufferFiltering
    KERNEL_VALIDATOR_BUFFERS_DEF
)
#endif
{
    const int texelX = (int)(textureUVs.x * (float)matProperty.textureWidth);
    const int texelY = (int)(textureUVs.y * (float)matProperty.textureHeight);
    const int textureOffset = GetTextureFetchOffset(matProperty, texelX, texelY, gBufferFiltering);
    return INDEX_SAFE(emissiveTextures_buffer, textureOffset);
}

#if !defined(UNITY_EDITOR)
// Fetch, unpack and convert from gamma to linear space.
static float4 GetNearestPixelColorUint32(
    __global const uchar4* restrict          albedoTextures_buffer,
    const          float2                    textureUVs,
    const          MaterialTextureProperties matProperty,
    const          bool                      gBufferFiltering
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    const int texelX = (int)(textureUVs.x * (float)matProperty.textureWidth);
    const int texelY = (int)(textureUVs.y * (float)matProperty.textureHeight);
    const int textureOffset = GetTextureFetchOffset(matProperty, texelX, texelY, gBufferFiltering);
    return GammaToLinearSpace4(Unpack8888ToFloat4(INDEX_SAFE(albedoTextures_buffer, textureOffset)));
}

#endif

#if defined(UNITY_EDITOR)
inline Vector4f GetBilinearFilteredPixelColor(
    const          Vector4f* const           emissiveTextures_buffer,
    const          Vector2f                  textureUVs,
    const          MaterialTextureProperties matProperty,
    const          bool                      gBufferFiltering)
#else
inline float4 GetBilinearFilteredPixelColor(
    const __global float4* const             emissiveTextures_buffer,
    const          float2                    textureUVs,
    const          MaterialTextureProperties matProperty,
    const          bool                      gBufferFiltering
    KERNEL_VALIDATOR_BUFFERS_DEF
)
#endif
{
    //Code adapted from https://en.wikipedia.org/wiki/Bilinear_filtering
    const float u = textureUVs.x * matProperty.textureWidth - 0.5f;
    const float v = textureUVs.y * matProperty.textureHeight - 0.5f;
    const float x = floor(u);
    const float y = floor(v);
    const float u_ratio = u - x;
    const float v_ratio = v - y;
    const float u_opposite = 1.0f - u_ratio;
    const float v_opposite = 1.0f - v_ratio;

    const int iX = (int)x;
    const int iY = (int)y;

    const int X0Y0 = GetTextureFetchOffset(matProperty, iX + 0, iY + 0, gBufferFiltering);
    const int X1Y0 = GetTextureFetchOffset(matProperty, iX + 1, iY + 0, gBufferFiltering);
    const int X0Y1 = GetTextureFetchOffset(matProperty, iX + 0, iY + 1, gBufferFiltering);
    const int X1Y1 = GetTextureFetchOffset(matProperty, iX + 1, iY + 1, gBufferFiltering);

    SHARED_float4_type result;
    result = (INDEX_SAFE(emissiveTextures_buffer, X0Y0) * u_opposite + INDEX_SAFE(emissiveTextures_buffer, X1Y0) * u_ratio) * v_opposite +
        (INDEX_SAFE(emissiveTextures_buffer, X0Y1) * u_opposite + INDEX_SAFE(emissiveTextures_buffer, X1Y1) * u_ratio) * v_ratio;
    return result;
}

#if !defined(UNITY_EDITOR)
inline float4 GetBilinearFilteredPixelColorUint32(
    const __global uchar4* restrict          albedoTextures_buffer,
    const          float2                    textureUVs,
    const          MaterialTextureProperties matProperty,
    const          bool                      gBufferFiltering
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    //Code adapted from https://en.wikipedia.org/wiki/Bilinear_filtering
    const float u = textureUVs.x * matProperty.textureWidth - 0.5f;
    const float v = textureUVs.y * matProperty.textureHeight - 0.5f;
    const float x = floor(u);
    const float y = floor(v);
    const float u_ratio = u - x;
    const float v_ratio = v - y;
    const float u_opposite = 1.0f - u_ratio;
    const float v_opposite = 1.0f - v_ratio;

    const int iX = (int)x;
    const int iY = (int)y;

    const int X0Y0 = GetTextureFetchOffset(matProperty, iX + 0, iY + 0, gBufferFiltering);
    const int X1Y0 = GetTextureFetchOffset(matProperty, iX + 1, iY + 0, gBufferFiltering);
    const int X0Y1 = GetTextureFetchOffset(matProperty, iX + 0, iY + 1, gBufferFiltering);
    const int X1Y1 = GetTextureFetchOffset(matProperty, iX + 1, iY + 1, gBufferFiltering);

    SHARED_float4_type result;
    result = (Unpack8888ToFloat4(INDEX_SAFE(albedoTextures_buffer, X0Y0)) * u_opposite + Unpack8888ToFloat4(INDEX_SAFE(albedoTextures_buffer, X1Y0)) * u_ratio) * v_opposite +
        (Unpack8888ToFloat4(INDEX_SAFE(albedoTextures_buffer, X0Y1)) * u_opposite + Unpack8888ToFloat4(INDEX_SAFE(albedoTextures_buffer, X1Y1)) * u_ratio) * v_ratio;
    return result;
}

#endif

#if !defined(UNITY_EDITOR)
static float4 FetchTextureFromMaterialAndUVs(
    const __global float4* const             emissiveTextures_buffer,
    const          float2                    textureUVs,
    const          MaterialTextureProperties matProperty,
    const          bool                      gBufferFiltering
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    const float2 saneUVs = any(isnan(textureUVs)) ? (float2)(0.0f, 0.0f) : textureUVs;

    bool useNearest = GetMaterialProperty(matProperty, kMaterialInstanceProperties_FilerMode_Point) || gBufferFiltering;
    if (useNearest)
    {
        return GetNearestPixelColor(emissiveTextures_buffer, saneUVs, matProperty, gBufferFiltering KERNEL_VALIDATOR_BUFFERS);
    }
    else
    {
        return GetBilinearFilteredPixelColor(emissiveTextures_buffer, saneUVs, matProperty, gBufferFiltering KERNEL_VALIDATOR_BUFFERS);
    }
}

static float4 FetchTextureFromMaterialAndUVsUint32(
    __global const uchar4* restrict             albedoTextures_buffer,
    const          float2                       textureUVs,
    const          MaterialTextureProperties    matProperty,
    const          bool                         gBufferFiltering
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    const float2 saneUVs = any(isnan(textureUVs)) ? (float2)(0.0f, 0.0f) : textureUVs;

    bool useNearest = GetMaterialProperty(matProperty, kMaterialInstanceProperties_FilerMode_Point) || gBufferFiltering;
    if (useNearest)
    {
        return GetNearestPixelColorUint32(albedoTextures_buffer, saneUVs, matProperty, gBufferFiltering KERNEL_VALIDATOR_BUFFERS);
    }
    else
    {
        return GetBilinearFilteredPixelColorUint32(albedoTextures_buffer, saneUVs, matProperty, gBufferFiltering KERNEL_VALIDATOR_BUFFERS);
    }
}

#endif

#if defined(UNITY_EDITOR)
#undef SHARED_float2_type
#undef SHARED_float3_type
#undef SHARED_float4_type
#undef INDEX_SAFE
#endif // UNITY_EDITOR

#endif // TEXTURE_FETCH_H
