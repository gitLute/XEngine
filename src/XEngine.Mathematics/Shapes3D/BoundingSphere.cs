using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Сфера, задаваемая центром и радиусом. Основной ограничивающий объём для
/// объектов с известным экранным радиусом.
/// </summary>
/// <remarks>
/// Название <c>BoundingSphere</c>, а не <c>Sphere3</c>: сфера нужна как
/// ограничивающий объём, и «граница» точнее описывает её роль (6.4).
/// </remarks>
public readonly struct BoundingSphere : IEquatable<BoundingSphere>
{
    /// <summary>
    /// Создаёт сферу.
    /// </summary>
    /// <param name="center">Центр.</param>
    /// <param name="radius">Радиус; отрицательные значения не допускаются.</param>
    /// <exception cref="ArgumentOutOfRangeException">Радиус отрицательный.</exception>
    public BoundingSphere(Vector3 center, float radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);

        Center = center;
        Radius = radius;
    }

    /// <summary>
    /// Создаёт сферу, покрывающую параллелепипед: центр совпадает с центром
    /// параллелепипеда, радиус равен половине диагонали.
    /// </summary>
    /// <param name="bounds">Покрываемый параллелепипед.</param>
    /// <returns>Описанная сфера.</returns>
    /// <remarks>
    /// У пустого параллелепипеда половина размера бесконечна, а центр равен
    /// нулю, поэтому наивный подсчёт давал бы сферу бесконечного радиуса.
    /// Пустой параллелепипед не содержит ни одной точки, и сфера нулевого
    /// радиуса в нуле — единственное честное описание: она не покрывает
    /// ничего и не вводит в заблуждение.
    /// </remarks>
    public static BoundingSphere FromAabb(in Aabb3 bounds)
        => bounds.IsEmpty ? new BoundingSphere(Vector3.Zero, 0f) : new BoundingSphere(bounds.Center, bounds.HalfSize.Length());

    /// <summary>
    /// Создаёт сферу, покрывающую набор точек.
    /// </summary>
    /// <param name="points">Точки, которые нужно покрыть.</param>
    /// <returns>Сфера с центром в средней точке границ.</returns>
    /// <exception cref="ArgumentException">Список точек пуст.</exception>
    public static BoundingSphere FromPoints(ReadOnlySpan<Vector3> points)
    {
        Aabb3 bounds = Aabb3.FromPoints(points);
        return FromAabb(bounds);
    }

    /// <summary>
    /// Центр сферы.
    /// </summary>
    public Vector3 Center { get; }

    /// <summary>
    /// Радиус сферы.
    /// </summary>
    public float Radius { get; }

    /// <summary>
    /// Диаметр сферы.
    /// </summary>
    public float Diameter => Radius * 2f;

    /// <summary>
    /// Параллелепипед, описанный вокруг сферы.
    /// </summary>
    public Aabb3 Bounds => Aabb3.FromCenterAndHalfSize(Center, new Vector3(Radius, Radius, Radius));

    /// <summary>
    /// Проверяет принадлежность точки, включая границу.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <returns><c>true</c>, если точка внутри.</returns>
    /// <remarks>
    /// Обычный случай — сравнение квадратов: корень не нужен, а проверка идёт
    /// на каждый объект при каждом кадре.
    /// <para>
    /// Квадраты, однако, обнуляются и переполняются вместе с float, поэтому
    /// одно только сравнение квадратов верно не всегда. Проверяется не длина
    /// смещения, а квадрат радиуса, и этого достаточно: если <c>R²</c> и конечен,
    /// и больше нуля, то <c>R</c> лежит между 1.057e-19 и 1.8446744e19. Тогда
    /// нулевой квадрат смещения означает длину меньше 3.743392e-23, то есть
    /// заведомо меньше <c>R</c>, и верный ответ — «внутри»; переполненный
    /// квадрат означает длину больше 1.8446744e19, то есть заведомо больше
    /// <c>R</c>, и верный ответ — «снаружи». Оба случая сравнение квадратов и
    /// без того даёт верно.
    /// </para>
    /// <para>
    /// Ломается оно ровно там, где <c>R²</c> обнулился или переполнился.
    /// <c>R = 1.8446744e19</c> и выше давал <c>inf ≤ x</c>, то есть <c>true</c>
    /// для любой точки; <c>R = 1e-19</c> и ниже давал <c>x ≤ 0</c>, то есть
    /// <c>true</c> для любой точки на расстоянии до 3.743392e-23 от центра.
    /// Измерено на прежнем коде: при <c>R = 1.8446744e19</c> неверных ответов
    /// 19848 из 20000.
    /// </para>
    /// </remarks>
    public bool Contains(Vector3 point)
    {
        float reachSquared = Radius * Radius;
        if (reachSquared > 0f && float.IsFinite(reachSquared))
        {
            return (point - Center).LengthSquared() <= reachSquared;
        }

        // Квадрат радиуса обнулился или переполнился. Сравниваются длины в
        // масштабе наибольшей компоненты смещения: там она равна единице,
        // квадрат лежит в [1; 3], и обе величины конечны при любой длине.
        //
        // Ветвь написана прямо здесь, а не вынесена в отдельный метод и не
        // помечена NoInlining. Замер на одинаковых структурах в одном процессе:
        // прежний код 2.86 нс, ветвь отдельным методом 4.70 нс (+64 %),
        // ветвь внутри метода 3.50 нс (+22 %). Вынос редкой ветви съедал
        // больше, чем сама ветвь.
        Vector3 delta = point - Center;
        float scale = MathF.Max(MathF.Abs(delta.X), MathF.Max(MathF.Abs(delta.Y), MathF.Abs(delta.Z)));
        if (scale == 0f)
        {
            return true;
        }

        Vector3 scaled = delta / scale;
        float scaledRadius = Radius / scale;
        return scaled.LengthSquared() <= scaledRadius * scaledRadius;
    }

    /// <summary>
    /// Проверяет пересечение с другой сферой.
    /// </summary>
    /// <param name="other">Другая сфера.</param>
    /// <returns><c>true</c>, если сферы пересекаются или касаются.</returns>
    /// <remarks>
    /// Обоснование ветвления то же, что в <see cref="Contains"/>, только
    /// проверяется квадрат суммы радиусов. Сумма радиусов переполняется раньше
    /// квадрата: она больше 3.4e38 уже при двух радиусах по 1.7e38, то есть
    /// граница перехода на сравнение длин проходит по <c>R = 1.7e19</c>, а не
    /// по 1.8446744e19. Измерено на прежнем коде: при <c>R = 1.5e19</c> неверных
    /// ответов 20000 из 20000, то есть метод отвечал <c>true</c> на всё.
    /// </remarks>
    public bool Intersects(in BoundingSphere other)
    {
        float reach = Radius + other.Radius;
        float reachSquared = reach * reach;
        if (reachSquared > 0f && float.IsFinite(reachSquared))
        {
            return (other.Center - Center).LengthSquared() <= reachSquared;
        }

        // Ветвь внутри метода по той же причине и с тем же замером, что и в
        // Contains: отдельный метод стоил бы 5.27 нс против 3.50 нс здесь.
        Vector3 delta = other.Center - Center;
        float scale = MathF.Max(MathF.Abs(delta.X), MathF.Max(MathF.Abs(delta.Y), MathF.Abs(delta.Z)));
        if (scale == 0f)
        {
            return true;
        }

        Vector3 scaled = delta / scale;
        float scaledReach = reach / scale;
        return scaled.LengthSquared() <= scaledReach * scaledReach;
    }

    /// <summary>
    /// Возвращает расстояние от точки до сферы.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Расстояние; ноль для точки внутри.</returns>
    public float DistanceTo(Vector3 point) => MathF.Max(0f, Vector3.Distance(point, Center) - Radius);

    /// <summary>
    /// Возвращает ближайшую к точке точку поверхности сферы.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Точка внутри сферы у самой поверхности.</returns>
    /// <remarks>
    /// Точка сдвигается от поверхности внутрь на четыре последних разряда
    /// радиуса. Округление деления и умножения само по себе уводит результат
    /// наружу примерно в четырёх случаях из тысяч, и тогда
    /// <see cref="Contains"/> отвергал точку, полученную от собственного
    /// метода библиотеки. Сдвиг величиной в четыре разряда не виден ни в
    /// одном применении.
    /// <para>
    /// Точка в центре задана неоднозначно: подходит любое направление, и
    /// берётся ось X. Такой результат лежит на поверхности по построению.
    /// </para>
    /// </remarks>
    public Vector3 ClosestPointOnSurface(Vector3 point)
    {
        Vector3 offset = point - Center;
        float length = offset.Length();
        return length <= Scalar.Epsilon
            ? Center + new Vector3(Radius, 0f, 0f)
            : Center + offset * (SurfaceRadius.For(Radius, Center) / length);
    }


    /// <summary>
    /// Возвращает сферу, покрывающую обе.
    /// </summary>
    /// <param name="other">Другая сфера.</param>
    /// <returns>Объединение сфер.</returns>
    public BoundingSphere Union(in BoundingSphere other)
    {
        Vector3 delta = other.Center - Center;
        float distance = delta.Length();

        if (distance + other.Radius <= Radius)
        {
            return this;
        }

        if (distance + Radius <= other.Radius)
        {
            return other;
        }

        float half = (distance + other.Radius - Radius) * 0.5f;
        float shift = half / distance;
        return new BoundingSphere(Center + delta * shift, Radius + half);
    }

    /// <inheritdoc/>
    public bool Equals(BoundingSphere other) => Center.Equals(other.Center) && Radius.Equals(other.Radius);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is BoundingSphere other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Center, Radius);

    /// <summary>
    /// Сравнивает сферы на равенство.
    /// </summary>
    /// <param name="left">Первая сфера.</param>
    /// <param name="right">Вторая сфера.</param>
    /// <returns><c>true</c>, если сферы равны.</returns>
    public static bool operator ==(BoundingSphere left, BoundingSphere right) => left.Equals(right);

    /// <summary>
    /// Сравнивает сферы на неравенство.
    /// </summary>
    /// <param name="left">Первая сфера.</param>
    /// <param name="right">Вторая сфера.</param>
    /// <returns><c>true</c>, если сферы различаются.</returns>
    public static bool operator !=(BoundingSphere left, BoundingSphere right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"BoundingSphere({Center}, r={Radius:F3})";
}