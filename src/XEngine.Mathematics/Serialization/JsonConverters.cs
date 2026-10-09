using System.Numerics;
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
}

/// <summary>
/// Конвертер <see cref="Angle"/> в JSON: объект с полями <c>radians</c> и <c>degrees</c>.
/// При чтении используется <c>degrees</c>, если он задан, иначе <c>radians</c>.
/// </summary>
public sealed class AngleJsonConverter : JsonConverter<Angle>
{
    /// <inheritdoc/>
    public override Angle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return Angle.FromRadians(reader.GetDouble());
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

            string property = reader.GetString() ?? string.Empty;
            reader.Read();

            switch (property)
            {
                case "radians":
                    radians = reader.GetDouble();
                    break;
                case "degrees":
                    degrees = reader.GetDouble();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return degrees.HasValue ? Angle.FromDegrees(degrees.Value) : Angle.FromRadians(radians);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Angle value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("radians", value.Radians);
        writer.WriteNumber("degrees", Math.Round(value.Degrees, 6));
        writer.WriteEndObject();
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
            return Rgba32.FromHex(reader.GetString() ?? "#000000FF");
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
        => writer.WriteStringValue(value.ToString());
}

#pragma warning restore CA1062
