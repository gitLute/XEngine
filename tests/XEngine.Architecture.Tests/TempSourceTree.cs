namespace XEngine.Architecture.Tests;

/// <summary>
/// Временное дерево исходников для проверки сканера. Создаётся в
/// <c>tmp/</c> решения (5.1: временные файлы сборки) и удаляется после теста,
/// поэтому тесты не зависят от прав на запись в системный каталог и не
/// оставляют мусор в клоне.
/// </summary>
public sealed class TempSourceTree : IDisposable
{
    private const string RootDirectoryName = "architecture-tests";

    /// <summary>
    /// Создаёт временное дерево с уникальным именем.
    /// </summary>
    /// <param name="name">Человекочитаемое имя набора файлов.</param>
    /// <exception cref="ArgumentException">Имя набора не задано.</exception>
    public TempSourceTree(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        string safeName = string.Concat(name.Select(character =>
            char.IsLetterOrDigit(character) ? character : '-'));

        Root = Path.Combine(RepositoryRoot.Find(), "tmp", RootDirectoryName, safeName + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Root);
    }

    /// <summary>
    /// Путь, который проверка передаёт как корень решения.
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// Записывает файл в проект внутри <c>src/</c>, создавая каталоги.
    /// </summary>
    /// <param name="project">Имя проекта: каталог внутри <c>src/</c>.</param>
    /// <param name="relativePath">Путь к файлу внутри проекта.</param>
    /// <param name="content">Содержимое файла.</param>
    /// <returns>Абсолютный путь к записанному файлу.</returns>
    public string WriteFile(string project, string relativePath, string content)
    {
        ArgumentException.ThrowIfNullOrEmpty(project);
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        ArgumentNullException.ThrowIfNull(content);

        string path = Path.Combine(Root, "src", project, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    /// Записывает файл в уже созданный каталог внутри <c>src/</c> без
    /// вычисления проекта: так проверяется, что <c>bin/</c> и <c>obj/</c>
    /// исключаются из проверки.
    /// </summary>
    /// <param name="project">Имя проекта: каталог внутри <c>src/</c>.</param>
    /// <param name="generatedDirectory">Каталог результатов сборки, например <c>obj</c>.</param>
    /// <param name="relativePath">Путь к файлу внутри каталога.</param>
    /// <param name="content">Содержимое файла.</param>
    /// <returns>Абсолютный путь к записанному файлу.</returns>
    public string WriteGeneratedFile(string project, string generatedDirectory, string relativePath, string content)
    {
        ArgumentException.ThrowIfNullOrEmpty(generatedDirectory);
        return WriteFile(project, Path.Combine(generatedDirectory, relativePath), content);
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
            // То же самое: причина та же, что и выше.
        }
    }
}