using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using XEngine.Mathematics;
using XEngine.Mathematics.Serialization;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Последние непокрытые ветви: операторы-обёртки, выгрузка углов,
/// вырожденные случаи в геометрии и разбор JSON.
/// </summary>
/// <remarks>
/// Это не новые контракты, а хвосты больших контрактов: операторы угла,
/// проверки размера буфера, ветви «значение на границе». Каждая ветвь
/// проверяется по своему определению — куда обязан вести оператор и что обязан
/// выдать разбор входа — а не по совпадению с соседним методом.
/// </remarks>
public class RemainingBranchTests
{
    private const float Tolerance = 1e-4f;

    private static Vector3 P(float x, float y, float z) => new(x, y, z);

    private static JsonSerializerOptions Options()
    {
        JsonSerializerOptions options = new();
        options.Converters.Add(new Vector2JsonConverter());
        options.Converters.Add(new Vector3JsonConverter());
        options.Converters.Add(new Vector4JsonConverter());
        options.Converters.Add(new AngleJsonConverter());
        options.Converters.Add(new Rgba32JsonConverter());
        return options;
    }

    #region Операторы угла

    /// <summary>
    /// Тангенс угла есть тангенс его радианов, а нормализация приводит угол в
    /// диапазон [−π; π].
    /// </summary>
    [Fact]
    public void Angle_TangentAndNormalization()
    {
        // Кратные 90° исключены: тангенс там не определён, и в одинарной
        // точности результат определяется тем, насколько округлённый π/2
        // отличается от настоящего прямого угла. Это не ошибка расчёта, но и
        // не величина, которую имеет смысл сверять.
        foreach (float degrees in new[] { 0f, 30f, 45f, 60f, 89f, 135f, -60f, -179f, 400f, 720f })
        {
            Angle angle = Angle.FromDegrees(degrees);

            double expectedTangent = Math.Tan(degrees * Math.PI / 180.0);
            Assert.InRange(angle.Tan, (float)expectedTangent - 1e-2f, (float)expectedTangent + 1e-2f);

            // Нормализованный угол лежит в диапазоне [−π; π].
            double radians = angle.Normalized().Radians;
            Assert.InRange(radians, -Math.PI - Tolerance, Math.PI + Tolerance);

            // Поворот нормализованным углом совпадает с поворотом исходным.
            Vector2 vector = new(1f, 2f);
            MathAssert.Equal(angle.Rotate(vector), angle.Normalized().Rotate(vector), 1e-4f);
        }
    }

    /// <summary>
    /// Операторы над углом обязаны совпадать с методами, которые они вызывают,
    /// и с обычной арифметикой: масштабирование угла есть умножение его
    /// радианов, деление есть деление радианов.
    /// </summary>
    [Fact]
    public void Angle_OperatorsMatchTheirMethods()
    {
        Angle angle = Angle.FromDegrees(30f);

        MathAssert.Equal(angle.Negated(), -angle);
        MathAssert.Equal(angle.Scale(2f), angle * 2f);

        // Масштабирование вдвое есть удвоение радианов, деление на четыре
        // есть деление радианов на четыре.
        Assert.InRange((angle * 2f).Radians, (angle.Radians * 2.0) - 1e-6, (angle.Radians * 2.0) + 1e-6);
        Assert.InRange((angle / 4f).Radians, (angle.Radians / 4.0) - 1e-6, (angle.Radians / 4.0) + 1e-6);
        Assert.InRange((-angle).Radians, -angle.Radians - 1e-9, -angle.Radians + 1e-9);

        // Поворот направления есть поворот единичного вектора: нормализация
        // внутри метода не должна менять результат.
        Vector2 vector = new(3f, 4f);
        Vector2 rotated = angle.RotateDirection(vector);

        // Метод по контракту возвращает единичный вектор: направление
        // нормализуется перед поворотом, длина исходного вектора отбрасывается.
        MathAssert.Equal(1f, rotated.Length(), Tolerance);
        MathAssert.Equal(angle.Rotate(Vector2.Normalize(vector)), rotated, 1e-4f);
    }

