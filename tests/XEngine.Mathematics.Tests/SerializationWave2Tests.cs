using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using XEngine.Mathematics.Serialization;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Волна 2: исправления сериализации (`Serialization/`).
/// </summary>
/// <remarks>
/// Главный проверяемый инвариант поддиректории: <b>две формы записи одной
/// величины обязаны давать один результат</b>, а <c>Write(Read(x)) == x</c> для
/// всех допустимых <c>x</c>. Загрузка сохранения — граница доверия, поэтому
/// проверка идёт не по отдельным примерам, а по матрицам входов, а решение
/// переводится в один счётчик нарушений, который проверяется после цикла.
/// </remarks>
public sealed class SerializationWave2Tests
{
    private static JsonSerializerOptions Options()
    {
        JsonSerializerOptions options = new()
        {
            Converters =
            {
                new Vector2JsonConverter(),
                new Vector3JsonConverter(),
                new Vector4JsonConverter(),
                new AngleJsonConverter(),
                new Rgba32JsonConverter(),
            },
        };

        return options;
    }

    /// <summary>
    /// Одно чтение, описанное словами: значение либо получено, либо отказ с
    /// указанием типа. Так две формы сравнимы и тогда, когда одна падает, а
    /// вторая нет.
    /// </summary>
    /// <param name="read">Чтение.</param>
    /// <returns>Результат чтения.</returns>
    private static string Outcome<T>(Func<T> read)
    {
        try
        {
            return "ok:" + read();
        }
        catch (Exception exception) when (exception is JsonException
            or NotSupportedException
            or FormatException
            or ArgumentException
            or OverflowException
            or InvalidOperationException)
        {
            return "throw:" + exception.GetType().Name;
        }
    }

    // =====================================================================
    // P1-8: вложенная структура в значении канала теряла позицию
    // =====================================================================

    /// <summary>
    /// Девять видов значения; каждый ставится в каждую из трёх позиций по очереди,
    /// остальные заполняются константой: 8 масок на 9 видов дают 72 пары
    /// «массивная форма против объектной».
    /// </summary>
    private static readonly string[] Kinds =
    {
        "1",
        "null",
        "\"текст\"",
        "true",
        "[2,3]",
        "{\"a\":2}",
        "{}",
        "[]",
        "[[1,2],[3,4]]",
    };

    [Fact]
    public void NestedValueInChannel_ObjectFormMatchesArrayForm()
    {
        JsonSerializerOptions options = Options();
        string[] vectorNames = ["x", "y", "z"];
        string[] colorNames = ["r", "g", "b"];
        int mismatches = 0;
        string first = string.Empty;
        int pairs = 0;

        for (int mask = 0; mask < 8; mask++)
        {
            for (int k = 0; k < Kinds.Length; k++)
            {
                string[] values = new string[3];
                for (int p = 0; p < 3; p++)
                {
                    values[p] = (mask & (1 << p)) != 0 ? Kinds[k] : "7";
                }

                string arrayJson = "[" + string.Join(",", values) + "]";
                string vectorJson = "{" + string.Join(",", vectorNames.Select((n, p) => $"\"{n}\":{values[p]}")) + "}";
                string colorJson = "{" + string.Join(",", colorNames.Select((n, p) => $"\"{n}\":{values[p]}")) + "}";

                string arrayVector = Outcome(() => JsonSerializer.Deserialize<Vector3>(arrayJson, options));
                string objectVector = Outcome(() => JsonSerializer.Deserialize<Vector3>(vectorJson, options));
                string arrayColor = Outcome(() => JsonSerializer.Deserialize<Rgba32>(arrayJson, options));
                string objectColor = Outcome(() => JsonSerializer.Deserialize<Rgba32>(colorJson, options));

                pairs += 2;
                if (arrayVector != objectVector)
                {
                    mismatches++;
                    if (first.Length == 0)
                    {
                        first = $"вектор: {arrayJson} -> {arrayVector}, {vectorJson} -> {objectVector}";
                    }
                }

                if (arrayColor != objectColor)
                {
                    mismatches++;
                    if (first.Length == 0)
                    {
                        first = $"цвет: {arrayJson} -> {arrayColor}, {colorJson} -> {objectColor}";
                    }
                }
            }
        }

        Assert.True(mismatches == 0, $"Формы разошлись в {mismatches} из {pairs} пар. Первое расхождение: {first}");
    }

