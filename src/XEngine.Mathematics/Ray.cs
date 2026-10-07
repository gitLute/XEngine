using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Луч: начало и нормализованное направление.
/// </summary>
public readonly struct Ray : IEquatable<Ray>
{
    /// <summary>
    /// Создаёт луч. Направление нормализуется автоматически.
    /// </summary>
    /// <param name="origin">Начало луча.</param>
    /// <param name="direction">Направление луча. Нулевое направление даёт луч с направлением по X.</param>
    public Ray(Vector2 origin, Vector2 direction)
    {
        Origin = origin;
        Direction = direction.SafeNormalize() == Vector2.Zero ? Vector2.UnitX : direction.SafeNormalize();
    }

    /// <summary>
    /// Начало луча.
    /// </summary>
    public Vector2 Origin { get; }

    /// <summary>
    /// Нормализованное направление луча.
    /// </summary>
    public Vector2 Direction { get; }

    /// <summary>
    /// Возвращает точку на расстоянии <paramref name="distance"/> от начала луча.
    /// </summary>
    /// <param name="distance">Расстояние. Отрицательные значения идут в обратную сторону.</param>
    /// <returns>Точка на луче.</returns>
    public Vector2 GetPoint(float distance) => Origin + Direction * distance;

    /// <summary>
    /// Возвращает расстояние от начала луча до заданной точки вдоль направления.
    /// </summary>
    /// <param name="point">Заданная точка.</param>
    /// <returns>Проекция точки на направление луча.</returns>
    public float ProjectOntoDirection(Vector2 point) => Vector2.Dot(point - Origin, Direction);

    /// <summary>
    /// Проверяет пересечение с AABB.
    /// </summary>
    /// <param name="bounds">Ограничивающий прямоугольник.</param>
    /// <returns><c>true</c>, если луч пересекает прямоугольник.</returns>
    public bool Intersects(Aabb bounds)
    {
        float tMin = 0f;
        float tMax = float.MaxValue;

        for (int axis = 0; axis < 2; axis++)
        {
            float origin = axis == 0 ? Origin.X : Origin.Y;
            float direction = axis == 0 ? Direction.X : Direction.Y;
            float min = axis == 0 ? bounds.Min.X : bounds.Min.Y;
            float max = axis == 0 ? bounds.Max.X : bounds.Max.Y;

            if (MathF.Abs(direction) <= Scalar.Epsilon)
            {
                if (origin < min || origin > max)
                {
                    return false;
                }

                continue;
            }

            float inverse = 1f / direction;
            float t1 = (min - origin) * inverse;
            float t2 = (max - origin) * inverse;
            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
            }

            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);
            if (tMin > tMax)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Проверяет пересечение с кругом.
    /// </summary>
    /// <param name="circle">Круг.</param>
    /// <returns><c>true</c>, если луч пересекает круг.</returns>
    public bool Intersects(Circle circle)
    {
        Vector2 toCenter = circle.Center - Origin;
        float projection = Vector2.Dot(toCenter, Direction);
        Vector2 closest = Origin + Direction * MathF.Max(0f, projection);
        return (closest - circle.Center).LengthSquared() <= circle.Radius * circle.Radius;
    }

    /// <summary>
    /// Проверяет пересечение с отрезком.
    /// </summary>
    /// <param name="segment">Отрезок.</param>
    /// <returns><c>true</c>, если луч пересекает отрезок.</returns>
    public bool Intersects(Segment segment)
    {
        Vector2 delta = segment.Delta;
        float denominator = Vector2.Cross(Direction, delta);
        if (MathF.Abs(denominator) <= Scalar.Epsilon)
        {
            return false;
        }

        Vector2 difference = segment.A - Origin;
        float rayT = Vector2.Cross(difference, delta) / denominator;
        float segmentT = Vector2.Cross(difference, Direction) / denominator;
        return rayT >= 0f && segmentT >= 0f && segmentT <= 1f;
    }

    /// <inheritdoc/>
    public bool Equals(Ray other) => Origin.Equals(other.Origin) && Direction.Equals(other.Direction);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Ray other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Origin, Direction);

    /// <summary>
    /// Сравнивает лучи на равенство.
    /// </summary>
    /// <param name="left">Первый луч.</param>
    /// <param name="right">Второй луч.</param>
    /// <returns><c>true</c>, если лучи равны.</returns>
    public static bool operator ==(Ray left, Ray right) => left.Equals(right);

    /// <summary>
    /// Сравнивает лучи на неравенство.
    /// </summary>
    /// <param name="left">Первый луч.</param>
    /// <param name="right">Второй луч.</param>
    /// <returns><c>true</c>, если лучи различаются.</returns>
    public static bool operator !=(Ray left, Ray right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"Ray({Origin}, {Direction})";
}
