using System.Diagnostics;

namespace XEngine.Windowing;

/// <summary>
/// Настенные часы на <see cref="Stopwatch"/>.
/// </summary>
/// <remarks>
/// Отсчёт начинается при создании, поэтому первый кадр получает время с
/// момента создания часов, а не с момента запуска цикла: иначе первая итерация
/// зависит от того, как долго собирался граф зависимостей.
/// </remarks>
public sealed class SystemFrameClock : IFrameClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private double _lastFrameSeconds;

    /// <inheritdoc/>
    public double NowSeconds => _stopwatch.Elapsed.TotalSeconds;

    /// <inheritdoc/>
    public double BeginFrame()
    {
        double now = NowSeconds;
        double frameSeconds = now - _lastFrameSeconds;
        _lastFrameSeconds = now;

        // Stopwatch монотонен, поэтому отрицательное значение означало бы
        // повреждение состояния, а не медленный кадр.
        return frameSeconds < 0.0 ? 0.0 : frameSeconds;
    }
}