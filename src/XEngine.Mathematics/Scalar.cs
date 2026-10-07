namespace XEngine.Mathematics;

/// <summary>
/// Скалярные математические операции и константы.
/// Общая точка для значений, которые не относятся к векторам и матрицам.
/// </summary>
public static class Scalar
{
    /// <summary>
    /// Допуск для сравнения чисел с плавающей точкой.
    /// </summary>
    public const float Epsilon = 1e-6f;

    /// <summary>
    /// Большой допуск: для величин, измеряемых в пикселях или мирах.
    /// </summary>
    public const float LargeEpsilon = 1e-4f;

    /// <summary>
    /// Возвращает знак числа: -1, 0 или 1.
    /// </summary>
    /// <param name="value">Исходное число.</param>
    /// <returns>Знак числа.</returns>
    public static int Sign(float value) => value > 0 ? 1 : value < 0 ? -1 : 0;

    /// <summary>
    /// Проверяет, что значение близко к нулю с точностью <see cref="Epsilon"/>.
    /// </summary>
    /// <param name="value">Проверяемое значение.</param>
    /// <returns><c>true</c>, если значение близко к нулю.</returns>
    public static bool IsNearlyZero(float value) => float.Abs(value) <= Epsilon;

    /// <summary>
    /// Проверяет равенство двух чисел с допуском.
    /// </summary>
    /// <param name="a">Первое число.</param>
    /// <param name="b">Второе число.</param>
    /// <param name="epsilon">Допуск.</param>
    /// <returns><c>true</c>, если числа равны в пределах допуска.</returns>
    public static bool IsNearlyEqual(float a, float b, float epsilon = Epsilon)
        => float.Abs(a - b) <= epsilon;

    /// <summary>
    /// Приводит значение к ближайшему кратному шага.
    /// </summary>
    /// <param name="value">Исходное значение.</param>
    /// <param name="step">Шаг. Должен быть больше нуля.</param>
    /// <returns>Значение, округлённое до кратного шага.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Шаг меньше или равен нулю.</exception>
    public static float Snap(float value, float step)
    {
        if (step <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, "Шаг должен быть больше нуля.");
        }

        return (float)Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
    }

    /// <summary>
    /// Ограничивает значение диапазоном.
    /// </summary>
    /// <param name="value">Исходное значение.</param>
    /// <param name="min">Нижняя граница.</param>
    /// <param name="max">Верхняя граница.</param>
    /// <returns>Значение в диапазоне <paramref name="min"/>..<paramref name="max"/>.</returns>
    /// <exception cref="ArgumentException">Минимальная граница больше максимальной.</exception>
    public static float Clamp(float value, float min, float max)
    {
        if (min > max)
        {
            throw new ArgumentException("Минимальная граница больше максимальной.", nameof(min));
        }

        return value < min ? min : value > max ? max : value;
    }

    /// <summary>
    /// Переводит радианы в градусы.
    /// </summary>
    /// <param name="radians">Угол в радианах.</param>
    /// <returns>Угол в градусах.</returns>
    public static double ToDegrees(double radians) => radians * (180.0 / Math.PI);

    /// <summary>
    /// Переводит градусы в радианы.
    /// </summary>
    /// <param name="degrees">Угол в градусах.</param>
    /// <returns>Угол в радианах.</returns>
    public static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);
}
