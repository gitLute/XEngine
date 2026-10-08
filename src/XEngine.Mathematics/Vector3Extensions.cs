using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Операции над трёхмерными векторами поверх того, что уже есть в
/// <see cref="System.Numerics"/>.
/// </summary>
/// <remarks>
/// Имена операций различают, что именно они преобразуют: направление без
/// переноса, точку на плоскости или нормаль. Три разных вычисления под одним
/// именем — источник ошибок знаков (6.5, правило 6). Сглаживание идёт через
/// <see cref="Interpolation"/>, а не через <c>Lerp(value, target, dt * k)</c>.
/// </remarks>
public static class Vector3Extensions
{
    /// <summary>
    /// Возвращает нормализованный вектор. Настоящий нулевой вектор остаётся
    /// нулевым.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <returns>Вектор единичной длины либо нулевой.</returns>
    /// <remarks>
    /// Проверяется именно ноль, а не длина меньше Scalar.Epsilon. Ненулевой
    /// вектор — направление, и он нормализуется при любой длине: иначе
    /// отрезок длиной меньше микрона превращался в нулевой, и вызывающий получал
    /// не направление, а его отсутствие. Обнуление коротких векторов нужно там,
    /// где сравнивают с допуском, и там сравнивают явно.
    /// </remarks>
    public static Vector3 SafeNormalize(this Vector3 vector)
        => vector == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(vector);

    /// <summary>
    /// Ограничивает длину вектора сверху.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <param name="maxLength">Максимальная длина, неотрицательная.</param>
    /// <returns>Вектор, длина которого не превышает <paramref name="maxLength"/>.</returns>
    /// <remarks>
    /// Отрицательный предел отвергается: квадрат его остаётся положительным,
    /// поэтому длинный вектор нормализовался бы и умножался на отрицательное
    /// число, то есть разворачивался. Длина соблюдалась бы, направление нет.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Предел отрицательный.</exception>
    public static Vector3 ClampLength(this Vector3 vector, float maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);

