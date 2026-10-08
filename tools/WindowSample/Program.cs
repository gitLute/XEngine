using XEngine.Core.Configuration;
using XEngine.Core.Logging;

namespace XEngine.Windowing.Sample;

/// <summary>
/// Точка входа проверочного примера окна.
/// </summary>
/// <remarks>
/// Пример запускается вручную и нужен для критерия приёмки этапа 3 «появляется
/// окно с очисткой кадра». Проверка с экраном выполняется человеком: тест по
/// общему правилу не должен требовать окна (17.6), а автоматический прогон не
/// имеет дисплея.
/// </remarks>
public static class Program
{
    /// <summary>
    /// Запускает окно, выполняет заданное число кадров и завершает работу.
    /// </summary>
    /// <param name="args">
    /// Необязательные аргументы: ширина, высота и число кадров. Без них окно
    /// имеет размер по умолчанию и закрывается по требованию пользователя.
    /// </param>
    /// <returns>Код возврата: ноль при успешном завершении цикла.</returns>
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ConsoleLogSink log = new();
        WindowConfig config = new();

        int frameLimit = 0;
        if (args.Length >= 2)
        {
            config = config with
            {
                Width = int.Parse(args[0]),
                Height = int.Parse(args[1]),
            };

            if (args.Length > 2)
            {
                frameLimit = int.Parse(args[2]);
            }
        }

        try
        {
            WindowSample.Run(config, log, frameLimit);
            return 0;
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or ArgumentException
            or ArgumentOutOfRangeException)
        {
            log.Write(LogLevel.Error, $"Окно не запущено: {exception.Message}");
            return 1;
        }
    }
}