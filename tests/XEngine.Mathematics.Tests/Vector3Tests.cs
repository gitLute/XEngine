using System.Numerics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Контракт <c>Vector3Extensions</c> (6.3). Имена операций различают, что именно
/// они преобразуют: точку, направление или нормаль (6.5, правило 6).
/// </summary>
public sealed class Vector3Tests
{
    [Fact]
    public void SafeNormalize_ReturnsUnitLength()
    {
        Vector3 result = new Vector3(3f, 0f, 4f).SafeNormalize();

        MathAssert.Equal(new Vector3(0.6f, 0f, 0.8f), result);
        MathAssert.Equal(1f, result.Length());
    }

    [Fact]
    public void SafeNormalize_KeepsZeroVector()
    {
        MathAssert.Equal(Vector3.Zero, Vector3.Zero.SafeNormalize());
    }

    [Fact]
    public void ClampLength_LimitsLongVectorAndKeepsShortOne()
    {
        Vector3 limited = new Vector3(30f, 40f, 0f).ClampLength(10f);
        MathAssert.Equal(new Vector3(6f, 8f, 0f), limited);

        Vector3 shortVector = new Vector3(1f, 0f, 0f);
        MathAssert.Equal(shortVector, shortVector.ClampLength(10f));
    }

    [Fact]
    public void ProjectOntoDirection_TakesComponentAlongDirection()
    {
        Vector3 result = new Vector3(2f, 3f, 0f).ProjectOntoDirection(new Vector3(1f, 0f, 0f));

        MathAssert.Equal(new Vector3(2f, 0f, 0f), result);
    }

    [Fact]
    public void ProjectOntoDirection_RejectsZeroDirection()
    {
        Assert.Throws<ArgumentException>(
            () => new Vector3(1f, 1f, 1f).ProjectOntoDirection(Vector3.Zero));
    }

    [Fact]
    public void ProjectOntoPlane_RemovesNormalComponent()
    {
        Vector3 point = new Vector3(1f, 2f, 3f);

        Vector3 projected = point.ProjectOntoPlane(Vector3.UnitY);

        MathAssert.Equal(new Vector3(1f, 0f, 3f), projected);
    }

    [Fact]
    public void ProjectOntoPlane_NormalizesNormalWithoutAskingCallerTo()
    {
        Vector3 point = new Vector3(0f, 5f, 0f);

        Vector3 projected = point.ProjectOntoPlane(new Vector3(0f, 100f, 0f));

        MathAssert.Equal(Vector3.Zero, projected);
    }

    [Fact]
    public void ProjectOntoPlane_RejectsZeroNormal()
    {
        Assert.Throws<ArgumentException>(
            () => new Vector3(1f, 1f, 1f).ProjectOntoPlane(Vector3.Zero));
    }

    [Fact]
    public void RejectFromPlane_KeepsOnlyNormalComponent()
    {
        Vector3 result = new Vector3(1f, 2f, 3f).RejectFromPlane(Vector3.UnitY);

        MathAssert.Equal(new Vector3(0f, 2f, 0f), result);
    }

    [Fact]
    public void ProjectAndReject_PartitionTheVector()
    {
        Vector3 point = new Vector3(1f, 2f, 3f);
        Vector3 normal = new Vector3(0f, 1f, 0f);

        Vector3 sum = point.ProjectOntoPlane(normal) + point.RejectFromPlane(normal);

        MathAssert.Equal(point, sum);
    }

    [Fact]
    public void MoveTowards_DoesNotOvershootTarget()
    {
        Vector3 start = Vector3.Zero;
        Vector3 target = new Vector3(10f, 0f, 0f);

        Vector3 step = start.MoveTowards(target, 3f);

        MathAssert.Equal(new Vector3(3f, 0f, 0f), step);
        MathAssert.Equal(target - Vector3.Zero, start.MoveTowards(target, 100f));
    }

    [Fact]
    public void RotateAround_TurnsPointAroundPivot()
    {
        Vector3 pivot = Vector3.Zero;

        Vector3 rotated = new Vector3(1f, 0f, 0f).RotateAround(pivot, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f));