    /// <summary>
    /// Сравнение углов по радианам: меньший радиан меньше, и порядок
    /// совпадает с порядком в градусах для углов одного оборота.
    /// </summary>
    [Fact]
    public void Angle_ComparesByRadians()
    {
        Angle small = Angle.FromDegrees(10f);
        Angle middle = Angle.FromDegrees(90f);
        Angle large = Angle.FromDegrees(170f);

        Assert.True(small.CompareTo(middle) < 0);
        Assert.True(middle.CompareTo(large) < 0);
        Assert.True(large.CompareTo(small) > 0);
        Assert.Equal(0, small.CompareTo(Angle.FromDegrees(10f)));

        // Сортировка даёт тот же порядок, что и сравнение радианов.
        Angle[] shuffled = [large, small, middle];
        Array.Sort(shuffled);

        Assert.Equal(small, shuffled[0]);
        Assert.Equal(middle, shuffled[1]);
        Assert.Equal(large, shuffled[2]);
    }

    #endregion

    #region Выгрузка углов параллелепипедов

    /// <summary>
    /// Углы выгружаются в заданном порядке, а слишком короткий буфер —
    /// ошибка вызывающего: молча обрезанный буфер дал бы неполный результат.
    /// </summary>
    [Fact]
    public void GetCorners_WritesAllCornersInOrder()
    {
        Aabb2 box2 = new(new Vector2(-1f, 2f), new Vector2(3f, 4f));
        Span<Vector2> corners2 = stackalloc Vector2[4];
        box2.GetCorners(corners2);

        Assert.Equal(new Vector2(-1f, 2f), corners2[0]);
        Assert.Equal(new Vector2(3f, 2f), corners2[1]);
        Assert.Equal(new Vector2(3f, 4f), corners2[2]);
        Assert.Equal(new Vector2(-1f, 4f), corners2[3]);

        Aabb3 box3 = new(P(-1f, 2f, 0), P(3f, 4f, 6f));
        Span<Vector3> corners3 = stackalloc Vector3[8];
        box3.GetCorners(corners3);

        for (int i = 0; i < 8; i++)
        {
            Assert.True(box3.Contains(corners3[i]), $"Угол {i} выпал из параллелепипеда.");
        }

        bool hasMin = false;
        bool hasMax = false;
        for (int i = 0; i < 8; i++)
        {
            hasMin |= corners3[i] == new Vector3(-1f, 2f, 0f);
            hasMax |= corners3[i] == new Vector3(3f, 4f, 6f);
        }

        Assert.True(hasMin, "Угол с минимальными координатами не выгружен.");
        Assert.True(hasMax, "Угол с максимальными координатами не выгружен.");
    }

    [Fact]
    public void GetCorners_RejectsShortBuffer()
    {
        Aabb2 box2 = new(new Vector2(0f, 0f), new Vector2(1f, 1f));
        Assert.Throws<ArgumentException>(() =>
        {
            Span<Vector2> short2 = stackalloc Vector2[3];
            box2.GetCorners(short2);
        });

        Aabb3 box3 = new(P(0f, 0f, 0f), P(1f, 1f, 1f));
        Assert.Throws<ArgumentException>(() =>
        {
            Span<Vector3> short3 = stackalloc Vector3[7];
            box3.GetCorners(short3);
        });
    }

    #endregion

    #region Капсула, окружность, пересечение

    /// <summary>
    /// Ближайшая точка границы капсулы лежит на радиусе от ближайшей точки
    /// оси в направлении исходной точки.
    /// </summary>
    [Fact]
    public void Capsule2_BoundaryPointIsAtRadius()
    {
        Capsule2 capsule = new(new Segment2(new Vector2(0, 0), new Vector2(2, 0)), 0.5f);
        var random = new XorShift64Star(24680);

        for (int i = 0; i < 300; i++)
        {
            Vector2 point = new(
                (random.NextFloat() - 0.5f) * 10f,
                (random.NextFloat() - 0.5f) * 10f);

            Vector2 boundary = capsule.ClosestPointOnBoundary(point);
            Vector2 axisPoint = capsule.Segment2.ClosestPointTo(point);

            float distance = Vector2.Distance(boundary, axisPoint);
            Assert.InRange(distance, capsule.Radius - Tolerance, capsule.Radius + Tolerance);

            Vector2 fromAxis = point - axisPoint;
            if (fromAxis.LengthSquared() > Tolerance * Tolerance)
            {
                Assert.True(
                    Vector2.Dot(fromAxis, boundary - axisPoint) > 0f,
                    "Точка границы оказалась с противоположной стороны оси.");
            }
        }

        // Точка ровно на оси: направление не определено, расстояние до оси
        // обязано быть радиусом, а не нулём.
        Vector2 onAxis = new(1f, 0f);
        float fromAxisPoint = Vector2.Distance(capsule.ClosestPointOnBoundary(onAxis), onAxis);
        MathAssert.Equal(capsule.Radius, fromAxisPoint, Tolerance);
    }

