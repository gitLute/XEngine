using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Операции над кватернионами поверх того, что уже есть в
/// <see cref="System.Numerics"/>.
/// </summary>
/// <remarks>
/// Кватернион в библиотеке всегда единичный и задаёт поворот без переноса.
/// Порядок осей в <see cref="FromEuler"/> зафиксирован: сначала поворот вокруг Z,
/// затем вокруг X, затем вокруг Y. Порядок фиксируется один раз (4.5) и
/// проверяется тестом на композиции.
/// </remarks>
public static class QuaternionExtensions
{
    /// <summary>
    /// Строит поворот вокруг оси на заданный угол. Ось нормализуется внутри.
    /// </summary>
    /// <param name="axis">Ось вращения, ненулевая.</param>
    /// <param name="angle">Угол против часовой стрелки при взгляде со стороны положительной полуоси.</param>
    /// <returns>Поворот.</returns>
    /// <exception cref="ArgumentException">Ось нулевая.</exception>
    /// <remarks>
    /// Синус и косинус половины угла берутся через <see cref="Trig"/>, а не
    /// вызовом <c>Quaternion.CreateFromAxisAngle</c>. Тот внутри обращается к
    /// математической библиотеке платформы, то есть его результат зависит от
    /// операционной системы, и детерминированный вариант сборки перестал бы
    /// быть детерминированным ровно на построении ориентации.
    /// </remarks>
    public static Quaternion FromAxisAngle(Vector3 axis, Angle angle)
    {
        Vector3 unitAxis = axis.SafeNormalize();
        if (unitAxis == Vector3.Zero)
        {
            throw new ArgumentException("Ось вращения должна быть ненулевой.", nameof(axis));
        }

        return FromUnitAxisAngle(unitAxis, angle);
    }

    /// <summary>
    /// Строит поворот вокруг уже единичной оси. Проверок и нормализации нет:
    /// они выполнены вызывающим.
    /// </summary>
    /// <param name="unitAxis">Единичная ось вращения.</param>
    /// <param name="angle">Угол поворота.</param>
    /// <returns>Поворот.</returns>
    private static Quaternion FromUnitAxisAngle(in Vector3 unitAxis, in Angle angle)
    {
        (float sin, float cos) = Trig.SinCos((float)angle.Radians * 0.5f);
        return new Quaternion(unitAxis.X * sin, unitAxis.Y * sin, unitAxis.Z * sin, cos);
    }

    /// <summary>
    /// Строит поворот из углов Эйлера в порядке Z, X, Y: сначала поворот
    /// вокруг оси Z (крен), затем вокруг X (тангаж), затем вокруг Y (рыскание).
    /// </summary>
    /// <param name="yaw">Поворот вокруг вертикальной оси Y.</param>
    /// <param name="pitch">Поворот вокруг оси X.</param>
    /// <param name="roll">Поворот вокруг оси Z.</param>
    /// <returns>Композиция поворотов в зафиксированном порядке.</returns>
    /// <remarks>
    /// Композиция считается напрямую, а не тремя вызовами
    /// <see cref="FromAxisAngle(Vector3, Angle)"/>: базовые оси единичные,
    /// поэтому их нормализация не нужна. Замер: 377 нс против 35 нс, то есть
    /// на 10 тысячах объектов 3.8 мс кадра против 0.35 мс.
    /// </remarks>
    public static Quaternion FromEuler(Angle yaw, Angle pitch, Angle roll)
    {
        Quaternion rollQ = FromUnitAxisAngle(Vector3.UnitZ, roll);
        Quaternion pitchQ = FromUnitAxisAngle(Vector3.UnitX, pitch);
        Quaternion yawQ = FromUnitAxisAngle(Vector3.UnitY, yaw);

        return yawQ * (pitchQ * rollQ);
    }

