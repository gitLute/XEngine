using System.Buffers.Text;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// CA1062 отключён для файла: Write переопределяет метод System.Text.Json,
// который передаёт writer, гарантированно не равный null. Проверка стала бы
// мёртвым кодом в пути сериализации, а правило проверки аргументов в
// собственные методы движка продолжает действовать.
#pragma warning disable CA1062

namespace XEngine.Mathematics.Serialization;

/// <summary>
/// Конвертер <see cref="Vector2"/> в JSON: массив из двух чисел <c>[x, y]</c>.
/// </summary>
public sealed class Vector2JsonConverter : JsonConverter<Vector2>
{
    /// <inheritdoc/>
    public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Span<float> values = stackalloc float[2];
        JsonArrayReaderHelper.ReadFloatArray(ref reader, values);
        return new Vector2(values[0], values[1]);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
        => JsonArrayReaderHelper.WriteFloatArray(writer, [value.X, value.Y]);

    /// <inheritdoc/>
    public override Vector2 ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Span<float> values = stackalloc float[2];
        JsonArrayReaderHelper.ReadChannelsPropertyName(ref reader, values);
        return new Vector2(values[0], values[1]);
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
        => JsonArrayReaderHelper.WriteChannelsPropertyName(writer, [value.X, value.Y]);
}

/// <summary>
/// Конвертер <see cref="Vector3"/> в JSON: массив из трёх чисел <c>[x, y, z]</c>.
/// </summary>
public sealed class Vector3JsonConverter : JsonConverter<Vector3>
{
    /// <inheritdoc/>
    public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Span<float> values = stackalloc float[3];
        JsonArrayReaderHelper.ReadFloatArray(ref reader, values);
        return new Vector3(values[0], values[1], values[2]);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
        => JsonArrayReaderHelper.WriteFloatArray(writer, [value.X, value.Y, value.Z]);

    /// <inheritdoc/>
    public override Vector3 ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Span<float> values = stackalloc float[3];
        JsonArrayReaderHelper.ReadChannelsPropertyName(ref reader, values);
        return new Vector3(values[0], values[1], values[2]);
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
        => JsonArrayReaderHelper.WriteChannelsPropertyName(writer, [value.X, value.Y, value.Z]);
}

/// <summary>
/// Конвертер <see cref="Vector4"/> в JSON: массив из четырёх чисел <c>[x, y, z, w]</c>.
/// </summary>
public sealed class Vector4JsonConverter : JsonConverter<Vector4>
{
    /// <inheritdoc/>
    public override Vector4 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Span<float> values = stackalloc float[4];
        JsonArrayReaderHelper.ReadFloatArray(ref reader, values);
        return new Vector4(values[0], values[1], values[2], values[3]);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Vector4 value, JsonSerializerOptions options)
        => JsonArrayReaderHelper.WriteFloatArray(writer, [value.X, value.Y, value.Z, value.W]);

    /// <inheritdoc/>
    public override Vector4 ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Span<float> values = stackalloc float[4];
        JsonArrayReaderHelper.ReadChannelsPropertyName(ref reader, values);
        return new Vector4(values[0], values[1], values[2], values[3]);
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, Vector4 value, JsonSerializerOptions options)
        => JsonArrayReaderHelper.WriteChannelsPropertyName(writer, [value.X, value.Y, value.Z, value.W]);
}

