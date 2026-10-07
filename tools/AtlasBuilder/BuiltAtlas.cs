namespace AtlasBuilder;

/// <summary>
/// Описание собранного атласа: результат проверки и основание для выходных
/// файлов <c>atlas.png</c> и <c>atlas.json</c> (14.2).
/// </summary>
public sealed class BuiltAtlas
{
    /// <summary>
    /// Создаёт описание собранного атласа.
    /// </summary>
    /// <param name="name">Имя атласа.</param>
    /// <param name="width">Ширина атласа в пикселях.</param>
    /// <param name="height">Высота атласа в пикселях.</param>
    /// <param name="bleedingPixels">Полоса между соседними регионами.</param>
    /// <param name="regions">Проверенные регионы.</param>
    /// <exception cref="ArgumentNullException">Список регионов отсутствует.</exception>
    public BuiltAtlas(
        string name,
        int width,
        int height,
        int bleedingPixels,
        IReadOnlyList<BuiltAtlasRegion> regions)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(regions);

        Name = name;
        Width = width;
        Height = height;
        BleedingPixels = bleedingPixels;
        Regions = regions;
    }

    /// <summary>
    /// Имя атласа.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Ширина атласа в пикселях.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Высота атласа в пикселях.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Полоса между соседними регионами в пикселях.
    /// </summary>
    public int BleedingPixels { get; }

    /// <summary>
    /// Регионы атласа в порядке описания.
    /// </summary>
    public IReadOnlyList<BuiltAtlasRegion> Regions { get; }
}

/// <summary>
/// Регион собранного атласа.
/// </summary>
public sealed class BuiltAtlasRegion
{
    /// <summary>
    /// Создаёт регион собранного атласа.
    /// </summary>
    /// <param name="name">Имя региона.</param>
    /// <param name="rect">Прямоугольник региона в пикселях атласа.</param>
    /// <param name="sizeTexels">Размер исходной текстуры в текселях.</param>
    /// <param name="texelsPerMeter">Рекомендуемая плотность региона.</param>
    /// <exception cref="ArgumentNullException">Имя региона отсутствует.</exception>
    public BuiltAtlasRegion(string name, IntRect rect, IntSize sizeTexels, float texelsPerMeter)
    {
        ArgumentNullException.ThrowIfNull(name);

        Name = name;
        Rect = rect;
        SizeTexels = sizeTexels;
        TexelsPerMeter = texelsPerMeter;
    }

    /// <summary>
    /// Имя региона.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Прямоугольник региона в пикселях готового атласа, границы включительные.
    /// </summary>
    public IntRect Rect { get; }

    /// <summary>
    /// Размер исходной текстуры в текселях.
    /// </summary>
    public IntSize SizeTexels { get; }

    /// <summary>
    /// Рекомендуемая плотность региона в текселях на метр.
    /// </summary>
    public float TexelsPerMeter { get; }
}

/// <summary>
/// Размер в целых текселях.
/// </summary>
/// <param name="X">Ширина.</param>
/// <param name="Y">Высота.</param>
public readonly record struct IntSize(int X, int Y);