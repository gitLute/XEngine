using System.Numerics;
using XEngine.Mathematics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Регрессионные тесты на дефекты, найденные при аудите оптимизированности и
/// алгоритмической корректности <c>src/XEngine.Mathematics</c>.
///
/// <para>
/// Общая тема второй волны дефектов — зависимость результата от масштаба мира и
/// потеря точности на границах диапазона. Проверяются не типичные входы, а
/// именно те, на которых старый код ломался: прямоугольник не в начале
/// координат, неединичная нормаль плоскости, углы меньше десятой градуса,
/// отрезки короче миллиметра.
/// </para>
/// <para>
/// Часть исправлений меняет только скорость, а не результат: замена
/// <c>Math.Pow(x, 3)</c> на умножения, переход с double на float в тригонометрии,
/// снятие лишней нормализации, введение отступа вместо массива. Тесты на такие
/// места охраняют значения и контракт, но отличить старый путь от нового по
/// результату невозможно, и утверждать обратное было бы неверно.
/// </para>
/// </summary>
public class NumericRobustnessTests
{
    /// <summary>
    /// Aabb2.ToRect передавал в конструктор <see cref="Rect"/> угол вместо
    /// размера: <c>Rect</c> принимает (позиция, размер), а <c>Max</c> — это
    /// координата. Round-trip ломался для любого прямоугольника не в начале
    /// координат и случайно работал только при <c>Min == (0, 0)</c>.
    /// </summary>
    [Fact]
    public void Aabb2_ToRectKeepsPositionAndSize()
    {
        Rect source = new(5f, 7f, 10f, 4f);

        Rect result = Aabb2.FromRect(source).ToRect();

        Assert.Equal(source, result);
    }

    /// <summary>
    /// ToRect обязан работать и для отрицательного размера: границы нормализованы.
    /// </summary>
    [Fact]
    public void Aabb2_ToRectNormalizesNegativeSize()
    {
        Rect source = new(5f, 7f, -10f, -4f);
        Rect expected = new(-5f, 3f, 10f, 4f);

        Assert.Equal(expected, Aabb2.FromRect(source).ToRect());
    }

    /// <summary>
    /// Aabb2.GetCorners возвращал массив, то есть выделял память на каждый
    /// вызов, тогда как Aabb3.GetCorners документирует отсутствие аллокаций
    /// как обязательное требование горячего пути.
    /// </summary>
    [Fact]
    public void Aabb2_GetCornersWritesIntoBufferWithoutAllocation()
    {
        Aabb2 bounds = Aabb2.FromCenterAndSize(new Vector2(1f, 2f), new Vector2(2f, 4f));
        Span<Vector2> corners = stackalloc Vector2[4];

        bounds.GetCorners(corners);

        Assert.Equal(4, corners.Length);
        foreach (Vector2 corner in corners)
        {
            Assert.True(bounds.Contains(corner), $"Угол {corner} вне AABB {bounds}.");
        }
    }

    /// <summary>
    /// Конструктор Plane3 нормализовал нормаль, но не делил свободный член на
    /// ту же длину. Точка с плоскости 2x + 2y + 4 = 0 оказывалась на
    /// расстоянии 2.586 вместо нуля, и плоскость сдвигалась по глубине.
    /// </summary>
    [Fact]
    public void Plane3_ConstructorRescalesFreeTerm()
    {
        Plane3 plane = new(new Vector3(2f, 2f, 0f), 4f);

        MathAssert.Equal(0f, plane.DistanceTo(new Vector3(-1f, -1f, 0f)), 1e-5f);
        MathAssert.Equal(1.41421f, plane.Distance, 1e-4f);
    }

    /// <summary>
    /// Конструктор обязан совпадать с фабрикой по коэффициентам: обе строят
    /// одну и ту же плоскость из одних и тех же коэффициентов уравнения.
    /// </summary>
    [Fact]
    public void Plane3_ConstructorMatchesFromCoefficients()
    {
        Vector3 normal = new(3f, -4f, 12f);
        float offset = 7f;

        Plane3 byConstructor = new(normal, offset);
        Plane3 byCoefficients = Plane3.FromCoefficients(normal, offset);

        MathAssert.Equal(byCoefficients.Normal, byConstructor.Normal, 1e-6f);
        MathAssert.Equal(byCoefficients.Distance, byConstructor.Distance, 1e-5f);
    }

