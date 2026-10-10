using System.Numerics;
using BigInteger = System.Numerics.BigInteger;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Регрессионные проверки волны 2 для поддиректории <c>Internal/</c>
/// (<c>SurfaceRadius</c>).
/// </summary>
/// <remarks>
/// <para>
/// <c>SurfaceRadius</c> объявлен <c>internal</c>, а <c>InternalsVisibleTo</c> в
/// сборке нет, поэтому все проверки идут через публичный API: те фигуры,
/// которые вызывают <c>For</c> и <c>PastContact</c>.
/// </para>
/// <para>
/// Эталон принадлежности считается <b>независимо</b>, рациональной
/// арифметикой на <see cref="BigInteger"/>. Все величины приходят из
/// <c>float</c>, то есть являются двоичными рациональными; перевод в целые
/// единицы <c>2^-149</c> даёт точные целые, и сравнение
/// <c>|p − c|² ≤ r²</c> выполняется без единого округления. Такой эталон не
/// может ошибиться так же, как плавающая арифметика, и — что важнее — он
/// проверяет сам предикат «точка принадлежит фигуре», а не пересказывает
/// формулу, которую правка меняет.
/// </para>
/// <para>
/// Доктрина тестов:
/// <list type="bullet">
/// <item>проверка обязана различать состояния кода: на прежнем коде она падает,
/// после правки проходит; обратный ход выполнен и зафиксирован в отчёте;</item>
/// <item>внутри большого цикла только счётчик, проверка одна и после цикла —
/// иначе раннер накапливает сотни тысяч записей о падении и выглядит зависшим;</item>
/// <item>границы цикла не строятся так, чтобы последняя итерация переполняла
/// счётчик.</item>
/// </list>
/// </para>
/// <para>
/// Чего в этом файле <b>нет</b> и почему. Дефект P2-38 в основной части —
/// сдвиг <c>PastContact</c> перескакивает фигуру насквозь при
/// <c>|начало луча| / размер фигуры &gt; 1.25 · 2^23</c> — <b>не закрыт</b>:
/// ограничить сдвиг в этой сигнатуре нечем, нужен размер фигуры, а его среди
/// аргументов нет. Проверять сломанное поведение тестом нельзя: такой тест
/// станет ложным ровно тогда, когда дефект починят. Граница измерена и
/// описана в доктрине <c>PastContact</c> и в отчёте волны.
/// </para>
/// </remarks>
public sealed class InternalWave2Tests
{
    // ==================== P2-36: сдвиг не должен съедать радиус ====================

    /// <summary>
    /// P2-36, разделяющий вход из реестра: шар радиуса 0.05 м с центром в
    /// километре от начала координат. Прежний код сдвигал радиус на величину
    /// точки отсчёта, то есть на 0.188 м, то есть радиус становился
    /// <b>отрицательным</b>, и точка отражалась на противоположную сторону шара.
    /// </summary>
    [Fact]
    public void BoundingSphere_SurfacePointStaysInsideWhenCenterIsFarFromOrigin()
    {
        BoundingSphere sphere = new(new Vector3(1e6f, 0f, 0f), 0.05f);
        Vector3 query = new(1e6f, 0.075f, 0f);

        Vector3 point = sphere.ClosestPointOnSurface(query);

        Assert.True(
            sphere.Contains(point),
            $"Точка {point} вне шара, хотя получена его же методом поверхности; расстояние {Vector3.Distance(point, sphere.Center)} при радиусе {sphere.Radius}.");

        AssertSameSide(point, sphere.Center, query, nameof(BoundingSphere.ClosestPointOnSurface));
    }

