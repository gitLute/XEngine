namespace XEngine.Core.Configuration;

/// <summary>
/// Режим исполнения цикла.
/// </summary>
public enum ThreadingMode
{
    /// <summary>
    /// Симуляция и рендер в одном потоке. Режим по умолчанию: он нужен для
    /// отладки, для проверки детерминизма без гонок и для запуска в
    /// окружениях без второго потока (11.9).
    /// </summary>
    SingleThreaded = 0,

    /// <summary>
    /// Симуляция на своём потоке, кадром владеет поток рендера. Появляется на
    /// этапе 14 вместе с <c>IThreadingStrategy</c>.
    /// </summary>
    Split = 1,
}