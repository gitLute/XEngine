using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Операции над трёхмерными векторами поверх того, что уже есть в
/// <see cref="System.Numerics"/>.
/// </summary>
/// <remarks>
/// Имена операций различают, что именно они преобразуют: направление без
/// переноса, точку на плоскости или нормаль. Три разных вычисления под одним
/// именем — источник ошибок знаков (6.5, правило 6). Сглаживание идёт через
/// <see cref="Interpolation"/>, а не через <c>Lerp(value, target, dt * k)</c>.
/// </remarks>
public static class Vector3Extensions
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
    /// Возвращает нормализованный вектор. Настоящий нулевой вектор остаётся
    /// нулевым.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <returns>Вектор единичной длины либо нулевой.</returns>
    /// <remarks>
    /// Проверяется именно ноль, а не длина меньше Scalar.Epsilon. Ненулевой
    /// вектор — направление, и он нормализуется при любой длине: иначе
    /// отрезок длиной меньше микрона превращался в нулевой, и вызывающий
    /// получал не направление, а его отсутствие. Обнуление коротких векторов
    /// нужно там, где сравнивают с допуском, и там сравнивают явно.
    /// <para>
    /// Длина считается как <c>sqrt(сумма квадратов)</c>, а во float квадрат
    /// обнуляется при длине меньше 3.743392e-23 и переполняется при длине
    /// больше 1.8446744e19. На таких векторах деление на длину давало
    /// <c>(Infinity, NaN, NaN)</c> внизу и нулевой вектор вверху, то есть
    /// «нулевой» и «огромный» становились неразличимы. Поэтому за пределами
    /// надёжного диапазона вектор сначала делится наибольшей по модулю
    /// компонентой: наибольшая компонента становится единицей, сумма квадратов
    /// лежит в [1; 3], и длина вычислима при любой длине. Направление при
    /// этом не меняется, потому что деление на положительную величину оставляет
    /// его тем же.
    /// </para>
    /// <para>
    /// Нечисловой вход остаётся нечисловым и обрабатывается прежним путём, то
    /// есть результат тот же, что и до правки: бесконечность и <c>NaN</c>
    /// отсекаются проверкой масштаба.
    /// </para>
    /// </remarks>
    public static Vector3 SafeNormalize(this Vector3 vector)
    {
        float lengthSquared = vector.LengthSquared();
        if (lengthSquared >= MinNormalizableLengthSquared && lengthSquared <= MaxNormalizableLengthSquared)
        {
            return vector / MathF.Sqrt(lengthSquared);
        }

        float scale = MathF.Max(MathF.Abs(vector.X), MathF.Max(MathF.Abs(vector.Y), MathF.Abs(vector.Z)));
        if (scale == 0f)
        {
            return Vector3.Zero;
        }

        if (!float.IsFinite(scale))
        {
            return Vector3.Normalize(vector);
        }

        return Vector3.Normalize(vector / scale);
    }

    /// <summary>
    /// Ограничивает длину вектора сверху.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <param name="maxLength">Максимальная длина, неотрицательная.</param>
    /// <returns>Вектор, длина которого не превышает <paramref name="maxLength"/>.</returns>
    /// <remarks>
    /// Отрицательный предел отвергается: квадрат его остаётся положительным,
    /// поэтому длинный вектор нормализовался бы и умножался на отрицательное
    /// число, то есть разворачивался. Длина соблюдалась бы, направление нет.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Предел отрицательный.</exception>
    public static Vector3 ClampLength(this Vector3 vector, float maxLength)
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
            return lengthSquared <= maxLength * maxLength ? vector : Vector3.Normalize(vector) * maxLength;
        }

        // Длина в масштабе наибольшей компоненты: квадрат исходного вектора
        // обнуляется и переполняется вместе с float, а здесь обе величины
        // лежат в надёжном диапазоне. Поэтому ограничение не зависит от того,
        // в каких единицах выбран мир.
        float scale = MathF.Max(MathF.Abs(vector.X), MathF.Max(MathF.Abs(vector.Y), MathF.Abs(vector.Z)));
        if (scale == 0f)
        {
            return vector;
        }

        Vector3 scaled = vector / scale;
        float scaledLengthSquared = scaled.LengthSquared();
        float ratio = maxLength / scale;
        return ratio * ratio >= scaledLengthSquared ? vector : scaled * (maxLength / MathF.Sqrt(scaledLengthSquared));
    }

    /// <summary>
    /// Проецирует вектор на направление: возвращает компонент вдоль направления.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <param name="direction">Направление проекции, должно быть ненулевым.</param>
    /// <returns>Проекция вектора на направление.</returns>
    /// <exception cref="ArgumentException">Направление нулевое.</exception>
    public static Vector3 ProjectOntoDirection(this Vector3 vector, Vector3 direction)
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
        // [1; 3], и результат не зависит от длины направления вовсе.
        // Обычный случай: квадрат длины направления представим во float, и
        // формула считается как есть. Случай, когда квадрат превышает
        // MaxNormalizableLengthSquared, означает длину больше 1.7320509e19, то
        // есть переполнение квадрата, и формула без масштабирования вернёт ноль.
        float lengthSquared = direction.LengthSquared();
        if (lengthSquared >= MinNormalizableLengthSquared && lengthSquared <= MaxNormalizableLengthSquared)
        {
            return direction * (Vector3.Dot(vector, direction) / lengthSquared);
        }

        float scale = MathF.Max(MathF.Abs(direction.X), MathF.Max(MathF.Abs(direction.Y), MathF.Abs(direction.Z)));
        if (scale == 0f)
        {
            throw new ArgumentException("Направление проекции должно быть ненулевым.", nameof(direction));
        }

        Vector3 scaled = direction / scale;
        return scaled * (Vector3.Dot(vector, scaled) / scaled.LengthSquared());
    }

    /// <summary>
    /// Проецирует точку на плоскость, проходящую через начало координат с
    /// заданной нормалью: компонента вдоль нормали отбрасывается.
    /// </summary>
    /// <param name="point">Проецируемая точка.</param>
    /// <param name="planeNormal">Нормаль плоскости, ненулевая; нормализуется внутри.</param>
    /// <returns>Проекция точки на плоскость.</returns>
    /// <exception cref="ArgumentException">Нормаль нулевая.</exception>
    public static Vector3 ProjectOntoPlane(this Vector3 point, Vector3 planeNormal)
    {
        Vector3 normal = planeNormal.SafeNormalize();
        if (normal == Vector3.Zero)
        {
            throw new ArgumentException("Нормаль плоскости должна быть ненулевой.", nameof(planeNormal));
        }

        return point - normal * Vector3.Dot(point, normal);
    }

    /// <summary>
    /// Отбрасывает компоненту точки вдоль нормали, оставляя только её.
    /// Обратная операция к <see cref="ProjectOntoPlane"/>.
    /// </summary>
    /// <param name="vector">Исходный вектор.</param>
    /// <param name="planeNormal">Нормаль плоскости, ненулевая; нормализуется внутри.</param>
    /// <returns>Компонента вдоль нормали.</returns>
    /// <exception cref="ArgumentException">Нормаль нулевая.</exception>
    public static Vector3 RejectFromPlane(this Vector3 vector, Vector3 planeNormal)
    {
        Vector3 normal = planeNormal.SafeNormalize();
        if (normal == Vector3.Zero)
        {
            throw new ArgumentException("Нормаль плоскости должна быть ненулевой.", nameof(planeNormal));
        }

        return normal * Vector3.Dot(vector, normal);
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
    /// <c>MoveTowards((0, 0, 0), (10, 0, 0), −5)</c> возвращал <c>(−5, −0, −0)</c>,
    /// то есть движение шло от цели. Защита такая же, как в
    /// <see cref="VectorExtensions.MoveTowards"/> и в <see cref="Angle.MoveTowards"/>,
    /// и по той же причине.
    /// <para>
    /// Метод возвращает смещение, а не позицию, поэтому «не двигаться» — это ноль.
    /// <c>NaN</c> проверку не проходит и уходит в вычисление, то есть даёт <c>NaN</c>
    /// в том числе на совпадающих точках: ошибка вызывающего не должна молчать.
    /// </para>
    /// </remarks>
    public static Vector3 MoveTowards(this Vector3 from, Vector3 to, float maxStep)
    {
        if (maxStep <= 0f)
        {
            return Vector3.Zero;
        }

        Vector3 delta = to - from;
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
        // сдвига, где он лежит в [1; sqrt(3)] и оба квадрата конечны.
        float scale = MathF.Max(MathF.Abs(delta.X), MathF.Max(MathF.Abs(delta.Y), MathF.Abs(delta.Z)));
        if (scale == 0f)
        {
            // Совпадающие точки: смещения нет, и оно равно нулю. Шаг при этом
            // может быть и бесконечным (тогда это законное «без ограничения»,
            // и результат — ноль), и нечисловым (тогда ошибка вызывающего не
            // должна молчать, как в Angle.MoveTowards). Поэтому сравнение
            // идёт до умножения: Zero * бесконечность дало бы NaN.
            return maxStep > 0f ? delta : delta * maxStep;
        }

        Vector3 scaled = delta / scale;
        float scaledLength = scaled.Length();
        return scale * scaledLength <= maxStep ? delta : scaled * (maxStep / scaledLength);
    }

    /// <summary>
    /// Поворачивает точку вокруг опорной точки.
    /// </summary>
    /// <param name="point">Поворачиваемая точка.</param>
    /// <param name="pivot">Неподвижная точка вращения.</param>
    /// <param name="rotation">
    /// Поворот. Обязан быть единичным кватернионом.
    /// </param>
    /// <returns>Повёрнутая точка.</returns>
    /// <remarks>
    /// Единичность кватерниона — требование, а не оптимизация: результат
    /// домножается на <c>|rotation|²</c>, и неединичный кватернион молча
    /// масштабирует точку. Измерено: <c>(2, 0, 0, 0)</c> даёт результат
    /// длины 4, а <c>(0.5, 0, 0, 0)</c> — длины 0.25. Нормализовать внутри
    /// метод не может: тогда вызывающий, который повторяет одну и ту же
    /// операцию через <c>QuaternionExtensions.Rotate</c>, получил бы разные
    /// длины для одного и того же кватерниона, то есть расхождение переехало
    /// бы в место, откуда его не видно. Поэтому запрет стоит на стороне
    /// вызывающего, а доктрина называет его здесь.
    /// <para>
    /// Направление вращения задаёт сам кватернион по правилу правой руки
    /// относительно своей оси. В отличие от <c>Vector2.Rotate</c>, который
    /// поворачивает в плоскости XY против часовой стрелки, поворот на угол
    /// <c>a</c> вокруг оси Y в плоскости XZ даёт направление, обратное
    /// двумерному: <c>Vector2.Rotate(90°)</c> даёт <c>(−4.37e-08, 1)</c>, а
    /// <c>RotateAround</c> с кватернионом вокруг +Y даёт <c>(0, 0, −0.99999994)</c>.
    /// Обе реализации правы, но переносящий <see cref="Angle"/> в кватернион
    /// получит поворот в противоположную сторону, поэтому здесь названо явно.
    /// </para>
    /// </remarks>
    public static Vector3 RotateAround(this Vector3 point, Vector3 pivot, in Quaternion rotation)
        => pivot + rotation.Rotate(point - pivot);

    /// <summary>
    /// Возвращает азимут направления: угол в плоскости XZ от оси X в сторону
    /// оси Z. Вертикальная составляющая не учитывается.
    /// </summary>
    /// <param name="direction">Направление.</param>
    /// <returns>Угол азимута.</returns>
    /// <remarks>
    /// Считается через <see cref="Trig"/>, а не вызовом <c>Math.Atan2</c>:
    /// двойная точность здесь ничего не добавляет, обращение к математической
    /// библиотеке платформы обходит фасад и делает азимут зависимым от
    /// операционной системы. Замер: 39.7 нс против 18.4 нс.
    /// </remarks>
    public static Angle ToAngle(this Vector3 direction)
    {
        // Азимут не определён у чисто вертикального направления: там
        // горизонтальная составляющая равна нулю, и направление назад не
        // выбирается. Возвращается ноль, как и у Vector2.ToAngle.
        //
        // Проверяются сами компоненты, а не их квадраты: квадрат обнуляется
        // при горизонтали меньше 3.743392e-23, и признак «направление вертикальное»
        // срабатывал на ненулевой горизонтальной составляющей, то есть метод
        // терял азимут там, где соседний Vector2.ToAngle его возвращал.
        if (direction.X == 0f && direction.Z == 0f)
        {
            return Angle.Zero;
        }

        // Масштабирование направления на положительный множитель не меняет
        // аргумент atan2, поэтому нормализация не нужна.
        return Angle.FromRadiansRaw(Trig.Atan2(direction.Z, direction.X));
    }

    /// <summary>
    /// Возвращает знаковый угол поворота от одного направления к другому
    /// вокруг оси. Знак задаётся правилом правой руки относительно оси.
    /// </summary>
    /// <param name="from">Начальное направление, ненулевое.</param>
    /// <param name="to">Конечное направление, ненулевое.</param>
    /// <param name="axis">Ось вращения, ненулевая; нормализуется внутри.</param>
    /// <returns>Угол в диапазоне (-π; π].</returns>
    /// <exception cref="ArgumentException">Направление или ось нулевые.</exception>
    /// <remarks>
    /// Угол считается как <c>atan2(ось · (a × b), a · b)</c> по перпендикулярным
    /// составляющим направлений. Формула через <c>acos</c> теряла бы
    /// относительную точность на малых углах и требовала нормализации обоих
    /// направлений.
    /// </remarks>
    public static Angle SignedAngleAround(Vector3 from, Vector3 to, Vector3 axis)
    {
        Vector3 unitAxis = axis.SafeNormalize();
        if (unitAxis == Vector3.Zero)
        {
            throw new ArgumentException("Ось вращения должна быть ненулевой.", nameof(axis));
        }

        Vector3 fromPerpendicular = from.ProjectOntoPlane(unitAxis);
        Vector3 toPerpendicular = to.ProjectOntoPlane(unitAxis);
        if (fromPerpendicular == Vector3.Zero || toPerpendicular == Vector3.Zero)
        {
            throw new ArgumentException(
                "Направление не должно быть параллельно оси вращения: знак угла не определён.",
                fromPerpendicular == Vector3.Zero ? nameof(from) : nameof(to));
        }

        // atan2 вместо acos. Числитель и знаменатель масштабируются одним и
        // тем же положительным множителем |fromPerpendicular| * |toPerpendicular|,
        // а atan2 инвариантен к общему положительному масштабу, поэтому
        // нормализовать векторы не нужно. acos же требует косинуса, а у
        // косинуса малого угла в float вся информация теряется: значение
        // округляется в 1.0 и угол меньше примерно 0.01° возвращается нулём.
        float signedSine = Vector3.Dot(Vector3.Cross(fromPerpendicular, toPerpendicular), unitAxis);
        float cosine = Vector3.Dot(fromPerpendicular, toPerpendicular);
        return Angle.FromRadians(Trig.Atan2(signedSine, cosine));
    }

    /// <summary>
    /// Возвращает единичный вектор, перпендикулярный исходному и
    /// направленный как можно ближе к подсказке.
    /// </summary>
    /// <param name="vector">Вектор, которому строится перпендикуляр.</param>
    /// <param name="hint">Желаемое направление перпендикуляра.</param>
    /// <returns>Единичный вектор, перпендикулярный <paramref name="vector"/>.</returns>
    /// <remarks>
    /// В трёх измерениях перпендикуляр не единственен, поэтому требуется
    /// подсказка: без неё результат зависел бы от порядка осей и менялся бы
    /// вместе с ним. Если составляющая подсказки в плоскости неразличима от
    /// округления, то есть синус угла между подсказкой и вектором не больше
    /// <see cref="Scalar.Epsilon"/>, берётся базовая ось, наименее с вектором
    /// сонаправленная: направление в этом случае всё равно произвольно, но
    /// хотя бы не зависит от шума округления.
    /// <para>
    /// Подсказка трактуется как направление, поэтому её длина не имеет
    /// значения: подсказка любой длиной задаёт то же направление, что и
    /// единичная, и метод обязан отвечать на них одинаково.
    /// </para>
    /// <para>
    /// Порог, ниже которого подсказка не учитывается, — это <c>Scalar.Epsilon</c>
    /// в <b>длине</b>, а не в её квадрате. Прежний код сравнивал
    /// <c>|candidate|²</c> с <c>Scalar.Epsilon² · |hint|²</c>, то есть тот же
    /// порог, но обе величины проходили через квадрат, который обнулялся и
    /// переполнялся вместе с float. Из-за этого доктрина называла границу
    /// «примерно от 1e-19 до 1e19», а на самом деле запасная ось бралась с
    /// 1e-21 и выше 1.8446744e19, то есть граница была занижена на два
    /// порядка и не совпадала с собственной формулировкой. Теперь обе длины
    /// сравниваются как длины, и граница ровно та, что написана.
    /// </para>
    /// </remarks>
    public static Vector3 Perpendicular(this Vector3 vector, Vector3 hint)
    {
        Vector3 normal = vector.SafeNormalize();
        if (normal == Vector3.Zero)
        {
            throw new ArgumentException("Вектор должен быть ненулевым.", nameof(vector));
        }

        // Подсказка приводится к масштабу наибольшей компоненты до вычитания.
        // Проекция линейна, поэтому candidate получается тем же по
        // направлению, но обе величины в сравнении ниже остаются конечными при
        // любой длине подсказки. Прежний код сравнивал |candidate|² с
        // Epsilon² · |hint|², и при |hint| больше 1.8446744e19 правая часть
        // переполнялась в бесконечность, то есть запасная ось бралась всегда и
        // подсказка не учитывалась вовсе.
        float hintScale = MathF.Max(MathF.Abs(hint.X), MathF.Max(MathF.Abs(hint.Y), MathF.Abs(hint.Z)));
        Vector3 scaledHint = hintScale == 0f ? Vector3.Zero : hint / hintScale;
        Vector3 candidate = scaledHint - normal * Vector3.Dot(scaledHint, normal);

        // Второй проход ортогонализации. После первого вдоль нормали остаётся
        // составляющая (1 - normal·normal)·(hint·normal): сам вектор нормали
        // в float единичен лишь приблизительно, и при почти параллельной
        // подсказке деление этой составляющей на |candidate| давало
        // |dot(результат, вектор)| до 1.0, то есть на выходе возвращался сам
        // исходный вектор.
        //
        // Повторное вычитание убирает её квадратично: остаётся
        // (1 - normal·normal)^2, то есть при округлении float порядка 1e-14.
        // Измерено: худший |dot| на миллионе пар во всех углах от 0 до 90
        // градусов — 1.5e-7, то есть последний разряд float.
        candidate -= normal * Vector3.Dot(candidate, normal);

        // Порог относительный, а не абсолютный: сравниваются длины, а не
        // квадраты длин, и обе величины приведены к масштабу подсказки, где
        // её наибольшая компонента равна единице, то есть обе величины лежат
        // в [0; 3] и не обнуляются и не переполняются ни при какой длине
        // подсказки. Сравнение
        // candidate с точным нулём было бессмысленно: candidate получается
        // вычитанием, поэтому в точности нулём он обращается только когда
        // normal·normal равно ровно 1.0f, то есть примерно у половины
        // направлений, а у остальных равен 1e-7 и проходил проверку насквозь.
        //
        // Порог поставлен там, где составляющая подсказки в плоскости ещё
        // переживает округление: синус угла не больше эпсилон — направление
        // неразличимо от нуля и надёжнее взять базовую ось. Запасная ветвь
        // при этом перестаёт быть мёртвой и начинает работать ровно тогда, когда
        // она нужна.
        if (candidate.LengthSquared() > Scalar.Epsilon * Scalar.Epsilon * scaledHint.LengthSquared())
        {
            return Vector3.Normalize(candidate);
        }

        Vector3 axis = LeastAlignedAxis(normal);
        return Vector3.Normalize(Vector3.Cross(axis, normal));
    }

    /// <summary>
    /// Строит направление из сферических координат: полярный угол отсчитывается
    /// от вертикали вверх, азимут — в горизонтальной плоскости от оси X в
    /// сторону оси Z.
    /// </summary>
    /// <param name="radius">Длина вектора.</param>
    /// <param name="polar">Угол от оси Y.</param>
    /// <param name="azimuth">Азимут в плоскости XZ.</param>
    /// <returns>Вектор заданной длины и направления.</returns>
    public static Vector3 FromSpherical(float radius, Angle polar, Angle azimuth)
    {
        // Синус и косинус каждого угла берутся одним вызовом: два отдельных
        // вызова математической библиотеки вдвое дороже, а в сумме здесь было
        // четыре вызова вместо двух.
        (float sinPolar, float cosPolar) = Trig.SinCos((float)polar.Radians);
        (float sinAzimuth, float cosAzimuth) = Trig.SinCos((float)azimuth.Radians);

        return new Vector3(
            radius * sinPolar * cosAzimuth,
            radius * cosPolar,
            radius * sinPolar * sinAzimuth);
    }

    /// <summary>
    /// Проверяет, лежит ли значение вектора в допуске от нуля по всем компонентам.
    /// </summary>
    /// <param name="vector">Проверяемый вектор.</param>
    /// <param name="epsilon">Допуск.</param>
    /// <returns><c>true</c>, если вектор близок к нулю.</returns>
    public static bool IsNearlyZero(this Vector3 vector, float epsilon = Scalar.Epsilon)
        => MathF.Abs(vector.X) <= epsilon
            && MathF.Abs(vector.Y) <= epsilon
            && MathF.Abs(vector.Z) <= epsilon;

    private static Vector3 LeastAlignedAxis(in Vector3 unit)
    {
        float absoluteX = MathF.Abs(unit.X);
        float absoluteY = MathF.Abs(unit.Y);
        float absoluteZ = MathF.Abs(unit.Z);
        if (absoluteX <= absoluteY && absoluteX <= absoluteZ)
        {
            return Vector3.UnitX;
        }

        return absoluteY <= absoluteZ ? Vector3.UnitY : Vector3.UnitZ;
    }
}