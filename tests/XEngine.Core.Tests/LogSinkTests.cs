using XEngine.Core.Configuration;
using XEngine.Core.Logging;
using Xunit;

namespace XEngine.Core.Tests;

/// <summary>
/// Контракт журнала: журнал — единственный способ писать из движка, поэтому
/// у него есть проверка уровня, заглушка и потокобезопасная запись (17.5, 11.10).
/// </summary>
public sealed class LogSinkTests
{
    [Fact]
    public void Null_SwallowsEverythingWithoutThrowing()
    {
        ILogSink sink = ILogSink.Null;

        Assert.False(sink.IsEnabled(LogLevel.Debug));
        Assert.False(sink.IsEnabled(LogLevel.Information));
        Assert.False(sink.IsEnabled(LogLevel.Warning));
        Assert.False(sink.IsEnabled(LogLevel.Error));
        sink.Write(LogLevel.Error, "сообщение не должно никуда попасть");
    }

    [Fact]
    public void Console_WritesAcceptedMessages()
    {
        using StringWriter output = new();
        TextWriter original = Console.Out;
        Console.SetOut(output);
        try
        {
            ILogSink sink = new ConsoleLogSink();

            sink.Write(LogLevel.Information, "окно создано");

            Assert.Contains("окно создано", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("INF", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Fact]
    public void Console_FiltersByMinimumLevel()
    {
        using StringWriter output = new();
        TextWriter original = Console.Out;
        Console.SetOut(output);
        try
        {
            ILogSink sink = new ConsoleLogSink(LogLevel.Warning);

            Assert.False(sink.IsEnabled(LogLevel.Debug));
            Assert.False(sink.IsEnabled(LogLevel.Information));
            Assert.True(sink.IsEnabled(LogLevel.Warning));
            Assert.True(sink.IsEnabled(LogLevel.Error));

            sink.Write(LogLevel.Information, "не должно попасть");
            sink.Write(LogLevel.Error, "должно попасть");

            string text = output.ToString();
            Assert.DoesNotContain("не должно попасть", text, StringComparison.Ordinal);
            Assert.Contains("должно попасть", text, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Fact]
    public void Console_RejectsNullMessage()
    {
        ILogSink sink = new ConsoleLogSink();

        Assert.Throws<ArgumentNullException>(() => sink.Write(LogLevel.Error, null!));
    }

    [Fact]
    public void LogLevel_IsOrderedByImportance()
    {
        Assert.True(LogLevel.Debug < LogLevel.Information);
        Assert.True(LogLevel.Information < LogLevel.Warning);
        Assert.True(LogLevel.Warning < LogLevel.Error);
    }
}