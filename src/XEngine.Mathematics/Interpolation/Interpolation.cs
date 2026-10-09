using System.Numerics;

namespace XEngine.Mathematics;

/// <summary>
/// Функции интерполяции и сглаживания.
/// Все функции сглаживания (<see cref="Damp(float, float, float, float)"/>, <see cref="SmoothDamp"/>) не зависят
/// от частоты кадров, в отличие от <c>Lerp(value, target, dt * k)</c>.
/// </summary>
public static class Interpolation
{
    /// <summary>
    /// Линейная интерполяция с ограничением параметра в диапазоне 0..1.
    /// </summary>
    /// <param name="from">Начальное значение.</param>
    /// <param name="to">Конечное значение.</param>
    /// <param name="t">Параметр интерполяции.</param>
    /// <returns>Значение между <paramref name="from"/> и <paramref name="to"/>.</returns>
    public static float Lerp(float from, float to, float t) => from + (to - from) * Clamp01(t);

    /// <summary>
    /// Линейная интерполяция без ограничения параметра.
    /// </summary>
    /// <param name="from">Начальное значение.</param>
    /// <param name="to">Конечное значение.</param>
    /// <param name="t">Параметр интерполяции.</param>
    /// <returns>Значение, полученное интерполяцией.</returns>
    public static float LerpUnclamped(float from, float to, float t) => from + (to - from) * t;

    /// <summary>
    /// Обратная линейная интерполяция: какая доля пути пройдена.
    /// </summary>
    /// <param name="from">Начальное значение.</param>
    /// <param name="to">Конечное значение.</param>
    /// <param name="value">Текущее значение.</param>
    /// <returns>Параметр интерполяции.</returns>
    /// <exception cref="ArgumentException">Начальное и конечное значения совпадают.</exception>
    public static float InverseLerp(float from, float to, float value)
    {
        if (IsEmptyRange(from, to))
        {
            throw new ArgumentException("Диапазон интерполяции пуст.", nameof(from));
        }

        return (value - from) / (to - from);
    }

    /// <summary>
    /// Признак диапазона, в котором параметр интерполяции не определён.
    /// </summary>
    /// <param name="from">Начало диапазона.</param>
    /// <param name="to">Конец диапазона.</param>
    /// <returns><c>true</c>, если границы совпадают как числа одинарной точности.</returns>
    /// <remarks>
    /// Критерий — различие границ, а не ширина. Прежний порог
    /// <c>Scalar.Epsilon</c> был абсолютным и отвергал всё уже 1e-6, включая
    /// диапазон <c>0 … 1e-7</c>, где ширина составляет сто процентов масштаба:
    /// <c>RemapUnclamped(5e-8, 0, 1e-7, 0, 10)</c> давал ноль вместо пяти.
    /// Абсолютная величина не имеет здесь смысла, потому что у разности углов
    /// нет собственного размера, с которым её можно было бы сравнить, —
    /// сравнение сходно с тем, чтобы мерить углы в метрах.
    /// <para>
    /// Если границы различаются как <c>float</c>, интервал содержит хотя бы одну
    /// представимую величину, и деление на ширину определено. Практически это то
    /// же, что «ширина не меньше одного последнего разряда наибольшей границы»,
    /// но без константы в коде: константа, выраженная через <c>ScaleB</c> или
    /// битовые маски, читалась бы как магия, а равенство читается как равенство.
    /// </para>
    /// </remarks>
    private static bool IsEmptyRange(float from, float to) => from == to;

    /// <summary>
    /// Интерполяция с ограничением параметра в диапазоне 0..1.
    /// </summary>
    /// <param name="from">Начальное значение.</param>
    /// <param name="to">Конечное значение.</param>
    /// <param name="t">Параметр интерполяции, ограниченный диапазоном 0..1.</param>
    /// <returns>Значение между <paramref name="from"/> и <paramref name="to"/>.</returns>
    public static float LerpClamped(float from, float to, float t) => Lerp(from, to, Clamp01(t));

