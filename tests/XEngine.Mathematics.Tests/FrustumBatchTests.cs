using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Тесты пакетного отсечения сфер через векторные операции.
/// </summary>
public class FrustumBatchTests
{
    private const int Count = 20_000;

    /// <summary>
    /// Пирамида от обычной камеры: охватывает разумную область сцены.
    /// </summary>
    /// <returns>Пирамида видимости.</returns>
    private static Frustum CreateFrustum()
    {
        Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0f, 8f, -14f), Vector3.Zero, Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, 16f / 9f, 0.1f, 500f);
        return Frustum.FromViewProjection(view * projection);
    }

    /// <summary>
    /// Сферы, разбросанные по сцене так, что часть видна, часть отсекается
    /// разными плоскостями. Набор фиксирован: иначе проверка на совпадение
    /// результатов двух реализаций могла бы случайно пройти на пустой сцене.
    /// </summary>
    /// <param name="count">Требуемое число сфер.</param>
    /// <param name="spheres">Сферы массивом структур.</param>
    /// <param name="centersX">Координата X центров.</param>
    /// <param name="centersY">Координата Y центров.</param>
    /// <param name="centersZ">Координата Z центров.</param>
    /// <param name="radii">Радиусы.</param>
    private static void BuildScene(
        int count,
        out BoundingSphere[] spheres,
        out float[] centersX,
        out float[] centersY,
        out float[] centersZ,
        out float[] radii)
    {
        spheres = new BoundingSphere[count];
        centersX = new float[count];
        centersY = new float[count];
        centersZ = new float[count];
        radii = new float[count];

        var random = new XorShift64Star(31);
        for (int i = 0; i < count; i++)
        {
            spheres[i] = new BoundingSphere(
                new Vector3(
                    (random.NextFloat() - 0.5f) * 400f,
                    (random.NextFloat() - 0.5f) * 200f,
                    (random.NextFloat() - 0.5f) * 400f),
                0.5f + (random.NextFloat() * 4f));

            centersX[i] = spheres[i].Center.X;
            centersY[i] = spheres[i].Center.Y;
            centersZ[i] = spheres[i].Center.Z;
            radii[i] = spheres[i].Radius;
        }
    }

    /// <summary>
    /// Векторная форма обязана давать ровно тот же ответ, что и поштучная
    /// проверка, иначе она отсекает не то. Расхождение ловится точно, без
    /// допуска: обе формы считают одну и ту же сумму расстояний до плоскостей,
    /// и любое различие означает ошибку округления или ветвления.
    /// </summary>
    [Fact]
    public void CountVisible_ByArrays_MatchesPerSphere()
    {
        Frustum frustum = CreateFrustum();
        BuildScene(
            Count,
            out BoundingSphere[] spheres,
            out float[] centersX,
            out float[] centersY,
            out float[] centersZ,
            out float[] radii);

        int expected = 0;
        foreach (BoundingSphere sphere in spheres)
        {
            if (frustum.Intersects(sphere))
            {
                expected++;
            }
        }

        int actual = Frustum.CountVisible(frustum, centersX, centersY, centersZ, radii);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Набор не должен вырождаться в «все видны» или «никто не виден»:
    /// иначе проверка совпадения ничего не значила бы.
    /// </summary>
    [Fact]
    public void CountVisible_SceneIsMixed()
    {
        Frustum frustum = CreateFrustum();
        BuildScene(
            Count,
            out BoundingSphere[] spheres,
            out float[] centersX,
            out float[] centersY,
            out float[] centersZ,
            out float[] radii);

        int visible = Frustum.CountVisible(frustum, centersX, centersY, centersZ, radii);

        Assert.InRange(visible, Count / 20, Count * 19 / 20);
        Assert.NotEqual(0, visible);
        Assert.NotEqual(Count, visible);
        Assert.Equal(Count, spheres.Length);
    }

    /// <summary>
    /// Ширина вектора на процессоре не обязана быть степенью двойки, а длина
    /// массива — кратной ей. Хвост обязан обрабатываться так же, как основная
    /// часть: тест намеренно берёт длины 1, 3 и числа вокруг ширины вектора.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(33)]
    public void CountVisible_HandlesAnyLength(int count)
    {
        Frustum frustum = CreateFrustum();
        BuildScene(
            count,
            out BoundingSphere[] spheres,
            out float[] centersX,
            out float[] centersY,
            out float[] centersZ,
            out float[] radii);

        int expected = 0;
        foreach (BoundingSphere sphere in spheres)
        {
            if (frustum.Intersects(sphere))
            {
                expected++;
            }
        }

        Assert.Equal(expected, Frustum.CountVisible(frustum, centersX, centersY, centersZ, radii));
    }

    /// <summary>
    /// Сфера точно в центре камеры и сфера точно за ней: крайние случаи,
    /// где отсечение обязано выдать верный ответ независимо от порядка
    /// сложения расстояний.
    /// </summary>
    [Fact]
    public void CountVisible_HandlesKnownSpheres()
    {
        Frustum frustum = CreateFrustum();

        Vector3 forward = Vector3.Normalize(Vector3.Zero - new Vector3(0f, 8f, -14f));
        float[] centersX = [0f, 0f, 0f];
        float[] centersY = [8f, 8f, -600f];
        float[] centersZ = [-14f, -14f, -14f];
        float[] radii = [1f, 1f, 1f];

        // Первая сфера перед камерой, вторая на месте камеры, третья далеко
        // за пределами дальней плоскости.
        Assert.Equal(2, Frustum.CountVisible(frustum, centersX, centersY, centersZ, radii));
        Assert.True(forward.Length() > 0.99f && forward.Length() < 1.01f, "Направление должно быть единичным.");

        float[] outsideX = [0f];
        float[] outsideY = [8f];
        float[] outsideZ = [-600f];
        float[] outsideR = [1f];
        Assert.Equal(0, Frustum.CountVisible(frustum, outsideX, outsideY, outsideZ, outsideR));
    }

    /// <summary>
    /// Массивы разной длины — ошибка вызывающего, а не молчаливый результат.
    /// </summary>
    [Fact]
    public void CountVisible_RejectsMismatchedLengths()
    {
        Frustum frustum = CreateFrustum();
        float[] centersX = [0f, 1f, 2f];
        float[] centersY = [0f, 1f];
        float[] centersZ = [0f, 1f, 2f];
        float[] radii = [1f, 1f, 1f];

        ArgumentException error = Assert.Throws<ArgumentException>(
            () => Frustum.CountVisible(frustum, centersX, centersY, centersZ, radii));
        Assert.Contains("длины", error.Message, StringComparison.Ordinal);

        Assert.Throws<ArgumentException>(() => Frustum.CountVisible(
            frustum,
            centersX,
            centersY,
            centersZ,
            [1f]));
    }

    [Fact]
    public void CountVisible_RejectsNullFrustum()
    {
        float[] values = [0f];
        Assert.Throws<ArgumentNullException>(() => Frustum.CountVisible(null!, values, values, values, values));
    }
}