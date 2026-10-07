using System.Text.Json.Serialization;

// CA2227 отключён: это описание файла, и сеттер коллекции обязателен для
// System.Text.Json. Собранный атлас наружу коллекцию отдаёт только для чтения.
#pragma warning disable CA2227

namespace AtlasBuilder;

/// <summary>
/// Описание атласа на входе инструмента: <c>assets/textures/source/world.json</c> (14.2).
/// </summary>
public sealed class AtlasSourceDescription
{
    /// <summary>
    /// Имя атласа. Одновременно имя выходных файлов без расширения.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Ширина атласа в пикселях.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Высота атласа в пикселях.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Полоса между соседними регионами в пикселях. Без неё билинейная
    /// фильтрация подмешивает соседний регион (21.4a).
    /// </summary>
    public int BleedingPixels { get; set; }

    /// <summary>
    /// Регионы атласа: имя, исходная текстура, прямоугольник и плотность.
    /// </summary>
    public List<AtlasRegionDescription> Regions { get; set; } = [];
}

/// <summary>
/// Регион атласа на входе инструмента.
/// </summary>
public sealed class AtlasRegionDescription
{
    /// <summary>
    /// Имя региона: по нему движок ищет регион при загрузке (10.6).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Файл исходной текстуры относительно каталога описания.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Прямоугольник региона в пикселях готового атласа, границы включительные.
    /// </summary>
    public IntRectDescription Rect { get; set; } = new();

    /// <summary>
    /// Размер исходной текстуры в текселях.
    /// </summary>
    public IntSizeDescription SizeTexels { get; set; } = new();

    /// <summary>
    /// Рекомендуемая плотность региона в текселях на метр. Это рекомендация
    /// ассета, а не конфигурация мира (10.5a).
    /// </summary>
    public float TexelsPerMeter { get; set; }
}

/// <summary>
/// Прямоугольник в JSON: координаты и размер в пикселях.
/// </summary>
/// <remarks>
/// Границы включительные, как у <see cref="IntRect"/>.
/// </remarks>
public sealed class IntRectDescription
{
    /// <summary>
    /// Координата левой границы.
    /// </summary>
    [JsonPropertyName("x")]
    public int X { get; set; }

    /// <summary>
    /// Координата верхней границы.
    /// </summary>
    [JsonPropertyName("y")]
    public int Y { get; set; }

    /// <summary>
    /// Число пикселей по горизонтали.
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; set; }

    /// <summary>
    /// Число пикселей по вертикали.
    /// </summary>
    [JsonPropertyName("height")]
    public int Height { get; set; }
}

/// <summary>
/// Размер в целых текселях: используется для исходной текстуры и региона.
/// </summary>
public sealed class IntSizeDescription
{
    /// <summary>
    /// Ширина в текселях.
    /// </summary>
    [JsonPropertyName("x")]
    public int X { get; set; }

    /// <summary>
    /// Высота в текселях.
    /// </summary>
    [JsonPropertyName("y")]
    public int Y { get; set; }
}