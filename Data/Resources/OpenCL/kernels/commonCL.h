// Common include file for the GPU version of the progressive lightmapper.
#ifndef COMMONCL_H
#define COMMONCL_H

// Set this to 1 to remove compaction when doing path rays (for debug purpose).
#define DISALLOW_PATHRAYS_COMPACTION 0

// Shared header file for .cl/C++ compatible code.
#include "CPPsharedCLincludes.h"

#ifndef INT_MIN
#define INT_MIN     (-2147483647 - 1) // minimum (signed) int value
#endif
#ifndef INT_MAX
#define INT_MAX       2147483647    // maximum (signed) int value
#endif

#ifndef M_PI
#define M_PI    3.1415926535897932384626433832795f
#endif

#ifndef M_ONE_OVER_PI
#define M_ONE_OVER_PI 0.31830988618379067153776752674503f
#endif

#ifndef FLT_EPSILON
#define FLT_EPSILON     1.192092896e-07F
#endif

#define LIGHTMAPMODE_NONDIRECTIONAL 0
#define LIGHTMAPMODE_DIRECTIONAL    1

#define TRANSMISSION_THRESHOLD 0.025f

__constant float3 kAverageFactors = (float3)(0.333333f, 0.333333f, 0.333333f);
__constant float3 kLuminanceFactors = (float3)(0.22f, 0.707f, 0.071f);
__constant sampler_t kSamplerClampNearestUnormCoords = CLK_NORMALIZED_COORDS_FALSE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_NEAREST;

//Assertion mechanism
#ifdef KERNEL_ASSERTS
int GetSafeBufferIndex(int index, int bufferID, int lineNumber, __global OpenCLKernelAssert* restrict assertBuffer, __global const uint* restrict bufferSizesBuffer)
{
    if (!assertBuffer || !bufferSizesBuffer)
        return index;

    int bufferSize = bufferSizesBuffer[bufferID];
    bool isIndexValid = (index >= 0 && index < bufferSize);

    if (!isIndexValid)
    {
        int oldDibs = atomic_inc(&(assertBuffer[0].dibs));
        if (oldDibs == 0)
        {
            assertBuffer[0].assertionValue = kOpenCLKernelAssertReason_BufferAccessedOutOfBound;
            assertBuffer[0].lineNumber = lineNumber;
            assertBuffer[0].index = index;
            assertBuffer[0].bufferSize = bufferSize;
            assertBuffer[0].bufferID = bufferID;
        }
    }
    return (isIndexValid) ? index : 0;
}

float4 ReadImagefSafe(image2d_t image, sampler_t sampler, int2 coord, int widthBufferID, int heightBufferID, int lineNumber, __global OpenCLKernelAssert* restrict assertBuffer, __global const uint* restrict bufferSizesBuffer)
{
    int2 safeCoord = (int2)(GetSafeBufferIndex(coord.x, widthBufferID, lineNumber, assertBuffer, bufferSizesBuffer), GetSafeBufferIndex(coord.y, heightBufferID, lineNumber, assertBuffer, bufferSizesBuffer));
    return read_imagef(image, sampler, safeCoord);
}

void KernelAssert(bool isOk, int lineNumber, __global OpenCLKernelAssert* restrict assertBuffer)
{
    if (!assertBuffer || isOk)
        return;

    int oldDibs = atomic_inc(&(assertBuffer[0].dibs));
    if (oldDibs == 0)
    {
        assertBuffer[0].assertionValue = kOpenCLKernelAssertReason_AssertionFailed;
        assertBuffer[0].lineNumber = lineNumber;
    }
}

    #define KERNEL_VALIDATOR_BUFFERS_DEF , __global OpenCLKernelAssert* restrict kernelAssertHelperBuffer, __global uint* restrict bufferIDToBufferSizeBuffer
    #define KERNEL_VALIDATOR_BUFFERS , kernelAssertHelperBuffer, bufferIDToBufferSizeBuffer

    #define GET_PTR_SAFE(buffer, index) buffer + GetSafeBufferIndex(index, kRRBuf_##buffer, __LINE__, kernelAssertHelperBuffer, bufferIDToBufferSizeBuffer)
    #define INDEX_SAFE(buffer, index) buffer[GetSafeBufferIndex(index, kRRBuf_##buffer, __LINE__, kernelAssertHelperBuffer, bufferIDToBufferSizeBuffer)]

    #define READ_IMAGEF_SAFE(image, sampler, coord) ReadImagefSafe(image, sampler, coord, kRRBuf_w_##image, kRRBuf_h_##image, __LINE__, kernelAssertHelperBuffer, bufferIDToBufferSizeBuffer)
    #define WRITE_IMAGEF_SAFE(image, coords, value) write_imagef(image, (int2)(GetSafeBufferIndex(coords.x, kRRBuf_w_##image, __LINE__, kernelAssertHelperBuffer, bufferIDToBufferSizeBuffer), GetSafeBufferIndex(coords.y, kRRBuf_h_##image, __LINE__, kernelAssertHelperBuffer, bufferIDToBufferSizeBuffer)), value)

    #define KERNEL_ASSERT(condition) KernelAssert(condition, __LINE__, kernelAssertHelperBuffer)