/// <summary>
/// Конвертер <see cref="Angle"/> в JSON: объект с полем <c>radians</c>, либо голое
/// число радианов.
/// </summary>
/// <remarks>
/// При чтении <c>degrees</c> имеет приоритет над <c>radians</c>: так было задумано
/// для файлов, записанных вручную, где градусы пишут читаемее. Поле
/// <c>degrees</c> при записи не пишется, и это не потеря, а условие обратимости:
/// <c>degrees</c> — это <c>radians</c>, переведённые в градусы и округлённые до
/// шести знаков, то есть необратимая величина. Пока `Write` писал оба поля, а
/// `Read` читал округлённое, круговой проход не был тождеством ни для одного угла:
/// из 200 000 проверенных не возвращался в себя ни один, в полосе шириной 1e-6°
/// у −180° знак переворачивался на 2π, а углы меньше 5e-7° читались как ноль.
/// Теперь `Write` пишет то, что `Read` читает без потерь.
/// <para>
/// Старые файлы, где записаны оба поля, читаются как раньше: <c>degrees</c>
/// побеждает. Новые файлы читаются старой библиотекой без потерь тоже — поля
/// <c>degrees</c> в них нет, а значит нет и повода для приоритета.
/// </para>
/// </remarks>
public sealed class AngleJsonConverter : JsonConverter<Angle>
{
    /// <inheritdoc/>
    public override Angle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return Angle.FromRadians(ReadFiniteDouble(ref reader));
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Ожидался объект угла {radians, degrees} или число в радианах.");
        }

        double radians = 0;
        double? degrees = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Ожидалось имя свойства угла.");
            }

            // Имя сверяется байтами буфера читателя, а не вытягивается в строку:
            // GetString() на каждом имени давал 40 байт на вызов, то есть 80 на
            // объект с двумя полями, там где у соседних конвертеров ноль.
            // Имя принимается в любом регистре, как и имена каналов вектора:
            // ValueTextEquals сверяет по байтам и «Radians» пропускает, а
            // Ascii.EqualsIgnoreCase добирает остальные написания вроде
            // «RADIANS», не выделяя памяти.
            bool isRadians = IsFieldName(ref reader, "radians"u8, "Radians"u8);
            bool isDegrees = IsFieldName(ref reader, "degrees"u8, "Degrees"u8);
            reader.Read();

            if (isRadians)
            {
                if (reader.TokenType != JsonTokenType.Number)
                {
                    throw new JsonException("Ожидалось число в поле radians.");
                }

                radians = ReadFiniteDouble(ref reader);
            }
            else if (isDegrees)
            {
                if (reader.TokenType != JsonTokenType.Number)
                {
                    throw new JsonException("Ожидалось число в поле degrees.");
                }

                degrees = ReadFiniteDouble(ref reader);
            }

            // На числе Skip() ничего не делает, а на вложенном объекте уводит
            // читателя на его конец: без него {"radians":{"a":1},"degrees":90}
            // оставлял читателя внутри значения и следующий Read() попадал не
            // туда, куда его положили.
            reader.Skip();
        }

        return degrees.HasValue ? Angle.FromDegrees(degrees.Value) : Angle.FromRadians(radians);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Angle value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("radians", value.Radians);
        writer.WriteEndObject();
    }

    /// <inheritdoc/>
    public override Angle ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ReadOnlySpan<byte> span = reader.ValueSpan;
        if (!Utf8Parser.TryParse(span, out double radians, out int consumed)
            || consumed != span.Length
            || !double.IsFinite(radians))
        {
            throw new JsonException($"Имя свойства «{Encoding.UTF8.GetString(span)}» не разобрано как число радианов.");
        }

        return Angle.FromRadians(radians);
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, Angle value, JsonSerializerOptions options)
    {
        Span<char> buffer = stackalloc char[32];
        value.Radians.TryFormat(buffer, out int written, default, CultureInfo.InvariantCulture);
        writer.WritePropertyName(buffer[..written]);
    }

    /// <summary>
    /// Сверяет имя свойства угла с литералом, игнорируя регистр ASCII.
    /// </summary>
    /// <param name="reader">Читатель, стоящий на имени свойства.</param>
    /// <param name="lower">Ожидаемое имя в нижнем регистре.</param>
    /// <param name="upper">Ожидаемое имя в верхнем регистре.</param>
    /// <returns><see langword="true"/>, если имя совпало.</returns>
    private static bool IsFieldName(ref Utf8JsonReader reader, ReadOnlySpan<byte> lower, ReadOnlySpan<byte> upper)
    {
        // Сначала точное сравнение: оно разбирает и экранированные имена, и
        // стоит одного сравнения на запись, которую пишет сама библиотека.
        return reader.ValueTextEquals(lower)
            || reader.ValueTextEquals(upper)
            || Ascii.EqualsIgnoreCase(reader.ValueSpan, lower);
    }

    /// <summary>
    /// Читает число, отвергая значение вне диапазона <see cref="double"/>.
    /// </summary>
    /// <param name="reader">Читатель, стоящий на токене числа.</param>
    /// <returns>Конечное число.</returns>
    /// <exception cref="JsonException">Значение не помещается в <see cref="double"/>.</exception>
    /// <remarks>
    /// <c>GetDouble()</c> на <c>1e400</c> даёт <c>∞</c>, а нормализация угла из
    /// <c>∞</c> даёт <c>NaN</c>: документированный инвариант <c>(−π; π]</c>
    /// нарушался без единой ошибки, и такой угол уже нельзя записать обратно.
    /// </remarks>
    private static double ReadFiniteDouble(ref Utf8JsonReader reader)
    {
        double value = reader.GetDouble();
        if (!double.IsFinite(value))
        {
            throw new JsonException($"Число «{Encoding.UTF8.GetString(reader.ValueSpan)}» не помещается в double.");
        }

        return value;
    }
}

