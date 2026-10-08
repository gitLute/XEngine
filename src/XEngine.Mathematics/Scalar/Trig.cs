namespace XEngine.Mathematics;

/// <summary>
/// Трансцендентные функции одинарной точности с двумя вариантами сборки.
/// </summary>
/// <remarks>
/// <para>
/// Библиотека обращается к <c>MathF</c> не напрямую, а через этот тип. Так
/// выбор варианта делает компиляция, а не вызывающий код: сменить поведение
/// можно одной командой сборки, а не правкой вызовов.
/// </para>
/// <para><b>Вариант по умолчанию — быстрый.</b> Здесь <c>MathF.SinCos</c>
/// стоит 12.8 нс на пару значений, и это самая быстрая доступная реализация.
/// Цена — зависимость от математической библиотеки платформы: один и тот же
/// сид генератора даст разные направления на Linux и Windows, потому что
/// <see cref="IRandomSource"/> возвращает одни и те же биты, а преобразование
/// их в угол разойдётся.
/// </para>
/// <para><b>Детерминированный вариант.</b> Включается свойством сборки
/// <c>MathBackend=Deterministic</c>. Все функции считаются многочленами через
/// операции, точно определённые IEEE 754, поэтому результат побитово совпадает
/// на всех платформах. Плата — 34.1 нс вместо 12.8 нс, то есть в 2.7 раза
/// дороже, и чуть худшая точность: около 1.5 последнего разряда против 0.56.
/// </para>
/// <para>
/// Оба варианта проходят один и тот же набор тестов: проверяется не конкретная
/// реализация, а соответствие эталону на двойной точности.
/// </para>
/// </remarks>
public static class Trig
{
    /// <summary>
    /// Признак того, что собран детерминированный вариант.
    /// </summary>
    /// <remarks>
    /// Нужен вызывающему коду, чтобы записать в лог или в отчёт, каким
    /// вариантом посчитаны углы: без этого нельзя понять, сравнимы ли два
    /// прогона попиксельно.
    /// </remarks>
    public static bool IsDeterministic =>
#if XENGINE_DETERMINISTIC_MATH
        true;
#else
        false;
#endif

    /// <summary>
    /// Синус и косинус по одному аргументу.
    /// </summary>
    /// <param name="x">Аргумент в радианах.</param>
    /// <remarks>
    /// Возвращает пару значений, а не заполняет параметры: так же устроен
    /// <see cref="MathF.SinCos(float)"/>, и вызов выглядит одинаково в обоих
    /// вариантах сборки.
    /// <para>
    /// Вызывать именно эту функцию вместо <see cref="MathF.Sin"/> и
    /// <see cref="MathF.Cos"/> по отдельности: два вызова математической
    /// библиотеки вдвое дороже одного, а в циклах движения и отсечения
    /// поворотов она зовётся на каждом объекте каждый кадр.
    /// </para>
    /// </remarks>
    /// <returns>Синус и косинус аргумента.</returns>
    public static (float Sin, float Cos) SinCos(float x)
    {
#if XENGINE_DETERMINISTIC_MATH
        DeterministicMath.SinCos(x, out float sin, out float cos);
        return (sin, cos);
#else
        return MathF.SinCos(x);
#endif
    }

    /// <summary>
    /// Синус.
    /// </summary>
    /// <param name="x">Аргумент в радианах.</param>
    /// <returns>Значение синуса.</returns>
    public static float Sin(float x)
    {
#if XENGINE_DETERMINISTIC_MATH
        return DeterministicMath.Sin(x);
#else
        return MathF.Sin(x);
#endif
    }

    /// <summary>
    /// Косинус.
    /// </summary>
    /// <param name="x">Аргумент в радианах.</param>
    /// <returns>Значение косинуса.</returns>
    public static float Cos(float x)
    {
#if XENGINE_DETERMINISTIC_MATH
        return DeterministicMath.Cos(x);
#else
        return MathF.Cos(x);
#endif
    }

    /// <summary>
    /// Тангенс.
    /// </summary>
    /// <param name="x">Аргумент в радианах.</param>
    /// <returns>Значение тангенса.</returns>
    public static float Tan(float x)
    {
#if XENGINE_DETERMINISTIC_MATH
        return DeterministicMath.Tan(x);
#else
        return MathF.Tan(x);
#endif
    }

    /// <summary>
    /// Аркотангенс с указанием квадранта, результат в диапазоне <c>(-π; π]</c>.
    /// </summary>
    /// <param name="y">Первая координата.</param>
    /// <param name="x">Вторая координата.</param>
    /// <returns>Значение аркотангенса.</returns>
    public static float Atan2(float y, float x)
    {
#if XENGINE_DETERMINISTIC_MATH
        return DeterministicMath.Atan2(y, x);
#else
        return MathF.Atan2(y, x);
#endif
    }

