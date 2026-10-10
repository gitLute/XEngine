using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Капсула в трёх измерениях: отрезок, утолщённый на радиус. Основная форма
/// коллайдера персонажей (6.3, 12.4).
/// </summary>
/// <remarks>
/// Отдельного типа <c>Segment3</c> нет: осевая линия — это два конца капсулы,
/// а расстояние до прямой нужно только внутри самой капсулы. Вводить
/// публичный тип ради одного внутреннего вычисления значило бы разойтись с
/// перечнем типов документа.
/// </remarks>
public readonly struct Capsule3 : IEquatable<Capsule3>
{
    /// <summary>
    /// Создаёт капсулу по концам осевой линии и радиусу.
    /// </summary>
    /// <param name="pointA">Начало осевой линии.</param>
    /// <param name="pointB">Конец осевой линии.</param>
    /// <param name="radius">Радиус; отрицательные значения не допускаются.</param>
    /// <exception cref="ArgumentOutOfRangeException">Радиус отрицательный.</exception>
    public Capsule3(Vector3 pointA, Vector3 pointB, float radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);

        PointA = pointA;
        PointB = pointB;
        Radius = radius;
    }

    /// <summary>
    /// Создаёт вертикальную капсулу заданной высоты с центром в заданной точке.
    /// </summary>
    /// <param name="center">Центр капсулы.</param>
    /// <param name="height">Полная высота вместе с закруглениями.</param>
    /// <param name="radius">Радиус; отрицательные значения не допускаются.</param>
    /// <returns>Капсула вдоль вертикальной оси.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Радиус отрицательный или превышает половину высоты.</exception>
    public static Capsule3 FromHeight(Vector3 center, float height, float radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        if (height < radius * 2f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height),
                height,
                "Высота капсулы не может быть меньше двух радиусов.");
        }

        float half = height * 0.5f - radius;
        return new Capsule3(center - new Vector3(0f, half, 0f), center + new Vector3(0f, half, 0f), radius);
    }

    /// <summary>
    /// Начало осевой линии.
    /// </summary>
    public Vector3 PointA { get; }

    /// <summary>
    /// Конец осевой линии.
    /// </summary>
    public Vector3 PointB { get; }

    /// <summary>
    /// Вектор осевой линии от начала к концу.
    /// </summary>
    public Vector3 Delta => PointB - PointA;

    /// <summary>
    /// Радиус капсулы.
    /// </summary>
    public float Radius { get; }

    /// <summary>
    /// Центр капсулы.
    /// </summary>
    public Vector3 Center => (PointA + PointB) * 0.5f;

    /// <summary>
    /// Параллелепипед, описанный вокруг капсулы.
    /// </summary>
    public Aabb3 Bounds
    {
        get
        {
            Vector3 half = new(Radius, Radius, Radius);
            return new Aabb3(Vector3.Min(PointA, PointB) - half, Vector3.Max(PointA, PointB) + half);
        }
    }

    /// <summary>
    /// Проверяет, находится ли точка внутри капсулы.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <returns><c>true</c>, если точка внутри.</returns>
    /// <remarks>
    /// Сравниваются квадраты расстояний, а не расстояния: корень в этой
    /// проверке не нужен, а она стоит в горячем пути попадания луча в капсулу.
    /// <para>
    /// Порога вырожденности здесь нет: ось считается вырожденной по точному
    /// нулю квадрата её длины. Прежняя проверка
    /// <c>lengthSquared &lt;= Epsilon²</c> была абсолютной: ось короче 1e-6
    /// целиком заменялась точкой <see cref="PointA"/>, и вердикт о попадании
    /// переставал зависеть от единиц. Измерено: ось длиной 0.5, радиус 0.25 и
    /// запрос на 0.2 от середины дают верный ответ при масштабе ×1, ×1e-2,
    /// ×1e-4 и ×1e-5 и неверный начиная с ×1e-6, то есть ось длиной 5e-7.
    /// </para>
    /// </remarks>
    public bool Contains(Vector3 point)
    {
        Vector3 delta = Delta;
        float lengthSquared = delta.LengthSquared();
        Vector3 closest = lengthSquared == 0f
            ? PointA
            : PointA + (delta * Scalar.Clamp(Vector3.Dot(point - PointA, delta) / lengthSquared, 0f, 1f));

        return (point - closest).LengthSquared() <= Radius * Radius;
    }

    /// <summary>
    /// Возвращает ближайшую к заданной точку осевой линии.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Точка на осевой линии.</returns>
    /// <remarks>
    /// Вырожденность определяется точным нулём квадрата длины оси, а не
    /// сравнением с <see cref="Scalar.Epsilon"/>: порог с размерностью длины
    /// делал ответ зависимым от выбора единиц. Тот же приём и то же обоснование,
    /// что в <see cref="Segment2.ClosestPointTo"/> и в
    /// <see cref="Capsule2.ClosestPointOnBoundary"/>.
    /// </remarks>
    public Vector3 ClosestPointOnAxis(Vector3 point)
    {
        Vector3 delta = Delta;
        float lengthSquared = delta.LengthSquared();
        if (lengthSquared == 0f)
        {
            return PointA;
        }

        float t = Scalar.Clamp(Vector3.Dot(point - PointA, delta) / lengthSquared, 0f, 1f);
        return PointA + delta * t;
    }

    /// <summary>
    /// Возвращает ближайшую к заданной точку поверхности капсулы.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Точка внутри капсулы у самой поверхности.</returns>
    /// <remarks>
    /// Точка сдвигается от поверхности внутрь на два последних разряда
    /// величины точки отсчёта. Округление умножения на радиус само по себе
    /// уводит результат наружу примерно в девяти случаях из ста, и тогда
    /// <see cref="Contains"/> отвергал точку, полученную от собственного
    /// метода библиотеки: у результата не было правильного объяснения.
    /// <para>
    /// Точка ровно на оси задана неоднозначно: подходит любое направление, и
    /// берётся ось X. Но и здесь сдвиг обязателен: точка с радиуса по
    /// построению лежит на поверхности лишь в приближении float. Измерено: на
    /// 50000 запросов ровно на ось при разных положениях капсулы метод
    /// отдавал точку вне капсулы 9782 раза, то есть в 19.6 % случаев.
    /// </para>
    /// </remarks>
    public Vector3 ClosestPointTo(Vector3 point)
    {
        Vector3 closest = ClosestPointOnAxis(point);
        Vector3 offset = point - closest;
        return offset == Vector3.Zero
            ? closest + new Vector3(SurfaceRadius.For(Radius, closest), 0f, 0f)
            : closest + (offset.SafeNormalize() * SurfaceRadius.For(Radius, closest));
    }


    /// <summary>
    /// Возвращает расстояние от точки до осевой линии.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Расстояние до отрезка, а не до прямой.</returns>
    public float DistanceToAxis(Vector3 point) => Vector3.Distance(ClosestPointOnAxis(point), point);

    /// <summary>
    /// Проверяет, пересекаются ли капсулы. Используется при грубой проверке
    /// пар форм до точного расчёта контактов.
    /// </summary>
    /// <param name="other">Другая капсула.</param>
    /// <returns><c>true</c>, если расстояние между осевыми линиями не больше суммы радиусов.</returns>
    public bool Intersects(in Capsule3 other)
    {
        ClosestPointsBetweenSegments(PointA, PointB, other.PointA, other.PointB, out Vector3 first, out Vector3 second);
        float reach = Radius + other.Radius;
        return Vector3.Distance(first, second) <= reach;
    }

    /// <summary>
    /// Ищет ближайшие точки двух отрезков по стандартной формуле для
    /// параметрических прямых; вырожденные случаи отрезка обрабатываются
    /// отдельно.
    /// </summary>
    /// <param name="startA">Начало первого отрезка.</param>
    /// <param name="endA">Конец первого отрезка.</param>
    /// <param name="startB">Начало второго отрезка.</param>
    /// <param name="endB">Конец второго отрезка.</param>
    /// <param name="closestA">Ближайшая точка первого отрезка.</param>
    /// <param name="closestB">Ближайшая точка второго отрезка.</param>
    private static void ClosestPointsBetweenSegments(
        Vector3 startA,
        Vector3 endA,
        Vector3 startB,
        Vector3 endB,
        out Vector3 closestA,
        out Vector3 closestB)
    {
        // Вырожденность отрезка определяется точным нулём квадрата его длины, а не
        // сравнением с абсолютным порогом. Прежний порог 1e-12 был мерой
        // длины в метрах, то есть вердикт зависел от выбора единиц: измерено,
        // что две пересекающиеся под прямым углом оси при масштабе ×1 дают
        // верный ответ, а начиная с ×1e-6 отвечают «не пересекаются», потому
        // что каждая из них короче 1e-6 и обе целиком заменялись концами.
        // Точный ноль осмыслен: делить на него действительно нельзя, и ровно
        // в этом случае отрезок и подлежит отдельной обработке.
        //
        // Ось короче микрона в float не перестаёт иметь внутренность: её длина
        // помещается в float нормально, пока длина не опустится ниже
        // 1.057e-19, где квадрат обнуляется, и тогда точный ноль срабатывает
        // сам.
        Vector3 first = endA - startA;
        Vector3 second = endB - startB;
        Vector3 offset = startA - startB;

        float lengthSquaredFirst = Vector3.Dot(first, first);
        float lengthSquaredSecond = Vector3.Dot(second, second);
        float alongSecond = Vector3.Dot(second, offset);

        if (lengthSquaredFirst == 0f && lengthSquaredSecond == 0f)
        {
            closestA = startA;
            closestB = startB;
            return;
        }

        if (lengthSquaredFirst == 0f)
        {
            float parameter = Scalar.Clamp(alongSecond / lengthSquaredSecond, 0f, 1f);
            closestA = startA;
            closestB = startB + second * parameter;
            return;
        }

        float alongFirst = Vector3.Dot(first, offset);
        if (lengthSquaredSecond == 0f)
        {
            float parameter = Scalar.Clamp(-alongFirst / lengthSquaredFirst, 0f, 1f);
            closestA = startA + first * parameter;
            closestB = startB;
            return;
        }

        float mixed = Vector3.Dot(first, second);

        // Знаменатель a·e − b² равен |d1|²|d2|²sin²θ, то есть произведение
        // квадратов длин на квадрат синуса угла. Порог задан относительно того
        // же произведения и по существу проверяет sin²θ > Epsilon.
        //
        // Но относительным он был только покуда произведение помещалось во
        // float. Произведение квадратов длин обнуляется при |d1||d2} меньше
        // примерно 1.09e-19, и тогда и знаменатель, и порог становятся нулём,
        // а сравнение 0 > 0 всегда ложно: любые отрезки объявляются
        // параллельными, и точка сближения берётся за конец первого.
        // Измерено на прежнем коде: при масштабе ×1e-12 пересекающиеся под
        // прямым углом оси отвечали «не пересекаются», хотя расстояние между
        // ними равно нулю.
        //
        // Поэтому вся арифметика формулы идёт в double. Все величины приходят
        // из float, то есть не превосходят 3.4e38, а их произведения — 1.2e77,
        // то есть помещаются в double с большим запасом. Относительность
        // порога становится настоящей при любом масштабе, и в этом ровно тот
        // же приём, что в Ray3.IntersectsCylinder: цена — несколько
        // наносекунд на запрос, и Intersects идёт в грубую проверку пар форм,
        // а не в горячий путь попадания точки.
        //
        // Модуль не берётся: знаменатель неотрицателен по неравенству Коши, а
        // округлением может стать слегка отрицательным, и модуль провёл бы
        // деление на почти нулевой знаменатель.
        double squaredFirst = (double)lengthSquaredFirst;
        double squaredSecond = (double)lengthSquaredSecond;
        double cross = (double)mixed;
        double along = (double)alongSecond;
        double alongStart = (double)alongFirst;

        double denominator = (squaredFirst * squaredSecond) - (cross * cross);
        double firstParameter = denominator > (double)Scalar.Epsilon * squaredFirst * squaredSecond
            ? Math.Clamp(((cross * along) - (squaredSecond * alongStart)) / denominator, 0.0, 1.0)
            : 0.0;

        double secondParameter = ((cross * firstParameter) + along) / squaredSecond;
        if (secondParameter < 0.0)
        {
            secondParameter = 0.0;
            firstParameter = Math.Clamp(-alongStart / squaredFirst, 0.0, 1.0);
        }
        else if (secondParameter > 1.0)
        {
            secondParameter = 1.0;
            firstParameter = Math.Clamp(((cross - alongStart) / squaredFirst), 0.0, 1.0);
        }

        closestA = startA + first * (float)firstParameter;
        closestB = startB + second * (float)secondParameter;
    }

    /// <inheritdoc/>
    public bool Equals(Capsule3 other)
        => PointA.Equals(other.PointA) && PointB.Equals(other.PointB) && Radius.Equals(other.Radius);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Capsule3 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(PointA, PointB, Radius);

    /// <summary>
    /// Сравнивает капсулы на равенство.
    /// </summary>
    /// <param name="left">Первая капсула.</param>
    /// <param name="right">Вторая капсула.</param>
    /// <returns><c>true</c>, если капсулы равны.</returns>
    public static bool operator ==(Capsule3 left, Capsule3 right) => left.Equals(right);

    /// <summary>
    /// Сравнивает капсулы на неравенство.
    /// </summary>
    /// <param name="left">Первая капсула.</param>
    /// <param name="right">Вторая капсула.</param>
    /// <returns><c>true</c>, если капсулы различаются.</returns>
    public static bool operator !=(Capsule3 left, Capsule3 right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"Capsule3({PointA} -> {PointB}, r={Radius:F3})";
}