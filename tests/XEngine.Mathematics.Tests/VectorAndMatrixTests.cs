using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

public sealed class VectorAndMatrixTests
{
    [Fact]
    public void WithDirection_PreservesLengthAndTakesNewAngle()
    {
        Vector2 source = new Vector2(2f, 0f);

        Vector2 result = VectorExtensions.WithDirection(source, Angle.FromDegrees(90));

        Assert.Equal(2f, result.Length(), 1e-5f);
        Assert.Equal(0f, result.X, 1e-5f);
        Assert.Equal(2f, result.Y, 1e-5f);
    }

    [Fact]
    public void Perpendicular_IsCounterClockwiseAndOrthogonal()
    {
        Vector2 source = new Vector2(1f, 0f);

        Vector2 result = VectorExtensions.Perpendicular(source);

        Assert.Equal(0f, Vector2.Dot(source, result), 1e-6f);
        Assert.Equal(0f, result.X, 1e-6f);
        Assert.Equal(1f, result.Y, 1e-6f);
    }

    [Fact]
    public void SafeNormalize_ReturnsZeroForZeroVector()
    {
        Assert.Equal(Vector2.Zero, VectorExtensions.SafeNormalize(Vector2.Zero));
    }

    [Fact]
    public void SafeNormalize_ReturnsUnitVector()
    {
        Vector2 result = VectorExtensions.SafeNormalize(new Vector2(3f, 4f));

        Assert.Equal(1f, result.Length(), 1e-5f);
    }

    [Fact]
    public void ClampLength_LimitsLengthButKeepsShortVectors()
    {
        Assert.Equal(5f, VectorExtensions.ClampLength(new Vector2(30f, 40f), 5f).Length(), 1e-4f);
        Assert.Equal(1f, VectorExtensions.ClampLength(new Vector2(1f, 0f), 5f).Length(), 1e-5f);
    }

    [Fact]
    public void Project_RemovesPerpendicularComponent()
    {
        Vector2 vector = new Vector2(2f, 3f);

        Vector2 result = VectorExtensions.Project(vector, Vector2.UnitX);

        Assert.Equal(2f, result.X, 1e-6f);
        Assert.Equal(0f, result.Y, 1e-6f);
    }

    [Fact]
    public void Project_RejectsZeroDirection()
    {
        Assert.Throws<ArgumentException>(() => VectorExtensions.Project(Vector2.UnitX, Vector2.Zero));
    }

    [Fact]
    public void MoveTowards_DoesNotOvershoot()
    {
        Vector2 from = Vector2.Zero;
        Vector2 to = new Vector2(1f, 0f);

        Vector2 result = VectorExtensions.MoveTowards(from, to, 5f);

        Assert.Equal(to, result);
    }

    [Fact]
    public void MoveTowards_LimitsStep()
    {
        Vector2 result = VectorExtensions.MoveTowards(Vector2.Zero, new Vector2(10f, 0f), 3f);

        Assert.Equal(3f, result.X, 1e-5f);
    }

    [Fact]
    public void IsNearlyZero_DetectsSmallVectors()
    {
        Assert.True(new Vector2(1e-9f, -1e-9f).IsNearlyZero());
        Assert.False(new Vector2(0.1f, 0f).IsNearlyZero());
    }

    [Fact]
    public void SignedDistanceToLine_ReturnsSignedOffset()
    {
        Vector2 origin = Vector2.Zero;
        Vector2 normal = Vector2.UnitY;

        Assert.Equal(5f, VectorExtensions.SignedDistanceToLine(new Vector2(0f, 5f), origin, normal), 1e-6f);
        Assert.Equal(-5f, VectorExtensions.SignedDistanceToLine(new Vector2(0f, -5f), origin, normal), 1e-6f);
    }

    [Fact]
    public void CreateTransform_AppliesScaleThenRotationThenTranslation()
    {
        Matrix3x2 matrix = Matrix3x2Extensions.CreateTransform(
            new Vector2(10f, 0f),
            Angle.FromDegrees(90),
            Vector2.One);

        Vector2 point = matrix.TransformPoint(Vector2.Zero);

        Assert.Equal(10f, point.X, 1e-5f);
        Assert.Equal(0f, point.Y, 1e-5f);

        Vector2 rotated = matrix.TransformPoint(Vector2.UnitX);
        Assert.Equal(10f, rotated.X, 1e-5f);
        Assert.Equal(1f, rotated.Y, 1e-5f);
    }

    [Fact]
    public void CreateTransform_KeepsPivotInPlace()
    {
        Vector2 pivot = new Vector2(5f, 5f);

        Matrix3x2 matrix = Matrix3x2Extensions.CreateTransform(Vector2.Zero, Angle.FromDegrees(90), Vector2.One, pivot);

        Vector2 result = matrix.TransformPoint(pivot);

        Assert.Equal(pivot.X, result.X, 1e-5f);
        Assert.Equal(pivot.Y, result.Y, 1e-5f);
    }

