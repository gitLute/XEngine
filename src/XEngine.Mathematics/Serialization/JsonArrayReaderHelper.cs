using System.Text.Json;

namespace XEngine.Mathematics.Serialization;

/// <summary>
/// Набор имён каналов, которые распознаёт объектная форма разбора.
/// </summary>
/// <remarks>
/// Наборы различаются не по построению, а по смыслу: `r` — это цвет, а не
/// второй компонент вектора. Смешивать их в одной таблице нельзя, потому что
/// тогда `{"r":1}` прочитался бы как `y`, то есть одна и та же опечатка давала
/// бы осмысленный, но неверный результат.
/// </remarks>
internal enum ChannelNames
{
    /// <summary>
    /// `x`, `y`, `z`, `w` — форма, которую пишут векторы.
    /// </summary>
    Vector,

    /// <summary>
    /// `r`, `g`, `b`, `a` — форма, которую пишет цвет.
    /// </summary>
    Color,
}

/// <summary>
/// Общие операции чтения и записи массивов чисел для конвертеров JSON.
/// Вынесены отдельно, чтобы конвертеры не дублировали разбор входных данных.
/// </summary>
internal static class JsonArrayReaderHelper
{
    /// <summary>
    /// Читает массив чисел, при нехватке элементов оставляет значения равными нулю,
    /// лишние элементы игнорирует. Принимает и массив, и объект с именами каналов.
    /// </summary>
    /// <param name="reader">Читатель JSON.</param>
    /// <param name="destination">Приёмник значений.</param>
    /// <param name="names">Набор имён каналов для объектной формы.</param>
    /// <returns>
    /// Сколько позиций приёмника заняли элементы. Вызывающий обязан
    /// опираться на это значение, а не на <c>destination.Length</c>: длина приёмника
    /// задана вызывающим и от длины JSON-массива не зависит.
    /// </returns>
    /// <exception cref="JsonException">Ожидался массив чисел.</exception>
    /// <remarks>
    /// Позиция определяется местом элемента, а не тем, оказался ли он числом. Нечисловой
    /// элемент занимает своё место и даёт ноль, как и отсутствующее имя в объектной
    /// форме: <c>[1.5, null, 3.5]</c> и <c>{"x":1.5, "y":null, "z":3.5}</c> обязаны
    /// читаться одинаково.
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
    /// Счётчик возвращает число занятых позиций, а не число прочитанных чисел, и в
    /// обеих формах считается одинаково. Это нужно вызывающему
    /// <see cref="Rgba32JsonConverter"/>, который по нему решает, задана ли альфа
    /// явно: массив из трёх чисел альфу не задаёт, и она берётся равной единице.
    /// </para>
    /// </remarks>
    internal static int ReadFloatArray(ref Utf8JsonReader reader, scoped Span<float> destination, ChannelNames names = ChannelNames.Vector)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return ReadNamedFloats(ref reader, destination, names);
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

    private static int ReadNamedFloats(ref Utf8JsonReader reader, scoped Span<float> destination, ChannelNames names)
    {
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
            int index = IndexOfChannel(ref reader, names);
            reader.Read();

            if (index >= 0 && index < destination.Length)
            {
                // Позицию занимает само имя канала, независимо от того, оказалось
                // ли значение числом: нечисловое даёт ноль, как и в массивной
                // форме. Так обе формы читают одну величину одинаково, и счётчик
                // занятых позиций совпадает с массивным.
                //
                // Приёмник обнулён при выделении, поэтому незаданные каналы уже
                // равны нулю и добирать их отдельно не нужно.
                destination[index] = reader.TokenType == JsonTokenType.Number ? reader.GetSingle() : 0f;
                count++;
            }
            else
            {
                reader.Skip();
            }
        }

        return count;
    }

    /// <summary>
    /// Возвращает индекс канала по имени свойства или <c>−1</c>, если имя не канал.
    /// </summary>
    /// <param name="reader">Читатель, стоящий на имени свойства.</param>
    /// <param name="names">Набор имён каналов.</param>
    /// <returns>Индекс канала или <c>−1</c>.</returns>
    /// <remarks>
    /// Сравнение идёт по четырём литералам UTF-8, а не поиском в массиве строк.
    /// Массив имён требовал бы либо аллокации на каждый вызов, либо сравнения
    /// строк по содержимому; и то и другое дороже, чем четыре сравнения
    /// последовательностей байт.
    /// </remarks>
    private static int IndexOfChannel(ref Utf8JsonReader reader, ChannelNames names)
    {
        if (names == ChannelNames.Color)
        {
            if (reader.ValueTextEquals("r"u8))
            {
                return 0;
            }

            if (reader.ValueTextEquals("g"u8))
            {
                return 1;
            }

            if (reader.ValueTextEquals("b"u8))
            {
                return 2;
            }

            if (reader.ValueTextEquals("a"u8))
            {
                return 3;
            }

            return -1;
        }

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