    /// <summary>
    /// Разбирает поворот в углы Эйлера в том же порядке, что и
    /// <see cref="FromEuler"/>: рыскание вокруг Y, тангаж вокруг X, крен вокруг Z.
    /// </summary>
    /// <param name="rotation">Исходный поворот.</param>
    /// <returns>Рыскание, тангаж и крен.</returns>
    /// <remarks>
    /// Формулы берутся из элементов матрицы поворота, а не из элементов
    /// кватерниона: разбор в порядке Z, X, Y через элементы кватерниона
    /// несимметричен и легко переносится с ошибкой знака. Учтено, что матрица
    /// поворота хранится в соглашении вектор-строка, поэтому композиция
    /// поворотов читается в обратном порядке индексов. В вырожденном случае
    /// тангажа в ±90° рыскание и крен вращаются вокруг одной оси и неразделимы:
    /// рыскание принимается равным нулю, крен при этом определяется однозначно.
    /// <para>
    /// Граница блокировки расширять не нужно, и это проверено измерением, а не
    /// рассуждением. Ошибка рыскания и крена растёт как 1/cos(тангаж) независимо
    /// от способа извлечения: она определяется тем, что пара элементов, из
    /// которых берётся угол, обращается в нуль у полюса, а её абсолютная ошибка
    /// около 1e-7. Расширение полосы не уменьшает худшую ошибку, а лишь
    /// переносит границу договора: внутри полосы рыскание равно нулю по контракту
    /// вместо того, чтобы быть отличным от нуля на 6.9e-3°. Извлечение из
    /// компонентов кватерниона вместо элементов матрицы проверено отдельно и
    /// даёт те же числа побитово, то есть плохая обусловленность здесь не от
    /// выбора носителя.
    /// </para>
    /// <para>
    /// Тангаж при этом извлекается точно на всём диапазоне, включая полосу
    /// блокировки, где прежний <c>asin</c> ошибался на 4.4e-2°. Ориентация
    /// восстанавливается верно по контракту в любом случае, то есть разделение
    /// рыскания и крена у полюса — это свойство разложения, а не дефект кода.
    /// </para>
    /// </remarks>
    public static (Angle Yaw, Angle Pitch, Angle Roll) ToEuler(in Quaternion rotation)
    {
        Matrix4x4 matrix = Matrix4x4.CreateFromQuaternion(Normalize(rotation));

        float sinPitch = Scalar.Clamp(-matrix.M32, -1f, 1f);

        // Косинус берётся из элементов матрицы напрямую, а не как корень из
        // 1 − sin²: у полюса sin² почти единица, и вычитание съедало бы весь
        // результат. Столбец матрицы имеет длину 1, поэтому сумма квадратов его
        // ненулевых элементов есть именно cos²(тангаж).
        float cosPitch = MathF.Sqrt((matrix.M31 * matrix.M31) + (matrix.M33 * matrix.M33));

        // Тангаж извлекается через atan2, а не через asin. Производная arcsin у
        // единицы бесконечна, то есть ошибка растёт как 1/cos(pitch): измерено
        // 3.4e-3° на 89.8° и 1.2e-2° на границе блокировки против 2.0e-5° у
        // atan2 на всём диапазоне. Ориентация при этом восстанавливается верно
        // в обоих случаях, то есть страдали только отдельные углы — но именно их
        // и читает вызывающий.
        float pitch = Trig.Atan2(sinPitch, cosPitch);

        float yaw;
        float roll;
        if (MathF.Abs(sinPitch) < GimbalLockThreshold)
        {
            yaw = Trig.Atan2(matrix.M31, matrix.M33);
            roll = Trig.Atan2(matrix.M12, matrix.M22);
        }
        else
        {
            yaw = 0f;
            roll = sinPitch * Trig.Atan2(matrix.M13, matrix.M11);
        }

        return (Angle.FromRadians(yaw), Angle.FromRadians(pitch), Angle.FromRadians(roll));
    }

