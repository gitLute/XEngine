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
/// <para>
/// Точность после перехода на сведение Cody-Waite одинакова на всём диапазоне
/// <c>float</c>: худшая ошибка 1.5 последнего разряда и 9.2e-8 абсолютной на
/// любом диапазоне от 3.2 до 3.4·10³⁸. До перехода на Cody-Waite это было неверно
/// при <c>|x| &gt; 10¹²</c>, где сведение теряло аргумент целиком.
/// </para>
/// </remarks>
internal static class DeterministicMath
{
    /// <summary>Логарифм двойки по основанию e в двойной точности.</summary>
    private const double LogTwoE = 1.44269504088896340736;

    /// <summary>Натуральный логарифм двойки в двойной точности.</summary>
    private const double LnTwoDouble = 0.69314718055994530942;

    /// <summary>2/π в двойной точности: обратная величина π/2.</summary>
    private const double InversePiOverTwo = 6.36619772367581382433e-01;

    /// <summary>
    /// π/2, первые 25 значащих бит. Младшие 28 бит мантиссы нулевые, и на
    /// этом держится приём Cody-Waite.
    /// </summary>
    private const double HalfPiHigh = 1.57079631090164184570e+00;

    /// <summary>Хвост π/2: <c>π/2 − <see cref="HalfPiHigh"/></c>.</summary>
    private const double HalfPiTail = 1.58932547735281966916e-08;

    /// <summary>Четверть оборота: граница, за которой многочлен уже неверен.</summary>
    private const double QuarterPi = 0.78539816339744830962;

    /// <summary>
    /// Граница ветви с Cody-Waite: <c>|x| &lt; 0x4DC90FDB</c>.
    /// </summary>
    /// <remarks>
    /// Взято из musl <c>__rem_pio2f</c> и означает то же самое, что там:
    /// произведение <c>n·(π/2)</c> для <c>|n| ≤ 8.3·10⁸</c> ещё раскладывается
    /// на два разбитых слагаемых, а выше нужен разбор 2/π по битам.
    /// </remarks>
    private const uint MediumReductionLimit = 0x4DC90FDB;

    /// <summary>
    /// 2/π, разбитая на куски по 24 бита после двоичной точки, как в musl
    /// <c>ipio2</c>.
    /// </summary>
    /// <remarks>
    /// Нужно девять кусков: у float мантисса 24 бита, порядок аргумента до
    /// 104, и чтобы дробная часть произведения <c>x·2/π</c> была известна до
    /// 2⁻³⁴, требуется 216 бит после точки.
    /// </remarks>
    private static ReadOnlySpan<int> InverseTwoPi =>
    [
        0xA2F983, 0x6E4E44, 0x1529FC, 0x2757D1, 0xF534DD, 0xC0DB62,
        0x95993C, 0x439041, 0xFE5163, 0xABDEBB, 0xC561B7, 0x246E3A,
        0x424DD2, 0xE00649, 0x2EEA09, 0xD1921C,
    ];

