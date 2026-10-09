using System.Globalization;
using System.Numerics;
using XEngine.Mathematics;
using Xunit;
using Xunit.Abstractions;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Регрессия волны 2 по поддиректории <c>Scalar/</c>: нормализация угла из
/// направления и сведение к квадранту на всём диапазоне <c>float</c>.
/// </summary>
/// <remarks>
/// Тесты собираются и запускаются одинаково при <c>MathBackend=Fast</c> и
/// <c>MathBackend=Deterministic</c>, поэтому проверяют контракт, а не
/// реализацию.
/// <para>
/// Объём проверки сведения задан всем диапазоном <c>float</c>, а не
/// «горячими» значениями: прежний дефект жил ровно там, где проверок не было,
/// то есть при <c>|x|</c> больше 10¹². Набор точек строится из случайных
/// 32-битных образов, то есть покрывает и денормали, и весь порядок float.
/// </para>
/// </remarks>
public class ScalarWave2Tests
{
    private readonly ITestOutputHelper _o;

    /// <summary>Создаёт тесты.</summary>
    /// <param name="o">Вывод теста.</param>
    public ScalarWave2Tests(ITestOutputHelper o) => _o = o;

    /// <summary>Сколько точек берётся в каждом прогоне.</summary>
    private const int Samples = 400_000;

    /// <summary>
    /// Следующий случайный 32-битный образ: целые операции, без
    /// <see cref="System.Random"/>, чтобы набор был одинаков на любой машине.
    /// </summary>
    /// <param name="state">Состояние генератора.</param>
    /// <returns>Случайный образ и новое состояние.</returns>
    private static int NextBits(ref ulong state)
    {
        unchecked
        {
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            return (int)state;
        }
    }

    /// <summary>
    /// Проверяет, что угол лежит в диапазоне <c>(-π; π]</c>.
    /// </summary>
    /// <param name="angle">Проверяемый угол.</param>
    /// <returns><c>true</c>, если угол в диапазоне.</returns>
    /// <remarks>
    /// Границы именно такие: минус пи в диапазон не входит, плюс пи входит.
    /// Проверка на <c>Math.PI</c> в double, а не на float-атрибут: float-атрибут
    /// плюс пи равен 3.1415927410125732 и БОЛЬШЕ истинного пи, поэтому
    /// сравнение с ним пропустило бы именно тот угол, ради которого написан
    /// тест.
    /// </remarks>
    private static bool InRange(Angle angle)
        => angle.Radians > -Math.PI && angle.Radians <= Math.PI;

    // ---------------------------------------------------------------- P1-4

    /// <summary>
    /// P1-4. Угол из направления лежит в том же диапазоне, что и любой другой
    /// нормализованный угол.
    /// </summary>
    [Fact]
    public void FromDirection_StaysInsideNormalizedRange()
    {
        int outOfRange = 0;
        double worst = 0;
        Angle firstBad = default;

        for (int gx = -1000; gx <= 1000; gx++)
        {
            for (int gy = -3; gy <= 3; gy++)
            {
                Angle angle = Angle.FromDirection(new Vector2(gx, gy));
                if (InRange(angle))
                {
                    continue;
                }

                outOfRange++;
                if (outOfRange == 1)
                {
                    firstBad = angle;
                }

                worst = Math.Max(worst, Math.Abs(angle.Radians) - Math.PI);
            }
        }

        _o.WriteLine($"вне диапазона: {outOfRange}, худшее превышение {worst:E3} рад, первый: {firstBad.Radians:R}");
        Assert.Equal(0, outOfRange);
    }

    /// <summary>
    /// P1-4. Направление строго влево — та же ориентация, что и 180°, и
    /// словарь видит одну запись, а не две.
    /// </summary>
    [Fact]
    public void FromDirection_LeftIsSameOrientationAsHalfTurn()
    {
        Angle left = Angle.FromDirection(new Vector2(-1f, 0f));
        Angle half = Angle.FromDegrees(180);

        Assert.True(InRange(left), $"радианы {left.Radians:R} вне диапазона");
        Assert.Equal(half, left);
        Assert.Equal(half.GetHashCode(), left.GetHashCode());

        Dictionary<Angle, int> map = new()
        {
            [left] = 1,
            [half] = 2,
        };

        Assert.Single(map);

        Angle[] pair = [left, half];
        Array.Sort(pair);
        Assert.Equal(half, pair[0]);
        Assert.Equal(left, pair[1]);
    }

