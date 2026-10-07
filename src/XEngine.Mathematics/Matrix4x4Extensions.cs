using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Построение и разбор матриц 4x4 поверх того, что уже есть в
/// <see cref="System.Numerics"/>.
/// </summary>
/// <remarks>
/// Соглашение пространства (4.5): правосторонняя система, Y вверх, метры,
/// вектор-строка — точка преобразуется как <c>v * M</c>. Матрица поворота
/// хранит в строках образы базовых векторов, поэтому произведение матриц
/// применяется справа налево.
/// <para>
/// Формулы проекций реализованы здесь, а не взяты из BCL: матрицы
/// <c>Matrix4x4.CreatePerspective*</c> и <c>CreateOrthographic</c> дают
/// диапазон глубины [0; 1], тогда как clip space OpenGL требует [-1; 1]
/// (6.5a). Диапазон проверяется тестом на известных значениях, а не согласован
/// с BCL.
/// </para>
/// </remarks>
public static class Matrix4x4Extensions
{
    /// <summary>
    /// Строит матрицу преобразования из масштаба, поворота и переноса.
    /// </summary>
    /// <param name="position">Перенос в метрах.</param>
    /// <param name="rotation">Поворот.</param>
    /// <param name="scale">Масштаб по осям.</param>
    /// <returns>Матрица преобразования.</returns>
    /// <remarks>
    /// Порядок применения к точке: сначала масштаб, затем поворот, затем
    /// перенос. Он же проверяется тестом на неравномерном масштабе, где
    /// перестановка даёт другой результат.
    /// </remarks>
    public static Matrix4x4 CreateTRS(Vector3 position, in Quaternion rotation, Vector3 scale)
        => Matrix4x4.CreateScale(scale)
            * Matrix4x4.CreateFromQuaternion(rotation)
            * Matrix4x4.CreateTranslation(position);

    /// <summary>
    /// Строит матрицу вида: камера смотрит вдоль <c>target - eye</c>, её
    /// локальная ось X направлена вправо относительно <paramref name="up"/>,
    /// а локальная ось Z — назад, поэтому точки перед камерой имеют
    /// отрицательную координату Z.
    /// </summary>
    /// <param name="eye">Положение камеры.</param>
    /// <param name="target">Точка взгляда.</param>
    /// <param name="up">Направление вверх.</param>
    /// <returns>Матрица вида.</returns>
    /// <exception cref="ArgumentException">
    /// Точка взгляда совпадает с положением камеры или направление вверх
    /// параллельно направлению взгляда: в обоих случаях ориентация не
    /// определена, а матрица вышла бы вырожденной молча.
    /// </exception>
    /// <remarks>
    /// Правый вектор камеры равен <c>cross(forward, up)</c>, а не
    /// <c>cross(up, forward)</c>: камера, смотрящая вдоль <c>+Z</c> при
    /// вертикали <c>+Y</c>, имеет правый вектор <c>-X</c>, поэтому ось X мира
    /// попадает в вид слева. Локальная ось Z вида направлена назад, и обратной
    /// к этой матрице является не <c>QuaternionExtensions.LookRotation</c>, а
    /// поворот с осью Z, направленной назад: функции решают разные задачи.
    /// </remarks>
    public static Matrix4x4 CreateLookAt(Vector3 eye, Vector3 target, Vector3 up)
    {
        Vector3 direction = target - eye;
        Vector3 unitForward = direction.SafeNormalize();
        if (unitForward == Vector3.Zero)
        {
            throw new ArgumentException("Точка взгляда не должна совпадать с положением камеры.", nameof(target));
        }

        Vector3 unitUp = up.SafeNormalize();
        if (unitUp == Vector3.Zero)
        {
            throw new ArgumentException("Направление вверх должно быть ненулевым.", nameof(up));
        }

        Vector3 crossUpForward = Vector3.Cross(unitForward, unitUp);
        if (crossUpForward.LengthSquared() <= Scalar.Epsilon * Scalar.Epsilon)
        {
            throw new ArgumentException(
                "Направление вверх не должно быть параллельно направлению взгляда.",
                nameof(up));
        }

        Vector3 right = Vector3.Normalize(crossUpForward);
        Vector3 trueUp = Vector3.Cross(right, unitForward);
        Vector3 backward = -unitForward;

        Matrix4x4 result = Matrix4x4.Identity;
        result.M11 = right.X;
        result.M12 = trueUp.X;
        result.M13 = backward.X;
        result.M21 = right.Y;
        result.M22 = trueUp.Y;
        result.M23 = backward.Y;
        result.M31 = right.Z;
        result.M32 = trueUp.Z;
        result.M33 = backward.Z;
        result.M41 = -Vector3.Dot(right, eye);
        result.M42 = -Vector3.Dot(trueUp, eye);
        result.M43 = -Vector3.Dot(backward, eye);
        return result;
    }

