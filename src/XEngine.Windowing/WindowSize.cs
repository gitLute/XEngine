namespace XEngine.Windowing;

/// <summary>
/// Размер окна в пикселях: ширина и высота.
/// </summary>
/// <remarks>
/// Собственный тип вместо <c>Vector2I</c> из Silk.NET и вместо пары целых.
/// Тип бэкенда в контракте означал бы, что подменить окно в тесте нельзя, а
/// пара целых не выражает смысл: перепутать местами ширину и высоту при
/// использовании компилятор не поймает.
/// <para>
/// В .NET нет встроенного вектора целых из двух компонент, поэтому тип
/// заведён здесь.
/// </para>
/// </remarks>
/// <param name="Width">Ширина в пикселях.</param>
/// <param name="Height">Высота в пикселях.</param>
public readonly record struct WindowSize(int Width, int Height)
{
    /// <summary>
    /// Размер, равный нулю по обеим осям. Признак недоступного фреймбуфера:
    /// окно свёрнуто или ещё не создано.
    /// </summary>
    public static WindowSize Zero => default;

    /// <summary>
    /// Отношение сторон: ширина, делённая на высоту.
    /// </summary>
    /// <remarks>
    /// Нулевого размера не бывает: при нулевой высоте отношение сторон не имеет
    /// смысла, и вычисление его дало бы бесконечность, которая потом утечёт в
    /// проекцию.
    /// </remarks>
    public float AspectRatio => Height == 0 ? 0f : (float)Width / Height;

    /// <summary>
    /// Проверяет, что размер пригоден для создания окна или проекции.
    /// </summary>
    /// <returns><c>true</c>, если обе стороны положительны.</returns>
    public bool IsValid => Width > 0 && Height > 0;

    /// <inheritdoc/>
    public override string ToString() => $"{Width}x{Height}";
}