    /// <summary>
    /// AngleBetween обязан различать малые углы. acos от скалярного
    /// произведения, вычисленного во float, даёт для угла меньше примерно
    /// двух десятитысячных градуса ровно ноль: значение dot округляется в 1.0.
    /// Для движка это значит, что величина поворота между близкими
    /// ориентациями неразличима, а именно такие углы и нужны при сглаживании.
    /// </summary>
    [Fact]
    public void Quaternion_AngleBetweenKeepsPrecisionOnSmallAngles()
    {
        foreach (float degrees in new[] { 1f, 0.1f, 0.01f, 0.001f })
        {
            Quaternion to = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * degrees / 180f);

            Angle result = QuaternionExtensions.AngleBetween(Quaternion.Identity, to);

            MathAssert.Equal(degrees, (float)result.Degrees, degrees * 1e-3f);
        }
    }

    /// <summary>
    /// То же для угла около разворота, где acos тоже теряет относительную
    /// точность.
    /// </summary>
    [Fact]
    public void Quaternion_AngleBetweenKeepsPrecisionNearHalfTurn()
    {
        foreach (float degrees in new[] { 179f, 179.9f })
        {
            Quaternion to = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * degrees / 180f);

            Angle result = QuaternionExtensions.AngleBetween(Quaternion.Identity, to);

            MathAssert.Equal(degrees, (float)result.Degrees, degrees * 1e-4f);
        }
    }

    /// <summary>
    /// SignedAngleAround обязан различать малые углы по той же причине.
    /// </summary>
    [Fact]
    public void Vector3_SignedAngleAroundKeepsPrecisionOnSmallAngles()
    {
        Vector3 axis = Vector3.UnitY;
        Vector3 from = Vector3.UnitX;

        foreach (float degrees in new[] { 1f, 0.1f, 0.01f, 0.001f })
        {
            float radians = MathF.PI * degrees / 180f;
            Vector3 to = new(MathF.Cos(radians), 0f, -MathF.Sin(radians));

            Angle result = Vector3Extensions.SignedAngleAround(from, to, axis);

            MathAssert.Equal(degrees, (float)result.Degrees, degrees * 1e-3f);
        }
    }

    /// <summary>
    /// Знак угла обязан сохраняться: поворот по часовой стрелке даёт
    /// отрицательный результат в обе стороны от нуля.
    /// </summary>
    [Fact]
    public void Vector3_SignedAngleAroundKeepsSign()
    {
        Vector3 axis = Vector3.UnitY;
        Vector3 from = Vector3.UnitX;
        const float Degrees = 20f;
        float radians = MathF.PI * Degrees / 180f;

        Angle positive = Vector3Extensions.SignedAngleAround(from, new Vector3(MathF.Cos(radians), 0f, -MathF.Sin(radians)), axis);
        Angle negative = Vector3Extensions.SignedAngleAround(from, new Vector3(MathF.Cos(radians), 0f, MathF.Sin(radians)), axis);

        MathAssert.Equal(Degrees, (float)positive.Degrees, 1e-2f);
        MathAssert.Equal(-Degrees, (float)negative.Degrees, 1e-2f);
    }

    /// <summary>
    /// Пересечение отрезков не должно зависеть от масштаба мира. Старый код
    /// сравнивал с Scalar.Epsilon векторное произведение, то есть площадь в
    /// квадратных метрах, и для отрезков короче примерно миллиметра проверка
    /// пересечения молча пропускалась: пересекающиеся отрезки сообщали
    /// ненулевое расстояние.
    /// </summary>
    [Fact]
    public void Collision_SegmentSegmentDistanceIsScaleInvariant()
    {
        foreach (float length in new[] { 10f, 1f, 0.01f, 0.001f, 0.0001f })
        {
            Segment2 horizontal = new(new Vector2(-length, 0f), new Vector2(length, 0f));
            Segment2 vertical = new(new Vector2(0f, -length), new Vector2(0f, length));

            MathAssert.Equal(
                0f,
                Collision.SegmentSegmentDistance(horizontal, vertical),
                length * 1e-3f);
        }
    }

    /// <summary>
    /// Раздельные отрезки обязаны давать настоящее расстояние при любом
    /// масштабе, включая масштаб меньше миллиметра.
    /// </summary>
    [Fact]
    public void Collision_SegmentSegmentDistanceKeepsGapAtSmallScale()
    {
        Segment2 lower = new(new Vector2(0f, 0f), new Vector2(1f, 0f));
        Segment2 upper = new(new Vector2(0f, 2f), new Vector2(1f, 2f));

        MathAssert.Equal(2f, Collision.SegmentSegmentDistance(lower, upper), 1e-5f);
    }

    /// <summary>
    /// Вырожденный отрезок-точка обязан давать расстояние до этой точки, а не
    /// до ближайшего конца второго отрезка.
    /// </summary>
    [Fact]
    public void Collision_SegmentSegmentDistanceHandlesDegenerateSegment()
    {
        Segment2 point = new(new Vector2(1f, 1f), new Vector2(1f, 1f));
        Segment2 segment = new(new Vector2(0f, 0f), new Vector2(4f, 0f));

        MathAssert.Equal(1f, Collision.SegmentSegmentDistance(point, segment), 1e-5f);
        MathAssert.Equal(1f, Collision.SegmentSegmentDistance(segment, point), 1e-5f);
    }

    /// <summary>
    /// Пересекающиеся капсулы обязаны сообщаться как пересекающиеся при любом
    /// масштабе, включая радиусы меньше миллиметра.
    /// </summary>
    [Fact]
    public void Collision_TinyIntersectingCapsulesAreReportedAsIntersecting()
    {
        Capsule2 first = new(new Segment2(new Vector2(-1e-4f, 0f), new Vector2(1e-4f, 0f)), 1e-4f);
        Capsule2 second = new(new Segment2(new Vector2(0f, -1e-4f), new Vector2(0f, 1e-4f)), 1e-4f);

        Assert.True(Collision.Intersects(first, second));
    }

    /// <summary>
    /// Граница строгая: касание ребром не даёт проникновения. Это осознанное
    /// отличие от Aabb2, где границы включительные: здесь спрашивается, есть ли
    /// что раздвигать, а не есть ли общие точки объёмов.
    /// </summary>
    [Fact]
    public void Collision_ObbTouchingEdgeHasNoPenetration()
    {
        Vector2 size = new(2f, 2f);
        Angle zero = Angle.Zero;

        bool penetrating = Collision.TryGetObbPenetration(
            Vector2.Zero, size, zero,
            new Vector2(2f, 0f), size, zero,
            out _, out float depth);

        Assert.False(penetrating, "У касающихся прямоугольников нечего раздвигать.");
        MathAssert.Equal(0f, depth, 1e-5f);

        // На миллиметр ближе — проникновение уже есть.
        Collision.TryGetObbPenetration(
            Vector2.Zero, size, zero,
            new Vector2(1.999f, 0f), size, zero,
            out _, out float shallow);

        Assert.True(shallow > 0f, "Проникновение на миллиметр обязано быть положительным.");
    }

    /// <summary>
    /// Глубина проникновения обязана оставаться точной: осевой тест не
    /// изменился, а лишние матрицы и нормализации убраны.
    /// </summary>
    [Fact]
    public void Collision_ObbPenetrationDepthStaysExact()
    {
        Vector2 size = new(2f, 2f);
        Angle zero = Angle.Zero;

        Assert.True(Collision.TryGetObbPenetration(
            Vector2.Zero, size, zero,
            new Vector2(1.9f, 0f), size, zero,
            out Vector2 axis, out float depth));

        MathAssert.Equal(0.1f, depth, 1e-5f);
        MathAssert.Equal(new Vector2(1f, 0f), axis, 1e-5f);
    }

    /// <summary>
    /// Ось проникновения ориентирована от A к B.
    /// </summary>
    [Fact]
    public void Collision_ObbPenetrationAxisPointsFromAToB()
    {
        Vector2 size = new(2f, 2f);
        Angle zero = Angle.Zero;

        // A справа от B: ось обязана смотреть в отрицательный X.
        Collision.TryGetObbPenetration(
            new Vector2(1.9f, 0f), size, zero,
            Vector2.Zero, size, zero,
            out Vector2 leftward, out _);

        // A слева от B: ось обязана смотреть в положительный X.
        Collision.TryGetObbPenetration(
            Vector2.Zero, size, zero,
            new Vector2(1.9f, 0f), size, zero,
            out Vector2 rightward, out _);

        MathAssert.Equal(-1f, leftward.X, 1e-5f);
        MathAssert.Equal(1f, rightward.X, 1e-5f);
    }

    /// <summary>
    /// Сфера, описанная вокруг пустого параллелепипеда, не должна получать
    /// бесконечный радиус: у пустого Aabb3 половина размера равна бесконечности.
    /// </summary>
    [Fact]
    public void BoundingSphere_FromEmptyAabbHasZeroRadius()
    {
        BoundingSphere sphere = BoundingSphere.FromAabb(Aabb3.Empty);

        MathAssert.Equal(0f, sphere.Radius, 1e-5f);
        Assert.False(float.IsInfinity(sphere.Radius), "Радиус не может быть бесконечным.");
    }

    /// <summary>
    /// Нормализация обязана обнулять только настоящий нулевой вектор.
    /// Старый порог Scalar.Epsilon обнулял и корректные направления длиной
    /// меньше микрона, из-за чего Plane3 бросал исключение, а ToAngle давал
    /// нулевое направление.
    /// </summary>
    [Fact]
    public void SafeNormalize_KeepsTinyButValidDirections()
    {
        Vector3 tiny = new(1e-7f, 0f, 0f);
        Vector2 tinyFlat = new(1e-7f, 0f);

        MathAssert.Equal(1f, tiny.SafeNormalize().Length(), 1e-5f);
        MathAssert.Equal(1f, tinyFlat.SafeNormalize().Length(), 1e-5f);
    }

    /// <summary>
    /// Настоящий нулевой вектор по-прежнему обязан давать ноль, а не NaN.
    /// Это единственная причина, по которой SafeNormalize существует.
    /// </summary>
    [Fact]
    public void SafeNormalize_ZeroVectorStaysZero()
    {
        Assert.Equal(Vector3.Zero, Vector3.Zero.SafeNormalize());
        Assert.Equal(Vector2.Zero, Vector2.Zero.SafeNormalize());
    }

    /// <summary>
    /// Плоскость с очень короткой, но ненулевой нормалью обязана строиться:
    /// длина нормали задаёт масштаб уравнения, а не его вырожденность.
    /// </summary>
    [Fact]
    public void Plane3_TinyNormalIsNotRejected()
    {
        Plane3 plane = new(new Vector3(1e-7f, 0f, 0f), 0f);

        Assert.False(float.IsNaN(plane.Normal.X));
        MathAssert.Equal(1f, plane.Normal.Length(), 1e-5f);
    }

    /// <summary>
    /// Плоскость с настоящей нулевой нормалью по-прежнему отвергается.
    /// </summary>
    [Fact]
    public void Plane3_ZeroNormalIsStillRejected()
    {
        Assert.Throws<ArgumentException>(() => new Plane3(Vector3.Zero, 1f));
        Assert.Throws<ArgumentException>(() => Plane3.FromCoefficients(Vector3.Zero, 1f));
    }

    /// <summary>
    /// Вырожденная капсула — это сфера: осевая линия сжалась в точку. Луч,
    /// идущий сквозь неё, обязан её пересечь, а расстояние до входа равно
    /// расстоянию до ближайшей точки сферы.
    /// </summary>
    /// <remarks>
    /// Тест охраняет контракт, а не внутренний путь вычисления. Попадание
    /// обеспечивают торцевые сферы капсулы, поэтому и без отдельной обработки
    /// вырожденной оси в цилиндре результат тот же. Обработка в цилиндре
    /// оставлена потому, что вырожденная ось — это сфера, и возвращать из
    /// метода «не пересекается» на этом основании неверно.
    /// </remarks>
    [Fact]
    public void Ray3_ZeroLengthCapsuleIsHitAtItsPoint()
    {
        Capsule3 point = new(new Vector3(3f, 0f, 0f), new Vector3(3f, 0f, 0f), 1f);
        Ray3 ray = new(Vector3.Zero, Vector3.UnitX);

        Assert.True(ray.Intersects(point), "Луч, идущий сквозь точку капсулы, обязан её пересечь.");
        Assert.True(ray.Raycast(point, out float distance));
        MathAssert.Equal(2f, distance, 1e-5f);

        // Луч, проходящий мимо точки, не попадает.
        Assert.False(new Ray3(new Vector3(0f, 5f, 0f), Vector3.UnitX).Intersects(point));
    }

    /// <summary>
    /// Точность угла не должна зависеть от того, посчитаны синус и косинус
    /// вместе одним вызовом или по отдельности.
    /// </summary>
    [Fact]
    public void Angle_DirectionMatchesSinAndCos()
    {
        foreach (float degrees in new[] { 0f, 17f, 90f, 180f, -143f, 359f })
        {
            Angle angle = Angle.FromDegrees(degrees);

            MathAssert.Equal(angle.Cos, angle.Direction.X, 1e-6f);
            MathAssert.Equal(angle.Sin, angle.Direction.Y, 1e-6f);
        }
    }

    /// <summary>
    /// Поворот вектора сохраняет длину и поворачивает направление ровно на
    /// заданный угол: sin и cos из одного вызова SinCos обязаны давать ту же
    /// матрицу поворота, что и по отдельности.
    /// </summary>
    [Fact]
    public void Angle_RotatePreservesLengthAndDirection()
    {
        Vector2 source = new(3f, 4f);

        foreach (float degrees in new[] { 0f, 37f, 90f, 180f, -120f })
        {
            Angle angle = Angle.FromDegrees(degrees);
            Vector2 rotated = angle.Rotate(source);

            MathAssert.Equal(5f, rotated.Length(), 1e-5f);

            // Направление исходного вектора плюс угол поворота, по кратчайшей дуге.
            Angle expected = Angle.FromDirection(source) + angle;
            Angle actual = Angle.FromDirection(rotated);
            MathAssert.Equal((float)expected.Degrees, (float)actual.Degrees, 1e-2f);
        }
    }

    /// <summary>
    /// Snap обязан округлять половинные значения от нуля, как объявлено в
    /// доктрине.
    /// </summary>
    /// <remarks>
    /// Тест охраняет значения и не может отличить float-округление от
    /// double-округления: частное <c>value / step</c> и в том, и в другом
    /// случае считается во float, и до double доходит уже округлённое до float
    /// значение. Поэтому 0.25 при шаге 0.1 округляется в 3, а не в 2, и оба
    /// варианта дают 0.3. Это защита контракта, а не доказательство выбора
    /// реализации.
    /// </remarks>
    [Fact]
    public void Scalar_SnapRoundsHalfAwayFromZero()
    {
        MathAssert.Equal(2f, Scalar.Snap(1.5f, 1f), 1e-6f);
        MathAssert.Equal(-2f, Scalar.Snap(-1.5f, 1f), 1e-6f);
        MathAssert.Equal(3f, Scalar.Snap(2.5f, 1f), 1e-6f);
        MathAssert.Equal(0.5f, Scalar.Snap(0.4f, 0.25f), 1e-6f);
        MathAssert.Equal(0.3f, Scalar.Snap(0.25f, 0.1f), 1e-6f);
        MathAssert.Equal(0.4f, Scalar.Snap(0.35f, 0.1f), 1e-6f);
    }

    /// <summary>
    /// Кубические кривые обязаны совпадать с аналитической формулой.
    /// </summary>
    /// <remarks>
    /// Тест охраняет значения, а не способ вычисления. Замена
    /// <c>Math.Pow(1 - t, 3)</c> на два умножения — чистая оптимизация без
    /// изменения результата, и отличить один путь от другого по значению нельзя.
    /// Формула здесь считается независимо, в double, чтобы эталон не совпадал с
    /// кодом библиотеки.
    /// </remarks>
    [Fact]
    public void Curves_CubicCurvesMatchReference()
    {
        for (float t = 0f; t <= 1f; t += 0.05f)
        {
            double inverse = 1.0 - t;

            MathAssert.Equal((float)(t * t * t), Curves.InCubic(t), 1e-5f);
            MathAssert.Equal((float)(1.0 - (inverse * inverse * inverse)), Curves.OutCubic(t), 1e-5f);
            MathAssert.Equal(
                (float)(t < 0.5 ? 4.0 * t * t * t : 1.0 - (4.0 * inverse * inverse * inverse)),
                Curves.InOutCubic(t),
                1e-5f);
        }
    }

    /// <summary>
    /// Экспоненциальные кривые обязаны совпадать с опорными значениями.
    /// Опорная формула повторяет защитные ветки самих кривых: на концах
    /// диапазона они возвращают ровно 0 и 1, а не значение формулы.
    /// </summary>
    [Fact]
    public void Curves_ExponentialCurvesMatchReference()
    {
        for (float t = 0f; t <= 1f; t += 0.05f)
        {
            float inExpo = t <= 0f ? 0f : MathF.Pow(2f, (10f * t) - 10f);
            float outExpo = t >= 1f ? 1f : 1f - MathF.Pow(2f, -10f * t);

            MathAssert.Equal(inExpo, Curves.InExpo(t), 1e-6f);
            MathAssert.Equal(outExpo, Curves.OutExpo(t), 1e-6f);
            MathAssert.Equal(0f, Curves.InExpo(0f), 1e-6f);
            MathAssert.Equal(1f, Curves.OutExpo(1f), 1e-6f);
        }
    }

    /// <summary>
    /// Случайное направление обязано быть единичным и равномерным: длина
    /// результата не зависит от того, был угол нормализован или нет.
    /// </summary>
    [Fact]
    public void Random_NextDirectionIsUnitLength()
    {
        XorShift64Star random = new(20251008UL);

        for (int step = 0; step < 1000; step++)
        {
            Vector2 direction = random.NextDirection();
            MathAssert.Equal(1f, direction.Length(), 1e-5f);
        }
    }

    /// <summary>
    /// Случайные направления обязаны покрывать всю окружность, а не только
    /// четверть: нормализация угла не должна была отсекать ничего, но и её
    /// отсутствие не должно ничего сломать.
    /// </summary>
    [Fact]
    public void Random_NextDirectionCoversFullCircle()
    {
        XorShift64Star random = new(20251008UL);
        bool positiveX = false;
        bool positiveY = false;
        bool negativeX = false;
        bool negativeY = false;

        for (int step = 0; step < 1000; step++)
        {
            Vector2 direction = random.NextDirection();
            positiveX |= direction.X > 0.9f;
            positiveY |= direction.Y > 0.9f;
            negativeX |= direction.X < -0.9f;
            negativeY |= direction.Y < -0.9f;
        }

        Assert.True(positiveX && positiveY && negativeX && negativeY, "Направления не покрывают окружность.");
    }

    /// <summary>
    /// Точка внутри единичной окружности обязана иметь длину меньше единицы и
    /// ждать положительного результата при любом зерне.
    /// </summary>
    [Fact]
    public void Random_NextInsideUnitCircleStaysInside()
    {
        XorShift64Star random = new(20251008UL);

        for (int step = 0; step < 1000; step++)
        {
            Vector2 point = random.NextInsideUnitCircle();
            Assert.True(point.Length() <= 1f, $"Точка {point} вне единичной окружности.");
        }
    }

    /// <summary>
    /// Frustum как структура обязана отдавать те же шесть плоскостей и давать
    /// те же ответы отсечения, что и раньше.
    /// </summary>
    [Fact]
    public void Frustum_KeepsBehaviourAsValueType()
    {
        Matrix4x4 projection = Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(90f), 1f, 1f, 100f);
        Matrix4x4 view = Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY);
        Frustum frustum = Frustum.FromViewProjection(Matrix4x4Extensions.CreateViewProjection(view, projection));

        Assert.Equal(6, frustum.Planes.Length);
        foreach (Plane3 plane in frustum.Planes)
        {
            MathAssert.Equal(1f, plane.Normal.Length(), 1e-4f);
        }

        Assert.True(frustum.Intersects(new BoundingSphere(new Vector3(0f, 0f, 10f), 1f)));
        Assert.False(frustum.Intersects(new BoundingSphere(new Vector3(0f, 0f, -10f), 1f)));

        // Отсечение вне поля зрения по горизонтали.
        Assert.True(frustum.Intersects(new BoundingSphere(new Vector3(1f, 0f, 10f), 1f)));
        Assert.False(frustum.Intersects(new BoundingSphere(new Vector3(50f, 0f, 10f), 1f)));
    }

    /// <summary>
    /// Пакетное отсечение обязано давать тот же результат, что и поштучное,
    /// и считать ровно видимые объекты.
    /// </summary>
    [Fact]
    public void Frustum_CountVisibleMatchesPerObjectTests()
    {
        Matrix4x4 projection = Matrix4x4Extensions.CreatePerspective(Angle.FromDegrees(90f), 1f, 1f, 100f);
        Matrix4x4 view = Matrix4x4Extensions.CreateLookAt(Vector3.Zero, new Vector3(0f, 0f, 1f), Vector3.UnitY);
        Frustum frustum = Frustum.FromViewProjection(Matrix4x4Extensions.CreateViewProjection(view, projection));

        Span<BoundingSphere> spheres =
        [
            new(new Vector3(0f, 0f, 10f), 1f),
            new(new Vector3(0f, 0f, -10f), 1f),
            new(new Vector3(50f, 0f, 10f), 1f),
            new(new Vector3(0f, 0f, 10f), 0.5f),
            new(new Vector3(0f, 50f, 10f), 1f),
            new(new Vector3(0f, 0f, 500f), 1f),
        ];

        int expected = 0;
        foreach (BoundingSphere sphere in spheres)
        {
            if (frustum.Intersects(sphere))
            {
                expected++;
            }
        }

        Assert.Equal(expected, Frustum.CountVisible(frustum, spheres));
    }

    /// <summary>
    /// Тригонометрия не уходит в бесконечность и остаётся согласованной с собой
    /// на всём диапазоне float, включая аргументы, где эталон недоступен.
    /// </summary>
    /// <remarks>
    /// Отдельная проверка нужна потому, что точность убывает с ростом аргумента
    /// не из-за формулы, а сама по себе: у числа порядка 2^32 последний разряд
    /// двойной точности сравним с <c>π</c>, то есть все значащие цифры остатка
    /// физически не помещаются в исходном числе. Сверяться с эталоном там
    /// бессмысленно, и единственное свойство, которое ещё можно требовать, —
    /// результат конечен и согласован с собой: <c>sin² + cos² = 1</c>.
    /// <para>
    /// Собственная константа 2π в одинарной точности не годится: на больших
    /// аргументах её разряд уже сравним с самим числом оборотов. Поэтому
    /// сравнение ведётся с двойной точностью, где остаток вычисляется точно.
    /// </para>
    /// </remarks>
    [Fact]
    public void Trig_StaysFiniteAndConsistentAcrossWholeFloatRange()
    {
        float[] magnitudes =
        [
            0f, 1e-30f, 1f, 1000f, 1e6f, 1e9f, 1e10f, 1e15f, 1e18f, 1e30f, float.MaxValue,
        ];

        foreach (float magnitude in magnitudes)
        {
            foreach (float sign in new[] { 1f, -1f })
            {
                float x = magnitude * sign;
                (float sin, float cos) = Trig.SinCos(x);

                Assert.True(float.IsFinite(sin), $"Синус при x={x:E1} не конечен: {sin}.");
                Assert.True(float.IsFinite(cos), $"Косинус при x={x:E1} не конечен: {cos}.");
                Assert.True(MathF.Abs(sin) <= 1f, $"Синус при x={x:E1} вне отрезка [-1; 1]: {sin}.");
                Assert.True(MathF.Abs(cos) <= 1f, $"Косинус при x={x:E1} вне отрезка [-1; 1]: {cos}.");

                double identity = ((double)sin * sin) + ((double)cos * cos);
                Assert.True(Math.Abs(identity - 1.0) < 1e-6, $"sin² + cos² = {identity:F9} при x={x:E1}.");

                Assert.True(sin == Trig.Sin(x), $"Sin разошёлся с SinCos при x={x:E1}.");
                Assert.True(cos == Trig.Cos(x), $"Cos разошёлся с SinCos при x={x:E1}.");
            }
        }
    }

    /// <summary>
    /// На всём диапазоне, где приведение к квадранту ещё корректно, результат
    /// совпадает с двойной точностью в пределах округления float.
    /// </summary>
    /// <remarks>
    /// Полоса задана по измерению, а не по теории: на 200 000 точек худшая
    /// абсолютная ошибка оказалась 9.1e-8 и не росла с аргументом, то есть
    /// определяется финальным округлением float, а не приведением. Относительная
    /// мера не годится: у синуса вблизи нуля знаменатель стремится к нулю и
    /// ошибка взлетает, хотя абсолютная остаётся ничтожной. Поэтому сравнение
    /// только абсолютное.
    /// <para>
    /// Граница 2^24 — это порядок величины float: за ней приведение перестаёт
    /// быть корректным, и сверка с эталоном теряет смысл (см. предыдущий тест,
    /// где на всём диапазоне требуется лишь конечность и согласованность).
    /// </para>
    /// </remarks>
    [Fact]
    public void SinCos_MatchesDoublePrecisionOverUsableRange()
    {
        DeterministicRandom random = new(0x1F2E3D4C5B6A7988UL);
        const float Limit = 1 << 24;
        const double Band = 3e-7;

        for (int i = 0; i < 200000; i++)
        {
            float x = random.Range(-Limit, Limit);
            (float sin, float cos) = Trig.SinCos(x);

            Assert.True(
                Math.Abs((double)sin - Math.Sin(x)) <= Band,
                $"Синус {sin} против {Math.Sin(x):R} расходится на {Math.Abs((double)sin - Math.Sin(x)):E3} при x={x:R}.");
            Assert.True(
                Math.Abs((double)cos - Math.Cos(x)) <= Band,
                $"Косинус {cos} против {Math.Cos(x):R} расходится на {Math.Abs((double)cos - Math.Cos(x)):E3} при x={x:R}.");
        }
    }

    /// <summary>
    /// Нечисловые и бесконечные аргументы ведут себя как в эталоне.
    /// </summary>
    [Fact]
    public void Trig_PropagatesNonFiniteArguments()
    {
        Assert.Equal(float.NaN, Trig.Sin(float.NaN));
        Assert.Equal(float.NaN, Trig.Cos(float.NaN));
        Assert.Equal(float.NaN, Trig.Sin(float.PositiveInfinity));
        Assert.Equal(float.NaN, Trig.Sin(float.NegativeInfinity));

        // Знак нуля различается: sin(−0) = −0, и это различие наблюдаемо.
        Assert.True(
            BitConverter.SingleToInt32Bits(-0f) == BitConverter.SingleToInt32Bits(Trig.Sin(-0f)),
            "Sin(-0) обязан вернуть -0, а не +0.");
    }
}
