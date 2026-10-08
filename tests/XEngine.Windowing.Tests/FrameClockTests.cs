using Xunit;

namespace XEngine.Windowing.Tests;

/// <summary>
/// Контракт часов: измеряют время и ничего не решают. Ограничение времени кадра
/// — дело <see cref="FixedStepper"/>, поэтому в тестах часы проверяются сами по
/// себе, без симуляции.
/// </summary>
public sealed class FrameClockTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void ManualFrameClock_FirstFrameHasNoTime()
    {
        ManualFrameClock clock = new();

        Assert.Equal(0.0, clock.BeginFrame(), Tolerance);
    }

    [Fact]
    public void ManualFrameClock_ReturnsElapsedTimeBetweenFrames()
    {
        ManualFrameClock clock = new();
        Assert.Equal(0.0, clock.BeginFrame(), Tolerance);

        clock.Advance(0.25);
        Assert.Equal(0.25, clock.BeginFrame(), Tolerance);

        clock.Advance(0.5);
        Assert.Equal(0.5, clock.BeginFrame(), Tolerance);
    }

    [Fact]
    public void ManualFrameClock_AdvanceToSetsAbsoluteTime()
    {
        ManualFrameClock clock = new();

        clock.AdvanceTo(3.5);

        Assert.Equal(3.5, clock.NowSeconds, Tolerance);
        Assert.Equal(3.5, clock.BeginFrame(), Tolerance);
    }

    [Fact]
    public void ManualFrameClock_StartsFromGivenMoment()
    {
        ManualFrameClock clock = new(100.0);
        clock.AdvanceTo(101.0);

        Assert.Equal(1.0, clock.BeginFrame(), Tolerance);
    }

    [Fact]
    public void ManualFrameClock_RejectsGoingBackwards()
    {
        ManualFrameClock clock = new(5.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceTo(4.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ManualFrameClock(-1.0));
    }

    [Fact]
    public void ManualFrameClock_FrameTimeNeverNegative()
    {
        ManualFrameClock clock = new();
        clock.BeginFrame();

        clock.AdvanceTo(0.0);
        Assert.Equal(0.0, clock.BeginFrame(), Tolerance);
    }

    [Fact]
    public void SystemFrameClock_ReportsNonNegativeTimeAndMovesForward()
    {
        IFrameClock clock = new SystemFrameClock();
        double before = clock.NowSeconds;

        double first = clock.BeginFrame();
        double second = clock.BeginFrame();
        double after = clock.NowSeconds;

        Assert.True(first >= 0.0, $"Время первого кадра отрицательно: {first}");
        Assert.True(second >= 0.0, $"Время второго кадра отрицательно: {second}");

        // Монотонно время, а не длительность кадра: кадр после долгого паузу
        // короче предыдущего, и это нормально. Проверять рост длительности
        // кадра означало бы запретить быстрый кадр после долгого.
        Assert.True(after >= before, $"Время идёт назад: {before} → {after}");
    }

    [Fact]
    public void SystemFrameClock_MeasuresBusyWaitAsPositiveTime()
    {
        IFrameClock clock = new SystemFrameClock();
        clock.BeginFrame();

        Thread.SpinWait(50_000);

        double frame = clock.BeginFrame();
        Assert.True(frame > 0.0, $"Ожидание должно занять время, а не ноль: {frame}");
    }
}