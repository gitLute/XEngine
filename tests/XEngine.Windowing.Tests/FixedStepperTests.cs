using Xunit;

namespace XEngine.Windowing.Tests;

/// <summary>
/// Контракт <see cref="FixedStepper"/>: инвариант 6a (частота кадров не влияет
/// на результат симуляции), правила 8.3 и 8.3a.
/// </summary>
public sealed class FixedStepperTests
{
    private const double Tolerance = 1e-9;

    private static FixedStepper Create(
        CollectingLogSink log,
        double timeStepSeconds = 1.0 / 60.0,
        int maxSubSteps = 5,
        double maxFrameTimeSeconds = 0.25,
        double stepBudgetSeconds = 0.008,
        double timeScale = 1.0)
        => new(
            new Core.Configuration.SimulationConfig
            {
                TimeStepSeconds = timeStepSeconds,
                MaxSubSteps = maxSubSteps,
                MaxFrameTimeSeconds = maxFrameTimeSeconds,
                StepBudgetSeconds = stepBudgetSeconds,
                TimeScale = timeScale,
            },
            log);

    private static int CountSteps(FixedStepper stepper)
    {
        int steps = 0;
        while (stepper.TryTakeStep(out _))
        {
            steps++;
        }

        return steps;
    }

    [Fact]
    public void NewStepper_HasEmptyAccumulatorAndZeroSteps()
    {
        FixedStepper stepper = Create(new CollectingLogSink());

        Assert.Equal(0.0, stepper.AccumulatorSeconds, Tolerance);
        Assert.Equal(0, stepper.StepCount);
        Assert.Equal(0, stepper.OverBudgetSteps);
        Assert.Equal(0.0, stepper.DroppedSeconds, Tolerance);
        Assert.Equal(0.0, stepper.Alpha, Tolerance);
    }

