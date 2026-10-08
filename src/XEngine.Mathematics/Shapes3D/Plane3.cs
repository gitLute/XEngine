using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Плоскость в трёх измерениях: уравнение <c>dot(Normal, X) + D = 0</c>,
/// нормаль единичная.
/// </summary>
/// <remarks>
/// Собственный тип вместо <see cref="Plane"/> из BCL: у того порядок
/// коэффициентов другой, и смешение двух соглашений в одном проекте даёт
/// знаковые ошибки, которые видны только в отсечении. Пересечение луча с
/// плоскостью считается на <see cref="Ray3"/> вместе с остальными
/// пересечениями луча: так у одной операции один контракт (инвариант 12).
/// </remarks>
public readonly struct Plane3 : IEquatable<Plane3>
{
    /// <summary>
    /// Создаёт плоскость по уравнению <c>a·X + b = 0</c>. Нормаль должна быть
    /// ненулевой.
    /// </summary>
    /// <param name="normal">Коэффициенты при координатах, норма не обязана быть единичной.</param>
    /// <param name="distance">Свободный член уравнения.</param>
    /// <exception cref="ArgumentException">Нормаль нулевая.</exception>
    /// <remarks>
    /// Свободный член делится на ту же длину, что и нормаль, иначе плоскость
    /// сдвигается по глубине: из <c>2x + 2y + 4 = 0</c> получалось
    /// <c>0.707x + 0.707y + 4 = 0</c>, то есть другая плоскость. Это ровно то,
    /// что описано в <see cref="FromCoefficients"/>, и конструктор обязан вести
    /// себя так же: он принимает те же коэффициенты уравнения.
    ///
    /// <para>
    /// Порог — <c>float.Epsilon</c>, а не <c>Scalar.Epsilon</c>, и это
    /// сознательно. <c>float.Epsilon</c> — наименьшее положительное
    /// нормализованное число, поэтому проверяется, что квадрат длины не
    /// обнулился: у нормали длиной 1e-20 квадрат равен 1e-40 и представим, а у
    /// 1e-30 квадрат 1e-60 обнуляется. Порог <c>Scalar.Epsilon</c> на квадрате
    /// длины отверг бы нормали короче миллиметра, то есть был бы слишком грубым.
    /// Единый порог на квадратах величин здесь означал бы отказ на корректных
    /// нормалях, то есть обмен одного дефекта на другой.
    /// </para>
    /// </remarks>
    public Plane3(Vector3 normal, float distance)
    {
        float lengthSquared = normal.LengthSquared();
        if (lengthSquared <= float.Epsilon)
        {
            throw new ArgumentException("Нормаль плоскости должна быть ненулевой.", nameof(normal));
        }

        float scale = 1f / MathF.Sqrt(lengthSquared);
        Normal = normal * scale;
        Distance = distance * scale;
    }

    /// <summary>
    /// Единичная нормаль плоскости.
    /// </summary>
    public Vector3 Normal { get; }

    /// <summary>
    /// Свободный член уравнения <c>dot(Normal, X) + Distance = 0</c>.
    /// </summary>
    public float Distance { get; }

    /// <summary>
    /// Создаёт плоскость по точке и нормали.
    /// </summary>
    /// <param name="point">Любая точка плоскости.</param>
    /// <param name="normal">Нормаль, задающая сторону; нормализуется внутри.</param>
    /// <returns>Плоскость.</returns>
    /// <exception cref="ArgumentException">Нормаль нулевая.</exception>
    /// <remarks>
    /// Порог проверки — <c>float.Epsilon</c> на квадрате длины, по той же
    /// причине и с тем же обоснованием, что и в конструкторе.
    /// </remarks>
    public static Plane3 FromPointNormal(Vector3 point, Vector3 normal)
    {
        float lengthSquared = normal.LengthSquared();
        if (lengthSquared <= float.Epsilon)
        {
            throw new ArgumentException("Нормаль плоскости должна быть ненулевой.", nameof(normal));
        }

        // Нормаль и свободный член делятся на одну и ту же длину: при
        // нормализации только нормали свободный член остался бы от другой
        // (ненормализованной) системы координат.
        float scale = 1f / MathF.Sqrt(lengthSquared);
        Vector3 unit = normal * scale;
        return new Plane3(unit, -Vector3.Dot(unit, point));
    }

    /// <summary>
    /// Создаёт плоскость по коэффициентам уравнения <c>a·X + b = 0</c>.
    /// </summary>
    /// <param name="normal">Коэффициенты при координатах, норма не обязана быть единичной.</param>
    /// <param name="offset">Свободный член уравнения.</param>
    /// <returns>Плоскость с единичной нормалью.</returns>
    /// <exception cref="ArgumentException">Норма нулевая.</exception>
    /// <remarks>
    /// Отдельно от <see cref="FromPointNormal"/>, потому что при нормализации
    /// свободный член обязан делиться на ту же длину, что и норма. Нормализация
    /// только нормали сдвигает плоскость по глубине, и ошибка не видна, пока
    /// не отсечёт не то: именно так терялись ближняя и дальняя плоскости
    /// пирамиды видимости.
    /// <para>
    /// Здесь порог сравнивается с самой длиной, а не с её квадратом, и потому
    /// равен <c>float.Epsilon</c>: длина 1e-30 ещё ненулевая и нормализуема,
    /// тогда как её квадрат обнулился бы. В конструкторе и в
    /// <see cref="FromPointNormal"/> сравнивается квадрат, и порог тот же по
    /// величине, но применён к другой степени. Оба выбора равноправны: важно,
    /// чтобы порог соответствовал тому, что обнуляется при округлении.
    /// </para>
    /// </remarks>
    public static Plane3 FromCoefficients(Vector3 normal, float offset)
    {
        // Длина считается один раз и сразу передаётся в конструктор, который
        // принимает уже нормализованную нормаль: иначе длина вычислялась бы
        // дважды, потому что конструктор нормализует нормаль сам.
        float length = normal.Length();
        if (length <= float.Epsilon)
        {
            throw new ArgumentException("Коэффициенты плоскости заданы ненулевым вектором.", nameof(normal));
        }

        return new Plane3(normal / length, offset / length);
    }

    /// <summary>
    /// Возвращает знаковое расстояние от точки до плоскости: положительное
    /// со стороны нормали, отрицательное с противоположной.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <returns>Расстояние в метрах.</returns>
    public float DistanceTo(Vector3 point) => Vector3.Dot(Normal, point) + Distance;

    /// <summary>
    /// Проецирует точку на плоскость по кратчайшему пути.
    /// </summary>
    /// <param name="point">Проецируемая точка.</param>
    /// <returns>Точка на плоскости.</returns>
    public Vector3 Project(Vector3 point) => point - Normal * DistanceTo(point);

    /// <inheritdoc/>
    public bool Equals(Plane3 other) => Normal.Equals(other.Normal) && Distance.Equals(other.Distance);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Plane3 other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Normal, Distance);

    /// <summary>
    /// Сравнивает плоскости на равенство.
    /// </summary>
    /// <param name="left">Первая плоскость.</param>
    /// <param name="right">Вторая плоскость.</param>
    /// <returns><c>true</c>, если плоскости равны.</returns>
    public static bool operator ==(Plane3 left, Plane3 right) => left.Equals(right);

    /// <summary>
    /// Сравнивает плоскости на неравенство.
    /// </summary>
    /// <param name="left">Первая плоскость.</param>
    /// <param name="right">Вторая плоскость.</param>
    /// <returns><c>true</c>, если плоскости различаются.</returns>
    public static bool operator !=(Plane3 left, Plane3 right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"Plane3(N={Normal}, D={Distance:F4})";
}