#else
    #define KERNEL_VALIDATOR_BUFFERS_DEF
    #define KERNEL_VALIDATOR_BUFFERS
    #define GET_PTR_SAFE(buffer, index) buffer + index
    #define INDEX_SAFE(buffer, index) buffer[index]
    #define READ_IMAGEF_SAFE(image, sampler, coords) read_imagef(image, sampler, coords)
    #define WRITE_IMAGEF_SAFE(image, coords, value) write_imagef(image, coords, value)
    #define KERNEL_ASSERT(condition)
#endif

/// Ray descriptor
typedef struct _ray
{
    /// xyz - origin, w - max range
    float4 o;
    /// xyz - direction, w - time
    float4 d;
    /// x - ray mask, y - activity flag
    int2 extra;
    int doBackfaceCulling;
    int padding;
} ray;

/// Intersection data returned by RadeonRays
typedef struct _Intersection
{
    // id of a shape
    int shapeid;
    // Primitive index
    int primid;
    // Padding elements
    int padding0;
    int padding1;

    // uv - hit barycentrics, w - ray distance
    float4 uvwt;
} Intersection;

typedef struct _matrix4x4
{
    float4 m0;
    float4 m1;
    float4 m2;
    float4 m3;
} Matrix4x4;

typedef struct _vector3f_storage
{
    float x;
    float y;
    float z;
} Vector3f_storage;

static float3 transform_vector(float3 p, Matrix4x4 mat)
{
    float3 res;
    res.x = dot(mat.m0.xyz, p);
    res.y = dot(mat.m1.xyz, p);
    res.z = dot(mat.m2.xyz, p);
    return res;
}

static float4 transform_point(float3 p, Matrix4x4 mat)
{
    float4 pForPoint = (float4)(p, 1.0);
    float4 res;
    res.x = dot(mat.m0, pForPoint);
    res.y = dot(mat.m1, pForPoint);
    res.z = dot(mat.m2, pForPoint);
    res.w = dot(mat.m3, pForPoint);
    return res;
}

static bool IsOccupiedPosition(float4 position)
{
    return position.w < -0.5f;
}

static bool IsCulled(unsigned char cullingMapValue)
{
    // Don't process the texels outside the camera frustum.
    return cullingMapValue != 255;
}

static void Ray_SetInactive(__private ray* r)
{
    r->extra.y = 0;
}

static bool Ray_IsInactive_Private(__private ray* r)
{
    return r->extra.y == 0;
}

static bool Ray_IsInactive(__global const ray* restrict r)
{
    return r->extra.y == 0;
}

static void Ray_SetIndex(__private ray* r, int idx)
{
    r->padding = idx;
}

static int Ray_GetIndex(__global const ray* r)
{
    return r->padding;
}

static int Ray_GetIndex_Private(__private const ray* r)
{
    return r->padding;
}

static int GetInstanceIdFromIntersection(__global const Intersection* restrict intersection)
{
    return (intersection->shapeid) - 1;
}

static void Ray_Init(__private ray* r, float3 origin, float3 direction, float maxt, float attenuation, int mask)
{
    // TODO: Check if it generates MTBUF_XYZW write
    r->o.xyz = origin;
    r->o.w = maxt;
    r->d.xyz = direction;
    r->d.w = attenuation; // nDotL [*solid angle for rectangular area light]
    r->extra.x = mask;
    r->extra.y = 0xFFFFFFFF;
}

static bool IsDirectConverged(const int currentDirectSampleCount, const int maxDirectSampleCount)
{
    return currentDirectSampleCount >= maxDirectSampleCount;
}

