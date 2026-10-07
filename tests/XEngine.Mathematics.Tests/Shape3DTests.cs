using System.Numerics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Контракты трёхмерных форм: <c>Aabb3</c>, <c>BoundingSphere</c>,
/// <c>Capsule3</c>, <c>Ray3</c>, <c>Plane3</c> (6.3).
/// </summary>
/// <remarks>
/// Имена методов совпадают с двумерными версиями, чтобы одна и та же мысль
/// одинаково записывалась в двумерном и трёхмерном коде.
/// </remarks>
public sealed class Shape3DTests
{
    [Fact]
    public void Aabb3_CoversAllPoints()
    {
        Aabb3 bounds = Aabb3.FromPoints(
        [
            new Vector3(-1f, 0f, 3f),
            new Vector3(4f, -2f, 0f),
            new Vector3(0f, 5f, 1f),
        ]);

        MathAssert.Equal(new Vector3(-1f, -2f, 0f), bounds.Min);
        MathAssert.Equal(new Vector3(4f, 5f, 3f), bounds.Max);
        MathAssert.Equal(new Vector3(5f, 7f, 3f), bounds.Size);
        MathAssert.Equal(new Vector3(1.5f, 1.5f, 1.5f), bounds.Center);
    }

    [Fact]
    public void Aabb3_RejectsEmptyPointList()
    {
        Assert.Throws<ArgumentException>(() => Aabb3.FromPoints([]));
    }

    [Fact]
    public void Aabb3_FromCenterAndSizeIsSymmetric()
    {
        Aabb3 bounds = Aabb3.FromCenterAndSize(new Vector3(1f, 2f, 3f), new Vector3(2f, 4f, 6f));

        MathAssert.Equal(new Vector3(0f, 0f, 0f), bounds.Min);
        MathAssert.Equal(new Vector3(2f, 4f, 6f), bounds.Max);
    }

    [Fact]
    public void Aabb3_ConstructorRejectsInvertedBounds()
    {
        Assert.Throws<ArgumentException>(() => new Aabb3(new Vector3(1f, 1f, 1f), new Vector3(0f, 2f, 2f)));
    }

    [Fact]
    public void Aabb3_EmptyContainsNothingAndUnionsToOther()
    {
        Aabb3 empty = Aabb3.Empty;
        Aabb3 bounds = Aabb3.FromCenterAndSize(Vector3.Zero, Vector3.One);

        Assert.True(empty.IsEmpty);
        Assert.False(empty.Contains(Vector3.Zero));
        Assert.Equal(bounds, empty.Union(bounds));
        Assert.Equal(bounds, bounds.Union(empty));
    }

    [Fact]
    public void Aabb3_ContainsIncludesBoundsAndExcludesOutside()
    {
        Aabb3 bounds = Aabb3.FromCenterAndSize(Vector3.Zero, new Vector3(2f, 2f, 2f));

        Assert.True(bounds.Contains(Vector3.Zero));
        Assert.True(bounds.Contains(new Vector3(1f, 1f, 1f)), "Граница входит в объём.");
        Assert.False(bounds.Contains(new Vector3(1.01f, 0f, 0f)));
        Assert.True(bounds.Contains(bounds));
        Assert.False(bounds.Contains(Aabb3.FromCenterAndSize(new Vector3(5f, 0f, 0f), Vector3.One)));
    }

    [Fact]
    public void Aabb3_IntersectsDetectsOverlap()
    {
        Aabb3 bounds = Aabb3.FromCenterAndSize(Vector3.Zero, new Vector3(2f, 2f, 2f));

        Assert.True(bounds.Intersects(Aabb3.FromCenterAndSize(new Vector3(1.5f, 0f, 0f), Vector3.One)));
        Assert.False(bounds.Intersects(Aabb3.FromCenterAndSize(new Vector3(3f, 0f, 0f), Vector3.One)));
    }

    [Fact]
    public void Aabb3_ClosestPointClampsToBounds()
    {
        Aabb3 bounds = Aabb3.FromCenterAndSize(Vector3.Zero, new Vector3(2f, 2f, 2f));

        MathAssert.Equal(new Vector3(1f, 0.5f, -1f), bounds.ClosestPoint(new Vector3(5f, 0.5f, -5f)));
        Assert.Equal(0f, bounds.DistanceTo(Vector3.Zero));
        Assert.Equal(0f, bounds.DistanceTo(new Vector3(1f, 1f, 1f)));
    }

