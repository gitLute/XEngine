namespace XEngine.Windowing;

/// <summary>
/// Часы, время в которых задаётся вручную.
/// </summary>
/// <remarks>
/// Нужны тестам цикла и любому прогону без реального времени (17.6): тест
/// задаёт время кадра точно и проверяет, сколько шагов из этого получилось.
/// Через него же проверяется воспроизводимость симуляции: одна и та же
/// последовательность кадров обязана давать одну и ту же последовательность
/// шагов.
/// </remarks>
public sealed class ManualFrameClock : IFrameClock
{
    private double _lastFrameSeconds;

    /// <summary>
    /// Создаёт часы, отсчитывающие от заданного момента.
    /// </summary>
    /// <param name="startSeconds">
    /// Начальное время в секундах. По умолчанию ноль: абсолютная точка отсчёта
    /// не имеет значения, важны только интервалы.
    /// </param>
    public ManualFrameClock(double startSeconds = 0.0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startSeconds);

        NowSeconds = startSeconds;
        _lastFrameSeconds = startSeconds;
    }

    /// <inheritdoc/>
    public double NowSeconds { get; private set; }

    /// <summary>
    /// Сдвигает время на заданное число секунд вперёд.
    /// </summary>
    /// <param name="seconds">Приращение времени в секундах.</param>
    /// <exception cref="ArgumentOutOfRangeException">Приращение отрицательно.</exception>
    public void Advance(double seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);

        NowSeconds += seconds;
    }

    /// <summary>
    /// Задаёт текущее время. Время назад не задаётся: расхождение времени
    /// кадров означало бы шаг симуляции с отрицательным временем.
    /// </summary>
    /// <param name="seconds">Новое время в секундах; не меньше текущего.</param>
    /// <exception cref="ArgumentOutOfRangeException">Время меньше текущего.</exception>
    public void AdvanceTo(double seconds)
    {
        if (seconds < NowSeconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(seconds),
                seconds,
                "Часы не идут назад: кадры идут по порядку.");
        }

        NowSeconds = seconds;
    }

    /// <inheritdoc/>
    public double BeginFrame()
    {
        double frameSeconds = NowSeconds - _lastFrameSeconds;
        _lastFrameSeconds = NowSeconds;
        return frameSeconds;
    }
}