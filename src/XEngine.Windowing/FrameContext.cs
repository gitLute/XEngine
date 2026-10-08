namespace XEngine.Windowing;

/// <summary>
/// Данные кадра, передаваемые цели цикла.
/// </summary>
/// <remarks>
/// Структура, а не класс: она передаётся по <c>in</c> на каждом кадре, и она
/// не меняется после создания. Значение, а не ссылка: цель цикла не должна
/// иметь возможности изменить то, что читает цикл (11.3).
/// <remarks>
/// Время кадра уже ограничено <see cref="Core.Configuration.SimulationConfig.MaxFrameTimeSeconds"/>,
/// поэтому цель может использовать его как есть: анимации интерполяции и
/// системы слежения получают именно ограниченный <c>dt</c> и после паузы не
/// прыгают.
/// </remarks>
/// </remarks>
/// <param name="FrameNumber">Номер кадра с нуля.</param>
/// <param name="DeltaSeconds">Время кадра в секундах, ограниченное сверху.</param>
/// <param name="SimulationAlpha">
/// Доля шага симуляции, накопленная к этому моменту: множитель интерполяции.
/// </param>
/// <param name="FramebufferSize">
/// Размер области рисования в пикселях на момент начала кадра.
/// </param>
public readonly record struct FrameContext(
    int FrameNumber,
    double DeltaSeconds,
    double SimulationAlpha,
    WindowSize FramebufferSize);