    /// <summary>
    /// Контрольные случаи с результатом, посчитанным по доктрине: вложенное
    /// значение занимает свою позицию и даёт ноль, как и любой нечисловой
    /// элемент.
    /// </summary>
    [Theory]
    [InlineData("{\"x\":1,\"y\":2,\"z\":{\"x\":5}}", 1f, 2f, 0f)]
    [InlineData("{\"x\":1,\"y\":[2,3],\"z\":4}", 1f, 0f, 4f)]
    [InlineData("{\"x\":1,\"y\":{\"a\":2},\"z\":4}", 1f, 0f, 4f)]
    [InlineData("{\"x\":1,\"y\":2,\"z\":[[3,4],[5,6]]}", 1f, 2f, 0f)]
    [InlineData("{\"x\":1,\"y\":2,\"z\":{}}", 1f, 2f, 0f)]
    [InlineData("{\"x\":1,\"y\":2,\"z\":[]}", 1f, 2f, 0f)]
    [InlineData("{\"z\":[9,9],\"x\":1,\"y\":2}", 1f, 2f, 0f)]
    public void NestedValueInKnownChannel_KeepsItsOwnSlot(string objectJson, float x, float y, float z)
    {
        JsonSerializerOptions options = Options();
        MathAssert.Equal(new Vector3(x, y, z), JsonSerializer.Deserialize<Vector3>(objectJson, options));
    }

    /// <summary>
    /// После чтения читатель обязан стоять на закрывающей скобке внешнего
    /// объекта, а следующий <c>Read</c> обязан сообщить о конце данных: прежде он
    /// оставался внутри вложенного значения, и внешняя скобка не читалась вовсе.
    /// </summary>
    [Theory]
    [InlineData("{\"x\":1,\"y\":2,\"z\":{\"x\":5}}")]
    [InlineData("{\"x\":{\"a\":1},\"y\":[2,3],\"z\":4}")]
    [InlineData("{\"x\":{\"a\":{\"b\":{\"c\":[1,2,3]}}},\"y\":2,\"z\":3}")]
    [InlineData("{\"x\":1,\"y\":{\"a\":1,\"b\":2,\"c\":3},\"z\":{\"d\":{\"e\":[1]}}}")]
    public void ReaderStandsOnOuterEndOfObjectAfterNestedValue(string json)
    {
        byte[] payload = Encoding.UTF8.GetBytes(json);
        Utf8JsonReader reader = new(payload, new JsonReaderOptions());
        reader.Read();

        _ = new Vector3JsonConverter().Read(ref reader, typeof(Vector3), Options());

        Assert.Equal(JsonTokenType.EndObject, reader.TokenType);
        Assert.False(reader.Read());
    }

    /// <summary>
    /// Цвет: вложенное значение в узнанном канале не попадает в другой канал.
    /// Прежде <c>{"r":1,"g":2,"b":{"r":0.25}}</c> давал <c>#40FF0000</c>: значение
    /// перезаписало красный, а альфа стала нулём.
    /// </summary>
    [Fact]
    public void NestedValueInColorChannel_KeepsItsOwnSlot()
    {
        Rgba32 color = JsonSerializer.Deserialize<Rgba32>("{\"r\":1,\"g\":2,\"b\":{\"r\":0.25}}", Options());

        SameColor(new Rgba32(1f, 2f, 0f, 1f), color);
    }

    /// <summary>
    /// Глубокая вложенность в чужом поле пропускается целиком: иначе следующий
    /// канал читается из глубины чужой структуры.
    /// </summary>
    [Fact]
    public void DeeplyNestedUnknownPropertyDoesNotShiftChannels()
    {
        JsonSerializerOptions options = Options();

        MathAssert.Equal(
            new Vector3(1f, 2f, 3f),
            JsonSerializer.Deserialize<Vector3>("{\"q\":{\"a\":{\"b\":[1,2,3]}},\"x\":1,\"y\":2,\"z\":3}", options));
        MathAssert.Equal(
            new Vector3(1f, 2f, 3f),
            JsonSerializer.Deserialize<Vector3>("{\"x\":1,\"y\":2,\"z\":3,\"q\":[{\"a\":[1]},{\"b\":2}]}", options));
        MathAssert.Equal(
            new Vector4(1f, 2f, 3f, 4f),
            JsonSerializer.Deserialize<Vector4>("{\"x\":1,\"y\":2,\"z\":3,\"w\":4,\"q\":{\"a\":[1,2]}}", options));
    }

    // =====================================================================
    // P2-45: угол не возвращался в себя
    // =====================================================================

    /// <summary>
    /// Эталон кругового прохода не переиспользует конвертер: записанный файл
    /// разбирается <see cref="JsonDocument"/>, а угол строится из поля
    /// <c>radians</c> напрямую. Если конвертер читает другое поле, эталон с ним
    /// расходится.
    /// </summary>
    /// <param name="json">Записанный угол.</param>
    /// <returns>Угол, который обязан получиться.</returns>
    private static Angle ReferenceAngle(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return Angle.FromRadians(document.RootElement.GetProperty("radians").GetDouble());
    }

