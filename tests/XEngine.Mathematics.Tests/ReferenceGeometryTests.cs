using System.Numerics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Проверка самого эталона.
/// </summary>
/// <remarks>
/// Перебор из <see cref="ReferenceGeometry"/> используется как истина в тестах
/// капсул, поэтому его собственная корректность обязана быть доказана на
/// случаях, где расстояние выводится из определения в уме, а не считается тем
/// же кодом.
/// </remarks>
public class ReferenceGeometryTests
{
    /// <summary>
    /// Допуск: перебор сходится точнее одинарной точности, но не настолько,
    /// чтобы утверждать равенство.
    /// </summary>
    private const double Tolerance = 1e-9;

    /// <summary>
    /// Параллельные отрезки на известном расстоянии: проекция одного на другой
    /// даёт ответ без всякого перебора.
    /// </summary>
    /// <param name="gap">Расстояние между отрезками.</param>
    [Theory]
    [InlineData(1.0)]
    [InlineData(0.25)]
    [InlineData(7.5)]
    [InlineData(0.001)]
    public void ParallelSegments_DistanceIsGap(double gap)
    {
        Vector3 a = new(0, 0, 0);
        Vector3 b = new(1, 0, 0);
        Vector3 c = new(0, (float)gap, 0);
        Vector3 d = new(1, (float)gap, 0);

        Assert.Equal(gap, ReferenceGeometry.SegmentDistanceBrute(a, b, c, d, 2000), Tolerance);
    }

    /// <summary>
    /// Параллельные отрезки, разнесённые вдоль общей оси: ближайшими
    /// оказываются концы, расстояние задаётся теоремой Пифагора.
    /// </summary>
    [Fact]
    public void OffsetParallelSegments_DistanceIsDiagonal()
    {
        double gap = 3.0;
        double offset = 4.0;

        Vector3 a = new(0, 0, 0);
        Vector3 b = new(1, 0, 0);
        Vector3 c = new((float)(1 + offset), (float)gap, 0);
        Vector3 d = new((float)(2 + offset), (float)gap, 0);

        double expected = Math.Sqrt((offset * offset) + (gap * gap));
        Assert.Equal(expected, ReferenceGeometry.SegmentDistanceBrute(a, b, c, d, 2000), Tolerance);
    }

    /// <summary>
    /// Скрещивающиеся отрезки: расстояние задаётся длиной общего перпендикуляра.
    /// </summary>
    [Fact]
    public void SkewSegments_DistanceIsCommonPerpendicular()
    {
        // Первый вдоль X при y = 0, второй вдоль Z при y = 2 и x = 5.
        // Общий перпендикуляр направлен вдоль Y, его длина задаёт расстояние.
        Vector3 a = new(0, 0, 0);
        Vector3 b = new(10, 0, 0);
        Vector3 c = new(5, 2, -3);
        Vector3 d = new(5, 2, 4);

        Assert.Equal(2.0, ReferenceGeometry.SegmentDistanceBrute(a, b, c, d, 2000), Tolerance);
    }

    /// <summary>
    /// Пересекающиеся отрезки дают ноль.
    /// </summary>
    [Fact]
    public void IntersectingSegments_DistanceIsZero()
    {
        Vector3 a = new(0, 0, 0);
        Vector3 b = new(1, 0, 0);
        Vector3 c = new(0.5f, -1, 0);
        Vector3 d = new(0.5f, 1, 0);

        Assert.Equal(0.0, ReferenceGeometry.SegmentDistanceBrute(a, b, c, d, 2000), Tolerance);
    }

    /// <summary>
    /// Отрезок, вырожденный в точку: расстояние до него есть расстояние до
    /// этой точки.
    /// </summary>
    [Fact]
    public void DegenerateSegments_DistanceIsPointToSegment()
    {
        Vector3 point = new(0, 0, 0);

        // Точка на отрезке.
        Assert.Equal(
            0.0,
            ReferenceGeometry.SegmentDistanceBrute(point, point, new Vector3(-1, 0, 0), new Vector3(1, 0, 0), 2000),
            Tolerance);

        // Точка за концом отрезка.
        Assert.Equal(
            2.0,
            ReferenceGeometry.SegmentDistanceBrute(new Vector3(3, 0, 0), new Vector3(3, 0, 0), new Vector3(-1, 0, 0), new Vector3(1, 0, 0), 2000),
            Tolerance);

        // Точка сбоку.
        Assert.Equal(
            5.0,
            ReferenceGeometry.SegmentDistanceBrute(new Vector3(0, 5, 0), new Vector3(0, 5, 0), new Vector3(-1, 0, 0), new Vector3(1, 0, 0), 2000),
            Tolerance);

        // Две точки.
        Assert.Equal(
            5.0,
            ReferenceGeometry.SegmentDistanceBrute(new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Vector3(3, 4, 0), new Vector3(3, 4, 0), 2000),
            Tolerance);
    }

    /// <summary>
    /// Точка на оси отрезка: ближайшая точка совпадает с самой точкой.
    /// </summary>
    [Fact]
    public void ClosestPointOnSegment_ProjectionAndClamping()
    {
        Vector3 a = new(0, 0, 0);
        Vector3 b = new(2, 0, 0);

        // Проекция внутри отрезка.
        MathAssert.Equal(new Vector3(1, 0, 0), ReferenceGeometry.ClosestPointOnSegment(new Vector3(1, 3, 0), a, b), 1e-6f);

        // Проекция левее начала: ближайшая точка — начало.
        MathAssert.Equal(a, ReferenceGeometry.ClosestPointOnSegment(new Vector3(-5, 0, 0), a, b), 1e-6f);

        // Проекция правее конца: ближайшая точка — конец.
        MathAssert.Equal(b, ReferenceGeometry.ClosestPointOnSegment(new Vector3(9, 0, 0), a, b), 1e-6f);

        // Вырожденный отрезок: ближайшая точка — сам конец.
        MathAssert.Equal(a, ReferenceGeometry.ClosestPointOnSegment(new Vector3(9, 9, 0), a, a), 1e-6f);
    }

    /// <summary>
    /// Перебор не должен зависеть от числа проб: уточнение обязано довести
    /// ответ до одной и той же величины.
    /// </summary>
    [Fact]
    public void BruteForce_IsStableUnderRefinement()
    {
        Vector3 a = new(0, 0, 0);
        Vector3 b = new(1, 0, 0);
        Vector3 c = new(0.7f, 1, 0.3f);
        Vector3 d = new(-0.2f, 1.4f, 0.9f);

        double coarse = ReferenceGeometry.SegmentDistanceBrute(a, b, c, d, 50);
        double fine = ReferenceGeometry.SegmentDistanceBrute(a, b, c, d, 5000);

        Assert.Equal(coarse, fine, 1e-6);
    }
}