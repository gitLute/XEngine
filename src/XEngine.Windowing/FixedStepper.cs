using XEngine.Core.Configuration;
using XEngine.Core.Logging;

namespace XEngine.Windowing;

/// <summary>
/// Механизм фиксированного шага: решает, сколько шагов симуляции нужно за
/// кадр, и следит за бюджетом времени шага (8.3, 8.3a).
/// </summary>
/// <remarks>
/// Инвариант, который механизм обеспечивает: **частота кадров не влияет на
/// результат симуляции** (6a). Время кадра складывается в аккумулятор, шаги
/// выполняются целыми по <see cref="TimeStepSeconds"/>, а остаток переносится на
/// следующий кадр, поэтому и при 30, и при 240 кадрах в секунду шагов в секунду
/// получается одно и то же.
/// <para>
/// Порядок вызовов на кадр: <see cref="BeginFrame"/> → <see cref="TryTakeStep"/>
/// столько раз, сколько вернёт <c>true</c> → <see cref="CompleteFrame"/>.
/// </para>
/// <para>
/// Экземпляр не хранит состояние, разделяемое между потоками: кадром владеет
/// поток рендера, и шаг считает тот же поток (11.3). Экземпляр не потокобезопасен
/// намеренно — блокировка на кадре обошлась бы дороже самой симуляции.
/// </para>
/// </remarks>
public sealed class FixedStepper
{
    /// <summary>
    /// Во сколько раз бюджет превышен, после чего превышение считается ошибкой
    /// конфигурации (8.3a).
    /// </summary>
    private const double SevereOverrunFactor = 4.0;

    /// <summary>
    /// Сколько сильных превышений проходит между сообщениями об ошибке.
    /// </summary>
    /// <remarks>
    /// Сильное превышение означает неверную конфигурацию, и сообщение нужно
    /// обязательно, но шаг с превышением вчетверо бывает на каждом кадре, и
    /// сообщение на каждом шаге превратило бы журнал в поток. Раз в минуту при
    /// 60 шагах в секунду достаточно, чтобы ошибка не потерялась, и не
    /// достаточно, чтобы забить вывод; между сообщениями превышения считаются и
    /// попадают в счётчик.
    /// </remarks>
    private const int SevereReportInterval = 60;

    private readonly SimulationConfig _config;
    private readonly ILogSink _log;
    private double _accumulatorSeconds;
    private int _stepsThisFrame;
    private int _stepsSinceLastSevereReport;
    private bool _budgetWarningReported;

    /// <summary>
    /// Создаёт механизм по конфигурации симуляции.
    /// </summary>
    /// <param name="config">Параметры шага; проверяются сразу при создании.</param>
    /// <param name="log">Журнал для диагностики превышений и сброса излишка.</param>
    /// <exception cref="ArgumentNullException">Конфигурация или журнал не заданы.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Конфигурация непригодна.</exception>
    public FixedStepper(SimulationConfig config, ILogSink log)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(log);

        config.Validate();

