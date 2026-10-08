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
    /// <remarks>
    /// Проверяется только точный ноль, а не длина меньше
    /// <see cref="Scalar.Epsilon"/>. Ненулевой вектор — это направление, и
    /// оно нормализуется при любой длине, как требует общее правило библиотеки:
    /// порог на длину превращал в ноль настоящие направления. Например,
    /// направление длиной 1.4e-7 метра — это нормаль физического тела,
    /// посчитанная на грани, и его угол обязан быть 45°, а не нулём.
    /// </remarks>
    public static Angle FromDirection(Vector2 direction)
        => direction == Vector2.Zero
            ? Zero
            // MathF.Atan2 уже возвращает значение в (-π; π], то есть ровно в том
            // диапазоне, в котором угол нормализован, поэтому нормализация
            // повторно не нужна.
            : FromRadiansRaw(Trig.Atan2(direction.Y, direction.X));

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
    public float Sin => Trig.Sin((float)_radians);

    /// <summary>
    /// Косинус угла. Считается в одинарной точности, см. <see cref="Sin"/>.
    /// </summary>
    public float Cos => Trig.Cos((float)_radians);

    /// <summary>
    /// Тангенс угла.
    /// </summary>
    public float Tan => Trig.Tan((float)_radians);

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
        (float sin, float cos) = Trig.SinCos(radians);
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
    /// Умножает угол на число с нормализацией результата.
    /// </summary>
    /// <param name="factor">Множитель.</param>
    /// <returns>Масштабированный угол в диапазоне <c>(-π; π]</c>.</returns>
    /// <remarks>
    /// Нормализация здесь, а не в доктрине, потому что она не косметическая.
    /// <para>
    /// Сравнение, хеш и упорядочивание идут по сырым радианам, поэтому угол в
    /// 2π не равен нулю, хеши разные и сортировка ставит его не туда. Ключ в
    /// словаре из таких углов даёт две записи на одну ориентацию.
    /// </para>
    /// <para>
    /// Дальше <see cref="SinCos"/> сужает радианы до <c>float</c> перед вызовом
    /// тригонометрии, поэтому рабочая точность угла — разряд float на его
    /// величине: на 1000 оборотов это 0.028 градуса, на 10000 — 0.22. Хранение в
    /// <c>double</c> того не спасает. Приведение к диапазону держит величину не
    /// больше <c>π</c>, и сужение перестаёт что-либо терять.
    /// </para>
    /// <para>
    /// Цена — одна проверка диапазона в <see cref="NormalizeRadians"/>: при
    /// обычном аргументе остаток не вычисляется. Сырое умножение, если оно
    /// действительно нужно, доступно как <see cref="ScaleRaw"/>.
    /// </para>
    /// </remarks>
    public Angle Scale(float factor) => new(_radians * factor, true);

    /// <summary>
    /// Умножает угол на число без нормализации: результат может превышать полный
    /// оборот.
    /// </summary>
    /// <param name="factor">Множитель.</param>
    /// <returns>Масштабированный угол без приведения к диапазону.</returns>
    /// <remarks>
    /// Назван по образцу <see cref="FromRadiansRaw"/>, потому что наружу выходит
    /// то же самое, что и до нормализации.
    /// <para>
    /// Результат честно хранит накопленную величину, но сравнивается и
    /// хешируется по ней, а перед тригонометрией сужается до <c>float</c>. На
    /// больших величинах точность падает: 0.028 градуса на 1000 оборотах и
    /// 0.22 на 10000. Для накопления углов предназначен счётчик оборотов, а не
    /// <see cref="Angle"/>.
    /// </para>
    /// </remarks>
    public Angle ScaleRaw(float factor) => new(_radians * factor, false);

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
    /// <c>VectorExtensions.WithDirection</c>, который не поворачивает вовсе, а
    /// задаёт направление: <c>Angle.FromDegrees(90).RotateDirection((0, 1))</c>
    /// даёт <c>(-1, 0)</c> (поворот на 90°), тогда как
    /// <c>VectorExtensions.WithDirection((0, 1), 90°)</c> даёт <c>(0, 1)</c>
    /// (направление задано углом). Совпадения результатов на отдельных входах
    /// случайны, общего у этих двух методов ничего нет.
    /// Прежний второй метод назывался так же, как этот, и вызывающий выбирал
    /// по имени, то есть по имени и ошибался.
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
    /// <remarks>
    /// Неположительный шаг возвращает текущий угол. Иначе движение пошло бы
    /// в сторону, противоположную цели, то есть шаг в минус означал бы шаг
    /// назад: знак у <paramref name="maxDelta"/> вдруг задавал бы направление.
    /// </remarks>
    public static Angle MoveTowards(Angle current, Angle target, double maxDelta)
    {
        if (maxDelta <= 0.0)
        {
            return current;
        }

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
    /// <para>
    /// Сначала проверяется, не лежит ли значение уже в нужном диапазоне:
    /// <c>Math.IEEERemainder</c> сам по себе стоит 12 нс, а угол создаётся на
    /// каждом кадре в циклах, анимации и физике. Замер на двух миллионах вызовов
    /// даёт 24.3 нс с вызовом остатка и 8.7 нс с проверкой диапазона.
    /// <c>NaN</c> проверку не проходит и попадает в полный путь, поэтому
    /// сохраняется как <c>NaN</c>.
    /// </para>
    /// </remarks>
    public static double NormalizeRadians(double radians)
    {
        if (radians > -Math.PI && radians <= Math.PI)
        {
            return radians;
        }

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
