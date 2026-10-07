using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Капсула: отрезок, утолщённый на радиус. Основная форма коллайдера персонажей.
/// </summary>
public readonly struct Capsule2 : IEquatable<Capsule2>
{
    /// <summary>
    /// Создаёт капсулу из отрезка и радиуса.
    /// </summary>
    /// <param name="segment">Осевая линия капсулы.</param>
    /// <param name="radius">Радиус. Отрицательные значения не допускаются.</param>
    /// <exception cref="ArgumentOutOfRangeException">Радиус отрицательный.</exception>
    public Capsule2(Segment2 segment, float radius)
    {
        if (radius < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Радиус не может быть отрицательным.");
        }

        Segment2 = segment;
        Radius = radius;
    }

    /// <summary>
    /// Создаёт капсулу, вписанную в прямоугольник.
    /// Узкое место определяется меньшей из сторон.
    /// </summary>
    /// <param name="bounds">Ограничивающий прямоугольник.</param>
    /// <returns>Капсула внутри прямоугольника.</returns>
    public static Capsule2 FromBounds(Aabb2 bounds)
    {
        Vector2 size = bounds.Size;
        float radius = MathF.Min(size.X, size.Y) * 0.5f;
        Vector2 halfDelta = new(
            MathF.Max(0f, (size.X * 0.5f) - radius),
            MathF.Max(0f, (size.Y * 0.5f) - radius));
        Vector2 center = bounds.Center;
        return new Capsule2(new Segment2(center - halfDelta, center + halfDelta), radius);
    }

    /// <summary>
    /// Осевая линия капсулы.
    /// </summary>
    public Segment2 Segment2 { get; }

    /// <summary>
    /// Радиус капсулы.
    /// </summary>
    public float Radius { get; }

    /// <summary>
    /// Начало осевой линии.
    /// </summary>
    public Vector2 A => Segment2.A;

    /// <summary>
    /// Конец осевой линии.
    /// </summary>
    public Vector2 B => Segment2.B;

    /// <summary>
    /// AABB, описанный вокруг капсулы.
    /// </summary>
    public Aabb2 Bounds => Segment2.Bounds.Expand(new Vector2(Radius, Radius));

    /// <summary>
    /// Проверяет, находится ли точка внутри капсулы.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <returns><c>true</c>, если точка внутри.</returns>
    public bool Contains(Vector2 point) => Segment2.DistanceTo(point) <= Radius;

    /// <summary>
    /// Возвращает ближайшую к заданной точку границы капсулы.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Точка на границе капсулы.</returns>
    public Vector2 ClosestPointOnBoundary(Vector2 point)
    {
        Vector2 closest = Segment2.ClosestPointTo(point);
        Vector2 delta = point - closest;
        return delta.LengthSquared() <= Scalar.Epsilon * Scalar.Epsilon
            ? closest + new Vector2(Radius, 0f)
            : closest + Vector2.Normalize(delta) * Radius;
    }

    /// <inheritdoc/>
    public bool Equals(Capsule2 other) => Segment2.Equals(other.Segment2) && Radius.Equals(other.Radius);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Capsule2 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Segment2, Radius);

    /// <summary>
    /// Сравнивает капсулы на равенство.
    /// </summary>
    /// <param name="left">Первая капсула.</param>
    /// <param name="right">Вторая капсула.</param>
    /// <returns><c>true</c>, если капсулы равны.</returns>
    public static bool operator ==(Capsule2 left, Capsule2 right) => left.Equals(right);

    /// <summary>
    /// Сравнивает капсулы на неравенство.
    /// </summary>
    /// <param name="left">Первая капсула.</param>
    /// <param name="right">Вторая капсула.</param>
    /// <returns><c>true</c>, если капсулы различаются.</returns>
    public static bool operator !=(Capsule2 left, Capsule2 right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"Capsule2({A} -> {B}, r={Radius:F2})";
}