        MathAssert.Equal(new Vector3(0f, 0f, -1f), rotated, MathAssert.LooseTolerance);
    }

    [Fact]
    public void RotateAround_KeepsPointOnPivot()
    {
        Vector3 pivot = new Vector3(5f, 5f, 5f);
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 1.1f);

        Vector3 rotated = pivot.RotateAround(pivot, rotation);

        MathAssert.Equal(pivot, rotated, MathAssert.LooseTolerance);
    }

    [Fact]
    public void ToAngle_ReturnsAzimuthInHorizontalPlane()
    {
        MathAssert.Equal(Angle.Zero, Vector3.UnitX.ToAngle());
        MathAssert.Equal(Angle.FromDegrees(90f), new Vector3(0f, 0f, 1f).ToAngle(), 1e-3);
    }

    [Fact]
    public void ToAngle_IgnoresVerticalComponent()
    {
        Angle flat = new Vector3(1f, 0f, 0f).ToAngle();
        Angle tilted = new Vector3(1f, 5f, 0f).ToAngle();

        MathAssert.Equal(flat, tilted);
    }

    [Fact]
    public void SignedAngleAround_MeasuresTurnAroundAxis()
    {
        // Поворот от +X к +Z вокруг +Y идёт по часовой стрелке при взгляде
        // сверху, поэтому знак отрицательный: правило правой руки.
        Angle angle = Vector3Extensions.SignedAngleAround(Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY);

        MathAssert.Equal(Angle.FromDegrees(-90f), angle, 1e-3);
    }

    [Fact]
    public void SignedAngleAround_MatchesRotationAroundSameAxis()
    {
        Angle yaw = Angle.FromDegrees(45f);
        Vector3 rotated = QuaternionExtensions.FromAxisAngle(Vector3.UnitY, yaw).Rotate(Vector3.UnitX);

        Angle angle = Vector3Extensions.SignedAngleAround(Vector3.UnitX, rotated, Vector3.UnitY);

        MathAssert.Equal(yaw, angle, 1e-3);
    }

    [Fact]
    public void SignedAngleAround_ChangesSignWithAxis()
    {
        Angle aroundPositiveY = Vector3Extensions.SignedAngleAround(Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY);
        Angle aroundNegativeY = Vector3Extensions.SignedAngleAround(Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitY);

        MathAssert.NearlyEqual(-aroundPositiveY.Radians, aroundNegativeY.Radians, 1e-3);
    }

    [Fact]
    public void SignedAngleAround_IsZeroForSameDirection()
    {
        Angle angle = Vector3Extensions.SignedAngleAround(Vector3.UnitX, Vector3.UnitX, Vector3.UnitY);

        MathAssert.Equal(Angle.Zero, angle);
    }

    [Fact]
    public void SignedAngleAround_RejectsZeroDirection()
    {
        Assert.Throws<ArgumentException>(
            () => Vector3Extensions.SignedAngleAround(Vector3.Zero, Vector3.UnitX, Vector3.UnitY));
        Assert.Throws<ArgumentException>(
            () => Vector3Extensions.SignedAngleAround(Vector3.UnitX, Vector3.Zero, Vector3.UnitY));
    }

    [Fact]
    public void Perpendicular_ReturnsVectorOrthogonalToInput()
    {
        Vector3 result = Vector3.UnitX.Perpendicular(Vector3.UnitY);

        MathAssert.Equal(0f, Vector3.Dot(Vector3.UnitX, result), MathAssert.LooseTolerance);
        MathAssert.Equal(1f, result.Length());
        MathAssert.Equal(Vector3.UnitY, result);
    }

    [Fact]
    public void Perpendicular_FallsBackWhenHintIsParallel()
    {
        Vector3 result = Vector3.UnitX.Perpendicular(Vector3.UnitX);

        MathAssert.Equal(1f, result.Length());
        MathAssert.Equal(0f, Vector3.Dot(Vector3.UnitX, result), MathAssert.LooseTolerance);
    }

    [Fact]
    public void Perpendicular_FallsBackWhenHintIsOppositeToInput()
    {
        Vector3 result = new Vector3(2f, 0f, 0f).Perpendicular(new Vector3(-2f, 0f, 0f));

        MathAssert.Equal(1f, result.Length());
        MathAssert.Equal(0f, Vector3.Dot(new Vector3(2f, 0f, 0f), result), MathAssert.LooseTolerance);
    }

    [Fact]
    public void FromSpherical_UsesPolarFromUpAndAzimuthFromXAxis()
    {
        Vector3 up = Vector3Extensions.FromSpherical(1f, Angle.Zero, Angle.Zero);
        MathAssert.Equal(Vector3.UnitY, up, MathAssert.LooseTolerance);

        Vector3 alongX = Vector3Extensions.FromSpherical(1f, Angle.FromDegrees(90f), Angle.Zero);
        MathAssert.Equal(Vector3.UnitX, alongX, MathAssert.LooseTolerance);

        Vector3 alongZ = Vector3Extensions.FromSpherical(1f, Angle.FromDegrees(90f), Angle.FromDegrees(90f));
        MathAssert.Equal(Vector3.UnitZ, alongZ, MathAssert.LooseTolerance);
    }

    [Fact]
    public void FromSpherical_ScalesWithRadius()
    {
        Vector3 result = Vector3Extensions.FromSpherical(5f, Angle.FromDegrees(90f), Angle.Zero);

        MathAssert.Equal(5f, result.Length(), MathAssert.LooseTolerance);
        MathAssert.Equal(new Vector3(5f, 0f, 0f), result, MathAssert.LooseTolerance);
    }

    [Fact]
    public void FromSpherical_RoundTripsAzimuthThroughToAngle()
    {
        Angle azimuth = Angle.FromDegrees(37f);

        Vector3 direction = Vector3Extensions.FromSpherical(2f, Angle.FromDegrees(90f), azimuth);

        MathAssert.Equal(azimuth, direction.ToAngle(), 1e-3);
    }

    [Fact]
    public void IsNearlyZero_ChecksAllComponents()
    {
        Assert.True(new Vector3(0f, 0f, 0f).IsNearlyZero());
        Assert.True(new Vector3(1e-9f, -1e-9f, 0f).IsNearlyZero());
        Assert.False(new Vector3(0.001f, 0f, 0f).IsNearlyZero());
    }
}