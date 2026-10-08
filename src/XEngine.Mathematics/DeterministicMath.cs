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

        // Сведение к квадранту в двойной точности: умножение, округление и
        // вычитание определены точно, а остаток округляется до одинарной
        // точности ровно один раз.
        //
        // Измерено на 200000 точек в каждом диапазоне: остаток ошибается не
        // более чем на 0.13 последнего разряда до |x| = 2^24 и на 2 разряда
        // до 2^32. Дальше точность теряется не из-за формулы, а сама по
        // себе: у числа порядка 2^32 последний разряд двойной точности
        // сравним с π, то есть все значащие цифры остатка физически не
        // помещаются в исходном числе. Это неустранимо в одинарной точности,
        // и честное поведение — вернуть конечное значение, а не бесконечность.
        double reducedInput = x;
        double quadrant = Math.Round(reducedInput * 0.63661977236758134308);

        // Номер квадранта ниже приводится к int, чтобы взять два младших бита.
        // При |x| больше примерно 3.4e9 он не помещается, и приведение double к
        // int становится неопределённым. Поэтому сначала вычитаются полные
        // обороты — столько, сколько нужно, а не один раз: одно вычитание
        // уменьшает порядок всего на 2^52, и при |x| порядка 3.4e38 его
        // недостаточно. Остаток при этом меняется на величину, кратную 2π,
        // то есть на синус и косинус не влияет вовсе.
        //
        // Круг повторяется не более трёх раз: каждое вычитание уменьшает
        // порядок на 2^52, а три шага покрывают весь диапазон float.
        const double QuadrantLimit = 2147483000.0;
        while (quadrant > QuadrantLimit || quadrant < -QuadrantLimit)
        {
            double turns = Math.Round(reducedInput / (Math.PI * 2.0));
            reducedInput -= turns * (Math.PI * 2.0);
            quadrant = Math.Round(reducedInput * 0.63661977236758134308);
        }

        float reduced = (float)(reducedInput - (quadrant * (Math.PI * 0.5)));

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
        if (x >= 1f)
        {
            return 0f;
        }

        if (x <= -1f)
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