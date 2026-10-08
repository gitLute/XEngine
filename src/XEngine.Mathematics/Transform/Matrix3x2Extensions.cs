using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Аффинные матрицы 2x3 поверх <see cref="Matrix3x2"/>.
/// Содержит построение матрицы из позиции, угла и масштаба, а также операции,
/// которых нет в типе из BCL.
/// </summary>
public static class Matrix3x2Extensions
{
    /// <summary>
    /// Строит матрицу из позиции, поворота и масштаба.
    /// Порядок преобразования: сначала масштаб, затем поворот, затем перенос.
    /// </summary>
    /// <param name="position">Позиция в мировых координатах.</param>
    /// <param name="rotation">Поворот против часовой стрелки.</param>
    /// <param name="scale">Масштаб по осям.</param>
    /// <param name="pivot">Опорная точка, остающаяся неподвижной при преобразовании.</param>
    /// <returns>Матрица преобразования.</returns>
    /// <remarks>
    /// Матрица собирается прямой формулой, а не произведением четырёх
    /// матриц <c>System.Numerics</c>. Разница не в числе операций, а в
    /// трансцендентных вызовах: <c>Matrix3x2.CreateRotation</c> внутри
    /// обращается к математической библиотеке платформы, поэтому её
    /// использование здесь обходило бы <see cref="Trig"/> и ломало
    /// детерминированный вариант сборки.
    /// <para>
    /// Порядок применения к точке: сдвиг на <c>-pivot</c>, масштаб, поворот,
    /// возврат в <c>pivot + position</c>. Он же совпадает с порядком
    /// произведения матриц, то есть расхождение с тестом на неравномерном
    /// масштабе означало бы ошибку в формуле.
    /// </para>
    /// </remarks>
    public static Matrix3x2 CreateTransform(Vector2 position, Angle rotation, Vector2 scale, Vector2 pivot = default)
    {
        (float sin, float cos) = Trig.SinCos((float)rotation.Radians);

        float originX = pivot.X + position.X;
        float originY = pivot.Y + position.Y;

        // Строки линейной части — образы базовых векторов, поэтому масштаб
        // входит в первую строку по X и во вторую по Y.
        float m11 = cos * scale.X;
        float m12 = sin * scale.X;
        float m21 = -sin * scale.Y;
        float m22 = cos * scale.Y;

        return new Matrix3x2(
            m11,
            m12,
            m21,
            m22,
            originX - ((m11 * pivot.X) + (m21 * pivot.Y)),
            originY - ((m12 * pivot.X) + (m22 * pivot.Y)));
    }

    /// <summary>
    /// Строит матрицу поворота вокруг начала координат.
    /// </summary>
    /// <param name="angle">Угол против часовой стрелки.</param>
    /// <returns>Матрица поворота.</returns>
    /// <remarks>
    /// Собственная реализация вместо <c>Matrix3x2.CreateRotation</c>: та
    /// внутри зовёт математическую библиотеку платформы, то есть обходит
    /// <see cref="Trig"/> и делает результат зависимым от операционной
    /// системы.
    /// </remarks>
    public static Matrix3x2 CreateRotation(Angle angle)
    {
        (float sin, float cos) = Trig.SinCos((float)angle.Radians);
        return new Matrix3x2(cos, sin, -sin, cos, 0f, 0f);
    }

    /// <summary>
    /// Преобразует точку матрицей.
    /// </summary>
    /// <param name="matrix">Матрица преобразования.</param>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Преобразованная точка.</returns>
    public static Vector2 TransformPoint(this Matrix3x2 matrix, Vector2 point)
        => Vector2.Transform(point, matrix);

    /// <summary>
    /// Преобразует направление матрицей, игнорируя перенос.
    /// </summary>
    /// <param name="matrix">Матрица преобразования.</param>
    /// <param name="direction">Исходное направление.</param>
    /// <returns>Преобразованное направление.</returns>
    public static Vector2 TransformDirection(this Matrix3x2 matrix, Vector2 direction)
        => Vector2.TransformNormal(direction, matrix);

    /// <summary>
    /// Возвращает компоненту переноса матрицы.
    /// </summary>
    /// <param name="matrix">Матрица.</param>
    /// <returns>Позиция переноса.</returns>
    public static Vector2 Translation(this Matrix3x2 matrix) => new(matrix.M31, matrix.M32);

    /// <summary>
    /// Возвращает угол поворота матрицы.
    /// </summary>
    /// <param name="matrix">Матрица.</param>
    /// <returns>Угол поворота.</returns>
    public static Angle Rotation(this Matrix3x2 matrix)
        => Angle.FromRadians(Trig.Atan2(matrix.M12, matrix.M11));

    /// <summary>
    /// Возвращает масштаб матрицы по осям.
    /// </summary>
    /// <param name="matrix">Матрица.</param>
    /// <returns>Масштаб по осям.</returns>
    public static Vector2 Scale(this Matrix3x2 matrix)
        => new(
            MathF.Sqrt((matrix.M11 * matrix.M11) + (matrix.M12 * matrix.M12)),
            MathF.Sqrt((matrix.M21 * matrix.M21) + (matrix.M22 * matrix.M22)));

    /// <summary>
    /// Возвращает обратную матрицу, если она существует.
    /// </summary>
    /// <param name="matrix">Исходная матрица.</param>
    /// <param name="inverse">Обратная матрица при успехе.</param>
    /// <returns><c>true</c>, если матрица обратима.</returns>
    /// <remarks>
    /// Вырожденной считается только матрица с нулевым определителем, и решает это
    /// <see cref="Matrix3x2.Invert"/>. Порога по величине определителя здесь нет
    /// намеренно: он был абсолютным и отвергал вполне обратимые матрицы с малым
    /// однородным масштабом, то есть результат зависел от размера мира. Так же и
    /// ведёт себя <see cref="Matrix4x4Extensions.TryInvert"/>, и два аналога в
    /// одной библиотеке обязаны отвечать одинаково.
    /// </remarks>
    public static bool TryInvert(this Matrix3x2 matrix, out Matrix3x2 inverse)
    {
        if (!Matrix3x2.Invert(matrix, out inverse))
        {
            inverse = Matrix3x2.Identity;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Преобразует матрицу 2D в матрицу 3D, сохраняя XY и добавляя единицу по Z.
    /// </summary>
    /// <param name="matrix">Матрица 2D.</param>
    /// <returns>Матрица 3D.</returns>
    public static Matrix4x4 ToMatrix4x4(this Matrix3x2 matrix)
        => new(
            matrix.M11, matrix.M12, 0f, 0f,
            matrix.M21, matrix.M22, 0f, 0f,
            0f, 0f, 1f, 0f,
            matrix.M31, matrix.M32, 0f, 1f);
}