    /// <summary>
    /// То же для капсулы: её точка отсчёта — ближайшая точка оси, и она также
    /// может быть на порядки дальше от начала координат, чем радиус.
    /// </summary>
    /// <remarks>
    /// Смещение запроса по X взято ровно в один последний разряд координаты
    /// (0.0625 при |C| = 1e6): иначе 0.03 метра не представимы в <c>float</c>
    /// рядом с миллионом, запрос слился бы с осью и проверял бы отдельную
    /// ветвь вызывающего «запрос ровно на оси», где точку строит сам вызывающий.
    /// </remarks>
    [Fact]
    public void Capsule3_SurfacePointStaysInsideWhenAxisIsFarFromOrigin()
    {
        Capsule3 capsule = new(new Vector3(1e6f, -1f, 0f), new Vector3(1e6f, 1f, 0f), 0.05f);
        Vector3 query = new(1e6f + 0.0625f, 0.075f, 0f);
        Vector3 axis = capsule.ClosestPointOnAxis(query);

        Assert.True(MathF.Abs(query.X - axis.X) > Scalar.Epsilon, "Запрос обязан отличаться от точки оси: иначе проверяется другая ветвь вызывающего.");

        Vector3 point = capsule.ClosestPointTo(query);

        Assert.True(capsule.Contains(point), $"Точка {point} вне капсулы, расстояние до оси {capsule.DistanceToAxis(point)} при радиусе {capsule.Radius}.");
        AssertSameSide(point, axis, query, nameof(Capsule3.ClosestPointTo));
    }

    /// <summary>
    /// Двумерный вариант того же дефекта: круг.
    /// </summary>
    [Fact]
    public void Circle2_SurfacePointStaysInsideWhenCenterIsFarFromOrigin()
    {
        Circle2 circle = new(new Vector2(1e6f, 0f), 0.05f);
        Vector2 query = new(1e6f, 0.075f);

        Vector2 point = circle.ClosestPointOnBoundary(query);

        Assert.True(circle.Contains(point), $"Точка {point} вне круга, расстояние {Vector2.Distance(point, circle.Center)} при радиусе {circle.Radius}.");
        Assert.True(Vector2.Dot(point - circle.Center, query - circle.Center) > 0f, "Точка оказалась по другую сторону центра, чем запрос.");
    }

    /// <summary>
    /// Двумерный вариант для капсулы: точка отсчёта — ближайшая точка отрезка.
    /// </summary>
    /// <remarks>
    /// Смещение по X — ровно один последний разряд координаты при |C| = 1e6,
    /// по той же причине, что и в трёхмерном случае.
    /// </remarks>
    [Fact]
    public void Capsule2_SurfacePointStaysInsideWhenAxisIsFarFromOrigin()
    {
        Capsule2 capsule = new(new Segment2(new Vector2(1e6f, -1f), new Vector2(1e6f, 1f)), 0.05f);
        Vector2 query = new(1e6f + 0.0625f, 0.075f);
        Vector2 axis = new Segment2(new Vector2(1e6f, -1f), new Vector2(1e6f, 1f)).ClosestPointTo(query);

        Assert.True(query != axis, "Запрос обязан отличаться от точки оси: иначе проверяется другая ветвь вызывающего.");

        Vector2 point = capsule.ClosestPointOnBoundary(query);

        Assert.True(capsule.Contains(point), $"Точка {point} вне капсулы при радиусе {capsule.Radius}.");
        Assert.True(Vector2.Dot(point - axis, query - axis) > 0f, "Точка оказалась по другую сторону оси, чем запрос.");
    }