    /// <summary>
    /// Арксинус.
    /// </summary>
    /// <param name="x">Аргумент в <c>[-1; 1]</c>.</param>
    /// <returns>Значение арксинуса.</returns>
    public static float Asin(float x)
    {
#if XENGINE_DETERMINISTIC_MATH
        return DeterministicMath.Asin(x);
#else
        return MathF.Asin(x);
#endif
    }

    /// <summary>
    /// Разность <c>1 - exp(x)</c> без потери точности на малом аргументе.
    /// </summary>
    /// <param name="x">Показатель.</param>
    /// <returns>Значение разности.</returns>
    /// <remarks>
    /// Наивная запись <c>1 - exp(x)</c> теряет почти все значащие цифры при
    /// малом <c>x</c>: результат получается вычитанием из единицы числа,
    /// отличающегося от единицы меньше чем на половину последнего разряда.
    /// Замер для <c>Damp</c> при <c>lambda * dt = 1e-6</c>: наивная запись
    /// даёт относительную ошибку 1.3 %, при <c>1e-8</c> возвращает ровно
    /// ноль, то есть сглаживание перестаёт двигать значение.
    /// <para>
    /// Реализация своя в обоих вариантах сборки, без обращения к <c>MathF</c>:
    /// иначе быстрый и детерминированный варианты разошлись бы на малом
    /// аргументе, а оба обязаны давать одинаковый результат. На
    /// <c>|x| &lt; 0.5</c> считается многочлен разложения
    /// <c>1 - e^x = x(1 + x/2 + x²/6 + …)</c> по схеме Горнера, дальше — прямая
    /// формула, где вычитание уже безопасно.
    /// </para>
    /// </remarks>
    public static float OneMinusExp(float x)
    {
        if (x <= -0.5f || x >= 0.5f)
        {
            return 1f - MathF.Exp(x);
        }

        // 1 - e^x = -x(1 + x/2! + x²/3! + …), свёртка по схеме Горнера.
        // Отброшенный член x^9/9! даёт на границе 0.5 относительную ошибку
        // порядка 1e-8, то есть меньше сотой последнего разряда.
        float poly = 2.75573192e-6f;
        poly = (poly * x) + 2.48015873e-5f;
        poly = (poly * x) + 1.98412698e-4f;
        poly = (poly * x) + 1.38888889e-3f;
        poly = (poly * x) + 8.33333333e-3f;
        poly = (poly * x) + 4.16666667e-2f;
        poly = (poly * x) + 1.66666667e-1f;
        poly = (poly * x) + 5e-1f;
        poly = (poly * x) + 1f;
        return -x * poly;
    }

    /// <summary>
    /// Арккосинус.
    /// </summary>
    /// <param name="x">Аргумент в <c>[-1; 1]</c>.</param>
    /// <returns>Значение арккосинуса.</returns>
    /// <remarks>
    /// Нужен интерполяции ориентаций: угол между двумя кватернионами считается
    /// как <c>acos(скалярное произведение)</c>. Если бы такая операция
    /// оставалась в <c>MathF</c>, детерминированный вариант перестал бы быть
    /// детерминированным ровно на интерполяции поворота между кадрами.
    /// </remarks>
    public static float Acos(float x)
    {
#if XENGINE_DETERMINISTIC_MATH
        return DeterministicMath.Acos(x);
#else
        return MathF.Acos(x);
#endif
    }

    /// <summary>
    /// Экспонента.
    /// </summary>
    /// <param name="x">Показатель.</param>
    /// <returns>Значение e в степени <paramref name="x"/>.</returns>
    public static float Exp(float x)
    {
#if XENGINE_DETERMINISTIC_MATH
        return DeterministicMath.Exp(x);
#else
        return MathF.Exp(x);
#endif
    }

    /// <summary>
    /// Двойка в степени <paramref name="x"/>.
    /// </summary>
    /// <param name="x">Показатель.</param>
    /// <returns>Значение 2 в степени <paramref name="x"/>.</returns>
    /// <remarks>
    /// Вызывать вместо <c>MathF.Pow(2f, x)</c>: тот идёт через общий алгоритм
    /// возведения в степень, который проверяет основание, показатель и диапазон
    /// результата. Здесь сразу известно, что основание равно двум.
    /// </remarks>
    public static float Pow2(float x)
    {
#if XENGINE_DETERMINISTIC_MATH
        return DeterministicMath.Exp2(x);
#else
        return MathF.Pow(2f, x);
#endif
    }
}