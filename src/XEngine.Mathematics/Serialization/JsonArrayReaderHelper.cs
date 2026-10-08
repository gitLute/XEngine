using System.Text.Json;

namespace XEngine.Mathematics.Serialization;

/// <summary>
/// Общие операции чтения и записи массивов чисел для конвертеров JSON.
/// Вынесены отдельно, чтобы конвертеры не дублировали разбор входных данных.
/// </summary>
internal static class JsonArrayReaderHelper
{
    /// <summary>
    /// Читает массив чисел, при нехватке элементов оставляет значения равными нулю,
    /// лишние элементы игнорирует.
    /// </summary>
    /// <param name="reader">Читатель JSON.</param>
    /// <param name="destination">Приёмник значений.</param>
    /// <returns>
    /// Сколько чисел реально прочитано. Вызывающий обязан опираться на это
    /// значение, а не на <c>destination.Length</c>: длина приёмника задана
    /// вызывающим и от длины JSON-массива не зависит.
    /// </returns>
    /// <exception cref="JsonException">Ожидался массив чисел.</exception>
    internal static int ReadFloatArray(ref Utf8JsonReader reader, scoped Span<float> destination)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return ReadNamedFloats(ref reader, destination);
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Ожидался массив чисел.");
        }

        int index = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            // Позиция продвигается на каждом элементе, а не только на числе:
            // иначе [1, null, 3] прочитался бы как (1, 3, 0) — сдвиг на
            // нечисловой элемент молча меняет значения.
            if (reader.TokenType == JsonTokenType.Number && index < destination.Length)
            {
                destination[index++] = reader.GetSingle();
            }
            else
            {
                reader.Skip();
            }
        }

        return index;
    }

    /// <summary>
    /// Записывает массив чисел в JSON.
    /// </summary>
    /// <param name="writer">Писатель JSON.</param>
    /// <param name="values">Значения.</param>
    internal static void WriteFloatArray(Utf8JsonWriter writer, ReadOnlySpan<float> values)
    {
        writer.WriteStartArray();
        for (int i = 0; i < values.Length; i++)
        {
            writer.WriteNumberValue(values[i]);
        }

        writer.WriteEndArray();
    }

    private static int ReadNamedFloats(ref Utf8JsonReader reader, scoped Span<float> destination)
    {
        string[] names = ["x", "y", "z", "w"];
        Span<bool> assigned = stackalloc bool[4];
        int count = 0;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Ожидалось имя свойства вектора.");
            }

            string property = reader.GetString() ?? string.Empty;
            reader.Read();

            int index = Array.IndexOf(names, property);
            if (index >= 0 && index < destination.Length && reader.TokenType == JsonTokenType.Number)
            {
                destination[index] = reader.GetSingle();
                assigned[index] = true;
                count++;
            }
            else
            {
                reader.Skip();
            }
        }

        for (int i = 0; i < destination.Length; i++)
        {
            if (!assigned[i])
            {
                destination[i] = 0f;
            }
        }

        return count;
    }
}
