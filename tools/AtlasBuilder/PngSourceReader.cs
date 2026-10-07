namespace AtlasBuilder;

/// <summary>
/// Читает размер PNG из заголовка файла.
/// </summary>
/// <remarks>
/// Читаются только сигнатура и блок IHDR: это 33 байта независимо от размера
/// изображения. Полное декодирование PNG на этапе 1 не требуется, а тащить
/// внешнюю библиотеку ради инструмента, который на этапе 1 только проверяет
/// разметку, не нужно.
/// </remarks>
public sealed class PngSourceReader : IAtlasSourceReader
{
    /// <summary>Сигнатура PNG: первые восемь байт файла.</summary>
    private static ReadOnlySpan<byte> PngSignature => [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>Длина блока IHDR вместе с полем длины: 8 байт заголовка плюс 13 байт данных.</summary>
    private const int HeaderLength = 33;

    /// <summary>Смещение ширины внутри заголовка.</summary>
    private const int WidthOffset = 16;

    /// <summary>Смещение высоты внутри заголовка.</summary>
    private const int HeightOffset = 20;

    /// <inheritdoc/>
    /// <exception cref="AtlasBuildException">Файл отсутствует или это не PNG.</exception>
    public IntSize ReadSize(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!File.Exists(path))
        {
            throw new AtlasBuildException($"Файл исходной текстуры не найден: {path}");
        }

        byte[] header = ReadHeader(path);

        if (!header.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
        {
            throw new AtlasBuildException($"Файл не является PNG: {path}");
        }

        int width = ReadBigEndianInt32(header, WidthOffset);
        int height = ReadBigEndianInt32(header, HeightOffset);
        if (width <= 0 || height <= 0)
        {
            throw new AtlasBuildException($"Некорректный размер изображения в файле: {path}");
        }

        return new IntSize(width, height);
    }

    private static byte[] ReadHeader(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            byte[] buffer = new byte[HeaderLength];
            int read = stream.ReadAtLeast(buffer, HeaderLength, throwOnEndOfStream: false);
            if (read < HeaderLength)
            {
                throw new AtlasBuildException($"Файл слишком короткий, чтобы быть PNG: {path}");
            }

            return buffer;
        }
        catch (IOException exception)
        {
            throw new AtlasBuildException($"Не удалось прочитать файл {path}: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new AtlasBuildException($"Нет доступа к файлу {path}: {exception.Message}");
        }
    }

    private static int ReadBigEndianInt32(byte[] buffer, int offset)
        => (buffer[offset] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3];
}