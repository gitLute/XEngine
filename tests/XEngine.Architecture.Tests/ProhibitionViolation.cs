namespace XEngine.Architecture.Tests;

/// <summary>
/// Найденное нарушение запрета: где, в какой строке и каким правилом.
/// </summary>
/// <param name="RuleId">Имя правила.</param>
/// <param name="Project">Проект, в котором найдено нарушение.</param>
/// <param name="RelativePath">Путь к файлу относительно проверяемого каталога.</param>
/// <param name="LineNumber">Номер строки, начиная с 1.</param>
/// <param name="LineText">Текст строки с нарушением.</param>
/// <param name="Pattern">Сработавшее выражение правила.</param>
public sealed record ProhibitionViolation(
    string RuleId,
    string Project,
    string RelativePath,
    int LineNumber,
    string LineText,
    string Pattern)
{
    /// <summary>
    /// Строка отчёта для вывода в консоль и в CI: она должна быть понятна без
    /// открытия файла.
    /// </summary>
    /// <returns>Строка вида <c>путь:строка [правило] текст</c>.</returns>
    public override string ToString()
        => $"{RelativePath}:{LineNumber} [{RuleId}] {LineText.Trim()}";
}