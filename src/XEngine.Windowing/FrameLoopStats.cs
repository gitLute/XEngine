namespace XEngine.Windowing;

/// <summary>
/// Итоги работы цикла за время выполнения.
/// </summary>
/// <remarks>
/// Возвращаются из <see cref="FrameLoop.Run"/> не для отчёта, а для диагностики:
/// тест проверяет по ним, что цикл остановился там, где должен, а не по
/// внутренним счётчикам цикла.
/// </remarks>
/// <param name="FrameCount">Сколько кадров выполнено.</param>
/// <param name="SimulationSteps">Сколько шагов симуляции выполнено за все кадры.</param>
public readonly record struct FrameLoopStats(int FrameCount, int SimulationSteps)
{
    /// <summary>
    /// Ни одного кадра: цикл не начал работу.
    /// </summary>
    public static FrameLoopStats Empty => default;
}