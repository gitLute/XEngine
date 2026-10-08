using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Ограничивающий параллелепипед в трёх измерениях: оси параллельны мировым.
/// </summary>
/// <remarks>
/// Основной объём объёмного отсечения. После поворота параллелепипед перестаёт
/// быть параллелепипедом, поэтому <see cref="Transform"/> строит объемлющий
/// параллелепипед: он больше исходного, но зато гарантированно содержит его и
/// не может отсечь видимый объект.
/// </remarks>
public readonly struct Aabb3 : IEquatable<Aabb3>
{
    /// <summary>
    /// Создаёт параллелепипед по границам.
    /// </summary>
    /// <param name="min">Минимальный угол по всем осям.</param>
    /// <param name="max">Максимальный угол по всем осям.</param>
    /// <exception cref="ArgumentException">Границы переставлены по любой оси.</exception>
    public Aabb3(Vector3 min, Vector3 max)
    {
        if (min.X > max.X || min.Y > max.Y || min.Z > max.Z)
        {
            throw new ArgumentException("Минимальные границы должны быть не больше максимальных.", nameof(min));
        }

        Min = min;
        Max = max;
    }

    /// <summary>
    /// Создаёт вырожденный параллелепипед в точке: используется как начальное
    /// значение при расширении по точкам.
    /// </summary>
    /// <param name="value">Точка вырождения.</param>
    public Aabb3(Vector3 value)
    {
        Min = value;
        Max = value;
    }

    private Aabb3(Vector3 min, Vector3 max, EmptyMarker marker)
    {
        _ = marker;
        Min = min;
        Max = max;
    }

    /// <summary>
    /// Метка пустого параллелепипеда: нужна, чтобы <see cref="Empty"/> не
    /// проходил проверку границ, где минимум больше максимума намеренно.
    /// </summary>
    private readonly struct EmptyMarker;

    /// <summary>
    /// Пустой параллелепипед: не содержит ни одной точки, объединение с ним
    /// даёт второй операнд.
    /// </summary>
    public static Aabb3 Empty => new(
        new Vector3(float.PositiveInfinity),
        new Vector3(float.NegativeInfinity),
        default(EmptyMarker));

    /// <summary>
    /// Создаёт параллелепипед из центра и половин размера.
    /// </summary>
    /// <param name="center">Центр.</param>
    /// <param name="halfSize">Половина размера по осям.</param>
    /// <returns>Параллелепипед.</returns>
    public static Aabb3 FromCenterAndHalfSize(Vector3 center, Vector3 halfSize)
        => new(center - halfSize, center + halfSize);

    /// <summary>
    /// Создаёт параллелепипед из центра и полного размера.
    /// </summary>
    /// <param name="center">Центр.</param>
    /// <param name="size">Размер по осям.</param>
    /// <returns>Параллелепипед.</returns>
    public static Aabb3 FromCenterAndSize(Vector3 center, Vector3 size)
        => FromCenterAndHalfSize(center, size * 0.5f);

    /// <summary>
    /// Создаёт параллелепипед, покрывающий все точки.
    /// </summary>
    /// <param name="points">Точки, которые нужно покрыть.</param>
    /// <returns>Параллелепипед.</returns>
    /// <exception cref="ArgumentException">Список точек пуст.</exception>
    public static Aabb3 FromPoints(ReadOnlySpan<Vector3> points)
    {
        if (points.Length == 0)
        {
            throw new ArgumentException("Список точек пуст: параллелепипед не построить.", nameof(points));
        }

        Vector3 min = points[0];
        Vector3 max = points[0];
        for (int index = 1; index < points.Length; index++)
        {
            Vector3 point = points[index];
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        return new Aabb3(min, max);
    }

    /// <summary>
    /// Минимальный угол по всем осям.
    /// </summary>
    public Vector3 Min { get; }

    /// <summary>
    /// Максимальный угол по всем осям.
    /// </summary>
    public Vector3 Max { get; }

    /// <summary>
    /// Размер по осям.
    /// </summary>
    public Vector3 Size => Max - Min;

    /// <summary>
    /// Половина размера по осям.
    /// </summary>
    public Vector3 HalfSize => (Max - Min) * 0.5f;

    /// <summary>
    /// Центр параллелепипеда.
    /// </summary>
    /// <remarks>
    /// У пустого параллелепипеда границы переставлены, поэтому сумма Min + Max
    /// даёт NaN. Для него возвращается нулевой центр: NaN в центре
    /// параллелепипеда не даёт вызывающему никакого решения.
    /// </remarks>
    public Vector3 Center => IsEmpty ? Vector3.Zero : (Min + Max) * 0.5f;

    /// <summary>
    /// Параллелепипед вырожден хотя бы по одной оси.
    /// </summary>
    public bool IsEmpty => Max.X < Min.X || Max.Y < Min.Y || Max.Z < Min.Z;

    /// <summary>
    /// Проверяет принадлежность точки, включая границы.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <returns><c>true</c>, если точка внутри.</returns>
    public bool Contains(Vector3 point)
        => point.X >= Min.X && point.X <= Max.X
            && point.Y >= Min.Y && point.Y <= Max.Y
            && point.Z >= Min.Z && point.Z <= Max.Z;

    /// <summary>
    /// Проверяет пересечение с другим параллелепипедом.
    /// </summary>
    /// <param name="other">Другой параллелепипед.</param>
    /// <returns><c>true</c>, если пересечение непустое.</returns>
    public bool Intersects(in Aabb3 other)
        => other.Max.X >= Min.X && other.Min.X <= Max.X
            && other.Max.Y >= Min.Y && other.Min.Y <= Max.Y
            && other.Max.Z >= Min.Z && other.Min.Z <= Max.Z;

    /// <summary>
    /// Проверяет, что параллелепипед целиком внутри другого.
    /// </summary>
    /// <param name="other">Ограничивающий параллелепипед.</param>
    /// <returns><c>true</c>, если параллелепипед внутри.</returns>
    /// <remarks>
    /// Пустой параллелепипед не содержится ни в чём, включая другой пустой:
    /// иначе его переставленные границы удовлетворяли бы сравнениям
    /// <c>+inf &gt;= Min</c> и <c>-inf &lt;= Max</c> в любом контейнере.
    /// </remarks>
    public bool Contains(in Aabb3 other)
        => !other.IsEmpty && !IsEmpty
            && other.Min.X >= Min.X && other.Max.X <= Max.X
            && other.Min.Y >= Min.Y && other.Max.Y <= Max.Y
            && other.Min.Z >= Min.Z && other.Max.Z <= Max.Z;

    /// <summary>
    /// Возвращает объединение двух параллелепипедов.
    /// </summary>
    /// <param name="other">Другой параллелепипед.</param>
    /// <returns>Параллелепипед, содержащий оба.</returns>
    public Aabb3 Union(in Aabb3 other)
    {
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        return new Aabb3(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));
    }

    /// <summary>
    /// Расширяет параллелепипед на заданное расстояние по всем осям.
    /// </summary>
    /// <param name="amount">Величина расширения по осям.</param>
    /// <returns>Расширенный параллелепипед.</returns>
    /// <remarks>
    /// Отрицательный отступ, превышающий половину размера, схлопывает
    /// параллелепипед в точку, а не бросает исключение: так же ведёт себя
    /// <see cref="Aabb2.Expand"/>, и два типа обязаны вести себя одинаково.
    /// Пустой параллелепипед остаётся пустым: его центр равен NaN, и без этой
    /// проверки результат содержал бы NaN в обеих границах вместо пустого
    /// значения.
    /// </remarks>
    public Aabb3 Expand(Vector3 amount)
    {
        if (IsEmpty)
        {
            return this;
        }

        // При отступе, превышающем половину размера, границы пересекаются, и
        // наивный min/max перевернул бы параллелепипед вместо того, чтобы
        // схлопнуть его: бокс стал бы только больше. Схлопывание идёт в центр,
        // и Aabb2.Expand ведёт себя так же.
        Vector3 center = (Min + Max) * 0.5f;
        return new Aabb3(
            Vector3.Min(Min - amount, center),
            Vector3.Max(Max + amount, center));
    }

    /// <summary>
    /// Возвращает ближайшую к точке точку параллелепипеда.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Точка на границе или внутри.</returns>
    /// <remarks>
    /// У пустого параллелепипеда границы переставлены, и ограничение по ним
    /// дало бы +inf, от которого расстояние тоже бесконечно. Для него
    /// возвращается исходная точка.
    /// </remarks>
    public Vector3 ClosestPoint(Vector3 point)
        => IsEmpty ? point : Vector3.Clamp(point, Min, Max);

    /// <summary>
    /// Возвращает расстояние от точки до параллелепипеда.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Расстояние; ноль для точки внутри.</returns>
    public float DistanceTo(Vector3 point) => Vector3.Distance(ClosestPoint(point), point);

    /// <summary>
    /// Записывает восемь углов в переданный буфер.
    /// </summary>
    /// <param name="destination">
    /// Буфер на восемь элементов. Метод не выделяет память: углы нужны в
    /// горячем пути, а массив на каждый вызов означал бы мусор в кадре (17.3).
    /// </param>
    /// <exception cref="ArgumentException">В буфере меньше восьми элементов.</exception>
    public void GetCorners(Span<Vector3> destination)
    {
        if (destination.Length < 8)
        {
            throw new ArgumentException("Буфер должен вмещать восемь углов.", nameof(destination));
        }

        destination[0] = new Vector3(Min.X, Min.Y, Min.Z);
        destination[1] = new Vector3(Max.X, Min.Y, Min.Z);
        destination[2] = new Vector3(Min.X, Max.Y, Min.Z);
        destination[3] = new Vector3(Max.X, Max.Y, Min.Z);
        destination[4] = new Vector3(Min.X, Min.Y, Max.Z);
        destination[5] = new Vector3(Max.X, Min.Y, Max.Z);
        destination[6] = new Vector3(Min.X, Max.Y, Max.Z);
        destination[7] = new Vector3(Max.X, Max.Y, Max.Z);
    }

    /// <summary>
    /// Строит параллелепипед, содержащий исходный после преобразования.
    /// </summary>
    /// <param name="matrix">Матрица преобразования.</param>
    /// <returns>Объемлющий параллелепипед в мировых координатах.</returns>
    /// <remarks>
    /// Преобразование пустого параллелепипеда даёт пустой: его углы содержат
    /// бесконечности, а умножение бесконечности на нулевой элемент матрицы
    /// даёт NaN в границах результата.
    /// </remarks>
    public Aabb3 Transform(in Matrix4x4 matrix)
    {
        if (IsEmpty)
        {
            return Empty;
        }

        Span<Vector3> corners = stackalloc Vector3[8];
        GetCorners(corners);

        Vector3 min = Vector3.Transform(corners[0], matrix);
        Vector3 max = min;
        for (int index = 1; index < corners.Length; index++)
        {
            Vector3 corner = Vector3.Transform(corners[index], matrix);
            min = Vector3.Min(min, corner);
            max = Vector3.Max(max, corner);
        }

        return new Aabb3(min, max);
    }

    /// <inheritdoc/>
    public bool Equals(Aabb3 other) => Min.Equals(other.Min) && Max.Equals(other.Max);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Aabb3 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Min, Max);

    /// <summary>
    /// Сравнивает параллелепипеды на равенство.
    /// </summary>
    /// <param name="left">Первый параллелепипед.</param>
    /// <param name="right">Второй параллелепипед.</param>
    /// <returns><c>true</c>, если параллелепипеды равны.</returns>
    public static bool operator ==(Aabb3 left, Aabb3 right) => left.Equals(right);

    /// <summary>
    /// Сравнивает параллелепипеды на неравенство.
    /// </summary>
    /// <param name="left">Первый параллелепипед.</param>
    /// <param name="right">Второй параллелепипед.</param>
    /// <returns><c>true</c>, если параллелепипеды различаются.</returns>
    public static bool operator !=(Aabb3 left, Aabb3 right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"Aabb3({Min} .. {Max})";
}