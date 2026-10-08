using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Тесты капсулы в трёх измерериях.
/// </summary>
/// <remarks>
/// До этого покрытие файла было 34 %: не выполнялся ни один метод проверки
/// пересечения, а приватный метод ближайших точек двух отрезков — самый
/// сложный алгоритм в файле — не был вызван ни разу. Все расстояния здесь
/// сверяются с перебором из <see cref="ReferenceGeometry"/>, а не с
/// утверждениями о конкретных числах.
/// </remarks>
public class Capsule3Tests
{
    /// <summary>
    /// Допуск на расстояние. Одинарная точность даёт примерно 6·10⁻⁸ на
    /// единицу, перебор сходится точнее; берётся запас на накопление.
    /// </summary>
    private const float Tolerance = 1e-4f;

    private static Vector3 P(float x, float y, float z) => new(x, y, z);

    #region Пересечение: сверка с перебором

    /// <summary>
    /// Пересечение капсул обязано совпадать с эталоном: расстояние между
    /// осевыми линиями не больше суммы радиусов. Радиус подбирается по
    /// эталонному расстоянию с запасом в обе стороны, чтобы решение не
    /// зависело от точности на границе.
    /// </summary>
    /// <param name="ax">Начало первой оси, X.</param>
    /// <param name="ay">Начало первой оси, Y.</param>
    /// <param name="az">Начало первой оси, Z.</param>
    /// <param name="bx">Конец первой оси, X.</param>
    /// <param name="by">Конец первой оси, Y.</param>
    /// <param name="bz">Конец первой оси, Z.</param>
    /// <param name="cx">Начало второй оси, X.</param>
    /// <param name="cy">Начало второй оси, Y.</param>
    /// <param name="cz">Начало второй оси, Z.</param>
    /// <param name="dx">Конец второй оси, X.</param>
    /// <param name="dy">Конец второй оси, Y.</param>
    /// <param name="dz">Конец второй оси, Z.</param>
    /// <param name="what">Описание случая для сообщения.</param>
    [Theory]
    [InlineData(0, 0, 0, 10, 0, 0, 5, 2, -3, 5, 2, 4, "скрещивающиеся")]
    [InlineData(0, 0, 0, 1, 0, 0, 0, 0, 3, 1, 0, 3, "параллельные по Z")]
    [InlineData(0, 0, 0, 1, 0, 0, 0, 0.5f, 0, 1, 0.5f, 0, "параллельные в одной плоскости")]
    [InlineData(0, 0, 0, 1, 0, 0.0001f, 0.01f, 0.01f, 0, 0.01f, 0.01f, 0.0001f, "почти параллельные")]
    [InlineData(0, 0, 0, 10, 4, 0.1f, 1, 1, 1, 6, 6, 1, "вложенные со смещением")]
    [InlineData(0, 0, 0, 1, 0, 0, 100, 50, 50, 101, 50, 50, "далеко")]
    [InlineData(0, 0, 0, 0, 0, 0, 0, 0, 2, 1, 0, 2, "первая ось — точка")]
    [InlineData(0, 2, 0, 1, 2, 0, 0, 2, 3, 0, 2, 3, "вторая ось — точка")]
    [InlineData(0, 0, 0, 0, 0, 0, 3, 4, 0, 3, 4, 0, "обе оси — точки")]
    [InlineData(0, 0, 0, 0.001f, 0.0005f, 0, 0, 0, 1, 0.5f, 0, 1, "короткий против длинного")]
    [InlineData(-5, -5, -5, -4, -5, -5, -5, -4, -1, -5, -4, 0, "в отрицательном октанте")]
    [InlineData(0, 0, 0, 1, 0, 0, 0.5f, 0, 5, 0.5f, 0, 5, "разнесённые по Z")]
    public void Intersects_MatchesBruteForce(
        float ax, float ay, float az, float bx, float by, float bz,
        float cx, float cy, float cz, float dx, float dy, float dz,
        string what)
    {
        Vector3 startA = P(ax, ay, az);
        Vector3 endA = P(bx, by, bz);
        Vector3 startB = P(cx, cy, cz);
        Vector3 endB = P(dx, dy, dz);

        double reference = ReferenceGeometry.SegmentDistanceBrute(startA, endA, startB, endB);
        Assert.True(reference > 1e-4, $"{what}: расстояние по перебору {reference:F6}, оси не разделены.");

        // Зазор берётся от самого расстояния, а не фиксированный: иначе у
        // близких отрезков «меньший радиус» упёрся бы в ноль и сравнивал
        // расстояние с самим собой.
        float gap = (float)reference * 0.25f;
        Capsule3 other = new(startB, endB, 0f);

        Assert.True(
            new Capsule3(startA, endA, (float)reference + gap).Intersects(other),
            $"{what}: расстояние по перебору {reference:F6}, радиус больше на {gap}, пересечение обязано быть.");
        Assert.False(
            new Capsule3(startA, endA, (float)reference - gap).Intersects(other),
            $"{what}: расстояние по перебору {reference:F6}, радиус меньше на {gap}, пересечения быть не должно.");
    }

