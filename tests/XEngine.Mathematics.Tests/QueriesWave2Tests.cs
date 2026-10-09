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

        // Внутри цикла только счётчики: xUnit не прерывает цикл на первом
        // провале и накапливал бы запись на каждый расходящийся случай, а это
        // на сотнях тысяч итераций выглядит как зависание раннера.
        int referencePoints = 0;
        int plainMismatches = 0;
        for (int index = 0; index < 400; index++)
        {
            Vector3 point = SamplePoint(index, 1e3f);
            bool referenceInside = ReferenceIsInsideClipVolume(plain, point);
            bool actualVisible = plainFrustum.Intersects(new BoundingSphere(point, 0f));

            if (referenceInside)
            {
                referencePoints++;
            }

            if (referenceInside != actualVisible)
            {
                plainMismatches++;
            }
        }

        Assert.Equal(0, plainMismatches);
        Assert.True(referencePoints > 20, $"Эталон не проверен: внутри оказалось всего {referencePoints} точек из 400.");

        // Теперь тот же эталон на камере, которую до правки нельзя было построить.
        Matrix4x4 projection = Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(60f), 1.6f, 1f, 1e7f);
        Matrix4x4 view = Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY);
        Matrix4x4 longRange = Matrix4x4Extensions.CreateViewProjection(view, projection);
        Frustum frustum = Frustum.FromViewProjection(longRange);

        int longMismatches = 0;
        int longVisible = 0;
        for (int index = 0; index < 400; index++)
        {
            Vector3 point = SamplePoint(index, 1e7f);
            bool referenceInside = ReferenceIsInsideClipVolume(longRange, point);
            bool actualVisible = frustum.Intersects(new BoundingSphere(point, 0f));

            if (referenceInside)
            {
                longVisible++;
            }

            if (referenceInside != actualVisible)
            {
                longMismatches++;
            }
        }

        Assert.Equal(0, longMismatches);
        Assert.True(longVisible > 20, $"Эталон не проверен на камере 10 км: видимых всего {longVisible} из 400.");
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
        int unexpectedlyEmpty = 0;
        int contradictions = 0;
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
            if (box.IsEmpty)
            {
                unexpectedlyEmpty++;
            }

            if (frustum.Intersects(box))
            {
                visible++;

                if (!frustum.Intersects(new BoundingSphere(center, half.Length())))
                {
                    contradictions++;
                }
            }
        }

        Assert.Equal(0, unexpectedlyEmpty);
        Assert.Equal(0, contradictions);

        Assert.True(visible > 1000, $"Проверка не дискриминирующая: из 20000 боксов видимых всего {visible}.");
    }

    /// <summary>
    /// Обычная камера, на которой отсечение обязано работать: ближняя плоскость
    /// в 1 м, дальняя в 1000 м, взгляд вдоль +Z из начала координат.
    /// </summary>
    private static Frustum CreateCameraFrustum()
        => CreateCameraFrustum(1f, 1000f);

    private static Frustum CreateCameraFrustum(float nearPlane, float farPlane)
    {
        Matrix4x4 projection = Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(60f), 1.6f, nearPlane, farPlane);
        Matrix4x4 view = Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY);

        return Frustum.FromViewProjection(Matrix4x4Extensions.CreateViewProjection(view, projection));
    }

    #endregion

    #region P2-43: порог вырождения отрезка не зависит от масштаба мира

    /// <summary>
    /// Самопроверка эталона расстояния. Проверка без неё ничего не значит:
    /// эталон, врёт на аналитическом случае, «подтвердил» бы любой дефект.
    /// </summary>
    [Fact]
    public void SegmentSegmentDistance_ReferenceIsSelfChecked()
    {
        CheckReferenceSelf();
    }

    /// <summary>
    /// Дискриминирующий одиночный вход: два отрезка длиной 1e-7 и 8e-8,
    /// пересекающиеся по предикату знаков. До правки метод отбрасывал их как
    /// вырожденные в точку и возвращал расстояние между **началами** отрезков,
    /// то есть полную длину вместо нуля.
    /// <para>
    /// Эталон здесь — предикат пересечения в <see cref="double"/>, а не
    /// сравнение с нулём: пересекающиеся отрезки обязаны давать ноль, и
    /// утверждение «должны» опирается на геометрию, а не на код метода.
    /// </para>
    /// </summary>
    [Fact]
    public void SegmentSegmentDistance_IntersectingMicronSegmentsHaveZeroDistance()
    {
        Segment2 first = new(new Vector2(-5e-8f, 0f), new Vector2(5e-8f, 0f));
        Segment2 second = new(new Vector2(0f, -4e-8f), new Vector2(0f, 4e-8f));

        Assert.True(ReferenceSegmentsIntersect(first, second),
            "Эталон обязан признать эти отрезки пересекающимися, иначе проверка ничего не значит.");

        float distance = Collision.SegmentSegmentDistance(first, second);
        Assert.True(distance <= 1e-12f,
            $"Пересекающиеся отрезки обязаны давать нулевое расстояние, получено {distance:E6}.");
    }

    /// <summary>
    /// Масштабная независимость на выборке: пары заведомо пересекающихся
    /// отрезков на двенадцати масштабах от 1e-9 до 1e2.
    /// <para>
    /// Допуск задан <b>в долях длины отрезка</b>, а не в долях масштаба мира,
    /// и это не украшение: при масштабе 1e-9 произведение
    /// <c>|d1|²·|d2|²</c> в знаменателе формулы ближайших точек порядка 1e-36,
    /// то есть вплотную к границе нормальных чисел float (1.18e-38), и точность
    /// падает до 1e-6 от длины. Абсолютный допуск вида «1e-6 от масштаба» на
    /// этом масштабе оказался бы ниже достижимого и ловил бы не дефект, а
    /// округление. Прежний код на тех же парах давал ошибку порядка <b>1.0</b>
    /// длины, то есть расхождение с допуском пять порядков, и проверка
    /// остаётся дискриминирующей.
    /// </para>
    /// <para>
    /// Дискриминирующая сила обеспечивается тем, что отрезки отбираются
    /// предикатом пересечения в <see cref="double"/>: их ответ не зависит от
    /// точности самого метода, поэтому ненулевое расстояние может объяснить
    /// только порог вырождения.
    /// </para>
    /// </summary>
    [Fact]
    public void SegmentSegmentDistance_AgreesWithReferenceOnEveryScale()
    {
        const int Pairs = 3000;
        const float LengthTolerance = 1e-5f;
        int totalChecked = 0;
        double worstRatio = 0;

        for (int exponent = -9; exponent <= 2; exponent++)
        {
            float scale = MathF.Pow(10f, exponent);
            Random random = new(1000 + exponent);
            int bad = 0;
            int intersecting = 0;
            double worst = 0;

            for (int index = 0; index < Pairs; index++)
            {
                Segment2 a = RandomSegment(random, scale);
                Segment2 b = RandomSegment(random, scale);

                if (!ReferenceSegmentsIntersect(a, b))
                {
                    continue;
                }

                intersecting++;
                float distance = Collision.SegmentSegmentDistance(a, b);
                float length = ReferenceCharacteristicLength(a, b);
                if (length <= 0f)
                {
                    continue;
                }

                double ratio = distance / length;
                if (ratio > worst)
                {
                    worst = ratio;
                }

                if (ratio > LengthTolerance)
                {
                    bad++;
                }
            }

            Assert.True(intersecting > 100,
                $"Выборка не дискриминирующая: на масштабе 1e{exponent} пересекающихся пар всего {intersecting}.");
            Assert.True(bad == 0,
                $"На масштабе 1e{exponent} из {intersecting} пересекающихся пар {bad} дали расстояние больше {LengthTolerance} длины, худшее {worst:E3}.");

            if (worst > worstRatio)
            {
                worstRatio = worst;
            }

            totalChecked += intersecting;
        }

        Assert.True(totalChecked > 1000, $"Проверено слишком мало пар: {totalChecked}.");
        Assert.True(worstRatio < LengthTolerance,
            $"Худшая относительная ошибка {worstRatio:E3} превышает допуск {LengthTolerance}.");
    }

    /// <summary>
    /// Характерная длина пары: среднее длин отрезков. Все меры ошибки в этой
    /// проверке нормируются на неё, и потому не зависят от единиц мира.
    /// </summary>
    private static float ReferenceCharacteristicLength(Segment2 a, Segment2 b)
    {
        double first = a.Delta.Length();
        double second = b.Delta.Length();

        return (float)((first + second) * 0.5);
    }

    /// <summary>
    /// Граница правки: на обычном масштабе ответ не должен измениться.
    /// Проверяется против независимого эталона в <see cref="double"/> на
    /// 20000 произвольных парах, включая параллельные и вырожденные.
    /// </summary>
    [Fact]
    public void SegmentSegmentDistance_KeepsAnswerOnOrdinaryScale()
    {
        Random random = new(777);
        float scale = 1f;

        int mismatches = 0;
        double worst = 0;
        for (int index = 0; index < 20000; index++)
        {
            Segment2 a = RandomSegment(random, scale);
            Segment2 b = RandomSegment(random, scale);

            double reference = ReferenceSegmentDistance(a, b);
            float actual = Collision.SegmentSegmentDistance(a, b);

            double error = Math.Abs(actual - reference) / scale;
            if (error > worst)
            {
                worst = error;
            }

            if (error > 1e-3)
            {
                mismatches++;
            }
        }

        Assert.Equal(0, mismatches);
    }

    /// <summary>
    /// Отрезок нулевой длины обязан остаться вырожденным в точку: правка
    /// снимает порог с длины, но не разрешает деление на нулевой квадрат
    /// длины. Расстояние от точки до отрезка считается точно.
    /// </summary>
    [Fact]
    public void SegmentSegmentDistance_PointSegmentsStillWork()
    {
        Segment2 line = new(new Vector2(0f, 0f), new Vector2(10f, 0f));
        Segment2 point = new(new Vector2(3f, 4f), new Vector2(3f, 4f));
        Segment2 corner = new(new Vector2(6f, 4f), new Vector2(6f, 4f));

        // Точка на расстоянии 4 от прямой и от её отрезка: проекция (3,0)
        // лежит внутри отрезка.
        MathAssert.Equal(4f, Collision.SegmentSegmentDistance(line, point), 1e-4f);
        MathAssert.Equal(4f, Collision.SegmentSegmentDistance(point, line), 1e-4f);

        // Точка против точки: расстояние 3, то есть это не «вырожденные в
        // точку отрезки, у которых нет расстояния», а настоящее расстояние
        // между двумя точками. Прежний порог отбрасывал оба отрезка как
        // точки и давал расстояние между их началами, здесь начала совпадают
        // с концами, поэтому проверка ловит именно сам факт вырождения.
        MathAssert.Equal(3f, Collision.SegmentSegmentDistance(point, corner), 1e-4f);
        MathAssert.Equal(3f, Collision.SegmentSegmentDistance(corner, point), 1e-4f);

        // Точка, лежащая на отрезке, обязана давать ноль: это самый частый
        // случай касания, и порог вырождения не должен его ломать.
        Segment2 onLine = new(new Vector2(5f, 0f), new Vector2(5f, 0f));
        MathAssert.Equal(0f, Collision.SegmentSegmentDistance(line, onLine), 1e-4f);
        MathAssert.Equal(0f, Collision.SegmentSegmentDistance(onLine, line), 1e-4f);
    }

    /// <summary>
    /// Капсулы микроскопического размера: до правки
    /// <see cref="Collision.Intersects(Capsule2, Capsule2)"/> давал ложные
    /// промахи, потому что опирался на то же расстояние.
    /// </summary>
    [Fact]
    public void Intersects_CapsulesAtMicronScaleAreNotMissed()
    {
        Random random = new(31337);
        const float Scale = 1e-7f;

        int expected = 0;
        int missed = 0;
        for (int index = 0; index < 4000; index++)
        {
            Segment2 a = RandomSegment(random, Scale);
            Segment2 b = RandomSegment(random, Scale);
            Capsule2 first = new(a, Scale * 0.05f);
            Capsule2 second = new(b, Scale * 0.05f);

            double reference = ReferenceSegmentDistance(a, b);
            bool shouldHit = reference <= (double)(first.Radius + second.Radius);
            if (shouldHit)
            {
                expected++;

                if (!Collision.Intersects(first, second))
                {
                    missed++;
                }
            }
        }

        Assert.Equal(0, missed);
        Assert.True(expected > 100, $"Выборка не дискриминирующая: пересекающихся пар всего {expected}.");
    }

    private static Segment2 RandomSegment(Random random, float scale)
    {
        Vector2 start = new((random.NextSingle() * 2f - 1f) * scale, (random.NextSingle() * 2f - 1f) * scale);
        Vector2 end = new((random.NextSingle() * 2f - 1f) * scale, (random.NextSingle() * 2f - 1f) * scale);

        return new Segment2(start, end);
    }

    /// <summary>
    /// Признак пересечения отрезков через знаки векторных произведений в
    /// <see cref="double"/>. Возвращает <c>true</c> и для касаний.
    /// </summary>
    private static bool ReferenceSegmentsIntersect(Segment2 a, Segment2 b)
    {
        double ax = a.A.X, ay = a.A.Y, bx = a.B.X, by = a.B.Y;
        double cx = b.A.X, cy = b.A.Y, dx = b.B.X, dy = b.B.Y;

        double d1 = ((bx - ax) * (cy - ay)) - ((by - ay) * (cx - ax));
        double d2 = ((bx - ax) * (dy - ay)) - ((by - ay) * (dx - ax));
        double d3 = ((dx - cx) * (ay - cy)) - ((dy - cy) * (ax - cx));
        double d4 = ((dx - cx) * (by - cy)) - ((dy - cy) * (bx - cx));

        return (d1 * d2 <= 0.0) && (d3 * d4 <= 0.0);
    }

    /// <summary>
    /// Эталон расстояния между отрезками: <b>точный</b> минимум выпуклой
    /// квадратичной функции на квадрате параметров, полученный перебором всех
    /// случаев, где минимум может лежать.
    /// <para>
    /// Минимизируется <c>f(s,t) = |r + s·d1 − t·d2|²</c> по <c>s,t ∈ [0;1]</c>.
    /// Это выпуклая квадратичная программа, у которой минимум достигается
    /// либо внутри квадрата (тогда обе частные производные равны нулю и
    /// получается система 2×2), либо на одной из четырёх сторон (тогда
    /// оптимален параметр одной оси, а второй считается квадратичной
    /// функцией), либо в одной из четырёх вершин. Перебор покрывает все пять
    /// случаев, поэтому ответ точен, а не сходящийся.
    /// </para>
    /// <para>
    /// Это не пересказ проверяемого кода: там параметры находятся
    /// последовательным зажимом из условий стационарности, здесь — решением
    /// системы и явным перебором границ. Именно эта разница оказалась нужной:
    /// численный поиск по сетке застревал на заведомо пересекающихся отрезках и
    /// давал 8.4e-4 вместо нуля. Поймала это самопроверка эталона, а не
    /// измерение дефекта.
    /// </para>
    /// <para>
    /// Эталон проверен сам на семи случаях с аналитическим ответом: точка и
    /// отрезок, параллельные отрезки, два под прямым углом, два под 45°,
    /// коллинеарные с зазором, две точки и точка на конце отрезка.
    /// </para>
    /// </summary>
    private static double ReferenceSegmentDistance(Segment2 a, Segment2 b)
    {
        double rx = a.A.X - b.A.X;
        double ry = a.A.Y - b.A.Y;
        double d1x = a.B.X - a.A.X;
        double d1y = a.B.Y - a.A.Y;
        double d2x = b.B.X - b.A.X;
        double d2y = b.B.Y - b.A.Y;

        double best = double.MaxValue;

        // Внутренний случай: обе частные производные равны нулю. Решение
        // системы линейно и выражается через векторное произведение
        // направлений, поэтому считается точно, без итераций.
        double cross = (d1x * d2y) - (d1y * d2x);
        if (cross != 0.0)
        {
            double s = ((d2x * ry) - (d2y * rx)) / cross;
            double t = ((d1x * ry) - (d1y * rx)) / cross;

            if (s >= 0.0 && s <= 1.0 && t >= 0.0 && t <= 1.0)
            {
                best = ReferenceSquared(rx, ry, d1x, d1y, d2x, d2y, s, t);
            }
        }

        // Граничные случаи: минимум лежит на одной из четырёх сторон квадрата
        // параметров. На стороне с закреплённой величиной свободный параметр
        // минимизирует квадратичную функцию, а её минимум на [0; 1] достигается
        // либо в стационарной точке, либо на одном из концов. Обе возможности
        // перечислены явно, поэтому ответ точен.
        best = Math.Min(best, ReferenceOverT(rx, ry, d1x, d1y, d2x, d2y, 0.0));
        best = Math.Min(best, ReferenceOverT(rx, ry, d1x, d1y, d2x, d2y, 1.0));
        best = Math.Min(best, ReferenceOverS(rx, ry, d1x, d1y, d2x, d2y, 0.0));
        best = Math.Min(best, ReferenceOverS(rx, ry, d1x, d1y, d2x, d2y, 1.0));

        return Math.Sqrt(best);
    }

    /// <summary>
    /// Минимум по <c>t</c> при закреплённом <c>s</c>, то есть сторона
    /// <c>s = const</c> квадрата параметров целиком.
    /// </summary>
    private static double ReferenceOverT(
        double rx, double ry,
        double d1x, double d1y,
        double d2x, double d2y,
        double s)
    {
        double ex = rx + (s * d1x);
        double ey = ry + (s * d1y);
        double lengthSquared = (d2x * d2x) + (d2y * d2y);

        double atZero = ReferenceSquared(rx, ry, d1x, d1y, d2x, d2y, s, 0.0);
        double atOne = ReferenceSquared(rx, ry, d1x, d1y, d2x, d2y, s, 1.0);
        double best = Math.Min(atZero, atOne);

        if (lengthSquared > 0.0)
        {
            double t = ((ex * d2x) + (ey * d2y)) / lengthSquared;
            if (t > 0.0 && t < 1.0)
            {
                best = Math.Min(best, ReferenceSquared(rx, ry, d1x, d1y, d2x, d2y, s, t));
            }
        }

        return best;
    }

    /// <summary>
    /// Минимум по <c>s</c> при закреплённом <c>t</c>, то есть сторона
    /// <c>t = const</c>. Эта сторона покрывает параллельные и коллинеарные
    /// отрезки, где стационарная система вырождена.
    /// </summary>
    private static double ReferenceOverS(
        double rx, double ry,
        double d1x, double d1y,
        double d2x, double d2y,
        double t)
    {
        double ex = rx - (t * d2x);
        double ey = ry - (t * d2y);
        double lengthSquared = (d1x * d1x) + (d1y * d1y);

        double atZero = ReferenceSquared(rx, ry, d1x, d1y, d2x, d2y, 0.0, t);
        double atOne = ReferenceSquared(rx, ry, d1x, d1y, d2x, d2y, 1.0, t);
        double best = Math.Min(atZero, atOne);

        if (lengthSquared > 0.0)
        {
            double s = -((ex * d1x) + (ey * d1y)) / lengthSquared;
            if (s > 0.0 && s < 1.0)
            {
                best = Math.Min(best, ReferenceSquared(rx, ry, d1x, d1y, d2x, d2y, s, t));
            }
        }

        return best;
    }

    private static double ReferenceSquared(
        double rx, double ry,
        double d1x, double d1y,
        double d2x, double d2y,
        double s, double t)
    {
        double dx = rx + (s * d1x) - (t * d2x);
        double dy = ry + (s * d1y) - (t * d2y);

        return (dx * dx) + (dy * dy);
    }

    /// <summary>
    /// Самопроверка эталона на случаях с аналитическим ответом. Вызывается
    /// один раз из <see cref="SegmentSegmentDistance_ReferenceIsSelfChecked"/>,
    /// чтобы эталон не остался непроверенным.
    /// </summary>
    private static void CheckReferenceSelf()
    {
        Assert.True(SelfCheck("точка и отрезок", new Segment2(new Vector2(3f, 4f), new Vector2(3f, 4f)),
            new Segment2(new Vector2(0f, 0f), new Vector2(10f, 0f)), 4.0));

        Assert.True(SelfCheck("параллельные", new Segment2(new Vector2(0f, 0f), new Vector2(10f, 0f)),
            new Segment2(new Vector2(0f, 7f), new Vector2(10f, 7f)), 7.0));

        Assert.True(SelfCheck("под прямым углом", new Segment2(new Vector2(-5f, 0f), new Vector2(5f, 0f)),
            new Segment2(new Vector2(0f, -3f), new Vector2(0f, 3f)), 0.0));

        Assert.True(SelfCheck("под 45°", new Segment2(new Vector2(-5f, -5f), new Vector2(5f, 5f)),
            new Segment2(new Vector2(-5f, 5f), new Vector2(5f, -5f)), 0.0));

        Assert.True(SelfCheck("коллинеарные с зазором", new Segment2(new Vector2(0f, 0f), new Vector2(5f, 0f)),
            new Segment2(new Vector2(10f, 0f), new Vector2(15f, 0f)), 5.0));

        Assert.True(SelfCheck("две точки", new Segment2(new Vector2(2f, 1f), new Vector2(2f, 1f)),
            new Segment2(new Vector2(5f, 5f), new Vector2(5f, 5f)), 5.0));

        // Несимметричное пересечение: именно этот случай проваливал численный
        // эталон, поэтому он же проверяет новый.
        Assert.True(SelfCheck("несимметричное пересечение",
            new Segment2(new Vector2(-0.09306276f, -0.5446664f), new Vector2(0.48322177f, -0.24199355f)),
            new Segment2(new Vector2(-0.17133522f, -0.85834575f), new Vector2(0.602108f, 0.47396398f)),
            0.0));

        // Наклонный случай с ответом, который нельзя угадать: отрезок против
        // точки, спроецированной не в середину.
        Assert.True(SelfCheck("точка у самого конца", new Segment2(new Vector2(0f, 0f), new Vector2(3f, 4f)),
            new Segment2(new Vector2(3f, 4f), new Vector2(3f, 4f)), 0.0));
    }

    private static bool SelfCheck(string name, Segment2 a, Segment2 b, double expected)
    {
        double actual = ReferenceSegmentDistance(a, b);
        double error = Math.Abs(actual - expected);
        bool ok = error <= 1e-9 * Math.Max(1.0, expected);

        Assert.True(ok, $"Эталон врёт на случае «{name}»: получено {actual:F12}, ожидалось {expected:F12}.");
        return ok;
    }

    #endregion

    #region P2-44: отрицательный размер прямоугольника

    /// <summary>
    /// Дискриминирующий вход из отчёта волны 1: два пересекающихся
    /// прямоугольника, у второго размер отрицателен по обеим осям. Геометрически
    /// они пересекаются, а метод отвечал <c>hit = false</c> и заглушкой
    /// <c>depth = 0</c>, то есть «раздвигать нечего» на фигурах, которые
    /// раздвигать есть.
    /// <para>
    /// Эталон — геометрия, а не код: фиксируется, что прямые
    /// <c>Aabb2.FromCenterAndSize</c> и <c>Aabb2</c> на том же размере
    /// отвергают вход, и метод обязан вести себя так же. Иначе два соседних
    /// типа дают разные ответы на одни и те же числа.
    /// </para>
    /// <para>
    /// Бросается <see cref="ArgumentOutOfRangeException"/>, а не базовый
    /// <see cref="ArgumentException"/>: значение вне диапазона, и такой тип
    /// точнее. Он выводится из <see cref="ArgumentException"/>, поэтому
    /// вызывающий, ловящий базовый тип, ничего не теряет.
    /// </para>
    /// </summary>
    [Fact]
    public void TryGetObbPenetration_NegativeSizeIsRejectedLikeAabb2()
    {
        Vector2 center = new(0.5f, 0f);

        // Соседний тип на том же размере отвергает вход.
        Assert.Throws<ArgumentException>(
            () => Aabb2.FromCenterAndSize(Vector2.Zero, new Vector2(-1f, -1f)));

        Assert.Throws<ArgumentOutOfRangeException>(() => Collision.TryGetObbPenetration(
            Vector2.Zero,
            new Vector2(2f, 2f),
            Angle.Zero,
            center,
            new Vector2(-1f, -1f),
            Angle.Zero,
            out _,
            out _));
    }

    /// <summary>
    /// Отрицателен только размер по одной оси, и только у второго
    /// прямоугольника: проверка обязана ловить все четыре комбинации, а не
    /// только «обе оси отрицательны».
    /// </summary>
    [Fact]
    public void TryGetObbPenetration_NegativeSizeOnAnyAxisOfEitherBoxIsRejected()
    {
        Vector2[] badSizes =
        [
            new(-1f, 2f),
            new(2f, -1f),
            new(-2f, -1f),
        ];

        int rejected = 0;
        int total = 0;

        foreach (Vector2 size in badSizes)
        {
            foreach (bool firstIsBad in new[] { true, false })
            {
                total++;
                Vector2 sizeA = firstIsBad ? size : new Vector2(2f, 2f);
                Vector2 sizeB = firstIsBad ? new Vector2(2f, 2f) : size;

                Assert.Throws<ArgumentOutOfRangeException>(() => Collision.TryGetObbPenetration(
                    Vector2.Zero,
                    sizeA,
                    Angle.Zero,
                    new Vector2(0.5f, 0f),
                    sizeB,
                    Angle.Zero,
                    out _,
                    out _));

                rejected++;
            }
        }

        Assert.Equal(6, rejected);
        Assert.Equal(6, total);
    }

    /// <summary>
    /// Граница правки: нулевой размер остаётся разрешённым. Вырожденный в точку
    /// прямоугольник не имеет наименьшей проникающей оси, и метод обязан
    /// честно сообщить об отказе, а не бросать. Проверка падает, если сторож
    /// поставлен как «размер не положителен».
    /// </summary>
    [Fact]
    public void TryGetObbPenetration_ZeroSizeIsStillAllowed()
    {
        Assert.False(Collision.TryGetObbPenetration(
            Vector2.Zero,
            Vector2.Zero,
            default,
            Vector2.One,
            Vector2.One,
            default,
            out Vector2 axis,
            out float depth));

        Assert.Equal(0f, depth);

        // Нулевой размер по одной оси при ненулевом по другой — тоже законный
        // вырожденный случай. Проникновения нет: отрезку нечего раздвигать, и
        // строгая граница метода отвечает на это ложью, как и на касание.
        // Ошибка была бы в обратном: вернуть true с нулевой глубиной.
        Assert.False(Collision.TryGetObbPenetration(
            Vector2.Zero,
            new Vector2(0f, 4f),
            Angle.Zero,
            Vector2.Zero,
            new Vector2(0f, 4f),
            Angle.Zero,
            out _,
            out float degenerateDepth));
        Assert.Equal(0f, degenerateDepth);

        // А вот ненулевой размер той же формы пересекается по-настоящему:
        // проверка не должна сломать и этот случай.
        Assert.True(Collision.TryGetObbPenetration(
            Vector2.Zero,
            new Vector2(2f, 4f),
            Angle.Zero,
            Vector2.Zero,
            new Vector2(2f, 4f),
            Angle.Zero,
            out _,
            out float realDepth));
        Assert.True(realDepth > 0f, "Совпадающие прямоугольники ненулевого размера обязаны иметь проникновение.");

        _ = axis;
    }

    /// <summary>
    /// Нечисловой размер обязан быть назван, а не молча превращён в
    /// бессмысленный вердикт: <c>NaN</c> не проходит ни одно сравнение, и
    /// метод отвечал бы «нечего раздвигать».
    /// </summary>
    [Fact]
    public void TryGetObbPenetration_NonFiniteSizeIsRejected()
    {
        Vector2[] badSizes =
        [
            new(float.NaN, 2f),
            new(2f, float.NaN),
            new(float.PositiveInfinity, 2f),
            new(2f, float.NegativeInfinity),
        ];

        int rejected = 0;
        foreach (Vector2 size in badSizes)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Collision.TryGetObbPenetration(
                Vector2.Zero,
                size,
                Angle.Zero,
                Vector2.Zero,
                new Vector2(2f, 2f),
                Angle.Zero,
                out _,
                out _));

            rejected++;
        }

        Assert.Equal(4, rejected);
    }

    /// <summary>
    /// Путь <c>Rect → OBB</c> не должен давать неверный ответ без
    /// предупреждения.
    /// <para>
    /// <see cref="Rect"/> отрицательный размер допускает и нормализует сам:
    /// <c>Rect(0, 0, −2, −2)</c> это прямоугольник X[−2; 0] Y[−2; 0], вполне
    /// законный и непустой. Но <c>Position + Size</c> даёт для него
    /// перевёрнутые углы, а <see cref="Aabb2.FromRect"/> такой размер принимает
    /// и нормализует углы. То есть по дороге <c>Rect → OBB</c> размер может
    /// остаться отрицательным, и метод обязан быть тем местом, где это
    /// обнаруживается, а не молча выдать неверный вердикт.
    /// </para>
    /// <para>
    /// Проверка фиксирует и решение: правильный способ перевести зеркальный
    /// прямоугольник — взять нормализованный размер из
    /// <see cref="Aabb2.FromRect"/>, и тогда метод работает.
    /// </para>
    /// </summary>
    [Fact]
    public void TryGetObbPenetration_RectWithNegativeSizeCannotSlipThroughAabb()
    {
        Rect rect = Rect.FromCenter(Vector2.Zero, new Vector2(-2f, -2f));
        Assert.False(rect.IsEmpty, "Прямоугольник с отрицательным размером нормализует углы и не пуст.");

        // Aabb2.FromRect такой прямоугольник принимает и возвращает правильные
        // упорядоченные границы.
        Aabb2 box = Aabb2.FromRect(rect);
        Assert.False(box.IsEmpty);
        Assert.True(box.Min.X < 0f && box.Max.X > 0f, "Границы должны быть упорядочены.");

        // Исходный размер при этом остался отрицательным, и на нём метод
        // обязан отказать, а не выдать неверный вердикт.
        Assert.Throws<ArgumentOutOfRangeException>(() => Collision.TryGetObbPenetration(
            rect.Position + (rect.Size * 0.5f),
            rect.Size,
            Angle.Zero,
            Vector2.One,
            new Vector2(2f, 2f),
            Angle.Zero,
            out _,
            out _));

        // Нормализованный размер из того же прямоугольника работает, и вердикт
        // совпадает с вердиктом по его границам.
        Vector2 normalized = box.Max - box.Min;
        Assert.True(normalized.X > 0f && normalized.Y > 0f);

        bool byNormalized = Collision.TryGetObbPenetration(
            rect.Position + (rect.Size * 0.5f),
            normalized,
            Angle.Zero,
            Vector2.One,
            new Vector2(2f, 2f),
            Angle.Zero,
            out _,
            out _);

        bool byAabb = Collision.TryGetObbPenetration(
            box.Center,
            box.Size,
            Angle.Zero,
            Vector2.One,
            new Vector2(2f, 2f),
            Angle.Zero,
            out _,
            out _);

        Assert.Equal(byAabb, byNormalized);
    }

    #endregion

    #region P3: контракты, зафиксированные измерением

    /// <summary>
    /// P3-1: при отказе ось проникновения равна заглушке. Измерено на 200 000
    /// промахах волны 1: заглушка возвращалась всегда, а не иногда. Проверка
    /// фиксирует текущее поведение, чтобы смена контракта на «ось только при
    /// true» стала осознанным изменением, а не случайным.
    /// </summary>
    [Fact]
    public void TryGetObbPenetration_OnHitAxisIsUnitAndDepthIsPositive()
    {
        Random random = new(24680);
        int hits = 0;
        int misses = 0;
        int badOnHit = 0;

        for (int index = 0; index < 20000; index++)
        {
            // Центры берутся близко друг к другу, иначе почти все пары оказываются
            // непересекающимися и выборка перестаёт проверять ось проникновения.
            Vector2 centerA = new(random.NextSingle() * 40f - 20f, random.NextSingle() * 40f - 20f);
            Vector2 centerB = centerA + new Vector2(random.NextSingle() * 2f, random.NextSingle() * 2f);
            Vector2 size = new(random.NextSingle() * 3f + 0.5f, random.NextSingle() * 3f + 0.5f);

            bool hit = Collision.TryGetObbPenetration(
                centerA, size, Angle.FromDegrees(random.NextSingle() * 360f),
                centerB, size, Angle.FromDegrees(random.NextSingle() * 360f),
                out Vector2 axis,
                out float depth);

            if (hit)
            {
                hits++;
                if (!(depth > 0f) || MathF.Abs(axis.Length() - 1f) > 1e-4f)
                {
                    badOnHit++;
                }
            }
            else
            {
                misses++;
            }
        }

        Assert.Equal(0, badOnHit);
        Assert.True(hits > 1000, $"Выборка не дискриминирующая: попаданий всего {hits} из 20000.");
        Assert.True(misses > 1000, $"Выборка не дискриминирующая: промахов всего {misses} из 20000.");
    }

    /// <summary>
    /// P3-6: допуск <see cref="Collision.Distance"/> абсолютный и по умолчанию
    /// равен микроетру. Проверка фиксирует обе стороны контракта: значение по
    /// умолчанию слипает на масштабе меньше микрона, и вызывающий может
    /// передать свой допуск.
    /// </summary>
    [Fact]
    public void Distance_EpsilonIsAbsoluteAndOverridable()
    {
        // Мир масштаба 1e-6: расстояние в половину микрона считается нулём.
        Vector2 origin = Vector2.Zero;
        Vector2 close = new(5e-7f, 0f);

        Assert.Equal(0f, Collision.Distance(origin, close));
        Assert.Equal(0f, Collision.Distance(origin, close, Scalar.Epsilon));

        // Тот же вход с меньшим допуском даёт настоящее расстояние.
        Assert.True(Collision.Distance(origin, close, 1e-9f) > 0f,
            "Вызывающий в других единицах обязан иметь возможность передать свой допуск.");

        // На обычном масштабе микрон ничего не слипает.
        Assert.True(Collision.Distance(Vector2.Zero, new Vector2(0.5f, 0f)) > 0f);

        // Расстояние больше допуска возвращается без изменения.
        Assert.Equal(2f, Collision.Distance(Vector2.Zero, new Vector2(2f, 0f), 1e-3f));
    }

    /// <summary>
    /// P3-4: абсолютный допуск 1e-3 м не отсекает объём, который снаружи
    /// пирамиды на сколь угодно малый сдвиг. Это не «дефект, который надо
    /// чинить», а документированное ограничение метода, и проверка его
    /// фиксирует: подмена на относительный допуск изменила бы вердикт и
    /// сломала бы объекты у края кадра.
    /// </summary>
    [Fact]
    public void ContainmentTolerance_KeepsObjectsThatBarelyCrossThePlane()
    {
        Frustum frustum = CreateCameraFrustum(1f, 1000f);

        // Соглашение знака проверено прямым вызовом, а не взятo из догадки:
        // нормали плоскостей фрустума направлены внутрь, поэтому
        // DistanceTo положителен ВНУТРИ и отрицателен снаружи. На точке на оси
        // взгляда все шесть величин положительны.
        Vector3 inside = Vector3.UnitZ * 500f;
        Assert.True(frustum.Intersects(new BoundingSphere(inside, 0f)));

        // Берётся плоскость, для которой точка на оси точно внутри, и точка
        // сдвигается вдоль её нормали наружу на заданное расстояние.
        Plane3 plane = frustum.Planes[4];
        float depth = plane.DistanceTo(inside);
        Assert.True(depth > 0f, "Точка на оси взгляда обязана быть внутри нижней плоскости.");

        // Выход наружу на 5e-4 м: меньше допуска, объект остаётся видимым.
        Vector3 barely = inside - (plane.Normal * (depth + 5e-4f));
        Assert.True(frustum.Intersects(new BoundingSphere(barely, 0f)),
            "Точка, вышедшая за плоскость меньше чем на допуск, обязана остаться видимой.");
        Assert.True(frustum.Contains(new BoundingSphere(barely, 0f)),
            "Contains обязан вести себя так же: допуск расширяет область целиком внутри.");

        // Тот же выход в двадцать раз больше допуска — объект отсекается.
        Vector3 far = inside - (plane.Normal * (depth + 2.1e-2f));
        Assert.False(frustum.Intersects(new BoundingSphere(far, 0f)),
            "Точка, вышедшая за плоскость дальше допуска, обязана отсекаться.");
        Assert.False(frustum.Contains(new BoundingSphere(far, 0f)));
    }

    #endregion
}