    [Fact]
    public void AngleRoundTripReturnsEveryAngleToItself()
    {
        JsonSerializerOptions options = Options();
        Random random = new(20260910);
        const int Total = 200_000;
        int mismatches = 0;
        string first = string.Empty;

        for (int i = 0; i < Total; i++)
        {
            Angle original = Angle.FromRadians((random.NextDouble() - 0.5) * 20.0 * Math.PI);
            string json = JsonSerializer.Serialize(original, options);
            Angle read = JsonSerializer.Deserialize<Angle>(json, options);

            if (!read.Equals(ReferenceAngle(json)))
            {
                mismatches++;
                if (first.Length == 0)
                {
                    first = $"{original.Radians:R} -> {json} -> {read.Radians:R}";
                }
            }
        }

        Assert.True(mismatches == 0, $"Углов не вернулось в себя {mismatches} из {Total}. Первое: {first}");
    }

    /// <summary>
    /// Полоса шириной 1e-6° у −180°: округление градусов переворачивало там знак
    /// на 2π, то есть поворот на полный оборот вместо полуоборота.
    /// </summary>
    [Fact]
    public void AngleRoundTripDoesNotFlipSignAtNegativeHalfTurn()
    {
        JsonSerializerOptions options = Options();
        const int Samples = 20_000;
        int flipped = 0;
        double worstGap = 0;

        for (int i = 0; i < Samples; i++)
        {
            Angle original = Angle.FromDegrees(-180.0 + (i * 5e-11));
            Angle read = JsonSerializer.Deserialize<Angle>(JsonSerializer.Serialize(original, options), options);

            double gap = Math.Abs(original.Radians - read.Radians);
            if (gap > worstGap)
            {
                worstGap = gap;
            }

            if (original.Radians < 0 && read.Radians > 0)
            {
                flipped++;
            }
        }

        Assert.True(flipped == 0, $"Знак перевернулся {flipped} раз из {Samples}, худший разрыв {worstGap:R} рад.");
    }

    /// <summary>
    /// Углы меньше половины шестого знака градуса прежде читались как ноль:
    /// округление градусов обнуляло их целиком.
    /// </summary>
    [Theory]
    [InlineData(5e-7)]
    [InlineData(1e-7)]
    [InlineData(1e-8)]
    [InlineData(1e-9)]
    [InlineData(1e-10)]
    [InlineData(-5e-7)]
    [InlineData(-1e-9)]
    public void TinyAnglesSurviveRoundTrip(double degrees)
    {
        JsonSerializerOptions options = Options();
        Angle original = Angle.FromDegrees(degrees);

        Angle read = JsonSerializer.Deserialize<Angle>(JsonSerializer.Serialize(original, options), options);

        Assert.Equal(original.Radians, read.Radians);
    }

    /// <summary>
    /// Запись не должна выдавать величину, которую нельзя прочитать обратно.
    /// Округлённое поле градусов уходило в файл и оттуда же выбиралось при
    /// чтении, то есть круговой проход не был тождеством.
    /// </summary>
    [Fact]
    public void WrittenAngleCarriesNoRoundedDegrees()
    {
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(Angle.FromDegrees(33.3), Options()));

