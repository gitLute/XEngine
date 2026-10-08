using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Операции над векторами, которых нет в <see cref="Vector2"/>.
/// Соглашения: поворот против часовой стрелки, длина в тех же единицах, что и мир.
/// </summary>
public static class VectorExtensions
{
    /// <summary>
    /// Возвращает вектор заданной длины под заданным углом.
    /// </summary>
    /// <param name="length">Длина вектора.</param>
    /// <param name="angle">Угол относительно оси X против часовой стрелки.</param>
    /// <returns>Вектор указанной длины и направления.</returns>
    public static Vector2 FromPolar(float length, Angle angle) => angle.Direction * length;

    /// <summary>
    /// Возвращает вектор, повёрнутый на заданный угол против часовой стрелки.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <param name="angle">Угол поворота.</param>
    /// <returns>Повёрнутый вектор той же длины.</returns>
    public static Vector2 Rotate(this Vector2 vector, Angle angle) => angle.Rotate(vector);

    /// <summary>
    /// Возвращает вектор той же длины с заданным направлением. Направление
    /// исходного вектора на результат не влияет: вместо поворота ему
    /// присваивается <paramref name="angle"/>.
    /// </summary>
    /// <param name="vector">Исходный вектор; используется только его длина.</param>
    /// <param name="angle">Целевое направление.</param>
    /// <returns>Вектор той же длины под заданным углом.</returns>
    /// <remarks>
    /// Не путать с <see cref="Angle.RotateDirection"/>, который именно
    /// поворачивает направление, и с <c>Angle.Rotate</c>, который поворачивает
    /// вектор целиком, сохраняя длину. Совпадения результатов на отдельных
    /// входах случайны.
    /// </remarks>
    public static Vector2 RotateDirection(this Vector2 vector, Angle angle)
        => angle.Direction * vector.Length();

    /// <summary>
    /// Возвращает вектор, повёрнутый на 90 градусов против часовой стрелки.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <returns>Перпендикулярный вектор той же длины.</returns>
    public static Vector2 Perpendicular(this Vector2 vector) => new(-vector.Y, vector.X);

    /// <summary>
    /// Возвращает угол направления вектора. Нулевой вектор даёт нулевой угол.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <returns>Угол направления.</returns>
    public static Angle ToAngle(this Vector2 vector) => Angle.FromDirection(vector);

    /// <summary>
    /// Возвращает нормализованный вектор. Настоящий нулевой вектор остаётся
    /// нулевым.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <returns>Вектор единичной длины либо нулевой.</returns>
    /// <remarks>
    /// Проверяется именно ноль, а не длина меньше Scalar.Epsilon: см.
    /// <see cref="Vector3Extensions.SafeNormalize"/>.
    /// </remarks>
    public static Vector2 SafeNormalize(this Vector2 vector)
        => vector == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(vector);

    /// <summary>
    /// Ограничивает длину вектора сверху.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <param name="maxLength">Максимальная длина.</param>
    /// <returns>Вектор, длина которого не превышает <paramref name="maxLength"/>.</returns>
    public static Vector2 ClampLength(this Vector2 vector, float maxLength)
        => vector.LengthSquared() <= maxLength * maxLength ? vector : Vector2.Normalize(vector) * maxLength;

    /// <summary>
    /// Проецирует вектор на направление.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <param name="direction">Направление проекции, должно быть ненулевым.</param>
    /// <returns>Проекция вектора на направление.</returns>
    /// <exception cref="ArgumentException">Направление нулевое.</exception>
    public static Vector2 Project(this Vector2 vector, Vector2 direction)
    {
        float lengthSquared = direction.LengthSquared();
        if (lengthSquared <= Scalar.Epsilon * Scalar.Epsilon)
        {
            throw new ArgumentException("Направление проекции должно быть ненулевым.", nameof(direction));
        }

        return direction * (Vector2.Dot(vector, direction) / lengthSquared);
    }

    /// <summary>
    /// Возвращает вектор движения к цели с ограничением длины шага.
    /// </summary>
    /// <param name="from">Текущая позиция.</param>
    /// <param name="to">Целевая позиция.</param>
    /// <param name="maxStep">Максимальная длина шага.</param>
    /// <returns>Вектор смещения, который не перескакивает цель.</returns>
    public static Vector2 MoveTowards(this Vector2 from, Vector2 to, float maxStep)
    {
        Vector2 delta = to - from;
        float lengthSquared = delta.LengthSquared();
        return lengthSquared <= maxStep * maxStep || lengthSquared <= Scalar.Epsilon * Scalar.Epsilon
            ? delta
            : delta * (maxStep / MathF.Sqrt(lengthSquared));
    }

    /// <summary>
    /// Возвращает точку, лежащую на отрезке по параметру <paramref name="t"/>.
    /// </summary>
    /// <param name="from">Начало отрезка.</param>
    /// <param name="to">Конец отрезка.</param>
    /// <param name="t">Параметр интерполяции.</param>
    /// <returns>Точка на отрезке.</returns>
    public static Vector2 LerpTo(this Vector2 from, Vector2 to, float t) => Vector2.Lerp(from, to, t);

    /// <summary>
    /// Возвращает знаковое расстояние от точки до прямой, заданной началом и нормалью.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <param name="lineOrigin">Точка на прямой.</param>
    /// <param name="lineNormal">Нормаль прямой, направленная в сторону положительной стороны.</param>
    /// <returns>Знаковое расстояние.</returns>
    public static float SignedDistanceToLine(this Vector2 point, Vector2 lineOrigin, Vector2 lineNormal)
        => Vector2.Dot(point - lineOrigin, lineNormal);

    /// <summary>
    /// Проверяет, лежит ли значение вектора в допуске от нуля по обеим компонентам.
    /// </summary>
    /// <param name="vector">Проверяемый вектор.</param>
    /// <param name="epsilon">Допуск.</param>
    /// <returns><c>true</c>, если вектор близок к нулю.</returns>
    public static bool IsNearlyZero(this Vector2 vector, float epsilon = Scalar.Epsilon)
        => MathF.Abs(vector.X) <= epsilon && MathF.Abs(vector.Y) <= epsilon;
}
