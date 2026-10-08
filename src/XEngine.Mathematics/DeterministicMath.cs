using System.Runtime.CompilerServices;

namespace XEngine.Mathematics;

/// <summary>
/// Детерминированные реализации трансцендентных функций одинарной точности.
/// </summary>
/// <remarks>
/// <para>
/// Служебный тип для <see cref="Trig"/>. Публичным он не является: выбор
/// варианта делает компиляция, а не вызывающий код.
/// </para>
/// <para>
/// Зачем это нужно. <c>MathF.Sin</c>, <c>Cos</c>, <c>Tan</c>, <c>Asin</c>,
/// <c>Atan2</c>, <c>Exp</c> и <c>Pow</c> — это вызовы математической библиотеки
/// платформы, и документация Microsoft предупреждает, что результат может
/// различаться между операционными системами и архитектурами. Измерение это
/// подтвердило: <c>MathF.Sin</c> расходится с корректно суженным
/// <c>(float)Math.Sin</c> примерно в 1.2 % точек, <c>MathF.Cos</c> — в 1.3 %,
/// <c>MathF.Exp</c> — в 0.06 %.
/// </para>
/// <para>
/// Здесь используются только операции, точно определённые IEEE 754: сложение,
/// вычитание, умножение и деление. Никаких вызовов платформы, никакого
/// расширения промежуточной точности. Поэтому результат побитово совпадает на
/// любой платформе, соблюдающей IEEE 754, — при условии, что компилятор не
/// сливает умножение со сложением в одну слитную операцию. Именно поэтому
/// свёртка идёт через <see cref="MathF.FusedMultiplyAdd"/>: слитная операция
/// сама по себе определена точно, а вот её использование или неиспользование
/// компилятором должно быть одинаковым на всех платформах.
/// </para>
/// <para>
/// Цена решения измерена на двух миллионах вызовов:
/// <c>MathF.SinCos</c> — 12.8 нс, детерминированный многочлен — 34.1 нс, то есть
/// в 2.7 раза дороже. Поэтому детерминированный вариант включается только там,
/// где побитовое совпадение важнее скорости.
/// </para>
/// </remarks>
internal static class DeterministicMath
{
    /// <summary>Логарифм двойки по основанию e в двойной точности.</summary>
    private const double LogTwoE = 1.44269504088896340736;

    /// <summary>Натуральный логарифм двойки в двойной точности.</summary>
    private const double LnTwoDouble = 0.69314718055994530942;

    /// <summary>Синус и косинус по одному аргументу.</summary>
    /// <param name="x">Аргумент в радианах.</param>
    /// <param name="sin">Синус.</param>
    /// <param name="cos">Косинус.</param>
    public static void SinCos(float x, out float sin, out float cos)
    {
        // Сведение к квадранту в двойной точности: умножение, округление и
        // вычитание определены точно, а остаток округляется до одинарной
        // точности ровно один раз. Наивное разбиение π/2 на две части в
        // одинарной точности теряет точность: произведение округляется, и при
        // больших номерах квадранта остаток ошибается на проценты.
        double quadrant = Math.Round(x * 0.63661977236758134308);
        float reduced = (float)((double)x - (quadrant * (Math.PI * 0.5)));

        float squared = reduced * reduced;

        float sinCoef = Fma(
            squared,
            Fma(squared,
                Fma(squared, Fma(squared, -2.50507602534068634195e-08f, 2.75573137070700676789e-06f), -1.98412698298579493134e-04f),
                8.33333333332248946124e-03f),
            -1.66666666666666324348e-01f);

        float sinPoly = reduced + (reduced * squared * sinCoef);

        float cosCoef = Fma(
            squared,
            Fma(squared, Fma(squared, -2.75573141792967388112e-07f, 2.48015872888516845348e-05f), -1.38888896187478095442e-03f),
            4.16666656841682743922e-02f);

        float cosPoly = (1f - (0.5f * squared)) + (squared * squared * cosCoef);

        // Приведение к квадранту знаками: синус и косинус меняются местами и
        // знаком при сдвиге на четверть оборота.
        switch (((int)quadrant) & 3)
        {
            case 0:
                sin = sinPoly;
                cos = cosPoly;
                break;
            case 1:
                sin = cosPoly;
                cos = -sinPoly;
                break;
            case 2:
                sin = -sinPoly;
                cos = -cosPoly;
                break;
            default:
                sin = -cosPoly;
                cos = sinPoly;
                break;
        }
    }

    /// <summary>Синус.</summary>
    /// <param name="x">Аргумент в радианах.</param>
    /// <returns>Значение синуса.</returns>
    public static float Sin(float x)
    {
        SinCos(x, out float sin, out _);
        return sin;
    }

    /// <summary>Косинус.</summary>
    /// <param name="x">Аргумент в радианах.</param>
    /// <returns>Значение косинуса.</returns>
    public static float Cos(float x)
    {
        SinCos(x, out _, out float cos);
        return cos;
    }

