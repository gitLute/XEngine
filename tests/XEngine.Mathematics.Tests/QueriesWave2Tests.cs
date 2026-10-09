using System.Numerics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Исправления волны 2 для <c>Queries</c>: <see cref="Frustum"/> и
/// <see cref="Collision"/>.
///
/// Файл собран по одному правилу: у каждого исправления есть проверка,
/// которая падает на прежнем коде, и независимый эталон, который не
/// пересказывает формулу правимого кода. Эталон расстояний и пересечений
/// считается в <see cref="double"/> либо точным предикатом на знаках, а не
/// повторением строк библиотеки.
/// </summary>
public sealed class QueriesWave2Tests
{
    #region P1-6: сторож плоскости отсечения

    /// <summary>
    /// Матрица перспективы, собранная самой библиотекой, обязана давать
    /// фрустум при любом допустимом отношении дальней плоскости к ближней.
    /// До правки сторож сравнивал длину сырой суммы строк с <c>1e-6</c>,
    /// а её законная длина для дальней плоскости равна примерно
    /// <c>2·near/far</c>: при <c>far = 10 км</c> и <c>near = 1 м</c> это
    /// <c>2e-7</c>, и камера, собранная <see cref="Matrix4x4Extensions.CreatePerspective"/>,
    /// роняла движок исключением.
    /// </summary>
    [Fact]
    public void FromViewProjection_LongRangePerspectiveCameraIsAccepted()
    {
        Matrix4x4 projection = Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(60f), 1.6f, 1f, 1e7f);
        Matrix4x4 view = Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY);
        Matrix4x4 viewProjection = Matrix4x4Extensions.CreateViewProjection(view, projection);

        Frustum frustum = Frustum.FromViewProjection(viewProjection);

        Assert.Equal(6, frustum.Planes.Length);
        foreach (Plane3 plane in frustum.Planes)
        {
            Assert.True(float.IsFinite(plane.Normal.X) && float.IsFinite(plane.Normal.Y) && float.IsFinite(plane.Normal.Z),
                $"Нормаль плоскости нечисловая: {plane.Normal}.");
            Assert.True(float.IsFinite(plane.Distance), $"Свободный член нечисловой: {plane.Distance}.");
            MathAssert.Equal(1f, plane.Normal.Length(), 1e-4f);
        }
    }

    /// <summary>
    /// Ортогональная камера с той же природой границы: у неё дальняя
    /// плоскость тоже получается вычитанием почти равных строк.
    /// </summary>
    [Fact]
    public void FromViewProjection_LongRangeOrthographicCameraIsAccepted()
    {
        Matrix4x4 projection = Matrix4x4Extensions.CreateOrthographic(20f, 10f, 1f, 1e9f);
        Matrix4x4 view = Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY);
        Matrix4x4 viewProjection = Matrix4x4Extensions.CreateViewProjection(view, projection);

        Frustum frustum = Frustum.FromViewProjection(viewProjection);

        Assert.Equal(6, frustum.Planes.Length);
    }

    /// <summary>
    /// Принять камеру мало: фрустум на 10 км обязан по-прежнему отсекать.
    /// Проверяется не формула плоскостей, а признак отсечения на аналитически
    /// известных расстояниях: камера в начале координат смотрит вдоль +Z, её
    /// ближняя плоскость в 1 м, дальняя — в 10 км.
    /// </summary>
    [Fact]
    public void FromViewProjection_LongRangeFrustumStillCulls()
    {
        Matrix4x4 projection = Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(60f), 1.6f, 1f, 1e7f);
        Matrix4x4 view = Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY);
        Frustum frustum = Frustum.FromViewProjection(Matrix4x4Extensions.CreateViewProjection(view, projection));

        // Зад камеры: за ближней плоскостью по любой из шести.
        Assert.False(frustum.Intersects(new BoundingSphere(new Vector3(0f, 0f, -10f), 1f)));

        // Вдвое дальше дальней плоскости: 2e7 против 1e7.
        Assert.False(frustum.Intersects(new BoundingSphere(new Vector3(0f, 0f, 2e7f), 1f)));

        // Глубоко внутри диапазона: 1e6, то есть в сто раз ближе дальней плоскости.
        Assert.True(frustum.Intersects(new BoundingSphere(new Vector3(0f, 0f, 1e6f), 1f)));

        // В стороне от поля зрения на близком расстоянии.
        Assert.False(frustum.Intersects(new BoundingSphere(new Vector3(1e6f, 0f, 1e3f), 1f)));
    }

    /// <summary>
    /// Эталон для плоскостей отсечения: условие попадания точки в
    /// clip-пространство, посчитанное в <see cref="double"/>. Это определение
    /// пирамиды, а не пересказ формулы извлечения плоскостей, поэтому
    /// расхождение с библиотекой означало бы ошибку в одной из сторон.
    /// <para>
    /// Эталон сначала проверяется сам: на камере с обычным диапазоном он обязан
    /// совпасть с библиотекой. Иначе расхождение ниже доказывало бы не дефект
    /// кода, а неверный эталон.
    /// </para>
    /// </summary>
    [Fact]
    public void FromViewProjection_LongRangeFrustumAgreesWithClipSpaceTest()
    {
        // Сначала сам эталон на камере, где плоскости точно строятся.
        Matrix4x4 plainProjection = Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(60f), 1.6f, 1f, 1e3f);
        Matrix4x4 plainView = Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY);
        Matrix4x4 plain = Matrix4x4Extensions.CreateViewProjection(plainView, plainProjection);
        Frustum plainFrustum = Frustum.FromViewProjection(plain);

        int referencePoints = 0;
        for (int index = 0; index < 400; index++)
        {
            Vector3 point = SamplePoint(index, 1e3f);
            if (ReferenceIsInsideClipVolume(plain, point))
            {
                referencePoints++;
                Assert.True(plainFrustum.Intersects(new BoundingSphere(point, 0f)),
                    $"Эталон и библиотека разошлись на точке {point}: эталон внутри, фрустум отсекает.");
            }
            else
            {
                Assert.False(plainFrustum.Intersects(new BoundingSphere(point, 0f)),
                    $"Эталон и библиотека разошлись на точке {point}: эталон снаружи, фрустум отсекает.");
            }
        }

        Assert.True(referencePoints > 20, $"Эталон не проверен: внутри оказалось всего {referencePoints} точек из 400.");

        // Теперь тот же эталон на камере, которую до правки нельзя было построить.
        Matrix4x4 projection = Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(60f), 1.6f, 1f, 1e7f);
        Matrix4x4 view = Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY);
        Matrix4x4 longRange = Matrix4x4Extensions.CreateViewProjection(view, projection);
        Frustum frustum = Frustum.FromViewProjection(longRange);

        for (int index = 0; index < 400; index++)
        {
            Vector3 point = SamplePoint(index, 1e7f);
            if (ReferenceIsInsideClipVolume(longRange, point))
            {
                Assert.True(frustum.Intersects(new BoundingSphere(point, 0f)),
                    $"Фрустум на 10 км отсекает точку {point}, которую clip-эталон считает видимой.");
            }
            else
            {
                Assert.False(frustum.Intersects(new BoundingSphere(point, 0f)),
                    $"Фрустум на 10 км показывает точку {point}, которую clip-эталон считает невидимой.");
            }
        }
    }

    /// <summary>
    /// Плоскость, у которой сумма строк даёт нулевую нормаль, восстановить
    /// нельзя: это не «некорректная проекция», а потеря информации в самой
    /// матрице. Сторож обязан продолжать ловить такой случай, иначе вместо
    /// отказа появился бы фрустум без плоскости.
    /// </summary>
    [Fact]
    public void FromViewProjection_StillRejectsMatrixWithoutRecoverablePlane()
    {
        // Нулевая матрица: ни одной плоскости.
        Assert.Throws<InvalidOperationException>(
            () => Frustum.FromViewProjection(default(Matrix4x4)));

        // Отношение far/near = 1e8 при near = 0.1: в float элементы M33 и M34
        // совпадают побитово, и дальняя плоскость не выводится вовсе.
        Matrix4x4 projection = Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(60f), 1.6f, 0.1f, 1e7f);
        Matrix4x4 view = Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY);
        Matrix4x4 viewProjection = Matrix4x4Extensions.CreateViewProjection(view, projection);
        Assert.Throws<InvalidOperationException>(
            () => Frustum.FromViewProjection(viewProjection));
    }

    /// <summary>
    /// Бесконечная дальняя плоскость даёт в матрице нечисловые элементы, а
    /// фрустум из неё получается с нечисловыми нормалями: расстояние до
    /// плоскости равно NaN, сравнение с порогом ложно, и дальнее отсечение
    /// молча отключается. Такой вход обязан быть назван, а не прожжён.
    /// </summary>
    [Fact]
    public void FromViewProjection_RejectsInfiniteFarPlane()
    {
        Matrix4x4 projection = Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(60f), 1.6f, 0.1f, float.PositiveInfinity);
        Matrix4x4 view = Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY);
        Matrix4x4 viewProjection = Matrix4x4Extensions.CreateViewProjection(view, projection);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => Frustum.FromViewProjection(viewProjection));
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    /// <summary>
    /// Детерминированный backend обязан вести себя так же: извлечение плоскостей
    /// трансцендентных функций не вызывает, поэтому вердикт отсечения обязан
    /// совпасть с вариантом Fast. Проверка одна на оба варианта сборки.
    /// </summary>
    [Fact]
    public void FromViewProjection_LongRangeCameraIsAcceptedInBothBackends()
    {
        Matrix4x4 projection = Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(60f), 1.6f, 1f, 1e7f);
        Matrix4x4 view = Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY);

        Frustum frustum = Frustum.FromViewProjection(Matrix4x4Extensions.CreateViewProjection(view, projection));

        Assert.True(frustum.Intersects(new BoundingSphere(new Vector3(0f, 0f, 1e6f), 1f)));
    }

    /// <summary>
    /// Точки для сверки с clip-эталоном: детерминированная сетка без
    /// случайности, чтобы расхождение воспроизводилось.
    /// </summary>
    private static Vector3 SamplePoint(int index, float scale)
    {
        int a = (index * 37) % 23 - 11;
        int b = (index * 53) % 19 - 9;
        int c = (index * 71) % 29 - 14;

        return new Vector3(
            a * 0.37f * (scale / 8f),
            b * 0.41f * (scale / 8f),
            (c + 1.5f) * 0.29f * (scale / 8f));
    }

    /// <summary>
    /// Определение пирамиды через clip-пространство в двойной точности:
    /// точка видима, когда все её координаты после проекции лежат между
    /// <c>-w</c> и <c>+w</c>. Считается по правилам <c>System.Numerics</c>,
    /// где <c>Vector4.Transform</c> умножает вектор-строку на матрицу.
    /// </summary>
    private static bool ReferenceIsInsideClipVolume(Matrix4x4 m, Vector3 point)
    {
        double x = point.X, y = point.Y, z = point.Z;

        double cx = (x * m.M11) + (y * m.M21) + (z * m.M31) + m.M41;
        double cy = (x * m.M12) + (y * m.M22) + (z * m.M32) + m.M42;
        double cz = (x * m.M13) + (y * m.M23) + (z * m.M33) + m.M43;
        double cw = (x * m.M14) + (y * m.M24) + (z * m.M34) + m.M44;

        if (!(cw > 0.0))
        {
            return false;
        }

        return cx >= -cw && cx <= cw && cy >= -cw && cy <= cw && cz >= -cw && cz <= cw;
    }

    #endregion
}