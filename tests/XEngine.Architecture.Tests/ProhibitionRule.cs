using System.Text.RegularExpressions;

namespace XEngine.Architecture.Tests;

/// <summary>
/// Запрет, проверяемый по исходникам: правило из 17.5, ставшее инвариантом.
/// </summary>
/// <remarks>
/// Правило проверяет текст исходника построчно и пропускает строки-комментарии:
/// упоминание имени бэкенда в пояснении не является нарушением, нарушением
/// является ссылка на бэкенд в коде.
/// </remarks>
public sealed record ProhibitionRule
{
    /// <summary>
    /// Короткое имя правила: оно попадает в отчёт и в вывод CI, поэтому
    /// должно читаться без ссылки на документ.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Что запрещает правило, словами требования.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Регулярные выражения, по которым опознаётся нарушение.
    /// </summary>
    public required IReadOnlyList<string> ForbiddenPatterns { get; init; }

    /// <summary>
    /// Проекты, в которых правило действует. Пустой список означает «весь src/».
    /// </summary>
    public IReadOnlySet<string> Projects { get; init; } = FrozenSet<string>.Empty;

    /// <summary>
    /// Маркеры, присутствие которых освобождает файл от правила. Нужно правилу
    /// про <c>System.Console</c>: вывод разрешён внутри реализации <c>ILogSink</c>,
    /// и больше нигде (17.5).
    /// </summary>
    public IReadOnlyList<string> AllowedFileMarkers { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Действует ли правило для файла проекта <paramref name="project"/>.
    /// </summary>
    /// <param name="project">Имя проекта: каталог внутри <c>src/</c>.</param>
    /// <param name="fileText">Полный текст файла, нужен для проверки маркеров.</param>
    /// <returns><see langword="true"/>, если правило применяется к файлу.</returns>
    public bool AppliesTo(string project, string fileText)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(fileText);

        if (Projects.Count > 0 && !Projects.Contains(project))
        {
            return false;
        }

        foreach (string marker in AllowedFileMarkers)
        {
            if (fileText.Contains(marker, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Возвращает первое сработавшее выражение правила для строки кода или
    /// пустую строку, если ни одно не совпало.
    /// </summary>
    /// <param name="codeLine">Строка исходника без комментария.</param>
    /// <returns>Сработавшее выражение либо пустая строка.</returns>
    public string Match(string codeLine)
    {
        ArgumentNullException.ThrowIfNull(codeLine);

        foreach (string pattern in ForbiddenPatterns)
        {
            if (Regex.IsMatch(codeLine, pattern, RegexOptions.CultureInvariant))
            {
                return pattern;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Собирает выражения, опознающие ссылку на пространство имён
    /// <paramref name="namespaceRoot"/>: директиву using и полностью
    /// уточнённое обращение.
    /// </summary>
    /// <param name="namespaceRoot">Корень пространства имён, например <c>Silk.NET</c>.</param>
    /// <returns>Выражения правила.</returns>
    public static string[] NamespacePatterns(string namespaceRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(namespaceRoot);

        return
        [
            $@"\busing\s+(static\s+|global\s+)*(global::)?{namespaceRoot}\b",
            $@"\b{namespaceRoot}\.[A-Za-z_]",
        ];
    }
}

/// <summary>
/// Множество, копируемое один раз: правила создаются на старте и не меняются,
/// поэтому копия снимает вопрос владения коллекцией в конструкторе.
/// </summary>
/// <param name="items">Исходные значения.</param>
public sealed class FrozenSet<T> : HashSet<T>
    where T : notnull
{
    private FrozenSet(IEnumerable<T> items)
        : base(items)
    {
    }

    /// <summary>
    /// Пустое множество: поле правила по умолчанию.
    /// </summary>
    public static FrozenSet<T> Empty { get; } = new([]);

    /// <summary>
    /// Создаёт множество из значений.
    /// </summary>
    /// <param name="items">Исходные значения.</param>
    /// <returns>Множество, не изменяемое после создания.</returns>
    public static FrozenSet<T> Of(params T[] items) => new(items);
}