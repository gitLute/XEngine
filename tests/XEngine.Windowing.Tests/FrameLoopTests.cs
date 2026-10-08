using XEngine.Core.Configuration;
using Xunit;

namespace XEngine.Windowing.Tests;

/// <summary>
/// Контракт цикла кадра: цикл останавливается по <c>IsClosing</c>, время кадра
/// ограничено, шаги выполняются до отрисовки, а проверяется всё это без окна,
/// GPU и реального времени (17.6).
/// </summary>
public sealed class FrameLoopTests
{
    private const double Tolerance = 1e-9;

    private static FrameLoop CreateLoop(
        FakeWindow window,
        ManualFrameClock clock,
        SimulationConfig config,
        out FixedStepper stepper)
    {
        stepper = new FixedStepper(config, new CollectingLogSink());
        return new FrameLoop(window, clock, stepper);
    }

    /// <summary>
    /// Заглушка цели цикла: пишет порядок событий и закрывает окно после
    /// заданного числа кадров.
    /// </summary>
    private sealed class RecordingTarget : IFrameLoopTarget
    {
        private readonly FakeWindow _window;
        private readonly int _closeAfterFrames;
        private readonly List<string> _order;

        public RecordingTarget(FakeWindow window, List<string> order, int closeAfterFrames)
        {
            _window = window;
            _order = order;
            _closeAfterFrames = closeAfterFrames;
        }

        public int FrameCount { get; private set; }

        public int StepCount { get; private set; }

        public List<double> Deltas { get; } = [];

        public List<double> Alphas { get; } = [];

        public List<WindowSize> Sizes { get; } = [];

        public List<int> FrameNumbers { get; } = [];

        public void Simulate(double stepSeconds, int stepNumber)
        {
            _order.Add("шаг");
            StepCount++;
        }

        public void Render(in FrameContext context)
        {
            _order.Add("кадр");
            FrameCount++;
            Deltas.Add(context.DeltaSeconds);
            Alphas.Add(context.SimulationAlpha);
            Sizes.Add(context.FramebufferSize);
            FrameNumbers.Add(context.FrameNumber);

            if (FrameCount >= _closeAfterFrames)
            {
                _window.SimulateUserClose();
            }
        }
    }

    [Fact]
    public void Run_StopsWhenWindowReportsClosing()
    {
        FakeWindow window = new();
        RecordingTarget target = new(window, [], closeAfterFrames: 3);
        FrameLoop loop = CreateLoop(window, new ManualFrameClock(), new SimulationConfig(), out FixedStepper stepper);

        FrameLoopStats stats = loop.Run(target);

        Assert.Equal(3, stats.FrameCount);
        Assert.Equal(3, window.PollCount);
    }

    [Fact]
    public void Run_DoesNothingWhenWindowIsAlreadyClosing()
    {
        FakeWindow window = new();
        window.SimulateUserClose();
        RecordingTarget target = new(window, [], closeAfterFrames: 1);
        FrameLoop loop = CreateLoop(window, new ManualFrameClock(), new SimulationConfig(), out FixedStepper stepper);

        FrameLoopStats stats = loop.Run(target);

        Assert.Equal(0, stats.FrameCount);
        Assert.Equal(0, window.PollCount);
        Assert.Equal(0, target.FrameCount);
    }

    [Fact]
    public void Run_LimitsFrameTimeSoPauseDoesNotTurnIntoDozensOfSteps()
    {
        FakeWindow window = new();
        ManualFrameClock clock = new();
        FrameLoop loop = CreateLoop(
            window,
            clock,
            new SimulationConfig { MaxFrameTimeSeconds = 0.25, MaxSubSteps = 600 },
            out FixedStepper stepper);

        List<double> deltas = [];
        int frames = 0;
        loop.Run(new DelegateTarget((context, _) =>
        {
            deltas.Add(context.DeltaSeconds);
            if (++frames == 1)
            {
                clock.AdvanceTo(10.0);
            }
            else
            {
                window.SimulateUserClose();
            }
        }));

        Assert.Equal(2, deltas.Count);
        Assert.True(
            deltas[1] <= 0.25 + Tolerance,
            $"Время кадра после паузы обязано ограничиваться: {deltas[1]}.");
        Assert.True(stepper.StepCount <= 16, $"Шагов после паузы слишком много: {stepper.StepCount}.");
    }