    /// <summary>
    /// Строит общую матрицу вида и проекции.
    /// </summary>
    /// <param name="view">Матрица вида.</param>
    /// <param name="projection">Матрица проекции.</param>
    /// <returns>Матрица, переводящая мировые координаты в отсечённые.</returns>
    /// <remarks>
    /// Порядок умножения здесь обратный привычному, и это не опечатка:
    /// <see cref="Matrix4x4"/> хранит матрицу транспонированной к соглашению
    /// вектор-строка, поэтому произведение <c>view * projection</c> применяет
    /// сначала вид, затем проекцию. Запись <c>projection * view</c> молча даёт
    /// неверный результат: <c>w</c> выходит отрицательным для точек перед
    /// камерой, и всё отсекается. Именно поэтому произведение собирается здесь
    /// одной функцией, а не по месту использования.
    /// </remarks>
    public static Matrix4x4 CreateViewProjection(in Matrix4x4 view, in Matrix4x4 projection) => view * projection;

    /// <summary>
    /// Строит перспективную проекцию с диапазоном глубины [-1; 1].
    /// </summary>
    /// <param name="fieldOfView">Угол обзора по вертикали.</param>
    /// <param name="aspectRatio">Отношение ширины к высоте вида.</param>
    /// <param name="nearPlane">Ближняя плоскость в метрах.</param>
    /// <param name="farPlane">Дальняя плоскость в метрах.</param>
    /// <returns>Матрица проекции.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Угол обзора вне (0; 180)°, отношение сторон неположительно,
    /// ближняя плоскость неположительна или дальняя не дальше ближней.
    /// </exception>
    public static Matrix4x4 CreatePerspective(Angle fieldOfView, float aspectRatio, float nearPlane, float farPlane)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(nearPlane, 0f);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(farPlane, nearPlane);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(aspectRatio, 0f);