        _config = config;
        _log = log;
    }

    /// <summary>
    /// Длительность фиксированного шага в секундах.
    /// </summary>
    public double TimeStepSeconds => _config.TimeStepSeconds;

    /// <summary>
    /// Сколько шагов допускается за один кадр.
    /// </summary>
    public int MaxSubSteps => _config.MaxSubSteps;

    /// <summary>
    /// Бюджет времени одного шага в секундах.
    /// </summary>
    public double StepBudgetSeconds => _config.StepBudgetSeconds;

    /// <summary>
    /// Предел времени кадра в секундах.
    /// </summary>
    /// <remarks>
    /// Открыт наружу, потому что ограничение времени кадра — решение цикла, а
    /// не степера: цель цикла тоже должна получать уже ограниченное время. Два
    /// места читают одно и то же значение из конфигурации, а не хранят свои
    /// копии предела.
    /// </remarks>
    public double MaxFrameTimeSeconds => _config.MaxFrameTimeSeconds;

    /// <summary>
    /// Накопленное время, ещё не превращённое в шаги.
    /// </summary>
    public double AccumulatorSeconds => _accumulatorSeconds;

    /// <summary>
    /// Сколько шагов выполнено в текущем кадре.
    /// </summary>
    public int StepsThisFrame => _stepsThisFrame;

    /// <summary>
    /// Сколько шагов выполнено за всё время.
    /// </summary>
    public int StepCount { get; private set; }

    /// <summary>
    /// Сколько шагов превысило бюджет времени за всё время.
    /// </summary>
    public int OverBudgetSteps { get; private set; }

    /// <summary>
    /// Сколько времени аккумулятора сброшено излишком за всё время, в секундах.
    /// </summary>
    /// <remarks>
    /// Величина ненулевая означает, что симуляция не успевает за кадром и
    /// время теряется безвозвратно. Компенсировать его догоняющими шагами
    /// запрещено (8.3a): работа лавиной ухудшает ровно то, что уже плохо.
    /// </remarks>
    public double DroppedSeconds { get; private set; }

    /// <summary>
    /// Доля шага, накопленная к текущему моменту: доля времени от последнего
    /// шага до следующего.
    /// </summary>
    /// <remarks>
    /// Это множитель интерполяции горячих данных при отрисовке (11.7). Он
    /// всегда в пределах от нуля до единицы: за пределами интерполировать нечего.
    /// </remarks>
    public double Alpha => Math.Clamp(_accumulatorSeconds / TimeStepSeconds, 0.0, 1.0);

    /// <summary>
    /// Начинает кадр: ограничивает время кадра и добавляет его в аккумулятор.
    /// </summary>
    /// <param name="deltaSeconds">
    /// Время кадра в секундах. Ограничивается сверху
    /// <see cref="SimulationConfig.MaxFrameTimeSeconds"/>: после сворачивания
    /// окна не выполняется сорок шагов разом. Затем умножается на темп времени.
    /// </param>
    /// <remarks>
    /// Сначала ограничение, потом темп: ограничение отвечает за паузу, темп —
    /// за намерение ускорить или замедлить игру. Ускорение вчетверо при длинном
    /// кадре даст больше шагов, и это ожидаемо, а не повод обходить предел.
    /// </remarks>
    public void BeginFrame(double deltaSeconds)
    {
        double limited = Math.Min(Math.Max(deltaSeconds, 0.0), _config.MaxFrameTimeSeconds);
        _accumulatorSeconds += limited * _config.TimeScale;
        _stepsThisFrame = 0;
    }

    /// <summary>
    /// Отдаёт очередной шаг, если аккумулятор накопил достаточно времени и
    /// предел шагов за кадр не исчерпан.
    /// </summary>
    /// <param name="timeStepSeconds">
    /// Длительность шага в секундах; равна <see cref="TimeStepSeconds"/> при
    /// успехе и нулю при отказе.
    /// </param>
    /// <returns><c>true</c>, если шаг нужно выполнить.</returns>
    public bool TryTakeStep(out double timeStepSeconds)
    {
        if (_stepsThisFrame >= MaxSubSteps || _accumulatorSeconds < TimeStepSeconds)
        {
            timeStepSeconds = 0.0;
            return false;
        }

        _accumulatorSeconds -= TimeStepSeconds;
        _stepsThisFrame++;
        StepCount++;
        timeStepSeconds = TimeStepSeconds;
        return true;
    }

    /// <summary>
    /// Завершает кадр: сбрасывает излишек аккумулятора, если предел шагов
    /// исчерпан, и обнуляет счётчик шагов кадра.
    /// </summary>
    /// <remarks>
    /// Излишек сбрасывается целыми шагами, а дробный остаток меньше одного шага
    /// переносится на следующий кадр: он не является потерей времени и его
    /// сохранение не создаёт лавины.
    /// </remarks>
    public void CompleteFrame()
    {
        _stepsThisFrame = 0;

        if (_accumulatorSeconds < TimeStepSeconds)
        {
            return;
        }

        double leftover = _accumulatorSeconds % TimeStepSeconds;
        int droppedSteps = (int)((_accumulatorSeconds - leftover) / TimeStepSeconds);
        double dropped = _accumulatorSeconds - leftover;

        _accumulatorSeconds = leftover;
        DroppedSeconds += dropped;

        _log.Write(
            LogLevel.Warning,
            $"Симуляция не успевает за кадром: сброшено {dropped:F3} с ({droppedSteps} шагов). " +
            "Число шагов за кадр ограничено, догоняющие шаги не выполняются: они ухудшают задержку.");
    }

    /// <summary>
    /// Отмечает, сколько времени занял шаг, и сообщает, уложился ли он в бюджет.
    /// </summary>
    /// <param name="durationSeconds">Время шага в секундах.</param>
    /// <returns>Результат сравнения с бюджетом.</returns>
    /// <remarks>
    /// Шаг не прерывается: прерывание оставило бы мир в недопустимо
    /// промежуточном состоянии. Превышение бюджета сверх
    /// <see cref="SevereOverrunFactor"/> раз считается ошибкой конфигурации.
    /// Имя самой тяжёлой системы в сообщение попадёт вместе с системами на
    /// этапе 14: до этого измерять нечего, а шагает один вызов симуляции.
    /// </remarks>
    public StepOutcome RecordStepDuration(double durationSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(durationSeconds);

        StepOutcome outcome = durationSeconds <= StepBudgetSeconds
            ? StepOutcome.WithinBudget
            : durationSeconds <= StepBudgetSeconds * SevereOverrunFactor
                ? StepOutcome.OverBudget
                : StepOutcome.OverBudgetSeverely;

        if (outcome == StepOutcome.WithinBudget)
        {
            return outcome;
        }

        OverBudgetSteps++;

        if (outcome == StepOutcome.OverBudget)
        {
            if (!_budgetWarningReported)
            {
                _budgetWarningReported = true;
                _log.Write(
                    LogLevel.Warning,
                    $"Шаг симуляции превысил бюджет {StepBudgetSeconds * 1000.0:F1} мс. " +
                    "Проверьте бюджет конфигурации и тяжесть систем симуляции.");
            }

            return outcome;
        }

        _stepsSinceLastSevereReport++;
        if (_stepsSinceLastSevereReport == 1 || _stepsSinceLastSevereReport >= SevereReportInterval)
        {
            string repeats = _stepsSinceLastSevereReport == 1
                ? string.Empty
                : $" Повторов с последнего сообщения: {_stepsSinceLastSevereReport - 1}.";
            _stepsSinceLastSevereReport = 0;

            _log.Write(
                LogLevel.Error,
                $"Шаг симуляции № {StepCount} превысил бюджет в несколько раз " +
                $"({durationSeconds * 1000.0:F1} мс при бюджете {StepBudgetSeconds * 1000.0:F1} мс). " +
                "Это ошибка конфигурации: бюджет занижен или система слишком тяжёлая." + repeats);
        }

        return outcome;
    }
}