    [Fact]
    public void Run_ExecutesSimulationStepsBeforeRenderOfSameFrame()
    {
        // Кадр в два с половиной шага: два шага набираются, половина переходит
        // дальше. Кадр ровно в один шаг дал бы один шаг, и порядок не было бы
        // видно.
        const double stepSeconds = 1.0 / 30.0;
        ManualFrameClock clock = new();
        FakeWindow window = new(clock, stepSeconds * 2.5);
        List<string> order = [];
        RecordingTarget target = new(window, order, closeAfterFrames: 1);
        FrameLoop loop = CreateLoop(
            window,
            clock,
            new SimulationConfig { TimeStepSeconds = stepSeconds },
            out FixedStepper stepper);

        loop.Run(target);

        // Оба шага обязаны идти до отрисовки: иначе кадр рисует состояние
        // неполного шага симуляции.
        Assert.Equal(["шаг", "шаг", "кадр"], order);
        Assert.Equal(2, stepper.StepCount);
    }

    [Fact]
    public void Run_RendersOncePerFrameEvenWithoutSimulationSteps()
    {
        ManualFrameClock clock = new();
        FakeWindow window = new(clock, 0.01);
        List<string> order = [];
        RecordingTarget target = new(window, order, closeAfterFrames: 2);
        FrameLoop loop = CreateLoop(
            window,
            clock,
            new SimulationConfig { TimeStepSeconds = 1.0 },
            out FixedStepper stepper);

        loop.Run(target);

        Assert.Equal(2, target.FrameCount);
        Assert.Equal(0, stepper.StepCount);
    }

    [Fact]
    public void Run_PollsEventsBeforeEveryFrame()
    {
        FakeWindow window = new();
        RecordingTarget target = new(window, [], closeAfterFrames: 4);
        FrameLoop loop = CreateLoop(window, new ManualFrameClock(), new SimulationConfig(), out FixedStepper stepper);

        loop.Run(target);

        Assert.Equal(4, window.PollCount);
    }

    [Fact]
    public void Run_ReportsFrameNumberFramebufferSizeAndAlpha()
    {
        ManualFrameClock clock = new();
        FakeWindow window = new(new WindowSize(1024, 768));
        FrameLoop loop = CreateLoop(
            window,
            clock,
            new SimulationConfig { TimeStepSeconds = 1.0 },
            out FixedStepper stepper);

        RecordingTarget target = new(window, [], closeAfterFrames: 2);
        loop.Run(target);

        Assert.Equal([0, 1], target.FrameNumbers);
        Assert.All(target.Sizes, size => Assert.Equal(new WindowSize(1024, 768), size));

        // Первый кадр имеет нулевое время: шага ещё не было, интерполировать
        // нечего, и множитель обязан быть нулём, а не случайной долей.
        Assert.Equal(0.0, target.Alphas[0], 1e-6);
    }

    [Fact]
    public void Run_PassesFramebufferSizeAfterResize()
    {
        FakeWindow window = new(new WindowSize(800, 600));
        ManualFrameClock clock = new();
        FrameLoop loop = CreateLoop(window, clock, new SimulationConfig(), out FixedStepper stepper);

        List<WindowSize> sizes = [];
        int frames = 0;
        loop.Run(new DelegateTarget((context, _) =>
        {
            sizes.Add(context.FramebufferSize);
            if (++frames == 2)
            {
                window.SimulateResize(new WindowSize(1920, 1080));
            }
            else if (frames == 3)
            {
                window.SimulateUserClose();
            }
        }));

        Assert.Equal(3, sizes.Count);
        Assert.Equal(new WindowSize(800, 600), sizes[1]);
        Assert.Equal(new WindowSize(1920, 1080), sizes[2]);
    }