    /// <summary>
    /// Двигает значение к цели с ограничением шага.
    /// </summary>
    /// <param name="current">Текущее значение.</param>
    /// <param name="target">Целевое значение.</param>
    /// <param name="maxStep">Максимальный шаг.</param>
    /// <returns>Новое значение, не перескакивающее цель.</returns>
    /// <remarks>
    /// Неположительный шаг возвращает текущее значение. Без этой проверки
    /// квадрат шага оставался положительным, проверка «не перескакиваем»
    /// проходила, а деление шло с отрицательным множителем: <c>MoveTowards(0, 10, −5)</c>
    /// давал <c>−5</c>, то есть движение уходило от цели. Защита такая же, как в
    /// <see cref="Angle.MoveTowards"/>, и по той же причине: знак у шага не должен
    /// задавать направление. Там она ещё и объяснена в доктрине, а здесь молчала.
    /// <para>
    /// <c>NaN</c> проверку не проходит и уходит в вычисление, то есть даёт
    /// <c>NaN</c>. Это осознанно и одинаково во всех методах <c>MoveTowards</c>:
    /// нечисловой шаг — ошибка вызывающего, которая должна быть видна сразу, а не
    /// заменяться молчаливым нулём.
    /// </para>
    /// </remarks>
    public static float MoveTowards(float current, float target, float maxStep)
    {
        if (maxStep <= 0f)
        {
            return current;
        }

        float delta = target - current;
        return MathF.Abs(delta) <= maxStep ? target : current + MathF.Sign(delta) * maxStep;
    }

    /// <summary>
    /// Двигает угол к цели с ограничением шага по кратчайшей дуге.
    /// </summary>
    /// <param name="current">Текущий угол.</param>
    /// <param name="target">Целевой угол.</param>
    /// <param name="maxDeltaDegrees">Максимальный шаг в градусах.</param>
    /// <returns>Новый угол.</returns>
    public static Angle MoveTowardsAngle(Angle current, Angle target, double maxDeltaDegrees)
        => Angle.MoveTowards(current, target, Scalar.ToRadians(maxDeltaDegrees));

    /// <summary>
    /// Экспоненциальное сглаживание, не зависящее от частоты кадров.
    /// Значение <paramref name="lambda"/> — скорость приближения: больше значит быстрее.
    /// </summary>
    /// <param name="current">Текущее значение.</param>
    /// <param name="target">Целевое значение.</param>
    /// <param name="lambda">Скорость сглаживания.</param>
    /// <param name="deltaTime">Время кадра в секунках.</param>
    /// <returns>Сглаженное значение.</returns>
    /// <remarks>
    /// Доля пути считается через <see cref="Trig.OneMinusExp"/>. Наивная запись
    /// <c>1 - exp(-λ·dt)</c> теряет почти все значащие цифры при малом
    /// произведении: при <c>λ·dt = 1e-6</c> ошибка была 1.3 %, а при
    /// <c>1e-8</c> возвращался ровно ноль, то есть медленно сглаживаемое
    /// значение переставало двигаться вовсе. Математическая независимость от
    /// частоты кадров при этом сохранялась — ломалась численная реализация.
    /// </remarks>
    public static float Damp(float current, float target, float lambda, float deltaTime)
        => LerpUnclamped(current, target, Trig.OneMinusExp(-lambda * deltaTime));

    /// <summary>
    /// Экспоненциальное сглаживание вектора.
    /// </summary>
    /// <param name="current">Текущее значение.</param>
    /// <param name="target">Целевое значение.</param>
    /// <param name="lambda">Скорость сглаживания.</param>
    /// <param name="deltaTime">Время кадра в секундах.</param>
    /// <returns>Сглаженное значение.</returns>
    public static Vector2 Damp(Vector2 current, Vector2 target, float lambda, float deltaTime)
        => Vector2.Lerp(current, target, Trig.OneMinusExp(-lambda * deltaTime));

