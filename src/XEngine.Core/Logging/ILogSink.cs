namespace XEngine.Core.Logging;

/// <summary>
/// Приёмник сообщений журнала.
/// </summary>
/// <remarks>
/// Единственный способ писать в журнал из движка: <c>System.Console</c> и
/// <c>System.Diagnostics.Debug</c> в <c>src/</c> запрещены (17.5), иначе
/// вывод нельзя ни перенаправить, ни собрать в тесте.
/// <para>
/// Вызывается с любого потока, поэтому реализация обязана сериализовать
/// запись (11.10). Горячим путём кадра журнал не пользуется: сообщения
/// появляются на редких событиях, и строки в кадре не создаются.
/// </para>
/// </remarks>
public interface ILogSink
{
    /// <summary>
    /// Заглушка: сообщения отбрасываются, логирование ничего не стоит.
    /// </summary>
    /// <remarks>
    /// Единственный статический объект в движке, разрешённый инвариантом 8:
    /// он не хранит состояния и не читает ничего извне, поэтому подменять его
    /// незачем.
    /// </remarks>
    static ILogSink Null { get; } = new NullLogSink();

    /// <summary>
    /// Проверяет, будет ли сообщение этого уровня принято.
    /// </summary>
    /// <param name="level">Уровень сообщения.</param>
    /// <returns><c>true</c>, если сообщение попадёт в вывод.</returns>
    /// <remarks>
    /// Проверка нужна, чтобы не собирать строку ради сообщения, которое всё
    /// равно отбросят.
    /// </remarks>
    bool IsEnabled(LogLevel level);

    /// <summary>
    /// Записывает сообщение.
    /// </summary>
    /// <param name="level">Уровень сообщения.</param>
    /// <param name="message">Текст сообщения.</param>
    void Write(LogLevel level, string message);
}

/// <summary>
/// Заглушка приёмника сообщений: всё отбрасывается.
/// </summary>
internal sealed class NullLogSink : ILogSink
{
    /// <inheritdoc/>
    public bool IsEnabled(LogLevel level) => false;

    /// <inheritdoc/>
    public void Write(LogLevel level, string message)
    {
        // Намеренно пусто: заглушка не пишет никуда, и проверка уровня у неё
        // всегда ложная, поэтому вызывающий код не тратит время на сбор строк.
    }
}