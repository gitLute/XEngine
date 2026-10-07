namespace XEngine.Architecture.Tests;

/// <summary>
/// Результат проверки каталога <c>src/</c>: найденные нарушения и объём проверки.
/// </summary>
public sealed class ProhibitionReport
{
    /// <summary>
    /// Создаёт отчёт проверки.
    /// </summary>
    /// <param name="violations">Найденные нарушения.</param>
    /// <param name="scannedFiles">Сколько файлов проверено.</param>
    /// <exception cref="ArgumentNullException">Список нарушений отсутствует.</exception>
    public ProhibitionReport(IReadOnlyList<ProhibitionViolation> violations, int scannedFiles)
    {
        ArgumentNullException.ThrowIfNull(violations);

        Violations = violations;
        ScannedFiles = scannedFiles;
    }

    /// <summary>
    /// Найденные нарушения в порядке обхода: каталоги, файлы, строки.
    /// </summary>
    public IReadOnlyList<ProhibitionViolation> Violations { get; }

    /// <summary>
    /// Сколько файлов проверено. В отчёт попадает, чтобы пустой результат был
    /// отличим от проверки, которая ничего не нашла из-за неверного пути.
    /// </summary>
    public int ScannedFiles { get; }

    /// <summary>
    /// Нарушений нет.
    /// </summary>
    public bool IsClean => Violations.Count == 0;

    /// <summary>
    /// Текст отчёта для вывода в консоль и в CI.
    /// </summary>
    /// <returns>Сводка и список нарушений либо подтверждение чистоты.</returns>
    public string Describe()
    {
        if (IsClean)
        {
            return $"Проверено файлов: {ScannedFiles}. Нарушений не найдено.";
        }

        return string.Join(Environment.NewLine, Violations.Select(violation => violation.ToString()))
            + Environment.NewLine
            + $"Проверено файлов: {ScannedFiles}. Нарушений: {Violations.Count}.";
    }
}