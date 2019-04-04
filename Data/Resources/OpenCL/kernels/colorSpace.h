#ifndef COLOR_SPACE_H
#define COLOR_SPACE_H

#include "commonCL.h"

#define LOWER_SCALE 12.92f
#define HIGHER_SCALE 1.055f
#define HIGHER_EXP 0.4166667f
#define HIGHER_OFFSET -0.055f
#define LOWER_THRESHOLD 0.0031308f
#define LINEAR_TO_GAMMA_POW 0.45454545454545f
#define GAMMA_TO_LINEAR_POW 2.2f

static float4 LinearToGammaSpace01(float4 value)
{
    const float alpha = value.w;
    const float4 belowLowerThresholdResult = value * LOWER_SCALE;
    const float4 aboveLowerThresholdResult = HIGHER_SCALE * pow(value, HIGHER_EXP) + HIGHER_OFFSET;
    const float4 t = saturate4(sign(value - LOWER_THRESHOLD));
    const float4 color = lerp4(belowLowerThresholdResult, aboveLowerThresholdResult, t);
    color.w = alpha;
    return color;
}

// http://www.opengl.org/registry/specs/EXT/framebuffer_sRGB.txt
// http://www.opengl.org/registry/specs/EXT/texture_sRGB_decode.txt
// {  0.0,                          0         <= cl
// {  12.92 * c,                    0         <  cl < 0.0031308
// {  1.055 * cl^0.41666 - 0.055,   0.0031308 <= cl < 1
// NOTE: sRGB extensions above define range [0..1) only
// For the values [1..+inf] we use gamma=2.2 as an approximation

static float LinearToGammaSpace(float value)
{
    if (value <= 0.0f)
        return 0.0f;
    else if (value <= 0.0031308f)
        return 12.92f * value;
    else if (value < 1.0f)
        return 1.055f * pow(value, 0.4166667f) - 0.055f;
    else if (value == 1.0f)
        return 1.0f;
    else
        return pow(value, LINEAR_TO_GAMMA_POW);
}

// http://www.opengl.org/registry/specs/EXT/framebuffer_sRGB.txt
// http://www.opengl.org/registry/specs/EXT/texture_sRGB_decode.txt
// {  cs / 12.92,                 cs <= 0.04045 }
// {  ((cs + 0.055)/1.055)^2.4,   cs >  0.04045 }
// NOTE: sRGB extensions above define range [0..1) only
// For the values [1..+inf] we use gamma=2.2 as an approximation

static float GammaToLinearSpace(float value)
{
    if (value <= 0.04045f)
        return value / 12.92f;
    else if (value < 1.0F)
        return pow((value + 0.055f) / 1.055f, 2.4f);
    else if (value == 1.0F)
        return 1.0f;
    else
        return pow(value, GAMMA_TO_LINEAR_POW);
}

static float4 GammaToLinearSpace4(float4 value)
{
    float4 result;
    result.x = GammaToLinearSpace(value.x);
    result.y = GammaToLinearSpace(value.y);
    result.z = GammaToLinearSpace(value.z);
    result.w = value.w;
    return result;
}

#endif
