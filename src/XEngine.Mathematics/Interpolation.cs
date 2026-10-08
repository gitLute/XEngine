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
        float range = to - from;
        if (MathF.Abs(range) <= Scalar.Epsilon)
        {
            throw new ArgumentException("Диапазон интерполяции пуст.", nameof(from));
        }

        return (value - from) / range;
    }

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
    public static float MoveTowards(float current, float target, float maxStep)
    {
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
    /// <param name="smoothTime">Время достижения цели, в секундах.</param>
    /// <param name="maxSpeed">Максимальная скорость изменения.</param>
    /// <param name="deltaTime">Время кадра в секундах.</param>
    /// <param name="velocity">Скорость на предыдущем кадре. Возвращается обновлённой.</param>
    /// <returns>Новое значение.</returns>
    public static float SmoothDamp(
        float current,
        float target,
        float smoothTime,
        float maxSpeed,
        float deltaTime,
        ref float velocity)
    {
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
    public static float Repeat(float value, float length)
    {
        if (length <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "Период должен быть больше нуля.");
        }

        return value - MathF.Floor(value / length) * length;
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
    public static float PingPong(float value, float length)
    {
        if (length <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "Период должен быть больше нуля.");
        }

        float t = Repeat01(value / (2f * length));
        return 1f - MathF.Abs(t * 2f - 1f);
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
    /// <returns>Значение в целевом диапазоне.</returns>
    public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
        => LerpClamped(toMin, toMax, InverseLerp(fromMin, fromMax, value));

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
    /// <returns>Линейное продолжение переноса, возможно вне целевого диапазона.</returns>
    public static float RemapUnclamped(float value, float fromMin, float fromMax, float toMin, float toMax)
        => LerpUnclamped(toMin, toMax, InverseLerp(fromMin, fromMax, value));

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
    public static float Clamp(float value, float min, float max)
    {
        if (min > max)
        {
            throw new ArgumentException("Минимальная граница больше максимальной.", nameof(min));
        }

        return value < min ? min : value > max ? max : value;
    }
}
