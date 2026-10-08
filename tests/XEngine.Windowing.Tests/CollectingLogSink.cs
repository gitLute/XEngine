using XEngine.Core.Logging;

namespace XEngine.Windowing.Tests;

/// <summary>
/// Заглушка журнала, собирающая сообщения: проверки диагностики должны видеть,
/// что сообщение действительно ушло, а не просто что метод не упал.
/// </summary>
internal sealed class CollectingLogSink : ILogSink
{
    private readonly List<string> _messages = [];
    private readonly LogLevel _minimumLevel;

    /// <summary>
    /// Создаёт заглушку с порогом уровня сообщений.
    /// </summary>
    /// <param name="minimumLevel">Минимальный принимаемый уровень.</param>
    public CollectingLogSink(LogLevel minimumLevel = LogLevel.Debug)
    {
        _minimumLevel = minimumLevel;
    }

    /// <summary>
    /// Все принятые сообщения в порядке поступления.
    /// </summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel level) => level >= _minimumLevel;

    /// <inheritdoc/>
    public void Write(LogLevel level, string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (IsEnabled(level))
        {
            _messages.Add(message);
        }
    }

    /// <summary>
    /// Проверяет, что есть принятое сообщение, содержащее подстроку.
    /// </summary>
    /// <remarks>
    /// Сравнение по подстроке, а не по равенству строк: проверяется смысл
    /// сообщения, а его формулировка меняется вместе с текстом журнала.
    /// </remarks>
    /// <param name="fragment">Искомая подстрока.</param>
    /// <returns><c>true</c>, если такое сообщение есть.</returns>
    public bool Contains(string fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);

        foreach (string message in _messages)
        {
            if (message.Contains(fragment, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}