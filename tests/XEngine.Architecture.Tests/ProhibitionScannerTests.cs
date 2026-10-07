using Xunit;

namespace XEngine.Architecture.Tests;

/// <summary>
/// Контрактные тесты сканера запретов. Смысл набора: у каждого правила есть
/// пример нарушителя, и проверка обязана его находить. Без этого набор правил
/// был бы списком пожеланий, которые молча ничего не ловят.
/// </summary>
public sealed class ProhibitionScannerTests
{
    private const string CleanProject = "XEngine.Core";

    private const string CleanCode = """
        namespace XEngine.Core;

        public sealed class Sample
        {
            public int Value { get; init; }
        }

        """;

    private static readonly ProhibitionScanner Scanner = new(ProhibitionRules.Create());

    public static TheoryData<string> BackendsInCore()
    {
        TheoryData<string> data = new();
        foreach (string backend in
                 new[] { "Silk.NET.OpenGL", "Silk.NET.Windowing", "OpenTK", "OpenTK.Graphics", "SharpGLTF", "Box2D.NET", "Box2D", "JoltPhysicsSharp" })
        {
            data.Add($"using {backend};");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(BackendsInCore))]
    public void Scan_ReportsBackendInProjectWithoutBackends(string violatorLine)
    {
        using TempSourceTree tree = new(nameof(Scan_ReportsBackendInProjectWithoutBackends));
        tree.WriteFile(CleanProject, "Backend.cs", violatorLine);

        ProhibitionReport report = Scanner.Scan(tree.Root);

        AssertViolation(report, "core-does-not-know-backends", violatorLine);
    }

    [Fact]
    public void Scan_ReportsReflectionInAnyProjectOfSourceTree()
    {
        using TempSourceTree tree = new(nameof(Scan_ReportsReflectionInAnyProjectOfSourceTree));
        tree.WriteFile("XEngine.Mathematics", "Helper.cs", "using System.Reflection;");

        ProhibitionReport report = Scanner.Scan(tree.Root);

        AssertViolation(report, "no-reflection-in-source", "using System.Reflection;");
    }

    [Fact]
    public void Scan_ReportsConsoleOutsideLogSink()
    {
        using TempSourceTree tree = new(nameof(Scan_ReportsConsoleOutsideLogSink));
        tree.WriteFile(CleanProject, "Printer.cs", "Console.WriteLine(value);");

        ProhibitionReport report = Scanner.Scan(tree.Root);

        AssertViolation(report, "no-console-and-debug-outside-log-sink", "Console.WriteLine(value);");
    }

    [Fact]
    public void Scan_ReportsDebugWriteOutsideLogSink()
    {
        using TempSourceTree tree = new(nameof(Scan_ReportsDebugWriteOutsideLogSink));
        tree.WriteFile(CleanProject, "Printer.cs", "Debug.WriteLine(value);");

        ProhibitionReport report = Scanner.Scan(tree.Root);

        AssertViolation(report, "no-console-and-debug-outside-log-sink", "Debug.WriteLine(value);");
    }

    [Fact]
    public void Scan_ReportsEveryForbiddenReflectionApi()
    {
        string[] violators =
        [
            "System.Reflection.Assembly.Load(\"x\");",
            "Type.GetMethod(\"Run\");",
            "Type.GetProperty(\"Value\");",
            "Activator.CreateInstance(componentType);",
            "Assembly.GetTypes();",
        ];

        foreach (string violator in violators)
        {
            using TempSourceTree tree = new(nameof(Scan_ReportsEveryForbiddenReflectionApi));
            tree.WriteFile(CleanProject, "Reflection.cs", violator);

            ProhibitionReport report = Scanner.Scan(tree.Root);

            AssertViolation(report, "no-reflection-in-source", violator);
        }
    }

    [Fact]
    public void Scan_IgnoresBackendInBackendProject()
    {
        using TempSourceTree tree = new(nameof(Scan_IgnoresBackendInBackendProject));
        tree.WriteFile("XEngine.Graphics.OpenGL", "GlCalls.cs", "using Silk.NET.OpenGL;");

        ProhibitionReport report = Scanner.Scan(tree.Root);

        Assert.True(report.IsClean, report.Describe());
    }