static bool IsGIConverged(const int currentGISampleCount, const int maxGISampleCount)
{
    return currentGISampleCount >= maxGISampleCount;
}

static float Luminance(float3 color)
{
    return dot(color, kLuminanceFactors);
}

static float4 saturate4(float4 x)
{
    const float4 zero = (float4)(0, 0, 0, 0);
    const float4 one = (float4)(1, 1, 1, 1);

    return clamp(x, zero, one);
}

static float saturate1(float x)
{
    return clamp(x, 0.0f, 1.0f);
}

static float lerp1(float a, float b, float t)
{
    return (1.0f - t) * a + t * b;
}

static float4 lerp4(float4 a, float4 b, float4 t)
{
    const float4 one = (float4)(1, 1, 1, 1);

    return (one - t) * a + t * b;
}

static float2 STTransform(float2 uv, float4 ST)
{
    return (uv * ST.xy) + ST.zw;
}

static float2 GetUVsAtPrimitiveIntersection(
    int                           const instanceId,
    int                           const primIndex,
    float2                        const barycentricCoord,
    __global const MeshDataOffsets* const instanceIdToMeshDataOffsets,
    __global const float2*          const geometryUV1sBuffer,
    __global const uint*            const geometryIndicesBuffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    const int indexOffset = INDEX_SAFE(instanceIdToMeshDataOffsets, instanceId).indexOffset;
    const int vertexOffset = INDEX_SAFE(instanceIdToMeshDataOffsets, instanceId).vertexOffset;

    const int idx0 = indexOffset + (primIndex * 3);
    const int idx1 = idx0 + 1;
    const int idx2 = idx0 + 2;

    const int uvsIdx0 = vertexOffset + INDEX_SAFE(geometryIndicesBuffer, idx0);
    const int uvsIdx1 = vertexOffset + INDEX_SAFE(geometryIndicesBuffer, idx1);
    const int uvsIdx2 = vertexOffset + INDEX_SAFE(geometryIndicesBuffer, idx2);

    const float uvs1weight = barycentricCoord.x;
    const float uvs2weight = barycentricCoord.y;
    const float uvs0weight = 1 - uvs1weight - uvs2weight;
    const float2 geometryUVs = uvs0weight * INDEX_SAFE(geometryUV1sBuffer, uvsIdx0) + uvs1weight * INDEX_SAFE(geometryUV1sBuffer, uvsIdx1) + uvs2weight * INDEX_SAFE(geometryUV1sBuffer, uvsIdx2);

    return geometryUVs;
}

static float2 GetUVsAtRayIntersection(
    int                           const rayIndex,
    __global Intersection*        const pathIntersectionsBuffer,
    __global MeshDataOffsets*     const instanceIdToMeshDataOffsets,
    __global float2*              const geometryUV1sBuffer,
    __global uint*                const geometryIndicesBuffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    const int instanceId = GetInstanceIdFromIntersection(GET_PTR_SAFE(pathIntersectionsBuffer, rayIndex));
    const int primIndex = INDEX_SAFE(pathIntersectionsBuffer, rayIndex).primid;
    const float2 barycentricCoord = INDEX_SAFE(pathIntersectionsBuffer, rayIndex).uvwt.xy;

    const float2 geometryUVs = GetUVsAtPrimitiveIntersection(instanceId, primIndex, barycentricCoord, instanceIdToMeshDataOffsets, geometryUV1sBuffer, geometryIndicesBuffer KERNEL_VALIDATOR_BUFFERS);

    return geometryUVs;
}

