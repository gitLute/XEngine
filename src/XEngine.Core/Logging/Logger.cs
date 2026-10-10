namespace XEngine.Core.Logging;

/// <summary>
/// Журнал движка: раскладывает сообщения по приёмникам.
/// </summary>
/// <remarks>
/// Журнал инстанциируемый: у движка нет глобального журнала (инвариант 8).
/// Подсистема создаёт свой экземпляр на старте, передаёт его дальше и
/// освобождает вместе с собой. Нужен и общий журнал запуска, и отдельный для
/// скриптов, и тестовый с подставным приёмником — экземпляров столько,
/// сколько нужно.
/// <para>
/// Порог уровня проверяется до сборки текста сообщения: чтобы не платить за
/// строку, которая всё равно отбросится, дорогой вызов оборачивают проверкой
/// <see cref="IsEnabled"/>.
/// </para>
/// <para>
/// Приёмники вызываются по очереди, и сбой одного не должен ронять кадр:
/// исключение приёмника отключает его до конца работы журнала и не влияет на
/// остальные.
/// </para>
/// </remarks>
public sealed class Logger : IDisposable
{
    private readonly ILogSink[] _sinks;
    private readonly bool[] _disabledSinks;
    private readonly Lock _writeLock = new();

    private LogLevel _minimumLevel;
    private bool _ownsSinks;
    private bool _disposed;

    /// <summary>
    /// Создаёт журнал с порогом <see cref="LogLevel.Information"/>.
    /// </summary>
    /// <param name="sinks">
    /// Приёмники сообщений. Повторы одного и того же экземпляра отбрасываются:
    /// иначе общий приёмник попал бы в список дважды и вывел каждое сообщение
    /// дважды. Приёмники остаются во владении вызывающего.
    /// </param>
    public Logger(params ILogSink[] sinks)
        : this(LogLevel.Information, sinks)
    {
    }

    /// <summary>
    /// Создаёт журнал с заданным порогом уровня.
    /// </summary>
    /// <param name="minimumLevel">
    /// Минимальный уровень сообщений всего журнала. Приёмник может фильтровать
    /// строже: порог журнала — верхняя граница, порог приёмника — своя.
    /// </param>
    /// <param name="sinks">
    /// Приёмники сообщений. Остаются во владении вызывающего: журнал не
    /// освобождает их, потому что приёмник может быть общим для нескольких
    /// журналов. Освобождает только те, что создал сам, см. фабрики.
    /// </param>
    public Logger(LogLevel minimumLevel, params ILogSink[] sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);

