using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Операции над кватернионами поверх того, что уже есть в
/// <see cref="System.Numerics"/>.
/// </summary>
/// <remarks>
/// Кватернион в библиотеке всегда единичный и задаёт поворот без переноса.
/// Порядок осей в <see cref="FromEuler"/> зафиксирован: сначала поворот вокруг Z,
/// затем вокруг X, затем вокруг Y. Порядок фиксируется один раз (4.5) и
/// проверяется тестом на композиции.
/// </remarks>
public static class QuaternionExtensions
{
    /// <summary>
    /// Строит поворот вокруг оси на заданный угол. Ось нормализуется внутри.
    /// </summary>
    /// <param name="axis">Ось вращения, ненулевая.</param>
    /// <param name="angle">Угол против часовой стрелки при взгляде со стороны положительной полуоси.</param>
    /// <returns>Поворот.</returns>
    /// <exception cref="ArgumentException">Ось нулевая.</exception>
    public static Quaternion FromAxisAngle(Vector3 axis, Angle angle)
    {
        Vector3 unitAxis = axis.SafeNormalize();
        if (unitAxis == Vector3.Zero)
        {
            throw new ArgumentException("Ось вращения должна быть ненулевой.", nameof(axis));
        }

        return Quaternion.CreateFromAxisAngle(unitAxis, (float)angle.Radians);
    }

    /// <summary>
    /// Строит поворот из углов Эйлера в порядке Z, X, Y: сначала поворот
    /// вокруг оси Z (крен), затем вокруг X (тангаж), затем вокруг Y (рыскание).
    /// </summary>
    /// <param name="yaw">Поворот вокруг вертикальной оси Y.</param>
    /// <param name="pitch">Поворот вокруг оси X.</param>
    /// <param name="roll">Поворот вокруг оси Z.</param>
    /// <returns>Композиция поворотов в зафиксированном порядке.</returns>
    public static Quaternion FromEuler(Angle yaw, Angle pitch, Angle roll)
        => FromAxisAngle(Vector3.UnitY, yaw)
            * FromAxisAngle(Vector3.UnitX, pitch)
            * FromAxisAngle(Vector3.UnitZ, roll);

    /// <summary>
    /// Разбирает поворот в углы Эйлера в том же порядке, что и
    /// <see cref="FromEuler"/>: рыскание вокруг Y, тангаж вокруг X, крен вокруг Z.
    /// </summary>
    /// <param name="rotation">Исходный поворот.</param>
    /// <returns>Рыскание, тангаж и крен.</returns>
    /// <remarks>
    /// Формулы берутся из элементов матрицы поворота, а не из элементов
    /// кватерниона: разбор в порядке Z, X, Y через элементы кватерниона
    /// несимметричен и легко переносится с ошибкой знака. Учтено, что матрица
    /// поворота хранится в соглашении вектор-строка, поэтому композиция
    /// поворотов читается в обратном порядке индексов. В вырожденном случае
    /// тангажа в ±90° рыскание и крен вращаются вокруг одной оси и неразделимы:
    /// рыскание принимается равным нулю, крен при этом определяется однозначно.
    /// </remarks>
    public static (Angle Yaw, Angle Pitch, Angle Roll) ToEuler(in Quaternion rotation)
    {
        Matrix4x4 matrix = Matrix4x4.CreateFromQuaternion(Normalize(rotation));

        float sinPitch = Scalar.Clamp(-matrix.M32, -1f, 1f);
        float pitch = MathF.Asin(sinPitch);

        float yaw;
        float roll;
        if (MathF.Abs(sinPitch) < GimbalLockThreshold)
        {
            yaw = MathF.Atan2(matrix.M31, matrix.M33);
            roll = MathF.Atan2(matrix.M12, matrix.M22);
        }
        else
        {
            yaw = 0f;
            roll = sinPitch * MathF.Atan2(matrix.M13, matrix.M11);
        }

        return (Angle.FromRadians(yaw), Angle.FromRadians(pitch), Angle.FromRadians(roll));
    }

