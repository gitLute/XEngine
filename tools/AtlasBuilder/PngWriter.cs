using System.Buffers.Binary;
using System.IO.Compression;

namespace AtlasBuilder;

/// <summary>
/// Пишет PNG без внешних зависимостей: сигнатура, блок IHDR, сжатые
/// скан-строки и блок IEND.
/// </summary>
/// <remarks>
/// Инструмент сборки ассетов запускается на этапе сборки, поэтому лишняя
/// зависимость в нём — это ещё одна причина сборка падает. Формат пишется
/// ровно один: 8 бит на канал, RGBA, фильтр 0 в каждой строке. Такой файл
/// читается любым загрузчиком текстур, включая загрузчик движка.
/// </remarks>
public static class PngWriter
{
    /// <summary>Байт на пиксель: четыре канала RGBA.</summary>
    public const int BytesPerPixel = 4;

    private const int ColorTypeRgba = 6;
    private const int BitDepth = 8;

    private static ReadOnlySpan<byte> PngSignature => [137, 80, 78, 71, 13, 10, 26, 10];
    private static ReadOnlySpan<byte> IhdrChunkType => "IHDR"u8;
    private static ReadOnlySpan<byte> IdatChunkType => "IDAT"u8;
    private static ReadOnlySpan<byte> IendChunkType => "IEND"u8;

    private static readonly uint[] CrcTable = CreateCrcTable();

    /// <summary>
    /// Пишет изображение, залитое сплошным цветом.
    /// </summary>
    /// <param name="path">Путь к файлу изображения.</param>
    /// <param name="width">Ширина в пикселях.</param>
    /// <param name="height">Высота в пикселях.</param>
    /// <param name="r">Красный канал заливки.</param>
    /// <param name="g">Зелёный канал заливки.</param>
    /// <param name="b">Синий канал заливки.</param>
    /// <param name="a">Прозрачность заливки.</param>
    /// <exception cref="ArgumentException">Путь пуст.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Размер неположителен.</exception>
    public static void WriteSolid(string path, int width, int height, byte r, byte g, byte b, byte a)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        byte[] pixels = new byte[width * height * BytesPerPixel];
        for (int index = 0; index < pixels.Length; index += BytesPerPixel)
        {
            pixels[index] = r;
            pixels[index + 1] = g;
            pixels[index + 2] = b;
            pixels[index + 3] = a;
        }

        Write(path, width, height, pixels);
    }

    /// <summary>
    /// Пишет изображение из строки пикселей RGBA.
    /// </summary>
    /// <param name="path">Путь к файлу изображения.</param>
    /// <param name="width">Ширина в пикселях.</param>
    /// <param name="height">Высота в пикселях.</param>
    /// <param name="pixels">Пиксели: <paramref name="width"/> на <paramref name="height"/> по четыре байта.</param>
    /// <exception cref="ArgumentException">Путь пуст.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Размер неположителен.</exception>
    /// <exception cref="ArgumentException">Размер массива пикселей не совпадает с размером изображения.</exception>
    public static void Write(string path, int width, int height, ReadOnlySpan<byte> pixels)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        int expected = width * height * BytesPerPixel;
        if (pixels.Length != expected)
        {
            throw new ArgumentException(
                $"Ожидалось {expected} байт пикселей для {width}x{height}, получено {pixels.Length}.",
                nameof(pixels));
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream stream = File.Create(path);
        stream.Write(PngSignature);

        byte[] header = CreateHeader(width, height);
        WriteChunk(stream, IhdrChunkType, header);
        WriteChunk(stream, IdatChunkType, CompressScanlines(width, height, pixels));
        WriteChunk(stream, IendChunkType, ReadOnlySpan<byte>.Empty);
    }

    private static byte[] CreateHeader(int width, int height)
    {
        byte[] header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = BitDepth;
        header[9] = ColorTypeRgba;
        return header;
    }

    /// <summary>
    /// Собирает и сжимает скан-строки: перед каждой строкой пикселей идёт
    /// байт фильтра, здесь всегда нулевой.
    /// </summary>
    /// <param name="width">Ширина в пикселях.</param>
    /// <param name="height">Высота в пикселях.</param>
    /// <param name="pixels">Пиксели RGBA.</param>
    /// <returns>Сжатые данные блока IDAT.</returns>
    private static byte[] CompressScanlines(int width, int height, ReadOnlySpan<byte> pixels)
    {
        int stride = width * BytesPerPixel;
        byte[] raw = new byte[height * (stride + 1)];
        for (int row = 0; row < height; row++)
        {
            int target = row * (stride + 1);
            raw[target] = 0;
            pixels.Slice(row * stride, stride).CopyTo(raw.AsSpan(target + 1, stride));
        }

        using MemoryStream buffer = new();
        using (ZLibStream compressor = new(buffer, CompressionLevel.Optimal, leaveOpen: true))
        {
            compressor.Write(raw);
        }

        return buffer.ToArray();
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> chunkType, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        stream.Write(chunkType);
        stream.Write(data);

        byte[] crcInput = new byte[chunkType.Length + data.Length];
        chunkType.CopyTo(crcInput);
        data.CopyTo(crcInput.AsSpan(chunkType.Length));

        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, ComputeCrc(crcInput));
        stream.Write(crc);
    }

    private static uint ComputeCrc(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] CreateCrcTable()
    {
        uint[] table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            uint value = index;
            for (int bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }
}