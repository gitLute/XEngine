namespace XEngine.Architecture.Tests;

/// <summary>
/// Проверяет каталог <c>src/</c> на нарушения правил из 17.5. Это реализация
/// проверки запретов из CI: она живёт в тестах, чтобы нарушение правила
/// останавливало обычный <c>dotnet test</c>, а не только ночной прогон.
/// </summary>
public sealed class ProhibitionScanner
{
    private const string SourceDirectoryName = "src";

    /// <summary>
    /// Каталоги результатов сборки: файлы в них генерируются MSBuild и
    /// повторяют исходники, поэтому проверять их бессмысленно.
    /// </summary>
    private static readonly string[] GeneratedDirectoryNames = ["bin", "obj"];

    private readonly IReadOnlyList<ProhibitionRule> _rules;

    /// <summary>
    /// Создаёт проверку по набору правил.
    /// </summary>
    /// <param name="rules">Правила проверки, обычно <see cref="ProhibitionRules.Create"/>.</param>
    /// <exception cref="ArgumentNullException">Список правил отсутствует.</exception>
    public ProhibitionScanner(IEnumerable<ProhibitionRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _rules = [.. rules];
    }

    /// <summary>
    /// Правила, по которым идёт проверка.
    /// </summary>
    public IReadOnlyList<ProhibitionRule> Rules => _rules;

    /// <summary>
    /// Проверяет все файлы C# каталога <c>src/</c>.
    /// </summary>
    /// <param name="repositoryRoot">
    /// Корень решения: в нём ищется каталог <c>src/</c>. Так задан путь,
    /// потому что он известен в тесте и не зависит от раскладки вывода.
    /// </param>
    /// <returns>Отчёт с найденными нарушениями.</returns>
    /// <exception cref="ArgumentNullException">Путь отсутствует.</exception>
    /// <exception cref="DirectoryNotFoundException">Каталога <c>src/</c> нет.</exception>
    public ProhibitionReport Scan(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(repositoryRoot);

        string sourceDirectory = Path.Combine(repositoryRoot, SourceDirectoryName);
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Каталог src не найден: {sourceDirectory}");
        }

        List<ProhibitionViolation> violations = [];
        int scannedFiles = 0;

        foreach (string file in EnumerateSourceFiles(sourceDirectory))
        {
            string project = ResolveProject(sourceDirectory, file);
            string relativePath = Path.GetRelativePath(repositoryRoot, file);
            string text = File.ReadAllText(file);
            scannedFiles++;

            foreach (ProhibitionRule rule in _rules)
            {
                if (!rule.AppliesTo(project, text))
                {
                    continue;
                }

                CollectViolations(rule, project, relativePath, text, violations);
            }
        }

        return new ProhibitionReport(violations, scannedFiles);
    }

    private static IEnumerable<string> EnumerateSourceFiles(string sourceDirectory)
    {
        foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories))
        {
            if (IsGenerated(file))
            {
                continue;
            }

            yield return file;
        }
    }

    private static bool IsGenerated(string file)
    {
        string[] segments = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (string segment in segments)
        {
            foreach (string generated in GeneratedDirectoryNames)
            {
                if (segment.Equals(generated, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string ResolveProject(string sourceDirectory, string file)
    {
        string relative = Path.GetRelativePath(sourceDirectory, file);
        int separator = relative.IndexOf(Path.DirectorySeparatorChar);
        return separator < 0 ? relative : relative[..separator];
    }

    private static void CollectViolations(
        ProhibitionRule rule,
        string project,
        string relativePath,
        string text,
        List<ProhibitionViolation> violations)
    {
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (IsCommentOnly(line))
            {
                continue;
            }

            string pattern = rule.Match(line);
            if (pattern.Length > 0)
            {
                violations.Add(new ProhibitionViolation(
                    rule.Id,
                    project,
                    relativePath,
                    index + 1,
                    line,
                    pattern));
            }
        }
    }

    /// <summary>
    /// Строка целиком является комментарием: такие строки пропускаются, потому
    /// что требование запрещает ссылку на бэкенд в коде, а не упоминание
    /// бэкенда в пояснении.
    /// </summary>
    /// <param name="line">Строка исходника с возможным ведущим отступом.</param>
    /// <returns><see langword="true"/>, если строка состоит только из комментария.</returns>
    private static bool IsCommentOnly(string line)
    {
        ReadOnlySpan<char> trimmed = line.AsSpan().TrimStart();
        return trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("/*", StringComparison.Ordinal)
            || trimmed.StartsWith('*')
            || trimmed.IsEmpty;
    }
}