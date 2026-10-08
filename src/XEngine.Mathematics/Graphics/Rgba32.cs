using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Цвет в формате RGBA с каналами, нормализованными в диапазон 0..1.
/// Выбран как замена <c>OpenTK.Mathematics.Color4</c>: не зависит от
/// платформенной библиотеки и выкладывается в память так же, как
/// uniform-вектор OpenGL.
/// </summary>
/// <remarks>
/// Имя <c>Rgba32</c> означает 32 бита на канал в шейдерном представлении, а
/// не размер типа: в памяти он занимает 16 байт, потому что четыре канала
/// хранятся как <see cref="float"/>. Для атрибута вершинного цвета, где нужен
/// упакованный 32-битный элемент, тип не подходит: пересчёт в байты даёт
/// <see cref="ToString"/> и разбор из строки.
/// </remarks>
public readonly struct Rgba32 : IEquatable<Rgba32>
{
    /// <summary>
    /// Создаёт цвет из значений каналов.
    /// </summary>
    /// <param name="r">Красный канал.</param>
    /// <param name="g">Зелёный канал.</param>
    /// <param name="b">Синий канал.</param>
    /// <param name="a">Альфа-канал.</param>
    public Rgba32(float r, float g, float b, float a = 1f)
    {
        R = Interpolation.Clamp01(r);
        G = Interpolation.Clamp01(g);
        B = Interpolation.Clamp01(b);
        A = Interpolation.Clamp01(a);
    }

    /// <summary>
    /// Создаёт цвет из байтов 0..255.
    /// </summary>
    /// <param name="r">Красный канал, 0..255.</param>
    /// <param name="g">Зелёный канал, 0..255.</param>
    /// <param name="b">Синий канал, 0..255.</param>
    /// <param name="a">Альфа-канал, 0..255.</param>
    /// <returns>Цвет.</returns>
    public static Rgba32 FromBytes(byte r, byte g, byte b, byte a = 255)
        => new(r / 255f, g / 255f, b / 255f, a / 255f);

    /// <summary>
    /// Разбирает цвет из строки в формате <c>#RRGGBB</c> или <c>#RRGGBBAA</c>.
    /// </summary>
    /// <param name="hex">Строка цвета, с решёткой или без.</param>
    /// <returns>Разобранный цвет.</returns>
    /// <exception cref="FormatException">Строка не является корректным hex-цветом.</exception>
    public static Rgba32 FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);

        ReadOnlySpan<char> span = hex.AsSpan().Trim();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span[1..];
        }

        if (span.Length is not (6 or 8))
        {
            throw new FormatException($"Ожидался цвет #RRGGBB или #RRGGBBAA, получено: {hex}");
        }

        try
        {
            byte r = byte.Parse(span[..2], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            byte g = byte.Parse(span.Slice(2, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            byte b = byte.Parse(span.Slice(4, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            byte a = span.Length == 8
                ? byte.Parse(span.Slice(6, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture)
                : (byte)255;
            return FromBytes(r, g, b, a);
        }
        catch (FormatException exception)
        {
            throw new FormatException($"Некорректный hex-цвет: {hex}", exception);
        }
    }

    /// <summary>
    /// Красный канал, 0..1.
    /// </summary>
    public float R { get; }

    /// <summary>
    /// Зелёный канал, 0..1.
    /// </summary>
    public float G { get; }

    /// <summary>
    /// Синий канал, 0..1.
    /// </summary>
    public float B { get; }

    /// <summary>
    /// Альфа-канал, 0..1.
    /// </summary>
    public float A { get; }

    /// <summary>
    /// Прозрачный чёрный.
    /// </summary>
    public static Rgba32 Transparent => new(0f, 0f, 0f, 0f);

    /// <summary>
    /// Белый.
    /// </summary>
    public static Rgba32 White => new(1f, 1f, 1f);

    /// <summary>
    /// Чёрный.
    /// </summary>
    public static Rgba32 Black => new(0f, 0f, 0f);

    /// <summary>
    /// Красный.
    /// </summary>
    public static Rgba32 Red => new(1f, 0f, 0f);

    /// <summary>
    /// Зелёный.
    /// </summary>
    public static Rgba32 Green => new(0f, 1f, 0f);

    /// <summary>
    /// Синий.
    /// </summary>
    public static Rgba32 Blue => new(0f, 0f, 1f);

    /// <summary>
    /// Жёлтый.
    /// </summary>
    public static Rgba32 Yellow => new(1f, 1f, 0f);

    /// <summary>
    /// Преобразует цвет в вектор для передачи в шейдер.
    /// </summary>
    /// <returns>Вектор с компонентами RGBA.</returns>
    public Vector4 ToVector4() => new(R, G, B, A);

    /// <summary>
    /// Умножает цвет на число, не затрагивая альфа-канал.
    /// Используется для затемнения текстурированных спрайтов.
    /// </summary>
    /// <param name="factor">Множитель яркости.</param>
    /// <returns>Затемнённый цвет.</returns>
    public Rgba32 WithBrightness(float factor) => new(R * factor, G * factor, B * factor, A);

    /// <summary>
    /// Задаёт альфа-канал.
    /// </summary>
    /// <param name="alpha">Новое значение альфа-канала.</param>
    /// <returns>Цвет с новой прозрачностью.</returns>
    public Rgba32 WithAlpha(float alpha) => new(R, G, B, alpha);

    /// <summary>
    /// Линейная интерполяция между цветами.
    /// </summary>
    /// <param name="from">Начальный цвет.</param>
    /// <param name="to">Конечный цвет.</param>
    /// <param name="t">Параметр интерполяции.</param>
    /// <returns>Интерполированный цвет.</returns>
    public static Rgba32 Lerp(Rgba32 from, Rgba32 to, float t)
    {
        float k = Interpolation.Clamp01(t);
        return new Rgba32(
            from.R + (to.R - from.R) * k,
            from.G + (to.G - from.G) * k,
            from.B + (to.B - from.B) * k,
            from.A + (to.A - from.A) * k);
    }

    /// <inheritdoc/>
    public bool Equals(Rgba32 other) => R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B) && A.Equals(other.A);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Rgba32 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);

    /// <summary>
    /// Сравнивает цвета на равенство.
    /// </summary>
    /// <param name="left">Первый цвет.</param>
    /// <param name="right">Второй цвет.</param>
    /// <returns><c>true</c>, если цвета равны.</returns>
    public static bool operator ==(Rgba32 left, Rgba32 right) => left.Equals(right);

    /// <summary>
    /// Сравнивает цвета на неравенство.
    /// </summary>
    /// <param name="left">Первый цвет.</param>
    /// <param name="right">Второй цвет.</param>
    /// <returns><c>true</c>, если цвета различаются.</returns>
    public static bool operator !=(Rgba32 left, Rgba32 right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"#{ToByte(R):X2}{ToByte(G):X2}{ToByte(B):X2}{ToByte(A):X2}";

    private static byte ToByte(float channel) => (byte)Scalar.Clamp(MathF.Round(channel * 255f), 0f, 255f);
}
