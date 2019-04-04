#ifndef RGBM_ENCODING_H
#define RGBM_ENCODING_H

#include "commonCL.h"

#define TWO_POINT_TWO       2.2f
#define INV_TWO_POINT_TWO   (1.0f / 2.2f)
#define TWO_FIVE_FIVE       255.0f
#define INV_TWO_FIVE_FIVE   (1.0f / 255.0f)

static float4 RGBMEncode(float4 colorLinearSpace, float rgbmMaxRange, float lowerThreshold)
{
    const float kOneOverRGBMMax = 1.0f / (float)rgbmMaxRange;

    const float4 kZero = (float4)(0, 0, 0, 0);
    const float4 kRGBMMax = (float4)rgbmMaxRange;

    float4 color = clamp(colorLinearSpace, kZero, kRGBMMax);
    color.w = lowerThreshold;

    // Calculate the max of R, G, B and lowerThreshold.
    float4 maxXYandZW = max(color.xyzw, color.yxwz);
    float4 a = max(maxXYandZW, maxXYandZW.zwxy);

    // Calculate the multiplier and take into account the way the data is decoded in the shader.
    a = kOneOverRGBMMax * pow(a, INV_TWO_POINT_TWO);                    // convert into pseudo gamma space and scale
    a = a * TWO_FIVE_FIVE;
    a = ceil(a) * INV_TWO_FIVE_FIVE;                                    // make sure the alpha value can be represented in 8-bit or we'll get banding
    const float4 k0 = pow(kRGBMMax * a, TWO_POINT_TWO);                 // convert from pseudo gamma space and back into linear space (just like in the shader)

    color = saturate4(color / k0);
    color.w = a.w;

    return color;
}

#endif