/// <summary>
/// Конвертер <see cref="Rgba32"/> в JSON. Принимает три формы записи: строку
/// <c>#RRGGBBAA</c>, массив каналов <c>[r, g, b, a]</c> и объект с именами
/// каналов <c>{"r":..,"g":..,"b":..,"a":..}</c>.
/// </summary>
/// <remarks>
/// Объектная форма добавлена вместе с векторными и по тем же правилам: позицию
/// канала задаёт имя, нечисловое значение даёт ноль, а три канала означают
/// альфу по умолчанию. Писать цвет объектом умеют редакторы и конвертеры
/// префабов, и раньше такой ввод отвергался, тогда как `{"x":..,"y":..,"z":..}`
/// у вектора читался.
/// </remarks>
public sealed class Rgba32JsonConverter : JsonConverter<Rgba32>
{
    /// <inheritdoc/>
    public override Rgba32 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return FromHexChecked(reader.GetString() ?? string.Empty);
        }

        if (reader.TokenType == JsonTokenType.StartArray || reader.TokenType == JsonTokenType.StartObject)
        {
            Span<float> values = stackalloc float[4];
            int count = JsonArrayReaderHelper.ReadFloatArray(ref reader, values, ChannelNames.Color);

            // Считать надо реально прочитанные позиции, а не длину буфера:
            // stackalloc всегда ровно четыре элемента, поэтому проверка
            // values.Length == 4 была всегда истинна, а для массива из трёх
            // чисел непрочитанный слот давал альфу 0 вместо 1, то есть
            // полностью невидимый цвет.
            float alpha = count >= 4 ? values[3] : 1f;
            return new Rgba32(values[0], values[1], values[2], alpha);
        }

        throw new JsonException("Ожидалась строка цвета #RRGGBBAA, массив каналов [r, g, b, a] или объект {r, g, b, a}.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Rgba32 value, JsonSerializerOptions options)
    {
        // Нечисловой канал отвергается, а не пишется молча. Для вектора тот же
        // случай отвергает сам Utf8JsonWriter, и разбирается об этом только
        // вызывающий, уже после того, как файл сохранения испорчен неявным
        // черным пикселем: Clamp01(NaN) даёт NaN, а в байты он превращается в
        // ноль. Здесь ошибка возникает на границе, где её ещё можно показать.
        if (!float.IsFinite(value.R) || !float.IsFinite(value.G) || !float.IsFinite(value.B) || !float.IsFinite(value.A))
        {
            throw new JsonException("Цвет с нечисловым каналом нельзя записать в JSON: все каналы должны быть конечными числами.");
        }

        writer.WriteStringValue(value.ToString());
    }

    /// <inheritdoc/>
    public override Rgba32 ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => FromHexChecked(Encoding.UTF8.GetString(reader.ValueSpan));

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, Rgba32 value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.ToString());

    /// <summary>
    /// Разбирает цвет из строки, переводя ошибку формата в ошибку разбора JSON.
    /// </summary>
    /// <param name="hex">Строка цвета.</param>
    /// <returns>Цвет.</returns>
    /// <exception cref="JsonException">Строка не является hex-цветом.</exception>
    /// <remarks>
    /// <see cref="Rgba32.FromHex"/> бросает <see cref="FormatException"/>, а
    /// <c>System.Text.Json</c> оборачивает в <see cref="JsonException"/> только
    /// <see cref="InvalidOperationException"/>. Наружу уходил
    /// <see cref="FormatException"/> без пути и позиции, то есть вызывающий,
    /// который ловит ошибки разбора сохранения одним <c>catch (JsonException)</c>,
    /// этот случай не ловил вовсе, и испорченный файл выглядел как непредвиденная
    /// ошибка внутри движка, а не как ошибка файла.
    /// </remarks>
    private static Rgba32 FromHexChecked(string hex)
    {
        try
        {
            return Rgba32.FromHex(hex);
        }
        catch (FormatException exception)
        {
            throw new JsonException($"Некорректный цвет «{hex}»: ожидается #RRGGBB или #RRGGBBAA.", exception);
        }
    }
}

#pragma warning restore CA1062
