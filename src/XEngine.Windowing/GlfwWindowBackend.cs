using System.Runtime.InteropServices;
using Silk.NET.Windowing;
using XEngine.Core.Configuration;
using XEngine.Core.Logging;
using Window = Silk.NET.Windowing.Window;

namespace XEngine.Windowing;

/// <summary>
/// Загрузчик окон на Silk.NET (GLFW).
/// </summary>
/// <remarks>
/// Реализация лежит рядом с контрактом в одном проекте: это внутренняя деталь
/// проекта, а не межпроектная зависимость (5.2). Контракт при этом не знает о
/// Silk.NET, поэтому в тестах окно подменяется заглушкой.
/// </remarks>
public sealed class GlfwWindowBackend : IWindowBackend
{
    private readonly ILogSink _log;
    private bool _isAvailable;

    /// <summary>
    /// Создаёт загрузчик окон.
    /// </summary>
    /// <param name="log">Журнал: недоступность платформы идёт в него, а не в исключение.</param>
    /// <exception cref="ArgumentNullException">Журнал не задан.</exception>
    public GlfwWindowBackend(ILogSink log)
    {
        ArgumentNullException.ThrowIfNull(log);

        _log = log;
    }

    /// <inheritdoc/>
    public bool IsAvailable
    {
        get
        {
            Probe();
            return _isAvailable;
        }
    }

    /// <summary>
    /// Определяет, доступна ли оконная платформа.
    /// </summary>
    /// <remarks>
    /// Признак — наличие зарегистрированной платформы окон. Платформы
    /// регистрируются библиотекой при первом обращении, поэтому проверка сводится
    /// к чтению списка.
    /// <para>
    /// Вызов <c>Window.PrioritizeGlfw()</c> здесь не делается, хотя напрашивается:
    /// он не потокобезопасен и при одновременном вызове из двух потоков падает.
    /// Приоритет платформы нужен только когда есть из чего выбирать, а
    /// список и без него содержит GLFW — проверено измерением.
    /// </para>
    /// <para>
    /// <c>Window.TryAdd("GLFW")</c> для этой цели тоже не годится: он
    /// возвращает <c>false</c> даже тогда, когда платформа загружена и окно
    /// создаётся.
    /// </para>
    /// <para>
    /// Отсутствие дисплея обнаруживается не здесь, а при создании окна:
    /// зарегистрировать платформу можно и без дисплея, а создать окно — нет.
    /// Поэтому <see cref="IsAvailable"/> означает «платформа готова», а не «окно
    /// точно создастся».
    /// </para>
    /// </remarks>
    public void Probe()
    {
        _isAvailable = Window.Platforms.Count > 0;

        if (!_isAvailable)
        {
            _log.Write(
                LogLevel.Warning,
                "Оконная платформа не зарегистрирована: окно создать нельзя.");
        }
    }

    /// <inheritdoc/>
    public IWindow Create(in WindowConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        config.Validate();
        Probe();

        if (!_isAvailable)
        {
            throw new InvalidOperationException(
                "Оконная платформа недоступна: нет ни GLFW, ни другой платформы окон. " +
                "Проверить это можно вызовом IsAvailable до создания окна.");
        }

        WindowOptions options = new()
        {
            Size = new Silk.NET.Maths.Vector2D<int>(config.Width, config.Height),
            Title = config.Title,
            VSync = config.VSync,
            ShouldSwapAutomatically = true,
            IsVisible = config.Visible,
            WindowBorder = config.Resizable ? WindowBorder.Resizable : WindowBorder.Fixed,
            API = GraphicsAPI.Default,
        };

        try
        {
            Silk.NET.Windowing.IWindow window = Window.Create(options);

            // Create создаёт объект окна, но не открывает его: нативное окно
            // появляется только после Initialize, и до неё у объекта нет
            // дескриптора. Проверка дескриптора без инициализации всегда даёт
            // ноль, поэтому порядок здесь обязателен.
            window.Initialize();

            // Проверка обязательна и после инициализации. Когда окно создать
            // нечем, GLFW не бросает исключение, а оставляет пустой дескриптор:
            // чтение любого свойства после этого обращается к пустому окну
            // внутри GLFW и обрывает процесс нативным утверждением мимо
            // управляемого кода. Необратимая ошибка превращается в исключение
            // с понятным текстом.
            if (window.Handle == IntPtr.Zero)
            {
                window.Dispose();

                throw new InvalidOperationException(
                    "Окно не создано: GLFW не смог открыть окно. " +
                    "Проверьте наличие дисплея или сервера окон.");
            }

            return new GlfwWindow(window, _log);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or PlatformNotSupportedException
            or DllNotFoundException)
        {
            // Отсутствие дисплея проявляется именно здесь: платформа
            // зарегистрировалась, но окно создать не из чего. Причина уходит в
            // журнал, чтобы вызывающий не разбирал исключение платформы.
            _log.Write(
                LogLevel.Error,
                $"Окно создать не удалось: {exception.Message}");

            throw new InvalidOperationException(
                "Не удалось создать окно. Проверьте наличие дисплея или сервера окон.",
                exception);
        }
    }

    /// <summary>
    /// Загрузчик не освобождает ничего: инициализация GLFW живёт в статике
    /// библиотеки и освобождается ею же.
    /// </summary>
    /// <remarks>
    /// Освобождать её отсюда означало бы вмешиваться в чужое владение, а окна
    /// всё равно освобождает вызывающий.
    /// </remarks>
    public void Dispose()
    {
    }
}