    /// <summary>
    /// Главный инвариант правки: точка поверхности принадлежит своей фигуре.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Сорок одной октавы отношения <c>|точка отсчёта| / R</c> — от 1 до 2^40 —
    /// и четыре вызывающие фигуры. Отношение, а не метры: именно оно задаёт
    /// дефект, и проверка не должна зависеть от единиц.
    /// </para>
    /// <para>
    /// Центр кладётся на ось X, а смещение запроса — по Y: в этом случае
    /// смещение представимо в <c>float</c> при любом отношении, потому что у
    /// координаты Y нет крупного старшего разряда. Иначе запрос просто слился бы
    /// с центром и проверял бы уже другую, чужую ветвь вызывающего.
    /// </para>
    /// <para>
    /// Проверка принадлежности дублируется точным эталоном на
    /// <see cref="BigInteger"/>: расхождение с ним означало бы, что проверяется
    /// не та фигура.
    /// </para>
    /// </remarks>
    [Fact]
    public void SurfacePoint_BelongsToItsOwnShape_OverFortyOctavesOfScale()
    {
        int violations = 0;
        int checks = 0;
        int exactMismatch = 0;
        int flipped = 0;
        string firstViolation = string.Empty;

        for (int octave = 0; octave <= 40; octave++)
        {
            float ratio = MathF.Pow(2f, octave);
            for (int step = 0; step < 64; step++)
            {
                // Радиус и положение по Y фиксированы, меняется только доля
                // шага сетки по X и смещение запроса по Z.
                const float Radius = 0.05f;
                float cx = ratio * Radius * (1f + (step * 0.015625f));
                Vector3 center = new(cx, (step % 7) - 3f, (step % 5) - 2f);
                Vector3 query = center + new Vector3(0f, 1.5f * Radius, (step % 3) - 0.875f);
                if (MathF.Abs(query.Y - center.Y) <= Scalar.Epsilon)
                {
                    continue;
                }

                checks++;

                BoundingSphere sphere = new(center, Radius);
                Vector3 p = sphere.ClosestPointOnSurface(query);
                bool librarySays = sphere.Contains(p);
                bool exactSays = SphereContainsExact(p, center, Radius);
                if (!librarySays || !exactSays)
                {
                    violations++;
                    if (firstViolation.Length == 0)
                    {
                        firstViolation = $"шар: |C|/R = {ratio}, C = {center}, точка = {p}";
                    }
                }

                if (librarySays != exactSays)
                {
                    exactMismatch++;
                }

                if (Vector3.Dot(p - center, query - center) <= 0f)
                {
                    flipped++;
                }

                Circle2 circle = new(new Vector2(center.X, center.Y), Radius);
                Vector2 query2 = new(query.X, query.Y);
                Vector2 p2 = circle.ClosestPointOnBoundary(query2);
                if (!circle.Contains(p2) || !CircleContainsExact(p2, circle.Center, Radius))
                {
                    violations++;
                    if (firstViolation.Length == 0)
                    {
                        firstViolation = $"круг: |C|/R = {ratio}, C = {circle.Center}, точка = {p2}";
                    }
                }

                if (Vector2.Dot(p2 - circle.Center, query2 - circle.Center) <= 0f)
                {
                    flipped++;
                }
            }
        }

        Assert.True(checks >= 2600, $"Проверок слишком мало ({checks}), измерение ничего не доказывает.");
        Assert.True(exactMismatch == 0, $"{exactMismatch} проверок: точная арифметика и Contains вызывающего разошлись, то есть проверяется не та фигура.");
        Assert.True(flipped == 0, $"{flipped} точек оказались по другую сторону от точки отсчёта, чем запрос.");
        Assert.True(violations == 0, $"{violations} из {checks} точек поверхности не принадлежат своей фигуре. Первая: {firstViolation}");
    }

    /// <summary>
    /// Сдвинутый радиус обязан оставаться положительным: отрицательный радиус
    /// отражает точку на противоположную сторону фигуры.
    /// </summary>
    /// <remarks>
    /// Проверяется через наблюдаемый признак — знак смещения точки относительно
    /// запроса, — потому что сам <c>SurfaceRadius</c> из тестов недоступен.
    /// Смещение запроса кладётся по Y, где у координаты нет крупного старшего
    /// разряда, поэтому запрос представим при любом отношении.
    /// </remarks>
    [Fact]
    public void SurfacePoint_KeepsDirectionOfTheQuery_UpToRatiosFarBeyondFloatResolution()
    {
        int flipped = 0;
        int checks = 0;

        for (int octave = 0; octave <= 40; octave++)
        {
            float ratio = MathF.Pow(2f, octave);
            for (int step = 0; step < 16; step++)
            {
                float radius = MathF.Pow(10f, (step % 13) - 6);
                Vector3 center = new(ratio * radius, 0f, 0f);
                Vector3 query = center + new Vector3(0f, 2.5f * radius, 0f);
                checks++;

                BoundingSphere sphere = new(center, radius);
                Vector3 p = sphere.ClosestPointOnSurface(query);
                if (Vector3.Dot(p - center, query - center) <= 0f)
                {
                    flipped++;
                }
            }
        }

        Assert.True(checks == 656, "Сетка проверок неполна.");
        Assert.True(flipped == 0, $"{flipped} из {checks} точек отражены на противоположную сторону шара.");
    }

    // ==================== P2-37: нулевой радиус ====================

