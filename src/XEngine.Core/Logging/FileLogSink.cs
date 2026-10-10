using System.Text;

namespace XEngine.Core.Logging;

/// <summary>
/// Приёмник сообщений, дописывающий их в файл.
/// </summary>
/// <remarks>
/// В отличие от консоли файл переживает перезапуск: движок часто падает, и
/// журнал последнего запуска нужен больше всего. Поэтому файл открывается
/// сразу, а не лениво, и каждая строка уходит на диск без буфера — иначе
/// последние записи терялись бы именно в том запуске, ради которого журнал
/// и нужен.
/// <para>
/// Запись сериализуется блокировкой, потому что журнал вызывается с потока
/// симуляции и с потока рендера (11.10).
/// </para>
/// <para>
/// Файл открыт на чтение и удаление извне: журнал можно смотреть во время игры
/// и отдавать сборщику дампов, не останавливая движок.
/// </para>
/// </remarks>
public sealed class FileLogSink : ILogSink, IDisposable
{
    /// <summary>
    /// Кодировка журнала: UTF-8 без метки порядка байтов. Метка ломает разбор
    /// журнала сторонними инструментами, поэтому задаётся явно. Поле хранит
    /// только неизменяемые параметры кодировки и состояния движка не содержит
    /// (инвариант 8).
    /// </summary>
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private const int BufferSize = 1024;

    private readonly LogLevel _minimumLevel;
    private readonly LogFormatter _formatter;
    private readonly long _maxFileSizeBytes;
    private readonly int _retainedFiles;
    private readonly string _path;
    private readonly int _newLineByteCount;
    private readonly Lock _writeLock = new();

    private StreamWriter? _writer;
    private bool _disposed;

    /// <summary>
    /// Создаёт приёмник и открывает файл на дозапись.
    /// </summary>
    /// <param name="path">
    /// Путь к файлу журнала. Недостающие каталоги создаются, существующий файл
    /// дописывается, а не затирается: несколько запусков подряд остаются в
    /// одном файле.
    /// </param>
    /// <param name="minimumLevel">
    /// Минимальный уровень, попадающий в файл. По умолчанию
    /// <see cref="LogLevel.Information"/>.
    /// </param>
    /// <param name="maxFileSizeBytes">
    /// Предел размера файла в байтах. Ноль означает «без предела». Файл
    /// сворачивается перед той строкой, которая в предел не влезет, поэтому
    /// свежий журнал всегда содержит хотя бы одну запись.
    /// </param>
    /// <param name="retainedFiles">
    /// Сколько прошлых файлов хранить после сворачивания: <c>.1</c>, <c>.2</c>
    /// и так далее рядом с журналом. Ноль вместе с ненулевым
    /// <paramref name="maxFileSizeBytes"/> означает, что прошлый файл
    /// удаляется без сохранения.
    /// </param>
    /// <param name="formatter">
    /// Формат строки. По умолчанию <see cref="LogFormatter"/> с настройками по
    /// умолчанию.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Путь пустой либо предел размера или число хранимых файлов отрицательны.
    /// </exception>
    /// <exception cref="IOException">Файл не удалось открыть.</exception>
    /// <exception cref="UnauthorizedAccessException">Нет прав на файл.</exception>
    public FileLogSink(
        string path,
        LogLevel minimumLevel = LogLevel.Information,
        long maxFileSizeBytes = 0,
        int retainedFiles = 0,
        LogFormatter? formatter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegative(maxFileSizeBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(retainedFiles);

        _path = path;
        _minimumLevel = minimumLevel;
        _maxFileSizeBytes = maxFileSizeBytes;
        _retainedFiles = retainedFiles;
        _formatter = formatter ?? new LogFormatter();
        _newLineByteCount = Utf8NoBom.GetByteCount(Environment.NewLine);

        OpenWriter();
    }

    /// <summary>
    /// Путь к текущему файлу журнала.
    /// </summary>
    public string FilePath => _path;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel level) => level >= _minimumLevel;

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Приёмник уже освобождён.</exception>
    public void Write(LogLevel level, string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!IsEnabled(level))
        {
            return;
        }

        string line = _formatter.Format(level, message);

        lock (_writeLock)
        {
            StreamWriter writer = Writer;
            long required = RequiredBytes(line);

            // Сворачивание идёт до записи, а не после: иначе при маленьком
            // пределе файл успевает переполниться первой же строкой, уходит в
            // сторонний и остаётся пустым, то есть свежий журнал ничего не
            // содержит.
            if (_maxFileSizeBytes > 0
                && writer.BaseStream.Length > 0
                && writer.BaseStream.Length + required > _maxFileSizeBytes)
            {
                Roll();
                writer = Writer;
            }

            writer.WriteLine(line);
        }
    }

    /// <summary>
    /// Дописывает накопленное на диск.
    /// </summary>
    /// <remarks>
    /// Каждая строка и так уходит на диск сразу, поэтому метод нужен только
    /// для проверки состояния после сбоя записи. После освобождения приёмника
    /// ничего не делает.
    /// </remarks>
    public void Flush()
    {
        lock (_writeLock)
        {
            _writer?.Flush();
        }
    }

    /// <summary>
    /// Закрывает файл. Повторный вызов и вызов после освобождения безопасны.
    /// </summary>
    public void Dispose()
    {
        lock (_writeLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CloseWriter();
        }
    }

    private StreamWriter Writer =>
        _writer ?? throw new ObjectDisposedException(nameof(FileLogSink));

    private void OpenWriter()
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(_path));

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Stream stream = new FileStream(
            _path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete,
            BufferSize,
            FileOptions.None);

        _writer = new StreamWriter(stream, Utf8NoBom, BufferSize)
        {
            AutoFlush = true,
        };
    }

    private void CloseWriter()
    {
        StreamWriter? writer = _writer;
        _writer = null;

        if (writer is null)
        {
            return;
        }

        try
        {
            writer.Flush();
            writer.Dispose();
        }
        catch (IOException)
        {
            // Приёмник уже освобождается: сбой при закрытии ничего изменить уже
            // не может, а исключение здесь затерело бы результат освобождения.
        }
    }

    /// <summary>
    /// Сворачивает переполненный файл: уводит его в сторонний, сдвигает
    /// прежние и продолжает писать в новый.
    /// </summary>
    private void Roll()
    {
        CloseWriter();

        if (_retainedFiles > 0)
        {
            File.Delete(RolledPath(_retainedFiles));

            for (int index = _retainedFiles - 1; index >= 1; index--)
            {
                string from = RolledPath(index);

                if (File.Exists(from))
                {
                    File.Move(from, RolledPath(index + 1), overwrite: true);
                }
            }

            if (File.Exists(_path))
            {
                File.Move(_path, RolledPath(1), overwrite: true);
            }
        }
        else
        {
            File.Delete(_path);
        }

        OpenWriter();
    }

    private string RolledPath(int index) => $"{_path}.{index}";

    /// <summary>
    /// Размер строки в байтах вместе с переводом строки: столько место в
    /// файле она займёт.
    /// </summary>
    private long RequiredBytes(string line) =>
        Utf8NoBom.GetByteCount(line) + _newLineByteCount;
}