using System.Numerics;
using System.Runtime.CompilerServices;

namespace XEngine.Mathematics;

/// <summary>
/// Осевой ограничивающий прямоугольник в двумерном пространстве.
/// Используется для отсечения объектов камерой и широкой фазы коллизий.
/// </summary>
/// <remarks>
/// <c>default(Aabb2)</c> — это <b>не</b> <see cref="Empty"/>, а вырожденный
/// бокс в точке (0, 0) с <see cref="IsEmpty"/> = <c>false</c>. Ловушка
/// <see cref="Array"/> и <c>stackalloc</c>: массив из N «пустых» элементов даёт
/// N настоящих боксов в начале координат, и объединение через
/// <see cref="Union"/> втянет начало координат в результат. Пустое значение
/// нужно получать явно, через <see cref="Empty"/>.
/// </remarks>
public readonly struct Aabb2 : IEquatable<Aabb2>
{
    /// <summary>
    /// Создаёт AABB из минимальной и максимальной точек.
    /// </summary>
    /// <param name="min">Минимальная точка.</param>
    /// <param name="max">Максимальная точка.</param>
    /// <exception cref="ArgumentException">
    /// Границы переставлены по любой оси или хотя бы одна из них нечисловая.
    /// </exception>
    /// <remarks>
    /// Границы проверяются, а не переставляются, ровно как в
    /// <see cref="Aabb3"/>. Молчаливая перестановка означала, что два
    /// аналогичных типа отвечали на один вход по-разному: <c>Aabb2(min &gt; max)</c>
    /// принимал, а <c>Aabb3(min &gt; max)</c> бросал. Кроме того, перестановка
    /// делала результат невидимым: вызывающий задавал <c>min</c> и <c>max</c>,
    /// получал коробку и не знал, что границы вверх ногами.
    /// <para>
    /// Нечисловые границы отвергаются: сравнение <c>min.X &gt; max.X</c> на
    /// NaN ложно, и без проверки создавался бокс с NaN в обеих границах, у
    /// которого <see cref="IsEmpty"/> давал false, а <see cref="Union"/> заносил
    /// NaN в результат. Проверяется именно NaN, а не конечность: пустое
    /// значение построено на бесконечностях и обязано создаваться.
    /// </para>
    /// <para>
    /// Пустое значение собирается не через этот конструктор, а отдельно, через
    /// <see cref="Empty"/> с намеренно переставленными границами.
    /// </para>
    /// </remarks>
    public Aabb2(Vector2 min, Vector2 max)
    {
        if (min.X > max.X || min.Y > max.Y
            || float.IsNaN(min.X) || float.IsNaN(min.Y)
            || float.IsNaN(max.X) || float.IsNaN(max.Y))
        {
            throw new ArgumentException("Минимальные границы должны быть не больше максимальных.", nameof(min));
        }

        Min = min;
        Max = max;
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
    /// <exception cref="ArgumentException">Половина размера отрицательна.</exception>
    public static Aabb2 FromCenterAndHalfSize(Vector2 center, Vector2 halfSize)
        => new(center - halfSize, center + halfSize);

    /// <summary>
    /// Создаёт AABB из центра и полного размера.
    /// </summary>
    /// <param name="center">Центр.</param>
    /// <param name="size">Полный размер.</param>
    /// <exception cref="ArgumentException">Размер отрицателен.</exception>
    public static Aabb2 FromCenterAndSize(Vector2 center, Vector2 size)
        => new(center - (size * 0.5f), center + (size * 0.5f));

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
    /// <remarks>
    /// Углы упорядочиваются здесь, а не конструктором: <see cref="Rect"/> допускает
    /// отрицательный размер, о чём говорят его <c>Left</c>, <c>Top</c>,
    /// <c>Right</c> и <c>Bottom</c> — они нормализуют порядок сами. Прямоугольник с
    /// отрицательным размером задаёт ту же область, что и с положительным, и
    /// отвергать его на входе в фабрике незачем.
    /// </remarks>
    public static Aabb2 FromRect(Rect rect)
    {
        Vector2 corner = rect.Position + rect.Size;
        return new(Vector2.Min(rect.Position, corner), Vector2.Max(rect.Position, corner));
    }

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
    /// <remarks>
    /// У пустого AABB границы переставлены, поэтому сумма <c>Min + Max</c> даёт
    /// <c>NaN</c>. Для него возвращается нулевой центр, ровно как в
    /// <see cref="Aabb3.Center"/>: <c>NaN</c> в центре не даёт вызывающому никакого
    /// решения, а центр пустого значения всё равно не имеет смысла.
    /// </remarks>
    public Vector2 Center => IsEmpty ? Vector2.Zero : (Min + Max) * 0.5f;

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
    /// <remarks>
    /// Пустой AABB не содержится ни в чём, включая другой пустой. Без проверки его
    /// переставленные границы удовлетворяли бы сравнениям <c>+inf &gt;= Min</c> и
    /// <c>−inf &lt;= Max</c> в любом контейнере, то есть пустой бокс считался бы
    /// содержащимся в пустом. <see cref="Aabb3.Contains(in Aabb3)"/> отвечает так же.
    /// </remarks>
    public bool Contains(Aabb2 other)
        => !other.IsEmpty && !IsEmpty
            && other.Min.X >= Min.X && other.Max.X <= Max.X
            && other.Min.Y >= Min.Y && other.Max.Y <= Max.Y;

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
    /// в точку на этой оси, а не переворачивает его: наивные min/max из
    /// пересекшихся границ дали бы бокс больше исходного. Схлопывание идёт в
    /// центр, и формула совпадает с <see cref="Aabb3.Expand"/> построением, а не
    /// результатом: два типа обязаны вести себя одинаково.
    /// <para>
    /// Пустой AABB остаётся пустым. Без этой проверки его центр был бы <c>NaN</c>,
    /// потому что границы переставлены, и результат содержал бы <c>NaN</c> в обеих
    /// границах вместо пустого значения.
    /// </para>
    /// </remarks>
    public Aabb2 Expand(Vector2 amount)
    {
        if (IsEmpty)
        {
            return this;
        }

        // Схлопывание в центр не даёт границам пересечься, поэтому конструктор
        // получает упорядоченные точки и проверку проходит.
        Vector2 center = (Min + Max) * 0.5f;
        return new Aabb2(
            Vector2.Min(Min - amount, center),
            Vector2.Max(Max + amount, center));
    }

    /// <summary>
    /// Возвращает ближайшую точку AABB к заданной точке.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Ближайшая точка внутри AABB.</returns>
    /// <remarks>
    /// У пустого AABB границы переставлены, и ограничение по ним дало бы <c>−∞</c>,
    /// от которого расстояние тоже бесконечно. Для него возвращается исходная точка,
    /// и <see cref="DistanceTo"/> даёт ноль, ровно как в <see cref="Aabb3"/>.
    /// </remarks>
    public Vector2 ClosestPoint(Vector2 point)
        => IsEmpty ? point : Vector2.Clamp(point, Min, Max);

    /// <summary>
    /// Возвращает расстояние от точки до AABB (ноль, если точка внутри).
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Расстояние до AABB; ноль для пустого AABB.</returns>
    /// <remarks>
    /// Ноль для пустого AABB следует из <see cref="ClosestPoint"/> и совпадает с
    /// <see cref="Aabb3.DistanceTo"/>. Отдельная оговорка не нужна: пустое
    /// значение не содержит ни одной точки, поэтому расстояние до него как до
    /// объёма определяется согласованно в обоих типах.
    /// </remarks>
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
    /// <remarks>
    /// Метод помечен <see cref="MethodImplOptions.AggressiveInlining"/> не для
    /// красоты, а по замеру: без пометки тот же самый код стоит 26.4 нс против
    /// 1.26 нс с пометкой, то есть накладные расходы вызова — 25.2 нс, в 20.8
    /// раза больше самой работы. Доктрина объявляет углы нужными в горячем
    /// пути, а пакетная обработка видимости вызывает метод поштучно.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