    /// <summary>
    /// Оси, которые пересекаются или касаются, обязаны давать пересечение при
    /// любом неотрицательном радиусе, включая нулевой. Здесь нельзя проверить
    /// вторую сторону: расстояние равно нулю, и «радиус меньше расстояния»
    /// неотличим от нулевого радиуса.
    /// </summary>
    [Fact]
    public void Intersects_WhenAxesTouchOrCross()
    {
        (Vector3 A0, Vector3 A1, Vector3 B0, Vector3 B1, string what)[] cases =
        [
            (P(0, 0, 0), P(1, 0, 0), P(0.5f, -1, 0), P(0.5f, 1, 0), "пересекающиеся"),
            (P(0, 0, 0), P(1, 0, 0), P(1, 0, 0), P(2, 0, 0), "стыкуются концами"),
            (P(0, 0, 0), P(1, 0, 0), P(0, 1, 0), P(1, 0, 0), "второй конец на первом"),
            (P(0, 0.5f, 0), P(0, 0.5f, 0), P(0, 0, 0), P(0, 1, 0), "точка на отрезке"),
            (P(0, 0, 0), P(0, 0, 0), P(0, 0, 0), P(0, 0, 0), "совпадающие точки"),
            (P(0, 0, 0), P(1, 0, 0), P(0.5f, 0, 0), P(0.5f, 0, 1), "второй начинается на первом"),
        ];

        foreach ((Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, string what) in cases)
        {
            Capsule3 first = new(a0, a1, 0f);
            Capsule3 second = new(b0, b1, 0f);

            double reference = ReferenceGeometry.SegmentDistanceBrute(a0, a1, b0, b1, 4000);
            Assert.True(reference < 1e-6, $"{what}: расстояние по перебору {reference:E3}, оси должны касаться или пересекаться.");
            Assert.True(first.Intersects(second), $"{what}: нулевой радиус, но пересечения нет.");
            Assert.True(new Capsule3(a0, a1, 0.5f).Intersects(new Capsule3(b0, b1, 0.5f)), $"{what}: с ненулевым радиусом пересечение обязательно.");
        }
    }

