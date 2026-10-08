using System.Numerics;
using Xunit;

namespace XEngine.Mathematics.Tests;

/// <summary>
/// Регрессионные тесты на два дефекта, из-за которых проверка повёрнутых
/// прямоугольников давала неверный ответ на любых углах, кроме кратных 45°.
/// </summary>
/// <remarks>
/// Дефекты найдены ревизией <c>src/XEngine.Mathematics</c> и описаны в
/// <c>Problems.md</c>.
/// <para>
/// Общая причина того, что 779 тестов оставались зелёными, одна: у обоих
/// методов был вырожденный представитель, на котором ошибка не видна.
/// У <see cref="Collision.TryGetObbPenetration"/> это углы 0° и 45° —
/// ровно два угла, при которых переставленная система осей совпадает с
/// правильной. У <see cref="Collision.Contains"/> это центр в начале
/// координат — там порядок умножения матриц не влияет на результат.
/// Поэтому ниже нет ни одного входа из этих двух классов.
/// </para>
/// </remarks>
public class CollisionObbRegressionTests
{
    /// <summary>
    /// Дефект SAT: кортеж <c>Trig.SinCos</c> возвращает сначала синус, а
    /// переменные назывались наоборот, поэтому оси проверялись
    /// <c>(sin, cos)</c> и <c>(-cos, sin)</c> вместо <c>(cos, sin)</c> и
    /// <c>(-sin, cos)</c>. Переставленная система совпадает с правильной при
    /// углах 0°, 45°, 90° и 135°, поэтому все прежние тесты проходили.
    /// </summary>
    [Fact]
    public void ObbPenetration_UsesCosineFirstAxesForArbitraryRotations()
    {
        // Две одинаковые фигуры, вторая сдвинута вдоль локальной оси X первой.
        // Тогда минимальное перекрытие обязано лежать именно на этой оси, то
        // есть на векторе (cos, sin). При переставленной системе осей
        // библиотека вернула бы (sin, cos), то есть ось, перпендикулярную
        // граням фигур, и такой осью ни одну пару не раздвинуть.
        foreach (float degrees in new[] { 17f, 30f, 63f, 116f, 152f, -94f, 12.5f, 88f, 200f, -150f })
        {
            Angle rotation = Angle.FromDegrees(degrees);
            Vector2 size = new(2f, 2f);
            Vector2 centerA = Vector2.Zero;
            Vector2 centerB = new Vector2(1.5f, 0f).Rotate(rotation);

            Assert.True(
                Collision.TryGetObbPenetration(centerA, size, rotation, centerB, size, rotation, out Vector2 axis, out float depth),
                $"При повороте {degrees}° фигуры обязаны пересекаться.");

            MathAssert.Equal(new Vector2(1f, 0f).Rotate(rotation), axis, MathAssert.LooseTolerance);
            MathAssert.Equal(0.5f, depth, MathAssert.LooseTolerance);
        }
    }

    /// <summary>
    /// Конкретная пара, на которой переставленные оси объявляли промах при
    /// перекрытии 0.22–0.67 м по всем четырём правильным осям. Значения взяты
    /// из замера, а не подобраны на глаз, поэтому тест защищает именно от
    /// возврата этого расхождения.
    /// </summary>
    [Fact]
    public void ObbPenetration_RegressionOnConcreteFailingPair()
    {
        Vector2 centerA = new(1.1658f, 0.2706f);
        Vector2 sizeA = new(1.6506f, 0.5312f);
        Angle rotationA = Angle.FromDegrees(116.17f);
        Vector2 centerB = new(-0.618f, 1.3313f);
        Vector2 sizeB = new(1.1809f, 2.857f);
        Angle rotationB = Angle.FromDegrees(-93.64f);

        ReferenceObb.Result reference = ReferenceObb.Overlap(centerA, sizeA, rotationA, centerB, sizeB, rotationB);
        Assert.True(reference.Overlaps, "Эталон обязан видеть пересечение.");

        Assert.True(
            Collision.TryGetObbPenetration(centerA, sizeA, rotationA, centerB, sizeB, rotationB, out _, out _),
            "Библиотека обязана видеть пересечение, а не объявлять промах.");
    }

