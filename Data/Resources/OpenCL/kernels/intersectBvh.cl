/**********************************************************************
Copyright (c) 2016 Advanced Micro Devices, Inc. All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
********************************************************************/

//UNITY++
//Source: https://github.com/GPUOpen-LibrariesAndSDKs/RadeonRays_SDK/blob/master/RadeonRays/src/kernels/CL/intersect_bvh2_lds.cl

/*************************************************************************
EXTENSIONS
**************************************************************************/
#ifdef AMD_MEDIA_OPS
#pragma OPENCL EXTENSION cl_amd_media_ops2 : enable
#endif //! AMD_MEDIA_OPS
//UNITY--
/*************************************************************************
INCLUDES
**************************************************************************/
//UNITY++
#include "commonCL.h"
#include "textureFetch.h"
//UNITY--

/*************************************************************************
TYPE DEFINITIONS
**************************************************************************/

//UNITY++
#define MISS_MARKER -1
//UNITY--
#define INVALID_ADDR 0xffffffffu
#define INTERNAL_NODE(node) (GetAddrLeft(node) != INVALID_ADDR)

//UNITY++
#define GROUP_SIZE INTERSECT_BVH_WORKGROUPSIZE
//UNITY--
#define STACK_SIZE 32
#define LDS_STACK_SIZE 16

// BVH node
typedef struct
{
    float4 aabb_left_min_or_v0_and_addr_left;
    float4 aabb_left_max_or_v1_and_mesh_id;
    float4 aabb_right_min_or_v2_and_addr_right;
    float4 aabb_right_max_and_prim_id;

} bvh_node;

//UNITY++
/*************************************************************************
HELPER FUNCTIONS
**************************************************************************/
//UNITY--

#define GetAddrLeft(node)   as_uint((node).aabb_left_min_or_v0_and_addr_left.w)
#define GetAddrRight(node)  as_uint((node).aabb_right_min_or_v2_and_addr_right.w)
#define GetMeshId(node)     as_uint((node).aabb_left_max_or_v1_and_mesh_id.w)
#define GetPrimId(node)     as_uint((node).aabb_right_max_and_prim_id.w)

//UNITY++
inline float min3(float a, float b, float c)
{
#ifdef AMD_MEDIA_OPS
    return amd_min3(a, b, c);
#else //! AMD_MEDIA_OPS
    return min(min(a, b), c);
#endif //! AMD_MEDIA_OPS
}

inline float max3(float a, float b, float c)
{
#ifdef AMD_MEDIA_OPS
    return amd_max3(a, b, c);
#else //! AMD_MEDIA_OPS
    return max(max(a, b), c);
#endif //! AMD_MEDIA_OPS
}
//UNITY--

inline float2 fast_intersect_bbox2(float3 pmin, float3 pmax, float3 invdir, float3 oxinvdir, float t_max)
{
    const float3 f = mad(pmax.xyz, invdir, oxinvdir);
    const float3 n = mad(pmin.xyz, invdir, oxinvdir);
    const float3 tmax = max(f, n);
    const float3 tmin = min(f, n);
    const float t1 = min(min3(tmax.x, tmax.y, tmax.z), t_max);
    const float t0 = max(max3(tmin.x, tmin.y, tmin.z), 0.f);
    return (float2)(t0, t1);
}

//UNITY++
// Intersect ray against a triangle and return intersection interval value if it is in
// (0, t_max], return t_max otherwise.
inline float fast_intersect_triangle(ray r, float3 v1, float3 v2, float3 v3, float t_max)
{
    float3 const e1 = v2 - v1;
    float3 const e2 = v3 - v1;
    float3 const s1 = cross(r.d.xyz, e2);

#ifdef USE_SAFE_MATH
    float const invd = 1.f / dot(s1, e1);
#else //! USE_SAFE_MATH
    float const invd = native_recip(dot(s1, e1));
#endif //! USE_SAFE_MATH

    float3 const d = r.o.xyz - v1;
    float const b1 = dot(d, s1) * invd;
    float3 const s2 = cross(d, e1);
    float const b2 = dot(r.d.xyz, s2) * invd;
    float const temp = dot(e2, s2) * invd;

    if (b1 < 0.f || b1 > 1.f || b2 < 0.f || b1 + b2 > 1.f || temp < 0.f || temp > t_max)
    {
        return t_max;
    }
    else
    {
        return temp;
    }
}

