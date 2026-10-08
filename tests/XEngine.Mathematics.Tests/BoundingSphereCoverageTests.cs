using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Тесты ограничивающей сферы: методы, которые раньше не вызывались.
/// </summary>
/// <remarks>
/// Сфера проверяется по своему определению, а не по сравнению с другими
/// методами библиотеки. Эталон — обычные формулы в двойной точности:
/// покрытие есть тогда и только тогда, когда точка не дальше радиуса, объединение
/// есть наименьшая сфера, содержащая обе.
/// </remarks>
public class BoundingSphereCoverageTests
{
    /// <summary>
    /// Допуск на расстояние и длину.
    /// </summary>
    private const float Tolerance = 1e-4f;

    private static Vector3 P(float x, float y, float z) => new(x, y, z);

    #region FromPoints

    /// <summary>
    /// Сфера по набору точек обязана покрывать их все и быть минимальной для
    /// прямоугольного охвата: центр в середине границ, радиус — половина
    /// диагонали. Эталон считается напрямую из минимумов и максимумов.
    /// </summary>
    [Fact]
    public void FromPoints_MatchesBoundsDefinition()
    {
        Vector3[] points =
        [
            P(-3f, 1, 0.5f),
            P(2, -4, 7),
            P(0, 0, 0),
            P(1.5f, 1.5f, -1f),
            P(-1, 9, -2),
        ];

        BoundingSphere sphere = BoundingSphere.FromPoints(points);

        Vector3 minimum = points[0];
        Vector3 maximum = points[0];
        foreach (Vector3 point in points)
        {
            minimum = Vector3.Min(minimum, point);
            maximum = Vector3.Max(maximum, point);
        }

        Vector3 expectedCenter = (minimum + maximum) * 0.5f;
        double expectedRadius = ReferenceGeometry.Distance(minimum, maximum) * 0.5;

        MathAssert.Equal(expectedCenter, sphere.Center, Tolerance);
        MathAssert.Equal((float)expectedRadius, sphere.Radius, Tolerance);

        foreach (Vector3 point in points)
        {
            Assert.True(sphere.Contains(point), $"Точка {point} не покрыта сферой по набору.");
        }
    }

    /// <summary>
    /// Одна точка: радиус нулевой, центр в этой точке.
    /// </summary>
    [Fact]
    public void FromPoints_SinglePoint()
    {
        BoundingSphere sphere = BoundingSphere.FromPoints([P(3f, -4, 5)]);

        MathAssert.Equal(P(3f, -4, 5), sphere.Center, Tolerance);
        MathAssert.Equal(0f, sphere.Radius, Tolerance);
        Assert.True(sphere.Contains(P(3f, -4, 5)));
    }

    /// <summary>
    /// Совпадающие точки: та же сфера нулевого радиуса.
    /// </summary>
    [Fact]
    public void FromPoints_IdenticalPoints()
    {
        Vector3[] points = [P(1, 1, 1), P(1, 1, 1), P(1, 1, 1)];
        BoundingSphere sphere = BoundingSphere.FromPoints(points);

        MathAssert.Equal(0f, sphere.Radius, Tolerance);
        MathAssert.Equal(P(1, 1, 1), sphere.Center, Tolerance);
    }

    /// <summary>
    /// Пустой набор — ошибка вызывающего.
    /// </summary>
    [Fact]
    public void FromPoints_RejectsEmpty()
        => Assert.Throws<ArgumentException>(() => BoundingSphere.FromPoints([]));

    #endregion

    #region Intersects

    /// <summary>
    /// Две сферы пересекаются тогда и только тогда, когда расстояние между
    /// центрами не больше суммы радиусов. Это и есть определение; радиус
    /// подбирается от эталонного расстояния с запасом.
    /// </summary>
    [Fact]
    public void Intersects_MatchesCenterDistance()
    {
        // Радиусы фиксированы, варьируется расстояние между центрами: так
        // граница «сумма радиусов против расстояния» проверяется напрямую.
        const float radiusA = 2f;
        const float radiusB = 1.5f;
        const float reach = radiusA + radiusB;

        foreach (float distance in new[] { 0f, 1f, 3.4f, 3.5f, 4f, 10f })
        {
            BoundingSphere first = new(P(0, 0, 0), radiusA);
            BoundingSphere second = new(P(distance, 0, 0), radiusB);

            double reference = ReferenceGeometry.Distance(first.Center, second.Center);
            Assert.True(Math.Abs(reference - distance) < Tolerance, "Эталонное расстояние не совпало с заданным.");

            bool expected = distance <= reach + Tolerance;
            Assert.Equal(expected, first.Intersects(second));
        }
    }

