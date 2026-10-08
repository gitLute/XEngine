using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Капсула в трёх измерениях: отрезок, утолщённый на радиус. Основная форма
/// коллайдера персонажей (6.3, 12.4).
/// </summary>
/// <remarks>
/// Отдельного типа <c>Segment3</c> нет: осевая линия — это два конца капсулы,
/// а расстояние до прямой нужно только внутри самой капсулы. Вводить
/// публичный тип ради одного внутреннего вычисления значило бы разойтись с
/// перечнем типов документа.
/// </remarks>
public readonly struct Capsule3 : IEquatable<Capsule3>
{
    /// <summary>
    /// Создаёт капсулу по концам осевой линии и радиусу.
    /// </summary>
    /// <param name="pointA">Начало осевой линии.</param>
    /// <param name="pointB">Конец осевой линии.</param>
    /// <param name="radius">Радиус; отрицательные значения не допускаются.</param>
    /// <exception cref="ArgumentOutOfRangeException">Радиус отрицательный.</exception>
    public Capsule3(Vector3 pointA, Vector3 pointB, float radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);

        PointA = pointA;
        PointB = pointB;
        Radius = radius;
    }

    /// <summary>
    /// Создаёт вертикальную капсулу заданной высоты с центром в заданной точке.
    /// </summary>
    /// <param name="center">Центр капсулы.</param>
    /// <param name="height">Полная высота вместе с закруглениями.</param>
    /// <param name="radius">Радиус; отрицательные значения не допускаются.</param>
    /// <returns>Капсула вдоль вертикальной оси.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Радиус отрицательный или превышает половину высоты.</exception>
    public static Capsule3 FromHeight(Vector3 center, float height, float radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        if (height < radius * 2f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height),
                height,
                "Высота капсулы не может быть меньше двух радиусов.");
        }

        float half = height * 0.5f - radius;
        return new Capsule3(center - new Vector3(0f, half, 0f), center + new Vector3(0f, half, 0f), radius);
    }

    /// <summary>
    /// Начало осевой линии.
    /// </summary>
    public Vector3 PointA { get; }

    /// <summary>
    /// Конец осевой линии.
    /// </summary>
    public Vector3 PointB { get; }

    /// <summary>
    /// Вектор осевой линии от начала к концу.
    /// </summary>
    public Vector3 Delta => PointB - PointA;

    /// <summary>
    /// Радиус капсулы.
    /// </summary>
    public float Radius { get; }

    /// <summary>
    /// Центр капсулы.
    /// </summary>
    public Vector3 Center => (PointA + PointB) * 0.5f;

    /// <summary>
    /// Параллелепипед, описанный вокруг капсулы.
    /// </summary>
    public Aabb3 Bounds
    {
        get
        {
            Vector3 half = new(Radius, Radius, Radius);
            return new Aabb3(Vector3.Min(PointA, PointB) - half, Vector3.Max(PointA, PointB) + half);
        }
    }

    /// <summary>
    /// Проверяет, находится ли точка внутри капсулы.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <returns><c>true</c>, если точка внутри.</returns>
    /// <remarks>
    /// Сравниваются квадраты расстояний, а не расстояния: корень в этой
    /// проверке не нужен, а она стоит в горячем пути попадания луча в капсулу.
    /// </remarks>
    public bool Contains(Vector3 point)
    {
        Vector3 delta = Delta;
        float lengthSquared = delta.LengthSquared();
        Vector3 closest = lengthSquared <= Scalar.Epsilon * Scalar.Epsilon
            ? PointA
            : PointA + (delta * Scalar.Clamp(Vector3.Dot(point - PointA, delta) / lengthSquared, 0f, 1f));

        return (point - closest).LengthSquared() <= Radius * Radius;
    }

    /// <summary>
    /// Возвращает ближайшую к заданной точку осевой линии.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Точка на осевой линии.</returns>
    public Vector3 ClosestPointOnAxis(Vector3 point)
    {
        Vector3 delta = Delta;
        float lengthSquared = delta.LengthSquared();
        if (lengthSquared <= Scalar.Epsilon * Scalar.Epsilon)
        {
            return PointA;
        }

        float t = Scalar.Clamp(Vector3.Dot(point - PointA, delta) / lengthSquared, 0f, 1f);
        return PointA + delta * t;
    }

    /// <summary>
    /// Возвращает ближайшую к заданной точку поверхности капсулы.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Точка на поверхности капсулы.</returns>
    public Vector3 ClosestPointTo(Vector3 point)
    {
        Vector3 closest = ClosestPointOnAxis(point);
        Vector3 offset = point - closest;
        return offset.LengthSquared() <= Scalar.Epsilon * Scalar.Epsilon
            ? closest + new Vector3(Radius, 0f, 0f)
            : closest + Vector3.Normalize(offset) * Radius;
    }

    /// <summary>
    /// Возвращает расстояние от точки до осевой линии.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Расстояние до отрезка, а не до прямой.</returns>
    public float DistanceToAxis(Vector3 point) => Vector3.Distance(ClosestPointOnAxis(point), point);

    /// <summary>
    /// Проверяет, пересекаются ли капсулы. Используется при грубой проверке
    /// пар форм до точного расчёта контактов.
    /// </summary>
    /// <param name="other">Другая капсула.</param>
    /// <returns><c>true</c>, если расстояние между осевыми линиями не больше суммы радиусов.</returns>
    public bool Intersects(in Capsule3 other)
    {
        ClosestPointsBetweenSegments(PointA, PointB, other.PointA, other.PointB, out Vector3 first, out Vector3 second);
        float reach = Radius + other.Radius;
        return Vector3.Distance(first, second) <= reach;
    }

    /// <summary>
    /// Ищет ближайшие точки двух отрезков по стандартной формуле для
    /// параметрических прямых; вырожденные случаи отрезка обрабатываются
    /// отдельно.
    /// </summary>
    /// <param name="startA">Начало первого отрезка.</param>
    /// <param name="endA">Конец первого отрезка.</param>
    /// <param name="startB">Начало второго отрезка.</param>
    /// <param name="endB">Конец второго отрезка.</param>
    /// <param name="closestA">Ближайшая точка первого отрезка.</param>
    /// <param name="closestB">Ближайшая точка второго отрезка.</param>
    private static void ClosestPointsBetweenSegments(
        Vector3 startA,
        Vector3 endA,
        Vector3 startB,
        Vector3 endB,
        out Vector3 closestA,
        out Vector3 closestB)
    {
        // Два порога вместо одного: длина отрезка в квадрате измеряется в
        // метрах², а denominator — произведение квадратов длин, то есть
        // площадь в метрах⁴. Один порог для величин разной размерности
        // означал бы, что параллельные отрезки произвольной длины
        // обрабатываются вырожденными.
        const float DegenerateLengthSquared = 1e-12f;
        const float DegenerateDenominator = 1e-18f;

        Vector3 first = endA - startA;
        Vector3 second = endB - startB;
        Vector3 offset = startA - startB;

        float lengthSquaredFirst = Vector3.Dot(first, first);
        float lengthSquaredSecond = Vector3.Dot(second, second);
        float alongSecond = Vector3.Dot(second, offset);

        if (lengthSquaredFirst <= DegenerateLengthSquared && lengthSquaredSecond <= DegenerateLengthSquared)
        {
            closestA = startA;
            closestB = startB;
            return;
        }

        if (lengthSquaredFirst <= DegenerateLengthSquared)
        {
            float parameter = Scalar.Clamp(alongSecond / lengthSquaredSecond, 0f, 1f);
            closestA = startA;
            closestB = startB + second * parameter;
            return;
        }

        float alongFirst = Vector3.Dot(first, offset);
        if (lengthSquaredSecond <= DegenerateLengthSquared)
        {
            float parameter = Scalar.Clamp(-alongFirst / lengthSquaredFirst, 0f, 1f);
            closestA = startA + first * parameter;
            closestB = startB;
            return;
        }

        float mixed = Vector3.Dot(first, second);
        float denominator = lengthSquaredFirst * lengthSquaredSecond - mixed * mixed;
        float parameterFirst = MathF.Abs(denominator) > DegenerateDenominator
            ? Scalar.Clamp((mixed * alongSecond - lengthSquaredSecond * alongFirst) / denominator, 0f, 1f)
            : 0f;

        float parameterSecond = (mixed * parameterFirst + alongSecond) / lengthSquaredSecond;
        if (parameterSecond < 0f)
        {
            parameterSecond = 0f;
            parameterFirst = Scalar.Clamp(-alongFirst / lengthSquaredFirst, 0f, 1f);
        }
        else if (parameterSecond > 1f)
        {
            parameterSecond = 1f;
            parameterFirst = Scalar.Clamp((mixed - alongFirst) / lengthSquaredFirst, 0f, 1f);
        }

        closestA = startA + first * parameterFirst;
        closestB = startB + second * parameterSecond;
    }

    /// <inheritdoc/>
    public bool Equals(Capsule3 other)
        => PointA.Equals(other.PointA) && PointB.Equals(other.PointB) && Radius.Equals(other.Radius);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Capsule3 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(PointA, PointB, Radius);

    /// <summary>
    /// Сравнивает капсулы на равенство.
    /// </summary>
    /// <param name="left">Первая капсула.</param>
    /// <param name="right">Вторая капсула.</param>
    /// <returns><c>true</c>, если капсулы равны.</returns>
    public static bool operator ==(Capsule3 left, Capsule3 right) => left.Equals(right);

    /// <summary>
    /// Сравнивает капсулы на неравенство.
    /// </summary>
    /// <param name="left">Первая капсула.</param>
    /// <param name="right">Вторая капсула.</param>
    /// <returns><c>true</c>, если капсулы различаются.</returns>
    public static bool operator !=(Capsule3 left, Capsule3 right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"Capsule3({PointA} -> {PointB}, r={Radius:F3})";
}