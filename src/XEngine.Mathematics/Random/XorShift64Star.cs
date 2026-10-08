namespace XEngine.Mathematics;

/// <summary>
/// Быстрый детерминированный генератор xorshift64*.
/// Достаточен для игровых задач (разброс, выбор варианта, шум) и заметно быстрее
/// <see cref="System.Random"/>, потому что не хранит объект состояния и не блокирует потоки.
/// </summary>
public sealed class XorShift64Star : IRandomSource
{
    private ulong _state;
    private readonly ulong _initialState;

    /// <summary>
    /// Создаёт генератор с заданным зерном.
    /// </summary>
    /// <param name="seed">Зерно. Ноль заменяется на ненулевое значение, иначе генератор вырождается.</param>
    public XorShift64Star(ulong seed = 0x9E3779B97F4A7C15UL)
    {
        ulong sanitized = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        _initialState = sanitized;
        _state = sanitized;
    }

    /// <inheritdoc/>
    public ulong NextUInt64()
    {
        unchecked
        {
            ulong x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            return x * 0x2545F4914F6CDD1DUL;
        }
    }

    /// <inheritdoc/>
    public float NextFloat()
    {
        // 24 значащих бита дают равномерное распределение в [0; 1).
        return (NextUInt64() >> 40) * (1f / 16777216f);
    }

    /// <inheritdoc/>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                maxExclusive,
                "Верхняя граница должна быть больше нижней.");
        }

        // Размах обязан считаться в long: разность int переполняется при диапазоне
        // шире int.MaxValue (например, NextInt(int.MinValue, int.MaxValue) даёт
        // -1) и превращается в огромный ulong, из которого возвращается
        // произвольное значение, в том числе отрицательное.
        ulong range = (ulong)((long)maxExclusive - minInclusive);
        return (int)((long)minInclusive + (long)(NextUInt64() % range));
    }

    /// <inheritdoc/>
    public void Reset() => _state = _initialState;
}
