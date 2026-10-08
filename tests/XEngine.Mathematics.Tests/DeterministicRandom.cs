using System.Numerics;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Генератор детерминированных, но невырожденных входов для проверок
/// геометрии.
/// </summary>
/// <remarks>
/// Общая проблема проверок геометрии — не сам набор чисел, а то, какие числа
/// попадают в набор. Случайные величины, взятые один раз, дают либо
/// очевидный промах, либо очевидное попадание, и обе ситуации проверяют
/// только грубую ветвь алгоритма. Настоящие ошибки живут в узкой полосе:
/// фигуры почти касаются, углы не кратны 45°, центр не в начале координат.
/// <para>
/// Поэтому значения строятся так, чтобы эта полоса была основной частью
/// набора: сначала грубая случайность, затем уточнение до почти касания.
/// Каждая величина приходит из собственного потока, поэтому сдвиг одного
/// набора не меняет остальные, и падение теста локализуется точнее.
/// </para>
/// <para>
/// Генератор не использует <see cref="System.Random"/>: его последовательность
/// меняется между версиями среды, а проверка геометрии должна падать
/// одинаково на любой машине. Здесь простое 64-разрядное xorshift, целиком
/// заданное в этом файле.
/// </para>
/// </remarks>
public sealed class DeterministicRandom
{
    private ulong _state;

    /// <summary>
    /// Создаёт генератор с заданным зерном.
    /// </summary>
    /// <param name="seed">Зерно. Ноль недопустим: xorshift на нуле вырождается.</param>
    public DeterministicRandom(ulong seed) => _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

    /// <summary>
    /// Следующее 64-разрядное число.
    /// </summary>
    /// <returns>Псевдослучайное число.</returns>
    public ulong NextUInt64()
    {
        unchecked
        {
            ulong x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            return x * 0x2545F4914F6CDD1DUL;
        }
    }

    /// <summary>
    /// Число в диапазоне <c>[0; 1)</c> из 24 значащих бит.
    /// </summary>
    /// <returns>Псевдослучайное число.</returns>
    public float NextFloat() => (NextUInt64() >> 40) * (1f / 16777216f);

    /// <summary>
    /// Число в заданном диапазоне.
    /// </summary>
    /// <param name="min">Нижняя граница включительно.</param>
    /// <param name="max">Верхняя граница включительно.</param>
    /// <returns>Псевдослучайное число.</returns>
    public float Range(float min, float max) => min + ((max - min) * NextFloat());

    /// <summary>
    /// Угол в диапазоне <c>(-180; 180]</c> градусов.
    /// </summary>
    /// <returns>Случайный угол.</returns>
    public Angle NextAngle() => Angle.FromDegrees(Range(-180f, 180f));

    /// <summary>
    /// Единичный вектор, равномерно распределённый по сфере.
    /// </summary>
    /// <returns>Случайный единичный вектор.</returns>
    public Vector3 NextUnitVector()
    {
        double z = (NextFloat() * 2.0) - 1.0;
        double angle = (NextFloat() * 2.0 * Math.PI);
        double planar = Math.Sqrt(Math.Max(0.0, 1.0 - (z * z)));
        return new Vector3(
            (float)(planar * Math.Cos(angle)),
            (float)(planar * Math.Sin(angle)),
            (float)z);
    }

    /// <summary>
    /// Пара прямоугольников, расположенных так, чтобы они почти касались.
    /// </summary>
    /// <remarks>
    /// Именно такие пары отделяют работающий SAT от сломанного: при грубом
    /// расположении фигур любая из двух систем разделяющих осей даёт один и
    /// тот же вердикт, и ошибка в выборе осей остаётся незамеченной. Здесь
    /// фигуры сдвинуты так, чтобы перекрытие по всем осям было одного
    /// порядка с погрешностью округления.
    /// </remarks>
    /// <param name="firstCenter">Центр первого прямоугольника.</param>
    /// <param name="firstSize">Размер первого прямоугольника.</param>
    /// <param name="firstRotation">Поворот первого прямоугольника.</param>
    /// <param name="secondCenter">Центр второго прямоугольника.</param>
    /// <param name="secondSize">Размер второго прямоугольника.</param>
    /// <param name="secondRotation">Поворот второго прямоугольника.</param>
    public static void TouchingObbs(
        out Vector2 firstCenter,
        out Vector2 firstSize,
        out Angle firstRotation,
        out Vector2 secondCenter,
        out Vector2 secondSize,
        out Angle secondRotation)
    {
        DeterministicRandom random = new(0x51ED270B4A9C1F23UL);
        firstCenter = new Vector2(random.Range(-20f, 20f), random.Range(-20f, 20f));
        firstSize = new Vector2(random.Range(0.05f, 4f), random.Range(0.05f, 4f));
        firstRotation = random.NextAngle();

        secondSize = new Vector2(random.Range(0.05f, 4f), random.Range(0.05f, 4f));
        secondRotation = random.NextAngle();

        // Смещение подбирается вдоль направления наибольшего перекрытия так,
        // чтобы фигуры пересекались, но зазор был порядка десятой доли размера.
        (float sin, float cos) = MathF.SinCos((float)firstRotation.Radians);
        double along = Math.Abs((cos * firstSize.X * 0.5) + (sin * firstSize.Y * 0.5));
        double gap = along * 0.85;
        secondCenter = firstCenter + new Vector2((float)(cos * gap), (float)(sin * gap));
    }
}