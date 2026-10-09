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
    /// <param name="radius">Радиус. Отрицательные и нечисловые значения не допускаются.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Радиус отрицательный или равен <see cref="float.NaN"/>.
    /// </exception>
    /// <remarks>
    /// Проверяется именно <see cref="float.NaN"/>, а не «радиус не конечен»:
    /// сравнение <c>NaN &lt; 0</c> ложно, и без проверки создавалась капсула с
    /// нечисловым радиусом, у которой <see cref="Bounds"/> и
    /// <see cref="Contains"/> молча давали мусор. Бесконечный радиус при этом
    /// не бессмыслен — капсула покрывает всю плоскость, — и отвергать его
    /// незачем.
    /// </remarks>
    public Capsule2(Segment2 segment, float radius)
    {
        if (radius < 0f || float.IsNaN(radius))
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Радиус должен быть неотрицательным и не быть NaN.");
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
    /// <remarks>
    /// Пустой <see cref="Aabb2"/> обрабатывается отдельно. У пустого бокса
    /// <see cref="Aabb2.Size"/> равен <c>(−∞, −∞)</c>, отсюда
    /// <c>radius = −∞</c>, и конструктор справедливо его отвергал — но
    /// собственное пустое значение библиотеки роняло соседнюю фабрику на
    /// законном вызове. Поведение согласовано с
    /// <c>BoundingSphere.FromAabb(Aabb3.Empty)</c>: вырожденная капсула нулевого
    /// радиуса в центре пустого бокса.
    /// <para>
    /// Отдельно проверено, что вырожденный бокс нулевой площади — не пустой:
    /// у него есть размер по одной оси, и он обязан обрабатываться как
    /// обычный.
    /// </para>
    /// </remarks>
    public static Capsule2 FromBounds(Aabb2 bounds)
    {
        if (bounds.IsEmpty)
        {
            return new Capsule2(new Segment2(Vector2.Zero, Vector2.Zero), 0f);
        }

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
    /// <remarks>
    /// Сравниваются квадраты расстояний, а не расстояния: корень здесь не
    /// нужен, а проверка попадания точки в капсулу идёт на каждый запрос.
    /// <para>
    /// Порога вырожденности у самого метода нет: он спрашивает о расстоянии до
    /// ближайшей точки <see cref="Segment2"/>. Ошибка на короткой оси приходила
    /// именно оттуда — при оси короче 1e-6 ближайшей точкой подставлялся конец
    /// A вместо проекции, и вердикт о попадании не зависел от масштаба. После
    /// правки <see cref="Segment2.ClosestPointTo"/> зависимости не осталось.
    /// </para>
    /// </remarks>
    public bool Contains(Vector2 point)
    {
        Vector2 closest = Segment2.ClosestPointTo(point);
        return (point - closest).LengthSquared() <= Radius * Radius;
    }

    /// <summary>
    /// Возвращает ближайшую к заданной точку точку границы капсулы.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Точка внутри капсулы у самой границы.</returns>
    /// <remarks>
    /// Точка сдвигается внутрь на четыре последних разряда радиуса, иначе
    /// округление умножения выводит её наружу и <see cref="Contains"/>
    /// отвергает результат собственного метода библиотеки. Сдвиг не виден ни в
    /// одном применении.
    /// </remarks>
    public Vector2 ClosestPointOnBoundary(Vector2 point)
    {
        Vector2 closest = Segment2.ClosestPointTo(point);
        Vector2 delta = point - closest;

        // Порог заменён на точный ноль, как в Circle2 и Segment2. Прежняя
        // проверка delta.LengthSquared() <= Epsilon² отсекала любое смещение
        // меньше 1e-6 и возвращала closest + (Radius, 0): на запросе в 4e-7 от
        // оси ошибка равнялась 141.42 % радиуса, потому что возвращённая точка
        // оказывалась по другую сторону оси. Точный ноль — это запрос ровно на
        // оси: направления нет, подходит любая точка границы.
        return delta == Vector2.Zero
            ? closest + new Vector2(Radius, 0f)
            : closest + (delta.SafeNormalize() * SurfaceRadius.For(Radius, closest));
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