static void GetNormalsAtRayIntersection(
    int                           const rayIndex,
    int                           const instanceId,
    __global const Intersection*        restrict pathIntersectionsBuffer,
    __global const MeshDataOffsets*     restrict instanceIdToMeshDataOffsets,
    __global const Matrix4x4*           restrict instanceIdToInvTransposedMatrices,
    __global const Vector3f_storage*    restrict geometryPositionsBuffer,
    __global const PackedNormalOctQuad* restrict geometryNormalsBuffer,
    __global const uint*                restrict geometryIndicesBuffer,
    float3 *                      const planeNormalWSOut,
    float3 *                      const interpVertexNormalWSOut
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    const int   indexOffset = INDEX_SAFE(instanceIdToMeshDataOffsets, instanceId).indexOffset;
    const int   vertexOffset = INDEX_SAFE(instanceIdToMeshDataOffsets, instanceId).vertexOffset;
    const int   primIndex = INDEX_SAFE(pathIntersectionsBuffer, rayIndex).primid;

    const int   idx0 = indexOffset + (primIndex * 3);
    const int   idx1 = idx0 + 1;
    const int   idx2 = idx0 + 2;

    const int vertexIdx0 = vertexOffset + INDEX_SAFE(geometryIndicesBuffer, idx0);
    const int vertexIdx1 = vertexOffset + INDEX_SAFE(geometryIndicesBuffer, idx1);
    const int vertexIdx2 = vertexOffset + INDEX_SAFE(geometryIndicesBuffer, idx2);

    //plane normal from triangle position
    const Vector3f_storage p0 = INDEX_SAFE(geometryPositionsBuffer, vertexIdx0);
    const Vector3f_storage p1 = INDEX_SAFE(geometryPositionsBuffer, vertexIdx1);
    const Vector3f_storage p2 = INDEX_SAFE(geometryPositionsBuffer, vertexIdx2);
    const float3 position0 = (float3)(p0.x, p0.y, p0.z);
    const float3 position1 = (float3)(p1.x, p1.y, p1.z);
    const float3 position2 = (float3)(p2.x, p2.y, p2.z);

    const float3 side01 = position1 - position0;
    const float3 side02 = position2 - position0;
    const float3 planeNormal = cross(side01, side02);

    //interpolated vertex normal
    const float3 normal0 = DecodeNormal(INDEX_SAFE(geometryNormalsBuffer, vertexIdx0));
    const float3 normal1 = DecodeNormal(INDEX_SAFE(geometryNormalsBuffer, vertexIdx1));
    const float3 normal2 = DecodeNormal(INDEX_SAFE(geometryNormalsBuffer, vertexIdx2));

    const float normal1Weight = INDEX_SAFE(pathIntersectionsBuffer, rayIndex).uvwt.y;
    const float normal2Weight = INDEX_SAFE(pathIntersectionsBuffer, rayIndex).uvwt.x;
    const float normal0Weight = 1.0f - normal1Weight - normal2Weight;
    float3 interpVertexNormal = normal0Weight * normal0 + normal1Weight * normal1 + normal2Weight * normal2;
    const float dotVal = dot(interpVertexNormal, planeNormal);
    interpVertexNormal = (dotVal <= 0.0 || isnan(dotVal)) ? planeNormal : interpVertexNormal;

    //convert to world space
    *planeNormalWSOut = normalize(transform_vector(planeNormal, INDEX_SAFE(instanceIdToInvTransposedMatrices, instanceId)));
    *interpVertexNormalWSOut = normalize(transform_vector(interpVertexNormal, INDEX_SAFE(instanceIdToInvTransposedMatrices, instanceId)));
}

static void CreateOrthoNormalBasis(float3 n, float3* tangent, float3* bitangent)
{
    const float sign = n.z >= 0.0f ? 1.0f : -1.0f;
    const float a    = -1.0f / (sign + n.z);
    const float b    = n.x * n.y * a;

    *tangent   = (float3)(1.0f + sign * n.x * n.x * a, sign * b, -sign * n.x);
    *bitangent = (float3)(b, sign + n.y * n.y * a, -n.y);
}

static uint GetScreenCoordHash(uint2 pixel)
{
    // combine the x and y into a 32-bit int
    uint iHash = ((pixel.y & 0xffff) << 16) + (pixel.x & 0xffff);

    iHash -= (iHash << 6);
    iHash ^= (iHash >> 17);
    iHash -= (iHash << 9);
    iHash ^= (iHash << 4);
    iHash -= (iHash << 3);
    iHash ^= (iHash << 10);
    iHash ^= (iHash >> 15);

    //iHash &= 0x7fffffff; //make sure it's not negative

    return iHash;
}

// Map sample on square to disk (http://psgraphics.blogspot.com/2011/01/improved-code-for-concentric-map.html)
static float2 MapSquareToDisk(float2 uv)
{
    float phi;
    float r;

    float a = uv.x * 2.0 - 1.0;
    float b = uv.y * 2.0 - 1.0;

    if (a * a > b * b)
    {
        r = a;
        phi = (M_PI * 0.25f) * (b / a);
    }
    else
    {
        r = b;

        if (b == 0.0)
        {
            phi = M_PI * 0.5f;
        }
        else
        {
            phi = (M_PI * 0.5f) - (M_PI * 0.25f) * (a / b);
        }
    }

    return (float2)(r * cos(phi), r * sin(phi));
}

