using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Тесты скалярных операций, ветвей исключений и методов-обёрток, которые
/// раньше не вызывались.
/// </summary>
/// <remarks>
/// Здесь нет сложных алгоритмов, но есть контракты, которые никто не проверял:
/// знак числа, округление до шага, поведение при неверном вводе. Эталон
/// считается независимо: через <see cref="Math.Sign(double)"/>, через деление с
/// округлением в двойной точности и через определение диапазона.
/// </remarks>
public class ScalarAndGuardTests
{
    private const float Tolerance = 1e-4f;

    #region Scalar

    /// <summary>
    /// Знак числа определяется знаком самого числа, а ноль даёт ноль.
    /// </summary>
    [Theory]
    [InlineData(0f, 0)]
    [InlineData(1f, 1)]
    [InlineData(-1f, -1)]
    [InlineData(0.0001f, 1)]
    [InlineData(-0.0001f, -1)]
    [InlineData(float.MaxValue, 1)]
    [InlineData(float.MinValue, -1)]
    [InlineData(1e-30f, 1)]
    public void Sign_FollowsDefinition(float value, int expected)
        => Assert.Equal(expected, Scalar.Sign(value));

    /// <summary>
    /// Знак совпадает со знаком в двойной точности — это независимое
    /// определение, а не та же формула.
    /// </summary>
    [Fact]
    public void Sign_MatchesDoublePrecision()
    {
        var random = new XorShift64Star(1234);

        for (int i = 0; i < 200_000; i++)
        {
            float value = (random.NextFloat() - 0.5f) * 1000f;
            if (random.NextInt(0, 50) == 0)
            {
                value = random.NextFloat() < 0.5f ? 0f : -0f;
            }

            Assert.Equal(Math.Sign((double)value), Scalar.Sign(value));
        }
    }

    /// <summary>
    /// Близость к нулю: значение не больше допуска по модулю.
    /// </summary>
    [Theory]
    [InlineData(0f, true)]
    [InlineData(1e-7f, true)]
    [InlineData(-1e-7f, true)]
    [InlineData(Scalar.Epsilon, true)]
    [InlineData(-Scalar.Epsilon, true)]
    [InlineData(1e-5f, false)]
    [InlineData(-1e-5f, false)]
    [InlineData(1f, false)]
    public void IsNearlyZero_UsesAbsoluteTolerance(float value, bool expected)
        => Assert.Equal(expected, Scalar.IsNearlyZero(value));

    /// <summary>
    /// Равенство с допуском: разность по модулю не превышает допуск.
    /// </summary>
    [Theory]
    [InlineData(1f, 1f, Scalar.Epsilon, true)]
    [InlineData(1f, 1.0000005f, Scalar.Epsilon, true)]
    [InlineData(1f, 1.001f, Scalar.Epsilon, false)]
    [InlineData(1f, 0.999f, Scalar.Epsilon, false)]
    [InlineData(-1f, -1.0000005f, Scalar.Epsilon, true)]
    [InlineData(1f, 1.01f, 0.1f, true)]
    public void IsNearlyEqual_UsesAbsoluteDifference(float a, float b, float epsilon, bool expected)
        => Assert.Equal(expected, Scalar.IsNearlyEqual(a, b, epsilon));