    [Fact]
    public void TransformDirection_IgnoresTranslation()
    {
        Matrix3x2 matrix = Matrix3x2.CreateTranslation(new Vector2(100f, 100f));

        Vector2 result = matrix.TransformDirection(Vector2.UnitX);

        Assert.Equal(1f, result.X, 1e-6f);
        Assert.Equal(0f, result.Y, 1e-6f);
    }

    [Fact]
    public void MatrixDecomposition_ReturnsOriginalValues()
    {
        Matrix3x2 matrix = Matrix3x2Extensions.CreateTransform(
            new Vector2(4f, -2f),
            Angle.FromDegrees(30),
            new Vector2(2f, 3f));

        Assert.Equal(4f, matrix.Translation().X, 1e-4f);
        Assert.Equal(-2f, matrix.Translation().Y, 1e-4f);
        Assert.Equal(30, matrix.Rotation().Degrees, 1e-3);
        Assert.Equal(2f, matrix.Scale().X, 1e-4f);
        Assert.Equal(3f, matrix.Scale().Y, 1e-4f);
    }

    [Fact]
    public void TryInvert_SucceedsForInvertibleMatrix()
    {
        Matrix3x2 matrix = Matrix3x2Extensions.CreateTransform(Vector2.One, Angle.FromDegrees(15), new Vector2(2f, 2f));

        Assert.True(matrix.TryInvert(out Matrix3x2 inverse));

        Vector2 point = new Vector2(7f, 9f);
        Vector2 roundTrip = inverse.TransformPoint(matrix.TransformPoint(point));

        Assert.Equal(point.X, roundTrip.X, 1e-3f);
        Assert.Equal(point.Y, roundTrip.Y, 1e-3f);
    }

    [Fact]
    public void TryInvert_FailsForSingularMatrix()
    {
        Matrix3x2 singular = Matrix3x2.CreateScale(0f, 0f);

        Assert.False(singular.TryInvert(out Matrix3x2 inverse));
        Assert.Equal(Matrix3x2.Identity, inverse);
    }

    [Fact]
    public void ToMatrix4x4_KeepsTranslationInLastRow()
    {
        Matrix3x2 matrix = Matrix3x2Extensions.CreateTransform(new Vector2(5f, 7f), Angle.Zero, Vector2.One);

        Matrix4x4 result = matrix.ToMatrix4x4();

        Assert.Equal(5f, result.M41, 1e-6f);
        Assert.Equal(7f, result.M42, 1e-6f);
        Assert.Equal(1f, result.M44, 1e-6f);
    }

    [Fact]
    public void ToColumnMajorArray_LaysColumnsFirst()
    {
        Matrix4x4 matrix = Matrix4x4.CreateTranslation(new Vector3(9f, 8f, 7f));

        float[] values = matrix.ToColumnMajorArray();

        Assert.Equal(16, values.Length);

        // Единичная матрица: диагональные элементы.
        Assert.Equal(1f, values[0], 1e-6f);
        Assert.Equal(1f, values[5], 1e-6f);
        Assert.Equal(1f, values[10], 1e-6f);
        Assert.Equal(1f, values[15], 1e-6f);

        // Перенос лежит в третьей строке, то есть в последнем элементе каждого столбца:
        // индексы 3, 7, 11, 15.
        Assert.Equal(9f, values[3], 1e-6f);
        Assert.Equal(8f, values[7], 1e-6f);
        Assert.Equal(7f, values[11], 1e-6f);
        Assert.Equal(1f, values[15], 1e-6f);
    }

    [Fact]
    public void CreateOrthographic2D_PutsOriginInCenter()
    {
        Matrix4x4 projection = MatrixExtensions.CreateOrthographic2D(100f, 50f);

        Vector4 center = Vector4.Transform(new Vector4(0f, 0f, 0f, 1f), projection);

        Assert.Equal(0f, center.X, 1e-5f);
        Assert.Equal(0f, center.Y, 1e-5f);

        Vector4 right = Vector4.Transform(new Vector4(50f, 0f, 0f, 1f), projection);
        Assert.Equal(1f, right.X, 1e-5f);

        Vector4 top = Vector4.Transform(new Vector4(0f, 25f, 0f, 1f), projection);
        Assert.Equal(1f, top.Y, 1e-5f);
    }

    [Fact]
    public void QuaternionConversion_IsConsistent()
    {
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI * 0.5f);

        Vector3 rotated = Vector3.Transform(Vector3.UnitX, Matrix4x4.CreateFromQuaternion(rotation));

        Assert.Equal(0f, rotated.X, 1e-5f);
        Assert.Equal(1f, rotated.Y, 1e-5f);
    }
}
