using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Осевой ограничивающий прямоугольник в двумерном пространстве.
/// Используется для отсечения объектов камерой и широкой фазы коллизий.
/// </summary>
public readonly struct Aabb2 : IEquatable<Aabb2>
{
    /// <summary>
    /// Создаёт AABB из минимальной и максимальной точек.
    /// </summary>
    /// <param name="min">Минимальная точка.</param>
    /// <param name="max">Максимальная точка.</param>
    public Aabb2(Vector2 min, Vector2 max)
    {
        Min = Vector2.Min(min, max);
        Max = Vector2.Max(min, max);
    }

    private Aabb2(Vector2 min, Vector2 max, EmptyMarker marker)
    {
        _ = marker;
        Min = min;
        Max = max;
    }

    /// <summary>
    /// Метка пустого AABB: нужна, чтобы <see cref="Empty"/> не проходил проверку
    /// размера, где минимум больше максимума намеренно.
    /// </summary>
    private readonly struct EmptyMarker;

    /// <summary>
    /// Создаёт AABB из центра и половины размера по каждой оси.
    /// </summary>
    /// <param name="center">Центр.</param>
    /// <param name="halfSize">Половина размера по каждой оси.</param>
    /// <returns>AABB.</returns>
    public static Aabb2 FromCenterAndHalfSize(Vector2 center, Vector2 halfSize)
        => new(center - halfSize, center + halfSize);

    /// <summary>
    /// Создаёт AABB из центра и полного размера.
    /// </summary>
    /// <param name="center">Центр.</param>
    /// <param name="size">Полный размер.</param>
    public static Aabb2 FromCenterAndSize(Vector2 center, Vector2 size)
        => new(center - size * 0.5f, center + size * 0.5f);

    /// <summary>
    /// Создаёт AABB из списка точек.
    /// </summary>
    /// <param name="points">Точки, которые должны быть покрыты.</param>
    /// <returns>AABB, содержащий все точки.</returns>
    /// <exception cref="ArgumentException">Список точек пуст.</exception>
    public static Aabb2 FromPoints(ReadOnlySpan<Vector2> points)
    {
        if (points.Length == 0)
        {
            throw new ArgumentException("Нужна хотя бы одна точка.", nameof(points));
        }

        Vector2 min = points[0];
        Vector2 max = points[0];
        for (int i = 1; i < points.Length; i++)
        {
            min = Vector2.Min(min, points[i]);
            max = Vector2.Max(max, points[i]);
        }

        return new Aabb2(min, max);
    }

    /// <summary>
    /// Создаёт AABB из прямоугольника.
    /// </summary>
    /// <param name="rect">Прямоугольник.</param>
    /// <returns>AABB.</returns>
    public static Aabb2 FromRect(Rect rect) => new(rect.Position, rect.Position + rect.Size);

    /// <summary>
    /// Пустой AABB, не содержащий ни одной точки.
    /// Границы намеренно переставлены: так пустой AABB отличается от
    /// вырожденного в точку и от AABB нулевой площади, а
    /// <see cref="Union"/> поглощает его, возвращая второй операнд.
    /// </summary>
    public static Aabb2 Empty => new(
        new Vector2(float.PositiveInfinity),
        new Vector2(float.NegativeInfinity),
        default(EmptyMarker));

    /// <summary>
    /// Минимальная точка.
    /// </summary>
    public Vector2 Min { get; }

    /// <summary>
    /// Максимальная точка.
    /// </summary>
    public Vector2 Max { get; }

    /// <summary>
    /// Размер по каждой оси.
    /// </summary>
    public Vector2 Size => Max - Min;

    /// <summary>
    /// Половина размера по каждой оси.
    /// </summary>
    public Vector2 HalfSize => (Max - Min) * 0.5f;

    /// <summary>
    /// Центр AABB.
    /// </summary>
    public Vector2 Center => (Min + Max) * 0.5f;

    /// <summary>
    /// Признак пустого AABB: параллелепипед переставлен хотя бы по одной оси.
    /// </summary>
    /// <remarks>
    /// Сравнение строгое, как в <see cref="Aabb3.IsEmpty"/>: AABB нулевой площади —
    /// это обычный коллайдер-линия или плоскость, а не пустое значение. При
    /// нестрогом сравнении <see cref="Union"/> выбрасывал бы такие боксы из
    /// широкой фазы, и объект, стоящий ровно на границе, переставал бы
    /// участвовать в отсечении.
    /// </remarks>
    public bool IsEmpty => Max.X < Min.X || Max.Y < Min.Y;

    /// <summary>
    /// Проверяет, находится ли точка внутри.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <returns><c>true</c>, если точка внутри.</returns>
    public bool Contains(Vector2 point)
        => point.X >= Min.X && point.X <= Max.X && point.Y >= Min.Y && point.Y <= Max.Y;

    /// <summary>
    /// Проверяет пересечение.
    /// </summary>
    /// <param name="other">Другой AABB.</param>
    /// <returns><c>true</c>, если AABB пересекаются.</returns>
    public bool Intersects(Aabb2 other)
        => other.Min.X <= Max.X && other.Max.X >= Min.X && other.Min.Y <= Max.Y && other.Max.Y >= Min.Y;

    /// <summary>
    /// Проверяет, содержится ли другой AABB внутри текущего.
    /// </summary>
    /// <param name="other">Другой AABB.</param>
    /// <returns><c>true</c>, если текущий AABB содержит другой.</returns>
    public bool Contains(Aabb2 other)
        => other.Min.X >= Min.X && other.Max.X <= Max.X && other.Min.Y >= Min.Y && other.Max.Y <= Max.Y;

    /// <summary>
    /// Возвращает объединение двух AABB.
    /// </summary>
    /// <param name="other">Другой AABB.</param>
    /// <returns>Объединение.</returns>
    public Aabb2 Union(Aabb2 other)
    {
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        return new Aabb2(Vector2.Min(Min, other.Min), Vector2.Max(Max, other.Max));
    }

    /// <summary>
    /// Расширяет AABB на отступ по обеим осям.
    /// </summary>
    /// <param name="amount">Отступ.</param>
    /// <returns>Расширённый AABB.</returns>
    /// <remarks>
    /// Отступ, превышающий половину размера по какой-либо оси, схлопывает AABB
    /// в точку на этой оси, а не переворачивает его: конструктор нормализует
    /// границы, поэтому наивный min/max из пересекшихся границ дал бы бокс
    /// больше исходного. Ведёт себя так же, как <see cref="Aabb3.Expand"/>.
    /// <para>
    /// Пустой AABB остаётся пустым. Без этой проверки его центр равен NaN,
    /// потому что границы переставлены, и результат содержал бы NaN в обеих
    /// границах вместо пустого значения.
    /// </para>
    /// </remarks>
    public Aabb2 Expand(Vector2 amount)
    {
        if (IsEmpty)
        {
            return this;
        }

        Vector2 center = (Min + Max) * 0.5f;
        return new Aabb2(Vector2.Min(Min - amount, center), Vector2.Max(Max + amount, center));
    }

    /// <summary>
    /// Возвращает ближайшую точку AABB к заданной точке.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Ближайшая точка внутри AABB.</returns>
    public Vector2 ClosestPoint(Vector2 point)
        => Vector2.Clamp(point, Min, Max);

    /// <summary>
    /// Возвращает расстояние от точки до AABB (ноль, если точка внутри).
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Расстояние до AABB.</returns>
    public float DistanceTo(Vector2 point) => Vector2.Distance(ClosestPoint(point), point);

    /// <summary>
    /// Преобразует AABB в прямоугольник.
    /// </summary>
    /// <returns>Прямоугольник с теми же границами.</returns>
    /// <remarks>
    /// Размер считается вычитанием: конструктор <see cref="Rect"/> принимает
    /// (позиция, размер), а <see cref="Max"/> — это координата угла, а не
    /// длина стороны. Передача <c>Max</c> в конструктор давала бы размер,
    /// равный координате, то есть тем больше, чем дальше прямоугольник от
    /// начала координат.
    /// </remarks>
    public Rect ToRect() => new(Min, Max - Min);

    /// <summary>
    /// Углы AABB в порядке: левый нижний, правый нижний, правый верхний, левый верхний.
    /// </summary>
    /// <param name="destination">
    /// Буфер на четыре элемента. Метод не выделяет память: углы нужны в
    /// горячем пути, а массив на каждый вызов означал бы мусор в кадре (17.3).
    /// </param>
    /// <exception cref="ArgumentException">В буфере меньше четырёх элементов.</exception>
    public void GetCorners(Span<Vector2> destination)
    {
        if (destination.Length < 4)
        {
            throw new ArgumentException("Буфер должен вмещать четыре угла.", nameof(destination));
        }

        destination[0] = new Vector2(Min.X, Min.Y);
        destination[1] = new Vector2(Max.X, Min.Y);
        destination[2] = new Vector2(Max.X, Max.Y);
        destination[3] = new Vector2(Min.X, Max.Y);
    }

    /// <inheritdoc/>
    public bool Equals(Aabb2 other) => Min.Equals(other.Min) && Max.Equals(other.Max);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Aabb2 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Min, Max);

    /// <summary>
    /// Сравнивает AABB на равенство.
    /// </summary>
    /// <param name="left">Первый AABB.</param>
    /// <param name="right">Второй AABB.</param>
    /// <returns><c>true</c>, если AABB равны.</returns>
    public static bool operator ==(Aabb2 left, Aabb2 right) => left.Equals(right);

    /// <summary>
    /// Сравнивает AABB на неравенство.
    /// </summary>
    /// <param name="left">Первый AABB.</param>
    /// <param name="right">Второй AABB.</param>
    /// <returns><c>true</c>, если AABB различаются.</returns>
    public static bool operator !=(Aabb2 left, Aabb2 right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"Aabb2({Min} .. {Max})";
}