inline int ray_is_active(ray const* r)
{
    return r->extra.y;
}

inline float3 safe_invdir(ray r)
{
    float const dirx = r.d.x;
    float const diry = r.d.y;
    float const dirz = r.d.z;
    float const ooeps = 1e-8;
    float3 invdir;
    invdir.x = 1.0f / (fabs(dirx) > ooeps ? dirx : copysign(ooeps, dirx));
    invdir.y = 1.0f / (fabs(diry) > ooeps ? diry : copysign(ooeps, diry));
    invdir.z = 1.0f / (fabs(dirz) > ooeps ? dirz : copysign(ooeps, dirz));
    return invdir;
}

// Given a point in triangle plane, calculate its barycentrics
inline float2 triangle_calculate_barycentrics(float3 p, float3 v1, float3 v2, float3 v3)
{
    float3 const e1 = v2 - v1;
    float3 const e2 = v3 - v1;
    float3 const e = p - v1;
    float const d00 = dot(e1, e1);
    float const d01 = dot(e1, e2);
    float const d11 = dot(e2, e2);
    float const d20 = dot(e, e1);
    float const d21 = dot(e, e2);

#ifdef USE_SAFE_MATH
    float const invdenom = 1.0f / (d00 * d11 - d01 * d01);
#else //! USE_SAFE_MATH
    float const invdenom = native_recip(d00 * d11 - d01 * d01);
#endif //! USE_SAFE_MATH

    float const b1 = (d11 * d20 - d01 * d21) * invdenom;
    float const b2 = (d00 * d21 - d01 * d20) * invdenom;

    return (float2)(b1, b2);
}

/*************************************************************************
KERNELS
**************************************************************************/

__kernel void clearIntersectionBuffer(
    __global Intersection* pathIntersectionsBuffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    int idx = get_global_id(0);
    INDEX_SAFE(pathIntersectionsBuffer, idx).primid = MISS_MARKER;
    INDEX_SAFE(pathIntersectionsBuffer, idx).shapeid = MISS_MARKER;
    INDEX_SAFE(pathIntersectionsBuffer, idx).uvwt = (float4)(0.0f, 0.0f, 0.0f, 0.0f);
}