    /// <summary>
    /// Перенос меняет только положение, радиус остаётся прежним.
    /// </summary>
    [Fact]
    public void Circle2_TranslatedMovesCenterOnly()
    {
        Circle2 circle = new(new Vector2(1f, 2f), 3f);
        Circle2 moved = circle.Translated(new Vector2(10f, -5f));

        MathAssert.Equal(new Vector2(11f, -3f), moved.Center, Tolerance);
        MathAssert.Equal(circle.Radius, moved.Radius, Tolerance);
    }

    /// <summary>
    /// Пересечение прямоугольников есть пересечение их границ: результат у
    /// отдельной перегрузки обязан совпадать с методом самого параллелепипеда.
    /// </summary>
    [Fact]
    public void Collision_AabbOverloadMatchesBoxMethod()
    {
        Aabb2 first = new(new Vector2(0f, 0f), new Vector2(2f, 2f));
        Aabb2 overlapping = new(new Vector2(1f, 1f), new Vector2(3f, 3f));
        Aabb2 separate = new(new Vector2(5f, 5f), new Vector2(6f, 6f));

        Assert.Equal(first.Intersects(overlapping), Collision.Intersects(first, overlapping));
        Assert.Equal(first.Intersects(separate), Collision.Intersects(first, separate));

        Assert.True(Collision.Intersects(first, overlapping));
        Assert.False(Collision.Intersects(first, separate));
    }

    /// <summary>
    /// Вырожденные прямоугольники не имеют наименьшей проникающей оси:
    /// глубина не определена, и метод обязан честно сказать об отказе.
    /// </summary>
    [Fact]
    public void TryGetObbPenetration_ReportsNoPenetrationForDegenerateBox()
    {
        // Нулевой размер по обеим осям: проекция на любую ось равна нулю,
        // наименьшей проникающей оси не существует.
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
        _ = axis;

        // Вырожденный прямоугольник внутри другого: пересечение есть, но
        // оси проникновения нет.
        Aabb2 degenerate = new(new Vector2(1f, 1f), new Vector2(1f, 1f));
        Aabb2 box = new(new Vector2(0f, 0f), new Vector2(2f, 2f));

        Assert.True(Collision.Intersects(box, degenerate));
        Assert.True(degenerate.Intersects(box));
    }

    /// <summary>
    /// Параллельные отрезки не имеют единственной пары общих перпендикуляров,
    /// и метод обязан выбрать разумную, а не вернуть бесконечность.
    /// </summary>
    [Fact]
    public void SegmentSegmentDistance_HandlesParallelSegments()
    {
        // Параллельные, разнесённые по одной оси.
        Segment2 first = new(new Vector2(0, 0), new Vector2(1, 0));
        Segment2 parallel = new(new Vector2(0, 2f), new Vector2(1, 2f));

        MathAssert.Equal(2f, Collision.SegmentSegmentDistance(first, parallel), Tolerance);

        // Параллельные, лежащие на одной прямой: расстояние ноль.
        Segment2 collinear = new(new Vector2(-1f, 0f), new Vector2(2f, 0f));
        MathAssert.Equal(0f, Collision.SegmentSegmentDistance(first, collinear), Tolerance);

        // Вырожденный отрезок и обычный: результат есть расстояние до точки.
        Segment2 point = new(new Vector2(0, 3f), new Vector2(0, 3f));
        MathAssert.Equal(3f, Collision.SegmentSegmentDistance(first, point), Tolerance);

        // Расстояние между отрезками не меньше расстояния до любого из них,
        // взятого целиком, и совпадает с ним, когда ближайшая точка лежит
        // внутри второго отрезка.
        MathAssert.Equal(
            Collision.SegmentSegmentDistance(first, parallel),
            Vector2.Distance(new Vector2(0, 0), new Vector2(0, 2f)),
            Tolerance);

        // Оба отрезка вырождены в точки: общей перпендикулярной пары нет,
        // и расстояние есть расстояние между точками.
        Segment2 pointA = new(new Vector2(1f, 1f), new Vector2(1f, 1f));
        Segment2 pointB = new(new Vector2(4f, 5f), new Vector2(4f, 5f));
        MathAssert.Equal(
            (float)Math.Sqrt(25.0),
            Collision.SegmentSegmentDistance(pointA, pointB),
            Tolerance);

        // Вырожденный отрезок в обратном порядке: ближайшая точка ищется на
        // обычном отрезке, а не на точке. Точка (0, 3) над осью segment,
        // идущей от (0, 0) до (1, 0), отстоит от неё на 3.
        MathAssert.Equal(
            3f,
            Collision.SegmentSegmentDistance(new Segment2(new Vector2(0, 3f), new Vector2(0, 3f)), first),
            Tolerance);
    }

