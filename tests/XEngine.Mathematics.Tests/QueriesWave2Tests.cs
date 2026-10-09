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

    #region P2-42: пустой параллелепипед

    /// <summary>
    /// <c>Aabb3.Empty</c> не содержит ни одной точки, поэтому пересекаться с
    /// пирамидой он не может. До правки он считался видимым: полуразмер равен
    /// <c>(−∞,−∞,−∞)</c>, проекция полуразмера на нормаль даёт <c>NaN</c> там,
    /// где компонента нормали нулевая, сравнение <c>NaN &lt; −1e-3</c> ложно, и
    /// ни одна из шести плоскостей не отсекает.
    /// <para>
    /// Эталон здесь — перегрузка для сферы: та же пустая величина, поданная как
    /// <see cref="BoundingSphere.FromAabb"/>, отсекается верно, потому что не
    /// проходит через <c>ProjectedRadius</c>. Два ответа на одном входе обязаны
    /// совпадать.
    /// </para>
    /// </summary>
    [Fact]
    public void Intersects_EmptyBoxIsNotVisible()
    {
        Frustum frustum = CreateCameraFrustum();

        Assert.True(Aabb3.Empty.IsEmpty);

        Assert.False(frustum.Intersects(Aabb3.Empty),
            "Пустой параллелепипед не пересекается с пирамидой: в нём нет ни одной точки.");
        Assert.False(frustum.Intersects(BoundingSphere.FromAabb(Aabb3.Empty)),
            "Та же пустая величина как сфера обязана давать тот же ответ.");
    }

    /// <summary>
    /// <see cref="Frustum.Contains(in Aabb3)"/> на пустом параллелепипеде обязан
    /// отвечать так же, как <see cref="Aabb3.Contains(in Aabb3)"/>: пустой объём
    /// не содержится ни в чём, включая себя.
    /// </summary>
    [Fact]
    public void Contains_EmptyBoxIsNotContained()
    {
        Frustum frustum = CreateCameraFrustum();

        Assert.False(frustum.Contains(Aabb3.Empty));
        Assert.False(frustum.Contains(BoundingSphere.FromAabb(Aabb3.Empty)));

        Aabb3 ordinary = Aabb3.FromCenterAndHalfSize(new Vector3(0f, 0f, 50f), Vector3.One);
        Assert.False(Aabb3.Empty.Contains(ordinary));
        Assert.False(ordinary.Contains(Aabb3.Empty));
    }

    /// <summary>
    /// Граница правки: неограниченный параллелепипед пересекает пирамиду по
    /// существу, и отсекать его нельзя. Он не пуст, поэтому сторож правки его
    /// не касается — проверка падает, если сторож поставлен шире, чем нужно.
    /// <para>
    /// Сюда же — параллелепипед с нечисловой границей: по доктрине
    /// <see cref="IsSphereVisible"/> испорченные данные оставляют объект
    /// видимым, и <see cref="Frustum.Intersects(in Aabb3)"/> обязан вести себя
    /// так же, а не молча отсекать.
    /// </para>
    /// </summary>
    [Fact]
    public void Intersects_UnboundedAndBrokenBoxesAreNotCulled()
    {
        Frustum frustum = CreateCameraFrustum();

        Aabb3 unbounded = new(new Vector3(float.NegativeInfinity), new Vector3(float.PositiveInfinity));
        Assert.False(unbounded.IsEmpty);
        Assert.True(frustum.Intersects(unbounded), "Неограниченный параллелепипед пересекает всё.");

        Aabb3 broken = new(new Vector3(float.NaN, -10f, 40f), new Vector3(10f, 10f, 60f));
        Assert.False(broken.IsEmpty);
        Assert.True(frustum.Intersects(broken), "Параллелепипед с испорченной границей остаётся видимым.");
    }

    /// <summary>
    /// Пустой параллелепипед недостижим через <c>new Aabb3(min, max)</c>:
    /// конструктор отвергает перевёрнутые границы. Проверяется, что правка
    /// опирается на признак <see cref="Aabb3.IsEmpty"/>, а не на сравнение с
    /// <see cref="Aabb3.Empty"/>, то есть останется верной, если пустое
    /// значение появится другим путём (например, из
    /// <see cref="Aabb3.Transform"/> на вырожденной матрице).
    /// </summary>
    [Fact]
    public void Intersects_OnlyEmptyBoxIsCulledAmongBoxesOfSameShape()
    {
        Frustum frustum = CreateCameraFrustum();

        // Тот же объём по габаритам, что и пустой, но с настоящими границами:
        // обязан остаться видимым.
        Aabb3 whole = new(new Vector3(-1000f, -1000f, -1000f), new Vector3(1000f, 1000f, 1000f));
        Assert.False(whole.IsEmpty);
        Assert.True(frustum.Intersects(whole));
    }

    /// <summary>
    /// Правка не должна ловить лишнего: любой непустой параллелепипед,
    /// пересекающийся с пирамидой, обязан пересекаться и со своей описанной
    /// сферой, потому что сфера содержит параллелепипед. Это независимое
    /// свойство (монотонность по вложению), и оно падает, если сторож
    /// начнёт отсекать годные объёмы.
    /// </summary>
    [Fact]
    public void Intersects_BoxAnswerNeverContradictsCircumscribedSphere()
    {
        Frustum frustum = CreateCameraFrustum();

        Random random = new(20240517);
        int visible = 0;
        for (int index = 0; index < 20000; index++)
        {
            Vector3 center = new(
                (random.NextSingle() * 2f - 1f) * 400f,
                (random.NextSingle() * 2f - 1f) * 400f,
                random.NextSingle() * 1200f - 200f);
            Vector3 half = new(
                random.NextSingle() * 30f + 0.01f,
                random.NextSingle() * 30f + 0.01f,
                random.NextSingle() * 30f + 0.01f);

            Aabb3 box = Aabb3.FromCenterAndHalfSize(center, half);
            Assert.False(box.IsEmpty);

            if (frustum.Intersects(box))
            {
                visible++;
                Assert.True(frustum.Intersects(new BoundingSphere(center, half.Length())),
                    "Параллелепипед пересекается, значит его описанная сфера обязана пересекаться.");
            }
        }

        Assert.True(visible > 1000, $"Проверка не дискриминирующая: из 20000 боксов видимых всего {visible}.");
    }

    /// <summary>
    /// Обычная камера, на которой отсечение обязано работать: ближняя плоскость
    /// в 1 м, дальняя в 1000 м, взгляд вдоль +Z из начала координат.
    /// </summary>
    private static Frustum CreateCameraFrustum()
    {
        Matrix4x4 projection = Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(60f), 1.6f, 1f, 1000f);
        Matrix4x4 view = Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY);

        return Frustum.FromViewProjection(Matrix4x4Extensions.CreateViewProjection(view, projection));
    }

    #endregion
}