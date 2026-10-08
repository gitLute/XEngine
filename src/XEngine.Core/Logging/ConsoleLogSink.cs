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
/// </remarks>
public sealed class ConsoleLogSink : ILogSink
{
    private readonly LogLevel _minimumLevel;
    private readonly Lock _writeLock = new();

    /// <summary>
    /// Создаёт приёмник с порогом уровня сообщений.
    /// </summary>
    /// <param name="minimumLevel">
    /// Минимальный уровень, попадающий в вывод. По умолчанию
    /// <see cref="LogLevel.Information"/>: отладочная выжимка без запроса не
    /// нужна.
    /// </param>
    public ConsoleLogSink(LogLevel minimumLevel = LogLevel.Information)
    {
        _minimumLevel = minimumLevel;
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
            Console.WriteLine($"[{Describe(level)}] {message}");
        }
    }

    private static string Describe(LogLevel level) => level switch
    {
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        _ => "???",
    };
}