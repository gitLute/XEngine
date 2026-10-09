using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Луч: начало и нормализованное направление.
/// </summary>
public readonly struct Ray2 : IEquatable<Ray2>
{
    /// <summary>
    /// Создаёт луч. Направление нормализуется автоматически.
    /// </summary>
    /// <param name="origin">Начало луча.</param>
    /// <param name="direction">Направление луча. Нулевое направление даёт луч с направлением по X.</param>
    public Ray2(Vector2 origin, Vector2 direction)
    {
        Origin = origin;

        // Нормализация считается один раз. Прежняя запись звала SafeNormalize
        // в обоих ветвлении, то есть выполняла корень дважды на каждый луч,
        // а луч создаётся на каждый запрос пересечения.
        Vector2 normalized = direction.SafeNormalize();
        Direction = normalized == Vector2.Zero ? Vector2.UnitX : normalized;
    }

    /// <summary>
    /// Начало луча.
    /// </summary>
    public Vector2 Origin { get; }

    /// <summary>
    /// Нормализованное направление луча.
    /// </summary>
    public Vector2 Direction { get; }

    /// <summary>
    /// Возвращает точку на расстоянии <paramref name="distance"/> от начала луча.
    /// </summary>
    /// <param name="distance">Расстояние. Отрицательные значения идут в обратную сторону.</param>
    /// <returns>Точка на луче.</returns>
    public Vector2 GetPoint(float distance) => Origin + Direction * distance;

    /// <summary>
    /// Возвращает расстояние от начала луча до заданной точки вдоль направления.
    /// </summary>
    /// <param name="point">Заданная точка.</param>
    /// <returns>Проекция точки на направление луча.</returns>
    public float ProjectOntoDirection(Vector2 point) => Vector2.Dot(point - Origin, Direction);

    /// <summary>
    /// Проверяет пересечение с AABB.
    /// </summary>
    /// <param name="bounds">Ограничивающий прямоугольник.</param>
    /// <returns><c>true</c>, если луч пересекает прямоугольник.</returns>
    /// <remarks>
    /// Пустой прямоугольник не пересекается ни с чем. Проверка обязательна:
    /// у <see cref="Aabb2.Empty"/> границы переставлены (<c>Min = +inf</c>,
    /// <c>Max = -inf</c>), и слэб-метод на такой паре даёт непустой интервал
    /// параметров, то есть объявлял бы пересечение пустого объёма. Это ровно то,
    /// что проверяет <c>Ray3.IntersectsBox</c>, и проверка перенесена оттуда
    /// без изменения смысла: два метода с одним названием не должны отвечать на
    /// один вопрос по-разному.
    /// <para>
    /// Ветка «компонента направления нулевая» этот случай закрывала сама,
    /// поэтому ошибка проявлялась только на лучах с двумя ненулевыми
    /// компонентами, то есть зависела от направления и не от позиции.
    /// </para>
    /// </remarks>
    public bool Intersects(Aabb2 bounds)
    {
        if (bounds.IsEmpty)
        {
            return false;
        }

        float tMin = 0f;
        float tMax = float.MaxValue;

        for (int axis = 0; axis < 2; axis++)
        {
            float origin = axis == 0 ? Origin.X : Origin.Y;
            float direction = axis == 0 ? Direction.X : Direction.Y;
            float min = axis == 0 ? bounds.Min.X : bounds.Min.Y;
            float max = axis == 0 ? bounds.Max.X : bounds.Max.Y;

            if (MathF.Abs(direction) <= Scalar.Epsilon)
            {
                if (origin < min || origin > max)
                {
                    return false;
                }

                continue;
            }

            float inverse = 1f / direction;
            float t1 = (min - origin) * inverse;
            float t2 = (max - origin) * inverse;
            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
            }

            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);
            if (tMin > tMax)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Проверяет пересечение с кругом.
    /// </summary>
    /// <param name="circle">Круг.</param>
    /// <returns><c>true</c>, если луч пересекает круг.</returns>
    public bool Intersects(Circle2 circle)
    {
        Vector2 toCenter = circle.Center - Origin;
        float projection = Vector2.Dot(toCenter, Direction);
        Vector2 closest = Origin + Direction * MathF.Max(0f, projection);
        return (closest - circle.Center).LengthSquared() <= circle.Radius * circle.Radius;
    }

    /// <summary>
    /// Проверяет пересечение с отрезком.
    /// </summary>
    /// <param name="segment">Отрезок.</param>
    /// <returns><c>true</c>, если луч пересекает отрезок.</returns>
    /// <remarks>
    /// Коллинеарные отрезки считаются пересекающимися, если лежат на луче, а не
    /// отбрасываются как параллельные: отрезок на одной прямой с лучом и
    /// лежащий впереди его начала пересекается с ним по всей длине.
    /// </remarks>
    public bool Intersects(Segment2 segment)
    {
        Vector2 delta = segment.Delta;
        if (delta.LengthSquared() <= Scalar.Epsilon * Scalar.Epsilon)
        {
            // Вырожденный отрезок — это точка: проверяется попадание её на луч.
            //
            // Расстояние до прямой луча берётся векторным произведением, а не
            // вычитанием |offset|^2 - along^2. Вычитание здесь катастрофично:
            // и |offset|^2, и along^2 порядка (расстояние до точки)^2, поэтому
            // разность теряет все значащие цифры и даёт ровно ноль всякий раз,
            // когда точка лежит на прямой луча и на расстоянии, на которое
            // накопленная ошибка уже превысила Scalar.Epsilon.
            //
            // Фактический порог получался не 1e-6 метра, а примерно
            // along * 2^-12.5, то есть рос линейно с расстоянием от начала луча:
            // измерено 2.4e-4 метра на 1 м, 2.5e-2 на 100 м и 3.2 на 10 км. Для
            // пикинга мышью это ложные попадания на расстоянии в десятки
            // сантиметров: вырожденный отрезок — это хитбокс тоньше микрона, то
            // есть ровно те хитбоксы, ради которых двумерный пикинг и пишут.
            //
            // Векторное произведение даёт то же расстояние без вычитания вообще.
            // Порог остаётся тем же, каким уже пользуется ветвь параллельных
            // отрезков ниже, то есть два ответа на вопрос «лежит ли точка на
            // прямой луча» теперь действительно совпадают.
            Vector2 offset = segment.A - Origin;
            float along = Vector2.Dot(offset, Direction);
            return along >= -Scalar.Epsilon
                   && MathF.Abs(Vector2.Cross(offset, Direction)) <= Scalar.Epsilon;
        }

        // Параллельность измеряется углом, а не длиной. Векторное произведение
        // равно |направление|·|delta|·sin угла, то есть произведение длины на
        // синус: сравнение с Scalar.Epsilon отвечает на вопрос о длине отрезка,
        // а спрашивался угол. Ответ получался разным для одного и того же
        // направления: отрезок длиной 1 мм объявлял параллельным луч, отклонённый
        // на 0.057 градуса, а отрезок длиной 1000 метров — только на 5.7e-8
        // радиана. Два отрезка на одной прямой давали противоположные ответы, и
        // результат зависел от масштаба мира, что запрещено соглашением
        // библиотеки.
        //
        // Поэтому здесь sin угла вычисляется явно: направление единичное, и
        // остаётся одно деление и длина delta. Порог тот же по величине, но
        // теперь на одинаковую величину во всех случаях.
        float denominator = Vector2.Cross(Direction, delta);
        float sine = MathF.Abs(denominator) / delta.Length();
        if (sine <= Scalar.Epsilon)
        {
            // Прямые параллельны: пересечение есть только при совпадении прямых,
            // то есть когда отрезок лежит на луче. Проверяются оба конца: луч
            // может идти вдоль отрезка, а может быть направлен от него.
            Vector2 offset = segment.A - Origin;
            if (MathF.Abs(Vector2.Cross(offset, Direction)) > Scalar.Epsilon)
            {
                return false;
            }

            float near = Vector2.Dot(offset, Direction);
            float far = near + Vector2.Dot(delta, Direction);
            return near >= -Scalar.Epsilon || far >= -Scalar.Epsilon;
        }

        Vector2 difference = segment.A - Origin;
        float rayT = Vector2.Cross(difference, delta) / denominator;
        float segmentT = Vector2.Cross(difference, Direction) / denominator;
        return rayT >= 0f && segmentT >= 0f && segmentT <= 1f;
    }

    /// <inheritdoc/>
    public bool Equals(Ray2 other) => Origin.Equals(other.Origin) && Direction.Equals(other.Direction);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Ray2 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Origin, Direction);

    /// <summary>
    /// Сравнивает лучи на равенство.
    /// </summary>
    /// <param name="left">Первый луч.</param>
    /// <param name="right">Второй луч.</param>
    /// <returns><c>true</c>, если лучи равны.</returns>
    public static bool operator ==(Ray2 left, Ray2 right) => left.Equals(right);

    /// <summary>
    /// Сравнивает лучи на неравенство.
    /// </summary>
    /// <param name="left">Первый луч.</param>
    /// <param name="right">Второй луч.</param>
    /// <returns><c>true</c>, если лучи различаются.</returns>
    public static bool operator !=(Ray2 left, Ray2 right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"Ray2({Origin}, {Direction})";
}