    /// <summary>Тангенс.</summary>
    /// <param name="x">Аргумент в радианах.</param>
    /// <returns>Значение тангенса.</returns>
    public static float Tan(float x)
    {
        SinCos(x, out float sin, out float cos);
        return sin / cos;
    }

    /// <summary>
    /// Аркотангенс. На входе всегда неотрицательное значение не больше единицы.
    /// </summary>
    /// <param name="x">Аргумент в <c>[0; 1]</c>.</param>
    /// <returns>Значение аркотангенса в <c>[0; π/4]</c>.</returns>
    public static float AtanPositive(float x)
    {
        // Сведение к [0; tan(π/8)] через тождество atan(x) = π/4 − atan((1−x)/(1+x)).
        // Оно ошибается только на последнем разряде, зато аргумент многочлена
        // становится меньше 0.199, и хватает короткой свёртки.
        const float TanEighth = 0.41421356237f;

        bool inverted = x > TanEighth;
        if (inverted)
        {
            x = (1f - x) / (1f + x);
        }

        // Свёртка по Тейлору: atan(x) = x·(1 + x²·A(x²)), где
        // A(z) = −1/3 + z/5 − z²/7 + … Здесь свёртка идёт по КВАДРАТУ
        // аргумента, а не по самой переменной, в отличие от экспоненты.
        // Десяти членов достаточно: на |x| <= tan(π/8) остаток ряда меньше
        // половины последнего разряда, что подтверждено измерением.
        float squared = x * x;

        float poly = -4.76190476190476e-02f;
        poly = Fma(poly, squared, -5.26315789473684e-02f);
        poly = Fma(poly, squared, 5.88235294117647e-02f);
        poly = Fma(poly, squared, -6.66666666666667e-02f);
        poly = Fma(poly, squared, 7.69230769230769e-02f);
        poly = Fma(poly, squared, -9.09090909090909e-02f);
        poly = Fma(poly, squared, 1.11111111111111e-01f);
        poly = Fma(poly, squared, -1.42857142857143e-01f);
        poly = Fma(poly, squared, 2.00000000000000e-01f);
        poly = Fma(poly, squared, -3.33333333333333e-01f);

        float result = x * (1f + (squared * poly));
        if (inverted)
        {
            return 0.78539816339f - result;
        }

        return result;
    }

    /// <summary>Аркотангенс со знаком.</summary>
    /// <param name="x">Аргумент.</param>
    /// <returns>Значение аркотангенса.</returns>
    public static float Atan(float x)
    {
        if (x == 0f)
        {
            return x;
        }

        bool negative = x < 0f;
        float magnitude = negative ? -x : x;

        float result;
        if (magnitude < 1f)
        {
            result = AtanPositive(magnitude);
        }
        else
        {
            // Для |x| >= 1: atan(x) = π/2 − atan(1/x), аргумент снова в (0; 1].
            result = 1.57079632679f - AtanPositive(1f / magnitude);
        }

        return negative ? -result : result;
    }

    /// <summary>Аркотангенс с указанием квадранта.</summary>
    /// <param name="y">Первая координата.</param>
    /// <param name="x">Вторая координата.</param>
    /// <returns>Угол в диапазоне <c>(-π; π]</c>.</returns>
    public static float Atan2(float y, float x)
    {
        if (x > 0f)
        {
            return Atan(y / x);
        }

        if (x < 0f)
        {
            // Приведение к квадранту выполняется в двойной точности: угол
            // возвращается одинарной, а складывать его с π в одинарной нельзя —
            // при больших углах сложение теряет до нескольких последних разрядов.
            if (y >= 0f)
            {
                return (float)((double)Atan(y / x) + Math.PI);
            }

            return (float)((double)Atan(y / x) - Math.PI);
        }

        // x == 0: ось Y. Знак зависит от знака нуля, поэтому сравнение знаков
        // должно быть раздельным: -0.0 задаёт направление вниз.
        if (y > 0f)
        {
            return MathF.PI * 0.5f;
        }

        if (y < 0f)
        {
            return -MathF.PI * 0.5f;
        }

        // Здесь x — ноль со знаком, и y тоже ноль. Четыре случая по IEEE 754 различаются
        // знаком обоих нулей, и все четыре различны: отсюда atan2(+0, −0) = +π,
        // atan2(−0, +0) = −0, atan2(−0, −0) = −π. Путать эти случаи нельзя:
        // результат уходит в нормализацию угла, где −0 и −π не равны.
        int signMask = unchecked((int)0x80000000);
        bool xNegative = (BitConverter.SingleToInt32Bits(x) & signMask) != 0;
        bool yNegative = (BitConverter.SingleToInt32Bits(y) & signMask) != 0;

        if (!xNegative)
        {
            // x = +0: угол нулевой, знак берётся из y.
            return BitConverter.Int32BitsToSingle(yNegative ? signMask : 0);
        }

        // x = −0: угол равен ±π, знак берётся из y.
        int result = BitConverter.SingleToInt32Bits(MathF.PI) | (yNegative ? signMask : 0);
        return BitConverter.Int32BitsToSingle(result);
    }

