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
    /// Сколько позиций приёмника заняли элементы массива. Вызывающий обязан
    /// опираться на это значение, а не на <c>destination.Length</c>: длина приёмника
    /// задана вызывающим и от длины JSON-массива не зависит.
    /// </returns>
    /// <exception cref="JsonException">Ожидался массив чисел.</exception>
    /// <remarks>
    /// Позиция определяется местом элемента в массиве, а не тем, оказался ли он
    /// числом. Нечисловой элемент занимает своё место и даёт ноль, как и
    /// отсутствующее имя в объектной форме: <c>[1.5, null, 3.5]</c> и
    /// <c>{"x":1.5, "y":null, "z":3.5}</c> обязаны читаться одинаково.
    /// <para>
    /// Прежде индекс двигался только на токене <c>Number</c>, и нечисловой элемент
    /// просто пропускался: <c>[1.5, null, 3.5]</c> читался как <c>(1.5, 3.5, 0)</c>,
    /// то есть третье число занимало место второго. Одна и та же величина, записанная
    /// двумя способами, давала два разных результата, и вызывающий выбирал способ
    /// записи, а не полагался на разбор. Это то же рассогласование, что было между
    /// <c>Aabb2</c> и <c>Aabb3</c>.
    /// </para>
    /// <para>
    /// Следствие для цвета: <c>[r, g, b, null]</c> даёт альфу 0, потому что
    /// четвёртая позиция занята и её значение ноль. Это следует из того же правила,
    /// а не выбрано отдельно: в объектной форме <c>{"r":1,"g":2,"b":3,"a":null}</c>
    /// альфа тоже читается как ноль. Прежде массив из четырёх элементов, где
    /// четвёртый не число, давал альфу 1, то есть отличался от объектной формы.
    /// </para>
    /// <para>
    /// Счётчик возвращает число занятых позиций, а не число прочитанных чисел.
    /// Это нужно вызывающему <see cref="Rgba32JsonConverter"/>, который по нему
    /// решает, задана ли альфа явно: массив из трёх чисел альфу не задаёт, и она
    /// берётся равной единице.
    /// </para>
    /// </remarks>
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
            if (index < destination.Length)
            {
                destination[index] = reader.TokenType == JsonTokenType.Number ? reader.GetSingle() : 0f;
            }

            reader.Skip();
            index++;
        }

        return System.Math.Min(index, destination.Length);
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
        Span<bool> assigned = stackalloc bool[4];
        int count = 0;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Ожидалось имя свойства вектора.");
            }

            // Имя свойства сверяется байтами прямо в буфере читателя, а не
            // вытягивается в строку. ValueTextEquals сравнивает UTF-8 без
            // выделения памяти, тогда как GetString() создаёт строку на каждое
            // свойство: замер на 20 000 вызовах давал 128 байт на объект, из
            // которых 48 уходили на массив имён и 80 — на четыре строки.
            int index = IndexOfVectorAxis(ref reader);
            reader.Read();

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

    /// <summary>
    /// Возвращает индекс оси по имени свойства или <c>−1</c>, если имя не ось.
    /// </summary>
    /// <param name="reader">Читатель, стоящий на имени свойства.</param>
    /// <returns>Индекс оси или <c>−1</c>.</returns>
    /// <remarks>
    /// Сравнение идёт по четырём литералам UTF-8, а не поиском в массиве строк.
    /// Массив имён требовал бы либо аллокации на каждый вызов, либо сравнения
    /// строк по содержимому; и то и другое дороже, чем четыре сравнения
    /// последовательностей байт.
    /// </remarks>
    private static int IndexOfVectorAxis(ref Utf8JsonReader reader)
    {
        if (reader.ValueTextEquals("x"u8))
        {
            return 0;
        }

        if (reader.ValueTextEquals("y"u8))
        {
            return 1;
        }

        if (reader.ValueTextEquals("z"u8))
        {
            return 2;
        }

        if (reader.ValueTextEquals("w"u8))
        {
            return 3;
        }

        return -1;
    }
}
