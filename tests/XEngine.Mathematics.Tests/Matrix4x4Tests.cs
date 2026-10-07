using System.Numerics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Контракт <c>Matrix4x4Extensions</c> (6.3, 6.5a).
/// </summary>
/// <remarks>
/// Соглашения проверяются на известных значениях, а не на согласованности с
/// BCL: формулы проекций в <c>System.Numerics</c> дают диапазон Z [0; 1],
/// тогда как clip space OpenGL требует [-1; 1]. Матрицы движка строятся
/// поэтому собственные, и тест фиксирует требуемые значения, а не те, что
/// получились бы вызовом готовой функции.
/// </remarks>
public sealed class Matrix4x4Tests
{
    private static readonly Vector3 _position = new(1f, 2f, 3f);

    private static float ClipDepth(in Matrix4x4 projection, float viewZ)
    {
        Vector4 clip = Vector4.Transform(new Vector4(0f, 0f, viewZ, 1f), projection);
        return MathF.Abs(clip.W) < Scalar.Epsilon ? float.NaN : clip.Z / clip.W;
    }

    [Fact]
    public void CreateTRS_AppliesScaleThenRotationThenTranslation()
    {
        Matrix4x4 trs = Matrix4x4Extensions.CreateTRS(
            new Vector3(0f, 0f, 10f),
            QuaternionExtensions.FromAxisAngle(Vector3.UnitY, Angle.FromDegrees(90f)),
            new Vector3(2f, 3f, 1f));

        // Масштаб: (1,1,0) -> (2,3,0); поворот на 90° вокруг Y по соглашению
        // System.Numerics (ось X уходит в -Z): -> (0,3,-2); перенос: -> (0,3,8).
        MathAssert.Equal(new Vector3(0f, 3f, 8f), trs.MultiplyPoint(new Vector3(1f, 1f, 0f)));
    }

    [Fact]
    public void CreateTRS_WithIdentityRotationMatchesScaleAndTranslate()
    {
        Matrix4x4 trs = Matrix4x4Extensions.CreateTRS(
            _position,
            Quaternion.Identity,
            new Vector3(2f, 2f, 2f));

        MathAssert.Equal(new Vector3(3f, 4f, 5f), trs.MultiplyPoint(new Vector3(1f, 1f, 1f)));
    }

    [Fact]
    public void CreateTRS_WithUnitScaleEqualsRotationAndTranslation()
    {
        Quaternion rotation = QuaternionExtensions.FromAxisAngle(Vector3.UnitZ, Angle.FromDegrees(37f));
        Vector3 point = new Vector3(0.5f, -1.5f, 2f);

        Matrix4x4 trs = Matrix4x4Extensions.CreateTRS(_position, rotation, Vector3.One);

        MathAssert.Equal(
            Vector3.Transform(point, Matrix4x4.CreateFromQuaternion(rotation)) + _position,
            trs.MultiplyPoint(point),
            MathAssert.LooseTolerance);
    }

    [Fact]
    public void CreateTRS_ScalesBeforeRotating()
    {
        // При обратном порядке точка ушла бы в (0,0,-1), а не в (0,0,-2).
        Quaternion rotation = QuaternionExtensions.FromAxisAngle(Vector3.UnitY, Angle.FromDegrees(90f));
        Matrix4x4 trs = Matrix4x4Extensions.CreateTRS(Vector3.Zero, rotation, new Vector3(2f, 1f, 1f));

        Vector3 scaleFirst = trs.MultiplyPoint(Vector3.UnitX);
        Vector3 rotateFirst = Matrix4x4.CreateScale(new Vector3(2f, 1f, 1f))
            .MultiplyPoint(rotation.Rotate(Vector3.UnitX));

        MathAssert.Equal(new Vector3(0f, 0f, -2f), scaleFirst);
        MathAssert.Equal(new Vector3(0f, 0f, -1f), rotateFirst);
        Assert.NotEqual(scaleFirst, rotateFirst);
    }

    [Fact]
    public void TryInvert_RoundTripsPointThroughTransform()
    {
        Matrix4x4 trs = Matrix4x4Extensions.CreateTRS(
            _position,
            QuaternionExtensions.FromEuler(Angle.FromDegrees(20f), Angle.FromDegrees(-15f), Angle.FromDegrees(5f)),
            new Vector3(2f, 3f, 4f));
        Vector3 point = new Vector3(1f, -2f, 0.5f);

        Assert.True(Matrix4x4Extensions.TryInvert(trs, out Matrix4x4 inverse));
        MathAssert.Equal(point, inverse.MultiplyPoint(trs.MultiplyPoint(point)), 1e-3f);
    }