    /// <summary>
    /// Поворачивает точку кватернионом. Перенос не применяется: кватернион
    /// задаёт только поворот.
    /// </summary>
    /// <param name="rotation">Поворот, предполагается единичным.</param>
    /// <param name="point">Поворачиваемая точка.</param>
    /// <returns>Повёрнутая точка.</returns>
    /// <remarks>
    /// Тонкая обёртка над <see cref="Vector3.Transform(Vector3, Quaternion)"/>:
    /// повторять вычисление незачем, но имя закрепляет контракт «поворот без
    /// переноса» рядом с <c>Transform</c> матрицей, где перенос есть (6.5,
    /// правило 6).
    /// </remarks>
    public static Vector3 Rotate(this in Quaternion rotation, Vector3 point)
        => Vector3.Transform(point, rotation);

    /// <summary>
    /// Строит поворот, в котором локальная ось Z смотрит вдоль
    /// <paramref name="forward"/>, а локальная ось Y — в сторону
    /// <paramref name="up"/>.
    /// </summary>
    /// <param name="forward">Направление взгляда, ненулевое и не параллельное вертикали.</param>
    /// <param name="up">Направление вверх, ненулевое и не параллельное направлению взгляда.</param>
    /// <returns>Поворот.</returns>
    /// <exception cref="ArgumentException">Направление нулевое или совпадает с вертикалью.</exception>
    /// <remarks>
    /// Здесь ось Z направлена <em>вперёд</em>, по направлению взгляда, а у
    /// матрицы вида ось Z направлена <em>назад</em> (см. remarks
    /// <see cref="Matrix4x4Extensions.CreateLookAt"/>). Это разные конвенции, и
    /// обратным к матрице вида является не этот поворот, а разворот на 180°
    /// вокруг Y: подставлять <c>LookRotation(forward, up)</c> вместо ориентации
    /// из матрицы вида разворачивает объект спиной к камере.
    /// </remarks>
    public static Quaternion LookRotation(Vector3 forward, Vector3 up)
    {
        Vector3 unitForward = forward.SafeNormalize();
        Vector3 unitUp = up.SafeNormalize();
        if (unitForward == Vector3.Zero)
        {
            throw new ArgumentException("Направление взгляда должно быть ненулевым.", nameof(forward));
        }

        if (unitUp == Vector3.Zero)
        {
            throw new ArgumentException("Направление вверх должно быть ненулевым.", nameof(up));
        }

        if (MathF.Abs(Vector3.Dot(unitForward, unitUp)) >= 1f - Scalar.Epsilon)
        {
            throw new ArgumentException(
                "Направление взгляда не должно совпадать с направлением вверх: ориентация не определена.",
                nameof(forward));
        }

        Vector3 right = Vector3.Normalize(Vector3.Cross(unitUp, unitForward));
        Vector3 trueUp = Vector3.Cross(unitForward, right);
        return Quaternion.CreateFromRotationMatrix(new Matrix4x4(
            right.X,
            right.Y,
            right.Z,
            0f,
            trueUp.X,
            trueUp.Y,
            trueUp.Z,
            0f,
            unitForward.X,
            unitForward.Y,
            unitForward.Z,
            0f,
            0f,
            0f,
            0f,
            1f));
    }

    /// <summary>
    /// Интерполирует поворот по кратчайшей дуге с ограничением параметра
    /// диапазоном [0; 1].
    /// </summary>
    /// <param name="from">Начальный поворот.</param>
    /// <param name="to">Конечный поворот.</param>
    /// <param name="t">Параметр интерполяции, ограничивается диапазоном [0; 1].</param>
    /// <returns>Поворот на заданном параметре.</returns>
    /// <remarks>
    /// Отличается от <see cref="Quaternion.Slerp"/> ограничением параметра и
    /// нормализацией результата: без них вызов за пределами [0; 1] возвращает
    /// ненормализованный кватернион, который дальше молча искажает масштаб.
    /// </remarks>
    public static Quaternion Slerp(Quaternion from, Quaternion to, float t)
    {
        float clamped = Interpolation.Clamp01(t);
        Quaternion result = Quaternion.Slerp(Normalize(from), Normalize(to), clamped);
        return Normalize(result);
    }