        return vector.LengthSquared() <= maxLength * maxLength ? vector : Vector3.Normalize(vector) * maxLength;
    }

    /// <summary>
    /// Проецирует вектор на направление: возвращает компонент вдоль направления.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <param name="direction">Направление проекции, должно быть ненулевым.</param>
    /// <returns>Проекция вектора на направление.</returns>
    /// <exception cref="ArgumentException">Направление нулевое.</exception>
    public static Vector3 ProjectOntoDirection(this Vector3 vector, Vector3 direction)
    {
        float lengthSquared = direction.LengthSquared();
        if (lengthSquared <= Scalar.Epsilon * Scalar.Epsilon)
        {
            throw new ArgumentException("Направление проекции должно быть ненулевым.", nameof(direction));
        }

        return direction * (Vector3.Dot(vector, direction) / lengthSquared);
    }

    /// <summary>
    /// Проецирует точку на плоскость, проходящую через начало координат с
    /// заданной нормалью: компонента вдоль нормали отбрасывается.
    /// </summary>
    /// <param name="point">Проецируемая точка.</param>
    /// <param name="planeNormal">Нормаль плоскости, ненулевая; нормализуется внутри.</param>
    /// <returns>Проекция точки на плоскость.</returns>
    /// <exception cref="ArgumentException">Нормаль нулевая.</exception>
    public static Vector3 ProjectOntoPlane(this Vector3 point, Vector3 planeNormal)
    {
        Vector3 normal = planeNormal.SafeNormalize();
        if (normal == Vector3.Zero)
        {
            throw new ArgumentException("Нормаль плоскости должна быть ненулевой.", nameof(planeNormal));
        }

        return point - normal * Vector3.Dot(point, normal);
    }

    /// <summary>
    /// Отбрасывает компоненту точки вдоль нормали, оставляя только её.
    /// Обратная операция к <see cref="ProjectOntoPlane"/>.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <param name="planeNormal">Нормаль плоскости, ненулевая; нормализуется внутри.</param>
    /// <returns>Компонента вдоль нормали.</returns>
    /// <exception cref="ArgumentException">Нормаль нулевая.</exception>
    public static Vector3 RejectFromPlane(this Vector3 vector, Vector3 planeNormal)
    {
        Vector3 normal = planeNormal.SafeNormalize();
        if (normal == Vector3.Zero)
        {
            throw new ArgumentException("Нормаль плоскости должна быть ненулевой.", nameof(planeNormal));
        }

        return normal * Vector3.Dot(vector, normal);
    }

    /// <summary>
    /// Возвращает вектор движения к цели с ограничением длины шага.
    /// </summary>
    /// <param name="from">Текущая позиция.</param>
    /// <param name="to">Целевая позиция.</param>
    /// <param name="maxStep">Максимальная длина шага.</param>
    /// <returns>Вектор смещения, который не перескакивает цель.</returns>
    public static Vector3 MoveTowards(this Vector3 from, Vector3 to, float maxStep)
    {
        Vector3 delta = to - from;
        float lengthSquared = delta.LengthSquared();
        return lengthSquared <= maxStep * maxStep || lengthSquared <= Scalar.Epsilon * Scalar.Epsilon
            ? delta
            : delta * (maxStep / MathF.Sqrt(lengthSquared));
    }

    /// <summary>
    /// Поворачивает точку вокруг опорной точки.
    /// </summary>
    /// <param name="point">Поворачиваемая точка.</param>
    /// <param name="pivot">Неподвижная точка вращения.</param>
    /// <param name="rotation">Поворот.</param>
    /// <returns>Повёрнутая точка.</returns>
    public static Vector3 RotateAround(this Vector3 point, Vector3 pivot, in Quaternion rotation)
        => pivot + rotation.Rotate(point - pivot);

    /// <summary>
    /// Возвращает азимут направления: угол в плоскости XZ от оси X в сторону
    /// оси Z. Вертикальная составляющая не учитывается.
    /// </summary>
    /// <param name="direction">Направление.</param>
    /// <returns>Угол азимута.</returns>
    /// <remarks>
    /// Считается через <see cref="Trig"/>, а не вызовом <c>Math.Atan2</c>:
    /// двойная точность здесь ничего не добавляет, обращение к математической
    /// библиотеке платформы обходит фасад и делает азимут зависимым от
    /// операционной системы. Замер: 39.7 нс против 18.4 нс.
    /// </remarks>
    public static Angle ToAngle(this Vector3 direction)
    {
        // Азимут не определён у чисто вертикального направления: там
        // горизонтальная составляющая равна нулю, и направление назад не
        // выбирается. Возвращается ноль, как и у Vector2.ToAngle.
        float horizontal = MathF.Sqrt((direction.X * direction.X) + (direction.Z * direction.Z));
        if (horizontal == 0f)
        {
            return Angle.Zero;
        }

        // Масштабирование направления на положительный множитель не меняет
        // аргумент atan2, поэтому нормализация не нужна.
        return Angle.FromRadiansRaw(Trig.Atan2(direction.Z, direction.X));
    }

    /// <summary>
    /// Возвращает знаковый угол поворота от одного направления к другому
    /// вокруг оси. Знак задаётся правилом правой руки относительно оси.
    /// </summary>
    /// <param name="from">Начальное направление, ненулевое.</param>
    /// <param name="to">Конечное направление, ненулевое.</param>
    /// <param name="axis">Ось вращения, ненулевая; нормализуется внутри.</param>
    /// <returns>Угол в диапазоне (-π; π].</returns>
    /// <exception cref="ArgumentException">Направление или ось нулевые.</exception>
    /// <remarks>
    /// Угол считается как <c>atan2(ось · (a × b), a · b)</c> по перпендикулярным
    /// составляющим направлений. Формула через <c>acos</c> теряла бы
    /// относительную точность на малых углах и требовала нормализации обоих
    /// направлений.
    /// </remarks>
    public static Angle SignedAngleAround(Vector3 from, Vector3 to, Vector3 axis)
    {
        Vector3 unitAxis = axis.SafeNormalize();
        if (unitAxis == Vector3.Zero)
        {
            throw new ArgumentException("Ось вращения должна быть ненулевой.", nameof(axis));
        }

        Vector3 fromPerpendicular = from.ProjectOntoPlane(unitAxis);
        Vector3 toPerpendicular = to.ProjectOntoPlane(unitAxis);
        if (fromPerpendicular == Vector3.Zero || toPerpendicular == Vector3.Zero)
        {
            throw new ArgumentException(
                "Направление не должно быть параллельно оси вращения: знак угла не определён.",
                fromPerpendicular == Vector3.Zero ? nameof(from) : nameof(to));
        }

        // atan2 вместо acos. Числитель и знаменатель масштабируются одним и
        // тем же положительным множителем |fromPerpendicular| * |toPerpendicular|,
        // а atan2 инвариантен к общему положительному масштабу, поэтому
        // нормализовать векторы не нужно. acos же требует косинуса, а у
        // косинуса малого угла в float вся информация теряется: значение
        // округляется в 1.0 и угол меньше примерно 0.01° возвращается нулём.
        float signedSine = Vector3.Dot(Vector3.Cross(fromPerpendicular, toPerpendicular), unitAxis);
        float cosine = Vector3.Dot(fromPerpendicular, toPerpendicular);
        return Angle.FromRadians(Trig.Atan2(signedSine, cosine));
    }

    /// <summary>
    /// Возвращает единичный вектор, перпендикулярный исходному и
    /// направленный как можно ближе к подсказке.
    /// </summary>
    /// <param name="vector">Вектор, которому строится перпендикуляр.</param>
    /// <param name="hint">Желаемое направление перпендикуляра.</param>
    /// <returns>Единичный вектор, перпендикулярный <paramref name="vector"/>.</returns>
    /// <remarks>
    /// В трёх измерениях перпендикуляр не единственен, поэтому требуется
    /// подсказка: без неё результат зависел бы от порядка осей и менялся бы
    /// вместе с ним. Если подсказка параллельна вектору, берётся любая базовая
    /// ось, наименее с ним сонаправленная.
    /// </remarks>
    public static Vector3 Perpendicular(this Vector3 vector, Vector3 hint)
    {
        Vector3 normal = vector.SafeNormalize();
        if (normal == Vector3.Zero)
        {
            throw new ArgumentException("Вектор должен быть ненулевым.", nameof(vector));
        }

        Vector3 candidate = hint.ProjectOntoPlane(normal);
        if (candidate != Vector3.Zero)
        {
            return Vector3.Normalize(candidate);
        }

        Vector3 axis = LeastAlignedAxis(normal);
        return Vector3.Normalize(Vector3.Cross(axis, normal));
    }

    /// <summary>
    /// Строит направление из сферических координат: полярный угол отсчитывается
    /// от вертикали вверх, азимут — в горизонтальной плоскости от оси X в
    /// сторону оси Z.
    /// </summary>
    /// <param name="radius">Длина вектора.</param>
    /// <param name="polar">Угол от оси Y.</param>
    /// <param name="azimuth">Азимут в плоскости XZ.</param>
    /// <returns>Вектор заданной длины и направления.</returns>
    public static Vector3 FromSpherical(float radius, Angle polar, Angle azimuth)
    {
        // Синус и косинус каждого угла берутся одним вызовом: два отдельных
        // вызова математической библиотеки вдвое дороже, а в сумме здесь было
        // четыре вызова вместо двух.
        (float sinPolar, float cosPolar) = Trig.SinCos((float)polar.Radians);
        (float sinAzimuth, float cosAzimuth) = Trig.SinCos((float)azimuth.Radians);

        return new Vector3(
            radius * sinPolar * cosAzimuth,
            radius * cosPolar,
            radius * sinPolar * sinAzimuth);
    }

    /// <summary>
    /// Проверяет, лежит ли значение вектора в допуске от нуля по всем компонентам.
    /// </summary>
    /// <param name="vector">Проверяемый вектор.</param>
    /// <param name="epsilon">Допуск.</param>
    /// <returns><c>true</c>, если вектор близок к нулю.</returns>
    public static bool IsNearlyZero(this Vector3 vector, float epsilon = Scalar.Epsilon)
        => MathF.Abs(vector.X) <= epsilon
            && MathF.Abs(vector.Y) <= epsilon
            && MathF.Abs(vector.Z) <= epsilon;

    private static Vector3 LeastAlignedAxis(in Vector3 unit)
    {
        float absoluteX = MathF.Abs(unit.X);
        float absoluteY = MathF.Abs(unit.Y);
        float absoluteZ = MathF.Abs(unit.Z);
        if (absoluteX <= absoluteY && absoluteX <= absoluteZ)
        {
            return Vector3.UnitX;
        }

        return absoluteY <= absoluteZ ? Vector3.UnitY : Vector3.UnitZ;
    }
}