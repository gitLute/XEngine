namespace XEngine.Mathematics;

/// <summary>
/// Источник случайных чисел.
/// Интерфейс позволяет использовать детерминированные генераторы вместо
/// <see cref="System.Random"/>, чтобы поведение игры можно было воспроизвести по зерну.
/// </summary>
public interface IRandomSource
{
    /// <summary>
    /// Возвращает следующее беззнаковое 64-битное число.
    /// </summary>
    /// <returns>Случайное число.</returns>
    ulong NextUInt64();

    /// <summary>
    /// Возвращает число с плавающей точкой в диапазоне [0; 1).
    /// </summary>
    /// <returns>Случайное число.</returns>
    float NextFloat();

    /// <summary>
    /// Возвращает целое число в диапазоне [minInclusive; maxExclusive).
    /// </summary>
    /// <param name="minInclusive">Нижняя граница включительно.</param>
    /// <param name="maxExclusive">Верхняя граница исключительно.</param>
    /// <returns>Случайное целое число.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Границы заданы неверно.</exception>
    int NextInt(int minInclusive, int maxExclusive);

    /// <summary>
    /// Сбрасывает состояние генератора в исходное зерно.
    /// </summary>
    void Reset();
}
