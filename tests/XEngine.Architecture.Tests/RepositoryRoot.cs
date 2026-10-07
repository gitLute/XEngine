namespace XEngine.Architecture.Tests;

/// <summary>
/// Ищет корень решения: тесты проверяют исходники репозитория, а не свою
/// раскладку вывода, поэтому путь определяется по файлу <c>XEngine.sln</c>.
/// </summary>
public static class RepositoryRoot
{
    private const string SolutionFileName = "XEngine.sln";

    /// <summary>
    /// Возвращает абсолютный путь к корню решения.
    /// </summary>
    /// <returns>Каталог, содержащий <c>XEngine.sln</c>.</returns>
    /// <exception cref="DirectoryNotFoundException">Корень решения не найден.</exception>
    public static string Find()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, SolutionFileName)))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Корень решения не найден: вверх от {AppContext.BaseDirectory} нет файла {SolutionFileName}.");
    }
}