    [Fact]
    public void Run_CompletesStepperAfterEachFrame()
    {
        ManualFrameClock clock = new();
        FakeWindow window = new(clock, 0.2);
        SimulationConfig config = new()
        {
            TimeStepSeconds = 1.0 / 60.0,
            MaxSubSteps = 2,
            MaxFrameTimeSeconds = 1.0,
        };
        FrameLoop loop = CreateLoop(window, clock, config, out FixedStepper stepper);

        RecordingTarget target = new(window, [], closeAfterFrames: 3);
        loop.Run(target);

        // Без CompleteFrame счётчик шагов кадра не обнулялся бы и через
        // MaxSubSteps кадров цикл перестал бы делать шаги вовсе.
        Assert.True(stepper.DroppedSeconds > 0.0, "Излишек обязан сбрасываться по завершении кадра.");
        Assert.True(stepper.StepCount >= 3, $"Шагов слишком мало: {stepper.StepCount}.");
    }

    [Fact]
    public void Run_StepsCountIsIndependentOfFrameRate()
    {
        static int CountSteps(int framesPerSecond, SimulationConfig config)
        {
            ManualFrameClock clock = new();
            FakeWindow window = new(clock, 1.0 / framesPerSecond);
            RecordingTarget target = new(window, [], closeAfterFrames: framesPerSecond);
            FrameLoop loop = new(window, clock, new FixedStepper(config, new CollectingLogSink()));

            loop.Run(target);
            return target.StepCount;
        }

        SimulationConfig config = new()
        {
            MaxSubSteps = 64,
            MaxFrameTimeSeconds = 1.0,
        };

        // Инвариант 6a проверяется на всём цикле, а не только на степпере:
        // частота кадров не должна влиять на число шагов симуляции.
        int at240 = CountSteps(240, config);
        int at30 = CountSteps(30, config);

        Assert.InRange(at240, 59, 60);
        Assert.True(
            Math.Abs(at240 - at30) <= 1,
            $"Шагов за секунду разошлось: 30 к/с дало {at30}, 240 к/с дало {at240}.");
    }

    [Fact]
    public void Constructor_RejectsMissingArguments()
    {
        FakeWindow window = new();
        ManualFrameClock clock = new();
        FixedStepper stepper = new FixedStepper(new SimulationConfig(), new CollectingLogSink());

        Assert.Throws<ArgumentNullException>(() => new FrameLoop(null!, clock, stepper));
        Assert.Throws<ArgumentNullException>(() => new FrameLoop(window, null!, stepper));
        Assert.Throws<ArgumentNullException>(() => new FrameLoop(window, clock, null!));
    }

    [Fact]
    public void Run_RejectsMissingTarget()
    {
        FrameLoop loop = new(
            new FakeWindow(),
            new ManualFrameClock(),
            new FixedStepper(new SimulationConfig(), new CollectingLogSink()));

        Assert.Throws<ArgumentNullException>(() => loop.Run(null!));
    }

    /// <summary>
    /// Заглушка цели, заданная делегатом отрисовки: тесты, которым не нужен
    /// порядок событий, не заставляют вести его журнал.
    /// </summary>
    private sealed class DelegateTarget : IFrameLoopTarget
    {
        private readonly Action<FrameContext, IFrameLoopTarget> _onRender;

        public DelegateTarget(Action<FrameContext, IFrameLoopTarget> onRender)
        {
            _onRender = onRender;
        }

        public void Simulate(double stepSeconds, int stepNumber)
        {
        }

        public void Render(in FrameContext context) => _onRender(context, this);
    }
}