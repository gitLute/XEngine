namespace AtlasBuilder;

/// <summary>
/// Прямоугольник в целых пикселях с включёнными границами.
/// </summary>
/// <remarks>
/// Границы включительные — как у <c>AtlasRegion.PixelRect</c> в движке (10.6):
/// прямоугольник 0,0,64,64 покрывает 64 пикселя, а не 65. Инструмент и движок
/// обязаны считать границы одинаково, иначе регион атласа на один пиксель
/// больше или меньше, чем ожидала разметка.
/// </remarks>
public readonly struct IntRect : IEquatable<IntRect>
{
    /// <summary>
    /// Создаёт прямоугольник.
    /// </summary>
    /// <param name="x">Координата левой границы.</param>
    /// <param name="y">Координата верхней границы.</param>
    /// <param name="width">Число пикселей по горизонтали.</param>
    /// <param name="height">Число пикселей по вертикали.</param>
    /// <exception cref="ArgumentOutOfRangeException">Размер неположителен.</exception>
    public IntRect(int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

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
    /// Номер самого правого пикселя: граница включительная.
    /// </summary>
    public int Right => X + Width - 1;

    /// <summary>
    /// Номер самого нижнего пикселя: граница включительная.
    /// </summary>
    public int Bottom => Y + Height - 1;

    /// <summary>
    /// Прямоугольники делят хотя бы один пиксель.
    /// </summary>
    /// <param name="other">Другой прямоугольник.</param>
    /// <returns><see langword="true"/>, если есть общий пиксель.</returns>
    public bool Intersects(in IntRect other)
        => X <= other.Right
            && other.X <= Right
            && Y <= other.Bottom
            && other.Y <= Bottom;

    /// <summary>
    /// Прямоугольник целиком внутри другого.
    /// </summary>
    /// <param name="other">Границы, которые должны вмещать прямоугольник.</param>
    /// <returns><see langword="true"/>, если прямоугольник внутри.</returns>
    public bool IsInside(in IntRect other)
        => X >= other.X
            && Y >= other.Y
            && Right <= other.Right
            && Bottom <= other.Bottom;

    /// <summary>
    /// Расстояние между прямоугольниками в пикселях: ноль при пересечении.
    /// </summary>
    /// <param name="other">Другой прямоугольник.</param>
    /// <returns>Число свободных пикселей между прямоугольниками по обеим осям.</returns>
    public int GapTo(in IntRect other)
    {
        int horizontal = X <= other.X ? other.X - Right - 1 : X - other.Right - 1;
        int vertical = Y <= other.Y ? other.Y - Bottom - 1 : Y - other.Bottom - 1;
        return Math.Max(Math.Max(horizontal, vertical), 0);
    }

    /// <inheritdoc/>
    public bool Equals(IntRect other)
        => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is IntRect other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

    /// <summary>
    /// Сравнивает два прямоугольника.
    /// </summary>
    /// <param name="left">Первый прямоугольник.</param>
    /// <param name="right">Второй прямоугольник.</param>
    /// <returns><see langword="true"/>, если прямоугольники равны.</returns>
    public static bool operator ==(IntRect left, IntRect right) => left.Equals(right);

    /// <summary>
    /// Сравнивает два прямоугольника.
    /// </summary>
    /// <param name="left">Первый прямоугольник.</param>
    /// <param name="right">Второй прямоугольник.</param>
    /// <returns><see langword="true"/>, если прямоугольники различаются.</returns>
    public static bool operator !=(IntRect left, IntRect right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"[{X}, {Y}, {Width}x{Height}]";
}