    #endregion

    #region Ограничения и отсечение

    /// <summary>
    /// Отсечение обязано возвращать границу, а значение внутри диапазона —
    /// проходить без изменения.
    /// </summary>
    [Fact]
    public void Interpolation_ClampReturnsBoundary()
    {
        Assert.Equal(0f, Interpolation.Clamp(-5f, 0f, 10f));
        Assert.Equal(10f, Interpolation.Clamp(15f, 0f, 10f));
        MathAssert.Equal(5f, Interpolation.Clamp(5f, 0f, 10f), 0f);
        MathAssert.Equal(5f, Interpolation.Clamp(5f, 5f, 5f), 0f);
        Assert.Equal(-1f, Interpolation.Clamp(-3f, -1f, 1f));
    }

    /// <summary>
    /// Сглаживание, которое уже перешагнуло цель, возвращается ровно на цель
    /// и гасит скорость: иначе значение пролетает мимо и возвращается,
    /// раскачиваясь вокруг цели.
    /// </summary>
    [Fact]
    public void SmoothDamp_SnapsToTargetWhenOvershooting()
    {
        // Текущее значение ниже цели, а скорость заведомо велика: за один шаг
        // значение перелетает цель. Именно этот случай ловится гашением.
        float velocity = 1000f;
        float value = 0f;

        float result = Interpolation.SmoothDamp(value, 10f, 0.3f, float.MaxValue, 1f / 60f, ref velocity);

        // Без гашения значение ушло бы за цель и стало бы качаться вокруг неё.
        Assert.Equal(10f, result);
        Assert.Equal(0f, velocity);
        _ = value;
    }

    #endregion

    #region Векторы трёх измерений

    /// <summary>
    /// Знаковый угол требует ненулевой оси: без неё направление вращения
    /// не определено, и знак был бы произвольным.
    /// </summary>
    [Fact]
    public void SignedAngleAround_RejectsZeroAxis()
    {
        Vector3 from = P(1, 0, 0);
        Vector3 to = P(0, 1, 0);

        Assert.Throws<ArgumentException>(() => Vector3Extensions.SignedAngleAround(from, to, Vector3.Zero));

        // С осью знак определён: поворот против часовой стрелки виден со
        // стороны положительной оси.
        Angle positive = Vector3Extensions.SignedAngleAround(from, to, P(0, 0, 1));
        Angle negative = Vector3Extensions.SignedAngleAround(from, to, P(0, 0, -1));

        Assert.True(positive.Radians > 0.0, $"Ожидался положительный угол, получен {positive}.");
        Assert.True(negative.Radians < 0.0, $"Ожидался отрицательный угол, получен {negative}.");

        // Модули совпадают: различие только в знаке, то есть в направлении
        // обхода.
        Assert.InRange(Math.Abs(positive.Radians), 1.5707, 1.5709);
        Assert.InRange(Math.Abs(negative.Radians), 1.5707, 1.5709);

        // Разность сведётся к 180°, а не к 90°, и это верно: направления
        // обхода противоположны, а не противоположны по углу величиной в 90°.
        Assert.InRange(Math.Abs((positive - negative).Degrees), 179.0, 181.0);
    }

    /// <summary>
    /// Перпендикуляр к нулевому вектору не определён: у нуля нет
    /// направления, относительно которого строится перпендикуляр.
    /// </summary>
    [Fact]
    public void Perpendicular_RejectsZeroVector()
        => Assert.Throws<ArgumentException>(() => Vector3.Zero.Perpendicular(new Vector3(1f, 0f, 0f)));

