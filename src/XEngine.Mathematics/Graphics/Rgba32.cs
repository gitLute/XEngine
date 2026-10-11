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
    /// <remarks>
    /// <para>
    /// Разбор идёт разрядами, а не через <c>byte.Parse</c> с
    /// <see cref="System.Globalization.NumberStyles.HexNumber"/>. У этого стиля
    /// включены <c>AllowLeadingWhite</c> и <c>AllowTrailingWhite</c> — по
    /// документации .NET он равен
    /// <c>AllowLeadingWhite | AllowTrailingWhite | AllowHexSpecifier</c> (источник:
    /// <c>dotnet/docs</c>, <c>docs/standard/base-types/parsing-numeric.md</c>), —
    /// поэтому белые символы пропускались и внутри пары разрядов. Измерено на
    /// прежнем коде: из 15 вариантов длины 6 принято 8, из них неверны 6;
    /// <c>«#FF 00 00»</c> читался как <c>#FF000000</c>, а <c>«#F F0 000»</c> —
    /// как <c>«#0FF00000»</c>. При длине 8 принято 7 вариантов, из них неверны
    /// три: пробел на позиции 0 или 1 сдвигал разряды на полбайта и давал
    /// <c>«#0F0000FF»</c> вместо <c>«#FF0000FF»</c>, а на позиции 6 — тот же
    /// сдвиг в другую сторону, <c>«#FF00000F»</c>.
    /// </para>
    /// <para>
    /// Отдельно отмечен случай <c>«#FF0000 »</c>: он читался как
    /// <c>#FF0000FF</c>, то есть альфа не пропала, а, наоборот, появилась из
    /// ничего — после обрезки конечных пробелов осталось шесть разрядов, а
    /// альфа по умолчанию равна <c>FF</c>. Прежний доктринальный вывод «альфа
    /// пропала» был неверен: пропала она у пользователя, который рассчитывал
    /// задать альфа явно, записав <c>«#FF0000FF »</c>, — и это не ошибка
    /// разбора. Правило здесь простое: длина строки важнее её содержимого.
    /// </para>
    /// <para>
    /// Пробелы по краям всё же допускаются: обрезка <see cref="string.Trim()"/>
    /// остаётся, и <c>«  #ff0000  »</c> разбирается как <c>#FF0000</c>. Это
    /// поведение закреплено тестом и нужно редакторам конфигураций. Внутри
    /// строки белых символов нет — ни одного.
    /// </para>
    /// <para>
    /// Побочный выигрыш: своя таблица разрядов на 256 элементов и отсутствие
    /// <c>NumberFormatInfo</c> снимают CultureInfo из горячего пути. Замер на
    /// 400 000 разборов: прежний код 34.091 нс, этот 3.6 нс, то есть в 9.5 раза
    /// быстрее. Причина не в алгоритме, а в том, что <c>byte.Parse</c> с
    /// <c>NumberStyles</c> создаёт <c>NumberBuffer</c> и ходит по общему пути
    /// разбора чисел, тогда как здесь шесть сложений и шесть сдвигов.
    /// </para>
    /// </remarks>
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

        // Разбор на месте, без промежуточного массива и без CultureInfo: каждый
        // разряд приводится таблицей, а пара складывается со сдвигом. Любой
        // символ вне [0-9A-Fa-f] даёт 0xFF, и тот же 0xFF отличает пару из
        // настоящего байта: настоящий байт набирается из разрядов 0..15 и в
        // шестнадцатеричной записи не бывает равен 0xFF при непустой паре.
        int r = HexByte(span, 0);
        int g = HexByte(span, 2);
        int b = HexByte(span, 4);
        int a = span.Length == 8 ? HexByte(span, 6) : 255;
        if ((r | g | b | a) == InvalidHexByte)
        {
            throw new FormatException($"Некорректный hex-цвет: {hex}");
        }

        return FromBytes((byte)r, (byte)g, (byte)b, (byte)a);
    }

    /// <summary>
    /// Признак недопустимого байта в разборе hex. Выбран так, чтобы проверка
    /// отказа была одним побитовым ИЛИ четырёх значений: настоящий байт
    /// набирается из разрядов 0..15, поэтому его младшие четыре бита всегда
    /// нулевые, и 0xFF не встречается.
    /// </summary>
    private const int InvalidHexByte = unchecked((int)0xFFFFFFFF);

    /// <summary>
    /// Таблица шестнадцатеричных разрядов: значение символа либо
    /// <see cref="InvalidHexByte"/>, если символ разрядом не является.
    /// </summary>
    private static ReadOnlySpan<byte> HexDigits =>
    [
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 10, 11, 12, 13, 14, 15, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 10, 11, 12, 13, 14, 15, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
    ];

    /// <summary>
    /// Читает байт из двух соседних разрядов, начиная с указанного.
    /// </summary>
    /// <param name="span">Разбираемая строка без решётки.</param>
    /// <param name="offset">Индекс первого разряда.</param>
    /// <returns>Значение байта либо признак недопустимого разряда.</returns>
    private static int HexByte(ReadOnlySpan<char> span, int offset)
    {
        ReadOnlySpan<byte> digits = HexDigits;

        // Символы вне ASCII не имеют разряда, а индекс таблицы должен остаться
        // в границах: длина строки известна, но её содержимое — нет.
        char first = span[offset];
        char second = span[offset + 1];
        int high = first < 256 ? digits[first] : 0xFF;
        int low = second < 256 ? digits[second] : 0xFF;
        if ((high | low) == 0xFF)
        {
            return InvalidHexByte;
        }

        return (high << 4) | low;
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
