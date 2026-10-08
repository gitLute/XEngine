using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Ветви проверок ввода и редкие ветки вычислений, которые раньше не
/// выполнялись.
/// </summary>
/// <remarks>
/// Здесь нет сложных алгоритмов, но есть контракты, которые не были проверены
/// ни разу: отказ на неверном вводе, вырожденные аргументы, насыщение.
/// Каждый случай проверяется по своему определению, а не по совпадению с
/// другим методом.
/// </remarks>
public class GuardAndEdgeCaseTests
{
    private const float Tolerance = 1e-4f;

    private static Vector3 P(float x, float y, float z) => new(x, y, z);

    #region Плоскость и нормали

    /// <summary>
    /// Нулевая нормаль не задаёт плоскость: это ошибка вызывающего, а не
    /// плоскость в бесконечности.
    /// </summary>
    [Fact]
    public void Plane3_RejectsZeroNormal()
    {
        Assert.Throws<ArgumentException>(() => Plane3.FromPointNormal(Vector3.Zero, Vector3.Zero));
        Assert.Throws<ArgumentException>(() => Plane3.FromCoefficients(Vector3.Zero, 1f));
        Assert.Throws<ArgumentException>(() => new Plane3(Vector3.Zero, 1f));

        // Ненулевая нормаль любой длины принимается и нормализуется.
        Plane3 plane = Plane3.FromPointNormal(Vector3.Zero, new Vector3(3f, 4, 0));
        MathAssert.Equal(1f, plane.Normal.Length(), Tolerance);
    }

    #endregion

    #region Матрицы

    [Fact]
    public void CreateLookAt_RejectsZeroUp()
    {
        Assert.Throws<ArgumentException>(() => Matrix4x4Extensions.CreateLookAt(Vector3.Zero, Vector3.One, Vector3.Zero));
        Assert.Throws<ArgumentException>(() => Matrix4x4Extensions.CreateLookAt(Vector3.Zero, Vector3.One, new Vector3(0f, 0f, 0f)));
    }

    /// <summary>
    /// Нормаль нельзя преобразовать вырожденной матрицей: обратной матрицы
    /// не существует, и результат был бы молчаливой пустотой.
    /// </summary>
    [Fact]
    public void TransformNormal_RejectsSingularMatrix()
    {
        Matrix4x4 singular = Matrix4x4.CreateScale(0f, 0f, 0f);

        Assert.Throws<InvalidOperationException>(() => singular.TransformNormal(Vector3.UnitY));
        Assert.False(Matrix4x4Extensions.TryInvert(singular, out _));
    }

    /// <summary>
    /// Матрица 3x2 вырождена, когда её определитель равен нулю: строки или
    /// столбцы линейно зависимы.
    /// </summary>
    [Fact]
    public void Matrix3x2_TryInvert_ReportsSingular()
    {
        Matrix3x2 singular = new(1f, 2f, 2f, 4f, 3f, 6f);

        Assert.False(singular.TryInvert(out Matrix3x2 inverse));
        Assert.Equal(Matrix3x2.Identity, inverse);

        Assert.False(default(Matrix3x2).TryInvert(out _));
    }

    #endregion

    #region Кватернионы и векторы

    /// <summary>
    /// Обратного элемента у нулевого кватерниона нет: метод обязан сказать
    /// об этом, а не вернуть молчаливую пустоту.
    /// </summary>
    [Fact]
    public void Inverse_RejectsZeroQuaternion()
    {
        Assert.Throws<ArgumentException>(() => QuaternionExtensions.Inverse(default(Quaternion)));
        Assert.Throws<ArgumentException>(() => QuaternionExtensions.Inverse(Quaternion.Zero));
    }

    /// <summary>
    /// Проекция на плоскость и отражение от неё требуют нормаль: нулевой
    /// вектор не задаёт ни одной плоскости, и результат был бы молчаливой
    /// пустотой.
    /// </summary>
    [Fact]
    public void PlaneProjection_RejectsZeroNormal()
    {
        Assert.Throws<ArgumentException>(() => Vector3.Zero.ProjectOntoPlane(Vector3.Zero));
        Assert.Throws<ArgumentException>(() => Vector3.Zero.RejectFromPlane(Vector3.Zero));
        Assert.Throws<ArgumentException>(() => P(1, 2, 3).ProjectOntoPlane(new Vector3(0f, 0f, 0f)));
        Assert.Throws<ArgumentException>(() => P(1, 2, 3).RejectFromPlane(new Vector3(0f, 0f, 0f)));
    }

