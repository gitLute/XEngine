namespace AtlasBuilder.Tests;

/// <summary>
/// Временный каталог теста внутри <c>tmp/</c> решения (5.1). Удаляется после
/// теста: тесты не должны оставлять мусор в клоне и не должны требовать прав
/// на запись в системный каталог.
/// </summary>
public sealed class TestDirectory : IDisposable
{
    private const string RootDirectoryName = "atlas-builder-tests";

    /// <summary>
    /// Создаёт временный каталог с уникальным именем.
    /// </summary>
    /// <param name="name">Человекочитаемое имя набора файлов.</param>
    /// <exception cref="ArgumentException">Имя набора не задано.</exception>
    public TestDirectory(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        string safeName = string.Concat(name.Select(character =>
            char.IsLetterOrDigit(character) ? character : '-'));

        Root = Path.Combine(
            RepositoryRoot.Find(),
            "tmp",
            RootDirectoryName,
            safeName + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Root);
    }

    /// <summary>
    /// Путь к каталогу.
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// Создаёт подкаталог и возвращает его путь.
    /// </summary>
    /// <param name="relativePath">Имя подкаталога относительно корня.</param>
    /// <returns>Путь к созданному подкаталогу.</returns>
    /// <exception cref="ArgumentException">Имя подкаталога пусто.</exception>
    public string CreateSubdirectory(string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(relativePath);

        string path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Путь внутри каталога без создания подкаталогов.
    /// </summary>
    /// <param name="relativePath">Относительный путь.</param>
    /// <returns>Полный путь.</returns>
    /// <exception cref="ArgumentException">Путь пуст.</exception>
    public string GetPath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(relativePath);

        return Path.Combine(Root, relativePath);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Каталог временный: оставшаяся копия в tmp/ не должна ронять тест.
        }
        catch (UnauthorizedAccessException)
        {
            // Причина та же, что и выше.
        }
    }
}

/// <summary>
/// Ищет корень решения: тесты проверяют файлы репозитория, а не свою раскладку
/// вывода.
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