#include "UnityPrefix.h"

#if ENABLE_UNIT_TESTS

#include "CPPsharedCLincludes.h"
#include "textureFetch.h"

#include "Runtime/Testing/Checks.h"
#include "Runtime/Testing/Testing.h"

UNIT_TEST_SUITE(CPPSharedIncludes)
{
    TEST(CPPSharedIncludes_PackAndUnpackFloat4ToRGBA8888Match)
    {
        // Emulate the packing that happens in OpenCLCommonBuffers::PrepareAlbedoAndEmissiveBuffers
        // and the unpacking that happens in FetchTextureFromMaterialAndUVsUint32.
        const ColorRGBAf albedoIn = ColorRGBAf(0.f, 0.25f, 0.5f, 1.0f);
        const ColorRGBA32 albedoPacked = albedoIn;
        const Vector4f unpacked = Unpack8888ToFloat4(albedoPacked);
        const float tolerance = 1.f / 256.f;
        CHECK_CLOSE_VECTOR4(albedoIn.GetVector4f(), unpacked, tolerance);
    }

    TEST(CPPSharedIncludes_PackFloat2To888AndUnpack)
    {
        const int kCount = 12;
        const Vector2f floats[kCount] =
        {
            Vector2f(1.f, 0.f),
            Vector2f(0.f, 1.f),
            Vector2f(0.f, 0.f),
            Vector2f(-1.f, 0.f),
            Vector2f(0.f, -1.f),
            Vector2f(0.f, 0.f),
            Vector2f(0.340799f,  0.647518f),
            Vector2f(0.340799f, -0.647518f),
            Vector2f(0.340799f,  0.647518f),
            Vector2f(-0.340799f, -0.647518f),
            Vector2f(-0.340799f, 0.647518f),
            Vector2f(-0.340799f, -0.647518f)
        };
        const float tolerance = 1.f / 256.f;
        const Vector2f half = make_float2(0.5f, 0.5f);
        const Vector2f two = make_float2(2.f, 2.f);
        const Vector2f one = make_float2(1.f, 1.f);
        for (int i = 0; i < kCount; ++i)
        {
            const Vector2f input = floats[i];
            const PackedNormalOctQuad packed = PackFloat2To888(input * half + half);
            const Vector2f unpacked = Unpack888ToFloat2(packed) * two - one;
            CHECK_CLOSE_VECTOR2(input, unpacked, tolerance);
        }
    }

    TEST(CPPSharedIncludes_PackNormalsTo888AndUnpack)
    {
        const int kNormalCount = 12;
        const Vector3f normals[kNormalCount] =
        {
            Vector3f(1.f, 0.f, 0.f),
            Vector3f(0.f, 1.f, 0.f),
            Vector3f(0.f, 0.f, 1.f),
            Vector3f(-1.f, 0.f, 0.f),
            Vector3f(0.f, -1.f, 0.f),
            Vector3f(0.f, 0.f, -1.f),
            Vector3f(0.340799f,  0.647518f,  0.681598f),
            Vector3f(0.340799f, -0.647518f,  0.681598f),
            Vector3f(0.340799f,  0.647518f, -0.681598f),
            Vector3f(-0.340799f, -0.647518f,  0.681598f),
            Vector3f(-0.340799f, 0.647518f, -0.681598f),
            Vector3f(-0.340799f, -0.647518f, -0.681598f)
        };

        const float tolerance = 1.f / 1024.f;
        for (int i = 0; i < kNormalCount; ++i)
        {
            const Vector3f& input = normals[i];
            const PackedNormalOctQuad packedNormal = EncodeNormalTo888(input);
            const Vector3f unpackedNormal = DecodeNormal(packedNormal);
            CHECK_CLOSE_VECTOR3(input, unpackedNormal, tolerance);
        }
    }

    TEST(CPPSharedIncludes_BuildMaterialProperties_GetterAndSetterMatch)
    {
        MaterialTextureProperties matProp;

        for (int i = kMaterialInstanceProperties_UseTransmission; i <= kMaterialInstanceProperties_OddNegativeScale; ++i)
        {
            matProp.materialProperties = 0;

            BuildMaterialProperties(&matProp, (MaterialInstanceProperties)i, true);
            CHECK_EQUAL(true, GetMaterialProperty(matProp, (MaterialInstanceProperties)i));
            CHECK_EQUAL(1 << i, matProp.materialProperties);

            BuildMaterialProperties(&matProp, (MaterialInstanceProperties)i, false);
            CHECK_EQUAL(false, GetMaterialProperty(matProp, (MaterialInstanceProperties)i));
            CHECK_EQUAL(0, matProp.materialProperties);
        }
    }

    TEST(CPPSharedIncludes_GetTextureFetchOffset_InsideBoundsReturnCorrectOffset)
    {
        MaterialTextureProperties matProp;
        matProp.textureOffset = 0;
        matProp.textureWidth = 28;
        matProp.textureHeight = 56;
        int offset = 0;

        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeU_Clamp, true);
        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeV_Clamp, true);
        offset = GetTextureFetchOffset(matProp, 5, 10, false);
        CHECK_EQUAL(matProp.textureWidth * 10 + 5, offset);
        offset = GetTextureFetchOffset(matProp, 5, 10, true);
        CHECK_EQUAL(matProp.textureWidth * 10 + 5, offset);

        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeU_Clamp, false);
        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeV_Clamp, false);
        offset = GetTextureFetchOffset(matProp, 5, 10, false);
        CHECK_EQUAL(matProp.textureWidth * 10 + 5, offset);
        offset = GetTextureFetchOffset(matProp, 5, 10, true);
        CHECK_EQUAL(matProp.textureWidth * 10 + 5, offset);

        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeU_Clamp, true);
        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeV_Clamp, false);
        offset = GetTextureFetchOffset(matProp, 5, 10, false);
        CHECK_EQUAL(matProp.textureWidth * 10 + 5, offset);
        offset = GetTextureFetchOffset(matProp, 5, 10, true);
        CHECK_EQUAL(matProp.textureWidth * 10 + 5, offset);

        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeU_Clamp, false);
        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeV_Clamp, true);
        offset = GetTextureFetchOffset(matProp, 5, 10, false);
        CHECK_EQUAL(matProp.textureWidth * 10 + 5, offset);
        offset = GetTextureFetchOffset(matProp, 5, 10, true);
        CHECK_EQUAL(matProp.textureWidth * 10 + 5, offset);
    }

    TEST(CPPSharedIncludes_GetTextureFetchOffset_GlobalTextureOffsetIsAdded)
    {
        MaterialTextureProperties matProp;
        matProp.textureOffset = 0;
        matProp.textureWidth = 28;
        matProp.textureHeight = 56;
        matProp.materialProperties = 0;
        const int beforeOffset = GetTextureFetchOffset(matProp, 5, 10, false);

        matProp.textureOffset = 102;
        const int afterOffset = GetTextureFetchOffset(matProp, 5, 10, false);

        CHECK_EQUAL(beforeOffset + 102, afterOffset);
    }

    TEST(CPPSharedIncludes_GetTextureFetchOffset_WrapAccordingToMaterialParamInU)
    {
        MaterialTextureProperties matProp;
        matProp.textureOffset = 0;
        matProp.textureWidth = 10;
        matProp.textureHeight = 1;
        int offset = 0;

        //Clamp
        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeU_Clamp, true);
        offset = GetTextureFetchOffset(matProp, 5, 1, false);
        CHECK_EQUAL(5, offset);
        offset = GetTextureFetchOffset(matProp, 5, 12, false);
        CHECK_EQUAL(5, offset);

        offset = GetTextureFetchOffset(matProp, 15, 1, false);
        CHECK_EQUAL(9, offset);
        offset = GetTextureFetchOffset(matProp, 15, 12, false);
        CHECK_EQUAL(9, offset);

        offset = GetTextureFetchOffset(matProp, -5, 1, false);
        CHECK_EQUAL(0, offset);
        offset = GetTextureFetchOffset(matProp, -5, 12, false);
        CHECK_EQUAL(0, offset);

        //Repeat
        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeU_Clamp, false);
        offset = GetTextureFetchOffset(matProp, 5, 1, false);
        CHECK_EQUAL(5, offset);
        offset = GetTextureFetchOffset(matProp, 5, 12, false);
        CHECK_EQUAL(5, offset);

        offset = GetTextureFetchOffset(matProp, 15, 1, false);
        CHECK_EQUAL(5, offset);
        offset = GetTextureFetchOffset(matProp, 15, 12, false);
        CHECK_EQUAL(5, offset);

        offset = GetTextureFetchOffset(matProp, -5, 1, false);
        CHECK_EQUAL(5, offset);
        offset = GetTextureFetchOffset(matProp, -5, 12, false);
        CHECK_EQUAL(5, offset);

        //GBuffer filtering (clamp even if repeat mode is set)
        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeU_Clamp, false);
        offset = GetTextureFetchOffset(matProp, 5, 1, true);
        CHECK_EQUAL(5, offset);
        offset = GetTextureFetchOffset(matProp, 5, 12, true);
        CHECK_EQUAL(5, offset);

        offset = GetTextureFetchOffset(matProp, 15, 1, true);
        CHECK_EQUAL(9, offset);
        offset = GetTextureFetchOffset(matProp, 15, 12, true);
        CHECK_EQUAL(9, offset);

        offset = GetTextureFetchOffset(matProp, -5, 1, true);
        CHECK_EQUAL(0, offset);
        offset = GetTextureFetchOffset(matProp, -5, 12, true);
        CHECK_EQUAL(0, offset);
    }

    TEST(CPPSharedIncludes_GetTextureFetchOffset_WrapAccordingToMaterialParamInV)
    {
        MaterialTextureProperties matProp;
        matProp.textureOffset = 0;
        matProp.textureWidth = 1;
        matProp.textureHeight = 10;
        int offset = 0;

        //Clamp
        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeV_Clamp, true);
        offset = GetTextureFetchOffset(matProp, 1, 5, false);
        CHECK_EQUAL(5, offset);
        offset = GetTextureFetchOffset(matProp, 12, 5, false);
        CHECK_EQUAL(5, offset);

        offset = GetTextureFetchOffset(matProp, 1, 15, false);
        CHECK_EQUAL(9, offset);
        offset = GetTextureFetchOffset(matProp, 12, 15, false);
        CHECK_EQUAL(9, offset);

        offset = GetTextureFetchOffset(matProp, 1, -5, false);
        CHECK_EQUAL(0, offset);
        offset = GetTextureFetchOffset(matProp, 12, -5, false);
        CHECK_EQUAL(0, offset);

        //Repeat
        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeV_Clamp, false);
        offset = GetTextureFetchOffset(matProp, 1, 5, false);
        CHECK_EQUAL(5, offset);
        offset = GetTextureFetchOffset(matProp, 12, 5, false);
        CHECK_EQUAL(5, offset);

        offset = GetTextureFetchOffset(matProp, 1, 15, false);
        CHECK_EQUAL(5, offset);
        offset = GetTextureFetchOffset(matProp, 12, 15, false);
        CHECK_EQUAL(5, offset);

        offset = GetTextureFetchOffset(matProp, 1, -5, false);
        CHECK_EQUAL(5, offset);
        offset = GetTextureFetchOffset(matProp, 12, -5, false);
        CHECK_EQUAL(5, offset);

        //GBuffer (same as clamp) even if release mode is set
        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeV_Clamp, false);
        offset = GetTextureFetchOffset(matProp, 1, 5, true);
        CHECK_EQUAL(5, offset);
        offset = GetTextureFetchOffset(matProp, 12, 5, true);
        CHECK_EQUAL(5, offset);

        offset = GetTextureFetchOffset(matProp, 1, 15, true);
        CHECK_EQUAL(9, offset);
        offset = GetTextureFetchOffset(matProp, 12, 15, true);
        CHECK_EQUAL(9, offset);

        offset = GetTextureFetchOffset(matProp, 1, -5, true);
        CHECK_EQUAL(0, offset);
        offset = GetTextureFetchOffset(matProp, 12, -5, true);
        CHECK_EQUAL(0, offset);
    }

    struct SetTextureFixture
    {
        SetTextureFixture() : tex(buffer + padding)
        {
            Assert((2 * padding) < bufferSize);

            memset(buffer, 0, sizeof(Vector4f) * bufferSize);
            //Set padding alpha channel to 1;
            for (int i = 0; i < padding; ++i)
            {
                buffer[i].w = 1.0f;
                buffer[bufferSize - 1 - i].w = 1.0f;
            }

            //Add red lines
            for (int u = 0; u < textureSize; u += 2)
            {
                for (int v = 0; v < textureSize; ++v)
                {
                    tex[v * textureSize + u].x = 1.0f;
                }
            }

            //Material property
            matProp.textureOffset = 0;
            matProp.textureWidth = textureSize;
            matProp.textureHeight = textureSize;
            matProp.materialProperties = 0;
        }

        static const int textureSize = 10;
        Vector4f* const tex;
        MaterialTextureProperties matProp;

    private:
        //'buffer' is the actual memory while 'tex' is a pointer to a subpart of it.
        static const int padding = 20;
        static const int bufferSize = textureSize * textureSize + 2 * padding;
        Vector4f buffer[bufferSize];
    };

    TEST_FIXTURE(SetTextureFixture, CPPSharedIncludes_GetBilinearFilteredPixelColor_DoNotMixChannelNorFetchOutOfBounds)
    {
        const float tolerance = 0.001f;

        for (float u = -1.0f; u < 2.0f; u += 0.1f)
        {
            for (float v = -1.0f; v < 2.0f; v += 0.1f)
            {
                Vector4f filteringColor = GetBilinearFilteredPixelColor(tex, Vector2f(u, v), matProp, true);
                CHECK(filteringColor.x >= -tolerance);
                CHECK(filteringColor.x <= (1.0f + tolerance));
                CHECK_EQUAL(0.0f, filteringColor.y);
                CHECK_EQUAL(0.0f, filteringColor.z);
                CHECK_EQUAL(0.0f, filteringColor.w);
            }
        }
    }

    TEST_FIXTURE(SetTextureFixture, CPPSharedIncludes_GetBilinearFilteredPixelColor_NoBlendingAtTexelCenter)
    {
        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeU_Clamp, false);
        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeV_Clamp, false);
        const float tolerance = 0.001f;

        for (int i = -textureSize; i < (2 * textureSize); ++i)
        {
            for (int j = -textureSize; j < (2 * textureSize); ++j)
            {
                Vector2f uvs((float)i, (float)j);
                uvs.x += 0.5f;
                uvs.y += 0.5f;
                uvs /= textureSize;
                Vector4f filteringColor = GetBilinearFilteredPixelColor(tex, uvs, matProp, false);
                if (i % 2)
                    CHECK_CLOSE(0.0f, filteringColor.x, tolerance);
                else
                    CHECK_CLOSE(1.0f, filteringColor.x, tolerance);
                CHECK_EQUAL(0.0f, filteringColor.y);
                CHECK_EQUAL(0.0f, filteringColor.z);
                CHECK_EQUAL(0.0f, filteringColor.w);
            }
        }
    }

    TEST_FIXTURE(SetTextureFixture, CPPSharedIncludes_GetBilinearFilteredPixelColor_BilinearBlending)
    {
        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeU_Clamp, false);
        BuildMaterialProperties(&matProp, kMaterialInstanceProperties_WrapModeV_Clamp, false);
        const float tolerance = 0.001f;

        for (int i = -textureSize; i < (2 * textureSize); ++i)
        {
            for (int j = -textureSize; j < (2 * textureSize); ++j)
            {
                Vector2f uvs((float)i, (float)j);
                uvs /= textureSize;
                Vector4f filteringColor = GetBilinearFilteredPixelColor(tex, uvs, matProp, false);
                CHECK_CLOSE(0.5f, filteringColor.x, tolerance);
                CHECK_EQUAL(0.0f, filteringColor.y);
                CHECK_EQUAL(0.0f, filteringColor.z);
                CHECK_EQUAL(0.0f, filteringColor.w);
            }
        }
    }

    TEST_FIXTURE(SetTextureFixture, CPPSharedIncludes_GetNearestPixelColor_NoFetchOutOfBound_NoBlending)
    {
        for (float u = -1.0f; u < 2.0f; u += 0.1f)
        {
            for (float v = -1.0f; v < 2.0f; v += 0.1f)
            {
                Vector4f filteringColor = GetNearestPixelColor(tex, Vector2f(u, v), matProp, true);
                CHECK(filteringColor.x == 1.0f || filteringColor.x == 0.0f);
                CHECK_EQUAL(0.0f, filteringColor.y);
                CHECK_EQUAL(0.0f, filteringColor.z);
                CHECK_EQUAL(0.0f, filteringColor.w);
            }
        }
    }
}


#endif // ENABLE_UNIT_TESTS
