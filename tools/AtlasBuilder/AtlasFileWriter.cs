namespace AtlasBuilder;

/// <summary>
/// Пишет собранный атлас на диск: изображение через растеризатор и описание
/// в JSON.
/// </summary>
/// <remarks>
/// Имена выходных файлов фиксированы форматом 14.2: <c>atlas.png</c> и
/// <c>atlas.json</c>. Движок читает их по этим именам, поэтому произвольные
/// имена из описания были бы лишним способом разойтись с читателем.
/// </remarks>
public sealed class AtlasFileWriter
{
    /// <summary>
    /// Имя файла изображения атласа.
    /// </summary>
    public const string ImageFileName = "atlas.png";

    /// <summary>
    /// Имя файла описания атласа.
    /// </summary>
    public const string DescriptionFileName = "atlas.json";

    private readonly IAtlasRasterizer _rasterizer;

    /// <summary>
    /// Создаёт писатель.
    /// </summary>
    /// <param name="rasterizer">Растеризатор, заливающий холст атласа.</param>
    /// <exception cref="ArgumentNullException">Растеризатор отсутствует.</exception>
    public AtlasFileWriter(IAtlasRasterizer rasterizer)
    {
        ArgumentNullException.ThrowIfNull(rasterizer);

        _rasterizer = rasterizer;
    }

    /// <summary>
    /// Пишет изображение и описание атласа в каталог.
    /// </summary>
    /// <param name="outputDirectory">Каталог для выходных файлов; создаётся при необходимости.</param>
    /// <param name="atlas">Собранный атлас.</param>
    /// <returns>Пути к записанным файлам: сначала изображение, затем описание.</returns>
    /// <exception cref="ArgumentException">Каталог пуст.</exception>
    /// <exception cref="ArgumentNullException">Атлас отсутствует.</exception>
    public (string ImagePath, string DescriptionPath) Write(string outputDirectory, BuiltAtlas atlas)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputDirectory);
        ArgumentNullException.ThrowIfNull(atlas);

        Directory.CreateDirectory(outputDirectory);

        string imagePath = Path.Combine(outputDirectory, ImageFileName);
        string descriptionPath = Path.Combine(outputDirectory, DescriptionFileName);

        _rasterizer.Write(imagePath, atlas.Width, atlas.Height);
        File.WriteAllText(descriptionPath, AtlasJson.WriteDescription(atlas));

        return (imagePath, descriptionPath);
    }
}