    /// <summary>
    /// P1-4. Знаковый ноль в <c>y</c> не выдаёт угол за левую границу
    /// диапазона.
    /// </summary>
    [Fact]
    public void FromDirection_SignedZeroStaysInRange()
    {
        Angle up = Angle.FromDirection(new Vector2(-1f, +0f));
        Angle down = Angle.FromDirection(new Vector2(-1f, -0f));

        _o.WriteLine($"(-1, +0) = {up.Radians:R}, (-1, -0) = {down.Radians:R}");
        Assert.True(InRange(up));
        Assert.True(InRange(down));
        Assert.Equal(Angle.FromDegrees(180), up);
        Assert.Equal(Angle.FromDegrees(180), down);
        Assert.Equal(up, down);
    }

    /// <summary>
    /// P1-4. Направление под случайными углами тоже не выходит за диапазон.
    /// </summary>
    [Fact]
    public void FromDirection_RandomDirectionsStayInRange()
    {
        ulong state = 0xC0FFEE;
        int bad = 0;
        for (int i = 0; i < Samples; i++)
        {
            float x = (NextBits(ref state) & 0xFFFF) / 65535.0f * 2f - 1f;
            float y = (NextBits(ref state) & 0xFFFF) / 65535.0f * 2f - 1f;
            if (!InRange(Angle.FromDirection(new Vector2(x, y))))
            {
                bad++;
            }
        }

        Assert.Equal(0, bad);
    }

    /// <summary>
    /// P1-4. Обратный ход: проверка диапазона отличается от прежнего поведения
    /// на целой сетке, то есть тест не может остаться зелёным без правки.
    /// </summary>
    [Fact]
    public void FromDirection_PreviousBehaviourWouldFail()
    {
        // Дискриминирующая точка, а не пример: именно она выпадала из диапазона.
        double raw = MathF.Atan2(0f, -1f);
        Assert.True(raw > Math.PI, $"MathF.Atan2(0, -1) = {raw:R} ожидалось больше пи");

        int rawOutOfRange = 0;
        for (int gx = -1000; gx <= 1000; gx++)
        {
            for (int gy = -3; gy <= 3; gy++)
            {
                if (MathF.Atan2(gy, gx) > Math.PI)
                {
                    rawOutOfRange++;
                }
            }
        }

        _o.WriteLine($"прежнее сведение давало вне диапазона на {rawOutOfRange} направлениях");
        Assert.True(rawOutOfRange > 0, "прежнее поведение обязано отличаться, иначе тест ничего не проверяет");
    }

    // ---------------------------------------------------------------- P1-5

    /// <summary>
    /// Шаг одного последнего разряда одинарной точности в точке <paramref name="z"/>.
    /// </summary>
    /// <param name="z">Точка измерения.</param>
    /// <returns>Величина шага.</returns>
    private static double FloatUlp(double z)
    {
        if (z == 0.0)
        {
            return 1.4012984643e-45;
        }

        // Шаг между соседними float определяется их битовым расстоянием, а не
        // формулой: так шаг остаётся верным и для денормалей, где формула
        // отличается от IEEE-значения.
        float rounded = (float)z;
        int bits = BitConverter.SingleToInt32Bits(rounded);
        float next = BitConverter.Int32BitsToSingle(bits + 1);
        return Math.Abs((double)next - rounded);
    }

