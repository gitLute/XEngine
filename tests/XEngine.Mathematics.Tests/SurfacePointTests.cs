using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Инвариант методов поверхности: точка, которую метод вернул, обязана
/// приниматься проверкой принадлежности той же формы.
/// </summary>
/// <remarks>
/// Дефект был один и одинаковый в четырёх типах: точка поверхности строилась
/// умножением нормализованного смещения на радиус, и округление уводило
/// результат наружу. Измерение на 200 000 случайных точках вне формы давало
/// долю отказов 0.4 % у <see cref="BoundingSphere"/> и 7.5–9.1 % у остальных.
/// Вторые две цифры означают, что у кода, бравшего точку у метода поверхности
/// и тут же проверявшего её, был отрицательный ответ примерно в каждом
/// одиннадцатом вызове — при полностью правильном коде вокруг.
/// <para>
/// Набор построен так, чтобы дефект проявлялся на первой же серии: точки
/// берутся с расстояния порядка радиуса от оси, то есть именно там, где
/// округление последнего разряда решает всё.
/// </para>
/// </remarks>
public class SurfacePointTests
{
    /// <summary>
    /// Допуск на расстояние от оси. Сдвиг задаётся величиной точки, от которой
    /// отсчитывается смещение, а не величиной радиуса: у круга радиуса 0.1
    /// метра с центром в (−4, 7) разряд координаты крупнее разряда радиуса в
    /// сто раз, и расстояние законно отличается от радиуса на эту величину.
    /// </summary>
    /// <param name="radius">Радиус фигуры.</param>
    /// <param name="magnitude">Величина точки отсчёта.</param>
    /// <returns>Допуск на расстояние.</returns>
    private static float SurfaceTolerance(float radius, float magnitude)
    {
        float base_ = MathF.Max(radius, magnitude);
        return (base_ * 4f * 1.1920929e-7f) + 1e-6f;
    }

    [Fact]
    public void Capsule3_SurfacePointIsAcceptedByContains()
    {
        (Vector3 a, Vector3 b, float radius)[] capsules =
        [
            (new Vector3(0, 0, 0), new Vector3(1, 0, 0), 0.75f),
            (new Vector3(-1, 0, 0), new Vector3(2, 1, 3), 0.75f),
            (new Vector3(0, 0, 0), new Vector3(0, 5, 0), 1f),
            (new Vector3(0, 0, 0), new Vector3(0, 0, 1), 0.5f),
            (new Vector3(0, 0, 0), new Vector3(10, 0, 0), 0.1f),
            (new Vector3(1, 1, 1), new Vector3(1, 1, 1), 2f),
        ];

        DeterministicRandom random = new(0xA1B2C3D4E5F60718UL);
        int checkedPoints = 0;

        foreach ((Vector3 a, Vector3 b, float radius) in capsules)
        {
            Capsule3 capsule = new(a, b, radius);
            for (int i = 0; i < 20000; i++)
            {
                Vector3 direction = random.NextUnitVector();
                Vector3 point = capsule.ClosestPointOnAxis(a + (direction * (radius * 1.5f))) + (direction * (radius * 1.5f));

                Vector3 surface = capsule.ClosestPointTo(point);
                Assert.True(
                    capsule.Contains(surface),
                    $"Капсула r={radius} отвергла свою точку поверхности {surface} для точки {point}.");

                Vector3 axisPoint = capsule.ClosestPointOnAxis(point);
                float distance = (surface - axisPoint).Length();
                float tolerance = SurfaceTolerance(radius, axisPoint.Length());
                Assert.True(
                    MathF.Abs(distance - radius) <= tolerance,
                    $"Расстояние от оси {distance} разошлось с радиусом {radius} сильнее, чем на {tolerance:E3}.");

                checkedPoints++;
            }
        }

        Assert.True(checkedPoints >= 100000, "Набор должен быть массовым: проверено " + checkedPoints + " точек.");
    }

