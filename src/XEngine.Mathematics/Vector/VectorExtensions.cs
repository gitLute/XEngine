using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Операции над векторами, которых нет в <see cref="Vector2"/>.
/// Соглашения: поворот против часовой стрелки, длина в тех же единицах, что и мир.
/// </summary>
public static class VectorExtensions
{
    /// <summary>
    /// Нижняя граница квадрата длины, до которой сумма квадратов считается
    /// в нормальных числах float.
    /// </summary>
    /// <remarks>
    /// Выбрана с запасом относительно точки обнуления
    /// <c>sqrt(float.Epsilon)</c> = 3.743392e-23, то есть квадрата 1.4e-45:
    /// при квадрате 1e-30 квадраты отдельных компонент лежат на 1e-31 и выше,
    /// то есть ещё нормальные, и сумма не теряет разряды. Соответствующая
    /// длина — 1e-15.
    /// </remarks>
    private const float MinNormalizableLengthSquared = 1e-30f;

    /// <summary>
    /// Верхняя граница квадрата длины, до которой сумма квадратов не
    /// переполняется.
    /// </summary>
    /// <remarks>
    /// <c>float.MaxValue</c> равен 3.4028235e38, поэтому квадрат 3e38 ещё
    /// конечен, а соответствующая длина 1.7320509e19 помещается в float со
    /// всем числом разрядов. Всё, что длиннее, считается масштабированием.
    /// </remarks>
    private const float MaxNormalizableLengthSquared = 3e38f;

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
    /// Метод не поворачивает, а задаёт направление, поэтому и назван
    /// <c>WithDirection</c>. Прежнее имя совпадало с
    /// <see cref="Angle.RotateDirection"/>, который действительно поворачивает,
    /// и вызывающий выбирал по имени: <c>RotateDirection((0, 1), 90°)</c>
    /// возвращает <c>(0, 1)</c>, а не <c>(-1, 0)</c>.
    /// <para>
    /// Отличать от <see cref="Rotate"/>, который поворачивает вектор целиком и
    /// сохраняет длину, и от <see cref="Angle.RotateDirection"/>, который
    /// поворачивает направление и теряет длину. Совпадения результатов на
    /// отдельных входах случайны.
    /// </para>
    /// </remarks>
    public static Vector2 WithDirection(this Vector2 vector, Angle angle)
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
    /// <see cref="Vector3Extensions.SafeNormalize"/>. Там же разобрано, почему
    /// длину нельзя считать как <c>sqrt(сумма квадратов)</c> без оговорок и
    /// чем это грозит на обоих концах диапазона float.
    /// </remarks>
    public static Vector2 SafeNormalize(this Vector2 vector)
    {
        float lengthSquared = vector.LengthSquared();
        if (lengthSquared >= MinNormalizableLengthSquared && lengthSquared <= MaxNormalizableLengthSquared)
        {
            return vector / MathF.Sqrt(lengthSquared);
        }

        float scale = MathF.Max(MathF.Abs(vector.X), MathF.Abs(vector.Y));
        if (scale == 0f)
        {
            return Vector2.Zero;
        }

        if (!float.IsFinite(scale))
        {
            return Vector2.Normalize(vector);
        }

        return Vector2.Normalize(vector / scale);
    }

    /// <summary>
    /// Ограничивает длину вектора сверху.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <param name="maxLength">Максимальная длина, неотрицательная.</param>
    /// <returns>Вектор, длина которого не превышает <paramref name="maxLength"/>.</returns>
    /// <remarks>
    /// Отрицательный предел отвергается, а не используется как есть. Без
    /// проверки квадрат предела оставался положительным, поэтому короткий
    /// вектор возвращался как есть, а длинный нормализовался и умножался на
    /// отрицательное число, то есть разворачивался: длина соблюдалась,
    /// направление переворачивалось.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Предел отрицательный.</exception>
    public static Vector2 ClampLength(this Vector2 vector, float maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);

        // Обычный случай: и квадрат длины, и квадрат предела представимы во
        // float, то есть сравнение квадратов равносильно сравнению длин.
        // Если квадрат предела переполнился, то предел больше 1.8446744e19,
        // а длина здесь меньше 1.7320509e19, то есть ограничение и не должно
        // было сработать; если обнулился — предел меньше 1.057e-19, а длина
        // здесь больше 1e-15, то есть ограничение должно сработать. Оба
        // случая дают верный ответ, поэтому отдельно их проверять не нужно.
        float lengthSquared = vector.LengthSquared();
        if (lengthSquared >= MinNormalizableLengthSquared && lengthSquared <= MaxNormalizableLengthSquared)
        {
            return lengthSquared <= maxLength * maxLength ? vector : Vector2.Normalize(vector) * maxLength;
        }

