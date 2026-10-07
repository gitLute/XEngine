namespace AtlasBuilder;

/// <summary>
/// Растеризатор этапа 1: заливает холст прозрачным цветом.
/// </summary>
/// <remarks>
/// Этап 1 проверяет геометрию атласа, а не пиксели: инструмент обязан
/// существовать и собирать пустой атлас (критерий приёмки этапа 1), а копирование
/// пикселей регионов проверяется на этапе 9 тестами UV-подстановки. Подмена
/// молча ушла бы незамеченной, поэтому пустая картинка названа своим именем.
/// </remarks>
public sealed class BlankRasterizer : IAtlasRasterizer
{
    /// <summary>
    /// Заливает весь холст прозрачным чёрным.
    /// </summary>
    /// <param name="path">Путь к файлу изображения.</param>
    /// <param name="width">Ширина холста в пикселях.</param>
    /// <param name="height">Высота холста в пикселях.</param>
    public void Write(string path, int width, int height)
        => PngWriter.WriteSolid(path, width, height, r: 0, g: 0, b: 0, a: 0);
}