using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Угол с нормализацией и интерполяцией по кратчайшей дуге.
/// Хранится в радианах типа <see cref="double"/>, чтобы длинная сессия не накапливала ошибку.
/// Экземпляр всегда нормализован в диапазон <c>(-π, π]</c>, кроме результатов
/// операций масштабирования (см. <see cref="Scale"/>).
/// </summary>
public readonly struct Angle : IEquatable<Angle>, IComparable<Angle>
{
    /// <summary>
    /// Полный оборот в радианах.
    /// </summary>
    public const double Tau = Math.PI * 2.0;

    /// <summary>
    /// Прямой угол в радианах.
    /// </summary>
    public const double HalfPi = Math.PI / 2.0;

    private readonly double _radians;

    private Angle(double radians, bool normalized)
    {
        _radians = normalized ? NormalizeRadians(radians) : radians;
    }

    /// <summary>
    /// Создаёт угол из значения в радианах с нормализацией.
    /// </summary>
    /// <param name="radians">Угол в радианах.</param>
    /// <returns>Нормализованный угол.</returns>
    public static Angle FromRadians(double radians) => new(radians, true);

    /// <summary>
    /// Создаёт угол из значения в радианах без нормализации.
    /// Используется для углов, кратных полному обороту, и результатов арифметики.
    /// </summary>
    /// <param name="radians">Угол в радианах.</param>
    /// <returns>Угол с исходным значением.</returns>
    public static Angle FromRadiansRaw(double radians) => new(radians, false);

    /// <summary>
    /// Создаёт угол из значения в градусах с нормализацией.
    /// </summary>
    /// <param name="degrees">Угол в градусах.</param>
    /// <returns>Нормализованный угол.</returns>
    public static Angle FromDegrees(double degrees) => FromRadians(Scalar.ToRadians(degrees));

    /// <summary>
    /// Создаёт угол из числа полных оборотов.
    /// </summary>
    /// <param name="turns">Число оборотов.</param>
    /// <returns>Нормализованный угол.</returns>
    public static Angle FromTurns(double turns) => FromRadians(turns * Tau);

    /// <summary>
    /// Создаёт угол, соответствующий направлению вектора.
    /// Нулевой вектор даёт нулевой угол.
    /// </summary>
    /// <param name="direction">Направляющий вектор.</param>
    /// <returns>Угол направления.</returns>
    public static Angle FromDirection(Vector2 direction)
        => MathF.Abs(direction.X) < Scalar.Epsilon && MathF.Abs(direction.Y) < Scalar.Epsilon
            ? Zero
            // MathF.Atan2 уже возвращает значение в (-π; π], то есть ровно в том
            // диапазоне, в котором угол нормализован, поэтому нормализация
            // повторно не нужна.
            : FromRadiansRaw(MathF.Atan2(direction.Y, direction.X));

    /// <summary>
    /// Нулевой угол.
    /// </summary>
    public static Angle Zero { get; } = new(0.0, true);

    /// <summary>
    /// Угол в радианах. Нормализован, если угол получен из нормализованного источника.
    /// </summary>
    public double Radians => _radians;

    /// <summary>
    /// Угол в градусах.
    /// </summary>
    public double Degrees => Scalar.ToDegrees(_radians);

    /// <summary>
    /// Угол в оборотах.
    /// </summary>
    public double Turns => _radians / Tau;

    /// <summary>
    /// Синус угла.
    /// </summary>
    /// <remarks>
    /// Считается в одинарной точности, как и сам возвращаемый тип: вычисление в
    /// double с последующим приведением к float не даёт выигрыша в точности
    /// (округление double до float не искажает результат), но стоит заметно
    /// дороже. Угол приводится к float один раз, поэтому <see cref="SinCos"/> и
    /// по отдельности <see cref="Sin"/> с <see cref="Cos"/> дают одно значение.
    /// <para>
    /// Результат зависит от операционной системы и архитектуры: MathF — это
    /// вызов математической библиотеки платформы. Для воспроизводимости
    /// физики по сети и записи в детерминированные снимки кадра это учитывать
    /// отдельно.
    /// </para>
    /// </remarks>
    public float Sin => MathF.Sin((float)_radians);

    /// <summary>
    /// Косинус угла. Считается в одинарной точности, см. <see cref="Sin"/>.
    /// </summary>
    public float Cos => MathF.Cos((float)_radians);

    /// <summary>
    /// Тангенс угла.
    /// </summary>
    public float Tan => MathF.Tan((float)_radians);

    /// <summary>
    /// Единичный вектор направления угла.
    /// </summary>
    /// <remarks>
    /// Синус и косинус берутся одним вызовом <see cref="MathF.SinCos"/>:
    /// два отдельных вызова математической библиотеки вдвое дороже, а
    /// <see cref="Rotate"/> на каждом угле делает именно два.
    /// </remarks>
    public Vector2 Direction
    {
        get
        {
            (float sin, float cos) = SinCos();
            return new Vector2(cos, sin);
        }
    }

    /// <summary>
    /// Возвращает синус и косинус угла одним вызовом математической библиотеки.
    /// </summary>
    /// <returns>Синус и косинус угла.</returns>
    internal (float Sin, float Cos) SinCos()
    {
        float radians = (float)_radians;
        (float sin, float cos) = MathF.SinCos(radians);
        return (sin, cos);
    }

    /// <summary>
    /// Приводит угол к диапазону <c>[-π, π]</c>.
    /// </summary>
    /// <returns>Нормализованный угол.</returns>
    public Angle Normalized() => new(_radians, true);

    /// <summary>
    /// Прибавляет угол.
    /// </summary>
    /// <param name="other">Прибавляемый угол.</param>
    /// <returns>Сумма углов.</returns>
    public Angle Add(Angle other) => new(_radians + other._radians, true);

    /// <summary>
    /// Вычитает угол.
    /// </summary>
    /// <param name="other">Вычитаемый угол.</param>
    /// <returns>Разность углов.</returns>
    public Angle Subtract(Angle other) => new(_radians - other._radians, true);

    /// <summary>
    /// Масштабирует угол без нормализации: результат может превышать полный оборот.
    /// </summary>
    /// <param name="factor">Множитель.</param>
    /// <returns>Масштабированный угол.</returns>
    public Angle Scale(float factor) => new(_radians * factor, false);

    /// <summary>
    /// Отрицает угол.
    /// </summary>
    /// <returns>Угол с противоположным знаком.</returns>
    public Angle Negated() => new(-_radians, true);

    /// <summary>
    /// Поворачивает вектор на этот угол против часовой стрелки.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <returns>Повёрнутый вектор.</returns>
    public Vector2 Rotate(Vector2 vector)
    {
        (float sin, float cos) = SinCos();
        return new Vector2(
            vector.X * cos - vector.Y * sin,
            vector.X * sin + vector.Y * cos);
    }

    /// <summary>
    /// Поворачивает направление вектора на этот угол против часовой стрелки,
    /// отбрасывая его длину: результат всегда единичный.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <returns>Единичный вектор, повёрнутый на угол.</returns>
    /// <remarks>
    /// Не путать с <see cref="Rotate"/>, который сохраняет длину, и с
    /// <c>VectorExtensions.RotateDirection</c>, который вообще не поворачивает
    /// направление, а задаёт его: <c>Angle.FromDegrees(90).RotateDirection((0, 1))</c>
    /// даёт <c>(-1, 0)</c> (поворот на 90°), тогда как
    /// <c>VectorExtensions.RotateDirection((0, 1), 90°)</c> даёт <c>(0, 1)</c>
    /// (направление задано углом). Совпадения результатов на отдельных входах
    /// случайны, общего у этих двух методов ничего нет.
    /// </remarks>
    public Vector2 RotateDirection(Vector2 vector) => Rotate(vector.SafeNormalize());

    /// <summary>
    /// Возвращает угол наименьшего поворота от <paramref name="from"/> к <paramref name="to"/>.
    /// </summary>
    /// <param name="from">Начальный угол.</param>
    /// <param name="to">Конечный угол.</param>
    /// <returns>Значение в диапазоне <c>[-π, π]</c>.</returns>
    public static double ShortestDelta(Angle from, Angle to) => NormalizeRadians(to._radians - from._radians);

    /// <summary>
    /// Интерполирует между углами по кратчайшей дуге.
    /// </summary>
    /// <param name="from">Начальный угол.</param>
    /// <param name="to">Конечный угол.</param>
    /// <param name="t">Параметр интерполяции, где 0 — <paramref name="from"/>, 1 — <paramref name="to"/>.</param>
    /// <returns>Интерполированный угол.</returns>
    public static Angle Lerp(Angle from, Angle to, float t) => from.Add(Angle.FromRadiansRaw(ShortestDelta(from, to) * t));

    /// <summary>
    /// Вычисляет угол между двумя направлениями в диапазоне <c>[-π, π]</c>.
    /// </summary>
    /// <param name="a">Первый угол.</param>
    /// <param name="b">Второй угол.</param>
    /// <returns>Угол между направлениями.</returns>
    public static double Between(Angle a, Angle b) => ShortestDelta(a, b);

    /// <summary>
    /// Плавно приближает угол к целевому, не превышая заданный шаг за вызов.
    /// </summary>
    /// <param name="current">Текущий угол.</param>
    /// <param name="target">Целевой угол.</param>
    /// <param name="maxDelta">Максимальный шаг в радианах.</param>
    /// <returns>Новый угол.</returns>
    public static Angle MoveTowards(Angle current, Angle target, double maxDelta)
    {
        double delta = ShortestDelta(current, target);
        return Math.Abs(delta) <= maxDelta ? target : current.Add(Angle.FromRadiansRaw(Math.Sign(delta) * maxDelta));
    }

    /// <summary>
    /// Нормализует значение в радианах в диапазон <c>(-π, π]</c>: минус пи
    /// приводится к плюс пи.
    /// </summary>
    /// <param name="radians">Исходное значение в радианах.</param>
    /// <returns>Нормализованное значение.</returns>
    /// <remarks>
    /// Границы именно такие, а не <c>[-π, π]</c>: <c>Math.IEEERemainder</c>
    /// возвращает <c>[-π, π]</c>, и следующая строка сдвигает левую границу
    /// внутрь. Из этого следует, что <c>FromRadians(0) != FromRadians(Tau)</c>
    /// (угол всегда нормализован), и что равенство и <see cref="GetHashCode"/>
    /// считаются по нормализованным радианам, а не по исходным.
    /// </remarks>
    public static double NormalizeRadians(double radians)
    {
        double wrapped = Math.IEEERemainder(radians, Tau);
        if (wrapped <= -Math.PI)
        {
            wrapped += Tau;
        }

        return wrapped;
    }

    /// <inheritdoc/>
    public bool Equals(Angle other) => _radians.Equals(other._radians);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Angle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _radians.GetHashCode();

    /// <inheritdoc/>
    public int CompareTo(Angle other) => _radians.CompareTo(other._radians);

    /// <summary>
    /// Сравнивает углы на равенство.
    /// </summary>
    /// <param name="left">Первый угол.</param>
    /// <param name="right">Второй угол.</param>
    /// <returns><c>true</c>, если углы равны.</returns>
    public static bool operator ==(Angle left, Angle right) => left.Equals(right);

    /// <summary>
    /// Сравнивает углы на неравенство.
    /// </summary>
    /// <param name="left">Первый угол.</param>
    /// <param name="right">Второй угол.</param>
    /// <returns><c>true</c>, если углы различаются.</returns>
    public static bool operator !=(Angle left, Angle right) => !left.Equals(right);

    /// <summary>
    /// Складывает углы.
    /// </summary>
    /// <param name="left">Первый угол.</param>
    /// <param name="right">Второй угол.</param>
    /// <returns>Сумма углов.</returns>
    public static Angle operator +(Angle left, Angle right) => left.Add(right);

    /// <summary>
    /// Вычитает углы.
    /// </summary>
    /// <param name="left">Первый угол.</param>
    /// <param name="right">Второй угол.</param>
    /// <returns>Разность углов.</returns>
    public static Angle operator -(Angle left, Angle right) => left.Subtract(right);

    /// <summary>
    /// Отрицает угол.
    /// </summary>
    /// <param name="value">Угол.</param>
    /// <returns>Противоположный угол.</returns>
    public static Angle operator -(Angle value) => value.Negated();

    /// <summary>
    /// Масштабирует угол.
    /// </summary>
    /// <param name="value">Угол.</param>
    /// <param name="factor">Множитель.</param>
    /// <returns>Масштабированный угол.</returns>
    public static Angle operator *(Angle value, float factor) => value.Scale(factor);

    /// <summary>
    /// Делит угол на число.
    /// </summary>
    /// <param name="value">Угол.</param>
    /// <param name="divisor">Делитель.</param>
    /// <returns>Результат деления.</returns>
    public static Angle operator /(Angle value, float divisor) => new(value._radians / divisor, true);

    /// <summary>
    /// Сравнивает углы: меньше, если радианы меньше.
    /// </summary>
    /// <param name="left">Первый угол.</param>
    /// <param name="right">Второй угол.</param>
    /// <returns>Результат сравнения.</returns>
    public static bool operator <(Angle left, Angle right) => left._radians < right._radians;

    /// <summary>
    /// Сравнивает углы: больше, если радианы больше.
    /// </summary>
    /// <param name="left">Первый угол.</param>
    /// <param name="right">Второй угол.</param>
    /// <returns>Результат сравнения.</returns>
    public static bool operator >(Angle left, Angle right) => left._radians > right._radians;

    /// <summary>
    /// Сравнивает углы: меньше или равно.
    /// </summary>
    /// <param name="left">Первый угол.</param>
    /// <param name="right">Второй угол.</param>
    /// <returns>Результат сравнения.</returns>
    public static bool operator <=(Angle left, Angle right) => left._radians <= right._radians;

    /// <summary>
    /// Сравнивает углы: больше или равно.
    /// </summary>
    /// <param name="left">Первый угол.</param>
    /// <param name="right">Второй угол.</param>
    /// <returns>Результат сравнения.</returns>
    public static bool operator >=(Angle left, Angle right) => left._radians >= right._radians;

    /// <inheritdoc/>
    public override string ToString() => $"{Degrees:F2} deg";
}
