using System.Numerics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Контракт <c>TexelDensity</c>: плотность текстуры — одна величина на мир,
/// а UV вычисляются из неё и физического размера поверхности (10.5, 10.5a).
/// </summary>
/// <remarks>
/// Функции не хранят состояние: плотность приходит параметром. Раньше здесь был
/// статический класс с изменяемым полем, что противоречило запрету статических
/// сервисов (17.2) и требованию «одна величина на мир».
/// </remarks>
public sealed class TexelDensityTests
{
    private const float Density = 32f;

    [Fact]
    public void RepeatForSize_CountsRepeatsOfTextureOnSurface()
    {
        Vector2 sizeMeters = new(4f, 2f);
        Vector2 textureTexels = new(64f, 64f);

        Vector2 repeat = TexelDensity.RepeatForSize(sizeMeters, Density, textureTexels);

        // 4 м при 32 текселях на метр — это 128 текселей, то есть две текстуры.
        MathAssert.Equal(new Vector2(2f, 1f), repeat);
    }

    [Fact]
    public void UvScaleForSize_MatchesRepeatCount()
    {
        Vector2 sizeMeters = new(1.5f, 3f);
        Vector2 textureTexels = new(48f, 64f);

        MathAssert.Equal(
            TexelDensity.RepeatForSize(sizeMeters, Density, textureTexels),
            TexelDensity.UvScaleForSize(sizeMeters, Density, textureTexels));
    }

    [Fact]
    public void RepeatForSize_IsLinearInDensity()
    {
        Vector2 sizeMeters = new(2f, 2f);
        Vector2 textureTexels = new(32f, 32f);

        Vector2 sixteen = TexelDensity.RepeatForSize(sizeMeters, 16f, textureTexels);
        Vector2 thirtyTwo = TexelDensity.RepeatForSize(sizeMeters, 32f, textureTexels);

        MathAssert.Equal(new Vector2(1f, 1f), sixteen);
        MathAssert.Equal(new Vector2(2f, 2f), thirtyTwo);
    }

    [Fact]
    public void RepeatForSize_KeepsDensityEqualForDifferentSurfaceSizesAndOrientations()
    {
        // Плотность — свойство поверхности, а не её размера: произведение
        // повторов на размер текстуры обязано дать ту же плотность.
        Vector2[] sizes =
        [
            new Vector2(4f, 2f),
            new Vector2(2f, 4f),
            new Vector2(0.5f, 8f),
        ];

        foreach (Vector2 size in sizes)
        {
            Vector2 texture = new(size.X * Density, size.Y * Density);
            Vector2 repeat = TexelDensity.RepeatForSize(size, Density, texture);

            MathAssert.Equal(Vector2.One, repeat, 1e-4f);
            MathAssert.Equal(Density, (repeat.X * texture.X) / size.X, 1e-3f);
            MathAssert.Equal(Density, (repeat.Y * texture.Y) / size.Y, 1e-3f);
        }
    }

    [Fact]
    public void RepeatForSize_DoesNotRoundFractionalRepeats()
    {
        Vector2 sizeMeters = new(1f, 1f);
        Vector2 textureTexels = new(64f, 64f);

        Vector2 repeat = TexelDensity.RepeatForSize(sizeMeters, Density, textureTexels);

        // Округление изменило бы фактическую плотность: 0.5 повтора и 0.51
        // повтора — разная плотность, а бесшовность даёт режим Repeat в шейдере.
        MathAssert.Equal(0.5f, repeat.X, 1e-5f);
        MathAssert.Equal(0.5f, repeat.Y, 1e-5f);
    }

    [Fact]
    public void RepeatForSize_RejectsNonPositiveParameters()
    {
        Vector2 validSize = new(1f, 1f);
        Vector2 validTexture = new(32f, 32f);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => TexelDensity.RepeatForSize(new Vector2(0f, 1f), Density, validTexture));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TexelDensity.RepeatForSize(validSize, 0f, validTexture));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TexelDensity.RepeatForSize(validSize, Density, new Vector2(0f, 32f)));
    }

    [Fact]
    public void CylinderCircumference_IsFullCircle()
    {
        MathAssert.Equal(2f * MathF.PI, TexelDensity.CylinderCircumference(1f), 1e-5f);
        MathAssert.Equal(0f, TexelDensity.CylinderCircumference(0f), 1e-6f);
    }

    [Fact]
    public void CylinderCircumference_GivesSameDensityAsFlatSurface()
    {
        const float radius = 0.5f;
        const float height = 2f;
        Vector2 textureTexels = new(64f, 64f);

        Vector2 size = new(TexelDensity.CylinderCircumference(radius), height);
        Vector2 repeat = TexelDensity.RepeatForSize(size, Density, textureTexels);

        // Развёртка цилиндра той же плотности, что и плоская поверхность.
        MathAssert.Equal(Density, (repeat.X * textureTexels.X) / size.X, 1e-3f);
        MathAssert.Equal(Density, (repeat.Y * textureTexels.Y) / size.Y, 1e-3f);
    }

    [Fact]
    public void RepeatForSize_WorksForThreeDimensionalSurfaces()
    {
        Vector3 sizeMeters = new(2f, 4f, 3f);
        Vector2 textureTexels = new(64f, 64f);

        Vector2 repeat = TexelDensity.RepeatForSize(sizeMeters, Density, textureTexels);

        MathAssert.Equal(new Vector2(1f, 2f), repeat);
    }

    [Fact]
    public void RepeatForSize_ForBoxFaceGivesSameDensityOnEveryFace()
    {
        Vector3 boxSize = new(2f, 4f, 3f);
        Vector2 textureTexels = new(64f, 64f);

        Vector2 faceX = TexelDensity.RepeatForSize(new Vector2(boxSize.Z, boxSize.Y), Density, textureTexels);
        Vector2 faceY = TexelDensity.RepeatForSize(new Vector2(boxSize.X, boxSize.Z), Density, textureTexels);
        Vector2 faceZ = TexelDensity.RepeatForSize(new Vector2(boxSize.X, boxSize.Y), Density, textureTexels);

        MathAssert.Equal(Density, (faceX.X * textureTexels.X) / boxSize.Z, 1e-3f);
        MathAssert.Equal(Density, (faceY.Y * textureTexels.Y) / boxSize.Z, 1e-3f);
        MathAssert.Equal(Density, (faceZ.Y * textureTexels.Y) / boxSize.Y, 1e-3f);
    }
}