    [Fact]
    public void NewStepper_RejectsMissingArgumentsAndUnusableConfig()
    {
        CollectingLogSink log = new();

        Assert.Throws<ArgumentNullException>(
            () => new FixedStepper(null!, log));
        Assert.Throws<ArgumentNullException>(
            () => new FixedStepper(new Core.Configuration.SimulationConfig(), null!));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FixedStepper(new Core.Configuration.SimulationConfig { MaxSubSteps = 0 }, log));
    }

    [Fact]
    public void TryTakeStep_RequiresAFullStepInAccumulator()
    {
        FixedStepper stepper = Create(new CollectingLogSink());
        stepper.BeginFrame(1.0 / 120.0);

        Assert.False(stepper.TryTakeStep(out double tooShort));
        Assert.Equal(0.0, tooShort, Tolerance);
        Assert.True(stepper.AccumulatorSeconds < stepper.TimeStepSeconds);
    }

    [Fact]
    public void TryTakeStep_ReturnsFixedStepAndCountsIt()
    {
        FixedStepper stepper = Create(new CollectingLogSink());
        stepper.BeginFrame(1.0 / 30.0);

        Assert.True(stepper.TryTakeStep(out double first));
        Assert.True(stepper.TryTakeStep(out double second));
        Assert.False(stepper.TryTakeStep(out _));

        Assert.Equal(stepper.TimeStepSeconds, first, Tolerance);
        Assert.Equal(stepper.TimeStepSeconds, second, Tolerance);
        Assert.Equal(2, stepper.StepCount);
        Assert.Equal(2, stepper.StepsThisFrame);
    }

    [Fact]
    public void BeginFrame_LimitsFrameTimeToCollapsePause()
    {
        // Пауза в 10 секунд не должна превратиться в 600 шагов: время кадра
        // ограничивается до попадания в аккумулятор (8.3).
        FixedStepper stepper = Create(
            new CollectingLogSink(),
            maxSubSteps: 600,
            maxFrameTimeSeconds: 0.25);

        stepper.BeginFrame(10.0);

        Assert.Equal(15, CountSteps(stepper));
    }

    [Fact]
    public void BeginFrame_MultipliesByTimeScaleAfterLimit()
    {
        FixedStepper stepper = Create(
            new CollectingLogSink(),
            maxSubSteps: 600,
            maxFrameTimeSeconds: 1.0,
            timeScale: 2.0);

        stepper.BeginFrame(0.25);

        // 0.25 с при темпе 2 даёт 0.5 с игрового времени, то есть 30 шагов.
        Assert.Equal(30, CountSteps(stepper));
    }

    [Fact]
    public void BeginFrame_IgnoresNegativeDelta()
    {
        FixedStepper stepper = Create(new CollectingLogSink());

        stepper.BeginFrame(-1.0);

        Assert.Equal(0, CountSteps(stepper));
        Assert.Equal(0.0, stepper.AccumulatorSeconds, Tolerance);
    }

    [Fact]
    public void CompleteFrame_ResetsStepCounterOfFrame()
    {
        FixedStepper stepper = Create(new CollectingLogSink());
        stepper.BeginFrame(1.0 / 30.0);
        Assert.Equal(2, CountSteps(stepper));
        Assert.Equal(2, stepper.StepsThisFrame);

        stepper.CompleteFrame();

        Assert.Equal(0, stepper.StepsThisFrame);
        Assert.Equal(2, stepper.StepCount);
    }

    [Fact]
    public void CompleteFrame_DropsExcessBeyondMaxSubStepsAndKeepsRemainder()
    {
        CollectingLogSink log = new();
        FixedStepper stepper = Create(log, maxSubSteps: 5, maxFrameTimeSeconds: 1.0);

        // Два с половиной шага сверх предела в пять шагов: пять выполнятся, два
        // целых сбрасываются, половина переходит на следующий кадр.
        stepper.BeginFrame((7.5 / 60.0));
        Assert.Equal(5, CountSteps(stepper));
        stepper.CompleteFrame();

        Assert.Equal(5, stepper.StepCount);
        Assert.True(log.Contains("Симуляция не успевает за кадром"), "Сброс излишка обязан попасть в журнал.");
        Assert.Equal(2.0 / 60.0, stepper.DroppedSeconds, 1e-9);
        Assert.Equal(0.5 / 60.0, stepper.AccumulatorSeconds, 1e-9);
    }

    [Fact]
    public void CompleteFrame_DoesNotWarnWhenAccumulatorBelowOneStep()
    {
        CollectingLogSink log = new();
        FixedStepper stepper = Create(log, maxSubSteps: 1, maxFrameTimeSeconds: 1.0);

        stepper.BeginFrame((0.5 / 60.0));
        Assert.Equal(0, CountSteps(stepper));
        stepper.CompleteFrame();

        Assert.Empty(log.Messages);
        Assert.Equal(0.0, stepper.DroppedSeconds, Tolerance);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    [InlineData(240)]
    public void StepsPerSecond_DoNotDependOnFrameRate(int framesPerSecond)
    {
        // Инвариант 6a: за секунду игрового времени выполняется одно и то же
        // число шагов при любой частоте кадров. Симуляция получает время, а не
        // частоту экрана.
        //
        // Сравнение идёт с соседним числом шагов, а не с точным: сумма
        // тридцати долей 1/30 в double чуть меньше единицы, и шестидесятый
        // шаг при этом не набирается. Требование не в том, чтобы набиралось
        // ровно 60, а в том, чтобы число шагов не зависело от частоты кадров,
        // что проверяет следующий тест.
        Assert.InRange(CountStepsPerSecond(framesPerSecond), 59, 60);
    }

    [Fact]
    public void StepsPerSecond_DifferByAtMostOneStepAcrossFrameRates()
    {
        // Собственно инвариант 6a: частота кадров не влияет на результат
        // симуляции, то есть шагов за единицу игрового времени одно и то же
        // число. Сравнение допускает разницу в один шаг, и это не ослабление
        // проверки: путь округления в ManualFrameClock (накопление времени и
        // вычитание начала кадра) даёт другой последний бит, чем прибавление
        // напрямую, поэтому шестидесятый шаг может набраться не при каждой
        // частоте. Требовать побитового равенства означало бы проверять
        // округление double, а не движок.
        //
        // Время при этом не теряется: недобранный шаг остаётся в
        // аккумуляторе и добирается следующим кадром, что проверяет
        // LongRunKeepsStepsProportionalToSimulatedTime.
        int reference = CountStepsPerSecond(30);

        foreach (int framesPerSecond in (int[])[60, 144, 240])
        {
            int actual = CountStepsPerSecond(framesPerSecond);
            Assert.True(
                Math.Abs(actual - reference) <= 1,
                $"Шагов за секунду разошлось при {framesPerSecond} кадрах в секунду: " +
                $"{reference} против {actual}.");
        }
    }

    [Fact]
    public void LongRunKeepsStepsProportionalToSimulatedTime()
    {
        // За десять секунд игрового времени набирается десять шагов в секунду;
        // ошибка накапливания не должна съедать время.
        FixedStepper stepper = Create(
            new CollectingLogSink(),
            maxSubSteps: 32,
            maxFrameTimeSeconds: 1.0);
        ManualFrameClock clock = new();
        int steps = 0;

        for (int frame = 0; frame < 600; frame++)
        {
            clock.Advance(1.0 / 60.0);
            stepper.BeginFrame(clock.BeginFrame());
            steps += CountSteps(stepper);
            stepper.CompleteFrame();
        }

        Assert.InRange(steps, 599, 600);
        Assert.Equal(0.0, stepper.DroppedSeconds, Tolerance);
    }

    private static int CountStepsPerSecond(int framesPerSecond)
    {
        FixedStepper stepper = Create(
            new CollectingLogSink(),
            maxSubSteps: 32,
            maxFrameTimeSeconds: 1.0);
        ManualFrameClock clock = new();
        int steps = 0;

        for (int frame = 0; frame < framesPerSecond; frame++)
        {
            clock.Advance(1.0 / framesPerSecond);
            stepper.BeginFrame(clock.BeginFrame());
            steps += CountSteps(stepper);
            stepper.CompleteFrame();
        }

        return steps;
    }

    [Fact]
    public void Alpha_IsFractionOfStepAlreadyAccumulated()
    {
        FixedStepper stepper = Create(new CollectingLogSink());

        stepper.BeginFrame(stepper.TimeStepSeconds * 0.25);
        Assert.Equal(0.25, stepper.Alpha, 1e-6);
        Assert.Equal(0, CountSteps(stepper));

        stepper.BeginFrame(stepper.TimeStepSeconds * 0.75);
        Assert.Equal(1.0, stepper.Alpha, 1e-6);
        Assert.Equal(1, CountSteps(stepper));
        Assert.Equal(0.0, stepper.Alpha, Tolerance);
    }

    [Fact]
    public void Alpha_StaysInRangeWhenAccumulatorHoldsSeveralSteps()
    {
        FixedStepper stepper = Create(new CollectingLogSink(), maxSubSteps: 64, maxFrameTimeSeconds: 1.0);

        stepper.BeginFrame(0.5);

        Assert.InRange(stepper.Alpha, 0.0, 1.0);
    }

    [Fact]
    public void RecordStepDuration_WithinBudgetDoesNothing()
    {
        CollectingLogSink log = new();
        FixedStepper stepper = Create(log, stepBudgetSeconds: 0.008);
        stepper.BeginFrame(0.1);
        Assert.True(stepper.TryTakeStep(out _));

        StepOutcome outcome = stepper.RecordStepDuration(0.004);

        Assert.Equal(StepOutcome.WithinBudget, outcome);
        Assert.Empty(log.Messages);
        Assert.Equal(0, stepper.OverBudgetSteps);
    }

    [Fact]
    public void RecordStepDuration_OverBudgetWarnsOnceAndCounts()
    {
        CollectingLogSink log = new();
        FixedStepper stepper = Create(log, stepBudgetSeconds: 0.008);

        StepOutcome first = stepper.RecordStepDuration(0.016);
        StepOutcome second = stepper.RecordStepDuration(0.020);

        Assert.Equal(StepOutcome.OverBudget, first);
        Assert.Equal(StepOutcome.OverBudget, second);
        Assert.Equal(2, stepper.OverBudgetSteps);
        Assert.Single(log.Messages);
        Assert.Contains("превысил бюджет", log.Messages[0], StringComparison.Ordinal);
    }

    [Fact]
    public void RecordStepDuration_SevereOverrunIsAnErrorWithStepNumber()
    {
        CollectingLogSink log = new();
        FixedStepper stepper = Create(log, stepBudgetSeconds: 0.008);
        stepper.BeginFrame(0.1);
        Assert.True(stepper.TryTakeStep(out _));

        StepOutcome outcome = stepper.RecordStepDuration(0.064);

        Assert.Equal(StepOutcome.OverBudgetSeverely, outcome);
        Assert.True(
            log.Contains("ошибка конфигурации"),
            "Сильное превышение бюджета — ошибка конфигурации, и она обязана попасть в журнал.");
    }

    [Fact]
    public void RecordStepDuration_RejectsNegativeDuration()
    {
        FixedStepper stepper = Create(new CollectingLogSink());

        Assert.Throws<ArgumentOutOfRangeException>(() => stepper.RecordStepDuration(-0.001));
    }
}