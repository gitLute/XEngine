using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Регрессионные тесты на дефекты, найденные при ревизии и описанные в
/// <c>Problems.md</c>: рассогласования контрактов, потеря значащих цифр и
/// поведение на нечисловых данных.
/// </summary>
/// <remarks>
/// Общая черта этих дефектов — не вычислительная ошибка, а молчаливое
/// расхождение между двумя путями к одному результату: соседние методы
/// отвечали на один вопрос по-разному, и наивная форма теряла точность там,
/// где результат сам по себе мал.
/// </remarks>
public class ContractConsistencyTests
{
    /// <summary>
    /// Пакетный отсекатель обязан переживать нечисловые данные уровня.
    /// Прежняя реализация собирала в хвосте <see cref="BoundingSphere"/>, чей
    /// конструктор отвергает нечисловой радиус, и одно <c>NaN</c> обрывало
    /// кадр исключением. Векторное тело при этом считало такую сферу
    /// видимой, то есть два пути к одному ответу расходились и по устойчивости.
    /// </summary>
    [Fact]
    public void FrustumBatch_SurvivesNonFiniteRadiusInTail()
    {
        Frustum frustum = BuildFrustum();
        int lanes = Vector<float>.Count;

        // Хвост начинается с индекса count - count % lanes, поэтому при длине
        // lanes + 1 последний элемент попадает именно в хвост, а при длине
        // lanes он остаётся в векторном теле.
        foreach (int count in new[] { lanes, lanes + 1, lanes + 2, (lanes * 2) + 1 })
        {
            float[] xs = new float[count];
            float[] ys = new float[count];
            float[] zs = new float[count];
            float[] radii = new float[count];
            for (int i = 0; i < count; i++)
            {
                xs[i] = 0f;
                ys[i] = 0f;
                zs[i] = -5f;
                radii[i] = 1f;
            }

            radii[count - 1] = float.NaN;

            int batched = Frustum.CountVisible(frustum, xs, ys, zs, radii);

            // Поштучный вызов для сравнения: Intersects считает расстояние
            // напрямую и NaN не приводит к исключению.
            int scalar = 0;
            for (int i = 0; i < count; i++)
            {
                Plane3[] planes = frustum.Planes.ToArray();
                bool visible = true;
                foreach (Plane3 plane in planes)
                {
                    float distance = ((plane.Normal.X * xs[i]) + (plane.Normal.Y * ys[i])) + ((plane.Normal.Z * zs[i]) + plane.Distance);
                    if ((distance + radii[i]) < -1e-3f)
                    {
                        visible = false;
                        break;
                    }
                }

                if (visible)
                {
                    scalar++;
                }
            }

            Assert.True(batched == scalar, $"При длине {count} пакетный счёт {batched} разошёлся с поштучным {scalar}.");
        }
    }

    /// <summary>
    /// Нечисловая координата не должна ронять пакетный отсекатель ни в теле,
    /// ни в хвосте.
    /// </summary>
    [Fact]
    public void FrustumBatch_SurvivesNonFiniteCoordinates()
    {
        Frustum frustum = BuildFrustum();
        int lanes = Vector<float>.Count;
        int count = lanes + 1;
        float[] xs = new float[count];
        float[] ys = new float[count];
        float[] zs = new float[count];
        float[] radii = new float[count];
        for (int i = 0; i < count; i++)
        {
            xs[i] = 0f;
            ys[i] = 0f;
            zs[i] = -5f;
            radii[i] = 1f;
        }

        xs[0] = float.NaN;
        ys[lanes] = float.PositiveInfinity;

        int visible = Frustum.CountVisible(frustum, xs, ys, zs, radii);
        Assert.True(visible >= 0 && visible <= count);
    }

