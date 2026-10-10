using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Круг в двумерном пространстве.
/// </summary>
public readonly struct Circle2 : IEquatable<Circle2>
{
    /// <summary>
    /// Создаёт круг.
    /// </summary>
    /// <param name="center">Центр круга.</param>
    /// <param name="radius">
    /// Радиус. Отрицательные и нечисловые значения не допускаются.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Радиус отрицательный или равен <see cref="float.NaN"/>.
    /// </exception>
    /// <remarks>
    /// Проверяется именно <see cref="float.NaN"/>, а не «радиус не конечен»:
    /// сравнение <c>NaN &lt; 0</c> ложно, и без проверки создавался круг с
    /// нечисловым радиусом, у которого <see cref="Bounds"/> давал непустой
    /// бокс из NaN, а <see cref="Contains"/> и <see cref="Intersects"/>
    /// молча отвечали false. Бесконечный радиус не бессмыслен — круг
    /// покрывает всю плоскость, — и отвергать его незачем.
    /// </remarks>
    public Circle2(Vector2 center, float radius)
    {
        if (radius < 0f || float.IsNaN(radius))
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Радиус должен быть неотрицательным и не быть NaN.");
        }

        Center = center;
        Radius = radius;
    }

    /// <summary>
    /// Центр круга.
    /// </summary>
    public Vector2 Center { get; }

    /// <summary>
    /// Радиус круга.
    /// </summary>
    public float Radius { get; }

    /// <summary>
    /// Диаметр круга.
    /// </summary>
    public float Diameter => Radius * 2f;

    /// <summary>
    /// AABB, описанный вокруг круга.
    /// </summary>
    public Aabb2 Bounds => Aabb2.FromCenterAndHalfSize(Center, new Vector2(Radius, Radius));

    /// <summary>
    /// Проверяет, находится ли точка внутри круга.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <returns><c>true</c>, если точка внутри или на границе.</returns>
    public bool Contains(Vector2 point) => (point - Center).LengthSquared() <= Radius * Radius;

    /// <summary>
    /// Проверяет пересечение двух кругов.
    /// </summary>
    /// <param name="other">Другой круг.</param>
    /// <returns><c>true</c>, если круги пересекаются.</returns>
    public bool Intersects(Circle2 other)
        => (other.Center - Center).LengthSquared() <= (Radius + other.Radius) * (Radius + other.Radius);

    /// <summary>
    /// Возвращает ближайшую к заданной точку точку границы круга.
    /// </summary>
    /// <param name="point">Исходная точка.</param>
    /// <returns>Точка внутри круга у самой границы.</returns>
    /// <remarks>
    /// Точка сдвигается от границы внутрь на величину, заданную в
    /// <see cref="SurfaceRadius"/>. Прежняя формулировка «на четыре последних
    /// разряда радиуса» была неверна: сдвиг считается от ВЕЛИЧИНЫ ТОЧКИ
    /// ОТСЧЁТА, то есть центра, а не от радиуса. Измерено: сдвиг равен ровно
    /// двум последним разрядам величины точки отсчёта (2^-22 от неё) и
    /// ограничен половиной радиуса. Там, где центр не дальше от начала
    /// координат, чем радиус, это от двух до четырёх разрядов радиуса; на
    /// большой карте это тысячи разрядов радиуса, пока сдвиг не упрётся в
    /// ограничение половиной радиуса.
    /// <para>
    /// Округление умножения на радиус само по себе уводит результат наружу
    /// примерно в восьми случаях из ста, и тогда <see cref="Contains"/>
    /// отвергал точку, полученную от собственного метода библиотеки: у
    /// результата не было правильного объяснения. Сдвиг величиной в доли
    /// микрометра не виден ни в одном применении, а инвариант «метод
    /// поверхности возвращает точку, которую её же форма считает своей»
    /// выполняется.
    /// </para>
    /// </remarks>
    public Vector2 ClosestPointOnBoundary(Vector2 point)
    {
        Vector2 delta = point - Center;

        // Порог заменён на точный ноль. Прежняя проверка
        // delta.LengthSquared() <= Epsilon² отсекала любой смещение меньше
        // 1e-6 и возвращала Center + (Radius, 0), то есть произвольное
        // направление: на смещении 7.07e-7 под 45° ошибка равнялась
        // 76.54 % радиуса, и не зависела от масштаба круга — её задавал
        // только порог. При радиусе 1e-6 и меньше неверным было 100 % площади
        // круга.
        //
        // Точный ноль остаётся отдельным случаем осмысленно: у запроса ровно
        // в центре направления нет, и подходит любая точка границы, поэтому
        // берётся заранее выбранная. SafeNormalize обнулил бы вектор и вернул
        // центр, а центр границей не является.
        //
        // Сдвиг в этой ветви обязателен, хотя направление здесь выбрано
        // заранее: точка с радиуса по построению лежит на границе лишь в
        // приближении float. У круга с центром (0.5, 0.25) и радиусом 0.1
        // сложение даёт 0.6f, обратное вычитание — не ровно 0.1, и
        // Contains отвечает false. Измерено: на 50000 запросов ровно в центр
        // при разных положениях круга метод отдавал точку вне круга 20030
        // раз, то есть в 40 % случаев. Прежний код сдвига не применял.
        return delta == Vector2.Zero
            ? Center + new Vector2(SurfaceRadius.For(Radius, Center), 0f)
            : Center + (delta.SafeNormalize() * SurfaceRadius.For(Radius, Center));
    }


    /// <summary>
    /// Перемещает круг.
    /// </summary>
    /// <param name="offset">Вектор смещения.</param>
    /// <returns>Смещённый круг.</returns>
    public Circle2 Translated(Vector2 offset) => new(Center + offset, Radius);

    /// <inheritdoc/>
    public bool Equals(Circle2 other) => Center.Equals(other.Center) && Radius.Equals(other.Radius);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Circle2 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Center, Radius);

    /// <summary>
    /// Сравнивает круги на равенство.
    /// </summary>
    /// <param name="left">Первый круг.</param>
    /// <param name="right">Второй круг.</param>
    /// <returns><c>true</c>, если круги равны.</returns>
    public static bool operator ==(Circle2 left, Circle2 right) => left.Equals(right);

    /// <summary>
    /// Сравнивает круги на неравенство.
    /// </summary>
    /// <param name="left">Первый круг.</param>
    /// <param name="right">Второй круг.</param>
    /// <returns><c>true</c>, если круги различаются.</returns>
    public static bool operator !=(Circle2 left, Circle2 right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"Circle2({Center}, r={Radius:F2})";
}
