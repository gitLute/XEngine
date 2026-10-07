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
    public static float OutCubic(float t) => 1f - (float)Math.Pow(1f - t, 3);

    /// <summary>
    /// Кубическое ускорение и замедление.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float InOutCubic(float t)
        => t < 0.5f ? 4f * t * t * t : 1f - 4f * (float)Math.Pow(1f - t, 3);

    /// <summary>
    /// Синусоидальное ускорение.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float InSine(float t) => 1f - MathF.Cos(t * MathF.PI * 0.5f);

    /// <summary>
    /// Синусоидальное замедление.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float OutSine(float t) => MathF.Sin(t * MathF.PI * 0.5f);

    /// <summary>
    /// Синусоидальное ускорение и замедление.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float InOutSine(float t) => -(MathF.Cos(MathF.PI * t) - 1f) * 0.5f;

    /// <summary>
    /// Экспоненциальное ускорение.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float InExpo(float t) => t <= 0f ? 0f : (float)Math.Pow(2, 10 * t - 10);

    /// <summary>
    /// Экспоненциальное замедление.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <returns>Значение кривой.</returns>
    public static float OutExpo(float t) => t >= 1f ? 1f : 1f - (float)Math.Pow(2, -10 * t);

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
            ? (float)Math.Pow(2, 20 * t - 10) * 0.5f
            : (1f - (float)Math.Pow(2, -20 * t + 10)) * 0.5f + 0.5f;
    }

    /// <summary>
    /// Ускорение с небольшим перелётом.
    /// </summary>
    /// <param name="t">Параметр кривой.</param>
    /// <param name="overshoot">Коэффициент перелёта, обычно 1..3.</param>
    /// <returns>Значение кривой.</returns>
    public static float InBack(float t, float overshoot = 1.70158f)
    {
        float x = t - 1f;
        return x * x * ((overshoot + 1f) * x + overshoot) + 1f;
    }

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

        return -(float)Math.Pow(2, 10 * t - 10) * MathF.Sin((t * 10f - 10.75f) * (2f * MathF.PI / 3f));
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

        return (float)Math.Pow(2, -10 * t) * MathF.Sin((t * 10f - 0.75f) * (2f * MathF.PI / 3f)) + 1f;
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
}