    /// <summary>
    /// Отрезок за пределами начала координат: луч, направленный в сторону
    /// угла, пересекает его, а направленный в сторону нуля — нет.
    /// </summary>
    [Fact]
    public void Ray2_HandlesNegativeDirectionAndMiss()
    {
        Aabb2 box = new(new Vector2(-1f, -1f), new Vector2(1f, 1f));

        // Отрицательные компоненты направления меняют порядок входов в
        // параллелепипед: их нужно переставить.
        Ray2 towardPositive = new(new Vector2(-5f, 0f), new Vector2(1f, 0f));
        Ray2 towardNegative = new(new Vector2(5f, 0f), new Vector2(-1f, 0f));

        // Оба луча пересекают параллелепипед: направление задаёт только то,
        // с какой стороны вход, а не сам факт пересечения.
        Assert.True(towardPositive.Intersects(box));
        Assert.True(towardNegative.Intersects(box));

        // Точка на расстоянии, пройденном лучом, обязана оказаться по
        // достижении параллелепипеда.
        Assert.True(box.Contains(towardPositive.GetPoint(4.5f)), "Луч слева не попал в параллелепипед.");
        Assert.True(box.Contains(towardNegative.GetPoint(4.5f)), "Луч справа не попал в параллелепипед.");

        // Луч, уходящий от параллелепипеда, не пересекает его.
        Ray2 away = new(new Vector2(5f, 5f), new Vector2(1f, 1f));
        Assert.False(away.Intersects(box));

        // Луч, проходящий мимо по касательной, тоже не пересекает.
        Ray2 beside = new(new Vector2(-5f, 3f), new Vector2(1f, 0f));
        Assert.False(beside.Intersects(box));
    }

    /// <summary>
    /// Луч, направленный от цилиндра, даёт отрицательные параметры входа по
    /// обеим сторонам, и оба отбрасываются: пересечения нет.
    /// </summary>
    [Fact]
    public void Ray3_IgnoresCandidatesBehindOrigin()
    {
        // Вертикальная капсула от 0 до 5 на расстоянии 1 от оси Z.
        Capsule3 capsule = new(P(0f, 0f, 0f), P(0f, 0f, 5f), 1f);

        // Луч уходит в сторону, противоположную капсуле: вход и выход по
        // цилиндру получаются отрицательными и отбрасываются. Начало луча
        // вынесено из капсулы, иначе пересечение было бы настоящим.
        Ray3 away = new(P(0f, 0f, -3f), P(0f, 0f, -1f));
        Assert.False(away.Intersects(in capsule));
        Assert.False(away.Raycast(in capsule, out float distanceAway));
        Assert.Equal(0f, distanceAway);

        // Луч, начинающийся за пределами капсулы и направленный на неё.
        Ray3 toward = new(P(0f, 0f, -5f), P(0f, 0f, 1f));
        Assert.True(toward.Intersects(in capsule));
        Assert.True(toward.Raycast(in capsule, out float distanceToward));
        Assert.InRange(distanceToward, 3.9f, 4.1f);

        // Луч, начало которого лежит на оси уже за торцом капсулы. До капсулы
        // оно не достаёт, но бесконечный цилиндр вокруг оси луч пересекает, и
        // ближайшее пересечение позади начала, то есть позади начала луча.
        // Такое пересечение обязано отбрасываться: иначе расстояние вышло бы
        // отрицательным, а капсула считалась бы пересечённой назад.
        Ray3 behindCap = new(P(0f, 0f, -3f), Vector3.Normalize(P(1f, 0f, 0.5f)));
        Assert.False(capsule.Contains(behindCap.Origin), "Начало луча обязано быть вне капсулы.");
        Assert.False(behindCap.Intersects(in capsule));
        Assert.False(behindCap.Raycast(in capsule, out float behindDistance));
        Assert.Equal(0f, behindDistance);

        // Тот же луч, направленный в сторону капсулы: пересечение уже впереди.
        Ray3 towardFromBehind = new(P(0f, 0f, -3f), Vector3.Normalize(P(0f, 0f, 1f)));
        Assert.True(towardFromBehind.Intersects(in capsule));
    }

    #endregion

    #region Разбор JSON

