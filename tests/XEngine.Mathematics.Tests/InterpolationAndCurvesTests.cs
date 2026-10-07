using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

public sealed class InterpolationAndCurvesTests
{
    [Fact]
    public void Lerp_ClampsParameter()
    {
        Assert.Equal(10f, Interpolation.Lerp(10f, 20f, -1f), 1e-5f);
        Assert.Equal(10f, Interpolation.Lerp(10f, 20f, 0f), 1e-5f);
        Assert.Equal(15f, Interpolation.Lerp(10f, 20f, 0.5f), 1e-5f);
        Assert.Equal(20f, Interpolation.Lerp(10f, 20f, 2f), 1e-5f);
    }

    [Fact]
    public void LerpUnclamped_AllowsOvershoot()
    {
        Assert.Equal(30f, Interpolation.LerpUnclamped(10f, 20f, 2f), 1e-5f);
    }

    [Fact]
    public void InverseLerp_IsInverseOfLerp()
    {
        float t = Interpolation.InverseLerp(10f, 20f, 12.5f);

        Assert.Equal(0.25f, t, 1e-5f);
    }

    [Fact]
    public void InverseLerp_RejectsEmptyRange()
    {
        Assert.Throws<ArgumentException>(() => Interpolation.InverseLerp(5f, 5f, 5f));
    }

    [Fact]
    public void MoveTowards_StopsExactlyAtTarget()
    {
        Assert.Equal(10f, Interpolation.MoveTowards(0f, 10f, 100f), 1e-5f);
    }

    [Fact]
    public void MoveTowards_LimitsStep()
    {
        Assert.Equal(3f, Interpolation.MoveTowards(0f, 10f, 3f), 1e-5f);
        Assert.Equal(-3f, Interpolation.MoveTowards(0f, -10f, 3f), 1e-5f);
    }

    [Fact]
    public void MoveTowardsAngle_TakesShortestPath()
    {
        Angle result = Interpolation.MoveTowardsAngle(Angle.FromDegrees(170), Angle.FromDegrees(-170), 5f);

        Assert.Equal(175, result.Degrees, 1e-6);
    }

    [Fact]
    public void Damp_IsFrameRateIndependent()
    {
        const float start = 0f;
        const float target = 100f;
        const float lambda = 5f;
        const float seconds = 1f;

        float at60Fps = start;
        int steps60 = (int)(seconds * 60);
        for (int i = 0; i < steps60; i++)
        {
            at60Fps = Interpolation.Damp(at60Fps, target, lambda, seconds / steps60);
        }

        float at30Fps = start;
        int steps30 = (int)(seconds * 30);
        for (int i = 0; i < steps30; i++)
        {
            at30Fps = Interpolation.Damp(at30Fps, target, lambda, seconds / steps30);
        }

        Assert.Equal(at60Fps, at30Fps, 1f);
        Assert.True(at60Fps > target * 0.9f);
    }

    [Fact]
    public void Damp_ConvergesToTarget()
    {
        float value = 0f;

        for (int i = 0; i < 600; i++)
        {
            value = Interpolation.Damp(value, 50f, 8f, 1f / 60f);
        }

        Assert.Equal(50f, value, 1e-2f);
    }

    [Fact]
    public void Damp_IsMonotonic()
    {
        float previous = -1f;

        for (int i = 0; i < 100; i++)
        {
            float value = Interpolation.Damp(previous, 1f, 4f, 1f / 60f);
            Assert.True(value > previous);
            previous = value;
        }
    }

    [Fact]
    public void Damp_IsStableForZeroDeltaTime()
    {
        float value = Interpolation.Damp(5f, 100f, 10f, 0f);

        Assert.Equal(5f, value, 1e-6f);
    }

    [Fact]
    public void Damp_WorksForVectors()
    {
        Vector2 result = Interpolation.Damp(Vector2.Zero, new Vector2(10f, 20f), 5f, 0.1f);

        Assert.True(result.X > 0f);
        Assert.True(result.Y > result.X);
    }

    [Fact]
    public void SmoothDamp_ApproachesTargetWithoutOvershoot()
    {
        float value = 0f;
        float velocity = 0f;

        for (int i = 0; i < 300; i++)
        {
            value = Interpolation.SmoothDamp(value, 10f, 0.3f, 100f, 1f / 60f, ref velocity);
            Assert.True(value <= 10f + 1e-3f);
        }

        Assert.Equal(10f, value, 1e-2f);
    }

    [Fact]
    public void Repeat_WrapsIntoRange()
    {
        Assert.Equal(0.25f, Interpolation.Repeat(4.25f, 1f), 1e-5f);
        Assert.Equal(0.75f, Interpolation.Repeat(-0.25f, 1f), 1e-5f);
        Assert.Equal(0.5f, Interpolation.Repeat(2.5f, 1f), 1e-5f);
    }

