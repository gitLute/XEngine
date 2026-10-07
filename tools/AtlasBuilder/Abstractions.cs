namespace AtlasBuilder;

/// <summary>
/// Читает размер исходной текстуры.
/// </summary>
/// <remarks>
/// Контракт этапа 1 — только размер: инструмент проверяет, что разметка
/// совпадает с файлом. Растеризация пикселей регионов относится к этапу 9,
/// где она проверяется тестами на настоящем атласе; подменять её заглушкой
/// незаметно нельзя, поэтому она идёт отдельным типом.
/// </remarks>
public interface IAtlasSourceReader
{
    /// <summary>
    /// Возвращает размер текстуры в пикселях.
    /// </summary>
    /// <param name="path">Путь к файлу текстуры.</param>
    /// <returns>Размер в пикселях.</returns>
    /// <exception cref="AtlasBuildException">Файл отсутствует или это не PNG.</exception>
    IntSize ReadSize(string path);
}

/// <summary>
/// Растеризатор: заливает холст атласа перед записью в файл.
/// </summary>
public interface IAtlasRasterizer
{
    /// <summary>
    /// Пишет изображение атласа.
    /// </summary>
    /// <param name="path">Путь к файлу изображения.</param>
    /// <param name="width">Ширина холста в пикселях.</param>
    /// <param name="height">Высота холста в пикселях.</param>
    void Write(string path, int width, int height);
}