    /// <summary>
    /// Приведение к шагу: результат кратен шагу и отличается от исходного
    /// не более чем на половину шага плюс округление.
    /// </summary>
    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(0.4f, 1f)]
    [InlineData(0.6f, 1f)]
    [InlineData(1.5f, 1f)]
    [InlineData(-1.5f, 1f)]
    [InlineData(7.3f, 0.5f)]
    [InlineData(7.3f, 2f)]
    [InlineData(0.1f, 0.1f)]
    [InlineData(123.456f, 0.01f)]
    public void Snap_RoundsToNearestMultiple(float value, float step)
    {
        float actual = Scalar.Snap(value, step);

        // Результат кратен шагу с точностью до округления.
        Assert.True(MathF.Abs((actual / step) - MathF.Round(actual / step)) < 1e-4f, "Результат не кратен шагу.");

        // Отличие от исходного не больше половины шага.
        Assert.True(MathF.Abs(actual - value) <= (step * 0.5f) + Tolerance, "Отличие больше половины шага.");

        // Эталон: округление половины от нуля, как в Math.Round, но в двойной.
        double reference = Math.Round((double)value / step, MidpointRounding.AwayFromZero) * step;
        MathAssert.Equal((float)reference, actual, Tolerance);
    }

    [Fact]
    public void Snap_RejectsNonPositiveStep()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Scalar.Snap(1f, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Scalar.Snap(1f, -1f));
    }

    /// <summary>
    /// Ограничение диапазоном: значение внутри не меняется, вне подтягивается
    /// к границе.
    /// </summary>
    [Theory]
    [InlineData(5f, 0f, 10f, 5f)]
    [InlineData(-1f, 0f, 10f, 0f)]
    [InlineData(11f, 0f, 10f, 10f)]
    [InlineData(0f, 0f, 10f, 0f)]
    [InlineData(10f, 0f, 10f, 10f)]
    [InlineData(-5f, -10f, -1f, -5f)]
    [InlineData(3f, 3f, 3f, 3f)]
    public void Clamp_KeepsValueInsideRange(float value, float min, float max, float expected)
        => Assert.Equal(expected, Scalar.Clamp(value, min, max));

    [Fact]
    public void Clamp_RejectsInvertedRange()
    {
        Assert.Throws<ArgumentException>(() => Scalar.Clamp(1f, 10f, 0f));
        Assert.Throws<ArgumentException>(() => Scalar.Clamp(1f, 1f, 0f));
    }

    /// <summary>
    /// Перевод градусов в радианы и обратно обязан воспроизводить исходное
    /// значение, иначе угол ползёт при каждом сохранении.
    /// </summary>
    [Fact]
    public void DegreesAndRadians_RoundTrip()
    {
        var random = new XorShift64Star(8642);

        for (int i = 0; i < 100_000; i++)
        {
            double degrees = (random.NextFloat() - 0.5f) * 720.0;
            double radians = Scalar.ToRadians(degrees);

            Assert.True(Math.Abs(radians - (degrees * Math.PI / 180.0)) < 1e-12, "Перевод в радианы отличается от формулы.");
            Assert.True(Math.Abs(Scalar.ToDegrees(radians) - degrees) < 1e-9, "Обратный перевод не воспроизводит значение.");
        }

        Assert.Equal(0.0, Scalar.ToRadians(0.0));
        Assert.Equal(180.0, Scalar.ToDegrees(Math.PI), 1e-9);
        Assert.Equal(Math.PI, Scalar.ToRadians(180.0), 1e-12);
    }

    #endregion

    #region Методы-обёртки: один вход на метод

    /// <summary>
    /// Обёртки обязаны вести себя как вызываемая под ними операция: иначе
    /// метод выглядит рабочим, а ведёт себя иначе.
    /// </summary>
    [Fact]
    public void Wrappers_BehaveLikeTheirTargets()
    {
        Vector2 vector = new(3, 4);
        Angle quarter = Angle.FromDegrees(90f);

        // Поворот вектора расширением и методом угла дают одно и то же.
        MathAssert.Equal(quarter.Rotate(vector), vector.Rotate(quarter), 1e-4f);

        // Назначение направления расширением.
        Vector2 rotated = vector.WithDirection(quarter);
        MathAssert.Equal(quarter.Direction * vector.Length(), rotated, 1e-4f);

        // Линейная интерполяция расширением и через System.Numerics.
        MathAssert.Equal(Vector2.Lerp(Vector2.Zero, vector, 0.3f), Vector2.Zero.LerpTo(vector, 0.3f), 1e-4f);

        // Расстояние между точками расширением и через системный тип.
        MathAssert.Equal(Vector2.Distance(Vector2.Zero, vector), Collision.Distance(Vector2.Zero, vector), 1e-4f);

        // Проекция луча на направление и скалярное произведение.
        Ray2 ray = new(Vector2.Zero, new Vector2(0, 1));
        MathAssert.Equal(Vector2.Dot(new Vector2(2, 3), ray.Direction), ray.ProjectOntoDirection(new Vector2(2, 3)), 1e-4f);

        // Полуразмер параллелепипеда: половина размера.
        Aabb2 box = new(new Vector2(-2, -4), new Vector2(6, 0));
        MathAssert.Equal(box.Size * 0.5f, box.HalfSize, 1e-4f);

        // Угол между двумя углами есть разность по кратчайшей дуге.
        Angle from = Angle.FromDegrees(10f);
        Angle to = Angle.FromDegrees(350f);
        MathAssert.Equal(from, Angle.FromDegrees(10f), 1e-4);
        Assert.True(
            Math.Abs(Angle.Between(from, to) - Angle.ShortestDelta(from, to)) < 1e-12,
            "Between и ShortestDelta разошлись.");

        // Окружность и отрезок: диаметр и середина.
        Circle2 circle = new(new Vector2(1, 2), 3f);
        MathAssert.Equal(6f, circle.Diameter, 1e-4f);
        MathAssert.Equal(3f, circle.Bounds.HalfSize.X, 1e-4f);

        Segment2 segment = new(new Vector2(0, 0), new Vector2(4, 8));
        MathAssert.Equal(segment.A + (segment.Delta * 0.5f), segment.Midpoint, 1e-4f);
    }

    /// <summary>
    /// Перенос прямоугольника по вектору смещает только положение.
    /// </summary>
    [Fact]
    public void RectOffset_MovesPositionOnly()
    {
        Rect rect = new(1f, 2f, 3f, 4f);
        Rect moved = rect.Offset(new Vector2(10f, -5f));

        MathAssert.Equal(11f, moved.X, Tolerance);
        MathAssert.Equal(-3f, moved.Y, Tolerance);
        MathAssert.Equal(3f, moved.Width, Tolerance);
        MathAssert.Equal(4f, moved.Height, Tolerance);
    }

    /// <summary>
    /// Случайные величины: симметричный диапазон и точка на окружности.
    /// </summary>
    [Fact]
    public void RandomHelpers_ProduceExpectedRanges()
    {
        var random = new XorShift64Star(2468);

        for (int i = 0; i < 100_000; i++)
        {
            float symmetric = random.NextSymmetric(3f);
            Assert.InRange(symmetric, -3f, 3f);

            Vector2 direction = random.NextDirection();
            MathAssert.Equal(1f, direction.Length(), 1e-4f);

            Vector2 onCircle = random.NextOnUnitCircle();
            MathAssert.Equal(1f, onCircle.Length(), 1e-4f);

            Vector2 inside = random.NextInsideUnitCircle();
            Assert.InRange(inside.Length(), 0f, 1f);
        }
    }

    #endregion
}