static float3 HemisphereCosineSample(float2 sample2D)
{
    float2 diskSample = MapSquareToDisk(sample2D);
    return (float3)(diskSample.x, diskSample.y, sqrt(1.0f - dot(diskSample, diskSample)));
}

static float2 Rotate2D(float angle, float2 point)
{
    float cosAng = cos(angle);
    float sinAng = sin(angle);
    return (float2)(point.x * cosAng - point.y * sinAng, point.y * cosAng + point.x * sinAng);
}

#define UNITY_SAMPLE_DIMS_PER_BOUNCE 300
#define UNITY_SAMPLE_DIM_CAMERA_OFFSET 1
#define UNITY_SAMPLE_DIM_LIGHT_OFFSET 2
#define UNITY_SAMPLE_DIM_SURFACE_OFFSET 5
#define UNITY_SAMPLE_DIM_TRANSMISSION_OFFSET 15

//TODO(RadeonRays) Fix Sobol
#define SAMPLER CMJ
//#define SAMPLER SOBOL
//#define SAMPLER RANDOM

#define NUM_SOBOL_MATRICES 1024
#include "Baikal_sampling.cl"

static uint GetScramble(int pixel_idx, int frame, int lightmapSize, __global const uint* restrict random_buffer KERNEL_VALIDATOR_BUFFERS_DEF)
{
    //TODO(RadeonRays) Decide witch code path we want to use, 2nd one is one from PVR.
    //*
    #if SAMPLER == SOBOL
    uint scrambleBaikal = INDEX_SAFE(random_buffer, pixel_idx) * 0x1fe3434f;
    #elif SAMPLER == CMJ
    uint rnd = INDEX_SAFE(random_buffer, pixel_idx);
    uint scrambleBaikal = rnd * 0x1fe3434f * ((frame + 13 * rnd) / (CMJ_DIM * CMJ_DIM));
    #endif
    return scrambleBaikal;
    /*/
    uint2 frameCoord;
    frameCoord.y = pixel_idx % lightmapSize;
    frameCoord.x = pixel_idx - (frameCoord.y * lightmapSize);
    uint scramblePVR = GetScreenCoordHash(frameCoord);

    return scramblePVR;
    //*/
}

static float GetRandomSample1D(
    int                      frame,
    const int                dimensionOffset,
    const uint               scramble,
    __global uint    const*  sobol_mat//size is NUM_SOBOL_MATRICES
)
{
    Sampler sampler;
#if SAMPLER == CMJ
    frame %= (CMJ_DIM * CMJ_DIM);
#endif
    Sampler_Init(&sampler, frame, dimensionOffset, scramble);
    float sample = Sampler_Sample1D(&sampler, SAMPLER_ARGS);
    return sample;
}

static float2 GetRandomSample2D(
    int                      frame,
    const int                dimensionOffset,
    const uint               scramble,
    __global uint    const*  sobol_mat//size is NUM_SOBOL_MATRICES
)
{
    Sampler sampler;
#if SAMPLER == CMJ
    frame %= (CMJ_DIM * CMJ_DIM);
#endif
    Sampler_Init(&sampler, frame, dimensionOffset, scramble);
    float2 sample2D = Sampler_Sample2D(&sampler, SAMPLER_ARGS);
    return sample2D;
}

static float3 GetRandomDirectionOnHemisphere(float2 sample2D, uint scramble, float3 normal, int numGoldenSample, __global float* goldenSamples)
{
    //TODO(RadeonRays) Decide witch code path we want to use, 2nd one is needed for Sobol and is the one from PVR.

    //*
    float3 direction = Sample_MapToHemisphere(sample2D, normal, 1.f);
    return direction;
}

