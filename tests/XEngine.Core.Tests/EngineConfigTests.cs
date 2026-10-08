using XEngine.Core.Configuration;
using Xunit;

namespace XEngine.Core.Tests;

/// <summary>
/// Контракт конфигурации: значения по умолчанию берутся из требований 8.3 и
/// 8.3a, непригодные значения отвергаются до создания окна и запуска цикла,
/// а конфигурация иммутабельна (14.1).
/// </summary>
public sealed class EngineConfigTests
{
    [Fact]
    public void Defaults_MatchStageRequirements()
    {
        SimulationConfig simulation = new();

        Assert.Equal(1.0 / 60.0, simulation.TimeStepSeconds, 10);
        Assert.Equal(5, simulation.MaxSubSteps);
        Assert.Equal(0.25, simulation.MaxFrameTimeSeconds, 10);
        Assert.Equal(0.008, simulation.StepBudgetSeconds, 10);
        Assert.Equal(1.0, simulation.TimeScale, 10);
        Assert.Equal(ThreadingMode.SingleThreaded, simulation.Threading);
        Assert.True(simulation.Interpolate);
    }

    [Fact]
    public void WindowDefaults_AreUsable()
    {
        WindowConfig window = new();

        Assert.Equal(1280, window.Width);
        Assert.Equal(720, window.Height);
        Assert.True(window.VSync);
        Assert.True(window.Resizable);
        Assert.True(window.Visible);
    }

    [Fact]
    public void Defaults_PassValidation()
    {
        EngineConfig config = new();

        config.Validate();
    }

    [Fact]
    public void Window_RejectsUnusableSizeAndTitle()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowConfig(Width: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowConfig(Height: -1).Validate());
        Assert.Throws<ArgumentException>(() => new WindowConfig(Title: "  ").Validate());
        Assert.Throws<ArgumentException>(() => new WindowConfig(Title: string.Empty).Validate());
    }

    [Fact]
    public void Simulation_RejectsUnusableStepParameters()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationConfig { TimeStepSeconds = 0.0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationConfig { TimeStepSeconds = -1.0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationConfig { MaxSubSteps = 0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationConfig { MaxFrameTimeSeconds = 0.0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationConfig { StepBudgetSeconds = 0.0 }.Validate());
    }

    [Fact]
    public void Simulation_RejectsUnusableTimeScale()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationConfig { TimeScale = 0.0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationConfig { TimeScale = -2.0 }.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SimulationConfig { TimeScale = double.PositiveInfinity }.Validate());
    }

    [Fact]
    public void EngineConfig_PassesValidationOfEverySection()
    {
        EngineConfig config = new()
        {
            Window = new WindowConfig(Width: 800, Height: 600),
            Simulation = new SimulationConfig { MaxSubSteps = 3 },
        };

        config.Validate();

        Assert.Equal(800, config.Window.Width);
        Assert.Equal(3, config.Simulation.MaxSubSteps);
    }

    [Fact]
    public void Config_IsImmutableAndCopiedByWithExpression()
    {
        SimulationConfig original = new();
        SimulationConfig changed = original with { TimeScale = 0.5 };

        Assert.Equal(1.0, original.TimeScale, 10);
        Assert.Equal(0.5, changed.TimeScale, 10);
        Assert.NotSame(original, changed);
    }
}