    /// <summary>
    /// Оба пути пакетного отсечения обязаны совпадать на обычных данных, иначе
    /// выбор формы (векторная или поштучная) менял бы результат.
    /// </summary>
    [Fact]
    public void FrustumBatch_AoSAndSoAAgreeOnOrdinaryData()
    {
        Frustum frustum = BuildFrustum();
        int lanes = Vector<float>.Count;
        DeterministicRandom random = new(0x9FA0B1C2D3E4F506UL);

        for (int count = 1; count <= (lanes * 2) + 3; count++)
        {
            float[] xs = new float[count];
            float[] ys = new float[count];
            float[] zs = new float[count];
            float[] radii = new float[count];
            BoundingSphere[] spheres = new BoundingSphere[count];

            for (int i = 0; i < count; i++)
            {
                xs[i] = random.Range(-20f, 20f);
                ys[i] = random.Range(-10f, 10f);
                zs[i] = random.Range(-40f, 2f);
                radii[i] = random.Range(0.1f, 3f);
                spheres[i] = new BoundingSphere(new Vector3(xs[i], ys[i], zs[i]), radii[i]);
            }

            int soa = Frustum.CountVisible(frustum, xs, ys, zs, radii);
            int aos = Frustum.CountVisible(frustum, spheres.AsSpan());

            Assert.True(soa == aos, $"При длине {count} раздельные массивы дали {soa}, массив сфер {aos}.");
        }
    }

    /// <summary>
    /// Ненулевой вектор — это направление, и его угол обязан быть найден при
    /// любой длине. Прежняя проверка обнуляла направления короче
    /// <see cref="Scalar.Epsilon"/>, то есть превращала в ноль настоящие
    /// нормали физических тел.
    /// </summary>
    [Theory]
    [InlineData(1e-6f)]
    [InlineData(1e-7f)]
    [InlineData(1e-9f)]
    [InlineData(1e-20f)]
    public void AngleFromDirection_KeepsTinyDirections(float length)
    {
        Vector2 diagonal = new(length, length);
        Angle angle = Angle.FromDirection(diagonal);

        Assert.Equal(45.0, angle.Degrees, 0.05);

        // Направление обязано совпадать с нормализованным вектором: это один
        // и тот же вопрос, и отвечать на него по-разному нельзя.
        Vector2 normalized = diagonal.SafeNormalize();
        Assert.Equal(MathF.Atan2(normalized.Y, normalized.X), (float)angle.Radians, 1e-5f);
    }

    /// <summary>
    /// Нулевой вектор остаётся нулём: направления у него нет.
    /// </summary>
    [Fact]
    public void AngleFromDirection_ZeroVectorGivesZero()
    {
        Assert.Equal(Angle.Zero, Angle.FromDirection(Vector2.Zero));
        Assert.Equal(Angle.Zero, Vector2.Zero.ToAngle());
    }

    /// <summary>
    /// <c>ToAngle</c> трёхмерного вектора обязан давать тот же угол, что и
    /// <see cref="Angle.FromDirection"/> на горизонтальной проекции: две
    /// дороги к одному ответу.
    /// </summary>
    [Theory]
    [InlineData(0.001f)]
    [InlineData(1f)]
    [InlineData(1000f)]
    public void VectorToAngle_MatchesHorizontalProjection(float length)
    {
        Vector3 direction = new(length, 0.3f, -length * 0.7f);
        Vector2 flattened = new Vector2(direction.X, direction.Z);
        Angle fromProjection = Angle.FromDirection(flattened);

        Assert.Equal(0.0, Math.Abs(Angle.ShortestDelta(flattened.ToAngle(), fromProjection)), 1e-5);
    }

    /// <summary>
    /// Неположительный шаг не должен двигать угол в сторону, противоположную
    /// цели: знак шага вдруг задавал бы направление движения.
    /// </summary>
    [Fact]
    public void AngleMoveTowards_NonPositiveStepDoesNotMove()
    {
        Angle current = Angle.FromDegrees(0);
        Angle target = Angle.FromDegrees(90);

        Assert.Equal(current, Angle.MoveTowards(current, target, 0.0));
        Assert.Equal(current, Angle.MoveTowards(current, target, -1.0));
    }