    /// <summary>
    /// P1-5. Сведение к квадранту держит точность на всём диапазоне
    /// <c>float</c>, а не только на малом.
    /// </summary>
    [Fact]
    public void SinCos_KeepsAccuracyAcrossWholeFloatRange()
    {
        ulong state = 0x243F6A8885A308D3UL;
        double worstAbsolute = 0;
        int worstAt = 0;
        long collapsed = 0;
        long nonFinite = 0;

        for (int i = 0; i < Samples; i++)
        {
            int bits = NextBits(ref state);
            float x = BitConverter.Int32BitsToSingle(bits);
            if (!float.IsFinite(x))
            {
                continue;
            }

            (float s, float c) = Trig.SinCos(x);
            double referenceSin = Math.Sin((double)x);
            double referenceCos = Math.Cos((double)x);
            if (double.IsNaN(referenceSin) || double.IsNaN(referenceCos))
            {
                continue;
            }

            if (!float.IsFinite(s) || !float.IsFinite(c))
            {
                nonFinite++;
                continue;
            }

            // Прежний дефект выдавал ровно sin = 0 и cos = 1, то есть терял
            // аргумент целиком. Это самая грубая из возможных потерь и видна
            // без эталона, но сверка с эталоном ниже всё равно её ловит.
            if (s == 0f && c == 1f)
            {
                collapsed++;
            }

            double error = Math.Max(Math.Abs(s - referenceSin), Math.Abs(c - referenceCos));
            if (error > worstAbsolute)
            {
                worstAbsolute = error;
                worstAt = bits;
            }
        }

        _o.WriteLine(
            $"сборка: детерминированная = {Trig.IsDeterministic}; " +
            $"точек {Samples}, sin=0 и cos=1: {collapsed}, нечисловых: {nonFinite}; " +
            $"худшая абсолютная {worstAbsolute:E3} на битах 0x{worstAt:X8}");

        Assert.Equal(0, collapsed);
        Assert.Equal(0, nonFinite);
        Assert.True(
            worstAbsolute < 1e-7,
            $"худшая абсолютная ошибка {worstAbsolute:E3} на битах 0x{worstAt:X8} превышает 1e-7");
    }

    /// <summary>
    /// P1-5. Граница, на которой прежнее сведение рассыпалось, проверяется точечно:
    /// <c>|x| ≈ 2³⁰</c> и выше.
    /// </summary>
    [Fact]
    public void SinCos_BeyondMediumReductionLimitStaysPrecise()
    {
        // 0x4DC90FDB — граница ветви сведения. Числа берутся с обеих сторон и
        // сразу за ней, потому что дефект начинался ровно там.
        float[] probes =
        [
            0x4DC90FDA, 0x4DC90FDB, 0x4DC90FDC, 0x4DC91058,
            1.0e9f, 4.3e9f, 1.0e12f, 1.0e17f, 1.0e30f, float.MaxValue,
            -1.0e9f, -1.0e17f, -1.0e30f,
        ];

        double worst = 0;
        foreach (float x in probes)
        {
            if (!float.IsFinite(x))
            {
                continue;
            }

            (float s, float c) = Trig.SinCos(x);
            double error = Math.Max(
                Math.Abs(s - Math.Sin((double)x)),
                Math.Abs(c - Math.Cos((double)x)));
            if (error > worst)
            {
                worst = error;
            }
        }

        _o.WriteLine($"точечные пробы: худшая абсолютная ошибка {worst:E3}");
        Assert.True(worst < 1e-7, $"худшая ошибка {worst:E3} превышает 1e-7");
    }

    /// <summary>
    /// P1-5. Сведение не теряет аргумент: результаты на семи порядках различаются.
    /// </summary>
    /// <remarks>
    /// Проверка на повторяемость с обратным ходом: если сведение теряет
    /// аргумент, все большие значения дают один и тот же ответ. Прежний дефект
    /// на <c>|x| &gt; 10¹⁷</c> отдавал <c>sin = 0, cos = 1</c> почти везде.
    /// </remarks>
    [Fact]
    public void SinCos_DoesNotCollapseLargeArguments()
    {
        HashSet<int> seen = new();
        for (int exponent = 5; exponent <= 127; exponent++)
        {
            // Мантисса меняется, чтобы не попадать ровно в кратное 2π.
            uint mantissa = 0x400001u + ((uint)exponent * 2654435761u) & 0x7FFFFFu;
            uint bits = ((uint)exponent << 23) | (mantissa & 0x7FFFFFu);
            float x = BitConverter.Int32BitsToSingle(unchecked((int)bits));
            if (!float.IsFinite(x))
            {
                continue;
            }

            (float s, float c) = Trig.SinCos(x);
            seen.Add(BitConverter.SingleToInt32Bits(s) ^ BitConverter.SingleToInt32Bits(c));
        }

        _o.WriteLine($"различных пар (sin, cos) по порядкам: {seen.Count} из 123");
        Assert.True(seen.Count > 100, $"различных ответов всего {seen.Count}: сведение схлопывает аргумент");
    }