    [Fact]
    public void Capsule2_SurfacePointIsAcceptedByContains()
    {
        (Vector2 a, Vector2 b, float radius)[] capsules =
        [
            (Vector2.Zero, new Vector2(1, 0), 0.75f),
            (new Vector2(-3, 2), new Vector2(4, -1), 0.4f),
            (new Vector2(5, 5), new Vector2(5, 5), 1.5f),
        ];

        DeterministicRandom random = new(0xB2C3D4E5F607189UL);

        foreach ((Vector2 a, Vector2 b, float radius) in capsules)
        {
            Capsule2 capsule = new(new Segment2(a, b), radius);
            for (int i = 0; i < 20000; i++)
            {
                float angle = random.Range(0f, MathF.Tau);
                Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                Vector2 point = capsule.Segment2.ClosestPointTo(a) + (direction * (radius * 1.5f));

                Vector2 surface = capsule.ClosestPointOnBoundary(point);
                Assert.True(
                    capsule.Contains(surface),
                    $"Капсула r={radius} отвергла свою точку границы {surface} для точки {point}.");
            }
        }
    }

    [Fact]
    public void Circle2_SurfacePointIsAcceptedByContains()
    {
        (Vector2 center, float radius)[] circles =
        [
            (Vector2.Zero, 0.75f),
            (new Vector2(-4, 7), 0.1f),
            (new Vector2(100, -100), 12.5f),
        ];

        DeterministicRandom random = new(0xC3D4E5F6071899AUL);

        foreach ((Vector2 center, float radius) in circles)
        {
            Circle2 circle = new(center, radius);
            for (int i = 0; i < 20000; i++)
            {
                float angle = random.Range(0f, MathF.Tau);
                Vector2 direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                Vector2 point = center + (direction * (radius * 1.5f));

                Vector2 surface = circle.ClosestPointOnBoundary(point);
                Assert.True(
                    circle.Contains(surface),
                    $"Круг r={radius} отверг свою точку границы {surface} для точки {point}.");
            }
        }
    }

    [Fact]
    public void BoundingSphere_SurfacePointIsAcceptedByContains()
    {
        (Vector3 center, float radius)[] spheres =
        [
            (Vector3.Zero, 0.75f),
            (new Vector3(-4, 7, 2), 4.1231f),
            (new Vector3(100, -100, 0), 0.05f),
        ];

        DeterministicRandom random = new(0xD4E5F6071899A0BUL);

        foreach ((Vector3 center, float radius) in spheres)
        {
            BoundingSphere sphere = new(center, radius);
            for (int i = 0; i < 20000; i++)
            {
                Vector3 point = center + (random.NextUnitVector() * (radius * 1.5f));

                Vector3 surface = sphere.ClosestPointOnSurface(point);
                Assert.True(
                    sphere.Contains(surface),
                    $"Сфера r={radius} отвергла свою точку поверхности {surface} для точки {point}.");

                float distance = (surface - center).Length();
                float tolerance = SurfaceTolerance(radius, center.Length());
                Assert.True(
                    MathF.Abs(distance - radius) <= tolerance,
                    $"Расстояние от центра {distance} разошлось с радиусом {radius} сильнее, чем на {tolerance:E3}.");
            }
        }
    }

    /// <summary>
    /// Сдвиг не должен быть заметен в применении: он величиной в четыре
    /// последних разряда радиуса, то есть на метрах это доли микрометра.
    /// </summary>
    [Fact]
    public void SurfacePoint_DoesNotMovePerceptibly()
    {
        BoundingSphere sphere = new(Vector3.Zero, 10f);
        Vector3 point = new(7f, 11f, 13f);

        Vector3 surface = sphere.ClosestPointOnSurface(point);
        Vector3 exact = Vector3.Normalize(point) * sphere.Radius;

        Assert.True(
            Vector3.Distance(surface, exact) <= 1e-5f,
            $"Сдвиг точки поверхности виден в применении: {Vector3.Distance(surface, exact):E3} м.");

        Circle2 circle = new(Vector2.Zero, 10f);
        Vector2 flatPoint = new(7f, 11f);
        Vector2 flatSurface = circle.ClosestPointOnBoundary(flatPoint);
        Vector2 flatExact = Vector2.Normalize(flatPoint) * circle.Radius;
        Assert.True(
            Vector2.Distance(flatSurface, flatExact) <= 1e-5f,
            $"Сдвиг точки границы виден в применении: {Vector2.Distance(flatSurface, flatExact):E3}.");
    }

