using System.Globalization;

namespace XEngine.Core.Logging;

/// <summary>
/// Собирает строку журнала в формате, принятом в движке.
/// </summary>
/// <remarks>
/// Формат строки: <c>[&lt;время&gt;] [&lt;поток&gt; / &lt;уровень&gt;]: &lt;сообщение&gt;</c>.
/// Например: <c>[2026-10-09 12:34:56.789] [Main / INF]: сцена загружена</c>.
/// <para>
/// Формат живёт в экземпляре, а не в статическом поле (инвариант 8): часы
/// внедряются снаружи, поэтому в тесте отметку времени можно задать
/// фиксированной функцией и сравнивать строки целиком.
/// </para>
/// <para>
/// Форматирование держится здесь, а не в приёмниках, чтобы консоль и файл
/// давали побайтово одинаковую строку и правило не расходилось при правке
/// одного приёмника.
/// </para>
/// </remarks>
public sealed class LogFormatter
{
    /// <summary>
    /// Формат отметки времени по умолчанию: местное время с миллисекундами,
    /// сортируемое как текст.
    /// </summary>
    public const string DefaultTimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    private readonly string _timestampFormat;
    private readonly Func<DateTimeOffset> _clock;

    /// <summary>
    /// Создаёт форматтер строк журнала.
    /// </summary>
    /// <param name="timestampFormat">
    /// Формат отметки времени. По умолчанию <see cref="DefaultTimestampFormat"/>.
    /// Строка берётся всегда в инвариантной культуре, поэтому журнал читается
    /// одинаково при любой локали машины.
    /// </param>
    /// <param name="clock">
    /// Источник времени. По умолчанию местное время. В тест передаётся
    /// фиксированная функция.
    /// </param>
    public LogFormatter(
        string timestampFormat = DefaultTimestampFormat,
        Func<DateTimeOffset>? clock = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(timestampFormat);

        _timestampFormat = timestampFormat;
        _clock = clock ?? (static () => DateTimeOffset.Now);
    }

    /// <summary>
    /// Формат отметки времени, заданный при создании.
    /// </summary>
    public string TimestampFormat => _timestampFormat;

    /// <summary>
    /// Собирает готовую строку журнала.
    /// </summary>
    /// <param name="level">Уровень сообщения.</param>
    /// <param name="message">Текст сообщения.</param>
    /// <returns>Строка вида <c>[время] [поток / уровень]: сообщение</c>.</returns>
    public string Format(LogLevel level, string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return $"[{Timestamp()}] [{ThreadName()} / {LevelName(level)}]: {message}";
    }

    /// <summary>
    /// Отметка времени текущего момента по заданному формату.
    /// </summary>
    public string Timestamp() =>
        _clock().ToString(_timestampFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Имя вызывающего потока.
    /// </summary>
    /// <remarks>
    /// Поток симуляции и поток рендера различаются в журнале, поэтому у потоков
    /// движка должно быть задано <see cref="Thread.Name"/>. Имя задаётся один
    /// раз за жизнь потока. Если имя не задано, остаётся идентификатор
    /// <c>#<see cref="Thread.ManagedThreadId"/></c>: он не уникален между
    /// запусками, но строки журнала сортируются и ищутся по времени, а не по нему.
    /// </remarks>
    public string ThreadName()
    {
        Thread thread = Thread.CurrentThread;

        return thread.Name ?? $"#{thread.ManagedThreadId}";
    }

    /// <summary>
    /// Короткое имя уровня для строки журнала.
    /// </summary>
    /// <param name="level">Уровень сообщения.</param>
    public string LevelName(LogLevel level) => level switch
    {
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        _ => "???",
    };
}