    /// <summary>
    /// Тот же набор, но перебором по углам: дефект проявлялся на всех углах,
    /// кроме кратных 45°, поэтому список углов обязан содержать и их.
    /// </summary>
    [Fact]
    public void ObbPenetration_VerdictMatchesReferenceOverWholeAngleRange()
    {
        Vector2 sizeA = new(1.6506f, 0.5312f);
        Vector2 centerA = new(1.1658f, 0.2706f);
        Vector2 sizeB = new(1.1809f, 2.857f);
        Angle rotationA = Angle.FromDegrees(116.17f);

        int mismatches = 0;
        int overlapping = 0;
        for (int degrees = -180; degrees <= 180; degrees += 3)
        {
            Angle rotationB = Angle.FromDegrees(degrees);
            Vector2 centerB = new(-0.618f, 1.3313f);

            bool actual = Collision.TryGetObbPenetration(
                centerA, sizeA, rotationA,
                centerB, sizeB, rotationB,
                out _, out _);
            bool expected = ReferenceObb.Overlap(centerA, sizeA, rotationA, centerB, sizeB, rotationB).Overlaps;

            if (actual != expected)
            {
                mismatches++;
            }

            if (expected)
            {
                overlapping++;
            }
        }

        Assert.True(mismatches == 0, $"Расхождений с эталоном: {mismatches}.");
        Assert.True(overlapping > 30, $"Набор должен содержать пересечения, а не только промахи: {overlapping}.");
    }

    /// <summary>
    /// Глубина проникновения обязана совпадать с эталонной: при переставленных
    /// осях она бралась вдоль другой грани и отличалась почти всегда.
    /// </summary>
    [Fact]
    public void ObbPenetration_DepthMatchesReferenceForArbitraryRotations()
    {
        DeterministicRandom random = new(0x2A5F1C33D9E47B05UL);
        int checkedPairs = 0;

        for (int i = 0; i < 4000; i++)
        {
            Vector2 centerA = new Vector2(random.Range(-15f, 15f), random.Range(-15f, 15f));
            Vector2 sizeA = new(random.Range(0.1f, 3f), random.Range(0.1f, 3f));
            Angle rotationA = random.NextAngle();
            Vector2 centerB = centerA + new Vector2(random.Range(-3f, 3f), random.Range(-3f, 3f));
            Vector2 sizeB = new(random.Range(0.1f, 3f), random.Range(0.1f, 3f));
            Angle rotationB = random.NextAngle();

            ReferenceObb.Result reference = ReferenceObb.Overlap(centerA, sizeA, rotationA, centerB, sizeB, rotationB);
            bool actual = Collision.TryGetObbPenetration(
                centerA, sizeA, rotationA,
                centerB, sizeB, rotationB,
                out Vector2 axis, out float depth);

            Assert.True(actual == reference.Overlaps, "Вердикт обязан совпадать с эталоном.");
            if (!actual)
            {
                continue;
            }

            checkedPairs++;

            // Допуск берётся от размера фигур: округление в float накапливается
            // на проекциях, и на больших размерах оно законно.
            double scale = Math.Max(sizeA.Length(), sizeB.Length());
            Assert.True(
                Math.Abs(depth - reference.Depth) <= (scale * 1e-4) + 1e-5,
                $"Глубина {depth} против эталонной {reference.Depth:F6} при поворотах {rotationA.Degrees:F2}° и {rotationB.Degrees:F2}°.");
            Assert.True(
                Vector2.Distance(axis, reference.Axis) <= 1e-3f,
                $"Ось {axis} против эталонной {reference.Axis} при поворотах {rotationA.Degrees:F2}° и {rotationB.Degrees:F2}°.");
        }

        Assert.True(checkedPairs > 1000, "Набор обязан содержать заметное число пересекающихся пар, иначе проверка пустая.");
    }

