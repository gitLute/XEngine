using Xunit;

namespace AtlasBuilder.Tests;

/// <summary>
/// Контрактные тесты сборщика атласа. Основной сценарий — две тестовые
/// текстуры превращаются в пустой атлас и описание; остальные проверки
/// фиксируют отказы, которые иначе видны только в кадре.
/// </summary>
public sealed class AtlasBuilderTests
{
    private const int TextureSize = 8;
    private const int AtlasSize = 32;

    [Fact]
    public void Build_AcceptsTwoRegions()
    {
        using TestDirectory directory = new(nameof(Build_AcceptsTwoRegions));
        string source = CreateSourcesWithTwoTextures(directory);
        string output = directory.CreateSubdirectory("out");

        AtlasSourceDescription description = AtlasJson.ReadSource(Path.Combine(source, "world.json"));
        BuiltAtlas atlas = new AtlasBuilder(new PngSourceReader()).Build(description, source);
        (string imagePath, string descriptionPath) = new AtlasFileWriter(new BlankRasterizer()).Write(output, atlas);

        Assert.Equal(AtlasSize, atlas.Width);
        Assert.Equal(AtlasSize, atlas.Height);
        Assert.Equal(2, atlas.Regions.Count);

        BuiltAtlasRegion first = atlas.Regions[0];
        Assert.Equal("stone", first.Name);
        Assert.Equal(new IntRect(0, 0, 8, 8), first.Rect);
        Assert.Equal(new IntSize(8, 8), first.SizeTexels);
        Assert.Equal(16f, first.TexelsPerMeter);

        BuiltAtlasRegion second = atlas.Regions[1];
        Assert.Equal("wood", second.Name);
        Assert.Equal(new IntRect(10, 0, 8, 8), second.Rect);

        Assert.True(File.Exists(imagePath));
        Assert.True(File.Exists(descriptionPath));
        Assert.Equal(new IntSize(AtlasSize, AtlasSize), new PngSourceReader().ReadSize(imagePath));
    }

    [Fact]
    public void Write_ProducesDescriptionWithSamePixelRects()
    {
        using TestDirectory directory = new(nameof(Write_ProducesDescriptionWithSamePixelRects));
        string source = CreateSourcesWithTwoTextures(directory);
        string output = directory.CreateSubdirectory("out");

        AtlasSourceDescription description = AtlasJson.ReadSource(Path.Combine(source, "world.json"));
        BuiltAtlas atlas = new AtlasBuilder(new PngSourceReader()).Build(description, source);
        (_, string descriptionPath) = new AtlasFileWriter(new BlankRasterizer()).Write(output, atlas);

        AtlasJsonDescription written = ReadDescription(descriptionPath);

        Assert.Equal("world", written.Name);
        Assert.Equal(AtlasSize, written.Width);
        Assert.Equal(AtlasSize, written.Height);
        Assert.Equal(AtlasBuilder.DefaultBleedingPixels, written.BleedingPixels);
        Assert.Equal(2, written.Regions.Count);
        Assert.Equal(0, written.Regions[0].Rect.X);
        Assert.Equal(8, written.Regions[0].Rect.Width);
        Assert.Equal(10, written.Regions[1].Rect.X);
        Assert.Equal(8, written.Regions[1].SizeTexels.X);
        Assert.Equal(16f, written.Regions[0].TexelsPerMeter);
    }