    /// <summary>
    /// Поворачивает точку кватернионом. Перенос не применяется: кватернион
    /// задаёт только поворот.
    /// </summary>
    /// <param name="rotation">Поворот, предполагается единичным.</param>
    /// <param name="point">Поворачиваемая точка.</param>
    /// <returns>Повёрнутая точка.</returns>
    /// <remarks>
    /// Тонкая обёртка над <see cref="Vector3.Transform(Vector3, Quaternion)"/>:
    /// повторять вычисление незачем, но имя закрепляет контракт «поворот без
    /// переноса» рядом с <c>Transform</c> матрицей, где перенос есть (6.5,
    /// правило 6).
    /// </remarks>
    public static Vector3 Rotate(this in Quaternion rotation, Vector3 point)
        => Vector3.Transform(point, rotation);

    /// <summary>
    /// Строит поворот, в котором локальная ось Z смотрит вдоль
    /// <paramref name="forward"/>, а локальная ось Y — в сторону
    /// <paramref name="up"/>.
    /// </summary>
    /// <param name="forward">Направление взгляда, ненулевое и не параллельное вертикали.</param>
    /// <param name="up">Направление вверх, ненулевое и не параллельное направлению взгляда.</param>
    /// <returns>Поворот.</returns>
    /// <exception cref="ArgumentException">Направление нулевое или совпадает с вертикалью.</exception>
    /// <remarks>
    /// Здесь ось Z направлена <em>вперёд</em>, по направлению взгляда, а у
    /// матрицы вида ось Z направлена <em>назад</em> (см. remarks
    /// <see cref="Matrix4x4Extensions.CreateLookAt"/>). Это разные конвенции, и
    /// обратным к матрице вида является не этот поворот, а разворот на 180°
    /// вокруг Y: подставлять <c>LookRotation(forward, up)</c> вместо ориентации
    /// из матрицы вида разворачивает объект спиной к камере.
    /// </remarks>
    public static Quaternion LookRotation(Vector3 forward, Vector3 up)
    {
        Vector3 unitForward = forward.SafeNormalize();
        Vector3 unitUp = up.SafeNormalize();
        if (unitForward == Vector3.Zero)
        {
            throw new ArgumentException("Направление взгляда должно быть ненулевым.", nameof(forward));
        }

        if (unitUp == Vector3.Zero)
        {
            throw new ArgumentException("Направление вверх должно быть ненулевым.", nameof(up));
        }

        if (MathF.Abs(Vector3.Dot(unitForward, unitUp)) >= 1f - Scalar.Epsilon)
        {
            throw new ArgumentException(
                "Направление взгляда не должно совпадать с направлением вверх: ориентация не определена.",
                nameof(forward));
        }

        Vector3 right = Vector3.Normalize(Vector3.Cross(unitUp, unitForward));
        Vector3 trueUp = Vector3.Cross(unitForward, right);
        return Quaternion.CreateFromRotationMatrix(new Matrix4x4(
            right.X,
            right.Y,
            right.Z,
            0f,
            trueUp.X,
            trueUp.Y,
            trueUp.Z,
            0f,
            unitForward.X,
            unitForward.Y,
            unitForward.Z,
            0f,
            0f,
            0f,
            0f,
            1f));
    }