    /// <summary>
    /// Пустой параллелепипед не должен давать NaN при расширении: его центр
    /// равен NaN, потому что границы переставлены.
    /// </summary>
    [Fact]
    public void Expand_OfEmptyBoxStaysEmptyAndFinite()
    {
        Aabb2 flat = Aabb2.Empty.Expand(new Vector2(1f, 1f));
        Aabb3 solid = Aabb3.Empty.Expand(new Vector3(1f, 1f, 1f));

        Assert.True(flat.IsEmpty, "Расширенный пустой AABB обязан остаться пустым.");
        Assert.True(solid.IsEmpty, "Расширенный пустой параллелепипед обязан остаться пустым.");

        Assert.False(float.IsNaN(flat.Min.X) || float.IsNaN(flat.Max.Y), "Границы расширенного пустого AABB не должны быть NaN.");
        Assert.False(
            float.IsNaN(solid.Min.X) || float.IsNaN(solid.Max.Y) || float.IsNaN(solid.Max.Z),
            "Границы расширенного пустого параллелепипеда не должны быть NaN.");
    }

    /// <summary>
    /// Два типа прямоугольников обязаны отвечать на один вопрос одинаково:
    /// пустой прямоугольник не содержится ни в чём, включая другой пустой.
    /// Иначе результат <c>Intersection</c>, возвращающий пустой прямоугольник
    /// при отсутствии пересечения, сам же себе противоречил бы.
    /// </summary>
    [Fact]
    public void RectAndRectUAgreeOnEmptyContainment()
    {
        Rect outer = new(0f, 0f, 10f, 10f);
        Rect emptyInsideOrigin = new(0f, 0f, 0f, 0f);

        Assert.True(outer.Contains(emptyInsideOrigin) == RectU.Empty.ContainsRect(RectU.Empty),
            "Rect и RectU обязаны одинаково трактовать пустой прямоугольник.");
        Assert.False(outer.Contains(Rect.Zero), "Пустой прямоугольник не содержится ни в чём.");
        Assert.False(Rect.Zero.Contains(Rect.Zero));

        Rect nonEmpty = new(1f, 1f, 2f, 2f);
        Assert.True(outer.Contains(nonEmpty));
        Assert.False(new Rect(0f, 0f, 1f, 1f).Contains(nonEmpty));
    }

    /// <summary>
    /// Отрицательный предел длины — ошибка вызывающего, а не повод
    /// переворачивать вектор: длина соблюдалась бы, направление нет.
    /// </summary>
    [Fact]
    public void ClampLength_NegativeLimitIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vector2(3f, 4f).ClampLength(-1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Vector3(3f, 4f, 0f).ClampLength(-1f));

