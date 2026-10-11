using System.Numerics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Контракт типа на границе: значения, которые метод возвращает, должны
/// лежать в диапазоне, который объявляет его же доктрина.
///
/// <para>
/// Общая черта закрытых здесь дефектов — проверка стояла не на той величине:
/// сравнение с нулём вместо сравнения с границей диапазона, сравнение квадрата
/// до округления, деление на величину, вычисленную в более узком типе.
/// </para>
///
/// <para>
/// Правило проверки обязательное: контрольный пример должен быть таким, чтобы
/// отмена правки была видна. Поэтому в каждом тесте есть явное утверждение о
/// границе, а не «значение похоже на ожидаемое». Границы проверяются и на
/// обычных числах — контроль без дефекта не заменяет проверку дефекта.
/// </para>
/// </summary>
public class ContractBoundaryTests
{
    // =================================================================
    // Graphics: Clamp01 и Rgba32
    // =================================================================

    /// <summary>
    /// <c>Clamp01</c> обязан ограничивать параметр в <c>[0; 1]</c>, а
    /// <c>NaN</c> не принадлежит этому отрезку. Прежние два сравнения с нулём
    /// и единицей были на <c>NaN</c> ложны, и метод возвращал нечисловой
    /// параметр как есть.
    /// </summary>
    [Fact]
    public void Clamp01_NeverLeavesUnitRange()
    {
        float[] inputs =
        [
            float.NaN, float.NegativeInfinity, float.PositiveInfinity,
            -1e30f, -1f, -1e-6f, -0f, 0f, 1e-6f, 0.5f, 1f, 7f, 1e30f,
        ];

        foreach (float input in inputs)
        {
            float result = Interpolation.Clamp01(input);
            Assert.True(
                result >= 0f && result <= 1f,
                $"Clamp01({input:R}) = {result:R} вне [0; 1]");
        }
    }

    /// <summary>
    /// Дискриминирующий случай: только нечисловой параметр отличает исправление
    /// от прежнего кода. На конечных числах обе реализации идентичны.
    /// </summary>
    [Fact]
    public void Clamp01_NonFiniteBecomesStartOfRange()
    {
        Assert.Equal(0f, Interpolation.Clamp01(float.NaN));
        Assert.Equal(0f, Interpolation.Clamp01(float.NegativeInfinity));
        Assert.Equal(1f, Interpolation.Clamp01(float.PositiveInfinity));
    }

    /// <summary>
    /// Контроль без дефекта: на конечных числах поведение прежнее.
    /// </summary>
    [Fact]
    public void Clamp01_OnFiniteValuesIsUnchanged()
    {
        Assert.Equal(0f, Interpolation.Clamp01(-5f));
        Assert.Equal(0f, Interpolation.Clamp01(0f));
        Assert.Equal(0.5f, Interpolation.Clamp01(0.5f));
        Assert.Equal(1f, Interpolation.Clamp01(1f));
        Assert.Equal(1f, Interpolation.Clamp01(7f));
    }

