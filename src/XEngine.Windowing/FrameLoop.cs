namespace XEngine.Windowing;

/// <summary>
/// Цикл кадра: опрос событий, ограничение времени, шаги симуляции, отрисовка.
/// </summary>
/// <remarks>
/// Цикл написан вручную, а не взят из <c>window.Run()</c> Silk.NET: порядок
/// шагов симуляции должен задавать движок, а не библиотека (16.1). Кадром
/// владеет этот поток (11.3), и цикл написан как однопоточный: разделение на
/// два потока появляется на этапе 14 и использует тот же порядок.
/// <para>
/// Порядок внутри кадра жёсткий и не переставляется по месту:
/// </para>
/// <list type="number">
/// <item>опрос событий окна;</item>
/// <item>время кадра, ограниченное сверху;</item>
/// <item>ноль или более шагов симуляции;</item>
/// <item>отрисовка с множителем интерполяции;</item>
/// <item>показ кадра;</item>
/// <item>завершение кадра степпера: сброс излишка.</item>
/// </list>
/// <para>
/// Перестановка этих шагов меняет смысл кадра: отрисовка до симуляции рисует
/// состояние прошлого кадра, а завершение степпера до отрисовки теряет остаток
/// времени между ними.
/// </para>
/// <para>
/// Показ обязателен и идёт после отрисовки: без него кадр не будет виден, а
/// частота кадров не ограничится ничем, кроме скорости опроса событий.
/// </para>
/// <para>
/// Экземпляр не потокобезопасен намеренно: кадром владеет один поток, и
/// блокировка на каждом кадре стоила бы дороже всей симуляции.
/// </para>
/// </remarks>
public sealed class FrameLoop
{
    private readonly IWindow _window;
    private readonly IFrameClock _clock;
    private readonly FixedStepper _stepper;
    private int _frameNumber;

    /// <summary>
    /// Создаёт цикл кадра.
    /// </summary>
    /// <param name="window">Окно: источник событий и признак закрытия.</param>
    /// <param name="clock">Часы: измеряют время кадра.</param>
    /// <param name="stepper">Механизм фиксированного шага.</param>
    /// <exception cref="ArgumentNullException">Один из аргументов не задан.</exception>
    public FrameLoop(IWindow window, IFrameClock clock, FixedStepper stepper)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(stepper);

        _window = window;
        _clock = clock;
        _stepper = stepper;
    }

    /// <summary>
    /// Выполняет кадры, пока окно не требует закрытия.
    /// </summary>
    /// <param name="target">Цель цикла: шаг симуляции и отрисовка.</param>
    /// <returns>Итоги: сколько кадров и шагов выполнено.</returns>
    /// <remarks>
    /// Вызов возвращает управление, когда окно требует закрытия
    /// (<see cref="IWindow.IsClosing"/>). Проверка выполняется до опроса
    /// событий, поэтому уже закрытое окно не даёт ни одного кадра.
    /// </remarks>
    public FrameLoopStats Run(IFrameLoopTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        int frames = 0;

        while (!_window.IsClosing)
        {
            _window.PollEvents();

            double deltaSeconds = LimitFrameTime(_clock.BeginFrame());
            _stepper.BeginFrame(deltaSeconds);

            int stepsThisFrame = 0;
            while (_stepper.TryTakeStep(out double stepSeconds))
            {
                target.Simulate(stepSeconds, stepsThisFrame);
                stepsThisFrame++;
            }

            FrameContext context = new(
                _frameNumber,
                deltaSeconds,
                _stepper.Alpha,
                _window.FramebufferSize);

            target.Render(context);
            _window.Present();

            _stepper.CompleteFrame();

            _frameNumber++;
            frames++;
        }

        return new FrameLoopStats(frames, _stepper.StepCount);
    }

    /// <summary>
    /// Ограничивает время кадра сверху и возвращает уже ограниченную величину.
    /// </summary>
    /// <remarks>
    /// Ограничение выполняется здесь, а не только в степпере, потому что
    /// ограниченное время должно попасть и в цель цикла: анимации интерполяции
    /// и системы слежения после паузы не должны получать десять секунд одним
    /// кадром. Степпер ограничивает повторно — как защиту от неверного вызова, а
    /// не как второе место, где принимается решение.
    /// </remarks>
    private double LimitFrameTime(double deltaSeconds)
        => Math.Min(Math.Max(deltaSeconds, 0.0), _stepper.MaxFrameTimeSeconds);
}