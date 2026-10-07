using System.Numerics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Контракт <c>QuaternionExtensions</c> (6.3). Порядок осей в <c>FromEuler</c>
/// зафиксирован в требованиях и проверяется тестом: расхождение порядка
/// незаметно в одном повороте и проявляется в composed-поворотах.
/// </summary>
public sealed class QuaternionTests
{
    private static readonly Quaternion _yawOnly =
        QuaternionExtensions.FromAxisAngle(Vector3.UnitY, Angle.FromDegrees(35f));

    private static readonly Quaternion _pitchOnly =
        QuaternionExtensions.FromAxisAngle(Vector3.UnitX, Angle.FromDegrees(-20f));

    private static readonly Quaternion _rollOnly =
        QuaternionExtensions.FromAxisAngle(Vector3.UnitZ, Angle.FromDegrees(10f));

    [Fact]
    public void FromAxisAngle_RotatesAroundNormalizedAxis()
    {
        Quaternion rotation = QuaternionExtensions.FromAxisAngle(new Vector3(0f, 5f, 0f), Angle.FromDegrees(90f));

        MathAssert.Equal(new Vector3(1f, 0f, 0f), rotation.Rotate(Vector3.UnitZ), MathAssert.LooseTolerance);
    }

    [Fact]
    public void FromAxisAngle_RejectsZeroAxis()
    {
        Assert.Throws<ArgumentException>(
            () => QuaternionExtensions.FromAxisAngle(Vector3.Zero, Angle.FromDegrees(45f)));
    }

    [Fact]
    public void Rotate_DoesNotApplyTranslation()
    {
        Quaternion rotation = QuaternionExtensions.FromAxisAngle(Vector3.UnitY, Angle.FromDegrees(90f));
        Vector3 point = new Vector3(2f, 3f, 4f);

        Vector3 rotated = rotation.Rotate(point);

        MathAssert.Equal(new Vector3(4f, 3f, -2f), rotated, MathAssert.LooseTolerance);
    }

    [Fact]
    public void Rotate_KeepsLengthAndIgnoresTranslationPart()
    {
        Matrix4x4 transform = Matrix4x4.CreateRotationY(0.7f) * Matrix4x4.CreateTranslation(100f, 50f, -30f);
        Quaternion rotation = Quaternion.CreateFromRotationMatrix(transform);
        Vector3 point = new Vector3(1f, 2f, 3f);

        Vector3 rotated = rotation.Rotate(point);

        MathAssert.Equal(point.Length(), rotated.Length(), MathAssert.LooseTolerance);
        Vector3 expected = Vector3.Transform(point, Matrix4x4.CreateRotationY(0.7f));
        MathAssert.Equal(expected, rotated, MathAssert.LooseTolerance);
    }

    [Fact]
    public void FromEuler_AppliesRollThenPitchThenYaw()
    {
        Quaternion yawOnly = QuaternionExtensions.FromEuler(Angle.FromDegrees(90f), Angle.Zero, Angle.Zero);
        MathAssert.Equal(Vector3.UnitX, yawOnly.Rotate(Vector3.UnitZ), MathAssert.LooseTolerance);

        Quaternion pitchOnly = QuaternionExtensions.FromEuler(Angle.Zero, Angle.FromDegrees(90f), Angle.Zero);
        MathAssert.Equal(new Vector3(0f, -1f, 0f), pitchOnly.Rotate(Vector3.UnitZ), MathAssert.LooseTolerance);

        Quaternion rollOnly = QuaternionExtensions.FromEuler(Angle.Zero, Angle.Zero, Angle.FromDegrees(90f));
        MathAssert.Equal(Vector3.UnitY, rollOnly.Rotate(Vector3.UnitX), MathAssert.LooseTolerance);
    }

    [Fact]
    public void FromEuler_ComposesYawPitchRollInSameOrder()
    {
        Angle yaw = Angle.FromDegrees(35f);
        Angle pitch = Angle.FromDegrees(-20f);
        Angle roll = Angle.FromDegrees(10f);

        Quaternion composed = _yawOnly * _pitchOnly * _rollOnly;

        MathAssert.Equal(composed, QuaternionExtensions.FromEuler(yaw, pitch, roll), MathAssert.LooseTolerance);
    }

