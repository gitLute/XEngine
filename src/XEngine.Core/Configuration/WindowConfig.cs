namespace XEngine.Core.Configuration;

/// <summary>
/// Описание создаваемого окна.
/// </summary>
/// <remarks>
/// Конфигурация иммутабельна (14.1): она собирается один раз, а параметры
/// окна после создания меняются только самим окном, поэтому менять их через
/// конфигурацию нельзя. Значения по умолчанию названы константами, а не
/// записаны числами в инициализаторах (инвариант 20).
/// </remarks>
/// <param name="Title">Заголовок окна.</param>
/// <param name="Width">Ширина окна в пикселях.</param>
/// <param name="Height">Высота окна в пикселях.</param>
/// <param name="VSync">Синхронизация с частотой развёртки.</param>
/// <param name="Resizable">Разрешает ли пользователь менять размер окна.</param>
/// <param name="Visible">Показывать ли окно сразу после создания.</param>
public sealed record WindowConfig(
    string Title = WindowConfig.DefaultTitle,
    int Width = WindowConfig.DefaultWidth,
    int Height = WindowConfig.DefaultHeight,
    bool VSync = WindowConfig.DefaultVSync,
    bool Resizable = WindowConfig.DefaultResizable,
    bool Visible = WindowConfig.DefaultVisible)
{
    /// <summary>
    /// Заголовок окна по умолчанию.
    /// </summary>
    public const string DefaultTitle = "XEngine";

    /// <summary>
    /// Ширина окна по умолчанию в пикселях.
    /// </summary>
    public const int DefaultWidth = 1280;

    /// <summary>
    /// Высота окна по умолчанию в пикселях.
    /// </summary>
    public const int DefaultHeight = 720;

    /// <summary>
    /// Синхронизация с развёрткой по умолчанию: включена, потому что без неё
    /// частота кадров не ограничена и шаги симуляции идут неравномерно.
    /// </summary>
    public const bool DefaultVSync = true;

    /// <summary>
    /// Окно изменяемо по умолчанию: уменьшенное окно нужно при отладке и при
    /// работе на ноутбуке.
    /// </summary>
    public const bool DefaultResizable = true;

    /// <summary>
    /// Окно показывается сразу по умолчанию.
    /// </summary>
    public const bool DefaultVisible = true;

    /// <summary>
    /// Проверяет, что параметры пригодны для создания окна.
    /// </summary>
    /// <exception cref="ArgumentException">Заголовок пуст или состоит из пробелов.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Размер окна неположителен.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new ArgumentException("Заголовок окна не может быть пустым.", nameof(Title));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Height);
    }
}