        double radians = fieldOfView.Radians;
        if (radians <= 0.0 || radians >= Math.PI)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fieldOfView),
                fieldOfView.Degrees,
                "Угол обзора должен лежать строго между 0 и 180 градусами.");
        }

        float yScale = 1.0f / MathF.Tan((float)radians * 0.5f);
        float range = farPlane - nearPlane;

        Matrix4x4 result = Matrix4x4.Identity;
        result.M11 = yScale / aspectRatio;
        result.M22 = yScale;
        result.M33 = -(farPlane + nearPlane) / range;
        result.M34 = -1f;
        result.M43 = -(2f * farPlane * nearPlane) / range;
        result.M44 = 0f;
        return result;
    }

    /// <summary>
    /// Строит ортографическую проекцию с диапазоном глубины [-1; 1].
    /// </summary>
    /// <param name="width">Ширина видимой области.</param>
    /// <param name="height">Высота видимой области.</param>
    /// <param name="nearPlane">Ближняя плоскость в метрах.</param>
    /// <param name="farPlane">Дальняя плоскость в метрах.</param>
    /// <returns>Матрица проекции.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Размер неположителен или дальняя плоскость не дальше ближней.
    /// </exception>
    public static Matrix4x4 CreateOrthographic(float width, float height, float nearPlane, float farPlane)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(farPlane, nearPlane);

        float range = farPlane - nearPlane;

        Matrix4x4 result = Matrix4x4.Identity;
        result.M11 = 2f / width;
        result.M22 = 2f / height;
        result.M33 = -2f / range;
        result.M43 = -(farPlane + nearPlane) / range;
        result.M44 = 1f;
        return result;
    }

    /// <summary>
    /// Строит ортографическую проекцию по высоте видимой области: ширина
    /// вычисляется как <paramref name="height"/> * <paramref name="aspectRatio"/>.
    /// </summary>
    /// <param name="height">Высота видимой области в метрах.</param>
    /// <param name="aspectRatio">Отношение ширины к высоте вида.</param>
    /// <param name="nearPlane">Ближняя плоскость в метрах.</param>
    /// <param name="farPlane">Дальняя плоскость в метрах.</param>
    /// <returns>Матрица проекции.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Высота неположительна, отношение сторон неположительно или дальняя
    /// плоскость не дальше ближней.
    /// </exception>
    /// <remarks>
    /// Высота, а не ширина, задаёт ортогональную проекцию камеры (10.10):
    /// ширина выводится из отношения сторон, поэтому при смене разрешения окна
    /// видимая ширина меняется вместе с ним, а не скачет.
    /// </remarks>
    public static Matrix4x4 CreateOrthographicByHeight(float height, float aspectRatio, float nearPlane, float farPlane)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(aspectRatio);

        return CreateOrthographic(height * aspectRatio, height, nearPlane, farPlane);
    }

    /// <summary>
    /// Пытается обратить матрицу. Ведёт себя как
    /// <see cref="Matrix4x4.Invert(Matrix4x4, out Matrix4x4)"/> и существует
    /// для единого имени в наборе Try-операций библиотеки.
    /// </summary>
    /// <param name="matrix">Исходная матрица.</param>
    /// <param name="result">Обратная матрица, если обращение удалось.</param>
    /// <returns><c>true</c>, если матрица обратима.</returns>
    public static bool TryInvert(in Matrix4x4 matrix, out Matrix4x4 result)
        => Matrix4x4.Invert(matrix, out result);

    /// <summary>
    /// Возвращает перенос из матрицы преобразования.
    /// </summary>
    /// <param name="matrix">Исходная матрица.</param>
    /// <returns>Компонент переноса в метрах.</returns>
    public static Vector3 GetTranslation(this in Matrix4x4 matrix) => matrix.Translation;

    /// <summary>
    /// Возвращает масштаб по осям как длины базовых векторов матрицы.
    /// </summary>
    /// <param name="matrix">Исходная матрица.</param>
    /// <returns>Масштаб по осям.</returns>
    public static Vector3 GetScale(this in Matrix4x4 matrix)
        => new(
            MathF.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12 + matrix.M13 * matrix.M13),
            MathF.Sqrt(matrix.M21 * matrix.M21 + matrix.M22 * matrix.M22 + matrix.M23 * matrix.M23),
            MathF.Sqrt(matrix.M31 * matrix.M31 + matrix.M32 * matrix.M32 + matrix.M33 * matrix.M33));

    /// <summary>
    /// Возвращает поворот из матрицы, предполагая отсутствие неравномерного
    /// масштаба.
    /// </summary>
    /// <param name="matrix">Исходная матрица.</param>
    /// <returns>Поворот.</returns>
    /// <remarks>
    /// При наличии масштаба поворот определяется приближённо: длины строк
    /// нормализуются, а остаточное искажение отбрасывается. Для случая, где
    /// масштаб есть и важен, разбирать матрицу должен владелец преобразования,
    /// а не математическая функция по умолчанию.
    /// </remarks>
    public static Quaternion GetRotation(this in Matrix4x4 matrix)
    {
        Vector3 scale = matrix.GetScale();
        float safeX = scale.X <= Scalar.Epsilon ? 1f : scale.X;
        float safeY = scale.Y <= Scalar.Epsilon ? 1f : scale.Y;
        float safeZ = scale.Z <= Scalar.Epsilon ? 1f : scale.Z;

        // Базис занимает столбцы матрицы, поэтому масштаб оси делит столбец,
        // а не строку.
        Matrix4x4 rotation = new(
            matrix.M11 / safeX,
            matrix.M12 / safeY,
            matrix.M13 / safeZ,
            matrix.M14,
            matrix.M21 / safeX,
            matrix.M22 / safeY,
            matrix.M23 / safeZ,
            matrix.M24,
            matrix.M31 / safeX,
            matrix.M32 / safeY,
            matrix.M33 / safeZ,
            matrix.M34,
            matrix.M41,
            matrix.M42,
            matrix.M43,
            matrix.M44);

        return Quaternion.CreateFromRotationMatrix(rotation);
    }

    /// <summary>
    /// Преобразует точку с учётом переноса.
    /// </summary>
    /// <param name="matrix">Матрица преобразования.</param>
    /// <param name="point">Преобразуемая точка.</param>
    /// <returns>Преобразённая точка.</returns>
    public static Vector3 MultiplyPoint(this in Matrix4x4 matrix, Vector3 point)
        => Vector3.Transform(point, matrix);

    /// <summary>
    /// Преобразует направление или вектор без переноса.
    /// </summary>
    /// <param name="matrix">Матрица преобразования.</param>
    /// <param name="vector">Преобразуемый вектор.</param>
    /// <returns>Преобразённый вектор.</returns>
    public static Vector3 MultiplyVector(this in Matrix4x4 matrix, Vector3 vector)
        => Vector3.TransformNormal(vector, matrix);

    /// <summary>
    /// Преобразует нормаль обратной транспонировкой: при неравномерном
    /// масштабе нормаль перестаёт быть перпендикулярной поверхности, если
    /// преобразовывать её как направление.
    /// </summary>
    /// <param name="matrix">Матрица преобразования.</param>
    /// <param name="normal">Преобразуемая нормаль.</param>
    /// <returns>Преобразённая нормаль.</returns>
    /// <remarks>
    /// В соглашении вектор-строка точки и направления преобразуются как
    /// <c>v * M</c>, а нормаль считается обратной транспонировкой:
    /// <c>n' = n * M * (Mᵀ M)⁻¹</c>, что сводится к <c>n' = n * M⁻ᵀ</c>.
    /// Готовая функция <c>Vector3.TransformNormal</c> перемножает строками, а
    /// не столбцами, поэтому здесь строка обратной матрицы скалярно
    /// умножается на нормаль вручную.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Матрица вырождена и необратима.</exception>
    public static Vector3 TransformNormal(this in Matrix4x4 matrix, Vector3 normal)
    {
        if (!Matrix4x4.Invert(matrix, out Matrix4x4 inverse))
        {
            throw new InvalidOperationException(
                "Матрица вырождена, нормаль преобразовать нельзя: обратной матрицы не существует.");
        }

        return new Vector3(
            inverse.M11 * normal.X + inverse.M12 * normal.Y + inverse.M13 * normal.Z,
            inverse.M21 * normal.X + inverse.M22 * normal.Y + inverse.M23 * normal.Z,
            inverse.M31 * normal.X + inverse.M32 * normal.Y + inverse.M33 * normal.Z);
    }
}