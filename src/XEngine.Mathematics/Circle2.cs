using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Круг в двумерном пространстве.
/// </summary>
public readonly struct Circle2 : IEquatable<Circle2>
{
    /// <summary>
    /// Создаёт круг.
    /// </summary>
    /// <param name="center">Центр круга.</param>
    /// <param name="radius">Радиус. Отрицательные значения не допускаются.</param>
    /// <exception cref="ArgumentOutOfRangeException">Радиус отрицательный.</exception>
    public Circle2(Vector2 center, float radius)
    {
        if (radius < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Радиус не может быть отрицательным.");
        }

        Center = center;
        Radius = radius;
    }

    /// <summary>
    /// Центр круга.
    /// </summary>
    public Vector2 Center { get; }

    /// <summary>
    /// Радиус круга.
    /// </summary>
    public float Radius { get; }

    /// <summary>
    /// Диаметр круга.
    /// </summary>
    public float Diameter => Radius * 2f;

    /// <summary>
    /// AABB, описанный вокруг круга.
    /// </summary>
    public Aabb2 Bounds => Aabb2.FromCenterAndHalfSize(Center, new Vector2(Radius, Radius));

    /// <summary>
    /// Проверяет, находится ли точка внутри круга.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <returns><c>true</c>, если точка внутри или на границе.</returns>
    public bool Contains(Vector2 point) => (point - Center).LengthSquared() <= Radius * Radius;

    /// <summary>
    /// Проверяет пересечение двух кругов.
    /// </summary>
    /// <param name="other">Другой круг.</param>
    /// <returns><c>true</c>, если круги пересекаются.</returns>
    public bool Intersects(Circle2 other)
        => (other.Center - Center).LengthSquared() <= (Radius + other.Radius) * (Radius + other.Radius);

    /// <summary>
    /// Возвращает ближайшую к заданной точку границы круга.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Точка на границе круга.</returns>
    public Vector2 ClosestPointOnBoundary(Vector2 point)
    {
        Vector2 delta = point - Center;
        return delta.LengthSquared() <= Scalar.Epsilon * Scalar.Epsilon
            ? Center + new Vector2(Radius, 0f)
            : Center + Vector2.Normalize(delta) * Radius;
    }

    /// <summary>
    /// Перемещает круг.
    /// </summary>
    /// <param name="offset">Вектор смещения.</param>
    /// <returns>Смещённый круг.</returns>
    public Circle2 Translated(Vector2 offset) => new(Center + offset, Radius);

    /// <inheritdoc/>
    public bool Equals(Circle2 other) => Center.Equals(other.Center) && Radius.Equals(other.Radius);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Circle2 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Center, Radius);

    /// <summary>
    /// Сравнивает круги на равенство.
    /// </summary>
    /// <param name="left">Первый круг.</param>
    /// <param name="right">Второй круг.</param>
    /// <returns><c>true</c>, если круги равны.</returns>
    public static bool operator ==(Circle2 left, Circle2 right) => left.Equals(right);

    /// <summary>
    /// Сравнивает круги на неравенство.
    /// </summary>
    /// <param name="left">Первый круг.</param>
    /// <param name="right">Второй круг.</param>
    /// <returns><c>true</c>, если круги различаются.</returns>
    public static bool operator !=(Circle2 left, Circle2 right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"Circle2({Center}, r={Radius:F2})";
}
