namespace AtlasBuilder;

/// <summary>
/// Проверяет описание атласа и строит его итоговое описание.
/// </summary>
/// <remarks>
/// Проверки из 21.4a обязательны: наложение прямоугольников и выход за границы
/// дают артефакты, которые видны только в кадре, и обнаруживаются позже всего.
/// Класс ничего не пишет на диск: проверка отделена от записи, поэтому её можно
/// вызывать на этапе сборки и в тестах без файловых побочных эффектов.
/// </remarks>
public sealed class AtlasBuilder
{
    /// <summary>
    /// Полоса между соседними регионами: два пикселя, иначе билинейная
    /// фильтрация подмешивает соседа (21.4a).
    /// </summary>
    public const int DefaultBleedingPixels = 2;

    private readonly IAtlasSourceReader _sourceReader;

    /// <summary>
    /// Создаёт сборщик.
    /// </summary>
    /// <param name="sourceReader">Читатель размера исходной текстуры.</param>
    /// <exception cref="ArgumentNullException">Читатель отсутствует.</exception>
    public AtlasBuilder(IAtlasSourceReader sourceReader)
    {
        ArgumentNullException.ThrowIfNull(sourceReader);

        _sourceReader = sourceReader;
    }

    /// <summary>
    /// Проверяет описание и строит итоговое описание атласа.
    /// </summary>
    /// <param name="description">Описание исходных регионов.</param>
    /// <param name="sourceDirectory">Каталог, относительно которого ищутся исходные текстуры.</param>
    /// <returns>Описание собранного атласа.</returns>
    /// <exception cref="ArgumentNullException">Описание отсутствует.</exception>
    /// <exception cref="ArgumentException">Каталог исходников пуст.</exception>
    /// <exception cref="AtlasBuildException">Описание некорректно.</exception>
    public BuiltAtlas Build(AtlasSourceDescription description, string sourceDirectory)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentException.ThrowIfNullOrEmpty(sourceDirectory);

        ValidateAtlas(description);
        IntRect atlasRect = new(0, 0, description.Width, description.Height);

        List<AtlasRegionPlacement> placements = new(description.Regions.Count);
        HashSet<string> names = new(StringComparer.Ordinal);

        foreach (AtlasRegionDescription region in description.Regions)
        {
            placements.Add(ValidateRegion(region, atlasRect, names, sourceDirectory));
        }

        ValidateBleeding(description.BleedingPixels, placements);

        List<BuiltAtlasRegion> regions = new(placements.Count);
        foreach (AtlasRegionPlacement placement in placements)
        {
            regions.Add(new BuiltAtlasRegion(
                placement.Name,
                placement.Rect,
                placement.SizeTexels,
                placement.TexelsPerMeter));
        }

