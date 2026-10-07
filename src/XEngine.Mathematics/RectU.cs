namespace XEngine.Mathematics;

/// <summary>
/// Прямоугольник в целых пикселях с включёнными границами: регион атласа (10.6).
/// </summary>
/// <remarks>
/// Границы включительные: прямоугольник <c>0,0,32,32</c> покрывает 32 пикселя
/// по каждой оси. Размер задаётся числом пикселей, а не координатой правой
/// нижней границы, иначе регион на один пиксель больше или меньше, чем
/// ожидала разметка атласа.
/// </remarks>
public readonly struct RectU : IEquatable<RectU>
{
    /// <summary>
    /// Создаёт прямоугольник.
    /// </summary>
    /// <param name="x">Координата левой границы в пикселях.</param>
    /// <param name="y">Координата верхней границы в пикселях.</param>
    /// <param name="width">Число пикселей по горизонтали.</param>
    /// <param name="height">Число пикселей по вертикали.</param>
    /// <exception cref="ArgumentOutOfRangeException">Размер неположителен.</exception>
    public RectU(int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Пустой прямоугольник: результат отсечения непересекающихся областей.
    /// </summary>
    public static RectU Empty => default;

    /// <summary>
    /// Координата левой границы.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Координата верхней границы.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Число пикселей по горизонтали.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Число пикселей по вертикали.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Координата левой границы.
    /// </summary>
    public int Left => X;

    /// <summary>
    /// Координата верхней границы.
    /// </summary>
    public int Top => Y;

    /// <summary>
    /// Номер самого правого пикселя: граница включительная.
    /// </summary>
    public int Right => X + Width - 1;

    /// <summary>
    /// Номер самого нижнего пикселя: граница включительная.
    /// </summary>
    public int Bottom => Y + Height - 1;

    /// <summary>
    /// Число пикселей в прямоугольнике.
    /// </summary>
    public int Area => Width * Height;

    /// <summary>
    /// Прямоугольник не содержит пикселей.
    /// </summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>
    /// Проверяет принадлежность пикселя прямоугольнику.
    /// </summary>
    /// <param name="x">Координата пикселя по горизонтали.</param>
    /// <param name="y">Координата пикселя по вертикали.</param>
    /// <returns><c>true</c>, если пиксель внутри, включая границы.</returns>
    public bool Contains(int x, int y)
        => !IsEmpty && x >= Left && x <= Right && y >= Top && y <= Bottom;

    /// <summary>
    /// Проверяет, что прямоугольник целиком внутри другого.
    /// </summary>
    /// <param name="other">Ограничивающий прямоугольник.</param>
    /// <returns><c>true</c>, если прямоугольник внутри.</returns>
    public bool ContainsRect(in RectU other)
        => !IsEmpty
            && !other.IsEmpty
            && other.Left >= Left
            && other.Top >= Top
            && other.Right <= Right
            && other.Bottom <= Bottom;

    /// <summary>
    /// Проверяет, есть ли у прямоугольников хотя бы один общий пиксель.
    /// </summary>
    /// <param name="other">Другой прямоугольник.</param>
    /// <returns><c>true</c>, если пересечение непустое.</returns>
    public bool Intersects(in RectU other)
        => !IsEmpty
            && !other.IsEmpty
            && Left <= other.Right
            && other.Left <= Right
            && Top <= other.Bottom
            && other.Top <= Bottom;

    /// <summary>
    /// Возвращает общую часть прямоугольников. Пересечение пустое, если
    /// общая часть состоит из пустого набора пикселей.
    /// </summary>
    /// <param name="other">Другой прямоугольник.</param>
    /// <returns>Пересечение или <see cref="Empty"/>.</returns>
    public RectU Intersection(in RectU other)
    {
        if (!Intersects(other))
        {
            return Empty;
        }

        int left = Math.Max(Left, other.Left);
        int top = Math.Max(Top, other.Top);
        int right = Math.Min(Right, other.Right);
        int bottom = Math.Min(Bottom, other.Bottom);
        return new RectU(left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary>
    /// Возвращает наименьший прямоугольник, содержащий оба.
    /// </summary>
    /// <param name="other">Другой прямоугольник.</param>
    /// <returns>Объединение прямоугольников.</returns>
    public RectU Union(in RectU other)
    {
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        int left = Math.Min(Left, other.Left);
        int top = Math.Min(Top, other.Top);
        int right = Math.Max(Right, other.Right);
        int bottom = Math.Max(Bottom, other.Bottom);
        return new RectU(left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary>
    /// Смещает прямоугольник, не меняя размера.
    /// </summary>
    /// <param name="offsetX">Смещение по горизонтали.</param>
    /// <param name="offsetY">Смещение по вертикали.</param>
    /// <returns>Смещённый прямоугольник.</returns>
    public RectU Offset(int offsetX, int offsetY)
        => IsEmpty ? this : new RectU(X + offsetX, Y + offsetY, Width, Height);

    /// <inheritdoc/>
    public bool Equals(RectU other)
        => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is RectU other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

    /// <summary>
    /// Сравнивает прямоугольники на равенство.
    /// </summary>
    /// <param name="left">Первый прямоугольник.</param>
    /// <param name="right">Второй прямоугольник.</param>
    /// <returns><c>true</c>, если прямоугольники равны.</returns>
    public static bool operator ==(RectU left, RectU right) => left.Equals(right);

    /// <summary>
    /// Сравнивает прямоугольники на неравенство.
    /// </summary>
    /// <param name="left">Первый прямоугольник.</param>
    /// <param name="right">Второй прямоугольник.</param>
    /// <returns><c>true</c>, если прямоугольники различаются.</returns>
    public static bool operator !=(RectU left, RectU right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"[x={X}, y={Y}, w={Width}, h={Height}]";
}