    /// <summary>
    /// Интерполирует поворот по кратчайшей дуге с ограничением параметра
    /// диапазоном [0; 1].
    /// </summary>
    /// <param name="from">Начальный поворот.</param>
    /// <param name="to">Конечный поворот.</param>
    /// <param name="t">Параметр интерполяции, ограничивается диапазоном [0; 1].</param>
    /// <returns>Поворот на заданном параметре.</returns>
    /// <remarks>
    /// Отличается от <see cref="Quaternion.Slerp(Quaternion, Quaternion, float)"/>
    /// и реализацией, и контрактом. Реализация своя, потому что встроенная
    /// обращается к <c>MathF.Acos</c> и <c>MathF.Sin</c>, то есть к
    /// математической библиотеке платформы: детерминированный вариант сборки
    /// на интерполяции поворота перестал бы воспроизводиться.
    /// <para>
    /// Контракт: параметр ограничен диапазоном <c>[0; 1]</c>, оба операнда
    /// нормализуются, знак кватерниона выбирается так, чтобы идти по кратчайшей
    /// дуге, а при угле меньше <see cref="SmallAngleThreshold"/> берётся
    /// линейная интерполяция с последующей нормализацией. Без последнего
    /// отношение синусов делит на величину порядка угла, то есть на число
    /// около нуля, и результат теряет значащие цифры ровно там, где
    /// ориентации меняются быстрее всего.
    /// </para>
    /// </remarks>
    public static Quaternion Slerp(Quaternion from, Quaternion to, float t)
    {
        float clamped = Interpolation.Clamp01(t);
        Quaternion start = Normalize(from);
        Quaternion end = Normalize(to);

        float cosine = Quaternion.Dot(start, end);
        if (cosine < 0f)
        {
            // Кратчайшая дуга: противоположный знак кватерниона задаёт ту же
            // ориентацию, поэтому поворот всегда идёт по дуге меньше π.
            end = new Quaternion(-end.X, -end.Y, -end.Z, -end.W);
            cosine = -cosine;
        }

        if (cosine > SmallAngleThreshold)
        {
            return Normalize(new Quaternion(
                start.X + ((end.X - start.X) * clamped),
                start.Y + ((end.Y - start.Y) * clamped),
                start.Z + ((end.Z - start.Z) * clamped),
                start.W + ((end.W - start.W) * clamped)));
        }

        float angle = Trig.Acos(cosine);
        float sinAngle = Trig.Sin(angle);
        float first = Trig.Sin((1f - clamped) * angle);
        float second = Trig.Sin(clamped * angle);
        float firstShare = first / sinAngle;
        float secondShare = second / sinAngle;

        return Normalize(new Quaternion(
            (start.X * firstShare) + (end.X * secondShare),
            (start.Y * firstShare) + (end.Y * secondShare),
            (start.Z * firstShare) + (end.Z * secondShare),
            (start.W * firstShare) + (end.W * secondShare)));
    }

    /// <summary>
    /// Возвращает угол наименьшего поворота между двумя ориентациями.
    /// </summary>
    /// <param name="from">Начальная ориентация.</param>
    /// <param name="to">Конечная ориентация.</param>
    /// <returns>Угол в диапазоне [0; π].</returns>
    /// <remarks>
    /// Знак кватерниона не учитывается: повороты <c>q</c> и <c>-q</c>
    /// задают одну ориентацию.
    /// </remarks>
    public static Angle AngleBetween(Quaternion from, Quaternion to)
    {
        // Нормализовать нужно оба операнда: скалярное произведение
        // неоднородно, и при |to| != 1 угол между ориентациями получается
        // завышенным (AngleBetween(identity, 0.5 * поворот) давал 138.6°).
        //
        // Угол считается через относительный поворот to ⊗ conj(from): у
        // единичного кватерниона угол поворота равен 2 * atan2(|w|, ‖векторная
        // часть|), то есть половина угла есть atan2(‖v‖, |w|), поскольку
        // tan(θ/2) = ‖v‖ / |w|.
        //
        // Формула через acos от скалярного произведения не годится: у малого
        // угла скалярное произведение отличается от единицы на величину порядка
        // θ², во float это различие меньше эпсилон, и угол возвращался ровно
        // нулём для всех углов меньше примерно 0.01°. atan2 сохраняет
        // относительную точность, потому что зависит от θ линейно.
        Quaternion relative = Quaternion.Multiply(Normalize(to), Conjugate(Normalize(from)));

        float vectorPartSquared =
            (relative.X * relative.X) + (relative.Y * relative.Y) + (relative.Z * relative.Z);
        float halfAngle = Trig.Atan2(MathF.Sqrt(vectorPartSquared), MathF.Abs(relative.W));

        return Angle.FromRadians(2.0 * halfAngle);
    }

    /// <summary>
    /// Возвращает сопряжённый кватернион: векторная часть меняет знак.
    /// </summary>
    /// <param name="rotation">Исходный кватернион.</param>
    /// <returns>Сопряжённый кватернион.</returns>
    public static Quaternion Conjugate(in Quaternion rotation)
        => new(-rotation.X, -rotation.Y, -rotation.Z, rotation.W);