__kernel void clearOcclusionBuffer(
    __global float4 *lightOcclusionBuffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
{
    int idx = get_global_id(0);
    INDEX_SAFE(lightOcclusionBuffer, idx) = (float4)(1.0f, 1.0f, 1.0f, 1.0f);
}
//UNITY--

__attribute__((reqd_work_group_size(GROUP_SIZE, 1, 1)))
__kernel void intersectWithTransmission(
    /*00*/ __global const bvh_node* restrict nodes,
    /*01*/ __global const ray* restrict pathRaysBuffer_0,
    /*02*/ __global const uint* restrict activePathCountBuffer_0,
    /*03*/ __global uint* bvhStackBuffer,
    /*04*/ __global Intersection* pathIntersectionsBuffer,
//UNITY++
    /*05*/ __global const MaterialTextureProperties* restrict instanceIdToTransmissionTextureProperties,
    /*06*/ __global const float4* restrict instanceIdToTransmissionTextureSTs,
    /*07*/ __global const MeshDataOffsets* restrict instanceIdToMeshDataOffsets,
    /*08*/ __global const float4* restrict transmissionTextures_buffer,
    /*09*/ __global const uint* restrict geometryIndicesBuffer,
    /*10*/ __global const float2* restrict geometryUV0sBuffer,
    /*11*/ __global float4* restrict pathThoughputBuffer,
    /*12*/ int lightmapSize,
    /*13*/ int frame,
    /*14*/ int bounce,
    /*15*/ __global const uint* restrict random_buffer,
    /*16*/ __global const uint* restrict sobol_buffer
    KERNEL_VALIDATOR_BUFFERS_DEF
)
//UNITY--
{
    uint index = get_global_id(0);
    uint local_index = get_local_id(0);
//UNITY++
    int dimensionOffset = UNITY_SAMPLE_DIM_SURFACE_OFFSET + bounce * UNITY_SAMPLE_DIMS_PER_BOUNCE + UNITY_SAMPLE_DIM_TRANSMISSION_OFFSET;
    __local uint lds_stack[GROUP_SIZE * LDS_STACK_SIZE];
//UNITY--

    // Handle only working subset
    if (index < INDEX_SAFE(activePathCountBuffer_0, 0))
    {
        const ray my_ray = INDEX_SAFE(pathRaysBuffer_0, index);

        if (ray_is_active(&my_ray))
        {
            const float3 invDir = safe_invdir(my_ray);
            const float3 oxInvDir = -my_ray.o.xyz * invDir;

            // Intersection parametric distance
            float closest_t = my_ray.o.w;

            // Current node address
            uint addr = 0;
            // Current closest address
            uint closest_addr = INVALID_ADDR;

            uint stack_bottom = STACK_SIZE * index;
            uint sptr = stack_bottom;
            uint lds_stack_bottom = local_index * LDS_STACK_SIZE;
            uint lds_sptr = lds_stack_bottom;

            KERNEL_ASSERT(lds_sptr < GROUP_SIZE * LDS_STACK_SIZE);
            lds_stack[lds_sptr++] = INVALID_ADDR;

            while (addr != INVALID_ADDR)
            {
                const bvh_node node = nodes[addr];

                if (INTERNAL_NODE(node))
                {
                    float2 s0 = fast_intersect_bbox2(
                        node.aabb_left_min_or_v0_and_addr_left.xyz,
                        node.aabb_left_max_or_v1_and_mesh_id.xyz,
                        invDir, oxInvDir, closest_t);
                    float2 s1 = fast_intersect_bbox2(
                        node.aabb_right_min_or_v2_and_addr_right.xyz,
                        node.aabb_right_max_and_prim_id.xyz,
                        invDir, oxInvDir, closest_t);

                    bool traverse_c0 = (s0.x <= s0.y);
                    bool traverse_c1 = (s1.x <= s1.y);
                    bool c1first = traverse_c1 && (s0.x > s1.x);

                    if (traverse_c0 || traverse_c1)
                    {
                        uint deferred = INVALID_ADDR;

                        if (c1first || !traverse_c0)
                        {
                            addr = GetAddrRight(node);
                            deferred = GetAddrLeft(node);
                        }
                        else
                        {
                            addr = GetAddrLeft(node);
                            deferred = GetAddrRight(node);
                        }

                        if (traverse_c0 && traverse_c1)
                        {
                            if (lds_sptr - lds_stack_bottom >= LDS_STACK_SIZE)
                            {
                                for (int i = 1; i < LDS_STACK_SIZE; ++i)
                                {
                                    KERNEL_ASSERT(lds_stack_bottom + i < GROUP_SIZE * LDS_STACK_SIZE);
                                    INDEX_SAFE(bvhStackBuffer, sptr + i) = lds_stack[lds_stack_bottom + i];
                                }

                                sptr += LDS_STACK_SIZE;
                                lds_sptr = lds_stack_bottom + 1;
                            }

                            KERNEL_ASSERT(lds_sptr < GROUP_SIZE * LDS_STACK_SIZE);
                            lds_stack[lds_sptr++] = deferred;
                        }

                        continue;
                    }
                }
                else
                {
                    float t = fast_intersect_triangle(
                        my_ray,
                        node.aabb_left_min_or_v0_and_addr_left.xyz,
                        node.aabb_left_max_or_v1_and_mesh_id.xyz,
                        node.aabb_right_min_or_v2_and_addr_right.xyz,
                        closest_t);

                    if (t < closest_t)
                    {
//UNITY++
                        // Evaluate whether we've hit a transparent material
                        const int instanceId = GetMeshId(node) - 1;
                        const MaterialTextureProperties matProperty = INDEX_SAFE(instanceIdToTransmissionTextureProperties, instanceId);
                        bool useTransmission = GetMaterialProperty(matProperty, kMaterialInstanceProperties_UseTransmission);
                        if (useTransmission)
                        {
                            const float3 p = my_ray.o.xyz + t * my_ray.d.xyz;
                            const float2 barycentricCoord = triangle_calculate_barycentrics(
                                p,
                                node.aabb_left_min_or_v0_and_addr_left.xyz,
                                node.aabb_left_max_or_v1_and_mesh_id.xyz,
                                node.aabb_right_min_or_v2_and_addr_right.xyz);

                            const int primIndex = GetPrimId(node);
                            const float2 geometryUVs = GetUVsAtPrimitiveIntersection(instanceId, primIndex, barycentricCoord, instanceIdToMeshDataOffsets, geometryUV0sBuffer, geometryIndicesBuffer KERNEL_VALIDATOR_BUFFERS);
                            const float2 textureUVs = geometryUVs * INDEX_SAFE(instanceIdToTransmissionTextureSTs, instanceId).xy + INDEX_SAFE(instanceIdToTransmissionTextureSTs, instanceId).zw;
                            const float4 transmission = FetchTextureFromMaterialAndUVs(transmissionTextures_buffer, textureUVs, matProperty, false KERNEL_VALIDATOR_BUFFERS);
                            const float averageTransmission = dot(transmission.xyz, kAverageFactors);
                            const int texelIndex = Ray_GetIndex(GET_PTR_SAFE(pathRaysBuffer_0, index));
                            INDEX_SAFE(pathThoughputBuffer, texelIndex) *= (float4)(transmission.x, transmission.y, transmission.z, averageTransmission);

                            // NOTE: This is wrong! The probability of either reflecting or refracting a ray
                            // should depend on the Fresnel of the material. However, since we do not support
                            // any specularity in PVR there is currently no way to query this value, so for now
                            // we use the transmission (texture) albedo.
                            uint scramble = GetScramble(texelIndex, frame, lightmapSize, random_buffer KERNEL_VALIDATOR_BUFFERS);
                            float rnd = GetRandomSample1D(frame, dimensionOffset++, scramble, sobol_buffer);
                            if (rnd >= averageTransmission)
                            {
                                closest_t = t;
                                closest_addr = addr;
                            }
                        }
                        else
                        {
//UNITY--
                            closest_t = t;
                            closest_addr = addr;
                        }
                    }
                }

                KERNEL_ASSERT(lds_sptr - 1 < GROUP_SIZE * LDS_STACK_SIZE);
                addr = lds_stack[--lds_sptr];

                if (addr == INVALID_ADDR && sptr > stack_bottom)
                {
                    sptr -= LDS_STACK_SIZE;
                    for (int i = 1; i < LDS_STACK_SIZE; ++i)
                    {
                        KERNEL_ASSERT(lds_stack_bottom + i < GROUP_SIZE * LDS_STACK_SIZE);
                        lds_stack[lds_stack_bottom + i] = INDEX_SAFE(bvhStackBuffer, sptr + i);
                    }

                    lds_sptr = lds_stack_bottom + LDS_STACK_SIZE - 1;
                    KERNEL_ASSERT(lds_sptr < GROUP_SIZE * LDS_STACK_SIZE);
                    addr = lds_stack[lds_sptr];
                }
            }

            // Check if we have found an intersection
            if (closest_addr != INVALID_ADDR)
            {
                // Calculate hit position
                const bvh_node node = nodes[closest_addr];
                const float3 p = my_ray.o.xyz + closest_t * my_ray.d.xyz;

                // Calculate barycentric coordinates
                const float2 uv = triangle_calculate_barycentrics(
                    p,
                    node.aabb_left_min_or_v0_and_addr_left.xyz,
                    node.aabb_left_max_or_v1_and_mesh_id.xyz,
                    node.aabb_right_min_or_v2_and_addr_right.xyz);

                // Update hit information
                INDEX_SAFE(pathIntersectionsBuffer, index).primid = GetPrimId(node);
                INDEX_SAFE(pathIntersectionsBuffer, index).shapeid = GetMeshId(node);
                INDEX_SAFE(pathIntersectionsBuffer, index).uvwt = (float4)(uv.x, uv.y, 0.0f, closest_t);
            }
            else
            {
                // Miss here
                INDEX_SAFE(pathIntersectionsBuffer, index).primid = MISS_MARKER;
                INDEX_SAFE(pathIntersectionsBuffer, index).shapeid = MISS_MARKER;
            }
        }
    }
}

__attribute__((reqd_work_group_size(GROUP_SIZE, 1, 1)))
__kernel void occludedWithTransmission(
    /*00*/ __global const bvh_node *restrict nodes,
    /*01*/ __global const ray *restrict lightRaysBuffer,
    /*02*/ __global const uint *restrict lightRaysCountBuffer,
    /*03*/ __global uint *bvhStackBuffer,
//UNITY++
    /*04*/ __global float4 *lightOcclusionBuffer,
    /*05*/ __global const MaterialTextureProperties* restrict instanceIdToTransmissionTextureProperties,
    /*06*/ __global const float4* restrict instanceIdToTransmissionTextureSTs,
    /*07*/ __global const MeshDataOffsets* restrict instanceIdToMeshDataOffsets,
    /*08*/ __global const float4* restrict transmissionTextures_buffer,
    /*09*/ __global const uint* restrict geometryIndicesBuffer,
    /*10*/ __global const float2* restrict geometryUV0sBuffer
    KERNEL_VALIDATOR_BUFFERS_DEF
//UNITY--
)
{
    uint index = get_global_id(0);
    uint local_index = get_local_id(0);
//UNITY++
    __local uint lds_stack[GROUP_SIZE * LDS_STACK_SIZE];
//UNITY--

    // Handle only working subset
    if (index < INDEX_SAFE(lightRaysCountBuffer, 0))
    {
//UNITY++
        // Initialize memory
        INDEX_SAFE(lightOcclusionBuffer, index) = (float4)(1.0f, 1.0f, 1.0f, 1.0f);
//UNITY--

        const ray my_ray = INDEX_SAFE(lightRaysBuffer, index);

        if (ray_is_active(&my_ray))
        {
            const float3 invDir = safe_invdir(my_ray);
            const float3 oxInvDir = -my_ray.o.xyz * invDir;

            // Current node address
            uint addr = 0;
            // Intersection parametric distance
            const float closest_t = my_ray.o.w;

            uint stack_bottom = STACK_SIZE * index;
            uint sptr = stack_bottom;
            uint lds_stack_bottom = local_index * LDS_STACK_SIZE;
            uint lds_sptr = lds_stack_bottom;

            KERNEL_ASSERT(lds_sptr < GROUP_SIZE * LDS_STACK_SIZE);
            lds_stack[lds_sptr++] = INVALID_ADDR;

            while (addr != INVALID_ADDR)
            {
                const bvh_node node = nodes[addr];

                if (INTERNAL_NODE(node))
                {
                    float2 s0 = fast_intersect_bbox2(
                        node.aabb_left_min_or_v0_and_addr_left.xyz,
                        node.aabb_left_max_or_v1_and_mesh_id.xyz,
                        invDir, oxInvDir, closest_t);
                    float2 s1 = fast_intersect_bbox2(
                        node.aabb_right_min_or_v2_and_addr_right.xyz,
                        node.aabb_right_max_and_prim_id.xyz,
                        invDir, oxInvDir, closest_t);

                    bool traverse_c0 = (s0.x <= s0.y);
                    bool traverse_c1 = (s1.x <= s1.y);
                    bool c1first = traverse_c1 && (s0.x > s1.x);

                    if (traverse_c0 || traverse_c1)
                    {
                        uint deferred = INVALID_ADDR;

                        if (c1first || !traverse_c0)
                        {
                            addr = GetAddrRight(node);
                            deferred = GetAddrLeft(node);
                        }
                        else
                        {
                            addr = GetAddrLeft(node);
                            deferred = GetAddrRight(node);
                        }

                        if (traverse_c0 && traverse_c1)
                        {
                            if (lds_sptr - lds_stack_bottom >= LDS_STACK_SIZE)
                            {
                                for (int i = 1; i < LDS_STACK_SIZE; ++i)
                                {
                                    KERNEL_ASSERT(lds_stack_bottom + i < GROUP_SIZE * LDS_STACK_SIZE);
                                    INDEX_SAFE(bvhStackBuffer, sptr + i) = lds_stack[lds_stack_bottom + i];
                                }

                                sptr += LDS_STACK_SIZE;
                                lds_sptr = lds_stack_bottom + 1;
                            }

                            KERNEL_ASSERT(lds_sptr < GROUP_SIZE * LDS_STACK_SIZE);
                            lds_stack[lds_sptr++] = deferred;
                        }

                        continue;
                    }
                }
                else
                {
                    float t = fast_intersect_triangle(
                        my_ray,
                        node.aabb_left_min_or_v0_and_addr_left.xyz,
                        node.aabb_left_max_or_v1_and_mesh_id.xyz,
                        node.aabb_right_min_or_v2_and_addr_right.xyz,
                        closest_t);

                    if (t < closest_t)
                    {
//UNITY++
                        // Evaluate whether we've hit a transparent material
                        const int instanceId = GetMeshId(node) - 1;
                        const MaterialTextureProperties matProperty = INDEX_SAFE(instanceIdToTransmissionTextureProperties, instanceId);
                        bool useTransmission = GetMaterialProperty(matProperty, kMaterialInstanceProperties_UseTransmission);
                        bool castShadows     = GetMaterialProperty(matProperty, kMaterialInstanceProperties_CastShadows);
                        if (castShadows)
                        {
                            if (useTransmission)
                            {
                                const float3 p = my_ray.o.xyz + t * my_ray.d.xyz;
                                const float2 barycentricCoord = triangle_calculate_barycentrics(
                                    p,
                                    node.aabb_left_min_or_v0_and_addr_left.xyz,
                                    node.aabb_left_max_or_v1_and_mesh_id.xyz,
                                    node.aabb_right_min_or_v2_and_addr_right.xyz);

                                const int primIndex = GetPrimId(node);
                                const float2 geometryUVs = GetUVsAtPrimitiveIntersection(instanceId, primIndex, barycentricCoord, instanceIdToMeshDataOffsets, geometryUV0sBuffer, geometryIndicesBuffer KERNEL_VALIDATOR_BUFFERS);
                                const float2 textureUVs = geometryUVs * INDEX_SAFE(instanceIdToTransmissionTextureSTs, instanceId).xy + INDEX_SAFE(instanceIdToTransmissionTextureSTs, instanceId).zw;
                                const float4 transmission = FetchTextureFromMaterialAndUVs(transmissionTextures_buffer, textureUVs, matProperty, false KERNEL_VALIDATOR_BUFFERS);
                                const float averageTransmission = dot(transmission.xyz, kAverageFactors);
                                INDEX_SAFE(lightOcclusionBuffer, index) *= (float4)(transmission.x, transmission.y, transmission.z, averageTransmission);
                                if (INDEX_SAFE(lightOcclusionBuffer, index).w < TRANSMISSION_THRESHOLD)
                                    return;// fully occluded
                            }
                            else
                            {
                                INDEX_SAFE(lightOcclusionBuffer, index) = (float4)(0.0f, 0.0f, 0.0f, 0.0f);
                                return;// fully occluded
                            }
                        }
//UNITY--
                    }
                }
                KERNEL_ASSERT(lds_sptr - 1 < GROUP_SIZE * LDS_STACK_SIZE);
                addr = lds_stack[--lds_sptr];

                if (addr == INVALID_ADDR && sptr > stack_bottom)
                {
                    sptr -= LDS_STACK_SIZE;
                    for (int i = 1; i < LDS_STACK_SIZE; ++i)
                    {
                        KERNEL_ASSERT(lds_stack_bottom + i < GROUP_SIZE * LDS_STACK_SIZE);
                        lds_stack[lds_stack_bottom + i] = INDEX_SAFE(bvhStackBuffer, sptr + i);
                    }

                    lds_sptr = lds_stack_bottom + LDS_STACK_SIZE - 1;
                    KERNEL_ASSERT(lds_sptr < GROUP_SIZE * LDS_STACK_SIZE);
                    addr = lds_stack[lds_sptr];
                }
            }
        }
    }
}
