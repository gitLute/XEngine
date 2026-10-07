using Xunit;

namespace AtlasBuilder.Tests;

/// <summary>
/// Тесты чтения и записи PNG. Инструмент не тянет внешнюю библиотеку, поэтому
/// формат проверяется по структуре файла: сигнатура, блоки и размер, который
/// потом читает исходная текстура.
/// </summary>
public sealed class PngFileTests
{
    private static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];

    [Fact]
    public void PngSourceReader_ReturnsSizeOfWrittenImage()
    {
        using TestDirectory directory = new(nameof(PngSourceReader_ReturnsSizeOfWrittenImage));
        string path = directory.GetPath("texture.png");

        PngWriter.WriteSolid(path, 13, 7, 1, 2, 3, 255);

        Assert.Equal(new IntSize(13, 7), new PngSourceReader().ReadSize(path));
    }

    [Fact]
    public void PngWriter_WritesSignatureAndRequiredChunks()
    {
        using TestDirectory directory = new(nameof(PngWriter_WritesSignatureAndRequiredChunks));
        string path = directory.GetPath("texture.png");

        PngWriter.WriteSolid(path, 4, 4, 0, 0, 0, 0);

        byte[] bytes = File.ReadAllBytes(path);
        Assert.True(bytes.AsSpan(0, Signature.Length).SequenceEqual(Signature), "Нет сигнатуры PNG.");
        Assert.True(ContainsChunk(bytes, "IHDR"), "Нет блока IHDR.");
        Assert.True(ContainsChunk(bytes, "IDAT"), "Нет блока IDAT.");
        Assert.True(ContainsChunk(bytes, "IEND"), "Нет блока IEND.");
    }

    [Fact]
    public void PngWriter_RejectsPixelBufferOfWrongLength()
    {
        using TestDirectory directory = new(nameof(PngWriter_RejectsPixelBufferOfWrongLength));
        string path = directory.GetPath("texture.png");

        Assert.Throws<ArgumentException>(() => PngWriter.Write(path, 4, 4, new byte[10]));
    }

    [Fact]
    public void PngWriter_RejectsNonPositiveSize()
    {
        using TestDirectory directory = new(nameof(PngWriter_RejectsNonPositiveSize));
        string path = directory.GetPath("texture.png");

        Assert.Throws<ArgumentOutOfRangeException>(() => PngWriter.WriteSolid(path, 0, 4, 0, 0, 0, 0));
    }

    [Fact]
    public void PngSourceReader_RejectsFileThatIsNotPng()
    {
        using TestDirectory directory = new(nameof(PngSourceReader_RejectsFileThatIsNotPng));
        string path = directory.GetPath("texture.png");
        File.WriteAllText(path, "это не изображение");

        AtlasBuildException exception = Assert.Throws<AtlasBuildException>(() => new PngSourceReader().ReadSize(path));

        Assert.Contains("не является PNG", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PngSourceReader_RejectsMissingFile()
    {
        using TestDirectory directory = new(nameof(PngSourceReader_RejectsMissingFile));
        string path = directory.GetPath("absent.png");

        AtlasBuildException exception = Assert.Throws<AtlasBuildException>(() => new PngSourceReader().ReadSize(path));

        Assert.Contains("не найден", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PngSourceReader_RejectsTruncatedFile()
    {
        using TestDirectory directory = new(nameof(PngSourceReader_RejectsTruncatedFile));
        string path = directory.GetPath("texture.png");
        File.WriteAllBytes(path, Signature.ToArray());

        AtlasBuildException exception = Assert.Throws<AtlasBuildException>(() => new PngSourceReader().ReadSize(path));

        Assert.Contains("слишком короткий", exception.Message, StringComparison.Ordinal);
    }

    private static bool ContainsChunk(ReadOnlySpan<byte> file, string chunkType)
        => file.IndexOf(System.Text.Encoding.ASCII.GetBytes(chunkType)) >= 0;
}