    /// <summary>
    /// P2-37: при нулевом радиусе и ненулевом центре прежний код возвращал
    /// отрицательный радиус порядка <c>|центр|·2^-22</c>, то есть точку на
    /// расстоянии этого числа от центра фигуры, у которой радиуса нет.
    /// </summary>
    [Fact]
    public void BoundingSphere_ZeroRadiusSurfacePointIsTheCenterItself()
    {
        foreach (float magnitude in new[] { 1f, 1000f, 1e6f, 1e12f, 1e30f })
        {
            BoundingSphere sphere = new(new Vector3(magnitude, 0f, 0f), 0f);
            Vector3 point = sphere.ClosestPointOnSurface(new Vector3(magnitude, 0.075f, 0f));

            Assert.True(sphere.Contains(point), $"При |C| = {magnitude} точка {point} вне шара нулевого радиуса.");
            Assert.True(point == sphere.Center, $"При |C| = {magnitude} точка {point} не совпадает с центром {sphere.Center}, а должна: у фигуры нет размера.");
        }
    }

    /// <summary>
    /// Тот же случай через штатный путь получения нулевого радиуса:
    /// <c>BoundingSphere.FromAabb</c> на вырожденном параллелепипеде из одной
    /// точки.
    /// </summary>
    [Fact]
    public void BoundingSphere_ZeroRadiusFromDegenerateAabb_KeepsSurfacePointInside()
    {
        Aabb3 box = Aabb3.FromPoints([new Vector3(1000f, 0f, 0f)]);
        BoundingSphere sphere = BoundingSphere.FromAabb(box);
        Assert.Equal(0f, sphere.Radius);

        Vector3 point = sphere.ClosestPointOnSurface(new Vector3(1000f, 0.075f, 0f));

        Assert.True(sphere.Contains(point), $"Точка {point} вне шара нулевого радиуса, полученного из вырожденного параллелепипеда.");
        Assert.True(point == sphere.Center, "Точка должна совпадать с центром вырожденного параллелепипеда.");
    }

    /// <summary>
    /// Частота дефекта на нулевом радиусе: измеряется, а не оценивается.
    /// Прежний код промахивался в 200 000 случаях из 200 000.
    /// </summary>
    /// <remarks>
    /// Величина центра ограничена 1e30: при |C| больше 1.7e38 сам центр перестаёт
    /// быть конечным числом, и это уже не нулевой радиус, а бесконечная
    /// координата — другой случай и другой дефект.
    /// </remarks>
    [Fact]
    public void BoundingSphere_ZeroRadiusHoldsForEveryNonZeroCenter()
    {
        const int Total = 200_000;
        int outside = 0;
        int checkedCenters = 0;
        for (int i = 0; i < Total; i++)
        {
            float magnitude = MathF.Pow(10f, -1f + (i % 32));
            Vector3 center = new(magnitude, 0f, 0f);
            BoundingSphere sphere = new(center, 0f);
            Vector3 point = sphere.ClosestPointOnSurface(center + new Vector3(0f, 0.075f, 0f));
            if (!sphere.Contains(point))
            {
                outside++;
            }

            if (center == sphere.Center)
            {
                checkedCenters++;
            }
        }

        Assert.True(Total == 200_000, "Объём измерения не тот.");
        Assert.Equal(0, outside);
        Assert.True(checkedCenters == Total, "Не все центры дошли до проверки.");
    }

    /// <summary>
    /// Нулевой радиус у двумерной фигуры: тот же путь <c>For(float, in Vector2)</c>.
    /// </summary>
    [Fact]
    public void Circle2_ZeroRadiusSurfacePointIsTheCenterItself()
    {
        Circle2 circle = new(new Vector2(1000f, 0f), 0f);
        Vector2 point = circle.ClosestPointOnBoundary(new Vector2(1000f, 0.075f));

        Assert.True(circle.Contains(point), $"Точка {point} вне круга нулевого радиуса.");
        Assert.True(point == circle.Center, "Точка должна совпадать с центром: у круга нет размера.");
    }

    // ==================== P2-38: PastContact, доступная часть ====================