    [Fact]
    public void TryInvert_ReturnsFalseForSingularMatrix()
    {
        Matrix4x4 singular = new(1f, 2f, 3f, 4f, 2f, 4f, 6f, 8f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f);

        // Значение out-параметра при отказе не задано: проверяется только факт
        // отказа, чтобы вызывающая сторона не полагалась на мусор в результате.
        Assert.False(Matrix4x4Extensions.TryInvert(singular, out _));
    }

    [Fact]
    public void TryInvert_MatchesFrameworkInvertOnSingularMatrix()
    {
        Matrix4x4 singular = Matrix4x4.CreateScale(Vector3.Zero);

        Assert.False(Matrix4x4Extensions.TryInvert(singular, out _));
        Assert.False(Matrix4x4.Invert(singular, out _), "Обёртка обязана вести себя как Matrix4x4.Invert.");
    }

    [Fact]
    public void CreatePerspective_MapsNearPlaneToMinusOneAndFarPlaneToPlusOne()
    {
        const float near = 1f;
        const float far = 100f;

        Matrix4x4 projection = Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(90f), 1f, near, far);

        MathAssert.Equal(-1f, ClipDepth(projection, -near), 1e-4f);
        MathAssert.Equal(1f, ClipDepth(projection, -far), 1e-4f);
    }

    [Fact]
    public void CreatePerspective_PutsViewOriginBehindNearPlaneAndPointsBehindCameraOutsideVolume()
    {
        Matrix4x4 projection = Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(60f), 16f / 9f, 0.5f, 500f);

        // Точка в начале видовых координат делит на ноль: она за ближней
        // плоскостью и отсекается.
        Assert.True(float.IsNaN(ClipDepth(projection, 0f)), "Начало видовых координат не должно попадать в отсек.");

        Vector4 behind = Vector4.Transform(new Vector4(0f, 0f, 1f, 1f), projection);
        Assert.True(behind.W < 0f, "Точка за камерой должна иметь отрицательный w и отсекаться.");
    }

    [Fact]
    public void CreatePerspective_KeepsAspectRatioInHorizontalScale()
    {
        Matrix4x4 square = Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(90f), 1f, 1f, 100f);
        Matrix4x4 wide = Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(90f), 2f, 1f, 100f);

        MathAssert.Equal(1f, square.M11, MathAssert.LooseTolerance);
        MathAssert.Equal(0.5f, wide.M11, MathAssert.LooseTolerance);
        MathAssert.Equal(square.M22, wide.M22, MathAssert.LooseTolerance);
    }

    [Fact]
    public void CreatePerspective_RejectsInvalidParameters()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Matrix4x4Extensions.CreatePerspective(Angle.Zero, 1f, 1f, 100f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(180f), 1f, 1f, 100f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(90f), 0f, 1f, 100f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(90f), 1f, 0f, 100f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(90f), 1f, 100f, 100f));
    }

    [Fact]
    public void CreateOrthographic_MapsPlanesAndKeepsMiddleOfDepthRangeAtZeroDepth()
    {
        const float near = 1f;
        const float far = 100f;

        Matrix4x4 projection = Matrix4x4Extensions.CreateOrthographic(4f, 2f, near, far);

        MathAssert.Equal(-1f, ClipDepth(projection, -near), 1e-4f);
        MathAssert.Equal(1f, ClipDepth(projection, -far), 1e-4f);

        // Нулевой глубине отсечённого объёма соответствует середина дистанции
        // между плоскостями, а не начало видовых координат: камера стоит
        // ближней плоскости.
        float middle = -(near + far) / 2f;
        MathAssert.Equal(0f, ClipDepth(projection, middle), 1e-4f);
        Assert.True(
            MathF.Abs(ClipDepth(projection, 0f)) > 1f,
            "Начало видовых координат находится за ближней плоскостью и не отображается в отсек.");
    }

    [Fact]
    public void CreateOrthographic_MapsRectangleCornersToClipSquare()
    {
        Matrix4x4 projection = Matrix4x4Extensions.CreateOrthographic(4f, 2f, 1f, 100f);

        Vector4 left = Vector4.Transform(new Vector4(-2f, -1f, -1f, 1f), projection);
        Vector4 right = Vector4.Transform(new Vector4(2f, 1f, -1f, 1f), projection);

        MathAssert.Equal(-1f, left.X, 1e-4f);
        MathAssert.Equal(-1f, left.Y, 1e-4f);
        MathAssert.Equal(1f, right.X, 1e-4f);
        MathAssert.Equal(1f, right.Y, 1e-4f);
    }

    [Fact]
    public void CreateOrthographic_RejectsInvalidParameters()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Matrix4x4Extensions.CreateOrthographic(0f, 2f, 1f, 100f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Matrix4x4Extensions.CreateOrthographic(4f, -2f, 1f, 100f));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Matrix4x4Extensions.CreateOrthographic(4f, 2f, 1f, 1f));
    }

    [Fact]
    public void CreateOrthographicByHeight_DerivesWidthFromAspect()
    {
        const float height = 6f;
        const float aspect = 16f / 9f;

        Matrix4x4 projection = Matrix4x4Extensions.CreateOrthographicByHeight(height, aspect, 1f, 100f);
        Matrix4x4 explicitRectangle = Matrix4x4Extensions.CreateOrthographic(height * aspect, height, 1f, 100f);

        MathAssert.Equal(explicitRectangle, projection);
    }

    [Fact]
    public void CreateLookAt_PutsCameraAxesOnTheirOwnScreenAxes()
    {
        Vector3 eye = new Vector3(4f, -3f, 7f);
        Vector3 target = new Vector3(-2f, 5f, 9f);
        Vector3 up = Vector3.UnitY;

        Matrix4x4 view = Matrix4x4Extensions.CreateLookAt(eye, target, up);

        Vector3 forward = Vector3.Normalize(target - eye);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, up));
        Vector3 trueUp = Vector3.Cross(right, forward);

        MathAssert.Equal(view.MultiplyPoint(eye), Vector3.Zero, 1e-3f);
        MathAssert.Equal(view.MultiplyPoint(eye + right), Vector3.UnitX, 1e-3f);
        MathAssert.Equal(view.MultiplyPoint(eye + trueUp), Vector3.UnitY, 1e-3f);
        MathAssert.Equal(view.MultiplyPoint(target), new Vector3(0f, 0f, -Vector3.Distance(eye, target)), 1e-3f);
    }

    [Fact]
    public void CreateLookAt_PutsWorldXAxisToTheLeftWhenLookingAlongZ()
    {
        // Камера смотрит вдоль +Z, её правый вектор равен -X, поэтому ось X
        // мира обязана оказаться слева. Ошибка знака здесь даёт зеркальный кадр.
        Matrix4x4 view = Matrix4x4Extensions.CreateLookAt(
            Vector3.Zero,
            new Vector3(0f, 0f, 1f),
            Vector3.UnitY);

        MathAssert.Equal(new Vector3(-1f, 0f, 0f), view.MultiplyPoint(Vector3.UnitX));
        MathAssert.Equal(new Vector3(0f, 1f, 0f), view.MultiplyPoint(Vector3.UnitY));
        MathAssert.Equal(new Vector3(0f, 0f, -1f), view.MultiplyPoint(Vector3.UnitZ));
    }

    [Fact]
    public void CreateLookAt_KeepsViewAndLookRotationConsistentAsHalfTurn()
    {
        // LookRotation поворачивает локальную ось Z модели в сторону взгляда,
        // а у вида ось Z направлена назад, поэтому поворот камеры отличается
        // от обратного поворота вида на полоборота вокруг вертикали. Тест
        // фиксирует связь, чтобы соглашение не «разъехалось» позже.
        Vector3 eye = new Vector3(3f, 2f, 1f);
        Vector3 target = new Vector3(-4f, 1f, 6f);
        Vector3 forward = Vector3.Normalize(target - eye);

        Quaternion look = QuaternionExtensions.LookRotation(forward, Vector3.UnitY);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        Vector3 trueUp = Vector3.Cross(right, forward);
        Quaternion camera = QuaternionExtensions.FromAxisAngle(trueUp, Angle.FromDegrees(180f)) * look;
        Matrix4x4 expected = Matrix4x4.CreateTranslation(-eye)
            * Matrix4x4.CreateFromQuaternion(camera.Inverse());

        MathAssert.Equal(expected, Matrix4x4Extensions.CreateLookAt(eye, target, Vector3.UnitY), 1e-3f);
    }

    [Fact]
    public void CreateLookAt_RejectsDegenerateInput()
    {
        Assert.Throws<ArgumentException>(
            () => Matrix4x4Extensions.CreateLookAt(Vector3.Zero, Vector3.Zero, Vector3.UnitY));
        Assert.Throws<ArgumentException>(
            () => Matrix4x4Extensions.CreateLookAt(Vector3.Zero, Vector3.UnitY, Vector3.UnitY));
    }

    [Fact]
    public void MultiplyPoint_AppliesTranslationAndMultiplyVector_DoesNot()
    {
        Matrix4x4 transform = Matrix4x4.CreateTranslation(_position)
            * Matrix4x4.CreateRotationX(MathF.PI / 2f);
        Vector3 point = new Vector3(1f, 0f, 0f);

        Vector3 withPoint = transform.MultiplyPoint(point);
        Vector3 withVector = transform.MultiplyVector(point);

        Assert.NotEqual(withPoint, withVector);
        MathAssert.Equal(Vector3.Transform(point, transform), withPoint);
        MathAssert.Equal(Vector3.TransformNormal(point, transform), withVector);
    }

    [Fact]
    public void GetTranslation_ReturnsTranslationPart()
    {
        Matrix4x4 trs = Matrix4x4Extensions.CreateTRS(_position, Quaternion.Identity, Vector3.One);

        MathAssert.Equal(_position, trs.GetTranslation());
    }

    [Fact]
    public void GetScale_ReturnsAxisLengths()
    {
        Matrix4x4 trs = Matrix4x4Extensions.CreateTRS(
            Vector3.Zero,
            QuaternionExtensions.FromEuler(Angle.FromDegrees(30f), Angle.FromDegrees(20f), Angle.FromDegrees(10f)),
            new Vector3(2f, 3f, 4f));

        MathAssert.Equal(new Vector3(2f, 3f, 4f), trs.GetScale(), 1e-3f);
    }

    [Fact]
    public void GetScale_SurvivesRotationWithoutScale()
    {
        Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(
            QuaternionExtensions.FromEuler(Angle.FromDegrees(17f), Angle.FromDegrees(-40f), Angle.FromDegrees(9f)));

        MathAssert.Equal(Vector3.One, rotation.GetScale(), 1e-3f);
    }

    [Fact]
    public void GetRotation_RoundTripsQuaternion()
    {
        Quaternion rotation = QuaternionExtensions.FromEuler(
            Angle.FromDegrees(35f),
            Angle.FromDegrees(-20f),
            Angle.FromDegrees(10f));

        Matrix4x4 matrix = Matrix4x4.CreateFromQuaternion(rotation);

        Quaternion restored = matrix.GetRotation();

        MathAssert.Equal(rotation, restored, 1e-4f);
    }

    [Fact]
    public void GetRotation_IgnoresUniformScale()
    {
        Quaternion rotation = QuaternionExtensions.FromEuler(
            Angle.FromDegrees(48f),
            Angle.FromDegrees(-25f),
            Angle.FromDegrees(15f));
        Matrix4x4 scaled = Matrix4x4Extensions.CreateTRS(_position, rotation, new Vector3(3f, 3f, 3f));

        Quaternion restored = scaled.GetRotation();

        MathAssert.Equal(rotation, restored, 1e-4f);
    }

    [Fact]
    public void TransformNormal_TransformsLikeDirectionForPureRotation()
    {
        Quaternion rotation = QuaternionExtensions.FromEuler(
            Angle.FromDegrees(33f),
            Angle.FromDegrees(-12f),
            Angle.FromDegrees(71f));
        Matrix4x4 transform = Matrix4x4.CreateFromQuaternion(rotation)
            * Matrix4x4.CreateTranslation(_position);
        Vector3 normal = new(1f, 0f, 0f);

        // Без масштаба нормаль преобразуется так же, как направление, иначе
        // после поворота она перестала бы быть перпендикулярной поверхности.
        MathAssert.Equal(rotation.Rotate(normal), transform.TransformNormal(normal), 1e-3f);
    }

    [Fact]
    public void TransformNormal_UsesInverseTransposeForNonUniformScale()
    {
        // Масштаб по X в два раза: нормаль (1,1,0) наклоняется вдвое сильнее
        // по Y, то есть её X-компонента делится на два.
        Matrix4x4 transform = Matrix4x4Extensions.CreateTRS(
            Vector3.Zero,
            Quaternion.Identity,
            new Vector3(2f, 1f, 1f));

        MathAssert.Equal(new Vector3(0.5f, 1f, 0f), transform.TransformNormal(new Vector3(1f, 1f, 0f)), 1e-4f);
    }

    [Fact]
    public void TransformNormal_RotatesNormalAfterNonUniformScale()
    {
        Quaternion rotation = QuaternionExtensions.FromAxisAngle(Vector3.UnitY, Angle.FromDegrees(45f));
        Matrix4x4 transform = Matrix4x4Extensions.CreateTRS(Vector3.Zero, rotation, new Vector3(2f, 1f, 1f));

        Vector3 result = transform.TransformNormal(Vector3.UnitX);

        // Нормаль локальной оси X после масштаба и поворота на 45° вокруг Y.
        MathAssert.Equal(new Vector3(MathF.Sqrt(0.5f) / 2f, 0f, -MathF.Sqrt(0.5f) / 2f), result, 1e-4f);
    }

    [Fact]
    public void TransformNormal_IgnoresTranslation()
    {
        Matrix4x4 transform = Matrix4x4.CreateTranslation(_position);

        MathAssert.Equal(Vector3.UnitX, transform.TransformNormal(Vector3.UnitX));
    }
}