    /// <summary>
    /// Векторы и цвета обязаны переживать запись и чтение без потери: движок
    /// сохраняет состояние сцены в JSON, и потеря компоненты ломает загрузку.
    /// </summary>
    [Fact]
    public void Json_VectorsRoundTripWithoutLoss()
    {
        JsonSerializerOptions options = Options();

        Vector2 v2 = new(1.25f, -2.5f);
        Assert.Equal(v2, JsonSerializer.Deserialize<Vector2>(JsonSerializer.Serialize(v2, options), options));

        Vector3 v3 = new(1.25f, -2.5f, 3.75f);
        Assert.Equal(v3, JsonSerializer.Deserialize<Vector3>(JsonSerializer.Serialize(v3, options), options));

        Vector4 v4 = new(1.25f, -2.5f, 3.75f, -4.5f);
        Assert.Equal(v4, JsonSerializer.Deserialize<Vector4>(JsonSerializer.Serialize(v4, options), options));

        // Каналы квантуются до байта, поэтому половина шага 1/255 есть
        // предел точности, а не отклонение. Цвета 0.25 и 0.75 попадают в
        // байты точно, 0.5 округляется к 128 и даёт 128/255.
        float channelStep = 0.5f / 255f + 1e-6f;
        Rgba32 color = new(0.25f, 0.5f, 0.75f, 1f);
        Rgba32 read = JsonSerializer.Deserialize<Rgba32>(JsonSerializer.Serialize(color, options), options);
        MathAssert.Equal(color.R, read.R, channelStep);
        MathAssert.Equal(color.G, read.G, channelStep);
        MathAssert.Equal(color.B, read.B, channelStep);
        MathAssert.Equal(color.A, read.A, channelStep);

        Angle angle = Angle.FromDegrees(33.3f);
        Angle readAngle = JsonSerializer.Deserialize<Angle>(JsonSerializer.Serialize(angle, options), options);
        MathAssert.Equal(angle, readAngle, 1e-3);
    }

    /// <summary>
    /// Угол принимается и числом радианов, и объектом с радианами, и объектом
    /// с градусами: формат задавался как совместимый со старыми файлами.
    /// </summary>
    [Fact]
    public void Json_AngleAcceptsAllThreeShapes()
    {
        JsonSerializerOptions options = Options();

        // Объект с градусами имеет приоритет над радианами: он описан позже
        // и читается последним.
        Angle byDegrees = JsonSerializer.Deserialize<Angle>(
            """{"radians":99.0,"degrees":90.0}""", options);
        Assert.InRange(byDegrees.Degrees, 90.0 - Tolerance, 90.0 + Tolerance);

        Angle byRadians = JsonSerializer.Deserialize<Angle>(
            """{"radians":1.5707963267948966}""", options);
        Assert.InRange(byRadians.Degrees, 90.0 - Tolerance, 90.0 + Tolerance);

        // Незаданные поля угла равны нулю, а неизвестные пропускаются.
        Angle empty = JsonSerializer.Deserialize<Angle>("""{"unknown":42}""", options);
        Assert.InRange(empty.Degrees, -Tolerance, Tolerance);

        // Неизвестные поля вектора пропускаются, незаданные — обнуляются.
        Vector2 partial = JsonSerializer.Deserialize<Vector2>("""{"x":5.0,"unknown":42}""", options);
        MathAssert.Equal(5f, partial.X, Tolerance);
        MathAssert.Equal(0f, partial.Y, Tolerance);
    }