    /// <summary>
    /// Плавное сглаживание с ограничением максимальной скорости изменения.
    /// Используется для камеры: значение не «дёргается» и не отстаёт бесконечно.
    /// </summary>
    /// <param name="current">Текущее значение.</param>
    /// <param name="target">Целевое значение.</param>
    /// <param name="smoothTime">
    /// Время достижения цели, в секундах. Должно быть больше нуля. Слишком малое
    /// положительное значение подтягивается к `0.0001`, чтобы знаменатель не
    /// обнулился: при нуле деление `2 / smoothTime` дало бы бесконечность и от
    /// этого испорченную скорость в `ref`-параметре.
    /// </param>
    /// <param name="maxSpeed">
    /// Максимальная скорость изменения. Должна быть неотрицательной: предел
    /// скорости величиной по модулю больше цели не имеет смысла. Ноль допустим и
    /// означает «не двигаться».
    /// </param>
    /// <param name="deltaTime">Время кадра в секундах.</param>
    /// <param name="velocity">Скорость на предыдущем кадре. Возвращается обновлённой.</param>
    /// <returns>Новое значение.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="smoothTime"/> не больше нуля либо
    /// <paramref name="maxSpeed"/> отрицателен.
    /// </exception>
    /// <remarks>
    /// Оба параметра проверяются здесь, а не полагаются на вложенные методы.
    /// Без проверки отрицательный <paramref name="maxSpeed"/> переставлял границы
    /// <see cref="Scalar.Clamp"/> и приводил к исключению с сообщением о
    /// «минимальной границе» и с именем параметра <c>min</c>, которого в
    /// сигнатуре этого метода нет: вызывающий получал в стеке <c>Clamp</c> и не
    /// мог понять, что виноват его аргумент.
    /// <para>
    /// Неположительный <paramref name="deltaTime"/> исключением не является:
    /// пауза и нулевой кадр — обычные случаи, и метод возвращает текущее
    /// значение, не записывая в скорость <c>NaN</c>.
    /// </para>
    /// </remarks>
    public static float SmoothDamp(
        float current,
        float target,
        float smoothTime,
        float maxSpeed,
        float deltaTime,
        ref float velocity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(smoothTime, 0f);
        ArgumentOutOfRangeException.ThrowIfNegative(maxSpeed);

        smoothTime = MathF.Max(0.0001f, smoothTime);

        if (deltaTime <= 0f)
        {
            // Без этого шага деление velocity = (output - originalTo) /
            // deltaTime ниже даёт 0/0 = NaN, и NaN записывается в ref-параметр
            // навсегда: все последующие вызовы возвращают NaN. Покой и пауза —
            // обычные случаи, а не крайние.
            return current;
        }

        float omega = 2f / smoothTime;

        float exponent = 1f / (1f + omega * deltaTime);
        float change = current - target;
        float originalTo = target;

        float maxChange = maxSpeed * smoothTime;
        change = Scalar.Clamp(change, -maxChange, maxChange);
        target = current - change;

        float temp = (velocity + omega * change) * deltaTime;
        velocity = (velocity - omega * temp) * exponent;
        float output = target + (change + temp) * exponent;

        if (originalTo - current > 0f == output > originalTo)
        {
            output = originalTo;
            velocity = (output - originalTo) / deltaTime;
        }

        return output;
    }

