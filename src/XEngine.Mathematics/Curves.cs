using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Кривые ускорения для анимаций, эффектов и движения камеры.
/// Все функции определены на отрезке 0..1 и дают значения в ожидаемом для вида диапазоне.
/// </summary>
public static class Curves
{
    /// <summary>
    /// Линейная кривая без ускорения.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float Linear(float t) => t;

    /// <summary>
    /// Квадратичное ускорение.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float InQuad(float t) => t * t;

    /// <summary>
    /// Квадратичное замедление.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);

    /// <summary>
    /// Квадратичное ускорение и замедление.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float InOutQuad(float t)
        => t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);

    /// <summary>
    /// Кубическое ускорение.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float InCubic(float t) => t * t * t;

    /// <summary>
    /// Кубическое замедление.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    /// <remarks>
    /// Раскрытое тождество <c>1 − (1 − t)³ = t·(3 − 3t + t²)</c>. Запись через
    /// <c>1 − t</c> теряла точность в начале интервала: при <c>t = 1e-4</c>
    /// ошибка была 0.14 %, потому что результат получается вычитанием из единицы
    /// числа, очень близкого к ней. Раскрытая форма умножает на <c>t</c>, то есть
    /// величину того же порядка, что и результат.
    /// </remarks>
    public static float OutCubic(float t) => t * ((t * (t - 3f)) + 3f);

    /// <summary>
    /// Кубическое ускорение и замедление.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float InOutCubic(float t)
    {
        float inverse = 1f - t;
        return t < 0.5f ? 4f * t * t * t : 1f - (4f * inverse * inverse * inverse);
    }

    /// <summary>
    /// Синусоидальное ускорение.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    /// <remarks>
    /// Формула <c>1 − cos(t·π/2)</c> здесь непригодна: при малом <c>t</c> косинус
    /// отличается от единицы меньше чем на половину последнего разряда, и
    /// вычитание съедает весь результат. Например при <c>t = 1e-4</c> исходная
    /// форма возвращает ровно ноль вместо <c>1.2337e-8</c>. Поэтому используется
    /// равносильная устойчивая форма <c>2·sin²(t·π/4)</c>, где результат
    /// получается умножением, а не вычитанием близких чисел.
    /// </remarks>
    public static float InSine(float t)
    {
        float sine = Trig.Sin(t * (MathF.PI * 0.25f));
        return (2f * sine) * sine;
    }

    /// <summary>
    /// Синусоидальное замедление.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float OutSine(float t) => Trig.Sin(t * MathF.PI * 0.5f);

    /// <summary>
    /// Синусоидальное ускорение и замедление.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    /// <remarks>
    /// Устойчивая форма <c>(1 − cos(πt))/2 = sin²(πt/2)</c>, по той же причине,
    /// что и в <see cref="InSine"/>: исходная форма теряет все значащие цифры
    /// около нуля (при <c>t = 1e-4</c> ошибка 20 %).
    /// </remarks>
    public static float InOutSine(float t)
    {
        float sine = Trig.Sin(t * (MathF.PI * 0.5f));
        return sine * sine;
    }

    /// <summary>
    /// Экспоненциальное ускорение.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float InExpo(float t) => t <= 0f ? 0f : Trig.Pow2((10f * t) - 10f);

    /// <summary>
    /// Экспоненциальное замедление.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float OutExpo(float t) => t >= 1f ? 1f : 1f - Trig.Pow2(-10f * t);

    /// <summary>
    /// Экспоненциальное ускорение и замедление.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float InOutExpo(float t)
    {
        if (t <= 0f)
        {
            return 0f;
        }

        if (t >= 1f)
        {
            return 1f;
        }

        return t < 0.5f
            ? Trig.Pow2((20f * t) - 10f) * 0.5f
            : (1f - Trig.Pow2((-20f * t) + 10f)) * 0.5f + 0.5f;
    }

    /// <summary>
    /// Ускорение с перелётом назад: кривая проваливается ниже нуля, прежде чем
    /// начать движение.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <param name="overshoot">Коэффициент перелёта, обычно 1..3.</param>
    /// <returns>Значение кривой.</returns>
    /// <remarks>
    /// Формула Пеннера <c>t²((s+1)t − s)</c>: знак перед <paramref name="overshoot"/>
    /// отрицательный, и именно он даёт провал ниже нуля. С плюсом получается
    /// <see cref="OutBack"/>, то есть дословная копия замедления.
    /// </remarks>
    public static float InBack(float t, float overshoot = 1.70158f)
        => t * t * ((overshoot + 1f) * t - overshoot);

    /// <summary>
    /// Замедление с небольшим перелётом.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <param name="overshoot">Коэффициент перелёта, обычно 1..3.</param>
    /// <returns>Значение кривой.</returns>
    public static float OutBack(float t, float overshoot = 1.70158f)
    {
        float x = t - 1f;
        return x * x * ((overshoot + 1f) * x + overshoot) + 1f;
    }

    /// <summary>
    /// Ускорение с упругим эффектом.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float InElastic(float t)
    {
        if (t <= 0f)
        {
            return 0f;
        }

        if (t >= 1f)
        {
            return 1f;
        }

        return -Trig.Pow2((10f * t) - 10f) * Trig.Sin((t * 10f - 10.75f) * (2f * MathF.PI / 3f));
    }

    /// <summary>
    /// Замедление с упругим эффектом.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float OutElastic(float t)
    {
        if (t <= 0f)
        {
            return 0f;
        }

        if (t >= 1f)
        {
            return 1f;
        }

        return Trig.Pow2(-10f * t) * Trig.Sin((t * 10f - 0.75f) * (2f * MathF.PI / 3f)) + 1f;
    }

    /// <summary>
    /// Замедление с отскоком.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float OutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1f / d1)
        {
            return n1 * t * t;
        }

        if (t < 2f / d1)
        {
            t -= 1.5f / d1;
            return n1 * t * t + 0.75f;
        }

        if (t < 2.5f / d1)
        {
            t -= 2.25f / d1;
            return n1 * t * t + 0.9375f;
        }

        t -= 2.625f / d1;
        return n1 * t * t + 0.984375f;
    }

    /// <summary>
    /// Вычисляет значение кривой по её функции-делегату.
    /// </summary>
    /// <param name="curve">Функция кривой.</param>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой при ограниченном параметре 0..1.</returns>
    public static float Evaluate(Func<float, float> curve, float t)
    {
        ArgumentNullException.ThrowIfNull(curve);
        return curve(Interpolation.Clamp01(t));
    }

    /// <summary>
    /// Вычисляет позицию на кривой Безье второго порядка.
    /// </summary>
    /// <param name="controlPointA">Первая контрольная точка.</param>
    /// <param name="controlPointB">Вторая контрольная точка.</param>
    /// <param name="controlPointC">Конечная точка.</param>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Точка кривой.</returns>
    public static Vector2 QuadraticBezier(Vector2 controlPointA, Vector2 controlPointB, Vector2 controlPointC, float t)
    {
        float u = 1f - t;
        return controlPointA * (u * u) + controlPointB * (2f * u * t) + controlPointC * (t * t);
    }

    /// <summary>
    /// Вычисляет позицию на трёхмерной кривой Безье второго порядка.
    /// </summary>
    /// <param name="controlPointA">Первая контрольная точка.</param>
    /// <param name="controlPointB">Вторая контрольная точка.</param>
    /// <param name="controlPointC">Конечная точка.</param>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Точка кривой.</returns>
    /// <remarks>
    /// Отдельный тип сглаживания не вводится: интерполяция величин уже покрыта
    /// <see cref="Interpolation"/>, а здесь только форма кривой.
    /// </remarks>
    public static Vector3 QuadraticBezier(Vector3 controlPointA, Vector3 controlPointB, Vector3 controlPointC, float t)
    {
        float u = 1f - t;
        return controlPointA * (u * u) + controlPointB * (2f * u * t) + controlPointC * (t * t);
    }

    /// <summary>
    /// Вычисляет позицию на трёхмерной кривой Безье третьего порядка.
    /// </summary>
    /// <param name="controlPointA">Первая контрольная точка.</param>
    /// <param name="controlPointB">Вторая контрольная точка.</param>
    /// <param name="controlPointC">Третья контрольная точка.</param>
    /// <param name="controlPointD">Конечная точка.</param>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Точка кривой.</returns>
    public static Vector3 CubicBezier(
        Vector3 controlPointA,
        Vector3 controlPointB,
        Vector3 controlPointC,
        Vector3 controlPointD,
        float t)
    {
        float u = 1f - t;
        float uu = u * u;
        float tt = t * t;
        return (controlPointA * (uu * u))
            + (controlPointB * (3f * uu * t))
            + (controlPointC * (3f * u * tt))
            + (controlPointD * (tt * t));
    }
}
