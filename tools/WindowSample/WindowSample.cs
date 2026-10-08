using XEngine.Core.Configuration;
using XEngine.Core.Logging;
using XEngine.Windowing;

namespace XEngine.Windowing.Sample;

/// <summary>
/// Пример: окно, фиксированный шаг и очистка кадра вручную.
/// </summary>
/// <remarks>
/// Пример существует ради критерия приёмки этапа 3 «появляется окно с очисткой
/// кадра». Очистка здесь сделана вызовом OpenGL напрямую, потому что графического
/// бэкенда ещё нет: он появляется на этапе 4 и заменит этот вызов.
/// <para>
/// Пример не входит в движок: он лежит в <c>tools/</c> как проверочный, а
/// <c>samples/Sandbox</c> появится на этапе 15 вместе с графикой.
/// </para>
/// </remarks>
public static class WindowSample
{
    /// <summary>
    /// Создаёт окно, выполняет несколько кадров с очисткой и завершает работу.
    /// </summary>
    /// <param name="config">Параметры окна.</param>
    /// <param name="log">Журнал.</param>
    /// <param name="frameLimit">
    /// Сколько кадров выполнить; нулевое или отрицательное значение означает
    /// «до запроса закрытия пользователем».
    /// </param>
    /// <exception cref="ArgumentNullException">Конфигурация или журнал не заданы.</exception>
    /// <exception cref="InvalidOperationException">Окно создать не удалось.</exception>
    public static void Run(in WindowConfig config, ILogSink log, int frameLimit = 0)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(log);

        using GlfwWindowBackend backend = new(log);

        if (!backend.IsAvailable)
        {
            throw new InvalidOperationException("Оконная платформа недоступна.");
        }

        using IWindow window = backend.Create(config);
        FixedStepper stepper = new(new SimulationConfig(), log);
        FrameLoop loop = new(window, new SystemFrameClock(), stepper);

        long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        FrameLoopStats stats = loop.Run(new ClearSampleTarget(window, frameLimit));
        double elapsedSeconds = System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalSeconds;

        log.Write(
            LogLevel.Information,
            $"Цикл завершён: кадров {stats.FrameCount}, шагов симуляции {stats.SimulationSteps}, " +
            $"время цикла {elapsedSeconds:F3} с.");

        if (elapsedSeconds > 0.0)
        {
            log.Write(
                LogLevel.Information,
                $"Частота кадров {stats.FrameCount / elapsedSeconds:F1} в секунду, " +
                $"ожидаемая синхронизацией 60.");
        }
    }

    /// <summary>
    /// Цель цикла: считает кадры, выполняет шаги и очищает кадр.
    /// </summary>
    /// <remarks>
    /// Очистка вызывает OpenGL напрямую. Это допустимо только здесь: в движке
    /// вызовы GL принадлежат бэкенду графики и выполняются на потоке рендера
    /// (инвариант 7). Пример запускается однопоточно, поэтому отдельного потока
    /// рендера ещё нет.
    /// </remarks>
    private sealed class ClearSampleTarget : IFrameLoopTarget
    {
        /// <summary>Цвет очистки: тёмно-синий, чтобы пустой кадр отличался от чёрного.</summary>
        private const float ClearRed = 0.15f;

        /// <summary>Зелёная составляющая цвета очистки.</summary>
        private const float ClearGreen = 0.35f;

        /// <summary>Синяя составляющая цвета очистки.</summary>
        private const float ClearBlue = 0.55f;

        private readonly IWindow _window;
        private readonly Silk.NET.OpenGL.GL _gl;
        private readonly int _frameLimit;
        private int _frames;

        public ClearSampleTarget(IWindow window, int frameLimit)
        {
            _window = window;
            _frameLimit = frameLimit;

            // Функции GL берутся из контекста окна: функции из чужого контекста
            // работают с чужим состоянием, и ошибка проявилась бы как случайные
            // сбои рисования. Экземпляр создаётся один раз, а не на кадре.
            _gl = Silk.NET.OpenGL.GL.GetApi(name =>
            {
                IntPtr address = window.GetGraphicsProcedure(name);
                return address;
            });
        }

        public void Simulate(double stepSeconds, int stepNumber)
        {
            // Симуляции на этапе 3 ещё нет: этап 3 проверяет, что шаги доходят до
            // цели в правильном порядке и в правильном количестве.
        }

        public void Render(in FrameContext context)
        {
            _gl.ClearColor(ClearRed, ClearGreen, ClearBlue, 1f);
            _gl.Clear((uint)Silk.NET.OpenGL.GLEnum.ColorBufferBit);

            _frames++;
            if (_frameLimit > 0 && _frames >= _frameLimit)
            {
                _window.RequestClose();
            }
        }
    }
}