        _minimumLevel = minimumLevel;
        _sinks = RemoveDuplicates(sinks);
        _disabledSinks = new bool[_sinks.Length];
    }

    /// <summary>
    /// Порог уровня журнала: сообщения ниже него не собираются и не пишутся.
    /// </summary>
    /// <remarks>
    /// Менять до начала работы потоков движка. Поле читается и пишется
    /// отдельно, поэтому гонки нет, но порядок «сначала подняли порог, потом
    /// запустили симуляцию» всё равно предпочтительнее.
    /// </remarks>
    public LogLevel MinimumLevel
    {
        get => _minimumLevel;
        set => _minimumLevel = value;
    }

    /// <summary>
    /// Приёмники журнала в порядке вызова.
    /// </summary>
    public IReadOnlyList<ILogSink> Sinks => _sinks;

    /// <summary>
    /// Проверяет, будет ли сообщение этого уровня куда-то записано.
    /// </summary>
    /// <param name="level">Уровень сообщения.</param>
    /// <returns><c>true</c>, если сообщение попадёт хотя бы в один приёмник.</returns>
    /// <remarks>
    /// Дешёвая проверка: не создаёт ни строки, ни записи. Ею оборачивают
    /// сообщение, текст которого собирается дорого.
    /// <para>
    /// Состояние освобождения не учитывается: проверка отвечает за уровень, а
    /// не за срок жизни. После <see cref="Dispose"/> запись бросает
    /// <see cref="ObjectDisposedException"/>.
    /// </para>
    /// </remarks>
    public bool IsEnabled(LogLevel level)
    {
        if (level < _minimumLevel)
        {
            return false;
        }

        for (int index = 0; index < _sinks.Length; index++)
        {
            if (!_disabledSinks[index] && _sinks[index].IsEnabled(level))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Записывает сообщение заданного уровня.
    /// </summary>
    /// <param name="level">Уровень сообщения.</param>
    /// <param name="message">Текст сообщения.</param>
    public void Log(LogLevel level, string message) =>
        Log(level, message, exception: null);

    /// <summary>
    /// Записывает сообщение заданного уровня с исключением.
    /// </summary>
    /// <param name="level">Уровень сообщения.</param>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="exception">
    /// Исключение. Дописывается под сообщением со стеком, потому что стек
    /// чаще всего и нужен при разборе падения.
    /// </param>
    public void Log(LogLevel level, string message, Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!IsEnabled(level))
        {
            return;
        }

        string text = exception is null ? message : $"{message}{Environment.NewLine}{exception}";

        lock (_writeLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            for (int index = 0; index < _sinks.Length; index++)
            {
                if (_disabledSinks[index])
                {
                    continue;
                }

                try
                {
                    _sinks[index].Write(level, text);
                }
#pragma warning disable CA1031
                // Приёмник — чужой код, и он вправе бросить что угодно: доступ к
                // диску, кодировка, собственная проверка. Ловится любое
                // исключение намеренно, иначе сбой приёмника роняет кадр.
                catch (Exception)
                {
                    // Приёмник сломан: отключаем его, чтобы каждое следующее
                    // сообщение не платило за то же самое исключение. Кадр
                    // должен продолжаться, а причина сбоя видна в файле и в
                    // консоли по остальным приёмникам.
                    _disabledSinks[index] = true;
                }
#pragma warning restore CA1031
            }
        }
    }

    /// <summary>
    /// Записывает отладочное сообщение.
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    public void Debug(string message) => Log(LogLevel.Debug, message);

    /// <summary>
    /// Записывает отладочное сообщение с исключением.
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="exception">Исключение.</param>
    public void Debug(string message, Exception exception) =>
        Log(LogLevel.Debug, message, exception);

    /// <summary>
    /// Записывает сообщение о ходе работы.
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    public void Information(string message) => Log(LogLevel.Information, message);

    /// <summary>
    /// Записывает сообщение о ходе работы с исключением.
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="exception">Исключение.</param>
    public void Information(string message, Exception exception) =>
        Log(LogLevel.Information, message, exception);

    /// <summary>
    /// Записывает предупреждение.
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    public void Warning(string message) => Log(LogLevel.Warning, message);

    /// <summary>
    /// Записывает предупреждение с исключением.
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="exception">Исключение.</param>
    public void Warning(string message, Exception exception) =>
        Log(LogLevel.Warning, message, exception);

    /// <summary>
    /// Записывает ошибку.
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    public void Error(string message) => Log(LogLevel.Error, message);

    /// <summary>
    /// Записывает ошибку с исключением.
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="exception">Исключение.</param>
    public void Error(string message, Exception exception) =>
        Log(LogLevel.Error, message, exception);

    /// <summary>
    /// Освобождает приёмники, созданные фабриками этого класса.
    /// </summary>
    /// <remarks>
    /// Приёмники, переданные в конструктор, не трогаются: они могут быть
    /// общими. Приёмник, упавший с исключением, всё равно освобождается —
    /// иначе остался бы незакрытый файл.
    /// <para>
    /// Повторный вызов и вызов после освобождения безопасны. После освобождения
    /// журналом нельзя писать.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        lock (_writeLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (!_ownsSinks)
            {
                return;
            }

            foreach (ILogSink sink in _sinks)
            {
                if (sink is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Создаёт журнал только для консоли.
    /// </summary>
    /// <param name="minimumLevel">Порог уровня журнала и приёмника.</param>
    /// <returns>Журнал, которому принадлежит приёмник.</returns>
    public static Logger CreateConsole(LogLevel minimumLevel = LogLevel.Information) =>
        new(minimumLevel, new ConsoleLogSink(minimumLevel)) { _ownsSinks = true };

    /// <summary>
    /// Создаёт журнал только для файла.
    /// </summary>
    /// <param name="path">Путь к файлу журнала.</param>
    /// <param name="minimumLevel">Порог уровня журнала и приёмника.</param>
    /// <returns>Журнал, которому принадлежит приёмник.</returns>
    public static Logger CreateFile(
        string path,
        LogLevel minimumLevel = LogLevel.Information) =>
        new(minimumLevel, new FileLogSink(path, minimumLevel)) { _ownsSinks = true };

    /// <summary>
    /// Создаёт журнал для консоли и файла одновременно.
    /// </summary>
    /// <param name="path">Путь к файлу журнала.</param>
    /// <param name="minimumLevel">Порог уровня журнала и обоих приёмников.</param>
    /// <returns>
    /// Журнал, которому принадлежат оба приёмника. Освобождение журнала
    /// закрывает файл.
    /// </returns>
    public static Logger CreateConsoleAndFile(
        string path,
        LogLevel minimumLevel = LogLevel.Information) =>
        new(minimumLevel, new ConsoleLogSink(minimumLevel), new FileLogSink(path, minimumLevel))
        {
            _ownsSinks = true,
        };

    private static ILogSink[] RemoveDuplicates(ILogSink[] sinks)
    {
        List<ILogSink> unique = new(sinks.Length);

        foreach (ILogSink sink in sinks)
        {
            ArgumentNullException.ThrowIfNull(sink, nameof(sinks));

            if (!unique.Contains(sink))
            {
                unique.Add(sink);
            }
        }

        return unique.ToArray();
    }
}