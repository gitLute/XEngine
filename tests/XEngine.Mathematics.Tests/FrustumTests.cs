using System.Numerics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Контракт <c>Frustum</c>: шесть плоскостей из матрицы view * projection и
/// консервативная проверка видимости (10.11).
/// </summary>
/// <remarks>
/// Ложное отрицание недопустимо: объект, частично попавший в пирамиду, не
/// должен считаться невидимым. Поэтому объём на границе остаётся видимым, и
/// это закреплено тестом на касании.
/// <para>
/// Камера тестов смотрит вдоль <c>+Z</c>: точки перед ней имеют положительную
/// координату Z в мире и отрицательную в виде. Знак оси перепутанный отсечение
/// не поймало бы, поэтому он зафиксирован в каждом тесте именем переменной.
/// </para>
/// </remarks>
public sealed class FrustumTests
{
    private const float Near = 1f;
    private const float Far = 100f;

    /// <summary>
    /// Камера в начале координат смотрит вдоль +Z, вверх — +Y, угол обзора 90°.
    /// На расстоянии 10 метров половина отсека у кадра равна 10 метрам.
    /// </summary>
    private static Frustum CreateCameraFrustum()
        => Frustum.FromViewProjection(
            Matrix4x4Extensions.CreateViewProjection(
                Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY),
                Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(90f), 1f, Near, Far)));

    [Fact]
    public void FromViewProjection_PutsPointInFrontOfCameraInsideClipVolume()
    {
        Matrix4x4 viewProjection = Matrix4x4Extensions.CreateViewProjection(
            Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY),
            Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(90f), 1f, Near, Far));

        Vector4 clip = Vector4.Transform(new Vector4(0f, 0f, 10f, 1f), viewProjection);

        // Перепутанный порядок произведения даёт здесь отрицательный w, и всё
        // отсекается; положительный w означает, что точка действительно перед камерой.
        Assert.True(clip.W > 0f, $"Ожидался положительный w, получено {clip.W}.");
        float depth = clip.Z / clip.W;
        Assert.True(depth >= -1f && depth <= 1f, $"Глубина {depth} вне диапазона [-1; 1].");
    }

    [Fact]
    public void FromViewProjection_ProducesSixNormalizedPlanes()
    {
        Frustum frustum = CreateCameraFrustum();

        Assert.Equal(6, frustum.Planes.Length);
        foreach (Plane3 plane in frustum.Planes)
        {
            MathAssert.Equal(1f, plane.Normal.Length(), 1e-4f);
        }
    }

    [Fact]
    public void FromViewProjection_RejectsMatrixThatIsNotProjection()
    {
        // Нулевая матрица вырождает все шесть плоскостей: признак того, что
        // произведение вида и проекции не собрано.
        Assert.Throws<InvalidOperationException>(
            () => Frustum.FromViewProjection(default(Matrix4x4)));
    }

    [Fact]
    public void Contains_SphereInFrontOfCameraIsVisible()
    {
        Frustum frustum = CreateCameraFrustum();
        const float inFront = 10f;

        Assert.True(frustum.Contains(new BoundingSphere(new Vector3(0f, 0f, inFront), 1f)));
    }

    [Fact]
    public void Contains_SphereBehindCameraIsCulled()
    {
        Frustum frustum = CreateCameraFrustum();
        const float behind = -10f;

        Assert.False(frustum.Contains(new BoundingSphere(new Vector3(0f, 0f, behind), 1f)));
    }

    [Fact]
    public void Contains_SphereBeyondFarPlaneIsCulled()
    {
        Frustum frustum = CreateCameraFrustum();
        const float beyondFar = 200f;

        Assert.False(frustum.Contains(new BoundingSphere(new Vector3(0f, 0f, beyondFar), 1f)));
    }

    [Fact]
    public void Contains_SphereCrossingNearPlaneIsNotFullyInside()
    {
        Frustum frustum = CreateCameraFrustum();
        const float straddlesNear = 0.5f;
        BoundingSphere sphere = new(new Vector3(0f, 0f, straddlesNear), 1f);

        Assert.False(frustum.Contains(sphere), "Половина сферы за ближней плоскостью: внутрь она не помещается.");
        Assert.True(frustum.Intersects(sphere), "Но отсекать её нельзя: часть сферы в кадре.");
    }

    [Fact]
    public void Intersects_SphereTouchingSidePlaneStaysVisible()
    {
        Frustum frustum = CreateCameraFrustum();
        const float touching = 10.4f;

        Assert.True(
            frustum.Intersects(new BoundingSphere(new Vector3(touching, 0f, 10f), 0.5f)),
            "Сфера, касающаяся границы кадра на 10 метрах, не должна мерцать.");
    }

    [Fact]
    public void Intersects_SphereFullyOutsideSidePlaneIsCulled()
    {
        Frustum frustum = CreateCameraFrustum();

        // Граница кадра на расстоянии 10 проходит по X = 10, и плоскость
        // диагональна, поэтому 0.6 метра вбок — это 0.42 метра до плоскости.
        Assert.True(
            frustum.Intersects(new BoundingSphere(new Vector3(10.6f, 0f, 10f), 0.5f)),
            "Сфера пересекает наклонную плоскость и частью в кадре.");
        Assert.False(frustum.Intersects(new BoundingSphere(new Vector3(11f, 0f, 10f), 0.5f)));
    }

    [Fact]
    public void Intersects_SphereBehindCameraIsCulled()
    {
        Frustum frustum = CreateCameraFrustum();
        const float behind = -10f;

        Assert.False(frustum.Intersects(new BoundingSphere(new Vector3(0f, 0f, behind), 1f)));
    }

    [Fact]
    public void Contains_BoxInFrontOfCameraIsVisible()
    {
        Frustum frustum = CreateCameraFrustum();
        Aabb3 bounds = Aabb3.FromCenterAndSize(new Vector3(0f, 0f, 10f), new Vector3(2f, 2f, 2f));

        Assert.True(frustum.Contains(bounds));
    }

    [Fact]
    public void Intersects_BoxStraddlingCameraPlaneIsNotCulled()
    {
        Frustum frustum = CreateCameraFrustum();

        // Ящик накрывает ближнюю плоскость: часть за камерой, часть в кадре.
        Aabb3 bounds = Aabb3.FromCenterAndSize(new Vector3(0f, 0f, 5f), new Vector3(40f, 40f, 10f));

        Assert.True(frustum.Intersects(bounds));
    }

    [Fact]
    public void Intersects_BoxCrossingSidePlaneIsNotCulled()
    {
        Frustum frustum = CreateCameraFrustum();
        Aabb3 bounds = Aabb3.FromCenterAndSize(new Vector3(9f, 0f, 10f), new Vector3(4f, 4f, 4f));

        Assert.True(frustum.Intersects(bounds));
    }

    [Fact]
    public void Intersects_BoxFarOutsideSidePlaneIsCulled()
    {
        Frustum frustum = CreateCameraFrustum();
        Aabb3 bounds = Aabb3.FromCenterAndSize(new Vector3(20f, 0f, 10f), Vector3.One);

        Assert.False(frustum.Intersects(bounds));
    }

    [Fact]
    public void Intersects_SphereAndBoxAgreeOnSameVolume()
    {
        Frustum frustum = CreateCameraFrustum();

        foreach (float distance in new[] { 5f, 50f, 150f })
        {
            Vector3 center = new Vector3(0f, 0f, distance);
            BoundingSphere sphere = new(center, 1f);
            Aabb3 box = Aabb3.FromCenterAndSize(center, new Vector3(2f, 2f, 2f));

            Assert.True(
                frustum.Intersects(sphere) == frustum.Intersects(box),
                $"Сфера и ящик с тем же центром разошлись на расстоянии {distance}.");
        }
    }

    [Fact]
    public void FromViewProjection_MovesWithCamera()
    {
        Matrix4x4 projection = Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(90f), 1f, Near, Far);
        Matrix4x4 firstView = Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY);
        Matrix4x4 secondView = Matrix4x4Extensions.CreateLookAt(
            new Vector3(0f, 0f, 10f),
            new Vector3(0f, 0f, 11f),
            Vector3.UnitY);

        Frustum first = Frustum.FromViewProjection(Matrix4x4Extensions.CreateViewProjection(firstView, projection));
        Frustum second = Frustum.FromViewProjection(Matrix4x4Extensions.CreateViewProjection(secondView, projection));

        Assert.True(first.Intersects(new BoundingSphere(new Vector3(0f, 0f, 5f), 1f)));
        Assert.False(
            second.Intersects(new BoundingSphere(new Vector3(0f, 0f, 5f), 1f)),
            "Вторая камера стоит в точке этого объекта и смотрит дальше.");
        Assert.True(second.Intersects(new BoundingSphere(new Vector3(0f, 0f, 15f), 1f)));
    }

    [Fact]
    public void FromViewProjection_OrthographicKeepsVisibleVolume()
    {
        Matrix4x4 projection = Matrix4x4Extensions.CreateOrthographicByHeight(20f, 1f, 1f, 50f);
        Matrix4x4 view = Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY);

        Frustum frustum = Frustum.FromViewProjection(
            Matrix4x4Extensions.CreateViewProjection(
                Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY),
                projection));

        Assert.True(frustum.Intersects(new BoundingSphere(new Vector3(0f, 0f, 25f), 1f)));
        Assert.True(
            frustum.Intersects(new BoundingSphere(new Vector3(9f, 0f, 25f), 1f)),
            "Половина ширины вида при высоте 20 и отношении 1 равна 10.");
        Assert.False(frustum.Intersects(new BoundingSphere(new Vector3(12f, 0f, 25f), 1f)));
        Assert.False(frustum.Intersects(new BoundingSphere(new Vector3(0f, 0f, -5f), 1f)), "Объект за камерой отсекается.");
    }
}