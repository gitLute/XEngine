namespace XEngine.Core.Configuration;

/// <summary>
/// Конфигурация движка целиком.
/// </summary>
/// <remarks>
/// Секции добавляются вместе с этапами, которые их читают (14.1): на этапе 3
/// существуют только окно и симуляция, и описание остальных подсистем
/// отсутствует намеренно — конфигурация не должна перечислять то, чего в
/// движке ещё нет.
/// </remarks>
public sealed record EngineConfig
{
    /// <summary>
    /// Параметры окна.
    /// </summary>
    public WindowConfig Window { get; init; } = new();

    /// <summary>
    /// Параметры симуляции и порядка кадра.
    /// </summary>
    public SimulationConfig Simulation { get; init; } = new();

    /// <summary>
    /// Проверяет все секции конфигурации.
    /// </summary>
    /// <exception cref="ArgumentException">Секция содержит непригодные значения.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Секция содержит непригодные значения.</exception>
    public void Validate()
    {
        Window.Validate();
        Simulation.Validate();
    }
}