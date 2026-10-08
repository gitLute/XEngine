using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Упаковка матриц для передачи в GPU и преобразования между 2D и 3D.
/// Расположение элементов соответствует column-major, то есть подходит для
/// <c>glUniformMatrix4fv</c> с параметром <c>transpose = false</c>.
/// </summary>
public static class MatrixExtensions
{
    /// <summary>
    /// Преобразует матрицу 4x4 в массив из 16 элементов в порядке column-major.
    /// </summary>
    /// <param name="matrix">Исходная матрица.</param>
    /// <returns>Массив для uniform-параметра OpenGL.</returns>
    public static float[] ToColumnMajorArray(this Matrix4x4 matrix) =>
    [
        matrix.M11, matrix.M21, matrix.M31, matrix.M41,
        matrix.M12, matrix.M22, matrix.M32, matrix.M42,
        matrix.M13, matrix.M23, matrix.M33, matrix.M43,
        matrix.M14, matrix.M24, matrix.M34, matrix.M44,
    ];

    /// <summary>
    /// Строит ортографическую проекцию 2D с нулевой точкой в центре вида.
    /// Границы: по X от -width/2 до +width/2, по Y от -height/2 до +height/2.
    /// Диапазон глубины [-1; 1], как у всех проекций движка (6.5a).
    /// </summary>
    /// <param name="width">Ширина видимой области.</param>
    /// <param name="height">Высота видимой области.</param>
    /// <param name="nearPlane">
    /// Ближняя плоскость отсечения, расстояние вдоль −Z. По умолчанию километр
    /// за камерой.
    /// </param>
    /// <param name="farPlane">
    /// Дальняя плоскость отсечения, расстояние вдоль −Z. По умолчанию километр
    /// перед камерой.
    /// </param>
    /// <returns>Матрица проекции.</returns>
    /// <remarks>
    /// Собственная реализация, а не <see cref="Matrix4x4.CreateOrthographic"/>:
    /// вариант BCL даёт диапазон глубины [0; 1], а clip space OpenGL требует
    /// [-1; 1]. Взяв BCL-вариант по незнанию, теряется половина точности
    /// глубины и проекция перестаёт совпадать с остальными проекциями движка.
    /// <para>
    /// <paramref name="nearPlane"/> и <paramref name="farPlane"/> трактуются как
    /// расстояния вдоль −Z, а не как координаты Z: <c>z = −near → −1</c>,
    /// <c>z = −far → +1</c>. Это тот же неочевидный контракт, что и в
    /// перспективной проекции, и он зафиксирован в разделе 4 документа.
    /// </para>
    /// <para>
    /// Умолчания были <c>−1</c> и <c>1</c>, то есть в кадр попадал ровно метр
    /// глубины перед камерой, а вторая половина диапазона NDC уходила на область
    /// за ней. Для 2.5D это самая вероятная проекция в движке, и молчаливое
    /// отсечение всего, что дальше метра, не имеет видимой причины. Километр в
    /// каждую сторону — это порядок размера игрового мира; вызывающему, которому
    /// нужен свой диапазон, остаётся задать его явно, как и раньше.
    /// </para>
    /// </remarks>
    public static Matrix4x4 CreateOrthographic2D(float width, float height, float nearPlane = -1000f, float farPlane = 1000f)
        => Matrix4x4Extensions.CreateOrthographic(width, height, nearPlane, farPlane);
}
