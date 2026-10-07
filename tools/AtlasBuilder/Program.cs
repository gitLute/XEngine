namespace AtlasBuilder;

/// <summary>
/// Точка входа инструмента сборки атласов.
/// </summary>
/// <remarks>
/// Инструмент, а не часть движка (5.5): он пишет в консоль отчёт о своей работе,
/// потому что его вывод и есть пользовательский интерфейс. Правило «логи только
/// через ILogSink» относится к коду движка и к пути кадра.
/// </remarks>
public static class Program
{
    private const string DefaultAtlasName = "world";

    /// <summary>
    /// Код возврата: описание или исходники противоречат друг другу.
    /// </summary>
    public const int ExitBuildFailed = 1;

    /// <summary>
    /// Код возврата: неверные аргументы командной строки.
    /// </summary>
    public const int ExitUsageError = 2;

    /// <summary>
    /// Собирает атлас по описанию из командной строки.
    /// </summary>
    /// <param name="args">Аргументы: <c>--source каталог --output каталог [--atlas имя]</c>.</param>
    /// <returns>Код возврата для процессов сборки.</returns>
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        try
        {
            return Run(args);
        }
        catch (AtlasBuildException exception)
        {
            Console.Error.WriteLine($"Ошибка сборки атласа: {exception.Message}");
            return ExitBuildFailed;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"Ошибка аргументов: {exception.Message}");
            Console.Error.WriteLine(Usage);
            return ExitUsageError;
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"Ошибка ввода-вывода: {exception.Message}");
            return ExitBuildFailed;
        }
    }

    /// <summary>
    /// Текст справки по запуску.
    /// </summary>
    /// <returns>Строки справки.</returns>
    public static string Usage =>
        "Сборка атласа: AtlasBuilder --source assets/textures/source --output assets/textures" + Environment.NewLine
        + "  --source  каталог с описанием регионов (по умолчанию world.json)" + Environment.NewLine
        + "  --output  каталог для atlas.png и atlas.json" + Environment.NewLine
        + "  --atlas   имя описания без расширения, по умолчанию " + DefaultAtlasName;

    private static int Run(string[] args)
    {
        if (args.Length == 1 && IsHelp(args[0]))
        {
            Console.WriteLine(Usage);
            return 0;
        }

        string? sourceDirectory = null;
        string? outputDirectory = null;
        string atlasName = DefaultAtlasName;

        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--source" when index + 1 < args.Length:
                    sourceDirectory = args[++index];
                    break;
                case "--output" when index + 1 < args.Length:
                    outputDirectory = args[++index];
                    break;
                case "--atlas" when index + 1 < args.Length:
                    atlasName = args[++index];
                    break;
                default:
                    throw new ArgumentException($"Неизвестный аргумент: {args[index]}");
            }
        }

        if (sourceDirectory is null || outputDirectory is null)
        {
            Console.Error.WriteLine(Usage);
            return ExitUsageError;
        }

        return Build(sourceDirectory, outputDirectory, atlasName);
    }

    private static int Build(string sourceDirectory, string outputDirectory, string atlasName)
    {
        string descriptionPath = Path.Combine(sourceDirectory, atlasName + ".json");
        AtlasSourceDescription description = AtlasJson.ReadSource(descriptionPath);

        AtlasBuilder builder = new(new PngSourceReader());
        BuiltAtlas atlas = builder.Build(description, sourceDirectory);

        AtlasFileWriter writer = new(new BlankRasterizer());
        (string imagePath, string descriptionOutputPath) = writer.Write(outputDirectory, atlas);

        Console.WriteLine($"Атлас '{atlas.Name}': {atlas.Width}x{atlas.Height}, регионов: {atlas.Regions.Count}.");
        foreach (BuiltAtlasRegion region in atlas.Regions)
        {
            Console.WriteLine($"  {region.Name}: {region.Rect}, тексели {region.SizeTexels.X}x{region.SizeTexels.Y}.");
        }

        Console.WriteLine($"Изображение: {imagePath}");
        Console.WriteLine($"Описание: {descriptionOutputPath}");
        return 0;
    }

    private static bool IsHelp(string argument)
        => argument is "--help" or "-h" or "/?";
}