    /// <summary>
    /// Переносит значение в диапазон с заданным периодом.
    /// </summary>
    /// <param name="value">Исходное значение.</param>
    /// <param name="length">Период. Должен быть больше нуля.</param>
    /// <returns>Значение в диапазоне 0..<paramref name="length"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Период меньше или равен нулю.</exception>
    /// <remarks>
    /// Вычисление идёт в <see cref="double"/>, а округляется один раз, при
    /// возврате. В <c>float</c> произведение <c>Floor(value / length) * length</c>
    /// округлялось обратно на <paramref name="value"/>, и разность давала ровно ноль
    /// начиная с отношения около 2²³: <c>Repeat(1e8, 7)</c> возвращал 0 вместо 2,
    /// а <c>Repeat(0.3, 1e-9)</c> — 0 вместо 4.06e-10.
    /// <para>
    /// Диапазон расширен, но не во все стороны, и граница измерена. Пока отношение
    /// <paramref name="value"/> к <paramref name="length"/> не больше 1e8, результат
    /// совпадает с точным остатком побитово; прежний путь давал точный остаток
    /// примерно до 2²³, то есть расширение в 12 раз. Дальше точность падает
    /// линейно по отношению, потому что остаток получается вычитанием двух чисел
    /// величиной <paramref name="value"/>: измерено 5.0e-5 на отношении 1e9,
    /// 4.2e-3 на 1e10 и полный промах на 1e14.
    /// </para>
    /// <para>
    /// Главное здесь не расширение, а смена характера ошибки. Прежний путь в этой
    /// области не терял точность, а возвращал полный ноль — то есть фаза анимации
    /// и координаты текстуры сбрасывались, и это видно глазом. Теперь вместо нуля
    /// получается приближение, а на расстоянии от цели появляется полоса точности.
    /// Произвольная точность дала бы остаток при любом отношении, но она здесь
    /// неуместна: остаток от деления в игровом цикле нужен для фазы анимации и
    /// координат текстуры, где отношение величин больше 1e8 не встречается.
    /// </para>
    /// <para>
    /// Результат принудительно попадает в <c>[0; length]</c>: округление на верхней
    /// границе может дать ровно <paramref name="length"/>, а это тот же остаток,
    /// что и ноль. Договорённость о диапазоне важнее точности в одной точке.
    /// Заодно это снимает <c>−∞</c> в ответе: при длине порядка 1e-30 отношение
    /// <paramref name="value"/> к <paramref name="length"/> переполнялось, и
    /// <c>Floor</c> давал бесконечность.
    /// </para>
    /// </remarks>
    public static float Repeat(float value, float length)
    {
        if (length <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "Период должен быть больше нуля.");
        }

        double remainder = value - (Math.Floor(value / (double)length) * length);

        // Нижняя граница нужна из-за знакового нуля: у отрицательного значения
        // и крошечного периода остаток может выйти в минус на величину
        // последнего разряда, и -0 ушёл бы в дальнейшие вычисления как число.
        if (remainder <= 0.0)
        {
            return 0f;
        }

