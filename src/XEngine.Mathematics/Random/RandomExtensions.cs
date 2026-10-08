using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Методы генерации случайных величин поверх <see cref="IRandomSource"/>.
/// Вынесены в расширения, чтобы вызов выглядел как <c>random.NextRange(1f, 5f)</c>,
/// а реализация оставалась заменяемой.
/// </summary>
public static class RandomExtensions
{
    /// <summary>
    /// Возвращает число с плавающей точкой в диапазоне [min; max).
    /// </summary>
    /// <param name="random">Источник случайности.</param>
    /// <param name="min">Нижняя граница.</param>
    /// <param name="max">Верхняя граница.</param>
    /// <returns>Случайное число.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Верхняя граница меньше нижней.</exception>
    public static float NextRange(this IRandomSource random, float min, float max)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (max < min)
        {
            throw new ArgumentOutOfRangeException(nameof(max), max, "Верхняя граница меньше нижней.");
        }

        return min + (max - min) * random.NextFloat();
    }

    /// <summary>
    /// Возвращает число с плавающей точкой в диапазоне [-range; range].
    /// </summary>
    /// <param name="random">Источник случайности.</param>
    /// <param name="range">Максимальное абсолютное значение.</param>
    /// <returns>Случайное число.</returns>
    public static float NextSymmetric(this IRandomSource random, float range) => random.NextRange(-range, range);

    /// <summary>
    /// Возвращает единичное направление, равномерно распределённое по окружности.
    /// </summary>
    /// <param name="random">Источник случайности.</param>
    /// <returns>Единичный вектор.</returns>
    /// <exception cref="ArgumentNullException">Источник случайности отсутствует.</exception>
    public static Vector2 NextDirection(this IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        // NextFloat() лежит в [0; 1), значит угол лежит в [0; 2π) — нормализация
        // тут не нужна, а она стоила вызова Math.IEEERemainder на каждый вызов.
        // Синус и косинус берутся одним вызовом, а Angle приводил бы угол к
        // float и обратно без пользы.
        float angle = random.NextFloat() * MathF.Tau;
        (float sin, float cos) = Trig.SinCos(angle);
        return new Vector2(cos, sin);
    }

    /// <summary>
    /// Возвращает случайную точку внутри единичной окружности с равномерным распределением по площади.
    /// </summary>
    /// <param name="random">Источник случайности.</param>
    /// <returns>Вектор с длиной меньше единицы.</returns>
    /// <exception cref="ArgumentNullException">Источник случайности отсутствует.</exception>
    public static Vector2 NextInsideUnitCircle(this IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        float angle = random.NextFloat() * MathF.Tau;
        float radius = MathF.Sqrt(random.NextFloat());
        (float sin, float cos) = Trig.SinCos(angle);
        return new Vector2(cos * radius, sin * radius);
    }

    /// <summary>
    /// Возвращает случайную точку на единичной окружности.
    /// </summary>
    /// <param name="random">Источник случайности.</param>
    /// <returns>Вектор единичной длины.</returns>
    public static Vector2 NextOnUnitCircle(this IRandomSource random) => random.NextDirection();

    /// <summary>
    /// Возвращает случайный элемент массива.
    /// </summary>
    /// <typeparam name="T">Тип элемента.</typeparam>
    /// <param name="random">Источник случайности.</param>
    /// <param name="items">Массив элементов.</param>
    /// <returns>Случайный элемент.</returns>
    /// <exception cref="ArgumentException">Массив пуст.</exception>
    public static T NextItem<T>(this IRandomSource random, T[] items)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(items);
        if (items.Length == 0)
        {
            throw new ArgumentException("Массив пуст.", nameof(items));
        }

        return items[random.NextInt(0, items.Length)];
    }

    /// <summary>
    /// Возвращает случайный элемент диапазона с весами.
    /// </summary>
    /// <param name="random">Источник случайности.</param>
    /// <param name="weights">Веса вариантов, должны быть неотрицательными.</param>
    /// <returns>Индекс выбранного варианта.</returns>
    /// <exception cref="ArgumentException">Список весов пуст или все веса нулевые.</exception>
    public static int NextWeightedIndex(this IRandomSource random, ReadOnlySpan<float> weights)
    {
        ArgumentNullException.ThrowIfNull(random);

        float total = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] < 0f)
            {
                throw new ArgumentException("Веса должны быть неотрицательными.", nameof(weights));
            }

            total += weights[i];
        }

        // Порог — ноль, а не Scalar.Epsilon: документированный контракт обещает
        // исключение только когда все веса нулевые. Сравнение с допуском
        // отбрасывало бы и набор весов 1e-6, который ненулевой и в котором
        // выбор варианта вполне определён.
        if (total <= 0f)
        {
            throw new ArgumentException("Сумма весов должна быть больше нуля.", nameof(weights));
        }

        float threshold = random.NextFloat() * total;
        float accumulated = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            accumulated += weights[i];
            if (threshold < accumulated)
            {
                return i;
            }
        }

        return weights.Length - 1;
    }

    /// <summary>
    /// Возвращает случайный угол в полном обороте.
    /// </summary>
    /// <param name="random">Источник случайности.</param>
    /// <returns>Случайный угол.</returns>
    /// <exception cref="ArgumentNullException">Источник случайности отсутствует.</exception>
    public static Angle NextAngle(this IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return Angle.FromRadians(random.NextFloat() * Angle.Tau);
    }

    /// <summary>
    /// Возвращает случайную точку внутри AABB.
    /// </summary>
    /// <param name="random">Источник случайности.</param>
    /// <param name="bounds">Ограничивающий прямоугольник.</param>
    /// <returns>Случайная точка внутри.</returns>
    public static Vector2 NextInside(this IRandomSource random, Aabb2 bounds)
    {
        ArgumentNullException.ThrowIfNull(random);
        return new Vector2(
            random.NextRange(bounds.Min.X, bounds.Max.X),
            random.NextRange(bounds.Min.Y, bounds.Max.Y));
    }
}
