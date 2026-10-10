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
    /// Порог — точный ноль, а не <c>float.Epsilon</c> и не
    /// <c>Scalar.Epsilon</c>. Длина считается как <c>sqrt(сумма квадратов)</c>,
    /// а во float квадрат обнуляется при длине меньше 3.743392e-23 и
    /// переполняется при длине больше 1.8446744e19. Прежний порог
    /// <c>lengthSquared &lt;= float.Epsilon</c> отвергал ненулевые нормали
    /// короче 3.743392e-23, причём по величине квадрата, а не по самой
    /// нормали. Ненулевой вектор — это направление, и он задаёт то же
    /// направление при любой длине.
    /// </para>
    /// <para>
    /// За пределами надёжного диапазона длина не вычислима вовсе, поэтому
    /// нормаль приводится к масштабу наибольшей компонентой: там её наибольшая
    /// компонента равна единице, квадрат лежит в [1; 3], и длина получается при
    /// любой длине исходной нормали. Измерено на прежнем коде: при длине
    /// нормали 1e-22 длина результата расходилась с единицей на 9.06 % на
    /// 20000 нормалей подряд, а при длине 1.8446745e19 и выше квадрат
    /// переполнялся и конструктор молча отдавал нулевую нормаль, тогда как
    /// <see cref="FromCoefficients"/> на тех же входах бросал
    /// <see cref="ArgumentException"/>.
    /// </para>
    /// </remarks>
    public Plane3(Vector3 normal, float distance)
    {
        // Обычный случай: квадрат длины представим во float, то есть длина
        // вычислима и результат совпадает с прежним побитово.
        float lengthSquared = normal.LengthSquared();
        if (lengthSquared >= Vector3Extensions.MinNormalizableLengthSquared
            && lengthSquared <= Vector3Extensions.MaxNormalizableLengthSquared)
        {
            float scale = 1f / MathF.Sqrt(lengthSquared);
            Normal = normal * scale;
            Distance = distance * scale;
            return;
        }

        float magnitude = MathF.Max(
            MathF.Abs(normal.X),
            MathF.Max(MathF.Abs(normal.Y), MathF.Abs(normal.Z)));
        if (magnitude == 0f)
        {
            throw new ArgumentException("Нормаль плоскости должна быть ненулевой.", nameof(normal));
        }

        Vector3 scaled = normal / magnitude;
        float scaledLength = scaled.Length();
        Normal = scaled / scaledLength;

        // Свободный член делится на ту же длину, что и нормаль, и делится в том
        // же порядке: сначала на масштаб наибольшей компоненты, потом на длину
        // уже масштабированного вектора. Обратный порядок переполнил бы 1/|n|
        // там, где результат представим, а при нулевом свободном члене дал
        // 0, умноженное на бесконечность, то есть нечисловое расстояние.
        Distance = (distance / scaledLength) / magnitude;
    }

    /// <summary>
    /// Создаёт плоскость из нормали, которая уже единичная: длина не
    /// вычисляется и повторно не применяется.
    /// </summary>
    /// <param name="unitNormal">Нормаль единичной длины.</param>
    /// <param name="distance">Свободный член уравнения.</param>
    /// <param name="normalIsUnit">
    /// Всегда <c>true</c>. Параметр нужен только чтобы отличить этот
    /// конструктор от публичного при одинаковом наборе типов.
    /// </param>
    /// <remarks>
    /// Отдельный конструктор требуется потому, что повторная нормализация
    /// единичной нормали не даёт её самой. Величина <c>1/√(n·n)</c> у единичного
    /// вектора равна единице лишь приближённо, и на этом шаге плоскость из
    /// <see cref="FromPointNormal"/> расходилась с плоскостью из конструктора
    /// на те же коэффициенты уравнения.
    /// </remarks>
    private Plane3(Vector3 unitNormal, float distance, bool normalIsUnit)
    {
        _ = normalIsUnit;
        Normal = unitNormal;
        Distance = distance;
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
    /// Порог проверки — точный ноль нормали, по той же причине и с тем же
    /// обоснованием, что и в конструкторе: длина считается в масштабе
    /// наибольшей компонентой и потому вычислима при любой ненулевой нормали.
    /// </remarks>
    public static Plane3 FromPointNormal(Vector3 point, Vector3 normal)
    {
        // Обычный случай: квадрат длины представим во float.
        float lengthSquared = normal.LengthSquared();
        if (lengthSquared >= Vector3Extensions.MinNormalizableLengthSquared
            && lengthSquared <= Vector3Extensions.MaxNormalizableLengthSquared)
        {
            Vector3 scaled = normal * (1f / MathF.Sqrt(lengthSquared));
            return FromUnitNormal(scaled, -Vector3.Dot(scaled, point));
        }

        float magnitude = MathF.Max(
            MathF.Abs(normal.X),
            MathF.Max(MathF.Abs(normal.Y), MathF.Abs(normal.Z)));
        if (magnitude == 0f)
        {
            throw new ArgumentException("Нормаль плоскости должна быть ненулевой.", nameof(normal));
        }

        Vector3 unit = normal / magnitude;
        unit /= unit.Length();
        return FromUnitNormal(unit, -Vector3.Dot(unit, point));
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
    /// Результат побитово совпадает с <see cref="Plane3(Vector3, float)"/> на
    /// тех же коэффициентах: фабричный метод не считает длину сам, а отдаёт
    /// коэффициенты конструктору. Прежде длина считалась здесь через
    /// <c>normal.Length()</c>, там — через <c>1f/MathF.Sqrt(lengthSquared)</c>,
    /// и на 200000 парах коэффициентов из четырёх результаты расходились в
    /// 74 % случаев (совпали 51 663). Расхождение в один ULP ломает
    /// <c>==</c> и разводит <see cref="GetHashCode"/>: одна и та же плоскость
    /// получалась разными значениями в хеш-контейнере и в сравнении.
    /// </para>
    /// </remarks>
    public static Plane3 FromCoefficients(Vector3 normal, float offset) => new(normal, offset);

    /// <summary>
    /// Собирает плоскость из уже единичной нормали и готового свободного члена.
    /// </summary>
    /// <param name="unitNormal">Нормаль единичной длины.</param>
    /// <param name="distance">Свободный член уравнения.</param>
    /// <returns>Плоскость.</returns>
    private static Plane3 FromUnitNormal(Vector3 unitNormal, float distance)
        => new(unitNormal, distance, true);

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