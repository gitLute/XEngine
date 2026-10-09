using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

public sealed class ShapeTests
{
    [Fact]
    public void Rect_Intersection_ReturnsOverlap()
    {
        Rect first = new(0f, 0f, 10f, 10f);
        Rect second = new(5f, 5f, 10f, 10f);

        Rect result = first.Intersection(second);

        Assert.Equal(5f, result.X, 1e-5f);
        Assert.Equal(5f, result.Y, 1e-5f);
        Assert.Equal(5f, result.Width, 1e-5f);
        Assert.Equal(5f, result.Height, 1e-5f);
    }

    [Fact]
    public void Rect_Intersection_ReturnsZeroWhenDisjoint()
    {
        Rect first = new(0f, 0f, 1f, 1f);
        Rect second = new(5f, 5f, 1f, 1f);

        Rect result = first.Intersection(second);

        Assert.True(result.IsEmpty);
        Assert.Equal(0f, result.Width, 1e-6f);
    }

    [Fact]
    public void Rect_Union_CoversBoth()
    {
        Rect first = new(0f, 0f, 2f, 2f);
        Rect second = new(8f, 8f, 2f, 2f);

        Rect result = first.Union(second);

        Assert.Equal(0f, result.Left, 1e-5f);
        Assert.Equal(10f, result.Right, 1e-5f);
        Assert.True(result.Contains(first));
        Assert.True(result.Contains(second));
    }

    [Fact]
    public void Rect_Contains_PointOnBoundary()
    {
        Rect rect = new(0f, 0f, 4f, 4f);

        Assert.True(rect.Contains(new Vector2(0f, 0f)));
        Assert.True(rect.Contains(new Vector2(4f, 4f)));
        Assert.False(rect.Contains(new Vector2(4.1f, 2f)));
    }

    [Fact]
    public void Rect_Contains_WithTolerance()
    {
        Rect rect = new(0f, 0f, 4f, 4f);

        Assert.True(rect.Contains(new Vector2(-0.05f, 2f), 0.1f));
        Assert.False(rect.Contains(new Vector2(-0.5f, 2f), 0.1f));
    }

    [Fact]
    public void Rect_IsEmpty_IsDecidedByAreaNotBySize()
    {
        // P1-2. Пустота определяется по ОБЛАСТИ, а не по размеру. Прямоугольник
        // с отрицательным размером задаёт настоящую область: Left и Right
        // нормализуют порядок, поэтому у Rect(0, 0, 5, -1) область X[0;5]
        // Y[-1;0] — площадь 5, и он не пуст.
        //
        // Прежняя редакция этого теста утверждала обратное
        // (Assert.True(new Rect(0f, 0f, 5f, -1f).IsEmpty)), то есть
        // закрепляла сам дефект P1-2. Тест переписан по решению владельца
        // кода вместе с закрытием P1-2.
        Assert.True(new Rect(0f, 0f, 0f, 5f).IsEmpty);    // нулевая ширина: области нет
        Assert.False(new Rect(0f, 0f, 5f, -1f).IsEmpty);   // область площадью 5
        Assert.False(new Rect(0f, 0f, -5f, 1f).IsEmpty);   // зеркальный случай
        Assert.False(new Rect(0f, 0f, 1f, 1f).IsEmpty);

        // Дискринирующий вход P1-2: при прежнем определении внутренний
        // прямоугольник считался пустым и Contains ложно отказывал, хотя
        // область X[4;8] Y[4;8] целиком внутри.
        Rect outer = new(0f, 0f, 10f, 10f);
        Rect inner = new(8f, 8f, -4f, -4f);

        Assert.True(outer.Contains(inner));
    }

    [Fact]
    public void Rect_FromCorners_NormalizesOrder()
    {
        Rect rect = Rect.FromCorners(new Vector2(10f, 10f), new Vector2(0f, 0f));

        Assert.Equal(0f, rect.X, 1e-5f);
        Assert.Equal(10f, rect.Width, 1e-5f);
    }

    [Fact]
    public void Rect_FromCenter_PlacesCenterCorrectly()
    {
        Rect rect = Rect.FromCenter(new Vector2(5f, 5f), new Vector2(4f, 2f));

        Assert.Equal(5f, rect.Center.X, 1e-5f);
        Assert.Equal(5f, rect.Center.Y, 1e-5f);
        Assert.Equal(4f, rect.Width, 1e-5f);
    }

    [Fact]
    public void Rect_InflateAndDefate_AreInverse()
    {
        Rect rect = new(0f, 0f, 10f, 10f);

        Rect inflated = rect.Inflate(new Vector2(2f, 3f));
        Rect restored = inflated.Deflate(new Vector2(2f, 3f));

        Assert.Equal(rect, restored);
    }

