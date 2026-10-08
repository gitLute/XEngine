using XEngine.Core.Configuration;

namespace XEngine.Windowing.Tests;

/// <summary>
/// Заглушка окна: тест цикла не должен требовать дисплея и GLFW (17.6).
/// </summary>
internal sealed class FakeWindow : IWindow
{
    private readonly ManualFrameClock? _clock;
    private readonly double _frameSeconds;
    private bool _isClosing;

    /// <summary>
    /// Создаёт заглушку с указанным размером фреймбуфера.
    /// </summary>
    /// <param name="framebufferSize">Размер области рисования.</param>
    public FakeWindow(WindowSize framebufferSize)
    {
        FramebufferSize = framebufferSize;
    }

    /// <summary>
    /// Создаёт заглушку с размером 800x600.
    /// </summary>
    public FakeWindow()
        : this(new WindowSize(800, 600))
    {
    }

    /// <summary>
    /// Создаёт заглушку, которая двигает ручные часы на заданный интервал при
    /// каждом опросе событий.
    /// </summary>
    /// <param name="clock">Часы, которые двигает заглушка.</param>
    /// <param name="frameSeconds">Длительность кадра в секундах.</param>
    public FakeWindow(ManualFrameClock clock, double frameSeconds)
    {
        _clock = clock;
        _frameSeconds = frameSeconds;
    }

    /// <inheritdoc/>
    public string Title { get; set; } = "FakeWindow";

    /// <inheritdoc/>
    public WindowSize Size { get; set; } = new(800, 600);

    /// <inheritdoc/>
    public WindowSize FramebufferSize { get; private set; }

    /// <inheritdoc/>
    public bool IsFocused { get; set; } = true;

    /// <inheritdoc/>
    public void RequestClose() => _isClosing = true;

    /// <inheritdoc/>
    public bool IsClosing => _isClosing;

    /// <summary>
    /// Имитирует нажатие кнопки закрытия пользователем.
    /// </summary>
    public void SimulateUserClose() => _isClosing = true;

    /// <summary>
    /// Имитирует изменение размера окна пользователем.
    /// </summary>
    /// <param name="framebufferSize">Новый размер области рисования.</param>
    public void SimulateResize(WindowSize framebufferSize) => FramebufferSize = framebufferSize;

    /// <summary>
    /// Сколько раз опрашивались события.
    /// </summary>
    public int PollCount { get; private set; }

    /// <summary>
    /// Сколько раз освобождалось окно.
    /// </summary>
    public int DisposeCount { get; private set; }

    /// <summary>
    /// Опрашивает платформу и, если заданы часы, двигает время на длительность
    /// кадра.
    /// </summary>
    /// <remarks>
    /// Время двигается здесь, а не в цели цикла, потому что между кадрами
    /// проходит время независимо от того, что делает симуляция. Часы в тестах
    /// ручные, и без этого шага цикл получал бы нулевое время кадра и не
    /// выполнил бы ни одного шага.
    /// </remarks>
    public void PollEvents()
    {
        PollCount++;

        if (_clock is not null)
        {
            _clock.Advance(_frameSeconds);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        DisposeCount++;
        _isClosing = true;
    }
}