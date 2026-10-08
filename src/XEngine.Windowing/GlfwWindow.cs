using Silk.NET.Windowing;
using XEngine.Core.Logging;

namespace XEngine.Windowing;

/// <summary>
/// Окно на Silk.NET.
/// </summary>
/// <remarks>
/// Обёртка нужна ради двух вещей. Первая: контракт не должен знать о Silk.NET,
/// иначе окно нельзя подменить в тесте. Вторая: у Silk.NET состояние фокуса не
/// читается опросом — оно приходит событием <c>FocusChanged</c> с флагом, — а
/// контракт требует обычное свойство. Флаг события копируется в поле, и
/// контракт читает поле.
/// <para>
/// Свойства размера читаются у платформы при каждом обращении, а не
/// кэшируются: размер меняется пользователем в любой момент, и кэш разошёлся
/// бы с окном после перетаскивания.
/// </para>
/// </remarks>
internal sealed class GlfwWindow : IWindow
{
    private readonly Silk.NET.Windowing.IWindow _window;
    private readonly ILogSink _log;
    private readonly Action<bool> _onFocusChanged;
    private readonly Silk.NET.Core.Contexts.IGLContext _context;
    private bool _isFocused = true;
    private bool _isDisposed;

    /// <summary>
    /// Оборачивает окно платформы.
    /// </summary>
    /// <param name="window">Окно Silk.NET.</param>
    /// <param name="log">Журнал для диагностики окна.</param>
    /// <exception cref="ArgumentNullException">Окно или журнал не заданы.</exception>
    public GlfwWindow(Silk.NET.Windowing.IWindow window, ILogSink log)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(log);

        _window = window;
        _log = log;

        // Подписка обязана удерживаться в поле: иначе сборщик соберёт её после
        // создания, и окно перестанет сообщать о фокусе.
        _onFocusChanged = SetFocused;
        _window.FocusChanged += _onFocusChanged;

        // Контекст берётся один раз при создании окна: обращение к свойству
        // создаёт объект-обёртку, и искать его на каждом вызове означало бы
        // лишние аллокации в горячем пути.
        //
        // Свойство помечено как nullable, хотя контекст у созданного окна есть
        // по определению: окно без контекста бесполезно, и тихо продолжить
        // работу с ним нельзя. Проверка обязательна и превращает в
        // NullReferenceException в середине рисования понятный отказ сразу.
        _context = ((Silk.NET.Core.Contexts.IGLContextSource)_window).GLContext
            ?? throw new InvalidOperationException(
                "У окна нет графического контекста: рисовать в нём нечем.");


        _log.Write(LogLevel.Information, $"Окно создано: {_window.Title}, размер {_window.Size}.");
    }

    /// <inheritdoc/>
    public string Title
    {
        get => _window.Title;
        set => _window.Title = value;
    }

    /// <inheritdoc/>
    public WindowSize Size
    {
        get
        {
            Silk.NET.Maths.Vector2D<int> size = _window.Size;
            return new WindowSize(size.X, size.Y);
        }
        set => _window.Size = new Silk.NET.Maths.Vector2D<int>(value.Width, value.Height);
    }

    /// <inheritdoc/>
    public WindowSize FramebufferSize
    {
        get
        {
            Silk.NET.Maths.Vector2D<int> size = _window.FramebufferSize;
            return new WindowSize(size.X, size.Y);
        }
    }

    /// <inheritdoc/>
    public IntPtr GetGraphicsProcedure(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        // Загрузчик функций живёт в самом окне платформы: оно реализует
        // источник контекста, и адреса функций достаются из того же контекста.
        // Обращаться к GLFW напрямую нельзя: это обошло бы контекст окна.
        return _context.GetProcAddress(name);
    }

    /// <inheritdoc/>
    public bool IsFocused => _isFocused;

    /// <inheritdoc/>
    public bool IsClosing => _window.IsClosing;

    /// <inheritdoc/>
    public void RequestClose()
    {
        if (!_window.IsClosing)
        {
            _window.IsClosing = true;
        }
    }

    /// <inheritdoc/>
    public void PollEvents() => _window.DoEvents();

    /// <inheritdoc/>
    public void Present() => _window.DoRender();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        _window.FocusChanged -= _onFocusChanged;
        _window.Dispose();

        _log.Write(LogLevel.Information, "Окно освобождено.");
    }

    private void SetFocused(bool isFocused) => _isFocused = isFocused;
}