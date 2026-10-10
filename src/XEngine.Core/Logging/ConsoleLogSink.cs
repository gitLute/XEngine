namespace XEngine.Core.Logging;

/// <summary>
/// Приёмник сообщений, пишущий в консоль.
/// </summary>
/// <remarks>
/// Это единственное место в <c>src/</c>, где обращение к <c>System.Console</c>
/// разрешено (17.5): файл реализует <see cref="ILogSink"/>, и весь остальной
/// движок пишет только через него.
/// <para>
/// Запись сериализуется блокировкой, потому что журнал вызывается с потока
/// симуляции и с потока рендера (11.10), а <see cref="Console"/> сам этого не
/// гарантирует.
/// </para>
/// <para>
/// Строку собирает <see cref="LogFormatter"/>, общий с файловым приёмником:
/// консоль и файл дают одинаковые строки, а правило одно на оба вывода.
/// </para>
/// </remarks>
public sealed class ConsoleLogSink : ILogSink
{
    private readonly LogLevel _minimumLevel;
    private readonly LogFormatter _formatter;
    private readonly Lock _writeLock = new();

    /// <summary>
    /// Создаёт приёмник с порогом уровня сообщений.
    /// </summary>
    /// <param name="minimumLevel">
    /// Минимальный уровень, попадающий в вывод. По умолчанию
    /// <see cref="LogLevel.Information"/>: отладочная выжимка без запроса не
    /// нужна.
    /// </param>
    /// <param name="formatter">
    /// Формат строки. По умолчанию <see cref="LogFormatter"/> с настройками по
    /// умолчанию.
    /// </param>
    public ConsoleLogSink(LogLevel minimumLevel = LogLevel.Information, LogFormatter? formatter = null)
    {
        _minimumLevel = minimumLevel;
        _formatter = formatter ?? new LogFormatter();
    }

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel level) => level >= _minimumLevel;

    /// <inheritdoc/>
    public void Write(LogLevel level, string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!IsEnabled(level))
        {
            return;
        }

        lock (_writeLock)
        {
            Console.WriteLine(_formatter.Format(level, message));
        }
    }
}