        return new BuiltAtlas(
            description.Name,
            description.Width,
            description.Height,
            description.BleedingPixels,
            regions);
    }

    private static void ValidateAtlas(AtlasSourceDescription description)
    {
        if (string.IsNullOrWhiteSpace(description.Name))
        {
            throw new AtlasBuildException("Не задано имя атласа.");
        }

        if (description.Width <= 0 || description.Height <= 0)
        {
            throw new AtlasBuildException(
                $"Некорректный размер атласа: {description.Width}x{description.Height}. " +
                "Оба размера должны быть положительными.");
        }

        if (description.BleedingPixels < 1)
        {
            throw new AtlasBuildException(
                $"Полоса между регионами не задана: {description.BleedingPixels}. " +
                $"Требуется не меньше {DefaultBleedingPixels}.");
        }

        if (description.Regions.Count == 0)
        {
            throw new AtlasBuildException("Описание не содержит ни одного региона.");
        }
    }

    private AtlasRegionPlacement ValidateRegion(
        AtlasRegionDescription region,
        in IntRect atlasRect,
        HashSet<string> names,
        string sourceDirectory)
    {
        if (string.IsNullOrWhiteSpace(region.Name))
        {
            throw new AtlasBuildException($"Не задано имя региона: {region.Source}");
        }

        if (!names.Add(region.Name))
        {
            throw new AtlasBuildException("Имя региона повторяется.", region.Name);
        }

        if (string.IsNullOrWhiteSpace(region.Source))
        {
            throw new AtlasBuildException("Не задан файл исходной текстуры.", region.Name);
        }

        if (region.Rect.Width <= 0 || region.Rect.Height <= 0)
        {
            throw new AtlasBuildException(
                $"Некорректный размер региона: {region.Rect.Width}x{region.Rect.Height}.",
                region.Name);
        }

        if (region.Rect.Width != region.SizeTexels.X || region.Rect.Height != region.SizeTexels.Y)
        {
            throw new AtlasBuildException(
                $"Размер региона {region.Rect.Width}x{region.Rect.Height} не совпадает " +
                $"с размером исходной текстуры {region.SizeTexels.X}x{region.SizeTexels.Y}.",
                region.Name);
        }

        if (region.TexelsPerMeter <= 0f)
        {
            throw new AtlasBuildException(
                $"Рекомендуемая плотность должна быть положительной: {region.TexelsPerMeter}.",
                region.Name);
        }

        IntRect rect = new(region.Rect.X, region.Rect.Y, region.Rect.Width, region.Rect.Height);
        if (!rect.IsInside(atlasRect))
        {
            throw new AtlasBuildException(
                $"Прямоугольник {rect} выходит за границы атласа {atlasRect}.",
                region.Name);
        }

        IntSize sizeTexels = new(region.SizeTexels.X, region.SizeTexels.Y);
        IntSize actualSize = _sourceReader.ReadSize(Path.Combine(sourceDirectory, region.Source));
        if (actualSize != sizeTexels)
        {
            throw new AtlasBuildException(
                $"Размер файла {region.Source} равен {actualSize.X}x{actualSize.Y}, " +
                $"а в описании указано {sizeTexels.X}x{sizeTexels.Y}.",
                region.Name);
        }

        return new AtlasRegionPlacement(region.Name, rect, sizeTexels, region.TexelsPerMeter);
    }

    /// <summary>
    /// Проверяет, что регионы не накладываются и между соседями остаётся
    /// полоса. Сравнение идёт по всем парам: два региона могут разойтись только
    /// по одной оси, и попарная проверка это ловит, а сравнение соседей по
    /// порядку описания — нет.
    /// </summary>
    /// <param name="bleedingPixels">Требуемая полоса между регионами.</param>
    /// <param name="placements">Размещённые регионы.</param>
    /// <exception cref="AtlasBuildException">Регионы накладываются или стоят слишком близко.</exception>
    private static void ValidateBleeding(int bleedingPixels, IReadOnlyList<AtlasRegionPlacement> placements)
    {
        for (int first = 0; first < placements.Count; first++)
        {
            for (int second = first + 1; second < placements.Count; second++)
            {
                AtlasRegionPlacement left = placements[first];
                AtlasRegionPlacement right = placements[second];

                if (left.Rect.Intersects(right.Rect))
                {
                    throw new AtlasBuildException(
                        $"Прямоугольник {left.Rect} накладывается на прямоугольник {right.Rect} региона '{right.Name}'.",
                        left.Name);
                }

                int gap = left.Rect.GapTo(right.Rect);
                if (gap < bleedingPixels)
                {
                    throw new AtlasBuildException(
                        $"Между {left.Rect} и {right.Rect} региона '{right.Name}' остаётся {gap} пикселей, " +
                        $"а полоса между регионами равна {bleedingPixels}.",
                        left.Name);
                }
            }
        }
    }

    /// <summary>
    /// Проверенный регион: всё, что нужно и для описания, и для проверки
    /// пересечений.
    /// </summary>
    /// <param name="Name">Имя региона.</param>
    /// <param name="Rect">Прямоугольник в пикселях атласа.</param>
    /// <param name="SizeTexels">Размер исходной текстуры.</param>
    /// <param name="TexelsPerMeter">Рекомендуемая плотность.</param>
    private readonly record struct AtlasRegionPlacement(
        string Name,
        IntRect Rect,
        IntSize SizeTexels,
        float TexelsPerMeter);
}