    [Fact]
    public void Aabb3_ExpandGrowsOnBothSides()
    {
        Aabb3 bounds = Aabb3.FromCenterAndSize(Vector3.Zero, Vector3.One).Expand(new Vector3(1f, 2f, 3f));

        MathAssert.Equal(new Vector3(-1.5f, -2.5f, -3.5f), bounds.Min);
        MathAssert.Equal(new Vector3(1.5f, 2.5f, 3.5f), bounds.Max);
    }

    [Fact]
    public void Aabb3_GetCornersWritesAllEightWithoutAllocation()
    {
        Aabb3 bounds = Aabb3.FromCenterAndSize(Vector3.Zero, Vector3.One);

        Span<Vector3> corners = stackalloc Vector3[8];
        bounds.GetCorners(corners);

        Assert.Equal(8, corners.Length);
        Assert.All(corners.ToArray(), corner => Assert.True(bounds.Contains(corner), $"Уг {corner} вне объёма."));
        Assert.NotEqual(corners[0], corners[7]);
    }

    [Fact]
    public void Aabb3_TransformCoversRotatedBox()
    {
        Aabb3 bounds = Aabb3.FromCenterAndSize(Vector3.Zero, Vector3.One);
        Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(
            QuaternionExtensions.FromAxisAngle(Vector3.UnitY, Angle.FromDegrees(45f)));

        Aabb3 transformed = bounds.Transform(rotation);

        float half = MathF.Sqrt(0.5f);
        MathAssert.Equal(new Vector3(-half, -0.5f, -half), transformed.Min, 1e-4f);
        MathAssert.Equal(new Vector3(half, 0.5f, half), transformed.Max, 1e-4f);
    }

    [Fact]
    public void Aabb3_TransformMovesBoxWithTranslation()
    {
        Aabb3 bounds = Aabb3.FromCenterAndSize(new Vector3(1f, 2f, 3f), new Vector3(2f, 2f, 2f));

        Aabb3 transformed = bounds.Transform(Matrix4x4.CreateTranslation(new Vector3(10f, 0f, 0f)));

        MathAssert.Equal(new Vector3(10f, 1f, 2f), transformed.Min);
        MathAssert.Equal(new Vector3(12f, 3f, 4f), transformed.Max);
    }

    [Fact]
    public void BoundingSphere_FromAabbEnclosesBox()
    {
        Aabb3 bounds = Aabb3.FromCenterAndSize(Vector3.Zero, new Vector3(2f, 2f, 2f));

        BoundingSphere sphere = BoundingSphere.FromAabb(bounds);

        MathAssert.Equal(Vector3.Zero, sphere.Center);
        MathAssert.Equal(MathF.Sqrt(3f), sphere.Radius, 1e-5f);
        Assert.True(sphere.Contains(new Vector3(1f, 1f, 1f)));
        Assert.False(sphere.Contains(new Vector3(1.8f, 0f, 0f)), "Точка вне описанной сферы не принадлежит ей.");
    }