        // length соответствует самому себе, то есть его представитель в
        // полуинтервале — ноль, а не сам period.
        return remainder >= (double)length ? 0f : (float)remainder;
    }

    /// <summary>
    /// Переносит значение в диапазон 0..1 с периодом 1.
    /// </summary>
    /// <param name="value">Исходное значение.</param>
    /// <returns>Значение в диапазоне 0..1.</returns>
    public static float Repeat01(float value) => Repeat(value, 1f);

    /// <summary>
    /// Значение «туда-обратно»: 0..1..0 с периодом 2*<paramref name="length"/>.
    /// </summary>
    /// <param name="value">Исходное значение.</param>
    /// <param name="length">Половина периода.</param>
    /// <returns>Значение в диапазоне 0..1.</returns>
    /// <remarks>
    /// Деление на <c>2 * length</c> считается в <see cref="double"/>, по той же
    /// причине, что и остаток в <see cref="Repeat"/>. В <c>float</c> это деление
    /// отбрасывало дробную часть раньше, чем она доходила до остатка: при
    /// величине порядка 1e7 в <c>float</c> нет разряда меньше единицы, то есть
    /// <c>PingPong(1e8, 3)</c> давал ровно ноль, тогда как верный ответ около
    /// 0.667. Правки одного остатка здесь было бы недостаточно, и доктрина
    /// «наследует исправление» была бы неверной.
    /// </remarks>
    public static float PingPong(float value, float length)
    {
        if (length <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "Период должен быть больше нуля.");
        }

        double phase = (double)value / ((double)length * 2.0);
        phase -= Math.Floor(phase);
        return (float)(1.0 - Math.Abs((phase * 2.0) - 1.0));
    }

    /// <summary>
    /// Переносит значение из одного диапазона в другой с ограничением
    /// целевым диапазоном.
    /// </summary>
    /// <param name="value">Исходное значение.</param>
    /// <param name="fromMin">Нижняя граница исходного диапазона.</param>
    /// <param name="fromMax">Верхняя граница исходного диапазона.</param>
    /// <param name="toMin">Нижняя граница целевого диапазона.</param>
    /// <param name="toMax">Верхняя граница целевого диапазона.</param>
    /// <returns>Значение в целевом диапазоне; <paramref name="toMin"/>, если исходный диапазон пуст.</returns>
    /// <remarks>
    /// На пустом исходном диапазоне возвращается <paramref name="toMin"/>, а не
    /// бросается исключение.
    /// <para>
    /// Перенос — это параметр интерполяции, а параметр на пустом диапазоне не
    /// определён. Здесь он считается нулём, то есть берётся ближний конец целевого
    /// диапазона. Это безопасное значение: продолжение кривой в вырожденном
    /// случае всё равно произвольно, а исключение посреди игрового цикла
    /// останавливает приложение.
    /// </para>
    /// <para>
    /// Вырожденность получается именно там, где вырождены данные: у уровня с
    /// одним типом врага нижняя и верхняя границы здоровья вычисляются из
    /// списка длиной один и совпадают. Ничто в сигнатуре не сообщает, что границы
    /// обязаны различаться, поэтому падать на таких данных нельзя.
    /// </para>
    /// <para>
    /// <see cref="InverseLerp"/> на пустом диапазоне по-прежнему бросает
    /// исключение: там деление на ноль действительно является ошибкой
    /// вызывающего, и она видна сразу. Здесь метод не вызывается.
    /// </para>
    /// </remarks>
    public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
        => IsEmptyRange(fromMin, fromMax)
            ? toMin
            : LerpClamped(toMin, toMax, InverseLerp(fromMin, fromMax, value));

    /// <summary>
    /// Переносит значение из одного диапазона в другой без ограничения:
    /// значение вне исходного диапазона продолжает линейную зависимость и
    /// выходит за границы целевого.
    /// </summary>
    /// <param name="value">Исходное значение.</param>
    /// <param name="fromMin">Нижняя граница исходного диапазона.</param>
    /// <param name="fromMax">Верхняя граница исходного диапазона.</param>
    /// <param name="toMin">Нижняя граница целевого диапазона.</param>
    /// <param name="toMax">Верхняя граница целевого диапазона.</param>
    /// <returns>
    /// Линейное продолжение переноса, возможно вне целевого диапазона;
    /// <paramref name="toMin"/>, если исходный диапазон пуст.
    /// </returns>
    /// <remarks>
    /// Поведение на пустом исходном диапазоне и его обоснование описаны в
    /// <see cref="Remap"/>.
    /// </remarks>
    public static float RemapUnclamped(float value, float fromMin, float fromMax, float toMin, float toMax)
        => IsEmptyRange(fromMin, fromMax)
            ? toMin
            : LerpUnclamped(toMin, toMax, InverseLerp(fromMin, fromMax, value));

    /// <summary>
    /// Ограничивает параметр интерполяции диапазоном 0..1.
    /// </summary>
    /// <param name="t">Исходный параметр.</param>
    /// <returns>Параметр в диапазоне 0..1.</returns>
    public static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;

    /// <summary>
    /// Сжимает значение в заданный диапазон.
    /// </summary>
    /// <param name="value">Исходное значение.</param>
    /// <param name="min">Нижняя граница.</param>
    /// <param name="max">Верхняя граница.</param>
    /// <returns>Значение в диапазоне.</returns>
    /// <remarks>
    /// Реализация общая с <see cref="Scalar.Clamp"/>: две одинаковые функции
    /// под разными именами разъезжаются при правке, и вызывающий получает
    /// разные сообщения об ошибке на одном и том же входе.
    /// </remarks>
    public static float Clamp(float value, float min, float max) => Scalar.Clamp(value, min, max);
}
