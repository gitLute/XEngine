using System.Text.Json;
using System.Text.Json.Serialization;

// CA2227 отключён: описание файла требует сеттера коллекции для
// System.Text.Json. Внутренняя модель собранного атласа коллекцию наружу
// не отдаёт на запись.
#pragma warning disable CA2227

namespace AtlasBuilder;

/// <summary>
/// Чтение и запись описаний атласа в JSON.
/// </summary>
/// <remarks>
/// Сериализация идёт через контекст исходного кода, а не через рефлексию:
/// требование отсутствия рефлексии (инвариант 2) распространяется и на
/// инструмент, иначе сборщик ассетов станет местом, где правило не действует.
/// </remarks>
public static class AtlasJson
{
    /// <summary>
    /// Читает описание исходных регионов.
    /// </summary>
    /// <param name="path">Путь к файлу описания.</param>
    /// <returns>Описание из файла.</returns>
    /// <exception cref="ArgumentException">Путь пуст.</exception>
    /// <exception cref="AtlasBuildException">Файл отсутствует или не разбирается.</exception>
    public static AtlasSourceDescription ReadSource(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!File.Exists(path))
        {
            throw new AtlasBuildException($"Файл описания не найден: {path}");
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), AtlasJsonContext.Default.AtlasSourceDescription)
                ?? throw new AtlasBuildException($"Файл описания пуст: {path}");
        }
        catch (JsonException exception)
        {
            throw new AtlasBuildException($"Не удалось разобрать описание {path}: {exception.Message}");
        }
    }

    /// <summary>
    /// Строит описание собранного атласа в формате 14.2.
    /// </summary>
    /// <param name="atlas">Собранный атлас.</param>
    /// <returns>Текст описания.</returns>
    /// <exception cref="ArgumentNullException">Атлас отсутствует.</exception>
    public static string WriteDescription(BuiltAtlas atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);

        return JsonSerializer.Serialize(ToDescription(atlas), AtlasJsonContext.Default.AtlasJsonDescription);
    }

    /// <summary>
    /// Переводит собранный атлас в форму описания для записи.
    /// </summary>
    /// <param name="atlas">Собранный атлас.</param>
    /// <returns>Описание с теми же прямоугольниками в пикселях.</returns>
    internal static AtlasJsonDescription ToDescription(BuiltAtlas atlas)
    {
        List<AtlasJsonRegion> regions = new(atlas.Regions.Count);
        foreach (BuiltAtlasRegion region in atlas.Regions)
        {
            regions.Add(new AtlasJsonRegion
            {
                Name = region.Name,
                Rect = ToDescription(region.Rect),
                SizeTexels = ToDescription(region.SizeTexels),
                TexelsPerMeter = region.TexelsPerMeter,
            });
        }

        return new AtlasJsonDescription
        {
            Name = atlas.Name,
            Width = atlas.Width,
            Height = atlas.Height,
            BleedingPixels = atlas.BleedingPixels,
            Regions = regions,
        };
    }

    private static IntRectDescription ToDescription(in IntRect rect)
        => new() { X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height };

    private static IntSizeDescription ToDescription(in IntSize size)
        => new() { X = size.X, Y = size.Y };
}

/// <summary>
/// Контекст сериализации описаний атласа.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AtlasSourceDescription))]
[JsonSerializable(typeof(AtlasJsonDescription))]
public sealed partial class AtlasJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Описание собранного атласа в файле <c>atlas.json</c> (14.2).
/// </summary>
public sealed class AtlasJsonDescription
{
    /// <summary>
    /// Имя атласа.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Ширина атласа в пикселях.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Высота атласа в пикселях.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Полоса между соседними регионами в пикселях.
    /// </summary>
    public int BleedingPixels { get; set; }

    /// <summary>
    /// Регионы атласа с теми же прямоугольниками в пикселях готового атласа.
    /// </summary>
    public List<AtlasJsonRegion> Regions { get; set; } = [];
}

/// <summary>
/// Регион в файле <c>atlas.json</c>.
/// </summary>
public sealed class AtlasJsonRegion
{
    /// <summary>
    /// Имя региона.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Прямоугольник региона в пикселях атласа, границы включительные.
    /// </summary>
    public IntRectDescription Rect { get; set; } = new();

    /// <summary>
    /// Размер исходной текстуры в текселях.
    /// </summary>
    public IntSizeDescription SizeTexels { get; set; } = new();

    /// <summary>
    /// Рекомендуемая плотность региона в текселях на метр.
    /// </summary>
    public float TexelsPerMeter { get; set; }
}