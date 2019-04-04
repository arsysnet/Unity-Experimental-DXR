#ifndef ENVIRONMENT_LIGHTING_H
#define ENVIRONMENT_LIGHTING_H

enum
{
    FACE_POS_X = 0,
    FACE_NEG_X,
    FACE_POS_Y,
    FACE_NEG_Y,
    FACE_POS_Z,
    FACE_NEG_Z
};

float4 TextureSampleTrilinear(__global float4 * const env_mipped_cube_texels_buffer, __global int * const env_mip_offsets_buffer, int face_idx, int dim_face, int nrMips, const float Sin, const float Tin, const float fLodIn KERNEL_VALIDATOR_BUFFERS_DEF);

static float4 EnvCubeMapSample(__global float4 * const env_mipped_cube_texels_buffer, __global int * const env_mip_offsets_buffer,
    int dim_face, int nrMips, float3 dir, float lod KERNEL_VALIDATOR_BUFFERS_DEF)
{
    const float vx = dir.x, vy = dir.y, vz = dir.z;

    int NumMaxCoordIdx = 0;
    float NumMaxCoord = vx;
    if (fabs(vz) >= fabs(vx) && fabs(vz) >= fabs(vy))
    {
        NumMaxCoordIdx = 2;
        NumMaxCoord = vz;
    }
    else if (fabs(vy) > fabs(vx))
    {
        NumMaxCoordIdx = 1;
        NumMaxCoord = vy;
    }

    bool IsPosSign = NumMaxCoord >= 0.0f;

    float S, T;

    int f = 0;
    switch (NumMaxCoordIdx)
    {
        case 0:
            f = IsPosSign ? FACE_POS_X : FACE_NEG_X;
            S = (IsPosSign ? (-1.0f) : 1.0f) * vz;
            T = vy;
            break;
        case 1:
            f = IsPosSign ? FACE_POS_Y : FACE_NEG_Y;
            S = vx;
            T = (IsPosSign ? (-1.0f) : 1.0f) * vz;
            break;
        default:
            f = IsPosSign ? FACE_POS_Z : FACE_NEG_Z;
            S = (IsPosSign ? 1.0f : (-1.0f)) * vx;
            T = vy;
    }

    float Denom = fabs(NumMaxCoord);
    Denom = Denom < FLT_EPSILON ? FLT_EPSILON : Denom;
    S /= Denom; T /= Denom;


    return TextureSampleTrilinear(env_mipped_cube_texels_buffer, env_mip_offsets_buffer, f, dim_face, nrMips, 0.5f * S + 0.5f, 0.5f * (-T) + 0.5f, lod KERNEL_VALIDATOR_BUFFERS);
}

float4 TextureSampleBilinear(__global float4 * const env_mipped_cube_texels_buffer, const int mipOffset, const int face_idx, const int mip_dim, const float S, const float T KERNEL_VALIDATOR_BUFFERS_DEF);

float4 TextureSampleTrilinear(__global float4 * const env_mipped_cube_texels_buffer, __global int * const env_mip_offsets_buffer,
    int face_idx, int dim_face, int nrMips, const float Sin, const float Tin, const float fLodIn KERNEL_VALIDATOR_BUFFERS_DEF)
{
    const float S = min(1.0f, max(0.0f, Sin));
    const float T = min(1.0f, max(0.0f, Tin));
    const float fLod = fLodIn > (nrMips - 1) ? (nrMips - 1) : (fLodIn < 0.0f ? 0.0f : fLodIn);
    const int iLod0 = (int)fLod;
    const int iLod1 = min(iLod0 + 1, nrMips - 1);
    const float Mix = fLod - iLod0;

    const float4 pix0 = TextureSampleBilinear(env_mipped_cube_texels_buffer, INDEX_SAFE(env_mip_offsets_buffer, iLod0), face_idx, dim_face >> iLod0, S, T KERNEL_VALIDATOR_BUFFERS);
    const float4 pix1 = TextureSampleBilinear(env_mipped_cube_texels_buffer, INDEX_SAFE(env_mip_offsets_buffer, iLod1), face_idx, dim_face >> iLod1, S, T KERNEL_VALIDATOR_BUFFERS);

    return (1 - Mix) * pix0 + Mix * pix1;
}

// generate an offset into a 2D image with a 1 pixel wide skirt.
static int GenImgExtIdx(const int x, const int y, const int dim)
{
    return (y + 1) * (dim + 2) + (x + 1);
}

// this function assumes S and T are in [0;1] and the presence of borderpixels/skirt with the texture
float4 TextureSampleBilinear(__global float4 * const env_mipped_cube_texels_buffer, const int mipOffset, const int face_idx, const int mip_dim, const float S, const float T KERNEL_VALIDATOR_BUFFERS_DEF)
{
    const float U = S * mip_dim - 0.5f, V = T * mip_dim - 0.5f;
    const int offset = mipOffset + ((mip_dim + 2) * (mip_dim + 2) * face_idx);

    // technically same as a floorf() since we know -0.5f is the min. possible value for U and V
    const int u0 = ((int)(U + 1.0f)) - 1;
    const int v0 = ((int)(V + 1.0f)) - 1;

    const float dx = U - u0, dy = V - v0;
    const float weights[] = {(1 - dx) * (1 - dy), dx * (1 - dy), (1 - dx) * dy, dx * dy};

    float4 res = 0.0f;

    for (int y = 0; y < 2; y++)
    {
        for (int x = 0; x < 2; x++)
        {
            const int idx = GenImgExtIdx(u0 + x, v0 + y, mip_dim);

            const int weightsIdx = 2 * y + x;
            KERNEL_ASSERT(weightsIdx < 4);
            res += weights[weightsIdx] * INDEX_SAFE(env_mipped_cube_texels_buffer, offset + idx);
        }
    }

    return res;
}

static float4 FinalGather(__global float4 * const env_mipped_cube_texels_buffer, __global int * const env_mip_offsets_buffer,
    float3 dir, float3 normal, int envDim, int numMips, float sMipOffs KERNEL_VALIDATOR_BUFFERS_DEF)
{
    const float n_dot_l = dot(dir, normal);
    const float pdf     = n_dot_l / M_PI * 10;
    const float3 ad     = fabs(dir);

    const float maxabscomp = max(max(ad.x, ad.y), ad.z);
    float lod = sMipOffs - 0.5f * log2(max(FLT_EPSILON, pdf * maxabscomp * maxabscomp * maxabscomp));
    lod = max(0.0f, lod);

    return EnvCubeMapSample(env_mipped_cube_texels_buffer, env_mip_offsets_buffer, envDim, numMips, dir, lod KERNEL_VALIDATOR_BUFFERS);
}

#endif // ENVIRONMENT_LIGHTING_H