    [Fact]
    public void Rect_ScaleAboutPivot_KeepsPivot()
    {
        Vector2 pivot = new Vector2(10f, 10f);

        Rect scaled = new Rect(0f, 0f, 4f, 4f).Scale(2f, pivot);

        Assert.Equal(-10f, scaled.Left, 1e-4f);
        Assert.Equal(-10f, scaled.Top, 1e-4f);
        Assert.Equal(8f, scaled.Width, 1e-4f);

        // Точка (10, 10) остаётся на месте, но прямоугольник уехал от неё влево и вниз.
        Assert.True(scaled.Right < pivot.X);
    }

    [Fact]
    public void Rect_ToMatrix_MapsUnitSquare()
    {
        Rect rect = new(3f, 4f, 2f, 6f);

        Matrix3x2 matrix = rect.ToMatrix();

        Vector2 origin = matrix.TransformPoint(Vector2.Zero);
        Vector2 far = matrix.TransformPoint(new Vector2(1f, 1f));

        Assert.Equal(3f, origin.X, 1e-5f);
        Assert.Equal(4f, origin.Y, 1e-5f);
        Assert.Equal(5f, far.X, 1e-5f);
        Assert.Equal(10f, far.Y, 1e-5f);
    }

    [Fact]
    public void Aabb2_FromPoints_CoversAllPoints()
    {
        Aabb2 bounds = Aabb2.FromPoints(
        [
            new Vector2(-1f, 5f),
            new Vector2(4f, -2f),
            new Vector2(0f, 0f),
        ]);

        Assert.Equal(-1f, bounds.Min.X, 1e-5f);
        Assert.Equal(-2f, bounds.Min.Y, 1e-5f);
        Assert.Equal(4f, bounds.Max.X, 1e-5f);
        Assert.Equal(5f, bounds.Max.Y, 1e-5f);
    }