    /// <summary>
    /// Точка опоры вращения и подсказка перпендикуляра нулевыми быть могут:
    /// первая задаёт положение, вторая лишь подсказывает, какое направление
    /// взять. Оба метода обязаны вернуть осмысленный результат.
    /// </summary>
    [Fact]
    public void Rotation_AcceptsZeroPivotAndHint()
    {
        // Вращение вокруг начала координат тождественно, точка не меняется.
        MathAssert.Equal(P(1, 2, 3), P(1, 2, 3).RotateAround(Vector3.Zero, Quaternion.Identity), Tolerance);

        // Поворот на четверть вокруг оси Z: точка на оси неподвижна.
        Vector3 onAxis = P(0f, 0f, 5f);
        MathAssert.Equal(onAxis, onAxis.RotateAround(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f)), Tolerance);

        // Перпендикуляр при нулевой подсказке всё равно перпендикулярен.
        Vector3 perpendicular = Vector3.UnitZ.Perpendicular(Vector3.Zero);
        Assert.True(perpendicular.Length() > 0.9f, "Перпендикуляр выродился в ноль.");
        Assert.True(MathF.Abs(Vector3.Dot(Vector3.UnitZ, perpendicular)) < 1e-4f, "Результат не перпендикулярен.");
    }

    /// <summary>
    /// Перпендикуляр строится по наименьшей по модулю компоненте, потому что
    /// вектор от неё наименее вырожден. Проверяется, что результат
    /// действительно перпендикулярен и ненулевой.
    /// </summary>
    [Fact]
    public void Perpendicular_IsOrthogonalAndNonZero()
    {
        var random = new XorShift64Star(777);

        for (int i = 0; i < 500; i++)
        {
            Vector3 direction = Vector3.Normalize(new Vector3(
                (random.NextFloat() - 0.5f) * 2f,
                (random.NextFloat() - 0.5f) * 2f,
                (random.NextFloat() - 0.5f) * 2f));

            Vector3 perpendicular = direction.Perpendicular(new Vector3(1f, 0f, 0f));

            Assert.True(Vector3.Dot(direction, perpendicular) is > -1e-4f and < 1e-4f, "Вектор не перпендикулярен.");
            Assert.True(perpendicular.Length() > 0.1f, "Перпендикуляр выродился в ноль.");
        }

        // Ось, совпадающая с подсказкой: подсказка отбрасывается, и результат
        // всё равно перпендикулярен исходному вектору.
        foreach (Vector3 basis in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
        {
            Vector3 perpendicular = basis.Perpendicular(basis);
            Assert.True(perpendicular.Length() > 0.9f, $"Перпендикуляр к {basis} выродился.");
            Assert.True(MathF.Abs(Vector3.Dot(basis, perpendicular)) < 1e-4f, "Результат не перпендикулярен.");
        }
    }

    #endregion

    #region Интерполяция

    /// <summary>
    /// Период обязан быть положительным: при нулевом или отрицательном
    /// значении перенос не определён.
    /// </summary>
    [Fact]
    public void RepeatAndPingPong_RejectNonPositivePeriod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Interpolation.Repeat(1f, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Interpolation.Repeat(1f, -1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Interpolation.PingPong(1f, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Interpolation.PingPong(1f, -1f));
    }

    /// <summary>
    /// Сглаживание не выходит за целевое значение и останавливается на нём:
    /// иначе камера проезжает сквозь цель и возвращается.
    /// </summary>
    [Fact]
    public void SmoothDamp_StopsAtTargetWithoutOvershoot()
    {
        float velocity = 0f;
        float value = 0f;

        for (int i = 0; i < 2000; i++)
        {
            value = Interpolation.SmoothDamp(value, 10f, 0.3f, float.MaxValue, 1f / 60f, ref velocity);
            Assert.True(value <= 10f + Tolerance, $"Значение ушло за цель: {value} на шаге {i}.");
        }

        Assert.True(float.IsFinite(value), "Сглаживание сошлось к нечисловому значению.");
        Assert.InRange(value, 9.99f, 10.01f);

        // Отрицательное направление работает так же.
        velocity = 0f;
        value = 0f;
        for (int i = 0; i < 2000; i++)
        {
            value = Interpolation.SmoothDamp(value, -10f, 0.3f, float.MaxValue, 1f / 60f, ref velocity);
            Assert.True(value >= -10f - Tolerance, $"Значение ушло за цель: {value} на шаге {i}.");
        }

        Assert.InRange(value, -10.01f, -9.99f);
    }

    /// <summary>
    /// Сглаживание с ограничением максимальной скорости не может превысить её
    /// ни на одном шаге.
    /// </summary>
    [Fact]
    public void SmoothDamp_RespectsMaxSpeed()
    {
        const float maxSpeed = 5f;
        float velocity = 0f;
        float value = 0f;
        float previous = 0f;

        for (int i = 0; i < 500; i++)
        {
            value = Interpolation.SmoothDamp(value, 100f, 0.5f, maxSpeed, 1f / 60f, ref velocity);
            float step = MathF.Abs(value - previous);
            Assert.InRange(step, 0f, (maxSpeed / 60f) + Tolerance + 1e-3f);
            previous = value;
        }
    }

    #endregion

    #region Случайность

    /// <summary>
    /// Отрицательный вес не имеет смысла и обязан отвергаться, а не
    /// молча портить распределение.
    /// </summary>
    [Fact]
    public void NextWeightedIndex_RejectsNegativeWeights()
    {
        var random = new XorShift64Star(11);

        Assert.Throws<ArgumentException>(() => random.NextWeightedIndex([-1f, 2f, 3f]));
        Assert.Throws<ArgumentException>(() => random.NextWeightedIndex([1f, -0.5f]));
        Assert.Throws<ArgumentException>(() => random.NextWeightedIndex([0f, 0f]));
        Assert.Throws<ArgumentException>(() => random.NextWeightedIndex([]));
    }

    /// <summary>
    /// Веса с нулём выбираются никогда: при нулевом весе вариант не может
    /// выпасть ни при каком пороге.
    /// </summary>
    [Fact]
    public void NextWeightedIndex_NeverPicksZeroWeight()
    {
        var random = new XorShift64Star(22);
        int[] counts = new int[4];
        float[] weights = [0f, 1f, 0f, 3f];

        for (int i = 0; i < 100_000; i++)
        {
            counts[random.NextWeightedIndex(weights)]++;
        }

        Assert.Equal(0, counts[0]);
        Assert.Equal(0, counts[2]);

        // Распределение пропорционально весам: 1 к 3.
        double ratio = (double)counts[3] / counts[1];
        Assert.InRange(ratio, 2.95, 3.05);
    }

    #endregion

    #region Цвет

    /// <summary>
    /// Некорректный hex — ошибка формата с указанием исходной строки.
    /// </summary>
    [Fact]
    public void Rgba32_RejectsMalformedHex()
    {
        foreach (string text in new[] { "не цвет", "#GG0000", "#00FF0", "#", "#1234567" })
        {
            FormatException error = Assert.Throws<FormatException>(() => Rgba32.FromHex(text));
            Assert.Contains(text, error.Message, StringComparison.Ordinal);
        }

        ArgumentNullException nullError = Assert.Throws<ArgumentNullException>(() => Rgba32.FromHex(null!));
        Assert.Equal("hex", nullError.ParamName);
    }

    /// <summary>
    /// Именованные цвета и замена прозрачности.
    /// </summary>
    [Fact]
    public void Rgba32_NamedColorsAndAlpha()
    {
        MathAssert.Equal(1f, Rgba32.Red.R, 1e-4f);
        MathAssert.Equal(0f, Rgba32.Red.G, 1e-4f);
        MathAssert.Equal(0f, Rgba32.Red.B, 1e-4f);

        MathAssert.Equal(0f, Rgba32.Green.R, 1e-4f);
        MathAssert.Equal(1f, Rgba32.Green.G, 1e-4f);

        MathAssert.Equal(0f, Rgba32.Blue.R, 1e-4f);
        MathAssert.Equal(0f, Rgba32.Blue.G, 1e-4f);
        MathAssert.Equal(1f, Rgba32.Blue.B, 1e-4f);

        MathAssert.Equal(1f, Rgba32.Yellow.R, 1e-4f);
        MathAssert.Equal(1f, Rgba32.Yellow.G, 1e-4f);
        MathAssert.Equal(0f, Rgba32.Yellow.B, 1e-4f);

        Rgba32 opaque = new Rgba32(0.25f, 0.5f, 0.75f, 1f);
        Rgba32 half = opaque.WithAlpha(0.5f);

        MathAssert.Equal(opaque.R, half.R, 1e-4f);
        MathAssert.Equal(opaque.G, half.G, 1e-4f);
        MathAssert.Equal(opaque.B, half.B, 1e-4f);
        MathAssert.Equal(0.5f, half.A, 1e-4f);

        // Именованные цвета непрозрачны.
        Assert.InRange(Rgba32.Red.A, 0.99f, 1f);
    }

    #endregion

    #region Капсула и объединения

    [Fact]
    public void Capsule2_RejectsNegativeRadiusAndExposesSurface()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Capsule2(new Segment2(Vector2.Zero, Vector2.One), -1f));

        Capsule2 capsule = new(new Segment2(new Vector2(0, 0), new Vector2(1, 0)), 0.5f);
        Vector3_AssertSurface(capsule, new Vector2(0.5f, 2f));
        Vector3_AssertSurface(capsule, new Vector2(0.5f, 0f));
        Vector3_AssertSurface(capsule, new Vector2(-3f, 0f));
    }

    private static void Vector3_AssertSurface(Capsule2 capsule, Vector2 point)
    {
        Vector2 axisPoint = capsule.Segment2.ClosestPointTo(point);
        float distance = Vector2.Distance(point, axisPoint);

        Assert.Equal(distance <= capsule.Radius + Tolerance, capsule.Contains(point));
    }

    /// <summary>
    /// Объединение параллелепипедов берёт границы по обеим сторонам, а не
    /// одну из них.
    /// </summary>
    [Fact]
    public void Aabb_UnionTakesBothExtremes()
    {
        Aabb2 first = new(new Vector2(-1, 5), new Vector2(2, 6));
        Aabb2 second = new(new Vector2(3, -7), new Vector2(4, 0));
        Aabb2 union = first.Union(second);

        MathAssert.Equal(new Vector2(-1, -7), union.Min, Tolerance);
        MathAssert.Equal(new Vector2(4, 6), union.Max, Tolerance);

        Aabb3 first3 = new(P(-1, 5, 0), P(2, 6, 1));
        Aabb3 second3 = new(P(3, -7, 2), P(4, 0, 3));
        Aabb3 union3 = first3.Union(second3);

        MathAssert.Equal(P(-1, -7, 0), union3.Min, Tolerance);
        MathAssert.Equal(P(4, 6, 3), union3.Max, Tolerance);
    }

    /// <summary>
    /// Параллелепипед из одной точки не пуст: равенства границ это вырожденный
    /// объём, а не пустое множество.
    /// </summary>
    [Fact]
    public void Aabb_FromSinglePoint_IsPointNotEmpty()
    {
        Aabb2 point2 = new(P2(3, 3), P2(3, 3));
        MathAssert.Equal(P2(3, 3), point2.Min, Tolerance);
        MathAssert.Equal(P2(3, 3), point2.Max, Tolerance);
        Assert.False(point2.IsEmpty, "Точка не должна считаться пустым параллелепипедом.");

        Aabb3 point3 = new(P(1, 2, 3), P(1, 2, 3));
        MathAssert.Equal(P(1, 2, 3), point3.Min, Tolerance);
        MathAssert.Equal(P(1, 2, 3), point3.Max, Tolerance);
        Assert.False(point3.IsEmpty, "Точка не должна считаться пустым параллелепипедом.");
    }

    private static Vector2 P2(float x, float y) => new(x, y);

    #endregion

    #region Крайние случаи тригонометрии

    /// <summary>
    /// Экспонента и степень двойки обязаны давать бесконечность и ноль на
    /// краях диапазона, а не NaN. Ветки переполнения и ненормальных чисел
    /// детерминированной реализации проверяются здесь же: оба варианта
    /// сборки обязаны вести себя одинаково на границах.
    /// </summary>
    [Fact]
    public void ExpAndPow2_SaturateWithoutNaN()
    {
        Assert.Equal(float.PositiveInfinity, Trig.Exp(1000f));
        Assert.Equal(0f, Trig.Exp(-1000f));
        Assert.Equal(float.PositiveInfinity, Trig.Pow2(10000f));
        Assert.Equal(0f, Trig.Pow2(-10000f));

        // Неопределённость на входе остаётся неопределённостью на выходе.
        Assert.True(float.IsNaN(Trig.Exp(float.NaN)));

        // Пределы по бесконечности: exp(+∞) переполняется, exp(−∞) обнуляется.
        // Именно ноль, а не NaN: неопределённости здесь нет, есть предел.
        Assert.True(float.IsPositiveInfinity(Trig.Exp(float.PositiveInfinity)));
        Assert.Equal(0f, Trig.Exp(float.NegativeInfinity));
        Assert.Equal(0f, Trig.Pow2(float.NegativeInfinity));
        Assert.True(float.IsPositiveInfinity(Trig.Pow2(float.PositiveInfinity)));
        Assert.True(float.IsNaN(Trig.Pow2(float.NaN)));

        // Область вблизи нуля, где результат мал, но ещё не обнулился:
        // exp(-100) около 3.8·10⁻⁴⁴, что больше наименьшего ненормального
        // числа 1.4·10⁻⁴⁵ и намного больше нуля.
        float tiny = Trig.Exp(-100f);
        Assert.InRange(tiny, 1e-45f, 1e-42f);
        Assert.True(tiny > 0f, "Экспонента обнулилась раньше, чем должна была.");

        // При этом чуть глубже результат обязан стать ровно нулём, а не
        // ненормальным числом: оба варианта сборки ведут себя одинаково.
        Assert.Equal(0f, Trig.Exp(-200f));

        // Нормальный диапазон обязан оставаться точным.
        MathAssert.Equal(1f, Trig.Exp(0f), 1e-6f);
        MathAssert.Equal((float)Math.E, Trig.Exp(1f), 1e-5f);
        MathAssert.Equal(1f / (float)Math.E, Trig.Exp(-1f), 1e-6f);
        MathAssert.Equal(2f, Trig.Pow2(1f), 1e-6f);
        MathAssert.Equal(0.5f, Trig.Pow2(-1f), 1e-6f);
        MathAssert.Equal(1f, Trig.Pow2(0f), 1e-6f);
    }

    /// <summary>
    /// Тангенс у своей асимптоты обязан давать бесконечность со знаком, а не
    /// NaN: cos там ноль, и наивное деление дало бы 0/0.
    /// </summary>
    [Fact]
    public void Tan_NearPoleDoesNotProduceNaN()
    {
        Assert.False(float.IsNaN(Trig.Tan(MathF.PI * 0.5f)));
        Assert.True(MathF.Abs(Trig.Tan(MathF.PI * 0.5f)) > 1e6f, "Тангенс у полюси не уходит в бесконечность.");

        // В нуле и около него тангенс конечен.
        MathAssert.Equal(0f, Trig.Tan(0f), 1e-6f);
        MathAssert.Equal(1f, Trig.Tan(MathF.PI * 0.25f), 1e-4f);
    }

    /// <summary>
    /// Вращение кватернионом в блокировке первого и второго порядка: при
    /// наклоне ровно на 90° курс и тангаж вырождаются, и разбор Эйлера обязан
    /// вернуть те же углы, что дали поворот.
    /// </summary>
    [Fact]
    public void ToEuler_HandlesGimbalLockRoundTrip()
    {
        foreach (float pitch in new[] { 90f, -90f })
        {
            Quaternion rotation = QuaternionExtensions.FromEuler(Angle.FromDegrees(37f), Angle.FromDegrees(pitch), Angle.FromDegrees(0f));
            (Angle yaw, Angle pitchOut, Angle roll) = QuaternionExtensions.ToEuler(rotation);

            // При блокировке курс и тангаж не определяются по отдельности, но
            // поворот обязан воспроизводиться: это и проверяется.
            Quaternion restored = QuaternionExtensions.FromEuler(yaw, pitchOut, roll);
            MathAssert.Equal(rotation.Rotate(Vector3.UnitZ), restored.Rotate(Vector3.UnitZ), 1e-3f);
        }
    }

    #endregion

    #region Отсечение

    /// <summary>
    /// Параллелепипед, у которого ближняя грань за ближней плоскостью, не
    /// виден: метод обязан вернуть false.
    /// </summary>
    [Fact]
    public void Frustum_Contains_RejectsBoxOutsideNearPlane()
    {
        Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0, 0, 0), new Vector3(0, 0, -1), Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, 1f, 0.1f, 100f);
        Frustum frustum = Frustum.FromViewProjection(view * projection);

        // Далеко за камерой: полностью вне пирамиды.
        Aabb3 behind = new(new Vector3(-1, -1, -200), new Vector3(1, 1, -199));
        Assert.False(frustum.Contains(behind), "Параллелепипед за камерой не может быть целиком внутри.");
        Assert.False(frustum.Intersects(behind), "Параллелепипед за камерой не может пересекаться.");

        // Перед камерой и целиком внутри.
        Aabb3 inFront = new(new Vector3(-1, -1, -10), new Vector3(1, 1, -9));
        Assert.True(frustum.Contains(inFront));
        Assert.True(frustum.Intersects(inFront));
    }

    #endregion
}