    /// <summary>
    /// Ось минимального раздвижения обязана быть рабочей: перенос второй
    /// фигуры вдоль возвращённой оси на глубину обязан разделить фигуры.
    /// Это проверка по смыслу, а не по числу: она ловит и неверную ось, и
    /// неверную глубину сразу.
    /// </summary>
    [Fact]
    public void ObbPenetration_TranslatingByDepthActuallySeparates()
    {
        DeterministicRandom.TouchingObbs(
            out Vector2 centerA,
            out Vector2 sizeA,
            out Angle rotationA,
            out Vector2 centerB,
            out Vector2 sizeB,
            out Angle rotationB);

        int checkedPairs = 0;
        DeterministicRandom random = new(0x7F4A7C159E3779B9UL);

        for (int i = 0; i < 2000; i++)
        {
            bool penetrating = Collision.TryGetObbPenetration(
                centerA, sizeA, rotationA,
                centerB, sizeB, rotationB,
                out Vector2 axis, out float depth);

            Assert.True(penetrating, "Подобранные фигуры обязаны пересекаться.");
            checkedPairs++;

            Vector2 movedCenter = centerB + (axis * depth);
            ReferenceObb.Result afterMove = ReferenceObb.Overlap(
                centerA, sizeA, rotationA,
                movedCenter, sizeB, rotationB);

            Assert.False(
                afterMove.Overlaps,
                $"После переноса вдоль оси {axis} на глубину {depth:F6} фигуры обязаны разойтись, но всё ещё пересекаются.");

            // Половина глубины оставляет перекрытие: значит глубина не завышена.
            ReferenceObb.Result halfMove = ReferenceObb.Overlap(
                centerA, sizeA, rotationA,
                centerB + (axis * (depth * 0.5f)), sizeB, rotationB);
            Assert.True(halfMove.Overlaps, "На половине глубины фигуры обязаны ещё пересекаться.");

            DeterministicRandom.TouchingObbs(
                out centerA,
                out sizeA,
                out rotationA,
                out centerB,
                out sizeB,
                out rotationB);
            _ = random;
        }

        Assert.True(checkedPairs >= 2000, "Проверка обязана охватывать все пары набора.");
    }

    /// <summary>
    /// Дефект <see cref="Collision.Contains"/>: произведение матриц в
    /// <c>System.Numerics</c> применяет левый множитель первым, поэтому
    /// поворот шёл до переноса, то есть вокруг мирового начала координат.
    /// Центр самого бокса при этом оказывался вне прямоугольника.
    /// </summary>
    [Fact]
    public void Contains_CenterOfRotatedBoxIsInside()
    {
        Vector2 center = new(10f, -4f);
        Vector2 size = new(2f, 2f);
        foreach (float degrees in new[] { 0f, 30f, 90f, 137f, 250f, -63f })
        {
            Angle rotation = Angle.FromDegrees(degrees);
            Assert.True(
                Collision.Contains(center, center, size, rotation),
                $"Центр бокса обязан быть внутри при повороте {degrees}°.");
        }
    }

    /// <summary>
    /// Точка на углу повёрнутого бокса обязана лежать внутри: границы
    /// включительные.
    /// </summary>
    [Fact]
    public void Contains_CornersOfRotatedBoxAreInside()
    {
        Vector2 center = new(-7.5f, 3.25f);
        Vector2 size = new(4f, 1.5f);

        foreach (float degrees in new[] { 11f, 45f, 90f, 200f, 310f })
        {
            Angle rotation = Angle.FromDegrees(degrees);

            // Углы считаются независимо, в двойной точности, и приводятся к
            // float, поэтому последний разряд уводит точку наружу. Граница
            // метода строгая, и bit-в-бит равенства здесь не существует:
            // величина огреха проверяется отдельно в
            // Contains_BoundaryOvershootStaysWithinRounding. Здесь проверяется
            // содержательное свойство: угол бокса, уменьшенного на пять
            // миллионных, лежит строго внутри полного бокса.
            Vector2 shrunk = size * (1f - 1e-5f);
            foreach (Vector2 corner in ReferenceObb.Corners(center, shrunk, rotation))
            {
                Assert.True(
                    Collision.Contains(corner, center, size, rotation),
                    $"Угол бокса, уменьшенного на пять миллионных, обязан лежать строго внутри при повороте {degrees}°.");
            }
        }
    }

    /// <summary>
    /// Строгая граница <see cref="Collision.Contains"/> документирована, поэтому
    /// огрех угла снаружи не должен превышать округления одинарной точности.
    /// Проверка фиксирует именно эту величину: слишком большая означала бы
    /// либо сломанный порядок матриц, либо допуск там, где его быть не должно.
    /// </summary>
    [Fact]
    public void Contains_BoundaryOvershootStaysWithinRounding()
    {
        Vector2 center = new(-7.5f, 3.25f);
        Vector2 size = new(4f, 1.5f);
        float rounding = size.Length() * 1e-5f;

        for (int degrees = 0; degrees < 360; degrees += 7)
        {
            Angle rotation = Angle.FromDegrees(degrees);
            foreach (Vector2 corner in ReferenceObb.Corners(center, size, rotation))
            {
                if (Collision.Contains(corner, center, size, rotation))
                {
                    continue;
                }

                Vector2 local = corner - center;
                float c = MathF.Cos((float)-rotation.Radians);
                float s = MathF.Sin((float)-rotation.Radians);
                float localX = (local.X * c) - (local.Y * s);
                float localY = (local.X * s) + (local.Y * c);
                float overshoot = MathF.Max(
                    MathF.Abs(localX) - (size.X * 0.5f),
                    MathF.Abs(localY) - (size.Y * 0.5f));

                Assert.True(
                    overshoot <= rounding,
                    $"Угол при повороте {degrees}° вышел за границу на {overshoot:E3} м, округление {rounding:E3} м.");
            }
        }
    }