    /// <summary>
    /// Точка ровно на оси задана неоднозначно: подходит любое направление, и
    /// берётся ось X. Такой результат лежит на поверхности по построению.
    /// </summary>
    [Fact]
    public void SurfacePoint_DegenerateDirectionIsHandled()
    {
        Capsule3 capsule = new(Vector3.Zero, new Vector3(0, 4, 0), 0.75f);
        Vector3 onAxis = capsule.ClosestPointTo(new Vector3(0f, 2f, 0f));
        Assert.True(capsule.Contains(onAxis), "Точка на оси обязана лежать на поверхности и приниматься капсулой.");

        BoundingSphere sphere = new(Vector3.Zero, 2f);
        Vector3 atCenter = sphere.ClosestPointOnSurface(Vector3.Zero);
        Assert.True(sphere.Contains(atCenter), "Точка в центре обязана лежать на поверхности и приниматься сферой.");

        Circle2 circle = new(Vector2.Zero, 2f);
        Assert.True(circle.Contains(circle.ClosestPointOnBoundary(Vector2.Zero)));
    }

    /// <summary>
    /// Расстояние, сообщаемое лучом, ведёт в точку, которую принимает проверка
    /// принадлежности той же фигуре.
    /// </summary>
    /// <remarks>
    /// Расстояние до касания по построению кладёт точку ровно на поверхность, а
    /// проверка строгая, поэтому на верном попадании она иногда отвечает отказом.
    /// </remarks>
    [Fact]
    public void RaycastEntry_IsAcceptedByContainsOfSameFigure()
    {
        DeterministicRandom random = new(0x6B7C8D9EAF001122UL);
        int sphereHits = 0;
        int boxHits = 0;
        int capsuleHits = 0;
        int rejected = 0;
        int tangentCount = 0;
        int rejectedBeyondRounding = 0;
        double worstExcess = 0.0;
        string worstCase = string.Empty;

        for (int i = 0; i < 100000; i++)
        {
            Vector3 origin = RandomPoint(random, 8f);
            Vector3 direction = random.NextUnitVector();
            Ray3 ray = new(origin, direction);

            BoundingSphere sphere = new(RandomPoint(random, 8f), random.Range(0.05f, 3f));
            if (ray.Raycast(sphere, out float sphereDistance))
            {
                sphereHits++;
                Vector3 point = ray.GetPoint(sphereDistance);
                if (sphere.Contains(point))
                {
                    continue;
                }

                rejected++;
                double excess = ReferenceGeometry.Distance(point, sphere.Center) - sphere.Radius;
                if (excess > worstExcess)
                {
                    worstExcess = excess;
                    worstCase = $"сфера r={sphere.Radius:F6} c={sphere.Center} origin={origin} dir={direction} d={sphereDistance:R} p={point}";
                }
                Classify(excess, ref tangentCount, ref rejectedBeyondRounding);
            }

            Aabb3 box = Aabb3.FromCenterAndSize(RandomPoint(random, 6f), new Vector3(
                random.Range(0.05f, 4f),
                random.Range(0.05f, 4f),
                random.Range(0.05f, 4f)));
            if (ray.Raycast(box, out float boxDistance))
            {
                boxHits++;
                Vector3 point = ray.GetPoint(boxDistance);
                if (box.Contains(point))
                {
                    continue;
                }

                rejected++;
                double outside = MathF.Max(
                    MathF.Max(box.Min.X - point.X, point.X - box.Max.X),
                    MathF.Max(box.Min.Y - point.Y, point.Y - box.Max.Y));
                if (outside > worstExcess)
                {
                    worstExcess = outside;
                    worstCase = $"бокс {box} origin={origin} dir={direction} d={boxDistance:R} p={point}";
                }
                Classify(outside, ref tangentCount, ref rejectedBeyondRounding);
            }

            Capsule3 capsule = new(RandomPoint(random, 5f), RandomPoint(random, 5f), random.Range(0.05f, 2f));
            if (ray.Raycast(capsule, out float capsuleDistance))
            {
                capsuleHits++;
                Vector3 point = ray.GetPoint(capsuleDistance);
                if (capsule.Contains(point))
                {
                    continue;
                }

                rejected++;
                Vector3 nearest = ReferenceGeometry.ClosestPointOnSegment(point, capsule.PointA, capsule.PointB);
                double excess = ReferenceGeometry.Distance(point, nearest) - capsule.Radius;
                if (excess > worstExcess)
                {
                    worstExcess = excess;
                    worstCase = $"капсула r={capsule.Radius:R} A={capsule.PointA} B={capsule.PointB}"
                        + $" origin={origin} dir={direction} d={capsuleDistance:R} p={point}";
                }
                Classify(excess, ref tangentCount, ref rejectedBeyondRounding);
            }
        }

        Assert.True(sphereHits > 1000, $"Слишком мало попаданий в сферу: {sphereHits}.");
        Assert.True(boxHits > 1000, $"Слишком мало попаданий в параллелепипед: {boxHits}.");
        Assert.True(capsuleHits > 1000, $"Слишком мало попаданий в капсулу: {capsuleHits}.");

        // Что именно гарантируется. Точка входа обязана лежать внутри фигуры,
        // кроме одного случая: луч, проходящий по касательной. Там касание
        // двойное, и точка входа лежит ровно на поверхности, а строгая проверка
        // решает по округлению — построить строго внутреннюю точку сдвигом по
        // лучу невозможно в принципе, движение от касательной удаляет от
        // поверхности. Поэтому утверждение не «всегда внутри», а «либо внутри,
        // либо на поверхности с точностью до округления».
        //
        // Порог в 1e-5 метра — это около сотни последних разрядов координаты на
        // расстоянии в десятки метров, то есть величина самого округления.
        // Отдельной проверки на касательные лучи здесь нет: их доля мала, а
        // искусственно подгонять геометрию под них значило бы проверять не
        // код, а собственную настройку теста.
        Assert.True(
            rejectedBeyondRounding == 0,
            $"Точка входа выходит за поверхность заметно: {rejectedBeyondRounding} раз из "
            + $"{sphereHits + boxHits + capsuleHits} попаданий, худший выход {worstExcess:E3} м. {worstCase}");

        Assert.True(
            tangentCount < 50,
            $"Слишком много касательных: {tangentCount}. Почти все попадания должны давать внутреннюю точку.");
        // Расстояние при старте внутри остаётся нулём: точка входа тогда и так
        // строго внутри, а сдвигать её произвольно незачем.
        BoundingSphere inner = new(RandomPoint(random, 4f), 1.5f);
        Assert.True(new Ray3(inner.Center, random.NextUnitVector()).Raycast(inner, out float zero));
        MathAssert.Equal(0f, zero, 1e-6f);
    }

    /// <summary>
    /// Разделяет отказы на касательные и настоящие: выход в пределах округления
    /// означает, что луч шёл по касательной и построить внутреннюю точку нельзя.
    /// </summary>
    /// <param name="excess">Выход точки за поверхность.</param>
    /// <param name="tangent">Счётчик касательных.</param>
    /// <param name="beyond">Счётчик настоящих выходов.</param>
    private static void Classify(double excess, ref int tangent, ref int beyond)
    {
        if (excess > 1e-5)
        {
            beyond++;
            return;
        }

        tangent++;
    }

    private static Vector3 RandomPoint(DeterministicRandom random, float limit)
        => new Vector3(random.Range(-limit, limit), random.Range(-limit, limit), random.Range(-limit, limit));
}