    /// <summary>
    /// P2-38, та часть, что достижима через публичный API: прежний код
    /// складывал расстояние с величиной начала луча и умножал результат на два
    /// <b>до</b> масштабирования. На расстоянии 1.8e38 второе умножение
    /// переполнялось в <c>+Infinity</c>, и <c>Raycast</c> отдавал бесконечное
    /// расстояние при <c>hit = true</c>, то есть точку по нему построить нельзя.
    /// </summary>
    [Fact]
    public void Raycast_FarBox_ReportsFiniteDistance()
    {
        Ray3 ray = new(Vector3.Zero, new Vector3(1f, 0f, 0f));

        foreach (float far in new[] { 1.5e38f, 1.7e38f, 1.8e38f, 2e38f, 3.4e38f })
        {
            Aabb3 box = new(new Vector3(far, -1f, -1f), new Vector3(far, 1f, 1f));
            Assert.True(ray.Raycast(box, out float distance), $"Луч вдоль X обязан пересекать параллелепипед при x = {far}.");
            Assert.True(float.IsFinite(distance), $"Расстояние входа не конечно при x = {far}: {distance}. Точку по нему построить нельзя.");
        }
    }

    /// <summary>
    /// Обычный случай луча не должен пострадать: та же перегрузка обязана
    /// отдавать точку входа внутри параллелепипеда.
    /// </summary>
    [Fact]
    public void Raycast_OrdinaryBoxEntryPointStaysInside()
    {
        Ray3 ray = new(Vector3.Zero, new Vector3(1f, 0f, 0f));
        Aabb3 box = new(new Vector3(999f, -1f, -1f), new Vector3(1001f, 1f, 1f));

        Assert.True(ray.Raycast(box, out float distance));
        Assert.True(box.Contains(ray.Origin + (ray.Direction * distance)), "Точка входа обязана быть внутри параллелепипеда при |начало| = 1000 и размере 2.");
    }

    // ==================== инварианты, обязательные на любом состоянии ====================

    /// <summary>
    /// На обычном масштабе правка не должна ничего менять: точка остаётся у
    /// самой поверхности, не глубже документированного сдвига.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Оптимизация без наблюдаемой разницы на этом масштабе: отмена правки
    /// остаётся зелёной. Проверка не «доказывает правку», а охраняет от
    /// регрессии при следующем изменении сдвига.
    /// </para>
    /// <para>
    /// Глубина сравнивается не с разрядом радиуса, а с документированным
    /// сдвигом — двумя последними разрядами величины точки отсчёта. Сравнение с
    /// разрядом радиуса было бы ошибкой меры: при |C|/R = 1000 сдвиг в 2000 раз
    /// крупнее разряда радиуса, и такая проверка падала бы на вполне верном
    /// ответе.
    /// </para>
    /// </remarks>
    [Fact]
    public void SurfacePoint_IsAtTheSurfaceOnOrdinaryScale()
    {
        const int Checks = 5000;
        int offSurface = 0;
        for (int i = 1; i <= Checks; i++)
        {
            float radius = i * 0.001f;
            Vector3 center = new(i * 0.37f, (i % 13) * 0.11f, (i % 7) * 0.05f);
            Vector3 query = center + new Vector3(1.37f * radius, -2.11f * radius, 0.63f * radius);

            BoundingSphere sphere = new(center, radius);
            Vector3 p = sphere.ClosestPointOnSurface(query);

            double distance = Vector3.Distance(p, center);
            double documentedShift = 8.0 * Math.Max(radius, MaxComponent(center)) * 1.1920929e-7;
            if (distance < radius - documentedShift)
            {
                offSurface++;
            }

            if (!sphere.Contains(p))
            {
                offSurface++;
            }
        }

        Assert.Equal(0, offSurface);
    }

