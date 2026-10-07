using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

public sealed class CollisionTests
{
    [Fact]
    public void ObbPenetration_ForAxisAlignedOverlap()
    {
        bool overlaps = Collision.TryGetObbPenetration(
            Vector2.Zero, new Vector2(2f, 2f), Angle.Zero,
            new Vector2(1.5f, 0f), new Vector2(2f, 2f), Angle.Zero,
            out Vector2 axis, out float depth);

        Assert.True(overlaps);
        Assert.Equal(0.5f, depth, 1e-4f);
        Assert.Equal(1f, axis.X, 1e-4f);
    }

    [Fact]
    public void ObbPenetration_DetectsSeparation()
    {
        bool overlaps = Collision.TryGetObbPenetration(
            Vector2.Zero, new Vector2(2f, 2f), Angle.Zero,
            new Vector2(10f, 0f), new Vector2(2f, 2f), Angle.Zero,
            out _, out float depth);

        Assert.False(overlaps);
        Assert.Equal(0f, depth, 1e-5f);
    }

    [Fact]
    public void ObbPenetration_HandlesRotation()
    {
        // Квадрат 2x2, повёрнутый на 45 градусов, касается квадрата 2x2 без поворота.
        bool overlaps = Collision.TryGetObbPenetration(
            Vector2.Zero, new Vector2(2f, 2f), Angle.FromDegrees(45),
            new Vector2(2.2f, 0f), new Vector2(2f, 2f), Angle.Zero,
            out _, out float depth);

        Assert.True(overlaps);
        Assert.True(depth > 0f);
    }

    [Fact]
    public void ObbPenetration_TouchingIsNotOverlap()
    {
        // Два квадрата 2x2, центры в 0 и 2: они касаются, но не пересекаются по объёму.
        bool overlaps = Collision.TryGetObbPenetration(
            Vector2.Zero, new Vector2(2f, 2f), Angle.Zero,
            new Vector2(2f, 0f), new Vector2(2f, 2f), Angle.Zero,
            out _, out float depth);

        Assert.False(overlaps);
        Assert.Equal(0f, depth, 1e-5f);
    }

    [Fact]
    public void ObbPenetration_ReportsMinimalAxis()
    {
        bool overlaps = Collision.TryGetObbPenetration(
            Vector2.Zero, new Vector2(4f, 4f), Angle.Zero,
            new Vector2(1.5f, 0f), new Vector2(4f, 4f), Angle.Zero,
            out Vector2 axis, out float depth);

        Assert.True(overlaps);

        // По X: 2 + 2 - 1.5 = 2.5, по Y: 2 + 2 - 0 = 4. Минимальная ось — X.
        Assert.Equal(2.5f, depth, 1e-4f);
        Assert.Equal(1f, axis.X, 1e-4f);
        Assert.Equal(0f, axis.Y, 1e-4f);
    }

    [Fact]
    public void ObbPenetration_AxisPointsFromFirstToSecond()
    {
        // Центр второго тела смещён вправо: ось минимального проникновения смотрит вправо.
        Collision.TryGetObbPenetration(
            Vector2.Zero, new Vector2(2f, 2f), Angle.Zero,
            new Vector2(1.5f, 0f), new Vector2(2f, 2f), Angle.Zero,
            out Vector2 axis, out _);

        Assert.True(axis.X > 0f);
    }

    [Fact]
    public void SegmentSegmentDistance_ParallelSegments()
    {
        Segment first = new(new Vector2(0f, 0f), new Vector2(10f, 0f));
        Segment second = new(new Vector2(0f, 3f), new Vector2(10f, 3f));

        Assert.Equal(3f, Collision.SegmentSegmentDistance(first, second), 1e-4f);
    }

    [Fact]
    public void SegmentSegmentDistance_CrossingIsZero()
    {
        Segment first = new(new Vector2(-5f, 0f), new Vector2(5f, 0f));
        Segment second = new(new Vector2(0f, -5f), new Vector2(0f, 5f));

        Assert.Equal(0f, Collision.SegmentSegmentDistance(first, second), 1e-4f);
    }

    [Fact]
    public void SegmentSegmentDistance_Disjoint()
    {
        Segment first = new(new Vector2(0f, 0f), new Vector2(1f, 0f));
        Segment second = new(new Vector2(0f, 4f), new Vector2(1f, 4f));

        Assert.Equal(4f, Collision.SegmentSegmentDistance(first, second), 1e-4f);
    }

    [Fact]
    public void CapsuleIntersection_Overlapping()
    {
        Capsule first = new(new Segment(new Vector2(-2f, 0f), new Vector2(2f, 0f)), 1f);
        Capsule second = new(new Segment(new Vector2(-2f, 1f), new Vector2(2f, 1f)), 1f);

        Assert.True(Collision.Intersects(first, second));
    }

    [Fact]
    public void CapsuleIntersection_Disjoint()
    {
        Capsule first = new(new Segment(new Vector2(-2f, 0f), new Vector2(2f, 0f)), 0.5f);
        Capsule second = new(new Segment(new Vector2(-2f, 5f), new Vector2(2f, 5f)), 0.5f);

        Assert.False(Collision.Intersects(first, second));
    }

    [Fact]
    public void Contains_RespectsRotation()
    {
        Vector2 point = new Vector2(1.2f, 0f).SafeNormalize();

        Assert.True(Collision.Contains(point, Vector2.Zero, new Vector2(2f, 2f), Angle.FromDegrees(30)));
        Assert.False(Collision.Contains(point, Vector2.Zero, new Vector2(0.5f, 0.5f), Angle.FromDegrees(30)));
    }

    [Fact]
    public void Distance_AppliesTolerance()
    {
        Assert.Equal(0f, Collision.Distance(Vector2.Zero, new Vector2(1e-9f, 0f)));
        Assert.Equal(1f, Collision.Distance(Vector2.Zero, new Vector2(1f, 0f)));
    }
}