    [Theory]
    [InlineData(35f, -20f, 10f)]
    [InlineData(0f, 0f, 0f)]
    [InlineData(120f, 45f, -75f)]
    [InlineData(-170f, -89f, 160f)]
    [InlineData(90f, 0f, 0f)]
    [InlineData(0f, 89f, 0f)]
    public void ToEuler_RoundTripsFromEuler(float yawDegrees, float pitchDegrees, float rollDegrees)
    {
        Angle yaw = Angle.FromDegrees(yawDegrees);
        Angle pitch = Angle.FromDegrees(pitchDegrees);
        Angle roll = Angle.FromDegrees(rollDegrees);

        (Angle outYaw, Angle outPitch, Angle outRoll) = QuaternionExtensions.ToEuler(
            QuaternionExtensions.FromEuler(yaw, pitch, roll));

        Quaternion restored = QuaternionExtensions.FromEuler(outYaw, outPitch, outRoll);
        Quaternion source = QuaternionExtensions.FromEuler(yaw, pitch, roll);

        // Сравнивается поворот, а не тройка углов: в вырожденном случае разбор
        // и восстановление дают другую равнозначную тройку углов.
        MathAssert.Equal(source, restored, MathAssert.LooseTolerance);
    }

    [Fact]
    public void ToEuler_ReturnsSameAnglesWhenNotDegenerate()
    {
        Angle yaw = Angle.FromDegrees(35f);
        Angle pitch = Angle.FromDegrees(-20f);
        Angle roll = Angle.FromDegrees(10f);

        (Angle outYaw, Angle outPitch, Angle outRoll) = QuaternionExtensions.ToEuler(
            QuaternionExtensions.FromEuler(yaw, pitch, roll));

        MathAssert.Equal(yaw, outYaw, 1e-3);
        MathAssert.Equal(pitch, outPitch, 1e-3);
        MathAssert.Equal(roll, outRoll, 1e-3);
    }

    [Fact]
    public void ToEuler_ReturnsIdentityForIdentityRotation()
    {
        (Angle yaw, Angle pitch, Angle roll) = QuaternionExtensions.ToEuler(Quaternion.Identity);

        MathAssert.Equal(Angle.Zero, yaw);
        MathAssert.Equal(Angle.Zero, pitch);
        MathAssert.Equal(Angle.Zero, roll);
    }

    [Fact]
    public void LookRotation_AlignsForwardAxisWithDirection()
    {
        Vector3 forward = Vector3Extensions.FromSpherical(1f, Angle.FromDegrees(70f), Angle.FromDegrees(40f));

        Quaternion rotation = QuaternionExtensions.LookRotation(forward, Vector3.UnitY);

        MathAssert.Equal(forward, rotation.Rotate(Vector3.UnitZ), MathAssert.LooseTolerance);
    }

    [Fact]
    public void LookRotation_KeepsUpAxisInUpperHemisphere()
    {
        // Взгляд строго вниз вырожден: вертикаль совпадает с направлением
        // взгляда, и ориентация вокруг оси Z произвольна. Поэтому берётся взгляд
        // близко к вертикали, но не совпадающий с ней.
        Vector3 forward = Vector3.Normalize(new Vector3(0.3f, -0.9f, 0.2f));

        Quaternion rotation = QuaternionExtensions.LookRotation(forward, Vector3.UnitZ);

        Vector3 up = rotation.Rotate(Vector3.UnitY);
        Assert.True(up.Y > 0f, $"Верхняя ось должна остаться вверх, получено {up}.");
    }

    [Fact]
    public void LookRotation_IsOrthonormalRotation()
    {
        Quaternion rotation = QuaternionExtensions.LookRotation(new Vector3(1f, 2f, 3f), Vector3.UnitY);

        Vector3 right = rotation.Rotate(Vector3.UnitX);
        Vector3 up = rotation.Rotate(Vector3.UnitY);
        Vector3 forward = rotation.Rotate(Vector3.UnitZ);

        MathAssert.Equal(1f, right.Length(), MathAssert.LooseTolerance);
        MathAssert.Equal(1f, up.Length(), MathAssert.LooseTolerance);
        MathAssert.Equal(1f, forward.Length(), MathAssert.LooseTolerance);
        MathAssert.Equal(0f, Vector3.Dot(right, up), MathAssert.LooseTolerance);
        MathAssert.Equal(0f, Vector3.Dot(right, forward), MathAssert.LooseTolerance);
        MathAssert.Equal(0f, Vector3.Dot(up, forward), MathAssert.LooseTolerance);
    }

    [Fact]
    public void LookRotation_RejectsDegenerateInput()
    {
        Assert.Throws<ArgumentException>(() => QuaternionExtensions.LookRotation(Vector3.Zero, Vector3.UnitY));
        Assert.Throws<ArgumentException>(() => QuaternionExtensions.LookRotation(Vector3.UnitY, Vector3.UnitY));
        Assert.Throws<ArgumentException>(() => QuaternionExtensions.LookRotation(Vector3.UnitZ, Vector3.Zero));
    }

