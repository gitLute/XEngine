using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Сфера, задаваемая центром и радиусом. Основной ограничивающий объём для
/// объектов с известным экранным радиусом.
/// </summary>
/// <remarks>
/// Название <c>BoundingSphere</c>, а не <c>Sphere3</c>: сфера нужна как
/// ограничивающий объём, и «граница» точнее описывает её роль (6.4).
/// </remarks>
public readonly struct BoundingSphere : IEquatable<BoundingSphere>
{
    /// <summary>
    /// Создаёт сферу.
    /// </summary>
    /// <param name="center">Центр.</param>
    /// <param name="radius">Радиус; отрицательные значения не допускаются.</param>
    /// <exception cref="ArgumentOutOfRangeException">Радиус отрицательный.</exception>
    public BoundingSphere(Vector3 center, float radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);

        Center = center;
        Radius = radius;
    }

    /// <summary>
    /// Создаёт сферу, покрывающую параллелепипед: центр совпадает с центром
    /// параллелепипеда, радиус равен половине диагонали.
    /// </summary>
    /// <param name="bounds">Покрываемый параллелепипед.</param>
    /// <returns>Описанная сфера.</returns>
    /// <remarks>
    /// У пустого параллелепипеда половина размера бесконечна, а центр равен
    /// нулю, поэтому наивный подсчёт давал бы сферу бесконечного радиуса.
    /// Пустой параллелепипед не содержит ни одной точки, и сфера нулевого
    /// радиуса в нуле — единственное честное описание: она не покрывает
    /// ничего и не вводит в заблуждение.
    /// </remarks>
    public static BoundingSphere FromAabb(in Aabb3 bounds)
        => bounds.IsEmpty ? new BoundingSphere(Vector3.Zero, 0f) : new BoundingSphere(bounds.Center, bounds.HalfSize.Length());

    /// <summary>
    /// Создаёт сферу, покрывающую набор точек.
    /// </summary>
    /// <param name="points">Точки, которые нужно покрыть.</param>
    /// <returns>Сфера с центром в средней точке границ.</returns>
    /// <exception cref="ArgumentException">Список точек пуст.</exception>
    public static BoundingSphere FromPoints(ReadOnlySpan<Vector3> points)
    {
        Aabb3 bounds = Aabb3.FromPoints(points);
        return FromAabb(bounds);
    }

    /// <summary>
    /// Центр сферы.
    /// </summary>
    public Vector3 Center { get; }

    /// <summary>
    /// Радиус сферы.
    /// </summary>
    public float Radius { get; }

    /// <summary>
    /// Диаметр сферы.
    /// </summary>
    public float Diameter => Radius * 2f;

    /// <summary>
    /// Параллелепипед, описанный вокруг сферы.
    /// </summary>
    public Aabb3 Bounds => Aabb3.FromCenterAndHalfSize(Center, new Vector3(Radius, Radius, Radius));

    /// <summary>
    /// Проверяет принадлежность точки, включая границу.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <returns><c>true</c>, если точка внутри.</returns>
    public bool Contains(Vector3 point) => (point - Center).LengthSquared() <= Radius * Radius;

    /// <summary>
    /// Проверяет пересечение с другой сферой.
    /// </summary>
    /// <param name="other">Другая сфера.</param>
    /// <returns><c>true</c>, если сферы пересекаются или касаются.</returns>
    public bool Intersects(in BoundingSphere other)
    {
        float reach = Radius + other.Radius;
        return (other.Center - Center).LengthSquared() <= reach * reach;
    }

    /// <summary>
    /// Возвращает расстояние от точки до сферы.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Расстояние; ноль для точки внутри.</returns>
    public float DistanceTo(Vector3 point) => MathF.Max(0f, Vector3.Distance(point, Center) - Radius);

    /// <summary>
    /// Возвращает ближайшую к точке точку поверхности сферы.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Точка внутри сферы у самой поверхности.</returns>
    /// <remarks>
    /// Точка сдвигается от поверхности внутрь на четыре последних разряда
    /// радиуса. Округление деления и умножения само по себе уводит результат
    /// наружу примерно в четырёх случаях из тысяч, и тогда
    /// <see cref="Contains"/> отвергал точку, полученную от собственного
    /// метода библиотеки. Сдвиг величиной в четыре разряда не виден ни в
    /// одном применении.
    /// <para>
    /// Точка в центре задана неоднозначно: подходит любое направление, и
    /// берётся ось X. Такой результат лежит на поверхности по построению.
    /// </para>
    /// </remarks>
    public Vector3 ClosestPointOnSurface(Vector3 point)
    {
        Vector3 offset = point - Center;
        float length = offset.Length();
        return length <= Scalar.Epsilon
            ? Center + new Vector3(Radius, 0f, 0f)
            : Center + offset * (SurfaceRadius.For(Radius, Center) / length);
    }


    /// <summary>
    /// Возвращает сферу, покрывающую обе.
    /// </summary>
    /// <param name="other">Другая сфера.</param>
    /// <returns>Объединение сфер.</returns>
    public BoundingSphere Union(in BoundingSphere other)
    {
        Vector3 delta = other.Center - Center;
        float distance = delta.Length();

        if (distance + other.Radius <= Radius)
        {
            return this;
        }

        if (distance + Radius <= other.Radius)
        {
            return other;
        }

        float half = (distance + other.Radius - Radius) * 0.5f;
        float shift = half / distance;
        return new BoundingSphere(Center + delta * shift, Radius + half);
    }

    /// <inheritdoc/>
    public bool Equals(BoundingSphere other) => Center.Equals(other.Center) && Radius.Equals(other.Radius);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is BoundingSphere other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Center, Radius);

    /// <summary>
    /// Сравнивает сферы на равенство.
    /// </summary>
    /// <param name="left">Первая сфера.</param>
    /// <param name="right">Вторая сфера.</param>
    /// <returns><c>true</c>, если сферы равны.</returns>
    public static bool operator ==(BoundingSphere left, BoundingSphere right) => left.Equals(right);

    /// <summary>
    /// Сравнивает сферы на неравенство.
    /// </summary>
    /// <param name="left">Первая сфера.</param>
    /// <param name="right">Вторая сфера.</param>
    /// <returns><c>true</c>, если сферы различаются.</returns>
    public static bool operator !=(BoundingSphere left, BoundingSphere right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"BoundingSphere({Center}, r={Radius:F3})";
}