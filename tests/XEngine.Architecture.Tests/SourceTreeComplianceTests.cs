using Xunit;
using Xunit.Abstractions;

namespace XEngine.Architecture.Tests;

/// <summary>
/// Проверка настоящего дерева <c>src/</c>: правила из 17.5 применяются к коду
/// движка, а не только к примерам в тестах. Тест падает, когда в исходниках
/// появляется ссылка на бэкенд в ядре или рефлексия в любом месте.
/// </summary>
public sealed class SourceTreeComplianceTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Создаёт проверку с выводом отчёта в журнал теста.
    /// </summary>
    /// <param name="output">Журнал теста.</param>
    public SourceTreeComplianceTests(ITestOutputHelper output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _output = output;
    }

    [Fact]
    public void SourceTree_HasNoProhibitionViolations()
    {
        ProhibitionScanner scanner = new(ProhibitionRules.Create());

        ProhibitionReport report = scanner.Scan(RepositoryRoot.Find());

        _output.WriteLine(report.Describe());
        Assert.True(report.IsClean, report.Describe());
    }

    [Fact]
    public void SourceTree_IsNotEmpty()
    {
        ProhibitionScanner scanner = new(ProhibitionRules.Create());

        ProhibitionReport report = scanner.Scan(RepositoryRoot.Find());

        // Пустой результат проверки не должен выдаваться за успех: если каталог
        // src/ перестал находиться, нарушений будет ноль при нуле файлов.
        Assert.True(
            report.ScannedFiles > 0,
            $"Проверено файлов: {report.ScannedFiles}. Проверка ничего не нашла, а это не успех.");
    }
}