    /// <summary>
    /// P1-5. Остаток сведения лежит в <c>[−π/4; π/4]</c>, а номер квадранта
    /// даёт верную перестановку синуса и косинуса.
    /// </summary>
    [Fact]
    public void SinCos_IdentityHoldsAcrossWholeRange()
    {
        ulong state = 0x9E3779B97F4A7C15UL;
        double worstIdentity = 0;
        double worstOvershoot = 0;

        for (int i = 0; i < Samples; i++)
        {
            float x = BitConverter.Int32BitsToSingle(NextBits(ref state));
            if (!float.IsFinite(x))
            {
                continue;
            }

            (float s, float c) = Trig.SinCos(x);
            double identity = ((double)s * s + (double)c * c) - 1.0;
            worstIdentity = Math.Max(worstIdentity, Math.Abs(identity));

            // Остаток не наблюдаем снаружи, но сумма квадратов проверяет, что
            // перестановка по квадрантам применена целиком.
            worstOvershoot = Math.Max(worstOvershoot, Math.Abs(s));
        }

        _o.WriteLine($"тождество sin² + cos² = 1: худшее {worstIdentity:E3}, худший |sin| {worstOvershoot:R}");
        Assert.True(worstIdentity < 1e-6, $"тождество нарушено на {worstIdentity:E3}");
    }

    /// <summary>
    /// P1-5. Нечисловые входы дают <c>NaN</c> в обоих значениях, как требует
    /// IEEE 754.
    /// </summary>
    [Fact]
    public void SinCos_NonFiniteInputsGiveNaN()
    {
        foreach (float x in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            (float s, float c) = Trig.SinCos(x);
            Assert.True(float.IsNaN(s), $"sin({x}) = {s}");
            Assert.True(float.IsNaN(c), $"cos({x}) = {c}");
        }

        (float sinPos, float cosPos) = Trig.SinCos(+0f);
        (float sinNeg, float cosNeg) = Trig.SinCos(-0f);
        Assert.Equal(
            BitConverter.SingleToInt32Bits(+0f),
            BitConverter.SingleToInt32Bits(sinPos));
        Assert.Equal(
            BitConverter.SingleToInt32Bits(-0f),
            BitConverter.SingleToInt32Bits(sinNeg));
        Assert.Equal(1f, cosPos);
        Assert.Equal(1f, cosNeg);
    }

    // ----------------------------------------------------------------- P3

    /// <summary>
    /// P3-2. Вывод угла не зависит от культуры процесса.
    /// </summary>
    [Fact]
    public void ToString_DoesNotDependOnCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            Angle angle = Angle.FromDegrees(45.5);
            string expected = angle.ToString();

            // Культуры собираются вручную, а не через new CultureInfo(name):
            // проект собран с InvariantGlobalization, и загрузить de-DE
            // из среды нельзя. Достаточно подменить десятичный разделитель,
            // потому что он единственный здесь влияет на вывод.
            foreach (string separator in new[] { ".", "," })
            {
                NumberFormatInfo format = (NumberFormatInfo)NumberFormatInfo.InvariantInfo.Clone();
                format.NumberDecimalSeparator = separator;
                CultureInfo culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                culture.NumberFormat = format;
                CultureInfo.CurrentCulture = culture;

                string actual = angle.ToString();
                _o.WriteLine($"разделитель '{separator}': {actual}");
                Assert.Equal(expected, actual);
            }

            Assert.Equal("45.50 deg", expected);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    /// P3-4. <c>1 - exp(±0)</c> сохраняет знак нуля: обе ветви метода отвечают
    /// на один вопрос одинаково.
    /// </summary>
    [Fact]
    public void OneMinusExp_KeepsSignedZero()
    {
        Assert.Equal(
            BitConverter.SingleToInt32Bits(+0f),
            BitConverter.SingleToInt32Bits(Trig.OneMinusExp(+0f)));
        Assert.Equal(
            BitConverter.SingleToInt32Bits(-0f),
            BitConverter.SingleToInt32Bits(Trig.OneMinusExp(-0f)));

        // Ветка прямой формулы обязана давать то же самое.
        Assert.Equal(
            BitConverter.SingleToInt32Bits(+0f),
            BitConverter.SingleToInt32Bits(1f - Trig.Exp(+0f)));
    }