// https://www.solidangle.com/research/egsr2013_spherical_rectangle.pdf
// s          - is the corner of the rectangle
// ex         - is the vector along the width of the rectangle (length is the width of the rectangle)
// ey         - is the vector along the height of the rectangle (length is the height of the rectangle)
// o          - is the surface point
// u, v       - are random numbers in [0-1]
// solidAngle - is the result
static float3 SphQuadSample(float3 s, float3 ex, float3 ey, float3 o, float u, float v, float* solidAngle)
{
    float exl = length(ex);
    float eyl = length(ey);
    // compute local reference system 'R'
    float3 x = ex / exl;
    float3 y = ey / eyl;
    float3 z = cross(x, y);
    // compute rectangle coords in local reference system
    float3 d = s - o;
    float z0 = dot(d, z);
    // flip 'z' to make it point against 'Q'
    if (z0 > 0.0f)
    {
        z *= -1.0f;
        z0 *= -1.0f;
    }
    float z0sq = z0 * z0;
    float x0 = dot(d, x);
    float y0 = dot(d, y);
    float x1 = x0 + exl;
    float y1 = y0 + eyl;
    float y0sq = y0 * y0;
    float y1sq = y1 * y1;
    // create vectors to four vertices
    float3 v00 = make_float3(x0, y0, z0);
    float3 v01 = make_float3(x0, y1, z0);
    float3 v10 = make_float3(x1, y0, z0);
    float3 v11 = make_float3(x1, y1, z0);
    // compute normals to edges
    float3 n0 = normalize(cross(v00, v10));
    float3 n1 = normalize(cross(v10, v11));
    float3 n2 = normalize(cross(v11, v01));
    float3 n3 = normalize(cross(v01, v00));
    // compute internal angles (gamma_i)
    float g0 = acos(-dot(n0, n1));
    float g1 = acos(-dot(n1, n2));
    float g2 = acos(-dot(n2, n3));
    float g3 = acos(-dot(n3, n0));
    // compute predefined constants
    float b0 = n0.z;
    float b1 = n2.z;
    float b0sq = b0 * b0;
    float k = 2.0f * PI - g2 - g3;
    // compute solid angle from internal angles
    float S = g0 + g1 - k;
    *solidAngle = S;

    // 1. compute 'cu'
    float au = u * S + k;
    float fu = (cos(au) * b0 - b1) / sin(au);
    float cu = 1.0f / sqrt(fu * fu + b0sq) * (fu > 0.0f ? 1.0f : -1.0f);
    cu = clamp(cu, -1.0f, 1.0f); // avoid NaNs
                                 // 2. compute 'xu'
    float xu = -(cu * z0) / sqrt(1.0f - cu * cu);
    xu = clamp(xu, x0, x1); // avoid Infs
                            // 3. compute 'yv'
    float d_ = sqrt(xu * xu + z0sq);
    float h0 = y0 / sqrt(d_ * d_ + y0sq);
    float h1 = y1 / sqrt(d_ * d_ + y1sq);
    float hv = h0 + v * (h1 - h0), hv2 = hv * hv;
    float eps = 0.0001f;
    float yv = (hv2 < 1.0f - eps) ? (hv * d_) / sqrt(1.0f - hv2) : y1;

    // 4. transform (xu,yv,z0) to world coords
    return (o + xu * x + yv * y + z0 * z);
}

// Keep in sync with SphericalHarmonicsL2::kCoeffCount and Wintermute::ProbeBakeTechnique
#define SH_COEFF_COUNT  9

// 1 / (2*sqrt(kPI))
#define K1DIV2SQRTPI        0.28209479177387814347403972578039
// sqrt(3) / (2*sqrt(kPI))
#define KSQRT3DIV2SQRTPI    0.48860251190291992158638462283835
// sqrt(15) / (2*sqrt(kPI))
#define KSQRT15DIV2SQRTPI   1.0925484305920790705433857058027
// 3 * sqrtf(5) / (4*sqrt(kPI))
#define K3SQRT5DIV4SQRTPI   0.94617469575756001809268107088713
// sqrt(15) / (4*sqrt(kPI))
#define KSQRT15DIV4SQRTPI   0.54627421529603953527169285290135
// sqrt(5)/(4*sqrt(kPI)) - comes from the missing -1 in K3SQRT5DIV4SQRTPI when compared to appendix A2 in http://www.ppsloan.org/publications/StupidSH36.pdf
#define KALMOSTONETHIRD     0.315391565252520050
// 16*kPI/17
#define KNORMALIZATION      2.9567930857315701067858823529412