    [Fact]
    public void Scan_IgnoresBackendNamesMentionedInComments()
    {
        using TempSourceTree tree = new(nameof(Scan_IgnoresBackendNamesMentionedInComments));
        tree.WriteFile(CleanProject, "Migration.cs", """
            namespace XEngine.Core;

            // Перенос с OpenTK и Box2D.NET на собственные бэкенды.
            /// <summary>Замена Silk.NET.OpenGL на IGraphicsBackend.</summary>
            /* Рефлексия System.Reflection запрещена инвариантом 2. */
            public sealed class Migration
            {
            }

            """);

        ProhibitionReport report = Scanner.Scan(tree.Root);

        Assert.True(report.IsClean, report.Describe());
    }

    [Fact]
    public void Scan_IgnoresConsoleInsideLogSinkImplementation()
    {
        using TempSourceTree tree = new(nameof(Scan_IgnoresConsoleInsideLogSinkImplementation));
        tree.WriteFile("XEngine.Core", "ConsoleLogSink.cs", """
            namespace XEngine.Core;

            public sealed class ConsoleLogSink : ILogSink
            {
                public void Info(string message) => Console.WriteLine(message);
            }

            """);

        ProhibitionReport report = Scanner.Scan(tree.Root);

        Assert.True(report.IsClean, report.Describe());
    }

    [Fact]
    public void Scan_IgnoresGeneratedFiles()
    {
        using TempSourceTree tree = new(nameof(Scan_IgnoresGeneratedFiles));
        tree.WriteFile(CleanProject, "Real.cs", CleanCode);
        tree.WriteGeneratedFile(CleanProject, "obj", "Generated.cs", "using Silk.NET.OpenGL;");
        tree.WriteGeneratedFile(CleanProject, "bin", "Copied.cs", "using System.Reflection;");

        ProhibitionReport report = Scanner.Scan(tree.Root);

        Assert.True(report.IsClean, report.Describe());
        Assert.Equal(1, report.ScannedFiles);
    }

    [Fact]
    public void Scan_ReportsProjectFileAndLineNumber()
    {
        using TempSourceTree tree = new(nameof(Scan_ReportsProjectFileAndLineNumber));
        tree.WriteFile(CleanProject, "Nested/Printer.cs", """
            namespace XEngine.Core;

            public sealed class Printer
            {
                public void Print() => Console.WriteLine("x");
            }

            """);

        ProhibitionReport report = Scanner.Scan(tree.Root);

        ProhibitionViolation violation = Assert.Single(report.Violations);
        Assert.Equal("no-console-and-debug-outside-log-sink", violation.RuleId);
        Assert.Equal(CleanProject, violation.Project);
        Assert.Equal("Printer.cs", Path.GetFileName(violation.RelativePath));
        Assert.Equal(5, violation.LineNumber);
        Assert.Contains("Console.WriteLine", violation.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_KeepsCleanTreeCleanAndReportsScope()
    {
        using TempSourceTree tree = new(nameof(Scan_KeepsCleanTreeCleanAndReportsScope));
        tree.WriteFile(CleanProject, "Sample.cs", CleanCode);
        tree.WriteFile("XEngine.Mathematics", "Sample.cs", CleanCode);

        ProhibitionReport report = Scanner.Scan(tree.Root);

        Assert.True(report.IsClean, report.Describe());
        Assert.Equal(2, report.ScannedFiles);
    }

    [Fact]
    public void Scan_ThrowsWhenSourceDirectoryIsMissing()
    {
        using TempSourceTree tree = new(nameof(Scan_ThrowsWhenSourceDirectoryIsMissing));

        Assert.Throws<DirectoryNotFoundException>(() => Scanner.Scan(tree.Root));
    }

    [Fact]
    public void Create_ReturnsRulesWithUniqueIdsAndNonEmptyPatterns()
    {
        IReadOnlyList<ProhibitionRule> rules = ProhibitionRules.Create();

        Assert.Equal(rules.Count, rules.Select(rule => rule.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(rules, rule =>
        {
            Assert.NotEmpty(rule.Id);
            Assert.NotEmpty(rule.Description);
            Assert.NotEmpty(rule.ForbiddenPatterns);
        });
    }

    private static void AssertViolation(ProhibitionReport report, string expectedRuleId, string expectedLine)
    {
        ProhibitionViolation violation = Assert.Single(report.Violations);
        Assert.Equal(expectedRuleId, violation.RuleId);
        Assert.Contains(expectedLine, violation.LineText, StringComparison.Ordinal);
        Assert.NotEmpty(violation.Pattern);
    }
}