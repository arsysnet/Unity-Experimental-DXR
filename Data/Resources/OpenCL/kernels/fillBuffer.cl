#include "commonCL.h"

#define FILLBUFFER(TYPE_) \
__kernel void fillBuffer_##TYPE_( \
    __global TYPE_* buffer,       \
    TYPE_ value,                  \
    int bufferSize                \
)                                 \
{                                 \
    int idx = get_global_id(0);   \
                                  \
    if (idx < bufferSize)         \
        buffer[idx] = value;      \
}

FILLBUFFER(float)
FILLBUFFER(float2)
FILLBUFFER(float4)
FILLBUFFER(Vector3f_storage)
FILLBUFFER(int)
FILLBUFFER(uint)
FILLBUFFER(uchar)
FILLBUFFER(uchar4)
FILLBUFFER(LightSample)
FILLBUFFER(LightBuffer)
FILLBUFFER(MeshDataOffsets)
FILLBUFFER(MaterialTextureProperties)
FILLBUFFER(ray)
FILLBUFFER(Matrix4x4)
FILLBUFFER(Intersection)
FILLBUFFER(OpenCLKernelAssert)
FILLBUFFER(ConvergenceOutputData)
FILLBUFFER(PackedNormalOctQuad)