    [Fact]
    public void BoundingSphere_RejectsNegativeRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundingSphere(Vector3.Zero, -1f));
    }

    [Fact]
    public void BoundingSphere_DistanceToAndInnerRadius()
    {
        BoundingSphere sphere = new(new Vector3(0f, 0f, 0f), 2f);

        MathAssert.Equal(0f, sphere.DistanceTo(new Vector3(1f, 0f, 0f)), 1e-5f);
        MathAssert.Equal(3f, sphere.DistanceTo(new Vector3(5f, 0f, 0f)), 1e-5f);
        MathAssert.Equal(2f, sphere.InnerRadius, 1e-5f);
    }

    [Fact]
    public void BoundingSphere_UnionCoversBothSpheres()
    {
        BoundingSphere first = new(new Vector3(0f, 0f, 0f), 1f);
        BoundingSphere second = new(new Vector3(4f, 0f, 0f), 1f);

        BoundingSphere union = first.Union(second);

        MathAssert.Equal(new Vector3(2f, 0f, 0f), union.Center, 1e-5f);
        MathAssert.Equal(3f, union.Radius, 1e-5f);
    }

    [Fact]
    public void Ray3_NormalizesDirectionAndRejectsZeroDirectionFallback()
    {
        Ray3 ray = new(Vector3.Zero, new Vector3(0f, 0f, 5f));

        MathAssert.Equal(Vector3.UnitZ, ray.Direction);
        MathAssert.Equal(Vector3.UnitX, new Ray3(Vector3.Zero, Vector3.Zero).Direction);
        MathAssert.Equal(new Vector3(0f, 0f, 7f), ray.GetPoint(7f));
    }

    [Fact]
    public void Ray3_ClosestPointToClampsBehindOrigin()
    {
        Ray3 ray = new(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 1f));

        MathAssert.Equal(new Vector3(0f, 0f, 3f), ray.ClosestPointTo(new Vector3(1f, 2f, 3f)));
        MathAssert.Equal(Vector3.Zero, ray.ClosestPointTo(new Vector3(1f, 2f, -5f)));
    }

    [Fact]
    public void Ray3_IntersectsAabb3()
    {
        Aabb3 bounds = Aabb3.FromCenterAndSize(new Vector3(0f, 0f, 10f), new Vector3(2f, 2f, 2f));

        Assert.True(new Ray3(Vector3.Zero, Vector3.UnitZ).Intersects(bounds));
        Assert.True(new Ray3(new Vector3(1f, 1f, 5f), Vector3.UnitZ).Intersects(bounds), "Луч, начинающийся внутри, тоже пересекает.");
        Assert.False(new Ray3(Vector3.Zero, -Vector3.UnitZ).Intersects(bounds));
        Assert.False(new Ray3(new Vector3(5f, 0f, 0f), Vector3.UnitZ).Intersects(bounds));
        Assert.False(new Ray3(new Vector3(0f, 0f, 20f), Vector3.UnitZ).Intersects(bounds), "Луч, уходящий от объёма, не пересекает.");
    }

    [Fact]
    public void Ray3_RaycastReportsDistanceToAabb3()
    {
        Aabb3 bounds = Aabb3.FromCenterAndSize(new Vector3(0f, 0f, 10f), Vector3.One);
        Ray3 ray = new(Vector3.Zero, Vector3.UnitZ);

        Assert.True(ray.Raycast(bounds, out float distance));
        MathAssert.Equal(9.5f, distance, 1e-4f);
        Assert.False(ray.Raycast(Aabb3.FromCenterAndSize(new Vector3(0f, 0f, -10f), Vector3.One), out float miss));
        Assert.True(miss <= 0f, "Расстояние при промахе не задаётся положительным.");
    }

    [Fact]
    public void Ray3_IntersectsBoundingSphere()
    {
        BoundingSphere sphere = new(new Vector3(0f, 0f, 10f), 2f);

        Assert.True(new Ray3(Vector3.Zero, Vector3.UnitZ).Intersects(sphere));
        Assert.False(new Ray3(new Vector3(5f, 0f, 0f), Vector3.UnitZ).Intersects(sphere));
        Assert.False(new Ray3(Vector3.Zero, -Vector3.UnitZ).Intersects(sphere));
    }

    [Fact]
    public void Ray3_RaycastReportsDistanceToBoundingSphere()
    {
        BoundingSphere sphere = new(new Vector3(0f, 0f, 10f), 2f);
        Ray3 ray = new(Vector3.Zero, Vector3.UnitZ);

        Assert.True(ray.Raycast(sphere, out float distance));
        MathAssert.Equal(8f, distance, 1e-4f);
    }

    [Fact]
    public void Ray3_IntersectsPlane3AndReportsDistance()
    {
        Plane3 plane = Plane3.FromPointNormal(new Vector3(0f, 0f, 10f), Vector3.UnitZ);
        Ray3 ray = new(Vector3.Zero, Vector3.UnitZ);

        Assert.True(ray.Intersects(plane));
        Assert.True(ray.Intersects(plane, out float distance));
        MathAssert.Equal(10f, distance, 1e-4f);
        Assert.False(ray.Intersects(Plane3.FromPointNormal(new Vector3(0f, 0f, -10f), Vector3.UnitZ)));
        Assert.False(
            new Ray3(new Vector3(0f, 5f, 0f), Vector3.UnitZ)
                .Intersects(Plane3.FromPointNormal(new Vector3(0f, 2f, 0f), Vector3.UnitY)),
            "Луч, параллельный плоскости, не пересекает её.");
    }

    [Fact]
    public void Ray3_IntersectsCapsule3()
    {
        Capsule3 capsule = new(new Vector3(0f, 0f, 10f), new Vector3(0f, 2f, 10f), 1f);

        Assert.True(new Ray3(Vector3.Zero, Vector3.UnitZ).Intersects(capsule));
        Assert.False(new Ray3(Vector3.Zero, new Vector3(1f, 0f, 1f).SafeNormalize()).Intersects(capsule));
    }

    [Fact]
    public void Ray3_RaycastReportsDistanceToCapsule3()
    {
        Capsule3 capsule = new(new Vector3(0f, 0f, 10f), new Vector3(0f, 2f, 10f), 1f);
        Ray3 ray = new(Vector3.Zero, Vector3.UnitZ);

        Assert.True(ray.Raycast(capsule, out float distance));
        MathAssert.Equal(9f, distance, 1e-4f);
    }

    [Fact]
    public void Capsule3_ContainsPointWithinRadiusOfAxis()
    {
        Capsule3 capsule = new(new Vector3(0f, 0f, 0f), new Vector3(0f, 2f, 0f), 1f);

        Assert.True(capsule.Contains(new Vector3(0f, 1f, 0f)), "Точка на оси внутри капсулы.");
        Assert.True(capsule.Contains(new Vector3(0f, 1f, 0.9f)), "Точка у поверхности внутри.");
        Assert.False(capsule.Contains(new Vector3(0f, 1f, 1.1f)));
        Assert.True(capsule.Contains(new Vector3(0f, 3f, 0f)), "Скруглённый торец входит в капсулу.");
        Assert.False(capsule.Contains(new Vector3(0f, 3.5f, 0f)), "За скруглённым торцом точка вне капсулы.");
    }

    [Fact]
    public void Capsule3_RejectsNegativeRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Capsule3(Vector3.Zero, Vector3.UnitY, -0.5f));
    }

    [Fact]
    public void Capsule3_ClosestPointToUsesAxisSegment()
    {
        Capsule3 capsule = new(new Vector3(0f, 0f, 0f), new Vector3(0f, 2f, 0f), 1f);

        Vector3 inside = capsule.ClosestPointTo(new Vector3(5f, 1f, 0f));
        MathAssert.Equal(new Vector3(1f, 1f, 0f), inside);

        Vector3 beyond = capsule.ClosestPointTo(new Vector3(0f, 5f, 0f));
        MathAssert.Equal(new Vector3(0f, 3f, 0f), beyond);
    }

    [Fact]
    public void Capsule3_BoundsCoverRadiusAroundAxis()
    {
        Capsule3 capsule = new(new Vector3(0f, 0f, 0f), new Vector3(0f, 2f, 0f), 1f);

        MathAssert.Equal(new Vector3(-1f, -1f, -1f), capsule.Bounds.Min);
        MathAssert.Equal(new Vector3(1f, 3f, 1f), capsule.Bounds.Max);
    }

    [Fact]
    public void Plane3_SignedDistanceIsPositiveOnNormalSide()
    {
        Plane3 plane = Plane3.FromPointNormal(new Vector3(0f, 2f, 0f), Vector3.UnitY);

        MathAssert.Equal(1f, plane.DistanceTo(new Vector3(0f, 3f, 0f)), 1e-5f);
        MathAssert.Equal(-1f, plane.DistanceTo(new Vector3(0f, 1f, 0f)), 1e-5f);
        MathAssert.Equal(0f, plane.DistanceTo(new Vector3(5f, 2f, 5f)), 1e-5f);
    }

    [Fact]
    public void Plane3_NormalizesNormalAndRejectsZero()
    {
        Plane3 plane = Plane3.FromPointNormal(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 7f));

        MathAssert.Equal(Vector3.UnitZ, plane.Normal);
        Assert.Throws<ArgumentException>(() => Plane3.FromPointNormal(Vector3.Zero, Vector3.Zero));
    }

    [Fact]
    public void Plane3_FromCoefficientsScalesOffsetWithNormal()
    {
        // Плоскость 2x + 4y + 6z - 6 = 0 проходит через точку (3, 0, 0) и
        // через (0, 1, 0): свободный член обязан масштабироваться вместе с
        // нормалью, иначе плоскость уезжает по глубине.
        Plane3 plane = Plane3.FromCoefficients(new Vector3(2f, 4f, 6f), -6f);

        MathAssert.Equal(0f, plane.DistanceTo(new Vector3(3f, 0f, 0f)), 1e-4f);
        MathAssert.Equal(0f, plane.DistanceTo(new Vector3(0f, 1.5f, 0f)), 1e-4f);
        MathAssert.Equal(1f, plane.Normal.Length(), 1e-5f);
    }

    [Fact]
    public void Plane3_FromCoefficientsRejectsZeroNormal()
    {
        Assert.Throws<ArgumentException>(() => Plane3.FromCoefficients(Vector3.Zero, 1f));
    }

    [Fact]
    public void Plane3_ProjectLiesOnPlane()
    {
        Plane3 plane = Plane3.FromPointNormal(new Vector3(0f, 0f, 5f), Vector3.UnitZ);

        Vector3 projected = plane.Project(new Vector3(2f, 3f, 9f));

        MathAssert.Equal(new Vector3(2f, 3f, 5f), projected, 1e-5f);
        MathAssert.Equal(0f, plane.DistanceTo(projected), 1e-5f);
    }
}