        // Длина в масштабе наибольшей компоненты: квадрат исходного вектора
        // обнуляется и переполняется вместе с float, а здесь обе величины
        // лежат в надёжном диапазоне. Поэтому ограничение не зависит от того,
        // в каких единицах выбран мир.
        float scale = MathF.Max(MathF.Abs(vector.X), MathF.Abs(vector.Y));
        if (scale == 0f)
        {
            return vector;
        }

        Vector2 scaled = vector / scale;
        float scaledLengthSquared = scaled.LengthSquared();
        float ratio = maxLength / scale;
        return ratio * ratio >= scaledLengthSquared ? vector : scaled * (maxLength / MathF.Sqrt(scaledLengthSquared));
    }

    /// <summary>
    /// Проецирует вектор на направление.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <param name="direction">Направление проекции, должно быть ненулевым.</param>
    /// <returns>Проекция вектора на направление.</returns>
    /// <exception cref="ArgumentException">Направление нулевое.</exception>
    public static Vector2 Project(this Vector2 vector, Vector2 direction)
    {
        // Направление проекции — безразмерная величина, и сравнивать его с
        // Scalar.Epsilon неверно: порог с размерностью длины отбрасывал
        // настоящие направления, которые задают ту же прямую, что и единичные.
        // Исключение по подписи метода означает «направление нулевое», и теперь
        // так и есть.
        //
        // Вдобавок формула d·(v·d)/(d·d) переполняется и обнуляется вместе с
        // квадратом направления, поэтому она считается в масштабе наибольшей
        // компоненты: там наибольшая компонента равна единице, квадрат лежит в
        // [1; 2], и результат не зависит от длины направления вовсе.
        // Обычный случай: квадрат длины направления представим во float, и
        // формула считается как есть. Случай, когда квадрат превышает
        // MaxNormalizableLengthSquared, означает длину больше 1.7320509e19, то
        // есть переполнение квадрата, и формула без масштабирования вернёт ноль.
        float lengthSquared = direction.LengthSquared();
        if (lengthSquared >= MinNormalizableLengthSquared && lengthSquared <= MaxNormalizableLengthSquared)
        {
            return direction * (Vector2.Dot(vector, direction) / lengthSquared);
        }

        float scale = MathF.Max(MathF.Abs(direction.X), MathF.Abs(direction.Y));
        if (scale == 0f)
        {
            throw new ArgumentException("Направление проекции должно быть ненулевым.", nameof(direction));
        }

        Vector2 scaled = direction / scale;
        return scaled * (Vector2.Dot(vector, scaled) / scaled.LengthSquared());
    }

    /// <summary>
    /// Возвращает вектор движения к цели с ограничением длины шага.
    /// </summary>
    /// <param name="from">Текущая позиция.</param>
    /// <param name="to">Целевая позиция.</param>
    /// <param name="maxStep">Максимальная длина шага.</param>
    /// <returns>Вектор смещения, который не перескакивает цель.</returns>
    /// <remarks>
    /// Неположительный шаг даёт нулевой вектор смещения, то есть «не двигаться».
    /// Без проверки квадрат шага оставался положительным, проверка «не перескакиваем»
    /// проходила, а деление шло с отрицательным множителем и вектор разворачивался:
    /// <c>MoveTowards((0, 0), (10, 0), −5)</c> возвращал <c>(−5, −0)</c>, то есть
    /// движение шло от цели. Защита такая же, как в <c>ClampLength</c> и в
    /// <see cref="Angle.MoveTowards"/>, и по той же причине.
    /// <para>
    /// Метод возвращает смещение, а не позицию, поэтому «не двигаться» — это ноль.
    /// <c>NaN</c> проверку не проходит и уходит в вычисление, то есть даёт <c>NaN</c>
    /// в том числе на совпадающих точках: ошибка вызывающего не должна молчать.
    /// </para>
    /// </remarks>
    public static Vector2 MoveTowards(this Vector2 from, Vector2 to, float maxStep)
    {
        if (maxStep <= 0f)
        {
            return Vector2.Zero;
        }

        Vector2 delta = to - from;
        float lengthSquared = delta.LengthSquared();

        // Обычный случай: квадрат расстояния и квадрат шага оба представимы во
        // float, то есть сравнение квадратов равносильно сравнению длин, а
        // деление в знаменателе безопас��е. Проверки на оба квадрата вместе с
        // границами диапазона не требуются: если квадрат шага переполнился,
        // то шаг больше 1.8446744e19, а длина здесь меньше 1.7320509e19 и
        // ограничение не должно сработать; если обнулился — шаг меньше
        // 1.057e-19, а длина здесь больше 1e-15 и ограничение должно
        // сработать. Оба случая дают верный ответ.
        if (lengthSquared >= MinNormalizableLengthSquared && lengthSquared <= MaxNormalizableLengthSquared)
        {
            return lengthSquared <= maxStep * maxStep
                ? delta
                : delta * (maxStep / MathF.Sqrt(lengthSquared));
        }

        // Длина не представима во float: квадрат обнулился или переполнился.
        // Ограничение шага тогда считается в масштабе наибольшей компоненты
        // сдвига, где он лежит в [1; sqrt(2)] и оба квадрата конечны.
        float scale = MathF.Max(MathF.Abs(delta.X), MathF.Abs(delta.Y));
        if (scale == 0f)
        {
            // Совпадающие точки: смещения нет, и оно равно нулю. Шаг при этом
            // может быть и бесконечным (тогда это законное «без ограничения»,
            // и результат — ноль), и нечисловым (тогда ошибка вызывающего не
            // должна молчать, как в Angle.MoveTowards). Поэтому сравнение
            // идёт до умножения: Zero * бесконечность дало бы NaN.
            return maxStep > 0f ? delta : delta * maxStep;
        }

        Vector2 scaled = delta / scale;
        float scaledLength = scaled.Length();
        return scale * scaledLength <= maxStep ? delta : scaled * (maxStep / scaledLength);
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
    /// <param name="lineNormal">
    /// Нормаль прямой любой длины, направленная в сторону положительной стороны.
    /// </param>
    /// <returns>
    /// Знаковое расстояние в единицах, в которых длина нормали равна единице, то
    /// есть расстояние, умноженное на <c>length(lineNormal)</c>.
    /// </returns>
    /// <remarks>
    /// Имя выбрано по смыслу, который был у метода на самом деле. Прежнее имя
    /// обещало расстояние, а возвращало скалярное произведение: нормаль (3, 0)
    /// давала 3 вместо 1, то есть величина отличалась от расстояния в
    /// <c>length(lineNormal)</c> раз. Это ровно тот класс, который библиотека
    /// уже признала и исправила: <c>RotateDirection</c> был переименован в
    /// <c>WithDirection</c>, потому что имя обещало больше, чем делал, и вызывающий
    /// выбирал по имени.
    /// <para>
    /// Нормализовать нормаль здесь означало бы молча изменить результат у всех
    /// существующих вызовов в <c>length(lineNormal)</c> раз, а возвращаемый тип
    /// <c>float</c> этого не показывает. Поэтому остаются два метода: этот,
    /// дешёвый и точный для своего контракта, и
    /// <see cref="SignedDistanceToLine"/>, возвращающий настоящее расстояние.
    /// </para>
    /// <para>
    /// Метод уместен там, где нормали уже единичной длины: у нормали плоскости,
    /// заданной углом, или у нормали, полученной как <c>line / |line|</c>. Тогда
    /// результат совпадает с расстоянием, и нормировку делать не нужно.
    /// </para>
    /// </remarks>
    public static float SignedLineOffset(this Vector2 point, Vector2 lineOrigin, Vector2 lineNormal)
        => Vector2.Dot(point - lineOrigin, lineNormal);

    /// <summary>
    /// Возвращает знаковое расстояние от точки до прямой в единицах длины.
    /// </summary>
    /// <param name="point">Проверяемая точка.</param>
    /// <param name="lineOrigin">Точка на прямой.</param>
    /// <param name="lineNormal">Нормаль прямой любой длины, нормализуется внутри.</param>
    /// <returns>Знаковое расстояние до прямой.</returns>
    /// <exception cref="ArgumentException">Нормаль нулевая.</exception>
    /// <remarks>
    /// Нормаль нормализуется, поэтому результат не зависит от того, единичная
    /// она или нет: нормаль (3, 0) даёт 1, а не 3. Знак совпадает со знаком
    /// скалярного произведения, то есть положительная сторона задаётся самой
    /// нормалью.
    /// <para>
    /// Цена — одно вычисление длины и деление на каждом вызове. Если нормали
    /// единичные, а это обычный случай у плоскостей, заданных углом, то
    /// <see cref="SignedLineOffset"/> даёт тот же ответ дешевле, и разница между
    /// методами становится чисто договорённостью о том, кто отвечает за длину
    /// нормали.
    /// </para>
    /// </remarks>
    public static float SignedDistanceToLine(this Vector2 point, Vector2 lineOrigin, Vector2 lineNormal)
    {
        Vector2 unit = lineNormal.SafeNormalize();
        if (unit == Vector2.Zero)
        {
            throw new ArgumentException("Нормаль прямой должна быть ненулевой.", nameof(lineNormal));
        }

        return Vector2.Dot(point - lineOrigin, unit);
    }

    /// <summary>
    /// Проверяет, лежит ли значение вектора в допуске от нуля по обеим компонентам.
    /// </summary>
    /// <param name="vector">Проверяемый вектор.</param>
    /// <param name="epsilon">Допуск.</param>
    /// <returns><c>true</c>, если вектор близок к нулю.</returns>
    public static bool IsNearlyZero(this Vector2 vector, float epsilon = Scalar.Epsilon)
        => MathF.Abs(vector.X) <= epsilon && MathF.Abs(vector.Y) <= epsilon;
}
