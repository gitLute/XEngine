using Xunit;

namespace XEngine.Windowing.Tests;

/// <summary>
/// Тест, который выполняется только по явному требованию: переменная окружения
/// <c>XENGINE_WINDOW_TESTS=1</c>.
/// </summary>
/// <remarks>
/// Причина техническая и обязательная. Без дисплея GLFW падает нативно, а не
/// бросает исключение: процесс тестов погибает вместе со всеми остальными
/// тестами прогона. Перехватить такое нельзя, поэтому единственный способ не
/// испортить прогон — не создавать окно, пока его не попросили.
/// <para>
/// Пропуск задан атрибутом, а не исключением в теле теста: в xUnit 2.9
/// динамический пропуск помечается служебным токеном, который раннер 2.8 не
/// распознаёт, и тест отмечается как упавший. Пропуск при обнаружении тестов
/// раннер понимает и показывает честно.
/// </para>
/// </remarks>
public sealed class WindowFactAttribute : FactAttribute
{
    /// <summary>
    /// Имя переменной окружения, разрешающей создание настоящего окна.
    /// </summary>
    public const string EnablingVariable = "XENGINE_WINDOW_TESTS";

    /// <summary>
    /// Создаёт атрибут, пропускающий тест, если окно не запрошено или дисплея нет.
    /// </summary>
    public WindowFactAttribute()
    {
        Skip = IsWindowTestEnabled() ? null : SkipReason();
    }

    /// <summary>
    /// Проверяет, разрешено ли создание настоящего окна.
    /// </summary>
    /// <returns><c>true</c>, если переменная разрешения задана и дисплей есть.</returns>
    public static bool IsWindowTestEnabled()
    {
        if (Environment.GetEnvironmentVariable(EnablingVariable) != "1")
        {
            return false;
        }

        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
    }

    private static string SkipReason()
    {
        if (Environment.GetEnvironmentVariable(EnablingVariable) != "1")
        {
            return $"Настоящее окно не проверяется автоматически: задайте {EnablingVariable}=1.";
        }

        return "В системе нет дисплея: настоящее окно создать нельзя.";
    }
}