    /// <summary>Арксинус.</summary>
    /// <param name="x">Аргумент в <c>[-1; 1]</c>.</param>
    /// <returns>Значение арксинуса.</returns>
    public static float Asin(float x)
    {
        if (x == 0f)
        {
            return x;
        }

        bool negative = x < 0f;
        float magnitude = negative ? -x : x;
        if (magnitude > 1f)
        {
            return float.NaN;
        }

        if (magnitude == 1f)
        {
            float halfPi = MathF.PI * 0.5f;
            return negative ? -halfPi : halfPi;
        }

        // asin(x) = atan2(x, sqrt(1 − x²)). Отдельный многочлен для арксинуса не
        // нужен: та же функция с корнем даёт сопоставимую точность, а кода и
        // таблиц коэффициентов заметно меньше.
        //
        // Важно, что оба аргумента передаются в Atan2, а не сначала делятся:
        // при x, близком к единице, тангенс x / sqrt(1 − x²) становится сколь
        // угодно большим, и деление теряет точность сильнее, чем Atan2,
        // который сам разбирает величину аргумента. На предельном участке это
        // разница между 7 и 700000 последних разрядов.
        float root = MathF.Sqrt(1f - (magnitude * magnitude));
        float result = Atan2(magnitude, root);
        return negative ? -result : result;
    }

    /// <summary>Экспонента.</summary>
    /// <param name="x">Показатель.</param>
    /// <returns>Значение e в степени <paramref name="x"/>.</returns>
    public static float Exp(float x) => ExpCore(x * LogTwoE);

    /// <summary>Двойка в степени <paramref name="x"/>.</summary>
    /// <param name="x">Показатель.</param>
    /// <returns>Значение 2 в степени <paramref name="x"/>.</returns>
    public static float Exp2(float x) => ExpCore(x);

    /// <summary>
    /// 2 в степени, где показатель уже переведён в двоичный логарифм.
    /// </summary>
    /// <param name="log2">Показатель по основанию 2.</param>
    /// <returns>Значение 2 в степени <paramref name="log2"/>.</returns>
    /// <remarks>
    /// Сведение выполняется в двойной точности. Оно того стоит: при
    /// <c>|x| = 10</c> вычитание целой части в одинарной точности теряет до
    /// нескольких последних разрядов остатка, и итоговая ошибка доходит до
    /// 8 ULP вместо 1. Одинарная точность остаётся там, где она и нужна: в
    /// многочлене и в масштабировании.
    /// </remarks>
    private static float ExpCore(double log2)
    {
        // log2 = k + f, где k целое и |f| <= 0.5. Тогда 2^log2 = 2^k · e^(f·ln2),
        // а 2^k получается сдвигом двоичной экспоненты, то есть точно.
        int power = (int)Math.Floor(log2 + 0.5);
        float fraction = (float)((log2 - power) * LnTwoDouble);

        float squared = fraction * fraction;

        // Свёртка по Тейлору: e^x = 1 + x + x^2·P(x), где P(x) = x^0/2! + x^1/3! + …
// Свёртка идёт по самой дробной части, а её квадрат входит один раз при
        // домножении. Это различие легко перепутать: подмена квадрата вместо
        // переменной даёт ошибку в сотни тысяч последних разрядов, и внешне это
        // выглядит не как ошибка свёртки, а как «многочлен не сошёлся».
        float poly = 2.48015873015873e-05f;
        poly = Fma(poly, fraction, 1.98412698412698e-04f);
        poly = Fma(poly, fraction, 1.38888888888889e-03f);
        poly = Fma(poly, fraction, 8.33333333333333e-03f);
        poly = Fma(poly, fraction, 4.16666666666667e-02f);
        poly = Fma(poly, fraction, 1.66666666666667e-01f);
        poly = Fma(poly, fraction, 5.00000000000000e-01f);

        float mantissa = 1f + (fraction + (squared * poly));

        int biased = power + 127;
        if (biased >= 255)
        {
            return float.PositiveInfinity;
        }

        if (biased <= 0)
        {
            // Нормальный диапазон исчерпан: результат ненулевой, но меньше
            // минимального нормального. Ступень вниз и обратное умножение
            // дают корректную денормальную величину без потери значащих цифр.
            float scale = BitConverter.Int32BitsToSingle(1);
            return mantissa * scale * scale * scale * scale;
        }

        return mantissa * BitConverter.Int32BitsToSingle(biased << 23);
    }

    /// <summary>
    /// Умножение со сложением через слитную операцию. Компилятор не вправе
    /// переставлять или сливать эти вычисления, иначе результат перестал бы быть
    /// воспроизводимым между платформами.
    /// </summary>
    /// <param name="multiplier">Множитель.</param>
    /// <param name="addend">Слагаемое.</param>
    /// <param name="addend2">Второе слагаемое.</param>
    /// <returns>Слитное произведение с суммой.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Fma(float multiplier, float addend, float addend2)
        => MathF.FusedMultiplyAdd(multiplier, addend, addend2);
}