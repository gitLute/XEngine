using XEngine.Core.Configuration;

namespace XEngine.Windowing;

/// <summary>
/// Загрузчик окон: создаёт окна по конфигурации.
/// </summary>
/// <remarks>
/// Загрузчик отделён от окна, потому что окно — это то, чем игра пользуется, а
/// загрузчик — то, чем окно создаётся. Иначе подменить окно в тесте нельзя, а
/// тест цикла не должен требовать ни дисплея, ни GLFW (17.6).
/// <para>
/// Загрузчик же владеет тем, что относится к платформе в целом: он один
/// инициализирует и завершает работу бэкенда окон.
/// </para>
/// </remarks>
public interface IWindowBackend : IDisposable
{
    /// <summary>
    /// Признак того, что платформа окон доступна в этой системе.
    /// </summary>
    /// <remarks>
    /// Без дисплея и без сервера окон окно создать нельзя, и честный ответ —
    /// «недоступно», а не исключение при попытке создать. Тесты цикла этим
    /// признаком решают, пропускать ли проверку настоящего окна.
    /// </remarks>
    bool IsAvailable { get; }

    /// <summary>
    /// Создаёт окно по конфигурации.
    /// </summary>
    /// <param name="config">Параметры окна.</param>
    /// <returns>Созданное окно.</returns>
    /// <exception cref="ArgumentNullException">Конфигурация не задана.</exception>
    /// <exception cref="ArgumentException">Параметры окна непригодны.</exception>
    /// <exception cref="InvalidOperationException">
    /// Оконная платформа недоступна: нет дисплея или сервера окон.
    /// </exception>
    IWindow Create(in WindowConfig config);
}