        int properties = 0;
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            properties++;
            Assert.Equal("radians", property.Name);
        }

        Assert.Equal(1, properties);
    }

    /// <summary>
    /// Файл прежней версии, где записаны оба поля, читается как раньше: градусы
    /// побеждают. Это единственное место, где приоритет остался прежним, и
    /// оно закреплено здесь явно.
    /// </summary>
    [Fact]
    public void LegacyAngleWithBothFieldsStillPrefersDegrees()
    {
        Angle angle = JsonSerializer.Deserialize<Angle>("{\"radians\":99.0,\"degrees\":90.0}", Options());

        MathAssert.NearlyEqual(90.0, angle.Degrees, 1e-9);
    }

    /// <summary>
    /// Запись устойчива: повторная запись прочитанного даёт тот же файл, то есть
    /// файл сохранения не «плывёт» от загрузки к загрузке.
    /// </summary>
    [Fact]
    public void AngleTextIsStableAcrossRepeatedRoundTrips()
    {
        JsonSerializerOptions options = Options();
        Random random = new(31337);
        const int Total = 20_000;
        int unstable = 0;

        for (int i = 0; i < Total; i++)
        {
            string first = JsonSerializer.Serialize(Angle.FromRadians((random.NextDouble() - 0.5) * 6.0 * Math.PI), options);
            string second = JsonSerializer.Serialize(JsonSerializer.Deserialize<Angle>(first, options), options);

            if (!string.Equals(first, second, StringComparison.Ordinal))
            {
                unstable++;
            }
        }

        Assert.True(unstable == 0, $"Файл угла изменился после кругового прохода {unstable} раз из {Total}.");
    }

    // =====================================================================
    // P2-46: имена каналов были регистрозависимы
    // =====================================================================

    [Theory]
    [InlineData("{\"X\":1,\"Y\":2,\"Z\":3}", 1f, 2f, 3f)]
    [InlineData("{\"x\":1,\"Y\":2,\"Z\":3}", 1f, 2f, 3f)]
    [InlineData("{\"X\":1,\"y\":2,\"z\":3}", 1f, 2f, 3f)]
    [InlineData("{\"X\":1.5,\"y\":2.5,\"Z\":3.5}", 1.5f, 2.5f, 3.5f)]
    [InlineData("{\"X\":[1],\"Y\":{\"a\":2},\"Z\":3}", 0f, 0f, 3f)]
    public void ChannelNamesAreCaseInsensitive(string json, float x, float y, float z)
    {
        MathAssert.Equal(new Vector3(x, y, z), JsonSerializer.Deserialize<Vector3>(json, Options()));
    }

    [Fact]
    public void ColorChannelNamesAreCaseInsensitive()
    {
        Rgba32 color = JsonSerializer.Deserialize<Rgba32>("{\"R\":1,\"G\":0.5,\"B\":0,\"A\":1}", Options());

        SameColor(new Rgba32(1f, 0.5f, 0f, 1f), color);
    }

    [Fact]
    public void AngleFieldNamesAreCaseInsensitive()
    {
        JsonSerializerOptions options = Options();

        MathAssert.NearlyEqual(1.0, JsonSerializer.Deserialize<Angle>("{\"Radians\":1}", options).Radians, 1e-15);
        MathAssert.NearlyEqual(1.0, JsonSerializer.Deserialize<Angle>("{\"RADIANS\":1}", options).Radians, 1e-15);
        MathAssert.NearlyEqual(90.0, JsonSerializer.Deserialize<Angle>("{\"Degrees\":90}", options).Degrees, 1e-9);
    }

    /// <summary>
    /// Флаг <c>PropertyNameCaseInsensitive</c> на собственные имена не влияет, и
    /// потому не может служить оправданием: с ним и без него результат один.
    /// </summary>
    [Fact]
    public void ChannelNamesDoNotDependOnSerializerOption()
    {
        JsonSerializerOptions options = Options();
        options.PropertyNameCaseInsensitive = true;

        MathAssert.Equal(new Vector3(1f, 2f, 3f), JsonSerializer.Deserialize<Vector3>("{\"X\":1,\"Y\":2,\"Z\":3}", options));
    }

    // =====================================================================
    // P2-47: числа вне диапазона проходили молча
    // =====================================================================

    /// <summary>
    /// Наборы имён разделены намеренно: у вектора это `x`, `y`, `z`, у цвета
    /// `r`, `g`, `b`. Чужое имя конвертер пропускает, поэтому один и тот же
    /// текст не может быть отвергнут обоими типами — и проверка на отказ сделана
    /// для каждого набора отдельно.
    /// </summary>
    [Theory]
    [InlineData("[1e40,2,3]")]
    [InlineData("[1e400]")]
    [InlineData("[-1e40,2,3]")]
    [InlineData("[1,1e40,1]")]
    [InlineData("[1e40,0,0,1]")]
    public void ArrayNumbersOutOfRangeAreRejected(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Vector3>(json, Options()));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Rgba32>(json, Options()));
    }

    [Theory]
    [InlineData("{\"x\":1e40,\"y\":2,\"z\":3}")]
    [InlineData("{\"X\":-1e40,\"Y\":2,\"Z\":3}")]
    [InlineData("{\"y\":2,\"z\":3,\"x\":1e400}")]
    public void VectorChannelNumbersOutOfRangeAreRejected(string json)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Vector3>(json, Options()));

    [Theory]
    [InlineData("{\"r\":1e40,\"g\":0,\"b\":0,\"a\":1}")]
    [InlineData("{\"R\":1,\"G\":0,\"B\":0,\"A\":-1e400}")]
    [InlineData("{\"b\":0,\"a\":1e39,\"g\":0,\"r\":1}")]
    public void ColorChannelNumbersOutOfRangeAreRejected(string json)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Rgba32>(json, Options()));

    [Theory]
    [InlineData("1e400")]
    [InlineData("-1e400")]
    [InlineData("{\"radians\":1e400}")]
    [InlineData("{\"degrees\":1e400}")]
    [InlineData("{\"radians\":1e400,\"degrees\":1}")]
    [InlineData("{\"Radians\":-1e400}")]
    public void AngleNumbersOutOfRangeAreRejected(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Angle>(json, Options()));
    }

    /// <summary>
    /// Отказ «не помещается в float» не должен путаться с отказом «слишком мало»:
    /// поднормальный ноль лежит в диапазоне и читается как ноль.
    /// </summary>
    [Theory]
    [InlineData("1e-46")]
    [InlineData("1e-400")]
    [InlineData("-1e-46")]
    public void FiniteTinyNumbersAreReadAsZero(string value)
    {
        MathAssert.Equal(
            new Vector3(0f, 2f, 3f),
            JsonSerializer.Deserialize<Vector3>($"[{value},2,3]", Options()));
    }

    /// <summary>
    /// Прочитанное из файла сохранения значение обязано быть записываемым
    /// обратно: иначе файл проходит загрузку и не проходит сохранение.
    /// </summary>
    [Fact]
    public void EveryReadableValueCanBeWrittenBack()
    {
        JsonSerializerOptions options = Options();
        Random random = new(987654);
        const int Total = 20_000;
        int failures = 0;
        string first = string.Empty;

        for (int i = 0; i < Total; i++)
        {
            Vector3 source = new(
                (float)random.NextDouble() * 40f - 20f,
                (float)random.NextDouble() * 40f - 20f,
                (float)random.NextDouble() * 40f - 20f);

            string json = JsonSerializer.Serialize(source, options);
            string outcome = Outcome(() =>
            {
                Vector3 read = JsonSerializer.Deserialize<Vector3>(json, options);
                return read.Equals(source) ? "ok" : "mismatch:" + JsonSerializer.Serialize(read, options);
            });

            if (outcome != "ok:ok")
            {
                failures++;
                if (first.Length == 0)
                {
                    first = $"{source} -> {outcome}";
                }
            }
        }

        Assert.True(failures == 0, $"Значение не пережило запись обратно {failures} раз из {Total}. Первое: {first}");
    }

    /// <summary>
    /// Инвариант диапазона угла <c>(−π; π]</c> обязан выполняться для любого
    /// прочитанного угла: прежде <c>1e400</c> давал <c>NaN</c> молча.
    /// </summary>
    [Fact]
    public void ReadAngleAlwaysStaysInDocumentedRange()
    {
        JsonSerializerOptions options = Options();
        string[] payloads =
        {
            "{\"radians\":0.5}",
            "{\"radians\":-3}",
            "{\"degrees\":179.9}",
            "{\"Radians\":1e-5}",
            "0.5",
            "-3.14159",
            "{\"degrees\":-180}",
        };

        int notFinite = 0;
        int outsideRange = 0;

        foreach (string payload in payloads)
        {
            Angle angle = JsonSerializer.Deserialize<Angle>(payload, options);

            if (!double.IsFinite(angle.Radians))
            {
                notFinite++;
            }
            else if (angle.Radians <= -Math.PI || angle.Radians > Math.PI)
            {
                outsideRange++;
            }
        }

        Random random = new(5150);
        for (int i = 0; i < 200_000; i++)
        {
            Angle angle = JsonSerializer.Deserialize<Angle>(
                JsonSerializer.Serialize(Angle.FromRadians((random.NextDouble() - 0.5) * 2.0 * Math.PI), options),
                options);

            if (!double.IsFinite(angle.Radians))
            {
                notFinite++;
            }
            else if (angle.Radians <= -Math.PI || angle.Radians > Math.PI)
            {
                outsideRange++;
            }
        }

        Assert.True(notFinite == 0, $"Прочитано нечисловых углов: {notFinite}.");
        Assert.True(outsideRange == 0, $"Прочитано углов вне (−π; π]: {outsideRange}.");
    }

    // =====================================================================
    // P2-48: наружу уходил FormatException
    // =====================================================================

    [Theory]
    [InlineData("\"мусор\"", "мусор")]
    [InlineData("\"#GG0000\"", "#GG0000")]
    [InlineData("\"#FF00\"", "#FF00")]
    [InlineData("\"\"", "ожидается #RRGGBB")]
    [InlineData("\"#F0000000FF\"", "#F0000000FF")]
    public void MalformedColorStringThrowsJsonException(string json, string expectedInMessage)
    {
        JsonException error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Rgba32>(json, Options()));

        Assert.Contains(expectedInMessage, error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Граница доверия: вызывающий ловит ошибки разбора одним
    /// <c>catch (JsonException)</c>. Всё, что конвертер может отвергнуть,
    /// обязано быть поймано этим одним обработчиком.
    /// </summary>
    [Fact]
    public void EveryColorFailureIsCaughtByJsonException()
    {
        string[] broken =
        {
            "\"мусор\"",
            "\"#GG0000\"",
            "\"\"",
            "\"#FF00\"",
            "[1e40,0,0,1]",
            "1.5",
            "true",
            "{\"r\":1e40,\"g\":0,\"b\":0}",
        };

        int escaped = 0;
        string first = string.Empty;

        foreach (string json in broken)
        {
            string outcome = Outcome(() =>
            {
                _ = JsonSerializer.Deserialize<Rgba32>(json, Options());
                return "ok";
            });

            if (outcome != "throw:JsonException")
            {
                escaped++;
                if (first.Length == 0)
                {
                    first = $"{json} -> {outcome}";
                }
            }
        }

        Assert.True(escaped == 0, $"Из {broken.Length} испорченных входов {escaped} вышли не как JsonException. Первое: {first}");
    }

    // =====================================================================
    // P3 волны 1: счётчик позиций и повтор имени
    // =====================================================================

    /// <summary>
    /// Повтор имени канала не создаёт позицию, которой в данных нет: прежде
    /// <c>{"r":1,"g":0.5,"b":0,"r":0.25}</c> давал <c>#40800000</c> — цвет
    /// полностью исчезал из-за альфы 0, которой в файле нет.
    /// </summary>
    [Fact]
    public void RepeatedChannelNameDoesNotInventAlpha()
    {
        Rgba32 color = JsonSerializer.Deserialize<Rgba32>("{\"r\":1,\"g\":0.5,\"b\":0,\"r\":0.25}", Options());

        SameColor(new Rgba32(1f, 0.5f, 0f, 1f), color);
    }

    /// <summary>
    /// Первое вхождение имени задаёт канал: повтор не должен ни двигать счётчик,
    /// ни переписывать значение.
    /// </summary>
    [Fact]
    public void RepeatedChannelNameKeepsFirstValueAndCount()
    {
        JsonSerializerOptions options = Options();

        SameColor(
            new Rgba32(1f, 0f, 0f, 1f),
            JsonSerializer.Deserialize<Rgba32>("{\"r\":1,\"r\":2,\"r\":3,\"r\":4,\"r\":5}", options));
        SameColor(
            new Rgba32(1f, 0.5f, 0f, 1f),
            JsonSerializer.Deserialize<Rgba32>("{\"r\":1,\"g\":0.5,\"b\":0,\"a\":1,\"r\":0.25}", options));
        SameColor(
            new Rgba32(1f, 0.5f, 0f, 1f),
            JsonSerializer.Deserialize<Rgba32>("{\"r\":1,\"g\":0.5,\"b\":0,\"q\":9,\"q\":9}", options));
    }

    /// <summary>
    /// Чужое имя не считается каналом: форма, где каналы заданы явно, читается
    /// так же, как то же самое массивом.
    /// </summary>
    [Fact]
    public void UnknownNamesDoNotCountAsChannels()
    {
        JsonSerializerOptions options = Options();

        SameColor(
            JsonSerializer.Deserialize<Rgba32>("[1,0.5,0]", options),
            JsonSerializer.Deserialize<Rgba32>("{\"r\":1,\"g\":0.5,\"b\":0,\"q\":9,\"q\":9}", options));
        SameColor(
            JsonSerializer.Deserialize<Rgba32>("[1,0.5,0,0.25]", options),
            JsonSerializer.Deserialize<Rgba32>("{\"r\":1,\"g\":0.5,\"b\":0,\"a\":0.25,\"q\":9}", options));
    }

    // =====================================================================
    // P3 волны 1: нечисловой канал цвета писался молча
    // =====================================================================

    /// <summary>
    /// <summary>
    /// Вектор с <c>NaN</c> отвергает уже сам писатель JSON. Цвет молча
    /// превращался в <c>#000000FF</c>, то есть испорченный файл сохранения
    /// выглядел рабочим. Теперь оба типа ведут себя одинаково: отказ на границе.
    /// </summary>
    /// <remarks>
    /// Проверяется только <c>NaN</c>: <c>±∞</c> до конвертера не доходит,
    /// потому что <c>Rgba32</c> отсекает каналы в <c>[0; 1]</c> в конструкторе, а
    /// <c>NaN</c> отсечку проходит — обе ложны на сравнениях. Это дефект самого
    /// <c>Clamp01</c> (открытый P2-49 в <c>Graphics/</c>), и когда он будет
    /// закрыт, страховка на записи станет проверяемой и для <c>±∞</c>.
    /// </remarks>
    [Fact]
    public void ColorWithNonFiniteChannelIsRejectedOnWrite()
    {
        JsonSerializerOptions options = Options();

        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(new Rgba32(float.NaN, 0f, 0f, 1f), options));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(new Rgba32(0f, float.NaN, 0f, 1f), options));

        // Контроль: конечный цвет пишется как обычно.
        Assert.Equal("\"#FF8000FF\"", JsonSerializer.Serialize(new Rgba32(1f, 0.5f, 0f, 1f), options));
    }

    // =====================================================================
    // P3 волны 1: ключи словарей
    // =====================================================================

    [Fact]
    public void VectorsWorkAsDictionaryKeys()
    {
        JsonSerializerOptions options = Options();

        Dictionary<Vector2, int> pairs = new() { [new Vector2(1.5f, -2.5f)] = 1, [new Vector2(0f, 0f)] = 2 };
        Dictionary<Vector2, int> vectors2 = RoundTrip(pairs, options);
        Assert.Equal(2, vectors2.Count);
        Assert.Equal(1, vectors2[new Vector2(1.5f, -2.5f)]);
        Assert.Equal(2, vectors2[new Vector2(0f, 0f)]);

        Dictionary<Vector3, int> vectors3 = RoundTrip(new Dictionary<Vector3, int> { [new Vector3(1f, 2f, 3f)] = 7 }, options);
        Assert.Equal(7, vectors3[new Vector3(1f, 2f, 3f)]);

        Dictionary<Vector4, int> vectors4 = RoundTrip(new Dictionary<Vector4, int> { [new Vector4(1f, 2f, 3f, 4f)] = 8 }, options);
        Assert.Equal(8, vectors4[new Vector4(1f, 2f, 3f, 4f)]);
    }

    [Fact]
    public void AngleAndColorWorkAsDictionaryKeys()
    {
        JsonSerializerOptions options = Options();

        Dictionary<Angle, int> angles = RoundTrip(
            new Dictionary<Angle, int> { [Angle.FromRadians(0.5)] = 3, [Angle.FromDegrees(-90)] = 4 },
            options);
        Assert.Equal(3, angles[Angle.FromRadians(0.5)]);
        Assert.Equal(4, angles[Angle.FromDegrees(-90)]);

        Dictionary<Rgba32, int> colors = RoundTrip(
            new Dictionary<Rgba32, int> { [new Rgba32(1f, 0f, 0f, 1f)] = 5, [new Rgba32(0f, 0f, 0f, 0f)] = 6 },
            options);
        Assert.Equal(5, colors[new Rgba32(1f, 0f, 0f, 1f)]);
        Assert.Equal(6, colors[new Rgba32(0f, 0f, 0f, 0f)]);
    }

    /// <summary>
    /// Имя ключа обязано быть каноническим: значение, записанное ключом,
    /// сравнивается с тем же значением, собранным иначе. Проверка идёт по тексту,
    /// а не по успешности разбора: разбор-то проходит и сейчас.
    /// </summary>
    [Fact]
    public void DictionaryKeyNameIsCanonicalAndInvariant()
    {
        JsonSerializerOptions options = Options();

        string json = JsonSerializer.Serialize(new Dictionary<Vector3, int> { [new Vector3(1.5f, -2.5f, 0f)] = 1 }, options);
        Assert.Equal("{\"1.5,-2.5,0\":1}", json);

        // Квадратные скобки при чтении допускаются, пробелы и нехватка каналов — нет.
        Dictionary<Vector3, int> parsed = JsonSerializer.Deserialize<Dictionary<Vector3, int>>("{\"[1.5,-2.5,0]\":1}", options)!;
        Assert.Equal(1, parsed[new Vector3(1.5f, -2.5f, 0f)]);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Dictionary<Vector3, int>>("{\"1.5, -2.5, 0\":1}", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Dictionary<Vector3, int>>("{\"1.5,-2.5\":1}", options));
    }

    // =====================================================================
    // P3 волны 1: объектная форма не аллоцирует (правка не была перенесена в угол)
    // =====================================================================

    [Theory]
    [InlineData("{\"x\":1.5,\"y\":2.5,\"z\":3.5}")]
    [InlineData("[1.5,2.5,3.5]")]
    [InlineData("{\"X\":1.5,\"Y\":2.5,\"Z\":3.5}")]
    public void VectorFormsAllocateNothing(string json)
        => Assert.Equal(0, MeasureConverterAllocations(json, new Vector3JsonConverter(), default(Vector3)));

    [Theory]
    [InlineData("{\"r\":1,\"g\":0.5,\"b\":0.25,\"a\":1}")]
    [InlineData("[1,0.5,0.25,1]")]
    public void ColorChannelFormsAllocateNothing(string json)
        => Assert.Equal(0, MeasureConverterAllocations(json, new Rgba32JsonConverter(), default(Rgba32)));

    /// <summary>
    /// Доктрина «объектная форма не аллоцирует» не была перенесена в угол, где
    /// имя каждого свойства стоило 40 байт строки.
    /// </summary>
    [Theory]
    [InlineData("{\"radians\":0.5,\"degrees\":28.6}")]
    [InlineData("0.5")]
    public void AngleFormsAllocateNothing(string json)
        => Assert.Equal(0, MeasureConverterAllocations(json, new AngleJsonConverter(), default(Angle)));

    /// <summary>
    /// Выделение памяти конвертера за вычетом базы: тот же цикл с тем же
    /// читателем, но без разбора. База разная для разных типов (упаковка
    /// <c>Vector3</c> — 32 байта, <c>Angle</c> — 24), поэтому одна общая база
    /// искажала бы дельту на десятки байт.
    /// </summary>
    /// <typeparam name="T">Тип значения.</typeparam>
    /// <param name="json">Разбираемый текст.</param>
    /// <param name="converter">Конвертер.</param>
    /// <param name="baseValue">Значение, которое возвращает база.</param>
    /// <returns>Сколько байт на вызов выделил конвертер.</returns>
    private static long MeasureConverterAllocations<T>(string json, JsonConverter<T> converter, T baseValue)
    {
        const int Iterations = 20_000;
        const int Warmup = 500;
        byte[] payload = Encoding.UTF8.GetBytes(json);
        JsonSerializerOptions options = Options();

        object? sink = null;
        for (int i = 0; i < Warmup; i++)
        {
            Utf8JsonReader warm = new(payload, new JsonReaderOptions());
            warm.Read();
            sink = converter.Read(ref warm, typeof(T), options);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long beforeBase = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < Iterations; i++)
        {
            Utf8JsonReader reader = new(payload, new JsonReaderOptions());
            reader.Read();
            sink = baseValue;
        }

        long baseBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBase;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long beforeRead = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < Iterations; i++)
        {
            Utf8JsonReader reader = new(payload, new JsonReaderOptions());
            reader.Read();
            sink = converter.Read(ref reader, typeof(T), options);
        }

        long readBytes = GC.GetAllocatedBytesForCurrentThread() - beforeRead;
        GC.KeepAlive(sink);
        return (readBytes - baseBytes) / Iterations;
    }

    // =====================================================================
    // Подтверждённое первой волной: не переделываем, но и не теряем
    // =====================================================================

    [Fact]
    public void TwoFormsOfTheSameValueStillAgree()
    {
        JsonSerializerOptions options = Options();

        MathAssert.Equal(
            JsonSerializer.Deserialize<Vector3>("[1.5,null,3.5]", options),
            JsonSerializer.Deserialize<Vector3>("{\"x\":1.5,\"y\":null,\"z\":3.5}", options));
        MathAssert.Equal(
            new Vector3(1.5f, 0f, 3.5f),
            JsonSerializer.Deserialize<Vector3>("{\"x\":1.5,\"y\":null,\"z\":3.5}", options));

        SameColor(
            JsonSerializer.Deserialize<Rgba32>("[1,2,3,null]", options),
            JsonSerializer.Deserialize<Rgba32>("{\"r\":1,\"g\":2,\"b\":3,\"a\":null}", options));

        SameColor(
            JsonSerializer.Deserialize<Rgba32>("[]", options),
            JsonSerializer.Deserialize<Rgba32>("{}", options));

        MathAssert.Equal(
            new Vector4(1f, 2f, 3f, 4f),
            JsonSerializer.Deserialize<Vector4>("[1,2,3,4,5,6]", options));
    }

    [Fact]
    public void EveryTypeSurvivesItsOwnWriter()
    {
        JsonSerializerOptions options = Options();

        Vector2 v2 = new(1.25f, -2.5f);
        Assert.Equal(v2, JsonSerializer.Deserialize<Vector2>(JsonSerializer.Serialize(v2, options), options));

        Vector3 v3 = new(1.25f, -2.5f, 3.75f);
        Assert.Equal(v3, JsonSerializer.Deserialize<Vector3>(JsonSerializer.Serialize(v3, options), options));

        Vector4 v4 = new(1.25f, -2.5f, 3.75f, -4.5f);
        Assert.Equal(v4, JsonSerializer.Deserialize<Vector4>(JsonSerializer.Serialize(v4, options), options));

        Rgba32 color = new(0.25f, 0.5f, 0.75f, 1f);
        Assert.Equal(
            color.ToString(),
            JsonSerializer.Deserialize<Rgba32>(JsonSerializer.Serialize(color, options), options).ToString());

        Angle angle = Angle.FromDegrees(33.3);
        Assert.Equal(
            angle.Radians,
            JsonSerializer.Deserialize<Angle>(JsonSerializer.Serialize(angle, options), options).Radians);
    }

    [Fact]
    public void UnknownAndMissingFieldsKeepTheirBehaviour()
    {
        JsonSerializerOptions options = Options();

        MathAssert.Equal(
            new Vector3(1f, 2f, 4f),
            JsonSerializer.Deserialize<Vector3>("{\"x\":1,\"y\":2,\"z\":4,\"q\":7}", options));
        MathAssert.Equal(Vector2.Zero, JsonSerializer.Deserialize<Vector2>("{}", options));
        MathAssert.NearlyEqual(0.0, JsonSerializer.Deserialize<Angle>("{\"unknown\":42}", options).Radians, 1e-15);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Angle>("{\"radians\":null}", options));
    }

    /// <summary>
    /// Сравнение цветов по каналам без допуска: разбор задаёт каналы точными
    /// числами из файла, поэтому и расхождение видно точно, а не «в пределах
    /// шага квантования в байт».
    /// </summary>
    /// <param name="expected">Ожидаемый цвет.</param>
    /// <param name="actual">Полученный цвет.</param>
    private static void SameColor(Rgba32 expected, Rgba32 actual)
    {
        MathAssert.Equal(expected.R, actual.R, 0f);
        MathAssert.Equal(expected.G, actual.G, 0f);
        MathAssert.Equal(expected.B, actual.B, 0f);
        MathAssert.Equal(expected.A, actual.A, 0f);
    }

    private static Dictionary<TKey, int> RoundTrip<TKey>(Dictionary<TKey, int> source, JsonSerializerOptions options)
        where TKey : notnull
    {
        string json = JsonSerializer.Serialize(source, options);
        return JsonSerializer.Deserialize<Dictionary<TKey, int>>(json, options)!;
    }
}
