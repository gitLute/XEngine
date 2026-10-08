using System.Numerics;
using System.Runtime.CompilerServices;

namespace XEngine.Mathematics;

/// <summary>
/// Луч в трёх измерениях: начало и нормализованное направление.
/// </summary>
/// <remarks>
/// Все запросы луча живут здесь: пересечение с параллелепипедом, сферой,
/// капсулой и плоскостью, а также расстояние до точки. Имена методов совпадают
/// с двумерным <see cref="Ray2"/>, чтобы одна и та же мысль одинаково
/// записывалась в двумерном и трёхмерном коде.
/// </remarks>
public readonly struct Ray3 : IEquatable<Ray3>
{
    /// <summary>
    /// Создаёт луч. Направление нормализуется автоматически.
    /// </summary>
    /// <param name="origin">Начало луча.</param>
    /// <param name="direction">
    /// Направление луча. Нулевое направление даёт луч вдоль оси X, чтобы
    /// вырожденный луч оставался пригодным для арифметики вместо NaN.
    /// </param>
    public Ray3(Vector3 origin, Vector3 direction)
    {
        Origin = origin;
        Vector3 normalized = direction.SafeNormalize();
        Direction = normalized == Vector3.Zero ? Vector3.UnitX : normalized;
    }

    /// <summary>
    /// Начало луча.
    /// </summary>
    public Vector3 Origin { get; }

    /// <summary>
    /// Нормализованное направление луча.
    /// </summary>
    public Vector3 Direction { get; }

    /// <summary>
    /// Возвращает точку на расстоянии <paramref name="distance"/> от начала луча.
    /// </summary>
    /// <param name="distance">Расстояние; отрицательные значения идут в обратную сторону.</param>
    /// <returns>Точка на луче.</returns>
    public Vector3 GetPoint(float distance) => Origin + Direction * distance;

    /// <summary>
    /// Возвращает расстояние от начала луча до заданной точки вдоль направления.
    /// </summary>
    /// <param name="point">Заданная точка.</param>
    /// <returns>Проекция точки на направление луча.</returns>
    public float ProjectOntoDirection(Vector3 point) => Vector3.Dot(point - Origin, Direction);

    /// <summary>
    /// Возвращает ближайшую к точке точку луча: за началом луча ближайшей
    /// точкой является само начало.
    /// </summary>
    /// <param name="point">Заданная точка.</param>
    /// <returns>Точка на луче.</returns>
    public Vector3 ClosestPointTo(Vector3 point)
    {
        float projection = ProjectOntoDirection(point);
        return Origin + Direction * MathF.Max(0f, projection);
    }

    /// <summary>
    /// Проверяет пересечение с параллелепипедом.
    /// </summary>
    /// <param name="bounds">Ограничивающий параллелепипед.</param>
    /// <returns><c>true</c>, если луч пересекает объём.</returns>
    public bool Intersects(in Aabb3 bounds) => IntersectsBox(bounds, out _);

    /// <summary>
    /// Проверяет пересечение с параллелепипедом и сообщает расстояние до
    /// ближайшей точки пересечения.
    /// </summary>
    /// <param name="bounds">Ограничивающий параллелепипед.</param>
    /// <param name="distance">
    /// Расстояние до входа в объём; ноль, если начало луча внутри или луч
    /// не пересекает объём.
    /// </param>
    /// <returns><c>true</c>, если луч пересекает объём.</returns>
    public bool Raycast(in Aabb3 bounds, out float distance) => IntersectsBox(bounds, out distance);

    /// <summary>
    /// Проверяет пересечение со сферой.
    /// </summary>
    /// <param name="sphere">Ограничивающая сфера.</param>
    /// <returns><c>true</c>, если луч пересекает сферу.</returns>
    public bool Intersects(in BoundingSphere sphere) => IntersectsSphere(sphere, out _);

    /// <summary>
    /// Проверяет пересечение со сферой и сообщает расстояние до входа.
    /// </summary>
    /// <param name="sphere">Ограничивающая сфера.</param>
    /// <param name="distance">Расстояние до входа; ноль при промахе или старте внутри.</param>
    /// <returns><c>true</c>, если луч пересекает сферу.</returns>
    public bool Raycast(in BoundingSphere sphere, out float distance) => IntersectsSphere(sphere, out distance);

    /// <summary>
    /// Проверяет пересечение с капсулой.
    /// </summary>
    /// <param name="capsule">Капсула.</param>
    /// <returns><c>true</c>, если луч пересекает капсулу.</returns>
    public bool Intersects(in Capsule3 capsule) => IntersectsCapsule(capsule, out _);

    /// <summary>
    /// Проверяет пересечение с капсулой и сообщает расстояние до входа.
    /// </summary>
    /// <param name="capsule">Капсула.</param>
    /// <param name="distance">Расстояние до входа; ноль при промахе или старте внутри.</param>
    /// <returns><c>true</c>, если луч пересекает капсулу.</returns>
    public bool Raycast(in Capsule3 capsule, out float distance) => IntersectsCapsule(capsule, out distance);

    /// <summary>
    /// Проверяет пересечение с плоскостью.
    /// </summary>
    /// <param name="plane">Плоскость.</param>
    /// <returns><c>true</c>, если луч пересекает плоскость.</returns>
    public bool Intersects(in Plane3 plane) => Intersects(plane, out _);

    /// <summary>
    /// Проверяет пересечение с плоскостью и сообщает расстояние до точки
    /// пересечения.
    /// </summary>
    /// <param name="plane">Плоскость.</param>
    /// <param name="distance">
    /// Расстояние до точки пересечения; ноль, если луч лежит в плоскости или
    /// параллелен ей.
    /// </param>
    /// <returns><c>true</c>, если луч пересекает плоскость.</returns>
    /// <remarks>
    /// Пересечение считается только перед началом луча: луч, направленный от
    /// плоскости, её не касается. Луч, лежащий в плоскости или параллельный ей,
    /// пересечением не считается: касание без пересечения неоднозначно и в
    /// отсечении трактуется как промах.
    /// </remarks>
    public bool Intersects(in Plane3 plane, out float distance)
    {
        float denominator = Vector3.Dot(Direction, plane.Normal);
        if (MathF.Abs(denominator) <= Scalar.Epsilon)
        {
            distance = 0f;
            return false;
        }

        distance = -plane.DistanceTo(Origin) / denominator;
        return distance >= 0f;
    }

    private bool IntersectsBox(in Aabb3 bounds, out float distance)
    {
        distance = 0f;
        if (bounds.IsEmpty)
        {
            // У пустого параллелепипеда Min = +inf и Max = -inf: слэб-метод
            // переставляет границы и получает пересечение, которого нет.
            return false;
        }

        float min = 0f;
        float max = float.MaxValue;
        Vector3 origin = Origin;
        Vector3 direction = Direction;

        // Оси развёрнуты, а не перебираются: цикл из трёх итераций с выбором
        // компоненты по индексу компилятор не разворачивает надёжно, а запрос
        // луча идёт на каждый объект при каждом движении. Порядок осей значения
        // не имеет, потому что отрезок параметров пересекается пересечением.
        if (!Clip(origin.X, direction.X, bounds.Min.X, bounds.Max.X, ref min, ref max))
        {
            distance = 0f;
            return false;
        }

        if (!Clip(origin.Y, direction.Y, bounds.Min.Y, bounds.Max.Y, ref min, ref max))
        {
            distance = 0f;
            return false;
        }

        if (!Clip(origin.Z, direction.Z, bounds.Min.Z, bounds.Max.Z, ref min, ref max))
        {
            distance = 0f;
            return false;
        }

        distance = min;
        return true;
    }

    /// <summary>
    /// Ограничивает интервал входа и выхода отрезком параметров по одной оси.
    /// </summary>
    /// <param name="origin">Координата начала луча по этой оси.</param>
    /// <param name="step">Компонента направления по этой оси; длина направления равна единице.</param>
    /// <param name="lower">Нижняя граница объёма.</param>
    /// <param name="upper">Верхняя граница объёма.</param>
    /// <param name="min">Текущее начало интервала входа.</param>
    /// <param name="max">Текущий конец интервала выхода.</param>
    /// <returns><c>false</c>, если после этой оси интервал пуст.</returns>
    /// <remarks>
    /// Порог сравнивается с длиной, а не с площадью: компонента направления у
    /// нормализованного вектора безразмерна, и сравнение с
    /// <see cref="Scalar.Epsilon"/> означает «отклонение меньше микрорадиана».
    /// Нулевая компонента обрабатывается отдельно: луч вдоль этой оси не
    /// пересекает грани и попадает внутрь, только если лежит между границами.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Clip(float origin, float step, float lower, float upper, ref float min, ref float max)
    {
        if (MathF.Abs(step) <= Scalar.Epsilon)
        {
            return origin >= lower && origin <= upper;
        }

        float inverse = 1f / step;
        float first = (lower - origin) * inverse;
        float second = (upper - origin) * inverse;
        if (first > second)
        {
            (first, second) = (second, first);
        }

        min = MathF.Max(min, first);
        max = MathF.Min(max, second);
        return min <= max;
    }

    private bool IntersectsSphere(in BoundingSphere sphere, out float distance)
    {
        distance = 0f;
        Vector3 toCenter = sphere.Center - Origin;

        float radius = sphere.Radius;
        float radiusSquared = radius * radius;
        float lengthSquared = toCenter.LengthSquared();

        // Начало луча внутри сферы проверяется напрямую и до всего остального.
        // Раньше здесь стоял ранний выход по projection < 0 («сфера за спиной»),
        // который отбрасывал и старт изнутри; строка Max(0f, ...) ниже была при
        // этом недостижима. Проверять entry < 0 вместо этого нельзя: при
        // отрицательной проекции сфера за спиной и старт изнутри дают один и
        // тот же знак entry.
        if (lengthSquared <= radiusSquared)
        {
            return true;
        }

        float projection = Vector3.Dot(toCenter, Direction);
        float perpendicularSquared = lengthSquared - projection * projection;
        if (perpendicularSquared > radiusSquared)
        {
            // Сфера целиком сбоку от луча: касаться её нечем.
            return false;
        }

        // Половина хорды пересечения сферы вдоль луча.
        float halfChord = MathF.Sqrt(MathF.Max(0f, radiusSquared - perpendicularSquared));
        float entry = projection - halfChord;
        if (entry < 0f)
        {
            return false;
        }

        distance = entry;
        return true;
    }

    private bool IntersectsCapsule(in Capsule3 capsule, out float distance)
    {
        distance = 0f;

        // Начало луча внутри капсулы: вход происходит в ноль. Без этой проверки
        // цилиндр вернул бы точку выхода из тела (луч, начатый внутри, входит в
        // уравнение корнем уже за началом), и расстояние до входа оказалось бы
        // больше нуля вопреки документации метода Raycast.
        if (capsule.Contains(Origin))
        {
            return true;
        }

        bool hitSphereA = IntersectsSphere(new BoundingSphere(capsule.PointA, capsule.Radius), out float enterA);
        bool hitSphereB = IntersectsSphere(new BoundingSphere(capsule.PointB, capsule.Radius), out float enterB);

        bool hitMiddle = IntersectsCylinder(capsule, out float enterCylinder);

        if (!hitSphereA && !hitSphereB && !hitMiddle)
        {
            return false;
        }

        float closest = float.MaxValue;
        if (hitSphereA)
        {
            closest = enterA;
        }

        if (hitSphereB)
        {
            closest = MathF.Min(closest, enterB);
        }

        if (hitMiddle)
        {
            closest = MathF.Min(closest, enterCylinder);
        }

        distance = MathF.Max(0f, closest);
        return true;
    }

    /// <summary>
    /// Пересечение с бесконечным цилиндром вокруг осевой линии капсулы.
    /// Попадание засчитывается, только если точка входа лежит между концами
    /// отрезка; торцы капсулы закрывают остальные случаи.
    /// </summary>
    /// <param name="capsule">Капсула.</param>
    /// <param name="distance">Расстояние до точки входа в цилиндр.</param>
    /// <returns><c>true</c>, если луч входит в тело цилиндра между концами.</returns>
    private bool IntersectsCylinder(in Capsule3 capsule, out float distance)
    {
        distance = 0f;

        Vector3 axis = capsule.Delta;
        float axisLengthSquared = axis.LengthSquared();

        Vector3 toPointA = Origin - capsule.PointA;
        float radius = capsule.Radius;

        // Вырожденная капсула — это сфера: осевая линия сжалась в точку, и
        // пересечение с ней обязано проверяться как пересечение со сферой.
        // Прежняя проверка возвращала false молча, и луч, идущий сквозь
        // капсулу-коллайдер, проходил насквозь.
        if (axisLengthSquared <= Scalar.Epsilon * Scalar.Epsilon)
        {
            return IntersectsSphere(new BoundingSphere(capsule.PointA, radius), out distance);
        }

        float directionOnAxis = Vector3.Dot(Direction, axis);
        float pointOnAxis = Vector3.Dot(toPointA, axis);
        Vector3 perpendicularDirection = Direction - axis * (directionOnAxis / axisLengthSquared);
        Vector3 perpendicularOffset = toPointA - axis * (pointOnAxis / axisLengthSquared);

        // |offset + t * directionPerp|^2 = radius^2
        // Порог — квадрат длины: perpendicularDirection по модулю не больше
        // единицы, и сравнивать его квадрат с длиной, то есть с Scalar.Epsilon,
        // означало бы отбрасывать лучи, отклонённые от оси меньше чем на миллиметр.
        float quadratic = perpendicularDirection.LengthSquared();
        if (quadratic <= Scalar.Epsilon * Scalar.Epsilon)
        {
            // Луч параллелен оси: боковую поверхность он пересечь не может,
            // а торцы закрыты эндкапсулами.
            return false;
        }

        float linear = Vector3.Dot(perpendicularOffset, perpendicularDirection);
        float constant = perpendicularOffset.LengthSquared() - (radius * radius);

        float discriminant = (linear * linear) - (quadratic * constant);
        if (discriminant < 0f)
        {
            return false;
        }

        float root = MathF.Sqrt(discriminant);
        float near = (-linear - root) / quadratic;
        float far = (-linear + root) / quadratic;

        foreach (float candidate in stackalloc[] { near, far })
        {
            if (candidate < 0f)
            {
                continue;
            }

            float along = (pointOnAxis + directionOnAxis * candidate) / axisLengthSquared;
            if (along >= 0f && along <= 1f)
            {
                distance = candidate;
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public bool Equals(Ray3 other) => Origin.Equals(other.Origin) && Direction.Equals(other.Direction);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Ray3 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Origin, Direction);

    /// <summary>
    /// Сравнивает лучи на равенство.
    /// </summary>
    /// <param name="left">Первый луч.</param>
    /// <param name="right">Второй луч.</param>
    /// <returns><c>true</c>, если лучи равны.</returns>
    public static bool operator ==(Ray3 left, Ray3 right) => left.Equals(right);

    /// <summary>
    /// Сравнивает лучи на неравенство.
    /// </summary>
    /// <param name="left">Первый луч.</param>
    /// <param name="right">Второй луч.</param>
    /// <returns><c>true</c>, если лучи различаются.</returns>
    public static bool operator !=(Ray3 left, Ray3 right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"Ray3({Origin}, {Direction})";
}