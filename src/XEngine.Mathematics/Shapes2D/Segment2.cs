using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Отрезок между двумя точками.
/// </summary>
public readonly struct Segment2 : IEquatable<Segment2>
{
    /// <summary>
    /// Создаёт отрезок.
    /// </summary>
    /// <param name="a">Начало отрезка.</param>
    /// <param name="b">Конец отрезка.</param>
    public Segment2(Vector2 a, Vector2 b)
    {
        A = a;
        B = b;
    }

    /// <summary>
    /// Начало отрезка.
    /// </summary>
    public Vector2 A { get; }

    /// <summary>
    /// Конец отрезка.
    /// </summary>
    public Vector2 B { get; }

    /// <summary>
    /// Вектор от начала к концу.
    /// </summary>
    public Vector2 Delta => B - A;

    /// <summary>
    /// Длина отрезка.
    /// </summary>
    public float Length => Delta.Length();

    /// <summary>
    /// Направление отрезка. Для вырожденного отрезка возвращает <see cref="Vector2.Zero"/>.
    /// </summary>
    public Vector2 Direction => Delta.SafeNormalize();

    /// <summary>
    /// Середина отрезка.
    /// </summary>
    public Vector2 Midpoint => (A + B) * 0.5f;

    /// <summary>
    /// AABB, описанный вокруг отрезка.
    /// </summary>
    public Aabb2 Bounds => Aabb2.FromPoints([A, B]);

    /// <summary>
    /// Возвращает ближайшую к заданной точку отрезка.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Ближайшая точка отрезка.</returns>
    public Vector2 ClosestPointTo(Vector2 point)
    {
        Vector2 delta = Delta;
        float lengthSquared = delta.LengthSquared();
        if (lengthSquared <= Scalar.Epsilon * Scalar.Epsilon)
        {
            return A;
        }

        float t = Interpolation.Clamp01(Vector2.Dot(point - A, delta) / lengthSquared);
        return A + delta * t;
    }

    /// <summary>
    /// Возвращает расстояние от точки до отрезка.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Расстояние до ближайшей точки отрезка.</returns>
    public float DistanceTo(Vector2 point) => (point - ClosestPointTo(point)).Length();

    /// <inheritdoc/>
    public bool Equals(Segment2 other) => A.Equals(other.A) && B.Equals(other.B);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Segment2 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(A, B);

    /// <summary>
    /// Сравнивает отрезки на равенство.
    /// </summary>
    /// <param name="left">Первый отрезок.</param>
    /// <param name="right">Вторый отрезок.</param>
    /// <returns><c>true</c>, если отрезки равны.</returns>
    public static bool operator ==(Segment2 left, Segment2 right) => left.Equals(right);

    /// <summary>
    /// Сравнивает отрезки на неравенство.
    /// </summary>
    /// <param name="left">Первый отрезок.</param>
    /// <param name="right">Второй отрезок.</param>
    /// <returns><c>true</c>, если отрезки различаются.</returns>
    public static bool operator !=(Segment2 left, Segment2 right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"Segment2({A} -> {B})";
}