    /// <summary>
    /// Возвращает угол наименьшего поворота между двумя ориентациями.
    /// </summary>
    /// <param name="from">Начальная ориентация.</param>
    /// <param name="to">Конечная ориентация.</param>
    /// <returns>Угол в диапазоне [0; π].</returns>
    /// <remarks>
    /// Знак кватерниона не учитывается: повороты <c>q</c> и <c>-q</c>
    /// задают одну ориентацию.
    /// </remarks>
    public static Angle AngleBetween(Quaternion from, Quaternion to)
    {
        // Нормализовать нужно оба операнда: скалярное произведение
        // неоднородно, и при |to| != 1 угол между ориентациями получается
        // завышенным (AngleBetween(identity, 0.5 * поворот) давал 138.6°).
        //
        // Угол считается через относительный поворот to ⊗ conj(from): у
        // единичного кватерниона угол поворота равен 2 * atan2(|w|, ‖векторная
        // часть|), то есть половина угла есть atan2(‖v‖, |w|), поскольку
        // tan(θ/2) = ‖v‖ / |w|.
        //
        // Формула через acos от скалярного произведения не годится: у малого
        // угла скалярное произведение отличается от единицы на величину порядка
        // θ², во float это различие меньше эпсилон, и угол возвращался ровно
        // нулём для всех углов меньше примерно 0.01°. atan2 сохраняет
        // относительную точность, потому что зависит от θ линейно.
        Quaternion relative = Quaternion.Multiply(Normalize(to), Conjugate(Normalize(from)));

        float vectorPartSquared =
            (relative.X * relative.X) + (relative.Y * relative.Y) + (relative.Z * relative.Z);
        float halfAngle = MathF.Atan2(MathF.Sqrt(vectorPartSquared), MathF.Abs(relative.W));

        return Angle.FromRadians(2.0 * halfAngle);
    }

    /// <summary>
    /// Возвращает сопряжённый кватернион: векторная часть меняет знак.
    /// </summary>
    /// <param name="rotation">Исходный кватернион.</param>
    /// <returns>Сопряжённый кватернион.</returns>
    public static Quaternion Conjugate(in Quaternion rotation)
        => new(-rotation.X, -rotation.Y, -rotation.Z, rotation.W);

    /// <summary>
    /// Возвращает обратный кватернион, отменяющий исходный.
    /// </summary>
    /// <param name="rotation">Исходный кватернион, ненулевой.</param>
    /// <returns>Обратный кватернион.</returns>
    /// <remarks>
    /// Для единичного кватерниона совпадает с <see cref="Conjugate"/>;
    /// расходится для произвольного множителя, который делит кватернион
    /// на квадрат длины.
    /// </remarks>
    public static Quaternion Inverse(this in Quaternion rotation)
    {
        float lengthSquared = rotation.LengthSquared();
        if (lengthSquared <= Scalar.Epsilon * Scalar.Epsilon)
        {
            throw new ArgumentException("Обратный кватернион не определён для нулевого значения.", nameof(rotation));
        }

        return new Quaternion(
            -rotation.X / lengthSquared,
            -rotation.Y / lengthSquared,
            -rotation.Z / lengthSquared,
            rotation.W / lengthSquared);
    }

    /// <summary>
    /// Порог, ниже которого разбор Эйлера считает невырожденным.
    /// </summary>
    private const float GimbalLockThreshold = 1f - 1e-6f;

    private static Quaternion Normalize(in Quaternion rotation)
    {
        float lengthSquared = rotation.LengthSquared();
        return lengthSquared <= Scalar.Epsilon * Scalar.Epsilon ? Quaternion.Identity : rotation * (1f / MathF.Sqrt(lengthSquared));
    }
}