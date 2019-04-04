#include "commonCL.h"
#include "colorSpace.h"
#include "rgbmEncoding.h"

__constant float4 kZero = (float4)(0.0f, 0.0f, 0.0f, 0.0f);
__constant float4 kOne = (float4)(1.0f, 1.0f, 1.0f, 1.0f);
__constant float4 kHalf = (float4)(0.5, 0.5f, 0.5f, 0.5f);

// ----------------------------------------------------------------------------------------------------
static uint ConvertLightmapCoordinatesToIndex(int2 lightmapCoords, int lightmapSize)
{
    const int2 minval = (int2)(0, 0);
    const int2 maxval = (int2)(lightmapSize - 1, lightmapSize - 1);
    const int2 clampedCoords = clamp(lightmapCoords, minval, maxval);
    return clampedCoords.y * lightmapSize + clampedCoords.x;
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingBlit(
    __write_only image2d_t   dynarg_dstImage,
    __read_only image2d_t    dynarg_srcImage
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    // Image coordinates
    int2 coords = (int2)(get_global_id(0), get_global_id(1));

    float4 srcColor = READ_IMAGEF_SAFE(dynarg_srcImage, kSamplerClampNearestUnormCoords, coords);
    WRITE_IMAGEF_SAFE(dynarg_dstImage, coords, srcColor);
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingMarkupInvalidTexels(
    __write_only image2d_t      dynarg_dstTile,
    __read_only image2d_t       dynarg_srcTile,
    __global float* const       indirectSamplesBuffer,
    __global float* const       outputValidityBuffer,
    float                       backfaceTolerance,
    int2                        tileCoordinates,
    int                         lightmapSize
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    // Coordinates in tile space
    int2 tileThreadId = (int2)(get_global_id(0), get_global_id(1));

    // Coordinates in lightmap space
    int2 lightmapCoords = tileThreadId + tileCoordinates;

    uint index = ConvertLightmapCoordinatesToIndex(lightmapCoords, lightmapSize);

    float sampleCount = INDEX_SAFE(indirectSamplesBuffer, index);

    if (sampleCount == 0)
        return;

    const float validityValue    = INDEX_SAFE(outputValidityBuffer, index);
    float4 value                 = READ_IMAGEF_SAFE(dynarg_srcTile, kSamplerClampNearestUnormCoords, tileThreadId);

    if ((validityValue / sampleCount) > (1.f - backfaceTolerance))
    {
        value.w = 0.0f;
        WRITE_IMAGEF_SAFE(dynarg_dstTile, tileThreadId, value);
    }
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingDirect(
    __write_only image2d_t      dynarg_dstTile,
    __global float4* const      outputDirectLightingBuffer,
    int2                        tileCoordinates,
    int                         lightmapSize
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    // Coordinates in tile space
    int2 tileThreadId = (int2)(get_global_id(0), get_global_id(1));

    // Coordinates in lightmap space
    int2 lightmapCoords = tileThreadId + tileCoordinates;

    uint index = ConvertLightmapCoordinatesToIndex(lightmapCoords, lightmapSize);

    float4 lightingValue = INDEX_SAFE(outputDirectLightingBuffer, index);

    if (lightingValue.w <= 0)
        return;

    float4 result = lightingValue / max(1.0f, lightingValue.w);
    result.w = saturate1(result.w);

    WRITE_IMAGEF_SAFE(dynarg_dstTile, tileThreadId, result);
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingShadowMask(
    __write_only image2d_t      dynarg_dstTile,
    __global float4* const      outputDirectLightingBuffer,
    __global float4* const      outputShadowmaskFromDirectBuffer,
    int2                        tileCoordinates,
    int                         lightmapSize
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    // Coordinates in tile space
    int2 tileThreadId = (int2)(get_global_id(0), get_global_id(1));

    // Coordinates in lightmap space
    int2 lightmapCoords = tileThreadId + tileCoordinates;

    uint index = ConvertLightmapCoordinatesToIndex(lightmapCoords, lightmapSize);

    float4 lightingValue = INDEX_SAFE(outputDirectLightingBuffer, index);
    float4 shadowMaskValue = INDEX_SAFE(outputShadowmaskFromDirectBuffer, index);

    if (lightingValue.w <= 0)
        return;

    float4 result = shadowMaskValue / max(1.0f, lightingValue.w);
    result = saturate4(result);

    WRITE_IMAGEF_SAFE(dynarg_dstTile, tileThreadId, result);
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingIndirect(
    __write_only image2d_t          dynarg_dstTile,
    __global float4* const          outputIndirectLightingBuffer,
    __global float* const           indirectSamplesBuffer,
    float                           indirectIntensity,
    int2                            tileCoordinates,
    int                             lightmapSize
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    // Coordinates in tile space
    int2 tileThreadId = (int2)(get_global_id(0), get_global_id(1));

    // Coordinates in lightmap space
    int2 lightmapCoords = tileThreadId + tileCoordinates;

    uint index = ConvertLightmapCoordinatesToIndex(lightmapCoords, lightmapSize);

    float sampleCount = INDEX_SAFE(indirectSamplesBuffer, index);

    if (sampleCount == 0)
        return;

    float4 indirectLightValue = INDEX_SAFE(outputIndirectLightingBuffer, index);

    float4 result = indirectIntensity * indirectLightValue / sampleCount;
    result.w = 1.0f;

    WRITE_IMAGEF_SAFE(dynarg_dstTile, tileThreadId, result);
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingAO(
    __write_only image2d_t      dynarg_dstTile,
    __global float* const       outputAoBuffer,
    __global float* const       indirectSamplesBuffer,
    int2                        tileCoordinates,
    float                       aoExponent,
    int                         lightmapSize
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    // Coordinates in tile space
    int2 tileThreadId = (int2)(get_global_id(0), get_global_id(1));

    // Coordinates in lightmap space
    int2 lightmapCoords = tileThreadId + tileCoordinates;

    uint index = ConvertLightmapCoordinatesToIndex(lightmapCoords, lightmapSize);

    float sampleCount = INDEX_SAFE(indirectSamplesBuffer, index);

    if (sampleCount == 0)
        return;

    float aoValue = INDEX_SAFE(outputAoBuffer, index);

    aoValue = aoValue / max(1.0f, sampleCount);

    aoValue = pow(aoValue, aoExponent);

    float4 result = (float4)(aoValue, aoValue, aoValue, 1.0f);

    WRITE_IMAGEF_SAFE(dynarg_dstTile, tileThreadId, result);
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingAddLighting(
    __write_only image2d_t      dynarg_dstTile,
    __read_only image2d_t       dynarg_srcTile0,    // directLightingImage
    __read_only image2d_t       dynarg_srcTile1     // indirectLightingImage
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    // Coordinates in tile space
    int2 tileThreadId = (int2)(get_global_id(0), get_global_id(1));

    float4 directLightingValue = READ_IMAGEF_SAFE(dynarg_srcTile0, kSamplerClampNearestUnormCoords, tileThreadId);
    float4 indirectLightingValue = READ_IMAGEF_SAFE(dynarg_srcTile1, kSamplerClampNearestUnormCoords, tileThreadId);

    float4 result = directLightingValue + indirectLightingValue;
    result.w = saturate1(result.w);

    WRITE_IMAGEF_SAFE(dynarg_dstTile, tileThreadId, result);
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingDilate(
    __write_only image2d_t          dynarg_dstTile,
    __read_only image2d_t           dynarg_srcTile,
    __global unsigned char* const   occupancyBuffer,
    int                             useOccupancy,
    int2                            tileCoordinates,
    int                             lightmapSize
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    // Coordinates in tile space
    int2 tileThreadId = (int2)(get_global_id(0), get_global_id(1));

    float4 inputValue = READ_IMAGEF_SAFE(dynarg_srcTile, kSamplerClampNearestUnormCoords, tileThreadId);

    // The texel is valid -> just write it to the output
    if (inputValue.w > 0)
    {
        WRITE_IMAGEF_SAFE(dynarg_dstTile, tileThreadId, inputValue);
        return;
    }

    if (useOccupancy) // Internal dilation
    {
        // Coordinates in lightmap space
        int2 lightmapCoords = tileThreadId + tileCoordinates;

        int index = ConvertLightmapCoordinatesToIndex(lightmapCoords, lightmapSize);

        const int occupiedSamplesWithinTexel = INDEX_SAFE(occupancyBuffer, index);

        // A non-occupied texel, just copy when doing internal dilation.
        if (occupiedSamplesWithinTexel == 0)
        {
            WRITE_IMAGEF_SAFE(dynarg_dstTile, tileThreadId, inputValue);
            return;
        }
    }

    float4 dilated = kZero;
    float weightCount = 0.0f;

    // Note: not using READ_IMAGEF_SAFE below as those samples are expected to read just outside of the tile boundary, they will get safely clamped though.

    // Upper row
    float4 value0 = read_imagef(dynarg_srcTile, kSamplerClampNearestUnormCoords, tileThreadId + (int2)(-1, -1));
    float4 value1 = read_imagef(dynarg_srcTile, kSamplerClampNearestUnormCoords, tileThreadId + (int2)(0, -1));
    float4 value2 = read_imagef(dynarg_srcTile, kSamplerClampNearestUnormCoords, tileThreadId + (int2)(1, -1));

    // Side values
    float4 value3 = read_imagef(dynarg_srcTile, kSamplerClampNearestUnormCoords, tileThreadId + (int2)(-1, 0));
    float4 value4 = read_imagef(dynarg_srcTile, kSamplerClampNearestUnormCoords, tileThreadId + (int2)(1, 0));

    // Bottom row
    float4 value5 = read_imagef(dynarg_srcTile, kSamplerClampNearestUnormCoords, tileThreadId + (int2)(-1, 1));
    float4 value6 = read_imagef(dynarg_srcTile, kSamplerClampNearestUnormCoords, tileThreadId + (int2)(0, 1));
    float4 value7 = read_imagef(dynarg_srcTile, kSamplerClampNearestUnormCoords, tileThreadId + (int2)(1, 1));

    dilated = value0.w * value0;
    dilated += value1.w * value1;
    dilated += value2.w * value2;
    dilated += value3.w * value3;
    dilated += value4.w * value4;
    dilated += value5.w * value5;
    dilated += value6.w * value6;
    dilated += value7.w * value7;

    weightCount = value0.w;
    weightCount += value1.w;
    weightCount += value2.w;
    weightCount += value3.w;
    weightCount += value4.w;
    weightCount += value5.w;
    weightCount += value6.w;
    weightCount += value7.w;

    dilated *= 1.0f / max(1.0f, weightCount);

    dilated.w = saturate1(dilated.w);

    WRITE_IMAGEF_SAFE(dynarg_dstTile, tileThreadId, dilated);
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingNormalizeDirectionality(
    __write_only image2d_t      dynarg_dstTile,
    __global float4* const      dynarg_directionalityBuffer,
    __global PackedNormalOctQuad* const interpNormalsWSBuffer,
    int2                        tileCoordinates,
    int                         lightmapSize,
    int                         superSamplingMultiplier
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    int2 tileThreadId = (int2)(get_global_id(0), get_global_id(1));

    int2 lightmapCoords = tileThreadId + tileCoordinates;

    int index = ConvertLightmapCoordinatesToIndex(lightmapCoords, lightmapSize);

    float4 dir = INDEX_SAFE(dynarg_directionalityBuffer, index);
    dir = dir / max(0.001f, dir.w);

    float3 normalWS = CalculateSuperSampledInterpolatedNormal(index, superSamplingMultiplier, interpNormalsWSBuffer KERNEL_VALIDATOR_BUFFERS);

    // Compute rebalancing coefficients
    dir.w = dot(normalWS.xyz, dir.xyz);

    dir = dir * kHalf + kHalf;

    WRITE_IMAGEF_SAFE(dynarg_dstTile, tileThreadId, dir);
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingCombineDirectionality(
    __write_only image2d_t      dynarg_dstTile,
    __read_only image2d_t       dynarg_srcTile0, // directLightingImage
    __read_only image2d_t       dynarg_srcTile1, // indirectLightingImage
    __read_only image2d_t       dynarg_srcTile2, // directionalityFromDirectImage
    __read_only image2d_t       dynarg_srcTile3, // directionalityFromIndirectImage
    float                       indirectScale
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    int2 tileThreadId = (int2)(get_global_id(0), get_global_id(1));

    float4 directLighting               = READ_IMAGEF_SAFE(dynarg_srcTile0, kSamplerClampNearestUnormCoords, tileThreadId);
    float4 indirectLighting             = READ_IMAGEF_SAFE(dynarg_srcTile1, kSamplerClampNearestUnormCoords, tileThreadId);
    float4 directionalityFromDirect     = READ_IMAGEF_SAFE(dynarg_srcTile2, kSamplerClampNearestUnormCoords, tileThreadId);
    float4 directionalityFromIndirect   = READ_IMAGEF_SAFE(dynarg_srcTile3, kSamplerClampNearestUnormCoords, tileThreadId);

    float directWeight      = Luminance(directLighting.xyz) * length(directionalityFromDirect.xyz);
    float indirectWeight    = Luminance(indirectLighting.xyz) * length(directionalityFromIndirect.xyz) * indirectScale;

    float normalizationWeight = directWeight + indirectWeight;

    directWeight = directWeight / max(0.0001f, normalizationWeight);

    float4 output = select(directionalityFromDirect, lerp4(directionalityFromIndirect, directionalityFromDirect, (float4)directWeight), (int4)(-(indirectLighting.w > 0.0f)));

    WRITE_IMAGEF_SAFE(dynarg_dstTile, tileThreadId, output);
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingSplitRGBA(
    __write_only image2d_t      dynarg_dstTile0,    // outRGBImage
    __write_only image2d_t      dynarg_dstTile1,    // outAlphaImage
    __read_only image2d_t       dynarg_srcTile0,    // directionalLightmap
    __read_only image2d_t       dynarg_srcTile1     // lightmap
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    int2 tileCoordinates = (int2)(get_global_id(0), get_global_id(1));

    float4 directionalValue = READ_IMAGEF_SAFE(dynarg_srcTile0, kSamplerClampNearestUnormCoords, tileCoordinates);
    float4 lightmapValue    = READ_IMAGEF_SAFE(dynarg_srcTile1, kSamplerClampNearestUnormCoords, tileCoordinates);

    float4 rgbValue         = (float4)(directionalValue.xyz, lightmapValue.w);
    float4 alphaValue       = (float4)(directionalValue.www, lightmapValue.w);

    WRITE_IMAGEF_SAFE(dynarg_dstTile0, tileCoordinates, rgbValue);
    WRITE_IMAGEF_SAFE(dynarg_dstTile1, tileCoordinates, alphaValue);
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingMergeRGBA(
    __write_only image2d_t      dynarg_dstTile,
    __read_only image2d_t       dynarg_srcTile0,    // dirRGBDilatedImage
    __read_only image2d_t       dynarg_srcTile1,    // dirAlphaDilatedImage
    uint                        tileBorderWidth
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    // Coordinates in tile space
    int2 tileCoords = (int2)(get_global_id(0), get_global_id(1));

    // Discard tile border(output is smaller than the input)
    int2 sampleCoords = (int2)(tileCoords.x + tileBorderWidth, tileCoords.y + tileBorderWidth);

    float4 dirRGBValue      = READ_IMAGEF_SAFE(dynarg_srcTile0, kSamplerClampNearestUnormCoords, sampleCoords);
    float4 dirAlphaValue    = READ_IMAGEF_SAFE(dynarg_srcTile1, kSamplerClampNearestUnormCoords, sampleCoords);

    float4 result = (float4)(dirRGBValue.xyz, dirAlphaValue.x);

    WRITE_IMAGEF_SAFE(dynarg_dstTile, tileCoords, result);
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingRGBMEncode(
    __write_only image2d_t      dynarg_dstTile,
    __read_only image2d_t       dynarg_srcTile,
    float                       rgbmRange,
    float                       lowerThreshold
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    int2 tileThreadId = (int2)(get_global_id(0), get_global_id(1));

    float4 linearSpaceColor = READ_IMAGEF_SAFE(dynarg_srcTile, kSamplerClampNearestUnormCoords, tileThreadId);

    float4 rgbmValue = RGBMEncode(linearSpaceColor, rgbmRange, lowerThreshold);

    float4 gammaSpaceColor = LinearToGammaSpace01(rgbmValue);

    WRITE_IMAGEF_SAFE(dynarg_dstTile, tileThreadId, gammaSpaceColor);
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingDLDREncode(
    __write_only image2d_t  dynarg_dstTile,
    __read_only image2d_t   dynarg_srcTile
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    int2 tileThreadId = (int2)(get_global_id(0), get_global_id(1));

    float4 linearSpaceColor = READ_IMAGEF_SAFE(dynarg_srcTile, kSamplerClampNearestUnormCoords, tileThreadId);

    float4 gammaSpaceColor = (float4)(LinearToGammaSpace(linearSpaceColor.x), LinearToGammaSpace(linearSpaceColor.y), LinearToGammaSpace(linearSpaceColor.z), linearSpaceColor.w);

    gammaSpaceColor = min(gammaSpaceColor * 0.5f, kOne);

    WRITE_IMAGEF_SAFE(dynarg_dstTile, tileThreadId, gammaSpaceColor);
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingLinearToGamma(
    __write_only image2d_t      dynarg_dstTile,
    __read_only image2d_t       dynarg_srcTile
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    int2 tileThreadId = (int2)(get_global_id(0), get_global_id(1));

    float4 linearSpaceColor = READ_IMAGEF_SAFE(dynarg_srcTile, kSamplerClampNearestUnormCoords, tileThreadId);

    float4 gammaSpaceColor = (float4)(LinearToGammaSpace(linearSpaceColor.x), LinearToGammaSpace(linearSpaceColor.y), LinearToGammaSpace(linearSpaceColor.z), linearSpaceColor.w);

    WRITE_IMAGEF_SAFE(dynarg_dstTile, tileThreadId, gammaSpaceColor);
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingClampValues(
    __write_only image2d_t      dynarg_dstTile,
    __read_only image2d_t       dynarg_srcTile,
    float                       min,
    float                       max
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    const float4 vMin = (float4)(min, min, min, min);
    const float4 vMax = (float4)(max, max, max, max);

    int2 tileThreadId = (int2)(get_global_id(0), get_global_id(1));

    float4 value = READ_IMAGEF_SAFE(dynarg_srcTile, kSamplerClampNearestUnormCoords, tileThreadId);

    value = clamp(value, vMin, vMax);

    WRITE_IMAGEF_SAFE(dynarg_dstTile, tileThreadId, value);
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingMultiplyImages(
    __write_only image2d_t      dynarg_dstTile,
    __read_only image2d_t       dynarg_srcTile0,
    __read_only image2d_t       dynarg_srcTile1
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    int2 tileThreadId = (int2)(get_global_id(0), get_global_id(1));

    float4 value1 = READ_IMAGEF_SAFE(dynarg_srcTile0, kSamplerClampNearestUnormCoords, tileThreadId);
    float4 value2 = READ_IMAGEF_SAFE(dynarg_srcTile1, kSamplerClampNearestUnormCoords, tileThreadId);

    float4 result = value1 * value2;

    WRITE_IMAGEF_SAFE(dynarg_dstTile, tileThreadId, result);
}

// ----------------------------------------------------------------------------------------------------
__kernel void compositingBlitTile(
    __write_only image2d_t      dynarg_dstTile,
    __read_only image2d_t       dynarg_srcTile,
    int2                        tileCoordinates
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    int2 tileThreadId = (int2)(get_global_id(0), get_global_id(1));

    float4 value = READ_IMAGEF_SAFE(dynarg_srcTile, kSamplerClampNearestUnormCoords, tileThreadId);

    int2 lightmapCoords = tileThreadId + tileCoordinates;

    // write_imagef does appropriate data format conversion to the target image format
    WRITE_IMAGEF_SAFE(dynarg_dstTile, lightmapCoords, value);
}

// ----------------------------------------------------------------------------------------------------
static int ReadChartId(
    __global int* const     chartIndexBuffer,
    int                     lightmapSize,
    int2                    lightmapCoords
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    const int index = ConvertLightmapCoordinatesToIndex(lightmapCoords, lightmapSize);
    return INDEX_SAFE(chartIndexBuffer, index);
}

// ----------------------------------------------------------------------------------------------------
// Filters horizontally or vertically depending on filterDirection - (1, 0) or (0, 1)
__kernel void compositingGaussFilter(
    __write_only image2d_t      dynarg_dstTile,
    __read_only image2d_t       dynarg_srcTile,
    __global float* const       dynarg_filterWeights,
    __global int* const         chartIndexBuffer,
    int                         kernelWidth,
    int2                        filterDirection,
    int2                        halfKernelWidth,
    int2                        tileCoordinates,
    int                         lightmapSize
    KERNEL_VALIDATOR_BUFFERS_DEF)
{
    // Coordinates in tile space
    int2 tileThreadId = (int2)(get_global_id(0), get_global_id(1));

    // Coordinates in lightmap space
    int2 lightmapCoords = tileThreadId + tileCoordinates;

    int centerChartId = ReadChartId(chartIndexBuffer, lightmapSize, lightmapCoords KERNEL_VALIDATOR_BUFFERS);

    float4 centerValue = READ_IMAGEF_SAFE(dynarg_srcTile, kSamplerClampNearestUnormCoords, tileThreadId);

    if (centerChartId == -1 || centerValue.w == 0.0f)
        return;

    float4 filtered     = kZero;
    float  weightSum    = 0.0f;
    float  weightCount  = 0.0f;

    int2 startOffset = tileThreadId - halfKernelWidth * filterDirection;
    for (int s = 0; s < kernelWidth; s++)
    {
        int2    sampleCoords    = startOffset + s * filterDirection;

        // Note: not using READ_IMAGEF_SAFE below as those samples are expected to read just outside of the tile boundary, they will get safely clamped though.
        // The srcTile and dstTile are of the same size, so iterating over dstTile texels means trying to sample by halfKernelWidth outside of srcTile at the edges.
        // We are using a separable Gaussian blur, first the vertical one and then the horizontal. The second pass depends on being able to read the results
        // stored in the border area from the first pass. Since we simply swap srcTile and dstTile, it's the easiest to keep them of the same size instead of doing
        // the tileSize vs expanded tileSize logic.

        float4  sampleValue     = read_imagef(dynarg_srcTile, kSamplerClampNearestUnormCoords, sampleCoords);
        int     sampleChartId   = ReadChartId(chartIndexBuffer, lightmapSize, sampleCoords + tileCoordinates KERNEL_VALIDATOR_BUFFERS);

        float weight = sampleValue.w * INDEX_SAFE(dynarg_filterWeights, s);

        weight *= sampleChartId == centerChartId ? 1.0f : 0.0f;

        weightSum   += weight;
        weightCount += sampleValue.w;
        filtered    += weight * sampleValue;
    }

    filtered *= 1.0f / lerp1(1.0f, weightSum, clamp(weightCount, 0.0f, 1.0f));
    filtered.w = 1.0f;

    WRITE_IMAGEF_SAFE(dynarg_dstTile, tileThreadId, filtered);
}
