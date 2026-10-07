using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Капсула: отрезок, утолщённый на радиус. Основная форма коллайдера персонажей.
/// </summary>
public readonly struct Capsule : IEquatable<Capsule>
{
    /// <summary>
    /// Создаёт капсулу из отрезка и радиуса.
    /// </summary>
    /// <param name="segment">Осевая линия капсулы.</param>
    /// <param name="radius">Радиус. Отрицательные значения не допускаются.</param>
    /// <exception cref="ArgumentOutOfRangeException">Радиус отрицательный.</exception>
    public Capsule(Segment segment, float radius)
    {
        if (radius < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Радиус не может быть отрицательным.");
        }

        Segment = segment;
        Radius = radius;
    }

    /// <summary>
    /// Создаёт капсулу, вписанную в прямоугольник.
    /// Узкое место определяется меньшей из сторон.
    /// </summary>
    /// <param name="bounds">Ограничивающий прямоугольник.</param>
    /// <returns>Капсула внутри прямоугольника.</returns>
    public static Capsule FromBounds(Aabb bounds)
    {
        Vector2 size = bounds.Size;
        float radius = MathF.Min(size.X, size.Y) * 0.5f;
        Vector2 halfDelta = new(
            MathF.Max(0f, (size.X * 0.5f) - radius),
            MathF.Max(0f, (size.Y * 0.5f) - radius));
        Vector2 center = bounds.Center;
        return new Capsule(new Segment(center - halfDelta, center + halfDelta), radius);
    }

    /// <summary>
    /// Осевая линия капсулы.
    /// </summary>
    public Segment Segment { get; }

    /// <summary>
    /// Радиус капсулы.
    /// </summary>
    public float Radius { get; }

    /// <summary>
    /// Начало осевой линии.
    /// </summary>
    public Vector2 A => Segment.A;

    /// <summary>
    /// Конец осевой линии.
    /// </summary>
    public Vector2 B => Segment.B;

    /// <summary>
    /// AABB, описанный вокруг капсулы.
    /// </summary>
    public Aabb Bounds => Segment.Bounds.Expand(new Vector2(Radius, Radius));

    /// <summary>
    /// Проверяет, находится ли точка внутри капсулы.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <returns><c>true</c>, если точка внутри.</returns>
    public bool Contains(Vector2 point) => Segment.DistanceTo(point) <= Radius;

    /// <summary>
    /// Возвращает ближайшую к заданной точку границы капсулы.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Точка на границе капсулы.</returns>
    public Vector2 ClosestPointOnBoundary(Vector2 point)
    {
        Vector2 closest = Segment.ClosestPointTo(point);
        Vector2 delta = point - closest;
        return delta.LengthSquared() <= Scalar.Epsilon * Scalar.Epsilon
            ? closest + new Vector2(Radius, 0f)
            : closest + Vector2.Normalize(delta) * Radius;
    }

    /// <inheritdoc/>
    public bool Equals(Capsule other) => Segment.Equals(other.Segment) && Radius.Equals(other.Radius);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Capsule other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Segment, Radius);

    /// <summary>
    /// Сравнивает капсулы на равенство.
    /// </summary>
    /// <param name="left">Первая капсула.</param>
    /// <param name="right">Вторая капсула.</param>
    /// <returns><c>true</c>, если капсулы равны.</returns>
    public static bool operator ==(Capsule left, Capsule right) => left.Equals(right);

    /// <summary>
    /// Сравнивает капсулы на неравенство.
    /// </summary>
    /// <param name="left">Первая капсула.</param>
    /// <param name="right">Вторая капсула.</param>
    /// <returns><c>true</c>, если капсулы различаются.</returns>
    public static bool operator !=(Capsule left, Capsule right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"Capsule({A} -> {B}, r={Radius:F2})";
}