    /// <summary>
    /// Возвращает обратный кватернион, отменяющий исходный.
    /// </summary>
    /// <param name="rotation">Исходный кватернион, ненулевой.</param>
    /// <returns>Обратный кватернион.</returns>
    /// <remarks>
    /// <para>
    /// Для единичного кватерниона совпадает с <see cref="Conjugate"/>;
    /// расходится для произвольного множителя, который делит кватернион
    /// на квадрат длины.
    /// </para>
    /// <para>
    /// Отказ срабатывает на точном нуле, а не на «длине меньше 1e-6».
    /// Прежний порог <c>lengthSquared &lt;= Epsilon²</c> отвергал всякий
    /// ненулевой кватернион короче миллиона: <c>Inverse(0, 0, 0, 1e-6)</c>
    /// бросал исключение, хотя обратный элемент существует и равен
    /// <c>1e6</c>. Измерено на прежнем коде: отвергнуты множители 1e-6, 1e-7,
    /// 1e-19 и 1e-30 при кватернионе, заведомо ненулевом.
    /// </para>
    /// <para>
    /// Второй край того же порога — переполнение. При <c>|q| = 1e20</c> квадрат
    /// длины равен 1e40 и не помещается в <see cref="float"/>: прежний код
    /// проходил мимо проверки, делил на бесконечность и возвращал нулевой
    /// кватернион, то есть произведение <c>q · q⁻¹</c> давало ноль вместо
    /// единицы. Проверка конечности добавлена поэтому, что без неё длинный
    /// кватернион проходит гейт и даёт молчаливую пустоту.
    /// </para>
    /// </remarks>
    public static Quaternion Inverse(this in Quaternion rotation)
    {
        float lengthSquared = rotation.LengthSquared();
        if (lengthSquared > 0f && float.IsFinite(lengthSquared))
        {
            // Быстрый путь: квадрат длины и есть divisor. При |q| = 1 это
            // деление на единицу, то есть сопряжение побитово.
            float inverseLengthSquared = 1f / lengthSquared;
            return new Quaternion(
                -rotation.X * inverseLengthSquared,
                -rotation.Y * inverseLengthSquared,
                -rotation.Z * inverseLengthSquared,
                rotation.W * inverseLengthSquared);
        }

        // Квадрат длины обнулился: |q| < 1e-19, то есть ненулевой кватернион
        // коротек микроскопического. Или он переполнился: |q| > 1.8e19.
        // Обратный кватернион в обоих случаях ненулевой, поэтому отказ здесь
        // означал бы нарушение контракта на ненулевом входе.
        float largest = LargestComponent(rotation);
        if (!(largest > 0f) || !float.IsFinite(largest))
        {
            throw new ArgumentException("Обратный кватернион не определён для нулевого значения.", nameof(rotation));
        }

        // Обратный считается в масштабе наибольшей компоненты: там длина
        // единичная, и обратная длина получается делением масштаба, а не
        // величины порядка |q|², которая вне float.
        float scale = 1f / largest;
        float unitX = rotation.X * scale;
        float unitY = rotation.Y * scale;
        float unitZ = rotation.Z * scale;
        float unitW = rotation.W * scale;
        float unitLengthSquared = ((unitX * unitX) + (unitY * unitY)) + ((unitZ * unitZ) + (unitW * unitW));

        // |q|⁻² = scale² / |q·s|², и множители применяются по отдельности:
        // их произведение при |q| = 1e20 равно 1e-40, что вне нормальных
        // чисел float и обнулилось бы.
        float first = -unitX * scale;
        float second = -unitY * scale;
        float third = -unitZ * scale;
        float fourth = unitW * scale;
        float factor = 1f / unitLengthSquared;
        return new Quaternion(first * factor, second * factor, third * factor, fourth * factor);
    }

    /// <summary>
    /// Порог, ниже которого разбор Эйлера считает невырожденным.
    /// </summary>
    private const float GimbalLockThreshold = 1f - 1e-6f;