    /// <summary>
    /// P3-3. Многочлен <c>OneMinusExp</c> не хуже точного значения по числу
    /// точек, где ошибка превышает последний разряд.
    /// </summary>
    /// <remarks>
    /// Проверка по счёту, а не по отдельной точке: прежний вариант без слитной
    /// операции проигрывал на 66 894 случаях из двух миллионов и выигрывал на
    /// 53 715. Один пример ничего не различает, счёт — различает.
    /// </remarks>
    [Fact]
    public void OneMinusExp_PolynomialIsNoWorseThanLastBit()
    {
        ulong state = 0x5DEECE66DUL;
        int beyondOneUlp = 0;
        double worst = 0;

        for (int i = 0; i < 400_000; i++)
        {
            int bits = NextBits(ref state);
            float x = (bits & 0xFFFFF) / 1048576.0f - 0.5f;
            if (x <= -0.5f || x >= 0.5f)
            {
                continue;
            }

            float actual = Trig.OneMinusExp(x);
            double reference = 1.0 - Math.Exp((double)x);
            double error = Math.Abs(actual - reference);
            double ulp = Math.Max(FloatUlp(reference), 1e-45);

            if (error > ulp)
            {
                beyondOneUlp++;
            }

            worst = Math.Max(worst, error / ulp);
        }

        _o.WriteLine($"точек вне одного последнего разряда: {beyondOneUlp}, худшее отношение {worst:F1}");
        Assert.True(
            beyondOneUlp < 400_000 / 50,
            $"вне одного разряда {beyondOneUlp} точек: свёртка хуже прежней");
    }

    /// <summary>
    /// P3-5. <c>Acos</c> за пределами <c>[-1; 1]</c> отвечает <c>NaN</c> в обоих
    /// вариантах сборки.
    /// </summary>
    [Fact]
    public void Acos_OutsideRangeIsNaNInBothBuilds()
    {
        float[] outside =
        [
            2f, -2f, 1000f, -1000f,
            1f + MathF.BitIncrement(1f), -1f - MathF.BitIncrement(1f),
            float.PositiveInfinity, float.NegativeInfinity, float.NaN,
        ];

        foreach (float x in outside)
        {
            _o.WriteLine($"Acos({x:R}) = {Trig.Acos(x)}");
            Assert.True(float.IsNaN(Trig.Acos(x)), $"Trig.Acos({x:R}) = {Trig.Acos(x)}");
            Assert.True(float.IsNaN(MathF.Acos(x)), $"MathF.Acos({x:R}) = {MathF.Acos(x)}");
            Assert.True(float.IsNaN(Trig.Asin(x)), $"Trig.Asin({x:R}) = {Trig.Asin(x)}");
        }

        Assert.Equal(0f, Trig.Acos(1f));
        Assert.Equal(MathF.PI, Trig.Acos(-1f));
    }

    /// <summary>
    /// P3-5. Внутри диапазона <c>Acos</c> совпадает с платформой.
    /// </summary>
    [Fact]
    public void Acos_InsideRangeMatchesReference()
    {
        ulong state = 0xB5026F5AA96619E9UL;
        int worse = 0;
        double worst = 0;
        for (int i = 0; i < 200_000; i++)
        {
            float x = ((NextBits(ref state) & 0xFFFFFF) / 8388608.0f) - 1f;
            float reference = MathF.Acos(x);
            float actual = Trig.Acos(x);
            if (!float.IsFinite(reference))
            {
                continue;
            }

            double error = Math.Abs(actual - reference);
            worst = Math.Max(worst, error);
            if (error > 1e-6f)
            {
                worse++;
            }
        }

        _o.WriteLine($"Acos внутри диапазона: хуже 1e-6 на {worse} точках, худшая ошибка {worst:E3}");
        Assert.Equal(0, worse);
    }