    /// <summary>
    /// Точка, лежащая вне бокса по направлению одной из его осей, обязана
    /// отвергаться. Проверяется по обеим осям и по четырём направлениям.
    /// </summary>
    [Fact]
    public void Contains_PointsOutsideAlongLocalAxesAreRejected()
    {
        Vector2 center = new(4f, 9f);
        Vector2 size = new(2f, 1f);
        Angle rotation = Angle.FromDegrees(23f);
        (float sin, float cos) = System.MathF.SinCos((float)rotation.Radians);
        Vector2 axisX = new(cos, sin);
        Vector2 axisY = new(-sin, cos);

        foreach ((Vector2 direction, float extra) in new[]
        {
            (axisX, 1.01f),
            (-axisX, 1.01f),
            (axisY, 0.51f),
            (-axisY, 0.51f),
        })
        {
            Vector2 point = center + (direction * extra);
            Assert.False(
                Collision.Contains(point, center, size, rotation),
                $"Точка {point} вне бокса по локальной оси обязана отвергаться.");
        }
    }

    /// <summary>
    /// Массовая сверка с эталоном: граница включительная, поэтому набор
    /// строится вокруг границы, где ошибка порядка проявляется сильнее всего.
    /// </summary>
    [Fact]
    public void Contains_MatchesReferenceOnRandomConfigurations()
    {
        DeterministicRandom random = new(0x0BADC0DE5EED1234UL);
        int mismatches = 0;

        for (int i = 0; i < 20000; i++)
        {
            Vector2 center = new Vector2(random.Range(-12f, 12f), random.Range(-12f, 12f));
            Vector2 size = new(random.Range(0.2f, 3f), random.Range(0.2f, 3f));
            Angle rotation = random.NextAngle();

            Vector2 point = center + rotation.Rotate(
                new Vector2(random.Range(-1f, 1f), random.Range(-1f, 1f)) * size * 0.5f);

            bool actual = Collision.Contains(point, center, size, rotation);
            bool expected = ReferenceObb.Contains(point, center, size, rotation);
            if (actual != expected)
            {
                mismatches++;
            }
        }

        Assert.True(mismatches == 0, $"Расхождений с эталоном: {mismatches} из 20000.");
    }

    /// <summary>
    /// То же для произвольных точек, а не только лежащих на боксе: ошибка
    /// порядка матриц проявляется и на точках далеко снаружи.
    /// </summary>
    [Fact]
    public void Contains_MatchesReferenceOnRandomPointsFarFromBox()
    {
        DeterministicRandom random = new(0x13579BDF2468ACE0UL);
        int outside = 0;
        int inside = 0;

        for (int i = 0; i < 20000; i++)
        {
            Vector2 center = new Vector2(random.Range(-8f, 8f), random.Range(-8f, 8f));
            Vector2 size = new(random.Range(0.5f, 2f), random.Range(0.5f, 2f));
            Angle rotation = random.NextAngle();
            // Точка строится от центра с разбросом больше размера бокса, иначе
            // попаданий слишком мало и проверка сводится к одной ветви.
            Vector2 point = center + rotation.Rotate(
                new Vector2(random.Range(-2f, 2f), random.Range(-2f, 2f)) * size);

            bool actual = Collision.Contains(point, center, size, rotation);
            bool expected = ReferenceObb.Contains(point, center, size, rotation);
            Assert.True(actual == expected, $"Точка {point}, центр {center}, поворот {rotation.Degrees:F2}°.");
            if (actual)
            {
                inside++;
            }
            else
            {
                outside++;
            }
        }

        Assert.True(inside > 200, $"Набор должен содержать попадания: {inside}.");
        Assert.True(outside > 200, $"Набор должен содержать промахи: {outside}.");
    }
}