    [Fact]
    public void Slerp_MatchesEndpointsAndMiddle()
    {
        Quaternion from = Quaternion.Identity;
        Quaternion to = QuaternionExtensions.FromAxisAngle(Vector3.UnitY, Angle.FromDegrees(60f));

        MathAssert.Equal(from, QuaternionExtensions.Slerp(from, to, 0f));
        MathAssert.Equal(to, QuaternionExtensions.Slerp(from, to, 1f), MathAssert.LooseTolerance);

        Quaternion middle = QuaternionExtensions.Slerp(from, to, 0.5f);
        float angleDegrees = (float)Scalar.ToDegrees(2.0 * Math.Acos(MathF.Min(1f, MathF.Abs(middle.W))));
        MathAssert.Equal(30f, angleDegrees, 1e-2f);
    }

    [Fact]
    public void Slerp_ClampsParameterOutsideUnitRange()
    {
        Quaternion from = Quaternion.Identity;
        Quaternion to = QuaternionExtensions.FromAxisAngle(Vector3.UnitY, Angle.FromDegrees(60f));

        MathAssert.Equal(from, QuaternionExtensions.Slerp(from, to, -5f));
        MathAssert.Equal(to, QuaternionExtensions.Slerp(from, to, 5f), MathAssert.LooseTolerance);
    }

    [Fact]
    public void Slerp_ReturnsNormalizedQuaternionForAntipodalInputs()
    {
        Quaternion from = Quaternion.Identity;
        Quaternion to = new Quaternion(0f, 0f, 0f, -1f);

        Quaternion result = QuaternionExtensions.Slerp(from, to, 0.5f);

        MathAssert.Equal(1f, result.Length(), MathAssert.LooseTolerance);
    }

    [Fact]
    public void AngleBetween_MeasuresSmallestRotationBetweenOrientations()
    {
        Quaternion from = Quaternion.Identity;
        Quaternion to = QuaternionExtensions.FromAxisAngle(Vector3.UnitY, Angle.FromDegrees(30f));

        MathAssert.Equal(Angle.FromDegrees(30f), QuaternionExtensions.AngleBetween(from, to), 1e-3);
    }

    [Fact]
    public void AngleBetween_IgnoresQuaternionSign()
    {
        Quaternion from = Quaternion.Identity;
        Quaternion to = QuaternionExtensions.FromAxisAngle(Vector3.UnitY, Angle.FromDegrees(30f));

        MathAssert.Equal(
            Angle.FromDegrees(30f),
            QuaternionExtensions.AngleBetween(from, -to),
            1e-3);
    }

    [Fact]
    public void Conjugate_NegatesVectorPart()
    {
        Quaternion source = new Quaternion(0.2f, 0.3f, 0.4f, 0.5f);

        Quaternion conjugate = QuaternionExtensions.Conjugate(source);

        MathAssert.Equal(new Quaternion(-0.2f, -0.3f, -0.4f, 0.5f), conjugate);
    }

    [Fact]
    public void Inverse_UndoesRotation()
    {
        Quaternion rotation = QuaternionExtensions.FromEuler(
            Angle.FromDegrees(25f),
            Angle.FromDegrees(-10f),
            Angle.FromDegrees(5f));
        Vector3 point = new Vector3(1f, 2f, 3f);

        Vector3 restored = rotation.Inverse().Rotate(rotation.Rotate(point));

        MathAssert.Equal(point, restored, MathAssert.LooseTolerance);
    }

    [Fact]
    public void Inverse_OfNonUnitRotationScalesCorrectly()
    {
        Quaternion nonUnit = new Quaternion(0f, 0f, 0f, 2f);

        Quaternion result = nonUnit.Inverse();

        Quaternion product = nonUnit * result;
        MathAssert.Equal(1f, product.W, MathAssert.LooseTolerance);
        MathAssert.Equal(0f, product.X, MathAssert.LooseTolerance);
        MathAssert.Equal(0f, product.Y, MathAssert.LooseTolerance);
        MathAssert.Equal(0f, product.Z, MathAssert.LooseTolerance);
    }

    [Fact]
    public void AngleBetween_Rotate_AndMatrixExtensionsAgree()
    {
        Quaternion rotation = QuaternionExtensions.FromEuler(
            Angle.FromDegrees(40f),
            Angle.FromDegrees(15f),
            Angle.FromDegrees(-5f));
        Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(rotation);
        Vector3 point = new Vector3(0.5f, -1.5f, 2f);

        MathAssert.Equal(Vector3.Transform(point, rotationMatrix), rotation.Rotate(point), MathAssert.LooseTolerance);
    }
}