    /// <summary>
    /// Значение не того типа обязано давать ошибку разбора с указанием
    /// ожидаемого формата, а не молчаливый ноль.
    /// </summary>
    [Fact]
    public void Json_RejectsValuesOfWrongType()
    {
        JsonSerializerOptions options = Options();

        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<Angle>("\"строка\"", options));
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<Vector2>("\"строка\"", options));
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<Vector3>("\"строка\"", options));

        // Массив вместо объекта — ошибка формата.
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<Angle>("[1,2]", options));

        // Пустой объект допустим: формат разрешает частичное заполнение, и
        // незаданные компоненты обнуляются.
        Vector2 empty = JsonSerializer.Deserialize<Vector2>("{}", options);
        MathAssert.Equal(Vector2.Zero, empty, 0f);

        // Нечисловое значение в поле вектора: поле пропускается и обнуляется,
        // а разбор продолжается, потому что формат допускает частичное
        // заполнение.
        Vector2 withText = JsonSerializer.Deserialize<Vector2>("""{"x":"текст","y":2.0}""", options);
        MathAssert.Equal(0f, withText.X, Tolerance);
        MathAssert.Equal(2f, withText.Y, Tolerance);
    }

    /// <summary>
    /// Угол принимается и «голым» числом радианов — кратчайшая запись,
    /// которой пользуются в конфигурационных файлах.
    /// </summary>
    [Fact]
    public void Json_AngleAcceptsBareNumber()
    {
        JsonSerializerOptions options = Options();

        Angle angle = JsonSerializer.Deserialize<Angle>("0.5", options);

        // 0.5 радиана — это 28.6479 градуса; сверяется с переводом, а не
        // выписывается числом.
        Assert.InRange(angle.Degrees, Scalar.ToDegrees(0.5) - 1e-4, Scalar.ToDegrees(0.5) + 1e-4);

        // Отрицательное радианы и ноль разбираются так же.
        Assert.InRange(JsonSerializer.Deserialize<Angle>("0", options).Degrees, -1e-4, 1e-4);
        Assert.InRange(
            JsonSerializer.Deserialize<Angle>("-1.5707963267948966", options).Degrees,
            -90.001,
            -89.999);
    }

    /// <summary>
    /// Цвет обязан быть строкой или массивом: число вместо цвета означает
    /// ошибку в файле, а не молчаливый чёрный.
    /// </summary>
    [Fact]
    public void Json_ColorRejectsNonColorTokens()
    {
        JsonSerializerOptions options = Options();

        // Ни строка, ни массив: ошибка формата с указанием ожидаемого вида.
        foreach (string text in new[] { "1.5", "true", "{}" })
        {
            JsonException error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Rgba32>(text, options));
            Assert.Contains("#RRGGBB", error.Message, StringComparison.Ordinal);
        }

        // Некорректная строка — ошибка разбора самого цвета.
        Assert.Throws<FormatException>(() => JsonSerializer.Deserialize<Rgba32>("\"мусор\"", options));
    }

    /// <summary>
    /// Цвет-массив из трёх элементов обязан получить непрозрачную альфу, а не
    /// нулевую: иначе цвет оказывается полностью невидимым без единой
    /// ошибки в файле.
    /// </summary>
    [Fact]
    public void Json_ColorArrayShorterThanFourKeepsOpaqueAlpha()
    {
        JsonSerializerOptions options = Options();

        Rgba32 fromThree = JsonSerializer.Deserialize<Rgba32>("[0.2,0.4,0.6]", options);
        Rgba32 fromFour = JsonSerializer.Deserialize<Rgba32>("[0.2,0.4,0.6,1.0]", options);

        MathAssert.Equal(fromFour.A, fromThree.A, 0f);
        MathAssert.Equal(1f, fromThree.A, Tolerance);
        MathAssert.Equal(0.2f, fromThree.R, 1e-3f);
        MathAssert.Equal(0.4f, fromThree.G, 1e-3f);
        MathAssert.Equal(0.6f, fromThree.B, 1e-3f);
    }

    #endregion

    #region Нечисловые веса

    /// <summary>
    /// Поведение метода при весах, которые не являются числами.
    /// </summary>
    /// <remarks>
    /// Отрицательные веса проверка отвергает, а нечисловые проходят мимо неё:
    /// условие «вес меньше нуля» для NaN ложно. Дальше накопление суммы
    /// становится неопределённым, сравнение с порогом всегда ложно, и метод
    /// доходит до последней строки возвратом последнего индекса. Это не
    /// взвешенный выбор, а произвольный индекс, и он не отличим от
    /// правильного результата снаружи. Пробел зафиксирован в Problems.md;
    /// здесь поведение закреплено, чтобы изменение было заметным.
    /// </remarks>
    [Fact]
    public void NextWeightedIndex_WithNaNWeightReturnsLastIndex()
    {
        var random = new XorShift64Star(1);

        // Неопределённость стоит первым: сумма становится неопределённой
        // сразу, и ни один индекс не может быть выбран сравнением с порогом.
        for (int attempt = 0; attempt < 100; attempt++)
        {
            Assert.Equal(1, random.NextWeightedIndex([float.NaN, 1f]));
        }

        // Явное утверждение на текущее поведение: возвращается последний
        // индекс, то есть произвольный, а не взвешенный. Это дефект, а не
        // замысел.
        Assert.Equal(2, random.NextWeightedIndex([1f, float.NaN, 3f]));
    }

    #endregion
}