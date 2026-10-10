using System.Buffers.Text;
using System.Globalization;
using System.Numerics;
using System.Text;
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
    /// <exception cref="JsonException">Ожидался массив чисел или число вне диапазона <c>float</c>.</exception>
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
    /// <para>
    /// Число вне диапазона <c>float</c> отвергается. <c>Utf8Parser</c> на переполнении
    /// возвращает <c>±∞</c> и не сообщает об ошибке, поэтому <c>[1e40, 2, 3]</c>
    /// читался как <c>(∞, 2, 3)</c>, а такую величину уже нельзя записать обратно:
    /// <c>Utf8JsonWriter</c> бросает на ней <see cref="ArgumentException"/>. Значение,
    /// прочитанное из файла сохранения и не переживающее запись в файл сохранения,
    /// нарушает инвариант сериализации, то есть ровно то свойство, ради которого
    /// разбор и существует.
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
                destination[index] = reader.TokenType == JsonTokenType.Number ? ReadFiniteSingle(ref reader) : 0f;
            }

            reader.Skip();
            index++;
        }

        return Math.Min(index, destination.Length);
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

    /// <summary>
    /// Записывает значения как имя свойства списком через запятую: <c>"1.5,2.5,3.5"</c>.
    /// </summary>
    /// <param name="writer">Писатель JSON.</param>
    /// <param name="values">Значения.</param>
    /// <remarks>
    /// Форма обязана быть канонической: без пробелов, в инвариантной культуре и с
    /// точностью, достаточной для обратного чтения, — иначе ключ словаря не
    /// сравнивается с тем же значением, записанным другим путём. Квадратные скобки
    /// при чтении допускаются, чтобы принять и <c>"[1,2,3]"</c>.
    /// </remarks>
    internal static void WriteChannelsPropertyName(Utf8JsonWriter writer, ReadOnlySpan<float> values)
    {
        Span<char> buffer = stackalloc char[128];
        int written = 0;

        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                buffer[written++] = ',';
            }

            written += values[i].TryFormat(buffer[written..], out int chars, default, CultureInfo.InvariantCulture)
                ? chars
                : 0;
        }

        writer.WritePropertyName(buffer[..written]);
    }

    /// <summary>
    /// Читает имя свойства как список каналов, записанный
    /// <see cref="WriteChannelsPropertyName"/>.
    /// </summary>
    /// <param name="reader">Читатель, стоящий на имени свойства.</param>
    /// <param name="destination">Приёмник значений.</param>
    /// <exception cref="JsonException">Имя не разобрано как список чисел нужной длины.</exception>
    /// <remarks>
    /// Разбор идёт прямо по байтам буфера читателя, без создания строки: имена
    /// свойств в таких файлах короткие, а лишняя строка на каждый ключ словаря
    /// означала бы выделение памяти на горячем пути разбора сохранения.
    /// </remarks>
    internal static void ReadChannelsPropertyName(ref Utf8JsonReader reader, scoped Span<float> destination)
    {
        ReadOnlySpan<byte> span = reader.ValueSpan;
        ReadOnlySpan<byte> raw = span;

        if (span.Length >= 2 && span[0] == (byte)'[' && span[^1] == (byte)']')
        {
            span = span[1..^1];
        }

        int offset = 0;
        for (int i = 0; i < destination.Length; i++)
        {
            if (i > 0)
            {
                if (offset >= span.Length || span[offset] != (byte)',')
                {
                    throw new JsonException(PropertyNameError(raw, destination.Length));
                }

                offset++;
            }

            if (!Utf8Parser.TryParse(span[offset..], out float value, out int consumed)
                || !float.IsFinite(value))
            {
                throw new JsonException(PropertyNameError(raw, destination.Length));
            }

            destination[i] = value;
            offset += consumed;
        }

        if (offset != span.Length)
        {
            throw new JsonException(PropertyNameError(raw, destination.Length));
        }
    }

    private static string PropertyNameError(ReadOnlySpan<byte> raw, int expected)
        => $"Имя свойства «{Encoding.UTF8.GetString(raw)}» не разобрано как список из {expected} чисел через запятую.";

    private static int ReadNamedFloats(ref Utf8JsonReader reader, scoped Span<float> destination, ChannelNames names)
    {
        int filled = 0;

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

            // Позицию занимает само имя канала, независимо от того, оказалось
            // ли значение числом: нечисловое даёт ноль, как и в массивной
            // форме. Так обе формы читают одну величину одинаково, и счётчик
            // занятых позиций совпадает с массивным.
            //
            // Приёмник обнулён при выделении, поэтому незаданные каналы уже
            // равны нулю и добирать их отдельно не нужно.
            //
            // Повтор имени канала игнорируется: первое вхождение задаёт
            // значение. Иначе {"r":1,"g":0.5,"b":0,"r":0.25} занимал четыре
            // позиции, цвет получал альфу 0, которой в данных нет, и целиком
            // исчезал. Счётчик считает занятые позиции по битовой маске, а не
            // свойства, поэтому он не может превысить длину приёмника.
            if (index >= 0 && index < destination.Length && (filled & (1 << index)) == 0)
            {
                filled |= 1 << index;
                destination[index] = reader.TokenType == JsonTokenType.Number ? ReadFiniteSingle(ref reader) : 0f;
            }

            // Пропуск обязателен в обеих ветвях и по той же причине, что и в
            // массивной форме: на числе Skip() ничего не делает, а на вложенном
            // объекте или массиве уводит читателя на их конец. Без него
            // {"x":1,"y":2,"z":{"x":5}} клало 5 в x и оставлял читателя внутри
            // вложенного объекта, то есть одна и та же величина, записанная
            // двумя способами, читалась двумя способами по-разному.
            reader.Skip();
        }

        return BitOperations.PopCount((uint)filled);
    }

    /// <summary>
    /// Читает число, отвергая значение вне диапазона <c>float</c>.
    /// </summary>
    /// <param name="reader">Читатель, стоящий на токене числа.</param>
    /// <returns>Конечное число.</returns>
    /// <exception cref="JsonException">Значение не помещается в <c>float</c>.</exception>
    private static float ReadFiniteSingle(ref Utf8JsonReader reader)
    {
        float value = reader.GetSingle();
        if (!float.IsFinite(value))
        {
            throw new JsonException($"Число «{Encoding.UTF8.GetString(reader.ValueSpan)}» не помещается в float.");
        }

        return value;
    }

    /// <summary>
    /// Возвращает индекс канала по имени свойства или <c>−1</c>, если имя не канал.
    /// </summary>
    /// <param name="reader">Читатель, стоящий на имени свойства.</param>
    /// <param name="names">Набор имён каналов.</param>
    /// <returns>Индекс канала или <c>−1</c>.</returns>
    /// <remarks>
    /// Имя канала — ровно один символ, а escape-последовательность в JSON не может
    /// быть короче шести символов. Значит, когда <c>ValueSpan</c> имеет длину один,
    /// это и есть всё имя целиком, и его можно разобрать одним байтом: сравнение
    /// идёт по одному байту вместо четырёх-восьми вызовов <c>ValueTextEquals</c>.
    /// Всё остальное, включая экранированные имена, разбирается запасным путём с
    /// <c>ValueTextEquals</c>, который снимает экранирование.
    /// <para>
    /// Имя принимается в любом регистре. Файл, записанный инструментом с именами
    /// вида <c>X</c>, <c>Y</c>, <c>Z</c>, обязан читаться так же, как написанный
    /// строчными: <c>{"X":1,"Y":2,"Z":3}</c> иначе читался как нулевой вектор без
    /// единой ошибки. Флаг <c>PropertyNameCaseInsensitive</c> тут не помогает: он
    /// действует на рефлексионный путь, а имена сверяет сам конвертер.
    /// </para>
    /// </remarks>
    private static int IndexOfChannel(ref Utf8JsonReader reader, ChannelNames names)
    {
        ReadOnlySpan<byte> span = reader.ValueSpan;
        if (span.Length == 1)
        {
            // 0x20 превращает прописную ASCII-букву в строчную, и только её:
            // цифры и знаки препинания этот бит не затрагивает, а значит иного
            // байта, дающего ту же букву, не существует.
            byte lower = (byte)(span[0] | 0x20);

            if (names == ChannelNames.Color)
            {
                return lower switch
                {
                    (byte)'r' => 0,
                    (byte)'g' => 1,
                    (byte)'b' => 2,
                    (byte)'a' => 3,
                    _ => -1,
                };
            }

            return lower switch
            {
                (byte)'x' => 0,
                (byte)'y' => 1,
                (byte)'z' => 2,
                (byte)'w' => 3,
                _ => -1,
            };
        }

        return IndexOfEscapedChannel(ref reader, names);
    }

    /// <summary>
    /// Запасной путь сверки имени: сравнение по литералам UTF-8, которое снимает
    /// экранирование. Сюда попадают только имена длиннее одного символа.
    /// </summary>
    /// <param name="reader">Читатель, стоящий на имени свойства.</param>
    /// <param name="names">Набор имён каналов.</param>
    /// <returns>Индекс канала или <c>−1</c>.</returns>
    private static int IndexOfEscapedChannel(ref Utf8JsonReader reader, ChannelNames names)
    {
        if (names == ChannelNames.Color)
        {
            if (IsChannelName(ref reader, "r"u8, "R"u8))
            {
                return 0;
            }

            if (IsChannelName(ref reader, "g"u8, "G"u8))
            {
                return 1;
            }

            if (IsChannelName(ref reader, "b"u8, "B"u8))
            {
                return 2;
            }

            if (IsChannelName(ref reader, "a"u8, "A"u8))
            {
                return 3;
            }

            return -1;
        }

        if (IsChannelName(ref reader, "x"u8, "X"u8))
        {
            return 0;
        }

        if (IsChannelName(ref reader, "y"u8, "Y"u8))
        {
            return 1;
        }

        if (IsChannelName(ref reader, "z"u8, "Z"u8))
        {
            return 2;
        }

        if (IsChannelName(ref reader, "w"u8, "W"u8))
        {
            return 3;
        }

        return -1;
    }

    /// <summary>
    /// Сверяет имя свойства с двумя литералами: строчным и прописным.
    /// </summary>
    /// <param name="reader">Читатель, стоящий на имени свойства.</param>
    /// <param name="lower">Строчный литерал.</param>
    /// <param name="upper">Прописной литерал.</param>
    /// <returns><see langword="true"/>, если имя совпало с одним из литералов.</returns>
    private static bool IsChannelName(ref Utf8JsonReader reader, ReadOnlySpan<byte> lower, ReadOnlySpan<byte> upper)
        => reader.ValueTextEquals(lower) || reader.ValueTextEquals(upper);
}
