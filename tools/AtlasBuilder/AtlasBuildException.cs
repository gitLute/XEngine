namespace AtlasBuilder;

/// <summary>
/// Ошибка сборки атласа: описание или исходники противоречат друг другу.
/// </summary>
/// <remarks>
/// В движке роль этой ошибки играет <c>EngineException</c>, но инструмент не
/// зависит от движка (5.2) и не должен ради этого зависеть от сборки,
/// которой ещё нет.
/// </remarks>
public sealed class AtlasBuildException : Exception
{
    /// <summary>
    /// Создаёт ошибку сборки атласа.
    /// </summary>
    public AtlasBuildException()
    {
    }

    /// <summary>
    /// Создаёт ошибку сборки атласа.
    /// </summary>
    /// <param name="message">Что не так, словами требования.</param>
    public AtlasBuildException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Создаёт ошибку сборки атласа с указанием источника проблемы.
    /// </summary>
    /// <param name="message">Что не так.</param>
    /// <param name="regionName">Имя региона, если ошибка относится к региону.</param>
    public AtlasBuildException(string message, string regionName)
        : base($"{message} Регион: {regionName}.")
    {
    }
}