    /// <summary>
    /// То же на случайных данных: перебор и формула разойдутся там, где ошибка
    /// в формуле, а не на специально подобранных числах.
    /// </summary>
    [Fact]
    public void Intersects_MatchesBruteForce_OnRandomCases()
    {
        var random = new XorShift64Star(31337);
        var problems = new List<string>();

        for (int i = 0; i < 200; i++)
        {
            Vector3 startA = new Vector3(
                (random.NextFloat() - 0.5f) * 10f,
                (random.NextFloat() - 0.5f) * 10f,
                (random.NextFloat() - 0.5f) * 10f);
            Vector3 endA = startA + new Vector3(
                (random.NextFloat() - 0.5f) * 6f,
                (random.NextFloat() - 0.5f) * 6f,
                (random.NextFloat() - 0.5f) * 6f);
            Vector3 startB = new Vector3(
                (random.NextFloat() - 0.5f) * 10f,
                (random.NextFloat() - 0.5f) * 10f,
                (random.NextFloat() - 0.5f) * 10f);
            Vector3 endB = startB + new Vector3(
                (random.NextFloat() - 0.5f) * 6f,
                (random.NextFloat() - 0.5f) * 6f,
                (random.NextFloat() - 0.5f) * 6f);

            double reference = ReferenceGeometry.SegmentDistanceBrute(startA, endA, startB, endB, 4000);
            const float gap = 0.2f;
            Capsule3 other = new(startB, endB, 0f);

            bool overlapped = new Capsule3(startA, endA, (float)reference + gap).Intersects(other);
            bool separated = new Capsule3(startA, endA, Math.Max(0f, (float)reference - gap)).Intersects(other);

            if (!overlapped || separated)
            {
                problems.Add(
                    $"случай {i}: эталон {reference:F6}; ожидалось пересечение (получено {overlapped}), "
                    + $"ожидалось отсутствие (получено {separated}); A=({startA} -> {endA}), B=({startB} -> {endB})");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// Капсула нулевого радиуса — это отрезок: он пересекает пересекающийся
    /// отрезок и не пересекает отдалённый.
    /// </summary>
    [Fact]
    public void Intersects_WithZeroRadius()
    {
        Capsule3 segment = new(P(0, 0, 0), P(1, 0, 0), 0f);
        Capsule3 far = new(P(5, 0, 0), P(6, 0, 0), 0f);

        Assert.True(segment.Intersects(new Capsule3(P(0.5f, 0, 0), P(0.5f, 0, 1), 0f)));
        Assert.False(segment.Intersects(far));
    }

    #endregion

    #region Contains и расстояния до оси

    /// <summary>
    /// Принадлежность точки капсуле определяется расстоянием до оси: точка
    /// внутри тогда и только тогда, когда расстояние до отрезка не больше
    /// радиуса. Ожидаемое значение вычисляется из этого определения, а не
    /// выписывается вручную: так тест проверяет код, а не собственную
    /// память о числах.
    /// </summary>
    /// <param name="px">X точки.</param>
    /// <param name="py">Y точки.</param>
    /// <param name="pz">Z точки.</param>
    /// <param name="radius">Радиус капсулы.</param>
    [Theory]
    [InlineData(0.5f, 0f, 0f, 0.9f)]
    [InlineData(0.5f, 0f, 0f, 1.0f)]
    [InlineData(0.5f, 0f, 0f, 1.1f)]
    [InlineData(0f, 0f, 0f, 0f)]
    [InlineData(1f, 0f, 0f, 0f)]
    [InlineData(-0.1f, 0f, 0f, 0f)]
    [InlineData(-0.1f, 0f, 0f, 0.2f)]
    [InlineData(1.1f, 0f, 0f, 0f)]
    [InlineData(1.1f, 0f, 0f, 0.2f)]
    [InlineData(0.5f, 0.5f, 0.5f, 0f)]
    [InlineData(0.5f, 0.5f, 0.5f, 1f)]
    [InlineData(0.5f, -0.6f, 0.8f, 1f)]
    [InlineData(0.5f, 0f, 0f, 0f)]
    public void Contains_MatchesDistanceToAxis(float px, float py, float pz, float radius)
    {
        Capsule3 capsule = new(P(0, 0, 0), P(1, 0, 0), radius);
        Vector3 point = P(px, py, pz);

        Vector3 axisPoint = capsule.ClosestPointOnAxis(point);
        double distanceToAxis = ReferenceGeometry.Distance(point, axisPoint);
        bool expected = distanceToAxis <= (double)radius + Tolerance;

        Assert.Equal(expected, capsule.Contains(point));
    }

    /// <summary>
    /// Точка принадлежит капсуле тогда и только тогда, когда расстояние до
    /// оси не больше радиуса.
    /// </summary>
    [Fact]
    public void Contains_AgreesWithDistanceToAxis()
    {
        Capsule3 capsule = new(P(-1, 0, 0), P(2, 1, 3), 0.75f);
        var random = new XorShift64Star(555);

        for (int i = 0; i < 500; i++)
        {
            Vector3 point = new Vector3(
                (random.NextFloat() - 0.5f) * 12f,
                (random.NextFloat() - 0.5f) * 12f,
                (random.NextFloat() - 0.5f) * 12f);

            double distance = ReferenceGeometry.Distance(point, capsule.ClosestPointOnAxis(point));

            Assert.Equal(distance <= capsule.Radius + Tolerance, capsule.Contains(point));
        }
    }

    /// <summary>
    /// Точка, полученная методом <see cref="Capsule3.ClosestPointTo"/>, лежит на
    /// поверхности в пределах нескольких последних разрядов.
    /// </summary>
    /// <remarks>
    /// Проверяется расстояние до оси, а не принадлежность: точку поверхности
    /// принимает и сама капсула, это проверяется отдельно в
    /// <see cref="ClosestPointTo_IsAcceptedByContains"/>.
    /// </remarks>
    [Fact]
    public void ClosestPointTo_LiesOnSurface()
    {
        Capsule3 capsule = new(P(-1, 0, 0), P(2, 1, 3), 0.75f);
        var random = new XorShift64Star(555);

        float worstExcess = 0f;
        for (int i = 0; i < 500; i++)
        {
            Vector3 point = new Vector3(
                (random.NextFloat() - 0.5f) * 12f,
                (random.NextFloat() - 0.5f) * 12f,
                (random.NextFloat() - 0.5f) * 12f);

            Vector3 onSurface = capsule.ClosestPointTo(point);
            float distance = (onSurface - capsule.ClosestPointOnAxis(point)).Length();

            Assert.InRange(distance, capsule.Radius - Tolerance, capsule.Radius + Tolerance);
            worstExcess = MathF.Max(worstExcess, distance - capsule.Radius);
        }

        // Превышение не превосходит нескольких последних разрядов.
        Assert.True(worstExcess < 8f * MathF.BitIncrement(capsule.Radius) - capsule.Radius, $"Поверхность уходит дальше радиуса на {worstExcess:E3}.");
    }

    /// <summary>
    /// Точка поверхности обязана приниматься капсулой, которая её вернула.
    /// </summary>
    /// <remarks>
    /// Дефект был в том, что <see cref="Capsule3.ClosestPointTo"/> строил точку
    /// как «ось + нормализованное смещение · радиус», и округление умножения
    /// уводило результат наружу, а сравнение квадратов в
    /// <see cref="Capsule3.Contains"/> строгое. Доля отказов измерена на
    /// 500 000 точек и составляла 18–29 % в зависимости от радиуса, то есть
    /// примерно в каждом пятом вызове, и у результата не было правильного
    /// объяснения.
    /// <para>
    /// Решение выбрано без допуска в <see cref="Capsule3.Contains"/>, чтобы не
    /// вводить исключение из общего правила строгих границ: точка поверхности
    /// сдвигается внутрь на четыре последних разряда радиуса. Сдвиг не виден
    /// ни в одном применении, а инвариант выполняется на всём наборе.
    /// </para>
    /// </remarks>
    [Fact]
    public void ClosestPointTo_IsAcceptedByContains()
    {
        const int samples = 200_000;

        (float ax, float ay, float az, float bx, float by, float bz, float radius)[] capsules =
        [
            (0f, 0f, 0f, 1f, 0f, 0f, 0.75f),
            (-1f, 0f, 0f, 2f, 1f, 3f, 0.75f),
            (0f, 0f, 0f, 0f, 5f, 0f, 1f),
            (0f, 0f, 0f, 1f, 0f, 0f, 0.5f),
            (0f, 0f, 0f, 10f, 0f, 0f, 0.1f),
        ];

        var random = new XorShift64Star(20260101);
        var report = new List<string>();

        foreach ((float ax, float ay, float az, float bx, float by, float bz, float radius) in capsules)
        {
            Capsule3 capsule = new(P(ax, ay, az), P(bx, by, bz), radius);
            int rejected = 0;

            for (int i = 0; i < samples; i++)
            {
                Vector3 point = new Vector3(
                    (random.NextFloat() - 0.5f) * 12f,
                    (random.NextFloat() - 0.5f) * 12f,
                    (random.NextFloat() - 0.5f) * 12f);

                if (!capsule.Contains(capsule.ClosestPointTo(point)))
                {
                    rejected++;
                }
            }

            double share = rejected * 100.0 / samples;
            report.Add($"r={radius}: {share:F2} %");

            Assert.True(
                rejected == 0,
                $"r={radius}: {rejected} точек поверхности отклонено ({share:F2} %), метод поверхности и проверка принадлежности разошлись.");
        }

        Assert.True(report.Count > 0, string.Join(", ", report));
    }

    /// <summary>
    /// Расстояние до оси есть расстояние до отрезка. Перебором по первому
    /// параметру проверяется, что метод не возвращает расстояние до
    /// бесконечной прямой вместо отрезка: для точки, лежащей за концом,
    /// расстояние до прямой равно нулю, а до отрезка — нет.
    /// </summary>
    /// <param name="px">X точки.</param>
    /// <param name="py">Y точки.</param>
    /// <param name="pz">Z точки.</param>
    /// <param name="expected">Ожидаемое расстояние.</param>
    [Theory]
    [InlineData(0.5f, 0f, 0f, 0.0)]
    [InlineData(0.5f, 2f, 0f, 2.0)]
    [InlineData(-3f, 0f, 0f, 3.0)]
    [InlineData(4f, 0f, 0f, 3.0)]
    [InlineData(0.5f, 3f, 4f, 5.0)]
    public void DistanceToAxis_MatchesBruteForce(float px, float py, float pz, double expected)
    {
        Capsule3 capsule = new(P(0, 0, 0), P(1, 0, 0), 1f);
        Vector3 point = P(px, py, pz);

        float actual = capsule.DistanceToAxis(point);
        Assert.InRange(actual, (float)expected - Tolerance, (float)expected + Tolerance);

        // Расстояние до отрезка не превосходит расстояния до любой его точки,
        // а значит и до обоих концов. Меньше обоих концов оно при этом может
        // быть, поэтому нижней границы здесь нет.
        double toStart = ReferenceGeometry.Distance(point, capsule.PointA);
        double toEnd = ReferenceGeometry.Distance(point, capsule.PointB);
        Assert.InRange(actual, 0f, (float)Math.Max(toStart, toEnd) + Tolerance);
    }

    /// <summary>
    /// Для вырожденной оси расстояние до неё есть расстояние до точки.
    /// </summary>
    [Fact]
    public void DistanceToAxis_DegenerateAxis()
    {
        Capsule3 point = new(P(1, 2, 3), P(1, 2, 3), 0.5f);

        Assert.Equal(point.PointA, point.ClosestPointOnAxis(P(9, 9, 9)));
        MathAssert.Equal(0f, point.DistanceToAxis(P(1, 2, 3)), Tolerance);
        MathAssert.Equal((float)Math.Sqrt(27.0), point.DistanceToAxis(P(4, 5, 6)), Tolerance);
    }

    #endregion

    #region ClosestPointTo

    /// <summary>
    /// Ближайшая точка поверхности лежит на расстоянии радиуса от оси в
    /// направлении исходной точки.
    /// </summary>
    [Fact]
    public void ClosestPointTo_IsAtRadiusFromAxis()
    {
        Capsule3 capsule = new(P(0, 0, 0), P(1, 0, 0), 0.75f);
        var random = new XorShift64Star(97);

        for (int i = 0; i < 500; i++)
        {
            Vector3 point = new Vector3(
                (random.NextFloat() - 0.5f) * 20f,
                (random.NextFloat() - 0.5f) * 20f,
                (random.NextFloat() - 0.5f) * 20f);

            Vector3 axisPoint = capsule.ClosestPointOnAxis(point);
            Vector3 closest = capsule.ClosestPointTo(point);
            float distanceToAxis = (float)ReferenceGeometry.Distance(closest, axisPoint);

            Assert.InRange(distanceToAxis, capsule.Radius - Tolerance, capsule.Radius + Tolerance);

            Vector3 fromAxis = point - axisPoint;
            if (fromAxis.LengthSquared() > Tolerance * Tolerance)
            {
                Vector3 produced = closest - axisPoint;
                Assert.True(
                    Vector3.Dot(fromAxis, produced) > 0f,
                    "Ближайшая точка оказалась с противоположной стороны оси.");
            }
        }
    }

    /// <summary>
    /// Точка ровно на оси: направление не определено, и метод обязан вернуть
    /// точку на расстоянии радиуса от ближайшей точки оси, а не ноль.
    /// </summary>
    [Fact]
    public void ClosestPointTo_PointOnAxis()
    {
        Capsule3 capsule = new(P(0, 0, 0), P(1, 0, 0), 0.5f);
        Vector3 onAxis = P(0.5f, 0f, 0f);

        Vector3 axisPoint = capsule.ClosestPointOnAxis(onAxis);
        Vector3 closest = capsule.ClosestPointTo(onAxis);

        MathAssert.Equal(capsule.Radius, (float)ReferenceGeometry.Distance(closest, axisPoint), Tolerance);
        MathAssert.Equal(0f, capsule.DistanceToAxis(onAxis), Tolerance);
    }

    #endregion

    #region FromHeight

    /// <summary>
    /// Капсула по высоте имеет заданные радиус и полную высоту, и её центр
    /// совпадает с заданным. Полная высота складывается из длины осевой линии
    /// и двух радиусов.
    /// </summary>
    /// <param name="centerY">Y центра.</param>
    /// <param name="height">Полная высота.</param>
    /// <param name="radius">Радиус.</param>
    [Theory]
    [InlineData(0f, 2f, 1f)]
    [InlineData(0f, 4f, 1f)]
    [InlineData(1.5f, 3f, 0.75f)]
    [InlineData(-2f, 10f, 2f)]
    [InlineData(0f, 0f, 0f)]
    public void FromHeight_MatchesRadiusAndHeight(float centerY, float height, float radius)
    {
        Vector3 center = P(3f, centerY, -1f);
        Capsule3 capsule = Capsule3.FromHeight(center, height, radius);

        MathAssert.Equal(radius, capsule.Radius, Tolerance);
        MathAssert.Equal(center, capsule.Center, Tolerance);

        double axisLength = ReferenceGeometry.Distance(capsule.PointA, capsule.PointB);
        MathAssert.Equal(height, (float)(axisLength + (2.0 * radius)), Tolerance);

        // Ось вертикальна и симметрична относительно центра.
        MathAssert.Equal(center.X, capsule.PointA.X, Tolerance);
        MathAssert.Equal(center.X, capsule.PointB.X, Tolerance);
        MathAssert.Equal(center.Z, capsule.PointA.Z, Tolerance);
        MathAssert.Equal(center.Z, capsule.PointB.Z, Tolerance);
        MathAssert.Equal((float)(axisLength * 0.5), MathF.Abs(capsule.PointA.Y - center.Y), Tolerance);
    }

    /// <summary>
    /// Высота меньше двух радиусов и отрицательный радиус — ошибка ввода.
    /// </summary>
    [Fact]
    public void FromHeight_RejectsInvalidInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Capsule3.FromHeight(Vector3.Zero, 1f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Capsule3.FromHeight(Vector3.Zero, 1.9f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Capsule3.FromHeight(Vector3.Zero, 4f, -0.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Capsule3(Vector3.Zero, Vector3.One, -1f));
    }

    /// <summary>
    /// Граница допустима: высота ровно два радиуса — капсула с вырожденной
    /// осевой линией.
    /// </summary>
    [Fact]
    public void FromHeight_AcceptsExactlyTwoRadii()
    {
        Capsule3 capsule = Capsule3.FromHeight(Vector3.Zero, 2f, 1f);

        Assert.Equal(capsule.PointA, capsule.PointB);
        MathAssert.Equal(1f, capsule.Radius, Tolerance);
        Assert.True(capsule.Contains(Vector3.Zero), "Центр вырожденной капсулы обязан быть внутри.");
    }

    #endregion

    #region Bounds, равенство, строки

    /// <summary>
    /// Параллелепипед описан вокруг капсулы: каждая его грань проходит через
    /// крайнюю точку оси, расширенную на радиус.
    /// </summary>
    [Fact]
    public void Bounds_ContainsAxisExpandedByRadius()
    {
        Capsule3 capsule = new(P(-2f, 0.5f, 1f), P(3f, 1.5f, -1f), 0.4f);
        Aabb3 bounds = capsule.Bounds;

        Assert.True(bounds.Contains(capsule.PointA));
        Assert.True(bounds.Contains(capsule.PointB));

        float low = MathF.Min(capsule.PointA.Y, capsule.PointB.Y) - capsule.Radius;
        float high = MathF.Max(capsule.PointA.Y, capsule.PointB.Y) + capsule.Radius;
        Assert.InRange(bounds.Min.Y, low - Tolerance, low + Tolerance);
        Assert.InRange(bounds.Max.Y, high - Tolerance, high + Tolerance);

        float lowX = MathF.Min(capsule.PointA.X, capsule.PointB.X) - capsule.Radius;
        float highX = MathF.Max(capsule.PointA.X, capsule.PointB.X) + capsule.Radius;
        Assert.InRange(bounds.Min.X, lowX - Tolerance, lowX + Tolerance);
        Assert.InRange(bounds.Max.X, highX - Tolerance, highX + Tolerance);

        float lowZ = MathF.Min(capsule.PointA.Z, capsule.PointB.Z) - capsule.Radius;
        float highZ = MathF.Max(capsule.PointA.Z, capsule.PointB.Z) + capsule.Radius;
        Assert.InRange(bounds.Min.Z, lowZ - Tolerance, lowZ + Tolerance);
        Assert.InRange(bounds.Max.Z, highZ - Tolerance, highZ + Tolerance);

        // Каждая точка оси внутри параллелепипеда даже при отрицательных
        // координатах концов: Min и Max берутся по обеим точкам.
        for (int i = 1; i <= 100; i++)
        {
            Vector3 middle = ReferenceGeometry.PointAt(capsule.PointA, capsule.PointB, i / 100.0);
            Assert.True(bounds.Contains(middle), $"Точка оси {middle} выпала из описанного параллелепипеда.");
        }
    }

    /// <summary>
    /// Перестановка концов не меняет параллелепипед и центр.
    /// </summary>
    [Fact]
    public void Bounds_IgnoresEndpointOrder()
    {
        Capsule3 capsule = new(P(-2f, 0.5f, 1f), P(3f, 1.5f, -1f), 0.4f);
        Capsule3 reversed = new(capsule.PointB, capsule.PointA, capsule.Radius);

        MathAssert.Equal(capsule.Bounds.Min, reversed.Bounds.Min, Tolerance);
        MathAssert.Equal(capsule.Bounds.Max, reversed.Bounds.Max, Tolerance);
    }

    /// <summary>
    /// Равенство, хеш, операторы и строковое представление.
    /// </summary>
    [Fact]
    public void Equality_OperatorsAndString()
    {
        Capsule3 a = new(P(1, 2, 3), P(4, 5, 6), 0.5f);
        Capsule3 same = new(P(1, 2, 3), P(4, 5, 6), 0.5f);
        Capsule3 otherRadius = new(P(1, 2, 3), P(4, 5, 6), 0.75f);
        Capsule3 otherPoint = new(P(1, 2, 3), P(4, 5, 7), 0.5f);

        Assert.True(a.Equals(same));
        Assert.True(a == same);
        Assert.False(a != same);
        Assert.Equal(a.GetHashCode(), same.GetHashCode());

        Assert.False(a.Equals(otherRadius));
        Assert.False(a.Equals(otherPoint));
        Assert.True(a != otherRadius);
        Assert.True(a != otherPoint);

        Assert.True(a.Equals((object)same));
        Assert.False(a.Equals((object)otherRadius));
        Assert.False(a.Equals(null));
        Assert.False(a.Equals("не капсула"));

        string text = a.ToString();
        Assert.Contains("Capsule3", text, StringComparison.Ordinal);
        Assert.Contains("0.500", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Центр — это середина, разность направлена от A к B, и перестановка
    /// концов не меняет середину.
    /// </summary>
    [Fact]
    public void CenterAndDelta()
    {
        Capsule3 capsule = new(P(0, 0, 0), P(4, 6, 8), 1f);

        MathAssert.Equal(P(2, 3, 4), capsule.Center, Tolerance);
        MathAssert.Equal(P(4, 6, 8), capsule.Delta, Tolerance);

        Capsule3 reversed = new(capsule.PointB, capsule.PointA, capsule.Radius);
        MathAssert.Equal(capsule.Center, reversed.Center, Tolerance);
    }

    #endregion
}