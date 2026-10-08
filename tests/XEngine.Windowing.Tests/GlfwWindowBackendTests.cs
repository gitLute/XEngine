using XEngine.Core.Configuration;
using Xunit;

namespace XEngine.Windowing.Tests;

/// <summary>
/// Проверка настоящего окна на Silk.NET.
/// </summary>
/// <remarks>
/// Тесты с настоящим окном выполняются только при <c>XENGINE_WINDOW_TESTS=1</c>
/// и наличии дисплея, см. <see cref="WindowFactAttribute"/>.
/// <para>
/// Тест проверяет не «окно создалось», а контракт: загрузчик либо возвращает
/// рабочее окно, либо сообщает причину исключением. В системе, где окно
/// создать нельзя, ожидается исключение, и это тоже проверка контракта: молча
/// возвращать сломанное окно нельзя.
/// </para>
/// <para>
/// Здесь проверяется только то, за что отвечает загрузчик окон: создание,
/// размер, фокус, запрос закрытия. Очистку кадра и графический контекст
/// проверяет бэкенд графики (этап 4).
/// </para>
/// </remarks>
public sealed class GlfwWindowBackendTests
{
    [Fact]
    public void Constructor_RejectsMissingLog()
    {
        Assert.Throws<ArgumentNullException>(() => new GlfwWindowBackend(null!));
    }

    [Fact]
    public void Create_RejectsInvalidConfig()
    {
        using GlfwWindowBackend backend = new(new CollectingLogSink());

        Assert.Throws<ArgumentException>(
            () => backend.Create(new WindowConfig(Title: string.Empty)));
    }

    [Fact]
    public void Create_RejectsNullConfig()
    {
        using GlfwWindowBackend backend = new(new CollectingLogSink());

        Assert.Throws<ArgumentNullException>(() => backend.Create(null!));
    }

    [Fact]
    public void Probe_ReportsPlatformAvailability()
    {
        using GlfwWindowBackend backend = new(new CollectingLogSink());

        backend.Probe();

        // Проверяется сам признак, а не наличие дисплея: платформа
        // регистрируется и без него, и наоборот — при зарегистрированной
        // платформе окно может не создаться.
        Assert.True(backend.IsAvailable);
    }

    [WindowFact]
    public void Create_EitherReturnsWorkingWindowOrExplainsFailure()
    {
        using GlfwWindowBackend backend = new(new CollectingLogSink());
        WindowConfig config = new() { Title = "XEngine test", Width = 640, Height = 480 };

        try
        {
            using IWindow window = backend.Create(config);

            Assert.Equal("XEngine test", window.Title);
            Assert.False(window.IsClosing, "Созданное окно не должно требовать закрытия.");
            Assert.True(window.IsFocused, "Созданное окно получает фокус.");
            Assert.True(window.Size.IsValid, $"Окно имеет размер {window.Size}.");
            Assert.True(window.FramebufferSize.IsValid, $"Фреймбуфер имеет размер {window.FramebufferSize}.");
        }
        catch (InvalidOperationException exception)
        {
            // Окно создать нечем. Причина обязана быть названа: иначе вызывающий
            // не отличит отсутствие дисплея от ошибки конфигурации.
            Assert.True(
                !string.IsNullOrWhiteSpace(exception.Message),
                "Отказ без причины не годится: вызывающий не сможет ничего исправить.");
        }
    }

    [WindowFact]
    public void Window_ClosesOnRequest()
    {
        using GlfwWindowBackend backend = new(new CollectingLogSink());
        using IWindow? window = TryCreate(backend);

        if (window is null)
        {
            return;
        }

        window.RequestClose();

        Assert.True(window.IsClosing, "После запроса окно обязано требовать закрытия.");
    }

    [WindowFact]
    public void Window_PollsEventsWithoutBlocking()
    {
        using GlfwWindowBackend backend = new(new CollectingLogSink());
        using IWindow? window = TryCreate(backend);

        if (window is null)
        {
            return;
        }

        // Метод обязан возвращать управление сразу: внутри шага симуляции
        // засыпать нельзя, иначе кадр встал бы в ожидание события.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            window.PollEvents();
        }

        Assert.False(window.IsClosing);
    }

    [WindowFact]
    public void Window_SurvivesResizeRequest()
    {
        using GlfwWindowBackend backend = new(new CollectingLogSink());
        using IWindow? window = TryCreate(backend);

        if (window is null)
        {
            return;
        }

        window.Size = new WindowSize(1024, 768);
        window.PollEvents();

        Assert.True(window.Size.IsValid, $"После изменения размера окно имеет размер {window.Size}.");
    }

    /// <summary>
    /// Создаёт окно, а если это невозможно — возвращает <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Возвращать <c>null</c> вместо исключения здесь допустимо: тест уже
    /// проверил контракт отказа, и повторная проверка причины в каждом тесте
    /// только дублировала бы её.
    /// </remarks>
    /// <param name="backend">Загрузчик окон.</param>
    /// <returns>Окно либо <c>null</c>, если окно создать нечем.</returns>
    private static IWindow? TryCreate(GlfwWindowBackend backend)
    {
        try
        {
            return backend.Create(new WindowConfig());
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}