        // Нулевой предел остаётся допустимым и даёт нулевой вектор.
        MathAssert.Equal(Vector2.Zero, new Vector2(3f, 4f).ClampLength(0f));
        Assert.Equal(Vector3.Zero, new Vector3(3f, 4f, 0f).ClampLength(0f));
    }

    /// <summary>
    /// Два имени одного ограничения обязаны вести себя одинаково, включая
    /// отказ на перепутанных границах.
    /// </summary>
    [Fact]
    public void Clamp_HasOneBehaviourUnderTwoNames()
    {
        Assert.Equal(Scalar.Clamp(5f, 0f, 10f), Interpolation.Clamp(5f, 0f, 10f));
        Assert.Equal(Scalar.Clamp(-5f, 0f, 10f), Interpolation.Clamp(-5f, 0f, 10f));

        Assert.Throws<ArgumentException>(() => Interpolation.Clamp(1f, 10f, 0f));
        Assert.Throws<ArgumentException>(() => Scalar.Clamp(1f, 10f, 0f));
    }

    /// <summary>
    /// Размер типа цвета обязан совпадать с тем, что обещает имя и доктрина:
    /// четыре канала по 4 байта.
    /// </summary>
    [Fact]
    public void Rgba32_LayoutMatchesItsDocumentedChannels()
    {
        Assert.Equal(16, System.Runtime.CompilerServices.Unsafe.SizeOf<Rgba32>());

        Vector4 white = Rgba32.White.ToVector4();
        Assert.Equal(1f, white.X);
        Assert.Equal(1f, white.Y);
        Assert.Equal(1f, white.Z);
        Assert.Equal(1f, white.W);
    }

    /// <summary>
    /// Третья компонента размера не должна влиять на повтор и не должна
    /// отвергаться: у текстуры ровно два измерения развёртки.
    /// </summary>
    [Fact]
    public void TexelDensity_IgnoresThirdComponentWithoutFailing()
    {
        Vector2 expected = TexelDensity.RepeatForSize(new Vector3(2f, 3f, 1f), 16f, new Vector2(64, 64));
        Vector2 ignored = TexelDensity.RepeatForSize(new Vector3(2f, 3f, float.NaN), 16f, new Vector2(64, 64));
        Vector2 negative = TexelDensity.RepeatForSize(new Vector3(2f, 3f, -5f), 16f, new Vector2(64, 64));

        Assert.Equal(expected, ignored);
        Assert.Equal(expected, negative);
    }

    /// <summary>
    /// Пирамида видимости для перспективной камеры, смотрящей вдоль −Z.
    /// </summary>
    /// <returns>Готовая пирамида.</returns>
    private static Frustum BuildFrustum() => Frustum.FromViewProjection(
        Matrix4x4Extensions.CreateViewProjection(
            Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, -1f), Vector3.UnitY),
            Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(60f), 1f, 0.1f, 100f)));

    /// <summary>
    /// Перенос диапазонов на вырожденном исходном диапазоне не бросает
    /// исключение, а даёт ближний конец целевого.
    /// </summary>
    /// <remarks>
    /// Раньше оба метода пробрасывали исключение из <c>InverseLerp</c>, причём
    /// в <c>ParamName</c> стояло имя параметра <c>from</c>, которого у
    /// <c>Remap</c> нет. Исключение приходило из вызываемого метода, поэтому в
    /// сигнатуре оно не читалось, а вырожденные границы получаются именно там,
    /// где вырождены данные: у уровня с одним типом врага нижняя и верхняя
    /// границы здоровья совпадают.
    /// </remarks>
    [Fact]
    public void Remap_OnEmptySourceRange_ReturnsTargetMinimum()
    {
        MathAssert.Equal(0f, Interpolation.Remap(1f, 5f, 5f, 0f, 10f), 1e-5f);
        MathAssert.Equal(0f, Interpolation.RemapUnclamped(1f, 5f, 5f, 0f, 10f), 1e-5f);
        MathAssert.Equal(-3f, Interpolation.Remap(7f, 5f, 5f, -3f, 9f), 1e-5f);
        MathAssert.Equal(-3f, Interpolation.RemapUnclamped(7f, 5f, 5f, -3f, 9f), 1e-5f);

        // Диапазон шире, чем отбрасываемый порог, но узкий: раньше тоже падал.
        const float Narrow = 1e-7f;
        MathAssert.Equal(0f, Interpolation.Remap(5f, 5f, 5f + Narrow, 0f, 10f), 1e-5f);
        MathAssert.Equal(0f, Interpolation.RemapUnclamped(5f, 5f, 5f + Narrow, 0f, 10f), 1e-5f);

        // Нечисловые границы по-прежнему дают нечисловой результат: NaN — это
        // вход, а не пустой диапазон.
        Assert.True(float.IsNaN(Interpolation.Remap(1f, float.NaN, 10f, 0f, 10f)), "NaN должен проходить насквозь.");

        // Вырожденный целевой диапазон поведения не меняет: это toMin при любом t.
        MathAssert.Equal(5f, Interpolation.Remap(1f, 0f, 10f, 5f, 5f), 1e-5f);

        // Исключение InverseLerp остаётся: там деление на ноль действительно
        // ошибка вызывающего, и её видно сразу.
        Assert.Throws<ArgumentException>(() => Interpolation.InverseLerp(5f, 5f, 1f));

        // Обычные случаи не задеты.
        MathAssert.Equal(50f, Interpolation.Remap(5f, 0f, 10f, 0f, 100f), 1e-4f);
        MathAssert.Equal(-50f, Interpolation.RemapUnclamped(-5f, 0f, 10f, 0f, 100f), 1e-4f);
        MathAssert.Equal(150f, Interpolation.RemapUnclamped(15f, 0f, 10f, 0f, 100f), 1e-4f);
    }
}