    [Fact]
    public void Repeat_RejectsNonPositiveLength()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Interpolation.Repeat(1f, 0f));
    }

    [Fact]
    public void PingPong_OscillatesBetweenZeroAndOne()
    {
        Assert.Equal(0f, Interpolation.PingPong(0f, 1f), 1e-5f);
        Assert.Equal(1f, Interpolation.PingPong(1f, 1f), 1e-5f);
        Assert.Equal(0f, Interpolation.PingPong(2f, 1f), 1e-5f);
        Assert.Equal(0.5f, Interpolation.PingPong(0.5f, 1f), 1e-5f);
    }

    [Fact]
    public void Remap_MapsBetweenRanges()
    {
        Assert.Equal(50f, Interpolation.Remap(5f, 0f, 10f, 0f, 100f), 1e-4f);
        Assert.Equal(0f, Interpolation.Remap(0f, 0f, 10f, 0f, 100f), 1e-4f);
        Assert.Equal(100f, Interpolation.Remap(10f, 0f, 10f, 0f, 100f), 1e-4f);
    }

    [Theory]
    [InlineData(-5f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(1f, 1f)]
    [InlineData(7f, 1f)]
    public void Clamp01_StaysInRange(float input, float expected)
    {
        Assert.Equal(expected, Interpolation.Clamp01(input), 1e-6f);
    }

    [Fact]
    public void Clamp_RejectsInvertedRange()
    {
        Assert.Throws<ArgumentException>(() => Interpolation.Clamp(1f, 5f, 0f));
    }

    [Fact]
    public void EaseCurves_StartAtZeroAndEndAtOne()
    {
        Assert.Equal(0f, Curves.InQuad(0f), 1e-6f);
        Assert.Equal(1f, Curves.InQuad(1f), 1e-6f);
        Assert.Equal(0f, Curves.OutCubic(0f), 1e-6f);
        Assert.Equal(1f, Curves.OutCubic(1f), 1e-6f);
        Assert.Equal(0f, Curves.InOutSine(0f), 1e-6f);
        Assert.Equal(1f, Curves.InOutSine(1f), 1e-6f);
        Assert.Equal(0f, Curves.OutBounce(0f), 1e-6f);
        Assert.Equal(1f, Curves.OutBounce(1f), 1e-6f);
    }

    [Fact]
    public void EaseCurves_AreMonotonicForAcceleratingShapes()
    {
        float previous = float.NegativeInfinity;
        for (int i = 0; i <= 20; i++)
        {
            float value = Curves.InCubic(i / 20f);
            Assert.True(value >= previous);
            previous = value;
        }
    }

    [Fact]
    public void Evaluate_ClampsParameter()
    {
        Assert.Equal(0f, Curves.Evaluate(Curves.InQuad, -1f), 1e-6f);
        Assert.Equal(1f, Curves.Evaluate(Curves.InQuad, 5f), 1e-6f);
    }

    [Fact]
    public void QuadraticBezier3_ReturnsEndpoints()
    {
        Vector3 a = new(0f, 0f, 0f);
        Vector3 b = new(0f, 10f, 0f);
        Vector3 c = new(10f, 0f, 0f);

        MathAssert.Equal(a, Curves.QuadraticBezier(a, b, c, 0f), 1e-4f);
        MathAssert.Equal(c, Curves.QuadraticBezier(a, b, c, 1f), 1e-4f);
    }

    [Fact]
    public void QuadraticBezier3_InterpolatesLinearlyWhenControlPointIsOnLine()
    {
        Vector3 a = new(0f, 0f, 0f);
        Vector3 b = new(5f, 0f, 0f);
        Vector3 c = new(10f, 0f, 0f);

        MathAssert.Equal(new Vector3(5f, 0f, 0f), Curves.QuadraticBezier(a, b, c, 0.5f), 1e-4f);
    }

    [Fact]
    public void QuadraticBezier3_StaysInsideControlPointHull()
    {
        Vector3 a = new(0f, 0f, 0f);
        Vector3 b = new(4f, 8f, 2f);
        Vector3 c = new(10f, 0f, -4f);

        for (int step = 0; step <= 10; step++)
        {
            Vector3 point = Curves.QuadraticBezier(a, b, c, step / 10f);
            Assert.InRange(point.X, -0.001f, 10.001f);
            Assert.InRange(point.Y, -0.001f, 8.001f);
            Assert.InRange(point.Z, -4.001f, 2.001f);
        }
    }

    [Fact]
    public void CubicBezier3_ReturnsEndpointsAndMiddleOfStraightLine()
    {
        Vector3 a = new(0f, 1f, 0f);
        Vector3 b = new(3f, 2f, 1f);
        Vector3 c = new(6f, 2f, 1f);
        Vector3 d = new(9f, 1f, 0f);

        MathAssert.Equal(a, Curves.CubicBezier(a, b, c, d, 0f), 1e-4f);
        MathAssert.Equal(d, Curves.CubicBezier(a, b, c, d, 1f), 1e-4f);

        // В середине параметра кубическая кривая равна взвешенному среднему
        // контрольных точек с весами 1, 3, 3, 1.
        MathAssert.Equal(new Vector3(4.5f, 1.75f, 0.75f), Curves.CubicBezier(a, b, c, d, 0.5f), 1e-4f);
    }

    [Fact]
    public void QuadraticBezier_ReturnsEndpoints()
    {
        Vector2 a = Vector2.Zero;
        Vector2 b = new Vector2(5f, 10f);
        Vector2 c = new Vector2(10f, 0f);

        Assert.Equal(a, Curves.QuadraticBezier(a, b, c, 0f));
        Assert.Equal(c, Curves.QuadraticBezier(a, b, c, 1f));
    }

    [Fact]
    public void QuadraticBezier_MidpointIsOnCurve()
    {
        Vector2 result = Curves.QuadraticBezier(
            Vector2.Zero, new Vector2(5f, 10f), new Vector2(10f, 0f), 0.5f);

        Assert.Equal(5f, result.X, 1e-4f);
        Assert.Equal(5f, result.Y, 1e-4f);
    }
}