    /// <summary>
    /// Цвет хранит каналы в <c>[0; 1]</c>. Нечисловой канал прежде складывался
    /// в цвет как есть, и <c>ToString</c> печатал его как обычный, то есть
    /// испорченный цвет выглядел рабочим.
    /// </summary>
    [Fact]
    public void Rgba32_ChannelsStayInsideUnitRange()
    {
        foreach (float channel in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity, -5f, 7f })
        {
            Rgba32 color = new(channel, 0.5f, 0.25f, 1f);
            Assert.InRange(color.R, 0f, 1f);
            Assert.InRange(color.G, 0f, 1f);
            Assert.InRange(color.B, 0f, 1f);
            Assert.InRange(color.A, 0f, 1f);
        }
    }

    /// <summary>
    /// Дискриминирующий случай: <c>ToString</c> прежде давал
    /// <c>#008040FF</c> — то есть выглядел как обычный цвет, и испорченный
    /// канал был виден только сравнением с самим собой.
    /// </summary>
    [Fact]
    public void Rgba32_ToStringIsDefinedForEveryChannel()
    {
        Rgba32 color = new(float.NaN, 0.5f, 0.25f, 1f);
        string text = color.ToString();

        Assert.StartsWith("#", text, StringComparison.Ordinal);
        Assert.Equal(9, text.Length);
        foreach (char symbol in text)
        {
            Assert.True(symbol == '#' || Uri.IsHexDigit(symbol), $"Не шестнадцатеричный разряд: {symbol}");
        }
    }

    /// <summary>
    /// <c>Lerp</c> обещает значение между границами, и нечисловой параметр не
    /// задаёт положения на отрезке.
    /// </summary>
    [Fact]
    public void Lerp_NonFiniteParameterStaysBetweenBounds()
    {
        foreach (float parameter in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity })
        {
            float result = Interpolation.Lerp(0f, 10f, parameter);
            Assert.InRange(result, 0f, 10f);
        }
    }

    // =================================================================
    // Graphics: Rgba32.FromHex, белые символы внутри строки
    // =================================================================

    /// <summary>
    /// Формат — <c>#RRGGBB[AA]</c>, то есть шесть или восемь разрядов подряд.
    /// <c>NumberStyles.HexNumber</c> включает ведущие и конечные белые
    /// символы, и прежний разбор ими их пропускал, сдвигая разряды.
    /// </summary>
    [Theory]
    [InlineData("#FF 00 00")]
    [InlineData("FF 00 00")]
    [InlineData("#F F0 000")]
    [InlineData("#FF0 000")]
    [InlineData("#FF00 00")]
    [InlineData("#FF000 0")]
    [InlineData("# FF0000")]
    [InlineData("#FF 00 00 AA")]
    [InlineData("#FF00 00AA")]
    [InlineData("#FF0000A A")]
    [InlineData("#FF0000 AA")]
    public void FromHex_RejectsWhiteSpaceInsideDigits(string text)
    {
        Assert.Throws<FormatException>(() => Rgba32.FromHex(text));
    }

    /// <summary>
    /// Дискриминирующий случай со сдвигом на полбайта. Прежний код принимал
    /// <c>«#FF 00 00»</c> как <c>#FF000000</c>, а <c>«#F F0 000»</c> — как
    /// <c>#0FF00000</c>, то есть не ошибался, а молча читал другой цвет.
    /// </summary>
    [Fact]
    public void FromHex_WhiteSpaceNeverShiftsDigitsByHalfByte()
    {
        // Прежние ответы на этих входах, зафиксированные измерением.
        Assert.Throws<FormatException>(() => Rgba32.FromHex("#FF 00 00"));
        Assert.Throws<FormatException>(() => Rgba32.FromHex("#F F0 000"));
    }

    /// <summary>
    /// Пробелы по краям допускаются: это закреплено поведением, нужным
    /// редакторам конфигураций. Правка их не отнимает.
    /// </summary>
    [Fact]
    public void FromHex_StillTrimsOuterWhiteSpace()
    {
        Rgba32 expected = Rgba32.FromHex("#FF0000");
        Assert.Equal(expected, Rgba32.FromHex("  #ff0000 "));
        Assert.Equal(expected, Rgba32.FromHex("\t#FF0000\n"));
        Assert.Equal(expected, Rgba32.FromHex("#FF0000"));
    }

    /// <summary>
    /// Шесть разрядов без альфы читаются как <c>#RRGGBB</c>. Прежний код на
    /// <c>«#FF0000 »</c> давал <c>#FF0000FF</c>, и доктринальный вывод
    /// «альфа пропала» был неверен: альфа не пропала, а появилась из ничего
    /// после обрезки, потому что осталось шесть разрядов.
    /// </summary>
    [Fact]
    public void FromHex_SixDigitsMeanOpaqueAlpha()
    {
        Assert.Equal(1f, Rgba32.FromHex("#FF0000").A);
        Assert.Equal(1f, Rgba32.FromHex("#FF0000 ").A);
        Assert.Equal(0x80 / 255f, Rgba32.FromHex("#00FF0080").A, 1e-6f);
    }

    /// <summary>
    /// Неразряд отвергается. Проверка обязана быть различающей: до правки
    /// белые символы разрядами считались.
    /// </summary>
    [Theory]
    [InlineData("#GG0000")]
    [InlineData("#00FF0")]
    [InlineData("#1234567")]
    [InlineData("#")]
    [InlineData("не цвет")]
    public void FromHex_RejectsNonDigits(string text)
    {
        Assert.Throws<FormatException>(() => Rgba32.FromHex(text));
    }

    /// <summary>
    /// Разбор в обе стороны: то, что метод напечатал, он же и читает.
    /// </summary>
    /// <remarks>
    /// Сравниваются строки, а не структуры: <c>ToString</c> печатает каналы
    /// байтами, и обратное чтение возвращает <c>байт / 255</c>. Для канала,
    /// выбранного произвольно, это другая величина, и сравнение структур
    /// проверяло бы квантование, а не разбор. Квантование закреплено отдельно,
    /// в <c>RandomAndColorTests</c>.
    /// </remarks>
    [Fact]
    public void FromHex_RoundTripsItsOwnOutput()
    {
        foreach (Rgba32 color in new[]
        {
            new Rgba32(1f, 0f, 0f, 1f),
            new Rgba32(0f, 0.5f, 1f, 0.25f),
            new Rgba32(0.2f, 0.4f, 0.6f, 0.8f),
        })
        {
            string text = color.ToString();
            Assert.Equal(text, Rgba32.FromHex(text).ToString());
        }
    }

    // =================================================================
    // Interpolation: Repeat, период как полуинтервал
    // =================================================================

    /// <summary>
    /// Период — <c>[0; length)</c>, и ни один конечный вход не должен из него
    /// выходить. Проверяется на 50 000 псевдослучайных пар, где величины
    /// берутся независимо: произведение длины на отношение само переполняется
    /// и все интересные случаи отфильтровываются.
    /// </summary>
    [Fact]
    public void Repeat_StaysInsidePeriodOnFiniteInputs()
    {
        XorShift64Star rng = new(0x51A1u);
        int total = 0;

        for (int i = 0; i < 50000; i++)
        {
            float length = WideFloat(ref rng, -30f, 30f);
            float value = WideFloat(ref rng, -38.4f, 38.4f);
            if (!(length > 0f) || !float.IsFinite(value))
            {
                continue;
            }

            total++;
            float result = Interpolation.Repeat(value, length);
            Assert.True(
                float.IsFinite(result) && result >= 0f && result < length,
                $"Repeat({value:R}, {length:R}) = {result:R} вне [0; {length:R})");
        }

        Assert.True(total > 40000, $"Слишком мало пригодных входов: {total}");
    }

    /// <summary>
    /// Дискриминирующий случай: нечисловое значение. Прежний код делил
    /// бесконечность на длину, <c>Floor</c> возвращал бесконечность, а
    /// <c>−∞ − (−∞)</c> равнялось <c>NaN</c>, и нечисловой результат
    /// выходил наружу. Нижняя граница его не ловила: <c>NaN ≤ 0</c> ложно.
    /// </summary>
    [Theory]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NaN)]
    public void Repeat_NonFiniteValueStaysInsidePeriod(float value)
    {
        float result = Interpolation.Repeat(value, 1.3205366e28f);

        Assert.True(
            float.IsFinite(result) && result >= 0f && result < 1.3205366e28f,
            $"Repeat({value:R}, 1.3205366E+28) = {result:R}");
    }

    /// <summary>
    /// Точный остаток от деления мантисс, а не «почти точно» в double: на
    /// величинах порядка 1e-30 в float уже ничего не различает.
    /// </summary>
    [Fact]
    public void Repeat_IsBitExactAgainstIntegerRemainder()
    {
        XorShift64Star rng = new(0x51A2u);
        int checkedCount = 0;

        for (int i = 0; i < 20000; i++)
        {
            float length = WideFloat(ref rng, -30f, 30f);
            double ratio = 1e-8 + (rng.NextFloat() * 2e8);
            if (!(length > 0f))
            {
                continue;
            }

            float value = (float)((double)length * ratio);
            if (!float.IsFinite(value))
            {
                continue;
            }

            (BigInteger exact, int exponent) = ExactRemainder(value, length);
            BigInteger got = ScaledTo(Interpolation.Repeat(value, length), exponent);
            Assert.True(got == exact, $"Repeat({value:R}, {length:R}) расходится с точным остатком");
            checkedCount++;
        }

        Assert.True(checkedCount > 15000, $"Слишком мало пригодных входов: {checkedCount}");
    }

    /// <summary>
    /// Граница точности измерена: до отношения 1e8 остаток побитово точен.
    /// </summary>
    [Fact]
    public void Repeat_StaysExactUpToRatioOfAHundredMillion()
    {
        XorShift64Star rng = new(0x51A3u);
        int checkedCount = 0;

        for (int i = 0; i < 20000; i++)
        {
            float length = WideFloat(ref rng, -20f, 20f);
            double ratio = (rng.NextFloat() * 2.0 - 1.0) * 1e8;
            if (!(length > 0f))
            {
                continue;
            }

            float value = (float)((double)length * ratio);
            if (!float.IsFinite(value))
            {
                continue;
            }

            (BigInteger exact, int exponent) = ExactRemainder(value, length);
            BigInteger got = ScaledTo(Interpolation.Repeat(value, length), exponent);
            Assert.True(got == exact, $"Repeat({value:R}, {length:R}) расходится с точным остатком");
            checkedCount++;
        }

        Assert.True(checkedCount > 15000, $"Слишком мало пригодных входов: {checkedCount}");
    }

    /// <summary>
    /// Контроль: значение в периоде повторяет само себя, то есть
    /// <c>Repeat(Repeat(v, l), l) = Repeat(v, l)</c>.
    /// </summary>
    [Fact]
    public void Repeat_IsIdempotentInsidePeriod()
    {
        foreach (float length in new[] { 1f, 0.5f, 7f, 1e-7f, 1e7f })
        {
            foreach (float value in new[] { 0f, 0.25f, 2.75f, -3.5f, 12345.678f })
            {
                float once = Interpolation.Repeat(value, length);
                float twice = Interpolation.Repeat(once, length);
                Assert.Equal(once, twice);
            }
        }
    }

    // =================================================================
    // Interpolation: InverseLerp, вычитание переполняется
    // =================================================================

    /// <summary>
    /// Вычитание во <see cref="float"/> переполняется раньше деления: на
    /// диапазоне <c>−3e38 … 3e38</c> ширина равна 6e38 и не помещается.
    /// Прежний код делил <c>inf / inf</c> и отдавал ноль вместо половины.
    /// </summary>
    [Theory]
    [InlineData(-3e38f, 3e38f, 0f)]
    [InlineData(-3e38f, 3e38f, 1f)]
    [InlineData(-float.MaxValue, float.MaxValue, 0f)]
    [InlineData(float.MaxValue, -float.MaxValue, 0f)]
    [InlineData(-1.7e38f, 1.7e38f, 0f)]
    public void InverseLerp_WideRangeAgreesWithDoubleReference(float from, float to, float value)
    {
        float got = Interpolation.InverseLerp(from, to, value);
        double reference = ((double)value - from) / ((double)to - from);

        Assert.True(
            float.IsFinite(got) || !double.IsFinite(reference),
            $"InverseLerp({from:R}, {to:R}, {value:R}) = {got:R}, эталон {reference:R}");
        MathAssert.Equal((float)reference, got, MathAssert.LooseTolerance);
    }

    /// <summary>
    /// Дискриминирующий случай: ровно на границе переполнения прежний код
    /// возвращал ноль, то есть уверенный и неверный ответ вместо
    /// неопределённого. Середина диапазона обязана давать половину.
    /// </summary>
    [Fact]
    public void InverseLerp_MidpointOfWidestRangeIsHalf()
    {
        Assert.Equal(0.5f, Interpolation.InverseLerp(-3e38f, 3e38f, 0f));
        Assert.Equal(0.5f, Interpolation.InverseLerp(float.MaxValue, -float.MaxValue, 0f));
    }

    /// <summary>
    /// Контроль без дефекта: обычные диапазоны не изменились.
    /// </summary>
    [Fact]
    public void InverseLerp_OnOrdinaryRangesIsUnchanged()
    {
        Assert.Equal(0.5f, Interpolation.InverseLerp(1f, 2f, 1.5f));
        Assert.Equal(0f, Interpolation.InverseLerp(1f, 2f, 1f));
        Assert.Equal(1f, Interpolation.InverseLerp(1f, 2f, 2f));
        Assert.Equal(0.5f, Interpolation.InverseLerp(-1e-30f, 1e-30f, 0f));
        Assert.Equal(0.75f, Interpolation.InverseLerp(-1e-30f, 1e-30f, 5e-31f));
    }

    /// <summary>
    /// Широкий диапазон не должен давать неверный вердикт и на случайной
    /// выборке: сверка с эталоном в двойной точности.
    /// </summary>
    [Fact]
    public void InverseLerp_WideRangeSampleAgreesWithReference()
    {
        XorShift64Star rng = new(0x51A4u);
        int total = 0;

        for (int i = 0; i < 20000; i++)
        {
            float half = WideFloat(ref rng, 30f, 38.4f);
            if (!float.IsFinite(half))
            {
                continue;
            }

            float value = (float)((double)half * (rng.NextFloat() * 2.0 - 1.0));
            if (!float.IsFinite(value))
            {
                continue;
            }

            total++;
            float got = Interpolation.InverseLerp(-half, half, value);
            float reference = (float)(((double)value + half) / ((double)half + half));

            Assert.True(
                MathF.Abs(got - reference) <= 4e-7f,
                $"InverseLerp(-{half:R}, {half:R}, {value:R}) = {got:R}, эталон {reference:R}");
        }

        Assert.True(total > 15000, $"Слишком мало пригодных входов: {total}");
    }

    // =================================================================
    // Interpolation: SmoothDamp при нулевом пределе скорости
    // =================================================================

    /// <summary>
    /// <c>maxSpeed = 0</c> означает «не двигаться». Прежний код обнулял
    /// ограничение изменения, но оставлял член <c>velocity · dt</c>, и значение
    /// продолжало ехать по накопленной скорости.
    /// </summary>
    [Fact]
    public void SmoothDamp_ZeroMaxSpeedDoesNotMove()
    {
        int moved = 0;
        for (int i = 0; i < 2000; i++)
        {
            float velocity = (i * 0.007f) - 7f;
            float result = Interpolation.SmoothDamp(0f, 10f, 0.3f, 0f, 0.1f, ref velocity);

            if (result != 0f)
            {
                moved++;
            }

            Assert.Equal(0f, velocity);
        }

        Assert.Equal(0, moved);
    }

    /// <summary>
    /// Дискриминирующий случай с ненулевой накопленной скоростью: прежний код
    /// на нём двигал сильнее всего.
    /// </summary>
    [Fact]
    public void SmoothDamp_ZeroMaxSpeedDiscardsStoredVelocity()
    {
        float velocity = 5f;
        float result = Interpolation.SmoothDamp(0f, 10f, 0.3f, 0f, 0.1f, ref velocity);

        Assert.Equal(0f, result);
        Assert.Equal(0f, velocity);
    }

    /// <summary>
    /// Контроль без дефекта: при положительном пределе сглаживание работает как
    /// прежде, то есть нулевая скорость не «залипает».
    /// </summary>
    [Fact]
    public void SmoothDamp_PositiveMaxSpeedStillMoves()
    {
        float velocity = 0f;
        float result = Interpolation.SmoothDamp(0f, 10f, 0.3f, float.PositiveInfinity, 0.1f, ref velocity);

        Assert.True(result > 0f, "Ненулевой предел обязан двигать значение.");
    }

    // =================================================================
    // Random: NextRange и NextInside
    // =================================================================

    /// <summary>
    /// Диапазон <c>[min; max)</c> по построению не получается: округление
    /// сложения может дать ровно <c>max</c>. На парах соседних <see cref="float"/>
    /// доля нарушений на прежнем коде измерена в 49.95 %.
    /// </summary>
    [Fact]
    public void NextRange_NeverReturnsUpperBound()
    {
        XorShift64Star rng = new(0x51A5u);
        int total = 0;

        for (int i = 0; i < 50000; i++)
        {
            float min = WideFloat(ref rng, -7f, 7f);
            if (!float.IsFinite(min))
            {
                continue;
            }

            float max = MathF.BitIncrement(min);
            total++;

            float value = rng.NextRange(min, max);
            Assert.True(
                value >= min && value < max,
                $"NextRange({min:R}, {max:R}) = {value:R} вне [{min:R}; {max:R})");
        }

        Assert.True(total > 40000, $"Слишком мало пригодных входов: {total}");
    }

    /// <summary>
    /// Контроль: диапазон, который шире одного разряда, тоже держит границу.
    /// </summary>
    [Fact]
    public void NextRange_HoldsBoundsOnOrdinaryRanges()
    {
        XorShift64Star rng = new(0x51A6u);
        for (int i = 0; i < 50000; i++)
        {
            float min = (rng.NextFloat() * 20f) - 10f;
            float max = min + (rng.NextFloat() * 5f) + 0.01f;

            float value = rng.NextRange(min, max);
            Assert.True(value >= min && value < max, $"NextRange({min:R}, {max:R}) = {value:R}");
        }
    }

    /// <summary>
    /// Вырожденный диапазон возвращает свою единственную точку, а не
    /// величину на разряд ниже границы, которой нет.
    /// </summary>
    [Fact]
    public void NextRange_DegenerateRangeReturnsItsOnlyPoint()
    {
        XorShift64Star rng = new(0x51A7u);
        foreach (float point in new[] { 0f, 1f, -3.5f, 1e7f })
        {
            float value = rng.NextRange(point, point);
            Assert.Equal(point, value);
        }
    }

    /// <summary>
    /// Точка внутри AABB лежит в прямоугольнике по обеим осям, включая
    /// вырожденные и соседние границы.
    /// </summary>
    [Fact]
    public void NextInside_StaysInsideBounds()
    {
        XorShift64Star rng = new(0x51A8u);
        int total = 0;

        for (int i = 0; i < 20000; i++)
        {
            float minX = WideFloat(ref rng, -7f, 7f);
            float minY = WideFloat(ref rng, -7f, 7f);
            if (!float.IsFinite(minX) || !float.IsFinite(minY))
            {
                continue;
            }

            // Ширина в один разряд — самый трудный случай для округления.
            Aabb2 bounds = new(
                new Vector2(minX, minY),
                new Vector2(MathF.BitIncrement(minX), MathF.BitIncrement(minY)));
            total++;

            Vector2 point = rng.NextInside(bounds);
            Assert.True(point.X >= bounds.Min.X && point.X < bounds.Max.X, $"X = {point.X:R} вне AABB");
            Assert.True(point.Y >= bounds.Min.Y && point.Y < bounds.Max.Y, $"Y = {point.Y:R} вне AABB");
        }

        Assert.True(total > 15000, $"Слишком мало пригодных входов: {total}");
    }

    // =================================================================
    // Transform: кватернионы
    // =================================================================

    /// <summary>
    /// Гейт <c>|q|² ≤ 1e-12</c> отвергал всякий ненулевой кватернион короче
    /// миллиона. Обратный элемент у ненулевого кватерниона существует, и
    /// отказ на нём нарушал контракт метода.
    /// </summary>
    [Theory]
    [InlineData(1e-6f)]
    [InlineData(1e-7f)]
    [InlineData(1e-19f)]
    [InlineData(1e-30f)]
    public void Inverse_AcceptsNonZeroQuaternionOfAnyLength(float multiplier)
    {
        Quaternion q = new(0.3f * multiplier, 0.4f * multiplier, 0.2f * multiplier, 0.5f * multiplier);

        // Признак ненулевого здесь — компонента, а не квадрат длины: на
        // |q| < 1e-19 квадрат обнуляется, и проверка LengthSquared дала бы
        // ложный отказ на ненулевом кватернионе.
        Assert.True(
            MathF.Max(
                MathF.Abs(q.X),
                MathF.Max(MathF.Abs(q.Y), MathF.Max(MathF.Abs(q.Z), MathF.Abs(q.W)))) > 0f,
            "Кватернион должен быть ненулевым.");

        Quaternion inverse = q.Inverse();
        Quaternion product = q * inverse;

        MathAssert.Equal(1f, product.W, 1e-3f);
        MathAssert.Equal(0f, product.X, 1e-3f);
        MathAssert.Equal(0f, product.Y, 1e-3f);
        MathAssert.Equal(0f, product.Z, 1e-3f);
    }

    /// <summary>
    /// Второй край того же гейта — переполнение. При <c>|q| = 1e20</c> квадрат
    /// длины не помещается во <see cref="float"/>, и прежний код делил на
    /// бесконечность, то есть возвращал нулевой кватернион.
    /// </summary>
    [Theory]
    [InlineData(1e20f)]
    [InlineData(1e30f)]
    public void Inverse_HandlesQuaternionWhoseLengthSquaredOverflows(float magnitude)
    {
        Quaternion q = new(0f, 0f, 0f, magnitude);
        Quaternion product = q * q.Inverse();

        MathAssert.Equal(1f, product.W, 1e-5f);
    }

    /// <summary>
    /// Настоящий ноль отвергается по-прежнему: обратного элемента у него нет.
    /// </summary>
    [Fact]
    public void Inverse_StillRejectsZeroQuaternion()
    {
        Assert.Throws<ArgumentException>(() => QuaternionExtensions.Inverse(default(Quaternion)));
        Assert.Throws<ArgumentException>(() => QuaternionExtensions.Inverse(Quaternion.Zero));
    }

    /// <summary>
    /// Контроль: точно единичный кватернион обращается сопряжением побитово.
    /// </summary>
    /// <remarks>
    /// Именно точно единичный. Кватернион, собранный из углов, единичен лишь
    /// приближённо, и деление на его квадрат длины меняет младшие разряды —
    /// это ожидаемо, а не дефект. Поэтому побитовое равенство проверяется на
    /// <see cref="Quaternion.Identity"/>, где квадрат длины равен ровно единице.
    /// </remarks>
    [Fact]
    public void Inverse_OfUnitQuaternionIsConjugate()
    {
        Quaternion inverse = Quaternion.Identity.Inverse();

        Assert.Equal(0f, inverse.X);
        Assert.Equal(0f, inverse.Y);
        Assert.Equal(0f, inverse.Z);
        Assert.Equal(1f, inverse.W);

        Quaternion rotation = QuaternionExtensions.FromEuler(
            Angle.FromDegrees(35f),
            Angle.FromDegrees(-12f),
            Angle.FromDegrees(70f));
        Quaternion approx = rotation.Inverse();
        MathAssert.Equal(-rotation.X, approx.X, 1e-6f);
        MathAssert.Equal(-rotation.Y, approx.Y, 1e-6f);
        MathAssert.Equal(-rotation.Z, approx.Z, 1e-6f);
        MathAssert.Equal(rotation.W, approx.W, 1e-6f);
    }

    /// <summary>
    /// Угол между ориентациями не зависит от общего множителя: прежний гейт
    /// возвращал угол ненулевой ориентации как угол в 49.19°.
    /// </summary>
    [Fact]
    public void AngleBetween_IsIndependentOfCommonMagnitude()
    {
        Quaternion rotation = QuaternionExtensions.FromEuler(
            Angle.FromDegrees(20f),
            Angle.FromDegrees(30f),
            Angle.FromDegrees(40f));

        foreach (float multiplier in new[] { 1f, 1e-3f, 1e-6f, 1e-7f, 1e-19f, 1e-30f })
        {
            Quaternion scaled = new(
                rotation.X * multiplier,
                rotation.Y * multiplier,
                rotation.Z * multiplier,
                rotation.W * multiplier);

            Angle angle = QuaternionExtensions.AngleBetween(scaled, scaled);
            Assert.True(
                angle.Degrees < 1e-3,
                $"AngleBetween при множителе {multiplier:R} дал {angle.Degrees:F4} градуса вместо нуля");
        }
    }

    /// <summary>
    /// Разбор Эйлера тоже не должен зависеть от общего множителя.
    /// </summary>
    [Fact]
    public void ToEuler_IsIndependentOfCommonMagnitude()
    {
        Quaternion rotation = QuaternionExtensions.FromEuler(
            Angle.FromDegrees(20f),
            Angle.FromDegrees(30f),
            Angle.FromDegrees(40f));

        foreach (float multiplier in new[] { 1e-6f, 1e-7f, 1e-30f })
        {
            Quaternion scaled = new(
                rotation.X * multiplier,
                rotation.Y * multiplier,
                rotation.Z * multiplier,
                rotation.W * multiplier);

            (Angle yaw, Angle pitch, Angle roll) = QuaternionExtensions.ToEuler(scaled);

            MathAssert.Equal(Angle.FromDegrees(20f), yaw, 1e-2);
            MathAssert.Equal(Angle.FromDegrees(30f), pitch, 1e-2);
            MathAssert.Equal(Angle.FromDegrees(40f), roll, 1e-2);
        }
    }

    // =================================================================
    // Transform: разбор матрицы
    // =================================================================

    /// <summary>
    /// Гейт <c>scale ≤ 1e-6</c> отбрасывал любой ненулевой масштаб меньше
    /// миллиона и подставлял единицу, то есть оставлял строку неделимой.
    /// </summary>
    [Theory]
    [InlineData(1e-6f)]
    [InlineData(1e-7f)]
    [InlineData(1e-12f)]
    [InlineData(1e-30f)]
    public void GetRotation_AcceptsNonZeroScaleOfAnyMagnitude(float scale)
    {
        Quaternion rotation = QuaternionExtensions.FromEuler(
            Angle.FromDegrees(20f),
            Angle.FromDegrees(30f),
            Angle.FromDegrees(40f));
        Matrix4x4 matrix = Matrix4x4Extensions.CreateTRS(
            Vector3.Zero,
            rotation,
            new Vector3(scale, scale, scale));

        Quaternion restored = matrix.GetRotation();
        double error = QuaternionExtensions.AngleBetween(rotation, restored).Degrees;

        Assert.True(
            error < 1e-2,
            $"GetRotation при масштабе {scale:R} разошёлся на {error:F4} градуса");
    }

    /// <summary>
    /// Дискриминирующий случай: прежний разбор при масштабе 1e-6 возвращал
    /// |q| = 0.5 и нулевые углы.
    /// </summary>
    [Fact]
    public void GetRotation_DoesNotSubstituteOneForSmallScale()
    {
        Quaternion rotation = QuaternionExtensions.FromEuler(
            Angle.FromDegrees(20f),
            Angle.FromDegrees(30f),
            Angle.FromDegrees(40f));
        Matrix4x4 matrix = Matrix4x4Extensions.CreateTRS(
            Vector3.Zero,
            rotation,
            new Vector3(1e-6f, 1e-6f, 1e-6f));

        Quaternion restored = matrix.GetRotation();
        MathAssert.Equal(1f, restored.Length(), 1e-4f);
    }

    /// <summary>
    /// <c>GetScale</c> обещает длины базовых векторов, и на матрице с
    /// ненулевыми, но крошечными элементами квадрат элемента обнулялся.
    /// </summary>
    [Theory]
    [InlineData(1e-22f)]
    [InlineData(1e-30f)]
    public void GetScale_DoesNotCollapseOnTinyMatrix(float scale)
    {
        Matrix4x4 matrix = Matrix4x4.CreateScale(scale);
        Vector3 got = matrix.GetScale();

        Assert.True(got.X > 0f && got.Y > 0f && got.Z > 0f, $"GetScale вернул {got} при масштабе {scale:R}");
        MathAssert.Equal(scale, got.X, scale * 0.05f);
    }

    /// <summary>
    /// Нулевая ось по-прежнему не отвергается: поворот не определяется, и
    /// метод возвращает то, что может.
    /// </summary>
    [Fact]
    public void GetRotation_SurvivesZeroAxis()
    {
        Quaternion rotation = QuaternionExtensions.FromEuler(
            Angle.FromDegrees(20f),
            Angle.FromDegrees(30f),
            Angle.FromDegrees(40f));
        Matrix4x4 matrix = Matrix4x4Extensions.CreateTRS(
            Vector3.Zero,
            rotation,
            new Vector3(0f, 1f, 1f));

        _ = matrix.GetRotation();
    }

    // =================================================================
    // Transform: нормаль после обратной транспонировки
    // =================================================================

    /// <summary>
    /// Проверка стояла на определителе, а умножается на числитель обратный
    /// множитель. Определитель может быть ненулевым и давать бесконечный
    /// множитель (переполнение) либо ненормальным и давать бесконечный
    /// множитель снизу. Прежний код пропускал оба и возвращал нулевую
    /// нормаль.
    /// </summary>
    [Theory]
    [InlineData(1e13f)]
    [InlineData(1e14f)]
    [InlineData(1e19f)]
    [InlineData(1e30f)]
    [InlineData(1e-14f)]
    public void TransformNormal_RejectsMatrixWithoutFloatInverse(float scale)
    {
        Matrix4x4 matrix = Matrix4x4.CreateScale(scale);

        Assert.Throws<InvalidOperationException>(() => matrix.TransformNormal(Vector3.UnitX));
    }

    /// <summary>
    /// Дискриминирующий случай: при масштабе 1e-13 определитель равен 1e-39,
    /// то есть ненормален, и обратная ему величина переполняется. Прежняя
    /// проверка <c>determinant == 0 || IsNaN</c> это пропускала.
    /// </summary>
    [Fact]
    public void TransformNormal_RejectsSubnormalDeterminant()
    {
        Matrix4x4 matrix = Matrix4x4.CreateScale(1e-13f);

        Assert.Throws<InvalidOperationException>(() => matrix.TransformNormal(Vector3.UnitX));
    }

    /// <summary>
    /// Вырожденная матрица отвергается по-прежнему.
    /// </summary>
    [Fact]
    public void TransformNormal_StillRejectsSingularMatrix()
    {
        Matrix4x4 singular = Matrix4x4.CreateScale(0f, 0f, 0f);
        Assert.Throws<InvalidOperationException>(() => singular.TransformNormal(Vector3.UnitY));
    }

    /// <summary>
    /// Контроль без дефекта: нормальный масштаб работает как прежде, и при
    /// зеркальном определителе знак не отвергается.
    /// </summary>
    [Fact]
    public void TransformNormal_KeepsWorkingOnOrdinaryAndMirroredScales()
    {
        // Нижняя граница здесь не 1e-38, а примерно 2.2e-13: определитель
        // равен кубу масштаба, и обратная ему величина должна помещаться в
        // float. На 1e-13 определитель ещё ненормален и обратный множитель
        // переполняется — это проверяется отдельным тестом выше.
        foreach (float scale in new[] { 1f, 2f, 1e-6f, 1e-12f, -1f, -3f })
        {
            Matrix4x4 matrix = Matrix4x4.CreateScale(scale);
            Vector3 normal = matrix.TransformNormal(Vector3.UnitX);

            Assert.True(
                float.IsFinite(normal.X) && normal.X != 0f,
                $"TransformNormal при масштабе {scale:R} вернула {normal}");
        }
    }

    // =================================================================
    // Эталон: независимый, без обращения к проверяемому коду
    // =================================================================

    /// <summary>
    /// Конечный <see cref="float"/> заданной десятичной величины. Берётся как
    /// <c>10^показатель</c> с мантиссой, а не умножением длины на отношение:
    /// произведение само переполняется и все интересные случаи отфильтровываются.
    /// </summary>
    private static float WideFloat(ref XorShift64Star rng, float minExponent, float maxExponent)
    {
        double exponent = minExponent + ((maxExponent - minExponent) * rng.NextFloat());
        double value = Math.Pow(10.0, exponent) * (1.0 + (rng.NextFloat() * 9.0));
        if (!double.IsFinite(value) || value > float.MaxValue)
        {
            return float.NaN;
        }

        return (float)value;
    }

    /// <summary>
    /// Раскладывает <see cref="float"/> на знак, нечётную мантиссу и
    /// показатель: значение равно <c>sign · mantissa · 2^exponent</c>.
    /// </summary>
    private static (BigInteger Mantissa, int Exponent) Decompose(float value)
    {
        int bits = BitConverter.SingleToInt32Bits(value);
        bool negative = (bits & unchecked((int)0x80000000)) != 0;
        int biased = (bits >> 23) & 0xFF;
        int fraction = bits & 0x7FFFFF;

        BigInteger mantissa;
        int exponent;
        if (biased == 0)
        {
            mantissa = fraction;
            exponent = -149;
        }
        else
        {
            mantissa = fraction | 0x800000;
            exponent = biased - 127 - 23;
        }

        return negative ? (-mantissa, exponent) : (mantissa, exponent);
    }

    /// <summary>
    /// Точный остаток <c>value mod length</c> целочисленно: никакого
    /// промежуточного округления, в отличие от вычисления в <see cref="double"/>.
    /// </summary>
    private static (BigInteger Mantissa, int Exponent) ExactRemainder(float value, float length)
    {
        (BigInteger valueMantissa, int valueExponent) = Decompose(value);
        (BigInteger lengthMantissa, int lengthExponent) = Decompose(length);

        int shift = valueExponent - lengthExponent;
        BigInteger numerator = shift >= 0 ? valueMantissa << shift : valueMantissa;
        BigInteger denominator = shift >= 0 ? lengthMantissa : lengthMantissa << -shift;

        BigInteger quotient = BigInteger.DivRem(numerator, denominator, out BigInteger rest);
        if (numerator.Sign < 0 && !rest.IsZero)
        {
            quotient -= 1;
            rest += denominator;
        }

        int exponent = Math.Min(valueExponent, lengthExponent);
        return ((valueMantissa << (valueExponent - exponent))
            - (quotient * (lengthMantissa << (lengthExponent - exponent))), exponent);
    }

    /// <summary>
    /// Приводит <see cref="float"/> к целому в основании <paramref name="exponent"/>,
    /// чтобы сравнить с точным остатком без потери и без округления.
    /// </summary>
    private static BigInteger ScaledTo(float value, int exponent)
    {
        (BigInteger mantissa, int valueExponent) = Decompose(value);
        return mantissa << (valueExponent - exponent);
    }
}