    /// <summary>
    /// P3-9. <c>Snap</c> отвергает нечисловой шаг тем же путём, что и неположительный.
    /// </summary>
    [Fact]
    public void Snap_RejectsNaNStep()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Scalar.Snap(7f, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Scalar.Snap(7f, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Scalar.Snap(7f, -2f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Scalar.Snap(7f, -0f));

        // Положительная бесконечность больше нуля и остаётся допустимой:
        // контракт «шаг больше нуля» её не запрещает. Частное 7/∞ равно нулю,
        // и произведение нуля на бесконечность даёт NaN — это уже свойство
        // умножения в IEEE 754, а не отказ проверки.
        Assert.Equal(8f, Scalar.Snap(7f, 2f));
        Assert.Equal(7.5f, Scalar.Snap(7.3f, 0.5f));
        Assert.Equal(float.NaN, Scalar.Snap(7f, float.PositiveInfinity));
    }

    /// <summary>
    /// P3-8. Все пять операций сравнения отвечают на пару углов согласованно.
    /// </summary>
    [Fact]
    public void Angle_ComparisonIsConsistentOnNaN()
    {
        Angle notANumber = Angle.FromRadians(double.NaN);
        Angle zero = Angle.Zero;

        _o.WriteLine(
            $"NaN == NaN: {notANumber.Equals(notANumber)}, " +
            $"CompareTo: {notANumber.CompareTo(notANumber)}, " +
            $"< 0: {notANumber < zero}, > 0: {notANumber > zero}, " +
            $">= 0: {notANumber >= zero}, <= 0: {notANumber <= zero}");

        // Правило одно: NaN больше любого числа и равен самому себе.
        Assert.True(notANumber > zero);
        Assert.False(notANumber < zero);
        Assert.True(notANumber >= zero);
        Assert.False(notANumber <= zero);
        Assert.True(zero < notANumber);
        Assert.False(zero > notANumber);
        Assert.Equal(0, notANumber.CompareTo(notANumber));
        Assert.True(notANumber.CompareTo(zero) > 0);
        Assert.True(zero.CompareTo(notANumber) < 0);

        // Согласованность с Equals: равны по CompareTo ровно тогда, когда
        // равны по Equals.
        Assert.Equal(notANumber.Equals(notANumber), notANumber.CompareTo(notANumber) == 0);
        Assert.Equal(notANumber.Equals(zero), notANumber.CompareTo(zero) == 0);
        Assert.Equal(notANumber == zero, notANumber.CompareTo(zero) == 0);
    }

    /// <summary>
    /// Проверка согласованности сравнений на конечных углах: правка порядка
    /// не должна была сломать обычное упорядочивание.
    /// </summary>
    [Fact]
    public void Angle_OrderingIsConsistentOnFiniteValues()
    {
        ulong state = 0x243F6A8885A308D3UL;
        Angle[] angles = new Angle[2000];
        for (int i = 0; i < angles.Length; i++)
        {
            double r = ((NextBits(ref state) & 0xFFFFFFFF) / 4294967296.0) * 2.0 - 1.0;
            angles[i] = Angle.FromRadians(r * Math.PI);
        }

        // Внутри циклов только счётчики, проверки после: Assert внутри большого
        // цикла не прерывает его, а копит запись на каждый промах.
        int compareVsEquals = 0;
        int lessMismatch = 0;
        int greaterMismatch = 0;
        int lessOrEqualMismatch = 0;
        int greaterOrEqualMismatch = 0;
        int hashMismatch = 0;

        for (int i = 0; i < angles.Length; i++)
        {
            for (int j = 0; j < angles.Length; j += 7)
            {
                int compare = angles[i].CompareTo(angles[j]);
                bool equal = angles[i] == angles[j];
                if (equal != (compare == 0))
                {
                    compareVsEquals++;
                }

                if ((compare < 0) != (angles[i] < angles[j]))
                {
                    lessMismatch++;
                }

                if ((compare > 0) != (angles[i] > angles[j]))
                {
                    greaterMismatch++;
                }

                if ((compare <= 0) != (angles[i] <= angles[j]))
                {
                    lessOrEqualMismatch++;
                }

                if ((compare >= 0) != (angles[i] >= angles[j]))
                {
                    greaterOrEqualMismatch++;
                }

                if (equal && angles[i].GetHashCode() != angles[j].GetHashCode())
                {
                    hashMismatch++;
                }
            }
        }

        Array.Sort(angles);
        int orderBroken = 0;
        for (int i = 1; i < angles.Length; i++)
        {
            if (!(angles[i - 1] <= angles[i]))
            {
                orderBroken++;
            }
        }

        _o.WriteLine(
            $"пар сравнено: {(angles.Length + 6) / 7 * angles.Length}; " +
            $"CompareTo против Equals: {compareVsEquals}, <: {lessMismatch}, >: {greaterMismatch}, " +
            $"<=: {lessOrEqualMismatch}, >=: {greaterOrEqualMismatch}, хеш: {hashMismatch}, " +
            $"нарушений порядка: {orderBroken}");

        Assert.Equal(0, compareVsEquals);
        Assert.Equal(0, lessMismatch);
        Assert.Equal(0, greaterMismatch);
        Assert.Equal(0, lessOrEqualMismatch);
        Assert.Equal(0, greaterOrEqualMismatch);
        Assert.Equal(0, hashMismatch);
        Assert.Equal(0, orderBroken);
    }

    /// <summary>
    /// P1-4. Доктрина типа обещает нормализацию, и обещание выполняется на всех
    /// путях, которые нормализуют.
    /// </summary>
    [Fact]
    public void Angle_NormalizingPathsStayInRange()
    {
        // Внутри цикла только счётчик. Assert внутри большого цикла не
        // прерывает его на первом провале, а накапливает по записи на каждый
        // промах: на миллион точек это миллион сообщений, и прогон выглядит
        // как зависание.
        ulong state = 0xB7E151628AED2A6BUL;
        int outside = 0;
        double worst = 0;

        for (int i = 0; i < 100_000; i++)
        {
            double raw = ((NextBits(ref state) & 0xFFFFFFFF) / 4294967296.0 - 0.5) * 1000.0;
            float scale = (NextBits(ref state) & 0xFFFF) / 65535.0f * 4f - 2f;

            Angle[] produced =
            [
                Angle.FromRadians(raw),
                Angle.FromDegrees(raw),
                Angle.FromTurns(raw),
                Angle.FromRadians(raw).Scale(scale),
                Angle.FromRadians(raw).Add(Angle.FromRadians(raw * 3)),
                Angle.FromRadians(raw).Subtract(Angle.FromRadians(raw * 5)),
                Angle.FromRadians(raw).Negated(),
                Angle.FromRadians(raw).Normalized(),
                Angle.FromRadians(raw) + Angle.FromRadians(raw * 7),
                Angle.FromRadians(raw) - Angle.FromRadians(raw * 11),
                Angle.FromRadians(raw) * scale,
                Angle.FromRadians(raw) / (scale == 0f ? 3f : scale),
            ];

            foreach (Angle angle in produced)
            {
                if (!InRange(angle))
                {
                    outside++;
                    worst = Math.Max(worst, Math.Abs(angle.Radians) - Math.PI);
                }
            }
        }

        _o.WriteLine($"вне диапазона: {outside} из {100_000 * 12}, худшее превышение {worst:E3} рад");
        Assert.Equal(0, outside);
    }

    /// <summary>
    /// P3-7. <c>Lerp</c> за единицу продолжает движение по кратчайшей дуге, и
    /// это зафиксировано поведением, а не только доктриной.
    /// </summary>
    [Fact]
    public void Lerp_BeyondOneKeepsShortestArc()
    {
        Angle from = Angle.FromDegrees(170);
        Angle to = Angle.FromDegrees(-170);

        Assert.Equal(170.0, Angle.Lerp(from, to, 0f).Degrees, 6);
        Assert.Equal(180.0, Angle.Lerp(from, to, 0.5f).Degrees, 6);
        Assert.Equal(-170.0, Angle.Lerp(from, to, 1f).Degrees, 6);
        Assert.Equal(-160.0, Angle.Lerp(from, to, 1.5f).Degrees, 6);
        Assert.Equal(-150.0, Angle.Lerp(from, to, 2f).Degrees, 6);

        _o.WriteLine(
            $"Lerp(...,0.9999) = {Angle.Lerp(from, to, 0.9999f).Degrees:F4}°, " +
            $"Lerp(...,1.0001) = {Angle.Lerp(from, to, 1.0001f).Degrees:F4}°");
    }
}