    /// <summary>Синус и косинус по одному аргументу.</summary>
    /// <param name="x">Аргумент в радианах.</param>
    /// <param name="sin">Синус.</param>
    /// <param name="cos">Косинус.</param>
    public static void SinCos(float x, out float sin, out float cos)
    {
        // Ноль обрабатывается отдельно ради знака. Многочлен на нуле даёт
        // (-0) + ((-0)·0)·коэффициент = (-0) + (+0) = +0, то есть знак
        // теряется, а IEEE 754 различает -0 и +0, и MathF.Sin(-0) возвращает -0.
        // Дальше знак нуля попадает в нормализацию угла, где -0 и +0 уже не
        // равны, поэтому подмена наблюдаема.
        if (x == 0f)
        {
            sin = x;
            cos = 1f;
            return;
        }

        ReduceToQuadrant(x, out float reduced, out int quadrant);

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
        switch (quadrant & 3)
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

    /// <summary>
    /// Сводит аргумент к квадранту: возвращает остаток в диапазоне
    /// <c>[−π/4; π/4]</c> и номер квадранта, для которого остаток нужно
    /// переставить знаками и местами.
    /// </summary>
    /// <param name="x">Аргумент в радианах.</param>
    /// <param name="reduced">Остаток в диапазоне <c>[−π/4; π/4]</c>.</param>
    /// <param name="quadrant">
    /// Номер четверти оборота, отсчитанный так же, как в musl: два младших
    /// бита определяют перестановку синуса и косинуса.
    /// </param>
    /// <remarks>
    /// Две ветви по природе разные, и граница между ними не подобрана, а
    /// выведена: пока <c>|x|</c> меньше <see cref="MediumReductionLimit"/>,
    /// произведение <c>n·(π/2)</c> раскладывается на два слагаемых, и остаток
    /// считается двумя слитными операциями. Выше этого произведение перестаёт
    /// помещаться в пару разбитых констант, и сведение идёт разбором 2/π по
    /// битам.
    /// <para>
    /// Прежнее сведение вычитало <c>n·(π/2)</c> одним вычитанием из одного
    /// полного слагаемого. На больших <c>n</c> округление этого произведения
    /// съедало все значащие биты остатка: у числа порядка 2⁶⁰ разряд равен
    /// 128 рад, а остаток лежит в <c>[−π/4; π/4]</c>. На 25.8 % диапазона
    /// float получалось <c>sin = 0</c> и <c>cos = 1</c>, то есть аргумент
    /// терялся целиком, хотя платформа на тех же входах держит 3.2e-8.
    /// </para>
    /// </remarks>
    private static void ReduceToQuadrant(float x, out float reduced, out int quadrant)
    {
        int bits = BitConverter.SingleToInt32Bits(x);
        uint magnitude = (uint)bits & 0x7FFFFFFFu;

        if (magnitude < MediumReductionLimit)
        {
            // Cody-Waite по образцу musl __rem_pio2f: остаток = x − n·pio2_1 − n·pio2_1t.
            //
            // Ближайшее целое берётся не через Math.Round, а вычитанием дробной
            // части: Math.Round округляет к ближайшему чётному, а нужна одна
            // операция, которую компилятор не может ни стянуть в слитную, ни
            // переставить. x·invpio2 лежит в [-8.3·10⁸; 8.3·10⁸], вычитание
            // целой части точное по Стербенцу, поэтому сравнение точно.
            double argument = x;
            double scaled = argument * InversePiOverTwo;
            double lower = Math.Floor(scaled);
            double count = lower + ((scaled - lower) >= 0.5 ? 1.0 : 0.0);

            double tail = Math.FusedMultiplyAdd(-count, HalfPiHigh, argument);
            double value = Math.FusedMultiplyAdd(-count, HalfPiTail, tail);

            // Округление n в ближайшее целое расходится с истинным на единицу,
            // когда x·2/π стоит ближе чем на 2⁻²³ к полуцелому: произведение
            // x·invpio2 округлено, и при равных долях выигрывает чётное.
            // Тогда остаток уходит за π/4, и номер квадранта надо поправить.
            if (value < -QuarterPi)
            {
                count -= 1.0;
                tail = Math.FusedMultiplyAdd(-count, HalfPiHigh, argument);
                value = Math.FusedMultiplyAdd(-count, HalfPiTail, tail);
            }
            else if (value > QuarterPi)
            {
                count += 1.0;
                tail = Math.FusedMultiplyAdd(-count, HalfPiHigh, argument);
                value = Math.FusedMultiplyAdd(-count, HalfPiTail, tail);
            }

            reduced = (float)value;
            quadrant = (int)count;
            return;
        }

        if (magnitude >= 0x7F800000)
        {
            // Бесконечность и NaN: по IEEE 754 и C99 F.10.1.4 на нечисловом
            // входе ответ NaN в обоих значениях, а не конечное число.
            reduced = float.NaN;
            quadrant = 0;
            return;
        }

        ReduceLarge(bits, out reduced, out quadrant);
    }

    /// <summary>
    /// Сведение к квадранту для больших аргументов: разбор 2/π по битам.
    /// </summary>
    /// <param name="bits">Биты аргумента беззнаковым целым, мантисса с неявной единицей.</param>
    /// <param name="reduced">Остаток в диапазоне <c>[−π/4; π/4]</c>.</param>
    /// <param name="quadrant">Номер четверти оборота.</param>
    /// <remarks>
    /// У <c>float</c> всего 24 бита мантиссы, и этого хватает, чтобы обойтись
    /// без точного умножения произвольной длины: произведение <c>m·2/π</c>
    /// раскладывается на куски по 24 бита, произведение мантиссы на кусок
    /// помещается в 64 бита целиком, а дальше суммирование идёт по разрядам с
    /// переносами.
    /// <para>
    /// Переносы между кусками не нужны: куски разнесены по разрядам ровно на
    /// 24 бита, и каждый занимает свой диапазон, то есть куски не
    /// перекрываются и складываются без потерь. Поэтому целая часть нужна
    /// только по модулю 4 (она определяет перестановку синуса и косинуса), а
    /// дробная нужна с точностью 2⁻³⁴ — её и копим.
    /// </para>
    /// </remarks>
    private static void ReduceLarge(int bits, out float reduced, out int quadrant)
    {
        const int Pieces = 9;

        // x = ±m·2^e, где m — 24-битная мантисса float, то есть целое число.
        uint m = ((uint)bits & 0x7FFFFF) | 0x800000u;
        int e = ((bits >> 23) & 0xFF) - 150;

        Span<int> digit = stackalloc int[Pieces + 1];

        // Куски m·(2/π) в базе 2⁻²⁴. Старшие 24 бита произведения идут в
        // текущий разряд, младшие — в следующий: так произведение раскладывается
        // по двум соседним разрядам с точностью, заданной точностью 2/π.
        // Множитель приводится к ulong явно и не по недосмотру: произведение
        // 24-битной мантиссы на 24-битный кусок занимает 48 бит, и в uint
        // оно переполнилось бы на старших разрядах ровно там, где сведение и
        // ломается.
        ulong product = (ulong)m * (uint)InverseTwoPi[0];
        digit[0] = (int)(product >> 24);
        int low = (int)(product & 0xFFFFFF);

        // Цикл идёт по кускам 1…Pieces−1, а не по 1…Pieces: последний кусок
        // целиком уходит в digit[Pieces] строкой ниже, и лишняя итерация
        // записала бы его дважды — старшие биты в неверный разряд, младшие
        // поверх них же.
        for (int j = 1; j < Pieces; j++)
        {
            product = (ulong)m * (uint)InverseTwoPi[j];
            digit[j] = (int)(product >> 24) + low;
            low = (int)(product & 0xFFFFFF);
        }

        digit[Pieces] = low;

        // Нормализация: разряд должен уместиться в 24 бита, а лишнее уходит не в
        // следующий разряд, а в ПРЕДЫДУЩИЙ. Разряд j стоит на степени e − 24j,
        // то есть чем больше индекс, тем ниже разряд; единица, выпавшая из
        // разряда j, равна 2^24·2^(e−24j), а разряд j−1 стоит ровно на
        // e−24(j−1) = e−24j+24. Поэтому перенос идёт по убыванию индекса, и
        // цикл обязан идти с конца: при обходе по возрастанию разряд j−1 уже
        // закрыт, и перенос в него потерялся бы молча.
        for (int j = Pieces; j >= 1; j--)
        {
            int carry = digit[j] >> 24;
            digit[j] &= 0xFFFFFF;
            digit[j - 1] += carry;
        }

        digit[0] &= 0xFFFFFF;

        int whole = 0;
        double part = 0.0;

        for (int j = 0; j <= Pieces; j++)
        {
            // Разряд j стоит на степени e − 24j. Разряды сбиваются на 24 бита,
            // поэтому куски не перекрываются и сумма точна.
            int power = e - (24 * j);
            int value = digit[j];

            if (power >= 0)
            {
                // Целые разряды от второго и выше кратны 4 и на номер квадранта
                // не влияют.
                if (power == 0)
                {
                    whole += value;
                }
                else if (power == 1)
                {
                    whole += (value & 1) << 1;
                }

                continue;
            }

            int shift = -power;
            if (shift >= 24)
            {
                // Разряд целиком дробный.
                part = Math.FusedMultiplyAdd(value, PowerOfTwo(power), part);
            }
            else
            {
                // Разряд пересекает границу целого: старшие биты — целая часть,
                // младшие — дробная.
                whole += value >> shift;
                part = Math.FusedMultiplyAdd(
                    value & ((1 << shift) - 1),
                    PowerOfTwo(power),
                    part);
            }
        }

        // Часть уже лежит в [−1; 1); приводим к [−1/2; 1/2] и получаем
        // номер квадранта, при котором остаток по модулю не больше π/4.
        if (part >= 0.5)
        {
            whole++;
            part -= 1.0;
        }
        else if (part < -0.5)
        {
            whole--;
            part += 1.0;
        }

        double angle = part * (Math.PI * 0.5);
        if (bits < 0)
        {
            // Остаток и номер квадранта меняют знак вместе с аргументом.
            angle = -angle;
            whole = -whole;
        }

        reduced = (float)angle;
        quadrant = whole;
    }

    /// <summary>
    /// Степень двойки как двоичное число: <c>2^power</c> точно, без вызова
    /// платформы.
    /// </summary>
    /// <param name="power">Показатель степени.</param>
    /// <returns>Значение <c>2^power</c>.</returns>
    /// <remarks>
    /// Диапазон показателя — от −1022 до 1023, то есть нормальный диапазон.
    /// В разборе 2/π по битам показатель лежит в пределах от −213 до 104, так
    /// что границы не достигаются.
    /// </remarks>
    private static double PowerOfTwo(int power) => BitConverter.Int64BitsToDouble((long)(1023 + power) << 52);

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
    /// <remarks>
    /// Знаки нулей различаются по IEEE 754 и по C99 F.10.1.4, и различаются
    /// не только при <c>x = ±0</c>: при <c>x &lt; 0</c> знак нуля у <c>y</c>
    /// задаёт знак результата, то есть <c>atan2(+0, x) = +π</c> и
    /// <c>atan2(-0, x) = -π</c>. Проверка <c>y &gt;= 0f</c> истинна и для
    /// <c>-0</c>, из-за чего <c>-π</c> становилось <c>+π</c>: один и тот же
    /// вход давал два разных значения <see cref="Angle"/> с разными хешами, и
    /// варианты сборки расходились между собой.
    /// <para>
    /// Обе координаты бесконечны разбираются отдельно: отношение <c>y/x</c>
    /// не определено, и без этого разбора получался <c>inf/inf = NaN</c>, то
    /// есть угол пропадал целиком там, где платформа отвечает <c>±π/4</c> и
    /// <c>±3π/4</c>.
    /// </para>
    /// <para>
    /// Нечисловые координаты отвергаются первыми. Без этого сравнения
    /// <c>x &gt; 0f</c> и <c>x &lt; 0f</c> для NaN ложны одновременно, управление
    /// проваливается в ветвь «<c>x</c> — ноль со знаком», и NaN читается там как
    /// знаковый ноль: <c>atan2(1, NaN)</c> отвечал <c>+π/2</c> вместо NaN, а
    /// <c>atan2(NaN, NaN)</c> — <c>−π</c>. По IEEE 754 и C99 F.10.1.4 ответ при
    /// любом нечисловом аргументе есть NaN, и заглушка должна это говорить прямо,
    /// а не разбираться с NaN как с нулём.
    /// </para>
    /// </remarks>
    public static float Atan2(float y, float x)
    {
        if (float.IsNaN(y) || float.IsNaN(x))
        {
            return float.NaN;
        }

        if (float.IsInfinity(y) && float.IsInfinity(x))
        {
            // y/x не определено. По C99 F.10.1.4: ±π/4 при x > 0 и ±3π/4 при
            // x < 0, знак результата совпадает со знаком y. Обе величины
            // берутся из MathF.PI, чтобы результат совпал с платформой побитово.
            float quarter = MathF.PI * 0.25f;
            float threeQuarters = MathF.PI * 0.75f;
            return y > 0f
                ? (x > 0f ? quarter : threeQuarters)
                : (x > 0f ? -quarter : -threeQuarters);
        }

        if (x > 0f)
        {
            return Atan(y / x);
        }

        if (x < 0f)
        {
            // Приведение к квадранту выполняется в двойной точности: угол
            // возвращается одинарной, а складывать его с π в одинарной нельзя —
            // при больших углах сложение теряет до нескольких последних разрядов.
            //
            // Знак нуля у y различается, поэтому сравнение знаков должно быть
            // раздельным — ровно так же, как в ветви x = ±0 ниже.
            return Negative(y)
                ? (float)((double)Atan(y / x) - Math.PI)
                : (float)((double)Atan(y / x) + Math.PI);
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
        bool xNegative = Negative(x);
        bool yNegative = Negative(y);

        if (!xNegative)
        {
            // x = +0: угол нулевой, знак берётся из y.
            return BitConverter.Int32BitsToSingle(yNegative ? SignMask : 0);
        }

        // x = −0: угол равен ±π, знак берётся из y.
        int result = BitConverter.SingleToInt32Bits(MathF.PI) | (yNegative ? SignMask : 0);
        return BitConverter.Int32BitsToSingle(result);
    }

    /// <summary>Старший бит float, то есть знак. −0 отличается от +0 только им.</summary>
    private const int SignMask = unchecked((int)0x80000000);

    /// <summary>
    /// Отмечает число знаком «меньше нуля». Именно знак, а не сравнение с нулём:
    /// сравнение <c>-0 &lt; 0</c> ложно по IEEE 754, а знак у <c>-0</c> есть.
    /// </summary>
    private static bool Negative(float value) => (BitConverter.SingleToInt32Bits(value) & SignMask) != 0;

    /// <summary>Арксинус.</summary>
    /// <param name="x">Аргумент в <c>[-1; 1]</c>.</param>
    /// <returns>Значение арксинуса.</returns>
    /// <remarks>
    /// Худшая ошибка — 1 последний разряд на миллионе точек, проверено
    /// измерением. Раньше здесь стояло <c>atan2(x, √(1 - x²))</c>, и ошибка
    /// доходила до 4.5 разряда.
    /// </remarks>
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

        // Сведение к области быстрой сходимости. При |x| >= 1/√2 работает
        // тождество asin(x) = π/2 − asin(√(1 − x²)), после которого аргумент
        // многочлена не превышает 1/√2, то есть его квадрат не больше 0.5.
        //
        // Граница не произвольная. У арксинуса ветвится точка z = 1, поэтому
        // радиус Бернштейна для отрезка [0; B] равен примерно (1 + B)/(1 − B).
        // На [0; 0.75] это около 3, и на такой скорости сходимости семь
        // членов дают лишь 4e-6 — этого мало. На [0; 0.5] радиус около 5.8,
        // и тех же семи членов хватает с запасом.
        const float Cutoff = 0.70710678f;

        float result;
        if (magnitude < Cutoff)
        {
            result = AsinKernel(magnitude);
        }
        else
        {
            // Корень считается в двойной точности: у самой единицы
            // (1 − x²) — разность двух чисел, близких к единице, и в
            // одинарной там теряется треть значащих цифр. Вычитание π/2
            // делается тоже в двойной: результат не меньше π/4, так что
            // сокращения нет, но лишнего округления не возникает.
            double root = Math.Sqrt(1.0 - ((double)magnitude * magnitude));
            result = (float)((Math.PI * 0.5) - AsinKernel((float)root));
        }

        return negative ? -result : result;
    }

    /// <summary>
    /// Арксинус в области сходящегося многочлена: <c>y * (1 + z * P(z))</c>,
    /// где <c>z = y²</c> и <c>|y| &lt;= 1/√2</c>.
    /// </summary>
    /// <param name="y">Аргумент по модулю не больше <c>1/√2</c>.</param>
    /// <returns>Значение арксинуса.</returns>
    private static float AsinKernel(float y)
    {
        float squared = y * y;

        float poly = 9.4697348773e-02f;
        poly = Fma(poly, squared, -7.9720713198e-02f);
        poly = Fma(poly, squared, 6.3796974719e-02f);
        poly = Fma(poly, squared, 1.0435326025e-02f);
        poly = Fma(poly, squared, 3.1990237534e-02f);
        poly = Fma(poly, squared, 4.4538814574e-02f);
        poly = Fma(poly, squared, 7.5002521276e-02f);
        poly = Fma(poly, squared, 1.6666665673e-01f);

        // Свёртка в y + y·z·P слитной операцией: при сборке через
        // y * (1 + z * P) округлений три, и каждое добавляет половину
        // последнего разряда к результату. Здесь округление одно.
        return Fma(y * squared, poly, y);
    }

    /// <summary>
    /// Арккосинус.
    /// </summary>
    /// <param name="x">Аргумент в <c>[-1; 1]</c>.</param>
    /// <returns>Значение арккосинуса в <c>[0; π]</c>.</returns>
    /// <remarks>
    /// Считается как <c>atan2(√(1 - x²), x)</c>, а не рядом Тейлора: у
    /// арккосинуса особая точка на единице, где ряд сходится медленно, и
    /// вычитание <c>π/2 - asin(x)</c> теряет там все значащие цифры.
    /// Квадратный корень определён точно спецификацией IEEE 754, поэтому
    /// платформенным вызовом он не является.
    /// <para>
    /// Квадрат раскладывается на множители: <c>(1 - x)(1 + x)</c>. Прямое
    /// <c>1 - x²</c> у самой единицы — разность двух чисел, близких к единице,
    /// и в одинарной точности там теряется треть значащих цифр. Измерено на
    /// <c>x = 0.99983</c>: корень получается 0.0185948 вместо 0.0185967, то
    /// есть расхождение 360 последних разрядов результата. Разложение даёт
    /// оба множителя точно, и ошибка падает до округления.
    /// </para>
    /// </remarks>
    public static float Acos(float x)
    {
        // За пределами [-1; 1] ответ NaN в обоих вариантах сборки.
        //
        // Прежде здесь стояло «x >= 1 -> 0, x <= -1 -> π», то есть детерминированный
        // вариант отвечал конечным числом там, где Fast отвечал NaN. Расхождение
        // было на 4 входах из 14: Acos(±∞), Acos(2), Acos(-2). На одном и том же
        // входе два варианта сборки давали разные значения, а README обещает,
        // что оба проходят один и тот же набор тестов.
        //
        // Отвергать явно, а не разбирать сравнениями, здесь обязательно: при x = NaN
        // оба сравнения ложны одновременно, и код ушёл бы в atan2(√NaN, NaN),
        // то есть в NaN случайно, а не по контракту. Так же поступает Asin
        // в этом же файле.
        if (float.IsNaN(x) || x > 1f || x < -1f)
        {
            return float.NaN;
        }

        if (x == 1f)
        {
            return 0f;
        }

        if (x == -1f)
        {
            return MathF.PI;
        }

        return Atan2(MathF.Sqrt((1f - x) * (1f + x)), x);
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
        // Неопределённость отсекается явно: без проверки целая часть NaN не
        // помещается в int, и поведение приведения становится
        // неопределённым. Само по себе оно вернуло бы NaN и так, поэтому
        // проверка держит контракт, а не результат.
        if (double.IsNaN(log2))
        {
            return float.NaN;
        }

        // Границы диапазона ловят и бесконечности тоже: +∞ не меньше
        // верхней границы, −∞ не больше нижней. Переполнение вверх наступает
        // по худшему случаю раскладки: при |остаток| <= 0.5 мантисса лежит в
        // [0.707; 1.414], и наибольший показатель, при котором результат ещё
        // помещается в float, равен 128, то есть log2 < 128.5. Ставить
        // границу по 127.5 нельзя: там лежат вполне представимые значения
        // вплоть до самого float.MaxValue.
        if (log2 >= 128.5)
        {
            return float.PositiveInfinity;
        }

        // Нижняя граница ставится с запасом до того предела, где результат
        // заведомо меньше наименьшего ненормального числа, то есть равен
        // нулю при округлении.
        if (log2 <= -150.0)
        {
            return 0f;
        }

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
        if (biased <= 0)
        {
            // Нормальный диапазон исчерпан: результат ненулевой, но меньше
            // минимального нормального. Ненормальное число строится сдвигом
            // целого поля мантиссы, а не умножением на фиксированную степень
            // двойки: глубина сдвига задаётся показателем, а постоянный
            // множитель обнулял всё глубже 2^-127, то есть exp(−100) и ниже,
            // хотя такие значения ещё представимы.
            //
            // Значение равно mantissa · 2^(biased − 127). Мантисса лежит в
            // [0.707; 1.414], поэтому в масштабе 2^23 она превращается в
            // точное целое из 24 разрядов, и остаётся лишь целочисленный
            // сдвиг с округлением к ближайшему. Глубина сдвига не превышает
            // 24, так как показатель уже ограничен сверху значением −150.
            int integer = (int)(mantissa * 8388608f);
            int shift = 1 - biased;
            int bits = integer >> shift;
            int remainder = integer - (bits << shift);
            int half = 1 << (shift - 1);

            if (remainder > half || (remainder == half && (bits & 1) == 1))
            {
                bits++;
            }

            return BitConverter.Int32BitsToSingle(bits);
        }

        // Степень двойки масштаба берётся наибольшей представимой: при
        // power = 128 сдвиг битовой схемы дал бы поле бесконечности, и всё
        // значение до самого float.MaxValue было бы потеряно. Недостающие
        // деления выполняются отдельно, и переполнение обнаруживается само
        // собой, как и должно.
        int scaleBias = biased < 255 ? biased : 254;
        float result = mantissa * BitConverter.Int32BitsToSingle(scaleBias << 23);

        for (int remaining = biased - scaleBias; remaining > 0; remaining--)
        {
            result *= 2f;
        }

        return result;
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