    [Fact]
    public void Aabb2_FromPoints_RejectsEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => Aabb2.FromPoints(ReadOnlySpan<Vector2>.Empty));
    }

    [Fact]
    public void Aabb2_ClosestPoint_ClampsToBounds()
    {
        Aabb2 bounds = new(new Vector2(0f, 0f), new Vector2(10f, 10f));

        Assert.Equal(new Vector2(3f, 7f), bounds.ClosestPoint(new Vector2(3f, 7f)));
        Assert.Equal(new Vector2(0f, 0f), bounds.ClosestPoint(new Vector2(-5f, -5f)));
        Assert.Equal(new Vector2(10f, 10f), bounds.ClosestPoint(new Vector2(50f, 50f)));
    }

    [Fact]
    public void Aabb2_DistanceTo_IsZeroInside()
    {
        Aabb2 bounds = new(new Vector2(0f, 0f), new Vector2(10f, 10f));

        Assert.Equal(0f, bounds.DistanceTo(new Vector2(5f, 5f)), 1e-5f);
        Assert.Equal(3f, bounds.DistanceTo(new Vector2(-3f, 5f)), 1e-5f);
    }

    [Fact]
    public void Aabb2_IntersectsAndContains()
    {
        Aabb2 outer = new(new Vector2(0f, 0f), new Vector2(10f, 10f));
        Aabb2 inner = new(new Vector2(1f, 1f), new Vector2(2f, 2f));
        Aabb2 outside = new(new Vector2(20f, 20f), new Vector2(30f, 30f));

        Assert.True(outer.Intersects(inner));
        Assert.True(outer.Contains(inner));
        Assert.False(outer.Intersects(outside));
    }

    [Fact]
    public void Aabb2_Empty_ContainsNothing()
    {
        Assert.True(Aabb2.Empty.IsEmpty);
        Assert.False(Aabb2.Empty.Contains(Vector2.Zero));
    }

    [Fact]
    public void Aabb2_FromRect_MatchesRect()
    {
        Rect rect = new(-2f, 3f, 6f, 4f);

        Aabb2 bounds = Aabb2.FromRect(rect);

        Assert.Equal(rect.Left, bounds.Min.X, 1e-5f);
        Assert.Equal(rect.Right, bounds.Max.X, 1e-5f);
        Assert.Equal(rect.Top, bounds.Min.Y, 1e-5f);
        Assert.Equal(rect.Bottom, bounds.Max.Y, 1e-5f);
    }

    [Fact]
    public void Circle2_ContainsPoint()
    {
        Circle2 circle = new(Vector2.Zero, 2f);

        Assert.True(circle.Contains(new Vector2(1f, 1f)));
        Assert.True(circle.Contains(new Vector2(2f, 0f)));
        Assert.False(circle.Contains(new Vector2(2.1f, 0f)));
    }

    [Fact]
    public void Circle2_RejectsNegativeRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Circle2(Vector2.Zero, -1f));
    }

    [Fact]
    public void Circle2_Intersects_TouchingCountsAsIntersection()
    {
        Circle2 first = new(Vector2.Zero, 1f);
        Circle2 second = new(new Vector2(2f, 0f), 1f);
        Circle2 third = new(new Vector2(2.01f, 0f), 1f);

        Assert.True(first.Intersects(second));
        Assert.False(first.Intersects(third));
    }

    [Fact]
    public void Circle2_ClosestPointOnBoundary()
    {
        Circle2 circle = new(Vector2.Zero, 3f);

        Vector2 result = circle.ClosestPointOnBoundary(new Vector2(10f, 0f));

        Assert.Equal(3f, result.Length(), 1e-5f);
        Assert.Equal(3f, result.X, 1e-5f);
    }

    [Fact]
    public void Segment2_ClosestPointTo_ClampsToEndpoints()
    {
        Segment2 segment = new(new Vector2(0f, 0f), new Vector2(10f, 0f));

        Assert.Equal(new Vector2(0f, 0f), segment.ClosestPointTo(new Vector2(-5f, 3f)));
        Assert.Equal(new Vector2(10f, 0f), segment.ClosestPointTo(new Vector2(15f, 3f)));
        Assert.Equal(new Vector2(5f, 0f), segment.ClosestPointTo(new Vector2(5f, 3f)));
    }

    [Fact]
    public void Segment2_HandlesDegenerateCase()
    {
        Segment2 segment = new(new Vector2(1f, 1f), new Vector2(1f, 1f));

        Assert.Equal(Vector2.Zero, segment.Direction);
        Assert.Equal(5f, segment.DistanceTo(new Vector2(4f, 5f)), 1e-4f);
        Assert.Equal(0f, segment.Length, 1e-6f);
    }

    [Fact]
    public void Capsule2_ContainsPoint()
    {
        Capsule2 capsule = new(new Segment2(new Vector2(-2f, 0f), new Vector2(2f, 0f)), 1f);

        Assert.True(capsule.Contains(new Vector2(0f, 0f)));
        Assert.True(capsule.Contains(new Vector2(2.5f, 0f)));
        Assert.True(capsule.Contains(new Vector2(-2f, 0.9f)));
        Assert.False(capsule.Contains(new Vector2(-2f, 1.5f)));
    }

    [Fact]
    public void Capsule2_FromBounds_UsesSmallerSideAsRadius()
    {
        Aabb2 bounds = Aabb2.FromCenterAndSize(Vector2.Zero, new Vector2(10f, 4f));

        Capsule2 capsule = Capsule2.FromBounds(bounds);

        Assert.Equal(2f, capsule.Radius, 1e-5f);
        Assert.Equal(-3f, capsule.A.X, 1e-4f);
        Assert.Equal(3f, capsule.B.X, 1e-4f);
        Assert.Equal(6f, capsule.Segment2.Length, 1e-4f);
    }

    [Fact]
    public void Capsule2_Bounds_AreExpandedByRadius()
    {
        Capsule2 capsule = new(new Segment2(Vector2.Zero, new Vector2(4f, 0f)), 1f);

        Aabb2 bounds = capsule.Bounds;

        Assert.Equal(-1f, bounds.Min.X, 1e-5f);
        Assert.Equal(-1f, bounds.Min.Y, 1e-5f);
        Assert.Equal(5f, bounds.Max.X, 1e-5f);
        Assert.Equal(1f, bounds.Max.Y, 1e-5f);
    }

    [Fact]
    public void Ray2_NormalizesDirection()
    {
        Ray2 ray = new(Vector2.Zero, new Vector2(10f, 0f));

        Assert.Equal(1f, ray.Direction.Length(), 1e-5f);
        Assert.Equal(new Vector2(1f, 0f), ray.Direction);
    }

    [Fact]
    public void Ray2_GetPoint_MovesAlongDirection()
    {
        Ray2 ray = new(new Vector2(1f, 1f), Vector2.UnitX);

        Assert.Equal(new Vector2(5f, 1f), ray.GetPoint(4f));
        Assert.Equal(new Vector2(-1f, 1f), ray.GetPoint(-2f));
    }

    [Fact]
    public void Ray2_IntersectsAabb2()
    {
        Ray2 ray = new(new Vector2(-5f, 0f), Vector2.UnitX);
        Aabb2 bounds = new(new Vector2(-1f, -1f), new Vector2(1f, 1f));

        Assert.True(ray.Intersects(bounds));
        Assert.False(ray.Intersects(new Aabb2(new Vector2(10f, 10f), new Vector2(11f, 11f))));
    }

    [Fact]
    public void Ray2_IntersectsCircle2()
    {
        Ray2 ray = new(new Vector2(-5f, 0f), Vector2.UnitX);

        Assert.True(ray.Intersects(new Circle2(Vector2.Zero, 1f)));
        Assert.False(ray.Intersects(new Circle2(new Vector2(0f, 5f), 1f)));
    }

    [Fact]
    public void Ray2_IntersectsSegment2()
    {
        Ray2 ray = new(new Vector2(-5f, 0f), Vector2.UnitX);
        Segment2 segment = new(new Vector2(0f, -2f), new Vector2(0f, 2f));

        Assert.True(ray.Intersects(segment));
        Assert.False(ray.Intersects(new Segment2(new Vector2(0f, 5f), new Vector2(0f, 8f))));
    }
}
