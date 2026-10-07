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
    /// </summary>
    /// <param name="width">Ширина видимой области.</param>
    /// <param name="height">Высота видимой области.</param>
    /// <param name="nearPlane">Ближняя плоскость отсечения.</param>
    /// <param name="farPlane">Дальняя плоскость отсечения.</param>
    /// <returns>Матрица проекции.</returns>
    public static Matrix4x4 CreateOrthographic2D(float width, float height, float nearPlane = -1f, float farPlane = 1f)
        => Matrix4x4.CreateOrthographic(width, height, nearPlane, farPlane);
}