    [Fact]
    public void Intersects_TouchingAndCoincident()
    {
        BoundingSphere sphere = new(P(1, 2, 3), 2f);

        // Касание внешним образом: расстояние равно сумме радиусов.
        Assert.True(sphere.Intersects(new BoundingSphere(P(1, 2, 5), 2f)));

        // Совпадающие центры.
        Assert.True(sphere.Intersects(sphere));

        // Одна внутри другой без касания границ.
        Assert.True(sphere.Intersects(new BoundingSphere(P(1, 2, 3), 0.5f)));
        Assert.True(new BoundingSphere(P(1, 2, 3), 0.5f).Intersects(sphere));

        // Раздельные.
        Assert.False(sphere.Intersects(new BoundingSphere(P(1, 2, 8), 2f)));
    }

    #endregion

    #region ClosestPointOnSurface

    /// <summary>
    /// Ближайшая точка поверхности лежит на радиусе от центра в направлении
    /// исходной точки.
    /// </summary>
    [Fact]
    public void ClosestPointOnSurface_LiesOnSphere()
    {
        BoundingSphere sphere = new(P(1, 2, 3), 2.5f);
        var random = new XorShift64Star(4321);

        for (int i = 0; i < 500; i++)
        {
            Vector3 point = new Vector3(
                (random.NextFloat() - 0.5f) * 40f,
                (random.NextFloat() - 0.5f) * 40f,
                (random.NextFloat() - 0.5f) * 40f);

            Vector3 closest = sphere.ClosestPointOnSurface(point);

            MathAssert.Equal(sphere.Radius, (float)ReferenceGeometry.Distance(closest, sphere.Center), Tolerance);

            Vector3 toPoint = point - sphere.Center;
            Vector3 toClosest = closest - sphere.Center;
            if (toPoint.LengthSquared() > Tolerance * Tolerance)
            {
                Assert.True(Vector3.Dot(toPoint, toClosest) > 0f, "Точка оказалась с противоположной стороны сферы.");
            }
        }
    }

    /// <summary>
    /// Точка в центре: направление не определено, и метод обязан вернуть точку
    /// на радиусе, а не ноль.
    /// </summary>
    [Fact]
    public void ClosestPointOnSurface_PointAtCenter()
    {
        BoundingSphere sphere = new(P(1, 2, 3), 1.5f);
        Vector3 closest = sphere.ClosestPointOnSurface(sphere.Center);

        MathAssert.Equal(sphere.Radius, (float)ReferenceGeometry.Distance(closest, sphere.Center), Tolerance);
        Assert.NotEqual(sphere.Center, closest);
    }

    /// <summary>
    /// Точка на поверхности возвращается сама: расстояние до неё нулевое.
    /// </summary>
    [Fact]
    public void ClosestPointOnSurface_PointOnSurface()
    {
        BoundingSphere sphere = new(P(0, 0, 0), 3f);
        Vector3 onSurface = P(3, 0, 0);

        MathAssert.Equal(onSurface, sphere.ClosestPointOnSurface(onSurface), Tolerance);
        MathAssert.Equal(0f, sphere.DistanceTo(onSurface), Tolerance);
    }

    #endregion

    #region Union

    /// <summary>
    /// Объединение обязано покрывать обе исходные сферы, и быть наименьшей
    /// такой сферой: при частичном перекрытии центр смещается в сторону
    /// меньшей сферы, а радиус достраивается ровно настолько, чтобы дойти до
    /// дальней границы второй.
    /// </summary>
    [Fact]
    public void Union_ProducesSmallestEnclosingSphere()
    {
        BoundingSphere first = new(P(0, 0, 0), 1f);
        BoundingSphere second = new(P(3, 0, 0), 2f);

        BoundingSphere union = first.Union(second);

        // Дальняя точка второй сферы достигается, ближняя — нет.
        MathAssert.Equal(P(5, 0, 0), union.ClosestPointOnSurface(P(5, 0, 0)), Tolerance);
        Assert.True(union.Contains(P(-1, 0, 0)), "Левая граница первой сферы потеряна.");
        Assert.True(union.Contains(P(5, 0, 0)), "Правая граница второй сферы потеряна.");
        Assert.True(union.Intersects(first));
        Assert.True(union.Intersects(second));

        // Наименьшая: радиус равен расстоянию от центра до самой далёкой точки.
        float required = MathF.Max(
            (float)ReferenceGeometry.Distance(union.Center, P(-1, 0, 0)),
            (float)ReferenceGeometry.Distance(union.Center, P(5, 0, 0)));
        MathAssert.Equal(required, union.Radius, Tolerance);
    }