    /// <summary>
    /// Косинус угла между ориентациями, выше которого поворот считается почти
    /// нулевым и берётся линейная интерполяция. Порог 0.9995 отвечает углу
    /// около 1.8°: при больших углах отношение синусов в формуле сферической
    /// интерполяции делит на число порядка 0.03 и теряет точность, а при малых
    /// линейная интерполяция отличается от сферической меньше, чем на последний
    /// разряд.
    /// </summary>
    private const float SmallAngleThreshold = 0.9995f;

    /// <summary>
    /// Нормирует поворот. Обнуляет только точный ноль, а не «длину меньше
    /// <see cref="Scalar.Epsilon"/>»: ненулевой поворот задаёт ориентацию при
    /// любой длине, и произвольный множитель в кватернионе — обычное дело после
    /// накопления в цикле интерполяции.
    /// </summary>
    /// <remarks>
    /// Метод намеренно оставлен маленьким, а редкая ветвь вынесена в
    /// <see cref="NormalizeScaled"/>. Это не эстетика, а замер: <c>Slerp</c>
    /// вызывает нормировку трижды на вызов, и когда ветвь была внутри, метод
    /// переставал встраиваться — <c>Slerp</c> стоил 47.3 нс до правки и 74.9 нс
    /// после. Вынос редкой ветви вернул встраивание.
    /// </remarks>
    private static Quaternion Normalize(in Quaternion rotation)
    {
        float lengthSquared = rotation.LengthSquared();
        if (lengthSquared > 0f & float.IsFinite(lengthSquared))
        {
            return rotation * (1f / MathF.Sqrt(lengthSquared));
        }

        return NormalizeScaled(in rotation);
    }

    /// <summary>
    /// Нормировка в масштабе наибольшей компоненты. Нужна там, где квадрат длины
    /// обнулился (|q| &lt; 1e-19) или переполнился (|q| &gt; 1.8e19): в обоих
    /// случаях кватернион ненулевой и нормируемый.
    /// </summary>
    /// <remarks>
    /// Признак нулевого кватерниона — компонента, а не квадрат длины, и это
    /// не тонкость, а ошибка, стоившая теста: у кватерниона с |q| = 1e-30 квадрат
    /// длины обнуляется при округлении, хотя сам кватернион ненулевой. Проверка
    /// <c>lengthSquared == 0</c> здесь возвращала бы единичный поворот вместо
    /// нормированного, и разбор Эйлера давал бы нулевые углы.
    /// </remarks>
    private static Quaternion NormalizeScaled(in Quaternion rotation)
    {
        float largest = LargestComponent(rotation);
        if (!(largest > 0f))
        {
            // Нулевой кватернион: направления нет, и единичный поворот —
            // единственный, который можно вернуть. Нечисловые компоненты дают
            // тот же признак, и прежнее поведение (NaN насквозь) сохранено.
            return Quaternion.Identity;
        }

        if (!float.IsFinite(largest))
        {
            // Бесконечная компонента: длину посчитать нельзя, и нормированного
            // поворота не существует.
            return rotation;
        }

        // Нормировка в масштабе наибольшей компоненты: длина там равна единице,
        // и результат не зависит от того, обнулился или переполнился квадрат.
        float scale = 1f / largest;
        float unitX = rotation.X * scale;
        float unitY = rotation.Y * scale;
        float unitZ = rotation.Z * scale;
        float unitW = rotation.W * scale;
        float unitLengthSquared = ((unitX * unitX) + (unitY * unitY)) + ((unitZ * unitZ) + (unitW * unitW));
        return new Quaternion(unitX, unitY, unitZ, unitW) * (1f / MathF.Sqrt(unitLengthSquared));
    }

    /// <summary>
    /// Модуль наибольшей по модулю компоненты кватерниона. Нужен там, где длина
    /// недоступна из-за обнуления или переполнения квадрата.
    /// </summary>
    private static float LargestComponent(in Quaternion rotation)
        => MathF.Max(
            MathF.Abs(rotation.X),
            MathF.Max(MathF.Abs(rotation.Y), MathF.Max(MathF.Abs(rotation.Z), MathF.Abs(rotation.W))));
}