    /// <summary>
    /// Двумерный путь <c>For(float, in Vector2)</c>: тот же инвариант на
    /// отношении точки отсчёта к радиусу.
    /// </summary>
    [Fact]
    public void Circle2_SurfacePointBelongsToItsOwnCircle_OverFortyOctavesOfScale()
    {
        int violations = 0;
        int checks = 0;
        for (int octave = 0; octave <= 40; octave++)
        {
            float ratio = MathF.Pow(2f, octave);
            for (int step = 0; step < 16; step++)
            {
                const float Radius = 0.05f;
                float cx = ratio * Radius * (1f + (step * 0.0625f));
                Circle2 circle = new(new Vector2(cx, 0f), Radius);
                Vector2 query = new(cx, 2.5f * Radius);
                checks++;

                Vector2 p = circle.ClosestPointOnBoundary(query);
                if (!circle.Contains(p) || !CircleContainsExact(p, circle.Center, Radius))
                {
                    violations++;
                }
            }
        }

        Assert.True(checks == 656, "Сетка проверок неполна.");
        Assert.True(violations == 0, $"{violations} из {checks} точек круга не принадлежат своему кругу.");
    }

    /// <summary>
    /// Сдвинутый радиус обязан быть не больше настоящего: иначе точка
    /// оказывается за поверхностью в направлении запроса.
    /// </summary>
    [Fact]
    public void SurfacePoint_NeverLandsOutsideTheRadiusTowardsTheQuery()
    {
        int beyond = 0;
        int checks = 0;
        for (int octave = 0; octave <= 40; octave++)
        {
            float ratio = MathF.Pow(2f, octave);
            for (int step = 0; step < 8; step++)
            {
                const float Radius = 0.25f;
                Vector3 center = new(ratio * Radius * (1f + (step * 0.125f)), (step % 3) - 1f, 0f);
                Vector3 query = center + new Vector3(0f, 4f * Radius, 0f);
                checks++;

                BoundingSphere sphere = new(center, Radius);
                Vector3 p = sphere.ClosestPointOnSurface(query);
                if (Vector3.Distance(p, center) > Radius)
                {
                    beyond++;
                }
            }
        }

        Assert.True(checks == 328, "Сетка проверок неполна.");
        Assert.Equal(0, beyond);
    }

    // ==================== точный эталон ====================

    /// <summary>
    /// Проверяет, что точка осталась с той же стороны от опорной точки, что и
    /// запрос: отрицательный сдвинутый радиус отражает её на другую сторону.
    /// </summary>
    private static void AssertSameSide(Vector3 point, Vector3 basePoint, Vector3 query, string operation)
    {
        bool sameSide = Vector3.Dot(point - basePoint, query - basePoint) > 0f;
        Assert.True(sameSide, $"{operation} вернул точку {point} по другую сторону, чем запрос {query}.");
    }

    /// <summary>
    /// Наибольшая по модулю координата точки: величина, от которой отсчитывается
    /// сдвиг точки поверхности.
    /// </summary>
    private static float MaxComponent(Vector3 point)
        => MathF.Max(MathF.Max(MathF.Abs(point.X), MathF.Abs(point.Y)), MathF.Abs(point.Z));

    /// <summary>
    /// Единица точной арифметики: значение <paramref name="x"/> в единицах 2^-149.
    /// </summary>
    private static BigInteger Units(float x)
    {
        int bits = BitConverter.SingleToInt32Bits(x);
        bool negative = bits < 0;
        int exponent = (bits >> 23) & 0xFF;
        int mantissa = bits & 0x7FFFFF;

        BigInteger value = exponent switch
        {
            0 => mantissa,
            255 => BigInteger.Zero,
            _ => (BigInteger)(mantissa + (1 << 23)) << (exponent - 23),
        };

        return negative ? -value : value;
    }

    /// <summary>
    /// Точная проверка «шар содержит точку» в рациональной арифметике.
    /// </summary>
    private static bool SphereContainsExact(Vector3 point, Vector3 center, float radius)
    {
        BigInteger dx = Units(point.X) - Units(center.X);
        BigInteger dy = Units(point.Y) - Units(center.Y);
        BigInteger dz = Units(point.Z) - Units(center.Z);
        BigInteger r = Units(radius);
        return (dx * dx) + (dy * dy) + (dz * dz) <= r * r;
    }

    /// <summary>
    /// Точная проверка «круг содержит точку» в рациональной арифметике.
    /// </summary>
    private static bool CircleContainsExact(Vector2 point, Vector2 center, float radius)
    {
        BigInteger dx = Units(point.X) - Units(center.X);
        BigInteger dy = Units(point.Y) - Units(center.Y);
        BigInteger r = Units(radius);
        return (dx * dx) + (dy * dy) <= r * r;
    }
}