    /// <summary>
    /// Если одна сфера целиком внутри другой, объединение есть большая из них.
    /// </summary>
    [Fact]
    public void Union_KeepsEnclosingSphere()
    {
        BoundingSphere big = new(P(0, 0, 0), 5f);
        BoundingSphere small = new(P(1, 0, 0), 1f);

        Assert.Equal(big, big.Union(small));
        Assert.Equal(big, small.Union(big));

        // Касание снаружи вложенностью не является: сфера заходит за границу,
        // и объединение обязано вырасти.
        BoundingSphere touching = new(P(5, 0, 0), 1f);
        BoundingSphere merged = big.Union(touching);
        Assert.True(merged.Radius > big.Radius, "Объединение с внешне касающейся сферой обязано быть больше.");
        Assert.True(merged.Contains(P(6, 0, 0)), "Дальняя точка касающейся сферы потеряна.");
        Assert.True(merged.Contains(P(-5, 0, 0)), "Левая граница большой сферы потеряна.");
    }

    /// <summary>
    /// Объединение коммутативно: порядок сфер не должен влиять на результат.
    /// </summary>
    [Fact]
    public void Union_IsCommutative()
    {
        var random = new XorShift64Star(2024);

        for (int i = 0; i < 200; i++)
        {
            BoundingSphere first = new(
                new Vector3(
                    (random.NextFloat() - 0.5f) * 20f,
                    (random.NextFloat() - 0.5f) * 20f,
                    (random.NextFloat() - 0.5f) * 20f),
                0.5f + (random.NextFloat() * 5f));
            BoundingSphere second = new(
                new Vector3(
                    (random.NextFloat() - 0.5f) * 20f,
                    (random.NextFloat() - 0.5f) * 20f,
                    (random.NextFloat() - 0.5f) * 20f),
                0.5f + (random.NextFloat() * 5f));

            BoundingSphere ab = first.Union(second);
            BoundingSphere ba = second.Union(first);

            MathAssert.Equal(ab.Center, ba.Center, Tolerance);
            MathAssert.Equal(ab.Radius, ba.Radius, Tolerance);
        }
    }

    #endregion

    #region Свойства, равенство, строки

    [Fact]
    public void DiameterAndBounds()
    {
        BoundingSphere sphere = new(P(1, 1, 1), 2f);

        MathAssert.Equal(4f, sphere.Diameter, Tolerance);

        Aabb3 bounds = sphere.Bounds;
        MathAssert.Equal(sphere.Center, bounds.Center, Tolerance);
        MathAssert.Equal(P(4, 4, 4), bounds.Size, Tolerance);

        // Описанный куб содержит сферу: углы куба дальше центра на √3 радиуса.
        Assert.True(bounds.Contains(sphere.ClosestPointOnSurface(P(100, 100, 100))));
    }

    [Fact]
    public void EqualityOperatorsAndString()
    {
        BoundingSphere a = new(P(1, 2, 3), 4f);
        BoundingSphere same = new(P(1, 2, 3), 4f);
        BoundingSphere otherRadius = new(P(1, 2, 3), 5f);
        BoundingSphere otherCenter = new(P(1, 2, 4), 4f);

        Assert.True(a.Equals(same));
        Assert.True(a == same);
        Assert.False(a != same);
        Assert.Equal(a.GetHashCode(), same.GetHashCode());

        Assert.False(a.Equals(otherRadius));
        Assert.False(a.Equals(otherCenter));
        Assert.True(a != otherRadius);
        Assert.True(a != otherCenter);

        Assert.True(a.Equals((object)same));
        Assert.False(a.Equals((object)otherRadius));
        Assert.False(a.Equals(null));
        Assert.False(a.Equals("не сфера"));

        string text = a.ToString();
        Assert.Contains("BoundingSphere", text, StringComparison.Ordinal);
        Assert.Contains("4.000", text, StringComparison.Ordinal);
    }

    #endregion
}