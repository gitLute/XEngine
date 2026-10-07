namespace XEngine.Architecture.Tests;

/// <summary>
/// Каталог правил проверки <c>src/</c> из 17.5: состав проверок фиксирован
/// здесь, чтобы и правила, и проверка их полноты лежали в одном месте.
/// </summary>
public static class ProhibitionRules
{
    /// <summary>
    /// Слой 1 и проекты абстракций: именно они не должны знать ни одного бэкенда
    /// (инвариант 1). Слой 3 состоит из бэкендов, поэтому правило на них не
    /// распространяется: там ссылки на бэкенд обязательны.
    /// </summary>
    public static FrozenSet<string> ProjectsWithoutBackends { get; } = FrozenSet<string>.Of(
        "XEngine.Core",
        "XEngine.Mathematics",
        "XEngine.Assets.Abstractions",
        "XEngine.Physics.Abstractions",
        "XEngine.Audio.Abstractions",
        "XEngine.Graphics.Abstractions");

    /// <summary>
    /// Каталог правил. Порядок соответствует перечню 17.5.
    /// </summary>
    /// <returns>Полный список правил.</returns>
    public static IReadOnlyList<ProhibitionRule> Create()
    {
        List<string> backendPatterns =
        [
            .. ProhibitionRule.NamespacePatterns(@"Silk\.NET"),
            .. ProhibitionRule.NamespacePatterns("OpenTK"),
            .. ProhibitionRule.NamespacePatterns("SharpGLTF"),
            .. ProhibitionRule.NamespacePatterns(@"Box2D(\.NET)?"),
            .. ProhibitionRule.NamespacePatterns("JoltPhysicsSharp"),
        ];

        List<ProhibitionRule> rules =
        [
            new ProhibitionRule
            {
                Id = "core-does-not-know-backends",
                Description =
                    "В XEngine.Core, XEngine.Mathematics и проектах *.Abstractions нет " +
                    "using Silk.NET, OpenTK, SharpGLTF, Box2D.NET и Jolt (17.5, инвариант 1).",
                ForbiddenPatterns = [.. backendPatterns],
                Projects = ProjectsWithoutBackends,
            },
            new ProhibitionRule
            {
                Id = "no-reflection-in-source",
                Description =
                    "В src/ не используется рефлексия: System.Reflection, Type.GetMethod, " +
                    "Type.GetProperty, Activator.CreateInstance, Assembly.GetTypes (17.5, инвариант 2).",
                ForbiddenPatterns =
                [
                    @"\bSystem\.Reflection\b",
                    @"\bType\.GetMethod\b",
                    @"\bType\.GetProperty\b",
                    @"\bActivator\.CreateInstance\b",
                    @"\bAssembly\.GetTypes\b",
                ],
            },
            new ProhibitionRule
            {
                Id = "no-console-and-debug-outside-log-sink",
                Description =
                    "System.Console и System.Diagnostics.Debug не используются вне ILogSink: " +
                    "логирование идёт только через ILogSink (17.5).",
                ForbiddenPatterns =
                [
                    @"\bSystem\.Console\b",
                    @"\bConsole\.(Write|WriteLine|Read|ReadLine|ReadKey|Error|Out)\b",
                    @"\bSystem\.Diagnostics\.Debug\b",
                    @"\bDebug\.(Write|WriteLine|Assert|Print)\b",
                ],
                AllowedFileMarkers = ["ILogSink"],
            },
        ];

        return rules;
    }
}