    [Fact]
    public void Build_RejectsRegionOutsideAtlas()
    {
        using TestDirectory directory = new(nameof(Build_RejectsRegionOutsideAtlas));
        string source = CreateSourcesWithTwoTextures(directory);
        AtlasSourceDescription description = AtlasJson.ReadSource(Path.Combine(source, "world.json"));
        description.Regions[0].Rect.X = AtlasSize - 4;

        AtlasBuildException exception = Assert.Throws<AtlasBuildException>(
            () => new AtlasBuilder(new PngSourceReader()).Build(description, source));

        Assert.Contains("выходит за границы атласа", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RejectsOverlappingRegions()
    {
        using TestDirectory directory = new(nameof(Build_RejectsOverlappingRegions));
        string source = CreateSourcesWithTwoTextures(directory);
        AtlasSourceDescription description = AtlasJson.ReadSource(Path.Combine(source, "world.json"));
        description.Regions[1].Rect.X = 4;

        AtlasBuildException exception = Assert.Throws<AtlasBuildException>(
            () => new AtlasBuilder(new PngSourceReader()).Build(description, source));

        Assert.Contains("накладывается", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RejectsRegionsCloserThanBleedingStrip()
    {
        using TestDirectory directory = new(nameof(Build_RejectsRegionsCloserThanBleedingStrip));
        string source = CreateSourcesWithTwoTextures(directory);
        AtlasSourceDescription description = AtlasJson.ReadSource(Path.Combine(source, "world.json"));
        description.Regions[1].Rect.X = 9;

        AtlasBuildException exception = Assert.Throws<AtlasBuildException>(
            () => new AtlasBuilder(new PngSourceReader()).Build(description, source));

        Assert.Contains("полоса между регионами", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_AcceptsRegionsSeparatedByBleedingStrip()
    {
        using TestDirectory directory = new(nameof(Build_AcceptsRegionsSeparatedByBleedingStrip));
        string source = CreateSourcesWithTwoTextures(directory);
        AtlasSourceDescription description = AtlasJson.ReadSource(Path.Combine(source, "world.json"));
        description.Regions[1].Rect.X = 10;

        BuiltAtlas atlas = new AtlasBuilder(new PngSourceReader()).Build(description, source);

        Assert.Equal(2, atlas.Regions.Count);
    }

    [Fact]
    public void Build_RejectsDuplicateRegionName()
    {
        using TestDirectory directory = new(nameof(Build_RejectsDuplicateRegionName));
        string source = CreateSourcesWithTwoTextures(directory);
        AtlasSourceDescription description = AtlasJson.ReadSource(Path.Combine(source, "world.json"));
        description.Regions[1].Name = description.Regions[0].Name;

        AtlasBuildException exception = Assert.Throws<AtlasBuildException>(
            () => new AtlasBuilder(new PngSourceReader()).Build(description, source));

        Assert.Contains("Имя региона повторяется", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RejectsMissingSourceTexture()
    {
        using TestDirectory directory = new(nameof(Build_RejectsMissingSourceTexture));
        string source = CreateSourcesWithTwoTextures(directory);
        AtlasSourceDescription description = AtlasJson.ReadSource(Path.Combine(source, "world.json"));
        description.Regions[1].Source = "missing.png";

        AtlasBuildException exception = Assert.Throws<AtlasBuildException>(
            () => new AtlasBuilder(new PngSourceReader()).Build(description, source));

        Assert.Contains("не найден", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RejectsSizeThatDiffersFromSourceTexture()
    {
        using TestDirectory directory = new(nameof(Build_RejectsSizeThatDiffersFromSourceTexture));
        string source = CreateSourcesWithTwoTextures(directory);
        AtlasSourceDescription description = AtlasJson.ReadSource(Path.Combine(source, "world.json"));
        description.Regions[1].Rect.Width = TextureSize / 2;
        description.Regions[1].Rect.Height = TextureSize / 2;
        description.Regions[1].SizeTexels.X = TextureSize / 2;
        description.Regions[1].SizeTexels.Y = TextureSize / 2;

        AtlasBuildException exception = Assert.Throws<AtlasBuildException>(
            () => new AtlasBuilder(new PngSourceReader()).Build(description, source));

        Assert.Contains("Размер файла", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RejectsRegionRectDifferentFromSizeTexels()
    {
        using TestDirectory directory = new(nameof(Build_RejectsRegionRectDifferentFromSizeTexels));
        string source = CreateSourcesWithTwoTextures(directory);
        AtlasSourceDescription description = AtlasJson.ReadSource(Path.Combine(source, "world.json"));
        description.Regions[0].SizeTexels.X = TextureSize + 1;

        AtlasBuildException exception = Assert.Throws<AtlasBuildException>(
            () => new AtlasBuilder(new PngSourceReader()).Build(description, source));

        Assert.Contains("не совпадает", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 32, 2)]
    [InlineData(32, 0, 2)]
    [InlineData(32, 32, 0)]
    public void Build_RejectsInvalidAtlasSize(int width, int height, int bleeding)
    {
        using TestDirectory directory = new(nameof(Build_RejectsInvalidAtlasSize));
        string source = CreateSourcesWithTwoTextures(directory);
        AtlasSourceDescription description = AtlasJson.ReadSource(Path.Combine(source, "world.json"));
        description.Width = width;
        description.Height = height;
        description.BleedingPixels = bleeding;

        Assert.Throws<AtlasBuildException>(
            () => new AtlasBuilder(new PngSourceReader()).Build(description, source));
    }

    [Fact]
    public void Build_RejectsAtlasWithoutRegions()
    {
        using TestDirectory directory = new(nameof(Build_RejectsAtlasWithoutRegions));
        string source = CreateSourcesWithTwoTextures(directory);
        AtlasSourceDescription description = AtlasJson.ReadSource(Path.Combine(source, "world.json"));
        description.Regions.Clear();

        AtlasBuildException exception = Assert.Throws<AtlasBuildException>(
            () => new AtlasBuilder(new PngSourceReader()).Build(description, source));

        Assert.Contains("ни одного региона", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RejectsNonPositiveTexelDensity()
    {
        using TestDirectory directory = new(nameof(Build_RejectsNonPositiveTexelDensity));
        string source = CreateSourcesWithTwoTextures(directory);
        AtlasSourceDescription description = AtlasJson.ReadSource(Path.Combine(source, "world.json"));
        description.Regions[0].TexelsPerMeter = 0f;

        AtlasBuildException exception = Assert.Throws<AtlasBuildException>(
            () => new AtlasBuilder(new PngSourceReader()).Build(description, source));

        Assert.Contains("плотность", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Создаёт каталог исходников с двумя тестовыми текстурами и описанием.
    /// Второй регион отодвинут на два пикселя от первого: ровно на ширину полосы.
    /// </summary>
    /// <param name="directory">Временный каталог теста.</param>
    /// <returns>Путь к каталогу исходников.</returns>
    private static string CreateSourcesWithTwoTextures(TestDirectory directory)
    {
        string source = directory.CreateSubdirectory("source");

        PngWriter.WriteSolid(Path.Combine(source, "stone.png"), TextureSize, TextureSize, 128, 128, 128, 255);
        PngWriter.WriteSolid(Path.Combine(source, "wood.png"), TextureSize, TextureSize, 96, 64, 32, 255);

        File.WriteAllText(Path.Combine(source, "world.json"), $$"""
            {
              "name": "world",
              "width": {{AtlasSize}},
              "height": {{AtlasSize}},
              "bleedingPixels": {{AtlasBuilder.DefaultBleedingPixels}},
              "regions": [
                {
                  "name": "stone",
                  "source": "stone.png",
                  "rect": { "x": 0, "y": 0, "width": {{TextureSize}}, "height": {{TextureSize}} },
                  "sizeTexels": { "x": {{TextureSize}}, "y": {{TextureSize}} },
                  "texelsPerMeter": 16
                },
                {
                  "name": "wood",
                  "source": "wood.png",
                  "rect": { "x": {{TextureSize + AtlasBuilder.DefaultBleedingPixels}}, "y": 0, "width": {{TextureSize}}, "height": {{TextureSize}} },
                  "sizeTexels": { "x": {{TextureSize}}, "y": {{TextureSize}} },
                  "texelsPerMeter": 16
                }
              ]
            }

            """);

        return source;
    }

    private static AtlasJsonDescription ReadDescription(string path) => AtlasJson.ReadBuilt(path);
}