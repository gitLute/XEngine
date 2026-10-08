namespace XEngine.Core.Configuration;

/// <summary>
/// Параметры фиксированного шага и порядка исполнения кадра.
/// </summary>
/// <remarks>
/// Появляется уже на этапе 3: цикл кадра читает эти значения, а не собственные
/// константы (14.1). Названия полей уточнены суффиксом <c>Seconds</c>, чтобы
/// единица измерения была видна в месте использования.
/// </remarks>
public sealed record SimulationConfig
{
    /// <summary>
    /// Шаг симуляции по умолчанию: 60 шагов в секунду.
    /// </summary>
    public const double DefaultTimeStepSeconds = 1.0 / 60.0;

    /// <summary>
    /// Предел числа шагов за один кадр по умолчанию (8.3).
    /// </summary>
    public const int DefaultMaxSubSteps = 5;

    /// <summary>
    /// Предел времени кадра по умолчанию: после сворачивания окна не делается
    /// сорока шагов (8.3).
    /// </summary>
    public const double DefaultMaxFrameTimeSeconds = 0.25;

    /// <summary>
    /// Бюджет времени шага по умолчанию: 8 мс (8.3a).
    /// </summary>
    public const double DefaultStepBudgetSeconds = 0.008;

    /// <summary>
    /// Темп времени по умолчанию: единица означает «как есть».
    /// </summary>
    public const double DefaultTimeScale = 1.0;

    /// <summary>
    /// Длительность фиксированного шага в секундах.
    /// </summary>
    public double TimeStepSeconds { get; init; } = DefaultTimeStepSeconds;

    /// <summary>
    /// Сколько шагов симуляции допускается за один кадр. Излишек аккумулятора
    /// сверх этого числа сбрасывается: догоняющие шаги ухудшают проблему
    /// (8.3a).
    /// </summary>
    public int MaxSubSteps { get; init; } = DefaultMaxSubSteps;

    /// <summary>
    /// Верхняя граница времени кадра в секундах. Время кадра ограничивается
    /// до прибавления к аккумулятору, иначе после сворачивания окна симуляция
    /// получила бы сорок шагов разом.
    /// </summary>
    public double MaxFrameTimeSeconds { get; init; } = DefaultMaxFrameTimeSeconds;

    /// <summary>
    /// Бюджет времени одного шага в секундах. Не прерывает шаг, а включает
    /// диагностику после превышения (8.3a).
    /// </summary>
    public double StepBudgetSeconds { get; init; } = DefaultStepBudgetSeconds;

    /// <summary>
    /// Темп игрового времени. Значение меньше единицы замедляет время, больше —
    /// ускоряет; на аккумулятор влияет до ограничения числа шагов.
    /// </summary>
    public double TimeScale { get; init; } = DefaultTimeScale;

    /// <summary>
    /// Режим исполнения цикла.
    /// </summary>
    public ThreadingMode Threading { get; init; } = ThreadingMode.SingleThreaded;

    /// <summary>
    /// Интерполировать ли горячие данные снапшота при отрисовке. Необходима,
    /// когда частота кадров выше частоты симуляции; при равных частотах
    /// выключается, чтобы не выполнять лишнюю работу (инвариант 6c, этап 14).
    /// </summary>
    public bool Interpolate { get; init; } = true;

    /// <summary>
    /// Проверяет, что параметры пригодны для цикла кадра.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Шаг неположителен, предел шагов меньше единицы, предел кадра или бюджет
    /// шага неположительны, темп времени неположителен или не конечен.
    /// </exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(TimeStepSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxFrameTimeSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(StepBudgetSeconds);

        if (MaxSubSteps < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxSubSteps),
                MaxSubSteps,
                "За кадр должен выполняться хотя бы один шаг симуляции.");
        }

        if (TimeScale <= 0.0 || !double.IsFinite(TimeScale))
        {
            throw new ArgumentOutOfRangeException(
                nameof(TimeScale),
                TimeScale,
                "Темп времени должен быть положительным конечным числом.");
        }
    }
}