void SHEvalDirection9(const float4 dir, float* const outsh)
{
    outsh[0] = K1DIV2SQRTPI;
    outsh[1] = -dir.y * KSQRT3DIV2SQRTPI;
    outsh[2] = dir.z * KSQRT3DIV2SQRTPI;
    outsh[3] = -dir.x * KSQRT3DIV2SQRTPI;
    outsh[4] = dir.x * dir.y * KSQRT15DIV2SQRTPI;
    outsh[5] = -dir.y * dir.z * KSQRT15DIV2SQRTPI;
    outsh[6] = (dir.z * dir.z * K3SQRT5DIV4SQRTPI) + (-KALMOSTONETHIRD);
    outsh[7] = -dir.x * dir.z * KSQRT15DIV2SQRTPI;
    outsh[8] = (dir.x * dir.x - dir.y * dir.y) * KSQRT15DIV4SQRTPI;
}

static void accumulateDirectSH(float3 col, float4 dir, float weight, __global float4* outputProbeDirectSHData, int probeId, int numProbes KERNEL_VALIDATOR_BUFFERS_DEF)
{
    float outsh[SH_COEFF_COUNT];

    SHEvalDirection9(dir, outsh);

    for (int c = 0; c < SH_COEFF_COUNT; ++c)
        INDEX_SAFE(outputProbeDirectSHData, numProbes * c + probeId) += (float4)(col.xyz * outsh[c] * (float)(KNORMALIZATION)*weight, 0.0);
}

static void accumulateIndirectSH(float3 col, float4 dir, float weight, __global float4* outputProbeIndirectSHData, int probeId, int numProbes KERNEL_VALIDATOR_BUFFERS_DEF)
{
    float outsh[SH_COEFF_COUNT];

    SHEvalDirection9(dir, outsh);

    for (int c = 0; c < SH_COEFF_COUNT; ++c)
        INDEX_SAFE(outputProbeIndirectSHData, numProbes * c + probeId) += (float4)(col.xyz * outsh[c] * (float)(KNORMALIZATION)*weight, 0.0);
}

int GetSuperSampledIndex(const int index, const int passIndex, const int superSamplingMultiplier)
{
    const int ss = superSamplingMultiplier * superSamplingMultiplier;
    return index * ss + (passIndex % ss);
}

static bool IsNormalValid(float3 normal)
{
    return any(isnotequal(normal, (float3)(0.0f)));
}

//Should match LightType from Lighting.h
typedef enum LightType
{
    kLightSpot = 0,
    kLightDirectional,
    kLightPoint,
    kLightRectangle,
    kLightDisc,
    kLightTypeLast = kLightDisc // keep this last
} LightType;

// Get all the super sampled normals and compute the downscaled normal.
static float3 CalculateSuperSampledPlaneNormal(const int texelIndex, const int superSamplingMultiplier, __global PackedNormalOctQuad* const planeNormalsWSBuffer KERNEL_VALIDATOR_BUFFERS_DEF)
{
    const int ssTexelCount = superSamplingMultiplier * superSamplingMultiplier;
    float3 normal = make_float3(0.f, 0.f, 0.f);
    for (int ssIndex = 0; ssIndex < ssTexelCount; ++ssIndex)
    {
        int ssIdx = GetSuperSampledIndex(texelIndex, ssIndex, superSamplingMultiplier);
        normal += DecodeNormal(INDEX_SAFE(planeNormalsWSBuffer, ssIdx));
    }
    normal /= ssTexelCount;
    return normal;
}

static float3 CalculateSuperSampledInterpolatedNormal(const int texelIndex, const int superSamplingMultiplier, __global PackedNormalOctQuad* const interpNormalsWSBuffer KERNEL_VALIDATOR_BUFFERS_DEF)
{
    const int ssTexelCount = superSamplingMultiplier * superSamplingMultiplier;
    float3 normal = make_float3(0.f, 0.f, 0.f);
    for (int ssIndex = 0; ssIndex < ssTexelCount; ++ssIndex)
    {
        int ssIdx = GetSuperSampledIndex(texelIndex, ssIndex, superSamplingMultiplier);
        normal += DecodeNormal(INDEX_SAFE(interpNormalsWSBuffer, ssIdx));
    }
    normal /= ssTexelCount;
    return normal;
}

static void AssertPositionIsOccupied(float4 position KERNEL_VALIDATOR_BUFFERS_DEF)
{
    // GPU PLM relies on BakeJobConservativeRaster::Execute(JobInfo& info) to fill all sample positions as occupied, if there's at least one occupied sample.
    // The case with no occupied samples is handled further up in the kernel already. See 'if (occupiedSamplesWithinTexel == 0)'
    KERNEL_ASSERT(!IsOccupiedPosition(position));
}

#endif
